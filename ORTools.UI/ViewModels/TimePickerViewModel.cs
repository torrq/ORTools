using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ORTools.UI.ViewModels;

public sealed partial class TimePickerViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<int?> _tcs = new();

    public Task<int?> ResultTask => _tcs.Task;

    [ObservableProperty] private int _hours;
    [ObservableProperty] private int _minutes;
    [ObservableProperty] private int _seconds;
    [ObservableProperty] private int _milliseconds;

    private readonly int _defaultMilliseconds;

    public TimePickerViewModel(int totalMilliseconds, int defaultMilliseconds = 1000)
    {
        _defaultMilliseconds = defaultMilliseconds;
        SetTime(totalMilliseconds);
    }

    private void SetTime(int totalMilliseconds)
    {
        Hours = totalMilliseconds / 3600000;
        totalMilliseconds %= 3600000;
        
        Minutes = totalMilliseconds / 60000;
        totalMilliseconds %= 60000;
        
        Seconds = totalMilliseconds / 1000;
        totalMilliseconds %= 1000;
        
        Milliseconds = totalMilliseconds;
    }

    [RelayCommand]
    private void Save()
    {
        // Clamp bounds to prevent overflow or negative values
        int h = System.Math.Clamp(Hours, 0, 100);
        int m = System.Math.Clamp(Minutes, 0, 999);
        int s = System.Math.Clamp(Seconds, 0, 999);
        int ms = System.Math.Clamp(Milliseconds, 0, 9999);

        // Allow flexible inputs, e.g. 15 minutes can be entered entirely in the Minutes box
        int totalMs = (h * 3600000) + (m * 60000) + (s * 1000) + ms;
        
        // Return totalMs
        _tcs.TrySetResult(totalMs);
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(null);
    }

    [RelayCommand]
    private void Reset()
    {
        SetTime(_defaultMilliseconds);
    }

    [RelayCommand]
    private void ResetToZero()
    {
        SetTime(0);
    }
}
