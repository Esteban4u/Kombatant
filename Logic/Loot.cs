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

		// Max window slots to probe per loot session.  The game silently ignores ClickItem
		// for out-of-range indices, so a fixed cap is safe.
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

			// Determine loot availability from both sources.
			// BUG FIX: clearing state when ONLY the memory array is empty caused sub-pass B
			// (Greed) to never run — the game clears item 0 from the array immediately after
			// rolling, while the NeedGreed window (with items 2+) is still open.
			// We now clear only when BOTH sources indicate no pending loot.
			var ngWindow = RaptureAtkUnitManager.GetWindowByName("NeedGreed");
			bool hasMemoryLoot = LootManager.HasLoot;

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
			// correct Need/Greed/Pass logic.
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

			// Pass 2: items visible in the NeedGreed UI window but absent from the memory
			// array.  Items 2+ in group loot are stored at dynamic per-item pointers and do
			// not appear in the flat array at LootsAddr+0x10.  We drive the window directly
			// via AtkAddonControl.SendAction.
			//
			// SendAction layout for a roll: (pairCount=4)
			//   Pair 1  (3, 2)         – event type 2 = roll
			//   Pair 2  (4, 0)         – always 0 (matches LlamaLibrary PassItem)
			//   Pair 3  (4, 0)         – item-id (0 = game uses the item selected by ClickItem)
			//   Pair 4  (3, rollType)  – roll type: 1=Pass, 2=Greed  (confirmed by in-game behaviour)
			//
			// Evidence: changing Pair 2 while leaving Pair 4=(3,1) produced Pass for every item,
			// confirming Pair 4 is the roll type and Pair 2 is not.
			// LlamaLibrary PassItem uses (3,1) in Pair 4 and rolls Pass — confirmed working.
			//
			// For NeedAndGreed mode we use Greed here.  Need eligibility cannot be determined
			// without reading window elements (TwoInt, unavailable in this compilation context).
			// Greed is safe for all rollable items and avoids a two-phase loop that routinely
			// runs out of time before the window closes.
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
							ngWindow.SendAction(4, 3, 2, 4, 0, 4, 0, 3, 2);  // Greed (rollType=2)
							LogHelper.Instance.Log($"[Loot] Sent Greed via NeedGreed window for slot {i}.");
							if (BotBase.Instance.ShowLootNotification)
								OverlayHelper.Instance.AddToast($"Rolled [Greed] window slot {i}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
							break;
						case LootMode.PassAll:
							ngWindow.SendAction(4, 3, 2, 4, 0, 4, 0, 3, 1);  // Pass (rollType=1)
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
