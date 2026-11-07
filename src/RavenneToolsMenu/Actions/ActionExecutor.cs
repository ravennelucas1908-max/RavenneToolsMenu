using System.Diagnostics;
using System.Globalization;
using RavenneToolsMenu.Config;
using RavenneToolsMenu.Core;
using RavenneToolsMenu.Detection;
using RavenneToolsMenu.UI;

namespace RavenneToolsMenu.Actions;

/// <summary>Exécute les actions de la roue. Appelé après fermeture de la roue.</summary>
public sealed class ActionExecutor
{
    private readonly ConfigManager _cfg;
    private readonly ToastHost _toasts;

    public ActionExecutor(ConfigManager cfg, ToastHost toasts)
    {
        _cfg = cfg;
        _toasts = toasts;
    }

    public void Run(MenuItem item, ContextInfo? ctx)
    {
        if (item.Action is null)
        {
            if (item.Toast is not null) _toasts.ShowInfo(item.Toast, item.Label);
            return;
        }
        Run(item.Action, item, ctx);
    }

    public void Run(ActionDef act, MenuItem? item, ContextInfo? ctx)
    {
        int delay = Math.Clamp(act.DelayMs, 0, 2000);
        _ = Task.Run(async () =>
        {
            if (delay > 0) await Task.Delay(delay);
            try { await Execute(act, item, ctx); }
            catch (Exception ex)
            {
                Log.Error($"Action « {item?.Label ?? act.Kind} » échouée", ex);
                _toasts.ShowError(ex.Message, item?.Label ?? "Action");
            }
        });
    }

    private async Task Execute(ActionDef act, MenuItem? item, ContextInfo? ctx)
    {
        var files = ctx?.Files.Where(File.Exists).ToList() ?? new List<string>();
        switch (act.Kind.ToLowerInvariant())
        {
            case "launch": Launch(act, ctx); break;
            case "url": LaunchUrl(act, ctx); break;
            case "keys": SendKeys(act.Keys ?? ""); break;
            case "media": SendMedia(act.Media ?? ""); break;
            case "system": RunSystem(act.System ?? ""); break;
            case "script": await RunScript(act); break;
            case "convertImage": ConvertImages(files, act); break;
            case "resizeImage": ResizeImages(files, act); break;
            case "ffmpeg": Ffmpeg(files, act); break;
            case "zip": Zip(files); break;
            case "recycle": Recycle(files); break;
            case "copyPath": CopyPath(files, act.Mode); break;
            case "openWith": OpenWith(files); break;
            case "openTerminalHere": OpenTerminalHere(files); break;
            case "textTransform": await TextTransform(act.TextTransform ?? "upper", ctx); break;
            case "reloadConfig":
                if (_cfg.ReloadFromDisk()) _toasts.ShowInfo("Configuration rechargée.", "Ravenne's Tools Menu");
                else _toasts.ShowError("Configuration illisible (voir journal).", "Rechargement");
                break;
            case "openConfigFolder":
                Process.Start(new ProcessStartInfo("explorer.exe", Quote(ConfigManager.AppDataDir)) { UseShellExecute = true });
                break;
            default:
                _toasts.ShowError($"Type d'action inconnu : {act.Kind}", item?.Label ?? "Action");
                break;
        }
        if (act.Kind is not ("script" or "convertImage" or "resizeImage" or "ffmpeg" or "zip" or "recycle") && item?.Toast is not null)
            _toasts.ShowInfo(item.Toast, item.Label);
    }

    // ---------- lancement ----------

    private void Launch(ActionDef act, ContextInfo? ctx)
    {
        var path = Tokens(act.Path ?? "", ctx, false);
        var args = Tokens(act.Args ?? "", ctx, false);
        try
        {
            Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception) when (path.Equals("wt.exe", StringComparison.OrdinalIgnoreCase))
        {
            // Repli : Windows Terminal absent → cmd
            Process.Start(new ProcessStartInfo("cmd.exe", "") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _toasts.ShowError($"Impossible de lancer {path} : {ex.Message}", "Lancement");
        }
    }

    private void LaunchUrl(ActionDef act, ContextInfo? ctx)
    {
        var url = Tokens(act.Url ?? "", ctx, true);
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _toasts.ShowError($"URL impossible : {ex.Message}", "Lancement"); }
    }

    // ---------- clavier / média / système ----------

    private static void SendKeys(string combo)
    {
        if (InputSimulator.TryParseKeys(combo, out var keys))
            InputSimulator.PressKeys(keys);
    }

