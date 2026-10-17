# Shared helpers for the Expedition layout generators (expedition_city.py, expedition_ward_zero.py, expedition_layout.py).
# Geometry in the Unity frame: x east, z north, y up, metres; yaw in degrees about +y, 0 faces +z, 90 faces +x.
# The collision model follows docs/arenas/scripts/wardzero_m1.py and Astra's tools/generate_wardzero.py: authored yaw-only
# boxes (collision as authored) plus closed walkable wedges (map ramps), and a capsule with a spherical bottom that steps
# 0.35 m. The probes are the ones Astra's layout loader uses (NativeExpeditionLayout: a 0.45 x 1.8 m pilot, a 0.954 x
# 3.06 m Hulk). Static analysis only: no Unity or PhysX claims.
# Deterministic: no clocks, no randomness, sorted outputs.
import hashlib, math, struct, zlib

RADIUS, HEIGHT, STEP = 0.45, 1.8, 0.35          # the pilot probe of Astra's layout validator (NativeExpeditionLayout: .45 m, 1.8 m)
HULK_RADIUS, HULK_HEIGHT = 0.954, 3.06          # NativeHordeKinds.HulkRadius, HulkHeight (the validator's Hulk probe)
HULK_SLOT = HULK_RADIUS + 0.05                  # free radius required at every slot, return and entry (the validator probes them as Hulks too)
PILOT_CLEAR = 0.6                               # free radius required at a slot or return (capsule radius + 0.18)
HULK_CLEAR = 1.2                                # free radius required where a Hulk walks out (radius 0.954 + 0.25)
HULK_BAND = 3.4                                 # headroom over a Hulk exit (doorways are 3.4 m)

def r4(x):
    x = round(x, 4); return 0.0 if x == 0 else x
def r6(x):
    x = round(x, 6); return 0.0 if x == 0 else x
def v(x=0, y=0, z=0): return dict(x=r4(x), y=r4(y), z=r4(z))
def q(x=0, y=0, z=0, w=1): return dict(x=r6(x), y=r6(y), z=r6(z), w=r6(w))
def qyaw(deg): a = math.radians(deg) / 2; return q(0, math.sin(a), 0, math.cos(a))
def yaw_of(dx, dz):
    """Degrees in [0, 360) of the direction (dx, dz): 0 is +z, 90 is +x."""
    return r4(math.degrees(math.atan2(dx, dz)) % 360.0) if (dx or dz) else 0.0
def yaw_rad(dx, dz): return r6(math.atan2(dx, dz)) if (dx or dz) else 0.0
def forward(deg): a = math.radians(deg); return math.sin(a), math.cos(a)
def vol(x, floor, z, sx, sy, sz, yaw=0.0):
    """A volume record standing on `floor`: x, y, z are its centre (y = floor + sy / 2, as NativeExpeditionLayout's
    ExpeditionVolume.Contains and its bounds check read it), sx, sy, sz its full size, yaw its turn about +y."""
    return dict(x=r4(x), y=r4(floor + sy / 2), z=r4(z), sx=r4(sx), sy=r4(sy), sz=r4(sz), yaw=r4(yaw % 360.0))
def vol_floor(V): return V['y'] - V['sy'] / 2
def pt(x, y, z, yaw=None):
    d = dict(x=r4(x), y=r4(y), z=r4(z))
    if yaw is not None: d['yaw'] = r4(yaw % 360.0)
    return d

# --- block geometry -------------------------------------------------------------------------------------------------
def yaw_only(b): r = b['rotation']; return abs(r['x']) < 1e-6 and abs(r['z']) < 1e-6
def block_yaw(b): r = b['rotation']; return 2 * math.atan2(r['y'], r['w'])
def local_xz(b, x, z):
    """(x, z) in a yaw-rotated block's frame, relative to its centre (Unity: +yaw turns +z toward +x)."""
    dx, dz = x - b['pos']['x'], z - b['pos']['z']; a = block_yaw(b); c, s = math.cos(a), math.sin(a)
    return (dx * c - dz * s, dx * s + dz * c)
def dist_xz(b, x, z):
    lx, lz = local_xz(b, x, z); hx, hz = b['size']['x'] / 2, b['size']['z'] / 2
    return math.hypot(max(abs(lx) - hx, 0), max(abs(lz) - hz, 0))
