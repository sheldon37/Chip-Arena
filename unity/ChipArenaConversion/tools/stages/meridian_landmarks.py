"""Meridian's landmarks and doors: the bank ChipArenaCityLandmarks (Core NativeMeridianLandmarks, Runtime NativeMeridianLandmarks).

Run inside Blender 5.x (bpy, numpy), headless:

  blender --background --factory-startup --python tools/stages/meridian_landmarks.py -- \
      --layouts LAYOUTS.json --out OUT_DIR [--sources SRC] [--only PREFIX,...] [--no-ao] [--report REPORT.json]

LAYOUTS.json is what the CoreTests flag `--meridian-landmarks-dump LAYOUTS.json` writes: the five approved layouts' blocks, ramps,
routes, halls and Core's placement of every piece. The bank goes to OUT_DIR/ChipArenaLocal/Resources/ChipArenaCityLandmarks.bytes
(keep it out of the repository; the integrator carries it in the Drive zip). The report (default OUT_DIR/landmarks-report.json)
holds every piece's triangles per tier, the art-rule check on every layout and the triangles in view from each camp eye.

The Meridian kit (meridian_kit.py: the shared finishes, walls, mouldings, lettering, clocks and lamps of the approved board 3) is
not in the repository yet; SRC is the durable copy of the board-3 sources (default below), whose `gates-caches/pass2` holds it.

Bank names (the runtime reads them):
  <key>/0     the Fine tier (LOD0; the Mid tier shares it)       group "landmark"
  <key>/2     the Low tier (LOD1, and every piece on the Low graphics tier)
  <key>/far   the far tier (the far layer's own mesh, drawn with the fog-capped far material)
Keys are Core's: exit/<layout>/<gate>-<name>, court/final, front/<family>/<part>, far/<family>, far/mast.

The art rules (the common brief; art bible 2.3), checked here on the built meshes in every layout and hall: art standing below
3.1 m over the floor beneath it lies within 0.25 m of a collider (flat paint of 6 cm or less excepted), and every exposed face of a
block the art replaces has art within 0.25 m of it. Presentation only: no collider, no rule, no network change.
"""
import json
import math
import os
import sys
import time
import zlib

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_SOURCES = r'F:/Chip Arena Development/scratch/meridian/scratch-sources-2026-11-19'
BANK = 'ChipArenaCityLandmarks'


def _args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    a = {'layouts': None, 'out': None, 'sources': DEFAULT_SOURCES, 'only': None, 'ao': True, 'report': None, 'check': True,
         'preview': None, 'seeds': None}
    i = 0
    while i < len(argv):
        k = argv[i]
        if k == '--no-ao':
            a['ao'] = False
        elif k == '--no-check':
            a['check'] = False
        else:
            a[k.lstrip('-')] = argv[i + 1]
            i += 1
        i += 1
    return a


ARGS = _args()
os.environ.setdefault('CHIP_STAGES', HERE)
for p in (HERE, os.path.join(ARGS['sources'], 'gates-caches', 'pass2')):
    if p not in sys.path:
        sys.path.insert(0, p)

import numpy as np  # noqa: E402
import stagekit as sk  # noqa: E402
import meridian_kit as mk  # noqa: E402
import meridian_lm_kit as lk  # noqa: E402
import meridian_lm_exits as lx  # noqa: E402
import meridian_lm_exits_b  # noqa: E402,F401  (registers the Navigation's and the Arcades' exits)
import meridian_lm_exits_c  # noqa: E402,F401  (registers the Stride's and the Yards')
import meridian_lm_civic as lc  # noqa: E402
import meridian_lm_fronts as lf  # noqa: E402
import meridian_lm_far as lr  # noqa: E402


# ---------------------------------------------------------------------------------------------------------------------------
# Baking (stagekit's bake, with paint and lettering kept out of the occluders, as the board-3 modules were baked)
# ---------------------------------------------------------------------------------------------------------------------------
def subgeo(g, mask):
    import array
    P, N, S, F = g.arrays()
    starts = np.zeros(len(S), dtype=np.int64)
    if len(S) > 1:
        starts[1:] = np.cumsum(S[:-1])
    sel = np.nonzero(mask)[0]
    out = sk.Geo()
    if len(sel) == 0:
        return out, np.zeros(0, dtype=np.int64)
    counts = S[sel]
    cidx = np.repeat(starts[sel], counts) + (np.arange(counts.sum()) - np.repeat(np.cumsum(counts) - counts, counts))
    out.p = array.array('f')
    out.p.frombytes(P[cidx].astype(np.float32).tobytes())
    out.n = array.array('f')
    out.n.frombytes(N[cidx].astype(np.float32).tobytes())
    out.size = array.array('B')
    out.size.frombytes(S[sel].astype(np.uint8).tobytes())
    out.fin = array.array('H')
    out.fin.frombytes(F[sel].astype(np.uint16).tobytes())
    return out, cidx


