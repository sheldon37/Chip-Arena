"""Meridian's lamp families (art bible 6.2): the Type 6 cobra-head sodium post (single, twin; working, flickering, dead,
route), the Swan-neck wall bracket and its variants (caged bulkhead, fluorescent wallpack, flush pool lamp), the Span pendant
(and the Level 0 troffer, the camp's bulb string, the span wire), the Lamplighter (single, triple, wall lantern) and the
Civil Defence flood mast. Every lamp's glowing glass is its own part ('lamp') so the runtime sets flicker and death per draw
(_StageGlow); each carries its light markers (the light field's pool, or a practical slot).

Frames: see mkit.py. A post's origin is the middle of its lamp- block on the pavement, its arm reaching toward +z (the road).
"""
import math

import mkit as mk
from mkit import F, LOD, Fr, Module, add, scale, norm, rot_y, lathe, sweep
import stagekit as sk

S6 = math.radians(8.0)   # the cobra arm's rise


# ---------------------------------------------------------------------------------------------------------------------------
# Sodium post: the Type 6 cobra head
# ---------------------------------------------------------------------------------------------------------------------------
def _shroud(g, T, number=None, lean=False):
    """The cast-iron base shroud: octagonal, 0.46 m across its foot, 1.1 m tall, a door panel and a lamp number plate."""
    green = F['iron-green']
    n = T.pick(8, 6)
    k = 1 / math.cos(math.pi / n)
    if T.lod == 0:
        prof = [(.23, 0.0), (.23, .055), (.2, .085), (.17, .21), (.162, .84), (.184, .875), (.184, .95), (.15, .99),
                (.112, 1.05), (.1, 1.1)]
        if lean:
            prof = [p for p in prof if p[1] not in (.085, 1.05)]
    else:
        prof = [(.2, 0.0), (.1, 1.1)] if lean else [(.215, 0.0), (.168, .94), (.09, 1.1)]
    lathe(g, [(r * k, y) for r, y in prof], (0, 0, 0), green, n, smooth=False, phase=math.pi / n)
    if T.lod > 0:
        return
    fr = Fr((0, 0, 0), (0, 0, 1))
    # the door panel, its keyhole and hinges, on the flat that faces the road
    mk.fbox(g, None, fr, -.068, .068, .3, .7, .158, .176, green, 0.0, skip=('-z',))
    for b in (.36, .64):
        mk.fbox(g, None, fr, -.076, -.062, b - .025, b + .025, .17, .184, green, 0.0, skip=('-z',))
    # the lamp number plate under the capital ring: green enamel, cream digits
    if number:
        mk.fbox(g, None, fr, -.07, .07, .74, .82, .158, .17, green, 0.0, skip=('-z',))
        mk.text(g, fr, 0.0, .752, .008, number, F['enamel-cream'], c=.176)


def _shaft(g, T, y0, y1, collars=(3.0,)):
    """The galvanised octagonal shaft, tapering 0.20 m to 0.11 m."""
    n = T.pick(8, 6)
    k = 1 / math.cos(math.pi / n)

    def r(y):
        return .1 + (.055 - .1) * (y - y0) / (y1 - y0)
    ys = [y0] + ([c for c in collars if y0 < c < y1] if T.lod == 0 else []) + [y1]
    lathe(g, [(r(y) * k, y) for y in ys], (0, 0, 0), F['galv'], n, smooth=False, phase=math.pi / n)
    if T.lod == 0:
        for c in collars:
            if y0 < c < y1:
                rc = r(c) * k
                lathe(g, [(rc, c - .045), (rc + .014, c - .03), (rc + .014, c + .03), (rc, c + .045)], (0, 0, 0), F['galv'], n,
                      smooth=False, phase=math.pi / n)
    return r


def _cobra_head(body, lamp, T, base, yaw, glass, dead=False, lean=False):
    """A 0.75 m cobra head on the arm's end `base`, along the arm (8 deg up) turned by yaw: the cast housing, its sagging
    glass bowl (the lamp part) and the photocell that never saw dawn (lean: the twin's lighter head, its photocell on the
    post). Returns the bowl's centre."""
    fwd = rot_y((0.0, math.sin(S6), math.cos(S6)), yaw)
    up = rot_y((0.0, math.cos(S6), -math.sin(S6)), yaw)
    side = norm(mk.cross(up, fwd))

    def P(s, t, u):
        return add(add(add(base, fwd, s), side, t), up, u)
    if T.lod == 0:
        prof = [(-.02, .05, .052, .05), (.05, .085, .08, .062), (.15, .14, .104, .066), (.3, .165, .114, .066),
                (.46, .165, .108, .066), (.6, .14, .09, .062), (.7, .095, .064, .055), (.75, .035, .028, .03)]
        n, pw = (8 if lean else 10), 2.6
    else:
        prof = [(-.02, .05, .052, .05), (.3, .165, .114, .066), (.75, .035, .028, .03)]
        n, pw = (5 if lean else 6), 2.4
    rings = []
    for (s, w, top, bot) in prof:
        ring = []
        for j in range(n):
            a = 2 * math.pi * j / n + (math.pi / n if n % 2 else 0.0)
            ca, sa = math.cos(a), math.sin(a)
            t = w * math.copysign(abs(ca) ** (2 / pw), ca)
            u = (top if sa > 0 else bot) * math.copysign(abs(sa) ** (2 / pw), sa)
            ring.append(P(s, t, u))
        rings.append(ring)
    mk.loft(body, rings, F['alu'], close0=T.lod == 0, close1=T.lod == 0 or not lean)
    # the bowl: a shallow elliptic dome hanging under the housing's middle
    c = P(.41, 0.0, -.062)
    down = scale(up, -1)
    fglass = F['glass-dead'] if dead else glass
    start = lamp.faces
    sides = T.pick(8 if lean else 10, 5 if lean else 6)
    mk.ellipsoid(lamp, c, (fwd, side, down), (.27, .125, .085), fglass, sides, T.pick(3, 1), top=1.0, bottom=0.0)
    if dead and T.lod == 0:
        # the bowl cracked: a shard gone (dark) and two crazed panes
        for k_, f in ((3, F['void']), (4, F['void']), (13, F['void'])):
            if start + k_ < lamp.faces:
                lamp.fin[start + k_] = f
    if T.lod == 0:
        # the bezel ring the bowl hangs from
        nb = 10 if lean else 12
        rim = [P(.41 + .275 * math.cos(2 * math.pi * j / nb), .13 * math.sin(2 * math.pi * j / nb), -.06) for j in range(nb)]
        rim2 = [P(.41 + .285 * math.cos(2 * math.pi * j / nb), .138 * math.sin(2 * math.pi * j / nb), -.072) for j in range(nb)]
        mk.loft(body, [rim, rim2], F['alu'])
        if not lean:
            _photocell(body, P(.13, 0.0, .1), (fwd, side, up))
    return add(c, down, .05)


