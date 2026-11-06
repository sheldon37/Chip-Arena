"""Side by side: the reference frame (its extracted skeleton) | NOCTIEL's solved rig from the same camera | from the side.
render.py video clip.json solved.json out.jpg frames(comma list or step:N) [solve.py module dir]
The rig is NOCTIEL's rest skeleton (tools/gear/fallen_boss.py REST) posed by the solved turns with forward kinematics, the claymore
on its hand by the runtime's own grip and blade axes. Not the game's renderer: the bones and the sword, to compare the motion."""
import json, sys, math, subprocess
import numpy as np, cv2
sys.argv_saved = list(sys.argv)
video, clipf, solvedf, out, which = sys.argv[1:6]
D = json.load(open(clipf)); R = json.load(open(solvedf)); W, H = D['w'], D['h']; N = R['frames']
from scipy.spatial.transform import Rotation as Rot
exec(open(__file__.replace('render.py', 'rig.py')).read())
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', video, '-f', 'rawvideo', '-pix_fmt', 'bgr24', '-'], capture_output=True).stdout
F = np.frombuffer(raw, np.uint8).reshape(-1, H, W, 3)
frames = list(range(0, N, int(which.split(':')[1]))) if which.startswith('step:') else [int(x) for x in which.split(',')]
LINKS = [('pelvis', 'spine_02'), ('spine_02', 'spine_03'), ('spine_03', 'spine_05'), ('spine_05', 'neck_01'), ('neck_01', 'head'), ('spine_05', 'upperarm_l'), ('upperarm_l', 'lowerarm_l'), ('lowerarm_l', 'hand_l'),
         ('spine_05', 'upperarm_r'), ('upperarm_r', 'lowerarm_r'), ('lowerarm_r', 'hand_r'), ('pelvis', 'thigh_l'), ('thigh_l', 'calf_l'), ('calf_l', 'foot_l'), ('foot_l', 'ball_l'), ('pelvis', 'thigh_r'), ('thigh_r', 'calf_r'), ('calf_r', 'foot_r'), ('foot_r', 'ball_r')]
BONES_MP = [(11, 12), (11, 23), (12, 24), (23, 24), (11, 13), (13, 15), (12, 14), (14, 16), (23, 25), (25, 27), (24, 26), (26, 28), (27, 31), (28, 32)]
tiles = []
for i in frames:
    im = F[i].copy(); f = D['frames'][i]
    if 'img' in f:
        Pm = [(int(l[0] * W), int(l[1] * H)) for l in f['img']]
        for a, b in BONES_MP: cv2.line(im, Pm[a], Pm[b], (0, 0, 255) if a % 2 == 0 and b % 2 == 0 else (255, 120, 0) if a % 2 and b % 2 else (0, 220, 0), 2)
    cv2.putText(im, 'reference f%d %.2fs' % (i, i / R['fps']), (8, 22), cv2.FONT_HERSHEY_SIMPLEX, .6, (0, 255, 255), 2)
    pos, hand, grip, tip = pose(R, i)
    planted = R.get('plant', [0] * N)[i] > .5
    if planted: tip = grip + np.array([0, -1.65, 0])  # the game's plant IK: the fist on the planted sword, the blade straight down
    views = []
    for view in ('front', 'side'):
        can = np.full((H, W, 3), 38, np.uint8); cv2.line(can, (0, int(H * .9)), (W, int(H * .9)), (90, 90, 90), 1)
        def pj(p):
            x, y, z = p
            if view == 'front': u = -x  # the camera faces the boss: its right is the image's left
            else: u = -z  # from the boss's right: its front is the image's left
            return (int(W * .5 + u * H * .38), int(H * .9 - y * H * .38))
        for a, b in LINKS:
            col = (60, 60, 240) if a.endswith('_r') or b.endswith('_r') else (240, 140, 40) if a.endswith('_l') or b.endswith('_l') else (90, 220, 90)
            cv2.line(can, pj(pos[a]), pj(pos[b]), col, 4)
        cv2.circle(can, pj(pos['head'] + np.array([0, .1, 0])), 12, (200, 200, 200), 2)
        cv2.line(can, pj(grip), pj(tip), (235, 235, 235), 3); cv2.circle(can, pj(grip), 5, (0, 255, 255), -1)
        sf = R['sword_filled'][i]; bf = R['body_filled'][i]
        cv2.putText(can, 'NOCTIEL rig %s%s%s%s' % (view, '  planted' if planted else '', '  sword filled' if sf else '', '  body held' if bf else ''), (8, 22), cv2.FONT_HERSHEY_SIMPLEX, .6, (0, 255, 255), 2)
        if not planted: cv2.putText(can, 'wrist %d  forearm roll %d' % (R['wrist_swing'][i], R['forearm_twist'][i]), (8, H - 12), cv2.FONT_HERSHEY_SIMPLEX, .5, (200, 200, 200), 1)
        views.append(can)
    tiles.append(np.hstack([im] + views))
img = np.vstack(tiles)
cv2.imwrite(out, cv2.resize(img, (img.shape[1] // 2, img.shape[0] // 2)), [cv2.IMWRITE_JPEG_QUALITY, 85])
print(out, len(tiles))
