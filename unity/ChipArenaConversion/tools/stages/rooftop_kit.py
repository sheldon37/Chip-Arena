"""The menu rooftop's art: what its scripts share (rooftop_art.py runs them; docs/MENU-ROOFTOP-ART.md): the layout turned into
Unity's frame, the finishes, the towers' facade faces (drawn by the menu's own facade shader, so a modelled tower keeps its
procedural bays and rooms) and small modelling helpers on stagekit and cybercity_kit.

The layout (Tests/RooftopLayout.cs, `--rooftop-layout`) is in Core's SOURCE frame; everything here is in the game's (Unity:
metres, +y up, x = -source x). In Unity's frame the camera floats at about (-7.1, 4.45, 4.5) looking toward +x and -z; our roof
spans x -1.7 .. 26, z -1.8 .. 16; the pilot sits on the front lip at (0, .32, -1.6) with its legs over the z = -1.8 edge."""
import json
import math

import numpy as np

import stagekit as sk
import cybercity_kit as ck
from cybercity_kit import F, glow, h01, pick, Fr, rod  # noqa: F401
from stagekit import Geo, box, sweep, lathe, cylinder  # noqa: F401

Y = (0.0, 1.0, 0.0)
CYAN, MAGENTA, VIOLET, ICE, WARM, AMBER, RED, LIME = 0x22E6FF, 0xFF3DCF, 0x8B5CFF, 0xC8F4FF, 0xFF8A4D, 0xFFB000, 0xFF2A3D, 0x7DFF8A
GLAZED, RESIDENTIAL, CORPORATE, MASONRY = 0, 1, 2, 3


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes (cybercity_kit's, and the rooftop's own)
# ---------------------------------------------------------------------------------------------------------------------------
def finishes():
    ck.finishes()
    A = sk

    def add(name, hexcol, detail=A.DETAIL_NONE, **kw):
        F[name] = A.fin(name, hexcol, detail, **kw)
    add('membrane', 0x34363F, A.DETAIL_PLASTER, smooth=.42, dirt=.9)
    add('membranelap', 0x2A2C34, A.DETAIL_PLASTER, smooth=.5, dirt=.8)
    add('gravel', 0x4A4B52, A.DETAIL_PLASTER, smooth=.12, dirt=1.0)
    add('coping', 0x6A6E7C, A.DETAIL_PLASTER, smooth=.38, dirt=.8, wear=.3)
    add('parapet', 0x454856, A.DETAIL_PLASTER, smooth=.3, dirt=.9, wear=.2)
    add('flashing', 0x7C8496, A.DETAIL_NONE, smooth=.7, metal=.5, dirt=.5, wear=.3)
    add('galv', 0x767E8E, A.DETAIL_NONE, smooth=.6, metal=.5, dirt=.6, wear=.3)
    add('galvdark', 0x4A5060, A.DETAIL_NONE, smooth=.55, metal=.5, dirt=.6, wear=.3)
    add('rust', 0x6E4632, A.DETAIL_NONE, smooth=.3, metal=.3, dirt=.9, wear=.2)
    add('stave', 0x6B4A36, A.DETAIL_WOOD, smooth=.3, param=1, dirt=.9, wear=.2)
    add('stavedark', 0x4A3428, A.DETAIL_WOOD, smooth=.3, param=1, dirt=.9, wear=.2)
    add('hazard', 0xE0A020, A.DETAIL_NONE, smooth=.4, dirt=.6)
    add('redpaint', 0x8E2630, A.DETAIL_NONE, smooth=.5, metal=.2, dirt=.6, wear=.4)
    add('bluepaint', 0x27405E, A.DETAIL_NONE, smooth=.5, metal=.2, dirt=.6, wear=.4)
    add('creme', 0xB8B2A4, A.DETAIL_NONE, smooth=.45, dirt=.8, wear=.3)
    add('crtshell', 0x8C8676, A.DETAIL_NONE, smooth=.4, dirt=.9, wear=.3)
    add('crtdark', 0x2A2926, A.DETAIL_NONE, smooth=.45, dirt=.5)
    add('pad', 0x5A5E6A, A.DETAIL_PLASTER, smooth=.35, dirt=.8)
    add('brick', 0x5A3C36, A.DETAIL_PLASTER, smooth=.2, dirt=.9, wear=.2)
    add('stone', 0x6C6A70, A.DETAIL_PLASTER, smooth=.25, dirt=.9, wear=.25)
    add('fabric', 0x3A4660, A.DETAIL_CLOTH, smooth=.2, dirt=.6)
    add('can', 0x2FA7C8, A.DETAIL_NONE, smooth=.8, metal=.5, dirt=.2)
    # towers' solid trim by style
    add('mullion', 0x2C3348, A.DETAIL_NONE, smooth=.62, metal=.5, dirt=.3)
    add('spandrel', 0x1E2434, A.DETAIL_NONE, smooth=.7, metal=.45, dirt=.3)
    add('ledge', 0x5A5E6E, A.DETAIL_PLASTER, smooth=.3, dirt=.7)
    add('ledgedark', 0x343745, A.DETAIL_PLASTER, smooth=.3, dirt=.7)
    add('resi', 0x4E4C5A, A.DETAIL_PLASTER, smooth=.25, dirt=.8)
    add('masonry', 0x5C3E3E, A.DETAIL_PLASTER, smooth=.2, dirt=.9)
    add('masonrypale', 0x7A6A62, A.DETAIL_PLASTER, smooth=.22, dirt=.9)
    add('roofglass', 0x1A2740, A.DETAIL_NONE, smooth=.92, metal=.4, dirt=.1)
    add('helideck', 0x5E6270, A.DETAIL_PLASTER, smooth=.35, dirt=.6)
    add('billback', 0x1A1C24, A.DETAIL_NONE, smooth=.4, metal=.3, dirt=.4)
    # the flying cars
    add('carwhite', 0xC4C9D4, A.DETAIL_NONE, smooth=.85, metal=.3, dirt=.2)
    add('carblack', 0x15161C, A.DETAIL_NONE, smooth=.85, metal=.4, dirt=.2)
    add('caryellow', 0xE2B32C, A.DETAIL_NONE, smooth=.85, metal=.3, dirt=.2)
    add('carred', 0xB32A40, A.DETAIL_NONE, smooth=.85, metal=.35, dirt=.2)
    add('carteal', 0x1E6E78, A.DETAIL_NONE, smooth=.85, metal=.35, dirt=.2)
    add('carglass', 0x0C1220, A.DETAIL_NONE, smooth=.95, metal=.45, dirt=.05)
    add('carunder', 0x0E0F14, A.DETAIL_NONE, smooth=.5, metal=.4, dirt=.2)


