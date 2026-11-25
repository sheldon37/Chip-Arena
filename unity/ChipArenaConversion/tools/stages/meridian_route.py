"""Meridian's described route: the bank ChipArenaCityRoute (Core NativeMeridianRoute, Runtime NativeMeridianRouteDraw;
docs/expedition/MERIDIAN-ROUTE.md).

Run inside Blender 5.x (bpy, numpy), headless:

  blender --background --factory-startup --python tools/stages/meridian_route.py -- \
      --route ROUTE.json --out OUT_DIR [--packs DIR] [--no-ao] [--preview DIR] [--only PREFIX,...]

ROUTE.json is what the CoreTests flag `--meridian-route-dump ROUTE.json` writes: Crown Hill's blocks (the stair guards with their
turns), ramps and placement, and the Ironworks' loading frame. The bank goes to OUT_DIR/ChipArenaLocal/Resources/
ChipArenaCityRoute.bytes (keep it out of the repository; the integrator carries it in the Drive zip). `--packs` names the folder
holding the owner's extracted Asset Store packs, one folder per pack as tools/pack_bank reads them (Modern Supermarket, Old
Industry): their meshes are read as data and their albedo textures baked into the vertex colours (nothing of a pack is written but
those baked meshes, inside the bank). Without it the shop's produce and the loading bay's Old Industry parts are left out.

Bank names (the runtime reads them): <key>/0 the Fine tier, <key>/2 the Low tier (only where the Fine mesh is big), group "route".
Keys are Core's: route/647/{rails,crossing,footway,corner,sign,roofs} (in the world's frame), route/front/ironworks/loading (in a
hall's door front frame, two 2.5 m bays wide, its face at z 0 facing -z), route/smoke/puff (a unit puff the runtime scales).

The art rules (the common brief), checked here on the built meshes: art standing below 3.1 m over the floor beneath it lies within
0.25 m of a collider, flat paint of 6 cm or less excepted. The balustrades' bars are the one known exception: they stand under the
guards' grips, which already stop a pilot; the report counts them. Presentation only: no collider, no rule, no network change.
"""
import json
import math
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
BANK = 'ChipArenaCityRoute'
GROUP = 'route'


def _args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    a = {'route': None, 'out': None, 'packs': None, 'ao': True, 'preview': None, 'only': None, 'report': None}
    i = 0
    while i < len(argv):
        k = argv[i]
        if k == '--no-ao':
            a['ao'] = False
        else:
            a[k.lstrip('-')] = argv[i + 1]
            i += 1
        i += 1
    return a


ARGS = _args()
os.environ.setdefault('CHIP_STAGES', HERE)
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
import stagekit as sk  # noqa: E402
import meridian_gates_kit as mk  # noqa: E402
import meridian_lm_kit as lk  # noqa: E402
from meridian_gates_kit import F, Geo, glow, bx  # noqa: E402

PI = math.pi
WARM, FURNACE = 0xFFD9A0, 0xFF6418
TIER_NAME = {0: '0', 2: '2'}
LOW_FROM = 2500       # Fine triangles from which a piece also banks a Low tier
CAPS = {'route/647/rails': (24000, 5000), 'route/647/crossing': (4000, 1500), 'route/647/footway': (6000, 2000),
        'route/647/corner': (26000, 6000), 'route/647/sign': (6000, 1500), 'route/647/roofs': (9000, 3000),
        'route/front/ironworks/loading': (24000, 5000), 'route/smoke/puff': (160, 0)}   # Tests/MeridianRouteChecks.Cap


# ---------------------------------------------------------------------------------------------------------------------------
# The route's own finishes (on top of the Meridian table)
# ---------------------------------------------------------------------------------------------------------------------------
X = {}


def finishes():
    if X:
        return X
    mk.finishes()
    lk.finishes()
    a = mk._add_fin
    D = sk
    X['rail'] = a('route-rail', 0x1E2422, D.DETAIL_IRON, .42, .35, .7, 0, .30)
    X['tactile'] = a('route-tactile', 0xB8925A, D.DETAIL_TILE, .30, 0, .8, .05, .15, .08)
    X['york'] = a('route-york', 0x8A8376, D.DETAIL_FLAG, .30, 0, .9, .10, .15, .6)
    X['shop'] = a('route-shop-green', 0x1F4A3A, D.DETAIL_WOOD, .50, 0, .7, .05, .30)
    X['shop-cream'] = a('route-shop-cream', 0xE6DCC0, D.DETAIL_WOOD, .45, 0, .7, .05, .30)
    X['canvas-red'] = a('route-canvas-red', 0xA8282E, D.DETAIL_CLOTH, .10, 0, .6, 0, 0)
    X['canvas-cream'] = a('route-canvas-cream', 0xE8DEC4, D.DETAIL_CLOTH, .10, 0, .6, 0, 0)
    X['pot'] = a('route-chimney-pot', 0xA0583A, D.DETAIL_NONE, .25, 0, 1.2, 0, .1)
    X['hazard'] = a('route-hazard-yellow', 0xD8B02A, D.DETAIL_NONE, .35, 0, .9, 0, .4)
    X['shutter'] = a('route-shutter', 0x6A6E70, D.DETAIL_IRON, .40, .6, 1.3, 0, .2)
    X['pack'] = a('route-pack', 0xFFFFFF, D.DETAIL_NONE, .30, 0, .5, 0, 0)          # pack meshes: the albedo comes from their textures
    X['pack-metal'] = a('route-pack-metal', 0xFFFFFF, D.DETAIL_NONE, .45, .5, .5, 0, 0)
    X['puff'] = a('route-puff', 0xC8C8CC, D.DETAIL_NONE, .05, 0, 0, 0, 0)
    return X


# ---------------------------------------------------------------------------------------------------------------------------
# The route's records (ROUTE.json)
# ---------------------------------------------------------------------------------------------------------------------------
class Route:
    def __init__(self, path):
        self.d = json.load(open(path))
        self.blocks = {}
        self.treads = []
        for b in self.d['blocks']:
            self.blocks[b[0]] = b
            if b[7]:
                self.treads.append(b)
        self.ramps = {r['id']: r for r in self.d['ramps']}

    def box(self, bid):
        b = self.blocks[bid]
        return b[1], b[2], b[3], b[4], b[5], b[6]

    def tread_top(self, x, z, below):
        """The top of the tread (or floor) under (x, z), at most `below`."""
        best = None
        for b in self.d['blocks']:
            if b[1] - 1e-3 <= x <= b[2] + 1e-3 and b[5] - 1e-3 <= z <= b[6] + 1e-3 and b[4] <= below + .02:
                if not b[0].startswith('stair-guard-') and (best is None or b[4] > best):
                    best = b[4]
        return best


# ---------------------------------------------------------------------------------------------------------------------------
# Pack meshes, read as data (tools/pack_bank), their albedo baked into the corners' colours
# ---------------------------------------------------------------------------------------------------------------------------
class Tints:
    """Corner colours (sRGB 0..1) the pack meshes set over the bake's, by the Geo's corner index."""

    def __init__(self):
        self.ranges = []        # (first corner, colours (n, 3))

    def apply(self, bk):
        for c0, col in self.ranges:
            n = len(col)
            ao = bk.colour[c0:c0 + n, 3:4].astype(np.float64) / 255.0
            shade = 1 - (1 - ao) * .3
            bk.colour[c0:c0 + n, :3] = np.clip(np.round(col * shade * 255), 0, 255).astype(np.uint8)


_IMG = {}
_PACKS = {}


def _pack_reader():
    sys.path.insert(0, os.path.join(HERE, '..', 'pack_bank'))
    import unity_assets as ua
    import fbx
    return ua, fbx


def _image(path):
    """The texture's pixels (h, w, 4), the stored values (sRGB for an albedo)."""
    if path in _IMG:
        return _IMG[path]
    import bpy
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = 'Non-Color'
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    arr = px.reshape(h, w, 4)
    _IMG[path] = arr
    return arr


def _sample(arr, uv):
    h, w = arr.shape[:2]
    u = np.mod(uv[:, 0], 1.0) * (w - 1)
    v = np.mod(uv[:, 1], 1.0) * (h - 1)
    return arr[np.clip(np.round(v).astype(int), 0, h - 1), np.clip(np.round(u).astype(int), 0, w - 1), :3]


