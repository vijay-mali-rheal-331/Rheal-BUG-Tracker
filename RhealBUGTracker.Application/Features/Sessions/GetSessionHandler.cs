using MediatR;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.DTOs;
using RhealBUGTracker.Application.Interfaces;

namespace RhealBUGTracker.Application.Features.Sessions;

public class GetSessionHandler : IRequestHandler<GetSessionQuery, SessionDto?>
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<GetSessionHandler> _logger;

    public GetSessionHandler(ISessionService sessionService, ILogger<GetSessionHandler> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<SessionDto?> Handle(GetSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found", request.SessionId);
            return null;
        }

        return new SessionDto
        {
            SessionId = session.SessionId,
            RepoUrl = session.RepoUrl,
            Branch = session.Branch,
            Status = session.Status,
            TotalFiles = session.TotalFiles,
            ProcessedFiles = session.ProcessedFiles,
            CreatedAt = session.CreatedAt,
            ErrorMessage = session.ErrorMessage
        };
    }
}