def hexlin(r, g, b):
    """A Core HDR colour (linear, any gain) as (sRGB hex of its hue at full value, gain)."""
    m = max(r, g, b, 1e-6)
    def enc(c):
        c = max(0.0, min(1.0, c / m))
        s = 12.92 * c if c <= .0031308 else 1.055 * c ** (1 / 2.4) - .055
        return int(round(s * 255))
    return (enc(r) << 16) | (enc(g) << 8) | enc(b), m


# ---------------------------------------------------------------------------------------------------------------------------
# The layout in Unity's frame
# ---------------------------------------------------------------------------------------------------------------------------
class Lot:
    def __init__(self, i, v):
        self.i = i
        self.x0, self.x1, self.z0, self.z1 = -v[1], -v[0], v[2], v[3]
        self.top, self.style, self.seed, self.hue, self.share = v[4], int(v[5]), v[6], v[7], v[8]
        self.detail, self.bare, self.chamfer, self.ntiers, self.tierv, self.city = int(v[9]), v[10] > 0, v[11], int(v[12]), v[13], v[14] > 0
        self.tiers = []      # (x0, x1, z0, z1, chamfer, y0, y1)
        self.parts = {}      # kind -> [numbers in Unity's frame]
        self.c = ((self.x0 + self.x1) / 2, (self.z0 + self.z1) / 2)

    def add(self, kind, v):
        self.parts.setdefault(kind, []).append(v)

    def all(self, kind):
        return self.parts.get(kind, [])


def _mx(v, idx):
    v = list(v)
    for i in idx:
        v[i] = -v[i]
    return v


