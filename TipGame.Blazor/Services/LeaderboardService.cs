using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class LeaderboardService
{
    private readonly Supabase.Client _supabase;

    public LeaderboardService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<LeaderboardDto>> GetLeaderboard()
    {
        var users = (await _supabase.From<User>().Get()).Models;
        var predictions = (await _supabase.From<Prediction>().Get()).Models;
        var matches = (await _supabase.From<Match>()
            .Where(m => m.Status == "FINISHED")
            .Get()).Models;

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

        return leaderboard;
    }
}