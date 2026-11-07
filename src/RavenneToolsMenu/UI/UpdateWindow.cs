using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RavenneToolsMenu.Config;
using RavenneToolsMenu.Core;

namespace RavenneToolsMenu.UI;

/// <summary>Fenêtre « nouvelle version disponible » : notes, téléchargement avec progression, relance de l'installateur.</summary>
public sealed class UpdateWindow : Window
{
    private static readonly Color Accent = Color.FromRgb(0x7C, 0x4D, 0xFF);
    private static readonly Brush Bg = Make(Color.FromRgb(0x17, 0x17, 0x1F));
    private static readonly Brush Card = Make(Color.FromRgb(0x1F, 0x1F, 0x2A));
    private static readonly Brush White = Make(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Sub = Make(Color.FromArgb(0xB4, 0xE8, 0xE8, 0xF2));
    private static readonly Brush AccentBrush = Make(Accent);

    private readonly ConfigManager _cfg;
    private readonly AppUpdate _update;
    private readonly ProgressBar _progress = new() { Height = 6, Opacity = 0, Foreground = AccentBrush, Background = Make(Color.FromRgb(0x2A, 0x2A, 0x38)), BorderThickness = new Thickness(0) };
    private readonly TextBlock _progressLabel = new() { Foreground = Sub, FontSize = 11.5, Opacity = 0, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _updateBtn;

    public UpdateWindow(ConfigManager cfg, AppUpdate update)
    {
        _cfg = cfg;
        _update = update;

        Title = "Mise à jour — Ravenne's Tools Menu";
        Width = 500; MinWidth = 440; MaxWidth = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Bg;
        Foreground = White;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;

        var font = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var root = new StackPanel { Margin = new Thickness(28, 22, 28, 22) };

        root.Children.Add(new TextBlock
        {
            Text = "NOUVELLE VERSION DISPONIBLE",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentBrush,
            FontFamily = font,
        });

        root.Children.Add(new TextBlock
        {
            Text = $"{UpdateChecker.CurrentVersion}  →  {update.Version}",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = White,
            FontFamily = font,
            Margin = new Thickness(0, 6, 0, 2),
        });

        root.Children.Add(new TextBlock
        {
            Text = update.Published.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm"),
            FontSize = 11.5,
            Foreground = Sub,
            FontFamily = font,
            Margin = new Thickness(0, 0, 0, 14),
        });

        if (!string.IsNullOrEmpty(update.Notes))
        {
            var notes = new Border
            {
                Background = Card,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                MaxHeight = 170,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock
                    {
                        Text = update.Notes,
                        FontSize = 12,
                        FontFamily = font,
                        Foreground = Sub,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            };
            root.Children.Add(notes);
        }

        root.Children.Add(_progress);
        root.Children.Add(_progressLabel);

        _updateBtn = DarkButton("Mettre à jour maintenant", accent: true);
        _updateBtn.Click += async (_, _) => await RunUpdateAsync();

        var laterBtn = DarkButton("Plus tard");
        laterBtn.Click += (_, _) => Close();

        var skipBtn = DarkButton("Ignorer cette version");
        skipBtn.Click += (_, _) =>
        {
            _cfg.MutateAndSave(c => c.Updates.SkipVersion = _update.Version.ToString());
            Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 18, 0, 0),
        };
        buttons.Children.Add(_updateBtn);
        buttons.Children.Add(laterBtn);
        buttons.Children.Add(skipBtn);

        root.Children.Add(buttons);
        Content = root;
    }

    private async Task RunUpdateAsync()
    {
        _updateBtn.IsEnabled = false;
        _progress.Opacity = 1;
        _progressLabel.Opacity = 1;
        _progressLabel.Text = "Téléchargement…";

        try
        {
            var dest = Path.Combine(Path.GetTempPath(), $"RavenneToolsMenu-Setup-{_update.Version}.exe");
            var progress = new Progress<int>(p =>
            {
                _progress.Value = p;
                _progressLabel.Text = $"Téléchargement… {p} %";
            });

            await UpdateChecker.DownloadAsync(_update, dest, progress);
            _progressLabel.Text = "Téléchargement terminé — lancement de l'installation…";

            await Task.Delay(600);
            Process.Start(new ProcessStartInfo(dest) { UseShellExecute = true });
            await Task.Delay(900); // laisser l'installateur démarrer avant de libérer les fichiers
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error("Téléchargement de la mise à jour échoué", ex);
            _progressLabel.Text = "Échec du téléchargement : " + ex.Message;
            _updateBtn.IsEnabled = true;
        }
    }

    private static Button DarkButton(string text, bool accent = false)
    {
        var b = new Button
        {
            Content = text,
            FontSize = 12.5,
            FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            Foreground = accent ? Brushes.White : Sub,
            Background = accent ? AccentBrush : Make(Color.FromArgb(0xFF, 0x24, 0x24, 0x30)),
            BorderBrush = accent ? AccentBrush : Make(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        return b;
    }

    private static Brush Make(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
