using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NileFusion.BookConverter.Services;

/// <summary>
/// Service for reading and reviewing KFX (Kindle Format 10) file information.
/// Supports both ZIP container KFX/KPF archives and native binary KFX containers.
/// </summary>
public class KfxReviewService
{
    /// <summary>
    /// Represents basic information extracted from a KFX file.
    /// </summary>
    public class KfxFileInfo
    {
        public string? FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Language { get; set; }
        public string? Publisher { get; set; }
        public string? Description { get; set; }
        public string? FormatType { get; set; }
        public List<string> Contents { get; set; } = new();
        public string? RawMetadata { get; set; }
        public string? ExtractedText { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Extracts information and text content from a KFX file.
    /// </summary>
    public KfxFileInfo ExtractKfxInfo(string kfxPath)
    {
        var info = new KfxFileInfo
        {
            FileName = Path.GetFileName(kfxPath),
            IsValid = false
        };

        try
        {
            if (!File.Exists(kfxPath))
            {
                info.ErrorMessage = $"File not found: {kfxPath}";
                return info;
            }

            var fileInfo = new FileInfo(kfxPath);
            info.FileSizeBytes = fileInfo.Length;
            info.CreatedDate = fileInfo.CreationTime;
            info.ModifiedDate = fileInfo.LastWriteTime;

            if (fileInfo.Length == 0)
            {
                info.ErrorMessage = "KFX file is empty (0 bytes).";
                return info;
            }

            // Check if file is ZIP archive or native binary KFX container
            using (var stream = File.OpenRead(kfxPath))
            {
                byte[] header = new byte[8];
                int bytesRead = stream.Read(header, 0, header.Length);
                stream.Position = 0;

                bool isZip = bytesRead >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

                if (isZip)
                {
                    info.FormatType = "KPF / EPUB ZIP Container";
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        info.IsValid = true;

                        foreach (var entry in archive.Entries)
                        {
                            if (!entry.Name.Contains("."))
                                continue;

                            info.Contents.Add($"{entry.Name} ({FormatFileSize(entry.Length)})");

                            string entryNameLower = entry.Name.ToLower();

                            if (entryNameLower.Contains("metadata") || 
                                entryNameLower.EndsWith(".opf") || 
                                entryNameLower.Contains("content") ||
                                entryNameLower.Contains("package.opf"))
                            {
                                try
                                {
                                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                                    string content = reader.ReadToEnd();

                                    if (info.Title == null)
                                    {
                                        info.Title = ExtractMetadataValue(content, "title");
                                        if (string.IsNullOrEmpty(info.Title))
                                            info.Title = ExtractMetadataValue(content, "dc:title");
                                    }

                                    if (info.Author == null)
                                    {
                                        info.Author = ExtractMetadataValue(content, "creator");
                                        if (string.IsNullOrEmpty(info.Author))
                                            info.Author = ExtractMetadataValue(content, "dc:creator");
                                    }

                                    if (info.Language == null)
                                    {
                                        info.Language = ExtractMetadataValue(content, "language");
                                        if (string.IsNullOrEmpty(info.Language))
                                            info.Language = ExtractMetadataValue(content, "dc:language");
                                    }

                                    if (info.RawMetadata == null && content.Length > 0)
                                        info.RawMetadata = content.Substring(0, Math.Min(500, content.Length));
                                }
                                catch { }
                            }
                        }
                    }
                }
                else
                {
                    // Native binary KFX container format
                    info.FormatType = "Amazon KFX Binary Container (Kindle Format 10)";
                    info.IsValid = true;
                    info.Contents.Add($"Amazon KFX Container ({FormatFileSize(info.FileSizeBytes)})");
                }

                // 1. Try extracting metadata using Calibre CLI (ebook-meta)
                TryExtractMetadataWithCalibre(kfxPath, info);

                // 2. Extract full text content using Calibre CLI (ebook-convert to .txt)
                TryExtractTextWithCalibre(kfxPath, info);

                // Set defaults if metadata not found
                if (string.IsNullOrEmpty(info.Title))
                    info.Title = Path.GetFileNameWithoutExtension(kfxPath);

                if (string.IsNullOrEmpty(info.Author))
                    info.Author = "Unknown";

                if (string.IsNullOrEmpty(info.Language))
                    info.Language = "ar";
            }
        }
        catch (Exception ex)
        {
            info.IsValid = false;
            info.ErrorMessage = $"Error reading KFX file: {ex.Message}";
        }

        return info;
    }

