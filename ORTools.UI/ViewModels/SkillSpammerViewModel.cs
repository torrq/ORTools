using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Linq;

namespace ORTools.UI.ViewModels;

public partial class SpammerKeyViewModel : ObservableObject
{
    private readonly WorkerService _worker;

    public string KeyName { get; }
    public string ImagePath { get; }

    [ObservableProperty]
    private bool? _isCheckedState; // null = Indeterminate (No Click), true = Checked (With Click), false = Unchecked (Disabled)

    public bool SuppressCommands { get; set; }

    public SpammerKeyViewModel(WorkerService worker, string keyName, string imagePath, bool? initialState)
    {
        _worker = worker;
        KeyName = keyName;
        ImagePath = imagePath;
        _isCheckedState = initialState;
    }

    partial void OnIsCheckedStateChanged(bool? value)
    {
        if (SuppressCommands) return;
        bool clickActive = value == true;
        bool isIndeterminate = value == null;
        _ = _worker.SendAsync(new UpdateSkillSpammerEntryCommand(KeyName, clickActive, isIndeterminate));
    }
}

public partial class SkillSpammerViewModel : ObservableObject
{
    private readonly WorkerService _worker;
    private bool _suppressCommands = false;

    [ObservableProperty]
    private int _spammerDelay = 50;

    [ObservableProperty]
    private bool _mouseFlick;

    [ObservableProperty]
    private bool _noShift;

    [ObservableProperty]
    private bool _toggleMode;

    [ObservableProperty]
    private string _toggleModeKey = "None";

    [ObservableProperty]
    private string _questImageSource = "pack://application:,,,/Assets/Banners/osro_quests_mr.png";

    [ObservableProperty]
    private string _questUrl = "https://torrq.github.io/osro-quests-mr/";

    public ObservableCollection<SpammerKeyViewModel> AllKeys { get; } = new();
    public ObservableCollection<SpammerKeyViewModel> FKeys { get; } = new();
    public ObservableCollection<SpammerKeyViewModel> NumKeys { get; } = new();
    public ObservableCollection<SpammerKeyViewModel> QKeys { get; } = new();
    public ObservableCollection<SpammerKeyViewModel> AKeys { get; } = new();
    public ObservableCollection<SpammerKeyViewModel> ZKeys { get; } = new();

    public SkillSpammerViewModel(WorkerService worker)
    {
        _worker = worker;
        
        InitializeKeyGrid();

        _worker.SkillSpammerConfigReceived += OnConfigReceived;
        _worker.AppStateReceived += OnAppState;
    }

    private void InitializeKeyGrid()
    {
        string[] fKeys = { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9" };
        string[] numKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        string[] qKeys = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O" };
        string[] aKeys = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
        string[] zKeys = { "Z", "X", "C", "V", "B", "N", "M" };

        foreach (var k in fKeys) { var vm = new SpammerKeyViewModel(_worker, k, $"/Assets/Icons/Key/key_{k.ToLower()}.png", false); FKeys.Add(vm); AllKeys.Add(vm); }
        foreach (var k in numKeys) { var vm = new SpammerKeyViewModel(_worker, k, $"/Assets/Icons/Key/key_{k.ToLower()}.png", false); NumKeys.Add(vm); AllKeys.Add(vm); }
        foreach (var k in qKeys) { var vm = new SpammerKeyViewModel(_worker, k, $"/Assets/Icons/Key/key_{k.ToLower()}.png", false); QKeys.Add(vm); AllKeys.Add(vm); }
        foreach (var k in aKeys) { var vm = new SpammerKeyViewModel(_worker, k, $"/Assets/Icons/Key/key_{k.ToLower()}.png", false); AKeys.Add(vm); AllKeys.Add(vm); }
        foreach (var k in zKeys) { var vm = new SpammerKeyViewModel(_worker, k, $"/Assets/Icons/Key/key_{k.ToLower()}.png", false); ZKeys.Add(vm); AllKeys.Add(vm); }
    }

    private void OnAppState(AppStateUpdate u)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            if (u.ServerMode == 1)
            {
                QuestImageSource = "pack://application:,,,/Assets/Banners/osro_quests_hr.png";
                QuestUrl = "https://torrq.github.io/osro-quests-hr/";
            }
            else
            {
                // Show MR quests/MVP links
                ShowHrQuests = false;
                QuestImageSource = "pack://application:,,,/Assets/Banners/osro_quests_mr.png";
                QuestUrl = "https://torrq.github.io/osro-quests-mr/";
            }
        });
    }

    private void OnConfigReceived(SkillSpammerConfigUpdate config)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            _suppressCommands = true;

            SpammerDelay = config.SpammerDelay;
            MouseFlick = config.MouseFlick;
            NoShift = config.NoShift;
            ToggleMode = config.ToggleMode;
            ToggleModeKey = config.ToggleModeKey;

            // Apply configuration
            var allKeys = AllKeys.ToList();
            
            foreach (var k in allKeys) k.SuppressCommands = true;

            foreach (var k in allKeys) k.IsCheckedState = false;

            foreach (var entry in config.Entries)
            {
                var vm = allKeys.FirstOrDefault(k => k.KeyName == entry.KeyName);
                if (vm != null)
                {
                    if (entry.IsIndeterminate) vm.IsCheckedState = null;
                    else if (entry.ClickActive) vm.IsCheckedState = true;
                    else vm.IsCheckedState = false;
                }
            }

            foreach (var k in allKeys) k.SuppressCommands = false;

            _suppressCommands = false;
        });
    }

    private void SendSettingsUpdate()
    {
        if (_suppressCommands) return;
        _ = _worker.SendAsync(new UpdateSkillSpammerSettingsCommand(
            SpammerDelay, MouseFlick, NoShift, ToggleMode, ToggleModeKey));
    }

    partial void OnSpammerDelayChanged(int value) => SendSettingsUpdate();
    partial void OnMouseFlickChanged(bool value) => SendSettingsUpdate();
    partial void OnNoShiftChanged(bool value) => SendSettingsUpdate();
    partial void OnToggleModeChanged(bool value) => SendSettingsUpdate();
    partial void OnToggleModeKeyChanged(string value) => SendSettingsUpdate();

    [RelayCommand]
    private void ListenForToggleKey()
    {
        // Simple listener behavior: Next key pressed becomes the toggle key
        // In a real WPF app, this would involve a dialog or capturing key input.
        // For now, since the legacy app allowed you to just type it in a TextBox, 
        // we might just bind a TextBox in the View to the ToggleModeKey property directly.
    }

    [RelayCommand]
    private void OpenQuestLink()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(QuestUrl) { UseShellExecute = true });
    }
}
