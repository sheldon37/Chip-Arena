# Breakwater M1 (the pier head at blue hour, 40x26 m of flat play, the 1v1 duel map) draft painter and checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z); ids swap n/s, e/w, a/b.
# usage: python3 docs/arenas/scripts/breakwater_m1.py [--out DIR] [--json PATH]   (stdout: plan + checks; --json writes the draft only;
#        --out writes breakwater_m1.json, draft map data in the UnityMaps.json shape, and breakwater_m1.png, a top-down plan, 12 px per metre)
# A draft for Astra (docs/arenas/NEW-STAGES.md section 1), NOT wired into the game. Flat: every walkable surface is y = 0 except the
# 1.0-1.1 m barriers and single crates; everything taller carries a pilot block, so even an air jump never finds high ground.
# Ids carry the prefixes the presentation keys its kit dressing on: -container- (Toon Harbor 20 ft), -crate- (crates, breakable),
# -barrier- (concrete jersey barriers), -drums- (oil drums), -booth- (the harbourmaster's booths), -winch- (winch houses), -parapet- (the pier wall).
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -21.2, 21.2, -14.2, 14.2     # slab: the pier head; play bounds x +-20, z +-13 inside the 1.2 m parapet
BOUNDS = {'minX': -20, 'maxX': 20, 'minZ': -13, 'maxZ': 13, 'ceiling': 16, 'killY': -6}
CW, CL, CH = 2.44, 6.06, 2.59                  # Toon Harbor 20 ft container: width, length, height
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp))
def crates(i, x0, z0, nx, nz, layers):
    # a stack of 1.2 m crates (breakable wood, 34 hp each, as the Factory's), nx by nz, `layers` high; ids <i>-<layer><k>
    for l in range(layers):
        for a in range(nx):
            for b in range(nz):
                add('%s-%d%d' % (i, l, a * nz + b), x0 + 1.2 * a, x0 + 1.2 * (a + 1), 1.2 * l, 1.2 * (l + 1), z0 + 1.2 * b, z0 + 1.2 * (b + 1), 'x', mat='wood', role='cover', hp=34)

# ---- the pier wall: a 1.2 m stone parapet all round (pilot blocks above it); the sea, tetrapods and the lighthouse are kit outside
add('breakwater-parapet-n', X0, X1, 0, 1.2, 13.0, Z1, '#')
add('breakwater-parapet-w', X0, -20.0, 0, 1.2, -13.0, 13.0, '#')
FLOOR = dict(id='breakwater-floor', mat='foundation', role='static', box=(X0, X1, -.5, 0, Z0, Z1), ch='.', hp=0)
# ---- the spine: two 20 ft containers staggered across the spawn line (the "chicane", a 2.4 m slot between them), and each side's
#      spawn screen 2 m in front of the spawn (the "spawn box"; exits north and south)
add('breakwater-container-mid-w', -1.2 - CW, -1.2, 0, CH, -2.0, -2.0 + CL, 'K', mat='metal')
add('breakwater-container-a', -15.5, -15.5 + CW, 0, CH, -CL / 2, CL / 2, 'K', mat='metal')
# ---- the harbourmaster's booths on the long sides (roofed, 3 m; open toward the pier wall), with a 2.4 m lane behind them
add('breakwater-booth-n-back', -3.0, 3.0, 0, 3.0, 6.6, 7.0, 'B')
add('breakwater-booth-n-w', -3.0, -2.6, 0, 3.0, 7.0, 10.6, 'B')
add('breakwater-booth-n-e', 2.6, 3.0, 0, 3.0, 7.0, 10.6, 'B')
add('breakwater-booth-n-roof', -3.2, 3.2, 3.0, 3.25, 6.4, 10.6, 'B', mat='metal')
# ---- A's north (the container corner, close quarters): the corner container flush to the parapet, the anchor stack, a barrier line
add('breakwater-container-corner-n', -20.0, -20.0 + CL, 0, CH, 13.0 - CW, 13.0, 'K', mat='metal')
crates('breakwater-crate-stack-n', -10.2, 4.6, 2, 3, 2)
add('breakwater-barrier-lane-n', -13.6, -12.8, 0, 1.1, 7.0, 10.2, '=')
# ---- A's mid stack in the spine band, off the duel line
crates('breakwater-crate-spine-a', -8.6, 0.4, 1, 2, 2)
# ---- A's south (the open apron, long duels over half cover): the lane stack flush to the parapet, the winch house, barrier pairs
crates('breakwater-crate-lane-s', -9.6, -13.0, 2, 2, 2)
add('breakwater-winch-s', -9.8, -8.0, 0, 2.6, -6.6, -4.8, 'W', mat='metal')
add('breakwater-barrier-apron-s-0', -6.4, -3.2, 0, 1.1, -8.4, -7.6, '=')
add('breakwater-barrier-apron-s-1', -16.8, -16.0, 0, 1.1, -11.4, -8.2, '=')
add('breakwater-drums-s', -14.4, -13.0, 0, 1.0, -7.4, -6.0, 'o', mat='metal', role='cover', hp=150)   # oil drums at the spawn's south exit

