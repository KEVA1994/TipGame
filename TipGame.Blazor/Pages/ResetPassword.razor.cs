using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace TipGame.Blazor.Pages;

public partial class ResetPassword
{
    [Inject] private PlayerState PlayerState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool isLoading = true;
    private bool isSaving;
    private bool isCompleted;
    private string? errorMessage;
    private string? validationMessage;

    private string newPassword = string.Empty;
    private string confirmPassword = string.Empty;

    private bool CanSubmit =>
        !isSaving
        && !string.IsNullOrWhiteSpace(newPassword)
        && newPassword.Length >= 6
        && newPassword == confirmPassword;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await PlayerState.InitializeAsync();

        try
        {
            var fragment = await JS.InvokeAsync<RecoveryFragment>("authRecovery.readAndClearFragment");

            if (!string.IsNullOrEmpty(fragment.Error))
            {
                errorMessage = $"Linket er ikke gyldigt: {fragment.Error}. Prøv at anmode om et nyt nulstillingslink.";
            }
            else if (string.IsNullOrEmpty(fragment.AccessToken) || string.IsNullOrEmpty(fragment.RefreshToken))
            {
                errorMessage = "Dette link er ikke gyldigt eller er udløbet. Prøv at anmode om et nyt nulstillingslink fra login-vinduet.";
            }
            else if (!string.Equals(fragment.Type, "recovery", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Linket har en ukendt type. Prøv at anmode om et nyt nulstillingslink.";
            }
            else
            {
                var sessionError = await PlayerState.ApplyRecoverySessionAsync(fragment.AccessToken, fragment.RefreshToken);
                if (sessionError is not null)
                {
                    errorMessage = "Vi kunne ikke validere linket. Det er muligvis udløbet — anmod om et nyt nulstillingslink.";
                }
            }
        }
        catch
        {
            errorMessage = "Der opstod en uventet fejl. Prøv at indlæse siden igen.";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleSubmit()
    {
        validationMessage = null;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            validationMessage = "Kodeordet skal være mindst 6 tegn.";
            return;
        }
        if (newPassword != confirmPassword)
        {
            validationMessage = "De to kodeord er ikke ens.";
            return;
        }

        isSaving = true;
        try
        {
            var error = await PlayerState.UpdatePasswordAsync(newPassword);
            if (error is not null)
            {
                validationMessage = "Kodeordet kunne ikke opdateres. Prøv igen.";
                return;
            }

            isCompleted = true;
            StateHasChanged();

            // Give the user a moment to read the success message before redirecting home.
            await Task.Delay(2000);
            Nav.NavigateTo("");
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task HandleKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && CanSubmit) await HandleSubmit();
    }

    private sealed class RecoveryFragment
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Type { get; set; }
        public string? Error { get; set; }
    }
}
