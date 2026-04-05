namespace TipGame.Api.Models;

public class MatchDto
{
    public int Id { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public DateTime KickoffTime { get; set; }
    public string Status { get; set; }

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
}