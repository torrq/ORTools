namespace ORTools.Worker;

public static class Constants
{
    // ── Hook types ────────────────────────────────────────────────────────────
    public const int WH_KEYBOARD_LL = 13;

    // ── Window messages ───────────────────────────────────────────────────────
    public const int WM_KEYDOWN_MSG_ID = 0x0100;
    public const int WM_KEYUP_MSG_ID   = 0x0101;
    public const int WM_SYSKEYDOWN     = 0x0104;
    public const int WM_SYSKEYUP       = 0x0105;
    public const int WM_MOUSEMOVE      = 0x0200;
    public const int WM_LBUTTONDOWN    = 0x0201;
    public const int WM_LBUTTONUP      = 0x0202;

    // ── Virtual key codes ─────────────────────────────────────────────────────
    public const byte VK_SHIFT   = 0x10;
    public const byte VK_LMENU   = 0xA4;   // Left Alt
    public const byte VK_RMENU   = 0xA5;   // Right Alt

    // ── keybd_event flags ─────────────────────────────────────────────────────
    public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const int KEYEVENTF_KEYUP       = 0x0002;

    // ── mouse_event / SendInput MOUSEINPUT flags ──────────────────────────────
    public const uint MOUSEEVENTF_MOVE           = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN       = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP         = 0x0004;
    public const uint MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000; // stop Windows from merging this move with adjacent ones
    public const uint MOUSEEVENTF_VIRTUALDESK    = 0x4000; // normalize against the full virtual screen, not just the primary monitor
    public const uint MOUSEEVENTF_ABSOLUTE       = 0x8000;

    // ── SendInput ──────────────────────────────────────────────────────────────
    public const uint INPUT_MOUSE = 0;

    // ── GetSystemMetrics indices ──────────────────────────────────────────────
    public const int SM_XVIRTUALSCREEN  = 76;
    public const int SM_YVIRTUALSCREEN  = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    // ── Mouse movement pixels for skill spammer flick ─────────────────────────
    public const int MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK = 2;

    // ── Memory / game constants ───────────────────────────────────────────────
    public const int  MAX_BUFF_LIST_INDEX_SIZE = 100;
    public const uint INVALID_STATUS           = uint.MaxValue;
    public const int  MINIMUM_HP_TO_RECOVER    = 1;   // HP must be above this to use buff/recovery items

    // ── Mouse button messages ─────────────────────────────────────────────────
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP   = 0x0205;
}
