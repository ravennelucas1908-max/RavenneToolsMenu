using System.IO;
using System.Text;

namespace RavenneToolsMenu.Core;

/// <summary>Journal fichier minimaliste, plafonné en taille.</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string _dir = "";

    public static void Init()
    {
        _dir = Path.Combine(Config.ConfigManager.AppDataDir, "logs");
        Directory.CreateDirectory(_dir);
        IsDebug = File.Exists(Path.Combine(AppContext.BaseDirectory, "debug.flag"))
                  || File.Exists(Path.Combine(_dir, "..", "debug.flag"));
        Info("--- session ---");
        if (IsDebug) Info("Mode diagnostic activé (debug.flag).");
    }

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                var file = Path.Combine(_dir, "ravenne.log");
                if (File.Exists(file) && new FileInfo(file).Length > 256 * 1024)
                    File.Delete(file);
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}\n", Encoding.UTF8);
            }
        }
        catch { }
    }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : $"{msg} :: {ex.GetType().Name}: {ex.Message}");

    /// <summary>Diagnostic détaillé : activé par le fichier « debug.flag » dans le dossier de l'application.</summary>
    public static bool IsDebug { get; private set; }

    public static void EnableDebug(string dir) => IsDebug = true;

    public static void Debug(string msg) { if (IsDebug) Write("DEBUG", msg); }
}
