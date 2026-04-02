using MediatR;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.DTOs;
using RhealBUGTracker.Application.Interfaces;

namespace RhealBUGTracker.Application.Features.Sessions;

public class CreateSessionHandler : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<CreateSessionHandler> _logger;

    public CreateSessionHandler(ISessionService sessionService, ILogger<CreateSessionHandler> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<CreateSessionResponse> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.CreateSessionAsync(request.RepoUrl, request.Branch, cancellationToken);

        _logger.LogInformation("Session {SessionId} created successfully", session.SessionId);

        return new CreateSessionResponse
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt
        };
    }
}
