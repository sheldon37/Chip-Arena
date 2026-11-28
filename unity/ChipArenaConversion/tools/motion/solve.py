"""Reference clip (MediaPipe landmarks + the 2D blade track) -> NOCTIEL's rig: solve.py clip.json sword.json out.json [overrides.json]

Body: the 33 world landmarks into the character's frame (x its right, y up, z its front), the camera's tilt and the body's
facing taken out (from the clip's standing frames), smoothed. Each limb bone is aimed along its limb with its hinge (the elbow's,
the knee's) from the bend, so a bone's twist is defined, never flips, and carries on through a straight limb. The spine is shared
between the hips and the chest, the head from the face. Sword: the blade's 2D angle and its foreshortening give its 3D way (the
depth's sign by continuity); the right hand is turned so the rig's blade lies on it, its roll chosen to keep the wrist inside a
human range (the forearm takes most of the twist). Root: the lowest contact (a sole, a knee) on the floor and the planted one held
still (foot lock), on NOCTIEL's own rest skeleton (Tools gear REST), so its proportions keep the feet planted.
Writes per frame: the root (rest metres, boss frame) and each bone's turn from its rest (x,y,z,w), plus a per-frame quality."""
import json, sys, math
import numpy as np
from scipy.signal import savgol_filter
from scipy.spatial.transform import Rotation as Rot

clip, swordf, out = sys.argv[1], sys.argv[2], sys.argv[3]
over = json.load(open(sys.argv[4])) if len(sys.argv) > 4 else {}
D = json.load(open(clip)); S = json.load(open(swordf)); W, H, FPS = D['w'], D['h'], D['fps']
N = len(D['frames'])

# ---------------- NOCTIEL's rest skeleton (tools/gear/fallen_boss.py REST: mesh space, x right, y up, z forward) ----------------
REST = {
    'pelvis': ((0, .987, .024), (0, -1, .06), (0, -.06, -1), (1, 0, 0)),
    'spine_02': ((0, 1.075, .036), (0, -.99, -.12), (0, .12, -.99), (1, 0, 0)),
    'spine_03': ((0, 1.136, .043), (0, -1, .06), (0, -.06, -1), (1, 0, 0)),
    'spine_05': ((0, 1.395, .001), (0, -.98, .21), (0, -.21, -.98), (1, 0, 0)),
    'neck_01': ((0, 1.491, -.015), (0, -1, -.09), (0, .09, -1), (1, 0, 0)),
    'head': ((0, 1.573, -.005), (1, 0, 0), (0, 1, 0), (0, 0, 1)),
    'upperarm_l': ((-.133, 1.425, -.028), (.6, .8, .01), (-.01, .02, -1), (-.8, .6, .02)),
    'lowerarm_l': ((-.291, 1.215, -.03), (.61, .69, -.39), (-.29, -.26, -.92), (-.74, .67, .04)),
    'hand_l': ((-.427, 1.06, .058), (.48, .78, -.4), (.72, -.61, -.33), (-.5, -.13, -.86)),
    'upperarm_r': ((.133, 1.425, -.028), (.6, -.8, -.01), (-.01, -.02, 1), (-.8, -.6, -.02)),
    'lowerarm_r': ((.291, 1.215, -.03), (.61, -.69, .39), (-.29, .26, .92), (-.74, -.67, -.04)),
    'hand_r': ((.427, 1.06, .058), (.48, -.78, .4), (.72, .61, .33), (-.5, .13, .86)),
    'thigh_l': ((-.093, .961, .038), (-.02, -1, -.02), (0, .02, -1), (1, -.02, 0)),
    'calf_l': ((-.103, .536, .03), (-.03, -1, -.08), (.01, .08, -1), (1, -.03, .01)),
    'foot_l': ((-.117, .11, -.005), (.01, -1, -.05), (0, .05, -1), (1, .01, 0)),
    'ball_l': ((-.116, .013, .116), (0, .05, -1), (-.01, 1, .05), (1, .01, 0)),
    'thigh_r': ((.093, .961, .038), (-.02, 1, .02), (0, -.02, 1), (1, .02, 0)),
    'calf_r': ((.103, .536, .03), (-.03, 1, .08), (.01, -.08, 1), (1, .03, -.01)),
    'foot_r': ((.117, .11, -.005), (.01, 1, .05), (0, -.05, 1), (1, -.01, 0)),
    'ball_r': ((.116, .013, .116), (0, -.05, 1), (-.01, -1, -.05), (1, -.01, 0)),
}
PARENT = {'spine_02': 'pelvis', 'spine_03': 'spine_02', 'spine_05': 'spine_03', 'neck_01': 'spine_05', 'head': 'neck_01',
          'upperarm_l': 'spine_05', 'lowerarm_l': 'upperarm_l', 'hand_l': 'lowerarm_l', 'upperarm_r': 'spine_05', 'lowerarm_r': 'upperarm_r', 'hand_r': 'lowerarm_r',
          'thigh_l': 'pelvis', 'calf_l': 'thigh_l', 'foot_l': 'calf_l', 'ball_l': 'foot_l', 'thigh_r': 'pelvis', 'calf_r': 'thigh_r', 'foot_r': 'calf_r', 'ball_r': 'foot_r'}
