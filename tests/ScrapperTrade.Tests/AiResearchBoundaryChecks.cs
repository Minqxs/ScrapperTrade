using System.Text.Json;
using System.Net;
using System.Text;
using ScrapperTrade.Application;
using ScrapperTrade.Domain;
using ScrapperTrade.Infrastructure;
using ScrapperTrade.Quant;

internal static class AiResearchBoundaryChecks
{
    public static IEnumerable<(string, Action)> All()
    {
        yield return ("manual research requires provenance", RequiresProvenance);
        yield return ("manual research accepts constrained candidate", AcceptsConstrainedCandidate);
        yield return ("manual prompt denies execution authority", PromptDeniesExecutionAuthority);
        yield return ("openai provider is optional and structured", OpenAiProviderIsOptionalAndStructured);
    }

    private static void RequiresProvenance()
    {
        var hypothesis = new StrategyHypothesis("test", ValidSpec(), [], []);
        var result = new ManualChatGptWorkflow().Import(JsonSerializer.Serialize(hypothesis));
        if (result.IsValid || !result.Errors.Any(error => error.Contains("provenance", StringComparison.OrdinalIgnoreCase)))
            throw new Exception("Candidate without provenance was accepted.");
    }

    private static void AcceptsConstrainedCandidate()
    {
        var citation = new ResearchCitation(Guid.NewGuid(), Guid.NewGuid(), "EMA crossover example", 0, 21);
        var hypothesis = new StrategyHypothesis("A bounded hypothesis", ValidSpec(), [citation], ["Demo evidence only"]);
        var result = new ManualChatGptWorkflow().Import(JsonSerializer.Serialize(hypothesis));
        if (!result.IsValid || result.Hypothesis?.Strategy.Id != "manual-candidate")
            throw new Exception(string.Join(" ", result.Errors));
    }

    private static void PromptDeniesExecutionAuthority()
    {
        var request = new AiResearchRequest(Guid.NewGuid(), "Propose a candidate", "source text", "{}", DateTimeOffset.UtcNow);
        var prompt = new ManualChatGptWorkflow().CreatePrompt(request);
        if (!prompt.Contains("no trading or configuration authority", StringComparison.Ordinal) ||
            !prompt.Contains("Do not generate code, tools, orders", StringComparison.Ordinal))
            throw new Exception("Research prompt omitted its authority boundary.");
    }

    private static StrategySpec ValidSpec() =>
        new("manual-candidate", 1, 10, 30, 1.5m, 2m, new HashSet<MarketRegime> { MarketRegime.TrendingUp });

    private static void OpenAiProviderIsOptionalAndStructured()
    {
        var unconfigured = new OpenAiResponsesProvider(new HttpClient(new FakeHandler()), () => null, "explicit-model");
        if (unconfigured.IsConfigured) throw new Exception("Provider claimed configuration without a secret.");

        var handler = new FakeHandler();
        var provider = new OpenAiResponsesProvider(new HttpClient(handler), () => "test-key-not-a-real-secret", "explicit-model");
        var request = new AiResearchRequest(Guid.NewGuid(), "candidate", "evidence", "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}", DateTimeOffset.UtcNow);
        var output = provider.GenerateAsync(request).GetAwaiter().GetResult();
        if (output != "{\"thesis\":\"bounded\"}" || handler.RequestBody is null || !handler.RequestBody.Contains("json_schema", StringComparison.Ordinal))
            throw new Exception("Provider did not request or return structured output.");
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"thesis\\\":\\\"bounded\\\"}\"}]}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
