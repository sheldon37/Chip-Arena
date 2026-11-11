"""Skyframe's far chunks (docs/arenas/SKYFRAME.md "Stage art"): what lies round and under the garden, too far for a finer tier, each
one chunk in one tier (`static/<name>/2`): the tower's own body going down into the clouds (glass between floor bands, lit
offices, a planted setback), the wind turbines' outriggers, masts and nacelles (the rotors are the rig's), the other towers
standing out of the cloud sea, the sea itself (a rolling sheet with heaped islands on it) and the great banks on the horizon.
Nothing here stands in the play space: the body ends under the garden (y -7), the masts stand outside the bounds."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe
import skyframe_kit as kit
from skyframe_kit import F, hashf, beam, ibeam, rod, blob
import skyframe_props as props

VIEWS = [('sky-down', (-30, 6, 26), (-80, -40, 120), 70), ('sky-wide', (-150, 60, -220), (0, -10, 0), 60), ('sky-horizon', (0, 6, 0), (-400, 0, -900), 60), ('sky-tower', (-70, -6, -60), (0, -30, 0), 60),
         ('sky-turbine', (-22, 4, 20), (-35, 14, 30), 60)]


def fins(names):
    return tuple(F[n] for n in names)


def tower(L):
    g = Geo()
    top = -7.0
    for t in range(3):
        w, d, h = 52 + t * 6, 32 + t * 6, (16, 20, 80)[t]
        x0, x1, z0, z1, y0 = -w / 2, w / 2, -d / 2, d / 2, top - h
        nb = int(h / 4)
        for side, (o, u, n, ln) in enumerate((((x0, 0, z0), (1, 0, 0), (0, 0, -1), w), ((x0, 0, z1), (1, 0, 0), (0, 0, 1), w), ((x0, 0, z0), (0, 0, 1), (-1, 0, 0), d), ((x1, 0, z0), (0, 0, 1), (1, 0, 0), d))):
            bays = int(ln / 4)
            def P(a, y, out=0.0):
                return (o[0] + u[0] * a + n[0] * out, y, o[2] + u[2] * a + n[2] * out)
            for fl in range(nb):
                ya, yb = top - (fl + 1) * 4.0, top - fl * 4.0
                # a floor: its band, then the glass in bays, some offices lit
                g.face((P(0, yb - .7, .12), P(ln, yb - .7, .12), P(ln, yb, .12), P(0, yb, .12)), F['coping'], n)
                g.face((P(0, yb - .7, 0), P(ln, yb - .7, 0), P(ln, yb - .7, .12), P(0, yb - .7, .12)), F['coping'], (0, -1, 0))
                run = 0
                for k in range(bays + 1):
                    lit = hashf(t, side, fl, k) > .78
                    if k == bays or lit:
                        if k > run:
                            g.face((P(ln * run / bays, ya, 0), P(ln * k / bays, ya, 0), P(ln * k / bays, yb - .7, 0), P(ln * run / bays, yb - .7, 0)), F['glass-dark'], n)
                        if k < bays:
                            g.face((P(ln * k / bays + .15, ya + .2, 0), P(ln * (k + 1) / bays - .15, ya + .2, 0), P(ln * (k + 1) / bays - .15, yb - .9, 0), P(ln * k / bays + .15, yb - .9, 0)), F['window'], n)
                            g.face((P(ln * k / bays, ya, -.02), P(ln * (k + 1) / bays, ya, -.02), P(ln * (k + 1) / bays, yb - .7, -.02), P(ln * k / bays, yb - .7, -.02)), F['steel'], n)
                        run = k + 1
            for k in range(bays + 1):
                if k % 2 == 0:
                    a = ln * k / bays
                    g.face((P(a - .12, y0, .1), P(a + .12, y0, .1), P(a + .12, top, .1), P(a - .12, top, .1)), F['steel'], n)
        # the setback's ledge: a planted terrace round the body where it steps out
        if t > 0:
            pw = 3.0
            for (a0, a1, c0, c1) in ((x0, x1, z0, z0 + pw), (x0, x1, z1 - pw, z1), (x0, x0 + pw, z0 + pw, z1 - pw), (x1 - pw, x1, z0 + pw, z1 - pw)):
                g.face(((a0, top, c0), (a1, top, c0), (a1, top, c1), (a0, top, c1)), F['paving'], (0, 1, 0))
                lx = (a1 - a0) > (c1 - c0)
                n2 = int(((a1 - a0) if lx else (c1 - c0)) / 5)
                for k in range(n2):
                    f = (k + .5) / n2
                    px, pz = (a0 + (a1 - a0) * f, (c0 + c1) / 2) if lx else ((a0 + a1) / 2, c0 + (c1 - c0) * f)
                    if hashf(t, k, a0, c0) < .55:
                        cols = ('leaf-gold', 'leaf-light', 'leaf-dark') if hashf(t, k, 7) > .7 else ('leaf-light', 'leaf', 'leaf-dark')
                        blob(g, (px, top + 2.2, pz), 1.5 + hashf(t, k, 3), sk.Tier(2), k + t * 9, fins(cols), squash=(1, .8, 1), sides=7)
                        rod(g, (px, top, pz), (px, top + 1.6, pz), .14, F['bark'], 4)
                    else:
                        box(g, px - 1.6, px + 1.6, top, top + .5, pz - .7, pz + .7, F['leaf'], None, 0, cell=0, skip=('-y',))
        top = y0
    return g


def turbines(L):
    g = Geo()
    S = L['skyframe']
    for i, (x, y, z) in enumerate(S['turbines']):
        cx, cz = math.copysign(26, x), math.copysign(16, z)
        by = -9.5
        ibeam(g, (cx, by, cz), (x, by, z), .5, .9, F['steel'], sk.Tier(1))
        beam(g, (cx, by - 4.5, cz), (x, by - .5, z), .3, .3, F['steel'])
        box(g, x - .9, x + .9, by + .45, by + .7, z - .9, z + .9, F['steel'], None, 0, cell=0)
        lathe(g, [(.55, 0), (.5, 2), (.38, y - by - 1.2), (.34, y - by - .5)], (x, by + .6, z), F['white'], 10)
        # the nacelle: a rounded body along the wind, its nose to the rotor
        lathe(g, [(.3, -.45), (.55, -.2), (.62, .6), (.55, 1.8), (.3, 2.5), (.05, 2.7)], (x, y, z), F['white'], 10, axis=(1, 0, 0), cap0=True)
        blob(g, (x + .6, y + .78, z), .12, sk.Tier(2), i, fins(('beacon', 'beacon', 'beacon')), lumps=0, sides=6)
    return g


def skyline(L):
    g = Geo()
    cy = L['skyframe']['cloudY']
    for i in range(16):
        a = (i + hashf(401, i) * .7) / 16 * 2 * math.pi
        r = 240 + hashf(401, i, 1) * 520
        x, z = math.sin(a) * r, math.cos(a) * r
        w, d = 24 + hashf(401, i, 2) * 30, 20 + hashf(401, i, 3) * 26
        top = -18 + hashf(401, i, 4) * 78
        y0 = cy - 30
        t = Geo()
        glass = F['glass-dark'] if i % 3 else F['glass']
        box(t, -w / 2, w / 2, y0, top, -d / 2, d / 2, glass, None, 0, cell=0, skip=('-y',))
        nb = int((top - cy + 4) / 6)
        for fl in range(nb):
            yb = top - fl * 6.0
            for (o, u, n, ln) in (((-w / 2, 0, -d / 2), (1, 0, 0), (0, 0, -1), w), ((-w / 2, 0, d / 2), (1, 0, 0), (0, 0, 1), w), ((-w / 2, 0, -d / 2), (0, 0, 1), (-1, 0, 0), d), ((w / 2, 0, -d / 2), (0, 0, 1), (1, 0, 0), d)):
                def P(aa, y, out=0.0):
                    return (o[0] + u[0] * aa + n[0] * out, y, o[2] + u[2] * aa + n[2] * out)
                t.face((P(0, yb - 1, .2), P(ln, yb - 1, .2), P(ln, yb, .2), P(0, yb, .2)), F['coping'], n)
                bays = int(ln / 6)
                for k in range(bays):
                    if hashf(i, fl, k, n[0] * 2 + n[2]) > .72:
                        t.face((P(ln * k / bays + .4, yb - 5.2, .05), P(ln * (k + 1) / bays - .4, yb - 5.2, .05), P(ln * (k + 1) / bays - .4, yb - 1.4, .05), P(ln * k / bays + .4, yb - 1.4, .05)), F['window'], n)
        if i % 4 == 1:
            # still going up: a few storeys of bare frame and a crane on it
            for sx in (-1, 0, 1):
                for sz in (-1, 0, 1):
                    box(t, sx * w * .42 - .5, sx * w * .42 + .5, top, top + 14 - 4 * abs(sx * sz), sz * d * .42 - .5, sz * d * .42 + .5, F['primer'], None, 0, cell=0, skip=('-y',))
            for yy in (top + 5, top + 10):
                for sz in (-1, 0, 1):
                    box(t, -w * .42, w * .42, yy - .4, yy + .4, sz * d * .42 - .3, sz * d * .42 + .3, F['primer'], None, 0, cell=0)
                for sx in (-1, 1):
                    box(t, sx * w * .42 - .3, sx * w * .42 + .3, yy - .5, yy + .3, -d * .42, d * .42, F['primer'], None, 0, cell=0)
            box(t, -.9, .9, top, top + 30, -.9, .9, F['crane'], None, 0, cell=0)
            box(t, -1.2, 34, top + 30, top + 31.2, -.7, .7, F['crane'], None, 0, cell=0)
            box(t, -12, -1.2, top + 30, top + 31.2, -.7, .7, F['crane'], None, 0, cell=0)
            box(t, -11, -8, top + 28, top + 30, -1, 1, F['weight'], None, 0, cell=0)
            beacon = top + 33
        elif i % 4 == 2:
            # a roof garden under a glass canopy
            for k in range(6):
                blob(t, ((hashf(i, k) - .5) * w * .7, top + 2.5, (hashf(i, k, 1) - .5) * d * .7), 3 + 2 * hashf(i, k, 2), sk.Tier(2), i * 7 + k, fins(('leaf-light', 'leaf', 'leaf-dark')), squash=(1, .7, 1), sides=6)
            beacon = top + 7
        else:
            box(t, -w * .2, w * .2, top, top + 5, -d * .2, d * .2, F['stone'], None, 0, cell=0, skip=('-y',))
            box(t, -.4, .4, top + 5, top + 16, -.4, .4, F['steel'], None, 0, cell=0, skip=('-y',))
            beacon = top + 16.5
        blob(t, (0, beacon, 0), 1.0, sk.Tier(2), i, fins(('beacon', 'beacon', 'beacon')), lumps=0, sides=6)
        yaw = hashf(401, i, 5) * math.pi / 2
        c, s = math.cos(yaw), math.sin(yaw)
        g.extend(t.transformed([c, 0, s, x, 0, 1, 0, 0, -s, 0, c, z]))
    return g


def sea_height(x, z):
    p = np.array([[x * .004, 0.0, z * .004]])
    q = np.array([[x * .013 + 7, 3.0, z * .013 - 5]])
    w = np.array([[x * .045 - 3, 1.0, z * .045 + 11]])
    return (sk.fbm(p, 1.0, 3, 5)[0] - .5) * 30 + (sk.fbm(q, 1.0, 2, 9)[0] - .5) * 12 + (sk.fbm(w, 1.0, 2, 13)[0] - .5) * 5


def sea(L):
    """The cloud sea: a rolling sheet out to the horizon (rings widening with the distance), heaped islands standing on it."""
    g = Geo()
    cy = L['skyframe']['cloudY'] - 6
    sx, sy, sz = L['skyframe']['sun']
    radii = [0, 14, 28, 42, 56, 70, 85, 100, 116, 133, 151, 170, 190, 212, 236, 262, 292, 326, 365, 410, 462, 522, 592, 675, 772, 885, 1020, 1180, 1370, 1700]
    seg = 72
    H = {}
    def pt(i, k):
        k %= seg
        if (i, k) not in H:
            r = radii[i]
            a = 2 * math.pi * k / seg
            x, z = math.sin(a) * r, math.cos(a) * r
            H[(i, k)] = (x, cy + sea_height(x, z) - (12 if r > 1500 else 0), z)
        return H[(i, k)]
    def nrm(i, k):
        a, b = pt(min(i + 1, len(radii) - 1), k), pt(max(i - 1, 0), k)
        c, d = pt(i, k + 1), pt(i, k - 1)
        u = (a[0] - b[0], a[1] - b[1], a[2] - b[2])
        v = (c[0] - d[0], c[1] - d[1], c[2] - d[2])
        n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
        l = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2) or 1
        n = (n[0] / l, n[1] / l, n[2] / l)
        return n if n[1] > 0 else (-n[0], -n[1], -n[2])
    for i in range(len(radii) - 1):
        for k in range(seg):
            pts = [pt(i, k), pt(i, k + 1), pt(i + 1, k + 1), pt(i + 1, k)]
            ns = [nrm(i, k), nrm(i, k + 1), nrm(i + 1, k + 1), nrm(i + 1, k)]
            if i == 0:
                pts, ns = pts[1:], ns[1:]
            my = sum(p[1] for p in pts) / len(pts) - cy
            lit = sum(n[0] * sx + n[1] * sy + n[2] * sz for n in ns) / len(ns)
            f = F['cloud-mid']
            g.face(pts, f, normals=ns)
    shapes = [props.cloud(v) for v in range(4)]
    for i in range(120):
        a = hashf(431, i) * 2 * math.pi
        r = 48 + (hashf(431, i, 1) ** 1.5) * 1150
        x, z = math.sin(a) * r, math.cos(a) * r
        if abs(z) > 70 and abs(z) < 190 and r < 520:
            continue                                   # the drifting islands' lanes stay clear
        hx = (13 + hashf(431, i, 2) * 30) * (1 + r / 260)
        hy = hx * (.24 + .2 * hashf(431, i, 3))
        hz = hx * (.6 + .3 * hashf(431, i, 4))
        yaw = hashf(431, i, 5) * 6.28
        c, s = math.cos(yaw), math.sin(yaw)
        y = cy + sea_height(x, z) + hy * .55
        g.extend(shapes[i % 4].transformed([c * hx, 0, s * hz, x, 0, hy, 0, y, -s * hx, 0, c * hz, z]))
    return g


def banks(L):
    """The great banks on the horizon: towers of cloud catching the last of the sun."""
    g = Geo()
    cy = L['skyframe']['cloudY']
    T = sk.Tier(2)
    for i in range(20):
        a = (i + hashf(441, i) * .6) / 20 * 2 * math.pi
        r = 1050 + hashf(441, i, 1) * 450
        x, z = math.sin(a) * r, math.cos(a) * r
        w = 130 + hashf(441, i, 2) * 150
        h = 70 + hashf(441, i, 3) * 190
        n = 5 + int(hashf(441, i, 4) * 3)
        for k in range(n):
            f = k / n
            rr = w * (1 - f * .55) * (.55 + .3 * hashf(441, i, 10 + k))
            blob(g, (x + (hashf(441, i, 20 + k) - .5) * w * .8, cy + h * f * .9 + rr * .2, z + (hashf(441, i, 30 + k) - .5) * w * .8), rr, T, i * 11 + k, fins(('cloud-top', 'cloud-mid', 'cloud-low')), squash=(1.25, .7, 1.1), lumps=.3, freq=1.2, sides=9)
    return g


def far(L):
    return [('tower', tower(L)), ('turbines', turbines(L)), ('skyline', skyline(L)), ('sea', sea(L)), ('banks', banks(L))]


def bank_far(sb, L):
    for name, g in far(L):
        bk = sk.Bake(g, None, floors=(), seed=5)
        sb.add('static/%s/2' % name, 'static', sk.to_mesh('static/%s/2' % name, g, bk))
