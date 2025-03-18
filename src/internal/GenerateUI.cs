using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SDG.Provider;
using SDG.Unturned;
using UnityEngine;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    public class GenerateUI
    {
        public  static bool                     active;
        private static List<UnturnedEconInfo>   items;
        private static SleekFullscreenBox       container;
        private static SleekButtonIcon          backButton;
        private static SleekButtonIcon          mythicalButton;
        private static SleekButtonIcon          particleButton;
        private static ISleekConstraintFrame    selectionFrame;
        private static SleekInventory[]         packageButtons;
        private static ISleekSlider             characterSlider;
        private static ISleekConstraintFrame    effectSelection;
        private static ISleekBox                availableBox;
        private static ISleekScrollView         effectScrollBox;
        private static List<ISleekButton>       effectButtons;
        private static ISleekField              searchField;
        private static ISleekButton             searchButton;
        private static ISleekBox                pageBox;
        private static SleekButtonIcon          leftButton;
        private static SleekButtonIcon          rightButton;
        private static SleekButtonIcon          githubButton;
        public  static Local                    localization;
        private static BKTree                   searchTree;
        private static int                      pageIndex;
        private static List<string>             currentEffectPool;

        private static int                      selectedItem;

        private static readonly int             maxSearchDistance = 1;
        private static readonly int             itemsInViewCount = 25;
        private static string                   searchString => searchField.Text;
        private static int                      numberOfPages => MathfEx.GetPageCount(
                                                    items.Count, itemsInViewCount);

        private static void updateItems()
        {
            Log("Updating items in GenerateUI...");

            if (string.IsNullOrEmpty(searchString))
            {
                Log("Using default items list.");
                items = Main.econInfoLoader.baseSkins.Values.ToList();
                return;
            }

            items = searchTree.Search(
                searchString.ToLowerInvariant(),
                maxDistance: maxSearchDistance,
                itemsPerPage: itemsInViewCount
            ).ToList();
        }

        public static void updatePage()
        {
            pageBox.Text = localization.format("Page", pageIndex + 1, numberOfPages);

            if (packageButtons == null)
            {
                Warn("Cannot update content page, generation UI elements are null.");
                return;
            }

            Log("Updating page content in GenerateUI...");

            int startIdx = itemsInViewCount * pageIndex;

            for (int i = 0; i < itemsInViewCount; ++i)
            {
                if (startIdx + i < items.Count)
                    packageButtons[i].updateInventory(
                        0uL, items[startIdx + i].itemdefid, 1,
                        isClickable: true, isLarge: false);
                else
                    packageButtons[i].updateInventory(
                        0uL, 0, 0, isClickable: false, isLarge: false);
            }
        }

        public static void viewPage(int newPage)
        {
            pageIndex = newPage;
            updatePage();
        }

        private static void updateContent()
        {
            updateItems();

            if (pageIndex >= numberOfPages)
                pageIndex = numberOfPages - 1;

            updatePage();
        }

        public static void closeEffectSelection()
        {
            effectSelection.IsVisible = false;
            selectionFrame.IsVisible = true;
            removeEffectButtons();
        }

        public static void open()
        {
            if (!active)
            {
                active = true;

                backButton.IsVisible      = true;
                githubButton.IsVisible    = true;
                selectionFrame.IsVisible  = true;
                mythicalButton.IsVisible  = true;
                particleButton.IsVisible  = true;
                effectSelection.IsVisible = false;

                pageBox.IsVisible           = true;
                leftButton.IsVisible        = true;
                rightButton.IsVisible       = true;
                searchField.IsVisible       = true;
                searchButton.IsVisible      = true;
                characterSlider.IsVisible   = true;

                foreach (SleekInventory button in packageButtons)
                    button.IsVisible = true;

                MenuSurvivorsClothingUIPatch.crafting.IsVisible = false;
                MenuSurvivorsClothingUIPatch.inventory.IsVisible = false;
                MenuSurvivorsClothingUIPatch.craftingButton.IsVisible = false;
                MenuSurvivorsClothingUIPatch.itemStoreButton.IsVisible = false;
                MenuSurvivorsClothingUIPatch.originalBackButton.IsVisible = false;
                MenuSurvivorsClothingUIPatch.generationMenuButton.IsVisible = false;
                Log("GenerateUI is in view");
            }
        }

        public static void close()
        {
            if (active)
            {
                active = false;

                if (MenuSurvivorsClothingUIPatch.wasLastOpenedInventory)
                {
                    MenuSurvivorsClothingUIPatch.inventory.IsVisible = true;
                    MenuSurvivorsClothingUI.viewPage(MenuSurvivorsClothingUIPatch.numberOfPages);
                }
                else
                    MenuSurvivorsClothingUIPatch.crafting.IsVisible = true;

                MenuSurvivorsClothingUIPatch.craftingButton.IsVisible = true;
                MenuSurvivorsClothingUIPatch.itemStoreButton.IsVisible = true;
                MenuSurvivorsClothingUIPatch.originalBackButton.IsVisible = true;
                MenuSurvivorsClothingUIPatch.generationMenuButton.IsVisible = true;

                backButton.IsVisible      = false;
                githubButton.IsVisible    = false;
                mythicalButton.IsVisible  = false;
                particleButton.IsVisible  = false;
                selectionFrame.IsVisible  = false;
                effectSelection.IsVisible = false;

                pageBox.IsVisible           = false;
                leftButton.IsVisible        = false;
                rightButton.IsVisible       = false;
                searchField.IsVisible       = false;
                searchButton.IsVisible      = false;
                characterSlider.IsVisible   = false;

                closeEffectSelection();

                foreach (SleekInventory button in packageButtons)
                    button.IsVisible = false;

                Log("GenerateUI is out of view");
            }
        }

        private static void onClickedLeftButton(ISleekElement button)
        {
            if (pageIndex > 0)
                viewPage(pageIndex - 1);
            else if (numberOfPages > 0)
                viewPage(numberOfPages - 1);
        }

        private static void onClickedRightButton(ISleekElement button)
        {
            if (pageIndex < numberOfPages - 1)
                viewPage(pageIndex + 1);
            else if (numberOfPages > 0)
                viewPage(0);
        }

        private static void onClickedBackButton(ISleekElement button)
        {
            MenuSurvivorsClothingUI.open();
            close();
        }

        private void onClickedGithubButton(ISleekElement button)
        {
            if (Provider.provider.browserService.canOpenBrowser)
                Provider.provider.browserService.open("http://github.com/DontCallMeLuca/Unturned-Skins-Mod/");
            else
                Warn("Failed to open github link due to missing browser permissions");
        }

        private static void onClickedItem(SleekInventory button)
        {
            selectedItem = button.item;
            selectionFrame.IsVisible = false;
            effectSelection.IsVisible = true;

            /* Used to differenciate skin types, but this is better for freedom */
            Dictionary<ushort, string> effects = EconInfoLoader.AllEffects;

            currentEffectPool = effects.Values.ToList();

            if (currentEffectPool == null || currentEffectPool.Count == 0)
            {
                Error($"No effects found for item {button.item}.");
                return;
            }

            addEffectButton("No Effect", 0);

            for (int i = 1; i < currentEffectPool.Count + 1; ++i)
                addEffectButton(currentEffectPool[i - 1], i);

            effectScrollBox.ContentSizeOffset = new Vector2(0f, effectButtons.Count * 30);
        }

        private static void onEnteredSearchField(ISleekField field)
        {
            updateContent();
        }

        private static void onClickedSearchButton(ISleekElement button)
        {
            updateContent();
        }

        private static void onMythicalButtonClicked(ISleekElement button)
        {
            Main.Instance.GenerateRandomItem(false);
        }

        private static void onParticleButtonClicked(ISleekElement button)
        {
            Main.Instance.GenerateRandomItem(true);
        }

        private static void onDraggedCharacterSlider(ISleekSlider slider, float state)
        {
            Characters.characterYaw = state * 360f;
        }

        private static void onClickedEffectButton(ISleekElement button)
        {
            if (currentEffectPool == null || currentEffectPool.Count == 0)
            {
                Error("Effect pool is empty.");
                return;
            }

            int effectIndex = effectScrollBox.FindIndexOfChild(button);

            if (effectIndex < 0 || effectIndex >= currentEffectPool.Count)
            {
                Error($"Invalid effect index: {effectIndex}/{currentEffectPool.Count}");
                return;
            }

            close();

            Main.Instance.GenerateSpecificItem(selectedItem,
                effectIndex != 0 ? currentEffectPool[effectIndex - 1] : "No Effect");
        }

        private static void addEffectButton(string effect, int idx)
        {
            ISleekButton sleekButton = Glazier.Get().CreateButton();
            sleekButton.PositionOffset_Y = idx * 30;
            sleekButton.SizeScale_X = 1f;
            sleekButton.SizeOffset_Y = 30f;
            sleekButton.AllowRichText = true;
            sleekButton.TextColor = new SleekColor(EconInfoLoader.getEffectColor(effect));
            sleekButton.TextContrastContext = ETextContrastContext.InconspicuousBackdrop;
            sleekButton.Text = effect;
            sleekButton.OnClicked += onClickedEffectButton;
            effectScrollBox.AddChild(sleekButton);
            effectButtons.Add(sleekButton);
        }

        private static void removeEffectButtons()
        {
            effectScrollBox.RemoveAllChildren();
            effectButtons.Clear();
        }

        public GenerateUI(MenuSurvivorsClothingUI __instance)
        {
            Log("Building GenerateUI...");

            if (__instance == null)
            {
                Error("Instance object is null");
                return;
            }

            if (Main.econInfoLoader.baseSkins == null)
            {
                Error("Skin data is null");
                return;
            }

            active = false;

            pageIndex = 0;

            selectedItem = -1;

            container = MenuSurvivorsClothingUIPatch.container;

            mythicalButton = new SleekButtonIcon(
                Resources.Load<Texture2D>("Economy/Mystery/Icon_Large"), 300);

            mythicalButton.PositionOffset_Y = 10;
            mythicalButton.PositionOffset_X = 0;
            mythicalButton.SizeScale_X = 0.1f;
            mythicalButton.SizeOffset_Y = 370f;
            mythicalButton.enableRichText = true;
            mythicalButton.textColor = Palette.MYTHICAL;
            mythicalButton.shadowStyle = ETextContrastContext.Default;
            mythicalButton.fontSize = ESleekFontSize.Large;
            mythicalButton.text = "Generate Random Mythical";
            mythicalButton.tooltip = "???";
            mythicalButton.IsVisible = false;

            ISleekLabel mythicalLabel = (ISleekLabel)typeof(SleekButtonIcon).GetField(
                "label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mythicalButton);

            ISleekImage mythicalImage = (ISleekImage)typeof(SleekButtonIcon).GetField(
                "iconImage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mythicalButton);

            mythicalLabel.PositionOffset_Y = 140f;
            mythicalLabel.PositionOffset_X = -150f;

            mythicalLabel.SizeOffset_X = mythicalImage.SizeOffset_X;

            mythicalButton.onClickedButton += onMythicalButtonClicked;
            mythicalButton.onRightClickedButton += onMythicalButtonClicked;

            container.AddChild(mythicalButton);

            particleButton = new SleekButtonIcon(
                Provider.provider.economyService.LoadItemIcon(19000), 300);

            particleButton.PositionOffset_Y = 10;
            particleButton.PositionOffset_X = 350;
            particleButton.SizeScale_X = 0.1f;
            particleButton.SizeOffset_Y = 370f;
            particleButton.enableRichText = true;
            particleButton.textColor = Palette.COLOR_Y;
            particleButton.fontSize = ESleekFontSize.Large;
            particleButton.text = "Craft Random Mythical";
            particleButton.shadowStyle = ETextContrastContext.Default;
            particleButton.tooltip = "???";
            particleButton.IsVisible = false;

            ISleekLabel particleLabel = (ISleekLabel)typeof(SleekButtonIcon).GetField(
                "label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(particleButton);

            ISleekImage particleImage = (ISleekImage)typeof(SleekButtonIcon).GetField(
                "iconImage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(particleButton);

            particleLabel.PositionOffset_Y = 140f;
            particleLabel.PositionOffset_X = -150f;

            particleLabel.SizeOffset_X = particleImage.SizeOffset_X;

            particleButton.onClickedButton += onParticleButtonClicked;
            particleButton.onRightClickedButton += onParticleButtonClicked;

            container.AddChild(particleButton);

            localization = MenuSurvivorsClothingUI.localization;

            backButton = new SleekButtonIcon(MenuDashboardUI.icons.load<Texture2D>("Exit"));
            backButton.PositionOffset_Y = -50f;
            backButton.PositionScale_Y = 1f;
            backButton.SizeOffset_X = 200f;
            backButton.SizeOffset_Y = 50f;
            backButton.text = MenuDashboardUI.localization.format("BackButtonText");
            backButton.tooltip = MenuDashboardUI.localization.format("BackButtonTooltip");
            backButton.onClickedButton += onClickedBackButton;
            backButton.fontSize = ESleekFontSize.Medium;
            backButton.iconColor = ESleekTint.FOREGROUND;
            backButton.IsVisible = false;
            container.AddChild(backButton);

            Bundle bundle = Bundles.getBundle(
                "/Bundles/Textures/Menu/Icons/Configuration/MenuConfiguration/MenuConfiguration.unity3d"
            );

            githubButton = new SleekButtonIcon(bundle.load<Texture2D>("Options"));
            githubButton.PositionOffset_Y = -110f;
            githubButton.PositionScale_Y = 1f;
            githubButton.SizeOffset_X = 200f;
            githubButton.SizeOffset_Y = 50f;
            githubButton.text = "Github";
            githubButton.tooltip = "Open the project on github";
            githubButton.onClickedButton += onClickedGithubButton;
            githubButton.fontSize = ESleekFontSize.Medium;
            githubButton.iconColor = ESleekTint.FOREGROUND;
            githubButton.IsVisible = false;
            container.AddChild(githubButton);

            selectionFrame = Glazier.Get().CreateConstraintFrame();
            selectionFrame.PositionOffset_Y = 80f;
            selectionFrame.PositionScale_X = 0.5f;
            selectionFrame.SizeScale_X = 0.5f;
            selectionFrame.SizeScale_Y = 1f;
            selectionFrame.SizeOffset_Y = -120f;
            selectionFrame.Constraint = ESleekConstraint.FitInParent;
            selectionFrame.IsVisible = false;
            container.AddChild(selectionFrame);

            packageButtons = new SleekInventory[itemsInViewCount];
            for (int i = 0; i < packageButtons.Length; i++)
            {
                SleekInventory sleekInventory = new SleekInventory
                {
                    IsVisible = false,
                    PositionOffset_X = 5f,
                    PositionOffset_Y = 5f,
                    PositionScale_X = i % 5 * 0.2f,
                    PositionScale_Y = Mathf.FloorToInt(i / 5f) * 0.2f,
                    SizeOffset_X = -10f,
                    SizeOffset_Y = -10f,
                    SizeScale_X = 0.2f,
                    SizeScale_Y = 0.2f,
                    onClickedInventory = onClickedItem
                };
                selectionFrame.AddChild(sleekInventory);
                packageButtons[i] = sleekInventory;
            }

            searchField = Glazier.Get().CreateStringField();
            searchField.PositionOffset_X = 45f;
            searchField.PositionOffset_Y = -35f;
            searchField.SizeOffset_X = -160f;
            searchField.SizeOffset_Y = 30f;
            searchField.SizeScale_X = 1f;
            searchField.PlaceholderText = localization.format("Search_Field_Hint");
            searchField.OnTextSubmitted += onEnteredSearchField;
            selectionFrame.AddChild(searchField);
            searchButton = Glazier.Get().CreateButton();
            searchButton.PositionOffset_X = -105f;
            searchButton.PositionOffset_Y = -35f;
            searchButton.PositionScale_X = 1f;
            searchButton.SizeOffset_X = 100f;
            searchButton.SizeOffset_Y = 30f;
            searchButton.Text = localization.format("Search");
            searchButton.TooltipText = localization.format("Search_Tooltip");
            searchButton.OnClicked += onClickedSearchButton;
            selectionFrame.AddChild(searchButton);

            pageBox = Glazier.Get().CreateBox();
            pageBox.PositionOffset_X = -145f;
            pageBox.PositionOffset_Y = 5f;
            pageBox.PositionScale_X = 1f;
            pageBox.PositionScale_Y = 1f;
            pageBox.SizeOffset_X = 100f;
            pageBox.SizeOffset_Y = 30f;
            pageBox.FontSize = ESleekFontSize.Medium;
            selectionFrame.AddChild(pageBox);

            leftButton = new SleekButtonIcon(MenuSurvivorsClothingUI.icons.load<Texture2D>("Left"));
            leftButton.PositionOffset_X = -185f;
            leftButton.PositionOffset_Y = 5f;
            leftButton.PositionScale_X = 1f;
            leftButton.PositionScale_Y = 1f;
            leftButton.SizeOffset_X = 30f;
            leftButton.SizeOffset_Y = 30f;
            leftButton.tooltip = localization.format("Left_Tooltip");
            leftButton.iconColor = ESleekTint.FOREGROUND;
            leftButton.onClickedButton += onClickedLeftButton;
            selectionFrame.AddChild(leftButton);

            rightButton = new SleekButtonIcon(MenuSurvivorsClothingUI.icons.load<Texture2D>("Right"));
            rightButton.PositionOffset_X = -35f;
            rightButton.PositionOffset_Y = 5f;
            rightButton.PositionScale_X = 1f;
            rightButton.PositionScale_Y = 1f;
            rightButton.SizeOffset_X = 30f;
            rightButton.SizeOffset_Y = 30f;
            rightButton.tooltip = localization.format("Right_Tooltip");
            rightButton.iconColor = ESleekTint.FOREGROUND;
            rightButton.onClickedButton += onClickedRightButton;
            selectionFrame.AddChild(rightButton);

            effectSelection = Glazier.Get().CreateConstraintFrame();
            effectSelection.IsVisible = false;
            effectSelection.PositionOffset_Y = 40f;
            effectSelection.PositionScale_X = 0.5f;
            effectSelection.SizeScale_X = 0.5f;
            effectSelection.SizeScale_Y = 1f;
            effectSelection.SizeOffset_Y = -80f;
            effectSelection.Constraint = ESleekConstraint.FitInParent;
            container.AddChild(effectSelection);

            characterSlider = Glazier.Get().CreateSlider();
            characterSlider.PositionOffset_X = 45f;
            characterSlider.PositionOffset_Y = 10f;
            characterSlider.PositionScale_Y = 1f;
            characterSlider.SizeOffset_X = -240f;
            characterSlider.SizeOffset_Y = 20f;
            characterSlider.SizeScale_X = 1f;
            characterSlider.Orientation = ESleekOrientation.HORIZONTAL;
            characterSlider.OnValueChanged += onDraggedCharacterSlider;
            selectionFrame.AddChild(characterSlider);

            availableBox = Glazier.Get().CreateBox();
            availableBox.SizeScale_X = 1f;
            availableBox.SizeOffset_Y = 30f;
            availableBox.AllowRichText = true;
            availableBox.TextColor = ESleekTint.RICH_TEXT_DEFAULT;
            availableBox.TextContrastContext = ETextContrastContext.InconspicuousBackdrop;
            availableBox.Text = "Available Mythical Effects";
            effectSelection.AddChild(availableBox);

            effectScrollBox = Glazier.Get().CreateScrollView();
            effectScrollBox.PositionOffset_Y = 40f;
            effectScrollBox.SizeScale_X = 1f;
            effectScrollBox.SizeScale_Y = 1f;
            effectScrollBox.SizeOffset_Y = -40f;
            effectSelection.AddChild(effectScrollBox);

            effectButtons = new List<ISleekButton>();

            effectScrollBox.ScaleContentToWidth = true;
            effectScrollBox.ContentSizeOffset = new Vector2(0f, effectButtons.Count * 30);

            updateContent();

            searchTree = new BKTree(items);
        }
    }
}
