using System;
using System.Collections.Generic;
using ORTools.Shared.Protocol;

namespace ORTools.Worker.Utils;

public static class ProcessManager
{
    public static List<ProcessEntry> GetActiveProcesses()
    {
        var result = new List<ProcessEntry>();
        foreach (var server in Server.GetLocalClients())
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(server.Name);
                foreach (var p in processes)
                {
                    string id = $"{p.ProcessName}.exe - {p.Id}";
                    string displayName = $"Client [{p.Id}]"; // Prettified default
                    try
                    {
                        using var tempClient = new Client(id);
                        tempClient.RefreshLoginStatus();
                        if (tempClient.IsLoggedIn)
                        {
                            string name = tempClient.ReadCharacterName();
                            string map = tempClient.ReadCurrentMap();
                            
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                displayName = string.IsNullOrWhiteSpace(map) ? name : $"{name} @ {map}";
                            }
                            else
                            {
                                displayName = "Loading Character...";
                            }
                        }
                        else
                        {
                            displayName = "Login / Select Screen";
                        }
                    }
                    catch { }

                    result.Add(new ProcessEntry(id, displayName));
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"Failed to get processes for {server.Name}: {ex.Message}");
            }
        }
        return result;
    }
}
