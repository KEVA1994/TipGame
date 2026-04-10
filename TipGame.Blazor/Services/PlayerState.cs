using Microsoft.JSInterop;

public class PlayerState
{
    private readonly IJSRuntime _js;

    public string PlayerName { get; private set; } = string.Empty;
    public bool IsInitialized { get; private set; }

    public event Action? OnChange;

    public PlayerState(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        PlayerName = await _js.InvokeAsync<string>("localStorage.getItem", "playerName") ?? string.Empty;
        IsInitialized = true;
    }

    public async Task SetNameAsync(string name)
    {
        PlayerName = name.Trim();
        await _js.InvokeVoidAsync("localStorage.setItem", "playerName", PlayerName);
        OnChange?.Invoke();
    }

    public async Task LogoutAsync()
    {
        PlayerName = string.Empty;
        await _js.InvokeVoidAsync("localStorage.removeItem", "playerName");
        OnChange?.Invoke();
    }
}
