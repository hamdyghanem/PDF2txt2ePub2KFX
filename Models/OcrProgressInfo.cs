namespace ArabicPdfOcrApp.Models;

public record OcrProgressInfo(
    int CurrentPage,
    int TotalPages,
    string StatusMessage,
    bool IsIndeterminate = false
)
{
    public double ProgressPercentage => TotalPages > 0 ? (double)CurrentPage / TotalPages * 100 : 0;
}
