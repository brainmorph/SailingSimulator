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
    ("Experimental Hunter sheet geometry pulls toward its lead and goes slack", () =>
    {
        var port = Hunter235SheetExperiment.Evaluate(true, 1, 0);
        var starboard = Hunter235SheetExperiment.Evaluate(false, 1, 0);
        Require(port.LengthM > port.SlackLengthM && port.ForceOnClewBodyN.Y < 0 &&
            port.TorqueAboutForestayNm < 0, "A tight port sheet must pull the centered clew to port.");
        Require(starboard.LengthM > starboard.SlackLengthM && starboard.ForceOnClewBodyN.Y > 0 &&
            starboard.TorqueAboutForestayNm > 0, "A tight starboard sheet must pull the centered clew to starboard.");
        Near(port.TorqueAboutForestayNm, -starboard.TorqueAboutForestayNm, 1e-10);
        Near(port.ForceOnClewBodyN.X, starboard.ForceOnClewBodyN.X, 1e-10);
        Near(port.ForceOnClewBodyN.Y, -starboard.ForceOnClewBodyN.Y, 1e-10);
        foreach (var onPort in new[] { true, false })
        {
            var taut = Hunter235SheetExperiment.Evaluate(onPort, 1, onPort ? 0.5 : -0.5);
            // The rope applies equal and opposite collinear forces at its two ends.
            var totalMoment = Vector3d.Cross(taut.ClewBodyM, taut.ForceOnClewBodyN) +
                Vector3d.Cross(taut.LeadBodyM, -taut.ForceOnClewBodyN);
            Near(totalMoment, Vector3d.Zero, 1e-10);
            Near((taut.ClewBodyM - taut.LeadBodyM).Length, taut.LengthM, 1e-12);
            var eased = Hunter235SheetExperiment.Evaluate(onPort, 0, 0);
            Near(eased.ForceOnClewBodyN, Vector3d.Zero, 0);
            Near(eased.TorqueAboutForestayNm, 0, 0);
            var onOwnSide = Hunter235SheetExperiment.Evaluate(onPort, 1, onPort ? -0.25 : 0.25);
            Near(onOwnSide.ForceOnClewBodyN, Vector3d.Zero, 0);
            Throws<ArgumentOutOfRangeException>(() => Hunter235SheetExperiment.Evaluate(onPort, -0.01, 0));
            Throws<ArgumentOutOfRangeException>(() => Hunter235SheetExperiment.Evaluate(onPort, 0, double.NaN));
            // Torque must be the negative derivative of spring energy with angle.
            const double delta = 1e-6;
            var angle = onPort ? 0.5 : -0.5;
            double Energy(double a)
            {
                var sheet = Hunter235SheetExperiment.Evaluate(onPort, 1, a);
                var extension = Math.Max(0, sheet.LengthM - sheet.SlackLengthM);
                return 0.5 * 1000 * extension * extension;
            }
            var gradient = (Energy(angle + delta) - Energy(angle - delta)) / (2 * delta);
            Near(Hunter235SheetExperiment.Evaluate(onPort, 1, angle).TorqueAboutForestayNm, -gradient, 1e-5);
        }
        // Equal tight sheets pull against each other without a net centerline torque.
        Near(port.TorqueAboutForestayNm + starboard.TorqueAboutForestayNm, 0, 1e-10);
    }),
    ("Experimental sheet resists a jib crossing under reversed airflow", () =>
    {
        // Prescribed, stationary hull and wind isolate the jib rather than simulate a tack.
        var rig = new Hunter235RigModel();
        var c = new SailingControls();
        var mass = new Hunter235MassModel().Evaluate(c);
        (double Angle, double Rate) Advance((double Angle, double Rate) state, double oldTrim, double side)
        {
            var wind = new EnvironmentConfig(new(2, -2 * side, 0), WavePreset.Calm,
                WaveDirectionMode.Independent, Vector3d.UnitX, 1);
            var eval = new ForceEvaluation(0, RigidBodyState.AtRest, mass, new(c, wind));
            (double Angle, double Rate) Derivative((double Angle, double Rate) s)
            {
                var sail = rig.Inspect(eval with { Rig = new(0, 0, s.Angle, s.Rate) }).Jib;
                var oldSheet = Hunter235SheetExperiment.Evaluate(side < 0, oldTrim, s.Angle);
                var otherSheet = Hunter235SheetExperiment.Evaluate(side > 0, 0, s.Angle);
                return (s.Rate, (sail.TorqueNm + oldSheet.TorqueAboutForestayNm +
                    otherSheet.TorqueAboutForestayNm - 80 * s.Rate) / 40);
            }
            const double dt = 1.0 / 60;
            var k1 = Derivative(state);
            var k2 = Derivative((state.Angle + dt * k1.Angle / 2, state.Rate + dt * k1.Rate / 2));
            var k3 = Derivative((state.Angle + dt * k2.Angle / 2, state.Rate + dt * k2.Rate / 2));
            var k4 = Derivative((state.Angle + dt * k3.Angle, state.Rate + dt * k3.Rate));
            return (state.Angle + dt * (k1.Angle + 2 * k2.Angle + 2 * k3.Angle + k4.Angle) / 6,
                state.Rate + dt * (k1.Rate + 2 * k2.Rate + 2 * k3.Rate + k4.Rate) / 6);
        }
        foreach (var side in new[] { -1.0, 1.0 })
        {
            var released = (Angle: 0.4 * side, Rate: 0.0);
            var held = released;
            var minimumHeldOpening = 0.4;
            for (var i = 0; i < 600; i++)
            {
                released = Advance(released, 0, side);
                held = Advance(held, 1, side);
                Require(double.IsFinite(released.Angle) && double.IsFinite(released.Rate) &&
                    double.IsFinite(held.Angle) && double.IsFinite(held.Rate), "Jib motion must remain finite.");
                minimumHeldOpening = Math.Min(minimumHeldOpening, side * held.Angle);
            }
            Console.WriteLine($"  Fixed-hull side {side:+0;-0}, 10 s: released {released.Angle:F3} rad, held {held.Angle:F3} rad, minimum held opening {minimumHeldOpening:F3} rad");
            Require(side * released.Angle < 0 && minimumHeldOpening > 0,
                "The held old sheet must retain the jib on its side while easing permits crossing.");
            for (var i = 0; i < 600; i++)
            {
                // Match the 0.25/s held-input easing rate over four seconds.
                var oldTrim = Math.Max(0, 1 - (i + 1) / 240.0);
                held = Advance(held, oldTrim, side);
                Require(double.IsFinite(held.Angle) && double.IsFinite(held.Rate),
                    "Jib motion must remain finite after releasing the sheet.");
            }
            Console.WriteLine($"  After gradually releasing the held sheet: jib {held.Angle:F3} rad");
            Require(side * held.Angle < -0.1,
                "Releasing the old sheet must allow the previously held jib to cross.");
        }
    }),
    ("Hunter steady reach settles and replays exactly", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        // Fixed helm represents a held rudder, not the host's spring-return input.
        var c = new SailingControls(tiller: -0.1, mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, portJibSheet: 0.65);
        var input = new SimulationInput(c, env);
        var first = new Hunter235BoatDefinition().CreateSimulation(input, Math.PI / 2);
        var second = new Hunter235BoatDefinition().CreateSimulation(input, Math.PI / 2);
        var minSpeed = double.PositiveInfinity; var maxSpeed = 0.0;
        var minHeading = double.PositiveInfinity; var maxHeading = double.NegativeInfinity;
        var maxHeel = 0.0; var maxLuff = 0.0;
        for (var i = 0; i < 3600; i++)
        {
            var a = first.Step(input);
            var b = second.Step(input);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.HunterRig!.Total == b.HunterRig!.Total,
                $"Steady reach replay differs at step {i + 1}.");
            if (i < 2400) continue;
            var attitude = PresentationConversions.GetAttitude(a.Boat.OrientationBodyToWorld);
            var speed = a.Boat.LinearVelocityWorldMps.Length;
            minSpeed = Math.Min(minSpeed, speed); maxSpeed = Math.Max(maxSpeed, speed);
            minHeading = Math.Min(minHeading, attitude.Heading); maxHeading = Math.Max(maxHeading, attitude.Heading);
            maxHeel = Math.Max(maxHeel, Math.Abs(attitude.Heel));
            maxLuff = Math.Max(maxLuff, Math.Max(a.HunterRig!.Main.Sections.Average(s => s.Luffing),
                a.HunterRig.Jib.Sections.Average(s => s.Luffing)));
        }
        Console.WriteLine($"  40–60 s: speed {minSpeed:F3}–{maxSpeed:F3} m/s, heading {minHeading * 180 / Math.PI:F2}–{maxHeading * 180 / Math.PI:F2} deg, max heel {maxHeel * 180 / Math.PI:F2} deg, max sail luff {maxLuff:F3}");
        Require(minSpeed > 1 && maxSpeed - minSpeed < 0.02 &&
            maxHeading - minHeading < Math.PI / 180 && maxHeel < Math.PI / 4 && maxLuff < 0.1,
            "Reach failed the scenario's settling, upright or sail-filling criteria.");
    }),
    ("Hunter scripted tack and control movements replay exactly", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        var controls = HunterDevelopmentScenario.InitialControls;
        var first = new Hunter235BoatDefinition().CreateSimulation(new(controls, env), Math.PI / 2);
        var second = new Hunter235BoatDefinition().CreateSimulation(new(controls, env), Math.PI / 2);
        var crossedWind = false;
        var crossedSails = false;
        for (long step = 0; step < HunterDevelopmentScenario.EndStep; step++)
        {
            controls = HunterDevelopmentScenario.Input(step).Advance(controls);
            var input = new SimulationInput(controls, env);
            var a = first.Step(input);
            var b = second.Step(input);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.Mass == b.Mass &&
                a.HunterRig!.Total == b.HunterRig!.Total, $"Replay differs at step {step + 1}.");
            var attitude = PresentationConversions.GetAttitude(a.Boat.OrientationBodyToWorld);
            if (HunterDevelopmentScenario.Stage(step) == 3 && attitude.Heading > Math.PI &&
                Math.Abs(attitude.Heel) < Math.PI / 2)
                crossedWind = true;
            if (crossedWind && a.Rig.BoomAngleRad < -0.1 && a.Rig.JibAngleRad < -0.1)
                crossedSails = true;
            if (step == 3239)
            {
                var rig = a.HunterRig!;
                Require(rig.Main.Sections.Average(s => s.Luffing) < 0.1 &&
                    rig.Jib.Sections.Average(s => s.Luffing) < 0.1 &&
                    a.Boat.LinearVelocityWorldMps.Length > 1.0,
                    "Both sails must fill and the boat regain speed after the turn.");
            }
        }
        Require(crossedWind, "Hunter did not cross the upwind heading while upright.");
        Require(crossedSails, "Both sails did not cross after the boat crossed the wind.");
        Require(controls.PortJibSheet < 0.01 && controls.StarboardJibSheet > 0.7 &&
            controls.Centerboard > 0.99 && controls.Hiking < -0.99, "Scripted controls did not reach expected positions.");
    }),
    ("Hunter tack comparison preserves the unreleased old sheet input", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        // The initial jib opens to starboard, so the starboard lead is the old side.
        var released = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 0.65);
        var held = released;
        var releasedBoat = new Hunter235BoatDefinition().CreateSimulation(new(released, env), Math.PI / 2);
        var heldBoat = new Hunter235BoatDefinition().CreateSimulation(new(held, env), Math.PI / 2);
        var jibAtSheetChange = 0.0;
        for (long step = 0; step < 3240; step++)
        {
            if (step == 1200) jibAtSheetChange = heldBoat.Latest.Rig.JibAngleRad;
            var changingSheets = HunterDevelopmentScenario.Stage(step) == 2;
            var releasedInput = changingSheets
                ? new HeldSailingInput(tiller: -0.6, portJibSheet: 1, starboardJibSheet: -1)
                : HunterDevelopmentScenario.Input(step);
            released = releasedInput.Advance(released);
            // Keep the old starboard sheet fixed while trimming the new port sheet.
            var heldInput = changingSheets
                ? new HeldSailingInput(tiller: -0.6, portJibSheet: 1)
                : HunterDevelopmentScenario.Input(step);
            held = heldInput.Advance(held);
            releasedBoat.Step(new(released, env));
            heldBoat.Step(new(held, env));
        }
        Require(jibAtSheetChange > 0.1, "The old starboard sheet must start with the jib on its side.");
        Require(released.StarboardJibSheet < 0.01 && held.StarboardJibSheet == 0.65 &&
            held.PortJibSheet > 0.7, "The comparison must differ in old-sheet release only.");
        var releasedEnd = releasedBoat.Latest;
        var heldEnd = heldBoat.Latest;
        Console.WriteLine($"  Jib at sheet change {jibAtSheetChange:F3} rad; at 54 s released {releasedEnd.Rig.JibAngleRad:F3} rad, held {heldEnd.Rig.JibAngleRad:F3} rad; held limits {heldEnd.HunterRig!.Jib.PortLimitRad:F3}/{heldEnd.HunterRig.Jib.StarboardLimitRad:F3} rad; held jib luff {heldEnd.HunterRig.Jib.Sections.Average(s => s.Luffing):F3}");
    }),
    ("Experimental sheet geometry runs a full-boat tack comparison", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        var released = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 0.65);
        var held = released;
        Simulation Create(SailingControls c)
        {
            var model = new Hunter235BoatModel(new Hunter235RigModel(experimentalSheetGeometry: true));
            var input = new SimulationInput(c, env);
            return new Simulation(model.Hull.CreateInitialState(input, Math.PI / 2), new Hunter235MassModel(), model, input);
        }
        var releasedBoat = Create(released);
        var heldBoat = Create(held);
        var initialJib = 0.0;
        for (long step = 0; step < 3240; step++)
        {
            if (step == 1200) initialJib = heldBoat.Latest.Rig.JibAngleRad;
            var changing = HunterDevelopmentScenario.Stage(step) == 2;
            released = (changing ? new HeldSailingInput(tiller: -0.6, portJibSheet: 1, starboardJibSheet: -1)
                : HunterDevelopmentScenario.Input(step)).Advance(released);
            held = (changing ? new HeldSailingInput(tiller: -0.6, portJibSheet: 1)
                : HunterDevelopmentScenario.Input(step)).Advance(held);
            releasedBoat.Step(new(released, env));
            heldBoat.Step(new(held, env));
        }
        var a = releasedBoat.Latest;
        var b = heldBoat.Latest;
        Console.WriteLine($"  Experimental tack: jib at sheet change {initialJib:F3} rad; released {a.Rig.JibAngleRad:F3}, held {b.Rig.JibAngleRad:F3} rad; headings {PresentationConversions.GetAttitude(a.Boat.OrientationBodyToWorld).Heading * 180 / Math.PI:F1}/{PresentationConversions.GetAttitude(b.Boat.OrientationBodyToWorld).Heading * 180 / Math.PI:F1} deg");
        Require(initialJib > 0.1 && released.StarboardJibSheet < 0.01 && held.StarboardJibSheet == 0.65,
            "The scenario must start the sheet change with the old starboard sheet on the jib side.");
        Require(a.HunterRig!.ExperimentalSheetGeometry && b.HunterRig!.ExperimentalSheetGeometry,
            "Completed diagnostics must identify the experimental sheet model.");
        Require(b.Rig.JibAngleRad - a.Rig.JibAngleRad > 0.1,
            "The retained sheet should change the jib position in this comparison.");
        foreach (var snapshot in new[] { a, b })
        {
            var attitude = PresentationConversions.GetAttitude(snapshot.Boat.OrientationBodyToWorld);
            Require(attitude.Heading > Math.PI && Math.Abs(attitude.Heel) < Math.PI / 2,
                "Both experimental runs must cross the upwind heading upright.");
        }
    }),
    ("Experimental tight old sheet encounters opposing airflow in a full-boat turn", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        var released = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 1);
        var held = released;
        Simulation Create()
        {
            var model = new Hunter235BoatModel(new Hunter235RigModel(experimentalSheetGeometry: true));
            var input = new SimulationInput(released, env);
            return new Simulation(model.Hull.CreateInitialState(input, Math.PI / 2), new Hunter235MassModel(), model, input);
        }
        var releasedBoat = Create();
        var heldBoat = Create();
        var opposingSteps = 0;
        var currentRun = 0;
        var longestRun = 0;
        var jibAtChange = 0.0;
        for (long step = 0; step < 3240; step++)
        {
            if (step == 1200) jibAtChange = heldBoat.Latest.Rig.JibAngleRad;
            var steering = step >= 240 && step < 2640 ? -0.6 : 0;
            var easing = step >= 1200 && step < 1440 ? -1 : 0;
            released = new HeldSailingInput(tiller: steering, starboardJibSheet: easing).Advance(released);
            held = new HeldSailingInput(tiller: steering).Advance(held);
            releasedBoat.Step(new(released, env));
            var snapshot = heldBoat.Step(new(held, env));
            var attitude = PresentationConversions.GetAttitude(snapshot.Boat.OrientationBodyToWorld);
            if (attitude.Heading > Math.PI && Math.Abs(attitude.Heel) < Math.PI / 2 &&
                snapshot.Rig.JibAngleRad > 0 && snapshot.HunterRig!.Jib.TorqueNm < -1)
            {
                opposingSteps++;
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else currentRun = 0;
        }
        var a = releasedBoat.Latest;
        var b = heldBoat.Latest;
        Console.WriteLine($"  Tight-sheet turn: jib at change {jibAtChange:F3} rad; released {a.Rig.JibAngleRad:F3}, held {b.Rig.JibAngleRad:F3} rad; held opposing-load steps {opposingSteps}, longest run {longestRun}; headings {PresentationConversions.GetAttitude(a.Boat.OrientationBodyToWorld).Heading * 180 / Math.PI:F1}/{PresentationConversions.GetAttitude(b.Boat.OrientationBodyToWorld).Heading * 180 / Math.PI:F1} deg");
        Require(jibAtChange > 0.1 && released.StarboardJibSheet < 0.01 && held.StarboardJibSheet == 1 &&
            held.PortJibSheet == 0 && released.PortJibSheet == 0,
            "The old sheet must start tight on the jib side and be released only in the comparison run.");
        Require(longestRun >= 60, "The held jib must encounter opposing aerodynamic torque for at least one second after the boat crosses the wind.");
        Require(b.Rig.JibAngleRad - a.Rig.JibAngleRad > 0.2,
            "The held sheet must reduce crossing compared with the released sheet in this scenario.");
    }),
    ("Experimental held-sheet turn mirrors across port and starboard", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        var starboard = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 1);
        var port = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, portJibSheet: 1);
        Simulation Create(SailingControls controls, double side)
        {
            var model = new Hunter235BoatModel(new Hunter235RigModel(experimentalSheetGeometry: true), experimentalRigMomentum: true);
            var input = new SimulationInput(controls, env);
            return new Simulation(model.Hull.CreateInitialState(input, side * Math.PI / 2),
                new Hunter235MassModel(), new HunterInitialRig(model, side), input);
        }
        var a = Create(starboard, 1);
        var b = Create(port, -1);
        var opposingPortSteps = 0;
        var currentOpposing = 0;
        var longestOpposing = 0;
        for (long step = 0; step < 3240; step++)
        {
            var steer = step >= 240 && step < 2640 ? -0.6 : 0;
            starboard = new HeldSailingInput(tiller: steer).Advance(starboard);
            port = new HeldSailingInput(tiller: -steer).Advance(port);
            var x = a.Step(new(starboard, env));
            var y = b.Step(new(port, env));
            Near(x.Rig.JibAngleRad, -y.Rig.JibAngleRad, 1e-7);
            Near(x.Rig.BoomAngleRad, -y.Rig.BoomAngleRad, 1e-7);
            Near(x.Boat.PositionWorldM.X, y.Boat.PositionWorldM.X, 1e-7);
            Near(x.Boat.PositionWorldM.Y, -y.Boat.PositionWorldM.Y, 1e-7);
            Near(x.Boat.PositionWorldM.Z, y.Boat.PositionWorldM.Z, 1e-7);
            Near(x.Boat.LinearVelocityWorldMps.X, y.Boat.LinearVelocityWorldMps.X, 1e-7);
            Near(x.Boat.LinearVelocityWorldMps.Y, -y.Boat.LinearVelocityWorldMps.Y, 1e-7);
            Near(x.Boat.LinearVelocityWorldMps.Z, y.Boat.LinearVelocityWorldMps.Z, 1e-7);
            Near(x.HunterRig!.Jib.TorqueNm, -y.HunterRig!.Jib.TorqueNm, 1e-6);
            var attitude = PresentationConversions.GetAttitude(y.Boat.OrientationBodyToWorld);
            if (attitude.Heading < Math.PI && Math.Abs(attitude.Heel) < Math.PI / 2 &&
                y.Rig.JibAngleRad < 0 && y.HunterRig!.Jib.TorqueNm > 1)
            {
                opposingPortSteps++;
                longestOpposing = Math.Max(longestOpposing, ++currentOpposing);
            }
            else currentOpposing = 0;
        }
        Console.WriteLine($"  Mirrored held-sheet turn: port opposing-load steps {opposingPortSteps}, longest run {longestOpposing}; final jib angles {a.Latest.Rig.JibAngleRad:F3}/{b.Latest.Rig.JibAngleRad:F3} rad");
        Require(longestOpposing >= 60, "The mirrored port-side jib must encounter opposing load for at least one second after crossing the wind.");
    }),
    ("Experimental rig momentum reaction conserves a closed-system test", () =>
    {
        // Isolate the internal spring and a single relative rig degree of freedom.
        var input = Input(new SailingControls(starboardJibSheet: 1), 0);
        var mass = new Hunter235MassModel().Evaluate(input.Controls);
        var uncoupled = new Simulation(RigidBodyState.AtRest, mass, new SheetMomentumExperiment(0), input);
        var reactionOnly = new Simulation(RigidBodyState.AtRest, mass, new SheetMomentumExperiment(1), input);
        var coupled = new Simulation(RigidBodyState.AtRest, mass, new SheetMomentumExperiment(2), input);
        var axis = SheetMomentumExperiment.Axis;
        Vector3d Momentum(SimulationState s) => s.Boat.OrientationBodyToWorld.Rotate(
            mass.InertiaBodyKgM2.Multiply(s.Boat.AngularVelocityBodyRadPerSec) + axis * (40 * s.Rig.JibRateRadPerSec));
        double Energy(SimulationState s)
        {
            var omega = s.Boat.AngularVelocityBodyRadPerSec;
            var rate = s.Rig.JibRateRadPerSec;
            var sheet = Hunter235SheetExperiment.Evaluate(false, 1, s.Rig.JibAngleRad);
            var extension = Math.Max(0, sheet.LengthM - sheet.SlackLengthM);
            return 0.5 * Vector3d.Dot(omega, mass.InertiaBodyKgM2.Multiply(omega)) +
                Vector3d.Dot(omega, axis) * (40 * rate) + 0.5 * 40 * rate * rate + 0.5 * 1000 * extension * extension;
        }
        var initialEnergy = Energy(coupled.Latest);
        var maxUncoupledMomentum = 0.0;
        var maxCoupledMomentum = 0.0;
        var maxEnergyChange = 0.0;
        var reactionEnergyChange = 0.0;
        for (var i = 0; i < 600; i++)
        {
            var a = uncoupled.Step(input);
            var r = reactionOnly.Step(input);
            var b = coupled.Step(input);
            maxUncoupledMomentum = Math.Max(maxUncoupledMomentum, Momentum(a).Length);
            maxCoupledMomentum = Math.Max(maxCoupledMomentum, Momentum(b).Length);
            maxEnergyChange = Math.Max(maxEnergyChange, Math.Abs(Energy(b) - initialEnergy));
            reactionEnergyChange = Math.Max(reactionEnergyChange, Math.Abs(Energy(r) - initialEnergy));
        }
        Console.WriteLine($"  Closed sheet test: max angular momentum without/with coupling {maxUncoupledMomentum:F6}/{maxCoupledMomentum:F6} kg m²/s; max energy change reaction-only/coupled {reactionEnergyChange:F6}/{maxEnergyChange:F6} J (initial {initialEnergy:F6} J)");
        Require(maxUncoupledMomentum > 1 && maxCoupledMomentum < 1e-5 &&
            reactionEnergyChange > 0.01 && maxEnergyChange < 0.001,
            "The coupled trial must preserve momentum and keep energy drift below 0.001 J in the isolated example.");
    }),
    ("Experimental two-sail momentum coupling conserves a closed-system test", () =>
    {
        var input = Input(new SailingControls(starboardJibSheet: 1), 0);
        var mass = new Hunter235MassModel().Evaluate(input.Controls);
        var simulation = new Simulation(RigidBodyState.AtRest, mass, new DualRigMomentumExperiment(), input);
        var boomAxis = -Vector3d.UnitZ;
        var jibAxis = SheetMomentumExperiment.Axis;
        Vector3d Momentum(SimulationState s) => s.Boat.OrientationBodyToWorld.Rotate(
            mass.InertiaBodyKgM2.Multiply(s.Boat.AngularVelocityBodyRadPerSec) +
            boomAxis * (120 * s.Rig.BoomRateRadPerSec) + jibAxis * (40 * s.Rig.JibRateRadPerSec));
        double Energy(SimulationState s)
        {
            var omega = s.Boat.AngularVelocityBodyRadPerSec;
            var boomRate = s.Rig.BoomRateRadPerSec;
            var jibRate = s.Rig.JibRateRadPerSec;
            var sheet = Hunter235SheetExperiment.Evaluate(false, 1, s.Rig.JibAngleRad);
            var extension = Math.Max(0, sheet.LengthM - sheet.SlackLengthM);
            return 0.5 * Vector3d.Dot(omega, mass.InertiaBodyKgM2.Multiply(omega)) +
                Vector3d.Dot(omega, boomAxis) * (120 * boomRate) +
                Vector3d.Dot(omega, jibAxis) * (40 * jibRate) +
                0.5 * 120 * boomRate * boomRate + 0.5 * 40 * jibRate * jibRate +
                0.5 * 500 * s.Rig.BoomAngleRad * s.Rig.BoomAngleRad + 0.5 * 1000 * extension * extension;
        }
        var startEnergy = Energy(simulation.Latest);
        var maxMomentum = 0.0;
        var maxEnergyChange = 0.0;
        for (var step = 0; step < 600; step++)
        {
            var snapshot = simulation.Step(input);
            maxMomentum = Math.Max(maxMomentum, Momentum(snapshot).Length);
            maxEnergyChange = Math.Max(maxEnergyChange, Math.Abs(Energy(snapshot) - startEnergy));
        }
        Console.WriteLine($"  Two-sail closed test: max angular momentum {maxMomentum:F8} kg m²/s; max energy change {maxEnergyChange:F6} J (initial {startEnergy:F6} J)");
        Require(maxMomentum < 1e-5 && maxEnergyChange < 0.001,
            "The two-sail trial must preserve momentum and energy within the scenario tolerances.");
    }),
    ("Experimental coupled Hunter sails replay a full-boat turn", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        var controls = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 1);
        var model = new Hunter235BoatModel(new Hunter235RigModel(experimentalSheetGeometry: true), experimentalRigMomentum: true);
        var input = new SimulationInput(controls, env);
        Simulation Create() => new(model.Hull.CreateInitialState(input, Math.PI / 2), new Hunter235MassModel(), model, input);
        var first = Create();
        var second = Create();
        var releasedBoat = Create();
        var releasedControls = controls;
        var opposingRun = 0;
        var longestOpposing = 0;
        for (long step = 0; step < 3240; step++)
        {
            controls = new HeldSailingInput(tiller: step >= 240 && step < 2640 ? -0.6 : 0).Advance(controls);
            input = new(controls, env);
            var a = first.Step(input);
            var b = second.Step(input);
            releasedControls = new HeldSailingInput(tiller: step >= 240 && step < 2640 ? -0.6 : 0,
                starboardJibSheet: step >= 1200 && step < 1440 ? -1 : 0).Advance(releasedControls);
            releasedBoat.Step(new(releasedControls, env));
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.HunterRig!.Total == b.HunterRig!.Total,
                $"Experimental coupled replay differs at step {step + 1}.");
            var attitude = PresentationConversions.GetAttitude(a.Boat.OrientationBodyToWorld);
            if (attitude.Heading > Math.PI && Math.Abs(attitude.Heel) < Math.PI / 2 &&
                a.Rig.JibAngleRad > 0 && a.HunterRig!.Jib.TorqueNm < -1)
                longestOpposing = Math.Max(longestOpposing, ++opposingRun);
            else opposingRun = 0;
        }
        var completed = first.Latest;
        var published = completed.HunterRig!;
        var evaluated = model.EvaluateDynamics(new(completed.TimeSeconds, completed.Boat, completed.Mass!, input, completed.Rig));
        Require(published.ExperimentalRigMomentum && published.ExperimentalSheetGeometry && published.Derivative == evaluated.Rig,
            "Published rig derivative must include the selected experimental coupling.");
        var evaluation = new ForceEvaluation(completed.TimeSeconds, completed.Boat, completed.Mass!, input, completed.Rig);
        var external = model.Gravity.Evaluate(evaluation) + model.Hull.Evaluate(evaluation) +
            model.Foils.Centerboard.Evaluate(evaluation) + model.Foils.Rudder.Evaluate(evaluation) + published.Total;
        var inertia = completed.Mass!.InertiaBodyKgM2;
        var omega = completed.Boat.AngularVelocityBodyRadPerSec;
        var boomAxis = -Vector3d.UnitZ;
        var jibAxis = SheetMomentumExperiment.Axis;
        var relativeMomentum = boomAxis * (120 * completed.Rig.BoomRateRadPerSec) +
            jibAxis * (40 * completed.Rig.JibRateRadPerSec);
        var bodyAcceleration = inertia.Solve(evaluated.Forces.MomentBodyNm - Vector3d.Cross(omega, inertia.Multiply(omega)));
        var momentumRate = inertia.Multiply(bodyAcceleration) +
            boomAxis * (120 * evaluated.Rig.AngularAcceleration) + jibAxis * (40 * evaluated.Rig.JibAngularAcceleration) +
            Vector3d.Cross(omega, inertia.Multiply(omega) + relativeMomentum);
        Near(momentumRate, external.MomentBodyNm, 1e-8);
        Throws<ArithmeticException>(() => Hunter235BoatModel.CoupleRig(
            evaluation with { Mass = new MassProperties(1, new(1, 1, 1)) }, default, default));
        Console.WriteLine($"  Coupled held-sheet turn: heading {PresentationConversions.GetAttitude(completed.Boat.OrientationBodyToWorld).Heading * 180 / Math.PI:F1} deg, held/released jib {completed.Rig.JibAngleRad:F3}/{releasedBoat.Latest.Rig.JibAngleRad:F3} rad, longest opposing-load run {longestOpposing} steps");
        Require(longestOpposing >= 60 && releasedControls.StarboardJibSheet < 0.01 &&
            completed.Rig.JibAngleRad - releasedBoat.Latest.Rig.JibAngleRad > 0.2,
            "The coupled turn must retain the jib under opposing load and respond to easing the old sheet.");
    }),
    ("Experimental coupled Hunter steady reach settles and replays", () =>
    {
        var env = HunterDevelopmentScenario.Environment();
        var controls = new SailingControls(tiller: -0.1, mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 0.65);
        var input = new SimulationInput(controls, env);
        var model = new Hunter235BoatModel(new Hunter235RigModel(true), experimentalRigMomentum: true);
        Simulation Create() => new(model.Hull.CreateInitialState(input, Math.PI / 2), new Hunter235MassModel(), model, input);
        var first = Create();
        var second = Create();
        var minHeading = double.PositiveInfinity; var maxHeading = double.NegativeInfinity;
        var minSpeed = double.PositiveInfinity; var maxSpeed = 0.0; var maxHeel = 0.0; var maxLuff = 0.0;
        for (var step = 0; step < 3600; step++)
        {
            var a = first.Step(input);
            var b = second.Step(input);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.HunterRig!.Total == b.HunterRig!.Total,
                $"Experimental reach differs at step {step + 1}.");
            if (step < 2400) continue;
            var attitude = PresentationConversions.GetAttitude(a.Boat.OrientationBodyToWorld);
            var speed = a.Boat.LinearVelocityWorldMps.Length;
            minHeading = Math.Min(minHeading, attitude.Heading); maxHeading = Math.Max(maxHeading, attitude.Heading);
            minSpeed = Math.Min(minSpeed, speed); maxSpeed = Math.Max(maxSpeed, speed);
            maxHeel = Math.Max(maxHeel, Math.Abs(attitude.Heel));
            maxLuff = Math.Max(maxLuff, Math.Max(a.HunterRig!.Main.Sections.Average(s => s.Luffing),
                a.HunterRig.Jib.Sections.Average(s => s.Luffing)));
        }
        Console.WriteLine($"  Experimental reach 40–60 s: heading {minHeading * 180 / Math.PI:F2}–{maxHeading * 180 / Math.PI:F2} deg; speed {minSpeed:F3}–{maxSpeed:F3} m/s, max heel {maxHeel * 180 / Math.PI:F2} deg, max luff {maxLuff:F3}");
        Require(minSpeed > 1 && maxSpeed - minSpeed < 0.02 && maxHeading - minHeading < Math.PI / 180 &&
            maxHeel < Math.PI / 4 && maxLuff < 0.1,
            "Experimental reach failed the settling, upright or sail-filling criteria.");
    }),
    ("Experimental coupled Hunter stays bounded with waves and moving controls", () =>
    {
        foreach (var preset in new[] { WavePreset.Calm, WavePreset.Gentle, WavePreset.Moderate })
        {
            var env = PresentationConversions.EnvironmentFromSettings(windSpeedKnots: 10, wavePreset: preset, waveSeed: 123);
            var controls = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
                outhaul: 0.5, cunningham: 0.3, starboardJibSheet: 0.65);
            var input = new SimulationInput(controls, env);
            var model = new Hunter235BoatModel(new Hunter235RigModel(true), experimentalRigMomentum: true);
            var sim = new Simulation(model.Hull.CreateInitialState(input, Math.PI / 2), new Hunter235MassModel(), model, input);
            var maxSpeed = 0.0; var maxAngularSpeed = 0.0;
            for (var step = 0; step < 1800; step++)
            {
                var held = step < 600 ? new HeldSailingInput(tiller: -0.1)
                    : step < 1200 ? new HeldSailingInput(centerboard: -1, hiking: 1, starboardJibSheet: -1)
                    : new HeldSailingInput(centerboard: 1, hiking: -1, portJibSheet: 1);
                controls = held.Advance(controls);
                var state = sim.Step(new(controls, env));
                maxSpeed = Math.Max(maxSpeed, state.Boat.LinearVelocityWorldMps.Length);
                maxAngularSpeed = Math.Max(maxAngularSpeed, state.Boat.AngularVelocityBodyRadPerSec.Length);
                Require(maxSpeed < 30 && maxAngularSpeed < 20 && Math.Abs(state.Boat.PositionWorldM.Z) < 3,
                    $"Experimental Hunter motion became unbounded in {preset} at step {step + 1}.");
            }
            Require(controls.Centerboard > 0.99 && controls.Hiking < -0.99 && controls.PortJibSheet > 0.99 && controls.StarboardJibSheet < 0.01,
                "The wave scenario must complete its board, crew and sheet movements.");
            Console.WriteLine($"  Experimental {preset}: max speed {maxSpeed:F3} m/s, max angular speed {maxAngularSpeed:F3} rad/s");
        }
    }),
    ("Hunter snapshots publish completed sail evaluations", () =>
    {
        var model = new Hunter235BoatModel();
        var simulation = new Hunter235BoatDefinition().CreateSimulation(windy);
        var initial = simulation.Latest;
        var initialRig = initial.HunterRig ?? throw new Exception("Initial Hunter sail state missing.");
        Require(!initialRig.ExperimentalSheetGeometry, "The Hunter definition must retain the default sheet model.");
        Require(initialRig.PortSheet is null && initialRig.StarboardSheet is null,
            "The default constraint must not publish experimental sheet forces.");
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
    ("Experimental Hunter snapshots publish completed sheet forces", () =>
    {
        var model = new Hunter235BoatModel(new Hunter235RigModel(experimentalSheetGeometry: true));
        var initialInput = Input(new SailingControls(starboardJibSheet: 1), 10);
        var simulation = new Simulation(model.Hull.CreateInitialState(initialInput), new Hunter235MassModel(), model, initialInput);
        var initial = simulation.Latest.HunterRig!;
        var savedPort = initial.PortSheet!.Value;
        var changedInput = Input(new SailingControls(portJibSheet: 0.8, starboardJibSheet: 0.2), 10);
        var completed = simulation.Step(changedInput);
        var published = completed.HunterRig!;
        Require(published.ExperimentalSheetGeometry && published.PortSheet.HasValue && published.StarboardSheet.HasValue,
            "The experimental rig must publish both sheet evaluations.");
        var port = published.PortSheet!.Value;
        var starboard = published.StarboardSheet!.Value;
        Require(port == Hunter235SheetExperiment.Evaluate(true, changedInput.Controls.PortJibSheet, completed.Rig.JibAngleRad) &&
            starboard == Hunter235SheetExperiment.Evaluate(false, changedInput.Controls.StarboardJibSheet, completed.Rig.JibAngleRad),
            "Published sheets must match the completed rig state and sampled controls.");
        Near(port.ClewBodyM, published.Jib.ClewBodyM, 1e-12);
        Near(starboard.ClewBodyM, published.Jib.ClewBodyM, 1e-12);
        Near(published.Derivative.JibAngularAcceleration,
            (published.Jib.TorqueNm + port.TorqueAboutForestayNm + starboard.TorqueAboutForestayNm -
             80 * completed.Rig.JibRateRadPerSec) / 40, 1e-12);
        Require(initial.PortSheet!.Value == savedPort, "Earlier sheet snapshots must remain unchanged.");
    }),
    ("Hunter presentation geometry matches physics and sampled controls", () =>
    {
        var simulation = new Hunter235BoatDefinition().CreateSimulation(windy);
        var initial = simulation.Latest;
        var initialGeometry = initial.HunterGeometry ?? throw new Exception("Hunter geometry missing.");
        var controls = new SailingControls(centerboard: 0.25, hiking: -0.8, portJibSheet: 0.7);
        var snapshot = simulation.Step(Input(controls, 10));
        var boat = snapshot.HunterGeometry ?? throw new Exception("Completed geometry missing.");
        var sails = snapshot.HunterRig ?? throw new Exception("Completed sail geometry missing.");
        var skipper = new Hunter235MassModel().Contributors(controls).Single(c => c.Name == "Skipper");
        Near(boat.SkipperBodyM, skipper.CenterBodyM, 0);
        Near((boat.CenterboardTipBodyM - boat.CenterboardTopBodyM).Length, 1.25 * controls.Centerboard, 1e-12);
        Near((initialGeometry.CenterboardTipBodyM - initialGeometry.CenterboardTopBodyM).Length, 1.25, 1e-12);
        foreach (var sail in new[] { sails.Main, sails.Jib })
        {
            Require(sail.Geometry.Count == sail.Sections.Count && sail.Geometry is not HunterSailSectionGeometry[],
                "Sail section geometry must be immutable and match the force sections.");
            for (var i = 0; i < sail.Sections.Count; i++)
            {
                var chord = sail.Geometry[i].TrailingEdgeBodyM - sail.Geometry[i].LeadingEdgeBodyM;
                Near(chord.Length, sail.Sections[i].ChordM, 1e-12);
                Near(sail.Geometry[i].LeadingEdgeBodyM + chord * 0.4, sail.Sections[i].CenterBodyM, 1e-12);
            }
        }
        Require(initial.HunterGeometry == initialGeometry && initialGeometry.SkipperBodyM != boat.SkipperBodyM,
            "Earlier geometry must remain unchanged after controls move.");
        Require(new IlcaBoatDefinition().CreateSimulation(windy).Latest.HunterGeometry is null,
            "Other boats must not publish Hunter geometry.");
    }),
    ("Hunter completed boat diagnostics match dynamics", () =>
    {
        foreach (var coupled in new[] { false, true })
        {
            var model = new Hunter235BoatModel(new Hunter235RigModel(coupled), experimentalRigMomentum: coupled);
            var simulation = new Simulation(model.Hull.CreateInitialState(windy), new Hunter235MassModel(), model, windy);
            for (var i = 0; i < 30; i++) simulation.Step(windy);
            var state = simulation.Latest;
            var evaluation = new ForceEvaluation(state.TimeSeconds, state.Boat, state.Mass!, state.Input, state.Rig);
            var diagnostics = state.HunterDiagnostics!;
            var forces = model.EvaluateDynamics(evaluation).Forces;
            Near(diagnostics.Total.ForceWorldN, forces.ForceWorldN, 1e-9);
            Near(diagnostics.Total.MomentBodyNm, forces.MomentBodyNm, 1e-9);
            Require(diagnostics.Main == state.HunterRig!.Main.Force && diagnostics.Jib == state.HunterRig.Jib.Force,
                "Boat and sail diagnostics must use the same completed state.");
            Require(coupled || diagnostics.RigReaction == default, "Uncoupled model must have no rig reaction.");
        }
        Require(new IlcaBoatDefinition().CreateSimulation(windy).Latest.HunterDiagnostics is null,
            "Other boats must not publish Hunter boat diagnostics.");
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
    ("Hunter jib sheets retain limits in both wind directions", () =>
    {
        var rig = new Hunter235RigModel();
        var bothTight = new SailingControls(portJibSheet: 1, starboardJibSheet: 1);
        var bothEased = new SailingControls(portJibSheet: 0, starboardJibSheet: 0);
        var mass = new Hunter235MassModel().Evaluate(bothTight);
        var still = new ForceEvaluation(0, RigidBodyState.AtRest, mass, Input(bothTight, 0));
        foreach (var sign in new[] { -1.0, 1.0 })
        {
            var state = new RigState(0, 0, sign * 0.5, 0);
            var tight = rig.Inspect(still with { Rig = state });
            var eased = rig.Inspect(still with { Input = Input(bothEased, 0), Rig = state });
            Require(sign * tight.Derivative.JibAngularAcceleration < 0,
                "Both tight sheets must pull the jib back from either side.");
            Near(eased.Derivative.JibAngularAcceleration, 0, 0);
            Near(tight.Jib.PortLimitRad, 0.15, 1e-12);
            Near(tight.Jib.StarboardLimitRad, 0.15, 1e-12);
            Near(eased.Jib.PortLimitRad, 1.45, 1e-12);
            Near(eased.Jib.StarboardLimitRad, 1.45, 1e-12);
        }

        var portHeld = new SailingControls(portJibSheet: 1, starboardJibSheet: 0);
        var positiveWind = new EnvironmentConfig(new(3, 4, 0), WavePreset.Calm, WaveDirectionMode.Independent, Vector3d.UnitX, 1);
        var negativeWind = new EnvironmentConfig(new(3, -4, 0), WavePreset.Calm, WaveDirectionMode.Independent, Vector3d.UnitX, 1);
        var centered = new RigState(0, 0, 0, 0);
        var first = rig.Inspect(still with { Input = new(portHeld, positiveWind), Rig = centered });
        var reversed = rig.Inspect(still with { Input = new(portHeld, negativeWind), Rig = centered });
        Require(first.Jib.TorqueNm * reversed.Jib.TorqueNm < 0,
            "Reversing lateral airflow must reverse the load on the centered jib.");
        Near(first.Jib.StarboardLimitRad, reversed.Jib.StarboardLimitRad, 0);
        Near(first.Jib.StarboardLimitRad, 0.15, 1e-12);
        Near(first.Jib.PortLimitRad, 1.45, 1e-12);

        var alignedWind = new EnvironmentConfig(new(-5, 0, 0), WavePreset.Calm, WaveDirectionMode.Independent, Vector3d.UnitX, 1);
        var luffing = rig.Inspect(still with { Input = new(bothEased, alignedWind), Rig = centered });
        Require(luffing.Main.Sections.All(s => s.Luffing > 0.99) &&
            luffing.Jib.Sections.All(s => s.Luffing > 0.99),
            "Airflow aligned with centered sail chords must luff both sails.");
        Near(luffing.Total.ForceWorldN, Vector3d.Zero, 1e-10);
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

// Test-only wrapper supplies an exactly mirrored initial rig to the normal solver.
sealed class HunterInitialRig(Hunter235BoatModel model, double side) : IRigDynamicsModel, IHunterRigInspectionModel
{
    public RigState InitialRig => new(0.1 * side, 0, 0.1 * side, 0);
    public ForceMoment Evaluate(in ForceEvaluation e) => model.Evaluate(in e);
    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e) => model.EvaluateDynamics(in e);
    public HunterRigEvaluation InspectHunterRig(in ForceEvaluation e) => model.InspectHunterRig(in e);
    public HunterBoatGeometry InspectHunterGeometry(SailingControls controls) => model.InspectHunterGeometry(controls);
}

// Isolated angular-momentum accounting experiment, not a full Hunter force model.
sealed class SheetMomentumExperiment(int mode) : IRigDynamicsModel
{
    public static readonly Vector3d Axis = new Vector3d(-2.5908, 0, -7.7724) / new Vector3d(-2.5908, 0, -7.7724).Length;
    public RigState InitialRig => new(0, 0, 0, 0);
    public ForceMoment Evaluate(in ForceEvaluation e) => EvaluateDynamics(in e).Forces;
    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e)
    {
        var torque = Hunter235SheetExperiment.Evaluate(false, e.Input.Controls.StarboardJibSheet, e.Rig.JibAngleRad).TorqueAboutForestayNm;
        var relativeMomentum = Axis * (40 * e.Rig.JibRateRadPerSec);
        var omega = e.Boat.AngularVelocityBodyRadPerSec;
        var relativeAcceleration = torque / 40;
        if (mode == 2)
        {
            var inertia = e.Mass.InertiaBodyKgM2;
            var baseAcceleration = inertia.Solve(-Vector3d.Cross(omega, inertia.Multiply(omega) + relativeMomentum));
            var coupling = Vector3d.Dot(Axis, inertia.Solve(Axis));
            relativeAcceleration = (torque / 40 - Vector3d.Dot(Axis, baseAcceleration)) / (1 - 40 * coupling);
        }
        var moment = mode > 0 ? -Axis * (40 * relativeAcceleration) - Vector3d.Cross(omega, relativeMomentum) : Vector3d.Zero;
        return (new(Vector3d.Zero, moment), new(0, 0, e.Rig.JibRateRadPerSec, relativeAcceleration));
    }
}

// Two body-fixed axes and constant scalar inertias; deliberately excludes external loads.
sealed class DualRigMomentumExperiment : IRigDynamicsModel
{
    public RigState InitialRig => new(0.25, 0, 0, 0);
    public ForceMoment Evaluate(in ForceEvaluation e) => EvaluateDynamics(in e).Forces;
    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e)
    {
        var boomTorque = -500 * e.Rig.BoomAngleRad;
        var jibTorque = Hunter235SheetExperiment.Evaluate(false, e.Input.Controls.StarboardJibSheet, e.Rig.JibAngleRad).TorqueAboutForestayNm;
        return Hunter235BoatModel.CoupleRig(in e, default,
            new(e.Rig.BoomRateRadPerSec, boomTorque / 120, e.Rig.JibRateRadPerSec, jibTorque / 40));
    }
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
