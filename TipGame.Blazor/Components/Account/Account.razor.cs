using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TipGame.Blazor.Components.Account;

public partial class AccountPopover
{
    [Inject] private PlayerState PlayerState { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private bool isSignUpMode;
    private string emailInput = string.Empty;
    private string passwordInput = string.Empty;
    private string signUpFirstName = string.Empty;
    private string signUpLastName = string.Empty;
    private string signUpEmail = string.Empty;
    private string signUpPassword = string.Empty;

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
}
