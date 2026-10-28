"""The pack bank tools (tools/pack_bank, docs/PACK-BANK.md) on a synthetic pack: no licensed content is read or written.

The synthetic pack is a binary FBX, a prefab, a material, a texture and their .meta files, written here. Its bank must equal
Tests/pack-bank-fixture.bytes, which Tests/PackBankChecks.cs reads, so the Python writer and the C# reader agree."""
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import zlib

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools' / 'pack_bank'))
import bank as bankfmt  # noqa: E402
import build_pack_bank  # noqa: E402
import fbx  # noqa: E402
import images  # noqa: E402
import unity_assets  # noqa: E402

FIXTURE = ROOT / 'Tests' / 'pack-bank-fixture.bytes'


# A minimal binary FBX 7.4 writer (32-bit offsets), enough for the reader's paths.
def _prop(v):
    if isinstance(v, bool):
        return b'C' + struct.pack('<?', v)
    if isinstance(v, int):
        return (b'I' + struct.pack('<i', v)) if -2 ** 31 <= v < 2 ** 31 else (b'L' + struct.pack('<q', v))
    if isinstance(v, float):
        return b'D' + struct.pack('<d', v)
    if isinstance(v, bytes):
        return b'S' + struct.pack('<I', len(v)) + v
    if isinstance(v, str):
        return _prop(v.encode())
    if isinstance(v, tuple) and v[0] in ('d', 'i'):
        kind, vals = v
        raw = struct.pack('<%d%s' % (len(vals), 'd' if kind == 'd' else 'i'), *vals)
        comp = zlib.compress(raw)
        return kind.encode() + struct.pack('<III', len(vals), 1, len(comp)) + comp
    raise TypeError(v)


def _node(name, props=(), children=(), at=0):
    body = b''.join(_prop(p) for p in props)
    head_len = 13 + len(name)
    kids, pos = b'', at + head_len + len(body)
    for c in children:
        blob = _node(*c, at=pos)
        kids += blob
        pos += len(blob)
    if children:
        kids += b'\x00' * 13
    end = at + head_len + len(body) + len(kids)
    return struct.pack('<III', end, len(props), len(body)) + bytes([len(name)]) + name.encode() + body + kids


def _p70(*entries):
    return ('Properties70', (), [('P', e) for e in entries])


def synthetic_fbx(unit=100.0):
    """A unit box's +Z face and +Y face (two quads), one material, normals by polygon vertex, one UV set; the model is
    turned 90 degrees about Y (which a mesh asset ignores) and has a geometric offset of +1 on X (which it keeps)."""
    verts = [0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1,   0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0]
    polys = [0, 1, 2, ~3, 4, 5, 6, ~7]
    normals = [0, 0, 1] * 4 + [0, 1, 0] * 4
    uvs = [0, 0, 1, 0, 1, 1, 0, 1] * 2
    top = [
        ('FBXHeaderExtension', (), [('FBXHeaderVersion', (1003,))]),
        ('GlobalSettings', (), [_p70(('UpAxis', 'int', 'Integer', '', 1), ('UpAxisSign', 'int', 'Integer', '', 1),
                                     ('FrontAxis', 'int', 'Integer', '', 2), ('FrontAxisSign', 'int', 'Integer', '', 1),
                                     ('CoordAxis', 'int', 'Integer', '', 0), ('CoordAxisSign', 'int', 'Integer', '', 1),
                                     ('UnitScaleFactor', 'double', 'Number', '', unit))]),
        ('Objects', (), [
            ('Geometry', (1001, b'Box\x00\x01Geometry', 'Mesh'), [
                ('Vertices', (('d', [float(v) for v in verts]),)),
                ('PolygonVertexIndex', (('i', polys),)),
                ('LayerElementNormal', (0,), [('MappingInformationType', ('ByPolygonVertex',)), ('ReferenceInformationType', ('Direct',)),
                                               ('Normals', (('d', [float(v) for v in normals]),))]),
                ('LayerElementUV', (0,), [('MappingInformationType', ('ByPolygonVertex',)), ('ReferenceInformationType', ('Direct',)),
                                           ('UV', (('d', [float(v) for v in uvs]),))]),
                ('LayerElementMaterial', (0,), [('MappingInformationType', ('AllSame',)), ('Materials', (('i', [0]),))]),
            ]),
            ('Model', (2001, b'Box\x00\x01Model', 'Mesh'), [_p70(('Lcl Rotation', 'Lcl Rotation', '', 'A', 0.0, 90.0, 0.0),
                                                                ('GeometricTranslation', 'Vector3D', 'Vector', '', 1.0, 0.0, 0.0))]),
            ('Material', (3001, b'Paint\x00\x01Material', ''), []),
        ]),
        ('Connections', (), [('C', ('OO', 1001, 2001)), ('C', ('OO', 2001, 0)), ('C', ('OO', 3001, 2001))]),
    ]
    out, pos = b'Kaydara FBX Binary  \x00\x1a\x00' + struct.pack('<I', 7400), 27
    for t in top:
        blob = _node(*t, at=pos)
        out += blob
        pos += len(blob)
    return out + b'\x00' * 13 + b'\x00' * 160


