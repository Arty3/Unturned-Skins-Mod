using System;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using SDG.Provider;
using SDG.Unturned;
using Steamworks;
using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    public class ItemPersistenceManager
    {
        /*
            Prevents generated items from being
            lost when the inventory is refreshed.
        */

        public static bool canPersistItems = false;

        private static HashSet<ulong> persistentItemIds = new HashSet<ulong>();

        public class EquippedCosmetic
        {
            public int itemDefId;
            public ushort effectId;
            public EItemType cosmeticType;

            public EquippedCosmetic()
            {
                itemDefId = 0;
                effectId = 0;
                cosmeticType = EItemType.HAT;
            }

            public EquippedCosmetic(int itemdefid, ushort effectid, EItemType cosmetictype)
            {
                itemDefId = itemdefid;
                effectId = effectid;
                cosmeticType = cosmetictype;
            }
        }

        public static Dictionary<ulong, EquippedCosmetic>   equippedCosmetics       = new Dictionary<ulong, EquippedCosmetic>();
        public static Dictionary<ulong, SteamItemDetails_t> cachedItems             = new Dictionary<ulong, SteamItemDetails_t>();
        public static Dictionary<ulong, DynamicEconDetails> cachedDynamicDetails    = new Dictionary<ulong, DynamicEconDetails>();

        public static void RegisterGeneratedItem(SteamItemDetails_t item, bool hasParticleEffect = false)
        {
            if (!canPersistItems) return;

            ulong instanceId = item.m_itemId.m_SteamItemInstanceID;

            persistentItemIds.Add(instanceId);
            cachedItems.Add(instanceId, item);

            if (hasParticleEffect &&
                Provider.provider.economyService
                .dynamicInventoryDetails.TryGetValue(
                    instanceId, out var dynamicDetails))
                cachedDynamicDetails.Add(instanceId, dynamicDetails);

            Log($"Registered generated item {item.m_iDefinition.m_SteamItemDef}");
        }

        public static void RegisterEquippedItem(ulong instanceId, int itemdefid, ushort mythicID, EItemType cosmeticType)
        {
            if (instanceId != 0 && mythicID != 0 && !IsItemEquipped(instanceId, itemdefid))
            {
                UnregisterItemOfSameType(cosmeticType);
                equippedCosmetics[instanceId] = new EquippedCosmetic(itemdefid, mythicID, cosmeticType);
                Log($"Registered equipped item {instanceId} with mythic effect {mythicID}");
            }
        }

        public static void UnregisterItemOfSameType(EItemType type)
        {
            var itemToRemove = equippedCosmetics.FirstOrDefault(
                pair => pair.Value.cosmeticType == type);

            if (itemToRemove.Value != null)
                UnregisterEquippedItem(itemToRemove.Key);
        }

        public static void UnregisterEquippedItem(ulong instanceId)
        {
            if (equippedCosmetics.ContainsKey(instanceId))
            {
                equippedCosmetics.Remove(instanceId);
                Log($"Unregistered equipped item {instanceId}");
            }
        }

        public static bool IsItemEquipped(ulong instanceId, int itemdefid)
        {
            var cosmetic = equippedCosmetics.Values.FirstOrDefault(x => x.itemDefId == itemdefid);

            return equippedCosmetics.ContainsKey(instanceId) || cosmetic != null;
        }

        public static ushort GetEffectForEquippedItem(int itemdefid)
        {
            var cosmetic = equippedCosmetics.Values.FirstOrDefault(
                            x => x.itemDefId == itemdefid);

            if (cosmetic != null && cosmetic.effectId == 0)
                Warn($"Failed to find mythical effect for equipped item {itemdefid}");

            return cosmetic != null ? cosmetic.effectId : (ushort)0;
        }

        public static void RestoreGeneratedItems()
        {
            if (!canPersistItems) return;

            foreach (var itemPair in cachedItems)
            {
                if (persistentItemIds.Contains(itemPair.Key))
                {
                    if (cachedDynamicDetails.TryGetValue(itemPair.Key, out var details))
                        addLocalItem(itemPair.Value, details.tags);
                    else
                        addLocalItem(itemPair.Value, string.Empty);
                }
            }

            Log("Restored generated items.");
        }

        public static void UnregisterGeneratedItem(ulong instanceId)
        {
            if (!canPersistItems) return;

            persistentItemIds.Remove(instanceId);
            cachedItems.Remove(instanceId);
            cachedDynamicDetails.Remove(instanceId);

            Log($"Unregistered generated item {instanceId}");
        }

        public static void addLocalItem(SteamItemDetails_t item, string tags)
        {
            Log($"Adding local item {item.m_itemId.m_SteamItemInstanceID} to inventory...");

            Provider.provider.economyService.inventoryDetails.Add(item);

            if (Provider.provider.economyService.dynamicInventoryDetails.ContainsKey(item.m_itemId.m_SteamItemInstanceID))
                Provider.provider.economyService.dynamicInventoryDetails.Remove(item.m_itemId.m_SteamItemInstanceID);

            Provider.provider.economyService.dynamicInventoryDetails.Add(
                item.m_itemId.m_SteamItemInstanceID, new DynamicEconDetails
                    { tags = tags, dynamic_props = string.Empty });

            if (MenuSurvivorsClothingUI.active)
            {
                try
                {
                    typeof(MenuSurvivorsClothingUI).GetMethod(
                        "updateFilter", BindingFlags.NonPublic | BindingFlags.Static
                    ).Invoke(null, null);
                }
                catch (Exception e)
                {
                    MissingReference("Failed to update filter.", e);
                }

                MenuSurvivorsClothingUI.updatePage();
            }
        }
    }
}
