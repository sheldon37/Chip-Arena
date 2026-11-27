"""The pilot's hair bank (owner 2026-11-26: "Lets add a lot more hairstyles, some anime ones as well (Think like sasuke from naruto)
[For both male and female]. Add magica cloth into these to simulate hair movement realistically"; docs/PILOT-BODY-HAIR.md section 3).

The pack's hair pieces each carry one short bone chain for the whole mass (or none), so cloth on them swings the hair as one block.
This tool rebuilds them for realistic motion and writes the local bank Resources/ChipArenaHair.bytes (made from the licensed pack's
meshes, so kept local like the bake; Runtime/NativeHairBankPresentation.cs builds each worn piece on the model's head):

- the piece as the pack has it, in its head bone's frame (the local hair lab's dump: Unity's import, so the frame and the vertex
  order are the game's): its surface, normals, colours (the toon's layer), its texture's used block (cropped, 256 px), and its tag
  shapes (Tag_Hat, Tag_Helm, Tag_Hood: how it tucks under headgear);
- its loose part: how far each vertex stands off the head (the head's own surface as a radial height field), eased from 2 to 5 cm;
- its flow: the distance over the hair's own surface from where it leaves the scalp (Dijkstra over the welded mesh), so a lock or a
  curtain is measured along itself however it bends;
- chains across each loose mass (one every ~20 degrees round the head, a mass's own when it is narrow: a twin tail, a side bang),
  each a row of joints down its sector of the hair at even flow, placed in the middle of the hair's thickness there; adjacent
  chains of one mass are one cloth group (Magica connects them as a sheet: a curtain stays a curtain);
- skin weights: the fixed part on its chain's root (which rides the head), the loose part shared between the two nearest chains and
  the two joints either side of its flow (four bones at most).

Bespoke anime pieces (generated locks: the spiked back and long side bangs the owner asked for) are made the same way from their own
lock curves (hairgen.py).

usage: python -I tools/gear/hair.py --dump DIR --out ASSETS/LocalAssets [--preview DIR] [--only style,style]
"""
import argparse
import heapq
import io
import json
import math
import os
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # (hairgen.py beside it, under python -I)

BANK = 'ChipArenaHair'
RESOURCES = 'ChipArenaLocal/Resources'
PACK = 'BoZo_StylizedModularCharacters/Outfits/Anime'

# Cloth kinds (the bank's chain names carry them: the local bridge tunes each): stiff spikes that spring back, medium locks,
# a free long fall, a connected curtain, a tied tail.
SPIKE, LOCK, FALL, CURTAIN, TAIL, RIGID = 'S', 'L', 'F', 'C', 'T', 'R'

# The styles (Core/NativeLook.cs ids): each a front and a back piece (a piece's cloth kind, its lateral spacing in degrees).
STYLES = [
    # generated (hairgen.py): the anime spikes, on the pack's short cap
    ('raven-spikes', [('HairBack_ShotaCut', RIGID), ('GEN_RavenBack', SPIKE), ('GEN_RavenFront', LOCK)]),
    ('hero-spikes', [('HairBack_ShotaCut', RIGID), ('GEN_HeroBack', SPIKE), ('GEN_HeroFront', LOCK)]),
    ('wolf', [('HairFront_LynxFringe', LOCK), ('HairBack_WildLocks', SPIKE)]),
    ('mane', [('HairFront_Messy', LOCK), ('HairBack_YokaiMane', SPIKE)]),
    ('messy', [('HairFront_Messy', LOCK), ('HairBack_MessyHair', SPIKE)]),
    ('emo', [('HairFront_EmoBangs', LOCK), ('HairBack_ShotaCut', RIGID)]),
    ('shag', [('HairFront_MinorFringe', LOCK), ('HairBack_Flare', LOCK)]),
    ('dreads', [('HairFront_SweaptBack', LOCK), ('HairBack_SweaptDreads', LOCK)]),
    ('short-pony', [('HairFront_SideSwept', LOCK), ('HairBack_ShortPonyTail', TAIL)]),
    ('twin-tails', [('HairFront_MinorFringe', LOCK), ('HairBack_TwinLongTails', TAIL)]),
    ('pigtails', [('HairFront_ShotaFringe', LOCK), ('HairBack_TwinShortTails', TAIL)]),
    ('twin-buns', [('HairFront_ShotaFringe', LOCK), ('HairBack_TwinBuns', RIGID)]),
    ('bun', [('HairFront_CurtainBangs', LOCK), ('HairBack_TiedBun', RIGID)]),
    ('hime', [('HairFront_HimeCut', CURTAIN), ('HairBack_LongStreight', CURTAIN)]),
    ('long-straight', [('HairFront_CurtainBangs', LOCK), ('HairBack_LongStreight', CURTAIN)]),
    ('shoujo', [('HairFront_Shoujo', CURTAIN), ('HairBack_CasualFlow', CURTAIN)]),
    ('lob', [('HairFront_AsymmetricalFringe', LOCK), ('HairBack_CasualFlow', CURTAIN)]),
]

