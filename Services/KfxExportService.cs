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

            // Wait a moment for any child processes (like Kindle Previewer) to fully start reading/writing
            await Task.Delay(1000, ct);

            // Wait for output file to exist
            int maxWaitAttempts = 30;
            while (maxWaitAttempts > 0 && !File.Exists(kfxPath))
            {
                await Task.Delay(100, ct);
                maxWaitAttempts--;
            }

            if (!File.Exists(kfxPath))
            {
                throw new Exception($"KFX output file was not created by Calibre at: {kfxPath}");
            }

            // Wait for file to be fully written - keep waiting while the file size is still changing
            long previousSize = -1;
            int stableCount = 0;
            int maxStableChecks = 10; // Need 10 consecutive stable checks = ~2 seconds

            while (stableCount < maxStableChecks)
            {
                try
                {
                    long currentSize = new FileInfo(kfxPath).Length;

                    if (currentSize == previousSize && currentSize > 0)
                    {
                        stableCount++;
                    }
                    else
                    {
                        stableCount = 0;
                    }

                    previousSize = currentSize;

                    if (stableCount < maxStableChecks)
                    {
                        await Task.Delay(200, ct);
                    }
                }
                catch
                {
                    // File might be locked, wait and retry
                    await Task.Delay(200, ct);
                }
            }

            // Now try to validate the output KFX file with retries
            int validationAttempts = 0;
            int maxValidationAttempts = 10;

            while (validationAttempts < maxValidationAttempts)
            {
                try
                {
                    var fileInfo = new FileInfo(kfxPath);
                    if (!fileInfo.Exists || fileInfo.Length == 0)
                    {
                        throw new Exception("KFX file is missing or empty.");
                    }

                    using (var testStream = new FileStream(kfxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (testStream.Length == 0)
                        {
                            throw new Exception("KFX file is 0 bytes.");
                        }

                        byte[] header = new byte[8];
                        int bytesRead = testStream.Read(header, 0, header.Length);

                        bool isZip = bytesRead >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

                        if (isZip)
                        {
                            testStream.Position = 0;
                            using (var testArchive = new System.IO.Compression.ZipArchive(testStream, System.IO.Compression.ZipArchiveMode.Read))
                            {
                                int entryCount = testArchive.Entries.Count;
                                if (entryCount > 0)
                                {
                                    progress?.Report($"KFX (ZIP container) validated successfully ({entryCount} entries)");
                                    break;
                                }
                                else
                                {
                                    throw new Exception("ZIP archive has 0 entries.");
                                }
                            }
                        }
                        else
                        {
                            // Standard KFX binary container format
                            progress?.Report($"KFX binary container file validated successfully ({fileInfo.Length / 1024.0:F1} KB)");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    validationAttempts++;
                    if (validationAttempts >= maxValidationAttempts)
                    {
                        throw new Exception(
                            $"KFX file validation failed after {maxValidationAttempts} attempts: {ex.Message}\n" +
                            $"The file may be corrupted or still locked by another process (e.g., Kindle Previewer).\n" +
                            $"Please close any applications accessing the file and try again.",
                            ex);
                    }
                    await Task.Delay(300, ct);
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
