"""Meridian size-B street plans: a small geometry kit, checks and JSON export.

Axes follow the game: x east, z north, y up, metres. The camp (49.6 m square, four 10 m gates) sits at the
origin on y 0 in every layout; the map bounds move instead, so the hub's place in the city varies.
Everything is axis-aligned boxes (ContentBlock) and axis-aligned wedges (MapRamp), as the game builds today.
"""
import json, math, random, zlib
from collections import deque
import numpy as np

DIRS = {'N': (0, 1), 'E': (1, 0), 'S': (0, -1), 'W': (-1, 0)}
LEFT = {'N': 'W', 'W': 'S', 'S': 'E', 'E': 'N'}
RIGHT = {v: k for k, v in LEFT.items()}
BACK = {'N': 'S', 'S': 'N', 'E': 'W', 'W': 'E'}
GATES = {'N': (0.0, 25.0), 'E': (25.0, 0.0), 'S': (0.0, -25.0), 'W': (-25.0, 0.0)}
STAIR_SLOPE = 0.5          # 26.6 degrees, as the plan's stair-streets
FLIGHT_RISE = 3.5          # a landing every 3.5 m of rise
LANDING = 3.0
HEADROOM = 1.8             # pilot standing room used by the reach check
HORDE_HEADROOM = 3.2


def rect_of(x0, z0, x1, z1):
    return (min(x0, x1), min(z0, z1), max(x0, x1), max(z0, z1))


