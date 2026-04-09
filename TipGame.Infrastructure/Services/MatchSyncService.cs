using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TipGame.Domain.Entities;
using TipGame.Infrastructure.Data;

public class MatchSyncService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;

    public MatchSyncService(AppDbContext context)
    {
        _context = context;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", "0d3ba9ce8e38458387268cdf58a0e211");
    }

    public async Task SyncMatches()
    {
        var response = await _httpClient.GetAsync("https://api.football-data.org/v4/competitions/PL/matches?dateFrom=2026-03-30&dateTo=2026-04-27");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"API returned {response.StatusCode}");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<FootballApiResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result is null)
            return;

        var count = 0;

        foreach (var apiMatch in result.Matches)
        {
            var match = await _context.Matches
                .FirstOrDefaultAsync(m => m.ExternalId == apiMatch.Id);

            if (match == null)
            {
                match = new Match
                {
                    ExternalId = apiMatch.Id,
                    HomeTeam = apiMatch.HomeTeam.Name,
                    AwayTeam = apiMatch.AwayTeam.Name,
                    KickoffTime = apiMatch.UtcDate,
                    Status = apiMatch.Status
                };

                _context.Matches.Add(match);
                count++;
            }
            else
            {
                match.Status = apiMatch.Status;
                match.HomeScore = apiMatch.Score.FullTime.Home;
                match.AwayScore = apiMatch.Score.FullTime.Away;
            }
        }

        await _context.SaveChangesAsync();

        Console.WriteLine($"Sync done — {result.Matches.Count} matches processed, {count} new.");
    }
}