using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace TipGame.Blazor.Components.Account;

public partial class AccountPopover
{
    [Inject] private PlayerState PlayerState { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    // PlayerState.PlayerName can change mid-flow (SignIn/SignUp update it —
    // sometimes more than once, see CompetitionState's reload coalescing
    // comment) — reacting to it live meant a successful login could flip the
    // still-open dialog from the login form straight to "Min konto" for a
    // moment before Close() caught up, reading as the dialog getting stuck on
    // the wrong screen. Decide once, when the dialog opens, whether to show
    // account info or the login form, and keep showing that for the dialog's
    // whole lifetime — a login/signup in progress always finishes on the form
    // it started on and then simply closes.
    private bool showAccountView;
    private bool wasOpen;

    protected override void OnParametersSet()
    {
        if (IsOpen && !wasOpen)
        {
            showAccountView = !string.IsNullOrEmpty(PlayerState.PlayerName);
        }
        wasOpen = IsOpen;
    }

    private bool isSignUpMode;
    private bool isForgotPasswordMode;
    private bool forgotPasswordSent;
    private bool isSendingResetEmail;
    private string emailInput = string.Empty;
    private string passwordInput = string.Empty;
    private string signUpFirstName = string.Empty;
    private string signUpLastName = string.Empty;
    private string signUpEmail = string.Empty;
    private string signUpPassword = string.Empty;
    private string forgotPasswordEmail = string.Empty;

    private readonly DialogOptions dialogOptions = new()
    {
        MaxWidth = MaxWidth.ExtraSmall,
        FullWidth = true,
        CloseOnEscapeKey = true,
        BackdropClick = true
    };

    private async Task Close()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    private async Task HandleLogin()
    {
        if (string.IsNullOrWhiteSpace(emailInput) || string.IsNullOrWhiteSpace(passwordInput)) return;

        var error = await PlayerState.SignInAsync(emailInput, passwordInput);
        if (error is not null)
        {
            Snackbar.Add("Forkert email eller kodeord.", Severity.Error);
        }
        else
        {
            emailInput = string.Empty;
            passwordInput = string.Empty;
            await Close();
        }
    }

    private async Task HandleSignUp()
    {
        if (string.IsNullOrWhiteSpace(signUpFirstName) || string.IsNullOrWhiteSpace(signUpLastName)
            || string.IsNullOrWhiteSpace(signUpEmail) || string.IsNullOrWhiteSpace(signUpPassword)) return;

        var displayName = $"{signUpFirstName.Trim()} {signUpLastName.Trim()}".Trim();
        var error = await PlayerState.SignUpAsync(signUpEmail, signUpPassword, displayName);
        if (error is not null)
        {
            Snackbar.Add(error, Severity.Error);
        }
        else
        {
            signUpFirstName = string.Empty;
            signUpLastName = string.Empty;
            signUpEmail = string.Empty;
            signUpPassword = string.Empty;
            Snackbar.Add("Konto oprettet! Du er nu logget ind.", Severity.Success);
            await Close();
        }
    }

    private async Task HandleLogout()
    {
        await PlayerState.LogoutAsync();
        await Close();
    }

    private async Task HandleLoginKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await HandleLogin();
    }

    private async Task HandleForgotPassword()
    {
        if (string.IsNullOrWhiteSpace(forgotPasswordEmail) || isSendingResetEmail) return;

        isSendingResetEmail = true;
        try
        {
            // Build the absolute URL for the reset page. NavigateTo("nulstil-kodeord", false)
            // would navigate locally; we just need the URL to give Supabase.
            var baseUri = Nav.BaseUri.TrimEnd('/');
            var redirect = $"{baseUri}/nulstil-kodeord";

            var error = await PlayerState.SendPasswordResetAsync(forgotPasswordEmail.Trim(), redirect);
            // Always show the same confirmation, regardless of whether the email exists,
            // to avoid leaking account information.
            forgotPasswordSent = true;

            if (error is not null)
            {
                // Still surface a soft hint if something obviously failed (e.g. invalid email format).
                Snackbar.Add("Vi forsøgte at sende e-mailen — modtager du intet, så prøv igen om lidt.", Severity.Info);
            }
        }
        finally
        {
            isSendingResetEmail = false;
        }
    }

    private async Task HandleForgotPasswordKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await HandleForgotPassword();
    }
}
