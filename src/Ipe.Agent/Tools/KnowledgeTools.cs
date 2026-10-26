using System.ComponentModel;
using Ipe.Agent.Knowledge;

namespace Ipe.Agent.Tools;

public sealed class KnowledgeTools(IKnowledgeBase knowledgeBase)
{
    [Description("""
        Busca na base de conhecimento da rede (ata do Comitê de Operações, manual de refrigeração,
        política de reposição e calendário de promoções). Use para explicar o PORQUÊ de cada problema.
        Cada trecho traz o documento de origem: cite-o na resposta.
        """)]
    public Task<IReadOnlyList<Passage>> SearchKnowledgeBaseAsync(
        [Description("Pergunta em linguagem natural, com os nomes de loja, cidade, freezer ou campanha envolvidos.")] string question,
        CancellationToken ct = default) =>
        knowledgeBase.SearchAsync(question, ct);
}
