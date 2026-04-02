namespace RhealBUGTracker.Infrastructure.AI;

public class AIOptions
{
    public const string Section = "AI";

    public string Provider { get; set; } = "Demo";

    public AnthropicOptions Anthropic { get; set; } = new();
    public OpenAIOptions OpenAI { get; set; } = new();
    public GitHubAIOptions GitHub { get; set; } = new();
    public GeminiOptions Gemini { get; set; } = new();
}

public class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-6";
}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1";
}

public class GitHubAIOptions
{
    public string Token { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1";
}

public class GeminiOptions
{
    public string Token { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-pro";
}