class Layout:
    """The layout's lots (the modelled ones: `city`), the loose pieces (frames, bridges, huts), the camera, the targets."""

    def __init__(self, path):
        with open(path, encoding='utf-8') as fh:
            L = json.load(fh)
        assert L['frame'] == 'source'
        self.raw = L
        self.street = L['street']
        self.fov = L['fov']
        U = lambda p: (-p[0], p[1], p[2])  # noqa: E731
        self.camera = [(c['t'], U(c['eye']), U(c['fwd'])) for c in L['camera']]
        self.eye = self.camera[0][1]
        self.fwd = self.camera[0][2]
        p = L['perch']
        self.perch = dict(x0=-p['x1'], x1=-p['x0'], z0=p['z0'], z1=p['z1'], lip=p['lipTop'], lipd=p['lipDepth'], seat=U(p['seat']),
                          beacon=U(p['beacon']), sign=U(p['signLight']), cyan=U(p['cyanLight']), spark=U(p['spark']))
        self.screens = [dict(kind=s['kind'], c=U(s['center']), w=s['width'], h=s['height'], n=(-math.sin(s['yaw']), 0.0, math.cos(s['yaw']))) for s in L['screens']]
        self.sites = {k: U(v['at']) for k, v in L['sites'].items()}
        self.targets = [(t['what'], U(t['at'])) for t in L['targets'] if t.get('clear', True)]
        self.hidden_targets = sum(1 for t in L['targets'] if not t.get('clear', True))
        self.glows = [(U(g['at']), g['size'], g['kind']) for g in L['glows']]
        self.steam = [U(p) for p in L['steam']]
        self.lanes = [dict(count=l['count'], near=l['near'], pts=[U(p) for p in l['points']]) for l in L['lanes']]
        self.lots, self.frames, self.bridges, self.runhuts = {}, [], [], []
        self.loose = {}     # pieces of no lot (our roof's tank), numbers in Unity's frame
        for n in L['notes']:
            k, i, v = n['k'], n['lot'], n['v']
            if k == 'lot':
                self.lots[i] = Lot(i, v)
                continue
            if k == 'frame':
                self.frames.append(dict(c=(-v[0], v[1], v[2]), n=(-math.sin(v[3]), 0.0, math.cos(v[3])), w=v[4], h=v[5], back=v[6]))
                continue
            if k == 'bridge':
                self.bridges.append((-v[1], -v[0], v[2], v[3], v[4]))
                continue
            if k == 'runhut':
                self.runhuts.append((-v[1], -v[0], v[2], v[3], v[4], v[5]))
                continue
            if i < 0:
                self.loose.setdefault(k, []).append(_mx(v, (0,)))
                continue
            lot = self.lots[i]
            if k == 'tier':
                lot.tiers.append((-v[2], -v[0], v[1], v[3], v[4], v[5], v[6]))
            elif k == 'lantern':
                lot.add(k, [-v[2], -v[0], v[1], v[3]] + list(v[4:]))
            elif k in ('mast', 'tank'):
                lot.add(k, _mx(v, (0,)))
            elif k == 'plant':
                lot.add(k, _mx(v, (0,)))
            elif k == 'stairhut':
                lot.add(k, [-(v[0] + 3.6), v[1], v[2]])
            elif k == 'helipad':
                lot.add(k, _mx(v, (0,)))
            elif k == 'billboard':
                lot.add(k, _mx(v, (0, 2, 4)))
            elif k in ('balcony', 'fireescape'):
                lot.add(k, _mx(v, (0, 2, 4)))
            elif k in ('aircon', 'hangsign'):
                lot.add(k, _mx(v, (0, 2, 4)))
            elif k == 'trim':
                lot.add(k, list(v[:4]) + [(-v[j], v[j + 1]) for j in range(4, len(v), 2)])
            else:   # fins, cornerneon, cornice, crown: no x in them
                lot.add(k, list(v))
        self.city = [l for l in self.lots.values() if l.city]

    def dist(self, x, z):
        return math.hypot(x - self.eye[0], z - self.eye[2])