def _photocell(g, at, axes):
    fwd, side, up = axes
    mk.rod(g, at, add(at, up, .05), .032, F['alu'], 6, caps=False)
    mk.ellipsoid(g, add(at, up, .05), (fwd, side, up), (.034, .034, .026), F['alu'], 6, 2, top=1.0, bottom=0.0)


def _arm(g, T, top, yaw, reach=1.4, bend=.42, nb=None):
    """The arm: out of the shaft's top in a bend, then `reach` metres rising at 8 deg. Returns its end."""
    th1 = math.pi / 2 - S6
    nb = nb or T.pick(5, 1)
    pts = []
    for i in range(nb + 1):
        th = th1 * i / nb
        pts.append(add(top, rot_y((0.0, bend * math.sin(th), bend * (1 - math.cos(th))), yaw)))
    end = add(pts[-1], rot_y((0.0, math.sin(S6), math.cos(S6)), yaw), reach)
    pts.append(end)
    mk.tube(g, pts, .05, F['galv'], T.pick(6, 4), caps=False, radii=[.05 - .008 * i / (len(pts) - 1) for i in range(len(pts))])
    return end


def sodium(T, twin=False, state='working'):
    """The Type 6 cobra head. state: working, flicker, dead, route."""
    mk.finishes()
    name = 'lamp.sodium.' + ('twin' if twin else 'single') + ('' if state == 'working' else '-' + state)
    m = Module(name, T.lod, block='lamp- (0.3 m)', cap=(900, 110) if twin else (700, 90),
               note='head %.1f m%s' % (8.0 if twin else 6.5, '; twin arm for avenues' if twin else ''))
    body, lamp = m.part('body'), m.part('lamp')
    glass = mk.glow(mk.ROUTE_GLASS if state == 'route' else mk.SODIUM_GLASS, 3.0)
    _shroud(body, T, number=('C 47' if twin else '214'), lean=twin or state == 'route')
    top_y = 7.38 if twin else 5.9
    _shaft(body, T, 1.05, top_y, collars=() if state == 'route' else (3.0,))
    n = T.pick(8, 6)
    k = 1 / math.cos(math.pi / n)
    if T.lod == 0:
        # the shaft's top collar where the arm leaves it (the twin's carries the post's photocell)
        lathe(body, [(.055 * k, top_y - .07), (.07 * k, top_y - .05), (.07 * k, top_y + .03), (.052 * k, top_y + .07)],
              (0, 0, 0), F['galv'], n, smooth=False, phase=math.pi / n, cap1=True)
        if twin:
            _photocell(body, (0.0, top_y + .07, 0.0), ((0, 0, 1), (1, 0, 0), (0, 1, 0)))
    yaws = (0.0, math.pi) if twin else (0.0,)
    for yaw in yaws:
        end = _arm(body, T, (0.0, top_y, 0.0), yaw, nb=(T.pick(4, 1) if twin else None))
        bowl = _cobra_head(body, lamp, T, end, yaw, glass, dead=state == 'dead', lean=twin)
        if state != 'dead':
            m.light(bowl, mk.ROUTE_LAMP if state == 'route' else mk.SODIUM_LAMP, 13.0, circuit=1 if state == 'flicker' else 0,
                    note='flickers (circuit 1)' if state == 'flicker' else ('the route home' if state == 'route' else ''))
    if state == 'route':
        _rag(body, T, 1.6, .1 + (.055 - .1) * (1.6 - 1.05) / (top_y - 1.05))
    return m


def _rag(g, T, y, r_shaft):
    """A white rag tied flat round the shaft at y (the route home)."""
    n = T.pick(8, 6)
    k = 1 / math.cos(math.pi / n)
    r = r_shaft * k
    if T.lod == 0:
        lathe(g, [(r + .004, y - .07), (r + .015, y - .055), (r + .015, y + .05), (r + .004, y + .065)], (0, 0, 0), F['rag'], n,
              smooth=False, phase=math.pi / n)
        fr = Fr((0, 0, 0), (0, 0, 1))
        d = r_shaft + .012
        mk.fbox(g, None, fr, -.035, .035, y - .03, y + .035, d, d + .035, F['rag'], 0.0, skip=('-z',))
        for (a0, a1, tilt) in ((-.03, .0, -.04), (.005, .035, .05)):
            mk.poly(g, fr, [(a0, y - .03), (a1, y - .03), (a1 + tilt, y - .2), (a0 + tilt, y - .19)], d + .03, F['rag'])
    else:
        lathe(g, [(r + .01, y - .07), (r + .01, y + .065)], (0, 0, 0), F['rag'], n, smooth=False, phase=math.pi / n)


