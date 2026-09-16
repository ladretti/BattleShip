using Microsoft.JSInterop;

namespace BattleShip.App.Services;

/// <summary>One of the three skins, with the copy the settings panel shows for it.</summary>
public sealed record Skin(string Id, string Name, string Hint, string[] Swatch);

/// <summary>
/// The look of the interface, and how much of it moves. Both are player settings, changeable
/// at any time, and both are remembered between visits.
///
/// <para>The three skins differ in substrate and material, never in structure: the same
/// markup, the same glyph on every cell, the same keyboard behaviour. That is what keeps one
/// accessibility story rather than three — see <c>wwwroot/css/app.css</c>.</para>
///
/// <para>The values live in <c>localStorage</c>, which is per-browser and can throw outright
/// in a private window or with site data blocked. Every call below therefore tolerates
/// failure and falls back to the defaults: a player who cannot store a preference must still
/// get a working game, not an exception.</para>
/// </summary>
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

    /// <summary>"full" or "calm". Calm stops the impact choreography without stopping the
    /// game: the states still change, they just stop moving.</summary>
    public string Motion { get; private set; } = "full";

    public event Action? OnChange;

    /// <summary>
    /// Reads back what the page already applied before Blazor booted. The bootstrap script in
    /// <c>index.html</c> sets <c>data-theme</c> on the first paint precisely so the player
    /// never sees one skin flash into another; this only brings those values into C#.
    /// </summary>
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
            // Storage refused. The setting still applies for this session through OnChange
            // below; only remembering it is lost, which is not worth an error on screen.
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

    /// <summary>
    /// A value from storage is data the player's browser hands back, not a value this code
    /// chose: it can be stale from an older build, or edited by hand. Anything outside the
    /// known set falls back rather than reaching the DOM as an unknown attribute.
    /// </summary>
    private static string Validate(string? value, IEnumerable<string> allowed, string fallback) =>
        value is not null && allowed.Contains(value, StringComparer.Ordinal) ? value : fallback;
}
