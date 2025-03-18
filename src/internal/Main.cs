using System;

using System.Collections.Generic;
using System.Reflection;

using SDG.Framework.Modules;
using SDG.Provider;
using SDG.Unturned;

using HarmonyLib;
using Steamworks;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    public class Main : IModuleNexus
    {
        public static Main Instance
        {
            get;
            private set;
        }

        public static MenuSurvivorsClothingUI uiInstance { get; set; }


        private static EconInfoLoader   _econInfoLoader = new EconInfoLoader();
        public  static EconInfoLoader   econInfoLoader  => _econInfoLoader;


        private ulong                   _instanceId;
        public ulong                    instanceId => _instanceId;


        public GenerateUI               generateUI;
        private static Harmony          _harmony;

        public static bool isParticleCrafting { private get; set; }

        public void initialize()
        {
            Instance = this;

            // Counter intuative, it's just to prevent extra item loading
            Level.onLevelLoaded += (int lvl) =>
            {
                ItemPersistenceManager.canPersistItems = false;
            };

            _harmony = new Harmony("com.swaggerballs.UnturnedSkinGenerationModule");

            try
            {
                typeof(TempSteamworksEconomy).GetProperty("hasCountryDetails",
                    BindingFlags.Instance | BindingFlags.Public
                ).SetValue(Provider.provider.economyService, true);

                typeof(TempSteamworksEconomy).GetProperty("doesCountryAllowRandomItems",
                    BindingFlags.Instance | BindingFlags.Public
                ).SetValue(Provider.provider.economyService, true);

                Log("Successfully overrode country economy restriction properties.");
            }
            catch (Exception e)
            {
                MissingReference("Failed to override country economy restriction properties.", e);
            }

            _econInfoLoader.LoadEconInfo();

            try
            {
                _harmony.Patch(
                    AccessTools.Constructor(typeof(MenuSurvivorsClothingUI)),
                    postfix: new HarmonyMethod(typeof(MenuSurvivorsClothingUIPatch), "Postfix_Constructor")
                );

                _harmony.Patch(
                    AccessTools.Method(typeof(TempSteamworksEconomy), "refreshInventory"),
                    postfix: new HarmonyMethod(typeof(TempSteamworksEconomyPatch), "Postfix_refreshInventory")
                );

                _harmony.Patch(AccessTools.Method(typeof(MenuUI), "customStart"),
                    postfix: new HarmonyMethod(typeof(MenuUIPatch), "Postfix_customStart")
                );

                _harmony.Patch(
                    AccessTools.Method(typeof(TempSteamworksEconomy), "getInventoryMythicID", new[] { typeof(int) }),
                    prefix: new HarmonyMethod(typeof(TempSteamworksEconomyPatch), "Prefix_getInventoryMythicID")
                );

                _harmony.Patch(
                    AccessTools.Method(typeof(MenuSurvivorsClothingItemUI), "onClickedUseButton", new[] { typeof(ISleekElement) }),
                    prefix: new HarmonyMethod(typeof(MenuSurvivorsClothingItemUIPatch), "Prefix_onClickedUseButton")
                );

                Log("Successfully overrode game classes.");
            }
            catch (Exception e)
            {
                MissingReference("Failed to override game classes.", e);
                return;
            }

            /*
                Will be ulong.MaxValue - 1 at first call
                because ulong.MaxValue is marked as invalid.
                Also starting at max to avoid existing item conflicts.
            */
            _instanceId = ulong.MaxValue;

            Log("Successfully initialized module.");
        }

        public void shutdown()
        {
            Instance = null;

            Log("Module successfully destructed");
        }

        private void _GenerateUI(SteamItemDetails_t grantedItem, bool isParticle)
        {
            GenerateUI.close();

            if (isParticle)
            {
                MenuSurvivorsClothingUI.isCrafting = false;
                MenuUI.closeAlert();
            }

            MenuUI.alertNewItems(MenuSurvivorsClothingUI.localization.format("Origin_Craft"),
                                 new List<SteamItemDetails_t>() { grantedItem });

            MenuSurvivorsClothingItemUI.viewItem(
                grantedItem.m_iDefinition.m_SteamItemDef,
                grantedItem.m_unQuantity,
                grantedItem.m_itemId.m_SteamItemInstanceID);

            MenuSurvivorsClothingItemUI.open();
            MenuSurvivorsClothingUI.close();

            GenerateUI.open();
        }

        private SteamItemDetails_t _GenerateRandomItem(bool isParticle)
        {
            --_instanceId;

            SteamItemDetails_t item = new SteamItemDetails_t
            {
                m_itemId = new SteamItemInstanceID_t
                { m_SteamItemInstanceID = _instanceId },

                m_iDefinition = new SteamItemDef_t
                {
                    m_SteamItemDef = isParticle ? _econInfoLoader.GetRandomSkinItemDefId()
                                                : _econInfoLoader.GetRandomMythicalItemDefId()
                },

                m_unQuantity = 1,
                m_unFlags = 0
            };

            string tags = isParticle ? $"particle_effect:{_econInfoLoader.GetRandomEffect()};" : string.Empty;

            ItemPersistenceManager.addLocalItem(item, tags);
            ItemPersistenceManager.RegisterGeneratedItem(item, isParticle);

            return item;
        }

        private SteamItemDetails_t _GenerateSpecificItem(int itemdefid, string effect, bool shouldEffect)
        {
            string tags;
            bool isParticle;

            UnturnedEconInfo itemData = shouldEffect ? _econInfoLoader.getMythicalVariantIfExist(itemdefid, effect)
                                                     : itemData = _econInfoLoader.GetItemBaseData(itemdefid);

            if (itemData == null)
            {
                Error("Item data not found");
                return default;
            }

            if (shouldEffect && itemData.quality != UnturnedEconInfo.EQuality.Mythical)
            {
                int effectId = _econInfoLoader.GetEffectId(effect);
                tags = $"particle_effect:{effectId};";
                isParticle = true;
            }
            else
            {
                tags = string.Empty;
                isParticle = false;
            }

            --_instanceId;

            SteamItemDetails_t item = new SteamItemDetails_t
            {
                m_itemId = new SteamItemInstanceID_t
                { m_SteamItemInstanceID = _instanceId },

                m_iDefinition = new SteamItemDef_t
                { m_SteamItemDef = itemData.itemdefid },

                m_unQuantity = 1,
                m_unFlags = 0
            };

            ItemPersistenceManager.addLocalItem(item, tags);
            ItemPersistenceManager.RegisterGeneratedItem(item, isParticle);

            return item;
        }

        public void GenerateSpecificItem(int itemdefid, string effect)
        {
            Log($"Generating specific item: {itemdefid}");

            MenuSurvivorsClothingUI.prepareForCraftResult();

            if (effect != "No Effect")
                Log($"Item Effect: {effect}");

            SteamItemDetails_t item = _GenerateSpecificItem(
                itemdefid, effect, effect != "No Effect"
            );

            _GenerateUI(item, true);
        }

        public void GenerateRandomItem(bool isParticle)
        {
            Log(isParticle ? "Generating particle craft" : "Generating mythical item");

            if (isParticle)
                MenuSurvivorsClothingUI.prepareForCraftResult();
            _GenerateUI(_GenerateRandomItem(isParticle), isParticle);
        }
    }
}
