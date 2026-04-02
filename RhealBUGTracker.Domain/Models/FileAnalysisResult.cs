namespace RhealBUGTracker.Domain.Models;

public class FileAnalysisResult
{
    public string File { get; set; } = string.Empty;
    public List<FileIssue> Issues { get; set; } = new();
}
