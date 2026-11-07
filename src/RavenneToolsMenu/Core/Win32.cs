using System.Runtime.InteropServices;
using System.Text;

namespace RavenneToolsMenu.Core;

/// <summary>Interops Win32 regroupées (souris, fenêtres, DPI, shell).</summary>
internal static class Win32
{
    public const int WH_MOUSE_LL = 14;

    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;

    public const int WM_NCLBUTTONDOWN = 0x00A1;

    public static readonly nint LLMHF_INJECTED = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hhk);
    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LockWorkStation();

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(nint hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("gdi32.dll")]
    public static extern nint GetDC(nint hWnd);
    [DllImport("gdi32.dll")]
    public static extern int ReleaseDC(nint hWnd, nint hDC);
    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(nint hdc, int nIndex);

    // ---- Recyclage (corbeille) ----
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBinW(nint hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct SHFILEOPSTRUCTW
    {
        public nint hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public nint hNameMappings;
        public string? lpszProgressTitle;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHFileOperationW(ref SHFILEOPSTRUCTW lpFileOp);

    public const uint FO_DELETE = 3;
    public const ushort FOF_ALLOWUNDO = 0x40;
    public const ushort FOF_NOCONFIRMATION = 0x10;
    public const ushort FOF_SILENT = 0x4;
    public const ushort FOF_NOERRORUI = 0x400;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint DragQueryFileW(nint hDrop, uint iFile, StringBuilder? lpszFile, int cch);

    // ---- SendInput ----
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nuint dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nuint dwExtraInfo;
    }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public MOUSEINPUT mi;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    public const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_XDOWN = 0x0080, MOUSEEVENTF_XUP = 0x0100;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern int SetWindowLongW(nint hWnd, int nIndex, long dwNewLong);
    [DllImport("user32.dll")]
    public static extern int GetWindowLongW(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    public const uint SWP_NOACTIVATE = 0x0010;
    public static readonly nint HWND_TOPMOST = -1;

    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x0800_0000;
    public const long WS_EX_TOOLWINDOW = 0x0000_0080;
    public const long WS_EX_TRANSPARENT = 0x0000_0020;
    public const long WS_EX_TOPMOST = 0x0000_0008;

    /// <summary>Marqueur placé sur les événements souris que nous ré-injectons nous-mêmes.</summary>
    public const ulong Sentinel = 0x5254_564E;

    public static string GetClassName(nint hwnd)
    {
        var sb = new StringBuilder(64);
        GetClassNameW(hwnd, sb, 64);
        return sb.ToString();
    }

    public static string GetProcessNameFromHwnd(nint hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "";
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName.ToLowerInvariant();
        }
        catch { return ""; }
    }

    /// <summary>Facteur d'échelle (DPI/96) du moniteur contenant le point, en pixels physiques.</summary>
    public static double GetScaleForPoint(int px, int py)
    {
        var pt = new POINT { X = px, Y = py };
        var mon = MonitorFromPoint(pt, 1 /*MONITOR_DEFAULTTONEAREST*/);
        if (mon != 0 && GetDpiForMonitor(mon, 0, out uint dx, out _) == 0 && dx > 0)
            return dx / 96.0;
        nint hdc = GetDC(0);
        if (hdc != 0)
        {
            int dpi = GetDeviceCaps(hdc, 88 /*LOGPIXELSX*/);
            ReleaseDC(0, hdc);
            if (dpi > 0) return dpi / 96.0;
        }
        return 1.0;
    }

    public const int GWL_STYLE = -16;
    public const long WS_CAPTION = 0x00C0_0000;

    /// <summary>
    /// Vrai si la fenêtre au premier plan est un vrai plein écran type jeu :
    /// couvre tout le moniteur ET n'a pas de bordure/barre de titre (WS_CAPTION).
    /// Une simple fenêtre maximisée (qui garde sa bordure) n'est PAS du plein écran.
    /// </summary>
    public static bool IsForegroundFullscreen()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0) return false;
        var cls = GetClassName(hwnd);
        if (cls is "Progman" or "WorkerW" or "SysListView32") return false;
        if (!GetWindowRect(hwnd, out var r)) return false;
        var mon = MonitorFromWindow(hwnd, 2 /*MONITOR_DEFAULTTONEAREST*/);
        if (mon == 0) return false;
        var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfoW(mon, ref mi)) return false;

        bool coversMonitor = r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top
                          && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        if (!coversMonitor) return false;

        // une fenêtre maximisée garde WS_CAPTION ; un jeu plein écran borderless ne l'a pas.
        long style = GetWindowLongW(hwnd, GWL_STYLE);
        return (style & WS_CAPTION) == 0;
    }
}
