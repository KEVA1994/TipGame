public class PlayerState
{
    private readonly Supabase.Client _supabase;

    public string PlayerName { get; private set; } = string.Empty;
    public string? AuthId => _supabase.Auth.CurrentUser?.Id;
    public string Email => _supabase.Auth.CurrentUser?.Email ?? string.Empty;
    public bool IsLoggedIn => !string.IsNullOrEmpty(PlayerName);
    public bool IsInitialized { get; private set; }

    public event Action? OnChange;

    public PlayerState(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        var session = _supabase.Auth.CurrentSession;
        if (session?.User is not null)
        {
            PlayerName = ExtractDisplayName(session.User);
        }

        _supabase.Auth.AddStateChangedListener((sender, args) =>
        {
            var user = _supabase.Auth.CurrentUser;
            PlayerName = user is not null ? ExtractDisplayName(user) : string.Empty;
            OnChange?.Invoke();
        });

        IsInitialized = true;
    }

    public async Task<string?> SignInAsync(string email, string password)
    {
        try
        {
            var session = await _supabase.Auth.SignIn(email, password);
            if (session?.User is not null)
            {
                PlayerName = ExtractDisplayName(session.User);
                OnChange?.Invoke();
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> SignUpAsync(string email, string password, string displayName)
    {
        try
        {
            var session = await _supabase.Auth.SignUp(email, password, new Supabase.Gotrue.SignUpOptions
            {
                Data = new Dictionary<string, object> { ["display_name"] = displayName }
            });
            if (session?.User is not null)
            {
                PlayerName = displayName;
                OnChange?.Invoke();
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task LogoutAsync()
    {
        await _supabase.Auth.SignOut();
        PlayerName = string.Empty;
        OnChange?.Invoke();
    }

    private static string ExtractDisplayName(Supabase.Gotrue.User user)
    {
        if (user.UserMetadata?.TryGetValue("display_name", out var name) == true && name is not null)
            return name.ToString()!;

        return user.Email ?? string.Empty;
    }
}
