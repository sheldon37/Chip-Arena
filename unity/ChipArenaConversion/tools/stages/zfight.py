"""Depth-fighting candidates of a stage's art (docs/arenas/STAGE-ART.md "Depth fighting"): faces that lie in one plane, turned
the same way, and overlap, among everything the stage draws at once:
  static   the bank's static chunks at one tier (world space);
  block    the bank's block models where their map blocks stand;
  rig      the bank's rig models where the stage's art pose puts them at one moment (--rest: CoreTests --stage-art-rest);
  kit      the kit boxes the stage keeps drawing over its art (--rest);
  dress    the dressed boxes of map blocks the art does not cover.
Plain Python with numpy (no Blender):
  python -I zfight.py BANK UNITYMAPS MAPID [--rest REST.json] [--tier 0] [--eps 0.004] [--amin 0.0002] [--top 40] [--out report.txt]
                      [--floors] [--with-tier N]
--floors measures only the faces turned up (floors, and what lies on them), out to 30 mm apart, with the gaps' histogram: across
a floor at a pilot's eye height the depth between two layers is their gap times the sine of the view's angle to the floor (1.7 m
up and 30 m off: a twentieth), so layers millimetres apart still fight there. --with-tier N adds the static chunks of tier N
(two neighbouring chunks may draw at different tiers; a chunk never meets itself at another tier).
A pair counts when its planes are within --eps (m) and its overlap is over --amin (m2). `exact` is the part closer than 0.5 mm
(it fights at any distance); the rest fights only where the depth buffer cannot part it. Faces turned opposite ways do not count
(the back one is culled). Faces on different layers do not count either (stagekit.layer_faces; ChipArena/StageLit draws a layered
face nearer the eye along its ray: layer / 32 rides the vertex's fine-surface kind, a block's or a rig's model draws PROP_LIFT layers up,
a kept kit box the layers the rest export gives it): their area is reported as `parted by layer`."""
import json, math, struct, sys, collections
import numpy as np

EXACT = .0005
PROP_LIFT = 2   # Core NativeStageArt.ModelLayers: _StageLift on the blocks' and the rig's materials

def read_bank(path):
    b = open(path, 'rb').read(); o = [0]
    def take(n):
        s = b[o[0]:o[0] + n]; o[0] += n; return s
    def i32(): return struct.unpack_from('<i', take(4))[0]
    def u8(): return take(1)[0]
    def f32s(n): return np.frombuffer(take(4 * n), dtype='<f4')
    def s(): n = i32(); return take(n).decode('utf-8')
    assert take(4) == b'CAPB' and i32() == 1
    for _ in range(i32()): s(); u8()
    for _ in range(i32()): s(); i32(); i32(); f32s(4); f32s(3); u8()
    meshes = []
    for _ in range(i32()):
        name = s(); f32s(3); f32s(3); n = i32(); attr = u8()
        pos = f32s(3 * n).reshape(-1, 3).astype(np.float64)
        if attr & 1: take(12 * n)
        if attr & 2: take(8 * n)
        lay = np.zeros(n)
        if attr & 4:
            k = f32s(2 * n).reshape(-1, 2)[:, 0].astype(np.float64); lay = np.floor((k - np.floor(k + 1 / 64)) * 32 + .5)
        if attr & 8: take(4 * n)
        wide = n > 65535; subs = []
        for _ in range(i32()):
            i32(); c = i32()
            subs.append(np.frombuffer(take(c * (4 if wide else 2)), dtype='<u4' if wide else '<u2').astype(np.int64).reshape(-1, 3))
        meshes.append((name, pos, subs, lay))
    props = []
    for _ in range(i32()):
        name = s(); group = s(); f32s(3); f32s(3); parts = []
        for _ in range(i32()):
            m = i32(); parts.append((m, f32s(12).astype(np.float64).reshape(3, 4)))
        props.append((name, group, parts))
    assert o[0] == len(b)
    return meshes, props

def prop_tris(meshes, prop, place=None, lift=0):
    """The triangles (n, 3, 3) of a prop, through `place` (3 x 4) when given, and their layers (+ lift)."""
    out = []; lay = []
    for m, a in prop[2]:
        pos = meshes[m][1]; w = pos @ a[:, :3].T + a[:, 3]
        if place is not None: w = w @ place[:, :3].T + place[:, 3]
        for idx in meshes[m][2]:
            if len(idx): out.append(w[idx]); lay.append(meshes[m][3][idx[:, 0]] + lift)
    return (np.concatenate(out), np.concatenate(lay)) if out else (np.zeros((0, 3, 3)), np.zeros(0))

