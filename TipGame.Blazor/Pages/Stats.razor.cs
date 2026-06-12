using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace TipGame.Blazor.Pages;

public partial class Stats
{
    private const string TabStorageKey = "stats.activeTab";

    [Inject] private StatsService StatsService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private StatsData? data;
    private bool isLoading = true;
    private string? errorMessage;
    private int activeTab;

    private List<ChartSeries<double>> pointSeries = [];
    private string[] pointLabels = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            var storedTab = await JS.InvokeAsync<string?>("localStorage.getItem", TabStorageKey);
            if (int.TryParse(storedTab, out var tab) && tab is >= 0 and <= 3)
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
