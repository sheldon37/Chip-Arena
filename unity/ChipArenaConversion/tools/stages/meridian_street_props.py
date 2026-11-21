"""Meridian's solid street props for the DEV preview (docs/expedition/MERIDIAN-STREETS.md, part 2; Core NativeMeridianStreetProps):
every model of the contract's table, built on the approved board-3 street kit (meridian_street_kit/: the Vale, the benches, the
bin, the bollards, the shelter, the kiosk, the barricade and the sodium post, as the owner approved them) and a few new pieces in
its style (the 647 bus, the police Vale, the burnt wreck, the dumpster, the crowd barrier, the newsstand, the planter, the
hydrant), each checked against its stem's block: below 3.1 m every vertex within 0.25 m of the block, and the model reaching
within 0.25 m of each of its faces (it fills its block: no invisible wall), at LOD0 and LOD1.

  blender --background --factory-startup --python meridian_street_props.py -- --out ASSETS [--preview DIR] [--no-ao]

Writes ASSETS/ChipArenaLocal/Resources/ChipArenaCityStreetProps.bytes (rig/props/<model>/<tier>; never committed: the DEV preview
reads it in the Editor; it ships only once Astra's blocks exist). The models' frame is the street kit's: the origin on the floor
at the middle of the block, +z the front (a car's nose, a bench's seat, a shelter's open side).
"""
import argparse
import math
import os
import re
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(HERE, 'meridian_street_kit')
sys.path.insert(0, HERE)
sys.path.insert(0, KIT)
import numpy as np  # noqa: E402

import stagekit as sk  # noqa: E402
import mkit as mk  # noqa: E402
import kit2  # noqa: E402  (installs the Meridian fixes: outward sweeps, the loft's one sign)
import car  # noqa: E402
import furniture as fu  # noqa: E402
import lamps  # noqa: E402
import shelter  # noqa: E402
import story  # noqa: E402
from mkit import F, box, rod, glow  # noqa: E402

BANK = 'ChipArenaCityStreetProps'
CORE = os.path.join(HERE, '..', '..', 'Assets', 'ChipArena', 'Core', 'NativeMeridianStreetProps.cs')
CLEAR = 3.1


def stems():
    """The stems' blocks and their models (Core NativeMeridianStreetProps.Stems): model -> (stem, size x, y, z)."""
    src = open(CORE, encoding='utf-8').read()
    out = {}
    for m in re.finditer(r'new Stem\("([^"]+)",V\(([^)]*)\),[^,]+,"[^"]*",((?:"[^"]+",?)+)\)', src):
        size = tuple(float(v.strip().rstrip('f')) for v in m.group(2).split(','))
        for model in re.findall(r'"([^"]+)"', m.group(3)):
            out[model] = (m.group(1), size)
    if len(out) < 20:
        raise SystemExit('read only %d prop models from %s' % (len(out), CORE))
    return out


def extra_finishes():
    mk.finishes()

    def add(name, hexcol, detail=sk.DETAIL_NONE, **kw):
        if name not in F:
            F[name] = sk.fin(name, hexcol, detail, **kw)
    add('car-burnt', 0x1E1A18, smooth=.15, metal=.2, dirt=1.0, wear=.6)
    add('car-police', 0xE6E8E4, smooth=.8, metal=.3, dirt=.5, wear=.1)
    add('police-blue', 0x1E3C8A, smooth=.6, metal=.2, dirt=.4)
    add('lens-blue-dead', 0x14203A, smooth=.9, dirt=.2)
    add('bus-green', 0x1F5A40, smooth=.65, metal=.2, dirt=.8, wear=.2)
    add('bus-cream', 0xE6DCC0, smooth=.6, metal=.1, dirt=.8, wear=.2)
    add('bus-gilt', 0xC8A040, sk.DETAIL_BRASS, smooth=.7, metal=1.0, dirt=.3)
    add('steel-dump', 0x2E4A3A, sk.DETAIL_IRON, smooth=.35, metal=.5, dirt=1.2, wear=.4)
    add('steel-lid', 0x1E2A22, smooth=.4, metal=.3, dirt=1.0)
    add('barrier-galv', 0x9AA0A4, smooth=.5, metal=.8, dirt=.6, wear=.1)
    add('hazard-red', 0xB8261E, smooth=.4, dirt=.6)
    add('stand-green', 0x24463A, smooth=.45, metal=.2, dirt=.8, wear=.35)
    add('stone-trough', 0x8A8478, sk.DETAIL_FLAG, smooth=.15, dirt=1.2, moss=.3)
    add('soil-dark', 0x221A12, smooth=.1, dirt=.4)
    add('twig', 0x4A3A28, sk.DETAIL_WOOD, smooth=.2, dirt=.4)
    add('hydrant-red', 0xA8261E, smooth=.5, metal=.2, dirt=.8, wear=.3)
    add('paper-bundle', 0xD8D2C0, smooth=.15, dirt=.6)
    add('void', 0x08080A, smooth=.1, dirt=0)
    return F


