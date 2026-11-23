using System.Security.Cryptography;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Identity;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using ElsaPoc.Server.Endpoints;
using ElsaPoc.Server.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

var services = builder.Services;
var configuration = builder.Configuration;
var identityTokenSection = configuration.GetSection("Identity:Tokens");

var adminUserName = configuration["Identity:Bootstrap:UserName"]
    ?? throw new InvalidOperationException("Identity:Bootstrap:UserName is required.");
var adminPassword = configuration["Identity:Bootstrap:Password"]
    ?? throw new InvalidOperationException("Identity:Bootstrap:Password is required.");

// For the POC, fall back to a per-process random signing key (users simply sign in again after a restart).
var signingKey = identityTokenSection["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
    signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

services.AddElsa(elsa => elsa
    .UseIdentity(identity =>
    {
        identity.TokenOptions += options =>
        {
            identityTokenSection.Bind(options);
            options.SigningKey = signingKey;
        };
        identity.UseEntityFrameworkCore(ef => ef.UseSqlite());
        identity.UseDefaultAdmin(admin => admin
            .WithAdminUserName(adminUserName)
            .WithAdminPassword(adminPassword)
            .WithAdminRoleName("admin")
            .WithAdminRolePermissions(new List<string> { "*" }));
    })
    .UseDefaultAuthentication()
    .UseWorkflowManagement(management =>
    {
        management.UseEntityFrameworkCore(ef => ef.UseSqlite());
        management.AddVariableType<ExpenseRequest>("ElsaPoc");
        management.AddVariableType<PurchaseRequest>("ElsaPoc");
    })
    .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite()))
    .UseScheduling()
    .UseJavaScript()
    .UseLiquid()
    .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
    .UseWorkflowsApi()
    .UseDashboardApi()
    .UseWorkflowRuntimeDashboard()
    .AddActivitiesFrom<Program>()
    .AddWorkflowsFrom<Program>());

services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("*")));
services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapWorkflowsApi();
app.UseWorkflows();
app.MapDemoEndpoints();
app.MapProcurementEndpoints();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");
app.Run();
