using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;

namespace ORTools.UI.ViewModels;

public sealed partial class AtkDefRowViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _isUpdatingFromServer;

    [ObservableProperty] private int _id;

    private string _spammerKey = "None";
    public string SpammerKey
    {
        get => _spammerKey;
        set
        {
            if (SetProperty(ref _spammerKey, value) && !_isUpdatingFromServer)
                _worker.Send(new UpdateAtkDefTriggerCommand(Id, value));
        }
    }

    private int _spammerDelay;
    public int SpammerDelay
    {
        get => _spammerDelay;
        set
        {
            if (SetProperty(ref _spammerDelay, value) && !_isUpdatingFromServer)
                _worker.Send(new UpdateAtkDefSpammerDelayCommand(Id, value));
        }
    }

    private int _switchDelay;
    public int SwitchDelay
    {
        get => _switchDelay;
        set
        {
            if (SetProperty(ref _switchDelay, value) && !_isUpdatingFromServer)
                _worker.Send(new UpdateAtkDefSwitchDelayCommand(Id, value));
        }
    }

    private bool _withClick;
    public bool WithClick
    {
        get => _withClick;
        set
        {
            if (SetProperty(ref _withClick, value) && !_isUpdatingFromServer)
                _worker.Send(new UpdateAtkDefClickCommand(Id, value));
        }
    }

    // ── DEF Slots ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _defHead = "None";
    partial void OnDefHeadChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "DEF", "Head", value)); }

    [ObservableProperty] private string _defBody = "None";
    partial void OnDefBodyChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "DEF", "Body", value)); }

    [ObservableProperty] private string _defWeapon = "None";
    partial void OnDefWeaponChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "DEF", "Weapon", value)); }

    [ObservableProperty] private string _defShield = "None";
    partial void OnDefShieldChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "DEF", "Shield", value)); }

    [ObservableProperty] private string _defGarment = "None";
    partial void OnDefGarmentChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "DEF", "Garment", value)); }

    [ObservableProperty] private string _defShoes = "None";
    partial void OnDefShoesChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "DEF", "Shoes", value)); }

    // ── ATK Slots ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _atkHead = "None";
    partial void OnAtkHeadChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "ATK", "Head", value)); }

    [ObservableProperty] private string _atkBody = "None";
    partial void OnAtkBodyChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "ATK", "Body", value)); }

    [ObservableProperty] private string _atkWeapon = "None";
    partial void OnAtkWeaponChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "ATK", "Weapon", value)); }

    [ObservableProperty] private string _atkShield = "None";
    partial void OnAtkShieldChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "ATK", "Shield", value)); }

    [ObservableProperty] private string _atkGarment = "None";
    partial void OnAtkGarmentChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "ATK", "Garment", value)); }

    [ObservableProperty] private string _atkShoes = "None";
    partial void OnAtkShoesChanged(string value) { if (!_isUpdatingFromServer) _worker.Send(new UpdateAtkDefEquipCommand(Id, "ATK", "Shoes", value)); }


    public AtkDefRowViewModel(WorkerService worker, int id)
    {
        _worker = worker;
        Id = id;
    }

    [RelayCommand]
    private void Reset()
    {
        _worker.Send(new ResetAtkDefRowCommand(Id));
    }

    public void SyncFromServer(AtkDefRowData row)
    {
        _isUpdatingFromServer = true;

        SpammerKey = row.TriggerKey;
        SpammerDelay = row.SpammerDelay;
        SwitchDelay = row.SwitchDelay;
        WithClick = row.Click;

        DefHead    = row.DefKeys.GetValueOrDefault("Head", "None");
        DefBody    = row.DefKeys.GetValueOrDefault("Body", "None");
        DefWeapon  = row.DefKeys.GetValueOrDefault("Weapon", "None");
        DefShield  = row.DefKeys.GetValueOrDefault("Shield", "None");
        DefGarment = row.DefKeys.GetValueOrDefault("Garment", "None");
        DefShoes   = row.DefKeys.GetValueOrDefault("Shoes", "None");

        AtkHead    = row.AtkKeys.GetValueOrDefault("Head", "None");
        AtkBody    = row.AtkKeys.GetValueOrDefault("Body", "None");
        AtkWeapon  = row.AtkKeys.GetValueOrDefault("Weapon", "None");
        AtkShield  = row.AtkKeys.GetValueOrDefault("Shield", "None");
        AtkGarment = row.AtkKeys.GetValueOrDefault("Garment", "None");
        AtkShoes   = row.AtkKeys.GetValueOrDefault("Shoes", "None");

        _isUpdatingFromServer = false;
    }
}

public sealed partial class AtkDefViewModel : ViewModelBase
{
    private readonly WorkerService _worker;

    public ObservableCollection<AtkDefRowViewModel> Rows { get; } = new();

    public AtkDefViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.AtkDefConfigReceived += OnConfigUpdate;
    }

    private void OnConfigUpdate(AtkDefConfigUpdate u)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (Rows.Count != u.Rows.Count)
            {
                Rows.Clear();
                foreach (var row in u.Rows)
                {
                    Rows.Add(new AtkDefRowViewModel(_worker, row.Id));
                }
            }

            for (int i = 0; i < u.Rows.Count; i++)
            {
                Rows[i].SyncFromServer(u.Rows[i]);
            }
        });
    }
}
