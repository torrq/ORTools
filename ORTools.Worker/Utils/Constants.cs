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
    public const int WM_LBUTTONDOWN    = 0x0201;
    public const int WM_LBUTTONUP      = 0x0202;

    // ── Virtual key codes ─────────────────────────────────────────────────────
    public const byte VK_SHIFT   = 0x10;
    public const byte VK_LMENU   = 0xA4;   // Left Alt
    public const byte VK_RMENU   = 0xA5;   // Right Alt

    // ── keybd_event flags ─────────────────────────────────────────────────────
    public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const int KEYEVENTF_KEYUP       = 0x0002;

    // ── mouse_event flags ─────────────────────────────────────────────────────
    public const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP    = 0x0004;

    // ── Mouse movement pixels for skill spammer flick ─────────────────────────
    public const int MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK = 2;

    // ── Memory / game constants ───────────────────────────────────────────────
    public const int  MAX_BUFF_LIST_INDEX_SIZE = 100;
    public const uint INVALID_STATUS           = uint.MaxValue;
}
