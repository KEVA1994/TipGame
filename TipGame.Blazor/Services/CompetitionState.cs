using System.Text.Json;
using Microsoft.JSInterop;
using TipGame.Domain.Entities;

/// <summary>
/// Holds the logged-in player's competitions and which one is currently
/// selected. Every data service filters by <see cref="Current"/>, and the
/// selection survives reloads via localStorage. Reloads itself when the
/// login state changes.
/// </summary>
public class CompetitionState
{
    private const string StorageKey = "tipgame-competition";

    private readonly Supabase.Client _supabase;
    private readonly PlayerState _playerState;
    private readonly IJSRuntime _js;
    private Dictionary<int, string> _roles = new();
    private Task? _initTask;

    public List<Competition> MyCompetitions { get; private set; } = new();
    public Competition? Current { get; private set; }
    public bool IsAdmin => Current is not null && _roles.GetValueOrDefault(Current.Id) == "admin";
    public bool IsLoaded { get; private set; }

    public event Action? OnChange;

    public CompetitionState(Supabase.Client supabase, PlayerState playerState, IJSRuntime js)
    {
        _supabase = supabase;
        _playerState = playerState;
        _js = js;
    }

    // Concurrent callers (layout + page rendering at the same time) all await
    // the same first load instead of returning before Current is set.
    public Task InitializeAsync() => _initTask ??= FirstLoadAsync();

    private async Task FirstLoadAsync()
    {
        _playerState.OnChange += () => _ = ReloadAsync();
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_playerState.AuthId))
            {
                MyCompetitions = new();
                _roles = new();
                Current = null;
                return;
            }

            var authId = _playerState.AuthId;
            var userResponse = await _supabase.From<User>()
                .Where(u => u.AuthId == authId)
                .Get();
            var userId = userResponse.Models.FirstOrDefault()?.Id;
            if (userId is null)
            {
                MyCompetitions = new();
                _roles = new();
                Current = null;
                return;
            }

            // RLS limits both queries to competitions the player is a member of.
            var membersTask = _supabase.From<CompetitionMember>()
                .Where(m => m.UserId == userId.Value)
                .Get();
            var compsTask = _supabase.From<Competition>().Get();
            await Task.WhenAll(membersTask, compsTask);

            _roles = membersTask.Result.Models.ToDictionary(m => m.CompetitionId, m => m.Role);
            MyCompetitions = compsTask.Result.Models
                .Where(c => _roles.ContainsKey(c.Id))
                .OrderBy(c => c.Status == "active" ? 0 : c.Status == "draft" ? 1 : 2)
                .ThenByDescending(c => c.Id)
                .ToList();

            var storedId = await ReadStoredSelectionAsync();
            Current = MyCompetitions.FirstOrDefault(c => c.Id == storedId)
                      ?? MyCompetitions.FirstOrDefault();
        }
        finally
        {
            IsLoaded = true;
            OnChange?.Invoke();
        }
    }

    public async Task SetCurrentAsync(int competitionId)
    {
        var comp = MyCompetitions.FirstOrDefault(c => c.Id == competitionId);
        if (comp is null) return;
        Current = comp;
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, competitionId.ToString());
        }
        catch { }
        OnChange?.Invoke();
    }

    private async Task<int?> ReadStoredSelectionAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return int.TryParse(stored, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }

    // --- RPC wrappers -------------------------------------------------------

    // Right after login/signup there is a brief window where the auth state
    // has changed but the Postgrest client still sends the anon key — an RPC
    // fired from an OnChange handler then fails auth. Retry shortly instead
    // of surfacing "log ind" to a user who just logged in.
    private async Task<Supabase.Postgrest.Responses.BaseResponse> RpcAsync(
        string function, Dictionary<string, object?> args)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await _supabase.Rpc(function, args);
            }
            catch (Exception ex) when (attempt < 3 && ex.Message.Contains("Not authenticated"))
            {
                await Task.Delay(500);
            }
        }
    }

    /// <summary>Creates a competition (draft), activates it and selects it.</summary>
    public async Task<(int? CompetitionId, string? Error)> CreateCompetitionAsync(
        string name, string code, DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            var response = await RpcAsync("create_competition", new Dictionary<string, object?>
            {
                ["p_name"] = name,
                ["p_code"] = code,
                ["p_date_from"] = dateFrom?.ToString("yyyy-MM-dd"),
                ["p_date_to"] = dateTo?.ToString("yyyy-MM-dd"),
            });
            var id = JsonSerializer.Deserialize<int>(response.Content ?? "0");

            // v1: activation is free and immediate. This call is the payment
            // hook — when creation becomes paid, it moves to a Stripe webhook.
            await RpcAsync("activate_competition", new Dictionary<string, object?>
            {
                ["p_competition_id"] = id,
            });

            await ReloadAsync();
            await SetCurrentAsync(id);
            return (id, null);
        }
        catch (Exception ex)
        {
            return (null, TranslateRpcError(ex.Message));
        }
    }

    public async Task<(string? Name, string? Status)> GetCompetitionByTokenAsync(Guid token)
    {
        try
        {
            var response = await RpcAsync("get_competition_by_token", new Dictionary<string, object?>
            {
                ["p_token"] = token.ToString(),
            });
            using var doc = JsonDocument.Parse(response.Content ?? "[]");
            var row = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (row.ValueKind != JsonValueKind.Object) return (null, null);
            return (row.GetProperty("Name").GetString(), row.GetProperty("Status").GetString());
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<(int? CompetitionId, string? Error)> JoinCompetitionAsync(Guid token)
    {
        try
        {
            var response = await RpcAsync("join_competition", new Dictionary<string, object?>
            {
                ["p_token"] = token.ToString(),
            });
            var id = JsonSerializer.Deserialize<int>(response.Content ?? "0");
            await ReloadAsync();
            await SetCurrentAsync(id);
            return (id, null);
        }
        catch (Exception ex)
        {
            return (null, TranslateRpcError(ex.Message));
        }
    }

    public async Task<Guid?> RegenerateInviteTokenAsync(int competitionId)
    {
        try
        {
            var response = await RpcAsync("regenerate_invite_token", new Dictionary<string, object?>
            {
                ["p_competition_id"] = competitionId,
            });
            var token = JsonSerializer.Deserialize<Guid>(response.Content ?? "null");
            await ReloadAsync();
            return token;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Queues a mail (currently only "final_standings") for a competition. Admin-only, one request per kind ever.</summary>
    public async Task<string?> RequestEmailAsync(int competitionId, string kind)
    {
        try
        {
            await RpcAsync("request_email", new Dictionary<string, object?>
            {
                ["p_competition_id"] = competitionId,
                ["p_kind"] = kind,
            });
            return null;
        }
        catch (Exception ex)
        {
            return TranslateRpcError(ex.Message);
        }
    }

    private static string TranslateRpcError(string message) => message switch
    {
        _ when message.Contains("not open for joining") =>
            "Konkurrencen er ikke åben for tilmelding.",
        _ when message.Contains("Invalid invite link") =>
            "Invitationslinket er ugyldigt — bed din admin om et nyt.",
        _ when message.Contains("Not authenticated") =>
            "Du skal være logget ind.",
        _ when message.Contains("Invalid competition code") =>
            "Ugyldig turneringskode.",
        _ when message.Contains("already been requested") =>
            "Mailen er allerede anmodet om — den bliver kun sendt én gang.",
        _ when message.Contains("Only the competition admin") =>
            "Kun konkurrencens admin kan gøre dette.",
        _ => "Noget gik galt. Prøv igen.",
    };
}
