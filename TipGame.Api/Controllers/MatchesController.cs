using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TipGame.Infrastructure.Data;
using TipGame.Domain.Entities;
using TipGame.Shared.Models;

namespace TipGame.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PredictionService _predictionService;

    public MatchesController(AppDbContext context, PredictionService predictionService)
    {
        this._context = context;
        this._predictionService = predictionService;
    }

    // GET: api/matches
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetAll()
    {
        var matches = await _context.Matches
            .OrderBy(m => m.KickoffTime)
            .Select(m => new MatchDto
            {
                Id = m.Id,
                HomeTeam = m.HomeTeam,
                AwayTeam = m.AwayTeam,
                KickoffTime = m.KickoffTime,
                Status = m.Status,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore
            })
            .ToListAsync();

        return Ok(matches);
    }

    // GET: api/matches/1
    [HttpGet("{id}")]
    public async Task<ActionResult<MatchDto>> GetById(int id)
    {
        var match = await _context.Matches.FindAsync(id);

        if (match == null)
            return NotFound();

        return Ok(new MatchDto
        {
            Id = match.Id,
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            KickoffTime = match.KickoffTime,
            Status = match.Status,
            HomeScore = match.HomeScore,
            AwayScore = match.AwayScore
        });
    }
    // POST: api/matches/1/result
    [HttpPost("{id}/result")]
    public async Task<IActionResult> SetResult(int id, [FromBody] SetResultDto dto)
    {
        var match = await _context.Matches
            .Include(m => m.Predictions)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (match == null)
            return NotFound();

        // Sæt resultat
        match.HomeScore = dto.HomeScore;
        match.AwayScore = dto.AwayScore;
        match.Status = "FINISHED";

        // 🔥 Beregn points
        _predictionService.CalculatePoints(match);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Result updated and points calculated" });
    }

    // POST: api/matches
    [HttpPost]
    public async Task<ActionResult<MatchDto>> Create(CreateMatchDto dto)
    {
        var match = new Match
        {
            HomeTeam = dto.HomeTeam,
            AwayTeam = dto.AwayTeam,
            KickoffTime = dto.KickoffTime,
            Status = "SCHEDULED"
        };

        _context.Matches.Add(match);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = match.Id }, new MatchDto
        {
            Id = match.Id,
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            KickoffTime = match.KickoffTime,
            Status = match.Status
        });
    }

    // DELETE: api/matches/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var match = await _context.Matches.FindAsync(id);

        if (match == null)
            return NotFound();

        _context.Matches.Remove(match);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}