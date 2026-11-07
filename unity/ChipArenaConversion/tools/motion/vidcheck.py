# vidcheck.py BAKED OUT: NOCTIEL's baked intro against the owner's real videos, on every frame, without Unity. CLIPDIR holds 02-intro-reference.mp4 and so on (never in git).
# The figure's silhouette is cut from the plain grey backdrop; the baked rig (bones and blade) is projected by one camera fitted per clip;
# for each limb, how far its bones stand outside the silhouette in the picture (pixels at 854x480). Depth can't be read this way, only the picture's plane.
import json,subprocess,sys,os
import numpy as np,cv2
from scipy.spatial.transform import Rotation as Rot
from scipy.optimize import minimize
HERE=os.path.dirname(os.path.abspath(__file__))
CLIPS=os.environ.get('CLIPDIR','.')
BAKED,OUT=sys.argv[1],sys.argv[2]
BLADE_LEN=float(os.environ.get('BLADE_LEN','1.65'))
os.makedirs(OUT,exist_ok=True)
d=json.load(open(BAKED));bones=d['bones'];rb=d['restBones'];par=d['restParent'];RP=np.array(d['restPos']).reshape(-1,3);nb=len(bones)
turn=[]
for r in range(len(rb)):
    k=-1;a=r
    while a>=0 and k<0:
        k=bones.index(rb[a]) if rb[a] in bones else -1;a=par[a]
    turn.append(k)
def fk(c,f):
    Q=np.array(c['q'][f*nb*4:(f+1)*nb*4]).reshape(-1,4);M=Rot.from_quat(Q).as_matrix();pos={};root=np.array(c['root'][f*3:f*3+3])
    for r,n in enumerate(rb):
        p=par[r];pos[n]=root if p<0 else pos[rb[p]]+M[turn[p]]@(RP[r]-RP[p])
    g=np.array(c['grip'][f*3:f*3+3]);b=np.array(c['blade'][f*3:f*3+3]);pl=c['plant'][f]>.5
    b=np.array((0,-1.,0)) if pl else b/max(1e-6,np.linalg.norm(b))
    return pos,g,g+b*BLADE_LEN
LIMBS={'spine':[('pelvis','spine_02'),('spine_02','spine_03'),('spine_03','spine_05'),('spine_05','neck_01'),('neck_01','head')],
 'armL':[('upperarm_l','lowerarm_l'),('lowerarm_l','hand_l')],'armR':[('upperarm_r','lowerarm_r'),('lowerarm_r','hand_r')],
 'legL':[('thigh_l','calf_l'),('calf_l','foot_l'),('foot_l','ball_l')],'legR':[('thigh_r','calf_r'),('calf_r','foot_r'),('foot_r','ball_r')]}
COL={'spine':(90,220,90),'armL':(240,140,40),'armR':(60,60,240),'legL':(240,180,90),'legR':(120,90,250),'blade':(0,240,240)}
def samples(pos,g,t):
    S={k:[] for k in LIMBS};S['blade']=[]
    for k,L in LIMBS.items():
        for a,b in L:
            for u in np.linspace(0,1,7):S[k].append(pos[a]*(1-u)+pos[b]*u)
    for u in np.linspace(.04,1,24):S['blade'].append(g*(1-u)+t*u)
    return {k:np.array(v) for k,v in S.items()}
def frames(p):
    r=subprocess.run(['ffmpeg','-v','error','-i',p,'-f','rawvideo','-pix_fmt','bgr24','-'],capture_output=True).stdout
    return np.frombuffer(r,np.uint8).reshape(-1,480,854,3)
def silhouette(im):
    g=cv2.cvtColor(im,cv2.COLOR_BGR2GRAY).astype(np.float32)
    row=np.median(g,axis=1,keepdims=True);col=np.median(g-row,axis=0,keepdims=True)
    m=(np.abs(g-row-col)>11).astype(np.uint8)
    m=cv2.morphologyEx(m,cv2.MORPH_OPEN,np.ones((2,2),np.uint8));m=cv2.morphologyEx(m,cv2.MORPH_CLOSE,np.ones((5,5),np.uint8))
    n,l,st,_=cv2.connectedComponentsWithStats(m,8)
    for i in range(1,n):
        if st[i,cv2.CC_STAT_AREA]<60:m[l==i]=0
    return m
def proj(P,cam):
    s,cx,cy=cam;return np.stack((cx-P[...,0]*s,cy-P[...,1]*s),axis=-1)
