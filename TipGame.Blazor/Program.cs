using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using TipGame.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var supabaseUrl = builder.Configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url not configured");
var supabaseKey = builder.Configuration["Supabase:Key"] ?? throw new InvalidOperationException("Supabase:Key not configured");
var supabase = new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false,
    AutoRefreshToken = true
});
builder.Services.AddSingleton(supabase);

builder.Services.AddMudServices();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<PlayerState>();

var host = builder.Build();

// Persist the Supabase session in localStorage so the user stays logged in
// across page reloads. Requires the in-process JS runtime (Blazor WebAssembly).
var js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
supabase.Auth.SetPersistence(new LocalStorageSessionPersistence(js));
supabase.Auth.LoadSession();
await supabase.InitializeAsync();

await host.RunAsync();
