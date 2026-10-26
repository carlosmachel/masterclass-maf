using System.ComponentModel;

namespace Ipe.DistributionCenter;

public sealed record TruckWindow(string Departure, string Vehicle, int CapacityBoxes);
public sealed record Capacity(string Dc, string Category, int StockBoxes, IReadOnlyList<TruckWindow> Windows);
public sealed record Delivery(string Departure, string Vehicle, int Boxes);
public sealed record Proposal(
    string ProposalId, int StoreId, string Category, int BoxesRequested, int BoxesFulfilled,
    IReadOnlyList<Delivery> Deliveries, string ValidUntil, string Note);

// The DC Southeast "system": stock and truck windows in memory. The tools are deterministic so the DC agent
// never makes up a number: it only talks; the math happens here.
public sealed class Logistics
{
    private const string Dc = "CD Sudeste (Jundiaí)";
    private int _nextProposal = 4101;

    private static readonly Dictionary<string, Capacity> Stock = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Congelados"] = new(Dc, "Congelados", 160,
            [new("hoje 18:00", "caminhão refrigerado", 120), new("amanhã 06:00", "caminhão refrigerado", 200)]),
        ["Bebidas"] = new(Dc, "Bebidas", 900,
            [new("hoje 16:00", "caminhão seco", 400), new("amanhã 06:00", "caminhão seco", 600)]),
        ["Laticínios"] = new(Dc, "Laticínios", 240,
            [new("hoje 18:00", "caminhão refrigerado", 80), new("amanhã 06:00", "caminhão refrigerado", 160)]),
    };

    [Description("Estoque do CD e as próximas janelas de caminhão (horário, veículo e capacidade em caixas) para uma categoria.")]
    public Capacity CheckCapacity([Description("Categoria: Congelados, Bebidas ou Laticínios.")] string category) =>
        Stock.TryGetValue(category.Trim(), out var c) ? c
            : throw new ArgumentException($"O {Dc} não trabalha com a categoria {category}.");

    [Description("""
        Monta a proposta de entrega para uma loja: divide as caixas pelas janelas de caminhão, na ordem, até o
        limite do estoque. Devolve o número da proposta, as entregas e a validade. Não confirma pedido nenhum.
        """)]
    public Proposal CreateProposal(
        [Description("ID da loja que vai receber.")] int storeId,
        [Description("Categoria: Congelados, Bebidas ou Laticínios.")] string category,
        [Description("Quantidade de caixas pedida.")] int boxes)
    {
        var capacity = CheckCapacity(category);
        var remaining = Math.Min(boxes, capacity.StockBoxes);
        var deliveries = new List<Delivery>();
        foreach (var window in capacity.Windows)
        {
            if (remaining <= 0) break;
            var here = Math.Min(remaining, window.CapacityBoxes);
            deliveries.Add(new(window.Departure, window.Vehicle, here));
            remaining -= here;
        }
        var fulfilled = deliveries.Sum(d => d.Boxes);
        var id = $"PRP-SE-{Interlocked.Increment(ref _nextProposal)}";
        var note = fulfilled < boxes
            ? $"Estoque do CD cobre {fulfilled} de {boxes} caixas."
            : deliveries.Count > 1 ? "Entrega dividida: a primeira janela não comporta tudo." : "Entrega única.";
        return new(id, storeId, capacity.Category, boxes, fulfilled, deliveries,
            DateTime.Now.AddHours(2).ToString("HH:mm"), note);
    }
}
