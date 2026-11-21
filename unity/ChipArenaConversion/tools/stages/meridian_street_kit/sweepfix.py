"""The Meridian fix for the shared stage tool's inside-out pipes and tubes (owner, 2026-11-19: fix them for the Meridian
pieces now, not the older stages).

The fault, measured: stagekit.sweep gives a swept section's sides INWARD normals whichever way the section winds (its sign is
-1 for a counter-clockwise outline, whose (dy, -dx) already points out, and +1 for a clockwise one, whose (dy, -dx) points in),
and Geo.face winds every face to agree with its normal. So every side face of stagekit.sweep and stagekit.tube faces into the
section (a test tube: 8 of 8 faces inward), while its end caps face out. StageLit culls back faces, so in the game such a pipe
shows only the far half of its own inside: thin, hollow and wrongly lit. Blender's board renders hide it, because Blender
draws back faces and flips their normals toward the eye.

`outward_sweep` is stagekit.sweep with the sign corrected (same arguments, `path_normals` included): every side faces out of
the section, whichever way the section winds. `install()` puts it in place of stagekit.sweep for this process only, so
stagekit.tube (which looks `sweep` up in its own module) and every Meridian script that calls sk.sweep or sk.tube build
outward pipes. The older stages are untouched: stagekit.py is not edited, and only a process that installs this sees it.

For the repository (not done here: scratch only): give stagekit.sweep an `outward=False` argument with this sign, have
city_kit.py (the Meridian scripts' shared kit) pass outward=True, and leave the older stages' banks byte-identical.

`expected(section, path, ...)` rebuilds a sweep's rings and each side's true outward direction (the section edge's outward
normal, from the outline's own winding, so it holds for any simple outline, convex or not): audit.py checks emitted faces
against it.
"""
import math

import stagekit as sk

_sub, _add, _cross, _dot, _norm, _lerp = sk._sub, sk._add, sk._cross, sk._dot, sk._norm, sk._lerp
ORIGINAL_SWEEP = sk.sweep


def _frames(path, closed, up, path_normals):
    """stagekit.sweep's frames: rotation minimising (double reflection) from `up`, or from per-point normals."""
    m = len(path)
    T = []
    for i in range(m):
        a = path[(i - 1) % m] if (closed or i > 0) else path[i]
        b = path[(i + 1) % m] if (closed or i < m - 1) else path[i]
        T.append(_norm(_sub(b, a)))
    frames = []
    if path_normals is not None:
        for i in range(m):
            v = _norm(path_normals[i])
            u = _norm(_cross(v, T[i]))
            v = _cross(T[i], u)
            frames.append((u, v))
        return T, frames
    ref = up if up is not None else ((0, 1, 0) if abs(T[0][1]) < .95 else (1, 0, 0))
    u = _norm(_cross(ref, T[0]))
    v = _cross(T[0], u)
    frames.append((u, v))
    for i in range(1, m):
        v1 = _sub(path[i], path[i - 1])
        c1 = _dot(v1, v1)
        if c1 < 1e-14:
            frames.append(frames[-1])
            continue
        uL = _add(u, v1, -2.0 / c1 * _dot(v1, u))
        tL = _add(T[i - 1], v1, -2.0 / c1 * _dot(v1, T[i - 1]))
        v2 = _sub(T[i], tL)
        c2 = _dot(v2, v2)
        u = _add(uL, v2, -2.0 / c2 * _dot(v2, uL)) if c2 > 1e-14 else uL
        u = _norm(u)
        v = _cross(T[i], u)
        frames.append((u, v))
    return T, frames


def _winding(section):
    """+1 for a counter-clockwise outline (positive area), -1 for a clockwise one."""
    k = len(section)
    area = 0.0
    for j in range(k):
        a, b = section[j], section[(j + 1) % k]
        area += a[0] * b[1] - b[0] * a[1]
    return 1.0 if area > 0 else -1.0


