using System;
using System.Reflection;

using HarmonyLib;
using SDG.Provider;
using SDG.Unturned;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
	[HarmonyPatch(typeof(MenuSurvivorsClothingItemUI))]
	public class MenuSurvivorsClothingItemUIPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch("onClickedUseButton")]
		public static bool Prefix_onClickedUseButton(ISleekElement button)
		{
			Log("Prefixing onClickedUseButton");

			try
			{
				SleekInventory packageBox = (SleekInventory)typeof(MenuSurvivorsClothingItemUI).GetField(
					"packageBox", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

				if (packageBox.itemAsset.type != EItemType.HAT		&&
					packageBox.itemAsset.type != EItemType.GLASSES	&&
					packageBox.itemAsset.type != EItemType.MASK		&&
					packageBox.itemAsset.type != EItemType.SHIRT	&&
					packageBox.itemAsset.type != EItemType.VEST		&&
					packageBox.itemAsset.type != EItemType.PANTS	&&
					packageBox.itemAsset.type != EItemType.BACKPACK)
					return true;

				ulong instance = (ulong)typeof(MenuSurvivorsClothingItemUI).GetField(
					"instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

				if (instance == 0)
				{
					Error("No instance ID found");
					return true;
				}

				if (!ItemPersistenceManager.cachedItems.TryGetValue(instance, out var details))
				{
					Error("Invalid item, no cached instance found");
					return true;
				}

				int itemdefid = details.m_iDefinition.m_SteamItemDef;

				if (ItemPersistenceManager.IsItemEquipped(instance, itemdefid))
				{
					Log("Dequipping cosmetic.");
					ItemPersistenceManager.UnregisterEquippedItem(instance);
				}
				else
				{
					if (ItemPersistenceManager.cachedDynamicDetails.TryGetValue(instance, out DynamicEconDetails dynamicDetails))
					{
						ItemPersistenceManager.RegisterEquippedItem(
							instance, itemdefid,
							dynamicDetails.getParticleEffect(),
							packageBox.itemAsset.type);

						Log($"Registered equipped cosmetic: {instance} with effect {dynamicDetails.getParticleEffect()}");
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				Error($"Error in onClickedUseButton postfix: {ex}");
				return true;
			}
		}
	}
}
