using System.ComponentModel;
using Ipe.Agent.Dashboard;
using Ipe.Agent.Data;

namespace Ipe.Agent.Tools;

// Read tools. The description is what makes the model choose well: write it for the model, in plain Portuguese
// (the language the agent works in).
public sealed class OperationsTools(IOperationsRepository repository)
{
    [Description("""
        Status atual de todos os freezers de congelados da rede, calculado nas últimas 24 horas.
        Status: CRITICO (2h ou mais acima de -15 °C), ATENCAO (média acima de -18,5 °C, temperatura subindo
        em relação à semana retrasada ou lacuna de leitura maior que 6h nos últimos 7 dias) ou OK.
        Use primeiro em qualquer pergunta sobre a operação ou sobre refrigeração.
        """)]
    public async Task<IReadOnlyList<FreezerStatus>> GetFreezerStatusAsync(
        [Description("Região para filtrar (Sudeste, Sul, Nordeste, Centro-Oeste, Norte). Omita para todas.")] string? region = null,
        [Description("IDs de lojas que o usuário removeu do dashboard e devem ficar de fora.")] int[]? excludedStoreIds = null,
        CancellationToken ct = default)
    {
        var (r, excluded) = DashboardFilters.Combine(region, excludedStoreIds);
        var all = await repository.GetFreezerStatusAsync(r, excluded, ct);
        // OK freezers only show up in the count: the model needs the problems, not 45 rows.
        return all.Where(f => f.Status != "OK").ToList();
    }

    [Description("""
        Série de temperatura de um freezer para o gráfico de linha do dashboard, agregada em blocos de horas
        (média e máxima de cada bloco), junto com a faixa ideal do equipamento. Use para mostrar a evolução
        de um freezer em ATENCAO ou CRITICO.
        """)]
    public async Task<TemperatureHistory?> GetTemperatureHistoryAsync(
        [Description("ID do freezer, por exemplo FRZ-07-2.")] string freezerId,
        [Description("Quantos dias para trás, contando da leitura mais recente. Padrão 14.")] int days = 14,
        CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 60);
        var hoursPerPoint = days <= 7 ? 3 : 6;
        return await repository.GetTemperatureHistoryAsync(freezerId, days, hoursPerPoint, ct);
    }

    [Description("""
        Vendas dos últimos 7 dias comparadas aos 7 dias anteriores, por loja e categoria, com a variação em %.
        Devolve só as combinações com variação acima de 15% (para mais ou para menos), que são as que importam.
        """)]
    public async Task<IReadOnlyList<WeeklySales>> GetWeeklySalesAsync(
        [Description("Região para filtrar (Sudeste, Sul, Nordeste, Centro-Oeste, Norte). Omita para todas.")] string? region = null,
        [Description("IDs de lojas que o usuário removeu do dashboard e devem ficar de fora.")] int[]? excludedStoreIds = null,
        CancellationToken ct = default)
    {
        var (r, excluded) = DashboardFilters.Combine(region, excludedStoreIds);
        var all = await repository.GetWeeklySalesAsync(r, excluded, ct);
        return all.Where(s => s.ChangePct is { } pct && Math.Abs(pct) > 15).ToList();
    }

    [Description("Itens de estoque abaixo do ponto de reposição (risco de ruptura), por loja.")]
    public async Task<IReadOnlyList<CriticalStockItem>> GetCriticalStockAsync(
        [Description("ID da loja para filtrar. Omita para todas.")] int? storeId = null,
        [Description("IDs de lojas que o usuário removeu do dashboard e devem ficar de fora.")] int[]? excludedStoreIds = null,
        CancellationToken ct = default)
    {
        var (region, excluded) = DashboardFilters.Combine(null, excludedStoreIds);
        var items = await repository.GetCriticalStockAsync(storeId, excluded, ct);
        return region is null ? items : items.Where(i => i.Region == region).ToList();
    }

    [Description("Histórico de chamados de manutenção de um freezer ou de uma loja, do mais recente para o mais antigo.")]
    public Task<IReadOnlyList<MaintenanceTicket>> ListMaintenanceTicketsAsync(
        [Description("ID do freezer, por exemplo FRZ-07-2.")] string? freezerId = null,
        [Description("ID da loja.")] int? storeId = null,
        CancellationToken ct = default) =>
        repository.ListMaintenanceTicketsAsync(freezerId, storeId, ct);
}
