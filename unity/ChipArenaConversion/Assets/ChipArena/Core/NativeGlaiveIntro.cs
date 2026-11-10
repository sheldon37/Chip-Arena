using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Crescent Return", the Glaive's entrance (owner 2026-11-09: "create a very well animated intro"; docs/GLAIVE.md "The intro"). 7 s,
    /// the name at 5.2 s. The stage is dark. The thrown ring streaks in from the left, a spinning light, and ricochets off two pylons in
    /// showers of sparks. On its way back the pilot drops in from above and catches it in the air (slow motion), turns a whirl as it
    /// falls and lands in a crouch. Rising, it flings the ring up: it comes down to orbit the pilot twice, carving a circle of light into
    /// the floor while the stage powers up. Caught again, one big forehand: a crescent of light hangs where it cut and the floor circle
    /// flashes up. Then the hero: the ring held upright beside the face, its edge lit, under the name.
    /// The body is keyed here; while the ring is in the hand its arm and the chest are the Glaive's keyed solver (Core
    /// NativeGlaiveMotion.Intro, on the model in Runtime NativeDogMalletPresentation.GlaiveIntro); while it is out the runtime draws it
    /// where Ring says. Presentation only: pure functions of the entrance clock, thinned by `quality`, every beat kept on Reduced.
    /// </summary>
    public sealed class NativeGlaiveIntro:NativeProceduralEntrance
    {
        public const string Id="glaive-crescent",Name="Crescent Return";
        public const float Total=7f,CardAt=5.2f,End=6.7f;
        public const float EnterAt=.35f,Hit1=.8f,Hit2=1.25f,DropAt=1.25f,CatchAt=1.7f,LandAt=2.15f,FlingAt=2.65f,OrbitFrom=2.9f,OrbitTo=3.45f,ReturnAt=3.65f,CoilAt=3.8f,SlashAt=4.05f,StandAt=4.65f;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        /// <summary>The orbit's radius and height, its turns.</summary>
        public const float OrbitRadius=1.7f,OrbitHeight=.45f,OrbitTurns=2;
        public static readonly Vector3 From=new Vector3(-6.6f,2.1f,1.3f),Pylon1=new Vector3(3.1f,1.6f,-2.3f),Pylon2=new Vector3(-2.9f,1.35f,-1.6f);
        /// <summary>Where the ring meets the hand at the catch (raised over the right shoulder in the air) and at the return (ahead, right).</summary>
        public static readonly Vector3 CatchPoint=Local(-.12f,3.1f,.25f),ReturnPoint=Local(-.35f,1.35f,.55f),FlingPoint=Local(-.05f,2.35f,.15f);
        private static readonly Vector3 Cool=NativeEntranceFx.Hex(0x7ff3ff),White=new Vector3(1,1,1),Deep=NativeEntranceFx.Hex(0x1a4a66),Dust=new Vector3(.42f,.4f,.46f);
        public NativeGlaiveIntro():base(Id,CatchAt,0x4fe8ff)
        {
            SlowRate=.3f;SlowHold=.12f;SlowRecover=.25f;SlowCatchUp=.6f;
            Start=Stance(.18f,.06f,.12f,-.2f,-.08f,-.38f);
            Steps=new[]{Step(true,StandAt-.12f,StandAt+.12f,.2f,.1f,.05f,.05f),Step(false,StandAt-.02f,StandAt+.2f,-.2f,-.06f,-.35f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // Out of sight above the stage until the drop.
            k.Key(0,stand).Root(0,4.2f,.1f,Facing).Planted(0).J(LA,-2.6f,0,.4f).J(RA,-2.6f,0,-.4f);
            k.Key(DropAt-.02f,stand).Root(0,4.2f,.1f,Facing).Planted(0).J(LA,-2.6f,0,.4f).J(RA,-2.6f,0,-.4f);
            // Falling in, stretched long, reaching up for the ring.
            k.Key(DropAt+.12f,stand).Root(0,3.0f,.08f,Facing).Planted(0).Spin(-.1f,0).J(Hd,-.3f).J(LL,-.15f,0,.05f).J(LK,.25f).J(RL,.05f,0,-.05f).J(RK,.1f).J(LA,-1.6f,0,.7f).J(LE,-.2f).PassKey();
            // The catch, high in the air (the slow motion): knees drawn up.
            k.Key(CatchAt,stand).Root(0,2.05f,.05f,Facing).Planted(0).J(Hd,-.25f).J(LL,-.7f,0,.1f).J(LK,1.0f).J(RL,-.4f,0,-.1f).J(RK,.7f).J(LA,-1.1f,0,.9f).J(LE,-.35f).ImpactKey();
            // The whirl as it falls (the turn is the arm's keys'), tucked.
            k.Key(2.0f,stand).Root(0,1.3f,0,Facing).Planted(0).J(B,.12f).J(LL,-1.05f,0,.12f).J(LK,1.55f).J(RL,-.95f,0,-.12f).J(RK,1.45f).J(LA,-.7f,0,1.05f).J(LE,-.3f);
            // The landing: a deep crouch, the free arm out.
            k.Key(LandAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.3f).J(B,.3f).J(Sp,.1f).J(Hd,-.12f).J(LA,-.45f,0,1.15f).J(LE,-.25f).J(LL,-.95f,0,.12f).J(LK,1.55f).J(RL,-.85f,0,-.12f).J(RK,1.45f).ImpactKey();
            k.Key(LandAt+.3f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.08f).J(B,.08f).J(LA,-.35f,0,.6f).J(LE,-.35f);
            // Up on the toes for the fling, looking up after it.
            k.Key(FlingAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(.04f).J(Hd,-.35f).J(LA,-.25f,0,.35f).J(LE,-.2f);
            // Watching the ring round, the free hand following it.
            k.Key(OrbitFrom,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(Hd,.08f,.35f,0).J(C,0,.25f,0).J(LA,-.4f,0,.55f).J(LE,-.5f);
            k.Key(OrbitTo,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(Hd,.05f,-.35f,0).J(C,0,-.25f,0).J(LA,-.45f,0,.45f).J(LE,-.55f);
            // The forehand: sunk into it, then planted wide through the cut.
            k.Key(CoilAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.1f).J(B,.1f).J(LA,-.3f,0,.5f).J(LE,-.6f).J(LL,-.35f,0,.12f).J(LK,.5f).J(RL,-.3f,0,-.12f).J(RK,.45f);
            k.Key(SlashAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.14f).J(B,.14f).J(LA,-.6f,0,.9f).J(LE,-.3f).J(LL,-.38f,0,.14f).J(LK,.55f).J(RL,-.32f,0,-.14f).J(RK,.5f).ImpactKey();
            k.Key(SlashAt+.35f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.08f).J(B,.06f).J(LA,-.4f,0,.6f).J(LE,-.4f);
            // The hero: standing tall, the free hand loose, the head turned a little to the ring.
            k.Key(StandAt,stand).Root(0,.88f,0,Facing+.08f).Planted(1).Drop(-.02f).J(C,0,.1f,0).J(Hd,-.04f,-.12f,0).J(LA,-.12f,0,.22f).J(LE,-.3f);
            k.Key(Total,stand).Root(0,.88f,0,Facing+.08f).Planted(1).Drop(-.02f).J(C,0,.12f,0).J(Hd,-.05f,-.14f,0).J(LA,-.12f,0,.22f).J(LE,-.3f);Keys=k;
            Shots=new[]
            {
                // The dark stage, the camera panning after the ring as it crosses to the first pylon.
                Shot(0,NativeEntranceCut.Cut,50,46,V(.6f,1.0f,5.6f),V(1.3f,1.15f,5.0f),V(-4.5f,1.9f,.5f),V(2.6f,1.6f,-2f),1,0),
                // A whip round after it to the second.
                Shot(Hit1-.04f,NativeEntranceCut.Whip,44,40,V(2.2f,1.0f,2.4f),V(1.7f,1.1f,2.1f),V(3f,1.6f,-2.2f),V(-2.6f,1.4f,-1.6f),1,0,.1f),
                // Low, looking up as it comes back and the pilot drops into the catch.
                Shot(Hit2-.04f,NativeEntranceCut.Cut,58,52,V(-1.4f,.35f,2.7f),V(-1.2f,.4f,2.35f),V(-1.4f,1.8f,-.8f),V(0,2.7f,0),1,0),
                // The landing, low and close in front.
                Shot(LandAt-.05f,NativeEntranceCut.Cut,46,42,V(-.9f,.45f,2.5f),V(-.75f,.5f,2.2f),V(0,.95f,0),V(0,1.0f,0),1,0),
                // High and wide over the orbit and its circle.
                Shot(FlingAt-.1f,NativeEntranceCut.Cut,56,60,V(.2f,4.4f,3.7f),V(.7f,4.8f,4f),V(0,.5f,0),V(0,.3f,0),1,0),
                // The forehand from its side.
                Shot(ReturnAt-.1f,NativeEntranceCut.Cut,42,38,V(-2f,1.25f,1.9f),V(-1.7f,1.3f,1.6f),V(0,1.25f,.2f),V(0,1.2f,.35f),1,0),
                // The hero, the ring by the face.
                Shot(StandAt,NativeEntranceCut.Blend,40,36,V(-1.15f,1.05f,3.8f),V(-.8f,1.08f,3.4f),V(0,1.2f,0),V(-.05f,1.22f,0),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.02f,3.55f),V(-.66f,1.06f,3.3f),V(-.1f,1.25f,0),V(-.12f,1.27f,0),1,0),
            };
        }
        /// <summary>Unseen until it drops in.</summary>
        public override float Solid(float t,int bone)=>t<DropAt?0:1;
        protected override Vector3 Focus(float t,Vector3 chest)=>t<DropAt&&Ring(t,out var at,out _)?at:chest;

        // ---- the ring ----
        /// <summary>Whether the ring is in the hand at `t` (between the catch and the fling, and from the return).</summary>
        public static bool InHand(float t)=>t>=CatchAt&&t<FlingAt||t>=ReturnAt;
        /// <summary>Where the ring is while it is out (stage frame), and which way it travels; false while it is in the hand or not yet in.</summary>
        public static bool Ring(float t,out Vector3 at,out Vector3 travel)
        {
            at=travel=Vector3.Zero;if(t<EnterAt||InHand(t))return false;
            at=RingRaw(t);var before=RingRaw(t-.01f);travel=at-before;if(travel.LengthSquared()<1e-10f)travel=Vector3.UnitZ;else travel=Vector3.Normalize(travel);return true;
        }
        private static Vector3 RingRaw(float t)
        {
            if(t<FlingAt)
            {
                // In from the left to the first pylon, across to the second, then up and back to the falling hand: each leg a little arc.
                if(t<Hit1)return Leg(From,Pylon1,(t-EnterAt)/(Hit1-EnterAt),.35f);
                if(t<Hit2)return Leg(Pylon1,Pylon2,(t-Hit1)/(Hit2-Hit1),.25f);
                return Leg(Pylon2,CatchPoint,NativeEntranceMath.Smooth((t-Hit2)/(CatchAt-Hit2))*.85f+(t-Hit2)/(CatchAt-Hit2)*.15f,.6f);
            }
            // Flung up, down onto its orbit, twice round, then up and in to the hand.
            float a=OrbitAngle(t);var ring=new Vector3((float)Math.Sin(a)*OrbitRadius,OrbitHeight,(float)Math.Cos(a)*OrbitRadius);
            if(t<OrbitFrom){float u=NativeEntranceMath.Smooth((t-FlingAt)/(OrbitFrom-FlingAt));var up=FlingPoint+new Vector3(0,.6f*(float)Math.Sin(u*Math.PI),0);return Vector3.Lerp(up,ring,u);}
            if(t<OrbitTo)return ring;
            float v=NativeEntranceMath.Smooth((t-OrbitTo)/(ReturnAt-OrbitTo));return Vector3.Lerp(ring,ReturnPoint,v)+new Vector3(0,.35f*(float)Math.Sin(v*Math.PI),0);
        }
        /// <summary>The orbit's angle about the pilot at `t` (from the front, turning to the pilot's left), OrbitTurns turns from the fling.</summary>
        public static float OrbitAngle(float t)=>(float)(Math.PI*2*OrbitTurns*NativeEntranceMath.Clamp01((t-FlingAt)/(ReturnAt-FlingAt)))+Facing;
        private static Vector3 Leg(Vector3 a,Vector3 b,float u,float rise){u=NativeEntranceMath.Clamp01(u);return Vector3.Lerp(a,b,u)+new Vector3(0,rise*(float)Math.Sin(u*Math.PI),0);}
        /// <summary>The ring's spin about its axle (radians) at `t`: four turns a second out of the hand.</summary>
        public static float Spin(float t)=>t*25.1f;

        // ---- the light and the look ----
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,Hit1,.01f,.18f)*.01f+NativeEntranceMath.Pulse(t,Hit2,.01f,.18f)*.01f+NativeEntranceMath.Pulse(t,LandAt,.01f,.3f)*.03f+NativeEntranceMath.Pulse(t,SlashAt,.01f,.3f)*.02f;
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            // A dark stage lit by the ring itself until the landing powers it up.
            float up=NativeEntranceMath.Ramp(t,LandAt-.1f,OrbitTo);l.DriveProbe=true;NativeEntranceStage.Rig(l,.07f+.6f*up,Accent,.35f+.65f*up,.08f+.45f*up,.1f+.55f*up);l.DriveProbe=true;
            if(Ring(t,out var at,out _))l.Add(at,Accent*3.4f,4.2f);
            float p1=NativeEntranceMath.Pulse(t,Hit1,.01f,.3f),p2=NativeEntranceMath.Pulse(t,Hit2,.01f,.3f);if(p1>.01f)l.Add(Pylon1,White*(5*p1),4.5f);if(p2>.01f)l.Add(Pylon2,White*(5*p2),4.5f);
            float c=NativeEntranceMath.Pulse(t,CatchAt,.01f,.4f);if(c>.01f)l.Add(CatchPoint,(White+Accent)*(3*c),5);
            // The orbit's circle glowing under it, flashing up with the cut.
            float ring=NativeEntranceMath.Ramp(t,OrbitFrom,OrbitTo)*(1-NativeEntranceMath.Ramp(t,SlashAt+.3f,CardAt));float flash=NativeEntranceMath.Pulse(t,SlashAt,.01f,.5f);
            if(ring+flash>.01f)l.Add(new Vector3(0,.2f,0),Accent*(1.8f*ring+4*flash),4.5f);
            if(t>=StandAt-.2f){var hand=Anchor(NativeEntranceAnchor.RightHand,t);l.Add(hand+new Vector3(0,.25f,.3f),Accent*(1.6f*NativeEntranceMath.Ramp(t,StandAt-.2f,StandAt+.3f)),2.4f);}
            if(t>=CardAt-.2f){float k=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(-2.2f,2.4f,2),Cool*(2.2f*k),8);l.Add(new Vector3(2.2f,2.2f,1.8f),NativeEntranceFx.Hex(0x8b5cff)*(2*k),8);}
        }
        protected override float Pylon(int i,float t)=>t<LandAt?(i%2==0&&t>=Hit1||i%2==1&&t>=Hit2?.25f:.03f):PowerOn(i,t-LandAt+.05f,.08f);
        public override void Props(float t,INativeEntranceProps props,float q)=>Stage(props,t,t<LandAt?.02f:.12f+.3f*NativeEntranceMath.Ramp(t,OrbitFrom,OrbitTo),SlashAt,Accent);

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;bool low=q<.5f;
            // The ring out of the hand: a hot core, its spinning glint and a wake of light back along its path.
            if(Ring(t,out var at,out var travel))
            {
                fx.Glow(at,(White+Accent)*1.6f,0,.32f,2,0,11000);
                float g=Spin(t);fx.Glow(at+new Vector3((float)Math.Cos(g)*.3f,0,(float)Math.Sin(g)*.3f),White*2.4f,0,.07f,3,0,11001);
                int n=low?6:14;for(int i=1;i<=n;i++){float back=t-i*.018f;if(!Ring(back,out var p,out var d))break;float u=i/(float)n;fx.Streak(p,d*12,Accent*(1.8f*(1-u)),.05f*(1-u)+.01f,.12f,.02f,.6f,u,11010+i);}
            }
            // The ricochets: a ping ring and a shower of sparks off each pylon.
            for(int h=0;h<2;h++)
            {
                float at2=h==0?Hit1:Hit2;var p=h==0?Pylon1:Pylon2;float s=t-at2;if(s<0||s>.7f)continue;float u=s/.7f;
                fx.Glow(p,White*(3*(1-u)),0,.2f+.9f*u,2,u,11100+h);
                NativeEntranceFx.Sparks(fx,t,at2,p,Vector3.Normalize(-p+new Vector3(0,1.5f,0)),1.2f,low?10:26,7.5f,.55f,12,.3f,White*2.4f,Accent,.01f,11120+h*40,q,floor);
            }
            // The catch: a flash and a ring of light snapping out round the hand.
            {
                float s=t-CatchAt;if(s>=0&&s<.6f){float u=s/.6f;fx.Glow(CatchPoint,(White+Accent)*(3*(1-u)),0,.35f+.8f*u,2,u,11200);int m=low?12:28;for(int i=0;i<m;i++){float a=i*6.2831853f/m;var d=new Vector3((float)Math.Cos(a),(float)Math.Sin(a)*.4f,(float)Math.Sin(a));fx.Streak(CatchPoint+d*(.2f+1.6f*u),d*5,Accent*(2*(1-u)),.02f,.12f,.03f,.7f,u,11210+i);}}
            }
            // The landing: dust and a ring over the floor.
            NativeEntranceFx.GroundDust(fx,t,LandAt,new Vector3(0,.05f,0),low?8:20,.3f,3.2f,.9f,Dust,.55f,.5f,11300,q,floor);
            {
                float s=t-LandAt;if(s>=0&&s<.5f){float u=s/.5f,reach=.3f+3.2f*(1-(1-u)*(1-u));int m=low?18:40;for(int i=0;i<m;i++){float a=i*6.2831853f/m;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(new Vector3(0,floor+.03f,0)+d*reach,d*5,Accent*(1.8f*(1-u)),.025f,.15f,.03f,.7f,u,11350+i);}}
            }
            // The orbit's circle carved into the floor behind the ring as it goes round (twice), bright where it passes; it flashes up with
            // the cut and fades out under the name.
            if(t>=OrbitFrom-.05f&&t<CardAt+.6f)
            {
                float done=OrbitAngle(Math.Min(t,OrbitTo))-OrbitAngle(OrbitFrom),fade=1-NativeEntranceMath.Ramp(t,SlashAt+.4f,CardAt+.6f),flash=NativeEntranceMath.Pulse(t,SlashAt,.01f,.6f);
                int segs=low?24:56;float start=OrbitAngle(OrbitFrom);
                for(int i=0;i<segs;i++)
                {
                    float a0=i*6.2831853f/segs,a1=(i+1)*6.2831853f/segs;if(a0>done)break;float a=start+a0;
                    var p=new Vector3((float)Math.Sin(a)*OrbitRadius,floor+.02f,(float)Math.Cos(a)*OrbitRadius);var d=new Vector3((float)Math.Cos(a),0,-(float)Math.Sin(a));
                    float fresh=t<OrbitTo?NativeEntranceMath.Clamp01(1-(done-a0)/2.2f):0;fx.Streak(p,d*(OrbitRadius*6.2831853f/segs*8),Accent*((.9f+1.6f*fresh+3*flash)*fade),.035f+.03f*flash,.2f,.02f,.3f,0,11400+i);
                }
                // The flash goes up as a wall of motes.
                if(flash>.02f){int m=low?14:36;for(int i=0;i<m;i++){float a=i*6.2831853f/m+NativeEntranceMath.Signed(11500,i)*.1f,s=t-SlashAt;var p=new Vector3((float)Math.Sin(a)*OrbitRadius,floor+.1f+2.4f*s*(.6f+.4f*NativeEntranceMath.Rand(11500,i+40)),(float)Math.Cos(a)*OrbitRadius);fx.Glow(p,Accent*(2.2f*flash),0,.06f,3,1-flash,11500+i);}}
                // Sparks shed off the ring on its orbit.
                if(t<OrbitTo&&Ring(t,out var r2,out var tv))NativeEntranceFx.Sparks(fx,t,t-.05f,r2,-tv,.8f,low?2:5,3,.3f,6,.4f,White*2,Accent,.008f,11600+(int)(t*20)%40,q,floor);
            }
            // The cut: a crescent of light hanging where the ring went across, then breaking into motes that drift up.
            {
                float s=t-SlashAt;if(s>=-.04f&&s<1.4f)
                {
                    float hold=s<.5f?1:1-NativeEntranceMath.Smooth((s-.5f)/.9f);int m=low?14:30;
                    for(int i=0;i<m;i++)
                    {
                        float u=i/(float)(m-1),a=-1.3f+2.6f*u,grow=NativeEntranceMath.Clamp01((s+.04f)/.1f);if(u>grow)break;
                        var p=Local((float)Math.Sin(a)*.95f,1.3f+.12f*(float)Math.Sin(a*1.4f),.35f+(float)Math.Cos(a)*.75f);var d=Local((float)Math.Cos(a),0,-(float)Math.Sin(a));
                        float edge=1-Math.Abs(u-.5f)*1.6f;fx.Streak(p,d*3,(White*.6f+Accent)*(2.6f*hold*edge),.05f*edge+.01f,.1f,.02f,.4f,1-hold,11700+i);
                        if(s>.45f&&!low&&i%2==0){float k=s-.45f;fx.Glow(p+new Vector3(0,.5f*k,0),Accent*(1.4f*hold),0,.05f,3,1-hold,11760+i);}
                    }
                }
            }
            // The hero: motes drifting up round the ring by the face.
            if(t>=StandAt)
            {
                var hand=Anchor(NativeEntranceAnchor.RightHand,t)+new Vector3(0,.25f,0);int m=low?6:16;
                for(int i=0;i<m;i++){float born=StandAt+(i/(float)m)*1.6f,a=((t-born)%1.6f+1.6f)%1.6f/1.6f;var p=hand+new Vector3(NativeEntranceMath.Signed(11800,i)*.35f,.6f*a,NativeEntranceMath.Signed(11800,i+30)*.25f);fx.Glow(p,Accent*(1.4f*(1-a)),0,.04f,3,a,11800+i);}
            }
        }
    }
}
