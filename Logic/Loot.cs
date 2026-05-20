using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using ff14bot.Managers;
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

		// Tracks (ObjectId, ItemId) pairs already acted on so we don't re-roll after the game
		// briefly keeps a slot valid while the roll result propagates.
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

		private const int LootSlots = 16;

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

			if (BotBase.Instance.LootMode == LootMode.DontLoot)
				return Task.FromResult(false);

			if (!WaitHelper.Instance.IsDoneWaiting("LootTimer", TimeSpan.FromMilliseconds(500)))
				return Task.FromResult(false);

			var rawItems = LootManager.RawLootItems;

			for (int i = 0; i < LootSlots; i++)
			{
				var item = rawItems[i];
				if (!item.Valid || item.Rolled || _attemptedItems.Contains(Key(item)) || item.LeftRollTime <= 0)
					continue;

				_attemptedItems.Add(Key(item));
				var itemData = item.Item;
				var itemName = itemData?.CurrentLocaleName ?? $"ItemId:{item.ItemId}";

				RollOption[] actions;
				switch (BotBase.Instance.LootMode)
				{
					case LootMode.NeedAndGreed:
						if (item.RollState == RollState.UpToNeed)
							actions = new[] { RollOption.Need, RollOption.Greed, RollOption.Pass };
						else if (item.RollState == RollState.UpToGreed)
							actions = new[] { RollOption.Greed, RollOption.Pass };
						else
							actions = new[] { RollOption.Pass };
						break;

					case LootMode.GreedAll:
						if (item.RollState == RollState.UpToNeed || item.RollState == RollState.UpToGreed)
							actions = new[] { RollOption.Greed, RollOption.Pass };
						else
							actions = new[] { RollOption.Pass };
						break;

					case LootMode.PassAll:
					default:
						actions = new[] { RollOption.Pass };
						break;
				}

				RollOption succeededAction = RollOption.Pass;
				bool rolled = false;
				foreach (var action in actions)
				{
					if (LootManager.RollDirect(action, i))
					{
						succeededAction = action;
						rolled = true;
						break;
					}
				}

				if (rolled)
				{
					LogHelper.Instance.Log($"[Loot] Rolled {succeededAction} for {itemName} (slot {i}).");
					if (BotBase.Instance.ShowLootNotification)
						OverlayHelper.Instance.AddToast($"Rolled [{succeededAction}] for {itemName}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
				}
				else
				{
					LogHelper.Instance.Log($"[Loot] All roll options failed for {itemName} (slot {i}).");
				}

				return Task.FromResult(true);
			}

			return Task.FromResult(false);
		}
	}
}
