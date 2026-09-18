
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Cursor = System.Windows.Forms.Cursor;

namespace ORTools.Worker
{
    public class SkillTimerKey
    {
        public Keys Key { get; set; }
        public bool AltKey { get; set; }
        public bool Enabled { get; set; }

        private int _delay = AppConfig.MacroDefaultDelay;
        public int Delay
        {
            get => _delay < 0 ? AppConfig.MacroDefaultDelay : _delay;
            set => _delay = value;
        }

        private int _clickMode = 0;

        /// <summary>
        /// Represents the click behavior for the skill timer.
        /// 0: No Click
        /// 1: Click at current mouse position
        /// Legacy 2 (Center) falls back to 1 (Cursor)
        /// </summary>
        public int ClickMode
        {
            get => _clickMode > 1 ? 1 : _clickMode;
            set => _clickMode = value > 1 ? 1 : value;
        }

        /// <summary>
        /// Constructor used by Newtonsoft.Json for deserialization.
        /// This allows loading profiles that may or may not contain the click-related properties.
        /// </summary>
        [JsonConstructor]
        public SkillTimerKey(Keys key, int delay)
        {
            this.Key = key;
            this.Delay = delay;
        }
    }
}