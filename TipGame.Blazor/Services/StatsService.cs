using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class StatsService
{
    // Stats only move when the sync cron settles points (~every minute),
    // so a short cache makes back-navigation instant without showing stale data for long.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly Supabase.Client _supabase;
    private StatsData? _cached;
    private DateTime _cachedAt;

    public StatsService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<StatsData> GetStatsAsync()
    {
        if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheTtl)
            return _cached;
        // Fetch in parallel — sequential awaits tripled the load time.
        var usersTask = _supabase.From<User>().Get();
        var predictionsTask = _supabase.GetAllAsync<Prediction>();
        var matchesTask = _supabase.From<Match>()
            .Where(m => m.Status == "FINISHED")
            .Get();
        await Task.WhenAll(usersTask, predictionsTask, matchesTask);

        var users = usersTask.Result.Models;
        var predictions = predictionsTask.Result;
        var matches = matchesTask.Result.Models;

        Console.WriteLine($"[Stats] Users={users.Count}, Predictions={predictions.Count}, FinishedMatches={matches.Count}");

        _cached = BuildStats(users, predictions, matches);
        _cachedAt = DateTime.UtcNow;
        return _cached;
    }

    private static StatsData BuildStats(List<User> users, List<Prediction> predictions, List<Match> matches)
    {

        var matchLookup = matches.ToDictionary(m => m.Id);
        var userLookup = users.ToDictionary(u => u.Id, u => u.Name);

        // Ordered matches for progression chart (group by match date)
        var orderedMatches = matches.OrderBy(m => m.KickoffTime).ToList();
        var matchDays = orderedMatches
            .GroupBy(m => m.KickoffTime.Date)
            .OrderBy(g => g.Key)
            .ToList();

        // 1. Point-udvikling over tid (kumulative point per kampdag per spiller)
        var matchdayLabels = matchDays
            .Select(g => g.Key.ToString("dd/MM"))
            .ToList();

        var cumulativeData = users.Select(u =>
        {
            var userPreds = predictions
                .Where(p => p.UserId == u.Id && matchLookup.ContainsKey(p.MatchId))
                .ToDictionary(p => p.MatchId, p => p.Points);

            var cumulative = new List<double>();
            var running = 0.0;
            foreach (var day in matchDays)
            {
                running += day.Sum(m => userPreds.GetValueOrDefault(m.Id));
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
                CorrectDraw = userPreds.Count(p => p.Points == 2),
                CorrectOutcome = userPreds.Count(p => p.Points == 1),
                Wrong = userPreds.Count(p => p.Points == 0)
            };
        }).OrderByDescending(a => a.Exact).ThenByDescending(a => a.CorrectDraw).ToList();

        // 3. Mest tippede resultater (spejlvendte resultater som 2-0 og 0-2 slås sammen)
        var popularTips = predictions
            .Where(p => matchLookup.ContainsKey(p.MatchId))
            .GroupBy(p =>
            {
                var hi = p.PredictedHome >= p.PredictedAway ? p.PredictedHome : p.PredictedAway;
                var lo = p.PredictedHome >= p.PredictedAway ? p.PredictedAway : p.PredictedHome;
                return $"{hi}-{lo}";
            })
            .Select(g =>
            {
                var parts = g.Key.Split('-');
                var hi = int.Parse(parts[0]);
                var lo = int.Parse(parts[1]);
                var isDraw = hi == lo;
                var score = isDraw ? $"{hi}-{lo}" : $"{hi}-{lo} / {lo}-{hi}";

                // De faktiske resultater der tæller som ramt for denne (eventuelt spejlvendte) scoring
                var scorelines = isDraw
                    ? new[] { (Home: hi, Away: lo) }
                    : new[] { (Home: hi, Away: lo), (Home: lo, Away: hi) };

                var hitMatchList = matches
                    .Where(m => scorelines.Any(s => m.HomeScore == s.Home && m.AwayScore == s.Away))
                    .Select(m =>
                    {
                        var players = predictions
                            .Where(p => p.MatchId == m.Id && p.PredictedHome == m.HomeScore && p.PredictedAway == m.AwayScore)
                            .Select(p => userLookup.GetValueOrDefault(p.UserId, "?"))
                            .ToList();
                        return new HitMatchInfo
                        {
                            MatchLabel = $"{m.HomeTeam} {m.HomeScore}-{m.AwayScore} {m.AwayTeam}",
                            Players = players
                        };
                    })
                    .ToList();

                var tippedMatches = g
                    .GroupBy(p => new { p.MatchId, p.PredictedHome, p.PredictedAway })
                    .Where(mg => matchLookup.ContainsKey(mg.Key.MatchId))
                    .Select(mg =>
                    {
                        var match = matchLookup[mg.Key.MatchId];
                        return new TipMatchInfo
                        {
                            MatchLabel = $"{match.HomeTeam} {match.HomeScore}-{match.AwayScore} {match.AwayTeam}",
                            PredictedScore = $"{mg.Key.PredictedHome}-{mg.Key.PredictedAway}",
                            Players = mg.Select(p => userLookup.GetValueOrDefault(p.UserId, "?")).ToList(),
                            WasCorrect = match.HomeScore == mg.Key.PredictedHome && match.AwayScore == mg.Key.PredictedAway
                        };
                    })
                    .OrderByDescending(t => t.WasCorrect)
                    .ThenByDescending(t => t.Players.Count)
                    .ToList();

                return new PopularTip
                {
                    Score = score,
                    Count = g.Count(),
                    ExactHits = hitMatchList.Count,
                    HitMatches = hitMatchList,
                    TippedMatches = tippedMatches
                };
            })
            .OrderByDescending(x => x.Count)
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
    public int CorrectDraw { get; set; }
    public int CorrectOutcome { get; set; }
    public int Wrong { get; set; }
}

public class PopularTip
{
    public string Score { get; set; } = "";
    public int Count { get; set; }
    public int ExactHits { get; set; }
    public int TotalPlayerHits => HitMatches.Sum(m => m.Players.Count);
    public List<HitMatchInfo> HitMatches { get; set; } = [];
    public List<TipMatchInfo> TippedMatches { get; set; } = [];
}

public class HitMatchInfo
{
    public string MatchLabel { get; set; } = "";
    public List<string> Players { get; set; } = [];
}

public class TipMatchInfo
{
    public string MatchLabel { get; set; } = "";
    public string PredictedScore { get; set; } = "";
    public List<string> Players { get; set; } = [];
    public bool WasCorrect { get; set; }
}

public class HeadToHeadPlayer
{
    public string Name { get; set; } = "";
    public Dictionary<int, int> PointsByMatch { get; set; } = new();
}
