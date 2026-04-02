using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Domain.Enums;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Infrastructure.AI;

internal static class AIResponseParser
{
    /// <summary>
    /// System instruction: one batch request covers ALL files.
    /// Response must be a JSON array — one object per file.
    /// </summary>
    internal static readonly string SystemPrompt = """
        You are a senior code reviewer and security expert.
        You will receive multiple source files in a single request along with the user's analysis instructions.
        Analyze ALL files and return ONLY a valid JSON array (no markdown, no explanation) with this exact structure:
        [
          {
            "file": "<exact filename as provided>",
            "issues": [
              {
                "type": "Bug|Validation|Security|Performance|CodeSmell|EdgeCase",
                "severity": "Critical|High|Medium|Low",
                "line": <line number or 0 if unknown>,
                "description": "<clear description of the issue>",
                "suggestion": "<actionable fix>"
              }
            ]
          }
        ]
        Include an entry for every file, even if it has zero issues (use an empty issues array).
        Do NOT output anything outside the JSON array.
        """;

    /// <summary>
    /// Builds one message that contains the user's prompt + every file's content.
    /// Prompt is attached once at the top — never repeated per file.
    /// </summary>
    internal static string BuildBatchMessage(
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        string prompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Analysis Instructions");
        sb.AppendLine();
        sb.AppendLine(prompt.Trim());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Files to Analyze");
        sb.AppendLine();

        foreach (var (fileName, fileType, content) in files)
        {
            sb.AppendLine($"## File: `{fileName}` ({fileType})");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Parses a JSON array response into per-file results.</summary>
    internal static List<FileAnalysisResult> ParseBatch(
        string rawJson,
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        ILogger logger)
    {
        try
        {
            var json = StripFences(rawJson);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Expect a top-level array
            var results = new List<FileAnalysisResult>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                    results.Add(ParseFileResult(item, logger));
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Graceful fallback: model returned a single object instead of array
                results.Add(ParseFileResult(root, logger));
            }

            // Ensure every submitted file has an entry
            foreach (var (fileName, _, _) in files)
            {
                if (!results.Any(r => r.File.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    results.Add(new FileAnalysisResult { File = fileName });
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse batch AI response. Raw snippet: {Raw}",
                rawJson[..Math.Min(300, rawJson.Length)]);

            // Return empty results for all files so the session still completes
            return files.Select(f => new FileAnalysisResult { File = f.FileName }).ToList();
        }
    }

    private static FileAnalysisResult ParseFileResult(JsonElement item, ILogger logger)
    {
        var result = new FileAnalysisResult
        {
            File = item.TryGetProperty("file", out var fileProp) ? fileProp.GetString() ?? string.Empty : string.Empty
        };

        if (item.TryGetProperty("issues", out var issuesEl))
        {
            foreach (var issue in issuesEl.EnumerateArray())
            {
                result.Issues.Add(new FileIssue
                {
                    Type     = ParseEnum(issue, "type",     IssueType.Bug),
                    Severity = ParseEnum(issue, "severity", IssueSeverity.Low),
                    Line     = issue.TryGetProperty("line", out var lineEl) ? lineEl.GetInt32() : 0,
                    Description = GetString(issue, "description"),
                    Suggestion  = GetString(issue, "suggestion")
                });
            }
        }

        return result;
    }

    private static string StripFences(string raw)
    {
        var json = raw.Trim();
        if (!json.StartsWith("```")) return json;
        var start = json.IndexOf('\n') + 1;
        var end   = json.LastIndexOf("```");
        return end > start ? json[start..end].Trim() : json;
    }

    private static T ParseEnum<T>(JsonElement el, string property, T defaultValue) where T : struct, Enum
    {
        if (el.TryGetProperty(property, out var prop) &&
            Enum.TryParse<T>(prop.GetString(), true, out var val))
            return val;
        return defaultValue;
    }

    private static string GetString(JsonElement el, string property)
        => el.TryGetProperty(property, out var prop) ? prop.GetString() ?? string.Empty : string.Empty;
}