def outward_sweep(g, section, path, f, closed=False, up=None, caps=True, smooth_u=True, scale=None, path_normals=None):
    """stagekit.sweep with every side facing OUT of the section (see the module's note)."""
    m = len(path)
    if m < 2:
        return
    T, frames = _frames(path, closed, up, path_normals)
    k = len(section)
    sgn = _winding(section)                 # (dy, -dx) of a counter-clockwise outline points out of it
    sn = []
    for j in range(k):
        a = section[(j - 1) % k]
        b = section[(j + 1) % k]
        d = (b[0] - a[0], b[1] - a[1])
        sn.append(_norm((sgn * d[1], -sgn * d[0], 0.0)))
    rings, nrings = [], []
    for i in range(m):
        u, v = frames[i]
        s = scale[i] if scale is not None else 1.0
        ring, nr = [], []
        for j in range(k):
            x, y = section[j]
            ring.append(_add(_add(path[i], u, x * s), v, y * s))
            nr.append(_norm(_add(_lerp((0, 0, 0), u, sn[j][0]), v, sn[j][1])))
        rings.append(ring)
        nrings.append(nr)
    count = m if closed else m - 1
    for i in range(count):
        i1 = (i + 1) % m
        for j in range(k):
            j1 = (j + 1) % k
            pts = (rings[i][j], rings[i][j1], rings[i1][j1], rings[i1][j])
            if smooth_u:
                g.face(pts, f, normals=(nrings[i][j], nrings[i][j1], nrings[i1][j1], nrings[i1][j]))
            else:
                # the side's own outward normal: the section edge's, between its two corners, in both rings' frames
                ex, ey = section[j1][0] - section[j][0], section[j1][1] - section[j][1]
                el = math.hypot(ex, ey) or 1.0
                nx, ny = sgn * ey / el, -sgn * ex / el
                (u0, v0), (u1, v1) = frames[i], frames[i1]
                g.face(pts, f, normal=_norm(_add(_add(_lerp((0, 0, 0), u0, nx), v0, ny), _add(_lerp((0, 0, 0), u1, nx), v1, ny))))
    if caps and not closed:
        g.face(rings[0], f, normal=(-T[0][0], -T[0][1], -T[0][2]))
        g.face(rings[-1], f, normal=T[-1])


def install():
    """Use outward_sweep for stagekit.sweep (and so stagekit.tube) in this process. Returns the original."""
    sk.sweep = outward_sweep
    return ORIGINAL_SWEEP


def uninstall():
    sk.sweep = ORIGINAL_SWEEP


def expected(section, path, closed=False, up=None, caps=True, scale=None, path_normals=None):
    """The faces a sweep of these arguments should emit, in its order: [(centroid, outward direction)] for each side face
    (from the outline's own winding, not from any normal the sweep computes), then the caps."""
    m = len(path)
    if m < 2:
        return []
    T, frames = _frames(path, closed, up, path_normals)
    k = len(section)
    sgn = _winding(section)
    rings = []
    for i in range(m):
        u, v = frames[i]
        s = scale[i] if scale is not None else 1.0
        rings.append([_add(_add(path[i], u, x * s), v, y * s) for (x, y) in section])
    out = []
    count = m if closed else m - 1
    for i in range(count):
        i1 = (i + 1) % m
        for j in range(k):
            j1 = (j + 1) % k
            pts = (rings[i][j], rings[i][j1], rings[i1][j1], rings[i1][j])
            c = tuple(sum(p[q] for p in pts) / 4 for q in range(3))
            ex, ey = section[j1][0] - section[j][0], section[j1][1] - section[j][1]
            el = math.hypot(ex, ey) or 1.0
            nx, ny = sgn * ey / el, -sgn * ex / el
            (u0, v0), (u1, v1) = frames[i], frames[i1]
            d = _add(_add(_lerp((0, 0, 0), u0, nx), v0, ny), _add(_lerp((0, 0, 0), u1, nx), v1, ny))
            out.append((c, _norm(d), 'side'))
    if caps and not closed:
        c0 = tuple(sum(p[q] for p in rings[0]) / k for q in range(3))
        c1 = tuple(sum(p[q] for p in rings[-1]) / k for q in range(3))
        out.append((c0, (-T[0][0], -T[0][1], -T[0][2]), 'cap'))
        out.append((c1, T[-1], 'cap'))
    return out
