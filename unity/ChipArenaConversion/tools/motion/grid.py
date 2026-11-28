# grid.py CLIP "f,f,f" OUT [x0 y0 x1 y1 crop in video px] [cols] [zoom]: real video frames at full size with a ruler in the rig's own metres (the clip's fitted camera), no rig drawn.
import sys,json,subprocess,os
import numpy as np,cv2
CAMS=os.environ.get('CAMDIR','.');CLIPS=os.environ.get('CLIPDIR','.')
c=int(sys.argv[1]);fl=[int(x) for x in sys.argv[2].split(',')];out=sys.argv[3]
crop=[int(x) for x in sys.argv[4:8]] if len(sys.argv)>7 else [0,0,854,480];cols=int(sys.argv[8]) if len(sys.argv)>8 else 4;zm=float(sys.argv[9]) if len(sys.argv)>9 else 1.5
NAME={2:'02-kneel-rise',3:'03-pull-salute',4:'04-raise-cut'}[c]
s,cx,cy=json.load(open(f'{CAMS}/{NAME}.cam.json'))['cam']
raw=subprocess.run(['ffmpeg','-v','error','-i',f'{CLIPS}/{c:02d}-intro-reference.mp4','-f','rawvideo','-pix_fmt','bgr24','-'],capture_output=True).stdout
V=np.frombuffer(raw,np.uint8).reshape(-1,480,854,3)
tiles=[]
for f in fl:
    im=V[f].copy()
    for xm in np.arange(-1.6,1.61,.1):
        u=int(cx-xm*s)
        if 0<=u<854:
            cv2.line(im,(u,0),(u,479),(255,255,0) if abs(xm%.5)<.01 or abs(xm%.5-.5)<.01 else (120,120,70),1)
            if abs(xm%.5)<.01 or abs(xm%.5-.5)<.01:cv2.putText(im,f'{xm:+.1f}',(u+2,max(crop[1]+12,12)),cv2.FONT_HERSHEY_SIMPLEX,.35,(255,255,0),1,cv2.LINE_AA)
    for ym in np.arange(0,2.4,.1):
        v=int(cy-ym*s)
        if 0<=v<480:
            cv2.line(im,(0,v),(853,v),(255,255,0) if abs(ym%.5)<.01 or abs(ym%.5-.5)<.01 else (120,120,70),1)
            if abs(ym%.5)<.01 or abs(ym%.5-.5)<.01:cv2.putText(im,f'{ym:.1f}',(max(crop[0]+2,2),v-2),cv2.FONT_HERSHEY_SIMPLEX,.35,(255,255,0),1,cv2.LINE_AA)
    t=im[crop[1]:crop[3],crop[0]:crop[2]]
    t=cv2.resize(t,None,fx=zm,fy=zm,interpolation=cv2.INTER_CUBIC)
    cv2.putText(t,f'{c:02d} f{f}',(6,t.shape[0]-8),cv2.FONT_HERSHEY_SIMPLEX,.8,(0,255,255),2,cv2.LINE_AA);tiles.append(t)
rows=[np.hstack(tiles[i:i+cols]+[np.zeros_like(tiles[0])]*(cols-len(tiles[i:i+cols]))) for i in range(0,len(tiles),cols)]
cv2.imwrite(out,np.vstack(rows),[cv2.IMWRITE_JPEG_QUALITY,92]);print(out,np.vstack(rows).shape)
