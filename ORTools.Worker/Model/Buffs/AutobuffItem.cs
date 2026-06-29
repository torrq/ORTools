using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using ORTools.Worker.IPC;

namespace ORTools.Worker
{
    public class AutoBuffItem : IAction
    {
        public static string ACTION_NAME_AUTOBUFFITEM = "AutobuffItem";
        public string ActionName { get; set; }
        private ThreadRunner thread;
        private int _delay = AppConfig.AutoBuffItemsDefaultDelay;

        public int Delay
        {
            get => _delay < 0 ? AppConfig.AutoBuffItemsDefaultDelay : _delay;
            set => _delay = value;
        }

        // Keys per status — List<Keys> allows multiple items sharing the same status ID (e.g. Water Converter + Box of Thunder)
        public ConcurrentDictionary<EffectStatusIDs, List<Keys>> buffMapping = new ConcurrentDictionary<EffectStatusIDs, List<Keys>>();


        // Add error tracking
        private int consecutiveErrors = 0;
        private const int maxConsecutiveErrors = 5;
        private DateTime lastSuccessfulRead = DateTime.Now;

        // Map cache
        private string _cachedMap = string.Empty;
        public AutoBuffItem(string actionName)
        {
            this.ActionName = actionName;
        }

        public void Start()
        {
            Stop();
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                // Reset error tracking
                consecutiveErrors = 0;
                lastSuccessfulRead = DateTime.Now;

                this.thread = AutoBuffThread(roClient);
                ThreadRunner.Start(this.thread);
            }
        }