# ---------------------------------------------------------------------------------------------------------------------------
# Wall bracket: the Swan-neck and its variants (origin on the wall's face at the mount point, +z out of the wall)
# ---------------------------------------------------------------------------------------------------------------------------
def _stadium(r, h, n):
    """A convex stadium outline (half width r, straight half height h) in (a, b), n points per end."""
    pts = []
    for i in range(n + 1):
        t = math.pi * i / n
        pts.append((r * math.cos(t), h + r * math.sin(t)))
    for i in range(n + 1):
        t = math.pi + math.pi * i / n
        pts.append((r * math.cos(t), -h + r * math.sin(t)))
    return pts


def _plate(g, T, fr, outline, c0, c1, f):
    """A cast plate on the wall: a convex outline [(a, b)] from depth c0 to c1 (its face and edges)."""
    n = len(outline)
    cen = (sum(p[0] for p in outline) / n, sum(p[1] for p in outline) / n)
    for i in range(n):
        p, q = outline[i], outline[(i + 1) % n]
        out = fr.D((p[0] + q[0]) / 2 - cen[0], (p[1] + q[1]) / 2 - cen[1], 0.0)
        g.face((fr.P(p[0], p[1], c0), fr.P(q[0], q[1], c0), fr.P(q[0], q[1], c1), fr.P(p[0], p[1], c1)), f, out)
    g.face([fr.P(a, b, c1) for (a, b) in outline], f, fr.n)


def swan_neck(T):
    mk.finishes()
    m = Module('lamp.wall.swan-neck', T.lod, mount='wall', rest=(0.0, 4.3, 0.0), block='a facade of 9 m or more (plate at 4.0-4.6 m)',
               cap=(450, 60), note='arm 0.82 m from the wall')
    body, lamp = m.part('body'), m.part('lamp')
    green = F['iron-green']
    fr = Fr((0, 0, 0), (0, 0, 1))
    if T.lod == 0:
        _plate(body, T, fr, _stadium(.085, .17, 5), 0.0, .03, green)
        for b in (.2, -.2):
            mk.rod(body, fr.P(0, b, .03), fr.P(0, b, .045), .016, F['iron-black'], 4, caps=False)
    else:
        mk.fbox(body, T, fr, -.085, .085, -.25, .25, 0.0, .03, green, skip=('-z',))
    # the swan neck: out of the plate, up and over, down to the shade
    ctrl = [(0.03, -0.05), (0.17, -0.03), (0.33, 0.09), (0.47, 0.27), (0.63, 0.33), (0.77, 0.25), (0.82, 0.08), (0.82, -0.02)]
    path = mk.catmull([(0.0, y, z) for (z, y) in ctrl], T.pick(2, 1))
    if T.lod > 0:
        path = [path[0], path[3], path[5], path[-1]]
    mk.tube(body, path, .032, green, T.pick(5, 4), caps=T.lod == 0, radii=[.032 - .008 * i / (len(path) - 1) for i in range(len(path))])
    if T.lod == 0:
        # the scroll brace under it, curling at its foot
        sc = mk.catmull([(0.0, -.17, .03), (0.0, -.14, .13), (0.0, -.07, .22), (0.0, .02, .29), (0.0, .07, .34)], 2)
        mk.bar(body, sc, .016, .03, green, up=(1, 0, 0))
        mk.ellipsoid(body, (0.0, .33, .64), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), (.03, .03, .03), F['brass'], 6, 2, top=1.0, bottom=-1.0)
    # the dish shade (enamel outside, the white reflector inside, faintly lit) over the globe
    sx = (0.0, -0.02, .82)
    n = T.pick(10, 6)
    lathe(body, [(.24, -.16), (.215, -.145), (.05, -.07), (.025, -.05)] if T.lod == 0 else [(.24, -.16), (.03, -.05)], sx, F['enamel-green'], n)
    if T.lod == 0:
        lathe(body, [(.24, -.16), (.232, -.172)], sx, F['enamel-green'], n)
    if T.lod == 0:
        lathe(lamp, [(0.0, -.088), (.232, -.172)], sx, mk.glow(0xFFE2B8, 1.2), n)
        mk.rod(body, add(sx, (0, -.09, 0)), add(sx, (0, -.13, 0)), .025, F['iron-black'], 6, caps=False)
    else:
        mk.disc(lamp, add(sx, (0, -.155, 0)), .23, mk.glow(0xFFE2B8, 1.2), 6, (0, -1, 0))
    g_c = add(sx, (0, -.19, 0))
    mk.ellipsoid(lamp, g_c, ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.062, .062, .075), mk.glow(mk.SODIUM_GLASS, 3.0), T.pick(8, 5),
                 T.pick(4, 2), top=1.0, bottom=-1.0)
    m.light(g_c, mk.SODIUM_LAMP, 9.0)
    return m


