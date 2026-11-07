using System.Collections.Concurrent;
using System.Windows.Automation;
using RavenneToolsMenu.Config;
using RavenneToolsMenu.Core;

namespace RavenneToolsMenu.Detection;

/// <summary>
/// Worker STA dédié aux appels COM/UI Automation (rapides mais bloquants) :
/// la roue s'ouvre immédiatement avec le contexte rapide (processus), et se
/// raffine si la détection complète arrive avant que l'utilisateur navigue.
/// </summary>
public sealed class DetectionWorker : IDisposable
{
    private readonly ConfigManager _cfg;
    private readonly BlockingCollection<(nint hwnd, int px, int py, int seq, Action<ContextInfo> done)> _queue = new(8);
    private readonly Thread _thread;
    private int _seq;

    public DetectionWorker(ConfigManager cfg)
    {
        _cfg = cfg;
        _thread = new Thread(Loop) { IsBackground = true, Name = "RavenneDetection" };
        _thread.SetApartmentState(System.Threading.ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Lance la détection ; le résultat arrive sur le thread UI via le callback.</summary>
    public int Enqueue(nint hwnd, int px, int py, Action<ContextInfo> done)
    {
        var seq = System.Threading.Interlocked.Increment(ref _seq);
        try { _queue.Add((hwnd, px, py, seq, done)); } catch { }
        return seq;
    }

    private void Loop()
    {
        try
        {
            foreach (var job in _queue.GetConsumingEnumerable())
            {
                ContextInfo ctx;
                try { ctx = Detect(job.hwnd, job.px, job.py); }
                catch (Exception ex)
                {
                    Log.Warn("Détection échouée : " + ex.Message);
                    ctx = new ContextInfo();
                }
                try { job.done(ctx); } catch { }
            }
        }
        catch (ObjectDisposedException) { }
    }

    private ContextInfo Detect(nint hwnd, int px, int py)
    {
        var ctx = new ContextInfo
        {
            ProcessName = Win32.GetProcessNameFromHwnd(hwnd),
            WindowClass = Win32.GetClassName(hwnd),
        };

        if (string.IsNullOrEmpty(ctx.ProcessName)) return ctx;
        if (_cfg.Cfg.DisabledProcesses.Contains(ctx.ProcessName, StringComparer.OrdinalIgnoreCase)) return ctx;

        // Fichiers sélectionnés dans l'Explorateur.
        if (ctx.WindowClass is "CabinetWClass" or "ExploreWClass")
            ExplorerSelection.TryGetPaths(hwnd, ctx.Files);

        // Texte sélectionné dans l'élément ayant le focus (UI Automation).
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused?.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj) == true)
            {
                var ranges = ((TextPattern)patternObj).GetSelection();
                if (ranges is { Length: > 0 })
                {
                    var text = ranges[0].GetText(4096) ?? "";
                    text = text.Replace("\r", " ").Replace("\n", " ").Trim();
                    if (text.Length > 0) ctx.SelectedText = text;
                }
            }
        }
        catch { }

        // Lien / image sous le curseur (expérimental, dépend de l'accessibilité du navigateur).
        try
        {
            var el = AutomationElement.FromPoint(new System.Windows.Point(px, py));
            int climb = 0;
            while (el is not null && climb < 7)
            {
                var type = el.Current.ControlType;
                if (type == ControlType.Hyperlink)
                {
                    ctx.LinkUrl = el.Current.Name;
                    break;
                }
                if (type == ControlType.Image)
                {
                    ctx.ImageName = el.Current.Name;
                    break;
                }
                el = System.Windows.Automation.TreeWalker.RawViewWalker.GetParent(el);
                climb++;
            }
        }
        catch { }

        return ctx;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        try { _thread.Join(600); } catch { }
        _queue.Dispose();
    }
}
