namespace TipGame.Shared.Models;

public class MatchDto
{
    public int Id { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public string? HomeCrest { get; set; }
    public string? AwayCrest { get; set; }
    public DateTime KickoffTime { get; set; }
    public string Status { get; set; }

    /// <summary>
    /// True once tipping is closed for this match — the same deadline that
    /// blocks saving/deleting tips (one hour before kickoff).
    /// </summary>
    public bool IsLocked => DateTime.UtcNow >= KickoffTime.AddHours(-1);

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public int? Minute { get; set; }
    public string? Group { get; set; }
    public string? Stage { get; set; }
    public int? Matchday { get; set; }
}
