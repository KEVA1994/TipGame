using TipGame.Domain.Entities;
using TipGame.Shared.Models;

public class PredictionService
{
    private readonly Supabase.Client _supabase;

    public PredictionService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task SaveTip(string playerName, int matchId, int home, int away)
    {
        // Find or create user by name
        var userResponse = await _supabase.From<User>()
            .Where(u => u.Name == playerName)
            .Get();

        var user = userResponse.Models.FirstOrDefault();

        if (user == null)
        {
            user = new User { Name = playerName };
            var insertResponse = await _supabase.From<User>().Insert(user);
            user = insertResponse.Models.First();
        }

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

    public async Task<List<PredictionDto>> GetPredictions(string playerName)
    {
        var userResponse = await _supabase.From<User>()
            .Where(u => u.Name == playerName)
            .Get();

        var user = userResponse.Models.FirstOrDefault();
        if (user == null)
            return new List<PredictionDto>();

        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == user.Id)
            .Get();

        return predResponse.Models.Select(p => new PredictionDto
        {
            MatchId = p.MatchId,
            HomeScore = p.PredictedHome,
            AwayScore = p.PredictedAway
        }).ToList();
    }
}