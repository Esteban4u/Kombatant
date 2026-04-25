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

		// Tracks memory-array items we have rolled on, by (ObjectId, ItemId).
		// Cleared when the loot window closes.
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

		// Tracks NeedGreed window item indices where we have already sent a Need action.
		// Phase 1 of the window pass.  Cleared when loot window closes.
		private readonly HashSet<int> _ngNeedTried = new HashSet<int>();

		// Tracks NeedGreed window item indices where we have already sent a Greed/Pass action.
		// Phase 2 of the window pass.  Cleared when loot window closes.
		private readonly HashSet<int> _ngGreedTried = new HashSet<int>();

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

		// Max window slots to probe.  The game silently ignores ClickItem for out-of-range indices,
		// so using a fixed cap avoids needing to read the window's element array.
		private const int NeedGreedMaxSlots = 8;

		/// <summary>
		/// Main task executor for the Loot logic.
		/// </summary>
		/// <returns>Returns <c>true</c> if any action was executed, otherwise <c>false</c>.</returns>
		internal new Task<bool> ExecuteLogic()
		{
			if (BotBase.Instance.IsPaused)
				return Task.FromResult(false);

			if (!LootManager.HasLoot)
			{
				_attemptedItems.Clear();
				_ngNeedTried.Clear();
				_ngGreedTried.Clear();
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot || !WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

			// Pass 1: items visible in the memory array have full struct data (RollState, ItemId, etc.).
			// This correctly handles item 0 and any other items the game surfaces in the flat array.
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
						_ngNeedTried.Add(slot);
						_ngGreedTried.Add(slot);
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
						_ngNeedTried.Add(slot);
						_ngGreedTried.Add(slot);
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
						_ngNeedTried.Add(slot);
						_ngGreedTried.Add(slot);
						item.Pass(slot);
						return Task.FromResult(true);
					}
					break;
			}

			// Pass 2: items visible in the NeedGreed UI window but absent from the memory array.
			// Items 2+ in group loot are stored at dynamic per-item pointers and do not appear
			// in the flat array at LootsAddr+0x10.  We drive the window directly via SendAction.
			//
			// NeedGreed window SendAction roll-option encoding (first pair value):
			//   0 = Need,  1 = Greed,  2 = Pass
			//
			// For NeedAndGreed mode we run two sub-passes:
			//   Sub-pass A (Need): send Need for every slot.  Slots where Need is ineligible are
			//                      silently rejected by the game; the item stays unrolled.
			//   Sub-pass B (Greed): send Greed for every slot.  Already-Needed items are ignored;
			//                       Need-rejected items now roll Greed.
			// Out-of-range slot indices (beyond actual item count) are silently ignored by the game.
			var ngWindow = RaptureAtkUnitManager.GetWindowByName("NeedGreed");
			if (ngWindow != null)
			{
				// Sub-pass A: Need (NeedAndGreed mode only)
				if (BotBase.Instance.LootMode == LootMode.NeedAndGreed)
				{
					for (int i = 0; i < NeedGreedMaxSlots; i++)
					{
						if (_ngNeedTried.Contains(i)) continue;
						_ngNeedTried.Add(i);
						ngWindow.SendAction(2, 3, 0, 4, (ulong)i);          // ClickItem(i)
						ngWindow.SendAction(4, 3, 0, 4, 0, 4, 0, 3, 1);    // Need
						LogHelper.Instance.Log($"Sent Need via NeedGreed window for slot {i}.");
						if (BotBase.Instance.ShowLootNotification)
							OverlayHelper.Instance.AddToast($"Rolled [Need] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
						return Task.FromResult(true);
					}
				}

				// Sub-pass B: Greed (NeedAndGreed / GreedAll) or Pass (PassAll)
				for (int i = 0; i < NeedGreedMaxSlots; i++)
				{
					if (_ngGreedTried.Contains(i)) continue;
					_ngGreedTried.Add(i);
					ngWindow.SendAction(2, 3, 0, 4, (ulong)i);  // ClickItem(i)

					switch (BotBase.Instance.LootMode)
					{
						case LootMode.NeedAndGreed:
						case LootMode.GreedAll:
							ngWindow.SendAction(4, 3, 1, 4, 0, 4, 0, 3, 1);    // Greed
							LogHelper.Instance.Log($"Sent Greed via NeedGreed window for slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Greed] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
						case LootMode.PassAll:
							ngWindow.SendAction(4, 3, 2, 4, 0, 4, 0, 3, 1);    // Pass
							LogHelper.Instance.Log($"Sent Pass via NeedGreed window for slot {i}.");
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
