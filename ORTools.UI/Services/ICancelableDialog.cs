using CommunityToolkit.Mvvm.Input;

namespace ORTools.UI.Services;

/// <summary>
/// Implemented by dialog ViewModels that provide a cancel or dismiss command.
/// Allows MainWindowViewModel to dismiss modal overlays without reflection.
/// </summary>
public interface ICancelableDialog
{
    IRelayCommand? CancelCommand { get; }
}
