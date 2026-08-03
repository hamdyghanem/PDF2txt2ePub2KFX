using System.Diagnostics;
using System.IO;

namespace NileFusion.BookConverter.Services;

public class KfxExportService
{
    // Common Calibre install locations on Windows
    private static readonly string[] CalibrePaths =
    [
        @"C:\Program Files\Calibre2\ebook-convert.exe",
        @"C:\Program Files (x86)\Calibre2\ebook-convert.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Calibre2\ebook-convert.exe"),
    ];

    /// <summary>
    /// Finds the ebook-convert.exe from Calibre on the system.
    /// Returns null if not found.
    /// </summary>
    public static string? FindCalibreConvert()
    {
        // Check well-known install paths first
        foreach (var path in CalibrePaths)
            if (File.Exists(path)) return path;

        // Try PATH
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("where", "ebook-convert.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            string? output = proc?.StandardOutput.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(output) && File.Exists(output))
                return output;
        }
        catch { /* not in PATH */ }

        return null;
    }

    /// <summary>
    /// Converts the extracted Arabic text to KFX by:
    /// 1. Building a temp EPUB using EpubExportService
    /// 2. Calling Calibre's ebook-convert to produce the KFX
    /// </summary>
    public async Task SaveKfxAsync(
        string kfxPath,
        string allText,
        string pdfTitle,
        string calibreExePath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // Step 1 – build a temp EPUB in system temp directory instead of output directory
        // This prevents file locking issues when Calibre processes the file
        string tempEpubFileName = $"temp_{Path.GetFileNameWithoutExtension(kfxPath)}_{Guid.NewGuid():N}.epub";
        string tempEpub = Path.Combine(Path.GetTempPath(), tempEpubFileName);

        try
        {
            progress?.Report("Building intermediate EPUB...");
            var epubSvc = new EpubExportService();
            await epubSvc.SaveEpubAsync(tempEpub, allText, pdfTitle, ct);

            // Step 2 – call Calibre's ebook-convert
            progress?.Report("Calling Calibre ebook-convert → KFX (this may take a moment)...");

            // Note: Calibre's KFX Output plugin may launch Kindle Previewer after conversion
            // This is a Calibre behavior that cannot be suppressed via command-line flags
            string args = $"\"{tempEpub}\" \"{kfxPath}\" " +
                          "--output-profile kindle_pw3 " +
                          "--input-encoding utf-8 " +
                          "--language ar";

            var psi = new ProcessStartInfo(calibreExePath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var outputLog = new System.Text.StringBuilder();

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputLog.AppendLine(e.Data);
                    progress?.Report(e.Data);
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) outputLog.AppendLine("[ERR] " + e.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Wait for completion with cancellation support
            await Task.Run(() => proc.WaitForExit(), ct);

            if (proc.ExitCode != 0)
            {
                throw new Exception(
                    $"Calibre ebook-convert exited with code {proc.ExitCode}.\n\n" +
                    $"Output:\n{outputLog}\n\n" +
                    "Make sure the Calibre KFX Output plugin is installed:\n" +
                    "Calibre → Preferences → Plugins → Get new plugins → search 'KFX Output'");
            }

            // Wait for output file to be fully written to disk
            // Sometimes Calibre returns before the file is completely flushed
            int maxWaitAttempts = 20;
            while (maxWaitAttempts > 0 && !File.Exists(kfxPath))
            {
                await Task.Delay(100, ct);
                maxWaitAttempts--;
            }

            // Give the file more time to be fully released and flushed by Calibre
            // KFX files can take a moment to be completely written
            await Task.Delay(2000, ct);

            // Verify the file is a valid ZIP archive (KFX is a ZIP file)
            // If it's still being written, wait a bit more
            int validationAttempts = 0;
            while (validationAttempts < 5)
            {
                try
                {
                    using (var testStream = File.OpenRead(kfxPath))
                    using (var testArchive = new System.IO.Compression.ZipArchive(testStream, System.IO.Compression.ZipArchiveMode.Read))
                    {
                        // If we can open it as a valid ZIP, we're done
                        progress?.Report("KFX file validated successfully");
                        break;
                    }
                }
                catch
                {
                    validationAttempts++;
                    if (validationAttempts < 5)
                    {
                        await Task.Delay(500, ct);
                    }
                }
            }

            progress?.Report($"KFX saved to: {Path.GetFileName(kfxPath)}");
        }
        finally
        {
            // Always clean up temp EPUB
            if (File.Exists(tempEpub))
                try { File.Delete(tempEpub); } catch { /* ignore */ }
        }
    }
}
