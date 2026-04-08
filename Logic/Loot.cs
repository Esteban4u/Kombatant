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

		/// <summary>
		/// Main task executor for the Loot logic.
		/// </summary>
		/// <returns>Returns <c>true</c> if any action was executed, otherwise <c>false</c>.</returns>
		internal new Task<bool> ExecuteLogic()
		{
			if (BotBase.Instance.IsPaused)
			{
				return Task.FromResult(false);
			}

			if (!LootManager.HasLoot || BotBase.Instance.LootMode == LootMode.DontLoot || !WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
			{
				return Task.FromResult(false);
			}

			switch (BotBase.Instance.LootMode)
			{
				case LootMode.NeedAndGreed:
					var needItems = LootManager.AvailableLoots.Where(i => !i.Rolled && i.LeftRollTime > 0 && !(i.Item.Unique && ConditionParser.HasItem(i.ItemId))).ToList();
					if (needItems.Any())
					{
						foreach (var item in needItems)
						{
							if (item.RollState == RollState.UpToNeed) item.Need();
							else if (item.RollState == RollState.UpToGreed) item.Greed();
							else item.Pass();
						}
						return Task.FromResult(true);
					}
					break;

				case LootMode.GreedAll:
					var greedItems = LootManager.AvailableLoots.Where(i => !i.Rolled && i.LeftRollTime > 0 && !(i.Item.Unique && ConditionParser.HasItem(i.ItemId))).ToList();
					if (greedItems.Any())
					{
						foreach (var item in greedItems)
						{
							if (item.RollState == RollState.UpToNeed || item.RollState == RollState.UpToGreed) item.Greed();
							else item.Pass();
						}
						return Task.FromResult(true);
					}
					break;

				case LootMode.PassAll:
					var passItems = LootManager.AvailableLoots.Where(i => !i.Rolled && i.LeftRollTime > 0).ToList();
					if (passItems.Any())
					{
						foreach (var item in passItems)
							item.Pass();
						return Task.FromResult(true);
					}
					break;
			}

			return Task.FromResult(false);
		}
	}
}
