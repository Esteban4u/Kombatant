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

		// Number of failed roll attempts per item. Each option is tried AttemptsPerOption
		// times before advancing to the next fallback. Item is abandoned once all options
		// and all retries are exhausted.
		private readonly Dictionary<(uint, uint), int> _failCount = new Dictionary<(uint, uint), int>();

		// After RollDirect reports success, watch the item for a few more ticks to confirm
		// RolledState actually advances server-side. RollDirect has been observed to return
		// true for rolls the server silently drops (e.g. Greed/Need on an already-owned
		// unique item, or a contested relic weapon) — if confirmation never lands, the item
		// is reopened so the normal fallback chain advances past that option.
		private readonly Dictionary<(uint, uint), (RollOption Submitted, int ChecksLeft, RollState LastRollState, RollOption LastRolledState)> _verifyQueue = new Dictionary<(uint, uint), (RollOption, int, RollState, RollOption)>();
		// ~5s of ticks at observed tick rate — comfortably above the sub-1s confirmation time
		// seen for normal rolls, to avoid mistaking a slow-but-legitimate roll for a silent failure.
		private const int VerifyChecks = 150;

		private static (uint, uint) Key(LootItem item) => (item.ObjectId, item.ItemId);

		private const int LootSlots = 16;
		private const int AttemptsPerOption = 3;

		private void ResetState()
		{
			_attemptedItems.Clear();
			_failCount.Clear();
			_verifyQueue.Clear();
		}

		// Confirms whether a previously "successful" roll actually stuck. If it never does,
		// reopens the item so the fallback chain in ExecuteLogic advances to the next option.
		private void RunVerification()
		{
			if (_verifyQueue.Count == 0)
				return;

			var rawItems = LootManager.RawLootItems;

			foreach (var vKey in new List<(uint, uint)>(_verifyQueue.Keys))
			{
				var (submitted, checksLeft, lastRollState, lastRolledState) = _verifyQueue[vKey];
				var match = Array.Find(rawItems, it => it.Valid && (it.ObjectId, it.ItemId) == vKey);

				if (match.ObjectId == 0 && match.ItemId == 0)
				{
					LogHelper.Instance.Log($"[Loot] Verify {vKey}: item no longer in loot list after submitting {submitted} (assume finalized), checksLeft was {checksLeft}.");
					_verifyQueue.Remove(vKey);
					continue;
				}

				bool changed = match.RollState != lastRollState || match.RolledState != lastRolledState;
				bool lastCheck = checksLeft <= 1;

				if (changed || lastCheck)
					LogHelper.Instance.Log($"[Loot] Verify {vKey}: submitted={submitted}, RollState={match.RollState}, RolledState={match.RolledState}, Rolled={match.Rolled}, LeftRollTime={match.LeftRollTime:F2}, checksLeft={checksLeft}");

				if (match.Rolled)
				{
					// Genuinely confirmed — safe to forget the fallback progress now.
					_failCount.Remove(vKey);
					_verifyQueue.Remove(vKey);
					continue;
				}

				if (lastCheck)
				{
					// RollDirect reported success but the roll never actually registered
					// server-side. Reopen the item and force the fallback chain past this option.
					_attemptedItems.Remove(vKey);
					int existing = _failCount.TryGetValue(vKey, out int f) ? f : 0;
					_failCount[vKey] = existing + AttemptsPerOption;
					LogHelper.Instance.Log($"[Loot] Verify {vKey}: {submitted} never confirmed after {VerifyChecks} checks — treating as silent failure, advancing fallback.");
					_verifyQueue.Remove(vKey);
				}
				else
				{
					_verifyQueue[vKey] = (submitted, checksLeft - 1, match.RollState, match.RolledState);
				}
			}
		}

		/// <summary>
		/// Main task executor for the Loot logic.
		/// </summary>
		/// <returns>Returns <c>true</c> if any action was executed, otherwise <c>false</c>.</returns>
		internal new Task<bool> ExecuteLogic()
		{
			if (BotBase.Instance.IsPaused)
				return Task.FromResult(false);

			RunVerification();

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

				if (!_failCount.ContainsKey(key))
				{
					bool alreadyOwned = itemData != null && itemData.Unique && ff14bot.NeoProfiles.ConditionParser.HasItem(item.ItemId);
					LogHelper.Instance.Log($"[Loot] Slot {i} {itemName} (Item {item.ItemId}): RollState={item.RollState}, Unique={itemData?.Unique}, AlreadyOwned={alreadyOwned}, LeftRollTime={item.LeftRollTime:F2}");
				}

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
				int maxFails = options.Length * AttemptsPerOption;

				if (fails >= maxFails)
				{
					// All options exhausted (each tried AttemptsPerOption times) — give up.
					_attemptedItems.Add(key);
					_failCount.Remove(key);
					LogHelper.Instance.Log($"[Loot] All roll options exhausted for {itemName} (slot {i}), skipping.");
					return Task.FromResult(true);
				}

				var action = options[Math.Min(fails / AttemptsPerOption, options.Length - 1)];
				bool result = LootManager.RollDirect(action, i);

				if (result)
				{
					_attemptedItems.Add(key);
					// Don't clear _failCount here — RollDirect returning true doesn't mean the
					// roll actually landed (see RunVerification). Fallback progress must survive
					// until confirmed, otherwise a silently-rejected option gets retried forever
					// instead of escalating to the next one.
					_verifyQueue[key] = (action, VerifyChecks, item.RollState, item.RolledState);
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
