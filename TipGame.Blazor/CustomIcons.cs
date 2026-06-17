namespace TipGame.Blazor;

/// <summary>
/// Custom SVG icons that aren't part of the bundled Material/Brand sets.
/// Paths use a 0 0 24 24 viewBox so they render correctly via MudBlazor's
/// icon components (MudIcon, MudIconButton, MudButton.StartIcon, …).
/// </summary>
public static class CustomIcons
{
    /// <summary>Facebook Messenger logo (Simple Icons, 24x24 viewBox).</summary>
    public const string Messenger =
        "<path d=\"M12 0C5.373 0 0 4.974 0 11.111c0 3.497 1.745 6.616 4.472 8.652V24l4.086-2.242c1.09.301 2.246.464 3.442.464 6.627 0 12-4.974 12-11.111C24 4.974 18.627 0 12 0zm1.191 14.963l-3.055-3.26-5.963 3.26 6.559-6.963 3.131 3.259 5.889-3.259-6.561 6.963z\"/>";
}
