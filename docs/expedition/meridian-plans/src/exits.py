"""The comparison sheet (five plans side by side, each with its four exits) and the 20-exit difference check."""
import itertools, json, math, os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Polygon
import matplotlib.patheffects as pe
from meridian_plan import DIRS, LEFT, RIGHT, BACK
from render_plan import draw_map, PAPER, INK, halo, dest_text
from layouts import ALL

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SPAN = 60.0

EXIT_NAMES = {
    ('crown-hill', 'N'): 'The Hundred Steps', ('crown-hill', 'E'): 'The Promenade',
    ('crown-hill', 'S'): 'The Chute', ('crown-hill', 'W'): 'The Hill Cutting',
    ('navigation', 'N'): 'The Iron Bridge', ('navigation', 'E'): 'The Lock ramp',
    ('navigation', 'S'): 'The Low Causeway', ('navigation', 'W'): 'Mill Lane dog-leg and stair',
    ('arcades', 'N'): 'The Galleria', ('arcades', 'E'): 'Arcade turn to the Shambles',
    ('arcades', 'S'): 'Arcade jog into the Undercroft', ('arcades', 'W'): 'The stair tower to the Leads',
    ('stride', 'N'): 'Arch Nought and Crag Road', ('stride', 'E'): 'The deck stair',
    ('stride', 'S'): 'The Vale haul ramp', ('stride', 'W'): 'The Valley Steps',
    ('yards', 'N'): 'The steel ramp to the Gantry', ('yards', 'E'): 'The sidings incline',
    ('yards', 'S'): 'The covered footbridge', ('yards', 'W'): 'The High Yard haul ramp',
}


def first60(r):
    """Segments clipped to the first 60 m, with headings relative to the gate's heading."""
    out, s = [], 0.0
    g = r.segs[0]['heading']
    rel = {g: 'U', RIGHT[g]: 'R', BACK[g]: 'D', LEFT[g]: 'L'}
    for seg in r.segs:
        if s >= SPAN: break
        L = min(seg['length'], SPAN - s)
        f = L / seg['length']
        out.append(dict(seg, length=L, rel=rel[seg['heading']], y1=seg['y0'] + (seg['y1'] - seg['y0']) * f, s0=s))
        s += L
    return out


COVERED = {'galleria', 'arcade', 'undercroft', 'under the arch', 'covered bridge', 'covered footbridge', 'goods shed',
           'jettied lane', 'stair tower'}


def describe(r):
    """Plain words for the first 60 m: stair flights and their landings merge into one climb."""
    segs = first60(r)
    parts = []
    i = 0
    while i < len(segs):
        seg = segs[i]; k = seg['kind']
        turn = ''
        if i > 0 and segs[i - 1]['heading'] != seg['heading']:
            h0, h1 = segs[i - 1]['heading'], seg['heading']
            turn = 'turn %s, ' % ('left' if LEFT[h0] == h1 else 'right' if RIGHT[h0] == h1 else 'back')
        if k in ('stair', 'ramp'):
            dy = seg['y1'] - seg['y0']; run = seg['length']; j = i + 1
            while j + 1 < len(segs) + 1 and j < len(segs):
                nx = segs[j]
                if nx['kind'] == 'flat' and nx['length'] <= 3.01 and j + 1 < len(segs) and segs[j + 1]['kind'] == k and                         (segs[j + 1]['y1'] - segs[j + 1]['y0']) * dy > 0:
                    dy += segs[j + 1]['y1'] - segs[j + 1]['y0']; run += nx['length'] + segs[j + 1]['length']; j += 2
                else: break
            word = 'stairs' if k == 'stair' else 'ramp 1:%d' % round(seg['length'] / max(0.01, abs(seg['y1'] - seg['y0'])))
            parts.append('%s%s %s%g m' % (turn, word, '+' if dy > 0 else '', round(dy, 1)))
            i = j; continue
        L = seg['length']; j = i + 1
        while j < len(segs) and segs[j]['kind'] == k and segs[j]['rel'] == seg['rel']: L += segs[j]['length']; j += 1
        name = {'bridge': 'bridge', 'roof': 'roofs'}.get(k, 'level')
        parts.append('%s%s %d m' % (turn, name, round(L)))
        i = j
    return ' > '.join(parts)


