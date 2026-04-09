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

		// Tracks slot indices we have already attempted to roll on in the current loot window.
		// Index is unique per loot slot (0-15); cleared when the loot window closes so new
		// windows (and new loot appearing mid-dungeon) are always picked up fresh.
		private readonly HashSet<uint> _attemptedIndices = new HashSet<uint>();

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
				_attemptedIndices.Clear();
				return Task.FromResult(false);
			}

			if (BotBase.Instance.LootMode == LootMode.DontLoot || !WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

			switch (BotBase.Instance.LootMode)
			{
				case LootMode.NeedAndGreed:
					var need = LootManager.AvailableLoots.FirstOrDefault(i =>
						!i.Rolled && !_attemptedIndices.Contains(i.Index) &&
						i.LeftRollTime > 0 && !(i.Item.Unique && ConditionParser.HasItem(i.ItemId)));
					if (need.Valid)
					{
						_attemptedIndices.Add(need.Index);
						if (need.RollState == RollState.UpToNeed) need.Need();
						else if (need.RollState == RollState.UpToGreed) need.Greed();
						else need.Pass();
						return Task.FromResult(true);
					}
					break;

				case LootMode.GreedAll:
					var greed = LootManager.AvailableLoots.FirstOrDefault(i =>
						!i.Rolled && !_attemptedIndices.Contains(i.Index) &&
						i.LeftRollTime > 0 && !(i.Item.Unique && ConditionParser.HasItem(i.ItemId)));
					if (greed.Valid)
					{
						_attemptedIndices.Add(greed.Index);
						if (greed.RollState == RollState.UpToNeed || greed.RollState == RollState.UpToGreed) greed.Greed();
						else greed.Pass();
						return Task.FromResult(true);
					}
					break;

				case LootMode.PassAll:
					var pass = LootManager.AvailableLoots.FirstOrDefault(i =>
						!i.Rolled && !_attemptedIndices.Contains(i.Index) && i.LeftRollTime > 0);
					if (pass.Valid)
					{
						_attemptedIndices.Add(pass.Index);
						pass.Pass();
						return Task.FromResult(true);
					}
					break;
			}

			return Task.FromResult(false);
		}
	}
}
