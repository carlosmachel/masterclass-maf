var builder = DistributedApplication.CreateBuilder(args);

// the DC Southeast agent (another "company", another process), speaking A2A. Fixed port 5180: it's the
// Agent Card URL (http://localhost:5180/.well-known/agent-card.json).
var dc = builder.AddProject<Projects.Ipe_DistributionCenter>("dc")
    .WithEnvironment("AZURE_TOKEN_CREDENTIALS", "dev")
    .WithEnvironment(e =>
    {
        if (builder.Configuration["Azure:TenantId"] is { Length: > 0 } tenant) e.EnvironmentVariables["AZURE_TENANT_ID"] = tenant;
    });

var agent = builder.AddProject<Projects.Ipe_Agent>("agent")
    // Local: developer credentials only (az login). Without it DefaultAzureCredential tries
    // managed identity (IMDS 169.254.169.254): ~1 s more per question and red spans in the trace.
    .WithEnvironment("AZURE_TOKEN_CREDENTIALS", "dev")
    // Tenant of the demo resources (user-secret Azure:TenantId): az login may point to another one.
    .WithEnvironment(e =>
    {
        if (builder.Configuration["Azure:TenantId"] is { Length: > 0 } tenant) e.EnvironmentVariables["AZURE_TENANT_ID"] = tenant;
    })
    // Demo only: the dashboard's "Prompt Shields" switch can turn the defense off to show the attack.
    .WithEnvironment("Security__AllowDashboardToggle", "true")
    // Local model (Ollama) for the dashboard's model picker: ollama/Modelfile.
    .WithEnvironment("LocalModel__Model", "qwen3-ipe-8b")
    // The on-call agent discovers the DC through its Agent Card (A2A). Without the DC up, RequestDcProposal returns an error to the agent.
    .WithEnvironment("DistributionCenter__Url", "http://localhost:5180");

builder.AddViteApp("web", "../../web")
    .WithEndpoint("http", e => e.Port = 4280)   // fixed URL: http://localhost:4280 (off Vite's default 5173)
    .WithReference(agent)
    .WaitFor(agent);

builder.Build().Run();
