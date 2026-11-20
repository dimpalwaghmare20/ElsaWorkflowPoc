using System.Text.Json;
using Elsa.Studio.Authentication.Abstractions.Models;
using Elsa.Studio.Authentication.ElsaIdentity.BlazorWasm.Extensions;
using Elsa.Studio.Authentication.ElsaIdentity.HttpMessageHandlers;
using Elsa.Studio.Authentication.ElsaIdentity.UI.Extensions;
using Elsa.Studio.Authentication.UI.Extensions;
using Elsa.Studio.Contracts;
using Elsa.Studio.Core.BlazorWasm.Extensions;
using Elsa.Studio.Dashboard.Extensions;
using Elsa.Studio.Extensions;
using Elsa.Studio.Localization.BlazorWasm.Extensions;
using Elsa.Studio.Localization.Models;
using Elsa.Studio.Models;
using Elsa.Studio.Options;
using Elsa.Studio.Shell;
using Elsa.Studio.Shell.Extensions;
using Elsa.Studio.Workflows.Designer.Extensions;
using Elsa.Studio.Workflows.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var configuration = builder.Configuration;

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.RegisterCustomElsaStudioElements();

// Sign in with Elsa Identity (username/password stored by the Elsa server).
builder.Services.AddStudioAuthenticationMode(options => options.Provider = StudioAuthenticationProvider.ElsaIdentity);
builder.Services.AddElsaIdentity();
builder.Services.AddElsaIdentityUI();

var backendApiConfig = new BackendApiConfig
{
    ConfigureBackendOptions = options => configuration.GetSection("Backend").Bind(options),
    ConfigureHttpClientBuilder = options => options.AuthenticationHandler = typeof(ElsaIdentityAuthenticatingApiHttpMessageHandler)
};

var localizationConfig = new LocalizationConfig
{
    ConfigureLocalizationOptions = options => configuration.GetSection("Localization").Bind(options)
};

builder.Services.AddCore();
builder.Services.AddShell();
builder.Services.AddAuthenticationUI();
builder.Services.AddRemoteBackend(backendApiConfig);
builder.Services.AddDashboardModule(backendApiConfig);
builder.Services.AddWorkflowsModule();
builder.Services.AddLocalizationModule(localizationConfig);

var app = builder.Build();

await app.UseElsaLocalization();

// The host page (_Host.cshtml) tells the client where the Elsa API lives.
var js = app.Services.GetRequiredService<IJSRuntime>();
var clientConfig = await js.InvokeAsync<JsonElement>("getClientConfig");
var apiUrl = clientConfig.GetProperty("apiUrl").GetString() ?? throw new InvalidOperationException("No API URL configured.");
app.Services.GetRequiredService<IOptions<BackendOptions>>().Value.Url = new(apiUrl);

await app.Services.GetRequiredService<IStartupTaskRunner>().RunStartupTasksAsync();

await app.RunAsync();
