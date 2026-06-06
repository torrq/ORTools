using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

/// <summary>
/// Runs two background loops that push live game state to the UI:
///   - HP/SP every ~50ms via HpSpUpdate
///   - Character name, map, job, exp, weight every ~1s via CharacterUpdate
///
/// Starts when a client connects, stops when it disconnects or the app turns off.
/// Owned by WorkerCore.
/// </summary>
public sealed class StatePublisher : IDisposable
{
    private readonly Func<Task> _broadcast;   // WorkerCore.BroadcastAsync wrapper
    private CancellationTokenSource? _cts;

    /// <param name="broadcast">
    /// Async delegate that sends one update; already wraps BroadcastAsync generically.
    /// </param>
    public StatePublisher(Func<IIpcMessage, Task> broadcast)
    {
        _broadcast = () => Task.CompletedTask;  // will be replaced per-start call
        _broadcastMsg = broadcast;
    }

    private readonly Func<IIpcMessage, Task> _broadcastMsg;

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        Task.Run(() => HpSpLoop(ct),      ct);
        Task.Run(() => CharacterLoop(ct), ct);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();

    // ── HP/SP loop — 50ms ─────────────────────────────────────────────────────

    private async Task HpSpLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = ClientSingleton.GetClient();
                if (client?.Process != null && !client.Process.HasExited && client.IsLoggedIn)
                {
                    var snap = client.ReadHpSp();
                    await _broadcastMsg(new HpSpUpdate(
                        snap.CurrentHp, snap.MaxHp,
                        snap.CurrentSp, snap.MaxSp));
                }
            }
            catch (Exception ex) { DebugLogger.Debug($"[StatePublisher.HpSp] {ex.Message}"); }

            await Task.Delay(50, ct).ConfigureAwait(false);
        }
    }

    // ── Character loop — 1000ms ───────────────────────────────────────────────

    private async Task CharacterLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = ClientSingleton.GetClient();
                if (client?.Process != null && !client.Process.HasExited && client.IsLoggedIn)
                {
                    string name = client.ReadCharacterName() ?? "";
                    string map  = client.ReadCurrentMap()    ?? "";
                    var    job  = client.ReadJobBlock();
                    var (wCur, wMax) = client.ReadWeight();

                    if (job.HasValue)
                    {
                        var j      = job.Value;
                        string exp = j.ExpToLevel > 0
                            ? $"{(double)j.Exp / j.ExpToLevel * 100:0.00}%"
                            : "100%";

                        await _broadcastMsg(new CharacterUpdate(
                            Name: name, Map: map,
                            Level: j.Level, JobLevel: j.JobLevel,
                            JobId: j.JobId,
                            Exp: j.Exp, ExpToLevel: j.ExpToLevel,
                            WeightCur: wCur, WeightMax: wMax));
                    }
                }
            }
            catch (Exception ex) { DebugLogger.Debug($"[StatePublisher.Character] {ex.Message}"); }

            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
    }
}
