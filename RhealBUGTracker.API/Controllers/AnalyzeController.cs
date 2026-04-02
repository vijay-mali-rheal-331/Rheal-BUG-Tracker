using MediatR;
using Microsoft.AspNetCore.Mvc;
using RhealBUGTracker.Application.Features.Analyze;
using RhealBUGTracker.Application.Features.GitHub;
using RhealBUGTracker.Application.Interfaces;

namespace RhealBUGTracker.API.Controllers;

[ApiController]
[Route("api/analyze")]
public class AnalyzeController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AnalyzeController> _logger;
    private const long MaxUploadSize = 50 * 1024 * 1024; // 50 MB total

    public AnalyzeController(IMediator mediator, ISessionService sessionService, ILogger<AnalyzeController> logger)
    {
        _mediator = mediator;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>Upload and analyze files for a session.</summary>
    [HttpPost("files")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeFiles([FromQuery] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId is required." });

        var session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
            return NotFound(new { error = $"Session '{sessionId}' not found or expired." });

        if (!Request.Form.Files.Any())
            return BadRequest(new { error = "At least one file must be uploaded." });

        var totalSize = Request.Form.Files.Sum(f => f.Length);
        if (totalSize > MaxUploadSize)
            return BadRequest(new { error = "Total upload size exceeds 50 MB limit." });

        var files = Request.Form.Files
            .Select(f => (f.FileName, f.OpenReadStream()))
            .ToList();

        _logger.LogInformation("Received {Count} files for session {SessionId}", files.Count, sessionId);

        await _mediator.Send(new AnalyzeFilesCommand(sessionId, files), ct);

        return Accepted(new { sessionId, message = "Analysis started. Poll GET /api/sessions/{sessionId} for progress." });
    }

    /// <summary>Analyze a GitHub repository linked to the session.</summary>
    [HttpPost("github")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeGitHub([FromQuery] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId is required." });

        var session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
            return NotFound(new { error = $"Session '{sessionId}' not found or expired." });

        if (string.IsNullOrEmpty(session.RepoUrl))
            return BadRequest(new { error = "Session does not have a repository URL. Set repoUrl when creating the session." });

        await _mediator.Send(new AnalyzeGitHubCommand(sessionId), ct);

        return Accepted(new { sessionId, message = "GitHub analysis started. Poll GET /api/sessions/{sessionId} for progress." });
    }
}
