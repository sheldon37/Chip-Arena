"""Author the Carillon mesh bank in Unity metres, Y up. No commercial assets.
Run with Python 3 from any directory. Named pieces have a local metre-space pivot.
"""
from collections import OrderedDict
from math import sin, cos, pi, sqrt, atan2
from pathlib import Path
import random
import struct

ROOT = Path(__file__).resolve().parents[2]
TAU = 2*pi
ROCK, SLATE, JADE, ENAMEL = '394652', '536a70', '386e69', '92cbc1'
COPPER, GOLD, DARK, BONE = 'a16946', 'dfac64', '263b45', 'e2d2aa'
AQUA, AMBER = '57e2d1', 'ffd17c'
pieces = OrderedDict()
rng = random.Random(671)
def piece(name, pivot=(0,0,0)):
    global vertices, faces
    vertices, faces = [], []
    pieces[name] = (pivot, vertices, faces)

def plus(a,b): return tuple(x+y for x,y in zip(a,b))
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def mul(a,k): return tuple(x*k for x in a)
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a): return mul(a,1/max(.00001,sqrt(sum(x*x for x in a))))
def color(c):
    return (*bytes.fromhex(c), 240 if c==AQUA else 205 if c==AMBER else 108 if c in (COPPER,GOLD,DARK) else 0)
def strips(rows,c,wrap=True):
    start=len(vertices); n=len(rows[0]); colors=[color(c)]*len(rows) if isinstance(c,str) else [color(v) for v in c]
    for i,row in enumerate(rows):
        vertices.extend((v,colors[i]) for v in row)
    for i in range(len(rows)-1):
        for j in range(n if wrap else n-1):
            a=start+i*n+j;b=start+i*n+(j+1)%n
            faces.extend(((a,a+n,b),(b,a+n,b+n)))
def lathe(x,y,z,profile,c,n=36,stretch=1):
    strips([[(x+r*cos(TAU*i/n),y+h,z+r*stretch*sin(TAU*i/n)) for i in range(n)] for h,r in profile],c)
def tube(path,r,c,n=7):
    rows=[]
    for i,p in enumerate(path):
        direction=unit(sub(path[min(i+1,len(path)-1)],path[max(0,i-1)]))
        side=unit(cross(direction,(0,0,1) if abs(direction[2])<.85 else (1,0,0)))
        # Consistent frame for paths with a sharp change in direction (mostly slender round rails).
        up=unit(cross(direction,side))
        rows.append([plus(p,plus(mul(side,r*cos(TAU*j/n)),mul(up,r*sin(TAU*j/n)))) for j in range(n)])
    strips(rows,c)
def ring(x,y,z,r,w,c,n=64,stretch=1):
    lathe(x,y,z,[(-w*.4,r-w),(0,r+w),(.07+w*.2,r+w),(.1+w*.2,r-w),(-w*.4,r-w)],c,n,stretch)
def slab(x,y,z,w,d,h,c,bevel=.18):
    """Architectural octagonal plinth with separate plinth, chamfer and coping."""
    def row(inset,height):
        a=w/2-inset;b=d/2-inset;k=min(bevel,a*.4,b*.4)
        return [(x+i,y+height,z+j) for i,j in [(-a+k,-b),(a-k,-b),(a,-b+k),(a,b-k),(a-k,b),(-a+k,b),(-a,b-k),(-a,-b+k)]]
    lip=min(.09,h*.2,w*.09,d*.09)
    rows=[row(lip,0),row(0,lip),row(0,h-lip),row(lip,h)]
    strips(rows,c);a=len(vertices);vertices.extend((v,color(c)) for v in rows[-1]);faces.extend((a,a+i+1,a+i) for i in range(1,7))
def axis_arch(a,b,height,c,r):
    path=[(a[0]+(b[0]-a[0])*i/32,height*(sin(pi*i/32)**.71),a[1]+(b[1]-a[1])*i/32) for i in range(33)]
    tube(path,r,c,12)
def bell(x,y,z,s,c):
    # Flared lip curls back into the dark throat; moulding rings follow the cast profile.
    prof=[(0,1.22),(.10,1.31),(.20,1.29),(.30,1.12),(.53,.92),(.94,.75),(1.35,.66),(1.55,.58),(1.69,.31),(1.72,.03)]
    lathe(x,y,z,[(h*s,r*s) for h,r in prof],c,48)
    lathe(x,y,z,[(.02*s,1.22*s),(.10*s,1.08*s),(.67*s,.68*s)],DARK,48)
    for h,r in [(.14,1.3),(.32,1.115),(1.31,.69),(1.48,.62)]: ring(x,y+h*s,z,r*s,.025*s,GOLD,48)
    tube([(x,y+.72*s,z),(x,y+.02*s,z)],.055*s,COPPER)
    lathe(x,y-.14*s,z,[(0,.02*s),(.08*s,.14*s),(.21*s,.16*s),(.29*s,.02*s)],GOLD,24)
