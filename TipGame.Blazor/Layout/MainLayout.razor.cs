using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TipGame.Blazor.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private PlayerState PlayerState { get; set; } = default!;

    private bool drawerOpen = true;
    private string firstNameInput = string.Empty;
    private string lastNameInput = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await PlayerState.InitializeAsync();
        PlayerState.OnChange += StateHasChanged;
    }

    private void ToggleDrawer() => drawerOpen = !drawerOpen;

    private async Task SaveName()
    {
        var fullName = $"{firstNameInput.Trim()} {lastNameInput.Trim()}";
        await PlayerState.SetNameAsync(fullName);
    }

    private async Task SwitchUser(MudChip<string> chip)
    {
        firstNameInput = string.Empty;
        lastNameInput = string.Empty;
        await PlayerState.LogoutAsync();
    }

    public void Dispose()
    {
        PlayerState.OnChange -= StateHasChanged;
    }
}
