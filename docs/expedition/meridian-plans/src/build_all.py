"""Build, check and export the five layouts: JSON specs, plan PNGs, and a check report.
Usage: python build_all.py [layout-index ...]"""
import json, os, sys, time
from layouts import ALL
from render_plan import plan_png

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')


def build(fn, render=True):
    t = time.time()
    L = fn(); L.finalize()
    gone = L.unpocket()
    if gone: print('   opened pockets by removing', gone)
    rr = L.reach()
    bad = L.route_checks()
    lens = {r.id: round(r.length, 1) for r in L.routes}
    print('== %s  (%.1fs)' % (L.name, time.time() - t))
    print('   routes', lens, ' lots', L.lot_count, ' blocks', len(L.blocks) + len(L.ground_boxes), ' ramps', len(L.ramps))
    print('   reach', {k: v for k, v in rr.items() if k != 'trapped_sample'})
    if rr['trapped']: print('   trapped sample', rr['trapped_sample'])
    if bad: print('   ROUTE FLOOR/HEADROOM PROBLEMS', len(bad), bad[:12])
    import math
    for r in L.routes:
        if not 250 <= r.length <= 300: print('   LENGTH OUT OF RANGE', r.id, round(r.length, 1))
        x, z, y = r.pts[-1]
        t = L.court['gateway'] if r.dest == 'court' else next(d for d in L.doors if 'door-' + d['id'] == r.dest)
        ty = L.court['y'] if r.dest == 'court' else t['y']
        if math.hypot(x - t['x'], z - t['z']) > 0.5 or abs(y - ty) > 0.01: print('   ROUTE MISSES ITS DOOR', r.id, (x, z, y), t)
    L.route_problems = bad
    js = L.to_json(); js['checks']['route_floor_problems'] = len(bad)
    slug = L.id.replace('meridian-b-', '')
    with open(os.path.join(OUT, slug + '.json'), 'w') as f: json.dump(js, f, indent=1)
    if render: plan_png(L, os.path.join(OUT, slug + '-plan.png'))
    return L


if __name__ == '__main__':
    idx = [int(a) for a in sys.argv[1:]] or range(len(ALL))
    for i in idx: build(ALL[i])