def bulkhead(T):
    """The caged bulkhead (the Ironworks, warehouses): a cast oval on the wall, a glass dome behind a cage."""
    mk.finishes()
    m = Module('lamp.wall.bulkhead', T.lod, mount='wall', rest=(0.0, 3.6, 0.0), block='a facade (over 3.1 m)', cap=(450, 60))
    body, lamp = m.part('body'), m.part('lamp')
    fr = Fr((0, 0, 0), (0, 0, 1))
    iron = F['iron-raw']
    ol = [(b, a) for (a, b) in _stadium(.11, .07, T.pick(5, 2))]
    _plate(body, T, fr, ol, 0.0, .045, iron)
    mk.ellipsoid(lamp, (0, 0, .045), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.15, .085, .075), mk.glow(0xFF9A30, 2.6), T.pick(10, 6),
                 T.pick(3, 1), top=1.0, bottom=0.0)
    if T.lod == 0:
        # the cage: three hoops over the dome and one across them, and the screws
        for yk in (-.05, 0.0, .05):
            q = math.sqrt(max(0.0, 1 - (yk / .095) ** 2))
            pts = [(.165 * math.cos(a), yk, .045 + .088 * q * math.sin(a)) for a in (math.pi * i / 8 for i in range(9))]
            mk.tube(body, pts, .011, F['iron-black'], 4, caps=False)
        pts = [(0.0, .1 * math.cos(a), .045 + .09 * math.sin(a)) for a in (math.pi * i / 6 for i in range(7))]
        mk.tube(body, pts, .011, F['iron-black'], 4, caps=False)
        for a in (-.19, .19):
            mk.rod(body, fr.P(a, 0, .045), fr.P(a, 0, .055), .013, F['iron-black'], 6)
    m.light((0, 0, .1), 0xFF9A30, 9.0, note='a caged work lamp (the Ironworks colour)')
    return m


def wallpack(T):
    """The fluorescent wallpack (Threshold Business Park): a sloped box with a prismatic lens."""
    mk.finishes()
    m = Module('lamp.wall.wallpack', T.lod, mount='wall', rest=(0.0, 3.5, 0.0), block='a facade (over 3.1 m)', cap=(450, 60))
    body, lamp = m.part('body'), m.part('lamp')
    sec = [(0.0, -.12), (.17, -.12), (.17, .0), (.1, .12), (0.0, .12)]   # (z, y)
    a0, a1 = -.21, .21
    cz, cy = .08, 0.0
    for i in range(len(sec)):
        p, q = sec[i], sec[(i + 1) % len(sec)]
        if p[0] == 0.0 and q[0] == 0.0:
            continue
        dz, dy = q[0] - p[0], q[1] - p[1]
        nz, ny = dy, -dz
        if nz * ((p[0] + q[0]) / 2 - cz) + ny * ((p[1] + q[1]) / 2 - cy) < 0:
            nz, ny = -nz, -ny
        body.face(((a0, p[1], p[0]), (a1, p[1], p[0]), (a1, q[1], q[0]), (a0, q[1], q[0])), F['alu'], (0.0, ny, nz))
    for x, nx in ((a0, -1), (a1, 1)):
        body.face([(x, y, z) for (z, y) in sec], F['alu'], (nx, 0, 0))
    if T.lod == 0:
        # the back plate it is screwed to, and the drip lip over its lens
        fb = Fr((0, 0, 0), (0, 0, 1))
        mk.fbox(body, None, fb, -.25, .25, -.15, .15, 0.0, .012, F['alu'], 0.0, skip=('-z',))
        for (a_, b_) in ((-.225, -.125), (.225, -.125), (-.225, .125), (.225, .125)):
            mk.rod(body, fb.P(a_, b_, .012), fb.P(a_, b_, .02), .011, F['iron-black'], 6)
        mk.box(body, None, -.215, .215, -.005, .008, .17, .2, F['alu'], 0.0)
    fr = Fr((0, 0, .17), (0, 0, 1))
    mk.quad(lamp, fr, -.19, .19, -.105, -.015, .006, mk.glow(mk.TROFFER, 2.4))
    if T.lod == 0:
        for b in (-.08, -.05):
            mk.fbox(body, T, fr, -.19, .19, b - .004, b + .004, .006, .014, F['reflector'], 0.0, skip=('-z',))
        frs = Fr((0, .06, .135), (0, .7, .7))
        for i in range(5):
            a = -.14 + .07 * i
            mk.quad(body, frs, a - .022, a + .022, -.02, .02, .004, F['void'])
    m.light((0, -.1, .25), mk.TROFFER, 9.0, circuit=2, note='Level 0 drift (district circuit)')
    return m


