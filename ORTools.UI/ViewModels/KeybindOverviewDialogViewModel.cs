using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.UI.Helpers;
using ORTools.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ORTools.UI.ViewModels;

public sealed record KeybindEntry(
    string KeyDisplay,
    string RawKey,
    string TabName,
    string Feature,
    int TabIndex,
    bool IsPaused,
    int SharedCount)
{
    public bool IsShared => SharedCount > 1;
    public string SharedLabel => $"×{SharedCount}";
    public bool CanJump => TabIndex >= 0;
}

public sealed partial class KeybindOverviewDialogViewModel : ViewModelBase, ICancelableDialog
{
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _main;
    private readonly List<KeybindEntry> _allEntries = new();

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private ObservableCollection<KeybindEntry> _filteredEntries = new();

    public string SummaryText => string.Format(
        LanguageService.Get("S.Keybinds.Summary"),
        _main.CurrentProfile,
        _allEntries.Count);

    public bool HasEntries => FilteredEntries.Count > 0;

    public KeybindOverviewDialogViewModel(MainWindowViewModel main, IDialogService dialogService)
    {
        _main = main;
        _dialogService = dialogService;
        BuildEntries();
        ApplyFilter();
    }

    private void BuildEntries()
    {
        var raw = new List<(string RawKey, string TabName, string Feature, int TabIndex, bool IsPaused)>();

        static bool IsValid(string? k) =>
            !string.IsNullOrWhiteSpace(k) && !k.Equals("None", StringComparison.OrdinalIgnoreCase);

        void Add(string? key, string tab, string feature, int tabIndex, bool isPaused = false)
        {
            if (!IsValid(key)) return;
            raw.Add((key!.Trim(), tab, feature, tabIndex, isPaused));
        }

        // Main window global toggle
        Add(_main.ToggleKey, "Global", "Turn ON / OFF", -1);

        // Profile switch hotkeys (Tab 10)
        foreach (var p in _main.Profiles.ProfileList)
        {
            if (p.HasHotkey)
                Add(p.Hotkey, "Profiles", $"Switch -> {p.Name}", 10);
        }

        // 0: Skill Spammer
        Add(_main.SkillSpammer.ToggleModeKey, "Skill Spammer", "Toggle Mode Key", 0, isPaused: !_main.SkillSpammer.ToggleMode);
        foreach (var k in _main.SkillSpammer.AllKeys)
        {
            if (k.IsCheckedState == true)
                Add(k.KeyName, "Skill Spammer", "Spam (With Click)", 0);
            else if (k.IsCheckedState == null)
                Add(k.KeyName, "Skill Spammer", "Spam (No Click)", 0);
        }

        // 1: Skill Timer
        foreach (var s in _main.SkillTimer.Slots)
        {
            if (IsValid(s.Key))
                Add(s.AltKey ? $"Alt+{s.Key}" : s.Key, "Skill Timer", $"Slot #{s.Id} ({s.Delay}ms)", 1, isPaused: !s.Enabled);
        }

        // 2: Autopot HP & SP
        foreach (var s in _main.AutopotHP.Slots)
        {
            if (IsValid(s.Key))
                Add(s.Key, "Autopot", $"HP #{s.Id} (< {s.Percent}%)", 2, isPaused: !s.Enabled);
        }
        foreach (var s in _main.AutopotSP.Slots)
        {
            if (IsValid(s.Key))
                Add(s.Key, "Autopot", $"SP #{s.Id} (< {s.Percent}%)", 2, isPaused: !s.Enabled);
        }

        // 3: Autobuff Skills & Items
        foreach (var g in _main.AutobuffSkill.SkillGroups)
        {
            foreach (var item in g.Items)
            {
                if (IsValid(item.Key))
                    Add(item.Key, "Autobuff", item.DisplayName, 3, isPaused: !item.IsEnabled);
            }
        }
        foreach (var g in _main.AutobuffItem.ItemGroups)
        {
            foreach (var item in g.Items)
            {
                if (IsValid(item.Key))
                    Add(item.Key, "Autobuff", item.DisplayName, 3, isPaused: !item.IsEnabled);
            }
        }

        // 4: Songs
        for (int i = 0; i < _main.MacroSong.Rows.Count; i++)
        {
            var r = _main.MacroSong.Rows[i];
            int rowNum = i + 1;
            Add(r.TriggerKey, "Songs", $"Row {rowNum} Trigger", 4);
            Add(r.AdaptationKey, "Songs", $"Row {rowNum} Adaptation", 4);
            Add(r.InstrumentKey, "Songs", $"Row {rowNum} Instrument", 4);
            for (int s = 0; s < r.Steps.Count; s++)
                Add(r.Steps[s].Key, "Songs", $"Row {rowNum} Song {s + 1}", 4);
        }

        // 5: Macro Switch
        for (int i = 0; i < _main.MacroSwitch.Rows.Count; i++)
        {
            var r = _main.MacroSwitch.Rows[i];
            int rowNum = i + 1;
            Add(r.TriggerKey, "Macro Switch", $"Chain {rowNum} Trigger", 5);
            for (int s = 0; s < r.Steps.Count; s++)
                Add(r.Steps[s].Key, "Macro Switch", $"Chain {rowNum} Step {s + 1}", 5);
        }

        // 6: ATK x DEF
        foreach (var r in _main.AtkDef.Rows)
        {
            Add(r.SpammerKey, "ATK x DEF", $"Row {r.Id} Spam Key", 6);
            Add(r.AtkHead, "ATK x DEF", $"Row {r.Id} ATK Head", 6);
            Add(r.AtkBody, "ATK x DEF", $"Row {r.Id} ATK Body", 6);
            Add(r.AtkWeapon, "ATK x DEF", $"Row {r.Id} ATK Weapon", 6);
            Add(r.AtkShield, "ATK x DEF", $"Row {r.Id} ATK Shield", 6);
            Add(r.AtkGarment, "ATK x DEF", $"Row {r.Id} ATK Garment", 6);
            Add(r.AtkShoes, "ATK x DEF", $"Row {r.Id} ATK Shoes", 6);
            Add(r.DefHead, "ATK x DEF", $"Row {r.Id} DEF Head", 6);
            Add(r.DefBody, "ATK x DEF", $"Row {r.Id} DEF Body", 6);
            Add(r.DefWeapon, "ATK x DEF", $"Row {r.Id} DEF Weapon", 6);
            Add(r.DefShield, "ATK x DEF", $"Row {r.Id} DEF Shield", 6);
            Add(r.DefGarment, "ATK x DEF", $"Row {r.Id} DEF Garment", 6);
            Add(r.DefShoes, "ATK x DEF", $"Row {r.Id} DEF Shoes", 6);
        }

        // 7: Debuffs & Status Recovery
        foreach (var item in _main.Debuffs.StatusRecoveryItems)
        {
            if (IsValid(item.Key))
                Add(item.Key, "Debuffs", item.DisplayName, 7, isPaused: !item.IsEnabled);
        }
        foreach (var item in _main.Debuffs.DebuffItems)
        {
            if (IsValid(item.Key))
                Add(item.Key, "Debuffs", item.DisplayName, 7, isPaused: !item.IsEnabled);
        }

        // 8: Misc
        Add(_main.Misc.TransferKey, "Misc", "Transfer Helper", 8);

        // 9: Auto Off
        if (IsValid(_main.AutoOff.AutoOffKey1))
            Add($"Alt+{_main.AutoOff.AutoOffKey1}", "Auto Off", "Macro Key 1", 9);
        if (IsValid(_main.AutoOff.AutoOffKey2))
            Add($"Alt+{_main.AutoOff.AutoOffKey2}", "Auto Off", "Macro Key 2", 9);

        // Count shared keys (case-insensitive)
        var counts = raw
            .GroupBy(x => x.RawKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var r in raw)
        {
            string display = KeyToStringConverter.FormatKey(r.RawKey);

            _allEntries.Add(new KeybindEntry(
                display,
                r.RawKey,
                r.TabName,
                r.Feature,
                r.TabIndex,
                r.IsPaused,
                counts[r.RawKey]));
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        string q = (FilterText ?? "").Trim();
        var items = string.IsNullOrEmpty(q)
            ? _allEntries
            : _allEntries.Where(e =>
                e.KeyDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.RawKey.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.TabName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Feature.Contains(q, StringComparison.OrdinalIgnoreCase));

        FilteredEntries = new ObservableCollection<KeybindEntry>(items);
        OnPropertyChanged(nameof(HasEntries));
    }

    [RelayCommand]
    private void JumpToTab(KeybindEntry entry)
    {
        _dialogService.CloseDialog();
        if (entry.TabIndex >= 0)
        {
            _main.SelectedMainTabIndex = entry.TabIndex;
        }
    }

    [RelayCommand]
    private void Close() => _dialogService.CloseDialog();

    [RelayCommand]
    private void Cancel() => Close();
}
