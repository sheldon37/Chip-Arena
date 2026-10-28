#!/usr/bin/env python3
"""Run the native cover duel measurements in an activated Unity Editor, never the Electron simulator."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', default=os.environ.get('UNITY_EDITOR'))
    parser.add_argument('--seeds', type=int, default=4, help='Paired seeds; each runs both spawn sides')
    parser.add_argument('--maps', default='brickfall,factory,cybercity')
    parser.add_argument('--difficulty', choices=['easy', 'normal', 'hard'], default='normal')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    editor = shutil.which(args.unity or 'Unity')
    if not editor:
        parser.error('Unity Editor is unavailable. Set UNITY_EDITOR or pass --unity to an activated Editor executable; no duel results were generated.')
    if not 1 <= args.seeds <= 64:
        parser.error('--seeds must be 1–64')
    project = Path(__file__).resolve().parents[1]
    output = (args.output or project / 'Temp' / 'MeleeDuels.json').resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    # Never accept an old completed report as this run's result.
    if output.exists():
        parser.error(f'Output already exists: {output}. Choose a new --output path.')
    env = dict(os.environ, CHIP_DUEL_SEEDS=str(args.seeds), CHIP_DUEL_MAPS=args.maps,
               CHIP_DUEL_DIFFICULTY=args.difficulty, CHIP_DUEL_OUTPUT=str(output))
    env['CHIP_DUEL_COMMIT'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=project, text=True).strip()
    dirty = subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=no'], cwd=project, text=True).strip()
    if dirty:
        env['CHIP_DUEL_COMMIT'] += '+dirty'
    log = output.with_suffix('.log')
    result = subprocess.run([editor, '-batchmode', '-nographics', '-quit', '-projectPath', str(project),
                             '-executeMethod', 'ChipArena.UnityConversion.Editor.NativeMeleeDuels.Run',
                             '-logFile', str(log)], cwd=project, env=env)
    if result.returncode:
        return result.returncode
    text = log.read_text(errors='replace') if log.exists() else ''
    if 'MELEE_DUELS_COMPLETE ' not in text or not output.exists():
        raise SystemExit(f'Native duel run incomplete; inspect {log}')
    report = json.loads(output.read_text())
    expected = len(args.maps.split(',')) * 4 * 7 * args.seeds * 2
    if len(report['duels']) != expected:
        raise SystemExit(f'Expected {expected} duels, got {len(report["duels"])}')
    for rate in report['rates']:
        print(f'{rate["map"]} {rate["melee"]}: {rate["winRate"]:.1%} ({rate["wins"]}/{rate["games"]}), draws={rate["draws"]}, timeouts={rate["timeouts"]}, target25–55={rate["inTarget"]}')
    print(f'Complete native measurements: {output}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
