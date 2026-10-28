using Godot;
using SailingSimulator.Adapters;

/// <summary>A passive reference panel: sailing and camera input continue while it is open.</summary>
internal static class ControlsHelp
{
    public static Control Create()
    {
        var root = new CenterContainer
        {
            Name = "ControlsHelp", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new(0.035f, 0.075f, 0.10f, 0.97f),
            BorderColor = new(0.25f, 0.49f, 0.57f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 20, ContentMarginBottom = 20
        });
        root.AddChild(panel);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        column.AddChild(Label("Controls", 26, "Title"));
        column.AddChild(Label("Hold sailing keys to adjust gradually. Opposing keys cancel.", 16));
        var grid = new GridContainer { Name = "Rows", Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(grid);
        column.AddChild(Label("", 16, "Notes"));
        column.AddChild(Label("Sailing continues while this window is open.  ·  F2 to close", 16));
        Show(root, BoatChoice.Ilca7);
        return root;
    }

    /// <summary>Lists only the selected boat's controls.</summary>
    public static void Show(Control root, BoatChoice boat)
    {
        var grid = root.FindChild("Rows", owned: false);
        foreach (var child in grid.GetChildren())
        {
            grid.RemoveChild(child);
            child.QueueFree();
        }
        var hunter = boat == BoatChoice.Hunter235;
        var crew = hunter ? "skipper" : "sailor";
        foreach (var (keys, action) in Rows(hunter, crew))
        {
            var keyLabel = Label(keys, 18);
            keyLabel.Modulate = new(0.55f, 0.85f, 0.95f);
            grid.AddChild(keyLabel);
            grid.AddChild(Label(action, 18));
        }
        ((Label)root.FindChild("Notes", owned: false)).Text = (hunter
            ? "Each jib sheet is independent: when tacking or jibing, ease the old sheet and trim the new one.\n"
            : "") + $"Release steering to centre the tiller.\nTrim, board and {crew} position stay where you leave them.";
        ((Label)root.FindChild("Title", owned: false)).Text = $"Controls · {BoatCatalog.Name(boat)}";
    }

    /// <summary>The displayed rows, for checks against the key map.</summary>
    public static IEnumerable<(string Keys, string Action)> Rows(bool hunter, string crew)
    {
        yield return ("A / D", "Steer port / starboard (left / right)");
        yield return ("W / S", "Mainsheet in / out");
        if (hunter)
        {
            yield return ("O / L", "Port jib sheet trim / ease");
            yield return ("P / ;", "Starboard jib sheet trim / ease");
        }
        yield return ("Q / E", "Traveler in / out");
        yield return ("R / F", "Vang on / off");
        yield return ("T / G", "Outhaul on / off");
        yield return ("U / J", "Cunningham on / off");
        yield return ("I / K", "Centerboard down / up");
        yield return ("Z / X", hunter ? "Move skipper toward port / starboard seat" : "Move sailor toward port / starboard");
        yield return ("C", "Switch onboard / third-person camera");
        yield return ("Right mouse drag", "Look around / orbit camera");
        yield return ("Mouse wheel", "Zoom");
        yield return ("F1", "Environment settings, boat and diagnostics");
        yield return ("F2", "Show / hide this window");
    }

    private static Label Label(string text, int size, string? name = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (name is not null) label.Name = name;
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
