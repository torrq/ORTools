using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Services;

public interface IDialogService
{
    /// <summary>
    /// Displays a modal dialog with the given view model and awaits its completion.
    /// The view model should implement logic to signal completion, often by 
    /// completing a TaskCompletionSource and returning a result.
    /// </summary>
    Task ShowDialogAsync(ViewModelBase dialogViewModel);

    /// <summary>
    /// Closes the currently open dialog, if any.
    /// </summary>
    void CloseDialog();
}
