
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;

namespace ORTools.Worker
{
    /// <summary>
    /// Represents a song row configuration with trigger, sequence, adaptation, and instrument
    /// </summary>
    public class SongRow
    {
        public int Id { get; set; }
        public Keys TriggerKey { get; set; } = Keys.None;
        public Keys AdaptationKey { get; set; } = Keys.None;
        public Keys InstrumentKey { get; set; } = Keys.None;
        private int _delay = AppConfig.MacroDefaultDelay;
        public int Delay
        {
            get => _delay < 0 ? AppConfig.MacroDefaultDelay : _delay;
            set => _delay = value;
        }

        /// <summary>
        /// Array of 8 song keys in sequence
        /// </summary>
        public Keys[] SongSequence { get; set; }

        public SongRow()
        {
            InitializeSequence();
        }

        public SongRow(int id) : this()
        {
            this.Id = id;
        }

        [JsonConstructor]
        public SongRow(int id, Keys triggerKey, Keys adaptationKey, Keys instrumentKey, int delay, Keys[] songSequence)
        {
            this.Id = id;
            this.TriggerKey = triggerKey;
            this.AdaptationKey = adaptationKey;
            this.InstrumentKey = instrumentKey;
            this.Delay = delay;
            this.SongSequence = songSequence;

            // Ensure sequence is properly initialized
            if (this.SongSequence == null || this.SongSequence.Length != 8)
            {
                InitializeSequence();
            }
        }

        private void InitializeSequence()
        {
            SongSequence = new Keys[8];
            for (int i = 0; i < 8; i++)
            {
                SongSequence[i] = Keys.None;
            }
        }

        /// <summary>
        /// Gets the active (non-None) song keys in sequence order
        /// </summary>
        public List<Keys> GetActiveSongKeys()
        {
            List<Keys> activeKeys = new List<Keys>();
            foreach (var key in SongSequence)
            {
                if (key != Keys.None)
                {
                    activeKeys.Add(key);
                }
            }
            return activeKeys;
        }

        /// <summary>
        /// Resets this row to default values
        /// </summary>
        public void Reset()
        {
            TriggerKey = Keys.None;
            AdaptationKey = Keys.None;
            InstrumentKey = Keys.None;
            Delay = AppConfig.MacroDefaultDelay;
            for (int i = 0; i < 8; i++)
            {
                SongSequence[i] = Keys.None;
            }
        }
    }

    /// <summary>
    /// Dedicated Song Macro class for bard/dancer songs
    /// </summary>
    public class MacroSong : IAction
    {
        public static string ACTION_NAME = "SongMacro";

        public string ActionName { get; set; } = ACTION_NAME;
        private ThreadRunner thread;
        public List<SongRow> SongRows { get; set; } = new List<SongRow>();
        private readonly Dictionary<int, bool> _wasTriggerPressed = new();
        private volatile bool _running = false;

        public MacroSong()
        {
            InitializeSongRows();
        }

        [JsonConstructor]
        public MacroSong(string actionName, List<SongRow> songRows)
        {
            this.ActionName = actionName ?? ACTION_NAME;
            this.SongRows = songRows ?? new List<SongRow>();

            // Ensure we have the correct number of rows based on current config
            EnsureCorrectRowCount(ConfigGlobal.GetConfig().SongRows);
        }

        [JsonIgnore]
        private bool isInitialized = false;

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (!isInitialized)
            {
                EnsureCorrectRowCount(ConfigGlobal.GetConfig().SongRows);
                isInitialized = true;
            }
        }

        private void InitializeSongRows()
        {
            // Initialize rows based on config
            int totalRows = ConfigGlobal.GetConfig().SongRows;
            for (int i = 1; i <= totalRows; i++)
            {
                SongRows.Add(new SongRow(i));
            }
        }

        public void EnsureCorrectRowCount(int count)
        {
            // Add missing rows if config has more rows than saved data
            while (SongRows.Count < count)
            {
                SongRows.Add(new SongRow(SongRows.Count + 1));
            }
        }

        public string GetActionName()
        {
            return this.ActionName;
        }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Resets a specific song row to default values
        /// </summary>
        public void ResetSongRow(int rowId)
        {
            try
            {
                var songRow = SongRows.Find(row => row.Id == rowId);
                songRow?.Reset();
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Exception in SongMacro.ResetSongRow: {ex}");
            }
        }