# ---- spawns (yaw from +z toward +x; the duel points face the far end; the FFA booth points face the booth mouth)
teamA = [(-17.5, 0.0), (-18.9, 1.5), (-18.9, -1.5), (-16.6, -1.6)]
teamB = [(-x, -z) for x, z in teamA]
ffa_names = ['A1', 'B1', 'f', 'F']
ffa = [teamA[0], teamB[0], (0.0, 9.0), (0.0, -9.0)]
YAW = {(0.0, 9.0): 0.0, (0.0, -9.0): 180.0}
def yaw(p): return YAW.get(p, (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360)

# ---- pilot-only blocking columns above everything taller than the barriers (flat map: no high ground, even with an air jump)
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
PILOT = [dict(id='breakwater-top-parapet-n', center=v(0, 0, 13.6), size=v(42.4, 0, 1.2), bottom=1.15),
         dict(id='breakwater-top-parapet-s', center=v(0, 0, -13.6), size=v(42.4, 0, 1.2), bottom=1.15),
         dict(id='breakwater-top-parapet-w', center=v(-20.6, 0, 0), size=v(1.2, 0, 26), bottom=1.15),
         dict(id='breakwater-top-parapet-e', center=v(20.6, 0, 0), size=v(1.2, 0, 26), bottom=1.15)]
def under(e):                                                        # something sits on top of it (the roof over a booth wall, a crate on a crate)
    x0, x1, y0, y1, z0, z1 = e['box']
    return any(o is not e and abs(o['box'][2] - y1) < .01 and o['box'][0] <= x0 + .01 and x1 <= o['box'][1] + .01 and o['box'][4] <= z0 + .01 and z1 <= o['box'][5] + .01 for o in els)
for e in els:
    x0, x1, y0, y1, z0, z1 = e['box']
    if e['id'].startswith('breakwater-parapet') or y1 <= 1.25 or under(e): continue
    PILOT.append(dict(id=e['id'].replace('breakwater-', 'breakwater-top-', 1), center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)),
                      size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .05, 3)))
PILOT = [p for p in PILOT if p['bottom'] > 1.2]                       # single crates (1.2 m) stay standable, like the barriers
HAZ = []

def paint():
    W = int(round((X1 - X0) * 2)); R = int(round(Z1 - Z0)); g = [['.'] * W for _ in range(R)]
    def put(x, z, ch):
        c = int(math.floor((x - X0) * 2)); r = int(math.floor(Z1 - z))
        if 0 <= c < W and 0 <= r < R: g[r][c] = ch
    def rect(x0, x1, z0, z1, ch):
        zz = z0
        while zz < z1 - 1e-9:
            xx = x0
            while xx < x1 - 1e-9: put(xx + .1, zz + .1, ch); xx += .25
            zz += .25
    for e in sorted(els, key=lambda e: e['box'][3]):
        if e['id'].endswith('-roof'): continue
        b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
    for i, (x, z) in enumerate(teamA): put(x, z, str(i + 1)); put(-x, -z, str(i + 5))
    for nm, (x, z) in zip(ffa_names[2:], ffa[2:]): put(x, z, nm)
    out = ['         x=-21' + ' ' * 36 + 'x=0' + ' ' * 37 + 'x=+21   (1 char = 0.5 m x 1 m)']
    for r in range(R): z = Z1 - r; out.append(f'{z-1:+6.1f}..{z:+5.1f} ' + ''.join(g[r]))
    return '\n'.join(out)

def seg_hits(a, b, e, eye):
    x0, x1, y0, y1, z0, z1 = e['box']
    if not (y0 < eye < y1) or y1 < 2.0: return False
    t0, t1 = 0.0, 1.0
    for o, d, lo, hi in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], z0, z1)):
        if abs(d) < 1e-9:
            if not (lo < o < hi): return False
        else:
            ta, tb = (lo - o) / d, (hi - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
def visible(a, b, eye): return not any(seg_hits(a, b, e, eye) for e in els)
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9) if visible((a[0] + dx, a[1] + dz), (b[0] + ex, b[1] + ez), eye))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a, b):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

