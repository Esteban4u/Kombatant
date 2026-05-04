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

		// Max window slots to probe per loot session.
		private const int NeedGreedMaxSlots = 8;

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

			// The NeedGreed window only appears after the player clicks the loot notification icon.
			// Check for that icon and open the window if it is not already open.
			var ngWindow = RaptureAtkUnitManager.GetWindowByName("NeedGreed");
			if (ngWindow == null)
			{
				var notification = RaptureAtkUnitManager.GetWindowByName("_Notification");
				if (notification != null)
				{
					// Same call LlamaLibrary GeneralFunctions.PassOnAllLoot() uses to open NeedGreed.
					notification.SendAction(3, 3, 0, 3, 2, 6, 0x375B30E7);
					LogHelper.Instance.Log("[Loot] Clicked loot notification to open NeedGreed window.");
					return Task.FromResult(true);
				}
			}

			bool hasMemoryLoot = LootManager.HasLoot;

			// Clear state only when both the memory array and the window indicate no loot.
			// The game clears the memory array immediately after item 0 is rolled, while the
			// window (with items 2+) may still be open.
			if (!hasMemoryLoot && ngWindow == null)
			{
				ResetState();
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot)
				return Task.FromResult(false);

			if (!WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

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
			// SendAction encoding — determined empirically:
			//   ClickItem  : SendAction(2, [3,0], [4,slotIndex])          — selects the item
			//   Roll       : SendAction(4, [3,eventId], [4,0], [4,0], [3,1])
			//
			//   eventId in Pair 1:
			//     2 = Pass  (confirmed: LlamaLibrary PassItem uses this, triggers SelectYesno)
			//     1 = Greed (hypothesis: next candidate after 2=Pass confirmed)
			//
			// Note: varying Pair 2 or Pair 4 while keeping eventId=2 always produced Pass,
			// confirming eventId (Pair 1) is the discriminator, not Pair 4.
			if (ngWindow != null)
			{
				for (int i = 0; i < NeedGreedMaxSlots; i++)
				{
					if (_triedSlots.Contains(i)) continue;
					_triedSlots.Add(i);

					ngWindow.SendAction(2, 3, 0, 4, (ulong)i);  // ClickItem(i)

					switch (BotBase.Instance.LootMode)
					{
						case LootMode.NeedAndGreed:
						case LootMode.GreedAll:
							// eventId=1 is the next untested candidate after 2=Pass.
							ngWindow.SendAction(4, 3, 1, 4, 0, 4, 0, 3, 1);
							LogHelper.Instance.Log($"[Loot] Sent Greed (eventId=1) via NeedGreed window for slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Greed] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
						case LootMode.PassAll:
							// eventId=2 confirmed Pass.
							ngWindow.SendAction(4, 3, 2, 4, 0, 4, 0, 3, 1);
							LogHelper.Instance.Log($"[Loot] Sent Pass (eventId=2) via NeedGreed window for slot {i}.");
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
