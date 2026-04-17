using Microsoft.AspNetCore.Components;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Home
{
    [Inject] private MatchService MatchService { get; set; } = default!;
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;

    private List<MatchDto> todayMatches = new();
    private List<LeaderboardDto> topPlayers = new();
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        var matchesTask = MatchService.GetMatches();
        var leaderboardTask = LeaderboardService.GetLeaderboard();

        try
        {
            var allMatches = await matchesTask;
            todayMatches = allMatches
                .Where(m => m.KickoffTime.Date == DateTime.Today)
                .ToList();
        }
        catch { }

        try
        {
            var leaderboard = await leaderboardTask;
            topPlayers = leaderboard.Take(3).ToList();
        }
        catch { }

        isLoading = false;
    }
}