def signature(r):
    f = first60(r)
    ex = r.exit
    turns = tuple(t for _, t in ex['turns'])
    ks = [s['kind'] + ('+' if s['y1'] > s['y0'] else '-' if s['y1'] < s['y0'] else '') for s in f]
    kinds = tuple(k for i, k in enumerate(ks) if i == 0 or ks[i - 1] != k)
    covers = tuple(sorted(set(s['cover'] for s in f)))
    widths = [s['width'] for s in f]
    wmain = max(set(widths), key=lambda w: sum(s['length'] for s in f if s['width'] == w))
    return dict(turns=turns, turn_at=[t for t, _ in ex['turns']], kinds=kinds, covers=covers, width=wmain,
                profile=ex['profile'], dy=ex['dy60'], heading=ex['heading_first'])


def compare(a, b):
    pa, pb = a['profile'], b['profile']
    n = min(len(pa), len(pb))
    rms = math.sqrt(sum((pa[i] - pb[i]) ** 2 for i in range(n)) / n)
    diff = {
        'level': rms >= 1.5,
        'turns': a['turns'] != b['turns'] or any(abs(x - y) > 10 for x, y in zip(a['turn_at'], b['turn_at'])),
        'width': abs(a['width'] - b['width']) >= 3,
        'cover': a['covers'] != b['covers'],
        'approach': a['kinds'] != b['kinds'],
    }
    return rms, diff


def run():
    Ls = []
    for fn in ALL:
        L = fn(); L.finalize(); L.unpocket(); L.reach()
        Ls.append(L)
    exits = []
    for L in Ls:
        slug = L.id.replace('meridian-b-', '')
        for r in L.routes:
            exits.append(dict(layout=slug, name=L.name, gate=r.gate, route=r, sig=signature(r),
                              title=EXIT_NAMES[(slug, r.gate)], text=describe(r)))
    # pairwise check
    rows, worst = [], None
    for a, b in itertools.combinations(exits, 2):
        rms, d = compare(a['sig'], b['sig'])
        n = sum(d.values())
        rows.append((n, rms, a['layout'] + ':' + a['gate'], b['layout'] + ':' + b['gate'], [k for k, v in d.items() if v]))
        if worst is None or (n, rms) < (worst[0], worst[1]): worst = rows[-1]
    rows.sort()
    fails = [r for r in rows if r[0] < 2]
    report = dict(pairs=len(rows), rule='every pair of the 20 exits differs in at least 2 of: level profile (RMS >= 1.5 m over '
                  'the first 60 m), turn pattern, main width (>= 3 m), cover, approach kinds', failures=[list(r) for r in fails],
                  closest=[list(r) for r in rows[:8]],
                  same_template=[], exits=[dict(layout=e['layout'], gate=e['gate'], title=e['title'], first60=e['text'],
                                                width=e['sig']['width'], covers=list(e['sig']['covers']),
                                                turns=list(e['sig']['turns']), dy60=e['sig']['dy']) for e in exits])
    # "no shared template": no two exits share the same kinds sequence AND turn pattern
    seen = {}
    for e in exits:
        key = (e['sig']['kinds'], e['sig']['turns'])
        seen.setdefault(key, []).append(e['layout'] + ':' + e['gate'])
    report['same_template'] = [v for v in seen.values() if len(v) > 1]
    with open(os.path.join(OUT, 'exit-check.json'), 'w') as f: json.dump(report, f, indent=1)
    print('pairs', len(rows), 'failures', len(fails), 'closest', rows[:5])
    print('shared kinds+turns', report['same_template'])
    sheet(Ls, exits)
    return report


def draw_exit(ax, e):
    """Top-down of the first 60 m, rotated so the exit leaves the gate upward; colour by kind."""
    ax.set_facecolor('#151b22')
    x, z = 0.0, 0.0
    dirs = {'U': (0, 1), 'R': (1, 0), 'D': (0, -1), 'L': (-1, 0)}
    for seg in first60(e['route']):
        dx, dz = dirs[seg['rel']]
        L, w = seg['length'], seg['width']
        x1, z1 = x + dx * L, z + dz * L
        if dx == 0: rect = (x - w / 2, min(z, z1), w, abs(z1 - z))
        else: rect = (min(x, x1), z - w / 2, abs(x1 - x), w)
        dy = seg['y1'] - seg['y0']
        k = seg['kind']
        col = {'flat': '#6d7883', 'bridge': '#d9d0c0', 'roof': '#d7a35a'}.get(k, '#e07a4f' if dy > 0 else '#4f8fd1')
        ax.add_patch(Rectangle(rect[:2], rect[2], rect[3], facecolor=col, edgecolor='#0b0e12', lw=0.8))
        if k == 'stair':
            n = int(L / 1.5)
            for i in range(1, n):
                t = i / n
                if dx == 0: ax.plot([x - w / 2, x + w / 2], [z + dz * L * t] * 2, color='#0b0e12', lw=0.6)
                else: ax.plot([x + dx * L * t] * 2, [z - w / 2, z + w / 2], color='#0b0e12', lw=0.6)
        if seg['cover'] in COVERED:
            ax.add_patch(Rectangle(rect[:2], rect[2], rect[3], facecolor='none', edgecolor='#f3ede2', hatch='////', lw=0))
        x, z = x1, z1
    ax.add_patch(Rectangle((-9, -2.5), 18, 2.5, facecolor='#4d8a5e', edgecolor='#d8f5dc', lw=1))
    ax.set_xlim(-34, 34); ax.set_ylim(-6, 64); ax.set_aspect('equal')
    ax.set_xticks([]); ax.set_yticks([])
    for s in ax.spines.values(): s.set_color('#3a4450')