def bake_ao(g, reach, samples):
    """Cycles occlusion for a piece, its layered faces (paint, letters, plates) baked over its base faces."""
    lay = sk.layer_faces(g) if g.faces else np.zeros(0, dtype=np.int64)
    base, bi = subgeo(g, lay == 0)
    dec, di = subgeo(g, lay > 0)
    ao = np.ones(len(g.p) // 3, dtype=np.float32)
    if base.faces:
        ao[bi] = sk.bake_ao([base], distance=reach, samples=samples)[0]
    if dec.faces:
        ao[di] = sk.bake_ao([dec], distance=reach, samples=samples, occluders=[base] if base.faces else [])[0]
    return ao


# ---------------------------------------------------------------------------------------------------------------------------
# The registry: every key Core places, and the builder of each tier
# ---------------------------------------------------------------------------------------------------------------------------
def registry(D):
    """key -> (builder(tier, ctx) -> Geo, tiers, occlusion reach, seed, floors). tier: 0 Fine, 2 Low, 'far'."""
    reg = {}
    for L in D['layouts']:
        for p in L['pieces']:
            if p['kind'] == 'Exit':
                fn = lx.BUILDERS.get(p['key'].split('-', 1)[1])
                if fn is None:
                    print('no builder yet for', p['key'])
                    continue
                reg[p["key"]] = (lambda t, c, fn=fn, L=L: fn(t, lk.Site(L)), (0, 2), 1.0, zlib.crc32(p["key"].encode()) & 0xFFFF)
    reg['court/final'] = (lambda t, c: lc.court(t, lk.Site(D['layouts'][0])), (0, 2, 'far'), 1.2, 61)
    for fam in lf.FAMILIES:
        for part in lf.PARTS:
            reg['front/%s/%s' % (fam, part)] = (lambda t, c, fam=fam, part=part: lf.build(fam, part, t), (0, 2), .8, 70 + len(reg))
    for fam in lr.FAMILIES:
        reg['far/' + fam] = (lambda t, c, fam=fam: lr.build(fam, t), ('far',), 2.0, 90 + len(reg))
    reg['far/mast'] = (lambda t, c: lr.mast(t), ('far',), 2.0, 99)
    return reg


TIER_NAME = {0: '0', 2: '2', 'far': 'far'}


def triangle_cap(key, t):
    """Core NativeMeridianLandmarks.TriangleCap (tier 0 Fine, 2 Low, 'far'): a piece over its cap is refused."""
    t = 3 if t == 'far' else t
    if key.startswith('exit/'):
        return {0: 14000, 2: 2500}.get(t, 0)
    if key == 'court/final':
        return {0: 45000, 2: 8000, 3: 600}[t]
    if key == 'far/mast':
        return 1600 if t == 3 else 0
    if key.startswith('far/'):
        return 2500 if t == 3 else 0
    part = key.rsplit('/', 1)[-1]
    caps = {'door': (4000, 1500), 'rear': (1500, 400), 'bay': (700, 150), 'side': (700, 150), 'back': (700, 150), 'lining': (120, 120), 'roof': (4, 4)}
    c = caps.get(part, (500, 300))
    return c[0] if t == 0 else c[1] if t == 2 else 0
LOW_FROM = 2500     # LOD0 triangles from which a piece also banks a Low tier


def build_all(D, only=None):
    mk.finishes()
    lk.finishes()
    reg = registry(D)
    sb = sk.StageBank()
    stats = {}
    built = {}
    meshes = {}
    for L in D['layouts']:
        g0 = [p for p in L['pieces'] if p['key'] == 'court/final']
        if g0:
            m = g0[0]['place']
            lc.check(lk.Site(L), (m[3], m[7], m[11]))
    for key in sorted(reg):
        if only and not any(key.startswith(o) for o in only):
            continue
        fn, tiers, reach, seed = reg[key]
        part = key.split('/')[-1]
        if key.startswith('front/') and part in lf.SHARED:
            src = key.rsplit('/', 1)[0] + '/' + lf.SHARED[part]
            for t in tiers:
                if (src, t) in meshes:
                    sb.share(key + '/' + TIER_NAME[t], 'landmark', meshes[(src, t)])
                    stats.setdefault(key, {})[TIER_NAME[t]] = stats[src][TIER_NAME[t]]
            if src in built:
                built[key] = built[src]
            continue
        for t in tiers:
            # a small piece keeps its Fine mesh at every distance (the Low tier would cost the bank more than it saves a view)
            if t == 2 and stats.get(key, {}).get('0', 0) < LOW_FROM:
                continue
            t0 = time.time()
            g = fn(t, None)
            if g is None or not g.faces:
                continue
            ao = bake_ao(g, reach, 16 if t == 0 else 8) if (ARGS['ao'] and t == 0) else None
            if ao is not None and os.environ.get('LM_AO_DEBUG'):
                print('ao', key, float(ao.min()), float(ao.mean()), float(ao.max()))
            bk = sk.Bake(g, ao=ao, floors=lk.floors_of(g), seed=seed)
            name = key + '/' + TIER_NAME[t]
            om = sk.to_mesh(name, g, bk)
            if om is None:
                continue
            sb.add(name, 'landmark', om)
            meshes[(key, t)] = len(sb.bank.meshes) - 1
            if t == 0 or t == 'far':
                built[key if t == 0 else key + '#far'] = g
                if t == 'far' and key not in built:
                    built[key] = g
            tris = sum(len(s) for s in om.submeshes) // 3
            stats.setdefault(key, {})[TIER_NAME[t]] = tris
            if tris > triangle_cap(key, t):
                raise SystemExit('%s tier %s: %d triangles over its cap %d (Core NativeMeridianLandmarks.TriangleCap)' % (key, t, tris, triangle_cap(key, t)))
            print('built %-44s %-3s %7d tris  %.1f s' % (key, TIER_NAME[t], tris, time.time() - t0))
    return sb, stats, built


# ---------------------------------------------------------------------------------------------------------------------------
# The art rules on the built meshes, in every layout
# ---------------------------------------------------------------------------------------------------------------------------
def world(g, m):
    P, N, S, F = g.arrays()
    A = np.array(m, dtype=np.float64).reshape(3, 4)
    return P.astype(np.float64) @ A[:, :3].T + A[:, 3], N, S


def check_layout(L, built, families):
    """Points of every placed piece that stand free below 3.1 m (more than 0.25 m from every block and not flat paint), and the
    exposed faces of replaced blocks with no art within 0.25 m. Returns a dict of findings."""
    B = np.array([b[1:7] for b in L['blocks']], dtype=np.float64)   # x0 x1 y0 y1 z0 z1
    ceiling = L['bounds'][4]          # art over the map's ceiling is out of every pilot's reach
    ids = [b[0] for b in L['blocks']]
    found = {'free': [], 'bare': [], 'count': {}}
    samples = []          # every placed piece's art points (for the bare-face check)
    for p in L['pieces']:
        keys = [p['key']]
        if p['kind'] in ('Front', 'FrontRig'):
            fam = p['key'].split('/')[1]
            keys = [p['key'].replace('/' + fam + '/', '/' + f + '/') for f in families]
        for key in keys:
            g = built.get(key)
            if g is None:
                continue
            Pw, N, S = world(g, p['place'])
            # sample the faces: corners and centroids
            starts = np.zeros(len(S), dtype=np.int64)
            starts[1:] = np.cumsum(S[:-1])
            cent = np.add.reduceat(Pw, starts, axis=0) / S[:, None]
            nrm = N[starts]
            pts = np.concatenate([Pw, cent])
            flat = np.concatenate([np.repeat(np.abs(nrm[:, 1]) > .95, S), np.abs(nrm[:, 1]) > .95])
            if key == p['key']:
                samples.append(dense(Pw, S))
            lo, hi = pts.min(axis=0) - 4, pts.max(axis=0) + 4
            near = np.nonzero((B[:, 0] < hi[0]) & (B[:, 1] > lo[0]) & (B[:, 4] < hi[2]) & (B[:, 5] > lo[2]))[0]
            if len(near) == 0:
                continue
            Bn = B[near]
            for c0 in range(0, len(pts), 20000):
                q = pts[c0:c0 + 20000]
                fl = flat[c0:c0 + 20000]
                inside_xz = (q[:, None, 0] >= Bn[None, :, 0] - 1e-3) & (q[:, None, 0] <= Bn[None, :, 1] + 1e-3) & \
                            (q[:, None, 2] >= Bn[None, :, 4] - 1e-3) & (q[:, None, 2] <= Bn[None, :, 5] + 1e-3)
                below = Bn[None, :, 3] <= q[:, None, 1] + .05
                tops = np.where(inside_xz & below, Bn[None, :, 3], -1e9)
                floor = tops.max(axis=1)
                h = q[:, 1] - floor
                dx = np.maximum(np.maximum(Bn[None, :, 0] - q[:, None, 0], 0), q[:, None, 0] - Bn[None, :, 1])
                dy = np.maximum(np.maximum(Bn[None, :, 2] - q[:, None, 1], 0), q[:, None, 1] - Bn[None, :, 3])
                dz = np.maximum(np.maximum(Bn[None, :, 4] - q[:, None, 2], 0), q[:, None, 2] - Bn[None, :, 5])
                dist = np.sqrt(dx * dx + dy * dy + dz * dz).min(axis=1)
                bad = (h < 3.08) & (h > -50) & (dist > .25) & ~((h <= .06) & fl) & (q[:, 1] < ceiling)
                idx = np.nonzero(bad)[0]
                if len(idx):
                    found['count'][key] = found['count'].get(key, 0) + int(len(idx))
                    i = idx[np.argmax(dist[idx])]
                    found['free'].append((key, [round(float(x), 2) for x in q[i]], round(float(h[i]), 2), round(float(dist[i]), 2)))
    # the replaced blocks' exposed faces: art within 0.25 m (at 1 m samples)
    if samples:
        import mathutils
        allp = np.concatenate(samples)
        tree = mathutils.kdtree.KDTree(len(allp))
        for i, q in enumerate(allp):
            tree.insert(q, i)
        tree.balance()
        for bid in L['covers']:
            k = ids.index(bid)
            x0, x1, y0, y1, z0, z1 = B[k]
            for (ax, sgn) in ((0, -1), (0, 1), (1, 1), (2, -1), (2, 1)):
                u_ax, v_ax = [a for a in range(3) if a != ax]
                ulo, uhi = (x0, x1) if u_ax == 0 else ((y0, y1) if u_ax == 1 else (z0, z1))
                vlo, vhi = (x0, x1) if v_ax == 0 else ((y0, y1) if v_ax == 1 else (z0, z1))
                plane = (x1 if sgn > 0 else x0) if ax == 0 else ((y1 if sgn > 0 else y0) if ax == 1 else (z1 if sgn > 0 else z0))
                for u in np.arange(ulo + .5, uhi, 1.0):
                    for v in np.arange(vlo + .5, vhi, 1.0):
                        q = [0.0, 0.0, 0.0]
                        q[ax], q[u_ax], q[v_ax] = plane + sgn * .05, u, v
                        # exposed: not inside another block
                        inside = (B[:, 0] < q[0]) & (B[:, 1] > q[0]) & (B[:, 2] < q[1]) & (B[:, 3] > q[1]) & (B[:, 4] < q[2]) & (B[:, 5] > q[2])
                        if inside.any():
                            continue
                        co, idx, d = tree.find(q)
                        if d is None or d > .55:   # the art's samples lie 0.4 m apart: 0.55 m to the nearest is about 0.25 m to its surface
                            found['bare'].append((bid, [round(x, 2) for x in q], round(d or 99, 2)))
    return found


EYES = [(0, 1.65, 24), (24, 1.65, 0), (0, 1.65, -24), (-24, 1.65, 0), (0, 1.65, 0)]


def dense(Pw, S, step=.4):
    """Points over every face of a placed mesh, at most `step` apart (each polygon fanned into triangles)."""
    out = [Pw]
    starts = np.zeros(len(S), dtype=np.int64)
    starts[1:] = np.cumsum(S[:-1])
    for f in range(len(S)):
        a = Pw[starts[f]]
        for k in range(1, S[f] - 1):
            b, c = Pw[starts[f] + k], Pw[starts[f] + k + 1]
            n = int(max(np.linalg.norm(b - a), np.linalg.norm(c - a)) / step)
            if n < 1:
                continue
            u, v = np.meshgrid(np.arange(n + 1), np.arange(n + 1))
            m = (u + v) <= n
            u, v = u[m] / n, v[m] / n
            out.append(a + np.outer(u, b - a) + np.outer(v, c - a))
    return np.concatenate(out)


def view_triangles(L, stats, built, low=False):
    """Triangles of the landmarks within each camp eye's reach (no culling: an upper bound), by the runtime's tier rule."""
    out = []
    for e in EYES:
        tot = 0
        for p in L['pieces']:
            key = p['key']
            if p['kind'] in ('Front', 'FrontRig'):
                key = key.replace('/' + key.split('/')[1] + '/', '/station/')
            s = stats.get(key)
            if not s:
                continue
            g = built.get(p['key']) or built.get(key)
            if g is None:
                continue
            P = np.frombuffer(g.p, dtype=np.float32).reshape(-1, 3)
            A = np.array(p['place'], dtype=np.float64).reshape(3, 4)
            lo, hi = P.min(axis=0), P.max(axis=0)
            corners = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])], dtype=np.float64) @ A[:, :3].T + A[:, 3]
            blo, bhi = corners.min(axis=0), corners.max(axis=0)
            d = float(np.linalg.norm(np.maximum(np.maximum(blo - np.array(e), 0), np.array(e) - bhi)))
            far = d > 101 or low
            t = 'far' if (far and 'far' in s) else ('2' if (d > 38 or low) else '0')
            tot += s.get(t, s.get('2', s.get('0', 0)))
        out.append(tot)
    return out


