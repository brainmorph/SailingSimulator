# Godot host

Open `project.godot` with **Godot 4.7 .NET (Mono)** and build the C# project,
then run the main scene (F6) or project (F5). The local executable is recorded
in [LOCAL_ENVIRONMENT.md](../LOCAL_ENVIRONMENT.md). From the repository root:

```sh
dotnet build godot/SailingSimulator.Godot.csproj
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot
```

The Godot SDK and bindings are the only engine dependencies; no third-party
packages are added. The root solution deliberately remains buildable with
only .NET 8; build the Godot project separately as shown above.

The test scene starts an ILCA 7 on a beam reach in the default 10-knot
northerly wind and gentle waves. Each session chooses a wave seed. The
physics coefficients are provisional. Hull, mast and boom/sail are simple
integration proxies; the plane shows mean water level. Sectional sail shape,
telltales, visible waves, sailor/foil meshes and both final camera views are
Phase 6 work. F1 settings, graphs and detailed diagnostics are Phase 7 work.

## Fixed controls

Hold a key to adjust. Both keys in a pair cancel. Physical key positions are
used, independent of keyboard text layout.

| Keys | Action | Rate (normalized units/second) |
| --- | --- | ---: |
| A / D | Tiller port / starboard (turn direction in forward flow) | 1.5 |
| W / S | Mainsheet in / out | 0.25 |
| Q / E | Traveler in / out | 0.25 |
| R / F | Vang on / off | 0.25 |
| T / G | Outhaul on / off | 0.25 |
| U / J | Cunningham on / off | 0.25 |
| I / K | Centerboard down / up | 0.25 |
| Z / X | Sailor toward port / starboard | 0.5 |
| Right mouse drag | Orbit camera | — |
| Mouse wheel | Camera distance | — |

Tiller returns to centre at 1.5 units/second when released. Other settings,
including hiking, stay at their last value. Losing application focus clears
held keys immediately and releases mouse orbit; simulation continues and
tiller centres. Releasing keys never resets trim. Initial normalized settings:
tiller 0, sheet 0.6, traveler 0.8, vang 0.6, outhaul 0.5, cunningham 0.3,
centerboard 1, hiking −0.3. These rates/settings are provisional usability
choices, not measured ILCA trim values. Stop/restart the project for a new
session; capsize recovery is deferred.

## Verification

Run the dependency-free handoff/scheduler/lifecycle regression checks:

```sh
dotnet run --project tests/GodotIntegrationCheck
```

After building the Godot project, verify the actual scene without a display:

```sh
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --editor --import --quit
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot -- --smoke-test
```

The smoke test uses seed 123, checks all physical key pairs and opposing
keys, compares rendered position/orientation with the completed simulation
state, deliberately stalls the main thread, tests focus loss and mouse
isolation, and joins the worker before exiting. A pass prints `PASS Godot
scene` and exits 0. See [the integration contract](../docs/godot-integration.md)
for ownership, cadence and failure behavior.