# ---------------------------------------------------------------------------------------------------------------------------
# Facade faces: the towers' walls, drawn by the menu's facade shader (Resources/NativeRooftopFacade.shader)
# ---------------------------------------------------------------------------------------------------------------------------
class Facade:
    """Vertical wall faces with the facade shader's attributes: per face its tower (seed, style, hue, lit share, top) and the
    metric coordinate along the wall (u; the height is the world's). The bank's `facade` prop carries them as
      uv0 (u, top)   uv1 (seed, style + .9 share)   colour.r hue
    which Runtime NativeRooftopModels turns back into the shader's own layout."""

    def __init__(self):
        self.g = Geo()
        self.rows = []      # per corner: (u, top, seed, style, share, hue)
        self.f = sk.fin('facade', 0x1A2030, sk.DETAIL_NONE, smooth=.8, metal=.3, dirt=0)

    def wall(self, a, b, y0, y1, tw, u0, y1b=None):
        """A wall from a to b (x, z), its outside on the right going a -> b round a counter-clockwise footprint (x right, z
        up): bottom y0, top y1 at a and y1b at b (a raked top), the wall's u starting at u0. Returns u at b."""
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz)
        if L < 1e-4:
            return u0
        n = (dz / L, 0.0, -dx / L)
        yb = y1 if y1b is None else y1b
        pts = ((a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], yb, b[1]), (a[0], y1, a[1]))
        before = self.g.faces
        self.g.face(pts, self.f, n)
        if self.g.faces == before:
            return u0 + L
        # Geo.face keeps the order unless it had to turn the face: read the corners back to place u
        P = np.frombuffer(self.g.p, dtype=np.float32).reshape(-1, 3)[-4:]
        for p in P:
            u = u0 + ((p[0] - a[0]) * dx + (p[2] - a[1]) * dz) / L
            self.rows.append((u, tw['top'], tw['seed'], tw['style'], tw['share'], tw['hue']))
        return u0 + L

    def ring(self, fp, y0, y1, tw, tops=None):
        """The walls round a counter-clockwise footprint [(x, z)]; tops: a top height per corner (a raked crown)."""
        u = 0.0
        n = len(fp)
        for i in range(n):
            j = (i + 1) % n
            ya = y1 if tops is None else tops[i]
            yb = y1 if tops is None else tops[j]
            u = self.wall(fp[i], fp[j], y0, ya, tw, u, yb)

    def mesh(self, name):
        P, N, S, Fi = self.g.arrays()
        if len(S) == 0:
            return None
        om = sk.OutMesh(name)
        R = np.array(self.rows, dtype=np.float64)
        om.positions = P.astype(np.float64).tolist()
        om.normals = N.astype(np.float64).tolist()
        om.uv0 = np.stack([R[:, 0], R[:, 1]], axis=1).tolist()
        om.uv1 = np.stack([R[:, 2], R[:, 3] + .9 * np.clip(R[:, 4], 0, 1)], axis=1).tolist()
        om.colors = np.stack([np.clip(R[:, 5], 0, 1), np.zeros(len(R)), np.zeros(len(R)), np.ones(len(R))], axis=1).tolist()
        tris = []
        for q in range(len(S)):
            b = q * 4
            tris.extend((b, b + 1, b + 2, b, b + 2, b + 3))
        om.submeshes = [tris]
        om._lo = tuple(float(x) for x in P.min(axis=0))
        om._hi = tuple(float(x) for x in P.max(axis=0))
        return om


def tower(lot, top=None, seed=None, share=None, style=None):
    return dict(seed=float(lot.seed if seed is None else seed), style=float(lot.style if style is None else style), hue=float(lot.hue),
                share=float(lot.share if share is None else share), top=float(lot.top if top is None else top))


# ---------------------------------------------------------------------------------------------------------------------------
# Footprints (counter-clockwise, x right, z up)
# ---------------------------------------------------------------------------------------------------------------------------
def rect_fp(x0, x1, z0, z1, chamfer=0.0, round_n=0):
    """A rectangle, its corners cut by `chamfer` (round_n > 0: rounded in that many facets)."""
    if chamfer <= .01:
        return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    c = min(chamfer, min(x1 - x0, z1 - z0) * .45)
    if round_n <= 0:
        return [(x0 + c, z0), (x1 - c, z0), (x1, z0 + c), (x1, z1 - c), (x1 - c, z1), (x0 + c, z1), (x0, z1 - c), (x0, z0 + c)]
    out = []
    for (cx, cz, a0) in ((x1 - c, z0 + c, -math.pi / 2), (x1 - c, z1 - c, 0.0), (x0 + c, z1 - c, math.pi / 2), (x0 + c, z0 + c, math.pi)):
        for k in range(round_n + 1):
            a = a0 + math.pi / 2 * k / round_n
            out.append((cx + c * math.cos(a), cz + c * math.sin(a)))
    return out


