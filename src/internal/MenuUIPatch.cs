using System;
using HarmonyLib;
using SDG.Unturned;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    [HarmonyPatch(typeof(MenuUI))]
    public class MenuUIPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("customStart", new Type[] { })]
        public static void Postfix_customStart(MenuSurvivorsClothingUI __instance)
        {
            /* Avoid repetitive calls during initial loading phase */

            Log("Enabling item persistence...");
            ItemPersistenceManager.canPersistItems = true;
            ItemPersistenceManager.RestoreGeneratedItems();
        }
    }
}
