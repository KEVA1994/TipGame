using Microsoft.JSInterop;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

/// <summary>
/// Persists the Supabase auth session in the browser's localStorage so the user
/// stays logged in across page reloads. Uses the synchronous in-process JS runtime
/// available in Blazor WebAssembly.
/// </summary>
public class LocalStorageSessionPersistence : IGotrueSessionPersistence<Session>
{
    private const string Key = "tipgame.supabase.session";
    private readonly IJSInProcessRuntime _js;

    public LocalStorageSessionPersistence(IJSInProcessRuntime js)
    {
        _js = js;
    }

    public void SaveSession(Session session)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(session);
        _js.InvokeVoid("localStorage.setItem", Key, json);
    }

    public void DestroySession()
    {
        _js.InvokeVoid("localStorage.removeItem", Key);
    }

    public Session? LoadSession()
    {
        var json = _js.Invoke<string?>("localStorage.getItem", Key);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Session>(json);
        }
        catch
        {
            return null;
        }
    }
}
