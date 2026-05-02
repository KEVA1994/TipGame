using Microsoft.AspNetCore.Components;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Leaderboard
{
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;

    private List<LeaderboardDto> players = [];
    private List<string> matchDays = [];
    private string latestDay = "";
    private bool isLoading = true;
    private Dictionary<string, int> maxPointsByDay = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            players = await LeaderboardService.GetLeaderboard();
        }
        catch
        {
            players = [];
        }

        matchDays = players
            .SelectMany(p => p.DailyPoints.Keys)
            .Distinct()
            .OrderBy(d => DateTime.ParseExact(d, "dd/MM", null))
            .ToList();

        latestDay = matchDays.LastOrDefault() ?? "";

        maxPointsByDay = matchDays.ToDictionary(
            day => day,
            day => players.Max(p => p.DailyPoints.GetValueOrDefault(day)));

        isLoading = false;
        StateHasChanged();
    }
}
