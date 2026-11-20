"""Meridian's buildings (tools/stages/meridian_buildings.py: board 3's approved city_buildings, pass 2, ported unchanged but for
its kit's name; meridian_bank.py builds the game's facade bays from it) (the MERIDIAN art bible, section 5): the Rows' terraced-house facade kit, the Junctions' corner shop,
the Walls' tenement and City Hall's clocktower, stopped at 6:47. Built from scratch on city_kit (stagekit's corner geometry,
Unity space: metres, +y up) for each tier: LOD0 and LOD1 (bible 10.2).

The grid (bible 2.3): 2.5 m bays, a 4.0 m ground storey and 3.0 m upper storeys, so buildings are 7, 10, 13, 16, 19 or 22 m.
Every module snaps to it: a bay module spans u 0..2.5 of its facade frame, a storey module v 0..3.0 over its storey's floor.

The modules (MODULES, `module/<family>.<kind>.<variant>/<tier>` in the bank, bible 11.1):
  row.ground.door      the door bay: panelled door in a 0.2 m reveal, fanlight with the house number, step, surround, hood,
                       porch lantern at 3.2 m, plinth, string course at 4.0 m                             (2.5 x 4.0 m)
  row.ground.window    the parlour bay: a tall two-over-two sash                                           (2.5 x 4.0 m)
  row.ground.shop      a shopfront bay: stallriser, lit display, transom, fascia with letters, console cornice (2.5 x 4.0)
  row.ground.shutter   the shopfront with its roller shutter half down                                     (2.5 x 4.0 m)
  row.upper.window     an upper bay: two-over-two sash, reveal, sill with drip, a seeded head              (2.5 x 3.0 m)
  row.upper.oriel      the canted oriel on stone corbels, from 4.0 m to under the eaves                   (2.5 m, 1 storey)
  row.roof.slate       a house's roof: 40 deg lapped slate, eaves, gutter, ridge, party-wall upstand, a stack of four pots
  row.gable.end        a run's end, breast variant: chimney breast into the end stack, sashes and blind windows, oculus,
                       string courses, pattress plates, downpipe, coping, kneelers, street-name plate         (pass 2)
  row.gable.ghost      a run's end, ghost-sign variant: a faded painted sign over the top storey, windows below (pass 2)
  row.house            a whole 5 m house of two storeys with its roof (the kit assembled; bible budget 4,500 / 450)
  corner.shop          the corner shop: chamfered corner, wrapped shopfront, corner turret under a lead dome, parapet; its
                       exposed party walls carry a ghost sign and a demolished neighbour's scar (pass 2)
  tenement.face        a 10 x 19 m tenement face: polychrome brick, segmental arches, the entrance under the corbelled
                       stair tower (pass 2), its half-landing windows lit
  tenement.escape      a plain face with its zigzag fire escape (bible budget 16,000 / 1,600 per face)
  tenement.back        the back court's lean face (pass 2)
  tenement.roofkit     the roof kit: stair house, water tank on legs, vents, aerials, hatch
  tenement.block       the block assembled: entry and shop faces, the escape end, the back court's faces, crown, roof, kit
  tenement.street      a 40 m tenement street of four blocks with washing lines: the chunk-budget check (pass 2)
  civic.clocktower     City Hall's tower alone: rusticated base, shaft, four 6 m opal dials at 6:47, belfry, the richer top
                       (pinnacles, urns, scrolls, drum columns, gilt-ribbed dome, lucarnes, lit lantern, aircraft light, vane)
  civic.cityhall       City Hall round its tower: portico of three pairs of columns, pediment crest, steps, wings, domed
                       pavilions; a far mesh too (FAR, build(name, 2)) (pass 2)

Each builder returns a Build: its Geo, rain-streak sources for the bake, the practical lights it carries (for the board), the
window fills it drew and its footprint. Variants come from a seed (bible 2.6 rule 5: no identical facade within 30 m). Every
swept or tubular part goes through city_kit's own sweep and tube (pass 2: stagekit's are inside out).
"""
import math

import meridian_kit as ck
from meridian_kit import (F, BAY, GROUND, STOREY, PLINTH, Frame, Hole, fbox, fquad, prism, wall, reveal, back_fill, mould,
                      smould, mould_lod, sill, cornice, console, sash_window, panel_door, text, text_width, downpipe,
                      gutter, chimney_pot, lantern, plan_mould, quoins, arch_ring, rng, storey_floor, frame_ring, bar, cut)
import stagekit as sk
from stagekit import Geo

SQ2 = math.sqrt(.5)


class Build:
    """A module's build for one tier."""

    def __init__(self, name, T):
        self.name, self.T = name, T
        self.g = Geo()
        self.streaks = ck.Streaks()
        self.lights = []      # (position, hex colour, radius, kind)
        self.floors = [0.0]
        self.windows = []     # the fills of the windows drawn (the window-mix check)

    def light(self, p, hexcol, radius=6.0, kind='lantern'):
        self.lights.append((tuple(p), hexcol, radius, kind))


# ---------------------------------------------------------------------------------------------------------------------------
# Styles (bible 5.1 variants by seed)
# ---------------------------------------------------------------------------------------------------------------------------
RENDERS = ('render-cream', 'render-mint', 'render-rose', 'render-powder', 'render-ochre')
DOORS = ('paint-green', 'paint-oxblood', 'paint-navy', 'paint-black', 'paint-cream', 'paint-mustard')


# The window mix by district (bible 3.1 and 4.4): the share of windows lit (12% in the neutral town, 25% round the camp, every
# window in Level 0's drift is the bleed's and not drawn here), the share of ground-floor windows boarded (30% in the neutral
# town, 60% near the instances) and of upper windows (not set by the bible: a few), Ward Zero's taped X, smashed panes.
DISTRICTS = {
    'neutral': dict(lit=.12, board_ground=.30, board_upper=.05, tape=0.0, broken=.03),
    'camp': dict(lit=.25, board_ground=.18, board_upper=.03, tape=0.0, broken=.02),
    'instance': dict(lit=.12, board_ground=.60, board_upper=.14, tape=0.0, broken=.06),
    'ward': dict(lit=.12, board_ground=.60, board_upper=.12, tape=.35, broken=.05),
}
DISTRICT = 'neutral'      # the default for every Style (an assembly may set its own)


class Style:
    """A house's seeded look: render colour or brick, door colour, window heads, the district's window mix, number."""

    def __init__(self, seed, brick=None, lit=None, ward=False, district=None):
        r = rng('style', seed)
        self.seed = seed
        self.brick = (r.random() < .3) if brick is None else brick
        self.wall = F['brick-stock'] if self.brick else F[r.choice(RENDERS)]
        self.plinth = F['brick-blue'] if self.brick else F['paint-plinth']
        self.dress = F['stone-dress']
        self.door = F[r.choice(DOORS)]
        self.frame = F['paint-white'] if r.random() < .8 else F['paint-cream']
        self.head = 'arch' if self.brick else r.choice(('lintel', 'hood', 'hood'))
        self.number = r.randint(2, 96)
        self.district = district or ('ward' if ward else DISTRICT)
        self.mix = dict(DISTRICTS[self.district])
        if lit is not None:
            self.mix['lit'] = lit
        self.lit = self.mix['lit']
        self.ward = self.district == 'ward'
        self.r = r

    def fill(self, r, key=None, ground=False):
        """A window's fill, from a generator of its own when given its key (its place): every tier draws the same. The
        shares are exact by construction: P(lit) = the district's lit share; P(boarded) its ground or upper share; a TV's
        blue flicker in 1 lit window of 40 (bible 3.1)."""
        if key is not None:
            r = rng('fill', self.seed, key)
        m = self.mix
        x = r.random()
        if x < m['lit']:
            if r.random() < 1 / 40:
                return 'tv'
            return r.choice(('lit', 'lit', 'lit-bright', 'lit-curtain', 'lit-blind'))
        if x < m['lit'] + (m['board_ground'] if ground else m['board_upper']):
            return 'board'
        y = r.random()
        if y < m['tape']:
            return 'tape'
        if y < m['tape'] + m['broken']:
            return 'broken'
        z = r.random()
        if z < .3:
            return 'curtain'
        if z < .48:
            return 'blind'
        return 'dark'

    def mark(self, key):
        """A boarded window's sprayed mark (the survivors' searches): about one board in three."""
        r = rng('mark', self.seed, key)
        return r.choice(('x', 'ring')) if r.random() < .35 else None

    def window(self, B, fr, h, r, ground=False, frame=None, bars=True, simple=False):
        """The sash in opening h with this house's fill for its place (and a sprayed mark if boarded); returns the fill."""
        key = ck.key_of(fr, h.u0, h.v0)
        fill = self.fill(r, key, ground=ground)
        sash_window(B.g, B.T, fr, h, fill, r, self.frame if frame is None else frame, bars=bars, simple=simple,
                    mark=self.mark(key) if fill == 'board' else None)
        B.windows.append(fill)
        return fill


# ---------------------------------------------------------------------------------------------------------------------------
# The Rows: terraced-house bays
# ---------------------------------------------------------------------------------------------------------------------------
def plinth_run(B, fr, u0, u1, f, holes=(), top=PLINTH):
    """The dark plinth, 4 cm proud, with its weathered cap (bible 2.3: .6-.9 m, it takes the grime)."""
    g, T = B.g, B.T
    wall(g, T, fr, u0, u1, 0.0, top, f, holes, c=.04, base=fr.o[1])
    cuts = [u0, u1]
    for h in holes:
        if h.v0 < top:
            cuts += [h.u0, h.u1]
    cuts = sorted(set(cuts))
    for i in range(len(cuts) - 1):
        a, b = cuts[i], cuts[i + 1]
        if b - a < .05 or any(h.u0 <= (a + b) / 2 <= h.u1 and h.v0 < top for h in holes):
            continue
        smould(g, T, fr, a, b, top, ck.P_PLINTH_CAP, f)


def window_head(B, fr, h, st):
    """The head over an opening: a stone lintel, a moulded hood with label stops, or (brick) a segmental arch of buff brick
    with a stone key."""
    g, T = B.g, B.T
    if T.lod > 0:
        return
    if st.head == 'arch' and h.rise > 0:
        arch_ring(g, T, fr, h, F['brick-buff'], .23, .03, None, key=st.dress)
    elif st.head == 'hood':
        mould(g, T, fr, h.u0 - .16, h.u1 + .16, h.top_v + .02, mould_lod(T, ck.P_HOOD), st.dress)
        if T.detail:
            for x in (h.u0 - .16, h.u1 + .08):
                fbox(g, T, fr, x, x + .08, h.top_v - .2, h.top_v + .02, 0.0, .1, st.dress, .01, skip=('-z',))
    else:
        fbox(g, T, fr, h.u0 - .14, h.u1 + .14, h.top_v, h.top_v + .26, 0.0, .05, st.dress, .012, skip=('-z',))
        if T.detail:   # a keyblock
            um = (h.u0 + h.u1) / 2
            fbox(g, T, fr, um - .1, um + .1, h.top_v - .04, h.top_v + .3, .05, .09, st.dress, .01, skip=('-z',))
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.top_v + .02, 1.0, .18)


def ground_door_bay(B, fr, st, r, lantern_side=1):
    """The door bay (bible 5.1): a panelled door in a 0.2 m reveal, a fanlight with the house number, the step inside the
    reveal, an architrave surround with a hood on consoles (within .25 m below 3.1 m), a porch lantern at 3.2 m."""
    g, T = B.g, B.T
    h = Hole(.55, 1.95, 0.0, 2.75, 0.0, .20)
    surround = Hole(.37, 2.13, 0.0, 2.95)
    plinth_run(B, fr, 0.0, BAY, st.plinth, [surround])
    wall(g, T, fr, 0.0, BAY, PLINTH, 3.92, st.wall, [surround], base=fr.o[1])
    lit = rng('fanlight', st.seed, ck.key_of(fr, 0.0, 0.0)).random() < .55
    if T.lod > 0:
        fquad(g, fr, .55, 0.0, 1.95, 2.38, .06, st.door)
        fquad(g, fr, .55, 2.38, 1.95, 2.75, .06, F['fanlight'] if lit else F['glass-dark'])
        fquad(g, fr, .37, 2.75, 2.13, 3.11, .06, st.dress)
        lu = 2.32 if lantern_side > 0 else .18
        lantern(g, T, fr.P(lu, 3.33, .24), .16, F['iron-black'], F['lantern'] if lit else F['glass-dark'])
        if lit:
            B.light(fr.P(lu, 3.33, .4), 0xFFB054, 4.5, 'porch')
        return
    reveal(g, T, fr, h, F['paint-white'] if not st.brick else st.dress, sill=False)
    # the step, the door's frame, the leaf, the transom and the fanlight
    fbox(g, T, fr, h.u0, h.u1, 0.0, .16, -.2, .02, F['granite'], .015, skip=('-z',))
    frame_ring(g, fr, h.u0, h.u1, .16, 2.75, .07, -.2, -.13, F['paint-white'], sides=(1, 1, 0, 1))
    panel_door(g, T, fr, .62, 1.88, .16, 2.30, -.15, st.door, r, panels=4 if r.random() < .6 else 6)
    bar(g, T, fr, .62, 1.88, 2.30, 2.38, -.2, -.12, F['paint-white'])
    fl = F['fanlight'] if lit else F['glass-dark']
    fquad(g, fr, .62, 2.38, 1.88, 2.68, -.16, fl)
    if T.detail:
        for x in (1.04, 1.46):
            bar(g, T, fr, x - .02, x + .02, 2.38, 2.68, -.16, -.13, F['paint-white'])
        text(g, T, fr, 1.25, 2.44, .026, str(st.number), F['gilt'], c=-.155, raised=False)
    # the surround: stiles on plinth blocks, the frieze, consoles and the hood
    for (a, b) in ((.37, .55), (1.95, 2.13)):
        fbox(g, T, fr, a, b, 0.0, 2.75, 0.0, .07, st.dress, .012, skip=('-z', '-y'))
    fbox(g, T, fr, .37, 2.13, 2.75, 2.95, 0.0, .07, st.dress, .012, skip=('-z',))
    mould(g, T, fr, .27, 2.23, 2.95, [(0.0, 0.0), (.09, 0.0), (.09, .04), (.16, .08), (.22, .1), (.22, .15), (0.0, .15)],
          st.dress)
    if T.detail:
        for x in (.46, 2.04):
            console(g, T, fr, x, 2.95, .13, .34, .15, st.dress)
    B.streaks.add_under(fr, .27, 2.23, 2.95, 1.6, .25)
    # the porch lantern at 3.2 m on a bracket (overhead art), its light for the board
    lu = 2.32 if lantern_side > 0 else .18
    fbox(g, T, fr, lu - .05, lu + .05, 3.36, 3.62, 0.0, .03, F['iron-black'], 0, skip=('-z',))
    fbox(g, T, fr, lu - .015, lu + .015, 3.52, 3.56, 0.0, .24, F['iron-black'], 0, skip=('-z',))
    lantern(g, T, fr.P(lu, 3.33, .24), .16, F['iron-black'], F['lantern'] if lit else F['glass-dark'])
    if lit:
        B.light(fr.P(lu, 3.33, .4), 0xFFB054, 4.5, 'porch')
    smould(g, T, fr, 0.0, BAY, 3.92, ck.P_STRING, st.dress)


def ground_window_bay(B, fr, st, r):
    """The parlour bay: a tall sash in its reveal, sill, head; the plinth and the string course."""
    g, T = B.g, B.T
    rise = .18 if st.head == 'arch' else 0.0
    h = Hole(.6, 1.9, .85, 3.12 - rise, rise, .18)
    plinth_run(B, fr, 0.0, BAY, st.plinth)
    wall(g, T, fr, 0.0, BAY, PLINTH, 3.92, st.wall, [h], base=fr.o[1])
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, st.dress)
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.0, .3)
    window_head(B, fr, h, st)
    st.window(B, fr, h, r, ground=True)
    smould(g, T, fr, 0.0, BAY, 3.92, ck.P_STRING, st.dress)


SHOP_PAINTS = ('paint-green', 'paint-oxblood', 'paint-navy', 'paint-black')


def fascia_text(B, fr, u0, u1, word, px=.05, c=.12, depth=.025, f=None):
    """Board 2: a shop's fascia letters laid out in the clear span between its consoles (u0..u1), shrunk to fit with 5 cm
    to spare each side and centred on the fascia's height (3.4-3.96 m). Pass 2 centred them on a bay's edge, where a
    console stands in front of them (NE S, GR ER, CO LER)."""
    span = (u1 - u0) - .1
    w = text_width(word, px)
    if w > span:
        px = px * span / w
    v = 3.68 - 3.5 * px
    text(B.g, B.T, fr, (u0 + u1) / 2, v, px, word, F['gilt'] if f is None else f, c=c, depth=depth)


# the clear span of a shop bay's fascia between its pilasters' consoles (each .18 wide, centred .1 in from the bay's edge)
CONSOLE_CLEAR = .19


def ground_shop_bay(B, fr, st, r, lit=True, shutter=False, pil=(True, True), fascia=True, paint=None, goods=True, trade=None):
    """A shopfront bay (bible 2.3): pilasters with consoles, a .55 m stallriser, a 2.3 m display lit from within with the
    goods silhouetted, a .55 m transom, a .6 m fascia under a console cornice at 4.0 m; or its roller shutter half down."""
    g, T = B.g, B.T
    fp = paint if paint is not None else F[r.choice(SHOP_PAINTS)]
    a0, a1 = .2, BAY - .2
    if T.lod > 0:
        fquad(g, fr, 0.0, 0.0, BAY, 3.4, 0.0, fp)
        fquad(g, fr, a0, .55, a1, 3.35, .02, F['lit-shop'] if lit else F['glass-dark'])
        if shutter:
            fquad(g, fr, a0, 1.6, a1, 3.35, .04, F['shutter'])
        if fascia:
            fquad(g, fr, 0.0, 3.4, BAY, 4.0, .12, fp)
            g.face((fr.P(0.0, 3.4, 0.0), fr.P(BAY, 3.4, 0.0), fr.P(BAY, 3.4, .12), fr.P(0.0, 3.4, .12)), fp, fr.mv)
            g.face((fr.P(0.0, 4.0, 0.0), fr.P(BAY, 4.0, 0.0), fr.P(BAY, 4.0, .12), fr.P(0.0, 4.0, .12)), fp, fr.v)  # its top
        if lit:
            B.light(fr.P(BAY / 2, 1.7, .6), 0xFFC98A, 4.0, 'shop')
        return
    wall(g, T, fr, 0.0, BAY, 3.92, 4.0, st.wall, base=fr.o[1])
    for k, (pa, pb) in enumerate(((0.0, .2), (BAY - .2, BAY))):
        if not pil[k]:
            continue
        fbox(g, T, fr, pa, pb, 0.0, 3.4, 0.0, .1, fp, .012, skip=('-z', '-y'))
        if T.detail:
            fbox(g, T, fr, pa - .02, pb + .02, 0.0, .45, 0.0, .13, fp, .012, skip=('-z', '-y'))
            fbox(g, T, fr, pa - .015, pb + .015, 3.2, 3.4, 0.0, .13, fp, .01, skip=('-z',))
        console(g, T, fr, (pa + pb) / 2, 4.0, .18, .62, .3, fp)
    # if a pilaster is left out, the frame closes the bay with a narrow stile
    if not pil[0]:
        a0 = .05
    if not pil[1]:
        a1 = BAY - .05
    # the stallriser with a fielded panel
    fbox(g, T, fr, a0, a1, 0.0, .55, -.02, .05, fp, .01, skip=('-z', '-y'))
    if T.detail:
        fbox(g, T, fr, a0 + .1, a1 - .1, .1, .45, .05, .07, fp, .008, skip=('-z',))
    # the display: frame, mullion, glass (lit or dark) and the goods against it
    gl = -.1
    frame_ring(g, fr, a0, a1, .55, 2.85, .06, gl, 0.0, fp)
    fill = (F['lit-cold'] if trade in COLD_TRADES else F['lit-shop']) if lit else F['glass-dark']
    fquad(g, fr, a0 + .06, .61, a1 - .06, 2.79, gl, fill)
    if T.detail:
        xm = (a0 + a1) / 2
        bar(g, T, fr, xm - .03, xm + .03, .61, 2.79, gl, -.01, fp)
        if lit and goods:
            shop_goods(g, fr, a0 + .06, a1 - .06, .61, 2.79, gl, trade, r, xm)
    # the transom lights
    bar(g, T, fr, a0, a1, 2.85, 2.93, gl - .02, .02, fp)
    frame_ring(g, fr, a0, a1, 2.93, 3.4, .05, gl, 0.0, fp)
    fquad(g, fr, a0 + .05, 2.98, a1 - .05, 3.35, gl, fill)
    if T.detail:
        for k in (1, 2):
            x = a0 + (a1 - a0) * k / 3
            bar(g, T, fr, x - .02, x + .02, 2.98, 3.35, gl, -.06, fp)
    # the fascia and its cornice
    if fascia:
        fbox(g, T, fr, 0.0, BAY, 3.4, 3.96, 0.0, .12, fp, .012, skip=('-z',))
        smould(g, T, fr, 0.0, BAY, 3.96, ck.P_FASCIA_CORNICE, fp)
        B.streaks.add_under(fr, 0.0, BAY, 3.4, .8, .2)
    if shutter:
        roller_shutter(B, fr, a0, a1, r.uniform(1.4, 1.9))
    if lit:
        cold = trade in COLD_TRADES
        B.light(fr.P(BAY / 2, 1.7, .6), 0xDCEBFF if cold else 0xFFC98A, 3.4 if cold else 4.0, 'shop')


# board 2: the trades whose shops are lit by cold tubes (the rest by warm tungsten), and each trade's goods in the window
COLD_TRADES = ('LAUNDRY', 'CHEMIST', 'TOOLS')   # (board 3: HARDWARE's eight letters put its bay over the 900 cap)


def shop_goods(g, fr, ua, ub, v0, v1, gl, trade, r, xm):
    """Board 2 (the critique: every shop window showed one shelf-and-boxes silhouette): each trade's own goods against the
    lit glass, dark (F['interior']), about 32 faces at most. ua..ub, v0..v1: the glass; xm: the mullion (kept clear)."""
    c = gl + .006
    fi = F['interior']

    def R(a, b, c0, d):
        fquad(g, fr, a, b, c0, d, c, fi)

    def poly(pts):
        g.face([fr.P(u, v, c) for (u, v) in pts], fi, fr.n)

    def hump(cu, v, w, h, n=4):
        pts = [(cu - w / 2, v)] + [(cu - w / 2 * math.cos(math.pi * k / n), v + h * math.sin(math.pi * k / n))
                                   for k in range(1, n)] + [(cu + w / 2, v)]
        poly(pts)

    def ngon(cu, cv, rr, n=6, sy=1.0):
        poly([(cu + rr * math.cos(2 * math.pi * k / n), cv + rr * sy * math.sin(2 * math.pi * k / n)) for k in range(n)])

    halves = ((ua, xm - .05), (xm + .05, ub))
    t = trade or 'GROCER'
    if t == 'BAKERY':
        for y in (1.25, 1.95):
            R(ua, y - .03, ub, y + .01)
            for (a, b) in halves:
                for k in range(3):
                    hump(a + (b - a) * (k + .5) / 3, y + .01, (b - a) / 3 * .8, r.uniform(.1, .16), 3)
        R(xm - .3, .61, xm + .3, .66)
        for (a, b) in halves:
            cu = (a + b) / 2
            poly([(cu - .2, .66), (cu + .2, .66), (cu + .26, .86), (cu - .26, .86)])
    elif t == 'NEWS':
        for y in (.95, 1.55, 2.15):
            R(ua, y - .03, ub, y + .0)
            for (a, b) in halves:
                x = a + .04
                while x < b - .14:
                    w = r.uniform(.14, .2)
                    poly([(x, y), (x + w, y), (x + w + .06, y + .3), (x + .06, y + .3)])
                    x += w + .05
    elif t == 'DAIRY':
        for y in (.95, 1.75):
            R(ua, y - .03, ub, y + .02)
            for (a, b) in halves:
                x = a + .05
                while x < b - .08:
                    R(x, y + .02, x + .07, y + .22)
                    R(x + .02, y + .22, x + .05, y + .3)
                    x += .1
        R(ua + .05, 2.3, ub - .05, 2.36)
    elif t == 'BUTCHER':
        R(ua, 2.45, ub, 2.5)
        for (a, b) in halves:
            for k in range(3):
                cu = a + (b - a) * (k + .5) / 3
                R(cu - .006, 2.2, cu + .006, 2.45)
                poly([(cu, 2.2), (cu + .12, 2.0), (cu + .1, 1.72), (cu, 1.62), (cu - .1, 1.72), (cu - .12, 2.0)])
        R(ua, .61, ub, 1.15)
    elif t == 'COBBLER':
        for y in (1.15, 1.85):
            R(ua, y - .03, ub, y + .0)
            for (a, b) in halves:
                x = a + .05
                while x < b - .22:
                    poly([(x, y), (x + .2, y), (x + .2, y + .05), (x + .02, y + .05)])
                    R(x, y + .05, x + .08, y + .16)
                    x += .28
        poly([(xm - .35, .61), (xm - .1, .61), (xm - .1, 1.0), (xm - .2, 1.0), (xm - .2, .74), (xm - .35, .7)])
    elif t == 'CAFE':
        for (a, b) in halves:
            cu = (a + b) / 2
            R(cu - .28, 1.3, cu + .28, 1.35)
            R(cu - .03, .61, cu + .03, 1.3)
            for sgn in (-1, 1):
                R(cu + sgn * .42 - .03, .61, cu + sgn * .42 + .03, 1.45)
                R(cu + sgn * .42 - .1, 1.0, cu + sgn * .42 + .1, 1.04)
        R(ua, 1.85, ub, 1.9)
        poly([(xm - .25, 1.9), (xm - .15, 1.9), (xm - .12, 2.25), (xm - .28, 2.25)])
        ngon(xm - .2, 2.32, .08, 6)
    elif t == 'LAUNDRY':
        for (a, b) in halves:
            for k in range(2):
                x0 = a + (b - a) * k / 2 + .03
                x1 = a + (b - a) * (k + 1) / 2 - .03
                R(x0, .61, x1, 1.45)
                cu, cv = (x0 + x1) / 2, 1.05
                g.face([fr.P(cu + .17 * math.cos(2 * math.pi * q / 8), cv + .17 * math.sin(2 * math.pi * q / 8), c + .002)
                        for q in range(8)], F['lit-cold'], fr.n)
        R(ua, 1.95, ub, 1.99)
        for (a, b) in halves:
            x = a + .06
            while x < b - .16:
                for k in range(3):
                    R(x, 1.99 + k * .07, x + .16, 2.05 + k * .07)
                x += .22
    elif t == 'TAILOR':
        for (a, b) in halves:
            cu = (a + b) / 2
            R(cu - .015, .61, cu + .015, 1.3)
            poly([(cu - .2, 1.3), (cu + .2, 1.3), (cu + .24, 1.62), (cu + .16, 2.0), (cu - .16, 2.0), (cu - .24, 1.62)])
            ngon(cu, 2.06, .06, 6)
        R(ua, 2.35, ub, 2.39)
        x = ua + .05
        while x < ub - .1:
            if not (xm - .08 < x < xm + .05):
                R(x, 2.39, x + .09, 2.62)
            x += .14
    elif t == 'CHEMIST':
        for y in (1.0, 1.8):
            R(ua, y - .03, ub, y + .0)
            for (a, b) in halves:
                cu = (a + b) / 2
                ngon(cu, y + .22, .2, 8, 1.1)
                R(cu - .05, y + .42, cu + .05, y + .56)
        R(ua, 2.45, ub, 2.49)
        for (a, b) in halves:
            x = a + .05
            while x < b - .07:
                R(x, 2.49, x + .05, 2.62)
                x += .09
    elif t == 'TOOLS':
        for (a, b) in halves:
            x = a + .07
            while x < b - .07:
                hgt = r.uniform(.4, .7)
                R(x - .012, 2.6 - hgt, x + .012, 2.6)
                R(x - .06, 2.6 - hgt - .08, x + .06, 2.6 - hgt)
                x += .17
            cu = (a + b) / 2
            poly([(cu - .2, .61), (cu + .2, .61), (cu + .26, 1.05), (cu - .26, 1.05)])
    else:
        # GROCER, TOBACCO and the rest: shelves of boxes and tins over crates of produce
        for y in (1.62, 2.2):
            R(ua, y - .03, ub, y + .02)
            x = ua + .04
            while x < ub - .16:
                w = r.uniform(.08, .22)
                hgt = r.uniform(.12, .36)
                if r.random() < .8 and not (xm - .05 < x + w / 2 < xm + .05):
                    R(x, y + .02, x + w, y + .02 + hgt)
                x += w + r.uniform(.02, .1)
        for (a, b) in halves:
            R(a + .02, .61, b - .02, .95)
            for k in range(4):
                hump(a + (b - a) * (k + .5) / 4, .95, (b - a) / 4 * .9, .07, 3)


