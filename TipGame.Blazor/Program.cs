using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TipGame.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7048"),
    Timeout = TimeSpan.FromSeconds(10)
});

builder.Services.AddMudServices();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<PlayerState>();

await builder.Build().RunAsync();
