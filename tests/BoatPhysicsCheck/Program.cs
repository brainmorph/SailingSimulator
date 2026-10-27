using SailingSimulator.Adapters;
using SailingSimulator.Core;
using static Check;

var boatModel = new IlcaBoatModel();
var masses = new IlcaMassModel();
var controls = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6, outhaul: 0.5, cunningham: 0.3);
var calm = Input(controls, 0);
var windy = Input(controls, 10);
var tests = new (string Name, Action Run)[]
{
    ("Hunter snapshots publish completed sail evaluations", () =>
    {
        var model = new Hunter235BoatModel();
        var simulation = new Hunter235BoatDefinition().CreateSimulation(windy);
        var initial = simulation.Latest;
        var initialRig = initial.HunterRig ?? throw new Exception("Initial Hunter sail state missing.");
        var initialSection = initialRig.Jib.Sections[0];
        var changed = Input(new SailingControls(mainsheet: 0.8, portJibSheet: 0.9, starboardJibSheet: 0.2), 10);
        var completed = simulation.Step(changed);
        var published = completed.HunterRig ?? throw new Exception("Completed Hunter sail state missing.");
        var expected = model.Rig.Inspect(new(completed.TimeSeconds, completed.Boat, completed.Mass!, changed, completed.Rig));
        Require(published.Main.Id == "main" && published.Jib.Id == "jib", "Sail identities are missing.");
        Require(published.Main.Sections.SequenceEqual(expected.Main.Sections) &&
            published.Jib.Sections.SequenceEqual(expected.Jib.Sections), "Sail sections must match the completed state.");
        Require(published.Total == expected.Total && published.Derivative == expected.Derivative,
            "Sail forces must match the completed state.");
        Require(published.Jib.StarboardLimitRad != initialRig.Jib.StarboardLimitRad,
            "Published sheet limit must use the sampled input.");
        Require(initialRig.Jib.Sections[0] == initialSection && initialRig.Jib.Sections is not SailSectionState[],
            "Earlier snapshots must retain their own immutable sections.");
        Require(new IlcaBoatDefinition().CreateSimulation(windy).Latest.HunterRig is null,
            "Other boats must not publish Hunter state.");
    }),
    ("Experimental Hunter composes forces and replays deterministically", () =>
    {
        var model = new Hunter235BoatModel();
        var state = model.Hull.CreateInitialState(windy);
        var mass = new Hunter235MassModel().Evaluate(windy.Controls);
        var e = new ForceEvaluation(0, state, mass, windy, model.InitialRig);
        var expected = model.Gravity.Evaluate(e) + model.Hull.Evaluate(e) + model.Foils.Centerboard.Evaluate(e)
            + model.Foils.Rudder.Evaluate(e) + model.Rig.Evaluate(e);
        var actual = model.EvaluateDynamics(e);
        Near(actual.Forces.ForceWorldN, expected.ForceWorldN, 1e-10);
        Near(actual.Forces.MomentBodyNm, expected.MomentBodyNm, 1e-10);
        IBoatDefinition definition = new Hunter235BoatDefinition();
        Require(definition.Id == "hunter-235", "Unexpected Hunter identity.");
        var first = definition.CreateSimulation(windy);
        var second = definition.CreateSimulation(windy);
        for (var i = 0; i < 120; i++)
        {
            var a = first.Step(windy);
            var b = second.Step(windy);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.Mass == b.Mass, "Hunter replay differs.");
        }
    }),
    ("Hunter independent sail forces and manual sheet limits", () =>
    {
        var rig = new Hunter235RigModel();
        var mass = new Hunter235MassModel().Evaluate(calm.Controls);
        var rest = new ForceEvaluation(0, RigidBodyState.AtRest, mass, calm);
        var still = rig.Inspect(rest);
        Near(still.Total.ForceWorldN, Vector3d.Zero, 0);
        Require(still.Main.Id == "main" && still.Jib.Id == "jib", "Sail identities must be stable.");
        Near(still.Main.Sections.Sum(s => s.AreaM2), 13.5, 0.06);
        Near(still.Jib.Sections.Sum(s => s.AreaM2), 8.425, 0.002);
        var wind = new EnvironmentConfig(new(3, 4, 0), WavePreset.Calm, WaveDirectionMode.Independent, Vector3d.UnitX, 1);
        var mirrorWind = new EnvironmentConfig(new(3, -4, 0), WavePreset.Calm, WaveDirectionMode.Independent, Vector3d.UnitX, 1);
        var a = rig.Inspect(rest with { Input = new(calm.Controls, wind), Rig = new(0.5, 0, 0.5, 0) });
        var b = rig.Inspect(rest with { Input = new(calm.Controls, mirrorWind), Rig = new(-0.5, 0, -0.5, 0) });
        Require(a.Main.Force.ForceWorldN.Length > 0 && a.Jib.Force.ForceWorldN.Length > 0, "Both sails must carry wind load.");
        Near(a.Total.ForceWorldN.X, b.Total.ForceWorldN.X, 1e-10);
        Near(a.Total.ForceWorldN.Y, -b.Total.ForceWorldN.Y, 1e-10);
        foreach (var sail in new[] { a.Main, a.Jib })
        {
            var sum = sail.Sections.Aggregate(default(ForceMoment), (acc, s) => acc + s.Force);
            Near(sum.ForceWorldN, sail.Force.ForceWorldN, 1e-10);
            Near(sum.MomentBodyNm, sail.Force.MomentBodyNm, 1e-10);
        }
        var tightPort = Input(new(portJibSheet: 1), 0);
        var tightStarboard = Input(new(starboardJibSheet: 1), 0);
        var angle = new RigState(0, 0, 0.5, 0);
        var port = rig.Inspect(rest with { Input = tightPort, Rig = angle });
        var starboard = rig.Inspect(rest with { Input = tightStarboard, Rig = angle });
        Require(port.Derivative.JibAngularAcceleration < 0 && starboard.Derivative.JibAngularAcceleration == 0,
            "Port sheet must restrain starboard opening independently.");
        var reverse = rig.Inspect(rest with { Input = tightStarboard, Rig = angle with { JibAngleRad = -0.5 } });
        Require(reverse.Derivative.JibAngularAcceleration > 0, "Starboard sheet must restrain port opening.");
        Require(a.Jib.Sections is not SailSectionState[], "Published sections must not expose a mutable array.");
    }),
    ("Hunter foils respond to deployment and steering", () =>
    {
        var foils = new Hunter235Foils();
        var hull = new Hunter235HullModel();
        var mass = new Hunter235MassModel();
        var boat = hull.CreateInitialState(calm) with { LinearVelocityWorldMps = new(2, 0.3, 0) };
        var lowered = new ForceEvaluation(0, boat, mass.Evaluate(calm.Controls), calm);
        var raisedInput = Input(new(centerboard: 0), 0);
        var raised = new ForceEvaluation(0, boat, mass.Evaluate(raisedInput.Controls), raisedInput);
        Require(foils.Centerboard.Evaluate(lowered).ForceWorldN.Length > 1, "Lowered board must resist leeway.");
        Near(foils.Centerboard.Evaluate(raised).ForceWorldN, Vector3d.Zero, 0);
        var leftInput = Input(new(tiller: -0.2), 0);
        var rightInput = Input(new(tiller: 0.2), 0);
        var straight = boat with { LinearVelocityWorldMps = new(2, 0, 0) };
        var left = foils.Rudder.Evaluate(new(0, straight, mass.Evaluate(leftInput.Controls), leftInput));
        var right = foils.Rudder.Evaluate(new(0, straight, mass.Evaluate(rightInput.Controls), rightInput));
        Require(left.MomentBodyNm.Z < 0 && right.MomentBodyNm.Z > 0, "Tiller must steer in both directions.");
    }),
    ("Hunter hull balances and restores in calm water", () =>
    {
        var hull = new Hunter235HullModel();
        var hunterMass = new Hunter235MassModel().Evaluate(calm.Controls);
        var boat = hull.CreateInitialState(calm);
        var evaluation = new ForceEvaluation(0, boat, hunterMass, calm);
        var buoyancy = hull.EvaluateHull(evaluation).Buoyancy;
        Near(buoyancy.ForceWorldN.Z + hunterMass.MassKg * 9.80665, 0, 1e-8);
        var deeper = evaluation with { Boat = boat with { PositionWorldM = boat.PositionWorldM + new Vector3d(0, 0, 0.03) } };
        Require(hull.EvaluateHull(deeper).Buoyancy.ForceWorldN.Z < buoyancy.ForceWorldN.Z, "Heave must restore upward.");
        foreach (var axis in new[] { Vector3d.UnitX, Vector3d.UnitY })
        {
            var tilted = evaluation with { Boat = boat with { OrientationBodyToWorld = Quaterniond.FromAxisAngle(axis, 0.03) } };
            var change = hull.EvaluateHull(tilted).Buoyancy.MomentBodyNm - buoyancy.MomentBodyNm;
            Require(Vector3d.Dot(change, axis) < 0, "Hull must restore a small tilt.");
        }
        var moving = evaluation with { Boat = boat with { LinearVelocityWorldMps = new(1, -0.5, 0.2), AngularVelocityBodyRadPerSec = new(0.1, -0.2, 0.3) } };
        var drag = hull.EvaluateHull(moving).Drag;
        Require(Vector3d.Dot(drag.ForceWorldN, moving.Boat.LinearVelocityWorldMps) + Vector3d.Dot(drag.MomentBodyNm, moving.Boat.AngularVelocityBodyRadPerSec) <= 0, "Hull drag must dissipate energy.");
        Require(hull.Samples.Count == 96, "Unexpected Hunter hull quadrature.");
    }),
    ("Hunter full ballast and cockpit mass budget", () =>
    {
        var hunter = new Hunter235MassModel();
        var lowered = new SailingControls(centerboard: 1, hiking: 1);
        var raised = new SailingControls(centerboard: 0, hiking: -1);
        var parts = hunter.Contributors(lowered);
        Near(parts.Where(c => c.Name != "Skipper").Sum(c => c.MassKg), 1360.77711, 1e-10);
        Near(hunter.Evaluate(lowered).MassKg, 1440.77711, 1e-10);
        Require(parts.Single(c => c.Name == "Water ballast") == hunter.Contributors(raised).Single(c => c.Name == "Water ballast"), "Board and seat movement must not change ballast.");
        Near(parts.Single(c => c.Name == "Skipper").CenterBodyM.Y, 0.55, 0);
        Near(hunter.Contributors(raised).Single(c => c.Name == "Skipper").CenterBodyM.Y, -0.55, 0);
        Require(hunter.Contributors(raised).Single(c => c.Name == "Centerboard").CenterBodyM.Z < parts.Single(c => c.Name == "Centerboard").CenterBodyM.Z, "Raised board must move upward.");
        var tensor = hunter.Evaluate(lowered).InertiaBodyKgM2;
        var vector = new Vector3d(1, -2, 3);
        Near(tensor.Solve(tensor.Multiply(vector)), vector, 1e-12);
    }),
    ("Boat definition preserves ILCA simulation and creates independent instances", () =>
    {
        IBoatDefinition definition = new IlcaBoatDefinition();
        Require(definition.Id == "ilca-7", "Unexpected boat identity.");
        var first = definition.CreateSimulation(windy, Math.PI / 2);
        var second = definition.CreateSimulation(windy, Math.PI / 2);
        var direct = new Simulation(boatModel.CreateInitialState(windy, Math.PI / 2), masses, boatModel, windy);
        for (var i = 0; i < 120; i++)
        {
            var a = first.Step(windy);
            var b = direct.Step(windy);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.Mass == b.Mass, "Factory changed ILCA behavior.");
        }
        Require(second.Latest.StepIndex == 0, "Simulations must have independent state.");
    }),
    ("Equipped budget, moving sailor/board and full inertia", () =>
    {
        var mass = masses.Evaluate(controls);
        Near(mass.MassKg, 138, 0);
        Near(masses.Contributors(controls).Where(c => c.Name != "Sailor").Sum(c => c.MassKg), 58, 0);
        var hiking = new SailingControls(hiking: 1);
        var hiked = masses.Evaluate(hiking);
        Near(masses.Contributors(hiking).Single(c => c.Name == "Sailor").CenterBodyM.Y, 0.85, 0);
        Near(hiked.CenterOfMassBodyM.Y, 80 * 0.85 / 138, 1e-14);
        Require(hiked.InertiaBodyKgM2.XY != 0 && hiked.InertiaBodyKgM2.YZ != 0, "Products of inertia missing.");
        var vector = new Vector3d(1, -2, 3);
        Near(hiked.InertiaBodyKgM2.Solve(hiked.InertiaBodyKgM2.Multiply(vector)), vector, 1e-13);
        Require(masses.Evaluate(new(centerboard: 0)).CenterOfMassBodyM.Z < mass.CenterOfMassBodyM.Z, "Raised board must move its mass up.");
        Throws<ArgumentException>(() => MassProperties.FromTensor(1, default, new(1, 1, 1, 2, 0, 0)));
        Throws<ArgumentException>(() => MassProperties.FromTensor(1, default, default));
        Throws<ArgumentException>(() => MassAggregation.Combine(Array.Empty<MassContributor>()));
        Throws<ArgumentException>(() => MassAggregation.Combine(new[] { new MassContributor("invalid", -1, default, default) }));
        var pair = MassAggregation.Combine(new[] {
            new MassContributor("port", 2, new(0, -1, 0), default),
            new MassContributor("starboard", 2, new(0, 1, 0), default),
            new MassContributor("center", 1, default, new(1, 1, 1, 0, 0, 0)) });
        Near(pair.MassKg, 5, 0);
        Near(pair.CenterOfMassBodyM, Vector3d.Zero, 0);
        Near(pair.InertiaBodyKgM2.XX, 5, 0);
        Near(pair.InertiaBodyKgM2.YY, 1, 0);
        Near(pair.InertiaBodyKgM2.ZZ, 5, 0);
    }),
    ("Tensor RK4 conserves torque-free angular momentum and energy", () =>
    {
        var mass = masses.Evaluate(new(hiking: 0.8));
        var omega = new Vector3d(0.1, 0.2, 0.3);
        var h0 = mass.InertiaBodyKgM2.Multiply(omega);
        var sim = new Simulation(RigidBodyState.AtRest with { AngularVelocityBodyRadPerSec = omega }, mass, new CompositeForceModel(), calm);
        for (var i = 0; i < 6000; i++) sim.Step(calm);
        var final = sim.Latest.Boat;
        var h1 = mass.InertiaBodyKgM2.Multiply(final.AngularVelocityBodyRadPerSec);
        Near(final.OrientationBodyToWorld.Rotate(h1), h0, 1e-7);
        Near(Vector3d.Dot(final.AngularVelocityBodyRadPerSec, h1), Vector3d.Dot(omega, h0), 1e-8);
    }),
    ("Internal repositioning conserves total momentum", () =>
    {
        var initial = RigidBodyState.AtRest with { LinearVelocityWorldMps = new(1, 2, 3), AngularVelocityBodyRadPerSec = new(0.1, 0.2, 0.3) };
        var sim = new Simulation(initial, masses, new CompositeForceModel(), calm);
        var h0 = sim.Latest.Mass!.InertiaBodyKgM2.Multiply(initial.AngularVelocityBodyRadPerSec);
        for (var i = 0; i < 120; i++)
            sim.Step(Input(new(hiking: (i + 1) / 120.0, centerboard: 1 - (i + 1) / 120.0), 0));
        var final = sim.Latest;
        Near(final.Boat.PositionWorldM, initial.LinearVelocityWorldMps * 2, 1e-12);
        Near(final.Boat.LinearVelocityWorldMps, initial.LinearVelocityWorldMps, 0);
        Near(final.Boat.OrientationBodyToWorld.Rotate(final.Mass!.InertiaBodyKgM2.Multiply(final.Boat.AngularVelocityBodyRadPerSec)), h0, 1e-7);
    }),
    ("Boom uses coupled RK4 stages and failed rig/diagnostics are atomic", () =>
    {
        var tensor = new MassProperties(138, new(100, 200, 200));
        var rigModel = new FunctionRig(e => new(e.Rig.BoomRateRadPerSec, e.TimeSeconds));
        var sim = new Simulation(RigidBodyState.AtRest, tensor, rigModel, calm);
        for (var i = 0; i < 60; i++) sim.Step(calm);
        Near(sim.Latest.Rig.BoomRateRadPerSec, 0.5, 1e-14);
        Near(sim.Latest.Rig.BoomAngleRad, 1.0 / 6, 1e-14);
        var broken = new Simulation(RigidBodyState.AtRest, tensor,
            new FunctionRig(e => new(0, e.TimeSeconds > 0 ? double.NaN : 0)), calm);
        var before = broken.Latest;
        try { broken.Step(calm); throw new InvalidOperationException("Expected rig failure."); }
        catch (SimulationStepException exception) { Require(exception.Stage == "k2", "Rig error must identify its stage."); }
        Require(ReferenceEquals(before, broken.Latest), "Rig failure published partial state.");
        var brokenDiagnostics = new Simulation(boatModel.CreateInitialState(calm), masses, new FailingDiagnostics(boatModel), calm);
        before = brokenDiagnostics.Latest;
        try { brokenDiagnostics.Step(calm); throw new InvalidOperationException("Expected diagnostic failure."); }
        catch (SimulationStepException exception) { Require(exception.Stage == "completion", "Diagnostic error must identify completion."); }
        Require(ReferenceEquals(before, brokenDiagnostics.Latest), "Diagnostic failure published partial state.");
    }),
    ("Jib state participates in coupled RK4 and rejects non-finite stages", () =>
    {
        var tensor = new MassProperties(138, new(100, 200, 200));
        // Boom acceleration is time; jib acceleration follows the evolving boom rate.
        var sim = new Simulation(RigidBodyState.AtRest, tensor,
            new FunctionRig(e => new(e.Rig.BoomRateRadPerSec, e.TimeSeconds,
                e.Rig.JibRateRadPerSec, e.Rig.BoomRateRadPerSec)), calm);
        for (var i = 0; i < 60; i++) sim.Step(calm);
        Near(sim.Latest.Rig.JibRateRadPerSec, 1.0 / 6, 1e-14);
        Near(sim.Latest.Rig.JibAngleRad, 1.0 / 24, 1e-14);
        var broken = new Simulation(RigidBodyState.AtRest, tensor,
            new FunctionRig(e => new(0, 0, 0, e.TimeSeconds > 0 ? double.NaN : 0)), calm);
        var before = broken.Latest;
        try { broken.Step(calm); throw new InvalidOperationException("Expected jib failure."); }
        catch (SimulationStepException exception) { Require(exception.Stage == "k2", "Jib error must identify its stage."); }
        Require(ReferenceEquals(before, broken.Latest), "Jib failure published partial state.");
    }),
    ("Hydrostatic balance, heave/roll/pitch restoring and hiking sign", () =>
    {
        var state = boatModel.CreateInitialState(calm);
        var e = Evaluation(state, calm);
        Near((boatModel.Gravity.Evaluate(e) + boatModel.Hull.Evaluate(e)).ForceWorldN.Z, 0, 1e-9);
        var deeper = e with { Boat = state with { PositionWorldM = state.PositionWorldM + new Vector3d(0, 0, 0.03) } };
        Require((boatModel.Gravity.Evaluate(deeper) + boatModel.Hull.Evaluate(deeper)).ForceWorldN.Z < 0, "Heave must restore upwards.");
        foreach (var axis in new[] { Vector3d.UnitX, Vector3d.UnitY })
        {
            var tilted = e with { Boat = state with { OrientationBodyToWorld = Quaterniond.FromAxisAngle(axis, 0.03) } };
            Require(Vector3d.Dot(boatModel.Hull.Evaluate(tilted).MomentBodyNm, axis) < 0, "Heel/pitch moment must oppose tilt.");
        }
        var hiked = e with { Mass = masses.Evaluate(new(hiking: 1)) };
        Require(boatModel.Hull.Evaluate(hiked).MomentBodyNm.X > 100, "Starboard sailor should right a port heel.");
        var airborne = e with { Boat = state with { PositionWorldM = new(0, 0, -10) } };
        Near(boatModel.Hull.Evaluate(airborne).ForceWorldN, default, 0);
        Near(boatModel.Centerboard.Evaluate(airborne).ForceWorldN, default, 0);
        Near(boatModel.Rudder.Evaluate(airborne).ForceWorldN, default, 0);
    }),
    ("Hull and foils dissipate water-relative motion", () =>
    {
        var baseState = boatModel.CreateInitialState(calm);
        foreach (var velocity in new[] { new Vector3d(3, 0.3, 0.2), new Vector3d(-3, -0.5, -0.2), new Vector3d(0, 1, 0) })
        {
            var e = Evaluation(baseState with { LinearVelocityWorldMps = velocity, AngularVelocityBodyRadPerSec = new(0.1, -0.2, 0.3) }, calm);
            foreach (var force in new[] { boatModel.Hull.EvaluateHull(e).Drag, boatModel.Centerboard.Evaluate(e), boatModel.Rudder.Evaluate(e) })
                Require(Vector3d.Dot(force.ForceWorldN, velocity) + Vector3d.Dot(force.MomentBodyNm, e.Boat.AngularVelocityBodyRadPerSec) <= 1e-10,
                    "Passive water force added kinetic energy.");
        }
    }),
    ("Foil leeway, board deployment and continuous rudder steering", () =>
    {
        var state = boatModel.CreateInitialState(calm) with { LinearVelocityWorldMps = new(3, 0.2, 0) };
        var e = Evaluation(state, calm);
        Require(boatModel.Centerboard.Evaluate(e).ForceWorldN.Y < 0, "Centerboard should oppose starboard leeway.");
        var raised = Evaluation(state, Input(new(centerboard: 0), 0));
        Near(boatModel.Centerboard.Evaluate(raised).ForceWorldN, default, 0);
        var straight = state with { LinearVelocityWorldMps = new(3, 0, 0) };
        var left = boatModel.Rudder.Evaluate(Evaluation(straight, Input(new(tiller: -0.2), 0)));
        var right = boatModel.Rudder.Evaluate(Evaluation(straight, Input(new(tiller: 0.2), 0)));
        Require(left.MomentBodyNm.Z < 0 && right.MomentBodyNm.Z > 0, "Tiller sign mismatch.");
        Near(left.MomentBodyNm.Z, -right.MomentBodyNm.Z, 1e-12);
        var small = boatModel.Rudder.Evaluate(Evaluation(straight, Input(new(tiller: 0.001), 0)));
        Require(small.MomentBodyNm.Z > 0 && small.MomentBodyNm.Z < right.MomentBodyNm.Z / 50, "Small steering must have a small response.");
    }),
    ("Shared sectional sail state, apparent wind, luffing and all trim controls", () =>
    {
        var state = boatModel.CreateInitialState(windy);
        var e = Evaluation(state, windy) with { Rig = new(0.5, 0) };
        var shape = boatModel.Sail.Shape(e);
        Near(shape.Sections.Sum(s => s.AreaM2), 7.06, 1e-14);
        var summed = shape.Sections.Aggregate(default(ForceMoment), (sum, s) => sum + s.Force);
        Near(summed.ForceWorldN, boatModel.Sail.Evaluate(e).ForceWorldN, 0);
        Near(shape.Sections[0].ApparentWindBodyMps, windy.Environment.TrueWindVelocityWorldMps, 1e-14);
        var flat = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 1, outhaul: 1, cunningham: 1);
        var flattened = boatModel.Sail.Shape(Evaluation(state, Input(flat, 10)) with { Rig = e.Rig });
        Require(flattened.Sections[^1].CamberRatio < shape.Sections[^1].CamberRatio &&
            Math.Abs(flattened.Sections[^1].TwistRad) < Math.Abs(shape.Sections[^1].TwistRad), "Trim must flatten and reduce twist.");
        var outhaul = boatModel.Sail.Shape(Evaluation(state, Input(new(outhaul: 1), 10)) with { Rig = e.Rig });
        var eased = boatModel.Sail.Shape(Evaluation(state, Input(new(), 10)) with { Rig = e.Rig });
        var cunningham = boatModel.Sail.Shape(Evaluation(state, Input(new(cunningham: 1), 10)) with { Rig = e.Rig });
        Require(outhaul.Sections[0].CamberRatio < eased.Sections[0].CamberRatio &&
            cunningham.Sections[0].CamberRatio < eased.Sections[0].CamberRatio, "Outhaul/cunningham are inactive.");
        Require(boatModel.Sail.SheetLimit(new(mainsheet: 1)) < boatModel.Sail.SheetLimit(new()), "Sheet must constrain opening.");
        Require(boatModel.Sail.SheetLimit(new(traveler: 1)) < boatModel.Sail.SheetLimit(new()), "Traveler must constrain opening.");
        // Align apparent wind exactly with the untwisted lower strip's chord: it must luff.
        var luffControls = new SailingControls(vang: 1, cunningham: 1);
        var luffInput = Input(luffControls, 10, 0);
        var luff = Evaluation(state, luffInput) with { Rig = default };
        var drawing = luff with { Rig = new(0.4, 0) };
        Require(boatModel.Sail.Shape(luff).Sections[0].Luffing > 0.999, "Aligned sail should visibly luff.");
        Require(boatModel.Sail.Evaluate(luff).ForceWorldN.Length < boatModel.Sail.Evaluate(drawing).ForceWorldN.Length * 0.01,
            "Luffing did not reduce force.");
        var moving = e with { Boat = state with { LinearVelocityWorldMps = new(1, 2, 0) } };
        Near(boatModel.Sail.Shape(moving).Sections[0].ApparentWindBodyMps, windy.Environment.TrueWindVelocityWorldMps - new Vector3d(1, 2, 0), 1e-14);
    }),
    ("Boom crosses continuously under reversed airflow", () =>
    {
        // Hold the hull in this subsystem check. RK4 still integrates boom/rig, never scripts a maneuver.
        var frozen = new FrozenHullRig(boatModel);
        var firstInput = Input(new(mainsheet: 0.8, vang: 0.5), 10);
        var sim = new Simulation(RigidBodyState.AtRest, new MassProperties(138, new(100, 200, 200)), frozen, firstInput);
        for (var i = 0; i < 300; i++) sim.Step(firstInput);
        Require(sim.Latest.Rig.BoomAngleRad > 0.2, "Wind should open boom to starboard.");
        var reverse = Input(firstInput.Controls, 10, 90);
        var crossed = false;
        for (var i = 0; i < 600; i++)
        {
            var old = sim.Latest.Rig.BoomAngleRad;
            var angle = sim.Step(reverse).Rig.BoomAngleRad;
            Require(Math.Abs(angle - old) < 0.15, "Boom side snapped.");
            crossed |= angle < -0.2;
        }
        Require(crossed, "Boom did not cross in opposite airflow.");
    }),
    ("Snapshot diagnostics, determinism and gradual input replay", () =>
    {
        var first = Make(windy);
        var second = Make(windy);
        var original = first.Latest;
        var linearSeen = Vector3d.Zero;
        var angularSeen = Vector3d.Zero;
        for (var i = 0; i < 1800; i++)
        {
            var c = new SailingControls(tiller: 0.1 * Math.Sin(i * 0.003), mainsheet: 0.5 + 0.2 * Math.Sin(i * 0.002),
                traveler: 0.8, vang: 0.6, outhaul: 0.5, cunningham: 0.3, hiking: -0.6 * Math.Sin(i * 0.001), centerboard: 0.8);
            var input = Input(c, 10);
            var a = first.Step(input);
            var b = second.Step(input);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.Mass == b.Mass, "Replay differs.");
            var diagnostics = a.Diagnostics!;
            var evaluation = new ForceEvaluation(a.TimeSeconds, a.Boat, a.Mass!, input, a.Rig);
            Require(diagnostics.Total == boatModel.Evaluate(evaluation), "Diagnostics differ from completed-state forces.");
            Require(diagnostics.SailShape.Sections.SequenceEqual(b.Diagnostics!.SailShape.Sections), "Sail output differs.");
            Require(ReferenceEquals(a.Input, input), "Snapshot input mismatch.");
            var v = a.Boat.LinearVelocityWorldMps;
            var w = a.Boat.AngularVelocityBodyRadPerSec;
            linearSeen = new(Math.Max(linearSeen.X, Math.Abs(v.X)), Math.Max(linearSeen.Y, Math.Abs(v.Y)), Math.Max(linearSeen.Z, Math.Abs(v.Z)));
            angularSeen = new(Math.Max(angularSeen.X, Math.Abs(w.X)), Math.Max(angularSeen.Y, Math.Abs(w.Y)), Math.Max(angularSeen.Z, Math.Abs(w.Z)));
        }
        Near(original.Diagnostics!.SailShape.BoomAngleRad, 0.1, 0);
        var sectionList = (IList<SailSectionState>)original.Diagnostics.SailShape.Sections;
        Throws<NotSupportedException>(() => sectionList[0] = default);
        Require((first.Latest.Boat.PositionWorldM - original.Boat.PositionWorldM).Length > 1, "Boat did not sail.");
        Require(linearSeen.X > 0.01 && linearSeen.Y > 0.01 && linearSeen.Z > 0.001 &&
            angularSeen.X > 0.001 && angularSeen.Y > 0.001 && angularSeen.Z > 0.001, "Not all six degrees responded.");
    }),
    ("Steering develops a tack and jibe through ordinary controls", () =>
    {
        foreach (var isTack in new[] { true, false })
        {
            var c = new SailingControls(mainsheet: isTack ? 0.8 : 0.2, traveler: 0.8, vang: 0.7);
            var input = Input(c, 10, 0);
            var heading = isTack ? Math.PI / 3 : 2 * Math.PI / 3;
            var initial = boatModel.CreateInitialState(input, heading);
            initial = initial with { LinearVelocityWorldMps = initial.OrientationBodyToWorld.Rotate(new(3, 0, 0)) };
            var sim = new Simulation(initial, masses, boatModel, input);
            var crossedWind = false;
            var crossedSail = false;
            var hiking = 0.0;
            var sheet = c.Mainsheet;
            for (var i = 0; i < 2700; i++)
            {
                // A gradual hiking feedback controller stands in for the sailor balancing heel.
                var attitude = PresentationConversions.GetAttitude(sim.Latest.Boat.OrientationBodyToWorld);
                var targetHiking = Math.Clamp(-0.7 * attitude.Heel - 0.3 * sim.Latest.Boat.AngularVelocityBodyRadPerSec.X, -0.8, 0.8);
                hiking += Math.Clamp(targetHiking - hiking, -0.5 * Simulation.StepSeconds, 0.5 * Simulation.StepSeconds);
                // Sheet in approaching dead downwind, as a sailor would to bring the boom across.
                if (!isTack && attitude.Heading > 2.6)
                    sheet += Math.Clamp(0.95 - sheet, -Simulation.StepSeconds, Simulation.StepSeconds);
                var sampled = Input(new(tiller: isTack ? -0.45 : 0.25, mainsheet: sheet, traveler: c.Traveler,
                    vang: c.Vang, hiking: hiking), 10, 0);
                var state = sim.Step(sampled);
                var headingAfter = PresentationConversions.GetAttitude(state.Boat.OrientationBodyToWorld).Heading;
                crossedWind |= isTack ? Math.Cos(headingAfter) > 0.98 : Math.Cos(headingAfter) < -0.98;
                crossedSail |= crossedWind && state.Rig.BoomAngleRad < -0.15;
                if (crossedSail)
                {
                    Require(Math.Abs(PresentationConversions.GetAttitude(state.Boat.OrientationBodyToWorld).Heel) < 1,
                        "Maneuver must cross while upright.");
                    Console.WriteLine($"  {(isTack ? "Tack" : "Jibe")}: crossed at {state.TimeSeconds:F2} s, heading {headingAfter:F3} rad, boom {state.Rig.BoomAngleRad:F3} rad");
                    break;
                }
            }
            Require(crossedWind && crossedSail, $"{(isTack ? "Tack" : "Jibe")} did not cross wind and boom; final rig {sim.Latest.Rig}, heading {PresentationConversions.GetAttitude(sim.Latest.Boat.OrientationBodyToWorld).Heading}.");
        }
    }),
    ("Fixed 60 Hz stability across still-water wind and control cases", () =>
    {
        var cases = new[]
        {
            (0.0, 0.0, new SailingControls()),
            (10.0, 0.0, controls),
            (10.0, Math.PI / 3, new SailingControls(mainsheet: 0.85, vang: 0.8, hiking: -0.6)),
            (10.0, -Math.PI / 3, new SailingControls(mainsheet: 0.85, vang: 0.8, hiking: 0.6)),
            (10.0, -Math.PI / 2, new SailingControls(mainsheet: 0, tiller: 0.2, centerboard: 0.3)),
            (30.0, 0.0, new SailingControls(mainsheet: 1, vang: 1, outhaul: 1, cunningham: 1, hiking: -1)),
            (30.0, Math.PI / 2, new SailingControls(mainsheet: 0, tiller: -1, centerboard: 0)),
            (10.0, Math.PI, new SailingControls(tiller: 1, hiking: 1, centerboard: 0))
        };
        foreach (var (speed, heading, c) in cases)
        {
            var input = Input(c, speed);
            var sim = Make(input, heading);
            for (var i = 0; i < 7200; i++)
            {
                var state = sim.Step(input);
                Require(state.Boat.LinearVelocityWorldMps.Length < 30 && state.Boat.AngularVelocityBodyRadPerSec.Length < 20 &&
                    Math.Abs(state.Boat.PositionWorldM.Z) < 3, $"Unbounded motion at {speed} knots, heading {heading}, step {i}.");
                Near(state.Boat.OrientationBodyToWorld.LengthSquared, 1, 1e-13);
            }
            Console.WriteLine($"  {speed:F0} kn heading {heading:F2}: speed {sim.Latest.Boat.LinearVelocityWorldMps.Length:F3} m/s, z {sim.Latest.Boat.PositionWorldM.Z:F3} m");
            if (speed == 0)
            {
                Require(sim.Latest.Boat.LinearVelocityWorldMps.Length < 0.01, "Flat-water rest did not settle.");
                Require(sim.Latest.Boat.AngularVelocityBodyRadPerSec.Length < 0.01, "Rest attitude did not settle.");
            }
        }
    })
};

