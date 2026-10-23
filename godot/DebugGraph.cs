using Godot;

/// <summary>Bounded, timestamped observations of completed snapshots; rendering never advances time.</summary>
internal partial class DebugGraph : Control
{
    private readonly Queue<(double Time, double Value)> points = new();
    private readonly string caption;
    private readonly double minimumRange;
    private readonly double? reference;
    public int SampleCount => points.Count;

    public DebugGraph(string caption, double minimumRange, double? reference = null)
    {
        this.caption = caption;
        this.minimumRange = minimumRange;
        this.reference = reference;
        CustomMinimumSize = new(480, 110);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Add(double time, double value)
    {
        if (points.Count > 0 && time <= points.Last().Time) return;
        points.Enqueue((time, value));
        while (points.Count > 600 || points.Peek().Time < time - 10) points.Dequeue();
        if (IsVisibleInTree()) QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new(Vector2.Zero, Size), new(0.02f, 0.04f, 0.055f));
        var font = ThemeDB.FallbackFont;
        if (points.Count == 0) { DrawString(font, new(8, 20), caption + " — waiting", fontSize: 14); return; }
        var low = Math.Min(0, points.Min(p => p.Value));
        var high = Math.Max(minimumRange, points.Max(p => p.Value));
        if (reference.HasValue) high = Math.Max(high, reference.Value);
        var range = high - low;
        var latest = points.Last();
        Vector2 Position(double time, double value) => new(
            8 + (float)((time - latest.Time + 10) / 10) * (Size.X - 16),
            34 + (float)((high - value) / range) * (Size.Y - 42));
        if (reference.HasValue)
            DrawLine(Position(latest.Time - 10, reference.Value), Position(latest.Time, reference.Value), new(0.85f, 0.4f, 0.3f));
        var vertices = points.Select(p => Position(p.Time, p.Value)).ToArray();
        if (vertices.Length > 1) DrawPolyline(vertices, new(0.4f, 0.85f, 0.92f), 1.5f, true);
        DrawString(font, new(8, 19), $"{caption}   {latest.Value:F2}   [{low:F1}, {high:F1}]   · last 10 s", fontSize: 14);
    }
}
