using System.IO;
using System.Text;

namespace ArabicPdfOcrApp.Services;

public class TextExportService : ITextExportService
{
    public async Task SaveTextAsync(string filePath, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Output file path cannot be empty.", nameof(filePath));

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use UTF-8 with BOM so Windows Notepad and text editors immediately recognize Arabic UTF-8 encoding
        var utf8WithBom = new UTF8Encoding(true);
        
        await File.WriteAllTextAsync(filePath, text, utf8WithBom, cancellationToken);
    }
}
