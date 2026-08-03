namespace NileFusion.BookConverter.Services;

public interface IOcrService
{
    string EngineName { get; }
    Task InitializeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<string> ProcessImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
