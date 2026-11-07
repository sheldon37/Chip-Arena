"""Hand corrections on the baked intro, read off the owner's reference frames in Unity side by side (docs/EXPEDITION-BOSS-FINAL.md section 19).

usage: python correct.py solved/FallenIntroMocap.solved.json ../../Assets/ChipArena/Resources/FallenIntroMocap.json

Single-camera depth is what the extraction gets wrong, and these are depth errors the picture itself settles. Nothing here is extraction: every frame this
changes is flagged in the file's per-clip `corrected` track, and the upper body's turns, the arms' paths after the pull and the blade's tracked way stay the
extracted ones unless named below. All positions are rest metres on the boss's frame (x its right, y up, z its front).

  Kneel and rise (clip 02): the reference has the rear knee on the floor under the hip, the rear shin running straight back, the front shin vertical, the hips at
    0.46 m, the left hand on the left knee. The solve had both thighs forward and the hips 0.13 m high, then jumped the whole body back 0.58 m at frames 120-132
    (the toe drag). Now: the hips, the rear leg and its one step forward are keyed from the frames at 60, 100, 125 and 150; the front foot never moves.
  One stance (clips 02-04): every held foot is locked to one place through all three clips, so no foot slides, in a take or across a join.
  The planted sword: one fixed point the arm reaches through the whole kneel and rise (the fist's offset from the shoulder as the reference has it at 60 and 168).
    It was the kneel's solved grip, out of the arm's reach for 70% of the rise, so the arm locked straight and the sword slid through the water.
  The pull (clip 03): the fist leaves the plant onto the extracted path over a second, without a jump.
  The wheel across the body (clip 03): the blade at frame 84 read off the picture (the solve's depth had it pointing at the lens).
  Out to the side (clip 03): the blade's depth at frames 100-156 turned forward (the solve had it back into the wings; the reference's tip is nearer the camera).
  The lunge (clip 04): the reference lands the right foot out to the side at the contact, flat, 0.68 m from the left, with the hips at 0.89 m sinking to 0.78 m. The
    solve had the foot forward and 0.1 m off the floor and the hips 0.15 m high. Now: the step and the hips are keyed from the frames at 126, 134 and 160.
"""
import json
import math
import sys

import numpy as np
from scipy.interpolate import PchipInterpolator
from scipy.spatial.transform import Rotation as Rot

src, out = sys.argv[1], sys.argv[2]
d = json.load(open(src, encoding='utf-8'))
BONES = d['bones']
NB = len(BONES)
RB = d['restBones']
PAR = d['restParent']
RP = {n: np.array(d['restPos'][i * 3:i * 3 + 3], float) for i, n in enumerate(RB)}
RR = {n: Rot.from_quat(d['restRot'][i * 4:i * 4 + 4]).as_matrix() for i, n in enumerate(RB)}
BLADE_L = np.array(d['bladeInHand'], float)
GRIP_L = np.array(d['gripInHand'], float)
CLIPS = d['clips']


