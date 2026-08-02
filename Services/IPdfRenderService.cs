namespace NileFusion.Converter.Services;

public interface IPdfRenderService
{
    Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken = default);
    // Returns: Tuple of (PreviewImage as object/null, HighResBytes as PNG bytes)
    Task<(object? PreviewImage, byte[] HighResBytes)> RenderPageAsync(string pdfPath, uint pageIndex, double dpi = 300, CancellationToken cancellationToken = default);
}
