using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using RavenneToolsMenu.Core;

namespace RavenneToolsMenu.Config;

/// <summary>
/// Charge config.json depuis %APPDATA%\RavenneToolsMenu, le recharge à chaud
/// quand le fichier change (éditeur externe ou bouton du systray).
/// </summary>
public sealed class ConfigManager
{
    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RavenneToolsMenu");
    public string ConfigPath => Path.Combine(AppDataDir, "config.json");

    public AppConfig Cfg { get; private set; } = new();
    public bool IsFirstRun { get; private set; }

    /// <summary>Incrémenté à chaque rechargement — permet aux services de relire leurs réglages sans abonnement.</summary>
    public int Generation { get; private set; }

    public event Action? Reloaded;
    public event Action<string>? ConfigFailed;

    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _debounce;
    private DateTime _lastSelfSave = DateTime.MinValue;

    private ConfigManager() { }

    public static ConfigManager Load()
    {
        Directory.CreateDirectory(AppDataDir);
        var m = new ConfigManager();
        m.IsFirstRun = !File.Exists(m.ConfigPath);
        m.Cfg = m.ReadFromDisk() ?? m.WriteDefault();
        m.StartWatch();
        return m;
    }

    /// <summary>Chargement sans surveillance (mode --validate).</summary>
    public static ConfigManager LoadForValidation()
    {
        Directory.CreateDirectory(AppDataDir);
        var m = new ConfigManager();
        m.IsFirstRun = !File.Exists(m.ConfigPath);
        m.Cfg = m.ReadFromDisk() ?? m.WriteDefault();
        return m;
    }

    private AppConfig? ReadFromDisk()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return null;
            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, ConfigJson.Options);
            if (cfg is null || cfg.Menus.Count == 0)
            {
                Log.Error("Configuration vide ou invalide.");
                ConfigFailed?.Invoke("Configuration vide ou invalide — valeurs par défaut conservées.");
                return null;
            }
            return cfg;
        }
        catch (Exception ex)
        {
            Log.Error("Lecture de config.json impossible", ex);
            ConfigFailed?.Invoke("config.json illisible : " + ex.Message);
            return null;
        }
    }

    private AppConfig WriteDefault()
    {
        try
        {
            File.WriteAllText(ConfigPath, DefaultConfig.Json);
            return JsonSerializer.Deserialize<AppConfig>(DefaultConfig.Json, ConfigJson.Options) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            Log.Error("Écriture de la config par défaut impossible", ex);
            return new AppConfig();
        }
    }

    private void StartWatch()
    {
        _watcher = new FileSystemWatcher(AppDataDir, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Renamed += (_, _) => ScheduleReload();
        _watcher.Created += (_, _) => ScheduleReload();
    }

    private void ScheduleReload()
    {
        // Le callback arrive sur un thread IO → repasser sur le thread UI.
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is null) return;
        app.Dispatcher.BeginInvoke(() =>
        {
            if (DateTime.UtcNow - _lastSelfSave < TimeSpan.FromSeconds(2)) return; // nos propres sauvegardes
            _debounce ??= new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(400),
            };
            _debounce.Tick -= DebounceTick;
            _debounce.Tick += DebounceTick;
            _debounce.Stop();
            _debounce.Start();
        });
    }

    private void DebounceTick(object? sender, EventArgs e)
    {
        _debounce!.Stop();
        ReloadFromDisk();
    }

    /// <summary>Recharge la config depuis le disque (bouton systray / action « Recharger »).</summary>
    public bool ReloadFromDisk()
    {
        var cfg = ReadFromDisk();
        if (cfg is null) return false;
        Cfg = cfg;
        Generation++;
        Reloaded?.Invoke();
        return true;
    }

    /// <summary>Sauvegarde atomique de la configuration courante.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Cfg, ConfigJson.Options));
            if (File.Exists(ConfigPath)) File.Replace(tmp, ConfigPath, null);
            else File.Move(tmp, ConfigPath);
            _lastSelfSave = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Log.Error("Sauvegarde de la configuration impossible", ex);
        }
    }

    /// <summary>Modifie puis sauvegarde (réglages du systray).</summary>
    public void MutateAndSave(Action<AppConfig> change)
    {
        change(Cfg);
        Save();
        Generation++;
        Reloaded?.Invoke();
    }
}
