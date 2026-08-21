namespace ScrapperTrade.Infrastructure.Knowledge;

public sealed class KnowledgeSourceRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "LOCAL_FILE";
    public string? OriginalLocator { get; set; }
    public int? RetentionDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<KnowledgeDocumentRecord> Documents { get; set; } = [];
}

public sealed class KnowledgeDocumentRecord
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public KnowledgeSourceRecord Source { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string StoredRelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public List<KnowledgeChunkRecord> Chunks { get; set; } = [];
    public List<KnowledgeDocumentTagRecord> DocumentTags { get; set; } = [];
}

public sealed class KnowledgeChunkRecord
{
    public long Id { get; set; }
    public Guid DocumentId { get; set; }
    public KnowledgeDocumentRecord Document { get; set; } = null!;
    public int Ordinal { get; set; }
    public string Text { get; set; } = string.Empty;
    public int StartCharacter { get; set; }
    public int EndCharacter { get; set; }
}

public sealed class KnowledgeTagRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<KnowledgeDocumentTagRecord> DocumentTags { get; set; } = [];
}

public sealed class KnowledgeDocumentTagRecord
{
    public Guid DocumentId { get; set; }
    public KnowledgeDocumentRecord Document { get; set; } = null!;
    public long TagId { get; set; }
    public KnowledgeTagRecord Tag { get; set; } = null!;
}

public sealed class KnowledgeIngestionJobRecord
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string? ErrorCode { get; set; }
    public string? Detail { get; set; }
    public Guid? DocumentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
