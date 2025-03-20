using System;

using HarmonyLib;
using SDG.Provider;
using Steamworks;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    [HarmonyPatch(typeof(TempSteamworksEconomy))]
    public class TempSteamworksEconomyPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("refreshInventory")]
        public static void Postfix_refreshInventory(TempSteamworksEconomy __instance)
        {
            Log("Postfixing TempSteamworksEconomy refreshInventory...");
            ItemPersistenceManager.RestoreGeneratedItems();
        }

        [HarmonyPostfix]
        [HarmonyPatch("handleClientResultReady")]
        public static void Postfix_handleClientResultReady(SteamInventoryResultReady_t callback)
        {
			Log("Postfixing handleClientResultReady...");
			ItemPersistenceManager.RestoreGeneratedItems();
			ItemPersistenceManager.EquipPreviouslyEquippedCosmetics();
		}

        [HarmonyPrefix]
        [HarmonyPatch("getInventoryMythicID", new Type[] { typeof(int) })]
        public static bool Prefix_getInventoryMythicID(TempSteamworksEconomy __instance, int item, ref ushort __result)
        {
            UnturnedEconInfo econInfo = AccessTools.Method(typeof(TempSteamworksEconomy), "FindEconInfo")
                .Invoke(__instance, new object[] { item }) as UnturnedEconInfo;

            if (econInfo != null && econInfo.quality == UnturnedEconInfo.EQuality.Mythical)
            {
                Log("Using default getInventoryMythicID");
                __result = (ushort)econInfo.item_effect;
                return false;
            }

			ushort mythicId = ItemPersistenceManager.GetEffectForEquippedItem(item);

			if (mythicId != 0)
			{
				Log($"Found mythic effect {mythicId} for item {item}");
				__result = mythicId;
				return false;
			}

			__result = 0;

			return true;
        }
    }
}
