using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One toon model of the Top Dog's entrance this frame (source frame): the cart, the spring, a hot dog, the bell.</summary>
    public struct NativeIntroToon{public string Id;public Vector3 At;public Quaternion Turn;public float Scale,Param,Wiggle;}

    /// <summary>
    /// "DEV · Order Up!", the Top Dog's entrance (owner 2026-11-08: "another dev class with an intro (aimed towards this new hotdog skin) VERY
    /// high quality and detailed"; docs/TOP-DOG.md "The intro"). 8.6 s, the name at 6.6 s, an old cartoon from its first frame: an iris opens on
    /// a hot dog cart steaming under its striped umbrella in the dark arena, a bell rings for service, a drumroll rattles the cart, the lid bursts
    /// open in a puff and hot dogs fly out, and the Top Dog springs out of the cart on a giant spring, spread like a star at the top, and lands
    /// in a squash. It reaches behind its back and pulls a mallet out of nowhere (it grows as it comes), swings it overhead and slams the floor:
    /// a ring, stars and the hot dogs on the floor hopping. It leans on the mallet under the name, gives a thumbs up, and the iris closes on it.
    /// Presentation only: pure functions of the entrance clock, thinned by `quality`, every beat kept on Reduced.
    /// </summary>
    public sealed class NativeTopDogIntro:NativeProceduralEntrance
    {
        public const float Total=8.6f,CardAt=6.6f,End=8.2f;
        public const float IrisOpen=.02f,BellAt=.85f,DrumAt=1.45f,LidAt=2.45f,SpringAt=2.5f,ApexAt=2.95f,LandAt=3.45f,MalletAt=4.0f,MalletOut=4.4f,SlamAt=5.0f,RestAt=5.7f,WinkAt=7.35f,IrisClose=7.7f;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        public static readonly Vector3 CartAt=new Vector3(0,0,-1.75f);
        private static readonly Vector3 Warm=NativeEntranceFx.Hex(0xffc46a),Mustard=NativeEntranceFx.Hex(0xffc230),Ketchup=NativeEntranceFx.Hex(0xe2362c),Steam=new Vector3(.85f,.85f,.88f),Dust=new Vector3(.42f,.36f,.3f);
        private static readonly Vector3 Land=Local(0,0,.42f);
        public NativeTopDogIntro():base(NativeTopDog.Entrance,SlamAt,0xffc230)
        {
            SlowRate=.35f;SlowHold=.1f;SlowRecover=.18f;SlowCatchUp=.5f;
            Start=Stance(.2f,.06f,-.1f,-.2f,-.08f,-.4f,Facing,Land.X,Land.Z);
            Steps=new[]
            {
                // A little shuffle as it leans on the mallet.
                Step(true,RestAt-.05f,RestAt+.2f,.24f,.1f,-.05f,.05f,Facing,Land.X,Land.Z),
            };
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            var cart=CartAt;var inCart=new Vector3(cart.X,.35f,cart.Z);var top=new Vector3(cart.X,1.55f,cart.Z+.15f);var apex=new Vector3(0,3.2f,-.75f);var drop=new Vector3(0,1.45f,.22f);var at=new Vector3(Land.X,.88f,Land.Z);
            // In the cart (hidden: Solid), crouched on the spring.
            k.Key(0,stand).Root(inCart.X,inCart.Y,inCart.Z,Facing).Planted(0).Drop(-.3f).J(B,.2f).J(LL,-1.2f).J(LK,1.9f).J(RL,-1.2f).J(RK,1.9f).J(LA,-.4f).J(RA,-.4f);
            k.Key(SpringAt-.02f,stand).Root(inCart.X,inCart.Y,inCart.Z,Facing).Planted(0).Drop(-.3f).J(B,.2f).J(LL,-1.2f).J(LK,1.9f).J(RL,-1.2f).J(RK,1.9f).J(LA,-.4f).J(RA,-.4f);
            // Out of the top, stretched long, arms straight up.
            k.Key(SpringAt+.14f,stand).Root(top.X,top.Y,top.Z,Facing).Planted(0).Spin(-.15f,0).Drop(.05f).J(Hd,-.2f).J(LA,-2.95f,0,.1f).J(LE,-.05f).J(RA,-2.95f,0,-.1f).J(RE,-.05f).J(LL,.05f).J(RL,.05f).PassKey();
            // The top: spread like a star.
            k.Key(ApexAt,stand).Root(apex.X,apex.Y,apex.Z,Facing).Planted(0).Spin(-.35f,.08f).J(Hd,-.25f).J(LA,-1.4f,0,1.25f).J(LE,-.1f).J(RA,-1.4f,0,-1.25f).J(RE,-.1f).J(LL,-.15f,0,.55f).J(LK,.2f).J(RL,-.15f,0,-.55f).J(RK,.2f).PassKey();
            // Tucked as it drops in.
            k.Key(LandAt-.14f,stand).Root(drop.X,drop.Y,drop.Z,Facing).Planted(0).Spin(.15f,0).J(B,.15f).J(Hd,.1f).J(LA,-.9f,0,.5f).J(LE,-.6f).J(RA,-.9f,0,-.5f).J(RE,-.6f).J(LL,-1f,0,.12f).J(LK,1.5f).J(RL,-.9f,0,-.12f).J(RK,1.4f);
            // The landing: a deep squash, arms out for balance.
            k.Key(LandAt,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(-.34f).J(B,.32f).J(Sp,.12f).J(Hd,-.1f).J(LA,-.5f,0,1.1f).J(LE,-.2f).J(RA,-.5f,0,-1.1f).J(RE,-.2f).J(LL,-.95f,0,.1f).J(LK,1.55f).J(RL,-.85f,0,-.1f).J(RK,1.45f).ImpactKey();
            // Springs up past standing, then settles: a toon recoil.
            k.Key(LandAt+.2f,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(.05f).J(B,-.08f).J(Hd,-.12f).J(LA,-.6f,0,.7f).J(RA,-.6f,0,-.7f).PassKey();
            k.Key(LandAt+.42f,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(-.03f).J(B,.04f).J(LA,-.3f,0,.3f).J(LE,-.3f).J(RA,-.3f,0,-.3f).J(RE,-.3f);
            // Reaching behind its back for the mallet, looking over its shoulder.
            k.Key(MalletAt,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(-.04f).J(C,0,-.38f,0).J(Hd,.05f,-.5f,0).J(RA,.85f,.2f,-.35f).J(RE,-.75f).J(LA,-.25f,0,.35f).J(LE,-.4f);
            // Out it comes, up overhead in both hands.
            k.Key(MalletOut,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(.02f).J(C,-.14f,0,0).J(Hd,-.22f).J(RA,-2.75f,0,-.12f).J(RE,-.35f).J(LA,-2.7f,0,.18f).J(LE,-.45f).PassKey();
            // Wound back, then the slam: bent over the floor, knees down.
            k.Key(SlamAt-.14f,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(-.02f).J(B,-.2f).J(C,-.22f).J(Hd,-.25f).J(RA,-3.05f,0,-.1f).J(RE,-.25f).J(LA,-3.0f,0,.15f).J(LE,-.3f);
            k.Key(SlamAt,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(-.24f).J(B,.5f).J(Sp,.25f).J(Hd,-.1f).J(RA,-.85f,0,-.1f).J(RE,-.15f).J(LA,-.8f,0,.15f).J(LE,-.2f).J(LL,-.55f,0,.08f).J(LK,.95f).J(RL,-.5f,0,-.08f).J(RK,.9f).ImpactKey();
            k.Key(SlamAt+.32f,stand).Root(at.X,at.Y,at.Z,Facing).Planted(1).Drop(-.2f).J(B,.45f).J(Sp,.2f).J(Hd,-.15f).J(RA,-.85f,0,-.1f).J(RE,-.15f).J(LA,-.8f,0,.15f).J(LE,-.2f).J(LL,-.5f,0,.08f).J(LK,.85f).J(RL,-.45f,0,-.08f).J(RK,.8f);
            // Up, leaning on it: the mallet's head on the floor, both hands on the top of its handle, a hip out.
            k.Key(RestAt,stand).Root(at.X,at.Y,at.Z,Facing+.1f).Planted(1).Drop(-.03f).J(B,.08f).J(C,0,.12f,0).J(Hd,-.05f,0,.12f).J(RA,-.75f,0,-.05f).J(RE,-.95f).J(LA,-.7f,0,.12f).J(LE,-1f);
            k.Key(CardAt,stand).Root(at.X,at.Y,at.Z,Facing+.1f).Planted(1).Drop(-.04f).J(B,.08f).J(C,0,.14f,0).J(Hd,-.06f,0,.16f).J(RA,-.75f,0,-.05f).J(RE,-.95f).J(LA,-.7f,0,.12f).J(LE,-1f);
            // The thumbs up.
            k.Key(WinkAt-.12f,stand).Root(at.X,at.Y,at.Z,Facing+.1f).Planted(1).Drop(-.04f).J(B,.06f).J(C,0,.14f,0).J(Hd,-.08f,0,.2f).J(RA,-.75f,0,-.05f).J(RE,-.95f).J(LA,-.7f,0,.12f).J(LE,-1f);
            k.Key(WinkAt+.1f,stand).Root(at.X,at.Y,at.Z,Facing+.1f).Planted(1).Drop(-.03f).J(B,.04f).J(C,0,.16f,0).J(Hd,-.1f,0,.24f).J(RA,-.75f,0,-.05f).J(RE,-.95f).J(LA,-1.25f,0,.3f).J(LE,-1.65f).ImpactKey();
            k.Key(Total,stand).Root(at.X,at.Y,at.Z,Facing+.1f).Planted(1).Drop(-.03f).J(B,.04f).J(C,0,.16f,0).J(Hd,-.1f,0,.24f).J(RA,-.75f,0,-.05f).J(RE,-.95f).J(LA,-1.25f,0,.3f).J(LE,-1.65f);Keys=k;
            Shots=new[]
            {
                // The cart in the dark, steaming, the camera creeping in low.
                Shot(0,NativeEntranceCut.Cut,46,40,V(1.7f,.75f,4.4f),V(1.05f,.8f,3.1f),V(cart.X,.95f,cart.Z),V(cart.X,1.05f,cart.Z),1,0),
                // The drumroll: close on the rattling lid.
                Shot(DrumAt,NativeEntranceCut.Cut,36,32,V(.8f,1.35f,.75f),V(.55f,1.4f,.35f),V(cart.X,1.0f,cart.Z),V(cart.X,1.05f,cart.Z),1,0),
                // The lid goes: whip out wide and low, looking up the spring.
                Shot(LidAt,NativeEntranceCut.Whip,58,52,V(2.6f,.35f,3.4f),V(2.3f,.45f,3.1f),V(0,2.2f,-1),V(0,1.6f,-.2f),1,0,.12f),
                // The landing from in front, low.
                Shot(LandAt-.06f,NativeEntranceCut.Cut,46,42,V(.95f,.38f,2.7f),V(.8f,.44f,2.35f),V(at.X,.9f,at.Z),V(at.X,1.0f,at.Z),1,0),
                // The pull: over the shoulder, close.
                Shot(MalletAt-.06f,NativeEntranceCut.Cut,40,36,V(-1.15f,1.35f,2.25f),V(-.9f,1.4f,1.95f),V(at.X,1.5f,at.Z),V(at.X,1.75f,at.Z),1,0),
                // The slam, wide and low.
                Shot(SlamAt-.3f,NativeEntranceCut.Cut,54,58,V(2.5f,.55f,3.1f),V(2.85f,.72f,3.55f),V(at.X,.85f,at.Z+.6f),V(at.X,.7f,at.Z+.9f),1,0),
                // The hero: leaning on the mallet under the name.
                Shot(RestAt,NativeEntranceCut.Blend,40,36,V(1.25f,1.0f,3.9f),V(.85f,1.05f,3.45f),V(at.X,1.1f,at.Z),V(at.X,1.12f,at.Z),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(.85f,1.02f,3.55f),V(.66f,1.06f,3.3f),V(at.X-.1f,1.18f,at.Z),V(at.X-.12f,1.2f,at.Z),1,0),
            };
        }
        /// <summary>Hidden in the cart until the lid goes.</summary>
        public override float Solid(float t,int bone)=>t<LidAt+.03f?0:1;
        protected override Vector3 Focus(float t,Vector3 chest)=>t<LidAt?CartAt+new Vector3(0,1,0):chest;
        /// <summary>The mallet's share out of hammerspace: none until the reach, grown to an overshoot and settled as it comes up.</summary>
        public static float MalletScale(float t)
        {
            if(t<MalletAt+.08f)return 0;float u=NativeEntranceMath.Clamp01((t-MalletAt-.08f)/(MalletOut-MalletAt-.08f));if(u<1)return u*u*(3-2*u)*1.18f;
            float s=t-MalletOut;return 1+.18f*(float)(Math.Exp(-7*s)*Math.Cos(s*24));
        }
        /// <summary>The lid at `t`: shut, rattling through the drumroll, thrown back with a bounce.</summary>
        public static float LidOpen(float t)
        {
            if(t<DrumAt)return 0;if(t<LidAt)return .04f*(float)Math.Abs(Math.Sin((t-DrumAt)*38))*NativeEntranceMath.Clamp01((t-DrumAt)/.6f);
            float s=t-LidAt;return Math.Min(1.12f,1-(float)(Math.Exp(-9*s)*Math.Cos(s*20)));
        }
        /// <summary>The cart's rattle (m) through the drumroll and its kick as the Top Dog springs.</summary>
        private static Vector3 CartShake(float t)
        {
            float a=t>=DrumAt&&t<LidAt?.012f*NativeEntranceMath.Clamp01((t-DrumAt)/.5f):0;a+=NativeEntranceMath.Pulse(t,SpringAt,.01f,.25f)*.03f;a+=NativeEntranceMath.Pulse(t,SlamAt,.01f,.3f)*.02f;
            return new Vector3((float)Math.Sin(t*71)*a,(float)Math.Abs(Math.Sin(t*53))*a,(float)Math.Sin(t*61+1)*a*.5f);
        }
        /// <summary>The hot dogs thrown out with the lid: each on its arc, then lying on the floor (hopping at the slam).</summary>
        public static Vector3 FlyingDog(int i,float t,out Quaternion turn,out float wiggle)
        {
            float a=-1.1f+i*.55f,sp=1.6f+.5f*(i%2),up=5.4f+.8f*((i*3)%4)/3f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a)*.6f+.4f);var from=CartAt+new Vector3(0,1.05f,0);
            float s=Math.Max(0,t-LidAt-.02f),land=(up+(float)Math.Sqrt(up*up+2*23*(from.Y-.05f)))/23;wiggle=1;
            if(s<land){turn=Quaternion.CreateFromYawPitchRoll(a+s*6,s*9+i,s*4);return from+d*(sp*s)+new Vector3(0,up*s-11.5f*s*s,0);}
            var rest=from+d*(sp*land);rest.Y=.06f;float hop=t>=SlamAt?Math.Max(0,(float)(1.2*Math.Sin(Math.PI*Math.Min(1,(t-SlamAt-.04f*i)/.45f)))):0;wiggle=.35f+(hop>0?.65f:0);
            turn=Quaternion.CreateFromYawPitchRoll(a+i,1.5708f,0);return rest+new Vector3(0,hop*.5f,0);
        }
        public const int FlyingDogs=5;
        /// <summary>This frame's toon models (Runtime NativeIntros draws them in the Top Dog's toon shader).</summary>
        public void Toons(float t,List<NativeIntroToon> into)
        {
            into.Clear();var shake=CartShake(t);var cartYaw=Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.PI);
            into.Add(new NativeIntroToon{Id="cart",At=CartAt+shake,Turn=cartYaw,Scale=1,Param=LidOpen(t)});
            // The spring out of the grill, under the feet until it has thrown them.
            if(t>=SpringAt-.02f&&t<LandAt+.6f)
            {
                var feet=(Anchor(NativeEntranceAnchor.LeftFoot,t)+Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;var baseAt=CartAt+new Vector3(0,NativeTopDogModels.CartTop,.1f)+shake;
                float h=t<ApexAt-.15f?Math.Max(.15f,feet.Y-baseAt.Y):Math.Max(.15f,.6f*(float)Math.Exp(-5*(t-ApexAt+.15f))*(1+.3f*(float)Math.Cos((t-ApexAt)*28)));
                into.Add(new NativeIntroToon{Id="spring",At=baseAt,Turn=Quaternion.Identity,Scale=.85f,Param=h});
            }
            if(t>=LidAt)for(int i=0;i<FlyingDogs;i++){var p=FlyingDog(i,t,out var q,out float w);into.Add(new NativeIntroToon{Id="hotdog",At=p,Turn=q,Scale=1.2f,Param=t*14+i*1.7f,Wiggle=w});}
        }
        // ---- the light and the look ----
        public override string AccentAt(float t)=>t<LidAt?"#3a2a10":t<SlamAt?"#ffc230":"#ffd860";
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,LandAt,.01f,.25f)*.02f+NativeEntranceMath.Pulse(t,SlamAt,.01f,.4f)*.045f+NativeEntranceMath.Pulse(t,LidAt,.01f,.2f)*.012f;
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            // A dark arena, a warm pool of light on the cart that widens to the stage once it is out.
            float up=NativeEntranceMath.Ramp(t,LidAt,LandAt+.3f);l.DriveProbe=true;NativeEntranceStage.Rig(l,.08f+.55f*up,Mustard,.4f+.6f*up,.1f+.4f*up,.12f+.5f*up);l.DriveProbe=true;
            float fl=.92f+.08f*(float)Math.Sin(t*13);l.Add(CartAt+new Vector3(0,2.6f,.9f),Warm*(5.2f*fl),6.5f);l.Add(CartAt+new Vector3(0,1.15f,.35f),Warm*(1.6f+1.2f*NativeEntranceMath.Pulse(t,LidAt,.01f,.4f)),2.6f);
            if(t>=LandAt-.1f){var chest=Anchor(NativeEntranceAnchor.Chest,t);l.Add(chest+new Vector3(.4f,1.2f,1.6f),Warm*(3.2f*up),6);l.Add(chest+new Vector3(-1.4f,.8f,-.6f),Ketchup*(2.2f*up),5);}
            float slam=NativeEntranceMath.Pulse(t,SlamAt,.01f,.35f);if(slam>.01f)l.Add(Land+new Vector3(0,.3f,.9f),Vector3.One*(4*slam),5);
            if(t>=CardAt-.2f){float c=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(-2,2.4f,2),NativeEntranceFx.Hex(0xff5aa0)*(2.4f*c),8);l.Add(new Vector3(2.2f,2.2f,1.8f),NativeEntranceFx.Hex(0x5ac8ff)*(2.4f*c),8);}
        }
        protected override float Pylon(int i,float t)=>t<LidAt?.04f:PowerOn(i,t-LidAt+.05f,.05f);
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,t<LidAt?.02f:.12f,SlamAt,Mustard);
            // Condiment drips on the floor by the cart, and the warm cone of the stand's light.
            props.Piece(WeaponShape.Sphere,false,CartAt+new Vector3(.62f,.004f,.42f),Quaternion.Identity,new Vector3(.16f,.006f,.1f),Mustard*.8f);
            props.Piece(WeaponShape.Sphere,false,CartAt+new Vector3(-.5f,.004f,.55f),Quaternion.Identity,new Vector3(.09f,.006f,.12f),Ketchup*.7f);
        }
        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;var grill=CartAt+new Vector3(0,NativeTopDogModels.CartTop+.05f,0);
            // The grill's steam, more of it through the drumroll, a gush when the lid goes.
            int n=q<.5f?5:12;float more=1+1.5f*NativeEntranceMath.Ramp(t,DrumAt,LidAt);
            for(int i=0;i<n;i++){float u=((t*.7f*more+i/(float)n)%1);var p=grill+new Vector3((float)Math.Sin(i*2.1f+t)*.25f*(1+u),.05f+1.4f*u,(float)Math.Cos(i*1.7f+t)*.12f*(1+u));fx.Smoke(p,Steam,.22f*(1-u)*(t<LidAt+.4f?1:1-NativeEntranceMath.Ramp(t,LidAt+.4f,LidAt+1.5f)),.15f+.4f*u,.15f+.6f*u,9100+i,floor);}
            // Sizzle: little bright pops off the grill.
            if(t<LidAt)for(int i=0;i<(q<.5f?3:8);i++){float a=((t*9+i*.29f)%1);int k=(int)(t*9)+i*31;var v=new Vector3(NativeEntranceMath.Signed(k,1)*.6f,.8f+.6f*NativeEntranceMath.Rand(k,2),NativeEntranceMath.Signed(k,3)*.4f);fx.Streak(grill+new Vector3(NativeEntranceMath.Signed(k,4)*.3f,0,0)+v*a*.3f,v,Warm*(1.8f*(1-a)),.005f,.01f,.02f,.6f,a,k);}
            // The bell's ring.
            if(t>=BellAt&&t<BellAt+.6f){float u=(t-BellAt)/.6f;fx.Glow(CartAt+new Vector3(.62f,1.12f,.2f),Warm*(2*(1-u)),0,.25f+.4f*u,2,u,9200);}
            // The lid: a white poof, streamers of steam, confetti-bright sparks.
            if(t>=LidAt&&t<LidAt+1.4f)
            {
                float s=t-LidAt;int m=q<.5f?8:22;for(int i=0;i<m;i++){var d=Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(9300+i,1),.6f+NativeEntranceMath.Rand(9300+i,2),NativeEntranceMath.Signed(9300+i,3)));float u=s/1.4f;var p=grill+d*(1.4f*(1-(1-u)*(1-u)))+new Vector3(0,.4f*u,0);fx.Smoke(p,new Vector3(.95f,.95f,.95f),.8f*(1-u)*(1-u),.35f+.9f*u,.1f+.7f*u,9300+i,floor);}
                NativeEntranceFx.Sparks(fx,t,LidAt,grill+new Vector3(0,.2f,0),new Vector3(0,1,0),1.1f,q<.5f?10:26,7,.8f,9,.4f,Warm*2.4f,Mustard,.012f,9400,q,floor);
            }
            // The spring's twang lines, the top's sparkle.
            if(t>=ApexAt-.1f&&t<ApexAt+.35f){var c=Anchor(NativeEntranceAnchor.Chest,t);float u=(t-ApexAt+.1f)/.45f;for(int i=0;i<6;i++){float a=i*1.047f+u*2;fx.Glow(c+new Vector3((float)Math.Cos(a)*(.5f+.6f*u),(float)Math.Sin(a)*(.5f+.6f*u),0),Mustard*(2.2f*(1-u)),0,.12f,3,u,9500+i);}}
            // The landing: a ring of dust and a puff.
            NativeEntranceFx.GroundDust(fx,t,LandAt,new Vector3(Land.X,.05f,Land.Z),q<.5f?8:20,.3f,3.2f,.9f,Dust,.6f,.55f,9600,q,floor);
            // The mallet out of nowhere: a twinkle where it appears.
            if(t>=MalletAt+.05f&&t<MalletOut+.25f){var h=Anchor(NativeEntranceAnchor.RightHand,t);float u=(t-MalletAt-.05f)/(MalletOut+.2f-MalletAt);fx.Glow(h,Vector3.One*(2.2f*(1-u)),0,.25f+.5f*u,1,u,9700);for(int i=0;i<5;i++){float a=i*1.256f+u*4;fx.Glow(h+new Vector3((float)Math.Cos(a)*.4f*u,(float)Math.Sin(a)*.4f*u,.1f),Mustard*(2.4f*(1-u)),0,.08f,3,u,9710+i);}}
            // The slam: a white ring tearing out over the floor, dust, the cartoon stars.
            float sw=t-SlamAt;var hit=new Vector3(Land.X,.04f,Land.Z+.95f);
            if(sw>=0&&sw<.6f){float reach=.3f+5.5f*(1-(1-sw/.6f)*(1-sw/.6f));int m=q<.5f?20:48;for(int i=0;i<m;i++){float a=i*6.2831853f/m;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(hit+d*reach,d*6,Vector3.One*(2.2f*(1-sw/.6f)),.03f,.2f,.04f,.7f,sw/.6f,9800+i);}}
            NativeEntranceFx.GroundDust(fx,t,SlamAt,hit,q<.5f?10:26,.4f,4.5f,1.2f,Dust,.65f,.7f,9900,q,floor);
            if(sw>=0&&sw<1.1f){int m=q<.5f?5:9;for(int i=0;i<m;i++){float life=.7f+.4f*NativeEntranceMath.Rand(9950+i,1);if(sw>life)continue;float u=sw/life;var d=Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(9950+i,2),.9f+.6f*NativeEntranceMath.Rand(9950+i,3),NativeEntranceMath.Signed(9950+i,4)));var p=hit+d*(3*sw)+new Vector3(0,-2.4f*sw*sw,0);fx.Glow(p,Mustard*(2.6f*(1-u*u)),0,.16f,3,u,9950+i);}}
            // Under the name: confetti drifting down, the diner's colours.
            if(t>=CardAt-.1f){Vector3[] c={Mustard,Ketchup,NativeEntranceFx.Hex(0x5ac8ff),NativeEntranceFx.Hex(0x7dff6a),Vector3.One};int m=q<.5f?24:60;for(int i=0;i<m;i++){float born=CardAt-.1f+.9f*NativeEntranceMath.Rand(10000+i,1),a=t-born;if(a<0||a>2.6f)continue;var p=new Vector3(NativeEntranceMath.Signed(10000+i,2)*3.2f+(float)Math.Sin(a*3+i)*.2f,3.2f-1.3f*a,Land.Z+NativeEntranceMath.Signed(10000+i,3)*1.8f+.8f);float flick=.5f+.5f*(float)Math.Abs(Math.Sin(a*11+i));fx.Glow(p,c[i%c.Length]*(1.6f*flick*(1-a/2.6f)),0,.06f,3,a/2.6f,10000+i);}}
        }
    }
}
