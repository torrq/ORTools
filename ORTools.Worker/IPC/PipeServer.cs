using System.IO.Pipes;
using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

/// <summary>
/// Named pipe server. Accepts one UI client at a time; when that client
/// disconnects the server loops and waits for a new connection.
///
/// All messages are newline-delimited JSON (IpcEnvelope wire format).
///
/// Note on security: on net8.0-windows, named pipes created by an elevated
/// process include the current user account in their DACL regardless of
/// integrity level, so a medium-integrity process running as the same user
/// can connect without an explicit world-readable ACL. If you ever see
/// connection failures in production packaging, add a PipeSecurity via
/// NamedPipeServerStreamAcl (System.IO.Pipes.AccessControl NuGet package).
/// </summary>
public sealed class PipeServer
{
    private readonly string            _pipeName;
    private readonly CommandDispatcher _dispatcher;

    private StreamWriter?          _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public PipeServer(string pipeName, CommandDispatcher dispatcher)
    {
        _pipeName   = pipeName;
        _dispatcher = dispatcher;
    }

    // ── Main loop ─────────────────────────────────────────────────────────────

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();

                Console.WriteLine("[PipeServer] Waiting for UI connection...");
                await pipe.WaitForConnectionAsync(ct);
                Console.WriteLine("[PipeServer] UI connected.");

                await HandleClientAsync(pipe, ct);

                Console.WriteLine("[PipeServer] UI disconnected — restarting listener.");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[PipeServer] Error: {ex.Message} — restarting in 1s...");
                await Task.Delay(1000, ct);
            }
        }
    }

    // ── Client session ────────────────────────────────────────────────────────

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var reader = new StreamReader(pipe, leaveOpen: true);
        _writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

        // Immediately tell the UI the worker is ready
        await SendAsync(new WorkerReadyUpdate(), ct);

        while (!ct.IsCancellationRequested && pipe.IsConnected)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            var envelope = IpcEnvelope.Parse(line);
            if (envelope is not null)
                await _dispatcher.HandleAsync(envelope, ct);
        }

        _writer = null;
    }

    // ── Outbound ──────────────────────────────────────────────────────────────

    /// <summary>Send an update to the connected UI. No-ops if no client is connected.</summary>
    public async Task BroadcastAsync<T>(T message) where T : IIpcMessage
        => await SendAsync(message, CancellationToken.None);

    private async Task SendAsync<T>(T message, CancellationToken ct) where T : IIpcMessage
    {
        if (_writer is null) return;
        await _writeLock.WaitAsync(ct);
        try
        {
            await _writer.WriteLineAsync(IpcEnvelope.Wrap(message));
        }
        catch
        {
            // Client disconnected mid-write; read loop will detect and exit
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Pipe factory ──────────────────────────────────────────────────────────

    private static NamedPipeServerStream CreatePipe() =>
        new(
            pipeName:                   "ORTools-Worker",
            direction:                  PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            transmissionMode:           PipeTransmissionMode.Byte,
            options:                    PipeOptions.Asynchronous,
            inBufferSize:               65536,
            outBufferSize:              65536);
}
