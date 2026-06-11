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
    }
}
