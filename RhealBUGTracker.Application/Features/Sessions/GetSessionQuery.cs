using MediatR;
using RhealBUGTracker.Application.DTOs;

namespace RhealBUGTracker.Application.Features.Sessions;

public record GetSessionQuery(string SessionId) : IRequest<SessionDto?>;