def nrm(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


def frame(a, b):
    a = nrm(a)
    b = nrm(b - a * a.dot(b))
    return np.stack([a, b, np.cross(a, b)], axis=1)


def smooth(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def min_rot(a, b):
    a, b = nrm(a), nrm(b)
    c = np.cross(a, b)
    s = np.linalg.norm(c)
    if s < 1e-8:
        return np.eye(3)
    return Rot.from_rotvec(c / s * math.atan2(s, a.dot(b))).as_matrix()


def Ry(deg):
    return Rot.from_euler('y', deg, degrees=True).as_matrix()


def slerpm(a, b, t):
    ra = Rot.from_matrix(a)
    return (ra * Rot.from_rotvec((ra.inv() * Rot.from_matrix(b)).as_rotvec() * t)).as_matrix()


class Clip:
    def __init__(self, c):
        self.c = c
        self.n = c['frames']
        self.q = np.array(c['q'], float).reshape(self.n, NB, 4)
        self.m = {b: Rot.from_quat(self.q[:, k]).as_matrix() for k, b in enumerate(BONES)}
        self.root = np.array(c['root'], float).reshape(self.n, 3)
        self.blade = np.array(c['blade'], float).reshape(self.n, 3)
        self.fixed = np.zeros(self.n, int)

    def turn(self, f, b):
        while b not in self.m:
            b = RB[PAR[RB.index(b)]]
        return self.m[b][f]

    def pos(self, f, b):
        chain = []
        n = b
        while True:
            chain.append(n)
            i = PAR[RB.index(n)]
            if i < 0:
                break
            n = RB[i]
        p = self.root[f].copy()
        for child, parent in zip(chain[::-1][1:], chain[::-1][:-1]):
            p = p + self.turn(f, parent) @ (RP[child] - RP[parent])
        return p

    def hand_world(self, f):
        return self.m['hand_r'][f] @ RR['hand_r']

    def grip(self, f):
        return self.pos(f, 'hand_r') + self.hand_world(f) @ GRIP_L


K, P, C = [Clip(c) for c in CLIPS]
# Each clip in one frame: the next picks up at the last one's root (Core keeps the same offsets, now near nothing).
for prev, cur in ((K, P), (P, C)):
    off = prev.root[-1] - cur.root[0]
    off[1] = 0
    cur.root = cur.root + off

UNIT = {s: {'u0': nrm(RP['calf_' + s] - RP['thigh_' + s]), 'v0': nrm(RP['foot_' + s] - RP['calf_' + s])} for s in 'lr'}
HL = {s: nrm(np.cross(np.array([0, 0, 1.0]), UNIT[s]['u0'])) for s in 'lr'}
LEN = {s: (np.linalg.norm(RP['calf_' + s] - RP['thigh_' + s]), np.linalg.norm(RP['foot_' + s] - RP['calf_' + s])) for s in 'lr'}
ARM = {s: (np.linalg.norm(RP['lowerarm_' + s] - RP['upperarm_' + s]), np.linalg.norm(RP['hand_' + s] - RP['lowerarm_' + s])) for s in 'lr'}
REST_ARM = {s: (RP['lowerarm_' + s] - RP['upperarm_' + s], RP['hand_' + s] - RP['lowerarm_' + s]) for s in 'lr'}


def leg_to(clip, f, s, foot, pole, foot_turn=None):
    """The leg on its foot, the knee toward `pole` (a way from the hip), its hinge square to the bend: no roll left in it."""
    hip = clip.pos(f, 'thigh_' + s)
    l1, l2 = LEN[s]
    to = np.array(foot, float) - hip
    dist = min(np.linalg.norm(to), (l1 + l2) * .998)
    dn = nrm(to)
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 1e-8))
    p = nrm(np.array(pole, float) - dn * dn.dot(pole))
    knee = hip + dn * a + p * h
    hinge = nrm(np.cross(p, dn))
    clip.m['thigh_' + s][f] = frame(knee - hip, hinge) @ frame(UNIT[s]['u0'], HL[s]).T
    clip.m['calf_' + s][f] = frame(hip + dn * dist - knee, hinge) @ frame(UNIT[s]['v0'], HL[s]).T
    if foot_turn is not None:
        clip.m['foot_' + s][f] = foot_turn
    clip.fixed[f] = 1


def arm_to(clip, f, s, wrist):
    """The arm's wrist to a place, its elbow kept in its own hinge's plane and each bone's roll kept (the least turn from the extracted)."""
    upper, lower = 'upperarm_' + s, 'lowerarm_' + s
    sh = clip.pos(f, upper)
    l1, l2 = ARM[s]
    ub = clip.m[upper][f] @ REST_ARM[s][0]
    vb = clip.m[lower][f] @ REST_ARM[s][1]
    hx = np.cross(ub, vb)
    hinge = nrm(hx) if np.linalg.norm(hx) > .012 else clip.m[upper][f] @ nrm(np.cross(REST_ARM[s][0], REST_ARM[s][1]))
    to = np.array(wrist, float) - sh
    dist = max(min(np.linalg.norm(to), (l1 + l2) * .995), abs(l1 - l2) + .01)
    dn = nrm(to)
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 1e-8))
    p = nrm(np.cross(dn, hinge))
    el = sh + dn * a + p * h
    clip.m[upper][f] = min_rot(ub, el - sh) @ clip.m[upper][f]
    clip.m[lower][f] = min_rot(vb, sh + dn * dist - el) @ clip.m[lower][f]
    clip.fixed[f] = 1


