using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Home
{
    [Inject] private MatchService MatchService { get; set; } = default!;
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const string ShareLink = "https://keva1994.github.io/TipGame/";
    private bool linkCopied;

    private List<MatchDto> todayMatches = new();
    private List<LeaderboardDto> topPlayers = new();
    private int totalMatches;
    private int totalPlayers;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        var matchesTask = MatchService.GetMatches();
        var leaderboardTask = LeaderboardService.GetLeaderboard();

        try
        {
            var allMatches = await matchesTask;
            totalMatches = allMatches.Count;
            todayMatches = allMatches
                .Where(m => m.KickoffTime.Date == DateTime.Today)
                .ToList();
        }
        catch { }

        try
        {
            var leaderboard = await leaderboardTask;
            totalPlayers = leaderboard.Count;
            topPlayers = leaderboard.Take(3).ToList();
        }
        catch { }

        isLoading = false;
    }

    private async Task CopyShareLink()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", ShareLink);
            linkCopied = true;
            StateHasChanged();
            await Task.Delay(2000);
            linkCopied = false;
            StateHasChanged();
        }
        catch { }
    }
}
