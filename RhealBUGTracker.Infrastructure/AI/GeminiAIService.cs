using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Infrastructure.AI;

/// <summary>
/// Calls the Google Gemini generateContent REST API.
/// Base URL : https://generativelanguage.googleapis.com
/// Auth      : x-goog-api-key header
/// </summary>
public class GeminiAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAIService> _logger;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiAIService(HttpClient httpClient, IOptions<AIOptions> options, ILogger<GeminiAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = options.Value.Gemini.Model;
    }

    public async Task<List<FileAnalysisResult>> AnalyzeBatchAsync(
        IReadOnlyList<(string FileName, string FileType, string Content)> files,
        string prompt,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Sending batch of {Count} files to Gemini model {Model}", files.Count, _model);

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = AIResponseParser.SystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = AIResponseParser.BuildBatchMessage(files, prompt) } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/v1beta/models/{_model}:generateContent", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody, JsonOptions);
        var rawJson = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "[]";

        return AIResponseParser.ParseBatch(rawJson, files, _logger);
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
