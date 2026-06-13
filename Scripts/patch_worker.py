import re

with open('ORTools.Worker/WorkerCore.cs', 'r', encoding='utf-8') as f:
    content = f.read()

unbind_method = """
    private bool UnbindKeyGlobally(Keys key)
    {
        if (key == Keys.None) return false;
        bool changed = false;
        var p = ProfileSingleton.GetCurrent();
        var prefs = p.UserPreferences;
        
        if (Enum.TryParse<Keys>(prefs.ToggleStateKey, out var tk) && tk == key)
        {
            prefs.ToggleStateKey = "None";
            changed = true;
        }
        if (Enum.TryParse<Keys>(ConfigGlobal.GetConfig().DefaultToggleStateKey, out var dtk) && dtk == key)
        {
            ConfigGlobal.GetConfig().DefaultToggleStateKey = "None";
            ConfigGlobal.SaveConfig();
            changed = true;
        }

        foreach (var slot in p.AutopotHP.HPSlots) if (slot.Key == key) { slot.Key = Keys.None; changed = true; }
        foreach (var slot in p.AutopotSP.SPSlots) if (slot.Key == key) { slot.Key = Keys.None; changed = true; }
        foreach (var slot in p.SkillTimer.skillTimer) if (slot.Value.Key == key) { slot.Value.Key = Keys.None; changed = true; }
        
        foreach (var kvp in p.AutobuffSkill.buffMapping.ToList())
        {
            if (kvp.Value == key) { p.AutobuffSkill.buffMapping.Remove(kvp.Key); changed = true; }
        }
        foreach (var kvp in p.AutobuffItem.buffMapping.ToList())
        {
            if (kvp.Value.Contains(key)) 
            { 
                kvp.Value.Remove(key); 
                if (kvp.Value.Count == 0) p.AutobuffItem.buffMapping.Remove(kvp.Key);
                changed = true; 
            }
        }
        foreach (var list in new[] { p.StatusRecovery.Panacea, p.StatusRecovery.RoyalJelly, p.StatusRecovery.GreenPotion })
        {
            if (list.Key == key) { list.Key = Keys.None; changed = true; }
        }
        foreach (var kvp in p.DebuffsRecovery.buffMapping.ToList())
        {
            if (kvp.Value == key) { p.DebuffsRecovery.buffMapping.Remove(kvp.Key); changed = true; }
        }
        foreach (var kvp in p.SkillSpammer.SpammerEntries)
        {
            if (kvp.Value.Key == key) { kvp.Value.Key = Keys.None; changed = true; }
        }
        if (p.SkillSpammer.ToggleModeKey == key) { p.SkillSpammer.ToggleModeKey = Keys.None; changed = true; }
        
        if (changed)
        {
            ProfileSingleton.SetConfiguration(prefs);
            ProfileSingleton.SetConfiguration(p.AutopotHP);
            ProfileSingleton.SetConfiguration(p.AutopotSP);
            ProfileSingleton.SetConfiguration(p.SkillTimer);
            ProfileSingleton.SetConfiguration(p.AutobuffSkill);
            ProfileSingleton.SetConfiguration(p.AutobuffItem);
            ProfileSingleton.SetConfiguration(p.StatusRecovery);
            ProfileSingleton.SetConfiguration(p.DebuffsRecovery);
            ProfileSingleton.SetConfiguration(p.SkillSpammer);
        }
        return changed;
    }
"""

if "UnbindKeyGlobally" not in content:
    content = content.replace("    // ── Broadcast ─────────────────────────────────────────────────────────────", unbind_method + "\n    // ── Broadcast ─────────────────────────────────────────────────────────────")
    
# Now we replace the usages in all HandleUpdate methods
# Instead of complex regex, we'll just inject a boolean flag and call UnbindKeyGlobally before Enum.TryParse, or right after.
# Wait, let's just make the script do it via regex replacing "if (Enum.TryParse<Keys>(cmd.Key... out var key))" with a block

with open('ORTools.Worker/WorkerCore.cs', 'w', encoding='utf-8') as f:
    f.write(content)
