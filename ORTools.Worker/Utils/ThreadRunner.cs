namespace ORTools.Worker;

public class ThreadRunner
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _suspendEvent = new(true);
    private volatile bool _running = true;

    public int IterationDelay { get; set; } = 5;

    public ThreadRunner(Func<int, int> toRun, string name = "Unnamed")
    {
        _thread = new Thread(() =>
        {
            while (_running)
            {
                try
                {
                    _suspendEvent.Wait();
                    int result = toRun(0);
                    if (result < 0) { _running = false; break; }
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"[ThreadRunner '{_thread.Name}'] {ex.Message}");
                }
                finally
                {
                    if (IterationDelay > 0) Thread.Sleep(IterationDelay);
                }
            }
        })
        {
            Name         = name,
            IsBackground = true,
        };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public static void Start(ThreadRunner? r)
    {
        if (r == null) return;
        r._suspendEvent.Set();
        if (!r._thread.IsAlive) r._thread.Start();
    }

    public static void Stop(ThreadRunner? r)
    {
        if (r == null || !r._thread.IsAlive) return;
        try { r._suspendEvent.Reset(); }
        catch (Exception ex) { DebugLogger.Error($"[ThreadRunner] Could not suspend: {ex}"); }
    }

    public void Terminate()
    {
        _running = false;
        _suspendEvent.Set();
    }
}