def draw_profile(ax, e):
    ax.set_facecolor('#151b22')
    pr = e['route'].profile(SPAN, 1.0)
    ax.fill_between([p[0] for p in pr], [p[1] for p in pr], -16, color=e['route'].color, alpha=0.14, lw=0)
    ax.plot([p[0] for p in pr], [p[1] for p in pr], color=e['route'].color, lw=2.6)
    ax.axhline(0, color='#5a6672', lw=0.8, ls='--')
    ax.set_xlim(0, SPAN); ax.set_ylim(-16, 16)
    ax.set_xticks([0, 30, 60]); ax.set_yticks([-14, -7, 0, 7, 14])
    ax.tick_params(colors='#aab3bb', labelsize=8, length=2)
    for s in ax.spines.values(): s.set_color('#3a4450')
    ax.grid(color='#26303a', lw=0.6)


def sheet(Ls, exits):
    fig = plt.figure(figsize=(30, 27), dpi=100, facecolor=PAPER)
    fig.text(0.012, 0.975, 'MERIDIAN · FIVE STREET PLANS (SIZE B, 600 × 480 m)', fontsize=34, weight='bold', color=INK)
    fig.text(0.012, 0.958, 'Each column is one layout: the plan, then its four exits from the camp. Exit cards show the first 60 m from '
             'the gate, turned so the exit leaves upward (grey flat · orange climbs · blue descends · cream bridge · amber roofs · '
             'hatched covered), with the height profile beside it.', fontsize=15, color='#c9c2b5')
    W = 0.192
    for c, L in enumerate(Ls):
        x0 = 0.012 + c * (W + 0.005)
        ax = fig.add_axes([x0, 0.715, W, 0.2])
        draw_map(ax, L, labels=False, lw_scale=0.45, small=True)
        from meridian_plan import GATES
        for r in L.routes:
            gx, gz = GATES[r.gate]
            ax.text(gx, gz, r.gate, ha='center', va='center', fontsize=8, weight='bold', color='#0a0d10',
                    bbox=dict(boxstyle='circle,pad=0.15', fc=r.color, ec='#0a0d10', lw=1), zorder=30)
        fig.text(x0, 0.937, '%d. %s' % (c + 1, L.name.upper()), fontsize=22, weight='bold', color=INK)
        fig.text(x0, 0.922, L.title, fontsize=12.5, color='#c9c2b5')
        doors = ' · '.join('%s %s %+d' % (d['id'].upper(), d['level'], d['y']) for d in L.doors)
        fig.text(x0, 0.705, 'Court %+d m · %s' % (L.court['y'], doors), fontsize=12, color=INK)
        fig.text(x0, 0.692, 'Hub: ' + L.hub_name.split(':')[0], fontsize=12, color='#c9c2b5')
        for k, r in enumerate(L.routes):
            e = next(e for e in exits if e['route'] is r)
            yb = 0.53 - k * 0.165
            fig.text(x0, yb + 0.142, '%s · %s' % (r.gate, e['title']), fontsize=14, weight='bold', color=r.color)
            fig.text(x0, yb + 0.131, dest_text(L, r) + '  ·  %d m' % round(r.length), fontsize=10.5, color='#c9c2b5')
            a1 = fig.add_axes([x0, yb, W * 0.36, 0.125])
            draw_exit(a1, e)
            a2 = fig.add_axes([x0 + W * 0.44, yb + 0.035, W * 0.55, 0.085])
            draw_profile(a2, e)
            import textwrap
            sig = e['sig']
            fig.text(x0 + W * 0.44, yb + 0.004, '\n'.join(textwrap.wrap('%s · %d m wide · %s' % (
                e['text'], sig['width'], '/'.join(sig['covers'])), 46)), fontsize=9.5, color='#d5cfc3', va='bottom')
    fig.savefig(os.path.join(OUT, 'comparison-sheet.png'), facecolor=PAPER)
    plt.close(fig)


if __name__ == '__main__':
    run()
