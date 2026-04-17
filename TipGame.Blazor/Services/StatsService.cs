using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class StatsService
{
    private readonly Supabase.Client _supabase;

    public StatsService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<StatsData> GetStatsAsync()
    {
        var users = (await _supabase.From<User>().Get()).Models;
        var predictions = (await _supabase.From<Prediction>().Get()).Models;
        var matches = (await _supabase.From<Match>()
            .Where(m => m.Status == "FINISHED")
            .Get()).Models;

        var matchLookup = matches.ToDictionary(m => m.Id);
        var userLookup = users.ToDictionary(u => u.Id, u => u.Name);

        // 1. Point-udvikling over tid (kumulative point per matchday per spiller)
        var allMatchdays = matches
            .Where(m => m.Matchday.HasValue)
            .Select(m => m.Matchday!.Value)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var matchdayLabels = allMatchdays.Select(d => $"Runde {d}").ToList();

        var cumulativeData = users.Select(u =>
        {
            var userPreds = predictions.Where(p => p.UserId == u.Id && matchLookup.ContainsKey(p.MatchId));
            var pointsByMatchday = userPreds
                .Where(p => matchLookup[p.MatchId].Matchday.HasValue)
                .GroupBy(p => matchLookup[p.MatchId].Matchday!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Points));

            var cumulative = new List<double>();
            var running = 0.0;
            foreach (var md in allMatchdays)
            {
                running += pointsByMatchday.GetValueOrDefault(md);
                cumulative.Add(running);
            }

            return new PlayerSeries { Name = u.Name, Values = cumulative.ToArray() };
        }).ToList();

        // 2. Treffsikkerhed per spiller
        var accuracy = users.Select(u =>
        {
            var userPreds = predictions.Where(p => p.UserId == u.Id && matchLookup.ContainsKey(p.MatchId)).ToList();
            return new AccuracyData
            {
                Name = u.Name,
                Exact = userPreds.Count(p => p.Points == 3),
                CorrectOutcome = userPreds.Count(p => p.Points == 1),
                Wrong = userPreds.Count(p => p.Points == 0)
            };
        }).ToList();

        // 3. Mest tippede resultater
        var popularTips = predictions
            .Where(p => matchLookup.ContainsKey(p.MatchId))
            .GroupBy(p => $"{p.PredictedHome}-{p.PredictedAway}")
            .Select(g => new PopularTip { Score = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        // 4. Head-to-head data
        var headToHead = users.Select(u =>
        {
            var userPreds = predictions
                .Where(p => p.UserId == u.Id && matchLookup.ContainsKey(p.MatchId))
                .ToDictionary(p => p.MatchId, p => p.Points);
            return new HeadToHeadPlayer { Name = u.Name, PointsByMatch = userPreds };
        }).ToList();

        var matchIds = matches.OrderBy(m => m.KickoffTime).Select(m => m.Id).ToList();
        var matchLabels = matches.OrderBy(m => m.KickoffTime)
            .Select(m => $"{m.HomeTeam[..3].ToUpper()} - {m.AwayTeam[..3].ToUpper()}")
            .ToList();

        return new StatsData
        {
            Dates = matchdayLabels,
            CumulativePoints = cumulativeData,
            Accuracy = accuracy,
            PopularTips = popularTips,
            HeadToHead = headToHead,
            MatchIds = matchIds,
            MatchLabels = matchLabels,
            PlayerNames = users.Select(u => u.Name).OrderBy(n => n).ToList()
        };
    }
}

public class StatsData
{
    public List<string> Dates { get; set; } = [];
    public List<PlayerSeries> CumulativePoints { get; set; } = [];
    public List<AccuracyData> Accuracy { get; set; } = [];
    public List<PopularTip> PopularTips { get; set; } = [];
    public List<HeadToHeadPlayer> HeadToHead { get; set; } = [];
    public List<int> MatchIds { get; set; } = [];
    public List<string> MatchLabels { get; set; } = [];
    public List<string> PlayerNames { get; set; } = [];
}

public class PlayerSeries
{
    public string Name { get; set; } = "";
    public double[] Values { get; set; } = [];
}

public class AccuracyData
{
    public string Name { get; set; } = "";
    public int Exact { get; set; }
    public int CorrectOutcome { get; set; }
    public int Wrong { get; set; }
}

public class PopularTip
{
    public string Score { get; set; } = "";
    public int Count { get; set; }
}

public class HeadToHeadPlayer
{
    public string Name { get; set; } = "";
    public Dictionary<int, int> PointsByMatch { get; set; } = new();
}
