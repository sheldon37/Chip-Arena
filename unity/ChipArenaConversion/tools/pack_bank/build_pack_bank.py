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
import sheets  # noqa: E402
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


def texture_meta(path, size, srgb=True, mips=True, alpha=False, normal=False, clamp=False, point=False, compress=True):
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
    textureCompression: {compress}
    overridden: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""".format(guid=guid_for(path), mips=int(mips), srgb=int(srgb and not normal), size=size, filt=0 if point else 1,
           wrap=1 if clamp else 0, kind=1 if normal else 0, alpha=int(alpha), compress=int(compress))


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

        # Effect sheets composed from pack textures (sheets.py): a toon sheet in the effects renderer's layout (linear data,
        # uncompressed, since its four channels are independent distance fields) or a sprite atlas (colour, compressed).
        decoded = {}

        def load(tile):
            path = os.path.join(self.packs, sources[tile['source']], tile['path'])
            if path not in decoded:
                if not os.path.isfile(path):
                    raise SelectionError('sheet tile missing: ' + tile['path'])
                decoded[path] = images.decode(path)
                self.provenance.append({'sheet-source': os.path.relpath(path, self.packs)})
            return decoded[path]
        for sh in sel.get('sheets', []):
            if not ID.match(sh['id']):
                raise SelectionError('sheet id ' + sh['id'])
            try:
                if sh.get('kind') == 'toon':
                    img = sheets.toon_sheet(sh, load)
                    settings = dict(srgb=False, mips=True, alpha=False, clamp=True, compress=False)
                elif sh.get('kind') == 'atlas':
                    img = sheets.atlas(sh, load)
                    settings = dict(srgb=True, mips=True, alpha=False, clamp=True, compress=True)
                else:
                    raise SelectionError('sheet kind ' + str(sh.get('kind')))
            except sheets.SheetError as e:
                raise SelectionError('sheet %s: %s' % (sh['id'], e))
            rel = '%s/%s.png' % (TEXTURES, sh['id'])
            if rel in self.files:
                raise SelectionError('two sheets write ' + sh['id'])
            self.files[rel] = images.encode_png(img, alpha=True)
            self.files[rel + '.meta'] = texture_meta(rel, max(img.w, img.h), **settings).encode()
            self.provenance.append({'file': rel, 'sheet': sh['kind']})
            out.texture(sh['id'], bankfmt.TEX_ALPHA | (0 if settings['srgb'] else bankfmt.TEX_LINEAR))

        for p in sel.get('props', []):
            if not ID.match(p['id']):
                raise SelectionError('prop id ' + p['id'])
            folder = self.folder(sources[p['source']])
            # A prefab, or a model file (`model`) where the pack's prefabs are binary-serialized; `albedo` (a texture path)
            # then gives every submesh one material drawing that texture as it is.
            prefab = os.path.join(folder.root, p['model'] if 'model' in p else p['prefab'])
            if not os.path.isfile(prefab):
                raise SelectionError(('model' if 'model' in p else 'prefab') + ' missing: ' + os.path.relpath(prefab, folder.root))
            albedo = None
            if 'albedo' in p:
                albedo = os.path.join(folder.root, p['albedo'])
                if not os.path.isfile(albedo):
                    raise SelectionError('albedo missing: ' + p['albedo'])
                stem = re.sub(r'[^a-z0-9]+', '-', os.path.splitext(os.path.basename(albedo))[0].lower()).strip('-')
                albedo = out.material(p['source'] + '/' + stem, tex_of(albedo, p['source']))
            # `materials` (a model's FBX material name -> {albedo, color, emission, flags}) gives each submesh its own material
            # where a model's materials carry no textures (the Backrooms kit: wallpaper, carpet and plaster on one wall).
            named = p.get('materials')
            named_cache = {}

            def named_mat(name):
                if name in named_cache:
                    return named_cache[name]
                spec = named.get(name) if named and name is not None else None
                if spec is None:
                    idx = out.material('none')
                else:
                    tex = None
                    if 'albedo' in spec:
                        tex = os.path.join(folder.root, spec['albedo'])
                        if not os.path.isfile(tex):
                            raise SelectionError('albedo missing: ' + spec['albedo'])
                    color = tuple(float(c) for c in spec.get('color', (1, 1, 1))[:3]) + (1.0,)
                    flags = (bankfmt.MAT_TWO_SIDED if spec.get('two_sided') else 0) | (bankfmt.MAT_CUTOUT if spec.get('cutout') else 0)
                    slug = re.sub(r'[^a-z0-9]+', '-', name.lower()).strip('-')
                    idx = out.material(p['source'] + '/' + slug, tex_of(tex, p['source']), -1, color, tuple(float(c) for c in spec.get('emission', (0, 0, 0))[:3]), flags)
                named_cache[name] = idx
                return idx

            parts = []
            for part in (ua.model_parts(folder, prefab) if 'model' in p else ua.flatten(folder, prefab)):
                mesh = ua.mesh_for(folder, part)
                if mesh is None or not mesh.submeshes:
                    continue
                # `wind_by_normals`: a model whose triangles are wound against their own normals in places (the Backrooms kit: its
                # floors face down by winding and up by normal, its lamps do not); each triangle facing away from its corners'
                # normals is turned, so faces cull as their normals point (Unity's front face: (b-a)x(c-a) toward the viewer).
                if p.get('wind_by_normals') and mesh.normals:
                    pos, nor = mesh.positions, mesh.normals
                    for sub in mesh.submeshes:
                        for t in range(0, len(sub) - 2, 3):
                            a, b, c = sub[t], sub[t + 1], sub[t + 2]
                            ux, uy, uz = pos[b][0] - pos[a][0], pos[b][1] - pos[a][1], pos[b][2] - pos[a][2]
                            vx, vy, vz = pos[c][0] - pos[a][0], pos[c][1] - pos[a][1], pos[c][2] - pos[a][2]
                            fx, fy, fz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
                            nx, ny, nz = nor[a][0] + nor[b][0] + nor[c][0], nor[a][1] + nor[b][1] + nor[c][1], nor[a][2] + nor[b][2] + nor[c][2]
                            if fx * nx + fy * ny + fz * nz < 0:
                                sub[t + 1], sub[t + 2] = c, b
                path, scene, model = part.source
                if named is not None:
                    names = model.materials
                    mats = [named_mat(names[i] if i < len(names) else (names[-1] if names else None)) for i in range(len(mesh.submeshes))]
                else:
                    mats = [albedo if albedo is not None else mat_of(folder, part.materials[i] if i < len(part.materials) else (part.materials[-1] if part.materials else None), p['source'])
                            for i in range(len(mesh.submeshes))]
                mesh_name = '%s/%s/%s' % (p['source'], os.path.splitext(os.path.basename(path))[0], model.name)
                mi = out.mesh(mesh_name, mesh, mats)
                parts.append((mi, bankfmt.affine(part.matrix), model.name))
            if not parts:
                raise SelectionError('prop has no meshes: ' + p.get('model', p.get('prefab', '')))
            # `split`: each mesh node its own prop, `<id>-<node>` (a kit's pieces laid out side by side in one model file), at
            # its node's turn and scale about the origin (the translation dropped: callers place pieces by their bounds).
            if p.get('split'):
                for mi, m, node in parts:
                    sub = (p['id'] + '-' + re.sub(r'[^a-z0-9]+', '-', node.lower()).strip('-'))[:80].rstrip('-')
                    if not ID.match(sub):
                        raise SelectionError('prop id ' + sub)
                    m = list(m)
                    m[3] = m[7] = m[11] = 0.0
                    out.prop(sub, p.get('group', ''), [(mi, m)])
            else:
                out.prop(p['id'], p.get('group', ''), [(mi, m) for mi, m, _ in parts])
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
