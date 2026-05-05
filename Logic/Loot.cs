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
			// Open it now if the icon is visible but the window is not yet open.
			if (ngWindow == null && hasLootNotif)
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

			// Pass 2: items visible in the NeedGreed UI window but absent from the memory array.
			// Items 2+ in group loot are stored at dynamic per-item pointers, not in the flat
			// array at LootsAddr+0x10.
			//
			// SendAction encoding — determined empirically over multiple in-game tests:
			//
			//   Known working:
			//     ClickItem (select):  SendAction(2, [3,0], [4,slot])     — actionCode=0
			//     Pass (confirmed):    ClickItem + SendAction(4-pair)     — triggers SelectYesno
			//     ALL 4-pair variants (any Pair1, Pair2, Pair4 value)     → always Pass
			//
			//   Tested, no effect (silent reject — consistent with Need on greed-only items):
			//     SendAction(2, [3,1], [4,slot])                          — actionCode=1
			//
			//   Current hypothesis — Greed:
			//     SendAction(2, [3,3], [4,slot])                          — actionCode=3
			//
			//   For NeedAndGreed mode both are sent in the same tick:
			//     actionCode=1 first (Need) — accepted if item is need-eligible, else rejected
			//     actionCode=3 second (Greed) — accepted for greed-only items; ignored if
			//     item was already Needed (game ignores duplicate/downgrade rolls).
			if (ngWindow != null)
			{
				for (int i = 0; i < NeedGreedMaxSlots; i++)
				{
					if (_triedSlots.Contains(i)) continue;
					_triedSlots.Add(i);

					switch (BotBase.Instance.LootMode)
					{
						case LootMode.NeedAndGreed:
							// Send Need then Greed in the same tick.
							// Need is accepted for need-eligible items; Greed handles the rest.
							ngWindow.SendAction(2, 3, 1, 4, (ulong)i);  // Need  (actionCode=1)
							ngWindow.SendAction(2, 3, 3, 4, (ulong)i);  // Greed (actionCode=3)
							LogHelper.Instance.Log($"[Loot] Sent Need+Greed (actionCode=1,3) via NeedGreed window for slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Need/Greed] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
						case LootMode.GreedAll:
							ngWindow.SendAction(2, 3, 3, 4, (ulong)i);  // Greed (actionCode=3)
							LogHelper.Instance.Log($"[Loot] Sent Greed (actionCode=3) via NeedGreed window for slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Greed] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
						case LootMode.PassAll:
							// 4-pair format confirmed Pass (triggers SelectYesno, auto-confirmed by Convenience).
							ngWindow.SendAction(2, 3, 0, 4, (ulong)i);        // ClickItem first
							ngWindow.SendAction(4, 3, 2, 4, 0, 4, 0, 3, 1);  // Pass
							LogHelper.Instance.Log($"[Loot] Sent Pass via NeedGreed window for slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Pass] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
					}
					return Task.FromResult(true);
				}
			}

			return Task.FromResult(false);
		}
	}
}