def ground_raster(step=.5):
    """0.5 m raster of the slab: a cell is blocked by any block reaching above a 0.45 m step within the 0.42 m capsule (the bots'
    obstacle ray starts about .45 m up). Returns the flood-filled reachable set from spawn A1 and the blocked set."""
    nx, nz = int(round((X1 - X0) / step)), int(round((Z1 - Z0) / step)); blocked = set()
    for i in range(nx):
        for j in range(nz):
            cx, cz = X0 + (i + .5) * step, Z0 + (j + .5) * step
            if not (BOUNDS['minX'] + .42 <= cx <= BOUNDS['maxX'] - .42 and BOUNDS['minZ'] + .42 <= cz <= BOUNDS['maxZ'] - .42): blocked.add((i, j)); continue
            for e in els:
                x0, x1, y0, y1, z0, z1 = e['box']
                if y0 < 1.0 and y1 > .45 and x0 - .42 < cx < x1 + .42 and z0 - .42 < cz < z1 + .42: blocked.add((i, j)); break
    cell = lambda p: (int((p[0] - X0) / step), int((p[1] - Z0) / step))
    start = cell(teamA[0]); seen = {start}; todo = [start]
    while todo:
        i, j = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            if 0 <= n[0] < nx and 0 <= n[1] < nz and n not in blocked and n not in seen: seen.add(n); todo.append(n)
    return seen, blocked, cell, nx * nz

