using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The procedural entrances (owner 2026-10-11: "Add more entrances too that fit this"): not tied to a weapon, so any
    // pilot can pick them in the Locker. Each keeps the standard entrance timing (4.8 s, name card 3.2 s) and lands its
    // beats on the 0.4 s grid of the sting it plays (NativeIntroRules.IntroSting). Empty hands, like every non-weapon entrance.

    /// <summary>
    /// A procedural entrance: keyed poses with planted feet, a step plan and an optional root path. Each settles into the
    /// shared hero hold under the name card (NativeEntranceKeys.Hero) with both arms loose: its last key before the card lets
    /// the arms hang (owner 2026-10-13: the old hold lifted the right fist to the hip, the one-arm raise in every entrance).
    /// </summary>
    public abstract class NativeProceduralEntrance:NativeEntranceScript
    {
        /// <summary>The hero facing every procedural entrance ends on.</summary>
        public const float Facing=-.22f;
        protected NativeEntranceKeys Keys;protected float[] Start;protected NativeEntrancePose.Step[] Steps=Array.Empty<NativeEntrancePose.Step>();
        private readonly float[] feet=new float[8];protected Func<float,Vector3,Vector3> RootPath;
        protected NativeProceduralEntrance(string style,float hit,int accent):base(style,hit,accent){Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f);}
        /// <summary>A stance (left x, z, yaw, right x, z, yaw) in the hero facing's frame, as world values.</summary>
        protected static float[] Stance(float lx,float lz,float lyaw,float rx,float rz,float ryaw,float facing=Facing,float ox=0,float oz=0)
        {
            var l=NativeEntranceMath.RotateY(new Vector3(lx,0,lz),facing);var r=NativeEntranceMath.RotateY(new Vector3(rx,0,rz),facing);
            return new[]{l.X+ox,l.Z+oz,lyaw+facing,r.X+ox,r.Z+oz,ryaw+facing};
        }
        protected static NativeEntrancePose.Step Step(bool left,float lift,float land,float x,float z,float yaw,float h=.08f,float facing=Facing,float ox=0,float oz=0)
        {var p=NativeEntranceMath.RotateY(new Vector3(x,0,z),facing);return new NativeEntrancePose.Step(left,lift,land,p.X+ox,p.Z+oz,yaw+facing,h);}
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            NativeEntrancePose.Feet(t,Start,Steps,feet);NativeEntrancePose.Keyed(Keys,t,pose,out root,out rotation,feet,NameAt,RootPath);Arms(t,pose,root,rotation);
        }
        /// <summary>Hand IK over the keyed arms (grips, a palm on the floor, a chip to the chest).</summary>
        protected virtual void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation){}
        public override void Lights(float t,NativeEntranceLights l){NativeEntranceStage.Rig(l,1,Accent,1,1);l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f),3.2f);}
        protected const int B=CapturedRig.Body,P=CapturedRig.Pelvis,Sp=CapturedRig.Spine,C=CapturedRig.Chest,Hd=CapturedRig.Head,LA=CapturedRig.LArm,LE=CapturedRig.LElbow,LH=CapturedRig.LHand,RA=CapturedRig.RArm,RE=CapturedRig.RElbow,RH=CapturedRig.RHand,LL=CapturedRig.LHip,LK=CapturedRig.LKnee,RL=CapturedRig.RHip,RK=CapturedRig.RKnee;
        /// <summary>The shared hero frame under the name card (the stage's axes; the pilot faces Facing).</summary>
        protected static NativeEntranceShot HeroShot()=>Shot(NameAt,NativeEntranceCut.Cut,36,34,V(.9f,.42f,3.7f),V(.6f,.46f,3.35f),V(0,1.0f,0),V(0,1.02f,0),1,0);
        protected static Vector3 Local(float x,float y,float z)=>NativeEntranceMath.RotateY(new Vector3(x,y,z),Facing);
    }

    /// <summary>Hero Landing: falls out of the sky into a three-point landing, the floor cracking in a crater of dust.</summary>
    internal sealed class HeroLanding:NativeProceduralEntrance
    {
        private const float Land=1.2f,Top=9.5f;
        public HeroLanding():base("hero-landing",Land,0x9fd8ff)
        {
            SlowRate=.25f;SlowHold=.16f;SlowRecover=.3f;SlowCatchUp=1.0f;
            Start=Stance(.2f,.36f,.15f,-.18f,-.12f,-.42f);
            Steps=new[]{Step(true,2.45f,2.8f,.16f,.07f,.1f,.06f),Step(false,2.55f,2.95f,-.18f,-.1f,-.42f,.07f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(.25f,.88f,-.35f,.5f).Spin(-.25f,0).Planted(0).J(B,-.25f).J(Hd,.1f).J(LA,-2.7f,0,.4f).J(LE,-.25f).J(RA,-2.5f,0,-.5f).J(RE,-.35f).J(LL,-.25f,0,.08f).J(LK,.45f).J(RL,.15f,0,-.08f).J(RK,.6f);
            k.Key(.55f,stand).Root(.15f,.88f,-.2f,.2f).Spin(.1f,0).Planted(0).J(B,.15f).J(Hd,-.1f).J(LA,-1.6f,0,1.0f).J(LE,-.5f).J(RA,-1.4f,0,-1.0f).J(RE,-.6f).J(LL,-1.1f,0,.15f).J(LK,1.5f).J(RL,-.7f,0,-.12f).J(RK,1.7f).PassKey();
            k.Key(1.02f,stand).Root(.04f,.88f,-.05f,-.12f).Planted(0).J(B,.25f).J(LA,-.4f,0,1.2f).J(LE,-.6f).J(RA,-1.0f,0,-.35f).J(RE,-.3f).J(LL,-.7f,0,.12f).J(LK,.9f).J(RL,-.3f,0,-.1f).J(RK,1.1f).PassKey();
            // The landing: left foot planted forward, right knee down, a fist on the floor, the other arm thrown back, head bowed.
            float[] kneel(float drop,float lean,float head)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=-.25f;v[NativeEntranceKeys.BodyY]=drop;v[NativeEntranceKeys.PlantL]=1;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Sp,.12f,0,0);NativeEntranceKeys.Set(v,C,.08f,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);
                NativeEntranceKeys.Set(v,LL,-1.3f,0,.22f);NativeEntranceKeys.Set(v,LK,1.8f,0,0);NativeEntranceKeys.Set(v,RL,.35f,0,-.12f);NativeEntranceKeys.Set(v,RK,1.5f,0,0);
                NativeEntranceKeys.Set(v,LA,.55f,0,1.05f);NativeEntranceKeys.Set(v,LE,-.7f,0,0);NativeEntranceKeys.Set(v,RA,-.8f,0,-.2f);NativeEntranceKeys.Set(v,RE,-.15f,0,0);});
            k.Key(Land,kneel(-.34f,.62f,.4f)).ImpactKey();
            k.Key(1.6f,kneel(-.37f,.66f,.48f));
            k.Key(2.05f,kneel(-.35f,.6f,.12f));
            k.Key(2.35f,kneel(-.31f,.5f,-.25f)).J(C,-.12f).J(LA,.3f,0,.8f);
            k.Key(2.9f,NativeEntranceKeys.Stand()).Root(0,.88f,0,-.22f).J(B,.04f).J(Hd,0,.1f,0).J(RA,-.14f,0,-.2f).J(RE,-.4f).J(LA,-.1f,0,.2f).J(LE,-.3f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            // The fall: constant gravity from far above, so it arrives fast and decelerates into the knees.
            RootPath=(t,r)=>t<Land?new Vector3(r.X,Top-(Top-.88f)*(t/Land)*(t/Land),r.Z):r;
            Shots=new[]
            {
                // High over the arena, falling with the pilot: the lit ring of pylons far below, a slow roll.
                Shot(0,NativeEntranceCut.Cut,56,50,V(1.8f,1.6f,2.4f),V(1.45f,1.45f,2.1f),V(0,.45f,0),V(0,.7f,0),0,1,roll0:.1f,roll1:.02f,trackY:1),
                // From the floor, wide: the meteor drops into the frame, the tilt chasing it down to the dais.
                Shot(.62f,NativeEntranceCut.Cut,46,44,V(3.4f,1.1f,3.6f),V(3.2f,1.0f,3.4f),V(0,4.6f,0),V(0,.9f,0),3,0),
                // The landing, slowed: low, orbiting through the dust.
                Orbit(Land,NativeEntranceCut.Cut,40,37,28,52,3.6f,3.3f,.45f,.55f,V(0,.5f,.1f),V(0,.55f,.1f),1,0),
                // The rise: low, tilting up as the pilot stands.
                Shot(2.05f,NativeEntranceCut.Cut,42,40,V(-1.3f,.35f,3.6f),V(-1.05f,.6f,3.7f),V(0,.75f,0),V(0,1.0f,0),0,0),
                Orbit(2.8f,NativeEntranceCut.Blend,38,36,-17,-8,3.85f,3.9f,.65f,.7f,V(0,1.0f,0),V(0,1.02f,0),0,0,.3f),
                HeroShot(),
            };
        }
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            // The fist on the floor through the landing, lifting as the pilot rises.
            float w=NativeEntranceMath.Ramp(t,Land-.06f,Land)*(1-NativeEntranceMath.Ramp(t,2.1f,2.45f));if(w<=.001f)return;
            var target=Local(-.22f,NativeEntranceStage.FloorY+.075f,.47f);NativeEntrancePose.Reach(pose,root,rotation,false,target,NativeEntranceMath.Yaw(-.25f)*CapturedRig.Euler(.25f,0,0),w);
        }
        protected override float Pylon(int i,float t)=>PowerOn(i,t,.1f,2);
        public override void Props(float t,INativeEntranceProps props,float q)=>Stage(props,t,.5f,Land);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);
            // Wind streaks round the falling body.
            if(t<Land)for(int i=0;i<16;i++)
            {
                if(NativeEntranceMath.Rand(3000,i)>q)continue;float phase=(t*2.2f+NativeEntranceMath.Rand(3000,i+20))%1f;var off=new Vector3(NativeEntranceMath.Signed(3000,i+40)*.55f,(phase-.5f)*1.6f,NativeEntranceMath.Signed(3000,i+60)*.55f);
                float speed=2*(Top-.88f)*t/(Land*Land);fx.Streak(pelvis+off,new Vector3(0,-speed,0),Accent*(.9f*(float)Math.Sin(Math.PI*phase)),.012f,.2f,.03f,.9f,0,3000+i);
            }
            if(t<Land)
            {
                // Re-entry: a hot glow under the body and a long fading trail up the drop.
                float heat=NativeEntranceMath.Ramp(t,.15f,Land),speed=2*(Top-.88f)*t/(Land*Land);fx.Glow(pelvis-new Vector3(0,.6f,0),Accent*(.6f+1.4f*heat),0,.7f+.4f*heat,1,0,3100);
                fx.Streak(pelvis-new Vector3(0,.35f,0),new Vector3(0,-Math.Max(2,speed),0),Vector3.Lerp(Accent,Vector3.One,.4f)*(1.2f+1.2f*heat),.22f,1.2f,.22f,.95f,0,3110);
                for(int i=0;i<6;i++){if(NativeEntranceMath.Rand(3120,i)>q)continue;float a=(t*3+i/6f)%1f;fx.Glow(pelvis+new Vector3(NativeEntranceMath.Signed(3120,i+10)*.15f,.4f+a*2.6f,NativeEntranceMath.Signed(3120,i+20)*.15f),Accent*(.5f*(1-a)),0,.25f+.3f*a,0,a,3120+i);}
            }
            var spot=new Vector3(0,NativeEntranceStage.FloorY,0);float c=t-Land;
            // The crater: cracks racing out and cooling, dust thrown along the floor, chips and sparks.
            if(c>0&&c<2.4f)
            {
                int lines=q<.5f?6:10;float cool=1-NativeEntranceMath.Ramp(c,.4f,2.4f);
                for(int l=0;l<lines;l++)
                {
                    float ang=l/(float)lines*6.2831853f+NativeEntranceMath.Signed(3200,l)*.25f;var prev=spot+new Vector3(0,.012f,0);
                    for(int k=1;k<=9;k++)
                    {
                        if(k*.2f>c*7)break;ang+=NativeEntranceMath.Signed(3201+l,k)*.4f;var p=prev+new Vector3((float)Math.Cos(ang)*.2f,0,(float)Math.Sin(ang)*.2f);
                        float heat=cool*(1-k*.07f);fx.Streak(p,p-prev,Vector3.Lerp(white*2.4f,Accent*1.6f,NativeEntranceMath.Clamp01(c*2.5f+k*.1f))*heat,.03f-k*.002f,0,1,0,c,3210+l*10+k);prev=p;
                    }
                }
            }
            NativeEntranceFx.GroundDust(fx,t,Land,spot,30,.4f,2.8f,2.2f,NativeEntranceFx.Hex(0x8790a2),.6f,.5f,3320,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.Sparks(fx,t,Land,spot+new Vector3(0,.05f,0),new Vector3(0,1,0),1.3f,32,5.2f,.8f,9.8f,.8f,white*3,Accent*2,.02f,3360,q,NativeEntranceStage.FloorY);
            float pop=NativeEntranceMath.Pulse(t,Land,.01f,.12f);if(pop>.02f)fx.Glow(spot+new Vector3(0,.3f,0),white*(2*pop),0,1.2f*pop,1,0,3390);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);float f=NativeEntranceMath.Pulse(t,Land,.01f,.25f);if(f>.01f)l.Add(new Vector3(0,.6f,.3f),NativeEntranceFx.Hex(0xe8f4ff,45*f),7);
            if(t<Land)l.Add(Anchor(NativeEntranceAnchor.Pelvis,t)-new Vector3(0,.4f,0),Accent*(6*NativeEntranceMath.Ramp(t,.3f,Land)),5);
        }
        protected override void Plan()=>Blasts=new[]{new NativeEntranceBlast{At=Land,Style=NativeBlastStyle.Quake,Position=new Vector3(0,NativeEntranceStage.FloorY+.95f,0),Radius=1.8f,Size=.5f}};
    }

    /// <summary>Orbital Drop: a drop pod streaks down and slams into the stage, vents, and blows its door open.</summary>
    internal sealed class OrbitalDrop:NativeProceduralEntrance
    {
        protected override float Pylon(int i,float t)=>PowerOn(i,t,Impact-.05f,1.2f);
        private const float Impact=1.2f,Door=1.6f;
        private static readonly Vector3 Landing=new Vector3(0,0,-1.05f),From=new Vector3(2.6f,18,-6.2f);
        public OrbitalDrop():base("orbital-drop",Door,0xff9a4a)
        {
            SlowRate=.28f;SlowHold=.14f;SlowCatchUp=.95f;
            Start=Stance(.18f,-.92f,.1f,-.17f,-1.12f,-.1f,0);
            Steps=new[]{Step(false,2.1f,2.42f,-.16f,-.6f,0,.09f,0),Step(true,2.35f,2.68f,.18f,-.18f,.05f,.09f,0),Step(false,2.62f,2.95f,-.18f,-.1f,-.42f,.07f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            float[] crouch(float drop,float lean,float head)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.RootZ]=Landing.Z;v[NativeEntranceKeys.BodyY]=drop;
                NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,RA,-.55f,0,-.3f);NativeEntranceKeys.Set(v,RE,-1.25f,0,0);NativeEntranceKeys.Set(v,LA,-.35f,0,.45f);NativeEntranceKeys.Set(v,LE,-1.05f,0,0);});
            k.Key(0,crouch(-.36f,.36f,.45f));
            k.Key(Door,crouch(-.36f,.36f,.42f));
            k.Key(1.95f,crouch(-.3f,.25f,-.15f));
            k.Key(2.3f,stand).Root(0,.88f,-.8f,0).Drop(-.12f).J(B,.14f).J(LA,.25f,0,.15f).J(RA,-.35f,0,-.15f).J(LE,-.4f).J(RE,-.5f).PassKey();
            k.Key(2.65f,stand).Root(0,.88f,-.42f,0).Drop(-.03f).J(B,.06f).J(LA,-.3f,0,.15f).J(RA,.2f,0,-.15f).J(LE,-.5f).J(RE,-.3f).PassKey();
            k.Key(2.98f,stand).Root(0,.88f,-.05f,-.15f).J(RA,-.14f,0,-.2f).J(RE,-.4f).J(LA,-.1f,0,.2f).J(LE,-.3f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            Shots=new[]
            {
                // From the dais, a long lens locked on the pod's fireball as it falls out of the dark.
                Follow(Shot(0,NativeEntranceCut.Cut,22,30,V(1.5f,.32f,2.8f),V(1.35f,.32f,2.55f),V(0,0,0),V(0,0,0),3,0,roll0:-.08f,roll1:-.02f),NativeEntranceAnchor.Focus),
                // Wide and low by the pylons as it comes in, the tilt riding it down to the dais.
                Shot(.72f,NativeEntranceCut.Cut,46,44,V(4.6f,1.0f,3.6f),V(4.3f,.9f,3.3f),V(.9f,5.0f,-2.4f),V(0,1.1f,-1.0f),3,0),
                // The slam: low and close on the steaming pod.
                Shot(Impact,NativeEntranceCut.Cut,42,40,V(1.0f,.32f,2.7f),V(.8f,.34f,2.3f),V(0,1.05f,-1.0f),V(0,1.0f,-.95f),1,0),
                // The door blows past, slowed; the pilot crouched in the light.
                Shot(Door,NativeEntranceCut.Cut,42,38,V(-.35f,1.0f,3.4f),V(-.25f,.92f,3.0f),V(0,1.0f,-.95f),V(0,.88f,-.7f),1,0),
                // Dolly back as the pilot steps out.
                Orbit(2.4f,NativeEntranceCut.Blend,40,38,-14,-24,2.9f,3.7f,.75f,.85f,V(0,1.0f,-.4f),V(0,1.02f,-.1f),0,0,.25f),
                // Hero, the pod behind.
                Shot(NameAt,NativeEntranceCut.Cut,38,35,V(.95f,.48f,3.9f),V(.62f,.52f,3.5f),V(0,1.08f,-.25f),V(0,1.1f,-.25f),1,0),
            };
        }
        /// <summary>The pod's base at `t`: falling along its line, slamming at Impact with a squash and recoil.</summary>
        private static Vector3 Pod(float t)
        {
            if(t<Impact){float u=t/Impact,s=.35f*u+.65f*u*u;return Vector3.Lerp(From,Landing,s);}
            float a=t-Impact;return Landing+new Vector3(0,-.07f*(float)Math.Exp(-9*a)*(float)Math.Cos(22*a)*(a<.6f?1:0),0);
        }
        private static Vector3 Velocity(float t){if(t>=Impact)return Vector3.Zero;float u=t/Impact;return (Landing-From)*((.35f+1.3f*u)/Impact);}
        public override float Solid(float t,int bone)=>t<Door-.05f?0:1;
        protected override Vector3 Focus(float t,Vector3 chest)=>Pod(t)+new Vector3(0,1.2f,0);
        private static readonly Vector3 Hull=NativeEntranceFx.Hex(0x1c212c),Trim=NativeEntranceFx.Hex(0x3a4152);
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,.45f,Impact);
            var b=Pod(t);var dir=Velocity(t);var tilt=dir.LengthSquared()>1e-4f?Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY,-Vector3.Normalize(dir))),.18f):Quaternion.Identity;
            Vector3 W(Vector3 local)=>b+Vector3.Transform(local,tilt);float heat=t<Impact?NativeEntranceMath.Ramp(t,0,.6f):1-NativeEntranceMath.Ramp(t,Impact,Impact+1.4f);
            float strip=t<Impact?2.5f:1.2f+1.6f*NativeEntranceMath.Pulse(t,Door,.02f,.3f)+.5f*(float)Math.Sin(t*9)*(t<Door?1:0);
            for(int i=0;i<8;i++)
            {
                float a=i*.7853982f;var q8=tilt*NativeEntranceMath.Yaw(a);var outward=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
                if(i!=0)props.Piece(WeaponShape.Cube,false,W(outward*.66f+new Vector3(0,NativeEntranceStage.FloorY+1.05f,0)),q8,new Vector3(.56f,2.0f,.07f),Hull);
                // Seams between the panels glow: white-hot on the way down, the pod's colour after.
                var seam=new Vector3((float)Math.Sin(a+.39f),0,(float)Math.Cos(a+.39f));props.Piece(WeaponShape.Cube,true,W(seam*.7f+new Vector3(0,NativeEntranceStage.FloorY+1.05f,0)),tilt*NativeEntranceMath.Yaw(a+.39f),new Vector3(.03f,1.8f,.03f),Vector3.Lerp(Accent*strip,new Vector3(3,2.2f,1.6f),heat*.6f));
            }
            props.Piece(WeaponShape.Cone,false,W(new Vector3(0,NativeEntranceStage.FloorY+2.4f,0)),tilt,new Vector3(.74f,.75f,.74f),Hull);
            props.Piece(WeaponShape.Cube,false,W(new Vector3(0,NativeEntranceStage.FloorY+.08f,0)),tilt*NativeEntranceMath.Yaw(.39f),new Vector3(1.55f,.16f,1.55f),Trim);
            props.Piece(WeaponShape.Cube,false,W(new Vector3(0,NativeEntranceStage.FloorY+2.06f,0)),tilt*NativeEntranceMath.Yaw(.39f),new Vector3(1.48f,.1f,1.48f),Trim);
            for(int i=0;i<4;i++){float a=i*1.5708f+.7854f;var o=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));props.Piece(WeaponShape.Cube,false,W(o*.82f+new Vector3(0,NativeEntranceStage.FloorY+.55f,0)),tilt*NativeEntranceMath.Yaw(a),new Vector3(.06f,1.0f,.34f),Trim);}
            // The beacon on the nose blinks after the landing.
            float beacon=t>Impact&&((int)(t*5))%2==0?3:.3f;props.Piece(WeaponShape.Sphere,true,W(new Vector3(0,NativeEntranceStage.FloorY+2.8f,0)),Quaternion.Identity,new Vector3(.07f),new Vector3(beacon,.1f,.05f));
            // Inside: a lit back wall.
            props.Piece(WeaponShape.Cube,true,W(new Vector3(0,NativeEntranceStage.FloorY+1.05f,-.58f)),tilt,new Vector3(.42f,1.5f,.02f),NativeEntranceFx.Hex(0xffe2b8,t>Door?2.2f:.4f));
            props.Piece(WeaponShape.Cube,false,W(new Vector3(0,NativeEntranceStage.FloorY+.17f,0)),tilt,new Vector3(1.0f,.02f,1.0f),Trim*.6f);
            // The door: shut until it blows, then thrown out to the side, tumbling, skidding to rest.
            DoorPanel(props,t,b,tilt);
        }
        private void DoorPanel(INativeEntranceProps props,float t,Vector3 b,Quaternion tilt)
        {
            Vector3 pos;Quaternion rot;var start=new Vector3(0,NativeEntranceStage.FloorY+1.05f,.68f);
            if(t<Door){pos=b+Vector3.Transform(start,tilt);rot=tilt;}
            else
            {
                float a=t-Door,land=.915f;var v=new Vector3(3.2f,3.5f,4.0f);
                if(a<land){pos=Landing+start+v*a+new Vector3(0,-4.9f*a*a,0);rot=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.3f,0,1)),-7*a)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,5.5f*a);}
                else{float s=a-land,slide=1-(float)Math.Exp(-4*s);var rest=Landing+start+v*land+new Vector3(0,-4.9f*land*land,0);pos=new Vector3(rest.X+1.1f*slide,NativeEntranceStage.FloorY+.04f,rest.Z+1.3f*slide);
                    rot=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.6f+.4f*slide)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,-1.5708f);}
            }
            props.Piece(WeaponShape.Cube,false,pos,rot,new Vector3(.52f,1.62f,.07f),Hull*1.3f);
            var glow=t<Door?Accent*(1.4f+1.4f*(float)Math.Sin(t*14)*(t>Impact?1:0)):Accent*.5f;
            props.Piece(WeaponShape.Cube,true,pos+Vector3.Transform(new Vector3(0,0,.045f),rot),rot,new Vector3(.3f,.05f,.02f),glow);
        }
        public override int Boxes(float t,Span<Vector4> into){int n=NativeEntranceStage.PylonBoxes(into,0);if(t>=Impact){into[n++]=new Vector4(Landing.X,1.05f,Landing.Z,0);into[n++]=new Vector4(.78f,1.3f,.78f,0);}return n;}
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;var fire=NativeEntranceFx.Hex(0xffb35a,2.6f);
            // Re-entry: a fireball at the shield and a smoke trail hanging along the line.
            if(t<Impact)
            {
                var b=Pod(t);var d=Vector3.Normalize(Velocity(t));fx.Glow(b+d*.05f,white*3,0,1.3f,1,0,3500);fx.Glow(b-d*.4f,fire,0,3.2f,0,0,3501);
                for(int i=0;i<10;i++){var p=b-d*(.4f+i*.35f)+NativeEntranceMath.Direction(3510,i)*.15f;fx.Streak(p,-d*6,Vector3.Lerp(white*3,fire,i/10f)*(1-i/12f),.12f-i*.008f,.4f,.05f,.8f,0,3510+i);}
                // The plasma sheath: flames peeling off the rim of the heat shield and streaming back past the hull.
                var side=Vector3.Normalize(Vector3.Cross(d,Vector3.UnitX));var side2=Vector3.Cross(d,side);
                for(int i=0;i<14;i++){if(i%2==1&&NativeEntranceMath.Rand(3540,i)>q)continue;float ang=i/14f*6.2831853f+t*3,ph=(t*4.5f+NativeEntranceMath.Rand(3540,i+30))%1f;
                    var p=b+(side*(float)Math.Cos(ang)+side2*(float)Math.Sin(ang))*(.8f+.25f*ph)-d*(.2f+1.6f*ph);fx.Streak(p,-d*9,Vector3.Lerp(white*2.6f,fire*1.4f,ph)*(1-ph*.8f),.07f-.03f*ph,.3f,.05f,.9f,0,3540+i);}
                // The burning trail along the path it came down (cooling from white through orange).
                for(int i=1;i<=16;i++){float back=t-i*.03f;if(back<0)break;if(i%2==0&&NativeEntranceMath.Rand(3530,i)>q)continue;var p=Pod(back)+new Vector3(0,.6f,0)+NativeEntranceMath.Direction(3530,i)*(.04f*i);
                    fx.Glow(p,Vector3.Lerp(white*1.6f,fire*.8f,i/8f)*(1-i/17f),0,.55f+.09f*i,0,0,3530+i);}
            }
            for(int k=0;k<34;k++)
            {
                if(NativeEntranceMath.Rand(3550,k)>q)continue;float born=k*Impact/34,a=t-born;if(a<0||a>2.6f||born>t)continue;float u=a/2.6f;var p=Pod(born)+new Vector3(0,.8f,0)+NativeEntranceMath.Direction(3550,k)*(.2f+.5f*u);
                fx.Smoke(p,NativeEntranceFx.Hex(0x6b6f7a),.55f*(1-u)*NativeEntranceMath.Smooth(u*8),.9f+1.6f*u,u,3550+k,NativeEntranceStage.FloorY);
            }
            var land=new Vector3(Landing.X,NativeEntranceStage.FloorY,Landing.Z);
            NativeEntranceFx.GroundDust(fx,t,Impact,land,30,.9f,2.8f,2.3f,NativeEntranceFx.Hex(0x878f9e),.62f,.55f,3600,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.Sparks(fx,t,Impact,land+new Vector3(0,.1f,0),new Vector3(0,1,0),1.5f,30,5.5f,.8f,9.8f,.8f,white*3,fire,.022f,3640,q,NativeEntranceStage.FloorY);
            // Vents: steam jets from the sides, hissing through the door blow.
            NativeEntranceFx.Jet(fx,t,Impact+.08f,2.4f,land+new Vector3(.75f,.55f,0),new Vector3(1,.15f,0),16,1.6f,1.0f,.32f,NativeEntranceFx.Hex(0xc9ced8),.42f,3680,q,NativeEntranceStage.FloorY,.5f,.18f);
            NativeEntranceFx.Jet(fx,t,Impact+.08f,2.4f,land+new Vector3(-.75f,.55f,0),new Vector3(-1,.15f,0),16,1.6f,1.0f,.32f,NativeEntranceFx.Hex(0xc9ced8),.42f,3700,q,NativeEntranceStage.FloorY,.5f,.18f);
            // The door seam flares, then blows.
            var door=land+new Vector3(0,1.05f,.7f);float seam=NativeEntranceMath.Ramp(t,Door-.3f,Door)*(t<Door?1:0);if(seam>.01f)for(int i=0;i<6;i++)fx.Glow(door+new Vector3(NativeEntranceMath.Signed(3720,i)*.28f,NativeEntranceMath.Signed(3721,i)*.75f,0),white*(1.5f*seam),0,.18f,0,0,3720+i);
            NativeEntranceFx.Sparks(fx,t,Door,door,new Vector3(.4f,.3f,1),.9f,34,4.6f,.7f,9.8f,1.0f,white*3,fire,.022f,3740,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.Jet(fx,t,Door,Door+.12f,door,new Vector3(0,.2f,1),70,1.8f,1.4f,.4f,NativeEntranceFx.Hex(0xb8bcc6),.5f,3780,q,NativeEntranceStage.FloorY,.3f,.5f);
            // The light pouring out of the open pod.
            float shaft=NativeEntranceMath.Ramp(t,Door,Door+.15f)*(1-.5f*NativeEntranceMath.Ramp(t,2.6f,3.4f));if(shaft>.01f)NativeEntranceFx.Shaft(fx,t,land+new Vector3(0,1.2f,-.3f),land+new Vector3(0,.4f,1.8f),.25f,.9f,NativeEntranceFx.Hex(0xffd8a8,.05f),shaft,7,10,3800,q);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float key=t<Impact?.35f:.35f+.65f*NativeEntranceMath.Ramp(t,Impact,Impact+.5f);NativeEntranceStage.Rig(l,key,Accent,1,.6f);
            if(t<Impact+.6f){float heat=t<Impact?NativeEntranceMath.Ramp(t,0,.5f):1-NativeEntranceMath.Ramp(t,Impact,Impact+.6f);l.Add(Pod(t)+new Vector3(0,.2f,0),NativeEntranceFx.Hex(0xff8a40,30*heat),12);}
            float f=NativeEntranceMath.Pulse(t,Impact,.01f,.2f);if(f>.01f)l.Add(Landing+new Vector3(0,.8f,1.6f),NativeEntranceFx.Hex(0xffd2a0,26*f),8);
            float d=NativeEntranceMath.Pulse(t,Door,.01f,.15f);if(d>.01f)l.Add(Landing+new Vector3(0,1.1f,1.8f),NativeEntranceFx.Hex(0xfff0d8,30*d),7);
            if(t>Door)l.Add(Landing+new Vector3(0,1.4f,.1f),NativeEntranceFx.Hex(0xffdcb0,7*NativeEntranceMath.Ramp(t,Door,Door+.2f)),4);
            if(t>Impact&&t<Door&&((int)(t*5))%2==0)l.Add(Landing+new Vector3(0,2.9f,0),NativeEntranceFx.Hex(0xff2020,6),5);
            if(t>Door-.05f)l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f),3.2f);
        }
        protected override void Plan()
        {
            Blasts=new[]{new NativeEntranceBlast{At=Impact,Style=NativeBlastStyle.Quake,Position=new Vector3(Landing.X,NativeEntranceStage.FloorY+.95f,Landing.Z),Radius=2.0f,Size=.5f},
                new NativeEntranceBlast{At=Door,Style=NativeBlastStyle.Kinetic,Position=new Vector3(Landing.X,NativeEntranceStage.FloorY+1.05f,Landing.Z+.75f),Radius=.9f,Size=.4f,Tint=new NativeBlastTint(0xffffff,0xffe0b0,0xff8a40)}};
        }
    }

    /// <summary>Grid Rez: the floor grid converges, and the pilot is built voxel by voxel from the feet up behind a scan front.</summary>
    internal sealed class GridRez:NativeProceduralEntrance
    {
        protected override float Pylon(int i,float t)=>.2f+.8f*PowerOn(i,t,Done-.05f,2);
        private const float From=.8f,Done=2.4f;
        public GridRez():base("grid-rez",Done,0x5ff2c8)
        {
            SlowRate=.45f;SlowHold=.1f;SlowRecover=.25f;SlowCatchUp=.7f;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            float[] boot=NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=-.1f;NativeEntranceKeys.Set(v,Hd,.42f,0,0);NativeEntranceKeys.Set(v,C,.08f,0,0);NativeEntranceKeys.Set(v,LA,-.05f,0,.42f);NativeEntranceKeys.Set(v,RA,-.05f,0,-.42f);NativeEntranceKeys.Set(v,LE,-.08f,0,0);NativeEntranceKeys.Set(v,RE,-.08f,0,0);});
            k.Key(0,boot);k.Key(2.1f,boot);
            k.Key(Done,stand).Root(0,.88f,0,-.16f).Drop(-.05f).J(Hd,-.12f).J(C,-.08f).J(LA,-.2f,0,.35f).J(RA,-.2f,0,-.35f).J(LE,-.6f).J(RE,-.6f).ImpactKey();
            k.Key(2.75f,stand).Root(0,.88f,0,-.2f).J(Hd,0,.08f,0).J(RA,-.14f,0,-.2f).J(RE,-.4f).J(LA,-.12f,0,.2f).J(LE,-.35f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            Shots=new[]
            {
                // A crane down over the stage, the grid's light running in to the centre under the lens.
                Shot(0,NativeEntranceCut.Cut,48,44,V(2.6f,3.6f,4.4f),V(1.7f,2.4f,3.5f),V(0,.1f,0),V(0,.55f,0),0,0,roll0:.18f,roll1:.06f),
                // Low round the feet as the legs build, the hologram towering over the lens.
                Orbit(From,NativeEntranceCut.Cut,42,42,42,-18,3.3f,3.0f,.32f,.5f,V(0,.62f,0),V(0,.86f,0),3,0),
                // Rising with the scan front to the head.
                Shot(1.6f,NativeEntranceCut.Cut,40,38,V(1.2f,.8f,3.2f),V(.95f,1.45f,2.9f),V(0,.9f,0),V(0,1.25f,0),0,0),
                // Rez complete: whipped wide.
                Orbit(Done,NativeEntranceCut.Whip,40,38,-26,-12,3.1f,3.6f,1.1f,.8f,V(0,1.0f,0),V(0,1.02f,0),1,0,.16f),
                HeroShot(),
            };
        }
        // The scan front's height: from the floor to over the head between From and 2.25 s.
        private static float Front(float t)=>NativeEntranceStage.FloorY-.05f+1.9f*NativeEntranceMath.Clamp01((t-From)/(2.25f-From));
        // The lowest point of each bone's geometry when standing (the front fills a bone as it passes it).
        private static float Base(int bone)
        {
            switch(bone){case CapturedRig.LFoot:case CapturedRig.RFoot:case CapturedRig.LToe:case CapturedRig.RToe:return .02f;case CapturedRig.LKnee:case CapturedRig.RKnee:return .1f;case CapturedRig.LHip:case CapturedRig.RHip:return .34f;
                case CapturedRig.Pelvis:case CapturedRig.Body:return .53f;case CapturedRig.LHand:case CapturedRig.RHand:return .5f;case CapturedRig.LElbow:case CapturedRig.RElbow:return .62f;case CapturedRig.Spine:return .7f;
                case CapturedRig.LArm:case CapturedRig.RArm:return .86f;case CapturedRig.Chest:return .92f;case CapturedRig.LClav:case CapturedRig.RClav:return 1.05f;case CapturedRig.Neck:return 1.22f;default:return 1.32f;}
        }
        public override float Solid(float t,int bone)=>t>=2.3f?1:NativeEntranceMath.Smooth((Front(t)-Base(bone))/.28f);
        public override bool Holo(float t,out Vector3 color,out float opacity,out Vector3 offset)
        {
            color=Accent*1.4f;offset=Vector3.Zero;float a=NativeEntranceMath.Ramp(t,.55f,From)*(1-NativeEntranceMath.Ramp(t,2.15f,2.45f));opacity=.13f*a*(.8f+.2f*(float)Math.Sin(t*37));return a>.01f;
        }
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            float level=NativeEntranceMath.Ramp(t,.1f,From)*.6f+NativeEntranceMath.Pulse(t,Done,.02f,.4f)*2;
            Stage(props,t,.4f+level,.05f,Accent*.25f);
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);var white=Vector3.One;float y=Front(t);var centre=Anchor(NativeEntranceAnchor.Pelvis,t);
            // Converging light: points running in along the grid lines to the centre before the build.
            for(int i=0;i<24;i++)
            {
                if(NativeEntranceMath.Rand(4000,i)>q)continue;float born=.05f+i*.03f,a=t-born;if(a<0||a>.6f)continue;float u=NativeEntranceMath.Smooth(a/.6f);int line=(int)(NativeEntranceMath.Rand(4000,i+50)*4);float r=7.5f*(1-u);
                var p=new Vector3(line==0?r:line==1?-r:0,NativeEntranceStage.FloorY+.03f,line==2?r:line==3?-r:0)+new Vector3(line>1?NativeEntranceMath.Signed(4000,i+90)*1:0,0,line<2?NativeEntranceMath.Signed(4000,i+90)*1:0);
                fx.Streak(p,(new Vector3(0,p.Y,0)-p)*.3f,Accent*3.5f*(1-u*.5f),.04f,.45f,.08f,.9f,0,4000+i);
            }
            if(t>From-.05f&&t<2.35f)
            {
                // Voxels flying in off the floor grid to the scan front, each snapping into the body.
                for(int i=0;i<90;i++)
                {
                    if(NativeEntranceMath.Rand(4100,i)>q)continue;float born=From-.3f+i*.0167f,a=t-born;if(a<0||a>.42f)continue;float u=a/.42f;
                    float ang=NativeEntranceMath.Rand(4100,i+200)*6.283f,startR=1.2f+2.2f*NativeEntranceMath.Rand(4100,i+300);var start=new Vector3((float)Math.Round(Math.Cos(ang)*startR),NativeEntranceStage.FloorY+.02f,(float)Math.Round(Math.Sin(ang)*startR));
                    float tgtY=Front(born+.42f)-.05f;var target=new Vector3(centre.X+NativeEntranceMath.Signed(4100,i+400)*.2f,tgtY,centre.Z+NativeEntranceMath.Signed(4100,i+500)*.14f);
                    var p=Vector3.Lerp(start,target,NativeEntranceMath.Smooth(u))+new Vector3(0,(float)Math.Sin(Math.PI*u)*.6f,0);float flash=u>.9f?3:1;
                    fx.Glow(p,Vector3.Lerp(Accent*2,white*2.5f,u)*flash,0,.05f+.03f*u,0,u,4100+i);
                }
                // The scan front: broken horizontal lines sweeping round the body at its height.
                if(y<1.85f)for(int i=0;i<12;i++)
                {
                    float ang=i/12f*6.283f+t*2.2f,len=.28f+.12f*(float)Math.Sin(t*9+i);var p=new Vector3(centre.X+(float)Math.Cos(ang)*.42f,y,centre.Z+(float)Math.Sin(ang)*.42f);var tan=new Vector3(-(float)Math.Sin(ang),0,(float)Math.Cos(ang));
                    fx.Streak(p,tan*len,Accent*(2.6f*(.6f+.4f*(float)Math.Sin(t*23+i))),.012f,0,1,.5f,0,4300+i);
                }
            }
            // Rez complete: a white pop at the chest and a shower of bits.
            float pop=NativeEntranceMath.Pulse(t,Done,.01f,.14f);if(pop>.02f){fx.Glow(Anchor(NativeEntranceAnchor.Chest,Done),white*(4*pop),0,1.2f*pop,1,0,4400);}
            NativeEntranceFx.Sparks(fx,t,Done,Anchor(NativeEntranceAnchor.Chest,Done),Vector3.Zero,1,34,3.4f,.8f,4,1.5f,white*2.5f,Accent*2,.02f,4410,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float key=.25f+.75f*NativeEntranceMath.Ramp(t,2.2f,2.6f);NativeEntranceStage.Rig(l,key,Accent,1,.3f+.7f*NativeEntranceMath.Ramp(t,2.2f,2.6f));
            if(t>From-.1f&&t<2.4f)l.Add(new Vector3(0,Math.Max(.75f,Front(t)),.9f),Accent*5,3);
            float f=NativeEntranceMath.Pulse(t,Done,.01f,.25f);if(f>.01f)l.Add(Anchor(NativeEntranceAnchor.Chest,Done)+new Vector3(0,0,.6f),NativeEntranceFx.Hex(0xe0fff4,30*f),6);
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f*NativeEntranceMath.Ramp(t,1.6f,2.2f)),3.2f);
        }
    }

    /// <summary>Holo Projection: four drones project the pilot as a flickering hologram that locks solid, top down.</summary>
    internal sealed class HoloProjection:NativeProceduralEntrance
    {
        protected override float Pylon(int i,float t)=>.35f+.65f*PowerOn(i,t,Lock-.05f,1.4f);
        private const float Lock=1.6f;
        private static readonly Vector3 HoloTint=NativeEntranceFx.Hex(0x62dcff);
        public HoloProjection():base("holo-projection",Lock,0x62dcff)
        {
            SlowRate=.35f;SlowHold=.1f;SlowRecover=.25f;SlowCatchUp=.85f;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,-.12f).J(Hd,.12f);k.Key(1.35f,stand).Root(0,.88f,0,-.12f).J(Hd,.1f);
            k.Key(Lock,stand).Root(0,.88f,0,-.14f).J(C,-.1f).J(Hd,-.08f).J(LA,-.08f,0,.3f).J(RA,-.08f,0,-.3f).ImpactKey();
            k.Key(2.15f,stand).Root(0,.88f,0,-.18f).J(C,0,.16f,0).J(Hd,0,-.1f,0).J(LA,-.1f,0,.22f).J(RA,-.15f,0,-.2f).J(RE,-.5f);
            k.Key(2.7f,stand).Root(0,.88f,0,-.2f).J(RA,-.14f,0,-.2f).J(RE,-.4f).J(LA,-.12f,0,.2f).J(LE,-.35f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            Shots=new[]
            {
                // A drone settling into place, its lens waking.
                Follow(Shot(0,NativeEntranceCut.Cut,24,21,V(2.4f,1.45f,3.3f),V(2.05f,1.65f,2.85f),V(0,-.05f,0),V(0,-.08f,0),0,0),NativeEntranceAnchor.Focus),
                // A slow orbit round the flickering projection.
                Orbit(.6f,NativeEntranceCut.Cut,40,40,38,-30,3.4f,3.05f,1.0f,1.15f,V(0,1.1f,0),V(0,1.15f,0),3,0),
                // Pushing in low as it locks solid, top down.
                Shot(Lock,NativeEntranceCut.Cut,38,35,V(.8f,.7f,3.3f),V(.65f,.78f,3.0f),V(0,1.12f,0),V(0,1.2f,0),1,0),
                // Dolly out as the drones lift away.
                Orbit(2.1f,NativeEntranceCut.Blend,36,38,12,-14,3.05f,3.7f,.8f,.75f,V(0,1.15f,0),V(0,1.02f,0),0,0,.3f),
                HeroShot(),
            };
        }
        private static Vector3 Drone(int i,float t)
        {
            float a=(45+90*i)*(float)Math.PI/180,settle=NativeEntranceMath.Smooth(t/.62f),leave=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((t-1.95f-.06f*i)/.7f));
            float y=NativeEntranceMath.Lerp(4.4f,2.15f,settle)+.04f*(float)Math.Sin(t*3+i)+3.5f*leave,r=1.35f+1.5f*leave;
            return new Vector3((float)Math.Sin(a)*r,y,(float)Math.Cos(a)*r);
        }
        protected override Vector3 Focus(float t,Vector3 chest)=>Drone(0,t);
        private static float Flicker(float t){int f=(int)(t*20);float drop=NativeEntranceMath.Rand(4600,f)>.86f?.25f:1;return (.78f+.22f*(float)Math.Sin(t*31))*drop;}
        public override float Solid(float t,int bone)
        {
            if(t<Lock)return 0;float y=1.95f-2.1f*NativeEntranceMath.Clamp01((t-Lock)/.45f);float top=bone==CapturedRig.Head?1.7f:bone==CapturedRig.Neck?1.35f:bone<=CapturedRig.Chest?1.2f:bone<=CapturedRig.RHand?1.15f:bone==CapturedRig.LHip||bone==CapturedRig.RHip?.6f:bone==CapturedRig.LKnee||bone==CapturedRig.RKnee?.35f:.1f;
            return NativeEntranceMath.Smooth((top-y)/.3f);
        }
        public override bool Holo(float t,out Vector3 color,out float opacity,out Vector3 offset)
        {
            float a=NativeEntranceMath.Ramp(t,.62f,.8f)*(1-NativeEntranceMath.Ramp(t,Lock+.15f,Lock+.5f));color=HoloTint*1.5f;
            int f=(int)(t*20);offset=NativeEntranceMath.Rand(4610,f)>.82f?Local(NativeEntranceMath.Signed(4611,f)*.05f,0,0):Vector3.Zero;opacity=.3f*a*Flicker(t);return a>.01f;
        }
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,.5f,Lock);
            var dark=NativeEntranceFx.Hex(0x3e475a);float lens=NativeEntranceMath.Ramp(t,.35f,.6f)*(1-NativeEntranceMath.Ramp(t,Lock,Lock+.2f));
            for(int i=0;i<4;i++)
            {
                var p=Drone(i,t);var toward=new Vector3(-p.X,0,-p.Z);var q4=NativeEntranceMath.Yaw((float)Math.Atan2(toward.X,toward.Z));
                props.Piece(WeaponShape.Cube,false,p,q4,new Vector3(.26f,.11f,.22f),dark);props.Piece(WeaponShape.Cube,false,p+new Vector3(0,.06f,0),q4,new Vector3(.5f,.02f,.06f),dark*1.4f);
                props.Piece(WeaponShape.Sphere,true,p+Vector3.Normalize(toward)*.12f-new Vector3(0,.03f,0),Quaternion.Identity,new Vector3(.045f),HoloTint*(.6f+3*lens));
                props.Piece(WeaponShape.Cube,true,p-Vector3.Normalize(toward)*.115f,q4,new Vector3(.12f,.02f,.01f),Accent*1.5f);
                // Rotor pods on the arm tips, their under-glow pulsing with the hover.
                var side=Vector3.Transform(Vector3.UnitX,q4);float hover=1.6f+.6f*(float)Math.Sin(t*17+i);
                for(int s=-1;s<=1;s+=2){var tip=p+side*(.25f*s)+new Vector3(0,.06f,0);props.Piece(WeaponShape.Cube,false,tip,q4,new Vector3(.08f,.05f,.08f),dark*1.2f);props.Piece(WeaponShape.Sphere,true,tip-new Vector3(0,.035f,0),Quaternion.Identity,new Vector3(.035f),HoloTint*hover);}
            }
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,HoloTint);var white=Vector3.One;float beams=NativeEntranceMath.Ramp(t,.55f,.7f)*(1-NativeEntranceMath.Ramp(t,Lock,Lock+.08f));
            if(beams>.01f)
            {
                var head=Anchor(NativeEntranceAnchor.Head,t);var chest=Anchor(NativeEntranceAnchor.Chest,t);var knee=Anchor(NativeEntranceAnchor.LeftKnee,t);
                for(int i=0;i<4;i++)
                {
                    var p=Drone(i,t)+Vector3.Normalize(-new Vector3(Drone(i,t).X,0,Drone(i,t).Z))*.12f-new Vector3(0,.03f,0);float f=Flicker(t+i*.13f);
                    fx.Streak(chest,chest-p,HoloTint*(.5f*beams*f),.012f,0,1,.6f,0,4700+i*3);fx.Streak(head,head-p,HoloTint*(.35f*beams*f),.008f,0,1,.6f,0,4701+i*3);fx.Streak(knee,knee-p,HoloTint*(.3f*beams*f),.008f,0,1,.6f,0,4702+i*3);
                    fx.Glow(p,HoloTint*(2.2f*beams),0,.16f,0,0,4720+i);
                }
            }
            // Scanlines scrolling up the projection.
            float holo=NativeEntranceMath.Ramp(t,.62f,.8f)*(1-NativeEntranceMath.Ramp(t,Lock,Lock+.3f));
            if(holo>.01f){var c=Anchor(NativeEntranceAnchor.Pelvis,t);var right=RightOf(t);var fwd=ForwardOf(t);for(int i=0;i<7;i++){float y=NativeEntranceStage.FloorY+((t*.9f+i/7f)%1f)*1.8f;var p=new Vector3(c.X,y,c.Z)+fwd*.2f;fx.Streak(p+right*.32f,right*.64f,HoloTint*(1.2f*holo*Flicker(t+i)),.01f,0,1,.2f,0,4740+i);}}
            // The lock: a white flash running down the body and glitch sparks.
            float run=(t-Lock)/.45f;if(run>0&&run<1){var c=Anchor(NativeEntranceAnchor.Pelvis,t);float y=1.95f-2.1f*run;var right=RightOf(t);fx.Streak(new Vector3(c.X,y,c.Z)+right*.35f,right*.7f,white*3,.03f,0,1,0,0,4760);fx.Glow(new Vector3(c.X,y,c.Z),HoloTint*2,0,.7f,0,0,4761);}
            NativeEntranceFx.Sparks(fx,t,Lock,Anchor(NativeEntranceAnchor.Chest,Lock),Vector3.Zero,1,26,2.8f,.6f,3,1.8f,white*2.5f,HoloTint*2,.016f,4780,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float key=.3f+.7f*NativeEntranceMath.Ramp(t,Lock,Lock+.5f);NativeEntranceStage.Rig(l,key,Accent,.8f,.3f+.7f*NativeEntranceMath.Ramp(t,Lock,Lock+.5f));
            float holo=NativeEntranceMath.Ramp(t,.62f,.8f)*(1-NativeEntranceMath.Ramp(t,Lock,Lock+.4f));if(holo>.01f)l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.5f),HoloTint*(5*holo*Flicker(t)),4);
            float f=NativeEntranceMath.Pulse(t,Lock,.01f,.22f);if(f>.01f)l.Add(Anchor(NativeEntranceAnchor.Chest,Lock)+new Vector3(0,.2f,.7f),NativeEntranceFx.Hex(0xe8fbff,28*f),6);
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f*NativeEntranceMath.Ramp(t,Lock,Lock+.4f)),3.2f);
        }
    }

    /// <summary>Blackout: the lights cut; a spotlight slams on a kneeling pilot; two more slam on as it rises; the arena powers back up.</summary>
    internal sealed class Blackout:NativeProceduralEntrance
    {
        private const float Cut=.4f,Slam=1.2f,Second=2.0f,Third=2.4f,Up=2.8f;
        public Blackout():base("blackout",Slam,0xff4a6a)
        {
            SlowRate=.4f;SlowHold=.1f;SlowRecover=.22f;SlowCatchUp=.6f;
            Start=Stance(.2f,.34f,.15f,-.17f,-.12f,-.42f);Steps=new[]{Step(true,2.35f,2.7f,.16f,.07f,.1f,.06f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            float[] kneel(float head)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=-.22f;v[NativeEntranceKeys.BodyY]=-.36f;v[NativeEntranceKeys.PlantL]=1;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,B,.22f,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,LL,-1.4f,0,.15f);NativeEntranceKeys.Set(v,LK,1.9f,0,0);NativeEntranceKeys.Set(v,RL,.22f,0,-.08f);NativeEntranceKeys.Set(v,RK,1.75f,0,0);
                NativeEntranceKeys.Set(v,LA,-.95f,0,.3f);NativeEntranceKeys.Set(v,LE,-1.15f,0,0);NativeEntranceKeys.Set(v,RA,-.15f,0,-.22f);NativeEntranceKeys.Set(v,RE,-.35f,0,0);});
            k.Key(0,stand).Root(0,.88f,0,-.22f).J(Hd,.35f).Planted(1);k.Key(Cut+.05f,stand).Root(0,.88f,0,-.22f).J(Hd,.35f);
            k.Key(.95f,kneel(.5f)).PassKey();k.Key(Slam,kneel(.55f)).ImpactKey();k.Key(1.5f,kneel(.5f));
            k.Key(1.62f,kneel(-.22f)).ImpactKey();k.Key(Second,kneel(-.18f));
            k.Key(2.55f,stand).Root(0,.88f,0,-.22f).Drop(-.08f).J(B,.08f).J(Hd,-.05f).J(RA,-.2f,0,-.3f).J(RE,-.7f).J(LA,-.15f,0,.3f).J(LE,-.5f);
            k.Key(2.95f,stand).Root(0,.88f,0,-.22f).J(RA,-.14f,0,-.2f).J(RE,-.4f).J(LA,-.12f,0,.2f).J(LE,-.35f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,40,39,-20,-15,4.6f,4.2f,1.4f,1.3f,V(0,.9f,0),V(0,.85f,0),0,0),
                // Creeping in through the dark.
                Shot(Cut,NativeEntranceCut.Cut,40,38,V(.65f,.9f,3.7f),V(.48f,.8f,3.1f),V(0,.7f,0),V(0,.62f,0),3,0),
                // The slam: the kneeling pilot alone in the beam, the shaft falling from the dark above.
                Shot(Slam,NativeEntranceCut.Cut,46,43,V(1.35f,.62f,3.4f),V(1.15f,.66f,3.1f),V(0,.95f,0),V(0,.9f,0),1,0),
                // The second light: low, looking up.
                Shot(Second,NativeEntranceCut.Cut,44,42,V(-1.4f,.3f,3.2f),V(-1.2f,.32f,2.95f),V(0,.85f,0),V(0,1.0f,0),1,0),
                // The third: a three-quarter dolly out as the arena powers up.
                Orbit(Third,NativeEntranceCut.Cut,40,38,58,26,3.3f,3.8f,.9f,.8f,V(0,1.05f,0),V(0,1.02f,0),0,0),
                HeroShot(),
            };
        }
        protected override float Pylon(int i,float t)
        {
            int order=Math.Min(i,NativeEntranceStage.Pylons-1-i);float off=Cut+.025f*order,flick=t>.78f&&t<.9f&&i==3&&((int)(t*40))%2==0?.8f:0;
            float before=1-NativeEntranceMath.Ramp(t,off,off+.04f);return Math.Max(before,flick)+PowerOn(i,t,Up,0)*NativeEntranceMath.Ramp(t,Up,Up+.01f);
        }
        private static readonly Vector3 SpotTop=new Vector3(0,6.2f,0),SecondTop=new Vector3(-2.4f,5.4f,-2.6f),ThirdTop=new Vector3(2.4f,5.4f,-2.6f);
        private static float Spot(float t,float at)=>t<at?0:1.2f*NativeEntranceMath.Pulse(t,at,.015f,.1f)+NativeEntranceMath.Ramp(t,at,at+.02f);
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,.5f*(1-NativeEntranceMath.Ramp(t,Cut,Cut+.1f))+.5f*NativeEntranceMath.Ramp(t,Up,Up+.3f));
            var dark=NativeEntranceFx.Hex(0x1c2230);
            void Lamp(Vector3 at,float level,Vector3 color){var d=Vector3.Normalize(new Vector3(0,NativeEntranceStage.FloorY,0)-at);var rot=Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(-Vector3.UnitY,d)+new Vector3(1e-4f,0,0)),(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(-Vector3.UnitY,d)))));
                props.Piece(WeaponShape.Cube,false,at,rot,new Vector3(.36f,.4f,.36f),dark);props.Piece(WeaponShape.Sphere,true,at+d*.2f,Quaternion.Identity,new Vector3(.12f),color*(.1f+3*level));}
            Lamp(SpotTop,Spot(t,Slam),Vector3.One);Lamp(SecondTop,Spot(t,Second),Accent);Lamp(ThirdTop,Spot(t,Third),NativeEntranceFx.Hex(0xb76bff));
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            if(t<Cut||t>Up)Motes(fx,t,q,Accent);var floor=new Vector3(0,NativeEntranceStage.FloorY,0);float fade=1-.55f*NativeEntranceMath.Ramp(t,Up,Up+.6f);
            NativeEntranceFx.Shaft(fx,t,SpotTop,floor,.12f,1.0f,new Vector3(.05f,.05f,.055f),Spot(t,Slam)*fade,12,26,4900,q);
            NativeEntranceFx.Shaft(fx,t,SecondTop,floor+new Vector3(-.15f,0,-.1f),.12f,.85f,Accent*.05f,Spot(t,Second)*fade,10,12,4950,q);
            NativeEntranceFx.Shaft(fx,t,ThirdTop,floor+new Vector3(.15f,0,-.1f),.12f,.85f,NativeEntranceFx.Hex(0xb76bff,.05f),Spot(t,Third)*fade,10,12,4980,q);
            // Sparks spitting off two dying pylons, dust pushed off the floor by the slam, a burst as the arena powers up.
            for(int k=0;k<2;k++){var p=NativeEntranceStage.Pylon(k==0?2:9)+new Vector3(0,NativeEntranceStage.GroundY+3.3f,0);NativeEntranceFx.Sparks(fx,t,Cut+.05f*k,p,new Vector3(0,.2f,0),1.2f,16,2.2f,1.2f,9.8f,.5f,Vector3.One*3,Accent*2,.016f,5000+k*20,q,NativeEntranceStage.GroundY);}
            NativeEntranceFx.GroundDust(fx,t,Slam,floor,16,.35f,1.0f,1.6f,NativeEntranceFx.Hex(0x9aa0ae),.4f,.32f,5050,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float on=1-NativeEntranceMath.Ramp(t,Cut,Cut+.08f),back=NativeEntranceMath.Ramp(t,Up,Up+.25f);
            NativeEntranceStage.Rig(l,Math.Max(on,back),Accent,Math.Max(on,back),Math.Max(on,back)*.8f,.02f+.98f*Math.Max(on,back));
            float a=Spot(t,Slam),b=Spot(t,Second),c=Spot(t,Third),fade=1-.5f*back;
            if(a>.01f)l.Add(new Vector3(0,3.2f,.15f),NativeEntranceFx.Hex(0xf4f6ff,26*a*fade),7);
            if(b>.01f)l.Add(Vector3.Lerp(SecondTop,Vector3.Zero,.45f),Accent*(26*b*fade),8);
            if(c>.01f)l.Add(Vector3.Lerp(ThirdTop,Vector3.Zero,.45f),NativeEntranceFx.Hex(0xb76bff,26*c*fade),8);
            // The armour's own glow only once a light finds the pilot (true dark in between).
            float self=Math.Max(Math.Max(on,back),.3f*Math.Max(a,b));if(self>.01f)l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f*self),3.2f);
        }
    }

    /// <summary>Skyline Zip: rides a cable down from a far tower, lets go over the stage and lands in a skid.</summary>
    internal sealed class SkylineZip:NativeProceduralEntrance
    {
        protected override float Pylon(int i,float t)=>PowerOn(i,t,.05f,1.4f);
        private const float Release=.8f,Land=1.4f,SlideEnd=1.75f;
        private static readonly Vector3 A=new Vector3(-11,7.0f,-12),Bend=new Vector3(4.5f,2.2f,4.5f);
        private const float U0=.1f,UR=.69f;
        public SkylineZip():base("skyline-zip",Land,0xffd166)
        {
            SlowRate=.3f;SlowHold=.12f;SlowCatchUp=.9f;
            Start=Stance(.24f,.22f,.2f,-.22f,-.16f,-.5f,.1f,.0f,-.32f);
            Steps=new[]{new NativeEntrancePose.Step(true,Land,SlideEnd,Stance(.24f,.22f,.2f,0,0,0,.1f)[0],Stance(.24f,.22f,.2f,0,0,0,.1f)[1],.3f,0),new NativeEntrancePose.Step(false,Land,SlideEnd,Stance(0,0,0,-.22f,-.16f,-.5f,.1f)[3],Stance(0,0,0,-.22f,-.16f,-.5f,.1f)[4],-.4f,0),
                Step(true,2.4f,2.75f,.16f,.07f,.1f,.06f),Step(false,2.55f,2.95f,-.18f,-.1f,-.42f,.07f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();float yaw=(float)Math.Atan2(Bend.X-A.X,Bend.Z-A.Z);
            float[] hang=NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=yaw;v[NativeEntranceKeys.PlantL]=0;v[NativeEntranceKeys.PlantR]=0;NativeEntranceKeys.Set(v,B,-.05f,0,0);NativeEntranceKeys.Set(v,Hd,-.1f,0,0);
                NativeEntranceKeys.Set(v,LA,-2.95f,0,.12f);NativeEntranceKeys.Set(v,LE,-.25f,0,0);NativeEntranceKeys.Set(v,RA,-2.95f,0,-.12f);NativeEntranceKeys.Set(v,RE,-.25f,0,0);NativeEntranceKeys.Set(v,LL,-.6f,0,.08f);NativeEntranceKeys.Set(v,LK,.55f,0,0);NativeEntranceKeys.Set(v,RL,-.35f,0,-.08f);NativeEntranceKeys.Set(v,RK,.85f,0,0);});
            k.Key(0,hang);k.Key(Release-.05f,hang);
            k.Key(1.0f,stand).Root(0,.88f,0,.4f).Planted(0).J(LA,-2.0f,0,.6f).J(RA,-1.9f,0,-.7f).J(LE,-.4f).J(RE,-.4f).J(LL,-1.1f,0,.1f).J(LK,1.5f).J(RL,-.9f,0,-.1f).J(RK,1.6f).J(B,.2f).PassKey();
            k.Key(1.28f,stand).Root(0,.88f,0,.2f).Planted(0).J(LA,-.6f,0,1.1f).J(RA,-.8f,0,-1.0f).J(LL,-.5f,0,.12f).J(LK,.7f).J(RL,-.35f,0,-.1f).J(RK,.9f).J(B,.25f).PassKey();
            k.Key(Land,stand).Root(0,.88f,0,.1f).Drop(-.38f).J(B,.45f).J(Hd,.05f).J(LA,-.3f,0,1.15f).J(LE,-.3f).J(RA,-.55f,0,-.3f).J(RE,-.2f).ImpactKey();
            k.Key(SlideEnd,stand).Root(0,.88f,0,0).Drop(-.4f).J(B,.5f).J(Hd,-.05f).J(LA,-.3f,0,1.0f).J(LE,-.4f).J(RA,-.6f,0,-.3f).J(RE,-.2f);
            k.Key(2.1f,stand).Root(0,.88f,0,-.1f).Drop(-.3f).J(B,.35f).J(Hd,-.2f).J(LA,-.2f,0,.7f).J(RA,-.4f,0,-.4f);
            k.Key(2.75f,stand).Root(0,.88f,0,-.2f).J(RA,-.14f,0,-.2f).J(RE,-.4f).J(LA,-.12f,0,.2f).J(LE,-.35f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            RootPath=(t,r)=>Body(t,r);
            Shots=new[]
            {
                // A long lens from the stage up the cable at the rider against the skyline.
                Follow(Shot(0,NativeEntranceCut.Cut,17,24,V(2.1f,1.0f,3.3f),V(2.0f,1.05f,3.2f),V(0,.55f,0),V(0,.4f,0),2,0),NativeEntranceAnchor.Pelvis),
                // Riding alongside.
                Follow(Orbit(.5f,NativeEntranceCut.Cut,48,46,80,62,3.7f,3.5f,.5f,.4f,V(0,.5f,0),V(0,.4f,0),3,1,face:.6f,trackY:1),NativeEntranceAnchor.Pelvis),
                // Whipped down to where it will land.
                Shot(1.1f,NativeEntranceCut.Whip,48,44,V(-1.5f,.3f,3.6f),V(-1.3f,.3f,3.3f),V(0,1.3f,0),V(0,.8f,0),1,0,.16f),
                // The skid, slowed: a low orbit through the dust.
                Orbit(Land,NativeEntranceCut.Cut,42,40,-30,8,3.4f,3.1f,.4f,.5f,V(0,.62f,.2f),V(0,.78f,0),1,0),
                // Dolly out, the cable overhead.
                Orbit(2.2f,NativeEntranceCut.Blend,38,37,8,-16,3.15f,3.8f,.6f,.9f,V(0,.9f,0),V(0,1.08f,0),0,0,.3f),
                HeroShot(),
            };
        }
        private static Vector3 Cable(float u)=>Vector3.Lerp(A,Bend,u);
        private static float Trolley(float t){float x=NativeEntranceMath.Clamp01(t/Release);return U0+(UR-U0)*(1-(float)Math.Pow(1-x,1.7f));}
        // The root: hanging under the trolley (swinging forward as it brakes), a ballistic drop from the release to the landing,
        // then a skid that slows to a stop.
        private static Vector3 Body(float t,Vector3 keyed)
        {
            var hang=new Vector3(0,-1.02f,0);var dir=Vector3.Normalize(Bend-A);
            if(t<=Release){float swing=.35f*NativeEntranceMath.Ramp(t,.4f,Release);return Cable(Trolley(t))+hang+new Vector3(dir.X,0,dir.Z)*swing;}
            var r0=Cable(UR)+hang+new Vector3(dir.X,0,dir.Z)*.35f;var landAt=new Vector3(0,.88f,-.32f);float a=Math.Min(t,Land)-Release,span=Land-Release;
            if(t<=Land){float vy=((landAt.Y-r0.Y)+4.9f*span*span)/span;var p=Vector3.Lerp(r0,landAt,a/span);p.Y=r0.Y+vy*a-4.9f*a*a;return p;}
            float s=NativeEntranceMath.EaseOut((t-Land)/(SlideEnd-Land));var slid=landAt+new Vector3(0,0,.32f*s);return new Vector3(slid.X,keyed.Y,slid.Z);
        }
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            // Both hands on the trolley's bar until the release.
            float w=1-NativeEntranceMath.Ramp(t,Release-.04f,Release+.06f);if(w<=.001f)return;var bar=Cable(Trolley(Math.Min(t,Release)))-new Vector3(0,.2f,0);var side=Vector3.Normalize(Vector3.Cross(Bend-A,Vector3.UnitY));
            var grip=Quaternion.CreateFromYawPitchRoll((float)Math.Atan2(Bend.X-A.X,Bend.Z-A.Z),(float)Math.PI,0);
            NativeEntrancePose.Reach(pose,root,rotation,true,bar-side*.13f,grip,w);NativeEntrancePose.Reach(pose,root,rotation,false,bar+side*.13f,grip,w);
        }
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,.55f,Land);
            var d=Bend-A;float len=d.Length();var rot=Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.Atan2(d.X,d.Z))*Quaternion.CreateFromAxisAngle(Vector3.UnitX,-(float)Math.Asin(d.Y/len));
            props.Piece(WeaponShape.Cube,false,(A+Bend)*.5f,rot,new Vector3(.035f,.035f,len),NativeEntranceFx.Hex(0x30384a));
            props.Piece(WeaponShape.Cube,true,(A+Bend)*.5f+new Vector3(0,.022f,0),rot,new Vector3(.01f,.01f,len),Accent*.6f);
            var metal=NativeEntranceFx.Hex(0x232a3a);
            props.Piece(WeaponShape.Cube,false,new Vector3(Bend.X,(Bend.Y+NativeEntranceStage.GroundY)*.5f,Bend.Z),NativeEntranceMath.Yaw(.8f),new Vector3(.28f,Bend.Y-NativeEntranceStage.GroundY+.2f,.28f),metal);
            props.Piece(WeaponShape.Cube,false,Bend+new Vector3(0,.12f,0),NativeEntranceMath.Yaw(.8f),new Vector3(.5f,.24f,.5f),metal*1.2f);
            props.Piece(WeaponShape.Sphere,true,Bend+new Vector3(0,.32f,0),Quaternion.Identity,new Vector3(.06f),((int)(t*3))%2==0?new Vector3(3,.2f,.1f):new Vector3(.4f,.03f,.02f));
            props.Piece(WeaponShape.Cube,false,A+new Vector3(0,-.4f,0),NativeEntranceMath.Yaw(.8f),new Vector3(1.4f,.8f,1.4f),metal);
            // The trolley: a block on the cable with a bar hanging under it.
            var tr=Cable(Trolley(t));props.Piece(WeaponShape.Cube,false,tr,rot,new Vector3(.12f,.14f,.3f),metal*1.6f);props.Piece(WeaponShape.Sphere,true,tr+new Vector3(0,.09f,0),Quaternion.Identity,new Vector3(.035f),Accent*3);
            var side=Vector3.Normalize(Vector3.Cross(d,Vector3.UnitY));props.Piece(WeaponShape.Cube,false,tr-new Vector3(0,.1f,0),Quaternion.Identity,new Vector3(.02f,.2f,.02f),metal);
            props.Piece(WeaponShape.Cube,false,tr-new Vector3(0,.2f,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.Atan2(side.X,side.Z)),new Vector3(.02f,.025f,.4f),metal*1.6f);
        }
        public override int Boxes(float t,Span<Vector4> into){int n=NativeEntranceStage.PylonBoxes(into,0);into[n++]=new Vector4(Bend.X,Bend.Y*.5f,Bend.Z,0);into[n++]=new Vector4(.4f,Bend.Y*.5f+.3f,.4f,0);return n;}
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;var dir=Vector3.Normalize(Bend-A);
            // Sparks spraying off the trolley's wheels, thick at speed.
            for(int i=0;i<40;i++)
            {
                if(NativeEntranceMath.Rand(5200,i)>q)continue;float born=i*Release/40,a=t-born;if(a<0||a>.5f)continue;float speed=(Trolley(born+.01f)-Trolley(born))*100*(Bend-A).Length();if(speed<1.5f)continue;
                var v=-dir*speed*.3f+NativeEntranceMath.Direction(5200,i)*1.2f+new Vector3(0,1.2f,0);var p=Cable(Trolley(born))+v*a+new Vector3(0,-4.9f*a*a,0);float u=a/.5f;
                fx.Streak(p,v+new Vector3(0,-9.8f*a,0),Vector3.Lerp(white*3,Accent*2,u)*(1-u),.014f,.03f,.03f,.8f,u,5200+i);
            }
            // Speed lines round the rider.
            if(t<Release){var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);for(int i=0;i<12;i++){if(NativeEntranceMath.Rand(5300,i)>q)continue;float ph=(t*3+NativeEntranceMath.Rand(5300,i+20))%1f;var off=new Vector3(NativeEntranceMath.Signed(5300,i+30)*.5f,NativeEntranceMath.Signed(5300,i+40)*.7f,NativeEntranceMath.Signed(5300,i+50)*.5f);fx.Streak(pelvis+off-dir*(ph-.5f)*1.5f,dir*12,Accent*(.6f*(float)Math.Sin(Math.PI*ph)),.01f,.4f,.04f,.9f,0,5300+i);}}
            NativeEntranceFx.Sparks(fx,t,Release,Cable(UR),new Vector3(0,.3f,0),1,18,2.6f,.6f,9.8f,.8f,white*3,Accent*2,.016f,5350,q,NativeEntranceStage.FloorY);
            // The skid: dust thrown up along the slide, and the landing's crater.
            for(int k=0;k<10;k++){if(NativeEntranceMath.Rand(5400,k)>q)continue;float born=Land+k*.035f,a=t-born;if(a<0||a>1.3f)continue;float u=a/1.3f;var at=Anchor(NativeEntranceAnchor.Root,born);
                fx.Smoke(new Vector3(at.X+NativeEntranceMath.Signed(5400,k)*.25f,NativeEntranceStage.FloorY+.12f+.3f*u,at.Z+NativeEntranceMath.Signed(5401,k)*.15f),NativeEntranceFx.Hex(0x8a90a0),.5f*(1-u)*NativeEntranceMath.Smooth(u*8),.35f+.6f*u,u,5400+k,NativeEntranceStage.FloorY);}
            NativeEntranceFx.GroundDust(fx,t,Land,new Vector3(0,NativeEntranceStage.FloorY,-.32f),18,.3f,1.6f,1.6f,NativeEntranceFx.Hex(0x8790a2),.5f,.38f,5450,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);float f=NativeEntranceMath.Pulse(t,Land,.01f,.2f);if(f>.01f)l.Add(new Vector3(0,.6f,.3f),NativeEntranceFx.Hex(0xfff2d0,30*f),6);
            if(t<Release+.2f)l.Add(Cable(Trolley(Math.Min(t,Release)))+new Vector3(0,-.2f,0),Accent*(6*(1-NativeEntranceMath.Ramp(t,Release,Release+.2f))),5);
        }
    }

    /// <summary>Overclock: a chip slammed into the chest, circuits lighting up every limb, a power surge into a wide stance.</summary>
    internal sealed class Overclock:NativeProceduralEntrance
    {
        protected override float Pylon(int i,float t){float charge=NativeEntranceMath.Ramp(t,Install,Surge);return t<Surge?.3f+.7f*charge*(1+.4f*(float)Math.Sin(t*30+i)):PowerOn(i,t,Surge-.04f,1.6f);}
        private const float Install=.8f,Surge=1.6f;
        public Overclock():base("overclock",Surge,0xffc14a)
        {
            SlowRate=.28f;SlowHold=.14f;SlowRecover=.3f;SlowCatchUp=.95f;
            Start=Stance(.17f,.05f,.1f,-.18f,-.08f,-.42f);Steps=new[]{Step(true,1.42f,1.6f,.27f,.08f,.2f,.06f),Step(false,1.45f,1.62f,-.28f,-.1f,-.5f,.06f),Step(true,2.7f,3.0f,.16f,.07f,.1f,.05f),Step(false,2.8f,3.1f,-.18f,-.1f,-.42f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,-.2f).J(RA,-1.9f,0,-.12f).J(RE,-1.55f).J(Hd,.08f).J(LA,-.1f,0,.15f).J(LE,-.3f);
            k.Key(.55f,stand).Root(0,.88f,0,-.25f).J(RA,-2.3f,0,-.45f).J(RE,-1.85f).J(C,0,.22f,0).J(Hd,-.05f).J(LA,-.1f,0,.2f).J(LE,-.4f);
            k.Key(Install,stand).Root(0,.88f,0,-.2f).Drop(-.06f).J(RA,-.95f,0,-.05f).J(RE,-2.2f).J(C,.12f,-.1f,0).J(Hd,.28f).J(LA,-.2f,0,.25f).ImpactKey();
            k.Key(1.05f,stand).Root(0,.88f,0,-.2f).Drop(-.18f).J(B,.2f).J(Hd,.32f).J(RA,-.95f,0,-.05f).J(RE,-2.2f).J(LA,-.9f,0,.2f).J(LE,-2.0f);
            k.Key(1.48f,stand).Root(0,.88f,0,-.2f).Drop(-.27f).J(B,.3f).J(Hd,.38f).J(RA,-1.0f,0,-.1f).J(RE,-2.25f).J(LA,-.95f,0,.25f).J(LE,-2.1f).PassKey();
            k.Key(Surge,stand).Root(0,.88f,0,-.2f).Drop(-.1f).J(B,-.16f).J(C,-.12f).J(Hd,-.3f).J(LA,-.4f,0,.95f).J(LE,-.1f).J(RA,-.4f,0,-.95f).J(RE,-.1f).ImpactKey();
            k.Key(2.05f,stand).Root(0,.88f,0,-.2f).Drop(-.12f).J(B,-.08f).J(Hd,-.15f).J(LA,-.35f,0,.8f).J(LE,-.25f).J(RA,-.35f,0,-.8f).J(RE,-.25f);
            k.Key(2.65f,stand).Root(0,.88f,0,-.22f).Drop(-.06f).J(LA,-.15f,0,.38f).J(LE,-.45f).J(RA,-.15f,0,-.38f).J(RE,-.45f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            Shots=new[]
            {
                // The chip, close, on a long lens.
                Follow(Shot(0,NativeEntranceCut.Cut,28,24,V(1.7f,.95f,2.0f),V(1.5f,.98f,1.8f),V(0,.08f,0),V(0,.1f,0),1,0),NativeEntranceAnchor.RightHand),
                // The install: on the chest.
                Shot(Install,NativeEntranceCut.Cut,36,33,V(.65f,1.12f,2.7f),V(.52f,1.1f,2.4f),V(0,1.1f,0),V(0,1.08f,0),1,0),
                // Charging: a fast low orbit.
                Orbit(1.2f,NativeEntranceCut.Cut,44,44,50,-38,3.0f,2.8f,.36f,.34f,V(0,1.0f,0),V(0,.95f,0),3,0),
                // The surge, slowed: wide in front.
                Orbit(Surge,NativeEntranceCut.Cut,42,38,-6,-20,3.4f,3.1f,.9f,.85f,V(0,1.0f,0),V(0,1.0f,0),1,0),
                Orbit(2.4f,NativeEntranceCut.Blend,38,37,-20,-10,3.15f,3.7f,.85f,.72f,V(0,1.0f,0),V(0,1.02f,0),0,0,.3f),
                HeroShot(),
            };
        }
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            // The chip pressed into the chest slot.
            float w=NativeEntranceMath.Ramp(t,Install-.12f,Install)*(1-NativeEntranceMath.Ramp(t,1.25f,1.5f));if(w<=.001f)return;pose.World();
            var target=NativeEntrancePose.Point(pose,root,rotation,CapturedRig.Chest,new Vector3(-.02f,.14f,.24f));NativeEntrancePose.Reach(pose,root,rotation,false,target,NativeEntrancePose.WorldRotation(pose,rotation,CapturedRig.RHand),w);
        }
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,.5f,Surge);
            // The chip: in the hand, then in the chest slot.
            if(t<Install+.02f){var h=Anchor(NativeEntranceAnchor.RightHand,t);props.Piece(WeaponShape.Cube,true,h+new Vector3(0,.07f,0),NativeEntranceMath.Yaw(t*.8f),new Vector3(.07f,.07f,.015f),Accent*(2.4f+.6f*(float)Math.Sin(t*12)));}
            else{var c=Anchor(NativeEntranceAnchor.Chest,t)+ForwardOf(t)*.17f+new Vector3(0,.05f,0);props.Piece(WeaponShape.Cube,true,c,NativeEntranceMath.Yaw(Heading(t)),new Vector3(.07f,.07f,.02f),Accent*(3+2*NativeEntranceMath.Ramp(t,Install,Surge)));}
        }
        private static readonly NativeEntranceAnchor[][] Circuits=
        {
            new[]{NativeEntranceAnchor.Chest,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.LeftHand},
            new[]{NativeEntranceAnchor.Chest,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.RightHand},
            new[]{NativeEntranceAnchor.Chest,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.LeftFoot},
            new[]{NativeEntranceAnchor.Chest,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.RightKnee,NativeEntranceAnchor.RightFoot},
            new[]{NativeEntranceAnchor.Chest,NativeEntranceAnchor.Head},
        };
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);var white=Vector3.One;var gold=Accent*2.4f;
            if(t<Install){var h=Anchor(NativeEntranceAnchor.RightHand,t)+new Vector3(0,.07f,0);fx.Glow(h,gold*.8f,0,.22f,0,0,5600);Trail(fx,NativeEntranceAnchor.RightHand,t,.5f,Install,.12f,gold,Accent,.03f,5610);}
            float click=NativeEntranceMath.Pulse(t,Install,.01f,.12f);var chest=Anchor(NativeEntranceAnchor.Chest,t)+ForwardOf(t)*.17f;if(click>.02f)NativeEntranceFx.Glint(fx,chest,white*(3*click),.4f*click+.05f,RightOf(t),Vector3.UnitY,5620);
            // The circuits: pulses racing from the chest out along every limb, three waves.
            if(t>Install&&t<Surge+.4f)for(int c=0;c<Circuits.Length;c++)for(int wave=0;wave<3;wave++)
            {
                float born=Install+.02f+wave*.25f,a=t-born;if(a<0||a>.45f)continue;var path=Circuits[c];float u=a/.38f*(path.Length-1);int seg=Math.Min(path.Length-2,(int)u);float f=Math.Min(1,u-seg);
                var p=Vector3.Lerp(Anchor(path[seg],t),Anchor(path[seg+1],t),f);var prev=Vector3.Lerp(Anchor(path[seg],t),Anchor(path[seg+1],t),Math.Max(0,f-.35f));
                fx.Streak(p,p-prev,Vector3.Lerp(white*3,gold,wave/2f)*(1-a/.45f),.025f,0,1,.3f,0,5630+c*3+wave);fx.Glow(p,gold*(.6f*(1-a/.45f)),0,.12f,0,0,5660+c*3+wave);
            }
            // Charge: motes spiralling in.
            for(int i=0;i<40;i++)
            {
                if(NativeEntranceMath.Rand(5700,i)>q)continue;float born=1.0f+i*.015f,a=t-born;if(a<0||a>.5f||born>Surge)continue;float u=a/.5f,ang=NativeEntranceMath.Rand(5700,i+50)*6.283f+u*4,r=1.3f*(1-u)+.1f,y=.3f+1.4f*NativeEntranceMath.Rand(5700,i+90);
                var c=Anchor(NativeEntranceAnchor.Pelvis,t);fx.Glow(new Vector3(c.X+(float)Math.Cos(ang)*r,NativeEntranceStage.FloorY+y,c.Z+(float)Math.Sin(ang)*r),gold*(.4f+u),0,.05f,0,u,5700+i);
            }
            // The aura: every joint flares at the surge and pulses down on the beat.
            float aura=NativeEntranceMath.Pulse(t,Surge,.02f,.5f)+.35f*NativeEntranceMath.Pulse(t,2.0f,.02f,.2f)+.25f*NativeEntranceMath.Pulse(t,2.4f,.02f,.2f)+.15f*NativeEntranceMath.Pulse(t,2.8f,.02f,.2f);
            if(aura>.02f)for(int j=0;j<(int)NativeEntranceAnchor.Count;j++){var which=(NativeEntranceAnchor)j;if(which==NativeEntranceAnchor.Root||which==NativeEntranceAnchor.Forward||which==NativeEntranceAnchor.Focus||which==NativeEntranceAnchor.LeftTip||which==NativeEntranceAnchor.RightTip)continue;fx.Glow(Anchor(which,t),gold*(.7f*aura),0,.3f,0,0,5800+j);}
            NativeEntranceFx.Sparks(fx,t,Surge,Anchor(NativeEntranceAnchor.Chest,Surge),Vector3.Zero,1,36,4.4f,.8f,4,1.4f,white*3,gold,.02f,5850,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.GroundDust(fx,t,Surge,new Vector3(0,NativeEntranceStage.FloorY,0),22,.35f,2.0f,1.8f,NativeEntranceFx.Hex(0x8f8a80),.5f,.42f,5900,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float charge=NativeEntranceMath.Ramp(t,Install,Surge);NativeEntranceStage.Rig(l,.55f+.45f*NativeEntranceMath.Ramp(t,Surge,Surge+.3f),Accent,.6f+.4f*charge,.5f+.5f*NativeEntranceMath.Ramp(t,Surge,Surge+.3f));
            if(t<Install)l.Add(Anchor(NativeEntranceAnchor.RightHand,t)+new Vector3(0,.1f,.15f),Accent*3,2.5f);
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)+ForwardOf(t)*.4f,Accent*(1+8*charge*(t<Surge?1:0)+12*NativeEntranceMath.Pulse(t,Surge,.01f,.5f)),4);
            float f=NativeEntranceMath.Pulse(t,Surge,.01f,.25f);if(f>.01f)l.Add(Anchor(NativeEntranceAnchor.Chest,Surge)+new Vector3(0,.3f,.6f),NativeEntranceFx.Hex(0xfff2c8,45*f),7);
        }
        protected override void Plan()=>Blasts=new[]{new NativeEntranceBlast{At=Surge,Style=NativeBlastStyle.Nova,Position=Anchor(NativeEntranceAnchor.Chest,Surge),Radius=1.5f,Size=.45f}};
    }
}
