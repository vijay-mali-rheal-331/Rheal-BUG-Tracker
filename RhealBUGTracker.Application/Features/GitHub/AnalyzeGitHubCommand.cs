using MediatR;

namespace RhealBUGTracker.Application.Features.GitHub;

public record AnalyzeGitHubCommand(string SessionId) : IRequest<string>;
