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
		public static bool handleCosmeticEquip(ulong instance, EItemType type)
		{
			if (EconInfoLoader.isCosmetic(type))
				return false;

			if (!ItemPersistenceManager.cachedItems.TryGetValue(instance, out var details))
				return false;

			int itemdefid = details.m_iDefinition.m_SteamItemDef;

			if (ItemPersistenceManager.IsItemEquipped(instance))
			{
				Log("Dequipping cosmetic.");
				ItemPersistenceManager.UnregisterEquippedItem(instance);
			}
			else
			{
				if (ItemPersistenceManager.cachedDynamicDetails.TryGetValue(
					instance, out DynamicEconDetails dynamicDetails))
				{
					ItemPersistenceManager.RegisterEquippedItem(
						instance, itemdefid,
						dynamicDetails.getParticleEffect(),
						type
					);

					Log($"Registered equipped cosmetic: {instance} with effect {dynamicDetails.getParticleEffect()}");
				}
			}

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch("onClickedUseButton")]
		public static bool Prefix_onClickedUseButton(ISleekElement button)
		{
			Log("Prefixing onClickedUseButton");

			try
			{
				Log("Enabling isEquipping");

				SleekInventory packageBox = (SleekInventory)typeof(MenuSurvivorsClothingItemUI).GetField(
					"packageBox", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

				ulong instance = (ulong)typeof(MenuSurvivorsClothingItemUI).GetField(
					"instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

				if (instance == 0)
				{
					Error("No instance ID found");
					return true;
				}

				handleCosmeticEquip(instance, packageBox.itemAsset.type);

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
