using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public struct WoundVector
    {
        public double x,y,z;public WoundVector(double x,double y,double z){this.x=x;this.y=y;this.z=z;}
        public double this[int i]{get=>i==0?x:i==1?y:z;set{if(i==0)x=value;else if(i==1)y=value;else z=value;}}
        public double Length=>Math.Sqrt(x*x+y*y+z*z);public WoundVector Normal=>Length>1e-9?this*(1/Length):new WoundVector();
        public static WoundVector operator +(WoundVector a,WoundVector b)=>new WoundVector(a.x+b.x,a.y+b.y,a.z+b.z);
        public static WoundVector operator -(WoundVector a,WoundVector b)=>new WoundVector(a.x-b.x,a.y-b.y,a.z-b.z);
        public static WoundVector operator *(WoundVector a,double b)=>new WoundVector(a.x*b,a.y*b,a.z*b);
    }
    [Serializable] public sealed class WoundSolid {public WoundVector min,max,color;public bool flesh;}
    [Serializable] public sealed class WoundCarveSpec {public WoundVector hit,dir,size,body;public int axis,sign;public string kind,trauma;public double radius,severity,vertical;public uint seed;public bool low;}
    // Direct port of body-wounds.ts. Double precision and unsigned hash arithmetic preserve the cell decisions.
    public static class NativeWoundCarve
    {
        private delegate bool Removed(double a,double b,double d,int i,int j,int k);
        private sealed class Shape {public double g;public int n,m;public Removed removed,scorched;}
        private static double Clamp(double v,double lo=0,double hi=1)=>Math.Min(hi,Math.Max(lo,v));
        private static double Hypot(double x,double y)=>Math.Sqrt(x*x+y*y);
        public static uint Seed(string text){unchecked{uint h=2166136261;foreach(char c in text)h=(h^c)*16777619;return h;}}
        public static double Hash(uint seed,int a=0,int b=0,int c=0)
        {unchecked{uint h=seed^(uint)(a+101)*0x9e3779b1u^(uint)(b+211)*0x85ebca77u^(uint)(c+307)*0xc2b2ae3du;h=(h^(h>>16))*0x7feb352du;h=(h^(h>>15))*0x846ca68bu;return (h^(h>>16))/4294967296.0;}}
        public static WoundVector Linear(int hex,double k=1)
        {double Channel(int n){double v=n/255.0;return k*(v<.04045?v*.0773993808:Math.Pow(v*.9478672986+.0521327014,2.4));}return new WoundVector(Channel((hex>>16)&255),Channel((hex>>8)&255),Channel(hex&255));}
        private static Shape ShapeOf(WoundCarveSpec s)
        {
            int ax=s.axis,ua=(ax+1)%3,va=(ax+2)%3;double thick=s.size[ax],spanU=s.size[ua],spanV=s.size[va],span=Math.Min(spanU,spanV),r=Math.Max(.012,s.radius),sev=Clamp(s.severity),scale=s.low?1.5:1;
            double Jit(int i,int j,int k)=>Hash(s.seed,i,j,k)-.5;double du=s.dir[ua],dv=s.dir[va],dd=-s.sign*s.dir[ax],drift=Hypot(du,dv),upU=ua==1?1:0,upV=va==1?1:0,vu=upU+upV>0?upU:1,vv=upU+upV>0?upV:0,hu=vv,hv=-vu;
            if(s.kind=="cut")
            {double g=(r>=.12?.016:.0125)*scale,L=Clamp(r*2.8,4*g,.92*Math.Max(spanU,spanV)),W=g*(r>=.12?2:1.15),D=Math.Min(2.5*g+2.5*g*sev,.38*thick),tilt=(Hash(s.seed,7)-.5)*.9+Clamp(s.vertical*1.2,-.5,.5),la=hu*Math.Cos(tilt)+vu*Math.Sin(tilt),lb=hv*Math.Cos(tilt)+vv*Math.Sin(tilt);
             return new Shape{g=g,n=(int)Math.Ceiling(L/2/g)+2,m=(int)Math.Ceiling(D/g)+2,removed=(a,b,d,i,j,k)=>{double along=a*la+b*lb,across=Math.Abs(-a*lb+b*la),end=2*Math.Abs(along)/L;return Math.Abs(along)<=L/2&&across<=W/2+.12*g*Jit(i,j,0)&&d>=0&&d<=D*Math.Sqrt(Math.Max(0,1-end*end*end));}};}
            if(s.trauma=="pierce"&&(s.kind=="puncture"||s.kind=="cavity"))
            {double g=.0125*scale,L=Clamp(r*2.4*(s.kind=="cavity"?1.5:1),3*g,.12),W=g*(s.kind=="cavity"?2.2:1.1),D=Math.Min(6*g+2*g*sev,.55*thick),tilt=(Hash(s.seed,9)-.5)*.5,la=vu*Math.Cos(tilt)+hu*Math.Sin(tilt),lb=vv*Math.Cos(tilt)+hv*Math.Sin(tilt);
             return new Shape{g=g,n=(int)Math.Ceiling((L/2+drift*D)/g)+2,m=(int)Math.Ceiling(D/g)+2,removed=(a,b,d,i,j,k)=>{if(d<-.5*g||d>D)return false;double t=d/Math.Max(.2,dd),ra=a-du*t,rb=b-dv*t;return Math.Abs(ra*la+rb*lb)<=L/2*(1-.3*d/D)&&Math.Abs(-ra*lb+rb*la)<=W/2;}};}
            if(s.kind=="cavity")
            {double g=Clamp(r*.3,.016,.03)*scale,rc=Math.Min(r*(.8+.35*sev),.45*span),dc=Math.Min(.95*rc,.5*thick),shift=.25*rc,ca=du*shift,cb=dv*shift,cd=dd*shift;
             return new Shape{g=g,n=(int)Math.Ceiling(rc/g)+2,m=(int)Math.Ceiling((dc+shift)/g)+2,removed=(a,b,d,i,j,k)=>{double x=a-ca,y=b-cb,z=d-cd;return d>=0&&(x*x+y*y)/(rc*rc)+z*z/(dc*dc)<=1+.44*(.5+sev)*Jit(i,j,k);}};}
            if(s.kind=="bruise")
            {double g=Clamp(r*.28,.016,.03)*scale,rb=Math.Min(r*(.75+.35*sev),.48*span);bool deep=sev>.55;return new Shape{g=g,n=(int)Math.Ceiling(rb/g)+2,m=deep?4:3,removed=(a,b,d,i,j,k)=>k==0&&Hypot(a,b)<=rb*(1+.25*Jit(i,j,0))||deep&&k==1&&Hypot(a,b)<=.42*rb};}
            if(s.kind=="scorch")
            {double g=Clamp(r*.28,.016,.03)*scale,rs=Math.Min(Math.Max(2*g,r*1.25),.48*span);return new Shape{g=g,n=(int)Math.Ceiling(rs/g)+2,m=2,removed=(a,b,d,i,j,k)=>false,scorched=(a,b,d,i,j,k)=>k==0&&Hypot(a,b)<=rs*(1+.3*Jit(i,j,0))};}
            {double g=Clamp(r*.3,.009,.016)*scale,rh=Math.Max(1.5*g,.62*r),D=Math.Min(4.5*g+2.5*g*sev,.42*thick);return new Shape{g=g,n=(int)Math.Ceiling((rh+drift*D)/g)+2,m=(int)Math.Ceiling(D/g)+2,removed=(a,b,d,i,j,k)=>{if(d<-.5*g||d>D)return false;double t=d/Math.Max(.2,dd),ra=a-du*t,rb=b-dv*t;return Hypot(ra,rb)<=rh*(1-.3*d/D)+.15*g*Jit(i,j,k);}};}
        }
        private static IEnumerable<WoundSolid> Subtract(WoundSolid b,WoundVector rmin,WoundVector rmax)
        {
            if(b.max.x<=rmin.x||b.min.x>=rmax.x||b.max.y<=rmin.y||b.min.y>=rmax.y||b.max.z<=rmin.z||b.min.z>=rmax.z){yield return b;yield break;}var lo=b.min;var hi=b.max;
            for(int a=0;a<3;a++)
            {if(lo[a]<rmin[a]){var max=hi;max[a]=rmin[a];yield return new WoundSolid{min=lo,max=max,color=b.color,flesh=b.flesh};lo[a]=rmin[a];}if(hi[a]>rmax[a]){var min=lo;min[a]=rmax[a];yield return new WoundSolid{min=min,max=hi,color=b.color,flesh=b.flesh};hi[a]=rmax[a];}}
        }
        public static List<WoundSolid> Carve(IReadOnlyList<WoundSolid> solids,WoundCarveSpec s)
        {
            var shape=ShapeOf(s);double g=shape.g;int n=shape.n,m=shape.m;while((2*n+1)*(2*n+1)*m>9000&&n>2)n--;m=Math.Min(m,24);
            int ax=s.axis,ua=(ax+1)%3,va=(ax+2)%3,sign=s.sign,count=solids.Count;double face=s.hit[ax],hu=s.hit[ua],hv=s.hit[va];var lo=new double[count*3];var hi=new double[count*3];
            for(int i=0;i<count;i++){int[] axes={ua,va,ax};for(int j=0;j<3;j++){lo[i*3+j]=solids[i].min[axes[j]];hi[i*3+j]=solids[i].max[axes[j]];}}
            int[] order=Enumerable.Range(0,count).OrderByDescending(i=>solids[i].flesh).ToArray();int SolidAt(double u,double v,double w){foreach(int i in order){int o=i*3;if(u>lo[o]&&u<hi[o]&&v>lo[o+1]&&v<hi[o+1]&&w>lo[o+2]&&w<hi[o+2])return i;}return -1;}
            int size=2*n+1,layer=size*size,cells=layer*m;var source=new int[cells];var gone=new bool[cells];var burnt=new bool[cells];double W(int k)=>face-sign*(k+.5)*g;int Id(int i,int j,int k)=>k*layer+(j+n)*size+(i+n);int deepest=0;
            for(int k=0;k<m;k++)for(int j=-n;j<=n;j++)for(int i=-n;i<=n;i++){int c=Id(i,j,k),src=SolidAt(hu+i*g,hv+j*g,W(k));source[c]=src;if(src<0)continue;if(shape.removed(i*g,j*g,(k+.5)*g,i,j,k)){gone[c]=true;deepest=Math.Max(deepest,k+1);}else if(shape.scorched?.Invoke(i*g,j*g,(k+.5)*g,i,j,k)==true)burnt[c]=true;}
            var regionMin=new WoundVector();var regionMax=new WoundVector();regionMin[ua]=hu-(n+.5)*g;regionMin[va]=hv-(n+.5)*g;regionMin[ax]=Math.Min(face+sign*.03,face-sign*m*g);regionMax[ua]=hu+(n+.5)*g;regionMax[va]=hv+(n+.5)*g;regionMax[ax]=Math.Max(face+sign*.03,face-sign*m*g);
            bool Inside(WoundVector p)=>p.x>regionMin.x&&p.x<regionMax.x&&p.y>regionMin.y&&p.y<regionMax.y&&p.z>regionMin.z&&p.z<regionMax.z;var output=new List<WoundSolid>();foreach(var b in solids){if(b.flesh){if(!Inside((b.min+b.max)*.5))output.Add(b);}else output.AddRange(Subtract(b,regionMin,regionMax).Where(v=>v.max.x-v.min.x>1e-6&&v.max.y-v.min.y>1e-6&&v.max.z-v.min.z>1e-6));}
            bool cut=s.kind=="cut",narrow=s.kind=="puncture"||s.trauma=="pierce";double half=g/2;var rows=new Dictionary<(int,int,string),WoundSolid>();
            WoundVector FleshAt(int k){double f=k/(double)Math.Max(1,deepest-1);return Linear(cut?(f<.5?0xbd1a24:0x6c0812):narrow?(f<.5?0xa8141f:0x780a14):f<.34?0xa8141f:f<.67?0x780a14:0x4f0610);}
            WoundSolid Box(int i,int j,int k,double lift=0){var min=new WoundVector();var max=new WoundVector();double w=W(k)+sign*lift;min[ua]=hu+i*g-half;min[va]=hv+j*g-half;min[ax]=w-half;max[ua]=hu+i*g+half;max[va]=hv+j*g+half;max[ax]=w+half;return new WoundSolid{min=min,max=max};}
            int[,] neighbors={{1,0,0},{-1,0,0},{0,1,0},{0,-1,0},{0,0,1},{0,0,-1}};
            for(int k=0;k<m;k++)for(int j=-n;j<=n;j++)for(int i=-n;i<=n;i++)
            {
                int c=Id(i,j,k),src=source[c];if(src<0||gone[c])continue;bool exposed=false,surface=k==0;
                for(int nb=0;nb<6;nb++){int ii=i+neighbors[nb,0],jj=j+neighbors[nb,1],kk=k+neighbors[nb,2];if(ii<-n||ii>n||jj<-n||jj>n||kk<0||kk>=m){if(!surface&&SolidAt(hu+ii*g,hv+jj*g,W(kk))<0)surface=true;continue;}int index=Id(ii,jj,kk);if(gone[index])exposed=true;else if(source[index]<0)surface=true;}
                if(!exposed&&!surface&&!burnt[c])continue;var from=solids[src];double hash=Hash(s.seed,i,j,k),tint=.86+.28*hash;var box=Box(i,j,k);box.flesh=true;
                if(from.flesh){box.color=from.color;output.Add(box);continue;}
                if(burnt[c]){box.color=hash<.06&&!s.low?Linear(0xe2621e):Linear(hash<.55?0x1b1714:0x2e2620,tint);output.Add(box);continue;}
                if(exposed&&k>0){if(s.kind=="bruise")box.color=hash<.18?Linear(0x780a14):s.body*.275+Linear(0x4a2236)*.45;else box.color=(s.kind=="puncture"&&k>=deepest-1?Linear(0x2c0307):FleshAt(k))*tint;output.Add(box);continue;}
                bool rim=exposed&&k==0;double shade=from.color.x*(rim?(cut?.82:.68):1),lift=rim&&s.kind=="cavity"&&!s.low?(.15+.4*hash)*g:0;box=Box(i,j,k,lift);
                if(rim&&s.kind!="bruise"&&s.kind!="scorch"){double mix=cut?.35:.5;box.color=new WoundVector(shade,shade,shade)*(1-mix)+Linear(0xa8141f)*mix;box.flesh=true;output.Add(box);continue;}
                box.color=new WoundVector(shade,shade,shade);if(!rim){var key=(j,k,shade.ToString("F4",System.Globalization.CultureInfo.InvariantCulture));if(rows.TryGetValue(key,out var prev)&&Math.Abs(prev.max[ua]-box.min[ua])<1e-6){prev.max[ua]=box.max[ua];continue;}rows[key]=box;}output.Add(box);
            }
            return output;
        }
    }
}
