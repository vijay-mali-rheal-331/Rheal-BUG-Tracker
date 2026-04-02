using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Enums;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Infrastructure.AI;

/// <summary>
/// Demo provider — returns realistic mock issues without calling any external API.
/// Use Provider = "Demo" in config for local development or CI without API keys.
/// </summary>
public class DemoAIService : IAIService
{
    private readonly ILogger<DemoAIService> _logger;

    public DemoAIService(ILogger<DemoAIService> logger)
    {
        _logger = logger;
    }

    public Task<List<FileAnalysisResult>> AnalyzeBatchAsync(
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        string prompt,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[Demo] Simulating batch AI analysis for {Count} files", files.Count);

        var results = files.Select(f => new FileAnalysisResult
        {
            File = f.FileName,
            Issues =
            [
                new FileIssue
                {
                    Type = IssueType.Security,
                    Severity = IssueSeverity.High,
                    Line = 12,
                    Description = "[DEMO] Potential hardcoded secret or unvalidated input detected.",
                    Suggestion = "Use environment variables or a secrets manager. Validate all inputs at the boundary."
                },
                new FileIssue
                {
                    Type = IssueType.Bug,
                    Severity = IssueSeverity.Medium,
                    Line = 34,
                    Description = "[DEMO] Null reference may occur if the collection is empty before iteration.",
                    Suggestion = "Add a null/empty guard before iterating or use null-conditional operators."
                },
                new FileIssue
                {
                    Type = IssueType.Performance,
                    Severity = IssueSeverity.Low,
                    Line = 58,
                    Description = "[DEMO] Synchronous I/O call inside an async method reduces throughput.",
                    Suggestion = "Replace with the async equivalent (e.g., ReadAllTextAsync instead of ReadAllText)."
                }
            ]
        }).ToList();

        return Task.FromResult(results);
    }
}
