"""Configure a separate Unity checkout for Steam networking before opening it in Unity."""
import argparse
import json
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
PACKAGE = "com.rlabrecque.steamworks.net"
PIN = "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.165.0"


def configure(project, app_id, default_steam):
    if not 0 < app_id <= 4294967295:
        raise ValueError("Expected a positive 32-bit Steam App ID")
    manifest = project / "Packages/manifest.json"
    data = json.loads(manifest.read_text(encoding="utf-8"))
    existing = data["dependencies"].get(PACKAGE)
    if existing and existing != PIN:
        raise ValueError("This checkout already specifies a different Steamworks.NET package")
    data["dependencies"][PACKAGE] = PIN
    manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    rsp = project / "Assets/csc.rsp"
    current = rsp.read_text(encoding="utf-8-sig") if rsp.exists() else ""
    define = "-define:CHIP_ARENA_STEAM"
    if define not in current.splitlines():
        rsp.write_text((current.rstrip() + "\n" if current.strip() else "") + define + "\n", encoding="utf-8")
    config = project / "Assets/StreamingAssets/ChipArenaSteam.json"
    config.parent.mkdir(exist_ok=True)
    config.write_text(json.dumps({"appId": app_id, "defaultSteam": default_steam}) + "\n", encoding="ascii")
    (project / "steam_appid.txt").write_text(str(app_id) + "\n", encoding="ascii")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app-id", required=True, type=int)
    parser.add_argument("--default", choices=["cloudflare", "steam"], default="cloudflare")
    options = parser.parse_args()
    try:
        configure(PROJECT, options.app_id, options.default == "steam")
    except (OSError, ValueError) as error:
        parser.exit(1, f"Steam configuration failed: {error}\n")
    print("Configured this checkout for Steam. Open it in Unity to resolve the pinned package.")
