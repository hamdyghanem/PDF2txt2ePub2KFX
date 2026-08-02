namespace NileFusion.Converter.Models;

public enum OcrEngineType
{
    TesseractArabic,
    WindowsMediaOcr
}

public enum OcrStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
