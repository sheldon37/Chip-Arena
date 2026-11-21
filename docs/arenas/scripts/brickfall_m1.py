# Brickfall M1 (compressed 64x56, 3-storey hotel, 2-storey tenements) draft painter + spawn LOS check.
# Coordinates: x east, z north, metres. Point symmetric: (x,z)->(-x,-z) unless noted.
import math, json, itertools, sys
X0, X1, Z0, Z1 = -32, 32, -28, 28
els = []   # (name, kind, x0,x1,z0,z1, top, char, solid_for_los)
def add(name, x0, x1, z0, z1, top, ch, los=None, mirror=True, mname=None):
    los = top >= 2.0 if los is None else los
    els.append((name, x0, x1, z0, z1, top, ch, los))
    if mirror:
        els.append((mname or (name + '~m'), -x1, -x0, -z1, -z0, top, ch, los))
def wall_x(name, x0, x1, zc, openings, top, ch, mirror=True, t=.3):
    cuts = [x0] + [v for o in sorted(openings) for v in o] + [x1]
    for i in range(0, len(cuts), 2):
        if cuts[i+1] > cuts[i]: add(f'{name}{i}', cuts[i], cuts[i+1], zc - t/2, zc + t/2, top, ch, mirror=mirror)
def wall_z(name, xc, z0, z1, openings, top, ch, mirror=True, t=.3):
    cuts = [z0] + [v for o in sorted(openings) for v in o] + [z1]
    for i in range(0, len(cuts), 2):
        if cuts[i+1] > cuts[i]: add(f'{name}{i}', xc - t/2, xc + t/2, cuts[i], cuts[i+1], top, ch, mirror=mirror)
# --- perimeter (indestructible frontage 2 m deep), alcoves are painted only
add('frontage-n', -32, 32, 26, 28, 14, '#')
add('frontage-w', -32, -30, -28, 28, 14, '#')
paint_only = []
for ax in (-27, -16, 1, 18): paint_only.append((ax, ax + 2, 26, 27.5, 'a', True))
# --- north stack (topples south along its fell line)
add('stack-n', 8.8, 11.2, 25.6, 28, 16, 'K')
paint_only.append((8.5, 11.5, 10, 25.6, 'k', True))          # fell line (kept free of props)
# --- Ashby House (NW) x -25..-6, z 12..21 ; 2 storeys, roof 7.2 + 1.0 parapet
wall_x('ashby-lane', -25, -6, 21, [(-23.6, -16.4), (-14.6, -7.4)], 8.2, 'T')   # ground shop windows between piers
wall_x('ashby-south', -25, -6, 12, [(-22.5, -20.5), (-9, -7.5)], 8.2, 'T')
wall_z('ashby-west', -25, 12, 21, [(13, 15)], 8.2, 'T')
wall_z('ashby-east', -6, 12, 21, [(13, 15)], 8.2, 'T')
add('ashby-core', -20, -9.6, 12.3, 16.7, 7.2, 'C')
for nm, px, pz in (('F1', -24.5, 20.5), ('F2', -15.5, 20.5), ('F3', -6.5, 20.5), ('R1', -24.5, 12.5), ('R2', -6.5, 12.5)):
    add('ashby-pier-' + nm, px - .5, px + .5, pz - .5, pz + .5, 3.6, 'P', mname='bray-pier-' + nm)
paint_only.append((-23, -7, 21, 23.8, ',', True))            # facade-peel berm volume (kept free of props)
# --- Kessel's corner shop (static, closed shutters), roof perch 3.6 via lane-side ramp
add('kessels', -2, 6, 14, 19.5, 4.4, 'S')
paint_only.append((-1.6, 6, 19.5, 21, '/', True))
# --- Hotel Mercer x -8..8, z -6..6 (self-symmetric shell; core arcade is the one asymmetry)
wall_x('hotel-n', -8, 8, 6, [(-7, -5.4), (0.2, 2.2), (3.5, 7)], 11.8, 'H', mirror=False)   # N door moved west so no line runs N door -> seam door -> S shopfront
wall_x('hotel-s', -8, 8, -6, [(-7, -3.5), (0.2, 2.2), (5.4, 7)], 11.8, 'H', mirror=False)
wall_z('hotel-w', -8, -6, 6, [(1, 3)], 11.8, 'H', mirror=False)
wall_z('hotel-e', 8, -6, 6, [(-3, -1)], 11.8, 'H', mirror=False)
add('hotel-seam-w', -8, -4, -.6, .6, 3.3, 'H', mirror=False)
add('hotel-seam-e', 4, 8, -.6, .6, 3.3, 'H', mirror=False)
add('hotel-core-w', -2.4, 0.2, -5.8, 5.8, 11.2, 'C', mirror=False)
add('hotel-core-e', 2.2, 2.4, -5.8, 5.8, 11.2, 'C', mirror=False)
for sx in (-1, 1):
    for sz in (-1, 1):
        add(f'hotel-pier-{"ns"[sz < 0]}{"we"[sx > 0]}', sx*8 - .6, sx*8 + .6, sz*6 - .6, sz*6 + .6, 3.6, 'P', mirror=False)
