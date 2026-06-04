using ORTools.Worker;

Console.Title = "ORTools Worker";
Console.WriteLine("[Worker] Starting...");

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Console.WriteLine($"[Worker] Unhandled: {e.ExceptionObject}");

var core = new WorkerCore();
await core.RunAsync(cts.Token);

Console.WriteLine("[Worker] Exited. Press any key to close.");
Console.ReadKey();
