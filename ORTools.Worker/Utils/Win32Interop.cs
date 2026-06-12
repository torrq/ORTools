using System.Runtime.InteropServices;

namespace ORTools.Worker;

internal static class Win32Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    // Window management
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // Mouse
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtra);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref System.Drawing.Point pt);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

    // Keyboard
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtra);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(Keys vKey);
    public static bool IsKeyPressed(Keys key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // Messaging
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessage(IntPtr hWnd, int Msg, Keys wParam, int lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

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

    // Timer resolution
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)] public static extern uint timeBeginPeriod(uint uMilliseconds);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod",   SetLastError = true)] public static extern uint timeEndPeriod(uint uMilliseconds);
}