LOOSE_FROM, LOOSE_TO = .02, .05     # m off the head: fixed below, loose above
SEGMENT = .035                       # m of flow between joints
MAX_SEGMENTS = 5
SPACING = math.radians(20)           # between chains round the head


# ---------------------------------------------------------------------------------------------------------------------------
# The dump
# ---------------------------------------------------------------------------------------------------------------------------
class Piece:
    def __init__(self, name, r):
        self.name = name
        self.v = np.array(r['v'], float).reshape(-1, 3)
        self.n = np.array(r['n'], float).reshape(-1, 3)
        self.uv = np.array(r['uv'], float).reshape(-1, 2)
        self.c = np.array(r['c'], float).reshape(-1, 4) if r['c'] else np.tile([1., 0, 0, 1], (len(self.v), 1))
        self.tris = np.concatenate([np.array(s, np.int64) for s in r['submeshes']]).reshape(-1, 3)
        self.shapes = [(s['name'].split('.')[-1], np.array(s['dv'], float).reshape(-1, 3)) for s in r['shapes']
                       if s['name'].split('.')[-1].startswith('Tag_') and s['name'].split('.')[-1] != 'Tag_Anime']
        self.front = name.startswith('HairFront')
        self.texture = None
        # the pack's own idea of what moves: its chain bones' weights by their depth down the chain (a chain's root is fixed)
        bones, parents = r['bones'], r['parents']
        bi = np.array(r['bi'], np.int64).reshape(-1, 4)
        bw = np.array(r['bw'], float).reshape(-1, 4)
        chain = lambda b: b.startswith('Hair_')
        depth = np.zeros(len(bones))
        for k, b in enumerate(bones):
            if not chain(b):
                continue
            d, p, names = 0, parents[k], dict(zip(bones, parents))
            while chain(p):
                d += 1
                p = names.get(p, '')
            length = d + 1 + max([0] + [1 for kk, bb in enumerate(bones) if parents[kk] == b])
            depth[k] = d
        # a bone's share: its depth over its chain's deepest
        deepest = {}
        for k, b in enumerate(bones):
            if chain(b):
                r0 = b
                while chain(dict(zip(bones, parents)).get(r0, '')):
                    r0 = dict(zip(bones, parents))[r0]
                deepest[r0] = max(deepest.get(r0, 0), depth[k])
        share = np.zeros(len(bones))
        for k, b in enumerate(bones):
            if chain(b) and depth[k] > 0:
                r0 = b
                while chain(dict(zip(bones, parents)).get(r0, '')):
                    r0 = dict(zip(bones, parents))[r0]
                share[k] = depth[k] / max(deepest[r0], 1)
        self.pack_loose = (bw * share[bi]).sum(1)


def load_piece(dump, name):
    d = json.load(open(os.path.join(dump, name + '.json')))
    rs = [r for r in d['renderers'] if len(r['v']) > 0]
    if len(rs) != 1:
        raise SystemExit('%s: %d renderers' % (name, len(rs)))
    return Piece(name, rs[0])


