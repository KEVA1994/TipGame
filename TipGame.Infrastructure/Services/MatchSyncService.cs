using System.Text.Json;
using TipGame.Domain.Entities;

public class MatchSyncService
{
    private readonly Supabase.Client _supabase;
    private readonly HttpClient _httpClient;
    private readonly PredictionService _predictionService;

    private readonly string _apiUrl;

    public MatchSyncService(Supabase.Client supabase, string footballApiToken, string apiUrl)
    {
        _supabase = supabase;
        _apiUrl = apiUrl;
        _predictionService = new PredictionService();
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", footballApiToken);
    }

    public async Task SyncMatches()
    {
        var response = await _httpClient.GetAsync(_apiUrl);

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
                    HomeCrest = apiMatch.HomeTeam.Crest,
                    AwayCrest = apiMatch.AwayTeam.Crest,
                    KickoffTime = apiMatch.UtcDate,
                    Status = apiMatch.Status
                };

                await _supabase.From<Match>().Insert(match);
                count++;
            }
            else
            {
                var wasFinished = match.Status == "FINISHED";
                var homeScore = apiMatch.Score?.FullTime?.Home;
                var awayScore = apiMatch.Score?.FullTime?.Away;

                // Calculate minute from kickoff time for live matches
                int? minute = apiMatch.Status == "IN_PLAY"
                    ? (int)Math.Min((DateTime.UtcNow - apiMatch.UtcDate).TotalMinutes, 120)
                    : null;

                var updateResponse = await _supabase.From<Match>()
                    .Where(m => m.Id == match.Id)
                    .Set(m => m.Status, apiMatch.Status)
                    .Set(m => m.HomeScore, homeScore)
                    .Set(m => m.AwayScore, awayScore)
                    .Set(m => m.KickoffTime, apiMatch.UtcDate)
                    .Set(m => m.Minute, minute)
                    .Set(m => m.HomeCrest, apiMatch.HomeTeam.Crest)
                    .Set(m => m.AwayCrest, apiMatch.AwayTeam.Crest)
                    .Update();

                Console.WriteLine($"Updated {match.HomeTeam} vs {match.AwayTeam}: status={apiMatch.Status}, score={homeScore}-{awayScore}, minute={minute}, rows={updateResponse.Models.Count}");

                // Calculate points when a match finishes
                if (apiMatch.Status == "FINISHED" && !wasFinished && homeScore is not null && awayScore is not null)
                {
                    match.HomeScore = homeScore;
                    match.AwayScore = awayScore;
                    match.Status = "FINISHED";

                    var predResponse = await _supabase.From<Prediction>()
                        .Where(p => p.MatchId == match.Id)
                        .Get();

                    match.Predictions = predResponse.Models.ToList<Prediction>();
                    _predictionService.CalculatePoints(match);

                    foreach (var pred in match.Predictions)
                    {
                        await _supabase.From<Prediction>()
                            .Where(p => p.Id == pred.Id)
                            .Set(p => p.Points, pred.Points)
                            .Update();
                    }

                    Console.WriteLine($"Points calculated for {match.HomeTeam} vs {match.AwayTeam}");
                }
            }
        }

        Console.WriteLine($"Sync done — {result.Matches.Count} matches processed, {count} new.");
    }
}