def _render(geo_bakes, views, out_dir, size=(960, 540)):
    """stagekit.preview with a far clip for the far layer (scratch stills only)."""
    """geo_bakes: [(Geo, Bake)]; views: [(name, eye, target, fov)] in Unity space. Writes out_dir/<name>.png."""
    import bpy
    from mathutils import Matrix, Vector
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for i, (g, bk) in enumerate(geo_bakes):
        P, N, S, F = g.arrays()
        me = bpy.data.meshes.new('pv%d' % i)
        n = len(P)
        me.vertices.add(n)
        Pm = P.astype(np.float32) * np.array([-1, 1, 1], np.float32)
        me.vertices.foreach_set('co', Pm.ravel())
        me.loops.add(n)
        me.loops.foreach_set('vertex_index', np.arange(n, dtype=np.int32))
        me.polygons.add(len(S))
        starts = np.zeros(len(S), dtype=np.int32)
        if len(S) > 1:
            starts[1:] = np.cumsum(S[:-1])
        me.polygons.foreach_set('loop_start', starts)
        me.update()
        attr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
        c = bk.colour.astype(np.float32) / 255.0
        lin = np.where(c[:, :3] <= .04045, c[:, :3] / 12.92, ((c[:, :3] + .055) / 1.055) ** 2.4)
        rgb = lin * (.35 + .65 * c[:, 3:4])
        rgba = np.concatenate([rgb, np.ones((n, 1), np.float32)], axis=1)
        attr.data.foreach_set('color', rgba.ravel())
        me.color_attributes.active_color = attr
        ob = bpy.data.objects.new('pv%d' % i, me)
        bpy.context.scene.collection.objects.link(ob)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'VERTEX'
    sc.display.shading.show_cavity = True
    sc.display.shading.cavity_type = 'WORLD'
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.view_settings.view_transform = 'Standard'
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(out_dir, exist_ok=True)
    for name, eye, target, fov in views:
        e, t = Vector((-eye[0], eye[1], eye[2])), Vector((-target[0], target[1], target[2]))
        fwd = (t - e).normalized()
        up = Vector((0, 1, 0))
        right = fwd.cross(up).normalized()
        upv = right.cross(fwd)
        m = Matrix(((right.x, upv.x, -fwd.x), (right.y, upv.y, -fwd.y), (right.z, upv.z, -fwd.z)))
        cam.matrix_world = Matrix.Translation(e) @ m.to_4x4()
        cam.data.angle = math.radians(fov)
        cam.data.clip_start, cam.data.clip_end = .1, 1500
        sc.render.filepath = os.path.join(out_dir, name + '.png')
        bpy.ops.render.render(write_still=True)


