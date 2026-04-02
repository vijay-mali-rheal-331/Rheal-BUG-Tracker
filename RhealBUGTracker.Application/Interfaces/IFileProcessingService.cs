namespace RhealBUGTracker.Application.Interfaces;

public interface IFileProcessingService
{
    IAsyncEnumerable<(string FileName, string FileType, IAsyncEnumerable<string> Chunks)> ProcessFilesAsync(IEnumerable<string> filePaths, CancellationToken ct = default);
    IAsyncEnumerable<(string FileName, string FileType, IAsyncEnumerable<string> Chunks)> ProcessUploadedFilesAsync(IEnumerable<(string FileName, Stream Stream)> files, CancellationToken ct = default);
    IAsyncEnumerable<string> GetRepositoryFilesAsync(string rootPath, CancellationToken ct = default);
    IAsyncEnumerable<string> ChunkContentAsync(string content, int chunkSize = 8000);
}
