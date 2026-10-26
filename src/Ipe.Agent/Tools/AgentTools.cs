using Microsoft.Extensions.AI;

namespace Ipe.Agent.Tools;

public static class AgentTools
{
    private static readonly HashSet<string> DashboardOnly = ["PublishAlerts", "OpenMaintenanceTicket", "CreateEmergencyOrder"];

    // Over MCP there is no dashboard and no approval: only the queries.
    public static IList<AITool> ReadOnly(IServiceProvider sp) =>
        [.. All(sp).Where(t => !DashboardOnly.Contains(t.Name))];

    public static IList<AITool> All(IServiceProvider sp)
    {
        var operations = ActivatorUtilities.CreateInstance<OperationsTools>(sp);
        var actions = ActivatorUtilities.CreateInstance<ActionTools>(sp);
        var knowledge = ActivatorUtilities.CreateInstance<KnowledgeTools>(sp);
        var dashboard = ActivatorUtilities.CreateInstance<DashboardTools>(sp);

        return
        [
            AIFunctionFactory.Create(operations.GetFreezerStatusAsync, "GetFreezerStatus"),
            AIFunctionFactory.Create(operations.GetTemperatureHistoryAsync, "GetTemperatureHistory"),
            AIFunctionFactory.Create(operations.GetWeeklySalesAsync, "GetWeeklySales"),
            AIFunctionFactory.Create(operations.GetCriticalStockAsync, "GetCriticalStock"),
            AIFunctionFactory.Create(operations.ListMaintenanceTicketsAsync, "ListMaintenanceTickets"),
            AIFunctionFactory.Create(knowledge.SearchKnowledgeBaseAsync, "SearchKnowledgeBase"),
            AIFunctionFactory.Create(dashboard.PublishAlertsAsync, "PublishAlerts"),
            // Actions: they only run after "Approve" (the Harness sends a ToolApprovalRequestContent to the front end).
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(actions.OpenMaintenanceTicketAsync, "OpenMaintenanceTicket")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(actions.CreateEmergencyOrderAsync, "CreateEmergencyOrder")),
        ];
    }
}
