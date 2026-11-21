"""Meridian street kit, pass 2: what the pass-2 modules share, on top of pass 1's mkit.py (the frames, the 5 x 7 glyph kit, the
6:47 clock, the crest, the transit mark, the outward sweep) and stagekit.

Added here:
- the sweep fix installed for this process (sweepfix.py): sk.sweep and sk.tube build outward pipes, as mkit's own sweep does;
- the pass-2 finishes (bible 4.2 and 3.4: the survivors' spray paint and chalk, poster papers and inks, rust, wet asphalt, the
  furniture of the barricades, the Civil Defence hessian);
- a hand font (polyline skeletons in a 4 x 6 box) drawn three ways: sprayed (a flared stroke, overspray specks, drips on
  walls), chalked (two thin, wobbling passes) and marker (one even stroke, for the MISSING bills);
- hand arrows (stroked and filled), star-shaped puddles with their wet rims (a fan of triangles, so any wobbling outline is
  safe), torn poster sheets (strips under a torn edge), rust runs and drip stains as thin layered faces.

Everything is in Unity space (metres, +y up), in each module's own frame (see mkit.py). Nothing licensed: every shape is drawn
from the art bible's words.
"""
import math
import os
import random
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
STREET = os.path.dirname(HERE)
for _p in (HERE, STREET):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import mkit as mk  # noqa: E402  (pass 1's kit: it puts tools/stages on the path)
import numpy as np  # noqa: E402
import stagekit as sk  # noqa: E402
import sweepfix  # noqa: E402

F = mk.F
_ORIG_LOFT = mk.loft


def loft_outward(g, rings, f, close0=False, close1=False, closed=True, fins=None, smooth=True):
    """mkit.loft with ONE outward sign for the whole grid (away from the grid's centre) instead of one per ring, and a
    degenerate corner's normal borrowed from the next ring. Pass 1's per-ring sign turned faces in wherever a ring shrank to a
    line (the Vale's rear window, raked down to its deck: inside out, 0.29 m2, found by audit.py)."""
    m, k = len(rings), len(rings[0])
    R = np.array(rings, dtype=np.float64)
    if closed:
        du = np.roll(R, -1, axis=1) - np.roll(R, 1, axis=1)
    else:
        du = np.empty_like(R)
        du[:, 1:-1] = R[:, 2:] - R[:, :-2]
        du[:, 0] = R[:, 1] - R[:, 0]
        du[:, -1] = R[:, -1] - R[:, -2]
    dv = np.empty_like(R)
    dv[1:-1] = R[2:] - R[:-2]
    dv[0] = R[1] - R[0]
    dv[-1] = R[-1] - R[-2]
    N = np.cross(du, dv)
    ln = np.linalg.norm(N, axis=2)
    scale_ = max(1e-12, float(np.median(ln)) * 1e-3)
    for i in list(range(m)) + list(range(m - 1, -1, -1)):
        for j in range(k):
            if ln[i, j] < scale_:
                for i2 in (i - 1, i + 1):
                    if 0 <= i2 < m and ln[i2, j] >= scale_:
                        N[i, j], ln[i, j] = N[i2, j], ln[i2, j]
                        break
    ctr = R.reshape(-1, 3).mean(axis=0)
    flip = 1.0 if np.sum(N * (R - ctr)) >= 0 else -1.0
    N = N * flip
    N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-12)
    jn = k if closed else k - 1
    for i in range(m - 1):
        for j in range(jn):
            j1 = (j + 1) % k
            ff = f if fins is None else fins(i, j)
            pts = (tuple(R[i, j]), tuple(R[i, j1]), tuple(R[i + 1, j1]), tuple(R[i + 1, j]))
            nn = (tuple(N[i, j]), tuple(N[i, j1]), tuple(N[i + 1, j1]), tuple(N[i + 1, j]))
            if smooth:
                g.face(pts, ff, normals=nn)
            else:
                avg = tuple(sum(x[q] for x in nn) for q in range(3))
                g.face(pts, ff, normal=avg)
    for (i, on, nb) in ((0, close0, 1), (m - 1, close1, m - 2)):
        if not on:
            continue
        axis = sk._norm(tuple(R[i].mean(axis=0) - R[nb].mean(axis=0)))
        g.face([tuple(p) for p in R[i]], f, axis)