def pool_flush(T):
    """The flush pool lamp, set low in the wall within 0.25 m (the Poolrooms' warp)."""
    mk.finishes()
    m = Module('lamp.wall.pool-flush', T.lod, mount='wall', rest=(0.0, .5, 0.0), block='a wall, low (0.25 m rule)', cap=(450, 60))
    body, lamp = m.part('body'), m.part('lamp')
    n = T.pick(16, 8)
    ax = (0.0, 0.0, 1.0)
    lathe(body, [(.18, 0.0), (.18, .008), (.165, .02), (.14, .022)] if T.lod == 0 else [(.18, 0.0), (.14, .02)], (0, 0, 0), F['chrome'], n, axis=ax)
    lathe(lamp, [(.14, .016), (.1, .022), (.0, .026)], (0, 0, 0), mk.glow(mk.POOL_AQUA, 3.0), n, axis=ax)
    if T.lod == 0:
        fr = Fr((0, 0, 0), ax)
        for i in range(6):
            a = 2 * math.pi * i / 6
            mk.disc(body, fr.P(.158 * math.cos(a), .158 * math.sin(a), .0215), .008, F['iron-black'], 5, ax)
    m.light((0, 0, .15), mk.POOL_AQUA, 6.0, circuit=2, note='Poolrooms warp (district circuit)')
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Hanging: the Span pendant (origin at the suspension point on the wires)
# ---------------------------------------------------------------------------------------------------------------------------
def pendant(T):
    mk.finishes()
    m = Module('lamp.span.pendant', T.lod, mount='span', rest=(0.0, 7.1, 0.0), block='span wires between facades (lamp at 6.5 m)',
               cap=(400, 50), note='wires: lamp.span.wire, 32 / 8 a span')
    body, lamp = m.part('body'), m.part('lamp')
    n = T.pick(10, 6)
    iron, green = F['iron-black'], F['enamel-green']
    # the cast junction the wires meet at, with its lugs
    if T.lod == 0:
        lathe(body, [(.04, -.08), (.045, -.06), (.045, .02), (.03, .045), (.001, .06)], (0, 0, 0), iron, 8)
        for s in (-1, 1):
            mk.box(body, None, s * .045, s * .085, -.012, .012, -.012, .012, iron, 0.0)
            mk.tube(body, [(s * .085, -.018, 0), (s * .1, 0, 0), (s * .085, .018, 0)], .007, iron, 4, caps=False)
    else:
        mk.rod(body, (0, -.08, 0), (0, .05, 0), .04, iron, 4, caps=False)
    # the drop rod and its canopy
    mk.rod(body, (0, -.08, 0), (0, -.33, 0), .018, iron, T.pick(6, 4), caps=False)
    if T.lod == 0:
        lathe(body, [(.02, -.33), (.06, -.355), (.075, -.375)], (0, 0, 0), iron, n)
    # the disc reflector: enamel over, white under (faintly lit), and the globe
    rx = (0.0, 0.0, 0.0)
    lathe(body, [(.31, -.47), (.29, -.455), (.1, -.39), (.07, -.375)] if T.lod == 0 else [(.31, -.47), (.07, -.375)], rx, green, n)
    if T.lod == 0:
        lathe(body, [(.31, -.47), (.302, -.482)], rx, green, n)
    lathe(lamp, [(0.0, -.41), (.08, -.415), (.302, -.482)] if T.lod == 0 else [(0.0, -.41), (.31, -.47)], rx, mk.glow(0xFFE2B8, 1.2), n)
    if T.lod == 0:
        mk.rod(body, (0, -.41, 0), (0, -.47, 0), .04, F['brass'], 8, caps=False)
    gc = (0.0, -.62, 0.0)
    mk.ellipsoid(lamp, gc, ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.15, .15, .155), mk.glow(mk.SODIUM_GLASS, 3.0), T.pick(8, 6), T.pick(6, 2),
                 top=1.0, bottom=-1.0)
    if T.lod == 0:
        mk.ellipsoid(body, (0, -.78, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.02, .02, .025), F['brass'], 6, 2, top=1.0, bottom=-1.0)
    m.light(gc, mk.SODIUM_LAMP, 12.0)
    return m


def span_wire(T, span=6.0, sag=.35):
    """One span of wire from the pendant's junction to a facade's anchor (a rosette and an eye bolt): 32 / 8 a span."""
    mk.finishes()
    m = Module('lamp.span.wire', T.lod, mount='span', rest=(0.0, 7.1, 0.0), block='between the pendant and a facade',
               cap=(48, 8), note='%.0f m span, %.2f m sag; 32 the wire (bible 6.2) + 16 its wall anchor' % (span, sag))
    body = m.part('body')
    a, b = (.1, 0.0, 0.0), (span, .45, 0.0)
    if T.lod == 0:
        mk.tube(body, mk.catenary(a, b, sag, 4), .015, F['iron-black'], 4, caps=False)
        fr = Fr((span, .45, 0.0), (-1, 0, 0))
        mk.disc(body, fr.P(0, 0, .004), .06, F['iron-black'], 6, fr.n)
        mk.rod(body, fr.P(0, 0, .004), fr.P(0, 0, .05), .012, F['iron-black'], 4)
    else:
        mk.tube(body, [a, b], .015, F['iron-black'], 4, caps=False)
    return m


def troffer(T):
    """Level 0's drift: a fluorescent troffer hung on the span wires."""
    mk.finishes()
    m = Module('lamp.span.troffer', T.lod, mount='span', rest=(0.0, 6.9, 0.0), block='span wires (Level 0 drift)', cap=(400, 50))
    body, lamp = m.part('body'), m.part('lamp')
    cream = F['paint-cream']
    for x in (-.5, .5):
        mk.rod(body, (x, 0.0, 0.0), (x, -.32, 0.0), .008, F['iron-black'], 4, caps=False)
    mk.box(body, T, -.64, .64, -.42, -.32, -.17, .17, cream, .012, skip=('-y',))
    mk.box(lamp, None, -.6, .6, -.425, -.418, -.13, .13, mk.glow(mk.TROFFER, 2.4), 0.0, skip=('+y',))
    if T.lod == 0:
        for x in (-.2, .2):
            mk.box(body, T, x - .006, x + .006, -.43, -.418, -.13, .13, cream, 0.0)
        for z in (-.15, .15):
            mk.box(body, T, -.62, .62, -.43, -.415, z - .02, z + .02, cream, 0.0)
    m.light((0, -.6, 0), mk.TROFFER, 8.0, circuit=2, note='Level 0 drift (district circuit)')
    return m


