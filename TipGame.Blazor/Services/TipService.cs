using System.Net.Http.Json;

public class TipService
{
    private readonly HttpClient _http;

    public TipService(HttpClient http)
    {
        _http = http;
    }

    public async Task SaveTip(int matchId, int home, int away)
    {
        var tip = new
        {
            MatchId = matchId,
            HomeScore = home,
            AwayScore = away
        };

        await _http.PostAsJsonAsync("api/tips", tip);
    }

    public async Task<List<TipDto>> GetTips()
    {
        return await _http.GetFromJsonAsync<List<TipDto>>("api/tips")
               ?? new List<TipDto>();
    }
}