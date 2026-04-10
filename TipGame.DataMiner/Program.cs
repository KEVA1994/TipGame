using System.Reflection;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();

var supabaseUrl = config["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url not configured");
var supabaseKey = config["Supabase:Key"]
    ?? throw new InvalidOperationException("Supabase:Key not configured");
var supabase = new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false
});
await supabase.InitializeAsync();

var syncService = new MatchSyncService(supabase,
    config["FootballApi:Token"] ?? throw new InvalidOperationException("FootballApi:Token not configured"),
    config["FootballApi:Url"] ?? "https://api.football-data.org/v4/competitions/PL/matches?dateFrom=2026-03-30&dateTo=2026-04-27");

Console.WriteLine("Starting match sync...");
await syncService.SyncMatches();
Console.WriteLine("Done.");
