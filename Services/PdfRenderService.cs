using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Data.Pdf;
using Windows.Storage;

namespace NileFusion.Converter.Services;

public class PdfRenderService : IPdfRenderService
{
    public async Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("PDF file not found.", pdfPath);
        }

        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(pdfPath);
            var pdfDoc = await PdfDocument.LoadFromFileAsync(storageFile);
            return (int)pdfDoc.PageCount;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load PDF document: {ex.Message}", ex);
        }
    }

    public async Task<(object? PreviewImage, byte[] HighResBytes)> RenderPageAsync(
        string pdfPath,
        uint pageIndex,
        double dpi = 300,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("PDF file not found.", pdfPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var storageFile = await StorageFile.GetFileFromPathAsync(pdfPath);
        var pdfDoc = await PdfDocument.LoadFromFileAsync(storageFile);

        if (pageIndex >= pdfDoc.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index is out of range.");
        }

        using var page = pdfDoc.GetPage(pageIndex);

        // PDF base DPI is 72. Calculate pixel dimensions for target DPI (e.g. 300 DPI for OCR).
        uint targetWidth = (uint)Math.Max(100, page.Size.Width * (dpi / 72.0));
        uint targetHeight = (uint)Math.Max(100, page.Size.Height * (dpi / 72.0));

        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var options = new PdfPageRenderOptions
        {
            DestinationWidth = targetWidth,
            DestinationHeight = targetHeight
        };

        await page.RenderToStreamAsync(stream, options);

        cancellationToken.ThrowIfCancellationRequested();

        byte[] rawBytes = new byte[stream.Size];
        using (var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0)))
        {
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(rawBytes);
        }

        // Convert raw PNG bytes to Bitmap for UI preview
        Bitmap? previewBitmap = null;
        try
        {
            using var ms = new MemoryStream(rawBytes);
            previewBitmap = new Bitmap(ms);
        }
        catch
        {
            // If conversion fails, preview is null
        }

        // re-encode as PNG for Tesseract OCR
        byte[] pngBytes = rawBytes; // rawBytes are already PNG from PDF rendering

        // Return bitmap as object for WinForms preview, PNG bytes for OCR
        return ((object?)previewBitmap, pngBytes);
    }
}

