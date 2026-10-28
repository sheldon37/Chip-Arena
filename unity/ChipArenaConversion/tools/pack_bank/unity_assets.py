"""Read a Unity asset folder as data (prefabs, materials, .meta GUIDs) and flatten a prefab into mesh parts.
Data only: nothing in the folder is run. Standard library only."""
import math
import os
import re

import fbx

HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?\s*$', re.M)
REF = re.compile(r'\{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?(?:, type: \d+)?\}')
VEC = re.compile(r'\{x: ([-0-9.eE+]+), y: ([-0-9.eE+]+), z: ([-0-9.eE+]+)(?:, w: ([-0-9.eE+]+))?\}')
GUID = re.compile(r'^guid: ([0-9a-f]{32})\s*$', re.M)


class AssetError(Exception):
    pass


class Folder:
    """Every asset under root by GUID (from the .meta files)."""

    def __init__(self, root):
        self.root = root
        self.by_guid = {}
        for d, _, files in os.walk(root):
            for f in files:
                if not f.endswith('.meta'):
                    continue
                path = os.path.join(d, f)
                with open(path, 'r', encoding='utf-8', errors='replace') as h:
                    m = GUID.search(h.read(2048))
                if m:
                    self.by_guid[m.group(1)] = path[:-5]
        self._scenes, self._metas = {}, {}

    def path(self, guid):
        return self.by_guid.get(guid)

    def meta(self, path):
        if path not in self._metas:
            try:
                with open(path + '.meta', 'r', encoding='utf-8', errors='replace') as h:
                    self._metas[path] = h.read()
            except OSError:
                self._metas[path] = ''
        return self._metas[path]

    def scene(self, path):
        if path not in self._scenes:
            self._scenes[path] = fbx.load(path)
        return self._scenes[path]


def documents(text):
    """(class id, file id, stripped, body) for each YAML document."""
    out = []
    marks = list(HEADER.finditer(text))
    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        out.append((int(m.group(1)), int(m.group(2)), bool(m.group(3)), text[m.end():end]))
    return out


def field(body, key):
    m = re.search(r'^\s*%s: (.*)$' % re.escape(key), body, re.M)
    return m.group(1).strip() if m else None


def ref(body, key):
    v = field(body, key)
    m = REF.search(v or '')
    return (int(m.group(1)), m.group(2)) if m else (0, None)


def vec(body, key, default):
    m = VEC.search(field(body, key) or '')
    if not m:
        return default
    vals = [float(x) for x in m.groups() if x is not None]
    return tuple(vals)


def ref_list(body, key):
    m = re.search(r"^(\s*)%s:\s*\n((?:\1 {0,2}- .*\n?)*)" % re.escape(key), body, re.M)
    if not m:
        return []
    return [(int(r.group(1)), r.group(2)) for r in REF.finditer(m.group(2))]


# Unity transforms ---------------------------------------------------------------------------------------------------

def quat_matrix(q):
    x, y, z, w = q
    n = math.sqrt(x * x + y * y + z * z + w * w) or 1.0
    x, y, z, w = x / n, y / n, z / n, w / n
    return [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0,
            2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0,
            2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0,
            0, 0, 0, 1.0]


def trs(pos, rot, scl):
    return fbx.mul(fbx.mul(fbx.translate(pos), quat_matrix(rot)), fbx.scale(scl))


def material(folder, guid):
    """{'name', 'albedo': path or None, 'emission': path or None, 'color': (r,g,b,a), 'emission_color', 'shader': guid,
    'cutout': bool, 'transparent': bool}."""
    path = folder.path(guid)
    if path is None:
        return None
    with open(path, 'r', encoding='utf-8', errors='replace') as h:
        text = h.read()

    def tex(name):
        m = re.search(r'- %s:\s*\n\s*m_Texture: (\{[^}]*\})' % re.escape(name), text)
        r = REF.search(m.group(1)) if m else None
        return folder.path(r.group(2)) if r and r.group(2) else None

    def albedo():
        for name in ('_MainTex', '_BaseMap', '_BaseColorMap', '_Albedo', '_AlbedoMap', '_Diffuse', '_TextureSample',
                     '_TextureSample0', '_TextureSample1', '_Texture'):
            t = tex(name)
            if t:
                return t
        # Any other texture that is not a ramp, noise, normal or mask map.
        for m in re.finditer(r'- (_\w+):\s*\n\s*m_Texture: (\{[^}]*\})', text):
            if re.search(r'ramp|noise|normal|bump|mask|metal|occlu|height|detail|emiss|highlight', m.group(1), re.I):
                continue
            r = REF.search(m.group(2))
            if r and r.group(2) and folder.path(r.group(2)):
                return folder.path(r.group(2))
        return None

    def color(name, default):
        m = re.search(r'- %s: \{r: ([-0-9.eE+]+), g: ([-0-9.eE+]+), b: ([-0-9.eE+]+), a: ([-0-9.eE+]+)\}' % re.escape(name), text)
        return tuple(float(x) for x in m.groups()) if m else default

    def number(name, default):
        m = re.search(r'- %s: ([-0-9.eE+]+)\s*$' % re.escape(name), text, re.M)
        return float(m.group(1)) if m else default

    shader = ref(text, 'm_Shader')
    keywords = field(text, 'm_ShaderKeywords') or ''
    mode = number('_Mode', 0)
    return {'name': field(text, 'm_Name') or os.path.basename(path), 'path': path,
            'albedo': albedo(),
            'emission': tex('_EmissionMap') if ('_EMISSION' in keywords or tex('_EmissionMap')) else None,
            'color': color('_Color', color('_BaseColor', (1.0, 1.0, 1.0, 1.0))),
            'emission_color': color('_EmissionColor', (0.0, 0.0, 0.0, 1.0)),
            'shader': shader[1], 'cutout': mode == 1 or '_ALPHATEST_ON' in keywords,
            'transparent': mode in (2, 3) or '_ALPHABLEND_ON' in keywords or '_ALPHAPREMULTIPLY_ON' in keywords}


