"""Meridian's city bank (the MERIDIAN art bible; plan W1, W8b/c, W10): every module Core NativeMeridianDress plans, built at LOD0
and LOD1 on board 3's approved kit (meridian_kit.py, meridian_buildings.py on stagekit), baked (Cycles occlusion, the kit's
colours, rain streaks) and written as one bank, ChipArenaCityMeridian.bytes (bank.py's format; props module/<key>/<tier>, tier 0
LOD0 and 2 LOD1, group "module").

The module frame (bible 11.1): a facade bay's origin is on the facade plane (the collider's face) at the bay's foot and
centre, x along the facade (-1.25..1.25), y up, z out of it. A hugging module (every ground bay, every upper bay's `.h`
variant, every flat piece) has every vertex between 0.25 m inside its face and 6 cm out of it (the integrator's rule: nothing
proud of a collider below 3.1 m but flat paint; this script flattens the relief into that band and drops what collapses),
so the planner may stand it in the actor band. Upper bays without `.h` and the cornices project freely (overhead art).
Fill modules (parapets, posts, wagons) are authored at a nominal box (see each) for the planner to stretch to its block.

Run headless (Blender 5.2):
    blender --background --factory-startup --python meridian_bank.py -- --out DIR [--only key,key] [--no-ao]
The bank is never committed: keep it in your own F: folder and put it in the Editor's Assets/LocalAssets/ChipArenaLocal/
Resources only to capture (the integrator packs it into the Drive zip).
"""
import json
import math
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402

import stagekit as sk  # noqa: E402
import meridian_kit as ck  # noqa: E402
import meridian_buildings as cb  # noqa: E402
from meridian_kit import (F, BAY, Frame, Hole, fbox, fquad, wall, reveal, smould, mould, mould_lod, sill, text, arch_ring, rng,  # noqa: E402
                          lantern, glow)

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = ARGS[ARGS.index('--out') + 1] if '--out' in ARGS else os.path.join(HERE, 'out')
ONLY = set(ARGS[ARGS.index('--only') + 1].split(',')) if '--only' in ARGS else None
AO = '--no-ao' not in ARGS
BANK = 'ChipArenaCityMeridian'
HUG_OUT, HUG_IN = .06, -.25
TRADES = ('BAKERY', 'GROCER', 'NEWS', 'CHEMIST', 'COBBLER', 'CAFE', 'BUTCHER', 'DAIRY', 'LAUNDRY', 'TAILOR')
# the street names per layout and route (Core NativeMeridianDressSheet.cs, tools/stages/meridian_sheet.py)
sys.path.insert(0, HERE)
import meridian_sheet  # noqa: E402

MODULES = {}   # key -> (builder(B), flags)


def module(key, hug=False, flat=False):
    def wrap(fn):
        MODULES[key] = (fn, dict(hug=hug, flat=flat))
        return fn
    return wrap


def bay_frame(y=0.0):
    return Frame((-BAY / 2, y, 0.0), (1.0, 0.0, 0.0))


def style(seed, brick):
    st = cb.Style(seed, brick=brick)
    return st, rng('module', seed, brick)


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes this bank adds to the kit's
# ---------------------------------------------------------------------------------------------------------------------------
def extra_finishes():
    def add(name, *a, **k):
        if name not in F:
            F[name] = sk.fin('mer-' + name, *a, **k)
    add('cd-olive', 0x5B6B3A, sk.DETAIL_NONE, .4, .2, .8, 0, wear=.3)
    add('cd-orange', 0xEE7420, sk.DETAIL_NONE, .4, .1, .6, 0)
    add('paint-road', 0xE6E0C8, sk.DETAIL_NONE, .3, 0, .4, 0, wear=.6)
    add('spray-route', 0xF0EEE6, sk.DETAIL_NONE, .25, 0, .5, 0)
    add('wagon-oxide', 0x6A2626, sk.DETAIL_WOOD, .45, 0, .8, .1, param=1.0)
    add('iron-grey', 0x3C4044, sk.DETAIL_NONE, .4, .45, .8, 0)
    if 'sodium-route' not in F:
        glow('sodium-route', 0xFFB866, 3.2)
    if 'sodium-dead' not in F:
        F['sodium-dead'] = sk.fin('mer-sodium-dead', 0x3A3226, sk.DETAIL_NONE, .85, 0, .3, 0)
    if 'lit-arch' not in F:
        glow('lit-arch', 0xFFC27A, 2.2)


# ---------------------------------------------------------------------------------------------------------------------------
# Ground bays (4.0 m; all hugging)
# ---------------------------------------------------------------------------------------------------------------------------
def _ground(fn, brick, seed, **kw):
    def build(B):
        st, r = style(seed, brick)
        fn(B, bay_frame(), st, r, **kw)
    return build


for v, brick in (('0', True), ('1', False)):
    for s in range(3):
        module('g.door.%s.%d' % (v, s), hug=True)(_ground(cb.ground_door_bay, brick, 11 + s * 7 + (0 if brick else 3)))
        module('g.window.%s.%d' % (v, s), hug=True)(_ground(cb.ground_window_bay, brick, 41 + s * 5 + (0 if brick else 2)))


