using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RavenneToolsMenu.Config;

namespace RavenneToolsMenu.UI;

/// <summary>Un niveau de roue (root ou sous-menu), ancré à un point de l'élément.</summary>
public sealed class WheelFrame
{
    public required MenuDef Menu { get; init; }
    public MenuItem? ParentItem { get; init; }
    public Point Center { get; set; }
    public bool IsRoot => ParentItem is null;
}

public enum WheelAnim { None, Open, Push, Pop, Close }

/// <summary>
/// Rendu de la roue — design sobre « verre dépoli » :
/// vignette à peine visible, icônes dans de petites pastilles givrées,
/// information au centre. Redessiné uniquement pendant animations/sélection.
/// </summary>
public sealed class WheelRenderer : FrameworkElement
{
    private List<WheelFrame> _frames = new();
    private Point _pointer;
    private int _hover = -1;

    private Theme _theme = null!;
    private double _radius = 150;
    private double _iconSize = 26;
    private bool _labels = false;
    private bool _animations = true;

    private WheelAnim _anim = WheelAnim.None;
    private long _animStart;
    private long _animDur;
    private Action? _animDone;
    private bool _rendering;

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<MenuItem, object> _iconCache = new();

    public double Radius => _radius;
    public double Deadzone => _radius * 0.22;
    public double ElementHalf => _radius * 2.05;

    public void Configure(AppConfig cfg)
    {
        var ap = cfg.Appearance;
        _theme = Theme.Build(ap);
        _radius = Math.Clamp(ap.Radius, 100, 220);
        _iconSize = Math.Clamp(ap.IconSize, 18, 40);
        _labels = ap.ShowItemLabels;
        _animations = ap.Animations;
        InvalidateVisual();
    }

    public void SetFrames(List<WheelFrame> frames)
    {
        _frames = frames;
        _hover = -1;
        InvalidateVisual();
    }

    public void SetPointer(Point pointer, int hover)
    {
        if (_pointer == pointer && _hover == hover) return;
        _pointer = pointer;
        _hover = hover;
        InvalidateVisual();
    }

