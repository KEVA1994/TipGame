using Microsoft.AspNetCore.Mvc;
using TipGame.Domain.Entities;
using TipGame.Shared.Models;

namespace TipGame.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly Supabase.Client _supabase;

    public LeaderboardController(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    // GET: api/leaderboard
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaderboardDto>>> Get()
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
                return new LeaderboardDto
                {
                    UserName = u.Name,
                    TotalPoints = userPreds.Sum(p => p.Points),
                    DailyPoints = userPreds
                        .Where(p => matchLookup.ContainsKey(p.MatchId))
                        .GroupBy(p => matchLookup[p.MatchId].KickoffTime.ToString("dd/MM"))
                        .ToDictionary(g => g.Key, g => g.Sum(p => p.Points))
                };
            })
            .OrderByDescending(x => x.TotalPoints)
            .ToList();

        // Calculate position change
        var today = DateTime.UtcNow.ToString("dd/MM");
        var previousRanking = leaderboard
            .Select(x => new
            {
                x.UserName,
                PreviousPoints = x.TotalPoints - x.DailyPoints.GetValueOrDefault(today)
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

        return Ok(leaderboard);
    }
}