using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using RavenneToolsMenu.Actions;
using RavenneToolsMenu.Config;
using RavenneToolsMenu.Core;
using RavenneToolsMenu.Detection;

namespace RavenneToolsMenu.UI;

/// <summary>
/// Fenêtre overlay de la roue + contrôleur gestuel :
/// maintenir le déclencheur → glisser vers une option → les sous-menus s'ouvrent
/// au passage → relâcher pour activer. Glisser au centre d'un sous-menu = revenir.
/// </summary>
public sealed class WheelWindow : Window, ITriggerTarget
{
    private readonly ConfigManager _cfg;
    private readonly MenuResolver _resolver;
    private readonly ActionExecutor _exec;
    private readonly ToastHost _toasts;
    private readonly DetectionWorker _detect;
    private readonly WheelRenderer _renderer;

    private readonly List<WheelFrame> _frames = new();
    private ContextInfo _ctx = new();
    private bool _open;
    private bool _sticky;
    private TriggerKind _triggerKind;

    // état du geste
    private long _pressTime;
    private bool _draggedOut;
    private bool _leftDeadzone;          // le pointeur est-il ressorti de la zone morte (depuis le dernier push) ?
    private int _hover = -1;
    private long _hoverSince;
    private long _deadzoneSince;
    private bool _popArmed;

    // clic droit long
    private readonly DispatcherTimer _rightTimer;
    private Point _rightPos;
    private bool _rightArmed;
    private bool _rightCaptured;

    // conversion écran → élément
    private double _scale = 1.0;

    private enum TriggerKind { Middle, X2, RightLong }

