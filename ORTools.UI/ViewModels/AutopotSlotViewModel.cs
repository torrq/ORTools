using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ORTools.UI.ViewModels;

public sealed partial class AutopotSlotViewModel : ObservableObject
{
    private readonly Action<AutopotSlotViewModel> _onChanged;

    public int Id { get; }

    [ObservableProperty] private string _key;
    [ObservableProperty] private int _percent;
    [ObservableProperty] private bool _enabled;

    private bool _isUpdating;

    public AutopotSlotViewModel(int id, string key, int percent, bool enabled, Action<AutopotSlotViewModel> onChanged)
    {
        _isUpdating = true;
        Id = id;
        _key = key;
        _percent = percent;
        _enabled = enabled;
        _onChanged = onChanged;
        _isUpdating = false;
    }

    public void UpdateFromWorker(string key, int percent, bool enabled)
    {
        _isUpdating = true;
        Key = key;
        Percent = percent;
        Enabled = enabled;
        _isUpdating = false;
    }

    partial void OnKeyChanged(string value) => NotifyChange();
    partial void OnPercentChanged(int value) => NotifyChange();
    partial void OnEnabledChanged(bool value) => NotifyChange();

    private void NotifyChange()
    {
        if (!_isUpdating)
        {
            _onChanged(this);
        }
    }
}