def keys(table):
    f, vals = zip(*table)
    return PchipInterpolator(f, np.array(vals, float), axis=0, extrapolate=True)


# ---------------- the one stance ----------------
STANCE = {'l': np.array((-.22, .11, -.08)), 'r': np.array((.15, .11, -.06))}
# Soles flat (the rest pose's are), turned out as the extracted feet are.
def splay(s):
    toe = P.m['foot_' + s][28] @ (RP['ball_' + s] - RP['foot_' + s])
    rest = RP['ball_' + s] - RP['foot_' + s]
    return math.degrees(math.atan2(toe[0], toe[2]) - math.atan2(rest[0], rest[2]))
FLAT = {s: Ry(splay(s)) for s in 'lr'}
POLE_STAND = {'l': (-.22, 0, 1), 'r': (.22, 0, 1)}

def hips_fit(clip, a, b):
    """The hips no higher than the legs reach to their held feet (locked feet moved them: a leg can't stretch to the floor), smoothly."""
    from scipy.signal import savgol_filter
    y = clip.root[:, 1].copy()
    for f in range(a, b):
        over = 0
        for s in 'lr':
            hip = clip.pos(f, 'thigh_' + s)
            dx = hip[[0, 2]] - STANCE[s][[0, 2]]
            reach = .985 * sum(LEN[s])
            need = math.sqrt(max(reach * reach - dx.dot(dx), .01))
            over = max(over, hip[1] - STANCE[s][1] - need)
        y[f] -= max(0, over)
    # Only ever down: the envelope, smoothed.
    w = min(21, (b - a) | 1)
    fit = savgol_filter(y[a:b], w, 2)
    y[a:b] = np.minimum(y[a:b], fit) if False else fit - max(0, float((fit - y[a:b]).max()))
    clip.root[a:b, 1] = y[a:b]


# ---------------- the planted sword: the fist's place from the shoulder as the reference has it, kneeling and standing ----------------
PLANT = np.array((.40, 1.25, -.33))

# ---------------- clip 02: the kneel and the rise ----------------
HIPS = keys([(0, (0, .465, -.47)), (60, (0, .465, -.47)), (98, (0, .49, -.46)), (112, (-.02, .6, -.40)), (125, (-.04, .73, -.32)), (137, (-.05, .86, -.25)), (150, (-.05, .93, -.2)), (168, K.root[168])])
for f in range(K.n):
    K.root[f] = HIPS(f)
# The solved chest leans back from the kneel's depth error (its shoulders 0.13 m behind its hips); the reference's is upright. Turned over the hips, easing out as it stands.
UPPER = ['spine_02', 'spine_03', 'spine_05', 'neck_01', 'head', 'upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r']
for f in range(K.n):
    lean = 20 * (1 - float(smooth((f - 104) / 34)))
    R = Rot.from_euler('x', lean, degrees=True).as_matrix()
    for b in UPPER:
        K.m[b][f] = R @ K.m[b][f]
KNEE_REAR = np.array((.26, .06, -.52))
FOOT_REAR = np.array((.27, .205, -.93))
TOES_DOWN = Rot.from_euler('x', 52, degrees=True).as_matrix() @ FLAT['r']
STEP_FROM, STEP_TO = 104, 146
for f in range(K.n):
    u = (f - STEP_FROM) / (STEP_TO - STEP_FROM)
    su = float(smooth(u))
    foot = FOOT_REAR + (STANCE['r'] - FOOT_REAR) * su
    foot[1] += .17 * math.sin(math.pi * min(1, max(0, u)))
    hip = K.pos(f, 'thigh_r')
    rear = KNEE_REAR - (np.array((.093, .44, -.46)))  # the way from the hip to the knee on the floor
    pole = nrm(rear) * (1 - float(smooth((f - 100) / 22))) + nrm(np.array(POLE_STAND['r'], float)) * float(smooth((f - 100) / 22))
    leg_to(K, f, 'r', foot, pole, slerpm(TOES_DOWN, FLAT['r'], float(smooth((f - 106) / 38))))
    kp = float(smooth((f - 104) / 30))
    pole = nrm(np.array((-.3, .62, .72))) * (1 - kp) + nrm(np.array(POLE_STAND['l'], float)) * kp
    leg_to(K, f, 'l', STANCE['l'], pole, FLAT['l'])
