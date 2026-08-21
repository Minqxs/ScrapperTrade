using System.Text.Json;
using System.Text.Json.Serialization;
using ScrapperTrade.Application;

namespace ScrapperTrade.Infrastructure;

public sealed class JsonShadowStrategyStateStore : IShadowStrategyStateStore
{
    private sealed record StateDocument(IReadOnlyList<ShadowStrategyDecision> Decisions);

    private readonly string path;
    private readonly object gate = new();
    private readonly JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly Dictionary<string, ShadowStrategyDecision> decisions;

    public JsonShadowStrategyStateStore(string path)
    {
        this.path = path;
        decisions = Load(path, options).ToDictionary(x => x.DecisionKey, StringComparer.Ordinal);
    }

    public ShadowStrategyDecision? Find(string decisionKey)
    {
        lock (gate) return decisions.GetValueOrDefault(decisionKey);
    }

    public ShadowStrategyDecision AddOrGet(ShadowStrategyDecision decision)
    {
        lock (gate)
        {
            if (decisions.TryGetValue(decision.DecisionKey, out var existing)) return existing;
            decisions.Add(decision.DecisionKey, decision);
            Persist();
            return decision;
        }
    }

    public IReadOnlyList<ShadowStrategyDecision> ReadAll()
    {
        lock (gate) return decisions.Values.OrderBy(x => x.EvaluatedAt).ThenBy(x => x.DecisionKey, StringComparer.Ordinal).ToArray();
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(new StateDocument(ReadAll()), options));
        File.Move(temporary, path, true);
    }

    private static IReadOnlyList<ShadowStrategyDecision> Load(string path, JsonSerializerOptions options)
    {
        if (!File.Exists(path)) return [];
        var document = JsonSerializer.Deserialize<StateDocument>(File.ReadAllText(path), options) ?? throw new InvalidDataException("Shadow strategy state is empty or invalid.");
        if (document.Decisions.Select(x => x.DecisionKey).Distinct(StringComparer.Ordinal).Count() != document.Decisions.Count)
            throw new InvalidDataException("Shadow strategy state contains duplicate decision keys.");
        return document.Decisions;
    }
}
