using Microsoft.AspNetCore.Mvc;
using TipGame.Domain.Entities;
using TipGame.Shared.Models;

namespace TipGame.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ControllerBase
{
    private readonly Supabase.Client _supabase;

    public PredictionsController(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePredictionDto dto)
    {
        // 1. Find or create user by name
        var userResponse = await _supabase.From<User>()
            .Where(u => u.Name == dto.Name)
            .Get();

        var user = userResponse.Models.FirstOrDefault();

        if (user == null)
        {
            user = new User { Name = dto.Name ?? "Player" };
            var insertResponse = await _supabase.From<User>().Insert(user);
            user = insertResponse.Models.First();
        }

        // 2. Check match exists and deadline
        var matchResponse = await _supabase.From<Match>()
            .Where(m => m.Id == dto.MatchId)
            .Get();

        var match = matchResponse.Models.FirstOrDefault();
        if (match == null)
            return BadRequest("Match not found");

        if (DateTime.UtcNow >= match.KickoffTime.AddHours(-1))
            return BadRequest("Deadline passed — tips lock 1 hour before kickoff");

        // 3. Upsert prediction
        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == user.Id)
            .Where(p => p.MatchId == dto.MatchId)
            .Get();

        var prediction = predResponse.Models.FirstOrDefault();

        if (prediction != null)
        {
            await _supabase.From<Prediction>()
                .Where(p => p.Id == prediction.Id)
                .Set(p => p.PredictedHome, dto.HomeScore)
                .Set(p => p.PredictedAway, dto.AwayScore)
                .Update();
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

            var insertResponse = await _supabase.From<Prediction>().Insert(prediction);
            prediction = insertResponse.Models.First();
        }

        return Ok(prediction.Id);
    }

    [HttpGet("match/{matchId}")]
    public async Task<ActionResult<IEnumerable<PredictionDto>>> GetByMatch(int matchId)
    {
        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.MatchId == matchId)
            .Get();

        var userIds = predResponse.Models.Select(p => p.UserId).Distinct().ToList();
        var usersResponse = await _supabase.From<User>().Get();
        var usersLookup = usersResponse.Models.ToDictionary(u => u.Id);

        var predictions = predResponse.Models.Select(p => new PredictionDto
        {
            MatchId = p.MatchId,
            UserName = usersLookup.GetValueOrDefault(p.UserId)?.Name ?? "Unknown",
            HomeScore = p.PredictedHome,
            AwayScore = p.PredictedAway,
            Points = p.Points
        });

        return Ok(predictions);
    }

    // GET: api/predictions/user?name=xxx
    [HttpGet("user")]
    public async Task<ActionResult<IEnumerable<PredictionDto>>> GetByUser([FromQuery] string name)
    {
        var userResponse = await _supabase.From<User>()
            .Where(u => u.Name == name)
            .Get();

        var user = userResponse.Models.FirstOrDefault();
        if (user == null)
            return Ok(new List<PredictionDto>());

        var predResponse = await _supabase.From<Prediction>()
            .Where(p => p.UserId == user.Id)
            .Get();

        var tips = predResponse.Models.Select(p => new PredictionDto
        {
            MatchId = p.MatchId,
            HomeScore = p.PredictedHome,
            AwayScore = p.PredictedAway
        });

        return Ok(tips);
    }
}