def bulb_string(T, span=6.0, sag=.45, bulbs=10):
    """The camp's bulb string: a sagging cable, bulbs in their sockets."""
    mk.finishes()
    m = Module('lamp.span.bulbs', T.lod, mount='span', rest=(0.0, 4.0, 0.0), block='the camp, from 3.5 m', cap=(400, 60))
    body, lamp = m.part('body'), m.part('lamp')
    pts = mk.catenary((0.0, 0.0, 0.0), (span, 0.0, 0.0), sag, T.pick(10, 3))
    mk.tube(body, pts, .008, F['rubber'], T.pick(4, 3), caps=False)
    for i in range(bulbs):
        t = (i + .5) / bulbs
        x = span * t
        y = -4 * sag * t * (1 - t)
        if T.lod == 0:
            mk.rod(body, (x, y, 0), (x, y - .05, 0), .016, F['rubber'], 4, caps=False)
            mk.ellipsoid(lamp, (x, y - .085, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.03, .03, .042), mk.glow(0xFFC274, 2.8), 6, 3)
        else:
            mk.ellipsoid(lamp, (x, y - .08, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.04, .04, .045), mk.glow(0xFFC274, 2.8), 3, 1)
    m.light((span / 2, -sag - .1, 0), 0xFFC274, 6.0, note='camp warmth')
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Gas-style heritage: the Lamplighter
# ---------------------------------------------------------------------------------------------------------------------------
def _lantern(body, lamp, T, c, yaw=0.0, finial=True):
    """The four-pane lantern standing on its seat at c (the seat's top): the panes are the lamp part. Returns its centre."""
    iron, gilt = F['iron-green'], F['brass']
    y0, y1 = c[1], c[1] + .53
    hb, ht = .12, .17

    def P(x, y, z):
        q = rot_y((x, 0.0, z), yaw)
        return (c[0] + q[0], y, c[2] + q[2])
    sq = math.sqrt(2)
    ph = math.pi / 4 + yaw
    # bottom and top rails
    lathe(body, [(hb * sq + .012, y0 - .02), (hb * sq + .012, y0 + .03)], (c[0], 0, c[2]), iron, 4, smooth=False, phase=ph, cap0=T.lod == 0)
    lathe(body, [(ht * sq + .012, y1 - .03), (ht * sq + .02, y1 + .01)], (c[0], 0, c[2]), iron, 4, smooth=False, phase=ph)
    # corner bars
    for sx in (-1, 1):
        for sz in (-1, 1):
            if T.lod == 0:
                mk.bar(body, [P(sx * hb, y0, sz * hb), P(sx * ht, y1, sz * ht)], .028, .028, iron, caps=False)
    # the panes (glow), a hair inside the bars
    glass = mk.glow(mk.BRIGHT_ROOM, 2.8)
    for k in range(4):
        a = yaw + k * math.pi / 2
        n = rot_y((0.0, 0.0, 1.0), a)
        r = rot_y((-1.0, 0.0, 0.0), a)
        pts = [add(add((c[0], y0 + .03, c[2]), n, hb - .004), r, -hb + .01), add(add((c[0], y0 + .03, c[2]), n, hb - .004), r, hb - .01),
               add(add((c[0], y1 - .03, c[2]), n, ht - .006), r, ht - .012), add(add((c[0], y1 - .03, c[2]), n, ht - .006), r, -ht + .012)]
        lamp.face(pts, glass, n)
    # the roof: a flared eave, a concave hip, the crown and the finial
    prof = [(ht * sq + .02, y1 + .01), (.225 * sq, y1 + .035), (.2 * sq, y1 + .06), (.12 * sq, y1 + .14), (.065 * sq, y1 + .2)]
    lathe(body, prof if T.lod == 0 else [prof[0], prof[-1]], (c[0], 0, c[2]), iron, 4, smooth=False, phase=ph)
    if T.lod == 0:
        cy = y1 + .2
        lathe(body, [(.06, cy - .01), (.065, cy + .02), (.05, cy + .03)], (c[0], 0, c[2]), gilt, 8)
        for k in range(8):
            a = yaw + 2 * math.pi * k / 8
            n = rot_y((0, 0, 1), a)
            r = rot_y((-1, 0, 0), a)
            o = add((c[0], cy + .02, c[2]), n, .062)
            body.face([add(o, r, -.018), add(o, r, .018), add(add(o, r, .006), (0, 1, 0), .06), add(add(o, r, -.006), (0, 1, 0), .06)],
                      gilt, n)
        lathe(body, [(.035, cy + .03), (.045, cy + .07), (.03, cy + .11), (.012, cy + .13), (.007, cy + .2), (0.0, cy + .24)],
              (c[0], 0, c[2]), gilt, 6)
    elif finial:
        lathe(body, [(.04, y1 + .2), (0.0, y1 + .34)], (c[0], 0, c[2]), gilt, 4)
    return (c[0], (y0 + y1) / 2, c[2])


def _seat(g, T, c, f):
    lathe(g, [(.055, c[1] - .14), (.055, c[1] - .07), (.105, c[1] - .035), (.14, c[1] - .01), (.14, c[1] + .005)]
          if T.lod == 0 else [(.05, c[1] - .14), (.14, c[1] + .005)], (c[0], 0, c[2]), f, T.pick(10, 6), cap1=T.lod == 0)


def _column(g, T, top=3.36, bar=True):
    """The moulded base and the fluted column, its mid collar and the ladder bar at 3.3 m."""
    iron, gilt = F['iron-green'], F['brass']
    n = T.pick(10, 6)
    if T.lod == 0:
        base = [(.25, 0.0), (.25, .05), (.232, .075), (.232, .11), (.205, .14), (.205, .24), (.18, .27), (.16, .38), (.13, .52),
                (.112, .6), (.125, .625), (.125, .67), (.095, .72)]
    else:
        base = [(.25, 0.0), (.16, .3), (.095, .72)]
    lathe(g, base, (0, 0, 0), iron, n)
    if T.lod == 0:
        sec = []
        for i in range(16):
            a = 2 * math.pi * i / 16
            r = .085 if i % 2 == 0 else .071
            sec.append((r * math.cos(a), r * math.sin(a)))
        sweep(g, sec, [(0, .71, 0), (0, 1.95, 0), (0, top, 0)], iron, caps=False, scale=[1.0, .9, .78], smooth_u=False)
        lathe(g, [(.08, 1.92), (.098, 1.945), (.098, 2.005), (.078, 2.03)], (0, 0, 0), iron, n)
    else:
        mk.rod(g, (0, .71, 0), (0, top, 0), .08, iron, 6, caps=False, r1=.064)
    # the ladder bar where the lamplighter leaned his ladder
    if not bar:
        return
    mk.rod(g, (-.36, 3.3, 0), (.36, 3.3, 0), .026, iron, T.pick(6, 4), caps=False)
    for s in (-1, 1):
        mk.ellipsoid(g, (s * .385, 3.3, 0), ((0, 0, 1), (0, 1, 0), (1, 0, 0)), (.042, .042, .042), gilt, T.pick(6, 4), T.pick(3, 1),
                     top=1.0, bottom=-1.0)