# ---------------------------------------------------------------------------------------------------------------------------
# The head: a radial height field round its centre (so a vertex's offset is how far it stands off the head)
# ---------------------------------------------------------------------------------------------------------------------------
class Head:
    def __init__(self, dump):
        d = json.load(open(os.path.join(dump, 'Head_AnimeYoung.json')))
        v = np.concatenate([np.array(r['v'], float).reshape(-1, 3) for r in d['renderers'] if r['v']])
        self.v = v
        self.c = np.array([0., .085, .02])
        # the head's radius per direction (a 48 x 24 grid in longitude and latitude, filled outward from its vertices)
        self.nl, self.na = 48, 24
        grid = np.zeros((self.na, self.nl))
        for p in v:
            a, l, r = self.polar(p)
            grid[a, l] = max(grid[a, l], r)
        # holes (no vertex in a cell) from their neighbours
        for _ in range(8):
            z = grid == 0
            if not z.any():
                break
            pad = np.pad(grid, ((1, 1), (0, 0)), mode='edge')
            nb = np.stack([np.roll(grid, 1, 1), np.roll(grid, -1, 1), pad[:-2], pad[2:]])
            cnt = (nb > 0).sum(0)
            grid[z & (cnt > 0)] = (nb.sum(0) / np.maximum(cnt, 1))[z & (cnt > 0)]
        self.grid = grid

    def polar(self, p):
        d = p - self.c
        r = np.linalg.norm(d)
        lon = math.atan2(d[0], d[2])
        lat = math.asin(max(-1., min(1., d[1] / max(r, 1e-9))))
        l = int((lon + math.pi) / (2 * math.pi) * self.nl) % self.nl
        a = min(self.na - 1, max(0, int((lat + math.pi / 2) / math.pi * self.na)))
        return a, l, r

    def offset(self, pts):
        out = np.zeros(len(pts))
        for i, p in enumerate(pts):
            a, l, r = self.polar(p)
            out[i] = r - self.grid[a, l]
        return out


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------------------------------------------------------------
# Flow and chains
# ---------------------------------------------------------------------------------------------------------------------------
def weld(v, tris, eps=1e-5):
    key = {}
    idx = np.zeros(len(v), np.int64)
    for i, p in enumerate(np.round(v / eps).astype(np.int64)):
        idx[i] = key.setdefault((p[0], p[1], p[2]), len(key))
    return idx, len(key)


def flow(v, tris, seeds):
    """Distance over the surface from the seed vertices (welded), and each vertex's source seed."""
    idx, n = weld(v, tris)
    pos = np.zeros((n, 3))
    pos[idx] = v
    adj = [[] for _ in range(n)]
    for t in idx[tris]:
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            if a != b:
                d = float(np.linalg.norm(pos[a] - pos[b]))
                adj[a].append((b, d))
                adj[b].append((a, d))
    dist = np.full(n, np.inf)
    src = np.full(n, -1)
    heap = []
    wseed = set(idx[seeds].tolist())
    for s in wseed:
        dist[s] = 0
        src[s] = s
        heapq.heappush(heap, (0., s))
    while heap:
        d, a = heapq.heappop(heap)
        if d > dist[a]:
            continue
        for b, w in adj[a]:
            if d + w < dist[b]:
                dist[b] = d + w
                src[b] = src[a]
                heapq.heappush(heap, (d + w, b))
    # islands no seed reaches: seeded at their own vertex nearest the head (handled by the caller's second pass)
    return dist[idx], src[idx], pos, idx


def lateral(p, front):
    """Round the head: 0 at the back's middle (a back piece) or the face's (a front piece), radians to either side."""
    return np.arctan2(p[..., 0], (p[..., 2] - .02) * (1 if front else -1))


class Chain:
    def __init__(self, theta, joints, group):
        self.theta = theta
        self.joints = joints      # (J+1, 3) head-frame points, root first
        self.levels = None        # the flow at each joint
        self.group = group


