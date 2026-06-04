using System.IO;
using System.Diagnostics;

namespace ORTools.UI.Services;

/// <summary>
/// Finds ORTools.Worker.exe (must be in the same directory as the UI exe)
/// and launches it with "runas" so Windows prompts for elevation once.
///
/// After a successful launch the Worker takes ~500ms to start its pipe server.
/// WorkerService handles the retry loop.
/// </summary>
public static class WorkerLauncher
{
    private const string WorkerExeName = "ORTools.Worker.exe";

    /// <summary>Returns true if the Worker process is already running.</summary>
    public static bool IsRunning()
        => Process.GetProcessesByName("ORTools.Worker").Length > 0;

    /// <summary>
    /// Launch the Worker. Returns true if the shell execute succeeded.
    /// The launch is fire-and-forget; wait for the pipe to become available
    /// rather than waiting on the Process object.
    /// </summary>
    public static bool TryLaunch()
    {
        string uiDir     = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        string workerPath = Path.Combine(uiDir, WorkerExeName);

        if (!File.Exists(workerPath))
        {
            Console.WriteLine($"[Launcher] Worker not found: {workerPath}");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo(workerPath)
            {
                UseShellExecute = true,   // required for "runas"
                Verb            = "runas",
                WindowStyle     = ProcessWindowStyle.Minimized
            };
            Process.Start(psi);
            Console.WriteLine("[Launcher] Worker launched (UAC prompt may appear).");
            return true;
        }
        catch (Exception ex)
        {
            // User cancelled UAC or launch failed
            Console.WriteLine($"[Launcher] Launch failed: {ex.Message}");
            return false;
        }
    }
}