        /// <summary>
        /// Gets a song row by ID
        /// </summary>
        public SongRow GetSongRow(int rowId)
        {
            return SongRows.Find(row => row.Id == rowId);
        }

        private int SongMacroThread(Client roClient)
        {
            if (!_running) return 0;
            if (!roClient.IsProcessRunning() || roClient.IsDead()) return 0;

            IntPtr hWnd = roClient.MainWindowHandle;
            if (hWnd == IntPtr.Zero || !ClientInput.IsForeground(hWnd))
            {
                _wasTriggerPressed.Clear();
                return 0;
            }

            if (roClient.IsTextInputActive())
            {
                _wasTriggerPressed.Clear();
                return 0;
            }

            int maxRows = ConfigGlobal.GetConfig().SongRows;
            for (int i = 0; i < maxRows && i < this.SongRows.Count; i++)
            {
                var songRow = this.SongRows[i];
                if (songRow.TriggerKey == Keys.None) continue;

                bool isPressed = ClientInput.IsKeyPressed(songRow.TriggerKey);
                _wasTriggerPressed.TryGetValue(songRow.Id, out bool wasPressed);

                if (isPressed && !wasPressed)
                {
                    _wasTriggerPressed[songRow.Id] = true;
                    ExecuteSongSequence(roClient, hWnd, songRow);
                    // Refresh state after sequence execution so holding the trigger key does not re-trigger
                    _wasTriggerPressed[songRow.Id] = ClientInput.IsKeyPressed(songRow.TriggerKey);
                    break;
                }
                else if (!isPressed && wasPressed)
                {
                    _wasTriggerPressed[songRow.Id] = false;
                }
            }
            return 0;
        }

        private void ExecuteSongSequence(Client roClient, IntPtr hWnd, SongRow songRow)
        {
            List<Keys> activeSongKeys = songRow.GetActiveSongKeys();
            if (activeSongKeys.Count == 0) return;

            // Equip instrument if specified and distinct from adaptation
            if (songRow.InstrumentKey != Keys.None && songRow.InstrumentKey != songRow.AdaptationKey)
            {
                ClientInput.SendKey(hWnd, songRow.InstrumentKey, blockOnAlt: false);
                if (!SleepWithCancel(roClient, 30)) return;
            }

            for (int step = 0; step < activeSongKeys.Count; step++)
            {
                if (!_running || !roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                    return;

                // 1. Cast the song key
                ClientInput.SendKey(hWnd, activeSongKeys[step], blockOnAlt: false);

                // Delay between song key and adaptation/cancel key
                if (songRow.Delay > 0)
                {
                    if (!SleepWithCancel(roClient, songRow.Delay)) return;
                }

                // 2. Cancel song via adaptation / weapon switch
                if (songRow.AdaptationKey != Keys.None)
                {
                    ClientInput.SendKey(hWnd, songRow.AdaptationKey, blockOnAlt: false);

                    // Delay between adaptation key and the next song in the chain
                    if (step < activeSongKeys.Count - 1 && songRow.Delay > 0)
                    {
                        if (!SleepWithCancel(roClient, songRow.Delay)) return;
                    }
                }
                else if (step < activeSongKeys.Count - 1 && songRow.Delay > 0)
                {
                    // If no adaptation key is set, still delay between consecutive songs
                    if (!SleepWithCancel(roClient, songRow.Delay)) return;
                }
            }
        }

        private bool SleepWithCancel(Client roClient, int milliseconds)
        {
            int elapsed = 0;
            while (elapsed < milliseconds)
            {
                if (!_running) return false;
                if (!roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                    return false;

                int slice = Math.Min(25, milliseconds - elapsed);
                Thread.Sleep(slice);
                elapsed += slice;
            }
            return true;
        }

        public void Start()
        {
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                Stop(); // ensure thread and state are cleaned before starting

                _running = true;
                _wasTriggerPressed.Clear();
                this.thread = new ThreadRunner((_) => SongMacroThread(roClient), "SongMacro") { IterationDelay = 1 };
                ThreadRunner.Start(this.thread);
            }
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
            _wasTriggerPressed.Clear();
        }
    }
}