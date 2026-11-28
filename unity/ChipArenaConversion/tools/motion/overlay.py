"""Draw the extracted 2D skeleton over a clip's frames and tile them in a sheet: overlay.py video json out.jpg step [cols]."""
import json, sys, subprocess
import numpy as np, cv2

video, js, out, step = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
cols = int(sys.argv[5]) if len(sys.argv) > 5 else 6
d = json.load(open(js)); w, h = d['w'], d['h']
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', video, '-f', 'rawvideo', '-pix_fmt', 'bgr24', '-'], capture_output=True).stdout
frames = np.frombuffer(raw, np.uint8).reshape(-1, h, w, 3)
BONES = [(11, 12), (11, 23), (12, 24), (23, 24), (11, 13), (13, 15), (12, 14), (14, 16), (23, 25), (25, 27), (24, 26), (26, 28), (27, 31), (28, 32), (15, 19), (16, 20), (0, 11), (0, 12)]
tiles = []
for i in range(0, len(frames), step):
    im = frames[i].copy(); f = d['frames'][i]
    if 'img' in f:
        P = [(int(l[0] * w), int(l[1] * h)) for l in f['img']]
        for a, b in BONES:
            col = (0, 0, 255) if (a % 2 == 0 and b % 2 == 0 and a > 10) else (255, 120, 0) if a > 10 and a % 2 == 1 and b % 2 == 1 else (0, 220, 0)
            cv2.line(im, P[a], P[b], col, 2)
    for hd in f.get('hands', []):
        for l in hd['img']:
            cv2.circle(im, (int(l[0] * w), int(l[1] * h)), 2, (0, 255, 255), -1)
    s = extra = ''
    if 'extra' in d and str(i) in d['extra']: extra = d['extra'][str(i)]
    cv2.putText(im, '%s f%d %.2fs %s' % (d['video'][:14], i, i / d['fps'], extra), (6, 20), cv2.FONT_HERSHEY_SIMPLEX, .55, (0, 255, 255), 1)
    tiles.append(cv2.resize(im, (w // 2, h // 2)))
while len(tiles) % cols: tiles.append(np.zeros_like(tiles[0]))
rows = [np.hstack(tiles[r:r + cols]) for r in range(0, len(tiles), cols)]
cv2.imwrite(out, np.vstack(rows), [cv2.IMWRITE_JPEG_QUALITY, 82])
print(out, len(tiles))
