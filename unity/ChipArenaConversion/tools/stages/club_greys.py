"""Substation 128's Greys (club_props.py banks them): the DJ and the crowd as Grey aliens, an original take on the classic
Grey: tall and thin, smooth grey skin, a large bulbous cranium, big glossy black almond eyes, small nose and mouth slits, a
narrow torso, long thin arms and long fingers. Each figure is rigid parts in their own frames, posed each frame by Core
NativeSubstationArt from the club rig's crowd and DJ transforms (their bobs, sways, raised arms and nods).

A crowd Grey at height 1 (the pose scales it by the rig's own height), facing its +z:
  grey-legs-V   origin between the feet on the floor, +y up to the hip joint (HIP m): the pose stretches it to the bobbing hip
  grey-body-V   origin at the hip joint: the pelvis, the narrow torso, the neck and the head
  grey-arm-V    origin at the shoulder, hanging down -y: upper arm, forearm, hand and fingers (turned over for a raised arm)
V 0..3 the look: grey with a cyan glow necklace; pale green-grey with magenta glow bracelets; blue-grey in a silver suit;
light grey in a dark top with a violet chest band.

The DJ (facing the hall, -z), in the rig's frames (Core NativeClubRig.Dj):
  dj-legs       origin between the feet on the stage
  dj-torso      origin at the rig's torso centre (1.25 m up): the jacket with its flared collar and glow piping, the neck
  dj-head       origin at the rig's head centre (1.73 m up): the head with its chrome headphones (the nod turns it about here)
  dj-arm-ear    origin at the rig's right arm centre: the arm bent up, its hand at the headphone
  dj-upper      origin at the left shoulder, down -y to the elbow (DJ_UPPER m)
  dj-fore       origin at the elbow, down -y: the forearm and the hand
"""
import math

import stagekit as sk
from stagekit import Geo, lathe
import club_kit as ck
from club_kit import F, glow, limb, skin, ring, mat_rot, rod

HIP, SHOULDER, SHOULDER_OUT, DJ_UPPER = .84, .5, .24, .3

# the head: rings from the neck to the crown: (height over the head's base, half width, half depth, set back)
HEAD = [(0.0, .032, .036, .0), (.03, .05, .056, .018), (.07, .086, .092, .014), (.13, .126, .136, .004), (.2, .152, .17, -.01),
        (.27, .157, .182, -.022), (.33, .138, .162, -.026), (.38, .094, .112, -.026), (.405, .03, .04, -.024)]
EYE_Y = .135


def head_at(y):
    """The head's half width, half depth and set-back at a height over its base (between the rings)."""
    for i in range(len(HEAD) - 1):
        a, b = HEAD[i], HEAD[i + 1]
        if y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return tuple(a[j] + (b[j] - a[j]) * t for j in (1, 2, 3))
    return HEAD[-1][1:]


def face_point(x, y):
    """The point of the face (the head's front, +z) at x across and y up, and its outward normal (head frame, base at 0)."""
    rx, rz, sh = head_at(y)
    t = max(-.98, min(.98, x / rx))
    z = sh + rz * math.sqrt(1 - t * t)
    e = .004
    rx2, rz2, sh2 = head_at(y + e)
    t2 = max(-.98, min(.98, x / rx2))
    dzdy = (sh2 + rz2 * math.sqrt(1 - t2 * t2) - z) / e
    dzdx = -rz * t / (rx * math.sqrt(1 - t * t))
    return (x, y, z), sk._norm((-dzdx, -dzdy, 1.0))


