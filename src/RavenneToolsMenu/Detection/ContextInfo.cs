namespace RavenneToolsMenu.Detection;

/// <summary>Instantané du contexte au moment de l'ouverture de la roue.</summary>
public sealed class ContextInfo
{
    public string ProcessName { get; set; } = "";
    public string WindowClass { get; set; } = "";
    /// <summary>Chemins complets sélectionnés dans l'Explorateur.</summary>
    public List<string> Files { get; set; } = new();
    /// <summary>Texte sélectionné dans l'application active (via UI Automation).</summary>
    public string SelectedText { get; set; } = "";
    /// <summary>URL du lien sous le curseur (expérimental, navigateurs).</summary>
    public string LinkUrl { get; set; } = "";
    /// <summary>Nom de l'image sous le curseur (expérimental).</summary>
    public string ImageName { get; set; } = "";
}
