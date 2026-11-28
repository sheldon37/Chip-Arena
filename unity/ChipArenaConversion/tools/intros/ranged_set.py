"""The ranged intros' set bank (docs/INTROS-RANGED.md): Sundown (pistol), One Breath (rifle), Brass Storm (machine gun) and
Lightfall (bow), modelled in Blender into one local bank, ChipArenaIntroRanged (git-ignored, never committed).

  blender --background --factory-startup --python tools/intros/ranged_set.py -- --out <folder> [--sets sundown,onebreath,...]
          [--tiers 0,2] [--no-ao] [--preview DIR] [--views a,b]

The bank lands in <folder>/ChipArenaLocal/Resources/ChipArenaIntroRanged.bytes (copy it into Assets/LocalAssets to see the sets
in the Editor). Each set's module lists its props and the places the intros aim at; Core NativeRangedSets mirrors those places.
"""
import argparse
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ranged_kit as rk  # noqa: E402

BANK = 'ChipArenaIntroRanged'
SETS = ('sundown', 'onebreath', 'brass', 'lightfall')


def module(name):
    import importlib
    return importlib.import_module({'sundown': 'ranged_sundown', 'onebreath': 'ranged_onebreath', 'brass': 'ranged_brass', 'lightfall': 'ranged_lightfall'}[name])


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', help='the folder to write ChipArenaLocal/Resources/%s.bytes into' % BANK)
    ap.add_argument('--sets', default=','.join(SETS))
    ap.add_argument('--tiers', default='0,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--preview', help='render views of the Fine tier into this folder')
    ap.add_argument('--views', help='only these views (comma separated)')
    args = ap.parse_args(argv)
    t0 = time.time()
    out = rk.Out()
    out.ao = not args.no_ao
    views = []
    for name in args.sets.split(','):
        m = module(name)
        m.finishes()
    for name in args.sets.split(','):
        m = module(name)
        for ti in [int(x) for x in args.tiers.split(',')]:
            T = rk.sk.Tier(ti)
            t = time.time()
            m.build(out, T)
            print('%s tier %s: %.1f s' % (name, T.name, time.time() - t))
        views += m.views()
    out.report()
    if args.preview:
        vs = views
        if args.views:
            vs = [v for v in vs if v[0] in args.views.split(',')]
        rk.sk.preview(out.preview, vs, args.preview)
    if args.out:
        path = out.write(args.out, BANK)
        print('bank', path, os.path.getsize(path), 'bytes')
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
