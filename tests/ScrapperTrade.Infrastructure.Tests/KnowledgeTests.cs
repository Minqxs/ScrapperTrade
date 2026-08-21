using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScrapperTrade.Infrastructure.Knowledge;
using ScrapperTrade.Infrastructure.Persistence;
using Xunit;

namespace ScrapperTrade.Infrastructure.Tests;

public sealed class KnowledgeTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ScrapperTrade.Knowledge.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "test.db");
    private string FilesPath => Path.Combine(directory, "files");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); return Task.CompletedTask; }

    [Fact]
    public async Task File_store_enforces_allowlist_size_and_content_addressing()
    {
        var store = new KnowledgeFileStore(FilesPath, 10);
        var rejected = await Assert.ThrowsAsync<KnowledgeIngestionException>(() => store.SaveAsync(new MemoryStream([1]), "payload.exe"));
        Assert.Equal("FILE_TYPE_NOT_ALLOWED", rejected.Code);
        var tooLarge = await Assert.ThrowsAsync<KnowledgeIngestionException>(() => store.SaveAsync(new MemoryStream(new byte[11]), "large.txt"));
        Assert.Equal("FILE_TOO_LARGE", tooLarge.Code);
        var saved = await store.SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes("abc")), "..\\notes.md");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", saved.Hash);
        Assert.StartsWith(Path.GetFullPath(FilesPath), saved.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", saved.RelativePath);
    }

    [Fact]
    public void Extraction_is_strict_and_json_is_canonicalized()
    {
        Directory.CreateDirectory(directory);
        var json = Path.Combine(directory, "input.json"); File.WriteAllText(json, "{\"b\":2,\"a\":1}", new UTF8Encoding(false));
        var first = DeterministicTextExtractor.Extract(json); var second = DeterministicTextExtractor.Extract(json);
        Assert.Equal(first, second); Assert.Contains(Environment.NewLine == "\r\n" ? "\n" : "\n", first);
        var invalid = Path.Combine(directory, "invalid.txt"); File.WriteAllBytes(invalid, [0xff, 0xfe]);
        Assert.Equal("INVALID_UTF8", Assert.Throws<KnowledgeIngestionException>(() => DeterministicTextExtractor.Extract(invalid)).Code);
    }

    [Fact]
    public async Task Ingestion_deduplicates_searches_with_provenance_and_honours_delete()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb();
        var service = new KnowledgeService(db, new KnowledgeFileStore(FilesPath)); var now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");
        const string content = "Breakout confirmation requires volume expansion and a protected stop.";
        var first = await service.IngestAsync(StreamOf(content), "lesson.md", "Breakout lesson", "Course A", ["Breakout", " risk "], now);
        var duplicate = await service.IngestAsync(StreamOf(content), "copy.md", "Copy", "Course B", null, now);
        Assert.False(first.Duplicate); Assert.True(duplicate.Duplicate); Assert.Equal(first.DocumentId, duplicate.DocumentId);
        Assert.Equal(1, await db.KnowledgeDocuments.CountAsync()); Assert.Equal(2, await db.KnowledgeTags.CountAsync()); Assert.Equal(2, await db.KnowledgeIngestionJobs.CountAsync());

        var hit = Assert.Single(await service.SearchAsync("volume expansion"));
        Assert.Equal(first.DocumentId, hit.DocumentId); Assert.Contains("Course A / Breakout lesson", hit.Citation); Assert.Contains("[volume]", hit.Snippet, StringComparison.OrdinalIgnoreCase);
        await service.DeleteAsync(first.DocumentId, false, now.AddMinutes(1)); Assert.Empty(await service.SearchAsync("volume"));
        var restored = await service.IngestAsync(StreamOf(content), "lesson.md", "Ignored duplicate title", "Course C", null, now.AddMinutes(2));
        Assert.True(restored.Duplicate); Assert.Single(await service.SearchAsync("volume"));
        var relative = (await db.KnowledgeDocuments.SingleAsync()).StoredRelativePath;
        await service.DeleteAsync(first.DocumentId, true, now.AddMinutes(3));
        Assert.False(File.Exists(new KnowledgeFileStore(FilesPath).Resolve(relative))); Assert.Empty(await service.SearchAsync("volume"));
    }

    [Fact]
    public async Task Failed_ingestion_is_audited_without_retaining_private_file()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb();
        var service = new KnowledgeService(db, new KnowledgeFileStore(FilesPath));
        await Assert.ThrowsAsync<KnowledgeIngestionException>(() => service.IngestAsync(StreamOf("not json"), "bad.json", "Bad", "Local", null, DateTimeOffset.UtcNow));
        var job = await db.KnowledgeIngestionJobs.SingleAsync(); Assert.Equal("FAILED", job.Status); Assert.Equal("INVALID_JSON", job.ErrorCode);
        Assert.Empty(Directory.EnumerateFiles(FilesPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Retention_purge_removes_expired_documents_and_files()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb();
        var store = new KnowledgeFileStore(FilesPath); var service = new KnowledgeService(db, store); var now = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var result = await service.IngestAsync(StreamOf("retained temporarily"), "retention.txt", "Retention", "Local", null, now, retentionDays: 7);
        var relative = (await db.KnowledgeDocuments.SingleAsync(x => x.Id == result.DocumentId)).StoredRelativePath;
        Assert.Equal(0, await service.PurgeExpiredAsync(now.AddDays(6))); Assert.Equal(1, await service.PurgeExpiredAsync(now.AddDays(7)));
        Assert.False(File.Exists(store.Resolve(relative)));
    }

    private static MemoryStream StreamOf(string value) => new(Encoding.UTF8.GetBytes(value));
    private ScrapperTradeDbContext CreateDb() => new(PersistenceBootstrap.CreateOptions(DatabasePath));
}
