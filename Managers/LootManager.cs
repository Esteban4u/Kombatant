using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Objects;
using Kombatant.Enums;
using Kombatant.Helpers;
using Kombatant.Memory;
using Kombatant.Settings;

namespace Kombatant.Managers
{

	[StructLayout(LayoutKind.Explicit, Size = 0x40)]
	public struct LootItem
	{
		[FieldOffset(0x0)] public uint ObjectId;
		[FieldOffset(0x8)] public uint ItemId;
		[FieldOffset(0x20)] public RollState RollState;
		[FieldOffset(0x24)] public RollOption RolledState;
		[FieldOffset(0x2C)] public float LeftRollTime;
		[FieldOffset(0x20)] public float TotalRollTime;
		[FieldOffset(0x3C)] public uint Index;

		public bool Rolled => RolledState > 0;
		public bool Valid => ObjectId != GameObjectManager.EmptyGameObject && ObjectId != 0 && ItemId != 0;

		//public bool Needed => RolledState == (uint)RollOption.Need;
		//public bool Greeded => RolledState == (uint)RollOption.Greed;
		//public bool Passed => RolledState == (uint)RollOption.Pass;

		public Item Item => DataManager.GetItem(ItemId);

		public bool Need() => Roll(RollOption.Need);
		public bool Greed() => Roll(RollOption.Greed);
		public bool Pass() => Roll(RollOption.Pass);

		public bool Roll(RollOption option)
		{
			bool result;
			var thisLootItem = this;
			var findIndex = Array.FindIndex(LootManager.RawLootItems, item => item.Equals(thisLootItem));
			using (Core.Memory.TemporaryCacheState(false))
			{
				lock (Core.Memory.Executor.AssemblyLock)
				{
					result = Core.Memory.CallInjected64<bool>(Offsets.Instance.LootFunc, new object[]
					{
						Offsets.Instance.LootsAddr,
						(ulong)option,
						findIndex
					});
				}
			}

			if (result)
			{
				LogHelper.Instance.Log($"Rolled {option} for {Item.CurrentLocaleName}. LootState: {RollState} Remaining time: {LeftRollTime:F2}");
				if (BotBase.Instance.ShowLootNotification)
				{
					OverlayHelper.Instance.AddToast($"Rolled [{option}] for {Item.CurrentLocaleName}.", Colors.Gold, Colors.Black, TimeSpan.FromSeconds(2.5));
				}
			}
			else
			{
				LogHelper.Instance.Log($"Failed rolling {option} for {Item.CurrentLocaleName}. LootState: {RollState} Remaining time: {LeftRollTime:F2}");
			}

			return result;
		}
	}

	public class LootManager
	{
		public static List<LootItem> AvailableLoots => RawLootItems.Where(i => i.Valid).ToList();
		public static LootItem[] RawLootItems => Core.Memory.ReadArray<LootItem>(Offsets.Instance.LootsAddr + 0x10, 16);
		public static bool HasLoot => AvailableLoots.Any();
	}
}
