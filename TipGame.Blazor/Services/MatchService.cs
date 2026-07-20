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

    public async Task<List<MatchDto>> GetMatches(int competitionId)
    {
        var response = await _supabase.From<Match>()
            .Where(m => m.CompetitionId == competitionId)
            .Order("KickoffTime", Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(ToDto).ToList();
    }

    public async Task<MatchDto?> GetMatch(int id)
    {
        var response = await _supabase.From<Match>()
            .Where(m => m.Id == id)
            .Get();

        var match = response.Models.FirstOrDefault();
        return match is null ? null : ToDto(match);
    }

    private static MatchDto ToDto(Match m) => new()
    {
        Id = m.Id,
        HomeTeam = m.HomeTeam,
        AwayTeam = m.AwayTeam,
        HomeCrest = m.HomeCrest,
        AwayCrest = m.AwayCrest,
        KickoffTime = m.KickoffTime,
        Status = m.Status,
        HomeScore = m.HomeScore,
        AwayScore = m.AwayScore,
        Minute = m.Minute,
        Group = m.Group,
        Stage = m.Stage,
        Matchday = m.Matchday
    };
}
