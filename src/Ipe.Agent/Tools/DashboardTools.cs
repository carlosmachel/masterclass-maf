using System.ComponentModel;
using System.Globalization;
using System.Text;
using Ipe.Agent.Dashboard;
using Ipe.Agent.Data;

namespace Ipe.Agent.Tools;

public sealed record AlertInput(
    [property: Description("ID da loja (storeId das tools).")] int StoreId,
    [property: Description("Nome da loja, por exemplo Ipê Cambuí (Campinas).")] string Store,
    [property: Description("Título curto do problema, por exemplo 'Freezer FRZ-07-2 crítico'.")] string Title,
    [property: Description("critico ou atencao.")] string Severity,
    [property: Description("Uma ou duas frases: o dado, a causa e a ação recomendada.")] string Summary,
    [property: Description("Nomes dos arquivos da base de conhecimento que explicam o problema.")] string[] Citations);

// UI tool: the data already reaches the dashboard on its own (DashboardMapping); the alerts are the agent's synthesis.
public sealed class DashboardTools(IOperationsRepository repository)
{
    [Description("""
        Publica no dashboard os cards de alerta da análise: um por problema, com resumo e citações dos documentos.
        Chame UMA vez, ao final de uma análise geral, com TODOS os problemas atuais (substitui os anteriores).
        """)]
    public async Task<IReadOnlyList<Alert>> PublishAlertsAsync(
        [Description("Todos os alertas atuais, do mais grave para o menos grave.")] AlertInput[] alerts,
        CancellationToken ct = default)
    {
        // The region comes from the database, not from the model: the dashboard's region filter needs it reliable.
        var regions = new Dictionary<int, string?>();
        foreach (var id in alerts.Select(a => a.StoreId).Distinct())
            regions[id] = await repository.GetStoreRegionAsync(id, ct);

        return alerts.Select(a => new Alert(
            Id(a.Store, a.Title),
            a.StoreId,
            a.Store,
            regions[a.StoreId],
            a.Title,
            Severity(a.Severity),
            a.Summary,
            // A citation is a knowledge base document (*.md); the model sometimes cites a tool name.
            (a.Citations ?? []).Where(c => c.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).Distinct().ToList())).ToList();
    }

    // The model writes the severity in Portuguese (critico | atencao); the dashboard uses critical | warning.
    private static string Severity(string fromModel) =>
        fromModel.Trim().ToLowerInvariant() is "critico" or "crítico" or "critical" ? "critical" : "warning";

    private static string Id(string store, string title)
    {
        var text = $"{store}-{title}".ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        }
        return string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