BONES = ['pelvis', 'spine_02', 'spine_03', 'spine_05', 'neck_01', 'head', 'upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r',
         'thigh_l', 'calf_l', 'foot_l', 'thigh_r', 'calf_r', 'foot_r']
RP = {b: np.array(v[0], float) for b, v in REST.items()}
# The rig's blade and flat in hand_r's own axes (Runtime NativeFallenBossBody FbBladeWay, FbFlatWay), and the rest hand's turn.
def rest_rot(b):
    _, x, y, z = REST[b]; m = np.array([x, y, z], float).T; u, _, vt = np.linalg.svd(m); return u @ vt
RR = {b: rest_rot(b) for b in REST}
BLADE_L = np.array([.144, .468, .872]); BLADE_L /= np.linalg.norm(BLADE_L); FLAT_L = np.array([.349, -.848, .398]); FLAT_L -= BLADE_L * FLAT_L.dot(BLADE_L); FLAT_L /= np.linalg.norm(FLAT_L)
GRIP_L = np.array([.096, -.018, -.015]); SWORD_LEN = 1.65


def nrm(v):
    n = np.linalg.norm(v); return v / n if n > 1e-9 else v


def frame(a, b):
    a = nrm(a); b = nrm(b - a * a.dot(b)); return np.stack([a, b, np.cross(a, b)], axis=1)


# ---------------- the landmarks in the character's frame ----------------
P = np.full((N, 33, 3), np.nan); IMG = np.full((N, 33, 2), np.nan)
for i, f in enumerate(D['frames']):
    if 'world' in f:
        w = np.array(f['world']); P[i] = np.stack([-w[:, 0], -w[:, 1], -w[:, 2]], axis=1)
        IMG[i] = np.array(f['img'])[:, :2] * [W, H]
good = ~np.isnan(P[:, 0, 0])
# Frames the overrides mark unreadable (a misread arm, a body found late) are held from the first good one.
good[:over.get('body_from', 0)] = False
filled = ~good
first = np.argmax(good); last = N - 1 - np.argmax(good[::-1])
for i in range(N):
    if not good[i]:
        j = first if i < first else last if i > last else max(k for k in range(i) if good[k])
        P[i] = P[j]; IMG[i] = IMG[j]
# Smooth (zero phase): the body over 9 frames, the arms over 7 (the swings are fast).
SMOOTH = [9] * 33
for k in (13, 14, 15, 16, 17, 18, 19, 20, 21, 22): SMOOTH[k] = 7
for k in range(33):
    P[:, k] = savgol_filter(P[:, k], SMOOTH[k], 3, axis=0)
mid = lambda a, b: (P[:, a] + P[:, b]) * .5
# The camera's tilt and the body's facing, from the clip's standing frames: up from the ankles to the shoulders, the hips square.
stand = over.get('stand', [0, min(N, 24)])
sl = slice(stand[0], stand[1])
up = nrm(np.mean(mid(11, 12)[sl] - mid(27, 28)[sl], axis=0))
right = np.mean(P[sl, 24] - P[sl, 23], axis=0); right = nrm(right - up * right.dot(up))
C = np.stack([right, up, np.cross(right, up)], axis=0)  # rows: the body's axes in the old frame
P = P @ C.T
yaw_extra = math.radians(over.get('yaw', 0)); cy, sy = math.cos(yaw_extra), math.sin(yaw_extra)
P = P @ np.array([[cy, 0, -sy], [0, 1, 0], [sy, 0, cy]]).T

# ---------------- the bones' aims ----------------
def hinge_series(a, b, c, rest_hinge_world, ant=None):
    """The hinge of the joint at b (a->b->c) per frame: from the bend while it is clear, carried on through a straight limb."""
    out = np.zeros((N, 3)); prev = None
    for i in range(N):
        u = nrm(P[i, b] - P[i, a]); v = nrm(P[i, c] - P[i, b]); hx = np.cross(u, v); s = np.linalg.norm(hx)
        bend = math.degrees(math.asin(min(1, s)))
        guess = ant[i] if ant is not None else (prev if prev is not None else rest_hinge_world(i))
        g = nrm(guess - u * guess.dot(u))
        if s > 1e-6:
            h = hx / s
            if prev is not None and h.dot(prev) < 0 and bend < 35: h = -h  # a near-straight limb's noise is not a hyperextension
            k = min(1, max(0, (bend - 8) / 14))
            h = nrm(h * k + g * (1 - k))
        else:
            h = g
        h = nrm(h - u * h.dot(u)); out[i] = h; prev = h
    return out


