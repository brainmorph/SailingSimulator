# Wind and wave environment

Phase 4 adds a repeatable directional wave field to the provisional ILCA model.
The standalone core remains in SI units and north/east/down (NED) world axes.
These are practical force approximations, awaiting sailing calibration.

## Configuration and replay

`PresentationConversions.EnvironmentFromSettings` accepts wind speed in [0,30]
knots, default 10, and compass directions in [0,360] degrees **from which**
wind/waves come. North wind moves south: `(-speed,0,0)` in NED; east wind moves
west. 360 equals 0. True wind is uniform and steady. Each sail section subtracts
its local translational, hull rotational and boom velocity to obtain apparent
wind; waves affect apparent wind through boat motion, without changing true wind.

Gentle waves aligned with wind are the default. Alignment preserves the
requested bearing even when wind speed is zero. Independent wave direction
changes only the water configuration; it never changes wind. No current is
imposed. Wave strength is independent of wind speed so comparisons can isolate
wave effects, including zero-wind tests.

An omitted nullable `waveSeed` chooses a random integer **once at configuration
creation**. Pass a saved integer for repeatable conditions. Preserve the current
seed when changing wind/preset/direction sliders; omit it only for a new session
or an explicit new realization. Randomness is never consumed during stepping
or rendering. The core constructor requires an explicit integer seed.

```csharp
var environment = PresentationConversions.EnvironmentFromSettings(
    windSpeedKnots: 10, windFromDegrees: 0,
    wavePreset: WavePreset.Gentle,
    waveDirectionMode: WaveDirectionMode.Independent,
    waveFromDegrees: 45, waveSeed: 123);
var water = environment.Water;
var surface = water.SampleSurface(new Vector3d(5, 2, 0), timeSeconds: 3);
var velocity = water.VelocityAt(new Vector3d(5, 2, 0.3), timeSeconds: 3);
```

The immutable `DirectionalWaveField` is cached in `EnvironmentConfig`; it is
safe to share across simulation/render threads. `Components` is read-only.
Equal settings/seeds preserve configuration value equality and realize the same
components on the same runtime/platform. Cross-platform floating point bit
identity is not promised. Replacing input configuration leaves old snapshots
and their water realizations intact. Wave phase uses absolute simulation time;
changing settings can discontinuously change the surface. Transitions are not
smoothed in this phase.

## Provisional physical presets

| Preset | Spectral significant height Hs | Wavelength support | Peak wavelength | Direction support |
| --- | ---: | ---: | ---: | ---: |
| Calm | 0 m | None | None | None |
| Gentle | 0.12 m | 3–12 m | 6 m | Mean ±25° |
| Moderate | 0.30 m | 3–12 m | 6 m | Mean ±25° |

These are **design estimates, not measured ILCA conditions or weather scales**.
Wavelengths roughly bracket the hull length, allowing differential immersion
and pitch without a very stiff short ripple model. The same spectral support
and phases are retained between gentle/moderate; amplitudes and orbital flow
scale by 2.5 for controlled comparisons. Hs describes ensemble variance, not
a maximum crest/trough or the exact significant height of a short time record.

The realization uses eight stratified log-wavelength bins. Each bin chooses
`λ = 3 × 4^((i+0.2+0.6u)/8)` m. Its energy weight is
`exp(-0.5 (ln(λ/6)/0.40)^2)`, distributed over three directions (-25°,0°,25°)
with weights (0.25,0.5,0.25). This is a compact estimated directional spectrum,
not a fitted JONSWAP or measured sea spectrum. Independent uniform phases and
wavelength jitter use an explicit 32-bit LCG (`1664525s+1013904223 mod 2^32`)
initialized from the integer seed. Amplitudes are normalized so
`Σ a²/2 = (Hs/4)²`. Calm has no components and is exactly flat/stationary.

## Water equations and force coupling

The linear-wave basis, deep-water dispersion and exponentially decaying orbital
flow follow Krogstad & Arntsen, NTNU,
[Linear Wave Theory, Part A](https://oivarn.folk.ntnu.no/hercules_ntnu/LWTcourse/lwt_new_2000_Part_A.pdf),
sections 5–8 (2000). The chosen spectrum/preset numbers are this project's
estimates; the notes do not supply them.

For each component with unit horizontal propagation `d`:

```text
k = 2π/λ; ω = sqrt(g k); θ = k d·(x,y,0) − ωt + φ
η = Σ a cos θ                         (height positive up)
surface Z = −η                        (NED positive down)
outward normal = normalize(−ηx,−ηy,−1)
vwater = Σ aω exp(−k max(0,Z)) (dx cos θ, dy cos θ, −sin θ)
```

Velocity attenuates with depth below mean water, and is held at its mean-surface
amplitude above mean water. This bounded extension supplies flow to wet samples
in crests; it is not a nonlinear free-surface solution. Sampling position/time
must be finite. The API exposes elevation, world Z, outward normal and local
water velocity for future Godot rendering using the same field.

At every RK4 stage, hull-column/foil endpoints sample their local surface at
the stage time. Signed endpoint depths are interpolated to estimate submerged
fraction and wet centroid. This approximates one wet segment along a short
straight column; it does not integrate arbitrary repeated wave intersections.
Buoyancy remains vertical Archimedean force at the wet centroid. Hull drag and
each foil's lift/drag use boat point velocity **minus local orbital velocity**
at that centroid. Wave elevation therefore supplies heave/roll/pitch forcing;
spatial orbital flow changes surge/sway/yaw, steering and resistance as well.
The normals are available to rendering; buoyancy is not tilted to the normal.

This model assumes deep water relative to wavelengths. It omits finite-depth
shoaling, breaking, wave radiation/diffraction, added mass, dynamic wave pressure,
Stokes drift, planing, slamming and flooding. Orbital flow can transfer wave
energy into the boat; passive drag is dissipative relative to water, not
necessarily relative to the stationary world. The existing calm-water checks
retain their original physical assumptions. Initial heave finds instantaneous
t=0 displacement balance, not equilibrium in a time-varying sea.

## Verification and next host work

`tests/EnvironmentCheck` covers units/bearings/apparent wind, immutable seeded
sampling, spectral strength, height derivatives/normal/vertical flow, independent
directions, local hull/foil forcing, dry/raised-board cases, deterministic
six-degree trajectories, environment replacement, and bounded gentle/moderate
runs at 0/10/30 knots. `SimulationHarness --waves --wave-seed 123` provides an
inspectable gentle-wave replay; `--boat` preserves the flat-water case.

Phase 5 owns the threaded Godot host and held-key input. Use each completed
snapshot's environment and simulation time for water rendering. Preserve its
seed on settings edits and publish a complete new input snapshot. Phase 6 owns
water visuals; Phase 7 owns F1 settings and timing diagnostics. These offline
tests do not verify wall-clock cadence, FPS, sailor handling, or speed polars.
