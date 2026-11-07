using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RavenneToolsMenu.UI;

/// <summary>
/// Notifications furtives (bas-droite de l'écran principal). Les fenêtres sont
/// courtes-vives : rien ne tourne quand aucun toast n'est affiché.
/// </summary>
public sealed class ToastHost
{
    public enum Kind { Info, Success, Error }

    private readonly List<ToastWindow> _active = new();

    public void ShowInfo(string message, string title = "") => Show(message, title, Kind.Info);
    public void ShowOk(string message, string title = "") => Show(message, title, Kind.Success);
    public void ShowError(string message, string title = "") => Show(message, title, Kind.Error);

    public void Show(string message, string title, Kind kind)
    {
        var app = Application.Current;
        if (app?.Dispatcher is null) return;
        if (!app.Dispatcher.CheckAccess()) { app.Dispatcher.BeginInvoke(() => Show(message, title, kind)); return; }

        while (_active.Count >= 3)
            _active[0].CloseNow();

        var toast = new ToastWindow(message, title, kind, t =>
        {
            _active.Remove(t);
            Reposition();
        });
        _active.Add(toast);
        Reposition();
        toast.Run();
    }

    private void Reposition()
    {
        var wa = SystemParameters.WorkArea;
        double y = wa.Bottom - 14;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var t = _active[i];
            y -= t.Height + 8;
            t.Left = wa.Right - t.Width - 14;
            t.Top = y;
        }
    }

    public void CloseAll()
    {
        foreach (var t in _active.ToArray()) t.CloseNow();
        _active.Clear();
    }

    private sealed class ToastWindow : Window
    {
        private const double W = 344, H = 66;
        private readonly Action<ToastWindow> _onClosed;
        public bool IsClosed { get; private set; }

        public ToastWindow(string message, string title, Kind kind, Action<ToastWindow> onClosed)
        {
            _onClosed = onClosed;
            Width = W; Height = H;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;

            var bar = kind switch
            {
                Kind.Success => new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0x6E)),
                Kind.Error => new SolidColorBrush(Color.FromRgb(0xE8, 0x4A, 0x4A)),
                _ => new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xFF)),
            };

            var grid = new Grid();
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF4, 0x17, 0x17, 0x1F)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 8, 12, 8),
            };
            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(title))
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = bar,
                    FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                    Margin = new Thickness(0, 0, 0, 2),
                });
            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xF0, 0xEE, 0xEE, 0xF6)),
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 34,
            });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var accentBar = new Border { Background = bar, CornerRadius = new CornerRadius(2) };
            Grid.SetColumn(accentBar, 0);
            Grid.SetColumn(border, 1);
            border.Child = panel;
            grid.Children.Add(accentBar);
            grid.Children.Add(border);
            Content = grid;
        }

        private DispatcherTimer? _timer;

        public void Run()
        {
            Opacity = 0;
            Show();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
            _timer.Tick += (_, _) => BeginClose();
            _timer.Start();
        }

        private void BeginClose()
        {
            _timer?.Stop();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) => CloseNow();
            BeginAnimation(OpacityProperty, fade);
        }

        public void CloseNow()
        {
            if (IsClosed) return;
            IsClosed = true;
            _timer?.Stop();
            Hide();
            Close();
            _onClosed(this);
        }
    }
}