var selected = args.Length == 0 ? tests : tests.Where(t => t.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();
Require(selected.Length > 0, "No matching regression group.");
foreach (var (name, run) in selected) { run(); Console.WriteLine($"PASS {name}"); }
Console.WriteLine($"Phase 3 boat physics: {selected.Length} regression groups passed.");

SimulationInput Input(SailingControls c, double knots, double from = 270) => new(c,
    PresentationConversions.EnvironmentFromSettings(windSpeedKnots: knots, windFromDegrees: from, wavePreset: WavePreset.Calm, waveSeed: 1));
ForceEvaluation Evaluation(RigidBodyState state, SimulationInput input) => new(0, state, masses.Evaluate(input.Controls), input);
Simulation Make(SimulationInput input, double heading = 0) => new(boatModel.CreateInitialState(input, heading), masses, boatModel, input);

static class Check
{
    public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Near(double actual, double expected, double tolerance) => Require(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance,
        $"Expected {expected:R} +/- {tolerance:R}, got {actual:R}.");
    public static void Near(Vector3d actual, Vector3d expected, double tolerance) => Near((actual - expected).Length, 0, tolerance);
    public static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}

sealed class FrozenHullRig(IlcaBoatModel model) : IRigDynamicsModel
{
    public RigState InitialRig => model.InitialRig;
    public ForceMoment Evaluate(in ForceEvaluation e) => default;
    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e) => (default, model.EvaluateDynamics(e).Rig);
}

sealed class FunctionRig(Func<ForceEvaluation, RigDerivative> evaluate) : IRigDynamicsModel
{
    public RigState InitialRig => default;
    public ForceMoment Evaluate(in ForceEvaluation e) => default;
    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e) => (default, evaluate(e));
}

sealed class FailingDiagnostics(IlcaBoatModel model) : IRigDynamicsModel, IBoatDiagnosticsModel
{
    public RigState InitialRig => model.InitialRig;
    public ForceMoment Evaluate(in ForceEvaluation e) => model.Evaluate(e);
    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e) => model.EvaluateDynamics(e);
    public BoatDiagnostics Inspect(in ForceEvaluation e) => e.TimeSeconds == 0 ? model.Inspect(e)
        : throw new ArithmeticException("Synthetic diagnostic failure.");
}