Rpelvis = np.zeros((N, 3, 3)); Rchest = np.zeros((N, 3, 3)); Rhead = np.zeros((N, 3, 3))
for i in range(N):
    hipR = P[i, 24] - P[i, 23]; trunk = mid(11, 12)[i] - mid(23, 24)[i]
    pup = nrm(nrm(trunk) * .45 + np.array([0, 1, 0]) * .55)
    Rpelvis[i] = frame(pup, hipR) @ frame(np.array([0, 1, 0]), np.array([1, 0, 0])).T
    shR = P[i, 12] - P[i, 11]
    Rchest[i] = frame(trunk, shR) @ frame(np.array([0, 1, 0]), np.array([1, 0, 0])).T
    earR = P[i, 8] - P[i, 7]; face = P[i, 0] - (P[i, 7] + P[i, 8]) * .5
    hup = nrm(np.cross(nrm(face), nrm(earR))) if np.linalg.norm(np.cross(nrm(face), nrm(earR))) > .1 else nrm(trunk)
    # The face points on a blank mannequin are loose: the head keeps 60% of the chest's way.
    hq = frame(hup, earR) @ frame(np.array([0, 1, 0]), np.array([1, 0, 0])).T
    Rhead[i] = (Rot.from_matrix(Rchest[i]) * Rot.from_rotvec((Rot.from_matrix(Rchest[i]).inv() * Rot.from_matrix(hq)).as_rotvec() * .4)).as_matrix()

def slerpm(A, B, t):
    ra = Rot.from_matrix(A); return (ra * Rot.from_rotvec((ra.inv() * Rot.from_matrix(B)).as_rotvec() * t)).as_matrix()

Q = {b: np.zeros((N, 3, 3)) for b in BONES}
STRAIGHTENED = {'arm': 0, 'leg': 0}
def unbend(u, v, h, kind):
    """An elbow or a knee bends one way about its hinge, in its plane: a bend the other way (landmark noise on a straight limb) is straightened."""
    # A hinge: the lower bone stays in the plane across its hinge (no sideways bend at an elbow or a knee).
    L = np.linalg.norm(v); v = v - h * v.dot(h); v = nrm(v) * L
    un, vn = nrm(u), nrm(v); sb = math.atan2(np.cross(un, vn).dot(h), un.dot(vn))
    if sb >= 0: return v
    STRAIGHTENED[kind] += 1
    return un * np.linalg.norm(v)
for i in range(N):
    Q['pelvis'][i] = Rpelvis[i]; Q['spine_02'][i] = slerpm(Rpelvis[i], Rchest[i], .35); Q['spine_03'][i] = slerpm(Rpelvis[i], Rchest[i], .7)
    Q['spine_05'][i] = Rchest[i]; Q['neck_01'][i] = slerpm(Rchest[i], Rhead[i], .5); Q['head'][i] = Rhead[i]

def limb(side, a, b, c, ub, lb, rest_hinge, ant=None):
    hinge = hinge_series(a, b, c, lambda i: Q['spine_05'][i] @ rest_hinge, ant)
    u0 = nrm(RP[lb] - RP[ub]);
    for i in range(N):
        u = P[i, b] - P[i, a]; v = P[i, c] - P[i, b]
        Q[ub][i] = frame(u, hinge[i]) @ frame(u0, rest_hinge).T
    return hinge

# Rest hinges: the arms' from their own slight bend; the knees' square across the legs (a straight rest leg has none).
def rest_hinge_arm(s):
    u = RP['lowerarm_' + s] - RP['upperarm_' + s]; v = RP['hand_' + s] - RP['lowerarm_' + s]; return nrm(np.cross(u, v))
HA = {s: rest_hinge_arm(s) for s in 'lr'}
def rest_hinge_leg(s):
    u = nrm(RP['calf_' + s] - RP['thigh_' + s]); return nrm(np.cross(np.array([0, 0, 1.0]), u))
HL = {s: rest_hinge_leg(s) for s in 'lr'}
hinge = {}
for s, (sh, el, wr) in {'l': (11, 13, 15), 'r': (12, 14, 16)}.items():
    hinge['arm' + s] = limb(s, sh, el, wr, 'upperarm_' + s, 'lowerarm_' + s, HA[s])
    u0 = nrm(RP['hand_' + s] - RP['lowerarm_' + s])
    for i in range(N):
        Q['lowerarm_' + s][i] = frame(unbend(P[i, el] - P[i, sh], P[i, wr] - P[i, el], hinge['arm' + s][i], 'arm'), hinge['arm' + s][i]) @ frame(u0, HA[s]).T
