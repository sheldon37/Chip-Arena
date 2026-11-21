"""Top-down plan PNGs, readable on a phone: tiers by tone, buildings by height, routes, doors, roofs."""
import math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Polygon, Circle, FancyBboxPatch
from matplotlib.collections import PatchCollection, LineCollection
import matplotlib.patheffects as pe
import numpy as np

TIER_STOPS = [(-21, '#16202b'), (-14, '#22303f'), (-7, '#304356'), (0, '#45596b'), (7, '#5f7180'),
              (14, '#7d8a8d'), (21, '#9fa194'), (28, '#bdb69c'), (35, '#d6caa6')]
BLD_STOPS = [(7, '#c98a6a'), (10, '#b5735a'), (13, '#a0604b'), (16, '#8a4e3d'), (19, '#743f31'), (22, '#5e3126'),
             (31, '#3d1f18')]
PAPER = '#0f141a'
INK = '#f3ede2'


def _interp(stops, v):
    if v <= stops[0][0]: return stops[0][1]
    for (a, ca), (b, cb) in zip(stops, stops[1:]):
        if v <= b:
            f = (v - a) / (b - a)
            A = np.array(matplotlib.colors.to_rgb(ca)); B = np.array(matplotlib.colors.to_rgb(cb))
            return tuple(A + (B - A) * f)
    return stops[-1][1]


def tier_color(y): return _interp(TIER_STOPS, y)
def bld_color(h): return _interp(BLD_STOPS, h)


def halo(lw=4, c=PAPER): return [pe.withStroke(linewidth=lw, foreground=c)]


