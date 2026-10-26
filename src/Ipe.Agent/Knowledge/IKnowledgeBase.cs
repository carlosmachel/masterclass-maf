using System.Text.Json.Serialization;

namespace Ipe.Agent.Knowledge;

// Document passage with the citation (source file name) the agent must show.
public sealed record Passage(
    string Document, string Title, string? Section, string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Blocked = false);   // true: Prompt Shields quarantine (Security/DocumentShield)

// Implementations: FoundryIqKnowledgeBase and LocalKnowledgeBase (data/docs).
public interface IKnowledgeBase
{
    Task<IReadOnlyList<Passage>> SearchAsync(string question, CancellationToken ct = default);
}
