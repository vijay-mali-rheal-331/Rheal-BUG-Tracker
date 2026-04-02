using RhealBUGTracker.Domain.Enums;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Application.DTOs;

public class AnalysisReportDto
{
    public string SessionId { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int TotalIssues { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public List<FileAnalysisResult> FileResults { get; set; } = new();
}
