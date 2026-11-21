"""The Hound's head (owner 2026-11-03, with a reference: a pale, emaciated thing on all fours with a wrinkled face, a bald crown and
long grey strands, pointed ears, small red-rimmed eyes, a grin of small sharp teeth, a white ruff at the throat; then "Change head
to be more doglike"; docs/ZOMBIE-MODELS.md 4j). Not the pack's head: its own skull and jaw on the head bone (Runtime ZombieGear;
the jaw hinged open by the runtime, ZombieBeastJaw), modelled here as metaballs (organic blends; negative ones carve the eye
sockets, the nostrils and the creases across the muzzle) polygonised, decimated and coloured by the kit's skin finish.

The design frame is the kit's (X right, Y forward, Z up, metres, the head bone at the origin), sized round the pack's head there
(rest.Head.Head_AnimeYoung: X +-.108, Y -.094..141, Z -.057..213): a long low cranium over the neck's top, a heavy brow, sunken
eyes, a muzzle reaching .24 ahead with a dark nose, the lips drawn back off small sharp teeth on dark gums, tall pointed ears
cupped forward, thin grey strands off the crown hanging back, a white ruff round the throat (the skull's) and a beard of it under
the chin (the jaw's). Built by station_gear.py's main with the rest of the bank.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

import gearkit as gk
from gearkit import Piece, catmull, cyl, finish, ribbon, sphere, tube

HEAD_FRAME = None  # set by props() (backrooms_gear.HEAD)
JAW_HINGE = Vector((0, .005, .018))     # design frame: the jaw turns about X through here (Runtime ZombieBeastJaw)


def finishes():
    finish('hound-skin', 0xE0C8BE, 'skin')
    finish('hound-lip', 0xB88A86, 'skin')
    finish('hound-nose', 0x4A3434, 'rubber')
    finish('hound-gum', 0x7A2A2C, 'flesh')
    finish('hound-tooth', 0xE8DCC0, 'plastic')
    finish('hound-eye', 0x1E0606, 'plastic', outline=False)
    finish('hound-eye-glint', 0xC8281C, 'glow', outline=False)
    finish('hound-ear-in', 0xC89090, 'skin')
    finish('hound-hair', 0xAEAAA2, 'cloth', outline=False, dirt=.4)
    finish('hound-fur', 0xEAE6DC, 'cloth', outline=False, dirt=.5)


# ---------------------------------------------------------------------------------------------------------------------------
# Metaballs: each element by the half-axes it shows (a lone element at stiffness 2 and threshold .6 shows about .67 of its radius)
# ---------------------------------------------------------------------------------------------------------------------------
SHOW = .672


def _meta(name, elems, res=.0034, decimate=.11):
    mb = bpy.data.metaballs.new(name)
    mb.resolution = res
    mb.render_resolution = res
    mb.threshold = .6
    for co, half, neg in elems:
        el = mb.elements.new(type='ELLIPSOID')
        m = max(half)
        el.co = co
        el.radius = m / SHOW
        el.size_x, el.size_y, el.size_z = half[0] / m, half[1] / m, half[2] / m
        el.stiffness = 2.0
        el.use_negative = neg
    ob = bpy.data.objects.new(name, mb)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.metaballs.remove(mb)
    pc = Piece(name)
    pc.bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(pc.bm, verts=pc.bm.verts, dist=1e-5)
    pc.mods.append(('DECIMATE', dict(ratio=decimate)))
    pc.mods.append(('SMOOTH', dict(factor=.6, iterations=8)))
    pc.sharp = 179
    return pc


def E(x, y, z, hx, hy, hz, neg=False):
    return ((x, y, z), (hx, hy, hz), neg)


def mirrored(elems):
    out = []
    for co, half, neg in elems:
        out.append((co, half, neg))
        if abs(co[0]) > 1e-6:
            out.append(((-co[0], co[1], co[2]), half, neg))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The skull: cranium, brow, cheekbones, muzzle, nose; sockets, nostrils and creases carved
# ---------------------------------------------------------------------------------------------------------------------------
def _skull_elems():
    return mirrored([
        E(0, -.02, .09, .072, .1, .07),          # cranium, long and low
        E(0, -.045, .03, .058, .07, .06),        # the occiput down over the neck's top
        E(0, -.025, -.03, .05, .05, .045),       # the neck cap (the pack's neck ends here)
        E(.03, .068, .097, .032, .03, .013),     # brow ridge
        E(.044, .052, .056, .03, .036, .021),    # cheekbone
        E(0, .09, .066, .05, .056, .043),        # muzzle root
        E(0, .145, .057, .04, .05, .033),        # muzzle
        E(0, .19, .051, .031, .035, .027),       # muzzle tip
        E(0, .222, .053, .022, .017, .02),       # nose
        E(.03, .12, .035, .022, .05, .016),      # upper lips' jowls
        E(0, .1, .095, .02, .06, .012),          # the bridge's ridge
        # carved: the eye sockets, the nostrils, creases across the bridge, the mouth's corners drawn back
        E(.034, .083, .084, .025, .022, .023, True),
        E(.008, .236, .049, .005, .005, .005, True),
        E(0, .112, .101, .028, .0026, .007, True),
        E(0, .128, .095, .026, .0026, .007, True),
        E(0, .144, .088, .024, .0026, .007, True),
        E(.03, .07, .018, .012, .03, .012, True),
    ])


def skull():
    out = []
    pc = _meta('hound-skull', _skull_elems())
    for f in pc.bm.faces:
        c = f.calc_center_median()
        fin = 'hound-skin'
        if c.y > .212 and c.z > .034:
            fin = 'hound-nose'
        elif c.z < .036 and c.y > .07 and abs(c.x) < .04:
            fin = 'hound-lip'
        f.material_index = pc.slot(fin)
    out.append(pc.make(recalc=True))
    out += _eyes()
    out += _teeth(upper=True)
    out += _ears()
    out += _strands()
    out += _ruff(throat=True)
    return out


def _eyes():
    pc = Piece('hound-eyes')
    for sx in (-1, 1):
        sphere(pc, (sx * .033, .087, .083), (.0088, .0088, .0088), 'hound-eye', u=14, v=10)
        sphere(pc, (sx * .0335, .0948, .0835), (.0028, .0016, .0028), 'hound-eye-glint', u=8, v=6)
        gk.torus(pc, (sx * .033, .0905, .083), Vector((sx * .3, 1, .05)).normalized(), .0098, .0024, 'hound-lip', segs=18, rsegs=6)
    return [pc.make()]


def _teeth(upper):
    """A row of small sharp teeth along the lip line, longer at the canines; the gum band behind them."""
    pc = Piece('hound-teeth-' + ('upper' if upper else 'lower'))
    n = 13
    z0 = .027 if upper else .0
    for side in (-1, 1):
        for k in range(n):
            t = k / (n - 1)
            y = .05 + .15 * t
            x = side * (.044 * (1 - t) ** .75 + .007)
            canine = math.exp(-((t - .74) / .07) ** 2)
            ln = (.013 + .017 * canine) * (1 if upper else .75)
            r = .0036 + .0022 * canine
            base = Vector((x, y, z0))
            tip = base + Vector((side * -.001, .002, -ln if upper else ln))
            cyl(pc, base, tip, r, 'hound-tooth', segs=7, r1=.0004)
        # the gums: a ribbon along the row
        pts = [Vector((side * (.044 * (1 - k / 10) ** .75 + .007), .05 + .15 * k / 10, z0 + (.003 if upper else -.003))) for k in range(11)]
        ribbon(pc, pts, [Vector((0, 0, 1))] * len(pts), .009, .005, 'hound-gum')
    return [pc.make()]


def _ears():
    """Tall pointed ears off the cranium's sides, broad at the root and cupped forward, a little out and back, pink inside."""
    out = []
    for shell in ('outer', 'inner'):
        pc = Piece('hound-ears-' + shell)
        nu, nv = 16, 10
        for side in (-1, 1):
            base = Vector((side * .058, -.03, .138))
            tip = Vector((side * .094, -.088, .252))
            axis = tip - base
            up = axis.normalized()
            out_dir = Vector((side, .35, .1)).normalized()
            across = up.cross(out_dir).normalized() * side
            grid = {}
            for i in range(nu + 1):
                u = i / nu
                w = (.052 * (1 - u) ** .8 + .0015) * (.8 if shell == 'inner' else 1)
                for j in range(nv + 1):
                    v = -1 + 2 * j / nv
                    cup = .02 * (1 - v * v) * (1 - u) ** .5
                    p = base + axis * (u * (.93 if shell == 'inner' else 1)) + across * (v * w) - out_dir * cup + Vector((0, -.012 * u * u, 0))
                    if shell == 'inner':
                        p = p + out_dir * .0035
                    grid[i, j] = pc.bm.verts.new(p)
            for i in range(nu):
                for j in range(nv):
                    f = pc.bm.faces.new((grid[i, j], grid[i, j + 1], grid[i + 1, j + 1], grid[i + 1, j]))
                    f.material_index = pc.slot('hound-skin' if shell == 'outer' else 'hound-ear-in')
        if shell == 'outer':
            pc.solidify(.003, 0)
        out.append(pc.make())
    return out


