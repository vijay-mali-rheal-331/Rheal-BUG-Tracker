namespace RhealBUGTracker.Application.Interfaces;

/// <summary>
/// Provides the fixed analysis prompt loaded from the bundled .md file.
/// The prompt is read once at startup and reused for every analysis request.
/// </summary>
public interface IPromptProvider
{
    string GetPrompt();
}
