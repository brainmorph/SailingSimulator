using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;
using static SailingSimulator.Adapters.PresentationConversions;

/// <summary>Main-thread view of the same completed snapshot as the boat. Widgets publish immutable settings.</summary>
internal partial class DebugOverlay : PanelContainer
{
    private readonly Label warning = TextLabel("");
    private readonly Label telemetry = TextLabel("");
    private readonly Label forces = TextLabel("");
    private readonly Label performance = TextLabel("");
    private readonly Label active = TextLabel("");
    private readonly DebugGraph speed = new("Speed · kn", 1);
    private readonly DebugGraph heel = new("Heel · deg", 10);
    private readonly DebugGraph step = new("Step · ms (red = 16.67)", 20, 1000.0 / 60);
    private readonly DebugGraph late = new("Deadline lateness · ms", 1);
    internal TabContainer Tabs { get; private set; } = null!;
    internal HSlider WindSpeed { get; private set; } = null!;
    internal HSlider WindDirection { get; private set; } = null!;
    internal HSlider WaveDirection { get; private set; } = null!;
    internal OptionButton Strength { get; private set; } = null!;
    internal CheckButton Align { get; private set; } = null!;
    internal SpinBox Seed { get; private set; } = null!;
    internal DebugEnvironmentSettings Settings { get; private set; } = new();
    internal long DisplayedStep { get; private set; } = -1;
    internal int GraphSamples => speed.SampleCount;
    internal string ActiveText => active.Text;
    internal string WarningText => warning.Text;
    private DebugEnvironmentSettings? saved;
    private Action<EnvironmentConfig>? publish;
    private bool syncing;
    private long lastObserved = -1;
    private double refresh;
    private double renderSeconds;
    private int renderFrames;
    private double renderFps;
    private double maxFrameMs;
    private double displayedMaxFrameMs;

