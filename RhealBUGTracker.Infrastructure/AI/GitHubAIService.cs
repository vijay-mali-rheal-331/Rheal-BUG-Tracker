using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Infrastructure.AI;

/// <summary>
/// Uses the GitHub Models inference endpoint, which is OpenAI-compatible.
/// Base URL: https://models.inference.ai.azure.com
/// Auth: Bearer &lt;GitHub PAT&gt;
/// </summary>
public class GitHubAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubAIService> _logger;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubAIService(HttpClient httpClient, IOptions<AIOptions> options, ILogger<GitHubAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = options.Value.GitHub.Model;
    }

    public async Task<List<FileAnalysisResult>> AnalyzeBatchAsync(
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        string prompt,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Sending batch of {Count} files to GitHub Models model {Model}", files.Count, _model);

        var requestBody = new
        {
            model = _model,
            max_tokens = 8192,
            messages = new[]
            {
                new { role = "system", content = AIResponseParser.SystemPrompt },
                new { role = "user", content = AIResponseParser.BuildBatchMessage(files, prompt) }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, JsonOptions);
        var rawJson = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "[]";

        return AIResponseParser.ParseBatch(rawJson, files, _logger);
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
