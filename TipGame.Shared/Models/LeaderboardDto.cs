namespace TipGame.Shared.Models;

public class LeaderboardDto
{
    public string UserName { get; set; } = "";
    public int TotalPoints { get; set; }
    public int Change { get; set; }
    public int MatchesPlayed { get; set; }
    public int ExactHits { get; set; }
    public int CorrectOutcomes { get; set; }
    public int CurrentStreak { get; set; }
    public double AvgPoints { get; set; }
    public Dictionary<string, int> DailyPoints { get; set; } = new();
}
