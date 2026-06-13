import re

with open('ORTools.UI/ViewModels/MainWindowViewModel.cs', 'r', encoding='utf-8') as f:
    content = f.read()

new_method = """    public bool IsKeyInUse(string newKey, object? sourceVM = null)
    {
        if (string.IsNullOrWhiteSpace(newKey) || newKey == "None") return false;
        
        if (ToggleKey == newKey && (sourceVM as string) != "ToggleKeyTextBox") return true;
        if (Settings.DefaultToggleStateKey == newKey && (sourceVM as string) != "Settings_DefaultToggleKey") return true;
        
        if (SkillSpammer.ToggleKey == newKey && (sourceVM as string) != "SkillSpammer_ToggleKey") return true;
        foreach (var entry in SkillSpammer.Slots) if (entry.KeyName == newKey && sourceVM != entry) return true;

        foreach (var slot in AutopotHP.Slots) if (slot.Key == newKey && sourceVM != slot) return true;
        foreach (var slot in AutopotSP.Slots) if (slot.Key == newKey && sourceVM != slot) return true;
        
        foreach (var sr in Debuffs.StatusRecoveryItems)
        {
            if (sr.Key == "None") continue;
            if (sr.Key == newKey && sourceVM != sr) return true;
        }

        foreach (var dr in Debuffs.DebuffItems)
        {
            if (dr.Key == "None") continue;
            if (dr.Key == newKey && sourceVM != dr) return true;
        }

        foreach (var st in SkillTimer.Slots) if (st.Key == newKey && sourceVM != st) return true;

        foreach (var group in AutobuffSkill.SkillGroups)
        {
            foreach (var item in group.Items)
            {
                if (item.Key == "None") continue;
                if (item.Key == newKey && sourceVM != item) return true;
            }
        }

        foreach (var group in AutobuffItem.ItemGroups)
        {
            foreach (var item in group.Items)
            {
                if (item.Key == "None") continue;
                if (item.Key == newKey && sourceVM != item) return true;
            }
        }
        
        return false;
    }"""

old_method_pattern = r'    public bool IsKeyInUse\(string newKey, object\? sourceVM = null\)\s*\{.*?(?=\n    public Brush HpBarBrush)'
content = re.sub(old_method_pattern, new_method, content, flags=re.DOTALL)

with open('ORTools.UI/ViewModels/MainWindowViewModel.cs', 'w', encoding='utf-8') as f:
    f.write(content)
