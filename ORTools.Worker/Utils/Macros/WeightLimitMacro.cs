using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;


namespace ORTools.Worker
{
    public static class WeightLimitMacro
    {
        private static readonly Dictionary<Keys, string> _sendKeysMap = new Dictionary<Keys, string>()
        {
             { Keys.D0, "0" },
             { Keys.D1, "1" },
             { Keys.D2, "2" },
             { Keys.D3, "3" },
             { Keys.D4, "4" },
             { Keys.D5, "5" },
             { Keys.D6, "6" },
             { Keys.D7, "7" },
             { Keys.D8, "8" },
             { Keys.D9, "9" }
        };

        public static string ToSendKeysFormat(Keys key)
        {
            if (_sendKeysMap.TryGetValue(key, out string value))
            {
                return value;
            }
            return key.ToString().ToLower();
        }

        public static void SendOverweightMacro()
        {
            var client = ClientSingleton.GetClient();
            if (client?.Process == null || client.Process.HasExited)
            {
                DebugLogger.Debug("OverweightMacro: Client process is null or has exited, aborting macro.");
                return;
            }

            ConfigProfile prefs = ProfileSingleton.GetCurrent().UserPreferences;
            int timesToSend = 2;
            int intervalMs = 5000;
            bool ShouldSendKey1 = (!string.IsNullOrEmpty(prefs.AutoOffKey1.ToString()) && prefs.AutoOffKey1.ToString() != AppConfig.TEXT_NONE);
            bool ShouldSendKey2 = (!string.IsNullOrEmpty(prefs.AutoOffKey2.ToString()) && prefs.AutoOffKey2.ToString() != AppConfig.TEXT_NONE);
            bool ShouldKillClient = prefs.AutoOffKillClient;

            DebugLogger.Debug($"OverweightMacro: ShouldSendKey1={ShouldSendKey1}, ShouldSendKey2={ShouldSendKey2}, ShouldKillClient={ShouldKillClient}");

            if (ShouldSendKey1 || ShouldSendKey2)
            {
                IntPtr hWnd = client.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    if (!ClientInput.IsForeground(hWnd)) { ClientInput.SetForeground(hWnd); }

                    Thread.Sleep(1000);

                    if (ShouldSendKey1)
                    {
                        for (int i = 0; i < timesToSend; i++)
                        {
                            if (client.Process == null || client.Process.HasExited) return;
                            ClientInput.SendAltKeyCombo(hWnd, prefs.AutoOffKey1);
                            DebugLogger.Info($"Sent macro {i + 1}/{timesToSend}: Alt + {prefs.AutoOffKey1} (Auto-off, key 1)");
                            if (i < timesToSend - 1) Thread.Sleep(intervalMs);
                        }
                    }

                    if (ShouldSendKey1 && ShouldSendKey2) Thread.Sleep(1000);

                    if (ShouldSendKey2)
                    {
                        for (int i = 0; i < timesToSend; i++)
                        {
                            if (client.Process == null || client.Process.HasExited) return;
                            ClientInput.SendAltKeyCombo(hWnd, prefs.AutoOffKey2);
                            DebugLogger.Info($"Sent macro {i + 1}/{timesToSend}: Alt + {prefs.AutoOffKey2} (Auto-off, key 2)");
                            if (i < timesToSend - 1) Thread.Sleep(intervalMs);
                        }
                    }
                }
            }

            if (ShouldKillClient)
            {
                if (client.Process != null && !client.Process.HasExited)
                {
                    Thread.Sleep(1000);
                    DebugLogger.Info("Killing the client (Auto-off)");
                    client.Kill();
                }
            }
        }

    }
}