    private static void SendMedia(string media)
    {
        ushort vk = media.ToLowerInvariant() switch
        {
            "playpause" or "play" or "pause" => 0xB3,
            "stop" => 0xB2,
            "next" => 0xB0,
            "prev" or "previous" => 0xB1,
            "volup" or "volumeup" => 0xAF,
            "voldn" or "volumedown" => 0xAE,
            "mute" or "volumemute" => 0xAD,
            _ => 0,
        };
        if (vk != 0) InputSimulator.TapKey(vk);
    }

    private void RunSystem(string what)
    {
        switch (what.ToLowerInvariant())
        {
            case "lock": Win32.LockWorkStation(); break;
            case "taskmgr": Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); break;
            case "screenshot":
                if (InputSimulator.TryParseKeys("win+shift+s", out var k)) InputSimulator.PressKeys(k);
                break;
            case "showdesktop":
                if (InputSimulator.TryParseKeys("win+d", out var kd)) InputSimulator.PressKeys(kd);
                break;
            case "emptytrash": Win32.SHEmptyRecycleBinW(0, null, 0x1 | 0x4); break; // sans confirmation, sans son
            case "opentrash": Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true }); break;
            case "sleep": Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { UseShellExecute = true }); break;
            case "settings": Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); break;
            case "signout": Process.Start(new ProcessStartInfo("shutdown.exe", "/l") { UseShellExecute = true, CreateNoWindow = true }); break;
            case "clipboardclear": OnUi(() => System.Windows.Clipboard.Clear()); break;
            default: _toasts.ShowError($"Action système inconnue : {what}", "Système"); break;
        }
    }

    // ---------- scripts ----------

    private async Task RunScript(ActionDef act)
    {
        if (string.IsNullOrWhiteSpace(act.Script)) return;
        var script = act.Script;
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "`\"")}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = !act.ShowWindow,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return;
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(20_000)) { p.Kill(); _toasts.ShowError("Script interrompu (timeout 20 s).", "Script"); return; }
            string output = (await outTask).Trim();
            string err = (await errTask).Trim();
            if (act.ToastResult || err.Length > 0)
            {
                var msg = err.Length > 0 ? err : output;
                if (msg.Length > 400) msg = msg[..400] + "…";
                if (err.Length > 0) _toasts.ShowError(msg, "Script");
                else _toasts.ShowInfo(string.IsNullOrEmpty(msg) ? "Terminé." : msg, "Script");
            }
        }
        catch (Exception ex)
        {
            _toasts.ShowError("Script impossible : " + ex.Message, "Script");
        }
    }

    // ---------- fichiers ----------

    private void ConvertImages(List<string> files, ActionDef act)
    {
        var images = files.Where(FileOps.IsImage).ToList();
        if (images.Count == 0) { _toasts.ShowError("Aucune image dans la sélection.", "Convertir"); return; }
        var r = FileOps.ConvertImages(images, act.ConvertTo ?? "png", act.Quality);
        ReportFileOp(r, $"converti(s) en {act.ConvertTo?.ToUpperInvariant()}");
    }

    private void ResizeImages(List<string> files, ActionDef act)
    {
        var images = files.Where(FileOps.IsImage).ToList();
        if (images.Count == 0) { _toasts.ShowError("Aucune image dans la sélection.", "Redimensionner"); return; }
        var r = FileOps.ResizeImages(images, act.Scale, act.MaxWidth);
        ReportFileOp(r, act.MaxWidth is int w ? $"redimensionné(s) à {w} px" : "redimensionné(s)");
    }

    private void Ffmpeg(List<string> files, ActionDef act)
    {
        if (files.Count == 0) { _toasts.ShowError("Aucun fichier sélectionné.", "ffmpeg"); return; }
        _toasts.ShowInfo("Conversion en cours…", "ffmpeg");
        var r = FileOps.FfmpegConvert(files, act.ConvertTo ?? "mp3", act.FfmpegArgs, _cfg.Cfg.Ffmpeg.Path);
        ReportFileOp(r, $"converti(s) en {act.ConvertTo}");
    }

    private void Zip(List<string> files)
    {
        if (files.Count == 0) { _toasts.ShowError("Aucun fichier sélectionné.", "ZIP"); return; }
        var (ok, path, err) = FileOps.ZipFiles(files);
        if (ok) _toasts.ShowInfo($"Archive créée : {Path.GetFileName(path)}", "ZIP");
        else _toasts.ShowError(err, "ZIP");
    }

    private void Recycle(List<string> files)
    {
        if (files.Count == 0) { _toasts.ShowError("Aucun fichier sélectionné.", "Corbeille"); return; }
        var r = FileOps.Recycle(files);
        if (r.Ok > 0) _toasts.ShowInfo($"{r.Ok} fichier(s) envoyé(s) à la corbeille.", "Corbeille");
        else ReportFileOp(r, "supprimé(s)");
    }

    private void ReportFileOp(FileOps.OpResult r, string verb)
    {
        var summary = r.Summary(verb);
        if (r.Errors.Count > 0 && r.Ok == 0) _toasts.ShowError($"{summary} — {r.Errors[0]}", "Fichiers");
        else if (r.Errors.Count > 0) _toasts.ShowError(summary, "Fichiers");
        else _toasts.ShowInfo(summary, "Fichiers");
    }

    private void CopyPath(List<string> files, string? mode)
    {
        if (files.Count == 0) { _toasts.ShowError("Aucun fichier sélectionné.", "Copier"); return; }
        var text = mode == "names"
            ? string.Join(Environment.NewLine, files.Select(Path.GetFileName))
            : string.Join(Environment.NewLine, files);
        SetClipboard(text);
        _toasts.ShowInfo($"{files.Count} chemin(s) copié(s).", "Presse-papiers");
    }

    private void OpenWith(List<string> files)
    {
        if (files.Count == 0) { _toasts.ShowError("Aucun fichier sélectionné.", "Ouvrir avec"); return; }
        Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {Quote(files[0])}")
        { UseShellExecute = true });
    }

    private void OpenTerminalHere(List<string> files)
    {
        var dir = files.Count > 0 ? Path.GetDirectoryName(files[0]) : null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        { _toasts.ShowError("Dossier introuvable.", "Terminal"); return; }
        try
        {
            Process.Start(new ProcessStartInfo("wt.exe", $"-d {Quote(dir)}") { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/k cd /d {Quote(dir)}") { UseShellExecute = true });
        }
    }

    // ---------- transformation du texte sélectionné ----------

    private async Task TextTransform(string mode, ContextInfo? ctx)
    {
        string? backup = GetClipboard();

        string text = ctx?.SelectedText ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            if (InputSimulator.TryParseKeys("ctrl+c", out var copy)) InputSimulator.PressKeys(copy);
            await Task.Delay(180);
            text = GetClipboard() ?? "";
        }
        if (string.IsNullOrWhiteSpace(text)) { _toasts.ShowError("Aucun texte sélectionné.", "Texte"); return; }

        var ti = CultureInfo.GetCultureInfo("fr-FR").TextInfo;
        var transformed = mode.ToLowerInvariant() switch
        {
            "upper" => text.ToUpperInvariant(),
            "lower" => text.ToLowerInvariant(),
            "title" => ti.ToTitleCase(text.ToLowerInvariant()),
            "trim" => text.Trim(),
            _ => text,
        };

        SetClipboard(transformed);
        if (InputSimulator.TryParseKeys("ctrl+v", out var paste)) InputSimulator.PressKeys(paste);
        await Task.Delay(260);
        if (backup is not null) SetClipboard(backup); // restaurer le presse-papiers précédent
    }

    // ---------- helpers ----------

    private static string Tokens(string s, ContextInfo? ctx, bool urlEncode)
    {
        if (!s.Contains('{')) return s;
        var text = ctx?.SelectedText ?? "";
        s = s.Replace("{text}", urlEncode ? Uri.EscapeDataString(text) : text);
        var file = ctx?.Files.FirstOrDefault() ?? "";
        s = s.Replace("{file}", file);
        s = s.Replace("{dir}", Path.GetDirectoryName(file) ?? "");
        return s;
    }

    private static string Quote(string s) => "\"" + s.Trim('"') + "\"";

    private static void OnUi(Action a)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is { } d && !d.CheckAccess()) d.Invoke(a);
        else a();
    }

    private static string? GetClipboard()
    {
        for (int i = 0; i < 3; i++)
        {
            try { return OnUiGet<string?>(() => System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : (string?)null); }
            catch { System.Threading.Thread.Sleep(30); }
        }
        return null;
    }

    private static void SetClipboard(string text)
    {
        for (int i = 0; i < 3; i++)
        {
            try { OnUi(() => System.Windows.Clipboard.SetText(text)); return; }
            catch { System.Threading.Thread.Sleep(30); }
        }
    }

    private static T OnUiGet<T>(Func<T> f)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is { } d && !d.CheckAccess()) return d.Invoke(f);
        return f();
    }
}
