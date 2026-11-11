"""Ward Zero's modelling helpers (docs/arenas/STAGE-ART.md, docs/arenas/WARDZERO-ART.md), on stagekit: the finishes (the ward's
palette: clinical panels and tiles, painted and stainless steel, containment teal, hazard stripes), a block's faces in the kit's
own terms (axis, sign, u along the face, y up it, d proud of it), clad wall faces (skirting, tiled dado, cap rail, panels with
open joints), floors with openings, round pipes on clamps, striped plates, and the fallback that rebuilds a kit part as a
finished piece. Used by wardzero_art.py. Everything is in the game's space (Unity: metres, +y up)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, obox, sweep, lathe, cylinder  # noqa: E402
import club_kit as ck  # noqa: E402  (rounded boxes, rods, I-beams, rivets, domes)
import emporium_kit as ek  # noqa: E402  (slanted stripes)

F = {}
A = sk
Y = (0.0, 1.0, 0.0)
VOID = 0x050507
PIPES = (0x3E6B5A, 0x3F5D86)


def finishes():
    def add(name, hexcol, detail=A.DETAIL_NONE, **kw):
        F[name] = A.fin(name, hexcol, detail, **kw)
    # floors and walls
    add('tile', 0xAEB8BC, A.DETAIL_TILE, smooth=.72, param=.6, dirt=.8)
    add('tileup', 0xB9C3C6, A.DETAIL_TILE, smooth=.72, param=.6, dirt=.6)
    add('reveal', 0x39444A, smooth=.3, dirt=.5)
    add('panel', 0xC9CFCC, smooth=.46, dirt=.7, wear=.12)
    add('dado', 0x9FAAAC, A.DETAIL_TILE, smooth=.74, param=.3, dirt=.5)
    add('showertile', 0xD8E4E2, A.DETAIL_TILE, smooth=.8, param=.15, dirt=.8)
    add('concrete', 0x6E7472, A.DETAIL_PLASTER, smooth=.22, dirt=.9)
    add('ceiling', 0x2B3745, A.DETAIL_PLASTER, smooth=.12, dirt=.3)
    add('soffit', 0x2C3742, A.DETAIL_PLASTER, smooth=.16, dirt=.3)
    add('beam', 0x43525E, smooth=.4, metal=.4, dirt=.4, wear=.25)
    # metals
    add('steel', 0x5D6B6E, smooth=.56, metal=.6, dirt=.7, wear=.35)
    add('steeldark', 0x2F383B, smooth=.5, metal=.6, dirt=.6, wear=.3)
    add('stainless', 0xB8C0C2, smooth=.78, metal=.85, dirt=.5, wear=.15)
    add('duct', 0x6A747C, smooth=.6, metal=.8, dirt=.6, wear=.2)
    add('grate', 0x4E5560, smooth=.45, metal=.7, dirt=.7, wear=.3)
    add('plate', 0x4A5560, smooth=.48, metal=.7, dirt=.9, wear=.4)
    add('rubber', 0x1A1D1E, smooth=.22, dirt=.5)
    add('void', VOID, smooth=.05, dirt=0.0)
    add('paint', 0xC8CC9A, smooth=.3, dirt=.5)
    add('stripe-yellow', 0xF2B21C, smooth=.42, dirt=.6, wear=.2)
    add('stripe-black', 0x1C1C20, smooth=.36, dirt=.5, wear=.2)
    # furniture
    add('mattress', 0xD7DDE0, A.DETAIL_CLOTH, smooth=.2, dirt=.5)
    add('cloth', 0x8FB8B0, A.DETAIL_CLOTH, smooth=.16, dirt=.5)
    add('fairing', 0x7C8C90, smooth=.5, dirt=.6)
    add('booth', 0x6F8A84, smooth=.5, dirt=.6, wear=.15)
    add('locker', 0x3F7F78, smooth=.55, metal=.3, dirt=.7, wear=.3)
    add('white', 0xD8DCDC, smooth=.6, dirt=.4)
    add('red', 0xC02830, smooth=.6, dirt=.3)
    add('glass', 0x1F4F48, smooth=.93, metal=.2, dirt=.15)
    add('vessel', 0x17403A, smooth=.9, metal=.3, dirt=.3)
    add('window', 0x7FA8A0, glow=.42)
    add('shardglow', 0x2FA890, glow=.4)
    add('throat', 0x0B0807, smooth=.1, dirt=0.0)


def cfin(hexcol, kind='paint', gain=0.0):
    """A finish for a kit colour: 'metal' (painted steel), 'paint' (matt), 'glow' (emissive at `gain`)."""
    name = '%s-%06x-%d' % (kind, hexcol, int(round(gain * 100)))
    if name not in F:
        if kind == 'glow':
            F[name] = A.fin(name, hexcol, glow=max(.05, gain))
        elif kind == 'metal':
            lum = ((hexcol >> 16 & 255) + (hexcol >> 8 & 255) + (hexcol & 255)) / 765.0
            F[name] = A.fin(name, hexcol, smooth=.55 + .2 * lum, metal=.55 + .25 * lum, dirt=.7, wear=.3)
        else:
            F[name] = A.fin(name, hexcol, smooth=.34, dirt=.6)
    return F[name]


def pfin(p):
    """The finish of a kit part by its look and colour."""
    look, col = p['look'], p['col']
    if col == VOID:
        return F['void']
    if look == 'cyber-metal':
        return {0x5D6B6E: F['steel'], 0x2F383B: F['steeldark'], 0xB8C0C2: F['stainless'], 0x4A5560: F['plate']}.get(col) or cfin(col, 'metal')
    if look == 'ward-panel':
        return F['dado']
    if look == 'ward-tile':
        return F['showertile']
    if look == 'cyber-grate':
        return F['grate']
    if look == 'window-lit':
        return F['window']
    if look in ('cyber-tube', 'cyber-holo', 'ward-light', 'ward-glass'):
        return cfin(col, 'glow', p['gain']) if p['gain'] >= .5 else cfin(col, 'paint')
    return cfin(col, 'paint')


# ---------------------------------------------------------------------------------------------------------------------------
# Blocks and faces
# ---------------------------------------------------------------------------------------------------------------------------
def ext(b):
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def upright(b):
    q = b['rot']
    return abs(q[0]) < 1e-5 and abs(q[1]) < 1e-5 and abs(q[2]) < 1e-5


def stem(b):
    return b['id'][6:] if b['id'].startswith('siren-') else None


class Face:
    """A vertical face of the world: axis 0 (the plane x = at, u along z) or 2 (z = at, u along x), its outward sign."""

    def __init__(self, axis, sign, at):
        self.axis, self.sign, self.at = axis, sign, at
        self.n = (sign, 0, 0) if axis == 0 else (0, 0, sign)
        self.U = (0, 0, 1) if axis == 0 else (1, 0, 0)

    def pt(self, u, y, d=0.0):
        return (self.at + self.sign * d, y, u) if self.axis == 0 else (u, y, self.at + self.sign * d)

    def box(self, g, u0, u1, y0, y1, d0, d1, f, T=None, bevel=.01, skip=(), cell=0):
        a, b = self.at + self.sign * d0, self.at + self.sign * d1
        back = ('-x' if self.sign > 0 else '+x') if self.axis == 0 else ('-z' if self.sign > 0 else '+z')
        sk_ = tuple(back if s == 'back' else s for s in skip)
        if self.axis == 0:
            box(g, a, b, y0, y1, u0, u1, f, T, bevel, cell=cell, skip=sk_)
        else:
            box(g, u0, u1, y0, y1, a, b, f, T, bevel, cell=cell, skip=sk_)

    def quad(self, g, u0, u1, y0, y1, d, f):
        g.face((self.pt(u0, y0, d), self.pt(u1, y0, d), self.pt(u1, y1, d), self.pt(u0, y1, d)), f, self.n)


def faces_of(b):
    """The four vertical faces of an upright block: (Face, u0, u1)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    return [(Face(0, -1, x0), z0, z1), (Face(0, 1, x1), z0, z1), (Face(2, -1, z0), x0, x1), (Face(2, 1, z1), x0, x1)]


