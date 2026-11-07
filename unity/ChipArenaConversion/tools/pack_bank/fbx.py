"""Read FBX files (binary 7.x and ASCII) into meshes as Unity imports them. Data only: nothing in a file is run.

Unity's model importer, as this mirrors it (docs/PACK-BANK.md):
- a mesh asset holds its node's geometry in the node's own space, with the node's geometric offset baked in, the file's
  unit and the import's scale factor applied (metres) and the X axis mirrored (FBX is right-handed, Unity left-handed);
  the triangle order is kept, which the mirror turns into Unity's clockwise front faces;
- a node's GameObject takes the node's transform, mirrored the same way; the file's root nodes also take the axis
  conversion from the file's up/front axes to Y up, Z forward (unless the import bakes it, bakeAxisConversion).
"""
import math
import re
import struct
import zlib


class FbxError(Exception):
    pass


# ---------------------------------------------------------------------------------------------------------------------
# The node tree.

class Node:
    __slots__ = ('name', 'props', 'children')

    def __init__(self, name, props, children):
        self.name, self.props, self.children = name, props, children

    def child(self, name):
        for c in self.children:
            if c.name == name:
                return c
        return None

    def all(self, name):
        return [c for c in self.children if c.name == name]

    def value(self, name, default=None):
        c = self.child(name)
        if c is None or not c.props:
            return default
        return c.props[0] if len(c.props) == 1 else c.props


def _binary(data):
    version = struct.unpack_from('<I', data, 23)[0]
    wide = version >= 7500
    null = 25 if wide else 13

    def prop(off):
        t = chr(data[off])
        off += 1
        if t in 'YCIFDL':
            fmt = {'Y': '<h', 'C': '<?', 'I': '<i', 'F': '<f', 'D': '<d', 'L': '<q'}[t]
            return struct.unpack_from(fmt, data, off)[0], off + struct.calcsize(fmt)
        if t in 'fdlib':
            n, enc, clen = struct.unpack_from('<III', data, off)
            off += 12
            raw = data[off:off + clen]
            off += clen
            if enc == 1:
                raw = zlib.decompress(raw)
            fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': '?'}[t]
            size = struct.calcsize(fmt)
            if len(raw) < n * size:
                raise FbxError('short array')
            return list(struct.unpack('<%d%s' % (n, fmt), raw[:n * size])), off
        if t in 'SR':
            n = struct.unpack_from('<I', data, off)[0]
            off += 4
            return bytes(data[off:off + n]), off + n
        raise FbxError('property type %r' % t)

    def node(off):
        if wide:
            end, count, length = struct.unpack_from('<QQQ', data, off)
            off += 24
        else:
            end, count, length = struct.unpack_from('<III', data, off)
            off += 12
        nlen = data[off]
        off += 1
        name = bytes(data[off:off + nlen]).decode('latin1')
        off += nlen
        if end == 0:
            return None, off
        if end > len(data):
            raise FbxError('node past the end')
        props, p = [], off
        for _ in range(count):
            v, p = prop(p)
            props.append(v)
        off += length
        children = []
        while off < end:
            if end - off == null and not any(data[off:end]):
                off = end
                break
            child, off = node(off)
            if child is None:
                break
            children.append(child)
        return Node(name, props, children), end

    top, off = [], 27
    while off + null <= len(data):
        n, off = node(off)
        if n is None:
            break
        top.append(n)
    return top


_TOKEN = re.compile(r'\s*(?:;[^\n]*\n)*\s*("(?:[^"\\]|\\.)*"|\*\d+|[{}:,]|[A-Za-z_][\w|]*|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)')


def _ascii(text):
    tokens = [m.group(1) for m in _TOKEN.finditer(text)]
    pos = 0

    def scalar(tok):
        if tok.startswith('"'):
            return tok[1:-1].encode('utf-8').replace(b'::', b'\x00\x01') if '::' in tok else tok[1:-1].encode('utf-8')
        try:
            return int(tok)
        except ValueError:
            try:
                return float(tok)
            except ValueError:
                return tok.encode('utf-8')

    def body():
        nonlocal pos
        nodes = []
        while pos < len(tokens) and tokens[pos] != '}':
            name = tokens[pos]
            pos += 1
            if pos >= len(tokens) or tokens[pos] != ':':
                raise FbxError('ascii: expected ":" after ' + name)
            pos += 1
            props, children = [], []
            while pos < len(tokens):
                tok = tokens[pos]
                if tok == '{':
                    pos += 1
                    if pos < len(tokens) and tokens[pos].startswith('*'):  # an array: *N { a: v, v, ... }
                        pos += 1
                        arr = []
                        while tokens[pos] != '}':
                            if tokens[pos] in ('a', ':', ','):
                                pos += 1
                                continue
                            arr.append(scalar(tokens[pos]))
                            pos += 1
                        pos += 1
                        props.append(arr)
                    else:
                        children = body()
                        pos += 1  # '}'
                    break
                if tok == ',':
                    pos += 1
                    continue
                if pos + 1 < len(tokens) and tokens[pos + 1] == ':' and not tok.startswith('"'):
                    break  # the next node's name
                if tok == '}':
                    break
                props.append(scalar(tok))
                pos += 1
            nodes.append(Node(name, props, children))
        return nodes

    return body()


