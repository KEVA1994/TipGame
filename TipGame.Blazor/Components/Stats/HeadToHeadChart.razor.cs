using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TipGame.Blazor.Components.Stats;

public partial class HeadToHeadChart
{
    private const string StorageKey = "stats.h2h.players";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public List<HeadToHeadPlayer> HeadToHead { get; set; } = [];
    [Parameter, EditorRequired] public List<int> MatchIds { get; set; } = [];
    [Parameter, EditorRequired] public List<string> MatchLabels { get; set; } = [];
    [Parameter, EditorRequired] public List<string> PlayerNames { get; set; } = [];

    private string? Player1 { get; set; }
    private string? Player2 { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Restore the last matchup from the browser.
        try
        {
            var stored = await JS.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(stored)) return;

            var names = System.Text.Json.JsonSerializer.Deserialize<string?[]>(stored);
            if (names is not { Length: 2 }) return;

            Player1 = PlayerNames.Contains(names[0]!) ? names[0] : null;
            Player2 = PlayerNames.Contains(names[1]!) && names[1] != Player1 ? names[1] : null;
            StateHasChanged();
        }
        catch
        {
            // Corrupt/unavailable storage — start with no players selected.
        }
    }

    private void SetPlayer1(string? name)
    {
        Player1 = name;
        _ = PersistSelectionAsync();
    }

    private void SetPlayer2(string? name)
    {
        Player2 = name;
        _ = PersistSelectionAsync();
    }

    private async Task PersistSelectionAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", StorageKey,
                System.Text.Json.JsonSerializer.Serialize(new[] { Player1, Player2 }));
        }
        catch
        {
            // Storage unavailable — selection just won't survive a refresh.
        }
    }

    private Task<IEnumerable<string>> SearchPlayer1(string value, CancellationToken ct) =>
        SearchPlayers(value, exclude: Player2);

    private Task<IEnumerable<string>> SearchPlayer2(string value, CancellationToken ct) =>
        SearchPlayers(value, exclude: Player1);

    private Task<IEnumerable<string>> SearchPlayers(string value, string? exclude)
    {
        var candidates = PlayerNames.Where(n => n != exclude);
        if (!string.IsNullOrWhiteSpace(value))
            candidates = candidates.Where(n => n.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(candidates);
    }

    private int GetTotal(string name) =>
        MatchIds.Sum(id => HeadToHead.First(h => h.Name == name).PointsByMatch.GetValueOrDefault(id));

    private int GetWins(string name, string opponent) =>
        MatchIds.Count(id =>
            HeadToHead.First(h => h.Name == name).PointsByMatch.GetValueOrDefault(id) >
            HeadToHead.First(h => h.Name == opponent).PointsByMatch.GetValueOrDefault(id));

    private int GetDraws() =>
        MatchIds.Count(id =>
            HeadToHead.First(h => h.Name == Player1!).PointsByMatch.GetValueOrDefault(id) ==
            HeadToHead.First(h => h.Name == Player2!).PointsByMatch.GetValueOrDefault(id));

    private List<MatchResult> GetMatchResults()
    {
        var p1 = HeadToHead.FirstOrDefault(h => h.Name == Player1);
        var p2 = HeadToHead.FirstOrDefault(h => h.Name == Player2);
        if (p1 is null || p2 is null) return [];

        return MatchIds.Select((id, i) => new MatchResult
        {
            Label = MatchLabels[i],
            P1Points = p1.PointsByMatch.GetValueOrDefault(id),
            P2Points = p2.PointsByMatch.GetValueOrDefault(id)
        }).ToList();
    }

    private sealed class MatchResult
    {
        public string Label { get; init; } = "";
        public int P1Points { get; init; }
        public int P2Points { get; init; }
    }
}
