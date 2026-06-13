import re

with open('ORTools.Worker/WorkerCore.cs', 'r', encoding='utf-8') as f:
    content = f.read()

replacements = [
    (
        """    public async Task HandleUpdateSkillTimerSlot(UpdateSkillTimerSlotCommand cmd)
    {
        var st = ProfileSingleton.GetCurrent().SkillTimer;
        if (!st.skillTimer.TryGetValue(cmd.Id, out var slot))
        {
            slot = new SkillTimerKey(System.Windows.Forms.Keys.None, 1000);
            st.skillTimer[cmd.Id] = slot;
        }

        if (Enum.TryParse<System.Windows.Forms.Keys>(cmd.Key, out var parsed))
            slot.Key = parsed;
        
        slot.Delay = cmd.Delay;
        slot.ClickMode = cmd.ClickMode;
        slot.AltKey = cmd.AltKey;
        slot.Enabled = cmd.Enabled;

        ProfileSingleton.SetConfiguration(st);
        
        // If the worker is ON, toggle the specific timer thread
        if (_isOn)
        {
            if (slot.Enabled) st.skillTimerThreads[cmd.Id].Start();
            else st.skillTimerThreads[cmd.Id].Stop();
        }
        
        await PushSkillTimerConfig();
    }""",
        """    public async Task HandleUpdateSkillTimerSlot(UpdateSkillTimerSlotCommand cmd)
    {
        var st = ProfileSingleton.GetCurrent().SkillTimer;
        if (!st.skillTimer.TryGetValue(cmd.Id, out var slot))
        {
            slot = new SkillTimerKey(System.Windows.Forms.Keys.None, 1000);
            st.skillTimer[cmd.Id] = slot;
        }

        bool unbindChanged = false;
        if (Enum.TryParse<System.Windows.Forms.Keys>(cmd.Key, out var parsed))
        {
            unbindChanged = UnbindKeyGlobally(parsed);
            slot.Key = parsed;
        }
        
        slot.Delay = cmd.Delay;
        slot.ClickMode = cmd.ClickMode;
        slot.AltKey = cmd.AltKey;
        slot.Enabled = cmd.Enabled;

        ProfileSingleton.SetConfiguration(st);
        
        // If the worker is ON, toggle the specific timer thread
        if (_isOn)
        {
            if (slot.Enabled) st.skillTimerThreads[cmd.Id].Start();
            else st.skillTimerThreads[cmd.Id].Stop();
        }
        
        if (unbindChanged) await PushAllConfigs();
        else await PushSkillTimerConfig();
    }"""
    ),
    (
        """    public async Task HandleUpdateDebuffRecoveryItem(UpdateDebuffRecoveryItemCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (key == Keys.None) dr.RemoveKeyFromBuff(statusId);
                else dr.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(dr);
        await BroadcastAsync(BuildDebuffRecoveryConfig());
    }""",
        """    public async Task HandleUpdateDebuffRecoveryItem(UpdateDebuffRecoveryItemCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        bool unbindChanged = false;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                unbindChanged = UnbindKeyGlobally(key);
                if (key == Keys.None) dr.RemoveKeyFromBuff(statusId);
                else dr.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(dr);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildDebuffRecoveryConfig());
    }"""
    ),
    (
        """    public async Task HandleUpdateAutobuffSkillItem(UpdateAutobuffSkillItemCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (key == Keys.None) abs.RemoveKeyFromBuff(statusId);
                else abs.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abs);
        await BroadcastAsync(BuildAutobuffSkillConfig());
    }""",
        """    public async Task HandleUpdateAutobuffSkillItem(UpdateAutobuffSkillItemCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        bool unbindChanged = false;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                unbindChanged = UnbindKeyGlobally(key);
                if (key == Keys.None) abs.RemoveKeyFromBuff(statusId);
                else abs.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abs);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutobuffSkillConfig());
    }"""
    ),
    (
        """    public async Task HandleUpdateAutobuffItemItem(UpdateAutobuffItemCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (key == Keys.None) abi.RemoveKeyFromBuff(statusId);
                else abi.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abi);
        await BroadcastAsync(BuildAutobuffItemConfig());
    }""",
        """    public async Task HandleUpdateAutobuffItemItem(UpdateAutobuffItemCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        bool unbindChanged = false;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                unbindChanged = UnbindKeyGlobally(key);
                if (key == Keys.None) abi.RemoveKeyFromBuff(statusId);
                else abi.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abi);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutobuffItemConfig());
    }"""
    )
]

for old, new in replacements:
    content = content.replace(old, new)

with open('ORTools.Worker/WorkerCore.cs', 'w', encoding='utf-8') as f:
    f.write(content)