heel_of = {'l': 29, 'r': 30}
# The rig's sole at rest: its heel contact to its toe contact (level).
sole0 = {s: nrm((RP['ball_' + s] + np.array((0, -.01, .075))) - (RP['foot_' + s] + np.array((0, -.1, -.06)))) for s in 'lr'}
for s, (hp, kn, an, toe) in {'l': (23, 25, 27, 31), 'r': (24, 26, 28, 32)}.items():
    ant = np.array([nrm(P[i, toe] - P[i, an]) for i in range(N)])
    ant_h = np.array([nrm(np.cross(ant[i], nrm(P[i, kn] - P[i, hp]))) for i in range(N)])
    hinge['leg' + s] = hinge_series(hp, kn, an, None, ant_h)
    u0 = nrm(RP['calf_' + s] - RP['thigh_' + s]); v0 = nrm(RP['foot_' + s] - RP['calf_' + s]); f0 = nrm(RP['ball_' + s] - RP['foot_' + s])
    for i in range(N):
        Q['thigh_' + s][i] = frame(P[i, kn] - P[i, hp], hinge['leg' + s][i]) @ frame(u0, HL[s]).T
        Q['calf_' + s][i] = frame(unbend(P[i, kn] - P[i, hp], P[i, an] - P[i, kn], hinge['leg' + s][i], 'leg'), hinge['leg' + s][i]) @ frame(v0, HL[s]).T
        # The foot: its sole (heel to toe) on the reference's sole, square to the knee's hinge (a flat foot stands flat).
        Q['foot_' + s][i] = frame(P[i, toe] - P[i, heel_of[s]], hinge['leg' + s][i]) @ frame(sole0[s], HL[s]).T

# The free (left) hand: in line with its forearm, turned with the knuckles (MediaPipe's index and pinky) by half.
for i in range(N):
    Q['hand_l'][i] = Q['lowerarm_l'][i]

# ---------------- the sword ----------------
B2 = S['blade']
def px_per_m(i):
    pairs = [(11, 12), (23, 24), (11, 23), (12, 24), (23, 25), (24, 26), (25, 27), (26, 28), (11, 13), (12, 14)]
    rs = [np.linalg.norm(IMG[i, a] - IMG[i, b]) / max(1e-3, np.linalg.norm(P[i, a] - P[i, b])) for a, b in pairs]
    return np.percentile(rs, 85)
scale = savgol_filter(np.array([px_per_m(i) for i in range(N)]), 15, 2)
rel = np.array([bool(B2[i]) and B2[i]['run'] >= over.get('min_run', 80) for i in range(N)])
for a, b in over.get('unreliable', []): rel[a:b + 1] = False
for a, b in over.get('reliable', []): rel[a:b + 1] = [bool(B2[k]) for k in range(a, b + 1)]
tips = np.array([B2[i]['tip'] / scale[i] if B2[i] and not B2[i].get('clipped') else 0 for i in range(N)])
Ls = np.percentile(tips[rel & (tips > 0)], 96) if (rel & (tips > 0)).any() else 1.2
Cinv = C  # image directions go through the same tilt as the body
blade = np.zeros((N, 3)); depth_sign = over.get('depth_sign', {})
# Each readable frame offers two ways (the depth's sign the picture can't tell). The sign is chosen over the whole clip at once
# (a shortest path through the frames): the least turning from frame to frame, never through the torso or the head.
def through(i, v):
    hand = P[i, 16]; pen = 0.0; a_, b_ = mid(23, 24)[i], mid(11, 12)[i]
    for k in range(1, 13):
        q = hand + v * Ls * k / 12
        t = np.clip((q - a_).dot(b_ - a_) / max(1e-6, (b_ - a_).dot(b_ - a_)), 0, 1); pen += max(0, .15 - np.linalg.norm(q - (a_ + (b_ - a_) * t)))
        pen += max(0, .13 - np.linalg.norm(q - P[i, 0]))
    return pen
YAW = np.array([[cy, 0, -sy], [0, 1, 0], [sy, 0, cy]])
cand = {}
for i in range(N):
    if not B2[i] or not rel[i]: continue
    a = math.radians(B2[i]['angle']); c = min(1, B2[i]['tip'] / (Ls * scale[i]))
    if B2[i].get('clipped'): c = max(c, .97)  # the tip is out of the frame: taken as in the picture's plane
    dz = math.sqrt(max(0, 1 - c * c)); opts = []
    for sg in (1, -1):
        v = nrm(YAW @ (C @ np.array([-c * math.cos(a), -c * math.sin(a), sg * dz])))
        forced = None
        for k, sgn in depth_sign.items():
            lo, hi = map(int, k.split('-'))
            if lo <= i <= hi: forced = sgn
        cost = through(i, v) * 40 + (0 if forced is None or forced == sg else 100)
        opts.append((v, cost))
    cand[i] = opts
idx_r = sorted(cand)
if idx_r:
    best = [[cand[idx_r[0]][s_][1], -1] for s_ in range(2)]; back = []
    for n_ in range(1, len(idx_r)):
        i0, i1 = idx_r[n_ - 1], idx_r[n_]; row = []
        for s1 in range(2):
            opts = [(best[s0][0] + math.acos(max(-1, min(1, cand[i0][s0][0].dot(cand[i1][s1][0])))) / max(1, i1 - i0), s0) for s0 in range(2)]
            c_, s0 = min(opts); row.append([c_ + cand[i1][s1][1], s0])
        back.append([r[1] for r in row]); best = row
    s_ = 0 if best[0][0] <= best[1][0] else 1
    for n_ in range(len(idx_r) - 1, -1, -1):
        blade[idx_r[n_]] = cand[idx_r[n_]][s_][0]
        if n_ > 0: s_ = back[n_ - 1][s_]
