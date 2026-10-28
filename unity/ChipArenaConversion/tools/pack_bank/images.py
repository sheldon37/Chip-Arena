"""Decode PNG, TGA and PSD (the merged image) to RGBA8, scale down, and encode PNG. Standard library only; data only."""
import struct
import zlib


class ImageError(Exception):
    pass


class Image:
    """w x h RGBA8, rows top to bottom."""

    def __init__(self, w, h, rgba):
        if len(rgba) != w * h * 4:
            raise ImageError('size mismatch')
        self.w, self.h, self.rgba = w, h, bytearray(rgba)

    def has_alpha(self):
        return any(self.rgba[i] != 255 for i in range(3, len(self.rgba), 4))


# PNG ------------------------------------------------------------------------------------------------------------------

def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def decode_png(data):
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ImageError('not a PNG')
    pos, chunks, idat = 8, {}, []
    palette = trns = None
    while pos < len(data):
        n, kind = struct.unpack_from('>I4s', data, pos)
        body = data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if kind == b'IHDR':
            w, h, depth, ctype, _, _, interlace = struct.unpack('>IIBBBBB', body)
        elif kind == b'PLTE':
            palette = body
        elif kind == b'tRNS':
            trns = body
        elif kind == b'IDAT':
            idat.append(body)
        elif kind == b'IEND':
            break
    chans = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    bits = depth * chans
    bpp = max(1, bits // 8)
    raw = zlib.decompress(b''.join(idat))
    out = bytearray(w * h * 4)
    # Adam7 interlacing: seven reduced images, each filtered on its own, scattered onto the grid (x0, y0, dx, dy).
    passes = ((0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)) if interlace else ((0, 0, 1, 1),)
    at = 0
    for x0, y0, dx, dy in passes:
        pw, ph = (w - x0 + dx - 1) // dx, (h - y0 + dy - 1) // dy
        if pw <= 0 or ph <= 0:
            continue
        stride = (pw * bits + 7) // 8
        prev = bytearray(stride)
        for row in range(ph):
            f = raw[at]
            line = bytearray(raw[at + 1:at + 1 + stride])
            at += 1 + stride
            if f == 1:
                for i in range(bpp, stride):
                    line[i] = (line[i] + line[i - bpp]) & 255
            elif f == 2:
                for i in range(stride):
                    line[i] = (line[i] + prev[i]) & 255
            elif f == 3:
                for i in range(stride):
                    line[i] = (line[i] + (((line[i - bpp] if i >= bpp else 0) + prev[i]) >> 1)) & 255
            elif f == 4:
                for i in range(stride):
                    a = line[i - bpp] if i >= bpp else 0
                    c = prev[i - bpp] if i >= bpp else 0
                    line[i] = (line[i] + _paeth(a, prev[i], c)) & 255
            prev = line
            y = y0 + row * dy
            for col in range(pw):
                x = col
                if depth == 8:
                    px = line[x * chans:(x + 1) * chans]
                elif depth == 16:
                    px = line[x * chans * 2:(x + 1) * chans * 2:2]
                else:  # 1, 2, 4 bit grey or palette
                    per = 8 // depth
                    byte = line[x // per]
                    v = (byte >> (8 - depth * (x % per + 1))) & ((1 << depth) - 1)
                    px = bytes([v if ctype == 3 else v * 255 // ((1 << depth) - 1)])
                if ctype == 0:
                    r = g = b = px[0]
                    a = 255
                elif ctype == 2:
                    r, g, b = px[0], px[1], px[2]
                    a = 255
                elif ctype == 3:
                    i = px[0]
                    r, g, b = palette[i * 3:i * 3 + 3]
                    a = trns[i] if trns and i < len(trns) else 255
                elif ctype == 4:
                    r = g = b = px[0]
                    a = px[1]
                else:
                    r, g, b, a = px[0], px[1], px[2], px[3]
                o = (y * w + x0 + col * dx) * 4
                out[o:o + 4] = bytes((r, g, b, a))
    return Image(w, h, out)


def encode_png(img, alpha=True):
    chans = 4 if alpha else 3
    rows = []
    src = img.rgba
    for y in range(img.h):
        row = src[y * img.w * 4:(y + 1) * img.w * 4]
        if not alpha:
            row = bytes(b for i, b in enumerate(row) if i % 4 != 3)
        rows.append(b'\x00' + bytes(row))
    raw = zlib.compress(b''.join(rows), 9)

    def chunk(kind, body):
        return struct.pack('>I', len(body)) + kind + body + struct.pack('>I', zlib.crc32(kind + body) & 0xffffffff)
    ihdr = struct.pack('>IIBBBBB', img.w, img.h, 8, 6 if alpha else 2, 0, 0, 0)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) + chunk(b'IDAT', raw) + chunk(b'IEND', b'')


# TGA ------------------------------------------------------------------------------------------------------------------

def decode_tga(data):
    idlen, cmtype, kind = data[0], data[1], data[2]
    w, h, bpp, desc = struct.unpack_from('<HHBB', data, 12)
    if cmtype or kind not in (2, 3, 10, 11):
        raise ImageError('TGA type %d' % kind)
    pos = 18 + idlen
    size = bpp // 8
    n = w * h
    pixels = bytearray()
    if kind in (2, 3):
        pixels = data[pos:pos + n * size]
    else:
        while len(pixels) < n * size:
            head = data[pos]
            pos += 1
            count = (head & 127) + 1
            if head & 128:
                pixels += data[pos:pos + size] * count
                pos += size
            else:
                pixels += data[pos:pos + count * size]
                pos += count * size
    out = bytearray(n * 4)
    top = bool(desc & 0x20)
    for y in range(h):
        sy = y if top else h - 1 - y
        for x in range(w):
            i = (sy * w + x) * size
            o = (y * w + x) * 4
            if size == 1:
                out[o:o + 4] = bytes((pixels[i], pixels[i], pixels[i], 255))
            elif size == 3:
                out[o:o + 4] = bytes((pixels[i + 2], pixels[i + 1], pixels[i], 255))
            elif size == 4:
                out[o:o + 4] = bytes((pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3]))
            else:
                raise ImageError('TGA depth %d' % bpp)
    return Image(w, h, out)


# PSD (the merged image only) -------------------------------------------------------------------------------------------

def decode_psd(data):
    if data[:4] != b'8BPS':
        raise ImageError('not a PSD')
    version, = struct.unpack_from('>H', data, 4)
    chans, h, w, depth, mode = struct.unpack_from('>HIIHH', data, 12)
    if depth not in (8, 16) or mode not in (1, 3):
        raise ImageError('PSD depth %d mode %d' % (depth, mode))
    pos = 26
    for _ in range(3):  # colour mode data, image resources, layer and mask info
        n = struct.unpack_from('>Q' if (_ == 2 and version == 2) else '>I', data, pos)[0]
        pos += (8 if (_ == 2 and version == 2) else 4) + n
    comp, = struct.unpack_from('>H', data, pos)
    pos += 2
    bpc = depth // 8
    rowbytes = w * bpc
    planes = []
    if comp == 0:
        for c in range(chans):
            planes.append(data[pos + c * h * rowbytes:pos + (c + 1) * h * rowbytes])
    elif comp == 1:
        cw = 4 if version == 2 else 2
        counts = struct.unpack_from('>%d%s' % (chans * h, 'I' if cw == 4 else 'H'), data, pos)
        pos += chans * h * cw
        for c in range(chans):
            plane = bytearray()
            for y in range(h):
                n = counts[c * h + y]
                row = data[pos:pos + n]
                pos += n
                i, line = 0, bytearray()
                while i < len(row):
                    k = row[i]
                    i += 1
                    if k < 128:
                        line += row[i:i + k + 1]
                        i += k + 1
                    elif k > 128:
                        line += bytes([row[i]]) * (257 - k)
                        i += 1
                plane += line[:rowbytes].ljust(rowbytes, b'\x00')
            planes.append(bytes(plane))
    else:
        raise ImageError('PSD compression %d' % comp)
    if bpc == 2:
        planes = [p[0::2] for p in planes]
    n = w * h
    out = bytearray(n * 4)
    if mode == 3:
        r, g, b = planes[0], planes[1], planes[2]
        a = planes[3] if chans > 3 else None
        out[0::4], out[1::4], out[2::4] = r[:n], g[:n], b[:n]
        out[3::4] = a[:n] if a is not None else b'\xff' * n
    else:
        v = planes[0][:n]
        out[0::4] = out[1::4] = out[2::4] = v
        out[3::4] = planes[1][:n] if chans > 1 else b'\xff' * n
    return Image(w, h, out)


def decode(path):
    with open(path, 'rb') as f:
        data = f.read()
    low = path.lower()
    if low.endswith('.png'):
        return decode_png(data)
    if low.endswith('.tga'):
        return decode_tga(data)
    if low.endswith('.psd'):
        return decode_psd(data)
    raise ImageError('unsupported image ' + path)


# Scaling ----------------------------------------------------------------------------------------------------------------

def half(img):
    """Box filter to half size (odd edges dropped)."""
    w, h = max(1, img.w // 2), max(1, img.h // 2)
    if img.w < 2 or img.h < 2:
        return img
    src, sw = img.rgba, img.w * 4
    out = bytearray(w * h * 4)
    for y in range(h):
        a = src[(2 * y) * sw:(2 * y + 1) * sw]
        b = src[(2 * y + 1) * sw:(2 * y + 2) * sw]
        # Sum the two rows, then pairs of pixels.
        s = [a[i] + b[i] for i in range(w * 8)]
        row = bytearray(w * 4)
        for x in range(w):
            i = x * 8
            row[x * 4] = (s[i] + s[i + 4] + 2) >> 2
            row[x * 4 + 1] = (s[i + 1] + s[i + 5] + 2) >> 2
            row[x * 4 + 2] = (s[i + 2] + s[i + 6] + 2) >> 2
            row[x * 4 + 3] = (s[i + 3] + s[i + 7] + 2) >> 2
        out[y * w * 4:(y + 1) * w * 4] = row
    return Image(w, h, out)


def fit(img, limit):
    while img.w > limit or img.h > limit:
        img = half(img)
    return img


def crop(img, x0, y0, w, h):
    out = bytearray(w * h * 4)
    for y in range(h):
        s = ((y0 + y) * img.w + x0) * 4
        out[y * w * 4:(y + 1) * w * 4] = img.rgba[s:s + w * 4]
    return Image(w, h, out)