def sector(r0,r1,t0,t1,y,c,n=8):
    strips([[(r*cos(t0+(t1-t0)*j/n),y,r*sin(t0+(t1-t0)*j/n)) for j in range(n+1)] for r in (r0,r1)],c,False)
def deck():
    piece('terraces')
    slab(0,-1.6,0,54,40,1.6,SLATE,1.5)
    # Cut radial paving and brass instrument scales. Thin inlays give players a sense of distance.
    for band in range(5):
        r=1.6+band*1.65
        for j in range(24):
            t=TAU*(j+.5*(band%2))/24
            sector(r,r+1.61,t+.004,t+TAU/24-.004,.015,(SLATE,'657b7a','586e71','687e7d')[(band+3*j)%4])
        ring(0,.024,0,r,.024,GOLD)
    for j in range(96):
        a=j*TAU/96
        sector(9.93,10.42 if j%4==0 else 10.15,a-.004,a+.004,.027,GOLD,1)
    # Alternating salt-fired pavers on the outer terrace with brass-wire inlays to each opposite well.
    for iz in range(-8,9):
        for ix in range(-10,11):
            x,z=2.44*ix+1.22*(iz%2),2.16*iz
            if x*x+z*z<116 or abs(x)>25 or abs(z)>18:continue
            slab(x,-.10,z,2.42,2.135,.112,('546d70','5a7477','627a78','53696e')[(ix*17+iz*7)%4],.11)
    for side in (-1,1):
        for strand in (-1,1):
            route=[(side*12,.048,side*4+strand*.07),(side*11,.048,side*2+strand*.07),(side*8,.048,side*2+strand*.07),
                   (side*5,.048,-side*1+strand*.07),(-side*2,.048,-side*1+strand*.07),(-side*5+strand*.07,.048,-side*4),(-side*5+strand*.07,.048,-side*8)]
            tube(route,.027,AMBER if side<0 else AQUA,5)
        z=side*13
        slab(0,0,z,14,5,2.86,JADE,.3);slab(0,2.86,z,14,5,.14,BONE,.17)
        for x in (-1,1):
            # Gallery slopes built as continuous faced wedges with narrow brass nosings.
            p=[(x*7,3,z-2.5),(x*15,0,z-2.5),(x*15,0,z+2.5),(x*7,3,z+2.5)]
            strips([[p[0],p[3]], [p[1],p[2]]],BONE,False)
            for edge in (-1,1):
                zz=z+2.5*edge
                strips([[(x*7,0,zz),(x*15,0,zz)],[(x*7,3,zz),(x*15,.02,zz)]],JADE,False)
            for j in range(1,12):
                t=j/12
                slab(x*(7+8*t),3*(1-t)+.012,z,.045,4.84,.025,COPPER,.02)
        for x in range(-6,7,2):
            for sidewall in (-1,1):
                tube([(x-.7,.15,z+2.51*sidewall),(x,2.45,z+2.51*sidewall),(x+.7,.15,z+2.51*sidewall)],.06,COPPER)
    for x in (-4.4,4.4):
        for z in (-4.4,4.4):
            lathe(x,0,z,[(0,1.34),(.25,1.34),(.38,1.14),(.7,1.12),(2.8,.87),(3.8,.86),(4.9,1.14),(5.2,1.2)],JADE,8)
            for y,r in [(.32,1.23),(.75,1.1),(3.9,.89),(4.8,1.14)]:ring(x,y,z,r,.06,GOLD,8)
            # Split wishbone trusses hold a suspended instrument, leaving the combat court clear.
            for k in (-1,1):
                path=[(x*(1-.55*t)+k*.29*sin(pi*t),5+9*t+2.2*sin(pi*t),z*(1-.55*t)-k*.3*sin(pi*t)) for t in [i/24 for i in range(25)]]
                tube(path,.22,COPPER,10)
    for sign in (-1,1):
        z=sign*4.4
        for h in [5.8,6.15]:
            tube([(-4.4+8.8*i/32,5.06+(h-5.06)*sin(pi*i/32),z) for i in range(33)],.13,GOLD,9)
    for r,w,h in [(2.8,.17,14.4),(3.1,.09,14.9),(2.85,.1,14.95)]:ring(0,h,0,r,w,GOLD)
