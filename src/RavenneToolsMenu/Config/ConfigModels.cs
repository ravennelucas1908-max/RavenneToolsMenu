using System.Text.Json;
using System.Text.Json.Serialization;

namespace RavenneToolsMenu.Config;

public class AppConfig
{
    public int Version { get; set; } = 1;

    public TriggerConfig Trigger { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = new();
    public FfmpegConfig Ffmpeg { get; set; } = new();
    public UpdatesConfig Updates { get; set; } = new();

    /// <summary>Ne pas déclencher quand l'application au premier plan est en plein écran (jeux).</summary>
    public bool SkipFullscreen { get; set; } = true;

    /// <summary>Processus où le déclencheur est désactivé (ex. "mstsc" pour le Bureau à distance).</summary>
    public List<string> DisabledProcesses { get; set; } = new();

    /// <summary>Règles ordonnées : la première qui correspond choisit la roue.</summary>
    public List<MenuRule> Rules { get; set; } = new();

    public Dictionary<string, MenuDef> Menus { get; set; } = new();
}

public class TriggerConfig
{
    /// <summary>middle | ctrl+middle | shift+middle | alt+middle | xbutton2 | right-long</summary>
    public string Button { get; set; } = "middle";
    public int LongPressMs { get; set; } = 320;
    public int QuickClickMs { get; set; } = 170;

    /// <summary>Un clic bref sans glisser retransmet le clic d'origine à l'application.</summary>
    public bool QuickClickPassthrough { get; set; } = true;
}

public class AppearanceConfig
{
    public string Accent { get; set; } = "#7C4DFF";
    /// <summary>Rayon de la roue en DIP (110 à 200).</summary>
    public int Radius { get; set; } = 150;
    public int IconSize { get; set; } = 26;
    public bool Animations { get; set; } = true;
    public bool ShowItemLabels { get; set; } = true;
    /// <summary>Opacité globale (0.5 à 1.0).</summary>
    public double Opacity { get; set; } = 0.97;
}

public class FfmpegConfig
{
    /// <summary>Chemin ffmpeg : "ffmpeg" (depuis le PATH) ou chemin complet.</summary>
    public string Path { get; set; } = "ffmpeg";
}

public class UpdatesConfig
{
    /// <summary>Dépôt GitHub source des mises à jour ("owner/nom").</summary>
    public string Repo { get; set; } = "ravennelucas1908-max/RavenneToolsMenu";

    /// <summary>Vérifier automatiquement au démarrage (1 fois / 24 h).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Version ignorée par l'utilisateur ("1.2.0" ou "" ).</summary>
    public string SkipVersion { get; set; } = "";

    /// <summary>Date UTC de la dernière vérification.</summary>
    public DateTime LastCheckUtc { get; set; } = DateTime.MinValue;
}

public class MenuRule
{
    public string Name { get; set; } = "";
    public RuleMatch Match { get; set; } = new();

    /// <summary>Identifiant du menu affiché quand la règle s'applique.</summary>
    public string Menu { get; set; } = "principal";
}

public class RuleMatch
{
    /// <summary>Noms de processus (sans .exe) auxquels la règle s'applique.</summary>
    public List<string> Processes { get; set; } = new();

    /// <summary>Règle active si du texte est sélectionné.</summary>
    public bool SelectedText { get; set; }

    /// <summary>Règle active si des fichiers sont sélectionnés dans l'Explorateur.</summary>
    public bool SelectedFiles { get; set; }

    /// <summary>Si non vide : la règle ne s'applique qu'à ces extensions (".png", …).</summary>
    public List<string> FileExtensions { get; set; } = new();
}

public class MenuDef
{
    public string Label { get; set; } = "";
    public List<MenuItem> Items { get; set; } = new();
}

public class MenuItem
{
    public string Label { get; set; } = "";

    /// <summary>Petite ligne d'aide affichée au centre de la roue.</summary>
    public string? Description { get; set; }

    /// <summary>Nom d'icône vectorielle ("copy", "globe"…), emoji, ou chemin d'image.</summary>
    public string? Icon { get; set; }

    public ActionDef? Action { get; set; }

    /// <summary>Sous-options (roue enfant).</summary>
    public List<MenuItem>? Children { get; set; }

    /// <summary>Message affiché en toast après l'exécution (optionnel).</summary>
    public string? Toast { get; set; }
}

public class ActionDef
{
    /// <summary>
    /// launch | url | keys | media | system | script | convertImage | resizeImage | ffmpeg |
    /// zip | copyPath | recycle | openWith | openTerminalHere | textTransform |
    /// reloadConfig | openConfigFolder
    /// </summary>
    public string Kind { get; set; } = "launch";

    public string? Path { get; set; }
    public string? Args { get; set; }
    public string? Url { get; set; }
    public string? Keys { get; set; }
    public string? Media { get; set; }
    public string? System { get; set; }
    public string? Script { get; set; }
    public bool ShowWindow { get; set; }
    /// <summary>Afficher la sortie du script dans un toast.</summary>
    public bool ToastResult { get; set; }

    // conversion d'images
    public string? ConvertTo { get; set; }     // png|jpg|bmp|gif|tiff|ico
    public int Quality { get; set; } = 90;     // qualité JPEG
    public double Scale { get; set; } = 1.0;   // redimensionnement (0.5 = 50 %)
    public int? MaxWidth { get; set; }         // largeur cible en px

    // ffmpeg
    public string? FfmpegArgs { get; set; }    // gabarit avec {input} {output}

    // copyPath : "full" (chemins complets) | "names" (noms seuls)
    public string? Mode { get; set; }

    // textTransform : upper | lower | title | trim
    public string? TextTransform { get; set; }

    /// <summary>Pause (ms) après fermeture de la roue avant d'exécuter (laisser le focus se stabiliser).</summary>
    public int DelayMs { get; set; } = 40;
}

public static class ConfigJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
