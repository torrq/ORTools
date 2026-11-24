using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ORTools.UI.ViewModels;

public partial class StatusRecoveryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _displayName = string.Empty;

    public string ImagePath => $"pack://application:,,,/Assets/Icons/Debuffs/{Name}.png";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(EffectiveIsEnabled))]
    private string _key = "None";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(EffectiveIsEnabled))]
    private bool _isEnabled = true;

    public bool IsActive => Key != "None" && IsEnabled;
    public bool EffectiveIsEnabled => Key == "None" || IsEnabled;

    partial void OnKeyChanged(string value)
    {
        OnKeyUpdated?.Invoke(this, value);
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        if (Key == "None") return;
        IsEnabled = !IsEnabled;
        OnEnabledToggled?.Invoke(this, IsEnabled);
    }

    public event Action<StatusRecoveryItemViewModel, string>? OnKeyUpdated;
    public event Action<StatusRecoveryItemViewModel, bool>? OnEnabledToggled;
}
