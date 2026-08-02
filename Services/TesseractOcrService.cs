using System.IO;
using System.Net.Http;
using NileFusion.Converter.Models;
using Tesseract;

namespace NileFusion.Converter.Services;

public class TesseractOcrService : IOcrService
{
    private TesseractEngine? _engine;
    private const string AraTrainedDataUrl = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/ara.traineddata";
    private readonly string _tessDataFolder;

    public string EngineName => "Tesseract OCR (Arabic - ara.traineddata)";

    public TesseractOcrService()
    {
        _tessDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
    }

    public async Task InitializeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_engine != null) return;

        if (!Directory.Exists(_tessDataFolder))
            Directory.CreateDirectory(_tessDataFolder);

        string araDataFilePath = Path.Combine(_tessDataFolder, "ara.traineddata");

        // Minimum valid ara.traineddata size is ~1 MB
        if (!File.Exists(araDataFilePath) || new FileInfo(araDataFilePath).Length < 500000)
        {
            progress?.Report("Downloading Arabic language OCR traineddata (ara.traineddata)...");
            await DownloadTrainedDataAsync(araDataFilePath, progress, cancellationToken);
        }

        progress?.Report("Initializing Tesseract OCR engine...");

        await Task.Run(() =>
        {
            try
            {
                _engine = new TesseractEngine(_tessDataFolder, "ara", EngineMode.Default);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize Tesseract OCR engine with tessdata at '{_tessDataFolder}': {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    private async Task DownloadTrainedDataAsync(string destinationPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        try
        {
            progress?.Report("Connecting to Tesseract GitHub repository...");
            using var response = await client.GetAsync(AraTrainedDataUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;

                if (totalBytes > 0)
                {
                    double pct = (double)totalRead / totalBytes * 100;
                    progress?.Report($"Downloading ara.traineddata: {pct:F0}% ({totalRead / 1024} KB / {totalBytes / 1024} KB)");
                }
                else
                {
                    progress?.Report($"Downloading ara.traineddata: {totalRead / 1024} KB");
                }
            }

            progress?.Report("Arabic language OCR data downloaded successfully.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (File.Exists(destinationPath))
                try { File.Delete(destinationPath); } catch { }

            throw new InvalidOperationException(
                "Could not download Tesseract Arabic language file 'ara.traineddata'. " +
                "Please check your internet connection or manually copy 'ara.traineddata' to the 'tessdata' folder.", ex);
        }
    }

    public async Task<string> ProcessImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (_engine == null)
            await InitializeAsync(cancellationToken: cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            if (_engine == null)
                throw new InvalidOperationException("Tesseract engine is not initialized.");

            using var pix = Pix.LoadFromMemory(imageBytes);
            if (pix == null)
                throw new InvalidOperationException("Failed to decode PNG image stream for Tesseract OCR.");

            float pageWidth  = pix.Width;
            float pageCenter = pageWidth / 2f;

            using var page = _engine.Process(pix);
            using var iter = page.GetIterator();
            iter.Begin();

            var sb = new System.Text.StringBuilder();

            do
            {
                // Bounding box at text-line level
                if (!iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var bbox))
                    continue;

                string lineText = iter.GetText(PageIteratorLevel.TextLine)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(lineText))
                    continue;

                float lineWidth  = bbox.X2 - bbox.X1;
                float lineCenter = (bbox.X1 + bbox.X2) / 2f;

                // Centered heuristic:
                //  • line occupies < 75% of page width (not a full-width paragraph)
                //  • line's horizontal mid-point is within ±12% of the page center
                bool isShortLine = lineWidth  < pageWidth * 0.75f;
                bool isMidPage   = Math.Abs(lineCenter - pageCenter) < pageWidth * 0.12f;
                bool isCentered  = isShortLine && isMidPage;

                // Tab prefix = "this line is centered" marker
                // • In the TextBox (RTL): tab pushes text inward → visually centered-ish
                // • In EPUB: BuildParagraphs() maps tab-prefixed lines to CSS text-align:center
                // • In TXT: tabs are standard indentation markers
                sb.AppendLine(isCentered ? "\t" + lineText : lineText);

            } while (iter.Next(PageIteratorLevel.TextLine));

            return sb.ToString().Trim();

        }, cancellationToken);
    }
}