def longest_sightlines(step=1.0, dirs=72, eye=1.6, keep=6, band=.3):
    """Longest open lanes of sight at standing eye height from every 1 m open cell, in 72 directions. A lane is the ray and its two
    parallels 0.3 m to each side (a 0.6 m band, most of a pilot's width): slits narrower than that between two pieces do not count.
    The boxes that hide a standing pilot (tops at 2.0 m or more) and the play bounds stop a ray. Returns the longest few lanes."""
    tall = [e['box'] for e in els if e['box'][3] >= 2.0 and e['box'][2] < eye]
    def ray(x, z, dx, dz):
        t = 1e9
        for lo, hi, o, d in ((BOUNDS['minX'], BOUNDS['maxX'], x, dx), (BOUNDS['minZ'], BOUNDS['maxZ'], z, dz)):
            if abs(d) > 1e-9: t = min(t, ((hi if d > 0 else lo) - o) / d)
        for b in tall:
            t0, t1 = 0.0, t
            for lo, hi, o, d in ((b[0], b[1], x, dx), (b[4], b[5], z, dz)):
                if abs(d) < 1e-9:
                    if not (lo < o < hi): t0 = 1e9; break
                else:
                    ta, tb = sorted(((lo - o) / d, (hi - o) / d)); t0, t1 = max(t0, ta), min(t1, tb)
            if t0 < t1: t = min(t, max(t0, 0.0))
        return t
    res = []
    xs = [BOUNDS['minX'] + .5 + step * k for k in range(int((BOUNDS['maxX'] - BOUNDS['minX']) / step))]
    zs = [BOUNDS['minZ'] + .5 + step * k for k in range(int((BOUNDS['maxZ'] - BOUNDS['minZ']) / step))]
    for x in xs:
        for z in zs:
            if any(b[0] <= x <= b[1] and b[4] <= z <= b[5] for b in tall): continue
            for k in range(dirs):
                a = 2 * math.pi * k / dirs; dx, dz = math.cos(a), math.sin(a)
                t = min(ray(x + o * -dz, z + o * dx, dx, dz) for o in (-band, 0, band))
                res.append((t, (x, z), (round(x + dx * t, 1), round(z + dz * t, 1))))
    res.sort(reverse=True); out = []
    for r in res:                                                        # report distinct lanes (drop near-duplicates of a longer one)
        if all(math.dist(r[1], o[1]) + math.dist(r[2], o[2]) > 6 for o in out): out.append(r)
        if len(out) == keep: break
    return out

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)),
                'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], 1.1, p[1]), 'yaw': round(yaw(p), 3)}
    # FFA lists 8 entries like every map: the 4 FFA points, then the other team points (rooms on this map are capped at 4)
    ffa8 = ffa + [teamA[1], teamB[1], teamA[2], teamB[2]]
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'breakwater', 'name': 'Breakwater', 'tag': 'The pier head at blue hour. One wall, one lamp, one of you leaves.', 'theme': 'breakwater',
                      'music': 'breakwater-blue-hour', 'ambience': 'breakwater-surf', 'look': 'breakwater-blue-hour', 'sky': 0x2A3E66, 'floor': 0x7A7F86, 'accent': 0xF2B45A,
                      'rotation': False, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa8], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=12):
    W, H = int(round((X1 - X0) * scale)), int(round((Z1 - Z0) * scale)); img = bytearray(b'\x10\x18\x28' * W * H)
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        i0, i1 = int(round((x0 - X0) * scale)), int(round((x1 - X0) * scale)); j0, j1 = int(round((Z1 - z1) * scale)), int(round((Z1 - z0) * scale))
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    fill(X0, X1, Z0, Z1, (96, 100, 106))
    for k in range(int(X0), int(X1) + 1):                                 # a 2 m grid to read distances
        if k % 2 == 0: fill(k - .03, k + .03, Z0, Z1, (110, 114, 120))
    for k in range(int(Z0), int(Z1) + 1):
        if k % 2 == 0: fill(X0, X1, k - .03, k + .03, (110, 114, 120))
    col = {'#': (150, 146, 136), 'K': (196, 84, 52), 'B': (70, 110, 150), '=': (176, 176, 170), 'W': (90, 96, 104), 'x': (184, 134, 78), 'o': (60, 120, 110)}
    for e in sorted(els, key=lambda e: e['box'][3]):
        if e['id'].endswith('-roof'): continue
        b = e['box']; c = col.get(e['ch'], (128, 128, 128))
        if e['ch'] == 'x' and b[2] > 0: c = tuple(min(255, int(k * 1.15)) for k in c)
        fill(b[0], b[1], b[4], b[5], c)
    for p in PILOT:
        c, s = p['center'], p['size']
        if p['bottom'] > 1.2 and 'parapet' not in p['id']: fill(c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, (255, 255, 255), .12)
    for i, p in enumerate(teamA + teamB): fill(p[0] - .4, p[0] + .4, p[1] - .4, p[1] + .4, (60, 200, 255) if i < 4 else (255, 90, 120))
    for p in ffa[2:]: fill(p[0] - .4, p[0] + .4, p[1] - .4, p[1] + .4, (240, 240, 240))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint())
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    sp = ffa + teamA[1:] + teamB[1:]
    print('duel spawns %.1f m apart; min FFA spacing %.1f m; min A-B spacing %.1f m' % (math.dist(teamA[0], teamB[0]), min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p, q) for p in teamA for q in teamB)))
    clr = min((min(dist_rect(p, (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < 1.9), p) for p in sp)
    print('closest spawn to any block: %.2f m at %s' % clr); bad += clr[0] < .6
    inside = all(BOUNDS['minX'] + .42 <= x <= BOUNDS['maxX'] - .42 and BOUNDS['minZ'] + .42 <= z <= BOUNDS['maxZ'] - .42 for x, z in sp)
    print('all spawns inside the bounds minus the capsule:', inside); bad += not inside
    print('hazards: none (a clean duel; the sea is outside the parapet, so there is no fall)')
    def covered(e):                                                      # the footprint lies inside a pilot column starting at or below its top
        x0, x1, y0, y1, z0, z1 = e['box']                               # (a booth wall's top is under the roof, whose column starts 0.2 m above it)
        return any(p['bottom'] <= y1 + .3 and p['center']['x'] - p['size']['x'] / 2 - .01 <= x0 and x1 <= p['center']['x'] + p['size']['x'] / 2 + .01
                   and p['center']['z'] - p['size']['z'] / 2 - .01 <= z0 and z1 <= p['center']['z'] + p['size']['z'] / 2 + .01 for p in PILOT)
    flat = [e['id'] for e in els if e['box'][3] > 1.25 and not covered(e)]
    print('flat: every top above 1.25 m under a pilot block:', not flat, flat or ''); bad += bool(flat)
    seen, blocked, cell, total = ground_raster()
    reach = {n: cell(p) in seen for n, p in list(zip(ffa_names, ffa)) + A + B}
    print('ground raster (0.5 m): %d of %d open cells reachable from A1; every spawn reachable: %s %s' % (len(seen), total - len(blocked), all(reach.values()), [n for n, r in reach.items() if not r] or ''))
    bad += not all(reach.values()) or len(seen) != total - len(blocked)
    long = longest_sightlines()
    print('longest open sight lanes (0.6 m wide) at eye 1.6 m; the play diagonal is %.1f m:' % math.hypot(40, 26))
    for t, a, b in long: print('   %.1f m  %s -> %s' % (t, a, b))
    bad += long[0][0] > 32.0                                             # no lane runs the pier end to end (the play length is 40 m)
    ids = [e['id'] for e in els] + [FLOOR['id']] + [p['id'] for p in PILOT]; dup = sorted({i for i in ids if ids.count(i) > 1})
    print('unique ids (blocks, floor, pilot blocks):', not dup, dup or ''); bad += bool(dup)
    print('blocks: %d (+ floor); crates (breakable): %d; hazards: %d; pilot blocks: %d' % (len(els), sum(e['role'] == 'cover' for e in els), len(HAZ), len(PILOT)))
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'breakwater_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'breakwater_m1.png')); print('wrote', os.path.join(out, 'breakwater_m1.json'), 'and breakwater_m1.png')
    sys.exit(1 if bad else 0)
