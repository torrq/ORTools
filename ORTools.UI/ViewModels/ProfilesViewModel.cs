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

    [ObservableProperty]
    private ObservableCollection<ProfileItemViewModel> _profileList = new();

    [ObservableProperty]
    private string _currentProfile = "Default";

    [ObservableProperty]
    private ProfileItemViewModel? _selectedProfile;

    public ProfilesViewModel(WorkerService worker)
    {
        _worker = worker;
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
    private void SaveProfile()
    {
        var dialog = new Views.Dialogs.InputDialog("Enter new profile name:", "Create Profile", "")
        {
            Owner = Application.Current?.MainWindow
        };
        
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            _worker.Send(new CreateProfileCommand(dialog.InputText));
        }
    }

    [RelayCommand]
    private void CopyProfile()
    {
        if (SelectedProfile == null) return;
        
        var dialog = new Views.Dialogs.InputDialog("Enter name for the copied profile:", "Copy Profile", $"{SelectedProfile.Name} (1)")
        {
            Owner = Application.Current?.MainWindow
        };
        
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            _worker.Send(new CopyProfileCommand(SelectedProfile.Name, dialog.InputText));
        }
    }

    [RelayCommand]
    private void RenameProfile()
    {
        if (SelectedProfile == null) return;
        if (SelectedProfile.Name == "Default")
        {
            MessageBox.Show("Cannot rename the Default profile!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Views.Dialogs.InputDialog("Enter new profile name:", "Rename Profile", SelectedProfile.Name)
        {
            Owner = Application.Current?.MainWindow
        };
        
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            _worker.Send(new RenameProfileCommand(SelectedProfile.Name, dialog.InputText));
        }
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile == null) return;
        if (SelectedProfile.Name == "Default")
        {
            MessageBox.Show("Cannot delete the Default profile!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Views.Dialogs.ConfirmDeleteDialog($"Are you sure you want to delete the profile '{SelectedProfile.Name}'?")
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            _worker.Send(new DeleteProfileCommand(SelectedProfile.Name));
        }
    }
}
