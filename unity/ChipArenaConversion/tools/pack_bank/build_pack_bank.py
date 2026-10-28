"""Build the pack banks (docs/PACK-BANK.md) from the owner's licensed packs, for the licensed-assets zip only.

usage: python3 -I tools/pack_bank/build_pack_bank.py --packs DIR --out LOCALASSETS [--zip OUT.zip] SELECTION.json...

DIR holds the owner's extracted Asset Store packs, one folder per pack (unpacked .unitypackage content with each asset's
.meta). Each SELECTION file (tools/pack_bank/selections/*.json, committed: names only, no pack content) lists the props,
meshes and textures one bank takes. The tool reads the packs as data (FBX, prefab and material YAML, PSD/TGA/PNG) and writes,
under LOCALASSETS (the git-ignored Assets/LocalAssets of a checkout):

- ChipArenaLocal/Resources/<bank>.bytes: the bank (Core/NativePackBank.cs), meshes already in Unity space;
- ChipArenaLocal/Resources/PackBank/<texture>.png: each texture, at most its size limit, with import settings in its .meta.

tools/pack_visual_body.py packs both (the .bytes beside the body prefab and the PNGs under that Resources folder) into the
licensed-assets zip; --zip also writes them alone, at the same paths, to copy over an Assets/LocalAssets. Nothing it writes is
ever committed or published as files. The output is deterministic: the same packs and selections give the same bytes.
"""
import argparse
import hashlib
import json
import os
import re
import sys
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bank as bankfmt  # noqa: E402
import fbx  # noqa: E402
import images  # noqa: E402
import unity_assets as ua  # noqa: E402

RESOURCES = 'ChipArenaLocal/Resources'
TEXTURES = RESOURCES + '/PackBank'
ID = re.compile(r'^[a-z0-9][a-z0-9-]{0,79}$')
STAMP = (2026, 1, 1, 0, 0, 0)


class SelectionError(Exception):
    pass


def guid_for(path):
    return hashlib.md5(('chip-arena-pack-bank:' + path).encode('utf-8')).hexdigest()


def folder_meta(path):
    return ('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n'
            '  assetBundleName: \n  assetBundleVariant: \n') % guid_for(path)


def bytes_meta(path):
    return ('fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n'
            '  assetBundleVariant: \n') % guid_for(path)


def texture_meta(path, size, srgb=True, mips=True, alpha=False, normal=False, clamp=False, point=False):
    return """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: {mips}
    sRGBTexture: {srgb}
  isReadable: 0
  streamingMipmaps: 0
  maxTextureSize: {size}
  textureSettings:
    serializedVersion: 2
    filterMode: {filt}
    aniso: 1
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nPOTScale: 1
  textureFormat: 1
  textureType: {kind}
  textureShape: 1
  alphaSource: 1
  alphaIsTransparency: {alpha}
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {size}
    textureFormat: -1
    textureCompression: 1
    overridden: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""".format(guid=guid_for(path), mips=int(mips), srgb=int(srgb and not normal), size=size, filt=0 if point else 1,
           wrap=1 if clamp else 0, kind=1 if normal else 0, alpha=int(alpha))


def source_texture_settings(meta):
    """(mips, srgb, normal, clamp, point) from a pack texture's own .meta."""
    def num(key, default):
        m = re.search(r'^\s*%s: (-?\d+)\s*$' % key, meta, re.M)
        return int(m.group(1)) if m else default
    return (num('enableMipMap', 1) == 1, num('sRGBTexture', 1) == 1, num('textureType', 0) == 1,
            num('wrapU', 0) == 1, num('filterMode', 1) == 0)


