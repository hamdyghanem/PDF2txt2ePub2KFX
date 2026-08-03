using System.Drawing;

namespace NileFusion.BookConverter.Models;

public class PdfPageItem
{
    public int PageIndex { get; set; }
    public Image? PageImage { get; set; }
    public byte[]? HighResImageBytes { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public OcrStatus Status { get; set; } = OcrStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int WordCount { get; set; }
    public int CharCount { get; set; }
}