def preview(D, built, out_dir, seeds=None):
    """Workbench stills of each layout's landmarks among its greybox blocks, from each gate's eye and a few closer views."""
    import bpy
    for L in D['layouts']:
        if seeds and L['seed'] not in seeds:
            continue
        geo = []
        grey = sk.Geo()
        f_grey = sk.fin('lm-preview-grey', 0x8A8C90)
        f_cover = sk.fin('lm-preview-cover', 0xB07070)
        covers = set(L['covers'])
        for b in L['blocks']:
            x0, x1, y0, y1, z0, z1 = b[1:7]
            if b[0] in covers or abs(x0) > 2000:
                continue
            sk.box(grey, x0, x1, y0, y1, z0, z1, f_grey, None, 0.0, cell=0)
        geo.append((grey, sk.Bake(grey, ao=None, floors=(0.0,))))
        for p in L['pieces']:
            g = built.get(p['key'])
            if p['kind'] in ('Front', 'FrontRig'):
                g = built.get(p['key'])
            if g is None:
                continue
            if p['kind'] in ('Far', 'Mast'):
                g = built.get(p['key'] + '#far', g)
            gw = g.transformed(p['place']) if any(abs(p['place'][i] - (1 if i in (0, 5, 10) else 0)) > 1e-6 for i in range(12)) else g
            geo.append((gw, sk.Bake(gw, ao=None, floors=lk.floors_of(gw))))
        views = []
        for k, e in enumerate(EYES[:4]):
            d = ((0, 0, 1), (1, 0, 0), (0, 0, -1), (-1, 0, 0))[k]
            views.append(('%d-%s-gate' % (L['seed'], 'NESW'[k]), e, (e[0] + d[0] * 40, e[1] + 4, e[2] + d[2] * 40), 62))
        for p in L['pieces']:
            if p['kind'] == 'Exit':
                for j, r in enumerate(p['reads'][:1]):
                    e = EYES[p['gate']]
                    v = (r[0] - e[0], r[1] - e[1], r[2] - e[2])
                    L2 = math.sqrt(sum(x * x for x in v))
                    eye = (r[0] - v[0] / L2 * 14, max(r[1] - v[1] / L2 * 14, 1.0), r[2] - v[2] / L2 * 14)
                    views.append(('%d-%s-close' % (L['seed'], p['key'].split('/')[-1]), eye, (r[0], r[1], r[2]), 50))
        for p in L['pieces']:
            m = p['place']
            if p['kind'] == 'Court':
                g0 = (m[3], m[7], m[11])
                views.append(('%d-court-gateway' % L['seed'], (g0[0] + 6, g0[1] + 1.7, g0[2] - 26), (g0[0], g0[1] + 14, g0[2]), 60))
                views.append(('%d-court-hall' % L['seed'], (g0[0] - 10, g0[1] + 1.7, g0[2] + 12), (g0[0], g0[1] + 22, g0[2] + 44), 70))
            elif p['kind'] in ('Far', 'Mast') and p['reads']:
                r = p['reads'][0]
                views.append(('%d-%s-from-camp' % (L['seed'], p['key'].replace('/', '-')), (0, 1.65, 0), (r[0], r[1] - 20, r[2]), 30))
        for h in L['halls']:
            d, o = h['door'], h['out']
            views.append(('%d-%s-front' % (L['seed'], h['id']), (d[0] + o[0] * 26 + h['across'][0] * 6, d[1] + 1.7, d[2] + o[2] * 26 + h['across'][2] * 6), (d[0], d[1] + 7, d[2]), 62))
        _render(geo, views, out_dir, size=(1280, 720))