def install_fixes():
    """The Meridian fixes for this process: stagekit's sweep (and tube) outward; mkit's loft with one outward sign; pass 1's
    three inside-out lathes and drape (pass1fix.py)."""
    import pass1fix
    sweepfix.install()
    mk.loft = loft_outward
    pass1fix.install()


def uninstall_fixes():
    import pass1fix
    sweepfix.uninstall()
    mk.loft = _ORIG_LOFT
    pass1fix.uninstall()


install_fixes()
Fr, Module = mk.Fr, mk.Module
add, sub, cross, dot, norm, lerp, scale = mk.add, mk.sub, mk.cross, mk.dot, mk.norm, mk.lerp, mk.scale
rot_y, lathe, tube, bar, rod, box, fbox, quad, poly = mk.rot_y, mk.lathe, mk.tube, mk.bar, mk.rod, mk.box, mk.fbox, mk.quad, mk.poly

# printed punctuation the posters need (the 5 x 7 kit, as pass 1's glyphs)
mk.GLYPHS.setdefault('!', '1,1,1,1,1,0,1')
mk.GLYPHS.setdefault(',', '0,0,0,0,0,1,1')
mk.GLYPHS.setdefault('?', '01110,10001,00001,00110,00100,00000,00100')
mk.GLYPHS.setdefault('(', '01,10,10,10,10,10,01')
mk.GLYPHS.setdefault(')', '10,01,01,01,01,01,10')
mk.GLYPHS.setdefault('+', '000,000,010,111,010,000,000')
mk.GLYPHS.setdefault('<', '0001,0010,0100,1000,0100,0010,0001')

_DONE = []


