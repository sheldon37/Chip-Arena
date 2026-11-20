"""Meridian landmarks: the five instance door fronts (art bible 7.1; the approved board 3's gates, `gates-caches/pass2`), laid out
on the approved layouts' halls.

The walk's halls are one kind of neutral mass: 30-44 m wide, 18-22 m deep, 16 m tall, with a 10 m wide, 9 m tall mouth through
the middle of the face (the wings either side up to 9 m, the upper block over the whole width from 9 m to 16 m). Board 3's fronts
were drawn for today's 6 m doors, so each family's front is laid out again here in its own materials, signs and lights on that
slot. In the hall's frame (Core places it at the door point): x across the face (the viewer's right), y up from the hall's floor,
z into the hall (the face at z = 0, its outside at z < 0). The parts Core places:
  door    the face round the mouth (|x| <= 7.5), the mouth's surround, the front's hero pieces over it, the apron in front
          (flat paint within 12 m, the join circle's corner marks outside its 3.3 m keep-out at 8 m out)
  bay     a 2.5 m wing bay of the face (x 0..2.5), 16 m tall, repeated and stretched to the corners; `side` shares it
  back    a plain 2.5 m bay for the hall's back
  roof    a unit square (x 0..1, z 0..1) of the roof's surface, stretched over the hall
  lining  a 2 m length of the mouth's lining (its walls at x = -5 and 5 and its ceiling at 9 m), repeated through the hall
  leaf_l, leaf_r  the airlock's leaves at rest (shut), posed by Core's PoseOf (the walk draws them open, inside their wings)
Everything below 3.1 m over the floor stands within 0.25 m of the hall's faces (relief), or is flat paint; the hero pieces over
the mouth hang 3.1 m and more over the apron.
"""
import math

import meridian_gates_kit as mk
import meridian_lm_kit as lk
import stagekit as sk
from meridian_gates_kit import F, Geo, bx, glow
from meridian_lm_kit import PI, WARM, OPAL

FAMILIES = ('level0', 'poolrooms', 'wardzero', 'station', 'ironworks')
PARTS = ('door', 'rear', 'bay', 'side', 'back', 'roof', 'lining', 'leaf_l', 'leaf_r')
SHARED = {'side': 'bay'}         # parts banked as another part's mesh
H, MOUTH_H, MOUTH_X, DOOR_X = 16.0, 9.0, 5.0, 7.5
JOIN_Z, KEEP = -8.0, 3.3

FLUO, EXIT_GREEN = 0xF2EE9A, 0x36E07A
AQUA, POOLBLUE = 0x52E4E0, 0x2AB4F2
COLD, RED, VIOLET, TEAL = 0xD4E6FF, 0xFF2A30, 0xA04CFF, 0x1F9C9C
FURNACE, CAGE = 0xFF6418, 0xFF9A30


def build(fam, part, t):
    T = lk.tier(t)
    E = lk.finishes()
    g = Geo()
    st = STYLES[fam]()
    if part == 'door':
        door(g, T, E, st)
    elif part == 'rear':
        rear(g, T, E, st)
    elif part in ('bay', 'side'):
        bay(g, T, E, st, False)
    elif part == 'back':
        bay(g, T, E, st, True)
    elif part == 'roof':
        mk.ground(g, T, 0.0, 1.0, 0.0, 1.0, H + .005, st['roof'], cell=0)
    elif part == 'lining':
        lining(g, T, E, st)
    elif part in ('leaf_l', 'leaf_r'):
        leaf(g, T, E, st, -1 if part == 'leaf_l' else 1)
    return g


def _style(fam, wall, joint, plinth, roof, in_wall, in_ceil, in_floor, light, leaf):
    return {'fam': fam, 'wall': wall, 'joint': joint, 'plinth': plinth, 'roof': roof, 'in_wall': in_wall, 'in_ceil': in_ceil,
            'in_floor': in_floor, 'in_light': light, 'leaf': leaf}


STYLES = {
    'level0': lambda: _style('level0', F['stucco-beige'], F['stucco-joint'], F['concrete-dark'], F['tar-felt'], F['wallpaper'],
                             F['ceiling-tile'], F['carpet-damp'], glow(FLUO, 2.0), F['veneer']),
    'poolrooms': lambda: _style('poolrooms', F['brick-glazed'], F['tile-teal'], F['tile-black'], F['tar-felt'], F['tile-pool'],
                                F['tile-aqua'], F['tile-pool'], glow(0xEAF6F4, 1.8), F['glass-frosted']),
    'wardzero': lambda: _style('wardzero', F['cladding-white'], F['cladding-joint'], F['concrete-dark'], F['concrete'],
                               F['cladding-white'], F['steel-white'], F['concrete'], glow(COLD, 2.2), F['steel-white']),
    'station': lambda: _style('station', F['brick-stock'], F['stone-dress'], F['granite'], F['slate'], F['tile-cream'],
                              F['paint-cream'], F['paving'], glow(0xF2EEE0, 1.6), F['cast-green']),
    'ironworks': lambda: _style('ironworks', F['brick-buff'], F['brick-blue'], F['brick-blue'], F['slate'], F['brick-soot'],
                                F['iron-heat'], F['slag'], glow(FURNACE, 1.4), F['iron-raw']),
}


