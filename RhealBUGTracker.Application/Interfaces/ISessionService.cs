using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Application.Interfaces;

public interface ISessionService
{
    Task<ScanSession> CreateSessionAsync(string? repoUrl, string? branch, CancellationToken ct = default);
    Task<ScanSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);
    Task UpdateSessionAsync(ScanSession session, CancellationToken ct = default);
    Task AppendResultAsync(string sessionId, FileAnalysisResult result, CancellationToken ct = default);
}
