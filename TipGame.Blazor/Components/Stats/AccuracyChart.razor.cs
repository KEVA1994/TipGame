using Microsoft.AspNetCore.Components;

namespace TipGame.Blazor.Components.Stats;

public partial class AccuracyChart
{
    [Parameter, EditorRequired] public List<AccuracyData> Players { get; set; } = [];
}
