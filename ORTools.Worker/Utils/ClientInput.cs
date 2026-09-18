using System;
using System.Drawing;
using System.Threading;

namespace ORTools.Worker;

public static class ClientInput
{
    public static bool SendKey(IntPtr hWnd, Keys key, bool blockOnAlt = true)
    {
        if (hWnd == IntPtr.Zero || key == Keys.None) return false;
        if (blockOnAlt && (Win32Interop.IsKeyPressed(Keys.LMenu) || Win32Interop.IsKeyPressed(Keys.RMenu))) return false;

        // Keep the key down briefly so older RO clients reliably process the
        // hotkey and transition into skill-targeting mode.
        Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, key, Win32Interop.CreateLParam(key, true));
        Thread.Sleep(20);
        Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, key, Win32Interop.CreateLParam(key, false));
        return true;
    }

    public static bool SendKey(Keys key, bool blockOnAlt = true)
    {
        var client = ClientSingleton.GetClient();
        if (client?.Process != null && !client.Process.HasExited)
            return SendKey(client.Process.MainWindowHandle, key, blockOnAlt);
        return false;
    }

    public static void SendLeftClick(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) return;
        Win32Interop.SendMessage(hWnd, Constants.WM_LBUTTONDOWN, (IntPtr)1, IntPtr.Zero);
        Thread.Sleep(25);
        Win32Interop.SendMessage(hWnd, Constants.WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
    }

    public static void SendRightClick(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) return;
        Win32Interop.SendMessage(hWnd, Constants.WM_RBUTTONDOWN, (IntPtr)2, IntPtr.Zero);
        Thread.Sleep(25);
        Win32Interop.SendMessage(hWnd, Constants.WM_RBUTTONUP, IntPtr.Zero, IntPtr.Zero);
    }

    public static void HoldAlt()
    {
        Win32Interop.keybd_event(Constants.VK_LMENU, 0, Constants.KEYEVENTF_EXTENDEDKEY, 0);
    }

    public static void ReleaseAlt()
    {
        Win32Interop.keybd_event(Constants.VK_LMENU, 0, Constants.KEYEVENTF_EXTENDEDKEY | Constants.KEYEVENTF_KEYUP, 0);
    }

    public static void SendAltKeyCombo(IntPtr hWnd, Keys key)
    {
        if (!IsForeground(hWnd))
        {
            SetForeground(hWnd);
            Thread.Sleep(30);
        }

        HoldAlt();
        Thread.Sleep(20);
        SendKey(hWnd, key, blockOnAlt: false);
        Thread.Sleep(20);
        ReleaseAlt();
    }

    public static bool IsKeyPressed(Keys key)
    {
        if (key == Keys.None) return false;
        return Win32Interop.IsKeyPressed(key);
    }

    public static bool IsForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        IntPtr fgWindow = Win32Interop.GetForegroundWindow();
        if (fgWindow == hWnd) return true;
        if (fgWindow != IntPtr.Zero)
        {
            Win32Interop.GetWindowThreadProcessId(fgWindow, out uint fgPid);
            Win32Interop.GetWindowThreadProcessId(hWnd, out uint targetPid);
            if (fgPid != 0 && fgPid == targetPid)
            {
                return true;
            }
        }
        return false;
    }

    public static void SetForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        Win32Interop.ShowWindow(hWnd, 9); // SW_RESTORE
        Win32Interop.SetForegroundWindow(hWnd);
    }

    public static void HoldShift()
    {
        Win32Interop.keybd_event(Constants.VK_SHIFT, 0x45, Constants.KEYEVENTF_EXTENDEDKEY, 0);
    }

    public static void ReleaseShift()
    {
        Win32Interop.keybd_event(Constants.VK_SHIFT, 0x45, Constants.KEYEVENTF_EXTENDEDKEY | Constants.KEYEVENTF_KEYUP, 0);
    }

    public static Point GetCursorPos()
    {
        Win32Interop.GetCursorPos(out Win32Interop.POINT pt);
        return new Point(pt.X, pt.Y);
    }

    public static void SetCursorPos(int x, int y)
    {
        Win32Interop.SetCursorPos(x, y);
    }

    public static Point ClientToScreen(IntPtr hWnd, Point pt)
    {
        var win32Pt = new Win32Interop.POINT { X = pt.X, Y = pt.Y };
        Win32Interop.ClientToScreen(hWnd, ref win32Pt);
        return new Point(win32Pt.X, win32Pt.Y);
    }

    public static Rectangle GetClientRect(IntPtr hWnd)
    {
        if (Win32Interop.GetClientRect(hWnd, out Win32Interop.RECT rect))
        {
            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        return Rectangle.Empty;
    }

    public static void SendRawMouseEvent(uint flags, uint x = 0, uint y = 0)
    {
        Win32Interop.mouse_event(flags, x, y, 0, 0);
    }

    public static void SendMouseMove(int dx, int dy)
    {
        Win32Interop.mouse_event(Constants.MOUSEEVENTF_MOVE, unchecked((uint)dx), unchecked((uint)dy), 0, 0);
    }

    public static bool IsWindowVisible(IntPtr hWnd)
    {
        return Win32Interop.IsWindowVisible(hWnd);
    }

    public static bool MoveCursorToCenter(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            var client = ClientSingleton.GetClient();
            hWnd = client?.MainWindowHandle ?? client?.Process?.MainWindowHandle ?? IntPtr.Zero;
        }

        if (hWnd == IntPtr.Zero) return false;

        if (!Win32Interop.GetClientRect(hWnd, out var rect)) return false;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return false;

        int centerX = width / 2;
        int centerY = height / 2;

        var centerPoint = new Win32Interop.POINT { X = centerX, Y = centerY };
        if (!Win32Interop.ClientToScreen(hWnd, ref centerPoint)) return false;

        SetCursorPos(centerPoint.X, centerPoint.Y);
        return true;
    }

    public static bool ClickAtCurrentPosition(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;

        Win32Interop.SendMessage(hWnd, Constants.WM_LBUTTONDOWN, (IntPtr)1, IntPtr.Zero);
        Thread.Sleep(25);
        Win32Interop.SendMessage(hWnd, Constants.WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);

        return true;
    }

    public static bool ClickAtWindowCenter(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            var client = ClientSingleton.GetClient();
            hWnd = client?.MainWindowHandle ?? client?.Process?.MainWindowHandle ?? IntPtr.Zero;
        }

        if (hWnd == IntPtr.Zero) return false;

        if (!IsForeground(hWnd))
        {
            SetForeground(hWnd);
            Thread.Sleep(30);
        }

        if (!Win32Interop.GetClientRect(hWnd, out var rect))
        {
            DebugLogger.Warning($"[SkillTimer] ClickAtWindowCenter: GetClientRect failed for hWnd 0x{hWnd:X}.");
            return false;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            DebugLogger.Warning($"[SkillTimer] ClickAtWindowCenter: Invalid client area {width}x{height} for hWnd 0x{hWnd:X}.");
            return false;
        }

        int centerX = width / 2;
        int centerY = height / 2;
        int lParam = ((centerY & 0xFFFF) << 16) | (centerX & 0xFFFF);

        // Do not call SetCursorPos — avoid synthetic cursor movement that triggers anti-cheat click blocking.
        // Send WM_MOUSEMOVE directly to the window so the game positions its targeting reticle at the center,
        // followed by WM_LBUTTONDOWN/UP with center coordinates matching Cursor Mode.
        Win32Interop.SendMessage(hWnd, Constants.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)lParam);
        Thread.Sleep(20);

        Win32Interop.SendMessage(hWnd, Constants.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
        Thread.Sleep(25);
        Win32Interop.SendMessage(hWnd, Constants.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);

        DebugLogger.Info($"[SkillTimer] Clicked center ({centerX}, {centerY}) on window 0x{hWnd:X}");
        return true;
    }
}