def finishes():
    """pass 1's finishes, then pass 2's (each made once)."""
    mk.finishes()
    if _DONE:
        return F

    def add_(name, hexcol, detail=sk.DETAIL_NONE, **kw):
        if name not in F:
            F[name] = sk.fin(name, hexcol, detail, **kw)
    # the survivors' marks (bible 3.4: spray white #F0EEE6 and orange #FF8A3A; chalk)
    add_('spray-white', 0xF0EEE6, smooth=.35, dirt=.45, wear=.35)
    add_('spray-orange', 0xFF8A3A, smooth=.35, dirt=.45, wear=.35)
    add_('chalk', 0xCFCBBE, smooth=.05, dirt=.35)
    # posters (bible 4.1: flat fields of colour with stroke titles)
    add_('paper-cream', 0xE6DCBF, smooth=.12, dirt=.7)
    add_('paper-white', 0xE9E7DF, smooth=.12, dirt=.6)
    add_('paper-back', 0xCFC9B6, smooth=.1, dirt=.7)
    add_('paper-soaked', 0xA89C80, smooth=.45, dirt=.8)
    add_('paper-olive', 0x5B6B3A, smooth=.15, dirt=.6)
    add_('ink-red', 0xA82A2A, smooth=.15, dirt=.3)
    add_('ink-black', 0x1C1C22, smooth=.15, dirt=.2)
    add_('ink-white', 0xECEBE2, smooth=.15, dirt=.3)
    add_('photo-dark', 0x34343A, smooth=.5, dirt=.2)
    add_('photo-mid', 0x7A7A80, smooth=.5, dirt=.2)
    add_('tape', 0xCBBF98, smooth=.55, dirt=.4)
    # the long night's stains
    add_('rust', 0x7A4426, smooth=.18, dirt=.5)
    add_('rust-dark', 0x4A2A1C, smooth=.2, dirt=.4)
    add_('stain', 0x2E2A2A, smooth=.6, dirt=.2)
    add_('wet-asphalt', 0x22242B, smooth=.72, dirt=.3)
    add_('grime-water', 0x2A2C30, smooth=.85, dirt=0)
    # the clock's opal dial and its unlit glass
    add_('opal', 0xEDE2C4, smooth=.7, dirt=.35, wear=.1)
    # furniture of the barricades (bible 4.2's timber, paint and cloth; original pieces)
    add_('wood-dark', 0x5A3E2A, sk.DETAIL_WOOD, smooth=.3, dirt=1.0, wear=.2, param=1)
    add_('wood-dark-x', 0x5A3E2A, sk.DETAIL_WOOD, smooth=.3, dirt=1.0, wear=.2, param=0)
    add_('wood-mid', 0x8A6A48, sk.DETAIL_WOOD, smooth=.25, dirt=1.0, wear=.2, param=0)
    add_('wood-mid-y', 0x8A6A48, sk.DETAIL_WOOD, smooth=.25, dirt=1.0, wear=.2, param=1)
    add_('wood-raw', 0xA88A62, sk.DETAIL_WOOD, smooth=.15, dirt=1.1, moss=.2, wear=.1, param=0)
    add_('wood-raw-z', 0xA88A62, sk.DETAIL_WOOD, smooth=.15, dirt=1.1, moss=.2, wear=.1, param=2)
    add_('paint-door-green', 0x2C5A48, sk.DETAIL_WOOD, smooth=.45, dirt=.8, wear=.3, param=1)
    add_('paint-door-blue', 0x33507A, sk.DETAIL_WOOD, smooth=.45, dirt=.8, wear=.3, param=1)
    add_('fabric-green', 0x3E5A44, sk.DETAIL_CLOTH, smooth=.1, dirt=.8)
    add_('fabric-rose', 0x8A4E4E, sk.DETAIL_CLOTH, smooth=.1, dirt=.8)
    add_('fabric-mustard', 0xA8863A, sk.DETAIL_CLOTH, smooth=.1, dirt=.8)
    add_('mattress', 0xD6CDB6, sk.DETAIL_CLOTH, smooth=.1, dirt=1.0)
    add_('mattress-stripe', 0x6E84A2, sk.DETAIL_CLOTH, smooth=.1, dirt=1.0)
    add_('enamel-white', 0xE4E0D4, smooth=.6, dirt=.6, wear=.2)
    add_('steel-grey', 0x6E747A, smooth=.4, metal=.5, dirt=.9, wear=.3)
    add_('hessian', 0x8C7C58, sk.DETAIL_CLOTH, smooth=.1, dirt=1.0, moss=.2)
    add_('hessian-dark', 0x6A5C40, sk.DETAIL_CLOTH, smooth=.1, dirt=1.0, moss=.2)
    add_('corr-rust', 0x8A4A2A, smooth=.3, metal=.4, dirt=1.0, wear=.3)
    add_('corr-teal', 0x3A7A7A, smooth=.3, metal=.4, dirt=1.0, wear=.3)
    add_('wire', 0x5A5C5E, smooth=.4, metal=.7, dirt=.6)
    add_('underbody', 0x2A2826, smooth=.25, metal=.3, dirt=1.2, wear=.1)
    _DONE.append(1)
    return F


glow = mk.glow