def _shop(trade, shutter=False):
    def build(B):
        st, r = style(hash(trade) % 97 if False else len(trade) * 13 + 5, False)
        fr = bay_frame()
        cb.ground_shop_bay(B, fr, st, r, lit=not shutter, shutter=shutter, paint=F[cb.SHOP_PAINTS[len(trade) % 4]], trade=trade)
        if B.T.lod == 0:
            cb.fascia_text(B, fr, cb.CONSOLE_CLEAR, BAY - cb.CONSOLE_CLEAR, trade if not shutter else 'CLOSED')
    return build


for t in TRADES:
    module('g.shop.' + t, hug=True)(_shop(t))
module('g.shutter', hug=True)(_shop('GROCER', True))


def ten_ground(B, entry, seed):
    """A tenement's ground bay (bible 5.3, after tenement_face): barred flat windows over a blue plinth, or the arched entry."""
    g, T = B.g, B.T
    fr = bay_frame()
    st = cb.Style(seed, brick=True)
    st.wall = F['brick-red']
    r = rng('tenground', seed, entry)
    fd = F['stone-dress']
    if entry:
        h = Hole(.55, 1.95, 0.0, 2.55, .7, .25)
        cb.plinth_run(B, fr, 0.0, BAY, F['brick-blue'], [h], top=.9)
        wall(g, T, fr, 0.0, BAY, .9, 4.0, st.wall, [h], bands=[(3.85, 4.0, F['brick-buff'])], base=0.0)
        cb.tenement_entry(B, fr, h, r, fd)
        return
    h = Hole(.65, 1.85, 1.1, 2.98, .17, .2)
    cb.plinth_run(B, fr, 0.0, BAY, F['brick-blue'], top=.9)
    wall(g, T, fr, 0.0, BAY, .9, 4.0, st.wall, [h], bands=[(3.85, 4.0, F['brick-buff'])], base=0.0)
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, fd)
    arch_ring(g, T, fr, h, F['brick-buff'], .22, .03, F['stone-dress'] if T.detail else None, key=None)
    st.window(B, fr, h, r, ground=True, frame=F['paint-white'])
    if T.lod == 0:
        for i in range(5):
            x = h.u0 + (h.u1 - h.u0) * (i + .5) / 5
            fbox(g, T, fr, x - .0175, x + .0175, h.v0, h.top_v, -.04, -.005, F['iron-black'], 0, skip=('-z',))
        for y in (h.v0 + .55, h.v0 + 1.45):
            fbox(g, T, fr, h.u0, h.u1, y - .02, y + .02, -.045, -.01, F['iron-black'], 0, skip=('-z',))
        fquad(g, fr, 1.03, .3, 1.47, .55, .041, F['iron-black'])
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.4, .34)


module('g.entry', hug=True)(lambda B: ten_ground(B, True, 5))
module('g.flats.0', hug=True)(lambda B: ten_ground(B, False, 6))
module('g.flats.1', hug=True)(lambda B: ten_ground(B, False, 9))


def ware_bay(B, kind, seed, upper=False):
    """A warehouse bay (bible 5.4): buff brick with a blue plinth, pilasters at the bay's edges; small-paned iron windows under
    segmental arches, or a loading door (3.0 x 3.6 m in the bible; 2.1 m in a bay) with a caged bulkhead lamp over it."""
    g, T = B.g, B.T
    fr = bay_frame()
    st = cb.Style(seed, brick=True)
    st.wall = F['brick-buff']
    r = rng('ware', seed, kind, upper)
    H = 3.0 if upper else 4.0
    if kind == 'load':
        h = Hole(.2, 2.3, 0.0, 3.4, 0.0, .22) if not upper else Hole(.35, 2.15, .15, 2.6, 0.0, .22)
        if not upper:
            cb.plinth_run(B, fr, 0.0, BAY, F['brick-blue'], [h], top=.75)
        wall(g, T, fr, 0.0, BAY, 0.0 if upper else .75, H, st.wall, [h], base=0.0)
        reveal(g, T, fr, h, st.wall, sill=False)
        fbox(g, T, fr, h.u0 - .1, h.u1 + .1, h.v1, h.v1 + .3, 0.0, .05, F['iron-black'], .01, skip=('-z',))   # the lintel girder
        n = 6
        for i in range(n):
            a = h.u0 + .04 + (h.u1 - h.u0 - .08) * i / n
            b = a + (h.u1 - h.u0 - .08) / n - .02
            fquad(g, fr, a, h.v0 + .04, b, h.v1 - .04, -h.depth + .03, F['paint-oxblood'] if i % 2 == 0 else F['timber-old'])
        if not upper:
            lantern(g, T, fr.P(BAY / 2, 3.72, .16), .13, F['iron-black'], F['lantern'] if r.random() < .5 else F['glass-dark'])
        return
    h = Hole(.45, 2.05, .9 if not upper else .55, 3.3 if not upper else 2.55, .25, .2)
    if not upper:
        cb.plinth_run(B, fr, 0.0, BAY, F['brick-blue'], top=.75)
    wall(g, T, fr, 0.0, BAY, 0.0 if upper else .75, H, st.wall, [h], base=0.0)
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, F['stone-dress'])
    arch_ring(g, T, fr, h, F['brick-blue'], .23, .03, None)
    # small-paned iron glazing: a grid of dark panes, a few lit or broken
    cols, rows = 4, 5
    for i in range(cols):
        for j in range(rows):
            a = h.u0 + .03 + (h.u1 - h.u0 - .06) * i / cols
            b = h.u0 + .03 + (h.u1 - h.u0 - .06) * (i + 1) / cols - .03
            c = h.v0 + .03 + (h.top_v - h.v0 - .06) * j / rows
            d = h.v0 + .03 + (h.top_v - h.v0 - .06) * (j + 1) / rows - .03
            x = r.random()
            fquad(g, fr, a, c, b, d, -h.depth + .05, F['lit-tungsten'] if x < .06 else F['interior'] if x < .12 else F['glass-dark'])
    fquad(g, fr, h.u0, h.v0, h.u1, h.top_v, -h.depth + .04, F['iron-black'])
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.6, .38)
    for x in (0.0, BAY - .12):
        fbox(g, T, fr, x, x + .12, 0.0, H, 0.0, .05, st.wall, .01, skip=('-z', '-y', '+y'))