def round_fp(cx, cz, rx, rz, n, phase=0.0):
    return [(cx + rx * math.cos(2 * math.pi * k / n + phase), cz + rz * math.sin(2 * math.pi * k / n + phase)) for k in range(n)]


def grow(fp, d):
    """A convex counter-clockwise footprint grown (shrunk, negative) by d along its edges' normals."""
    n = len(fp)
    out = []
    for k in range(n):
        a, b, c = fp[(k - 1) % n], fp[k], fp[(k + 1) % n]
        n0 = _unit((b[1] - a[1], -(b[0] - a[0])))
        n1 = _unit((c[1] - b[1], -(c[0] - b[0])))
        m = _unit((n0[0] + n1[0], n0[1] + n1[1]))
        cos = max(.3, m[0] * n0[0] + m[1] * n0[1])
        out.append((b[0] + m[0] * d / cos, b[1] + m[1] * d / cos))
    return out


def _unit(v):
    l = math.hypot(v[0], v[1])
    return (v[0] / l, v[1] / l) if l > 1e-9 else (1.0, 0.0)


def bounds(fp):
    xs, zs = [p[0] for p in fp], [p[1] for p in fp]
    return min(xs), max(xs), min(zs), max(zs)


def cap(g, fp, y, f, up=True):
    """A flat cap over a convex footprint."""
    pts = [(p[0], y, p[1]) for p in fp]
    g.face(pts, f, (0, 1, 0) if up else (0, -1, 0))


def band(g, fp, y0, y1, f, top=None, bottom=None, d0=0.0, d1=None):
    """A band round a footprint: its outer face on the footprint grown by d0 at the bottom (and d1 at the top: a splayed
    band), with a top and a bottom ring back to the footprint when asked (finishes)."""
    lo = grow(fp, d0) if d0 else fp
    hi = lo if d1 is None else (grow(fp, d1) if d1 else fp)
    n = len(fp)
    for i in range(n):
        j = (i + 1) % n
        nx, nz = _unit((lo[j][1] - lo[i][1], -(lo[j][0] - lo[i][0])))
        g.face(((lo[i][0], y0, lo[i][1]), (lo[j][0], y0, lo[j][1]), (hi[j][0], y1, hi[j][1]), (hi[i][0], y1, hi[i][1])), f,
               (nx * (y1 - y0), d0 - (d1 if d1 is not None else d0), nz * (y1 - y0)))
        if top is not None:
            g.face(((hi[i][0], y1, hi[i][1]), (hi[j][0], y1, hi[j][1]), (fp[j][0], y1, fp[j][1]), (fp[i][0], y1, fp[i][1])), top, (0, 1, 0))
        if bottom is not None:
            g.face(((lo[i][0], y0, lo[i][1]), (lo[j][0], y0, lo[j][1]), (fp[j][0], y0, fp[j][1]), (fp[i][0], y0, fp[i][1])), bottom, (0, -1, 0))


def wall_ring(g, fp, y0, y1, f, inward=False):
    """Plain solid walls round a footprint (inward: facing in, a parapet's inner face)."""
    n = len(fp)
    for i in range(n):
        j = (i + 1) % n
        nx, nz = fp[j][1] - fp[i][1], -(fp[j][0] - fp[i][0])
        if inward:
            nx, nz = -nx, -nz
        g.face(((fp[i][0], y0, fp[i][1]), (fp[j][0], y0, fp[j][1]), (fp[j][0], y1, fp[j][1]), (fp[i][0], y1, fp[i][1])), f, (nx, 0, nz))


def edge_frame(a, b, y=0.0):
    """The face frame of a footprint edge a -> b (counter-clockwise footprint): origin at its middle, the normal outward."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dz)
    return Fr(((a[0] + b[0]) / 2, y, (a[1] + b[1]) / 2), (dz / L, 0.0, -dx / L), L / 2, 0.0)


def frame_n(o, n, half=0.0):
    l = math.hypot(n[0], n[2])
    return Fr(o, (n[0] / l, 0.0, n[2] / l), half, 0.0)


def strut(g, a, b, r, f, sides=4):
    rod(g, a, b, r, f, sides, caps=False)


def mirror_note():
    return 'Unity frame'
