namespace TipGame.Shared.Models;

public class CreatePredictionDto
{
    public int MatchId { get; set; }

    public string ClientId { get; set; } // 👈 vigtigt

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
