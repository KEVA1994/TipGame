using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TipGame.Domain.Entities;
using TipGame.Infrastructure.Data;

public class MatchSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;

    public MatchSyncService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", "0d3ba9ce8e38458387268cdf58a0e211");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncMatches();

            // kør hver 5 min
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task SyncMatches()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var predictionService = scope.ServiceProvider.GetRequiredService<PredictionService>();

        var response = await _httpClient.GetAsync("https://api.football-data.org/v4/matches");

        if (!response.IsSuccessStatusCode)
            return;

        var json = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<FootballApiResponse>(
            json, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true});

        if (result is null)
            return;

        foreach (var apiMatch in result.Matches)
        {
            var match = await context.Matches
                .Include(m => m.Predictions)
                .FirstOrDefaultAsync(m => m.ExternalId == apiMatch.Id);

            if (match == null)
            {
                // opret ny match
                match = new Match
                {
                    ExternalId = apiMatch.Id,
                    HomeTeam = apiMatch.HomeTeam.Name,
                    AwayTeam = apiMatch.AwayTeam.Name,
                    KickoffTime = apiMatch.UtcDate,
                    Status = apiMatch.Status
                };

                context.Matches.Add(match);
            }
            else
            {
                // opdater eksisterende
                match.Status = apiMatch.Status;

                if (apiMatch.Status == "FINISHED")
                {
                    match.HomeScore = apiMatch.Score.FullTime.Home;
                    match.AwayScore = apiMatch.Score.FullTime.Away;

                    predictionService.CalculatePoints(match);
                }
            }
        }

        await context.SaveChangesAsync();
    }
}