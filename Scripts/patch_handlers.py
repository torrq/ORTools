import re

with open('ORTools.Worker/WorkerCore.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# For each HandleUpdate... method, if there's a TryParse block, we want to inject UnbindKeyGlobally and PushAllConfigs
# The easiest way is to do it manually for each method, since there are only about 8-10 of them.
# Let's write the replacements as (old, new) tuples

replacements = [
    (
        """        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key)) slot.Key = key;
        
        slot.HPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(hp);
        
        await BroadcastAsync(BuildAutopotHPConfig());""",
        """        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            slot.Key = key;
        }
        
        slot.HPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(hp);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutopotHPConfig());"""
    ),
    (
        """        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key)) slot.Key = key;
        
        slot.SPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(sp);
        
        await BroadcastAsync(BuildAutopotSPConfig());""",
        """        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            slot.Key = key;
        }
        
        slot.SPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(sp);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutopotSPConfig());"""
    ),
    (
        """        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key)) sr.SetKeyForList(cmd.Name, key);
        ProfileSingleton.SetConfiguration(sr);
        
        await BroadcastAsync(BuildStatusRecoveryConfig());""",
        """        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            sr.SetKeyForList(cmd.Name, key);
        }
        ProfileSingleton.SetConfiguration(sr);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildStatusRecoveryConfig());"""
    ),
    (
        """        if (Enum.TryParse<System.Windows.Forms.Keys>(cmd.Key, out var parsed)) slot.Key = parsed;
        
        slot.Delay = cmd.Delay;""",
        """        bool unbindChanged = false;
        if (Enum.TryParse<System.Windows.Forms.Keys>(cmd.Key, out var parsed))
        {
            unbindChanged = UnbindKeyGlobally(parsed);
            slot.Key = parsed;
        }
        
        slot.Delay = cmd.Delay;"""
    ),
    (
        """        ProfileSingleton.SetConfiguration(st);
        await PushSkillTimerConfig();""",
        """        ProfileSingleton.SetConfiguration(st);
        if (unbindChanged) await PushAllConfigs();
        else await PushSkillTimerConfig();"""
    ),
    (
        """            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (cmd.IsChecked || cmd.IsIndeterminate)
                    dr.buffMapping[statusId] = key;
                else
                    dr.buffMapping.Remove(statusId);
            }
        }
        ProfileSingleton.SetConfiguration(dr);
        
        await BroadcastAsync(BuildDebuffRecoveryConfig());""",
        """            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (UnbindKeyGlobally(key)) unbindChanged = true;
                if (cmd.IsChecked || cmd.IsIndeterminate)
                    dr.buffMapping[statusId] = key;
                else
                    dr.buffMapping.Remove(statusId);
            }
        }
        ProfileSingleton.SetConfiguration(dr);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildDebuffRecoveryConfig());"""
    ),
    (
        """    public async Task HandleUpdateDebuffsRecoverySettings(UpdateDebuffsRecoverySettingsCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;""",
        """    public async Task HandleUpdateDebuffsRecoverySettings(UpdateDebuffsRecoverySettingsCommand cmd)
    {
        bool unbindChanged = false;
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;"""
    ),
    (
        """            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (cmd.IsChecked || cmd.IsIndeterminate)
                    abs.buffMapping[statusId] = key;
                else
                    abs.buffMapping.Remove(statusId);
            }
        }
        ProfileSingleton.SetConfiguration(abs);
        
        await BroadcastAsync(BuildAutobuffSkillConfig());""",
        """            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (UnbindKeyGlobally(key)) unbindChanged = true;
                if (cmd.IsChecked || cmd.IsIndeterminate)
                    abs.buffMapping[statusId] = key;
                else
                    abs.buffMapping.Remove(statusId);
            }
        }
        ProfileSingleton.SetConfiguration(abs);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutobuffSkillConfig());"""
    ),
    (
        """    public async Task HandleUpdateAutobuffSkillSettings(UpdateAutobuffSkillSettingsCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;""",
        """    public async Task HandleUpdateAutobuffSkillSettings(UpdateAutobuffSkillSettingsCommand cmd)
    {
        bool unbindChanged = false;
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;"""
    ),
    (
        """            if (Enum.TryParse<Keys>(cmd.KeyStr, out var key))
            {
                if (!newMap.TryGetValue(statusId, out var list))
                {
                    list = new List<Keys>();
                    newMap[statusId] = list;
                }
                if (cmd.IsChecked || cmd.IsIndeterminate)
                    list.Add(key);
            }
        }
        ProfileSingleton.SetConfiguration(abi);
        
        await BroadcastAsync(BuildAutobuffItemConfig());""",
        """            if (Enum.TryParse<Keys>(cmd.KeyStr, out var key))
            {
                if (UnbindKeyGlobally(key)) unbindChanged = true;
                if (!newMap.TryGetValue(statusId, out var list))
                {
                    list = new List<Keys>();
                    newMap[statusId] = list;
                }
                if (cmd.IsChecked || cmd.IsIndeterminate)
                    list.Add(key);
            }
        }
        ProfileSingleton.SetConfiguration(abi);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutobuffItemConfig());"""
    ),
    (
        """    public async Task HandleUpdateAutobuffItemSettings(UpdateAutobuffItemSettingsCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;""",
        """    public async Task HandleUpdateAutobuffItemSettings(UpdateAutobuffItemSettingsCommand cmd)
    {
        bool unbindChanged = false;
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;"""
    ),
    (
        """    public async Task HandleUpdateSkillSpammerEntry(UpdateSkillSpammerEntryCommand cmd)
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        if (Enum.TryParse<Keys>(cmd.KeyName, out var key))
        {
            if (cmd.IsChecked || cmd.IsIndeterminate)
            {
                spammer.AddSkillSpammerEntry(cmd.KeyName, new KeyConfig(key, cmd.IsChecked, cmd.IsIndeterminate));
            }
            else
            {
                spammer.RemoveSkillSpammerEntry(cmd.KeyName);
            }
            ProfileSingleton.SetConfiguration(spammer);
            await BroadcastAsync(BuildSkillSpammerConfig());
        }
    }""",
        """    public async Task HandleUpdateSkillSpammerEntry(UpdateSkillSpammerEntryCommand cmd)
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.KeyName, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            if (cmd.IsChecked || cmd.IsIndeterminate)
            {
                spammer.AddSkillSpammerEntry(cmd.KeyName, new KeyConfig(key, cmd.IsChecked, cmd.IsIndeterminate));
            }
            else
            {
                spammer.RemoveSkillSpammerEntry(cmd.KeyName);
            }
            ProfileSingleton.SetConfiguration(spammer);
            if (unbindChanged) await PushAllConfigs();
            else await BroadcastAsync(BuildSkillSpammerConfig());
        }
    }"""
    ),
    (
        """        if (Enum.TryParse<Keys>(cmd.ToggleModeKey, out var toggleKey))
        {
            spammer.ToggleModeKey = toggleKey;
        }

        ProfileSingleton.SetConfiguration(spammer);
        await BroadcastAsync(BuildSkillSpammerConfig());""",
        """        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.ToggleModeKey, out var toggleKey))
        {
            unbindChanged = UnbindKeyGlobally(toggleKey);
            spammer.ToggleModeKey = toggleKey;
        }

        ProfileSingleton.SetConfiguration(spammer);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildSkillSpammerConfig());"""
    ),
    (
        """    public async Task HandleUpdateToggleKey(string keyStr)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        prefs.ToggleStateKey = keyStr;
        ProfileSingleton.SetConfiguration(prefs);
        RefreshToggleHotkey();
        
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: keyStr));
    }""",
        """    public async Task HandleUpdateToggleKey(string keyStr)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(keyStr, ignoreCase: true, out var key)) unbindChanged = UnbindKeyGlobally(key);
        prefs.ToggleStateKey = keyStr;
        ProfileSingleton.SetConfiguration(prefs);
        RefreshToggleHotkey();
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: keyStr));
    }"""
    ),
    (
        """    public async Task HandleUpdateGlobalConfig(UpdateGlobalConfigCommand cmd)
    {
        var config = ConfigGlobal.GetConfig();
        config.SongRows = cmd.SongRows;
        config.MacroSwitchRows = cmd.MacroSwitchRows;
        config.DefaultToggleStateKey = cmd.DefaultToggleStateKey;""",
        """    public async Task HandleUpdateGlobalConfig(UpdateGlobalConfigCommand cmd)
    {
        var config = ConfigGlobal.GetConfig();
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.DefaultToggleStateKey, ignoreCase: true, out var dtk)) unbindChanged = UnbindKeyGlobally(dtk);
        config.SongRows = cmd.SongRows;
        config.MacroSwitchRows = cmd.MacroSwitchRows;
        config.DefaultToggleStateKey = cmd.DefaultToggleStateKey;"""
    ),
    (
        """        config.AlwaysOnTop = cmd.AlwaysOnTop;
        ConfigGlobal.SaveConfig();
    }""",
        """        config.AlwaysOnTop = cmd.AlwaysOnTop;
        ConfigGlobal.SaveConfig();
        if (unbindChanged) await PushAllConfigs();
    }"""
    )
]

for old, new in replacements:
    content = content.replace(old, new)

with open('ORTools.Worker/WorkerCore.cs', 'w', encoding='utf-8') as f:
    f.write(content)
