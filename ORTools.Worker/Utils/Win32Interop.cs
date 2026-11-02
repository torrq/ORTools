using System.Runtime.InteropServices;

namespace ORTools.Worker;

internal static class Win32Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // Window management
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // Mouse
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtra);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref System.Drawing.Point pt);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

    // Keyboard
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtra);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(Keys vKey);
    public static bool IsKeyPressed(Keys key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    public static bool SendRelativeMouseMove(int dx, int dy)
    {
        var input = new INPUT
        {
            type = 0, // INPUT_MOUSE
            mi = new MOUSEINPUT
            {
                dx = dx,
                dy = dy,
                mouseData = 0,
                dwFlags = 0x0001, // MOUSEEVENTF_MOVE
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT))) == 1;
    }

    // Messaging
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessage(IntPtr hWnd, int Msg, Keys wParam, int lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public static int CreateLParam(Keys key, bool isKeyDown)
    {
        uint scanCode = MapVirtualKey((uint)key, 0);
        int lParam = 1; // Repeat count
        lParam |= (int)(scanCode << 16);
        if (!isKeyDown)
        {
            lParam |= (1 << 30); // Previous key state
            lParam |= unchecked((int)0x80000000); // Transition state
        }
        return lParam;
    }

    // Hooks
    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr SetWindowsHookEx(int idHook, KeyboardHook.HookProc lpfn, IntPtr hInstance, int threadId);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)] public static extern bool UnhookWindowsHookEx(IntPtr idHook);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] public static extern IntPtr GetModuleHandle(string lpModuleName);

    // Timer resolution & power throttling
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)] public static extern uint timeBeginPeriod(uint uMilliseconds);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod",   SetLastError = true)] public static extern uint timeEndPeriod(uint uMilliseconds);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    public const int ProcessPowerThrottling = 4;
    public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    public const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        uint ProcessInformationSize);

    /// <summary>
    /// Opts the process out of Windows 10/11 EcoQoS execution speed throttling and background timer
    /// resolution throttling so that <see cref="timeBeginPeriod"/>(1) is honored even when ORTools
    /// is minimized or occluded behind the game window.
    /// </summary>
    public static void DisableProcessPowerThrottling()
    {
        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                StateMask = 0
            };
            SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
        }
        catch
        {
            // Ignore on older OS versions that do not support ProcessPowerThrottling
        }
    }
}
