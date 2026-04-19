using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
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

		// Tracks items visible in the memory array that we have already rolled on, by (ObjectId, ItemId).
		// Cleared when the loot window closes.
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

		// Tracks Roll() indices (0-15) that we have already attempted via the blind pass.
		// The game may surface multiple loot items for the same window at Roll() indices > 0
		// even when they don't appear as valid slots in the memory array read.
		// Cleared when the loot window closes.
		private readonly HashSet<int> _triedBlindIndices = new HashSet<int>();

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

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
				_triedBlindIndices.Clear();
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot || !WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

			var rawItems = LootManager.RawLootItems;

			// Diagnostic: dump the full array state.
			var sb = new System.Text.StringBuilder();
			sb.Append($"[LootDiag] AttemptedKeys={_attemptedItems.Count} BlindTried={_triedBlindIndices.Count} Slots: ");
			for (int d = 0; d < rawItems.Length; d++)
			{
				var di = rawItems[d];
				if (di.ObjectId == 0 && di.ItemId == 0) continue;
				sb.Append($"[{d}] ObjId={di.ObjectId} ItemId={di.ItemId} RS={di.RollState} RO={di.RolledState} T={di.LeftRollTime:F1} V={di.Valid} Rolled={di.Rolled} Tried={_attemptedItems.Contains(Key(di))} | ");
			}
			LogHelper.Instance.Log(sb.ToString());

			// Pass 1 — memory-visible items: use full item data to roll correctly.
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
						_triedBlindIndices.Add(slot); // so blind pass skips this index
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
						_triedBlindIndices.Add(slot);
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
						_triedBlindIndices.Add(slot);
						item.Pass(slot);
						return Task.FromResult(true);
					}
					break;
			}

			// Pass 2 — blind roll: try Roll() indices not yet attempted, in case the game surfaces
			// items at indices > 0 that don't appear as valid slots in our memory array read.
			// Try options highest-to-lowest (Need > Greed > Pass); the game returns false for
			// options the item doesn't support, so we naturally land on the correct roll tier.
			for (int blindIdx = 0; blindIdx < 16; blindIdx++)
			{
				if (_triedBlindIndices.Contains(blindIdx)) continue;
				_triedBlindIndices.Add(blindIdx);

				bool rolled = false;
				RollOption rolledOption = RollOption.Pass;

				switch (BotBase.Instance.LootMode)
				{
					case LootMode.NeedAndGreed:
						if (LootManager.RollBlind(RollOption.Need, blindIdx)) { rolledOption = RollOption.Need; rolled = true; }
						else if (LootManager.RollBlind(RollOption.Greed, blindIdx)) { rolledOption = RollOption.Greed; rolled = true; }
						else if (LootManager.RollBlind(RollOption.Pass, blindIdx)) { rolledOption = RollOption.Pass; rolled = true; }
						break;
					case LootMode.GreedAll:
						if (LootManager.RollBlind(RollOption.Greed, blindIdx)) { rolledOption = RollOption.Greed; rolled = true; }
						else if (LootManager.RollBlind(RollOption.Pass, blindIdx)) { rolledOption = RollOption.Pass; rolled = true; }
						break;
					case LootMode.PassAll:
						if (LootManager.RollBlind(RollOption.Pass, blindIdx)) { rolledOption = RollOption.Pass; rolled = true; }
						break;
				}

				if (rolled)
				{
					LogHelper.Instance.Log($"Blind-rolled {rolledOption} on loot index {blindIdx}.");
					if (BotBase.Instance.ShowLootNotification)
						OverlayHelper.Instance.AddToast($"Blind-rolled [{rolledOption}] index {blindIdx}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
					return Task.FromResult(true);
				}
			}

			return Task.FromResult(false);
		}
	}
}
