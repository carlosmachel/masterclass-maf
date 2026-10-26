using Ipe.Agent.OnCall;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipe.Agent.Tests;

// On-call workflow with a fake model: triage (code) decides whether the agent is called.
public class OnCallTests
{
    [Fact]
    public async Task Alarm_with_critical_freezer_calls_the_agent_and_closing_wraps_up()
    {
        var model = new FakeModel();
        var outputs = await Run(model, new ChatMessage(ChatRole.User, "Sensor FRZ-07-2 acima de -15 °C."));

        var request = Assert.Single(model.Requests);
        Assert.Contains("FRZ-07-2", request);                       // triage confirmed it with the data
        Assert.Contains("Plantão encerrado: 0 chamado(s) e 0 pedido(s) emergencial(is).", outputs);
    }

    [Fact]
    public async Task Turn_without_alarm_text_does_not_call_the_agent()
    {
        // This is what reaches triage on an approval resume: the response goes through the workflow's port.
        var model = new FakeModel();
        var outputs = await Run(model, new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "ok")]));

        Assert.Empty(model.Requests);
        Assert.Empty(outputs);
    }

    private static async Task<List<string>> Run(IChatClient model, ChatMessage message)
    {
        var sp = new ServiceCollection()
            .AddSingleton(Sources.FreshLocal())
            // DC A2A client: it only resolves the Agent Card on the first real call, which the fake model never makes.
            .AddSingleton(new DistributionCenterClient(new ConfigurationBuilder().Build()))
            .BuildServiceProvider();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await InProcessExecution.RunStreamingAsync(OnCallWorkflow.Create(model, sp), new List<ChatMessage> { message }, cancellationToken: ct);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: false));
        var outputs = new List<string>();
        await foreach (var e in run.WatchStreamAsync(ct))
            if (e is WorkflowOutputEvent o && o.Data is string s) outputs.Add(s);
        return outputs;
    }

    private sealed class FakeModel : IChatClient
    {
        public List<string> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Diagnóstico: compressor.")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var u in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates()) yield return u;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
