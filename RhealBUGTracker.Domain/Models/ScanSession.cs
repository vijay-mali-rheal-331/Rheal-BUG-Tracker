using RhealBUGTracker.Domain.Enums;

namespace RhealBUGTracker.Domain.Models;

public class ScanSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string? RepoUrl { get; set; }
    public string? Branch { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Pending;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<FileAnalysisResult> Results { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
