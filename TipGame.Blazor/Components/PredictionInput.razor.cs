using Microsoft.AspNetCore.Components;
using MudBlazor;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Components;

public partial class PredictionInput
{
    [Inject] private PredictionService PredictionService { get; set; } = default!;

    [Parameter] public int MatchId { get; set; }
    [Parameter] public DateTime Kickoff { get; set; }
    [Parameter] public string PlayerName { get; set; } = string.Empty;
    [Parameter] public string? AuthId { get; set; }
    [Parameter] public PredictionDto? ExistingTip { get; set; }
    [Parameter] public EventCallback<PredictionDto> OnTipSaved { get; set; }

    private int? home;
    private int? away;
    private bool isSaved;
    private bool isEditing;
    private string? _previousAuthId;

    private bool CanEdit => !string.IsNullOrEmpty(AuthId) && DateTime.UtcNow < Kickoff.AddHours(-1);
    private bool IsReadOnly => !CanEdit || (isSaved && !isEditing);

    protected override void OnParametersSet()
    {
        var playerChanged = _previousAuthId != AuthId;
        _previousAuthId = AuthId;

        if (playerChanged)
        {
            isSaved = false;
            isEditing = false;
            home = null;
            away = null;
        }

        if (ExistingTip is not null && !isSaved && !isEditing)
        {
            home = ExistingTip.HomeScore;
            away = ExistingTip.AwayScore;
            isSaved = true;
        }
    }

    private void Edit()
    {
        isEditing = true;
    }

    private async Task Save()
    {
        if (string.IsNullOrEmpty(AuthId)) return;
        await PredictionService.SaveTip(AuthId, MatchId, home!.Value, away!.Value);
        isSaved = true;
        isEditing = false;
        await OnTipSaved.InvokeAsync(new PredictionDto { MatchId = MatchId, HomeScore = home!.Value, AwayScore = away!.Value });
    }
}