module('g.load', hug=True)(lambda B: ware_bay(B, 'load', 3))
module('g.ware', hug=True)(lambda B: ware_bay(B, 'win', 4))


def civic_bay(B, seed, upper=False):
    """A civic bay (bible 5.5's family): stone ashlar, rusticated below, a tall arched window with a keystone and a hood."""
    g, T = B.g, B.T
    fr = bay_frame()
    st = cb.Style(seed, brick=False)
    st.wall = F['stone-civic']
    st.dress = F['stone-dress']
    st.head = 'hood'
    r = rng('civic', seed, upper)
    H = 3.0 if upper else 4.0
    h = Hole(.6, 1.9, .7 if upper else 1.0, 2.6 if upper else 3.3, .3 if not upper else 0.0, .2)
    if upper:
        wall(g, T, fr, 0.0, BAY, 0.0, H, st.wall, [h], base=0.0)
    else:
        cb.rusticate(B, fr, BAY, 0.0, H, [h], st.wall)
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, st.dress)
    if upper:
        cb.window_head(B, fr, h, st)
    else:
        arch_ring(g, T, fr, h, st.dress, .22, .03, None, key=st.dress)
    st.window(B, fr, h, r, ground=not upper)
    smould(g, T, fr, 0.0, BAY, H - .18, ck.P_STRING, st.dress)


module('g.civic.0', hug=True)(lambda B: civic_bay(B, 3))
module('g.civic.1', hug=True)(lambda B: civic_bay(B, 8))


def blind_ground(B, seed, kind):
    """A blind ground bay: the plinth, the wall and its string course; posters on their own papers, or a survivor's mark."""
    g, T = B.g, B.T
    fr = bay_frame()
    st, r = style(seed, True)
    cb.plinth_run(B, fr, 0.0, BAY, st.plinth)
    wall(g, T, fr, 0.0, BAY, ck.PLINTH, 3.92, st.wall, base=0.0)
    smould(g, T, fr, 0.0, BAY, 3.92, ck.P_STRING, st.dress)
    if kind == 1:
        cb.posters(B, fr, r)
    elif kind == 2 and T.lod == 0:
        ck.spray(g, T, fr, 1.25, 1.6, .7, .012, 'x', F['spray-white'])
    B.streaks.add_under(fr, 0.0, BAY, 3.92, 1.8, .3)


for k in range(3):
    module('g.blind.%d' % k, hug=True)((lambda kk: (lambda B: blind_ground(B, 21 + kk, kk)))(k))


# ---------------------------------------------------------------------------------------------------------------------------
# Upper bays (3.0 m): each free (its sills and heads standing out as built) and hugging (`.h`)
# ---------------------------------------------------------------------------------------------------------------------------
def upper(key, fn):
    module(key)(fn)
    module(key + '.h', hug=True)(fn)


def _upper_win(brick, seed):
    def build(B):
        st, r = style(seed, brick)
        cb.upper_window_bay(B, bay_frame(), st, r)
    return build


for v, brick in (('0', True), ('1', False)):
    for s in range(3):
        upper('u.win.%s.%d' % (v, s), _upper_win(brick, 61 + s * 11 + (0 if brick else 5)))
module('u.oriel')(lambda B: cb.oriel(B, bay_frame(), *style(71, False), 1))


