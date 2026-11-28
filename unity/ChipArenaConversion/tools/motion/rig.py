# NOCTIEL's rest skeleton and forward kinematics for a solved clip (shared by render.py and the checks' reference numbers).
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
ORDER = ['pelvis', 'spine_02', 'spine_03', 'spine_05', 'neck_01', 'head', 'upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r', 'thigh_l', 'calf_l', 'foot_l', 'ball_l', 'thigh_r', 'calf_r', 'foot_r', 'ball_r']
RP = {b: np.array(v[0], float) for b, v in REST.items()}
def _rest_rot(b):
    _, x, y, z = REST[b]; m = np.array([x, y, z], float).T; u, _, vt = np.linalg.svd(m); return u @ vt
RR = {b: _rest_rot(b) for b in REST}
BLADE_L = np.array([.144, .468, .872]); BLADE_L /= np.linalg.norm(BLADE_L)
GRIP_L = np.array([.096, -.018, -.015])
def pose(R, i):
    qm = {b: Rot.from_quat(R['q'][b][i]).as_matrix() for b in R['bones']}
    q = lambda b: qm[b] if b in qm else qm[PARENT[b]]
    pos = {'pelvis': np.array(R['root'][i], float)}
    for b in ORDER[1:]:
        p = PARENT[b]; pos[b] = pos[p] + q(p) @ (RP[b] - RP[p])
    hw = q('hand_r') @ RR['hand_r']
    grip = pos['hand_r'] + hw @ GRIP_L; tip = grip + hw @ BLADE_L * 1.65
    return pos, hw, grip, tip
