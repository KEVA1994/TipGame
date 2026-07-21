public class PlayerState
{
    private readonly Supabase.Client _supabase;

    public string PlayerName { get; private set; } = string.Empty;
    public string? AuthId { get; private set; }
    public string Email { get; private set; } = string.Empty;
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
            AuthId = session.User.Id;
            Email = session.User.Email ?? string.Empty;
        }

        _supabase.Auth.AddStateChangedListener((sender, args) =>
        {
            var user = _supabase.Auth.CurrentUser;
            PlayerName = user is not null ? ExtractDisplayName(user) : string.Empty;
            AuthId = user?.Id;
            Email = user?.Email ?? string.Empty;
            OnChange?.Invoke();
        });

        IsInitialized = true;
    }

    public async Task<string?> SignInAsync(string email, string password)
    {
        try
        {
            var session = await _supabase.Auth.SignIn(email, password);
            session = await EnsureSessionWiredAsync(session);
            if (session?.User is not null)
            {
                PlayerName = ExtractDisplayName(session.User);
                AuthId = session.User.Id;
                Email = session.User.Email ?? string.Empty;
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
            session = await EnsureSessionWiredAsync(session);
            if (session?.User is not null)
            {
                PlayerName = displayName;
                AuthId = session.User.Id;
                Email = session.User.Email ?? string.Empty;
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
        AuthId = null;
        Email = string.Empty;
        OnChange?.Invoke();
    }

    public async Task<string?> SendPasswordResetAsync(string email, string redirectUrl)
    {
        try
        {
            // Supabase Gotrue v1.1.1 sends the email; the recovery link points to the
            // redirect URL configured both here and in Supabase's Auth -> URL Configuration.
            await _supabase.Auth.ResetPasswordForEmail(new Supabase.Gotrue.ResetPasswordForEmailOptions(email)
            {
                RedirectTo = redirectUrl
            });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> ApplyRecoverySessionAsync(string accessToken, string refreshToken)
    {
        try
        {
            var session = await _supabase.Auth.SetSession(accessToken, refreshToken);
            if (session?.User is not null)
            {
                PlayerName = ExtractDisplayName(session.User);
                AuthId = session.User.Id;
                Email = session.User.Email ?? string.Empty;
                OnChange?.Invoke();
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            await _supabase.Auth.Update(new Supabase.Gotrue.UserAttributes
            {
                Password = newPassword
            });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // SignUp/SignIn establish a session object in memory, but the Supabase C#
    // SDK does not reliably wire that session into outgoing Postgrest/RPC
    // requests (or persist it) the same way a session restored via SetSession
    // does — observed as RPC calls failing with "Not authenticated" right
    // after signup even though the user is clearly logged in client-side.
    // Re-applying the session through SetSession forces the same code path
    // that a page-load session restore uses, which reliably wires headers.
    private async Task<Supabase.Gotrue.Session?> EnsureSessionWiredAsync(Supabase.Gotrue.Session? session)
    {
        if (session?.AccessToken is null || session.RefreshToken is null)
            return session;

        try
        {
            return await _supabase.Auth.SetSession(session.AccessToken, session.RefreshToken);
        }
        catch
        {
            return session;
        }
    }

    private static string ExtractDisplayName(Supabase.Gotrue.User user)
    {
        if (user.UserMetadata?.TryGetValue("display_name", out var name) == true && name is not null)
            return name.ToString()!;

        return user.Email ?? string.Empty;
    }
}
