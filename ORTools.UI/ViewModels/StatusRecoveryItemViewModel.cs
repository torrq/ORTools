using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ORTools.UI.ViewModels;

public partial class StatusRecoveryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _key = "None";

    partial void OnKeyChanged(string value)
    {
        OnKeyUpdated?.Invoke(this, value);
    }

    public event Action<StatusRecoveryItemViewModel, string>? OnKeyUpdated;
}
