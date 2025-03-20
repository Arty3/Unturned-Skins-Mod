using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;
using SDG.Provider;
using SDG.Unturned;

using Unturned.SystemEx;

using static SkinsModule.ModuleLogger;

/*
    Unholy sins ahead...
*/

namespace SkinsModule
{
    public class EconInfoLoader
    {
        public Dictionary<int, UnturnedEconInfo> econInfo           { get; private set; }

        public Dictionary<int, UnturnedEconInfo> baseSkins          { get; private set; }
        public Dictionary<int, UnturnedEconInfo> skinsEconInfo      { get; private set; }
		public Dictionary<int, UnturnedEconInfo> mythicalsEconInfo  { get; private set; }

        /*
            I could go and parse all the mythical .dat files
            but because there is no distinction between head mythicals
            and applicable crafting effects, I can only hardcode them,
            since the crafting logic is server-sided. :(
        */

        private class EffectColorPair
        {
            public string   effect;
            public Color    color;

            public EffectColorPair(string effect, Color color)
            {
                this.effect = effect;
                this.color  = color;
            }
        }

        /* Unturned has highly limited color support with this stuff */
        private static Dictionary<ushort, EffectColorPair> _allEffects = new Dictionary<ushort, EffectColorPair>()
        {
            { 43, new EffectColorPair("Electrostatic",      new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 44, new EffectColorPair("Wicked Aura",        new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 20, new EffectColorPair("Atomic",             new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 25, new EffectColorPair("Blood Sucker",       new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 30, new EffectColorPair("Blossoming",         new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 51, new EffectColorPair("Crimson Navigator",  new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 62, new EffectColorPair("Cascading Chips",    new Color { r = 255, g = 255, b = 255, a = 1f }) },
            { 52, new EffectColorPair("Dazzling",           new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 9,  new EffectColorPair("Bubbling",           new Color { r = 255, g = 255, b = 255, a = 1f }) },
            { 1,  new EffectColorPair("Burning",            new Color { r = 255, g = 130, b = 0,   a = 1f }) },
            { 22, new EffectColorPair("Confetti",           new Color { r = 50,  g = 190, b = 0,   a = 1f }) },
            { 55, new EffectColorPair("Golden Confetti",    new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 10, new EffectColorPair("Cosmic",             new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 8,  new EffectColorPair("Divine",             new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 63, new EffectColorPair("Dizzy Birds",        new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 64, new EffectColorPair("Dizzy Stars",        new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 65, new EffectColorPair("Golden Butterflies", new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 66, new EffectColorPair("Magic Butterflies",  new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 59, new EffectColorPair("Pretty Ipê",         new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 60, new EffectColorPair("Thorny Roses",       new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 61, new EffectColorPair("Toxic",              new Color { r = 0,   g = 255, b = 0,   a = 1f }) },
            { 29, new EffectColorPair("Ice Dragon",         new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 28, new EffectColorPair("Fire Dragon",        new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 31, new EffectColorPair("Bananza",            new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 32, new EffectColorPair("High Tide",          new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 11, new EffectColorPair("Electric",           new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 39, new EffectColorPair("Frosty",             new Color { r = 0,   g = 210, b = 230, a = 1f }) },
            { 38, new EffectColorPair("Sacrificial",        new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 40, new EffectColorPair("Spectral Gems",      new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 33, new EffectColorPair("Decked Out",         new Color { r = 255, g = 255, b = 255, a = 1f }) },
            { 34, new EffectColorPair("Crystal Shards",     new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 35, new EffectColorPair("Soul Shattered",     new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 36, new EffectColorPair("Enchanted",          new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 37, new EffectColorPair("Cryptic Runes",      new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 16, new EffectColorPair("Energized",          new Color { r = 0,   g = 255, b = 255, a = 1f }) },
            { 15, new EffectColorPair("Freezing",           new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 47, new EffectColorPair("Fire Crown",         new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 46, new EffectColorPair("Ice Crown",          new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 48, new EffectColorPair("Firefly",            new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 49, new EffectColorPair("Falling Icicles",    new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 50, new EffectColorPair("Snowflake",          new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 6,  new EffectColorPair("Glitched",           new Color { r = 0,   g = 255, b = 0,   a = 1f }) },
            { 2,  new EffectColorPair("Glowing",            new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 14, new EffectColorPair("Haunted",            new Color { r = 225, g = 225, b = 225, a = 1f }) },
            { 45, new EffectColorPair("Palm Nights",        new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 41, new EffectColorPair("Sunrise",            new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 42, new EffectColorPair("Sunset",             new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 27, new EffectColorPair("Sky Lantern",        new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 3,  new EffectColorPair("Lovely",             new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 26, new EffectColorPair("Lucky Coins",        new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 21, new EffectColorPair("Melting",            new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 18, new EffectColorPair("Meta",               new Color { r = 255, g = 0,   b = 200, a = 1f }) },
            { 4,  new EffectColorPair("Musical",            new Color { r = 255, g = 255, b = 255, a = 1f }) },
            { 17, new EffectColorPair("Holiday Spirit",     new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 13, new EffectColorPair("Party",              new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 54, new EffectColorPair("Purple Hole",        new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 53, new EffectColorPair("Spirit Signs",       new Color { r = 0,   g = 225, b = 255, a = 1f }) },
            { 19, new EffectColorPair("Pyrotechnic",        new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 23, new EffectColorPair("Radioactive",        new Color { r = 0,   g = 200, b = 0,   a = 1f }) },
            { 12, new EffectColorPair("Rainbow",            new Color { r = 255, g = 0,   b = 0,   a = 1f }) },
            { 5,  new EffectColorPair("Shiny",              new Color { r = 255, g = 255, b = 255, a = 1f }) },
            { 56, new EffectColorPair("Golden Shine",       new Color { r = 255, g = 200, b = 0,   a = 1f }) },
            { 24, new EffectColorPair("Steampunk",          new Color { r = 180, g = 100, b = 75,  a = 1f }) },
            { 57, new EffectColorPair("Sugar Rush",         new Color { r = 200, g = 0,   b = 255, a = 1f }) },
            { 7,  new EffectColorPair("Wealthy",            new Color { r = 0,   g = 255, b = 0,   a = 1f }) }
        };

        private static Dictionary<ushort, string> _particleEffects = new Dictionary<ushort, string>()
        {
            { 43, "Electrostatic"       },
            { 30, "Blossoming"          },
            { 9,  "Bubbling"            },
            { 1,  "Burning"             },
            { 62, "Cascading Chips"     },
            { 22, "Confetti"            },
            { 10, "Cosmic"              },
            { 11, "Electric"            },
            { 39, "Frosty"              },
            { 65, "Golden Butterflies"  },
            { 66, "Magic Butterflies"   },
            { 59, "Pretty Ipê"          },
            { 60, "Thorny Roses"        },
            { 61, "Toxic"               },
            { 40, "Spectral Gems"       },
            { 33, "Decked Out"          },
            { 34, "Crystal Shards"      },
            { 35, "Soul Shattered"      },
            { 36, "Enchanted"           },
            { 16, "Energized"           },
            { 15, "Freezing"            },
            { 48, "Firefly"             },
            { 6,  "Glitched"            },
            { 27, "Sky Lantern"         },
            { 3,  "Lovely"              },
            { 26, "Lucky Coins"         },
            { 21, "Melting"             },
            { 18, "Meta"                },
            { 4,  "Musical"             },
            { 17, "Holiday Spirit"      },
            { 23, "Radioactive"         },
            { 5,  "Shiny"               },
            { 24, "Steampunk"           },
            { 7,  "Wealthy"             }
        };

        public static Dictionary<ushort, string> AllEffects      =  _allEffects.ToDictionary(
                                                                        kvp => kvp.Key,
                                                                        kvp => kvp.Value.effect);
        public static Dictionary<ushort, string> ParticleEffects => _particleEffects;

        public static Color getEffectColor(string effect)
        {
            if (string.IsNullOrEmpty(effect) ||
                _allEffects == null          ||
                effect == "No Effect")
                return Color.white;

            return _allEffects.Values.FirstOrDefault(
                    pair => pair.effect.ToLowerInvariant()
                        == effect.ToLowerInvariant()).color;
        }

        public static bool isHeadItem(int itemid)
        {
            if (Main.econInfoLoader.econInfo.TryGetValue(itemid, out var info))
            {
                return info.display_type.Contains("Hat")  ||
                       info.display_type.Contains("Mask") ||
                       info.display_type.Contains("Glasses");
            }

            Error("Invalid item not in found in data");
            return false;
        }

        public static bool isSkinItem(int itemid)
        {
            if (Main.econInfoLoader.econInfo.TryGetValue(itemid, out var info))
                return info.item_skin != 0;

            Error("Invalid item not in found in data");
            return false;
        }

        public void LoadEconInfo()
        {
            Log("Loading EconInfo.bin data...");

            try
            {
                econInfo = (Dictionary<int, UnturnedEconInfo>)typeof(TempSteamworksEconomy)
                    .GetProperty("econInfo", BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(Provider.provider.economyService);
            }
            catch (Exception e)
            {
                MissingReference("Failed to gather economy info.", e);

                econInfo = new Dictionary<int, UnturnedEconInfo>();
                Log("Initialized empty economy info dictionary");
            }

            string path = PathEx.Join(UnturnedPaths.RootDirectory, "EconInfo.bin");

            baseSkins = new Dictionary<int, UnturnedEconInfo>();
            skinsEconInfo = new Dictionary<int, UnturnedEconInfo>();
			mythicalsEconInfo = new Dictionary<int, UnturnedEconInfo>();

            try
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    int version = reader.ReadInt32();

                    if (version > 3)
                    {
                        Error($"Unable to load future EconInfo.bin version ({version})");
                        return;
                    }

                    LoadItems(reader, version);

					Log("Successfully loaded EconInfo.bin");

                    Log($"Particle Craft Uniqueness: 1 in {skinsEconInfo.Count * _particleEffects.Count}.");
                }
            }
            catch (Exception e)
            {
                Error($"Failed to load EconInfo.bin:\n{e.Message}");
            }
        }

        private void LoadItems(BinaryReader reader, int version)
        {
            int itemCount = reader.ReadInt32();

            for (int i = 0; i < itemCount; ++i)
            {
                UnturnedEconInfo item = new UnturnedEconInfo
                {
                    name = reader.ReadString(),
                    display_type = reader.ReadString(),
                    description = reader.ReadString(),
                    name_color = reader.ReadString(),
                    itemdefid = reader.ReadInt32(),
                    marketable = reader.ReadBoolean(),
                    scraps = reader.ReadInt32(),
                    target_game_asset_guid = new Guid(reader.ReadBytes(16)),
                    item_skin = reader.ReadInt32(),
                    item_effect = reader.ReadInt32(),
                    quality = (UnturnedEconInfo.EQuality)reader.ReadInt32(),
                    econ_type = reader.ReadInt32()
                };

                if (version >= 2)
                    item.creationTimeUtc = DateTime.FromBinary(reader.ReadInt64());

                item.isEligibleForPromotion = (version >= 3) ? reader.ReadBoolean() : true;

                if (ShouldIncludeBaseItem(item))
                    baseSkins.Add(item.itemdefid, item);

                if (ShouldIncludeSkinItem(item))
                    skinsEconInfo.Add(item.itemdefid, item);

                if (ShouldIncludeMythicalItem(item))
                    mythicalsEconInfo.Add(item.itemdefid, item);
            }
        }

        public static bool isScrapItem(int itemdefid)
        {
            return ((itemdefid >= 19000 && itemdefid <= 19011) || itemdefid == 19044);
        }

        public static bool isBoxItem(string displayType)
        {
            return displayType.Contains("Box") || displayType.Contains("Present");
        }

        public static bool isBundleItem(string displayType)
        {
            return displayType.Contains("Bundle");
        }

        public static bool isKeyItem(string displayType)
        {
            return displayType.Contains("Key") || displayType == "Achievement Access Pass";
        }

        public static bool isModifier(string displayType)
        {
            return displayType == "Ragdoll Modifier Tool";
        }

        public static bool isCosmetic(EItemType type)
        {
            return  type == EItemType.HAT       &&
                    type == EItemType.GLASSES   &&
                    type == EItemType.MASK      &&
                    type == EItemType.SHIRT     &&
                    type == EItemType.VEST      &&
                    type == EItemType.PANTS     &&
                    type == EItemType.BACKPACK;
		}

        public UnturnedEconInfo getMythicalVariantIfExist(int itemDefId, string effect)
        {
            UnturnedEconInfo baseItem = GetItemBaseData(itemDefId);

            if (baseItem == null)
                return null;

            string baseName = baseItem.name.ToLowerInvariant();
            string effectLower = effect.ToLowerInvariant();

            foreach (var entry in mythicalsEconInfo)
            {
                UnturnedEconInfo item = entry.Value;
                if (item.quality == UnturnedEconInfo.EQuality.Mythical)
                {
                    string itemNameLower = item.name.ToLowerInvariant();

					if (itemNameLower.StartsWith("mythical "))
						itemNameLower = itemNameLower.Substring(9);

					if (itemNameLower == $"{effectLower} {baseName}")
						return item;
                }
            }

            return baseItem;
        }

        public bool isMythical(string itemName)
        {
            return itemName.ToLowerInvariant().StartsWith("mythical ");
        }

        public static bool isAchievementItem(int itemDefId)
        {
            return itemDefId >= 400000;
        }

        private bool ShouldIncludeBaseItem(UnturnedEconInfo item)
        {
            return !isMythical(item.name) &&
                   !isScrapItem(item.itemdefid) &&
                   !isBoxItem(item.display_type) &&
                   !isBundleItem(item.display_type) &&
                   !isKeyItem(item.display_type) &&
                   !isModifier(item.display_type);
        }

        private bool ShouldIncludeSkinItem(UnturnedEconInfo item)
        {
            if (item.item_skin == 0)
                return false;
            if (item.item_effect != 0)
                return false;

            if (isScrapItem(item.itemdefid))
                return false;

            if (isBoxItem(item.display_type))
                return false;

            if (isBundleItem(item.display_type))
                return false;

            if (isKeyItem(item.display_type))
                return false;

            if (isModifier(item.display_type))
                return false;

            if (item.itemdefid == 83700)
                return false;

            if (item.itemdefid >= 83600 &&
                item.itemdefid <= 83611)
                return false;

            if (item.itemdefid == 913 ||
                item.itemdefid == 914)
                return false;

            if (item.name.ToLowerInvariant().Contains("sedan"))
                return false;

            if (item.name.ToLowerInvariant().Contains("offroader"))
                return false;

            if (isAchievementItem(item.itemdefid))
                return false;

            return true;
        }

        private bool ShouldIncludeMythicalItem(UnturnedEconInfo item)
        {
            if (item.item_skin != 0)
                return false;
            if (item.item_effect == 0)
                return false;

            if (isScrapItem(item.itemdefid))
                return false;

            if (isBoxItem(item.display_type))
                return false;

            if (isBundleItem(item.display_type))
                return false;

            if (isKeyItem(item.display_type))
                return false;

            if (isModifier(item.display_type))
                return false;

            return true;
        }

        public int GetRandomSkinItemDefId()
        {
            if (skinsEconInfo == null || skinsEconInfo.Count == 0) return -1;

			return skinsEconInfo.Keys.ElementAt(
				UnityEngine.Random.Range(0, skinsEconInfo.Count));
		}

        public int GetRandomMythicalItemDefId()
        {
            if (mythicalsEconInfo == null || mythicalsEconInfo.Count == 0) return -1;

            return mythicalsEconInfo.Keys.ElementAt(
                UnityEngine.Random.Range(0, mythicalsEconInfo.Count));
        }

        public ushort GetRandomEffect()
        {
            if (_particleEffects == null || _particleEffects.Count == 0) return 1;

            return _particleEffects.Keys.ElementAt(
                UnityEngine.Random.Range(0, _particleEffects.Count));
        }


        public ushort GetEffectId(string effect)
        {
            return _allEffects.FirstOrDefault(
                pair => pair.Value.effect.Equals(
                    effect, StringComparison.OrdinalIgnoreCase)).Key;
        }

        public UnturnedEconInfo GetItemDataFromArgs(int itemdefid)
        {
            if (econInfo.TryGetValue(itemdefid, out var info))
                return info;

            return GetItemBaseData(itemdefid);
        }

        public UnturnedEconInfo GetItemBaseData(int itemdefid)
        {
            if (baseSkins == null)
            {
                Error("Base skins not yet loaded");
                return null;
            }

            if (baseSkins.TryGetValue(itemdefid, out var info))
                return info;

            return null;
        }

        public string GetItemNameFromId(int itemDefId)
        {
            if (econInfo.TryGetValue(itemDefId, out var info))
                return info.name;

            return string.Empty;
        }
    }
}