# Fill the frames it could not see (blur, behind the body): along the great circle, through any key the overrides give.
keys = {int(k): nrm(np.array(v, float)) for k, v in over.get('blade_keys', {}).items()}
for k, v in keys.items(): blade[k] = v; rel[k] = True
idx = np.where(rel)[0]
sword_filled = ~rel
for i in range(N):
    if rel[i]: continue
    lo = idx[idx < i]; hi = idx[idx > i]
    if len(lo) and len(hi):
        a, b = lo[-1], hi[0]; t = (i - a) / (b - a); ra, rb = blade[a], blade[b]
        om = math.acos(max(-1, min(1, ra.dot(rb))))
        blade[i] = ra if om < 1e-4 else nrm(math.sin((1 - t) * om) / math.sin(om) * ra + math.sin(t * om) / math.sin(om) * rb)
    else: blade[i] = blade[lo[-1]] if len(lo) else blade[hi[0]]
# A planted sword stands straight up out of the water (the reference's does): no tracker noise on it.
for i in range(min(N, over.get('plant_until', -1))): blade[i] = np.array([0, -1.0, 0]); sword_filled[i] = False
blade = np.array([nrm(v) for v in savgol_filter(blade, 5, 2, axis=0)])

# The right hand: its blade onto the track, its roll the one that keeps the wrist nearest straight (and changes least).
twist_prev = 0.0; wrist_swing = np.zeros(N); twist_used = np.zeros(N); blade_err = np.zeros(N); WRIST_CAP = over.get('wrist_cap', 80)
# First the forearm's roll each frame (the one that keeps the wrist nearest straight, changing least), then smoothed through
# time so the fist never jumps between two rolls; then the hand on that roll.
rolls = np.zeros(N)
for i in range(N):
    axis = nrm(P[i, 16] - P[i, 14]); best = None
    for tw in np.radians(np.arange(-100, 101, 2)):
        bw = Rot.from_rotvec(axis * tw).as_matrix() @ Q['lowerarm_r'][i] @ RR['hand_r'] @ BLADE_L
        ang = math.acos(max(-1, min(1, bw.dot(blade[i])))); cost = ang + .6 * abs(tw - twist_prev) + .05 * abs(tw)
        if best is None or cost < best[0]: best = (cost, tw)
    rolls[i] = best[1]; twist_prev = best[1]
rolls = savgol_filter(rolls, 9, 2)
for i in range(N):
    Rf = Q['lowerarm_r'][i] @ RR['lowerarm_r']; axis = nrm(P[i, 16] - P[i, 14])
    tw = rolls[i]; Rt = Rot.from_rotvec(axis * tw).as_matrix() @ Q['lowerarm_r'][i]
    ang = math.acos(max(-1, min(1, (Rt @ RR['hand_r'] @ BLADE_L).dot(blade[i])))); twist_used[i] = math.degrees(tw); wrist_swing[i] = math.degrees(ang)
    bw = Rt @ RR['hand_r'] @ BLADE_L
    swr = Rot.align_vectors([blade[i]], [bw])[0]
    # A human wrist: at most WRIST_CAP (80) degrees off its forearm here; what is left over is the blade's error, reported per frame.
    cap = math.radians(WRIST_CAP)
    if ang > cap: swr = Rot.from_rotvec(swr.as_rotvec() * (cap / ang))
    blade_err[i] = math.degrees(max(0, ang - cap))
    Q['hand_r'][i] = swr.as_matrix() @ Rt
    # The forearm takes 60% of the twist (the plate rolls with it), the rest is in the wrist.
    Q['lowerarm_r'][i] = Rot.from_rotvec(axis * tw * .6).as_matrix() @ Q['lowerarm_r'][i]

# ---------------- forward kinematics on NOCTIEL's rest, the floor and the foot lock ----------------
ORDER = ['pelvis', 'spine_02', 'spine_03', 'spine_05', 'neck_01', 'head', 'upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r', 'thigh_l', 'calf_l', 'foot_l', 'ball_l', 'thigh_r', 'calf_r', 'foot_r', 'ball_r']
def qof(b, i):
    return Q[b][i] if b in Q else Q[PARENT[b]][i]
def fk(i):
    pos = {'pelvis': np.zeros(3)}
    for b in ORDER[1:]:
        p = PARENT[b]; pos[b] = pos[p] + qof(p, i) @ (RP[b] - RP[p])
    return pos