def draw_map(ax, L, labels=True, lw_scale=1.0, small=False):
    ax.set_facecolor(PAPER)
    # ground tiers
    pats, cols = [], []
    for g in L.ground_boxes:
        pats.append(Rectangle((g['x0'], g['z0']), g['x1'] - g['x0'], g['z1'] - g['z0']))
        cols.append(tier_color(g['top']))
    ax.add_collection(PatchCollection(pats, facecolors=cols, edgecolors=cols, linewidths=0.3, zorder=1))
    # terrace edges (retaining walls): draw where the ground steps by 2 m or more
    g = L.ground
    segs = []
    dz = np.abs(np.diff(g, axis=0)) >= 2
    for iz, ix in zip(*np.nonzero(dz)):
        segs.append([(L.minX + ix, L.minZ + iz + 1), (L.minX + ix + 1, L.minZ + iz + 1)])
    dx = np.abs(np.diff(g, axis=1)) >= 2
    for iz, ix in zip(*np.nonzero(dx)):
        segs.append([(L.minX + ix + 1, L.minZ + iz), (L.minX + ix + 1, L.minZ + iz + 1)])
    ax.add_collection(LineCollection(segs, colors='#0b0e12', linewidths=1.6 * lw_scale, zorder=2))
    # hub precinct
    for r in L.hub_rects:
        ax.add_patch(Rectangle((r[0], r[1]), r[2] - r[0], r[3] - r[1], facecolor='#3f6a4d', edgecolor='#88c49a',
                               linewidth=1.2 * lw_scale, linestyle='--', zorder=3, alpha=0.9))
    ax.add_patch(Rectangle((-25, -25), 50, 50, facecolor='#4d8a5e', edgecolor='#d8f5dc', linewidth=2.2 * lw_scale, zorder=4))
    if labels:
        ax.text(0, 0, 'CAMP', ha='center', va='center', fontsize=15 if not small else 7, color='#f4fff5', weight='bold', zorder=20,
                path_effects=halo(3, '#22442c'))
    # ramps and stairs
    for rp in L.ramps:
        x0, z0, x1, z1 = rp['x0'], rp['z0'], rp['x1'], rp['z1']
        c = tier_color((rp['ylo'] + rp['yhi']) / 2)
        ax.add_patch(Rectangle((x0, z0), x1 - x0, z1 - z0, facecolor=c, edgecolor='#e9dfc8', linewidth=0.9 * lw_scale, zorder=5))
        t = rp['toward']
        if rp['kind'] == 'stair':
            n = max(2, int((rp['run']) / 1.6))
            ls = []
            for k in range(1, n):
                f = k / n
                if t in 'NS':
                    z = z0 + (z1 - z0) * f; ls.append([(x0, z), (x1, z)])
                else:
                    x = x0 + (x1 - x0) * f; ls.append([(x, z0), (x, z1)])
            ax.add_collection(LineCollection(ls, colors='#efe6d2', linewidths=0.7 * lw_scale, zorder=6))
        else:
            # chevrons pointing uphill
            n = max(1, int(rp['run'] / 9))
            for k in range(n):
                f = (k + 0.5) / n
                if t in 'NS':
                    zc = z0 + (z1 - z0) * f; s = 1 if t == 'N' else -1; hw = (x1 - x0) * 0.32
                    ax.plot([x0 + (x1 - x0) / 2 - hw, (x0 + x1) / 2, (x0 + x1) / 2 + hw], [zc - 2 * s, zc + 1.5 * s, zc - 2 * s],
                            color='#efe6d2', lw=1.1 * lw_scale, zorder=6, solid_capstyle='round')
                else:
                    xc = x0 + (x1 - x0) * f; s = 1 if t == 'E' else -1; hw = (z1 - z0) * 0.32
                    ax.plot([xc - 2 * s, xc + 1.5 * s, xc - 2 * s], [(z0 + z1) / 2 - hw, (z0 + z1) / 2, (z0 + z1) / 2 + hw],
                            color='#efe6d2', lw=1.1 * lw_scale, zorder=6, solid_capstyle='round')
    # blocks
    on_foot = getattr(L, 'roofs_on_foot', set())
    order = sorted(L.blocks, key=lambda b: b['top'])
    for b in order:
        role = b['role']
        if role == 'cordon': continue
        x0, z0, w, d = b['x0'], b['z0'], b['x1'] - b['x0'], b['z1'] - b['z0']
        h = b['top'] - b['bottom']
        if role in ('lot', 'hall', 'city-hall', 'tunnel-roof', 'landmark', 'shed', 'tower', 'lid'):
            if role == 'city-hall': fc, ec = '#efe2c2', '#1b1410'
            elif role == 'tower': fc, ec = '#fff1b8', '#1b1410'
            elif role == 'hall': fc, ec = '#d9cfc0', '#1b1410'
            elif role in ('tunnel-roof', 'lid'): fc, ec = '#8f857a', '#e9dfc8'
            elif role == 'landmark': fc, ec = '#e6d6a8', '#1b1410'
            else: fc, ec = bld_color(h), '#1b1410'
            ax.add_patch(Rectangle((x0, z0), w, d, facecolor=fc, edgecolor=ec, linewidth=0.8 * lw_scale, zorder=8))
            if b.get('roof') == 'pitched' and role == 'lot' and not small:
                if w >= d: ax.plot([x0 + 1, x0 + w - 1], [z0 + d / 2] * 2, color='#e7c3a8', lw=0.6, zorder=9, alpha=0.8)
                else: ax.plot([x0 + w / 2] * 2, [z0 + 1, z0 + d - 1], color='#e7c3a8', lw=0.6, zorder=9, alpha=0.8)
            if b.get('roof') == 'walkable' and role in ('lot', 'hall', 'city-hall', 'tunnel-roof', 'lid'):
                reached = b['id'] in on_foot
                ins = min(1.2, w / 6, d / 6)
                ax.add_patch(Rectangle((x0 + ins, z0 + ins), w - 2 * ins, d - 2 * ins, facecolor='none',
                                       edgecolor='#ffd257' if reached else '#c9a85a', linewidth=(1.6 if reached else 0.8) * lw_scale,
                                       linestyle='-' if reached else (0, (2, 2)), zorder=9))
        elif role in ('bridge', 'deck', 'viaduct'):
            ax.add_patch(Rectangle((x0, z0), w, d, facecolor='#d8d0c2' if role != 'viaduct' else '#b9b0a3', edgecolor='#14110e',
                                   linewidth=1.0 * lw_scale, zorder=10))
        elif role == 'arcade-roof':
            ax.add_patch(Rectangle((x0, z0), w, d, facecolor='#c79f72', edgecolor='#ffd257' if b['id'] in on_foot else '#2a1d12',
                                   linewidth=1.0 * lw_scale, zorder=10, alpha=0.92))
        elif role in ('court-wall', 'court-gate', 'wall', 'parapet', 'pier', 'post', 'cover'):
            c = '#0a0a0a' if role != 'court-gate' else '#f1d27a'
            ax.add_patch(Rectangle((x0, z0), max(w, 0.8), max(d, 0.8), facecolor=c, edgecolor=c, linewidth=0.4, zorder=11))
        elif role == 'wagon':
            ax.add_patch(Rectangle((x0, z0), w, d, facecolor='#6f6a5e', edgecolor='#14110e', linewidth=0.5, zorder=8))
        elif role == 'glass':
            ax.add_patch(Rectangle((x0, z0), w, d, facecolor='#9fd6e8', edgecolor='#e8fbff', linewidth=0.8, zorder=10, alpha=0.35))
        else:
            ax.add_patch(Rectangle((x0, z0), w, d, facecolor='#9a9a9a', edgecolor='#111', linewidth=0.5, zorder=8))
    # court floor tint
    if L.court:
        c = L.court
        ax.add_patch(Rectangle((c['x0'], c['z0']), c['x1'] - c['x0'], c['z1'] - c['z0'], facecolor='#f6e7b0', edgecolor='none', alpha=0.18, zorder=7))
    # fire escapes and other pilot links
    for lk in L.links:
        a, b = lk['a'], lk['b']
        if lk['kind'] == 'fire-escape':
            n = 6
            xs = np.linspace(a['x'], b['x'], n * 2 + 1); zs = np.linspace(a['z'], b['z'], n * 2 + 1)
            dxv, dzv = b['x'] - a['x'], b['z'] - a['z']; Ln = math.hypot(dxv, dzv) or 1
            px, pz = -dzv / Ln * 0.9, dxv / Ln * 0.9
            pts = [(x + (px if k % 2 else -px), z + (pz if k % 2 else -pz)) for k, (x, z) in enumerate(zip(xs, zs))]
            ax.plot([p[0] for p in pts], [p[1] for p in pts], color='#ffd257', lw=0.9 * lw_scale, zorder=12)
    # routes
    for r in L.routes:
        xs = [p[0] for p in r.pts]; zs = [p[1] for p in r.pts]
        ax.plot(xs, zs, color=r.color, lw=6.5 * lw_scale, alpha=0.9, zorder=14, solid_joinstyle='round', solid_capstyle='round',
                path_effects=[pe.withStroke(linewidth=9.5 * lw_scale, foreground='#0a0d10')])
        # direction ticks every 40 m
        for (x, z, y, st) in r.samples(40.0)[1:-1]:
            ax.add_patch(Circle((x, z), 1.6 * lw_scale, facecolor='#0a0d10', edgecolor='none', zorder=15))
    # gates
    from meridian_plan import GATES
    for r in L.routes:
        gx, gz = GATES[r.gate]
        if labels:
            ax.add_patch(Circle((gx, gz), 7.5, facecolor=r.color, edgecolor='#0a0d10', linewidth=2, zorder=18))
            ax.text(gx, gz - 0.3, r.gate, ha='center', va='center', fontsize=13 if not small else 6, weight='bold', color='#0a0d10', zorder=19)
    # doors and court
    for d in L.doors:
        col = next(r.color for r in L.routes if r.dest == 'door-' + d['id'])
        mk = {'ground': 's', 'raised': '^', 'sunk': 'v'}[d['level']]
        ax.plot([d['x']], [d['z']], marker=mk, markersize=22 if not small else 9, color=col, markeredgecolor='#0a0d10',
                markeredgewidth=2.2, zorder=21)
    if L.court:
        gw = L.court['gateway']
        ax.plot([gw['x']], [gw['z']], marker='*', markersize=30 if not small else 12, color=GOLD_, markeredgecolor='#0a0d10',
                markeredgewidth=2, zorder=21)
    # landmarks
    if labels:
        for k, lm in enumerate(L.landmarks):
            ax.add_patch(Circle((lm['x'], lm['z']), 6.5, facecolor='#f3ede2', edgecolor='#0a0d10', linewidth=1.6, zorder=22))
            ax.text(lm['x'], lm['z'] - 0.2, str(k + 1), ha='center', va='center', fontsize=10, weight='bold', color='#0a0d10', zorder=23)
    ax.add_patch(Rectangle((L.minX, L.minZ), 600, 480, facecolor='none', edgecolor='#d9d9d9', linewidth=3 * lw_scale, zorder=25))
    ax.set_xlim(L.minX - 4, L.maxX + 4); ax.set_ylim(L.minZ - 4, L.maxZ + 4)
    ax.set_aspect('equal'); ax.set_xticks([]); ax.set_yticks([])
    for s in ax.spines.values(): s.set_visible(False)