def head(g, base, T, skinf, s=1.0, quality=0):
    """A Grey's head on `base` (the top of the neck), scaled by s: the cranium, the two almond eyes, the nose and mouth slits.
    quality 1: the DJ's (finer)."""
    n = (18, 12, 8)[T.i] + (4 if quality and T.i == 0 else 0)
    rows = HEAD if T.i < 2 else [HEAD[0], HEAD[2], HEAD[4], HEAD[6], HEAD[8]]
    rings = [ring((base[0], base[1] + y * s, base[2] + sh * s), rx * s, rz * s, n) for (y, rx, rz, sh) in rows]
    skin(g, rings, skinf)

    def W(p):
        return (base[0] + p[0] * s, base[1] + p[1] * s, base[2] + p[2] * s)
    # the eyes: black glass almonds lying in the face, slanted up and out, a little sunk at their rims
    for sx in (-1, 1):
        cx, cy = sx * .068, EYE_Y
        a = math.radians(24) * sx
        k = (14, 10, 6)[T.i]
        rr = (1.0, .74, .42, .06) if T.i == 0 else (1.0, .5, .06)
        rings_e = []
        for rho in rr:
            pts = []
            for i in range(k):
                ph = 2 * math.pi * i / k
                u = math.cos(ph) * .05 * rho
                v = math.sin(ph) * .034 * rho * (1 - .42 * math.cos(ph) ** 2)
                # the almond lies in the face: each point on the head's own surface (so it wraps round), bulging off it
                pt, nrm = face_point(cx + u * math.cos(a) - v * math.sin(a), cy + u * math.sin(a) + v * math.cos(a))
                bulge = .016 * (1 - rho * rho) - .003
                pts.append(W(tuple(pt[j] + nrm[j] * bulge for j in range(3))))
            rings_e.append(pts)
        skin(g, rings_e, F['eye'], close0=False, close1=True)
    if T.i == 2:
        return
    # the nose: two short slits; the mouth: one thin line
    for (x, y, w, h, tilt) in ((-.013, .082, .006, .02, .25), (.013, .082, .006, .02, -.25), (0.0, .048, .034, .005, 0.0)):
        c, nrm = face_point(x, y)
        up = sk._norm(sk._sub((0, 1, 0), tuple(nrm[j] * nrm[1] for j in range(3))))
        side = sk._cross(up, nrm)
        el = tuple(side[j] * math.cos(tilt) + up[j] * math.sin(tilt) for j in range(3))
        es = sk._cross(nrm, el)
        pts = [W(tuple(c[j] + el[j] * a * w + es[j] * b * h + nrm[j] * .0025 for j in range(3))) for (a, b) in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        g.face(pts, F['slit'], nrm)


def torso_rings(T, wide=1.0):
    """The body from under the hip to the top of the neck (hip frame): a narrow pelvis and waist, slight ribs, sloping
    shoulders, a long thin neck."""
    rows = [(-.07, .06, .05, 2.0), (-.02, .112, .072, 2.3), (.06, .108, .07, 2.3), (.15, .088, .06, 2.2), (.27, .112, .07, 2.3), (.39, .15, .078, 2.6),
            (.46, .168, .078, 2.8), (.51, .15, .068, 2.5), (.545, .07, .05, 2.0), (.59, .038, .038, 2.0), (.67, .033, .034, 2.0)]
    if T.i == 2:
        rows = [rows[0], rows[1], rows[3], rows[6], rows[8], rows[10]]
    n = (14, 10, 6)[T.i]
    return [ring((0, y, 0), rx * wide, rz * wide, n, power=p) for (y, rx, rz, p) in rows]


NECK_TOP = .66


def crowd_body(T, v):
    g = Geo()
    sk_ = F['skin%d' % v]
    rings = torso_rings(T)
    if v == 2:
        # the silver suit to the collar, the neck bare
        cut = 9 if T.i < 2 else 4
        skin(g, rings[:cut], F['suitsilver'], close1=False)
        skin(g, rings[cut - 1:], sk_, close0=False)
        if T.i < 2:
            lathe(g, [(.075, .53), (.085, .56), (.07, .575)], (0, 0, 0), F['suitsilver'], 8)
    elif v == 3:
        a, b = (2, 9) if T.i < 2 else (1, 4)
        skin(g, rings[:a + 1], sk_, close1=False)
        skin(g, rings[a:b], F['suit'], close0=False, close1=False)
        skin(g, rings[b - 1:], sk_, close0=False)
        if T.i < 2:
            g.extend(band(.39, .152, .08, 2.6, .02, glow(ck.VIOLET, 2.2), 10))
    else:
        skin(g, rings, sk_)
    if v == 0:
        g.extend(band(.535, .085, .062, 2.0, .014, glow(ck.CYAN, 2.4), 8 if T.i < 2 else 5))
    if T.i < 2:
        for sx in (-1, 1):
            ck.ball(g, (sx * .2, .485, 0), .052, F['suitsilver'] if v == 2 else F['suit'] if v == 3 else sk_, 6, 4)
    head(g, (0, NECK_TOP, 0), T, sk_)
    return g


def band(y, rx, rz, power, r, f, n):
    """A thin ring round the body at height y (a glow necklace, a chest band)."""
    g = Geo()
    path = ring((0, y, 0), rx + r * .6, rz + r * .6, max(n, 8), power=power)
    sk.tube(g, path, r, f, 4, closed=True, caps=False)
    return g


def crowd_legs(T, v):
    """Two thin legs from the feet to the hip joint, the knees a little bent (the pose stretches them as the figure bobs)."""
    g = Geo()
    f = F['suitsilver'] if v == 2 else F['skin%d' % v]
    n = (8, 6, 4)[T.i]
    for sx in (-1, 1):
        pts = [(sx * .07, .035, -.01), (sx * .072, .11, -.012), (sx * .08, .45, .03), (sx * .068, HIP - .02, 0.0)]
        rad = [.03, .028, .04, .058]
        if T.i == 0:
            pts.insert(2, (sx * .078, .3, .012))
            rad.insert(2, .037)
        limb(g, pts, rad, f, n)
        # the foot: a long narrow pad
        limb(g, [(sx * .072, .022, -.06), (sx * .075, .03, .04), (sx * .08, .016, .15)], [.026, .034, .02], F['suitsilver'] if v == 2 else F['skin%d' % v], (6, 5, 4)[T.i], flat=.6, side=(1, 0, 0))
    return g


def arm(T, skinf, cuff=None, s=1.0, bend=.05, quality=0, start=0.0):
    """A long thin arm hanging from its shoulder (origin) down -y: upper arm, a slight elbow, the forearm, a narrow palm and
    three long fingers and a thumb. cuff: a glowing bracelet's (or a sleeve cuff's) finish at the wrist."""
    g = Geo()
    n = (8, 5, 4)[T.i] + (2 if quality and T.i == 0 else 0)
    pts = [(0, -start, 0), (0, -.15 * s, .004), (0, -.29 * s, .012), (0, -.44 * s, .012 + bend * .5), (0, -.57 * s, .012 + bend)]
    rad = [.036, .033, .028, .026, .021]
    if T.i == 2:
        pts, rad = [pts[0], pts[2], pts[4]], [rad[0], rad[2], rad[4]]
    limb(g, pts, rad, skinf, n, close1=False)
    w = pts[-1]
    hand(g, w, T, skinf, s, quality)
    if cuff is not None and T.i < 2:
        lathe(g, [(.027, -.012), (.033, 0.0), (.027, .012)], (w[0], w[1] + .03, w[2] - .004), cuff, 6)
    return g


def hand(g, w, T, skinf, s=1.0, quality=0, curl=0.0):
    """A hand from the wrist w down -y: a narrow palm, three long fingers and a thumb (a mitt on the far tiers)."""
    if T.i == 2:
        limb(g, [w, (w[0], w[1] - .1 * s, w[2] + .012), (w[0], w[1] - .2 * s, w[2] + .02)], [.021, .028, .008], skinf, 4, flat=.5)
        return
    palm = (w[0], w[1] - .075 * s, w[2] + .01)
    limb(g, [w, (w[0], w[1] - .035 * s, w[2] + .005), palm], [.021, .03, .026], skinf, 6 if T.i == 0 else 5, flat=.48)
    k = 4 if T.i else 5
    for (dx, ln) in ((-.019, .13), (0.0, .15), (.019, .135)):
        a = (palm[0] + dx, palm[1] + .008, palm[2])
        m = (palm[0] + dx * 1.5, palm[1] - ln * .5 * s, palm[2] + .012 + curl * .03)
        b = (palm[0] + dx * 1.9, palm[1] - ln * s, palm[2] + .02 + curl * .09)
        limb(g, [a, m, b], [.0105, .009, .0065], skinf, k, close0=False)
        if quality and T.i == 0:
            ck.ball(g, b, .0085, skinf, 5, 3)
    a = (w[0] + .022, w[1] - .03 * s, w[2] + .012)
    limb(g, [a, (a[0] + .022, a[1] - .04 * s, a[2] + .02), (a[0] + .03, a[1] - .085 * s, a[2] + .028)], [.011, .0095, .007], skinf, k, close0=False)


def crowd_arm(T, v):
    cuff = glow(ck.MAGENTA, 2.4) if v == 1 else F['suitsilver'] if v == 2 else None
    return arm(T, F['skin%d' % v], cuff)


def crowd_figure(T, v, up=False, tall=1.0):
    """A whole crowd Grey standing at rest (previews, the bake's occluders): legs, body, both arms."""
    g = Geo()
    g.extend(crowd_legs(T, v).transformed([1, 0, 0, 0, 0, tall, 0, 0, 0, 0, 1, 0]))
    g.extend(crowd_body(T, v).transformed([1, 0, 0, 0, 0, tall, 0, HIP * tall, 0, 0, 1, 0]))
    a = crowd_arm(T, v)
    for sx in (-1, 1):
        ang = (sx * .3 + math.pi) if up else sx * .12
        m = mat_rot((0, 0, 1), ang)
        g.extend(a.transformed([m[0][0], m[0][1] * tall, m[0][2], sx * SHOULDER_OUT, m[1][0], m[1][1] * tall, m[1][2], (HIP + SHOULDER) * tall, m[2][0], m[2][1] * tall, m[2][2], 0]))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The DJ (built facing +z, then turned to face the hall)
# ---------------------------------------------------------------------------------------------------------------------------
TURN = [-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0]


def dj_legs(T):
    g = Geo()
    n = (10, 6, 4)[T.i]
    for sx in (-1, 1):
        limb(g, [(sx * .085, .04, -.01), (sx * .086, .12, -.012), (sx * .09, .3, .008), (sx * .095, .48, .022), (sx * .085, .9, 0.0)], [.034, .031, .04, .045, .066], F['suit'], n)
        limb(g, [(sx * .086, .026, -.07), (sx * .09, .034, .05), (sx * .095, .018, .17)], [.03, .04, .022], F['suitsilver'], (8, 5, 4)[T.i], flat=.6)
    return g.transformed(TURN)


def dj_torso(T):
    """Origin at the rig's torso centre (1.25 m over the stage): the pelvis at -.36, the shoulders at +.3, the neck to +.4."""
    g = Geo()
    n = (16, 10, 6)[T.i]
    rows = [(-.4, .07, .055, 2.0), (-.34, .125, .08, 2.3), (-.24, .118, .075, 2.3), (-.12, .1, .066, 2.2), (.02, .13, .078, 2.4), (.16, .175, .088, 2.7),
            (.25, .2, .09, 2.9), (.305, .18, .078, 2.6), (.335, .08, .056, 2.0), (.37, .042, .042, 2.0), (.41, .037, .038, 2.0)]
    rings = [ring((0, y, 0), rx, rz, n, power=p) for (y, rx, rz, p) in rows]
    skin(g, rings[:9], F['jacket'], close1=False)
    skin(g, rings[8:], F['skindj'], close0=False)
    for sx in (-1, 1):
        ck.ball(g, (sx * .235, .28, 0), .062, F['jacket'], 8 if T.i < 2 else 5, 4)
    if T.i < 2:
        # the flared collar: a ring standing off the shoulders, open at the front; glow piping down the front and round the hem
        pts = []
        for i in range(13):
            a = math.pi * (.18 + 1.64 * i / 12)                     # from the front right, round the back, to the front left
            pts.append((math.sin(a) * .115, .34 + .1 * math.sin(math.pi * i / 12) ** .6, -math.cos(a) * .085 - .01))
        base = [(p[0] * .7, .325, p[2] * .75) for p in pts]
        for i in range(12):
            g.face((base[i], base[i + 1], pts[i + 1], pts[i]), F['jacket'], normals=[sk._norm((p[0], .3, p[2])) for p in (base[i], base[i + 1], pts[i + 1], pts[i])])
            g.face((pts[i], pts[i + 1], base[i + 1], base[i]), F['suitsilver'], normals=[sk._norm((-p[0], .5, -p[2])) for p in (pts[i], pts[i + 1], base[i + 1], base[i])])
        sk.tube(g, pts, .008, glow(ck.MAGENTA, 2.6), 4, caps=False)
        for sx in (-1, 1):
            sk.tube(g, [(sx * .03, .3, .082), (sx * .034, .16, .092), (sx * .03, .0, .082), (sx * .028, -.2, .078)], .007, glow(ck.MAGENTA, 2.6), 4, caps=False)
        g.extend(band(-.3, .125, .082, 2.3, .01, glow(ck.CYAN, 2.2), 12))
    return g.transformed(TURN)


def dj_head(T):
    """Origin at the rig's head centre: the chin .13 below, the crown .3 above; chrome headphones with glowing cups."""
    g = Geo()
    s = 1.08
    base = (0, -.135, .012)
    head(g, base, T, F['skindj'], s, quality=1)
    # the headphones: a band over the crown, a cup on each side at the eyes' height
    yc = base[1] + (EYE_Y + .0) * s
    rx = head_at(EYE_Y)[0] * s
    n = (12, 8, 6)[T.i]
    for sx in (-1, 1):
        c = (sx * (rx + .012), yc, base[2] - .004)
        lathe(g, [(.05, -.012), (.074, 0.0), (.076, .03), (.06, .046), (.03, .05), (1e-4, .05)], c, F['chrome'], n, axis=(sx, 0, 0))
        lathe(g, [(.066, .034), (.052, .048)], c, glow(ck.CYAN, 2.6), n, axis=(sx, 0, 0))
        lathe(g, [(.058, -.03), (.07, -.012)], c, F['black'], n, axis=(sx, 0, 0))
    path = []
    k = 12 if T.i == 0 else 6
    for i in range(k + 1):
        a = math.pi * i / k
        yy = EYE_Y + .02 + math.sin(a) * (HEAD[-1][0] - EYE_Y + .012)
        path.append((-math.cos(a) * .188 * s, base[1] + yy * s, base[2] - .012))
    sk.sweep(g, sk.rect_section(.034, .012, .004), path, F['chrome'], up=(0, 0, 1), smooth_u=False)
    return g.transformed(TURN)


def dj_arm_ear(T):
    """The right arm (the +x one), bent up with its hand at the headphone cup: origin at the rig's box centre (.26 out, 1.5 up,
    .05 toward the hall); built straight in the stage's axes."""
    g = Geo()
    n = (10, 6, 4)[T.i]
    sh, el, wr = (-.025, .03, .05), (.2, -.17, .0), (.04, .12, .0)
    limb(g, [sh, sk._lerp(sh, el, .5), el], [.04, .037, .033], F['jacket'], n, close1=False)
    ck.ball(g, el, .034, F['jacket'], 6, 4)
    limb(g, [el, sk._lerp(el, wr, .5), wr], [.031, .027, .022], F['skindj'], n, close0=False, close1=False)
    if T.i < 2:
        mid = sk._lerp(el, wr, .12)
        d = sk._norm(sk._sub(wr, el))
        lathe(g, [(.034, -.014), (.038, 0.0), (.034, .014)], mid, glow(ck.MAGENTA, 2.6), 6, axis=d)
    # the hand reaching up to the cup: build it hanging from the wrist, then turn it up along the forearm
    h = Geo()
    hand(h, (0, 0, 0), T, F['skindj'], 1.0, quality=1, curl=.6)
    d = sk._norm(sk._sub(wr, el))
    axis = sk._cross((0, -1, 0), d)
    ang = math.acos(max(-1, min(1, -d[1])))
    m = mat_rot(axis, ang) if sk._dot(axis, axis) > 1e-9 else mat_rot((0, 0, 1), 0)
    g.extend(h.transformed([m[0][0], m[0][1], m[0][2], wr[0], m[1][0], m[1][1], m[1][2], wr[1], m[2][0], m[2][1], m[2][2], wr[2]]))
    return g


def dj_upper(T):
    g = Geo()
    n = (10, 6, 4)[T.i]
    limb(g, [(0, .01, 0), (0, -.15, 0), (0, -DJ_UPPER, 0)], [.042, .037, .033], F['jacket'], n)
    ck.ball(g, (0, -DJ_UPPER, 0), .034, F['jacket'], 6, 4)
    return g


def dj_fore(T):
    """The forearm and hand from the elbow down -y; the fingers spread on the mixer."""
    g = Geo()
    n = (10, 6, 4)[T.i]
    limb(g, [(0, 0, 0), (0, -.15, 0), (0, -.3, 0)], [.031, .027, .022], F['skindj'], n, close1=False)
    if T.i < 2:
        lathe(g, [(.034, -.014), (.038, 0.0), (.034, .014)], (0, -.04, 0), glow(ck.MAGENTA, 2.6), 6, axis=(0, -1, 0))
    h = Geo()
    hand(h, (0, 0, 0), T, F['skindj'], 1.0, quality=1, curl=.3)
    # the palm turned to face down the arm's swing (toward the hall when the arm is forward)
    g.extend(h.transformed([-1, 0, 0, 0, 0, 1, 0, -.3, 0, 0, -1, 0]))
    return g


def dj_figure(T, lift=0.0):
    """The whole DJ at rest behind the booth (stage space, origin between the feet): for the previews and the bake."""
    g = Geo()

    def put(part, at, m=None):
        m = m or ((1, 0, 0), (0, 1, 0), (0, 0, 1))
        g.extend(part.transformed([m[0][0], m[0][1], m[0][2], at[0], m[1][0], m[1][1], m[1][2], at[1], m[2][0], m[2][1], m[2][2], at[2]]))
    put(dj_legs(T), (0, 0, 0))
    put(dj_torso(T), (0, 1.25, 0))
    put(dj_head(T), (0, 1.73, -.04))
    put(dj_arm_ear(T), (.26, 1.5, -.05))
    a = .3 + 2.2 * lift
    m = mat_rot((1, 0, 0), a)
    put(dj_upper(T), (-.27, 1.55, 0), m)
    e = tuple(m[r][1] * -DJ_UPPER for r in range(3))
    bend = 1.15 + (.2 - 1.15) * lift
    put(dj_fore(T), (-.27 + e[0], 1.55 + e[1], e[2]), mat_rot((1, 0, 0), a + bend))
    return g
