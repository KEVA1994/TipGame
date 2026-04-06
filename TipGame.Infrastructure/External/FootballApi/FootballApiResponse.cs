using System.Text.Json.Serialization;

public class FootballApiResponse
{
    public List<ApiMatch> Matches { get; set; }
}

public class ApiMatch
{
    public int Id { get; set; }
    public string Status { get; set; }
    public DateTime UtcDate { get; set; }

    public Team HomeTeam { get; set; }
    public Team AwayTeam { get; set; }

    public Score Score { get; set; }
}

public class Team
{
    public string Name { get; set; }
}

public class Score
{
    public FullTime FullTime { get; set; }
}

public class FullTime
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}