def roller_shutter(B, fr, a0, a1, bottom):
    """A roller shutter half down (6:47, closing up): its box under the fascia, guides, slats and bottom rail."""
    g, T = B.g, B.T
    fbox(g, T, fr, a0 - .04, a1 + .04, 3.08, 3.4, 0.0, .16, F['shutter'], 0, skip=('-z',))
    for x in (a0, a1 - .05):
        fbox(g, T, fr, x, x + .05, 0.0, 3.08, 0.0, .07, F['iron-black'], .006, skip=('-z', '-y'))
    slat = .125 if T.lod == 0 else 3.0
    y = 3.08
    while y > bottom + 1e-3:
        y0 = max(bottom, y - slat)
        ym = (y0 + y) / 2
        if T.lod == 0:
            g.face((fr.P(a0, y0, .03), fr.P(a1, y0, .03), fr.P(a1, ym, .05), fr.P(a0, ym, .05)), F['shutter'], fr.D(0, -.5, 1))
            g.face((fr.P(a0, ym, .05), fr.P(a1, ym, .05), fr.P(a1, y, .03), fr.P(a0, y, .03)), F['shutter'], fr.D(0, .5, 1))
        else:
            fquad(g, fr, a0, y0, a1, y, .04, F['shutter'])
        y = y0
    fbox(g, T, fr, a0, a1, bottom - .06, bottom, .02, .08, F['iron-black'], 0, skip=('-z',))


def upper_window_bay(B, fr, st, r, top=False):
    """An upper bay (bible 2.3): a 1.1 x 1.9 m two-over-two sash, sill .75 m, head 2.65 m, an 18 cm reveal, a sill 12 cm
    proud with its drip, the seeded head."""
    g, T = B.g, B.T
    rise = .16 if st.head == 'arch' else 0.0
    h = Hole(.7, 1.8, .75, 2.65 - rise, rise, .18)
    wall(g, T, fr, 0.0, BAY, 0.0, STOREY, st.wall, [h], base=fr.o[1])
    reveal(g, T, fr, h, st.wall)
    sill(g, T, fr, h.u0, h.u1, h.v0, st.dress)
    B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.3, .32)
    window_head(B, fr, h, st)
    st.window(B, fr, h, r)


def oriel(B, fr, st, r, storeys=1):
    """The canted oriel (bible 5.1): 0.6 m deep on two stone corbels from 4.0 m (fr's foot) to under the eaves; a sash in
    each face; a cornice and a hipped lead cap. Overhead art: it starts above 3.1 m."""
    g, T = B.g, B.T
    H = storeys * STOREY
    top = H - .82                        # the cornice and lead cap end under the eaves' soffit
    plan = [(.2, 0.0), (.55, .6), (1.95, .6), (2.3, 0.0)]
    wall(g, T, fr, 0.0, BAY, 0.0, H, st.wall, [Hole(.2, 2.3, 0.0, H)], base=fr.o[1])
    faces = []
    for i in range(3):
        (a0, c0), (a1, c1) = plan[i], plan[i + 1]
        d = (a1 - a0, c1 - c0)
        w = math.hypot(*d)
        faces.append((Frame(fr.P(a0, 0.0, c0), fr.D(d[0], 0.0, d[1])), w))
    for k in range(storeys):
        v0 = k * STOREY
        for i, (ff, w) in enumerate(faces):
            front = i == 1
            vh = min(v0 + 2.0, top - .14)
            h = Hole(.2, w - .2, v0 + .45, vh, 0.0, .1) if front else Hole(.11, w - .11, v0 + .45, vh, 0.0, .1)
            vt = min(top, v0 + STOREY)
            wall(g, T, ff, 0.0, w, v0, vt, st.wall, [h], base=ff.o[1])
            reveal(g, T, ff, h, st.wall)
            smould(g, T, ff, h.u0 - .04, h.u1 + .04, h.v0, [(0.0, -.07), (.07, -.07), (.08, -.02), (0.0, .01)], st.dress)
            st.window(B, ff, h, r, bars=front)
            B.streaks.add_under(ff, h.u0, h.u1, h.v0 - .07, .8, .25)
            if k < storeys - 1:
                smould(g, T, ff, 0.0, w, v0 + STOREY - .1, ck.P_STRING_SMALL, st.dress)
    # corner posts
    if T.detail:
        for i in (1, 2):
            a, c = plan[i]
            fbox(g, T, fr, a - .05, a + .05, 0.0, top, c - .05, c + .05, st.dress, .01, skip=('-y', '+y'))
    pts = [(fr.P(a, 0.0, c)[0], fr.P(a, 0.0, c)[2]) for a, c in plan]
    outs = []
    for (ff, w) in faces:
        outs.append((ff.n[0], ff.n[2]))
    # the floor slab on its corbels and the base moulding
    fh = Frame(fr.P(0.0, -.22, 0.0), fr.u, fr.mn)
    prism(g, fh, [(a, -c) for a, c in plan], 0.0, .22, st.dress)
    plan_mould(g, T, pts, fr.o[1] - .22, mould_lod(T, [(0.0, 0.0), (.06, .0), (.06, .1), (.03, .14), (0.0, .16)]), st.dress, outs)
    for x in (.66, 1.84):
        console(g, T, fr, x, -.22, .16, .52, .56, st.dress)
    # the cornice and the hipped lead cap
    plan_mould(g, T, pts, fr.o[1] + top, mould_lod(T, [(0.0, 0.0), (.05, 0.0), (.05, .05), (.1, .09), (.1, .15), (0.0, .17)]),
               st.dress, outs)
    y1, y2 = top + .17, top + .37
    lo = [(.12, 0.0), (.5, .7), (2.0, .7), (2.38, 0.0)]
    P = lambda a, b, c: fr.P(a, b, c)
    g.face((P(lo[0][0], y1, lo[0][1]), P(lo[1][0], y1, lo[1][1]), P(.55, y2, 0.0)), F['lead'], fr.D(-.6, .5, .35))
    g.face((P(lo[1][0], y1, lo[1][1]), P(lo[2][0], y1, lo[2][1]), P(1.95, y2, 0.0), P(.55, y2, 0.0)), F['lead'], fr.D(0, .7, .45))
    g.face((P(lo[2][0], y1, lo[2][1]), P(lo[3][0], y1, lo[3][1]), P(1.95, y2, 0.0)), F['lead'], fr.D(.6, .5, .35))
    if T.detail:
        for (a, c) in ((.5, .7), (2.0, .7)):
            sk.cylinder(g, P(a, y1, c), P(.55 if a < 1 else 1.95, y2, 0.0), .025, F['lead'], 4)


# ---------------------------------------------------------------------------------------------------------------------------
# The Rows: roofs and ends
# ---------------------------------------------------------------------------------------------------------------------------
PITCH = math.radians(40.0)


def roof_geom(He, D):
    t = math.tan(PITCH)
    v0 = He + .12                    # the slates' top over the wall face
    return t, v0, v0 + t * D / 2     # tan, at the face, at the ridge


def slope(B, fr, W, He, D, r, damage=True):
    """One slope of lapped slate from the eaves (fr: the eaves' wall face, u 0..W) to the ridge D/2 in: courses of .25 m,
    each lifted 2 cm at its tail, so the roof reads in lines (bible 4.2: modelled courses at LOD0); a few slipped slates."""
    g, T = B.g, B.T
    t, v0, vr = roof_geom(He, D)
    ce = .32
    ve = v0 - t * ce
    ct, st_ = math.cos(PITCH), math.sin(PITCH)
    S = (D / 2 + ce) / ct
    up = fr.D(0, st_, -ct)
    nrm = fr.D(0, ct, st_)
    down = fr.D(0, -st_, ct)

    def p(s, u, lift=0.0):
        b = fr.P(u, ve + s * st_, ce - s * ct)
        return (b[0] + nrm[0] * lift, b[1] + nrm[1] * lift, b[2] + nrm[2] * lift)
    # the long night's damage (bible 5.1: slipped slates), from the slope's own place so LOD1 keeps the same hole
    rh = rng('slope', *[round(x, 1) for x in fr.o])
    holes = []
    if damage and rh.random() < .75:
        hs = rh.uniform(1.0, S - 1.6)
        hu = rh.uniform(.4, W - 1.3)
        holes.append((hs, hs + rh.choice((.5, .75, 1.0)), hu, hu + rh.uniform(.55, .95)))
    if T.lod > 0:
        g.face((p(0, 0), p(0, W), p(S, W), p(S, 0)), F['slate'], nrm)
        for (s0, s1, u0, u1) in holes:
            g.face((p(s0, u0, .03), p(s0, u1, .03), p(s1, u1, .03), p(s1, u0, .03)), F['batten'], nrm)
        return
    gauge = .25
    n = int(math.ceil(S / gauge))
    cells = max(1, int(round(W / 2.5)))
    for i in range(n):
        s0, s1 = S * i / n, S * (i + 1) / n
        for c in range(cells):
            u0, u1 = W * c / cells, W * (c + 1) / cells
            fs = F['slate'] if (i + c) % 7 else F['slate-dark']
            g.face((p(s0, u0, .022), p(s0, u1, .022), p(s1, u1), p(s1, u0)), fs, nrm)
            g.face((p(s0, u0), p(s0, u1), p(s0, u1, .022), p(s0, u0, .022)), fs, down)
    if not damage:
        return
    for (s0, s1, u0, u1) in holes:
        # slates gone: the dark felt and the battens across it, the broken edge's slates lifted
        g.face((p(s0, u0, .03), p(s0, u1, .03), p(s1, u1, .03), p(s1, u0, .03)), F['batten'], nrm)
        b = s0 + .12
        while b < s1 - .05:
            g.face((p(b, u0, .036), p(b, u1, .036), p(b + .045, u1, .036), p(b + .045, u0, .036)), F['timber-old'], nrm)
            b += gauge
        for k in range(3):
            # the slates round the hole, tilted loose
            uu = u0 + (u1 - u0) * (k + .5) / 3
            a = r.uniform(-.35, .35)
            w, h = .28, .45
            cx, sx = math.cos(a), math.sin(a)
            corners = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
            pts = [p(s0 - .05 + y * cx + x * sx, uu + x * cx - y * sx, .045 + .03 * (y > 0)) for x, y in corners]
            g.face(pts, F['slate-dark'] if k % 2 else F['slate'], nrm)
    # a run of slipped slates down one line of the slope, and one fallen into the gutter
    sl_u = r.uniform(.4, W - .6)
    s_top = r.uniform(1.4, S - .8)
    for k in range(r.randint(2, 4)):
        s = s_top - k * .34
        a = r.uniform(-.3, .3)
        w, h = .3, .5
        cx, sx = math.cos(a), math.sin(a)
        corners = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
        pts = [p(s + y * cx + x * sx, sl_u + r.uniform(-.05, .05) + x * cx - y * sx, .04 + .025 * (y > 0)) for x, y in corners]
        g.face(pts, F['slate-dark'], nrm)
    if r.random() < .5:
        gu = r.uniform(.5, W - .5)
        q = [fr.P(gu - .14, He - .2, .37), fr.P(gu + .16, He - .17, .38), fr.P(gu + .13, He - .05, .5), fr.P(gu - .15, He - .08, .49)]
        g.face(q, F['slate-dark'], fr.D(0, .6, .8))
        g.face(q[::-1], F['slate-dark'], fr.D(0, -.6, -.8))


def eaves(B, fr, W, He):
    """The eaves on a wall face (fr): soffit, fascia board and the cast-iron ogee gutter."""
    g, T = B.g, B.T
    if T.lod > 0:
        fquad(g, fr, 0.0, He - .47, W, He - .17, .34, F['paint-white'])
        return
    fquad(g, Frame(fr.P(0.0, He - .45, 0.0), fr.u, fr.mn), 0.0, -.3, W, 0.0, 0.0, F['paint-white'], fr.mv)
    fbox(g, T, fr, 0.0, W, He - .47, He - .17, .3, .34, F['paint-white'], .008, skip=('-z',))
    if T.lod == 0:
        gutter(g, T, fr.shifted(0, 0, .29), 0.0, W, He - .17, .13, F['iron-black'])


def ridge(B, fr, W, He, D, f=None):
    g, T = B.g, B.T
    t, v0, vr = roof_geom(He, D)
    n = T.small if T.lod == 0 else 2
    sec = [(.12 * math.cos(math.pi * i / n), .12 * math.sin(math.pi * i / n) - .02) for i in range(n + 1)]
    ck.sweep(g, sec, [fr.P(0.0, vr, -D / 2), fr.P(W, vr, -D / 2)], f or F['terracotta'], up=fr.v, caps=True)


def upstand(B, fr, u, He, D, f, coping=True, kneelers=True, wide=.45, outer_cap=True):
    """A party-wall upstand (or a gable's parapet) on the line u: brick rising .3 m over both slopes, a stone coping, and
    kneelers at the eaves. outer_cap False: its end on the -u side is left to the gable's own wall (pass 2)."""
    g, T = B.g, B.T
    t, v0, vr = roof_geom(He, D)
    hw = wide / 2
    side = Frame(fr.P(u - hw, 0.0, 0.0), fr.mn, fr.v)   # a = -c (into the house), b = v, depth along +u
    over = .3
    pts = [(-.05, He - .3), (-.05, v0 + t * -.05 + over), (D / 2, vr + over), (D + .05, v0 + t * -.05 + over), (D + .05, He - .3)]
    prism(g, side, pts, 0.0, wide, f, caps=(outer_cap, True))
    if coping:
        cw = .06
        sideC = Frame(fr.P(u - hw - cw, 0.0, 0.0), fr.mn, fr.v)
        lo = lambda a: v0 + t * min(a, D - a) + over
        for (aa, bb) in ((-.1, D / 2), (D / 2, D + .1)):
            prism(g, sideC, [(aa, lo(aa) - .02), (bb, lo(bb) - .02), (bb, lo(bb) + .1), (aa, lo(aa) + .1)], 0.0, wide + 2 * cw,
                  F['stone-dress'])
    if kneelers and T.lod == 0:
        for a in (-.12, D - .3):
            fbox(g, T, Frame(fr.P(u, 0.0, 0.0), fr.u, fr.v), -hw - .05, hw + .05, He - .32, He + .1, -a - .42, -a, F['stone-dress'], .015)


def stack(B, fr, u, He, D, r, pots=4, f=None, along_u=True):
    """A chimney stack astride the party wall at the ridge (bible 5.1: four pots), with two oversailing courses and its
    flaunching; brick, the pots clay."""
    g, T = B.g, B.T
    t, v0, vr = roof_geom(He, D)
    f = f or F['brick-stock']
    hu, hc = (.7, .32) if along_u else (.32, .7)
    cc = -D / 2
    top = vr + 1.25
    fbox(g, T, fr, u - hu, u + hu, vr - 1.2, top, cc - hc, cc + hc, f, .02, skip=('-y',))
    fbox(g, T, fr, u - hu - .06, u + hu + .06, top, top + .1, cc - hc - .06, cc + hc + .06, f, .012)
    if T.lod == 0:
        fbox(g, T, fr, u - hu - .03, u + hu + .03, top + .1, top + .2, cc - hc - .03, cc + hc + .03, f, .012)
        # flaunching
        fbox(g, T, fr, u - hu + .02, u + hu - .02, top + .2, top + .27, cc - hc + .02, cc + hc - .02, F['concrete'], .03)
    for i in range(pots):
        x = (i + .5) / pots * 2 - 1
        pu = u + x * (hu - .18) if along_u else u
        pc = cc if along_u else cc + x * (hc - .18)
        hgt = r.uniform(.45, .7)
        chimney_pot(g, T, fr.P(pu, top + (.27 if T.lod == 0 else .1), pc), F['terracotta'], hgt, .11)


def roof(B, fr, W, He, D, r, up_left=True, up_right=False, stack_left=True, wall_f=None):
    """A house's roof (bible 5.1): two slopes of lapped slate at 40 deg, eaves front and back, ridge tiles, the party-wall
    upstand at the module's left with its chimney stack."""
    slope(B, fr, W, He, D, r)
    back = Frame(fr.P(W, 0.0, -D), fr.mu)
    slope(B, back, W, He, D, r, damage=False)
    eaves(B, fr, W, He)
    eaves(B, back, W, He)
    ridge(B, fr, W, He, D)
    wf = wall_f or F['brick-stock']
    if up_left:
        upstand(B, fr, 0.0, He, D, wf)
    if up_right:
        upstand(B, fr, W, He, D, wf)
    if stack_left:
        stack(B, fr, 0.0, He, D, r)


GHOST_SIGNS = (
    (('MERIDIAN', .075), ('BISCUIT CO.', .06)),
    (('VALE COLD', .07), ('STORE', .085)),
    (('DRINK', .06), ('VALE ALES', .075)),
    (('WMRD 64.7', .075), ('THE TIME SIGNAL', .05)),
    (('RIALTO', .085), ('PICTURES', .06)),
    (('ASHGROVE', .065), ('TEAS', .085)),
    (('M & V R', .08), ('EXCURSIONS', .055)),
)
GHOST_GROUNDS = ('ghost-ground', 'ghost-blue', 'ghost-green', 'ghost-ground')


def fit_lines(lines, width):
    """Scale a sign's lines of lettering down to fit `width` (never below .05 m a cell: .35 m letters, bible 2.4)."""
    out = []
    for s_, px in lines:
        w = text_width(s_, px)
        out.append((s_, max(.05, px * min(1.0, width / w))))
    return out


def rake_wall(B, fr, u0, u1, v0, vtop_at, f, c=0.0, base=0.0):
    """The wall of a gable's triangle (or any face under a sloping edge) from v0 up to vtop_at(u) between u0 and u1: strips
    of the tier's cell height, each cut into cells across, so the bake has corners to colour (pass 1 drew one polygon)."""
    g, T = B.g, B.T
    ymax = max(vtop_at(u0 + (u1 - u0) * i / 16) for i in range(17))
    y = v0
    while y < ymax - 1e-4:
        cs = T.cell_at(base + y)
        y1 = min(ymax, y + cs)
        n = max(2, int(math.ceil((u1 - u0) / cs)))
        for i in range(n):
            a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
            ta, tb = vtop_at(a), vtop_at(b)
            if ta <= y + 1e-4 and tb <= y + 1e-4:
                continue
            # the cell [a, b] x [y, y1] clipped under the top line (straight across the cell)
            poly = [(a, y), (b, y), (b, y1), (a, y1)]
            out = []
            for k in range(4):
                p, q = poly[k], poly[(k + 1) % 4]
                fp = vtop_at(p[0]) - p[1]
                fq = vtop_at(q[0]) - q[1]
                if fp >= 0:
                    out.append(p)
                if (fp >= 0) != (fq >= 0):
                    tt = fp / (fp - fq)
                    x = p[0] + (q[0] - p[0]) * tt
                    out.append((x, vtop_at(x) if abs(q[0] - p[0]) > 1e-9 else p[1] + (q[1] - p[1]) * tt))
            clean = []
            for pt in out:
                if not clean or abs(pt[0] - clean[-1][0]) + abs(pt[1] - clean[-1][1]) > 1e-5:
                    clean.append(pt)
            if len(clean) > 1 and abs(clean[0][0] - clean[-1][0]) + abs(clean[0][1] - clean[-1][1]) < 1e-5:
                clean.pop()
            if len(clean) >= 3:
                g.face([fr.P(x, yy, c) for x, yy in clean], f, fr.n)
        y = y1


BREAST = .19     # a gable's chimney breast: with its plinth and cap .25 m proud at most below 3.1 m (bible 2.3)


def gable(B, fr, He, D, st, r, name=None, front_u=None, storeys=None, kind=None):
    """A run's end (bible 5.1), pass 2: the gable wall at u = 0 of `fr` facing -u, broken up so no flat face reads as a box
    (bible 2.6 rule 2), in one of two seeded variants:
    - 'breast': a chimney breast up the middle (.19 m proud: with its plinth, inside the .25 m rule) into the end stack,
      real sashes on the front half, blind windows of later brick on the back half, an oculus in the gable;
    - 'ghost': a faded painted sign over the top storey and into the gable, windows below it;
    both with string courses at the floor lines, tie-rod pattress plates, a downpipe at the back corner, the parapet coping
    and kneelers, rain streaks under the coping, kneelers, string courses and sills; and the street-name plate."""
    g, T = B.g, B.T
    t, v0, vr = roof_geom(He, D)
    storeys = storeys or (2 if He < 8 else 3)
    gf = Frame(fr.P(0.0, 0.0, -D), fr.n)       # u along the house's depth (back to front), n = -u of fr
    # a terrace's flank is mostly left in brick even where the front is rendered
    gr0 = rng('gablekind', st.seed)
    brick_flank = st.brick or gr0.random() < .75
    wf = F['brick-stock'] if brick_flank else st.wall
    front_u = D if front_u is None else front_u
    kind = kind or (gr0.choice(('breast', 'ghost', 'ghost')) if brick_flank else 'breast')
    if kind == 'ghost' and not brick_flank:
        brick_flank, wf = True, F['brick-stock']
    gr = rng('gable', st.seed, kind)
    over = .3
    # the rake: the top of the gable wall, just under the parapet's coping (the upstand's outline is v0 + over + t a)
    rake = lambda a: v0 + over + t * min(max(a, 0.0), max(D - a, 0.0)) - .01
    bw = .8                                    # the breast's half width
    um = D / 2
    fwd = front_u > 0                          # the front is at u = D (else at u = 0)
    uf = (lambda a: D - a) if fwd else (lambda a: a)     # a distance from the front -> u
    holes, blind = [], []
    sign = None
    if kind == 'ghost':
        sv0 = storey_floor(storeys - 1) + .4
        sign = (1.2, D - 1.2, sv0, rake(1.2) - .35)
    for k in range(storeys):
        y = storey_floor(k)
        if sign and y + .75 >= sign[2] - .2:
            continue
        hv0, hv1 = (y + 1.0, y + 2.9) if k == 0 else (y + .75, y + 2.6)
        rise = .16 if st.head == 'arch' else 0.0
        a0, a1 = sorted((uf(1.15), uf(2.35)))
        holes.append(Hole(a0, a1, hv0, hv1 - rise, rise, .18))
        if kind == 'breast' or k == 0:
            b0, b1 = sorted((uf(D - 2.35), uf(D - 1.15)))
            blind.append(Hole(b0, b1, hv0, hv1 - rise, rise, .06, keep=False))
    keep_out = [Hole(um - bw, um + bw, 0.0, He + 5.0, keep=True)] if kind == 'breast' else []
    plinth_run(B, gf, 0.0, D, st.plinth, keep_out)
    wall(g, T, gf, 0.0, D, PLINTH, He - .3, wf, holes + blind + keep_out, base=gf.o[1])
    # the gable's triangle, under the parapet's coping (pass 1: one polygon, the upstand's end)
    if not keep_out:
        rake_wall(B, gf, 0.0, D, He - .3, rake, wf, base=gf.o[1])
    else:
        rake_wall(B, gf, 0.0, um - bw, He - .3, rake, wf, base=gf.o[1])
        rake_wall(B, gf, um + bw, D, He - .3, rake, wf, base=gf.o[1])
    for h in holes:
        reveal(g, T, gf, h, wf)
        sill(g, T, gf, h.u0, h.u1, h.v0, st.dress)
        window_head(B, gf, h, st)
        st.window(B, gf, h, r, ground=h.v0 < GROUND)
        B.streaks.add_under(gf, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.3, .3)
    for h in blind:
        # a blind window: the opening bricked up 6 cm back in a later, darker brick, its sill and head kept
        if cut(T, h):
            reveal(g, T, gf, h, wf)
            back_fill(g, T, gf, h, -h.depth, F['brick-infill'])
            sill(g, T, gf, h.u0, h.u1, h.v0, st.dress)
            window_head(B, gf, h, st)
            B.streaks.add_under(gf, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.0, .25)
    # string courses at the floor lines, continuing the front's
    for k in range(1, storeys):
        y = storey_floor(k) - .08
        if kind == 'breast':
            smould(g, T, gf, 0.0, um - bw, y, ck.P_STRING, st.dress)
            smould(g, T, gf, um + bw, D, y, ck.P_STRING, st.dress)
        else:
            smould(g, T, gf, 0.0, D, y, ck.P_STRING, st.dress)
        B.streaks.add_under(gf, .2, D - .2, y, .7, .12)
    if kind == 'breast':
        # the breast: a pentagon to the parapet's line, BREAST proud, its own plinth and string courses; the end stack on
        # its head
        top_c = rake(um) + .1
        poly = [(um - bw, 0.0), (um + bw, 0.0), (um + bw, rake(um + bw) + .05), (um, top_c), (um - bw, rake(um - bw) + .05)]
        prism(g, gf, poly, 0.0, BREAST, wf, caps=(False, True))
        bf = gf.shifted(0.0, 0.0, BREAST)
        plinth_run(B, bf, um - bw, um + bw, st.plinth)
        for k in range(1, storeys):
            smould(g, T, bf, um - bw, um + bw, storey_floor(k) - .08, ck.P_STRING, st.dress)
        B.streaks.add_under(bf, um - bw + .1, um + bw - .1, rake(um + bw), 2.4, .3)
        if T.detail and rake(uf(1.75)) > He + 1.6:
            # an oculus in the gable on the front side of the breast: a stone ring round a dark (or lit) disc
            oc = gf.P(uf(1.75), He + .85, 0.0)
            sk.lathe(g, [(.42, 0.0), (.42, .05), (.3, .08), (.3, -.1)], oc, st.dress, T.round, axis=gf.n)
            sk.lathe(g, [(.3, -.1), (0.0, -.1)], oc, F['glass-dark'] if gr.random() < .7 else F['lit-tungsten'], T.round,
                     axis=gf.n)
    if sign is not None:
        su0, su1, sv0, sv1 = sign
        lines = fit_lines(GHOST_SIGNS[gr.randrange(len(GHOST_SIGNS))], su1 - su0 - .6)
        ck.ghost_sign(g, T, gf, su0, su1, sv0, sv1, lines, GHOST_GROUNDS[gr.randrange(4)],
                      'ghost-gold' if gr.random() < .3 else 'ghost-cream', gr, streaks=B.streaks)
    upstand(B, fr, .225, He, D, wf, wide=.45, outer_cap=False)
    # the end stack: on the breast's head, or on the ridge astride the gable
    sb = Frame(fr.P(0.0, 0.0, 0.0), fr.u, fr.v)
    stack(B, sb, .3 - BREAST if kind == 'breast' else .3, He, D, r, pots=3, f=wf, along_u=False)
    if not brick_flank and T.detail:
        quoins(g, T, gf, front_u, PLINTH, He - .3, st.dress, into=-1 if front_u > 0 else 1)
    # tie-rod pattress plates at the floor lines, clear of the windows
    if T.detail:
        pk = 'disc' if gr.random() < .6 else 's'
        for k in range(1, storeys):
            for a in (uf(.6), uf(D - .6)):
                ck.pattress(g, T, gf, a, storey_floor(k) + .3, kind=pk)
    # rain: the coping's drips down the rake, long stains under the kneelers
    for a in [x * .9 for x in range(1, int(D / .9))]:
        if abs(a - um) > bw + .1 or kind != 'breast':
            B.streaks.add_under(gf, a - .35, a + .35, rake(a) - .05, 1.6, .22)
    for a in (.25, D - .25):
        B.streaks.add_under(gf, a - .25, a + .25, He - .32, 3.2, .4)
    # a downpipe at the back corner, from the back gutter
    downpipe(g, T, gf, uf(D - .18), 0.0, He - .25)
    # the street-name plate at 3.4 m near the front corner
    if name and T.lod == 0:
        pw = text_width(name, .03) + .24
        a = front_u - .5 - pw if front_u > 0 else .5
        fbox(g, T, gf, a, a + pw, 3.3, 3.62, 0.0, .03, F['enamel-green'], .008, skip=('-z',))
        text(g, T, gf, a + pw / 2, 3.36, .03, name, F['enamel-cream'], c=.03, depth=.004, raised=False)


