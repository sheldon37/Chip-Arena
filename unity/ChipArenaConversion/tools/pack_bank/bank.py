"""Write the pack bank (the format Core/NativePackBank.cs reads; docs/PACK-BANK.md)."""
import struct

MAGIC, VERSION = b'CAPB', 1
ATTR_NORMAL, ATTR_UV0, ATTR_UV1, ATTR_COLOR = 1, 2, 4, 8
TEX_ALPHA, TEX_LINEAR = 1, 2
MAT_CUTOUT, MAT_TRANSPARENT, MAT_TWO_SIDED, MAT_GLASS = 1, 2, 4, 8


def _str(s):
    b = s.encode('utf-8')
    return struct.pack('<i', len(b)) + b


class Bank:
    def __init__(self):
        self.textures, self.materials, self.meshes, self.props = [], [], [], []
        self._tex, self._mat, self._mesh = {}, {}, {}

    def texture(self, name, flags=0):
        if name not in self._tex:
            self._tex[name] = len(self.textures)
            self.textures.append((name, flags))
        return self._tex[name]

    def material(self, name, albedo=-1, emission=-1, color=(1, 1, 1, 1), emission_color=(0, 0, 0), flags=0):
        if name not in self._mat:
            self._mat[name] = len(self.materials)
            self.materials.append((name, albedo, emission, tuple(color), tuple(emission_color), flags))
        return self._mat[name]

    def mesh(self, name, mesh, materials):
        """mesh: fbx.Mesh in Unity space; materials: bank material index per submesh."""
        if name in self._mesh:
            return self._mesh[name]
        if len(materials) != len(mesh.submeshes):
            raise ValueError('materials per submesh')
        self._mesh[name] = len(self.meshes)
        self.meshes.append((name, mesh, list(materials)))
        return self._mesh[name]

    def prop(self, name, group, parts):
        """parts: [(mesh index, 3x4 row-major matrix as 12 floats)]."""
        lo, hi = [1e30] * 3, [-1e30] * 3
        for mi, m in parts:
            mesh = self.meshes[mi][1]
            for p in mesh.positions:
                q = (m[0] * p[0] + m[1] * p[1] + m[2] * p[2] + m[3], m[4] * p[0] + m[5] * p[1] + m[6] * p[2] + m[7],
                     m[8] * p[0] + m[9] * p[1] + m[10] * p[2] + m[11])
                for i in range(3):
                    lo[i], hi[i] = min(lo[i], q[i]), max(hi[i], q[i])
        if lo[0] > hi[0]:
            lo, hi = [0.0] * 3, [0.0] * 3
        self.props.append((name, group, tuple(lo), tuple(hi), parts))

    def write(self):
        out = [MAGIC, struct.pack('<i', VERSION), struct.pack('<i', len(self.textures))]
        for name, flags in self.textures:
            out += [_str(name), struct.pack('<B', flags)]
        out.append(struct.pack('<i', len(self.materials)))
        for name, albedo, emission, color, ecolor, flags in self.materials:
            out += [_str(name), struct.pack('<ii4f3fB', albedo, emission, *color, *ecolor, flags)]
        out.append(struct.pack('<i', len(self.meshes)))
        for name, mesh, mats in self.meshes:
            lo, hi = mesh.bounds()
            n = len(mesh.positions)
            attrs = ATTR_NORMAL | ATTR_UV0 | (ATTR_UV1 if mesh.uv1 else 0) | (ATTR_COLOR if mesh.colors else 0)
            out += [_str(name), struct.pack('<3f3fiB', *lo, *hi, n, attrs)]
            out.append(struct.pack('<%df' % (n * 3), *[c for p in mesh.positions for c in p]))
            out.append(struct.pack('<%df' % (n * 3), *[c for p in mesh.normals for c in p]))
            out.append(struct.pack('<%df' % (n * 2), *[c for p in mesh.uv0 for c in p]))
            if mesh.uv1:
                out.append(struct.pack('<%df' % (n * 2), *[c for p in mesh.uv1 for c in p]))
            if mesh.colors:
                out.append(bytes(max(0, min(255, int(round(c * 255)))) for col in mesh.colors for c in col))
            out.append(struct.pack('<i', len(mesh.submeshes)))
            wide = n > 65535
            for mat, sub in zip(mats, mesh.submeshes):
                out.append(struct.pack('<ii', mat, len(sub)))
                out.append(struct.pack('<%d%s' % (len(sub), 'I' if wide else 'H'), *sub))
        out.append(struct.pack('<i', len(self.props)))
        for name, group, lo, hi, parts in self.props:
            out += [_str(name), _str(group), struct.pack('<3f3fi', *lo, *hi, len(parts))]
            for mi, m in parts:
                out.append(struct.pack('<i12f', mi, *m))
        return b''.join(out)


def affine(m):
    """The 3x4 rows of a 4x4 row-major matrix."""
    return [float(x) for x in m[:12]]
