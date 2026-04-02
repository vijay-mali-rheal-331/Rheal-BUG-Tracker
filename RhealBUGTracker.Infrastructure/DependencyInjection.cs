using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Infrastructure.AI;
using RhealBUGTracker.Infrastructure.Cache;
using RhealBUGTracker.Infrastructure.FileProcessing;
using RhealBUGTracker.Infrastructure.GitHub;
using RhealBUGTracker.Infrastructure.Services;

namespace RhealBUGTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddSingleton<ISessionService, SessionCacheService>();
        services.AddSingleton<IPromptProvider, FilePromptProvider>();
        services.AddScoped<IFileProcessingService, FileProcessingService>();
        services.AddScoped<IGitHubService, GitHubService>();
        services.AddScoped<IReportService, ReportService>();

        // Bind the whole AI section once; all services read it via IOptions<AIOptions>
        services.Configure<AIOptions>(configuration.GetSection(AIOptions.Section));

        var aiOptions = configuration.GetSection(AIOptions.Section).Get<AIOptions>() ?? new AIOptions();

        RegisterAIProvider(services, aiOptions);

        return services;
    }

    private static void RegisterAIProvider(IServiceCollection services, AIOptions options)
    {
        switch (options.Provider.Trim().ToLowerInvariant())
        {
            case "anthropic":
                if (string.IsNullOrWhiteSpace(options.Anthropic.ApiKey))
                    throw new InvalidOperationException("AI:Anthropic:ApiKey is required when Provider is 'Anthropic'.");

                services.AddHttpClient<IAIService, AnthropicAIService>(client =>
                {
                    client.BaseAddress = new Uri("https://api.anthropic.com");
                    client.DefaultRequestHeaders.Add("x-api-key", options.Anthropic.ApiKey);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());
                break;

            case "openai":
                if (string.IsNullOrWhiteSpace(options.OpenAI.ApiKey))
                    throw new InvalidOperationException("AI:OpenAI:ApiKey is required when Provider is 'OpenAI'.");

                services.AddHttpClient<IAIService, OpenAIAIService>(client =>
                {
                    client.BaseAddress = new Uri("https://api.openai.com");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.OpenAI.ApiKey}");
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());
                break;

            case "github":
                if (string.IsNullOrWhiteSpace(options.GitHub.Token))
                    throw new InvalidOperationException("AI:GitHub:Token is required when Provider is 'GitHub'.");

                services.AddHttpClient<IAIService, GitHubAIService>(client =>
                {
                    client.BaseAddress = new Uri("https://models.inference.ai.azure.com");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.GitHub.Token}");
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());
                break;

            case "gemini":
                if (string.IsNullOrWhiteSpace(options.Gemini.Token))
                    throw new InvalidOperationException("AI:Gemini:Token is required when Provider is 'Gemini'.");

                services.AddHttpClient<IAIService, GeminiAIService>(client =>
                {
                    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
                    client.DefaultRequestHeaders.Add("x-goog-api-key", options.Gemini.Token);
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());
                break;

            case "demo":
                services.AddScoped<IAIService, DemoAIService>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown AI provider '{options.Provider}'. Valid values: Anthropic, OpenAI, GitHub, Gemini, Demo.");
        }
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, retryCount, _) =>
                    Console.WriteLine($"[Polly] Retry {retryCount} after {delay.TotalSeconds}s — {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}"));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
