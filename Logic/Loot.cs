using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using ff14bot.Managers;
using ff14bot.NeoProfiles;
using Kombatant.Enums;
using Kombatant.Helpers;
using Kombatant.Interfaces;
using Kombatant.Managers;
using Kombatant.Settings;

namespace Kombatant.Logic
{
	/// <summary>
	/// Logic for Looting.
	/// </summary>
	/// <inheritdoc cref="M:Komabatant.Interfaces.LogicExecutor"/>
	// ReSharper disable once InconsistentNaming
	internal class Loot : LogicExecutor
	{
		#region Singleton

		private static Loot _lootLogic;
		internal static Loot Instance => _lootLogic ?? (_lootLogic = new Loot());

		#endregion

		// Tracks memory-array items we have already rolled on, by (ObjectId, ItemId).
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

		// Tracks NeedGreed window slot indices we have already acted on.
		// Populated by both Pass 1 (memory-array slots) and Pass 2 (window slots).
		private readonly HashSet<int> _triedSlots = new HashSet<int>();

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

		// Max window slots to probe per loot session.  Alliance raids can have 9+ items.
		private const int NeedGreedMaxSlots = 16;

		private void ResetState()
		{
			_attemptedItems.Clear();
			_triedSlots.Clear();
		}

		/// <summary>
		/// Main task executor for the Loot logic.
		/// </summary>
		/// <returns>Returns <c>true</c> if any action was executed, otherwise <c>false</c>.</returns>
		internal new Task<bool> ExecuteLogic()
		{
			if (BotBase.Instance.IsPaused)
				return Task.FromResult(false);

			var ngWindow      = RaptureAtkUnitManager.GetWindowByName("NeedGreed");
			bool hasMemoryLoot = LootManager.HasLoot;
			// _NotificationLoot is the specific loot-chest icon; _Notification is the generic
			// notification bar we SendAction on to open it.  Check the specific one first so
			// we never mistake a party-invite or other non-loot notification for pending loot.
			bool hasLootNotif  = RaptureAtkUnitManager.GetWindowByName("_NotificationLoot") != null;

			// Reset state only when ALL three sources say there is no loot pending.
			// Previously clearing on !hasMemoryLoot alone caused a reset/reprocess loop:
			// the game clears the memory array the moment item 0 is rolled, while both the
			// NeedGreed window (items 2+) and the notification icon may still be open.
			if (!hasMemoryLoot && ngWindow == null && !hasLootNotif)
			{
				ResetState();
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot)
				return Task.FromResult(false);

			// Rate-limit ALL loot actions — including the notification click — through the
			// shared 500 ms timer.  Previously the notification click returned true before
			// reaching this check, firing on every bot tick and starving the combat rotation.
			if (!WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

			// The NeedGreed window only appears after clicking the loot notification icon.
			// Open it now if the icon is visible but the window is not yet open AND we still
			// have untried slots (avoids a spurious re-click after all 16 slots are exhausted
			// while the notification icon lingers).
			if (ngWindow == null && hasLootNotif && _triedSlots.Count < NeedGreedMaxSlots)
			{
				var notification = RaptureAtkUnitManager.GetWindowByName("_Notification");
				if (notification != null)
				{
					notification.SendAction(3, 3, 0, 3, 2, 6, 0x375B30E7);
					LogHelper.Instance.Log("[Loot] Clicked loot notification to open NeedGreed window.");
					return Task.FromResult(true);
				}
			}

			// Pass 1: items visible in the memory array carry full RollState/ItemId data.
			// Handles item 0 (and any others the game surfaces in the flat array) with
			// correct Need/Greed/Pass logic based on actual per-item roll state.
			if (hasMemoryLoot)
			{
				var rawItems = LootManager.RawLootItems;
				switch (BotBase.Instance.LootMode)
				{
					case LootMode.NeedAndGreed:
						for (int slot = 0; slot < rawItems.Length; slot++)
						{
							var item = rawItems[slot];
							if (!item.Valid || item.Rolled || _attemptedItems.Contains(Key(item)) || item.LeftRollTime <= 0) continue;
							var itemData = item.Item;
							if (itemData != null && itemData.Unique && ConditionParser.HasItem(item.ItemId)) continue;
							_attemptedItems.Add(Key(item));
							_triedSlots.Add(slot);
							if (item.RollState == RollState.UpToNeed) item.Need(slot);
							else if (item.RollState == RollState.UpToGreed) item.Greed(slot);
							else item.Pass(slot);
							return Task.FromResult(true);
						}
						break;

					case LootMode.GreedAll:
						for (int slot = 0; slot < rawItems.Length; slot++)
						{
							var item = rawItems[slot];
							if (!item.Valid || item.Rolled || _attemptedItems.Contains(Key(item)) || item.LeftRollTime <= 0) continue;
							var itemData = item.Item;
							if (itemData != null && itemData.Unique && ConditionParser.HasItem(item.ItemId)) continue;
							_attemptedItems.Add(Key(item));
							_triedSlots.Add(slot);
							if (item.RollState == RollState.UpToNeed || item.RollState == RollState.UpToGreed) item.Greed(slot);
							else item.Pass(slot);
							return Task.FromResult(true);
						}
						break;

					case LootMode.PassAll:
						for (int slot = 0; slot < rawItems.Length; slot++)
						{
							var item = rawItems[slot];
							if (!item.Valid || item.Rolled || _attemptedItems.Contains(Key(item)) || item.LeftRollTime <= 0) continue;
							_attemptedItems.Add(Key(item));
							_triedSlots.Add(slot);
							item.Pass(slot);
							return Task.FromResult(true);
						}
						break;
				}
			}

			// Pass 2: call LootFunc directly for slots 1+ while the NeedGreed window is open.
			//
			// History: an earlier "blind roll" attempt called LootFunc for all indices before
			// the NeedGreed window was ever opened (pre-notification fix) and returned true
			// for everything but never visibly rolled.  All subsequent SendAction approaches on
			// the NeedGreed window either Passed (4-pair variants) or had no effect (2-pair
			// actionCodes 1 and 3 — the game played clicking sounds but nothing was rolled).
			//
			// Key difference now: the notification fix ensures the NeedGreed window is open
			// before we reach this code, so the game's loot state machine should have all
			// items indexed and LootFunc may now work for slots 1+.
			//
			// For NeedAndGreed: try Need first (accepted for need-eligible items, rejected for
			// greed-only) then Greed fallback, then Pass as last resort.
			// LootFunc return value: true = game processed the call, false = no item at index.
			if (ngWindow != null)
			{
				for (int i = 0; i < NeedGreedMaxSlots; i++)
				{
					if (_triedSlots.Contains(i)) continue;
					_triedSlots.Add(i);

					bool result = false;
					string rolledAs = "Pass";

					switch (BotBase.Instance.LootMode)
					{
						case LootMode.NeedAndGreed:
							if (LootManager.RollByIndex(RollOption.Need, i))  { result = true; rolledAs = "Need"; }
							else if (LootManager.RollByIndex(RollOption.Greed, i)) { result = true; rolledAs = "Greed"; }
							else if (LootManager.RollByIndex(RollOption.Pass, i)) { result = true; rolledAs = "Pass"; }
							break;
						case LootMode.GreedAll:
							if (LootManager.RollByIndex(RollOption.Greed, i)) { result = true; rolledAs = "Greed"; }
							else if (LootManager.RollByIndex(RollOption.Pass, i)) { result = true; rolledAs = "Pass"; }
							break;
						case LootMode.PassAll:
							if (LootManager.RollByIndex(RollOption.Pass, i)) { result = true; rolledAs = "Pass"; }
							break;
					}

					// LootFunc returns true for every index (including empty slots) — "true" means
				// the game accepted the call, not necessarily that a real item was there.
				// Real items produce a visible roll; empty slots are silent no-ops.
				LogHelper.Instance.Log(result
						? $"[Loot] LootFunc accepted {rolledAs} for slot {i} (real item or silent no-op)."
						: $"[Loot] LootFunc rejected slot {i}.");

					if (result && BotBase.Instance.ShowLootNotification)
						OverlayHelper.Instance.AddToast($"Rolled [{rolledAs}] slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));

					return Task.FromResult(true);
				}
			}

			return Task.FromResult(false);
		}
	}
}
