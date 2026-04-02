using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Application.Interfaces;

/// <summary>
/// Sends ALL files in one batch request so the prompt is attached once,
/// not repeated per-file. The AI returns issues for every file in a single response.
/// </summary>
public interface IAIService
{
    Task<List<FileAnalysisResult>> AnalyzeBatchAsync(
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        string prompt,
        CancellationToken ct = default);
}
