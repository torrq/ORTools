using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ORTools.UI.ViewModels;

public partial class ProfileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHotkey))]
    private string _hotkey = "None";

    public bool HasHotkey => !string.IsNullOrWhiteSpace(Hotkey) && !Hotkey.Equals("None", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<string> LinkedCharacters { get; } = new();

    public bool HasLinkedCharacters => LinkedCharacters.Count > 0;
    public string LinkedCharactersSummary => string.Join(", ", LinkedCharacters);

    public ProfileItemViewModel(string name, bool isCurrent, string hotkey, IEnumerable<string>? linkedChars)
    {
        _name = name;
        _isCurrent = isCurrent;
        _hotkey = string.IsNullOrWhiteSpace(hotkey) ? "None" : hotkey;
        if (linkedChars != null)
        {
            foreach (var c in linkedChars.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                LinkedCharacters.Add(c);
        }
    }
}

public partial class ProfilesViewModel : ObservableObject
{
    private readonly WorkerService _worker;
    private readonly IDialogService _dialogService;
    private bool _suppressUpdates;

    [ObservableProperty]
    private ObservableCollection<ProfileItemViewModel> _profileList = new();

    [ObservableProperty]
    private string _currentProfile = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedProfile))]
    private ProfileItemViewModel? _selectedProfile;

    public bool HasSelectedProfile => SelectedProfile != null;

    [ObservableProperty]
    private bool _autoSwitchProfileByChar = true;

    [ObservableProperty]
    private string _activeCharacterName = "";

    public ProfilesViewModel(WorkerService worker, IDialogService dialogService)
    {
        _worker = worker;
        _dialogService = dialogService;
        _worker.ProfileListReceived += OnProfileList;
        _worker.CharacterReceived += OnCharacterReceived;
    }

    private void OnCharacterReceived(CharacterUpdate u)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(u.Name))
                ActiveCharacterName = u.Name;
        });
    }

    private void OnProfileList(ProfileListUpdate u)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _suppressUpdates = true;
            string? previouslySelected = SelectedProfile?.Name;
            CurrentProfile = u.CurrentProfile;
            AutoSwitchProfileByChar = u.AutoSwitchProfileByChar;

            var newList = new ObservableCollection<ProfileItemViewModel>();
            foreach (var p in u.Profiles)
            {
                string hk = "None";
                if (u.ProfileHotkeys != null)
                {
                    var match = u.ProfileHotkeys.FirstOrDefault(k => string.Equals(k.Key, p, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(match.Value)) hk = match.Value;
                }

                List<string>? chars = null;
                if (u.LinkedCharacters != null)
                {
                    var match = u.LinkedCharacters.FirstOrDefault(k => string.Equals(k.Key, p, StringComparison.OrdinalIgnoreCase));
                    chars = match.Value;
                }

                newList.Add(new ProfileItemViewModel(p, p == u.CurrentProfile, hk, chars));
            }
            ProfileList = newList;

            SelectedProfile = ProfileList.FirstOrDefault(x => x.Name == previouslySelected)
                           ?? ProfileList.FirstOrDefault(x => x.IsCurrent)
                           ?? ProfileList.FirstOrDefault();

            _suppressUpdates = false;
        });
    }

    partial void OnAutoSwitchProfileByCharChanged(bool value)
    {
        if (!_suppressUpdates)
            _worker.Send(new SetAutoSwitchProfileByCharCommand(value));
    }

    public void UpdateSelectedProfileHotkey(string hotkey)
    {
        if (SelectedProfile == null) return;
        string normalized = string.IsNullOrWhiteSpace(hotkey) ? "None" : hotkey;
        SelectedProfile.Hotkey = normalized;
        _worker.Send(new SetProfileHotkeyCommand(SelectedProfile.Name, normalized));
    }

    public void ImportProfilesFromPaths(IEnumerable<string> filePaths)
    {
        var list = filePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (list == null || list.Count == 0) return;
        _worker.Send(new ImportProfilesCommand(list));
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LinkCharacter()
    {
        if (SelectedProfile == null) return;

        var vm = new InputDialogViewModel(
            LanguageService.Get("S.Profiles.LinkCharTitle"),
            LanguageService.Get("S.Profiles.LinkCharPrompt"),
            ActiveCharacterName);
        await _dialogService.ShowDialogAsync(vm);

        var result = await vm.ResultTask;
        _dialogService.CloseDialog();

        if (!string.IsNullOrWhiteSpace(result))
        {
            _worker.Send(new LinkCharacterToProfileCommand(result.Trim(), SelectedProfile.Name));
        }
    }

    [RelayCommand]
    private void UnlinkCharacter(string charName)
    {
        if (string.IsNullOrWhiteSpace(charName)) return;
        _worker.Send(new UnlinkCharacterCommand(charName));
    }

    [RelayCommand]
    private void ImportProfile()
    {
        var dlg = new OpenFileDialog
        {
            Title = LanguageService.Get("S.Profiles.Import"),
            Filter = "OSRO Tools Profile (*.json)|*.json|All Files (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
        {
            _worker.Send(new ImportProfilesCommand(dlg.FileNames.ToList()));
        }
    }

    [RelayCommand]
    private void ExportProfile()
    {
        if (SelectedProfile == null) return;
        var dlg = new SaveFileDialog
        {
            Title = LanguageService.Get("S.Profiles.Export"),
            Filter = "OSRO Tools Profile (*.json)|*.json",
            FileName = $"{SelectedProfile.Name}.json"
        };
        if (dlg.ShowDialog() == true)
        {
            _worker.Send(new ExportProfileCommand(SelectedProfile.Name, dlg.FileName));
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveProfile()
    {
        var vm = new InputDialogViewModel(
            LanguageService.Get("S.Profiles.CreateTitle"),
            LanguageService.Get("S.Profiles.CreatePrompt"),
            "");
        await _dialogService.ShowDialogAsync(vm);

        var result = await vm.ResultTask;
        _dialogService.CloseDialog();

        if (result != null && !string.IsNullOrWhiteSpace(result))
        {
            _worker.Send(new CreateProfileCommand(result));
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyProfile()
    {
        if (SelectedProfile == null) return;

        var vm = new InputDialogViewModel(
            LanguageService.Get("S.Profiles.CopyTitle"),
            LanguageService.Get("S.Profiles.CopyPrompt"),
            $"{SelectedProfile.Name} (1)");
        await _dialogService.ShowDialogAsync(vm);

        var result = await vm.ResultTask;
        _dialogService.CloseDialog();

        if (result != null && !string.IsNullOrWhiteSpace(result))
        {
            _worker.Send(new CopyProfileCommand(SelectedProfile.Name, result));
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RenameProfile()
    {
        if (SelectedProfile == null) return;
        if (SelectedProfile.Name == "Default")
        {
            var alert = new AlertDialogViewModel(
                LanguageService.Get("S.Profiles.ErrorTitle"),
                LanguageService.Get("S.Profiles.ErrorRenameDefault"));
            await _dialogService.ShowDialogAsync(alert);
            await alert.ResultTask;
            _dialogService.CloseDialog();
            return;
        }

        var vm = new InputDialogViewModel(
            LanguageService.Get("S.Profiles.RenameTitle"),
            LanguageService.Get("S.Profiles.RenamePrompt"),
            SelectedProfile.Name);
        await _dialogService.ShowDialogAsync(vm);

        var result = await vm.ResultTask;
        _dialogService.CloseDialog();

        if (result != null && !string.IsNullOrWhiteSpace(result))
        {
            _worker.Send(new RenameProfileCommand(SelectedProfile.Name, result));
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RemoveProfile()
    {
        if (SelectedProfile == null) return;
        if (SelectedProfile.Name == "Default")
        {
            var alert = new AlertDialogViewModel(
                LanguageService.Get("S.Profiles.ErrorTitle"),
                LanguageService.Get("S.Profiles.ErrorDeleteDefault"));
            await _dialogService.ShowDialogAsync(alert);
            await alert.ResultTask;
            _dialogService.CloseDialog();
            return;
        }

        var confirm = new ConfirmDeleteDialogViewModel(
            string.Format(LanguageService.Get("S.Profiles.ConfirmDeleteMessage"), SelectedProfile.Name));
        await _dialogService.ShowDialogAsync(confirm);

        bool confirmed = await confirm.ResultTask;
        _dialogService.CloseDialog();

        if (confirmed)
        {
            _worker.Send(new DeleteProfileCommand(SelectedProfile.Name));
        }
    }
}