class Layout:
    def __init__(self, lid, name, title, bounds, default_y, seed, base_y=None):
        self.id, self.name, self.title = lid, name, title
        self.minX, self.maxX, self.minZ, self.maxZ = bounds
        assert abs((self.maxX - self.minX) - 600) < 1e-6 and abs((self.maxZ - self.minZ) - 480) < 1e-6
        self.nx, self.nz = int(self.maxX - self.minX), int(self.maxZ - self.minZ)
        self.ground = np.full((self.nz, self.nx), float(default_y))
        self.reserved = np.zeros((self.nz, self.nx), bool)
        self.lotmask = np.zeros((self.nz, self.nx), bool)
        self.rng = random.Random(seed)
        self.seed = seed
        self.base_y = base_y
        self.blocks, self.ramps, self.routes, self.doors, self.landmarks = [], [], [], [], []
        self.links, self.zones, self.notes, self.parapet_runs = [], [], [], []
        self.court = None
        self.hub_rects = []
        self.description = ''
        self.hub_name = ''
        self.tier_names = {}
        self.ceiling = 60.0

    # ---- raster helpers -------------------------------------------------------------------------
    def _ix(self, x0, z0, x1, z1):
        ix0 = int(round(x0 - self.minX)); ix1 = int(round(x1 - self.minX))
        iz0 = int(round(z0 - self.minZ)); iz1 = int(round(z1 - self.minZ))
        return max(0, iz0), min(self.nz, iz1), max(0, ix0), min(self.nx, ix1)

    def paint(self, r, y):
        a, b, c, d = self._ix(*r)
        self.ground[a:b, c:d] = y

    def reserve(self, r, margin=0.0):
        a, b, c, d = self._ix(r[0] - margin, r[1] - margin, r[2] + margin, r[3] + margin)
        self.reserved[a:b, c:d] = True

    def ground_at(self, x, z):
        ix = int(math.floor(x - self.minX)); iz = int(math.floor(z - self.minZ))
        ix = min(max(ix, 0), self.nx - 1); iz = min(max(iz, 0), self.nz - 1)
        return float(self.ground[iz, ix])

    # ---- primitives ---------------------------------------------------------------------------
    def block(self, bid, role, r, bottom, top, roof=None, reserve=True, margin=0.0, **meta):
        r = rect_of(*r)
        b = dict(id=bid, role=role, x0=r[0], z0=r[1], x1=r[2], z1=r[3], bottom=float(bottom), top=float(top))
        if roof: b['roof'] = roof
        b.update(meta)
        self.blocks.append(b)
        if reserve: self.reserve(r, margin)
        return b

    def ramp(self, rid, kind, r, toward, ylo, yhi, horde=True, route=None):
        """A MapRamp wedge on footprint r, rising toward `toward` from ylo to yhi. The ground under it is ylo."""
        r = rect_of(*r)
        x0, z0, x1, z1 = r
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        if toward in 'NS':
            lo = (cx, ylo, z0 if toward == 'N' else z1); hi = (cx, yhi, z1 if toward == 'N' else z0)
            width, run = x1 - x0, z1 - z0
        else:
            lo = (x0 if toward == 'E' else x1, ylo, cz); hi = (x1 if toward == 'E' else x0, yhi, cz)
            width, run = z1 - z0, x1 - x0
        rp = dict(id=rid, kind=kind, toward=toward, x0=x0, z0=z0, x1=x1, z1=z1, ylo=float(ylo), yhi=float(yhi),
                  low=dict(x=lo[0], y=lo[1], z=lo[2]), high=dict(x=hi[0], y=hi[1], z=hi[2]), width=width,
                  run=run, bottom=float(ylo) - 0.25, grade=(yhi - ylo) / run, horde=horde)
        if route: rp['route'] = route
        assert rp['grade'] < 1.0, rid
        self.ramps.append(rp)
        self.paint(r, ylo)
        self.reserve(r, 1.0)
        return rp

    def deck(self, did, r, y, thick=1.0, role='bridge', parapet=True, along=None, reserve=True, **meta):
        b = self.block(did, role, r, y - thick, y, roof='walkable', reserve=reserve, **meta)
        if parapet:
            x0, z0, x1, z1 = rect_of(*r)
            if along is None: along = 'NS' if (z1 - z0) >= (x1 - x0) else 'EW'
            if along == 'NS':
                self.block(did + '-parapet-w', 'parapet', (x0, z0, x0 + 0.5, z1), y, y + 1.1, reserve=False)
                self.block(did + '-parapet-e', 'parapet', (x1 - 0.5, z0, x1, z1), y, y + 1.1, reserve=False)
            else:
                self.block(did + '-parapet-s', 'parapet', (x0, z0, x1, z0 + 0.5), y, y + 1.1, reserve=False)
                self.block(did + '-parapet-n', 'parapet', (x0, z1 - 0.5, x1, z1), y, y + 1.1, reserve=False)
        return b

    def piers(self, pid, r, y_bottom, y_top, along, spacing=10.0, thick=2.0, depth=None):
        """Piers under a deck: slabs across the deck every `spacing` metres along it."""
        x0, z0, x1, z1 = rect_of(*r)
        out = []
        if along == 'EW':
            x = x0 + spacing / 2
            while x < x1:
                out.append(self.block('%s-%d' % (pid, len(out)), 'pier', (x - thick / 2, z0, x + thick / 2, z1),
                                      y_bottom, y_top, reserve=False))
                x += spacing
        else:
            z = z0 + spacing / 2
            while z < z1:
                out.append(self.block('%s-%d' % (pid, len(out)), 'pier', (x0, z - thick / 2, x1, z + thick / 2),
                                      y_bottom, y_top, reserve=False))
                z += spacing
        return out

    def arcade(self, aid, r, y, along, height=5.0, roof_thick=1.0, spacing=7.5, roof='walkable', sides='both'):
        """Posts on both long sides and a roof slab over the passage: a covered street with a walkable top."""
        x0, z0, x1, z1 = rect_of(*r)
        self.block(aid + '-roof', 'arcade-roof', r, y + height, y + height + roof_thick, roof=roof, reserve=False)
        n = 0
        if along == 'EW':
            x = x0 + 0.5
            while x <= x1 - 0.5:
                for side, (za, zb) in (('s', (z0, z0 + 1)), ('n', (z1 - 1, z1))):
                    if sides in ('both', side):
                        self.block('%s-post-%d' % (aid, n), 'post', (x - .5, za, x + .5, zb), y, y + height, reserve=False); n += 1
                x += spacing
        else:
            z = z0 + 0.5
            while z <= z1 - 0.5:
                for side, (xa, xb) in (('w', (x0, x0 + 1)), ('e', (x1 - 1, x1))):
                    if sides in ('both', side):
                        self.block('%s-post-%d' % (aid, n), 'post', (xa, z - .5, xb, z + .5), y, y + height, reserve=False); n += 1
                z += spacing

    def stairway(self, sid, x, z, toward, y0, y1, w=6.0):
        """Flights of 3.5 m rise and 3 m landings from (x, z) toward a side, climbing from y0 to y1 (y1 > y0)."""
        dx, dz = DIRS[toward]
        n = int(math.ceil((y1 - y0) / FLIGHT_RISE - 1e-9)); rise = (y1 - y0) / n; run = rise / STAIR_SLOPE
        y = y0
        def rr(a, b):
            if toward in 'NS': return rect_of(x - w / 2, a, x + w / 2, b)
            return rect_of(a, z - w / 2, b, z + w / 2)
        u = z if toward in 'NS' else x
        sg = dz if toward in 'NS' else dx
        for k in range(n):
            self.ramp('%s-%d' % (sid, k), 'stair', rr(u, u + sg * run), toward, y, y + rise)
            u += sg * run; y += rise
            if k < n - 1:
                self.paint(rr(u, u + sg * LANDING), y); self.reserve(rr(u, u + sg * LANDING), 1)
                u += sg * LANDING
        return u

    def rake(self, rid, x0, z0, x1, z1, along, car=15.0, gap=1.5, h=3.2):
        """A line of parked wagons (low cover), skipping any route corridor."""
        corr = [r for rt in self.routes for (r, m) in rt.corridors]
        u = x0 if along == 'EW' else z0
        end = x1 if along == 'EW' else z1
        k = 0
        while u + car <= end:
            r = (u, z0, u + car, z1) if along == 'EW' else (x0, u, x1, u + car)
            if not any(r[0] < c[2] + 2 and r[2] > c[0] - 2 and r[1] < c[3] + 2 and r[3] > c[1] - 2 for c in corr):
                gy = self.ground_at((r[0] + r[2]) / 2, (r[1] + r[3]) / 2)
                self.block('%s-%d' % (rid, k), 'wagon', r, gy, gy + h, roof='walkable', reserve=True, margin=0.5); k += 1
            u += car + gap

    def skybridges(self, style='market', max_gap=6.0, width=3.0, limit=40):
        """Footbridges between walkable roofs of one height across a narrow alley."""
        lots = [b for b in self.blocks if b['role'] == 'lot' and b.get('style') == style and b.get('roof') == 'walkable']
        made = 0
        for i, a in enumerate(lots):
            for b in lots[i + 1:]:
                if made >= limit: return made
                if abs(a['top'] - b['top']) > 0.01: continue
                ox0, ox1 = max(a['x0'], b['x0']), min(a['x1'], b['x1'])
                oz0, oz1 = max(a['z0'], b['z0']), min(a['z1'], b['z1'])
                if ox1 - ox0 >= 8 and 0 < (max(a['z0'], b['z0']) - min(a['z1'], b['z1'])) <= max_gap:
                    zlo, zhi = min(a['z1'], b['z1']), max(a['z0'], b['z0']); xm = (ox0 + ox1) / 2
                    self.deck('skybridge-%d' % made, (xm - width / 2, zlo, xm + width / 2, zhi), a['top'], thick=0.6, along='NS', reserve=False); made += 1
                elif oz1 - oz0 >= 8 and 0 < (max(a['x0'], b['x0']) - min(a['x1'], b['x1'])) <= max_gap:
                    xlo, xhi = min(a['x1'], b['x1']), max(a['x0'], b['x0']); zm = (oz0 + oz1) / 2
                    self.deck('skybridge-%d' % made, (xlo, zm - width / 2, xhi, zm + width / 2), a['top'], thick=0.6, along='EW', reserve=False); made += 1
        return made

    def wall(self, wid, r, bottom, top, role='wall'):
        return self.block(wid, role, r, bottom, top, reserve=True)

    def link(self, kind, a, b, width=3.0, horde=False, note='', blocks=None):
        self.links.append(dict(id='%s-%d' % (kind, len(self.links)), kind=kind, a=dict(x=a[0], y=a[1], z=a[2]),
                               b=dict(x=b[0], y=b[1], z=b[2]), width=width, horde=horde, note=note))

    def landmark(self, lid, name, x, z, y, kind, note=''):
        self.landmarks.append(dict(id=lid, name=name, x=x, z=z, y=y, kind=kind, note=note))

    def hub(self, name, rects, y=0.0):
        self.hub_name = name
        for r in rects:
            r = rect_of(*r)
            self.hub_rects.append(r)
            self.paint(r, y); self.reserve(r, 0)
        self.paint((-25, -25, 25, 25), y); self.reserve((-25, -25, 25, 25), 3)

    def route(self, rid, label, gate, dest, color):
        x, z = GATES[gate]
        r = Route(self, rid, label, gate, dest, color, x, z, 0.0, gate)
        self.routes.append(r)
        return r

    def door(self, did, name, x, z, y, facing, level, plaza=30.0, hall_w=40.0, hall_d=22.0, hall_h=16.0, note=''):
        """An instance hall. `facing` is the way the door looks (toward its approach). The hall stands behind it."""
        dx, dz = DIRS[facing]
        bx, bz = -dx, -dz      # into the hall
        if facing in 'NS':
            hall = rect_of(x - hall_w / 2, z, x + hall_w / 2, z + bz * hall_d)
            pl = rect_of(x - hall_w / 2 - 5, z, x + hall_w / 2 + 5, z + dz * plaza)
        else:
            hall = rect_of(x, z - hall_w / 2, x + bx * hall_d, z + hall_w / 2)
            pl = rect_of(x, z - hall_w / 2 - 5, x + dx * plaza, z + hall_w / 2 + 5)
        self.paint(pl, y); self.reserve(pl, 2)
        self.paint(hall, y)
        hb = self.block('hall-' + did, 'hall', hall, y, y + hall_h, roof='walkable', margin=2)
        # the hall mouth: 10 m wide, 9 m tall, recessed into the front face
        d = dict(id=did, name=name, x=x, y=y, z=z, facing=facing, level=level, width=10.0, height=9.0,
                 hall=hb['id'], plaza=dict(zip(('x0', 'z0', 'x1', 'z1'), pl)), note=note,
                 bleed=dict(threshold=30, warp=60, drift=110, rumour=160))
        # a fire escape on whichever side of the hall stands on the door's own level (as today's greybox does)
        for side in ((0, 1) if facing in 'NS' else (2, 3)):
            if facing in 'NS':
                fx = hall[2] + 1.5 if side == 0 else hall[0] - 1.5
                front, back = (hall[1], hall[3]) if facing == 'S' else (hall[3], hall[1])
                sgn = 1 if back > front else -1
                a, b = (fx, y, front + 3 * sgn), (fx, y + hall_h, back - 3 * sgn)
            else:
                fz = hall[3] + 1.5 if side == 2 else hall[1] - 1.5
                front, back = (hall[0], hall[2]) if facing == 'W' else (hall[2], hall[0])
                sgn = 1 if back > front else -1
                a, b = (front + 3 * sgn, y, fz), (back - 3 * sgn, y + hall_h, fz)
            if abs(self.ground_at(a[0], a[2]) - y) < 0.01 and abs(self.ground_at((a[0] + b[0]) / 2, (a[2] + b[2]) / 2) - y) < 0.01:
                self.link('fire-escape', a, b, 3, note='hall roof'); break
        self.doors.append(d)
        return d

    def city_hall(self, x, z, y, w=74.0, d=64.0, gate='S', name='City Hall court'):
        """The final court: a walled yard with a gateway, City Hall across the back and its 62 m tower."""
        r = rect_of(x - w / 2, z - d / 2, x + w / 2, z + d / 2)
        self.paint(rect_of(r[0] - 6, r[1] - 6, r[2] + 6, r[3] + 6), y)
        self.reserve(r, 6)
        x0, z0, x1, z1 = r
        t, wh = 1.5, 7.0
        gw = 12.0
        sides = {'S': (x0, z0, x1, z0 + t), 'N': (x0, z1 - t, x1, z1), 'W': (x0, z0, x0 + t, z1), 'E': (x1 - t, z0, x1, z1)}
        for s, rr in sides.items():
            if s == gate:
                if s in 'NS':
                    self.block('court-wall-%s-a' % s, 'court-wall', (rr[0], rr[1], x - gw / 2, rr[3]), y, y + wh, reserve=False)
                    self.block('court-wall-%s-b' % s, 'court-wall', (x + gw / 2, rr[1], rr[2], rr[3]), y, y + wh, reserve=False)
                    self.block('court-gate-lintel', 'court-gate', (x - gw / 2, rr[1] - 1, x + gw / 2, rr[3] + 1), y + 10, y + 12.5, reserve=False)
                    self.block('court-gate-pier-a', 'court-gate', (x - gw / 2 - 2, rr[1] - 1, x - gw / 2, rr[3] + 1), y, y + 12.5, reserve=False)
                    self.block('court-gate-pier-b', 'court-gate', (x + gw / 2, rr[1] - 1, x + gw / 2 + 2, rr[3] + 1), y, y + 12.5, reserve=False)
                else:
                    self.block('court-wall-%s-a' % s, 'court-wall', (rr[0], rr[1], rr[2], z - gw / 2), y, y + wh, reserve=False)
                    self.block('court-wall-%s-b' % s, 'court-wall', (rr[0], z + gw / 2, rr[2], rr[3]), y, y + wh, reserve=False)
                    self.block('court-gate-lintel', 'court-gate', (rr[0] - 1, z - gw / 2, rr[2] + 1, z + gw / 2), y + 10, y + 12.5, reserve=False)
                    self.block('court-gate-pier-a', 'court-gate', (rr[0] - 1, z - gw / 2 - 2, rr[2] + 1, z - gw / 2), y, y + 12.5, reserve=False)
                    self.block('court-gate-pier-b', 'court-gate', (rr[0] - 1, z + gw / 2, rr[2] + 1, z + gw / 2 + 2), y, y + 12.5, reserve=False)
            else:
                self.block('court-wall-' + s, 'court-wall', rr, y, y + wh, reserve=False)
        back = BACK[gate]
        hd = 24.0
        if back == 'N': hall = (x0 + 10, z1 - t - hd, x1 - 10, z1 - t)
        elif back == 'S': hall = (x0 + 10, z0 + t, x1 - 10, z0 + t + hd)
        elif back == 'E': hall = (x1 - t - hd, z0 + 10, x1 - t, z1 - 10)
        else: hall = (x0 + t, z0 + 10, x0 + t + hd, z1 - 10)
        self.block('city-hall', 'city-hall', hall, y, y + 19, roof='walkable', reserve=False)
        hx, hz = (hall[0] + hall[2]) / 2, (hall[1] + hall[3]) / 2
        self.block('city-hall-tower', 'tower', (hx - 6, hz - 6, hx + 6, hz + 6), y + 19, y + 62, reserve=False)
        gx, gz = {'S': (x, z0), 'N': (x, z1), 'W': (x0, z), 'E': (x1, z)}[gate]
        self.court = dict(name=name, x=x, y=y, z=z, x0=x0, z0=z0, x1=x1, z1=z1, gate=gate, gateway=dict(x=gx, y=y, z=gz, width=gw),
                          wall_height=wh, hall='city-hall', tower=dict(x=hx, z=hz, top=y + 62))
        self.landmark('city-hall', 'City Hall clock tower (6:47)', hx, hz, y + 62, 'tower', 'reads from the camp')

    def zone(self, zid, r, style, density=0.7, **kw):
        self.zones.append(dict(id=zid, rect=rect_of(*r), style=style, density=density, **kw))

    # ---- build ----------------------------------------------------------------------------------
    def finalize(self):
        for rp in self.ramps:                      # ramps win over any flat paint that ran across their ends
            self.paint((rp['x0'], rp['z0'], rp['x1'], rp['z1']), rp['ylo'])
        for r in self.routes: r.reserve_all()
        if getattr(self, 'pre_fill', None): self.pre_fill(self)
        self.fill_lots()
        for fn in getattr(self, 'post_fill', []): fn(self)
        self.ground_boxes = self.merge_ground()
        self.lowest = float(self.ground.min())
        if self.base_y is None: self.base_y = self.lowest - 2
        for g in self.ground_boxes: g['bottom'] = self.base_y
        self.killY = self.lowest - 6
        tops = [b['top'] for b in self.blocks if b['role'] not in ('tower',)]
        self.ceiling = max(60.0, math.ceil(max(tops + [float(self.ground.max())]) + 14))
        self.cordon()
        self.roof_links()
        for r in self.routes: r.summarize()

    def cordon(self):
        lo = self.lowest
        for s, rr in (('s', (self.minX, self.minZ, self.maxX, self.minZ + 1)), ('n', (self.minX, self.maxZ - 1, self.maxX, self.maxZ)),
                      ('w', (self.minX, self.minZ, self.minX + 1, self.maxZ)), ('e', (self.maxX - 1, self.minZ, self.maxX, self.maxZ))):
            self.block('cordon-' + s, 'cordon', rr, lo - 2, lo + 14, reserve=False)
        # the Cordon wall is 14 m over the ground it stands on; where the city is higher it tops out higher
        for b in self.blocks:
            if b['role'] == 'cordon':
                x0, z0, x1, z1 = b['x0'], b['z0'], b['x1'], b['z1']
                a, bb, c, d = self._ix(x0, z0, x1, z1)
                g = self.ground[max(0, a - 1):bb + 1, max(0, c - 1):d + 1]
                b['top'] = float(g.max()) + 14

    def merge_ground(self):
        """Greedy rectangles of equal height over the 1 m raster."""
        g = self.ground
        nz, nx = g.shape
        boxes = []
        active = {}
        for iz in range(nz + 1):
            runs = {}
            if iz < nz:
                row = g[iz]
                start = 0
                for ix in range(1, nx + 1):
                    if ix == nx or row[ix] != row[start]:
                        runs[(start, ix, float(row[start]))] = True
                        start = ix
            for key in list(active.keys()):
                if key not in runs:
                    s, e, y = key
                    boxes.append(dict(x0=self.minX + s, x1=self.minX + e, z0=self.minZ + active[key], z1=self.minZ + iz, top=y))
                    del active[key]
            for key in runs:
                if key not in active: active[key] = iz
        for i, b in enumerate(boxes):
            b['id'] = 'terrace-%d' % i
            b['role'] = 'ground'
        return boxes

    # ---- lot fill ---------------------------------------------------------------------------------
    STYLES = {
        # depth options, width options, heights (weights), roof rule, alley
        'rows':       dict(depth=(10, 12), width=(20, 25, 30, 35, 40), heights=((7, 5), (10, 3)), roof='pitched', alley=5),
        'shops':      dict(depth=(15, 20), width=(15, 20), heights=((10, 2), (13, 5), (16, 1)), roof='mixed', alley=5),
        'tenements':  dict(depth=(15, 20), width=(20, 25, 30), heights=((16, 3), (19, 3), (22, 2)), roof='walkable', alley=7.5),
        'warehouses': dict(depth=(20, 25, 30), width=(30, 40, 50), heights=((10, 3), (13, 3), (16, 1)), roof='mixed', alley=7.5),
        'market':     dict(depth=(15, 20), width=(15, 20, 25), heights=((7, 6), (10, 2)), roof='walkable', alley=5),
        'sheds':      dict(depth=(20, 25), width=(50, 60, 80), heights=((10, 2), (13, 1)), roof='pitched', alley=10),
        'civic':      dict(depth=(20, 25), width=(25, 30), heights=((13, 2), (16, 2), (19, 1)), roof='walkable', alley=7.5),
        'villas':     dict(depth=(12, 15), width=(12, 15), heights=((7, 3), (10, 2)), roof='pitched', alley=7.5),
    }

    def _free(self, r, margin=1.0, lot_margin=None):
        a, b, c, d = self._ix(r[0] - margin, r[1] - margin, r[2] + margin, r[3] + margin)
        if b <= a or d <= c: return False
        if self.reserved[a:b, c:d].any(): return False
        lm = margin if lot_margin is None else lot_margin
        a1, b1, c1, d1 = self._ix(r[0] - lm, r[1] - lm, r[2] + lm, r[3] + lm)
        if self.lotmask[a1:b1, c1:d1].any(): return False
        a2, b2, c2, d2 = self._ix(*r)
        if (b2 - a2) * (d2 - c2) <= 0: return False
        if a2 < 2 or c2 < 2 or b2 > self.nz - 2 or d2 > self.nx - 2: return False
        g = self.ground[a2:b2, c2:d2]
        return float(g.min()) == float(g.max())

    PERIM = {
        # perimeter blocks: street walls round a courtyard. block sizes, street width, building depth, run lengths
        'rows':      dict(block=((40, 30), (50, 32), (60, 30)), street=10, depth=(10,), run=(15, 20, 25, 30), gap=0.12),
        'shops':     dict(block=((40, 40), (45, 35), (35, 35)), street=10, depth=(12, 15), run=(12, 15, 20), gap=0.15),
        'tenements': dict(block=((60, 45), (55, 50), (50, 45)), street=12, depth=(15,), run=(20, 25, 30), gap=0.15),
    }

    def _pick_height(self, rng, st, zn):
        hs = st['heights']; tot = sum(wt for _, wt in hs); pick = rng.random() * tot
        for h, wt in hs:
            pick -= wt
            if pick <= 0: break
        if zn.get('tall') and rng.random() < zn['tall']: h = rng.choice((28, 31, 34))
        return h

    def _place(self, rng, r, st, zn, n, margin=1.0, lot_margin=None):
        if not self._free(r, margin, lot_margin): return False
        h = self._pick_height(rng, st, zn)
        gy = self.ground_at((r[0] + r[2]) / 2, (r[1] + r[3]) / 2)
        roof = st['roof']
        if roof == 'mixed': roof = 'walkable' if rng.random() < 0.5 else 'pitched'
        self.block('lot-%s-%d' % (zn['id'], n), 'lot', r, gy, gy + h, roof=roof, reserve=False, style=zn['style'], district=zn['id'])
        a, b, c, d = self._ix(*r)
        self.lotmask[a:b, c:d] = True
        return True

    def fill_perimeter(self, zn, rng, n):
        st = dict(self.STYLES[zn['style']]); pm = self.PERIM[zn['style']]
        x0, z0, x1, z1 = zn['rect']
        bw, bd = rng.choice(pm['block'])
        z = z0 + rng.choice((0, 5))
        while z + 20 <= z1:
            x = x0 + rng.choice((0, 5, 10))
            while x + 20 <= x1:
                bx1, bz1 = min(x + bw, x1), min(z + bd, z1)
                d = rng.choice(pm['depth'])
                strips = [((x, bz1 - d), (bx1, bz1), 'EW'), ((x, z), (bx1, z + d), 'EW'),
                          ((x, z + d), (x + d, bz1 - d), 'NS'), ((bx1 - d, z + d), (bx1, bz1 - d), 'NS')]
                k_open = rng.randrange(4)          # every courtyard keeps a passage (a carriage arch's width or more)
                for k, (sa, sb, along) in enumerate(strips):
                    u0, u1 = (sa[0], sb[0]) if along == 'EW' else (sa[1], sb[1])
                    u = u0
                    mid = (u0 + u1) / 2
                    while u1 - u >= 8:
                        L = min(rng.choice(pm['run']), u1 - u)
                        if u1 - (u + L) < 8: L = u1 - u
                        r = (u, sa[1], u + L, sb[1]) if along == 'EW' else (sa[0], u, sb[0], u + L)
                        forced = k == k_open and u <= mid < u + L
                        if not forced and rng.random() > pm['gap'] and rng.random() < zn['density'] + 0.15:
                            if self._place(rng, r, st, zn, n, margin=1.0, lot_margin=0.0): n += 1
                        u += L
                x += bw + pm['street']
                bw, bd = rng.choice(pm['block'])
            z += bd + pm['street']
        return n

    def fill_lots(self):
        n = 0
        for zn in self.zones:
            if zn['style'] in self.PERIM and not zn.get('grid'):
                rng = random.Random(self.seed * 1000 + len(zn['id']) * 7 + n)
                n = self.fill_perimeter(zn, rng, n)
                continue
            st = dict(self.STYLES[zn['style']]); st.update(zn.get('override', {}))
            rng = random.Random(self.seed * 1000 + len(zn['id']) * 7 + n)
            x0, z0, x1, z1 = zn['rect']
            along = zn.get('along', 'EW' if (x1 - x0) >= (z1 - z0) else 'NS')
            u0, u1, v0, v1 = (x0, x1, z0, z1) if along == 'EW' else (z0, z1, x0, x1)
            v = v0
            while v < v1 - 6:
                depth = rng.choice(st['depth'])
                u = u0
                while u < u1 - 6:
                    w = rng.choice(st['width'])
                    w = min(w, u1 - u)
                    if w < 10: break
                    r = (u, v, u + w, v + depth) if along == 'EW' else (v, u, v + depth, u + w)
                    if v + depth <= v1 and rng.random() < zn['density'] and self._free(r):
                        hs = st['heights']; tot = sum(wt for _, wt in hs); pick = rng.random() * tot
                        for h, wt in hs:
                            pick -= wt
                            if pick <= 0: break
                        if zn.get('tall') and rng.random() < zn['tall']: h = rng.choice((28, 31, 34))
                        gy = self.ground_at((r[0] + r[2]) / 2, (r[1] + r[3]) / 2)
                        roof = st['roof']
                        if roof == 'mixed': roof = 'walkable' if rng.random() < 0.5 else 'pitched'
                        bid = 'lot-%s-%d' % (zn['id'], n)
                        self.block(bid, 'lot', r, gy, gy + h, roof=roof, reserve=False, style=zn['style'], district=zn['id'])
                        a, b, c, d = self._ix(*r)
                        self.lotmask[a:b, c:d] = True
                        n += 1
                    u += w + st['alley']
                v += depth + st['alley']
        self.lot_count = n

    def roof_links(self):
        """Step-on roofs (a row whose roof meets the next terrace), fire escapes on tall walkable roofs by a street."""
        for b in self.blocks:
            if b['role'] != 'lot': continue
            x0, z0, x1, z1, top = b['x0'], b['z0'], b['x1'], b['z1'], b['top']
            sides = {'N': (x0, z1 + 0.5, x1, z1 + 1.5), 'S': (x0, z0 - 1.5, x1, z0 - 0.5),
                     'E': (x1 + 0.5, z0, x1 + 1.5, z1), 'W': (x0 - 1.5, z0, x0 - 0.5, z1)}
            stepped = None
            for s, rr in sides.items():
                a, bb, c, d = self._ix(*rr)
                g = self.ground[a:bb, c:d]
                if g.size and (np.abs(g - top) < 0.01).sum() >= 5 * max(1, min(g.shape)):
                    stepped = s; break
            if stepped:
                b['roof'] = 'walkable'
                b['roof_link'] = 'step-' + stepped
                continue
            if b['roof'] == 'walkable' and top - b['bottom'] >= 10 and (zlib.crc32(b['id'].encode()) % 7) < 4 and b.get('style') in ('tenements', 'civic', 'shops', 'warehouses', 'market'):
                # fire escape on the longest face that stands on open street at the lot's base
                best = None
                for s, rr in sides.items():
                    a, bb, c, d = self._ix(*rr)
                    if bb <= a or d <= c: continue
                    g = self.ground[a:bb, c:d]; lm = self.lotmask[a:bb, c:d]
                    free = ((np.abs(g - b['bottom']) < 0.01) & ~lm).mean()
                    length = (x1 - x0) if s in 'NS' else (z1 - z0)
                    if free > 0.8 and (best is None or length > best[1]): best = (s, length)
                if best:
                    s = best[0]
                    if s in 'NS':
                        zz = z1 + 1.5 if s == 'N' else z0 - 1.5
                        a, bpt = (x0 + 2, b['bottom'], zz), (x1 - 2, top, zz)
                    else:
                        xx = x1 + 1.5 if s == 'E' else x0 - 1.5
                        a, bpt = (xx, b['bottom'], z0 + 2), (xx, top, z1 - 2)
                    self.link('fire-escape', a, bpt, 1.5, note=b['id'])
                    b['roof_link'] = 'fire-escape-' + s
        # the roof-hatch rule: every walkable roof without a horde connector gets a hatch spawn entry
        for b in self.blocks:
            if b.get('roof') == 'walkable' and b['role'] in ('lot', 'hall', 'arcade-roof', 'city-hall'):
                b['hatch'] = True

    # ---- reach check --------------------------------------------------------------------------------
    def solids(self):
        out = [dict(x0=g['x0'], z0=g['z0'], x1=g['x1'], z1=g['z1'], bottom=g['bottom'], top=g['top'], role='ground', id=g['id']) for g in self.ground_boxes]
        out += [b for b in self.blocks if b['role'] != 'cordon']
        return out

    def reach(self, cell=2.0):
        nx, nz = int(600 / cell), int(480 / cell)
        cols = [[[] for _ in range(nx)] for _ in range(nz)]

        def cells_of(x0, z0, x1, z1):
            i0 = int(math.ceil((x0 - self.minX) / cell - 0.5)); i1 = int(math.floor((x1 - self.minX) / cell - 0.5))
            j0 = int(math.ceil((z0 - self.minZ) / cell - 0.5)); j1 = int(math.floor((z1 - self.minZ) / cell - 0.5))
            return max(0, j0), min(nz - 1, j1), max(0, i0), min(nx - 1, i1)
        for s in self.solids():
            if s['top'] > self.ceiling: s = dict(s, top=s['top'])
            j0, j1, i0, i1 = cells_of(s['x0'], s['z0'], s['x1'], s['z1'])
            for j in range(j0, j1 + 1):
                for i in range(i0, i1 + 1):
                    cols[j][i].append((s['bottom'], s['top'], s['role'], s['id']))
        for rp in self.ramps:
            j0, j1, i0, i1 = cells_of(rp['x0'], rp['z0'], rp['x1'], rp['z1'])
            for j in range(j0, j1 + 1):
                for i in range(i0, i1 + 1):
                    x = self.minX + (i + 0.5) * cell; z = self.minZ + (j + 0.5) * cell
                    y = ramp_y(rp, x, z)
                    cols[j][i].append((rp['bottom'], y, 'ramp', rp['id']))
        surf = {}
        for j in range(nz):
            for i in range(nx):
                col = cols[j][i]
                ss = []
                for (b0, t0, role, sid) in col:
                    if t0 > self.ceiling - 0.5 or role in ('post',): continue
                    blocked = any(b1 < t0 + HEADROOM and t1 > t0 + 0.05 for (b1, t1, r1, i1) in col if i1 != sid)
                    if not blocked: ss.append((t0, role, sid))
                if ss: surf[(j, i)] = sorted(set(ss))
        self._cols, self._surf, self._cell = cols, surf, cell

        def landing(j, i, y):
            best = None
            for (t, role, sid) in surf.get((j, i), ()):
                if t <= y + 1.1:
                    # column above t up to y+HEADROOM must be clear
                    if any(b1 < y + HEADROOM and t1 > t + 0.05 for (b1, t1, r1, i1) in self._cols[j][i] if i1 != sid):
                        continue
                    if best is None or t > best[0]: best = (t, role, sid)
            return best
        nodes = {}
        for (j, i), ss in surf.items():
            for (t, role, sid) in ss: nodes[(j, i, round(t, 2))] = (role, sid)
        fwd = {k: [] for k in nodes}
        for (j, i, t) in nodes:
            for dj, di in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                jj, ii = j + dj, i + di
                if not (0 <= jj < nz and 0 <= ii < nx): continue
                lb = landing(jj, ii, t)
                if lb is not None:
                    fwd[(j, i, t)].append((jj, ii, round(lb[0], 2)))
                    if lb[0] < t - 1.0:   # a gap: a running jump clears about 4 m
                        for k in (2, 3):
                            j3, i3 = j + dj * k, i + di * k
                            if not (0 <= j3 < nz and 0 <= i3 < nx): break
                            l3 = landing(j3, i3, t)
                            if l3 is not None and l3[0] >= t - 1.5:
                                fwd[(j, i, t)].append((j3, i3, round(l3[0], 2))); break
        # fire escapes and other pilot links (both ways)
        def node_near(x, y, z):
            i = int((x - self.minX) / cell); j = int((z - self.minZ) / cell)
            best = None
            for dj in (-2, -1, 0, 1, 2):
                for di in (-2, -1, 0, 1, 2):
                    for (t, role, sid) in surf.get((j + dj, i + di), ()):
                        dd = abs(t - y) + 0.1 * (abs(dj) + abs(di))
                        if dd < 1.2 and (best is None or dd < best[0]): best = (dd, (j + dj, i + di, round(t, 2)))
            return best[1] if best else None
        self.link_fail = []
        for lk in self.links:
            a = node_near(lk['a']['x'], lk['a']['y'], lk['a']['z']); b = node_near(lk['b']['x'], lk['b']['y'], lk['b']['z'])
            if a and b and a in fwd and b in fwd:
                fwd[a].append(b); fwd[b].append(a)
            else:
                self.link_fail.append(lk['id'])
        rev = {k: [] for k in nodes}
        for a, bs in fwd.items():
            for b in bs:
                if b in rev: rev[b].append(a)
        hub = [k for k in nodes if abs(k[2]) < 0.01 and abs(self.minX + (k[1] + .5) * cell) < 20 and abs(self.minZ + (k[0] + .5) * cell) < 20]

        def bfs(adj, starts):
            seen = set(starts); q = deque(starts)
            while q:
                u = q.popleft()
                for v in adj.get(u, ()):
                    if v not in seen: seen.add(v); q.append(v)
            return seen
        from_hub = bfs(fwd, hub)
        to_hub = bfs(rev, hub)
        self._nodes, self._from_hub, self._to_hub = nodes, from_hub, to_hub
        trapped = [k for k in nodes if k not in to_hub]
        # walkable roofs reached on foot from the camp (no chips)
        roof_ids = {b['id'] for b in self.blocks if b.get('roof') == 'walkable'}
        reached_roofs = {nodes[k][1] for k in from_hub if nodes[k][1] in roof_ids}
        self.reach_report = dict(cells=len(nodes), trapped=len(trapped), trapped_sample=[
            (round(self.minX + (k[1] + .5) * cell, 1), k[2], round(self.minZ + (k[0] + .5) * cell, 1), nodes[k][1]) for k in trapped[:12]],
            unreached_from_hub=len([k for k in nodes if k not in from_hub]),
            walkable_roofs=len(roof_ids), roofs_on_foot=len(reached_roofs), link_failures=self.link_fail)
        self.roofs_on_foot = reached_roofs
        return self.reach_report

    def unpocket(self, rounds=12):
        """Open any courtyard the fill sealed: drop the smallest lot walling a pocket, until nothing is trapped."""
        removed = []
        for _ in range(rounds):
            rr = self.reach()
            if not rr['trapped']: break
            cells = [(self.minX + (k[1] + .5) * self._cell, k[2], self.minZ + (k[0] + .5) * self._cell)
                     for k in self._nodes if k not in self._to_hub]
            cand = []
            for b in self.blocks:
                if b['role'] != 'lot': continue
                near = sum(1 for (x, y, z) in cells if b['x0'] - 2.5 <= x <= b['x1'] + 2.5 and b['z0'] - 2.5 <= z <= b['z1'] + 2.5
                           and b['top'] > y + 1.1 and b['bottom'] <= y + 0.5)
                if near: cand.append(((b['x1'] - b['x0']) * (b['z1'] - b['z0']), b))
            if not cand: break
            cand.sort(key=lambda t: t[0])
            gone = cand[0][1]
            self.blocks.remove(gone); removed.append(gone['id'])
            self.links = [l for l in self.links if l.get('note') != gone['id']]
            self.lot_count -= 1
        self.unpocketed = removed
        return removed

    def route_checks(self):
        """Every 1 m of every route stands on a surface at the route's height, with pilot headroom."""
        bad = []
        sol = self.solids()
        for r in self.routes:
            for (x, z, y, st) in r.samples(1.0):
                tops = [s['top'] for s in sol if s['x0'] - 1e-6 <= x <= s['x1'] + 1e-6 and s['z0'] - 1e-6 <= z <= s['z1'] + 1e-6 and s['role'] not in ('parapet', 'post')]
                tops += [ramp_y(rp, x, z) for rp in self.ramps if rp['x0'] <= x <= rp['x1'] and rp['z0'] <= z <= rp['z1']]
                ok = any(abs(t - y) < 0.35 for t in tops)
                head = [s for s in sol if s['x0'] < x < s['x1'] and s['z0'] < z < s['z1'] and s['bottom'] < y + HORDE_HEADROOM and s['top'] > y + 0.4 and s['role'] not in ('parapet',)]
                if not ok or head:
                    bad.append((r.id, round(st, 1), round(x, 1), round(z, 1), round(y, 2), 'no floor' if not ok else 'headroom ' + head[0]['id']))
        return bad

    # ---- export ---------------------------------------------------------------------------------------
    def to_json(self):
        rnd = lambda v: round(v, 3) if isinstance(v, float) else v
        def clean(d): return {k: (rnd(v) if not isinstance(v, dict) else {kk: rnd(vv) for kk, vv in v.items()}) for k, v in d.items()}
        return dict(
            format='meridian-street-plan/1', id=self.id, name=self.name, title=self.title, size='B',
            axes='x east, z north, y up; metres; the camp at the origin on y 0',
            bounds=dict(minX=self.minX, maxX=self.maxX, minZ=self.minZ, maxZ=self.maxZ, ceiling=self.ceiling, killY=self.killY),
            tiers=self.tier_names, description=self.description,
            hub=dict(name=self.hub_name, y=0.0, camp=dict(x0=-25, z0=-25, x1=25, z1=25, fence=24.8, gate_width=10),
                     precinct=[dict(zip(('x0', 'z0', 'x1', 'z1'), r)) for r in self.hub_rects],
                     gates=[dict(id=g, x=GATES[g][0], z=GATES[g][1], route=next(r.id for r in self.routes if r.gate == g)) for g in 'NESW']),
            routes=[r.to_json() for r in self.routes],
            doors=[clean(d) for d in self.doors], court=clean(self.court) if self.court else None,
            ground=[clean(g) for g in self.ground_boxes],
            ramps=[clean(rp) for rp in self.ramps],
            blocks=[clean(b) for b in self.blocks],
            links=self.links, landmarks=self.landmarks,
            lamps=[[round(a, 2), round(b, 2), round(c, 2)] for a, b, c in self.lamps()],
            fill=dict(rule='explicit lots from the zones below (deterministic, seed %d); build the lots in blocks[] as listed' % self.seed,
                      zones=[dict(id=z['id'], style=z['style'], density=z['density'], rect=dict(zip(('x0', 'z0', 'x1', 'z1'), z['rect'])))
                             for z in self.zones], styles=self.STYLES),
            notes=self.notes,
            checks=dict(reach=getattr(self, 'reach_report', None), lots=self.lot_count,
                        blocks=len(self.blocks) + len(self.ground_boxes), ramps=len(self.ramps)))

    def lamps(self):
        out = []
        for r in self.routes:
            k = 0
            for (x, z, y, st) in r.samples(12.0):
                if st < 3: continue
                h, w = r.heading_at(st)
                dx, dz = DIRS[h]
                side = 1 if k % 2 == 0 else -1
                off = w / 2 - 0.6
                out.append((x + dz * off * side, y, z - dx * off * side))
                k += 1
        return out


