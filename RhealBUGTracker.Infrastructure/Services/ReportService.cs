using System.Text;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Enums;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Infrastructure.Services;

public class ReportService : IReportService
{
    public string GenerateMarkdownReport(ScanSession session)
    {
        var sb = new StringBuilder();
        var allIssues = session.Results.SelectMany(r => r.Issues).ToList();

        sb.AppendLine("# RhealBUGTracker Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"**Session ID:** {session.SessionId}");
        sb.AppendLine($"**Status:** {session.Status}");
        sb.AppendLine($"**Created:** {session.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Files Analyzed:** {session.ProcessedFiles} / {session.TotalFiles}");
        sb.AppendLine($"**Total Issues:** {allIssues.Count}");
        sb.AppendLine();

        sb.AppendLine("## Issue Summary");
        sb.AppendLine();
        sb.AppendLine("| Severity | Count |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| Critical | {allIssues.Count(i => i.Severity == IssueSeverity.Critical)} |");
        sb.AppendLine($"| High     | {allIssues.Count(i => i.Severity == IssueSeverity.High)} |");
        sb.AppendLine($"| Medium   | {allIssues.Count(i => i.Severity == IssueSeverity.Medium)} |");
        sb.AppendLine($"| Low      | {allIssues.Count(i => i.Severity == IssueSeverity.Low)} |");
        sb.AppendLine();

        foreach (var fileResult in session.Results)
        {
            if (!fileResult.Issues.Any()) continue;

            sb.AppendLine($"## {fileResult.File}");
            sb.AppendLine();

            foreach (var issue in fileResult.Issues.OrderByDescending(i => i.Severity))
            {
                sb.AppendLine($"### [{issue.Severity}] {issue.Type} — Line {issue.Line}");
                sb.AppendLine();
                sb.AppendLine($"**Description:** {issue.Description}");
                sb.AppendLine();
                sb.AppendLine($"**Suggestion:** {issue.Suggestion}");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public object GenerateJsonReport(ScanSession session)
    {
        var allIssues = session.Results.SelectMany(r => r.Issues).ToList();
        return new
        {
            sessionId = session.SessionId,
            status = session.Status.ToString(),
            createdAt = session.CreatedAt,
            totalFiles = session.TotalFiles,
            processedFiles = session.ProcessedFiles,
            summary = new
            {
                totalIssues = allIssues.Count,
                critical = allIssues.Count(i => i.Severity == IssueSeverity.Critical),
                high = allIssues.Count(i => i.Severity == IssueSeverity.High),
                medium = allIssues.Count(i => i.Severity == IssueSeverity.Medium),
                low = allIssues.Count(i => i.Severity == IssueSeverity.Low)
            },
            files = session.Results.Select(r => new
            {
                file = r.File,
                issueCount = r.Issues.Count,
                issues = r.Issues.Select(i => new
                {
                    type = i.Type.ToString(),
                    severity = i.Severity.ToString(),
                    line = i.Line,
                    description = i.Description,
                    suggestion = i.Suggestion
                })
            })
        };
    }
}
