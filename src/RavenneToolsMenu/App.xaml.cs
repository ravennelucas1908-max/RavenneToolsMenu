using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using RavenneToolsMenu.Actions;
using RavenneToolsMenu.Config;
using RavenneToolsMenu.Core;
using RavenneToolsMenu.Detection;
using RavenneToolsMenu.UI;

namespace RavenneToolsMenu;

public partial class App : Application
{
    private Mutex? _singleMutex;
    private ConfigManager _cfg = null!;
    private ToastHost _toasts = null!;
    private ActionExecutor _exec = null!;
    private DetectionWorker _detect = null!;
    private MenuResolver _resolver = null!;
    private WheelWindow _wheel = null!;
    private TrayService _tray = null!;
    private MouseHookService _hook = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleMutex = new Mutex(true, @"Local\RavenneToolsMenu_SingleInstance", out bool first);
        if (!first)
        {
            Shutdown(2);
            return;
        }

        // Mode sans UI : valide la configuration et écrit un rapport.
        var reportArg = e.Args.FirstOrDefault(a => a.StartsWith("--report=", StringComparison.OrdinalIgnoreCase));
        if (e.Args.Contains("--validate", StringComparer.OrdinalIgnoreCase))
        {
            Validate(reportArg?["--report=".Length..]);
            return;
        }

        Log.Init();
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error("Exception non gérée", args.Exception);
            try { _toasts?.ShowError("Une erreur interne est survenue — voir le journal.", "Ravenne's Tools Menu"); } catch { }
            args.Handled = true;
        };

        _cfg = ConfigManager.Load();
        _toasts = new ToastHost();
        _exec = new ActionExecutor(_cfg, _toasts);
        _detect = new DetectionWorker(_cfg);
        _resolver = new MenuResolver(_cfg);
        _wheel = new WheelWindow(_cfg, _resolver, _exec, _toasts, _detect);
        _tray = new TrayService(_cfg, _wheel, _toasts, CheckForUpdate);

        _cfg.Reloaded += () =>
        {
            _tray.RebuildMenu();
            _toasts.ShowInfo("Configuration rechargée.", "Ravenne's Tools Menu");
        };

        _tray.Start();
        _hook = new MouseHookService(_cfg, _wheel);
        _hook.Start();

        // vérification de mise à jour automatique (une fois par 24 h, après stabilisation du démarrage)
        if (_cfg.Cfg.Updates.Enabled)
        {
            var firstCheck = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            { Interval = TimeSpan.FromSeconds(20) };
            firstCheck.Tick += (_, _) => { firstCheck.Stop(); CheckForUpdate(manual: false); };
            firstCheck.Start();
        }

        if (_cfg.IsFirstRun)
            _toasts.ShowInfo("Cliquez (ou maintenez) le bouton du milieu de la souris pour ouvrir la roue.", "Ravenne's Tools Menu est prêt");

        Log.Info("Démarrage terminé.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _hook?.Stop();
            _tray?.Dispose();
            _detect?.Dispose();
            _toasts?.CloseAll();
            _wheel?.ForceClose();
        }
        catch { }
        try { _singleMutex?.ReleaseMutex(); } catch { }
        Log.Info("Sortie de l'application.");
        base.OnExit(e);
    }

    /// <summary>Vérifie les mises à jour (GitHub Releases). manual = déclenché par l'utilisateur.</summary>
    public void CheckForUpdate(bool manual)
    {
        var updates = _cfg.Cfg.Updates;
        if (!manual && !updates.Enabled) return;
        if (!manual && (DateTime.UtcNow - updates.LastCheckUtc).TotalHours < 24) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var update = await Core.UpdateChecker.CheckAsync(updates.Repo);
                updates.LastCheckUtc = DateTime.UtcNow;
                _cfg.Save();

                Dispatcher.BeginInvoke(() =>
                {
                    if (update is null)
                    {
                        if (manual) _toasts.ShowInfo($"Vous êtes à jour (v{Core.UpdateChecker.CurrentVersion}).", "Mises à jour");
                        return;
                    }
                    if (!manual && update.Version.ToString() == _cfg.Cfg.Updates.SkipVersion) return;
                    new UpdateWindow(_cfg, update).Show();
                });
            }
            catch (Exception ex)
            {
                Core.Log.Error("Vérification de mise à jour échouée", ex);
                if (manual)
                    Dispatcher.BeginInvoke(() => _toasts.ShowError("Vérification impossible : " + ex.Message, "Mises à jour"));
            }
        });
    }

    private void Validate(string? reportPath)
    {
        var path = reportPath ?? Path.Combine(ConfigManager.AppDataDir, "validate-report.txt");
        var lines = new List<string>();
        try
        {
            Directory.CreateDirectory(ConfigManager.AppDataDir);
            var cfg = ConfigManager.LoadForValidation();
            var resolver = new MenuResolver(cfg);
            var samples = new (string nom, ContextInfo ctx)[]
            {
                ("Aucun contexte", new ContextInfo()),
                ("Navigateur (chrome)", new ContextInfo { ProcessName = "chrome" }),
                ("Fichiers image sélectionnés", new ContextInfo { Files = new List<string> { @"C:\Img\photo.jpg" } }),
                ("Fichiers quelconques", new ContextInfo { Files = new List<string> { @"C:\Doc\rapport.pdf" } }),
                ("Texte sélectionné", new ContextInfo { SelectedText = "bonjour le monde" }),
            };
            foreach (var (nom, ctx) in samples)
            {
                var (id, menu) = resolver.Resolve(ctx);
                lines.Add($"[{nom}] -> menu « {id} » : {menu.Items.Count} options — {string.Join(", ", menu.Items.Take(12).Select(i => i.Label))}");
            }
            lines.Add("OK : configuration valide.");
            File.WriteAllLines(path, lines);
            Console.WriteLine(string.Join(Environment.NewLine, lines));
        }
        catch (Exception ex)
        {
            File.WriteAllLines(path, new[] { "ERREUR : " + ex.Message });
            Console.WriteLine("ERREUR : " + ex.Message);
        }
        Shutdown(0);
    }
}