# ---------------------------------------------------------------------------------------------------------------------------
# The hand font: skeletons in a 4 x 6 box (baseline 0, cap height 6)
# ---------------------------------------------------------------------------------------------------------------------------
_O = [(1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 1), (3, 0), (1, 0)]
SKEL = {
    'A': [[(0, 0), (2, 6), (4, 0)], [(.8, 2.3), (3.2, 2.3)]],
    'B': [[(0, 0), (0, 6), (2.8, 6), (3.6, 5.3), (3.6, 3.8), (2.8, 3.1), (0, 3.1)], [(2.8, 3.1), (3.9, 2.4), (3.9, .8), (3, 0), (0, 0)]],
    'C': [[(3.9, 5.1), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (3.9, .9)]],
    'D': [[(0, 0), (0, 6), (2.3, 6), (3.6, 5), (4, 3), (3.6, 1), (2.3, 0), (0, 0)]],
    'E': [[(4, 6), (0, 6), (0, 0), (4, 0)], [(0, 3.1), (3, 3.1)]],
    'F': [[(4, 6), (0, 6), (0, 0)], [(0, 3.1), (3, 3.1)]],
    'G': [[(3.9, 5.1), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (4, 1), (4, 2.8), (2.2, 2.8)]],
    'H': [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 3), (4, 3)]],
    'I': [[(2, 0), (2, 6)], [(.9, 6), (3.1, 6)], [(.9, 0), (3.1, 0)]],
    'J': [[(4, 6), (4, 1.2), (3, 0), (1, 0), (0, 1.2)]],
    'K': [[(0, 0), (0, 6)], [(4, 6), (0, 2.4)], [(1.3, 3.4), (4, 0)]],
    'L': [[(0, 6), (0, 0), (4, 0)]],
    'M': [[(0, 0), (0, 6), (2, 2.6), (4, 6), (4, 0)]],
    'N': [[(0, 0), (0, 6), (4, 0), (4, 6)]],
    'O': [_O],
    'P': [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 3.8), (3, 2.8), (0, 2.8)]],
    'Q': [_O, [(2.4, 1.6), (4.2, -.4)]],
    'R': [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 3.9), (3, 3), (0, 3)], [(1.8, 3), (4, 0)]],
    'S': [[(4, 5.1), (3.2, 6), (.9, 6), (0, 5.1), (0, 4), (.9, 3.2), (3.1, 2.8), (4, 2), (4, .9), (3.1, 0), (.8, 0), (0, .8)]],
    'T': [[(0, 6), (4, 6)], [(2, 6), (2, 0)]],
    'U': [[(0, 6), (0, 1), (1, 0), (3, 0), (4, 1), (4, 6)]],
    'V': [[(0, 6), (2, 0), (4, 6)]],
    'W': [[(0, 6), (1, 0), (2, 3.6), (3, 0), (4, 6)]],
    'X': [[(0, 6), (4, 0)], [(4, 6), (0, 0)]],
    'Y': [[(0, 6), (2, 3), (4, 6)], [(2, 3), (2, 0)]],
    'Z': [[(0, 6), (4, 6), (0, 0), (4, 0)]],
    '0': [_O],
    '1': [[(1, 4.6), (2.4, 6), (2.4, 0)]],
    '2': [[(0, 4.8), (1, 6), (3, 6), (4, 5), (4, 3.8), (0, 0), (4, 0)]],
    '3': [[(0, 5.2), (1, 6), (3, 6), (4, 5), (4, 4), (3, 3.1), (1.4, 3.1)], [(3, 3.1), (4, 2.1), (4, 1), (3, 0), (1, 0), (0, .8)]],
    '4': [[(3, 0), (3, 6), (0, 1.8), (4, 1.8)]],
    '5': [[(4, 6), (.4, 6), (0, 3.2), (2.8, 3.6), (4, 2.6), (4, 1), (3, 0), (1, 0), (0, .8)]],
    '6': [[(3.6, 6), (1.6, 6), (0, 4), (0, 1), (1, 0), (3, 0), (4, 1), (4, 2.4), (3, 3.4), (1, 3.4), (0, 2.4)]],
    '7': [[(0, 6), (4, 6), (1.4, 0)]],
    '8': [[(1, 3.2), (0, 4.2), (0, 5), (1, 6), (3, 6), (4, 5), (4, 4.2), (3, 3.2), (1, 3.2), (0, 2.2), (0, 1), (1, 0), (3, 0),
           (4, 1), (4, 2.2), (3, 3.2)]],
    '9': [[(.4, 0), (2.4, 0), (4, 2), (4, 5), (3, 6), (1, 6), (0, 5), (0, 3.6), (1, 2.6), (3, 2.6), (4, 3.6)]],
    '-': [[(.6, 3), (3.4, 3)]],
    '.': [[(1.7, 0), (2.3, .35)]],
    ':': [[(1.8, .9), (2.2, 1.4)], [(1.8, 4.1), (2.2, 4.6)]],
    '>': [[(.5, 6), (3.8, 3), (.5, 0)]],
    '<': [[(3.5, 6), (.2, 3), (3.5, 0)]],
    '!': [[(2, 6), (2, 1.8)], [(1.8, 0), (2.2, .35)]],
    '?': [[(0, 5), (1, 6), (3, 6), (4, 5), (4, 4), (2, 2.8), (2, 1.8)], [(1.8, 0), (2.2, .35)]],
    '/': [[(0, 0), (4, 6)]],
    "'": [[(2, 6), (1.8, 4.6)]],
    '+': [[(2, 1), (2, 5)], [(0, 3), (4, 3)]],
    ' ': [],
}
ADV = 5.4          # the advance of a hand glyph, in skeleton units (the box is 4 wide)


