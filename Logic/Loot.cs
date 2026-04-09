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

		// Tracks which raw array slots (0-15) we have already attempted to roll on.
		// Uses the array position in RawLootItems rather than any field inside the struct,
		// since ObjectId is shared across items from the same source and Index (0x3C) is
		// unreliable. Cleared when the loot window closes so new windows start fresh.
		private readonly HashSet<int> _attemptedSlots = new HashSet<int>();

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
				_attemptedSlots.Clear();
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
						if (!item.Valid || item.Rolled || _attemptedSlots.Contains(slot) || item.LeftRollTime <= 0) continue;
						if (item.Item.Unique && ConditionParser.HasItem(item.ItemId)) continue;
						_attemptedSlots.Add(slot);
						if (item.RollState == RollState.UpToNeed) item.Need();
						else if (item.RollState == RollState.UpToGreed) item.Greed();
						else item.Pass();
						return Task.FromResult(true);
					}
					break;

				case LootMode.GreedAll:
					for (int slot = 0; slot < rawItems.Length; slot++)
					{
						var item = rawItems[slot];
						if (!item.Valid || item.Rolled || _attemptedSlots.Contains(slot) || item.LeftRollTime <= 0) continue;
						if (item.Item.Unique && ConditionParser.HasItem(item.ItemId)) continue;
						_attemptedSlots.Add(slot);
						if (item.RollState == RollState.UpToNeed || item.RollState == RollState.UpToGreed) item.Greed();
						else item.Pass();
						return Task.FromResult(true);
					}
					break;

				case LootMode.PassAll:
					for (int slot = 0; slot < rawItems.Length; slot++)
					{
						var item = rawItems[slot];
						if (!item.Valid || item.Rolled || _attemptedSlots.Contains(slot) || item.LeftRollTime <= 0) continue;
						_attemptedSlots.Add(slot);
						item.Pass();
						return Task.FromResult(true);
					}
					break;
			}

			return Task.FromResult(false);
		}
	}
}
