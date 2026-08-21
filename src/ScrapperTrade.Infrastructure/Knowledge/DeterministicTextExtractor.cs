using System.Text;
using System.Text.Json;

namespace ScrapperTrade.Infrastructure.Knowledge;

public static class DeterministicTextExtractor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static string Extract(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var bytes = File.ReadAllBytes(path);
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException exception) { throw new KnowledgeIngestionException("INVALID_UTF8", exception.Message); }
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (extension == ".json")
        {
            try { using var json = JsonDocument.Parse(text); text = JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true }); }
            catch (JsonException exception) { throw new KnowledgeIngestionException("INVALID_JSON", exception.Message); }
        }
        return text;
    }

    public static IReadOnlyList<ExtractedChunk> Chunk(string text, int maximumCharacters = 2000)
    {
        if (maximumCharacters < 100) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        var chunks = new List<ExtractedChunk>(); var start = 0; var ordinal = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maximumCharacters, text.Length - start);
            if (start + length < text.Length) { var newline = text.LastIndexOf('\n', start + length - 1, length); if (newline > start) length = newline - start + 1; }
            var value = text.Substring(start, length).Trim();
            if (value.Length > 0) chunks.Add(new(ordinal++, value, start, start + length));
            start += length;
        }
        return chunks;
    }
}

public sealed record ExtractedChunk(int Ordinal, string Text, int StartCharacter, int EndCharacter);
