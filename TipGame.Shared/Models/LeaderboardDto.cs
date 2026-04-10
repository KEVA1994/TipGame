namespace TipGame.Shared.Models;

public class LeaderboardDto
{
    public string UserName { get; set; }
    public int TotalPoints { get; set; }
    public int Change { get; set; }
    public Dictionary<string, int> DailyPoints { get; set; } = new();
}
