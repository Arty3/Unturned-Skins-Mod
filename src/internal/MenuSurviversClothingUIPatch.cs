using System;

using System.Collections.Generic;
using System.Reflection;

using Steamworks;
using SDG.Unturned;
using UnityEngine;
using HarmonyLib;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    [HarmonyPatch(typeof(MenuSurvivorsClothingUI))]
    public static class MenuSurvivorsClothingUIPatch
    {
        public static SleekFullscreenBox        container;
        public static ISleekConstraintFrame     inventory;
        public static ISleekConstraintFrame     crafting;
        public static SleekButtonIcon           itemStoreButton;
        public static SleekButtonIcon           generationMenuButton;
        public static SleekButtonIcon           originalBackButton;
        public static SleekButtonIcon           craftingButton;
        public static bool                      wasLastOpenedInventory;
        public static List<SteamItemDetails_t>  filteredItems;
        public static SleekInventory[]          packageButtons;

		public static int numberOfPages
		{
			get
			{
				var filteredItems = (List<SteamItemDetails_t>)typeof(MenuSurvivorsClothingUI).GetField(
					"filteredItems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

				return MathfEx.GetPageCount(filteredItems.Count, 25);
			}
		}

        [HarmonyPostfix]
        [HarmonyPatch("open")]
        public static void Postfix_open()
        {
            ItemPersistenceManager.RestoreGeneratedItems();
		}

		[HarmonyPrefix]
		[HarmonyPatch("onClickedInventory")]
		public static bool Prefix_onClickedInventory(SleekInventory button)
        {
            int pageIndex = (int)typeof(MenuSurvivorsClothingUI).GetField(
                "pageIndex", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

			packageButtons = (SleekInventory[])typeof(MenuSurvivorsClothingUI).GetField(
	            "packageButtons", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

			filteredItems = (List<SteamItemDetails_t>)typeof(MenuSurvivorsClothingUI).GetField(
	            "filteredItems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

			int num     = packageButtons.Length * pageIndex;
			int num2    = inventory.FindIndexOfChild(button);

            if (num + num2 >= filteredItems.Count)
                return false;

            int item        = button.item;
            ulong instance  = button.instance;
            ushort quantity = button.quantity;

			if (instance == 0)
			{
				Error("No instance ID found");
				return false;
			}

			EEconFilterMode filterMode = (EEconFilterMode)typeof(MenuSurvivorsClothingUI).GetField(
                "filterMode", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            if (filterMode == EEconFilterMode.STAT_TRACKER ||
                filterMode == EEconFilterMode.STAT_TRACKER_REMOVAL ||
                filterMode == EEconFilterMode.RAGDOLL_EFFECT_REMOVAL ||
                filterMode == EEconFilterMode.RAGDOLL_EFFECT)
                return false;

            else if (Provider.preferenceData.Allow_Ctrl_Shift_Alt_Salvage &&
                     InputEx.GetKey(KeyCode.LeftControl) &&
                     InputEx.GetKey(KeyCode.LeftShift) &&
                     InputEx.GetKey(KeyCode.LeftAlt))
                return false;

            else if (InputEx.GetKey(ControlsSettings.other) &&
                     packageButtons[num2].itemAsset != null &&
					 button.itemAsset.type != EItemType.BOX)
            {
                MenuSurvivorsClothingItemUIPatch.handleCosmeticEquip(instance, button.itemAsset.type);
                Characters.ToggleEquipItemByInstanceId(instance);
			}

            else
            {
                MenuSurvivorsClothingItemUI.viewItem(item, quantity, instance);
                MenuSurvivorsClothingItemUI.open();
                MenuSurvivorsClothingUI.close();
            }

            return false;
        }


		[HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, new Type[] { })]
        public static void Postfix_Constructor(MenuSurvivorsClothingUI __instance)
        {
            Log("Postfixing MenuSurvivorsClothingUI Constructor...");

            Main.uiInstance = __instance;

            Log("Generating new layout...");

            try
            {
                container = (SleekFullscreenBox)typeof(MenuSurvivorsClothingUI).GetField(
                    "container", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                inventory = (ISleekConstraintFrame)typeof(MenuSurvivorsClothingUI).GetField(
                    "inventory", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                crafting = (ISleekConstraintFrame)typeof(MenuSurvivorsClothingUI).GetField(
                    "crafting", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                itemStoreButton = (SleekButtonIcon)typeof(MenuSurvivorsClothingUI).GetField(
                    "itemstoreButton", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                originalBackButton = (SleekButtonIcon)typeof(MenuSurvivorsClothingUI).GetField(
                    "backButton", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                craftingButton = (SleekButtonIcon)typeof(MenuSurvivorsClothingUI).GetField(
                    "craftingButton", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                filteredItems = (List<SteamItemDetails_t>)typeof(MenuSurvivorsClothingUI).GetField(
                    "filteredItems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);

                packageButtons = (SleekInventory[])typeof(MenuSurvivorsClothingUI).GetField(
                    "packageButtons", BindingFlags.Static | BindingFlags.NonPublic).GetValue(__instance);
            }
            catch (Exception e)
            {
                MissingReference("Failed to get UI components.", e);
                return;
            }

            Main.Instance.generateUI = new GenerateUI(__instance);

            Bundle bundle = Bundles.getBundle(
                "/Bundles/Textures/Menu/Icons/Survivors/MenuSurvivors/MenuSurvivors.unity3d"
            );

            generationMenuButton = new SleekButtonIcon(bundle.load<Texture2D>("clothing"));

            generationMenuButton.PositionOffset_Y = -230f;
            generationMenuButton.PositionScale_Y = 1f;
            generationMenuButton.SizeOffset_X = 200f;
            generationMenuButton.SizeOffset_Y = 50f;
            generationMenuButton.text = "Skin Menu";
            generationMenuButton.tooltip = "Click here to view generate skins";
            generationMenuButton.fontSize = ESleekFontSize.Medium;
            generationMenuButton.iconColor = ESleekTint.FOREGROUND;

            void onGenerationMenuButtonClicked(ISleekElement button)
            {
                Log("Loading skin generation menu...");
                wasLastOpenedInventory = inventory.IsVisible;
                GenerateUI.open();
            }

            generationMenuButton.onClickedButton += onGenerationMenuButtonClicked;
            generationMenuButton.onRightClickedButton += onGenerationMenuButtonClicked;

            container.AddChild(generationMenuButton);

			Log("Finished generating new layout");
        }
    }
}
