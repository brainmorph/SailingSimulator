# Windows counterpart of run-game.sh: builds the C# game, imports resources once, then launches it.
# Optional arguments are forwarded to Godot. Set GODOT_BIN to choose a specific Godot executable.
$ErrorActionPreference = 'Stop'

# Resolve the project from the script location, even when launched elsewhere.
$projectDir = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectDir

function Fail([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'Install the .NET 8 SDK (or newer) to build and launch the game.' }

function Find-Godot {
    if ($env:GODOT_BIN) {
        $command = Get-Command $env:GODOT_BIN -ErrorAction SilentlyContinue
        if (-not $command) { Fail "Cannot find Godot: $env:GODOT_BIN" }
        return $command.Source
    }
    foreach ($name in 'godot-mono', 'godot4-mono', 'godot4', 'godot') {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }
    # Prefer the console build so Godot's output appears in this terminal.
    $roots = "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Downloads", "$env:LOCALAPPDATA\Programs", $env:ProgramFiles, 'C:\Godot', 'C:\tools'
    $found = foreach ($root in $roots | Where-Object { $_ -and (Test-Path -LiteralPath $_) }) {
        Get-ChildItem -LiteralPath $root -Filter 'Godot_v4.7*mono_win64_console.exe' -Recurse -Depth 3 -ErrorAction SilentlyContinue
    }
    if ($found) { return ($found | Sort-Object Name -Descending | Select-Object -First 1).FullName }
    return $null
}

$godot = Find-Godot
if (-not $godot) { Fail 'Godot 4.7 .NET was not found. Set GODOT_BIN to its executable path.' }

$godotVersion = (& $godot --version | Select-Object -Last 1).Trim()
if ($godotVersion -notmatch '^4\.7\..*mono') {
    Fail "Godot 4.7 .NET (Mono) is required; found $godotVersion at $godot. Set GODOT_BIN to the correct executable."
}

# Incremental builds pick up code changes and stop the launch on build failure.
& dotnet build "$projectDir\godot\SailingSimulator.Godot.csproj" --nologo --verbosity minimal -m:1 /nodeReuse:false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Prepare a fresh checkout without opening an editor window.
if (-not (Test-Path -LiteralPath "$projectDir\godot\.godot\uid_cache.bin")) {
    & $godot --headless --path "$projectDir\godot" --editor --import --quit
}

# Run the game directly. Optional arguments are forwarded to Godot.
& $godot --path "$projectDir\godot" @args
exit $LASTEXITCODE