def party_wall(B, fr, u, He, D, f, facing=-1):
    """An exposed party wall (a lone house shown by itself): brick up the roof's profile, at u, facing -u (or +u)."""
    g, T = B.g, B.T
    t, v0, vr = roof_geom(He, D)
    if facing < 0:
        pf = Frame(fr.P(u, 0.0, -D), fr.n)
    else:
        pf = Frame(fr.P(u, 0.0, 0.0), fr.mn)
    wall(g, T, pf, 0.0, D, 0.0, He, f, base=pf.o[1])
    g.face((pf.P(0.0, He), pf.P(D, He), pf.P(D / 2, vr - .1)), f, pf.n)


def back_wall(B, fr, st, r, W, storeys, D):
    """The rear elevation: a back door and plain one-over-one sashes, a downpipe; leaner than the front."""
    g, T = B.g, B.T
    bf = Frame(fr.P(W, 0.0, -D), fr.mu)
    He = storey_floor(storeys)
    wf = F['brick-stock'] if (st.brick or r.random() < .5) else st.wall
    holes = [Hole(.6, 1.5, 0.0, 2.2, 0.0, .12), Hole(2.9, 4.1, 1.0, 2.6, 0.0, .15)]
    for k in range(1, storeys):
        y = storey_floor(k)
        holes.append(Hole(.7, 1.6, y + .8, y + 2.4, 0.0, .15))
        holes.append(Hole(3.0, 4.0, y + .8, y + 2.4, 0.0, .15))
    plinth_run(B, bf, 0.0, W, F['brick-blue'], [holes[0]])
    wall(g, T, bf, 0.0, W, PLINTH, He, wf, holes, base=bf.o[1])
    for h in holes:
        reveal(g, T, bf, h, wf, sill=h.v0 > 0)
        if h.v0 > 0:
            sill(g, T, bf, h.u0, h.u1, h.v0, F['stone-dress'])
            st.window(B, bf, h, r, frame=F['paint-white'], bars=False, simple=True)
            B.streaks.add_under(bf, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.1, .3)
        else:
            panel_door(g, T, bf, h.u0, h.u1, 0.0, h.v1, -h.depth + .02, F['paint-green'], r, panels=4, knocker=False)
    downpipe(g, T, bf, 4.75, 0.0, He - .25)


# ---------------------------------------------------------------------------------------------------------------------------
# The Rows: a house and a run
# ---------------------------------------------------------------------------------------------------------------------------
HOUSE_D = 8.0


def house(B, fr, st, r, storeys=2, door_bay=0, shop=False, up_right=False, ends=False, back=True, shop_sign=None,
          up_left=True, stack_left=True):
    """A 5 m terraced house (bible 5.1) in fr (u 0..5): the door bay and the oriel bay (or a shopfront), the upper storeys,
    the roof with its left upstand and stack, the back elevation; ends: exposed party walls (a house shown alone)."""
    W, D = 2 * BAY, HOUSE_D
    He = storey_floor(storeys)
    du = door_bay * BAY
    wu = (1 - door_bay) * BAY
    ground_door_bay(B, fr.shifted(du), st, r, lantern_side=1 if door_bay == 0 else -1)
    if shop:
        rs = rng('shopbay', st.seed)
        ground_shop_bay(B, fr.shifted(wu), st, r, lit=rs.random() < .8, shutter=rs.random() < .3, trade=shop_sign)
        if shop_sign and B.T.lod == 0:
            fascia_text(B, fr, wu + CONSOLE_CLEAR, wu + BAY - CONSOLE_CLEAR, shop_sign)
    else:
        ground_window_bay(B, fr.shifted(wu), st, r)
    for k in range(1, storeys):
        upper_window_bay(B, fr.shifted(du, storey_floor(k)), st, r)
    if shop:
        for k in range(1, storeys):
            upper_window_bay(B, fr.shifted(wu, storey_floor(k)), st, r)
    else:
        oriel(B, fr.shifted(wu, GROUND), st, r, storeys - 1)
    roof(B, fr, W, He, D, r, up_left=up_left, up_right=up_right, stack_left=stack_left)
    if door_bay == 0:
        downpipe(B.g, B.T, fr, .14, 0.0, He - .3)
    if back:
        back_wall(B, fr, st, r, W, storeys, D)
    if ends:
        party_wall(B, fr, 0.0, He, D, F['brick-stock'], -1)
        party_wall(B, fr, W, He, D, F['brick-stock'], 1)
    B.streaks.add_under(fr, 0.0, W, He - .4, 1.6, .2)


def row(B, seed, n=4, storeys=2, name='ALBION ROW', shops=(), x0=0.0):
    """A run of n houses (bible 5.1: runs of 4-10, ending in a gable), seeded so no two neighbours match."""
    fr = Frame((x0, 0.0, 0.0), (1.0, 0.0, 0.0))
    for i in range(n):
        st = Style(seed * 31 + i)
        r = rng('house', seed, i)
        # (pass 2: the end houses leave their outer upstand and stack to the gable's own; pass 1 drew both, so a second
        # upstand stood .225 m proud of the gable's face and a second stack clashed with its end stack)
        house(B, fr.shifted(i * 2 * BAY), st, r, storeys, door_bay=(i + seed) % 2, shop=i in shops,
              up_right=False, up_left=i > 0, stack_left=i > 0,
              shop_sign=('BAKERY', 'NEWS', 'TOOLS', 'TOBACCO')[i % 4] if i in shops else None)
    st0 = Style(seed * 31)
    gable(B, fr, storey_floor(storeys), HOUSE_D, st0, rng('gable', seed), name)
    # the run's other end: a gable facing +u
    stn = Style(seed * 31 + n - 1)
    end = Frame(fr.P(n * 2 * BAY, 0.0, -HOUSE_D), fr.mu)
    gable(B, end, storey_floor(storeys), HOUSE_D, stn, rng('gable2', seed), None, front_u=0.0)


# ---------------------------------------------------------------------------------------------------------------------------
# The Junctions: the corner shop (bible 5.2)
# ---------------------------------------------------------------------------------------------------------------------------
CH = 2.5 * SQ2   # the chamfer's run along each street: 1.768 m


def dressed_window(B, fr, h, st, r, hood=False, f_dress=None):
    """A window with a moulded stone architrave (and, on the first floor, a cornice hood on consoles)."""
    g, T = B.g, B.T
    fd = f_dress or F['stone-dress']
    reveal(g, T, fr, h, fd)
    sill(g, T, fr, h.u0 - .08, h.u1 + .08, h.v0, fd)
    B.streaks.add_under(fr, h.u0 - .16, h.u1 + .16, h.v0 - .1, 1.3, .3)
    if T.detail:
        frame_ring(g, fr, h.u0 - .14, h.u1 + .14, h.v0, h.top_v + .14, .14, 0.0, .04, fd, inner=False, sides=(1, 1, 0, 1))
        for (a, nrm) in ((h.u0 - .14, fr.mu), (h.u1 + .14, fr.u)):       # the architrave's outer edges (pass 1 drew one inner)
            g.face((fr.P(a, h.v0, .04), fr.P(a, h.top_v + .14, .04), fr.P(a, h.top_v + .14, 0.0), fr.P(a, h.v0, 0.0)), fd, nrm)
    if hood:
        y = h.top_v + .14
        mould(g, T, fr, h.u0 - .3, h.u1 + .3, y, mould_lod(T, ck.P_HOOD), fd)
        if T.detail:
            for x in (h.u0 - .2, h.u1 + .2):
                console(g, T, fr, x, y, .12, .32, .14, fd)
    st.window(B, fr, h, r)


def octagon(cx, cz, R, phase):
    return [(cx + R * math.cos(phase + k * math.pi / 4), cz + R * math.sin(phase + k * math.pi / 4)) for k in range(8)]


def corner_shop(B, seed, storeys=3):
    """The corner shop (bible 5.2): three storeys on the 2.3 grid (ground + two at 3 m: a 10 m cornice; the parapet to
    11.4 m), faces of three bays on two streets, the corner cut as a 2.5 m chamfer; the shopfront wraps round it with the
    corner door; an octagonal corner turret from 4.0 m on its corbel, up through the cornice into a drum under a lead dome
    (the junction's mark on the skyline); a parapet with the name panel; awning, bracket sign, a shutter half down, posters,
    and the sodium swan-neck lamp at 4.4 m."""
    g, T = B.g, B.T
    r = rng('corner', seed)
    st = Style(seed, brick=True)
    st.wall = F['brick-red']
    st.head = 'lintel'
    st.lit = .22
    fd = F['stone-dress']
    paint = F['paint-green']
    L = CH + 3 * BAY                   # each street face's run from the corner: 9.268 m
    H = storey_floor(storeys)          # 10.0
    frA = Frame((L, 0.0, 0.0), (-1.0, 0.0, 0.0))          # street A, facing -z; u 0..7.5 toward the corner
    frB = Frame((0.0, 0.0, CH), (0.0, 0.0, 1.0))          # street B, facing -x; u 0..7.5 away from the corner
    frC = Frame((CH, 0.0, 0.0), (-SQ2, 0.0, SQ2))         # the chamfer, u 0..2.5
    # --- ground floor ------------------------------------------------------------------------------------------------
    ground_door_bay(B, frA, st, r, lantern_side=-1)
    # (the flats' door bay has a string course; the shop's fascia runs from bay 1 round to street B's bay 1)
    ground_shop_bay(B, frA.shifted(BAY), st, r, lit=True, pil=(True, False), paint=paint, trade='GROCER')
    ground_shop_bay(B, frA.shifted(2 * BAY), st, r, lit=True, pil=(True, True), paint=paint, trade='GROCER')
    ground_shop_bay(B, frB, st, r, lit=True, pil=(True, True), paint=paint, trade='NEWS')
    ground_shop_bay(B, frB.shifted(BAY), st, r, lit=False, shutter=True, pil=(False, True), paint=paint)
    # street B's last bay: a blind bay with the posters of the long night pasted in layers
    blind = frB.shifted(2 * BAY)
    plinth_run(B, blind, 0.0, BAY, F['brick-blue'])
    wall(g, T, blind, 0.0, BAY, PLINTH, 3.92, st.wall, base=0.0)
    smould(g, T, blind, 0.0, BAY, 3.92, ck.P_STRING, fd)
    if T.detail:
        posters(B, blind, r)
    # the corner door in the chamfer, in its lobby (a recess of .25 m)
    h = Hole(.55, 1.95, 0.0, 2.75, 0.0, .25)
    wall(g, T, frC, 0.0, BAY, 0.0, 3.4, paint, [h], c=.0, base=0.0)
    reveal(g, T, frC, h, paint, sill=False)
    if T.lod > 0:
        fquad(g, frC, h.u0, 0.0, h.u1, 2.75, .02, F['lit-shop'])
    if T.lod == 0:
        frame_ring(g, frC, h.u0, h.u1, 0.0, 2.75, .07, -.25, -.18, paint, sides=(1, 1, 0, 1))
        fquad(g, frC, h.u0 + .07, 0.0, h.u1 - .07, .9, -.22, paint)
        fquad(g, frC, h.u0 + .07, .9, h.u1 - .07, 2.2, -.22, F['lit-shop'])
        bar(g, T, frC, h.u0 + .07, h.u1 - .07, 2.2, 2.3, -.22, -.18, paint)
        fquad(g, frC, h.u0 + .07, 2.3, h.u1 - .07, 2.68, -.22, F['lit-shop'])
    if T.detail:
        sk.lathe(g, [(.03, 0.0), (.03, .02), (0.0, .05)], frC.P(1.75, 1.05, -.22), F['brass'], T.small, axis=frC.n)
        text(g, T, frC, 1.25, 1.62, .022, 'CLOSED', F['poster-c'], c=-.214, raised=False)
    fbox(g, T, frC, 0.0, BAY, 2.75, 3.4, 0.0, .05, paint, .01, skip=('-z',))
    fbox(g, T, frC, 0.0, BAY, 3.4, 3.96, 0.0, .12, paint, .012, skip=('-z',))
    smould(g, T, frC, 0.0, BAY, 3.96, ck.P_FASCIA_CORNICE, paint)
    wall(g, T, frC, 0.0, BAY, 3.92, 4.0, st.wall, base=0.0)
    for x in (0.0, BAY):
        console(g, T, frC, x, 4.0, .18, .62, .3, paint)
    B.light(frC.P(1.25, 1.4, .6), 0xFFC98A, 4.0, 'shop')
    # the fascia letters
    if T.lod == 0:
        # (board 2: each word in one bay's clear span between its consoles; ASHGROVE under the turret's corbel cone, which
        # hides the chamfer's fascia, on the panel over the corner door)
        fascia_text(B, frA, 2 * BAY + CONSOLE_CLEAR, 3 * BAY - CONSOLE_CLEAR, 'GROCER', px=.052)
        fascia_text(B, frB, CONSOLE_CLEAR, BAY - CONSOLE_CLEAR, 'NEWS', px=.052)
        text(g, T, frC, 1.25, 2.95, .03, 'ASHGROVE', F['gilt'], c=.05, depth=.015)
    # --- upper storeys -------------------------------------------------------------------------------------------------
    bands = [(storey_floor(k) - .12, storey_floor(k) + .12, F['brick-buff']) for k in range(2, storeys)]
    for fr, kinds in ((frA, (0, 1, 2)), (frB, (0, 1, 2))):
        holes = []
        for k in range(1, storeys):
            y = storey_floor(k)
            for b in kinds:
                holes.append((Hole(b * BAY + .72, b * BAY + 1.78, y + .78, y + 2.55, 0.0, .18), k))
        wall(g, T, fr, 0.0, 3 * BAY, GROUND, H, st.wall, [hh for hh, _ in holes], bands=bands, base=0.0)
        for hh, k in holes:
            dressed_window(B, fr, hh, st, r, hood=(k == 1))
    for k in range(2, storeys):
        y = storey_floor(k) - .15
        if T.lod == 0:
            plan_mould(g, T, [(L, 0.0), (CH, 0.0)], y, ck.P_STRING_SMALL, fd, outs=[(0.0, -1.0)])
            plan_mould(g, T, [(0.0, CH), (0.0, L)], y, ck.P_STRING_SMALL, fd, outs=[(-1.0, 0.0)])
    if T.detail:
        quoins(g, T, frA, 0.0, GROUND, H, fd, into=1)
        quoins(g, T, frB, 3 * BAY, GROUND, H, fd, into=-1)
    # the chamfer's walls beside the turret
    turret_cx, turret_c, R = 1.25, .45, 1.25
    wall(g, T, frC, 0.0, BAY, GROUND, H, st.wall, [Hole(.1, 2.4, GROUND, H)], base=0.0)
    if T.detail:
        quoins(g, T, frC, 0.0, GROUND, H, fd, into=1)
        quoins(g, T, frC, BAY, GROUND, H, fd, into=-1)
    turret(B, frC, turret_cx, turret_c, R, st, r, storeys, fd)
    # --- cornice, parapet, name panels ---------------------------------------------------------------------------------
    street = [(L, 0.0), (CH, 0.0), (0.0, CH), (0.0, L)]
    souts = [(0.0, -1.0), (-SQ2, -SQ2), (-1.0, 0.0)]
    plan_mould(g, T, street, H, mould_lod(T, ck.P_CORNICE), fd, souts)
    if T.detail:
        for fr in (frA, frB):
            for i in range(10):
                x = .4 + i * .74
                fbox(g, T, fr, x - .07, x + .07, H - .16, H + .02, 0.0, .26, fd, 0, skip=('-z', '+y'))
    top = H + 1.4
    for fr, (u0, u1) in ((frA, (0.0, 3 * BAY)), (frB, (0.0, 3 * BAY))):
        wall(g, T, fr, u0, u1, H + .5, top, st.wall, base=0.0)
        inner = Frame(fr.P(u1, 0.0, -.32), fr.mu)
        wall(g, T, inner, 0.0, u1 - u0, H + .3, top, st.wall, base=0.0)
    sec = [(-.08, 0.0), (.42, 0.0), (.42, .06), (.3, .16), (-.08, .12)]
    ck.sweep(g, sec, [(L, top, -.04), (CH + .2, top, -.04)], fd, up=(0, 1, 0), smooth_u=False)
    ck.sweep(g, sec, [(-.04, top, CH + .2), (-.04, top, L)], fd, up=(0, 1, 0), smooth_u=False)
    panel = (1.1, 6.1)
    fbox(g, T, frA, panel[0], panel[1], H + .7, top - .12, 0.0, .06, fd, .012, skip=('-z',))
    fbox(g, T, frB, 2.0, 5.5, H + .7, top - .12, 0.0, .06, fd, .012, skip=('-z',))
    if T.lod == 0:
        text(g, T, frA, sum(panel) / 2, H + .76, .062, 'STORES', F['paint-green'], c=.06, depth=.02)
        text(g, T, frB, 3.75, H + .76, .062, '1903', F['paint-green'], c=.06, depth=.02)
    # --- the rear: party walls, roof, stacks ---------------------------------------------------------------------------
    pa = Frame((L, 0.0, L), (0.0, 0.0, -1.0))       # the party wall on street A's neighbour side, facing +x
    pb = Frame((0.0, 0.0, L), (1.0, 0.0, 0.0))     # the party wall on street B's neighbour side, facing +z
    for pf in (pa, pb):
        wall(g, T, pf, 0.0, L, 0.0, top, F['brick-stock'], base=0.0)
        plinth_run(B, pf, 0.0, L, F['brick-blue'])
        for k in (1, 2):
            smould(g, T, pf, 0.0, L, storey_floor(k) - .08, ck.P_STRING, fd)
            B.streaks.add_under(pf, .2, L - .2, storey_floor(k) - .08, .7, .12)
        # the party wall's parapet seen from the roof (pass 1 left its inside open): its inner face and a coping
        wall(g, T, Frame(pf.P(L, 0.0, -.32), pf.mu), 0.0, L, H + .3, top, F['brick-stock'], base=0.0)
        ck.sweep(g, COPING, [pf.P(L, top, .04), pf.P(0.0, top, .04)][::-1], fd, up=(0, 1, 0), smooth_u=False)
        if T.detail:
            for a in (.9, L - .9):
                ck.pattress(g, T, pf, a, storey_floor(1) + .3, kind='disc')
        B.streaks.add_under(pf, .3, L - .3, top - .05, 2.6, .3)
    # street A's side: the shop's tea, faded on the brick; street B's side: the scar of the house that came down
    ck.ghost_sign(g, T, pa, 1.0, L - 1.0, 6.0, 10.5, fit_lines((('MERIDIAN TEA', .07), ('SINCE 1888', .06)), L - 2.6),
                  'ghost-green', 'ghost-cream', rng('ghostsign', seed), streaks=B.streaks)
    neighbour_scar(B, pb, rng('scar', seed))
    roofp = [(0.0, CH), (CH, 0.0), (L, 0.0), (L, L), (0.0, L)]
    rf = Frame((0.0, H + .3, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0))   # a horizontal frame: (a, b) = (x, z), n = +y
    prism(g, rf, [(x, -z) for x, z in roofp], 0.0, .02, F['tar-felt'], caps=(False, True))
    stack(B, Frame((L - .35, 0.0, 0.0), (0.0, 0.0, 1.0)), 4.6, H - 2.7, 0.0, r, pots=4, along_u=True)
    stack(B, Frame((0.0, 0.0, L - .35), (1.0, 0.0, 0.0)), 5.0, H - 2.7, 0.0, r, pots=3, along_u=True)
    fbox(g, T, rf, 5.5, 6.5, -5.3, -4.5, .02, .55, F['galv'], .02)
    # --- street furniture the corner carries ---------------------------------------------------------------------------
    awning(B, frA, 2.55, 7.45, r)
    bracket_sign(B, frB, 3.75, 'NEWS')
    swan_neck(B, frA, 7.2, 4.4)
    downpipe(g, T, frA, .14, 0.0, H)


def neighbour_scar(B, fr, r, He=7.0, D=8.0):
    """The scar a demolished two-storey neighbour left on an exposed party wall (fr faces out of it, u 0 at the old house's
    front): its section in pale render over the brick, the lead flashing of its roof along the old rakes, the soot of its
    two flues, the joist pockets of its floor, the toothing where its back wall was bonded in. Flat paint and shallow
    relief, inside the .25 m rule; LOD1 keeps the patch and the flashing (a colour mass)."""
    g, T = B.g, B.T
    t = math.tan(PITCH)
    ee = He + .12
    roofline = lambda a: ee + t * min(max(a, 0.0), max(D - a, 0.0))
    if T.lod > 0:
        g.face([fr.P(0.0, .75, .006), fr.P(D, .75, .006), fr.P(D, ee, .006), fr.P(D / 2, ee + t * D / 2, .006),
                fr.P(0.0, ee, .006)], F['render-patch'], fr.n)
    else:
        rake_wall(B, fr, 0.0, D, .75, roofline, F['render-patch'], c=.006)
    # the flashing along the old roof: two bands .14 m wide, proud 1.2 cm
    for (a0, a1) in ((0.0, D / 2), (D / 2, D)):
        y0, y1 = roofline(a0), roofline(a1)
        g.face((fr.P(a0, y0, .012), fr.P(a1, y1, .012), fr.P(a1, y1 + .14, .012), fr.P(a0, y0 + .14, .012)), F['flashing'], fr.n)
    if T.lod > 0:
        return
    # the flues' soot, up from the old fireplaces to the roofline
    for a in (2.2, D - 2.2):
        fquad(g, fr, a - .18, 1.1, a + .18, roofline(a) - .05, .009, F['batten'])
        fquad(g, fr, a - .32, .9, a + .32, 1.6, .0095, F['concrete'])       # the hearth's cement scar
    # the joist pockets of the old first floor and its ceiling
    for y in (GROUND - .25, He - .25):
        a = .3
        while a < D - .3:
            fquad(g, fr, a, y, a + .1, y + .2, .011, F['interior'])
            a += .45
    # toothing at the old back wall's line: bricks left proud in alternate courses
    k = 0
    y = .1
    while y < He - .2:
        L_ = .23 if k % 2 == 0 else .11
        fbox(g, T, fr, D - .12, D + .12, y, y + .14, 0.0, L_ * .45, F['brick-stock'], 0, skip=('-z',))
        y += .3
        k += 1
    B.streaks.add_under(fr, .3, D - .3, ee - .1, 1.5, .2)


def posters(B, fr, r):
    """Posters in layers (bible 1.5): Civil Defence notices under STAY INDOORS under MISSING; flat colour with stroke titles."""
    g, T = B.g, B.T
    layers = [((.25, 1.0, 1.15, 2.2), 'poster-d', None), ((.95, 1.15, 1.95, 2.55), 'poster-a', 'STAY'),
              ((.4, 1.6, 1.35, 2.95), 'poster-b', None), ((1.4, .95, 2.25, 2.1), 'poster-c', 'MISSING')]
    c = .004
    for (u0, v0, u1, v1), f, title in layers:
        a = r.uniform(-.03, .03)
        fquad(g, fr, u0, v0 + a, u1, v1 + a, c, F[f])
        if title:
            text(g, T, fr, (u0 + u1) / 2, v1 - .3, .03, title, F['paint-black'] if f != 'poster-c' else F['poster-d'],
                 c=c + .002, raised=False)
            if title == 'STAY':
                text(g, T, fr, (u0 + u1) / 2, v1 - .58, .03, 'INDOORS', F['paint-black'], c=c + .002, raised=False)
        c += .004


def outs_from(pts, c, closed=False):
    """Outward normals (in plan) for a polyline round a centre c: each segment's normal turned away from c."""
    out = []
    m = len(pts)
    for i in range(m if closed else m - 1):
        (x0, z0), (x1, z1) = pts[i], pts[(i + 1) % m]
        dx, dz = x1 - x0, z1 - z0
        L = math.hypot(dx, dz) or 1.0
        nx, nz = dz / L, -dx / L
        if nx * ((x0 + x1) / 2 - c[0]) + nz * ((z0 + z1) / 2 - c[1]) < 0:
            nx, nz = -nx, -nz
        out.append((nx, nz))
    return out


