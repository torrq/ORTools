using ORTools.Shared.Protocol;
using ORTools.Worker.IPC;

namespace ORTools.Worker;

/// <summary>
/// Root of the Worker process.
///
/// Phase 1: owns the pipe server and a stub command dispatcher.
/// Phase 2: also owns all model instances (AutopotHP, SkillTimer, etc.)
///          and wires their events to BroadcastAsync so the UI gets live updates.
///
/// Lifetime: created once in Program.cs, lives until CancellationToken fires.
/// </summary>
public sealed class WorkerCore
{
    public const string PipeName = "ORTools-Worker";

    private readonly PipeServer        _server;
    private readonly CommandDispatcher _dispatcher;

    // ── Phase 2: add model instances here ─────────────────────────────────────
    // public AutopotHP   AutopotHP   { get; } = new AutopotHP("AutopotHP");
    // public AutopotSP   AutopotSP   { get; } = new AutopotSP("AutopotSP");
    // public SkillTimer  SkillTimer  { get; } = new SkillTimer();
    // public SkillSpammer SkillSpammer { get; } = new SkillSpammer();
    // public StatusRecovery StatusRecovery { get; } = new StatusRecovery();
    // ... etc.

    public WorkerCore()
    {
        _dispatcher = new CommandDispatcher(this);
        _server     = new PipeServer(PipeName, _dispatcher);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[WorkerCore] Named pipe: {PipeName}");
        await _server.RunAsync(ct);
    }

    /// <summary>
    /// Push a state update to the connected UI client.
    /// Safe to call from any thread; PipeServer handles the write lock.
    /// Returns immediately if no UI is connected.
    /// </summary>
    public Task BroadcastAsync<T>(T update) where T : IIpcMessage
        => _server.BroadcastAsync(update);
}
