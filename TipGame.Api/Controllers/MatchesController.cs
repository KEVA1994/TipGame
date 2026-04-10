using Microsoft.AspNetCore.Mvc;
using Supabase.Postgrest;
using TipGame.Domain.Entities;
using TipGame.Shared.Models;

namespace TipGame.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly Supabase.Client _supabase;
    private readonly PredictionService _predictionService;

    public MatchesController(Supabase.Client supabase, PredictionService predictionService)
    {
        _supabase = supabase;
        _predictionService = predictionService;
    }

    // GET: api/matches
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetAll()
    {
        var response = await _supabase.From<Match>()
            .Order("KickoffTime", Constants.Ordering.Ascending)
            .Get();

        var matches = response.Models.Select(m => new MatchDto
        {
            Id = m.Id,
            HomeTeam = m.HomeTeam,
            AwayTeam = m.AwayTeam,
            KickoffTime = m.KickoffTime,
            Status = m.Status,
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore
        });

        return Ok(matches);
    }

    // GET: api/matches/1
    [HttpGet("{id}")]
    public async Task<ActionResult<MatchDto>> GetById(int id)
    {
        var response = await _supabase.From<Match>()
            .Where(m => m.Id == id)
            .Get();

        var match = response.Models.FirstOrDefault();
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
        var matchResponse = await _supabase.From<Match>()
            .Where(m => m.Id == id)
            .Get();

        var match = matchResponse.Models.FirstOrDefault();
        if (match == null)
            return NotFound();

        // Update match result
        await _supabase.From<Match>()
            .Where(m => m.Id == id)
            .Set(m => m.HomeScore, dto.HomeScore)
            .Set(m => m.AwayScore, dto.AwayScore)
            .Set(m => m.Status, "FINISHED")
            .Update();

        // Load predictions for point calculation
        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.MatchId == id)
            .Get();

        match.HomeScore = dto.HomeScore;
        match.AwayScore = dto.AwayScore;
        match.Status = "FINISHED";
        match.Predictions = predResponse.Models.ToList<Prediction>();

        _predictionService.CalculatePoints(match);

        // Update each prediction's points
        foreach (var pred in match.Predictions)
        {
            await _supabase.From<Prediction>()
                .Where(p => p.Id == pred.Id)
                .Set(p => p.Points, pred.Points)
                .Update();
        }

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

        var response = await _supabase.From<Match>().Insert(match);
        var created = response.Models.First();

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new MatchDto
        {
            Id = created.Id,
            HomeTeam = created.HomeTeam,
            AwayTeam = created.AwayTeam,
            KickoffTime = created.KickoffTime,
            Status = created.Status
        });
    }

    // DELETE: api/matches/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var response = await _supabase.From<Match>()
            .Where(m => m.Id == id)
            .Get();

        if (!response.Models.Any())
            return NotFound();

        await _supabase.From<Match>()
            .Where(m => m.Id == id)
            .Delete();

        return NoContent();
    }
}