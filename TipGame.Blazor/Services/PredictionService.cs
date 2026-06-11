using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class PredictionService
{
    private readonly Supabase.Client _supabase;

    public PredictionService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task SaveTip(string authId, int matchId, int home, int away)
    {
        // Find user by AuthId (created automatically by database trigger on signup)
        var userResponse = await _supabase.From<User>()
            .Where(u => u.AuthId == authId)
            .Get();

        var user = userResponse.Models.FirstOrDefault();
        if (user == null) return;

        // Check deadline
        var matchResponse = await _supabase.From<Match>()
            .Where(m => m.Id == matchId)
            .Get();

        var match = matchResponse.Models.FirstOrDefault();
        if (match == null || DateTime.UtcNow >= match.KickoffTime.AddHours(-1))
            return;

        // Upsert prediction
        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == user.Id)
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
                UserId = user.Id,
                PredictedHome = home,
                PredictedAway = away,
                Points = 0
            });
        }
    }

    public async Task DeleteTip(string authId, int matchId)
    {
        var userResponse = await _supabase.From<User>()
            .Where(u => u.AuthId == authId)
            .Get();

        var user = userResponse.Models.FirstOrDefault();
        if (user == null) return;

        // Honor deadline on delete too
        var matchResponse = await _supabase.From<Match>()
            .Where(m => m.Id == matchId)
            .Get();

        var match = matchResponse.Models.FirstOrDefault();
        if (match == null || DateTime.UtcNow >= match.KickoffTime.AddHours(-1))
            return;

        await _supabase.From<Prediction>()
            .Where(p => p.UserId == user.Id)
            .Where(p => p.MatchId == matchId)
            .Delete();
    }

    public async Task<List<PredictionDto>> GetPredictions(string authId)
    {
        var userResponse = await _supabase.From<User>()
            .Where(u => u.AuthId == authId)
            .Get();

        var user = userResponse.Models.FirstOrDefault();
        if (user == null)
            return [];

        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == user.Id)
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