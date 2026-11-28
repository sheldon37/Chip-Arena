"""headobj.py DUMP OUT.obj: the pilot's head (the hair lab's dump of Head_AnimeYoung, the head bone's frame: tools/gear/hair.py
--dump) as an OBJ for the face gear models (run_model.py's HEAD.obj: reference only, never exported). The mesh is the bought
pack's, so the OBJ stays local (the scratch folder), never committed."""
import sys, os, json
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import garments as G


def main():
    dump, out = sys.argv[1], sys.argv[2]
    d = json.load(open(os.path.join(dump, 'Head_AnimeYoung.json')))
    r = max(d['renderers'], key=lambda r: len(r['v']))
    V = np.array(r['v'], float).reshape(-1, 3)
    F = np.concatenate([np.array(s, np.int64) for s in r['submeshes']]).reshape(-1, 3)
    V, F, _ = G.compact(V, F)
    with open(out, 'w') as w:
        for v in V:
            w.write('v %.5f %.5f %.5f\n' % tuple(v))
        for f in F:
            w.write('f %d %d %d\n' % tuple(f + 1))
    print('head: %d vertices, %d faces -> %s' % (len(V), len(F), out))


if __name__ == '__main__':
    main()
