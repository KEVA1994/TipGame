using Microsoft.AspNetCore.Components;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Leaderboard
{
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;

    private List<LeaderboardDto> players = new();
    private string latestDay = "";
    private bool isLoading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            players = await LeaderboardService.GetLeaderboard();
        }
        catch
        {
            players = new();
        }

        var matchDays = players
            .SelectMany(p => p.DailyPoints.Keys)
            .Distinct()
            .OrderBy(d => DateTime.ParseExact(d, "dd/MM", null))
            .ToList();

        latestDay = matchDays.LastOrDefault() ?? "";

        isLoading = false;
        StateHasChanged();
    }
}
