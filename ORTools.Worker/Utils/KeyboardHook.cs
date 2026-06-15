using System.Runtime.InteropServices;

namespace ORTools.Worker;

public static class KeyboardHook
{
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr _hHook = IntPtr.Zero;
    private static readonly HookProc _hookProc = Filter;
    private static bool _enabled;

    public static bool Control { get; private set; }
    public static bool Shift   { get; private set; }
    public static bool Alt     { get; private set; }
    public static bool Win     { get; private set; }

    public delegate bool KeyPressed();
    public static KeyboardHookHandler? KeyDown;

    public static event Action<Keys>? OnKeyDownEvent;
    public static event Action<Keys>? OnKeyUpEvent;

    public delegate bool KeyboardHookHandler(Keys key);

    private static readonly Dictionary<Keys, KeyPressed> _keysDown = new();
    private static readonly Dictionary<Keys, KeyPressed> _keysUp   = new();

    public static bool Enable()
    {
        if (_enabled) return false;
        try
        {
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            _hHook  = Win32Interop.SetWindowsHookEx(Constants.WH_KEYBOARD_LL, _hookProc,
                          Win32Interop.GetModuleHandle(mod.ModuleName!), 0);
            _enabled = true;
            return true;
        }
        catch { _enabled = false; return false; }
    }

    public static bool Disable()
    {
        if (!_enabled) return false;
        try { Win32Interop.UnhookWindowsHookEx(_hHook); _enabled = false; return true; }
        catch { _enabled = true; return false; }
    }

    public static bool AddKeyDown(Keys key, KeyPressed cb)
    {
        KeyDown = null;
        if (_keysDown.ContainsKey(key)) return false;
        _keysDown.Add(key, cb); return true;
    }

    public static bool AddKeyUp(Keys key, KeyPressed cb)
    {
        if (_keysUp.ContainsKey(key)) return false;
        _keysUp.Add(key, cb); return true;
    }

    public static void ClearKeyDowns()
    {
        _keysDown.Clear();
    }

    public static bool RemoveDown(Keys key) => _keysDown.Remove(key);
    public static bool RemoveUp(Keys key)   => _keysUp.Remove(key);
    public static bool Add(Keys key, KeyPressed cb) => AddKeyDown(key, cb);
    public static bool Remove(Keys key)              => RemoveDown(key);

    private static IntPtr Filter(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == (IntPtr)Constants.WM_KEYDOWN_MSG_ID || wParam == (IntPtr)Constants.WM_SYSKEYDOWN)
            {
                int vk = Marshal.ReadInt32(lParam);
                switch ((Keys)vk)
                {
                    case Keys.LControlKey: case Keys.RControlKey: Control = true;  break;
                    case Keys.LShiftKey:   case Keys.RShiftKey:   Shift   = true;  break;
                    case Keys.LMenu:       case Keys.RMenu:       Alt     = true;  break;
                    case Keys.LWin:        case Keys.RWin:        Win     = true;  break;
                    default:
                        bool pass = OnKeyDown((Keys)vk);
                        return pass ? Win32Interop.CallNextHookEx(_hHook, nCode, wParam, lParam) : new IntPtr(1);
                }
            }
            else if (wParam == (IntPtr)Constants.WM_KEYUP_MSG_ID || wParam == (IntPtr)Constants.WM_SYSKEYUP)
            {
                int vk = Marshal.ReadInt32(lParam);
                switch ((Keys)vk)
                {
                    case Keys.LControlKey: case Keys.RControlKey: Control = false; break;
                    case Keys.LShiftKey:   case Keys.RShiftKey:   Shift   = false; break;
                    case Keys.LMenu:       case Keys.RMenu:       Alt     = false; break;
                    case Keys.LWin:        case Keys.RWin:        Win     = false; break;
                    default:
                        bool pass = OnKeyUp((Keys)vk);
                        return pass ? Win32Interop.CallNextHookEx(_hHook, nCode, wParam, lParam) : new IntPtr(1);
                }
            }
        }
        return Win32Interop.CallNextHookEx(_hHook, nCode, wParam, lParam);
    }

    private static bool OnKeyDown(Keys key)
    {
        OnKeyDownEvent?.Invoke(key);
        if (KeyDown != null) return KeyDown(key);
        return _keysDown.TryGetValue(key, out var cb) ? cb() : true;
    }

    private static bool OnKeyUp(Keys key)
    {
        OnKeyUpEvent?.Invoke(key);
        return _keysUp.TryGetValue(key, out var cb) ? cb() : true;
    }

    public static string KeyToString(Keys key) =>
        (Control ? "Ctrl + " : "") + (Alt ? "Alt + " : "") +
        (Shift ? "Shift + " : "") + (Win ? "Win + " : "") + key;
}
