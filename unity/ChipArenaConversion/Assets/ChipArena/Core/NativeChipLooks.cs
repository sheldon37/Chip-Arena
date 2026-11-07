using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>How one chip part (NativeShotRules.Modules) draws in the effects drawer: a soft streak for a box, a flame or a shard for a cone, a
    /// glint, a covering dark core or a smooth ball for an orb, a ring card for a ring.</summary>
    public enum NativeChipPart{Streak,Flame,Shard,Glint,Dark,Ball,Ring}
    /// <summary>What NativeShotPresentation makes of one of the scene's draws: its source mesh, a chip part, a heavy round's slug, Bowling Club's ball.</summary>
    public enum NativeShotLook:byte{Source,Part,Slug,Bowling}

    /// <summary>A shot model's smooth mesh in source numbers: positions, unit normals, a shade per vertex (1: the draw's colour; 0: black) and triangles
    /// whose front faces are outward by the same rule as NativeShotGeometry.</summary>
    public sealed class NativeChipMesh
    {
        public readonly float[] Positions,Normals,Paint;public readonly int[] Indices;
        public int VertexCount=>Positions.Length/3;
        public NativeChipMesh(float[] positions,float[] normals,float[] paint,int[] indices){Positions=positions;Normals=normals;Paint=paint;Indices=indices;}
    }

    /// <summary>
    /// The chips and shots that were still built of boxes (owner 2026-11-04: "I think some chips still use voxel implementations too"). The source
    /// tables stay as they are (NativeShotRules.Modules, NativeShotShapes, the parity fixtures); this says what the game draws in their place, on the
    /// same transforms: every chip part becomes a soft effect in the weapon-effects drawer, so Split's and Velocity's box streaks are streaks; Bowling Club's cube
    /// is a ball with finger holes that turns as it flies; Blade Runner's flat box is a crescent of energy; a round a chip has made heavy (Dense Core,
    /// Lobber, Firework Factory, Going Up) keeps its size as a smooth slug under a wider tracer, so its danger still shows; the frag, the
    /// rocket, the orbital mark and ball lightning get smooth bodies at their source sizes. Reduced effects and the low tier thin the parts, and a
    /// frame's budget holds any stack. Pure and cosmetic: nothing here reaches collision, damage or a frame.
    /// </summary>
    public static class NativeChipLooks
    {
        /// <summary>A gun round's radius before chips; a round this much larger is drawn heavy; how far that widens its tracer.</summary>
        public const float RoundRadius=.045f,HeavyFrom=1.3f,HeavyWiden=2f;
        /// <summary>An orb part as large as this is a ball (Pong's); a colour darker than this covers instead of glowing (Vacuum's core).</summary>
        public const float BallFrom=.15f,DarkBelow=.08f;
        /// <summary>A ring's line sits at this share of its card (the shader's FxRing), so a card reaches past the part's radius by its inverse.</summary>
        public const float RingShare=.8f;
        /// <summary>Widths and sizes as multiples of the source part's, for a soft edge that shows the part's own size.</summary>
        public const float StreakWidth=2f,FlameWidth=3f,ShardWidth=2.6f,GlintSize=3.4f,DarkSize=3f;
        /// <summary>The fewest metres wide a streak is drawn.</summary>
        public const float StreakMin=.018f;
        /// <summary>A part's strength as it nears the eye and far off is the tracer head's (NativeTracers.HeadNear).</summary>
        public static float Near(float distance)=>NativeTracers.HeadNear(distance);

        /// <summary>How a source part draws. radius: the part's world size (an orb's radius); r, g, b: its linear colour.</summary>
        public static NativeChipPart Part(NativeShotShape shape,NativeShotRules.PartAnim anim,float radius,float r,float g,float b)
        {
            switch(shape)
            {
                case NativeShotShape.Ring:return NativeChipPart.Ring;
                case NativeShotShape.ModuleCone:return anim==NativeShotRules.PartAnim.Flame?NativeChipPart.Flame:NativeChipPart.Shard;
                case NativeShotShape.Orb:return Math.Max(r,Math.Max(g,b))<DarkBelow?NativeChipPart.Dark:radius>=BallFrom?NativeChipPart.Ball:NativeChipPart.Glint;
                default:return NativeChipPart.Streak;
            }
        }
        /// <summary>A part's light (the core's white comes from the shader's hot share).</summary>
        public static float Gain(NativeChipPart part)=>part==NativeChipPart.Streak?1.5f:part==NativeChipPart.Flame?1.9f:part==NativeChipPart.Shard?1.9f:part==NativeChipPart.Ring?1.5f:1.5f;
        /// <summary>Whether a shot's part `index` draws: a stack keeps its first parts, Reduced effects every other one of its first six, the low tier its first eight.</summary>
        public static bool Keep(int index,float quality,bool low)=>quality<.5f?index%2==0&&index<6:low?index<8:index<14;
        /// <summary>The most parts a frame draws, whatever is in the air.</summary>
        public static int Budget(float quality,bool low)=>quality<.5f?60:low?120:240;

        /// <summary>A round a chip has made heavy shows it.</summary>
        public static bool Heavy(double radius)=>radius>=RoundRadius*HeavyFrom;
        /// <summary>A heavy round's tracer is wider, and its head larger, by the root of its size (a long way past the base is held).</summary>
        public static void HeavyTracer(ref NativeTracerStyle s,double radius)
        {
            if(!Heavy(radius))return;float k=Math.Min(HeavyWiden,(float)Math.Sqrt(radius/RoundRadius));s.Core*=k;s.Sheath*=k;s.HeadSize*=k;
        }

        /// <summary>The ball's colour; how fast it visibly turns (rad/s: a true roll at this speed would be a blur); past this it loses its finger holes (m).</summary>
        public const int BallColor=0x3f4e86;public const float BallTurn=14,BallFar=26;
        /// <summary>The ball's turn about its flight's side axis.</summary>
        public static float BallSpin(long id,double time)=>(float)((id&31)*.7+time*BallTurn);
        /// <summary>The ball's rim of light and its highlight (premultiplied gains), its wake's gain and seconds of flight (never longer than it has flown).</summary>
        public const float BallRim=.5f,BallGlint=1.1f,BallWake=.55f,BallWakeTail=.045f;
        /// <summary>The source's flat shading is none: a smooth model takes its shape from the sky above it.</summary>
        public static float Shade(float normalY)=>.58f+.42f*Math.Max(0,normalY);

        /// <summary>Blade Runner's blade: its arc's radius (m), half span (rad), lean back from flat (rad), roll off level (rad), band as a share of the radius,
        /// and how far its tips sweep behind its middle as a share of the radius.</summary>
        public const float BladeRadius=.56f,BladeSpan=1.22f,BladeLean=.87f,BladeRoll=.3f,BladeBand=.6f,BladeSweep=.5f;
        /// <summary>The blade's quads along the arc, and its echoes behind it.</summary>
        public static int BladeSegments(float quality,bool low)=>quality<.5f?6:low?8:10;
        public static int BladeEchoes(float quality,bool low)=>quality<.5f?0:low?1:2;
        /// <summary>From this many blades in a frame on, the rest are drawn without echoes.</summary>
        public const int BladeEchoLimit=8;
        /// <summary>Its light, its tips' wakes' gain and length (m).</summary>
        public const float BladeGain=2f,BladeWake=1.1f,BladeWakeLength=1.5f;
        /// <summary>Echo `k` (0 the blade itself): metres behind it, size and light.</summary>
        public static void BladeEcho(int k,out float back,out float size,out float gain){back=k==0?0:k==1?.42f:.9f;size=k==0?1:k==1?.94f:.86f;gain=k==0?1:k==1?.42f:.2f;}
        /// <summary>The blade's frame: `side` across it, `lean` the way its middle points. A flat blade is lost edge-on and a blade facing its flight is lost side-on,
        /// so it leans between them, and rolls off level (by shot, one way or the other) so it reads as a cut.</summary>
        public static void BladeFrame(Vector3 dir,long id,out Vector3 side,out Vector3 lean)
        {
            side=Vector3.Cross(Vector3.UnitY,dir);if(side.LengthSquared()<1e-6f)side=Vector3.UnitX;side=Vector3.Normalize(side);var up=Vector3.Cross(dir,side);
            float roll=(id&1)==0?BladeRoll:-BladeRoll,c=(float)Math.Cos(roll),s=(float)Math.Sin(roll);var s2=side*c+up*s;var u2=up*c-side*s;
            side=s2;lean=dir*(float)Math.Cos(BladeLean)+u2*(float)Math.Sin(BladeLean);
        }
        /// <summary>A point of the blade's arc at u (0 one tip, 1 the other) and `band` (1 its outer edge, 0 its inner one), from its centre, scaled by `size`.</summary>
        public static Vector3 BladePoint(float u,float band,Vector3 side,Vector3 lean,Vector3 dir,float size)
        {
            float a=(u*2-1)*BladeSpan,r=BladeRadius*size,ring=r*(1-BladeBand*(1-band)),cos=(float)Math.Cos(a),sin=(float)Math.Sin(a);
            return side*(ring*sin)+lean*(ring*cos-r*.55f)-dir*(r*BladeSweep*(1-cos));
        }

        private sealed class Build
        {
            public readonly List<float> P=new List<float>(),N=new List<float>(),C=new List<float>();public readonly List<int> I=new List<int>();
            public int Count=>P.Count/3;
            public void Vertex(double x,double y,double z,double nx,double ny,double nz,double paint)
            {
                double l=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(l<1e-9){nx=0;ny=1;nz=0;l=1;}
                P.Add((float)x);P.Add((float)y);P.Add((float)z);N.Add((float)(nx/l));N.Add((float)(ny/l));N.Add((float)(nz/l));C.Add((float)paint);
            }
            /// <summary>A surface turned round the z axis from a profile of (z, radius) pairs with z rising, smooth along it, its middle at (x, y, z).</summary>
            public void Lathe(double[] profile,int radial,double paint=1,double x=0,double y=0,double z=0)
            {
                int rings=profile.Length/2,start=Count;
                for(int i=0;i<rings;i++)
                {
                    int a=Math.Max(0,i-1),b=Math.Min(rings-1,i+1);double dz=profile[b*2]-profile[a*2],dr=profile[b*2+1]-profile[a*2+1],pz=profile[i*2],pr=profile[i*2+1];
                    for(int j=0;j<=radial;j++)
                    {
                        double t=j*Math.PI*2/radial,cos=Math.Cos(t),sin=Math.Sin(t);
                        Vertex(x+pr*cos,y+pr*sin,z+pz,dz*cos,dz*sin,-dr,paint);
                    }
                }
                for(int i=0;i+1<rings;i++)for(int j=0;j<radial;j++)
                {
                    int p=start+i*(radial+1)+j,q=p+1,s=p+radial+1,r=s+1;
                    if(profile[i*2+1]>1e-6){I.Add(p);I.Add(q);I.Add(r);}
                    if(profile[i*2+3]>1e-6){I.Add(p);I.Add(r);I.Add(s);}
                }
            }
            /// <summary>A sphere about (x, y, z).</summary>
            public void Ball(double radius,int rings,int radial,double paint=1,double x=0,double y=0,double z=0)
            {
                var profile=new double[(rings+1)*2];for(int i=0;i<=rings;i++){double t=-Math.PI/2+Math.PI*i/rings;profile[i*2]=radius*Math.Sin(t);profile[i*2+1]=i==0||i==rings?0:radius*Math.Cos(t);}
                Lathe(profile,radial,paint,x,y,z);
            }
            /// <summary>A flat disc on a ball of `radius` about the origin, centred on direction (dx, dy, dz).</summary>
            public void Disc(double dx,double dy,double dz,double radius,double size,int sides,double paint)
            {
                double l=Math.Sqrt(dx*dx+dy*dy+dz*dz);dx/=l;dy/=l;dz/=l;double ax=Math.Abs(dy)<.9?0:1,ay=Math.Abs(dy)<.9?1:0;
                double ux=ay*dz,uy=-ax*dz,uz=ax*dy-ay*dx,ul=Math.Sqrt(ux*ux+uy*uy+uz*uz);ux/=ul;uy/=ul;uz/=ul;double vx=dy*uz-dz*uy,vy=dz*ux-dx*uz,vz=dx*uy-dy*ux;
                double cx=dx*radius*1.006,cy=dy*radius*1.006,cz=dz*radius*1.006;int c=Count;Vertex(cx,cy,cz,dx,dy,dz,paint);
                for(int j=0;j<sides;j++){double t=j*Math.PI*2/sides,ct=Math.Cos(t)*size,st=Math.Sin(t)*size;Vertex(cx+ux*ct+vx*st,cy+uy*ct+vy*st,cz+uz*ct+vz*st,dx,dy,dz,paint);}
                for(int j=0;j<sides;j++){I.Add(c);I.Add(c+1+j);I.Add(c+1+(j+1)%sides);}
            }
            /// <summary>A thin plate seen from both sides, corners in order.</summary>
            public void Plate(double[] a,double[] b,double[] c,double[] d,double paint)
            {
                double ex=b[0]-a[0],ey=b[1]-a[1],ez=b[2]-a[2],fx=c[0]-a[0],fy=c[1]-a[1],fz=c[2]-a[2],nx=ey*fz-ez*fy,ny=ez*fx-ex*fz,nz=ex*fy-ey*fx;
                int s=Count;foreach(var v in new[]{a,b,c,d})Vertex(v[0],v[1],v[2],nx,ny,nz,paint);I.Add(s);I.Add(s+1);I.Add(s+2);I.Add(s);I.Add(s+2);I.Add(s+3);
                s=Count;foreach(var v in new[]{a,b,c,d})Vertex(v[0],v[1],v[2],-nx,-ny,-nz,paint);I.Add(s);I.Add(s+2);I.Add(s+1);I.Add(s);I.Add(s+3);I.Add(s+2);
            }
            public NativeChipMesh Mesh()=>new NativeChipMesh(P.ToArray(),N.ToArray(),C.ToArray(),I.ToArray());
        }
        // The ball and the slug fill the source's unit box, so the box's transform sizes them to the round's own radius.
        private static NativeChipMesh ball,ballFar,ballPlain,slug,frag,rocket,mark,lightning;
        /// <summary>Bowling Club's ball (radius .5, three finger holes).</summary>
        public static NativeChipMesh Ball=>ball??=BallMesh();
        /// <summary>The ball past BallFar: fewer faces, no holes.</summary>
        public static NativeChipMesh BallAway=>ballFar??=PlainBall(6,8);
        /// <summary>A plain smooth ball of radius .5 (Pong's).</summary>
        public static NativeChipMesh BallPlain=>ballPlain??=PlainBall(10,14);
        /// <summary>A heavy round: a blunt bullet filling the unit box along z.</summary>
        public static NativeChipMesh Slug=>slug??=SlugMesh();
        private static NativeChipMesh BallMesh()
        {
            var b=new Build();b.Ball(.5,12,18);
            b.Disc(.2,.28,.94,.5,.075,10,.08);b.Disc(-.2,.28,.94,.5,.075,10,.08);b.Disc(0,-.1,.99,.5,.085,10,.08);
            return b.Mesh();
        }
        private static NativeChipMesh PlainBall(int rings,int radial){var b=new Build();b.Ball(.5,rings,radial);return b.Mesh();}
        private static NativeChipMesh SlugMesh()
        {
            var b=new Build();b.Lathe(new[]{-.5,0,-.5,.3,-.4,.43,-.05,.5,.22,.42,.4,.2,.5,0},10);return b.Mesh();
        }
        private static NativeChipMesh FragMesh()
        {
            // The source's body and neck, turned; the lever a thin plate down its side.
            var b=new Build();b.Lathe(new[]{-.085,0,-.082,.03,-.055,.07,0,.082,.055,.07,.082,.036,.09,.03,.135,.03,.14,0},10);
            b.Plate(new[]{.012,.03,.13},new[]{.012,.09,.03},new[]{-.012,.09,.03},new[]{-.012,.03,.13},.75);
            return b.Mesh();
        }
        private static NativeChipMesh RocketMesh()
        {
            var b=new Build();b.Lathe(new[]{-.26,0,-.26,.045,-.24,.066,.14,.066},10);b.Lathe(new[]{.14,.066,.22,.052,.3,.018,.33,0},10,.62);
            for(int k=0;k<4;k++)
            {
                double t=k*Math.PI/2+Math.PI/4,c=Math.Cos(t),s=Math.Sin(t);
                b.Plate(new[]{c*.06,s*.06,-.11},new[]{c*.06,s*.06,-.25},new[]{c*.165,s*.165,-.29},new[]{c*.165,s*.165,-.22},.55);
            }
            return b.Mesh();
        }
        private static NativeChipMesh MarkMesh(){var b=new Build();b.Lathe(new[]{-.3,0,-.2,.012,.12,.03,.22,.024,.3,0},8);return b.Mesh();}
        private static NativeChipMesh LightningMesh(){var b=new Build();b.Ball(.21,8,12);b.Ball(.13,6,10,1,.16,.12,-.1);b.Ball(.12,6,10,1,-.14,-.1,.14);return b.Mesh();}
        /// <summary>The share of the combat glow's halo an ability shot keeps, so its smooth body shows through (NativeGlowRules sizes it by the shot's radius, up to 4 m).</summary>
        public static float HaloShare(string ability)=>ability=="ball-lightning"?.4f:ability=="frag-grenade"||ability=="rocket"?.5f:1;
        /// <summary>The radius the energy trail draws ball lightning at (the rest of the energy shots: their own): a ball of light, not a 4 m flash over the caster.</summary>
        public const float LightningGlow=.26f;
        /// <summary>The smooth body for an ability shot's source boxes; null: its source mesh stays (those are not box-built, or NativeMagicFx owns them).</summary>
        public static NativeChipMesh Smooth(NativeShotShape shape)
        {
            switch(shape)
            {
                case NativeShotShape.Frag:return frag??=FragMesh();
                case NativeShotShape.Rocket:return rocket??=RocketMesh();
                case NativeShotShape.OrbitalMark:return mark??=MarkMesh();
                case NativeShotShape.BallLightning:return lightning??=LightningMesh();
                default:return null;
            }
        }
    }
}
