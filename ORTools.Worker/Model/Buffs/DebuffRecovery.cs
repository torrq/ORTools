

using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ORTools.Worker
{
    public class DebuffRecovery : IAction
    {
        public static string ACTION_NAME_DEBUFF_RECOVERY = "DebuffsRecovery";

        private ThreadRunner thread;
        private volatile bool _running;
        public ConcurrentDictionary<EffectStatusIDs, Keys> buffMapping = new ConcurrentDictionary<EffectStatusIDs, Keys>();
        public int Delay { get; set; } = 50;

        private readonly string ActionName;

        // Add error tracking
        private int consecutiveErrors = 0;
        private const int maxConsecutiveErrors = 5;
        private DateTime lastSuccessfulRead = DateTime.Now;

        // Default constructor
        public DebuffRecovery() : this(ACTION_NAME_DEBUFF_RECOVERY)
        {
        }

        // Constructor with custom action name
        public DebuffRecovery(string actionName)
        {
            this.ActionName = actionName;
        }

        public string GetActionName()
        {
            return this.ActionName;
        }

        public ThreadRunner DebuffRecoveryThread(Client c)
        {
            return new ThreadRunner(_ =>
            {
                if (!_running) return -1;

                try
                {
                    // Check if client is still valid before proceeding
                    if (c?.Process == null || c.Process.HasExited)
                    {
                        DebugLogger.Debug("DebuffRecovery: Client process is null or has exited, stopping thread.");
                        WorkerNotifier.RequestTurnOff("DebuffRecovery");
                        return -1; // Signal thread to stop
                    }

                    // Respect Pause in Town setting
                    var prefs = ProfileSingleton.GetCurrent().UserPreferences;
                    if (prefs != null && prefs.StopBuffsCity && Server.GetCityList().Contains(c.ReadCurrentMapCached()))
                    {
                        SleepResponsive(Math.Max(100, this.Delay));
                        return 0;
                    }

                    // Do not execute if player is typing in chat or dead
                    if (c.IsTextInputActive() || c.IsDead())
                    {
                        SleepResponsive(Math.Max(50, this.Delay));
                        return 0;
                    }

                    // Check if we've had too many consecutive errors
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        var timeSinceLastSuccess = DateTime.Now - lastSuccessfulRead;
                        if (timeSinceLastSuccess.TotalSeconds > 30) // Wait 30 seconds before retrying
                        {
                            DebugLogger.Debug($"DebuffRecovery: Too many consecutive errors ({consecutiveErrors}), waiting before retry...");
                            SleepResponsive(5000);
                            consecutiveErrors = 0; // Reset error count
                            c.RefreshLoginStatus(); // Force refresh process status
                        }
                        else
                        {
                            SleepResponsive(this.Delay);
                            return 0;
                        }
                    }

                    bool hadError = false;
                    bool foundAnyStatus = false;

                    // Read entire status buffer in one RPM call instead of 100 individual reads
                    var statusBuffer = c.ReadStatusBuffer();
                    if (statusBuffer == null)
                    {
                        hadError = true;
                    }
                    else
                    {
                        var pressedKeysThisCycle = new HashSet<Keys>();

                        for (int i = 1; i < Constants.MAX_BUFF_LIST_INDEX_SIZE; i++)
                        {
                            uint currentStatus = statusBuffer[i];

                            if (!StatusUtils.IsValidStatus(currentStatus) || currentStatus == 0)
                            {
                                continue;
                            }

                            foundAnyStatus = true;
                            EffectStatusIDs status = (EffectStatusIDs)currentStatus;

                            // Check if we have a mapping for this status
                            if (buffMapping.TryGetValue(status, out Keys key) && WorkerNotifier.IsValidKey(key))
                            {
                                if (pressedKeysThisCycle.Add(key))
                                {
                                    this.UseStatusRecovery(key);
                                    DebugLogger.Debug($"DebuffRecovery: Used key {key} for status {status}");
                                }
                            }
                        }
                    }

                    // Update error tracking
                    if (hadError)
                    {
                        consecutiveErrors++;
                        DebugLogger.Debug($"DebuffRecovery: Consecutive errors: {consecutiveErrors}");
                    }
                    else
                    {
                        consecutiveErrors = 0;
                        lastSuccessfulRead = DateTime.Now;
                    }

                    // If we couldn't read any status and had an error, the process might be invalid
                    if (hadError && !foundAnyStatus)
                    {
                        DebugLogger.Debug("DebuffRecovery: Could not read any status data, process may be invalid.");
                    }
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    DebugLogger.Debug($"DebuffRecovery: Thread exception: {ex.Message}");
                }

                SleepResponsive(this.Delay);
                return 0;
            }, "DebuffRecovery");
        }

        public string GetConfiguration()
        {
            // Create a configuration object that includes both mapping and delay
            var configData = new Dictionary<string, object>
            {
                ["BuffMapping"] = this.buffMapping,
                ["Delay"] = this.Delay
            };
            return JsonConvert.SerializeObject(configData);
        }

        public void LoadConfiguration(string config)
        {
            try
            {
                // Try to deserialize as the new format with Delay
                var configData = JsonConvert.DeserializeObject<Dictionary<string, object>>(config);
                if (configData != null)
                {
                    // Load buff mapping
                    if (configData.ContainsKey("BuffMapping"))
                    {
                        var mappingData = JsonConvert.DeserializeObject<Dictionary<EffectStatusIDs, Keys>>(configData["BuffMapping"].ToString());
                        if (mappingData != null)
                        {
                            this.buffMapping = new ConcurrentDictionary<EffectStatusIDs, Keys>(mappingData);
                        }
                    }

                    // Load delay
                    if (configData.ContainsKey("Delay"))
                    {
                        if (int.TryParse(configData["Delay"].ToString(), out int delay))
                        {
                            this.Delay = delay;
                        }
                    }
                    return;
                }
            }
            catch
            {
                // Fall back to old format
            }

            try
            {
                // Try to deserialize as the old format (full DebuffRecovery object)
                var oldDebuffRecovery = JsonConvert.DeserializeObject<DebuffRecovery>(config);
                if (oldDebuffRecovery != null)
                {
                    if (oldDebuffRecovery.buffMapping != null)
                    {
                        this.buffMapping = oldDebuffRecovery.buffMapping;
                    }

                    if (oldDebuffRecovery.Delay > 0)
                    {
                        this.Delay = oldDebuffRecovery.Delay;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error loading DebuffRecovery configuration: {ex.Message}");
            }
        }

        public void Start()
        {
            Stop();
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                _running = true;
                // Reset error tracking
                consecutiveErrors = 0;
                lastSuccessfulRead = DateTime.Now;

                this.thread = DebuffRecoveryThread(roClient);
                ThreadRunner.Start(this.thread);
            }
        }

        public void AddKeyToBuff(EffectStatusIDs status, Keys key)
        {
            if (buffMapping.ContainsKey(status))
            {
                buffMapping.TryRemove(status, out var _);
            }
            if (WorkerNotifier.IsValidKey(key))
            {
                buffMapping.TryAdd(status, key);
            }
        }

        public void RemoveKeyFromBuff(EffectStatusIDs status)
        {
            if (buffMapping.ContainsKey(status))
            {
                buffMapping.TryRemove(status, out _);
                DebugLogger.Debug($"DebuffRecovery: Removed mapping for status {status}");
            }
        }

        public void ClearAllMappings()
        {
            buffMapping.Clear();
            //DebugLogger.Debug("DebuffRecovery: Cleared all status mappings");
        }

        public bool HasMappingForStatus(EffectStatusIDs status)
        {
            return buffMapping.ContainsKey(status);
        }

        public Keys GetKeyForStatus(EffectStatusIDs status)
        {
            return buffMapping.ContainsKey(status) ? buffMapping[status] : Keys.None;
        }

        public Dictionary<EffectStatusIDs, Keys> GetAllMappings()
        {
            return new Dictionary<EffectStatusIDs, Keys>(buffMapping);
        }

        public int GetMappingCount()
        {
            return buffMapping.Count;
        }

        public void Stop()
        {
            _running = false;
            if (this.thread != null)
            {
                ThreadRunner.Stop(this.thread);
                this.thread.Terminate();
                this.thread = null;
            }
        }

        private void UseStatusRecovery(Keys key)
        {
            try
            {
                var client = ClientSingleton.GetClient();
                if (client?.Process != null && !client.Process.HasExited && !client.IsTextInputActive() && !client.IsDead())
                {
                    ClientInput.SendKey(client.MainWindowHandle, key);
                    StatusBufferCache.Invalidate();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"DebuffRecovery: Error using status recovery key {key}: {ex.Message}");
            }
        }

        private void SleepResponsive(int ms)
        {
            int elapsed = 0;
            while (_running && elapsed < ms)
            {
                int step = Math.Min(25, ms - elapsed);
                Thread.Sleep(step);
                elapsed += step;
            }
        }

        // Properties for monitoring and diagnostics
        public int ConsecutiveErrorCount => consecutiveErrors;
        public DateTime LastSuccessfulRead => lastSuccessfulRead;

        // Method to manually reset error tracking
        public void ResetErrorTracking()
        {
            consecutiveErrors = 0;
            lastSuccessfulRead = DateTime.Now;
        }
    }
}