def plate(g, fc, u0, u1, y0, y1, d, f, T, ch=.004):
    """A cladding plate standing d proud of a face: its front and, on the finer tiers, its chamfered rim."""
    if T.i == 0 and ch > 0 and u1 - u0 > 4 * ch and y1 - y0 > 4 * ch:
        a, b, c, e = u0 + ch, u1 - ch, y0 + ch, y1 - ch
        n = fc.n
        g.face((fc.pt(a, c, d), fc.pt(b, c, d), fc.pt(b, e, d), fc.pt(a, e, d)), f, n)
        Ud = fc.U
        for (p0, p1, q0, q1, side) in (((a, c), (b, c), (u0, y0), (u1, y0), (0, -1, 0)), ((b, e), (a, e), (u1, y1), (u0, y1), (0, 1, 0)),
                                       ((a, e), (a, c), (u0, y1), (u0, y0), (-Ud[0], 0, -Ud[2])), ((b, c), (b, e), (u1, y0), (u1, y1), (Ud[0], 0, Ud[2]))):
            g.face((fc.pt(p0[0], p0[1], d), fc.pt(p1[0], p1[1], d), fc.pt(q1[0], q1[1], d - ch), fc.pt(q0[0], q0[1], d - ch)), f, normals=(n, n, side, side))
    else:
        fc.quad(g, u0, u1, y0, y1, d, f)