def yspan(b): p, s = b['pos'], b['size']; return p['y'] - s['y'] / 2, p['y'] + s['y'] / 2
def footprint(b):
    """The world-axis bounding rectangle of the block's footprint (x0, x1, z0, z1)."""
    p, s = b['pos'], b['size']; a = block_yaw(b); c, sn = abs(math.cos(a)), abs(math.sin(a))
    hx = s['x'] / 2 * c + s['z'] / 2 * sn; hz = s['x'] / 2 * sn + s['z'] / 2 * c
    return p['x'] - hx, p['x'] + hx, p['z'] - hz, p['z'] + hz
def corners(b):
    p, s = b['pos'], b['size']; a = block_yaw(b); c, sn = math.cos(a), math.sin(a); out = []
    for ux, uz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        lx, lz = ux * s['x'] / 2, uz * s['z'] / 2
        out.append((p['x'] + lx * c + lz * sn, p['z'] - lx * sn + lz * c))
    return out
def vol_block(id, V):
    """A volume record as a pseudo block (collision or containment tests)."""
    return dict(id=id, pos=dict(x=V['x'], y=V['y'], z=V['z']), size=dict(x=V['sx'], y=V['sy'], z=V['sz']), rotation=qyaw(V['yaw']))
def ramp_block(r):
    """A ramp wedge's footprint as a yaw-rotated pseudo block (bottom to top)."""
    lx, lz, hx, hz = r['low']['x'], r['low']['z'], r['high']['x'], r['high']['z']; L = math.hypot(hx - lx, hz - lz)
    return dict(id=r['id'], pos=dict(x=(lx + hx) / 2, y=(r['bottom'] + r['high']['y']) / 2, z=(lz + hz) / 2), size=dict(x=r['width'], y=r['high']['y'] - r['bottom'], z=L),
                rotation=qyaw(math.degrees(math.atan2(hx - lx, hz - lz))))
def in_vol(V, x, y, z, pad=0.0):
    b = vol_block('', V); lx, lz = local_xz(b, x, z)
    return abs(lx) <= V['sx'] / 2 + pad and abs(lz) <= V['sz'] / 2 + pad and abs(y - V['y']) <= V['sy'] / 2 + 1e-6 + pad
def vol_in_bounds(V, b, margin=0.01):
    """NativeExpeditionLayout's volume bounds test (its rotated extents inside the map bounds), with a margin."""
    a = math.radians(V['yaw']); c, s = abs(math.cos(a)), abs(math.sin(a)); rx = (c * V['sx'] + s * V['sz']) / 2; rz = (s * V['sx'] + c * V['sz']) / 2
    return (V['x'] - rx >= b['minX'] + margin and V['x'] + rx <= b['maxX'] - margin and V['z'] - rz >= b['minZ'] + margin and V['z'] + rz <= b['maxZ'] - margin
            and V['y'] - V['sy'] / 2 >= b['killY'] + margin and V['y'] + V['sy'] / 2 <= b['ceiling'] - margin)
def vol_dist_xz(V, x, z): return dist_xz(vol_block('', V), x, z)

