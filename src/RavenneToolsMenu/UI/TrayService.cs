using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using RavenneToolsMenu.Config;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace RavenneToolsMenu.UI;

/// <summary>Icône de notification + menu de réglages rapides (déclencheur, accent, taille, démarrage auto…).</summary>
public sealed class TrayService : IDisposable
{
    private static readonly string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "RavenneToolsMenu";

    private readonly ConfigManager _cfg;
    private readonly WheelWindow _wheel;
    private readonly ToastHost _toasts;
    private readonly Action<bool> _checkForUpdate;
    private WinForms.NotifyIcon? _icon;

    private static readonly (string Label, string Value)[] Triggers =
    {
        ("Molette", "middle"),
        ("Ctrl + Molette", "ctrl+middle"),
        ("Maj + Molette", "shift+middle"),
        ("Alt + Molette", "alt+middle"),
        ("Bouton latéral avant", "xbutton2"),
        ("Clic droit long", "right-long"),
    };

    private static readonly (string Label, string Hex)[] Accents =
    {
        ("Violet", "#7C4DFF"), ("Bleu", "#0078D7"), ("Cyan", "#00B7C3"), ("Vert", "#107C10"),
        ("Orange", "#CA5010"), ("Rose", "#E3008C"), ("Rouge", "#E81123"), ("Ambre", "#FFB900"),
    };

    private static readonly (string Label, int Radius)[] Sizes =
    {
        ("Petite", 120), ("Moyenne", 150), ("Grande", 185), ("Très grande", 215),
    };

    public TrayService(ConfigManager cfg, WheelWindow wheel, ToastHost toasts, Action<bool> checkForUpdate)
    {
        _cfg = cfg;
        _wheel = wheel;
        _toasts = toasts;
        _checkForUpdate = checkForUpdate;
    }