# The left hand on the left knee until it stands; the fist on the plant throughout.
for f in range(K.n):
    k = 1 - float(smooth((f - 132) / 20))
    knee = K.pos(f, 'calf_l')
    wrist_on_knee = knee + np.array((.03, .1, .03))
    if k > 0:
        arm_to(K, f, 'l', K.pos(f, 'hand_l') * (1 - k) + wrist_on_knee * k)
    arm_to(K, f, 'r', PLANT - K.hand_world(f) @ GRIP_L)
K.fixed[:] = 1

# ---------------- clip 03: the pull, the wheel, out to the side, lowered: feet locked; the fist leaves the plant smoothly ----------------
# The blade across the body at frame 84: down and across in the picture (in-plane 0.75 of its length), the rest of it forward.
READ_84 = nrm(np.array((-.66, -.48, .58)))
now_84 = P.hand_world(84) @ BLADE_L
TURN_84 = Rot.from_matrix(min_rot(now_84, READ_84)).as_rotvec()
for f in range(70, 99):
    w = float(np.cos((f - 84) / 14 * math.pi / 2) ** 2) if abs(f - 84) < 14 else 0
    if w <= 0:
        continue
    P.m['hand_r'][f] = Rot.from_rotvec(TURN_84 * w).as_matrix() @ P.m['hand_r'][f]
    P.blade[f] = P.hand_world(f) @ BLADE_L
    P.fixed[f] = 1
    CLIPS[1]['swordFilled'][f] = 1
# Out to the side (frames 100-156): the solve took the blade's depth the wrong way, back into the wings. The reference's tip is nearer the camera than its hilt
# (it is wider there at 119): the same way in the picture, its depth turned forward, back to the solve's as it lowers.
for f in range(100, 157):
    w = float(smooth((f - 100) / 4)) * (1 - float(smooth((f - 146) / 10)))
    b = P.hand_world(f) @ BLADE_L
    want = nrm(np.array((b[0], b[1], b[2] * (1 - 2 * w))))
    P.m['hand_r'][f] = min_rot(b, want) @ P.m['hand_r'][f]
    P.blade[f] = P.hand_world(f) @ BLADE_L
    P.fixed[f] = 1
    CLIPS[1]['swordFilled'][f] = 1
hips_fit(P, 0, P.n)
from_grip = P.grip(56)
for f in range(P.n):
    for s in 'lr':
        leg_to(P, f, s, STANCE[s], POLE_STAND[s], FLAT[s])
    if f <= 56:
        arm_to(P, f, 'r', PLANT - P.hand_world(f) @ GRIP_L)
    elif f < 84:
        k = 1 - float(smooth((f - 56) / 28))
        arm_to(P, f, 'r', P.pos(f, 'hand_r') + (PLANT - from_grip) * k)

# ---------------- clip 04: the raise, the guard, the windup on the stance; the step and the lunge from the frames ----------------
HIPS_Y = keys([(0, C.root[0][1])] + [(120, C.root[120][1]), (126, .9), (134, .87), (140, .81), (150, .78), (168, .775)])
HIPS_X = keys([(0, C.root[0][0]), (120, C.root[120][0]), (134, .04), (150, .07), (168, .07)])
hips_fit(C, 0, 122)
HIPS_Y = keys([(0, C.root[0][1]), (120, C.root[120][1]), (126, .9), (134, .87), (140, .81), (150, .78), (168, .775)])
for f in range(121, C.n):
    C.root[f][1] = HIPS_Y(f)
    C.root[f][0] = HIPS_X(f)
