"""The game's icon set from the owner-supplied logo (docs/ASSET-CREDITS.md; owner 2026-11-11: "This is also the new logo for the
game", "Yes, icon too").

  python -I tools/branding/make_icons.py <logo.png> <out dir> [--sheet sheet.png]

Writes ChipArenaIcon.ico (16, 24, 32, 48, 64, 128, 256), ChipArenaIcon-1024.png (the master) and ChipArenaIcon-<n>.png.
 - 64 px and up: the full logo (chip and lettering) centred on a transparent square.
 - 48 px and below, where the lettering cannot be read: the logo's mark, drawn here from its parts in its colours: the purple
   chip with its gold pins, the orange swoosh round it and a bold cream C. 48 and 24 are drawn large and reduced; 32 and 16
   are laid out on their own pixel grids (whole-pixel pins and gaps, so they do not blur) and only the swoosh and the C are
   antialiased.
Not wired into the player, the launcher, the updater or the Site here (packaging does that)."""
import os
import sys

from PIL import Image, ImageDraw

PURPLE, PURPLE_HI, PURPLE_LO, EDGE = (124, 58, 196), (158, 96, 230), (78, 30, 138), (38, 14, 70)
GOLD, GOLD_HI, GOLD_LO = (246, 178, 34), (255, 222, 96), (176, 108, 12)
ORANGE, ORANGE_HI, CREAM, CREAM_LO = (250, 122, 18), (255, 190, 60), (255, 240, 208), (226, 150, 70)


