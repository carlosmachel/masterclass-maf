using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ipe.Agent;

// The AG-UI adapter only returns RUN_ERROR "StreamingError" and does not log the cause.
// This decorator logs the real exception (it shows up in the Aspire log and trace) and rethrows it.
public sealed class ErrorLoggingAgent(AIAgent inner, ILogger logger) : DelegatingAIAgent(inner)
{
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await InnerAgent.RunAsync(messages, session, options, cancellationToken);
        }
        catch (Exception ex) when (Log(ex))
        {
            throw;
        }
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var updates = InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            try
            {
                if (!await updates.MoveNextAsync()) yield break;
            }
            catch (Exception ex) when (Log(ex))
            {
                throw;
            }
            yield return updates.Current;
        }
    }

    // Exception filter: logs without unwinding the stack and returns false so the exception keeps going.
    private bool Log(Exception ex)
    {
        if (ex is not OperationCanceledException) logger.LogError(ex, "Agent {Agent} run failed", Name);
        return false;
    }
}