def clad(g, fc, u0, u1, y0, y1, T, floor=None, panel=None, gap=.012):
    """Clads a wall face: where it stands on a floor (floor: its height) a dark skirting, a tiled dado to 1.15 m and a steel cap
    rail; above, panels about 1.2 m by 2.4 m with open joints (the dark wall behind shows in them)."""
    panel = F['panel'] if panel is None else panel
    L = u1 - u0
    if L < .05 or y1 - y0 < .05:
        return
    top = y0
    if floor is not None and abs(y0 - floor) < .06:
        h = min(1.15, y1 - y0)
        if T.i < 2:
            fc.box(g, u0, u1, y0, y0 + min(.15, h), 0, .03, F['steeldark'], T, bevel=.012, skip=('back', '-y'))
            plate(g, fc, u0 + .004, u1 - .004, y0 + .15, y0 + h, .007, F['dado'], T)
            if y1 - y0 > 1.4:
                sec = [(-.03, 0), (-.03, .012), (-.02, .026), (0, .032), (.02, .026), (.03, .012), (.03, 0)]
                sweep(g, sec, [fc.pt(u0, y0 + 1.18, 0), fc.pt(u1, y0 + 1.18, 0)], F['steel'], up=fc.n, caps=True)
                top = y0 + 1.21
            else:
                top = y0 + h
        else:
            fc.quad(g, u0, u1, y0, y0 + h, .007, F['dado'])
            top = y0 + h
    if y1 - top < .1:
        return
    if T.i == 2:
        fc.quad(g, u0, u1, top, y1, .005, panel)
        return
    nu = max(1, int(round(L / 1.2)))
    nv = max(1, int(math.ceil((y1 - top) / 2.5)))
    for i in range(nu):
        a, b = u0 + L * i / nu, u0 + L * (i + 1) / nu
        for j in range(nv):
            c, e = top + (y1 - top) * j / nv, top + (y1 - top) * (j + 1) / nv
            plate(g, fc, a + gap / 2, b - gap / 2, c + gap / 2, e - gap / 2, .005, panel, T)


def floor(g, x0, x1, z0, z1, y, f, cell, holes=(), up=True):
    """A flat floor (or, up False, a ceiling) cut into cells, with rectangular openings (x0, x1, z0, z1) left out."""
    xs = set([x0, x1])
    zs = set([z0, z1])
    n = max(1, int(math.ceil((x1 - x0) / cell)))
    for i in range(1, n):
        xs.add(x0 + (x1 - x0) * i / n)
    n = max(1, int(math.ceil((z1 - z0) / cell)))
    for i in range(1, n):
        zs.add(z0 + (z1 - z0) * i / n)
    for h in holes:
        for v in h[:2]:
            if x0 < v < x1:
                xs.add(v)
        for v in h[2:4]:
            if z0 < v < z1:
                zs.add(v)
    xs, zs = sorted(xs), sorted(zs)
    # drop splits a hair apart
    xs = [v for i, v in enumerate(xs) if i == 0 or v - xs[i - 1] > 1e-4]
    zs = [v for i, v in enumerate(zs) if i == 0 or v - zs[i - 1] > 1e-4]
    nrm = (0, 1, 0) if up else (0, -1, 0)
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            if any(h[0] < cx < h[1] and h[2] < cz < h[3] for h in holes):
                continue
            g.face(((xs[i], y, zs[j]), (xs[i + 1], y, zs[j]), (xs[i + 1], y, zs[j + 1]), (xs[i], y, zs[j + 1])), f, nrm)


def rod(g, a, b, r, f, sides, caps=True):
    ck.rod(g, a, b, r, f, sides, caps)


def pipe(g, a, b, r, f, T, stand=None, every=2.4):
    """A round pipe from a to b with a collar at each end and, with `stand` (the unit way to the wall), clamps on stand-offs."""
    rod(g, a, b, r, f, T.small if r < .09 else T.round, caps=True)
    if not T.detail:
        return
    d = sk._sub(b, a)
    L = math.sqrt(sk._dot(d, d))
    w = sk._norm(d)
    for t in (0.0, L - .05):
        rod(g, sk._add(a, w, t), sk._add(a, w, t + .05), r * 1.3, F['steel'], T.small)
    if stand is not None:
        n = max(1, int(L / every))
        for i in range(n):
            c = sk._add(a, w, L * (i + .5) / n)
            rod(g, sk._add(c, w, -.02), sk._add(c, w, .02), r * 1.25, F['steeldark'], T.small)
            rod(g, c, sk._add(c, stand, r + .1), .012, F['steeldark'], 4, caps=False)


