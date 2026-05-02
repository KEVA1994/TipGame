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
        if (allNames.Count == 0 && Series.Count > 0)
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

    [Parameter] public List<PlayerSeries> CumulativePoints { get; set; } = [];

    private List<(PlayerStanding Player, int Index)> GetCurrentStandings()
    {
        return CumulativePoints
            .Select(p => new PlayerStanding
            {
                Name = p.Name,
                CurrentPoints = p.Values.Length > 0 ? (int)p.Values[^1] : 0
            })
            .OrderByDescending(p => p.CurrentPoints)
            .Select((p, i) => (p, i))
            .ToList();
    }

    private List<DailyChange> GetLatestChanges()
    {
        return CumulativePoints
            .Select(p =>
            {
                var current = p.Values.Length > 0 ? (int)p.Values[^1] : 0;
                var previous = p.Values.Length > 1 ? (int)p.Values[^2] : 0;
                return new DailyChange { Name = p.Name, Delta = current - previous };
            })
            .OrderByDescending(c => c.Delta)
            .ToList();
    }

    private sealed class PlayerStanding
    {
        public string Name { get; init; } = "";
        public int CurrentPoints { get; init; }
    }

    private sealed class DailyChange
    {
        public string Name { get; init; } = "";
        public int Delta { get; init; }
    }
}
