using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TipGame.Infrastructure.Data;
using TipGame.Domain.Entities;
using TipGame.Shared.Models;

namespace TipGame.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PredictionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePredictionDto dto)
    {
        // 1. Find eller opret user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.ClientId == dto.ClientId);

        if (user == null)
        {
            user = new User
            {
                ClientId = dto.ClientId,
                Name = dto.Name ?? "Player"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else if (!string.IsNullOrEmpty(dto.Name) && user.Name != dto.Name)
        {
            user.Name = dto.Name;
        }

        // 2. Check match findes og deadline
        var match = await _context.Matches.FindAsync(dto.MatchId);

        if (match == null)
            return BadRequest("Match not found");

        if (DateTime.UtcNow >= match.KickoffTime.AddHours(-1))
            return BadRequest("Deadline passed — tips lock 1 hour before kickoff");

        // 3. Opret eller opdater prediction
        var prediction = await _context.Predictions
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.MatchId == dto.MatchId);

        if (prediction != null)
        {
            prediction.PredictedHome = dto.HomeScore;
            prediction.PredictedAway = dto.AwayScore;
        }
        else
        {
            prediction = new Prediction
            {
                MatchId = dto.MatchId,
                UserId = user.Id,
                PredictedHome = dto.HomeScore,
                PredictedAway = dto.AwayScore,
                Points = 0
            };

            _context.Predictions.Add(prediction);
        }

        await _context.SaveChangesAsync();

        return Ok(prediction.Id);
    }

    [HttpGet("match/{matchId}")]
    public async Task<ActionResult<IEnumerable<Shared.Models.PredictionDto>>> GetByMatch(int matchId)
    {
        var predictions = await _context.Predictions
            .Where(p => p.MatchId == matchId)
            .Include(p => p.User)
            .Select(p => new PredictionDto
            {
                MatchId = p.Id,
                UserName = p.User.Name,
                HomeScore = p.PredictedHome,
                AwayScore = p.PredictedAway,
                Points = p.Points
            })
            .ToListAsync();

        return Ok(predictions);
    }

    // GET: api/predictions/user?clientId=xxx
    [HttpGet("user")]
    public async Task<ActionResult<IEnumerable<PredictionDto>>> GetByUser([FromQuery] string clientId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.ClientId == clientId);

        if (user == null)
            return Ok(new List<PredictionDto>());

        var tips = await _context.Predictions
            .Where(p => p.UserId == user.Id)
            .Select(p => new PredictionDto
            {
                MatchId = p.MatchId,
                HomeScore = p.PredictedHome,
                AwayScore = p.PredictedAway
            })
            .ToListAsync();

        return Ok(tips);
    }
}