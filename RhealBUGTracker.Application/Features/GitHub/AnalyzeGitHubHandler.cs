using MediatR;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Enums;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Application.Features.GitHub;

public class AnalyzeGitHubHandler : IRequestHandler<AnalyzeGitHubCommand, string>
{
    private readonly ISessionService _sessionService;
    private readonly IAIService _aiService;
    private readonly IGitHubService _gitHubService;
    private readonly IFileProcessingService _fileProcessingService;
    private readonly IPromptProvider _promptProvider;
    private readonly ILogger<AnalyzeGitHubHandler> _logger;

    public AnalyzeGitHubHandler(
        ISessionService sessionService,
        IAIService aiService,
        IGitHubService gitHubService,
        IFileProcessingService fileProcessingService,
        IPromptProvider promptProvider,
        ILogger<AnalyzeGitHubHandler> logger)
    {
        _sessionService = sessionService;
        _aiService = aiService;
        _gitHubService = gitHubService;
        _fileProcessingService = fileProcessingService;
        _promptProvider = promptProvider;
        _logger = logger;
    }

    public async Task<string> Handle(AnalyzeGitHubCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionAsync(request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {request.SessionId} not found.");

        if (string.IsNullOrEmpty(session.RepoUrl))
            throw new InvalidOperationException("Session does not have a repository URL.");

        session.Status = SessionStatus.Running;
        await _sessionService.UpdateSessionAsync(session, cancellationToken);

        _ = Task.Run(() => ProcessGitHubBackgroundAsync(session, cancellationToken), CancellationToken.None);

        return request.SessionId;
    }

    private async Task ProcessGitHubBackgroundAsync(ScanSession session, CancellationToken ct)
    {
        string? clonePath = null;
        try
        {
            _logger.LogInformation("Cloning repository {RepoUrl} for session {SessionId}", session.RepoUrl, session.SessionId);
            clonePath = await _gitHubService.CloneRepositoryAsync(session.RepoUrl!, session.Branch, ct);

            // Collect all file paths from the repo
            var filePaths = new List<string>();
            await foreach (var path in _fileProcessingService.GetRepositoryFilesAsync(clonePath, ct))
                filePaths.Add(path);

            _logger.LogInformation("Found {Count} files in session {SessionId}", filePaths.Count, session.SessionId);

            // Read all file contents
            var allFiles = new List<(string FileName, string FileType, string Content)>();
            await foreach (var (fileName, fileType, fileChunks) in _fileProcessingService.ProcessFilesAsync(filePaths, ct))
            {
                var chunks = new List<string>();
                await foreach (var chunk in fileChunks)
                    chunks.Add(chunk);

                var combinedContent = string.Join("\n\n--- CHUNK ---\n\n", chunks);
                allFiles.Add((fileName, fileType, combinedContent));
            }

            session.TotalFiles = allFiles.Count;
            await _sessionService.UpdateSessionAsync(session, ct);

            _logger.LogInformation("Sending batch of {Count} files to AI for session {SessionId}", allFiles.Count, session.SessionId);

            // Prompt is loaded from the bundled .md file — not supplied by the user
            var prompt = _promptProvider.GetPrompt();
            var results = await _aiService.AnalyzeBatchAsync(allFiles, prompt, ct);

            foreach (var result in results)
            {
                await _sessionService.AppendResultAsync(session.SessionId, result, ct);
                session.ProcessedFiles++;
                await _sessionService.UpdateSessionAsync(session, ct);
            }

            session.Status = SessionStatus.Completed;
            await _sessionService.UpdateSessionAsync(session, ct);
            _logger.LogInformation("Session {SessionId} GitHub analysis completed. Analyzed {Count} files.", session.SessionId, session.TotalFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} failed during GitHub analysis", session.SessionId);
            session.Status = SessionStatus.Failed;
            session.ErrorMessage = ex.Message;
            await _sessionService.UpdateSessionAsync(session, CancellationToken.None);
        }
        finally
        {
            if (clonePath is not null)
            {
                _gitHubService.Cleanup(clonePath);
                _logger.LogInformation("Cleaned up temp directory {ClonePath}", clonePath);
            }
        }
    }
}
