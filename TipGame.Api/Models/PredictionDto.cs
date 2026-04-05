namespace TipGame.Api.Models;

public class PredictionDto
{
    public int Id { get; set; }

    public string UserName { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public int Points { get; set; }
}