def ten_upper(B, seed, stair=False):
    """A tenement's upper bay (bible 5.3, after tenement_face): red brick, a buff band at the floor and a blue one at the sills,
    a segmental arch of buff voussoirs; or the stair's half-landing window, lit (the strongest vertical at night)."""
    g, T = B.g, B.T
    fr = bay_frame()
    st = cb.Style(seed, brick=True)
    st.wall = F['brick-red']
    r = rng('tenupper', seed, stair)
    bands = [(0.0, .15, F['brick-buff']), (2.85, 3.0, F['brick-buff']), (.62, .74, F['brick-blue'])]
    if stair:
        h = Hole(.8, 1.7, 1.25, 2.75, .15, .2)
        wall(g, T, fr, 0.0, BAY, 0.0, 3.0, st.wall, [h], bands=bands, base=0.0)
        reveal(g, T, fr, h, st.wall)
        sill(g, T, fr, h.u0, h.u1, h.v0, F['stone-dress'], lean=True)
        arch_ring(g, T, fr, h, F['brick-buff'], .22, .03, None)
        ck.sash_window(g, T, fr, h, 'lit-stair' if 'lit-stair' in F else 'lit', r, F['paint-white'], bars=False, simple=True)
        return
    h = Hole(.7, 1.8, .75, 2.47, .18, .2)
    wall(g, T, fr, 0.0, BAY, 0.0, 3.0, st.wall, [h], bands=bands, base=0.0)
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, F['stone-dress'], lean=True)
    arch_ring(g, T, fr, h, F['brick-buff'], .22, .03, F['stone-dress'] if T.detail else None, key=F['stone-dress'] if seed % 2 else None)
    st.window(B, fr, h, r, frame=F['paint-white'], simple=seed % 3 == 0)
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.4, .34)


for s in range(3):
    upper('u.ten.%d' % s, (lambda ss: (lambda B: ten_upper(B, 81 + ss * 3)))(s))
upper('u.stair', lambda B: ten_upper(B, 90, True))
for s in range(2):
    upper('u.ware.%d' % s, (lambda ss: (lambda B: ware_bay(B, 'win', 31 + ss, upper=True)))(s))
    upper('u.civic.%d' % s, (lambda ss: (lambda B: civic_bay(B, 41 + ss, upper=True)))(s))


def blind_upper(B, seed, kind):
    """A blind upper bay (board 3's fix for bare walls): a bricked-up window in its reveal under its head, or plain wall."""
    g, T = B.g, B.T
    fr = bay_frame()
    st, r = style(seed, True)
    if kind == 'plain':
        wall(g, T, fr, 0.0, BAY, 0.0, 3.0, st.wall, base=0.0)
        smould(g, T, fr, 0.0, BAY, 0.0, ck.P_STRING_SMALL, st.dress)
        B.streaks.add_under(fr, .3, BAY - .3, 3.0, 2.2, .26)
        return
    h = Hole(.7, 1.8, .75, 2.65, .16, .1)
    wall(g, T, fr, 0.0, BAY, 0.0, 3.0, st.wall, [h], base=0.0)
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, st.dress, lean=True)
    cb.window_head(B, fr, h, st)
    fquad(g, fr, h.u0, h.v0, h.u1, h.top_v, -h.depth, F['brick-infill'])
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.3, .32)


upper('u.plain', lambda B: blind_upper(B, 51, 'plain'))
for s in range(2):
    upper('u.blind.%d' % s, (lambda ss: (lambda B: blind_upper(B, 53 + ss, 'blind')))(s))


# ---------------------------------------------------------------------------------------------------------------------------
# Cornices (placed at the roof line, hanging under it: y <= 0), ghost signs, plates, marks, puddles, hatches
# ---------------------------------------------------------------------------------------------------------------------------
def top_band(B, f, big=False, dentils=False):
    g, T = B.g, B.T
    fr = Frame((-BAY / 2, -.95, 0.0), (1.0, 0.0, 0.0))
    prof = ck.P_CORNICE_BIG if big else ck.P_CORNICE
    hgt = prof[-1][1]
    mould(g, T, fr, 0.0, BAY, .95 - hgt, mould_lod(T, prof), f)
    smould(g, T, fr, 0.0, BAY, .0, ck.P_STRING_SMALL, f)
    if dentils and T.detail:
        for i in range(8):
            x = .1 + i * .3
            fbox(g, T, fr, x, x + .14, .95 - hgt - .12, .95 - hgt, 0.0, .1, f, .006, skip=('-z',))


module('t.brick')(lambda B: top_band(B, F['stone-dress'], dentils=True))
module('t.render')(lambda B: top_band(B, F['stone-dress']))
module('t.ten')(lambda B: top_band(B, F['stone-dress'], big=True, dentils=True))
module('t.ware')(lambda B: top_band(B, F['brick-blue']))
module('t.civic')(lambda B: top_band(B, F['stone-civic'], big=True, dentils=True))


def ghost(B, k):
    """A ghost sign over three bays (board 3): faded paint 6 mm proud, its letters kept to LOD1."""
    g, T = B.g, B.T
    fr = Frame((-3.75, 0.0, 0.0), (1.0, 0.0, 0.0))
    lines = cb.GHOST_SIGNS[k % len(cb.GHOST_SIGNS)]
    ck.ghost_sign(g, T, fr, .2, 7.3, .2, 4.3, cb.fit_lines(lines, 7.0), ground=cb.GHOST_GROUNDS[k % 4], rnd=rng('ghost', k), streaks=B.streaks)


for k in range(4):
    module('ghost.%d' % k, hug=True)((lambda kk: (lambda B: ghost(B, kk)))(k))


