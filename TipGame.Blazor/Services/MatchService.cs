using TipGame.Domain.Entities;
using TipGame.Shared.Models;
using Supabase.Postgrest;

public class MatchService
{
    private readonly Supabase.Client _supabase;

    public MatchService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<MatchDto>> GetMatches()
    {
        var response = await _supabase.From<Match>()
            .Order("KickoffTime", Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => new MatchDto
        {
            Id = m.Id,
            HomeTeam = m.HomeTeam,
            AwayTeam = m.AwayTeam,
            HomeCrest = m.HomeCrest,
            AwayCrest = m.AwayCrest,
            KickoffTime = DateTime.SpecifyKind(m.KickoffTime, DateTimeKind.Utc),
            Status = m.Status,
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,
            Minute = m.Minute
        }).ToList();
    }
}