def guid_meta(guid, extra=''):
    return 'fileFormatVersion: 2\nguid: %s\n%s' % (guid, extra)


def write_pack(root):
    """The synthetic pack folder: Assets/Kit/{Box.fbx, Paint.mat, Paint.png, Box.prefab} with .meta files."""
    kit = Path(root) / 'Synthetic' / 'Assets' / 'Kit'
    kit.mkdir(parents=True)
    (kit / 'Box.fbx').write_bytes(synthetic_fbx())
    (kit / 'Box.fbx.meta').write_text(guid_meta('a' * 32, 'ModelImporter:\n  fileIDToRecycleName:\n    100000: //RootNode\n    4300000: Box\n  meshes:\n    globalScale: 1\n    useFileScale: 1\n'))
    img = images.Image(2, 2, bytes([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]))
    (kit / 'Paint.png').write_bytes(images.encode_png(img, alpha=False))
    (kit / 'Paint.png.meta').write_text(guid_meta('b' * 32, 'TextureImporter:\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: 1\n'))
    (kit / 'Paint.mat').write_text('%YAML 1.1\n--- !u!21 &2100000\nMaterial:\n  m_Name: Paint\n  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}\n'
                                   '  m_ShaderKeywords: \n  m_SavedProperties:\n    m_TexEnvs:\n    - _MainTex:\n        m_Texture: {fileID: 2800000, guid: ' + 'b' * 32 + ', type: 3}\n'
                                   '    m_Colors:\n    - _Color: {r: 1, g: 0.5, b: 0.25, a: 1}\n')
    (kit / 'Paint.mat.meta').write_text(guid_meta('c' * 32, 'NativeFormatImporter:\n'))
    (kit / 'Box.prefab').write_text('%YAML 1.1\n'
                                    '--- !u!1 &10\nGameObject:\n  m_Component:\n  - component: {fileID: 11}\n  - component: {fileID: 12}\n  - component: {fileID: 13}\n  m_Name: Box\n  m_IsActive: 1\n'
                                    '--- !u!4 &11\nTransform:\n  m_GameObject: {fileID: 10}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Children:\n  - {fileID: 21}\n  m_Father: {fileID: 0}\n'
                                    '--- !u!33 &12\nMeshFilter:\n  m_GameObject: {fileID: 10}\n  m_Mesh: {fileID: 4300000, guid: ' + 'a' * 32 + ', type: 3}\n'
                                    '--- !u!23 &13\nMeshRenderer:\n  m_GameObject: {fileID: 10}\n  m_Enabled: 1\n  m_Materials:\n  - {fileID: 2100000, guid: ' + 'c' * 32 + ', type: 2}\n'
                                    '--- !u!1 &20\nGameObject:\n  m_Component:\n  - component: {fileID: 21}\n  - component: {fileID: 22}\n  - component: {fileID: 23}\n  m_Name: Lid\n  m_IsActive: 1\n'
                                    '--- !u!4 &21\nTransform:\n  m_GameObject: {fileID: 20}\n  m_LocalRotation: {x: 0, y: 0.7071068, z: 0, w: 0.7071068}\n  m_LocalPosition: {x: 0, y: 2, z: 0}\n  m_LocalScale: {x: 2, y: 2, z: 2}\n  m_Children: []\n  m_Father: {fileID: 11}\n'
                                    '--- !u!33 &22\nMeshFilter:\n  m_GameObject: {fileID: 20}\n  m_Mesh: {fileID: 4300000, guid: ' + 'a' * 32 + ', type: 3}\n'
                                    '--- !u!23 &23\nMeshRenderer:\n  m_GameObject: {fileID: 20}\n  m_Enabled: 1\n  m_Materials:\n  - {fileID: 2100000, guid: ' + 'c' * 32 + ', type: 2}\n')
    (kit / 'Box.prefab.meta').write_text(guid_meta('d' * 32, 'PrefabImporter:\n'))
    return Path(root)


