using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TipGame.Blazor.Pages;

public partial class Stats
{
    [Inject] private StatsService StatsService { get; set; } = default!;

    private StatsData? data;
    private bool isLoading = true;
    private string? errorMessage;

    private List<ChartSeries<double>> pointSeries = [];
    private string[] pointLabels = [];

    private string? h2hPlayer1;
    private string? h2hPlayer2;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            data = await StatsService.GetStatsAsync();

            pointLabels = data.Dates.ToArray();
            pointSeries = data.CumulativePoints
                .Select(p => new ChartSeries<double> { Name = p.Name, Data = p.Values })
                .ToList();

            if (data.PlayerNames.Count >= 2)
            {
                h2hPlayer1 = data.PlayerNames[0];
                h2hPlayer2 = data.PlayerNames[1];
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Kunne ikke hente statistik: {ex.Message}";
            Console.WriteLine($"[Stats] Error: {ex}");
        }

        isLoading = false;
        StateHasChanged();
    }
}
