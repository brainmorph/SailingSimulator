using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

public partial class SailingScene
{
    private bool visualReview;
    private bool capturing;
    private int reviewFrame;
    private int reviewIndex;
    private readonly string[] reviewNames =
    ["reach-starboard", "reach-port", "luff-a", "luff-b", "onboard-starboard", "onboard-port", "heel-wake", "run-before-jibe", "run-after-jibe", "calm-stopped", "controls-help", "controls-help-hunter",
     "hunter-reach-starboard", "hunter-reach-port", "hunter-onboard", "hunter-held-old-sheet", "hunter-head-to-wind", "hunter-debug-telemetry"];

    private void StartVisualReview()
    {
        visualReview = true;
        SetReviewFixture();
    }

    private void SetReviewFixture()
    {
        var index = reviewIndex;
        controlsHelp.Visible = index is 10 or 11;
        ilcaBoat.Visible = index < 12;
        hunterBoat.Visible = index >= 12;
        activeBoat = index < 12 ? ilcaBoat : hunterBoat;
        view = index < 12 ? ilcaBoat : hunterBoat;
        if (index >= 12)
        {
            SetHunterFixture(index);
            return;
        }
        // The Hunter list is shown over the ILCA fixture; only the help panel is under review.
        ControlsHelp.Show(controlsHelp, index == 11 ? BoatChoice.Hunter235 : BoatChoice.Ilca7);
        var port = index is 1 or 5 or 8;
        var sign = port ? -1 : 1;
        var luff = index is 2 or 3;
        onboard = index is 4 or 5;
        onboardYaw = port ? -0.1f : 0.1f;
        onboardPitch = 0.08f;
        cameraYaw = port ? 0.75f : -0.75f;
        cameraPitch = 0.28f;
        cameraDistance = 10.5f;
        var controls = new SailingControls(mainsheet: 0.5, traveler: 0.8, vang: 0.5, outhaul: 0.5,
            cunningham: 0.3, hiking: -sign * 0.7, centerboard: index == 9 ? 0 : 1);
        var downwind = index is 7 or 8;
        environment = PresentationConversions.EnvironmentFromSettings(waveSeed: 123,
            windSpeedKnots: index == 9 ? 0 : 10,
            wavePreset: index == 9 ? WavePreset.Calm : WavePreset.Gentle);
        var input = new SimulationInput(controls, environment);
        var mass = new IlcaMassModel().Evaluate(controls);
        var heading = luff || index == 9 ? 0 : downwind ? Math.PI : sign * Math.PI / 2;
        var orientation = Quaterniond.FromAxisAngle(Vector3d.UnitZ, heading) *
            Quaterniond.FromAxisAngle(Vector3d.UnitX, index == 6 ? 0.4 : sign * 0.08);
        var rig = new RigState(luff ? 0 : sign * (downwind ? 1.25 : 0.8), 0);
        var time = index == 3 ? 6.07 : 6;
        var velocity = luff || index == 9 ? Vector3d.Zero : orientation.Rotate(new(3, 0, 0));
        // Instantaneous fixtures, not calibrated steady sailing. Use real force diagnostics for every sail.
        var body = new RigidBodyState(new Vector3d(0, 0, -0.20) + orientation.Rotate(mass.CenterOfMassBodyM), orientation, velocity, default);
        var diagnostics = new IlcaBoatModel().Inspect(new(time, body, mass, input, rig));
        var state = new SimulationState(index, time, body, input) { Mass = mass, Diagnostics = diagnostics, Rig = rig };
        ilcaBoat.Render(state);
        water.Trail.Clear();
        water.WakeStartHalfWidthM = ilcaBoat.WakeStartHalfWidthM;
        if (index == 6)
        {
            for (var i = 0; i < 60; i++)
            {
                var t = i / 10.0;
                water.Trail.Update(state with { TimeSeconds = t, Boat = body with { PositionWorldM = body.PositionWorldM - velocity * (6 - t) } });
            }
        }
        water.Render(state, ilcaBoat.Position);
        UpdateCamera();
        debug.Visible = false;
    }

