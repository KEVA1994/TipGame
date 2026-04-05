using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TipGame.Infrastructure.Data;
using TipGame.Domain.Entities;
using TipGame.Api.Models;

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
                Name = "Player" // kan forbedres senere
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // 2. Check match findes
        var matchExists = await _context.Matches
            .AnyAsync(m => m.Id == dto.MatchId);

        if (!matchExists)
            return BadRequest("Match not found");

        // 3. Opret prediction
        var prediction = new Prediction
        {
            MatchId = dto.MatchId,
            UserId = user.Id,
            PredictedHome = dto.HomeScore,
            PredictedAway = dto.AwayScore,
            Points = 0
        };

        _context.Predictions.Add(prediction);
        await _context.SaveChangesAsync();

        return Ok(prediction.Id);
    }

    [HttpGet("match/{matchId}")]
    public async Task<ActionResult<IEnumerable<PredictionDto>>> GetByMatch(int matchId)
    {
        var predictions = await _context.Predictions
            .Where(p => p.MatchId == matchId)
            .Include(p => p.User)
            .Select(p => new PredictionDto
            {
                Id = p.Id,
                UserName = p.User.Name,
                HomeScore = p.PredictedHome,
                AwayScore = p.PredictedAway,
                Points = p.Points
            })
            .ToListAsync();

        return Ok(predictions);
    }   
}