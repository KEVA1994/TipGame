using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TipGame.Infrastructure.Data;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();

services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("Default")));

services.AddScoped<MatchSyncService>();

var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();
var syncService = scope.ServiceProvider.GetRequiredService<MatchSyncService>();

Console.WriteLine("Starting match sync...");
await syncService.SyncMatches();
Console.WriteLine("Done.");
