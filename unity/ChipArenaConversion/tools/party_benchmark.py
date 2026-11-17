"""Print matched host/guest windows from Unity Player.log files as CSV."""
import argparse
import csv
import json
from pathlib import Path
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("logs", nargs="+", type=Path)
    args = parser.parse_args()
    writer = csv.writer(sys.stdout)
    keys = "epoch map visit segment role seat humans utc frames seconds p95Ms worstMs maxSimulationMs maxPresentationMs disconnected".split()
    writer.writerow(["log", *keys, "outKiBps"])
    rows = []
    for path in args.logs:
        with path.open(errors="replace") as log:
            for line in log:
                if "PARTY_BENCH {" not in line:
                    continue
                sample = json.loads(line.partition("PARTY_BENCH ")[2])
                rows.append((sample, path.name))
    for sample, name in sorted(rows, key=lambda item: (item[0]["epoch"], item[0]["map"], item[0]["visit"], item[0]["utc"])):
        writer.writerow([name, *(sample[k] for k in keys), round(sample["sentBytes"] / 1024 / sample["seconds"], 2)])
    if not rows:
        parser.exit(1, "No PARTY_BENCH windows found in the supplied logs.\n")


if __name__ == "__main__":
    main()
