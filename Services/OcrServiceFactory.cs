using NileFusion.Converter.Models;

namespace NileFusion.Converter.Services;

public static class OcrServiceFactory
{
    private static readonly Lazy<TesseractOcrService> _tesseractService = new(() => new TesseractOcrService());
    private static readonly Lazy<WindowsMediaOcrService> _windowsService = new(() => new WindowsMediaOcrService());

    public static IOcrService GetService(OcrEngineType engineType)
    {
        return engineType switch
        {
            OcrEngineType.TesseractArabic => _tesseractService.Value,
            OcrEngineType.WindowsMediaOcr => _windowsService.Value,
            _ => _tesseractService.Value
        };
    }
}
