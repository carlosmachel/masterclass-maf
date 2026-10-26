using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

namespace Ipe.Agent.OnCall;

public static class OnCallRegistration
{
    // The workflow becomes an AIAgent (AsAIAgent): the same MapAGUIServer and the same front end serve the on-call flow.
    public static IHostedAgentBuilder AddOnCall(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<DistributionCenterClient>();   // A2A client for the DC agent
        var onCall = builder.AddAIAgent(OnCallWorkflow.Name, (sp, name) => new ErrorLoggingAgent(
            OnCallWorkflow.Create(AnalystAgent.ChatClient(builder.Configuration), sp).AsAIAgent(name: name, includeExceptionDetails: true, includeWorkflowOutputsInResponse: true),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Ipe.OnCall")));
        onCall.WithInMemorySessionStore(withIsolation: false);   // the approval comes back to the same session (threadId)
        return onCall;
    }
}
