using Godot;

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
        column.AddChild(Label("Controls", 26));
        column.AddChild(Label("Hold sailing keys to adjust gradually. Opposing keys cancel.", 16));
        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(grid);
        var rows = new (string Keys, string Action)[]
        {
            ("A / D", "Steer port / starboard (left / right)"),
            ("W / S", "Mainsheet in / out"),
            ("Q / E", "Traveler in / out"),
            ("R / F", "Vang on / off"),
            ("T / G", "Outhaul on / off"),
            ("U / J", "Cunningham on / off"),
            ("I / K", "Centerboard down / up"),
            ("Z / X", "Move sailor toward port / starboard"),
            ("C", "Switch onboard / third-person camera"),
            ("Right mouse drag", "Look around / orbit camera"),
            ("Mouse wheel", "Zoom"),
            ("F1", "Environment settings and diagnostics"),
            ("F2", "Show / hide this window")
        };
        foreach (var (keys, action) in rows)
        {
            var keyLabel = Label(keys, 18);
            keyLabel.Modulate = new(0.55f, 0.85f, 0.95f);
            grid.AddChild(keyLabel);
            grid.AddChild(Label(action, 18));
        }
        column.AddChild(Label("Release steering to centre the tiller.\nTrim, board and sailor position stay where you leave them.", 16));
        column.AddChild(Label("Sailing continues while this window is open.  ·  F2 to close", 16));
        return root;
    }

    private static Label Label(string text, int size)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
