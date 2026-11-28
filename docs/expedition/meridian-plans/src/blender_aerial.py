"""Aerial greybox of one Meridian street plan, built from its JSON spec, at dusk.
blender -b --factory-startup --python blender_aerial.py -- <layout.json> <out.png> [samples]"""
import bpy, json, math, sys

argv = sys.argv[sys.argv.index('--') + 1:]
SPEC, OUT = argv[0], argv[1]
SAMPLES = int(argv[2]) if len(argv) > 2 else 96
J = json.load(open(SPEC, encoding='utf-8'))

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
scene = bpy.context.scene


def lerp(a, b, f): return tuple(a[i] + (b[i] - a[i]) * f for i in range(len(a)))


def hexc(h):
    h = h.lstrip('#'); c = tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    return tuple(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c)


def material(name, color, rough=0.85, emit=None, strength=0.0, windows=False):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name)
    try: m.use_nodes = True
    except Exception: pass
    nt = m.node_tree; bsdf = nt.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = rough
    if emit is not None:
        bsdf.inputs['Emission Color'].default_value = (*emit, 1)
        bsdf.inputs['Emission Strength'].default_value = strength
    if windows:
        # lit windows on the 2.5 m bay and 3 m storey: a brick texture as the window grid, a quarter of them lit
        tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
        nt.links.new(tc.outputs['Object'] if False else tc.outputs['Generated'], sep.inputs[0]) if False else None
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        sep2 = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(geo.outputs['Position'], sep2.inputs[0])
        add = nt.nodes.new('ShaderNodeMath'); add.operation = 'ADD'
        nt.links.new(sep2.outputs['X'], add.inputs[0]); nt.links.new(sep2.outputs['Y'], add.inputs[1])
        comb = nt.nodes.new('ShaderNodeCombineXYZ')
        nt.links.new(add.outputs[0], comb.inputs['X']); nt.links.new(sep2.outputs['Z'], comb.inputs['Y'])
        br = nt.nodes.new('ShaderNodeTexBrick')
        br.inputs['Scale'].default_value = 1.0
        br.inputs['Mortar Size'].default_value = 0.85
        br.inputs['Brick Width'].default_value = 2.5
        br.inputs['Row Height'].default_value = 3.0
        br.offset = 0.0
        br.inputs['Color1'].default_value = (0, 0, 0, 1); br.inputs['Color2'].default_value = (1, 1, 1, 1)
        nt.links.new(comb.outputs[0], br.inputs['Vector'])
        bw = nt.nodes.new('ShaderNodeRGBToBW'); nt.links.new(br.outputs['Color'], bw.inputs[0])
        gt = nt.nodes.new('ShaderNodeMath'); gt.operation = 'GREATER_THAN'; gt.inputs[1].default_value = 0.8
        nt.links.new(bw.outputs[0], gt.inputs[0])
        inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
        nt.links.new(br.outputs['Fac'], inv.inputs[1])
        mul = nt.nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'
        nt.links.new(gt.outputs[0], mul.inputs[0]); nt.links.new(inv.outputs[0], mul.inputs[1])
        nz = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(geo.outputs['Normal'], nz.inputs[0])
        ab = nt.nodes.new('ShaderNodeMath'); ab.operation = 'ABSOLUTE'; nt.links.new(nz.outputs['Z'], ab.inputs[0])
        lt = nt.nodes.new('ShaderNodeMath'); lt.operation = 'LESS_THAN'; lt.inputs[1].default_value = 0.3
        nt.links.new(ab.outputs[0], lt.inputs[0])
        # no windows in the ground storey's lowest metre band
        hz = nt.nodes.new('ShaderNodeMath'); hz.operation = 'MULTIPLY'
        nt.links.new(mul.outputs[0], hz.inputs[0]); nt.links.new(lt.outputs[0], hz.inputs[1])
        st = nt.nodes.new('ShaderNodeMath'); st.operation = 'MULTIPLY'; st.inputs[1].default_value = 3.0
        nt.links.new(hz.outputs[0], st.inputs[0])
        bsdf.inputs['Emission Color'].default_value = (1.0, 0.62, 0.28, 1)
        nt.links.new(st.outputs[0], bsdf.inputs['Emission Strength'])
    return m