        public ThreadRunner AutoBuffThread(Client c)
        {
            ThreadRunner autobuffItemThread = new ThreadRunner(_ =>
            {
                try
                {
                    // Check if client is still valid before proceeding
                    if (c?.Process == null || c.Process.HasExited)
                    {
                        DebugLogger.Debug("AutoBuffItem: Client process is null or has exited, stopping thread.");
                        WorkerNotifier.RequestTurnOff("AutobuffItem");
                        return -1; // Signal thread to stop
                    }

                    // Check if we've had too many consecutive errors
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        var timeSinceLastSuccess = DateTime.Now - lastSuccessfulRead;
                        if (timeSinceLastSuccess.TotalSeconds > 10) // Wait 10 seconds before retrying
                        {
                            DebugLogger.Debug($"AutoBuffItem: Too many consecutive errors ({consecutiveErrors}), waiting before retry...");
                            Thread.Sleep(5000); // Wait 5 seconds
                            consecutiveErrors = 0; // Reset error count
                            c.RefreshLoginStatus(); // Force refresh process status
                        }
                        else
                        {
                            Thread.Sleep(300);
                            return 0;
                        }
                    }

                    bool hadError = false;
                    bool foundQuag = false;
                    bool foundDecreaseAgi = false;
                    string currentMap = string.Empty;
                    ConfigProfile prefs = null;

                    try
                    {
                        currentMap = c.ReadCurrentMapCached();
                        prefs = ProfileSingleton.GetCurrent().UserPreferences;
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Debug($"AutoBuffItem: Error reading map or preferences: {ex.Message}");
                        hadError = true;
                    }

                    if (!hadError && prefs != null)
                    {
                        // Read entire status buffer in one RPM call instead of 99 individual reads
                        var statusBuffer = c.ReadStatusBuffer();
                        var statusList = new List<(int index, uint statusId)>();
                        bool statusReadError = statusBuffer == null;

                        if (!statusReadError)
                        {
                            for (int i = 1; i < Constants.MAX_BUFF_LIST_INDEX_SIZE; i++)
                            {
                                uint currentStatus = statusBuffer[i];
                                if (StatusUtils.IsValidStatus(currentStatus))
                                    statusList.Add((i, currentStatus));
                            }
                        }

                        if (!statusReadError)
                        {
                            try
                            {
                                StatusEffectLogger.LogAllStatuses(statusList);
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Debug($"AutoBuffItem: Error logging statuses: {ex.Message}");
                            }

                            if (!prefs.StopBuffsCity || !Server.GetCityList().Contains(currentMap))
                            {
                                // Process buffs
                                List<EffectStatusIDs> buffs = new List<EffectStatusIDs>();
                                ConcurrentDictionary<EffectStatusIDs, List<Keys>> bmClone = new ConcurrentDictionary<EffectStatusIDs, List<Keys>>(this.buffMapping);

                                foreach (var (i, currentStatus) in statusList)
                                {
                                    if (!StatusUtils.IsValidStatus(currentStatus)) { continue; }

                                    buffs.Add((EffectStatusIDs)currentStatus);
                                    EffectStatusIDs status = (EffectStatusIDs)currentStatus;

                                    if (status == EffectStatusIDs.WS_OVERTHRUSTMAX)
                                    {
                                        if (buffMapping.ContainsKey(EffectStatusIDs.BS_OVERTHRUST))
                                        {
                                            bmClone.TryRemove(EffectStatusIDs.BS_OVERTHRUST, out var _);
                                        }
                                    }

                                    if (buffMapping.ContainsKey(status))
                                    {
                                        bmClone.TryRemove(status, out var _);
                                    }

                                    if (status == EffectStatusIDs.WZ_QUAGMIRE) foundQuag = true;
                                    if (status == EffectStatusIDs.AL_DECAGI) foundDecreaseAgi = true;
                                }

                                buffs.Clear();

                                // Read HP once before applying buffs — reused for all buff checks below
                                uint currentHp = c.ReadCurrentHp();

                                // Apply buffs with error handling
                                foreach (var item in bmClone)
                                {
                                    try
                                    {
                                        if (foundQuag && (item.Key == EffectStatusIDs.AC_CONCENTRATION || item.Key == EffectStatusIDs.AL_INCAGI || item.Key == EffectStatusIDs.SN_SIGHT || item.Key == EffectStatusIDs.BS_ADRENALINE || item.Key == EffectStatusIDs.CR_SPEARQUICKEN || item.Key == EffectStatusIDs.KN_ONEHAND || item.Key == EffectStatusIDs.SN_WINDWALK))
                                        {
                                            continue;
                                        }
                                        else if (foundDecreaseAgi && (item.Key == EffectStatusIDs.KN_TWOHANDQUICKEN || item.Key == EffectStatusIDs.BS_ADRENALINE || item.Key == EffectStatusIDs.BS_ADRENALINE2 || item.Key == EffectStatusIDs.KN_ONEHAND || item.Key == EffectStatusIDs.CR_SPEARQUICKEN))
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            if (currentHp >= Constants.MINIMUM_HP_TO_RECOVER && !c.IsTextInputActive() && !c.IsDead())
                                            {
                                                // Send first available key (list allows multiple items sharing same status)
                                                var keys = item.Value;
                                                if (keys != null && keys.Count > 0)
                                                    this.UseAutobuff(keys[0]);
                                                StatusBufferCache.Invalidate(); // force fresh status read next cycle
                                                Thread.Sleep(Delay);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugLogger.Debug($"AutoBuffItem: Error applying buff {item.Key}: {ex.Message}");
                                        hadError = true;
                                        break;
                                    }
                                }


                            }
                        }
                        else
                        {
                            hadError = true;
                        }
                    }

                    // Update error tracking
                    if (hadError)
                    {
                        consecutiveErrors++;
                        DebugLogger.Debug($"AutoBuffItem: Consecutive errors: {consecutiveErrors}");
                    }
                    else
                    {
                        consecutiveErrors = 0;
                        lastSuccessfulRead = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    DebugLogger.Debug($"AutoBuffItem: Thread exception: {ex.Message}");
                }

                Thread.Sleep(this.Delay);
                return 0;
            }, "AutobuffItem");

            return autobuffItemThread;
        }

        public void AddKeyToBuff(EffectStatusIDs status, Keys key)
        {
            // Invalid/None key means the user cleared the slot — remove the mapping
            if (!WorkerNotifier.IsValidKey(key))
            {
                buffMapping.TryRemove(status, out _);
                return;
            }
            if (!buffMapping.ContainsKey(status))
                buffMapping[status] = new List<Keys>();
            if (!buffMapping[status].Contains(key))
                buffMapping[status].Add(key);
        }

        public void ReplaceKeyForBuff(EffectStatusIDs status, Keys oldKey, Keys newKey)
        {
            if (buffMapping.ContainsKey(status))
            {
                buffMapping[status].Remove(oldKey);
                if (buffMapping[status].Count == 0)
                    buffMapping.TryRemove(status, out _);
            }
            if (WorkerNotifier.IsValidKey(newKey))
                AddKeyToBuff(status, newKey);
        }

        public void RemoveKeyFromBuff(EffectStatusIDs status)
        {
            if (buffMapping.ContainsKey(status))
            {
                buffMapping.TryRemove(status, out _);
                DebugLogger.Debug($"AutoBuffItem: Removed mapping for status {status}");
            }
        }

        public void ClearKeyMapping()
        {
            buffMapping.Clear();
            //DebugLogger.Debug("AutoBuffItem: Cleared all key mappings");
        }

        public bool HasMappingForStatus(EffectStatusIDs status)
        {
            return buffMapping.ContainsKey(status);
        }

        public Keys GetKeyForStatus(EffectStatusIDs status)
        {
            if (!buffMapping.ContainsKey(status) || buffMapping[status].Count == 0) return Keys.None;
            return buffMapping[status][0];
        }

        public List<Keys> GetKeysForStatus(EffectStatusIDs status)
        {
            return buffMapping.ContainsKey(status) ? new List<Keys>(buffMapping[status]) : new List<Keys>();
        }

        public Dictionary<EffectStatusIDs, Keys> GetAllMappings()
        {
            var result = new Dictionary<EffectStatusIDs, Keys>();
            foreach (var kvp in buffMapping)
                if (kvp.Value.Count > 0) result[kvp.Key] = kvp.Value[0];
            return result;
        }

        public int GetMappingCount()
        {
            return buffMapping.Count;
        }

        public void Stop()
        {
            if (this.thread != null)
            {
                ThreadRunner.Stop(this.thread);
                this.thread.Terminate();
                this.thread = null;
            }
        }

        public string GetConfiguration()
        {
            // Create a configuration object that includes both mapping and delay
            var configData = new Dictionary<string, object>
            {
                ["ActionName"] = this.ActionName,
                ["BuffMapping"] = GetAllMappings(), // serialize as flat dict for profile compatibility
                ["Delay"] = this._delay
            };
            return JsonConvert.SerializeObject(configData);
        }

        public void LoadConfiguration(string config)
        {
            try
            {
                var jObj = JsonConvert.DeserializeObject<JObject>(config);
                if (jObj != null)
                {
                    // Load action name
                    var actionNameToken = jObj.GetValue("ActionName", StringComparison.OrdinalIgnoreCase);
                    if (actionNameToken != null)
                    {
                        this.ActionName = actionNameToken.ToString();
                    }

                    // Load buff mapping (handle both cases)
                    var mappingToken = jObj.GetValue("BuffMapping", StringComparison.OrdinalIgnoreCase);
                    if (mappingToken != null)
                    {
                        var mappingData = mappingToken.ToObject<Dictionary<EffectStatusIDs, Keys>>();
                        if (mappingData != null)
                        {
                            this.buffMapping = new ConcurrentDictionary<EffectStatusIDs, List<Keys>>();
                            foreach (var kvp in mappingData)
                            {
                                this.buffMapping.TryAdd(kvp.Key, new List<Keys> { kvp.Value });
                            }
                        }
                    }

                    // Load delay
                    var delayToken = jObj.GetValue("Delay", StringComparison.OrdinalIgnoreCase);
                    if (delayToken != null)
                    {
                        if (int.TryParse(delayToken.ToString(), out int delay))
                        {
                            if (delay == 50) delay = AppConfig.AutoBuffItemsDefaultDelay; // Migrate old 50ms default
                            this._delay = delay;
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
                // Try to deserialize as the old format (full AutoBuffItem object)
                var oldAutoBuffItem = JsonConvert.DeserializeObject<AutoBuffItem>(config);
                if (oldAutoBuffItem != null)
                {
                    if (!string.IsNullOrEmpty(oldAutoBuffItem.ActionName))
                    {
                        this.ActionName = oldAutoBuffItem.ActionName;
                    }

                    if (oldAutoBuffItem.buffMapping != null)
                    {
                        this.buffMapping = oldAutoBuffItem.buffMapping;
                    }

                    this._delay = oldAutoBuffItem._delay;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error loading AutoBuffItem configuration: {ex.Message}");
            }
        }

        public string GetActionName()
        {
            return this.ActionName;
        }

        private void UseAutobuff(Keys key)
        {
            try
            {
                ClientInput.SendKey(key);
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"AutoBuffItem: Error using autobuff key {key}: {ex.Message}");
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