def _cluster(P, I, cell):
    """Vertex clustering (a quick decimation for small detailed props): corners snapped to a `cell` grid share one place (their
    mean); triangles left with fewer than three distinct places are dropped. Returns (P2, kept triangle mask)."""
    if not cell:
        return P, np.ones(len(I), dtype=bool)
    key = np.round(P / cell).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.ravel()
    sums = np.zeros((inv.max() + 1, 3))
    np.add.at(sums, inv, P)
    cnt = np.bincount(inv)
    P2 = (sums / cnt[:, None])[inv]
    c = inv[I]
    keep = (c[:, 0] != c[:, 1]) & (c[:, 1] != c[:, 2]) & (c[:, 0] != c[:, 2])
    return P2, keep


def pack_part(g, tints, pack, prefab, m, fin=None, lod_keep=0, keep=None, cell=0.0):
    """Adds a pack prefab's visible meshes to g through the 3x4 affine m (Unity space), each corner coloured from its material's
    albedo (sampled at its corner and toward the triangle's middle) times the material's colour. Returns the triangles added (0
    without --packs). keep(material name) may drop submeshes (e.g. glass); cell > 0 clusters the corners to that grid (m) first,
    each face then flat."""
    root = ARGS['packs']
    if not root:
        return 0
    ua, fbx = _pack_reader()
    key = (pack,)
    if key not in _PACKS:
        _PACKS[key] = ua.Folder(os.path.join(root, pack))
    folder = _PACKS[key]
    full = os.path.join(root, pack, prefab)
    parts = ua.flatten(folder, full) or _flatten_by_name(ua, fbx, folder, full)
    A = np.array(m, dtype=np.float64).reshape(3, 4)
    R, t = A[:, :3], A[:, 3]
    Ni = np.linalg.inv(R).T
    fin = finishes()['pack'] if fin is None else fin
    tris = 0
    for part in parts:
        mesh = ua.mesh_for(folder, part)
        if mesh is None or not mesh.positions:
            continue
        M = np.array(part.matrix, dtype=np.float64).reshape(4, 4)
        P = np.array(mesh.positions, dtype=np.float64)
        P = P @ M[:3, :3].T + M[:3, 3]
        N = np.array(mesh.normals if mesh.normals else [(0, 1, 0)] * len(P), dtype=np.float64)
        N = N @ np.linalg.inv(M[:3, :3])
        P = P @ R.T + t
        N = N @ Ni.T
        N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-9)
        UV = np.array(mesh.uv0 if mesh.uv0 else [(0, 0)] * len(P), dtype=np.float64)
        for s, idx in enumerate(mesh.submeshes):
            mat = part.materials[s] if s < len(part.materials) else (part.materials[-1] if part.materials else None)
            md = None
            if mat:
                g_ = next((gg for gg, pp in folder.by_guid.items() if os.path.normcase(pp) == os.path.normcase(mat)), None) if os.path.sep in mat or '/' in mat else mat
                md = ua.material(folder, g_) if g_ else None
            if keep is not None and not keep(md['name'] if md else ''):
                continue
            col = np.array(md['color'][:3], dtype=np.float64) if md else np.array([.6, .6, .6])
            tex = _image(md['albedo']) if md and md.get('albedo') and os.path.exists(md['albedo']) else None
            I = np.array(idx, dtype=np.int64).reshape(-1, 3)
            Pc, kept = _cluster(P, I, cell)
            for ti, tri in enumerate(I):
                if not kept[ti]:
                    continue
                pts = [tuple(Pc[j]) for j in tri]
                nrm = [tuple(N[j]) for j in tri]
                c0 = len(g.p) // 3
                if cell:
                    fn = np.mean(N[tri], axis=0)
                    g.face(pts, fin, normal=tuple(fn))
                else:
                    g.face(pts, fin, normals=nrm)
                if len(g.p) // 3 == c0:
                    continue
                if tex is not None:
                    # each corner its vertex's texel: a smooth vertex keeps one colour, so its corners merge in the bank
                    cs = _sample(tex, UV[tri])
                else:
                    cs = np.ones((3, 3))
                cs = cs * col
                # the face may have been flipped to its normal: keep the colours with their corners
                Pn = np.array(g.p[c0 * 3:c0 * 3 + 9], dtype=np.float64).reshape(3, 3)
                order = [int(np.argmin(np.linalg.norm(Pc[tri] - Pn[k], axis=1))) for k in range(3)]
                tints.ranges.append((c0, cs[order]))
                tris += 1
    return tris


def _flatten_by_name(ua, fbx, folder, prefab_path):
    """A prefab whose meshes the committed reader cannot name (fileIdsGeneration 2 with an empty internalIDToNameTable, Old
    Industry): each active MeshFilter's mesh found in its model by its GameObject's name (LOD1 and further left out)."""
    import re
    with open(prefab_path, 'r', encoding='utf-8', errors='replace') as h:
        docs = ua.documents(h.read())
    objs = {fid: (cls, stripped, body) for cls, fid, stripped, body in docs}
    transforms, by_go = {}, {}
    for fid, (cls, stripped, body) in objs.items():
        if cls in (4, 224) and not stripped:
            transforms[fid] = body
            by_go[ua.ref(body, 'm_GameObject')[0]] = fid

    def world(fid):
        m, seen, chain = fbx.ident(), set(), []
        while fid in transforms and fid not in seen:
            seen.add(fid)
            chain.append(fid)
            fid = ua.ref(transforms[fid], 'm_Father')[0]
        for t in reversed(chain):
            b = transforms[t]
            m = fbx.mul(m, ua.trs(ua.vec(b, 'm_LocalPosition', (0, 0, 0)), ua.vec(b, 'm_LocalRotation', (0, 0, 0, 1)), ua.vec(b, 'm_LocalScale', (1, 1, 1))))
        return m
    parts = []
    for fid, (cls, stripped, body) in objs.items():
        if cls != 33 or stripped:
            continue
        go = ua.ref(body, 'm_GameObject')[0]
        gob = objs.get(go, (0, False, ''))[2]
        if ua.field(gob, 'm_IsActive') == '0':
            continue
        name = ua.field(gob, 'm_Name') or ''
        lod = re.search(r'_LOD(\d+)$', name)
        if lod and lod.group(1) != '0':
            continue
        mesh_ref = ua.ref(body, 'm_Mesh')
        path = folder.path(mesh_ref[1]) if mesh_ref[1] else None
        if not path or not path.lower().endswith('.fbx'):
            continue
        cand = [p for p in ua.model_parts(folder, path) if p.name == name]
        if not cand:
            continue
        renderer = next((b2 for f2, (c2, s2, b2) in objs.items() if c2 == 23 and not s2 and ua.ref(b2, 'm_GameObject')[0] == go), None)
        if renderer is None:
            continue
        mats = [folder.path(gg) if gg else None for _, gg in ua.ref_list(renderer, 'm_Materials')]
        tf = by_go.get(go)
        parts.append(ua.Part(cand[0].source, world(tf) if tf is not None else fbx.ident(), mats, name))
    return parts


_BOUNDS = {}


def bounds_of(pack, prefab):
    """A pack prefab's bounds (lo, hi) in its own space, or None without --packs."""
    if (pack, prefab) not in _BOUNDS:
        g, t = Geo(), Tints()
        if not pack_part(g, t, pack, prefab, sk.affine((0, 0, 0, 1))):
            _BOUNDS[(pack, prefab)] = None
        else:
            P = np.array(g.p, dtype=np.float64).reshape(-1, 3)
            _BOUNDS[(pack, prefab)] = (P.min(axis=0), P.max(axis=0))
    return _BOUNDS[(pack, prefab)]


def fit(pack, prefab, lo, hi, yaw=0.0, keep_aspect=True, anchor=(.5, 0, .5)):
    """The affine that puts a pack prefab into the box lo..hi (Unity space) turned by yaw degrees about +y: scaled uniformly to
    fit (or to fill when keep_aspect is False), its anchor (fractions of its own bounds) on the box's."""
    b = bounds_of(pack, prefab)
    if b is None:
        return None
    s0, s1 = b
    q = mk.qy(yaw)
    Rm = np.array(sk.affine(q), dtype=np.float64).reshape(3, 4)[:, :3]
    corners = np.array([[x, y, z] for x in (s0[0], s1[0]) for y in (s0[1], s1[1]) for z in (s0[2], s1[2])]) @ Rm.T
    r0, r1 = corners.min(axis=0), corners.max(axis=0)
    size, want = r1 - r0, np.array(hi, dtype=np.float64) - np.array(lo, dtype=np.float64)
    k = want / np.maximum(size, 1e-6)
    if keep_aspect:
        k = np.full(3, k[size > 1e-3].min() if (size > 1e-3).any() else 1.0)
    an = np.array(anchor, dtype=np.float64)
    # p' = k (R p) + t, with the turned bounds' anchor on the box's anchor
    M = np.diag(k) @ Rm
    t = (np.array(lo) + want * an) - k * (r0 + (r1 - r0) * an)
    return [M[0, 0], M[0, 1], M[0, 2], t[0], M[1, 0], M[1, 1], M[1, 2], t[1], M[2, 0], M[2, 1], M[2, 2], t[2]]


