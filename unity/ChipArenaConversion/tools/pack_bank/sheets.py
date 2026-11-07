"""Effect sheets composed from pack textures (docs/PACK-BANK.md, docs/PACK-EFFECTS.md). Standard library only; data only;
deterministic: the same packs and selection give the same bytes.

Two kinds, named by a selection's `sheets` entries (texture-only selections; the game loads each by its id):

- `toon`: the shared effects renderer's toon sheet layout (Core/NativeVfxTextures.ToonSheet: 1024 x 512, 8 x 4 tiles of
  128 px, tile t at column t % 8, row t / 8 from the bottom), rebuilt from the packs' cartoon masks, so the billboard shader
  (Resources/NativeVfx.shader) draws the packs' shapes with its own palette, lighting, erosion and low tier unchanged:
  - puff tiles 0-15: R the signed distance to the silhouette (alpha), G the lit distance (the mask's bright tone), B the
    erosion field (interior depth and round cells, as Core's), A the distance to the outline (an outlined mask's darker
    ring, or a thin rim along the silhouette);
  - flash, crescent and spark tiles 16-31: R the shape's signed distance, G its hot core's (R less `core`), B a round halo,
    A 0; a tile may be `{"procedural": "drop"}` (Core's teardrop, no pack content).
  Distances are .5 + d / (2 x .25) in tile units (the tile's half width is 1), as Core's, so a shader draws a crisp edge.
- `atlas`: a grid of pack sprites (colour times alpha, premultiplied; alpha the coverage), each fitted into its tile, for
  the magic bolts and their hits (Core/NativeMagicPackFx: tile numbers are the entries' order).

Each tile names a `source` (a key of the selection's `sources`), a `path` under it, and optionally a flipbook `grid`
[columns, rows] with its `frame` (0 the top left, as Unity's texture sheets), `scale` (the shape's size in its tile),
`rotate` (degrees, counterclockwise), `mirror` (left to right) and `grow` (tile units added to the distances).
"""
import math

import images

TOON_W, TOON_H, TOON_T, TOON_COLS, TOON_ROWS = 1024, 512, 128, 8, 4
SDF_RANGE = .25
INF = 1e20


# ---- planes: one float channel, rows top to bottom (the image's own order) ----------------------------------------------

