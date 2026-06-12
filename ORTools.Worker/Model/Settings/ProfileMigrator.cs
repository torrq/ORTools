using System.Linq;

namespace ORTools.Worker
{
    /// <summary>
    /// Handles migrating legacy profile configurations from the old .NET Framework WinForms app 
    /// to the modern .NET 8 WPF architecture.
    /// </summary>
    public static class ProfileMigrator
    {
        /// <summary>
        /// Runs all necessary migrations on a newly loaded profile to ensure 
        /// older settings map correctly to the new architecture.
        /// </summary>
        public static void Migrate(Profile profile)
        {
            if (profile == null) return;
            
            MigrateSkillSpammerKeys(profile);
            MigrateMacroSwitchSteps(profile);
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
        private static void MigrateSkillSpammerKeys(Profile profile)
        {
            if (profile.SkillSpammer?.SpammerEntries == null) return;

            var oldKeys = profile.SkillSpammer.SpammerEntries.Keys
                .Where(k => k.StartsWith("chk"))
                .ToList();

            foreach (var k in oldKeys)
            {
                var val = profile.SkillSpammer.SpammerEntries[k];
                profile.SkillSpammer.SpammerEntries.Remove(k);
                
                // Re-insert using the clean enum string (e.g., "R" instead of "chkR")
                profile.SkillSpammer.SpammerEntries[val.Key.ToString()] = val;
            }
        }

        /// <summary>
        /// Migration: Macro Switch steps extension
        /// 
        /// Reason:
        /// The old application had 7 Macro Switch steps. We updated it to 9.
        /// This ensures legacy profiles with 7 steps are padded up to TOTAL_MACRO_KEYS
        /// with default empty entries so the UI bindings don't fail.
        /// </summary>
        private static void MigrateMacroSwitchSteps(Profile profile)
        {
            if (profile.MacroSwitch?.ChainConfigs == null) return;

            foreach (var chainConfig in profile.MacroSwitch.ChainConfigs)
            {
                if (chainConfig.macroEntries == null)
                    chainConfig.macroEntries = new System.Collections.Generic.List<MacroSwitchKey>();

                while (chainConfig.macroEntries.Count < MacroSwitchKey.TOTAL_MACRO_KEYS)
                {
                    chainConfig.macroEntries.Add(new MacroSwitchKey(System.Windows.Forms.Keys.None, AppConfig.MacroDefaultDelay));
                }
            }
        }
    }
}
