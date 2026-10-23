#!/usr/bin/env bash
set -euo pipefail

# Resolve the project from the script location, even when launched elsewhere.
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd -- "$project_dir"

fail() {
    printf '%s\n' "$*" >&2
    exit 1
}

command -v dotnet >/dev/null 2>&1 || fail "Install the .NET 8 SDK to build and launch the game."

if [[ -n "${GODOT_BIN:-}" ]]; then
    godot_executable="$(command -v -- "$GODOT_BIN")" || fail "Cannot find Godot: $GODOT_BIN"
else
    godot_executable="$HOME/Downloads/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe"
    if [[ ! -x "$godot_executable" ]]; then
        godot_executable=""
        for candidate in godot-mono godot4-mono godot4 godot; do
            if godot_executable="$(command -v -- "$candidate")"; then break; fi
        done
    fi
    [[ -n "$godot_executable" ]] || fail "Godot 4.7 .NET was not found. Set GODOT_BIN to its executable path."
fi

godot_version="$("$godot_executable" --version)"
case "$godot_version" in
    4.7.*mono*) ;;
    *) fail "Godot 4.7 .NET (Mono) is required; found $godot_version. Set GODOT_BIN to the correct executable." ;;
esac

# Incremental builds pick up code changes and stop the launch on build failure.
dotnet build "$project_dir/godot/SailingSimulator.Godot.csproj" \
    --nologo --verbosity minimal -m:1 /nodeReuse:false

# Prepare a fresh checkout without opening an editor window.
if [[ ! -f "$project_dir/godot/.godot/uid_cache.bin" ]]; then
    "$godot_executable" --headless --path "$project_dir/godot" --editor --import --quit
fi

# Run the game directly. Optional arguments are forwarded to Godot.
exec "$godot_executable" --path "$project_dir/godot" "$@"
