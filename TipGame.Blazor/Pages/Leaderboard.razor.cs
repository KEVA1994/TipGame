using Microsoft.AspNetCore.Components;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Leaderboard
{
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;
    [Inject] private CompetitionState CompetitionState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private List<LeaderboardDto> players = [];
    private string latestDay = "";
    private bool isLoading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await CompetitionState.InitializeAsync();

        try
        {
            players = CompetitionState.Current is { } comp
                ? await LeaderboardService.GetLeaderboard(comp.Id)
                : [];
        }
        catch
        {
            players = [];
        }

        latestDay = players
            .SelectMany(p => p.DailyPoints.Keys)
            .Distinct()
            .OrderBy(d => DateTime.ParseExact(d, "dd/MM", null))
            .LastOrDefault() ?? "";

        isLoading = false;
        StateHasChanged();
    }

    private void GoToPlayer(string userName) =>
        Nav.NavigateTo($"player/{Uri.EscapeDataString(userName)}");
}
