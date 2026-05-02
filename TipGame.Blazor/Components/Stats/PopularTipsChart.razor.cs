using Microsoft.AspNetCore.Components;

namespace TipGame.Blazor.Components.Stats;

public partial class PopularTipsChart
{
    [Parameter, EditorRequired] public List<PopularTip> Tips { get; set; } = [];

    private int MaxCount => Tips.Count > 0 ? Tips.Max(t => t.Count) : 1;
}