    public void Start()
    {
        _icon = new WinForms.NotifyIcon
        {
            Text = "Ravenne's Tools Menu",
            Icon = LoadIcon(),
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _wheel.DebugOpenAtCursor();
        RebuildMenu();
    }

    public void RebuildMenu()
    {
        if (_icon is null) return;
        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = BuildMenu();
        old?.Dispose();
    }

    private WinForms.ContextMenuStrip BuildMenu()
    {
        var menu = new WinForms.ContextMenuStrip { Renderer = new DarkRenderer(), ShowImageMargin = false, Font = new Drawing.Font("Segoe UI", 9.5f) };
        var fg = Drawing.Color.FromArgb(235, 235, 245);
        var fgDim = Drawing.Color.FromArgb(150, 150, 165);

        WinForms.ToolStripMenuItem Item(string text, EventHandler onClick, bool check = false, bool enabled = true)
        {
            var it = new WinForms.ToolStripMenuItem(text) { ForeColor = fg, Checked = check, Enabled = enabled };
            it.Click += onClick;
            return it;
        }

        menu.Items.Add(Item("Tester la roue", (_, _) => _wheel.DebugOpenAtCursor()));

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(TriggerMenu(fg, fgDim));
        menu.Items.Add(AccentMenu(fg, fgDim));
        menu.Items.Add(SizeMenu(fg, fgDim));

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Item("Animations", (_, _) => _cfg.MutateAndSave(c => c.Appearance.Animations = !c.Appearance.Animations), _cfg.Cfg.Appearance.Animations));
        menu.Items.Add(Item("Étiquettes des options", (_, _) => _cfg.MutateAndSave(c => c.Appearance.ShowItemLabels = !c.Appearance.ShowItemLabels), _cfg.Cfg.Appearance.ShowItemLabels));
        menu.Items.Add(Item("Clic bref transmis à l'appli", (_, _) => _cfg.MutateAndSave(c => c.Trigger.QuickClickPassthrough = !c.Trigger.QuickClickPassthrough), _cfg.Cfg.Trigger.QuickClickPassthrough));
        menu.Items.Add(Item("Désactivé en plein écran (jeux)", (_, _) => _cfg.MutateAndSave(c => c.SkipFullscreen = !c.SkipFullscreen), _cfg.Cfg.SkipFullscreen));
        menu.Items.Add(Item("Démarrage auto avec Windows", (_, _) =>
        {
            bool enable = !GetAutoStart();
            SetAutoStart(enable);
            // coche visuelle rafraîchie au prochain RebuildMenu
            RebuildMenu();
        }, GetAutoStart()));

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Item("Vérifier les mises à jour…", (_, _) => _checkForUpdate(true)));
        menu.Items.Add(Item("Vérifier les mises à jour au démarrage", (_, _) =>
            _cfg.MutateAndSave(c => c.Updates.Enabled = !c.Updates.Enabled), _cfg.Cfg.Updates.Enabled));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Item("Ouvrir la configuration (JSON)", (_, _) =>
            Process.Start(new ProcessStartInfo("notepad.exe", Quote(_cfg.ConfigPath)) { UseShellExecute = true })));
        menu.Items.Add(Item("Recharger la configuration", (_, _) => _cfg.ReloadFromDisk()));
        menu.Items.Add(Item("Dossier des journaux", (_, _) =>
        {
            var dir = Path.Combine(ConfigManager.AppDataDir, "logs");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", Quote(dir)) { UseShellExecute = true });
        }));

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Item("À propos", (_, _) =>
            _toasts.ShowInfo("Ravenne's Tools Menu v1.0 — roue d'outils contextuelle. Config : %APPDATA%\\RavenneToolsMenu", "À propos")));
        menu.Items.Add(Item("Quitter", (_, _) => System.Windows.Application.Current.Shutdown()));

        return menu;
    }

    private WinForms.ToolStripMenuItem TriggerMenu(Drawing.Color fg, Drawing.Color dim)
    {
        var root = new WinForms.ToolStripMenuItem("Déclencheur") { ForeColor = fg };
        var current = _cfg.Cfg.Trigger.Button.ToLowerInvariant();
        foreach (var (label, value) in Triggers)
        {
            var it = new WinForms.ToolStripMenuItem(label) { ForeColor = fg, Checked = current == value };
            it.Click += (_, _) => _cfg.MutateAndSave(c => c.Trigger.Button = value);
            root.DropDownItems.Add(it);
        }
        return root;
    }

    private WinForms.ToolStripMenuItem AccentMenu(Drawing.Color fg, Drawing.Color dim)
    {
        var root = new WinForms.ToolStripMenuItem("Couleur d'accent") { ForeColor = fg };
        var current = _cfg.Cfg.Appearance.Accent.Trim().ToUpperInvariant();
        foreach (var (label, hex) in Accents)
        {
            var it = new WinForms.ToolStripMenuItem(label) { ForeColor = fg, Checked = hex.ToUpperInvariant() == current };
            it.Click += (_, _) => _cfg.MutateAndSave(c => c.Appearance.Accent = hex);
            root.DropDownItems.Add(it);
        }
        return root;
    }

    private WinForms.ToolStripMenuItem SizeMenu(Drawing.Color fg, Drawing.Color dim)
    {
        var root = new WinForms.ToolStripMenuItem("Taille de la roue") { ForeColor = fg };
        var current = _cfg.Cfg.Appearance.Radius;
        foreach (var (label, radius) in Sizes)
        {
            var r = radius;
            var it = new WinForms.ToolStripMenuItem(label) { ForeColor = fg, Checked = current == r };
            it.Click += (_, _) => _cfg.MutateAndSave(c => c.Appearance.Radius = r);
            root.DropDownItems.Add(it);
        }
        return root;
    }

    private static string Quote(string s) => "\"" + s + "\"";

    private static bool GetAutoStart() =>
        Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue(RunName) is not null;

    private static void SetAutoStart(bool enable)
    {
        var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable)
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "RavenneToolsMenu.exe");
            key?.SetValue(RunName, Quote(exe));
        }
        else key?.DeleteValue(RunName, false);
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "ravenne.ico");
            if (File.Exists(icoPath)) return new Drawing.Icon(icoPath);
        }
 catch { }
        return GenerateIcon();
    }

    /// <summary>Icône de repli dessinée à la volée (disque + anneau accent).</summary>
    private static Drawing.Icon GenerateIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new Drawing.SolidBrush(Drawing.Color.FromArgb(27, 27, 35));
            using var accent = new Drawing.Pen(Drawing.Color.FromArgb(124, 77, 255), 2.4f);
            using var dot = new Drawing.SolidBrush(Drawing.Color.FromArgb(235, 235, 245));
            g.FillEllipse(bg, 1, 1, 30, 30);
            g.DrawEllipse(accent, 4.5f, 4.5f, 23, 23);
            for (int i = 0; i < 6; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 3;
                g.FillEllipse(dot, (float)(15 + 9.5 * Math.Cos(a) - 1.6), (float)(15 + 9.5 * Math.Sin(a) - 1.6), 3.2f, 3.2f);
            }
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }

    /// <summary>Menu contextuel sombre minimal.</summary>
    private sealed class DarkRenderer : WinForms.ToolStripProfessionalRenderer
    {
        private sealed class DarkColors : WinForms.ProfessionalColorTable
        {
            private static Drawing.Color Bg => Drawing.Color.FromArgb(24, 24, 33);
            private static Drawing.Color Border => Drawing.Color.FromArgb(48, 48, 62);
            private static Drawing.Color Sel => Drawing.Color.FromArgb(46, 42, 66);

            public override Drawing.Color ToolStripDropDownBackground => Bg;
            public override Drawing.Color ImageMarginGradientBegin => Bg;
            public override Drawing.Color ImageMarginGradientMiddle => Bg;
            public override Drawing.Color ImageMarginGradientEnd => Bg;
            public override Drawing.Color MenuBorder => Border;
            public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(90, 80, 160);
            public override Drawing.Color MenuItemSelected => Sel;
            public override Drawing.Color MenuItemSelectedGradientBegin => Sel;
            public override Drawing.Color MenuItemSelectedGradientEnd => Sel;
            public override Drawing.Color MenuItemPressedGradientBegin => Sel;
            public override Drawing.Color MenuItemPressedGradientEnd => Sel;
            public override Drawing.Color SeparatorDark => Border;
            public override Drawing.Color SeparatorLight => Border;
            public override Drawing.Color ToolStripBorder => Border;
            public override Drawing.Color StatusStripBorder => Border;
        }

        public DarkRenderer() : base(new DarkColors()) { }

        protected override void OnRenderArrow(WinForms.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Drawing.Color.FromArgb(160, 160, 175);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(WinForms.ToolStripItemImageRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(150, 120, 255), 2f);
            var r = e.ImageRectangle;
            e.Graphics.DrawLine(pen, r.Left + 3, r.Top + r.Height / 2, r.Left + r.Width / 2, r.Bottom - 4);
            e.Graphics.DrawLine(pen, r.Left + r.Width / 2, r.Bottom - 4, r.Right - 2, r.Top + 3);
        }
    }
}
