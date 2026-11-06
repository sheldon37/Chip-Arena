import json,sys,subprocess,numpy as np,cv2,math
video,sj,out,step=sys.argv[1],sys.argv[2],sys.argv[3],int(sys.argv[4])
d=json.load(open(sj));w,h=d['w'],d['h']
raw=subprocess.run(['ffmpeg','-v','error','-i',video,'-f','rawvideo','-pix_fmt','bgr24','-'],capture_output=True).stdout
F=np.frombuffer(raw,np.uint8).reshape(-1,h,w,3)
tiles=[]
for i in range(0,len(F),step):
    im=F[i].copy();b=d['blade'][i]
    if b:
        a=math.radians(b['angle']);hx,hy=b['hand']
        col=(0,255,0) if b['run']>=80 else (0,0,255)
        cv2.line(im,(int(hx),int(hy)),(int(hx+math.cos(a)*b['tip']),int(hy+math.sin(a)*b['tip'])),col,2);cv2.circle(im,(int(hx),int(hy)),4,(255,0,255),-1)
    cv2.putText(im,'f%d run %s'%(i,int(b['run']) if b else '-'),(6,20),cv2.FONT_HERSHEY_SIMPLEX,.6,(0,255,255),2)
    tiles.append(cv2.resize(im,(w//2,h//2)))
cols=6
while len(tiles)%cols:tiles.append(np.zeros_like(tiles[0]))
cv2.imwrite(out,np.vstack([np.hstack(tiles[r:r+cols]) for r in range(0,len(tiles),cols)]),[cv2.IMWRITE_JPEG_QUALITY,80])
