using MediatR;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Enums;
using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Application.Features.Analyze;

public class AnalyzeFilesHandler : IRequestHandler<AnalyzeFilesCommand, string>
{
    private readonly ISessionService _sessionService;
    private readonly IAIService _aiService;
    private readonly IFileProcessingService _fileProcessingService;
    private readonly IPromptProvider _promptProvider;
    private readonly ILogger<AnalyzeFilesHandler> _logger;

    public AnalyzeFilesHandler(
        ISessionService sessionService,
        IAIService aiService,
        IFileProcessingService fileProcessingService,
        IPromptProvider promptProvider,
        ILogger<AnalyzeFilesHandler> logger)
    {
        _sessionService = sessionService;
        _aiService = aiService;
        _fileProcessingService = fileProcessingService;
        _promptProvider = promptProvider;
        _logger = logger;
    }

    public async Task<string> Handle(AnalyzeFilesCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionAsync(request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {request.SessionId} not found.");

        session.Status = SessionStatus.Running;
        await _sessionService.UpdateSessionAsync(session, cancellationToken);

        // Fire and forget background processing
        _ = Task.Run(() => ProcessFilesBackgroundAsync(session, request.Files, cancellationToken), CancellationToken.None);

        return request.SessionId;
    }

    private async Task ProcessFilesBackgroundAsync(
        ScanSession session,
        IEnumerable<(string FileName, Stream Stream)> files,
        CancellationToken ct)
    {
        try
        {
            var fileList = files.ToList();

            // Collect ALL file contents first
            var allFiles = new List<(string FileName, string FileType, string Content)>();

            await foreach (var (fileName, fileType, chunks) in _fileProcessingService.ProcessUploadedFilesAsync(fileList, ct))
            {
                var chunkList = new List<string>();
                await foreach (var chunk in chunks)
                    chunkList.Add(chunk);

                var combinedContent = string.Join("\n\n--- CHUNK ---\n\n", chunkList);
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
            _logger.LogInformation("Session {SessionId} completed. Analyzed {Count} files.", session.SessionId, session.TotalFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} failed during file analysis", session.SessionId);
            session.Status = SessionStatus.Failed;
            session.ErrorMessage = ex.Message;
            await _sessionService.UpdateSessionAsync(session, CancellationToken.None);
        }
    }
}
