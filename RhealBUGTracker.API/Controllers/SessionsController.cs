using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RhealBUGTracker.Application.DTOs;
using RhealBUGTracker.Application.Features.Sessions;

namespace RhealBUGTracker.API.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IValidator<CreateSessionRequest> _validator;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(IMediator mediator, IValidator<CreateSessionRequest> validator, ILogger<SessionsController> logger)
    {
        _mediator = mediator;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>Creates a new scan session.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }));

        var response = await _mediator.Send(new CreateSessionCommand(request.RepoUrl, request.Branch), ct);
        return CreatedAtAction(nameof(GetSession), new { sessionId = response.SessionId }, response);
    }

    /// <summary>Gets the status of a scan session.</summary>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(string sessionId, CancellationToken ct)
    {
        var session = await _mediator.Send(new GetSessionQuery(sessionId), ct);
        if (session is null)
            return NotFound(new { error = $"Session '{sessionId}' not found or expired." });

        return Ok(session);
    }
}
