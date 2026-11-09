using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Hot Dog's face, animated (owner 2026-11-09: "animate eyes and mouth with hotdog character. Give them expressions lol"). The bank's
    // texture paints the face; at load its features are painted out in the sausage's own red (HotDogBlankFace) and a toon face is laid on
    // the head in their place (NativeHotDogFaceRig, on the head's sausage twin so it jiggles with it): white eyes with pupils that look
    // about, lids that blink, squint and laugh, brows, and a mouth with teeth and a tongue, shaped by the mood (Core NativeHotDogFace) of
    // whoever wears it: the live pilot's (HotDogFaceMood), Order Up!'s beats, X eyes on a corpse, idle with blinks anywhere else (the Locker,
    // portraits). It sits on the head's surface, read from the mesh (HotDogFaceFit), and mirrors the Hot Dog's own visibility, flash and
    // dissolve. Presentation only.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The head's face as the bank has it (bind space, m), read at load (HotDogFlattenFace, HotDogBlankFace): its skin as a map of
        /// the radius about an upright axis through (AxisX, AxisZ) by the angle round from the front and the height (the sculpted features
        /// filled over from the skin round them); the painted eyes' middles and half sizes; the mouth's height and half width; the red.</summary>
        internal sealed class HotDogFaceFit
        {
            public const float T0=-1.5f,T1=1.5f,Y0=1.36f,Y1=1.84f;public const int NT=150,NY=120;
            public float AxisX=.007f,AxisZ=.063f;public float[] Map=new float[NT*NY];
            /// <summary>Per map cell, the bone weights of the head's skin nearest it (HotDogFlattenFace), so the face is skinned as the skin
            /// under it is and bends, jiggles and squashes with it (owner 2026-11-09: "it goes into the body... it should be attached").</summary>
            public BoneWeight[] Weights;
            /// <summary>Load only: per kept triangle, whether it is over the pressed features (HotDogBlankFace repaints all of it).</summary>
            internal bool[] Pressed;
            private readonly int[] wb=new int[16];private readonly float[] ww=new float[16];
            /// <summary>The skin's weights at `theta` and `y`: the four cells round it blended, the four strongest bones kept.</summary>
            public BoneWeight WeightAt(float theta,float y)
            {
                if(Weights==null)return default;float fx=Mathf.Clamp((theta-T0)/(T1-T0)*(NT-1),0,NT-1.001f),fy=Mathf.Clamp((y-Y0)/(Y1-Y0)*(NY-1),0,NY-1.001f);int i=(int)fx,j=(int)fy;float u=fx-i,v=fy-j;int n=0;
                void Add(int bone,float w){if(w<=0)return;for(int k=0;k<n;k++)if(wb[k]==bone){ww[k]+=w;return;}if(n<16){wb[n]=bone;ww[n]=w;n++;}}
                void Cell(int c,float s){var b=Weights[c];Add(b.boneIndex0,b.weight0*s);Add(b.boneIndex1,b.weight1*s);Add(b.boneIndex2,b.weight2*s);Add(b.boneIndex3,b.weight3*s);}
                Cell(j*NT+i,(1-u)*(1-v));Cell(j*NT+i+1,u*(1-v));Cell((j+1)*NT+i,(1-u)*v);Cell((j+1)*NT+i+1,u*v);
                var o=new BoneWeight();float total=0;
                for(int slot=0;slot<4;slot++)
                {
                    int best=-1;for(int k=0;k<n;k++)if(ww[k]>0&&(best<0||ww[k]>ww[best]))best=k;if(best<0)break;float w=ww[best];total+=w;
                    switch(slot){case 0:o.boneIndex0=wb[best];o.weight0=w;break;case 1:o.boneIndex1=wb[best];o.weight1=w;break;case 2:o.boneIndex2=wb[best];o.weight2=w;break;default:o.boneIndex3=wb[best];o.weight3=w;break;}
                    ww[best]=0;
                }
                if(total>0){o.weight0/=total;o.weight1/=total;o.weight2/=total;o.weight3/=total;}
                return o;
            }
            public NVector EyeL=new NVector(-.055f,1.603f,.147f),EyeR=new NVector(.066f,1.607f,.147f);public float EyeW=.04f,EyeH=.066f,MouthY=1.495f,MouthW=.088f;public uint Skin=0xD8261E;
            /// <summary>The head's radius about its axis at `theta` (radians round from the front) and height `y`.</summary>
            public float Radius(float theta,float y)
            {
                float fx=Mathf.Clamp((theta-T0)/(T1-T0)*(NT-1),0,NT-1.001f),fy=Mathf.Clamp((y-Y0)/(Y1-Y0)*(NY-1),0,NY-1.001f);int i=(int)fx,j=(int)fy;float u=fx-i,v=fy-j;
                return Map[j*NT+i]*(1-u)*(1-v)+Map[j*NT+i+1]*u*(1-v)+Map[(j+1)*NT+i]*(1-u)*v+Map[(j+1)*NT+i+1]*u*v;
            }
            public NVector Surface(float theta,float y){float r=Radius(theta,y);return new NVector(AxisX+(float)Math.Sin(theta)*r,y,AxisZ+(float)Math.Cos(theta)*r);}
            /// <summary>The surface at `theta` and `y`, pushed out `lift` along its normal `n`.</summary>
            public NVector At(float theta,float y,float lift,out NVector n)
            {
                var p=Surface(theta,y);var dt=Surface(theta+.03f,y)-Surface(theta-.03f,y);var dy=Surface(theta,y+.006f)-Surface(theta,y-.006f);
                n=NVector.Normalize(NVector.Cross(dt,dy));return p+n*lift;
            }
            public float Theta(NVector p)=>(float)Math.Atan2(p.X-AxisX,p.Z-AxisZ);
        }
        internal static HotDogFaceFit hotDogFace;
        // The face's box on the bank's model (bind space, m): the front of the head from the chin to the brows.
        private static bool HotDogFaceBox(Vector3 c,float pad=0)=>c.z>.06f-pad&&Mathf.Abs(c.x)<.13f+pad&&c.y>1.43f-pad&&c.y<1.75f+pad;
        private static bool HotDogFaceTri(Vector3 c,Vector3 n)=>HotDogFaceBox(c)&&n.z>.1f;
        private static bool HotDogRed(Color32 c)=>c.r>120&&c.r>c.g*1.7f&&c.r>c.b*1.7f;
        /// <summary>At load (HotDogRead, once the texture is read and before the mesh is made), the bank's sculpted face taken off and the
        /// animated one's place read: HotDogFlattenFace, then HotDogBlankFace. Returns the triangles to keep.</summary>
        private static int[] HotDogFaceOff(Texture2D tex,Vector3[] pos,Vector3[] nor,Vector2[] uv,BoneWeight[] weights,int[] tri)
        {
            Color32[] px;try{px=tex.GetPixels32();}catch{return tri;}
            var kept=HotDogFlattenFace(px,tex.width,tex.height,pos,nor,uv,tri,weights,out var fit);if(fit==null)return tri;
            HotDogBlankFace(px,tex.width,tex.height,pos,uv,kept,fit);tex.SetPixels32(px);hotDogFace=fit;return kept;
        }
        /// <summary>The bank's face is sculpted: the eyes and the brows bulge out of the head, and the pupils, the mouth and its five teeth are
        /// shells of their own. The shells wholly inside the face's box are dropped, and the head's corners over the features (painted other
        /// than the red, within a centimetre of that across the face, or standing out of it as the brows' backs do) are pressed onto a map of
        /// the skin round them, filled over them, so the animated face lies on a plain sausage. Returns the triangles kept, with the fit (null,
        /// and the triangles as they were, when the bank is not the shape expected).</summary>
        private static int[] HotDogFlattenFace(Color32[] px,int W,int H,Vector3[] pos,Vector3[] nor,Vector2[] uv,int[] tri,BoneWeight[] weights,out HotDogFaceFit fit)
        {
            fit=null;int n=pos.Length,nt=tri.Length/3;if(n==0||nt==0)return tri;
            Color32 Sample(Vector2 t){int x=Mathf.Clamp((int)(Mathf.Repeat(t.x,1)*W),0,W-1),y=Mathf.Clamp((int)(Mathf.Repeat(t.y,1)*H),0,H-1);return px[y*W+x];}
            // Corners welded (a seam's split corners are one), then joined into shells through their triangles.
            var weld=new int[n];var at=new Dictionary<(int,int,int),int>();
            for(int i=0;i<n;i++){var k=(Mathf.RoundToInt(pos[i].x*1e5f),Mathf.RoundToInt(pos[i].y*1e5f),Mathf.RoundToInt(pos[i].z*1e5f));if(!at.TryGetValue(k,out int w)){w=at.Count;at[k]=w;}weld[i]=w;}
            var root=new int[at.Count];for(int i=0;i<root.Length;i++)root[i]=i;
            int Find(int a){while(root[a]!=a){root[a]=root[root[a]];a=root[a];}return a;}
            for(int t=0;t<nt;t++){int a=Find(weld[tri[t*3]]),b=Find(weld[tri[t*3+1]]);root[b]=a;int c=Find(weld[tri[t*3+2]]);root[c]=Find(a);}
            var shell=new int[nt];var total=new Dictionary<int,int>();var inBox=new Dictionary<int,int>();var onFace=new Dictionary<int,int>();
            void Count(Dictionary<int,int> d,int s){d[s]=d.TryGetValue(s,out int v)?v+1:1;}
            for(int t=0;t<nt;t++)
            {
                int s=Find(weld[tri[t*3]]);shell[t]=s;var mid=(pos[tri[t*3]]+pos[tri[t*3+1]]+pos[tri[t*3+2]])/3;Count(total,s);
                if(HotDogFaceBox(mid,.01f))Count(inBox,s);if(HotDogFaceBox(mid))Count(onFace,s);
            }
            int head=-1,most=0;foreach(var kv in onFace)if(kv.Value>most){most=kv.Value;head=kv.Key;}if(head<0||most<200)return tri;
            var drop=new HashSet<int>();foreach(var kv in inBox)if(kv.Key!=head&&kv.Value==total[kv.Key]&&kv.Value<800)drop.Add(kv.Key);
            var f=new HotDogFaceFit();
            // The mouth: the biggest shell dropped below the eyes (its height and half width).
            var box=new Dictionary<int,Bounds>();
            for(int t=0;t<nt;t++){if(!drop.Contains(shell[t]))continue;for(int j=0;j<3;j++){var p=pos[tri[t*3+j]];if(box.TryGetValue(shell[t],out var bb)){bb.Encapsulate(p);box[shell[t]]=bb;}else box[shell[t]]=new Bounds(p,Vector3.zero);}}
            int mouth=-1;foreach(int s in drop)if(box[s].center.y<1.54f&&(mouth<0||total[s]>total[mouth]))mouth=s;
            if(mouth>=0){f.MouthY=box[mouth].center.y;f.MouthW=Mathf.Clamp(box[mouth].extents.x,.05f,.11f);}
            // Each corner round the axis; the head's; the features painted on it (a triangle's middle or a corner other than the red).
            var th=new float[n];var rr=new float[n];var isHead=new bool[n];var feature=new bool[n];
            for(int i=0;i<n;i++){float dx=pos[i].x-f.AxisX,dz=pos[i].z-f.AxisZ;th[i]=Mathf.Atan2(dx,dz);rr[i]=Mathf.Sqrt(dx*dx+dz*dz);}
            for(int t=0;t<nt;t++)
            {
                if(shell[t]!=head)continue;for(int j=0;j<3;j++)isHead[tri[t*3+j]]=true;var mid=(pos[tri[t*3]]+pos[tri[t*3+1]]+pos[tri[t*3+2]])/3;
                if(HotDogFaceBox(mid,.01f)&&!HotDogRed(Sample((uv[tri[t*3]]+uv[tri[t*3+1]]+uv[tri[t*3+2]])/3)))for(int j=0;j<3;j++)feature[tri[t*3+j]]=true;
            }
            var feat=new List<int>();for(int i=0;i<n;i++){if(isHead[i]&&!feature[i]&&HotDogFaceBox(pos[i],.01f)&&!HotDogRed(Sample(uv[i])))feature[i]=true;if(feature[i]&&isHead[i])feat.Add(i);}
            if(feat.Count<50)return tri;
            // The region: the head's corners on the front within a centimetre of a feature across the face (its arc and height) or, within
            // 2.5 cm, standing 4 mm out of the head with anything narrower than 2 cm opened away.
            var all=HotDogRaster(pos,th,rr,tri,t=>shell[t]==head);var opened=HotDogOpen(all,8,5);
            var press=new bool[n];int pressed=0;
            for(int i=0;i<n;i++)
            {
                if(!isHead[i])continue;var p=pos[i];if(p.z<=0||Mathf.Abs(p.x)>=.18f||p.y<=1.38f||p.y>=1.8f)continue;
                float best=float.MaxValue;foreach(int k in feat){float a=(th[k]-th[i])*.11f,b=pos[k].y-p.y,d=a*a+b*b;if(d<best)best=d;}best=Mathf.Sqrt(best);
                bool on=best<.010f;
                if(!on&&best<.025f&&HotDogFaceBox(p,.01f)){float o=HotDogCell(opened,th[i],p.y);on=!float.IsNaN(o)&&rr[i]-o>.004f;}
                if(on){press[i]=true;pressed++;}
            }
            // The skin's map (the head's triangles with no corner in the region), filled over the holes, and the region pressed onto it.
            var skin=HotDogRaster(pos,th,rr,tri,t=>shell[t]==head&&!press[tri[t*3]]&&!press[tri[t*3+1]]&&!press[tri[t*3+2]]);
            if(!HotDogFill(skin))return tri;f.Map=skin;
            for(int i=0;i<n;i++){if(!press[i])continue;var s=f.At(th[i],pos[i].y,0,out var sn);pos[i]=new Vector3(s.X,s.Y,s.Z);nor[i]=new Vector3(sn.X,sn.Y,sn.Z);}
            // The skin's weights over the map: each cell the nearest of the head's corners on the front (across the face, its arc and height).
            if(weights!=null&&weights.Length==n)
            {
                var near=new List<int>();for(int i=0;i<n;i++)if(isHead[i]&&pos[i].z>-.04f&&Mathf.Abs(th[i])<1.65f&&pos[i].y>HotDogFaceFit.Y0-.05f&&pos[i].y<HotDogFaceFit.Y1+.05f)near.Add(i);
                if(near.Count>0)
                {
                    f.Weights=new BoneWeight[HotDogFaceFit.NT*HotDogFaceFit.NY];
                    for(int j=0;j<HotDogFaceFit.NY;j++)for(int c=0;c<HotDogFaceFit.NT;c++)
                    {
                        float ct=HotDogFaceFit.T0+(HotDogFaceFit.T1-HotDogFaceFit.T0)*c/(HotDogFaceFit.NT-1),cy=HotDogFaceFit.Y0+(HotDogFaceFit.Y1-HotDogFaceFit.Y0)*j/(HotDogFaceFit.NY-1);
                        int best=near[0];float bd=float.MaxValue;foreach(int i in near){float a=(th[i]-ct)*.1f,b=pos[i].y-cy,d=a*a+b*b;if(d<bd){bd=d;best=i;}}
                        f.Weights[j*HotDogFaceFit.NT+c]=weights[best];
                    }
                }
            }
            // A pressed bulge's walls and folds lie flat now, some turned over: those (and the slivers) go, as the outline pass would ink
            // them through the skin; the triangles facing out still cover the face.
            var kept=new List<int>(tri.Length);var over=new List<bool>(nt);int folded=0;
            for(int t=0;t<nt;t++)
            {
                if(drop.Contains(shell[t]))continue;int a=tri[t*3],b=tri[t*3+1],c=tri[t*3+2];
                bool pressedTri=press[a]||press[b]||press[c];
                if(pressedTri)
                {
                    var cr=Vector3.Cross(pos[b]-pos[a],pos[c]-pos[a]);var mid=(pos[a]+pos[b]+pos[c])/3;f.At(Mathf.Atan2(mid.x-f.AxisX,mid.z-f.AxisZ),mid.y,0,out var sn);
                    if(cr.sqrMagnitude<1e-14f||Vector3.Dot(cr.normalized,new Vector3(sn.X,sn.Y,sn.Z))<.3f){folded++;continue;}
                }
                kept.Add(a);kept.Add(b);kept.Add(c);over.Add(pressedTri);
            }
            f.Pressed=over.ToArray();fit=f;return kept.ToArray();
        }
        // The front-most radius over the map's grid (HotDogFaceFit) of the triangles `use` takes; NaN where none lies.
        private static float[] HotDogRaster(Vector3[] pos,float[] th,float[] rr,int[] tri,Func<int,bool> use)
        {
            const int NT=HotDogFaceFit.NT,NY=HotDogFaceFit.NY;const float T0=HotDogFaceFit.T0,T1=HotDogFaceFit.T1,Y0=HotDogFaceFit.Y0,Y1=HotDogFaceFit.Y1;
            var m=new float[NT*NY];for(int i=0;i<m.Length;i++)m[i]=float.NaN;var X=new float[3];var Y=new float[3];
            for(int t=0;t<tri.Length/3;t++)
            {
                if(!use(t))continue;bool ok=true;
                for(int j=0;j<3;j++){int v=tri[t*3+j];var p=pos[v];if(p.z<=-.04f||p.y<=Y0-.03f||p.y>=Y1+.03f||Mathf.Abs(th[v])>=1.7f){ok=false;break;}X[j]=(th[v]-T0)/(T1-T0)*(NT-1);Y[j]=(p.y-Y0)/(Y1-Y0)*(NY-1);}
                if(!ok)continue;float d=(Y[1]-Y[2])*(X[0]-X[2])+(X[2]-X[1])*(Y[0]-Y[2]);if(Mathf.Abs(d)<1e-9f)continue;
                int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(X[0],Mathf.Min(X[1],X[2])))),x1=Mathf.Min(NT-1,Mathf.CeilToInt(Mathf.Max(X[0],Mathf.Max(X[1],X[2]))));
                int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(Y[0],Mathf.Min(Y[1],Y[2])))),y1=Mathf.Min(NY-1,Mathf.CeilToInt(Mathf.Max(Y[0],Mathf.Max(Y[1],Y[2]))));
                float r0=rr[tri[t*3]],r1=rr[tri[t*3+1]],r2=rr[tri[t*3+2]];
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float a=((Y[1]-Y[2])*(x-X[2])+(X[2]-X[1])*(y-Y[2]))/d,b=((Y[2]-Y[0])*(x-X[2])+(X[0]-X[2])*(y-Y[2]))/d,c=1-a-b;
                    if(a<-.02f||b<-.02f||c<-.02f)continue;float r=a*r0+b*r1+c*r2;int k=y*NT+x;if(float.IsNaN(m[k])||r>m[k])m[k]=r;
                }
            }
            return m;
        }
        private static float HotDogCell(float[] m,float theta,float y)
        {
            int i=Mathf.Clamp((int)((theta-HotDogFaceFit.T0)/(HotDogFaceFit.T1-HotDogFaceFit.T0)*(HotDogFaceFit.NT-1)),0,HotDogFaceFit.NT-1),j=Mathf.Clamp((int)((y-HotDogFaceFit.Y0)/(HotDogFaceFit.Y1-HotDogFaceFit.Y0)*(HotDogFaceFit.NY-1)),0,HotDogFaceFit.NY-1);
            return m[j*HotDogFaceFit.NT+i];
        }
        // A grey opening (the least over a window, then the most of that): what is narrower than the window goes. NaN stays NaN.
        private static float[] HotDogOpen(float[] m,int kt,int ky)
        {
            const int NT=HotDogFaceFit.NT,NY=HotDogFaceFit.NY;var e=new float[m.Length];var o=new float[m.Length];
            for(int y=0;y<NY;y++)for(int x=0;x<NT;x++)
            {
                float lo=float.PositiveInfinity;for(int dy=-ky;dy<=ky;dy++){int yy=y+dy;if(yy<0||yy>=NY)continue;for(int dx=-kt;dx<=kt;dx++){int xx=x+dx;if(xx<0||xx>=NT)continue;float v=m[yy*NT+xx];if(!float.IsNaN(v)&&v<lo)lo=v;}}
                e[y*NT+x]=float.IsPositiveInfinity(lo)?float.NaN:lo;
            }
            for(int y=0;y<NY;y++)for(int x=0;x<NT;x++)
            {
                if(float.IsNaN(m[y*NT+x])){o[y*NT+x]=float.NaN;continue;}
                float hi=float.NegativeInfinity;for(int dy=-ky;dy<=ky;dy++){int yy=y+dy;if(yy<0||yy>=NY)continue;for(int dx=-kt;dx<=kt;dx++){int xx=x+dx;if(xx<0||xx>=NT)continue;float v=e[yy*NT+xx];if(!float.IsNaN(v)&&v>hi)hi=v;}}
                o[y*NT+x]=float.IsNegativeInfinity(hi)?float.NaN:hi;
            }
            return o;
        }
        // The map's holes filled from the cells round them (inverse distance cubed, in metres across the face), then smoothed a little.
        private static bool HotDogFill(float[] m)
        {
            const int NT=HotDogFaceFit.NT,NY=HotDogFaceFit.NY;float sx=(HotDogFaceFit.T1-HotDogFaceFit.T0)/(NT-1)*.1f,sy=(HotDogFaceFit.Y1-HotDogFaceFit.Y0)/(NY-1);
            var hole=new bool[m.Length];var rim=new List<int>();int holes=0;for(int k=0;k<m.Length;k++)if(float.IsNaN(m[k])){hole[k]=true;holes++;}
            if(holes==m.Length)return false;
            for(int y=0;y<NY;y++)for(int x=0;x<NT;x++){int k=y*NT+x;if(hole[k])continue;if(x>0&&hole[k-1]||x<NT-1&&hole[k+1]||y>0&&hole[k-NT]||y<NY-1&&hole[k+NT])rim.Add(k);}
            if(rim.Count==0)return holes==0;
            for(int k=0;k<m.Length;k++)
            {
                if(!hole[k])continue;int hx=k%NT,hy=k/NT;double s=0,ws=0;
                foreach(int q in rim){float a=(q%NT-hx)*sx,b=(q/NT-hy)*sy;double d=Math.Max(a*a+b*b,1e-10);double w=1/(d*Math.Sqrt(d));s+=w*m[q];ws+=w;}
                m[k]=(float)(s/ws);
            }
            var a0=(float[])m.Clone();var a1=new float[m.Length];
            for(int pass=0;pass<2;pass++)
            {
                for(int y=0;y<NY;y++)for(int x=0;x<NT;x++){int k=y*NT+x;a1[k]=(a0[k]+a0[y*NT+Math.Max(0,x-1)]+a0[y*NT+Math.Min(NT-1,x+1)]+a0[Math.Max(0,y-1)*NT+x]+a0[Math.Min(NY-1,y+1)*NT+x])/5;}
                var sw=a0;a0=a1;a1=sw;
            }
            for(int k=0;k<m.Length;k++)if(hole[k])m[k]=a0[k];return true;
        }
        /// <summary>After HotDogFlattenFace: the painted eyes read off the mesh (their whites' middles and half sizes) and every painted
        /// feature on the face painted out in the face's red.</summary>
        private static void HotDogBlankFace(Color32[] px,int W,int H,Vector3[] pos,Vector2[] uv,int[] tri,HotDogFaceFit fit)
        {
            Color32 Sample(Vector2 t){int x=Mathf.Clamp((int)(Mathf.Repeat(t.x,1)*W),0,W-1),y=Mathf.Clamp((int)(Mathf.Repeat(t.y,1)*H),0,H-1);return px[y*W+x];}
            var reds=new List<Color32>();var faces=new List<int>();Vector3 eL=default,eR=default;float aL=0,aR=0;Bounds? bL=null,bR=null;
            for(int t=0;t<tri.Length;t+=3)
            {
                Vector3 a=pos[tri[t]],b=pos[tri[t+1]],c=pos[tri[t+2]];var n=Vector3.Cross(b-a,c-a);float area=n.magnitude*.5f;var mid=(a+b+c)/3;
                // (A pressed bulge's side is a sliver now, its normal anywhere: the box alone says it is on the face.)
                bool box=HotDogFaceBox(mid),on=area>1e-9f?HotDogFaceTri(mid,n/(area*2)):box;
                // The red to paint with: the head's own round the face (the face's reds run darker at the painted rims).
                if(!box){if(mid.y>1.4f&&mid.y<1.85f&&area>1e-9f){var hc=Sample((uv[tri[t]]+uv[tri[t+1]]+uv[tri[t+2]])/3);if(HotDogRed(hc))reds.Add(hc);}continue;}
                faces.Add(t);var col=Sample((uv[tri[t]]+uv[tri[t+1]]+uv[tri[t+2]])/3);if(area<1e-9f||!on)continue;
                if(HotDogRed(col)){if(reds.Count<64)reds.Add(col);continue;}
                if(col.r>200&&col.g>200&&col.b>200&&mid.y>1.53f)
                {
                    if(mid.x<0){eL+=mid*area;aL+=area;if(bL is Bounds l){l.Encapsulate(mid);bL=l;}else bL=new Bounds(mid,Vector3.zero);}
                    else{eR+=mid*area;aR+=area;if(bR is Bounds r){r.Encapsulate(mid);bR=r;}else bR=new Bounds(mid,Vector3.zero);}
                }
            }
            if(faces.Count==0)return;
            byte Median(Func<Color32,byte> c){var v=new List<byte>(reds.Count);foreach(var r in reds)v.Add(c(r));v.Sort();return v[v.Count/2];}
            var skin=reds.Count>0?new Color32(Median(c=>c.r),Median(c=>c.g),Median(c=>c.b),255):new Color32(230,10,1,255);fit.Skin=(uint)(skin.r<<16|skin.g<<8|skin.b);
            if(aL>0)fit.EyeL=FaceVec(eL/aL);if(aR>0)fit.EyeR=FaceVec(eR/aR);
            if(bL is Bounds el&&bR is Bounds er){fit.EyeW=Mathf.Clamp((el.extents.x+er.extents.x)*.5f*1.04f,.025f,.06f);fit.EyeH=Mathf.Clamp((el.extents.y+er.extents.y)*.5f*1.04f,.04f,.09f);}
            // Paint the features out: each face triangle's texels that are not the red become the face's red.
            foreach(int t in faces)
            {
                Vector2 u0=Vector2.Scale(uv[tri[t]],new Vector2(W,H)),u1=Vector2.Scale(uv[tri[t+1]],new Vector2(W,H)),u2=Vector2.Scale(uv[tri[t+2]],new Vector2(W,H));
                int x0=Mathf.Max(0,(int)Mathf.Floor(Mathf.Min(u0.x,Mathf.Min(u1.x,u2.x)))-1),x1=Mathf.Min(W-1,(int)Mathf.Ceil(Mathf.Max(u0.x,Mathf.Max(u1.x,u2.x)))+1);
                int y0=Mathf.Max(0,(int)Mathf.Floor(Mathf.Min(u0.y,Mathf.Min(u1.y,u2.y)))-1),y1=Mathf.Min(H-1,(int)Mathf.Ceil(Mathf.Max(u0.y,Mathf.Max(u1.y,u2.y)))+1);
                if((x1-x0)*(long)(y1-y0)>400000)continue;float d=(u1.y-u2.y)*(u0.x-u2.x)+(u2.x-u1.x)*(u0.y-u2.y);if(Mathf.Abs(d)<1e-6f)continue;
                // Over the pressed features every texel goes (the texture's own shading is painted into the red there: it darkened the
                // old crevices); elsewhere on the face only what is not the red.
                bool all=fit.Pressed!=null&&t/3<fit.Pressed.Length&&fit.Pressed[t/3];
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float px_=x+.5f,py=y+.5f,w0=((u1.y-u2.y)*(px_-u2.x)+(u2.x-u1.x)*(py-u2.y))/d,w1=((u2.y-u0.y)*(px_-u2.x)+(u0.x-u2.x)*(py-u2.y))/d,w2=1-w0-w1;
                    const float E=-.06f;if(w0<E||w1<E||w2<E)continue;int i=y*W+x;if(all||!HotDogRed(px[i]))px[i]=skin;
                }
            }
            fit.Pressed=null;
        }
        private static NVector FaceVec(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static Material hotDogFaceMaterial;
        internal static Material HotDogFaceMaterial()
        {
            if(hotDogFaceMaterial!=null)return hotDogFaceMaterial;var shader=Resources.Load<Shader>("NativeVisualBodyToon");if(shader==null||!shader.isSupported)return null;
            var m=new Material(shader){name="Hot Dog face",hideFlags=HideFlags.HideAndDontSave};m.SetFloat(UseCustomId,0);m.SetFloat(VertexAlbedoId,1e-4f);m.SetFloat(UseOutlineId,1);m.SetFloat(CullId,2);
            return hotDogFaceMaterial=m;
        }
        /// <summary>HotDogWear: the toon face on the head's twin, its frame the bank's bind space (the twin's bind pose).</summary>
        private static void HotDogFaceAttach(Transform headTwin,SkinnedMeshRenderer hotDog)
        {
            if(hotDogFace==null||headTwin==null||hotDogMesh==null)return;int k=Array.IndexOf(hotDogBones,"~head");var bind=hotDogMesh.bindposes;if(k<0||k>=bind.Length)return;var mat=HotDogFaceMaterial();if(mat==null)return;
            var m=bind[k];var go=new GameObject(HotDogFaceName){layer=hotDog.gameObject.layer};var t=go.transform;t.SetParent(headTwin,false);
            t.localPosition=m.GetColumn(3);t.localRotation=m.rotation;t.localScale=m.lossyScale;
            // Skinned on the Hot Dog's own bones, each corner weighted as the skin under it is (HotDogFaceFit.WeightAt), so it stays on the
            // head however the sausage bends (the frame above only places it for a camera aimed at the face).
            var sr=go.AddComponent<SkinnedMeshRenderer>();sr.sharedMaterial=mat;sr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;sr.receiveShadows=true;
            sr.bones=hotDog.bones;sr.rootBone=hotDog.rootBone;sr.localBounds=hotDog.localBounds;sr.updateWhenOffscreen=false;
            go.AddComponent<NativeHotDogFaceRig>().Bind(hotDog,hotDogFace);
        }
        public const string HotDogFaceName="HotDog face";
        internal static Matrix4x4[] HotDogBindposes=>hotDogBindposes??=hotDogMesh!=null?hotDogMesh.bindposes:new Matrix4x4[0];private static Matrix4x4[] hotDogBindposes;
        // ---- whose face it is, and its mood ----
        /// <summary>The live mood of Hot Dog pilot `id` and how long it has been on (the rig's source; Idle when it is nobody's).</summary>
        internal static Func<int,(HotDogMood mood,float age)> HotDogFaceSource;
        private float hotDogIntroAge;private int hotDogIntroFrame=-10,hotDogIntroId=-1;private Func<int,(HotDogMood,float)> hotDogFaceSource;
        private (HotDogMood,float) HotDogFaceMood(int id)
        {
            // (The intro's actor as DogIntroToons saw it: the intro lets go of it before the face asks.)
            if(id==hotDogIntroId&&hotDogIntroFrame>=Time.frameCount-1){var im=NativeHotDogFace.Intro(hotDogIntroAge,out float isince);return (im,hotDogIntroAge-isince);}
            var a=DogActor(id);if(a==null||!a.Alive)return (HotDogMood.Idle,0);
            bool dazed=DogMarkOn(id,NativeTopDog.Pancake,dogNow)!=null||DogMarkOn(id,NativeTopDog.Dazed,dogNow)!=null||DogMarkOn(id,NativeTopDog.Walled,dogNow)!=null;
            var m=NativeHotDogFace.Of(a,a.SourceSpec==NativeTopDog.Spec?NativeDogView.Of(a):null,dazed,dogNow,out float since);return (m,Mathf.Max(0,dogNow-since));
        }
    }

    /// <summary>The Hot Dog's toon face on its head (ArenaPresentation.HotDogFaceAttach): its pose eased toward its wearer's mood each frame
    /// and built into one mesh in the bank's bind space.</summary>
    internal sealed class NativeHotDogFaceRig:MonoBehaviour
    {
        private SkinnedMeshRenderer hotDog;private ArenaPresentation.HotDogFaceFit fit;private SkinnedMeshRenderer view;private Mesh mesh;private BoneWeight[] weights;
        private readonly NativeFallenKit kit=new NativeFallenKit();private readonly List<Vector2> uvs=new List<Vector2>();private bool hidden;private HotDogFacePose pose;private bool posed;private int id=-1,seed;private bool dead,named;private float last=-1;
        public void Bind(SkinnedMeshRenderer hotDog,ArenaPresentation.HotDogFaceFit fit){this.hotDog=hotDog;this.fit=fit;view=GetComponent<SkinnedMeshRenderer>();seed=GetInstanceID();}
        private void Name()
        {
            named=true;for(var t=transform;t!=null;t=t.parent)
            {
                var n=t.name;if(n.StartsWith("Visual body ",StringComparison.Ordinal)&&int.TryParse(n.Substring(12),out int a)){id=a;break;}
                if(n.StartsWith("Visual corpse ",StringComparison.Ordinal)){dead=true;int.TryParse(n.Substring(14),out id);break;}
            }
            seed=id>0?id*7919:seed;
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
        private void LateUpdate()
        {
            if(hotDog==null||fit==null||view==null)return;if(!named)Name();
            // Hidden with the Hot Dog (the body's own passes set its flash, dissolve and spawn looks on this renderer as on the rest of
            // the body, and may hide it themselves: a split's other half, the first-person arms, which this leaves be).
            bool host=hotDog.enabled&&hotDog.gameObject.activeInHierarchy;
            if(!host){if(view.enabled){view.enabled=false;hidden=true;}return;}
            if(hidden){view.enabled=true;hidden=false;}if(!view.enabled)return;
            // (A pass that gave this renderer a mesh of its own, the first-person arms' or a sever's copy, has it: it is left as made.)
            if(view.sharedMesh!=null&&view.sharedMesh!=mesh)return;
            float now=Time.unscaledTime,dt=last<0?1:Mathf.Clamp(now-last,0,.1f);last=now;
            HotDogMood mood=HotDogMood.Idle;float age=now;
            if(dead)mood=HotDogMood.Dead;else if(id>0&&ArenaPresentation.HotDogFaceSource!=null){var s=ArenaPresentation.HotDogFaceSource(id);mood=s.mood;age=s.age;}
            var target=NativeHotDogFace.At(mood,age,now,seed);pose=posed?NativeHotDogFace.Ease(pose,target,dt):target;posed=true;
            Build();
        }
        // Colours (sRGB hex): the eyes' white, the ink, the mouth's dark, teeth, tongue.
        private const uint White=0xF7F4EE,Ink=0x161015,Mouth=0x3A0C14,Teeth=0xFBF8F0,Tongue=0xE86A7A;
        private static NVector Cross(NVector a,NVector b)=>NVector.Cross(a,b);
        private void Build()
        {
            kit.Clear();var p=pose;
            Eye(fit.EyeL,-1,p.OpenL,p.LowerL,p.BrowL,p);Eye(fit.EyeR,1,p.OpenR,p.LowerR,p.BrowR,p);MouthShape(p);
            kit.Finish();if(kit.Count==0)return;mesh=ArenaPresentation.FbKitMesh(kit,Matrix4x4.identity,"Hot Dog face",out _,mesh);
            // Texture coordinates across the face, so the dead pilot's dissolve noise eats it in patches as it does the skin.
            uvs.Clear();for(int i=0;i<kit.Count;i++){var q=kit.P[i];uvs.Add(new Vector2(q.X*2.2f+.5f,(q.Y-1.4f)*2.2f));}mesh.SetUVs(0,uvs);
            // Each corner skinned as the head's skin under it (its place on the face's map).
            if(weights==null||weights.Length!=kit.Count)weights=new BoneWeight[kit.Count];
            for(int i=0;i<kit.Count;i++){var q=kit.P[i];weights[i]=fit.WeightAt(fit.Theta(q),q.Y);}
            mesh.boneWeights=weights;mesh.bindposes=ArenaPresentation.HotDogBindposes;mesh.bounds=new Bounds(new Vector3(fit.AxisX,1.6f,fit.AxisZ+.1f),new Vector3(.5f,.5f,.5f));view.sharedMesh=mesh;
        }
        // One eye at `at` (the painted eye's middle; `side` -1 its left, 1 its right): the white, the pupil (or an X), the lids, the brow.
        private void Eye(NVector at,int side,float open,float lower,float brow,HotDogFacePose p)
        {
            float th=fit.Theta(at);var surf=fit.At(th,at.Y,.004f,out var n);var right=NVector.Normalize(Cross(NVector.UnitY,n));var up=NVector.Normalize(Cross(n,right));
            // The size of the painted eye it replaces (HotDogBlankFace), bulging out of the head as the sculpted one did.
            float rx=fit.EyeW,ry=fit.EyeH,rz=.024f;var c=surf+n*.004f;
            kit.Ellipsoid(c,right*rx,up*ry,n*rz,18,12,White);
            if(p.Cross)
            {
                for(int k=-1;k<=1;k+=2){var a=c+right*(-.024f)+up*(k*.034f)+n*(rz*.96f);var b=c+right*(.024f)+up*(-k*.034f)+n*(rz*.96f);kit.Tube(new[]{a,(a+b)/2+n*.004f,b},.006f,6,Ink,line:false);}
            }
            else
            {
                float s=Math.Max(.4f,p.Pupil),lx=Clamp(p.LookX,-1,1)*.017f,ly=Clamp(p.LookY,-1,1)*.024f,ux=lx/rx,uy=ly/ry;
                var front=c+right*lx+up*ly+n*(rz*(float)Math.Sqrt(Math.Max(0,1-ux*ux-uy*uy)));
                kit.Ellipsoid(front,right*(.015f*s),up*(.023f*s),n*.0045f,12,6,Ink,line:false);
                kit.Ellipsoid(front+right*.005f+up*.007f+n*.004f,right*.004f,up*.004f,n*.002f,6,4,White,line:false);
            }
            // The lids: an upper one down to `open`, a lower one up by `lower` (to a laugh's crescent), each a cap of a slightly larger shell.
            float shut=1-Clamp01(open);if(shut>.02f)Lid(c,right,up,n,rx,ry,rz,Mathf.PI*shut,true);
            float rise=Clamp01(lower)*.55f;if(rise>.02f)Lid(c,right,up,n,rx,ry,rz,Mathf.PI*rise,false);
            // The brow: an arc over the eye, its inner end down when angry (brow -1), up when worried (1).
            var pts=new NVector[5];float inner=-side;
            for(int k=0;k<5;k++)
            {
                float u=k/4f,x=inner*(1-2*u)*rx*1.05f,y=c.Y+ry*1.22f+p.BrowUp*.018f+(brow*.017f-(brow<0?.01f:0))*(1-u)+.006f*(float)Math.Sin(Math.PI*u);
                // Over the eye (an angry brow dips onto it) it rides the lids' shell, not the skin.
                float ex=x/(rx*1.08f),ey=(y-c.Y)/(ry*1.05f),over=ex*ex+ey*ey<1?.004f+rz*1.3f*(float)Math.Sqrt(1-ex*ex-ey*ey)+.008f:0;
                var q=c+right*x;pts[k]=fit.At(fit.Theta(q),y,Math.Max(.011f,over),out _);
            }
            kit.Tube(pts,.0085f,7,Ink);
        }
        private void Lid(NVector c,NVector right,NVector up,NVector n,float rx,float ry,float rz,float sweep,bool top)
        {
            const int J=7,Seg=16;var rings=new List<NVector[]>();var cs=new List<NVector>();float sx=rx*1.08f,sy=ry*1.05f,sz=rz*1.3f;
            for(int j=0;j<=J;j++)
            {
                float phi=Math.Max(.05f,sweep*j/J);float h=(float)Math.Cos(phi)*sy*(top?1:-1),w=(float)Math.Sin(phi);var cc=c+up*h;
                rings.Add(NativeFallenKit.Ring(cc,right,n,sx*w,sz*w,Seg));cs.Add(cc);
            }
            kit.Loft(rings,cs,fit.Skin,capLast:true);
        }
        // The mouth on the surface: its middle line curved by the smile, opened mostly downward (the jaw), dark inside, the top teeth and
        // the tongue showing as the mood has them, its lips lined.
        private void MouthShape(HotDogFacePose p)
        {
            const int M=18;float halfW=fit.MouthW*(.45f+.55f*Clamp01(p.Wide)),open=Clamp01(p.Open);var top=new NVector[M+1];var bottom=new NVector[M+1];var mid=new float[M+1];var h=new float[M+1];
            for(int i=0;i<=M;i++)
            {
                float x=i/(float)M*2-1,curve=Clamp(p.Smile,-1,1)*.02f*(x*x-.35f),gap=open*.06f*(float)Math.Pow(Math.Max(0,1-x*x),.75f)+.0055f;mid[i]=curve;h[i]=gap;
                top[i]=Map(x*halfW,curve+gap*.22f,.0035f);bottom[i]=Map(x*halfW,curve-gap*.78f,.0035f);
            }
            for(int i=0;i<M;i++)Quad(top[i],top[i+1],bottom[i+1],bottom[i],Mouth);
            // (Each quad faces out along the head's surface: Quad.)
            // Teeth: a row along the top, and past half a row along the bottom too (the painted face's gritted grin), each tooth on its own
            // with a sliver of the mouth between; then the tongue in the bottom.
            float teeth=Clamp01(p.Teeth);const int Teeth6=6;
            float Edge(float x,bool upper){float u=(x+1)*.5f*M;int i=Math.Max(0,Math.Min(M-1,(int)u));float w=u-i;float m=mid[i]+(mid[i+1]-mid[i])*w,g=h[i]+(h[i+1]-h[i])*w;return upper?m+g*.22f:m-g*.78f;}
            float Gap(float x){float u=(x+1)*.5f*M;int i=Math.Max(0,Math.Min(M-1,(int)u));return h[i]+(h[i+1]-h[i])*(u-i);}
            for(int row=0;row<2;row++)
            {
                float depth=row==0?teeth*.02f:Math.Max(0,teeth-.5f)*2*.017f;if(depth<.001f)continue;float share=row==0?.5f:.45f;
                for(int k=0;k<Teeth6;k++)
                {
                    float x0=-.8f+1.6f*k/Teeth6+.025f,x1=-.8f+1.6f*(k+1)/Teeth6-.025f;
                    float e0=Edge(x0,row==0),e1=Edge(x1,row==0),d0=Math.Min(Gap(x0)*share,depth),d1=Math.Min(Gap(x1)*share,depth),s=row==0?-1:1;
                    Quad(Map(x0*halfW,e0,.0045f),Map(x1*halfW,e1,.0045f),Map(x1*halfW,e1+s*d1,.0045f),Map(x0*halfW,e0+s*d0,.0045f),Teeth);
                }
            }
            float tongue=Clamp01(p.Tongue)*open;
            if(tongue>.05f){int c=M/2;float y=mid[c]-h[c]*.62f,ht=h[c]*.32f*Math.Max(.4f,tongue),wt=halfW*.42f;var at=Map(0,y,.005f);var tn=Normal(0,y);var tr=NVector.Normalize(Cross(NVector.UnitY,tn));kit.Ellipsoid(at,tr*wt,NVector.UnitY*ht,tn*.004f,12,6,Tongue,line:false);}
            kit.Tube(top,.0042f,5,Ink,line:false);kit.Tube(bottom,.0042f,5,Ink,line:false);
        }
        private NVector Map(float u,float v,float lift){float y=fit.MouthY+v,r=fit.Radius(0,y);return fit.At(u/Math.Max(.05f,r),y,lift,out _);}
        private NVector Normal(float u,float v){float y=fit.MouthY+v,r=fit.Radius(0,y);fit.At(u/Math.Max(.05f,r),y,0,out var n);return n;}
        private void Quad(NVector a,NVector b,NVector c,NVector d,uint col)
        {
            var mid=(a+b+c+d)*.25f;var outward=mid-new NVector(fit.AxisX,mid.Y,fit.AxisZ);if(outward.LengthSquared()<1e-12f)return;
            int ia=kit.V(a,col,false),ib=kit.V(b,col,false),ic=kit.V(c,col,false),id=kit.V(d,col,false);kit.Quad(ia,ib,ic,id,outward,false);
        }
        private static float Clamp(float x,float a,float b)=>x<a?a:x>b?b:x;private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}
