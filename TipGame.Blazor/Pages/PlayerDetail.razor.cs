using Microsoft.AspNetCore.Components;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class PlayerDetail
{
    [Parameter] public string Name { get; set; } = "";

    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;
    [Inject] private CompetitionState CompetitionState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PlayerDetailDto? player;
    private bool isLoading = true;

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;

        try
        {
            await CompetitionState.InitializeAsync();
            player = CompetitionState.Current is { } comp
                ? await LeaderboardService.GetPlayerDetail(comp.Id, Name)
                : null;
        }
        catch
        {
            // Leave player null so the page shows the not-found state.
            player = null;
        }

        isLoading = false;
    }

    private void GoBack() => Nav.NavigateTo("leaderboard");

    private void GoToMatch(int matchId) => Nav.NavigateTo($"matches/{matchId}");

    // Danish day names — WASM runs under the invariant culture, so "dddd"
    // would render English names.
    private static string FormatDate(DateTime kickoff)
    {
        var day = kickoff.DayOfWeek switch
        {
            DayOfWeek.Monday => "mandag",
            DayOfWeek.Tuesday => "tirsdag",
            DayOfWeek.Wednesday => "onsdag",
            DayOfWeek.Thursday => "torsdag",
            DayOfWeek.Friday => "fredag",
            DayOfWeek.Saturday => "lørdag",
            DayOfWeek.Sunday => "søndag",
            _ => ""
        };
        return $"{day} {kickoff:dd-MM-yyyy}";
    }
}
