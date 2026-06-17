using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Home
{
    [Inject] private MatchService MatchService { get; set; } = default!;
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "goto")]
    public string? GoTo { get; set; }

    private const string ShareLink = "https://keva1994.github.io/TipGame/";
    private bool linkCopied;

    private int totalMatches;
    private int totalPlayers;

    protected override async Task OnInitializedAsync()
    {
        var matchesTask = MatchService.GetMatches();
        var leaderboardTask = LeaderboardService.GetLeaderboard();

        try
        {
            totalMatches = (await matchesTask).Count;
        }
        catch { }

        try
        {
            totalPlayers = (await leaderboardTask).Count;
        }
        catch { }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && string.Equals(GoTo, "pay", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await JS.InvokeVoidAsync("scrollToElement", "mobilepay");
            }
            catch { }
        }
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