SELECTION = {'schema': 1, 'bank': 'ChipArenaPackTest', 'texture_max': 1024, 'sources': {'syn': 'Synthetic/Assets/Kit'},
             'props': [{'id': 'syn-box', 'source': 'syn', 'prefab': 'Box.prefab', 'group': 'box'}]}


def build_synthetic(tmp):
    root = write_pack(tmp)
    b = build_pack_bank.Builder(str(root))
    b.build(SELECTION)
    b.finish()
    return b


class Images(unittest.TestCase):
    def test_png_round_trip(self):
        img = images.Image(3, 2, bytes(range(24)))
        back = images.decode_png(images.encode_png(img))
        self.assertEqual((back.w, back.h, bytes(back.rgba)), (3, 2, bytes(range(24))))
        rgb = images.decode_png(images.encode_png(img, alpha=False))
        self.assertEqual(bytes(rgb.rgba[3::4]), b'\xff' * 6)

    def test_tga_rle_bottom_up(self):
        # 2x2, 24 bit, RLE, bottom-up rows: a run of two blue pixels then two raw ones.
        head = bytes([0, 0, 10]) + b'\x00' * 9 + struct.pack('<HHBB', 2, 2, 24, 0)
        body = bytes([0x81, 255, 0, 0, 0x01, 0, 255, 0, 0, 0, 255])
        img = images.decode_tga(head + body)
        self.assertEqual(bytes(img.rgba[:8]), bytes([0, 255, 0, 255, 255, 0, 0, 255]))  # top row = the last file row
        self.assertEqual(bytes(img.rgba[8:]), bytes([0, 0, 255, 255, 0, 0, 255, 255]))

    def test_psd_merged_rle(self):
        w, h = 2, 1
        planes = [b'\x10\x20', b'\x30\x40', b'\x50\x60', b'\xff\x80']
        rows = [bytes([1]) + p for p in planes]  # literal runs of 2
        data = (b'8BPS' + struct.pack('>H', 1) + b'\x00' * 6 + struct.pack('>HIIHH', 4, h, w, 8, 3) + struct.pack('>I', 0) * 3
                + struct.pack('>H', 1) + struct.pack('>4H', *[len(r) for r in rows]) + b''.join(rows))
        img = images.decode_psd(data)
        self.assertEqual(bytes(img.rgba), bytes([0x10, 0x30, 0x50, 0xff, 0x20, 0x40, 0x60, 0x80]))

    def test_half_box_filter(self):
        img = images.Image(2, 2, bytes([0, 0, 0, 0, 100, 100, 100, 100, 200, 200, 200, 200, 100, 100, 100, 100]))
        self.assertEqual(bytes(images.half(img).rgba), bytes([100, 100, 100, 100]))


