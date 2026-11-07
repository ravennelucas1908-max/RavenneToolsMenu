using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RavenneToolsMenu.Config;

namespace RavenneToolsMenu.UI;

/// <summary>
/// Palette de la roue : couleurs dérivées de l'accent, pinceaux gelés (zéro allocation au rendu).
/// </summary>
internal sealed class Theme
{
    public Color Accent;
    public Brush AccentBrush = null!;
    public Brush AccentSoftBrush = null!;   // accent ~20 %
    public Brush AccentGlowBrush = null!;   // accent ~7 %
    public Brush AccentTrailBrush = null!;  // accent ~35 %
    public Pen AccentPen = null!;           // accent ~90 %
    public Pen AccentSoftPen = null!;       // accent ~50 %

    public Brush BackdropBrush = null!;     // disque principal
    public Brush BackdropInnerBrush = null!;

    public Brush HubBrush = null!;
    public Pen HubPen = null!;
    public Pen HairPen = null!;             // contour discret blanc
    public Pen IconPen = null!;             // icônes (blanc)
    public Brush WhiteBrush = null!;
    public Brush SubBrush = null!;

    public Typeface LabelTypeface = null!;
    public Typeface SubTypeface = null!;

    public double GlobalOpacity = 0.97;

    private Theme() { }

    public static Theme Build(AppearanceConfig ap)
    {
        var accent = ParseColor(ap.Accent, Color.FromRgb(0x7C, 0x4D, 0xFF));
        var accentBrush = new SolidColorBrush(accent);

        var t = new Theme
        {
            Accent = accent,
            AccentBrush = Freeze(accentBrush),
            AccentSoftBrush = Freeze(new SolidColorBrush(WithAlpha(accent, 0.22))),
            AccentGlowBrush = Freeze(new SolidColorBrush(WithAlpha(accent, 0.08))),
            AccentTrailBrush = Freeze(new SolidColorBrush(WithAlpha(accent, 0.35))),
            AccentPen = FreezePen(new Pen(new SolidColorBrush(WithAlpha(accent, 0.9)), 1.4)),
            AccentSoftPen = FreezePen(new Pen(new SolidColorBrush(WithAlpha(accent, 0.55)), 1.2)),
            BackdropBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xF2, 0x1B, 0x1B, 0x23))),
            BackdropInnerBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0x13, 0x13, 0x19))),
            HubBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xFA, 0x0F, 0x0F, 0x16))),
            HubPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)), 1.0)),
            HairPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)), 1.0)),
            IconPen = FreezePen(new Pen(Brushes.White, 2.0)),
            WhiteBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF))),
            SubBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xB4, 0xE8, 0xE8, 0xF2))),
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
            var c = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return c;
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
