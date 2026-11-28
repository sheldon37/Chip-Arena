"""Bake solved clips into the game's file: bake.py out.json name=solved.json ...

Resources/FallenIntroMocap.json: the bones, NOCTIEL's rest skeleton (for the managed checks' forward kinematics), and per clip
and frame the root, each bone's turn from its rest (x,y,z,w), the right fist's grip, the left hand, the plant and two-handed
weights, the tracked blade, and which frames were held or filled (not seen in the video)."""
import json, sys
import numpy as np
from scipy.spatial.transform import Rotation as Rot
exec(open(__file__.replace('bake.py', 'rig.py')).read().split('def pose(')[0])
out = sys.argv[1]
clips = []
for arg in sys.argv[2:]:
    name, path = arg.split('=')
    R = json.load(open(path)); N = R['frames']; bones = R['bones']
    q = np.array([[R['q'][b][i] for b in bones] for i in range(N)])
    r4 = lambda a, d=4: [round(float(x), d) for x in np.asarray(a).reshape(-1)]
    clips.append({'name': name, 'video': R['clip'], 'frames': N, 'q': r4(q), 'root': r4(R['root']), 'grip': r4(R['grip_r']), 'left': r4(R['hand_l']),
                  'plant': r4(R['plant'], 3), 'two': r4(R['two'], 3), 'blade': r4(R['blade']), 'bladeErr': r4(R['blade_err'], 1),
                  'bodyHeld': [int(x) for x in R['body_filled']], 'swordFilled': [int(x) for x in R['sword_filled']]})
names = list(REST.keys())
rest_rot = [round(float(x), 5) for b in names for x in Rot.from_matrix(RR[b]).as_quat()]
data = {'source': 'NOCTIEL intro: the owner reference clips 02-04 (2026-11-06), MediaPipe pose landmarks + the tracked blade, solved onto the rest skeleton (tools/motion)',
        'fps': 24, 'bones': bones, 'restBones': names, 'restParent': [names.index(PARENT[b]) if b in PARENT else -1 for b in names],
        'restPos': [round(float(x), 4) for b in names for x in RP[b]], 'restRot': rest_rot,
        'bladeInHand': [round(float(x), 4) for x in BLADE_L], 'gripInHand': [round(float(x), 4) for x in GRIP_L], 'clips': clips}
json.dump(data, open(out, 'w'), separators=(',', ':'))
import os; print(out, os.path.getsize(out), 'bytes', [c['name'] + ':' + str(c['frames']) for c in clips])
