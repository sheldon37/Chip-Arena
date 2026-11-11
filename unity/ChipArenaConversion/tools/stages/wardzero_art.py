"""Ward Zero's stage art (docs/arenas/STAGE-ART.md, docs/arenas/WARDZERO-ART.md): the quarantine ward ("siren", and the Expedition's
"expedition-ward-zero", which shares the bank) modelled on its map blocks and its Core kit's parts, into the stage bank
ChipArenaStageWardZero. Run inside Blender 5.x:

  blender --background --factory-startup --python tools/stages/wardzero_art.py -- --layout SIREN.json [--also EXPEDITION.json]
      --out <Assets>/LocalAssets [--preview DIR [--views a,b]] [--tiers 0,1,2] [--no-ao] [--stats]

(the layouts: CoreTests `--stage-layout siren SIREN.json` and `--stage-layout expedition-ward-zero EXPEDITION.json`; --also adds
the blocks only the Expedition's record has: its exit frame).

What is where in the bank:
- static/<chunk>/<tier>: the floor with its vent pits, the roof's ceiling with its beams, ducts, trays and light housings, the
  shell's clad faces, the cell and observation rows, the walls that never leave, the decks, bridges and gantries, the rails, the
  stairs and ramps, the fittings (tanks, benches, chambers, lockers, showers, consoles, racks, the nurse booths, the furnaces, the
  vessel's shards), and every steady kit part as a finished piece.
- rig/b/<block id>/<tier>: a block that can leave, in its own frame (its centre): the interior walls a Hulk may break out, the
  breach panels, the Expedition's exit frame (Core NativeWardZeroArt.Posed), each with the kit's steady parts on it.
- rig/m<N>/<tier>: a mover's model (Core NativeWardZeroKit.Movers), in the frame of its group's origin at rest (the layout's
  mover `at`): grate lids, louvres, gate bars, door leaves, hatch leaves, shutter slats.
- block/<block id>/<tier>: the breakable gurneys and carts, in the block's frame.
- cover/<block id>: every map block (the art replaces all the dressed boxes).
The kit's lit and animated parts (the layout's `kept`) are not modelled: they still draw as their boxes over the art, so the art
keeps clear of them (cladding stands 5 to 7 mm proud where the kit's ink and lettering stand 12 mm and more)."""
import argparse
import math
import os
import sys
import time
from collections import defaultdict

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, obox, sweep, lathe, cylinder  # noqa: E402
import club_kit as ck  # noqa: E402
import wardzero_kit as wk  # noqa: E402
from wardzero_kit import F, Face, ext, stem, upright, rod  # noqa: E402

BANK = 'ChipArenaStageWardZero'
UPPER, ROOF = 6.0, 13.0
STATS = defaultdict(int)
COUNT = False


class Scene:
    """One tier's geometry by destination: the static scenery, the posed blocks, the breakable props and the movers' models
    (all in the world, at rest)."""

    def __init__(self, L, T):
        self.L, self.T = L, T
        self.static = Geo()
        self.B = {b['id']: b for b in L['blocks']}
        self.posed = {i: Geo() for i in L['posed']}
        self.props = {b['id']: Geo() for b in L['blocks'] if b['role'] != 'static'}
        self.movers = {}
        self.where = {}

    def geo_of(self, owner):
        if owner in self.posed:
            return self.posed[owner]
        if owner in self.props:
            return self.props[owner]
        return self.static


KEYS = ('-x', '+x', '-y', '+y', '-z', '+z')
COVER = []          # the static upright blocks' boxes (what can hide a face for good)
BOUNDS = None


def set_cover(L):
    global BOUNDS
    BOUNDS = L['bounds']
    posed = set(L['posed'])
    del COVER[:]
    for b in L['blocks']:
        if b['role'] == 'static' and upright(b) and b['id'] not in posed:
            COVER.append((ext(b), b['id']))


def hidden(b):
    """The faces of an upright block that a static block (or the world outside the bounds) covers whole: never seen, and left
    out so that no two faces lie in one plane. A face against a block that can leave (a posed wall) stays."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    out = []
    for key in KEYS:
        ax = 'xyz'.index(key[1])
        lo, hi = [x0, y0, z0], [x1, y1, z1]
        at = (lo[ax] - .012) if key[0] == '-' else (hi[ax] + .012)
        o = [i for i in range(3) if i != ax]
        ok = True
        for (s, t) in ((.5, .5), (.04, .04), (.96, .04), (.04, .96), (.96, .96), (.5, .04), (.5, .96), (.04, .5), (.96, .5)):
            p = [0, 0, 0]
            p[ax] = at
            p[o[0]] = lo[o[0]] + (hi[o[0]] - lo[o[0]]) * s
            p[o[1]] = lo[o[1]] + (hi[o[1]] - lo[o[1]]) * t
            if p[0] < BOUNDS['minX'] or p[0] > BOUNDS['maxX'] or p[2] < BOUNDS['minZ'] or p[2] > BOUNDS['maxZ']:
                continue
            if not any(e[0] <= p[0] <= e[1] and e[2] <= p[1] <= e[3] and e[4] <= p[2] <= e[5] for e, i in COVER if i != b['id']):
                ok = False
                break
        if ok:
            out.append(key)
    return tuple(out)


def fkey(fc):
    return ('-' if fc.sign < 0 else '+') + ('x' if fc.axis == 0 else 'z')


def bbox(g, b, f, T, bevel=.02, extra=()):
    """A block's own box in a finish, less its hidden faces (and its underside on a floor)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    skip = set(hidden(b)) | set(extra)
    if level(y0) is not None:
        skip.add('-y')
    box(g, x0, x1, y0, y1, z0, z1, f, T, bevel if not (skip - {'-y'}) else 0, cell=0, skip=tuple(skip))


def tally(g, key, n0):
    if COUNT:
        STATS[key] += g.faces - n0


def level(y):
    """The floor a height stands on (0 or the upper floor), or None."""
    for f in (0.0, UPPER):
        if abs(y - f) < .06:
            return f
    return None


# ---------------------------------------------------------------------------------------------------------------------------
# Block bodies
# ---------------------------------------------------------------------------------------------------------------------------
def solid_block(g, b, T, body=None, top=None, panel=None, cladding=True, bevel=.012):
    """A wall or a solid mass: its dark body with every tall face clad (skirting, dado, panels) and its narrow ends plain."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    body = F['reveal'] if body is None else body
    fl = level(y0)
    hid = hidden(b)
    skip = list(hid) + (['-y'] if fl is not None else [])
    if y1 > ROOF - .05:
        skip.append('+y')
    if T.i == 2 and cladding:
        skip += ['-x', '+x', '-z', '+z']
    box(g, x0, x1, y0, y1, z0, z1, body, T, 0, cell=0, skip=tuple(set(skip)))
    if top is not None:
        box(g, x0 - .025, x1 + .025, y1, y1 + .05, z0 - .025, z1 + .025, top, T, .015, cell=0, skip=('-y',))
    if not cladding:
        return
    for fc, u0, u1 in wk.faces_of(b):
        if fkey(fc) in hid:
            continue
        if u1 - u0 < .7:
            fc.grid(g, u0, u1, y0, y1, .006, F['panel'] if panel is None else panel, 1.2 if T.i < 2 else 3.0)
        else:
            wk.clad(g, fc, u0, u1, y0, y1, T, floor=fl, panel=panel)


def shell(g, b, T, bounds):
    """An outer wall: only its inner face, from the slab to the roof."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    if b['size'][0] < b['size'][2]:
        s = -1 if b['pos'][0] > 0 else 1
        fc, u0, u1 = Face(0, s, x0 if s < 0 else x1), bounds['minZ'], bounds['maxZ']
    else:
        s = -1 if b['pos'][2] > 0 else 1
        fc, u0, u1 = Face(2, s, z0 if s < 0 else z1), bounds['minX'], bounds['maxX']
    for (a, c) in ((0.0, UPPER - .4), (UPPER, ROOF)):
        if T.i == 2:
            fc.quad(g, u0, u1, a, c, 0, F['reveal'])
        else:
            n = max(1, int((u1 - u0) / 8))
            for i in range(n):
                fc.quad(g, u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n, a, c, 0, F['reveal'])
        wk.clad(g, fc, u0, u1, a, c, T, floor=a)
    fc.quad(g, u0, u1, UPPER - .4, UPPER, 0, F['steeldark'])