def full(logo, n):
    """The whole logo centred on an n x n transparent square (a little margin)."""
    m = max(1, round(n * .03))
    w, h = logo.size
    s = min((n - 2 * m) / w, (n - 2 * m) / h)
    im = logo.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    out = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    out.alpha_composite(im, ((n - im.width) // 2, (n - im.height) // 2))
    return out


def mark(n, ss=8, pins=4):
    """The simplified mark drawn at ss x n and reduced: chip, pins, swoosh, C."""
    N = n * ss
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    u = N / 100.0

    def P(pts):
        return [(x * u, y * u) for (x, y) in pts]
    # the swoosh's far half (behind the chip): an orange ring tilted
    back = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    db = ImageDraw.Draw(back)
    db.ellipse((2 * u, 30 * u, 98 * u, 78 * u), outline=ORANGE + (255,), width=max(1, int(7 * u)))
    back = back.rotate(-14, resample=Image.BICUBIC, center=(N / 2, N / 2))
    im.alpha_composite(back)
    # pins top and bottom
    x0, x1 = 24, 76
    step = (x1 - x0) / pins
    for k in range(pins):
        a = x0 + k * step + step * .18
        b = a + step * .64
        for (ya, yb) in ((8, 22), (78, 92)):
            d.rectangle((a * u, ya * u, b * u, yb * u), fill=GOLD_LO + (255,))
            d.rectangle((a * u, ya * u, (b - step * .14) * u, (yb - 2.5) * u if ya < 50 else yb * u), fill=GOLD + (255,))
            d.rectangle((a * u, ya * u, (a + step * .2) * u, yb * u), fill=GOLD_HI + (255,))
    # the chip: an outlined chamfered body with a lit top edge and an inner panel
    body = [(20, 22), (72, 18), (82, 26), (80, 74), (74, 80), (26, 82), (16, 74), (16, 28)]
    d.polygon(P(body), fill=EDGE + (255,))
    inner = [(22.5, 24.5), (71, 21), (79, 27.5), (77.5, 72.5), (72.5, 77.5), (27, 79.5), (18.8, 72.5), (18.8, 29)]
    d.polygon(P(inner), fill=PURPLE + (255,))
    d.polygon(P([(22.5, 24.5), (71, 21), (79, 27.5), (76, 30), (69.5, 25.5), (24, 28.5), (21.5, 31), (18.8, 29)]), fill=PURPLE_HI + (255,))
    d.polygon(P([(27, 79.5), (72.5, 77.5), (77.5, 72.5), (77.8, 66), (71, 72.5), (28, 74.5), (19, 67), (18.8, 72.5)]), fill=PURPLE_LO + (255,))
    # the C: heavy, slanted, cream with a warm lower edge
    C = [(66, 30), (40, 31.5), (30, 40), (27, 62), (34, 70), (62, 68.5), (64.5, 58), (42, 59.5), (40, 56), (42, 45), (45.5, 42), (64, 41)]
    sh = [(x + 1.6, y + 2.2) for (x, y) in C]
    d.polygon(P(sh), fill=EDGE + (255,))
    d.polygon(P(C), fill=CREAM + (255,))
    d.polygon(P([(27, 62), (34, 70), (62, 68.5), (63, 64.5), (35.5, 66), (29.5, 59.5)]), fill=CREAM_LO + (255,))
    # the swoosh's near half (in front, lower right) with a bright core
    front = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    df = ImageDraw.Draw(front)
    df.arc((2 * u, 30 * u, 98 * u, 78 * u), 20, 165, fill=EDGE + (255,), width=max(1, int(10.5 * u)))
    df.arc((2 * u, 30 * u, 98 * u, 78 * u), 22, 163, fill=ORANGE + (255,), width=max(1, int(7 * u)))
    df.arc((3.2 * u, 31.2 * u, 96.8 * u, 76.8 * u), 40, 150, fill=ORANGE_HI + (255,), width=max(1, int(2.6 * u)))
    front = front.rotate(-14, resample=Image.BICUBIC, center=(N / 2, N / 2))
    im.alpha_composite(front)
    return im.resize((n, n), Image.LANCZOS)


def grid(n):
    """The 32 and 16 px marks on their own pixel grids: the chip and its pins in whole pixels, the swoosh and the C laid over
    them from the large drawing."""
    k = n // 16                              # 1 at 16, 2 at 32
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    px = im.load()

    def rect(x0, y0, x1, y1, c):
        for y in range(y0 * k, y1 * k):
            for x in range(x0 * k, x1 * k):
                px[x, y] = c + (255,)
    # in 16 px units: pins 2 wide with 1 gaps at x 4, 7, 10 (three pins), rows 1-2 and 13-14
    for x in (4, 7, 10):
        rect(x, 1, x + 2, 3, GOLD)
        rect(x, 13, x + 2, 15, GOLD)
        if k > 1:
            for y in range(1 * k, 3 * k):
                px[x * k, y] = GOLD_HI + (255,)
            for y in range(13 * k, 15 * k):
                px[x * k, y] = GOLD_HI + (255,)
                px[(x + 2) * k - 1, y] = GOLD_LO + (255,)
        else:
            px[x + 1, 14] = GOLD_LO + (255,)
            px[x + 1, 1] = GOLD_HI + (255,)
    # the chip: outline 2..14 x 3..13, body inside, a lit top row and a shaded bottom row
    rect(2, 3, 14, 13, EDGE)
    rect(3, 4, 13, 12, PURPLE)
    rect(3, 4, 13, 5, PURPLE_HI)
    rect(3, 11, 13, 12, PURPLE_LO)
    for (cx, cy) in ((2, 3), (13, 3), (2, 12), (13, 12)):
        for y in range(cy * k, (cy + 1) * k):
            for x in range(cx * k, (cx + 1) * k):
                if k == 1 or (abs(x - (cx * k + (0 if cx < 8 else k - 1))) + abs(y - (cy * k + (0 if cy < 8 else k - 1)))) < k:
                    px[x, y] = (0, 0, 0, 0)
    # the C in whole pixels (16 px units): a 5 x 6 block letter, cream
    for (x0, y0, x1, y1) in ((6, 5, 11, 7), (5, 6, 7, 10), (6, 9, 11, 11)):
        rect(x0, y0, x1, y1, CREAM)
    if k > 1:
        rect(6, 10, 11, 11, CREAM_LO)
        for (x, y) in ((5 * k, 6 * k), (5 * k, 10 * k - 1)):
            px[x, y] = PURPLE + (255,)
    # the swoosh: the large drawing's front arc alone, reduced, laid over the lower right
    N = n * 8
    u = N / 100.0
    front = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    df = ImageDraw.Draw(front)
    df.arc((1 * u, 34 * u, 99 * u, 84 * u), 12, 168, fill=EDGE + (255,), width=max(1, int(15 * u)))
    df.arc((1 * u, 34 * u, 99 * u, 84 * u), 14, 166, fill=ORANGE + (255,), width=max(1, int(9.5 * u)))
    front = front.rotate(-14, resample=Image.BICUBIC, center=(N / 2, N / 2)).resize((n, n), Image.LANCZOS)
    # keep the pins crisp: the swoosh does not cover the bottom pins' pixels
    keep = im.copy()
    im.alpha_composite(front)
    for x in (4, 7, 10):
        for y in range(13 * k, 15 * k):
            for xx in range(x * k, (x + 2) * k):
                px[xx, y] = keep.getpixel((xx, y))
    return im


def build(logo_path, out, sheet=None):
    logo = Image.open(logo_path).convert('RGBA')
    os.makedirs(out, exist_ok=True)
    sizes = (16, 24, 32, 48, 64, 128, 256)
    ims = {}
    for n in sizes + (1024,):
        if n >= 64:
            ims[n] = full(logo, n)
        elif n in (16, 32):
            ims[n] = grid(n)
        else:
            ims[n] = mark(n)
    for n in sizes:
        ims[n].save(os.path.join(out, 'ChipArenaIcon-%d.png' % n))
    ims[1024].save(os.path.join(out, 'ChipArenaIcon-1024.png'))
    ims[256].save(os.path.join(out, 'ChipArenaIcon.ico'), format='ICO', sizes=[(n, n) for n in sizes], append_images=[ims[n] for n in sizes if n != 256])
    if sheet:
        pad, zoom = 12, 4
        rows = []
        for bg in ((238, 240, 244), (32, 34, 40)):
            w = sum(max(n, 64) for n in sizes) + pad * (len(sizes) + 1)
            row = Image.new('RGBA', (w, 256 + 2 * pad), bg + (255,))
            x = pad
            for n in sizes:
                row.alpha_composite(ims[n], (x, pad + (256 - n) // 2))
                x += max(n, 64) + pad
            rows.append(row)
            # the small sizes enlarged (nearest), to judge the pixels
            w2 = sum(n * zoom for n in sizes[:4]) + pad * 5
            row2 = Image.new('RGBA', (w, 48 * zoom + 2 * pad), bg + (255,))
            x = pad
            for n in sizes[:4]:
                row2.alpha_composite(ims[n].resize((n * zoom, n * zoom), Image.NEAREST), (x, pad + (48 * zoom - n * zoom) // 2))
                x += n * zoom + pad
            rows.append(row2)
        s = Image.new('RGB', (rows[0].width, sum(r.height for r in rows)))
        y = 0
        for r in rows:
            s.paste(r.convert('RGB'), (0, y))
            y += r.height
        s.save(sheet)
    return ims


if __name__ == '__main__':
    a = sys.argv[1:]
    build(a[0], a[1], a[a.index('--sheet') + 1] if '--sheet' in a else None)
