using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Infrastructure.AI;

public class AnthropicAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicAIService> _logger;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public AnthropicAIService(HttpClient httpClient, IOptions<AIOptions> options, ILogger<AnthropicAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = options.Value.Anthropic.Model;
    }

    public async Task<List<FileAnalysisResult>> AnalyzeBatchAsync(
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        string prompt,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Sending batch of {Count} files to Anthropic model {Model}", files.Count, _model);

        var requestBody = new
        {
            model = _model,
            max_tokens = 8192,
            system = AIResponseParser.SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = AIResponseParser.BuildBatchMessage(files, prompt) }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/messages", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseBody, JsonOptions);
        var rawJson = claudeResponse?.Content?.FirstOrDefault()?.Text ?? "[]";

        return AIResponseParser.ParseBatch(rawJson, files, _logger);
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public List<ContentBlock>? Content { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