def hand_width(s, h):
    u = h / 6.0
    return max(0.0, (len(s) * ADV - (ADV - 4.0)) * u)


def _strip(g, fr, pts, w0, w1, c, f, round_ends=True):
    """A stroke along a polyline [(a, b)] on a frame's face at depth c, its width eased from w0 to w1 (mitred joints, so a
    stroke never overlaps itself). Returns nothing; faces wind to the frame's normal."""
    n = len(pts)
    if n < 2:
        return
    # each point's half-width and its offset direction (the mitre of the two segments' normals)
    L = [0.0]
    for i in range(1, n):
        L.append(L[-1] + math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]))
    tot = L[-1] or 1.0
    left, right = [], []
    for i in range(n):
        if i == 0:
            d = (pts[1][0] - pts[0][0], pts[1][1] - pts[0][1])
            nx, ny = -d[1], d[0]
        elif i == n - 1:
            d = (pts[-1][0] - pts[-2][0], pts[-1][1] - pts[-2][1])
            nx, ny = -d[1], d[0]
        else:
            d0 = (pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1])
            d1 = (pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1])
            l0, l1 = math.hypot(*d0) or 1.0, math.hypot(*d1) or 1.0
            n0 = (-d0[1] / l0, d0[0] / l0)
            n1 = (-d1[1] / l1, d1[0] / l1)
            nx, ny = n0[0] + n1[0], n0[1] + n1[1]
            ln = math.hypot(nx, ny)
            if ln < 1e-6:
                nx, ny = n0
            else:
                nx, ny = nx / ln, ny / ln
                cosh = max(.35, nx * n0[0] + ny * n0[1])     # the mitre's stretch, capped on sharp turns
                nx, ny = nx / cosh, ny / cosh
        if i in (0, n - 1):
            ln = math.hypot(nx, ny) or 1.0
            nx, ny = nx / ln, ny / ln
        hw = (w0 + (w1 - w0) * L[i] / tot) / 2
        left.append((pts[i][0] + nx * hw, pts[i][1] + ny * hw))
        right.append((pts[i][0] - nx * hw, pts[i][1] - ny * hw))
    for i in range(n - 1):
        q = [right[i], right[i + 1], left[i + 1], left[i]]
        g.face([fr.P(a, b, c) for (a, b) in q], f, fr.n)
    if round_ends:
        for (i, j) in ((0, 1), (n - 1, n - 2)):
            # a small cap: a triangle past the end, so a stroke ends round-ish rather than square
            d = (pts[i][0] - pts[j][0], pts[i][1] - pts[j][1])
            ld = math.hypot(*d) or 1.0
            hw = (w0 if i == 0 else w1) / 2
            tip = (pts[i][0] + d[0] / ld * hw * .8, pts[i][1] + d[1] / ld * hw * .8)
            g.face([fr.P(left[i][0], left[i][1], c), fr.P(right[i][0], right[i][1], c), fr.P(tip[0], tip[1], c)], f, fr.n)


def _jitter(pts, r, amp):
    return [(x + r.uniform(-amp, amp), y + r.uniform(-amp, amp)) for (x, y) in pts]


def _subdivide(pts, step):
    out = [pts[0]]
    for i in range(1, len(pts)):
        a, b = pts[i - 1], pts[i]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        k = max(1, int(L / step))
        for t in range(1, k + 1):
            out.append((a[0] + (b[0] - a[0]) * t / k, a[1] + (b[1] - a[1]) * t / k))
    return out