def add_mesh(name, verts, faces, mat):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    return ob


class Batch:
    def __init__(self): self.v, self.f = [], []

    def box(self, x0, z0, x1, z1, b, t):
        if t - b <= 0.001 or x1 - x0 <= 0.001 or z1 - z0 <= 0.001: return
        n = len(self.v)
        # game (x, y, z) -> blender (x, z, y)
        for (x, z, y) in ((x0, z0, b), (x1, z0, b), (x1, z1, b), (x0, z1, b), (x0, z0, t), (x1, z0, t), (x1, z1, t), (x0, z1, t)):
            self.v.append((x, z, y))
        self.f += [(n, n + 3, n + 2, n + 1), (n + 4, n + 5, n + 6, n + 7), (n, n + 1, n + 5, n + 4),
                   (n + 1, n + 2, n + 6, n + 5), (n + 2, n + 3, n + 7, n + 6), (n + 3, n, n + 4, n + 7)]

    def wedge(self, rp):
        x0, z0, x1, z1, lo, hi, b = rp['x0'], rp['z0'], rp['x1'], rp['z1'], rp['ylo'], rp['yhi'], rp['bottom']
        t = rp['toward']
        def h(x, z):
            if t == 'N': return hi if z == z1 else lo
            if t == 'S': return hi if z == z0 else lo
            if t == 'E': return hi if x == x1 else lo
            return hi if x == x0 else lo
        c = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
        n = len(self.v)
        for (x, z) in c: self.v.append((x, z, b))
        for (x, z) in c: self.v.append((x, z, h(x, z)))
        self.f += [(n, n + 3, n + 2, n + 1), (n + 4, n + 5, n + 6, n + 7), (n, n + 1, n + 5, n + 4),
                   (n + 1, n + 2, n + 6, n + 5), (n + 2, n + 3, n + 7, n + 6), (n + 3, n, n + 4, n + 7)]

    def stairs(self, rp):
        x0, z0, x1, z1, lo, hi = rp['x0'], rp['z0'], rp['x1'], rp['z1'], rp['ylo'], rp['yhi']
        n = max(1, int(math.ceil((hi - lo) / 0.25 - 1e-6)))
        t = rp['toward']
        for i in range(n):
            f0, f1 = i / n, (i + 1) / n
            top = lo + (hi - lo) * (i + 1) / n
            if t == 'N': self.box(x0, z0 + (z1 - z0) * f0, x1, z0 + (z1 - z0) * f1, lo - 0.25, top)
            elif t == 'S': self.box(x0, z1 - (z1 - z0) * f1, x1, z1 - (z1 - z0) * f0, lo - 0.25, top)
            elif t == 'E': self.box(x0 + (x1 - x0) * f0, z0, x0 + (x1 - x0) * f1, z1, lo - 0.25, top)
            else: self.box(x1 - (x1 - x0) * f1, z0, x1 - (x1 - x0) * f0, z1, lo - 0.25, top)

    def quad(self, a, b, c, d):
        n = len(self.v); self.v += [a, b, c, d]; self.f.append((n, n + 1, n + 2, n + 3))


TIERS = [(-21, (0.08, 0.10, 0.14)), (-14, (0.11, 0.14, 0.19)), (-7, (0.16, 0.19, 0.24)), (0, (0.24, 0.25, 0.27)),
         (7, (0.33, 0.31, 0.29)), (14, (0.42, 0.37, 0.31)), (21, (0.50, 0.43, 0.33)), (28, (0.58, 0.49, 0.36))]


