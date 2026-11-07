"""Hand corrections on the baked intro, read off the owner's reference frames in Unity side by side (docs/EXPEDITION-BOSS-FINAL.md section 19).

usage: python correct.py solved/FallenIntroMocap.solved.json ../../Assets/ChipArena/Resources/FallenIntroMocap.json

Single-camera depth is what the extraction gets wrong, and these are depth errors the picture itself settles. Nothing here is extraction: what it changes on each frame is flagged in
the file's per-clip `corrected` track (a mask: 1 legs and hips, 2 right arm, 4 left arm, 8 blade, 16 chest), and everything not flagged stays the extracted motion:
the spine, the head, the arms' paths after the pull and the blade's tracked way, unless named below. All positions are rest metres on the boss's frame (x its right, y up, z its front).

  Kneel and rise (clip 02): the reference has the rear knee on the floor under the hip, the rear shin running straight back, the front shin vertical, the hips at
    0.46 m, the left hand on the left knee. The solve had both thighs forward and the hips 0.13 m high, then jumped the whole body back 0.58 m at frames 120-132
    (the toe drag). Now: the hips are keyed from the frames at 60, 100, 125 and 150, and the rear leg read off the video every few frames from 100 to 148: the foot stays low and behind,
    turned in under the knee, while the knee comes up over it, then one step forward landing at 148. The front foot never moves.
  One stance (clips 02-04): every held foot is locked to one place through all three clips, so no foot slides, in a take or across a join.
  The planted sword: one fixed point the arm reaches through the whole kneel and rise (the fist's offset from the shoulder as the reference has it at 60 and 168).
    It was the kneel's solved grip, out of the arm's reach for 70% of the rise, so the arm locked straight and the sword slid through the water.
  The pull (clip 03): the fist leaves the plant onto the extracted path over a second, without a jump.
  The wheel across the body (clip 03): the blade at frames 68-104 read off the video's own frames: its angle in the picture, how foreshortened it is, its tip toward the lens
    (the solve had it trailing 20-30 degrees low at 85-89, nearly at the lens by 89).
  Out to the side (clip 03): the blade's depth at frames 100-156 turned forward (the solve had it back into the wings; the reference's tip is nearer the camera).
  The lunge (clip 04): the reference lands the right foot out to the side at the contact, flat, 0.68 m from the left, with the hips at 0.89 m sinking to 0.78 m. The
    solve had the foot forward and 0.1 m off the floor and the hips 0.15 m high. Now: the step and the hips are keyed from the frames at 126, 134 and 160.
  The raise (clip 04): the blade at 52-76 read off the video (the track jumped about 60 degrees back and forth at 64-70, where the blade leaves the picture); 8 degrees up through the
    long hold, 76-118, measured on the video by tools/motion/vidblade.py. Stretches within 5 degrees of the video are left as extracted.
  The chop (clip 04, 120-135): one overhand swing of about 300 degrees, read frame by frame: the tip low behind the head, straight back, up to the picture's left, across the chest toward
    the lens and out low to the picture's right. The fill it replaces went round the other way (up and over the picture's right), between the two frames read when only the sheets were on hand.
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


LEGS, ARM_R, ARM_L, BLADE, CHEST = 1, 2, 4, 8, 16


class Clip:
    def __init__(self, c):
        self.c = c
        self.n = c['frames']
        self.q = np.array(c['q'], float).reshape(self.n, NB, 4)
        self.m = {b: Rot.from_quat(self.q[:, k]).as_matrix() for k, b in enumerate(BONES)}
        self.root = np.array(c['root'], float).reshape(self.n, 3)
        self.blade = np.array(c['blade'], float).reshape(self.n, 3)
        self.fixed = np.zeros(self.n, int)  # per frame: LEGS the legs and hips, ARM_R, ARM_L, BLADE the blade's way, CHEST the chest's lean

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
    clip.fixed[f] |= LEGS


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
    clip.fixed[f] |= ARM_R if s == 'r' else ARM_L


def keys(table):
    f, vals = zip(*table)
    return PchipInterpolator(f, np.array(vals, float), axis=0, extrapolate=True)


def read_way(theta, r, toward):
    """The blade's way from the video: `theta` its angle in the picture (0 the picture's right, 90 up), `r` how much of its length shows, `toward` 1 the tip nearer the lens, -1 away."""
    a = math.radians(theta)
    return nrm(np.array((-r * math.cos(a), r * math.sin(a), toward * math.sqrt(max(0, 1 - r * r)))))


def blade_read(clip, ci, table, leave, filled):
    """The hand carries the blade along `table` (frame: way), each frame the least turn from the last, so no roll is left over from the track it replaces, then rejoins its own over `leave` frames."""
    fr = sorted(table)
    a, b = fr[0], fr[-1]
    old = {f: clip.m['hand_r'][f].copy() for f in range(a, min(clip.n, b + leave + 1))}
    for f in range(a, b + 1):
        n = next(x for x in fr if x >= f)
        p = max(x for x in fr if x <= f)
        want = table[f] if f in table else nrm(table[p] * (n - f) + table[n] * (f - p))
        cur = old[a] if f == a else clip.m['hand_r'][f - 1]
        clip.m['hand_r'][f] = min_rot(cur @ RR['hand_r'] @ BLADE_L, want) @ cur
        clip.blade[f] = clip.hand_world(f) @ BLADE_L
        clip.fixed[f] |= BLADE
        if filled:
            CLIPS[ci]['swordFilled'][f] = 1
    for f in range(b + 1, min(clip.n, b + leave + 1)):
        cur = clip.m['hand_r'][f - 1]
        carried = min_rot(cur @ RR['hand_r'] @ BLADE_L, old[f] @ RR['hand_r'] @ BLADE_L) @ cur
        clip.m['hand_r'][f] = slerpm(carried, old[f], float(smooth((f - b) / (leave + 1))))
        clip.blade[f] = clip.hand_world(f) @ BLADE_L
        clip.fixed[f] |= BLADE


def blade_angle(clip, table, a, b):
    """The blade turned in the picture by what the video measures (degrees, `table`), its depth kept."""
    d = keys(table)
    for f in range(a, b + 1):
        t = math.radians(float(d(f)))
        w = clip.hand_world(f) @ BLADE_L
        xi = -w[0]
        want = np.array((-(xi * math.cos(t) - w[1] * math.sin(t)), xi * math.sin(t) + w[1] * math.cos(t), w[2]))
        clip.m['hand_r'][f] = min_rot(w, want) @ clip.m['hand_r'][f]
        clip.blade[f] = clip.hand_world(f) @ BLADE_L
        clip.fixed[f] |= BLADE


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
    if lean > 0:
        K.fixed[f] |= CHEST
TOES_DOWN = Rot.from_euler('x', 52, degrees=True).as_matrix() @ FLAT['r']
# The rear leg off the video's own frames (rest metres in the picture, less the floor's rise and the narrowing with depth): the picture gives each joint's x and y, the bones' lengths its depth.
KNEE_READ = keys([(100, (.26, .063)), (108, (.23, .09)), (112, (.21, .141)), (116, (.174, .172)), (120, (.16, .219)), (124, (.147, .279)), (128, (.123, .324)),
                  (132, (.09, .375)), (136, (.10, .417)), (140, (.098, .462)), (144, (.107, .487)), (148, (.115, .502))])
FOOT_READ = keys([(100, (.135, .205)), (112, (.13, .19)), (120, (.11, .17)), (128, (.10, .17)), (132, (.09, .18)), (136, (.09, .19)), (140, (.10, .19)), (144, (.125, .15)), (148, (.15, .115)), (150, (.15, .11))])
REAR_LEG = []
L1, L2 = LEN['r']
for f in range(K.n):
    hip = K.pos(f, 'thigh_r')
    kx, ky = KNEE_READ(min(max(f, 100), 148))
    h = math.hypot(kx - hip[0], ky - hip[1])
    if h > L1 * .995:  # the hips' height keeps the knee in the thigh's reach
        kx, ky = hip[0] + (kx - hip[0]) * L1 * .995 / h, hip[1] + (ky - hip[1]) * L1 * .995 / h
        h = L1 * .995
    side = -1 + 2 * float(smooth((f - 128) / 18))  # behind the hip until it steps, then ahead of it
    knee = np.array((kx, ky, hip[2] + side * math.sqrt(max(L1 * L1 - h * h, 0))))
    fx, fy = FOOT_READ(min(max(f, 100), 150))
    back = math.sqrt(max((L2 * .995) ** 2 - (fx - kx) ** 2 - (fy - ky) ** 2, 0))
    REAR_LEG.append((knee, np.array((fx, fy, knee[2] - back))))
from scipy.signal import savgol_filter
REAR_FOOT = savgol_filter(np.array([r[1] for r in REAR_LEG]), 9, 2, axis=0)
for f in range(K.n):
    knee = REAR_LEG[f][0]
    foot = REAR_FOOT[f].copy()
    take = float(smooth((f - 134) / 15))
    foot = foot * (1 - take) + STANCE['r'] * take
    turn = slerpm(TOES_DOWN, FLAT['r'], float(smooth((f - 124) / 24)))
    foot[1] = max(foot[1], .012 - (turn @ (RP['ball_r'] - RP['foot_r']))[1], .11)  # the toe and the heel on the water, not through it
    hip = K.pos(f, 'thigh_r')
    k = float(smooth((f - 144) / 8))
    pole = nrm(knee - hip) * (1 - k) + nrm(np.array(POLE_STAND['r'], float)) * k
    leg_to(K, f, 'r', foot, pole, turn)
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

# ---------------- clip 03: the pull, the wheel, out to the side, lowered: feet locked; the fist leaves the plant smoothly ----------------
# Out to the side (frames 100-156): the solve took the blade's depth the wrong way, back into the wings. The reference's tip is nearer the camera than its hilt
# (it is wider there at 119): the same way in the picture, its depth turned forward, back to the solve's as it lowers.
for f in range(100, 157):
    w = float(smooth((f - 100) / 4)) * (1 - float(smooth((f - 146) / 10)))
    b = P.hand_world(f) @ BLADE_L
    want = nrm(np.array((b[0], b[1], b[2] * (1 - 2 * w))))
    P.m['hand_r'][f] = min_rot(b, want) @ P.m['hand_r'][f]
    P.blade[f] = P.hand_world(f) @ BLADE_L
    P.fixed[f] |= BLADE
    CLIPS[1]['swordFilled'][f] = 1
# The wheel across the body off the video's frames: the tip toward the lens and across, in front of the chest, about two thirds of the blade showing at its flattest.
WHEEL = {68: (-88, .99), 72: (-79, .95), 76: (-71, .93), 78: (-63, .91), 80: (-50, .89), 82: (-40, .86), 84: (-31, .75), 85: (-21, .68), 86: (-11, .65), 87: (-3, .66), 88: (4, .65), 89: (17, .65),
         90: (25, .66), 91: (31, .68), 92: (40, .72), 94: (54, .76), 96: (63, .8), 98: (72, .84), 100: (88, .88), 102: (96, .9), 104: (104, .92)}
blade_read(P, 1, {f: read_way(a, r, 1) for f, (a, r) in WHEEL.items()}, 5, True)
# Going out to the side the video's blade still leads by up to 10 degrees, closing by 116 (tools/motion/vidblade.py); from there on it is within 5.
blade_angle(P, [(105, 7), (108, 8), (110, 3), (116, 0)], 105, 116)
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

# The raise off the video: the track jumps where the blade leaves the top of the picture (64-70); here it is the video's, read every two frames, at the depth around it.
RAISE = {52: (102, .96), 54: (93, .93), 56: (80, .9), 58: (68, .88), 60: (61, .88), 62: (51, .88), 64: (42, .88), 66: (35, .88), 68: (29, .88), 70: (22, .88), 72: (15, .88), 74: (10, .87), 76: (5, .84)}
blade_read(C, 2, {f: read_way(a, r, -1) for f, (a, r) in RAISE.items()}, 4, False)
# The long hold: the video's blade 8 degrees above the track, measured on every frame (tools/motion/vidblade.py).
blade_angle(C, [(77, 4), (80, 8), (96, 8), (104, 5), (112, 4), (119, 6)], 77, 119)
# The chop off the video, frame by frame: (its angle in the picture, how much of it shows, the tip toward the lens or away).
CHOP = {120: (-22, .85, -1), 122: (-26, .82, -1), 124: (-36, .78, -1), 125: (-41, .62, -1), 126: (-41, .35, -1), 127: (113, .3, -1), 128: (127, .6, -1), 129: (129, .8, -1), 130: (134, .93, -1),
        131: (130, .97, 1), 132: (124, .45, 1), 133: (-24, .39, 1), 134: (-22, .92, 1), 135: (-27, .99, 1)}
blade_read(C, 2, {f: read_way(*v) for f, v in CHOP.items()}, 4, True)
# The fists through the chop: the video's drop to the lunge's low place by 132-134; the track, smoothed through the blur, got there at 139. Moved by what the picture shows (its right, up), their depth kept.
FIST_SHIFT = keys([(131, (0, 0)), (132, (0, -.2)), (133, (0, -.16)), (134, (.05, -.18)), (135, (.08, -.18)), (136, (.06, -.18)), (138, (.03, -.08)), (140, (0, 0))])
for f in range(132, 140):
    dx, dy = FIST_SHIFT(f)
    arm_to(C, f, 'r', C.pos(f, 'hand_r') + np.array((-dx, dy, 0)))

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
NAMES = (('legs', LEGS), ('right arm', ARM_R), ('left arm', ARM_L), ('blade', BLADE), ('chest', CHEST))
for c in CLIPS:
    print(c['name'], c['frames'], 'frames; corrected:', {n: sum(1 for v in c['corrected'] if v & m) for n, m in NAMES})
