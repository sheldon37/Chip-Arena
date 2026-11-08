using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One vertex of THE DEEP END's water meshes as Core builds it (Runtime uploads the array as it is): position, normal, A (u, v, kind, seed), B (a linear colour and a share, by kind).</summary>
    public struct DeepEndFlowVertex{public float Px,Py,Pz,Nx,Ny,Nz,Au,Av,Kind,Seed,R,G,B,A;}

    /// <summary>
    /// A frame's water geometry (Resources/NativeDeepEndFlow.shader): falling sheets, the jet's tube, the hands of water, foam collars, the room's round lights and their halos, shadows on the water, drifting tile. One
    /// vertex array, drawn in one call (Runtime NativeDeepEndBossWater). Fixed capacity; past it a shape is refused. Allocation free. Pure.
    /// </summary>
    public sealed class DeepEndFlow
    {
        public const float Sheet=0,Jet=1,Hand=2,Collar=3,Halo=4,Shadow=5,Solid=6,Mark=7,Disc=8,Curtain=9,Crown=10;
        public readonly DeepEndFlowVertex[] V;public readonly int[] T;public int VertexCount,IndexCount,Refused;
        public DeepEndFlow(int vertices,int indices){V=new DeepEndFlowVertex[vertices];T=new int[indices];}
        public void Clear(){VertexCount=IndexCount=0;Refused=0;}
        public bool Room(int v,int i){if(VertexCount+v>V.Length||IndexCount+i>T.Length){Refused++;return false;}return true;}
        public int Add(Vector3 p,Vector3 n,float u,float v,float kind,float seed,Vector3 rgb,float a)
        {
            V[VertexCount]=new DeepEndFlowVertex{Px=p.X,Py=p.Y,Pz=p.Z,Nx=n.X,Ny=n.Y,Nz=n.Z,Au=u,Av=v,Kind=kind,Seed=seed,R=rgb.X,G=rgb.Y,B=rgb.Z,A=a};return VertexCount++;
        }
        public void Tri(int a,int b,int c){T[IndexCount++]=a;T[IndexCount++]=b;T[IndexCount++]=c;}
        public void Quad(int a,int b,int c,int d){Tri(a,b,c);Tri(a,c,d);}
        /// <summary>A flat quad at `c` spanning ±x, ±y (u, v -1..1 across it).</summary>
        public void Billboard(Vector3 c,Vector3 x,Vector3 y,Vector3 n,float kind,float seed,Vector3 rgb,float a)
        {
            if(!Room(4,6))return;int i=Add(c-x-y,n,-1,-1,kind,seed,rgb,a);Add(c+x-y,n,1,-1,kind,seed,rgb,a);Add(c+x+y,n,1,1,kind,seed,rgb,a);Add(c-x+y,n,-1,1,kind,seed,rgb,a);Quad(i,i+1,i+2,i+3);
        }
        /// <summary>A round tube along the `count` centres with radii (its u along it 0..1 scaled, v round it).</summary>
        public void Tube(Vector3[] c,float[] r,int count,int sides,float kind,float seed,Vector3 rgb,float a,float uScale=1)
        {
            if(count<2||!Room(count*(sides+1),(count-1)*sides*6))return;int first=VertexCount;var refUp=Vector3.UnitY;
            for(int i=0;i<count;i++)
            {
                var t=c[Math.Min(count-1,i+1)]-c[Math.Max(0,i-1)];float l=t.Length();t=l>1e-4f?t/l:Vector3.UnitZ;var x=Vector3.Cross(refUp,t);float xl=x.Length();if(xl<.08f){x=Vector3.Cross(Vector3.UnitX,t);xl=x.Length();}x/=xl;var y=Vector3.Cross(t,x);
                for(int k=0;k<=sides;k++){float an=BossMath.Tau*k/sides,cs=(float)Math.Cos(an),sn=(float)Math.Sin(an);var d=x*cs+y*sn;Add(c[i]+d*r[i],d,i/(float)(count-1)*uScale,k/(float)sides,kind,seed,rgb,a);}
            }
            for(int i=0;i<count-1;i++)for(int k=0;k<sides;k++){int a0=first+i*(sides+1)+k,a1=a0+1,b0=a0+sides+1,b1=b0+1;Quad(a0,b0,b1,a1);}
        }
    }

    /// <summary>
    /// THE DEEP END's effects as functions of the record and the clock (doc section 7): the flank lamps' and throat's light, the room's lights and their halos, the sheets pouring off it for seconds after it rises, foam
    /// collars where it pierces the surface, spray and mist, the jet's solid beam, the hands of water, the telegraphs (the dark over Breach's circle, the Flop's long shadow, the mist line), the debris going round the
    /// whirlpool. Pure and allocation free: host, guests and the killcam draw one frame from one record. Particles thin with the effects' quality. <see cref="Couple"/> is what the body does to the wave field.
    /// </summary>
    public sealed class NativeDeepEndBossFx
    {
        public readonly NativeBossVfx Vfx=new NativeBossVfx(3200);
        /// <summary>What is drawn under the water sheet (refracted by it, absorbed by its depth) and what is over it.</summary>
        public readonly DeepEndFlow Under=new DeepEndFlow(8192,16384),Over=new DeepEndFlow(24000,60000);
        /// <summary>The flank lamps' glow, the small running lights', the throat's (the toon's emission share).</summary>
        public readonly float[] LampGlow=new float[NativeDeepEndBossLook.Lamps];public float MarkerGlow,ThroatGlow;
        /// <summary>This frame's lights the room takes from it (world positions and ranges, x y z range; linear colours).</summary>
        public const int MaxLights=4;public readonly float[] LightPos=new float[MaxLights*4],LightRgb=new float[MaxLights*3];public int LightCount;
        /// <summary>The ring buoy going round in the whirlpool: shown, where, how turned.</summary>
        public bool FloatOn;public Vector3 FloatAt;public Quaternion FloatTurn;
        /// <summary>The pilots, in the room's frame (the Whistle's rings): the caller's array, the stand-ins in the preview.</summary>
        public Vector2[] Pilots=NativeDeepEndBossPreview.Pilots;public int PilotCount=NativeDeepEndBossPreview.Pilots.Length;
        private const uint Seed=0xDEE9E7D;
        private static float H(int i)=>BossMath.Hash(Seed,i);
        private static readonly Vector3 LampTint=new Vector3(.52f,1f,.98f),WaterWhite=new Vector3(.82f,.95f,.97f),Foam=new Vector3(.93f,.97f,.97f);
        /// <summary>The surface's height at (x, z, the room's frame) as the water draws it, from the composed field.</summary>
        public static float SurfaceAt(DeepEndWaves w,float x,float z)
        {
            float gx=x/DeepEndWaves.Texel+DeepEndWaves.Size*.5f-.5f,gz=z/DeepEndWaves.Texel+DeepEndWaves.Size*.5f-.5f;int j=(int)Math.Floor(gx),k=(int)Math.Floor(gz);if(j<0)j=0;if(k<0)k=0;if(j>DeepEndWaves.Size-2)j=DeepEndWaves.Size-2;if(k>DeepEndWaves.Size-2)k=DeepEndWaves.Size-2;float fx=BossMath.Clamp01(gx-j),fz=BossMath.Clamp01(gz-k);int i=(j+DeepEndWaves.Size*k)*2;var g=w.Rg;
            return (g[i]*(1-fx)+g[i+2]*fx)*(1-fz)+(g[i+DeepEndWaves.Size*2]*(1-fx)+g[i+DeepEndWaves.Size*2+2]*fx)*fz;
        }
        private readonly Vector3[] tubeC=new Vector3[32];private readonly float[] tubeR=new float[32];

        /// <summary>What the body and the record do to the waves this frame (a wake, the collars' foam, a move's impact on its hit). `last` the clock at the last frame.</summary>
        public void Couple(DeepEndBossRig rig,in DeepEndBossView v,DeepEndWaves w,in DeepEndBossStage s,float last,float now)
        {
            float dt=now-last;if(!(dt>0)||dt>.25f)return;var o=rig.Origin;float tide=s.Tide;
            for(int i=0;i<NativeDeepEndBossLook.Spine;i++)
            {
                var p=rig.Shown.Position[i]-o;float r=NativeDeepEndBossLook.Radius(i*NativeDeepEndBossLook.Seg),depth=tide-p.Y;if(depth<-r*1.05f||depth>r*2.2f)continue;
                var vel=rig.Velocity[i];float hs=BossMath.FlatLength(vel),vy=Math.Abs(vel.Y),sp=hs+vy*.6f;if(sp<.25f)continue;
                float near=1-Math.Min(1,Math.Max(0,depth-r*.4f)/(r*1.8f));float amount=near*Math.Min(.2f,sp*.012f)*(dt*60)*.3f;
                // A hump ahead of the body, a hollow behind it; foam where it cuts.
                float dx=hs>.3f?vel.X/hs:0,dz=hs>.3f?vel.Z/hs:0;
                w.Disturb(p.X+dx*r*.9f,p.Z+dz*r*.9f,r*1.05f,amount);w.Disturb(p.X-dx*r*.9f,p.Z-dz*r*.9f,r*1.2f,-amount*.8f);
                if(depth>-r&&depth<r*1.1f)w.AddFoam(p.X,p.Z,r+.5f,Math.Min(.8f,sp*.09f)*dt*7);
            }
            // ---- the moves' impacts, once each (when their hit falls in this frame) ----
            bool In(float at)=>at>last&&at<=now;
            var b=NativeDeepEndBossBeats.Current;
            if(DeepEndBossView.At(v.spawned))
            {
                float a0=last-v.spawned,a1=now-v.spawned;
                if(a0<b.Ripple&&a1>=b.Ripple)w.Disturb(5.2f,-2.8f,.6f,-.045f);
                if(a0<b.Ripple+.9f&&a1>=b.Ripple+.9f)w.Disturb(5.2f,-2.8f,.5f,-.016f);
                if(a0<b.Break&&a1>=b.Break){var bk=-BossMath.Forward(v.yaw*BossMath.Deg)*NativeDeepEndBoss.BreakBack;w.Disturb(bk.X,bk.Z,3f,.55f,.8f);w.Disturb(bk.X,bk.Z,5.5f,-.22f,.2f);}
                float fall=b.Break+2*(5.6f/DeepEndBossPaths.IntroG);
                if(a0<fall&&a1>=fall){w.Disturb(rig.MouthAt.X-o.X,rig.MouthAt.Z-o.Z,3.4f,-.55f,1);}
            }
            if(v.Moving(now,out var m,out float age,out float hit))
            {
                float at=v.actionAt+hit;var tp=new Vector3(v.targetX,0,v.targetZ);
                if(m.Id=="breach"&&In(at)){w.Disturb(tp.X,tp.Z,m.Radius,.7f,1);w.Disturb(tp.X,tp.Z,m.Radius*2,-.28f,.2f);}
                if(m.Id=="flop"&&In(at))
                {
                    var e=new Vector3(v.aimX,0,v.aimZ);for(int k=0;k<=12;k++){var q=Vector3.Lerp(tp,e,k/12f);w.Disturb(q.X,q.Z,3.1f,-.5f,1);w.Disturb(q.X,q.Z,6,.1f,.2f);}
                }
                // The jet: hard where it comes down at its end, the spray raining off it pocking the water under its line.
                if(m.Id=="jet"&&age>=hit&&age<hit+m.Strike&&s.Tide>-.5f){var head=rig.MouthAt-o;var d=DeepEndBossRig.JetDir(v,age,hit,m,head,s.Tide);float reach=JetReach(head,d,m,out var flatD,out _);var land=head+flatD*reach;
                    w.Disturb(land.X,land.Z,1.5f,-Math.Min(.14f,.13f*(dt*60)),.5f);
                    for(int k=1;k<=5;k++){var q=head+flatD*(reach*k/6f);if(Math.Abs(q.X)<NativeDeepEndArena.Half&&Math.Abs(q.Z)<NativeDeepEndArena.Half){w.Disturb(q.X,q.Z,1.1f,-Math.Min(.04f,.035f*(dt*60)),.4f);w.AddFoam(q.X,q.Z,1.2f,.5f*dt*7);}}}
                if(m.Id=="lull"&&In(at)){w.Disturb(tp.X,tp.Z,2.2f,.25f,.8f);}
                if(m.Id=="seiche"){var ax=s.SeicheAxis;float half=NativeDeepEndArena.Half;float t0=age-hit,tl=t0-dt;float cross=2*half/m.Speed;if(t0>=cross&&tl<cross){for(int k=-8;k<=8;k++){float side=k*2.5f;w.Disturb(ax.X*(half-1.2f)+ax.Z*side,ax.Z*(half-1.2f)-ax.X*side,2.4f,.32f,.9f);}}}
                if(m.Id=="whistle"&&In(at))for(int k=0;k<PilotCount;k++){var pl=Pilots[k];w.Disturb(pl.X+1.2f,pl.Y+.8f,1.4f,-.2f,.7f);}
            }
            if(DeepEndBossView.At(v.phaseAt)&&In(v.phaseAt+.4f)){w.Disturb(0,0,4,.6f,.6f);}
        }

        /// <summary>The frame's effects for the record at `now` on the room's stage. (ex.. the eye's frame, for what faces it.)</summary>
        public void Build(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,Vector3 eye,Vector3 right,Vector3 up,double effects,bool low,bool reduced)
        {
            Vfx.Begin(effects,low);Under.Clear();Over.Clear();LightCount=0;FloatOn=false;var o=rig.Origin;
            float lampLevel=s.Lamps;bool dead=v.Dead(now);float deadAge=dead?now-v.diedAt:0;
            // ---- the lamps' glow: going dark one by one from the tail as it dies ----
            for(int i=0;i<NativeDeepEndBossLook.Lamps;i++)
            {
                float g=lampLevel;if(v.LampBroken(i,now))g=0;
                if(dead){NativeDeepEndBossLook.Lamp(i,out float ls,out _,out _);float at=DeepEndBossRig.DeathSettle+(32-ls)*.11f;g*=1-BossMath.Smooth((deadAge-at)/.35f);}
                LampGlow[i]=g;
            }
            MarkerGlow=lampLevel*(dead?1-BossMath.Smooth((deadAge-2)/1.6f):1);
            ThroatGlow=(.3f+5.5f*rig.Iris)*(dead?1-BossMath.Smooth(deadAge/.8f):1);
            Lights(rig,v,s,now);
            if(rig.Iris>.02f){float g=rig.Iris*(dead?1-BossMath.Smooth(deadAge/.8f):1);Vfx.Glow(rig.MouthAt-rig.MouthDir*.7f,new Vector3(.22f,.62f,1.1f)*(2.6f*g),2.1f,NativeBossVfx.Core);}
            RoomLamps(s,o,right,up);
            Curtains(v,s,o,now);
            BodyLamps(rig,v,s,now,eye,right,up,low);
            Sheets(rig,s,waves,now,low);
            Collars(rig,s,waves,now);
            Marks(rig,v,s,waves,now,low);Warnings(rig,v,s,waves,now);
            Moves(rig,v,s,waves,now,low,reduced);
            Arrival(rig,v,s,waves,now,low);
            Debris(rig,v,s,waves,now,low);
        }
        // The room's lights: the ceiling's round lights and the lights under the water, each with its own level.
        private void RoomLamps(in DeepEndBossStage s,Vector3 o,Vector3 right,Vector3 up)
        {
            // The ceiling's (the level's own disc is hidden while the boss's stage is on).
            float c=PoolroomsLayout.Cell;var warm=Vector3.Lerp(new Vector3(1f,.98f,.92f),new Vector3(1f,.86f,.64f),s.Warm);
            for(int z=0;z<NativeDeepEndArena.Cells;z++)for(int x=0;x<NativeDeepEndArena.Cells;x++)
            {
                if(x%3!=1||z%3!=1)continue;float wx=(x-NativeDeepEndArena.Cells*.5f+.5f)*c,wz=(z-NativeDeepEndArena.Cells*.5f+.5f)*c;
                float order=(float)Math.Sqrt(wx*wx+wz*wz)/28f;float lv=s.Ceiling*(1-BossMath.Smooth((s.CeilingOut*1.25f-order)/.14f));
                var at=o+new Vector3(wx,NativeDeepEndArena.Ceiling-PoolroomsMesh.LightDrop,wz);
                Under.Billboard(at,new Vector3(PoolroomsMesh.LightRadius,0,0),new Vector3(0,0,PoolroomsMesh.LightRadius),-Vector3.UnitY,DeepEndFlow.Disc,0,warm*(.12f+3.4f*lv),1);
            }
            // The ones under the water: a chrome ring in the wall, a lens; a halo in the water (under the sheet, so it shimmers with it).
            var lamps=NativeDeepEndArena.Lamps;
            for(int i=0;i<lamps.Length;i++)
            {
                ref var l=ref lamps[i];float lv=s.Water*(1-BossMath.Smooth((s.WaterOut*1.12f-l.Order)/.05f));if(l.Kind==NativeDeepEndArena.RoomWall){lv*=BossMath.Smooth((s.Tide-l.At.Y+.3f)/.4f);if(lv<.01f)continue;}
                var at=o+l.At;var x=Vector3.Cross(l.Facing,Vector3.UnitY);if(x.LengthSquared()<.01f)x=Vector3.UnitX;x=Vector3.Normalize(x);var y=Vector3.UnitY;
                bool under=l.At.Y<s.Tide;var list=under?Under:Over;float deep=Math.Max(0,s.Tide-l.At.Y);float boost=(float)Math.Exp(Math.Min(3.2f,.105f*s.Murk*deep));
                list.Billboard(at+l.Facing*.012f,x*l.Size,y*l.Size,l.Facing,DeepEndFlow.Disc,1,new Vector3(.62f,1f,1f)*(.08f+5.6f*lv*boost),1);
                if(lv>.02f){float hs=l.Size*4.2f;list.Billboard(at+l.Facing*.1f,x*hs,y*hs,l.Facing,DeepEndFlow.Halo,0,new Vector3(.3f,.82f,.82f)*(.42f*lv*boost),0);}
            }
        }
        // The arches flood shut as it arrives: a curtain of water falling across each, just behind the wall's face; it goes when the room does.
        private void Curtains(in DeepEndBossView v,in DeepEndBossStage s,Vector3 o,float now)
        {
            float k=1;if(DeepEndBossView.At(v.spawned)){var b=NativeDeepEndBossBeats.Current;k=BossMath.Smooth((now-v.spawned-b.Break)/1.6f);}if(v.Dead(now))k*=1-BossMath.Smooth((now-v.diedAt-3.6f)/2.4f);if(k<.01f)return;
            float half=NativeDeepEndArena.Half+PoolroomsLayout.Thickness*.5f+.12f,w=NativeDeepEndArena.ArchHalf,top=NativeDeepEndArena.ArchTop*k;
            void One(Vector3 c,Vector3 along,Vector3 face)
            {
                if(!Over.Room(4,6))return;var bottom=o+c;var up=new Vector3(0,top,0);int i=Over.Add(bottom-along*w,face,-1,0,DeepEndFlow.Curtain,0,WaterWhite,k);Over.Add(bottom+along*w,face,1,0,DeepEndFlow.Curtain,0,WaterWhite,k);Over.Add(bottom+along*w+up,face,1,top,DeepEndFlow.Curtain,0,WaterWhite,k);Over.Add(bottom-along*w+up,face,-1,top,DeepEndFlow.Curtain,0,WaterWhite,k);Over.Quad(i,i+1,i+2,i+3);
            }
            float y=Math.Min(0,s.Tide);One(new Vector3(0,y,half),Vector3.UnitX,-Vector3.UnitZ);One(new Vector3(0,y,-half),Vector3.UnitX,Vector3.UnitZ);One(new Vector3(half,y,0),Vector3.UnitZ,-Vector3.UnitX);One(new Vector3(-half,y,0),Vector3.UnitZ,Vector3.UnitX);
        }
        // The flank lamps' and the running lights' halos, facing the eye: what makes the spiral from above.
        private void BodyLamps(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,float now,Vector3 eye,Vector3 right,Vector3 up,bool low)
        {
            var o=rig.Origin;float surface=s.Tide;var n=Vector3.Normalize(Vector3.Cross(up,right));
            for(int i=0;i<NativeDeepEndBossLook.Lamps;i++)
            {
                float g=LampGlow[i];if(g<.01f)continue;rig.LampFrame(i,out var at,out var q,out float scale);float depth=surface-(at.Y-o.Y);bool under=depth>0;
                // Under the tile, the tile doesn't stop its light: a soft glow on the water over it, passing underneath (Breach's tell, its route between moves).
                float floorAt=NativeDeepEndArena.Floor(at.X-o.X,at.Z-o.Z);if(at.Y-o.Y<floorAt&&surface>floorAt-.01f&&depth<9){float wy=o.Y+surface+.03f;float gl=g*(1-depth/9f);var flat=new Vector3(at.X,wy,at.Z);Over.Billboard(flat,new Vector3(1.5f,0,0),new Vector3(0,0,1.5f),Vector3.UnitY,DeepEndFlow.Halo,0,new Vector3(.2f,.62f,.62f)*(.8f*gl),0);continue;}bool over=!under||s.Murk>1.05f;float boost=over?(under?(float)Math.Exp(-.012f*depth):1):(float)Math.Exp(Math.Min(4.6f,.105f*depth));var list=over?Over:Under;if(under&&over)at+=(right*(float)Math.Sin(now*2.3f+i*1.7f)+up*(float)Math.Cos(now*1.9f+i))*(.012f*depth);
                var face=BossMath.Rotate(q,new Vector3(0,0,1));float size=scale*(3.1f+.05f*Math.Max(0,depth))*(.7f+.3f*g);
                if(over){Vfx.Glow(at+face*.1f,LampTint*(.55f*g*boost),size*2,NativeBossVfx.Mote);Vfx.Glow(at+face*.1f,new Vector3(.34f,.95f,.95f)*(1.5f*g*boost),size*.8f,NativeBossVfx.Core);}
                else{list.Billboard(at+face*.1f,right*size,up*size,n,DeepEndFlow.Halo,0,LampTint*(.8f*g*boost),0);list.Billboard(at+face*.1f,right*size*.3f,up*size*.3f,n,DeepEndFlow.Halo,0,new Vector3(.5f,1,1)*(2.6f*g*boost),0);}
            }
            if(MarkerGlow<.01f||low)return;
            // The running lights along each coping (the small ones; the bank's glow sub-mesh has their lenses).
            float mg=MarkerGlow;
            for(int side=1;side>=-1;side-=2)for(float ms=5;ms<29;ms+=1)
            {
                bool nearLamp=false;foreach(float q in NativeDeepEndBossLook.LampS)if(Math.Abs(ms-q)<.62f)nearLamp=true;if(nearLamp)continue;
                rig.SpineFrame(ms,out var c,out var r);var local=NativeDeepEndBossLook.Skin(ms,side*NativeDeepEndBossLook.CopingTheta);var at=c+BossMath.Rotate(r,local);float depth=surface-(at.Y-o.Y);bool under=depth>0;bool over=!under||s.Murk>1.05f;float boost=over?(under?(float)Math.Exp(-.012f*depth):1):(float)Math.Exp(Math.Min(4.6f,.105f*depth));if(under&&over)at+=(right*(float)Math.Sin(now*2.6f+ms*2.1f)+up*(float)Math.Cos(now*2.2f+ms))*(.012f*depth);
                float size=.34f+.014f*Math.Max(0,depth);if(over)Vfx.Glow(at,new Vector3(.28f,.9f,.9f)*(1.3f*mg*boost),size*2.1f,NativeBossVfx.Mote);else Under.Billboard(at,right*size,up*size,n,DeepEndFlow.Halo,0,new Vector3(.42f,1,1)*(1.8f*mg*boost),0);
            }
        }
        // The lights the room takes: the mouth's throat when it's open, the two brightest places along the body out of the water or near it.
        private void Lights(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,float now)
        {
            void L(Vector3 at,float range,Vector3 rgb){if(LightCount>=MaxLights)return;int k=LightCount++;LightPos[k*4]=at.X;LightPos[k*4+1]=at.Y;LightPos[k*4+2]=at.Z;LightPos[k*4+3]=range;LightRgb[k*3]=rgb.X;LightRgb[k*3+1]=rgb.Y;LightRgb[k*3+2]=rgb.Z;}
            L(rig.MouthAt+rig.MouthDir*.8f,5+9*rig.Iris,new Vector3(.3f,.72f,1f)*(.12f+.95f*rig.Iris));
            float lamps=s.Lamps*MarkerGlowOr1();
            rig.SpineFrame(8,out var a,out _);rig.SpineFrame(18,out var b,out _);
            float ka=BossMath.Clamp01((a.Y-rig.Origin.Y-s.Tide+2f)/2f),kb=BossMath.Clamp01((b.Y-rig.Origin.Y-s.Tide+2f)/2f);L(a,10,new Vector3(.3f,.86f,.86f)*(.72f*lamps*ka));L(b,10,new Vector3(.3f,.86f,.86f)*(.62f*lamps*kb));
            float MarkerGlowOr1()=>MarkerGlow;
        }
        // The sheets pouring off it for seconds after it rises: a strip off each flank at each joint out of the water, fresh from the skin, falling on true arcs behind the body's motion, breaking up as they fall.
        private void Sheets(DeepEndBossRig rig,in DeepEndBossStage s,DeepEndWaves waves,float now,bool low)
        {
            var o=rig.Origin;int step=low?2:1;
            for(int i=0;i<NativeDeepEndBossLook.Spine-1;i+=step)
            {
                var at=rig.Shown.Position[i];float y=at.Y-o.Y,ground=SurfaceAt(waves,at.X-o.X,at.Z-o.Z);float over=y-ground;if(over<.2f)continue;
                float wet=(float)Math.Exp(-(now-rig.EmergedAt[i])/2.4f);if(wet<.05f)continue;float sn=i*NativeDeepEndBossLook.Seg,rad=NativeDeepEndBossLook.Radius(sn);
                var q=rig.Shown.Rotation[i];var side=BossMath.Rotate(q,new Vector3(1,0,0));var tan=BossMath.Rotate(q,new Vector3(0,0,1));var vel=rig.Velocity[i];
                float fall=(float)Math.Sqrt(2*Math.Max(.2f,over)/BossMath.Gravity);
                var up2=BossMath.Rotate(q,new Vector3(0,1,0));
                for(int f=0;f<4;f++)
                {
                    // The strips leave round the lower side of the body, where water runs off it.
                    float round=f==0?-1:f==1?1:f==2?-.42f:.42f;float height=f<2?-.3f:-.86f;
                    var leave=at+side*(round*rad*.9f)+up2*(height*rad);if(leave.Y-o.Y-ground<.1f||BossMath.Hash(Seed,i*7+f*3)<.08f)continue;float width=NativeDeepEndBossLook.Seg*.62f*(.85f+.5f*BossMath.Hash(Seed,i*3+f));var along=tan*width;
                    const int rows=7;if(!Over.Room((rows+1)*2,rows*6))return;int first=Over.VertexCount;float seed=BossMath.Hash(Seed,i*11+f*5)*50;
                    var n=Vector3.Normalize(side*round+Vector3.UnitY*.05f);
                    for(int r=0;r<=rows;r++)
                    {
                        float u=r/(float)rows,tau=fall*u*u*.65f+fall*u*.35f;
                        // Water at fall time tau left the body tau ago: it trails where the body was, and keeps a share of the body's speed.
                        var trail=vel;if(trail.Y<0)trail.Y*=.2f;var pos=leave-trail*(tau*.72f)+new Vector3(0,-.5f*BossMath.Gravity*tau*tau,0);float spread=1+u*.5f;
                        float a=wet*(1-u*.45f);
                        Over.Add(pos-along*spread,n,-1,tau*2.2f,DeepEndFlow.Sheet,seed,WaterWhite,a);Over.Add(pos+along*spread,n,1,tau*2.2f,DeepEndFlow.Sheet,seed,WaterWhite,a);
                    }
                    for(int r=0;r<rows;r++){int a0=first+r*2;Over.Quad(a0,a0+1,a0+3,a0+2);}
                    // Where the sheet lands: a spatter of foam and small drops.
                    float tl=fall;var land=leave-vel*(tl*.72f);if(Vfx.Scale(10)>5)Vfx.Drop(new Vector3(land.X,o.Y+ground+.25f+.4f*BossMath.Frac(now*2.3f+seed),land.Z),new Vector3(0,.25f,0),WaterWhite,.05f,.5f*wet,.5f,seed);
                }
            }
        }
        // A foam collar where each stretch of the body pierces the surface.
        private void Collars(DeepEndBossRig rig,in DeepEndBossStage s,DeepEndWaves waves,float now)
        {
            var o=rig.Origin;int rings=0;
            for(int i=0;i<NativeDeepEndBossLook.Spine-1&&rings<8;i++)
            {
                var a=rig.Shown.Position[i]-o;var b=rig.Shown.Position[i+1]-o;float sa=SurfaceAt(waves,a.X,a.Z),sb=SurfaceAt(waves,b.X,b.Z),da=a.Y-sa,db=b.Y-sb;if(da*db>0)continue;
                float k=da/(da-db);var c=Vector3.Lerp(a,b,k);float rad=NativeDeepEndBossLook.Radius((i+k)*NativeDeepEndBossLook.Seg);var ax=Vector3.Normalize(a-b);float flat=Math.Max(.35f,Math.Abs(ax.Y));
                var dirx=BossMath.FlatLength(ax)>.05f?Vector3.Normalize(new Vector3(ax.X,0,ax.Z)):Vector3.UnitX;var diry=new Vector3(-dirx.Z,0,dirx.X);float along=rad/flat;
                const int segs=24;if(!Over.Room((segs+1)*2,segs*6))return;int first=Over.VertexCount;float seed=i*1.7f;
                for(int q=0;q<=segs;q++)
                {
                    float an=BossMath.Tau*q/segs,cs=(float)Math.Cos(an),sn=(float)Math.Sin(an);var d=dirx*(cs*along)+diry*(sn*rad);float w=.55f+.55f*(float)Math.Sin(an*3+seed+now*2.2f)*.3f+rad*.25f;
                    var inner=c+d*.92f;var outer=c+d*(1+w/Math.Max(rad,.5f));inner.Y=SurfaceAt(waves,inner.X,inner.Z)+.05f;outer.Y=SurfaceAt(waves,outer.X,outer.Z)+.03f;
                    Over.Add(inner+o,Vector3.UnitY,q/(float)segs,0,DeepEndFlow.Collar,seed,Foam,1);Over.Add(outer+o,Vector3.UnitY,q/(float)segs,1,DeepEndFlow.Collar,seed,Foam,1);
                }
                for(int q=0;q<segs;q++){int a0=first+q*2;Over.Quad(a0,a0+2,a0+3,a0+1);}
                rings++;
                // A little steady spray where it cuts the water fast.
                var vel=rig.Velocity[i];float sp=vel.Length();if(sp>3)Vfx.SmokeFlow(now,c+o,new Vector3(0,.6f,0),.7f,.32f,3.5f*Math.Min(1,sp/8),.9f,.5f,1.8f,WaterWhite,.2f,0,now,Seed,i+40,lit:.6f);
            }
        }
        private void Spray(float now,Vector3 p,Vector3 dir,float spread,float speed,int count,float life,float t0,float span,int key,float width,float floor,float cover=.8f)
        {
            if(now<t0||now>t0+span+life)return;int n=Vfx.Scale(count);
            for(int i=0;i<n;i++)
            {
                int k=key*977+i*7;float born=t0+span*H(k),age=now-born,lf=life*(.6f+.4f*H(k+1));if(age<0||age>lf)continue;
                var v0=NativeBossVfx.Cone(dir,spread,H(k+2),H(k+3),H(k+4))*(speed*(.45f+.7f*H(k+5)));var at=NativeBossVfx.Fly(p,v0,.25f,BossMath.Gravity,age,out var vel);if(at.Y<floor)continue;
                Vfx.Drop(at,-vel*.028f,WaterWhite,width*(.6f+.7f*H(k+6)),cover*(1-age/lf*.4f),age/lf,i);
            }
        }
        /// <summary>Whether (x, z) from the room's centre is under water at this tide (a few centimetres or more).</summary>
        public static bool Wet(float tide,float x,float z)=>tide>NativeDeepEndArena.Floor(x,z)+.06f;
        private static readonly Vector3 TileChip=new Vector3(.86f,.88f,.86f),Grout=new Vector3(.38f,.42f,.42f),TileDust=new Vector3(.78f,.8f,.78f),Scar=new Vector3(.01f,.012f,.014f);
        // A slam where it comes down (the owner, 2026-11-07: "add slam effects when the deep end slams onto the tiles. (Add slam into water effects [Splashing])
        // when done in water"). In water a splash: spray thrown up and out, mist, a crown of water. On dry tile a white flash, chips of tile and grout
        // thrown up that bounce, dust rolling out along the floor in a ring and a column of it, and a dark scar that fades. `burst`: out of the floor
        // (Breach) rather than onto it: the chips fly higher.
        private void Slam(float now,Vector3 o,float x,float z,float tide,float at,float size,int key,bool burst)
        {
            float age=now-at;if(age<0||age>3.4f)return;
            if(Wet(tide,x,z))
            {
                var c=o+new Vector3(x,tide,z);float floor=o.Y+tide-.1f;
                Spray(now,c,Vector3.UnitY,.75f,10.5f*size,60,1.5f,at,.14f,key,.05f,floor);Spray(now,c,Vector3.UnitY,1.25f,6*size,40,1.1f,at,.2f,key+400,.035f,floor);
                Mist(now,c+new Vector3(0,.3f,0),new Vector3(0,2.6f,0),2.8f*size,5,1.9f,.8f,3.2f*size,.17f,at,.3f,key+800,floor);
                CrownRing(c,age,1.2f,2.6f*size,.8f*size,key);return;
            }
            float fy=NativeDeepEndArena.Floor(x,z);var p=o+new Vector3(x,fy,z);float ground=o.Y+fy;
            if(age<.2f)Vfx.Glow(p+new Vector3(0,.35f,0),new Vector3(1,1,1)*(2.4f*(1-age/.2f)),1.8f*size,NativeBossVfx.Core);
            Vfx.RubbleBurst(now,p+new Vector3(0,.06f,0),Vector3.UnitY,burst?.55f:.9f,(burst?8.5f:6.2f)*size,16,1.7f,at,.075f*size,TileChip,Seed,key,0,ground);
            Vfx.RubbleBurst(now,p+new Vector3(0,.04f,0),Vector3.UnitY,1.1f,4.2f*size,10,1.4f,at,.045f*size,Grout,Seed,key+500,0,ground);
            for(int k=0;k<9;k++){float an=k*BossMath.Tau/9+H(key*13+k);var dir=new Vector3((float)Math.Cos(an),0,(float)Math.Sin(an));
                Vfx.SmokeBurst(now,p+dir*(.6f*size)+new Vector3(0,.18f,0),dir*(3.6f*size)+new Vector3(0,.3f,0),.25f,.12f,2,1.5f,.45f*size,1.7f*size,TileDust,.36f,at,.05f,Seed,key*20+k,floor:ground,lit:.6f);}
            Vfx.SmokeBurst(now,p+new Vector3(0,.35f,0),new Vector3(0,burst?2.6f:1.5f,0),.7f*size,.45f,6,2f,.6f*size,2.4f*size,TileDust,.3f,at,.12f,Seed,key*20+9,floor:ground,lit:.6f);
            float scar=1-BossMath.Smooth((age-1.4f)/2f);if(scar>.01f)Over.Billboard(p+new Vector3(0,.014f,0),new Vector3(1.4f*size,0,0),new Vector3(0,0,1.4f*size),Vector3.UnitY,DeepEndFlow.Shadow,1,Scar,.8f*scar);
        }
        // A wall of water thrown up by an impact: rises fast, hangs, collapses, tearing from its top as it goes. Along a line (both sides) or round a ring.
        private void CrownLine(Vector3 a,Vector3 b,float age,float life,float height,float width,float seed)
        {
            if(age<0||age>life)return;var d=b-a;d.Y=0;float len=d.Length();if(len<.01f)return;var dir=d/len;var side=new Vector3(dir.Z,0,-dir.X);float k=age/life,h=height*(float)Math.Pow(Math.Sin(BossMath.Pi*Math.Min(1,k*1.05f)),.6f),spread=width+1.6f*k;
            for(int f=-1;f<=1;f+=2)
            {
                const int segs=26;if(!Over.Room((segs+1)*3,segs*12))return;int first=Over.VertexCount;var n=side*f;
                for(int i=0;i<=segs;i++)
                {
                    float u=i/(float)segs;var c=a+d*u;c.Y=a.Y;float bump=.62f+.38f*(float)Math.Sin(u*19+seed+f)*(float)Math.Sin(u*7.3f+seed*2);float top=h*bump*BossMath.Smooth(u/.06f)*BossMath.Smooth((1-u)/.06f);
                    var low=c+n*spread;var mid=low+n*(top*.16f)+new Vector3(0,top*.55f,0);var hi=low+n*(top*.42f)+new Vector3(0,top,0);
                    Over.Add(low,n,u*len,0,DeepEndFlow.Crown,seed+f,WaterWhite,1-k*.4f);Over.Add(mid,n,u*len,.55f,DeepEndFlow.Crown,seed+f,WaterWhite,1-k*.4f);Over.Add(hi,n,u*len,1,DeepEndFlow.Crown,seed+f,WaterWhite,1-k*.4f);
                }
                for(int i=0;i<segs;i++){int a0=first+i*3;Over.Quad(a0,a0+3,a0+4,a0+1);Over.Quad(a0+1,a0+4,a0+5,a0+2);}
            }
        }
        private void CrownRing(Vector3 c,float age,float life,float height,float radius,float seed)
        {
            if(age<0||age>life)return;float k=age/life,h=height*(float)Math.Pow(Math.Sin(BossMath.Pi*Math.Min(1,k*1.05f)),.6f),rad=radius+2.2f*k;
            const int segs=36;if(!Over.Room((segs+1)*3,segs*12))return;int first=Over.VertexCount;
            for(int i=0;i<=segs;i++)
            {
                float u=i/(float)segs,an=BossMath.Tau*u;var n=new Vector3((float)Math.Cos(an),0,(float)Math.Sin(an));float bump=.62f+.38f*(float)Math.Sin(an*5+seed)*(float)Math.Sin(an*3+seed*2);float top=h*bump;
                var low=c+n*rad;var mid=low+n*(top*.2f)+new Vector3(0,top*.55f,0);var hi=low+n*(top*.5f)+new Vector3(0,top,0);float run=u*BossMath.Tau*rad;
                Over.Add(low,n,run,0,DeepEndFlow.Crown,seed,WaterWhite,1-k*.4f);Over.Add(mid,n,run,.55f,DeepEndFlow.Crown,seed,WaterWhite,1-k*.4f);Over.Add(hi,n,run,1,DeepEndFlow.Crown,seed,WaterWhite,1-k*.4f);
            }
            for(int i=0;i<segs;i++){int a0=first+i*3;Over.Quad(a0,a0+3,a0+4,a0+1);Over.Quad(a0+1,a0+4,a0+5,a0+2);}
        }
        private void Mist(float now,Vector3 p,Vector3 vel,float scatter,int count,float life,float s0,float s1,float a,float t0,float span,int key,float floor)=>Vfx.SmokeBurst(now,p,vel,scatter,.35f,count,life,s0,s1,WaterWhite,a,t0,span,Seed,key,lit:.65f,floor:floor);
        // The telegraphs: the water tells you first.
        // ---------------- the attack warnings on the shared renderer (Core NativeTelegraph; docs/TELEGRAPHS.md) ----------------
        /// <summary>This frame's warnings, each its rule's own geometry and time (NativeBackroomsBossRules.Contacts), in the water look;
        /// the presenter hands them to the shared renderer.</summary>
        public readonly NativeTelegraphList Telegraphs=new NativeTelegraphList(96);
        /// <summary>The warnings this frame (the old water marks are the moves' own tells and stay).</summary>
        public int Telegraph;
        /// <summary>Moves with no warning drawn: the waves and the vortex show themselves.</summary>
        public static bool Unwarned(string id)=>id=="seiche"||id=="drain";
        private void Warnings(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now)
        {
            Telegraphs.Clear();Telegraph=0;if(!v.Moving(now,out var m,out float age,out float hit))return;
            if(DeepEndBossView.At(v.cancelAt)&&v.cancelAt>=v.actionAt-1e-3f&&now>=v.cancelAt)return;
            var o=rig.Origin;float start=v.actionAt,at=v.actionAt+hit;var tp=new Vector3(v.targetX,0,v.targetZ);
            float tide=s.Tide;Vector3 On(float x,float z)=>o+new Vector3(x,Math.Max(SurfaceAt(waves,x,z),tide)+.08f,z);
            void W(NativeTelegraph.Spec sp,float from,float to,float live){sp.Style=NativeTelegraph.Water;sp.Strength=1;if(NativeTelegraph.Timed(ref sp,from,to,now,live)&&Telegraphs.Add(sp))Telegraph++;}
            float Yaw(float dx,float dz)=>(float)(Math.Atan2(dx,dz)*180/Math.PI);
            switch(m.Id)
            {
                // Breach: the rule's 3.2 m round the locked spot, its 0.25 s.
                case "breach":W(new NativeTelegraph.Spec{Shape=NativeTelegraph.Circle,Fill=NativeTelegraph.FromEdge,At=On(tp.X,tp.Z),A=m.Radius,Seed=31},start,at,m.Strike);break;
                // Belly Flop: its body's length from where it was to where it lands, 2 m either side and past each end.
                case "flop":{var e=new Vector3(v.aimX,0,v.aimZ);var d=e-tp;float len=d.Length();if(len<.1f)break;float hw=m.Width*.5f;
                    W(new NativeTelegraph.Spec{Shape=NativeTelegraph.Lane,Fill=NativeTelegraph.Along,At=On(tp.X,tp.Z),Yaw=Yaw(d.X,d.Z),A=len+hw,B=hw,C=hw,Seed=32},start,at,m.Strike);break;}
                // Thrash: 5 m round it.
                case "thrash":W(new NativeTelegraph.Spec{Shape=NativeTelegraph.Circle,Fill=NativeTelegraph.FromCentre,At=On(v.x,v.z),A=m.Radius,Seed=33},start,at,m.Strike);break;
                // The Lull: 1.8 m where the hands rise, live while they hold (1.8 s).
                case "lull":W(new NativeTelegraph.Spec{Shape=NativeTelegraph.Circle,Fill=NativeTelegraph.FromEdge,At=On(tp.X,tp.Z),A=m.Radius,Seed=34},start,at,1.8f);break;
                // The Drain and Seiche draw none (the owner, 2026-11-07: "we dont need hit indicators for the waves", "we can also remove hit indicators
                // from that one too"): the vortex and the wave are their own tells (<see cref="Unwarned"/>).
                // Hydro Jet: from the mouth along the sweep, 1 m either side, 30 m (the rule's own line, recomputed as it sweeps).
                case "jet":{var head=rig.MouthAt-o;var d=DeepEndBossRig.JetDir(v,Math.Max(age,hit),hit,m,head,s.Tide);var flat=new Vector3(d.X,0,d.Z);float l=flat.Length();if(l<1e-3f)break;
                    W(new NativeTelegraph.Spec{Shape=NativeTelegraph.Lane,Fill=NativeTelegraph.Along,At=On(head.X,head.Z),Yaw=Yaw(flat.X,flat.Z),A=m.Reach*l+1,B=m.Width*.5f,C=1,Seed=37},start,at,2.2f);break;}
                // Last Lap: the body itself, where it breaks the surface, 1.6 m either side of each joint, while it laps (6 s).
                case "lap":{float water=o.Y+s.Tide;
                    for(int j=1;j<NativeDeepEndBossLook.Spine;j++){var a=rig.Shown.Position[j-1];var q=rig.Shown.Position[j];if(Math.Max(a.Y,q.Y)+NativeDeepEndBossLook.Radius(j*NativeDeepEndBossLook.Seg)<water-.6f)continue;
                        var d=new Vector3(q.X-a.X,0,q.Z-a.Z);float len=d.Length();float hw=m.Width*.5f;
                        W(new NativeTelegraph.Spec{Shape=NativeTelegraph.Lane,Fill=NativeTelegraph.FromEdge,At=On(a.X-o.X,a.Z-o.Z),Yaw=len>1e-3f?Yaw(d.X,d.Z):0,A=len+hw,B=hw,C=hw,Seed=40+j},start,at,6);}
                    break;}
            }
        }
        private void Marks(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,bool low)
        {
            if(!v.Moving(now,out var m,out float age,out float hit))return;var o=rig.Origin;var t=new Vector3(v.targetX,0,v.targetZ);
            switch(m.Id)
            {
                case "breach":if(age<hit+.3f)
                {
                    // The water over the locked spot goes dark, bubbles rise in a ring.
                    float k=BossMath.Smooth(age/(hit*.75f))*(1-BossMath.Smooth((age-hit)/.25f));float y=SurfaceAt(waves,t.X,t.Z)+.04f;var c=o+new Vector3(t.X,y,t.Z);
                    Over.Billboard(c,new Vector3(m.Radius*1.2f,0,0),new Vector3(0,0,m.Radius*1.2f),Vector3.UnitY,DeepEndFlow.Shadow,1,new Vector3(.005f,.03f,.05f),.72f*k);
                    int n=Vfx.Scale(26);for(int i=0;i<n;i++)
                    {
                        float a0=H(i*5+900)*BossMath.Tau,r=m.Radius*(.86f+.14f*H(i*5+901)),ph=BossMath.Frac(now*(.8f+.6f*H(i*5+902))+H(i*5+903));var at=o+new Vector3(t.X+(float)Math.Cos(a0)*r,y+.02f+ph*.22f,t.Z+(float)Math.Sin(a0)*r);
                        Vfx.Glow(at,WaterWhite*(.5f*k*(1-ph)),.2f+.16f*H(i*5+904),NativeBossVfx.RingGlow);
                    }
                    if(age>hit*.5f)Vfx.SmokeFlow(now,c,new Vector3(0,.5f,0),m.Radius*.5f,.2f,6,.8f,.4f,1.4f,WaterWhite,.16f*k,v.actionAt,v.actionAt+hit,Seed,31,lit:.5f);
                }break;
                case "flop":if(age<hit)
                {
                    // A long shadow on the water, its own length, darkening.
                    var e=new Vector3(v.aimX,0,v.aimZ);var mid=(t+e)*.5f;var d=e-t;float len=d.Length();var dir=len>.1f?d/len:Vector3.UnitZ;var side=new Vector3(dir.Z,0,-dir.X);float k=BossMath.Smoother(age/hit);
                    float y=Math.Max(SurfaceAt(waves,mid.X,mid.Z),.02f)+.05f;var c=o+new Vector3(mid.X,y,mid.Z);
                    Over.Billboard(c,dir*(len*.54f),side*(m.Width*(.62f+.2f*k)),Vector3.UnitY,DeepEndFlow.Shadow,0,new Vector3(.004f,.02f,.03f),.82f*k);
                }break;
                case "jet":if(age<hit)
                {
                    // The mouth irises, the throat brightens, a thin line of mist tracks the sweep's start.
                    // (On the water, or just over the dry deck, which stands above the pool's water: the line used to lie at the water's level there, under the tile.)
                    var head=rig.MouthAt-o;var dir0=DeepEndBossRig.JetDir(v,hit,hit,m,head,s.Tide);float reach0=JetReach(head,dir0,m,out var flat,out _);var land=head+flat*reach0;
                    float rise=BossMath.Smooth(age/hit);var farAt=Vector3.Lerp(new Vector3(head.X,0,head.Z),new Vector3(land.X,0,land.Z),rise);
                    float lineY=(DeepEndBossPaths.Wet(head.X,head.Z,s.Tide)&&DeepEndBossPaths.Wet(farAt.X,farAt.Z,s.Tide)?s.Tide:Math.Max(s.Tide,0))+.06f;
                    var near=new Vector3(head.X,lineY,head.Z);var far=new Vector3(farAt.X,lineY,farAt.Z);var midp=(near+far)*.5f;var span=(far-near)*.5f;var nrm=new Vector3(flat.Z,0,-flat.X);
                    Over.Billboard(o+midp,span,nrm*.22f,Vector3.UnitY,DeepEndFlow.Mark,0,new Vector3(.7f,.95f,1f)*(1.1f*rise),0);
                    Vfx.SmokeFlow(now,o+far+new Vector3(0,.15f,0),new Vector3(0,.5f,0),.4f,.2f,14,.7f,.25f,.9f,WaterWhite,.3f*rise,v.actionAt,v.actionAt+hit,Seed,33,lit:.6f);
                }break;
                case "lull":
                {
                    // The ring where the hands will rise (the warning already softens the view).
                    float k=BossMath.Smooth(age/hit)*(1-BossMath.Smooth((age-hit-m.Strike-m.Follow)/.4f));float y=Math.Max(SurfaceAt(waves,t.X,t.Z),.02f)+.04f;
                    Over.Billboard(o+new Vector3(t.X,y,t.Z),new Vector3(m.Radius*1.5f,0,0),new Vector3(0,0,m.Radius*1.5f),Vector3.UnitY,DeepEndFlow.Shadow,2,new Vector3(.02f,.12f,.14f),.7f*k);
                }break;
            }
        }
        // The moves' own effects.
        private void Moves(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,bool low,bool reduced)
        {
            var o=rig.Origin;
            if(v.Moving(now,out var m,out float age,out float hit))
            {
                float at=v.actionAt+hit;var t=new Vector3(v.targetX,0,v.targetZ);float surface=s.Tide;float floor=o.Y+surface-.1f;
                switch(m.Id)
                {
                    // Breach: out of the water a burst of spray; out of dry tile it bursts up through it.
                    case "breach":if(Wet(surface,t.X,t.Z)){var c=o+new Vector3(t.X,surface,t.Z);Spray(now,c,Vector3.UnitY,.65f,9,150,1.5f,at,.12f,1,.05f,floor);Spray(now,c,Vector3.UnitY,1.2f,5,110,1.1f,at,.25f,2,.03f,floor);Mist(now,c+new Vector3(0,.3f,0),new Vector3(0,2.8f,0),2.4f,12,1.9f,.8f,3.2f,.2f,at,.3f,3,floor);CrownRing(c,now-at,1.15f,3.4f,m.Radius*.62f,1);}
                        else Slam(now,o,t.X,t.Z,surface,at,1.25f,80,true);break;
                    // Belly Flop: its body comes down along its line, head first: each place a splash in water, a slam on tile; the water's wall where it's wet.
                    case "flop":if(age>=hit){var e=new Vector3(v.aimX,0,v.aimZ);int wet=0;for(int k=0;k<=6;k++){var q=Vector3.Lerp(e,t,k/6f);if(Wet(surface,q.X,q.Z))wet++;Slam(now,o,q.X,q.Z,surface,at+.035f*k,1.3f,10+k,false);}
                        if(wet>=4){var w0=o+new Vector3(t.X,Math.Max(surface,0),t.Z);var w1=o+new Vector3(e.X,Math.Max(surface,0),e.Z);CrownLine(w0,w1,now-at,1.3f,3.6f,m.Width*.42f,3);}}break;
                    // Thrash: the body hits round it, four blows a beat apart.
                    case "thrash":if(age>=hit)for(int k=0;k<4;k++){float an=k*1.7f+v.actionAt*.37f,rr=2.6f+.9f*H(k*7+(int)(v.actionAt*10));Slam(now,o,v.x+(float)Math.Cos(an)*rr,v.z+(float)Math.Sin(an)*rr,surface,at+.07f*k,.8f,90+k,false);}break;
                    case "jet":Jet(rig,v,s,waves,now,age,hit,m,low);break;
                    case "lull":Hands(rig,v,s,waves,now,age,hit,m,low);break;
                    case "drain":if(age<hit+m.Strike&&!(DeepEndBossView.At(v.cancelAt)&&now>=v.cancelAt)){float k=BossMath.Smooth((age-hit*.6f)/.6f)*(1-BossMath.Smooth((age-hit-m.Strike+.3f)/.3f));if(k>.02f){DrainPull(rig,s,waves,now,k,low);var hd=rig.MouthAt;Vfx.SmokeFlow(now,new Vector3(hd.X,o.Y+surface+.4f,hd.Z),new Vector3(0,.2f,0),1.4f,.22f,14,1.5f,.5f,2.4f,WaterWhite,.2f*k,v.actionAt,v.actionAt+hit+m.Strike,Seed,41,lit:.6f);}}break;
                    case "whistle":if(age>=hit*.5f&&age<hit+m.Strike){var pl=Pilots;for(int k=0;k<PilotCount;k++){float e=age-hit*.5f,y=Math.Max(SurfaceAt(waves,pl[k].X,pl[k].Y),.02f)+.05f;Over.Billboard(o+new Vector3(pl[k].X+1.2f,y,pl[k].Y+.8f),new Vector3(1.4f,0,0),new Vector3(0,0,1.4f),Vector3.UnitY,DeepEndFlow.Shadow,2,new Vector3(.01f,.1f,.12f),.6f*BossMath.Smooth(e/.4f)*(1-BossMath.Smooth((age-hit-m.Strike+.5f)/.5f)));}}break;
                    case "lap":if(age>=hit&&age<hit+m.Strike+1){var c=rig.Shown.Position[1];Vfx.SmokeFlow(now,new Vector3(c.X,o.Y+surface+.4f,c.Z),new Vector3(0,1,0),1.2f,.3f,12,1.1f,.6f,2.2f,WaterWhite,.22f,at,at+m.Strike+1,Seed,43,lit:.6f,trail:rig.Velocity[1]*.8f);}break;
                }
            }
            // Broken lamps leak a cloud of dark water; a glassy puff when they go.
            if(v.parts!=0)for(int i=0;i<NativeDeepEndBossLook.Lamps;i++)if(v.LampBroken(i,now))
            {
                rig.LampFrame(i,out var at,out _,out _);float t0=v.partAt!=null&&i<v.partAt.Length?v.partAt[i]:now-5;float e=now-t0;
                if(e<.6f)Vfx.SparkBurst(now,at,BossMath.Rotate(rig.Shown.Rotation[0],new Vector3(0,1,0)),1.1f,5,10,.5f,t0,.04f,Seed,60+i,.8f,.02f,1,false,o.Y+s.Tide);
                Vfx.SmokeFlow(now,at,new Vector3(0,-.15f,0),.25f,.02f,5,2.8f,.4f,1.8f,new Vector3(.03f,.08f,.1f),.5f,t0,now+1,Seed,70+i,floor:NativeBossVfx.NoFloor);
            }
        }
        // Hydro Jet, drawn on the rule's own line (the owner, 2026-11-07: it hit without looking as if it did; then: "make the water more realistic, it currently has a unrealistic curve to it ...
        // shooting straight from their mouth and splashing"). The rule sweeps JetDir, flat, 2 m wide and the table's reach long. The jet leaves the mouth dead straight along JetDir, its head reaching
        // where it lands in a tenth of a second, drops peeling off it as it goes; where it lands a crown of water is thrown up again and again with spray and mist; beyond, the water skips on along the
        // line as spray (no sheet) to its end or the wall. Whoever stands in the line is struck, and it shows on them.
        /// <summary>How far (m, on the ground) the jet from `head` along `d` runs before the room's walls stop it (its full reach otherwise), its flat direction, and whether a wall stops it.</summary>
        public static float JetReach(Vector3 head,Vector3 d,in DeepEndBossMove m,out Vector3 flat,out bool wall)
        {
            flat=new Vector3(d.X,0,d.Z);float fl=flat.Length();flat=fl>1e-3f?flat/fl:Vector3.UnitZ;float full=m.Reach*fl,reach=full;
            float halfRoom=NativeDeepEndArena.Half-.25f;if(Math.Abs(flat.X)>1e-3f)reach=Math.Min(reach,((flat.X>0?halfRoom:-halfRoom)-head.X)/flat.X);if(Math.Abs(flat.Z)>1e-3f)reach=Math.Min(reach,((flat.Z>0?halfRoom:-halfRoom)-head.Z)/flat.Z);
            reach=Math.Max(1,reach);wall=reach<full-.5f;return reach;
        }
        /// <summary>A point `u` of the way along the jet (0 the mouth, 1 its end, `reach` m on the ground): down its straight line from the mouth; where no wall stops it, its last
        /// stretch comes down to the floor (`endY`) at its end.</summary>
        public static Vector3 JetAt(Vector3 head,Vector3 d,float reach,bool wall,float endY,float u)
        {
            float fl=Math.Max(1e-3f,new Vector3(d.X,0,d.Z).Length()),length=reach/fl;var p=head+d*(length*u);
            if(!wall){float lineEnd=head.Y+d.Y*length;p.Y+=(endY-lineEnd)*(float)Math.Pow(BossMath.Clamp01(u),5);}
            return p;
        }
        private void Jet(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,float age,float hit,in DeepEndBossMove m,bool low)
        {
            float on=BossMath.Smooth((age-hit)/.06f)*(1-BossMath.Smooth((age-hit-m.Strike)/.18f));if(on<.01f)return;var o=rig.Origin;var mouth=rig.MouthAt;var head=mouth-o;float surface=s.Tide;
            var d=DeepEndBossRig.JetDir(v,age,hit,m,head,surface);float reach=JetReach(head,d,m,out var flat,out bool wall);
            // The floor under it: the water, or the tile where the deck is dry.
            float Ground(float x,float z)=>DeepEndBossPaths.Wet(x,z,surface)?Math.Max(SurfaceAt(waves,x,z),surface):0;
            var endFlat=head+flat*reach;float endY=Ground(endFlat.X,endFlat.Z);
            // One stream over the whole lane the rule hits, above the floor and its warning (the owner, 2026-11-08: "their spray attack seems to be flat. It usually ends up being
            // UNDER the hit indicator"): straight from the mouth along its line, coming down to the floor only in its last stretch, or striking the wall that stops it. Its head
            // reaches the end in a tenth of a second (the rule hits from the first moment; the eye needs to see it go); a narrow jet that spreads as it flies, its surface shivering,
            // a bright core down its middle.
            float grow=BossMath.Smooth((age-hit)/.1f);
            const int rings=22;
            for(int i=0;i<rings;i++){float u=i/(float)(rings-1);tubeC[i]=o+JetAt(head,d,reach,wall,endY,u*grow);tubeR[i]=(.15f+.3f*u*grow)*(1+.09f*(float)Math.Sin(now*41+i*1.9f))*on;}
            Over.Tube(tubeC,tubeR,rings,low?8:14,DeepEndFlow.Jet,0,new Vector3(.86f,.97f,1f),on,rings*.6f);
            for(int i=0;i<rings;i++)tubeR[i]*=.45f;Over.Tube(tubeC,tubeR,rings,low?6:10,DeepEndFlow.Jet,1,new Vector3(1.8f,2.2f,2.4f),on,rings*.9f);
            // Drops peeling off the jet along its length, flung on and out, raining onto the lane.
            int peel=Vfx.Scale(60);var spreadDir=Vector3.Cross(d,Vector3.UnitY);if(spreadDir.LengthSquared()<1e-4f)spreadDir=Vector3.UnitX;else spreadDir=Vector3.Normalize(spreadDir);
            for(int i=0;i<peel;i++)
            {
                int k=2600+i*7;float ph=BossMath.Frac(now*(2.2f+H(k))+H(k+1)),at0=H(k+2)*grow;var p0=o+JetAt(head,d,reach,wall,endY,at0);float sideways=(H(k+3)-.5f)*3.2f;
                var vel0=d*(9+8*H(k+4))+spreadDir*sideways+new Vector3(0,.4f+1.4f*H(k+5),0);var at=NativeBossVfx.Fly(p0,vel0,.3f,BossMath.Gravity,ph*.45f,out var vel);if(at.Y<o.Y+Ground(at.X-o.X,at.Z-o.Z)-.05f)continue;
                Vfx.Drop(at,-vel*.03f,WaterWhite,.04f+.05f*H(k+6),.8f*on*(1-ph*.7f),ph,i);
            }
            var side=new Vector3(flat.Z,0,-flat.X);
            if(grow<.98f)goto Struck;
            // The hard splash where it comes down (at a wall's foot, the water running down it): a crown of water thrown up and out again and again, spray and mist.
            float start=v.actionAt+hit,end=start+m.Strike;var lc=o+new Vector3(endFlat.X,endY,endFlat.Z);int spr=Vfx.Scale(70);
            const float crownEvery=.42f;for(int c=0;c<(low?2:3);c++){float ca=(age-hit-.1f+c*crownEvery/3)%crownEvery;if(age-hit-.1f+c*crownEvery/3<0)continue;CrownRing(lc,ca,crownEvery,(1.05f+.25f*H(c+91))*on,.3f,53+c);}
            for(int i=0;i<spr;i++)
            {
                int k=1800+i*7;float ph=BossMath.Frac(now*(1.1f+.7f*H(k))+H(k+1)),age2=ph*1.05f;var dir=NativeBossVfx.Cone(new Vector3(flat.X*.8f,1,flat.Z*.8f),.7f,H(k+2),H(k+3),H(k+4));var p0=lc+new Vector3(H(k+5)-.5f,0,H(k+6)-.5f)*1.2f;var at=NativeBossVfx.Fly(p0,dir*(5+9*H(k+1)),.3f,BossMath.Gravity,age2,out var vel);if(at.Y<lc.Y)continue;Vfx.Drop(at,-vel*.03f,WaterWhite,.07f+.07f*H(k+7),.85f*on*(1-ph*.5f),ph,i);
            }
            Vfx.SmokeFlow(now,lc+new Vector3(0,.4f,0),new Vector3(0,2,0),1.2f,.3f,14,1f,.5f,2.2f,WaterWhite,.16f*on,start,end,Seed,81,lit:.7f);
            // The line's end: against a wall it climbs it from where it strikes.
            if(wall){var ec=o+JetAt(head,d,reach,true,endY,1);int up=Vfx.Scale(26);for(int i=0;i<up;i++){int k=2400+i*5;float ph=BossMath.Frac(now*(1.3f+.6f*H(k))+H(k+1));var at=NativeBossVfx.Fly(ec+side*((H(k+2)-.5f)*2),new Vector3(0,7+4*H(k+3),0)-flat*(2*H(k+4)),.3f,BossMath.Gravity,ph*.8f,out var vel);if(at.Y<ec.Y)continue;Vfx.Drop(at,-vel*.03f,WaterWhite,.08f+.06f*H(k+2),.8f*on*(1-ph*.5f),ph,i);}}
            Struck:
            // Whoever stands in the line: the water bursting on them, so a hit looks like one.
            for(int n=0;n<PilotCount;n++)
            {
                var q=new Vector3(Pilots[n].X,0,Pilots[n].Y)-new Vector3(head.X,0,head.Z);float along=Vector3.Dot(q,flat);if(along<0||along>reach)continue;float across=Math.Abs(Vector3.Dot(q,side));if(across>m.Width*.5f+.15f)continue;
                var bp=o+new Vector3(Pilots[n].X,Ground(Pilots[n].X,Pilots[n].Y),Pilots[n].Y);int burst=Vfx.Scale(40);
                for(int i=0;i<burst;i++){int k=2600+n*211+i*3;float ph=BossMath.Frac(now*(1.9f+.8f*H(k))+H(k+1));var dir=NativeBossVfx.Cone(Vector3.Normalize(new Vector3(flat.X*.9f,1,flat.Z*.9f)),.85f,H(k+2),H(k+3),H(k+4));var at=NativeBossVfx.Fly(bp+new Vector3(0,.5f+.9f*H(k+5),0),dir*(4+8*H(k+1)),.2f,BossMath.Gravity,ph*.5f,out var vel);Vfx.Drop(at,-vel*.03f,WaterWhite,.07f+.08f*H(k+6),.95f*on*(1-ph*.6f),ph,i);}
                Over.Billboard(bp+new Vector3(0,.05f,0),flat*1.5f,side*1.5f,Vector3.UnitY,DeepEndFlow.Shadow,2,new Vector3(.75f,.92f,.96f),.55f*on);
                Vfx.Glow(bp+new Vector3(0,1,0),new Vector3(.4f,.7f,.85f)*(.8f*on),2.2f,NativeBossVfx.Core);
            }
            // The throat's light, a glow at the mouth.
            Vfx.Glow(mouth+rig.MouthDir*.5f,new Vector3(.5f,.9f,1.2f)*(1.6f*on),2.6f,NativeBossVfx.Core);
        }
        // The Drain: the whole room is going down the pit, so its pull has to show wherever you stand: spray dragged in along the surface and the dry deck, spiralling
        // tighter as it nears the pit (the water's own whirl turns the surface); the throat bright with what it's taking in. No streaks on the floor (the owner,
        // 2026-11-07: "the lines don't need to be there during that ability").
        private void DrainPull(DeepEndBossRig rig,in DeepEndBossStage s,DeepEndWaves waves,float now,float k,bool low)
        {
            var o=rig.Origin;float surface=s.Tide;
            int sprays=Vfx.Scale(30);for(int i=0;i<sprays;i++)
            {
                int q=3300+i*4;float cyc=BossMath.Frac(now*(.5f+.3f*H(q))+H(q+1));float r0=5+21*H(q+2),rad=Math.Max(.8f,r0*(1-cyc));float an=H(q+3)*BossMath.Tau+1.7f*cyc;float x=(float)Math.Cos(an)*rad,z=(float)Math.Sin(an)*rad;if(!NativeDeepEndArena.InRoom(x,z))continue;
                float y=DeepEndBossPaths.Wet(x,z,surface)?surface:0;Vfx.Drop(o+new Vector3(x,y+.25f+.5f*H(q),z),new Vector3(-(float)Math.Cos(an),.05f,-(float)Math.Sin(an))*.28f,WaterWhite,.05f+.05f*H(q+1),.7f*k*(1-cyc*.4f),cyc,i);
            }
            Vfx.Glow(rig.MouthAt+rig.MouthDir*.3f,new Vector3(.45f,.85f,1.1f)*(1.5f*k*(.8f+.2f*(float)Math.Sin(now*6.3f))),3.1f,NativeBossVfx.Core);
        }
        // The Lull's hands: formed from the surface, rising round the pilot, pulling down.
        private void Hands(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,float age,float hit,in DeepEndBossMove m,bool low)
        {
            float rise=BossMath.Smooth((age-hit*.25f)/(hit*.75f)),fall=BossMath.Smooth((age-hit-m.Strike-m.Follow*.55f)/(m.Follow*.45f));float k=rise*(1-fall);if(k<.01f)return;var o=rig.Origin;float tx=v.targetX,tz=v.targetZ;float y0=Math.Max(SurfaceAt(waves,tx,tz),0);
            int hands=low?3:5;float grip=BossMath.Smooth((age-hit)/.22f);var tint=new Vector3(.52f,.86f,.86f);
            for(int h=0;h<hands;h++)
            {
                float an=BossMath.Tau*h/hands+.5f+H(h+2000)*.4f;var outward=new Vector3((float)Math.Cos(an),0,(float)Math.Sin(an));float ring=1.15f+.25f*H(h+2010)-.55f*grip;var root=new Vector3(tx,y0,tz)+outward*(ring+.5f);float height=(1.35f+.4f*H(h+2020))*k-fall*.5f;
                // The arm: thick from the surface, thinning; bending in over the pilot.
                const int rings=7;float sway=(float)Math.Sin(now*1.7f+h*2)*.06f;Vector3 wrist=default;Vector3 armDir=default;
                for(int r=0;r<rings;r++)
                {
                    float u=r/(float)(rings-1);var p=root+new Vector3(0,height*u,0)-outward*(.55f*u*u*(.6f+grip))+new Vector3(sway*u,0,-sway*u);tubeC[r]=p+o;tubeR[r]=(.34f*(1-u)*(1-u)+.11f)*(.5f+.5f*k)*(1+.12f*(float)Math.Sin(u*9+now*3+h));if(r==rings-1){wrist=p;armDir=Vector3.Normalize(p-tubeC[r-1]+o);}
                }
                Over.Tube(tubeC,tubeR,rings,low?6:9,DeepEndFlow.Hand,h,tint,k,1);
                // Four fingers, curling in as it grips.
                var side=Vector3.Normalize(Vector3.Cross(outward,Vector3.UnitY));
                for(int f=0;f<4;f++)
                {
                    float sp=(f-1.5f)*.1f;Vector3 c=wrist+side*sp;var dir=Vector3.Normalize(armDir+side*sp*1.4f);const int nf=4;float curl=.5f+.7f*grip,len=.2f;
                    for(int q=0;q<nf;q++){tubeC[q]=c+o;tubeR[q]=(.06f-.009f*q);c+=dir*len;dir=Vector3.Normalize(dir-outward*curl*.9f-new Vector3(0,curl*.55f,0));}
                    Over.Tube(tubeC,tubeR,nf,low?4:5,DeepEndFlow.Hand,h+f*.1f,tint,k,1);
                }
                // Where it leaves the surface.
                float ry=y0+.05f;Over.Billboard(o+new Vector3(root.X,ry,root.Z),new Vector3(.95f,0,0),new Vector3(0,0,.95f),Vector3.UnitY,DeepEndFlow.Shadow,3,Foam,.55f*k);
            }
            // On dry deck, a puddle forms under them.
            if(s.Tide<0&&!NativeDeepEndArena.InPool(tx,tz)){Over.Billboard(o+new Vector3(tx,.015f,tz),new Vector3(2.8f,0,0),new Vector3(0,0,2.8f),Vector3.UnitY,DeepEndFlow.Shadow,4,new Vector3(.2f,.52f,.52f),.8f*k);}
            var c0=o+new Vector3(tx,y0+.1f,tz);Spray(now,c0,Vector3.UnitY,1.2f,3.4f,18,.8f,v.actionAt+hit,.2f,90,.04f,o.Y+y0-.1f);
        }
        // The arrival's beats that are effects (the shots and the room's light are the shots' and the stage's).
        private void Arrival(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,bool low)
        {
            if(float.IsNaN(s.IntroAge)||!DeepEndBossView.At(v.spawned))return;var b=NativeDeepEndBossBeats.Current;float a=s.IntroAge;var o=rig.Origin;float at=v.spawned+b.Break;float surface=s.Tide;float floor=o.Y+surface-.1f;var fw=BossMath.Forward(v.yaw*BossMath.Deg);var c=o+new Vector3(0,surface,0)-fw*NativeDeepEndBoss.BreakBack;
            if(a>=b.Break&&a<b.Break+3.6f)
            {
                Spray(now,c,new Vector3(.1f,1,.2f),.5f,8,190,2.4f,at,.3f,100,.05f,floor);Spray(now,c,new Vector3(.1f,1,.2f),.95f,5,190,1.7f,at,.5f,101,.03f,floor);
                Mist(now,c+new Vector3(0,.3f,0),new Vector3(0,1.8f,0),2.6f,22,3.0f,1.1f,5,.22f,at,.5f,102,floor);CrownRing(c,now-at,2.2f,3.2f,1.7f,5);
            }
            // The head's fall into the water after the hold: the crash.
            float crash=v.spawned+b.Break+2*(5.6f/DeepEndBossPaths.IntroG);if(now>=crash&&now<crash+3)
            {
                var head=c+fw*(2.9f*(crash-v.spawned-b.Break));Spray(now,head,new Vector3(0,1,.25f),.6f,12,180,2,crash,.2f,110,.06f,floor);Mist(now,head+new Vector3(0,.4f,0),new Vector3(0,3,0),3,14,2.4f,1f,4.4f,.24f,crash,.35f,111,floor);CrownRing(head,now-crash,1.5f,4.2f,2f,7);
            }
            // The wave's crest throws a line of spray.
            if(s.RingH>.2f&&a<b.Hold+1.4f)
            {
                int n=Vfx.Scale(50);for(int i=0;i<n;i++){float an=-1.1f+2.2f*H(i+2100)+1.57f;float ph=BossMath.Frac(now*1.4f+H(i+2110));var p=o+new Vector3((float)Math.Cos(an)*(s.RingR-1+ph*2),surface+s.RingH*(.8f+ph*.7f)-ph*ph*.8f,(float)Math.Sin(an)*(s.RingR-1+ph*2));Vfx.Drop(p,new Vector3(0,-.12f-.2f*ph,-.1f),WaterWhite,.05f+.05f*H(i+2120),.6f*(1-ph)*BossMath.Clamp01(s.RingH),ph,i);}
            }
            // Before the break: rising bubbles over the pit as it comes.
            if(a>=b.DownShot-.2f&&a<b.Break)
            {
                float k=BossMath.Smooth((a-b.DownShot)/1f);int n=Vfx.Scale(30);
                for(int i=0;i<n;i++){float an=H(i+2200)*BossMath.Tau,r=1+3*H(i+2210),sp=2+2.6f*H(i+2220),life=3.4f,ph=BossMath.Frac((now/life)*sp*.3f+H(i+2230));float y=surface-(1-ph)*7.5f;var p=o+new Vector3((float)Math.Cos(an)*r,y,(float)Math.Sin(an)*r);var g=new Vector3(.2f,.45f,.45f)*(.24f*k*ph);Under.Billboard(p,new Vector3(.05f+.06f*H(i+2240),0,0),new Vector3(0,0,.05f+.06f*H(i+2240)),Vector3.UnitY,DeepEndFlow.Halo,0,g,0);}
            }
        }
        // White tile drifting; in the whirlpool, tile and a ring buoy going round and down.
        private void Debris(DeepEndBossRig rig,in DeepEndBossView v,in DeepEndBossStage s,DeepEndWaves waves,float now,bool low)
        {
            var o=rig.Origin;float intro=!float.IsNaN(s.IntroAge)?BossMath.Smooth((s.IntroAge-.5f)/1f)*(1-BossMath.Smooth((s.IntroAge-6)/1f)):0;float whirl=s.Whirl;float show=Math.Max(intro,whirl);if(show<.01f)return;
            int n=(whirl>.01f?(low?14:34):(low?6:12));var white=new Vector3(.62f,.68f,.66f);
            for(int i=0;i<n;i++)
            {
                float a0=H(i*3+2300)*BossMath.Tau,r0=2.8f+6.4f*H(i*3+2301),x,z;float tilt=.2f+.6f*H(i*3+2302);
                if(whirl>.01f)
                {
                    // In on a log spiral, over and over.
                    float cyc=BossMath.Frac((now*.09f)*(1+.6f*H(i*3+2303))+H(i*3+2304));float rad=Math.Max(.6f,r0*(1-cyc*cyc)+.6f);float an=a0+s.WhirlTurn*2.4f/(.5f+rad*.28f);float mx=(float)Math.Cos(an)*rad,mz=(float)Math.Sin(an)*rad;
                    // In the arrival's drift they sit near the steps; blending never happens since one or the other shows.
                    x=mx;z=mz;
                }
                else{x=(float)Math.Cos(a0)*r0*.75f+(float)Math.Sin(now*.14f+i)*.35f;z=-6.2f+(float)Math.Sin(a0)*3.2f+(float)Math.Cos(now*.11f+i*.7f)*.35f;}
                if(!NativeDeepEndArena.InPool(x,z)&&s.Tide<0)continue;float y=SurfaceAt(waves,x,z)-.01f+.025f*(float)Math.Sin(now*1.1f+i);float sz=.075f;var q=new Quaternion(0,(float)Math.Sin(a0+now*.2f),0,(float)Math.Cos(a0+now*.2f));
                var ux=BossMath.Rotate(q,new Vector3(sz,sz*tilt*.25f,0));var uz=BossMath.Rotate(q,new Vector3(0,sz*tilt*.25f,sz));var nrm=Vector3.Normalize(Vector3.Cross(uz,ux));var c=o+new Vector3(x,y,z);Over.Billboard(c,ux,uz,nrm,DeepEndFlow.Solid,1,white,show);
            }
            if(whirl>.05f)
            {
                float cyc=BossMath.Frac(now*.06f);float rad=Math.Max(1.4f,9*(1-cyc*cyc)+1);float an=1.1f+s.WhirlTurn*2.4f/(.5f+rad*.28f);float x=(float)Math.Cos(an)*rad,z=(float)Math.Sin(an)*rad;FloatOn=true;FloatAt=o+new Vector3(x,SurfaceAt(waves,x,z)+.02f,z);FloatTurn=BossMath.AxisAngle(Vector3.UnitY,an*1.3f)*BossMath.AxisAngle(Vector3.UnitX,.12f*(float)Math.Sin(now*.9f));
            }
        }
    }
}