def rig(piece, head, kind, spacing=SPACING):
    v = piece.v
    if kind == RIGID:  # (a bun: rides the head whole)
        n = len(v)
        return [], (np.zeros((n, 4), np.int64), np.concatenate([np.ones((n, 1)), np.zeros((n, 3))], 1)), dict(loose=np.zeros(n))
    off = head.offset(v)
    loose = np.maximum(smoothstep(LOOSE_FROM, LOOSE_TO, off), np.clip(piece.pack_loose * 1.5, 0, 1))
    seeds = np.where(loose < .05)[0]
    if len(seeds) == 0:
        seeds = np.array([int(np.argmin(off))])
    g, src, _, _ = flow(v, piece.tris, seeds)
    # islands with no seed: from their own nearest-to-head vertex
    if np.isinf(g).any():
        idx, _ = weld(v, piece.tris)
        lost = np.where(np.isinf(g))[0]
        extra = []
        # one seed per unreached island
        reach_g, _, _, _ = None, None, None, None
        comp = {}
        parent = list(range(int(idx.max()) + 1))

        def f(a):
            while parent[a] != a:
                parent[a] = parent[parent[a]]
                a = parent[a]
            return a
        for t in idx[piece.tris]:
            a = f(t[0]); parent[f(t[1])] = a; parent[f(t[2])] = f(a)
        for i in lost:
            r = f(idx[i])
            if r not in comp or off[i] < off[comp[r]]:
                comp[r] = i
        extra = list(comp.values())
        g, src, _, _ = flow(v, piece.tris, np.concatenate([seeds, np.array(extra, np.int64)]))
    root_of = np.where(src >= 0, src, 0)
    # the lateral coordinate of each vertex: its source seed's (the flow carries a lock's root round the head)
    idx, _ = weld(v, piece.tris)
    welded_pos = {}
    for i, k in enumerate(idx):
        welded_pos[k] = v[i]
    roots = np.array([welded_pos[s] if s in welded_pos else v[0] for s in root_of])
    th = lateral(roots, piece.front)
    # for a loose vertex whose root is far round (a curtain's side), its own lateral position reads better: blend toward it
    th = np.where(loose > .5, np.arctan2(np.sin(th) + np.sin(lateral(v, piece.front)), np.cos(th) + np.cos(lateral(v, piece.front))), th)

    m = loose * np.clip(g / .02, 0, 1)
    chains = []
    if (m > .3).sum() >= 60:
        # a slot every `spacing` round the head (one on the middle line); a slot with enough loose hair in it holds a chain at
        # that hair's middle, and slots next to each other are one mass (their chains one cloth sheet)
        n = int(round(2 * math.pi / spacing))
        step = 2 * math.pi / n
        centres = np.array([((k * step + math.pi) % (2 * math.pi)) - math.pi for k in range(n)])
        total = m.sum()
        kept = []
        for k, c in enumerate(centres):
            d = np.angle(np.exp(1j * (th - c)))
            w = m * (np.abs(d) <= step / 2)
            if w.sum() >= max(total * .04, 12):
                kept.append((k, float(c + (w * d).sum() / w.sum())))
        keep = {k for k, _ in kept}
        group, of = 0, {}
        for k, _ in kept:
            if (k - 1) % n in of:
                of[k] = of[(k - 1) % n]
            else:
                group += 1
                of[k] = group
        # (a mass that wraps past the first slot joins its other end)
        if kept and 0 in of and (n - 1) in of and of[0] != of[n - 1]:
            old_g = of[n - 1]
            for k in list(of):
                if of[k] == old_g:
                    of[k] = of[0]
        # each mass's chains in order round the head (a sheet's rows are joined in this order), none closer than half a slot
        by = {}
        for k, t in kept:
            by.setdefault(of[k], []).append(t)
        for grp in sorted(by):
            ts = by[grp]
            ref = math.atan2(sum(math.sin(t) for t in ts), sum(math.cos(t) for t in ts))
            ts = sorted(ts, key=lambda t: float(np.angle(np.exp(1j * (t - ref)))))
            last = None
            for t in ts:
                if last is not None and abs(float(np.angle(np.exp(1j * (t - last))))) < step * .45:
                    continue
                chains.append(Chain(t, None, grp))
                last = t
        for ch in chains:
            sigma_t = max(spacing * .6, math.radians(6))
            dt = np.angle(np.exp(1j * (th - ch.theta)))
            wt = m * np.exp(-.5 * (dt / sigma_t) ** 2)
            sel = wt > wt.max() * .05 if wt.max() > 0 else np.zeros(len(v), bool)
            if sel.sum() < 4:
                ch.joints = None
                continue
            gs = g[sel]
            g0 = float(np.percentile(g[sel & (loose > .5)], 3)) if (sel & (loose > .5)).sum() > 3 else float(gs.min())
            g1 = float(np.percentile(gs, 98))
            if g1 - g0 < .02:
                ch.joints = None
                continue
            nseg = max(1, min(MAX_SEGMENTS, int(round((g1 - g0) / SEGMENT))))
            levels = np.linspace(g0, g1, nseg + 1)
            sg = (g1 - g0) / nseg * .6
            joints = []
            for lv in levels:
                w = wt * np.exp(-.5 * ((g - lv) / sg) ** 2)
                if w.sum() < 1e-6:
                    joints.append(joints[-1] if joints else v[sel].mean(0))
                else:
                    joints.append((v * w[:, None]).sum(0) / w.sum())
            ch.joints = np.array(joints)
            ch.levels = levels
        chains = [c for c in chains if c.joints is not None]
    weights = weigh(piece, chains, th, g, loose)
    return chains, weights, dict(off=off, loose=loose, flow=g, theta=th)


