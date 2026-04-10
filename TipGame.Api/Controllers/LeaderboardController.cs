using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TipGame.Infrastructure.Data;
using TipGame.Shared.Models;

namespace TipGame.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public LeaderboardController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/leaderboard
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaderboardDto>>> Get()
    {
        var users = await _context.Users
            .Include(u => u.Predictions)
                .ThenInclude(p => p.Match)
            .ToListAsync();

        // Build current leaderboard with daily breakdown
        var leaderboard = users
            .Select(u => new LeaderboardDto
            {
                UserName = u.Name,
                TotalPoints = u.Predictions.Sum(p => p.Points),
                DailyPoints = u.Predictions
                    .Where(p => p.Match.Status == "FINISHED")
                    .GroupBy(p => p.Match.KickoffTime.ToString("dd/MM"))
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Points))
            })
            .OrderByDescending(x => x.TotalPoints)
            .ToList();

        // Calculate position change by comparing with "yesterday's" ranking
        // (ranking without today's points)
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
                player.Change = prevPos - i; // positive = moved up
            }
        }

        return Ok(leaderboard);
    }
}