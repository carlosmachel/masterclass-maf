using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.Knowledge;

// Small models "translate" the cited file name (política-de-reposição.md for politica-reposicao.md). This client sits
// under function invocation: it learns the real names from the SearchKnowledgeBase results already in the conversation
// and swaps a near miss for the real name, in the text and in tool arguments (the dashboard alerts).
// A name with no close match stays as written: the golden eval (Citações reais) still catches it.
public sealed partial class CitationFixer(IChatClient inner) : DelegatingChatClient(inner)
{
    // No "_" (\w includes it): the agent cites in Markdown italics, _manual-refrigeracao.md_.
    [GeneratedRegex(@"[\p{L}\p{N}-]+\.md(?![\p{L}\p{N}])")]
    private static partial Regex FileName();

    [GeneratedRegex(@"""document""\s*:\s*""([^""]+\.md)""", RegexOptions.IgnoreCase)]
    private static partial Regex DocumentField();

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var known = KnownDocuments(messages);
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        if (known.Count == 0) return response;

        foreach (var message in response.Messages)
            message.Contents = [.. message.Contents.Select(c => c switch
            {
                TextContent t => new TextContent(Fix(t.Text, known)) { AdditionalProperties = t.AdditionalProperties },
                FunctionCallContent f => Fix(f, known),
                _ => c,
            })];
        return response;
    }

    // Streaming: a name can arrive split across updates, so the text after the last whitespace waits for the next one.
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var known = KnownDocuments(messages);
        if (known.Count == 0)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
                yield return update;
            yield break;
        }

        var pending = new StringBuilder();
        ChatResponseUpdate? lastText = null;

        ChatResponseUpdate Flush()
        {
            var text = Fix(pending.ToString(), known);
            pending.Clear();
            return new ChatResponseUpdate(lastText!.Role, text)
            {
                MessageId = lastText.MessageId,
                ResponseId = lastText.ResponseId,
                AuthorName = lastText.AuthorName,
                ConversationId = lastText.ConversationId,
                ModelId = lastText.ModelId,
            };
        }

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (!update.Contents.Any(c => c is TextContent))
            {
                if (pending.Length > 0) yield return Flush();
                update.Contents = [.. update.Contents.Select(c => c is FunctionCallContent f ? Fix(f, known) : c)];
                yield return update;
                continue;
            }

            var contents = new List<AIContent>();
            foreach (var content in update.Contents)
            {
                if (content is not TextContent t)
                {
                    contents.Add(content is FunctionCallContent f ? Fix(f, known) : content);
                    continue;
                }
                pending.Append(t.Text);
                var buffered = pending.ToString();
                var cut = buffered.LastIndexOfAny([' ', '\n', '\t']) + 1;
                if (cut == 0) continue;
                contents.Add(new TextContent(Fix(buffered[..cut], known)));
                pending.Remove(0, cut);
            }
            lastText = update;
            if (contents.Count == 0 && update.FinishReason is null) continue;
            update.Contents = contents;
            yield return update;
        }
        if (pending.Length > 0) yield return Flush();
    }

    // Real names: the "document" field of every knowledge base result the model has seen.
    private static List<string> KnownDocuments(IEnumerable<ChatMessage> messages) =>
        [.. messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .SelectMany(r => DocumentField().Matches(r.Result as string ?? JsonSerializer.Serialize(r.Result, AIJsonUtilities.DefaultOptions)))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    public static string Fix(string text, IReadOnlyList<string> known) =>
        FileName().Replace(text, m => Closest(m.Value, known) ?? m.Value);

    private static FunctionCallContent Fix(FunctionCallContent call, IReadOnlyList<string> known)
    {
        if (call.Arguments is null) return call;
        var json = JsonSerializer.Serialize(call.Arguments, AIJsonUtilities.DefaultOptions);
        var fixedJson = Fix(json, known);
        if (fixedJson == json) return call;
        call.Arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(fixedJson, AIJsonUtilities.DefaultOptions);
        return call;
    }

    // The real name when the cited one is it, or is a few edits away once accents, case and separators go away.
    private static string? Closest(string cited, IReadOnlyList<string> known)
    {
        if (known.Contains(cited, StringComparer.OrdinalIgnoreCase)) return null;
        var c = Normalize(cited);
        return known
            .Select(k => (Name: k, Distance: Distance(c, Normalize(k))))
            .Where(k => k.Distance <= Math.Max(2, Normalize(k.Name).Length / 5))
            .OrderBy(k => k.Distance)
            .Select(k => k.Name)
            .FirstOrDefault();
    }

    private static string Normalize(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name[..^3].ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(ch) && CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString();
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }
}