    /// <summary>
    /// Uses Calibre's ebook-meta tool to read KFX metadata if available.
    /// </summary>
    private void TryExtractMetadataWithCalibre(string kfxPath, KfxFileInfo info)
    {
        string? calibreMeta = KfxExportService.FindCalibreConvert();
        if (string.IsNullOrEmpty(calibreMeta)) return;

        string calibreDir = Path.GetDirectoryName(calibreMeta) ?? "";
        string ebookMetaExe = Path.Combine(calibreDir, "ebook-meta.exe");

        if (!File.Exists(ebookMetaExe)) return;

        try
        {
            var psi = new ProcessStartInfo(ebookMetaExe, $"\"{kfxPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = line.Substring(0, colonIdx).Trim().ToLower();
                    string val = line.Substring(colonIdx + 1).Trim();

                    if (string.IsNullOrEmpty(val) || val == "Unknown" || val == "None") continue;

                    if (key.StartsWith("title") && string.IsNullOrEmpty(info.Title))
                        info.Title = val;
                    else if (key.StartsWith("author") && string.IsNullOrEmpty(info.Author))
                        info.Author = val;
                    else if (key.StartsWith("language") && string.IsNullOrEmpty(info.Language))
                        info.Language = val;
                    else if (key.StartsWith("publisher") && string.IsNullOrEmpty(info.Publisher))
                        info.Publisher = val;
                    else if (key.StartsWith("comments") && string.IsNullOrEmpty(info.Description))
                        info.Description = val;
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Uses Calibre's ebook-convert to extract book text content.
    /// </summary>
    private void TryExtractTextWithCalibre(string kfxPath, KfxFileInfo info)
    {
        string? calibreConvert = KfxExportService.FindCalibreConvert();
        if (string.IsNullOrEmpty(calibreConvert) || !File.Exists(calibreConvert)) return;

        string tempTxt = Path.Combine(Path.GetTempPath(), $"kfx_extract_{Guid.NewGuid():N}.txt");

        try
        {
            var psi = new ProcessStartInfo(calibreConvert, $"\"{kfxPath}\" \"{tempTxt}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            proc.WaitForExit(15000);

            if (File.Exists(tempTxt))
            {
                string text = File.ReadAllText(tempTxt, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    info.ExtractedText = text.Trim();
                    info.CharacterCount = info.ExtractedText.Length;
                    info.WordCount = CountWords(info.ExtractedText);
                }
            }
        }
        catch { }
        finally
        {
            if (File.Exists(tempTxt))
            {
                try { File.Delete(tempTxt); } catch { }
            }
        }
    }

    private static void ProcessRun(List<byte> runBytes, StringBuilder sb)
    {
        try
        {
            string str = Encoding.UTF8.GetString(runBytes.ToArray());
            string cleaned = Regex.Replace(str, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F]", " ").Trim();
            if (cleaned.Length >= 4 && ContainsArabicOrText(cleaned))
            {
                sb.AppendLine(cleaned);
            }
        }
        catch { }
    }

    private static bool ContainsArabicOrText(string str)
    {
        foreach (char c in str)
        {
            if ((c >= 0x0600 && c <= 0x06FF) || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                return true;
        }
        return false;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        char[] delimiters = { ' ', '\r', '\n', '\t', '،', '؛', '.', '!', '؟' };
        return text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private string? ExtractMetadataValue(string xmlContent, string tagName)
    {
        try
        {
            var startTag = $"<{tagName}>";
            var endTag = $"</{tagName}>";

            int startIndex = xmlContent.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                startIndex += startTag.Length;
                int endIndex = xmlContent.IndexOf(endTag, startIndex, StringComparison.OrdinalIgnoreCase);
                if (endIndex >= 0)
                    return xmlContent.Substring(startIndex, endIndex - startIndex).Trim();
            }

            var pattern = $@"<[^>]*:{tagName}[^>]*>([^<]*)<\/[^>]*:{tagName}>";
            var match = Regex.Match(xmlContent, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value.Trim();

            var attributePattern = $@"<{tagName}[^>]*>([^<]*)<\/{tagName}>";
            var attributeMatch = Regex.Match(xmlContent, attributePattern, RegexOptions.IgnoreCase);
            if (attributeMatch.Success && attributeMatch.Groups.Count > 1)
                return attributeMatch.Groups[1].Value.Trim();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Gets a formatted summary of the KFX file information and text content.
    /// </summary>
    public string GetFormattedSummary(KfxFileInfo info)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"📄 File: {info.FileName}");
        sb.AppendLine($"📊 Size: {FormatFileSize(info.FileSizeBytes)}");
        sb.AppendLine($"📅 Created: {info.CreatedDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"📝 Modified: {info.ModifiedDate:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrEmpty(info.FormatType))
        {
            sb.AppendLine($"📦 Format: {info.FormatType}");
        }
        sb.AppendLine();

        sb.AppendLine("─── Metadata ───");
        sb.AppendLine($"📚 Title: {info.Title ?? "N/A"}");
        sb.AppendLine($"✍️  Author: {info.Author ?? "N/A"}");
        sb.AppendLine($"🌐 Language: {info.Language ?? "N/A"}");
        if (!string.IsNullOrEmpty(info.Publisher))
        {
            sb.AppendLine($"🏢 Publisher: {info.Publisher}");
        }
        if (!string.IsNullOrEmpty(info.Description))
        {
            sb.AppendLine($"📝 Description: {info.Description}");
        }
        sb.AppendLine();

        if (info.WordCount > 0 || info.CharacterCount > 0)
        {
            sb.AppendLine("─── Text Statistics ───");
            sb.AppendLine($"📖 Word Count: {info.WordCount:N0} words");
            sb.AppendLine($"🔤 Character Count: {info.CharacterCount:N0} characters");
        }

        if (!string.IsNullOrEmpty(info.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️ Warning: {info.ErrorMessage}");
        }

        return sb.ToString();
    }
}