def merged(m):
    """A street-kit module's parts in one Geo."""
    g = sk.Geo()
    for p in m.parts.values():
        g.extend(p)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# New pieces in the kit's style (module frame: the floor at y 0, the block's middle at x 0 z 0, the front +z)
# ---------------------------------------------------------------------------------------------------------------------------
def wheel(g, T, x, z, r, w, side):
    sk.cylinder(g, (x, r, z), (x + side * w, r, z), r, F['rubber'], T.round + 4)
    sk.cylinder(g, (x + side * (w + .002), r, z), (x + side * (w + .004), r, z), r * .55, F['chrome'], T.round, caps=True)


def bus_647(T):
    # the route 647 single-decker: green and cream with the gilt waistband, a glazed band, the blind reading 647, wheels
    g = sk.Geo()
    L, W, H = 10.8, 2.45, 2.95
    box(g, T, -W / 2, W / 2, .35, 1.05, -L / 2, L / 2, F['bus-green'], .06)
    box(g, T, -W / 2 + .01, W / 2 - .01, 1.05, 1.12, -L / 2 + .02, L / 2 - .02, F['bus-gilt'], 0)
    box(g, T, -W / 2, W / 2, 1.12, 2.15, -L / 2, L / 2, F['bus-cream'], .05)
    box(g, T, -W / 2 + .02, W / 2 - .02, 2.15, H - .08, -L / 2 + .04, L / 2 - .04, F['bus-green'], .08)
    box(g, T, -W / 2 + .1, W / 2 - .1, H - .08, H, -L / 2 + .3, L / 2 - .3, F['bus-green'], .03)
    # the windows down both sides and the windscreen
    n = 7 if T.lod == 0 else 1
    for side in (1, -1):
        x = side * (W / 2 + .004)
        for k in range(n):
            z0 = -L / 2 + .9 + (L - 2.0) * k / n
            z1 = z0 + (L - 2.0) / n - .12
            g.face(((x, 1.25, z0), (x, 1.25, z1), (x, 2.05, z1), (x, 2.05, z0)), F['glass-dark'], (side, 0, 0))
        wheel(g, T, side * (W / 2 - .28), L / 2 - 2.1, .48, .26, side)
        wheel(g, T, side * (W / 2 - .28), -L / 2 + 2.4, .48, .26, side)
    g.face(((-W / 2 + .15, 1.2, L / 2 + .004), (W / 2 - .15, 1.2, L / 2 + .004), (W / 2 - .15, 2.3, L / 2 + .004), (-W / 2 + .15, 2.3, L / 2 + .004)), F['glass-dark'], (0, 0, 1))
    if T.detail:
        # the destination blind: 647 BELVEDERE (lit dead), the headlamps, the bumper
        g.face(((-.75, 2.45, L / 2 + .006), (.75, 2.45, L / 2 + .006), (.75, 2.75, L / 2 + .006), (-.75, 2.75, L / 2 + .006)), F['ink'], (0, 0, 1))
        mk.text(g, mk.front((0, 0, L / 2 + .008)), 0, 2.52, .03, '647', F['bus-cream'], c=0.0)
        for x in (-.85, .85):
            sk.cylinder(g, (x, .62, L / 2), (x, .62, L / 2 + .03), .1, F['chrome'], T.round)
        box(g, T, -W / 2 + .05, W / 2 - .05, .3, .45, L / 2 - .02, L / 2 + .08, F['chrome'], .02)
        box(g, T, -W / 2 + .05, W / 2 - .05, .3, .45, -L / 2 - .08, -L / 2 + .02, F['chrome'], .02)
    return g


