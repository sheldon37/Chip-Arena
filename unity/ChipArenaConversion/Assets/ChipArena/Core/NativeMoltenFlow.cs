using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One vertex of the molten metal mesh (Resources/NativeMoltenFlow.shader's input, uploaded as is by
    /// the Converter's runtime presenter): position, normal, and three float4s the shader reads as TEXCOORD0..2:
    /// A (flow coordinate, across coordinate, share along or out, seed), B (heat 0..1, crust 0..1, kind, emission gain),
    /// C (speed m/s, age s, distance to the visible edge m, the piece's size m).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MoltenVertex
    {
        public float Px,Py,Pz,Nx,Ny,Nz,Ax,Ay,Az,Aw,Bx,By,Bz,Bw,Cx,Cy,Cz,Cw;
    }

    /// <summary>A pool this frame (the checks): its hazard id, centre, the authority radius, the drawn edge's nearest and farthest
    /// radius from the centre (the farthest is the radius times the spread), its spread share, heat and crust.</summary>
    public struct MoltenPoolShape{public int Id;public float X,Y,Z,Radius,EdgeMin,EdgeMax,Spread,Heat,Crust;}

    /// <summary>
    /// THE CONVERTER's molten metal as smooth geometry (owner 2026-10-19: "When Ironworks pours it looks like minecraft lava right
    /// now. I want it to flow realistically."), replacing the stacked molten cubes: one dynamic mesh a frame, drawn with
    /// Resources/NativeMoltenFlow.shader (scrolling flow noise advected at the metal's own speed, a white-yellow core, orange,
    /// red at the cooler skin, a crust that darkens and cracks into glowing fissures as it cools, bubbles).
    /// <list type="bullet">
    /// <item><see cref="Stream"/>: a pour as a continuous tube on its ballistic arc; its cross-section narrows as the metal
    /// accelerates (the flow is conserved: radius as the square root of the speed ratio), necks into a growing varicose wave
    /// carried down at the flow's speed (the Plateau-Rayleigh break-up) and sways a little; a rounded front while it first falls,
    /// a rounded tail when it detaches.</item>
    /// <item><see cref="Pool"/>: a puddle with a flat top and a rounded meniscus edge, its edge perturbed only inward (lobes,
    /// splash fingers) so the drawn hot area never passes the hazard's radius and fills it within the lobes' few percent.</item>
    /// <item><see cref="Crown"/>: the splash crown where a stream lands; <see cref="Blob"/>: a droplet or a slag lump stretched
    /// along its motion; <see cref="Ribbon"/>: a flowing strip on a surface (the spill down the nose, the cracks in the pit).</item>
    /// </list>
    /// Pure geometry: the same inputs give the same vertices (host, guests and the killcam alike). Resolution halves on the low
    /// tier. Allocation free: pieces that would pass the capacity are refused whole (<see cref="Refused"/>).
    /// </summary>
    public sealed class NativeMoltenFlow
    {
        public const float StreamKind=0,PoolKind=1,CrownKind=2,BlobKind=3,RibbonKind=4;
        public const float Gravity=9.81f;
        public readonly MoltenVertex[] V;public readonly int[] T;public int VertexCount,IndexCount;
        public readonly MoltenPoolShape[] Pools=new MoltenPoolShape[80];public int PoolCount;
        /// <summary>Pieces this frame by kind (checks), and pieces refused for room.</summary>
        public int Streams,Crowns,Blobs,Ribbons,Refused;
        /// <summary>A scratch path for <see cref="Ribbon"/> (points, surface normals, half widths, heat along it).</summary>
        public readonly Vector3[] PathP=new Vector3[48],PathN=new Vector3[48];public readonly float[] PathW=new float[48],PathHeat=new float[48];
        private bool low;private readonly float[] edgeAt=new float[66],cosAt=new float[66],sinAt=new float[66];
        public NativeMoltenFlow(int vertices=26000,int indices=140000){V=new MoltenVertex[vertices];T=new int[indices];}
        public bool Low=>low;
        public void Clear(bool lowTier){VertexCount=IndexCount=0;PoolCount=0;Streams=Crowns=Blobs=Ribbons=Refused=0;low=lowTier;}
        private bool Room(int v,int i){if(VertexCount+v>V.Length||IndexCount+i>T.Length){Refused++;return false;}return true;}
        private int Vert(Vector3 p,Vector3 n,float ax,float ay,float az,float aw,float heat,float crust,float kind,float gain,float cx,float cy,float cz,float cw)
        {
            int k=VertexCount++;V[k]=new MoltenVertex{Px=p.X,Py=p.Y,Pz=p.Z,Nx=n.X,Ny=n.Y,Nz=n.Z,Ax=ax,Ay=ay,Az=az,Aw=aw,Bx=heat,By=crust,Bz=kind,Bw=gain,Cx=cx,Cy=cy,Cz=cz,Cw=cw};return k;
        }
        private void Quad(int a,int b,int c,int d){T[IndexCount++]=a;T[IndexCount++]=b;T[IndexCount++]=c;T[IndexCount++]=a;T[IndexCount++]=c;T[IndexCount++]=d;}
        private void Tri(int a,int b,int c){T[IndexCount++]=a;T[IndexCount++]=b;T[IndexCount++]=c;}
        private static float H(uint s,int i)=>BossMath.Hash(s,i);

        // ============================================================================================================
        // the pour's arc
        // ============================================================================================================
        /// <summary>
        /// The launch velocity that carries metal leaving `lip` onto `land` (on the floor) on a ballistic arc whose start leans
        /// `rise` (the launch's vertical speed over its horizontal speed: the lip's tilt, -0.6 to 0.4), and the flight time.
        /// </summary>
        public static Vector3 Launch(Vector3 lip,Vector3 land,float rise,out float flight)
        {
            var d=new Vector3(land.X-lip.X,0,land.Z-lip.Z);float D=d.Length(),h=lip.Y-land.Y;rise=BossMath.Clamp(rise,-.6f,.4f);
            float drop=Math.Max(.08f,h+rise*D);flight=BossMath.Clamp((float)Math.Sqrt(2*drop/Gravity),.18f,1.4f);
            float vy=(-h+.5f*Gravity*flight*flight)/flight;var vh=D>1e-4f?d*(1/flight):Vector3.Zero;return new Vector3(vh.X,vy,vh.Z);
        }
        /// <summary>A point of the arc `s` s after the lip, and the metal's velocity there.</summary>
        public static Vector3 Arc(Vector3 lip,Vector3 v0,float s,out Vector3 vel){vel=new Vector3(v0.X,v0.Y-Gravity*s,v0.Z);return new Vector3(lip.X+v0.X*s,lip.Y+v0.Y*s-.5f*Gravity*s*s,lip.Z+v0.Z*s);}

        /// <summary>
        /// A falling stream on the arc from `lip` at `v0`, drawn between flight times s0 (0 at the lip; above 0 once it has
        /// detached) and s1 (the landing, or the front while it first falls), `r0` its radius at the lip. `clock` (the
        /// presentation clock) carries the necking wave down; `breakup` 0..1 grows it (the stream thinning into drops at its end);
        /// `landed`: its end is in the pool (no cap).
        /// </summary>
        public void Stream(Vector3 lip,Vector3 v0,float s0,float s1,float r0,float clock,float heat,float gain,float seed,float breakup,bool landed)
        {
            if(!(s1>s0+.005f)||!(r0>.002f))return;
            int rings=low?14:30,sides=low?6:12,caps=2;int nv=(rings+1+caps*2)*(sides+1),ni=(rings+caps*2)*sides*6;if(!Room(nv,ni))return;Streams++;
            // The arc's plane is constant: its normal (the stream's binormal) from the launch's horizontal direction.
            var hz=new Vector3(v0.X,0,v0.Z);float hl=hz.Length();var bin=hl>1e-4f?Vector3.Normalize(Vector3.Cross(Vector3.UnitY,hz/hl)):Vector3.UnitX;
            float v0l=Math.Max(.3f,v0.Length());int first=VertexCount;float ph0=seed*6.28f;int ringCount=0;
            float fade=BossMath.Clamp01(.6f+.4f*heat);
            void Ring(float s,float rScale,float capShare)
            {
                var c=Arc(lip,v0,s,out var vel);float sp=Math.Max(.3f,vel.Length());var tan=vel/sp;var nrm=Vector3.Normalize(Vector3.Cross(bin,tan));
                float u=BossMath.Clamp01((s-s0)/Math.Max(.01f,s1-s0)),down=BossMath.Clamp01(s/Math.Max(.05f,s1));
                // Mass flow: the section narrows as the metal speeds up; the varicose wave rides the flow (its phase is the time
                // the metal left the lip) and grows downstream; a slow sway.
                float phase=clock-s;float amp=(.05f+.19f*down*down)*(1+1.6f*breakup);
                float neck=1+amp*((float)Math.Sin(phase*73.9f+ph0)*.72f+(float)Math.Sin(phase*39.3f+ph0*1.7f)*.28f);
                float r=r0*(float)Math.Sqrt(v0l/sp)*Math.Max(.25f,neck)*rScale;
                var sway=bin*(.035f*down*(float)Math.Sin(phase*27f+ph0*2.3f))+nrm*(.02f*down*(float)Math.Sin(phase*19f+ph0));
                for(int i=0;i<=sides;i++)
                {
                    float a=i*BossMath.Tau/sides,ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);var dir=nrm*ca+bin*sa;
                    float cap=Math.Abs(capShare);var n=cap>0?Vector3.Normalize(dir*(1-cap)+tan*(-capShare)):dir;
                    Vert(c+sway+dir*r,n,s,i/(float)sides,u,seed,heat*fade,0,StreamKind,gain,sp,0,r,r0);
                }
                ringCount++;
            }
            // The tail's rounded top once detached, the body, the front's round end while it falls (none in the pool).
            bool tail=s0>.01f;
            if(tail){Ring(s0,.0f,1);Ring(s0+.012f,.62f,.55f);}else{Ring(s0,.0f,1);Ring(s0+.0001f,1,0);}
            for(int k=1;k<rings;k++)Ring(s0+(s1-s0)*k/rings,1,0);
            if(landed){Ring(s1,1,0);Ring(s1+.0001f,1,0);}else{Ring(s1-.012f,.62f,-.55f);Ring(s1,0,-1);}
            for(int k=0;k+1<ringCount;k++)for(int i=0;i<sides;i++){int a=first+k*(sides+1)+i,b=a+sides+1;Quad(a,b,b+1,a+1);}
        }

        /// <summary>
        /// The splash crown where a stream meets its pool: a ring of thrown-up metal (radius `r`, height `h`) with a jagged,
        /// changing lip and a mound under the stream; `clock` animates it.
        /// </summary>
        public void Crown(Vector3 c,float r,float h,float clock,float heat,float gain,float seed)
        {
            if(!(r>.02f)||!(h>.01f))return;int sides=low?8:16;int nv=(sides+1)*3+1+sides,ni=sides*6*2+sides*3;if(!Room(nv,ni))return;Crowns++;
            int first=VertexCount;uint sd=(uint)(seed*7919);
            for(int i=0;i<=sides;i++)
            {
                float a=i*BossMath.Tau/sides,ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);var o=new Vector3(ca,0,sa);int j=i%sides;
                float jag=.55f+.45f*(float)Math.Abs(Math.Sin(clock*11.3f+H(sd,j)*6.28f))*(j%2==0?1:.55f);float flare=r*(1.35f+.25f*H(sd,j+40));
                var b=c+o*(r*.7f);var m=c+o*(r*1.02f)+Vector3.UnitY*(h*.45f);var t=c+o*flare+Vector3.UnitY*(h*jag);
                var n=Vector3.Normalize(o*.8f+Vector3.UnitY*.6f);
                Vert(b,n,0,i/(float)sides,0,seed,heat,0,CrownKind,gain,0,0,0,r);Vert(m,n,.45f,i/(float)sides,0,seed,heat,0,CrownKind,gain,0,0,0,r);Vert(t,n,1,i/(float)sides,0,seed,heat*.85f,0,CrownKind,gain,0,0,0,r);
            }
            for(int i=0;i<sides;i++){int a=first+i*3,b=a+3;Quad(a,b,b+1,a+1);Quad(a+1,b+1,b+2,a+2);}
            // The mound under the stream.
            int top=Vert(c+Vector3.UnitY*(h*.55f),Vector3.UnitY,.2f,0,0,seed,heat,0,CrownKind,gain,0,0,0,r);int ring=VertexCount;
            for(int i=0;i<sides;i++){float a=i*BossMath.Tau/sides;var o=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));Vert(c+o*(r*.75f)+Vector3.UnitY*.01f,Vector3.Normalize(o+Vector3.UnitY*1.6f),.1f,i/(float)sides,0,seed,heat,0,CrownKind,gain,0,0,0,r);}
            for(int i=0;i<sides;i++)Tri(top,ring+i,ring+(i+1)%sides);
        }

        // ============================================================================================================
        // pools
        // ============================================================================================================
        private static readonly float[] RingsFull={.22f,.42f,.6f,.74f,.85f,.92f,.965f,.99f,1f},RingsLow={.4f,.72f,.9f,1f};
        /// <summary>
        /// A puddle of metal centred at `c` on the floor for the hazard `id` of radius `R`: drawn to `spread` of it (1 from the
        /// hazard's hit: the visible hot area is the damaging area); its edge pulled in by up to `lobes` of the radius in soft
        /// viscous lobes and `fingers` between splash fingers (both inward only), `square` 0..1 rounding it to a square (the pit).
        /// `heat` 1 white-hot .. 0 cold; `crust` 0 liquid .. 1 crusted over (glowing cracks remain while it is hot); `age`
        /// since it landed (bubbles, the surface's slow flow).
        /// </summary>
        public void Pool(int id,Vector3 c,float R,float spread,float lobes,float fingers,float heat,float crust,float age,float seed,float gain=1,float square=0)
        {
            spread=BossMath.Clamp01(spread);if(!(R>.01f)||spread<=.01f)return;
            var rings=low?RingsLow:RingsFull;int segs=Math.Max(low?12:18,Math.Min(low?32:64,(int)Math.Round((low?10:16)+R*(low?6:13))));
            int nv=1+(rings.Length+1)*(segs+1),ni=segs*3+rings.Length*segs*6;if(!Room(nv,ni))return;
            uint sd=(uint)(seed*7919+id*131);float a1=H(sd,1)*6.28f,a2=H(sd,2)*6.28f,a3=H(sd,3)*6.28f,a4=H(sd,4)*6.28f;int fn=5+(int)(H(sd,5)*4);
            float Rs=R*spread,H0=.016f+.004f*heat,baseY=.006f,emin=float.MaxValue,emax=0;
            // The edge's inward pull at angle a (0 at its farthest: exactly R), lobes and fingers.
            float Pull(float a)
            {
                float l=.5f+.5f*(.55f*(float)Math.Sin(3*a+a1)+.3f*(float)Math.Sin(5*a+a2)+.15f*(float)Math.Sin(9*a+a3));
                float f=(float)Math.Pow(.5f+.5f*Math.Sin(fn*a+a4),3);
                return lobes*l+fingers*(1-f);
            }
            float Square(float a){if(square<=0)return 1;float ca=Math.Abs((float)Math.Cos(a)),sa=Math.Abs((float)Math.Sin(a));float m=Math.Max(ca,sa);return BossMath.Lerp(1,1/Math.Max(.7071f,m)*.97f,square);}
            // The edge round the pool (once per segment), the pull's least value taken off so the farthest edge lies exactly on the
            // radius.
            float least=float.MaxValue;for(int i=0;i<segs;i++){float pa=Pull(i*BossMath.Tau/segs);edgeAt[i]=pa;if(pa<least)least=pa;}
            for(int i=0;i<segs;i++){float a=i*BossMath.Tau/segs;edgeAt[i]=Rs*(1-(edgeAt[i]-least))*Square(a);cosAt[i]=(float)Math.Cos(a);sinAt[i]=(float)Math.Sin(a);}
            edgeAt[segs]=edgeAt[0];cosAt[segs]=cosAt[0];sinAt[segs]=sinAt[0];
            int centre=Vert(c+new Vector3(0,baseY+H0,0),Vector3.UnitY,0,0,0,seed,heat,crust,PoolKind,gain,spread,age,Rs,R);
            int first=VertexCount;
            for(int k=0;k<=rings.Length;k++)
            {
                bool skirt=k==rings.Length;float rho=skirt?1:rings[k];
                // Flat top, a rounded meniscus at the edge: height (1 - rho^6)^0.5; the skirt tucks the lip under the floor line.
                float rho5=rho*rho*rho*rho*rho,prof=(float)Math.Sqrt(Math.Max(0,1-rho5*rho)),y=skirt?-.01f:baseY+H0*prof,steep=H0*3*rho5/Math.Max(.03f,prof);
                for(int i=0;i<=segs;i++)
                {
                    float ca=cosAt[i],sa=sinAt[i];float edge=edgeAt[i];
                    if(skirt||k==rings.Length-1){emin=Math.Min(emin,edge);emax=Math.Max(emax,edge);}
                    float r=edge*rho;var o=new Vector3(ca,0,sa);
                    float slope=skirt?8:Math.Min(8,steep/Math.Max(.05f,edge));
                    var n=Vector3.Normalize(Vector3.UnitY+o*slope);
                    var p=c+o*r+new Vector3(0,y,0);
                    Vert(p,n,o.X*r,o.Z*r,r/Math.Max(.01f,R),seed,heat,crust,PoolKind,gain,spread,age,Math.Max(0,edge-r),R);
                }
            }
            for(int i=0;i<segs;i++)Tri(centre,first+i+1,first+i);
            for(int k=0;k<rings.Length;k++)for(int i=0;i<segs;i++){int a=first+k*(segs+1)+i,b=a+segs+1;Quad(a,a+1,b+1,b);}
            if(PoolCount<Pools.Length)Pools[PoolCount++]=new MoltenPoolShape{Id=id,X=c.X,Y=c.Y,Z=c.Z,Radius=R,EdgeMin=emin,EdgeMax=emax,Spread=spread,Heat=heat,Crust=crust};
        }

        // ============================================================================================================
        // droplets, lumps and strips
        // ============================================================================================================
        /// <summary>
        /// A droplet (or a lump of slag) at `c` moving at `vel`: a sphere of radius `r` stretched `stretch` along the motion into
        /// a teardrop (its tail longer behind); `rough` 0..1 knobbles it (slag); `crust` its dark skin with glowing cracks.
        /// </summary>
        public void Blob(Vector3 c,Vector3 vel,float r,float stretch,float heat,float crust,float seed,float rough,float gain=1)
        {
            if(!(r>.005f))return;int lat=low?4:6,lon=low?6:10;int nv=(lat+1)*(lon+1),ni=lat*lon*6;if(!Room(nv,ni))return;Blobs++;
            float sp=vel.Length();var f=sp>1e-3f?vel/sp:Vector3.UnitY;var side=Vector3.Normalize(Vector3.Cross(f,Math.Abs(f.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var up=Vector3.Cross(side,f);
            int first=VertexCount;uint sd=(uint)(seed*7919);
            for(int a=0;a<=lat;a++)
            {
                float th=a*BossMath.Pi/lat,ct=(float)Math.Cos(th),st=(float)Math.Sin(th);
                for(int b=0;b<=lon;b++)
                {
                    float ph=b*BossMath.Tau/lon,cp=(float)Math.Cos(ph),spp=(float)Math.Sin(ph);int j=b%lon;
                    float k=1+rough*(H(sd,a*31+j)-.5f)*(a==0||a==lat?0:1);
                    // ct along the motion: +1 the head, -1 the tail (longer behind).
                    float along=ct>0?ct*(1+stretch*.35f):ct*(1+stretch);var lp=new Vector3(st*cp*k,along*k,st*spp*k);
                    var p=c+(side*lp.X+f*lp.Y+up*lp.Z)*r;var n=Vector3.Normalize(side*(st*cp)+f*(ct/(1+stretch*.5f))+up*(st*spp));
                    Vert(p,n,lp.X,lp.Y,lp.Z,seed,heat,crust,BlobKind,gain,sp,0,r,r);
                }
            }
            for(int a=0;a<lat;a++)for(int b=0;b<lon;b++){int i0=first+a*(lon+1)+b,i1=i0+lon+1;Quad(i0,i1,i1+1,i0+1);}
        }
        /// <summary>
        /// A flowing strip along <see cref="PathP"/>[0..n) on a surface (<see cref="PathN"/>: its normals), half width
        /// <see cref="PathW"/>, heat <see cref="PathHeat"/>: crowned `bulge` m at its centre line, the metal running along it at
        /// `speed` m/s (toward the last point), `crust` its plates.
        /// </summary>
        public void Ribbon(int n,float speed,float crust,float bulge,float seed,float gain=1,float lift=.004f)
        {
            if(n<2)return;int nv=n*3,ni=(n-1)*12;if(!Room(nv,ni))return;Ribbons++;int first=VertexCount;float along=0,total=0;
            for(int i=1;i<n;i++)total+=Vector3.Distance(PathP[i],PathP[i-1]);
            for(int i=0;i<n;i++)
            {
                if(i>0)along+=Vector3.Distance(PathP[i],PathP[i-1]);
                var d=i+1<n?PathP[i+1]-PathP[i]:PathP[i]-PathP[i-1];var nn=PathN[i];var s=Vector3.Cross(nn,d);float sl=s.Length();s=sl>1e-5f?s/sl:Vector3.UnitX;
                float w=PathW[i],u=total>0?along/total:0,h=PathHeat[i];
                Vert(PathP[i]-s*w+nn*lift,Vector3.Normalize(nn-s*.6f),along,-1,u,seed,h,crust,RibbonKind,gain,speed,0,w,w);
                Vert(PathP[i]+nn*(lift+bulge*Math.Min(1,w*6)),nn,along,0,u,seed,h,crust,RibbonKind,gain,speed,0,w,w);
                Vert(PathP[i]+s*w+nn*lift,Vector3.Normalize(nn+s*.6f),along,1,u,seed,h,crust,RibbonKind,gain,speed,0,w,w);
            }
            for(int i=0;i+1<n;i++){int a=first+i*3,b=a+3;Quad(a,b,b+1,a+1);Quad(a+1,b+1,b+2,a+2);}
        }
    }
}
