using System.ComponentModel;
using Ipe.Agent.Data;

namespace Ipe.Agent.Tools;

public sealed record ActionConfirmation(int ActionId, string Type, int StoreId, string? Reference, string Message);

// Tools that change the world. Always registered with ApprovalRequiredAIFunction (see AgentTools.cs):
// they only run after the Approve click.
public sealed class ActionTools(IOperationsRepository repository)
{
    private const string ApprovedBy = "aprovação humana no dashboard";
    private static readonly string[] Priorities = ["ALTA", "MEDIA", "BAIXA"];

    [Description("""
        Abre um chamado de manutenção para um freezer. Exige aprovação humana antes de executar.
        Use para freezer CRITICO (prioridade ALTA) ou sem leitura por mais de 6 horas.
        """)]
    public async Task<ActionConfirmation> OpenMaintenanceTicketAsync(
        [Description("ID do freezer, por exemplo FRZ-07-2.")] string freezerId,
        [Description("ALTA, MEDIA ou BAIXA.")] string priority,
        [Description("O que aconteceu e o que precisa ser feito, em uma ou duas frases.")] string description,
        CancellationToken ct = default)
    {
        priority = priority.Trim().ToUpperInvariant();
        if (!Priorities.Contains(priority))
            throw new ArgumentException($"Prioridade inválida: {priority}. Use ALTA, MEDIA ou BAIXA.");
        var storeId = await repository.GetFreezerStoreAsync(freezerId, ct)
            ?? throw new ArgumentException($"Freezer não encontrado: {freezerId}.");

        var action = await repository.RecordActionAsync(
            new NewAction(ActionType.Ticket, storeId, freezerId, priority, description, ApprovedBy), ct);
        return new ActionConfirmation(action.ActionId, action.Type, action.StoreId, action.Reference,
            $"Chamado {action.ActionId} aberto para {freezerId} com prioridade {priority}.");
    }

    [Description("""
        Cria um pedido emergencial de reposição ao centro de distribuição da região da loja.
        Exige aprovação humana antes de executar. Use quando há risco de ruptura.
        """)]
    public async Task<ActionConfirmation> CreateEmergencyOrderAsync(
        [Description("ID da loja.")] int storeId,
        [Description("Categoria a repor, por exemplo Bebidas ou Congelados.")] string category,
        [Description("Por que o pedido é necessário, citando o dado e o documento que o justificam.")] string justification,
        CancellationToken ct = default)
    {
        var action = await repository.RecordActionAsync(
            new NewAction(ActionType.EmergencyOrder, storeId, category, null, justification, ApprovedBy), ct);
        return new ActionConfirmation(action.ActionId, action.Type, action.StoreId, action.Reference,
            $"Pedido emergencial {action.ActionId} de {category} criado para a loja {storeId}.");
    }
}
