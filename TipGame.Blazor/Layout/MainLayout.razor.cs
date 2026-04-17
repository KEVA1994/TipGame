using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TipGame.Blazor.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private PlayerState PlayerState { get; set; } = default!;

    private bool drawerOpen = true;
    private bool isDarkMode = true;
    private string firstNameInput = string.Empty;
    private string lastNameInput = string.Empty;

    private readonly MudTheme appTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1B5E20",
            PrimaryDarken = "#0D3B0E",
            PrimaryLighten = "#388E3C",
            Secondary = "#FFB300",
            SecondaryDarken = "#E69500",
            SecondaryLighten = "#FFD54F",
            Tertiary = "#1565C0",
            AppbarBackground = "#1B5E20",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#F5F5F5",
            DrawerText = "#424242",
            Background = "#FAFAFA",
            Surface = "#FFFFFF",
            Success = "#2E7D32",
            Warning = "#F9A825",
            Error = "#C62828",
            Info = "#1565C0",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#43A047",
            PrimaryDarken = "#2E7D32",
            PrimaryLighten = "#66BB6A",
            Secondary = "#FFB300",
            SecondaryDarken = "#E69500",
            SecondaryLighten = "#FFD54F",
            Tertiary = "#42A5F5",
            AppbarBackground = "#1A2E1A",
            AppbarText = "#E8F5E9",
            DrawerBackground = "#1A2E1A",
            DrawerText = "#C8E6C9",
            Background = "#121212",
            Surface = "#1E1E1E",
            Success = "#43A047",
            Warning = "#FFB300",
            Error = "#EF5350",
            Info = "#42A5F5",
            TextPrimary = "#E0E0E0",
            TextSecondary = "#9E9E9E",
            ActionDefault = "#BDBDBD",
            DrawerIcon = "#81C784",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"]
            },
            H4 = new H4Typography { FontWeight = "800" },
            H6 = new H6Typography { FontWeight = "700" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };

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
