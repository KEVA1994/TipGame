using Microsoft.AspNetCore.Components;

namespace TipGame.Blazor.Components.Stats;

public partial class HeadToHeadChart
{
    [Parameter, EditorRequired] public List<HeadToHeadPlayer> HeadToHead { get; set; } = [];
    [Parameter, EditorRequired] public List<int> MatchIds { get; set; } = [];
    [Parameter, EditorRequired] public List<string> MatchLabels { get; set; } = [];
    [Parameter, EditorRequired] public List<string> PlayerNames { get; set; } = [];
    [Parameter] public string? Player1 { get; set; }
    [Parameter] public string? Player2 { get; set; }

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