def parse(data):
    if data[:20] == b'Kaydara FBX Binary  ':
        return _binary(data)
    return _ascii(data.decode('utf-8', 'replace'))


# ---------------------------------------------------------------------------------------------------------------------
# 4x4 matrices (row major lists, column vectors).

def ident():
    return [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]


def mul(a, b):
    return [sum(a[r * 4 + k] * b[k * 4 + c] for k in range(4)) for r in range(4) for c in range(4)]


def translate(v):
    m = ident()
    m[3], m[7], m[11] = v[0], v[1], v[2]
    return m


def scale(v):
    m = ident()
    m[0], m[5], m[10] = v[0], v[1], v[2]
    return m


def _axis(i, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    m = ident()
    if i == 0:
        m[5], m[6], m[9], m[10] = c, -s, s, c
    elif i == 1:
        m[0], m[2], m[8], m[10] = c, s, -s, c
    else:
        m[0], m[1], m[4], m[5] = c, -s, s, c
    return m


# FBX rotation orders: the first named axis turns first.
_ORDERS = {0: (0, 1, 2), 1: (0, 2, 1), 2: (1, 2, 0), 3: (1, 0, 2), 4: (2, 0, 1), 5: (2, 1, 0), 6: (0, 1, 2)}


def euler(v, order=0):
    m = ident()
    for i in _ORDERS.get(int(order), (0, 1, 2)):
        m = mul(_axis(i, v[i]), m)
    return m


def inverse(m):
    """Inverse of an affine 4x4."""
    a = [m[0], m[1], m[2], m[4], m[5], m[6], m[8], m[9], m[10]]
    det = a[0] * (a[4] * a[8] - a[5] * a[7]) - a[1] * (a[3] * a[8] - a[5] * a[6]) + a[2] * (a[3] * a[7] - a[4] * a[6])
    if abs(det) < 1e-20:
        raise FbxError('singular transform')
    inv = [(a[4] * a[8] - a[5] * a[7]) / det, (a[2] * a[7] - a[1] * a[8]) / det, (a[1] * a[5] - a[2] * a[4]) / det,
           (a[5] * a[6] - a[3] * a[8]) / det, (a[0] * a[8] - a[2] * a[6]) / det, (a[2] * a[3] - a[0] * a[5]) / det,
           (a[3] * a[7] - a[4] * a[6]) / det, (a[1] * a[6] - a[0] * a[7]) / det, (a[0] * a[4] - a[1] * a[3]) / det]
    t = [m[3], m[7], m[11]]
    out = ident()
    for r in range(3):
        for c in range(3):
            out[r * 4 + c] = inv[r * 3 + c]
        out[r * 4 + 3] = -sum(inv[r * 3 + k] * t[k] for k in range(3))
    return out


def point(m, v):
    return (m[0] * v[0] + m[1] * v[1] + m[2] * v[2] + m[3],
            m[4] * v[0] + m[5] * v[1] + m[6] * v[2] + m[7],
            m[8] * v[0] + m[9] * v[1] + m[10] * v[2] + m[11])


def normal_matrix(m):
    """The inverse transpose of m's 3x3, as a 4x4 without translation."""
    i = inverse(m)
    return [i[0], i[4], i[8], 0, i[1], i[5], i[9], 0, i[2], i[6], i[10], 0, 0, 0, 0, 1.0]


def direction(m, v):
    x, y, z = m[0] * v[0] + m[1] * v[1] + m[2] * v[2], m[4] * v[0] + m[5] * v[1] + m[6] * v[2], m[8] * v[0] + m[9] * v[1] + m[10] * v[2]
    n = math.sqrt(x * x + y * y + z * z)
    return (x / n, y / n, z / n) if n > 1e-12 else (0.0, 1.0, 0.0)


MIRROR = scale((-1.0, 1.0, 1.0))


# ---------------------------------------------------------------------------------------------------------------------
# The scene.

def _p70(node):
    out = {}
    if node is None:
        return out
    p = node.child('Properties70')
    for pp in (p.children if p else []):
        if pp.props and isinstance(pp.props[0], bytes):
            out[pp.props[0].decode('latin1')] = pp.props[4:]
    return out


def _name(raw):
    if isinstance(raw, bytes):
        return raw.split(b'\x00')[0].decode('utf-8', 'replace')
    return str(raw)


class Model:
    def __init__(self, uid, name, kind, props):
        self.uid, self.name, self.kind, self.props = uid, name, kind, props
        self.parent, self.children, self.geometry, self.materials = None, [], None, []

    def vec(self, key, default):
        v = self.props.get(key)
        return tuple(float(x) for x in v[:3]) if v and len(v) >= 3 else default

    def flag(self, key, default=0):
        v = self.props.get(key)
        return int(v[0]) if v else default

    def local(self):
        """The FBX SDK's local transform: T Roff Rp Rpre R Rpost^-1 Rp^-1 Soff Sp S Sp^-1."""
        zero, one = (0.0, 0.0, 0.0), (1.0, 1.0, 1.0)
        t, r, s = self.vec('Lcl Translation', zero), self.vec('Lcl Rotation', zero), self.vec('Lcl Scaling', one)
        active = self.flag('RotationActive', 0)
        pre = self.vec('PreRotation', zero) if active else zero
        post = self.vec('PostRotation', zero) if active else zero
        order = self.flag('RotationOrder', 0) if active else 0
        roff, rp = self.vec('RotationOffset', zero), self.vec('RotationPivot', zero)
        soff, sp = self.vec('ScalingOffset', zero), self.vec('ScalingPivot', zero)
        m = translate(t)
        for x in (translate(roff), translate(rp), euler(pre), euler(r, order), inverse(euler(post)), translate(tuple(-c for c in rp)),
                  translate(soff), translate(sp), scale(s), translate(tuple(-c for c in sp))):
            m = mul(m, x)
        return m

    def geometric(self):
        zero, one = (0.0, 0.0, 0.0), (1.0, 1.0, 1.0)
        return mul(mul(translate(self.vec('GeometricTranslation', zero)), euler(self.vec('GeometricRotation', zero))),
                   scale(self.vec('GeometricScaling', one)))

    def world(self):
        m, n = ident(), self
        chain = []
        while n is not None:
            chain.append(n)
            n = n.parent
        for n in reversed(chain):
            m = mul(m, n.local())
        return m


class Scene:
    def __init__(self, top):
        by = {n.name: n for n in top}
        settings = _p70(by.get('GlobalSettings'))
        self.unit = float(settings.get('UnitScaleFactor', [1.0])[0])
        self.up = (int(settings.get('UpAxis', [1])[0]), int(settings.get('UpAxisSign', [1])[0]))
        self.front = (int(settings.get('FrontAxis', [2])[0]), int(settings.get('FrontAxisSign', [1])[0]))
        self.coord = (int(settings.get('CoordAxis', [0])[0]), int(settings.get('CoordAxisSign', [1])[0]))
        templates = {}
        defs = by.get('Definitions')
        for ot in (defs.all('ObjectType') if defs else []):
            for pt in ot.all('PropertyTemplate'):
                templates[_name(ot.props[0]) if ot.props else ''] = _p70(pt)
        objects = by.get('Objects')
        self.models, self.geometries, self.materials = {}, {}, {}
        for o in (objects.children if objects else []):
            uid = o.props[0] if o.props else None
            if o.name == 'Model':
                props = dict(templates.get('Model', {}))
                props.update(_p70(o))
                self.models[uid] = Model(uid, _name(o.props[1]), _name(o.props[2]) if len(o.props) > 2 else '', props)
            elif o.name == 'Geometry' and len(o.props) > 2 and _name(o.props[2]) == 'Mesh':
                self.geometries[uid] = o
            elif o.name == 'Material':
                self.materials[uid] = _name(o.props[1])
        conns = by.get('Connections')
        for c in (conns.all('C') if conns else []):
            kind, child, parent = _name(c.props[0]), c.props[1], c.props[2]
            if kind != 'OO':
                continue
            if child in self.geometries and parent in self.models:
                self.models[parent].geometry = self.geometries[child]
            elif child in self.models and parent in self.models:
                self.models[child].parent = self.models[parent]
                self.models[parent].children.append(self.models[child])
            elif child in self.materials and parent in self.models:
                self.models[parent].materials.append(self.materials[child])

    def axis_conversion(self):
        """The rotation from the file's axes to Y up, Z front, X coord (the FBX SDK's ConvertScene to Unity's)."""
        def unit(axis, sign):
            v = [0.0, 0.0, 0.0]
            v[axis] = float(sign)
            return v
        rows = [unit(*self.coord), unit(*self.up), unit(*self.front)]
        m = ident()
        for r in range(3):
            for c in range(3):
                m[r * 4 + c] = rows[r][c]
        return m

    def roots(self):
        return [m for m in self.models.values() if m.parent is None]


# ---------------------------------------------------------------------------------------------------------------------
# Geometry to triangles.

def _layer(geom, kind, key, index_key, size, polys, count_cp):
    """Per polygon-vertex values of a layer element (or None)."""
    el = geom.child(kind)
    if el is None:
        return None
    data = el.value(key)
    if data is None:
        return None
    if not isinstance(data, list):
        data = [data]
    mapping = _name(el.value('MappingInformationType', b'ByPolygonVertex'))
    ref = _name(el.value('ReferenceInformationType', b'Direct'))
    index = el.value(index_key) if ref in ('IndexToDirect', 'Index') else None
    if index is not None and not isinstance(index, list):
        index = [index]
    vals = [tuple(data[i * size:(i + 1) * size]) for i in range(len(data) // size)]

    def pick(i):
        j = index[i] if index is not None else i
        return vals[j] if 0 <= j < len(vals) else vals[0] if vals else None

    out = []
    pv, poly = 0, 0
    for raw in polys:
        cp = raw if raw >= 0 else ~raw
        if mapping == 'ByPolygonVertex':
            out.append(pick(pv))
        elif mapping in ('ByVertice', 'ByVertex', 'ByControlPoint'):
            out.append(pick(cp))
        elif mapping == 'ByPolygon':
            out.append(pick(poly))
        else:  # AllSame
            out.append(pick(0))
        pv += 1
        if raw < 0:
            poly += 1
    return out


def _materials(geom, polys):
    el = geom.child('LayerElementMaterial')
    count = sum(1 for p in polys if p < 0)
    if el is None:
        return [0] * count
    mats = el.value('Materials')
    if mats is None:
        return [0] * count
    if not isinstance(mats, list):
        mats = [mats]
    mapping = _name(el.value('MappingInformationType', b'AllSame'))
    if mapping == 'AllSame' or len(mats) < count:
        return [mats[0] if mats else 0] * count
    return mats[:count]


class Mesh:
    """Triangles in Unity space: positions, normals, uv0 (and uv1, colors when present), and one index list per used
    material, in the order of the material slots Unity makes (the used FBX material indices, ascending)."""

    def __init__(self, name):
        self.name = name
        self.positions, self.normals, self.uv0, self.uv1, self.colors = [], [], [], [], []
        self.submeshes, self.slots = [], []

    def bounds(self):
        if not self.positions:
            return (0, 0, 0), (0, 0, 0)
        return tuple(min(p[i] for p in self.positions) for i in range(3)), tuple(max(p[i] for p in self.positions) for i in range(3))


def triangulate(model, transform, name=None, weld=True):
    """The model's geometry under transform (geometry space to Unity space) as a Mesh."""
    geom = model.geometry
    if geom is None:
        return None
    verts = geom.value('Vertices')
    polys = geom.value('PolygonVertexIndex')
    if not verts or not polys:
        return None
    if not isinstance(polys, list):
        polys = [polys]
    cps = [tuple(verts[i * 3:i * 3 + 3]) for i in range(len(verts) // 3)]
    normals = _layer(geom, 'LayerElementNormal', 'Normals', 'NormalsIndex', 3, polys, len(cps))
    uv_layers = sorted(geom.all('LayerElementUV'), key=lambda n: n.props[0] if n.props else 0)
    uvs = []
    for el in uv_layers[:2]:
        holder = Node('g', [], [el])
        uvs.append(_layer(holder, 'LayerElementUV', 'UV', 'UVIndex', 2, polys, len(cps)))
    colors = _layer(geom, 'LayerElementColor', 'Colors', 'ColorIndex', 4, polys, len(cps))
    mats = _materials(geom, polys)
    nmat = normal_matrix(transform)
    mesh = Mesh(name or model.name)
    used = sorted(set(mats))
    slot_of = {m: i for i, m in enumerate(used)}
    mesh.slots = list(used)
    mesh.submeshes = [[] for _ in used]
    cache = {}
    corners, poly = [], 0
    for i, raw in enumerate(polys):
        corners.append(i)
        if raw >= 0:
            continue
        # A fan over the polygon's corners (the FBX SDK triangulates convex polygons the same way).
        if len(corners) >= 3:
            out = []
            for c in corners:
                cp = polys[c] if polys[c] >= 0 else ~polys[c]
                p = point(transform, cps[cp])
                n = direction(nmat, normals[c]) if normals else (0.0, 0.0, 0.0)
                t0 = uvs[0][c] if uvs and uvs[0] else (0.0, 0.0)
                t1 = uvs[1][c] if len(uvs) > 1 and uvs[1] else None
                col = colors[c] if colors else None
                key = (round(p[0], 6), round(p[1], 6), round(p[2], 6), round(n[0], 4), round(n[1], 4), round(n[2], 4),
                       round(t0[0], 6), round(t0[1], 6), t1 and (round(t1[0], 6), round(t1[1], 6)), col and tuple(round(x, 4) for x in col))
                idx = cache.get(key) if weld else None
                if idx is None:
                    idx = len(mesh.positions)
                    mesh.positions.append(p)
                    mesh.normals.append(n)
                    mesh.uv0.append((float(t0[0]), float(t0[1])))
                    if t1 is not None:
                        mesh.uv1.append((float(t1[0]), float(t1[1])))
                    if col is not None:
                        mesh.colors.append(tuple(float(x) for x in col))
                    if weld:
                        cache[key] = idx
                out.append(idx)
            sub = mesh.submeshes[slot_of[mats[poly] if poly < len(mats) else used[0]]]
            for k in range(1, len(out) - 1):
                sub.extend((out[0], out[k], out[k + 1]))
        corners, poly = [], poly + 1
    if len(mesh.uv1) != len(mesh.positions):
        mesh.uv1 = []
    if len(mesh.colors) != len(mesh.positions):
        mesh.colors = []
    if not normals:
        _flat_normals(mesh)
    keep = [i for i, s in enumerate(mesh.submeshes) if s]
    mesh.submeshes = [mesh.submeshes[i] for i in keep]
    mesh.slots = [mesh.slots[i] for i in keep]
    return mesh


def _flat_normals(mesh):
    acc = [[0.0, 0.0, 0.0] for _ in mesh.positions]
    for sub in mesh.submeshes:
        for k in range(0, len(sub), 3):
            a, b, c = (mesh.positions[sub[k + j]] for j in range(3))
            u = [b[i] - a[i] for i in range(3)]
            v = [c[i] - a[i] for i in range(3)]
            # Unity's clockwise front faces: the normal is v x u.
            n = (v[1] * u[2] - v[2] * u[1], v[2] * u[0] - v[0] * u[2], v[0] * u[1] - v[1] * u[0])
            for j in range(3):
                for i in range(3):
                    acc[sub[k + j]][i] += n[i]
    mesh.normals = [direction(ident(), a) for a in acc]


def import_scale(scene, meta):
    """Metres per file unit under the import settings (meta: the .fbx.meta text or '')."""
    def setting(key, default):
        m = re.search(r'^\s*%s:\s*([-0-9.eE+]+)\s*$' % key, meta or '', re.M)
        return float(m.group(1)) if m else default
    s = setting('globalScale', 1.0)
    if setting('useFileScale', 1.0) >= .5:
        s *= scene.unit / 100.0
    return s


def bakes_axis(meta):
    m = re.search(r'^\s*bakeAxisConversion:\s*(\d)\s*$', meta or '', re.M)
    return bool(m and m.group(1) == '1')


def mesh_asset(scene, model, meta):
    """The mesh asset Unity makes for model (its own space: geometric offset, scale, mirror; with a baked axis
    conversion, the root's conversion and the node's world transform as Unity bakes them)."""
    s = import_scale(scene, meta)
    m = mul(scale((s, s, s)), MIRROR)
    if bakes_axis(meta):
        m = mul(m, scene.axis_conversion())
    return triangulate(model, mul(m, model.geometric()))


def node_world(scene, model, meta):
    """The node GameObject's transform in Unity space (the model prefab's root at the origin), times its mesh asset's
    inverse space, i.e. the transform from FBX geometry space to the instanced model's Unity space."""
    s = import_scale(scene, meta)
    conv = ident() if bakes_axis(meta) else scene.axis_conversion()
    return mul(mul(mul(scale((s, s, s)), MIRROR), conv), mul(model.world(), model.geometric()))


def load(path):
    with open(path, 'rb') as f:
        return Scene(parse(f.read()))
