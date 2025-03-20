using System;

using HarmonyLib;
using SDG.Unturned;

namespace SkinsModule
{
	[HarmonyPatch(typeof(MenuSurvivorsClothingDeleteUI))]
	public class MenuSurvivorsClothingDeleteUIPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch("salvageItem", new Type[] { })]
		public static bool Prefix_salvageItem(int itemID, ulong instanceID)
		{
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch("onClickedYesButton")]
		public static bool Prefix_onClickedYesButton(ISleekElement button)
		{
			return false;
		}
	}
}