def turret(B, frC, cx, cc, R, st, r, storeys, fd):
    """The corner turret: an octagon on frC (the chamfer) from 4.0 m to the cornice on a corbelled cone, a sash in each of
    its three faces a storey; above the cornice a drum with oculi under an octagonal lead dome, ribs and a finial."""
    g, T = B.g, B.T
    H = storey_floor(storeys)
    ph = math.radians(22.5)
    V = [(cx + R * math.cos(ph + k * math.pi / 4), cc + R * math.sin(ph + k * math.pi / 4)) for k in range(8)]
    _c = frC.P(cx, 0.0, cc)
    ctr = (_c[0], _c[2])
    # the front facets: 7 (clipped), 0, 1, 2, 3 (clipped), ordered so each facet's frame faces out
    def clip(p, q):
        (a0, c0), (a1, c1) = p, q
        if c0 >= 0 and c1 >= 0:
            return p, q
        t = c0 / (c0 - c1)
        x = (a0 + (a1 - a0) * t, 0.0)
        return (x, q) if c0 < 0 else (p, x)
    e0 = (V[4][0] + (V[3][0] - V[4][0]) * (0 - V[4][1]) / (V[3][1] - V[4][1]), 0.0)   # where facets 3 and 7 meet the wall
    e1 = (V[7][0] + (V[0][0] - V[7][0]) * (0 - V[7][1]) / (V[0][1] - V[7][1]), 0.0)
    outline = [(frC.P(a, 0, c)[0], frC.P(a, 0, c)[2]) for a, c in (e0, V[3], V[2], V[1], V[0], e1)]
    facets = []
    for k in (3, 2, 1, 0, 7):
        p, q = V[(k + 1) % 8], V[k]
        p, q = clip(p, q)
        d = (q[0] - p[0], q[1] - p[1])
        w = math.hypot(*d)
        facets.append((Frame(frC.P(p[0], 0.0, p[1]), frC.D(d[0], 0.0, d[1])), w, k))
    fw = fd
    for k_st in range(1, storeys):
        y = storey_floor(k_st)
        for ff, w, k in facets:
            if k in (0, 1, 2):
                h = Hole(.2, w - .2, y + .8, y + 2.45, 0.0, .14)
                wall(g, T, ff, 0.0, w, y, y + STOREY, fw, [h], base=0.0)
                reveal(g, T, ff, h, fw)
                mould(g, T, ff, h.u0 - .05, h.u1 + .05, h.v0, mould_lod(T, [(0.0, -.08), (.08, -.08), (.09, -.02), (0.0, .01)]), fd)
                st.window(B, ff, h, r, bars=False)
                B.streaks.add_under(ff, h.u0, h.u1, h.v0 - .08, 1.0, .3)
            else:
                wall(g, T, ff, 0.0, w, y, y + STOREY, fw, base=0.0)
        if T.detail:
            plan_mould(g, T, outline, y + STOREY - .14, ck.P_STRING_SMALL, fd, outs_from(outline, ctr))
    # the corbelled cone under it
    rings = [(GROUND, R, cc), (GROUND - .25, R * .78, cc * .8), (GROUND - .5, R * .5, cc * .55), (GROUND - .72, R * .22, cc * .35)]
    def ring_pts(Rr, ccr):
        out = []
        for k in range(8):
            a = ph + k * math.pi / 4
            out.append((cx + Rr * math.cos(a), max(0.0, ccr + Rr * math.sin(a))))
        return out
    rp = [ring_pts(Rr, cr) for _, Rr, cr in rings]
    for i in range(len(rings) - 1):
        y0, y1 = rings[i][0], rings[i + 1][0]
        for k in (7, 0, 1, 2, 3):
            a, b = rp[i][k], rp[i][(k + 1) % 8]
            c_, d = rp[i + 1][(k + 1) % 8], rp[i + 1][k]
            pts = [frC.P(a[0], y0, a[1]), frC.P(b[0], y0, b[1]), frC.P(c_[0], y1, c_[1]), frC.P(d[0], y1, d[1])]
            mid = math.radians(22.5 + 45 * k + 22.5)
            g.face(pts, fd, frC.D(math.cos(mid), -.7, math.sin(mid)))
    last = rp[-1]
    g.face([frC.P(a, rings[-1][0], c) for a, c in last], fd, (0, -1, 0))
    # the cornice ring at the main cornice and the drum above
    pts = [(frC.P(a, 0, c)[0], frC.P(a, 0, c)[2]) for a, c in [V[3], V[2], V[1], V[0]]]
    plan_mould(g, T, pts, H, mould_lod(T, ck.P_CORNICE), fd, outs_from(pts, ctr), caps=True)
    Rd = R * .92
    D8 = [(cx + Rd * math.cos(ph + k * math.pi / 4), cc + Rd * math.sin(ph + k * math.pi / 4)) for k in range(8)]
    y0, y1 = H + .5, H + 1.75
    for k in range(8):
        p, q = D8[(k + 1) % 8], D8[k]
        d = (q[0] - p[0], q[1] - p[1])
        w = math.hypot(*d)
        ff = Frame(frC.P(p[0], 0.0, p[1]), frC.D(d[0], 0.0, d[1]))
        wall(g, T, ff, 0.0, w, y0, y1, fd, base=0.0)
        if k in (0, 1, 2) and T.detail:
            oc = ff.P(w / 2, (y0 + y1) / 2, 0.0)
            sk.lathe(g, [(.3, -.0), (.3, .05), (.22, .07), (.22, -.02)], oc, fd, T.round, axis=ff.n)
            sk.lathe(g, [(.22, -.02), (0.0, -.02)], oc, F['glass-dark'] if r.random() < .6 else F['lit-tungsten'], T.round, axis=ff.n)
    dpts = [(frC.P(a, 0, c)[0], frC.P(a, 0, c)[2]) for a, c in D8]
    plan_mould(g, T, dpts, y0 - .5, [(0.0, 0.0), (.0, .5)], fd, outs_from(dpts, ctr, True), closed=True)
    plan_mould(g, T, dpts, y1, mould_lod(T, [(0.0, 0.0), (.06, 0.0), (.06, .06), (.14, .12), (.14, .2), (0.0, .22)]), fd,
               outs_from(dpts, ctr, True), closed=True)
    # the dome: octagonal, an ogee of lead with rolls on its hips, a gilt finial
    base = frC.P(cx, y1 + .22, cc)
    prof = [(Rd * 1.08, 0.0), (Rd * 1.08, .1), (Rd * 1.0, .2), (Rd * .97, .55), (Rd * .85, 1.0), (Rd * .62, 1.45), (Rd * .38, 1.8),
            (Rd * .2, 2.05), (Rd * .12, 2.2)]
    if T.lod > 0:
        prof = [prof[0], prof[2], prof[4], prof[6], prof[8]]
    dirs = []
    for k in range(8):
        a = ph + k * math.pi / 4
        dirs.append(frC.D(math.cos(a), 0.0, math.sin(a)))
    rings2 = []
    for (rr, hh) in prof:
        rings2.append([(base[0] + d[0] * rr, base[1] + hh, base[2] + d[2] * rr) for d in dirs])
    for i in range(len(prof) - 1):
        for k in range(8):
            k1 = (k + 1) % 8
            pts = (rings2[i][k], rings2[i][k1], rings2[i + 1][k1], rings2[i + 1][k])
            mid = frC.D(math.cos(ph + (k + .5) * math.pi / 4), 0.0, math.sin(ph + (k + .5) * math.pi / 4))
            dr = prof[i + 1][0] - prof[i][0]
            dh = prof[i + 1][1] - prof[i][1]
            g.face(pts, F['lead'], (mid[0] * dh, -dr, mid[2] * dh))
    if T.detail:
        for k in range(8):
            path = [rings2[i][k] for i in range(len(prof))]
            ck.tube(g, path, .035, F['lead'], 4, caps=False)
    tip = (base[0], base[1] + prof[-1][1], base[2])
    sk.lathe(g, [(Rd * .14, 0.0), (.12, .08), (.16, .2), (.12, .32), (.04, .4), (.03, .7), (0.0, .78)], tip, F['gilt'], T.small)
    sk.lathe(g, [(.0, -.01), (.12, .02), (.15, .12), (.12, .22), (0.0, .25)], (tip[0], tip[1] + .42, tip[2]), F['gilt'], T.small)


def awning(B, fr, u0, u1, r):
    """A striped canvas awning, half out, from under the fascia (its valance's foot at 3.12 m: overhead art)."""
    g, T = B.g, B.T
    y0, y1, c1 = 3.4, 3.3, 1.05
    fbox(g, T, fr, u0, u1, 3.36, 3.44, 0.0, .1, F['iron-black'], .008, skip=('-z',))
    n = max(1, int((u1 - u0) / .3)) if T.lod == 0 else 1
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        f = F['canvas-red'] if i % 2 == 0 or T.lod > 0 else F['canvas-cream']
        top = (fr.P(a, y0, .1), fr.P(b, y0, .1), fr.P(b, y1, c1), fr.P(a, y1, c1))
        g.face(top, f, fr.D(0, 1, .1))
        g.face(top[::-1], f, fr.D(0, -1, -.1))
        g.face((fr.P(a, 3.12, c1), fr.P(b, 3.12, c1), fr.P(b, y1, c1), fr.P(a, y1, c1)), f, fr.n)
        g.face((fr.P(a, 3.12, c1 - .005), fr.P(b, 3.12, c1 - .005), fr.P(b, y1, c1 - .005), fr.P(a, y1, c1 - .005)), f, fr.mn)
    for x in (u0 + .1, u1 - .1):
        sk.cylinder(g, fr.P(x, 3.12, .03), fr.P(x, y1 - .02, c1 - .03), .018, F['iron-black'], T.small)
    sk.cylinder(g, fr.P(u0, y1 - .01, c1 - .02), fr.P(u1, y1 - .01, c1 - .02), .025, F['iron-black'], T.small)


def bracket_sign(B, fr, u, word):
    """A hanging bracket sign above 3.1 m: a scrolled iron bracket and a board lettered both sides."""
    g, T = B.g, B.T
    fbox(g, T, fr, u - .05, u + .05, 3.55, 4.05, 0.0, .04, F['iron-black'], .006, skip=('-z',))
    sk.cylinder(g, fr.P(u, 3.95, .02), fr.P(u, 3.95, 1.0), .02, F['iron-black'], T.small)
    if T.detail:
        pts = [fr.P(u, 3.62, .03), fr.P(u, 3.72, .35), fr.P(u, 3.88, .6), fr.P(u, 3.95, .8)]
        ck.tube(g, sk.catmull(pts, 4), .014, F['iron-black'], 4)
    a0, a1 = .2, 1.0
    fbox(g, T, fr, u - .025, u + .025, 3.18, 3.82, a0, a1, F['paint-green'], .01)
    for x in (a0 + .1, a1 - .1):
        sk.cylinder(g, fr.P(u, 3.82, x), fr.P(u, 3.95, x), .008, F['iron-black'], 4)
    if T.lod == 0:
        for side in (1, -1):
            sf = Frame(fr.P(u + .026 * side, 0.0, (a0 + a1) / 2), fr.mn if side > 0 else fr.n)
            text(g, T, sf, 0.0, 3.36, .042, word, F['gilt'], c=0.0, depth=.012)


def swan_neck(B, fr, u, y):
    """The sodium swan-neck lamp on a wall bracket (bible 5.2: on the corner at 4.4 m)."""
    g, T = B.g, B.T
    fbox(g, T, fr, u - .08, u + .08, y + .15, y + .75, 0.0, .05, F['iron-green'], .01, skip=('-z',))
    pts = [fr.P(u, y + .45, .04), fr.P(u, y + .6, .4), fr.P(u, y + .62, .75), fr.P(u, y + .5, .95), fr.P(u, y + .32, 1.0)]
    ck.tube(g, sk.catmull(pts, 4 if T.lod == 0 else 2), .035, F['iron-green'], T.small)
    head = fr.P(u, y + .2, 1.0)
    # the dish shade, seen from the street below and the roofs above (pass 1 drew only its underside, inside out), and the
    # bowl walked from its bottom up so its glass faces down and out
    ck.lathe_shell(g, [(.26, -.02), (.26, 0.0), (.2, .06), (.05, .14), (0.0, .15)], head, F['iron-green'], T.round, .015)
    sk.lathe(g, [(0.0, -.24), (.1, -.22), (.2, -.14), (.24, -.03)], head, F['sodium'], T.round)
    sk.lathe(g, [(.24, -.035), (.26, -.035)], head, F['iron-green'], T.round, smooth=False)
    B.light((head[0] + fr.n[0] * .0, head[1] - .25, head[2] + fr.n[2] * .0), 0xFFA14A, 9.0, 'sodium')


# ---------------------------------------------------------------------------------------------------------------------------
# The Walls: the Victorian tenement (bible 5.3)
# ---------------------------------------------------------------------------------------------------------------------------
# A parapet's coping (pass 2): x runs INTO the block along these sweeps (their frames' u is the path's left, which is inward
# for a block's loop and a face run along its u). Pass 1's section was the mirror image: its coping hung .40 m over the
# street and left the parapet's top open, so from the roofs one saw down into the wall.
COPING = [(.36, 0.0), (-.1, 0.0), (-.1, .07), (0.0, .17), (.36, .13)]
TEN_STOREYS = 6       # ground + five: 19 m
TOWER_P = 1.0         # the stair tower's projection from the face (deep relief, bible 2.1: it keeps a silhouette to 25 m)
TOWER_SPAN = (BAY - .25, 2 * BAY + .25)   # its span on an entry face (bay 1 and a little more)
TOWER_BACK = 2.2      # how far its stair house reaches back over the roof


def tenement_face(B, fr, seed, kind='entry', mouldings=True, ground='flats', storeys=TEN_STOREYS, tower=True):
    """A 10 x 19 m tenement face (4 bays): red brick with buff bands at the floors and blue at the sills (polychrome),
    segmental arches of alternating voussoirs, stone sills. kind:
    - 'entry': the arched entrance in bay 1 under the stair tower (pass 2: `stair_tower`, corbelled out from 3.2 m, its
      half-landing windows lit up the face, the strongest vertical at night (bible 5.3));
    - 'plain' and 'escape' (a plain face the fire escape climbs);
    - 'back' (pass 2): the back court's face, lean: stone lintels for arches, the simple sash, the back door, a soil stack.
    ground 'flats' (barred windows) or 'shops'; mouldings: its own cornice, corbel table and parapet (a block runs them round
    itself instead). Windows above the third floor take the simple sash (pass 2; their bars do not read from the street)."""
    g, T = B.g, B.T
    r = rng('tenement', seed, kind)
    st = Style(seed, brick=True, district=getattr(B, 'district', None))
    st.wall = F['brick-red']
    W = 4 * BAY
    H = storey_floor(storeys)
    fd = F['stone-dress']
    back = kind == 'back'
    entry = kind == 'entry'
    bands = []
    for k in range(1, storeys):
        y = storey_floor(k)
        bands.append((y - .15, y + .15, F['brick-buff']))
        if not back:
            bands.append((y + .62, y + .74, F['brick-blue']))
    holes = []
    for k in range(1, storeys):
        y = storey_floor(k)
        for b in range(4):
            if entry and b == 1:
                continue
            if back:
                holes.append(('win', Hole(b * BAY + .75, b * BAY + 1.75, y + .8, y + 2.45, 0.0, .16), k))
            else:
                holes.append(('win', Hole(b * BAY + .7, b * BAY + 1.8, y + .75, y + 2.47, .18, .2), k))
    if entry:
        holes.append(('entry', Hole(BAY + .55, BAY + 1.95, 0.0, 2.55, .7, .25), 0))
    for b in range(4):
        if entry and b == 1:
            continue
        if ground == 'shops' and b < 2:
            continue
        if back and b == 1:
            holes.append(('backdoor', Hole(b * BAY + .7, b * BAY + 1.8, 0.0, 2.45, 0.0, .16), 0))
        elif back:
            holes.append(('win', Hole(b * BAY + .75, b * BAY + 1.75, 1.1, 2.7, 0.0, .16), 0))
        else:
            holes.append(('barred', Hole(b * BAY + .65, b * BAY + 1.85, 1.1, 2.98, .17, .2), 0))
    hs = [h for _, h, _ in holes]
    has_tower = entry and tower
    if has_tower:
        hs.append(Hole(TOWER_SPAN[0], TOWER_SPAN[1], 4.0, H + 2.0, keep=True))   # the face behind the tower is not drawn
    if ground == 'shops':
        hs.append(Hole(0.0, 2 * BAY, 0.0, GROUND, keep=True))
    plinth_top = .9
    wall(g, T, fr, 0.0, W, plinth_top, H + .8, st.wall, hs, bands=bands, base=fr.o[1])
    gb = [h for kk, h, _ in holes if kk in ('entry', 'backdoor')]
    if ground == 'shops':
        trade = rng('shopname', seed).choice(('DAIRY', 'BUTCHER', 'COBBLER', 'CAFE', 'NEWS', 'LAUNDRY', 'GROCER', 'TAILOR'))
        for b in range(2):
            ground_shop_bay(B, fr.shifted(b * BAY), st, r, lit=b == 0, shutter=b == 1, pil=(True, b == 1),
                            paint=F['paint-oxblood'], trade=trade)
        wall(g, T, fr, 2 * BAY, W, 0.0, plinth_top, F['brick-blue'], gb, c=.04, base=fr.o[1])
        smould(g, T, fr, 2 * BAY, W, plinth_top, ck.P_PLINTH_CAP, F['brick-blue'])
        if T.lod == 0:
            # (board 2: in the lit bay's clear span, between its console and the next bay's, not across that console)
            fascia_text(B, fr, CONSOLE_CLEAR, BAY + .01, trade)
    else:
        plinth_run(B, fr, 0.0, W, F['brick-blue'], gb, top=plinth_top)
    for kind_, h, k in holes:
        if kind_ == 'entry':
            tenement_entry(B, fr, h, r, fd)
            continue
        if kind_ == 'backdoor':
            reveal(g, T, fr, h, st.wall, sill=False)
            panel_door(g, T, fr, h.u0 + .04, h.u1 - .04, 0.0, h.v1, -h.depth + .02, F['paint-green'], r, panels=4,
                       knocker=False)
            fbox(g, T, fr, h.u0 - .14, h.u1 + .14, h.v1, h.v1 + .24, 0.0, .04, fd, 0, skip=('-z',))
            continue
        reveal(g, T, fr, h, st.wall)
        sill(g, T, fr, h.u0, h.u1, h.v0, fd, lean=k >= 1, slab=k >= 2)
        B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.4, .34)
        if back:
            fbox(g, T, fr, h.u0 - .12, h.u1 + .12, h.v1, h.v1 + .22, 0.0, .035, fd, 0, skip=('-z',))
        else:
            arch_ring(g, T, fr, h, F['brick-buff'], .22, .03, F['stone-dress'] if T.detail else None,
                      key=fd if k in (1, 3) else None)
        st.window(B, fr, h, r, ground=(k == 0), frame=F['paint-white'], simple=back or k >= 2)
        if kind_ == 'barred' and T.lod == 0:
            for i in range(5):
                x = h.u0 + (h.u1 - h.u0) * (i + .5) / 5
                fbox(g, T, fr, x - .0175, x + .0175, h.v0, h.top_v, -.04, -.005, F['iron-black'], 0, skip=('-z',))
            for y in (h.v0 + .55, h.v0 + 1.45):
                fbox(g, T, fr, h.u0, h.u1, y - .02, y + .02, -.045, -.01, F['iron-black'], 0, skip=('-z',))
    # ventilation grilles in the plinth (the vents' entries)
    if T.detail and not back:
        for b in range(4):
            if (entry and b == 1) or (ground == 'shops' and b < 2):
                continue
            x = b * BAY + 1.25
            fquad(g, fr, x - .22, .3, x + .22, .55, .041, F['iron-black'])
            for i in range(4):
                xx = x - .165 + i * .11
                fbox(g, T, fr, xx - .014, xx + .014, .31, .54, .041, .055, F['iron-black'], 0, skip=('-z', '-y', '+y'))
    if has_tower:
        stair_tower(B, fr, seed, st, storeys)
    if mouldings:
        cornice_block(B, [(fr.P(0, 0, 0)[0], fr.P(0, 0, 0)[2]), (fr.P(W, 0, 0)[0], fr.P(W, 0, 0)[2])], H, closed=False,
                      outs=[(fr.n[0], fr.n[2])])
        wall(g, T, fr, 0.0, W, H + 1.25, H + 1.9, st.wall, [Hole(TOWER_SPAN[0], TOWER_SPAN[1], H, H + 2, keep=True)]
             if has_tower else (), base=fr.o[1])
        ck.sweep(g, COPING, [fr.P(0.0, H + 1.9, .04), fr.P(W, H + 1.9, .04)],
                 fd, up=(0, 1, 0), smooth_u=False)
    if T.detail:
        if back:
            downpipe(g, T, fr, BAY - .2, 0.0, H + .7, c=.1, r=.075, f=F['iron-black'])      # the soil stack (shoe .25 proud)
        downpipe(g, T, fr, W - .14, 0.0, H + .7, f=F['iron-black'])


def tenement_entry(B, fr, h, r, fd):
    """The tenement's arched entrance (bible 5.3): a stone surround and keystone, double doors, a lit fanlight with its
    glazing bars, the 1894 datestone; a real dark opening under a bright frame (bible 2.7)."""
    g, T = B.g, B.T
    if not cut(T, h):
        fquad(g, fr, h.u0, 0.0, h.u1, h.v1, .06, F['paint-green'])
        fquad(g, fr, h.u0, h.v1, h.u1, h.top_v, .06, F['lit-stair'])
        B.light(fr.P((h.u0 + h.u1) / 2, 2.6, .6), 0xFFD9A0, 4.0, 'fanlight')
        return
    reveal(g, T, fr, h, fd, sill=False)
    arch_ring(g, T, fr, h, fd, .32, .07, key=fd)
    for (a, b) in ((h.u0 - .26, h.u0), (h.u1, h.u1 + .26)):
        fbox(g, T, fr, a, b, 0.0, h.v1, 0.0, .07, fd, .012, skip=('-z', '-y'))
    for (a, b) in ((h.u0 - .3, h.u0), (h.u1, h.u1 + .3)):
        fbox(g, T, fr, a, b, h.v1 - .16, h.v1, 0.0, .1, fd, .012, skip=('-z',))
    bar(g, T, fr, h.u0, h.u1, h.v1 - .08, h.v1, -.25, -.17, F['paint-green'])
    for (a, b) in ((h.u0 + .05, (h.u0 + h.u1) / 2), ((h.u0 + h.u1) / 2, h.u1 - .05)):
        panel_door(g, T, fr, a, b, 0.0, h.v1 - .08, -.22, F['paint-green'], r, panels=4, knocker=False)
    hh = Hole(h.u0, h.u1, h.v1, h.v1, h.rise, .25)
    cu = hh.cuts(T)
    for i in range(len(cu) - 1):
        a, b = cu[i], cu[i + 1]
        g.face((fr.P(a, h.v1, -.2), fr.P(b, h.v1, -.2), fr.P(b, hh.top(b), -.2), fr.P(a, hh.top(a), -.2)), F['lit-stair'], fr.n)
    if T.detail:
        cxu = (h.u0 + h.u1) / 2
        for i in range(1, 6):
            a = math.pi * i / 6
            p0 = fr.P(cxu, h.v1, -.19)
            p1 = fr.P(cxu - math.cos(a) * h.rise, h.v1 + math.sin(a) * h.rise, -.19)
            sk.cylinder(g, p0, p1, .015, F['paint-white'], 4, caps=False)
        fbox(g, T, fr, h.u0 + .4, h.u1 - .4, 3.42, 3.86, 0.0, .05, fd, .01, skip=('-z',))
        text(g, T, fr, (h.u0 + h.u1) / 2, 3.5, .04, '1894', F['paint-black'], c=.05, depth=.008)
    B.light(fr.P((h.u0 + h.u1) / 2, 2.6, .6), 0xFFD9A0, 4.0, 'fanlight')


def stair_tower(B, fr, seed, st, storeys=TEN_STOREYS):
    """The tenement's stair tower (pass 2; bible 5.3 'stone stair halls', 'a lit stairwell strip, the strongest vertical at
    night'): corbelled out from the face in three stone courses from 3.2 m (so it is overhead art: nothing new stands proud
    below 3.1 m), 1.0 m deep and 3.0 m wide over the entrance, up past the parapet into the stair house on the roof (its
    door opening onto the roof at the back; on a reachable roof its volume is a block, bible 2.3), under a slate pyramid
    with a lead finial. Its windows sit on the half landings, out of step with the flats' floors, all lit but one now and
    then; a caged lamp over the roof door."""
    g, T = B.g, B.T
    fd = F['stone-dress']
    wf = st.wall
    H = storey_floor(storeys)
    u0, u1 = TOWER_SPAN
    P = TOWER_P
    Wt = u1 - u0
    roof_y = H + .8
    top = H + 4.4
    cb = -TOWER_BACK
    r = rng('stairtower', seed)
    bands = []
    for k in range(1, storeys):
        y = storey_floor(k)
        bands.append((y - .15, y + .15, F['brick-buff']))
    bands.append((top - .9, top - .6, F['brick-buff']))
    # the corbel courses: three steps of stone under the tower, each shading the next
    for i in range(3):
        va, vb = 3.2 + .27 * i, 3.2 + .27 * (i + 1)
        fbox(g, T, fr, u0 + .02 * (2 - i), u1 - .02 * (2 - i), va, vb, 0.0, P * (i + 1) / 3, fd, .01 if T.bevel else 0,
             skip=('-z',))
    # the front: the half-landing windows (segmental heads), and one more in the stair house
    tf = Frame(fr.P(u0, 0.0, P), fr.u)
    wins = []
    for k in range(1, storeys):
        yk = storey_floor(k) + 1.55
        wins.append(Hole(Wt / 2 - .5, Wt / 2 + .5, yk - .35, yk + .95, .25, .2))
    wins.append(Hole(Wt / 2 - .5, Wt / 2 + .5, roof_y + .9, roof_y + 2.3, .25, .2))
    wall(g, T, tf, 0.0, Wt, 4.0, top, wf, wins, bands=bands, base=0.0)
    dead = r.randrange(len(wins) + 3)        # now and then one landing's bulb has gone
    for i, h in enumerate(wins):
        reveal(g, T, tf, h, wf)
        sill(g, T, tf, h.u0, h.u1, h.v0, fd, lean=True)
        arch_ring(g, T, tf, h, F['brick-buff'], .2, .03, None, key=fd if i % 2 == 0 else None)
        lit = i != dead
        sash_window(g, T, tf, h, 'lit-stair' if lit else 'dark', r, F['paint-white'], bars=False, simple=True)
        B.streaks.add_under(tf, h.u0 - .1, h.u1 + .1, h.v0 - .1, 1.6, .34)
    # the sides: the tower part from the corbels up, the stair house part from the roof up (over the parapet's inside)
    lf = Frame(fr.P(u0, 0.0, cb), fr.n)          # faces -u
    rf = Frame(fr.P(u1, 0.0, P), fr.mn)          # faces +u
    for (sf, a_t0, a_t1, a_h0, a_h1) in ((lf, -cb, -cb + P, 0.0, -cb), (rf, 0.0, P, P, P - cb)):
        wall(g, T, sf, a_t0, a_t1, 4.0, top, wf, bands=bands, base=0.0)
        wall(g, T, sf, a_h0, a_h1, roof_y, top, wf, bands=[b for b in bands if b[0] > roof_y], base=0.0)
    # the stair house's back, its door onto the roof and the caged lamp over it
    bfr = Frame(fr.P(u1, 0.0, cb), fr.mu)
    dh = Hole(Wt / 2 - .45, Wt / 2 + .45, roof_y, roof_y + 2.1, 0.0, .12)
    wall(g, T, bfr, 0.0, Wt, roof_y, top, wf, [dh], base=0.0)
    reveal(g, T, bfr, dh, wf, sill=False)
    panel_door(g, T, bfr, dh.u0, dh.u1, roof_y, roof_y + 2.1, -.1, F['paint-oxblood'], r, panels=4, knocker=False)
    lp = bfr.P(Wt / 2, roof_y + 2.4, .12)
    sk.box(g, lp[0] - .1, lp[0] + .1, lp[1] - .07, lp[1] + .07, lp[2] - .1, lp[2] + .1, F['lit-cold'], None, 0)
    B.light((lp[0] - fr.n[0] * .4, lp[1] - .1, lp[2] - fr.n[2] * .4), 0xDCEBFF, 5.0, 'bulkhead')
    # stone strips up the tower's front corners
    if T.detail:
        for a in (0.0, Wt - .2):
            fbox(g, T, tf, a, a + .2, 4.0, top - .35, 0.0, .03, fd, 0, skip=('-z',))
    # its cornice at the block's and a crown under the roof, round its three faces and the stair house's back
    loop = [fr.P(u0, 0.0, cb), fr.P(u0, 0.0, P), fr.P(u1, 0.0, P), fr.P(u1, 0.0, cb)]
    pts = [(p[0], p[2]) for p in loop]
    outs = [(-fr.u[0], -fr.u[2]), (fr.n[0], fr.n[2]), (fr.u[0], fr.u[2]), (-fr.n[0], -fr.n[2])]
    street3 = [(fr.P(u0, 0.0, 0.0)[0], fr.P(u0, 0.0, 0.0)[2]), pts[1], pts[2], (fr.P(u1, 0.0, 0.0)[0], fr.P(u1, 0.0, 0.0)[2])]
    plan_mould(g, T, street3, H + .8, mould_lod(T, ck.P_CORNICE), fd, outs[0:3], closed=False)
    plan_mould(g, T, pts, top - .35, mould_lod(T, [(0.0, 0.0), (.06, 0.0), (.06, .08), (.16, .16), (.16, .28), (0.0, .35)]),
               fd, outs, closed=True)
    # the slate pyramid over the stair house, its eaves out .15, and a lead finial
    e = .15
    cx, cz = (pts[0][0] + pts[2][0]) / 2, (pts[0][1] + pts[2][1]) / 2
    corners = []
    for (px, pz) in pts:
        dx, dz = px - cx, pz - cz
        L = math.hypot(dx, dz)
        corners.append((px + dx / L * e * 1.414, pz + dz / L * e * 1.414))
    apex = (cx, top + 1.7, cz)
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        mx, mz = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cz
        g.face(((a[0], top, a[1]), (b[0], top, b[1]), apex), F['slate'], (mx, 1.2, mz))
        if T.detail:
            # the hip's lead roll
            ck.tube(g, [(a[0], top + .02, a[1]), (apex[0], apex[1] + .02, apex[2])], .03, F['lead'], 4, caps=False)
    g.face([(c[0], top, c[1]) for c in corners][::-1], F['paint-white'], (0, -1, 0))
    sk.lathe(g, [(.09, 0.0), (.09, .25), (.14, .35), (.05, .55), (.03, .95), (0.0, 1.0)], (apex[0], apex[1] - .1, apex[2]),
             F['lead'], T.small)
    B.streaks.add_under(tf, .2, Wt - .2, top - .35, 2.0, .3)