def plate(B, name):
    """A street-name plate (bible 6.7): cream letters on green enamel, a black iron frame, 2 cm proud."""
    g, T = B.g, B.T
    fr = Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0))
    px = .045
    w = ck.text_width(name, px) + .3
    fbox(g, T, fr, -w / 2, w / 2, -.2, .2, 0.0, .02, F['iron-black'], .004, skip=('-z',))
    fquad(g, fr, -w / 2 + .03, -.17, w / 2 - .03, .17, .021, F['enamel-green'])
    text(g, T, fr, 0.0, -.16, px, name, F['enamel-cream'], c=.021, depth=.008)


for layout, names in meridian_sheet.STREETS.items():
    for i, name in enumerate(names):
        module('plate.%d.%d' % (layout, i), hug=True)((lambda nn: (lambda B: plate(B, nn)))(name))


def ground_frame():
    """Paint laid on the street, read by a pilot walking toward +z (home): the kit lays letters for a reader in front of the
    frame with its u to the reader's left, so u runs along -x and v (the letters' up) along +z; n = u x v = +y."""
    return Frame((0.0, .004, 0.0), (-1.0, 0.0, 0.0), (0.0, 0.0, 1.0))


def road_text(B, s, px, f):
    g, T = B.g, B.T
    fr = ground_frame()
    text(g, T, fr, 0.0, 0.0, px, s, f, c=0.0, depth=.006)


@module('paint.sanctuary', flat=True)
def _sanct(B):
    """SANCTUARY on the road with its arrow pointing home (+z), white survivors' paint (board 3)."""
    g, T = B.g, B.T
    road_text(B, 'SANCTUARY', .1, F['spray-route'])
    fr = ground_frame()
    # the arrow: a shaft and a head, ahead of the word
    fquad(g, fr, -.12, .95, .12, 1.9, 0.0, F['spray-route'])
    g.face([fr.P(-.5, 1.9, 0.0), fr.P(.5, 1.9, 0.0), fr.P(0.0, 2.6, 0.0)], F['spray-route'], fr.n)


for d in '0123456789M':
    module('paint.' + d, flat=True)((lambda dd: (lambda B: road_text(B, dd, .1, F['spray-route'])))(d))


def puddle(B, k):
    """Standing water (bible 4.2: .88 smooth, flat faces 4 mm proud): a few overlapping blobs."""
    g, T = B.g, B.T
    r = rng('puddle', k)
    for j in range(2 + k % 2):
        cx, cz = r.uniform(-.8, .8), r.uniform(-.5, .5)
        rx, rz = r.uniform(.6, 1.3), r.uniform(.4, .9)
        n = 10 if T.lod == 0 else 6
        pts = [(cx + rx * math.cos(2 * math.pi * i / n) * (1 + .15 * math.sin(i * 2.3 + k)), .004 + .001 * j,
                cz + rz * math.sin(2 * math.pi * i / n)) for i in range(n)]
        g.face(pts[::-1], F['puddle'], (0.0, 1.0, 0.0))


for k in range(3):
    module('puddle.%d' % k, flat=True)((lambda kk: (lambda B: puddle(B, kk)))(k))


@module('roof.hatch', flat=True)
def _hatch(B):
    """A roof hatch laid flat (5 cm): its galvanised lid in an angle frame, the hinge side darker."""
    g, T = B.g, B.T
    sk.box(g, -.6, .6, 0.0, .03, -.6, .6, F['iron-black'], T, bevel=.01)
    sk.box(g, -.5, .5, .03, .05, -.5, .5, F['galv'], T, bevel=.008)


# ---------------------------------------------------------------------------------------------------------------------------
# Lamps (bible 6.2): the swan-neck bracket, the span pendant and its wire, the flush lamp; working, route (warmer), dead
# ---------------------------------------------------------------------------------------------------------------------------
def swan(B, state):
    """The swan-neck: its wall plate centred on the origin, the dish 1.0 m out, the bowl's centre about 0.37 m under the plate
    (Core NativeMeridianDress.SwanReach and SwanDrop)."""
    cb.swan_neck(B, Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0)), 0.0, -.45)
    if state != 'working':
        # re-finish the bowl: its glow faces (sodium) become the route's warmer glass or the dead lamp's cracked dark bowl
        sodium = F['sodium']
        new = F['sodium-route'] if state == 'route' else F['sodium-dead']
        B.g.fin = __import__('array').array('H', [new if f == sodium else f for f in B.g.fin])


for state in ('working', 'route', 'dead'):
    module('lamp.swan' + ('' if state == 'working' else '.' + state))((lambda ss: (lambda B: swan(B, ss)))(state))


def pendant(B, state):
    """A span pendant: a short drop from the wire, a disc reflector over a globe (about 0.5 m under the wire)."""
    g, T = B.g, B.T
    sk.cylinder(g, (0.0, 0.0, 0.0), (0.0, -.22, 0.0), .015, F['iron-black'], T.small)
    ck.lathe_shell(g, [(.32, -.02), (.32, 0.0), (.22, .05), (.06, .1), (0.0, .11)], (0.0, -.33, 0.0), F['iron-green'], T.round, .015)
    fg = F['sodium'] if state == 'working' else F['sodium-route'] if state == 'route' else F['sodium-dead']
    sk.lathe(g, [(0.0, -.26), (.1, -.24), (.17, -.16), (.19, -.07), (.16, -.02)], (0.0, -.33, 0.0), fg, T.round)


