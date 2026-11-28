# sculpted.py: the Sculpted face gear (the owner's pick, 2026-11-27, of two new models captured in game): helmet, half mask, visor,
# mask and ear pods for the Chip Arena pilot, modelled from scratch in Blender (5.x, headless: run_model.py; docs/PILOT-BODY-HAIR.md 6).
# Frame: Blender metres, the head faces -Y, up +Z. Every surface is a parametric plate laid over a smooth envelope of the skull.
# (Refined from m-sculpted after the panel review: Corinthian lenses + white bar over a METAL muzzle, crown spine line, wrap-round
# cheek shells to a back seam with a nape node, hidden neck seal, teardrop ear pivots; the family rebuilt in the same language.)
import bpy, bmesh, math
from mathutils import Vector, kdtree
from mathutils.bvhtree import BVHTree
import blhelp

D2R = math.pi / 180
C = Vector((0.0, -0.025, 0.075))          # the envelope's centre (inside the skull)

head = bpy.data.objects['REF head']
HV = [head.matrix_world @ v.co for v in head.data.vertices]
BVH = BVHTree.FromPolygons(HV, [tuple(p.vertices) for p in head.data.polygons])


# ---------------------------------------------------------------- directions
def dAE(a, e):
    """azimuth a (0 = front -Y, + toward +X), elevation e (deg)"""
    a *= D2R; e *= D2R
    return Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))


def dSL(s, l):
    """sagittal s (0 front, 90 up, 180 back), lateral l (+X) (deg)"""
    s *= D2R; l *= D2R
    return Vector((math.sin(l), -math.cos(l) * math.cos(s), math.cos(l) * math.sin(s)))


def ae_of(d):
    return math.atan2(d.x, -d.y) / D2R, math.asin(max(-1, min(1, d.z))) / D2R


def sl_of(d):
    return math.atan2(d.z, -d.y) / D2R, math.asin(max(-1, min(1, d.x))) / D2R


def curve(keys):
    """smooth Catmull-Rom through sorted (x, y) keys, clamped at the ends"""
    xs = [k[0] for k in keys]; ys = [k[1] for k in keys]

    def f(x):
        if x <= xs[0]: return ys[0]
        if x >= xs[-1]: return ys[-1]
        i = max(j for j in range(len(xs) - 1) if xs[j] <= x)
        x0, x1 = xs[i], xs[i + 1]; t = (x - x0) / (x1 - x0)
        y0, y1 = ys[i], ys[i + 1]
        m0 = (ys[i + 1] - ys[i - 1]) / (xs[i + 1] - xs[i - 1]) * (x1 - x0) if i > 0 else (y1 - y0)
        m1 = (ys[i + 2] - ys[i]) / (xs[i + 2] - xs[i]) * (x1 - x0) if i + 2 < len(xs) else (y1 - y0)
        t2, t3 = t * t, t * t * t
        return (2 * t3 - 3 * t2 + 1) * y0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * y1 + (t3 - t2) * m1
    return f


