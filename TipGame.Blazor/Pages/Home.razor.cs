using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TipGame.Blazor.Pages;

public partial class Home : IDisposable
{
    [Inject] private MatchService MatchService { get; set; } = default!;
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;
    [Inject] private PlayerState PlayerState { get; set; } = default!;
    [Inject] private CompetitionState CompetitionState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool linkCopied;
    private bool accountPopoverOpen;
    private bool isReady;

    private int totalMatches;
    private int totalPlayers;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await PlayerState.InitializeAsync();
        await CompetitionState.InitializeAsync();
        PlayerState.OnChange += HandleStateChanged;
        CompetitionState.OnChange += HandleStateChanged;

        await LoadStatsAsync();
        isReady = true;
        StateHasChanged();
    }

    private async Task LoadStatsAsync()
    {
        if (CompetitionState.Current is not { } comp)
        {
            totalMatches = 0;
            totalPlayers = 0;
            return;
        }

        var matchesTask = MatchService.GetMatches(comp.Id);
        var leaderboardTask = LeaderboardService.GetLeaderboard(comp.Id);

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

    private void HandleStateChanged() => _ = OnStateChangedAsync();

    private async Task OnStateChangedAsync()
    {
        await LoadStatsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CopyShareLink()
    {
        if (CompetitionState.Current is not { } comp) return;
        try
        {
            var link = $"{Nav.BaseUri}join/{comp.InviteToken}";
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", link);
            linkCopied = true;
            StateHasChanged();
            await Task.Delay(2000);
            linkCopied = false;
            StateHasChanged();
        }
        catch { }
    }

    public void Dispose()
    {
        PlayerState.OnChange -= HandleStateChanged;
        CompetitionState.OnChange -= HandleStateChanged;
    }
}