class Builder:
    def __init__(self, packs):
        self.packs = packs
        self.folders = {}
        self.files = {}  # LocalAssets-relative path -> bytes
        self.provenance = []

    def folder(self, root):
        if root not in self.folders:
            path = os.path.join(self.packs, root)
            if not os.path.isdir(path):
                raise SelectionError('pack folder missing: ' + root)
            self.folders[root] = ua.Folder(path)
        return self.folders[root]

    def emit_texture(self, tex_id, path, limit, alpha=None, crop=None, settings=None):
        rel = '%s/%s.png' % (TEXTURES, tex_id)
        if rel in self.files:
            return
        img = images.decode(path)
        if crop:
            img = images.crop(img, *crop)
        img = images.fit(img, limit)
        has_alpha = img.has_alpha() if alpha is None else alpha
        self.files[rel] = images.encode_png(img, alpha=has_alpha)
        try:
            with open(path + '.meta', 'r', encoding='utf-8', errors='replace') as h:
                src = source_texture_settings(h.read())
        except OSError:
            src = (True, True, False, False, False)
        mips, srgb, normal, clamp, point = settings or src
        self.files[rel + '.meta'] = texture_meta(rel, max(32, min(limit, max(img.w, img.h))), srgb, mips, has_alpha, normal, clamp, point).encode()
        self.provenance.append({'file': rel, 'from': os.path.relpath(path, self.packs)})

    def build(self, sel):
        if sel.get('schema') != 1:
            raise SelectionError('selection schema')
        name = sel.get('bank')
        if name is None and sel.get('props'):
            raise SelectionError('props need a bank')
        if name is not None and not re.match(r'^ChipArenaPack[A-Za-z]+$', name):
            raise SelectionError('bank name ' + name)
        sources = sel.get('sources', {})
        limit = int(sel.get('texture_max', 1024))
        out = bankfmt.Bank()
        tex_ids = {}

        def tex_of(path, source):
            if path is None:
                return -1
            if path not in tex_ids:
                stem = re.sub(r'[^a-z0-9]+', '-', os.path.splitext(os.path.basename(path))[0].lower()).strip('-')
                tex_id = (source + '-' + stem)[:80]
                self.emit_texture(tex_id, path, limit)
                tex_ids[path] = out.texture(tex_id, bankfmt.TEX_ALPHA if self.files['%s/%s.png' % (TEXTURES, tex_id)][25] == 6 else 0)
            return tex_ids[path]

        mat_cache = {}

        def mat_of(folder, mat_path, source):
            if mat_path is None:
                return out.material('none')
            if mat_path in mat_cache:
                return mat_cache[mat_path]
            guid = next((g for g, p in folder.by_guid.items() if p == mat_path), None)
            info = ua.material(folder, guid) if guid else None
            if info is None:
                idx = out.material('none')
            else:
                flags = (bankfmt.MAT_CUTOUT if info['cutout'] else 0) | (bankfmt.MAT_TRANSPARENT if info['transparent'] else 0)
                if 'glass' in info['name'].lower():
                    flags |= bankfmt.MAT_GLASS
                idx = out.material(source + '/' + info['name'], tex_of(info['albedo'], source), tex_of(info['emission'], source),
                                   info['color'], info['emission_color'][:3], flags)
            mat_cache[mat_path] = idx
            return idx

        for t in sel.get('textures', []):
            if not ID.match(t['id']):
                raise SelectionError('texture id ' + t['id'])
            folder_root = sources[t['source']]
            path = os.path.join(self.packs, folder_root, t['path'])
            if not os.path.isfile(path):
                raise SelectionError('texture missing: ' + t['path'])
            settings = None
            if 'mips' in t or 'linear' in t or 'clamp' in t:
                settings = (t.get('mips', True), not t.get('linear', False), t.get('normal', False), t.get('clamp', False), t.get('point', False))
            self.emit_texture(t['id'], path, int(t.get('max', limit)), t.get('alpha'), t.get('crop'), settings)
            out.texture(t['id'], (bankfmt.TEX_ALPHA if t.get('alpha') else 0) | (bankfmt.TEX_LINEAR if t.get('linear') else 0))

        for p in sel.get('props', []):
            if not ID.match(p['id']):
                raise SelectionError('prop id ' + p['id'])
            folder = self.folder(sources[p['source']])
            prefab = os.path.join(folder.root, p['prefab'])
            if not os.path.isfile(prefab):
                raise SelectionError('prefab missing: ' + p['prefab'])
            parts = []
            for part in ua.flatten(folder, prefab):
                mesh = ua.mesh_for(folder, part)
                if mesh is None or not mesh.submeshes:
                    continue
                mats = [mat_of(folder, part.materials[i] if i < len(part.materials) else (part.materials[-1] if part.materials else None), p['source'])
                        for i in range(len(mesh.submeshes))]
                path, scene, model = part.source
                mesh_name = '%s/%s/%s' % (p['source'], os.path.splitext(os.path.basename(path))[0], model.name)
                mi = out.mesh(mesh_name, mesh, mats)
                parts.append((mi, bankfmt.affine(part.matrix)))
            if not parts:
                raise SelectionError('prop has no meshes: ' + p['prefab'])
            out.prop(p['id'], p.get('group', ''), parts)
            self.provenance.append({'prop': p['id'], 'from': os.path.relpath(prefab, self.packs)})

        if name is not None:  # a textures-only selection (effect sheets, loaded by id) writes no bank
            rel = '%s/%s.bytes' % (RESOURCES, name)
            if rel in self.files:
                raise SelectionError('two selections write ' + name)
            self.files[rel] = out.write()
            self.files[rel + '.meta'] = bytes_meta(rel).encode()
        return out

    def finish(self):
        for rel in list(self.files):
            parts = rel.split('/')
            for i in range(1, len(parts)):
                folder = '/'.join(parts[:i])
                self.files.setdefault(folder + '.meta', folder_meta(folder).encode())


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('--packs', required=True)
    ap.add_argument('--out')
    ap.add_argument('--zip')
    ap.add_argument('selections', nargs='+')
    args = ap.parse_args(argv)
    builder = Builder(args.packs)
    for path in args.selections:
        with open(path, 'r', encoding='utf-8') as h:
            sel = json.load(h)
        b = builder.build(sel)
        print('%s: %d props, %d meshes, %d materials, %d textures' % (sel.get('bank') or os.path.basename(path), len(b.props), len(b.meshes), len(b.materials), len(b.textures)))
    builder.finish()
    total = sum(len(v) for v in builder.files.values())
    if args.out:
        for rel, data in sorted(builder.files.items()):
            dest = os.path.join(args.out, rel)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, 'wb') as h:
                h.write(data)
    if args.zip:
        with zipfile.ZipFile(args.zip, 'w') as z:
            for rel, data in sorted(builder.files.items()):
                info = zipfile.ZipInfo(rel, STAMP)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                z.writestr(info, data)
            info = zipfile.ZipInfo('pack-bank-sources.json', STAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, json.dumps(builder.provenance, indent=1, sort_keys=True) + '\n')
    print('%d files, %.2f MB' % (len(builder.files), total / 1e6))
    return 0


if __name__ == '__main__':
    sys.exit(main())
