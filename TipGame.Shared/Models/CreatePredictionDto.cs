namespace TipGame.Shared.Models;

public class CreatePredictionDto
{
    public int MatchId { get; set; }

    public string Name { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
