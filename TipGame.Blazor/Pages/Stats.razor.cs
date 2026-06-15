using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Stats
{
    private const string TabStorageKey = "stats.activeTab";

    [Inject] private StatsService StatsService { get; set; } = default!;
    [Inject] private LeaderboardService LeaderboardService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private StatsData? data;
    private bool isLoading = true;
    private string? errorMessage;
    private int activeTab;

    private List<ChartSeries<double>> pointSeries = [];
    private string[] pointLabels = [];

    private List<LeaderboardDto> players = [];
    private List<string> matchDays = [];
    private Dictionary<string, int> maxPointsByDay = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            var storedTab = await JS.InvokeAsync<string?>("localStorage.getItem", TabStorageKey);
            if (int.TryParse(storedTab, out var tab) && tab is >= 0 and <= 4)
                activeTab = tab;
        }
        catch
        {
            // Storage unavailable — start on the first tab.
        }

        try
        {
            data = await StatsService.GetStatsAsync();

            pointLabels = data.Dates.ToArray();
            pointSeries = data.CumulativePoints
                .Select(p => new ChartSeries<double> { Name = p.Name, Data = p.Values })
                .ToList();

            players = await LeaderboardService.GetLeaderboard();
            matchDays = players
                .SelectMany(p => p.DailyPoints.Keys)
                .Distinct()
                .OrderBy(d => DateTime.ParseExact(d, "dd/MM", null))
                .ToList();
            maxPointsByDay = matchDays.ToDictionary(
                day => day,
                day => players.Max(p => p.DailyPoints.GetValueOrDefault(day)));
        }
        catch (Exception ex)
        {
            errorMessage = $"Kunne ikke hente statistik: {ex.Message}";
            Console.WriteLine($"[Stats] Error: {ex}");
        }

        isLoading = false;
        StateHasChanged();
    }

    private void OnTabChanged(int index)
    {
        activeTab = index;
        _ = PersistTabAsync(index);
    }

    private async Task PersistTabAsync(int index)
    {
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", TabStorageKey, index.ToString());
        }
        catch
        {
            // Storage unavailable — the tab just won't survive a refresh.
        }
    }
}
