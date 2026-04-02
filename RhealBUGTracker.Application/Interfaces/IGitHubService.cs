namespace RhealBUGTracker.Application.Interfaces;

public interface IGitHubService
{
    Task<string> CloneRepositoryAsync(string repoUrl, string? branch, CancellationToken ct = default);
    void Cleanup(string localPath);
}