def cornice_block(B, pts, H, closed=True, outs=None):
    """The tenement's crown over its top storey (H): brick corbels from H, a buff corbel table at H + .4, the stone cornice at
    H + .8; run round the block with mitred corners. (Pass 2: the corbels are plain blocks, no chamfer, no back, no top:
    a block's crown was 4,500 triangles of them.)"""
    g, T = B.g, B.T
    fd = F['stone-dress']
    plan_mould(g, T, pts, H + .4, mould_lod(T, [(0.0, 0.0), (.12, 0.0), (.12, .38), (.0, .4)]), F['brick-buff'], outs,
               closed=closed)
    plan_mould(g, T, pts, H + .8, mould_lod(T, ck.P_CORNICE), fd, outs, closed=closed)
    if T.detail:
        m = len(pts)
        ns = m if closed else m - 1
        for s in range(ns):
            (x0, z0), (x1, z1) = pts[s], pts[(s + 1) % m]
            L = math.hypot(x1 - x0, z1 - z0)
            n = int(L / .6)
            ux, uz = (x1 - x0) / L, (z1 - z0) / L
            nx, nz = (outs[s] if outs else (uz, -ux))
            for i in range(n):
                t = (i + .5) * L / n
                x, z = x0 + ux * t, z0 + uz * t
                fr = Frame((x - ux * .09, 0.0, z - uz * .09), (ux, 0.0, uz))
                if abs(fr.n[0] - nx) + abs(fr.n[2] - nz) > .5:
                    fr = Frame((x + ux * .09, 0.0, z + uz * .09), (-ux, 0.0, -uz))
                fbox(g, T, fr, 0.0, .18, H, H + .4, 0.0, .1, F['brick-buff'], 0, skip=('-z', '+y'))


def fire_escape(B, fr, u0=2.6, u1=7.4, storeys=TEN_STOREYS):
    """The zigzag fire escape (bible 5.3, 5.8): platforms at each upper floor, flights between them alternating, railings
    with chunky balusters (35 mm, the chunk factor), brackets, and the drop ladder to 3.2 m (overhead art; the pilots' route
    is Astra's blocks). Pass 2: balusters every .4 m as open-ended bars, the grating as fewer slots."""
    g, T = B.g, B.T
    fi = F['iron-black']
    d0, d1 = .06, 1.12
    run = 2.35
    if T.lod > 0:
        for k in range(1, storeys):
            y = storey_floor(k)
            fbox(g, T, fr, u0, u1, y - .06, y, d0, d1, fi, 0, skip=('-z',))
            fquad(g, fr, u0, y, u1, y + 1.0, d1, fi)
            fquad(g, fr, u0, y, u1, y + 1.0, d1 - .01, fi, fr.mn)
            for (x, nrm) in ((u0, fr.mu), (u1, fr.u)):
                q = (fr.P(x, y, d0), fr.P(x, y, d1), fr.P(x, y + 1.0, d1), fr.P(x, y + 1.0, d0))
                g.face(q, fi, nrm)
                g.face(q[::-1], fi, (-nrm[0], -nrm[1], -nrm[2]))
            if k < storeys - 1:
                left = (k % 2 == 1)
                xa, xb = (u0 + .45, u0 + .45 + run) if left else (u1 - .45, u1 - .45 - run)
                g.face((fr.P(xa, y, .58), fr.P(xb, storey_floor(k + 1), .58), fr.P(xb, storey_floor(k + 1), 1.06),
                        fr.P(xa, y, 1.06)), fi, (0, 1, 0))
        return
    for k in range(1, storeys):
        y = storey_floor(k)
        hole = None
        if k > 1:
            if (k - 1) % 2 == 1:      # the flight below rose toward +u
                xb = u0 + .45 + run
                hole = (xb - run * .45, xb + .05)
            else:
                xb = u1 - .45 - run
                hole = (xb - .05, xb + run * .45)
        slab = Frame(fr.P(0.0, y, 0.0), fr.u, fr.mn)    # horizontal: a = u, b = -c
        cuts = sorted([u0, u1] + (list(hole) if hole else []))
        for i in range(len(cuts) - 1):
            a, b = cuts[i], cuts[i + 1]
            if hole and hole[0] <= (a + b) / 2 <= hole[1]:
                fbox(g, T, fr, a, b, y - .06, y, d0, .55, fi, 0)
                continue
            fbox(g, T, fr, a, b, y - .06, y, d0, d1, fi, .006)
            n = int((b - a) / .3)          # (board 3: slots every 0.3 m, the dressed chunk's cap; pass 2's 0.2 m)
            for j in range(1, n):
                x = a + (b - a) * j / n
                fquad(g, slab, x - .01, -d1 + .02, x + .01, -d0 - .02, .001, F['batten'], (0, 1, 0))
        rails = [(fr.P(u0, y, d1), fr.P(u1, y, d1)), (fr.P(u0, y, d0), fr.P(u0, y, d1)), (fr.P(u1, y, d0), fr.P(u1, y, d1))]
        for (p, q) in rails:
            for hh in (1.0, .5):
                sk.cylinder(g, (p[0], p[1] + hh, p[2]), (q[0], q[1] + hh, q[2]), .022 if hh == 1.0 else .016, fi, 4)
            L = math.dist(p, q)
            nb = max(2, int(L / .6))       # (board 3: balusters every 0.6 m; pass 2's 0.4 m)
            for j in range(nb + 1):
                t = j / nb
                x = (p[0] + (q[0] - p[0]) * t, p[1], p[2] + (q[2] - p[2]) * t)
                sk.box(g, x[0] - .0175, x[0] + .0175, x[1], x[1] + 1.0, x[2] - .0175, x[2] + .0175, fi, None, 0,
                       skip=('-y', '+y'))
        for x in (u0 + .15, u1 - .15):
            sk.cylinder(g, fr.P(x, y - .85, .03), fr.P(x, y - .06, d1 - .08), .025, fi, 4)
        if k < storeys - 1:
            left = (k % 2 == 1)
            ya, yb = y, storey_floor(k + 1)
            xa, xb = (u0 + .45, u0 + .45 + run) if left else (u1 - .45, u1 - .45 - run)
            ca, cbb = .58, 1.06
            for cc in (ca, cbb):
                sk.cylinder(g, fr.P(xa, ya, cc), fr.P(xb, yb, cc), .03, fi, 4)
            for j in range(1, 9):          # (board 3: eight treads a flight; pass 2's eleven)
                t = j / 9
                x = xa + (xb - xa) * t
                yy = ya + (yb - ya) * t
                w = .26 * (1 if xb > xa else -1)
                fbox(g, T, fr, min(x, x + w), max(x, x + w), yy - .025, yy, ca, cbb, fi, 0, skip=('-z',))
            sk.cylinder(g, fr.P(xa, ya + .95, cbb), fr.P(xb, yb + .95, cbb), .02, fi, 4)
    y = storey_floor(1)
    for cc in (.85, 1.05):
        sk.cylinder(g, fr.P(u1 - .7, 3.2, cc), fr.P(u1 - .7, y + 1.0, cc), .02, fi, 4)
    yy = 3.35
    while yy < y + .9:
        sk.cylinder(g, fr.P(u1 - .7, yy, .85), fr.P(u1 - .7, yy, 1.05), .012, fi, 4, caps=False)
        yy += .3


def roof_kit(B, rf, x0, z0, seed, stair_house=True):
    """The tenement's roof kit (bible 5.3): a stair house with its door and caged lamp (left out where the stair tower's own
    stair house stands, pass 2), a water tank on legs, mushroom vents, TV aerials and a hatch. rf: a horizontal frame on the
    roof (a = x, b = z, n = +y)."""
    g, T = B.g, B.T
    r = rng('roofkit', seed)
    y = rf.o[1]
    if stair_house:
        sx0, sx1, sz0, sz1 = x0 + 2.6, x0 + 5.4, z0 - 4.0, z0 - 1.2
        sf = Frame((sx0, y, sz1), (1.0, 0.0, 0.0))       # its front, facing +z
        h = Hole(.9, 1.85, 0.0, 2.1, 0.0, .12)
        wall(g, T, sf, 0.0, sx1 - sx0, 0.0, 2.8, F['brick-red'], [h], base=y)
        reveal(g, T, sf, h, F['brick-red'], sill=False)
        panel_door(g, T, sf, h.u0, h.u1, 0.0, h.v1, -.1, F['paint-oxblood'], r, panels=4, knocker=False)
        for ff, wdt in ((Frame((sx1, y, sz0), (-1.0, 0.0, 0.0)), sx1 - sx0),
                        (Frame((sx1, y, sz1), (0.0, 0.0, -1.0)), sz1 - sz0),
                        (Frame((sx0, y, sz0), (0.0, 0.0, 1.0)), sz1 - sz0)):
            wall(g, T, ff, 0.0, wdt, 0.0, 2.8, F['brick-red'], base=y)
        ck.bx(g, T, sx0 - .12, sx1 + .12, y + 2.8, y + 3.0, sz0 - .12, sz1 + .12, F['concrete'], .02)
        lp = sf.P((h.u0 + h.u1) / 2, 2.35, .12)
        sk.box(g, lp[0] - .1, lp[0] + .1, lp[1] - .07, lp[1] + .07, lp[2] - .1, lp[2] + .02, F['lit-cold'], None, 0)
        if T.detail:
            for i in range(3):
                x = lp[0] - .08 + i * .08
                sk.box(g, x - .008, x + .008, lp[1] - .08, lp[1] + .08, lp[2] - .1, lp[2] + .05, F['iron-black'], None, 0)
        B.light((lp[0], lp[1] - .1, lp[2] + .4), 0xDCEBFF, 5.0, 'bulkhead')
    # the water tank on legs (a block: its stand is the collider)
    tx, tz = x0 + 14.5, z0 - 6.2
    for (dx, dz) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        ck.bx(g, T, tx + dx * .95 - .07, tx + dx * .95 + .07, y, y + 2.6, tz + dz * .95 - .07, tz + dz * .95 + .07, F['iron-raw'],
              .01, skip=('-y',))
    for (yy, w) in (((y + 1.2, .95), (y + 2.5, 1.0)) if T.lod == 0 else ((y + 2.5, 1.0),)):
        for (a, b, c, d) in ((-w, w, -w, -w), (-w, w, w, w), (-w, -w, -w, w), (w, w, -w, w)):
            sk.box(g, tx + min(a, b) - .04, tx + max(a, b) + .04, yy - .05, yy + .05, tz + min(c, d) - .04, tz + max(c, d) + .04,
                   F['iron-raw'], None, 0)
    if T.detail:
        for (a, b) in (((-1, -1), (1, 1)), ((1, -1), (-1, 1))):
            sk.cylinder(g, (tx + a[0] * .95, y + .1, tz + a[1] * .95), (tx + b[0] * .95, y + 2.4, tz + b[1] * .95), .02,
                        F['iron-raw'], 4)
    base = (tx, y + 2.6, tz)
    sides = T.round + 2 if T.lod == 0 else 6
    sk.lathe(g, [(1.15, 0.0), (1.15, 2.1)], base, F['timber-old'], sides, cap0=True)
    if T.detail:
        for hh in (.25, .9, 1.6):
            sk.lathe(g, [(1.165, hh - .04), (1.18, hh), (1.165, hh + .04)], base, F['iron-black'], sides)
    # the conical lid, its overhang closed underneath (pass 1 left it open: seen from the street, its inside showed)
    sk.lathe(g, [(1.15, 2.1), (1.25, 2.1)], base, F['tar-felt'], sides, smooth=False)
    sk.lathe(g, [(1.25, 2.1), (1.25, 2.16), (.06, 2.7), (0.0, 2.72)] if T.lod == 0 else [(1.25, 2.1), (0.0, 2.72)], base,
             F['tar-felt'], sides, smooth=False)
    # vents and aerials (LOD1: the vents' caps only; aerials are below the far read)
    for (vx, vz) in ((x0 + 9.0, z0 - 2.0), (x0 + 11.5, z0 - 7.5), (x0 + 18.0, z0 - 3.0)):
        sk.lathe(g, [(.12, 0.0), (.12, .7), (.32, .78), (.3, .9), (.0, 1.0)] if T.lod == 0 else [(.3, .7), (0.0, 1.0)],
                 (vx, y, vz), F['galv'], T.small + 2 if T.lod == 0 else 4, smooth=False)
    for (ax, az, hh) in ((x0 + 7.0, z0 - 8.4, 3.4), (x0 + 16.5, z0 - 1.8, 2.8)):
        if T.lod > 0:
            continue
        sk.cylinder(g, (ax, y, az), (ax, y + hh, az), .025, F['galv'], 4)
        sk.cylinder(g, (ax - .9, y + hh - .2, az), (ax + .9, y + hh - .2, az), .015, F['galv'], 4)
        for i in range(6):
            x = ax - .8 + i * .32
            sk.cylinder(g, (x, y + hh - .2, az - .45), (x, y + hh - .2, az + .45), .009, F['galv'], 4)
    ck.bx(g, T, x0 + 12.0, x0 + 12.9, y, y + .45, z0 - 2.9, z0 - 2.0, F['galv'], .02)


def tenement_block(B, seed, escape='east', back_faces=True, ends=(True, True), district=None):
    """The tenement block (bible 5.3): 20 x 10 m, six storeys (19 m) behind a parapet to 20.4 m; the entry face (with its
    stair tower, pass 2) and a face with shops on the street (+z), the fire escape on the east or west end, the back court's
    lean faces behind (pass 2); the corbel table, cornice and parapet run round it; the flat roof with its kit. ends: draw
    the end faces (False where the next block abuts: a party wall nobody sees)."""
    g, T = B.g, B.T
    if district:
        B.district = district
    H = storey_floor(TEN_STOREYS)
    front = Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0))
    tenement_face(B, front, seed, 'entry', mouldings=False)
    tenement_face(B, front.shifted(4 * BAY), seed + 1, 'plain', mouldings=False, ground='shops')
    east = Frame((20.0, 0.0, 0.0), (0.0, 0.0, -1.0))
    west = Frame((0.0, 0.0, -10.0), (0.0, 0.0, 1.0))
    if ends[1]:
        tenement_face(B, east, seed + 2, 'escape' if escape == 'east' else 'plain', mouldings=False)
        if escape == 'east':
            fire_escape(B, east)
    if ends[0]:
        tenement_face(B, west, seed + 3, 'escape' if escape == 'west' else 'plain', mouldings=False)
        if escape == 'west':
            fire_escape(B, west)
    back = Frame((20.0, 0.0, -10.0), (-1.0, 0.0, 0.0))
    if back_faces:
        tenement_face(B, back, seed + 4, 'back', mouldings=False)
        tenement_face(B, back.shifted(4 * BAY), seed + 5, 'back', mouldings=False)
    pts = [(0.0, 0.0), (20.0, 0.0), (20.0, -10.0), (0.0, -10.0)]
    outs = [(0.0, 1.0), (1.0, 0.0), (0.0, -1.0), (-1.0, 0.0)]
    cornice_block(B, pts, H, closed=True, outs=outs)
    # quoins up the street corners only (pass 2: the back court's corners go plain), in chunkier .4 m courses
    fd = F['stone-dress']
    if T.detail:
        if ends[0]:
            quoins(g, T, front, 0.0, .9, H, fd, into=1, course=.4)
            quoins(g, T, west, 10.0, .9, H, fd, into=-1, course=.4)
        if ends[1]:
            quoins(g, T, front, 20.0, .9, H, fd, into=-1, course=.4)
            quoins(g, T, east, 0.0, .9, H, fd, into=1, course=.4)
    # the parapet: outer faces, inner faces, coping (the stair tower stands through the front's)
    RH = H + .8          # the roof: a 1.1 m parapet over it
    for fr, w, tw in ((front, 20.0, True), (east, 10.0, False), (west, 10.0, False), (back, 20.0, False)):
        holes = [Hole(TOWER_SPAN[0], TOWER_SPAN[1], H, H + 2.0, keep=True)] if tw else []
        wall(g, T, fr, 0.0, w, H + 1.25, H + 1.9, F['brick-red'], holes, base=0.0)
        # the inner faces .30 in: under the coping (which reaches .32 in, mitred at the corners), no slot between
        inner = Frame(fr.P(w - .30, 0.0, -.30), fr.mu)
        ih = [Hole(w - .30 - TOWER_SPAN[1], w - .30 - TOWER_SPAN[0], RH - .1, H + 2.0, keep=True)] if tw else []
        wall(g, T, inner, 0.0, w - .60, RH, H + 1.9, F['brick-red'], ih, base=0.0)
    loop = [(-.04, H + 1.9, .04), (20.04, H + 1.9, .04), (20.04, H + 1.9, -10.04), (-.04, H + 1.9, -10.04)]
    ck.sweep(g, COPING, loop, fd, closed=True, up=(0, 1, 0),
             smooth_u=False)
    # the roof
    rf = Frame((0.0, RH, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0))
    prism(g, rf, [(0.0, 0.0), (20.0, 0.0), (20.0, 10.0), (0.0, 10.0)], 0.0, .02, F['tar-felt'], caps=(False, True))
    roof_kit(B, Frame((0.0, RH + .02, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0)), 0.0, 0.0, seed, stair_house=False)
    # stacks on the side walls
    r = rng('tstack', seed)
    for (fx, z) in ((.3, -3.0), (19.7, -7.0)):
        ck.bx(g, T, fx - .4, fx + .4, RH, RH + 2.6, z - .9, z + .9, F['brick-red'], .02)
        ck.bx(g, T, fx - .46, fx + .46, RH + 2.6, RH + 2.75, z - .96, z + .96, F['brick-buff'], .012)
        for i in range(4):
            chimney_pot(g, T, (fx, RH + 2.75, z - .6 + i * .4), F['terracotta'], r.uniform(.4, .65), .1)


def xform(M, p):
    """A point through a 3x4 affine (rows)."""
    return (M[0] * p[0] + M[1] * p[1] + M[2] * p[2] + M[3], M[4] * p[0] + M[5] * p[1] + M[6] * p[2] + M[7],
            M[8] * p[0] + M[9] * p[1] + M[10] * p[2] + M[11])


def washing_line(B, p0, p1, seed, sag=.35):
    """A washing line strung across a tenement street above 3.1 m (overhead art, bible 5.3): a 3 cm line (the chunk factor)
    sagging between two wall plates, sheets, shirts and towels pegged along it, hanging still (no wind at 6:47). Board 2:
    the pieces are shaped (a sheet with a dipping hem, a towel, a shirt hung by its tail with its sleeves, trousers by the
    cuffs) and stay at LOD1 as the same few faces (bible 2.5 puts washing in the skyline at 40-150 m); the line and its plates
    are LOD0 only."""
    g, T = B.g, B.T
    r = rng('washing', seed)
    if T.lod == 0:
        n = 10
        pts = []
        for i in range(n + 1):
            t = i / n
            pts.append((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - sag * 4 * t * (1 - t), p0[2] + (p1[2] - p0[2]) * t))
        ck.tube(g, pts, .015, F['galv'], 4, caps=False)
        for p in (p0, p1):
            sk.box(g, p[0] - .06, p[0] + .06, p[1] - .06, p[1] + .06, p[2] - .06, p[2] + .06, F['iron-black'], None, 0)
    L = math.dist(p0, p1)
    d = ((p1[0] - p0[0]) / L, 0.0, (p1[2] - p0[2]) / L)
    nrm = (-d[2], 0.0, d[0])
    t = r.uniform(.1, .22)
    cloths = ('canvas-cream', 'blind', 'curtain-a', 'curtain-b', 'curtain-c', 'paint-white', 'canvas-red', 'paint-white')

    def along(a, dy=0.0):
        return (p0[0] + (p1[0] - p0[0]) * a, p0[1] + (p1[1] - p0[1]) * a - sag * 4 * a * (1 - a) - .01 + dy,
                p0[2] + (p1[2] - p0[2]) * a)

    def cloth(pts, f):
        """A convex piece of cloth (points in (s, drop): s metres along the line from the piece's start, drop below the line),
        both sides: board 2 shapes them (shirt, trousers, sheet, towel) and keeps them at LOD1 as the same few faces."""
        P = []
        for (s_, dy) in pts:
            a_ = t + s_ / L
            q_ = along(a_)
            P.append((q_[0], q_[1] - dy, q_[2]))
        g.face(P, f, nrm)
        g.face(P[::-1], f, (-nrm[0], 0.0, -nrm[2]))

    while t < .88:
        kind = r.choice(('sheet', 'shirt', 'shirt', 'trousers', 'towel', 'sheet'))
        wid = {'sheet': r.uniform(1.2, 1.8), 'shirt': r.uniform(.62, .72), 'trousers': r.uniform(.42, .5),
               'towel': r.uniform(.45, .6)}[kind]
        if t + wid / L > .92:
            break
        f = F[r.choice(cloths)]
        if kind == 'sheet':
            hgt = r.uniform(1.0, 1.45)
            # its hem dips at the middle, where it hangs heaviest
            cloth([(0.0, 0.0), (wid, 0.0), (wid, hgt * .96), (wid / 2, hgt), (0.0, hgt * .96)], f)
        elif kind == 'towel':
            hgt = r.uniform(.5, .7)
            cloth([(0.0, 0.0), (wid, 0.0), (wid, hgt), (0.0, hgt)], f)
        elif kind == 'shirt':
            # pegged by its tail, upside down: the body, then the sleeves hanging from its far end
            bw, bh = wid * .62, r.uniform(.62, .75)
            x0 = (wid - bw) / 2
            cloth([(x0, 0.0), (x0 + bw, 0.0), (x0 + bw, bh), (x0, bh)], f)
            cloth([(x0, bh * .62), (x0, bh), (x0 - .19, bh * .95), (x0 - .16, bh * .7)], f)
            cloth([(x0 + bw, bh * .62), (x0 + bw + .16, bh * .7), (x0 + bw + .19, bh * .95), (x0 + bw, bh)], f)
        else:
            # trousers pegged at the cuffs: two legs joined at the seat
            lh, lw = r.uniform(.72, .9), wid * .42
            cloth([(0.0, 0.0), (lw, 0.0), (lw + .02, lh * .78), (0.0, lh * .78)], f)
            cloth([(wid - lw, 0.0), (wid, 0.0), (wid, lh * .78), (wid - lw - .02, lh * .78)], f)
            cloth([(0.0, lh * .78), (wid, lh * .78), (wid, lh), (0.0, lh)], f)
        t = t + wid / L + r.uniform(.04, .16)


def tenement_street(B, seed, length_blocks=2, district=None):
    """A tenement street (bible 5.3, SB-07's canyon): a 10 m street between two rows of blocks facing each other, each row
    `length_blocks` blocks of 20 m abutting (their party walls left out), the back courts' faces behind, washing lines
    across the street above 3.1 m. The street runs along +x; the south row's fronts face +z at z = 0, the north row's face
    -z at z = 10. Used for the chunk budget (bible 10.3: a 32 m chunk at 60,000 / 7,000)."""
    g, T = B.g, B.T
    for side in (0, 1):
        for i in range(length_blocks):
            sub = Build(B.name, T)
            sub.district = district
            # the street's two ends get the end faces (and the fire escapes); between blocks the party walls are left out.
            # The north row is turned round, so its blocks' own west and east swap.
            first, last = i == 0, i == length_blocks - 1
            ends = (first, last) if side == 0 else (last, first)
            esc = ('west' if first else 'east') if side == 0 else ('east' if first else 'west')
            tenement_block(sub, seed + 97 * side + 13 * i, escape=esc, ends=ends, district=district)
            if side == 0:
                M = sk.affine((0, 0, 0, 1), (20.0 * i, 0.0, 0.0))
            else:
                M = sk.affine(sk.quat_axis(0, 1, 0, math.pi), (20.0 * (i + 1), 0.0, 10.0))
            g.extend(sub.g.transformed(M))
            B.streaks.extend(sub.streaks.transformed(M))
            for (p, hexcol, rad, kind) in sub.lights:
                B.lights.append((xform(M, p), hexcol, rad, kind))
            B.windows.extend(sub.windows)
    r = rng('lines', seed)
    x = 3.0
    while x < 20.0 * length_blocks - 3:
        y0 = r.uniform(5.5, 12.0)
        washing_line(B, (x, y0, .05), (x + r.uniform(-1.5, 1.5), y0 + r.uniform(-.6, .6), 9.95), seed * 7 + int(x))
        x += r.uniform(6.0, 11.0)