def weigh(piece, chains, th, g, loose):
    """Per vertex up to four (bone, weight): bone 0 the head, then each chain's joints in order."""
    n = len(piece.v)
    out = np.zeros((n, 4), np.int64), np.zeros((n, 4))
    if not chains:
        out[1][:, 0] = 1
        return out
    base = []
    b = 1
    for ch in chains:
        base.append(b)
        b += len(ch.joints)
    thetas = np.array([c.theta for c in chains])
    groups = np.array([c.group for c in chains])
    for i in range(n):
        dt = np.angle(np.exp(1j * (th[i] - thetas)))
        order = np.argsort(np.abs(dt))
        a = order[0]
        # the neighbour on the other side, in the same mass
        bnb = None
        for k in order[1:]:
            if groups[k] == groups[a] and np.sign(dt[k]) != np.sign(dt[a]):
                bnb = k
                break
        if bnb is None or abs(dt[a]) < 1e-6:
            pairs = [(a, 1.)]
        else:
            da, db = abs(dt[a]), abs(dt[bnb])
            pairs = [(a, db / (da + db)), (bnb, da / (da + db))]
        infl = {}
        f = float(loose[i]) * float(np.clip((g[i] - chains[a].levels[0]) / .02 + 1, 0, 1))
        for c, s in pairs:
            ch = chains[c]
            lv = ch.levels
            t = np.interp(g[i], lv, np.arange(len(lv)))
            j = int(min(math.floor(t), len(lv) - 2)) if len(lv) > 1 else 0
            u = t - j
            for jj, w in ((j, 1 - u), (j + 1, u)):
                if w > 0 and jj < len(lv):
                    infl[base[c] + jj] = infl.get(base[c] + jj, 0) + f * s * w
        # the fixed part rides the nearest chain's root (which rides the head)
        infl[base[a]] = infl.get(base[a], 0) + (1 - f)
        top = sorted(infl.items(), key=lambda kv: -kv[1])[:4]
        tot = sum(w for _, w in top)
        for k, (bone, w) in enumerate(top):
            out[0][i, k] = bone
            out[1][i, k] = w / tot
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# Textures: the used block of the pack's texture (back pieces u,v .75..1, fronts u .5..75), 256 px
# ---------------------------------------------------------------------------------------------------------------------------
def texture_block(pack_root, piece):
    from PIL import Image
    folder = os.path.join(pack_root, 'HairFront' if piece.front else 'HairBack')
    path = None
    for root, _, files in os.walk(folder):
        for f in files:
            stem = f[:-4].lower()
            if f.lower().endswith('.png') and (stem.startswith(piece.name.lower()) or (piece.name.lower().startswith(stem.replace('_d', '').replace('_mat', '')) and len(stem) > 8)):
                path = os.path.join(root, f)
                break
        if path:
            break
    special = {'HairBack_LongPonyTail': 'HairBack_PonyTails/HairBack_PonyTail_D.png', 'HairBack_ShortPonyTail': 'HairBack_PonyTails/HairBack_PonyTail_D.png',
               'HairBack_TwinLongTails': 'HairBack_PonyTails/HairBack_TwinTails_D.png', 'HairBack_TwinShortTails': 'HairBack_PonyTails/HairBack_TwinTails_D.png',
               'HairBack_TiedBun': 'HairBack_PonyTails/HairBack_PonyTail_D.png', 'HairBack_TwinBuns': 'HairBack_PonyTails/HairBack_TwinTails_D.png',
               'HairFront_MinorFringe1': 'HairFront_MinorFringe/HairFront_MinorFringe.png'}
    if piece.name in special:
        path = os.path.join(pack_root, 'HairFront' if piece.front else 'HairBack', special[piece.name])
    if not path or not os.path.exists(path):
        raise SystemExit('no texture for ' + piece.name)
    im = Image.open(path).convert('RGBA')
    w, h = im.size
    u0 = .5 if piece.front else .75
    box = (int(u0 * w), 0, int((u0 + .25) * w), int(.25 * h))   # v .75..1 is the image's top quarter
    crop = im.crop(box).resize((256, 256), Image.LANCZOS)
    buf = io.BytesIO()
    crop.save(buf, 'PNG', optimize=True)
    uv = piece.uv.copy()
    uv[:, 0] = (uv[:, 0] - u0) / .25
    uv[:, 1] = (uv[:, 1] - .75) / .25
    return os.path.basename(path), buf.getvalue(), uv