# --- collision and walkable floor ------------------------------------------------------------------------------------
class Collider:
    """Authored colliding blocks (yaw-only boxes) plus closed walkable wedges, over a 0.5 m grid inside the bounds."""
    BUCKET = 4.0
    def __init__(self, blocks, ramps=(), bounds=None, cell=0.5, top=None):
        self.blocks = [b for b in blocks]; self.ramps = list(ramps); self.cell = cell
        for b in self.blocks: assert yaw_only(b), 'collision blocks must be yaw-only: ' + b['id']
        self.bounds = bounds; self.top = top
        self.buckets = {}
        for b in self.blocks:
            x0, x1, z0, z1 = footprint(b)
            for bi in range(int(math.floor((x0 - 1) / self.BUCKET)), int(math.floor((x1 + 1) / self.BUCKET)) + 1):
                for bj in range(int(math.floor((z0 - 1) / self.BUCKET)), int(math.floor((z1 + 1) / self.BUCKET)) + 1):
                    self.buckets.setdefault((bi, bj), []).append(b)
        self.nx = int(round((bounds['maxX'] - bounds['minX']) / cell)); self.nz = int(round((bounds['maxZ'] - bounds['minZ']) / cell))
        self.graph = None; self.by_cell = {}
    def near(self, x, z): return self.buckets.get((int(math.floor(x / self.BUCKET)), int(math.floor(z / self.BUCKET))), ())
    def near_r(self, x, z, r):
        seen = set(); out = []
        for bi in range(int(math.floor((x - r) / self.BUCKET)), int(math.floor((x + r) / self.BUCKET)) + 1):
            for bj in range(int(math.floor((z - r) / self.BUCKET)), int(math.floor((z + r) / self.BUCKET)) + 1):
                for b in self.buckets.get((bi, bj), ()):
                    if id(b) not in seen: seen.add(id(b)); out.append(b)
        return out
    @staticmethod
    def wedge(r, x, z):
        lx, lz = r['low']['x'], r['low']['z']; hx, hz = r['high']['x'], r['high']['z']
        dx, dz = hx - lx, hz - lz; L = math.hypot(dx, dz); ux, uz = dx / L, dz / L
        t = (x - lx) * ux + (z - lz) * uz; w = -(x - lx) * uz + (z - lz) * ux
        if t < 0 or t > L or abs(w) > r['width'] / 2: return None
        return r['low']['y'] + (r['high']['y'] - r['low']['y']) * t / L
    def cell_xz(self, i, j): return (self.bounds['minX'] + (i + 0.5) * self.cell, self.bounds['minZ'] + (j + 0.5) * self.cell)
    def surfaces(self, x, z):
        out = set()
        for b in self.near(x, z):
            if dist_xz(b, x, z) <= 1e-9:
                y0, y1 = yspan(b)
                if y1 - y0 > 0.02: out.add(round(y1, 3))
        for r in self.ramps:
            h = self.wedge(r, x, z)
            if h is not None: out.add(round(h, 3))
        return sorted(out)
    def blocked(self, x, z, y, radius=RADIUS, height=HEIGHT):
        """Whether a capsule (radius, height, a spherical bottom) standing at (x, y, z) overlaps anything. An obstacle at
        horizontal distance d blocks when it rises above the step height and above the bottom sphere's surface there
        (y + r - sqrt(r^2 - d^2)), so a capsule rides a slope or a deck edge as PhysX does; a flat-bottomed cylinder
        would wrongly ground a Hulk on its own stair."""
        hi = y + height + 0.05; step = y + STEP + 0.01
        def surface(d): return y + radius - math.sqrt(max(radius * radius - d * d, 0.0)) + 0.03
        for b in self.near_r(x, z, radius) if radius > 1.0 else self.near(x, z):
            d = dist_xz(b, x, z)
            if d < radius:
                y0, y1 = yspan(b)
                if y0 < hi and y1 > max(step, surface(d)): return True
        for r in self.ramps:
            for k in range(8):
                a = k * math.pi / 4
                for f in (0.5, 1.0):
                    h = self.wedge(r, x + math.sin(a) * radius * f, z + math.cos(a) * radius * f)
                    if h is not None and h > max(step, surface(radius * f)): return True
        return False
    def clearance(self, x, z, ylo, yhi, maxr=8.0, skip=()):
        """The free radius at (x, z): the distance to the nearest colliding block or ramp wedge overlapping ylo..yhi
        (capped at maxr). A wedge counts as its whole footprint from its bottom to its top (conservative)."""
        best = maxr
        for b in self.near_r(x, z, maxr):
            if b['id'] in skip: continue
            y0, y1 = yspan(b)
            if y0 < yhi and y1 > ylo: best = min(best, dist_xz(b, x, z))
        for r in self.ramps:
            if r['bottom'] < yhi and r['high']['y'] > ylo: best = min(best, dist_xz(ramp_block(r), x, z))
        return best
    def nearest_block(self, x, z, ylo, yhi, maxr=8.0):
        best = (maxr, None)
        for b in self.near_r(x, z, maxr):
            y0, y1 = yspan(b)
            if y0 < yhi and y1 > ylo:
                d = dist_xz(b, x, z)
                if d < best[0]: best = (d, b['id'])
        return best
    def build(self, keep=lambda y: True, radius=RADIUS, height=HEIGHT):
        """The standable cells for a capsule of this radius and height (the pilot probe by default)."""
        self.graph = set(); self.by_cell = {}
        for i in range(self.nx):
            for j in range(self.nz):
                x, z = self.cell_xz(i, j)
                for y in self.surfaces(x, z):
                    if not keep(y): continue
                    if not self.blocked(x, z, y, radius, height): self.graph.add((i, j, y)); self.by_cell.setdefault((i, j), []).append((i, j, y))
        return self.graph
    def neighbours(self, n):
        i, j, y = n
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            for m in self.by_cell.get((i + di, j + dj), ()):
                if abs(m[2] - y) <= STEP + 1e-6: yield m
    def flood(self, starts, allowed=None):
        seen = set(); stack = list(starts)
        while stack:
            n = stack.pop()
            if n in seen or n not in self.graph or (allowed is not None and not allowed(n)): continue
            seen.add(n); stack.extend(self.neighbours(n))
        return seen
    def node_at(self, x, y, z, tol=0.6):
        best = None; ci = int((x - self.bounds['minX']) / self.cell); cj = int((z - self.bounds['minZ']) / self.cell)
        for di in range(-2, 3):
            for dj in range(-2, 3):
                for n in self.by_cell.get((ci + di, cj + dj), ()):
                    if abs(n[2] - y) > 0.3: continue
                    cx, cz = self.cell_xz(n[0], n[1]); d = math.hypot(cx - x, cz - z)
                    if d <= tol and (best is None or d < best[0]): best = (d, n)
        return best[1] if best else None
    def floor_under(self, x, z, y, tol=0.05):
        """Whether a collider surface lies at y under (x, z) (the record's y is the floor it stands on)."""
        return any(abs(s - y) <= tol for s in self.surfaces(x, z))

