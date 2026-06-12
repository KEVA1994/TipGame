using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class LeaderboardService
{
    // Same short cache as StatsService: instant back-navigation, refreshed
    // on the same ~1 minute cadence as the point-settling cron.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly Supabase.Client _supabase;
    private List<LeaderboardDto>? _cached;
    private DateTime _cachedAt;

    public LeaderboardService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<LeaderboardDto>> GetLeaderboard()
    {
        if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheTtl)
            return _cached;
        // Parallel fetch + paged predictions (Supabase caps a request at 1000 rows).
        var usersTask = _supabase.From<User>().Get();
        var predictionsTask = _supabase.GetAllAsync<Prediction>();
        var matchesTask = _supabase.From<Match>()
            .Where(m => m.Status == "FINISHED")
            .Get();
        await Task.WhenAll(usersTask, predictionsTask, matchesTask);

        var users = usersTask.Result.Models;
        var predictions = predictionsTask.Result;
        var matches = matchesTask.Result.Models;

        var matchLookup = matches.ToDictionary(m => m.Id);

        var leaderboard = users
            .Select(u =>
            {
                var userPreds = predictions.Where(p => p.UserId == u.Id).ToList();
                var scoredPreds = userPreds.Where(p => matchLookup.ContainsKey(p.MatchId)).ToList();
                var matchesPlayed = scoredPreds.Count;
                var exactHits = scoredPreds.Count(p => p.Points == 3);
                var correctOutcomes = scoredPreds.Count(p => p.Points >= 1);

                // Current streak: consecutive scored predictions (ordered by match time)
                var orderedPoints = scoredPreds
                    .OrderByDescending(p => matchLookup[p.MatchId].KickoffTime)
                    .Select(p => p.Points)
                    .ToList();
                var streak = 0;
                foreach (var pts in orderedPoints)
                {
                    if (pts > 0) streak++;
                    else break;
                }

                return new LeaderboardDto
                {
                    UserName = u.Name,
                    TotalPoints = userPreds.Sum(p => p.Points),
                    MatchesPlayed = matchesPlayed,
                    ExactHits = exactHits,
                    CorrectOutcomes = correctOutcomes,
                    CurrentStreak = streak,
                    AvgPoints = matchesPlayed > 0 ? Math.Round(userPreds.Sum(p => p.Points) / (double)matchesPlayed, 2) : 0,
                    DailyPoints = scoredPreds
                        .GroupBy(p => matchLookup[p.MatchId].KickoffTime.ToString("dd/MM"))
                        .ToDictionary(g => g.Key, g => g.Sum(p => p.Points))
                };
            })
            .OrderByDescending(x => x.TotalPoints)
            .ToList();

        // Calculate position change based on latest day with data
        var latestDay = leaderboard
            .SelectMany(x => x.DailyPoints.Keys)
            .Distinct()
            .OrderDescending()
            .FirstOrDefault();

        var previousRanking = leaderboard
            .Select(x => new
            {
                x.UserName,
                PreviousPoints = latestDay is not null
                    ? x.TotalPoints - x.DailyPoints.GetValueOrDefault(latestDay)
                    : x.TotalPoints
            })
            .OrderByDescending(x => x.PreviousPoints)
            .Select((x, i) => new { x.UserName, Position = i })
            .ToDictionary(x => x.UserName, x => x.Position);

        for (var i = 0; i < leaderboard.Count; i++)
        {
            var player = leaderboard[i];
            if (previousRanking.TryGetValue(player.UserName, out var prevPos))
            {
                player.Change = prevPos - i;
            }
        }

        _cached = leaderboard;
        _cachedAt = DateTime.UtcNow;
        return leaderboard;
    }
}