def _frame(img, grid, frame):
    if not grid:
        return img
    cols, rows = grid
    w, h = img.w // cols, img.h // rows
    return images.crop(img, (frame % cols) * w, (frame // cols) * h, w, h)


def _planes(img):
    """(alpha, luminance) planes, 0..1."""
    px = img.rgba
    n = img.w * img.h
    alpha = [px[i * 4 + 3] / 255.0 for i in range(n)]
    lum = [(px[i * 4] * .2126 + px[i * 4 + 1] * .7152 + px[i * 4 + 2] * .0722) / 255.0 for i in range(n)]
    return alpha, lum


def _dt1(f, n):
    """Felzenszwalb and Huttenlocher's 1D squared distance transform of f (n samples)."""
    d = [0.0] * n
    v = [0] * n
    z = [0.0] * (n + 1)
    k = 0
    z[0], z[1] = -INF, INF
    for q in range(1, n):
        fq = f[q] + q * q
        p = v[k]
        s = (fq - (f[p] + p * p)) / (2 * q - 2 * p)
        while s <= z[k]:
            k -= 1
            p = v[k]
            s = (fq - (f[p] + p * p)) / (2 * q - 2 * p)
        k += 1
        v[k] = q
        z[k] = s
        z[k + 1] = INF
    k = 0
    for q in range(n):
        while z[k + 1] < q:
            k += 1
        p = v[k]
        d[q] = (q - p) * (q - p) + f[p]
    return d


def _edt(seed, w, h):
    """Squared Euclidean distance (px) of every pixel to the nearest seed pixel."""
    g = [0.0 if s else INF for s in seed]
    for x in range(w):
        col = _dt1(g[x::w], h)
        g[x::w] = col
    out = [0.0] * (w * h)
    for y in range(h):
        out[y * w:(y + 1) * w] = _dt1(g[y * w:(y + 1) * w], w)
    return out


def sdf(mask, w, h):
    """Signed distance (px, positive inside) to the edge of a boolean mask, measured between pixel centres less half a pixel."""
    if not any(mask):
        return [-float(w + h)] * (w * h)
    if all(mask):
        return [float(w + h)] * (w * h)
    to_out = _edt([not m for m in mask], w, h)
    to_in = _edt(mask, w, h)
    return [math.sqrt(a) - .5 if m else .5 - math.sqrt(b) for m, a, b in zip(mask, to_out, to_in)]


def _bilinear(plane, w, h, x, y, outside=False):
    """Plane value at pixel coordinates x, y (pixel centres at integers), clamped at the borders. outside: a distance
    plane, less the distance (px) the point lies beyond the border (so a shape never runs on past its image)."""
    over = 0.0
    if x < 0:
        over, x = -x, 0.0
    elif x > w - 1:
        over, x = x - (w - 1), float(w - 1)
    if y < 0:
        over, y = math.hypot(over, y), 0.0
    elif y > h - 1:
        over, y = math.hypot(over, y - (h - 1)), float(h - 1)
    x0, y0 = int(x), int(y)
    x1, y1 = min(x0 + 1, w - 1), min(y0 + 1, h - 1)
    fx, fy = x - x0, y - y0
    a = plane[y0 * w + x0]
    b = plane[y0 * w + x1]
    c = plane[y1 * w + x0]
    d = plane[y1 * w + x1]
    v = (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy
    return v - over if outside else v


def _byte(v):
    return 0 if v <= 0 else 255 if v >= 1 else int(math.floor(v * 255 + .5))


def _sdf_byte(d):
    return _byte(.5 + d / (2 * SDF_RANGE))


# ---- Core's cell noise (NativeVfxTextures.Lattice and Cells), for the erosion field ------------------------------------

M32 = 0xffffffff


def _lattice(x, y, seed):
    h = ((x * 0x8da6b343) ^ (y * 0xd8163841) ^ (seed * 0xcb1ab31f)) & M32
    h ^= h >> 13
    h = (h * 0x5bd1e995) & M32
    h ^= h >> 15
    h = (h * 0x27d4eb2d) & M32
    h ^= h >> 16
    return (h & 0xffffff) / 16777216.0


def _cells(x, y, seed):
    ix, iy = math.floor(x), math.floor(y)
    best = 9.0
    for j in (-1, 0, 1):
        for i in (-1, 0, 1):
            cx, cy = ix + i, iy + j
            px = cx + _lattice(cx, cy, seed)
            py = cy + _lattice(cx, cy, seed ^ 0x9e3779b9)
            dd = (px - x) ** 2 + (py - y) ** 2
            if dd < best:
                best = dd
    return min(1.0, math.sqrt(best) / 1.1)


class _Rng:
    def __init__(self, seed):
        self.s = seed & M32

    def next(self):
        self.s = (self.s * 1664525 + 1013904223) & M32
        return self.s / 4294967296.0


# ---- the toon sheet -------------------------------------------------------------------------------------------------------

class SheetError(Exception):
    pass


def _mapper(tile, n_w, n_h, alpha=None):
    """Tile point (x, y in tile units, y up) to source pixel coordinates, and the source pixels per tile unit. With `reach`
    (and an alpha plane) the shape is centred on its bounds and sized so its farthest covered pixel lies `reach` from the
    tile's centre (times `scale`); otherwise the source's half width spans `scale` of the tile's."""
    scale = float(tile.get('scale', .9 if 'reach' not in tile else 1))
    a = math.radians(float(tile.get('rotate', 0)))
    ca, sa = math.cos(a), math.sin(a)
    mirror = -1.0 if tile.get('mirror') else 1.0
    half = min(n_w, n_h) / 2.0
    cx, cy = n_w / 2.0, n_h / 2.0
    if 'reach' in tile and alpha is not None:
        xs = [i % n_w for i, v in enumerate(alpha) if v >= .5]
        ys = [i // n_w for i, v in enumerate(alpha) if v >= .5]
        if xs:
            cx, cy = (min(xs) + max(xs) + 1) / 2.0, (min(ys) + max(ys) + 1) / 2.0
            far = max(math.sqrt((x + .5 - cx) ** 2 + (y + .5 - cy) ** 2) for x, y in zip(xs, ys))
            half = far / float(tile['reach'])
    per_unit = half / scale

    squash = float(tile.get('squash', 1))

    def to_source(x, y):
        # Undo the tile's squash (across y) and rotation, then its size: source half widths, y up; then pixels, top down.
        y = y / squash
        u = (x * ca + y * sa) * mirror
        v = -x * sa + y * ca
        return cx + u * per_unit - .5, cy - v * per_unit - .5
    return to_source, per_unit


# The puffs' light (tile units, y up): from the upper left, as Core's ToonLightX/Y; `shadow` how far toward it a lit point's
# silhouette is tested (Core's ToonShadow), so each puff's lower right is a hard shadow crescent.
LIGHT_X, LIGHT_Y, SHADOW = -.5, .8660254037844386, .17


def _puff_tile(src, tile, t, data, seed):
    alpha, lum = _planes(src)
    w, h = src.w, src.h
    inside = [a >= .5 for a in alpha]
    d_r = sdf(inside, w, h)
    # The lit tone: the mask's own bright tone (`mask`), the silhouette shifted toward the light (`shift`), or both: the
    # shifted silhouette less the mask's inner creases (its shadow tone deeper than `crease` px inside the silhouette, the
    # folds between its lobes; its even rim band is left to the light).
    light = tile.get('light', 'shift')
    d_g = d_c = None
    if light in ('mask', 'both'):
        level, crease = float(tile.get('lit', .92)), float(tile.get('crease', 0 if light == 'mask' else 16))
        lit = [m and (l >= level or d <= crease) for m, l, d in zip(inside, lum, d_r)]
        d_g = sdf(lit, w, h)
        # The creases also take the outline (`fold`): the darker line between lobes the cartoon draws.
        if tile.get('fold', light == 'both'):
            d_c = sdf([m and not k for m, k in zip(inside, lit)], w, h)
    shadow = float(tile.get('shadow', SHADOW))
    outline = tile.get('outline', 'rim')
    if outline == 'ring':
        ring = float(tile.get('ring', .67))
        d_a = sdf([m and l >= ring for m, l in zip(inside, lum)], w, h)
        bias = .03
    else:
        d_a = d_r
        bias = float(tile.get('rim', 0))
    grow = float(tile.get('grow', 0))
    # The erosion's interior depth (tile units): Core's .24; deeper keeps a big puff's middle longer, so it is eaten into round
    # blobs rather than points.
    deep = float(tile.get('depth', .24))
    to_source, px_per_unit = _mapper(tile, w, h, alpha)
    rng = _Rng(seed + t * 0x9e37)
    cells = (rng.s ^ 0x4f1b) & M32
    jx, jy = rng.next() * 16, rng.next() * 16
    T = TOON_T
    ox, oy = (t % TOON_COLS) * T, (t // TOON_COLS) * T
    for py in range(T):
        y = (py + .5) / T * 2 - 1
        for px in range(T):
            x = (px + .5) / T * 2 - 1
            r = math.sqrt(x * x + y * y)
            sx, sy = to_source(x, y)
            # Distances in tile units; everything outside .9 of the tile is outside (the gutter the quad never shows past).
            dr = min(_bilinear(d_r, w, h, sx, sy, True) / px_per_unit + grow, .9 - r)
            dg = dr
            if light in ('shift', 'both'):
                lx, ly = to_source(x - LIGHT_X * shadow, y - LIGHT_Y * shadow)
                dg = min(dg, _bilinear(d_r, w, h, lx, ly, True) / px_per_unit + grow)
            if d_g is not None:
                dg = min(dg, _bilinear(d_g, w, h, sx, sy, True) / px_per_unit + grow)
            da = _bilinear(d_a, w, h, sx, sy, True) / px_per_unit + grow + bias
            if d_c is not None:
                da = min(da, -_bilinear(d_c, w, h, sx, sy, True) / px_per_unit + .03)
            depth = min(1.0, max(0.0, dr / deep))
            blob = 1 - min(1.0, max(0.0, _cells(x * 2.4 + jx, y * 2.4 + jy, cells) * 1.25))
            b = min(1.0, max(0.0, .08 + .47 * depth + .45 * blob * (.4 + .6 * depth))) if _sdf_byte(dr) >= 128 else 0.0
            o = ((oy + py) * TOON_W + ox + px) * 4
            data[o] = _sdf_byte(dr)
            data[o + 1] = _sdf_byte(dg)
            data[o + 2] = _byte(b)
            data[o + 3] = _sdf_byte(da)


def _drop(x, y):
    """Core's droplet (NativeVfxTextures.Shape, the default case): a round head toward +x, a tail tapering to -.86."""
    head = .4 - math.sqrt((x - .42) ** 2 + y * y)
    if x >= .42:
        return head
    q = min(1.0, max(0.0, (x + .86) / 1.28))
    wid = .4 * q * math.sqrt(q)
    return max(head, min(wid - abs(y), x + .86))


def _shape_tile(src, tile, t, data):
    T = TOON_T
    ox, oy = (t % TOON_COLS) * T, (t // TOON_COLS) * T
    core = float(tile.get('core', .1))
    grow = float(tile.get('grow', 0))
    if src is None:
        field = None
    else:
        alpha, _ = _planes(src)
        w, h = src.w, src.h
        d = sdf([a >= float(tile.get('cut', .5)) for a in alpha], w, h)
        to_source, px_per_unit = _mapper(tile, w, h, alpha)
        field = (d, w, h, to_source, px_per_unit)
    for py in range(T):
        y = (py + .5) / T * 2 - 1
        for px in range(T):
            x = (px + .5) / T * 2 - 1
            r = math.sqrt(x * x + y * y)
            if field is None:
                dist = _drop(x, y)
            else:
                d, w, h, to_source, px_per_unit = field
                sx, sy = to_source(x, y)
                dist = _bilinear(d, w, h, sx, sy, True) / px_per_unit + grow
            dist = min(dist, .92 - r)
            halo = min(1.0, max(0.0, 1 - r / .95))
            o = ((oy + py) * TOON_W + ox + px) * 4
            data[o] = _sdf_byte(dist)
            data[o + 1] = _sdf_byte(dist - core)
            data[o + 2] = _byte(halo * halo)
            data[o + 3] = 0


def toon_sheet(spec, load):
    """The toon sheet image (rows top to bottom, as a PNG) for a `toon` sheet entry; load(tile) gives a tile's source image."""
    puffs, shapes = spec.get('puffs', []), spec.get('shapes', [])
    if len(puffs) != 16 or len(shapes) != 16:
        raise SheetError('a toon sheet needs 16 puffs and 16 shapes (the flash flipbook, 4 crescents, spike, lens, star, drop)')
    data = bytearray(TOON_W * TOON_H * 4)  # rows bottom to top (Core's layout), flipped below
    seed = int(spec.get('seed', 0x70a1))
    for t, tile in enumerate(puffs):
        _puff_tile(_frame(load(tile), tile.get('grid'), int(tile.get('frame', 0))), tile, t, data, seed)
    for k, tile in enumerate(shapes):
        src = None if tile.get('procedural') == 'drop' else _frame(load(tile), tile.get('grid'), int(tile.get('frame', 0)))
        if tile.get('procedural') not in (None, 'drop'):
            raise SheetError('unknown procedural tile ' + tile['procedural'])
        _shape_tile(src, tile, 16 + k, data)
    row = TOON_W * 4
    flipped = bytearray(len(data))
    for y in range(TOON_H):
        flipped[(TOON_H - 1 - y) * row:(TOON_H - y) * row] = data[y * row:(y + 1) * row]
    return images.Image(TOON_W, TOON_H, flipped)


# ---- sprite atlases ---------------------------------------------------------------------------------------------------------

def _premultiplied(img):
    px = img.rgba
    n = img.w * img.h
    out = [0.0] * (n * 4)
    for i in range(n):
        a = px[i * 4 + 3] / 255.0
        out[i * 4] = px[i * 4] / 255.0 * a
        out[i * 4 + 1] = px[i * 4 + 1] / 255.0 * a
        out[i * 4 + 2] = px[i * 4 + 2] / 255.0 * a
        out[i * 4 + 3] = a
    return out


def _half4(p, w, h):
    w2, h2 = max(1, w // 2), max(1, h // 2)
    out = [0.0] * (w2 * h2 * 4)
    for y in range(h2):
        r0, r1 = (2 * y) * w * 4, (2 * y + 1) * w * 4
        for x in range(w2):
            a, b = r0 + 8 * x, r1 + 8 * x
            o = (y * w2 + x) * 4
            for c in range(4):
                out[o + c] = (p[a + c] + p[a + 4 + c] + p[b + c] + p[b + 4 + c]) * .25
    return out, w2, h2


def atlas(spec, load):
    """The atlas image (rows top to bottom) for an `atlas` sheet entry: premultiplied colour, coverage alpha."""
    cols, rows = spec.get('grid', (8, 4))
    T = int(spec.get('tile', 256))
    tiles = spec.get('tiles', [])
    if len(tiles) > cols * rows:
        raise SheetError('atlas: more tiles than its grid holds')
    W, H = cols * T, rows * T
    out = bytearray(W * H * 4)
    for t, tile in enumerate(tiles):
        src = _frame(load(tile), tile.get('grid'), int(tile.get('frame', 0)))
        p, w, h = _premultiplied(src), src.w, src.h
        fit = float(tile.get('scale', 1.0))
        # Halve while the source is at least twice the tile (a box filter on premultiplied colour), then sample bilinearly.
        while w >= 2 * T and h >= 2 * T:
            p, w, h = _half4(p, w, h)
        k = max(w, h) / (T * fit)  # source pixels per tile pixel, fitted (contain) and centred
        a = math.radians(float(tile.get('rotate', 0)))
        ca, sa = math.cos(a), math.sin(a)
        ox, oy = (t % cols) * T, (t // cols) * T
        for py in range(T):
            for px in range(T):
                u, v = px + .5 - T / 2.0, py + .5 - T / 2.0
                u, v = u * ca + v * sa, -u * sa + v * ca
                sx, sy = w / 2.0 + u * k - .5, h / 2.0 + v * k - .5
                o = ((oy + py) * W + ox + px) * 4
                if sx < -.5 or sy < -.5 or sx > w - .5 or sy > h - .5:
                    continue
                x0, y0 = int(math.floor(sx)), int(math.floor(sy))
                fx, fy = sx - x0, sy - y0
                acc = [0.0, 0.0, 0.0, 0.0]
                for (xx, yy, wt) in ((x0, y0, (1 - fx) * (1 - fy)), (x0 + 1, y0, fx * (1 - fy)), (x0, y0 + 1, (1 - fx) * fy), (x0 + 1, y0 + 1, fx * fy)):
                    if 0 <= xx < w and 0 <= yy < h and wt > 0:
                        i = (yy * w + xx) * 4
                        for c in range(4):
                            acc[c] += p[i + c] * wt
                out[o] = _byte(acc[0])
                out[o + 1] = _byte(acc[1])
                out[o + 2] = _byte(acc[2])
                out[o + 3] = _byte(acc[3])
    return images.Image(W, H, out)
