using System.IO;
using System.IO.Compression;
using System.Text;

namespace NileFusion.BookConverter.Services;

/// <summary>
/// Service for reading and reviewing KFX (Kindle Format 10) file information.
/// KFX files are ZIP archives with metadata and content files.
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
        public List<string> Contents { get; set; } = new();
        public string? RawMetadata { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Extracts information from a KFX file.
    /// KFX files are ZIP archives, so this reads the structure and metadata.
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

            // KFX files are ZIP archives
            using (var stream = File.OpenRead(kfxPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                info.IsValid = true;

                // List all entries in the archive
                foreach (var entry in archive.Entries)
                {
                    // Skip directory entries
                    if (!entry.Name.Contains("."))
                        continue;

                    info.Contents.Add($"{entry.Name} ({FormatFileSize(entry.Length)})");

                    // Look for metadata in various locations and formats
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

                            // Extract basic metadata - try multiple tag variants
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
                        catch
                        {
                            // Silently ignore read errors for individual entries
                        }
                    }
                }

                // Set defaults if metadata not found
                if (string.IsNullOrEmpty(info.Title))
                    info.Title = Path.GetFileNameWithoutExtension(kfxPath);

                if (string.IsNullOrEmpty(info.Author))
                    info.Author = "Unknown";

                if (string.IsNullOrEmpty(info.Language))
                    info.Language = "Unknown";
            }
        }
        catch (Exception ex)
        {
            info.IsValid = false;

            // Provide specific guidance for common KFX corruption issues
            if (ex.Message.Contains("End of Central Directory"))
            {
                info.ErrorMessage = "The KFX file appears to be corrupted or incomplete. " +
                    "This can happen if the file was still being written when you tried to open it. " +
                    "Please try exporting the KFX file again.";
            }
            else
            {
                info.ErrorMessage = $"Error reading KFX file: {ex.Message}";
            }
        }

        return info;
    }

    /// <summary>
    /// Extracts a metadata value from XML content.
    /// </summary>
    private string? ExtractMetadataValue(string xmlContent, string tagName)
    {
        try
        {
            // Try standard tag format first
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

            // Try with namespace prefix (dc:, opf:, etc.)
            // Match any tag that ends with the tagName, like <dc:title>...</dc:title>
            var pattern = $@"<[^>]*:{tagName}[^>]*>([^<]*)<\/[^>]*:{tagName}>";
            var match = System.Text.RegularExpressions.Regex.Match(xmlContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value.Trim();

            // Try with attributes on the tag
            var attributePattern = $@"<{tagName}[^>]*>([^<]*)<\/{tagName}>";
            var attributeMatch = System.Text.RegularExpressions.Regex.Match(xmlContent, attributePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (attributeMatch.Success && attributeMatch.Groups.Count > 1)
                return attributeMatch.Groups[1].Value.Trim();

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
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
    /// Gets a formatted summary of the KFX file information.
    /// </summary>
    public string GetFormattedSummary(KfxFileInfo info)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"📄 File: {info.FileName}");
        sb.AppendLine($"📊 Size: {FormatFileSize(info.FileSizeBytes)}");
        sb.AppendLine($"📅 Created: {info.CreatedDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"📝 Modified: {info.ModifiedDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("─── Metadata ───");
        sb.AppendLine($"📚 Title: {info.Title ?? "N/A"}");
        sb.AppendLine($"✍️  Author: {info.Author ?? "N/A"}");
        sb.AppendLine($"🌐 Language: {info.Language ?? "N/A"}");
        sb.AppendLine();

        if (info.Contents.Count > 0)
        {
            sb.AppendLine("─── Archive Contents ───");
            foreach (var content in info.Contents.Take(20))
            {
                sb.AppendLine($"  • {content}");
            }

            if (info.Contents.Count > 20)
            {
                sb.AppendLine($"  ... and {info.Contents.Count - 20} more files");
            }
        }

        if (!string.IsNullOrEmpty(info.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️ Error: {info.ErrorMessage}");
        }

        return sb.ToString();
    }
}
