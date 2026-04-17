using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TipGame.Shared.Models;

namespace TipGame.Blazor.Pages;

public partial class Matches : IAsyncDisposable
{
    [Inject] private MatchService MatchService { get; set; } = default!;
    [Inject] private PredictionService PredictionService { get; set; } = default!;
    [Inject] private PlayerState PlayerState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;

    private Dictionary<int, PredictionDto> tips = new();
    private List<MatchDto> matches = new();
    private List<IGrouping<string, MatchDto>> groupedMatches = new();
    private bool isLoading = true;
    private string? errorMessage;
    private DotNetObjectReference<Matches>? _dotNetRef;

    protected override void OnInitialized()
    {
        PlayerState.OnChange += OnPlayerChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await PlayerState.InitializeAsync();

        var matchesTask = MatchService.GetMatches();

        Task<List<PredictionDto>>? tipsTask = null;
        if (!string.IsNullOrEmpty(PlayerState.PlayerName))
        {
            tipsTask = PredictionService.GetPredictions(PlayerState.PlayerName);
        }

        try
        {
            matches = await matchesTask;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            errorMessage = "Kunne ikke hente kampe.";
        }

        BuildGroupedMatches();

        if (tipsTask is not null)
        {
            try
            {
                var tipList = await tipsTask;
                tips = tipList.ToDictionary(t => t.MatchId);
            }
            catch { }
        }

        isLoading = false;
        StateHasChanged();

        var url = Configuration["Supabase:Url"]!;
        var key = Configuration["Supabase:Key"]!;
        await JS.InvokeVoidAsync("supabaseRealtime.init", url, key);

        _dotNetRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("supabaseRealtime.subscribeMatches", _dotNetRef);
    }

    [JSInvokable]
    public async Task OnMatchChanged(JsonElement payload)
    {
        Console.WriteLine($"[Realtime] Received: {payload}");

        if (!payload.TryGetProperty("Id", out var idEl)) return;
        var id = idEl.GetInt32();
        var existing = matches.FirstOrDefault(m => m.Id == id);
        if (existing is null) return;

        if (payload.TryGetProperty("Status", out var statusEl))
            existing.Status = statusEl.GetString() ?? existing.Status;
        if (payload.TryGetProperty("HomeScore", out var hs))
            existing.HomeScore = hs.ValueKind == JsonValueKind.Null ? null : hs.GetInt32();
        if (payload.TryGetProperty("AwayScore", out var aws))
            existing.AwayScore = aws.ValueKind == JsonValueKind.Null ? null : aws.GetInt32();
        if (payload.TryGetProperty("Minute", out var min))
            existing.Minute = min.ValueKind == JsonValueKind.Null ? null : min.GetInt32();

        await InvokeAsync(StateHasChanged);
    }

    private void OnTipSaved(PredictionDto tip)
    {
        tips[tip.MatchId] = tip;
    }

    private async void OnPlayerChanged()
    {
        if (!string.IsNullOrEmpty(PlayerState.PlayerName))
        {
            try
            {
                var tipList = await PredictionService.GetPredictions(PlayerState.PlayerName);
                tips = tipList.ToDictionary(t => t.MatchId);
            }
            catch { }
        }
        else
        {
            tips = new();
        }

        await InvokeAsync(StateHasChanged);
    }

    private void BuildGroupedMatches()
    {
        groupedMatches = matches
            .GroupBy(m => m.Group ?? m.Stage ?? "Øvrige")
            .OrderBy(g => g.Key == "GROUP_TEST" ? 0 : 1)
            .ThenBy(g => g.Key)
            .ToList();
    }

    private static string FormatGroupName(string key) => key switch
    {
        "GROUP_A" => "Gruppe A",
        "GROUP_B" => "Gruppe B",
        "GROUP_C" => "Gruppe C",
        "GROUP_D" => "Gruppe D",
        "GROUP_E" => "Gruppe E",
        "GROUP_F" => "Gruppe F",
        "GROUP_G" => "Gruppe G",
        "GROUP_H" => "Gruppe H",
        "GROUP_I" => "Gruppe I",
        "GROUP_J" => "Gruppe J",
        "GROUP_K" => "Gruppe K",
        "GROUP_L" => "Gruppe L",
        "GROUP_TEST" => "🧪 Test",
        "LAST_32" => "1/16-finale",
        "LAST_16" => "1/8-finale",
        "QUARTER_FINALS" => "Kvartfinale",
        "SEMI_FINALS" => "Semifinale",
        "THIRD_PLACE" => "Bronzekamp",
        "FINAL" => "Finale",
        _ => key
    };

    public async ValueTask DisposeAsync()
    {
        PlayerState.OnChange -= OnPlayerChanged;
        try { await JS.InvokeVoidAsync("supabaseRealtime.unsubscribe"); } catch { }
        _dotNetRef?.Dispose();
    }
}
