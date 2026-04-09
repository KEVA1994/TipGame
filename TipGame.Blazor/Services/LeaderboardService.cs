using System.Net.Http.Json;
using TipGame.Shared.Models;

public class LeaderboardService
{
    private readonly HttpClient _http;

    public LeaderboardService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<LeaderboardDto>> GetLeaderboard()
    {
        return await _http.GetFromJsonAsync<List<LeaderboardDto>>("api/leaderboard")
               ?? new List<LeaderboardDto>();
    }
}