paint_only.append((-8, -6, 6, 12, '=', True))                 # Ashby skybridge deck 7.2 (overhead, not a LOS blocker)
# --- square streets cover
add('tram-shelter-n', -1.5, 1.5, 8.5, 10, 2.6, 'O')
add('monument-ne', 10.5, 12.5, 7, 9, 2.4, 'O')
add('barricade-w', -11.5, -9.5, -1, 1, 1.2, 'o')
# --- West Market x -30..-14, z -12..12 (open girder canopy at 6 m is presentation only)
add('kiosk-w', -23.5, -20.5, -1.5, 1.5, 3.2, 'O')
wall_x('market-wall-w', -30, -14, -12, [(-28.5, -26), (-20, -17)], 4.0, 'M')   # market hall south wall, two arches
add('storeroom-w', -30, -24, -7, -2, 3.5, 'O')
for i, (cx, cz) in enumerate(((-26.5, 5.5), (-17.5, 6.5), (-22.5, 9.5), (-17.5, -4.5), (-21.5, -8), (-26.5, -9))):
    add(f'stall-w{i}', cx - 1.2, cx + 1.2, cz - .6, cz + .6, 1.1, 's')
# --- Brick Yard (SW, team A) x -30..-14, z -26..-12
add('dock-sw', -29.5, -25, -25.5, -23, 1.2, 'd')
paint_only.append((-25, -22.4, -25.5, -23, '/', True))
add('truck-sw', -21.5, -15, -19.5, -17, 3.0, 't')
for i, (cx, cz) in enumerate(((-28, -17.5), (-24.5, -12.8), (-17, -24.5), (-15.5, -13.5))):
    add(f'pallet-sw{i}', cx - .7, cx + .7, cz - .7, cz + .7, 1.1, 'o')
# --- lanes, close, steps
add('news-kiosk-n', 5.5, 7.5, 23, 25, 2.8, 'O')
add('phonebox-nw', -29.6, -28.4, 23.9, 25.1, 2.5, 'O')
for i, (cx, cz) in enumerate(((-27.5, 14.5), (-3.8, 23.2), (13, 22.5), (7.2, 16.5))):
    add(f'sandbag-{i}', cx - .9, cx + .9, cz - .5, cz + .5, 1.1, 'o')
# --- spawns
teamA = [(-27, -21), (-21.5, -14.5), (-17.5, -22), (-27.5, -13.5)]
teamB = [(-x, -z) for x, z in teamA]
ffa = [teamA[0], teamB[0], (-27.5, 19), (27.5, -19), (-16, 9.5), (16, -9.5), (-4, 16), (4, -16)]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']

def paint():
    W = X1 - X0; R = (Z1 - Z0) // 2
    g = [['.'] * W for _ in range(R)]
    def put(x, z, ch):
        c = int(math.floor(x - X0)); r = int((Z1 - z) // 2)
        if 0 <= c < W and 0 <= r < R: g[r][c] = ch
    def rect(x0, x1, z0, z1, ch):
        zz = z0
        while zz < z1:
            xx = x0
            while xx < x1:
                put(xx + .25, zz + .25, ch); xx += .5
            zz += .5
    for e in els:
        if e[6] == '#': rect(e[1], e[2], e[3], e[4], e[6])
    for (x0, x1, z0, z1, ch, m) in paint_only:
        rect(x0, x1, z0, z1, ch)
        if m: rect(-x1, -x0, -z1, -z0, ch)
    for e in els:
        if e[6] in 'CP#': continue
        rect(e[1], e[2], e[3], e[4], e[6])
    for e in els:
        if e[6] in 'CP': rect(e[1], e[2], e[3], e[4], e[6])
    for i, (x, z) in enumerate(teamA): put(x, z, str(i + 1)); put(-x, -z, str(i + 5))
    for nm, (x, z) in zip(ffa_names[2:], ffa[2:]): put(x, z, nm)
    out = ['         x=-32' + ' ' * 26 + 'x=0' + ' ' * 27 + 'x=+31']
    for r in range(R):
        z = Z1 - 2 * r
        out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

def seg_hits(a, b, e, eye):
    if not e[7] or e[5] <= eye: return False
    x0, x1, z0, z1 = e[1], e[2], e[3], e[4]
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
def visible(a, b, eye):
    return not any(seg_hits(a, b, e, eye) for e in els)
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        n = 0
        for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4):
            for eye in (1.2, 1.6, 1.9):
                if visible((a[0] + dx, a[1] + dz), (b[0] + ex, b[1] + ez), eye): n += 1
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a, b):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights')
    for s in bad: print('   OPEN', s)