def hand_text(g, fr, a, b, h, s, f, style='spray', c=0.0, seed=0, align='left', slant=.08, lod=0, wall=False):
    """Hand lettering on a frame's face: cap height h, its foot at b, starting at a (align left), centred on a or ending at a.
    style: 'spray' (a 0.16 h stroke, flared, overspray specks; drips when `wall`), 'chalk' (two thin wobbling passes),
    'marker' (one even stroke). Returns (a0, a1, drip feet)."""
    r = random.Random(seed * 7919 + len(s) * 31 + int(h * 1000))
    u = h / 6.0
    w = hand_width(s, h)
    x0 = a if align == 'left' else (a - w / 2 if align == 'centre' else a - w)
    feet = []
    sw = {'spray': .16, 'chalk': .085, 'marker': .12}[style] * h
    amp = {'spray': .18, 'chalk': .22, 'marker': .1}[style]
    x = x0
    for ch in s.upper():
        strokes = SKEL.get(ch, [])
        base_y = b + r.uniform(-.25, .25) * u * (1.0 if style != 'marker' else .5)
        for st in strokes:
            pts = [(x + (px + py * slant) * u, base_y + py * u) for (px, py) in st]
            if style == 'chalk' and lod == 0:
                pts = _subdivide(pts, 1.4 * u)
            if lod == 0:
                pts = _jitter(pts, r, amp * u)
            if style == 'spray':
                _strip(g, fr, pts, sw * r.uniform(.85, 1.1), sw * r.uniform(.95, 1.25), c, f, round_ends=lod == 0)
                if lod == 0:
                    # overspray: a few specks beside the stroke
                    for _ in range(2):
                        i = r.randrange(len(pts))
                        ang = r.uniform(0, 2 * math.pi)
                        rr = sw * r.uniform(.8, 1.6)
                        cx, cy = pts[i][0] + math.cos(ang) * rr, pts[i][1] + math.sin(ang) * rr
                        sz = sw * r.uniform(.12, .25)
                        g.face([fr.P(cx - sz, cy - sz, c), fr.P(cx + sz, cy - sz, c), fr.P(cx + sz * .6, cy + sz, c)], f, fr.n)
                    # the lowest point of the stroke runs (drips) on a wall
                    if wall:
                        lo = min(pts, key=lambda p: p[1])
                        if r.random() < .55:
                            feet.append(lo)
            elif style == 'chalk':
                _strip(g, fr, pts, sw, sw * .8, c, f, round_ends=False)
                if lod == 0 and r.random() < .7:
                    p2 = _jitter(pts, r, amp * u * .8)
                    _strip(g, fr, p2, sw * .55, sw * .45, c + .0006, f, round_ends=False)
            else:
                _strip(g, fr, pts, sw, sw, c, f, round_ends=lod == 0)
        x += ADV * u
    if wall and style == 'spray' and lod == 0:
        for (px, py) in feet:
            drip(g, fr, px, py - sw * .3, r.uniform(.04, .22), sw * .45, c, f)
    return x0, x0 + w, feet


def drip(g, fr, a, b, length, w, c, f):
    """A paint drip running down from (a, b): a thin tapered run ending in a bead."""
    g.face([fr.P(a - w / 2, b, c), fr.P(a + w / 2, b, c), fr.P(a + w * .18, b - length, c), fr.P(a - w * .18, b - length, c)], f, fr.n)
    g.face([fr.P(a - w * .35, b - length, c), fr.P(a + w * .35, b - length, c), fr.P(a + w * .2, b - length - w * .5, c),
            fr.P(a - w * .2, b - length - w * .5, c)], f, fr.n)


