using System.Text.Json;
using Ipe.Agent.Data;
using Ipe.Agent.Knowledge;
using Ipe.Agent.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Ipe.Agent.Tests;

public class ToolsTests
{
    private static IList<AITool> Tools(IOperationsRepository repo) =>
        AgentTools.All(new ServiceCollection()
            .AddSingleton(repo)
            .AddSingleton<IKnowledgeBase>(Sources.Knowledge.Value)
            .BuildServiceProvider());

    [Fact]
    public void Only_action_tools_require_approval()
    {
        var tools = Tools(Sources.FreshLocal());

        var withApproval = tools.OfType<ApprovalRequiredAIFunction>().Select(t => t.Name).Order();
        Assert.Equal(["CreateEmergencyOrder", "OpenMaintenanceTicket"], withApproval);
        Assert.Equal(
            ["GetCriticalStock", "GetFreezerStatus", "GetTemperatureHistory", "GetWeeklySales",
             "ListMaintenanceTickets", "PublishAlerts", "SearchKnowledgeBase"],
            tools.Where(t => t is not ApprovalRequiredAIFunction).Select(t => t.Name).Order());
    }

    [Fact]
    public async Task Freezer_status_returns_only_the_problems()
    {
        var tool = Tools(Sources.FreshLocal()).OfType<AIFunction>().Single(t => t.Name == "GetFreezerStatus");

        var result = Json(await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken));

        Assert.Equal(2, result.GetArrayLength());
    }

    [Fact]
    public async Task Opening_a_ticket_records_the_action_with_the_freezer_store()
    {
        var repo = Sources.FreshLocal();
        var tool = Tools(repo).OfType<AIFunction>().Single(t => t.Name == "OpenMaintenanceTicket");

        // Direct call, as the Harness does after "Approve".
        await tool.InvokeAsync(new AIFunctionArguments
        {
            ["freezerId"] = "FRZ-07-2",
            ["priority"] = "alta",
            ["description"] = "Temperatura acima de -15 °C; antecipar troca do compressor.",
        }, TestContext.Current.CancellationToken);

        var action = Assert.Single(await repo.ListActionsAsync(TestContext.Current.CancellationToken));
        Assert.Equal((ActionType.Ticket, 7, "FRZ-07-2", "ALTA"), (action.Type, action.StoreId, action.Reference, action.Priority));
    }

    [Theory]
    [InlineData("Por que Curitiba vendeu tanta bebida?", "calendario-promocoes.md")]
    [InlineData("Esse problema do freezer de Campinas já era conhecido?", "ata-comite-operacoes-2026-08-14.md")]
    [InlineData("Freezer sem leitura por mais de 6 horas, o que fazer?", "manual-refrigeracao.md")]
    [InlineData("Qual o estoque reforçado durante promoções?", "politica-reposicao.md")]
    public async Task Local_knowledge_base_cites_the_right_document(string question, string document)
    {
        var passages = await Sources.Knowledge.Value.SearchAsync(question, TestContext.Current.CancellationToken);

        Assert.Contains(passages, p => p.Document == document);
    }

    [Theory]
    [InlineData("Por que Curitiba vendeu tanta bebida?", "calendario-promocoes.md")]
    [InlineData("Esse problema do freezer de Campinas já era conhecido?", "ata-comite-operacoes-2026-08-14.md")]
    [InlineData("Freezer sem leitura por mais de 6 horas, o que fazer?", "manual-refrigeracao.md")]
    [InlineData("Qual o estoque reforçado durante promoções?", "politica-reposicao.md")]
    public async Task Foundry_IQ_cites_the_right_document(string question, string document)
    {
        var passages = await FoundryIq.KnowledgeBase().SearchAsync(question, TestContext.Current.CancellationToken);

        Assert.Contains(passages, p => p.Document == document);
    }

    private static JsonElement Json(object? value) => Assert.IsType<JsonElement>(value);
}
