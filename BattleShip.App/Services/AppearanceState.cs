using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public sealed record Skin(string Id, string Name, string Hint, string[] Swatch);

public sealed class AppearanceState(IJSRuntime js)
{
    private const string ThemeKey = "battleship.theme";
    private const string MotionKey = "battleship.motion";

    public static readonly IReadOnlyList<Skin> Skins =
    [
        new("chart", "Hydrographic",
            "A sea chart. Shots land as ink.",
            ["#0E2233", "#DCE3E0", "#C8322A", "#D8A62B"]),
        new("steel", "Steel",
            "Painted hull plate. Shots burn.",
            ["#22262B", "#464D56", "#D8501F", "#DFA425"]),
        new("board", "Tabletop",
            "The plastic game, up close. Shots are pegs.",
            ["#10365E", "#1B57A2", "#C2261F", "#EFEDE7"])
    ];

    public string Theme { get; private set; } = "chart";

    public string Motion { get; private set; } = "full";

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        Theme = Validate(await ReadAsync(ThemeKey, "chart"), Skins.Select(s => s.Id), "chart");
        Motion = Validate(await ReadAsync(MotionKey, "full"), ["full", "calm"], "full");
        OnChange?.Invoke();
    }

    public async Task SetThemeAsync(string theme)
    {
        Theme = Validate(theme, Skins.Select(s => s.Id), Theme);
        await ApplyAsync(ThemeKey, Theme);
    }

    public async Task SetMotionAsync(string motion)
    {
        Motion = Validate(motion, ["full", "calm"], Motion);
        await ApplyAsync(MotionKey, Motion);
    }

    private async Task ApplyAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("battleshipSettings.write", key, value);
            await js.InvokeVoidAsync("battleshipSettings.apply", Theme, Motion);
        }
        catch (JSException)
        {
        }

        OnChange?.Invoke();
    }

    private async Task<string> ReadAsync(string key, string fallback)
    {
        try
        {
            return await js.InvokeAsync<string>("battleshipSettings.read", key, fallback);
        }
        catch (JSException)
        {
            return fallback;
        }
    }

    private static string Validate(string? value, IEnumerable<string> allowed, string fallback) =>
    value is not null && allowed.Contains(value, StringComparer.Ordinal) ? value : fallback;
}
