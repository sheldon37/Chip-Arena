using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's hardlight boxing string (owner 2026-10-15: "better basic attacks, (much better combo animation on
    /// melee)"), authored pose to pose on the replicated swing: each strike is a few key poses of the hips, spine, chest, head
    /// and both fists in the body's own frame (metres and radians; +x the pilot's left, +z ahead, as the rig's group), placed
    /// on the swing's windup (its start to the replicated hit, the kit's Release) and follow-through (the hit to its end), and
    /// sampled with C1 Hermite curves. The striking fist accelerates into full extension exactly on the replicated hit, holds
    /// for a beat, snaps back, and the follow-through settles into a ready pose the next step starts from. Orthodox, left foot
    /// and fist leading: the jab (left) steps in, the cross (right) drives the hips round on a pivoting rear foot, the spinning
    /// backfist (left) carries the cross's turn on round in a hopped spin, the rising uppercut (right) drops and drives up
    /// onto the toes, the Pulse Palm (both) chambers and stomps forward. The fists are drawn through the rig's own arm IK
    /// (MeleeCapture rows), the feet by CyberComboFeet. The same keys give the effects their fist paths (the hardlight trails).
    /// Pure: no allocation, no state; nothing reaches the authority, hit timing, hitboxes, damage or movement.
    /// </summary>
    public static class CyberCombo
    {
        public const int Jab=0,Cross=1,Spin=2,Rise=3,Palm=4,Kinds=5;
        public static readonly string[] States={"cyber-jab","cyber-cross","cyber-spin","cyber-rise","cyber-palm"};
        /// <summary>The striking hand per kind: 0 the left (lead), 1 the right (rear), 2 both.</summary>
        public static readonly int[] Hand={0,1,0,1,2};
        /// <summary>v1's three steps (Jab, Cross, Pulse Palm) and v2's five (CyberKit2Spec.ComboKinds order).</summary>
        public static int V1Kind(int step)=>step==1?Cross:step==2?Palm:Jab;
        public static int V2Kind(int step)=>step<0?Jab:step>4?Palm:step;
        /// <summary>The kind before `kind` in a chained string (v1 skips the spin and the rise).</summary>
        public static int Previous(int kind,bool v2)=>kind==Palm&&!v2?Cross:kind-1;
        /// <summary>A chained step blends from where the last one left the body over this share of its windup, and a
        /// finished string eases back to the stance over Settle (s).</summary>
        public const float ChainShare=.55f,Settle=.22f;

        // ---- the pose channels (body space) ----
        public const int HY=0,HP=1,HR=2,SY=3,SP=4,SR=5,CY=6,CP=7,CR=8,NY=9,NP=10,NR=11,Drop=12,ShX=13,ShZ=14,
            L=15,R=18,LK=21,RK=24,LP=27,RP=30,LO=33,RO=36,LC=39,RC=40,Follow=41,LT=42,RT=43,LKn=44,RKn=45,Air=46,N=47;
        /// <summary>A key pose: its place on the swing (Phase 0 the windup, 1 the follow-through; U along it), how the curve
        /// meets it (Flow: smooth through; Stop: eases to rest; Impact: arrives at speed and stops dead), and its channels.</summary>
        public struct Key{public byte Phase,How;public float U;public float[] V;}
        public const byte Flow=0,Stop=1,Impact=2;
        public sealed class B
        {
            public readonly float[] V=new float[N];
            public B(){}
            public B(B o){Array.Copy(o.V,V,N);}
            public B Hips(float yaw,float pitch,float roll){V[HY]=yaw;V[HP]=pitch;V[HR]=roll;return this;}
            public B Spine(float yaw,float pitch,float roll){V[SY]=yaw;V[SP]=pitch;V[SR]=roll;return this;}
            public B Chest(float yaw,float pitch,float roll){V[CY]=yaw;V[CP]=pitch;V[CR]=roll;return this;}
            public B Head(float yaw,float pitch,float roll=0){V[NY]=yaw;V[NP]=pitch;V[NR]=roll;return this;}
            public B Body(float drop,float x,float z){V[Drop]=drop;V[ShX]=x;V[ShZ]=z;return this;}
            public B Left(float x,float y,float z){V[L]=x;V[L+1]=y;V[L+2]=z;return this;}
            public B Right(float x,float y,float z){V[R]=x;V[R+1]=y;V[R+2]=z;return this;}
            public B LFist(float kx,float ky,float kz,float px,float py,float pz){V[LK]=kx;V[LK+1]=ky;V[LK+2]=kz;V[LP]=px;V[LP+1]=py;V[LP+2]=pz;return this;}
            public B RFist(float kx,float ky,float kz,float px,float py,float pz){V[RK]=kx;V[RK+1]=ky;V[RK+2]=kz;V[RP]=px;V[RP+1]=py;V[RP+2]=pz;return this;}
            public B Poles(float lx,float ly,float lz,float rx,float ry,float rz){V[LO]=lx;V[LO+1]=ly;V[LO+2]=lz;V[RO]=rx;V[RO+1]=ry;V[RO+2]=rz;return this;}
            public B Reach(float left,float right){V[LC]=left;V[RC]=right;return this;}
            public B Turn(float follow){V[Follow]=follow;return this;}
            /// <summary>The legs for an airborne pose (Move.Legs): each thigh's swing (+ forward) and knee bend, the lead (left) first.</summary>
            public B Legs(float leadThigh,float rearThigh,float leadKnee,float rearKnee){V[LT]=leadThigh;V[RT]=rearThigh;V[LKn]=leadKnee;V[RKn]=rearKnee;V[Air]=1;return this;}
            /// <summary>Back on planted feet (the legs the gait's and CyberComboFeet's again).</summary>
            public B Ground(){V[LT]=V[RT]=V[LKn]=V[RKn]=0;V[Air]=0;return this;}
            public B Lean(float hips,float spine,float chest){V[HP]=hips;V[SP]=spine;V[CP]=chest;return this;}
            public Key At(byte phase,float u,byte how=Flow)=>new Key{Phase=phase,U=u,How=how,V=(float[])V.Clone()};
        }
        /// <summary>The guard every string starts from and settles into: bladed (the hips and chest turned .48 rad off the
        /// facing to the right), the lead fist out in front of the face, the rear fist at the chin, elbows in, the head kept on
        /// the facing.</summary>
        public static B Guard()=>new B().Hips(-.30f,.06f,0).Spine(-.08f,.16f,0).Chest(-.10f,.14f,0).Head(.42f,-.30f).Body(0,0,0)
            .Left(.14f,.30f,.40f).LFist(-.05f,.45f,.9f,-1,0,.15f).Right(-.12f,.29f,.27f).RFist(.1f,.6f,.8f,1,0,.15f).Poles(.35f,-1,-.1f,-.35f,-1,-.1f);
        private static readonly Key[][] keys=Build();
        /// <summary>A kind's keys: the five strikes here, the v2 abilities' poses (CyberKit2MeleePoses) after them.</summary>
        public static Key[] Keys(int kind)=>kind>=Kinds?CyberKit2MeleePoses.Keys(kind):keys[Math.Max(0,kind)];
        /// <summary>Whether a kind stands on planted feet (CyberComboFeet); the rest leave the legs to the gait or author them in the air.</summary>
        public static bool PlantsFeet(int kind)=>kind<Kinds||CyberKit2MeleePoses.Plants(kind);
        private static Key[][] Build()
        {
            var k=new Key[Kinds][];var g=Guard();
            // Jab (left): the lead foot steps in as the fist shoots straight out at head height, the lead hip and shoulder
            // driving it (the torso turning right), the rear fist kept at the chin; it snaps back on the same line.
            {
                var load=new B(g).Hips(-.36f,.07f,0).Spine(-.08f,.18f,0).Chest(-.15f,.15f,0).Body(-.03f,0,.01f).Left(.15f,.28f,.36f).LFist(0,.3f,1,-.7f,-.7f,0);
                var hit=new B(g).Hips(-.50f,.08f,0).Spine(-.12f,.24f,0).Chest(-.26f,.24f,0).Head(.74f,-.40f).Body(-.045f,.02f,.04f)
                    .Left(.06f,.34f,.88f).LFist(-.08f,.06f,1,0,-1,0).Poles(.7f,-.7f,0,-.35f,-1,-.1f).Reach(.3f,0).Right(-.12f,.30f,.27f);
                var held=new B(hit).Left(.065f,.335f,.85f);
                var back=new B(g).Hips(-.36f,.07f,0).Spine(-.08f,.2f,0).Chest(-.14f,.18f,0).Head(.55f,-.34f).Body(-.03f,.01f,.03f).Left(.13f,.31f,.48f).LFist(0,.4f,1,-.8f,-.5f,0).Reach(.05f,0);
                var ready=new B(g).Body(-.02f,0,.025f);
                k[Jab]=new[]{g.At(0,0,Stop),load.At(0,.45f),hit.At(0,1,Impact),held.At(1,.15f,Stop),back.At(1,.5f),ready.At(1,1,Stop)};
            }
            // Cross (right): the rear fist drives through the centre line as the hips and shoulders turn hard left over a
            // pivoting rear foot, the weight onto the lead leg; the lead fist returns to the chin.
            {
                var load=new B(g).Hips(-.38f,.07f,0).Spine(-.08f,.18f,0).Chest(-.14f,.15f,0).Body(-.04f,-.01f,.01f).Right(-.14f,.28f,.22f).RFist(0,.4f,1,.8f,-.5f,0).Left(.11f,.31f,.36f);
                var hit=new B(g).Hips(.32f,.08f,0).Spine(.12f,.24f,0).Chest(.22f,.26f,0).Head(-.50f,-.40f).Body(-.06f,.04f,.05f)
                    .Right(-.02f,.34f,.88f).RFist(.05f,.05f,1,0,-1,0).Left(.12f,.30f,.26f).LFist(-.1f,.6f,.8f,-1,0,.2f).Poles(.35f,-1,-.1f,-.7f,-.7f,0).Reach(0,.32f);
                var held=new B(hit).Right(-.025f,.335f,.85f);
                var back=new B(g).Hips(.05f,.07f,0).Spine(.02f,.18f,0).Chest(.05f,.18f,0).Head(-.08f,-.32f).Body(-.04f,.02f,.03f).Right(-.12f,.31f,.36f).RFist(0,.5f,1,.8f,-.4f,0).Reach(0,.05f);
                var ready=new B(g).Hips(.05f,.06f,0).Spine(.02f,.16f,0).Chest(0,.14f,0).Head(-.06f,-.30f).Body(-.025f,.01f,.02f);
                k[Cross]=new[]{g.At(0,0,Stop),load.At(0,.45f),hit.At(0,1,Impact),held.At(1,.15f,Stop),back.At(1,.5f),ready.At(1,1,Stop)};
            }
            // Spinning backfist (left): the cross's turn carried on round to the left in a short hop, the head whipping ahead to
            // spot, the lead fist cocked across the chest and flung out at the end of the arm, its back striking as it sweeps
            // across the front; the spin runs on past the target and settles bladed. The fists ride the torso's turn (Follow).
            {
                var start=new B(g).Hips(.05f,.04f,0).Spine(.02f,.10f,0).Chest(0,.12f,0).Head(-.06f,-.14f).Turn(1);
                var coil=new B(start).Hips(1.45f,.04f,0).Spine(.25f,.08f,0).Chest(.22f,.05f,0).Head(.55f,-.1f).Body(.0f,0,0)
                    .Left(-.08f,.30f,.24f).LFist(-.5f,.25f,.8f,0,0,1).Right(-.12f,.30f,.24f).Poles(.2f,-.6f,.8f,-.35f,-1,-.1f);
                var hit=new B(start).Hips(3.4f,.03f,0).Spine(.5f,.06f,0).Chest(.5f,.04f,0).Head(1.3f,-.05f).Body(.02f,0,0)
                    .Left(.62f,.33f,.16f).LFist(1,.05f,.2f,0,0,1).Right(-.12f,.28f,.20f).Poles(.5f,-.35f,.6f,-.35f,-1,-.1f).Reach(.25f,0);
                var sweep=new B(hit).Hips(4.3f,.03f,0).Spine(.45f,.06f,0).Chest(.35f,.06f,0).Head(.9f,-.08f).Left(.6f,.31f,-.02f).LFist(1,0,-.1f,0,0,1);
                var land=new B(g).Hips(5.9f,.05f,0).Spine(.2f,.1f,0).Chest(.15f,.1f,0).Head(.1f,-.12f).Body(-.05f,0,0).Left(.22f,.30f,.30f).LFist(0,.4f,1,-.8f,-.5f,0).Right(-.12f,.30f,.24f).Turn(1);
                var ready=new B(g).Hips(MeleeMath.TAU-.30f,.04f,0).Turn(1).Body(-.035f,0,0);
                k[Spin]=new[]{start.At(0,0,Stop),coil.At(0,.45f),hit.At(0,1,Impact),sweep.At(1,.2f),land.At(1,.55f),ready.At(1,1,Stop)};
            }
            // Rising uppercut (right): a deep drop with the rear shoulder dipped and the fist low by the hip, then the legs drive
            // the body up onto the toes and the fist rises through the chin line (knuckles up, palm to the face) and on overhead
            // in a short hop; it lands back in the guard.
            {
                var load=new B(g).Hips(-.45f,.12f,0).Spine(-.12f,.25f,-.06f).Chest(-.15f,.20f,-.12f).Head(.55f,-.32f).Body(-.13f,0,.01f)
                    .Right(-.20f,.02f,.30f).RFist(0,1,.3f,0,.1f,-1).Left(.13f,.30f,.30f).Poles(.35f,-1,-.1f,-.4f,-1,.3f);
                var hit=new B(g).Hips(.30f,.0f,0).Spine(.12f,-.05f,.04f).Chest(.16f,-.08f,.06f).Head(-.35f,-.2f).Body(.04f,0,.05f)
                    .Right(-.04f,.45f,.50f).RFist(.05f,1,.35f,0,.1f,-1).Left(.12f,.31f,.26f).Poles(.35f,-1,-.1f,-.5f,-.8f,.4f).Reach(0,.2f);
                var high=new B(hit).Hips(.40f,-.02f,0).Chest(.16f,-.12f,.06f).Head(-.40f,-.26f).Body(.08f,0,.05f).Right(-.03f,.62f,.42f);
                var down=new B(g).Hips(.10f,.04f,0).Spine(0,.08f,0).Chest(.02f,.08f,0).Head(-.05f,-.16f).Body(-.04f,0,.03f).Right(-.12f,.38f,.30f).RFist(0,.7f,.6f,.8f,0,-.2f);
                var ready=new B(g).Hips(-.2f,.04f,0).Body(-.035f,0,.02f);
                k[Rise]=new[]{g.At(0,0,Stop),load.At(0,.55f),hit.At(0,1,Impact),high.At(1,.25f),down.At(1,.6f),ready.At(1,1,Stop)};
            }
            // Pulse Palm (both): both hands chambered by the rear hip as the body coils, then a stomping step in and both palms
            // thrust out side by side at chest height, the hips squared, the body low and forward; held as the pulse fires.
            {
                var load=new B(g).Hips(-.55f,.10f,0).Spine(-.15f,.18f,0).Chest(-.22f,.12f,0).Head(.8f,-.30f).Body(-.07f,-.02f,-.01f)
                    .Left(.0f,.02f,.18f).LFist(0,1,.1f,0,.1f,1).Right(-.17f,-.02f,.08f).RFist(0,1,.1f,0,.1f,1).Poles(.5f,-1,-.3f,-.5f,-1,-.3f);
                var hit=new B(g).Hips(.05f,.14f,0).Spine(.05f,.28f,0).Chest(.04f,.16f,0).Head(-.08f,-.40f).Body(-.10f,0,.06f)
                    .Left(.15f,.13f,.86f).LFist(.1f,1,.25f,0,-.25f,1).Right(-.13f,.11f,.86f).RFist(-.1f,1,.25f,0,-.25f,1).Poles(.8f,-.6f,0,-.8f,-.6f,0).Reach(.3f,.3f);
                var held=new B(hit).Left(.15f,.14f,.82f).Right(-.13f,.12f,.82f);
                var back=new B(g).Hips(-.2f,.08f,0).Spine(-.05f,.18f,0).Chest(-.05f,.14f,0).Head(.25f,-.32f).Body(-.05f,0,.04f).Left(.14f,.26f,.48f).Right(-.13f,.26f,.36f);
                var ready=new B(g).Body(-.03f,0,.03f);
                k[Palm]=new[]{g.At(0,0,Stop),load.At(0,.5f),hit.At(0,1,Impact),held.At(1,.25f,Stop),back.At(1,.6f),ready.At(1,1,Stop)};
            }
            return k;
        }

        /// <summary>One swing on the replicated clock: its kind and its start, hit (the kit's Release) and end.</summary>
        public struct Swing
        {
            public int Kind;public float At,Hit,Until;
            public Swing(int kind,float at,float hit,float until){Kind=kind;At=at;Hit=Math.Max(at+1e-3f,hit);Until=Math.Max(Hit+1e-3f,until);}
            public float KeyTime(in Key k)=>k.Phase==0?At+k.U*(Hit-At):Hit+k.U*(Until-Hit);
            /// <summary>The swing before this one in a string chained at its start (the same pace).</summary>
            public Swing Before(int kind)
            {
                float scale=(Hit-At)/Math.Max(1e-3f,Windup(Kind)),until=At,hit=until-(Seconds(kind)-Windup(kind))*scale;return new Swing(kind,hit-Windup(kind)*scale,hit,until);
            }
        }
        /// <summary>The nominal windup and length of a kind at attack rate 1 (v2's; v1's Jab / Cross / Palm scale the same keys).</summary>
        public static float Windup(int kind)=>CyberKit2Spec.ComboWindups[Math.Max(0,Math.Min(4,kind))];
        public static float Seconds(int kind)=>CyberKit2Spec.ComboSeconds[Math.Max(0,Math.Min(4,kind))];

        /// <summary>Channel `c` (`n` wide) of the swing's keys at `time` into `o` (C1 Hermite through the keys; before the first
        /// key its pose, after the last the last).</summary>
        public static void Sample(in Swing s,float time,float[] o)=>Sample(s,time,o,0,N);
        public static void Sample(in Swing s,float time,float[] o,int c,int n)
        {
            var k=Keys(s.Kind);int last=k.Length-1;
            if(time<=s.KeyTime(k[0])){for(int i=c;i<c+n;i++)o[i]=k[0].V[i];return;}
            if(time>=s.KeyTime(k[last])){for(int i=c;i<c+n;i++)o[i]=k[last].V[i];return;}
            int seg=0;while(seg<last-1&&time>=s.KeyTime(k[seg+1]))seg++;
            float t0=s.KeyTime(k[seg]),t1=s.KeyTime(k[seg+1]),dt=Math.Max(1e-4f,t1-t0),u=(time-t0)/dt;
            for(int i=c;i<c+n;i++)o[i]=MeleeMath.Hermite(k[seg].V[i],k[seg+1].V[i],Out(s,k,seg,i)*dt,In(s,k,seg+1,i)*dt,u);
        }
        // The curve's slope leaving key `j` and arriving at key `j` (per second): Catmull-Rom on the swing's own clock, zero at a
        // Stop and at either end, an Impact arriving at 1.6 times its segment's average speed and leaving at rest.
        private static float Slope(in Swing s,Key[] k,int j,int i)
        {
            if(j<=0||j>=k.Length-1||k[j].How!=Flow)return 0;float a=s.KeyTime(k[j-1]),b=s.KeyTime(k[j+1]);return (k[j+1].V[i]-k[j-1].V[i])/Math.Max(1e-4f,b-a);
        }
        private static float Out(in Swing s,Key[] k,int j,int i)=>k[j].How==Impact?0:Slope(s,k,j,i);
        private static float In(in Swing s,Key[] k,int j,int i)
        {
            if(k[j].How!=Impact)return Slope(s,k,j,i);float a=s.KeyTime(k[j-1]),b=s.KeyTime(k[j]);return 1.6f*(k[j].V[i]-k[j-1].V[i])/Math.Max(1e-4f,b-a);
        }
        /// <summary>The swing's pose at `time`, eased in from `from` (the pose the last swing left at this one's start, or none)
        /// over the first ChainShare of the windup. Yaw channels blend the short way round (the spin's turn).</summary>
        public static void Pose(in Swing s,float time,float[] o,in Swing from,bool chained,float[] scratch)
        {
            Sample(s,time,o);if(!chained)return;float k=MeleeMath.Ease((time-s.At)/Math.Max(1e-4f,(s.Hit-s.At)*ChainShare));if(k>=1)return;
            Sample(from,s.At,scratch);
            for(int i=0;i<N;i++){float a=scratch[i];if(i==HY||i==SY||i==CY)a=o[i]+MeleeMath.Wrap(a-o[i]);o[i]=a+(o[i]-a)*k;}
        }

        // ---- into a capture row (MeleeCapture's channels; the fists through the rig's arm IK) ----
        private static Vector3 V3(float[] p,int i)=>new Vector3(p[i],p[i+1],p[i+2]);
        private static void Put(float[] row,int i,Vector3 v){row[i]=v.X;row[i+1]=v.Y;row[i+2]=v.Z;}
        private static void PutQ(float[] row,int i,Quaternion q)
        {
            q=Quaternion.Normalize(q);if(q.W<0)q=new Quaternion(-q.X,-q.Y,-q.Z,-q.W);float s=(float)Math.Sqrt(Math.Max(0,1-q.W*q.W));
            if(s<1e-6f){row[i]=row[i+1]=row[i+2]=0;return;}float a=2*(float)Math.Acos(MeleeMath.Clamp(q.W,-1,1));row[i]=q.X/s*a;row[i+1]=q.Y/s*a;row[i+2]=q.Z/s*a;
        }
        /// <summary>The hips' height channel the stance takes ride (the alert take's mean); a pose's Drop is under it.</summary>
        public const float BaseBy=-.10f;
        /// <summary>
        /// A pose (channels from Pose) as a capture row: the hips, spine, chest, neck and head rotations, the hips' height, the
        /// clavicles' reach, and each fist as a chest-space IK target and wrist frame (its knuckles along K, its palm side to P)
        /// with an elbow pole, measured through the row's own torso (the drawn body's lean and sway move it a little). `torso`
        /// scales the torso's turn (the first-person view keeps a third). Fists with Follow ride the torso's whole turn.
        /// </summary>
        public static void Row(float[] p,float torso,float[] row)
        {
            Array.Clear(row,0,row.Length);
            float hy=p[HY]*torso,sy=p[SY]*torso,cy=p[CY]*torso;
            // Each segment's lean and side bend are toward the facing (the body's frame), its twist about its own axis: the hips,
            // then the spine and chest adding theirs, the neck and head turning and pitching on from the chest (the head's
            // pitch relative to upright, so -lean looks level).
            Quaternion W(float lean,float side,float yaw)=>MeleeMath.AxisAngle(MeleeMath.X,lean)*MeleeMath.AxisAngle(MeleeMath.Z,side)*MeleeMath.AxisAngle(MeleeMath.Y,yaw);
            float l1=p[HP],l2=l1+p[SP],l3=l2+p[CP],r1=p[HR],r2=r1+p[SR],r3=r2+p[CR],y3=hy+sy+cy;
            Quaternion wp=W(l1,r1,hy),ws=W(l2,r2,hy+sy),wc=W(l3,r3,y3),wn=W(l3+p[NP]*.45f,r3+p[NR]*.45f,y3+p[NY]*.45f*torso),wh=W(l3+p[NP],r3+p[NR],y3+p[NY]*torso);
            Quaternion qp=wp,qs=MeleeMath.Inverse(wp)*ws,qc=MeleeMath.Inverse(ws)*wc;
            PutQ(row,MeleeCapture.Pelvis,qp);PutQ(row,MeleeCapture.Spine,qs);PutQ(row,MeleeCapture.Chest,qc);
            PutQ(row,MeleeCapture.Neck,MeleeMath.Inverse(wc)*wn);PutQ(row,MeleeCapture.Head,MeleeMath.Inverse(wn)*wh);
            PutQ(row,MeleeCapture.LClav,MeleeMath.AxisAngle(MeleeMath.Y,-p[LC]));PutQ(row,MeleeCapture.RClav,MeleeMath.AxisAngle(MeleeMath.Y,p[RC]));
            row[MeleeCapture.By]=BaseBy+p[Drop];
            // The chest's frame in the body's (the rig's bone offsets; the body itself unturned at the hips' height and shift).
            var body=new Vector3(p[ShX],row[MeleeCapture.By]-.025f,p[ShZ]);var pelvis=body+CapturedRig.Bones[CapturedRig.Pelvis].Position;
            var spine=pelvis+Vector3.Transform(CapturedRig.Bones[CapturedRig.Spine].Position,qp);var qps=qp*qs;
            var chest=spine+Vector3.Transform(CapturedRig.Bones[CapturedRig.Chest].Position,qps);var qch=qps*qc;var inv=MeleeMath.Inverse(qch);
            var follow=MeleeMath.AxisAngle(MeleeMath.Y,(hy+sy+cy)*p[Follow]);
            Fist(p,L,LK,LP,LO,follow,chest,inv,out var lt,out var lf,out var lu,out var lpole);
            Fist(p,R,RK,RP,RO,follow,chest,inv,out var rt,out var rf,out var ru,out var rpole);
            Put(row,MeleeCapture.Grip,rt);Put(row,MeleeCapture.Blade,rf);Put(row,MeleeCapture.Edge,Vector3.Cross(ru,rf));Put(row,MeleeCapture.RPole,rpole);
            Put(row,MeleeCapture.LHand,lt);Put(row,MeleeCapture.LFwd,lf);Put(row,MeleeCapture.LUp,lu);Put(row,MeleeCapture.LPole,lpole);
            // The legs (used only by airborne poses, Move.Legs): each thigh swung about the hips' axis, slightly apart; the knee bent.
            PutQ(row,MeleeCapture.LHip,MeleeMath.AxisAngle(MeleeMath.X,-p[LT])*MeleeMath.AxisAngle(MeleeMath.Z,.06f));PutQ(row,MeleeCapture.RHip,MeleeMath.AxisAngle(MeleeMath.X,-p[RT])*MeleeMath.AxisAngle(MeleeMath.Z,-.06f));
            row[MeleeCapture.LKnee]=Math.Max(0,p[LKn]);row[MeleeCapture.RKnee]=Math.Max(0,p[RKn]);
            PutQ(row,MeleeCapture.LAnkle,MeleeMath.AxisAngle(MeleeMath.X,.35f*Math.Min(1,p[LKn])));PutQ(row,MeleeCapture.RAnkle,MeleeMath.AxisAngle(MeleeMath.X,.35f*Math.Min(1,p[RKn])));
        }
        // A fist into chest space: the target, the wrist's +z (the palm side) and +y (back up the forearm: against the knuckles).
        private static void Fist(float[] p,int at,int knuckle,int palm,int pole,Quaternion follow,Vector3 chest,Quaternion inv,out Vector3 target,out Vector3 fwd,out Vector3 up,out Vector3 elbow)
        {
            var pos=Vector3.Transform(V3(p,at),follow);var k=MeleeMath.Unit(Vector3.Transform(V3(p,knuckle),follow));var z=Vector3.Transform(V3(p,palm),follow);
            if(k.LengthSquared()<1e-6f)k=MeleeMath.Z;var y=-k;z-=y*Vector3.Dot(z,y);if(z.LengthSquared()<1e-6f)z=Vector3.Cross(y,MeleeMath.X);z=Vector3.Normalize(z);
            target=Vector3.Transform(pos-chest,inv);fwd=Vector3.Transform(z,inv);up=Vector3.Transform(y,inv);elbow=Vector3.Transform(Vector3.Transform(V3(p,pole),follow),inv);
        }
        /// <summary>A fist's body-space point on the swing at `time` (the keys alone: the effects' trails).</summary>
        public static Vector3 FistAt(in Swing s,float time,bool left,float[] scratch)
        {
            Sample(s,time,scratch);var f=MeleeMath.AxisAngle(MeleeMath.Y,(scratch[HY]+scratch[SY]+scratch[CY])*scratch[Follow]);return Vector3.Transform(V3(scratch,left?L:R),f);
        }
        /// <summary>The whole torso's turn (hips, spine and chest yaw) at `time`: the spin's progress.</summary>
        public static float Turn(in Swing s,float time,float[] scratch){Sample(s,time,scratch,HY,1);float h=scratch[HY];Sample(s,time,scratch,SY,1);float sp=scratch[SY];Sample(s,time,scratch,CY,1);return h+sp+scratch[CY];}
        /// <summary>The striking fist's speed share at `time` (0 at rest, 1 at the drive into the hit): the trails' strength.</summary>
        public static float Drive(in Swing s,float time)
        {
            if(time<s.At||time>s.Until)return 0;float lead=(s.Hit-s.At)*.65f;
            return time<=s.Hit?MeleeMath.Smooth((time-(s.Hit-lead))/Math.Max(1e-4f,lead)):1-MeleeMath.Smooth((time-s.Hit)/Math.Max(1e-4f,(s.Until-s.Hit)*.35f));
        }

        // ---- the footwork plan (CyberComboFeet) ----
        /// <summary>One foot move in a swing: a Step (lifted, carried on an arc, landed flat on its stance spot plus X/Z, turned
        /// by Yaw), a Pivot (the heel raised by Lift on the pinned ball and the foot turned by Yaw), or a Hop (both feet off
        /// the floor by Lift, carried round with the hips' turn and landed on their stance spots).</summary>
        public struct FootMove{public int Foot;public byte Type;public byte P0,P1;public float U0,U1,X,Z,Yaw,Lift;}
        public const byte StepMove=0,PivotMove=1,HopMove=2;
        public const int LeadFoot=0,RearFoot=1,BothFeet=2;
        private static FootMove Step(int foot,byte p0,float u0,byte p1,float u1,float x,float z,float yaw,float lift)=>new FootMove{Foot=foot,Type=StepMove,P0=p0,U0=u0,P1=p1,U1=u1,X=x,Z=z,Yaw=yaw,Lift=lift};
        private static FootMove Pivot(int foot,byte p0,float u0,byte p1,float u1,float yaw,float heel)=>new FootMove{Foot=foot,Type=PivotMove,P0=p0,U0=u0,P1=p1,U1=u1,Yaw=yaw,Lift=heel};
        private static FootMove Hop(byte p0,float u0,byte p1,float u1,float lift)=>new FootMove{Foot=BothFeet,Type=HopMove,P0=p0,U0=u0,P1=p1,U1=u1,Lift=lift};
        private static readonly FootMove[][] feet=
        {
            // Jab: the lead foot steps in 12 cm, landing as the fist does; the rear foot follows in the recovery.
            new[]{Step(LeadFoot,0,.05f,0,.92f,0,.12f,0,.035f),Step(RearFoot,1,.25f,1,.75f,0,.06f,0,.02f)},
            // Cross: the rear foot pivots on its ball, the heel turning out as the hips drive round.
            new[]{Pivot(RearFoot,0,.1f,1,.75f,.45f,.5f)},
            // Spinning backfist: a short hop round with the hips, landing back on the stance.
            new[]{Hop(0,.12f,1,.38f,.07f)},
            // Rising uppercut: both heels up as the legs drive, then a little hop off the toes.
            new[]{Pivot(LeadFoot,0,.45f,1,.12f,0,.4f),Pivot(RearFoot,0,.45f,1,.12f,0,.45f),Hop(1,.1f,1,.5f,.045f)},
            // Pulse Palm: a stomping step in 18 cm landing on the hit; the rear foot drags up after.
            new[]{Step(LeadFoot,0,.25f,0,.97f,0,.18f,0,.06f),Step(RearFoot,1,.35f,1,.82f,0,.08f,0,.02f)},
        };
        public static FootMove[] Feet(int kind)=>kind>=Kinds?CyberKit2MeleePoses.Feet(kind):feet[Math.Max(0,kind)];
        public static FootMove MakeStep(int foot,byte p0,float u0,byte p1,float u1,float x,float z,float yaw,float lift)=>Step(foot,p0,u0,p1,u1,x,z,yaw,lift);
        public static FootMove MakePivot(int foot,byte p0,float u0,byte p1,float u1,float yaw,float heel)=>Pivot(foot,p0,u0,p1,u1,yaw,heel);
        public static float MoveTime(in Swing s,byte phase,float u)=>phase==0?s.At+u*(s.Hit-s.At):s.Hit+u*(s.Until-s.Hit);
    }

    /// <summary>
    /// The combo's feet on one rig (Core/CyberLordCombo.cs): a foot on the floor never moves along it (pinned flat, or on its
    /// ball with the heel raised while it pivots); otherwise it is lifted on a real step (off its ball, on an arc that clears
    /// the floor, landing flat) or in a hop. Each swing's moves start from where the feet actually stand as it begins (the gait's
    /// feet, or the last swing's), held in the world (the swing's own frame), so a turn of the aim mid-swing never slides them;
    /// the targets are the stance spots (CyberLordStance) plus each move's offset. After the string the gait takes them back
    /// to the stance with its own settle steps. Rig-local presentation state only; a rewound clock starts the swing over.
    /// </summary>
    public sealed class CyberComboFeet
    {
        private struct Foot{public float X,Z,Yaw,Pitch,Y;}
        private readonly Foot[] start=new Foot[2],now=new Foot[2];
        private float swingAt=-100,last=-100;private Vector3 origin;private float frameYaw;private bool has,idle=true;
        /// <summary>The hop's lift of the body this frame (m) and whether the feet are posed.</summary>
        public float Hop{get;private set;}
        public bool Active{get;private set;}
        public void Reset(){has=false;swingAt=-100;last=-100;Active=false;Hop=0;seenAt=float.NaN;}
        private readonly Vector3[] seen=new Vector3[2];private readonly Quaternion[] seenQ=new Quaternion[2];private float seenAt=float.NaN;
        /// <summary>Notes where the gait stands the feet this frame, before the strike's torso moves the pelvis over them: a new
        /// swing's feet start there (read after the torso, they would jump with it).</summary>
        public void Before(MeleeRig r,float time){for(int i=0;i<2;i++)seen[i]=r.ReadAnkle(i==0,out seenQ[i],out _);seenAt=time;}
        private static readonly float[] turnScratch=new float[CyberCombo.N];
        private static void Stance(int foot,out float x,out float z,out float yaw)=>CyberLordStance.Stance.Foot(foot==0,out x,out z,out yaw);
        // The ball (toe joint) of a foot whose ankle is `ankle` with heading `yaw` and heel pitch `pitch` (group space).
        private static Vector2 Ball(Vector3 ankle,float yaw,float pitch){float d=MeleeFootwork.BallAnkleZ(pitch);return new Vector2(ankle.X-(float)Math.Sin(yaw)*d,ankle.Z-(float)Math.Cos(yaw)*d);}
        private Vector3 ToGroup(Vector2 swingXZ,float y,MeleeRig r)
        {
            // swing frame -> world -> the rig's group now
            float c=(float)Math.Cos(frameYaw),s=(float)Math.Sin(frameYaw);var w=new Vector3(origin.X+swingXZ.X*c+swingXZ.Y*s,0,origin.Z-swingXZ.X*s+swingXZ.Y*c);
            float dx=w.X-r.GroupPosition.X,dz=w.Z-r.GroupPosition.Z,gc=(float)Math.Cos(-r.GroupYaw),gs=(float)Math.Sin(-r.GroupYaw);return new Vector3(dx*gc+dz*gs,y,-dx*gs+dz*gc);
        }
        /// <summary>
        /// Poses both legs for `swing` at `time` over the rig (after the arms and torso), at `weight` from the legs it holds.
        /// `fresh`: the swing is new to this rig (its feet start where they stand now).
        /// </summary>
        public void Pose(MeleeRig r,in CyberCombo.Swing swing,float time,float weight)
        {
            Active=false;Hop=0;if(weight<=1e-4f){last=time;idle=true;return;}
            bool rewound=time<last-.05f||time-last>.25f;
            if(!has||Math.Abs(swing.At-swingAt)>1e-4f||rewound||idle)
            {
                // Where the feet stand as the swing starts: the last swing's own (continuous), else the rig's posed ankles.
                bool carry=has&&!rewound&&!idle&&time-last<.1f;var prevOrigin=origin;float prevYaw=frameYaw;
                origin=r.GroupPosition;frameYaw=r.GroupYaw;swingAt=swing.At;
                for(int i=0;i<2;i++)
                {
                    if(carry)
                    {
                        // re-express the last swing's foot in the new frame
                        float c=(float)Math.Cos(prevYaw),s=(float)Math.Sin(prevYaw);var f=now[i];var w=new Vector3(prevOrigin.X+f.X*c+f.Z*s,0,prevOrigin.Z-f.X*s+f.Z*c);
                        float dx=w.X-origin.X,dz=w.Z-origin.Z,gc=(float)Math.Cos(-frameYaw),gs=(float)Math.Sin(-frameYaw);start[i]=new Foot{X=dx*gc+dz*gs,Z=-dx*gs+dz*gc,Yaw=MeleeMath.Wrap(f.Yaw+prevYaw-frameYaw),Pitch=f.Pitch};
                    }
                    else
                    {
                        Vector3 ankle;Quaternion q;if(seenAt==time){ankle=seen[i];q=seenQ[i];}else ankle=r.ReadAnkle(i==0,out q,out _);var fwd=Vector3.Transform(MeleeMath.Z,q);float yaw=(float)Math.Atan2(fwd.X,fwd.Z),pitch=(float)Math.Asin(MeleeMath.Clamp(-fwd.Y,-1,1));
                        pitch=Math.Max(0,pitch);var b=Ball(ankle,yaw,pitch);start[i]=new Foot{X=b.X,Z=b.Y,Yaw=yaw,Pitch=ankle.Y>MeleeRig.FLOOR+.02f?pitch:0};
                    }
                }
                has=true;idle=false;
            }
            last=time;
            // A body that leaves the swing's spot (a knockback, a host move) takes its feet with it: the gait's again.
            float drift=new Vector2(r.GroupPosition.X-origin.X,r.GroupPosition.Z-origin.Z).Length();weight*=1-MeleeMath.Smooth((drift-.15f)/.2f);if(weight<=1e-4f){idle=true;return;}
            for(int i=0;i<2;i++)now[i]=start[i];
            // The moves in order, each from where the earlier ones left its foot: a pure function of the swing's start and the time.
            var moves=CyberCombo.Feet(swing.Kind);
            for(int m=0;m<moves.Length;m++)
            {
                var mv=moves[m];float t0=CyberCombo.MoveTime(swing,mv.P0,mv.U0),t1=Math.Max(t0+1e-3f,CyberCombo.MoveTime(swing,mv.P1,mv.U1));
                if(time<t0)continue;float u=MeleeMath.Clamp((time-t0)/(t1-t0),0,1);
                for(int i=0;i<2;i++)
                {
                    if(mv.Foot!=CyberCombo.BothFeet&&mv.Foot!=i)continue;
                    Stance(i,out float sx,out float sz,out float syaw);var spot=Ball(new Vector3(sx,MeleeRig.FLOOR,sz),syaw,0);var f=now[i];float yaw0=f.Yaw,pitch0=f.Pitch;
                    switch(mv.Type)
                    {
                        case CyberCombo.StepMove:
                        {
                            float tx=spot.X+mv.X,tz=spot.Y+mv.Z,tyaw=syaw+mv.Yaw;
                            if(Math.Abs(tx-f.X)+Math.Abs(tz-f.Z)<.02f&&Math.Abs(MeleeMath.Wrap(tyaw-f.Yaw))<.05f){f.Pitch=pitch0*(1-MeleeMath.Smooth(u));break;}
                            float e=MeleeMath.Ease(u);f.X+=(tx-f.X)*e;f.Z+=(tz-f.Z)*e;f.Yaw=yaw0+MeleeMath.Wrap(tyaw-yaw0)*e;
                            // off the ball (the heel first), over the arc, down flat
                            f.Y=mv.Lift*4*u*(1-u);f.Pitch=Math.Max(pitch0*(1-MeleeMath.Smooth(u/.4f)),.3f*(1-MeleeMath.Smooth(u/.45f)));break;
                        }
                        case CyberCombo.PivotMove:
                        {
                            // the heel up quickly, held, down by the end; the foot turns on its pinned ball
                            float heel=mv.Lift*MeleeMath.Smooth(u/.3f)*(1-MeleeMath.Smooth((u-.7f)/.3f));f.Pitch=Math.Max(pitch0*(1-MeleeMath.Smooth(u/.3f)),heel);f.Yaw=yaw0+mv.Yaw*MeleeMath.Ease(u/.6f);break;
                        }
                        default:
                        {
                            // both feet leave the floor, carried round with the hips' turn (eased in as they lift, so nothing
                            // drags along the floor), and land on the stance spots: R(turn so far) of the way from where they
                            // stood to the spots taken back by the whole turn, so it starts where they stand and ends on the spots
                            float e=MeleeMath.Ease(u),t0Turn=CyberCombo.Turn(swing,t0,turnScratch),whole=CyberCombo.Turn(swing,t1,turnScratch)-t0Turn;
                            float sofar=u>=1?whole:(CyberCombo.Turn(swing,time,turnScratch)-t0Turn)*MeleeMath.Smooth(u/.3f);
                            float bc=(float)Math.Cos(-whole),bs=(float)Math.Sin(-whole),sx2=spot.X*bc+spot.Y*bs,sz2=-spot.X*bs+spot.Y*bc;
                            float x=f.X+(sx2-f.X)*e,z=f.Z+(sz2-f.Z)*e,c=(float)Math.Cos(sofar),s=(float)Math.Sin(sofar);
                            f.X=x*c+z*s;f.Z=-x*s+z*c;f.Yaw=yaw0+MeleeMath.Wrap(syaw-whole-yaw0)*e+sofar;f.Y=mv.Lift*(float)Math.Sin(Math.PI*u);f.Pitch=Math.Max(pitch0*(1-e),.35f*(float)Math.Sin(Math.PI*u));
                            Hop=Math.Max(Hop,f.Y);break;
                        }
                    }
                    if(u>=1){f.Y=0;if(mv.Type!=CyberCombo.PivotMove)f.Pitch=0;}
                    now[i]=f;
                }
            }
            Active=true;
            for(int i=0;i<2;i++)
            {
                var f=now[i];var ball=ToGroup(new Vector2(f.X,f.Z),0,r);float yaw=MeleeMath.Wrap(f.Yaw+frameYaw-r.GroupYaw),pitch=f.Pitch;
                float d=MeleeFootwork.BallAnkleZ(pitch);var ankle=new Vector3(ball.X+(float)Math.Sin(yaw)*d,Math.Max(MeleeRig.FLOOR,MeleeFootwork.BallAnkleY(pitch))+f.Y,ball.Z+(float)Math.Cos(yaw)*d);
                r.StepFoot(i==0,ankle,yaw,pitch,-Math.Max(0,pitch)*.85f*(f.Y>.005f?.4f:1),weight);
            }
        }
    }
}