    public void Build()
    {
        Name = "DebugOverlay";
        Visible = false;
        OffsetLeft = -624; OffsetRight = -16; OffsetTop = 16; OffsetBottom = -48;
        AnchorLeft = 1; AnchorRight = 1; AnchorBottom = 1;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new(0.025f, 0.055f, 0.075f, 0.97f),
            BorderColor = new(0.24f, 0.48f, 0.55f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10
        });
        var column = new VBoxContainer();
        AddChild(column);
        var title = TextLabel("Development · F1 to close · Sailing continues");
        title.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(title);
        column.AddChild(warning);
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        Tabs = tabs;
        column.AddChild(tabs);
        var values = Page(tabs, "Telemetry");
        values.AddChild(telemetry);
        values.AddChild(speed);
        values.AddChild(heel);
        values.AddChild(TextLabel("Forces: world N/E/down (N) · moments: body forward/starboard/down (N m)"));
        values.AddChild(forces);
        var settings = Page(tabs, "Environment");
        settings.AddChild(TextLabel("Compass FROM bearings · 0° N · 90° E · 180° S · 270° W · 360° N"));
        WindSpeed = Slider(settings, "Wind speed · kn", 30, 0.1, v => Edit(Settings with { WindSpeedKnots = v }));
        WindDirection = Slider(settings, "Wind from · deg", 360, 1, v => Edit(Settings with { WindFromDegrees = v }));
        Align = new CheckButton { Text = "Align waves with wind", FocusMode = FocusModeEnum.None };
        settings.AddChild(Align);
        Align.Toggled += v =>
        {
            if (!syncing) Edit(Settings with { AlignWaves = v,
                WaveFromDegrees = v ? Settings.WaveFromDegrees : Settings.WindFromDegrees });
        };
        WaveDirection = Slider(settings, "Independent waves from · deg", 360, 1, v => Edit(Settings with { WaveFromDegrees = v }));
        Strength = new OptionButton { FocusMode = FocusModeEnum.None };
        foreach (var preset in Enum.GetValues<WavePreset>()) Strength.AddItem(preset.ToString(), (int)preset);
        settings.AddChild(TextLabel("Wave strength"));
        settings.AddChild(Strength);
        Strength.ItemSelected += i => { if (!syncing) Edit(Settings with { WavePreset = (WavePreset)i }); };
        settings.AddChild(TextLabel("Wave realization seed · exact signed integer"));
        Seed = new SpinBox { MinValue = int.MinValue, MaxValue = int.MaxValue, Step = 1, AllowGreater = false, AllowLesser = false };
        settings.AddChild(Seed);
        Button(settings, "Apply seed", () => { Seed.Apply(); Edit(Settings with { WaveSeed = checked((int)Seed.Value) }); });
        Button(settings, "New random seed", () => Edit(Settings with { WaveSeed = Random.Shared.Next(int.MinValue, int.MaxValue) }));
        Button(settings, "Save these conditions", () => { saved = Settings; UpdateSavedLabel(); });
        Button(settings, "Restore saved conditions", () => { if (saved is not null) Edit(saved); });
        Button(settings, "Default conditions (keep seed)", () => Edit(new(WaveSeed: Settings.WaveSeed)));
        settings.AddChild(active);
        settings.AddChild(TextLabel("Seed edits apply on the next completed step.\nSaved conditions last for this session and restore wind, waves and seed.\nThey do not rewind boat position, trim or simulation time."));
        var timing = Page(tabs, "Performance");
        timing.AddChild(performance);
        timing.AddChild(step);
        timing.AddChild(late);
        timing.AddChild(TextLabel("300 completed steps · nearest-rank percentiles, including warm-up.\nHull/foil/sail timings include wave sampling and nest inside RK4/inspection.\nGC counts and heap are process-wide; allocation rate is worker-only.\nDeadlines stay on the original 60 Hz grid. No catch-up or time reset."));
    }

    public void Initialize(EnvironmentConfig environment, Action<EnvironmentConfig> changed)
    {
        publish = changed;
        Settings = DebugEnvironmentSettings.FromEnvironment(environment);
        SyncWidgets();
    }

    internal void Edit(DebugEnvironmentSettings settings)
    {
        var environment = settings.Build(); // Validate before changing either UI or published input.
        Settings = settings;
        SyncWidgets();
        publish?.Invoke(environment);
    }

    private void SyncWidgets()
    {
        syncing = true;
        WindSpeed.Value = Settings.WindSpeedKnots;
        WindDirection.Value = Settings.WindFromDegrees;
        Align.ButtonPressed = Settings.AlignWaves;
        WaveDirection.Value = Settings.AlignWaves ? Settings.WindFromDegrees : Settings.WaveFromDegrees;
        WaveDirection.Editable = !Settings.AlignWaves;
        Strength.Select((int)Settings.WavePreset);
        Seed.Value = Settings.WaveSeed;
        syncing = false;
    }

    private string SavedLabel => saved is null ? "No saved conditions" : $"Saved seed {saved.WaveSeed}, {saved.WindSpeedKnots:F1} kn from {saved.WindFromDegrees:F0}°";
    private void UpdateSavedLabel() => active.Text = SavedLabel + "\nWaiting for next completed snapshot…";

    public void Observe(RunnerSnapshot snapshot, double delta)
    {
        renderFrames++;
        renderSeconds += delta;
        maxFrameMs = Math.Max(maxFrameMs, delta * 1000);
        if (renderSeconds >= 1)
        {
            renderFps = renderFrames / renderSeconds;
            displayedMaxFrameMs = maxFrameMs;
            renderFrames = 0; renderSeconds = 0; maxFrameMs = 0;
        }
        var state = snapshot.State;
        if (state.StepIndex != lastObserved)
        {
            speed.Add(state.TimeSeconds, MetersPerSecondToKnots(Horizontal(state.Boat.LinearVelocityWorldMps)));
            heel.Add(state.TimeSeconds, RadiansToDegrees(GetAttitude(state.Boat.OrientationBodyToWorld).Heel));
            step.Add(state.TimeSeconds, snapshot.Timing.StepMilliseconds);
            late.Add(state.TimeSeconds, snapshot.Timing.DeadlineLatenessMilliseconds);
            lastObserved = state.StepIndex;
        }
        refresh -= delta;
        if (!Visible || refresh > 0) return;
        refresh = 0.1;
        DisplayedStep = state.StepIndex;
        var t = snapshot.Timing;
        warning.Text = snapshot.Fault is { } fault ? $"STOPPED: {fault.Message}"
            : $"{(snapshot.Running ? "Running" : "Stopped")} · Deadline: {t.Warning} · consecutive {t.ConsecutiveLateSteps} · total {t.TotalLateSteps}";
        warning.Modulate = snapshot.Fault is not null || t.Warning == DeadlineWarning.Hard ? new(1, 0.45f, 0.4f)
            : t.Warning == DeadlineWarning.Soft ? new(1, 0.8f, 0.3f) : Colors.White;
        var b = state.Boat;
        var attitude = GetAttitude(b.OrientationBodyToWorld);
        var bodyVelocity = b.OrientationBodyToWorld.Conjugate().Rotate(b.LinearVelocityWorldMps);
        var windBody = b.OrientationBodyToWorld.Conjugate().Rotate(state.Input.Environment.TrueWindVelocityWorldMps - b.LinearVelocityWorldMps);
        var c = state.Input.Controls;
        telemetry.Text = $"Step {state.StepIndex} · simulation {state.TimeSeconds:F2} s · wall {t.ElapsedSeconds:F2} s\n" +
            $"Speed {MetersPerSecondToKnots(Horizontal(b.LinearVelocityWorldMps)):F2} kn · heading {RadiansToDegrees(attitude.Heading):F1}°\n" +
            $"Heel {RadiansToDegrees(attitude.Heel):F1}° · pitch {RadiansToDegrees(attitude.Pitch):F1}° · leeway {RadiansToDegrees(Math.Atan2(bodyVelocity.Y, bodyVelocity.X)):F1}°\n" +
            $"Apparent wind at COM {MetersPerSecondToKnots(windBody.Length):F2} kn · from {RadiansToDegrees(Math.Atan2(-windBody.Y, -windBody.X)):F1}° body\n" +
            $"Position N/E/down · m  {Vector(b.PositionWorldM)}\nVelocity N/E/down · m/s  {Vector(b.LinearVelocityWorldMps)}\n" +
            $"Angular velocity body · deg/s  {Vector(b.AngularVelocityBodyRadPerSec * (180 / Math.PI))}\n" +
            $"Boom {RadiansToDegrees(state.Rig.BoomAngleRad):F1}° · rate {RadiansToDegrees(state.Rig.BoomRateRadPerSec):F1}°/s\n" +
            $"Tiller {c.Tiller:F2} · sheet {c.Mainsheet:F2} · traveler {c.Traveler:F2} · vang {c.Vang:F2}\n" +
            $"Outhaul {c.Outhaul:F2} · cunningham {c.Cunningham:F2} · board {c.Centerboard:F2} · hiking {c.Hiking:F2}";
        if (state.Diagnostics is { } d)
        {
            forces.Text = string.Join("\n", new[] { ForceRow("Gravity", d.Gravity), ForceRow("Buoyancy", d.Buoyancy),
                ForceRow("Hull drag", d.HullDrag), ForceRow("Centerboard", d.Centerboard), ForceRow("Rudder", d.Rudder),
                ForceRow("Sail", d.Sail), ForceRow("Total", d.Total) }) + "\n" +
                string.Join("\n", d.SailShape.Sections.Select((s, i) => $"Strip {i + 1}: angle {RadiansToDegrees(s.AngleRad):F1}° · twist {RadiansToDegrees(s.TwistRad):F1}° · camber {s.CamberRatio:F3} · luff {s.Luffing:F2}"));
        }
        var e = state.Input.Environment;
        var windFrom = e.TrueWindVelocityWorldMps.Length > 0
            ? $"from {DebugEnvironmentSettings.FromBearing(e.TrueWindVelocityWorldMps):F0}°" : "calm";
        active.Text = $"Active step {state.StepIndex}: wind {MetersPerSecondToKnots(e.TrueWindVelocityWorldMps.Length):F1} kn · " +
            $"{windFrom}\n{e.WavePreset} · {e.WaveDirectionMode} · waves from {DebugEnvironmentSettings.FromBearing(e.WaveTravelDirectionWorld):F0}°\nSeed {e.WaveSeed} · {SavedLabel}";
        if (snapshot.Performance is { } p)
        {
            var s = p.Subsystems;
            performance.Text = $"Step {t.StepMilliseconds:F3} ms · deadline lateness {t.DeadlineLatenessMilliseconds:F3} ms\n" +
                $"Compute p50 {p.P50Ms:F3} · p95 {p.P95Ms:F3} · p99 {p.P99Ms:F3} · max {p.MaxMs:F3} ms ({p.Samples} steps)\n" +
                $"Mass {s.MassMs:F3} · RK4 {s.Rk4Ms:F3} · inspection {s.DiagnosticsMs:F3} ms\n" +
                $"Hull + water {s.HullMs:F3} · board {s.CenterboardMs:F3} · rudder {s.RudderMs:F3} · sail {s.SailMs:F3} ms\n" +
                $"Worker allocation {p.AllocatedBytes:N0} B/interval · rolling {p.AllocationBytesPerSecond / 1024:F1} KiB/s\n" +
                $"Process GC since start: gen0 {p.Gen0Collections} · gen1 {p.Gen1Collections} · gen2 {p.Gen2Collections}\n" +
                $"Managed heap {p.ManagedHeapBytes / 1048576.0:F1} MiB\nRender {renderFps:F1} FPS · max frame {displayedMaxFrameMs:F1} ms (last ~1 s)";
        }
    }

    private static double Horizontal(Vector3d v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
    private static string Vector(Vector3d v) => $"({v.X:F2}, {v.Y:F2}, {v.Z:F2})";
    private static string ForceRow(string name, ForceMoment f) => $"{name}: F {Vector(f.ForceWorldN)} · M {Vector(f.MomentBodyNm)}";
    private static Label TextLabel(string text) => new()
    {
        Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill
    };
    private static VBoxContainer Page(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        return column;
    }
    private HSlider Slider(VBoxContainer parent, string name, double max, double increment, Action<double> changed)
    {
        var label = TextLabel($"{name}  0.0");
        parent.AddChild(label);
        var slider = new HSlider { MinValue = 0, MaxValue = max, Step = increment, FocusMode = FocusModeEnum.None };
        parent.AddChild(slider);
        slider.ValueChanged += v => { label.Text = $"{name}  {v:F1}"; if (!syncing) changed(v); };
        return slider;
    }
    private static void Button(VBoxContainer parent, string title, Action clicked)
    {
        var button = new Button { Text = title, FocusMode = FocusModeEnum.None };
        button.Pressed += clicked;
        parent.AddChild(button);
    }
}