def police_car(T):
    g = merged(car.vale(T, 'police'))
    # the light bar, dark (the cordon long abandoned), and the force's blue band
    box(g, T, -.55, .55, 1.42, 1.5, -.12, .12, F['iron-black'], .01)
    for x in (-.45, -.1, .1, .45):
        box(g, T, x - .14, x + .14, 1.5, 1.58, -.1, .1, F['lens-blue-dead'], .01)
    for side in (1, -1):
        x = side * .862
        g.face(((x, .62, -1.6), (x, .62, 1.6), (x, .74, 1.6), (x, .74, -1.6)), F['police-blue'], (side, 0, 0))
    return g


def wreck(T, burnt):
    g = merged(car.vale(T, 'burnt' if burnt else 'cream', 'smashed'))
    if burnt and T.detail:
        # scorched ground under it (paint, within its block)
        g.face(((-.92, .004, -2.1), (.92, .004, -2.1), (.92, .004, 2.1), (-.92, .004, 2.1)), F['ink'], (0, 1, 0))
    return g


def dumpster(T, open_):
    # a commercial dumpster: a tapered steel body on its skids, two lids (one thrown back over the wall side), its pockets
    g = sk.Geo()
    box(g, T, -.98, .98, .12, 1.25, -.58, .58, F['steel-dump'], .03)
    box(g, T, -.95, .95, 0, .12, -.5, .5, F['iron-black'], .01)
    for x in (-.8, .8):
        box(g, T, x - .12, x + .12, .3, .5, .58, .62, F['iron-black'], .005)
    if open_:
        box(g, T, -.96, 0, 1.25, 1.3, -.6, .55, F['steel-lid'], .01)
        # the other lid stands open against the back (within the block's height)
        box(g, T, 0, .96, 1.25, 1.38, -.6, -.52, F['steel-lid'], .01)
        if T.detail:
            for k in range(3):
                mk.box(g, T, .1 + .25 * k, .3 + .25 * k, 1.12, 1.3, -.3 + .2 * (k % 2), -.05 + .2 * (k % 2), F['rubber'], .03)
    else:
        box(g, T, -.96, .96, 1.25, 1.3, -.6, .55, F['steel-lid'], .01)
    if T.detail:
        box(g, T, -.6, .6, .7, .95, .58, .6, F['bus-cream'], 0)
    return g


def barrier(T):
    # a steel crowd barrier: a frame of tube with uprights, on two flat feet (2.3 long along x, 0.6 deep at the feet)
    g = sk.Geo()
    s = T.small
    for x in (-1.1, 1.1):
        rod(g, (x, .1, 0), (x, 1.08, 0), .025, F['barrier-galv'], s)
        box(g, T, x - .05, x + .05, 0, .04, -.28, .28, F['barrier-galv'], .005)
        rod(g, (x, .1, 0), (x, .04, -.25), .018, F['barrier-galv'], s)
        rod(g, (x, .1, 0), (x, .04, .25), .018, F['barrier-galv'], s)
    for y in (.15, 1.06):
        rod(g, (-1.1, y, 0), (1.1, y, 0), .022, F['barrier-galv'], s)
    n = 13 if T.lod == 0 else 5
    for k in range(n):
        x = -1.0 + 2.0 * k / (n - 1)
        rod(g, (x, .15, 0), (x, 1.06, 0), .01, F['barrier-galv'], 4)
    if T.detail:
        # a CD sign wired to it, and the police tape knotted round the top rail
        box(g, T, -.3, .3, .55, .85, .015, .03, F['bus-cream'], 0)
        box(g, T, -.3, .3, .78, .85, .031, .034, F['hazard-red'], 0)
        box(g, T, -1.1, 1.1, .98, 1.02, -.03, .03, F['bus-cream'], 0)
    return g


