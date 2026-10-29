using Godot;
using SailingSimulator.Adapters;

/// <summary>Shown before the first session; no simulation runs until a boat is chosen.</summary>
internal static class BoatPicker
{
    public static Control Create(Action<BoatChoice> chosen)
    {
        var root = new CenterContainer { Name = "BoatPicker", Visible = false };
        var panel = new PanelContainer();
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
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);
        column.AddChild(Label("Choose a boat", 26));
        foreach (var boat in BoatCatalog.All)
        {
            var button = new Button { Text = BoatCatalog.Name(boat), CustomMinimumSize = new(320, 48) };
            button.AddThemeFontSizeOverride("font_size", 20);
            button.Pressed += () => chosen(boat);
            column.AddChild(button);
        }
        column.AddChild(Label("Change boats later in F1 · Session. Changing restarts the session.", 16));
        return root;
    }

    private static Label Label(string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