def _capital(g, T, y):
    iron = F['iron-green']
    n = T.pick(10, 6)
    if T.lod == 0:
        lathe(g, [(.066, y), (.08, y + .05), (.12, y + .13), (.15, y + .19), (.155, y + .22), (.11, y + .24), (.068, y + .265)], (0, 0, 0),
              iron, n)
        for k in range(6):
            a = 2 * math.pi * k / 6 + math.pi / 6
            n_ = rot_y((0, 0, 1), a)
            r = rot_y((-1, 0, 0), a)
            o = add((0, y + .05, 0), n_, .085)
            tip = add(add((0, y + .2, 0), n_, .175), r, 0.0)
            g.face([add(o, r, -.03), add(o, r, .03), add(tip, r, .02), add(tip, r, -.02)], iron, add(n_, (0, -.4, 0)))
    else:
        lathe(g, [(.07, y), (.15, y + .22)], (0, 0, 0), iron, n)


def lamplighter(T, kind='single'):
    """kind: single (lantern at 4.0 m), triple (squares), wall (a lantern on a scroll bracket)."""
    mk.finishes()
    if kind == 'wall':
        return _wall_lantern(T)
    m = Module('lamp.lamplighter.' + kind, T.lod, block='lamp- (0.3 m)' if kind == 'single' else 'lamp- (square)',
               cap=(1400, 140) if kind == 'single' else (2600, 240), note='lantern at %.1f m' % (4.0 if kind == 'single' else 4.2))
    body, lamp = m.part('body'), m.part('lamp')
    iron = F['iron-green']
    _column(body, T, bar=T.lod == 0 or kind == 'single')
    _capital(body, T, 3.36)
    if kind == 'single':
        _seat(body, T, (0, 3.765, 0), iron)
        c = _lantern(body, lamp, T, (0, 3.765, 0))
        m.light(c, mk.BRIGHT_ROOM, 7.0, note='heritage, warm')
        return m
    # the triple branch: a boss and three scroll arms, a lantern on each, a finial in the middle
    lathe(body, [(.07, 3.62), (.09, 3.66), (.09, 3.74), (.06, 3.78)] if T.lod == 0 else [(.09, 3.62), (.07, 3.78)], (0, 0, 0), iron,
          T.pick(10, 6), cap1=T.lod == 0)
    for k in range(3):
        yaw = 2 * math.pi * k / 3 + math.pi / 2
        d = rot_y((0, 0, 1), yaw)
        ctrl = [(0.05, 3.7), (.2, 3.74), (.38, 3.86), (.52, 3.98), (.56, 4.0)]
        pts = [add((0, y, 0), d, s) for (s, y) in ctrl]
        mk.tube(body, mk.catmull(pts, 2) if T.lod == 0 else [pts[0], pts[2], pts[4]], .03, iron, T.pick(6, 3), caps=False)
        if T.lod == 0:
            curl = [add((0, 3.7 - .06 + .06 * math.cos(a), 0), d, .25 + .06 * math.sin(a)) for a in (math.radians(x) for x in (200, 270, 340, 50, 110))]
            mk.tube(body, [add((0, 3.7, 0), d, .08)] + mk.catmull(curl, 1), .016, iron, 4, caps=True)
        seat = add((0, 4.015, 0), d, .56)
        _seat(body, T, seat, iron)
        c = _lantern(body, lamp, T, seat, yaw, finial=T.lod == 0)
        m.light(c, mk.BRIGHT_ROOM, 7.0, note='one of three')
    lathe(body, [(.04, 3.78), (.03, 3.95), (.05, 4.0), (.03, 4.05), (.01, 4.2), (0.0, 4.32)] if T.lod == 0 else [(.04, 3.78), (0.0, 4.32)],
          (0, 0, 0), F['brass'], T.pick(8, 4))
    return m