# ---------------------------------------------------------------------------------------------------------------------------
# The Hundred Steps' balustrades (route/647/rails), on Astra's guards: posts and grips, both sides, four flights, three landings
# ---------------------------------------------------------------------------------------------------------------------------
STEMS = ('n-stair-1', 'n-landing-2', 'n-stair-3', 'n-landing-4', 'n-stair-5', 'n-landing-6', 'n-stair-7')
GRIP = 1.05
BOTTOM = .80          # the bottom rail under the handrail
SPACING = .12         # the bars


def _rail_line(R, side):
    """The handrail's line on one side: (x, [(z, y)...]) through the flights' ends, 1.05 m over their slopes."""
    pts = []
    x = None
    for st in ('n-stair-1', 'n-stair-3', 'n-stair-5', 'n-stair-7'):
        r = R.ramps[st]
        lo, hi = r['low'], r['high']
        x = (r['width'] / 2 - .25) * (1 if side == 'right' else -1)
        for p in (lo, hi):
            q = (p[2], p[1] + GRIP)
            if not pts or abs(pts[-1][0] - q[0]) > 1e-3:
                pts.append(q)
    return x, pts


def _fillet(pts, r, n):
    """A polyline (z, y) with each inner corner rounded by an arc of `r` (n segments)."""
    if n <= 0 or len(pts) < 3:
        return list(pts)
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = np.array(pts[i - 1]), np.array(pts[i]), np.array(pts[i + 1])
        u = (a - b) / np.linalg.norm(a - b)
        v = (c - b) / np.linalg.norm(c - b)
        d = min(r, np.linalg.norm(a - b) * .45, np.linalg.norm(c - b) * .45)
        p0, p1 = b + u * d, b + v * d
        for k in range(n + 1):
            t = k / n
            q = (1 - t) ** 2 * p0 + 2 * (1 - t) * t * b + t * t * p1
            out.append((float(q[0]), float(q[1])))
    out.append(pts[-1])
    return out


def _y_at(line, z):
    for (z0, y0), (z1, y1) in zip(line[:-1], line[1:]):
        if z0 - 1e-6 <= z <= z1 + 1e-6:
            return y0 + (y1 - y0) * (z - z0) / max(z1 - z0, 1e-6)
    return line[0][1] if z < line[0][0] else line[-1][1]


def rails(t, R):
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    iron = E['rail']
    for side in ('left', 'right'):
        x, line = _rail_line(R, side)
        smooth = _fillet(line, .32, 5 if t == 0 else 1)
        path = [(x, y, z) for z, y in smooth]
        mk.tube_path(g, path, .032, iron, 8 if t == 0 else 5, caps=True)
        # the bottom rail, a flat bar under the handrail
        low = [(x, y - BOTTOM, z) for z, y in smooth]
        for a, b in zip(low[:-1], low[1:]):
            mk.seg(g, T, a, b, .014, .035, iron)
        # the posts: Astra's, seated on the treads and landings under them
        posts = sorted(((b[5] + b[6]) / 2, b) for k, b in R.blocks.items()
                       if k.startswith('stair-guard-') and '-%s-post-' % side in k and any(k.startswith('stair-guard-' + s + '-') for s in STEMS))
        zs = []
        for i, (z, b) in enumerate(posts):
            if zs and abs(z - zs[-1]) < .2:
                continue          # a landing's end post where a flight's stands
            zs.append(z)
            px = (b[1] + b[2]) / 2
            top = _y_at(smooth, z)
            foot = R.tread_top(px, z, top - .5)
            if foot is None:
                foot = b[3]
            end = (i == 0 or i == len(posts) - 1)
            w = .09 if end else .055
            bx(g, T, px - w / 2, px + w / 2, foot, top - (0 if end else .03), z - w / 2, z + w / 2, iron, bevel=.008, cell=0)
            if T.detail:
                bx(g, T, px - .06, px + .06, foot, foot + .045, z - .06, z + .06, iron, bevel=.008, cell=0)   # the shoe on the tread
                bx(g, T, px - .045, px + .045, top - .09, top - .06, z - .045, z + .045, iron, bevel=.006, cell=0)
            if end:
                mk.ellipsoid(g, (px, top + .1, z), (.065, .065), iron, 8 if t == 0 else 5, rings=4 if t == 0 else 2)
                bx(g, T, px - .06, px + .06, top + .01, top + .04, z - .06, z + .06, iron, bevel=.006, cell=0)
        # the bars, every 12 cm between the posts (the Fine tier)
        if T.detail:
            for z0, z1 in zip(zs[:-1], zs[1:]):
                n = max(1, int((z1 - z0) / SPACING))
                for k in range(1, n):
                    z = z0 + (z1 - z0) * k / n
                    y = _y_at(smooth, z)
                    # a round bar, smooth, capless (the rails hide its ends): eight corners
                    mk.rod(g, (x, y - BOTTOM + .018, z), (x, y - .03, z), .009, iron, 4, caps=False)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# Leaving camp (route/647/crossing): the camp's apron road, the zebra, the dropped kerb, the footway to the first tread
# ---------------------------------------------------------------------------------------------------------------------------
def _slab(g, x0, x1, z0, z1, y, f):
    g.face(((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)), f, (0, 1, 0))


def _kerb(g, T, a, b, axis, w, h, f, stone=.9):
    """Kerbstones from a to b along x (axis 0) or z (axis 2), w wide, h tall, jointed every `stone` m (Fine)."""
    lo, hi = min(a[axis], b[axis]), max(a[axis], b[axis])
    n = max(1, int(round((hi - lo) / stone))) if T.detail else 1
    for k in range(n):
        s0 = lo + (hi - lo) * k / n + (.004 if T.detail else 0)
        s1 = lo + (hi - lo) * (k + 1) / n - (.004 if T.detail else 0)
        if axis == 0:
            bx(g, T, s0, s1, 0.0 + a[1], h + a[1], a[2] - w / 2, a[2] + w / 2, f, bevel=.012, cell=0, skip=('-y',))
        else:
            bx(g, T, a[0] - w / 2, a[0] + w / 2, 0.0 + a[1], h + a[1], s0, s1, f, bevel=.012, cell=0, skip=('-y',))


def crossing(t, R):
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    fx0, fx1 = R.box('camp-fence-0--1')[0], R.box('camp-fence-0-1')[1]        # -25 .. 25
    zf = R.box('camp-fence-0--1')[5]                                            # 25: the fence's outer face
    zr = zf + 4.6                                                               # the road's north kerb line
    ztread = R.ramps['n-stair-1']['low'][2]                                     # 38: the first tread
    wst = R.ramps['n-stair-1']['width'] / 2                                     # 6
    ex = fx1 - .4
    # the apron road (tar), its zebra (white over the tar) and the zigzags along its kerb
    _slab(g, -ex, ex, zf + .1, zr, .008, F['asphalt'])
    for i in range(10):
        a = -5.0 + i + .25
        _slab(g, a, a + .5, zf + .25, zr - .25, .016, F['paint-line'])
    if T.detail:
        for sgn in (-1, 1):
            # the give-way studs either side of the stripes and the zigzags on the kerb's side, 10 m each way
            for k in range(8):
                z0 = zf + .35 + k * .5
                _slab(g, sgn * 5.55 - .08, sgn * 5.55 + .08, z0, z0 + .25, .016, F['paint-line'])
            zig = [(sgn * (6.3 + k * 1.0), zr - (.35 if k % 2 == 0 else .75)) for k in range(11)]
            for (ax_, az), (bx_, bz) in zip(zig[:-1], zig[1:]):
                mk.seg(g, T, (ax_, .015, az), (bx_, .015, bz), .1, .006, F['paint-line'])
    # the north kerb, dropped (flush) across the crossing, and its returns to the fence at the camp's corners
    h = .06
    _kerb(g, T, (-ex, 0, zr + .075), (-5.0, 0, zr + .075), 0, .15, h, F['granite'])
    _kerb(g, T, (5.0, 0, zr + .075), (ex, 0, zr + .075), 0, .15, h, F['granite'])
    _kerb(g, T, (-5.0, 0, zr + .075), (5.0, 0, zr + .075), 0, .15, .012, F['granite'])
    for sgn in (-1, 1):
        _kerb(g, T, (sgn * (ex + .075), 0, zf + .1), (sgn * (ex + .075), 0, zr + .15), 2, .15, h, F['granite'])
    # the tactile band behind the dropped kerb, then the footway's flags to the first tread, edged in the same granite
    _slab(g, -5.0, 5.0, zr + .15, zr + .95, .013, E['tactile'])
    _slab(g, -wst, wst, zr + .15, ztread - .02, .011, E['york'])
    for sgn in (-1, 1):
        _kerb(g, T, (sgn * (wst - .09), 0, zr + .15), (sgn * (wst - .09), 0, ztread - .02), 2, .18, .016, F['granite'], stone=1.2)
    # Belisha beacons over the gate's fence ends (over 3.1 m: on the fence, never a post in the street)
    if True:
        for sgn in (-1, 1):
            x = sgn * 5.45
            for k in range(4):
                mk.rod(g, (x, 3.22 + k * .22, zf - .1), (x, 3.22 + (k + 1) * .22, zf - .1), .045, F['paint-black'] if k % 2 == 0 else F['paint-cream'], 6 if t == 0 else 4, caps=False)
            mk.ellipsoid(g, (x, 4.32, zf - .1), (.19, .19), glow(0xFFA020, 2.4), 10 if t == 0 else 6, rings=5 if t == 0 else 3)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# Clock Street's footways (route/647/footway): the same kerb and flags up both sides from the stair head, the west one round
