using System.IO;
using System.Text;

namespace NileFusion.Converter.Services;

public class TextExportService : ITextExportService
{
    private readonly ILogService _logger;

    public TextExportService() : this(new NullLogService())
    {
    }

    public TextExportService(ILogService logger)
    {
        _logger = logger ?? new NullLogService();
    }

    public async Task SaveTextAsync(string filePath, string text, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        var (isValid, errorMessage) = ValidationService.ValidateFilePath(filePath, mustExist: false, ".txt");
        if (!isValid)
        {
            _logger.LogError($"Invalid output path: {errorMessage}");
            throw new ArgumentException(errorMessage, nameof(filePath));
        }

        if (!ValidationService.ValidateTextContent(text, out var textError))
        {
            _logger.LogWarning(textError!);
            throw new ArgumentException(textError, nameof(text));
        }

        try
        {
            _logger.LogInfo($"Starting text export to: {filePath}");

            // Create directory if it doesn't exist
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInfo($"Created output directory: {directory}");
            }

            // Check for existing file
            if (File.Exists(filePath))
            {
                _logger.LogWarning($"Output file already exists, will be overwritten: {filePath}");
            }

            // Use UTF-8 with BOM for Arabic text, ensuring proper encoding
            // Windows Notepad and text editors immediately recognize UTF-8 with BOM
            var utf8WithBom = new UTF8Encoding(true);

            await File.WriteAllTextAsync(filePath, text, utf8WithBom, cancellationToken);

            var fileInfo = new FileInfo(filePath);
            _logger.LogInfo($"Text export completed successfully. File size: {fileInfo.Length:N0} bytes");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Text export was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Text export failed", ex);
            throw;
        }
    }
}

