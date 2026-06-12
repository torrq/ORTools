namespace ORTools.Worker;

/// <summary>
/// Replaces FormHelper's UI-specific methods for the Worker context.
///
/// FormHelper.ToggleStateOff  → WorkerNotifier.RequestTurnOff
/// FormHelper.IsValidKey      → WorkerNotifier.IsValidKey
/// FormHelper.StateSwitchFormInstance.TurnOFF() → WorkerNotifier.RequestTurnOff
///
/// WorkerCore subscribes to TurnOffRequested and handles the shutdown
/// of all model threads plus broadcasting AppStateUpdate(false) to the UI.
/// </summary>
public static class WorkerNotifier
{
    /// <summary>
    /// Fired by model threads when they detect the app should stop.
    /// (e.g. client process died, overweight limit hit, AutoOff triggered.)
    /// </summary>
    public static event Action<string>? TurnOffRequested;

    public static void RequestTurnOff(string reason)
    {
        DebugLogger.Info($"[WorkerNotifier] TurnOff requested by: {reason}");
        TurnOffRequested?.Invoke(reason);
    }

    public static bool IsValidKey(Keys key) => key != Keys.None;
    public static bool IsValidKey(string key) => Enum.TryParse<Keys>(key, out var parsed) && parsed != Keys.None;
}
