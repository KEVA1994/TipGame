using System.Text.Json;
using TipGame.Domain.Entities;

public class MatchSyncService
{
    private readonly Supabase.Client _supabase;
    private readonly HttpClient _httpClient;

    public MatchSyncService(Supabase.Client supabase)
    {
        _supabase = supabase;
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

        var existingMatches = (await _supabase.From<Match>().Get()).Models;
        var count = 0;

        foreach (var apiMatch in result.Matches)
        {
            var match = existingMatches.FirstOrDefault(m => m.ExternalId == apiMatch.Id);

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

                await _supabase.From<Match>().Insert(match);
                count++;
            }
            else
            {
                await _supabase.From<Match>()
                    .Where(m => m.Id == match.Id)
                    .Set(m => m.Status, apiMatch.Status)
                    .Set(m => m.HomeScore, apiMatch.Score.FullTime.Home)
                    .Set(m => m.AwayScore, apiMatch.Score.FullTime.Away)
                    .Update();
            }
        }

        Console.WriteLine($"Sync done — {result.Matches.Count} matches processed, {count} new.");
    }
}