def backdrop():
    piece('horizon')
    for j in range(22):
        a=TAU*j/22+.038;r=rng.uniform(68,93);x,z=r*cos(a),r*sin(a);h=rng.uniform(8,24)
        n=9;profile=[(-37,.14),(-19,.60),(-9,1),(0,.92),(.5,.59),(1,.09)]
        rad=rng.uniform(6,12);top=rng.uniform(-10,1.4)
        strips([[(x+rad*k*cos(i*TAU/n),top+y+(sin(3*i+.8*j)*1.3 if y>=-9 else 0),z+rad*k*sin(i*TAU/n)) for i in range(n)] for y,k in profile],[DARK,ROCK,SLATE,JADE,JADE,SLATE])
        if j%3==0:
            tube([(x-4+8*t,top+h*sin(pi*t)**.71,z) for t in [i/30 for i in range(31)]],.48,SLATE,10)
            tube([(x,top+h,z),(x,h*.6+2.52,z)],.078,GOLD,8)
            bell(x,h*.6,z,1.45,COPPER)
    # The carillon was cast into a single basalt island. Fluted strata run below its coping.
    for j in range(64):
        a=TAU*j/64
        x,z=27.6*cos(a),21.2*sin(a)
        if abs(x)<17 and abs(z)<18:continue
        depth=1.8+3.0*(.5+.5*sin(j*2.4));rings=[]
        for yy,fac,rad in [(-12-depth,.77,.78),(-6,.92,1.75),(-1.5,.98,2.04),(-.19,1,1.72)]:
            rings.append([(x*fac+rad*cos(k*TAU/7),yy+.09*sin(k+j),z*fac+rad*sin(k*TAU/7)) for k in range(7)])
        strips(rings,[DARK,ROCK,SLATE,JADE])
    # Perimeter colonnades, ceramic louvers and ship-sized bell pipes in the storm beyond the parapet.
    for j in range(16):
        a=TAU*(j+.5)/16;x,z=31*cos(a),24*sin(a);tall=6+4*(.5+.5*cos(3*a))
        for offset in (-1,1):
            q=(x+2.4*offset*sin(a),z-2.4*offset*cos(a))
            axis_arch(q,(x+5*cos(a),z+5*sin(a)),tall,JADE,.47)
            tube([(q[0],-.6,q[1]),(q[0],1.1,q[1])],.35,GOLD)
        # Each pair of arches rises from a visible tiered island footing.
        lathe(x,-6,z,[(0,1.1),(2.2,1.8),(4.0,3.6),(5.8,3.9),(6,3.6),(6.02,.03)],SLATE,11)
        if j%2==0:
            for k in range(3):
                xx=x+k*.8*sin(a);zz=z-k*.8*cos(a);hh=tall+1.7*k
                lathe(xx,-1,zz,[(0,.36),(hh-.9,.39),(hh-.42,.56),(hh,.61),(hh+.02,.48),(hh-.75,.32)],COPPER,20)
def landmark():
    piece('heart',(0,13.85,0))
    bell(0,-6.05,0,3.45,COPPER)
    # Raised maker's cartouches and tiered sound bow mouldings.
    for j in range(12):
        a=TAU*j/12
        path=[]
        for k in range(33):
            b=TAU*k/32;angle=a+.13*cos(b);height=.69*sin(b)
            r=2.83-.34*height
            path.append((r*cos(angle),-3.63+height,r*sin(angle)))
        tube(path,.065,GOLD,7)
    for j in range(20):
        a=j*TAU/20
        path=[((2.35+.9*(1-t)**2)*cos(a),-4.9+3.55*t,(2.35+.9*(1-t)**2)*sin(a)) for t in [i/12 for i in range(13)]]
        tube(path,.047,ENAMEL)
    piece('orrery',(0,11.4,0))
    for r in (5.35,5.62):ring(0,0,0,r,.065,GOLD)
    for i in range(60):
        a=i*TAU/60;sector(5.24,5.82 if i%5==0 else 5.55,a-.009,a+.009,.06,AMBER,1)
    for j in range(8):
        a=j*TAU/8;d=(cos(a),0,sin(a));tangent=(-sin(a),0,cos(a))
        piece('petal'+str(j),mul(d,3.35)[:1]+(10.6,)+mul(d,3.35)[2:])
        path=[];rows=[]
        for i in range(21):
            u=i/20;w=.055+1.13*sin(pi*u)**.72
            center=plus(mul(d,1.5*u+1.0*sin(pi*u)),(0,5.8*u,0));path.append(center)
            rows.append([plus(plus(center,mul(tangent,w*v)),mul(d,.38*(1-v*v)*sin(pi*u))) for v in (-1,-.5,0,.5,1)])
        strips(rows,JADE,False)
        for k in (0,2,4):tube([row[k] for row in rows],.075 if k!=2 else .055,GOLD if k!=2 else AQUA)
        for i in (5,9,13,17):tube(rows[i],.035,COPPER)
