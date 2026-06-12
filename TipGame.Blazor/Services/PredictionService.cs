using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class PredictionService
{
    private readonly Supabase.Client _supabase;

    // AuthId -> Users.Id never changes, so look it up once per session
    // instead of on every save/delete/load.
    private readonly Dictionary<string, int> _userIdCache = new();

    public PredictionService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    private async Task<int?> GetUserIdAsync(string authId)
    {
        if (_userIdCache.TryGetValue(authId, out var cached))
            return cached;

        // User row is created automatically by a database trigger on signup
        var userResponse = await _supabase.From<User>()
            .Where(u => u.AuthId == authId)
            .Get();

        var user = userResponse.Models.FirstOrDefault();
        if (user == null) return null;

        _userIdCache[authId] = user.Id;
        return user.Id;
    }

    public async Task SaveTip(string authId, int matchId, int home, int away)
    {
        // User lookup and deadline check are independent — run them in parallel.
        var userIdTask = GetUserIdAsync(authId);
        var matchTask = _supabase.From<Match>()
            .Where(m => m.Id == matchId)
            .Get();
        await Task.WhenAll(userIdTask, matchTask);

        if (userIdTask.Result is not int userId) return;

        var match = matchTask.Result.Models.FirstOrDefault();
        if (match == null || DateTime.UtcNow >= match.KickoffTime.AddHours(-1))
            return;

        // Upsert prediction
        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == userId)
            .Where(p => p.MatchId == matchId)
            .Get();

        var prediction = predResponse.Models.FirstOrDefault();

        if (prediction != null)
        {
            await _supabase.From<Prediction>()
                .Where(p => p.Id == prediction.Id)
                .Set(p => p.PredictedHome, home)
                .Set(p => p.PredictedAway, away)
                .Update();
        }
        else
        {
            await _supabase.From<Prediction>().Insert(new Prediction
            {
                MatchId = matchId,
                UserId = userId,
                PredictedHome = home,
                PredictedAway = away,
                Points = 0
            });
        }
    }

    public async Task DeleteTip(string authId, int matchId)
    {
        var userIdTask = GetUserIdAsync(authId);
        var matchTask = _supabase.From<Match>()
            .Where(m => m.Id == matchId)
            .Get();
        await Task.WhenAll(userIdTask, matchTask);

        if (userIdTask.Result is not int userId) return;

        // Honor deadline on delete too
        var match = matchTask.Result.Models.FirstOrDefault();
        if (match == null || DateTime.UtcNow >= match.KickoffTime.AddHours(-1))
            return;

        await _supabase.From<Prediction>()
            .Where(p => p.UserId == userId)
            .Where(p => p.MatchId == matchId)
            .Delete();
    }

    public async Task<List<PredictionDto>> GetPredictions(string authId)
    {
        if (await GetUserIdAsync(authId) is not int userId)
            return [];

        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == userId)
            .Get();

        return predResponse.Models.Select(p => new PredictionDto
        {
            MatchId = p.MatchId,
            HomeScore = p.PredictedHome,
            AwayScore = p.PredictedAway,
            Points = p.Points
        }).ToList();
    }
}