def smooth01(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- the skull envelope (morphological closing of the head)
def fib(n):
    ga = math.pi * (3 - math.sqrt(5)); out = []
    for i in range(n):
        z = 1 - 2 * (i + .5) / n; r = math.sqrt(1 - z * z); th = ga * i
        out.append(Vector((r * math.cos(th), r * math.sin(th), z)))
    return out


def last_hit(o, d, bvh):
    best = None; org = o.copy()
    for _ in range(10):
        loc, nrm, idx, dist = bvh.ray_cast(org, d)
        if loc is None: break
        best = (loc - o).length; org = loc + d * 1e-5
    return best


# the head's convex hull: no eye sockets, nostrils or lips in the envelope (a sleek face front)
_bm = bmesh.new()
for v in HV: _bm.verts.new(v)
_res = bmesh.ops.convex_hull(_bm, input=_bm.verts)
for v in [v for v in _bm.verts if not v.link_faces]: _bm.verts.remove(v)
_bm.verts.ensure_lookup_table()
HULL = BVHTree.FromBMesh(_bm)
DIRS = fib(5000)
H = [last_hit(C, d, HULL) for d in DIRS]
KD = kdtree.KDTree(len(DIRS))
for i, d in enumerate(DIRS): KD.insert(d, i)
KD.balance()


def chord(deg): return 2 * math.sin(deg * D2R / 2)


WIN, SIG = 9.0, 6.0
HD = []
for i, d in enumerate(DIRS):
    vals = [H[j] for (_, j, _) in KD.find_range(d, chord(WIN)) if H[j] is not None]
    HD.append(max(vals) if vals else None)
# a second, wider smoothing of the dilated field (lumps out), never more than .7 mm under it
NB = []
for i, d in enumerate(DIRS):
    lst = []
    for (co, j, dist) in KD.find_range(d, chord(2.4 * SIG)):
        ang = 2 * math.asin(min(1, dist / 2)) / D2R
        lst.append((j, math.exp(-(ang / SIG) ** 2)))
    NB.append(lst)


def blur(vals):
    out = []
    for i in range(len(vals)):
        if vals[i] is None: out.append(None); continue
        sw = sv = 0.0
        for j, w in NB[i]:
            if vals[j] is not None: sw += w; sv += w * vals[j]
        out.append(sv / sw)
    return out


B1 = blur(HD)
B2 = blur(B1)
HD = [None if b1 is None else max(b2, b1 - .0007) for b1, b2 in zip(B1, B2)]
_cache = {}
SIG_I = 3.5


def env(d):
    """envelope radius along unit direction d (smooth, >= the hull)"""
    key = (round(d.x, 5), round(d.y, 5), round(d.z, 5))
    r = _cache.get(key)
    if r is not None: return r
    sw = sv = 0.0
    for (co, j, dist) in KD.find_range(d, chord(2.5 * SIG_I)):
        if HD[j] is None: continue
        ang = 2 * math.asin(min(1, dist / 2)) / D2R
        w = math.exp(-(ang / SIG_I) ** 2); sw += w; sv += w * HD[j]
    if sw < 1e-6:
        (co, j, dist) = min(((c, j, dd) for (c, j, dd) in KD.find_n(d, 60) if HD[j] is not None), key=lambda t: t[2])
        r = HD[j]
    else:
        r = sv / sw
    _cache[key] = r
    return r


CLR = .0038        # the under-layer's clearance over the envelope
EAR_D = (Vector((.108, -.014, .061)) - C).normalized()
EAR_CUT = .0028    # thinner over the ears (the ears' outer faces are exempt from clearance; keeps the helmet narrow)


CHIN_D = (Vector((0, -.12, -.045)) - C).normalized()
NOSE_D = (Vector((0, -.151, .033)) - C).normalized()


def clr(d):
    ed = EAR_D.copy(); ed.x = math.copysign(ed.x, d.x)
    ang = math.acos(max(-1, min(1, d.dot(ed)))) / D2R
    angc = math.acos(max(-1, min(1, d.dot(CHIN_D)))) / D2R
    angn = math.acos(max(-1, min(1, d.dot(NOSE_D)))) / D2R
    return CLR - EAR_CUT * math.exp(-(ang / 40.0) ** 2) + .0010 * math.exp(-(angc / 14.0) ** 2) + .0016 * math.exp(-(angn / 12.0) ** 2)


SWEEP = .0014


def sweep(d):
    """the shell swept back: extra radius over the upper back of the skull (an aero teardrop profile)"""
    s, l = sl_of(d)
    if s < 0: s += 360
    g = math.exp(-((s - 148.0) / 38.0) ** 2)
    return SWEEP * g * math.cos(l * D2R) ** 2


def P(d, layer):
    return C + d * (env(d) + clr(d) + sweep(d) + layer)


# ---------------------------------------------------------------- mesh helpers
_sba = None


def smooth_by_angle_group():
    global _sba
    if _sba: return _sba
    ng = bpy.data.node_groups.new('SBA', 'GeometryNodeTree')
    ng.interface.new_socket('Geometry', in_out='INPUT', socket_type='NodeSocketGeometry')
    ng.interface.new_socket('Geometry', in_out='OUTPUT', socket_type='NodeSocketGeometry')
    n = ng.nodes; L = ng.links
    gi = n.new('NodeGroupInput'); go = n.new('NodeGroupOutput')
    s1 = n.new('GeometryNodeSetShadeSmooth'); s1.domain = 'FACE'
    s2 = n.new('GeometryNodeSetShadeSmooth'); s2.domain = 'EDGE'
    ea = n.new('GeometryNodeInputMeshEdgeAngle')
    cmp = n.new('FunctionNodeCompare'); cmp.data_type = 'FLOAT'; cmp.operation = 'GREATER_THAN'
    cmp.inputs['B'].default_value = 32 * D2R
    s1.inputs['Shade Smooth'].default_value = True
    s2.inputs['Shade Smooth'].default_value = False
    L.new(gi.outputs[0], s1.inputs['Geometry']); L.new(s1.outputs[0], s2.inputs['Geometry'])
    L.new(ea.outputs['Unsigned Angle'], cmp.inputs['A']); L.new(cmp.outputs[0], s2.inputs['Selection'])
    L.new(s2.outputs[0], go.inputs[0])
    _sba = ng
    return ng


def finish(ob, thick=None, offset=1.0, bev=.0007, seg=2, sba=True, rim_only=False):
    if thick:
        m = ob.modifiers.new('sol', 'SOLIDIFY'); m.thickness = thick; m.offset = offset; m.use_even_offset = True; m.use_rim = True
        m.use_rim_only = rim_only
    if bev:
        m = ob.modifiers.new('bev', 'BEVEL'); m.width = bev; m.segments = seg; m.limit_method = 'ANGLE'; m.angle_limit = 40 * D2R
    if sba:
        m = ob.modifiers.new('sba', 'NODES'); m.node_group = smooth_by_angle_group()
    return ob


def grid_obj(name, G, paint='SHELL', paints=None, closed_u=False):
    """G[i][j]: positions (rows along u, columns along v); faces wound to face away from C."""
    nu, nv = len(G), len(G[0])
    verts = [tuple(p) for row in G for p in row]
    faces = []
    iu = nu if closed_u else nu - 1
    for i in range(iu):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            faces.append((i * nv + j, i2 * nv + j, i2 * nv + j + 1, i * nv + j + 1))
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    # orientation: outward = away from C
    s = 0.0
    for p in me.polygons:
        s += p.normal.dot(p.center - C)
    if s < 0:
        for p in me.polygons: p.flip()
        me.update()
    names = paints[1] if paints else [paint]
    for nm in names: me.materials.append(blhelp.mat(nm))
    if paints:
        fnc = paints[0]
        for k, p in enumerate(me.polygons):
            i, j = divmod(k, nv - 1)
            p.material_index = fnc(i, j)
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def plate(name, fn, nu, nv, layer, thick, paint='SHELL', bev=.0007, seg=2, closed_u=False, paints=None, offset=1.0, rim_only=True, pos=None):
    """a plate from layer to layer + thick over the envelope; rim_only: just the top surface and its edge walls (the hidden
    underside is left out)"""
    G = []
    lift = thick if (rim_only and thick and offset > 0) else 0.0
    for i in range(nu):
        u = i / (nu if closed_u else nu - 1)
        G.append([(pos or P)(fn(u, j / (nv - 1)), (layer(u, j / (nv - 1)) if callable(layer) else layer) + lift) for j in range(nv)])
    ob = grid_obj(name, G, paint, paints, closed_u)
    if lift:
        return finish(ob, thick, -1.0, bev, seg, rim_only=True)
    return finish(ob, thick, offset, bev, seg)


def lerp(a, b, t): return a + (b - a) * t


# ================================================================ HELMET
# Key curves (degrees, a/e frame). The visor is a wide almond split by the central rib; its tips sweep back to the temples.
# (the head's eye openings sit at Z .055-.088: the lenses span Z ~.05-.10 at the eye columns)
TIP_A, TIP_E = 80.0, 0.5
vis_top = curve([(0, 6.8), (10, 8.6), (26, 9.7), (44, 9.4), (58, 7.4), (70, 4.2), (TIP_A, TIP_E)])
vis_bot = curve([(0, -11.4), (12, -11.3), (28, -10.6), (46, -8.3), (62, -4.6), (TIP_A, TIP_E)])
BOT = curve([(0, -58.6), (6, -58.4), (16, -58.8), (40, -60.6), (70, -63.0), (95, -63.5), (125, -63.0), (155, -62.0), (180, -61.0)])   # helmet's lower edge
PEAK = 3.5


def cp_front_e(a):
    return vis_top(abs(a)) - PEAK * (1 - smooth01(abs(a) / 11.0))


# --- cranial plate: from the visor's top edge over the crown, tapering to a point at the nape (sagittal frame)
NCOL = 41
front_sl = []
for j in range(NCOL):
    a = lerp(-TIP_A + .5, TIP_A - .5, j / (NCOL - 1))
    front_sl.append(sl_of(dAE(a, cp_front_e(a))))
S_END = 203.5
# the cranial plate's lateral edge, drawn explicitly: from the visor tip it sweeps up over the ear and down to the nape
cpe = curve([(TIP_A, TIP_E), (88, 5.5), (98, 10.5), (112, 14.0), (128, 13.0), (145, 6.0), (160, -5.0), (172, -16.5), (180, -23.0)])


def _cp_w_table():
    s_tip, l_tip = front_sl[-1]
    out = []
    for i in range(61):
        u = i / 60
        s = lerp(s_tip, S_END, u)
        lo, hi = 0.0, 89.5
        for _ in range(40):
            m = (lo + hi) / 2
            a, e = ae_of(dSL(s, m))
            if e - cpe(abs(a)) > 0: lo = m
            else: hi = m
        out.append((u, max(.02, min(1.2, lo / l_tip))))
    out[0] = (0.0, 1.0)
    return out


cp_w = curve(_cp_w_table())


def cp_dir(u, v):
    k = v * (NCOL - 1); j = min(int(k), NCOL - 2); t = k - j
    s0 = lerp(front_sl[j][0], front_sl[j + 1][0], t); l0 = lerp(front_sl[j][1], front_sl[j + 1][1], t)
    return dSL(lerp(s0, S_END, u), l0 * cp_w(u))


L_UNDER = 0.0
L_CG, T_CG = .0010, .0026
L_NP, T_NP = .0018, .0026
L_CW, T_CW = .0026, .0025
L_CP, T_CP = .0036, .0032


def line(name, pts, w0, w1, layer, thick, paint='LIGHT', pos=None, shift=None):
    """a thin strip along pts [(a, e)], widened sideways (degrees) from w0 to w1 along the curve's left normal"""
    G = []
    n = len(pts)
    for i in range(n):
        a0, e0 = pts[max(0, i - 1)]; a1, e1 = pts[min(n - 1, i + 1)]
        ce = max(.2, math.cos(pts[i][1] * D2R))
        ta, te = (a1 - a0) * ce, e1 - e0
        ln = math.hypot(ta, te) or 1; na, ne = -te / ln, ta / ln
        row = []
        sh = shift(i) if shift else 0.0
        for w in (w0 + sh, (w0 + w1) / 2 + sh, w1 + sh):
            row.append((pos or P)(dAE(pts[i][0] + na * w / ce, pts[i][1] + ne * w), layer))
        G.append(row)
    return finish(grid_obj(name, G, paint), thick, 1.0, 0, 1)


# ---------------------------------------------------------------- teardrop pods (ear pods, the half mask's anchors, the helmet's ear pivots)
def tear_mesh(name, O, t, up, n, r0, tail, h0, base_h, paint, nr=9, nphi=40):
    """a polar dome over the plane through O (normal n): round at the front (-t), pointed at the tail (+t)"""
    bm = bmesh.new()
    apex = bm.verts.new(O + n * (base_h + h0))
    rings = []
    for k in range(1, nr + 1):
        rho = k / nr
        ring = []
        for m in range(nphi):
            ph = 2 * math.pi * m / nphi
            c = math.cos(ph)
            R = r0 * (1 + (tail / r0 - 1) * max(0.0, c) ** 6)
            x, y = R * rho * c, R * rho * math.sin(ph)
            hh = base_h + h0 * math.sqrt(max(0.0, 1 - rho * rho)) * (1 - .35 * rho * max(0.0, c))
            ring.append(bm.verts.new(O + t * x + up * y + n * hh))
        rings.append(ring)
    for m in range(nphi):
        bm.faces.new((apex, rings[0][m], rings[0][(m + 1) % nphi]))
    for k in range(nr - 1):
        for m in range(nphi):
            m2 = (m + 1) % nphi
            bm.faces.new((rings[k][m], rings[k + 1][m], rings[k + 1][m2], rings[k][m2]))
    bm.normal_update()
    if sum(f.normal.dot(n) for f in bm.faces) < 0:
        for f in bm.faces: f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(blhelp.mat(paint))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def pod_frame(n, tilt):
    """n: outward normal; the tail points back (+Y) and up by tilt degrees, in the plane normal to n"""
    back = Vector((0, math.cos(tilt * D2R), math.sin(tilt * D2R)))
    t = (back - n * back.dot(n)).normalized()
    up = n.cross(t).normalized()
    if up.z < 0: up = -up
    return t, up


def pod2(prefix, side, O, n, r0, L, h_base, h_shell, tilt=20.0, light=True, base_off=.0032, ring=(.80, .87, .94), nphi=40, slit_w=.0006):
    t, up = pod_frame(n, tilt)
    finish(tear_mesh(prefix + ' base ' + side, O, t, up, n, r0, L, h_base, base_off, 'METAL', nphi=nphi), .0016, -1.0, .0005, 2, rim_only=True)
    O2 = O - t * .0012
    sb = base_off + h_base * .45
    finish(tear_mesh(prefix + ' shell ' + side, O2, t, up, n, r0 - .0024, L - .0038, h_shell, sb, 'SHELL', nphi=nphi),
           .0018, -1.0, .0005, 2, rim_only=True)
    # a glowing outline round the shell, on the base's rim (the teardrop drawn in light)
    G = []
    for m in range(48):
        ph = 2 * math.pi * m / 48; c = math.cos(ph)
        R = r0 * (1 + (L / r0 - 1) * max(0.0, c) ** 6)
        row = []
        for rho in ring:
            hh = base_off + h_base * math.sqrt(max(0.0, 1 - rho * rho)) * (1 - .35 * rho * max(0.0, c)) + .00035
            row.append(O + t * (R * rho * c) + up * (R * rho * math.sin(ph)) + n * hh)
        G.append(row)
    ob = grid_obj(prefix + ' ring ' + side, G, 'LIGHT', closed_u=True)
    if sum(p.normal.dot(n) for p in ob.data.polygons) < 0:
        for p in ob.data.polygons: p.flip()
    finish(ob, .0006, -1.0, 0, 1, rim_only=True)
    if light:
        G = []
        for i in range(14):
            x = lerp(-(r0 - .0024) * .45, (L - .0038) * .6, i / 13)
            rho = abs(x) / ((r0 - .0024) if x < 0 else (L - .0038))
            row = []
            for y in (-slit_w, 0, slit_w):
                hh = sb + h_shell * math.sqrt(max(0.0, 1 - rho * rho)) * (1 - .35 * rho * (x > 0)) + .0003
                row.append(O2 + t * x + up * y + n * hh)
            G.append(row)
        ob = grid_obj(prefix + ' slit ' + side, G, 'LIGHT')
        if sum(p.normal.dot(n) for p in ob.data.polygons) < 0:
            for p in ob.data.polygons: p.flip()
        finish(ob, .0006, -1.0, 0, 1, rim_only=True)


def pod(prefix, sgn, O, r0, L, h_base, h_shell, tilt=20.0, light=True):
    pod2(prefix, 'R' if sgn > 0 else 'L', O, Vector((sgn, 0, 0)), r0, L, h_base, h_shell, tilt, light)


# ================================================================ HELMET
FACE = 'metal'       # 'metal': a white bar under the lenses over a dark muzzle; 'white': one white faceplate inside the V
BAR_H = 2.7          # the bar's height under the lenses at the centre (deg)
G_SP = 1.0           # the crown spine groove's half-width (lateral degrees)
BG = .75             # the back seam's half-width (azimuth degrees)
GP = .85             # the V grooves' half-gap (deg, across the line)
L_TIP = abs(front_sl[-1][1])
# the white shells' lower edge: higher than the rim at the jaw, so the dark under-layer shows as a neck trim that slims the jaw
def BOTW(a): return BOT(a) + 1.0 + 1.2 * smooth01((a - 8) / 30)


def groove_w(u):
    """the spine groove's half-width: opens above the brow gem, narrows with the plate into the nape point"""
    return min(G_SP * smooth01((u - .045) / .03), .40 * L_TIP * cp_w(u))


def cp_half(u, v, sgn):
    """one half of the cranial plate (v = 0 at the spine groove, 1 at the lateral edge)"""
    vf = .5 + .5 * v if sgn > 0 else .5 - .5 * v
    k = vf * (NCOL - 1); j = min(int(k), NCOL - 2); t = k - j
    s0 = lerp(front_sl[j][0], front_sl[j + 1][0], t); l0 = lerp(front_sl[j][1], front_sl[j + 1][1], t)
    lmax = L_TIP * cp_w(u)
    l = abs(l0) * cp_w(u); g = groove_w(u)
    l = g + l * (1 - g / max(lmax, 1e-6))
    return dSL(lerp(s0, S_END, u), sgn * l)


def strip(name, fn, nu, nv, layer, thick, paint, bev=0.0, seg=1):
    return plate(name, fn, nu, nv, layer, thick, paint, bev=bev, seg=seg)


def diamond(prefix, center_ae, gh, gw, base, scale=.66, rise=.0014, n_u=13, pos=None):
    """a faceted cyan diamond in a dark bezel (a/e centre, half height gh, half width gw, degrees)"""
    a0, e0 = center_ae

    def gd(u, v, k):
        w = gw * k * (1 - abs(2 * u - 1)) + .06
        return dAE(a0 + lerp(-w, w, v), e0 - gh * k + 2 * gh * k * u)
    plate(prefix + ' bezel', lambda u, v: gd(u, v, 1.0), n_u, 7, base, .0012, 'METAL', bev=0, seg=1, pos=pos)
    plate(prefix + ' gem', lambda u, v: gd(u, v, scale), n_u, 9,
          lambda u, v: base + .0012 + rise * (1 - max(abs(2 * u - 1), abs(2 * v - 1))), .0004, 'LIGHT', bev=0, rim_only=False, pos=pos)


def deriv(f, x, h=.05):
    return (f(x + h) - f(x - h)) / (2 * h)


def build_helmet():
    # under-layer (METAL): one shell from the lower edge up to the crown
    def us_dir(u, v):
        a = -180 + 360 * u
        return dAE(a, lerp(BOT(abs(a)) - 1.0, 89.0, v))
    plate('Helmet: under', us_dir, 64, 26, L_UNDER - .0012, .0012, 'METAL', bev=0, closed_u=True)

    # visor glass (DARK)
    def vg_dir(u, v):
        a = lerp(-TIP_A - 2, TIP_A + 2, u)
        return dAE(a, lerp(vis_bot(abs(a)) - 3, vis_top(abs(a)) + 2, v))
    plate('Helmet: visor glass', vg_dir, 49, 10, .0004, .0008, 'DARK', bev=0)

    # cranial plate (SHELL), in two halves either side of the spine groove
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        plate("Helmet: cranial " + nm, lambda u, v, s=sgn: cp_half(u, v, s), 54, 26, L_CP, T_CP, 'SHELL', bev=.0009, seg=2)

    # the spine: a dark groove floor with a cyan line, from the brow gem over the crown into the nape point
    s_c0 = front_sl[NCOL // 2][0]
    us = [i / 600 for i in range(601)]
    ua = next(u for u in us if groove_w(u) > .25 * G_SP)
    ub = max(u for u in us if groove_w(u) > .06)

    def sp_dir(u, v, wf, ext, cap):
        uu = lerp(ua, ub, u); g = groove_w(uu)
        w = min(cap, g * wf + ext)
        return dSL(lerp(s_c0, S_END, uu), lerp(-w, w, v))
    strip('Helmet: spine floor', lambda u, v: sp_dir(u, v, 1.0, .35, 9), 72, 3, L_CP + T_CP - .0026, .0006, 'METAL')
    strip('Helmet: spine line', lambda u, v: sp_dir(u, v, .75, 0, .46), 72, 3, L_CP + T_CP - .0020, .0005, 'LIGHT')

    # the cranial plate's lateral edge in a/e
    edge = [ae_of(cp_dir(i / 69, 1.0)) for i in range(70)] + [ae_of(cp_dir(1.0, 1 - .5 * i / 12)) for i in range(1, 13)]
    edge = [p for p in edge if p[0] >= TIP_A - .01]
    cp_edge_pts = list(edge)
    edge = sorted(edge)
    cp_edge = curve(edge)
    GAP = 4.2

    def side_top(a):
        return cp_edge(a) - GAP * smooth01((a - TIP_A) / 18)

    # ---- the face. The cheek shells' front edges (fb) rise from the chin point and curve out into the lens bottoms; between
    # them and the faceplate run dark grooves with cyan lines (a V that flows into the visor's lower outline)
    E_APEX = BOT(0) + 4.5
    A_V = 39.0
    fb = curve([(0, E_APEX), (8, E_APEX + 12.5), (16, E_APEX + 25.5), (22, E_APEX + 34.0), (27, vis_bot(27) - 4.6),
                (31, vis_bot(31) - 1.7), (35, vis_bot(35) - .35), (A_V, vis_bot(A_V))])

    def gvh(a):
        """the vertical half-gap giving a constant gap across the curve, closing where it joins the lens line"""
        a = min(max(a, .05), A_V)
        k = deriv(fb, a) / max(.3, math.cos(fb(a) * D2R))
        return GP * math.sqrt(1 + k * k) * (1 - smooth01((a - (A_V - 5)) / 5))

    # V groove floors (METAL)
    def vf_dir(u, v, sgn):
        a = lerp(0, A_V, u)
        g = gvh(a) + .9
        return dAE(sgn * a, lerp(fb(a) - g, min(fb(a) + g, vis_bot(a) + .5), v))
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        strip('Helmet: vfloor ' + nm, lambda u, v, s=sgn: vf_dir(u, v, s), 48, 4, .0026, .0006, 'METAL')

    def fp_lo(a): return fb(a) + gvh(a)
    a_f = next(a for a in [i * .05 for i in range(1, 900)] if fp_lo(a) > vis_bot(a) - .9)
    if FACE == 'white':
        def fp_dir(u, v):
            a = lerp(-a_f, a_f, u)
            lo = fp_lo(abs(a)); hi = vis_bot(abs(a)) + .6
            return dAE(a, lerp(lo, max(hi, lo + .05), v))
        plate('Helmet: faceplate', fp_dir, 72, 18, .0030, .0026, 'SHELL', bev=.0009, seg=2)
    else:
        def bar_raw(a): return vis_bot(a) - BAR_H * (1 - .1 * (a / a_f) ** 2)

        def bar_lo(a): return max(bar_raw(a), fp_lo(a))
        a_c = next(a for a in [i * .05 for i in range(1, 900)] if fp_lo(a) >= bar_raw(a))

        def bar_dir(u, v):
            a = lerp(-a_f, a_f, u)
            lo = bar_lo(abs(a)); hi = vis_bot(abs(a)) + .6
            return dAE(a, lerp(lo, max(hi, lo + .05), v))
        plate('Helmet: bar', bar_dir, 64, 8, .0030, .0026, 'SHELL', bev=.0007, seg=2)

        def mz_dir(u, v):
            a = lerp(-a_c - .6, a_c + .6, u)
            lo = fp_lo(abs(a)) - .9; hi = bar_lo(min(abs(a), a_c)) + .9
            return dAE(a, lerp(lo, max(hi, lo + .05), v))
        plate('Helmet: muzzle', mz_dir, 44, 16, .0024, .0020, 'METAL', bev=0)

    # cheek shells (SHELL): from the V at the front, under the lenses, round the jaw to the back seam under the nape point
    A1 = 180.0 - BG

    def cw_hi(a):
        if a < A_V: return fb(a) - gvh(a)
        if a <= TIP_A: return vis_bot(a)
        return side_top(a)

    def cw_a(u): return A1 * (.30 * u + .70 * u * u)

    def cw_dir(u, v, sgn=1):
        a = cw_a(u)
        lo = BOTW(a); hi = max(cw_hi(a), lo + .7)
        return dAE(sgn * a, lerp(lo, hi, v))
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        plate('Helmet: cheek ' + nm, lambda u, v, s=sgn: cw_dir(u, v, s), 92, 22, L_CW, T_CW, 'SHELL', bev=.0009, seg=2)

    # back seam: a dark groove with the cyan spine line, from the nape point down to the rim (the suit's spine seam)
    e_tip = ae_of(cp_dir(1.0, .5))[1]

    def bs_dir(u, v, w):
        return dAE(lerp(180 - w, 180 + w, v), lerp(BOT(180) - .8, e_tip, u))
    strip('Helmet: back floor', lambda u, v: bs_dir(u, v, BG + .4), 24, 3, L_CW + T_CW - .0022, .0006, 'METAL')
    line('Helmet: back line', [(180.0, e) for e in [lerp(BOT(180) + .2, e_tip - 1.0, i / 20) for i in range(21)]], -.42, .42,
         L_CW + T_CW - .0016, .0005)
    # the nape node: a small diamond where the spine, the cranial seam's V and the back seam meet
    diamond('Helmet: nape', (180.0, e_tip + 1.2), 3.0, 1.7, L_CP + T_CP - .0002, rise=.0010)

    # nasal guard + keel (SHELL): from the brow peak down between the lenses and on down the face as a keel
    eb, et = E_APEX + 2 * GP + 3.0, cp_front_e(0) + 4.0
    e0 = cp_front_e(0)
    rw = curve([(eb, 1.3), (eb + 4, 1.6), (eb + 10, 1.7), (vis_bot(0) - 4, 2.2), (vis_bot(0) + 6, 2.0), (e0 - 7, 2.3), (e0 - 1.5, 3.6), (e0 + 1, 5.0), (et, 5.6)])

    def rb_dir(u, v):
        e = lerp(eb, et, u); w = rw(e)
        return dAE(lerp(-w, w, v), e)

    def rb_layer(u, v):
        return L_CP - .0004 + .0007 * math.sin(math.pi * v) - .0014 * (1 - smooth01(u / .14))
    plate('Helmet: nasal', rb_dir, 44, 7, rb_layer, T_CP, 'SHELL', bev=.0006, seg=1)

    # brow blades (SHELL, outermost): swept layers from the brow peak over each lens, tapering behind the ears
    wb = curve([(6, 2.4), (16, 5.0), (40, 6.0), (70, 6.0), (92, 5.0), (106, 3.6), (117, 2.4), (124, 1.6)])

    def bb_lo(a):
        t = smooth01((a - (TIP_A - 12)) / 16)
        return lerp(cp_front_e(min(a, TIP_A)) + 1.3, cpe(max(a, TIP_A)) + 3.0, t)
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        def bb_dir(u, v, sgn=sgn):
            a = lerp(6, 124, u); lo = bb_lo(a)
            return dAE(sgn * a, lerp(lo, lo + wb(a), v))
        plate('Helmet: brow ' + nm, bb_dir, 64, 8, lambda u, v: L_CP + T_CP - .0004 + .0006 * math.sin(math.pi * v) - .0011 * math.exp(-((lerp(6, 124, u) - 97) / 14) ** 2), .0030, 'SHELL', bev=.0007, seg=2)

    # brow gem: a faceted cyan diamond in a dark bezel on the brow peak (echoes the suit's chest gem)
    diamond('Helmet: brow', (0.0, cp_front_e(0) + 4.8), 4.0, 2.4, L_CP + T_CP + .0001)

    # eye slits (LIGHT): a thin angled blade in each lens
    slit_e = curve([(8, -4.2), (22, -2.3), (36, -.4)])

    def sl_dir(u, v, sgn):
        a = lerp(8, 36, u); hw = .5 * math.sin(math.pi * u) ** .5 + .02
        return dAE(sgn * a, slit_e(a) + lerp(-hw, hw, v))
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        strip('Helmet: slit ' + nm, lambda u, v, s=sgn: sl_dir(u, v, s), 26, 3, .0013, .0004, 'LIGHT')

    # ear pivots: the family's teardrop pod on each cheek shell over the ear, its tail swept back along the cranial seam
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        d = dAE(sgn * 96.0, -6.0)
        O = P(d, L_CW + T_CW - .0012)
        pod2('Helmet: pivot', nm, O, d, .0125, .0195, .0016, .0021, tilt=16, light=True, base_off=.0, nphi=32)

    # LIGHT lines: the visor's outline, the side seams under the cranial edge, the cheek shells' V edges
    top = [(a, cp_front_e(a)) for a in [lerp(-TIP_A, TIP_A, i / 64) for i in range(65)]]
    line('Helmet: line visor top', top, -1.0, .55, .0010, .0006)
    bot = [(a, vis_bot(abs(a))) for a in [lerp(TIP_A, -TIP_A, i / 64) for i in range(65)]]
    line('Helmet: line visor bottom', bot, -.9, .55, .0010, .0006)
    ring = [(-p[0], p[1]) for p in cp_edge_pts] + [(-360 + p[0], p[1]) for p in cp_edge_pts[::-1][1:]]
    ring = [p for p in ring if TIP_A + .3 < -p[0] < 360 - TIP_A - .3]
    line('Helmet: line cranial', ring, -.42, .42, .0002, .0007,
         shift=lambda i: GAP * .5 * smooth01((min(abs(ring[i][0]), 360 - abs(ring[i][0])) - TIP_A) / 18))
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        pts = [(sgn * a, fb(a)) for a in [lerp(0.0, A_V + 1.5, i / 56) for i in range(57)]]
        if sgn < 0: pts = pts[::-1]
        line('Helmet: line cheek ' + nm, pts, -.45, .45, .0033, .0005)

    # neck seal (DARK): a shallow funnel from inside the rim in to a collar 5.5 mm round the neck, a few mm under the rim
    # (only a thin dark lip can show below the shell)
    NC = Vector((0, -.010, 0))
    NR, NK = 64, 13
    rims, rads = [], []
    for i in range(NR):
        a = -180 + 360 * i / NR
        pr = P(dAE(a, BOT(abs(a)) - .5), -.0012)
        h = Vector((pr.x - NC.x, pr.y - NC.y, 0)); hl = h.length; h /= hl
        # at the throat the collar climbs to just under the chin (else it hangs below the jaw line in side view)
        wf = math.exp(-(a / 38.0) ** 2)
        zc = pr.z - .003
        for _ in range(2):
            r = .034
            for _ in range(40):
                q = Vector((NC.x + h.x * r, NC.y + h.y * r, zc))
                loc, nrm, idx, dist = BVH.find_nearest(q)
                if dist is None or dist >= .0056: break
                r += .0008
            loc, nrm, idx, dist = BVH.ray_cast(Vector((NC.x + h.x * r, NC.y + h.y * r, -.3)), Vector((0, 0, 1)))
            ceil = loc.z - .0045 if (loc is not None and loc.z < .05) else pr.z + .012
            zc = lerp(pr.z - .003, max(pr.z - .003, min(ceil, pr.z + .016)), wf)
        rims.append((pr, h, hl, zc)); rads.append(min(r, hl - .004))
    r0 = list(rads)
    for _ in range(4):
        rads = [max(r0[i], (rads[(i - 1) % NR] + 2 * rads[i] + rads[(i + 1) % NR]) / 4) for i in range(NR)]
    rows = []
    for i in range(NR):
        pr, h, hl, zc = rims[i]
        pc = Vector((NC.x + h.x * rads[i], NC.y + h.y * rads[i], zc))
        row = []
        rise = .014 * math.exp(-(i * 360 / NR - 180) ** 2 / 45 ** 2)     # under the chin it rises to hug the jaw
        for k in range(NK):
            t = k / (NK - 1)
            q = pr.lerp(pc, t); q.z += -.002 * math.sin(math.pi * t) + rise * smooth01(t / .1) * (1 - smooth01((t - .72) / .28))
            loc, nrm, idx, dist = BVH.ray_cast(Vector((q.x, q.y, -.3)), Vector((0, 0, 1)))
            if loc is not None and loc.z < .05: q.z = min(q.z, loc.z - .0040)
            row.append(q)
        rows.append(row)
    for it in range(12):
        zs = [[r.z for r in row] for row in rows]
        for i in range(NR):
            for k in range(1, NK - 1):
                q = rows[i][k]
                sm = (zs[(i - 1) % NR][k] + zs[(i + 1) % NR][k] + zs[i][k - 1] + zs[i][k + 1]) / 4
                loc, nrm, idx, dist = BVH.ray_cast(Vector((q.x, q.y, -.3)), Vector((0, 0, 1)))
                ceil = loc.z - .0040 if (loc is not None and loc.z < .05) else 9.0
                q.z = min(ceil, .5 * q.z + .5 * sm)
    for row in rows:
        for q in row:
            for _ in range(20):
                loc, nrm, idx, dist = BVH.find_nearest(q)
                if dist is None or dist >= .0034: break
                q.z -= .001
    finish(grid_obj('Helmet: neck seal', rows, 'DARK', closed_u=True), .0010, 1.0, 0, 1, rim_only=True)


build_helmet()
# ================================================================ THE REST OF THE FAMILY
# Same language as the helmet: white sculpted plates over a dark under-layer, the white keel/nasal on the centre line,
# cyan V seams converging on the chin, almond lenses with a brow peak and gem, teardrop forms swept back.

# --- a face-following envelope for the fitted mask (the raw head, lightly dilated and blurred: nose, lips and chin read)
HF = [last_hit(C, d, BVH) for d in DIRS]
HFD = []
for i, d in enumerate(DIRS):
    vals = [HF[j] for (_, j, _) in KD.find_range(d, chord(6.0)) if HF[j] is not None]
    HFD.append(max(vals) if vals else None)
_cf = {}


def env_face(d):
    key = (round(d.x, 5), round(d.y, 5), round(d.z, 5))
    r = _cf.get(key)
    if r is not None: return r
    sw = sv = 0.0
    for (co, j, dist) in KD.find_range(d, chord(2.4 * 3.6)):
        if HFD[j] is None: continue
        ang = 2 * math.asin(min(1, dist / 2)) / D2R
        w = math.exp(-(ang / 3.6) ** 2); sw += w; sv += w * HFD[j]
    r = sv / sw if sw > 0 else env(d)
    _cf[key] = r
    return r


def PF(d, layer):
    angn = math.acos(max(-1, min(1, d.dot(NOSE_D)))) / D2R
    return C + d * (env_face(d) + .0043 + .0012 * math.exp(-(angn / 10.0) ** 2) + layer)


# --- a softer face envelope for the rigid half mask and the visor's arms (hugs the cheeks and temples, smooth)
HFS = []
for i, d in enumerate(DIRS):
    vals = [HF[j] for (_, j, _) in KD.find_range(d, chord(8.0)) if HF[j] is not None]
    HFS.append(max(vals) if vals else None)
HFS = blur(HFS)
_cs = {}


def env_soft(d):
    key = (round(d.x, 5), round(d.y, 5), round(d.z, 5))
    r = _cs.get(key)
    if r is not None: return r
    sw = sv = 0.0
    for (co, j, dist) in KD.find_range(d, chord(2.4 * 4.0)):
        if HFS[j] is None: continue
        ang = 2 * math.asin(min(1, dist / 2)) / D2R
        w = math.exp(-(ang / 4.0) ** 2); sw += w; sv += w * HFS[j]
    r = sv / sw if sw > 0 else env(d)
    _cs[key] = r
    return r


def PS(d, layer):
    a, b = env_soft(d), env_face(d)
    r = (a + b + math.sqrt((a - b) ** 2 + .0025 ** 2)) / 2      # smooth max: never under the fitted envelope (nose tip)
    return C + d * (r + .0046 + layer)


def PV(d, layer):
    """the visor: the hull envelope in front of the eyes, the soft face envelope back along the temples"""
    a, e = ae_of(d)
    w = smooth01((abs(a) - 62.0) / 26.0)
    return P(d, layer).lerp(PS(d, layer), w)


# ================================================================ HALF MASK (nose, mouth, chin; eyes free)
def build_half_mask():
    """the helmet's lower face on its own: white cheek wings and a white bar over the nose bridge frame a dark muzzle with a
    sculpted keel; the wings' cyan V edges curve up into a glowing top edge; the family's teardrop anchors at the ears"""
    hm_top = curve([(0, -9.0), (9, -10.5), (20, -14.0), (34, -15.0), (50, -13.5), (66, -10.5), (80, -9.0), (92, -10.5)])
    hm_bot = curve([(0, -57.6), (6, -56.6), (16, -54.4), (36, -50.0), (56, -42.0), (72, -32.0), (84, -20.0), (92, -9.5)])
    AP = hm_bot(0) + 5.0
    HA_V = 31.0
    fv = curve([(0, AP), (6, AP + 11.0), (12, AP + 22.0), (17, AP + 30.0), (22, hm_top(22) - 4.6), (26, hm_top(26) - 1.9),
                (HA_V, hm_top(HA_V) - .2)])
    GPH = .75

    def gv(a):
        a = min(max(a, .05), HA_V)
        k = deriv(fv, a) / max(.3, math.cos(fv(a) * D2R))
        return GPH * math.sqrt(1 + k * k) * (1 - smooth01((a - (HA_V - 5)) / 5))

    # V groove floors (METAL)
    def vf(u, v, sgn):
        a = lerp(0, HA_V, u); g = gv(a) + .8
        return dAE(sgn * a, lerp(fv(a) - g, min(fv(a) + g, hm_top(a) - .2), v))
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        plate('Half mask: vfloor ' + nm, lambda u, v, s=sgn: vf(u, v, s), 34, 3, -.0012, .0005, 'METAL', bev=0, pos=PS)

    def lo_f(a): return fv(a) + gv(a)
    a_f = next(a for a in [i * .05 for i in range(1, 900)] if lo_f(a) > hm_top(a) - .3)

    def nose_lift(d, k=.0020, w=11.0):
        return k * math.exp(-(math.acos(max(-1, min(1, d.dot(NOSE_D)))) / D2R / w) ** 2)

    # bar (SHELL): a white strip over the nose bridge joining the wings, its ends running out along the V's top
    def bar_raw(a): return hm_top(a) - 3.2

    def bar_lo(a): return max(bar_raw(a), lo_f(a))
    a_c = next(a for a in [i * .05 for i in range(1, 900)] if lo_f(a) >= bar_raw(a))

    def br(u, v):
        a = lerp(-a_f, a_f, u)
        lo = bar_lo(abs(a)); hi = hm_top(abs(a))
        return dAE(a, lerp(lo, max(hi, lo + .05), v))
    plate('Half mask: bar', br, 56, 7, lambda u, v: -.0004 + nose_lift(br(u, v)), .0026, 'SHELL', bev=.0005, seg=2, pos=PS)

    # muzzle (METAL): the dark shield between the V edges under the bar
    def mz(u, v):
        a = lerp(-a_c - .6, a_c + .6, u)
        lo = lo_f(abs(a)) - .9; hi = bar_lo(min(abs(a), a_c)) + .9
        return dAE(a, lerp(lo, max(hi, lo + .05), v))
    plate('Half mask: muzzle', mz, 30, 12, lambda u, v: -.0016 + nose_lift(mz(u, v)), .0016, 'METAL', bev=0, pos=PS)

    # wings (SHELL): from the V round the jaw, tapering to a point in front of the ear; they meet under the muzzle in a white chin
    def wing(u, v, sgn):
        a = lerp(0.0, 93.0, u)
        lo = hm_bot(a) - .4 + 1.2 * (1 - smooth01(a / 14))
        hi = fv(a) - gv(a) if a < HA_V else hm_top(a)
        hi = max(hi, lo + .8)
        return dAE(sgn * a, lerp(lo, hi, v))
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        plate("Half mask: wing " + nm, lambda u, v, s=sgn: wing(u, v, s), 56, 12, -.0006, .0026, 'SHELL', bev=.0008, pos=PS)
        pts = [(sgn * a, fv(a)) for a in [lerp(0.0, HA_V, i / 36) for i in range(37)]]
        if sgn < 0: pts = pts[::-1]
        line('Half mask: line ' + nm, pts, -.42, .42, -.0006, .0005, pos=PS)

    # the glowing top edge: a thin light peeking out from under the bar's and wings' upper edge, ear to ear
    pts = [(a, hm_top(abs(a))) for a in [lerp(86, -86, i / 72) for i in range(73)]]
    line('Half mask: brim', pts, -.25, .5, -.0011, .0007, pos=lambda d, l: PS(d, l + .0012 * math.exp(-(ae_of(d)[0] / 14) ** 2)))

    # keel (METAL): a sculpted ridge down the muzzle, from the chin point up under the bar
    e_b = AP + 2 * GPH + 2.0
    e_t = bar_raw(0) + .8
    kw = curve([(e_b, .9), (e_b + 4, 1.3), (-25, 1.4), (e_t, 1.6)])

    def kl(u, v):
        e = lerp(e_b, e_t, u); w = kw(e)
        return dAE(lerp(-w, w, v), e)
    plate('Half mask: keel', kl, 24, 6, lambda u, v: -.0002 + .0006 * math.sin(math.pi * v) + nose_lift(kl(u, v)), .0012, 'METAL', bev=.0004, seg=1, pos=PS)

    # ear anchors: the family's teardrop pods where the wings end
    for sgn in (1, -1):
        pod2('Half mask: anchor', 'R' if sgn > 0 else 'L', Vector((sgn * .1095, -.010, .058)), Vector((sgn, 0, 0)),
             .0098, .0135, .0022, .0030, tilt=14, light=True, nphi=32)


# ================================================================ VISOR (eyes only)
def build_visor():
    # lenses (DARK glass), the helmet's almond pair
    def gl(u, v):
        a = lerp(-TIP_A - 1, TIP_A + 1, u)
        return dAE(a, lerp(vis_bot(abs(a)) - 1.5, cp_front_e(a) + 1.5, v))
    plate('Visor: glass', gl, 49, 10, .0004, .0008, 'DARK', bev=0, rim_only=False, pos=PV)

    # brow band (SHELL) with the peak, sweeping back past the lens tips into tapering temple arms
    AE = 89.0
    bandw = curve([(0, 6.2), (14, 5.4), (40, 4.8), (66, 4.6), (TIP_A, 3.6), (85, 2.6), (AE, 1.9)])

    def band_lo(a):
        a = abs(a)
        if a <= TIP_A: return cp_front_e(a)
        return TIP_E + (a - TIP_A) * .12

    def bd(u, v):
        a = lerp(-AE, AE, u)
        return dAE(a, lerp(band_lo(a), band_lo(a) + bandw(abs(a)), v))
    plate('Visor: brow', bd, 110, 9, lambda u, v: .0022 + .0008 * math.sin(math.pi * v) * (1 - smooth01((abs(2 * u - 1) - .9) / .1)), .0026, 'SHELL', bev=.0006, pos=PV)

    # lower rim (SHELL): a slim sweep under the lenses, merging into the arms at the tips
    rimw = curve([(0, 2.2), (30, 2.0), (60, 1.8), (TIP_A, 2.2), (TIP_A + 7, 1.0)])

    def rm(u, v):
        a = lerp(-TIP_A - 7, TIP_A + 7, u)
        lo = vis_bot(min(abs(a), TIP_A)) - rimw(abs(a)) if abs(a) <= TIP_A else TIP_E - 2.2 + (abs(a) - TIP_A) * .45
        return dAE(a, lerp(lo, lo + rimw(abs(a)) + .9, v))
    plate('Visor: rim', rm, 80, 5, .0012, .0022, 'SHELL', bev=.0007, pos=PV)

    # nasal bridge (SHELL)
    e0 = cp_front_e(0)
    nw = curve([(vis_bot(0) - 2.0, 2.0), (vis_bot(0) + 6, 1.7), (e0 - 5, 2.2), (e0 + 1, 4.2)])

    def nb(u, v):
        e = lerp(vis_bot(0) - 2.0, e0 + 1, u); w = nw(e)
        return dAE(lerp(-w, w, v), e)
    plate('Visor: nasal', nb, 18, 6, lambda u, v: .0016 + .0006 * math.sin(math.pi * v), .0028, 'SHELL', bev=.0008, pos=PV)

    # lens outlines (LIGHT) and the brow gem
    top = [(a, cp_front_e(a)) for a in [lerp(-TIP_A, TIP_A, i / 64) for i in range(65)]]
    line('Visor: line top', top, -.9, .6, .0010, .0006, pos=PV)
    bot = [(a, vis_bot(abs(a))) for a in [lerp(TIP_A, -TIP_A, i / 64) for i in range(65)]]
    line('Visor: line bottom', bot, -.9, .6, .0010, .0006, pos=PV)
    diamond('Visor: brow', (0.0, cp_front_e(0) + 4.4), 3.6, 2.2, .0022 + .0008 + .0026 - .0002, pos=PV)
    # eye slits (LIGHT) in the lenses, as on the helmet
    slit_e = curve([(8, -4.2), (22, -2.3), (36, -.4)])
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        def sl(u, v, sgn=sgn):
            a = lerp(8, 36, u); hw = .5 * math.sin(math.pi * u) ** .5 + .02
            return dAE(sgn * a, slit_e(a) + lerp(-hw, hw, v))
        plate('Visor: slit ' + nm, sl, 26, 3, .0013, .0004, 'LIGHT', bev=0, pos=PV)


# ================================================================ MASK (a thin fitted cover: nose, mouth, chin)
def build_mask():
    mk_top = curve([(0, -8.5), (9, -10.0), (20, -13.5), (36, -14.5), (54, -12.5), (70, -10.0), (78, -10.0), (83, -11.0)])
    mk_bot = curve([(0, -58.6), (8, -57.6), (20, -55.6), (40, -50.0), (60, -40.0), (72, -29.0), (78, -22.5), (83, -17.0)])

    def cv(u, v):
        a = lerp(-83, 83, u)
        return dAE(a, lerp(mk_bot(abs(a)), mk_top(abs(a)), v))
    plate('Mask: cover', cv, 90, 22, 0.0, .0013, 'DARK', bev=0, pos=PF)
    # the keel: a raised METAL ridge down the centre line
    kw = curve([(-58, 1.2), (-40, 1.6), (-18, 1.6), (-9.5, 2.2)])

    def kl(u, v):
        e = lerp(-57.5, -10.0, u); w = kw(e)
        return dAE(lerp(-w, w, v), e)
    plate('Mask: keel', kl, 34, 7, lambda u, v: .0007 + .0007 * math.sin(math.pi * v), .0012, 'METAL', bev=.0005, pos=PF)
    # cyan V seams converging on the chin, and a thin brim along the top edge
    V0, VJ = 1.0, 38.0
    mv = curve([(V0, mk_bot(0) + 5.5), (7, mk_bot(0) + 16.0), (13, -38.0), (19, -28.5), (25, mk_top(25) - 5.6), (30, mk_top(30) - 2.6),
                (34, mk_top(34) - 1.3), (VJ, mk_top(VJ) - .9)])
    for sgn, nm in ((1, 'R'), (-1, 'L')):
        pts = [(sgn * a, mv(a)) for a in [lerp(V0, VJ, i / 40) for i in range(41)]]
        if sgn < 0: pts = pts[::-1]
        line('Mask: seam ' + nm, pts, -.4, .4, .0011, .0005, pos=PF)
    top = [(a, mk_top(abs(a)) - .9) for a in [lerp(-81, 81, i / 90) for i in range(91)]]
    line('Mask: brim', top, -.36, .36, .0011, .0005, pos=PF)


# ================================================================ EAR PODS
def build_ears():
    for sgn in (1, -1):
        pod2('Ears: pod', 'R' if sgn > 0 else 'L', Vector((sgn * .1095, -.0135, .0605)), Vector((sgn, 0, 0)), .0142, .0178, .0032, .0046,
             tilt=18, light=True, ring=(.72, .83, .94), slit_w=.0009, base_off=.0012)


build_half_mask()
build_visor()
build_mask()
build_ears()


# ---------------------------------------------------------------- one object per gear (parts budget): bake the modifiers, merge
PAINTS = ['SHELL', 'METAL', 'DARK', 'LIGHT', 'SOFT']


def join_gear(prefix, name):
    dg = bpy.context.evaluated_depsgraph_get()
    obs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(prefix)]
    bm = bmesh.new()
    for o in obs:
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        me.calc_loop_triangles(); cn = me.corner_normals; nb = 0; at = None
        for lt in me.loop_triangles:
            vs = [me.vertices[me.loops[li].vertex_index].co for li in lt.loops]
            gn = (vs[1] - vs[0]).cross(vs[2] - vs[0])
            sn = sum((Vector(cn[li].vector) for li in lt.loops), Vector())
            if gn.dot(sn) < 0: nb += 1; at = tuple(round(c, 3) for c in (o.matrix_world @ ((vs[0] + vs[1] + vs[2]) / 3)))
        print("PART %-30s %6d  wrong %d %s" % (o.name, len(me.vertices), nb, at if nb else ''))
        slots = [(m.name.split('.')[0] if m else 'SHELL') for m in me.materials] or ['SHELL']
        b1 = bmesh.new(); b1.from_mesh(me)
        for f in b1.faces:
            f.material_index = PAINTS.index(slots[f.material_index] if f.material_index < len(slots) else 'SHELL')
        b1.transform(o.matrix_world)
        tmp = bpy.data.meshes.new('tmp'); b1.to_mesh(tmp); b1.free(); ev.to_mesh_clear()
        bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    for o in obs: bpy.data.objects.remove(o)
    # drop degenerate, folded slivers (< 0.3 mm2, facing against their neighbours) the bevels leave at acute plate tips
    bm.normal_update()

    def flipped(f):
        nb = [g for e in f.edges for g in e.link_faces if g is not f]
        if not nb: return False
        avg = sum((g.normal * g.calc_area() for g in nb), Vector())
        return avg.length > 0 and f.normal.dot(avg) < 0
    tiny = [f for f in bm.faces if f.calc_area() < 3e-7 and flipped(f)]
    if tiny:
        bmesh.ops.delete(bm, geom=tiny, context='FACES_ONLY')
        bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context='EDGES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    print('JOIN %s: dropped %d sliver faces' % (name, len(tiny)))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for n in PAINTS: me.materials.append(blhelp.mat(n))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


for _p, _n in (('Helmet: ', 'Helmet: shell'), ('Half mask: ', 'Half mask: shell'), ('Visor: ', 'Visor: shell'), ('Mask: ', 'Mask: shell'), ('Ears: ', 'Ears: pods')):
    join_gear(_p, _n)


def clearance_report():
    dg = bpy.context.evaluated_depsgraph_get()
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or not ob.name.startswith(blhelp.GEARS): continue
        ev = ob.evaluated_get(dg); me = ev.to_mesh(); mn = 9; bad = 0; at = None
        for v in me.vertices:
            p = ob.matrix_world @ v.co
            if abs(p.x) > .085 and abs(p.z - .061) < .035 and abs(p.y + .014) < .035: continue
            loc, n, i, dist = BVH.find_nearest(p)
            if dist is not None:
                bad += dist < .0025
                if dist < mn: mn = dist; at = p
        by = max((ob.matrix_world @ v.co for v in me.vertices), key=lambda p: p.y)
        print("CLEAR %-34s verts %6d  min %.2f mm  under2.5 %d  at %s  back %s" % (ob.name, len(me.vertices), mn * 1000, bad, tuple(round(c, 3) for c in at) if at else None, tuple(round(c, 4) for c in by)))
        ev.to_mesh_clear()


clearance_report()
