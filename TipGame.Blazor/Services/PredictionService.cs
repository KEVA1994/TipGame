using System.Net.Http.Json;
using TipGame.Shared.Models;

public class PredictionService
{
    private readonly HttpClient _http;

    public PredictionService(HttpClient http)
    {
        _http = http;
    }

    public async Task SaveTip(string clientId, string name, int matchId, int home, int away)
    {
        var prediction = new
        {
            ClientId = clientId,
            Name = name,
            MatchId = matchId,
            HomeScore = home,
            AwayScore = away
        };

        await _http.PostAsJsonAsync("api/predictions", prediction);
    }

    public async Task<List<PredictionDto>> GetPredictions(string clientId)
    {
        return await _http.GetFromJsonAsync<List<PredictionDto>>($"api/predictions/user?clientId={clientId}")
               ?? new List<PredictionDto>();
    }
}