# ---- foot contacts: a foot the reference holds still in the picture is on the floor (two-bone IK, the knee in its own plane) ----
STILL = over.get('still_px', 1.5)  # px a frame in the picture: below it, a contact is held still by the reference
LMF = {'l': (29, 31), 'r': (30, 32)}
fsp = {sd: savgol_filter(np.maximum(np.r_[0, np.linalg.norm(np.diff(IMG[:, a], axis=0), axis=1)], np.r_[0, np.linalg.norm(np.diff(IMG[:, b], axis=0), axis=1)]), 5, 1) for sd, (a, b) in LMF.items()}
FOOTPTS = {'l': [('foot_l', (0, -.1, -.06)), ('ball_l', (0, -.012, 0)), ('ball_l', (0, -.01, .075))], 'r': [('foot_r', (0, -.1, -.06)), ('ball_r', (0, -.012, 0)), ('ball_r', (0, -.01, .075))]}
contact_fixed = 0
def sole_low(pos, i, sd):
    return min((pos[b] + qof(b, i) @ np.array(o))[1] for b, o in FOOTPTS[sd])
def leg_to(i, sd, drop, move=None):
    """Moves the ankle down by drop (or by the vector move), the pelvis held, the knee kept in its plane; the foot keeps its turn."""
    pos = fk(i); hip, knee, ank = pos['thigh_' + sd], pos['calf_' + sd], pos['foot_' + sd]
    L1 = np.linalg.norm(RP['calf_' + sd] - RP['thigh_' + sd]); L2 = np.linalg.norm(RP['foot_' + sd] - RP['calf_' + sd])
    goal = ank - np.array([0, drop, 0]) if move is None else ank + move; d = goal - hip; dist = np.linalg.norm(d)
    dist = min(max(dist, abs(L1 - L2) + 1e-3), L1 + L2 - 1e-4); dn = nrm(d); goal = hip + dn * dist
    a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist); hgt = math.sqrt(max(0, L1 * L1 - a * a))
    # The knee goes the way its hinge bends it (square to the hinge and the hip-to-ankle line): never backward, steady when straight.
    hc = nrm(Q['thigh_' + sd][i] @ HL[sd]); pole = np.cross(dn, hc)
    pole = nrm(pole) if np.linalg.norm(pole) > 1e-6 else nrm((knee - hip) - dn * (knee - hip).dot(dn))
    kn = hip + dn * a + pole * hgt; u = kn - hip; v = goal - kn
    h = nrm(np.cross(u, v)) if np.linalg.norm(np.cross(u, v)) > 1e-5 else hc
    if h.dot(hc) < 0: h = -h
    u0 = nrm(RP['calf_' + sd] - RP['thigh_' + sd]); v0 = nrm(RP['foot_' + sd] - RP['calf_' + sd]); Rf = Q['foot_' + sd][i].copy()
    Q['thigh_' + sd][i] = frame(u, h) @ frame(u0, HL[sd]).T; Q['calf_' + sd][i] = frame(v, h) @ frame(v0, HL[sd]).T; Q['foot_' + sd][i] = Rf
ksp = {sd: savgol_filter(np.r_[0, np.linalg.norm(np.diff(IMG[:, k], axis=0), axis=1)], 5, 1) for sd, k in (('l', 25), ('r', 26))}
csp = {k: savgol_filter(np.r_[0, np.linalg.norm(np.diff(IMG[:, v], axis=0), axis=1)], 5, 1) for k, v in (('heel_l', 29), ('toe_l', 31), ('heel_r', 30), ('toe_r', 32))}
# Held (the reference keeps it still): set once it is still two frames running, let go once it moves past twice that two frames
# running (MediaPipe's points jitter around any one line).
def held_mask(spd):
    m_ = np.zeros(N, bool); on = False
    for i in range(N):
        if not on and spd[i] < STILL and (i == 0 or spd[i - 1] < STILL): on = True
        elif on and spd[i] > 2 * STILL and i > 0 and spd[i - 1] > 2 * STILL: on = False
        m_[i] = on
    return m_
HELD = {k: held_mask(v) for k, v in csp.items()}
KHELD = {sd: held_mask(v) for sd, v in ksp.items()}
CPT = {'heel_l': ('foot_l', (0, -.1, -.06)), 'toe_l': ('ball_l', (0, -.01, .075)), 'heel_r': ('foot_r', (0, -.1, -.06)), 'toe_r': ('ball_r', (0, -.01, .075))}
def cpts(pos, i): return {k: pos[b] + qof(b, i) @ np.array(o) for k, (b, o) in CPT.items()}
def tip_foot(i, sd, rise):
    """Turns the foot about its own cross axis at the ankle until the heel is rise above the toe (a pivot on the ball)."""
    best = None
    for ang in np.radians(np.arange(-40, 41, 2)):
        R0 = Rot.from_rotvec(hinge['leg' + sd][i] * ang).as_matrix() @ Q['foot_' + sd][i]
        hl = R0 @ np.array((0, -.1, -.06)) ; tl = (RP['ball_' + sd] - RP['foot_' + sd]); tl = R0 @ tl + R0 @ np.array((0, -.01, .075))
        d = hl[1] - tl[1]
        if d >= rise and (best is None or abs(ang) < abs(best[0])): best = (ang, R0)
    if best is not None: Q['foot_' + sd][i] = best[1]
