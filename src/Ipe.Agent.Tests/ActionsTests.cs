using Ipe.Agent.Data;

namespace Ipe.Agent.Tests;

public class ActionsTests
{
    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Record_action_returns_the_stored_record(Mode mode)
    {
        // Really writes: in Azure mode it only runs with IPE_SQL_ALLOW_WRITES=1 (a test database, never the stage one).
        if (mode == Mode.Azure && Environment.GetEnvironmentVariable("IPE_SQL_ALLOW_WRITES") != "1")
            Assert.Skip("Azure mode with writes: set IPE_SQL_ALLOW_WRITES=1 on a test database.");
        var repo = mode == Mode.Local ? Sources.FreshLocal() : Sources.Repository(mode);
        var ct = TestContext.Current.CancellationToken;

        var action = await repo.RecordActionAsync(
            new NewAction(ActionType.EmergencyOrder, 4, "Bebidas", null, "Teste automatizado", "teste"), ct);

        Assert.True(action.ActionId > 0);
        Assert.Equal((ActionType.EmergencyOrder, 4, "Bebidas", (string?)null), (action.Type, action.StoreId, action.Reference, action.Priority));
        Assert.Contains(await repo.ListActionsAsync(ct), a => a.ActionId == action.ActionId);
    }
}
