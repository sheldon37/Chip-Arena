using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Port of mocap/blade-clearance.ts (presentation only). A human capture rests the blade on a narrow shoulder; the
    // voxel rig's shoulders, chest and head are wider and blockier, so a retargeted pose can run the blade through them.
    // Given the main grip and the blade/edge directions in chest space, the blade is turned about the grip (and, for a
    // full clearance, the grip shifted) until its sampled slab clears the shoulder caps, chest shell and head; near a
    // contact a carried blade rolls its flat onto the surface. Stateless per call: the same pose gives the same blade.
    public sealed class MeleeClearance
    {
        public const float Clearance=.02f,RollRange=.07f,HalfWidth=.09f,HalfThickness=.025f,Step=.07f,Guard=.22f,FloorClear=-.78f;
        private struct Obstacle{public int[] Chain;public Vector3 Position,Size;public bool Lift;}
        // character-rig.ts body boxes the blade must clear (BLADE_OBSTACLES): parent chain below the chest, local box.
        private static readonly Obstacle[] Obstacles=
        {
            O(new[]{CapturedRig.RClav},-.065f,-.01f,0,.19f,.12f,.21f,true),O(new[]{CapturedRig.LClav},.065f,-.01f,0,.19f,.12f,.21f,true),
            O(new int[0],0,.1f,0,.53f,.3f,.3f,true),O(new int[0],0,.235f,0,.47f,.075f,.28f,true),O(new int[0],0,.29f,-.01f,.28f,.05f,.22f,true),
            O(new int[0],0,.1f,-.157f,.3f,.21f,.025f,true),O(new int[0],0,.13f,.158f,.075f,.22f,.022f,false),
            O(new[]{CapturedRig.Neck,CapturedRig.Head},0,.165f,0,.34f,.34f,.32f,false),O(new[]{CapturedRig.Neck,CapturedRig.Head},0,.34f,0,.3f,.04f,.28f,false),
            O(new[]{CapturedRig.Neck,CapturedRig.Head},0,-.015f,.01f,.28f,.04f,.28f,false),O(new[]{CapturedRig.Neck},0,.025f,0,.15f,.15f,.15f,false),
        };
        private static Obstacle O(int[] chain,float x,float y,float z,float sx,float sy,float sz,bool lift)=>new Obstacle{Chain=chain,Position=new Vector3(x,y,z),Size=new Vector3(sx,sy,sz),Lift=lift};
        private const int MaxStations=64,NB=11;
        private readonly Vector3[] c=new Vector3[NB],h=new Vector3[NB];private readonly Quaternion[] q=new Quaternion[NB],qi=new Quaternion[NB];
        private readonly float[] turnN=new float[3*MaxStations*5*NB];private readonly int[] stamp=new int[MaxStations*5*NB];private int call;
        private readonly bool[] inReach=new bool[NB];private readonly float[] st=new float[MaxStations];
        private float halfWidth=HalfWidth;private Vector3 y,floorN;private float floorD;
        public static readonly MeleeClearance Shared=new MeleeClearance();
        private void BoxPoses(MeleeRig r)
        {
            for(int i=0;i<NB;i++)
            {
                var o=Obstacles[i];Vector3 p=Vector3.Zero;Quaternion b=Quaternion.Identity;
                foreach(int n in o.Chain){p+=Vector3.Transform(r.P[n],b);b*=r.Q[n];}
                c[i]=p+Vector3.Transform(o.Position,b);q[i]=b;qi[i]=MeleeMath.Inverse(b);h[i]=o.Size*.5f;
            }
        }
        private static float Near(float v,float m)=>MeleeMath.SmoothStep(v-m,-.02f,0);
        private static float Mid(float l)=>MeleeMath.SmoothStep(Math.Abs(l),0,.02f);
        private static float Sign(float v)=>v>0?1:v<0?-1:1;
        private static Vector3 Normalize(Vector3 v){float l=v.Length();return l>0?v/l:v;}
        // Signed distance from a chest-space point to box i (negative inside) and the blended outward normal.
        private float Sdf(int i,Vector3 p,out Vector3 n,bool medial=false)
        {
            Vector3 l=Vector3.Transform(p-c[i],qi[i]);float qx=Math.Abs(l.X)-h[i].X,qy=Math.Abs(l.Y)-h[i].Y,qz=Math.Abs(l.Z)-h[i].Z,m=Math.Max(qx,Math.Max(qy,qz)),B=.02f;
            float sx=Sign(l.X),sy=Sign(l.Y),sz=Sign(l.Z);
            if(m>0){float ox=Math.Max(qx,0),oy=Math.Max(qy,0),oz=Math.Max(qz,0),d=(float)Math.Sqrt(ox*ox+oy*oy+oz*oz);
                n=Vector3.Transform(Normalize(new Vector3((ox+B*Near(qx,m))*sx,(oy+B*Near(qy,m))*sy,(oz+B*Near(qz,m))*sz)),q[i]);return d;}
            if(medial){n=new Vector3(Near(qx,m)*Mid(l.X)*sx,Near(qy,m)*Mid(l.Y)*sy,Near(qz,m)*Mid(l.Z)*sz);n=Vector3.Transform(n/Math.Max(1,n.Length()),q[i]);return m;}
            n=Vector3.Transform(Normalize(new Vector3(Near(qx,m)*sx,Near(qy,m)*sy,Near(qz,m)*sz)),q[i]);return m;
        }
        private Vector3 Point(Vector3 grip,Vector3 blade,Vector3 edge,float s,int k)=>grip+blade*s+edge*(k==1?halfWidth:k==2?-halfWidth:0)+y*(k==3?HalfThickness:k==4?-HalfThickness:0);
        private static readonly int[] FloorChain={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest};
        // The floor plane in chest space: group -> body -> pelvis -> spine -> chest.
        private void FloorPlane(MeleeRig r)
        {
            Quaternion b=Quaternion.Identity;Vector3 p=Vector3.Zero;
            foreach(int n in FloorChain){p+=Vector3.Transform(r.P[n],b);b*=r.Q[n];}
            floorN=Vector3.Transform(Vector3.UnitY,MeleeMath.Inverse(b));floorD=p.Y-FloorClear;
        }
        // Push the blade clear in place. `from`/`to`: the weapon's extent along the blade from the grip (pommel, blade end).
        // A turn-only clearance (`move` off: cuts) keeps each sample's way out as the captured pose had it.
        public void Clear(MeleeRig r,ref Vector3 grip,ref Vector3 blade,ref Vector3 edge,float from,float to,bool move=true,float width=HalfWidth,bool floor=false)
        {
            BoxPoses(r);halfWidth=width;if(floor)FloorPlane(r);
            int ns=0;if(move)for(int k=0;from+k*Step<=-.05f+1e-6f;k++)st[ns++]=from+k*Step;
            for(int k=0;Guard+k*Step<=to+1e-6f&&ns<MaxStations;k++)st[ns++]=Guard+k*Step;
            bool fixedN=!move;Vector3 b0=blade,e0=edge,y0=Vector3.Cross(blade,edge);if(fixedN)call=unchecked(call+1);
            float slab=(float)Math.Sqrt(width*width+HalfThickness*HalfThickness),lo=ns>0?st[0]:0,hi=ns>0?st[ns-1]:0;
            for(int it=0;it<10;it++)
            {
                y=Vector3.Cross(blade,edge);Vector3 w=Vector3.Zero,tr=Vector3.Zero,na=Vector3.Zero;float total=0,nw=0,rmin=float.PositiveInfinity;
                float reach=slab+Clearance+.004f+(move&&it==0?RollRange:0)+.005f;
                for(int i=0;i<NB;i++){Vector3 cc=c[i]-grip;float t=MeleeMath.Clamp(Vector3.Dot(cc,blade),lo,hi);inReach[i]=(cc-blade*t).Length()-h[i].Length()<reach;}
                for(int si=0;si<ns;si++)
                {
                    float s=st[si];
                    for(int pk=0;pk<5;pk++)
                    {
                        Vector3 p=Point(grip,blade,edge,s,pk);
                        // The floor (`floor`): a cut's follow-through turns up about the grip.
                        if(floor&&s>0){float df=Vector3.Dot(p,floorN)+floorD,penf=Clearance+.004f-df;if(penf>0){float kf=penf*penf*penf;w+=Vector3.Cross(blade,floorN)*(penf*kf/s);total+=kf;}}
                        for(int i=0;i<NB;i++)
                        {
                            if(!inReach[i])continue;float d=Sdf(i,p,out Vector3 hn,fixedN);
                            if(fixedN)
                            {
                                int qn=(si*5+pk)*NB+i,j=3*qn;
                                if(stamp[qn]!=call)
                                {
                                    stamp[qn]=call;
                                    if(it>0){Vector3 p0=grip+b0*s+e0*(pk==1?halfWidth:pk==2?-halfWidth:0)+y0*(pk==3?HalfThickness:pk==4?-HalfThickness:0);Sdf(i,p0,out hn,true);}
                                    turnN[j]=hn.X;turnN[j+1]=hn.Y;turnN[j+2]=hn.Z;
                                }
                                else hn=new Vector3(turnN[j],turnN[j+1],turnN[j+2]);
                            }
                            // Inside the shoulders and chest a carried blade's way out is up: it comes to rest on top of them.
                            if(d<0&&move&&Obstacles[i].Lift){float u=MeleeMath.SmoothStep(-d,0,.03f)*MeleeMath.SmoothStep(hn.Y,-.5f,0);if(u>0){hn.Y+=1.2f*u;hn=Normalize(hn);}}
                            if(it==0&&move&&s>0&&d<Clearance+RollRange){float u=1-MeleeMath.SmoothStep(d,Clearance,Clearance+RollRange);na+=hn*u;nw+=u;rmin=Math.Min(rmin,d);}
                            float pen=Clearance+.004f-d;if(pen<=0)continue;
                            // A blade resting on the shoulders keeps its lie: the grip rises rather than the tip.
                            float lever=MeleeMath.SmoothStep(Math.Abs(s),.15f,.6f)*(move&&Obstacles[i].Lift?.35f:1);
                            float kk=pen*pen*pen;w+=Vector3.Cross(blade,hn)*(pen*kk*lever/s);tr+=hn*(pen*kk*(1-.6f*lever));total+=kk;
                        }
                    }
                }
                // The tip itself (8 cm past the slab's last station) on the floor too.
                if(floor){float tip=to+.08f;Vector3 p=Point(grip,blade,edge,tip,0);float df=Vector3.Dot(p,floorN)+floorD,penf=Clearance+.004f-df;if(penf>0){float kf=penf*penf*penf;w+=Vector3.Cross(blade,floorN)*(penf*kf/tip);total+=kf;}}
                if(it==0&&rmin<Clearance+RollRange&&na.LengthSquared()>1e-8f)
                {
                    // Lay the flat on the surface (carries; a cut keeps its captured edge), toward the nearer of the two edges.
                    float k=(1-MeleeMath.SmoothStep(rmin,Clearance,Clearance+RollRange))*MeleeMath.SmoothStep(na.Length()/Math.Max(nw,1e-9f),.3f,.7f);
                    Vector3 ax=Vector3.Cross(blade,Normalize(na));
                    if(ax.LengthSquared()>1e-4f){ax=Vector3.Normalize(ax);float cc=Vector3.Dot(ax,edge);if(cc<0)ax=-ax;edge=MeleeMath.Lerp(edge,ax,k*MeleeMath.SmoothStep(Math.Abs(cc),0,.35f));edge=Normalize(edge-blade*Vector3.Dot(edge,blade));}
                }
                if(total<=0)break;
                w/=total;tr/=total;if(move)grip+=tr;
                float a=Math.Min(.5f,w.Length());
                if(a>1e-6f){Quaternion rq=MeleeMath.AxisAngle(Vector3.Normalize(w),a);blade=Normalize(Vector3.Transform(blade,rq));edge=Vector3.Transform(edge,rq);edge=Normalize(edge-blade*Vector3.Dot(edge,blade));}
                if(a<2e-4f&&(!move||tr.LengthSquared()<1e-8f))break;
            }
        }
    }
}