    public void BeginAnim(WheelAnim kind, int durationMs, Action? done = null)
    {
        _anim = kind;
        _animDur = _animations ? Math.Max(0, durationMs) : 0;
        _animStart = Environment.TickCount64;
        _animDone = done;
        if (!_rendering)
        {
            CompositionTarget.Rendering += OnRendering;
            _rendering = true;
        }
        InvalidateVisual();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_anim == WheelAnim.None) return;
        if (Environment.TickCount64 - _animStart >= _animDur)
        {
            var done = _animDone;
            _anim = WheelAnim.None;
            _animDone = null;
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
            done?.Invoke();
        }
        InvalidateVisual();
    }

    // ---------- géométrie partagée avec le contrôleur ----------

    public static int HitIndex(WheelFrame f, Point p, double radius)
    {
        double dx = p.X - f.Center.X, dy = p.Y - f.Center.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < radius * 0.22) return -1;
        int n = f.Menu.Items.Count;
        if (n == 0) return -1;
        double step = 360.0 / n;
        double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI; // -180..180, 0 = droite
        double rel = ((ang + 90) % 360 + 360) % 360;       // 0 = haut, sens horaire
        int idx = (int)Math.Round(rel / step) % n;
        return idx;
    }

    public static Point ItemPosition(WheelFrame f, int index, double radius)
    {
        int n = Math.Max(1, f.Menu.Items.Count);
        double angDeg = -90 + index * (360.0 / n);
        double ring = radius * 0.58;
        double rad = angDeg * Math.PI / 180.0;
        return new Point(f.Center.X + ring * Math.Cos(rad), f.Center.Y + ring * Math.Sin(rad));
    }

    // ---------- rendu ----------

    protected override void OnRender(DrawingContext dc)
    {
        if (_frames.Count == 0 || _theme is null) return;

        double t = _anim == WheelAnim.None ? 1 : Math.Clamp(
            (Environment.TickCount64 - _animStart) / (double)Math.Max(1, _animDur), 0, 1);
        double p = EaseOutCubic(t);

        double globalAlpha = _anim switch
        {
            WheelAnim.Open => p,
            WheelAnim.Close => 1 - p,
            _ => 1,
        };
        double globalScale = _anim switch
        {
            WheelAnim.Open => Lerp(0.88, 1, p),
            WheelAnim.Close => Lerp(1, 0.96, p),
            _ => 1,
        };

        var top = _frames[^1];
        var parent = _frames.Count > 1 ? _frames[^2] : null;

        double topScale = _anim == WheelAnim.Push ? Lerp(0.8, 1, p) : 1;
        double topAlpha = _anim == WheelAnim.Pop ? 1 - p : 1;
        double parentAlpha = parent is null ? 0 : _anim switch
        {
            WheelAnim.Push => Lerp(1, 0.12, p),
            WheelAnim.Pop => Lerp(0.12, 1, p),
            _ => 0.12,
        };

        dc.PushOpacity(globalAlpha * _theme.GlobalOpacity);

        if (parent is not null && parentAlpha > 0.01)
            DrawFrame(dc, parent, parentAlpha, 0.92, ghost: true);

        DrawFrame(dc, top, topAlpha, topScale * globalScale, ghost: false);

        dc.Pop();
    }

    private void DrawFrame(DrawingContext dc, WheelFrame f, double alpha, double scale, bool ghost)
    {
        if (alpha <= 0.01 || f.Menu.Items.Count == 0) return;

        double r = _radius * scale;
        var c = f.Center;
        int n = f.Menu.Items.Count;
        double chipR = _iconSize * 0.82;
        bool isTop = ReferenceEquals(f, _frames[^1]) && !ghost;
        int hoverIdx = isTop ? _hover : -1;

        dc.PushOpacity(alpha);

        // vignette très légère : lisibilité sans plaque opaque
        var vignette = new RadialGradientBrush(
            Color.FromArgb((byte)(ghost ? 0x10 : 0x44), _theme.VignetteColor.R, _theme.VignetteColor.G, _theme.VignetteColor.B),
            Color.FromArgb(0x00, _theme.VignetteColor.R, _theme.VignetteColor.G, _theme.VignetteColor.B));
        vignette.MappingMode = BrushMappingMode.Absolute;
        vignette.Center = c;
        vignette.RadiusX = vignette.RadiusY = r * 1.1;
        vignette.Freeze();
        dc.DrawEllipse(vignette, null, c, r * 1.1, r * 1.1);

        for (int i = 0; i < n; i++)
        {
            var item = f.Menu.Items[i];
            var pos = ItemPosition(f, i, r);
            bool hovered = i == hoverIdx;

            dc.PushTransform(new TranslateTransform(pos.X, pos.Y));

            double dim = hoverIdx >= 0 && !hovered ? 0.55 : 1.0;
            if (dim < 1.0) dc.PushOpacity(dim);

            double cr = chipR * (hovered ? 1.14 : 1.0);

            // halo accent discret sur la pastille survolée
            if (hovered)
            {
                var glow = new RadialGradientBrush(
                    Color.FromArgb(0x38, _theme.Accent.R, _theme.Accent.G, _theme.Accent.B),
                    Color.FromArgb(0x00, _theme.Accent.R, _theme.Accent.G, _theme.Accent.B));
                glow.MappingMode = BrushMappingMode.Absolute;
                glow.Center = new Point(0, 0);
                glow.RadiusX = glow.RadiusY = cr * 2.4;
                glow.Freeze();
                dc.DrawEllipse(glow, null, new Point(0, 0), cr * 2.4, cr * 2.4);
            }

            // pastille givrée
            dc.DrawEllipse(_theme.ChipBrush,
                           hovered ? _theme.ChipHoverPen : _theme.ChipPen,
                           new Point(0, 0), cr, cr);

            // icône
            double iconScale = (_iconSize / 24.0) * (hovered ? 1.05 : 0.92);
            dc.PushTransform(new ScaleTransform(iconScale, iconScale));
            DrawIcon(dc, item, hovered);
            dc.Pop();

            // marqueur de sous-menu : trois points accent sous la pastille
            if (item.Children is { Count: > 0 })
            {
                for (int d = -1; d <= 1; d++)
                    dc.DrawEllipse(_theme.AccentDotBrush, null,
                        new Point(d * 5.2, cr + 7), 2.1, 2.1);
            }

            // étiquette sous la pastille (option « showItemLabels »)
            if (_labels && !ghost && n <= 8 && !string.IsNullOrEmpty(item.Label))
            {
                var ft = _theme.Text(item.Label, 10.5, sub: true);
                dc.DrawText(ft, new Point(-ft.Width / 2, cr + 9));
            }

            if (dim < 1.0) dc.Pop();
            dc.Pop();
        }

        // moyeu : pastille centrale sobre avec l'info courante
        double hubR = Math.Max(30, r * 0.185);
        dc.DrawEllipse(_theme.HubBrush, _theme.HubPen, c, hubR, hubR);

        string title, sub;
        if (hoverIdx >= 0 && hoverIdx < n)
        {
            title = f.Menu.Items[hoverIdx].Label;
            sub = f.Menu.Items[hoverIdx].Description ?? "";
        }
        else
        {
            title = string.IsNullOrEmpty(f.Menu.Label) ? "Ravenne's Tools" : f.Menu.Label;
            sub = f.IsRoot ? "" : "Glissez vers le centre pour revenir";
        }

        var titleFt = _theme.Text(title, 13.5);
        titleFt.MaxTextWidth = hubR * 1.7;
        double ty = string.IsNullOrEmpty(sub) ? c.Y - titleFt.Height / 2 : c.Y - titleFt.Height - 2;
        dc.DrawText(titleFt, new Point(c.X - titleFt.Width / 2, ty));

        if (!string.IsNullOrEmpty(sub))
        {
            var subFt = _theme.Text(sub, 9.5, sub: true);
            subFt.MaxTextWidth = hubR * 1.75;
            dc.DrawText(subFt, new Point(c.X - subFt.Width / 2, c.Y + 2));
        }

        dc.Pop();
    }

    private void DrawIcon(DrawingContext dc, MenuItem item, bool hovered)
    {
        var source = _iconCache.GetValue(item, it => ResolveIcon(it)!);
        switch (source)
        {
            case Geometry geo:
                dc.DrawGeometry(null, hovered ? _theme.AccentPen : _theme.IconPen, geo);
                break;
            case ImageSource img:
                dc.DrawImage(img, new Rect(-_iconSize / 2.0, -_iconSize / 2.0, _iconSize, _iconSize));
                break;
            case string txt when txt.StartsWith("txt:", StringComparison.Ordinal):
                var emoji = _theme.Text(txt[4..], _iconSize * 0.8);
                dc.DrawText(emoji, new Point(-emoji.Width / 2, -emoji.Height / 2));
                break;
            default:
                var letter = _theme.Text(string.IsNullOrEmpty(item.Label) ? "?" : item.Label[..1].ToUpperInvariant(), 15);
                dc.DrawText(letter, new Point(-letter.Width / 2, -letter.Height / 2));
                break;
        }
    }

    private static object? ResolveIcon(MenuItem item)
    {
        var geo = Icons.Get(item.Icon);
        if (geo is not null) return geo;

        // chemin d'image ?
        if (item.Icon is { } path && path.Length > 3 && (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            && File.Exists(path))
        {
            try
            {
                var bmp = new BitmapImage(new Uri(Path.GetFullPath(path)));
                bmp.Freeze();
                return bmp;
            }
            catch { }
        }

        // emoji / texte court
        if (item.Icon is { Length: > 0 and <= 4 }) return "txt:" + item.Icon;
        return null;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
}
