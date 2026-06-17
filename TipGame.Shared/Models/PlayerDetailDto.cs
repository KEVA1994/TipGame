namespace TipGame.Shared.Models;

public class PlayerDetailDto
{
    public string UserName { get; set; }

    /// <summary>Sum of points across the matches the player scored on.</summary>
    public int TotalPoints { get; set; }

    /// <summary>Finished matches where the player earned points, newest first.</summary>
    public List<PlayerMatchPointDto> Matches { get; set; } = [];
}

public class PlayerMatchPointDto
{
    public int MatchId { get; set; }

    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public string? HomeCrest { get; set; }
    public string? AwayCrest { get; set; }

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    public DateTime KickoffTime { get; set; }

    public int PredictedHome { get; set; }
    public int PredictedAway { get; set; }

    public int Points { get; set; }
}