# the corner and along the first switchback's frontage
# ---------------------------------------------------------------------------------------------------------------------------
def footway(t, R):
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    lot = R.box('lot-lower-hill-72')
    y = lot[2]                       # 14: Clock Street
    z0 = R.ramps['n-stair-7']['high'][2] + .1          # 75.1: the stair head
    zc = lot[5] + .15                                  # 100.15: the lots' north faces
    zn = zc + 2.85                                     # 103: the switchback frontage's kerb line
    xw = R.ramps['n-ramp-9']['high'][0]                # -56: the switchback's top
    h = .06
    # west: kerb at x -7, flags to x -10; turning west at the top along the frontage to the switchback's end
    _slab(g, -10.0, -7.15, z0, zn, y + .011, E['york'])
    _slab(g, xw - 4.0, -10.0, zc, zn, y + .011, E['york'])
    _kerb(g, T, (-7.075, y, z0), (-7.075, y, zn + .15), 2, .15, h, F['granite'])
    _kerb(g, T, (-7.0, y, zn + .075), (xw - 4.0, y, zn + .075), 0, .15, h, F['granite'])
    # east: kerb at x 7, flags to x 10, ending at the junction with a return
    _slab(g, 7.15, 10.0, z0, zn, y + .011, E['york'])
    _kerb(g, T, (7.075, y, z0), (7.075, y, zn + .15), 2, .15, h, F['granite'])
    _kerb(g, T, (7.0, y, zn + .075), (10.0, y, zn + .075), 0, .15, h, F['granite'])
    # granite edging on the footways' back edges (flush), so the flags read as laid
    _kerb(g, T, (-10.09, y, z0), (-10.09, y, zc), 2, .18, .016, F['granite'], stone=1.2)
    _kerb(g, T, (10.09, y, z0), (10.09, y, zn), 2, .18, .016, F['granite'], stone=1.2)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The CITY HALL sign (route/647/sign) high on the terrace wall that closes Clock Street (its face at z 117, facing -z)
# ---------------------------------------------------------------------------------------------------------------------------
def _arrow_left(g, T, x0, x1, ym, z, h, f):
    """A bold arrow pointing -x (left as the wall is seen from the street), lying d off the face."""
    head = h * .9
    mk.seg(g, T, (x0 + head * .55, ym, z), (x1, ym, z), .03, h * .28, f)
    mk.seg(g, T, (x0, ym, z), (x0 + head, ym + head * .62, z), .03, h * .26, f)
    mk.seg(g, T, (x0, ym, z), (x0 + head, ym - head * .62, z), .03, h * .26, f)


def sign(t, R):
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    wall = R.box('terrace-81')
    zf = wall[4]                  # 117
    cx, y0, y1 = -1.0, 18.0, 20.35
    w = 8.4
    green, cream = F['enamel-green'], F['enamel-cream']
    letters = glow(0xF4ECD0, 1.5)
    # the enamel board in its cast frame, standing 8 cm off the wall on brackets
    bx(g, T, cx - w / 2 - .12, cx + w / 2 + .12, y0 - .12, y1 + .12, zf - .1, zf - .02, F['iron-black'], bevel=.02, cell=0, skip=('+z',))
    bx(g, T, cx - w / 2, cx + w / 2, y0, y1, zf - .13, zf - .1, green, bevel=.006, cell=0, skip=('+z',))
    if T.detail:
        e = .07
        for (a0, a1, b0, b1) in ((cx - w / 2 + e, cx + w / 2 - e, y0 + e, y0 + e * 1.8), (cx - w / 2 + e, cx + w / 2 - e, y1 - e * 1.8, y1 - e),
                                 (cx - w / 2 + e, cx - w / 2 + e * 1.8, y0 + e, y1 - e), (cx + w / 2 - e * 1.8, cx + w / 2 - e, y0 + e, y1 - e)):
            g.face(((a0, b0, zf - .132), (a1, b0, zf - .132), (a1, b1, zf - .132), (a0, b1, zf - .132)), cream, (0, 0, -1))
    # the arrow and CITY HALL in lit cream letters, THE ZIGZAG under them
    _arrow_left(g, T, cx - w / 2 + .35, cx - w / 2 + 1.75, y0 + 1.42, zf - .16, .62, letters)
    mk.text_stroke(g, 'CITY HALL', cx + .75, y0 + .95, zf - .13, .118, letters, T, style='flat', d=.03)
    mk.text_stroke(g, 'THE ZIGZAG  CROWN COURT', cx + .75, y0 + .26, zf - .13, .052, cream, T, style='flat', d=.03)
    # two gooseneck lamps over it, their shades lit
    for dx in (-2.6, 2.6):
        x = cx + dx
        mk.seg(g, T, (x, y1 + .1, zf - .03), (x, y1 + .55, zf - .03), .05, .05, F['iron-black'])
        mk.seg(g, T, (x, y1 + .55, zf - .03), (x, y1 + .55, zf - .62), .045, .045, F['iron-black'])
        sk.lathe(g, [(.05, 0), (.2, -.16), (.22, -.2)], (x, y1 + .6, zf - .66), F['enamel-green'], 10 if t == 0 else 6, smooth=False)
        sk.lathe(g, [(.18, -.19), (0, -.2)], (x, y1 + .6, zf - .66), glow(WARM, 2.2), 10 if t == 0 else 6, smooth=False)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# Meridian Stores (route/647/corner) on the corner lot's ground storey: its east face on Clock Street, its north face on the
# switchback. Built per face in the canonical frame (the face at z 0 facing -z, x along it as the street sees it, y over the
# street) and turned onto the lot.
# ---------------------------------------------------------------------------------------------------------------------------
STOREY = 4.0
PRODUCE = ('Assets/ModernSupermarket/Prefabs/Props/Produce/Apple_k7nzrr_Crate.prefab',
           'Assets/ModernSupermarket/Prefabs/Props/Produce/Apple_yf18nm_Crate.prefab',
           'Assets/ModernSupermarket/Prefabs/Props/Produce/Artichoke_fdde0u_Crate.prefab')
SALE = ('Assets/ModernSupermarket/Prefabs/Furniture/Sign_9jlt52_Rounded.prefab',
        'Assets/ModernSupermarket/Prefabs/Furniture/Sign_9jlt52_Star.prefab')
DOOR = 'Assets/ModernSupermarket/Prefabs/Architecture/Door_c5bu08.prefab'


