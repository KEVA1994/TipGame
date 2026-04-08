namespace TipGame.Shared.Models;

public class CreateMatchDto
{
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public DateTime KickoffTime { get; set; }
}