def _wall_lantern(T):
    mk.finishes()
    m = Module('lamp.lamplighter.wall', T.lod, mount='wall', rest=(0.0, 3.4, 0.0), block='a civic facade (over 3.1 m)', cap=(1100, 110))
    body, lamp = m.part('body'), m.part('lamp')
    iron = F['iron-green']
    fr = Fr((0, 0, 0), (0, 0, 1))
    _plate(body, T, fr, _stadium(.075, .14, T.pick(5, 2)), 0.0, .03, iron)
    mk.tube(body, mk.catmull([(0, 0.0, .03), (0, .0, .3), (0, .02, .5), (0, .06, .6)], T.pick(2, 1)), .03, iron, T.pick(6, 4), caps=False)
    if T.lod == 0:
        sc = mk.catmull([(0, -.2, .03), (0, -.17, .16), (0, -.09, .3), (0, -.02, .4)], 2)
        mk.bar(body, sc, .016, .03, iron, up=(1, 0, 0))
        curl = [(0, -.2 + .05 * math.sin(a), .1 - .05 * math.cos(a)) for a in (math.radians(x) for x in (0, 70, 140, 210, 270))]
        mk.bar(body, mk.catmull(curl, 1), .016, .03, iron, up=(1, 0, 0))
    seat = (0.0, .2, .6)
    _seat(body, T, seat, iron)
    c = _lantern(body, lamp, T, seat)
    m.light(c, mk.BRIGHT_ROOM, 6.0)
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# The Civil Defence flood mast
# ---------------------------------------------------------------------------------------------------------------------------
def flood_mast(T, height=7.2):
    mk.finishes()
    m = Module('lamp.cd.flood-mast', T.lod, block='a 0.6 m mast block (camp, courts, hall doors, the Cordon)', cap=(1600, 160),
               note='quad head at %.1f m' % height)
    body, lamp = m.part('body'), m.part('lamp')
    olive, orange = F['cd-olive'], F['cd-orange']
    R = .22
    legs = [(R * math.cos(a), R * math.sin(a)) for a in (math.pi / 2 + 2 * math.pi * k / 3 for k in range(3))]
    # the base plate and its feet
    plate = [(1.35 * x, 1.35 * z) for (x, z) in legs]
    mk.prism(body, plate, 0.0, .03, olive, top=True)
    if T.lod == 0:
        for (x, z) in legs:
            mk.rod(body, (x * 1.2, .03, z * 1.2), (x * 1.2, .06, z * 1.2), .02, F['iron-black'], 6)
    # the legs: orange to 2.1 m, olive above
    for (x, z) in legs:
        if T.lod == 0:
            mk.rod(body, (x, .03, z), (x, 2.1, z), .032, orange, 6, caps=False)
            mk.rod(body, (x, 2.1, z), (x, height, z), .032, olive, 6, caps=False)
        else:
            mk.rod(body, (x, .03, z), (x, height, z), .032, olive, 3, caps=False)
    # the lattice: a zig-zag on each face, battens every third bay
    bay = T.pick(.6, height / 2.0 - .06)
    nb = int(height / bay)
    for k in range(3):
        (ax, az), (bx, bz) = legs[k], legs[(k + 1) % 3]
        for i in range(nb):
            y0, y1 = .1 + i * bay, .1 + (i + 1) * bay
            if y1 > height - .05:
                break
            p0 = (ax, y0, az) if i % 2 == 0 else (bx, y0, bz)
            p1 = (bx, y1, bz) if i % 2 == 0 else (ax, y1, az)
            mk.rod(body, p0, p1, .013, olive if y0 > 2.0 else orange, T.pick(4, 3), caps=False)
            if T.lod == 0 and i % 3 == 0:
                mk.rod(body, (ax, y0, az), (bx, y0, bz), .015, olive if y0 > 2.0 else orange, 4, caps=False)
    # the junction box and the CD roundel at 1.3 m, and the conduit up the mast
    if T.lod == 0:
        fr = Fr((0, 0, .255), (0, 0, 1))
        mk.fbox(body, T, fr, -.16, .16, 1.0, 1.42, 0.0, .16, olive, .012)
        mk.disc(body, fr.P(0, 1.28, .166), .09, F['cd-white'], 12, fr.n)
        mk.crest(body, T, fr, 0, 1.28, .07, .172, F['cd-white'], F['cd-olive'], F['iron-black'], F['cd-olive'])
        mk.text(body, fr, 0, 1.06, .009, 'CD 647', F['cd-white'], c=.166)
        mk.rod(body, fr.P(.1, 1.42, .05), fr.P(.1, height - .2, .05), .018, F['iron-black'], 4, caps=False)
    # the head: a square frame and four floods aimed out and down
    yh = height
    mk.box(body, T, -.5, .5, yh - .03, yh + .03, -.03, .03, orange, .01)
    mk.box(body, T, -.03, .03, yh - .03, yh + .03, -.5, .5, orange, .01)
    for (x, z) in legs:
        if T.lod == 0:
            mk.rod(body, (x, yh - .3, z), (x * .3, yh, z * .3), .02, olive, 4, caps=False)
    for k in range(4):
        yaw = math.pi / 4 + k * math.pi / 2
        d = rot_y((0, 0, 1), yaw)
        o = add((0, yh + .05, 0), d, .42)
        _flood(body, lamp, T, o, yaw)
        aim = norm(add(d, (0, -.65, 0)))
        m.light(add(add(o, d, .12), (0, -.05, 0)), mk.FLOOD, 18.0, kind='spot', direction=aim, note='flood %d of 4' % (k + 1))
    return m


def _flood(body, lamp, T, o, yaw, tilt=math.radians(33)):
    """One flood: a housing on a yoke, aimed `tilt` down, its lens the lamp part."""
    d = rot_y((0, 0, 1), yaw)
    n = norm(add(scale(d, math.cos(tilt)), (0, -math.sin(tilt), 0)))
    fr = Fr(o, n, (0, 1, 0))
    orange, olive = F['cd-orange'], F['cd-olive']
    # the yoke from the frame
    if T.lod == 0:
        mk.fbox(body, T, fr, -.23, -.2, -.05, .05, -.12, .04, olive, .006)
        mk.fbox(body, T, fr, .2, .23, -.05, .05, -.12, .04, olive, .006)
    # the housing
    mk.fbox(body, T, fr, -.19, .19, -.15, .15, -.14, .02, orange, .02)
    mk.quad(lamp, fr, -.165, .165, -.125, .125, .026, mk.glow(mk.FLOOD, 3.6))
    if T.lod == 0:
        # the visor over the lens and the fins behind
        body.face([fr.P(-.19, .15, .02), fr.P(.19, .15, .02), fr.P(.19, .17, .12), fr.P(-.19, .17, .12)], orange, fr.D(0, 1, -.2))
        body.face([fr.P(-.19, .17, .12), fr.P(.19, .17, .12), fr.P(.19, .15, .02), fr.P(-.19, .15, .02)], orange, fr.D(0, -1, .2))
        for a in (-.12, -.04, .04, .12):
            mk.fbox(body, T, fr, a - .008, a + .008, -.13, .13, -.2, -.14, orange, 0.0)
