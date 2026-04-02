using MediatR;
using RhealBUGTracker.Application.DTOs;

namespace RhealBUGTracker.Application.Features.Sessions;

public record CreateSessionCommand(string? RepoUrl, string? Branch) : IRequest<CreateSessionResponse>;
