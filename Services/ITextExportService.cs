namespace NileFusion.BookConverter.Services;

public interface ITextExportService
{
    Task SaveTextAsync(string filePath, string text, CancellationToken cancellationToken = default);
}