def newsstand(T):
    # a news kiosk against the wall: a green box with its shutters down, a canopy, a lit-dead NEWS board, bundles at its foot
    g = sk.Geo()
    box(g, T, -1.18, 1.18, 0, 2.15, -.63, .6, F['stand-green'], .03)
    box(g, T, -1.2, 1.2, 2.15, 2.38, -.65, .64, F['stand-green'], .02)
    n = 14 if T.lod == 0 else 1
    for k in range(n):
        y0 = .9 + 1.15 * k / n
        y1 = .9 + 1.15 * (k + 1) / n - (.012 if n > 1 else 0)
        g.face(((-1.1, y0, .605), (1.1, y0, .605), (1.1, y1, .605), (-1.1, y1, .605)), F['barrier-galv'], (0, 0, 1))
    if T.detail:
        g.face(((-.9, 2.18, .645), (.9, 2.18, .645), (.9, 2.34, .645), (-.9, 2.34, .645)), F['bus-cream'], (0, 0, 1))
        mk.text(g, mk.front((0, 0, .646)), 0, 2.2, .022, 'NEWS', F['ink'], c=0.0)
        for k in range(3):
            box(g, T, -.9 + .55 * k, -.5 + .55 * k, 0, .28, .3, .6, F['paper-bundle'], .02)
    return g


def planter(T):
    # a stone trough, the shrub in it long dead to stubs under its rim
    g = sk.Geo()
    box(g, T, -.75, .75, 0, .9, -.5, .5, F['stone-trough'], .03)
    box(g, T, -.65, .65, .8, .86, -.4, .4, F['soil-dark'], 0)
    if T.detail:
        for k in range(7):
            a = 2 * math.pi * k / 7
            x, z = math.cos(a) * .3, math.sin(a) * .2
            rod(g, (x * .4, .86, z * .4), (x, .9 - .01, z), .012, F['twig'], 3)
    return g


def hydrant(T):
    # a pillar hydrant: a red barrel with its cap and two outlets
    g = sk.Geo()
    mk.lathe(g, [(.17, 0), (.17, .06), (.13, .08), (.12, .62), (.14, .66), (.1, .74), (.05, .82), (0, .84)], (0, 0, 0), F['hydrant-red'], T.round + 2, cap0=True)
    for side in (1, -1):
        rod(g, (0, .45, 0), (side * .19, .45, 0), .045, F['hydrant-red'], T.small)
        rod(g, (side * .19, .45, 0), (side * .2, .45, 0), .055, F['chrome'], T.small)
    if T.detail:
        rod(g, (0, .45, 0), (0, .45, .19), .055, F['hydrant-red'], T.small)
    return g


BUILD = {
    'car.blue': lambda T: merged(car.vale(T, 'blue')), 'car.cream': lambda T: merged(car.vale(T, 'cream')),
    'car.maroon': lambda T: merged(car.vale(T, 'maroon')), 'car.green': lambda T: merged(car.vale(T, 'green')),
    'car.mustard': lambda T: merged(car.vale(T, 'mustard')), 'car.white': lambda T: merged(car.vale(T, 'white')),
    'car.smashed': lambda T: merged(car.vale(T, 'mustard', 'smashed')), 'car.rack': lambda T: merged(car.vale(T, 'maroon', 'rack')),
    'wreck.burnt': lambda T: wreck(T, True), 'wreck.crashed': lambda T: wreck(T, False), 'police.car': police_car,
    'bus.647': bus_647, 'shelter.cast': lambda T: merged(shelter.shelter(T)),
    'bench': lambda T: merged(fu.bench(T)), 'bench.broken': lambda T: merged(fu.bench(T, 'broken')), 'bench.blanket': lambda T: merged(fu.bench(T, 'blanket')),
    'bin': lambda T: merged(fu.litter_bin(T)), 'dumpster': lambda T: dumpster(T, False), 'dumpster.open': lambda T: dumpster(T, True),
    'barricade.furniture': lambda T: merged(story.barricade_furniture(T)).transformed([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -.175]), 'barrier': barrier, 'newsstand': newsstand,
    'kiosk.m': lambda T: merged(shelter.kiosk(T)), 'planter': planter,
    'bollard.cannon': lambda T: merged(fu.bollard(T, 'cannon')), 'bollard.sleeve': lambda T: merged(fu.bollard(T, 'sleeve')),
    'hydrant': hydrant, 'lamp.post': lambda T: merged(lamps.sodium(T)), 'lamp.post.dead': lambda T: merged(lamps.sodium(T, state='dead')),
}