    /// <summary>
    /// Hunter fixtures are short offline runs of the real model from the session start, not frozen
    /// hand-made states, so sails, sheets and cues are the completed physics state.
    /// </summary>
    private void SetHunterFixture(int index)
    {
        var telemetry = index == 17;
        var port = index == 13;
        var backwind = index == 15;
        var headToWind = index == 16;
        onboard = index == 14;
        onboardYaw = -0.15f;
        onboardPitch = 0.12f;
        cameraYaw = backwind ? -2.6f : port ? 0.75f : -0.75f;
        cameraPitch = 0.22f;
        cameraDistance = backwind ? 10 : hunterBoat.CameraDistance;
        environment = PresentationConversions.EnvironmentFromSettings(waveSeed: 123);
        var start = BoatCatalog.InitialControls(BoatChoice.Hunter235);
        var controls = port
            ? new SailingControls(mainsheet: start.Mainsheet, traveler: start.Traveler, vang: start.Vang, outhaul: start.Outhaul,
                cunningham: start.Cunningham, hiking: 1, portJibSheet: start.StarboardJibSheet)
            // Close-hauled trim with the starboard jib sheet fully in, never released through the tack.
            : backwind ? new SailingControls(mainsheet: 0.9, traveler: start.Traveler, vang: start.Vang, outhaul: start.Outhaul,
                cunningham: start.Cunningham, hiking: start.Hiking, starboardJibSheet: 1)
            : start;
        var heading = headToWind ? 0 : port ? -Math.PI / 2 : SailingSession.StartHeadingRad;
        var simulation = BoatCatalog.Definition(BoatChoice.Hunter235).CreateSimulation(new(controls, environment), heading);
        if (backwind)
        {
            // Tack from the session start without touching the sheets: three seconds after the bow
            // crosses the wind, the old (starboard) jib sheet still holds the clew.
            for (var i = 0; i < 600; i++) simulation.Step(new(controls, environment));
            var crossed = double.PositiveInfinity;
            while (simulation.Latest.TimeSeconds < crossed + 3)
            {
                Require(simulation.Latest.TimeSeconds < 60, "backwind fixture tacks");
                controls = new HeldSailingInput(tiller: -1).Advance(controls);
                var stepped = simulation.Step(new(controls, environment));
                if (double.IsInfinity(crossed) && Math.IEEERemainder(PresentationConversions.GetAttitude(stepped.Boat.OrientationBodyToWorld).Heading, 2 * Math.PI) < 0)
                    crossed = stepped.TimeSeconds;
            }
        }
        water.Trail.Clear();
        water.WakeStartHalfWidthM = hunterBoat.WakeStartHalfWidthM;
        if (!backwind)
        {
            // The wake comes from the same completed states, from the Hunter's published stern.
            for (var i = 0; i < (headToWind ? 120 : 900); i++)
            {
                var stepped = simulation.Step(new(controls, environment));
                if (i >= 480) water.Trail.Update(stepped);
            }
        }
        var state = simulation.Latest;
        hunterBoat.Render(state);
        water.Render(state, hunterBoat.Position);
        UpdateCamera();
        debug.Visible = telemetry;
        if (telemetry)
        {
            debug.Tabs.CurrentTab = 0;
            debug.Observe(new RunnerSnapshot(state, new StepTiming(state.StepIndex, state.TimeSeconds, 0, 0, 0, 0), false), 0.2);
        }
        var rig = state.HunterRig!;
        // Bellies follow each strip's local flow; report how many jib strips are bellied toward the windward side.
        var windFromStarboard = state.Boat.OrientationBodyToWorld.Conjugate().Rotate(environment.TrueWindVelocityWorldMps).Y < 0;
        var windwardBellies = SailingVisuals.HunterStrips(rig.Jib).Skip(1).SkipLast(1)
            .Count(strip => Math.Sign(Vector3d.Dot(strip.Section.ApparentWindBodyMps, strip.Normal)) * strip.Normal.Y > 0 == windFromStarboard);
        GD.Print($"REVIEW {reviewNames[index]}: speed {state.Boat.LinearVelocityWorldMps.Length:F2} m/s, jib {state.Rig.JibAngleRad:F2} rad, " +
            $"luff main/jib {rig.Main.Sections.Average(s => s.Luffing):F2}/{rig.Jib.Sections.Average(s => s.Luffing):F2}, " +
            $"jib strips bellied to windward {windwardBellies}/{rig.Jib.Sections.Count}, t {state.TimeSeconds:F1} s");
    }

    private async void ProcessVisualReview()
    {
        if (capturing || ++reviewFrame < 12) return;
        capturing = true;
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var result = GetViewport().GetTexture().GetImage().SavePng($"/tmp/SailingSimulator-{reviewNames[reviewIndex]}.png");
            Require(result == Error.Ok, "review screenshot saved");
            GD.Print($"REVIEW {reviewNames[reviewIndex]}");
            if (++reviewIndex == reviewNames.Length)
            {
                GD.Print("PASS visual review capture");
                GetTree().Quit();
                return;
            }
            reviewFrame = 0;
            SetReviewFixture();
            capturing = false;
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL visual review: {exception.Message}");
            GetTree().Quit(1);
        }
    }
}
