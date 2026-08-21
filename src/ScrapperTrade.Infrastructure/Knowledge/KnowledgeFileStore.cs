using System.Security.Cryptography;

namespace ScrapperTrade.Infrastructure.Knowledge;

public sealed class KnowledgeFileStore
{
    public const long DefaultMaximumBytes = 25 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".markdown", ".csv", ".json" };
    private readonly string root;
    private readonly long maximumBytes;

    public KnowledgeFileStore(string rootDirectory, long maximumBytes = DefaultMaximumBytes)
    {
        root = Path.GetFullPath(rootDirectory);
        this.maximumBytes = maximumBytes > 0 ? maximumBytes : throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        Directory.CreateDirectory(root);
    }

    public static string GetDefaultRoot(string? localApplicationData = null)
    {
        var root = localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("The per-user local application data directory is unavailable.");
        return Path.Combine(Path.GetFullPath(root), "ScrapperTrade", "knowledge", "files");
    }

    public async Task<StoredKnowledgeFile> SaveAsync(Stream input, string originalFileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(Path.GetFileName(originalFileName));
        if (!AllowedExtensions.Contains(extension)) throw new KnowledgeIngestionException("FILE_TYPE_NOT_ALLOWED", $"File type '{extension}' is not allowed.");
        var staging = Path.Combine(root, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read; if (total > maximumBytes) throw new KnowledgeIngestionException("FILE_TOO_LARGE", $"File exceeds {maximumBytes} bytes.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            string hash;
            await using (var hashInput = File.OpenRead(staging))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(hashInput, cancellationToken)).ToLowerInvariant();
            var relative = Path.Combine(hash[..2], hash + extension.ToLowerInvariant());
            var destination = Resolve(relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) File.Delete(staging); else File.Move(staging, destination);
            return new(hash, relative, new FileInfo(destination).Length, destination);
        }
        catch { if (File.Exists(staging)) File.Delete(staging); throw; }
    }

    public string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Stored path escapes the knowledge root.");
        return full;
    }

    public void Delete(string relativePath) { var path = Resolve(relativePath); if (File.Exists(path)) File.Delete(path); }
}

public sealed record StoredKnowledgeFile(string Hash, string RelativePath, long SizeBytes, string AbsolutePath);
public sealed class KnowledgeIngestionException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