# ---------------------------------------------------------------------------------------------------------------------------
# The wing bay (x 0..2.5): the family's face, a plinth, its bands and its top
# ---------------------------------------------------------------------------------------------------------------------------
def bay(g, T, E, st, plain):
    fam = st['fam']
    w = 2.5
    win = None
    if not plain:
        win = {'poolrooms': (.45, 2.05, 4.2, 8.2, (AQUA, 1.1), .8), 'station': (.55, 1.95, 4.6, 8.4, (WARM, 1.3), .7),
               'ironworks': (.45, 2.05, 3.6, 10.0, (FURNACE, 1.2), .8), 'wardzero': (.7, 1.8, 11.0, 13.0, (COLD, .9))}.get(fam)
    lk.facade(g, T, -.02, 0.0, w, 0.0, H, st['wall'], [win] if win else [], f_frame=st['joint'] if fam != 'level0' else F['alu'], reveal=.2)
    bx(g, T, 0.0, w, 0.0, .6, -.07, -.02, st['plinth'], bevel=.01, skip=('+z',))
    if fam == 'level0':
        # the render's joints: a V at the bay's edge and at the floor lines, the copied damp stain on every bay
        bx(g, T, -.015, .015, .6, H - .3, -.025, -.018, st['joint'], bevel=0, cell=0)
        for y in (4.0, 8.0, 12.0):
            bx(g, T, 0.0, w, y - .015, y + .015, -.025, -.018, st['joint'], bevel=0, cell=0)
        if T.detail:
            g.face(((1.2, H - 1.4, -.024), (1.9, H - 1.4, -.024), (1.75, H - 3.6, -.024), (1.3, H - 2.8, -.024)), F['stain'], (0, 0, -1))
        bx(g, T, 0.0, w, H - .26, H, -.06, -.02, F['alu'], bevel=.005, skip=('+z',))
    elif fam == 'poolrooms':
        for y in (3.0, 12.6):
            bx(g, T, 0.0, w, y, y + .4, -.06, -.02, F['tile-teal'], bevel=.01, skip=('+z',))
        for k in range(3):
            bx(g, T, 0.0, w, H - 1.2 + k * .35, H - .85 + k * .35, -.22 + k * .06, -.02, F['coping'], bevel=.01, skip=('+z',))
    elif fam == 'wardzero':
        for y in (3.0, 6.25, 9.5, 12.75):
            bx(g, T, 0.0, w, y - .02, y + .02, -.025, -.018, st['joint'], bevel=0, cell=0)
        bx(g, T, -.015, .015, .6, H, -.025, -.018, st['joint'], bevel=0, cell=0)
        bx(g, T, 0.0, w, 2.4, 2.9, -.05, -.02, F['cladding-teal'], bevel=.005, skip=('+z',))
        bx(g, T, 0.0, w, H - .3, H, -.08, -.02, F['steel-white'], bevel=.005, skip=('+z',))
    elif fam == 'station':
        for y in (4.0, 10.4):
            bx(g, T, 0.0, w, y, y + .3, -.12, -.02, F['stone-dress'], bevel=.01, skip=('+z',))
        bx(g, T, 0.0, w, .6, 1.4, -.05, -.02, F['tile-green'], bevel=.005, skip=('+z',))
        if not plain:
            g.face(((.75, 11.3, -.03), (1.75, 11.3, -.03), (1.75, 13.4, -.03), (.75, 13.4, -.03)), glow(WARM, 1.0), (0, 0, -1))
        lk.cornice(g, T, H - .015, 0.0, w, -.02, F['stone-dress'], s=.5)
    elif fam == 'ironworks':
        for y in (2.0, 11.6, 14.0):
            bx(g, T, 0.0, w, y, y + .3, -.06, -.02, F['brick-blue'], bevel=.005, skip=('+z',))
        lk.cornice(g, T, H - .015, 0.0, w, -.02, F['brick-blue'], s=.5)