def quat_matrix(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])

BOX_FACES = [((0, 1), 2, 1), ((0, 1), 2, -1), ((1, 2), 0, 1), ((1, 2), 0, -1), ((2, 0), 1, 1), ((2, 0), 1, -1)]
def box_tris(c, h, q):
    """The twelve triangles of a box (centre, half extents, quaternion), wound outward."""
    r = quat_matrix(q); c = np.asarray(c, float); out = []
    for (u, v), n, s in BOX_FACES:
        def P(a, b):
            p = np.zeros(3); p[u] = a * h[u]; p[v] = b * h[v]; p[n] = s * h[n]; return c + r @ p
        quad = [P(-1, -1), P(1, -1), P(1, 1), P(-1, 1)]
        if s < 0: quad = quad[::-1]
        out.append([quad[0], quad[1], quad[2]]); out.append([quad[0], quad[2], quad[3]])
    return np.array(out)

def rot_of(b):
    r = b.get('rotation') or b.get('rot')
    if not r: return (0, 0, 0, 1)
    if isinstance(r, dict): return (r.get('x', 0), r.get('y', 0), r.get('z', 0), r.get('w', 1))
    return tuple(r)
def vec_of(v): return (v['x'], v['y'], v['z']) if isinstance(v, dict) else tuple(v)

def gather(meshes, props, m, rest, tier, other=None):
    """[(kind, name, triangles)] of everything drawn together at `tier`."""
    out = []; by_name = {p[0]: p for p in props}
    def tiered(stem):
        for t in (tier, 1, 0, 2):
            p = by_name.get('%s/%d' % (stem, t))
            if p is not None and p[2]: return p
        return None
    for p in props:
        if p[1] == 'static' and p[0].endswith('/%d' % tier): out.append(('static', p[0]) + prop_tris(meshes, p))
        if other is not None and other != tier and p[1] == 'static' and p[0].endswith('/%d' % other): out.append(('static', p[0]) + prop_tris(meshes, p))
    covers = {p[0][6:] for p in props if p[1] == 'cover'}
    for b in m['blocks']:
        p = tiered('block/' + b['id'])
        if p is not None:
            place = np.zeros((3, 4)); place[:, :3] = quat_matrix(rot_of(b)); place[:, 3] = vec_of(b['pos'])
            out.append(('block', b['id']) + prop_tris(meshes, p, place, PROP_LIFT))
        if b['id'] not in covers:
            out.append(('dress', b['id'], box_tris(vec_of(b['pos']), [x / 2 for x in vec_of(b['size'])], rot_of(b)), np.zeros(12)))
    if rest:
        for i, r in enumerate(rest.get('rig', ())):
            p = tiered('rig/' + r['slot'])
            if p is not None: out.append(('rig', '%s#%d' % (r['slot'], i)) + prop_tris(meshes, p, np.array(r['place'], float).reshape(3, 4), PROP_LIFT))
        for i, k in enumerate(rest.get('boxes', ())):
            out.append(('kit', '%s#%d' % (k['kind'], i), box_tris(k['c'], k['h'], k['q']), np.full(12, float(k.get('layer', 0)))))
    return out, covers

def clip(subject, clipper):
    """Sutherland-Hodgman polygon clip (convex clipper, any winding)."""
    def area2(poly): return sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
    if area2(clipper) < 0: clipper = clipper[::-1]
    out = subject
    for i in range(len(clipper)):
        a, b = clipper[i], clipper[(i + 1) % len(clipper)]; inp = out; out = []
        if not inp: break
        def inside(p): return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= -1e-12
        def inter(p, q):
            x1, y1, x2, y2 = p[0], p[1], q[0], q[1]; x3, y3, x4, y4 = a[0], a[1], b[0], b[1]
            d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
            if abs(d) < 1e-15: return q
            t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d; return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))
        for j in range(len(inp)):
            p, q = inp[j], inp[(j + 1) % len(inp)]
            if inside(q):
                if not inside(p): out.append(inter(p, q))
                out.append(q)
            elif inside(p): out.append(inter(p, q))
    return out

def overlap(t1, t2, ax):
    k = [i for i in range(3) if i != ax]
    p = clip([(q[k[0]], q[k[1]]) for q in t1], [(q[k[0]], q[k[1]]) for q in t2])
    if len(p) < 3: return 0.0, None
    a = abs(sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p)))) / 2
    return a, p

