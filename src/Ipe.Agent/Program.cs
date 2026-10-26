using Ipe.Agent;
using Ipe.Agent.Dashboard;
using Ipe.Agent.OnCall;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDemoSources();                // Demo:Mode = Azure (SQL + Foundry IQ) or Local (CSV + docs)
builder.AddSecurity();                   // Prompt Shields on knowledge base documents
builder.Services.AddAGUIServer();

var analyst = builder.AddAIAgent(AnalystAgent.Name, (sp, _) =>
    AnalystAgent.Create(AnalystAgent.ChatClient(builder.Configuration), sp));

// One session per AG-UI threadId: the approval must come back to the same session.
analyst.WithInMemorySessionStore(withIsolation: false);

var onCall = builder.AddOnCall();        // workflow: alarm → triage → on-call agent → approval → closing
builder.AddAnalystMcp();                 // the same analyst as an MCP tool (Claude Code, VS Code...)

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapAGUIServer(analyst, "/").WithMetadata(DashboardMapping.Options());   // tool results → dashboard
app.MapAGUIServer(onCall, "/oncall").WithMetadata(DashboardMapping.Options());
app.MapMcp("/mcp");
app.Run();
