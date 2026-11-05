"""Mirror reviewed sound inputs into the Unity-only builder source; no generation."""
from pathlib import Path
import shutil
import sys
import sound_queue as queue


def export(check=False):
    source = queue.gen.REPO
    destination = queue.BUNDLED_REPO
    canonical = source / 'docs/audio/sound-queue.json'
    if not canonical.is_file():
        # A packaged/sparse tree must still contain a complete, valid plan.
        queue.plan(destination / 'docs/audio/sound-queue.json', destination)
        return
    plans = queue.plan(canonical, source)
    paths = {'docs/audio/sound-queue.json'} | {p[0]['manifest'] for p in plans}
    existing = {p.relative_to(destination).as_posix() for p in destination.rglob('*') if p.is_file()}
    if check:
        if existing != paths or any(not (destination / p).is_file() or
                (destination / p).read_bytes() != (source / p).read_bytes() for p in paths):
            raise ValueError('Bundled sound inputs are stale; run tools/export_sound_inputs.py and commit them with the queue.')
    else:
        for p in existing - paths:
            (destination / p).unlink()
        for p in paths:
            target = destination / p
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / p, target)
    queue.plan(destination / 'docs/audio/sound-queue.json', destination)


if __name__ == '__main__':
    export(check='--check' in sys.argv)
    print('Sound builder inputs verified; no API requests.')
