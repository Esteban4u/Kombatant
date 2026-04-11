using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		// Tracks items we have already attempted to roll on this loot window, by (ObjectId, ItemId).
		// Slot-position tracking was unreliable because the game compacts the array after each roll,
		// causing the next real item to land at slot 0 where it would be skipped.
		// Composite key handles shared ObjectId across items from the same source.
		// Cleared when the loot window closes so new windows start fresh.
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

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
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot || !WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

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
						item.Pass(slot);
						return Task.FromResult(true);
					}
					break;
			}

			return Task.FromResult(false);
		}
	}
}
