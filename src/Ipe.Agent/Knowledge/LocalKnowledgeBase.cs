using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ipe.Agent.Knowledge;

// Plan B without Foundry IQ: each "## " section of data/docs/*.md becomes a passage, scored by the terms
// of the question (no accents, stem = first 6 characters). Simple on purpose.
public sealed class LocalKnowledgeBase : IKnowledgeBase
{
    private const int MaxPassages = 3;

    // Portuguese stop words: the documents and the questions are in Portuguese.
    private static readonly HashSet<string> StopWords =
    [
        "a", "o", "as", "os", "de", "da", "do", "das", "dos", "e", "em", "no", "na", "nos", "nas", "um", "uma",
        "por", "para", "com", "que", "se", "ja", "era", "foi", "ser", "esse", "essa", "isso", "tanta", "tanto",
        "qual", "quais", "como", "porque", "loja", "lojas",
    ];

    private readonly List<(Passage Passage, HashSet<string> Terms)> _passages = [];

    public LocalKnowledgeBase(string docsFolder)
    {
        foreach (var file in Directory.EnumerateFiles(docsFolder, "*.md").Order())
        {
            var lines = File.ReadAllLines(file);
            var title = lines.FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim() ?? Path.GetFileNameWithoutExtension(file);
            var document = Path.GetFileName(file);

            string? section = null;
            var body = new StringBuilder();
            void Close()
            {
                var text = body.ToString().Trim();
                if (text.Length > 0 && !text.StartsWith("> Documento fictício"))
                {
                    var passage = new Passage(document, title, section, text);
                    _passages.Add((passage, Terms($"{title} {section} {text}")));
                }
                body.Clear();
            }

            foreach (var line in lines.Skip(1))
            {
                if (line.StartsWith("## "))
                {
                    Close();
                    section = line[3..].Trim();
                }
                else if (!line.StartsWith("> Documento fictício"))
                {
                    body.AppendLine(line);
                }
            }
            Close();
        }
    }

    public Task<IReadOnlyList<Passage>> SearchAsync(string question, CancellationToken ct = default)
    {
        var terms = Terms(question);
        IReadOnlyList<Passage> result = _passages
            .Select(p => (p.Passage, Score: p.Terms.Count(terms.Contains)))
            .Where(p => p.Score > 0)
            .OrderByDescending(p => p.Score)
            .Take(MaxPassages)
            .Select(p => p.Passage)
            .ToList();
        return Task.FromResult(result);
    }

    private static HashSet<string> Terms(string text) =>
        Regex.Split(RemoveAccents(text.ToLowerInvariant()), @"[^\p{L}\p{N}-]+")
            .Select(t => t.Trim('-'))
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .Select(t => t.Length > 6 ? t[..6] : t)
            .ToHashSet();

    private static string RemoveAccents(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
