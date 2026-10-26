using A2A.AspNetCore;
using Ipe.DistributionCenter;
using Microsoft.Agents.AI.Hosting;

// the DC Southeast agent, another process, exposed over A2A (Agent Card + HTTP+JSON).
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<Logistics>();

var dc = builder.AddAIAgent(DcAgent.Name, (sp, _) => DcAgent.Create(builder.Configuration, sp));
dc.AddA2AServer();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapA2AHttpJson(dc, DcAgent.Route);
app.MapWellKnownAgentCard(DcAgent.Card(builder.Configuration["DistributionCenter:PublicUrl"] ?? "http://localhost:5180"));
app.Run();
