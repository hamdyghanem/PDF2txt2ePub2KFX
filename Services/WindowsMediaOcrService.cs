using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace NileFusion.Converter.Services;

public class WindowsMediaOcrService : IOcrService
{
    private OcrEngine? _ocrEngine;

    public string EngineName => "Windows Media OCR (Native Windows)";

    public Task InitializeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("Initializing Windows Media OCR engine...");

        // Try Arabic language pack first
        var arabicLang = new Language("ar-SA");
        if (OcrEngine.IsLanguageSupported(arabicLang))
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(arabicLang);
        }
        else
        {
            var genArabic = new Language("ar");
            if (OcrEngine.IsLanguageSupported(genArabic))
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(genArabic);
            }
            else
            {
                // Fallback to first available recognizer language on machine
                var available = OcrEngine.AvailableRecognizerLanguages;
                if (available.Count > 0)
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(available[0]);
                }
            }
        }

        if (_ocrEngine == null)
        {
            throw new InvalidOperationException("Windows Media OCR engine could not be initialized. No supported OCR language pack found.");
        }

        progress?.Report($"Windows Media OCR ready (Language: {_ocrEngine.RecognizerLanguage.DisplayName})");
        return Task.CompletedTask;
    }

    public async Task<string> ProcessImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (_ocrEngine == null)
        {
            await InitializeAsync(cancellationToken: cancellationToken);
        }

        if (_ocrEngine == null)
            throw new InvalidOperationException("Windows Media OCR engine is not initialized.");

        cancellationToken.ThrowIfCancellationRequested();

        using var ms = new MemoryStream(imageBytes);
        using var randomAccessStream = ms.AsRandomAccessStream();

        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _ocrEngine.RecognizeAsync(softwareBitmap);
        return result.Text ?? string.Empty;
    }
}