# ---------------------------------------------------------------------------------------------------------------------------
# City Hall's clocktower (bible 5.5), stopped at 6:47
# ---------------------------------------------------------------------------------------------------------------------------
MINUTE_ANGLE = math.radians(282.0)     # the minute hand on the 47th minute
HOUR_ANGLE = math.radians(203.5)       # the hour hand nearly four-fifths of the way from VI to VII
NUMERALS = ('XII', 'I', 'II', 'III', 'IIII', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI')


def numeral_strokes(s, h):
    """A Roman numeral's strokes as convex polygons in its own frame (x right, y up; centred, height h)."""
    w_i, w_v, gap, sw = .22 * h, .62 * h, .1 * h, .14 * h
    widths = [w_i if ch == 'I' else w_v for ch in s]
    W = sum(widths) + gap * (len(s) - 1)
    x = -W / 2
    polys = []
    for ch, w in zip(s, widths):
        if ch == 'I':
            xm = x + w / 2
            polys.append([(xm - sw / 2, -h / 2), (xm + sw / 2, -h / 2), (xm + sw / 2, h / 2), (xm - sw / 2, h / 2)])
            polys.append([(x, h / 2 - .1 * h), (x + w, h / 2 - .1 * h), (x + w, h / 2), (x, h / 2)])
            polys.append([(x, -h / 2), (x + w, -h / 2), (x + w, -h / 2 + .1 * h), (x, -h / 2 + .1 * h)])
        elif ch == 'V':
            xm = x + w / 2
            polys.append([(x, h / 2), (x + sw, h / 2), (xm + sw * .3, -h / 2), (xm - sw * .4, -h / 2)])
            polys.append([(x + w - sw * .6, h / 2), (x + w, h / 2), (xm + sw * .3, -h / 2), (xm - sw * .2, -h / 2)])
        elif ch == 'X':
            polys.append([(x, h / 2), (x + sw, h / 2), (x + w, -h / 2), (x + w - sw, -h / 2)])
            polys.append([(x + w - sw * .7, h / 2), (x + w, h / 2), (x + sw * .7, -h / 2), (x, -h / 2)])
        x += w + gap
    return polys


def dial(B, fr, uc, vc, Rd=3.0):
    """A 6 m opal dial (always lit, #F2E6C6) in the face fr centred at (uc, vc): a moulded stone bezel, the gilt chapter
    rings and minute track, radial Roman numerals and the two hands at 6:47 (bible 1.3: the minute hand on the 47th minute,
    the hour hand nearly four-fifths from VI to VII). Read from the street: the reader's right is -u."""
    g, T = B.g, B.T
    # (pass 2: LOD1 at 12 segments; beyond 38 m the dial reads as its lit disc. Board 3: LOD0 at 36, not 48, which pays for
    # City Hall's banded columns and urns under its 55,000 cap; at the tower's height the facets do not read)
    n = T.arc * (3 if T.lod == 0 else 2)
    centre = fr.P(uc, vc, 0.0)
    cd = -.14     # the dial's plane, recessed in the bezel
    # bezel (outer to inner, so its faces look out)
    bez = [(Rd + .32, 0.0), (Rd + .26, .11), (Rd + .12, .14), (Rd, .06), (Rd, cd)] if T.lod == 0 else \
        [(Rd + .32, 0.0), (Rd + .1, .14), (Rd, cd)]
    sk.lathe(g, bez, centre, F['stone-civic'], n, axis=fr.n)
    # the opal disc
    D = Frame(fr.P(uc, vc, cd), fr.u)
    ring = [(Rd * math.cos(2 * math.pi * i / n), Rd * math.sin(2 * math.pi * i / n)) for i in range(n)]
    for i in range(n):
        a, b = ring[i], ring[(i + 1) % n]
        g.face((D.P(0.0, 0.0), D.P(a[0], a[1]), D.P(b[0], b[1])), F['clock-opal'], D.n)
    # the gilt rings and minute track
    def annulus(r0, r1, c, f, segs):
        for i in range(segs):
            a0 = 2 * math.pi * i / segs
            a1 = 2 * math.pi * (i + 1) / segs
            g.face((D.P(r0 * math.cos(a0), r0 * math.sin(a0), c), D.P(r1 * math.cos(a0), r1 * math.sin(a0), c),
                    D.P(r1 * math.cos(a1), r1 * math.sin(a1), c), D.P(r0 * math.cos(a1), r0 * math.sin(a1), c)), f, D.n)
    annulus(Rd * .93, Rd * .955, .012, F['gilt'], n)
    if T.detail:
        annulus(Rd * .8, Rd * .815, .012, F['gilt'], n)
    # minute marks and hour batons
    for m in range(60):
        th = 2 * math.pi * m / 60
        big = m % 5 == 0
        if not big and not T.detail:
            continue
        r0, r1 = (Rd * .8, Rd * .93) if big else (Rd * .87, Rd * .93)
        w = .07 if big else .025
        rad = (-math.sin(th), math.cos(th))        # frame (a, b): the reader's right is -u
        tan = (-math.cos(th), -math.sin(th))
        pts = [(rad[0] * rr + tan[0] * ww, rad[1] * rr + tan[1] * ww) for (rr, ww) in ((r0, -w), (r0, w), (r1, w), (r1, -w))]
        g.face([D.P(a, b, .014) for a, b in pts], F['gilt'], D.n)
    # the numerals, radial (their feet toward the centre)
    if T.detail:
        hgt = .62
        for k, s in enumerate(NUMERALS):
            th = 2 * math.pi * k / 12
            rad = (-math.sin(th), math.cos(th))
            tan = (-math.cos(th), -math.sin(th))
            rc = Rd * .66
            for j, poly in enumerate(numeral_strokes(s, hgt)):
                pts = [(rad[0] * (rc + y) + tan[0] * x, rad[1] * (rc + y) + tan[1] * x) for x, y in poly]
                prism(g, D, pts, .01, .03 + .002 * j, F['gilt'], caps=(False, True))
    # the hands at 6:47 (hour below, minute above), with counterweights and the boss
    def hand(theta, polys, c0, c1):
        rad = (-math.sin(theta), math.cos(theta))
        tan = (-math.cos(theta), -math.sin(theta))
        for poly in polys:
            pts = [(rad[0] * y + tan[0] * x, rad[1] * y + tan[1] * x) for x, y in poly]
            prism(g, D, pts, c0, c1, F['gilt'], caps=(False, True))
    s = Rd / 3.0
    hand(HOUR_ANGLE, [[(-.13 * s, -.5 * s), (.13 * s, -.5 * s), (.1 * s, 1.25 * s), (-.1 * s, 1.25 * s)],
                      [(-.1 * s, 1.25 * s), (.1 * s, 1.25 * s), (.24 * s, 1.52 * s), (0.0, 1.95 * s), (-.24 * s, 1.52 * s)]],
         .04, .08)
    hand(MINUTE_ANGLE, [[(-.1 * s, -.7 * s), (.1 * s, -.7 * s), (.07 * s, 2.25 * s), (0.0, 2.7 * s), (-.07 * s, 2.25 * s)]],
         .09, .13)
    if T.detail:
        for th, rr in ((HOUR_ANGLE, .5), (MINUTE_ANGLE, .7)):
            rad = (-math.sin(th), math.cos(th))
            p = D.P(-rad[0] * rr * s, -rad[1] * rr * s, .1)
            sk.lathe(g, [(.16 * s, -.03), (.16 * s, .03), (0.0, .05)], p, F['gilt'], T.small, axis=D.n)
    sk.lathe(g, [(.22, 0.0), (.22, .16), (.12, .2), (0.0, .22)], D.P(0.0, 0.0, 0.0), F['gilt'], T.round, axis=D.n)


def tower_faces(half, y=0.0):
    """The four faces of a square tower centred on the origin: (frame, width), each frame at its face's left foot."""
    out = []
    for (nx, nz) in ((0.0, 1.0), (1.0, 0.0), (0.0, -1.0), (-1.0, 0.0)):
        u = (nz, 0.0, -nx)
        o = (nx * half - u[0] * half, y, nz * half - u[2] * half)
        out.append((Frame(o, u), 2 * half))
    return out


def rusticate(B, fr, w, v0, v1, holes, f, course=.6, proud=.045):
    """Channelled rustication: each course a band standing `proud` with chamfered joints, stopping at the openings."""
    g, T = B.g, B.T
    if not T.detail:
        return
    v = v0
    while v < v1 - .1:
        top = min(v1, v + course)
        a, b = v + .035, top - .035
        spans = [(0.0, w)]
        for h in holes:
            hb = (h.u0 - .5, h.u1 + .5, h.v0, h.top_v + .7)
            if hb[2] < top and hb[3] > v:
                out = []
                for (s0, s1) in spans:
                    if hb[1] <= s0 or hb[0] >= s1:
                        out.append((s0, s1))
                        continue
                    if hb[0] > s0:
                        out.append((s0, hb[0]))
                    if hb[1] < s1:
                        out.append((hb[1], s1))
                spans = out
        for (s0, s1) in spans:
            if s1 - s0 < .2:
                continue
            fquad(g, fr, s0, a, s1, b, proud, f)
            g.face((fr.P(s0, b, proud), fr.P(s1, b, proud), fr.P(s1, top, 0.0), fr.P(s0, top, 0.0)), f, fr.D(0, 1, 1))
            g.face((fr.P(s0, v, 0.0), fr.P(s1, v, 0.0), fr.P(s1, a, proud), fr.P(s0, a, proud)), f, fr.D(0, -1, 1))
            for (x, nrm) in ((s0, fr.mu), (s1, fr.u)):
                g.face((fr.P(x, a, 0.0), fr.P(x, b, 0.0), fr.P(x, b, proud), fr.P(x, a, proud)), f, nrm)
        v = top


def baluster(g, T, p, h, f):
    s = h / .75
    prof = [(.1 * s, 0.0), (.06 * s, .12 * s), (.11 * s, .3 * s), (.05 * s, .6 * s), (.09 * s, .66 * s), (.09 * s, .75 * s)]
    if T.lod > 0:
        prof = [prof[0], prof[3], prof[5], prof[7]]
    sk.lathe(g, prof, p, f, 6 if T.lod == 0 else 4)


def clocktower(B, seed=0, base=True, hidden_below=0.0):
    """City Hall's clocktower (bible 5.5), 62 m over its court: a granite plinth; a rusticated base with the great arched
    door; a shaft in three stages; the clock stage with four 6 m opal dials at 6:47 between chunky engaged columns under a
    deep entablature ('toon Baroque'); the belfry with louvred arches and a balustrade; an octagonal cupola under a copper
    dome; a lantern, a spire and the gilt sun vane.

    Pass 2: base False stands the tower in City Hall's central block (no plinth or rusticated base, nothing of the shaft
    below `hidden_below`, the hall's roof). The top is richer, for the skyline and SB-02: obelisk pinnacles with gilt balls on
    the clock stage's corners; flaming urns on the belfry's balustrade; four scroll buttresses from the balustrade's
    platform up the cupola's drum; engaged columns round the drum; gilt ribs and four lit lucarnes on the copper dome; an
    open lantern lit warm; a red aircraft light; a bigger gilt sun vane. The belfry's louvres are slats with two sides
    (pass 1's were single faces turned up: from the street, below them, their backs showed)."""
    g, T = B.g, B.T
    r = rng('tower', seed)
    stone, civic, rust = F['stone-dress'], F['stone-civic'], F['stone-rust']
    sq = lambda hw: [(-hw, hw), (hw, hw), (hw, -hw), (-hw, -hw)]
    sqo = [(0.0, 1.0), (1.0, 0.0), (0.0, -1.0), (-1.0, 0.0)]
    if base:
        ck.bx(g, T, -5.3, 5.3, 0.0, .9, -5.3, 5.3, F['granite'], .04)
        plan_mould(g, T, sq(5.3), .9, mould_lod(T, [(0.0, 0.0), (0.0, .04), (-.15, .14), (-.3, .14)]), F['granite'], sqo,
                   closed=True)
        for i, (fr, w) in enumerate(tower_faces(5.0)):
            if i == 0:
                hs = [Hole(3.0, 7.0, 1.0, 6.0, 2.0, .8)]
            else:
                hs = [Hole(2.1, 3.7, 4.2, 7.6, .8, .45), Hole(6.3, 7.9, 4.2, 7.6, .8, .45)]
            wall(g, T, fr, 0.0, w, 1.0, 11.4, rust, hs, base=0.0)
            rusticate(B, fr, w, 1.0, 11.2, hs, rust)
            for h in hs:
                reveal(g, T, fr, h, stone, sill=(i != 0))
                arch_ring(g, T, fr, h, stone, .55 if i == 0 else .32, .1, key=stone)
                if i == 0:
                    for (a, b) in ((h.u0 + .1, 5.0), (5.0, h.u1 - .1)):
                        panel_door(g, T, fr, a, b, 1.0, 5.9, -.75, F['paint-oxblood'], r, panels=6, knocker=False)
                    bar(g, T, fr, h.u0, h.u1, 5.86, 6.04, -.8, -.66, F['paint-oxblood'])
                    hh = Hole(h.u0, h.u1, 6.0, 6.0, 2.0, .8)
                    cu = hh.cuts(T)
                    for k in range(len(cu) - 1):
                        a, b = cu[k], cu[k + 1]
                        g.face((fr.P(a, 6.0, -.7), fr.P(b, 6.0, -.7), fr.P(b, hh.top(b), -.7), fr.P(a, hh.top(a), -.7)),
                               F['fanlight'], fr.n)
                    if T.detail:
                        for k in range(1, 8):
                            a = math.pi * k / 8
                            sk.cylinder(g, fr.P(5.0, 6.0, -.68), fr.P(5.0 - 2.0 * math.cos(a), 6.0 + 2.0 * math.sin(a), -.68),
                                        .03, F['iron-black'], 4, caps=False)
                        sk.lathe(g, [(2.0 * .35, -.0), (0.0, .0)], fr.P(5.0, 6.0, -.67), F['iron-black'], T.small, axis=fr.n)
                    B.light(fr.P(5.0, 4.0, 1.5), 0xFFC27A, 6.0, 'door')
                else:
                    sill(g, T, fr, h.u0, h.u1, h.v0, stone)
                    sash_window(g, T, fr, h, 'dark' if rng('towerwin', seed, ck.key_of(fr, h.u0, h.v0)).random() < .7
                                else 'lit', r, F['paint-white'])
        plan_mould(g, T, sq(5.0), 11.3, mould_lod(T, ck.P_CORNICE_BIG), stone, sqo, closed=True)
        # the base's top, flush with its walls (pass 1 inset it .25 m, leaving a slot down into the hollow base)
        ck.bx(g, T, -5.0, 5.0, 11.4, 12.6, -5.0, 5.0, stone, .03, skip=('-y',))
    # the shaft, 12.6 to 30.0 (from the hall's roof up when it stands in the hall)
    s0 = max(12.6, hidden_below)
    shaft = tower_faces(4.3)
    for fr, w in shaft:
        hs = [Hole(3.1, 4.0, 16.0, 23.4, .45, .35), Hole(4.6, 5.5, 16.0, 23.4, .45, .35)]
        wall(g, T, fr, 0.0, w, s0, 29.4, civic, [h for h in hs if h.v0 >= s0], base=0.0)
        for h in hs:
            if h.v0 < s0:
                continue
            reveal(g, T, fr, h, stone)
            sill(g, T, fr, h.u0, h.u1, h.v0, stone)
            arch_ring(g, T, fr, h, stone, .2, .06)
            back_fill(g, T, fr, h, -.3, F['glass-dark'])
            if T.detail:
                for y in (18.0, 20.0, 22.0):
                    bar(g, T, fr, h.u0, h.u1, y - .025, y + .025, -.3, -.26, F['iron-black'])
            B.streaks.add_under(fr, h.u0 - .1, h.u1 + .1, h.v0 - .1, 2.5, .35)
        if T.detail:
            for (a, b) in ((0.0, .9), (w - .9, w)):
                v = s0
                k = 0
                while v < 29.3:
                    top = min(29.4, v + .6)
                    aa, bb = (a, b - (.35 if k % 2 else 0)) if a == 0 else (a + (.35 if k % 2 else 0), b)
                    fbox(g, T, fr, aa, bb, v + .03, top - .03, 0.0, .1, rust, 0, skip=('-z',))
                    v = top
                    k += 1
            oc = fr.P(w / 2, 26.6, 0.0)
            sk.lathe(g, [(1.0, 0.0), (1.0, .08), (.82, .12), (.7, .05), (.7, -.2)], oc, stone, T.arc * 2, axis=fr.n)
            sk.lathe(g, [(.7, -.2), (0.0, -.2)], oc, F['glass-dark'], T.arc * 2, axis=fr.n)
            for (a, b) in ((1.25, 2.95), (w - 2.95, w - 1.25)):
                frame_ring(g, fr, a, b, 25.1, 28.7, .14, 0.0, .06, stone, inner=False)
        smould(g, T, fr, 0.0, w, 24.4, ck.P_STRING, stone)
        if s0 < 16.0:
            smould(g, T, fr, 0.0, w, 15.86, ck.P_STRING, stone)
    plan_mould(g, T, sq(4.3), 29.4, mould_lod(T, ck.P_CORNICE), stone, sqo, closed=True)
    if T.detail:
        for fr, w in shaft:
            for i in range(9):
                console(g, T, fr, .5 + i * (w - 1.0) / 8, 29.42, .14, .34, .32, stone)
    # the clock stage, 30.0 to 40.6
    cs = tower_faces(4.6)
    plan_mould(g, T, sq(4.3), 29.9, [(0.0, 0.0), (.3, .3), (.3, .42), (0.0, .42)], stone, sqo, closed=True)
    for fr, w in cs:
        n = T.arc * (4 if T.lod == 0 else 2)
        uc, vc, Ro = w / 2, 35.0, 3.32
        sqh = 3.6
        wall(g, T, fr, 0.0, w, 30.32, 39.0, civic, [Hole(uc - sqh, uc + sqh, vc - sqh, vc + sqh, keep=True)], base=0.0)
        for i in range(n):
            a0 = 2 * math.pi * i / n
            a1 = 2 * math.pi * (i + 1) / n

            def sqp(a):
                dx, dy = math.cos(a), math.sin(a)
                k = sqh / max(abs(dx), abs(dy))
                return (uc + dx * k, vc + dy * k)
            p0 = (uc + Ro * math.cos(a0), vc + Ro * math.sin(a0))
            p1 = (uc + Ro * math.cos(a1), vc + Ro * math.sin(a1))
            q0, q1 = sqp(a0), sqp(a1)
            g.face((fr.P(*p0), fr.P(*p1), fr.P(*q1), fr.P(*q0)), civic, fr.n)
        dial(B, fr, uc, vc, 3.0)
        if T.detail:
            frame_ring(g, fr, uc - sqh - .3, uc + sqh + .3, vc - sqh - .3, vc + sqh + .3, .3, 0.0, .12, stone, inner=True)
            fbox(g, T, fr, uc - .45, uc + .45, vc + sqh - .3, vc + sqh + .7, .1, .3, stone, .03, skip=('-z',))
            for (du, dv) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = fr.P(uc + du * (sqh - .55), vc + dv * (sqh - .55), 0.0)
                sk.lathe(g, [(.32, 0.0), (.3, .06), (.16, .12), (0.0, .14)], p, F['gilt'], T.round, axis=fr.n)
        else:
            frame_ring(g, fr, uc - sqh - .3, uc + sqh + .3, vc - sqh - .3, vc + sqh + .3, .3, 0.0, .12, stone, inner=False)
    for (sx, sz) in ((1, 1), (1, -1), (-1, -1), (-1, 1)):
        cx, cz = sx * 4.6, sz * 4.6
        ck.bx(g, T, cx - .72, cx + .72, 30.3, 31.0, cz - .72, cz + .72, stone, .03)
        prof = [(.62, 0.0), (.62, .12), (.55, .2), (.55, 7.1), (.6, 7.2)]
        if T.detail:
            prof = [(.62, 0.0), (.62, .12), (.55, .2), (.56, 2.5), (.54, 5.0), (.52, 7.1), (.6, 7.2)]
        sk.lathe(g, prof, (cx, 31.0, cz), stone, T.round + 2 if T.lod == 0 else 6)
        if T.detail:
            for k in range(8):
                y = 31.4 + k * .85
                sk.lathe(g, [(.565, -.06), (.6, 0.0), (.565, .06)], (cx, y, cz), stone, T.round + 2)
        ck.bx(g, T, cx - .78, cx + .78, 38.2, 39.0, cz - .78, cz + .78, stone, .04)
    plan_mould(g, T, sq(4.6), 39.0, mould_lod(T, [(0.0, 0.0), (.14, 0.0), (.14, .32), (.2, .38), (.2, .5), (.12, .56), (.12, 1.0),
                                                     (0.0, 1.0)]), stone, sqo, closed=True)
    plan_mould(g, T, sq(4.6), 40.0, mould_lod(T, [(0.0, 0.0), (.12, 0.0), (.12, .14), (.3, .26), (.3, .36), (.62, .54), (.72, .6),
                                                     (.8, .7), (.8, .86), (0.0, .86)]), stone, sqo, closed=True)
    # pass 2: obelisk pinnacles on the clock stage's corners, a gilt ball on each (the skyline's first step in)
    for (sx, sz) in ((1, 1), (1, -1), (-1, -1), (-1, 1)):
        cx, cz = sx * 4.75, sz * 4.75
        ck.bx(g, T, cx - .5, cx + .5, 40.86, 41.5, cz - .5, cz + .5, stone, .02)
        sk.lathe(g, [(.36, 0.0), (.33, .1), (.1, 2.7), (0.0, 2.72)], (cx, 41.5, cz), stone, 4, smooth=False, phase=math.pi / 4)
        sk.lathe(g, [(0.0, -.02), (.12, .04), (.19, .18), (.12, .33), (0.0, .38)], (cx, 44.2, cz), F['gilt'],
                 T.small + 2 if T.lod == 0 else 5)
    # the belfry, 40.9 to 48.0: louvred arches between paired pilasters
    bel = tower_faces(4.0, 0.0)
    ck.bx(g, T, -4.6, 4.6, 40.86, 41.1, -4.6, 4.6, stone, .02)      # (pass 2: to the clock stage's wall line; no slot)
    for fr, w in bel:
        h = Hole(2.3, 5.7, 42.0, 44.9, 1.7, .9)
        wall(g, T, fr, 0.0, w, 41.1, 47.4, civic, [h], base=0.0)
        reveal(g, T, fr, h, stone, sill=True)
        arch_ring(g, T, fr, h, stone, .3, .08, key=stone)
        back_fill(g, T, fr, h, -.9, F['interior'])
        nl = 11 if T.detail else 4
        for k in range(nl):
            y = h.v0 + .25 + (h.top_v - h.v0 - .5) * k / max(1, nl - 1)
            half = (h.u1 - h.u0) / 2
            uc = (h.u0 + h.u1) / 2
            if y > h.v1:
                dy = y - h.v1
                half = math.sqrt(max(0.0, (1.7 * 1.7) - dy * dy)) * half / 1.7 - .04
            if half < .15:
                continue
            # a slat with two sides, falling outward to shed the rain: its top seen from level, its underside from the
            # street below
            p0, p1 = (uc - half, y + .08, -.66), (uc + half, y + .08, -.66)
            q0, q1 = (uc - half, y - .12, -.32), (uc + half, y - .12, -.32)
            g.face((fr.P(*p0), fr.P(*p1), fr.P(*q1), fr.P(*q0)), F['timber-old'], fr.D(0, .86, .5))
            dn = .03
            g.face((fr.P(p0[0], p0[1] - dn, p0[2]), fr.P(p1[0], p1[1] - dn, p1[2]), fr.P(q1[0], q1[1] - dn, q1[2]),
                    fr.P(q0[0], q0[1] - dn, q0[2])), F['timber-old'], fr.D(0, -.86, -.5))
            g.face((fr.P(q0[0], q0[1] - dn, q0[2]), fr.P(q1[0], q1[1] - dn, q1[2]), fr.P(*q1), fr.P(*q0)), F['timber-old'],
                   fr.D(0, -.5, .86))
        for (a, b) in (((1.05, 1.6), (1.75, 2.2), (5.8, 6.25), (6.4, 6.95)) if T.lod == 0 else ()):
            fbox(g, T, fr, a, b, 41.1, 46.7, 0.0, .18, stone, .02, skip=('-z',))
            fbox(g, T, fr, a - .06, b + .06, 46.7, 47.1, 0.0, .26, stone, .02, skip=('-z',))
    plan_mould(g, T, sq(4.0), 47.2, mould_lod(T, ck.P_CORNICE_BIG), stone, sqo, closed=True)
    # the balustrade with corner pedestals and flaming urns
    y0 = 47.94
    ck.bx(g, T, -4.25, 4.25, y0, y0 + .2, -4.25, 4.25, stone, .02)
    for fr, w in tower_faces(3.95, 0.0):
        n = 11 if T.detail else 0
        for k in range(n):
            u = .9 + (w - 1.8) * (k + .5) / n
            baluster(g, T, fr.P(u, y0 + .2, -.15), .72, stone)
        if not T.detail:
            fbox(g, T, fr, .6, w - .6, y0 + .2, y0 + .92, -.25, -.05, stone, 0)
        fbox(g, T, fr, .5, w - .5, y0 + .92, y0 + 1.1, -.3, .02, stone, .015)
    for (sx, sz) in ((1, 1), (1, -1), (-1, -1), (-1, 1)):
        cx, cz = sx * 3.85, sz * 3.85
        ck.bx(g, T, cx - .42, cx + .42, y0 + .2, y0 + 1.2, cz - .42, cz + .42, stone, .02)
        prof = [(.22, 0.0), (.18, .08), (.3, .28), (.36, .5), (.28, .7), (.14, .78), (.18, .86), (.06, 1.0), (0.0, 1.06)]
        if T.lod > 0:
            prof = [prof[0], prof[3], prof[8]]
        sk.lathe(g, prof, (cx, y0 + 1.2, cz), stone, T.round if T.lod == 0 else 5)
        if T.detail:
            sk.lathe(g, [(.1, 0.0), (.13, .12), (.07, .3), (0.0, .5)], (cx, y0 + 2.24, cz), F['gilt'], T.small)
    # the cupola: the balustrade's platform, scroll buttresses up the drum's diagonals, an octagonal drum with engaged
    # columns and round-headed openings, a cornice ring
    Rc = 2.9
    ph = math.pi / 8
    ck.bx(g, T, -3.25, 3.25, 48.1, 48.82, -3.25, 3.25, stone, .02)
    oc = [(Rc * math.cos(ph + k * math.pi / 4), Rc * math.sin(ph + k * math.pi / 4)) for k in range(8)]
    y0, y1 = 49.1, 52.6
    apo = Rc * math.cos(math.pi / 8)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        d = (math.cos(a), math.sin(a))
        # u outward along the diagonal, the scroll .4 m thick centred on it
        sf = Frame((d[0] * apo + d[1] * .2, 0.0, d[1] * apo - d[0] * .2), (d[0], 0.0, d[1]))
        pts = [(0.0, 48.82)]
        nseg = 6 if T.lod == 0 else 3
        for i in range(nseg + 1):
            t_ = math.pi / 2 * i / nseg
            pts.append((1.35 * math.sin(t_), 48.82 + (y1 - .25 - 48.82) * math.cos(t_)))
        poly = [(0.0, 48.82), (1.35, 48.82)] + [p for p in pts[1:][::-1][1:-1]] + [(0.0, y1 - .25)]
        prism(g, sf, poly, 0.0, .4, stone)
        if T.detail:
            # the scroll's curl at its foot
            sk.lathe(g, [(.22, 0.0), (.22, .42), (0.0, .44)], sf.P(1.18, 49.06, -.01), stone, T.small, axis=sf.n)
            sk.lathe(g, [(.22, 0.0), (0.0, .0)], sf.P(1.18, 49.06, -.01), stone, T.small, axis=sf.mn)
    for k in range(8):
        p, q = oc[(k + 1) % 8], oc[k]
        d = (q[0] - p[0], q[1] - p[1])
        w = math.hypot(*d)
        ff = Frame((p[0], 0.0, p[1]), (d[0], 0.0, d[1]))
        if ff.n[0] * (p[0] + q[0]) + ff.n[2] * (p[1] + q[1]) < 0:
            ff = Frame((q[0], 0.0, q[1]), (-d[0], 0.0, -d[1]))
        if k % 2 == 0:
            wall(g, T, ff, 0.0, w, y0, y1, civic, base=0.0)       # a diagonal bay, blind behind its scroll
            continue
        h = Hole(.62, w - .62, y0 + .7, y0 + 2.3, (w - 1.24) / 2, .3)
        wall(g, T, ff, 0.0, w, y0, y1, civic, [h], base=0.0)
        reveal(g, T, ff, h, stone, sill=True)
        back_fill(g, T, ff, h, -.3, F['interior'] if k % 4 == 1 else F['glass-dark'])
    for (x, z) in oc:
        sk.lathe(g, [(.2, 0.0), (.2, .14), (.15, .22), (.14, 3.1), (.2, 3.2), (.2, 3.35)] if T.lod == 0 else
                 [(.17, 0.0), (.17, 3.35)], (x * 1.02, y0, z * 1.02), stone, 8 if T.lod == 0 else 4)
    plan_mould(g, T, oc, y0 - .3, [(0.0, 0.0), (.25, 0.0), (.25, .3), (0.0, .3)], stone, closed=True)
    plan_mould(g, T, oc, y1, mould_lod(T, [(0.0, 0.0), (.08, 0.0), (.08, .1), (.24, .2), (.24, .32), (.34, .4), (.34, .5),
                                           (0.0, .5)]), stone, closed=True)
    # the copper dome, its gilt ribs, four lucarnes lit warm
    yd = y1 + .5
    prof = [(Rc * 1.05, 0.0), (Rc * 1.05, .12), (Rc * .98, .22), (Rc * .95, .7), (Rc * .82, 1.4), (Rc * .6, 2.1), (Rc * .36, 2.65),
            (Rc * .2, 3.0), (Rc * .13, 3.2)]
    if T.lod > 0:
        prof = [prof[0], prof[2], prof[4], prof[6], prof[8]]
    dirs = [(math.cos(ph + k * math.pi / 4), math.sin(ph + k * math.pi / 4)) for k in range(8)]
    rings = [[(d[0] * rr, yd + hh, d[1] * rr) for d in dirs] for rr, hh in prof]
    for i in range(len(prof) - 1):
        for k in range(8):
            k1 = (k + 1) % 8
            mid = (math.cos(ph + (k + .5) * math.pi / 4), math.sin(ph + (k + .5) * math.pi / 4))
            dr = prof[i + 1][0] - prof[i][0]
            dh = prof[i + 1][1] - prof[i][1]
            g.face((rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k]), F['copper'], (mid[0] * dh, -dr, mid[1] * dh))
    for k in (range(8) if T.lod == 0 else ()):
        ck.tube(g, [rings[i][k] for i in range(len(prof))], .06, F['gilt'], 4, caps=False)
    if T.detail:
        for k in range(4):
            a = k * math.pi / 2
            rr = Rc * .9
            hh = .95
            c = (math.cos(a) * rr, yd + hh, math.sin(a) * rr)
            ax = (math.cos(a) * .82, .57, math.sin(a) * .82)
            sk.lathe(g, [(.38, -.1), (.38, .12), (.3, .18), (.24, .1)], c, F['copper-dark'], T.small + 2, axis=ax)
            sk.lathe(g, [(.24, .1), (0.0, .1)], c, F['lantern'], T.small + 2, axis=ax)
            B.light((c[0] + ax[0] * .6, c[1] + .3, c[2] + ax[2] * .6), 0xFFB054, 2.5, 'lucarne')
    # the lantern: eight colonnettes round a warm-lit core, its cap, the red aircraft light, the spire, the ball, the vane
    yl = yd + prof[-1][1]
    sk.lathe(g, [(.7, 0.0), (.7, .15), (.6, .2)], (0.0, yl, 0.0), stone, T.round)
    for k in range(8 if T.detail else 4):
        a = 2 * math.pi * k / (8 if T.detail else 4)
        sk.cylinder(g, (.55 * math.cos(a), yl + .2, .55 * math.sin(a)), (.55 * math.cos(a), yl + 1.6, .55 * math.sin(a)), .07,
                    stone, T.small, caps=False)
    sk.lathe(g, [(.36, .2), (.36, 1.55), (0.0, 1.56)], (0.0, yl, 0.0), F['lantern'], T.small + 2)
    sk.lathe(g, [(.6, .2), (.36, .2)], (0.0, yl, 0.0), stone, T.small + 2, smooth=False)
    B.light((0.0, yl + .9, 0.0), 0xFFB054, 4.0, 'lantern')
    sk.lathe(g, [(.72, 1.6), (.72, 1.75), (.55, 1.85), (.3, 2.1), (.18, 2.25)], (0.0, yl, 0.0), F['copper'], 8)
    sk.lathe(g, [(0.0, -.06), (.09, -.03), (.11, .05), (.07, .12), (0.0, .14)], (0.0, yl + 2.27, 0.0), F['beacon'], 6)
    sk.lathe(g, [(.18, 2.25), (.12, 3.2), (.05, 4.0), (.03, 4.25)], (0.0, yl, 0.0), F['copper'], 8)
    ys = yl + 4.25
    sk.lathe(g, [(0.0, -.02), (.12, .02), (.2, .12), (.18, .26), (.08, .36), (0.0, .38)], (0.0, ys, 0.0), F['gilt'], T.small + 2)
    yv = ys + .4
    sk.cylinder(g, (0.0, yv, 0.0), (0.0, yv + 1.6, 0.0), .03, F['gilt'], 4)
    # the sun vane: a disc with twelve rays, 2.5 m across, facing the court (+z); the arrow 2 m to the north (-z)
    sun = Frame((0.0, yv + 1.05, .0), (1.0, 0.0, 0.0))
    nn = 12 if T.detail else 6
    R0, R1 = .5, 1.25
    for k in range(nn):
        a0, a1 = 2 * math.pi * k / nn, 2 * math.pi * (k + 1) / nn
        g.face((sun.P(0, 0, 0), sun.P(R0 * math.cos(a0), R0 * math.sin(a0)), sun.P(R0 * math.cos(a1), R0 * math.sin(a1))), F['gilt'], sun.n)
        g.face((sun.P(0, 0, -.006), sun.P(R0 * math.cos(a1), R0 * math.sin(a1), -.006), sun.P(R0 * math.cos(a0), R0 * math.sin(a0), -.006)), F['gilt'], sun.mn)
        am = (a0 + a1) / 2
        tip = (R1 * math.cos(am), R1 * math.sin(am))
        b0 = (R0 * .96 * math.cos(am - .14), R0 * .96 * math.sin(am - .14))
        b1 = (R0 * .96 * math.cos(am + .14), R0 * .96 * math.sin(am + .14))
        g.face((sun.P(*b0), sun.P(*b1), sun.P(*tip)), F['gilt'], sun.n)
        g.face((sun.P(b0[0], b0[1], -.006), sun.P(*tip, -.006), sun.P(b1[0], b1[1], -.006)), F['gilt'], sun.mn)
    arrow = Frame((0.0, yv + .2, 1.0), (0.0, 0.0, -1.0))
    g.face((arrow.P(0.0, -.025), arrow.P(1.8, -.025), arrow.P(1.8, .025), arrow.P(0.0, .025)), F['gilt'], arrow.n)
    g.face((arrow.P(0.0, -.025, -.004), arrow.P(0.0, .025, -.004), arrow.P(1.8, .025, -.004), arrow.P(1.8, -.025, -.004)), F['gilt'], arrow.mn)
    g.face((arrow.P(1.65, -.16), arrow.P(2.1, 0.0), arrow.P(1.65, .16)), F['gilt'], arrow.n)
    g.face((arrow.P(1.65, -.16, -.004), arrow.P(1.65, .16, -.004), arrow.P(2.1, 0.0, -.004)), F['gilt'], arrow.mn)
    # the dials' glow lights the stone round them a little (the board's practical slots)
    for fr, w in cs:
        B.light(fr.P(w / 2, 35.0, 2.5), 0xF2E6C6, 7.0, 'dial')


