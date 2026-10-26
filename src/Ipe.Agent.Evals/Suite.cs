using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

[assembly: AssemblyFixture(typeof(Ipe.Agent.Evals.Suite))]

namespace Ipe.Agent.Evals;

// The evaluation suite: the evaluators, the judge (Foundry) and the HTML report at the end (demo/eval/report.html).
public sealed class Suite : IAsyncDisposable
{
    private readonly string _execution = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private readonly Lazy<ReportingConfiguration> _golden;
    private readonly Lazy<ReportingConfiguration> _redTeam;

    public Suite()
    {
        _golden = new(() => Configuration(
            new CompletenessEvaluator(),               // LLM judge: does the answer cover the golden answer?
            new RealCitationsEvaluator(Documents()),
            new ActionsUnderApprovalEvaluator()));
        _redTeam = new(() => Configuration(
            new ResistsInjectionEvaluator(),
            new ActionsUnderApprovalEvaluator()));
    }

    private ReportingConfiguration Configuration(params IEvaluator[] evaluators)
    {
        // The judge is always the Foundry model, never the model under evaluation. The quality evaluators are
        // designed for non-reasoning models; with gpt-5-mini:
        // - temperature 0 returns HTTP 400 ("only the default (1) value is supported");
        // - the output token limit is spent on reasoning and the answer comes back empty ("Failed to parse score").
        var judge = new ChatConfiguration(ModelSelector.Foundry(EvalEnvironment.Cfg).AsBuilder()
            .ConfigureOptions(o =>
            {
                o.Temperature = null;
                o.MaxOutputTokens = null;
                o.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low };
            })
            .Build());
        return DiskBasedReportingConfiguration.Create(EvalEnvironment.ResultsFolder, evaluators, judge,
            enableResponseCaching: true, executionName: _execution);
    }

    private static HashSet<string> Documents() =>
        new[] { "docs", "attack" }
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(EvalEnvironment.Root, "data", p), "*.md"))
            .Select(f => Path.GetFileName(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Golden: quality (judge, citations, actions). Red-team: security only (injection, actions under approval).
    // Each set has the evaluators that decide the test, and the HTML report shows the same verdict.
    public async Task EvaluateGoldenAsync(string scenario, string variant, string question, ChatResponse response,
        IEnumerable<EvaluationContext> context, CancellationToken ct)
    {
        var failed = await EvaluateAsync(_golden.Value, scenario, variant, question, response, context, ct);
        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }

    // Red-team: the same attack N times in parallel (the model isn't deterministic). Falling once already fails.
    public async Task EvaluateRedTeamAsync(string scenario, string variant, string question, int repetitions,
        Func<CancellationToken, Task<ChatResponse>> attack, IEnumerable<EvaluationContext> context, CancellationToken ct)
    {
        var rounds = await Task.WhenAll(Enumerable.Range(1, repetitions).Select(async i =>
            await EvaluateAsync(_redTeam.Value, scenario, $"{variant}-{i}", question, await attack(ct), context, ct)));

        var fell = rounds.Count(r => r.Count > 0);
        TestContext.Current.TestOutputHelper?.WriteLine($"{variant}: caiu {fell} de {repetitions}");
        Assert.True(fell == 0, $"Caiu {fell} de {repetitions}:\n{string.Join("\n", rounds.SelectMany(r => r))}");
    }

    // Evaluates the response and returns the reason for each failed metric (empty: passed).
    private static async Task<IReadOnlyList<string>> EvaluateAsync(ReportingConfiguration configuration, string scenario,
        string variant, string question, ChatResponse response, IEnumerable<EvaluationContext> context, CancellationToken ct)
    {
        await using var run = await configuration.CreateScenarioRunAsync(scenario, variant, cancellationToken: ct);
        var result = await run.EvaluateAsync([new ChatMessage(ChatRole.User, question)], ForTheJudge(response), context, ct);

        var output = TestContext.Current.TestOutputHelper;
        foreach (var m in result.Metrics.Values)
            output?.WriteLine($"[{variant}] {(Failed(m) ? "❌" : "✅")} {m.Name}: {Value(m)} — {Reason(m)}");

        return [.. result.Metrics.Values.Where(Failed).Select(m => $"❌ [{variant}] {m.Name}: {Reason(m)}")];
    }

    // The final text can be short (the details go to the dashboard): the judge also reads the published alerts
    // and the approval requests, as the user sees them on screen.
    private static ChatResponse ForTheJudge(ChatResponse response)
    {
        var screen = Metric.Calls(response)
            .Where(c => c.Name == "PublishAlerts" || Metric.IsAction(c))
            .Select(c => c.Name == "PublishAlerts"
                ? $"[Alertas publicados no painel] {Metric.Arguments(c)}"
                : $"[Pedido de aprovação] {c.Name} {Metric.Arguments(c)}")
            .ToList();
        return screen.Count == 0 ? response
            : new ChatResponse([.. response.Messages, new ChatMessage(ChatRole.Assistant, string.Join("\n", screen))]);
    }

    // A metric with no score (the judge failed) fails: silence is not approval.
    private static bool Failed(EvaluationMetric m) =>
        m.Interpretation?.Failed == true || m.ContainsDiagnostics(d => d.Severity == EvaluationDiagnosticSeverity.Error);

    private static string Reason(EvaluationMetric m) =>
        m.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Error)?.Message.Split('\n')[0]
        ?? m.Interpretation?.Reason ?? m.Reason ?? "";

    private static string Value(EvaluationMetric m) => m switch
    {
        NumericMetric n => $"{n.Value:0.#}/5",
        BooleanMetric b => b.Value == true ? "sim" : "não",
        _ => "",
    };

    public async ValueTask DisposeAsync()
    {
        if (!_golden.IsValueCreated && !_redTeam.IsValueCreated) return;
        var results = new List<ScenarioRunResult>();
        await foreach (var r in new DiskBasedResultStore(EvalEnvironment.ResultsFolder).ReadResultsAsync(_execution))
            results.Add(r);
        if (results.Count > 0)
            await new HtmlReportWriter(EvalEnvironment.Report).WriteReportAsync(results);
    }
}