def _fbx_names(meta):
    """fileID -> name for a model's sub-assets (both the legacy and the current table)."""
    out = {}
    for m in re.finditer(r'^\s+(-?\d+): (.+)$', meta.split('externalObjects')[0], re.M):
        out[int(m.group(1))] = m.group(2).strip()
    for m in re.finditer(r'- first:\s*\n\s*(\d+): (-?\d+)\s*\n\s*second: (.+)$', meta, re.M):
        out[int(m.group(2))] = m.group(3).strip()
    return out


def fbx_mesh(folder, guid, file_id):
    """(path, scene, model) of the FBX node whose mesh asset file_id names."""
    path = folder.path(guid)
    if path is None or not path.lower().endswith('.fbx'):
        return None
    meta = folder.meta(path)
    scene = folder.scene(path)
    name = _fbx_names(meta).get(file_id)
    meshes = [m for m in scene.models.values() if m.geometry is not None]
    if name is not None:
        for m in meshes:
            if m.name == name:
                return path, scene, m
    if file_id == 4300000 and meshes:  # the legacy first mesh
        return path, scene, meshes[0]
    if len(meshes) == 1:
        return path, scene, meshes[0]
    return None


class Part:
    """One mesh draw of a flattened prefab: (fbx path, scene, model) under matrix (mesh asset space to prefab space),
    with its renderer's materials (one per submesh)."""

    def __init__(self, source, matrix, materials, name, kind='mesh'):
        self.source, self.matrix, self.materials, self.name, self.kind = source, matrix, materials, name, kind


