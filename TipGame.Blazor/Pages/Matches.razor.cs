using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private Dictionary<int, TipState> tipStates = new();
    private List<MatchDto> matches = new();
    private List<MatchGroup> matchGroups = new();
    private Dictionary<string, bool> expandedStates = new();
    private bool isLoading = true;
    private string? errorMessage;
    private DotNetObjectReference<Matches>? _dotNetRef;
    private int activeTab;
    private int liveCount;

    protected override void OnInitialized()
    {
        PlayerState.OnChange += HandlePlayerChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await PlayerState.InitializeAsync();

        var matchesTask = MatchService.GetMatches();

        Task<List<PredictionDto>>? tipsTask = null;
        if (!string.IsNullOrEmpty(PlayerState.AuthId))
        {
            tipsTask = PredictionService.GetPredictions(PlayerState.AuthId);
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
        liveCount = matches.Count(m => m.Status is "IN_PLAY" or "PAUSED");

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

        liveCount = matches.Count(m => m.Status is "IN_PLAY" or "PAUSED");
        await InvokeAsync(StateHasChanged);
    }

    private void OnTipSaved(PredictionDto tip)
    {
        tips[tip.MatchId] = tip;
    }

    private bool CanEdit(MatchDto match) =>
        !string.IsNullOrEmpty(PlayerState.AuthId) && DateTime.Now < match.KickoffTime.AddHours(-1);

    private TipState GetTipState(int matchId)
    {
        if (!tipStates.TryGetValue(matchId, out var state))
        {
            state = new TipState();
            tipStates[matchId] = state;
        }
        return state;
    }

    private async Task SaveTip(int matchId, TipState state)
    {
        if (string.IsNullOrEmpty(PlayerState.AuthId) || state.Home is null || state.Away is null) return;
        await PredictionService.SaveTip(PlayerState.AuthId, matchId, state.Home.Value, state.Away.Value);
        var dto = new PredictionDto { MatchId = matchId, HomeScore = state.Home.Value, AwayScore = state.Away.Value };
        tips[matchId] = dto;
    }

    private async Task DeleteTip(int matchId)
    {
        if (string.IsNullOrEmpty(PlayerState.AuthId)) return;
        try
        {
            await PredictionService.DeleteTip(PlayerState.AuthId, matchId);
        }
        catch { }
        tips.Remove(matchId);
        tipStates.Remove(matchId);
        StateHasChanged();
    }

    private async Task TryAutoSaveTip(int matchId, TipState state)
    {
        if (state.Home is null || state.Away is null) return;
        if (state.IsSaving) return;

        // Avoid re-saving the same value
        var existing = tips.GetValueOrDefault(matchId);
        if (existing is not null && existing.HomeScore == state.Home && existing.AwayScore == state.Away)
        {
            return;
        }

        state.IsSaving = true;
        try
        {
            await SaveTip(matchId, state);
        }
        finally
        {
            state.IsSaving = false;
        }
    }

    private async Task HandleTipKeyDown(KeyboardEventArgs e, int matchId, TipState state, bool isHome)
    {
        if (e.Key != "Enter") return;

        if (isHome)
        {
            // Auto-fill empty home with 0
            if (state.Home is null)
            {
                state.Home = 0;
                await TryAutoSaveTip(matchId, state);
            }
            try { await JS.InvokeVoidAsync("tipInputs.focusById", $"tip-away-{matchId}"); } catch { }
        }
        else
        {
            // Auto-fill empty away with 0 (this is the only case where Enter itself triggers a save)
            if (state.Away is null)
            {
                state.Away = 0;
                await TryAutoSaveTip(matchId, state);
            }
            else
            {
                // If a save is still in flight from the last keystroke, wait briefly so we don't double-fire
                if (state.IsSaving)
                {
                    var waited = 0;
                    while (state.IsSaving && waited < 1500)
                    {
                        await Task.Delay(50);
                        waited += 50;
                    }
                }
                // Ensure persisted (no-op if already saved with same values)
                await TryAutoSaveTip(matchId, state);
            }

            try { await JS.InvokeVoidAsync("tipInputs.blurById", $"tip-away-{matchId}"); } catch { }

            // Jump to the next match's home field (tab-like across matches).
            await Task.Yield();
            await InvokeAsync(StateHasChanged);
            await Task.Delay(50);
            try { await JS.InvokeVoidAsync("tipInputs.focusNextHome", matchId); } catch { }
        }
    }

    private void HandlePlayerChanged() => _ = OnPlayerChangedAsync();

    private async Task OnPlayerChangedAsync()
    {
        if (!string.IsNullOrEmpty(PlayerState.AuthId))
        {
            try
            {
                var tipList = await PredictionService.GetPredictions(PlayerState.AuthId);
                tips = tipList.ToDictionary(t => t.MatchId);
            }
            catch { }
        }
        else
        {
            tips = new();
        }

        tipStates = new();
        await InvokeAsync(StateHasChanged);
    }

    private void BuildGroupedMatches()
    {
        matchGroups = matches
            .GroupBy(m => m.Group is not null
                ? $"RUNDE_{m.Matchday ?? 0}"
                : m.Stage ?? "Øvrige")
            .Select(g => new MatchGroup
            {
                GroupKey = g.Key,
                Rounds =
                [
                    new MatchRound
                    {
                        Matchday = 0,
                        Matches = g.OrderBy(m => m.KickoffTime).ToList()
                    }
                ]
            })
            .OrderBy(g => g.Rounds[0].Matches[0].KickoffTime)
            .ToList();

        // Default: expand groups that have live or today's matches
        expandedStates = matchGroups.ToDictionary(
            g => g.GroupKey,
            g => g.Rounds.Any(r => r.Matches.Any(m =>
                m.Status is "IN_PLAY" or "PAUSED" ||
                m.KickoffTime.Date == DateTime.Now.Date)));

        // If nothing is expanded, expand the first group with upcoming matches
        if (!expandedStates.Values.Any(v => v))
        {
            var firstUpcoming = matchGroups.FirstOrDefault(g =>
                g.Rounds.Any(r => r.Matches.Any(m => m.Status != "FINISHED")));
            if (firstUpcoming is not null)
                expandedStates[firstUpcoming.GroupKey] = true;
        }
    }

    private List<MatchGroup> FilteredGroups => activeTab switch
    {
        1 => FilterGroups(m => m.KickoffTime.Date == DateTime.Now.Date),  // I dag
        2 => FilterGroups(m => m.Status is "TIMED" or "SCHEDULED"),       // Kommende
        3 => FilterGroups(m => m.Status == "FINISHED"),                    // Afsluttede
        4 => FilterGroups(m => m.Status is "IN_PLAY" or "PAUSED"),        // Live
        _ => matchGroups                                                   // Alle
    };

    private void OnTabChanged(int index)
    {
        activeTab = index;

        // "I dag" tab: expand all groups so every today-match is visible
        if (activeTab == 1)
        {
            foreach (var key in expandedStates.Keys)
                expandedStates[key] = true;
        }
    }

    private List<MatchGroup> FilterGroups(Func<MatchDto, bool> predicate) =>
        matchGroups
            .Select(g => new MatchGroup
            {
                GroupKey = g.GroupKey,
                Rounds = g.Rounds
                    .Select(r => new MatchRound
                    {
                        Matchday = r.Matchday,
                        Matches = r.Matches.Where(predicate).ToList()
                    })
                    .Where(r => r.Matches.Count > 0)
                    .ToList()
            })
            .Where(g => g.Rounds.Count > 0)
            .ToList();

    private static int CalculatePoints(MatchDto match, PredictionDto tip)
    {
        if (match.HomeScore is null || match.AwayScore is null)
            return 0;
        if (tip.HomeScore == match.HomeScore && tip.AwayScore == match.AwayScore)
            return 3; // Exact
        var actualDiff = match.HomeScore - match.AwayScore;
        var tipDiff = tip.HomeScore - tip.AwayScore;
        if (actualDiff > 0 && tipDiff > 0 || actualDiff < 0 && tipDiff < 0 || actualDiff == 0 && tipDiff == 0)
            return 1; // Correct outcome
        return 0;
    }

    private static string FormatDate(DateTime date)
    {
        var day = date.DayOfWeek switch
        {
            DayOfWeek.Monday => "mandag",
            DayOfWeek.Tuesday => "tirsdag",
            DayOfWeek.Wednesday => "onsdag",
            DayOfWeek.Thursday => "torsdag",
            DayOfWeek.Friday => "fredag",
            DayOfWeek.Saturday => "lørdag",
            DayOfWeek.Sunday => "søndag",
            _ => ""
        };
        return $"{day} {date:dd-MM-yyyy}";
    }

    private static string FormatGroupName(string key) => key switch
    {
        "RUNDE_1" => "Runde 1",
        "RUNDE_2" => "Runde 2",
        "RUNDE_3" => "Runde 3",
        "RUNDE_0" => "Gruppespil",
        "GROUP_TEST" => "🧪 Test Gruppe",
        "LAST_32" => "1/16-finale",
        "LAST_16" => "1/8-finale",
        "QUARTER_FINALS" => "Kvartfinale",
        "SEMI_FINALS" => "Semifinale",
        "THIRD_PLACE" => "Bronzekamp",
        "FINAL" => "Finale",
        _ => key
    };

    private record MatchGroup
    {
        public string GroupKey { get; init; } = "";
        public List<MatchRound> Rounds { get; init; } = [];
    }

    private record MatchRound
    {
        public int Matchday { get; init; }
        public List<MatchDto> Matches { get; init; } = [];
    }

    private class TipState
    {
        public int? Home { get; set; }
        public int? Away { get; set; }
        public bool IsSaving { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        PlayerState.OnChange -= HandlePlayerChanged;
        try { await JS.InvokeVoidAsync("supabaseRealtime.unsubscribe"); } catch { }
        _dotNetRef?.Dispose();
    }
}