# ---------------------------------------------------------------------------------------------------------------------------
# The bank (Core/NativeHairBank.cs reads it)
# ---------------------------------------------------------------------------------------------------------------------------
def s_str(out, s):
    b = s.encode('utf-8')
    out.write(struct.pack('<i', len(b)))
    out.write(b)


def bone_names(piece_name, kind, chains):
    short = piece_name.split('_', 1)[1]
    names, parents, local = [], [], []
    for k, ch in enumerate(chains):
        for j, p in enumerate(ch.joints):
            names.append('Hair_B%s%02d_%s_%02d_%d' % (kind, ch.group, short, k, j))
            if j == 0:
                parents.append(-1)
                local.append(p)
            else:
                parents.append(len(names) - 2)
                local.append(p - ch.joints[j - 1])
    return names, parents, local


def write_bank(path, pieces, styles, textures):
    out = io.BytesIO()
    out.write(b'HAIR')
    out.write(struct.pack('<i', 1))
    out.write(struct.pack('<i', len(textures)))
    for name, data in textures:
        s_str(out, name)
        out.write(struct.pack('<i', len(data)))
        out.write(data)
    out.write(struct.pack('<i', len(pieces)))
    for p in pieces:
        s_str(out, p['renderer'])
        out.write(struct.pack('<ifB', p['texture'], p['cutoff'], p['cull']))
        out.write(struct.pack('<i', len(p['bones'][0])))   # (bones: names, parents, offsets)
        for name, parent, loc in zip(*p['bones']):
            s_str(out, name)
            out.write(struct.pack('<i3f', parent, *map(float, loc)))
        n = len(p['v'])
        out.write(struct.pack('<i', n))
        out.write(np.asarray(p['v'], '<f4').tobytes())
        out.write(np.asarray(p['n'], '<f4').tobytes())
        out.write(np.asarray(p['uv'], '<f4').tobytes())
        out.write(np.clip(np.round(np.asarray(p['c']) * 255), 0, 255).astype(np.uint8).tobytes())
        out.write(np.asarray(p['bi'], np.uint8).tobytes())
        out.write(np.asarray(p['bw'], '<f4').tobytes())
        tris = np.asarray(p['tris'], '<i4').reshape(-1)
        out.write(struct.pack('<i', len(tris)))
        out.write(tris.tobytes())
        out.write(struct.pack('<i', len(p['shapes'])))
        for name, dv in p['shapes']:
            s_str(out, name)
            nz = np.where(np.abs(dv).max(1) > 1e-6)[0]
            out.write(struct.pack('<i', len(nz)))
            out.write(nz.astype('<i4').tobytes())
            out.write(np.asarray(dv[nz], '<f4').tobytes())
    out.write(struct.pack('<i', len(styles)))
    for sid, idxs in styles:
        s_str(out, sid)
        out.write(struct.pack('<i', len(idxs)))
        for i in idxs:
            out.write(struct.pack('<i', i))
    data = out.getvalue()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as fh:
        fh.write(data)
    return len(data)


