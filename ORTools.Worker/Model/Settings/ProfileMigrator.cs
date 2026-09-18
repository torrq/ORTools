using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ORTools.Worker
{
    /// <summary>
    /// Handles migrating legacy profile configurations from the old .NET Framework WinForms app 
    /// to the modern .NET 8 WPF architecture.
    /// </summary>
    public static class ProfileMigrator
    {
        private static readonly string[] SlotNames = ["Head", "Body", "Weapon", "Shield", "Garment", "Shoes"];

        /// <summary>
        /// Runs all necessary migrations on a newly loaded profile to ensure 
        /// older settings map correctly to the new architecture.
        /// Returns true if any data was migrated.
        /// </summary>
        public static bool Migrate(Profile profile)
        {
            if (profile == null) return false;
            
            bool migrated = false;
            migrated |= MigrateSkillSpammerKeys(profile);
            migrated |= MigrateSkillSpammerNumberKeys(profile);
            migrated |= MigrateMacroSwitchSteps(profile);
            migrated |= MigrateAtkDef(profile);

            if (profile.UserPreferences != null && profile.UserPreferences.ConfigVersion < AppConfig.ConfigVersion)
            {
                profile.UserPreferences.ConfigVersion = AppConfig.ConfigVersion;
                migrated = true;
            }

            return migrated;
        }

        /// <summary>
        /// Migration: Skill Spammer legacy number keys
        /// 
        /// Reason:
        /// In legacy profiles, keys 1-9 were saved directly as integers 1-9. In the Keys enum,
        /// 1 is LButton, 2 is RButton, etc. This caused left-clicks to trigger the spammer.
        /// We need to remap 1-9 to D1-D9 (which are 49-57 in the Keys enum).
        /// </summary>
        private static bool MigrateSkillSpammerNumberKeys(Profile profile)
        {
            if (profile.SkillSpammer?.SpammerEntries == null) return false;

            var numberKeysToMigrate = profile.SkillSpammer.SpammerEntries.Values
                .Where(c => (int)c.Key >= 1 && (int)c.Key <= 9)
                .ToList();

            if (numberKeysToMigrate.Count == 0) return false;

            foreach (var config in numberKeysToMigrate)
            {
                // Remove the old entry
                profile.SkillSpammer.SpammerEntries.TryRemove(config.Key.ToString(), out var _);
                profile.SkillSpammer.SpammerEntries.TryRemove(((int)config.Key).ToString(), out var _); // sometimes it's saved as "1"
                
                // Remap to D1-D9
                int oldVal = (int)config.Key;
                config.Key = (Keys)(oldVal + 48); // 1 + 48 = 49 (Keys.D1)
                
                // Re-insert with the new enum string
                profile.SkillSpammer.SpammerEntries[config.Key.ToString()] = config;
            }
            return true;
        }

        /// <summary>
        /// Migration: Skill Spammer legacy 'chk' prefixes
        /// 
        /// Reason: 
        /// In the legacy WinForms UI, the checkboxes representing skill spammer keys 
        /// were named with a "chk" prefix (e.g. "chkR", "chkF1"). The legacy profile 
        /// JSON serialized these exact WinForms control names as the dictionary keys.
        /// In the new WPF UI, we bind purely on the `Keys` enum string representations 
        /// (e.g. "R", "F1"). 
        /// 
        /// This migration detects keys starting with "chk", strips the prefix by looking
        /// at the actual enum value stored in the KeyConfig, and updates the dictionary 
        /// so the new UI can find and render them correctly.
        /// </summary>
        private static bool MigrateSkillSpammerKeys(Profile profile)
        {
            if (profile.SkillSpammer?.SpammerEntries == null) return false;

            var oldKeys = profile.SkillSpammer.SpammerEntries.Keys
                .Where(k => k.StartsWith("chk"))
                .ToList();

            if (oldKeys.Count == 0) return false;

            foreach (var k in oldKeys)
            {
                var val = profile.SkillSpammer.SpammerEntries[k];
                profile.SkillSpammer.SpammerEntries.TryRemove(k, out var _);
                
                // Re-insert using the clean enum string (e.g., "R" instead of "chkR")
                profile.SkillSpammer.SpammerEntries[val.Key.ToString()] = val;
            }
            return true;
        }

        /// <summary>
        /// Migration: Macro Switch steps extension
        /// 
        /// Reason:
        /// The old application had 7 Macro Switch steps. We updated it to 9.
        /// This ensures legacy profiles with 7 steps are padded up to TOTAL_MACRO_KEYS
        /// with default empty entries so the UI bindings don't fail.
        /// </summary>
        private static bool MigrateMacroSwitchSteps(Profile profile)
        {
            if (profile.MacroSwitch?.ChainConfigs == null) return false;

            bool anyChanged = false;
            foreach (var chainConfig in profile.MacroSwitch.ChainConfigs)
            {
                if (chainConfig.macroEntries == null)
                {
                    chainConfig.macroEntries = new List<MacroSwitchKey>();
                    anyChanged = true;
                }

                while (chainConfig.macroEntries.Count < MacroSwitchKey.TOTAL_MACRO_KEYS)
                {
                    chainConfig.macroEntries.Add(new MacroSwitchKey(Keys.None, AppConfig.MacroDefaultDelay));
                    anyChanged = true;
                }
            }
            return anyChanged;
        }

        /// <summary>
        /// Migration: ATK x DEF legacy slot keys, key spammer remapping, and slot name normalization
        /// 
        /// Reason:
        /// In legacy WinForms profiles, ATK x DEF equipment slots were stored in DefKeys / AtkKeys 
        /// using control names like "in1Def1".."in1Def6" and "in1Atk1".."in1Atk6", with key values 
        /// saved as enum integers (e.g. 112 for F1). Additionally, KeySpammer was saved as an 
        /// integer (0 for None, 1-9 for number keys, or raw enum codes like 112).
        /// 
        /// Modern ORTools binds DefKeys and AtkKeys to semantic slot names ("Head", "Body", 
        /// "Weapon", "Shield", "Garment", "Shoes") and expects clean string key names (e.g. "F1", "None").
        /// </summary>
        private static bool MigrateAtkDef(Profile profile)
        {
            if (profile.ATKDEFMode?.EquipConfigs == null) return false;

            bool anyChanged = false;

            // Check if there are duplicate IDs or list corruption
            if (profile.ATKDEFMode.EquipConfigs.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            {
                profile.ATKDEFMode.EnsureCorrectRowCount(ConfigGlobal.GetConfig().AtkDefRows);
                anyChanged = true;
            }

            foreach (var equipConfig in profile.ATKDEFMode.EquipConfigs)
            {
                // 1. Normalize KeySpammer
                string normalizedSpammer = NormalizeKeyString(equipConfig.KeySpammer);
                if (equipConfig.KeySpammer != normalizedSpammer)
                {
                    equipConfig.KeySpammer = normalizedSpammer;
                    anyChanged = true;
                }

                // 2. Migrate DefKeys
                if (MigrateSlotDictionary(equipConfig.DefKeys))
                {
                    anyChanged = true;
                }

                // 3. Migrate AtkKeys
                if (MigrateSlotDictionary(equipConfig.AtkKeys))
                {
                    anyChanged = true;
                }
            }

            return anyChanged;
        }

        private static bool MigrateSlotDictionary(ConcurrentDictionary<string, string> dict)
        {
            if (dict == null || dict.IsEmpty) return false;

            bool needsMigration = false;

            // Check if any key is not a canonical slot name or if any value is numeric or "0"
            foreach (var kvp in dict)
            {
                if (!SlotNames.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase) ||
                    int.TryParse(kvp.Value, out _) ||
                    kvp.Value == "0" ||
                    string.IsNullOrWhiteSpace(kvp.Value))
                {
                    needsMigration = true;
                    break;
                }
            }

            if (!needsMigration) return false;

            var newDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in dict)
            {
                string normalizedVal = NormalizeKeyString(kvp.Value);
                if (normalizedVal == "None")
                {
                    // Unassigned or None: skip adding to dictionary
                    continue;
                }

                string? targetSlot = ResolveSlotName(kvp.Key);
                if (targetSlot != null)
                {
                    newDict[targetSlot] = normalizedVal;
                }
            }

            dict.Clear();
            foreach (var kvp in newDict)
            {
                dict[kvp.Key] = kvp.Value;
            }

            return true;
        }

        private static string? ResolveSlotName(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName)) return null;

            // Check if already a valid canonical slot name
            foreach (var slot in SlotNames)
            {
                if (slot.Equals(keyName, StringComparison.OrdinalIgnoreCase))
                    return slot;
            }

            // Legacy patterns: "in1Def1", "in1Atk1", "in2Def3", "Def4", "Atk5", etc.
            // Check trailing digit 1-6
            if (keyName.Length > 0 && char.IsDigit(keyName[^1]))
            {
                int index = keyName[^1] - '0';
                if (index >= 1 && index <= 6)
                {
                    return SlotNames[index - 1];
                }
            }

            return null;
        }

        private static string NormalizeKeyString(string? rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey) || rawKey.Equals("None", StringComparison.OrdinalIgnoreCase))
                return "None";

            // If it's a numeric string (e.g. "0", "1", "112")
            if (int.TryParse(rawKey, out int intVal))
            {
                if (intVal == 0) return "None";
                if (intVal >= 1 && intVal <= 9)
                {
                    // Remap legacy 1-9 to D1-D9 (49-57)
                    return ((Keys)(intVal + 48)).ToString();
                }
                if (Enum.IsDefined(typeof(Keys), intVal))
                {
                    return ((Keys)intVal).ToString();
                }
                return "None";
            }

            // If it's an enum name (e.g. "F1", "D1", "R")
            if (Enum.TryParse<Keys>(rawKey, true, out var parsed))
            {
                return parsed == Keys.None ? "None" : parsed.ToString();
            }

            return "None";
        }
    }
}
