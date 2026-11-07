using RavenneToolsMenu.Config;

namespace RavenneToolsMenu.Detection;

/// <summary>Choisit la roue à afficher selon les règles de la configuration.</summary>
public sealed class MenuResolver
{
    private const int MaxItems = 12;
    private readonly ConfigManager _cfg;

    public MenuResolver(ConfigManager cfg) => _cfg = cfg;

    public (string Id, MenuDef Menu) Resolve(ContextInfo? ctx)
    {
        if (ctx is not null)
        {
            foreach (var rule in _cfg.Cfg.Rules)
            {
                if (Matches(rule.Match, ctx))
                    return Normalize(rule.Menu);
            }
        }
        return Normalize("principal");
    }

    private static bool Matches(RuleMatch m, ContextInfo ctx)
    {
        if (m.Processes.Count > 0 &&
            !m.Processes.Contains(ctx.ProcessName, StringComparer.OrdinalIgnoreCase)) return false;

        if ((m.SelectedFiles || m.FileExtensions.Count > 0) && ctx.Files.Count == 0) return false;

        if (m.FileExtensions.Count > 0 &&
            !ctx.Files.Any(f => m.FileExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
            return false;

        if (m.SelectedText && string.IsNullOrWhiteSpace(ctx.SelectedText)) return false;

        return true;
    }

    private (string, MenuDef) Normalize(string id)
    {
        var menus = _cfg.Cfg.Menus;
        MenuDef? menu = null;
        var resolved = id;
        if (!menus.TryGetValue(id, out menu) || menu is null)
        {
            if (menus.TryGetValue("principal", out menu) && menu is not null) resolved = "principal";
            else
            {
                var first = menus.FirstOrDefault();
                menu = first.Value;
                resolved = first.Key;
            }
        }
        menu ??= new MenuDef();
        if (menu.Items.Count > MaxItems)
            menu.Items = menu.Items.Take(MaxItems).ToList();
        return (resolved, menu);
    }
}
