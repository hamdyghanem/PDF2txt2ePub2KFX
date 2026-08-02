using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArabicPdfOcrApp.Models;

public partial class PdfPageItem : ObservableObject
{
    [ObservableProperty]
    private int _pageIndex;

    [ObservableProperty]
    private BitmapSource? _pageImage;

    [ObservableProperty]
    private byte[]? _highResImageBytes;

    [ObservableProperty]
    private string _extractedText = string.Empty;

    [ObservableProperty]
    private OcrStatus _status = OcrStatus.Pending;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private int _charCount;

    partial void OnExtractedTextChanged(string value)
    {
        CharCount = value?.Length ?? 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            WordCount = 0;
        }
        else
        {
            WordCount = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