LAND = np.array((.46, .11, -.04))
LAND_TURN = Ry(38) @ FLAT['r']
STEP0, STEP1 = 123, 134.5
LEFT_TURN = {f: C.m['foot_l'][f].copy() for f in range(C.n)}
for f in range(C.n):
    u = (f - STEP0) / (STEP1 - STEP0)
    su = float(smooth(u))
    foot = STANCE['r'] + (LAND - STANCE['r']) * su
    foot[1] += .25 * math.sin(math.pi * min(1, max(0, u)) ** .8)
    k = float(smooth((f - 120) / 14))
    pole = np.array(POLE_STAND['r'], float) * (1 - k) + np.array((.62, 0, .78)) * k
    turn = slerpm(FLAT['r'], LAND_TURN, su)
    if 0 < u < 1:
        turn = Rot.from_euler('x', 28 * math.sin(math.pi * u), degrees=True).as_matrix() @ turn
    leg_to(C, f, 'r', foot, pole, turn)
    # The rear foot stays where it is and pivots as the body turns over it.
    pole = np.array(POLE_STAND['l'], float) * (1 - k) + np.array((-.78, 0, .55)) * k
    leg_to(C, f, 'l', STANCE['l'], pole, slerpm(FLAT['l'], Ry(-62) @ FLAT['l'], float(smooth((f - 124) / 14))))
C.fixed[:] = 1

# ---------------- the wrist: the hand no further off its forearm than a human's, the forearm taking what's over ----------------
RH = RP['hand_r'] - RP['lowerarm_r']
AXIS = nrm(RR['hand_r'].T @ RH)
CAP = math.radians(84)
def wrist_angle(clip, f):
    rel = (clip.m['lowerarm_r'][f] @ RR['hand_r']).T @ (clip.m['hand_r'][f] @ RR['hand_r'])
    return math.acos(max(-1, min(1, AXIS.dot(rel @ AXIS))))
for clip in (K, P, C):
    for f in range(clip.n):
        a = wrist_angle(clip, f)
        if a <= CAP:
            continue
        # Turn the forearm (about the elbow) toward the hand's way by what's over: the hand's world turn and so the blade stay.
        wrist_way = clip.m['hand_r'][f] @ RH
        fore = clip.m['lowerarm_r'][f] @ RH
        over = (a - CAP) / a
        clip.m['lowerarm_r'][f] = slerpm(np.eye(3), min_rot(fore, wrist_way), over) @ clip.m['lowerarm_r'][f]
worst = {c: max((math.degrees(wrist_angle(x, f)), f) for f in range(x.n)) for c, x in (('kneel', K), ('pull', P), ('cut', C))}
print('wrist worst', {k: (round(v[0], 1), v[1]) for k, v in worst.items()})
# ---------------- back into the file ----------------
for clip in (K, P, C):
    c = clip.c
    for k, b in enumerate(BONES):
        q = Rot.from_matrix(clip.m[b]).as_quat()
        # One hemisphere from frame to frame: the sampler slerps between them.
        for f in range(1, clip.n):
            if q[f].dot(q[f - 1]) < 0:
                q[f] = -q[f]
        clip.q[:, k] = q
    grip = np.array([clip.grip(f) for f in range(clip.n)])
    left = np.array([clip.pos(f, 'hand_l') for f in range(clip.n)])
    c['q'] = np.round(clip.q.reshape(-1), 5).tolist()
    c['root'] = np.round(clip.root.reshape(-1), 4).tolist()
    c['grip'] = np.round(grip.reshape(-1), 4).tolist()
    c['left'] = np.round(left.reshape(-1), 4).tolist()
    c['blade'] = np.round(clip.blade.reshape(-1), 4).tolist()
    c['corrected'] = clip.fixed.tolist()
d['plantGrip'] = np.round(PLANT, 4).tolist()
d['source'] = str(d.get('source', '')).split(' + hand corrections')[0] + ' + hand corrections from the reference frames (tools/motion/correct.py, flagged per frame in corrected)'
with open(out, 'w', encoding='utf-8', newline='\n') as fh:
    json.dump(d, fh, separators=(',', ':'))
print('corrected:', {c['name']: int(sum(c['corrected'])) for c in CLIPS}, 'of', {c['name']: c['frames'] for c in CLIPS})
