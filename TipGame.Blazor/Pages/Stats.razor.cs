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
    private List<ChartSeries<double>> popularSeries = [];
    private string[] popularLabels = [];
    private string[] pointLabels = [];
    private string[] accuracyLabels = ["Exakt (3p)", "Rigtigt udfald (1p)", "Forkert (0p)"];

    private string? h2hPlayer1;
    private string? h2hPlayer2;

    private LineChartOptions lineOptions = new()
    {
        XAxisTitle = "Runde",
        YAxisTitle = "Point",
        ShowDataMarkers = true,
        LineStrokeWidth = 2.5,
        XAxisLabelRotation = 90
    };

    private BarChartOptions popularBarOptions = new()
    {
        XAxisTitle = "Tippet resultat",
        YAxisTitle = "Antal tips",
        XAxisLabelRotation = 90
    };

    private BarChartOptions h2hBarOptions = new()
    {
        XAxisTitle = "Kamp",
        YAxisTitle = "Point",
        XAxisLabelRotation = 90
    };

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

            popularLabels = data.PopularTips.Select(t => t.Score).ToArray();
            popularSeries =
            [
                new ChartSeries<double> { Name = "Antal tips", Data = data.PopularTips.Select(t => (double)t.Count).ToArray() }
            ];

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

    private List<ChartSeries<double>> GetH2HSeries()
    {
        if (data is null || h2hPlayer1 is null || h2hPlayer2 is null)
            return [];

        var p1 = data.HeadToHead.FirstOrDefault(h => h.Name == h2hPlayer1);
        var p2 = data.HeadToHead.FirstOrDefault(h => h.Name == h2hPlayer2);
        if (p1 is null || p2 is null) return [];

        return
        [
            new ChartSeries<double>
            {
                Name = h2hPlayer1,
                Data = data.MatchIds.Select(id => (double)p1.PointsByMatch.GetValueOrDefault(id)).ToArray()
            },
            new ChartSeries<double>
            {
                Name = h2hPlayer2,
                Data = data.MatchIds.Select(id => (double)p2.PointsByMatch.GetValueOrDefault(id)).ToArray()
            }
        ];
    }

    private double[] GetAccuracyData(AccuracyData player) =>
        [player.Exact, player.CorrectOutcome, player.Wrong];

    private int GetH2HTotal(string playerName) =>
        data?.MatchIds.Sum(id => data.HeadToHead.First(h => h.Name == playerName).PointsByMatch.GetValueOrDefault(id)) ?? 0;

    private int GetH2HWins(string playerName, string opponentName) =>
        data?.MatchIds.Count(id =>
            data.HeadToHead.First(h => h.Name == playerName).PointsByMatch.GetValueOrDefault(id) >
            data.HeadToHead.First(h => h.Name == opponentName).PointsByMatch.GetValueOrDefault(id)) ?? 0;

    private int GetH2HDraws() =>
        data?.MatchIds.Count(id =>
            data.HeadToHead.First(h => h.Name == h2hPlayer1!).PointsByMatch.GetValueOrDefault(id) ==
            data.HeadToHead.First(h => h.Name == h2hPlayer2!).PointsByMatch.GetValueOrDefault(id)) ?? 0;
}
