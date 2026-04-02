using RhealBUGTracker.Domain.Enums;

namespace RhealBUGTracker.Domain.Models;

public class FileIssue
{
    public IssueType Type { get; set; }
    public IssueSeverity Severity { get; set; }
    public int Line { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
}