def components(col, cells):
    """Connected groups of the given nodes (size, centre x, centre z), largest first."""
    left = set(cells); out = []
    while left:
        s = left.pop(); comp = col.flood([s], allowed=lambda m: m in left or m == s); left -= comp
        xs = [col.cell_xz(m[0], m[1]) for m in comp]
        out.append((len(comp), sum(p[0] for p in xs) / len(xs), sum(p[1] for p in xs) / len(xs)))
    return sorted(out, reverse=True)

# --- the map's geometry hash (the manifest's per-area blockHash) ----------------------------------------------------
def _i(x, k): return str(int(round(float(x) * k)))
def block_hash(m):
    """SHA-256 (lowercase hex) of the map's geometry as UTF-8 lines, each ending in '\\n', in file order:
    B|id|material|role|hp|px|py|pz|sx|sy|sz|qx|qy|qz|qw   per block (hp, positions and sizes x 10^4, quaternion x 10^6,
                                                       each rounded to the nearest integer)
    R|id|kind|lx|ly|lz|hx|hy|hz|width|bottom|b1,b2,...   per ramp (x 10^4)
    C|id                                               per navHints.collisionOff id
    K|minX|maxX|minZ|maxZ|ceiling|killY                the bounds (x 10^4)"""
    L = []
    for b in m['blocks']:
        p, s, r = b['pos'], b['size'], b['rotation']
        L.append('|'.join(['B', b['id'], b['material'], b['role'], _i(b['hp'], 1e4)] + [_i(p[k], 1e4) for k in 'xyz'] + [_i(s[k], 1e4) for k in 'xyz'] + [_i(r[k], 1e6) for k in 'xyzw']))
    for r in m.get('ramps') or []:
        L.append('|'.join(['R', r['id'], r['kind']] + [_i(r['low'][k], 1e4) for k in 'xyz'] + [_i(r['high'][k], 1e4) for k in 'xyz'] + [_i(r['width'], 1e4), _i(r['bottom'], 1e4), ','.join(r['blocks'])]))
    for c in (m.get('navHints') or {}).get('collisionOff') or []: L.append('C|' + c)
    b = m['bounds']; L.append('|'.join(['K'] + [_i(b[k], 1e4) for k in ('minX', 'maxX', 'minZ', 'maxZ', 'ceiling', 'killY')]))
    return hashlib.sha256(''.join(l + '\n' for l in L).encode('utf-8')).hexdigest()

def id_ok(i):
    """ExpeditionValidation.Id: 1..128 characters, letters, digits, '-', '_', '.', ':'."""
    return isinstance(i, str) and 0 < len(i) <= 128 and all(c.isalnum() or c in '-_.:' for c in i)

# --- PNG plans --------------------------------------------------------------------------------------------------------
GLYPHS = dict(A='010101111101101', B='110101110101110', C='011100100100011', D='110101101101110', E='111100110100111', F='111100110100100',
              G='011100101101011', H='101101111101101', I='111010010010111', J='001001001101010', K='101101110101101', L='100100100100111',
              M='101111111101101', N='110101101101101', O='010101101101010', P='110101110100100', Q='010101101110011', R='110101110101101',
              S='011100010001110', T='111010010010010', U='101101101101111', V='101101101101010', W='101101111111101', X='101101010101101',
              Y='101101010010010', Z='111001010100111')
