# vidblade.py BAKED OUT [zoom specs]: the blade's angle in the picture on every frame of the real videos, against the baked rig's. Run vidcheck.py first: it fits the cameras (CAMDIR, default OUT).
# The blade is the long thin part of the figure's silhouette: the figure less its thick parts, the piece running out of the fist, its long axis by PCA.
# Also writes per-frame strips with the rig drawn over the video, for specs like 4:124,125,126.
import json,sys,os,subprocess,math
import numpy as np,cv2
HERE=os.path.dirname(os.path.abspath(__file__))
src=open(f'{HERE}/vidcheck.py',encoding='utf-8').read().split("def run(ci,name,vid,first):")[0]
BAKED=sys.argv[1];OUT=sys.argv[2];ZOOM=sys.argv[3:]
sys.argv=['x',BAKED,OUT]
exec(src)
NAMES={2:(0,'02-kneel-rise',60),3:(1,'03-pull-salute',0),4:(2,'04-raise-cut',0)}
CAMDIR=os.environ.get('CAMDIR',OUT)
def cam_of(c):return json.load(open(f'{CAMDIR}/{NAMES[c][1]}.cam.json'))['cam']
OFFS=np.array([(x,y) for x in range(-24,25,6) for y in range(-24,25,6)],np.float32)
TS=np.arange(70,200,4,dtype=np.float32)
def measure(m,gp,th0):
    # A thin line out of the fist: on the figure along its centre, off it on both sides 13 px away (a limb or the torso has figure on the sides too).
    # th0 is the rig's own angle: the search keeps within 70 degrees of it, so it can't lock onto the far side of the body.
    th=np.radians(th0+np.arange(-70,71,1.5,dtype=np.float32));ax=np.stack((np.cos(th),-np.sin(th)),1);nr=np.stack((ax[:,1],-ax[:,0]),1)
    base=gp[None,None,None,:]+OFFS[None,:,None,:]
    C=base+ax[:,None,None,:]*TS[None,None,:,None]
    def on(P):
        x=np.clip(P[...,0].astype(int),0,853);y=np.clip(P[...,1].astype(int),0,479);return m[y,x].astype(np.float32)
    S=on(C).mean(2)-.5*(on(C+nr[:,None,None,:]*26).mean(2)+on(C-nr[:,None,None,:]*26).mean(2))
    i,j=np.unravel_index(S.argmax(),S.shape)
    if S[i,j]<.6:return None
    a=ax[i]
    # how far the thin line runs, for the apparent length
    start=gp+OFFS[j];run=0
    for t in range(30,700,4):
        p=start+a*t;x,y=int(p[0]),int(p[1])
        if not(0<=x<854 and 0<=y<480) or not m[y,x]:break
        run=t
    # Any line inside the blade's width scores the same, so the search alone leans: the blade's own pixels in a corridor round that line, their long axis, twice over.
    ys,xs=np.nonzero(m);P=np.stack((xs,ys),1).astype(np.float32);n=np.array((a[1],-a[0]));st=start.copy()
    for _ in range(3):
        d=P-st;al=d@a;ac=d@n;keep=(al>55)&(al<max(run,120))&(np.abs(ac)<24)
        if keep.sum()<400:return None
        Q=P[keep];mu=Q.mean(0);_,_,vt=np.linalg.svd(Q-mu,full_matrices=False);v=vt[0]
        if v@a<0:v=-v
        a=v;n=np.array((a[1],-a[0]));st=mu-a*((mu-st)@a)
    return float(S[i,j]),a,float(run)
def ang(v):return math.degrees(math.atan2(-v[1],v[0]))
res={}
for c,(ci,name,first) in NAMES.items():
    cl=d['clips'][ci];cam=cam_of(c);V=frames(f'{CLIPS}/{c:02d}-intro-reference.mp4');rows=[]
    for f in range(first,len(V)):
        pos,g,t=fk(cl,f);gp=proj(g,cam);tp=proj(t,cam);rv=tp-gp;m=silhouette(V[f]);b=measure(m,gp,ang(rv))
        rows.append((f,ang(rv),np.linalg.norm(rv),(ang(b[1]) if b else float('nan')),(b[2] if b else float('nan')),cl['plant'][f]>.5,cl['swordFilled'][f],cl['corrected'][f]))
    res[c]=rows
    print('==',name,'px/m %.0f'%cam[0])
    last=None;line=[]
    for r in rows:
        if r[0]%2 or math.isnan(r[3]) or r[5]:continue
        e=(r[3]-r[1]+180)%360-180;line.append(f'{r[0]}:{e:+.0f}')
    print(' blade picture-angle error (video minus rig), every 4th unplanted frame:',' '.join(line))
    E=np.array([(r[0],(r[3]-r[1]+180)%360-180,r[4],r[2]) for r in rows if not r[5] and not math.isnan(r[3])])
    if len(E):
        print('  frames read',len(E),'median |err| %.1f, 95th %.1f, worst %.1f @%d'%(np.median(abs(E[:,1])),np.percentile(abs(E[:,1]),95),abs(E[:,1]).max(),E[abs(E[:,1]).argmax(),0]))
    np.save(f'{OUT}/{name}-blade.npy',np.array([(r[0],r[1],r[2],r[3],r[4]) for r in rows]))
for z in ZOOM:
    c,fs=z.split(':');c=int(c);fl=[int(x) for x in fs.split(',')];ci,name,first=NAMES[c];cl=d['clips'][ci];cam=cam_of(c);V=frames(f'{CLIPS}/{c:02d}-intro-reference.mp4')
    tw,th=427,240;cols=6;rows=(len(fl)+cols-1)//cols;sheet=np.zeros((rows*th,cols*tw,3),np.uint8)
    for i,f in enumerate(fl):
        im=V[f].copy();pos,g,t=fk(cl,f)
        for k,L in LIMBS.items():
            for a,b in L:cv2.line(im,tuple(int(v) for v in proj(pos[a],cam)),tuple(int(v) for v in proj(pos[b],cam)),COL[k],2,cv2.LINE_AA)
        cv2.line(im,tuple(int(v) for v in proj(g,cam)),tuple(int(v) for v in proj(t,cam)),COL['blade'],2,cv2.LINE_AA)
        cv2.putText(im,f'{c:02d} f{f}',(8,28),cv2.FONT_HERSHEY_SIMPLEX,.9,(0,255,255),2,cv2.LINE_AA)
        sheet[(i//cols)*th:(i//cols+1)*th,(i%cols)*tw:(i%cols+1)*tw]=cv2.resize(im,(tw,th),interpolation=cv2.INTER_AREA)
    cv2.imwrite(f"{OUT}/zoom{c:02d}-{fl[0]}.jpg",sheet,[cv2.IMWRITE_JPEG_QUALITY,90])