# ---------------------------------------------------------------------------------------------------------------------------
# Generated pieces (hairgen.py)
# ---------------------------------------------------------------------------------------------------------------------------
class GenChain:
    def __init__(self, joints, group):
        self.joints = joints
        self.group = group
        self.theta = 0.


GENERATED = {'GEN_RavenBack': ('HairBack.Bank_RavenSpikes', 'raven_back'), 'GEN_RavenFront': ('HairFront.Bank_RavenBangs', 'raven_front'),
             'GEN_HeroBack': ('HairBack.Bank_HeroSpikes', 'hero_back'), 'GEN_HeroFront': ('HairFront.Bank_HeroBangs', 'hero_front')}


def generated_piece(name, head, cap):
    import hairgen
    renderer, maker = GENERATED[name]
    locks = getattr(hairgen, maker)(head, cap.v)
    V, U, T, chains, bi3, bw2 = hairgen.build(locks, name)
    n = len(V)
    # bones: each chain its own group (its own cloth), kinds from the locks
    names, parents, local, base = [], [], [], [0]
    short = renderer.split('Bank_', 1)[1]
    for k, (kind, joints) in enumerate(chains):
        base.append(len(names) + 1)
        for j, q in enumerate(joints):
            names.append('Hair_B%s%02d_%s_%02d_%d' % (kind, k + 1, short, k, j))
            parents.append(-1 if j == 0 else len(names) - 2)
            local.append(q if j == 0 else q - joints[j - 1])
    bi = np.zeros((n, 4), np.int64)
    bw = np.zeros((n, 4))
    roots = np.zeros((n, 3))
    lock_of = np.zeros(n, np.int64)
    off = 0
    for li, lk in enumerate(locks):
        m = len(hairgen.lock_mesh(lk)[0])
        roots[off:off + m] = lk.points[0]
        lock_of[off:off + m] = li
        off += m
    for i in range(n):
        c, j0, j1 = bi3[i]
        if c == 0:
            bi[i, 0], bw[i, 0] = 0, 1
        else:
            bi[i, 0], bw[i, 0] = base[c] + j0, bw2[i][0]
            bi[i, 1], bw[i, 1] = base[c] + j1, bw2[i][1]
    # tag shapes: under a beanie the spikes fold in toward their roots (the bangs stay), under a helmet or a hood every lock does
    fold = roots + (V - roots) * .18 - V
    is_front = renderer.startswith('HairFront.')
    shapes = [('Tag_Hat', np.zeros_like(V) if is_front else fold), ('Tag_Helm', fold), ('Tag_Hood', fold)]
    p = Piece.__new__(Piece)
    p.name, p.v, p.n, p.uv, p.c, p.tris, p.shapes, p.front = name, V, hairgen.normals(V, T), U, np.tile([1., 0, 0, 1], (n, 1)), T, shapes, is_front
    gchains = [GenChain(j, k + 1) for k, (_, j) in enumerate(chains)]
    return renderer, p, gchains, (bi, bw), (names, parents, local)