GLYPHS.update({'0': '111101101101111', '1': '010110010010111', '2': '110001010100111', '3': '110001010001110', '4': '101101111001001',
               '5': '111100110001110', '6': '011100111101111', '7': '111001010010010', '8': '111101111101111', '9': '111101111001110',
               '-': '000000111000000', '+': '000010111010000', '.': '000000000000010', ':': '000010000010000', '/': '001001010100100',
               '(': '010100100100010', ')': '010001001001010', ',': '000000000010100', '=': '000111000111000', '#': '101111101111101',
               "'": '010010000000000', ' ': '0' * 15})
class Canvas:
    def __init__(self, W, H, bg=(14, 16, 20)):
        self.W, self.H = W, H; self.img = bytearray(bytes(bg) * (W * H))
    def px(self, x, y, c, a=1.0):
        if 0 <= x < self.W and 0 <= y < self.H:
            o = (y * self.W + x) * 3
            if a >= 1.0: self.img[o:o + 3] = bytes(c)
            else:
                for k in range(3): self.img[o + k] = int(self.img[o + k] * (1 - a) + c[k] * a)
    def rect(self, x0, y0, x1, y1, c, a=1.0):
        x0, x1 = max(0, min(x0, x1)), min(self.W, max(x0, x1)); y0, y1 = max(0, min(y0, y1)), min(self.H, max(y0, y1))
        if x1 <= x0 or y1 <= y0: return
        if a >= 1.0:
            row = bytes(c) * (x1 - x0)
            for y in range(y0, y1): o = (y * self.W + x0) * 3; self.img[o:o + len(row)] = row
        else:
            for y in range(y0, y1):
                for x in range(x0, x1): self.px(x, y, c, a)
    def frame(self, x0, y0, x1, y1, c, t=1):
        self.rect(x0, y0, x1, y0 + t, c); self.rect(x0, y1 - t, x1, y1, c); self.rect(x0, y0, x0 + t, y1, c); self.rect(x1 - t, y0, x1, y1, c)
    def disc(self, cx, cy, r, c, a=1.0):
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r: self.px(cx + dx, cy + dy, c, a)
    def ring(self, cx, cy, r, c, t=1):
        for dy in range(-r - 1, r + 2):
            for dx in range(-r - 1, r + 2):
                d = math.hypot(dx, dy)
                if r - t < d <= r: self.px(cx + dx, cy + dy, c)
    def line(self, x0, y0, x1, y1, c, t=1):
        n = int(max(abs(x1 - x0), abs(y1 - y0), 1))
        for k in range(n + 1):
            x = int(round(x0 + (x1 - x0) * k / n)); y = int(round(y0 + (y1 - y0) * k / n))
            if t <= 1: self.px(x, y, c)
            else: self.rect(x - t // 2, y - t // 2, x - t // 2 + t, y - t // 2 + t, c)
    def poly(self, pts, c, a=1.0):
        ys = [p[1] for p in pts]
        for y in range(max(0, int(min(ys))), min(self.H, int(max(ys)) + 1)):
            xs = []
            for k in range(len(pts)):
                (x0, y0), (x1, y1) = pts[k], pts[(k + 1) % len(pts)]
                if (y0 <= y + 0.5 < y1) or (y1 <= y + 0.5 < y0): xs.append(x0 + (y + 0.5 - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for k in range(0, len(xs) - 1, 2): self.rect(int(round(xs[k])), y, int(round(xs[k + 1])), y + 1, c, a)
    def text(self, x, y, s, c, k=2):
        for ch in s.upper():
            bits = GLYPHS.get(ch, '0' * 15)
            for r in range(5):
                for col in range(3):
                    if bits[r * 3 + col] == '1': self.rect(x + col * k, y + r * k, x + col * k + k, y + r * k + k, c)
            x += 4 * k
        return x
    def save(self, path):
        raw = b''.join(b'\x00' + bytes(self.img[y * self.W * 3:(y + 1) * self.W * 3]) for y in range(self.H))
        def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
        with open(path, 'wb') as f:
            f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', self.W, self.H, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))
def text_width(s, k=2): return len(s) * 4 * k