def local(g, q, c):
    """A Geo to build in a part's or a block's own frame and its closing call, which adds it turned by q about c."""
    t = Geo()
    return t, (lambda: g.extend(t.transformed(sk.affine(q, c))))


def buried(p, owner):
    """The faces of an upright kit part that lie on or in its upright block ('-x', '+x', ...): never seen, left out."""
    if owner is None or p['q'][3] < .9999 or not upright(owner):
        return ()
    e = ext(owner)
    out = []
    for ax in range(3):
        for s, k in ((-1, '-'), (1, '+')):
            q = list(p['c'])
            q[ax] += s * (p['h'][ax] + .002)
            if e[0] <= q[0] <= e[1] and e[2] <= q[1] <= e[3] and e[4] <= q[2] <= e[5]:
                out.append(k + 'xyz'[ax])
    return tuple(out)


def striped(g, p, T, band=.16, owner=None):
    """A hazard-striped kit part: a dark body with slanted yellow and black stripes on its two largest faces."""
    hx, hy, hz = p['h']
    t, done = local(g, p['q'], p['c'])
    thin = min(range(3), key=lambda i: p['h'][i])
    keys = (('-x', '+x'), ('-y', '+y'), ('-z', '+z'))[thin]
    box(t, -hx, hx, -hy, hy, -hz, hz, F['stripe-black'], None, 0, cell=0, skip=keys)
    gone = buried(p, owner)
    for s in (-1, 1):
        if ('-' if s < 0 else '+') + 'xyz'[thin] in gone:
            continue
        if thin == 0:
            o, U, V, lu, lv, n = (s * hx, -hy, -hz), (0, 0, 1), (0, 1, 0), 2 * hz, 2 * hy, (s, 0, 0)
        elif thin == 1:
            o, U, V, lu, lv, n = (-hx, s * hy, -hz), (1, 0, 0), (0, 0, 1), 2 * hx, 2 * hz, (0, s, 0)
        else:
            o, U, V, lu, lv, n = (-hx, -hy, s * hz), (1, 0, 0), (0, 1, 0), 2 * hx, 2 * hy, (0, 0, s)
        if T.i == 2:
            t.face((o, sk._add(o, U, lu), sk._add(sk._add(o, U, lu), V, lv), sk._add(o, V, lv)), F['stripe-yellow'], n)
        else:
            ek.stripes(t, o, U, V, lu, lv, n, band=band if min(lu, lv) > .1 else band / 2, fa=F['stripe-yellow'], fb=F['stripe-black'])
    done()


def long_axis(p):
    return max(range(3), key=lambda i: p['h'][i])


def kbox(g, p, T, f=None, bevel=.01, owner=None):
    """A kit part as a chamfered piece in its own finish (less the faces that lie on its block)."""
    gone = buried(p, owner)
    if gone:
        c, h = p['c'], p['h']
        box(g, c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2], pfin(p) if f is None else f, T, bevel, cell=0, skip=gone)
    else:
        obox(g, p['c'], p['h'], p['q'], pfin(p) if f is None else f, T, bevel)


def kit_part(g, p, T, owner=None):
    """The fallback: a kit part rebuilt as a finished piece: a striped plate, a round pipe on clamps, a rivet's dome, or a
    chamfered box."""
    h = p['h']
    if p['look'] == 'demolition':
        striped(g, p, T, owner=owner)
        return
    if p['look'] == 'cyber-metal' and p['col'] in PIPES and p['q'][3] > .9999:
        ax = long_axis(p)
        o = [i for i in range(3) if i != ax]
        r = min(h[o[0]], h[o[1]])
        a, b = list(p['c']), list(p['c'])
        a[ax] -= h[ax]
        b[ax] += h[ax]
        pipe(g, tuple(a), tuple(b), r, pfin(p), T)
        return
    if max(h) < .04 and p['look'] == 'cyber-metal':
        if T.tiny:
            ck.dome(g, p['c'], max(h) * 1.1, pfin(p), 6, 2, axis=(0, 1, 0))
        return
    kbox(g, p, T, bevel=min(.012, min(h) * .5), owner=owner)