floor_of = np.zeros(N)
for i in range(N):
    pos = fk(i); C_ = cpts(pos, i)
    knees = {sd: (pos['calf_' + sd] + qof('calf_' + sd, i) @ np.array((0, -.06, .07)))[1] for sd in 'lr'}
    held = [k for k in C_ if HELD[k][i]]
    # A foot pivoting on its toe (the toe still, the heel moving): the heel above the toe, the foot tipped on the ball.
    for sd in 'lr':
        if 'toe_' + sd in held and 'heel_' + sd not in held and C_['heel_' + sd][1] < C_['toe_' + sd][1] + .015: tip_foot(i, sd, .015)
    pos = fk(i); C_ = cpts(pos, i)
    # The floor is under what the reference holds still (a heel, a toe, a kneeling knee); only what moves may be off it, and never
    # under it: a held contact above it comes down, a foot with nothing held that would dip into it is lifted clear.
    hk = [knees[sd] for sd in 'lr' if KHELD[sd][i] and knees[sd] < min(v[1] for v in C_.values()) + .05]
    vals = [C_[k][1] for k in held] + hk
    floor = min(vals) if vals else min([v[1] for v in C_.values()] + list(knees.values()))
    for sd in 'lr':
        mine = [C_[k][1] for k in ('heel_' + sd, 'toe_' + sd) if k in held]
        low = min(C_['heel_' + sd][1], C_['toe_' + sd][1])
        if mine and min(mine) - floor > .01: leg_to(i, sd, min(mine) - floor); contact_fixed += 1
        elif not mine and low < floor + .012: leg_to(i, sd, low - (floor + .012)); contact_fixed += 1
    floor_of[i] = floor
# Smooth the legs' turns through time where the contacts moved them (no pop at a contact's start or end).
for b in ('thigh_l', 'calf_l', 'thigh_r', 'calf_r', 'foot_l', 'foot_r'):
    rv = Rot.from_matrix(Q[b]).as_quat()
    for i in range(1, N):
        if rv[i].dot(rv[i - 1]) < 0: rv[i] *= -1
    rv = savgol_filter(rv, 7, 2, axis=0); Q[b] = Rot.from_quat(rv / np.linalg.norm(rv, axis=1)[:, None]).as_matrix()

CONTACTS = {'heel_l': ('foot_l', (0, -.1, -.06)), 'heel_r': ('foot_r', (0, -.1, -.06)), 'ball_l': ('ball_l', (0, -.012, 0)), 'ball_r': ('ball_r', (0, -.012, 0)),
            'toe_l': ('ball_l', (0, -.01, .075)), 'toe_r': ('ball_r', (0, -.01, .075)), 'knee_l': ('calf_l', (0, -.06, .07)), 'knee_r': ('calf_r', (0, -.06, .07))}
FK = []; CP = []
for i in range(N):
    pos = fk(i); FK.append(pos)
    CP.append({k: pos[b] + qof(b, i) @ np.array(o) for k, (b, o) in CONTACTS.items()})
# Each contact's speed in the picture (px a frame, smoothed): heel, ball and toe by the heel and toe landmarks, the knee by its own.
LM = {'heel_l': 29, 'heel_r': 30, 'ball_l': 31, 'ball_r': 32, 'toe_l': 31, 'toe_r': 32, 'knee_l': 25, 'knee_r': 26}
sp = {k: np.r_[0, np.linalg.norm(np.diff(IMG[:, v], axis=0), axis=1)] for k, v in LM.items()}
sp = {k: savgol_filter(v, 5, 1) for k, v in sp.items()}
img_speed = [{k: sp[k][i] for k in LM} for i in range(N)]
root = np.zeros((N, 3)); stance = None; stance_log = []
for i in range(N):
    ys = {k: v[1] for k, v in CP[i].items()}; low = min(ys.values())
    # The floor the contacts were fitted to (what is held still); a knee or a toe lower than it would be in the water: then lowest.
    root[i, 1] = -min(floor_of[i], low + .0) if low < floor_of[i] - .03 else -floor_of[i]
    # The planted one: among the contacts near the floor, the one standing still in the picture (the camera is fixed), kept while
    # it stays still.
    near = [k for k, y in ys.items() if y < low + .04]
    if stance is not None and stance in near and img_speed[i][stance] < STILL: new = stance
    else: new = min(near, key=lambda k: (img_speed[i][k], ys[k]))
    if i > 0:
        if new == stance:
            d = CP[i][stance] - CP[i - 1][stance]; root[i, 0] = root[i - 1, 0] - d[0]; root[i, 2] = root[i - 1, 2] - d[2]
        else: root[i, 0], root[i, 2] = root[i - 1, 0], root[i - 1, 2]
    stance = new; stance_log.append(stance)
