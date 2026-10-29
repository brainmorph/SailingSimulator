using Godot;
using SailingSimulator.Adapters;

/// <summary>Physical keys keep sailing independent of text layout and mouse camera events.</summary>
public static class SailingKeyMap
{
    private static readonly (string Action, Key Key)[] Bindings =
    {
        ("tiller_port", Key.A), ("tiller_starboard", Key.D),
        ("sheet_in", Key.W), ("sheet_out", Key.S),
        ("traveler_in", Key.Q), ("traveler_out", Key.E),
        ("vang_on", Key.R), ("vang_off", Key.F),
        ("outhaul_on", Key.T), ("outhaul_off", Key.G),
        ("cunningham_on", Key.U), ("cunningham_off", Key.J),
        ("board_down", Key.I), ("board_up", Key.K),
        ("hike_port", Key.Z), ("hike_starboard", Key.X),
        ("port_jib_trim", Key.O), ("port_jib_ease", Key.L),
        ("starboard_jib_trim", Key.P), ("starboard_jib_ease", Key.Semicolon)
    };

    public static void Register()
    {
        foreach (var (action, key) in Bindings)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }

    /// <summary>Jib-sheet keys are ignored for boats without a jib, so their controls stay unchanged.</summary>
    public static HeldSailingInput Read(bool jibSheets = true) => new(
        Axis("tiller_port", "tiller_starboard"), Axis("sheet_out", "sheet_in"),
        Axis("traveler_out", "traveler_in"), Axis("vang_off", "vang_on"),
        Axis("outhaul_off", "outhaul_on"), Axis("cunningham_off", "cunningham_on"),
        Axis("board_up", "board_down"), Axis("hike_port", "hike_starboard"),
        jibSheets ? Axis("port_jib_ease", "port_jib_trim") : 0,
        jibSheets ? Axis("starboard_jib_ease", "starboard_jib_trim") : 0);

    private static double Axis(string negative, string positive) =>
        (Input.IsActionPressed(positive) ? 1 : 0) - (Input.IsActionPressed(negative) ? 1 : 0);

    public static void ReleaseAll()
    {
        foreach (var (action, _) in Bindings) Input.ActionRelease(action);
    }
}
