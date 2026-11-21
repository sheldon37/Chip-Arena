using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Apex", the Fangs' intro (intros v2; docs/INTROS-MELEE.md): a predator's pounce. 7.4 s, the name at 5.9 s. Night in the Canopy
    /// (overgrown temple ruins: a carved gateway, a strangler fig, roots over the flagstones, fireflies in the mist). The camera drifts low
    /// through the ferns like prey looking round; two green eyes open on the gateway's lintel; it is the pilot, crouched up there, the fangs
    /// out, venom beading on their tips. It coils, and leaps long out over the lens (slowed at the top of its arc); its feet find the floor
    /// a moment before both fangs go into the earth in a bloom of venom, leaves and mist. It rises into a hunter's stance, the fangs low and
    /// forward, venom dripping, under the name.
    /// The body is keyed (no match clips), its feet on the lintel's top on the model (ModelGround); the fangs on their keyed paths.
    /// Presentation only: pure functions of the presentation clock.
    /// </summary>
    public sealed class NativeIntroApex:NativeIntroMeleeBlade
    {
        public const string Title="Apex";
        public const float Total=7.4f,CardAt=5.9f;
        /// <summary>The beats (presentation s): the eyes, the reveal, the coil, the leap, its top, the landing strike, the rise.</summary>
        public const float EyesAt=1.05f,RevealAt=1.45f,CoilAt=2.75f,LeapAt=3.25f,ApexAt=3.55f,TouchAt=3.89f,LandAt=3.95f,RiseAt=4.55f,StandAt=4.75f;
        private static readonly Vector3 Venom=NativeEntranceFx.Hex(0x7dff3a),Bile=NativeEntranceFx.Hex(0xd9ff8a),Fumes=NativeEntranceFx.Hex(0x4dd22e),Moon=NativeEntranceFx.Hex(0xa9c4e8),
            Night=NativeEntranceFx.Hex(0x040705),Mist=new Vector3(.2f,.26f,.24f),Firefly=NativeEntranceFx.Hex(0xd8ff7a),Earth=NativeEntranceFx.Hex(0x3a2c1c),Leaf=NativeEntranceFx.Hex(0x3f5a26);
        public override string Spec=>NativeFangs.Spec;
        /// <summary>Where it crouches on the lintel (the canopy set's flat top, 3.2 m) and where it lands (source frame, on the floor).</summary>
        public static readonly Vector3 Perch=new Vector3(.6f,3.2f,-3.0f),Landing=new Vector3(0,0,0);
        /// <summary>How high the leap's arc stands over the line from the lintel to the landing (m at its middle): enough that it rises
        /// first (its top a quarter of the way, about 0.26 m over the lintel).</summary>
        private const float LeapRise=1.4f;

        public NativeIntroApex():base(NativeIntroCatalog.WeaponPrefix+NativeFangs.Spec,Total,CardAt,LandAt,0x7dff3a)
        {
            RootPath=(t,r)=>r+Way(t);
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips; the feet and drops on the model, ModelFeet): the root stands at
            // .88 over the surface under it (the lintel's top, then the floor: Way carries it), the feet on the lintel's top (ModelGround) ----
            Start=Stance(.15f,.1f,.2f,-.15f,-.12f,-.3f,Facing,Perch.X,Perch.Z);
            Steps=new[]{Step(true,LeapAt+.02f,TouchAt-.02f,.2f,.06f,.25f,.2f,Facing,Landing.X,Landing.Z),Step(false,LeapAt+.04f,TouchAt-.01f,-.2f,-.1f,-.35f,.2f,Facing,Landing.X,Landing.Z),
                Step(true,RiseAt+.15f,RiseAt+.45f,.17f,.12f,.22f,.06f,Facing,Landing.X,Landing.Z)};
            var crouch=NativeIntroPoses.Crouch();var k=new NativeEntranceKeys();
            // On the lintel: a low predator's crouch, leaning out over the edge, head up, one fang down at the stone, the other back.
            k.Key(0,crouch).Root(0,.88f,0,Facing).Planted(1).Drop(-.54f).J(B,.48f).J(Hd,-.4f,.05f,0);
            k.Key(EyesAt,crouch).Root(0,.88f,0,Facing).Planted(1).Drop(-.55f).J(B,.49f).J(Hd,-.45f,.05f,0);
            // It turns its head down to the prey below and shifts its weight onto the forward foot.
            k.Key(RevealAt+.35f,crouch).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.56f).J(B,.54f).J(N,.08f).J(Hd,-.22f,-.25f,0);
            k.Key(CoilAt-.2f,crouch).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.56f).J(B,.54f).J(N,.06f).J(Hd,-.25f,-.2f,0);
            // The coil: lower still, the claws cocked by the shoulders, the haunches loaded, the eyes on the lens.
            k.Key(LeapAt-.06f,crouch).Root(0,.88f,-.02f,Facing).Planted(1).Drop(-.62f).J(B,.56f).J(Hd,-.5f,0,0);
            // The leap: the legs drive it out long, the arms swinging forward.
            k.Key(LeapAt+.12f,NativeEntranceKeys.Stand()).Root(0,.88f,0,Facing).Planted(0).Drop(-.03f).J(B,.5f).J(Sp,.05f).J(C,-.05f,0,0).J(Hd,-.2f,0,0).J(LL,.2f,0,.1f).J(LK,.2f).J(RL,.28f,0,-.1f).J(RK,.3f).PassKey();
            // The top: pitched forward over the lens, the fangs raised and spread, the legs trailing behind with the knees bent.
            k.Key(ApexAt,NativeEntranceKeys.Stand()).Root(0,.88f,0,Facing).Planted(0).Drop(0).J(B,.55f).J(C,-.12f,0,0).J(Hd,-.3f,0,0).J(LL,-.3f,0,.1f).J(LK,1.1f).J(RL,-.15f,0,-.1f).J(RK,1.3f).PassKey();
            // Falling into the strike: the knees come through, the fangs high.
            k.Key(TouchAt-.07f,NativeEntranceKeys.Stand()).Root(0,.88f,0,Facing).Planted(0).Drop(-.02f).J(B,.25f).J(C,-.08f,0,0).J(Hd,-.15f,0,0).J(LL,-.55f,0,.12f).J(LK,.45f).J(RL,-.35f,0,-.12f).J(RK,.5f);
            // The feet find the floor a moment before the fangs do.
            k.Key(TouchAt,NativeEntranceKeys.Stand()).Root(0,.88f,0,Facing).Planted(1).Drop(-.22f).J(B,.4f).J(C,-.02f,0,0).J(Hd,-.3f,0,0);
            // The strike: both fangs driven into the earth, a deep crouch over them, the head up at the lens.
            k.Key(LandAt,NativeIntroPoses.Landing()).Root(0,.88f,0,Facing).Planted(1).Drop(-.56f).J(B,.62f).J(Sp,.1f).J(N,-.12f).J(Hd,-.4f,0,0).ImpactKey();
            k.Key(LandAt+.35f,NativeIntroPoses.Landing()).Root(0,.88f,0,Facing).Planted(1).Drop(-.6f).J(B,.64f).J(Sp,.1f).J(N,-.14f).J(Hd,-.42f,0,0);
            // It rises slowly, the fangs drawn out of the earth and spread low, into a hunter's stance.
            k.Key(RiseAt+.15f,NativeEntranceKeys.Stand()).Root(0,.88f,0,Facing).Planted(1).Drop(-.32f).J(B,.3f).J(Sp,.06f).J(C,.04f,0,0).J(Hd,-.18f,0,0);
            k.Key(StandAt+.5f,NativeIntroPoses.HeroWide()).Root(0,.88f,0,Facing).Planted(1).Drop(-.16f).J(B,.16f).J(Sp,.04f).J(Hd,-.06f,0,0);
            k.Key(Total,NativeIntroPoses.HeroWide()).Root(0,.88f,0,Facing).Planted(1).Drop(-.16f).J(B,.14f).J(Sp,.04f).J(C,0,.04f,0).J(Hd,-.08f,.04f,0);Keys=k;
            // ---- the fangs' ways (each hand's grip and its blade, in the keyed chest's frame) ----
            RightFang.InChest().Key(0,V(-.2f,-.33f,.32f),V(-.22f,-.7f,.68f)).Key(CoilAt-.2f,V(-.2f,-.32f,.32f),V(-.22f,-.69f,.69f))
                .Key(LeapAt-.06f,V(-.46f,0,.08f),V(-.3f,.51f,.81f))
                .Key(LeapAt+.12f,V(-.2f,.15f,.35f),V(0,.69f,.72f)).Pass()
                .Key(ApexAt,V(-.3f,.38f,.3f),V(-.74f,.4f,.54f)).Pass()
                .Key(LandAt-.1f,V(-.22f,.4f,.3f),V(-.25f,-.65f,.72f))
                .Key(TouchAt-.01f,V(-.21f,.05f,.34f),V(-.34f,-.81f,.48f)).Pass()
                .Key(TouchAt+.02f,V(-.22f,-.15f,.36f),V(-.41f,-.65f,.65f))
                .Key(RiseAt+.15f,V(-.42f,-.25f,.2f),V(-.35f,-.5f,.8f))
                .Key(StandAt+.5f,V(-.4f,-.3f,.18f),V(-.25f,-.45f,.86f))
                .Key(Total,V(-.4f,-.3f,.18f),V(-.25f,-.45f,.86f));
            LeftFang.InChest().Key(0,V(.38f,.02f,.04f),V(.3f,.51f,.81f)).Key(CoilAt-.2f,V(.38f,.03f,.04f),V(.3f,.52f,.8f))
                .Key(LeapAt-.06f,V(.46f,0,.08f),V(.3f,.51f,.81f))
                .Key(LeapAt+.12f,V(.2f,.15f,.35f),V(0,.69f,.72f)).Pass()
                .Key(ApexAt,V(.3f,.38f,.3f),V(.74f,.4f,.54f)).Pass()
                .Key(LandAt-.1f,V(.22f,.4f,.3f),V(.25f,-.65f,.72f))
                .Key(TouchAt-.01f,V(.21f,.05f,.34f),V(.34f,-.81f,.48f)).Pass()
                .Key(TouchAt+.02f,V(.22f,-.15f,.36f),V(.41f,-.65f,.65f))
                .Key(RiseAt+.15f,V(.42f,-.25f,.2f),V(.35f,-.5f,.8f))
                .Key(StandAt+.5f,V(.4f,-.3f,.18f),V(.25f,-.45f,.86f))
                .Key(Total,V(.4f,-.3f,.18f),V(.25f,-.45f,.86f));
            // The camera: low through the ferns (a little handheld), up to the lintel as the eyes open, close on the head as it launches, then
            // beside it riding the pounce (its line across the frame, slowed at the top), low and wide on the strike from the same side, the hero.
            var perchHead=Perch+new Vector3(0,.8f+NativeEntranceStage.FloorY,0);
            Rig.Dolly(0,V(1.9f,.55f,5.6f),V(1.25f,.62f,4.3f),V(0,2.4f,-3),V(.3f,2.95f,-3),40,36).Ease(NativeIntroEase.Linear).Handheld(.7f)
               .Crane(RevealAt,V(-1.8f,1.35f,1.0f),V(-1.05f,2.0f,-.6f),.35f,perchHead+V(0,-.1f,0),perchHead,33,27).Handheld(.35f)
               .Dolly(CoilAt-.05f,perchHead+V(-1.1f,.12f,1.9f),perchHead+V(-1.0f,.08f,1.7f),perchHead+V(0,-.2f,0),perchHead+V(0,-.24f,0),30,28).Focus(0,0,.5f)
               .Dolly(LeapAt+.08f,V(3.75f,4.2f,-2.05f),V(3.1f,1.5f,.75f),V(0,.12f,0),V(0,.05f,0),42,52).Follow(NativeEntranceAnchor.Chest).Ease(NativeIntroEase.InOut)
               .Dolly(LandAt-.02f,V(2.4f,.9f,2.0f),V(2.55f,.95f,2.2f),V(0,.55f,.35f),V(0,.62f,.3f),46,45);
            HeroFull(RiseAt+.05f,-1,4.4f,32,.35f);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
            Ramps.Ramp(LeapAt+.18f,.32f,.3f,.9f);
        }
        public override string SetBank=>Bank;
        /// <summary>The pilot's way from the keyed root (source frame): on the lintel, then the leap's arc down to the landing.</summary>
        public static Vector3 Way(float t)
        {
            var perch=Perch;
            if(t<LeapAt)return perch;
            if(t<TouchAt){float u=(t-LeapAt)/(TouchAt-LeapAt);var p=Vector3.Lerp(perch,Landing,u);p.Y=perch.Y*(1-u)+Landing.Y*u+LeapRise*4*u*(1-u);return p;}
            return Landing;
        }

        // ---- the body: keyed (no match clips): both arms solved onto the fangs' ways, the feet planted on the lintel's top until the leap ----
        private readonly NativeIntroWield RightFang=new NativeIntroWield(),LeftFang=new NativeIntroWield();
        protected override float TipLength(bool left)=>FangLength;
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            // Both fangs on their ways; from the strike until the rise, their points in the earth before the feet (the drawn grips there).
            float bite=NativeEntranceMath.Smooth((t-(TouchAt+.02f))/(LandAt-TouchAt-.02f))*(1-NativeEntranceMath.Smooth((t-RiseAt)/.25f));
            for(int side=0;side<2;side++)
            {
                bool left=side==1;var path=left?LeftFang:RightFang;
                if(bite<.001f){Wield(path,t,pose,root,rotation,left);continue;}
                float x=left?.24f:-.24f;var blade=Vector3.Normalize(Local(left?.1f:-.1f,-.72f,.68f));var tip=Landing+Local(x,0,.62f)+new Vector3(0,NativeEntranceStage.FloorY-.04f,0);
                var at=DrawnTarget(pose,root,rotation,left,tip-blade*FangLength);var axis=blade;
                if(bite<.999f&&WieldTarget(path,t,pose,root,rotation,out var pathAt,out var pathAxis,out _)){at=Vector3.Lerp(pathAt,at,bite);axis=Vector3.Normalize(Vector3.Lerp(pathAxis,axis,bite));}
                HoldAt(pose,root,rotation,left,at,axis,1);
            }
        }
        private const float FangLength=.42f;
        /// <summary>The lintel's top under the feet until they push off it; the floor after.</summary>
        protected override float ModelGround(float t)=>t<LeapAt+.1f?NativeEntranceStage.FloorY+Perch.Y:NativeEntranceStage.FloorY;
        /// <summary>The feet leave the lintel and come down in the strike (their planting blends out and in as they go and arrive).</summary>
        public override bool FeetSliding(float t)=>t>=LeapAt-.06f&&t<LeapAt+.12f||t>=TouchAt-.1f&&t<TouchAt+.03f;

        // ---- the head as drawn (for the eyes: the base's Eyes) ----
        private Vector3 HeadAt(float t)=>Anchor(NativeEntranceAnchor.Eyes,t);

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("canopy/static"))return false;
            // The bracket fungi's faint glow (the glow sub-mesh), breathing slowly.
            float g=.8f+.2f*(float)Math.Sin(t*.9f);set.Piece("canopy/static",SetAt,Quaternion.Identity,1,White,White*g);return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x0f1610),Fumes,.8f,NativeEntranceFx.Hex(0x1f3b27));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x0e1a15);l.FogNear=6;l.FogFar=34;l.DriveProbe=true;
            // (brighter than the first pass, which lost the ruins in the dark: a moonlit night, the stone and the leaves readable)
            l.Sky=NativeEntranceFx.Hex(0x2c4540,.62f);l.Ground=NativeEntranceFx.Hex(0x141a12,.42f);
            l.ReflectSky=NativeEntranceFx.Hex(0x2a4058);l.ReflectHorizon=NativeEntranceFx.Hex(0x1c3a2c);
            // (the set takes only directionals and the ambient: the moon's shaft down through the canopy's gap, a cold rim from the gate, a
            // soft fill from the lens's side off the mist)
            l.Directional(new Vector3(.25f,1,.35f),Moon*1.0f);l.Directional(new Vector3(-.3f,.45f,-1),Moon*.45f);l.Directional(new Vector3(.4f,.25f,1),Fumes*.18f+Moon*.14f);
            // No sky through the canopy: a shaft of moonlight onto the lintel and the floor, a cold rim from behind the gate, the venom's glow.
            l.Add(new Vector3(.4f,6.5f,-.8f),Moon*4.2f,10);
            l.Add(new Vector3(-.8f,4.6f,-5.2f),Moon*2.0f,7.5f);
            l.Add(new Vector3(1.6f,1.4f,3.2f),Moon*.9f,6);
            float eyes=NativeEntranceMath.Ramp(t,EyesAt,EyesAt+.4f)*(1-NativeEntranceMath.Ramp(t,LeapAt,LeapAt+.3f));
            Hand(t,0,out var rh);l.Add(rh+new Vector3(0,.1f,.1f),Venom*(.8f+.6f*eyes),2.6f);
            if(eyes>.01f)l.Add(HeadAt(t)+new Vector3(0,.05f,.3f),Venom*(.9f*eyes),2.2f);
            float strike=Flash(t,LandAt,.015f,.3f);if(strike>.01f)l.Add(new Vector3(0,.4f,.6f),(Venom*2+Bile)*(3.2f*strike),6);
            float name=NativeEntranceMath.Ramp(t,CardAt-.25f,CardAt+.35f);if(name>.01f)l.Add(new Vector3(1.8f,2.0f,1.6f),Fumes*(1.6f*name),7);
        }

        // ---- the effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY;
            // Fireflies drifting through the dark, glowing up and down slowly.
            for(int i=0,n=low?12:28;i<n;i++)
            {
                var p=new Vector3(NativeEntranceMath.Signed(34001,i)*6.5f,.4f+2.6f*NativeEntranceMath.Rand(34003,i),-6f+10.5f*NativeEntranceMath.Rand(34005,i));
                p+=new Vector3((float)Math.Sin(t*.37f+i*1.3f)*.35f,(float)Math.Sin(t*.51f+i*2.1f)*.18f,(float)Math.Cos(t*.29f+i*.7f)*.35f);
                float glow=.5f+.5f*(float)Math.Sin(t*(1.1f+.6f*NativeEntranceMath.Rand(34007,i))+i*2.3f);fx.Glow(p,Firefly*(.55f*glow*glow),0,.035f,0,0,34010+i);
            }
            // Leaves spinning down out of the canopy.
            for(int i=0,n=low?5:12;i<n;i++)
            {
                float per=5.5f+2*NativeEntranceMath.Rand(34151,i),u=((t/per)+NativeEntranceMath.Rand(34153,i))%1f,y=7.5f-7.6f*u;if(y<floor+.02f)continue;
                var p=new Vector3(NativeEntranceMath.Signed(34155,i)*5+.4f*(float)Math.Sin(t*1.7f+i),y,-5+9*NativeEntranceMath.Rand(34157,i)+.3f*(float)Math.Cos(t*1.3f+i*2));
                var d=new Vector3((float)Math.Cos(t*3.1f+i),.35f*(float)Math.Sin(t*2.3f+i),(float)Math.Sin(t*3.1f+i))*.05f;fx.Streak(p+d,d*2,Leaf*.9f,.035f,.05f,1,.1f,0,34160+i);
            }
            // Mist low over the floor, drifting.
            for(int i=0,n=low?5:10;i<n;i++)
            {
                var p=new Vector3(NativeEntranceMath.Signed(34101,i)*5.5f+.2f*(float)Math.Sin(t*.2f+i),floor+.25f,-5.5f+9f*NativeEntranceMath.Rand(34103,i));
                fx.Smoke(p,Mist,.12f,1.5f+.9f*NativeEntranceMath.Rand(34105,i),.5f,34110+i,floor-.05f);
            }
            // The eyes: two green slits on the dark lintel, opening; they fade as it leaps.
            float eyes=NativeEntranceMath.Ramp(t,EyesAt,EyesAt+.35f)*(1-NativeEntranceMath.Ramp(t,LeapAt-.05f,LeapAt+.2f));
            if(eyes>.01f)
            {
                var h=HeadAt(t)+new Vector3(0,.04f,.11f);var across=Vector3.Transform(Vector3.UnitX,Quaternion.CreateFromAxisAngle(Vector3.UnitY,Facing));
                for(int s=-1;s<=1;s+=2){var e=h+across*(.045f*s);fx.Streak(e+across*.022f,across*.044f,(Venom*1.6f+Bile)*(1.6f*eyes),.007f,0,1,.5f,0,34200+s+1);fx.Glow(e,Venom*(.5f*eyes),0,.06f,0,0,34204+s);}
            }
            // Venom beading on both fangs and dripping (from the blades as the model holds them).
            for(int hand=0;hand<2;hand++)
            {
                if(!Held(t,hand,out _,out var tip))continue;
                float per=.42f+.08f*hand,u=((t/per)+hand*.37f)%1f;
                if(u<.55f){float swell=u/.55f;fx.Glow(tip,(Venom*1.2f+Bile*.4f)*(.6f+.6f*swell),0,.012f+.02f*swell,0,0,34300+hand);}
                else{float a=(u-.55f)*per;fx.Streak(tip-new Vector3(0,4.9f*a*a,0),new Vector3(0,-9.8f*a,0),Venom*1.3f,.006f,.02f,.02f,.5f,0,34310+hand);}
            }
            // The leap: green trails from both fangs.
            if(t>=LeapAt-.1f&&t<RiseAt){Trail(fx,t,0,.24f,Fumes,Bile,.016f,1.1f,34400,low);Trail(fx,t,1,.24f,Fumes,Bile,.016f,1.1f,34500,low);}
            // The strike into the earth: a bloom of venom thrown out low, leaves and earth kicked up, the mist pushed out, a flash at the fangs.
            float a2=t-LandAt;
            if(a2>=0&&a2<1.6f)
            {
                var at=new Vector3(0,floor+.02f,.75f);
                if(a2<.18f)fx.Glow(at+new Vector3(0,.15f,0),(Venom*2+Bile)*(1.8f*(1-a2/.18f)),0,.3f+.6f*a2,1,a2/.18f,34600);
                int m=low?18:42;
                for(int i=0;i<m;i++)
                {
                    float life=.5f+.45f*NativeEntranceMath.Rand(34611,i);if(a2>life)continue;float u=a2/life,ang=i*6.2831853f/m+.25f*NativeEntranceMath.Signed(34613,i),sp=2.2f+2.6f*NativeEntranceMath.Rand(34615,i),up=.6f+1.8f*NativeEntranceMath.Rand(34617,i);
                    var v0=new Vector3((float)Math.Cos(ang)*sp,up,(float)Math.Sin(ang)*sp*.8f+.4f);var p=at+v0*a2+new Vector3(0,-4.9f*a2*a2,0);if(p.Y<floor){p.Y=floor+.005f;}
                    fx.Streak(p,v0+new Vector3(0,-9.8f*a2,0),(i%4==0?Bile:Venom)*(1.6f*(1-u)),.009f,.03f,.035f,.6f,u,34620+i);
                }
                int e=low?8:20;
                for(int i=0;i<e;i++)
                {
                    float life=.7f+.5f*NativeEntranceMath.Rand(34711,i);if(a2>life)continue;float u=a2/life,ang=NativeEntranceMath.Rand(34713,i)*6.2831853f,sp=1.4f+1.8f*NativeEntranceMath.Rand(34715,i);
                    var v0=new Vector3((float)Math.Cos(ang)*sp,1.6f+1.8f*NativeEntranceMath.Rand(34717,i),(float)Math.Sin(ang)*sp);var p=at+v0*a2+new Vector3(0,-4.2f*a2*a2,0);if(p.Y<floor)continue;
                    fx.Streak(p,v0+new Vector3(0,-8.4f*a2,0),(i%2==0?Leaf:Earth)*(1.1f*(1-u*u)),.012f,.025f,.02f,.3f,u,34720+i);
                }
                NativeEntranceFx.GroundDust(fx,t,LandAt,at,low?6:12,.4f,3.2f,1.4f,Mist*1.4f,.35f,.7f,34800,q,floor);
            }
            // Under the name: venom smoking off the fangs.
            if(t>=RiseAt)for(int hand=0;hand<2;hand++){if(!Held(t,hand,out _,out var tip))continue;for(int i=0;i<(low?2:4);i++){float u=((t*.6f+i*.25f+hand*.13f)%1f);fx.Smoke(tip+new Vector3(.05f*(float)Math.Sin(t+i),.05f+.35f*u,.05f*(float)Math.Cos(t*.8f+i)),Fumes*.5f,.12f*(1-u),.06f+.14f*u,u,34900+hand*8+i,floor);}}
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.apex.night",null,"darkness",.3f,.6f),
            new NativeIntroCue(EyesAt,"intro2.apex.hiss","magic.darkness.hit","darkness",.5f,.6f),
            new NativeIntroCue(CoilAt,"intro2.apex.coil","melee.daggers.swing","daggers",.3f,.8f),
            new NativeIntroCue(LeapAt,"intro2.apex.leap","boss.noctiel.leap","dash",.7f),
            new NativeIntroCue(LandAt,"intro2.apex.strike","melee.daggers.hit","hit",1f),
            new NativeIntroCue(LandAt+.03f,"intro2.apex.venom","magic.water.hit","water",.7f,1.3f),
            new NativeIntroCue(RiseAt+.05f,"intro2.apex.rise","boss.noctiel.sword.pull","daggers",.3f,1.4f),
            new NativeIntroCue(CardAt,"intro2.apex.name","magic.darkness.hit","poison-tick",.5f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.94f,1,.9f),Venom*1.2f);card.Style=NativeIntroCardStyle.Rise;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
