"""hatfit.py GEAR_JSON HATS_TXT [OUT_JSON [NativeSleekFace.cs]]: the helmet's hat envelope and each model hat's fit on it
(docs/PILOT-BODY-HAIR.md section 6). GEAR_JSON: the face gear model's export (facemodel/run_model.py); HATS_TXT: the model hats'
points (CoreTests --dump-model-hats); with a .cs it rewrites the generated block of Core/NativeSleekFace.cs.

The envelope: the helmet's outer surface as a star-shaped radius table round the head's middle C (azimuth 0-180 degrees from ahead,
the helmet being symmetric; elevation -30 to 90 degrees), each cell the furthest helmet point near it, so a point is outside the helmet
when |p - C| > the table's radius in its direction (Core/NativeSleekFace.cs Inside reads the same table).
Each hat (its points in the head bone's frame as worn on the bare head) goes to p * scale + move, move =
HatPivot * (1 - scale) + (0, lift, ahead) (Core/NativeRobotHelm.HatFit's form): for each scale and ahead on a grid the lowest lift at
which every point above the band (y > .06; the cone's above its elastic, .187) clears the envelope by 2 mm; the hat covers as much of
the helmet as it covers of the bare head (its band as far under the helmet's top as under the crown), at the smallest scale (none
under its own size) that reaches it, else the scale whose band comes lowest. The halo keeps its own gap over the crown as on the bare
head."""
import sys, json, math
import numpy as np

C = np.array([0, .07, .02])
AZ = np.radians(np.arange(0, 181, 15))          # 13
EL = np.radians(np.arange(-30, 91, 10))         # 13
PIVOT = np.array([0, .03, .025])
HATS = ["cap", "beanie", "top", "wizard", "cowboy", "crown", "cone", "antenna", "halo"]
CLEAR = .0022  # (the test asks 2 mm: the table is rounded to a tenth of a millimetre)


def helmet_points(path):
    d = json.load(open(path))
    V = [np.asarray(r['V'], float) for r in d['records'] if r['name'].startswith('Helmet: ')]
    return np.concatenate(V)


def envelope(V):
    D = V - C
    r = np.linalg.norm(D, axis=1)
    az = np.arctan2(np.abs(D[:, 0]), D[:, 2])
    el = np.arcsin(np.clip(D[:, 1] / np.maximum(r, 1e-9), -1, 1))
    E = np.zeros((len(AZ), len(EL)))
    w_az, w_el = np.radians(15) * .75, np.radians(10) * .75
    for i, a in enumerate(AZ):
        for j, e in enumerate(EL):
            # (at the pole every azimuth is the same direction)
            near = (np.abs(el - e) <= w_el) & ((np.abs(az - a) <= w_az) | (e > np.radians(85)))
            E[i, j] = r[near].max() if near.any() else 0
    return E


def env_radius(E, P):
    D = P - C
    r = np.linalg.norm(D, axis=1)
    az = np.arctan2(np.abs(D[:, 0]), D[:, 2])
    el = np.arcsin(np.clip(D[:, 1] / np.maximum(r, 1e-9), -1, 1))
    fi = np.clip((az - AZ[0]) / (AZ[1] - AZ[0]), 0, len(AZ) - 1.0001)
    fj = np.clip((el - EL[0]) / (EL[1] - EL[0]), 0, len(EL) - 1.0001)
    i0, j0 = fi.astype(int), fj.astype(int)
    ti, tj = fi - i0, fj - j0
    R = (E[i0, j0] * (1 - ti) * (1 - tj) + E[i0 + 1, j0] * ti * (1 - tj) + E[i0, j0 + 1] * (1 - ti) * tj + E[i0 + 1, j0 + 1] * ti * tj)
    return r, R


def inside(E, P, grow=0.0):
    r, R = env_radius(E, P)
    return r / np.maximum(R + grow, 1e-9)


def load_hats(path):
    H = {}
    for line in open(path):
        a = line.split()
        if len(a) == 4:
            H.setdefault(a[0], []).append([float(t) for t in a[1:]])
    return {k: np.array(v) for k, v in H.items()}


def place(P, s, lift, ahead):
    return P * s + PIVOT * (1 - s) + np.array([0, lift, ahead])


def lowest_lift(E, P, s, ahead):
    lo, hi = -.05, .12
    if inside(E, place(P, s, hi, ahead), CLEAR).min() < 1:
        return None
    for _ in range(26):
        m = (lo + hi) / 2
        if inside(E, place(P, s, m, ahead), CLEAR).min() < 1:
            lo = m
        else:
            hi = m
    return hi


