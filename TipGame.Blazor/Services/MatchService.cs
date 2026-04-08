using System.Net.Http.Json;
using TipGame.Shared.Models;

public class MatchService
{
    private readonly HttpClient _http;

    public MatchService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<MatchDto>> GetMatches()
    {
        var result = await _http.GetFromJsonAsync<List<MatchDto>>("api/matches");
        return result ?? new List<MatchDto>();
    }
}