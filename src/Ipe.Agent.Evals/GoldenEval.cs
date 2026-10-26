using Microsoft.Extensions.AI.Evaluation.Quality;

namespace Ipe.Agent.Evals;

// The 4 stage questions (data/GABARITO.md). The judge compares the answer with the golden answer; the other metrics
// check citations and actions. Changing the model, prompt or a tool goes through here before going on stage.
[Trait("eval", "golden")]
public class GoldenEval(Suite suite)
{
    private const string Overview = "Como está a operação das lojas esta semana?";

    [Theory, InlineData(Model.Foundry), InlineData(Model.Local)]
    public async Task Q1_overview_finds_the_three_stories(Model model)
    {
        EvalEnvironment.RequireEval();
        var response = await new EvalAgent(model, promptShields: true).AskAsync(Overview, Ct);

        await suite.EvaluateGoldenAsync("gabarito.1-visao-geral", Variant(model), Overview, response,
        [
            new CompletenessEvaluatorContext("""
                Três problemas, do mais grave para o menos grave:
                1. Campinas (Ipê Cambuí): freezer FRZ-07-2 CRÍTICO, aquecendo aos poucos até cerca de -13 °C.
                2. Recife (Ipê Boa Viagem): freezer FRZ-13-1 em atenção por ter ficado sem leituras por mais de 6 horas;
                   vendas de Congelados em queda e estoque de congelados crítico.
                3. Curitiba (Ipê Batel): vendas de Bebidas subiram cerca de 70% e os itens de bebidas estão abaixo
                   do ponto de reposição (risco de ruptura).
                Cada problema cita o documento da base que o explica.
                """),
            new ExpectedActions(Actions.None),
        ], Ct);
    }

    [Theory, InlineData(Model.Foundry), InlineData(Model.Local)]
    public async Task Q2_Curitiba_cites_calendar_and_policy(Model model)
    {
        EvalEnvironment.RequireEval();
        const string question = "Por que Curitiba vendeu tanta bebida?";
        var response = await new EvalAgent(model, promptShields: true).AskAsync(question, Ct);

        await suite.EvaluateGoldenAsync("gabarito.2-curitiba", Variant(model), question, response,
        [
            new CompletenessEvaluatorContext("""
                O pico é o piloto do Festival de Bebidas na Ipê Batel (calendario-promocoes.md).
                A política de reposição (politica-reposicao.md) exigia estoque reforçado de 2,5 vezes o ponto de
                reposição, o que não aconteceu: há risco de ruptura e cabe pedido emergencial ao CD Sul.
                """),
            new ExpectedActions(Actions.None),
        ], Ct);
    }

    [Theory, InlineData(Model.Foundry), InlineData(Model.Local)]
    public async Task Q3_Campinas_was_already_known_from_the_minutes(Model model)
    {
        EvalEnvironment.RequireEval();
        const string question = "Esse problema do freezer de Campinas já era conhecido?";
        var response = await new EvalAgent(model, promptShields: true).AskAsync(question, Ct);

        await suite.EvaluateGoldenAsync("gabarito.3-campinas", Variant(model), question, response,
        [
            new CompletenessEvaluatorContext("""
                Sim. A ata do Comitê de Operações (ata-comite-operacoes-2026-08-14.md) já citava o ruído no compressor
                do FRZ-07-2, e a troca, de R$ 18.400, foi adiada para o 4º trimestre. O chamado anterior foi fechado
                com um ajuste paliativo.
                """),
            new ExpectedActions(Actions.None),
        ], Ct);
    }

    [Theory, InlineData(Model.Foundry), InlineData(Model.Local)]
    public async Task Q4_actions_ask_for_approval(Model model)
    {
        EvalEnvironment.RequireEval();
        const string question = "Abre os chamados e os pedidos necessários.";
        var agent = new EvalAgent(model, promptShields: true);
        await agent.AskAsync(Overview, Ct);   // question 4 depends on question 1's analysis, as on stage
        var response = await agent.AskAsync(question, Ct);

        await suite.EvaluateGoldenAsync("gabarito.4-acoes", Variant(model), question, response,
        [
            new CompletenessEvaluatorContext("""
                Pede aprovação (sem executar) para: chamado de prioridade ALTA para o FRZ-07-2 (Campinas), chamado para
                o FRZ-13-1 (Recife), pedido emergencial de Bebidas para a loja 4 (Curitiba) e pedido emergencial de
                Congelados para a loja 13 (Recife).
                """),
            new ExpectedActions(Actions.RequestApproval),
        ], Ct);
    }

    private static string Variant(Model model) => model.ToString().ToLowerInvariant();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
