using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public sealed class GameSessionStorage(IJSRuntime js)
{
    private const string Key = "battleship.currentGame";

    public async Task SaveAsync(Guid id)
    {
        try
        {
            await js.InvokeVoidAsync("battleshipSettings.write", Key, id.ToString());
        }
        catch (JSException)
        {
        }
    }

    public async Task<Guid?> ReadAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string>("battleshipSettings.read", Key, "");
            return Guid.TryParse(stored, out var id) ? id : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await js.InvokeVoidAsync("battleshipSettings.remove", Key);
        }
        catch (JSException)
        {
        }
    }
}