def fit(E, name, P, top_y):
    keep = P[:, 1] >= (.187 if name == 'cone' else .06)
    Q = P[keep]
    if name == 'halo':
        # the halo floats: its gap over the helmet's top as over the bare head's crown (21.2 cm)
        return 1.0, round(float(top_y - .212), 4), 0.0
    # the same depth: on the bare head the hat's band (its lowest centimetre, mean height yb) sits `depth` under the crown (21.2 cm);
    # on the helmet the smallest scale (from its own size up) whose lowest clearing lift (ahead on a grid) brings the band as far under
    # the helmet's top; none reaching it: the scale whose band comes lowest
    band = Q[Q[:, 1] < Q[:, 1].min() + .01]
    yb = float(band[:, 1].mean()); target = top_y - (.212 - yb)
    best, lowest = None, None
    for s in np.arange(1.0, 1.3001, .005):
        seat = None
        for ahead in np.arange(-.012, .0121, .002):
            lift = lowest_lift(E, Q, s, ahead)
            if lift is not None and (seat is None or lift < seat[1]):
                seat = (s, lift, ahead)
        if seat is None:
            continue
        by = PIVOT[1] + (yb - PIVOT[1]) * seat[0] + seat[1]
        if lowest is None or by < lowest[0]:
            lowest = (by, seat)
        if by <= target:
            best = (0, seat[0], seat[1], seat[2], 0)
            break
    if best is None:
        best = (0, lowest[1][0], lowest[1][1], lowest[1][2], 0)
    _, s, lift, ahead, gap = best
    return round(float(s), 3), round(float(lift), 4), round(float(ahead), 4)


def main():
    V = helmet_points(sys.argv[1])
    E = envelope(V)
    hats = load_hats(sys.argv[2])
    out = {'centre': C.tolist(), 'az_deg': [15, 13], 'el_deg': [-30, 10, 13], 'envelope': np.round(E, 4).tolist(), 'fits': {}}
    for h in HATS:
        if h in hats:
            s, lift, ahead = fit(E, h, hats[h], float(V[:, 1].max()))
            P = hats[h][hats[h][:, 1] >= (.187 if h == 'cone' else .06)]
            q = place(P, s, lift, ahead)
            clear_mm = float(((env_radius(E, q)[0] - env_radius(E, q)[1])).min() * 1000)
            out['fits'][h] = {'scale': s, 'lift': lift, 'ahead': ahead, 'min_clear_mm': round(clear_mm, 2)}
            print('%-8s scale %.3f lift %.4f ahead %.4f  clear %.1f mm' % (h, s, lift, ahead, clear_mm))
    missing = [h for h in HATS if h not in out['fits']]
    if missing:
        sys.exit('no points for hats %s in %s' % (missing, sys.argv[2]))
    if len(sys.argv) > 3:
        json.dump(out, open(sys.argv[3], 'w'), indent=1)
    if len(sys.argv) > 4:
        write_cs(out, sys.argv[4])


def write_cs(fit, path):
    """Rewrite the generated block of Core/NativeSleekFace.cs with this envelope and these fits."""
    f = lambda v: ('%.4f' % v).rstrip('0').rstrip('.').replace('0.', '.', 1) if v >= 0 else '-' + ('%.4f' % -v).rstrip('0').rstrip('.').replace('0.', '.', 1)
    ff = lambda v: (f(v) or '0') + ('f' if f(v) not in ('0', '1', '-1') and '.' in f(v) else '')
    E = np.array(fit['envelope'])
    env = ','.join(ff(v) for v in E.reshape(-1))
    order = ["cap", "beanie", "top", "wizard", "cowboy", "crown", "cone", "antenna", "halo"]
    sc = ','.join(ff(fit['fits'][h]['scale']) for h in order)
    li = ','.join(ff(fit['fits'][h]['lift']) for h in order)
    ah = ','.join(ff(fit['fits'][h]['ahead']) for h in order)
    s = open(path, encoding='utf-8').read()
    a = s.index('        // <generated by tools/gear/hatfit.py>'); b = s.index('        // </generated>')
    block = ('        // <generated by tools/gear/hatfit.py>\n'
             '        public static readonly Vector3 EnvCentre=new Vector3(0,.07f,.02f);\n'
             '        public const int EnvAz=13,EnvEl=13;public const float EnvAzStep=15,EnvEl0=-30,EnvElStep=10;\n'
             '        private static readonly float[] Envelope={%s};\n'
             '        private static readonly string[] FitHats={"cap","beanie","top","wizard","cowboy","crown","cone","antenna","halo"};\n'
             '        private static readonly float[] FitScale={%s},FitLift={%s},FitAhead={%s};\n') % (env, sc, li, ah)
    open(path, 'w', encoding='utf-8', newline='\n').write(s[:a] + block + s[b:])
    print('wrote', path)


if __name__ == '__main__':
    main()
