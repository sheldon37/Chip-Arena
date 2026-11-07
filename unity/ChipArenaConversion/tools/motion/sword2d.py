"""Track the claymore's blade in 2D from the right hand: sword2d.py video clip.json out.json

For each frame, cast rays from the hand (MediaPipe right wrist/index/pinky/thumb) every degree and measure how far a bright,
narrow ridge (the blade, lighter than the grey backdrop on both sides) runs along each. The longest run is the blade: its
angle and its projected length (to the tip). Frames where the run is short (blur, the blade behind the body) are marked
unreliable for the solver to fill."""
import json, sys, subprocess, math
import numpy as np

video, js, out = sys.argv[1], sys.argv[2], sys.argv[3]
d = json.load(open(js)); w, h = d['w'], d['h']
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', video, '-f', 'rawvideo', '-pix_fmt', 'gray', '-'], capture_output=True).stdout
frames = np.frombuffer(raw, np.uint8).reshape(-1, h, w)
import cv2
# Thin structures only (the blade is 5-10 px across, the limbs much wider): the white and the black top-hat of each frame.
K = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (31, 31))
thin = np.array([np.maximum(cv2.morphologyEx(f, cv2.MORPH_TOPHAT, K), cv2.morphologyEx(f, cv2.MORPH_BLACKHAT, K)) for f in frames]).astype(np.float32)
frames = frames.astype(np.float32)


def sample(img, x, y):
    x = np.clip(x, 0, w - 1.001); y = np.clip(y, 0, h - 1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = x - x0; fy = y - y0
    a = img[y0, x0]; b = img[y0, x0 + 1]; c = img[y0 + 1, x0]; e = img[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + e * fx) * fy


ANG = np.radians(np.arange(0, 360, 1.0))
R = np.arange(14, 420, 2.0)
res = []
prev = None
for i, img in enumerate(frames):
    th = thin[i]
    f = d['frames'][i]
    if 'img' not in f:
        res.append(None); continue
    L = f['img']
    hand = np.mean([[L[k][0] * w, L[k][1] * h] for k in (16, 18, 20, 22)], axis=0)
    el = np.array([L[14][0] * w, L[14][1] * h]); ev = el - hand
    elbow_angle = float(np.degrees(np.arctan2(ev[1], ev[0]))) if np.hypot(*ev) > 12 else None
    dx = np.cos(ANG)[:, None]; dy = np.sin(ANG)[:, None]
    px = hand[0] + dx * R[None, :]; py = hand[1] + dy * R[None, :]
    centre = sample(img, px, py)
    best = None
    for off in (5, 7, 9, 12, 16):
        s1 = sample(img, px - dy * off, py + dx * off); s2 = sample(img, px + dy * off, py - dx * off)
        # Either polarity: a lit blade on the backdrop, or one in shadow darker than it (the lunge).
        ridge = np.maximum(centre - np.maximum(s1, s2), np.minimum(s1, s2) - centre)
        ok = ridge > 7
        # The run: from the first hit within 60 px of the hand, how far the ridge holds (gaps of up to 4 samples bridged).
        for a in range(len(ANG)):
            row = ok[a]
            start = np.argmax(row[:24]) if row[:24].any() else -1
            if start < 0: continue
            gap = 0; end = start
            for k in range(start, len(R)):
                if row[k]: end = k; gap = 0
                else:
                    gap += 1
                    if gap > 7: break
            run = R[end] - R[start]
            score = run * (1 + .15 * ridge[a, start:end + 1].clip(0, 40).mean() / 40)
            # Never back along the forearm (the blade can't pass through the arm).
            ea = elbow_angle
            if ea is not None and abs((np.degrees(ANG[a]) - ea + 180) % 360 - 180) < 18: continue
            if prev is not None:
                da = abs((np.degrees(ANG[a]) - prev + 180) % 360 - 180)
                score *= 1 - .35 * min(1, da / 90)
            if best is None or score > best[0]:
                best = (score, float(np.degrees(ANG[a])), float(R[end]), float(run), off)
    if best is None:
        res.append(None); continue
    a = math.radians(best[1]); rr = np.arange(14, 470, 1.0)
    tv = sample(th, hand[0] + math.cos(a) * rr, hand[1] + math.sin(a) * rr) > 10
    end = 0; gap = 0
    for k in range(len(rr)):
        if tv[k]: end = k; gap = 0
        elif rr[k] > 40:
            gap += 1
            if gap > 14: break
    best = (best[0], best[1], max(best[2], float(rr[end])), best[3], best[4])
    # Did the blade run off the frame before it ended? Then its length here is only a lower bound.
    ex = hand[0] + math.cos(a) * (best[2] + 10); ey = hand[1] + math.sin(a) * (best[2] + 10)
    clipped = not (2 <= ex <= w - 3 and 2 <= ey <= h - 3)
    prev = best[1]
    res.append({'angle': best[1], 'tip': best[2], 'run': best[3], 'hand': hand.tolist(), 'clipped': clipped})
json.dump({'video': d['video'], 'fps': d['fps'], 'w': w, 'h': h, 'blade': res}, open(out, 'w'))
runs = [r['run'] if r else 0 for r in res]
print(out, 'frames', len(res), 'median run %.0f' % np.median(runs), 'short(<80px)', sum(1 for r in runs if r < 80))