# ---------------------------------------------------------------------------------------------------------------------------
# City Hall (bible 5.5; pass 2): the final court's building, its portico, steps and domes, round the tower
# ---------------------------------------------------------------------------------------------------------------------------
HALL_FRONT = 0.0       # the central block's front wall (local z); the portico and steps stand in front of it
HALL_BACK = -18.0
HALL_ROOF = 15.0       # the central block's cornice; the tower's shaft rises out of its roof
TOWER_Z = -9.0         # the tower's centre (local z)


def column(g, T, base, h, r, f, drums=4, fb=None):
    """A toon-Baroque column (bible 5.5: chunky drums, block capitals): a square plinth, a torus, the shaft with drum joints
    (rings), a block capital with its abacus. base: the plinth's foot (Unity)."""
    x, y, z = base
    fb = fb or f
    pw = r * 1.35
    sk.box(g, x - pw, x + pw, y, y + .45, z - pw, z + pw, fb, T if T.bevel else None, .02 if T.bevel else 0.0, cell=0)
    prof = [(r * 1.22, .45), (r * 1.22, .55), (r * 1.08, .66), (r, .74), (r * .94, h - 1.05), (r * 1.02, h - .95)]
    if T.lod > 0:
        prof = [(r * 1.18, .45), (r * 1.18, .62), (r, .7), (r * .94, h - .95)]
    sides = T.round + 2 if T.lod == 0 else 6
    sk.lathe(g, prof, (x, y, z), f, sides)
    if T.lod == 0:
        # board 3 (the critique: slender plain columns read Neoclassical, not toon Baroque): banded drums, each a block
        # 0.36 m tall standing 13 % proud of the shaft, in the rusticated stone (fb), the Gibbs way
        for k in range(1, drums):
            yy = y + .74 + (h - 1.8) * k / drums
            rr = r * (1.0 - .06 * k / drums)
            sk.lathe(g, [(rr * .99, -.18), (rr * 1.13, -.14), (rr * 1.13, .14), (rr * .99, .18)], (x, yy, z), fb, sides)
    # the block capital and abacus (LOD1: one block)
    cw = r * 1.3
    if T.lod > 0:
        sk.box(g, x - cw, x + cw, y + h - .95, y + h, z - cw, z + cw, f, None, 0.0, cell=0, skip=('-y',))
        return
    sk.box(g, x - cw, x + cw, y + h - .95, y + h - .35, z - cw, z + cw, f, T if T.bevel else None, .025 if T.bevel else 0.0, cell=0)
    aw = r * 1.5
    sk.box(g, x - aw, x + aw, y + h - .35, y + h, z - aw, z + aw, f, T if T.bevel else None, .02 if T.bevel else 0.0, cell=0)


def crest(B, fr, uc, vc, R, f, fgilt):
    """The city crest carved in the pediment (bible 1.7 rule 4): a clock face over three stepped hills, the hands gilt at
    6:47 (the minute on the 47th minute, the hour four-fifths past VI). Shallow relief in fr's plane."""
    g, T = B.g, B.T
    n = 24 if T.lod == 0 else 12
    sk.lathe(g, [(R, 0.0), (R, .1), (R * .86, .12), (R * .86, .06)], fr.P(uc, vc, 0.0), f, n, axis=fr.n, smooth=False)
    sk.lathe(g, [(R * .86, .06), (0.0, .06)], fr.P(uc, vc, 0.0), F['crest-opal'], n, axis=fr.n, smooth=False)
    # the three stepped hills under the face
    for i, (a, b, hgt) in enumerate(((-R * 1.35, -R * .45, R * .55), (-R * .55, R * .55, R * .85), (R * .45, R * 1.35, R * .55))):
        fbox(g, T, fr, uc + a, uc + b, vc - R - .1, vc - R - .1 + hgt, 0.0, .12 + .02 * (i == 1), f, 0, skip=('-z',))
    if T.lod > 0:
        return
    D_ = Frame(fr.P(uc, vc, .07), fr.u)
    for m in range(12):
        th = 2 * math.pi * m / 12
        rad = (-math.sin(th), math.cos(th))
        tan = (-math.cos(th), -math.sin(th))
        w = .05
        pts = [(rad[0] * rr + tan[0] * ww, rad[1] * rr + tan[1] * ww) for (rr, ww) in ((R * .66, -w), (R * .66, w), (R * .8, w), (R * .8, -w))]
        g.face([D_.P(a, b, .002) for a, b in pts], fgilt, D_.n)
    for (theta, L, w) in ((HOUR_ANGLE, R * .45, .09), (MINUTE_ANGLE, R * .72, .06)):
        rad = (-math.sin(theta), math.cos(theta))
        tan = (-math.cos(theta), -math.sin(theta))
        poly = [(-w, -.08), (w, -.08), (w * .6, L), (0.0, L + .1), (-w * .6, L)]
        prism(g, D_, [(rad[0] * y + tan[0] * x, rad[1] * y + tan[1] * x) for x, y in poly], .004, .03, fgilt, caps=(False, True))


def hall_window(B, fr, h, st, r, kind='hood', f=None):
    """A civic window (bible 5.5): its reveal, sill, an architrave, and over it a cornice hood or a pediment (triangular or
    segmental, alternating: toon Baroque) on consoles; a tall sash."""
    g, T = B.g, B.T
    f = f or F['stone-dress']
    reveal(g, T, fr, h, f)
    sill(g, T, fr, h.u0 - .06, h.u1 + .06, h.v0, f)
    B.streaks.add_under(fr, h.u0 - .2, h.u1 + .2, h.v0 - .1, 1.8, .3)
    if T.detail:
        frame_ring(g, fr, h.u0 - .16, h.u1 + .16, h.v0, h.top_v + .16, .16, 0.0, .05, f, inner=False, sides=(1, 1, 0, 1))
        for (a, nrm) in ((h.u0 - .16, fr.mu), (h.u1 + .16, fr.u)):
            g.face((fr.P(a, h.v0, .05), fr.P(a, h.top_v + .16, .05), fr.P(a, h.top_v + .16, 0.0), fr.P(a, h.v0, 0.0)), f, nrm)
    y = h.top_v + .16
    u0, u1 = h.u0 - .32, h.u1 + .32
    if kind != 'none':
        mould(g, T, fr, u0, u1, y, mould_lod(T, [(0.0, 0.0), (.08, 0.0), (.08, .1), (.2, .18), (.24, .26), (0.0, .26)]), f)
        if T.detail:
            for x in (u0 + .12, u1 - .12):
                console(g, T, fr, x, y, .14, .36, .2, f)
    if kind == 'tri':
        um = (u0 + u1) / 2
        rise = (u1 - u0) * .22
        prism(g, fr, [(u0, y + .26), (u1, y + .26), (um, y + .26 + rise)], 0.0, .22, f)
    elif kind == 'seg' and T.lod == 0:
        um = (u0 + u1) / 2
        L = (u1 - u0) / 2
        pts = [(u0, y + .26)] + [(um - L + 2 * L * i / 6, y + .26 + sk.arch_height(L, L * .38, min(2 * L * i / 6, 2 * L - 2 * L * i / 6))) for i in range(1, 6)] + [(u1, y + .26)]
        prism(g, fr, pts, 0.0, .22, f)
    st.window(B, fr, h, r)


def flood(B, p, aim, lit, f_body=None):
    """A civic flood on a short iron bracket (bible 5.5: the portico's floods are dying, circuit 1): a box head with its
    lens toward `aim` (Unity direction). lit: 1 working, between 0 and 1 dying (its glow and pool scaled)."""
    g, T = B.g, B.T
    f_body = f_body or F['iron-black']
    ax = _n3(aim)
    if T.lod > 0:
        if lit > 0:
            B.light((p[0] + ax[0], p[1] + ax[1], p[2] + ax[2]), 0xFFD9A8, 7.0 * lit, 'flood')
        return
    sk.cylinder(g, (p[0], p[1] - .25, p[2]), p, .04, f_body, 4)
    t = Geo()
    sk.box(t, -.22, .22, -.16, .16, -.18, .1, f_body, None, 0)
    sk.box(t, -.17, .17, -.12, .12, .1, .115, F['flood'] if lit > 0 else F['glass-dark'], None, 0, skip=('-z',))
    # aim: turn +z to ax
    yaw = math.atan2(ax[0], ax[2])
    pitch = -math.asin(max(-1.0, min(1.0, ax[1])))
    q1 = sk.quat_axis(0, 1, 0, yaw)
    q2 = sk.quat_axis(1, 0, 0, pitch)
    qx = _qmul(q1, q2)
    g.extend(t.transformed(sk.affine(qx, p)))
    if lit > 0:
        B.light((p[0] + ax[0] * 1.0, p[1] + ax[1] * 1.0, p[2] + ax[2] * 1.0), 0xFFD9A8, 7.0 * lit, 'flood')
        # its beam up the facade, for the board's point-light sum (the game draws the beam itself: VLB or its own beams)
        B.light((p[0] + ax[0] * 7.5, p[1] + ax[1] * 7.5, p[2] + ax[2] * 7.5), 0xFFD9A8, 7.5 * lit, 'flood-beam')


def _n3(a):
    L = math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]) or 1.0
    return (a[0] / L, a[1] / L, a[2] / L)


def _qmul(a, b):
    ax, ay, az, aw = a
    bx_, by, bz, bw = b
    return (aw * bx_ + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx_,
            aw * bz + ax * by - ay * bx_ + az * bw, aw * bw - ax * bx_ - ay * by - az * bz)


def copper_dome(B, c, R, H, lantern=True, ribs=8):
    """A ribbed copper dome on its drum's cornice at c (Unity, the dome's foot centre): radius R, rise H, gilt ribs, a small
    lantern and a gilt finial (the pavilions' domes; bible 5.5's council chamber dome)."""
    g, T = B.g, B.T
    n = 16 if T.lod == 0 else 8
    prof = [(R, 0.0), (R, .15), (R * .97, H * .3), (R * .86, H * .58), (R * .62, H * .82), (R * .3, H * .97), (R * .18, H)]
    if T.lod > 0:
        prof = [prof[0], prof[2], prof[4], prof[6]]
    sk.lathe(g, prof, c, F['copper'], n, smooth=True)
    if T.detail:
        for k in range(ribs):
            a = 2 * math.pi * k / ribs
            path = [(c[0] + math.cos(a) * rr * 1.01, c[1] + hh, c[2] + math.sin(a) * rr * 1.01) for rr, hh in prof[1:]]
            ck.tube(g, path, .05, F['gilt'], 4, caps=False)
    top = (c[0], c[1] + H, c[2])
    if lantern:
        sk.lathe(g, [(R * .2, 0.0), (R * .2, .9), (R * .26, 1.0), (R * .26, 1.1), (R * .1, 1.5), (0.0, 1.55)], top, F['copper'],
                 8 if T.lod == 0 else 4)
        if T.detail:
            for k in range(4):
                a = math.pi / 4 + k * math.pi / 2
                fquad(g, Frame((top[0] + math.cos(a) * R * .2, top[1], top[2] + math.sin(a) * R * .2),
                               (math.sin(a), 0.0, -math.cos(a))), -.12, .25, .12, .75, .005, F['lantern'])
        sk.lathe(g, [(0.0, -.02), (.08, .05), (.12, .18), (.06, .3), (.03, .7), (0.0, .8)], (top[0], top[1] + 1.55, top[2]),
                 F['gilt'], T.small)


