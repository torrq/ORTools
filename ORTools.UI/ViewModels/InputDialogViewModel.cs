using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ORTools.UI.ViewModels;

public sealed partial class InputDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public Task<string?> ResultTask => _tcs.Task;

    [ObservableProperty] private string _titleText;
    [ObservableProperty] private string _messageText;
    [ObservableProperty] private string _inputText;

    public InputDialogViewModel(string title, string message, string defaultText = "")
    {
        TitleText = title;
        MessageText = message;
        InputText = defaultText;
    }

    [RelayCommand]
    private void Save()
    {
        _tcs.TrySetResult(InputText);
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(null);
    }
}