if __name__ == '__main__':
    print(paint())
    named_ffa = list(zip(ffa_names, ffa))
    check([(p, q) for p, q in itertools.combinations(named_ffa, 2)], 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]
    check([(p, q) for p in A for q in B], 'Teams A-B')
    sp = ffa + teamA[1:] + teamB[1:]
    print('min FFA spacing %.1f m' % min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)))
    print('min A-B spacing %.1f m' % min(math.dist(p, q) for p in teamA for q in teamB))
    # Crush, fell, berm and rubble volumes PLUS the space a structure sweeps while it falls:
    # hotel facades peel per storey (3.6 m panels, hinge at each floor line, <=50 deg): 3.6*sin50 = 2.8 m beyond the face;
    # tenement facade rows (8.2 m) peel out over the lane up to the frontage (z 21..26).
    P = 2.8
    vols = [((8.5, 11.5, 10, 25.6), 'fell-n'), ((-11.5, -8.5, -25.6, -10), 'fell-s'), ((-23, -7, 21, 23.8), 'berm-ashby'), ((7, 23, -23.8, -21), 'berm-bray'),
            ((-25, -6, 21, 26), 'sweep-facade-ashby'), ((6, 25, -26, -21), 'sweep-facade-bray'),
            ((-8 - P, -2.4, -0.5, 6 + P), 'hotel-nw'), ((2.4, 8 + P, -6 - P, 0.5), 'hotel-se'), ((-8 - P, -2.4, -6 - P, 0.5), 'hotel-sw'), ((2.4, 8 + P, -0.5, 6 + P), 'hotel-ne'),
            ((-9, -5, 6, 12.5), 'bridge-ashby'), ((5, 9, -12.5, -6), 'bridge-bray')]
    def dist_rect(p, r):
        dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)
    worst = min((dist_rect(p, v), n, p) for p in sp for v, n in vols)
    print('closest spawn to any crush/fell/berm/sweep volume: %.1f m (%s, spawn %s)' % worst)
    clr = min((min(dist_rect(p, (e[1], e[2], e[3], e[4])) for e in els), p) for p in sp)
    print('closest spawn to any block: %.1f m at %s' % clr)
    print('all spawns inside bot clamp |x|<=28,|z|<=24:', all(abs(x) <= 28 and abs(z) <= 24 for x, z in sp))
    # Hotel ground floor: lines through two openings on opposite faces (eye 1.6 m, 9 samples per opening).
    # The Mercer Arcade (x 0.2..2.2 on both N and S faces) is a deliberate straight N-S lane and is reported separately.
    N = [(-7, -5.4), (0.2, 2.2), (3.5, 7)]; S = [(-7, -3.5), (0.2, 2.2), (5.4, 7)]; Wd = [(1, 3)]; Ed = [(-3, -1)]
    smp = lambda o: [o[0] + (o[1] - o[0]) * (i + .5) / 9 for i in range(9)]
    arcade = (0.2, 2.2)
    ns = [(on, os_, xa, xb) for on in N for os_ in S for xa in smp(on) for xb in smp(os_) if visible((xa, 6.4), (xb, -6.4), 1.6)]
    other = [l for l in ns if not (l[0] == arcade and l[1] == arcade)]
    we = [(a, b) for a in smp(Wd[0]) for b in smp(Ed[0]) if visible((-8.4, a), (8.4, b), 1.6)]
    print('hotel N->S open lines: %d of %d sampled (arcade-to-arcade %d, any other pair %d); W->E open: %d of 81'
          % (len(ns), 9 * 9 * 9, len(ns) - len(other), len(other), len(we)))