def measure(sources, eps=.004, amin=2e-4, cell=2.0, skip=None):
    """Pairs of coplanar, same-facing, overlapping triangles: [(source a, source b, area in the plane, gap, where)]."""
    T = np.concatenate([s[2] for s in sources]); src = np.concatenate([np.full(len(s[2]), i) for i, s in enumerate(sources)]); lay = np.concatenate([s[3] for s in sources])
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); l = np.linalg.norm(n, axis=1); ok = l > 2e-5
    T, src, n, l, lay = T[ok], src[ok], n[ok], l[ok], lay[ok]; n = n / l[:, None]; layL = lay.tolist()
    # one sign per plane, so both facings share a bucket
    first = np.argmax(np.abs(n) > 1e-6, axis=1); sg = np.sign(n[np.arange(len(n)), first]); nn = n * sg[:, None]
    d = np.einsum('ij,ij->i', nn, T[:, 0]); ax = np.argmax(np.abs(n), axis=1)
    lo = np.floor(T.min(axis=1) / cell).astype(int); hi = np.floor(T.max(axis=1) / cell).astype(int)
    nk = np.round(nn * 200).astype(int); dk = np.floor(d / eps).astype(int)
    planes = collections.defaultdict(lambda: collections.defaultdict(list))
    TL = T.tolist(); nL = n.tolist(); dL = d.tolist(); axL = ax.tolist(); srcL = src.tolist()
    span = (hi - lo + 1).prod(axis=1)
    for i in range(len(T)):
        g = planes[(nk[i, 0], nk[i, 1], nk[i, 2], dk[i])]
        if span[i] > 400: g['big'].append(i); continue     # a far sheet: met with every face of its plane instead
        for cx in range(lo[i, 0], hi[i, 0] + 1):
            for cy in range(lo[i, 1], hi[i, 1] + 1):
                for cz in range(lo[i, 2], hi[i, 2] + 1): g[(cx, cy, cz)].append(i)
    pairs = []; seen = set(); parted = [0.0]
    for pk, grid in planes.items():
        for dd in (0, 1):
            other = planes.get((pk[0], pk[1], pk[2], pk[3] + dd))
            if not other: continue
            everything = None
            for c, fs in grid.items():
                if c == 'big':
                    if everything is None: everything = sorted({j for v in other.values() for j in v})
                    near = everything
                else:
                    near = other.get(c)
                    if other.get('big'): near = (near or []) + other['big']
                if not near: continue
                for i in fs:
                    for j in near:
                        if j == i: continue
                        pr = (i, j) if i < j else (j, i)
                        if pr in seen: continue
                        seen.add(pr)
                        gap = abs(dL[i] - dL[j])
                        if gap > eps: continue
                        if skip is not None and skip(srcL[i], srcL[j]): continue
                        a, b = nL[i], nL[j]
                        if a[0] * b[0] + a[1] * b[1] + a[2] * b[2] < .9995: continue
                        ar, poly = overlap(TL[i], TL[j], axL[i])
                        if ar <= 0: continue
                        ar /= max(abs(a[axL[i]]), 1e-6)       # the area in the plane, not in the projection
                        if ar > amin and layL[i] != layL[j]: parted[0] += ar
                        elif ar > amin:
                            t = TL[i]; pairs.append((srcL[pr[0]], srcL[pr[1]], ar, gap, tuple(round(sum(p[k] for p in t) / 3, 2) for k in range(3))))
    return pairs, parted[0]