GOLD_ = '#ffc83d'


def dest_text(L, r):
    if r.dest == 'court':
        return 'FINAL COURT  %+d m' % L.court['y']
    d = next(d for d in L.doors if 'door-' + d['id'] == r.dest)
    return '%s  %s %+d m' % (r.label, d['level'].upper(), d['y'])


def draw_tier_labels(ax, L):
    seen = {}
    for gb in sorted(L.ground_boxes, key=lambda g: -(g['x1'] - g['x0']) * (g['z1'] - g['z0'])):
        y = gb['top']
        area = (gb['x1'] - gb['x0']) * (gb['z1'] - gb['z0'])
        if area < 1500 or seen.get(y, 0) >= 2: continue
        cx, cz = (gb['x0'] + gb['x1']) / 2, (gb['z0'] + gb['z1']) / 2
        if any(math.hypot(cx - a, cz - b) < 60 for (a, b) in seen.get(('pts', y), [])): continue
        seen[y] = seen.get(y, 0) + 1
        seen.setdefault(('pts', y), []).append((cx, cz))
        ax.text(cx, cz, '%+d' % y if y else '0', ha='center', va='center', fontsize=12, color='#e8eef2', alpha=0.85, zorder=13,
                style='italic', path_effects=halo(3))


def plan_png(L, path):
    fig = plt.figure(figsize=(16, 23), dpi=110, facecolor=PAPER)
    ax = fig.add_axes([0.02, 0.385, 0.96, 0.545])
    draw_map(ax, L)
    draw_tier_labels(ax, L)
    # destination labels near the ends
    for r in L.routes:
        x, z, y = r.pts[-1]
        ox = -70 if x > L.maxX - 120 else 10 if x < L.minX + 120 else -30
        oz = 14 if z < L.maxZ - 40 else -22
        ax.text(x + ox, z + oz, dest_text(L, r), fontsize=12.5, weight='bold', color=r.color, zorder=30,
                path_effects=halo(4))
    # scale bar and north
    sx, sz = L.minX + 16, L.minZ + 14
    ax.add_patch(Rectangle((sx, sz), 100, 4, facecolor=INK, edgecolor=PAPER, zorder=40))
    ax.add_patch(Rectangle((sx + 50, sz), 50, 4, facecolor=PAPER, edgecolor=INK, zorder=41))
    ax.text(sx, sz + 9, '0', color=INK, fontsize=11, zorder=42, path_effects=halo(3)); ax.text(sx + 92, sz + 9, '100 m', color=INK, fontsize=11, zorder=42, path_effects=halo(3))
    nx, nz = L.maxX - 22, L.maxZ - 40
    ax.add_patch(Polygon([(nx, nz + 26), (nx - 8, nz), (nx, nz + 6), (nx + 8, nz)], closed=True, facecolor=INK, edgecolor=PAPER, zorder=40))
    ax.text(nx, nz - 9, 'N', ha='center', va='center', color=INK, fontsize=16, weight='bold', zorder=40, path_effects=halo(3))
    # title
    fig.text(0.02, 0.977, '%s' % L.name.upper(), fontsize=34, weight='bold', color=INK)
    fig.text(0.02, 0.959, L.title + '  ·  600 × 480 m  ·  hub: ' + L.hub_name, fontsize=13.5, color='#c9c2b5')
    rr = L.reach_report
    fig.text(0.02, 0.945, 'Routes gate to door: ' + ',  '.join('%s %d m' % (r.gate, round(r.length)) for r in L.routes) +
             '   ·   %d lots, %d walkable roofs (%d on foot from the camp)' % (L.lot_count, rr['walkable_roofs'], rr['roofs_on_foot']),
             fontsize=12.5, color='#c9c2b5')
    # elevation profiles
    ax2 = fig.add_axes([0.07, 0.235, 0.6, 0.12], facecolor='#151b22')
    ys = []
    for r in L.routes:
        pr = r.profile(step=2.0)
        ax2.plot([p[0] for p in pr], [p[1] for p in pr], color=r.color, lw=3.2, label=r.gate + ' · ' + dest_text(L, r))
        ys += [p[1] for p in pr]
    ax2.axvspan(0, 60, color='#ffffff', alpha=0.06)
    ax2.text(30, max(ys) + 3, 'first 60 m', ha='center', color='#9aa3ab', fontsize=10)
    ax2.set_xlim(0, 305); ax2.set_ylim(min(ys) - 4, max(ys) + 7)
    ax2.set_xlabel('metres walked from the gate', color=INK, fontsize=11); ax2.set_ylabel('height (m)', color=INK, fontsize=11)
    ax2.tick_params(colors=INK, labelsize=10)
    for s in ax2.spines.values(): s.set_color('#566270')
    ax2.grid(color='#2a333d', lw=0.8)
    ax2.legend(loc='upper center', bbox_to_anchor=(0.5, -0.32), ncol=2, frameon=False, fontsize=11.5, labelcolor=INK)
    ax2.set_title('Height along each route', color=INK, fontsize=13, loc='left')
    # legend
    lx = 0.71
    fig.text(lx, 0.36, 'KEY', color=INK, fontsize=14, weight='bold')
    items = [('s', 'door at street level (ground)'), ('^', 'door raised on a terrace or roof'), ('v', 'door sunk in a cut'),
             ('*', 'final court gateway')]
    for k, (m, t) in enumerate(items):
        yy = 0.345 - k * 0.0145
        fig.lines.append(matplotlib.lines.Line2D([lx + 0.01], [yy + 0.004], marker=m, markersize=13, color='#e8e1d3',
                                                 markeredgecolor='#000', transform=fig.transFigure, figure=fig))
        fig.text(lx + 0.025, yy, t, color=INK, fontsize=11.5)
    yy = 0.345 - 4 * 0.0145
    for t, c, ls in (('walkable roof, reached on foot', '#ffd257', '-'), ('walkable roof, chips or a drop only', '#c9a85a', ':')):
        fig.patches.append(Rectangle((lx + 0.003, yy - 0.001), 0.016, 0.009, transform=fig.transFigure, facecolor='none', edgecolor=c,
                                     linewidth=2, linestyle=ls, figure=fig))
        fig.text(lx + 0.025, yy, t, color=INK, fontsize=11.5); yy -= 0.0145
    zx = [lx + 0.003 + 0.002 * k for k in range(9)]; zy = [yy + 0.004 + (0.003 if k % 2 else -0.003) for k in range(9)]
    fig.lines.append(matplotlib.lines.Line2D(zx, zy, color='#ffd257', lw=1.6, transform=fig.transFigure, figure=fig))
    fig.text(lx + 0.025, yy, 'fire escape (pilot route)', color=INK, fontsize=11.5); yy -= 0.0145
    fig.patches.append(Rectangle((lx + 0.003, yy - 0.001), 0.016, 0.009, transform=fig.transFigure, facecolor='#d8d0c2', edgecolor='#000', figure=fig))
    fig.text(lx + 0.025, yy, 'bridge, deck or arcade roof', color=INK, fontsize=11.5); yy -= 0.0145
    fig.text(lx + 0.003, yy, '>>  ramp (points uphill)    ≡  stair', color=INK, fontsize=11.5); yy -= 0.0145
    fig.text(lx + 0.003, yy, '—  dark line: terrace wall (2 m or more)', color=INK, fontsize=11.5); yy -= 0.02
    # tier and building ramps
    fig.text(lx, yy, 'ground height', color=INK, fontsize=11.5); yy -= 0.014
    tiers = sorted(set(g['top'] for g in L.ground_boxes))
    sw = min(0.035, 0.27 / max(1, len(tiers)))
    for k, t in enumerate(tiers):
        fig.patches.append(Rectangle((lx + k * sw, yy - 0.003), sw - 0.002, 0.011, transform=fig.transFigure, facecolor=tier_color(t), edgecolor='#000', figure=fig))
        fig.text(lx + k * sw + sw / 2, yy - 0.012, ('%+g' % t) if t else '0', color=INK, fontsize=9 if sw < 0.034 else 9.5, ha='center')
    yy -= 0.028
    fig.text(lx, yy, 'building height', color=INK, fontsize=11.5); yy -= 0.014
    for k, h in enumerate((7, 10, 13, 16, 19, 22, 31)):
        fig.patches.append(Rectangle((lx + k * 0.035, yy - 0.003), 0.033, 0.011, transform=fig.transFigure, facecolor=bld_color(h), edgecolor='#000', figure=fig))
        fig.text(lx + k * 0.035 + 0.0165, yy - 0.012, '%d m' % h if h < 31 else '25+', color=INK, fontsize=9.5, ha='center')
    # landmarks list + description
    fig.text(0.02, 0.165, 'LANDMARKS', color=INK, fontsize=14, weight='bold')
    lm = L.landmarks
    half = (len(lm) + 1) // 2
    for k, m in enumerate(lm):
        col, row = (0, k) if k < half else (1, k - half)
        fig.text(0.02 + col * 0.36, 0.148 - row * 0.0145, '%d  %s' % (k + 1, m['name']), color=INK, fontsize=12)
    import textwrap
    fig.text(0.02, 0.148 - half * 0.0145 - 0.008, '\n'.join(textwrap.wrap(L.description, 128)), color='#d5cfc3', fontsize=13,
             va='top', linespacing=1.4)
    fig.savefig(path, facecolor=PAPER)
    plt.close(fig)
