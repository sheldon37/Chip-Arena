"""The effects bank's flipbooks and the blood spray's sheets (Claude X1, 2026-11-25; docs/PACK-EFFECTS.md "Explosions, fire and blood
spray from the flipbooks"). Local tool: it reads the licensed packs' source sheets and writes git-ignored banks; nothing here is
pack content.

usage: python -I tools/fx_bank/flipbooks.py SRC OUT
  SRC  a folder holding the packs' source sheets at the paths below (the effects audit's lab subset, Assets/LocalAssets/V1FxLab,
       extracted from the fire and blood packs in F:/Chip Arena Development/AssetStoreCache)
  OUT  writes OUT/ChipArenaFx/Resources/ChipArenaBlastFx/*.png (the effects bank) and OUT/ChipArenaLocal/Resources/ChipArenaBloodSpray/*.png
       (the licensed-assets zip), each with a Unity .meta (GUIDs kept when a meta exists), and OUT/bytes.json.
The 8 x 8 flipbooks are written at 1024 (128 px frames) as grey + alpha (the sources are greyscale renders), resized premultiplied;
the splatters as one grey 2 x 2 atlas at 1024, the mist at 512. Deterministic for the same sources (PNG, compress level 9).
"""
import uuid
#        OUT/ChipArenaLocal/Resources/ChipArenaBloodSpray/*.png (the Drive zip), then prints the bytes.
import sys, os, json
from PIL import Image
import numpy as np

src, out = sys.argv[1], sys.argv[2]
fx = os.path.join(out, 'ChipArenaFx', 'Resources', 'ChipArenaBlastFx')
blood = os.path.join(out, 'ChipArenaLocal', 'Resources', 'ChipArenaBloodSpray')
os.makedirs(fx, exist_ok=True); os.makedirs(blood, exist_ok=True)

def load(rel):
    im = Image.open(os.path.join(src, rel)); im.load(); return im

def grey_alpha(im, size, grey='max'):
    """A greyscale RGBA render as grey + alpha at size x size, resized premultiplied (no dark fringes)."""
    a = np.asarray(im.convert('RGBA'), dtype=np.float32) / 255.0
    g = a[..., :3].max(axis=2) if grey == 'max' else (a[..., 0] * .2126 + a[..., 1] * .7152 + a[..., 2] * .0722)
    al = a[..., 3]
    pm = Image.fromarray((g * al * 65535).astype(np.uint16).astype(np.int32), 'I').resize((size, size), Image.LANCZOS)
    am = Image.fromarray((al * 65535).astype(np.uint16).astype(np.int32), 'I').resize((size, size), Image.LANCZOS)
    pg = np.clip(np.asarray(pm, dtype=np.float64) / 65535.0, 0, 1); pa = np.clip(np.asarray(am, dtype=np.float64) / 65535.0, 0, 1)
    gg = np.where(pa > 1e-4, np.clip(pg / np.maximum(pa, 1e-4), 0, 1), 0)
    la = np.stack([(gg * 255 + .5).astype(np.uint8), (pa * 255 + .5).astype(np.uint8)], axis=2)
    return Image.fromarray(la, 'LA')

def grey_only(im, size):
    a = np.asarray(im.convert('RGBA'), dtype=np.float32)
    g = a[..., :3].max(axis=2) if im.mode != 'L' else np.asarray(im, dtype=np.float32)
    return Image.fromarray(g.astype(np.uint8), 'L').resize((size, size), Image.LANCZOS)

def save(im, path):
    im.save(path, optimize=True, compress_level=9); return os.path.getsize(path)

rows = []
F = 'FireVFX/Textures/'
sheets = [
    ('burst', F + 'Fire/FireBurst/T_ExplosionToFireBurst_01_render_all_8x8.PNG'),
    ('smoke', F + 'Smoke/Aftermath/T_Aftermath_Smoke_01_render_all_8x8.PNG'),
    ('dust', 'BloodVFX/Textures/T_SmokeBurst_01.PNG'),
    ('flame', F + 'Fire/Campfire/T_Campfire_01_render_all_8x8.PNG'),
    ('large', F + 'Fire/LargeScaleFire/T_LargeScaleFire_01_render_all_8x8.PNG'),
    ('flamesmoke', F + 'Smoke/Campfire/T_Campfire_Smoke_02_render_all_8x8.PNG'),
]
for name, rel in sheets:
    rows.append(('fx', name, 1024, save(grey_alpha(load(rel), 1024, 'lum' if name == 'dust' else 'max'), os.path.join(fx, name + '.png'))))

# Blood: the three splatter sprites in a 2 x 2 atlas at 1024 (512 each; the fourth tile is Splatter_03), grey only (the shape),
# and the mist (the smoke-burst flipbook, 8 x 8) at 512, grey + alpha.
B = 'BloodVFX/Textures/'
tiles = ['Splatters/T_BloodSplatter_01.PNG', 'Splatters/T_BloodSplatter_02.PNG', 'Splatters/T_BloodSplatter_Directional_01.PNG', 'Splatters/T_BloodSplatter_03.PNG']
atlas = Image.new('L', (1024, 1024), 0)
for i, rel in enumerate(tiles):
    atlas.paste(grey_only(load(B + rel), 512), ((i % 2) * 512, (i // 2) * 512))
rows.append(('blood', 'splatters', 1024, save(atlas, os.path.join(blood, 'splatters.png'))))
rows.append(('blood', 'mist', 512, save(grey_alpha(load(B + 'T_SmokeBurst_01.PNG'), 512, 'lum'), os.path.join(blood, 'mist.png'))))

tot = {}
for bank, name, size, n in rows:
    tot[bank] = tot.get(bank, 0) + n; print('%-6s %-11s %5d px %9d bytes' % (bank, name, size, n))
for k, v in tot.items(): print('%s total %d bytes (%.3f MB, %.3f MiB)' % (k, v, v / 1e6, v / 1048576))
json.dump({'rows': rows, 'totals': tot}, open(os.path.join(out, 'bytes.json'), 'w'), indent=1)

# ---- the .meta files ----
TEX = '''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''
FOLDER = '''fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
'''
def write(path, text):
    if os.path.exists(path): print('kept', path); return
    open(path, 'w', encoding='utf-8', newline='\n').write(text.format(guid=uuid.uuid4().hex)); print('wrote', path)
for bank, folder in (('ChipArenaFx', 'ChipArenaBlastFx'), ('ChipArenaLocal', 'ChipArenaBloodSpray')):
    d = os.path.join(out, bank, 'Resources', folder)
    write(d + '.meta', FOLDER)
    for f in sorted(os.listdir(d)):
        if f.endswith('.png'): write(os.path.join(d, f + '.meta'), TEX)
