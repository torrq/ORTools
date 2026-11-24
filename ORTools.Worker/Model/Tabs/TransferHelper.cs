using Newtonsoft.Json;
using System;
using System.Threading;
using System.Windows.Forms;

namespace ORTools.Worker
{
    public class TransferHelper : IAction
    {
        public static string ACTION_NAME_TRANSFER = "TransferHelper";
        public string ActionName { get; set; } = ACTION_NAME_TRANSFER;
        private ThreadRunner? thread;
        private volatile bool _running = false;
        private int _altHeld = 0;

        public Keys TransferKey { get; set; } = Keys.None;

        public string GetActionName() => ACTION_NAME_TRANSFER;

        public string GetConfiguration() => JsonConvert.SerializeObject(this);

        private void HoldAltOnce()
        {
            if (Interlocked.Exchange(ref _altHeld, 1) == 0)
            {
                ClientInput.HoldAlt();
            }
        }

        private void ReleaseAltIfHeld()
        {
            if (Interlocked.Exchange(ref _altHeld, 0) == 1)
            {
                ClientInput.ReleaseAlt();
            }
        }

        private int TransferHelperThread(Client roClient)
        {
            if (!_running) return 0;

            var transferKey = ProfileSingleton.GetCurrent().TransferHelper.TransferKey;
            if (transferKey == Keys.None)
            {
                Thread.Sleep(50);
                return 0;
            }

            if (!roClient.IsProcessRunning() || roClient.IsTextInputActive() || roClient.IsDead())
            {
                Thread.Sleep(50);
                return 0;
            }

            IntPtr hWnd = roClient.MainWindowHandle;
            if (hWnd == IntPtr.Zero || !ClientInput.IsForeground(hWnd))
            {
                Thread.Sleep(50);
                return 0;
            }

            if (ClientInput.IsKeyPressed(transferKey))
            {
                TransferHelperMacro(roClient, hWnd, transferKey);
                return 0;
            }

            Thread.Sleep(50);
            return 0;
        }

        private void TransferHelperMacro(Client roClient, IntPtr hWnd, Keys key)
        {
            try
            {
                HoldAltOnce();

                while (_running
                    && ClientInput.IsKeyPressed(key)
                    && roClient.IsProcessRunning()
                    && ClientInput.IsForeground(hWnd)
                    && !roClient.IsTextInputActive()
                    && !roClient.IsDead())
                {
                    ClientInput.SendRightClickFast(hWnd);
                    Thread.Sleep(10);
                }
            }
            finally
            {
                ReleaseAltIfHeld();
            }
        }

        public void Start()
        {
            Client? roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                Stop();
                _running = true;
                this.thread = new ThreadRunner((_) => TransferHelperThread(roClient), "TransferHelper") { IterationDelay = 1 };
                ThreadRunner.Start(this.thread);
            }
        }

        public void Stop()
        {
            _running = false;
            ReleaseAltIfHeld();
            if (this.thread != null)
            {
                ThreadRunner.Stop(this.thread);
                this.thread.Terminate();
                this.thread.Join(500);
                this.thread = null;
            }
        }
    }
}
