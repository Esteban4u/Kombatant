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

		// Items successfully rolled — never retried.
		private readonly HashSet<(uint, uint)> _attemptedItems = new HashSet<(uint, uint)>();

		// Number of failed roll attempts per item. Each tick advances one step through
		// the Need→Greed→Pass fallback chain. Item is abandoned once all options are tried.
		private readonly Dictionary<(uint, uint), int> _failCount = new Dictionary<(uint, uint), int>();

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

		private const int LootSlots = 16;

		private void ResetState()
		{
			_attemptedItems.Clear();
			_failCount.Clear();
		}

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
				ResetState();
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
				if (!item.Valid || item.Rolled || item.LeftRollTime <= 0)
					continue;

				var key = Key(item);
				if (_attemptedItems.Contains(key))
					continue;

				var itemData = item.Item;
				var itemName = itemData?.CurrentLocaleName ?? $"ItemId:{item.ItemId}";

				RollOption[] options;
				switch (BotBase.Instance.LootMode)
				{
					case LootMode.NeedAndGreed:
						if (item.RollState == RollState.UpToNeed)
							options = new[] { RollOption.Need, RollOption.Greed, RollOption.Pass };
						else if (item.RollState == RollState.UpToGreed)
							options = new[] { RollOption.Greed, RollOption.Pass };
						else
							options = new[] { RollOption.Pass };
						break;

					case LootMode.GreedAll:
						if (item.RollState == RollState.UpToNeed || item.RollState == RollState.UpToGreed)
							options = new[] { RollOption.Greed, RollOption.Pass };
						else
							options = new[] { RollOption.Pass };
						break;

					case LootMode.PassAll:
					default:
						options = new[] { RollOption.Pass };
						break;
				}

				int fails = _failCount.TryGetValue(key, out int f) ? f : 0;

				if (fails >= options.Length)
				{
					// All options exhausted across previous ticks — give up on this item.
					_attemptedItems.Add(key);
					_failCount.Remove(key);
					LogHelper.Instance.Log($"[Loot] All roll options exhausted for {itemName} (slot {i}), skipping.");
					return Task.FromResult(true);
				}

				var action = options[fails];
				bool result = LootManager.RollDirect(action, i);

				if (result)
				{
					_attemptedItems.Add(key);
					_failCount.Remove(key);
					LogHelper.Instance.Log($"[Loot] Rolled {action} for {itemName} (slot {i}).");
					if (BotBase.Instance.ShowLootNotification)
						OverlayHelper.Instance.AddToast($"Rolled [{action}] for {itemName}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
				}
				else
				{
					_failCount[key] = fails + 1;
					LogHelper.Instance.Log($"[Loot] {action} rejected for {itemName} (slot {i}), will retry next tick.");
				}

				return Task.FromResult(true);
			}

			return Task.FromResult(false);
		}
	}
}
