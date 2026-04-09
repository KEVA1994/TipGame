using Microsoft.JSInterop;

public class PlayerState
{
    private readonly IJSRuntime _js;

    public string ClientId { get; private set; } = string.Empty;
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

        ClientId = await _js.InvokeAsync<string>("localStorage.getItem", "clientId") ?? string.Empty;
        if (string.IsNullOrEmpty(ClientId))
        {
            ClientId = Guid.NewGuid().ToString();
            await _js.InvokeVoidAsync("localStorage.setItem", "clientId", ClientId);
        }

        PlayerName = await _js.InvokeAsync<string>("localStorage.getItem", "playerName") ?? string.Empty;

        IsInitialized = true;
    }

    public async Task SetNameAsync(string name)
    {
        PlayerName = name.Trim();
        await _js.InvokeVoidAsync("localStorage.setItem", "playerName", PlayerName);
        OnChange?.Invoke();
    }
}
