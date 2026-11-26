"""The realistic magic schools' flipbook atlases (Claude MG1, 2026-11-25; docs/PACK-EFFECTS.md "Realistic magic from the flipbooks").
Local tool: it reads the licensed effects pack's source sheets and writes a git-ignored bank; nothing here is pack content.

usage: python -I tools/fx_bank/magic.py SRC OUT
  SRC  the effects pack's root folder (the one holding Effects/Textures: kripto289's Realistic Effects Pack 4, extracted from
       F:/Chip Arena Development/AssetStoreCache, or its lab import in the Editor)
  OUT  writes OUT/ChipArenaFx/Resources/ChipArenaMagicFx/magic0.png and magic1.png (the effects bank), each with a Unity .meta (GUIDs
       kept when a meta exists), and OUT/magic-bytes.json.
Each source flipbook is resized whole into its quadrant (1024 x 1024; its own grid kept, so frames may be non-square) as grey + alpha:
the flame's grey is its density (its heat), the smoke's and spray's a soft shading from their density, and the additive art (plasma,
arcs, energy shell) stores its light as both grey and alpha; resized premultiplied (no dark fringes). magic0 and magic1 are 2048 x 2048
(magic1's last quadrant empty). The layout is Core NativeMagicFxPack.Sheets (pinned by CoreTests --fx-magic).
Deterministic for the same sources (PNG, compress level 9).
"""
import sys, os, json, uuid
from PIL import Image
import numpy as np

src, out = sys.argv[1], sys.argv[2]
dst = os.path.join(out, 'ChipArenaFx', 'Resources', 'ChipArenaMagicFx')
os.makedirs(dst, exist_ok=True)
Q = 1024
# (atlas, quadrant x, quadrant y, source, how): the white sheets carry their shape in alpha only ('mask': grey = the density, the
# fire's heat; 'fog': grey a soft shading from the density); the additive sheets have no alpha ('grey': grey = alpha = the light).
LAYOUT = [
    (0, 0, 0, 'Fire2.png', 'mask'),       # 0 flame burst, 8 x 4
    (0, 1, 0, 'Fog5.png', 'fog'),         # 1 smoke wisps, 8 x 8
    (0, 0, 1, 'PlasmaBall1.png', 'grey'), # 2 plasma ball, 16 x 8
    (0, 1, 1, 'Lightning2.png', 'grey'),  # 3 arcs, 4 x 8
    (1, 0, 0, 'EnergyBall.png', 'grey'),  # 4 energy shell, 8 x 8
    (1, 1, 0, 'Fog1.png', 'fog'),         # 5 waves, 8 x 16
    (1, 0, 1, 'Fire4.png', 'mask'),       # 6 flame tongues, 16 x 8
]
SIZES = {0: (2048, 2048), 1: (2048, 2048)}

def grey_alpha(path, alpha):
    im = Image.open(path); im.load()
    a = np.asarray(im.convert('RGBA'), dtype=np.float32) / 255.0
    g = a[..., :3].max(axis=2); al = a[..., 3]
    if alpha == 'mask': g = al.copy()
    elif alpha == 'fog': g = .55 + .45 * np.sqrt(al)
    elif alpha == 'grey': al = g.copy(); g = np.ones_like(g)
    pm = Image.fromarray((g * al * 65535).astype(np.uint16).astype(np.int32), 'I').resize((Q, Q), Image.LANCZOS)
    am = Image.fromarray((al * 65535).astype(np.uint16).astype(np.int32), 'I').resize((Q, Q), Image.LANCZOS)
    pg = np.clip(np.asarray(pm, dtype=np.float64) / 65535.0, 0, 1); pa = np.clip(np.asarray(am, dtype=np.float64) / 65535.0, 0, 1)
    if alpha == 'grey':
        # Additive art: the grey is the light itself; stored as grey = alpha = the resized light.
        return pg, pg
    gg = np.where(pa > 1e-4, np.clip(pg / np.maximum(pa, 1e-4), 0, 1), 0)
    return gg, pa

atlases = {k: (np.zeros((h, w)), np.zeros((h, w))) for k, (w, h) in SIZES.items()}
for atlas, qx, qy, name, alpha in LAYOUT:
    g, a = grey_alpha(os.path.join(src, 'Effects', 'Textures', name), alpha)
    G, A = atlases[atlas]; G[qy * Q:(qy + 1) * Q, qx * Q:(qx + 1) * Q] = g; A[qy * Q:(qy + 1) * Q, qx * Q:(qx + 1) * Q] = a
rows = []
for k, (G, A) in atlases.items():
    la = np.stack([(G * 255 + .5).astype(np.uint8), (A * 255 + .5).astype(np.uint8)], axis=2)
    path = os.path.join(dst, 'magic%d.png' % k); Image.fromarray(la, 'LA').save(path, optimize=True, compress_level=9)
    rows.append(('magic%d' % k, SIZES[k], os.path.getsize(path))); print('magic%d %dx%d %d bytes' % (k, SIZES[k][0], SIZES[k][1], os.path.getsize(path)))

TEX = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'flipbooks.py'), encoding='utf-8').read()
TEX = TEX[TEX.index("TEX = '''") + 9:]; TEX = TEX[:TEX.index("'''")]
TEX = TEX.replace('maxTextureSize: 1024', 'maxTextureSize: 2048')
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
write(dst + '.meta', FOLDER)
for f in sorted(os.listdir(dst)):
    if f.endswith('.png'): write(os.path.join(dst, f + '.meta'), TEX)
total = sum(os.path.getsize(os.path.join(dst, f)) for f in os.listdir(dst)) + os.path.getsize(dst + '.meta')
print('ChipArenaMagicFx total with metas: %d bytes (%.3f MB, %.3f MiB)' % (total, total / 1e6, total / 1048576))
json.dump({'rows': rows, 'total': total}, open(os.path.join(out, 'magic-bytes.json'), 'w'), indent=1)