def flatten(folder, prefab_path, depth=0):
    """The prefab's visible mesh parts in prefab space (its root at the origin)."""
    if depth > 6:
        raise AssetError('prefab nesting too deep')
    with open(prefab_path, 'r', encoding='utf-8', errors='replace') as h:
        docs = documents(h.read())
    objs = {fid: (cls, stripped, body) for cls, fid, stripped, body in docs}
    transforms, by_go = {}, {}
    for fid, (cls, stripped, body) in objs.items():
        if cls in (4, 224) and not stripped:
            go = ref(body, 'm_GameObject')[0]
            transforms[fid] = body
            by_go[go] = fid

    def local(fid):
        body = transforms[fid]
        return trs(vec(body, 'm_LocalPosition', (0, 0, 0)), vec(body, 'm_LocalRotation', (0, 0, 0, 1)),
                   vec(body, 'm_LocalScale', (1, 1, 1)))

    def world(fid):
        m, seen = fbx.ident(), set()
        chain = []
        while fid in transforms and fid not in seen:
            seen.add(fid)
            chain.append(fid)
            fid = ref(transforms[fid], 'm_Father')[0]
        stripped_parent = fid if fid not in transforms else 0
        for f in reversed(chain):
            m = fbx.mul(m, local(f))
        return m, stripped_parent

    def active(go):
        body = objs.get(go, (0, False, ''))[2]
        return field(body, 'm_IsActive') != '0'

    parts = []
    # Instances first, so their parent chains can be resolved.
    instance_matrix = {}
    for fid, (cls, stripped, body) in objs.items():
        if cls != 1001:
            continue
        src = ref(body, 'm_SourcePrefab')[1]
        parent = ref(body, 'm_TransformParent')[0]
        mods = re.findall(r'- target: (\{[^}]*\})\s*\n\s*propertyPath: (.+)\n\s*value: (.*)\n', body)
        pos, rot, scl = [0.0, 0.0, 0.0], [0.0, 0.0, 0.0, 1.0], [1.0, 1.0, 1.0]
        mats = {}
        removed = 'm_RemovedGameObjects' in body and re.search(r'm_RemovedGameObjects:\s*\n\s*- ', body)
        for target, path, value in mods:
            v = value.strip()
            for key, arr, n in (('m_LocalPosition.', pos, 'xyz'), ('m_LocalRotation.', rot, 'xyzw'), ('m_LocalScale.', scl, 'xyz')):
                if path.startswith(key) and path[len(key):] in n and v:
                    try:
                        arr[n.index(path[len(key):])] = float(v)
                    except ValueError:
                        pass
        root = trs(pos, rot, scl)
        pm = world(parent)[0] if parent in transforms else fbx.ident()
        base = fbx.mul(pm, root)
        instance_matrix[fid] = base
        src_path = folder.path(src) if src else None
        if src_path is None:
            continue
        if src_path.lower().endswith('.prefab'):
            for p in flatten(folder, src_path, depth + 1):
                parts.append(Part(p.source, fbx.mul(base, p.matrix), p.materials, p.name, p.kind))
        elif src_path.lower().endswith('.fbx'):
            # A model prefab instance: every node with a mesh, placed as the model prefab places it. Its root is the
            # file's single root node (or a new root over several), whose transform the instance's own replaces.
            scene = folder.scene(src_path)
            meta = folder.meta(src_path)
            remap = _remaps(folder, meta)
            roots = scene.roots()
            rel = fbx.inverse(_unity_world(scene, roots[0], meta)) if len(roots) == 1 else fbx.ident()
            for m in scene.models.values():
                if m.geometry is None:
                    continue
                place = fbx.mul(fbx.mul(base, rel), _unity_world(scene, m, meta))
                mats = [remap.get(name) for name in m.materials]
                parts.append(Part((src_path, scene, m), place, mats, m.name))
    for fid, (cls, stripped, body) in objs.items():
        if cls != 33 or stripped:
            continue
        go = ref(body, 'm_GameObject')[0]
        if not active(go):
            continue
        mesh_ref = ref(body, 'm_Mesh')
        if not mesh_ref[1]:
            continue
        src = fbx_mesh(folder, mesh_ref[1], mesh_ref[0])
        if src is None:
            continue
        renderer = None
        for f2, (c2, s2, b2) in objs.items():
            if c2 == 23 and not s2 and ref(b2, 'm_GameObject')[0] == go:
                renderer = b2
                break
        if renderer is None or field(renderer, 'm_Enabled') == '0':
            continue
        mats = [folder.path(g) if g else None for _, g in ref_list(renderer, 'm_Materials')]
        tf = by_go.get(go)
        m, stripped_parent = world(tf) if tf is not None else (fbx.ident(), 0)
        if stripped_parent:
            sp = objs.get(stripped_parent)
            inst = ref(sp[2], 'm_PrefabInstance')[0] if sp else 0
            if inst in instance_matrix:
                m = fbx.mul(instance_matrix[inst], m)
        name = field(objs.get(go, (0, False, ''))[2], 'm_Name') or os.path.basename(src[0])
        parts.append(Part(src, m, mats, name))
    return parts


def model_parts(folder, model_path):
    """A model file's mesh parts as its model prefab places them (its single root node at the origin), for a pack whose own
    prefabs are binary-serialized: each node with a mesh, with the materials its import remaps (None where it has none)."""
    scene = folder.scene(model_path)
    meta = folder.meta(model_path)
    remap = _remaps(folder, meta)
    roots = scene.roots()
    rel = fbx.inverse(_unity_world(scene, roots[0], meta)) if len(roots) == 1 else fbx.ident()
    parts = []
    for m in scene.models.values():
        if m.geometry is None:
            continue
        parts.append(Part((model_path, scene, m), fbx.mul(rel, _unity_world(scene, m, meta)), [remap.get(name) for name in m.materials], m.name))
    return parts


def _unity_world(scene, model, meta):
    """The node's GameObject transform in the model prefab (Unity space), as a map from its mesh asset's space:
    Sm Mirror C World(n) (Sm Mirror)^-1, with the axis conversion C at the root unless the import bakes it."""
    s = fbx.import_scale(scene, meta)
    sm = fbx.mul(fbx.scale((s, s, s)), fbx.MIRROR)
    conv = fbx.ident() if fbx.bakes_axis(meta) else scene.axis_conversion()
    return fbx.mul(fbx.mul(fbx.mul(sm, conv), model.world()), fbx.inverse(sm))


def _remaps(folder, meta):
    """FBX material name -> material asset path, from the import's external object remaps."""
    out = {}
    for m in re.finditer(r'- first:\s*\n\s*type: UnityEngine:Material\s*\n\s*assembly: [^\n]*\n\s*name: ([^\n]+)\n\s*second: (\{[^}]*\})', meta):
        r = REF.search(m.group(2))
        if r and r.group(2):
            out[m.group(1).strip()] = folder.path(r.group(2))
    return out


def mesh_for(folder, part):
    """The part's mesh asset in Unity space (cached on the model)."""
    path, scene, model = part.source
    if getattr(model, '_asset', None) is None:
        model._asset = fbx.mesh_asset(scene, model, folder.meta(path))
    return model._asset
