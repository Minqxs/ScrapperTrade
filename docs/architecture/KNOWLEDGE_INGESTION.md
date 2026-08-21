# Offline Knowledge Ingestion

The first knowledge slice is deliberately local and deterministic. It does not call OpenAI, ChatGPT, transcription services, or any network provider, and it has no route to execution.

## Storage

- Metadata, provenance, chunks, tags, and ingestion-job outcomes are stored in SQLite migrations.
- Uploaded files are content-addressed by SHA-256 below `%LOCALAPPDATA%/ScrapperTrade/knowledge/files` and never stored in Git.
- The accepted extensions are `.txt`, `.md`, `.markdown`, `.csv`, and `.json`; inputs default to a 25 MiB limit and require valid UTF-8.
- Original filenames are metadata only. They never participate in the stored path.

## Extraction and search

Plain text, Markdown, and CSV retain their text with normalized line endings. JSON must parse and is serialized deterministically before chunking. Chunks retain ordinal and character offsets to form inspectable citations back to source and document records.

SQLite FTS5 indexes chunk text using migration-managed insert, update, and delete triggers. Search accepts sanitized alphanumeric terms, filters soft-deleted documents, and returns source/document/chunk/character provenance. Search results are evidence retrieval only; they cannot become strategies or trading commands without later constrained research and validation boundaries.

## Lifecycle

- Identical bytes deduplicate by a unique SHA-256 content hash. Re-importing a soft-deleted document restores it without duplicating content.
- A soft delete immediately excludes a document from search while retaining local evidence.
- A purge removes the private file, cascades its chunks and tags links, and updates FTS through database triggers.
- Optional source retention days permit deterministic expiry cleanup.
- Failed jobs retain status and error codes, while invalid staged files are deleted.

Media transcription, PDFs, office formats, keyframes, and provider-assisted extraction remain future bounded adapters; unsupported formats fail closed today.
