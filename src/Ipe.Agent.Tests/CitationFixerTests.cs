using System.Runtime.CompilerServices;
using Ipe.Agent.Knowledge;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.Tests;

// The 8B "translates" the cited name (política-de-reposição.md): the fixer swaps it for the real one it saw in the search.
public class CitationFixerTests
{
    private static readonly List<ChatMessage> Conversation =
    [
        new(ChatRole.User, "Por que faltou leite?"),
        new(ChatRole.Tool, [new FunctionResultContent("c1", new List<Passage>
        {
            new("politica-reposicao.md", "Política de reposição", null, "Estoque mínimo de 3 dias."),
            new("manual-refrigeracao.md", "Manual de refrigeração", null, "Alarme acima de -15 °C."),
        })]),
    ];

    [Theory]
    [InlineData("Ver (_política-de-reposição.md_).", "Ver (_politica-reposicao.md_).")]
    [InlineData("Ver _Manual_Refrigeração.md_ e _manual-refrigeracao.md_.", "Ver _Manual_Refrigeração.md_ e _manual-refrigeracao.md_.")]
    [InlineData("Ver _manual-refrigeraçao.md_.", "Ver _manual-refrigeracao.md_.")]
    [InlineData("Ver _calendario-promocoes.md_.", "Ver _calendario-promocoes.md_.")]   // far from both: stays (the eval catches it)
    public async Task Fixes_near_misses_and_keeps_the_rest(string answer, string expected)
    {
        var client = new CitationFixer(new FakeClient(answer));

        var response = await client.GetResponseAsync(Conversation, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.Text);
    }

    [Fact]
    public async Task Streaming_fixes_a_name_split_across_updates()
    {
        var client = new CitationFixer(new FakeClient("Segundo a (_polí", "tica-de-repo", "sição.md_), o mínimo ", "é 3 dias."));

        var text = string.Concat(await client.GetStreamingResponseAsync(Conversation, cancellationToken: TestContext.Current.CancellationToken)
            .Select(u => u.Text).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Segundo a (_politica-reposicao.md_), o mínimo é 3 dias.", text);
    }

    [Fact]
    public async Task Fixes_the_citation_in_tool_arguments()
    {
        var call = new FunctionCallContent("c2", "PublishAlerts", new Dictionary<string, object?>
        {
            ["alerts"] = new[] { new { title = "Leite", source = "política-de-reposição.md" } },
        });
        var client = new CitationFixer(new FakeClient(call));

        var response = await client.GetResponseAsync(Conversation, cancellationToken: TestContext.Current.CancellationToken);

        var fixedCall = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Single();
        Assert.Contains("politica-reposicao.md", fixedCall.Arguments!["alerts"]!.ToString());
    }

    [Fact]
    public async Task Without_a_search_nothing_changes()
    {
        var client = new CitationFixer(new FakeClient("Ver _política-de-reposição.md_."));

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "oi")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Ver _política-de-reposição.md_.", response.Text);
    }

    private sealed class FakeClient : IChatClient
    {
        private readonly AIContent[] _contents;
        public FakeClient(params string[] chunks) => _contents = [.. chunks.Select(c => new TextContent(c))];
        public FakeClient(AIContent content) => _contents = [content];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. _contents])));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var c in _contents)
            {
                await Task.Yield();
                yield return new ChatResponseUpdate(ChatRole.Assistant, [c]) { MessageId = "m1" };
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