root[:, 1] = savgol_filter(root[:, 1], 7, 2); root[:, 0] = savgol_filter(root[:, 0], 9, 2); root[:, 2] = savgol_filter(root[:, 2], 9, 2)
# Every contact the reference holds still keeps its place on the floor, not only the one the hips are pinned by: the other
# leg is moved onto it (two-bone IK), from the frame it was set down until it moves again.
locked = {}; lock_fixed = 0; corr = {'l': np.zeros(3), 'r': np.zeros(3)}
for i in range(N):
    pos = fk(i); Cw = {k: root[i] + v for k, v in cpts(pos, i).items()}
    held = [k for k in Cw if HELD[k][i]]
    for sd in 'lr':
        mine = [k for k in held if k.endswith(sd) and k in locked]
        if mine:
            # The foot as one: the mean of what its held contacts need (a rolling foot can't keep both its heel and toe exact).
            for k in mine:
                dk = locked[k] - Cw[k]; dk[1] = 0
                # A long hold the reference's foot creeps through: the lock follows it, slowly, past 8 cm.
                if np.linalg.norm(dk) > .08: locked[k] += (Cw[k] - locked[k]) * .2
            d = np.mean([locked[k] - Cw[k] for k in mine], axis=0); d[1] = 0
            corr[sd] = d
        else:
            corr[sd] = corr[sd] * .6  # let go over a few frames, never in one
    for sd in 'lr':
        if np.linalg.norm(corr[sd]) > .003: leg_to(i, sd, 0, move=corr[sd].copy()); lock_fixed += 1
    pos = fk(i); Cw = {k: root[i] + v for k, v in cpts(pos, i).items()}
    for k in list(Cw):
        if HELD[k][i]: locked.setdefault(k, Cw[k].copy())
        else: locked.pop(k, None)

# The right fist's grip and the left hand (boss frame, rest metres, with the root): where a planted sword stands, what a
# two-handed grip reaches for. The plant (the fist on a planted sword) and the two-handed grip as weights per frame.
grip_r = np.zeros((N, 3)); hand_l = np.zeros((N, 3))
for i in range(N):
    pos = fk(i); hw = Q['hand_r'][i] @ RR['hand_r']
    grip_r[i] = root[i] + pos['hand_r'] + hw @ GRIP_L; hand_l[i] = root[i] + pos['hand_l']
plant = np.zeros(N); pu = over.get('plant_until', -1)
for i in range(N): plant[i] = 1.0 if pu >= N else max(0.0, min(1.0, (pu - i) / 4.0)) if pu >= 0 else 0.0
two = np.array([1.0 if np.linalg.norm(P[i, 15] - P[i, 16]) < over.get('two_dist', .24) and i >= over.get('two_from', 0) else 0.0 for i in range(N)])
two = np.clip(savgol_filter(two, 5, 1), 0, 1)
if not over.get('two_hands', False): two[:] = 0

# ---------------- out ----------------
def q4(m):
    q = Rot.from_matrix(m).as_quat(); return q  # x, y, z, w
quats = {b: np.array([q4(Q[b][i]) for i in range(N)]) for b in BONES}
for b in BONES:  # one hemisphere through time (no 360 degree flips in interpolation)
    for i in range(1, N):
        if quats[b][i].dot(quats[b][i - 1]) < 0: quats[b][i] *= -1
res = {'clip': D['video'], 'fps': FPS, 'frames': N, 'bones': BONES,
       'root': np.round(root, 4).tolist(), 'q': {b: np.round(quats[b], 5).tolist() for b in BONES},
       'body_filled': filled.astype(int).tolist(), 'sword_filled': sword_filled.astype(int).tolist(),
       'wrist_swing': np.round(wrist_swing, 1).tolist(), 'forearm_twist': np.round(twist_used, 1).tolist(), 'stance': stance_log,
       'blade': np.round(blade, 4).tolist(), 'blade_len_ref': float(Ls), 'blade_err': np.round(blade_err, 1).tolist(),
       'grip_r': np.round(grip_r, 4).tolist(), 'hand_l': np.round(hand_l, 4).tolist(), 'plant': np.round(plant, 3).tolist(), 'two': np.round(two, 3).tolist()}
json.dump(res, open(out, 'w'))
print(out, 'blade err >0: %d frames, max %.0f deg' % (int((blade_err > 0).sum()), blade_err.max()), 'two-handed', int((two > .5).sum()), 'planted', int((plant > .5).sum()))
print(out, 'held contacts kept in place on the other leg: %d foot-frames' % lock_fixed)
print(out, 'feet brought down to the floor (still in the picture, floating on the rig): %d foot-frames' % contact_fixed)
print(out, 'straightened (a backward bend in the landmarks): elbows %d, knees %d frames' % (STRAIGHTENED['arm'], STRAIGHTENED['leg']))
print(out, 'frames', N, 'body filled', int(filled.sum()), 'sword filled', int(sword_filled.sum()), 'wrist swing max %.0f p90 %.0f' % (wrist_swing.max(), np.percentile(wrist_swing, 90)), 'blade %.2fm' % Ls)
