using Azure.Identity;

using Ipe.Agent.Security;

namespace Ipe.Agent;

public static class SecurityRegistration
{
    // ContentSafety:Endpoint; without it, the same AI Services resource as the model (AzureOpenAI:Endpoint).
    public static IHostApplicationBuilder AddSecurity(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient(nameof(PromptShields));
        builder.Services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var endpoint = cfg["ContentSafety:Endpoint"] is { Length: > 0 } cs ? cs : cfg["AzureOpenAI:Endpoint"]!;
            return new PromptShields(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PromptShields)),
                new Uri(endpoint), new DefaultAzureCredential());
        });
        builder.Services.AddSingleton<DocumentShield>();
        return builder;
    }
}