def report(sources, pairs, top=40, say=print, hist=False):
    cls = collections.defaultdict(lambda: [0, 0.0, 0.0])
    by = collections.defaultdict(lambda: [0, 0.0, 0.0, None, 0.0]); spots = collections.defaultdict(lambda: [0.0, 0.0, None, collections.Counter()])
    for a, b, ar, gap, at in pairs:
        ka, kb = sources[a][0], sources[b][0]; k = '|'.join(sorted((ka, kb))); same = a == b
        if same: k = ka + ' (itself)'
        c = cls[k]; c[0] += 1; c[1 if gap < EXACT else 2] += ar
        e = by[(a, b)]; e[0] += 1; e[1 if gap < EXACT else 2] += ar
        if e[3] is None or ar > e[4]: e[3] = at; e[4] = ar
        s = spots[tuple(int(math.floor(v / 2)) for v in at)]; s[0 if gap < EXACT else 1] += ar; s[3][k] += ar
        if s[2] is None: s[2] = at
    if hist:
        edges = (.0005, .003, .006, .012, .02, .03, 1e9); names = ('< 0.5 mm', '0.5-3', '3-6', '6-12', '12-20', '20-30', 'more'); h = [0.0] * len(edges)
        for a, b, ar, gap, at in pairs: h[next(k for k, e in enumerate(edges) if gap < e)] += ar
        say('gaps (m2): ' + ', '.join('%s: %.2f' % (n, v) for n, v in zip(names, h) if v > 0))
    say('by kind (pairs, exact m2 [< 0.5 mm], near m2):')
    for k, c in sorted(cls.items(), key=lambda x: -(x[1][1] + x[1][2])): say('  %-26s %6d %9.3f %9.3f' % (k, c[0], c[1], c[2]))
    ex = sum(c[1] for c in cls.values()); nr = sum(c[2] for c in cls.values())
    say('total: pairs %d, exact %.3f m2, near %.3f m2' % (len(pairs), ex, nr))
    say('worst pairs of pieces (a | b: triangles, exact m2, near m2, at):')
    for (a, b), e in sorted(by.items(), key=lambda x: -(x[1][1] + x[1][2]))[:top]:
        say('  %-40s | %-40s %5d %8.3f %8.3f at %s' % (sources[a][0] + ' ' + sources[a][1], sources[b][0] + ' ' + sources[b][1], e[0], e[1], e[2], e[3]))
    say('worst places (2 m cells: exact m2, near m2, at, kinds):')
    for c, s in sorted(spots.items(), key=lambda x: -(x[1][0] + x[1][1]))[:top]:
        say('  %8.3f %8.3f at %-24s %s' % (s[0], s[1], s[2], ', '.join('%s %.2f' % kv for kv in s[3].most_common(3))))
    return ex, nr

def main():
    a = sys.argv[1:]; bank_path, maps_path, map_id = a[0], a[1], a[2]
    def opt(name, default, kind=float): return kind(a[a.index(name) + 1]) if name in a else default
    tier = opt('--tier', 0, int); eps = opt('--eps', .004); amin = opt('--amin', 2e-4); top = opt('--top', 40, int)
    out_path = opt('--out', None, str); rest_path = opt('--rest', None, str); floors = '--floors' in a; other = opt('--with-tier', None, int)
    if floors and '--eps' not in a: eps = .03
    meshes, props = read_bank(bank_path)
    m = [x for x in json.load(open(maps_path, encoding='utf-8'))['maps'] if x['id'] == map_id][0]
    rest = json.load(open(rest_path, encoding='utf-8')) if rest_path else None
    sources, covers = gather(meshes, props, m, rest, tier, other)
    if floors:
        def up(t):
            n = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0]); l = np.linalg.norm(n, axis=1)
            return t[(l > 1e-9) & (n[:, 1] > .97 * np.maximum(l, 1e-12))]
        def keep(t):
            n = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0]); l = np.linalg.norm(n, axis=1)
            return (l > 1e-9) & (n[:, 1] > .97 * np.maximum(l, 1e-12))
        sources = [(k, nm, t[keep(t)], ly[keep(t)]) for k, nm, t, ly in sources]; sources = [x for x in sources if len(x[2])]
    def chunk(i): return sources[i][1].rsplit('/', 1)[0] if sources[i][0] == 'static' else None
    def skip(i, j): return i != j and chunk(i) is not None and chunk(i) == chunk(j)
    lines = []
    def say(*v): lines.append(' '.join(str(x) for x in v))
    kinds = collections.Counter(); tris = collections.Counter()
    for k, _, t, _ly in sources: kinds[k] += 1; tris[k] += len(t)
    say(map_id, 'floors' if floors else 'all faces', 'tier', tier, 'with tier %d' % other if other is not None else '', 'eps', eps, 'amin', amin, '|', ', '.join('%s %d (%d triangles)' % (k, kinds[k], tris[k]) for k in kinds), '| covers', len(covers), 'of', len(m['blocks']), 'blocks')
    pairs, parted = measure(sources, eps, amin, skip=skip)
    say('parted by layer: %.2f m2 (not counted below)' % parted)
    report(sources, pairs, top, say, hist=floors)
    text = '\n'.join(lines); print(text)
    if out_path: open(out_path, 'w', encoding='utf-8').write(text + '\n')

if __name__ == '__main__': main()