def check(model, stem, size, g):
    """The honesty rule on a model against its block: below 3.1 m every vertex within 0.25 m of the block (and none under the
    floor); the model reaching within 0.25 m of each of the block's sides and its top (it fills the block)."""
    P, _, _, _ = g.arrays()
    if not len(P):
        return '%s: no geometry' % model
    hx, hy, hz = size[0] / 2, size[1], size[2] / 2
    low = P[P[:, 1] < CLEAR]
    bad = []
    if len(low):
        if low[:, 0].min() < -hx - .25 or low[:, 0].max() > hx + .25 or low[:, 2].min() < -hz - .25 or low[:, 2].max() > hz + .25:
            bad.append('stands more than 0.25 m out of its block below 3.1 m (x %.2f..%.2f, z %.2f..%.2f against %.2f, %.2f)' %
                       (low[:, 0].min(), low[:, 0].max(), low[:, 2].min(), low[:, 2].max(), hx, hz))
    if P[:, 1].min() < -.01:
        bad.append('goes under the floor (%.3f)' % P[:, 1].min())
    reach = (P[:, 0].min(), P[:, 0].max(), P[:, 2].min(), P[:, 2].max(), P[:, 1].max())
    want = (-hx + .25, hx - .25, -hz + .25, hz - .25, hy - .25)
    if reach[0] > want[0] or reach[1] < want[1] or reach[2] > want[2] or reach[3] < want[3] or reach[4] < want[4]:
        bad.append('does not fill its block within 0.25 m (x %.2f..%.2f, z %.2f..%.2f, top %.2f; block %.2f x %.2f x %.2f)' %
                   (reach[0], reach[1], reach[2], reach[3], reach[4], size[0], size[1], size[2]))
    return ('%s (%s): ' % (model, stem) + '; '.join(bad)) if bad else None


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out')
    ap.add_argument('--preview')
    ap.add_argument('--no-ao', action='store_true')
    args = ap.parse_args(argv)
    S = stems()
    missing = sorted(set(S) - set(BUILD))
    if missing:
        raise SystemExit('no builder for %s' % ', '.join(missing))
    extra_finishes()
    sb = sk.StageBank()
    t0 = time.time()
    models = sorted(S)
    report = []
    for lod, ti in ((0, 0), (1, 2)):
        T = mk.LOD(lod)
        geos, bad = [], []
        for m in models:
            g = BUILD[m](T)
            why = check(m, S[m][0], S[m][1], g)
            if why:
                bad.append(why)
            geos.append(g)
        if bad:
            raise SystemExit(chr(10).join(bad))
        aos = [None] * len(geos)
        if not args.no_ao:
            moved = [g.transformed([1, 0, 0, 30.0 * n, 0, 1, 0, 0, 0, 0, 1, 0]) for n, g in enumerate(geos)]
            aos = sk.bake_ao(moved, distance=.7, samples=32 if lod == 0 else 12)
        pv = []
        for m, g, ao in zip(models, geos, aos):
            if ao is not None:
                ao = 1 - (1 - ao) * .85
            bk = sk.Bake(g, ao, floors=(0.0,), seed=abs(hash(m)) % 997)
            name = 'rig/props/%s/%d' % (m, ti)
            sb.add(name, 'rig', sk.to_mesh(name, g, bk))
            if lod == 0:
                sb.share('rig/props/%s/1' % m, 'rig', len(sb.bank.meshes) - 1)
            _, _, Sz, _ = g.arrays()
            report.append((m, lod, int((Sz - 2).sum())))
            pv.append((g, bk))
        print('tier %d: %d models, %.1f s' % (ti, len(models), time.time() - t0))
        if args.preview and lod == 0:
            laid = []
            for n, ((g, bk), m) in enumerate(zip(pv, models)):
                laid.append((g.transformed([1, 0, 0, (n % 8) * 6.0, 0, 1, 0, 0, 0, 0, 1, -(n // 8) * 13.0]), bk))
            rows = (len(models) + 7) // 8
            views = [('props-%d' % r, (21.0, 4.0, -(r * 13.0) - 14.0), (21.0, 1.2, -(r * 13.0)), 55) for r in range(rows)]
            views.append(('props-above', (21.0, 40.0, -10.0), (21.0, 0.0, -9.0), 60))
            sk.preview(laid, views, args.preview, size=(1400, 700))
    for m in models:
        tri = [t for (mm, l, t) in report if mm == m]
        print('  %-20s %-9s %6d / %5d' % (m, S[m][0], tri[0], tri[1]))
    if args.out:
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