def renderer_name(piece_name):
    side, short = piece_name.split('_', 1)
    return '%s.Bank_%s' % (side, short)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--dump', required=True)
    ap.add_argument('--out', required=True, help='the project Assets/LocalAssets folder')
    ap.add_argument('--preview', help='writes each piece rest and swung as OBJ with its chains, for preview renders')
    ap.add_argument('--only', help='style ids')
    ap.add_argument('--pack', help='the LocalAssets folder with the pack (default: --out)')
    a = ap.parse_args()
    head = Head(a.dump)
    only = set(a.only.split(',')) if a.only else None
    styles = [s for s in STYLES if not only or s[0] in only]
    pack_root = os.path.join(a.pack or a.out, PACK)
    pieces, index, textures, tex_index = [], {}, [], {}
    report = []
    for sid, parts in styles:
        for name, kind in parts:
            key = (name, kind)
            if key in index:
                continue
            if name in GENERATED:
                cap = load_piece(a.dump, 'HairBack_ShotaCut')
                rname, p, chains, (bi, bw), names = generated_piece(name, head, cap)
                tname = 'generated-gradient'
                if tname not in tex_index:
                    import hairgen
                    tex_index[tname] = len(textures)
                    textures.append((tname, hairgen.gradient_png()))
                uv = p.uv
                info = dict(loose=np.ones(len(p.v)))
            else:
                p = load_piece(a.dump, name)
                chains, (bi, bw), info = rig(p, head, kind)
                tname, png, uv = texture_block(pack_root, p)
                if tname not in tex_index:
                    tex_index[tname] = len(textures)
                    textures.append((tname, png))
                names = bone_names(name, kind, chains)
                rname = renderer_name(name) + ('' if kind in (LOCK, RIGID) or name not in [k[0] for k in index] else '_' + kind)
            rec = dict(renderer=rname, texture=tex_index[tname], cutoff=.5, cull=2, bones=names, v=p.v, n=p.n, uv=uv, c=p.c,
                       bi=bi, bw=bw, tris=p.tris, shapes=p.shapes)
            index[key] = len(pieces)
            pieces.append(rec)
            report.append('%-30s %s chains %2d joints %3d groups %d loose %4d/%4d' % (name, kind, len(chains), len(names[0]),
                          len(set(c.group for c in chains)), int((info['loose'] > .5).sum()), len(p.v)))
            if a.preview:
                preview(a.preview, name, p, chains, bi, bw)
    style_rows = [(sid, [index[(n, k)] for n, k in parts]) for sid, parts in styles]
    path = os.path.join(a.out, RESOURCES, BANK + '.bytes')
    size = write_bank(path, pieces, style_rows, textures)
    print('\n'.join(report))
    print('bank %s: %d styles, %d pieces, %d textures, %d bytes' % (path, len(style_rows), len(pieces), len(textures), size))


# ---------------------------------------------------------------------------------------------------------------------------
# Preview: rest and swung OBJs (each chain's joints turned back and down by 35 degrees spread along it), and the chains
# ---------------------------------------------------------------------------------------------------------------------------
def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def preview(out, name, p, chains, bi, bw):
    os.makedirs(out, exist_ok=True)
    # bone matrices: head identity; each chain's joint j turned about its root by (j/J) * the swing
    mats = [np.eye(4)]
    for ch in chains:
        J = len(ch.joints) - 1
        for j, q in enumerate(ch.joints):
            ang = .6 * (j / max(J, 1))
            R = rot_y(.5 * ang) @ rot_x(-ang)
            root = ch.joints[0]
            M = np.eye(4)
            M[:3, :3] = R
            M[:3, 3] = root - R @ root
            mats.append(M)
    pos = np.zeros_like(p.v)
    for k in range(4):
        w = bw[:, k][:, None]
        Ms = np.array([mats[b] for b in bi[:, k]])
        pos += w * (np.einsum('nij,nj->ni', Ms[:, :3, :3], p.v) + Ms[:, :3, 3])
    of_chain = [0]
    for k, ch in enumerate(chains):
        of_chain += [k + 1] * len(ch.joints)
    of_chain = np.array(of_chain)
    dom = of_chain[bi[np.arange(len(bi)), np.argmax(bw, 1)]]
    loose_w = 1 - np.where(bi == 0, bw, 0).sum(1)
    for tag, P in (('rest', p.v), ('swing', pos)):
        with open(os.path.join(out, '%s_%s.obj' % (name, tag)), 'w') as fh:
            for q, b in zip(P, dom):
                h = (b * 0.618) % 1
                fh.write('v %.5f %.5f %.5f %.3f %.3f %.3f\n' % (q[0], q[1], q[2], *hsv(h if b else 0, .6 if b else 0, .9)))
            for t in p.tris:
                fh.write('f %d %d %d\n' % (t[0] + 1, t[1] + 1, t[2] + 1))
    with open(os.path.join(out, '%s_chains.txt' % name), 'w') as fh:
        for ch in chains:
            fh.write('chain %d %.3f %s\n' % (ch.group, getattr(ch, 'theta', 0.), ' '.join('%.4f,%.4f,%.4f' % tuple(q) for q in ch.joints)))


def hsv(h, s, v):
    i = int(h * 6) % 6
    f = h * 6 - int(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    return [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)][i]


if __name__ == '__main__':
    main()