def ramp_y(rp, x, z):
    t = rp['toward']
    if t == 'N': f = (z - rp['z0']) / (rp['z1'] - rp['z0'])
    elif t == 'S': f = (rp['z1'] - z) / (rp['z1'] - rp['z0'])
    elif t == 'E': f = (x - rp['x0']) / (rp['x1'] - rp['x0'])
    else: f = (rp['x1'] - x) / (rp['x1'] - rp['x0'])
    f = min(max(f, 0.0), 1.0)
    return rp['ylo'] + (rp['yhi'] - rp['ylo']) * f


class Route:
    """A turtle that lays a route from a gate: each call adds one segment, its geometry and its corridor."""

    def __init__(self, L, rid, label, gate, dest, color, x, z, y, heading):
        self.L, self.id, self.label, self.gate, self.dest, self.color = L, rid, label, gate, dest, color
        self.x, self.z, self.y, self.h = x, z, y, heading
        self.pts = [(x, z, y)]
        self.segs = []
        self.corridors = []

    def _corr(self, x0, z0, x1, z1, w, ext0=0.0, ext1=0.0):
        dx, dz = DIRS[self.h]
        xa, za = x0 - dx * ext0, z0 - dz * ext0
        xb, zb = x1 + dx * ext1, z1 + dz * ext1
        if self.h in 'NS': return rect_of(xa - w / 2, za, xb + w / 2, zb)
        return rect_of(xa, za - w / 2, xb, zb + w / 2)

    def _add(self, kind, length, w, cover, y0, y1, rect, margin=3.0, **kw):
        self.segs.append(dict(kind=kind, heading=self.h, length=length, width=w, cover=cover, y0=y0, y1=y1, **kw))
        self.corridors.append((rect, margin))

    def go(self, length, w, cover='open', paint=True, deck=False, deck_id=None, parapet=True, roof=False, lid=None, margin=3.0):
        dx, dz = DIRS[self.h]
        x1, z1 = self.x + dx * length, self.z + dz * length
        prev = self.segs[-1] if self.segs else None
        back = 0.0 if (prev and prev['kind'] in ('stair', 'ramp') and prev['heading'] == self.h) else w / 2
        r = self._corr(self.x, self.z, x1, z1, w, back, w / 2)
        if lid is not None:   # a roof over the passage: (bottom, top) above the floor
            rr = self._corr(self.x, self.z, x1, z1, w + 2)
            self.L.block('%s-lid-%d' % (self.id, len(self.segs)), 'lid', rr, self.y + lid[0], self.y + lid[1],
                         roof='walkable', reserve=False)
        if roof:
            rr = self._corr(self.x, self.z, x1, z1, w)
            gy = min(self.L.ground_at((rr[0] + rr[2]) / 2, (rr[1] + rr[3]) / 2), self.y - 4)
            self.L.block('%s-leads-%d' % (self.id, len(self.segs)), 'lot', rr, gy, self.y, roof='walkable',
                         style='market', district='leads', roof_link='route')
            r = rr
        elif deck:
            rr = self._corr(self.x, self.z, x1, z1, w)
            self.L.deck(deck_id or '%s-deck-%d' % (self.id, len(self.segs)), rr, self.y, parapet=parapet,
                        along='NS' if self.h in 'NS' else 'EW')
            r = rr
        elif paint:
            self.L.paint(r, self.y)
        self._add('bridge' if deck else 'roof' if roof else 'flat', length, w, cover, self.y, self.y, r, margin=margin)
        self.x, self.z = x1, z1
        self.pts.append((x1, z1, self.y))
        return self

    def slope(self, dy, run, w, cover='open', kind='ramp'):
        dx, dz = DIRS[self.h]
        x1, z1 = self.x + dx * run, self.z + dz * run
        r = self._corr(self.x, self.z, x1, z1, w)
        y0, y1 = self.y, self.y + dy
        toward = self.h if dy > 0 else BACK[self.h]
        self.L.ramp('%s-%s-%d' % (self.id, kind, len(self.segs)), kind, r, toward, min(y0, y1), max(y0, y1), route=self.id)
        self._add(kind, run, w, cover, y0, y1, r)
        self.x, self.z, self.y = x1, z1, y1
        self.pts.append((x1, z1, y1))
        return self

    def stairs(self, dy, w, cover='open', landing=LANDING):
        n = int(math.ceil(abs(dy) / FLIGHT_RISE - 1e-9))
        rise = dy / n
        for k in range(n):
            self.slope(rise, abs(rise) / STAIR_SLOPE, w, cover, kind='stair')
            if k < n - 1: self.go(landing, w, cover)
        return self

    def turn(self, side):
        self.h = LEFT[self.h] if side == 'L' else RIGHT[self.h] if side == 'R' else BACK[self.h]
        return self

    def reserve_all(self):
        for r, m in self.corridors: self.L.reserve(r, m)

    # ---- sampling ---------------------------------------------------------------------------------
    def samples(self, step):
        out = []
        st = 0.0
        for a, b in zip(self.pts, self.pts[1:]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            if L <= 0: continue
            n = max(1, int(round(L / step)))
            for k in range(n):
                f = k / n
                out.append((a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f, st + L * f))
            st += L
        last = self.pts[-1]
        out.append((last[0], last[1], last[2], st))
        return out

    @property
    def length(self):
        return sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(self.pts, self.pts[1:]))

    def heading_at(self, st):
        s = 0.0
        for seg in self.segs:
            if st <= s + seg['length'] + 1e-6: return seg['heading'], seg['width']
            s += seg['length']
        return self.segs[-1]['heading'], self.segs[-1]['width']

    def profile(self, upto=None, step=1.0):
        return [(st, y) for (x, z, y, st) in self.samples(step) if upto is None or st <= upto + 1e-6]

    def summarize(self, span=60.0):
        """What the first 60 m from the gate do: turns, level change, widths, cover, approach kinds."""
        s, turns, widths, covers, kinds = 0.0, [], [], [], []
        prev = self.segs[0]['heading']
        for seg in self.segs:
            if s >= span: break
            if seg['heading'] != prev:
                t = 'L' if LEFT[prev] == seg['heading'] else 'R' if RIGHT[prev] == seg['heading'] else 'U'
                turns.append((round(s), t))
                prev = seg['heading']
            widths.append(seg['width']); covers.append(seg['cover']); kinds.append(seg['kind'])
            s += seg['length']
        prof = self.profile(span, 2.0)
        ys = [y for _, y in prof]
        self.exit = dict(turns=turns, width_min=min(widths), width_max=max(widths), covers=sorted(set(covers)),
                         kinds=[k for i, k in enumerate(kinds) if i == 0 or kinds[i - 1] != k],
                         dy60=round(ys[-1] - ys[0], 2), ymin=round(min(ys), 2), ymax=round(max(ys), 2),
                         heading_first=self.segs[0]['heading'], profile=[round(y, 2) for y in ys])
        return self.exit

    def to_json(self):
        return dict(id=self.id, label=self.label, gate=self.gate, to=self.dest, color=self.color,
                    length=round(self.length, 1),
                    points=[[round(x, 2), round(y, 2), round(z, 2)] for (x, z, y) in self.pts],
                    segments=[{k: (round(v, 3) if isinstance(v, float) else v) for k, v in s.items()} for s in self.segs],
                    exit60=self.exit)