class Fbx(unittest.TestCase):
    def test_binary_mesh_asset(self):
        scene = fbx.Scene(fbx.parse(synthetic_fbx(unit=1.0)))
        model = next(m for m in scene.models.values() if m.geometry is not None)
        mesh = fbx.mesh_asset(scene, model, 'globalScale: 1\nuseFileScale: 1\n')
        self.assertEqual(sum(len(s) for s in mesh.submeshes), 12)  # two quads, two triangles each
        xs = sorted({round(p[0], 6) for p in mesh.positions})
        # Centimetres to metres, the geometric offset kept, X mirrored, the node's own rotation not in the asset.
        self.assertEqual(xs, [-0.02, -0.01])
        self.assertTrue(all(abs(n[0]) < 1e-6 for n in mesh.normals))
        self.assertEqual(model.materials, ['Paint'])

    def test_axis_conversion_z_up(self):
        scene = fbx.Scene(fbx.parse(synthetic_fbx()))
        scene.up, scene.front, scene.coord = (2, 1), (1, -1), (0, 1)
        m = scene.axis_conversion()
        self.assertEqual(tuple(round(c, 6) for c in fbx.point(m, (0, 0, 1))), (0, 1, 0))
        self.assertEqual(tuple(round(c, 6) for c in fbx.point(m, (0, 1, 0))), (0, 0, -1))


class Prefabs(unittest.TestCase):
    def test_flatten_and_material(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = write_pack(tmp)
            folder = unity_assets.Folder(str(root / 'Synthetic' / 'Assets' / 'Kit'))
            parts = unity_assets.flatten(folder, str(root / 'Synthetic' / 'Assets' / 'Kit' / 'Box.prefab'))
            self.assertEqual(sorted(p.name for p in parts), ['Box', 'Lid'])
            lid = next(p for p in parts if p.name == 'Lid')
            self.assertEqual([round(v, 4) for v in fbx.point(lid.matrix, (1, 0, 0))], [0, 2, -2])  # turned, raised, doubled
            info = unity_assets.material(folder, 'c' * 32)
            self.assertTrue(info['albedo'].endswith('Paint.png'))
            self.assertEqual(info['color'], (1.0, 0.5, 0.25, 1.0))


class Bank(unittest.TestCase):
    def test_synthetic_bank_matches_fixture(self):
        with tempfile.TemporaryDirectory() as tmp:
            b = build_synthetic(tmp)
            data = b.files['ChipArenaLocal/Resources/ChipArenaPackTest.bytes']
            self.assertEqual(data[:4], b'CAPB')
            self.assertEqual(data, FIXTURE.read_bytes(), 'regenerate with: python3 Tests/test_pack_bank.py --write-fixture')
            self.assertIn('ChipArenaLocal/Resources/PackBank/syn-paint.png', b.files)
            self.assertIn('ChipArenaLocal/Resources/PackBank/syn-paint.png.meta', b.files)
            self.assertIn('ChipArenaLocal/Resources/PackBank.meta', b.files)
            self.assertIn('ChipArenaLocal.meta', b.files)
            again = build_synthetic(tempfile.mkdtemp(dir=tmp))
            self.assertEqual(again.files, b.files)  # deterministic

    def test_selection_rules(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = write_pack(tmp)
            b = build_pack_bank.Builder(str(root))
            with self.assertRaises(build_pack_bank.SelectionError):
                b.build(dict(SELECTION, bank='Other'))
            with self.assertRaises(build_pack_bank.SelectionError):
                b.build(dict(SELECTION, props=[{'id': 'Bad Id', 'source': 'syn', 'prefab': 'Box.prefab'}]))

    def test_no_pack_content_committed(self):
        # The selections name pack files; nothing under tools/pack_bank may be pack content.
        for path in (ROOT / 'tools' / 'pack_bank').rglob('*'):
            if path.is_file() and '__pycache__' not in path.parts:
                self.assertIn(path.suffix, {'.py', '.json', '.md'}, path)


if __name__ == '__main__':
    if '--write-fixture' in sys.argv:
        with tempfile.TemporaryDirectory() as tmp:
            FIXTURE.write_bytes(build_synthetic(tmp).files['ChipArenaLocal/Resources/ChipArenaPackTest.bytes'])
        print('wrote', FIXTURE)
    else:
        unittest.main()