def hand_arrow(g, fr, a, b, L, h, f, style='spray', c=0.0, seed=0, direction=1, lod=0, filled=False):
    """A hand-drawn arrow from (a, b) along the frame's +a (direction 1) or -a (-1): L long, its head h tall. filled: a sprayed
    solid arrow (a shaft and a head, jittered), else stroked."""
    r = random.Random(seed * 131 + 7)
    sw = {'spray': .1, 'chalk': .05, 'marker': .07}[style] * h * 2
    d = direction
    if filled:
        sh = h * .38
        jit = (lambda: r.uniform(-.04, .04) * h) if lod == 0 else (lambda: 0.0)
        x1 = a + d * (L - h * .9)
        shaft = [(a, b - sh / 2 + jit()), (x1, b - sh / 2 + jit()), (x1, b + sh / 2 + jit()), (a, b + sh / 2 + jit())]
        if d < 0:
            shaft = [shaft[1], shaft[0], shaft[3], shaft[2]]
        g.face([fr.P(p[0], p[1], c) for p in shaft], f, fr.n)
        head = [(x1 - d * .02 * h, b - h / 2 + jit()), (a + d * L + jit() * d, b + jit()), (x1 - d * .02 * h, b + h / 2 + jit())]
        g.face([fr.P(p[0], p[1], c) for p in head], f, fr.n)
        return
    shaft = [(a, b), (a + d * L * .5, b + r.uniform(-.03, .03) * h), (a + d * L, b)]
    if lod == 0:
        shaft = _jitter(shaft, r, .03 * h)
    tip = shaft[-1]
    _strip(g, fr, shaft, sw, sw * 1.1, c, f, round_ends=lod == 0)
    for s_ in (1, -1):
        hd = [(tip[0] - d * h * .55, tip[1] + s_ * h * .5), tip]
        if lod == 0:
            hd = [(hd[0][0] + r.uniform(-.05, .05) * h, hd[0][1] + r.uniform(-.05, .05) * h), tip]
        _strip(g, fr, hd, sw, sw * 1.05, c + .0003 * (s_ + 1), f, round_ends=lod == 0)


# ---------------------------------------------------------------------------------------------------------------------------
# Puddles (bible 4.2: standing water as layered flat faces, smoothness .88) and their wet rims
# ---------------------------------------------------------------------------------------------------------------------------
def blob(r, cx, cz, R, n, aspect=1.0, wobble=.22, yaw=0.0):
    """A star-shaped wobbling outline [(x, z)] about (cx, cz): radius R, n points, stretched by `aspect` along its own x."""
    ph = [r.uniform(0, 2 * math.pi) for _ in range(4)]
    amps = [wobble * k for k in (.55, .3, .2, .12)]
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        k = 1 + sum(amps[j] * math.sin((j + 2) * t + ph[j]) for j in range(4))
        x, z = R * k * math.cos(t) * aspect, R * k * math.sin(t)
        c, s = math.cos(yaw), math.sin(yaw)
        out.append((cx + x * c - z * s, cz + x * s + z * c))
    return out


def fan(g, centre, pts, y, f):
    """A star-shaped polygon as a fan of triangles from its centre (safe for any wobble), facing up."""
    cx, cz = centre
    for i in range(len(pts)):
        p, q = pts[i], pts[(i + 1) % len(pts)]
        g.face(((cx, y, cz), (p[0], y, p[1]), (q[0], y, q[1])), f, (0, 1, 0))


def ring_fan(g, centre, inner, k_out, pad, y, f):
    """The band between an outline and the same outline grown by k_out (about the centre) plus pad, as triangles."""
    cx, cz = centre
    outer = []
    for (x, z) in inner:
        dx, dz = x - cx, z - cz
        ln = math.hypot(dx, dz) or 1.0
        outer.append((cx + dx * k_out + dx / ln * pad, cz + dz * k_out + dz / ln * pad))
    n = len(inner)
    for i in range(n):
        a, b = inner[i], inner[(i + 1) % n]
        c, d = outer[i], outer[(i + 1) % n]
        g.face(((a[0], y, a[1]), (c[0], y, c[1]), (d[0], y, d[1])), f, (0, 1, 0))
        g.face(((a[0], y, a[1]), (d[0], y, d[1]), (b[0], y, b[1])), f, (0, 1, 0))
    return outer


def puddle(g, T, r, cx, cz, y, R, aspect=1.0, yaw=0.0, rim=True, wobble=.22):
    """A puddle with its wet rim: the water (puddle, .88) 6 mm up, the rim of wet asphalt 3 mm up (flat paint, under 6 cm)."""
    n = T.pick(16, 7)
    pts = blob(r, cx, cz, R, n, aspect, wobble, yaw)
    if rim and T.lod == 0:
        ring_fan(g, (cx, cz), pts, 1.12, .07, y + .003, F['wet-asphalt'])
    fan(g, (cx, cz), pts, y + .006, F['puddle'])
    return pts


