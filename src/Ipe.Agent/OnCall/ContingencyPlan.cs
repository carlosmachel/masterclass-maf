using System.ComponentModel;
using Ipe.Agent.Tools;

namespace Ipe.Agent.OnCall;

public sealed record ContingencyConfirmation(ActionConfirmation Ticket, ActionConfirmation Order, string Message);

// The on-call agent asks for ONE approval for the whole plan (ticket + order with the DC proposal). Two approvals
// in a row inside the workflow (1.24 preview) don't work: in parallel the resume doesn't continue; in sequence the
// agent ends its turn after the first one. Action-by-action decisions stay with the analyst (step 4).
public sealed class ContingencyPlan(ActionTools actions)
{
    [Description("""
        Executa o plano de contingência de um freezer crítico: abre o chamado de manutenção ALTA e cria o pedido
        emergencial de Congelados com a proposta do CD. Exige UMA aprovação humana para o plano inteiro.
        """)]
    public async Task<ContingencyConfirmation> ExecuteAsync(
        [Description("ID do freezer, por exemplo FRZ-07-2.")] string freezerId,
        [Description("ID da loja do freezer.")] int storeId,
        [Description("Diagnóstico e o que a manutenção precisa fazer, em uma ou duas frases.")] string ticketDescription,
        [Description("Número da proposta do CD e as entregas, por exemplo: PRP-SE-4102, 120 cx hoje 18:00 + 30 cx amanhã 06:00.")] string dcProposal,
        CancellationToken ct = default)
    {
        var ticket = await actions.OpenMaintenanceTicketAsync(freezerId, "ALTA", ticketDescription, ct);
        var order = await actions.CreateEmergencyOrderAsync(storeId, "Congelados",
            $"Reposição da carga do {freezerId} (plantão). Proposta do CD Sudeste: {dcProposal}.", ct);
        return new(ticket, order, $"{ticket.Message} {order.Message}");
    }
}