for state in ('working', 'route', 'dead'):
    module('lamp.pendant' + ('' if state == 'working' else '.' + state))((lambda ss: (lambda B: pendant(B, ss)))(state))


@module('lamp.wire')
def _wire(B):
    """A unit span of wire along x (0..1), 3 cm (the chunk factor), stretched to its span by the planner."""
    sk.box(B.g, 0.0, 1.0, -.015, .015, -.015, .015, F['iron-black'], B.T, bevel=0)


def flush(B, state):
    """A flush lamp set in a wall (bible 6.2's flush mount): a cast frame 3 cm proud, its lens set back, lit low."""
    g, T = B.g, B.T
    fr = Frame((-.22, -.14, 0.0), (1.0, 0.0, 0.0))
    fbox(g, T, fr, 0.0, .44, 0.0, .28, -.2, .03, F['iron-black'], .008)
    fg = F['lantern'] if state == 'working' else F['sodium-route'] if state == 'route' else F['sodium-dead']
    fquad(g, fr, .05, .05, .39, .23, .031, fg)
    for x in (.16, .28):
        fbox(g, T, fr, x - .01, x + .01, .05, .23, .031, .045, F['iron-black'], 0, skip=('-z',))


for state in ('working', 'route', 'dead'):
    module('lamp.flush' + ('' if state == 'working' else '.' + state), hug=True)((lambda ss: (lambda B: flush(B, ss)))(state))


# ---------------------------------------------------------------------------------------------------------------------------
# The 6:47 clock and the Civil Defence siren (wall mounted, overhead)
# ---------------------------------------------------------------------------------------------------------------------------
@module('clock.wall')
def _clock(B):
    """The 6:47 bracket clock (bible 2.4: a station bracket clock, 1.2 m): a wall plate, a scrolled arm, a drum with a dial
    each way along the street, the hands at 6:47."""
    g, T = B.g, B.T
    fr = Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0))
    fbox(g, T, fr, -.12, .12, -.4, .4, 0.0, .05, F['iron-green'], .01, skip=('-z',))
    ck.tube(g, sk.catmull([(0.0, .15, .05), (0.0, .3, .4), (0.0, .25, .8)], 4 if T.lod == 0 else 2), .04, F['iron-green'], T.small)
    c = (0.0, -.2, .95)
    sk.cylinder(g, (c[0] - .09, c[1], c[2]), (c[0] + .09, c[1], c[2]), .62, F['iron-green'], T.round)
    for side in (1, -1):
        o = (c[0] + side * .095, c[1], c[2])
        ff = Frame((o[0], o[1] - .6, o[2] + .6 * side), (0.0, 0.0, -side * 1.0))
        cb.dial(B, ff, .6, .6, .55)


@module('cd.siren')
def _siren(B):
    """A Civil Defence siren on a wall bracket (bible 6.7), olive with an orange band, its two horns along the wall."""
    g, T = B.g, B.T
    fr = Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0))
    fbox(g, T, fr, -.15, .15, -.5, .5, 0.0, .06, F['cd-olive'], .01, skip=('-z',))
    sk.cylinder(g, (0.0, 0.0, .06), (0.0, 0.0, .7), .06, F['cd-olive'], T.small)
    c = (0.0, 0.0, .75)
    sk.cylinder(g, (c[0], c[1] - .25, c[2]), (c[0], c[1] + .25, c[2]), .28, F['cd-olive'], T.round)
    sk.cylinder(g, (c[0], c[1] - .05, c[2]), (c[0], c[1] + .05, c[2]), .29, F['cd-orange'], T.round)
    for side in (1, -1):
        sk.cylinder(g, (c[0] + side * .2, c[1] + .1, c[2]), (c[0] + side * .75, c[1] + .1, c[2]), .12, F['cd-olive'], T.round, r1=.3)


# ---------------------------------------------------------------------------------------------------------------------------
# Fill modules (stretched to their blocks): parapets (2.5 x 1.1 x 0.5 m), the arcade post (1 x 6 x 1 m), the wagon (12 x 3.2
# x 3 m), the viaduct's arch (a 10 m span under a 12 m deep deck)
# ---------------------------------------------------------------------------------------------------------------------------
@module('parapet.iron', hug=True)
def _parapet_iron(B):
    """An iron parapet panel filling its box: a plinth rail, a top rail, uprights and a crest roundel, municipal green."""
    g, T = B.g, B.T
    f = F['iron-green']
    sk.box(g, -1.25, 1.25, 0.0, .18, -.25, .25, f, T, bevel=.01)
    sk.box(g, -1.25, 1.25, .98, 1.1, -.25, .25, f, T, bevel=.01)
    for x in (-1.2, 1.2):
        sk.box(g, x - .05, x + .05, .18, .98, -.25, .25, f, T, bevel=.008)
    n = 11 if T.lod == 0 else 5
    for i in range(1, n):
        x = -1.25 + 2.5 * i / n
        sk.box(g, x - .018, x + .018, .18, .98, -.04, .04, f, T, bevel=0)
    sk.box(g, -1.15, 1.15, .86, .9, -.04, .04, f, T, bevel=0)


