using System.Drawing;

namespace ORTools.Worker;

public static class MouseHelper
{
    public static Point GetCursorPosition()
    {
        Win32Interop.GetCursorPos(out var pt);
        return new Point(pt.X, pt.Y);
    }

    public static void LeftClick(int x, int y)
    {
        Win32Interop.mouse_event(Constants.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
        Thread.Sleep(5);
        Win32Interop.mouse_event(Constants.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
    }
}
