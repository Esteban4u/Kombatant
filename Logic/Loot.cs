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

		// Tracks memory-array items already rolled on, by (ObjectId, ItemId).
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

		// Tracks NeedGreed window slot indices already acted on this session.
		private readonly HashSet<int> _triedSlots = new HashSet<int>();

		// Session expiry: the window is kept open and new items are processed until this time.
		// Reset when all loot sources clear.  Extended by 30 s whenever a new item is found
		// (handles dungeon sub-boss drops without hard-coding a duration for whole runs).
		private DateTime _sessionExpiry = DateTime.MinValue;
		private static readonly TimeSpan SessionDuration = TimeSpan.FromSeconds(30);
		private bool SessionActive => DateTime.UtcNow < _sessionExpiry;

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

		// Max window slots to probe per session.  Alliance raids cap at 16 visible slots.
		private const int NeedGreedMaxSlots = 16;

		private void ResetState()
		{
			_attemptedItems.Clear();
			_triedSlots.Clear();
			_sessionExpiry = DateTime.MinValue;
		}

		private void ExtendSession()
		{
			_sessionExpiry = DateTime.UtcNow + SessionDuration;
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
			bool hasLootNotif  = RaptureAtkUnitManager.GetWindowByName("_NotificationLoot") != null;

			// Reset only when every loot source is gone.
			if (!hasMemoryLoot && ngWindow == null && !hasLootNotif)
			{
				ResetState();
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot)
				return Task.FromResult(false);

			if (!WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

			// Open the NeedGreed window from the notification icon when:
			//   • the window is not yet open, AND
			//   • the loot notification icon is visible, AND
			//   • either the session just started (no expiry set yet) or the session is still
			//     active with untried slots remaining.
			// The session-active + untried-slots guard prevents reopening the window after the
			// user closes it once all items have been rolled and the 30 s window has elapsed.
			if (ngWindow == null && hasLootNotif)
			{
				bool sessionNotStarted = _sessionExpiry == DateTime.MinValue;
				bool shouldOpen = sessionNotStarted || (SessionActive && _triedSlots.Count < NeedGreedMaxSlots);
				if (shouldOpen)
				{
					var notification = RaptureAtkUnitManager.GetWindowByName("_Notification");
					if (notification != null)
					{
						notification.SendAction(3, 3, 0, 3, 2, 6, 0x375B30E7);
						ExtendSession();
						LogHelper.Instance.Log("[Loot] Clicked loot notification to open NeedGreed window.");
						return Task.FromResult(true);
					}
				}
			}

			// Pass 1: items in the memory array carry full RollState/ItemId data.
			// When a genuinely new item is found (not in _attemptedItems), _triedSlots is
			// cleared so Pass 2 rescans all window slots — this handles dungeon sub-boss drops
			// that add items at new window positions while old items stay at their original slots.
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
							_triedSlots.Clear();
							_triedSlots.Add(slot);
							ExtendSession();
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
							_triedSlots.Clear();
							_triedSlots.Add(slot);
							ExtendSession();
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
							_triedSlots.Clear();
							_triedSlots.Add(slot);
							ExtendSession();
							item.Pass(slot);
							return Task.FromResult(true);
						}
						break;
				}
			}

			// Pass 2: items visible in the NeedGreed UI window but absent from the memory array.
			// Items 2+ are at dynamic per-item pointers and do not appear in the flat array at
			// LootsAddr+0x10.  LootFunc (used in Pass 1) always returns true for any index and
			// does NOT roll items 2+, so we drive the window directly via AtkAddonControl.SendAction.
			//
			// Confirmed SendAction encodings (empirically verified):
			//   ClickItem(i)     : SendAction(2,  3, 0, 4, i)               — selects item i
			//   Pass             : ClickItem(i) + SendAction(4, 3, 2, 4, 0, 4, 0, 3, 1)
			//                      (opens SelectYesno; auto-confirmed by Convenience.cs)
			//   Greed (attempt)  : SendAction(2,  3, 1, 4, i)               — 2-pair, action 1
			//     The 4-pair format with ANY Pair-1 value of (3,2) always executes Pass regardless
			//     of other parameters.  The 2-pair format encodes the item index directly and
			//     matches the ClickItem pattern (action 0 = select, action 1 = Greed hypothesis).
			//
			// One slot is processed per 500 ms WaitTimer tick.
			if (ngWindow != null && SessionActive)
			{
				for (int i = 0; i < NeedGreedMaxSlots; i++)
				{
					if (_triedSlots.Contains(i)) continue;
					_triedSlots.Add(i);

					switch (BotBase.Instance.LootMode)
					{
						case LootMode.NeedAndGreed:
						case LootMode.GreedAll:
							// 2-pair: action 1, item index i — Greed hypothesis
							ngWindow.SendAction(2, 3, 1, 4, (ulong)i);
							LogHelper.Instance.Log($"[Loot] Sent Greed (2-pair action 1) for window slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Greed] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
						case LootMode.PassAll:
							// ClickItem + 4-pair Pass — confirmed working
							ngWindow.SendAction(2, 3, 0, 4, (ulong)i);
							ngWindow.SendAction(4, 3, 2, 4, 0, 4, 0, 3, 1);
							LogHelper.Instance.Log($"[Loot] Sent Pass (ClickItem + 4-pair) for window slot {i}.");
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
