using RavenneToolsMenu.Config;

namespace RavenneToolsMenu.Core;

/// <summary>Contrat entre le hook global et la fenêtre roue (appelé sur le thread UI, doit rester rapide).</summary>
internal interface ITriggerTarget
{
    bool MiddleGestureActive { get; }   // un geste molette est en cours (le UP doit être avalé)
    bool X2GestureActive { get; }       // idem, bouton latéral
    bool RightCaptured { get; }         // clic droit long capturé (le UP doit être avalé)
    bool WantsTracking { get; }         // roue ouverte : suivre les mouvements
    bool IsStickyOpen { get; }          // roue ouverte en mode test (clic = sélection)

    /// <summary>Début du déclencheur instantané. Retourne true si l'événement doit être avalé.</summary>
    bool HandleTriggerDown(int x, int y);
    void HandleTriggerMove(int x, int y);
    void HandleTriggerUp(int x, int y);

    void HandleRightDown(int x, int y);
    void HandleRightMove(int x, int y);
    void HandleRightUp(int x, int y, bool captured);

    void StickyMove(int x, int y);
    void StickyClick(int x, int y);
}

/// <summary>
/// Hook bas niveau souris (WH_MOUSE_LL) : intercepte le déclencheur configuré,
/// laisse passer tout le reste. Le callback s'exécute sur le thread UI (celui qui installe le hook).
/// </summary>
internal sealed class MouseHookService
{
    private readonly ConfigManager _cfg;
    private readonly ITriggerTarget _target;
    private nint _hook;
    private Win32.LowLevelMouseProc? _proc; // gardé vivant pour éviter le GC du délégué

    // état du déclencheur
    private bool _useMiddle, _useCtrl, _useShift, _useAlt, _useX2, _useRightLong;
    private bool _middleGesture, _x2Gesture;
    private int _lastGeneration;

    private const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_MENU = 0x12;

    public MouseHookService(ConfigManager cfg, ITriggerTarget target)
    {
        _cfg = cfg;
        _target = target;
        ParseTrigger(cfg.Cfg.Trigger.Button);
        _lastGeneration = cfg.Generation;
    }

    private void ParseTrigger(string b)
    {
        _useMiddle = _useCtrl = _useShift = _useAlt = _useX2 = _useRightLong = false;
        var parts = b.ToLowerInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            switch (p)
            {
                case "ctrl": _useCtrl = true; break;
                case "shift": case "maj": _useShift = true; break;
                case "alt": _useAlt = true; break;
                case "middle": case "molette": _useMiddle = true; break;
                case "xbutton2": case "latéral": _useX2 = true; break;
                case "right-long": case "clic-droit-long": _useRightLong = true; break;
            }
        }
        if (!_useMiddle && !_useX2 && !_useRightLong) _useMiddle = true;
    }

    public void Start()
    {
        if (_hook != 0) return;
        _proc = HookProc;
        _hook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, Win32.GetModuleHandleW(null), 0);
        if (_hook == 0)
            Core.Log.Error("Impossible d'installer le hook souris global (erreur " + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ")");
    }

    public void Stop()
    {
        if (_hook != 0) { Win32.UnhookWindowsHookEx(_hook); _hook = 0; }
        _proc = null;
    }

    private nint HookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0)
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);

        var data = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
        int msg = (int)wParam;

        // Ignorer les événements que nous avons nous-mêmes ré-injectés (anti-boucle).
        if ((data.flags & (uint)Win32.LLMHF_INJECTED) != 0 && data.dwExtraInfo == (nuint)Win32.Sentinel)
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);

        // Relire le déclencheur si la configuration a changé à chaud.
        if (_cfg.Generation != _lastGeneration)
        {
            _lastGeneration = _cfg.Generation;
            ParseTrigger(_cfg.Cfg.Trigger.Button);
        }

        // Bouclier critique : une exception qui traverserait le callback natif du hook
        // tuerait le processus entier (0xc000041d). On journalise et on rend la main.
        try
        {
            return Handle(msg, data, nCode, wParam, lParam);
        }
        catch (Exception ex)
        {
            _middleGesture = false;
            _x2Gesture = false;
            Log.Error("Erreur dans le hook souris (neutralisée)", ex);
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    private nint Handle(int msg, Win32.MSLLHOOKSTRUCT data, int nCode, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32.WM_MBUTTONDOWN:
                if (_useMiddle && ModsDown() && _target.HandleTriggerDown(data.pt.X, data.pt.Y))
                { _middleGesture = true; return 1; }
                break;

            case Win32.WM_MBUTTONUP:
                if (_middleGesture && _target.MiddleGestureActive)
                {
                    _middleGesture = false;
                    _target.HandleTriggerUp(data.pt.X, data.pt.Y);
                    return 1;
                }
                _middleGesture = false;
                break;

            case Win32.WM_XBUTTONDOWN:
                if (_useX2 && (data.mouseData >> 16) == 2 && _target.HandleTriggerDown(data.pt.X, data.pt.Y))
                { _x2Gesture = true; return 1; }
                break;

            case Win32.WM_XBUTTONUP:
                if (_x2Gesture && _target.X2GestureActive)
                {
                    _x2Gesture = false;
                    _target.HandleTriggerUp(data.pt.X, data.pt.Y);
                    return 1;
                }
                _x2Gesture = false;
                break;

            case Win32.WM_RBUTTONDOWN:
                if (_useRightLong) _target.HandleRightDown(data.pt.X, data.pt.Y);
                break;

            case Win32.WM_RBUTTONUP:
                if (_useRightLong)
                {
                    bool captured = _target.RightCaptured;
                    _target.HandleRightUp(data.pt.X, data.pt.Y, captured);
                    if (captured) return 1;
                }
                break;

            case Win32.WM_MOUSEMOVE:
                if (_target.WantsTracking) _target.HandleTriggerMove(data.pt.X, data.pt.Y);
                else if (_target.IsStickyOpen) _target.StickyMove(data.pt.X, data.pt.Y);
                break;

            case Win32.WM_LBUTTONDOWN:
                if (_target.IsStickyOpen)
                {
                    _target.StickyClick(data.pt.X, data.pt.Y);
                    return 1;
                }
                break;
        }

        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool ModsDown()
    {
        if (_useCtrl && Win32.GetAsyncKeyState(VK_CONTROL) >= 0) return false;
        if (_useShift && Win32.GetAsyncKeyState(VK_SHIFT) >= 0) return false;
        if (_useAlt && Win32.GetAsyncKeyState(VK_MENU) >= 0) return false;
        return true;
    }
}
