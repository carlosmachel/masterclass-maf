using System.Text.Json;
using AGUI.Abstractions;
using AGUI.Server;
using Ipe.Agent.Dashboard;

namespace Ipe.Agent.Tests;

// The state comes from the client (untrusted): only a known region and integer ids get through.
public class FiltersTests
{
    private static Filters Read(string stateJson)
    {
        var input = new RunAgentInput
        {
            ThreadId = "t",
            RunId = "r",
            State = JsonDocument.Parse(stateJson).RootElement.Clone(),
            Messages = [],
        };
        // Same path as MapAGUIServer: RunAgentInput -> ChatOptions holding the input.
        var ctx = input.ToChatRequestContext(JsonSerializerOptions.Web, null);
        return DashboardFilters.Read(ctx.ChatOptions);
    }

    [Fact]
    public void Reads_region_and_excluded_stores()
    {
        var f = Read("""{ "filters": { "region": "sul", "excludedStoreIds": [4, 13, 4] }, "kpis": {} }""");

        Assert.Equal("Sul", f.Region);
        Assert.Equal([4, 13], f.ExcludedStoreIds);
    }

    [Theory]
    [InlineData("""{ "filters": { "region": "Marte", "excludedStoreIds": ["4; DROP TABLE x", -1, 2.5] } }""")]
    [InlineData("""{ "filters": "anything" }""")]
    [InlineData("""{ }""")]
    public void Ignores_what_is_not_valid(string state)
    {
        Assert.True(Read(state).IsEmpty);
    }
}