def tier_col(y):
    for (a, ca), (b, cb) in zip(TIERS, TIERS[1:]):
        if y <= b: return lerp(ca, cb, max(0, (y - a) / (b - a)))
    return TIERS[-1][1]


batches = {}
def B(key): return batches.setdefault(key, Batch())


for g in J['ground']:
    B(('tier', g['top'])).box(g['x0'], g['z0'], g['x1'], g['z1'], g['bottom'], g['top'])
for rp in J['ramps']:
    (B(('stair', 0)).stairs(rp) if rp['kind'] == 'stair' else B(('ramp', 0)).wedge(rp))
ROLE = {'lot': 'brick', 'hall': 'hall', 'city-hall': 'civic', 'tower': 'civic', 'bridge': 'deck', 'deck': 'deck', 'viaduct': 'blue',
        'pier': 'blue', 'parapet': 'stone', 'court-wall': 'stone', 'court-gate': 'gilt', 'wall': 'stone', 'post': 'iron',
        'arcade-roof': 'slate', 'lid': 'slate', 'tunnel-roof': 'stone', 'wagon': 'wagon', 'landmark': 'stone', 'shed': 'brick-dark',
        'cordon': 'concrete', 'glass': 'glass'}
for b in J['blocks']:
    role = ROLE.get(b['role'], 'stone')
    if role == 'brick':
        h = b['top'] - b['bottom']; role = 'brick-%d' % (0 if h <= 8 else 1 if h <= 14 else 2 if h <= 20 else 3)
    B((role, 0)).box(b['x0'], b['z0'], b['x1'], b['z1'], b['bottom'], b['top'])

mats = {'brick-0': material('brick-0', hexc('#9a5a44'), windows=True), 'brick-1': material('brick-1', hexc('#84493a'), windows=True),
        'brick-2': material('brick-2', hexc('#6e3d31'), windows=True), 'brick-3': material('brick-3', hexc('#5a3229'), windows=True),
        'brick-dark': material('brick-dark', hexc('#5b3a30')), 'hall': material('hall', hexc('#b9ad9a')),
        'civic': material('civic', hexc('#d8cdb5'), 0.7), 'deck': material('deck', hexc('#8a8680')),
        'blue': material('blue', hexc('#4c5260')), 'stone': material('stone', hexc('#9b9488')),
        'gilt': material('gilt', hexc('#c9a24a'), 0.4), 'iron': material('iron', hexc('#2a2826')),
        'slate': material('slate', hexc('#3b3f46')), 'wagon': material('wagon', hexc('#5a2f24')),
        'concrete': material('concrete', hexc('#77777a')), 'glass': material('glass', hexc('#9fd6e8'), 0.1),
        'stair': material('stairmat', hexc('#b8ae9c')), 'ramp': material('rampmat', hexc('#8f877b'))}
for key, bt in batches.items():
    kind, val = key
    if kind == 'tier': m = material('tier-%g' % val, tier_col(val), 0.9)
    elif kind in ('stair', 'ramp'): m = mats[kind]
    else: m = mats[kind]
    add_mesh('%s-%g' % (kind, val), bt.v, bt.f, m)

# the camp: a lawn, the brazier and a soft fire light
camp = Batch(); camp.box(-24.5, -24.5, 24.5, 24.5, -0.2, 0.06)
add_mesh('camp', camp.v, camp.f, material('lawn', hexc('#3a5a3e')))
fence = Batch()
for s in (-1, 1):
    for (a0, a1) in ((-24.8, -5), (5, 24.8)):
        fence.box(a0, s * 25 - 0.3, a1, s * 25 + 0.3, 0, 3.2); fence.box(s * 25 - 0.3, a0, s * 25 + 0.3, a1, 0, 3.2)
add_mesh('fence', fence.v, fence.f, material('timber', hexc('#5e4630')))
fire = Batch(); fire.box(-1.2, -1.2, 1.2, 1.2, 0, 1.0)
add_mesh('fire', fire.v, fire.f, material('fire', (1, .4, .1), emit=(1, .45, .12), strength=30))


