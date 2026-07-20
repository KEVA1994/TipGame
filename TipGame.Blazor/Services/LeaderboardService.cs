using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class LeaderboardService
{
    // Same short cache as StatsService: instant back-navigation, refreshed
    // on the same ~1 minute cadence as the point-settling cron.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly Supabase.Client _supabase;
    private readonly Dictionary<int, (List<LeaderboardDto> Data, DateTime At)> _cache = new();

    public LeaderboardService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    /// <summary>
    /// Competition-scoped data used by both the leaderboard and player detail:
    /// the competition's members, its matches, and only the predictions that
    /// belong to those matches.
    /// </summary>
    private async Task<(List<User> Users, List<Match> Matches, List<Prediction> Predictions)>
        LoadCompetitionDataAsync(int competitionId)
    {
        var membersTask = _supabase.From<CompetitionMember>()
            .Where(m => m.CompetitionId == competitionId)
            .Get();
        var usersTask = _supabase.From<User>().Get();
        var predictionsTask = _supabase.GetAllAsync<Prediction>();
        var matchesTask = _supabase.From<Match>()
            .Where(m => m.CompetitionId == competitionId)
            .Get();
        await Task.WhenAll(membersTask, usersTask, predictionsTask, matchesTask);

        var memberIds = membersTask.Result.Models.Select(m => m.UserId).ToHashSet();
        var users = usersTask.Result.Models.Where(u => memberIds.Contains(u.Id)).ToList();
        var matches = matchesTask.Result.Models;
        var matchIds = matches.Select(m => m.Id).ToHashSet();
        var predictions = predictionsTask.Result
            .Where(p => matchIds.Contains(p.MatchId) && memberIds.Contains(p.UserId))
            .ToList();

        return (users, matches, predictions);
    }

    public async Task<List<LeaderboardDto>> GetLeaderboard(int competitionId)
    {
        if (_cache.TryGetValue(competitionId, out var cached)
            && DateTime.UtcNow - cached.At < CacheTtl)
            return cached.Data;

        var (users, allMatches, predictions) = await LoadCompetitionDataAsync(competitionId);
        var matches = allMatches.Where(m => m.Status == "FINISHED").ToList();

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
                    TotalPoints = scoredPreds.Sum(p => p.Points),
                    MatchesPlayed = matchesPlayed,
                    ExactHits = exactHits,
                    CorrectOutcomes = correctOutcomes,
                    CurrentStreak = streak,
                    AvgPoints = matchesPlayed > 0 ? Math.Round(scoredPreds.Sum(p => p.Points) / (double)matchesPlayed, 2) : 0,
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

        _cache[competitionId] = (leaderboard, DateTime.UtcNow);
        return leaderboard;
    }

    /// <summary>
    /// A single player's scoring history within a competition: every FINISHED
    /// match where they earned points, newest first, with the tip they made
    /// and the points it gave — plus the locked matches they forgot to tip on.
    /// Returns null when no player with that name exists in the competition.
    /// </summary>
    public async Task<PlayerDetailDto?> GetPlayerDetail(int competitionId, string userName)
    {
        // All matches, not just FINISHED — missed tips include locked matches
        // that haven't been played yet.
        var (users, allMatches, predictions) = await LoadCompetitionDataAsync(competitionId);

        var user = users.FirstOrDefault(u => u.Name == userName);
        if (user is null) return null;

        var matchLookup = allMatches
            .Where(m => m.Status == "FINISHED")
            .ToDictionary(m => m.Id);

        var tippedMatchIds = predictions
            .Where(p => p.UserId == user.Id)
            .Select(p => p.MatchId)
            .ToHashSet();

        // Forgotten tips: the deadline passed without a tip. Postponed and
        // cancelled matches don't count — there was nothing to tip on.
        var missedMatches = allMatches
            .Where(m => m.Status is not ("POSTPONED" or "CANCELLED")
                && DateTime.UtcNow >= m.KickoffTime.AddHours(-1)
                && !tippedMatchIds.Contains(m.Id))
            .OrderByDescending(m => m.KickoffTime)
            .Select(m => new PlayerMissedMatchDto
            {
                MatchId = m.Id,
                HomeTeam = m.HomeTeam,
                AwayTeam = m.AwayTeam,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                KickoffTime = m.KickoffTime
            })
            .ToList();

        var matches = predictions
            .Where(p => p.UserId == user.Id && p.Points > 0 && matchLookup.ContainsKey(p.MatchId))
            .Select(p =>
            {
                var m = matchLookup[p.MatchId];
                return new PlayerMatchPointDto
                {
                    MatchId = m.Id,
                    HomeTeam = m.HomeTeam,
                    AwayTeam = m.AwayTeam,
                    HomeCrest = m.HomeCrest,
                    AwayCrest = m.AwayCrest,
                    HomeScore = m.HomeScore,
                    AwayScore = m.AwayScore,
                    KickoffTime = m.KickoffTime,
                    PredictedHome = p.PredictedHome,
                    PredictedAway = p.PredictedAway,
                    Points = p.Points
                };
            })
            .OrderByDescending(m => m.KickoffTime)
            .ToList();

        return new PlayerDetailDto
        {
            UserName = user.Name,
            TotalPoints = matches.Sum(m => m.Points),
            Matches = matches,
            MissedMatches = missedMatches
        };
    }
}