@module('parapet.stone', hug=True)
def _parapet_stone(B):
    """A stone balustrade filling its box: a plinth, chunky balusters (the chunk factor), a coping."""
    g, T = B.g, B.T
    f = F['stone-dress']
    sk.box(g, -1.25, 1.25, 0.0, .22, -.25, .25, f, T, bevel=.015)
    sk.box(g, -1.25, 1.25, .92, 1.1, -.25, .25, f, T, bevel=.015)
    for i in range(5):
        x = -1.0 + i * .5
        if T.lod == 0:
            sk.lathe(g, [(.0, .22), (.1, .22), (.1, .28), (.07, .34), (.12, .52), (.06, .78), (.08, .86), (.1, .92), (0.0, .92)], (x, 0.0, 0.0), f, T.round)
        else:
            sk.box(g, x - .08, x + .08, .22, .92, -.08, .08, f, T, bevel=0)


@module('post.iron')
def _post(B):
    """A cast-iron market column filling a 1 x 6 x 1 m post: a square base, a fluted shaft, a capital with brackets."""
    g, T = B.g, B.T
    f = F['iron-green']
    sk.box(g, -.5, .5, 0.0, .5, -.5, .5, f, T, bevel=.02)
    sk.lathe(g, [(.0, .5), (.38, .5), (.32, .7), (.24, .8), (.22, 4.9), (.3, 5.1), (.42, 5.4), (.5, 5.55), (.5, 6.0), (0.0, 6.0)], (0.0, 0.0, 0.0), f, T.round)


@module('wagon.van')
def _wagon(B):
    """A railway goods van filling a 12 x 3.2 x 3 m wagon block: oxide red boards on a black underframe, a curved roof."""
    g, T = B.g, B.T
    sk.box(g, -6.0, 6.0, 0.0, .9, -1.4, 1.4, F['iron-black'], T, bevel=.02)
    for x in (-4.2, -2.8, 2.8, 4.2):
        sk.cylinder(g, (x, .45, -1.5), (x, .45, 1.5), .45, F['iron-black'], T.round)
    sk.box(g, -6.0, 6.0, .9, 2.85, -1.5, 1.5, F['wagon-oxide'], T, bevel=.02)
    for x in (-5.0, -2.5, 0.0, 2.5, 5.0):
        sk.box(g, x - .05, x + .05, .9, 2.85, -1.52, 1.52, F['iron-black'], T, bevel=0)
    n = 8 if T.lod == 0 else 4
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        p = lambda a: (1.5 * math.cos(a), 2.85 + .35 * math.sin(a))
        (z0, y0), (z1, y1) = p(a0), p(a1)
        g.face([(-6.0, y0, z0), (6.0, y0, z0), (6.0, y1, z1), (-6.0, y1, z1)], F['lead'], (0.0, (y0 + y1) / 2 - 2.85, (z0 + z1) / 2))
    for x in (-6.0, 6.0):
        pts = [(x, 2.85 + .35 * math.sin(math.pi * k / n), 1.5 * math.cos(math.pi * k / n)) for k in range(n + 1)]
        g.face(pts if x > 0 else pts[::-1], F['wagon-oxide'], (1.0 if x > 0 else -1.0, 0.0, 0.0))


@module('viaduct.arch')
def _arch(B):
    """The Stride's arch (bible 5.6): blue engineering brick spandrels and a semicircular soffit hung from the deck's underside
    over a 10 m span, voussoirs of brick and stone, the face on z 0 and the arch running 12 m back (z to -12)."""
    g, T = B.g, B.T
    span, rise, depth = 10.0, 4.0, 12.0
    n = 12 if T.lod == 0 else 6
    pts = [(-span / 2 + span * (.5 - .5 * math.cos(math.pi * k / n)), -rise + rise * math.sin(math.pi * k / n)) for k in range(n + 1)]
    for zf, nz in ((0.0, 1.0), (-depth, -1.0)):
        for k in range(n):
            (x0, y0), (x1, y1) = pts[k], pts[k + 1]
            face = [(x0, y0, zf), (x1, y1, zf), (x1, 0.0, zf), (x0, 0.0, zf)]
            g.face(face if nz > 0 else face[::-1], F['brick-blue'], (0.0, 0.0, nz))
        if T.detail:
            for k in range(0, n, 2):
                (x0, y0), (x1, y1) = pts[k], pts[k + 1]
                fz = zf + .03 * nz
                g.face([(x0, y0, fz), (x1, y1, fz), (x1 * .97, y1 + .45, fz), (x0 * .97, y0 + .45, fz)][::(1 if nz > 0 else -1)],
                       F['stone-dress'], (0.0, 0.0, nz))
    for k in range(n):
        (x0, y0), (x1, y1) = pts[k], pts[k + 1]
        g.face([(x0, y0, -depth), (x1, y1, -depth), (x1, y1, 0.0), (x0, y0, 0.0)], F['brick-blue'], (-(y1 - y0), x1 - x0, 0.0))


