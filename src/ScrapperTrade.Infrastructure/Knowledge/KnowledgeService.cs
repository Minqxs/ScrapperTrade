using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ScrapperTrade.Infrastructure.Knowledge;

public sealed class KnowledgeService(Persistence.ScrapperTradeDbContext db, KnowledgeFileStore files)
{
    public async Task<KnowledgeIngestionResult> IngestAsync(Stream input, string fileName, string title, string sourceName, IReadOnlyCollection<string>? tags, DateTimeOffset now, int? retentionDays = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title); ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        var job = new KnowledgeIngestionJobRecord { Id = Guid.NewGuid(), OriginalFileName = Path.GetFileName(fileName), Status = "RUNNING", CreatedAt = now, UpdatedAt = now };
        db.KnowledgeIngestionJobs.Add(job); await db.SaveChangesAsync(cancellationToken);
        StoredKnowledgeFile? storedFile = null; var retained = false;
        try
        {
            var stored = storedFile = await files.SaveAsync(input, fileName, cancellationToken);
            var duplicate = await db.KnowledgeDocuments.SingleOrDefaultAsync(x => x.ContentHash == stored.Hash, cancellationToken);
            if (duplicate is not null)
            {
                duplicate.DeletedAt = null; retained = true;
                job.Status = "DUPLICATE"; job.DocumentId = duplicate.Id; job.Detail = "Content hash already exists."; job.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken); return new(duplicate.Id, true, stored.Hash, 0);
            }

            var text = DeterministicTextExtractor.Extract(stored.AbsolutePath);
            if (string.IsNullOrWhiteSpace(text)) throw new KnowledgeIngestionException("EMPTY_CONTENT", "The document contains no searchable text.");
            var source = new KnowledgeSourceRecord { Id = Guid.NewGuid(), Name = sourceName.Trim(), SourceType = "LOCAL_FILE", OriginalLocator = Path.GetFileName(fileName), RetentionDays = retentionDays, CreatedAt = now };
            var document = new KnowledgeDocumentRecord { Id = Guid.NewGuid(), Source = source, Title = title.Trim(), OriginalFileName = Path.GetFileName(fileName), MediaType = MediaType(fileName), ContentHash = stored.Hash, StoredRelativePath = stored.RelativePath, SizeBytes = stored.SizeBytes, IngestedAt = now };
            document.Chunks.AddRange(DeterministicTextExtractor.Chunk(text).Select(x => new KnowledgeChunkRecord { Ordinal = x.Ordinal, Text = x.Text, StartCharacter = x.StartCharacter, EndCharacter = x.EndCharacter }));
            foreach (var tagName in (tags ?? []).Select(NormalizeTag).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal))
            {
                var tag = await db.KnowledgeTags.SingleOrDefaultAsync(x => x.Name == tagName, cancellationToken) ?? new KnowledgeTagRecord { Name = tagName };
                document.DocumentTags.Add(new KnowledgeDocumentTagRecord { Tag = tag });
            }
            db.KnowledgeDocuments.Add(document); job.Status = "COMPLETED"; job.DocumentId = document.Id; job.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken); retained = true;
            return new(document.Id, false, stored.Hash, document.Chunks.Count);
        }
        catch (Exception exception)
        {
            if (!retained && storedFile is not null) files.Delete(storedFile.RelativePath);
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added && !ReferenceEquals(x.Entity, job)).ToArray()) entry.State = EntityState.Detached;
            job.Status = "FAILED"; job.ErrorCode = exception is KnowledgeIngestionException known ? known.Code : "INGESTION_FAILED"; job.Detail = exception.Message; job.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken); throw;
        }
    }

    public async Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var match = BuildFtsQuery(query); if (match.Length == 0) return [];
        var connection = (SqliteConnection)db.Database.GetDbConnection(); var close = connection.State != System.Data.ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT c.Id, c.DocumentId, d.Title, s.Name, c.Ordinal, c.StartCharacter, c.EndCharacter,
                       snippet(knowledge_chunks_fts, 0, '[', ']', ' … ', 20)
                FROM knowledge_chunks_fts
                JOIN knowledge_chunks c ON c.Id = knowledge_chunks_fts.rowid
                JOIN knowledge_documents d ON d.Id = c.DocumentId
                JOIN knowledge_sources s ON s.Id = d.SourceId
                WHERE knowledge_chunks_fts MATCH $query AND d.DeletedAt IS NULL
                ORDER BY bm25(knowledge_chunks_fts), c.Id
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$query", match); command.Parameters.AddWithValue("$limit", limit);
            var hits = new List<KnowledgeSearchHit>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) hits.Add(new(reader.GetInt64(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetString(7)));
            return hits;
        }
        finally { if (close) await connection.CloseAsync(); }
    }

    public async Task DeleteAsync(Guid documentId, bool purge, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var document = await db.KnowledgeDocuments.SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken) ?? throw new KeyNotFoundException("Knowledge document not found.");
        if (!purge) { document.DeletedAt ??= now; await db.SaveChangesAsync(cancellationToken); return; }
        var path = document.StoredRelativePath; files.Delete(path); db.KnowledgeDocuments.Remove(document); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var documents = await db.KnowledgeDocuments.Include(x => x.Source).ToListAsync(cancellationToken); var count = 0;
        foreach (var document in documents.Where(x => x.Source.RetentionDays is int days && x.IngestedAt.AddDays(days) <= now).ToArray()) { await DeleteAsync(document.Id, true, now, cancellationToken); count++; }
        return count;
    }

    private static string NormalizeTag(string value) => value.Trim().ToLowerInvariant();
    private static string MediaType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch { ".md" or ".markdown" => "text/markdown", ".csv" => "text/csv", ".json" => "application/json", _ => "text/plain" };
    private static string BuildFtsQuery(string value) => string.Join(" AND ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(x => new string(x.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()).Where(x => x.Length > 0).Select(x => $"\"{x}\""));
}

public sealed record KnowledgeIngestionResult(Guid DocumentId, bool Duplicate, string ContentHash, int ChunkCount);
public sealed record KnowledgeSearchHit(long ChunkId, Guid DocumentId, string DocumentTitle, string SourceName, int ChunkOrdinal, int StartCharacter, int EndCharacter, string Snippet)
{
    public string Citation => $"{SourceName} / {DocumentTitle} [chunk {ChunkOrdinal}, chars {StartCharacter}-{EndCharacter}]";
}