def slab_block(g, b, T, top, bottom, side, holes=(), cell=None, pit=.32):
    """A floor slab or a deck: its top (with the vents' openings and their dark pits), its soffit and its edges."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (1.0, 3.0, 6.0)[T.i] if cell is None else cell
    wk.floor(g, x0, x1, z0, z1, y1, top, c, holes)
    for h in holes:
        d = y1 - min(pit, (y1 - y0) - .04)
        box_in(g, h[0], h[1], d, y1, h[2], h[3], F['void'])
    if bottom is not None:
        wk.floor(g, x0, x1, z0, z1, y0, bottom, c * 2, up=False)
    if side is not None:
        hid = hidden(b)
        for fc, u0, u1 in wk.faces_of(b):
            if fkey(fc) not in hid:
                fc.quad(g, u0, u1, y0, y1, 0, side)


def box_in(g, x0, x1, y0, y1, z0, z1, f):
    """The inside of a pit: its four walls and its bottom, facing in."""
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), f, (0, 1, 0))
    g.face(((x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)), f, (1, 0, 0))
    g.face(((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)), f, (-1, 0, 0))
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)), f, (0, 0, 1))
    g.face(((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)), f, (0, 0, -1))


def vent_holes(L, b):
    """The floor vents' openings in a block's top (the kit's shaft plate is 1.9 m square)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    out = []
    for e in L['entries']:
        if e['kind'] == 'vent' and e['surface'] == 'floor' and abs(e['from'][1] - y1) < .08:
            x, z = e['from'][0], e['from'][2]
            if x0 < x < x1 and z0 < z < z1:
                out.append((x - .92, x + .92, z - .92, z + .92))
    return out


def ceiling(g, b, T, L):
    """The roof's underside: a dark ceiling, deep steel beams one way and purlins the other between the light fixtures, cut
    round the hatches and vents."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    y = y0
    bd = L['bounds']
    wk.floor(g, bd['minX'], bd['maxX'], bd['minZ'], bd['maxZ'], y, F['ceiling'], (2.4, 4.8, 9.6)[T.i], up=False)
    keep = []
    for p in L['parts']:
        if p['o'] == b['id'] and p['f'] in ('hatch', 'ceiling-vent'):
            keep.append((p['c'][0] - p['h'][0] - .12, p['c'][0] + p['h'][0] + .12, p['c'][2] - p['h'][2] - .12, p['c'][2] + p['h'][2] + .12))

    def spans(lo, hi, fixed, along_x):
        cuts = []
        for k in keep:
            a, c = (k[2], k[3]) if along_x else (k[0], k[1])
            if a - .15 < fixed < c + .15:
                cuts.append((k[0], k[1]) if along_x else (k[2], k[3]))
        out, at = [], lo
        for a, c in sorted(cuts):
            if a > at:
                out.append((at, a))
            at = max(at, c)
        if at < hi:
            out.append((at, hi))
        return out
    if T.i < 2:
        z = -28 + 2.4
        while z < bd['maxZ']:
            for a, c in spans(bd['minX'], bd['maxX'], z, True):
                ck.ibeam(g, [(a, y - .21, z), (c, y - .21, z)], .42, .22, F['beam'], up=(0, 1, 0), caps=False)
            z += 4.8
    if T.i == 0:
        x = -36 + 2.4
        while x < bd['maxX']:
            for a, c in spans(bd['minZ'], bd['maxZ'], x, False):
                # in bays, from one beam's flange to the next
                zs = [a] + [zz for zz in [-28 + 2.4 + 4.8 * k for k in range(13)] if a < zz < c] + [c]
                for k in range(len(zs) - 1):
                    lo_, hi_ = zs[k] + (.115 if k > 0 else 0), zs[k + 1] - (.115 if k < len(zs) - 2 else 0)
                    if hi_ - lo_ > .2:
                        box(g, x - .06, x + .06, y - .2, y, lo_, hi_, F['beam'], None, 0, cell=0, skip=('+y', '-z', '+z'))
            x += 4.8


def deck(g, b, T, L, metal=False):
    x0, x1, y0, y1, z0, z1 = ext(b)
    holes = vent_holes(L, b)
    slab_block(g, b, T, F['grate'] if metal else F['tileup'], F['beam'] if metal else F['soffit'], F['steeldark'], holes)
    if metal and T.i < 2:
        # cross members under the grating
        long_z = b['size'][2] > b['size'][0]
        n = int((b['size'][2] if long_z else b['size'][0]) / 2.0)
        for i in range(1, n):
            if long_z:
                z = z0 + (z1 - z0) * i / n
                box(g, x0 + .1, x1 - .1, y0 - .12, y0, z - .04, z + .04, F['beam'], T, .008, cell=0, skip=('+y',))
            else:
                x = x0 + (x1 - x0) * i / n
                box(g, x - .04, x + .04, y0 - .12, y0, z0 + .1, z1 - .1, F['beam'], T, .008, cell=0, skip=('+y',))


def ribs(g, b, T, L):
    """Downstand ribs under a deck, across its short way every 4 m between the troffers' rows, cut round whatever the kit
    hangs under it (vents, hatches, lights)."""
    if T.i > 0:
        return
    x0, x1, y0, y1, z0, z1 = ext(b)
    long_x = (x1 - x0) >= (z1 - z0)
    keep = []
    for p in L['parts']:
        if p['o'] == b['id'] and p['c'][1] < y0 + .05:
            keep.append((p['c'][0] - p['h'][0] - .1, p['c'][0] + p['h'][0] + .1, p['c'][2] - p['h'][2] - .1, p['c'][2] + p['h'][2] + .1))
    a0, a1, c0, c1 = (x0, x1, z0, z1) if long_x else (z0, z1, x0, x1)
    t = a0 + 4.0
    while t < a1 - .5:
        cuts = sorted(((k[2], k[3]) if long_x else (k[0], k[1])) for k in keep if ((k[0] if long_x else k[2]) - .08 < t < (k[1] if long_x else k[3]) + .08))
        at = c0
        spans = []
        for lo, hi in cuts:
            if lo > at:
                spans.append((at, lo))
            at = max(at, hi)
        if at < c1:
            spans.append((at, c1))
        for lo, hi in spans:
            if hi - lo < .3:
                continue
            if long_x:
                box(g, t - .07, t + .07, y0 - .16, y0, lo, hi, F['beam'], None, 0, cell=0, skip=('+y', '-z', '+z'))
            else:
                box(g, lo, hi, y0 - .16, y0, t - .07, t + .07, F['beam'], None, 0, cell=0, skip=('+y', '-x', '+x'))
        t += 4.0


def rail_block(g, b, T, kick):
    x0, x1, y0, y1, z0, z1 = ext(b)
    if kick:
        box(g, x0, x1, y0, y1, z0, z1, F['steeldark'], T, .012, cell=0, skip=('-y',))
        return
    along_x = b['size'][0] > b['size'][2]
    c = b['pos']
    a = (x0, c[1], c[2]) if along_x else (c[0], c[1], z0)
    e = (x1, c[1], c[2]) if along_x else (c[0], c[1], z1)
    rod(g, a, e, .06, F['stainless'], (10, 6, 5)[T.i])


def tread(g, b, T, route):
    """A stair tread: the step itself (its top and riser) and the flight's solid sides down to the floor."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    ux, uz = route['ux'], route['uz']
    rise = min(.25, y1 - y0)
    back = ('+x' if ux > 0 else '-x') if ux != 0 else ('+z' if uz > 0 else '-z')
    box(g, x0, x1, y1 - rise, y1, z0, z1, F['tile'], T, .012, cell=0, skip=('-y', back))
    if y1 - rise - y0 > .01:
        for fc, u0, u1 in wk.faces_of(b):
            side = (fc.axis == 0) == (ux == 0)
            if side:
                fc.grid(g, u0, u1, y0, y1 - rise, 0, F['panel'], 1.0 if T.i < 2 else 3.0)
    # the last tread's back (under the landing) closes the flight
    if abs(y1 - UPPER) < .01:
        fc = Face(0, 1 if ux > 0 else -1, x1 if ux > 0 else x0) if ux != 0 else Face(2, 1 if uz > 0 else -1, z1 if uz > 0 else z0)
        u0, u1 = (z0, z1) if ux != 0 else (x0, x1)
        fc.grid(g, u0, u1, y0, y1, 0, F['panel'], 1.0 if T.i < 2 else 3.0)


def ramp_deck(g, b, T):
    hx, hy, hz = [v / 2 for v in b['size']]
    t, done = wk.local(g, b['rot'], b['pos'])
    box(t, -hx, hx, -hy, hy, -hz, hz, F['concrete'], T, .012, cell=0)
    done()


# ---------------------------------------------------------------------------------------------------------------------------
# Fittings (the block's body; the kit's steady parts on it follow from the parts' pass)
# ---------------------------------------------------------------------------------------------------------------------------
def inner_face(b):
    """The face of a fitting that looks into the room: (Face, u0, u1), by where the block stands (as the kit picks it)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    if b['size'][0] < b['size'][2]:
        s = -1 if b['pos'][0] > 0 else 1
        return Face(0, s, x0 if s < 0 else x1), z0, z1
    s = -1 if b['pos'][2] > 0 else 1
    return Face(2, s, z0 if s < 0 else z1), x0, x1


def cabinet(g, b, T, body, front=None, module=.6, top=None, toe=True):
    """A cabinet body: a chamfered case, door plates on its long faces and (toe) a dark recessed plinth line."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    bbox(g, b, body, T)
    if T.i == 2 or front is None:
        return
    hid = hidden(b)
    for fc, u0, u1 in wk.faces_of(b):
        if u1 - u0 < .9 or fkey(fc) in hid:
            continue
        n = max(1, int(round((u1 - u0) / module)))
        for i in range(n):
            a, c = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
            wk.plate(g, fc, a + .02, c - .02, y0 + (.12 if toe else .03), y1 - .05, .008, front, T)
            if T.tiny:
                fc.box(g, c - .1, c - .06, (y0 + y1) / 2 - .08, (y0 + y1) / 2 + .08, .008, .03, F['steeldark'], None, .006, skip=('back',))


def tank(g, b, T):
    """A specimen tank's steelwork round the kit's lit fluid: a dark core should the fluid ever be left out."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0 + .06, x1 - .06, y0, y1, z0 + .06, z1 - .06, F['steeldark'], T, 0, cell=0, skip=('-y', '+y'))


def furnace(g, b, T):
    bbox(g, b, F['steeldark'], T, .03)


def shard(g, b, T, seed):
    """A shard of the Siren's vessel: a thick wall of smoked armour glass standing in a steel shoe, its top broken off jagged."""
    hx, hy, hz = [v / 2 for v in b['size']]
    t, done = wk.local(g, b['rot'], b['pos'])
    rnd = np.random.RandomState(seed)
    n = 8 if T.i < 2 else 3
    tops = [hy - .02 - .3 * rnd.rand() for _ in range(n + 1)]
    xs = [-hx + 2 * hx * i / n for i in range(n + 1)]
    f = F['vessel']
    bow = [.1 * (1 - (x / hx) ** 2) for x in xs]            # the vessel's round: the wall bows a little along its length
    for i in range(n):
        for s in (-1, 1):
            na, nb = (-.25 * xs[i] / hx, .08, s), (-.25 * xs[i + 1] / hx, .08, s)
            pts = ((xs[i], -hy + .16, s * hz * .85 + bow[i] - .05), (xs[i + 1], -hy + .16, s * hz * .85 + bow[i + 1] - .05),
                   (xs[i + 1], tops[i + 1], s * hz * .55 + bow[i + 1] - .05), (xs[i], tops[i], s * hz * .55 + bow[i] - .05))
            t.face(pts, f, normals=(na, nb, nb, na))
        t.face(((xs[i], tops[i], -hz * .55 + bow[i] - .05), (xs[i + 1], tops[i + 1], -hz * .55 + bow[i + 1] - .05),
                (xs[i + 1], tops[i + 1], hz * .55 + bow[i + 1] - .05), (xs[i], tops[i], hz * .55 + bow[i] - .05)), F['glass'], (0, 1, 0))
    for s, x, top in ((-1, -hx, tops[0]), (1, hx, tops[-1])):
        t.face(((x, -hy + .16, -hz), (x, -hy + .16, hz), (x, top, hz * .7), (x, top, -hz * .7)), f, (s, 0, 0))
    box(t, -hx - .03, hx + .03, -hy, -hy + .18, -hz - .03, hz + .03, F['steeldark'], T, .03, cell=0, skip=('-y',))
    if T.detail:
        for x in (-hx * .5, hx * .5):
            box(t, x - .05, x + .05, -hy + .16, min(tops) - .1, -hz - .02, hz + .02, F['steel'], T, .015, cell=0, skip=('-y',))
    done()


def gurney(g, b, T):
    """A transport stretcher (breakable cover): a solid fairing under a tube frame, a mattress and pillow, side rails, castors."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    lx = b['size'][0] > b['size'][2]
    box(g, x0 + .1, x1 - .1, y0 + .1, y1 - .34, z0 + .08, z1 - .08, F['fairing'], T, .04, cell=0, skip=())
    box(g, x0 + .02, x1 - .02, y1 - .36, y1 - .28, z0 + .02, z1 - .02, F['steel'], T, .02, cell=0)
    ck.rbox(g, x0 + .06, x1 - .06, y1 - .28, y1 - .1, z0 + .06, z1 - .06, .06, F['mattress'], 2 if T.i == 0 else 1)
    px = (x0 + .12, x0 + .62) if lx else (x0 + .12, x1 - .12)
    pz = (z0 + .12, z1 - .12) if lx else (z0 + .12, z0 + .62)
    ck.rbox(g, px[0], px[1], y1 - .13, y1 - .01, pz[0], pz[1], .05, F['white'], 2 if T.i == 0 else 1)
    for sx in (x0 + .14, x1 - .14):
        for sz in (z0 + .12, z1 - .12):
            rod(g, (sx, y0 + .1, sz), (sx, y1 - .36, sz), .025, F['steel'], T.small)
            rod(g, (sx - (.03 if lx else 0), y0 + .07, sz - (0 if lx else .03)), (sx + (.03 if lx else 0), y0 + .07, sz + (0 if lx else .03)), .07, F['rubber'], T.round if T.i == 0 else 6)
    if T.detail:
        # the side rails: a tube loop along each long side
        for s in (-1, 1):
            if lx:
                z = (z0 + .03) if s < 0 else (z1 - .03)
                path = [(x0 + .5, y1 - .3, z), (x0 + .5, y1 - .02, z), (x1 - .3, y1 - .02, z), (x1 - .3, y1 - .3, z)]
            else:
                x = (x0 + .03) if s < 0 else (x1 - .03)
                path = [(x, y1 - .3, z0 + .5), (x, y1 - .02, z0 + .5), (x, y1 - .02, z1 - .3), (x, y1 - .3, z1 - .3)]
            sk.tube(g, path, .016, F['stainless'], T.small, caps=True)


def cart(g, b, T):
    """An equipment cart (breakable cover): a drawer cabinet on castors, a rimmed top tray with canisters, a push bar."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    lx = b['size'][0] >= b['size'][2]
    ck.rbox(g, x0 + .03, x1 - .03, y0 + .12, y1 - .1, z0 + .03, z1 - .03, .04, F['white'], 2 if T.i == 0 else 1)
    box(g, x0, x1, y1 - .1, y1 - .04, z0, z1, F['stainless'], T, .015, cell=0)
    if T.i < 2:
        for fc, u0, u1 in wk.faces_of(b):
            if (u1 - u0) < max(b['size'][0], b['size'][2]) - .01:
                continue
            n = max(1, int(round((u1 - u0) / .55)))
            for i in range(n):
                for j in range(3):
                    a, c = u0 + .05 + (u1 - u0 - .1) * i / n, u0 + .05 + (u1 - u0 - .1) * (i + 1) / n
                    ya = y0 + .16 + (y1 - y0 - .32) * j / 3
                    wk.plate(g, fc, a + .015, c - .015, ya + .01, ya + (y1 - y0 - .32) / 3 - .01, -.022, F['stainless'], T)
    for sx in (x0 + .1, x1 - .1):
        for sz in (z0 + .1, z1 - .1):
            rod(g, (sx, y0 + .06, sz - .025), (sx, y0 + .06, sz + .025), .06, F['rubber'], T.round if T.i == 0 else 6)
            rod(g, (sx, y0 + .06, sz), (sx, y0 + .14, sz), .02, F['steel'], T.small)
    cx, cz = b['pos'][0], b['pos'][2]
    k = 3
    for i in range(k):
        t = (i - 1) * .28
        x, z = (cx + t, cz) if lx else (cx, cz + t)
        f = F['red'] if i == 1 else F['white']
        lathe(g, [(.085, 0), (.085, .16), (.05, .2), (.03, .24), (.03, .27)], (x, y1 - .04, z), f, T.small + 2, cap1=True)
    if T.detail:
        if lx:
            sk.tube(g, [(x1 - .02, y1 - .2, z0 + .1), (x1 + .0, y1 + .02, z0 + .1), (x1 + .0, y1 + .02, z1 - .1), (x1 - .02, y1 - .2, z1 - .1)], .016, F['stainless'], T.small)
        else:
            sk.tube(g, [(x0 + .1, y1 - .2, z1 - .02), (x0 + .1, y1 + .02, z1), (x1 - .1, y1 + .02, z1), (x1 - .1, y1 - .2, z1 - .02)], .016, F['stainless'], T.small)


def booth(g, b, T):
    """The nurse booth: its desk-high base, mullioned glazing (the kit's lit panes), corner posts."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['steeldark'], T, 0, cell=0, skip=('-y',))
    if T.i < 2:
        for x in (x0, x1):
            for z in (z0, z1):
                rod(g, (x, y0, z), (x, y1 + .06, z), .05, F['steel'], T.round if T.i == 0 else 6)
        for fc, u0, u1 in wk.faces_of(b):
            n = max(1, int(round((u1 - u0) / 1.2)))
            for i in range(1, n):
                u = u0 + (u1 - u0) * i / n
                fc.box(g, u - .025, u + .025, y0 + 1.1, y1, .02, .05, F['steel'], None, .008, skip=('back',))
            fc.box(g, u0, u1, y0 + 1.08, y0 + 1.17, .02, .07, F['stainless'], None, .012, skip=('back',))


def block_body(S, b):
    """The art of one map block's own box, into the scene (returns nothing; unknown blocks get a finished box)."""
    L, T = S.L, S.T
    s = stem(b)
    g = S.geo_of(b['id'])
    n0 = g.faces
    key = 'block'
    if s is None:
        # the Expedition's exit frame: heavy steel jambs and a lintel
        x0, x1, y0, y1, z0, z1 = ext(b)
        box(g, x0, x1, y0, y1, z0, z1, F['steel'], T, .03, cell=0, skip=('-y',) if y0 < .05 else ())
        key = 'exit'
    elif s == 'slab':
        slab_block(g, b, T, F['tile'], None, None, vent_holes(L, b), pit=.42)
        key = 'floor'
    elif s == 'vault':
        ceiling(g, b, T, L)
        key = 'ceiling'
    elif s.startswith('shell-'):
        shell(g, b, T, L['bounds'])
        key = 'shell'
    elif s.startswith('breach-'):
        x0, x1, y0, y1, z0, z1 = ext(b)
        box(g, x0, x1, y0, y1, z0, z1, F['plate'], T, .02, cell=0, skip=('-y',) if level(y0) is not None else ())
        key = 'breach'
    elif '-rail' in s:
        rail_block(g, b, T, s.endswith('-kick'))
        key = 'rail'
    elif s.startswith('stair-') and '-landing' not in s:
        r = [r for r in L['routes'] if r['stair'] and r['id'] == 'siren-' + s[:s.rindex('-')]]
        if r:
            tread(g, b, T, r[0])
        key = 'stair'
    elif not upright(b):
        if s.startswith('cradle-'):
            shard(g, b, T, sum(ord(c) for c in s))
            key = 'shard'
        else:
            ramp_deck(g, b, T)
            key = 'ramp'
    elif s.startswith('bridge-') and s.endswith('-deck') or s.startswith('gantry-') or s.endswith('-link'):
        deck(g, b, T, L, metal=True)
        key = 'deck'
    elif s.startswith('deck-') or s.startswith('gallery-') or s.endswith('-platform') or s.endswith('-specimen') or s.endswith('-landing'):
        deck(g, b, T, L)
        ribs(g, b, T, L)
        key = 'deck'
    elif '-skirt' in s:
        solid_block(g, b, T, body=F['concrete'], cladding=False)
        key = 'ramp'
    elif '-row' in s:
        solid_block(g, b, T)
        if T.i < 2:
            # a steel canopy line over the doors, under the kit's lit band
            fc, u0, u1 = inner_face(b)
            y0 = b['pos'][1] - b['size'][1] / 2
            sweep(g, [(-.06, 0), (-.06, .05), (-.02, .09), (.05, .09), (.05, 0)], [fc.pt(u0, y0 + 3.24, 0), fc.pt(u1, y0 + 3.24, 0)], F['steel'], up=fc.n, caps=False, smooth_u=False)
        key = 'row'
    elif '-tank-' in s:
        tank(g, b, T)
        key = 'tank'
    elif '-furnace' in s:
        furnace(g, b, T)
        key = 'furnace'
    elif '-gurney-' in s:
        gurney(g, b, T)
        key = 'gurney'
    elif '-cart' in s:
        cart(g, b, T)
        key = 'gurney'
    elif '-station' in s:
        booth(g, b, T)
        key = 'booth'
    elif s.startswith('lab-') and '-bench' in s:
        cabinet(g, b, T, wk.cfin(0x2E4E4C, 'metal'), wk.cfin(0x3D6662, 'metal'), module=.75)
        key = 'fitting'
    elif '-bench' in s:
        x0, x1, y0, y1, z0, z1 = ext(b)
        ck.rbox(g, x0, x1, y0 + .06, y1, z0, z1, .05, F['white'], 2 if T.i == 0 else 1)
        box(g, x0 + .08, x1 - .08, y0, y0 + .08, z0 + .08, z1 - .08, F['steeldark'], T, 0, cell=0, skip=('-y', '+y'))
        key = 'fitting'
    elif '-slab-' in s:
        cabinet(g, b, T, F['steel'], F['stainless'], module=1.1)
        key = 'fitting'
    elif '-chamber-' in s or '-freezer-' in s or '-lockers' in s or '-rack' in s:
        x0, x1, y0, y1, z0, z1 = ext(b)
        bbox(g, b, F['steeldark'] if '-rack' in s else F['steel'], T)
        if '-rack' in s and T.i < 2:
            fc, u0, u1 = inner_face(b)
            n = 4
            for i in range(n):
                a, c = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
                wk.plate(g, fc, a + .04, c - .04, y0 + .1, y1 - .1, .006, wk.cfin(0x1A2226, 'metal'), T)
        key = 'fitting'
    elif '-shower-' in s:
        x0, x1, y0, y1, z0, z1 = ext(b)
        bbox(g, b, F['showertile'], T, .015)
        key = 'fitting'
    elif '-console' in s or '-desk' in s:
        cabinet(g, b, T, F['steeldark'] if '-console' in s else F['panel'], wk.cfin(0x3A464C, 'metal') if '-console' in s else F['white'], module=.8)
        key = 'fitting'
    elif '-screen-' in s:
        x0, x1, y0, y1, z0, z1 = ext(b)
        bbox(g, b, F['steeldark'], T)
        key = 'fitting'
    elif min(b['size'][0], b['size'][2]) <= .65 and b['size'][1] >= .9:
        free_top = b['pos'][1] + b['size'][1] / 2 < 5.5
        solid_block(g, b, T, top=F['steel'] if free_top else None)
        key = 'wall'
    else:
        x0, x1, y0, y1, z0, z1 = ext(b)
        bbox(g, b, F['steel'] if b['material'] == 'metal' else F['panel'], T)
        key = 'other'
    tally(g, key, n0)


# ---------------------------------------------------------------------------------------------------------------------------
# The kit's steady parts
# ---------------------------------------------------------------------------------------------------------------------------
def near(a, b, e=.004):
    return abs(a - b) < e


def facing(p, b):
    """The face of the block a flat kit part lies on: (axis, sign) of its normal."""
    ax = min(range(3), key=lambda i: p['h'][i])
    return ax, (1 if p['c'][ax] > b['pos'][ax] else -1)


def troffer(g, p, T):
    """A troffer's housing: a steel frame round the kit's lit diffuser, flush under its ceiling."""
    x, y, z = p['c']
    hx, hz = p['h'][0], p['h'][2]
    top = y + p['h'][1] - .02
    ix, iz = hx - .045, hz - .045
    lo = top - .075
    f = F['stainless']
    if T.i == 2:
        return
    # a shallow tray: four sloped sides from the ceiling down to a flat rim round the diffuser
    O = [(x - hx - .07, top, z - hz - .07), (x + hx + .07, top, z - hz - .07), (x + hx + .07, top, z + hz + .07), (x - hx - .07, top, z + hz + .07)]
    M = [(x - hx - .02, lo, z - hz - .02), (x + hx + .02, lo, z - hz - .02), (x + hx + .02, lo, z + hz + .02), (x - hx - .02, lo, z + hz + .02)]
    I = [(x - ix, lo, z - iz), (x + ix, lo, z - iz), (x + ix, lo, z + iz), (x - ix, lo, z + iz)]
    for k in range(4):
        j = (k + 1) % 4
        mid = sk._lerp(M[k], M[j], .5)
        g.face((O[k], O[j], M[j], M[k]), f, normal=(mid[0] - x, -.6, mid[2] - z))
        g.face((M[k], M[j], I[j], I[k]), f, normal=(0, -1, 0))


def highbay(g, p, T):
    """A high-bay's pendant: a square steel shroud round the kit's lit panel, hung on two rods."""
    x, y, z = p['c']
    top = y + p['h'][1]
    lo = top - .2
    if T.i == 2:
        return
    for (a, c, d, e) in ((x - .8, x - .66, z - .8, z + .8), (x + .66, x + .8, z - .8, z + .8), (x - .66, x + .66, z - .8, z - .66), (x - .66, x + .66, z + .66, z + .8)):
        box(g, a, c, lo, top, d, e, F['steeldark'], T, .02, cell=0, skip=('+y',))
    box(g, x - .66, x + .66, top - .06, top, z - .66, z + .66, F['steeldark'], None, 0, cell=0, skip=('+y',))


def beacon(g, p, T):
    """A rotating beacon's fitting: a turned cap on the ceiling and a wire cage round the kit's lamp."""
    x, y, z = p['c']
    top = y + p['h'][1]
    lathe(g, [(.16, -.24), (.16, -.1), (.2, -.06), (.2, 0)], (x, top, z), F['steeldark'], T.round if T.i < 2 else 6, cap0=True)
    if T.detail:
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            cx, cz = x + .19 * math.cos(a), z + .19 * math.sin(a)
            rod(g, (cx, top - .24, cz), (cx, top - .46, cz), .008, F['steel'], 4, caps=False)
        sk.tube(g, [(x + .19 * math.cos(2 * math.pi * i / 12), top - .46, z + .19 * math.sin(2 * math.pi * i / 12)) for i in range(12)], .008, F['steel'], 4, closed=True)


def duct(g, p, T):
    """A round ventilation duct along the roof, with flanged joints and strap hangers."""
    x, y, z = p['c']
    hx = p['h'][0]
    yc = 12.08
    r = .4
    cylinder(g, (x - hx, yc, z), (x + hx, yc, z), r, F['duct'], (16, 10, 6)[T.i], caps=True)
    if T.i < 2:
        n = int(2 * hx / 4.8)
        for i in range(n + 1):
            xx = x - hx + .2 + (2 * hx - .4) * i / n
            cylinder(g, (xx - .03, yc, z), (xx + .03, yc, z), r + .035, F['steel'], (16, 10, 6)[T.i], caps=True)
            if T.i == 0:
                for s in (-1, 1):
                    rod(g, (xx + 1.2, yc + .1, z + s * (r + .02)), (xx + 1.2, 13.0, z + s * (r + .02)), .012, F['steeldark'], 4, caps=False)


def tray(g, p, T):
    """A cable tray under the beams: a ladder channel on hangers with a bundle of cables in it."""
    x, y, z = p['c']
    ax = wk.long_axis(p)
    hl = p['h'][ax]
    yc = 12.42

    def P(t, off, yy):
        return (x + t, yy, z + off) if ax == 0 else (x + off, yy, z + t)
    for s in (-1, 1):
        a, c = P(-hl, s * .29, yc), P(hl, s * .29, yc)
        sweep(g, sk.rect_section(.03, .1), [a, c], F['steeldark'], up=(0, 1, 0), caps=True, smooth_u=False)
    if T.i < 2:
        n = int(2 * hl / (.6 if T.i == 0 else 2.4))
        for i in range(n + 1):
            t = -hl + 2 * hl * i / n
            a, c = P(t, -.29, yc - .035), P(t, .29, yc - .035)
            rod(g, a, c, .012, F['steeldark'], 4, caps=False)
        for i, (off, col, r) in enumerate(((-.16, 0x15181A, .04), (-.05, 0x3F5D86, .03), (.05, 0x15181A, .035), (.16, 0xC8A020, .025))):
            rod(g, P(-hl, off, yc - .005 + r), P(hl, off, yc - .005 + r), r, wk.cfin(col, 'paint'), 5 if T.i == 0 else 4, caps=False)
        m = int(2 * hl / 4.8)
        for i in range(m + 1):
            t = -hl + .4 + (2 * hl - .8) * i / m
            for s in (-1, 1):
                rod(g, P(t, s * .29, yc), P(t, s * .29, 13.0), .01, F['steeldark'], 4, caps=False)


def post(g, p, T, f=None):
    x, y, z = p['c']
    h = p['h'][1]
    rod(g, (x, y - h, z), (x, y + h, z), .024, f or F['steeldark'], T.small)
    if T.tiny:
        rod(g, (x, y - h, z), (x, y - h + .025, z), .05, F['steeldark'], T.small)


def bar_tube(g, p, T, r, f):
    ax = wk.long_axis(p)
    a, b = list(p['c']), list(p['c'])
    a[ax] -= p['h'][ax]
    b[ax] += p['h'][ax]
    rod(g, tuple(a), tuple(b), r, f, T.small)


def cell_door(g, p, b, T):
    """A shut cell door on the kit's door slab: a heavy steel leaf with a sunk panel, three hinges, a wheel lock and bolts."""
    ax, s = facing(p, b)
    fc = Face(ax, s, p['c'][ax] - s * p['h'][ax])
    u = p['c'][2] if ax == 0 else p['c'][0]
    hu = p['h'][2] if ax == 0 else p['h'][0]
    y0, y1 = p['c'][1] - p['h'][1], p['c'][1] + p['h'][1]
    d = 2 * p['h'][ax]
    f = wk.pfin(p)
    fc.box(g, u - hu, u + hu, y0, y1, 0, d, f, T, .02, skip=('back',))
    if T.i == 2:
        return
    wk.plate(g, fc, u - hu + .14, u + hu - .14, y0 + .2, y0 + 1.25, d + .006, wk.cfin(0x56625E, 'metal'), T)
    for k in range(3):
        yy = y0 + .35 + k * (y1 - y0 - .7) / 2
        rod(g, fc.pt(u - hu + .01, yy - .09, d + .012), fc.pt(u - hu + .01, yy + .09, d + .012), .028, F['steeldark'], T.small)
    c = fc.pt(u + hu - .28, y0 + 1.25, d)
    lathe(g, [(.11, 0), (.11, .025), (.03, .04), (.03, .07), (.0001, .07)], c, F['stainless'], T.round if T.i == 0 else 6, axis=fc.n)
    if T.tiny:
        ck.rivets(g, [fc.pt(u - hu + .06 + (2 * hu - .12) * i / 5, yy, d) for i in range(6) for yy in (y0 + .07, y1 - .07)], fc.n, .014, F['stainless'])


def drawer(g, p, b, T):
    """A morgue drawer's or a locker's front: a pillowed stainless plate."""
    ax, s = facing(p, b)
    fc = Face(ax, s, p['c'][ax] - s * p['h'][ax])
    u = p['c'][2] if ax == 0 else p['c'][0]
    hu = p['h'][2] if ax == 0 else p['h'][0]
    y0, y1 = p['c'][1] - p['h'][1], p['c'][1] + p['h'][1]
    d = 2 * p['h'][ax]
    fc.box(g, u - hu, u + hu, y0, y1, 0, d, wk.pfin(p), T, min(.02, d * .45), skip=('back',))
    return fc, u, hu, y0, y1, d


def nozzle(g, p, T):
    x, y, z = p['c']
    top = y + p['h'][1]
    lathe(g, [(.03, -.2), (.1, -.16), (.1, -.1), (.05, -.06), (.05, 0)], (x, top, z), F['steel'], T.round if T.i == 0 else 6, cap0=True)


def kit_parts(S):
    L, T = S.L, S.T
    cradle_done = False
    rails = defaultdict(list)
    for i, p in enumerate(L['parts']):
        if p['kept'] or p['mover'] >= 0:
            continue
        f, b = p['f'], S.B[p['o']]
        g = S.geo_of(p['o'])
        n0 = g.faces
        look, col, h = p['look'], p['col'], p['h']
        if p['o'] in S.props:
            continue                                            # a gurney's or a cart's own model has its parts
        if f in ('wall', 'shell', 'skirt') and (look == 'ward-panel' or (look == 'cyber-metal' and col == 0x2F383B and near(h[1], .075)) or
                                                (look == 'cyber-metal' and col == 0x5D6B6E and near(h[1], .03) and near(min(h[0], h[2]), .015))):
            continue                                            # the wainscot, the base and the chair rail: the cladding's own
        if f == 'shell' and col == 0xA8B2B6:
            fc, u, hu, y0, y1, d = drawer(g, p, b, T)
            if T.detail:
                fc.box(g, u - .16, u + .16, y1 - .2, y1 - .15, d, d + .035, F['steeldark'], None, .01, skip=('back',))
        elif f == 'troffer':
            troffer(g, p, T)
        elif f == 'vault' and near(h[0], .8) and near(h[2], .8):
            highbay(g, p, T)
        elif f == 'vault' and near(h[0], .175) and near(h[2], .175):
            beacon(g, p, T)
        elif f == 'vault' and near(h[1], .4):
            duct(g, p, T)
        elif f == 'vault' and near(h[1], .06):
            tray(g, p, T)
        elif f == 'slab' and col == 0x3A4448:
            if not cradle_done:
                cradle_done = True
                cradle_ring(g, b, T)
        elif f == 'floor-vent' and col == wk.VOID:
            continue                                            # the pit is cut in the floor
        elif f == 'rail' and col == 0x5D6B6E:
            continue                                            # the handrail's cap: the rail's own round bar
        elif f == 'rail' and near(h[1], .4):
            post(g, p, T)
        elif f == 'rail':
            bar_tube(g, p, T, .014, F['steeldark'])
            if T.i == 0:
                for dy in (-.2, .2):
                    q = dict(p)
                    q['c'] = (p['c'][0], p['c'][1] + dy, p['c'][2])
                    bar_tube(g, q, T, .008, F['steel'])
        elif f == 'tread' and near(h[1], .5):
            post(g, p, T, F['steel'])
            lat = 0 if b['size'][0] > b['size'][2] else 2      # a tread is widest across its flight
            rails[(p['o'][:p['o'].rindex('-')], p['c'][lat] > b['pos'][lat])].append((p['c'][0], p['c'][1] + .5, p['c'][2]))
        elif f == 'tread':
            continue                                            # the stepped rail: one sloped handrail a flight side instead
        elif f == 'cell-door' and near(max(h[0], h[2]), .75):
            cell_door(g, p, b, T)
        elif f in ('chamber', 'lockers') and min(h) < .03 and max(h[0], h[2]) > .2 and h[1] > .2:
            fc, u, hu, y0, y1, d = drawer(g, p, b, T)
            if f == 'lockers' and T.tiny:
                for k in range(4):
                    fc.box(g, u - hu + .08, u + hu - .08, y1 - .5 + k * .05, y1 - .48 + k * .05, d - .004, d + .008, F['steeldark'], None, 0, skip=('back',))
        elif f == 'tank' and near(h[0], .05) and near(h[2], .05):
            x, y, z = p['c']
            rod(g, (x, y - h[1], z), (x, y + h[1], z), .055, F['steel'], T.round if T.i == 0 else 6)
        elif f == 'tank' and near(h[1], .15):
            x, y, z = p['c']
            ck.rbox(g, x - h[0], x + h[0], y - h[1], y + h[1], z - h[2], z + h[2], .06, wk.pfin(p), 2 if T.i == 0 else 1)
            if y > b['pos'][1] and T.detail:
                lathe(g, [(.5, 0), (.5, .1), (.3, .2), (.12, .22)], (x, y + h[1], z), F['steel'], T.round if T.i == 0 else 8, cap1=True)
        elif f == 'furnace' and near(h[2], .04) and h[1] > 1:
            x, y, z = p['c']
            rod(g, (x, y - h[1], z), (x, y + h[1], z), .04, F['steeldark'], T.small)
        elif f == 'bench' and col == 0xD8DCDC:
            x, y, z = p['c']
            lathe(g, [(.11, -h[1]), (.11, 0), (.05, .08), (.035, h[1])], (x, y, z), F['white'], T.round if T.i == 0 else 6, cap1=True)
        elif f == 'shower' and near(h[1], .04):
            x, y, z = p['c']
            ax, s = (2, 1 if z > b['pos'][2] else -1)
            a = (x, y, z - s * h[2])
            e = (x, y, z + s * h[2])
            rod(g, a, e, .02, F['stainless'], T.small)
            lathe(g, [(.02, 0), (.09, -.05), (.09, -.07)], (e[0], e[1], e[2]), F['stainless'], T.round if T.i == 0 else 6, cap0=True)
        elif f == 'gas' and near(h[0], .15):
            nozzle(g, p, T)
        elif f == 'shutter' and near(h[1], .18):
            x, y, z = p['c']
            ck.rbox(g, x - h[0], x + h[0], y - h[1], y + h[1], z - h[2], z + h[2], .07, F['steeldark'], 2 if T.i == 0 else 1)
        elif f == 'shard' and look == 'ward-glass':
            tooth(g, p, T, i)
        elif f == 'breach' and max(h) < .04:
            if T.tiny:
                ax, s = facing(p, b)
                n = [0, 0, 0]
                n[ax] = s
                ck.rivets(g, [tuple(p['c'][k] - n[k] * h[k] for k in range(3))], tuple(n), .03, F['stainless'])
        elif look == 'cyber-grate':
            grating(g, p, T)
        elif col == wk.VOID and min(h) < .01 and upright(b):
            # a dark opening's plate: stood clear of the cladding under it
            ax, s = facing(p, b)
            q = dict(p)
            q['c'] = tuple(p['c'][k] + (s * .004 if k == ax else 0) for k in range(3))
            q['h'] = tuple(p['h'][k] + (.004 if k == ax else 0) for k in range(3))
            wk.kit_part(g, q, T, owner=b)
        else:
            wk.kit_part(g, p, T, owner=b)
        tally(g, 'kit ' + f, n0)
    # the stairs' handrails: a sloped tube through the posts' tops on each open side of a flight
    for key, tops in rails.items():
        horiz = 0 if abs(tops[0][0] - tops[-1][0]) > abs(tops[0][2] - tops[-1][2]) else 2
        tops.sort(key=lambda t: t[horiz])
        if len(tops) >= 2:
            a, e = tops[0], tops[-1]
            d = sk._norm(sk._sub(e, a))
            sk.tube(S.static, [sk._add(a, d, -.3), sk._add(e, d, .3)], .035, F['stainless'], T.small, caps=True)


def cradle_ring(g, b, T):
    """The broken vessel's seat round the Cradle: a low steel ring (5.6 m), bolted down."""
    y = b['pos'][1] + b['size'][1] / 2
    n = (64, 32, 16)[T.i]
    sec = [(-.24, 0), (-.2, .035), (.2, .035), (.24, 0)]
    path = [(5.6 * math.sin(2 * math.pi * i / n), y, 5.6 * math.cos(2 * math.pi * i / n)) for i in range(n)]
    sweep(g, sec, path, wk.cfin(0x3A4448, 'metal'), closed=True, up=(0, 1, 0), smooth_u=False)
    # a band of dark tile round the seat and a paler disc inside it (laid on the floor: the coplanar pass stands them clear)
    m = n if T.i < 2 else 16
    for (r0, r1, f) in ((5.9, 6.5, wk.cfin(0x56626A, 'paint')), (6.5, 6.62, F['stainless'])):
        for i in range(m):
            a0, a1 = 2 * math.pi * i / m, 2 * math.pi * (i + 1) / m
            g.face(((r0 * math.sin(a0), y, r0 * math.cos(a0)), (r1 * math.sin(a0), y, r1 * math.cos(a0)), (r1 * math.sin(a1), y, r1 * math.cos(a1)), (r0 * math.sin(a1), y, r0 * math.cos(a1))), f, (0, 1, 0))
    if T.tiny:
        ck.rivets(g, [((5.6 + s * .12) * math.sin(2 * math.pi * i / 48), y + .035, (5.6 + s * .12) * math.cos(2 * math.pi * i / 48)) for i in range(48) for s in (-1, 1)], (0, 1, 0), .025, F['stainless'])


def tooth(g, p, T, seed):
    """A jagged tooth of the vessel's broken glass, lit from within (the kit's glowing tooth, as a splinter)."""
    hx, hy, hz = p['h']
    t, done = wk.local(g, p['q'], p['c'])
    rnd = np.random.RandomState(seed)
    k = .3 + .4 * rnd.rand()
    lo = -hy - .45                                              # sunk into the shard's broken top
    wx = hx * .7
    tip = ((k - .5) * 2 * wx * .8, hy, (rnd.rand() - .5) * hz * .6)
    base = [(-wx, lo, -hz * .45), (wx, lo, -hz * .45), (wx, lo, hz * .45), (-wx, lo, hz * .45)]
    mid = [(-wx * .8, hy * (.1 + .5 * rnd.rand()), -hz * .35), (wx * .7, hy * (-.3 + .5 * rnd.rand()), -hz * .35), (wx * .7, hy * (-.3 + .4 * rnd.rand()), hz * .35), (-wx * .8, hy * (.1 + .4 * rnd.rand()), hz * .35)]
    f = F['shardglow']
    for i in range(4):
        j = (i + 1) % 4
        t.face((base[i], base[j], mid[j], mid[i]), f)
        t.face((mid[i], mid[j], tip), f)
    done()


def grating(g, p, T):
    """A steady grating (the gantries' decks): bearing bars across a dark plate."""
    x, y, z = p['c']
    hx, hy, hz = p['h']
    if T.i > 0:
        return
    n = int(2 * hz / .5)
    for i in range(n + 1):
        zz = z - hz + .05 + (2 * hz - .1) * i / n
        box(g, x - hx, x + hx, y - hy, y + hy + .004, zz - .02, zz + .02, F['steel'], None, 0, cell=0, skip=('-y',))
    for s in (-1, 0, 1):
        box(g, x + s * (hx - .04) - .03, x + s * (hx - .04) + .03, y - hy, y + hy + .014, z - hz, z + hz, F['steel'], None, 0, cell=0, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
# The movers
# ---------------------------------------------------------------------------------------------------------------------------
def mover(L, m, T):
    g = Geo()
    P = [L['parts'][i] for i in m['parts']]
    f = m['f']
    if f in ('floor-vent', 'ceiling-vent'):
        for p in P:
            x, y, z = p['c']
            hx, hy, hz = p['h']
            for (a, c, d, e) in ((x - hx, x - hx + .05, z - hz, z + hz), (x + hx - .05, x + hx, z - hz, z + hz), (x - hx + .05, x + hx - .05, z - hz, z - hz + .05), (x - hx + .05, x + hx - .05, z + hz - .05, z + hz)):
                box(g, a, c, y - hy, y + hy, d, e, F['steel'], T, .006, cell=0)
            n = (14, 8, 4)[T.i]
            for i in range(n):
                xx = x - hx + .05 + (2 * hx - .1) * (i + .5) / n
                box(g, xx - .014, xx + .014, y - hy + .004, y + hy - .004, z - hz + .05, z + hz - .05, F['grate'], None, 0, cell=0)
            for zz in (z - hz * .34, z + hz * .34):
                box(g, x - hx + .05, x + hx - .05, y - hy + .002, y + hy - .008, zz - .02, zz + .02, F['grate'], None, 0, cell=0)
    elif f == 'wall-vent':
        for p in P:
            ax = wk.long_axis(p)
            axis = (1, 0, 0) if ax == 0 else (0, 0, 1)
            thin = 2 if ax == 0 else 0
            sgn = 1 if True else -1
            t, done = wk.local(g, sk.quat_axis(axis[0], axis[1], axis[2], .5 if ax == 0 else -.5), p['c'])
            hh = list(p['h'])
            hh[thin] = .008
            hh[1] = p['h'][1] * 1.25
            box(t, -hh[0], hh[0], -hh[1], hh[1], -hh[2], hh[2], wk.pfin(p), None, 0, cell=0)
            done()
    elif f == 'entry-door':
        for p in P:
            if near(p['h'][0], .03) and near(p['h'][2], .03):
                x, y, z = p['c']
                rod(g, (x, y - p['h'][1], z), (x, y + p['h'][1], z), .03, wk.pfin(p), T.small + (2 if T.i == 0 else 0))
            elif p['look'] == 'cyber-tube':
                wk.kbox(g, p, T, bevel=0)
            elif p['h'][1] > 1:
                leaf(g, p, T)
            else:
                wk.kbox(g, p, T, bevel=.012)
    elif f == 'hatch':
        for p in P:
            wk.kbox(g, p, T, f=F['plate'], bevel=.015)
            if T.detail:
                x, y, z = p['c']
                hx, hy, hz = p['h']
                for k in (-.5, 0, .5):
                    box(g, x - hx + .06, x + hx - .06, y - hy - .03, y - hy, z + k * hz * 1.5 - .04, z + k * hz * 1.5 + .04, F['steeldark'], None, .008, cell=0, skip=('+y',))
    elif f == 'shutter':
        for p in P:
            ax = wk.long_axis(p)
            hy = p['h'][1]
            ht = min(p['h'][0], p['h'][2])
            a, b = list(p['c']), list(p['c'])
            a[ax] -= p['h'][ax]
            b[ax] += p['h'][ax]
            sec = [(0, -hy), (ht, -hy * .45), (ht, hy * .45), (0, hy), (-ht, hy * .45), (-ht, -hy * .45)]
            sweep(g, sec, [tuple(a), tuple(b)], wk.pfin(p), up=(0, 1, 0), caps=True, smooth_u=False)
    else:
        for p in P:
            wk.kit_part(g, p, T)
    return g


def leaf(g, p, T):
    """A sliding door leaf: a steel slab with a raised rail frame and a kick plate."""
    ax = min((0, 2), key=lambda i: p['h'][i])
    wk.kbox(g, p, T, bevel=.012)
    if T.i == 2:
        return
    for s in (-1, 1):
        fc = Face(ax, s, p['c'][ax] + s * p['h'][ax])
        u = p['c'][2] if ax == 0 else p['c'][0]
        hu = p['h'][2] if ax == 0 else p['h'][0]
        y0, y1 = p['c'][1] - p['h'][1], p['c'][1] + p['h'][1]
        wk.plate(g, fc, u - hu + .06, u + hu - .06, y0 + .08, y0 + .5, .006, F['stainless'], T)
        wk.plate(g, fc, u - hu + .06, u + hu - .06, y0 + .62, y0 + 1.35, .006, wk.cfin(0x7C8A90, 'metal'), T)
        wk.plate(g, fc, u - hu + .06, u + hu - .06, y0 + 2.0, y1 - .08, .006, wk.cfin(0x7C8A90, 'metal'), T)


# ---------------------------------------------------------------------------------------------------------------------------
# A tier's scene
# ---------------------------------------------------------------------------------------------------------------------------
def build(L, T):
    set_cover(L)
    S = Scene(L, T)
    for b in L['blocks']:
        block_body(S, b)
    kit_parts(S)
    for m in L['movers']:
        if m['slot'] not in S.movers:
            S.movers[m['slot']] = (mover(L, m, T), m['at'])
    return S


def lift_all(geos, ti):
    """Settles coplanar faces over the whole rest scene at once (the posed blocks and props against the static scenery too)."""
    allg = Geo()
    counts = []
    for g in geos:
        counts.append((len(g.p), len(g.size)))
        allg.extend(g)
    # (a little over the kit's nearness at the Fine tier: faces 4 mm apart still fight at its range)
    rep = sk.lift_coplanar(allg, eps=max(sk.NEAR_TIER[ti], .0045), lift=sk.LIFT_TIER[ti], passes=5)
    at = 0
    for g, (n, _) in zip(geos, counts):
        g.p = allg.p[at:at + n]
        at += n
    return rep


def views():
    here = os.path.dirname(os.path.abspath(__file__))
    v = [('atrium', (0, 1.8, -12.5), (0, 3.2, 6), 70), ('galleries', (-9, 1.6, -6.5), (6, 9.5, 7), 70), ('void', (-12.6, 7.7, 11.6), (7, 5.5, -8), 70),
         ('bridge', (6, 7.6, -9.4), (6, 6.6, 8), 70), ('roof', (-14.5, 12, -12.5), (3, 0, 3), 70), ('hall', (-31, 1.8, 16.7), (10, 2.6, 16.7), 70),
         ('cells', (-8.5, 1.8, 20.6), (1.5, 2.2, 26), 70), ('lab', (22.5, 1.8, -12.5), (19.6, 2, -3), 70), ('furnace', (34.2, 1.8, -12.6), (36.4, 1.6, 2), 70),
         ('morgue', (-23.5, 1.7, 26), (-34, 1.2, 21), 70), ('control', (19.5, 7.7, 17), (36, 7.2, 23), 70), ('stair', (20.5, 1.8, 27.6), (32, 3.2, 28.7), 70),
         ('ramp', (26.8, 1.8, -12.8), (23.4, 3.6, 2), 70), ('decon', (-25, 7.7, 17.2), (-29, 8.2, 23.5), 70), ('cradle', (-3, 1.6, -9), (2, 1.2, 0), 60),
         ('celldoor', (-4.5, 1.7, 22.6), (-2, 1.5, 25), 55), ('tank', (17.2, 1.7, -4), (19.4, 1.6, -7), 60), ('rail', (-9.5, 7.6, 9.2), (-6, 6.6, 10.1), 55)]
    return v


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--also')
    ap.add_argument('--out')
    ap.add_argument('--preview')
    ap.add_argument('--views')
    ap.add_argument('--tiers', default='0,2')
    ap.add_argument('--own-mid', action='store_true', help='build a Mid tier of its own (default: the Mid props share the Fine meshes)')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--stats', action='store_true')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    if args.also:
        E = sk.load_layout(args.also)
        have = set(b['id'] for b in L['blocks'])
        L['blocks'] += [b for b in E['blocks'] if b['id'] not in have]
        L['posed'] += [i for i in E['posed'] if i not in L['posed']]
    global COUNT
    COUNT = args.stats
    wk.finishes()
    sb = sk.StageBank()
    chunks = sk.Chunks(16.0, (-40.0, -32.0), (5.55, 11.4))
    floors = (0.0, UPPER)
    t0 = time.time()
    shared = []

    def put(name, group, om):
        if om is None:
            return
        mi = sb.mesh(om)
        sb.bank.prop(name, group, [(mi, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
        st = sb.stats.setdefault((group, name.rsplit('/', 1)[-1]), [0, 0, 0])
        st[0] += 1
        st[1] += sum(len(s) for s in om.submeshes) // 3
        st[2] += len(om.positions)
        if name.endswith('/0') and not args.own_mid:
            shared.append((name[:-1] + '1', group, mi))
    tiers = [int(x) for x in args.tiers.split(',')]
    if args.own_mid and 1 not in tiers:
        tiers = sorted(tiers + [1])
    for ti in tiers:
        T = sk.Tier(ti)
        t = time.time()
        S = build(L, T)
        names = ['static'] + ['b/' + i for i in S.posed] + ['p/' + i for i in S.props] + list(S.movers)
        geos = [S.static] + list(S.posed.values()) + list(S.props.values()) + [g for g, _ in S.movers.values()]
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % lift_all(geos[:1 + len(S.posed) + len(S.props)], ti))
        print('tier %s: static %d faces, posed %d, props %d, movers %d, %.1f s' % (T.name, S.static.faces, sum(g.faces for g in S.posed.values()),
              sum(g.faces for g in S.props.values()), sum(g.faces for g, _ in S.movers.values()), time.time() - t))
        if args.stats:
            print('  ' + ', '.join('%s %d' % kv for kv in sorted(STATS.items(), key=lambda kv: -kv[1]) if kv[1] > 0))
        STATS.clear()
        aos = [None] * len(geos)
        if not args.no_ao:
            # two objects: the static scenery, and everything else at rest in one (its occlusion cut back apart by corners)
            rest = Geo()
            for g in geos[1:]:
                rest.extend(g)
            a0, a1 = sk.bake_ao([S.static, rest], distance=(1.3, 1.3, 1.6)[ti], samples=(32, 16, 12)[ti])
            aos, at = [a0], 0
            for g in geos[1:]:
                n = len(g.p) // 3
                aos.append(a1[at:at + n])
                at += n
        bakes = [sk.Bake(g, ao, floors=floors, seed=11) if g.faces else None for g, ao in zip(geos, aos)]
        ids = chunks.ids(S.static)
        for cid in np.unique(ids):
            put('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), S.static, bakes[0], ids == cid))
        k = 1
        for bid, g in S.posed.items():
            if g.faces:
                p = S.B[bid]['pos']
                put('rig/b/%s/%d' % (bid, ti), 'rig', sk.to_mesh('rig/b/%s/%d' % (bid, ti), g.transformed([1, 0, 0, -p[0], 0, 1, 0, -p[1], 0, 0, 1, -p[2]]), bakes[k]))
            k += 1
        for bid, g in S.props.items():
            if g.faces:
                p = S.B[bid]['pos']
                put('block/%s/%d' % (bid, ti), 'block', sk.to_mesh('block/%s/%d' % (bid, ti), g.transformed([1, 0, 0, -p[0], 0, 1, 0, -p[1], 0, 0, 1, -p[2]]), bakes[k]))
            k += 1
        for slot, (g, at) in S.movers.items():
            if g.faces:
                put('rig/%s/%d' % (slot, ti), 'rig', sk.to_mesh('rig/%s/%d' % (slot, ti), g.transformed([1, 0, 0, -at[0], 0, 1, 0, -at[1], 0, 0, 1, -at[2]]), bakes[k]))
            k += 1
        if args.preview and ti == 0:
            vs = views()
            if args.views:
                vs = [v for v in vs if v[0] in args.views.split(',')]
            sk.preview([(g, bk) for g, bk in zip(geos, bakes) if bk is not None], vs, args.preview)
    if args.out:
        for name, group, mi in shared:
            sb.share(name, group, mi)
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