def props():
    for sign in (-1,1):
        z=sign*4.4
        piece('gate'+str(sign),(0,0,z))
        for j in range(-5,6):
            xx=j*.56;h=4.62-.11*abs(j)
            slab(xx,0,0,.575,.51,h,JADE,.095)
            tube([(xx-.19,.25,-.27),(xx,h*.63,-.27),(xx+.19,h-.15,-.27)],.055,GOLD)
            tube([(xx-.19,.25,.27),(xx,h*.63,.27),(xx+.19,h-.15,.27)],.055,GOLD)
        piece('well'+str(sign),(sign*5,.022,sign*8))
        lathe(0,0,0,[(0,1.56),(.13,1.47),(.13,1.23),(.04,1.19)],COPPER)
        for j in range(12):
            a=j*TAU/12
            sector(.20,1.19,a,a+.30,.12,JADE)
        ring(0,.13,0,1.23,.045,AQUA)
        piece('amber-stop' if sign<0 else 'jade-stop',(sign*12,1.1,sign*4))
        lathe(0,-1.1,0,[(0,.89),(.22,.92),(.35,.65),(.91,.47),(1.32,.66),(1.62,.78),(1.93,.52),(2.20,.12)],BONE,12,.83)
        for h,r in [(-.85,.87),(-.1,.53),(.45,.77)]:ring(0,h,0,r,.063,AMBER if sign<0 else AQUA,24,.83)
        piece('votive'+str(sign),(sign*17,1.35,-sign*5))
        for j in range(5):
            slab((j-2)*.67,-1.35,0,.68,.67,2.55+.13*cos(j*pi/2),ENAMEL,.15)
            for face in (-1,1):
                tube([((j-2)*.67-.25,-1.15,.35*face),((j-2)*.67,.27,.35*face),((j-2)*.67+.24,1.05,.35*face)],.035,COPPER)
        piece('drum'+str(sign),(sign*8,.85,-sign*8))
        lathe(0,0,0,[(-.85,.68),(-.75,.80),(-.60,.74),(0,.80),(.62,.74),(.73,.8),(.83,.76),(.85,.02)],COPPER,20)
        for y in (-.7,0,.72):ring(0,y,0,.78,.045,GOLD,24)
        piece('sentinel-base'+str(sign),(sign*20,0,sign*8))
        lathe(0,0,0,[(0,.76),(.3,.76),(.56,.48),(1.75,.27),(2.48,.47),(2.66,.31)],JADE,12)
        piece('sentinel'+str(sign),(sign*20,2.78,sign*8))
        bell(0,-.7,0,.40,GOLD)
    for name,z in [('grate',7.8),('bellows',-7.8)]:
        piece(name,(0,.02,z))
        slab(0,0,0,4,3,.10,DARK,.55)
        for i in range(9):
            x=(i-4)*.4
            if name=='grate':
                tube([(x-.13,.13,-1.28),(x+.15,.13,0),(x-.13,.13,1.28)],.065,AQUA)
            else:
                slab(x,.09,0,.18,2.37,.12,COPPER,.07)
        for edge in (-1,1):slab(0,.1,edge*1.37,3.45,.08,.07,GOLD,.03)
def write(path):
    with open(path,'wb') as out:
        out.write(struct.pack('<4sH',b'CAR1',len(pieces)))
        for name,(pivot,vs,fs) in pieces.items():
            key=name.encode();out.write(struct.pack('<B',len(key))+key+struct.pack('<3fII',*pivot,len(vs),len(fs)))
            for v,c in vs:out.write(struct.pack('<3f4B',*v,*c))
            for face in fs:out.write(struct.pack('<3I',*face))
if __name__=='__main__':
    deck();backdrop();landmark();props()
    write(ROOT/'Assets/ChipArena/Resources/ThunderheartArt.bytes')
    print(len(pieces),'pieces,',sum(len(fs) for _,_,fs in pieces.values()),'triangles')