def point(name, loc, energy, color, radius=0.3):
    ld = bpy.data.lights.new(name, 'POINT'); ld.energy = energy; ld.color = color; ld.shadow_soft_size = radius
    ob = bpy.data.objects.new(name, ld); ob.location = loc; scene.collection.objects.link(ob)


point('brazier', (0, 0, 3), 6000, (1, .55, .25), 1.0)

# lamps about every 12 m along the routes: a glowing head and a sodium pool
heads = Batch()
for k, (x, y, z) in enumerate(J['lamps']):
    heads.box(x - .35, z - .35, x + .35, z + .35, y + 4.2, y + 4.7)
    point('lamp-%d' % k, (x, z, y + 4.4), 900, (1, .62, .3), 0.2)
add_mesh('lamp-heads', heads.v, heads.f, material('sodium', (1, .6, .3), emit=(1, .6, .28), strength=40))

# route ribbons, faint, in each route's colour, so the four ways out read from the air
for r in J['routes']:
    rb = Batch()
    pts = r['points']
    for (a, b) in zip(pts, pts[1:]):
        ax_, ay, az = a; bx, by, bz = b
        dx, dz = bx - ax_, bz - az; L = math.hypot(dx, dz)
        if L <= 0: continue
        px, pz = -dz / L * 0.6, dx / L * 0.6
        rb.quad((ax_ - px, az - pz, ay + .12), (bx - px, bz - pz, by + .12), (bx + px, bz + pz, by + .12), (ax_ + px, az + pz, ay + .12))
    c = hexc(r['color'])
    add_mesh('route-' + r['id'], rb.v, rb.f, material('route-' + r['id'], c, emit=c, strength=6))

# doors: a glowing mouth in each hall's face; City Hall's four clock faces
dm = Batch()
for d in J['doors']:
    x, y, z, f = d['x'], d['y'], d['z'], d['facing']
    if f in 'NS': dm.box(x - 5, z - 0.4, x + 5, z + 0.4, y, y + 9)
    else: dm.box(x - 0.4, z - 5, x + 0.4, z + 5, y, y + 9)
add_mesh('door-mouths', dm.v, dm.f, material('door-glow', (.8, .85, 1), emit=(.75, .85, 1), strength=12))
c = J['court']; t = c['tower']
cf = Batch(); top = t['top'] - 10
for (x0, z0, x1, z1) in ((t['x'] - 3, t['z'] - 6.2, t['x'] + 3, t['z'] - 6.0), (t['x'] - 3, t['z'] + 6.0, t['x'] + 3, t['z'] + 6.2),
                         (t['x'] - 6.2, t['z'] - 3, t['x'] - 6.0, t['z'] + 3), (t['x'] + 6.0, t['z'] - 3, t['x'] + 6.2, t['z'] + 3)):
    cf.box(x0, z0, x1, z1, top - 3, top + 3)
add_mesh('clock-faces', cf.v, cf.f, material('clock', hexc('#f2e6c6'), emit=hexc('#f2e6c6'), strength=25))
point('court-flood', (c['x'], c['z'], c['y'] + 14), 20000, (1, .85, .6), 3)