def _strands():
    """Thin grey strands off the bald crown, hanging down beside the face and past the ears (the head carried level on its long
    neck: hanging back, they would only run into it)."""
    pc = Piece('hound-strands')
    rnd = random.Random(7)
    for k in range(24):
        side = -1 if k % 2 else 1
        u = rnd.uniform(0, 1)
        root = Vector((side * (.025 + .04 * u), .03 - .085 * rnd.uniform(0, 1), .15 - .03 * u))
        ln = rnd.uniform(.16, .28)
        out = side * rnd.uniform(.035, .055)
        pts = [root,
               root + Vector((out * .6, .005, -.015)),
               root + Vector((out, .0, -.06)),
               root + Vector((out * 1.1, -.01, -ln * .6)),
               root + Vector((out * 1.05 + rnd.uniform(-.01, .01), -.02, -ln))]
        pts = catmull(pts, per=3)
        radii = [.0022 * (1 - .8 * (i / (len(pts) - 1))) + .0004 for i in range(len(pts))]
        tube(pc, pts, .002, 'hound-hair', segs=4, radii=radii)
    return [pc.make()]


def _ruff(throat):
    """White fur: round the throat under the skull (throat) or as a beard under the chin (on the jaw)."""
    pc = Piece('hound-ruff-' + ('throat' if throat else 'beard'))
    rnd = random.Random(11 if throat else 13)
    count = 96 if throat else 40
    for k in range(count):
        if throat:
            a = rnd.uniform(-math.pi * .9, math.pi * .9)
            root = Vector((math.sin(a) * .05, -.03 + math.cos(a) * .03 - .01, -.035 + rnd.uniform(-.015, .015)))
            d = Vector((math.sin(a) * .5, -.2 + math.cos(a) * .3, -1)).normalized()
        else:
            t = rnd.uniform(0, 1)
            root = Vector((rnd.uniform(-.026, .026) * (1 - t * .6), .07 + .11 * t, -.02 + rnd.uniform(-.004, .004)))
            d = Vector((root.x * 4, .25, -1)).normalized()
        ln = rnd.uniform(.03, .06) * (1.15 if throat else 1)
        mid = root + d * ln * .5 + Vector((rnd.uniform(-.006, .006), -.006, -ln * .12))
        tip = root + d * ln + Vector((rnd.uniform(-.008, .008), rnd.uniform(-.008, .008), -ln * .2))
        pts = catmull([root, mid, tip], per=2)
        radii = [.0085 * (1 - i / (len(pts) - 1)) ** .8 + .0003 for i in range(len(pts))]
        tube(pc, pts, .007, 'hound-fur', segs=4, radii=radii)
    return [pc.make()]


# ---------------------------------------------------------------------------------------------------------------------------
# The jaw: hinged under the skull's back (JAW_HINGE), its own teeth and beard
# ---------------------------------------------------------------------------------------------------------------------------
def _jaw_elems():
    return mirrored([
        E(.045, .0, .0, .016, .03, .03),          # the jaw's rami up to the hinge
        E(0, .07, -.008, .042, .06, .02),         # the jaw's body
        E(0, .13, -.006, .03, .05, .017),
        E(0, .182, -.003, .02, .026, .015),       # the chin
        E(.024, .11, .0, .016, .05, .01),         # the lower lips
    ])


def jaw():
    out = []
    pc = _meta('hound-jaw', _jaw_elems())
    for f in pc.bm.faces:
        c = f.calc_center_median()
        f.material_index = pc.slot('hound-lip' if c.z > -.002 and c.y > .07 else 'hound-skin')
    out.append(pc.make(recalc=True))
    out += _teeth(upper=False)
    out += _ruff(throat=False)
    return out


def props(head):
    finishes()
    return [
        ('hound-skull', 'head', head, skull),
        ('hound-jaw', 'head', head, jaw),
    ]
