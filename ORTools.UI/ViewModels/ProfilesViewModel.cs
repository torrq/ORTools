using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace ORTools.UI.ViewModels;

public partial class ProfileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isCurrent;

    public ProfileItemViewModel(string name, bool isCurrent)
    {
        Name = name;
        IsCurrent = isCurrent;
    }
}

public partial class ProfilesViewModel : ObservableObject
{
    private readonly WorkerService _worker;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<ProfileItemViewModel> _profileList = new();

    [ObservableProperty]
    private string _currentProfile = "Default";

    [ObservableProperty]
    private ProfileItemViewModel? _selectedProfile;

    public ProfilesViewModel(WorkerService worker, IDialogService dialogService)
    {
        _worker = worker;
        _dialogService = dialogService;
        _worker.ProfileListReceived += OnProfileList;
    }

    private void OnProfileList(ProfileListUpdate u)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            CurrentProfile = u.CurrentProfile;
            
            var newList = new ObservableCollection<ProfileItemViewModel>();
            foreach (var p in u.Profiles)
            {
                newList.Add(new ProfileItemViewModel(p, p == u.CurrentProfile));
            }
            ProfileList = newList;
        });
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

        string format = LanguageService.Get("S.Profiles.ConfirmDeleteMessage");
        var vm = new ConfirmDeleteDialogViewModel(string.Format(format, SelectedProfile.Name));
        await _dialogService.ShowDialogAsync(vm);
        
        var result = await vm.ResultTask;
        _dialogService.CloseDialog();

        if (result)
        {
            _worker.Send(new DeleteProfileCommand(SelectedProfile.Name));
        }
    }
}
