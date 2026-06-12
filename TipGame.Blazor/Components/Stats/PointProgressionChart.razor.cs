using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace TipGame.Blazor.Components.Stats;

public partial class PointProgressionChart
{
    private const string StorageKey = "stats.pointChart.players";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public List<ChartSeries<double>> Series { get; set; } = [];
    [Parameter, EditorRequired] public string[] Labels { get; set; } = [];

    private int selectedIndex = -1;
    private HashSet<string> visibleNames = [];
    private List<string> allNames = [];
    private List<ChartSeries<double>> filteredSeries = [];
    private MudAutocomplete<string>? playerSearch;

    private readonly LineChartOptions lineOptions = new()
    {
        ShowDataMarkers = true,
        YAxisTicks = 0,
        ShowToolTips = true,
    };

    private Task<IEnumerable<string>> SearchPlayers(string value, CancellationToken _)
    {
        // Offer the players that aren't already selected, filtered by the search text.
        var candidates = allNames.Where(n => !visibleNames.Contains(n));
        if (!string.IsNullOrWhiteSpace(value))
            candidates = candidates.Where(n => n.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(candidates);
    }

    protected override void OnParametersSet()
    {
        if (allNames.Count == 0 && Series.Count > 0 && Labels.Length > 0)
        {
            // No players selected by default — the user picks who to follow.
            allNames = Series.Select(s => s.Name).ToList();
        }
        UpdateFilteredSeries();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Restore the followed players from the browser; no stored value means "none selected".
        try
        {
            var stored = await JS.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (stored is not null)
            {
                var names = JsonSerializer.Deserialize<List<string>>(stored) ?? [];
                visibleNames = names.Where(allNames.Contains).ToHashSet();
                UpdateFilteredSeries();
                StateHasChanged();
            }
        }
        catch
        {
            // Corrupt/unavailable storage — keep the default (no players selected).
        }
    }

    private async Task AddPlayer(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;

        visibleNames.Add(name);
        UpdateFilteredSeries();
        _ = PersistSelectionAsync();

        // Reset the autocomplete so it's ready for the next search.
        if (playerSearch is not null)
            await playerSearch.ClearAsync();
    }

    private void RemovePlayer(string name)
    {
        visibleNames.Remove(name);
        UpdateFilteredSeries();
        _ = PersistSelectionAsync();
    }

    private async Task PersistSelectionAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", StorageKey,
                JsonSerializer.Serialize(visibleNames.ToList()));
        }
        catch
        {
            // Storage unavailable (private mode etc.) — selection just won't survive a refresh.
        }
    }

    private void UpdateFilteredSeries() =>
        filteredSeries = Series.Where(s => visibleNames.Contains(s.Name)).ToList();
}
