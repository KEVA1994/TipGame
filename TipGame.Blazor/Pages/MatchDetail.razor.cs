using Microsoft.AspNetCore.Components;
using MudBlazor;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class MatchDetail
{
    [Parameter] public int Id { get; set; }

    [Inject] private MatchService MatchService { get; set; } = default!;
    [Inject] private PredictionService PredictionService { get; set; } = default!;
    [Inject] private PlayerState PlayerState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private MatchDto? match;
    private List<PredictionDto> predictions = new();
    private int exactCount;
    private bool isLoading = true;

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;
        await PlayerState.InitializeAsync();

        var matchTask = MatchService.GetMatch(Id);
        var predsTask = PredictionService.GetMatchPredictions(Id);

        try
        {
            match = await matchTask;
            predictions = await predsTask;
            exactCount = predictions.Count(p => p.Points == 3);
        }
        catch
        {
            // Leave match null so the page shows the not-found state.
            match = null;
        }

        isLoading = false;
    }

    private void GoBack() => Nav.NavigateTo("matches");

    // Danish names built by hand — WASM runs under the invariant culture, so
    // "dddd"/"MMMM" would render English month and day names.
    private static string FormatKickoff(DateTime kickoff)
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
        return $"{day} {kickoff:dd-MM-yyyy} kl. {kickoff:HH:mm}";
    }

    private string StatusLabel => match?.Status switch
    {
        "FINISHED" => "Slut",
        "IN_PLAY" => "Live",
        "PAUSED" => "Pause",
        "TIMED" or "SCHEDULED" => "Ikke spillet",
        "POSTPONED" => "Udsat",
        "CANCELLED" => "Aflyst",
        _ => match?.Status ?? ""
    };

    private Color StatusColor => match?.Status switch
    {
        "IN_PLAY" or "PAUSED" => Color.Primary,
        "FINISHED" => Color.Default,
        _ => Color.Secondary
    };
}
