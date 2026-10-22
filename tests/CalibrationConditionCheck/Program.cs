using System.Text.Json;

var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
var fixturePath = Path.Combine(repositoryRoot, "phases", "calibration-condition.example.json");
using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
var root = document.RootElement;

Require(root.GetProperty("id").GetString() == "steady-10kt-gentle-seed-1", "Condition ID changed unexpectedly.");

var wind = root.GetProperty("wind");
var windSpeed = wind.GetProperty("speed_mps").GetDouble();
Require(Math.Abs(windSpeed - 10 * 0.514444) < 0.000005, "Wind speed must be the 10-knot default converted to m/s.");
Require(wind.GetProperty("from_degrees").GetDouble() == 0, "North wind must use a from-direction of 0 degrees.");

var waves = root.GetProperty("waves");
Require(waves.GetProperty("preset").GetString() == "Gentle", "Gentle must remain the default wave preset.");
Require(waves.GetProperty("direction_mode").GetString() == "AlignedWithWind", "Default waves must align with wind.");
Require(waves.GetProperty("from_degrees").ValueKind == JsonValueKind.Null, "Aligned waves must not specify an independent direction.");
Require(waves.GetProperty("seed").GetInt32() == 1, "Wave seed must be an integer.");

var controls = root.GetProperty("initial_controls");
RequireRange(controls, "tiller", -1, 1);
RequireRange(controls, "mainsheet", 0, 1);
RequireRange(controls, "traveler", 0, 1);
RequireRange(controls, "vang", 0, 1);
RequireRange(controls, "outhaul", 0, 1);
RequireRange(controls, "cunningham", 0, 1);
RequireRange(controls, "centerboard", 0, 1);
RequireRange(controls, "hiking", -1, 1);

var assumptions = root.GetProperty("assumptions");
Require(assumptions.GetProperty("sailor_mass_kg").GetDouble() == 80, "Reference sailor mass must remain 80 kg.");
Require(assumptions.GetProperty("water_current").GetString() == "still", "Initial conditions must use still water.");
Require(root.GetProperty("results").ValueKind == JsonValueKind.Null, "Example results must remain unset until a run is recorded.");

Console.WriteLine("Phase 1 calibration condition contract passed.");

static void RequireRange(JsonElement controls, string field, double minimum, double maximum)
{
    var value = controls.GetProperty(field).GetDouble();
    Require(value >= minimum && value <= maximum, $"Control '{field}' must be between {minimum} and {maximum}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string FindRepositoryRoot(string startingDirectory)
{
    var directory = new DirectoryInfo(startingDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "PLAN.md")) && Directory.Exists(Path.Combine(directory.FullName, "phases")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing PLAN.md and phases/.");
}