def city_hall(B, seed=0):
    """City Hall (bible 5.5), the final court's building, built round the clocktower (pass 2). Its front faces +z:
    - the central block (18 m wide, its cornice at 15 m and a balustrade over it) holds the tower, whose shaft rises out of
      its roof; its front, inside the portico, has the great arched door with its lit fanlight and two tall windows;
    - the giant-order portico of three pairs of columns (two coupled pairs at its ends and the middle pair framing the door),
      chunky drums and block capitals, a deep entablature lettered MERIDIAN CITY HALL, and a pediment carved with the city
      crest (a clock face over three stepped hills, its gilt hands at 6:47), on a stylobate up eight steps from the court,
      its two floods dying (one flickering);
    - wings of three bays: a rusticated ground storey with arched windows, a piano nobile of tall windows under alternating
      pediments, the cornice and a solid parapet;
    - end pavilions stepping forward, each under a ribbed copper dome with gilt ribs and a lantern (the council chamber's
      to the east, the hall of records' to the west);
    - plain sides and back with windows. Budget (bible 10.3): the hall and tower 55,000 / 7,000."""
    g, T = B.g, B.T
    r = rng('cityhall', seed)
    st = Style(seed * 11 + 3, brick=False)
    st.wall = F['stone-civic']
    st.frame = F['paint-white']
    st.head = 'lintel'
    st.mix.update(lit=.1, board_ground=.12, board_upper=.03)      # the Time Service's hall: kept up longer than the town
    st.lit = .1
    civic, stone, rust = F['stone-civic'], F['stone-dress'], F['stone-rust']
    Z0 = HALL_FRONT
    ZB = HALL_BACK
    HR = HALL_ROOF
    WING_Z = Z0 - 2.0        # the wings stand back 2 m
    PAV_Z = Z0 - 1.0         # the pavilions 1 m forward of the wings
    XC, XW, XP = 9.0, 17.5, 22.5   # the central block's half width, the wings' end, the pavilions' end
    WH = 12.0                # the wings' and pavilions' cornice
    # --- the tower, out of the central block's roof ------------------------------------------------------------------
    sub = Build(B.name, T)
    clocktower(sub, seed, base=False, hidden_below=HR + .8)
    M = sk.affine((0, 0, 0, 1), (0.0, 0.0, TOWER_Z))
    g.extend(sub.g.transformed(M))
    B.streaks.extend(sub.streaks.transformed(M))
    for (p, hexcol, rad, kind) in sub.lights:
        B.lights.append((xform(M, p), hexcol, rad, kind))
    # rustication courses as thin dark joints in the wall's own plane (bands), so the base reads rusticated for nothing
    rust_bands = []
    y = .9
    while y < 5.2:
        rust_bands.append((y, y + .05, F['flashing']))
        y += .62
    # --- the central block ---------------------------------------------------------------------------------------------
    cf = Frame((-XC, 0.0, Z0), (1.0, 0.0, 0.0))            # the front, facing +z; u 0..18 from x = -9 to x = 9
    door = Hole(XC - 1.75, XC + 1.75, 1.2, 6.2, 1.75, .6)
    wins = [Hole(XC - 5.9, XC - 4.4, 1.6 + 3.2, 1.6 + 7.2, .75, .35), Hole(XC + 4.4, XC + 5.9, 4.8, 8.8, .75, .35)]
    wins_g = [Hole(XC - 5.9, XC - 4.4, 1.6, 3.8, 0.0, .3), Hole(XC + 4.4, XC + 5.9, 1.6, 3.8, 0.0, .3)]
    upper = [Hole(XC - .65, XC + .65, 9.2, 10.9, 0.0, .25)]      # one light over the door, under the portico's ceiling
    # (the wall inside the stylobate and inside the portico's attic, behind the entablature, is never seen: left out)
    hidden = [Hole(XC - 8.2, XC + 8.2, 0.0, 1.2, keep=True), Hole(XC - 7.8, XC + 7.8, 11.4, 13.7, keep=True)]
    wall(g, T, cf, 0.0, 2 * XC, 0.0, HR, civic, [door] + wins + wins_g + upper + hidden, bands=rust_bands, base=0.0)
    reveal(g, T, cf, door, stone, sill=False)
    arch_ring(g, T, cf, door, stone, .45, .1, key=stone)
    for (a, b) in ((door.u0 + .05, XC), (XC, door.u1 - .05)):
        panel_door(g, T, cf, a, b, 1.2, 6.1, -.55, F['paint-oxblood'], r, panels=6, knocker=False)
    bar(g, T, cf, door.u0, door.u1, 6.06, 6.24, -.6, -.46, F['paint-oxblood'])
    hh = Hole(door.u0, door.u1, 6.2, 6.2, door.rise, .6)
    cu = hh.cuts(T)
    for k in range(len(cu) - 1):
        a, b = cu[k], cu[k + 1]
        g.face((cf.P(a, 6.2, -.5), cf.P(b, 6.2, -.5), cf.P(b, hh.top(b), -.5), cf.P(a, hh.top(a), -.5)), F['fanlight'], cf.n)
    if T.detail:
        for k in range(1, 6):
            a = math.pi * k / 6
            sk.cylinder(g, cf.P(XC, 6.2, -.48), cf.P(XC - 1.75 * math.cos(a), 6.2 + 1.75 * math.sin(a), -.48), .03,
                        F['iron-black'], 4, caps=False)
    B.light(cf.P(XC, 5.0, 2.0), 0xFFC27A, 7.0, 'door')
    for i, h in enumerate(wins):
        hall_window(B, cf, h, st, r, 'seg' if i == 0 else 'tri')
    for h in wins_g + upper:
        reveal(g, T, cf, h, stone)
        sill(g, T, cf, h.u0, h.u1, h.v0, stone, lean=True)
        st.window(B, cf, h, r, simple=True)
    plan_mould(g, T, [(XC, Z0), (-XC, Z0)], HR, mould_lod(T, ck.P_CORNICE_BIG), stone, [(0.0, 1.0)], closed=False)
    # the front's top behind the cornice, up into the balustrade's base: no slot under the roof's edge
    fquad(g, cf, 0.0, HR + .6, 2 * XC, HR + .8, 0.0, civic)
    for x in (-7.0, -5.5, -2.6, 2.6, 5.5, 7.0):        # pilasters answering the columns, clear of the door and windows
        fbox(g, T, cf, x + XC - .4, x + XC + .4, 1.2, 11.4, 0.0, .1, stone, 0, skip=('-z', '-y'))
    # the central block's sides above the wings, its back, its roof and balustrade
    # (their feet a little under the wings' roofs at WH + .75: no slot between)
    for (fr, w, v0) in ((Frame((-XC, 0.0, ZB), (0.0, 0.0, 1.0)), Z0 - ZB, WH + .7),     # west side, facing -x
                        (Frame((XC, 0.0, Z0), (0.0, 0.0, -1.0)), Z0 - ZB, WH + .7)):     # east side, facing +x
        wall(g, T, fr, 0.0, w, v0, HR + .8, civic, base=0.0)
        plan_mould(g, T, [(fr.P(0, 0, 0)[0], fr.P(0, 0, 0)[2]), (fr.P(w, 0, 0)[0], fr.P(w, 0, 0)[2])], HR,
                   mould_lod(T, ck.P_CORNICE), stone, [(fr.n[0], fr.n[2])], closed=False)
        for k in range(1, 5):
            a = w * k / 5
            fbox(g, T, fr, a - .4, a + .4, v0, HR, 0.0, .08, stone, 0, skip=('-z', '-y'))
    bk = Frame((XC, 0.0, ZB), (-1.0, 0.0, 0.0))            # the central block's back, facing -z, with its attic lights
    att = [Hole(1.7 + 3.6 * i, 3.0 + 3.6 * i, 13.0, 14.6, 0.0, .2) for i in range(5)]
    wall(g, T, bk, 0.0, 2 * XC, WH + .2, HR + .8, civic, att, base=0.0)
    for h in att:
        reveal(g, T, bk, h, stone)
        st.window(B, bk, h, r, simple=True)
    rf = Frame((-XC, HR + .8, ZB), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0))
    prism(g, rf, [(0.0, 0.0), (2 * XC, 0.0), (2 * XC, -(Z0 - ZB)), (0.0, -(Z0 - ZB))], 0.0, .02, F['lead'], caps=(False, True))
    # the balustrade: the front's (balusters at LOD0), a solid parapet round the rest
    yb = HR + .8
    ck.bx(g, T, -XC, XC, yb - .02, yb + .22, Z0 - .5, Z0 + .02, stone, .02)
    nb = 30 if T.detail else 0
    for k in range(nb):
        x = -XC + .6 + (2 * XC - 1.2) * (k + .5) / nb
        if abs(x) < 1.0:
            continue
        sk.lathe(g, [(.12, 0.0), (.08, .1), (.13, .3), (.07, .55), (.11, .62), (.11, .7)], (x, yb + .22, Z0 - .28), stone, 4,
                 smooth=False, phase=math.pi / 4)
    if not T.detail:
        ck.bx(g, T, -XC + .3, XC - .3, yb + .22, yb + .92, Z0 - .4, Z0 - .15, stone, 0)
    ck.bx(g, T, -XC, XC, yb + .92, yb + 1.1, Z0 - .55, Z0, stone, .015)
    for x in (-XC + .3, -1.0, 1.0, XC - .3):
        ck.bx(g, T, x - .32, x + .32, yb - .02, yb + 1.2, Z0 - .62, Z0 + .02, stone, .02)
    # --- the portico ---------------------------------------------------------------------------------------------------
    ZP = Z0 + 5.4                     # the stylobate's front edge
    SY = 1.2                          # the stylobate's top (eight risers of .15 up from the court)
    ck.bx(g, T, -8.2, 8.2, 0.0, SY, Z0, ZP, F['granite'], .03, skip=('-z',))
    for k in range(8):
        z1 = ZP + (8 - k) * .42
        ck.bx(g, T, -9.4, 9.4, .15 * k, .15 * (k + 1), ZP, z1, F['granite'], .015 if T.bevel else 0)
    for x in (-9.8, 9.8):
        # the steps' cheek pedestals, a triple lamp's mount (the street kit's Lamplighter)
        ck.bx(g, T, x - .7, x + .7, 0.0, 1.5, ZP - .4, ZP + 3.4, stone, .03)
        ck.bx(g, T, x - .8, x + .8, 1.5, 1.7, ZP - .5, ZP + 3.5, stone, .02)
    cols = [-7.0, -5.5, -1.85, 1.85, 5.5, 7.0]
    CH_ = 10.2
    for x in cols:
        column(g, T, (x, SY, ZP - .9), CH_, .54, civic, drums=4, fb=stone)   # (board 3: chunkier, 0.54 m; pass 2's 0.48)
    yE = SY + CH_
    # the entablature over the columns, returned to the wall: architrave, the lettered frieze, the deep cornice
    pts = [(-7.8, Z0), (-7.8, ZP - .25), (7.8, ZP - .25), (7.8, Z0)]
    outs = [(-1.0, 0.0), (0.0, 1.0), (1.0, 0.0)]
    plan_mould(g, T, pts, yE, mould_lod(T, [(0.0, 0.0), (.14, 0.0), (.14, .4), (.2, .45), (.2, .55), (.1, .6), (.1, 1.4),
                                           (0.0, 1.4)]), stone, outs, closed=False)
    plan_mould(g, T, pts, yE + 1.4, mould_lod(T, [(0.0, 0.0), (.12, 0.0), (.12, .14), (.32, .26), (.32, .36), (.66, .54),
                                                 (.76, .62), (.84, .72), (.84, .9), (0.0, .9)]), stone, outs, closed=False)
    ff_ = Frame((-7.8, 0.0, ZP - .25 + .1), (1.0, 0.0, 0.0))   # the frieze's face (u from x = -7.8 to 7.8)
    if T.lod == 0:
        # (board 2: 0.7 m letters, the flat frieze's full height (pass 2: 0.49 m); LOD0 only, as LOD1 has no room under its cap)
        text(g, T, ff_, 7.8, yE + .65, .1, 'MERIDIAN CITY HALL', F['gilt'], c=.004, depth=.03)
    # the portico's ceiling, beamed
    cy = yE - .02
    g.face(((-7.8, cy, Z0), (7.8, cy, Z0), (7.8, cy, ZP - .25), (-7.8, cy, ZP - .25)), civic, (0, -1, 0))
    if T.detail:
        for x in (-5.5, -1.85, 1.85, 5.5):
            ck.bx(g, T, x - .25, x + .25, cy - .45, cy, Z0, ZP - .3, stone, 0, skip=('+y',))
    # the pediment: its tympanum carved with the crest, raking cornices, the horizontal cornice under it
    yp = yE + 2.3
    rise = 3.3
    pf = Frame((-8.4, 0.0, ZP - .25), (1.0, 0.0, 0.0))
    prism(g, pf, [(0.0, yp), (16.8, yp), (8.4, yp + rise)], -(ZP - .25 - Z0), 0.0, civic, caps=(True, True))
    crest(B, pf, 8.4, yp + 1.5, 1.2, stone, F['gilt'])          # (board 2: a 2.4 m clock, pass 2's was 1.7 m)
    if T.lod == 0:
        # board 3: urns over the pediment, at its apex and its two ends (the critique: the flat roofline wanted breaking), on
        # plinths; the apex urn flames gilt
        uprof = [(.34, 0.0), (.22, .14), (.42, .5), (.48, .82), (.26, 1.12), (.32, 1.26), (0.0, 1.4)]
        for (u_, v_, k) in ((8.4, yp + rise + .3, 1.25), (.1, yp + .05, 1.0), (16.7, yp + .05, 1.0)):
            c_ = pf.P(u_, v_, -.55)
            ck.bx(g, T, c_[0] - .5 * k, c_[0] + .5 * k, c_[1], c_[1] + .55 * k, c_[2] - .5 * k, c_[2] + .5 * k, stone, .02)
            sk.lathe(g, [(a * k, b * k) for a, b in uprof], (c_[0], c_[1] + .55 * k, c_[2]), stone, 10)
            if k > 1:
                sk.lathe(g, [(.16, 0.0), (.2, .2), (.08, .5), (0.0, .78)], (c_[0], c_[1] + .55 * k + 1.4 * k, c_[2]), F['gilt'], 6)
    for (a, b) in (((-.3, yp - .05), (8.4, yp + rise + .1)), ((8.4, yp + rise + .1), (17.1, yp - .05))):
        ck.sweep(g, [(.25, 0.0), (-.15, 0.0), (-.15, .1), (-.32, .26), (-.32, .42), (.25, .42)],
                 [pf.P(a[0], a[1], .02), pf.P(b[0], b[1], .02)], stone, up=(0, 1, 0), smooth_u=False)
    # the floods on the stylobate, aimed up the columns: one working, one dying
    flood(B, (-8.0, SY + .25, ZP - .15), (.25, .9, -.35), 1.0)
    flood(B, (8.0, SY + .25, ZP - .15), (-.25, .9, -.35), .35)
    # --- the wings and the pavilions ----------------------------------------------------------------------------------
    for sgn in (-1, 1):
        # the wing: x from sgn*XC to sgn*XW, its front at WING_Z
        x0, x1 = sgn * XC, sgn * XW
        wf = Frame((min(x0, x1), 0.0, WING_Z), (1.0, 0.0, 0.0))
        Ww = XW - XC
        gw = [Hole(1.05 + 2.8 * i, 2.35 + 2.8 * i, 1.2, 3.3, .65, .3) for i in range(3)]
        pw = [Hole(.95 + 2.8 * i, 2.45 + 2.8 * i, 5.6, 9.4, 0.0, .3) for i in range(3)]
        wall(g, T, wf, 0.0, Ww, 0.0, WH, civic, gw + pw, bands=rust_bands, base=0.0)
        for h in gw:
            reveal(g, T, wf, h, stone)
            sill(g, T, wf, h.u0, h.u1, h.v0, stone)
            arch_ring(g, T, wf, h, rust, .4, .06, key=stone)
            st.window(B, wf, h, r, ground=True)
        for i, h in enumerate(pw):
            hall_window(B, wf, h, st, r, ('tri', 'seg', 'tri')[i])
        smould(g, T, wf, 0.0, Ww, 5.0, ck.P_STRING, stone)
        plan_mould(g, T, [(wf.P(0, 0, 0)[0], wf.P(0, 0, 0)[2]), (wf.P(Ww, 0, 0)[0], wf.P(Ww, 0, 0)[2])], WH,
                   mould_lod(T, ck.P_CORNICE_BIG), stone, [(0.0, 1.0)], closed=False)
        # the wing's parapet with sunk panels, and its inner face over the roof
        wall(g, T, wf, 0.0, Ww, WH + .74, WH + 1.7, civic, base=0.0)
        wall(g, T, Frame(wf.P(Ww, 0.0, -.38), wf.mu), 0.0, Ww, WH + .75, WH + 1.7, civic, base=0.0)
        if T.detail:
            for i in range(3):
                fquad(g, wf, .6 + 2.8 * i, WH + .95, 2.4 + 2.8 * i, WH + 1.5, -.01, F['stone-rust'])
        ck.sweep(g, [(.4, 0.0), (-.12, 0.0), (-.12, .08), (0.0, .16), (.4, .14)],
                 [wf.P(0.0, WH + 1.7, .02), wf.P(Ww, WH + 1.7, .02)], stone, up=(0, 1, 0), smooth_u=False)
        # the wing's roof (lead, behind the parapet) to the central block
        g.face(((min(x0, x1), WH + .75, WING_Z - .3), (max(x0, x1), WH + .75, WING_Z - .3), (max(x0, x1), WH + .75, ZB),
                (min(x0, x1), WH + .75, ZB)), F['lead'], (0, 1, 0))
        # the wing's return to the central block's front (the 2 m step)
        rf_ = Frame((x0, 0.0, Z0), (0.0, 0.0, -1.0)) if sgn > 0 else Frame((x0, 0.0, WING_Z), (0.0, 0.0, 1.0))
        wall(g, T, rf_, 0.0, 2.0, 0.0, HR, civic, bands=rust_bands, base=0.0)
        # the pavilion: x from sgn*XW to sgn*XP, its front at PAV_Z
        px0, px1 = sgn * XW, sgn * XP
        pfr = Frame((min(px0, px1), 0.0, PAV_Z), (1.0, 0.0, 0.0))
        Wp = XP - XW
        ph_ = [Hole(1.6, 3.4, 1.2, 3.5, .9, .35), Hole(1.4, 3.6, 5.6, 10.0, 1.1, .35)]
        wall(g, T, pfr, 0.0, Wp, 0.0, WH + 1.0, civic, ph_, bands=rust_bands, base=0.0)
        reveal(g, T, pfr, ph_[0], stone)
        sill(g, T, pfr, ph_[0].u0, ph_[0].u1, ph_[0].v0, stone)
        arch_ring(g, T, pfr, ph_[0], rust, .45, .07, key=stone)
        st.window(B, pfr, ph_[0], r, ground=True)
        hall_window(B, pfr, ph_[1], st, r, 'none')
        arch_ring(g, T, pfr, ph_[1], stone, .35, .08, key=stone)
        if T.detail:
            quoins(g, T, pfr, 0.0, .9, WH + .9, rust, into=1, course=.62)
            quoins(g, T, pfr, Wp, .9, WH + .9, rust, into=-1, course=.62)
        # the pavilion's two returns (the 1 m steps forward of the wing) and its outer side
        ret_in = Frame((px0, 0.0, WING_Z), (0.0, 0.0, 1.0)) if sgn > 0 else Frame((px0, 0.0, PAV_Z), (0.0, 0.0, -1.0))
        wall(g, T, ret_in, 0.0, 1.0, 0.0, WH + 1.76, civic, bands=rust_bands, base=0.0)
        # the pavilion's side over the wing's roof, facing the wing (its roof stands a metre higher)
        over = Frame((px0, 0.0, ZB), (0.0, 0.0, 1.0)) if sgn > 0 else Frame((px0, 0.0, WING_Z), (0.0, 0.0, -1.0))
        wall(g, T, over, 0.0, WING_Z - ZB, WH + .74, WH + 1.76, civic, base=0.0)
        side = Frame((px1, 0.0, PAV_Z), (0.0, 0.0, -1.0)) if sgn > 0 else Frame((px1, 0.0, ZB), (0.0, 0.0, 1.0))
        sw = PAV_Z - ZB
        sh = [Hole(2.2 + 3.7 * i, 3.6 + 3.7 * i, 5.8, 9.6, 0.0, .25) for i in range(4)] + \
             [Hole(2.2 + 3.7 * i, 3.6 + 3.7 * i, 1.4, 3.6, 0.0, .25) for i in range(4)]
        wall(g, T, side, 0.0, sw, 0.0, WH + 1.0, civic, sh, bands=rust_bands, base=0.0)
        for h in sh:
            reveal(g, T, side, h, stone)
            sill(g, T, side, h.u0, h.u1, h.v0, stone, lean=True)
            st.window(B, side, h, r, ground=h.v0 < 4.0, simple=True)
        smould(g, T, side, 0.0, sw, 5.0, ck.P_STRING, stone)
        for i in range(5):
            a = .9 + 3.7 * i
            if a + .4 < sw:
                fbox(g, T, side, a - .4, a + .4, 5.2, WH + .95, 0.0, .08, stone, 0, skip=('-z', '-y'))
        fbox(g, T, side, 0.0, sw, 0.0, .7, 0.0, .05, F['granite'], 0, skip=('-z',))
        plan_mould(g, T, [(pfr.P(0, 0, 0)[0], pfr.P(0, 0, 0)[2]), (pfr.P(Wp, 0, 0)[0], pfr.P(Wp, 0, 0)[2])], WH + 1.0,
                   mould_lod(T, ck.P_CORNICE_BIG), stone, [(0.0, 1.0)], closed=False)
        plan_mould(g, T, [(side.P(0, 0, 0)[0], side.P(0, 0, 0)[2]), (side.P(sw, 0, 0)[0], side.P(sw, 0, 0)[2])], WH + 1.0,
                   mould_lod(T, ck.P_CORNICE), stone, [(side.n[0], side.n[2])], closed=False)
        g.face(((min(px0, px1), WH + 1.76, PAV_Z), (max(px0, px1), WH + 1.76, PAV_Z), (max(px0, px1), WH + 1.76, ZB),
                (min(px0, px1), WH + 1.76, ZB)), F['lead'], (0, 1, 0))
        wall(g, T, side, 0.0, PAV_Z - ZB, WH + 1.0, WH + 1.76, civic, base=0.0)
        # the drum and the copper dome over the pavilion
        dcx = sgn * (XW + XP) / 2
        dcz = PAV_Z - 3.0
        ck.bx(g, T, dcx - 2.6, dcx + 2.6, WH + 1.0, WH + 1.9, dcz - 2.6, dcz + 2.6, stone, .03)
        sk.lathe(g, [(2.2, 0.0), (2.2, 1.7), (2.35, 1.8), (2.35, 2.0), (2.2, 2.0)], (dcx, WH + 1.9, dcz), civic,
                 16 if T.lod == 0 else 8)
        if T.detail:
            for k in range(8):
                a = 2 * math.pi * (k + .5) / 8
                oc_ = (dcx + math.cos(a) * 2.2, WH + 2.8, dcz + math.sin(a) * 2.2)
                sk.lathe(g, [(.32, -.05), (.32, .06), (.22, .1), (.22, -.06)], oc_, stone, 8, axis=(math.cos(a), 0.0, math.sin(a)))
                sk.lathe(g, [(.22, -.06), (0.0, -.06)], oc_, F['lantern'] if (k + (sgn > 0)) % 4 == 0 else F['glass-dark'], 8,
                         axis=(math.cos(a), 0.0, math.sin(a)))
        copper_dome(B, (dcx, WH + 3.9, dcz), 2.3, 3.0)
    # --- the back: the wings' and pavilions' backs, one plain face with windows ---------------------------------------
    back = Frame((XP, 0.0, ZB), (-1.0, 0.0, 0.0))
    bw = 2 * XP
    bh = []
    for i in range(int(bw / 3.6)):
        u = 1.6 + 3.6 * i
        if abs(u - XP) < XC + .3:
            continue
        bh.append(Hole(u, u + 1.3, 1.4, 3.6, 0.0, .25))
        bh.append(Hole(u, u + 1.3, 5.8, 9.6, 0.0, .25))
    wall(g, T, back, 0.0, XP - XC, 0.0, WH + 1.0, civic, [h for h in bh if h.u1 <= XP - XC], bands=rust_bands, base=0.0)
    wall(g, T, back, XP + XC, bw, 0.0, WH + 1.0, civic, [h for h in bh if h.u0 >= XP + XC], bands=rust_bands, base=0.0)
    for (a, b) in ((0.0, XP - XW), (XP + XW, bw)):
        wall(g, T, back, a, b, WH + 1.0, WH + 1.76, civic, base=0.0)       # up to the pavilions' roofs
    ch_ = [Hole(XP - XC + 1.7 + 3.6 * i, XP - XC + 3.0 + 3.6 * i, v0_, v1_, 0.0, .25) for i in range(5)
           for (v0_, v1_) in ((1.4, 3.6), (5.8, 9.6))]
    wall(g, T, back, XP - XC, XP + XC, 0.0, WH + .2, civic, ch_, bands=rust_bands, base=0.0)
    bh = bh + ch_
    smould(g, T, back, 0.0, bw, 5.0, ck.P_STRING, stone)
    # the back's pilaster strips between its bays and its granite base course (bible 2.6 rule 2: no bare 45 m frieze)
    for i in range(int(bw / 3.6) + 1):
        a = .2 + 3.6 * i
        if a + .4 < bw and not any(h.u0 - .05 < a + .4 and a - .4 < h.u1 + .05 for h in bh):
            fbox(g, T, back, a - .35, a + .35, .7, WH + .95, 0.0, .07, stone, 0, skip=('-z', '-y'))
    fbox(g, T, back, 0.0, bw, 0.0, .7, 0.0, .05, F['granite'], 0, skip=('-z',))
    for h in bh:
        reveal(g, T, back, h, stone)
        st.window(B, back, h, r, ground=h.v0 < 4.0, simple=True)
    plan_mould(g, T, [(-XP, ZB), (XP, ZB)], WH + 1.0, mould_lod(T, ck.P_CORNICE), stone, [(0.0, -1.0)], closed=False)
    # the wings' sides seen from over the pavilions are inside; a plinth of granite round the whole front
    for (a, b, z) in ((-XP, -XW, PAV_Z), (-XW, -XC, WING_Z), (XC, XW, WING_Z), (XW, XP, PAV_Z)):
        ck.bx(g, T, a, b, 0.0, .6, z - .05, z + .12, F['granite'], .02 if T.bevel else 0, skip=('-z',))


def city_hall_far(B, seed=0):
    """City Hall's far mesh (bible 10.2: landmarks only in the far layer, to 420 m, 2,500 triangles or fewer for the hall and
    tower; emissive parts and silhouettes): the hall's masses as boxes, the pediment, the pavilions' domes, the tower's
    stages as boxes and low lathes, its four dials lit, the belfry's dark arches, the lantern lit and the red aircraft
    light, the spire; nothing smaller than the far read's 4 m (bible 2.2) but the lit points."""
    g, T = B.g, B.T
    civic, stone, cop = F['stone-civic'], F['stone-dress'], F['copper']
    box = lambda x0, x1, y0, y1, z0, z1, f, skip=('-y',): sk.box(g, x0, x1, y0, y1, z0, z1, f, None, 0.0, cell=0, skip=skip)
    Z0, ZB, HR = HALL_FRONT, HALL_BACK, HALL_ROOF
    # the hall: central block, wings, pavilions, portico and pediment
    box(-9.0, 9.0, 0.0, HR + .9, ZB, Z0, civic)
    box(-17.5, 17.5, 0.0, 12.9, ZB, Z0 - 2.0, civic)
    for sg in (-1, 1):
        box(min(sg * 17.5, sg * 22.5), max(sg * 17.5, sg * 22.5), 0.0, 13.8, ZB, Z0 - 1.0, civic)
        sk.lathe(g, [(2.3, 0.0), (2.3, 2.0), (2.1, 2.9), (1.2, 4.6), (0.0, 5.1)], (sg * 20.0, 13.8, Z0 - 4.0), cop, 6, smooth=False)
        sk.lathe(g, [(.35, 5.1), (0.0, 5.6)], (sg * 20.0, 13.8, Z0 - 4.0), F['lantern'], 4)
    box(-8.2, 8.2, 0.0, 1.2, Z0, Z0 + 5.4, F['granite'])
    for x in (-7.0, -5.5, -1.85, 1.85, 5.5, 7.0):
        box(x - .45, x + .45, 1.2, 11.4, Z0 + 4.05, Z0 + 4.95, civic, skip=('-y', '+y'))
    box(-7.8, 7.8, 11.4, 13.7, Z0, Z0 + 5.4, stone)
    pf = Frame((-8.4, 0.0, Z0 + 5.15), (1.0, 0.0, 0.0))
    prism(g, pf, [(0.0, 13.7), (16.8, 13.7), (8.4, 17.0)], -5.15, 0.0, civic, caps=(False, True))
    fquad(g, Frame((-1.75, 0.0, Z0 + .02), (1.0, 0.0, 0.0)), 0.0, 1.2, 3.5, 7.2, 0.0, F['fanlight'])
    # the tower over the hall
    cz = TOWER_Z
    t = lambda x0, x1, y0, y1, f: box(x0, x1, y0, y1, cz + x0, cz + x1, f)
    t(-4.3, 4.3, HR + .8, 30.0, civic)
    t(-4.6, 4.6, 30.0, 40.86, civic)
    t(-4.0, 4.0, 40.86, 47.94, civic)
    t(-4.25, 4.25, 47.94, 49.1, stone)
    for fr, w in tower_faces(4.6):
        c = fr.P(w / 2, 35.0, .02)
        sk.lathe(g, [(3.0, 0.0), (0.0, 0.0)], (c[0], c[1], c[2] + cz), F['clock-opal'], 12, axis=fr.n, smooth=False)
    for fr, w in tower_faces(4.0):
        fquad(g, Frame((fr.o[0], 0.0, fr.o[2] + cz), fr.u), 2.4, 42.0, 5.6, 46.0, .02, F['interior'])
    sk.lathe(g, [(2.9, 0.0), (2.9, 3.5), (3.05, 4.0), (2.75, 4.7), (1.7, 6.3), (0.4, 7.3)], (0.0, 49.1, cz), civic, 8, smooth=False)
    sk.lathe(g, [(.4, 0.0), (.4, 1.4), (0.0, 1.5)], (0.0, 56.4, cz), F['lantern'], 4)
    sk.lathe(g, [(.2, 0.0), (0.0, 4.6)], (0.0, 57.9, cz), cop, 4, smooth=False)
    sk.lathe(g, [(.14, -.12), (.14, .12), (0.0, .14)], (0.0, 58.4, cz), F['beacon'], 4)


FAR = {'civic.cityhall': city_hall_far}


# ---------------------------------------------------------------------------------------------------------------------------
# The module registry
# ---------------------------------------------------------------------------------------------------------------------------
def _bay_module(fn, height=GROUND, y=0.0, **kw):
    def build(B, seed):
        st = Style(seed)
        r = rng('module', B.name, seed)
        fr = Frame((-BAY / 2, y, 0.0), (1.0, 0.0, 0.0))
        fn(B, fr, st, r, **kw)
    return build


def _shop(B, seed, shutter=False):
    st = Style(seed)
    r = rng('module', B.name, seed)
    fr = Frame((-BAY / 2, 0.0, 0.0), (1.0, 0.0, 0.0))
    word = 'BAKERY' if not shutter else 'CHEMIST'
    ground_shop_bay(B, fr, st, r, lit=not shutter, shutter=shutter, paint=F['paint-green'], trade=word)
    if B.T.lod == 0:
        fascia_text(B, fr, CONSOLE_CLEAR, BAY - CONSOLE_CLEAR, word)


def _upper(B, seed):
    st = Style(seed)
    r = rng('module', B.name, seed)
    upper_window_bay(B, Frame((-BAY / 2, GROUND, 0.0), (1.0, 0.0, 0.0)), st, r)


def _oriel(B, seed):
    st = Style(seed)
    r = rng('module', B.name, seed)
    oriel(B, Frame((-BAY / 2, GROUND, 0.0), (1.0, 0.0, 0.0)), st, r, 1)


def _roof(B, seed):
    r = rng('module', B.name, seed)
    roof(B, Frame((-BAY, 0.0, HOUSE_D / 2), (1.0, 0.0, 0.0)), 2 * BAY, storey_floor(2), HOUSE_D, r, up_left=True,
         stack_left=True)


def _gable(B, seed, kind='breast'):
    st = Style(seed)
    r = rng('module', B.name, seed)
    fr = Frame((0.0, 0.0, HOUSE_D / 2), (1.0, 0.0, 0.0))
    gable(B, fr, storey_floor(2), HOUSE_D, st, r, 'ALBION ROW', kind=kind)


def _house(B, seed):
    st = Style(seed)
    r = rng('module', B.name, seed)
    house(B, Frame((-BAY, 0.0, HOUSE_D / 2), (1.0, 0.0, 0.0)), st, r, 2, door_bay=0, up_right=True, ends=True)


def _run(B, seed):
    row(B, seed, n=4, storeys=2, shops=(2,), x0=-2 * 2 * BAY)


def _corner(B, seed):
    corner_shop(B, seed)


def _ten_face(B, seed):
    fr = Frame((-2 * BAY, 0.0, 0.0), (1.0, 0.0, 0.0))
    tenement_face(B, fr, seed, 'entry', mouldings=True)


def _ten_escape(B, seed):
    fr = Frame((-2 * BAY, 0.0, 0.0), (1.0, 0.0, 0.0))
    tenement_face(B, fr, seed, 'escape', mouldings=True)
    fire_escape(B, fr)


def _ten_back(B, seed):
    fr = Frame((-2 * BAY, 0.0, 0.0), (1.0, 0.0, 0.0))
    tenement_face(B, fr, seed, 'back', mouldings=True)


def _roofkit(B, seed):
    roof_kit(B, Frame((-10.0, 0.0, 5.0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0)), -10.0, 5.0, seed)


def _block(B, seed):
    tenement_block(B, seed)


def _ten_street(B, seed):
    tenement_street(B, seed, 2)


def _tower(B, seed):
    clocktower(B, seed)


def _hall(B, seed):
    city_hall(B, seed)


# name: (builder, hero, the bible's LOD0 / LOD1 caps where it gives one, a description)
MODULES = {
    'row.ground.door': (_bay_module(ground_door_bay), False, (700, 60), 'door bay, 2.5 x 4.0 m'),
    'row.ground.window': (_bay_module(ground_window_bay), False, (700, 60), 'parlour window bay, 2.5 x 4.0 m'),
    'row.ground.shop': (lambda B, s: _shop(B, s, False), False, (900, 80), 'shopfront bay, lit, 2.5 x 4.0 m'),
    'row.ground.shutter': (lambda B, s: _shop(B, s, True), False, (900, 80), 'shopfront bay, shutter half down'),
    'row.upper.window': (_upper, False, (300, 30), 'upper bay, 2.5 x 3.0 m'),
    'row.upper.oriel': (_oriel, False, (1800, 160), 'canted oriel, one storey, on corbels'),
    'row.roof.slate': (_roof, False, (1200 + 400, 64 + 50), '5 m house roof + upstand + stack (4 x 300 + 400)'),
    'row.gable.end': (_gable, False, None, 'run end: gable with chimney breast, blind windows, plates, name plate'),
    'row.gable.ghost': (lambda B, s: _gable(B, s, 'ghost'), False, None, 'run end: gable with a ghost sign, windows below'),
    'row.house': (_house, True, (4500, 450), '5 m house, two storeys, roof (+ back, party walls)'),
    'row.run': (_run, False, None, 'a run of four houses with a shop, gables at both ends'),
    'corner.shop': (_corner, True, (14000, 1400), 'corner shop, 3 storeys, two faces, turret'),
    'tenement.face': (_ten_face, False, (16000, 1600), 'tenement face 10 x 19 m, entry under the stair tower'),
    'tenement.escape': (_ten_escape, False, (16000, 1600), 'tenement face 10 x 19 m with fire escape'),
    'tenement.back': (_ten_back, False, (16000, 1600), 'tenement back-court face 10 x 19 m (lean)'),
    'tenement.roofkit': (_roofkit, False, (600 + 700 + 250 + 400, 60 + 80 + 30 + 40), 'stair house, tank, vents, aerials, hatch'),
    'tenement.block': (_block, True, None, 'tenement block 20 x 10 x 19 m: entry + stair tower, shops, escape, back court'),
    'tenement.street': (_ten_street, False, None, 'a 40 m tenement street, two rows of two blocks (the chunk check)'),
    'civic.clocktower': (_tower, True, (55000, 7000), 'City Hall tower alone, 62 m, dials at 6:47, richer top'),
    'civic.cityhall': (_hall, True, (55000, 7000), 'City Hall: hall, portico, steps, domes, the tower (cap: hall + tower)'),
}


def build(name, lod, seed=7):
    """Build a module for a tier: returns its Build. lod 2: the far layer's mesh, for the landmarks in FAR."""
    ck.finishes()
    T = ck.CityTier(min(lod, 1))
    B = Build(name, T)
    if lod >= 2:
        FAR[name](B, seed)
        return B
    MODULES[name][0](B, seed)
    return B


def triangles(g):
    import numpy as np
    S = np.frombuffer(g.size, dtype=np.uint8).astype(np.int64)
    return int((S - 2).sum())


def finishes_used(g):
    import numpy as np
    Fi = np.frombuffer(g.fin, dtype=np.uint16)
    return sorted({sk.FINS[i].name for i in np.unique(Fi)})
