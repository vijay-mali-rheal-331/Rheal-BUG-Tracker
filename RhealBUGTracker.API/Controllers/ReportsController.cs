using Microsoft.AspNetCore.Mvc;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Enums;

namespace RhealBUGTracker.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IReportService _reportService;

    public ReportsController(ISessionService sessionService, IReportService reportService)
    {
        _sessionService = sessionService;
        _reportService = reportService;
    }

    /// <summary>Get the analysis report for a session as JSON.</summary>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetReport(string sessionId, CancellationToken ct)
    {
        var session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
            return NotFound(new { error = $"Session '{sessionId}' not found or expired." });

        if (session.Status == SessionStatus.Running || session.Status == SessionStatus.Pending)
            return Conflict(new { error = "Analysis is still in progress.", status = session.Status.ToString(), progress = session.ProcessedFiles, total = session.TotalFiles });

        if (session.Status == SessionStatus.Failed)
            return UnprocessableEntity(new { error = "Analysis failed.", details = session.ErrorMessage });

        var report = _reportService.GenerateJsonReport(session);
        return Ok(report);
    }

    /// <summary>Get the analysis report for a session as Markdown.</summary>
    [HttpGet("{sessionId}/markdown")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarkdownReport(string sessionId, CancellationToken ct)
    {
        var session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
            return NotFound(new { error = $"Session '{sessionId}' not found or expired." });

        if (session.Status != SessionStatus.Completed)
            return Conflict(new { error = "Report only available after analysis completes.", status = session.Status.ToString() });

        var markdown = _reportService.GenerateMarkdownReport(session);
        return Content(markdown, "text/markdown");
    }
}