# ---------------------------------------------------------------------------------------------------------------------------
# The mouth's lining: a 2 m length of its walls and ceiling
# ---------------------------------------------------------------------------------------------------------------------------
def lining(g, T, E, st):
    L = 2.0
    for sgn in (-1, 1):
        x = sgn * (MOUTH_X - .02)
        mk.side_rect(g, T, 0.0, L, 0.0, MOUTH_H, x, st['in_wall'], n=-sgn, cell=0)
        bx(g, T, min(x, x - sgn * .05), max(x, x - sgn * .05), 0.0, .3, 0.0, L, st['plinth'], bevel=0, cell=0)
    g.face(((-MOUTH_X, MOUTH_H - .02, 0.0), (MOUTH_X, MOUTH_H - .02, 0.0), (MOUTH_X, MOUTH_H - .02, L), (-MOUTH_X, MOUTH_H - .02, L)), st['in_ceil'], (0, -1, 0))
    g.face(((-.4, MOUTH_H - .05, .15), (.4, MOUTH_H - .05, .15), (.4, MOUTH_H - .05, L - .15), (-.4, MOUTH_H - .05, L - .15)), st['in_light'], (0, -1, 0))
    g.face(((-MOUTH_X + .02, .01, 0.0), (MOUTH_X - .02, .01, 0.0), (MOUTH_X - .02, .01, L), (-MOUTH_X + .02, .01, L)), st['in_floor'], (0, 1, 0))
    if st['fam'] == 'ironworks':
        for sgn in (-1, 1):
            g.face(((sgn * (MOUTH_X - .4), .02, 0.0), (sgn * (MOUTH_X - .1), .02, 0.0), (sgn * (MOUTH_X - .1), .02, L), (sgn * (MOUTH_X - .4), .02, L)), glow(FURNACE, .6), (0, 1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The airlock's leaves (shut): each half the mouth wide, in the mouth 0.2-0.45 m in
# ---------------------------------------------------------------------------------------------------------------------------
def leaf(g, T, E, st, side):
    fam = st['fam']
    x0, x1 = (-MOUTH_X, 0.0) if side < 0 else (0.0, MOUTH_X)
    z0, z1 = .2, .45
    bx(g, T, x0 + .02, x1 - .02, 0.0, MOUTH_H - .05, z0, z1, st['leaf'], bevel=.02)
    xc = x1 - .4 if side < 0 else x0 + .4
    if fam == 'level0':
        bx(g, T, x0 + .1, x1 - .1, .05, .6, z0 - .01, z0, F['kickplate'], bevel=0, cell=0)
        mk.ellipsoid(g, (xc, 1.0, z0 - .05), (.05, .05), F['brass'], 6, rings=3)
        xa, xb = (xc - .25, xc) if side < 0 else (xc, xc + .25)
        g.face(((xa, 4.0, z0 - .01), (xb, 4.0, z0 - .01), (xb, 6.0, z0 - .01), (xa, 6.0, z0 - .01)), glow(0xE8D27A, .9), (0, 0, -1))
    elif fam == 'poolrooms':
        for k in range(3):
            mk.disc(g, xc, 2.0 + k * 2.6, z0 - .01, .35, 10, glow(0xBFEFEA, 1.1), normal=(0, 0, -1))
        bx(g, T, x0 + .05, x1 - .05, 0.0, .25, z0 - .02, z0, F['chrome'], bevel=0, cell=0)
    elif fam == 'wardzero':
        for y in (2.2, 4.5, 6.8):
            bx(g, T, x0 + .2, x1 - .2, y - .02, y + .02, z0 - .01, z0, F['cladding-joint'], bevel=0, cell=0)
        bx(g, T, x0 + .2, x1 - .2, 3.2, 3.5, z0 - .012, z0, F['cladding-teal'], bevel=0, cell=0)
    elif fam == 'station':
        for k in range(10):
            u = x0 + (k + .5) * (x1 - x0) / 10
            mk.seg(g, T, (u - .2, .2, z0 - .03), (u + .2, MOUTH_H - .3, z0 - .03), .05, .05, F['iron-black'])
            mk.seg(g, T, (u + .2, .2, z0 - .05), (u - .2, MOUTH_H - .3, z0 - .05), .05, .05, F['iron-black'])
    elif fam == 'ironworks':
        for y in (1.0, 3.0, 5.0, 7.0, 8.6):
            bx(g, T, x0 + .05, x1 - .05, y - .08, y + .08, z0 - .04, z0, F['iron-heat'], bevel=0, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# The door piece: the face round the mouth (|x| <= 7.5), its surround, the hero pieces, the apron and the join's corner marks
# ---------------------------------------------------------------------------------------------------------------------------
def door(g, T, E, st):
    wall = st['wall']
    lk.facade(g, T, -.02, -DOOR_X, -MOUTH_X, 0.0, MOUTH_H, wall, [], reveal=.2)
    lk.facade(g, T, -.02, MOUTH_X, DOOR_X, 0.0, MOUTH_H, wall, [], reveal=.2)
    lk.facade(g, T, -.02, -DOOR_X, DOOR_X, MOUTH_H, H, wall, [], reveal=.2)
    for (a, b) in ((-DOOR_X, -MOUTH_X), (MOUTH_X, DOOR_X)):
        bx(g, T, a, b, 0.0, .6, -.07, -.02, st['plinth'], bevel=.01, skip=('+z',))
    g.face(((-MOUTH_X, MOUTH_H - .005, -.02), (MOUTH_X, MOUTH_H - .005, -.02), (MOUTH_X, MOUTH_H - .005, .3), (-MOUTH_X, MOUTH_H - .005, .3)), st['in_ceil'], (0, -1, 0))
    _apron(g, T, E, st)
    _corners(g, T, st)
    HERO[st['fam']](g, T, E, st)


def rear(g, T, E, st):
    """The hall's back round the mouth's far end (|x| <= 7.5): the plain face, a surround and the soffit."""
    wall = st['wall']
    lk.facade(g, T, -.02, -DOOR_X, -MOUTH_X, 0.0, MOUTH_H, wall, [], reveal=.2)
    lk.facade(g, T, -.02, MOUTH_X, DOOR_X, 0.0, MOUTH_H, wall, [], reveal=.2)
    lk.facade(g, T, -.02, -DOOR_X, DOOR_X, MOUTH_H, H, wall, [], reveal=.2)
    for (a, b) in ((-DOOR_X, -MOUTH_X), (MOUTH_X, DOOR_X)):
        bx(g, T, a, b, 0.0, .6, -.07, -.02, st['plinth'], bevel=.01, skip=('+z',))
    _surround(g, T, st['joint'] if st['fam'] != 'level0' else F['skirting'], w=.4, d=.15)
    if st['fam'] == 'level0':
        bx(g, T, -7.4, 7.4, H - .26, H, -.06, -.02, F['alu'], bevel=.005, skip=('+z',))
    elif st['fam'] in ('station', 'ironworks'):
        lk.cornice(g, T, H, -DOOR_X, DOOR_X, -.02, st['joint'] if st['fam'] == 'ironworks' else F['stone-dress'], s=.5)


def _flat(g, pts, y, f):
    g.face([(p[0], y, p[1]) for p in pts], f, (0, 1, 0))


def _apron(g, T, E, st):
    """The ground in front (flat paint, 3 cm): the family's floor creeping out of the mouth and its lines."""
    fam = st['fam']
    y = .03
    if fam == 'level0':
        _flat(g, [(-MOUTH_X, 0.0), (-3.9, -2.6), (-1.6, -4.2), (1.8, -3.9), (3.6, -2.4), (MOUTH_X, 0.0)], y, F['carpet'])
        for x in (-6.5, -3.5, 3.5, 6.5):
            _flat(g, [(x - .06, -6.0), (x - .06, -11.5), (x + .06, -11.5), (x + .06, -6.0)], y - .01, F['paint-line'])
    elif fam == 'poolrooms':
        _flat(g, [(-DOOR_X, 0.0), (-DOOR_X, -4.0), (DOOR_X, -4.0), (DOOR_X, 0.0)], y, F['tile-pool'])
        _flat(g, [(-DOOR_X, -4.0), (-DOOR_X, -4.2), (DOOR_X, -4.2), (DOOR_X, -4.0)], y + .005, F['tile-teal'])
        if T.detail:
            for (x, z, r) in ((-6.0, -6.0, 1.2), (5.2, -10.0, 1.6), (-2.0, -11.6, .9)):
                if math.hypot(x, z - JOIN_Z) - r > KEEP:
                    mk.disc(g, x, y, z, r, 10, F['water'], normal=(0, 1, 0))
    elif fam == 'wardzero':
        for x, f, w in ((-4.4, F['paint-line'], .06), (4.4, F['paint-line'], .06), (-3.6, F['decon-teal'], .12), (3.6, F['decon-teal'], .12)):
            _flat(g, [(x - w, 0.0), (x - w, -11.8), (x + w, -11.8), (x + w, 0.0)], y, f)
    elif fam == 'station':
        _flat(g, [(-DOOR_X, 0.0), (-DOOR_X, -11.8), (DOOR_X, -11.8), (DOOR_X, 0.0)], y - .01, F['setts'])
        for x in (-6.4, -5.2, 5.2, 6.4):
            _flat(g, [(x * .8 - .05, 0.0), (x - .05, -11.8), (x + .05, -11.8), (x * .8 + .05, 0.0)], y, F['rail'])
    elif fam == 'ironworks':
        for (x0, x1) in ((-DOOR_X, -5.4), (5.4, DOOR_X)):
            _flat(g, [(x0, 0.0), (x0, -11.8), (x1, -11.8), (x1, 0.0)], y, F['slag'])
            if T.detail:
                for k in range(5):
                    z = -11.0 + k * 2.2
                    xm = (x0 + x1) / 2
                    _flat(g, [(xm - .4, z), (xm - .45, z + .1), (xm + .25, z + 1.0), (xm + .3, z + .9)], y + .004, glow(FURNACE, .5))


def _corners(g, T, st):
    """The join circle's four corner marks (board 3: no ring), on the square of half side 3.5 m round it, outside the keep-out."""
    f = {'level0': F['alu'], 'poolrooms': F['tile-teal'], 'wardzero': F['decon-teal'], 'station': F['granite'], 'ironworks': F['iron-raw']}[st['fam']]
    half, arm, w, y = 3.5, 1.2, .14, .035
    for sx in (-1, 1):
        for sz in (-1, 1):
            X, Z = sx * half, JOIN_Z + sz * half
            for (a, b, c, d) in ((X - sx * arm, X + sx * w, Z, Z + sz * w), (X, X + sx * w, Z - sz * arm, Z)):
                x0, x1 = sorted((a, b))
                z0, z1 = sorted((c, d))
                assert min(math.hypot(px, pz - JOIN_Z) for px in (x0, x1) for pz in (z0, z1)) >= KEEP
                _flat(g, [(x0, z0), (x0, z1), (x1, z1), (x1, z0)][::-1], y, f)


def _surround(g, T, f, w=.45, d=.22, top=MOUTH_H):
    """A flat frame round the mouth on the face (within 0.25 m), its head along 9 m."""
    bx(g, T, -MOUTH_X - w, -MOUTH_X, 0.0, top + w, -d, -.02, f, bevel=.02, skip=('+z',))
    bx(g, T, MOUTH_X, MOUTH_X + w, 0.0, top + w, -d, -.02, f, bevel=.02, skip=('+z',))
    bx(g, T, -MOUTH_X, MOUTH_X, top, top + w, -d, -.02, f, bevel=.02, skip=('+z',))


def _door_level0(g, T, E, st):
    """THRESHOLD MUTUAL's rear entrance: the mouth framed as an office door's architrave made giant, the green EXIT box over it, a
    drop ceiling with troffers hung over the apron, the company's letters, the keypad and the ordinary fire door beside it."""
    _surround(g, T, F['skirting'], w=.5, d=.2)
    bx(g, T, -1.3, 1.3, 9.9, 10.8, -.75, -.25, F['enamel-green'], bevel=.02)
    g.face(((-1.2, 9.98, -.76), (1.2, 9.98, -.76), (1.2, 10.72, -.76), (-1.2, 10.72, -.76)), glow(EXIT_GREEN, 2.4), (0, 0, -1))
    mk.text_stroke(g, 'EXIT', 0.0, 10.12, -.77, .08, F['tbar'], T, style='flat', w=.07, d=.004)
    y = 6.5
    for i in range(6):
        for j in range(3):
            x0, x1 = -6.0 + i * 2.0, -4.0 + i * 2.0
            z0, z1 = -4.0 + j * 1.3, -2.7 + j * 1.3
            if (i + j) % 3 == 1:
                g.face(((x0 + .2, y, z0 + .2), (x0 + .2, y, z1 - .2), (x1 - .2, y, z1 - .2), (x1 - .2, y, z0 + .2)), glow(FLUO, 2.2), (0, -1, 0))
                g.face(((x0, y + .01, z0), (x0, y + .01, z1), (x1, y + .01, z1), (x1, y + .01, z0)), F['ceiling-tile'], (0, -1, 0))
            elif mk.h01(651, i, j) >= .2:
                g.face(((x0, y, z0), (x0, y, z1), (x1, y, z1), (x1, y, z0)), F['ceiling-tile'] if mk.h01(652, i, j) > .25 else F['ceiling-stain'], (0, -1, 0))
            if T.detail:
                mk.seg(g, T, (x0, y - .02, z0), (x1, y - .02, z0), .04, .04, F['tbar'])
                mk.seg(g, T, (x0, y - .02, z0), (x0, y - .02, z1), .04, .04, F['tbar'])
    for x in (-5.0, 5.0):
        mk.rod(g, (x, y, -3.5), (x, MOUTH_H + 1.2, -.05), .02, F['steel-dark'], 4, caps=False)
    mk.text_stroke(g, 'THRESHOLD MUTUAL', 0.0, 12.2, -.02, .12, F['alu'], T, style='cast' if T.detail else 'flat', w=.09, d=.05)
    bx(g, T, -1.0, 1.0, 14.3, 15.1, -.08, -.02, F['alu-dark'], bevel=.01)
    bx(g, T, 5.6, 5.9, 1.1, 1.5, -.07, -.02, F['keypad'], bevel=.005)
    g.face(((5.66, 1.42, -.075), (5.84, 1.42, -.075), (5.84, 1.47, -.075), (5.66, 1.47, -.075)), glow(EXIT_GREEN, 2.0), (0, 0, -1))
    bx(g, T, -7.1, -6.0, 0.0, 2.2, -.12, -.02, F['door-steel'], bevel=.01)
    for x in (-6.9, 6.6):
        bx(g, T, x - .25, x + .25, 4.4, 4.75, -.2, -.02, F['alu'], bevel=.01)
        g.face(((x - .22, 4.39, -.18), (x + .22, 4.39, -.18), (x + .22, 4.39, -.04), (x - .22, 4.39, -.04)), glow(FLUO, 1.6), (0, -1, 0))


def _door_poolrooms(g, T, E, st):
    """MERIDIAN PUBLIC BATHS: glazed white brick under a stepped Deco head, a mosaic sunburst lunette over the mouth, BATHS in Deco
    letters lit aqua, glass-block pylons beside the mouth, the NO RUNNING and NO DIVING plates and the pool lamps."""
    _surround(g, T, F['tile-teal'], w=.55, d=.2)
    cy, R = 9.6, 4.4
    n = 14 if T.detail else 7
    cols = (F['mosaic-deep'], F['mosaic-white'], F['mosaic-blue'], F['mosaic-gold'], F['mosaic-aqua'])
    for i in range(n):
        a0, a1 = PI * i / n, PI * (i + 1) / n
        g.face(((0.0, cy, -.08), (R * math.cos(a1), cy + R * math.sin(a1), -.08), (R * math.cos(a0), cy + R * math.sin(a0), -.08)), cols[i % len(cols)], (0, 0, -1))
    mk.disc(g, 0.0, cy + .05, -.1, 1.3, 10, glow(0xE4FBF8, 1.6), normal=(0, 0, -1))
    k = 16 if T.detail else 8
    for i in range(k):
        a0, a1 = PI * i / k, PI * (i + 1) / k
        mk.seg(g, T, (R * math.cos(a0), cy + R * math.sin(a0), -.12), (R * math.cos(a1), cy + R * math.sin(a1), -.12), .4, .2, F['tile-teal'])
    mk.text_stroke(g, 'BATHS', 0.0, 14.3, -.06, .2, glow(AQUA, 2.6), T, style='neon', w=.1, standoff=.06)
    for kk in range(3):
        bx(g, T, -4.0 + kk * .9, 4.0 - kk * .9, H - .9 + kk * .3, H - .6 + kk * .3, -.35, -.02, F['coping'], bevel=.01)
    for sgn in (-1, 1):
        x0, x1 = sorted((sgn * 5.8, sgn * 7.2))
        g.face(((x0, 1.2, -.06), (x1, 1.2, -.06), (x1, 8.2, -.06), (x0, 8.2, -.06)), glow(POOLBLUE, 1.2), (0, 0, -1))
        if T.detail:
            for k2 in range(1, 20):
                yy = 1.2 + k2 * .35
                bx(g, T, x0, x1, yy - .02, yy + .02, -.08, -.06, F['tile-teal'], bevel=0, cell=0)
            for k2 in range(1, 4):
                xx = x0 + k2 * (x1 - x0) / 4
                bx(g, T, xx - .02, xx + .02, 1.2, 8.2, -.08, -.06, F['tile-teal'], bevel=0, cell=0)
    for (x, word) in ((-6.5, 'NO RUNNING'), (6.5, 'NO DIVING')):
        mk.plate_text(g, T, word, x, .9, -.03, 1.6, .4, F['enamel-cream'], F['enamel-cobalt'], px=.035)
    for x in (-6.5, 6.5):
        bx(g, T, x - .3, x + .3, 9.5, 10.0, -.12, -.02, F['chrome'], bevel=.01)
        g.face(((x - .25, 9.55, -.125), (x + .25, 9.55, -.125), (x + .25, 9.95, -.125), (x - .25, 9.95, -.125)), glow(AQUA, 2.2), (0, 0, -1))


def _door_wardzero(g, T, E, st):
    """The Decon Gate of Meridian Biocontainment: a white steel portal frame round the mouth, red turning beacons on its head,
    cold floods, quarantine sheeting hung over the mouth down to 3.2 m and backlit, QUARANTINE stencils, the hexagon, and violet
    growth climbing the frame."""
    _surround(g, T, F['steel-white'], w=.6, d=.24)
    bx(g, T, -MOUTH_X - .6, MOUTH_X + .6, MOUTH_H + .6, MOUTH_H + 1.4, -.9, -.02, F['steel-white'], bevel=.02)
    for x in (-4.6, 4.6):
        sub = Geo()
        mk.beacon(sub, T, F['steel-dark'], glow(RED, 3.0), r=.3, h=.55)
        mk.part(g, sub, (0, 0, 0, 1), (x, MOUTH_H + 1.4, -.5))
    for x in (-6.6, 6.6):
        mk.seg(g, T, (x, 8.0, -.02), (x, 8.0, -.7), .08, .08, F['steel-dark'])
        sub = Geo()
        mk.flood(sub, T, F['steel-dark'], glow(COLD, 3.0), w=.6, h=.42, d=.24)
        mk.part(g, sub, mk.qx(-30), (x, 7.85, -.8))
    zs = -.6
    k = 10 if T.detail else 4
    for i in range(k):
        x0, x1 = -MOUTH_X + i * 2 * MOUTH_X / k, -MOUTH_X + (i + 1) * 2 * MOUTH_X / k
        yb = 3.25 + (.3 if i % 2 else 0.0)
        g.face(((x0, yb, zs), (x1, yb, zs), (x1, MOUTH_H - .05, zs), (x0, MOUTH_H - .05, zs)), F['sheet'], (0, 0, -1))
        g.face(((x0, yb, zs + .01), (x0, MOUTH_H - .05, zs + .01), (x1, MOUTH_H - .05, zs + .01), (x1, yb, zs + .01)), glow(0xC8E2DC, .5), (0, 0, 1))
    mk.seg(g, T, (-MOUTH_X - .4, MOUTH_H - .05, zs), (MOUTH_X + .4, MOUTH_H - .05, zs), .1, .1, F['steel-dark'])
    mk.text_stroke(g, 'QUARANTINE', 0.0, 6.6, zs - .01, .1, F['stencil-red'], T, style='flat', w=.08, d=.003)
    mk.text_stroke(g, 'DO NOT REMOVE', 0.0, 5.6, zs - .01, .05, F['stencil-black'], T, style='flat', w=.04, d=.003)
    sub = Geo()
    sub.face([(math.cos(PI / 3 * i) * 1.3, math.sin(PI / 3 * i) * 1.3, 0) for i in range(6)], F['cladding-teal'], (0, 0, -1))
    sub.face([(math.cos(PI / 3 * i) * .7, math.sin(PI / 3 * i) * .7, -.01) for i in range(6)], F['cladding-white'], (0, 0, -1))
    mk.part(g, sub, (0, 0, 0, 1), (0.0, 13.6, -.05))
    mk.text_stroke(g, 'MERIDIAN BIOCONTAINMENT', 0.0, 11.4, -.03, .07, F['cladding-teal'], T, style='flat', w=.05, d=.004)
    for (x, y, r) in ((-6.4, .8, .7), (-6.8, 2.2, .5), (6.5, 1.0, .8), (6.2, 2.6, .45), (-5.6, 10.9, .6), (4.9, 11.2, .7), (6.9, 8.6, .5)):
        depth = .2 if y < 3.1 else .6
        mk.ellipsoid(g, (x, y, -.02), (r, r * .8), F['growth-skin'], 7 if T.detail else 5, rings=3, axis=(0, 0, 1), top=1.0, bottom=0.0) if False else \
            mk.ellipsoid(g, (x, y, -.02 - depth * .5), (r, depth * .5), F['growth-skin'], 7 if T.detail else 5, rings=3, axis=(0, 0, -1))
        if T.detail:
            mk.ellipsoid(g, (x + .2, y + .1, -.02 - depth * .8), (r * .3, depth * .2), glow(VIOLET, 1.4), 5, rings=2, axis=(0, 0, -1))


def _door_station(g, T, E, st):
    """Central Station's Station Road portico: brick and stone, an iron-and-glass canopy cantilevered on brackets over the apron,
    CENTRAL STATION in cast letters, the clock at 6:47 and the departure board, cream and green tile below."""
    _surround(g, T, F['stone-dress'], w=.55, d=.22)
    for sgn in (-1, 1):
        x0, x1 = sorted((sgn * 5.6, sgn * DOOR_X))
        bx(g, T, x0, x1, .6, 3.0, -.06, -.02, F['tile-cream'], bevel=.005, skip=('+z',))
        bx(g, T, x0, x1, .6, 1.2, -.08, -.06, F['tile-green'], bevel=.005)
    y, D = 5.2, 4.4
    for x in (-7.2, -2.4, 2.4, 7.2):
        lk.scroll_bracket(g, T, (x, y, -.02), (x, y, -D), F['cast-green'], w=.1, curl=.25)
    bx(g, T, -7.5, 7.5, y, y + .2, -D, -.02, F['cast-green'], bevel=.02)
    g.face(((-7.4, y + .22, -D + .1), (7.4, y + .22, -D + .1), (7.4, y + .9, -.1), (-7.4, y + .9, -.1)), F['glass-canopy'], (0, .99, -.15))
    k = 30 if T.detail else 10
    for i in range(k):
        x = -7.5 + i * 15.0 / k
        g.face(((x, y, -D), (x + 15.0 / k, y, -D), (x + 7.5 / k, y - .35, -D)), F['cast-green'], (0, 0, -1))
    for x in (-4.8, 0.0, 4.8):
        lk.lantern(g, T, x, y - .02, -D + 1.2, s=.7, hang=.2)
    bx(g, T, -7.2, 7.2, 10.2, 11.6, -.18, -.02, F['stone-dress'], bevel=.02)
    mk.text_stroke(g, 'CENTRAL STATION', 0.0, 10.5, -.18, .1, F['cast-green'], T, style='cast', w=.08, d=.04)
    bx(g, T, -2.0, 2.0, 11.9, 15.3, -.1, -.02, F['stone-dress'], bevel=.02)
    sub = Geo()
    mk.clock(sub, T, 1.4, glow(OPAL, 1.6), F['brass'], F['iron-black'], F['iron-black'], roman=True, depth=.06, rim=.14)
    mk.part(g, sub, (0, 0, 0, 1), (0.0, 13.6, -.12))
    bx(g, T, -4.2, 4.2, 6.2, 7.6, -.5, -.3, F['board-black'], bevel=.02)
    for k2 in range(2):
        mk.rod(g, (-3.5 + k2 * 7.0, 7.6, -.4), (-3.5 + k2 * 7.0, MOUTH_H, -.1), .03, F['iron-black'], 4, caps=False)
    mk.text_stroke(g, '6:47 EVACUATION SPECIAL', 0.0, 7.0, -.51, .045, glow(0xFFC27A, 2.0), T, style='flat', w=.035, d=.003)
    mk.text_stroke(g, 'DELAYED', 0.0, 6.45, -.51, .045, glow(0xFF3A2E, 2.2), T, style='flat', w=.035, d=.003)


def _door_ironworks(g, T, E, st):
    """City Ironworks' Furnace House No. 3: buff and blue brick, the riveted board with CITY IRONWORKS, the CLOCK IN board, a steam
    whistle on its pipe, caged orange work lamps and a jib crane out over the court, the lintel blackened by heat."""
    _surround(g, T, F['brick-blue'], w=.6, d=.22)
    bx(g, T, -MOUTH_X, MOUTH_X, MOUTH_H, MOUTH_H + .6, -.24, -.2, F['iron-heat'], bevel=.01)
    bx(g, T, -6.0, 6.0, 11.0, 12.6, -.2, -.02, F['iron-raw'], bevel=.02)
    mk.text_stroke(g, 'CITY IRONWORKS', 0.0, 11.35, -.2, .11, F['cream-letter'], T, style='cast', w=.09, d=.04)
    if T.detail:
        for i in range(21):
            for yy in (11.08, 12.52):
                mk.ellipsoid(g, (-5.8 + i * .58, yy, -.21), (.03, .03), F['iron-raw'], 4, rings=2)
    bx(g, T, 5.7, 7.1, 1.4, 2.8, -.12, -.02, F['timber-old'], bevel=.01)
    mk.text_stroke(g, 'CLOCK IN', 6.4, 2.35, -.12, .025, F['cream-letter'], T, style='flat', w=.02, d=.003)
    mk.rod(g, (-6.6, 9.4, -.4), (-6.6, 13.6, -.4), .07, F['brass'], 6)
    mk.seg(g, T, (-6.6, 9.4, -.02), (-6.6, 9.4, -.4), .1, .1, F['brass'])
    sk.lathe(g, [(.12, 13.6), (.18, 13.7), (.18, 14.3), (.1, 14.5), (.02, 14.7)], (-6.6, 0.0, -.4), F['brass'], 8, smooth=False)
    for x in (-6.6, 6.6):
        sub = Geo()
        mk.caged_lamp(sub, T, F['iron-black'], glow(CAGE, 2.8), F['iron-black'], length=.36, r=.13)
        mk.part(g, sub, (0, 0, 0, 1), (x, 7.0, -.02))
    xj = 6.9
    bx(g, T, xj - .2, xj + .2, 9.6, 15.4, -.6, -.2, F['iron-raw'], bevel=.01)
    lk.lattice(g, T, (xj, 14.6, -.4), (xj - 5.5, 14.6, -6.0), (0, 1, 0), .8, F['iron-raw'], bays=6, w=.1)
    mk.seg(g, T, (xj, 10.0, -.4), (xj - 5.5, 14.6, -6.0), .1, .1, F['iron-raw'])
    mk.rod(g, (xj - 5.2, 14.5, -5.7), (xj - 5.2, 8.0, -5.7), .02, F['iron-black'], 4, caps=False)
    bx(g, T, xj - 5.45, xj - 4.95, 7.4, 8.0, -5.95, -5.45, F['paint-yellow'], bevel=.02)
    g.face(((-3.0, 15.0, -.04), (3.0, 15.0, -.04), (3.0, 15.7, -.04), (-3.0, 15.7, -.04)), glow(FURNACE, 1.3), (0, 0, -1))


HERO = {'level0': _door_level0, 'poolrooms': _door_poolrooms, 'wardzero': _door_wardzero, 'station': _door_station, 'ironworks': _door_ironworks}