# beyond the Cordon: dark suburbs on a low plain, a few windows still lit (skyline art only)
import random
rnd = random.Random(647)
bd0 = J['bounds']; low = bd0['killY'] + 3
plain = Batch(); plain.box(bd0['minX'] - 2500, bd0['minZ'] - 2500, bd0['maxX'] + 2500, bd0['maxZ'] + 2500, low - 1, low)
add_mesh('plain', plain.v, plain.f, material('plain', (0.018, 0.02, 0.028), 0.95))
sub, win = Batch(), Batch()
for k in range(900):
    while True:
        x = rnd.uniform(bd0['minX'] - 900, bd0['maxX'] + 900); z = rnd.uniform(bd0['minZ'] - 900, bd0['maxZ'] + 900)
        if not (bd0['minX'] - 20 < x < bd0['maxX'] + 20 and bd0['minZ'] - 20 < z < bd0['maxZ'] + 20): break
    w_, d_, h_ = rnd.choice((10, 15, 20, 25)), rnd.choice((10, 15, 20)), rnd.choice((7, 10, 13, 16, 22))
    sub.box(x, z, x + w_, z + d_, low, low + h_)
    if rnd.random() < 0.35:
        win.box(x + w_ * 0.3, z - 0.2, x + w_ * 0.3 + 1.4, z + 0.05, low + h_ * 0.5, low + h_ * 0.5 + 1.8)
add_mesh('suburbs', sub.v, sub.f, material('suburb', (0.03, 0.03, 0.04), 0.95))
add_mesh('suburb-windows', win.v, win.f, material('suburb-win', (1, .6, .3), emit=(1, .6, .3), strength=14))

# dusk: a low warm sun out of the west, a deep blue sky with a sodium-orange horizon
sd = bpy.data.lights.new('sun', 'SUN'); sd.energy = 2.6; sd.color = (1.0, 0.55, 0.32); sd.angle = math.radians(2)
so = bpy.data.objects.new('sun', sd); so.rotation_euler = (math.radians(84), 0, math.radians(-115)); scene.collection.objects.link(so)
md = bpy.data.lights.new('moon', 'SUN'); md.energy = 0.35; md.color = (0.55, 0.65, 1.0)
mo = bpy.data.objects.new('moon', md); mo.rotation_euler = (math.radians(35), 0, math.radians(150)); scene.collection.objects.link(mo)
w = bpy.data.worlds.new('dusk'); scene.world = w
try: w.use_nodes = True
except Exception: pass
nt = w.node_tree; bg = nt.nodes.get('Background')
tc = nt.nodes.new('ShaderNodeTexCoord'); sp = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(tc.outputs['Generated'], sp.inputs[0])
ramp = nt.nodes.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (0.30, 0.12, 0.06, 1)
ramp.color_ramp.elements[1].position = 0.22; ramp.color_ramp.elements[1].color = (0.012, 0.02, 0.05, 1)
e = ramp.color_ramp.elements.new(0.06); e.color = (0.10, 0.06, 0.09, 1)
mr = nt.nodes.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 0.0; mr.inputs['From Max'].default_value = 1.0
nt.links.new(sp.outputs['Z'], mr.inputs['Value']); nt.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
nt.links.new(ramp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.0

# camera above the hub at 45 degrees, looking north toward City Hall
cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 22; cam_d.clip_end = 3000
cam = bpy.data.objects.new('cam', cam_d); scene.collection.objects.link(cam); scene.camera = cam
bd = J['bounds']
tx = 0.55 * (bd['minX'] + bd['maxX']) / 2
tz = 0.55 * (bd['minZ'] + bd['maxZ']) / 2 + 35
dist = 470.0
cam.location = (tx, tz - dist * math.cos(math.radians(45)), dist * math.sin(math.radians(45)))
cam.rotation_euler = (math.radians(45), 0, 0)

scene.render.engine = 'CYCLES'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for dev in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = dev
            prefs.refresh_devices() if hasattr(prefs, 'refresh_devices') else prefs.get_devices()
            if any(d.type == dev for d in prefs.devices):
                for d in prefs.devices: d.use = d.type == dev
                scene.cycles.device = 'GPU'; break
        except Exception: continue
except Exception as ex:
    print('GPU setup failed', ex)
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 4
scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
scene.render.film_transparent = False
try: scene.view_settings.view_transform = 'AgX'
except Exception: pass
scene.view_settings.exposure = 0.55
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('WROTE', OUT, 'device', scene.cycles.device)