def _shopfront(t, L, door, fascia, sub_text, awning, corner_left, tints, seed):
    """One face of the shop, canonical: L m long; door (x0, x1) or None; the fascia's words; an awning over the windows; the
    corner pilaster at x 0 (corner_left) or at x L."""
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    green, cream, stone = E['shop'], E['shop-cream'], F['stone-dress']
    rec = .22
    # pilasters (6 cm proud), the corner one broader
    pil = [(0.0, .6 if corner_left else .4), (L - (.4 if corner_left else .6), L)]
    if door:
        pil += [(door[0] - .3, door[0]), (door[1], door[1] + .3)]
    for a, b in pil:
        bx(g, T, a, b, 0.0, 3.1, -.06, 0.0, stone, bevel=.01, cell=0, skip=('+z',))
        bx(g, T, a - .04, b + .04, 0.0, .35, -.06, 0.0, F['granite'], bevel=.008, cell=0, skip=('+z',))
    # the windows between the pilasters: a stall riser, the frame, the lit interior behind with its shelves and the produce
    spans = []
    xs = sorted(pil)
    for (a0, a1), (b0, b1) in zip(xs[:-1], xs[1:]):
        if b0 - a1 > .4 and not (door and abs(a1 - door[0]) < .01 and abs(b0 - door[1]) < .01):
            spans.append((a1, b0))
    for k, (a, b) in enumerate(spans):
        bx(g, T, a, b, 0.0, .62, -.045, 0.0, green, bevel=.008, cell=0, skip=('+z',))
        bx(g, T, a, b, .62, .7, -.06, 0.0, stone, bevel=.005, cell=0, skip=('+z',))           # the sill
        # the frame round the glazing, the glazing bars, the lit back
        bx(g, T, a, b, 2.95, 3.1, -.05, 0.0, green, bevel=.005, cell=0, skip=('+z',))
        g.face(((a, .7, rec), (b, .7, rec), (b, 2.95, rec), (a, 2.95, rec)), glow(WARM, .95), (0, 0, -1))
        g.face(((a, .7, 0.0), (a, .7, rec), (a, 2.95, rec), (a, 2.95, 0.0)), green, (1, 0, 0))
        g.face(((b, .7, rec), (b, .7, 0.0), (b, 2.95, 0.0), (b, 2.95, rec)), green, (-1, 0, 0))
        g.face(((a, 2.95, 0.0), (b, 2.95, 0.0), (b, 2.95, rec), (a, 2.95, rec)), green, (0, -1, 0))
        g.face(((a, .7, rec), (b, .7, rec), (b, .7, 0.0), (a, .7, 0.0)), F['timber-old'], (0, 1, 0))
        n = max(1, int(round((b - a) / 1.3)))
        for i in range(1, n):
            x = a + (b - a) * i / n
            bx(g, T, x - .03, x + .03, .7, 2.95, -.02, .03, green, bevel=0, cell=0)
        bx(g, T, a, b, 2.35, 2.4, -.02, .03, green, bevel=0, cell=0)                              # the transom
        if T.detail:
            # shelves inside the window (dark), the produce crates on the sill and the stall boards, SALE cards
            for ys in (1.45, 2.05):
                bx(g, T, a + .05, b - .05, ys, ys + .03, rec - .12, rec - .005, F['timber-old'], bevel=0, cell=0)
            # the produce: crates on the sill (the pack's, clustered to 3 cm: lumps of fruit at street distance)
            m = max(1, int((b - a) / .6))
            for i in range(m):
                if i % 2:
                    continue          # every other slot: a crate and a gap (the shelves read behind)
                x0 = a + .1 + i * (b - a - .2) / m
                x1 = x0 + (b - a - .2) / m - .06
                pr = PRODUCE[(seed + i) % len(PRODUCE)]
                m_ = fit('Modern Supermarket', pr, (x0, .702, rec - .2), (x1, 1.0, rec - .005), yaw=180)
                if m_ is not None:
                    pack_part(g, tints, 'Modern Supermarket', pr, m_, cell=.05)
            if k % 2 == 0:
                sp = SALE[(seed + k) % len(SALE)]
                m_ = fit('Modern Supermarket', sp, (a + .25, 1.6, .005), (a + .85, 2.3, .02), yaw=90)
                if m_ is not None:
                    pack_part(g, tints, 'Modern Supermarket', sp, m_)
    # the door: recessed, the supermarket's sliding doors in a lit vestibule, a step
    if door:
        a, b = door
        dr = .25
        g.face(((a, 0.0, dr), (b, 0.0, dr), (b, 3.1, dr), (a, 3.1, dr)), glow(WARM, 1.25), (0, 0, -1))
        g.face(((a, 0.0, 0.0), (a, 0.0, dr), (a, 3.1, dr), (a, 3.1, 0.0)), stone, (1, 0, 0))
        g.face(((b, 0.0, dr), (b, 0.0, 0.0), (b, 3.1, 0.0), (b, 3.1, dr)), stone, (-1, 0, 0))
        g.face(((a, 3.1, 0.0), (b, 3.1, 0.0), (b, 3.1, dr), (a, 3.1, dr)), stone, (0, -1, 0))
        m_ = fit('Modern Supermarket', DOOR, (a + .05, .02, dr - .14), (b - .05, 2.6, dr - .02), keep_aspect=False)
        if m_ is not None:
            pack_part(g, tints, 'Modern Supermarket', DOOR, m_, fin=E['pack-metal'], keep=lambda n: 'glass' not in n.lower())
        bx(g, T, a, b, 2.62, 3.1, -.02, dr - .1, green, bevel=0, cell=0)
        bx(g, T, a - .05, b + .05, 0.0, .04, -.25, 0.0, F['granite'], bevel=.008, cell=0, skip=('-y',))
    # the wall between the shopfront's head and the fascia (the street dress draws nothing on these bays)
    g.face(((0.0, 3.09, 0.0), (L, 3.09, 0.0), (L, 3.13, 0.0), (0.0, 3.13, 0.0)), stone, (0, 0, -1))
    # the fascia over 3.1 m: a deep board with lit letters, its cornice; the brackets at its ends
    bx(g, T, 0.0, L, 3.12, 3.86, -.24, 0.0, green, bevel=.012, cell=0, skip=('+z',))
    bx(g, T, .1, L - .1, 3.2, 3.78, -.255, -.24, F['iron-black'], bevel=0, cell=0)
    mk.text_stroke(g, fascia, L / 2, 3.28, -.255, .068, glow(0xF6EAC8, 1.6), T, style='flat', d=.02)
    lk.cornice(g, T, 4.0, -.02, L + .02, -.24, stone, s=.35)
    for x in (.12, L - .12):
        bx(g, T, x - .1, x + .1, 3.12, 3.95, -.32, -.24, stone, bevel=.01, cell=0)
    if sub_text and T.detail:
        mk.text_stroke(g, sub_text, L / 2, 3.13 - .001, -.03, .028, cream, T, style='flat', d=.01)
    # the awning: a red and cream striped canvas over the windows, its lowest edge 3.12 m over the street
    if awning:
        a0, a1 = (spans[0][0], spans[-1][1]) if spans else (0.0, L)
        n = max(4, int((a1 - a0) / .38))
        for i in range(n):
            x0, x1 = a0 + (a1 - a0) * i / n, a0 + (a1 - a0) * (i + 1) / n
            f = E['canvas-red'] if i % 2 == 0 else E['canvas-cream']
            g.face(((x0, 3.13, -1.45), (x1, 3.13, -1.45), (x1, 3.11 + .62, -.27), (x0, 3.11 + .62, -.27)), f, (0, .89, -.45))
            g.face(((x0, 3.11 + .62, -.27), (x1, 3.11 + .62, -.27), (x1, 3.13, -1.45), (x0, 3.13, -1.45)), f, (0, -.89, .45))
            # the valance: a scalloped hem standing over 3.1 m (its points at 3.12)
            if T.detail:
                g.face(((x0, 3.13, -1.45), (x1, 3.13, -1.45), (x1, 3.33, -1.45), (x0, 3.33, -1.45)), f, (0, 0, -1))
        bx(g, T, a0, a1, 3.68, 3.82, -.3, -.24, F['iron-black'], bevel=.01, cell=0)          # the roller box
        for x in (a0 + .1, a1 - .1):
            mk.seg(g, T, (x, 3.75, -.26), (x, 3.18, -1.42), .03, .03, F['iron-black'])
    return g


