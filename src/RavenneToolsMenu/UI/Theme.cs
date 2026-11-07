using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RavenneToolsMenu.Config;

namespace RavenneToolsMenu.UI;

/// <summary>
/// Palette de la roue : couleurs dérivées de l'accent, pinceaux gelés (zéro allocation au rendu).
/// Design sobre « verre dépoli » : pastilles d'icônes givrées, vignette discrète.
/// </summary>
internal sealed class Theme
{
    public Color Accent;
    public Brush AccentBrush = null!;
    public Brush AccentSoftBrush = null!;   // accent ~75 %
    public Brush AccentDotBrush = null!;    // accent ~95 % (marqueurs sous-menu)
    public Pen AccentPen = null!;           // accent ~90 %

    public Brush HubBrush = null!;
    public Pen HubPen = null!;
    public Pen IconPen = null!;             // icônes (blanc)
    public Brush WhiteBrush = null!;
    public Brush SubBrush = null!;

    // pastilles d'icônes
    public Brush ChipBrush = null!;         // pastille givrée
    public Pen ChipPen = null!;             // contour pastille
    public Pen ChipHoverPen = null!;        // contour pastille survolée (accent)
    public Color VignetteColor;             // teinte de la vignette de fond

    public Typeface LabelTypeface = null!;
    public Typeface SubTypeface = null!;

    public double GlobalOpacity = 0.97;

    private Theme() { }

    public static Theme Build(AppearanceConfig ap)
    {
        var accent = ParseColor(ap.Accent, Color.FromRgb(0x7C, 0x4D, 0xFF));

        var t = new Theme
        {
            Accent = accent,
            AccentBrush = Freeze(new SolidColorBrush(accent)),
            AccentSoftBrush = Freeze(new SolidColorBrush(WithAlpha(accent, 0.75))),
            AccentDotBrush = Freeze(new SolidColorBrush(WithAlpha(accent, 0.95))),
            AccentPen = FreezePen(new Pen(new SolidColorBrush(WithAlpha(accent, 0.9)), 1.4)),

            HubBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xE0, 0x14, 0x14, 0x1B))),
            HubPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 1.0)),
            IconPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF)), 1.9)),
            WhiteBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xF6, 0xFF, 0xFF, 0xFF))),
            SubBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xA6, 0xE9, 0xE9, 0xF4))),

            ChipBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xD6, 0x18, 0x18, 0x22))),
            ChipPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)), 1.0)),
            ChipHoverPen = FreezePen(new Pen(new SolidColorBrush(WithAlpha(accent, 0.9)), 1.6)),
            VignetteColor = Color.FromArgb(0x00, 0x0B, 0x0B, 0x12),

            GlobalOpacity = Math.Clamp(ap.Opacity, 0.5, 1.0),
        };

        var regular = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var semibold = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        t.LabelTypeface = semibold;
        t.SubTypeface = regular;
        return t;
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex.Trim());
        }
        catch { return fallback; }
    }

    private static Color WithAlpha(Color c, double a) =>
        Color.FromArgb((byte)Math.Clamp((int)(a * 255), 0, 255), c.R, c.G, c.B);

    private static Brush Freeze(Brush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    // ---------- texte ----------

    private readonly Dictionary<(string text, double size, bool sub), FormattedText> _textCache = new();

    public FormattedText Text(string s, double size, bool sub = false)
    {
        var key = (s, size, sub);
        if (_textCache.TryGetValue(key, out var ft)) return ft;
        ft = new FormattedText(
            s,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            sub ? SubTypeface : LabelTypeface,
            size,
            sub ? SubBrush : WhiteBrush,
            1.0);
        _textCache[key] = ft;
        return ft;
    }
}
