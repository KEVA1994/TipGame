using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var supabaseUrl = config["Supabase:Url"] ?? config["SUPABASE_URL"]
    ?? throw new InvalidOperationException("Supabase URL not configured");
var supabaseKey = config["Supabase:Key"] ?? config["SUPABASE_KEY"]
    ?? throw new InvalidOperationException("Supabase Key not configured");
var supabase = new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false
});
await supabase.InitializeAsync();

var syncService = new MatchSyncService(supabase, config["FootballApi:Token"] ?? config["FOOTBALL_API_TOKEN"]);

Console.WriteLine("Starting match sync...");
await syncService.SyncMatches();
Console.WriteLine("Done.");
