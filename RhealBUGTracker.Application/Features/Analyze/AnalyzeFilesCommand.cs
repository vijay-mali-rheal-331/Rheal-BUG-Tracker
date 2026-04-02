using MediatR;

namespace RhealBUGTracker.Application.Features.Analyze;

public record AnalyzeFilesCommand(string SessionId, IEnumerable<(string FileName, Stream Stream)> Files) : IRequest<string>;
