using System.Windows.Media.Imaging;

namespace ArabicPdfOcrApp.Services;

public interface IPdfRenderService
{
    Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken = default);
    Task<(BitmapSource PreviewImage, byte[] HighResBytes)> RenderPageAsync(string pdfPath, uint pageIndex, double dpi = 300, CancellationToken cancellationToken = default);
}
