using RhealBUGTracker.Domain.Enums;

namespace RhealBUGTracker.Application.DTOs;

public class SessionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string? RepoUrl { get; set; }
    public string? Branch { get; set; }
    public SessionStatus Status { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public double ProgressPercent => TotalFiles == 0 ? 0 : Math.Round((double)ProcessedFiles / TotalFiles * 100, 1);
}
