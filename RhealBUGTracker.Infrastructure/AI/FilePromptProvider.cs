using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;

namespace RhealBUGTracker.Infrastructure.AI;

/// <summary>
/// Reads the analysis prompt from Assets/RhealBUGTrackerPrompt.md (relative to the
/// application content root) once at startup and caches it for the lifetime of the app.
/// </summary>
public class FilePromptProvider : IPromptProvider
{
    private readonly string _prompt;

    public FilePromptProvider(IHostEnvironment env, ILogger<FilePromptProvider> logger)
    {
        var fullPath = Path.Combine(env.ContentRootPath, "Assets", "RhealBUGTrackerPrompt.md");

        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Prompt file not found at {Path}. Analysis will proceed with no system prompt.", fullPath);
            _prompt = string.Empty;
        }
        else
        {
            _prompt = File.ReadAllText(fullPath);
            logger.LogInformation("Loaded analysis prompt from {Path} ({Length} chars)", fullPath, _prompt.Length);
        }
    }

    public string GetPrompt() => _prompt;
}
