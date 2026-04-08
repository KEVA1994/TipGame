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
        var leaderboard = await _context.Users
            .Select(u => new LeaderboardDto
            {
                UserName = u.Name,
                TotalPoints = u.Predictions.Sum(p => p.Points)
            })
            .OrderByDescending(x => x.TotalPoints)
            .ToListAsync();

        return Ok(leaderboard);
    }
}