def corner(t, R, tints):
    T = mk.tier(t)
    g = Geo()
    x0, x1, y0, y1, z0, z1 = R.box('lot-lower-hill-72')
    te, tn = Tints(), Tints()
    east = _shopfront(t, z1 - z0, (z1 - z0 - 2.9, z1 - z0 - .9), 'MERIDIAN STORES', 'GROCER  PROVISIONS  OPEN LATE', False, False, te, 1)
    north = _shopfront(t, x1 - x0, None, 'PROVISIONS', 'MERIDIAN STORES', True, True, tn, 4)
    # the tints were recorded against each face's own corners: shifted as each joins g
    _join(g, east, mk.qy(270), (x1, y0, z0), te, tints)
    _join(g, north, mk.qy(180), (x1, y0, z1), tn, tints)
    # the corner: a projecting blade sign and a lamp, over 3.1 m, turned toward the junction (north-east)
    blade = Geo()
    E = finishes()
    bx(blade, T, -.03, .03, 3.4, 4.7, -1.5, -.25, E['shop'], bevel=.01, cell=0)
    if T.detail:
        for s in (-1, 1):
            sub = Geo()
            mk.text_stroke(sub, 'OPEN', 0.0, .0, 0.0, .045, glow(0xFF5A3C, 2.0), T, style='flat', d=.015)
            mk.part(blade, sub, mk.qy(-90 * s), (s * .035, 3.55, -.88))
            sub = Geo()
            mk.text_stroke(sub, 'STORES', 0.0, .0, 0.0, .05, glow(0xF6EAC8, 1.6), T, style='flat', d=.015)
            mk.part(blade, sub, mk.qy(-90 * s), (s * .035, 4.1, -.88))
    mk.seg(blade, T, (0.0, 4.82, 0.0), (0.0, 4.82, -1.55), .05, .05, F['iron-black'])
    lk.lantern(blade, T, 0.0, 4.75, -1.75, s=.7)
    mk.part(g, blade, mk.qy(225), (x1, y0, z1))
    return g


def _join(g, sub, q, t, sub_tints, tints):
    c0 = len(g.p) // 3
    g.extend(sub.transformed(sk.affine(q, t)))
    tints.ranges.extend((c0 + a, col) for a, col in sub_tints.ranges)
    return c0


# ---------------------------------------------------------------------------------------------------------------------------
# Clock Street's roofs (route/647/roofs): hipped slate roofs on its four rows (71 and 72 one roof), chimney stacks on the ridges
# ---------------------------------------------------------------------------------------------------------------------------
ROOF_LOTS = (('lot-lower-hill-71', 'lot-lower-hill-72'), ('lot-lower-hill-73',), ('lot-lower-hill-74',))
PITCH = math.radians(38)


