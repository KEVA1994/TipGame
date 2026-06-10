using TipGame.Domain.Entities;
using TipGame.Shared.Models;
using Supabase.Postgrest;
using Microsoft.JSInterop;

public class MatchService
{
    private readonly Supabase.Client _supabase;
    private readonly IJSRuntime _js;

    public MatchService(Supabase.Client supabase, IJSRuntime js)
    {
        _supabase = supabase;
        _js = js;
    }

    public async Task<List<MatchDto>> GetMatches()
    {
        var offsetMinutes = await _js.InvokeAsync<int>("getUtcOffsetMinutes");

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
            KickoffTime = DateTime.SpecifyKind(m.KickoffTime, DateTimeKind.Utc).AddMinutes(offsetMinutes),
            Status = m.Status,
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,
            Minute = m.Minute,
            Group = m.Group,
            Stage = m.Stage,
            Matchday = m.Matchday
        }).ToList();
    }
}