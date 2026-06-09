using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TipGame.Blazor.Components.Stats;

public partial class PointProgressionChart
{
    [Parameter, EditorRequired] public List<ChartSeries<double>> Series { get; set; } = [];
    [Parameter, EditorRequired] public string[] Labels { get; set; } = [];

    private int selectedIndex = -1;
    private HashSet<string> visibleNames = [];
    private List<string> allNames = [];
    private List<ChartSeries<double>> filteredSeries = [];

    private readonly LineChartOptions lineOptions = new()
    {
        ShowDataMarkers = true,
        YAxisTicks = 0,
        ShowToolTips = true,
    };

    protected override void OnParametersSet()
    {
        if (allNames.Count == 0 && Series.Count > 0 && Labels.Length > 0)
        {
            allNames = Series.Select(s => s.Name).ToList();
            visibleNames = [.. allNames];
        }
        UpdateFilteredSeries();
    }

    private void TogglePlayer(string name)
    {
        if (!visibleNames.Remove(name))
            visibleNames.Add(name);
        UpdateFilteredSeries();
    }

    private void UpdateFilteredSeries() =>
        filteredSeries = Series.Where(s => visibleNames.Contains(s.Name)).ToList();
}
