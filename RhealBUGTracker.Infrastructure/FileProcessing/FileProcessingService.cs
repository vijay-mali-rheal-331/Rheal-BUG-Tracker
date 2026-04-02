using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RhealBUGTracker.Application.Interfaces;

namespace RhealBUGTracker.Infrastructure.FileProcessing;

public class FileProcessingService : IFileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB per file
    private const int ChunkSize = 8000; // ~8K chars per chunk

    /// <summary>
    /// Directories that are always skipped regardless of .gitignore.
    /// Covers dependency stores, build outputs, IDE artifacts, and temp folders
    /// across Node, .NET, Java, Python, Ruby, Go, Rust, PHP, Terraform and Angular ecosystems.
    /// </summary>
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Dependencies
        "node_modules", "bower_components", "vendor", "packages",
        // .NET
        "bin", "obj", "publish",
        // Java / Gradle / Maven
        "target", ".gradle", ".mvn",
        // Python
        "__pycache__", ".venv", "venv", "env", ".tox",
        // Ruby
        ".bundle",
        // Go
        "vendor",
        // Rust
        "target",
        // PHP
        "vendor",
        // Frontend build outputs
        "dist", "out", "build", ".next", ".nuxt", ".svelte-kit",
        // Angular
        ".angular",
        // Coverage / test artifacts
        "coverage", ".nyc_output",
        // VCS / IDE
        ".git", ".svn", ".hg",
        ".vs", ".vscode", ".idea", ".fleet",
        // Cloud / IaC
        ".terraform",
        // Temp / cache
        "tmp", "temp", ".cache",
        // Migrations (usually not useful for bug review)
        "migrations",
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".go", ".rb", ".php",
        ".cpp", ".c", ".h", ".swift", ".kt", ".rs", ".vue", ".html", ".css", ".scss",
        ".sql", ".sh", ".yaml", ".yml", ".json", ".xml", ".config"
    };

    public FileProcessingService(ILogger<FileProcessingService> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<(string FileName, string FileType, IAsyncEnumerable<string> Chunks)> ProcessUploadedFilesAsync(
        IEnumerable<(string FileName, Stream Stream)> files,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var (fileName, stream) in files)
        {
            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".zip")
            {
                await foreach (var entry in ProcessZipStreamAsync(stream, ct))
                    yield return entry;
            }
            else if (SupportedExtensions.Contains(ext))
            {
                yield return (fileName, GetFileType(ext), ReadStreamChunksAsync(stream, ct));
            }
            else
            {
                _logger.LogDebug("Skipping unsupported file: {FileName}", fileName);
            }
        }
    }

    public async IAsyncEnumerable<(string FileName, string FileType, IAsyncEnumerable<string> Chunks)> ProcessFilesAsync(
        IEnumerable<string> filePaths,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var filePath in filePaths)
        {
            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext)) continue;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning("Skipping large file {FilePath} ({Size} bytes)", filePath, fileInfo.Length);
                continue;
            }

            yield return (Path.GetFileName(filePath), GetFileType(ext), ReadFileChunksAsync(filePath, ct));
        }
    }

    public async IAsyncEnumerable<string> GetRepositoryFilesAsync(
        string rootPath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Load .gitignore patterns from the root if present
        var gitignorePatterns = LoadGitignorePatterns(rootPath);

        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = queue.Dequeue();

            var dirName = Path.GetFileName(current);
            if (IgnoredDirectories.Contains(dirName)) continue;

            // Check against .gitignore patterns (relative path from root)
            var relDir = Path.GetRelativePath(rootPath, current).Replace('\\', '/');
            if (IsGitignored(relDir, gitignorePatterns)) continue;

            foreach (var file in Directory.EnumerateFiles(current))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!SupportedExtensions.Contains(ext)) continue;

                var relFile = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
                if (!IsGitignored(relFile, gitignorePatterns))
                    yield return file;
            }

            foreach (var subDir in Directory.EnumerateDirectories(current))
                queue.Enqueue(subDir);
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ChunkContentAsync(
        string content,
        int chunkSize = ChunkSize)
    {
        for (var i = 0; i < content.Length; i += chunkSize)
        {
            yield return content[i..Math.Min(i + chunkSize, content.Length)];
            await Task.Yield();
        }
    }

    // ── .gitignore helpers ──────────────────────────────────────────────────

    private static List<Regex> LoadGitignorePatterns(string rootPath)
    {
        var patterns = new List<Regex>();
        var gitignorePath = Path.Combine(rootPath, ".gitignore");
        if (!File.Exists(gitignorePath)) return patterns;

        foreach (var line in File.ReadLines(gitignorePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

            // Convert gitignore glob to regex
            try
            {
                var regex = GlobToRegex(trimmed);
                patterns.Add(regex);
            }
            catch
            {
                // Ignore malformed patterns
            }
        }

        return patterns;
    }

    private static bool IsGitignored(string relativePath, List<Regex> patterns)
    {
        if (patterns.Count == 0) return false;

        var normalised = relativePath.TrimStart('/');
        return patterns.Any(p => p.IsMatch(normalised));
    }

    /// <summary>
    /// Converts a .gitignore glob pattern to a <see cref="Regex"/>.
    /// Handles the most common cases: *, **, ?, leading /, directory patterns.
    /// </summary>
    private static Regex GlobToRegex(string glob)
    {
        // Negation patterns (!) are intentionally NOT supported — we only exclude, not re-include
        var anchored = glob.StartsWith('/');
        var pattern  = anchored ? glob[1..] : glob;

        // Escape dots and other regex metacharacters except * and ?
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*\*", "§DBLSTAR§")
            .Replace(@"\*",   "[^/]*")
            .Replace(@"\?",   "[^/]")
            .Replace("§DBLSTAR§", ".*");

        // If the pattern ends with / it matches directories
        if (escaped.EndsWith('/'))
            escaped += ".*";

        var regexPattern = anchored
            ? $"^{escaped}"
            : $"(^|/){escaped}";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    // ── Zip / stream helpers ────────────────────────────────────────────────

    private async IAsyncEnumerable<(string FileName, string FileType, IAsyncEnumerable<string> Chunks)> ProcessZipStreamAsync(
        Stream zipStream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Length == 0) continue; // directory entry

            var entryPath = entry.FullName.Replace('\\', '/');
            if (entryPath.Split('/').Any(part => IgnoredDirectories.Contains(part))) continue;

            var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext)) continue;
            if (entry.Length > MaxFileSizeBytes) continue;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms, ct);
            var content = System.Text.Encoding.UTF8.GetString(ms.ToArray());

            yield return (entry.FullName, GetFileType(ext), ChunkContentAsync(content));
        }
    }

    private async IAsyncEnumerable<string> ReadStreamChunksAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var buffer = new char[ChunkSize];
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            yield return new string(buffer, 0, read);
        }
    }

    private async IAsyncEnumerable<string> ReadFileChunksAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var reader = new StreamReader(fs);
        var buffer = new char[ChunkSize];
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            yield return new string(buffer, 0, read);
        }
    }

    private static string GetFileType(string extension) => extension.TrimStart('.').ToUpperInvariant();
}
