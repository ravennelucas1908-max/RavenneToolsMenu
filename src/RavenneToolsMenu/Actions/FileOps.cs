using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RavenneToolsMenu.Core;

namespace RavenneToolsMenu.Actions;

/// <summary>Opérations fichiers : conversion d'images, redimensionnement, ZIP, corbeille, ffmpeg.</summary>
internal static class FileOps
{
    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico" };

    public static bool IsImage(string path) => ImageExts.Contains(Path.GetExtension(path));

    public sealed record OpResult(int Ok, int Skipped, List<string> Errors)
    {
        public string Summary(string verb)
        {
            var s = Ok > 0 ? $"{Ok} fichier(s) {verb}" : "Aucun fichier traité";
            if (Skipped > 0) s += $", {Skipped} ignoré(s)";
            if (Errors.Count > 0) s += $" — {Errors.Count} erreur(s)";
            return s;
        }
    }

    // ---------- Conversion d'images (codecs natifs Windows) ----------

    public static OpResult ConvertImages(List<string> files, string target, int quality)
    {
        int ok = 0, skipped = 0;
        var errors = new List<string>();
        foreach (var file in files)
        {
            if (!IsImage(file)) { skipped++; continue; }
            try
            {
                var srcExt = Path.GetExtension(file).ToLowerInvariant();
                if (srcExt == "." + target.ToLowerInvariant()) { skipped++; continue; }
                var source = LoadBitmap(file);
                var outPath = UniquePath(Path.Combine(Path.GetDirectoryName(file)!,
                    Path.GetFileNameWithoutExtension(file) + "." + target.ToLowerInvariant()));
                if (target.Equals("ico", StringComparison.OrdinalIgnoreCase))
                {
                    SaveAsIco(source, outPath);
                }
                else
                {
                    using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                    var encoder = EncoderFor(target, quality);
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(fs);
                }
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)} : {FriendlyCodecError(ex)}");
            }
        }
        return new OpResult(ok, skipped, errors);
    }

    /// <summary>ICO moderne (PNG embarqué, natif depuis Windows Vista).</summary>
    private static void SaveAsIco(BitmapSource source, string outPath)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        png.Save(ms);
        var bytes = ms.ToArray();

        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        w.Write((ushort)0);                                   // réservé
        w.Write((ushort)1);                                   // type : icône
        w.Write((ushort)1);                                   // 1 image
        w.Write((byte)(source.PixelWidth >= 256 ? 0 : source.PixelWidth));
        w.Write((byte)(source.PixelHeight >= 256 ? 0 : source.PixelHeight));
        w.Write((byte)0);                                     // palette
        w.Write((byte)0);                                     // réservé
        w.Write((ushort)1);                                   // plans
        w.Write((ushort)32);                                  // bpp
        w.Write((uint)bytes.Length);
        w.Write((uint)22);                                    // décalage des données
        w.Write(bytes);
    }

    // ---------- Redimensionnement ----------

    public static OpResult ResizeImages(List<string> files, double scale, int? maxWidth)
    {
        int ok = 0, skipped = 0;
        var errors = new List<string>();
        foreach (var file in files)
        {
            if (!IsImage(file)) { skipped++; continue; }
            try
            {
                var source = LoadBitmap(file);
                double factor;
                if (maxWidth is int w && w > 0) factor = (double)w / source.PixelWidth;
                else if (scale > 0) factor = scale;
                else { skipped++; continue; }
                if (Math.Abs(factor - 1.0) < 0.001) { skipped++; continue; }

                var resized = new TransformedBitmap(source, new ScaleTransform(factor, factor));
                var suffix = maxWidth is int w2 ? $"_{w2}px" : $"_{(int)Math.Round(scale * 100)}pct";
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".jpg" or ".jpeg" or ".jfif") ext = ".jpg";
                var outPath = UniquePath(Path.Combine(Path.GetDirectoryName(file)!,
                    Path.GetFileNameWithoutExtension(file) + suffix + ext));
                using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                var encoder = EncoderFor(ext.TrimStart('.'), 90);
                encoder.Frames.Add(BitmapFrame.Create(resized));
                encoder.Save(fs);
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)} : {ex.Message}");
            }
        }
        return new OpResult(ok, skipped, errors);
    }

    private static BitmapSource LoadBitmap(string file)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return ApplyOrientation(frame);
    }

    /// <summary>Applique l'orientation EXIF (photos de téléphone) avant conversion.</summary>
    private static BitmapSource ApplyOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata meta &&
                meta.GetQueryOrNull("/app1/ifd/{ushort=274}") is ushort orientation)
            {
                Transform t = orientation switch
                {
                    2 => new ScaleTransform(-1, 1),
                    3 => new RotateTransform(180),
                    4 => new ScaleTransform(1, -1),
                    5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
                    6 => new RotateTransform(90),
                    7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
                    8 => new RotateTransform(270),
                    _ => Transform.Identity,
                };
                if (t != Transform.Identity)
                    return new TransformedBitmap(frame, t);
            }
        }
        catch { }
        return frame;
    }

    private static BitmapEncoder EncoderFor(string target, int quality) => target.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 10, 100) },
        "png" => new PngBitmapEncoder(),
        "bmp" => new BmpBitmapEncoder(),
        "gif" => new GifBitmapEncoder(),
        "tiff" or "tif" => new TiffBitmapEncoder(),
        _ => new PngBitmapEncoder(),
    };

    private static string FriendlyCodecError(Exception ex) =>
        ex.Message.Contains("codec", StringComparison.OrdinalIgnoreCase) || ex is NotSupportedException
            ? "format non pris en charge (codec absent)"
            : ex.Message;

    // ---------- ZIP ----------

    public static (bool Ok, string Path, string Error) ZipFiles(List<string> files)
    {
        var existing = files.Where(File.Exists).ToList();
        if (existing.Count == 0) return (false, "", "aucun fichier trouvé");

        var dir = Path.GetDirectoryName(existing[0])!;
        var name = existing.Count == 1
            ? Path.GetFileNameWithoutExtension(existing[0]) + ".zip"
            : $"Archive-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var zipPath = UniquePath(Path.Combine(dir, name));

        try
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in existing)
            {
                var entry = Path.GetFileName(f);
                int n = 2;
                while (!used.Add(entry))
                    entry = $"{Path.GetFileNameWithoutExtension(f)} ({n++}){Path.GetExtension(f)}";
                archive.CreateEntryFromFile(f, entry, CompressionLevel.Optimal);
            }
            return (true, zipPath, "");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    // ---------- Corbeille ----------

    public static OpResult Recycle(List<string> files)
    {
        var existing = files.Where(File.Exists).ToList();
        if (existing.Count == 0) return new OpResult(0, 0, new List<string> { "aucun fichier trouvé" });

        // SHFileOperation exige une liste "chemin\0chemin\0\0".
        var packed = string.Join('\0', existing) + "\0\0";
        var op = new Win32.SHFILEOPSTRUCTW
        {
            wFunc = Win32.FO_DELETE,
            pFrom = packed,
            fFlags = (ushort)(Win32.FOF_ALLOWUNDO | Win32.FOF_NOCONFIRMATION | Win32.FOF_SILENT | Win32.FOF_NOERRORUI),
        };
        int rc = Win32.SHFileOperationW(ref op);
        return rc == 0
            ? new OpResult(existing.Count, 0, new List<string>())
            : new OpResult(0, 0, new List<string> { $"erreur {rc}" });
    }

    // ---------- ffmpeg (optionnel) ----------

    private static string? _ffmpegCache;
    private static bool _ffmpegSearched;

    public static string? ResolveFfmpeg(string configured)
    {
        if (_ffmpegCache is not null) return _ffmpegCache;
        if (_ffmpegSearched) return null;
        _ffmpegSearched = true;
        try
        {
            if (File.Exists(configured)) { _ffmpegCache = configured; return _ffmpegCache; }
            var psi = new ProcessStartInfo("where.exe", "ffmpeg")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            string? line = null;
            p.WaitForExit(3000);
            line = p.StandardOutput.ReadLine()?.Trim();
            if (p.ExitCode == 0 && !string.IsNullOrEmpty(line) && File.Exists(line))
                _ffmpegCache = line;
        }
        catch { }
        return _ffmpegCache;
    }

    public static void ResetFfmpegCache() { _ffmpegCache = null; _ffmpegSearched = false; }

    public static OpResult FfmpegConvert(List<string> files, string targetExt, string? argsTemplate, string ffmpegPath)
    {
        var exe = ResolveFfmpeg(ffmpegPath);
        if (exe is null)
            return new OpResult(0, 0, new List<string> { "ffmpeg introuvable — installez-le ou indiquez son chemin dans config.json (ffmpeg.path)" });

        int ok = 0, skipped = 0;
        var errors = new List<string>();
        var defaultArgs = targetExt.ToLowerInvariant() switch
        {
            "mp3" => "-vn -q:a 2",
            "mp4" => "-c:v libx264 -crf 23 -c:a aac -movflags +faststart",
            "wav" => "-vn -c:a pcm_s16le",
            "webm" => "-c:v libvpx-vp9 -crf 30 -b:a 128k",
            _ => "",
        };
        var template = string.IsNullOrWhiteSpace(argsTemplate) ? defaultArgs : argsTemplate;

        foreach (var file in files.Where(File.Exists))
        {
            try
            {
                if (Path.GetExtension(file).TrimStart('.').Equals(targetExt, StringComparison.OrdinalIgnoreCase))
                { skipped++; continue; }
                var outPath = UniquePath(Path.Combine(Path.GetDirectoryName(file)!,
                    Path.GetFileNameWithoutExtension(file) + "-conv." + targetExt));
                var args = template.Replace("{input}", Quote(file)).Replace("{output}", Quote(outPath));
                var psi = new ProcessStartInfo(exe, $"-y -i {Quote(file)} {args}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                if (p is null) { errors.Add(Path.GetFileName(file) + " : démarrage impossible"); continue; }
                if (!p.WaitForExit(180_000)) { p.Kill(); errors.Add(Path.GetFileName(file) + " : timeout"); continue; }
                if (p.ExitCode == 0 && File.Exists(outPath)) ok++;
                else errors.Add($"{Path.GetFileName(file)} : code {p.ExitCode}");
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)} : {ex.Message}");
            }
        }
        return new OpResult(ok, skipped, errors);
    }

    private static string Quote(string s) => "\"" + s.Trim('"') + "\"";

    public static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}

internal static class BitmapMetadataExtensions
{
    public static object? GetQueryOrNull(this BitmapMetadata meta, string query)
    {
        try { return meta.GetQuery(query); } catch { return null; }
    }
}