def main():
    D = json.load(open(ARGS['layouts']))
    only = ARGS['only'].split(',') if ARGS['only'] else None
    sb, stats, built = build_all(D, only)
    out = ARGS['out']
    path = sb.write(out, BANK)
    report = {'bank': path, 'bytes': os.path.getsize(path), 'stats': stats, 'layouts': {}}
    if ARGS['check'] and not only:
        for L in D['layouts']:
            t = time.time()
            f = check_layout(L, built, lf.FAMILIES)
            report['layouts'][L['name']] = {'free': f['free'][:200], 'free_count': sum(f['count'].values()), 'free_by_piece': f['count'], 'bare': f['bare'][:60], 'bare_count': len(f['bare']),
                                            'view_high': view_triangles(L, stats, built), 'view_low': view_triangles(L, stats, built, True)}
            print('%-16s free %4d  bare %4d  view tris (High) %s (Low) %s  %.1f s' % (L['name'], sum(f['count'].values()), len(f['bare']),
                  report['layouts'][L['name']]['view_high'], report['layouts'][L['name']]['view_low'], time.time() - t))
    if ARGS.get('preview'):
        preview(D, built, ARGS['preview'], [int(x) for x in ARGS['seeds'].split(',')] if ARGS.get('seeds') else None)
    rp = ARGS['report'] or os.path.join(out, 'landmarks-report.json')
    with open(rp, 'w') as fh:
        json.dump(report, fh, indent=1)
    print('wrote', rp)


if __name__ == '__main__':
    main()
