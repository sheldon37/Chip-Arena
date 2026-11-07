"""Extract per-frame body and hand landmarks from a reference clip with MediaPipe (pose landmarker heavy + hand landmarker).

Writes <out>.json: fps, size, and per frame the 33 pose landmarks (image: x,y normalised, z relative; world: metres, hip-centred,
x right, y down, z toward the camera negative) with visibility, and up to two hands (21 image + world landmarks, handedness)."""
import json, sys, subprocess
import numpy as np
import mediapipe as mp
from mediapipe.tasks.python import vision, BaseOptions

video, out, models = sys.argv[1], sys.argv[2], sys.argv[3]
probe = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=width,height,r_frame_rate', '-of', 'csv=p=0', video], capture_output=True, text=True).stdout.strip().split(',')
w, h = int(probe[0]), int(probe[1]); num, den = probe[2].split('/'); fps = float(num) / float(den)
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', video, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], capture_output=True).stdout
frames = np.frombuffer(raw, np.uint8).reshape(-1, h, w, 3)

pose = vision.PoseLandmarker.create_from_options(vision.PoseLandmarkerOptions(base_options=BaseOptions(model_asset_path=models + '/pose_landmarker_heavy.task'), running_mode=vision.RunningMode.VIDEO, num_poses=1, min_pose_detection_confidence=.3, min_tracking_confidence=.3))
hands = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(base_options=BaseOptions(model_asset_path=models + '/hand_landmarker.task'), running_mode=vision.RunningMode.VIDEO, num_hands=2, min_hand_detection_confidence=.2, min_tracking_confidence=.2))
res = []
for i, f in enumerate(frames):
    img = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(f))
    ms = int(round(i * 1000 / fps))
    p = pose.detect_for_video(img, ms)
    hd = hands.detect_for_video(img, ms)
    fr = {'i': i}
    if p.pose_landmarks:
        fr['img'] = [[l.x, l.y, l.z, l.visibility] for l in p.pose_landmarks[0]]
        fr['world'] = [[l.x, l.y, l.z] for l in p.pose_world_landmarks[0]]
    fr['hands'] = []
    for k in range(len(hd.hand_landmarks)):
        fr['hands'].append({'side': hd.handedness[k][0].category_name, 'score': hd.handedness[k][0].score, 'img': [[l.x, l.y, l.z] for l in hd.hand_landmarks[k]], 'world': [[l.x, l.y, l.z] for l in hd.hand_world_landmarks[k]]})
    res.append(fr)
json.dump({'video': video.split('/')[-1], 'fps': fps, 'w': w, 'h': h, 'frames': res}, open(out, 'w'))
print(out, len(res), 'frames', sum(1 for r in res if 'world' in r), 'with a body', sum(len(r['hands']) for r in res), 'hands')