def roofs(t, R):
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    for group in ROOF_LOTS:
        if not all(i in R.blocks for i in group):
            continue
        bs = [R.box(i) for i in group]
        x0, x1 = min(b[0] for b in bs), max(b[1] for b in bs)
        z0, z1 = min(b[4] for b in bs), max(b[5] for b in bs)
        top = max(b[3] for b in bs)
        o = .3                                         # the eaves' overhang
        ye = top + .38                                 # over the street dress's coping
        X0, X1, Z0, Z1 = x0 - o, x1 + o, z0 - o, z1 + o
        half = (Z1 - Z0) / 2 if (X1 - X0) >= (Z1 - Z0) else (X1 - X0) / 2
        rise = half * math.tan(PITCH)
        yr = ye + rise
        along_x = (X1 - X0) >= (Z1 - Z0)
        slate = F['slate']
        # the eaves board (flush on the faces) up to the eaves
        for (a, b, c, d, n) in (((x0, z0 - .02), (x1, z0 - .02), None, None, (0, 0, -1)), ((x1, z1 + .02), (x0, z1 + .02), None, None, (0, 0, 1)),
                                ((x0 - .02, z1), (x0 - .02, z0), None, None, (-1, 0, 0)), ((x1 + .02, z0), (x1 + .02, z1), None, None, (1, 0, 0))):
            g.face(((a[0], top, a[1]), (b[0], top, b[1]), (b[0], ye, b[1]), (a[0], ye, a[1])), F['paint-black'], n)
        # the soffits under the overhang
        g.face(((X0, ye, Z0), (X1, ye, Z0), (X1, ye, Z1), (X0, ye, Z1)), F['paint-black'], (0, -1, 0))
        if along_x:
            r0, r1 = (X0 + half, yr, (Z0 + Z1) / 2), (X1 - half, yr, (Z0 + Z1) / 2)
            planes = [((X0, ye, Z0), (X1, ye, Z0), (r1[0], yr, r1[2]), (r0[0], yr, r0[2])),
                      ((X1, ye, Z1), (X0, ye, Z1), (r0[0], yr, r0[2]), (r1[0], yr, r1[2])),
                      ((X0, ye, Z1), (X0, ye, Z0), (r0[0], yr, r0[2])),
                      ((X1, ye, Z0), (X1, ye, Z1), (r1[0], yr, r1[2]))]
        else:
            r0, r1 = ((X0 + X1) / 2, yr, Z0 + half), ((X0 + X1) / 2, yr, Z1 - half)
            planes = [((X1, ye, Z0), (X1, ye, Z1), (r1[0], yr, r1[2]), (r0[0], yr, r0[2])),
                      ((X0, ye, Z1), (X0, ye, Z0), (r0[0], yr, r0[2]), (r1[0], yr, r1[2])),
                      ((X0, ye, Z0), (X1, ye, Z0), (r0[0], yr, r0[2])),
                      ((X1, ye, Z1), (X0, ye, Z1), (r1[0], yr, r1[2]))]
        for pl in planes:
            nrm = sk._norm(sk._cross(sk._sub(pl[1], pl[0]), sk._sub(pl[2], pl[0])))
            if nrm[1] < 0:
                nrm = (-nrm[0], -nrm[1], -nrm[2])
            g.face(pl, slate, nrm)
            # slate courses: thin lead-grey lines every 0.45 m up the slope (Fine)
        mk.seg(g, T, r0, r1, .16, .12, F['lead'])
        # the hips
        for c in ((X0, ye, Z0), (X1, ye, Z0), (X1, ye, Z1), (X0, ye, Z1)):
            r = r0 if (c[0] - r0[0]) ** 2 + (c[2] - r0[2]) ** 2 < (c[0] - r1[0]) ** 2 + (c[2] - r1[2]) ** 2 else r1
            mk.seg(g, T, c, r, .1, .06, F['lead'])
        # the gutters along the eaves
        for (a, b) in (((X0, Z0), (X1, Z0)), ((X1, Z1), (X0, Z1)), ((X0, Z1), (X0, Z0)), ((X1, Z0), (X1, Z1))):
            mk.seg(g, T, (a[0], ye - .04, a[1]), (b[0], ye - .04, b[1]), .12, .1, F['iron-black'])
        # chimney stacks astride the ridge: at the party walls and the ends
        xs = sorted({x0 + 2.2, x1 - 2.2} | {b[1] for b in bs[:-1]}) if along_x else [(x0 + x1) / 2]
        zs = [(z0 + z1) / 2] if along_x else sorted({z0 + 2.2, z1 - 2.2})
        for cx in xs:
            for cz in zs:
                ys = yr - .6
                w, d = (.7, 1.1) if along_x else (1.1, .7)
                bx(g, T, cx - w / 2, cx + w / 2, ys, yr + 1.5, cz - d / 2, cz + d / 2, F['brick-stock'], bevel=.02, cell=0)
                bx(g, T, cx - w / 2 - .06, cx + w / 2 + .06, yr + 1.35, yr + 1.5, cz - d / 2 - .06, cz + d / 2 + .06, F['stone-dress'], bevel=.01, cell=0)
                for k in range(3):
                    off = (k - 1) * .32
                    px, pz = (cx, cz + off) if along_x else (cx + off, cz)
                    sk.lathe(g, [(.11, 0), (.09, .3), (.12, .36), (.1, .42)], (px, yr + 1.5, pz), finishes()['pot'], 8 if t == 0 else 5, smooth=False)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The Ironworks' loading bay (route/front/ironworks/loading): two of the door front's 2.5 m bays (x 0..5, stretched at runtime
# with them), the hall's buff brick and blue bands carried through, the loading opening with its half-raised shutter and the
# furnace's glow under it, Old Industry's personnel door, steel window bay, sheet canopy on brackets and hanging lamp.
# ---------------------------------------------------------------------------------------------------------------------------
HALL_H = 16.0
OI = 'Old Industry'
OI_DOOR = 'Assets/OldIndustry/Prefabs/Architecture/Doors/Door03.prefab'
OI_WINDOW = 'Assets/OldIndustry/Prefabs/Architecture/Windows/WindowBay01.prefab'
OI_CANOPY = 'Assets/OldIndustry/Prefabs/Architecture/Roofs/Roof03a.prefab'
OI_LAMP = 'Assets/OldIndustry/Prefabs/Furniture/Lamps/Lamp01_Hanging02.prefab'
OI_GUTTER = 'Assets/OldIndustry/Prefabs/Architecture/Gutters/Gutter01_Up.prefab'


def loading(t, R, tints):
    T = mk.tier(t)
    E = finishes()
    g = Geo()
    W = 5.0
    buff, blue = F['brick-buff'], F['brick-blue']
    ox0, ox1, oy1 = .35, 3.55, 4.1                   # the loading opening
    dx0, dx1 = 3.95, 4.85                            # the personnel door
    wy0, wy1 = 6.0, 8.8                              # the window bay
    rec = .22                                        # the reveals' back (the hall's face is at z 0; art no deeper than 0.25 m)
    # the wall: buff brick, the openings cut, 2 cm proud of the hall's face as the bays are; plain reveals (no glazing of the kit's)
    holes = [mk.Hole(ox0, ox1, 0.0, oy1, 0.0), mk.Hole(dx0, dx1, 0.0, 2.2, 0.0), mk.Hole(.4, 4.6, wy0, wy1, 0.0)]
    mk.wall(g, T, -.02, 0.0, W, 0.0, HALL_H, buff, holes=holes, n=-1)
    for h_ in holes:
        mk.reveal(g, T, h_, -.02, rec, buff, sill=h_.y0 > .5, head=True)
    bx(g, T, 0.0, ox0, 0.0, .6, -.07, -.02, blue, bevel=.01, skip=('+z',))
    bx(g, T, ox1, dx0, 0.0, .6, -.07, -.02, blue, bevel=.01, skip=('+z',))
    bx(g, T, dx1, W, 0.0, .6, -.07, -.02, blue, bevel=.01, skip=('+z',))
    for y in (11.6, 14.0):
        bx(g, T, 0.0, W, y, y + .3, -.06, -.02, blue, bevel=.005, skip=('+z',))
    lk.cornice(g, T, HALL_H - .015, 0.0, W, -.02, blue, s=.5)
    # the opening: blue-brick jambs with hazard paint, a steel lintel, the shutter half up over the furnace's glow
    for a, b in ((ox0 - .3, ox0), (ox1, ox1 + .3)):
        bx(g, T, a, b, 0.0, oy1 + .05, -.06, -.02, blue, bevel=.01, skip=('+z',))
        for k in range(6):
            y = .1 + k * .3
            g.face(((a, y, -.062), (b, y + .15, -.062), (b, y + .3, -.062), (a, y + .15, -.062)), E['hazard'] if k % 2 == 0 else F['paint-black'], (0, 0, -1))
    bx(g, T, ox0 - .35, ox1 + .35, oy1, oy1 + .36, -.08, -.02, F['steel-dark'], bevel=.01, skip=('+z',))
    shutter_bottom = 1.55
    g.face(((ox0, 0.0, rec - .01), (ox1, 0.0, rec - .01), (ox1, shutter_bottom, rec - .01), (ox0, shutter_bottom, rec - .01)), glow(FURNACE, .7), (0, 0, -1))
    n = int((oy1 - shutter_bottom) / .11)
    for k in range(n):
        y = shutter_bottom + k * (oy1 - shutter_bottom) / n
        bx(g, T, ox0 + .02, ox1 - .02, y, y + (oy1 - shutter_bottom) / n - .015, rec - .09, rec - .05, E['shutter'], bevel=0, cell=0)
    bx(g, T, ox0 + .02, ox1 - .02, shutter_bottom - .06, shutter_bottom, rec - .11, rec - .04, F['steel-dark'], bevel=0, cell=0)
    # the dock's edge: a steel nosing at the opening's foot (flush)
    bx(g, T, ox0, ox1, 0.0, .03, -.05, rec, F['steel-dark'], bevel=0, cell=0, skip=('-y',))
    # Old Industry's personnel door in its reveal, its steel window bay above, lit from the furnace behind
    m_ = fit(OI, OI_DOOR, (dx0 + .02, .0, rec - .14), (dx1 - .02, 2.18, rec - .02), keep_aspect=False)
    if m_ is not None:
        pack_part(g, tints, OI, OI_DOOR, m_, fin=E['pack-metal'])
    else:
        bx(g, T, dx0, dx1, 0.0, 2.2, rec - .06, rec, F['enamel-red'], bevel=0, cell=0)
    g.face(((.4, wy0, rec - .003), (4.6, wy0, rec - .003), (4.6, wy1, rec - .003), (.4, wy1, rec - .003)), glow(FURNACE, 1.1), (0, 0, -1))
    m_ = fit(OI, OI_WINDOW, (.4, wy0, rec - .1), (4.6, wy1, rec - .005), keep_aspect=False)
    if m_ is not None:
        pack_part(g, tints, OI, OI_WINDOW, m_, fin=E['pack-metal'], keep=lambda nm: 'glass' not in nm.lower() or 'opaque' in nm.lower(), cell=.02)
    else:
        for k in range(1, 6):
            x = .4 + 4.2 * k / 6
            bx(g, T, x - .03, x + .03, wy0, wy1, rec - .05, rec, F['iron-raw'], bevel=0, cell=0)
    # the canopy over the opening on steel brackets (its lowest edge 3.6 m up), its gutter, the lamp under it
    cy = 4.75
    m_ = fit(OI, OI_CANOPY, (ox0 - .4, cy, -2.3), (ox1 + .4, cy + .45, -.02), keep_aspect=False)
    if m_ is not None:
        pack_part(g, tints, OI, OI_CANOPY, m_, fin=E['pack-metal'])
    else:
        g.face(((ox0 - .4, cy + .4, -.02), (ox1 + .4, cy + .4, -.02), (ox1 + .4, cy, -2.3), (ox0 - .4, cy, -2.3)), F['galv'], (0, .98, -.18))
    for x in (ox0 - .2, (ox0 + ox1) / 2, ox1 + .2):
        mk.seg(g, T, (x, 3.6, -.02), (x, cy - .02, -2.1), .08, .06, F['steel-dark'])
        mk.seg(g, T, (x, cy - .02, -.02), (x, cy - .02, -2.25), .1, .08, F['steel-dark'])
    m_ = fit(OI, OI_GUTTER, (ox0 - .4, cy - .12, -2.42), (ox1 + .4, cy + .02, -2.26), keep_aspect=False)
    if m_ is not None:
        pack_part(g, tints, OI, OI_GUTTER, m_, fin=E['pack-metal'])
    m_ = fit(OI, OI_LAMP, ((ox0 + ox1) / 2 - .4, 3.6, -1.6), ((ox0 + ox1) / 2 + .4, cy - .05, -.8))
    if m_ is not None:
        pack_part(g, tints, OI, OI_LAMP, m_, fin=E['pack-metal'])
        sk.lathe(g, [(.12, 0), (0, -.06)], ((ox0 + ox1) / 2, 3.66, -1.2), glow(0xFFC878, 2.4), 8 if t == 0 else 5, smooth=False)
    else:
        lk.lantern(g, T, (ox0 + ox1) / 2, 3.98, -1.2, s=.45, glass=glow(0xFFC878, 2.4))
    # the painted sign on the brick over the window bay
    mk.text_stroke(g, 'LOADING BAY 3', W / 2, 9.55, -.025, .07, F['paint-cream'], T, style='flat', d=.004)
    mk.text_stroke(g, 'CITY IRONWORKS', W / 2, 10.25, -.025, .055, F['paint-cream'], T, style='flat', d=.004)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The smoke's puff (route/smoke/puff): a unit low-poly puff (an icosahedron once divided, flat), paler on top
# ---------------------------------------------------------------------------------------------------------------------------
def puff(t):
    finishes()
    g = Geo()
    p = (1 + 5 ** .5) / 2
    V = [(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p), (0, -1, -p), (0, 1, -p), (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)]
    V = [np.array(v, dtype=np.float64) / np.linalg.norm(v) for v in V]
    Fc = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
          (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    tris = []
    for a, b, c in Fc:
        A, B, C = V[a], V[b], V[c]
        ab, bc, ca = [(x / np.linalg.norm(x)) for x in ((A + B) / 2, (B + C) / 2, (C + A) / 2)]
        tris += [(A, ab, ca), (B, bc, ab), (C, ca, bc), (ab, bc, ca)]
    shades = []
    for k, (A, B, C) in enumerate(tris):
        # a lumpy puff: each corner pushed out a little by a fixed hash, flat faces
        pts = []
        for P_ in (A, B, C):
            h = math.sin(P_[0] * 12.9898 + P_[1] * 78.233 + P_[2] * 37.719) * 43758.5453
            h = h - math.floor(h)
            pts.append(tuple(P_ * (.88 + .24 * h)))
        g.face(pts, X['puff'], normal=tuple(np.mean(np.array(pts), axis=0)))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# Building, checking and writing the bank
# ---------------------------------------------------------------------------------------------------------------------------
def floors_of(g):
    P = np.frombuffer(g.p, dtype=np.float32).reshape(-1, 3)
    lo = float(P[:, 1].min()) if len(P) else 0.0
    return tuple(sorted({round(lo, 2), 0.0, 3.5, 7.0, 10.5, 14.0, 21.0}))


def registry(R):
    reg = {
        'route/647/rails': lambda t, tn: rails(t, R),
        'route/647/crossing': lambda t, tn: crossing(t, R),
        'route/647/footway': lambda t, tn: footway(t, R),
        'route/647/sign': lambda t, tn: sign(t, R),
        'route/647/corner': lambda t, tn: corner(t, R, tn),
        'route/647/roofs': lambda t, tn: roofs(t, R),
        'route/front/ironworks/loading': lambda t, tn: loading(t, R, tn),
        'route/smoke/puff': lambda t, tn: puff(t),
    }
    return reg


def build_all(R, only=None):
    finishes()
    reg = registry(R)
    sb = sk.StageBank()
    sb.far_layers = False
    stats, built = {}, {}
    for key in sorted(reg):
        if only and not any(key.startswith(o) for o in only):
            continue
        for t in (0, 2):
            if key == 'route/smoke/puff' and t == 2:
                continue
            if t == 2 and stats.get(key, {}).get('0', 0) < LOW_FROM:
                continue
            t0 = time.time()
            tints = Tints()
            g = reg[key](t, tints)
            if g is None or not g.faces:
                continue
            ao = None
            if ARGS['ao'] and t == 0 and key != 'route/smoke/puff':
                ao = sk.bake_ao([g], distance=.8, samples=12)[0]
            bk = sk.Bake(g, ao=ao, floors=floors_of(g), seed=len(key))
            tints.apply(bk)
            name = key + '/' + TIER_NAME[t]
            om = sk.to_mesh(name, g, bk)
            if om is None:
                continue
            tris = sum(len(s) for s in om.submeshes) // 3
            if t == 2 and tris > .6 * stats.get(key, {}).get('0', 0):
                print('skip %-34s 2  %7d tris: not light enough to bank a Low tier' % (key, tris))
                continue
            sb.add(name, GROUP, om)
            stats.setdefault(key, {})[TIER_NAME[t]] = tris
            cap = CAPS[key][0 if t == 0 else 1]
            if tris > cap:
                raise SystemExit('%s tier %s: %d triangles over its cap %d (Tests/MeridianRouteChecks.Cap)' % (key, t, tris, cap))
            if t == 0:
                built[key] = (g, bk)
            print('built %-34s %-2s %7d tris %6d verts  %.1f s' % (key, TIER_NAME[t], tris, len(om.positions), time.time() - t0))
    return sb, stats, built


def check_free(R, built):
    """Points of the world-framed pieces standing free below 3.1 m (more than 0.25 m from every block, not flat paint of 6 cm or
    less): the art rule. Returns {key: (count, worst point)}."""
    B = np.array([b[1:7] for b in R.d['blocks']], dtype=np.float64)
    out = {}
    for key, (g, bk) in built.items():
        if not key.startswith('route/647/'):
            continue
        P, N, S, Fi = g.arrays()
        P = P.astype(np.float64)
        starts = np.zeros(len(S), dtype=np.int64)
        starts[1:] = np.cumsum(S[:-1])
        cent = np.add.reduceat(P, starts, axis=0) / S[:, None]
        pts = np.concatenate([P, cent])
        lo, hi = pts.min(axis=0) - 4, pts.max(axis=0) + 4
        near = np.nonzero((B[:, 0] < hi[0]) & (B[:, 1] > lo[0]) & (B[:, 4] < hi[2]) & (B[:, 5] > lo[2]))[0]
        Bn = B[near]
        bad, worst = 0, None
        for c0 in range(0, len(pts), 8000):
            q = pts[c0:c0 + 8000]
            inside_xz = (q[:, None, 0] >= Bn[None, :, 0] - 1e-3) & (q[:, None, 0] <= Bn[None, :, 1] + 1e-3) & \
                        (q[:, None, 2] >= Bn[None, :, 4] - 1e-3) & (q[:, None, 2] <= Bn[None, :, 5] + 1e-3)
            below = Bn[None, :, 3] <= q[:, None, 1] + .05
            floor = np.where(inside_xz & below, Bn[None, :, 3], -1e9).max(axis=1)
            h = q[:, 1] - floor
            dx = np.maximum(np.maximum(Bn[None, :, 0] - q[:, None, 0], 0), q[:, None, 0] - Bn[None, :, 1])
            dy = np.maximum(np.maximum(Bn[None, :, 2] - q[:, None, 1], 0), q[:, None, 1] - Bn[None, :, 3])
            dz = np.maximum(np.maximum(Bn[None, :, 4] - q[:, None, 2], 0), q[:, None, 2] - Bn[None, :, 5])
            dist = np.sqrt(dx * dx + dy * dy + dz * dz).min(axis=1)
            b = (h < 3.08) & (h > -50) & (dist > .25) & (h > .061)
            idx = np.nonzero(b)[0]
            bad += len(idx)
            if len(idx):
                i = idx[np.argmax(dist[idx])]
                worst = [round(float(v), 2) for v in q[i]] + [round(float(h[i]), 2), round(float(dist[i]), 2)]
        out[key] = (bad, worst)
    return out


def preview(R, built, out_dir):
    """Check renders of the world-framed pieces in place, over the blocks' boxes (grey)."""
    geos = []
    box = Geo()
    for b in R.d['blocks']:
        if b[0].startswith('stair-guard-'):
            continue
        if b[2] < -70 or b[1] > 70 or b[6] < 15 or b[5] > 135:
            continue
        sk.box(box, b[1], b[2], b[3], b[4], b[5], b[6], F['concrete'], None, 0.0, cell=0)
    geos.append((box, sk.Bake(box, floors=(0.0,))))
    for key, (g, bk) in built.items():
        if key.startswith('route/647/'):
            geos.append((g, bk))
    views = [('gate', (0, 1.7, 26.5), (0, 6, 60), 62), ('gate_edge', (-3.5, 1.7, 27), (-6, 0, 38), 60),
             ('rails_low', (3.4, 2.9, 40.2), (5.75, 1.4, 42.2), 60), ('rails_join', (3.2, 5.2, 46.4), (5.75, 4.2, 48.2), 60),
             ('flights_side', (-22, 9, 41), (0, 6, 57), 55), ('top_north', (0, 15.7, 78), (-6, 17, 112), 62),
             ('corner', (-8, 15.7, 86), (-27, 17, 97), 60), ('corner_close', (-16, 15.7, 96), (-25, 16, 99), 60),
             ('sign', (0, 15.7, 95), (-1, 19, 117), 55), ('roofs', (0, 1.7, 35), (0, 30, 120), 60),
             ('window', (-21.5, 15.7, 93.5), (-25, 15.4, 93.5), 60), ('door', (-21.5, 15.7, 98.1), (-25, 15.4, 98.1), 60),
             ('north_face', (-29, 15.7, 104), (-30, 15.6, 100), 70)]
    sk.preview(geos, views, out_dir, size=(1280, 720))
    lg = built.get('route/front/ironworks/loading')
    if lg:
        sk.preview([lg], [('loading', (2.5, 3.0, -9), (2.5, 4, 0), 60), ('loading_far', (2.5, 8, -24), (2.5, 7, 0), 50)], out_dir, size=(1280, 720))


def main():
    R = Route(ARGS['route'])
    only = ARGS['only'].split(',') if ARGS['only'] else None
    sb, stats, built = build_all(R, only)
    out = ARGS['out']
    path = sb.write(out, BANK)
    report = {'bank': path, 'bytes': os.path.getsize(path), 'stats': stats, 'packs': bool(ARGS['packs'])}
    free = check_free(R, built)
    report['free'] = {k: {'count': v[0], 'worst': v[1]} for k, v in free.items()}
    for k, v in sorted(free.items()):
        print('free below 3.1 m: %-24s %6d %s' % (k, v[0], v[1] or ''))
    if ARGS.get('preview'):
        preview(R, built, ARGS['preview'])
    rp = ARGS['report'] or os.path.join(out, 'route-report.json')
    with open(rp, 'w') as fh:
        json.dump(report, fh, indent=1)
    print('wrote', rp, report['bytes'], 'bytes')


if __name__ == '__main__':
    main()