# ---------------------------------------------------------------------------------------------------------------------------
# Paper: torn sheets, tape, run-off
# ---------------------------------------------------------------------------------------------------------------------------
def sheet(g, fr, a0, b0, w, h, c, f, torn_bottom=None, torn_right=None, r=None, steps=10):
    """A rectangle of paper on a frame's face at depth c, from (a0, b0) w x h; torn_bottom: a ragged lower edge that rises up
    to that many metres (strips under it, so any tear stays convex piecewise); torn_right likewise for the right edge."""
    if torn_bottom is None and torn_right is None:
        quad(g, fr, a0, a0 + w, b0, b0 + h, c, f)
        return
    r = r or random.Random(1)
    if torn_bottom is not None:
        xs = [a0 + w * i / steps for i in range(steps + 1)]
        bot = [b0 + max(0.0, r.uniform(-.2, 1.0)) * torn_bottom * (1 - abs(2 * i / steps - 1) ** 3) for i in range(steps + 1)]
        for i in range(steps):
            g.face([fr.P(xs[i], bot[i], c), fr.P(xs[i + 1], bot[i + 1], c), fr.P(xs[i + 1], b0 + h, c), fr.P(xs[i], b0 + h, c)], f, fr.n)
        return
    ys = [b0 + h * i / steps for i in range(steps + 1)]
    rt = [a0 + w - max(0.0, r.uniform(-.2, 1.0)) * torn_right * (1 - abs(2 * i / steps - 1) ** 3) for i in range(steps + 1)]
    for i in range(steps):
        g.face([fr.P(a0, ys[i], c), fr.P(rt[i], ys[i], c), fr.P(rt[i + 1], ys[i + 1], c), fr.P(a0, ys[i + 1], c)], f, fr.n)


def tape(g, fr, a, b, ang, c, w=.05, h=.022):
    """A strip of tape at (a, b), turned by ang radians."""
    ca, sa = math.cos(ang), math.sin(ang)
    pts = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    g.face([fr.P(a + x * ca - y * sa, b + x * sa + y * ca, c) for (x, y) in pts], F['tape'], fr.n)


def ink_lines(g, fr, a0, a1, b_top, rows, pitch, thick, c, f, r=None, ragged=True):
    """Body text as ink bars: `rows` lines from b_top down, each a run of word bars (ragged right)."""
    r = r or random.Random(3)
    for i in range(rows):
        b = b_top - i * pitch
        x = a0
        end = a1 - (r.uniform(0, (a1 - a0) * .35) if ragged else 0.0)
        while x < end - .01:
            wl = min(end - x, r.uniform(.025, .09) * (a1 - a0) / .5)
            quad(g, fr, x, x + wl, b - thick, b, c, f)
            x += wl + thick * 1.6


def run(g, fr, a, b, length, w, c, f, drift=0.0):
    """A run-off stain from (a, b) downward: tapered, its foot drifting by `drift`."""
    g.face([fr.P(a - w / 2, b, c), fr.P(a + w / 2, b, c), fr.P(a + drift + w * .15, b - length, c), fr.P(a + drift - w * .15, b - length, c)],
           f, fr.n)


def photo(g, fr, a, b, w, h, c):
    """A missing person's photo, small (never life-size; bible 1.7 rule 3): a dark print, a lighter head and shoulders."""
    quad(g, fr, a, a + w, b, b + h, c, F['photo-dark'])
    cx = a + w / 2
    poly(g, fr, mk.ngon_pts(w * .17, 8, 0.0, cx, b + h * .6), c + .0008, F['photo-mid'])
    poly(g, fr, [(cx - w * .36, b + h * .04), (cx + w * .36, b + h * .04), (cx + w * .22, b + h * .36), (cx - w * .22, b + h * .36)],
         c + .0008, F['photo-mid'])


def tris(g):
    return mk.tris(g)
