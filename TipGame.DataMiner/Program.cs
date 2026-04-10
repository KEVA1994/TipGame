using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var supabaseUrl = config["Supabase:Url"]!;
var supabaseKey = config["Supabase:Key"]!;
var supabase = new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false
});
await supabase.InitializeAsync();

var syncService = new MatchSyncService(supabase);

Console.WriteLine("Starting match sync...");
await syncService.SyncMatches();
Console.WriteLine("Done.");