    public WheelWindow(ConfigManager cfg, MenuResolver resolver, ActionExecutor exec, ToastHost toasts, DetectionWorker detect)
    {
        _cfg = cfg;
        _resolver = resolver;
        _exec = exec;
        _toasts = toasts;
        _detect = detect;

        _renderer = new WheelRenderer();
        _renderer.Configure(cfg.Cfg);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Content = _renderer;
        Visibility = Visibility.Hidden;
        Left = -10000; Top = -10000;

        _rightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Clamp(cfg.Cfg.Trigger.LongPressMs, 150, 1500)) };
        _rightTimer.Tick += (_, _) => RightTimerFired();

        SourceInitialized += (_, _) => SetExStyles();
        // Préparer la fenêtre une fois (compilation des ressources d'affichage) pour une ouverture instantanée ensuite.
        SourceInitialized += (_, _) => Dispatcher.BeginInvoke(PreWarm, DispatcherPriority.ApplicationIdle);
    }

    private void SetExStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32.SetWindowLongW(hwnd, Win32.GWL_EXSTYLE,
            Win32.GetWindowLongW(hwnd, Win32.GWL_EXSTYLE)
            | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST);
    }

    private bool _preWarmed;

    private void PreWarm()
    {
        // Un rendu hors écran pour forcer l'initialisation GPU, puis masquage.
        // Si un geste a déjà commencé (molette pressée juste après le lancement),
        // on ne touche surtout pas aux frames — le geste réchauffe la fenêtre lui-même.
        if (_open || _preWarmed) return;
        _preWarmed = true;
        _frames.Clear();
        _frames.Add(new WheelFrame
        {
            Menu = new MenuDef { Label = "", Items = new List<MenuItem>() },
            Center = new Point(0, 0),
        });
        _renderer.SetFrames(_frames);
        Visibility = Visibility.Visible;
        Visibility = Visibility.Hidden;
        _frames.Clear();
        _renderer.SetFrames(_frames);
    }

    // ===================== ITriggerTarget =====================

    public bool MiddleGestureActive => _open && _triggerKind == TriggerKind.Middle && !_sticky;
    public bool X2GestureActive => _open && _triggerKind == TriggerKind.X2 && !_sticky;
    public bool RightCaptured => _rightCaptured;
    public bool WantsTracking => _open && !_sticky;
    public bool IsStickyOpen => _sticky;

    bool ITriggerTarget.HandleTriggerDown(int x, int y)
    {
        if (_open) return false;
        if (!ShouldOpen())
        {
            Log.Info("Déclencheur ignoré (processus exclu ou application plein écran)");
            return false;
        }
        _triggerKind = DetectTriggerKind();
        BeginGesture(x, y);
        return true;
    }

    void ITriggerTarget.HandleTriggerMove(int x, int y) => UpdatePointer(x, y);

    void ITriggerTarget.HandleTriggerUp(int x, int y) => FinishGesture(x, y);

    void ITriggerTarget.HandleRightDown(int x, int y)
    {
        if (_open || !ShouldOpen()) return;
        _rightArmed = true;
        _rightPos = new Point(x, y);
        _rightTimer.Start();
    }

    void ITriggerTarget.HandleRightMove(int x, int y)
    {
        if (_rightCaptured && _open)
        {
            UpdatePointer(x, y);
            return;
        }
        if (_rightArmed && Math.Sqrt(Math.Pow(x - _rightPos.X, 2) + Math.Pow(y - _rightPos.Y, 2)) > 12)
            CancelRight();
    }

    void ITriggerTarget.HandleRightUp(int x, int y, bool captured)
    {
        if (captured && _open)
        {
            _rightCaptured = false;
            FinishGesture(x, y);
        }
        else CancelRight();
    }

    void ITriggerTarget.StickyMove(int x, int y) => UpdatePointer(x, y);

    void ITriggerTarget.StickyClick(int x, int y)
    {
        if (!_sticky) return;
        FinishGesture(x, y, sticky: true);
    }

    // ===================== logique du geste =====================

    private TriggerKind DetectTriggerKind()
    {
        var b = _cfg.Cfg.Trigger.Button.ToLowerInvariant();
        return b switch
        {
            "xbutton2" or "latéral" => TriggerKind.X2,
            "right-long" or "clic-droit-long" => TriggerKind.RightLong,
            _ => TriggerKind.Middle,
        };
    }


    private bool ShouldOpen()
    {
        var cfg = _cfg.Cfg;
        var fg = Win32.GetForegroundWindow();
        var proc = Win32.GetProcessNameFromHwnd(fg);
        if (!string.IsNullOrEmpty(proc) &&
            cfg.DisabledProcesses.Contains(proc, StringComparer.OrdinalIgnoreCase))
            return false;
        if (cfg.SkipFullscreen && Win32.IsForegroundFullscreen()) return false;
        return true;
    }

    private void RightTimerFired()
    {
        _rightTimer.Stop();
        if (!_rightArmed || _open) return;
        _rightArmed = false;
        _rightCaptured = true;
        _triggerKind = TriggerKind.RightLong;
        Win32.GetCursorPos(out var pt);
        BeginGesture(pt.X, pt.Y);
    }

    private void CancelRight()
    {
        _rightTimer.Stop();
        _rightArmed = false;
    }

    private void BeginGesture(int px, int py)
    {
        _pressTime = Environment.TickCount64;
        _draggedOut = false;
        _leftDeadzone = false;
        _popArmed = false;
        _hover = -1;

        var fg = Win32.GetForegroundWindow();
        _ctx = new ContextInfo
        {
            ProcessName = Win32.GetProcessNameFromHwnd(fg),
            WindowClass = Win32.GetClassName(fg),
        };

        var (_, menu) = _resolver.Resolve(_ctx);
        Log.Info($"Roue ouverte à ({px},{py}) — appli « {_ctx.ProcessName} », menu {menu.Items.Count} options");
        OpenWheel(menu, px, py);

        // raffiner avec la détection complète (fichiers, texte, liens) si l'utilisateur n'a pas encore navigué
        _detect.Enqueue(fg, px, py, ctx => OnContextDetected(ctx));
    }

    private void OnContextDetected(ContextInfo ctx)
    {
        if (!_open || _draggedOut || _frames.Count == 0 || _frames.Count > 1) return;

        var (_, menu) = _resolver.Resolve(ctx);
        if (!ReferenceEquals(menu, _frames[0].Menu))
        {
            _ctx = ctx;
            _frames[0] = new WheelFrame { Menu = menu, Center = _frames[0].Center };
            _renderer.SetFrames(_frames);
            _renderer.BeginAnim(WheelAnim.Open, 110);
        }
        else
        {
            _ctx = ctx; // même roue, on garde le contexte enrichi pour les actions
        }
    }

    private void OpenWheel(MenuDef menu, int px, int py)
    {
        _renderer.Configure(_cfg.Cfg); // lit l'accent/taille à jour
        _scale = Win32.GetScaleForPoint(px, py);
        double half = _renderer.ElementHalf;
        Width = Height = half * 2;
        Left = px / _scale - half;
        Top = py / _scale - half;
        var center = new Point(half, half);

        _frames.Clear();
        _frames.Add(new WheelFrame { Menu = menu, Center = center });
        _renderer.SetFrames(_frames);
        _renderer.BeginAnim(WheelAnim.Open, 110);

        _open = true;
        Visibility = Visibility.Visible;
        UpdatePointer(px, py);
    }

    private void UpdatePointer(int px, int py)
    {
        if (!_open || _frames.Count == 0) return;
        var p = new Point(px / _scale - Left, py / _scale - Top);
        var top = _frames[^1];
        if (top.Menu.Items.Count == 0) return;
        long now = Environment.TickCount64;
        double radius = _renderer.Radius;
        double dz = _renderer.Deadzone;

        double dist = Math.Sqrt(Math.Pow(p.X - top.Center.X, 2) + Math.Pow(p.Y - top.Center.Y, 2));
        if (dist > dz)
        {
            _draggedOut = true;
            _leftDeadzone = true;
            _popArmed = false;
        }

        int idx = WheelRenderer.HitIndex(top, p, radius);
        if (idx < 0 || idx >= top.Menu.Items.Count) return; // défense en profondeur
        if (idx != _hover)
        {
            _hover = idx;
            _hoverSince = now;
        }

        // ouvrir un sous-menu après un court survol (≈ 80 ms)
        if (idx >= 0)
        {
            var item = top.Menu.Items[idx];
            if (item.Children is { Count: > 0 } && now - _hoverSince > 80 && dist > dz * 1.15)
            {
                Push(item, idx);
                _renderer.SetPointer(p, -1);
                return;
            }
        }

        // revenir au niveau parent : rester dans la zone morte > 110 ms
        if (idx < 0 && _frames.Count > 1 && _leftDeadzone)
        {
            if (!_popArmed) { _popArmed = true; _deadzoneSince = now; }
            else if (now - _deadzoneSince > 110)
            {
                Pop();
                _renderer.SetPointer(p, -1);
                return;
            }
        }
        else _popArmed = false;

        _renderer.SetPointer(p, _hover);
    }

    private void Push(MenuItem parent, int index)
    {
        var top = _frames[^1];
        var pos = WheelRenderer.ItemPosition(top, index, _renderer.Radius);

        var child = new WheelFrame
        {
            Menu = new MenuDef { Label = parent.Label, Items = parent.Children! },
            ParentItem = parent,
            Center = ClampCenter(pos),
        };
        _frames.Add(child);

        _hover = -1;
        _leftDeadzone = false;
        _popArmed = false;
        _draggedOut = true;
        _renderer.BeginAnim(WheelAnim.Push, 110);
    }

    private void Pop()
    {
        if (_frames.Count <= 1) return;
        _frames.RemoveAt(_frames.Count - 1);
        _hover = -1;
        _leftDeadzone = false;
        _popArmed = false;
        _renderer.BeginAnim(WheelAnim.Pop, 100);
    }

    private Point ClampCenter(Point c)
    {
        double half = _renderer.ElementHalf;
        double margin = _renderer.Radius + 12;
        double x = Math.Clamp(c.X, margin, half * 2 - margin);
        double y = Math.Clamp(c.Y, margin, half * 2 - margin);
        return new Point(x, y);
    }

    private void FinishGesture(int px, int py, bool sticky = false)
    {
        if (!_open || _frames.Count == 0)
        {
            ForceClose();
            return;
        }

        var p = new Point(px / _scale - Left, py / _scale - Top);
        var top = _frames[^1];
        if (top.Menu.Items.Count == 0)
        {
            CloseWheel();
            return;
        }
        int idx = sticky ? _hover : WheelRenderer.HitIndex(top, p, _renderer.Radius);

        bool wasQuick = !sticky
            && Environment.TickCount64 - _pressTime < Math.Clamp(_cfg.Cfg.Trigger.QuickClickMs, 60, 600)
            && !_draggedOut;

        MenuItem? toRun = null;

        if (idx >= 0 && idx < top.Menu.Items.Count)
        {
            var item = top.Menu.Items[idx];
            if (item.Children is { Count: > 0 })
                toRun = item.Action is null ? null : item; // relâchement rapide sur un parent : son action directe
            else
                toRun = item;
        }
        else if (_frames.Count > 1)
        {
            toRun = top.ParentItem is { Action: not null } parent ? parent : null;
        }

        CloseWheel();

        if (toRun is not null)
        {
            Log.Info($"Relâchement sur « {toRun.Label} » → action {toRun.Action?.Kind ?? "aucune"}");
            _exec.Run(toRun, _ctx);
        }
        else if (wasQuick && _cfg.Cfg.Trigger.QuickClickPassthrough && _triggerKind != TriggerKind.RightLong)
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                if (_triggerKind == TriggerKind.X2) InputSimulator.SynthesizeX2Click();
                else InputSimulator.SynthesizeMiddleClick();
            });
    }

    private void CloseWheel()
    {
        _open = false;
        _sticky = false;
        _rightCaptured = false;
        CancelRight();
        var frames = _frames;
        _renderer.BeginAnim(WheelAnim.Close, 90, () =>
        {
            // un nouveau geste a pu démarrer pendant le fondu de fermeture : ne rien casser
            if (_open) return;
            frames.Clear();
            _renderer.SetFrames(frames);
            Visibility = Visibility.Hidden;
        });
    }

    public void ForceClose()
    {
        _open = false;
        _sticky = false;
        _rightCaptured = false;
        CancelRight();
        _frames.Clear();
        _renderer.SetFrames(_frames);
        Visibility = Visibility.Hidden;
    }

    /// <summary>Ouvre la roue en mode « test » (clic gauche = valider), depuis le systray.</summary>
    public void DebugOpenAtCursor()
    {
        if (_open) return;
        Win32.GetCursorPos(out var pt);
        _pressTime = Environment.TickCount64 - 10_000; // jamais un « clic rapide »
        _sticky = true;
        var fg = Win32.GetForegroundWindow();
        _ctx = new ContextInfo { ProcessName = Win32.GetProcessNameFromHwnd(fg) };
        var (_, menu) = _resolver.Resolve(_ctx);
        OpenWheel(menu, pt.X, pt.Y);
        _detect.Enqueue(fg, pt.X, pt.Y, ctx => OnContextDetected(ctx));
    }
}
