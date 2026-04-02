using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;
using RhealBUGTracker.Domain.Models;
using System.Collections.Concurrent;

namespace RhealBUGTracker.Infrastructure.Cache;

public class SessionCacheService : ISessionService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionCacheService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(1);

    public SessionCacheService(IMemoryCache cache, ILogger<SessionCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<ScanSession> CreateSessionAsync(string? repoUrl, string? branch, CancellationToken ct = default)
    {
        var session = new ScanSession
        {
            RepoUrl = repoUrl,
            Branch = branch
        };

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(SessionTtl)
            .RegisterPostEvictionCallback((key, _, _, _) =>
            {
                _sessionLocks.TryRemove(key.ToString()!, out var sem);
                sem?.Dispose();
                _logger.LogInformation("Session {SessionId} expired and evicted from cache", key);
            });

        _cache.Set(CacheKey(session.SessionId), session, options);
        _sessionLocks.TryAdd(session.SessionId, new SemaphoreSlim(1, 1));

        _logger.LogInformation("Session {SessionId} stored in cache with TTL {Ttl}", session.SessionId, SessionTtl);
        return Task.FromResult(session);
    }

    public Task<ScanSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _cache.TryGetValue(CacheKey(sessionId), out ScanSession? session);
        return Task.FromResult(session);
    }

    public async Task UpdateSessionAsync(ScanSession session, CancellationToken ct = default)
    {
        var sem = _sessionLocks.GetOrAdd(session.SessionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey(session.SessionId), out ScanSession? _))
            {
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(SessionTtl);
                _cache.Set(CacheKey(session.SessionId), session, options);
            }
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task AppendResultAsync(string sessionId, FileAnalysisResult result, CancellationToken ct = default)
    {
        var sem = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey(sessionId), out ScanSession? session) && session is not null)
            {
                session.Results.Add(result);
                var options = new MemoryCacheEntryOptions().SetAbsoluteExpiration(SessionTtl);
                _cache.Set(CacheKey(sessionId), session, options);
            }
        }
        finally
        {
            sem.Release();
        }
    }

    private static string CacheKey(string sessionId) => $"session:{sessionId}";
}
