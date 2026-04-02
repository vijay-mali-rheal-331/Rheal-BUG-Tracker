using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;

namespace RhealBUGTracker.Infrastructure.GitHub;

public class GitHubService : IGitHubService
{
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(ILogger<GitHubService> logger)
    {
        _logger = logger;
    }

    public Task<string> CloneRepositoryAsync(string repoUrl, string? branch, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "rheal-bugtracker", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        _logger.LogInformation("Cloning {RepoUrl} (branch: {Branch}) to {TempPath}", repoUrl, branch ?? "default", tempPath);

        var cloneOptions = new CloneOptions();
        if (!string.IsNullOrEmpty(branch))
            cloneOptions.BranchName = branch;

        // LibGit2Sharp clone is synchronous; offload to thread pool
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            Repository.Clone(repoUrl, tempPath, cloneOptions);
            _logger.LogInformation("Clone complete: {TempPath}", tempPath);
            return tempPath;
        }, ct);
    }

    public void Cleanup(string localPath)
    {
        try
        {
            if (!Directory.Exists(localPath)) return;

            // Git repos have read-only files; force-delete
            foreach (var file in Directory.GetFiles(localPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(localPath, recursive: true);
            _logger.LogInformation("Cleaned up {LocalPath}", localPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp directory {LocalPath}", localPath);
        }
    }
}