def dt(m):return cv2.distanceTransform((m==0).astype(np.uint8),cv2.DIST_L2,3)
def run(ci,name,vid,first):
    c=d['clips'][ci];V=frames(f'{CLIPS}/{vid}');N=len(V);M=[silhouette(V[i]) for i in range(N)];DT=[dt(m) for m in M]
    use=list(range(first,N,6))
    S0={f:samples(*fk(c,f)) for f in range(first,N)}
    def cost(x):
        tot=0
        for f in use:
            cam=x;pts=np.concatenate([v for v in S0[f].values()]);uv=proj(pts,cam);xi=np.clip(uv[:,0].astype(int),0,853);yi=np.clip(uv[:,1].astype(int),0,479)
            a=DT[f][yi,xi].mean()
            rig=np.zeros((480,854),np.uint8);rig[yi,xi]=1;D=cv2.distanceTransform(1-rig,cv2.DIST_L2,3);b=D[M[f]>0]
            tot+=a+.5*(b.mean() if len(b) else 0)
        return tot/len(use)
    best=None
    for s0 in (130,160,190):
        r=minimize(cost,(s0,427,440),method='Nelder-Mead',options=dict(xatol=.3,fatol=.01,maxiter=220))
        if best is None or r.fun<best.fun:best=r
    cam=best.x;print(name,'camera px/m %.1f cx %.1f floor y %.1f chamfer %.2f'%(cam[0],cam[1],cam[2],best.fun),flush=True)
    rows=[];keys=['spine','armL','armR','legL','legR','blade']
    for f in range(first,N):
        S=S0[f];row=[f]
        for k in keys:
            uv=proj(S[k],cam);xi=np.clip(uv[:,0].astype(int),0,853);yi=np.clip(uv[:,1].astype(int),0,479);row.append(float(DT[f][yi,xi].max()))
        ys,xs=np.nonzero(M[f]);row+=[float(np.median(xs)) if len(xs) else 0]
        pts=proj(np.concatenate([S[k] for k in LIMBS]),cam);row+=[float(np.median(pts[:,0]))];rows.append(row)
    R=np.array(rows);np.save(f'{OUT}/{name}.npy',R)
    print(' per limb px outside (median / 95th / worst@frame):')
    for j,k in enumerate(keys):
        v=R[:,j+1];i=int(v.argmax());print(f'  {k:6s} {np.median(v):5.1f} {np.percentile(v,95):5.1f} {v[i]:6.1f} @{int(R[i,0])}')
    # stretches where any limb stands more than 20 px (about 0.13 rest m) outside
    for j,k in enumerate(keys):
        bad=R[:,j+1]>20;runs=[];a=None
        for i,b in enumerate(bad):
            if b and a is None:a=i
            if not b and a is not None:runs.append((int(R[a,0]),int(R[i-1,0])));a=None
        if a is not None:runs.append((int(R[a,0]),int(R[-1,0])))
        if runs:print(f'  over 20 px {k}: {runs}')
    def draw(f):
        im=V[f].copy();pos,g,t=fk(c,f)
        mm=M[f];edge=cv2.morphologyEx(mm,cv2.MORPH_GRADIENT,np.ones((3,3),np.uint8))
        for k,L in LIMBS.items():
            for a,b in L:
                pa=proj(pos[a],cam).astype(int);pb=proj(pos[b],cam).astype(int);cv2.line(im,tuple(int(v) for v in pa),tuple(int(v) for v in pb),COL[k],2,cv2.LINE_AA)
        pa=proj(g,cam).astype(int);pb=proj(t,cam).astype(int);cv2.line(im,tuple(int(v) for v in pa),tuple(int(v) for v in pb),COL['blade'],2,cv2.LINE_AA)
        fl=int(c['corrected'][f]);cv2.putText(im,f'{name[:2]} f{f} fix{fl}',(8,24),cv2.FONT_HERSHEY_SIMPLEX,.7,(0,255,255),2,cv2.LINE_AA)
        return im
    fs=list(range(first,N,4));cols=8;rowsn=(len(fs)+cols-1)//cols;tw,th=320,180;sheet=np.zeros((rowsn*th,cols*tw,3),np.uint8)
    for i,f in enumerate(fs):
        sheet[(i//cols)*th:(i//cols+1)*th,(i%cols)*tw:(i%cols+1)*tw]=cv2.resize(draw(f),(tw,th),interpolation=cv2.INTER_AREA)
    cv2.imwrite(f'{OUT}/{name}.jpg',sheet,[cv2.IMWRITE_JPEG_QUALITY,88])
    # the masks of a few frames, to see the cut itself
    fm=[first,(first+N)//2,N-1];cv2.imwrite(f'{OUT}/{name}-mask.jpg',np.hstack([cv2.resize(M[f]*255,(427,240)) for f in fm]))
    json.dump(dict(cam=[float(v) for v in cam]),open(f'{OUT}/{name}.cam.json','w'))
run(0,'02-kneel-rise','02-intro-reference.mp4',60)
run(1,'03-pull-salute','03-intro-reference.mp4',0)
run(2,'04-raise-cut','04-intro-reference.mp4',0)
