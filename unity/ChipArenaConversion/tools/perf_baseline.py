"""The voxel retirement's player baselines (docs/VOXEL-STYLE.md "Baselines"; docs/VOXEL-RETIREMENT.md V0).

Reads Unity Player.log files and prints one Markdown row per scenario from their PERF_SAMPLE lines (a 30 s summary
during a match: p95 and p99 of the last 240 frames, the worst frame, and the scene) and their PERF_HITCH lines (a
recovered stall). A scenario is the graphics tier, the body style (Settings - Player model, the --ca-voxel-bodies and
--ca-voxel-zombies switches; logged by builds from claude/voxel-v0-guard on), the map, PvP or the expedition, and the
pilot count; minions are averaged. Samples taken paused, unfocused or during a replay are left out, as are samples
from an older build without the body style (marked "?").

  python tools/perf_baseline.py "%USERPROFILE%/AppData/LocalLow/ChipArenaConversion/Chip Arena/Player.log" [more logs]

The rows go into the budget table with the machine, the tier and the settings named.
"""
import argparse
import json
import statistics
import sys
from collections import defaultdict
from pathlib import Path


def style(s):
    model = s.get("playerModel")
    if model is None:
        return "?"
    body = "voxel bodies (switch)" if s.get("voxelBodiesSwitch") else ("Classic" if model == "Classic" else "model")
    return body + (" + voxel zombies" if s.get("voxelZombiesSwitch") else "")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("logs", nargs="+", type=Path)
    args = parser.parse_args()
    groups = defaultdict(list)
    hitches = defaultdict(int)
    skipped = 0
    for path in args.logs:
        with path.open(encoding="utf-8", errors="replace") as log:
            for line in log:
                for tag in ("PERF_SAMPLE ", "PERF_HITCH "):
                    at = line.find(tag + "{")
                    if at < 0:
                        continue
                    try:
                        s = json.loads(line[at + len(tag):])
                    except ValueError:
                        continue
                    if s.get("paused") or not s.get("focused", True) or s.get("replayActive"):
                        skipped += 1
                        continue
                    mode = "expedition" if s.get("pveStage") else "PvP"
                    key = (s.get("graphics") or "?", style(s), s.get("map") or "?", mode, s.get("pilots", 0))
                    if tag == "PERF_SAMPLE ":
                        groups[key].append(s)
                    else:
                        hitches[key] += 1
    if not groups:
        parser.exit(1, "No PERF_SAMPLE lines found (play at least 30 s of a match in each scenario).\n")
    print("| Tier | Bodies | Map | Mode | Pilots | Minions (mean) | Samples | p95 ms (median, max) | p99 ms (median, max) | Worst ms | Stalls | Unexpected voxel draws | Resolution |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for key in sorted(groups):
        rows = groups[key]
        p95 = [r.get("p95Ms", 0) for r in rows]
        p99 = [r.get("p99Ms", 0) for r in rows]
        worst = max(r.get("worstMs", 0) for r in rows)
        minions = statistics.mean(r.get("minions", 0) for r in rows)
        voxel = sorted({r.get("voxelUnexpected", -1) for r in rows})
        voxel = "counter off" if voxel == [-1] else ", ".join(str(v) for v in voxel if v >= 0)
        res = "%dx%d" % (rows[-1].get("width", 0), rows[-1].get("height", 0))
        print("| %s | %s | %s | %s | %d | %.0f | %d | %.1f, %.1f | %.1f, %.1f | %.1f | %d | %s | %s |" % (
            key[0], key[1], key[2], key[3], key[4], minions, len(rows), statistics.median(p95), max(p95),
            statistics.median(p99), max(p99), worst, hitches.get(key, 0), voxel, res))
    if skipped:
        print("\n(%d paused, unfocused or replay samples left out.)" % skipped, file=sys.stderr)


if __name__ == "__main__":
    main()
