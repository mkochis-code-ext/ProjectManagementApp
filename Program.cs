using ProjectManagementApp.Components;
using ProjectManagementApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Data access goes through the API process (sole owner of the JSON file).
var apiBaseUrl = builder.Configuration["Endpoints:Api"] ?? "http://localhost:5180";
builder.Services.AddHttpClient<IBoardService, BoardApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Health monitoring of the other processes for the UI dashboard.
var mcpBaseUrl = builder.Configuration["Endpoints:Mcp"] ?? "http://localhost:5190";
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddHttpClient("mcp", client =>
{
    client.BaseAddress = new Uri(mcpBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddScoped<ProjectManagementApp.HealthChecks.HealthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