# ---------------------------------------------------------------------------------------------------------------------------
# Build, hug, bake, bank
# ---------------------------------------------------------------------------------------------------------------------------
def clamp_geo(g, axis):
    """Every corner flattened into 0.25 m in .. 6 cm out of the face (axis 2, z), or for a flat piece into 6 cm over its floor
    (axis 1, y); faces that collapse are dropped."""
    P, N, S, Fi = g.arrays()
    P = P.copy()
    P[:, axis] = np.clip(P[:, axis], HUG_IN if axis == 2 else -.01, HUG_OUT)
    out = sk.Geo()
    starts = np.zeros(len(S), dtype=np.int64)
    if len(S) > 1:
        starts[1:] = np.cumsum(S[:-1])
    for f in range(len(S)):
        idx = range(starts[f], starts[f] + S[f])
        pts = [tuple(float(x) for x in P[i]) for i in idx]
        nrm = [tuple(float(x) for x in N[i]) for i in idx]
        a = np.array(pts)
        area = 0.0
        for k in range(1, len(pts) - 1):
            area += np.linalg.norm(np.cross(a[k] - a[0], a[k + 1] - a[0])) * .5
        if area < 1e-6:
            continue
        out.face(pts, int(Fi[f]), normals=nrm)
    return out


def build_all():
    ck.finishes()
    extra_finishes()
    keys = [k for k in MODULES if ONLY is None or k in ONLY]
    builds = {}
    for key in keys:
        fn, fl = MODULES[key]
        for lod in (0, 1):
            T = ck.CityTier(lod)
            B = cb.Build(key, T)
            fn(B)
            g = B.g
            if fl['hug']:
                g = clamp_geo(g, 2)
            if fl['flat']:
                g = clamp_geo(g, 1)
            B.g = g
            builds[(key, lod)] = B
    return keys, builds


def main():
    t0 = time.time()
    keys, builds = build_all()
    print('built %d modules in %.1fs' % (len(keys), time.time() - t0))
    # bake: every module laid out apart (12 m), its neighbours (a bay either side) and a ground and a wall plane as occluders
    items, place = [], {}
    x = 0.0
    for key in keys:
        place[key] = x
        x += 14.0
    geos, occ = [], []
    order = []
    for key in keys:
        for lod in (0, 1):
            B = builds[(key, lod)]
            M = [1, 0, 0, place[key], 0, 1, 0, 0, 0, 0, 1, -60.0 * lod]
            geos.append(B.g.transformed(M))
            order.append((key, lod))
            fl = MODULES[key][1]
            if (key.startswith('g.') or key.startswith('u.')) and lod == 0:
                for dx in (-BAY, BAY):
                    occ.append(B.g.transformed([1, 0, 0, place[key] + dx, 0, 1, 0, 0, 0, 0, 1, 0.0]))
    ground = sk.Geo()
    ground.face(((-50.0, 0.0, -150.0), (x + 50, 0.0, -150.0), (x + 50, 0.0, 30.0), (-50.0, 0.0, 30.0))[::-1], 0, (0, 1, 0))
    occ.append(ground)
    t = time.time()
    aos = sk.bake_ao(geos, 1.0, 16, occ) if AO else [None] * len(geos)
    print('baked in %.1fs' % (time.time() - t))
    bank = sk.StageBank()
    stats = {}
    for (key, lod), gw, ao in zip(order, geos, aos):
        B = builds[(key, lod)]
        bk = sk.Bake(B.g, ao, floors=(0.0,), seed=7)
        P, N, S, Fi = B.g.arrays()
        B.streaks.apply(bk.colour, P, N)
        om = sk.to_mesh('module/%s/%d' % (key, 0 if lod == 0 else 2), B.g, bk)
        if om is None:
            continue
        bank.add(om.name, 'module', om)
        tris = sum(len(s) for s in om.submeshes) // 3
        stats.setdefault(key, {})['lod%d' % lod] = tris
        lo, hi = om.bounds()
        stats[key]['bounds%d' % lod] = [[round(v, 3) for v in lo], [round(v, 3) for v in hi]]
    os.makedirs(OUT, exist_ok=True)
    # the bank goes to OUT/ChipArenaLocal/Resources (the layout stagekit writes); copy it into an Editor checkout to capture
    path = bank.write(OUT, BANK)
    with open(os.path.join(OUT, 'meridian-bank-counts.json'), 'w') as fh:
        json.dump(stats, fh, indent=1, sort_keys=True)
    # the hugging check (the integrator's rule), on the written meshes
    bad = []
    for key in keys:
        fl = MODULES[key][1]
        for lod in (0, 1):
            b = stats.get(key, {}).get('bounds%d' % lod)
            if not b:
                continue
            if fl['hug'] and (b[1][2] > HUG_OUT + 1e-3 or b[0][2] < HUG_IN - 1e-3):
                bad.append((key, lod, b))
            if fl['flat'] and b[1][1] > HUG_OUT + 1e-3:
                bad.append((key, lod, b))
    print('hug check:', 'ok' if not bad else bad[:10])
    total0 = sum(v.get('lod0', 0) for v in stats.values())
    total1 = sum(v.get('lod1', 0) for v in stats.values())
    print('modules %d, triangles LOD0 %d, LOD1 %d, bank %s (%d bytes) in %.1fs' % (len(stats), total0, total1, path, os.path.getsize(path), time.time() - t0))


main()
