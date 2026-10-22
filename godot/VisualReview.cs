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
    ["reach-starboard", "reach-port", "luff-a", "luff-b", "onboard-starboard", "onboard-port", "heel-wake", "run-before-jibe", "run-after-jibe", "calm-stopped", "controls-help"];

    private void StartVisualReview()
    {
        visualReview = true;
        SetReviewFixture();
    }

    private void SetReviewFixture()
    {
        var index = reviewIndex;
        controlsHelp.Visible = index == 10;
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
        boat.Render(state);
        water.Trail.Clear();
        if (index == 6)
        {
            for (var i = 0; i < 60; i++)
            {
                var t = i / 10.0;
                water.Trail.Update(state with { TimeSeconds = t, Boat = body with { PositionWorldM = body.PositionWorldM - velocity * (6 - t) } });
            }
        }
        water.Render(state, boat.Position);
        UpdateCamera();
        status.Text = "";
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
