using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Apex", the Fangs' intro (intros v2; docs/INTROS-MELEE.md): a predator's pounce. 7.4 s, the name at 5.9 s. Night in the Canopy
    /// (overgrown temple ruins: a carved gateway, a strangler fig, roots over the flagstones, fireflies in the mist). The camera drifts low
    /// through the ferns like prey looking round; two green eyes open on the gateway's lintel; it is the pilot, crouched up there, the fangs
    /// out, venom beading on their tips. It coils, and leaps (slowed at the top of its arc) straight at the lens; both fangs go into the
    /// earth in a bloom of venom, leaves and mist thrown out. It rises, venom dripping, under the name.
    /// The model plays the Frank Slash Pack's assassin and rogue clips (NativeBladeIntro); the keyed root carries it off the lintel.
    /// Presentation only: pure functions of the presentation clock.
    /// </summary>
    public sealed class NativeIntroApex:NativeIntroMeleeBlade
    {
        public const string Title="Apex";
        public const float Total=7.4f,CardAt=5.9f;
        /// <summary>The beats (presentation s): the eyes, the reveal, the coil, the leap, its top, the landing strike, the rise.</summary>
        public const float EyesAt=1.05f,RevealAt=1.45f,CoilAt=2.75f,LeapAt=3.25f,ApexAt=3.55f,LandAt=3.95f,RiseAt=4.55f,StandAt=4.75f;
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
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips): the root stands at .88 over the surface under it (the lintel's
            // top, then the floor: Way carries it), the crouch is the body's drop; on the lintel the feet are planted on its top (Arms) ----
            Start=Stance(.2f,.18f,.2f,-.22f,-.2f,-.5f);
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // On the lintel: a low predator's crouch, leaning out over the edge, head up, one fang down at the stone, the other back.
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.44f).J(B,.55f).J(Sp,.12f).J(C,.08f,.05f,0).J(Hd,-.5f,.05f,0).J(LL,-1.3f,0,.22f).J(LK,2.0f).J(RL,-.9f,0,-.2f).J(RK,2.1f)
             .J(RA,-.8f,0,-.3f).J(RE,-.4f).J(LA,.3f,0,.4f).J(LE,-.6f);
            k.Key(EyesAt,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.45f).J(B,.56f).J(Sp,.12f).J(C,.08f,.05f,0).J(Hd,-.58f,.05f,0).J(LL,-1.3f,0,.22f).J(LK,2.0f).J(RL,-.9f,0,-.2f).J(RK,2.1f)
             .J(RA,-.8f,0,-.3f).J(RE,-.4f).J(LA,.3f,0,.4f).J(LE,-.6f);
            // It turns its head down to the prey below and shifts its weight onto the forward foot.
            k.Key(RevealAt+.35f,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.46f).J(B,.6f).J(Sp,.12f).J(C,.1f,-.08f,0).J(Hd,-.3f,-.25f,0).J(LL,-1.35f,0,.22f).J(LK,2.05f).J(RL,-.85f,0,-.2f).J(RK,2.05f)
             .J(RA,-.85f,0,-.3f).J(RE,-.45f).J(LA,.25f,0,.4f).J(LE,-.65f);
            k.Key(CoilAt-.2f,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.46f).J(B,.6f).J(Sp,.12f).J(C,.1f,-.06f,0).J(Hd,-.3f,-.2f,0).J(LL,-1.35f,0,.22f).J(LK,2.05f).J(RL,-.85f,0,-.2f).J(RK,2.05f)
             .J(RA,-.85f,0,-.3f).J(RE,-.45f).J(LA,.25f,0,.4f).J(LE,-.65f);
            // The coil: lower still, both fangs drawn back along the body, the haunches loaded.
            k.Key(LeapAt-.06f,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.52f).J(B,.7f).J(Sp,.14f).J(C,.12f,0,0).J(Hd,-.45f,0,0).J(LL,-1.45f,0,.22f).J(LK,2.2f).J(RL,-1.0f,0,-.2f).J(RK,2.2f)
             .J(RA,.6f,0,-.35f).J(RE,-.5f).J(LA,.6f,0,.35f).J(LE,-.5f);
            // The leap: the legs drive it out long, the arms swinging forward.
            k.Key(LeapAt+.12f,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.05f).J(B,.35f).J(Sp,.05f).J(C,-.05f,0,0).J(Hd,-.2f,0,0).J(LL,-.2f,0,.14f).J(LK,.2f).J(RL,.25f,0,-.14f).J(RK,.35f)
             .J(RA,-1.4f,0,-.25f).J(RE,-.3f).J(LA,-1.4f,0,.25f).J(LE,-.3f).PassKey();
            // The top: stretched out over the lens, the fangs raised, the legs trailing.
            k.Key(ApexAt,stand).Root(0,.88f,0,Facing).Planted(0).Drop(0).J(B,.3f).J(Sp,0).J(C,-.12f,0,0).J(Hd,-.1f,0,0).J(LL,-.35f,0,.14f).J(LK,1.1f).J(RL,.15f,0,-.14f).J(RK,1.35f)
             .J(RA,-2.3f,0,-.3f).J(RE,-.6f).J(LA,-2.3f,0,.3f).J(LE,-.6f).PassKey();
            // Falling into the strike: the knees come through, the fangs high.
            k.Key(LandAt-.1f,stand).Root(0,.88f,0,Facing).Planted(0).Drop(-.05f).J(B,.25f).J(C,-.1f,0,0).J(Hd,-.15f,0,0).J(LL,-.9f,0,.16f).J(LK,1.0f).J(RL,-.6f,0,-.16f).J(RK,1.1f)
             .J(RA,-2.6f,0,-.35f).J(RE,-.5f).J(LA,-2.6f,0,.35f).J(LE,-.5f);
            // The strike: both fangs driven into the earth, a deep crouch, the head up at the lens.
            k.Key(LandAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.42f).J(B,.6f).J(Sp,.12f).J(C,.12f,0,0).J(Hd,-.45f,0,0).J(LL,-1.25f,0,.2f).J(LK,1.9f).J(RL,-.75f,0,-.2f).J(RK,1.95f)
             .J(RA,-.9f,0,-.25f).J(RE,-.2f).J(LA,-.9f,0,.25f).J(LE,-.2f).ImpactKey();
            k.Key(LandAt+.35f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.45f).J(B,.62f).J(Sp,.12f).J(C,.12f,0,0).J(Hd,-.5f,0,0).J(LL,-1.28f,0,.2f).J(LK,1.95f).J(RL,-.78f,0,-.2f).J(RK,2.0f)
             .J(RA,-.9f,0,-.25f).J(RE,-.2f).J(LA,-.9f,0,.25f).J(LE,-.2f);
            // It rises slowly, the fangs drawn out of the earth and spread low, into a hunter's stance.
            k.Key(RiseAt+.15f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.26f).J(B,.35f).J(Sp,.06f).J(C,.04f,0,0).J(Hd,-.2f,0,0).J(LL,-.8f,0,.2f).J(LK,1.2f).J(RL,-.45f,0,-.2f).J(RK,1.25f)
             .J(RA,-.6f,0,-.7f).J(RE,-.3f).J(LA,-.6f,0,.7f).J(LE,-.3f);
            k.Key(StandAt+.5f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.17f).J(B,.18f).J(Sp,.04f).J(C,0,0,0).J(Hd,-.06f,0,0).J(LL,-.45f,0,.2f).J(LK,.7f).J(RL,-.2f,0,-.2f).J(RK,.75f)
             .J(RA,-.45f,0,-.75f).J(RE,-.35f).J(LA,-.45f,0,.75f).J(LE,-.35f);
            k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.17f).J(B,.16f).J(Sp,.04f).J(C,0,.04f,0).J(Hd,-.08f,.04f,0).J(LL,-.45f,0,.2f).J(LK,.7f).J(RL,-.2f,0,-.2f).J(RK,.75f)
             .J(RA,-.45f,0,-.75f).J(RE,-.35f).J(LA,-.45f,0,.75f).J(LE,-.35f);Keys=k;
            // ---- the fangs' ways (each hand's grip and its blade, in the pilot's frame over the surface it stands on) ----
            RightFang.Key(0,V(-.2f,.32f,.48f),V(0,-.65f,.76f)).Key(CoilAt-.2f,V(-.22f,.3f,.5f),V(0,-.7f,.71f))
                .Key(LeapAt-.06f,V(-.28f,.55f,-.12f),V(0,.15f,-.99f))
                .Key(LeapAt+.12f,V(-.22f,1.1f,.42f),V(0,.35f,.94f)).Pass()
                .Key(ApexAt,V(-.24f,1.5f,.42f),V(0,-.45f,.89f)).Pass()
                .Key(LandAt-.1f,V(-.22f,1.62f,.22f),V(0,-.1f,.99f))
                .Key(LandAt,V(-.2f,.3f,.62f),V(0,-.95f,.3f)).Impact()
                .Key(LandAt+.35f,V(-.2f,.28f,.6f),V(0,-.96f,.28f))
                .Key(RiseAt+.15f,V(-.34f,.66f,.48f),V(-.4f,-.6f,.69f))
                .Key(StandAt+.5f,V(-.42f,.8f,.26f),V(-.6f,-.55f,.58f))
                .Key(Total,V(-.42f,.8f,.26f),V(-.6f,-.55f,.58f));
            LeftFang.Key(0,V(.26f,.5f,-.08f),V(.15f,-.45f,-.88f)).Key(CoilAt-.2f,V(.26f,.5f,-.06f),V(.15f,-.45f,-.88f))
                .Key(LeapAt-.06f,V(.28f,.55f,-.12f),V(0,.15f,-.99f))
                .Key(LeapAt+.12f,V(.22f,1.1f,.42f),V(0,.35f,.94f)).Pass()
                .Key(ApexAt,V(.24f,1.5f,.42f),V(0,-.45f,.89f)).Pass()
                .Key(LandAt-.1f,V(.22f,1.62f,.22f),V(0,-.1f,.99f))
                .Key(LandAt,V(.2f,.3f,.62f),V(0,-.95f,.3f)).Impact()
                .Key(LandAt+.35f,V(.2f,.28f,.6f),V(0,-.96f,.28f))
                .Key(RiseAt+.15f,V(.34f,.66f,.48f),V(.4f,-.6f,.69f))
                .Key(StandAt+.5f,V(.42f,.8f,.26f),V(.6f,-.55f,.58f))
                .Key(Total,V(.42f,.8f,.26f),V(.6f,-.55f,.58f));
            // The camera: low through the ferns (a little handheld), up to the lintel as the eyes open, close on the head, the prey's eye on the
            // floor as it leaps at the lens (slowed at the top), low and wide on the strike, the hero.
            var perchHead=Perch+new Vector3(0,.8f+NativeEntranceStage.FloorY,0);
            Rig.Dolly(0,V(1.9f,.55f,5.6f),V(1.25f,.62f,4.3f),V(0,2.4f,-3),V(.3f,2.95f,-3),40,36).Ease(NativeIntroEase.Linear).Handheld(.7f)
               .Crane(RevealAt,V(-1.8f,1.35f,1.0f),V(-1.05f,2.0f,-.6f),.35f,perchHead+V(0,-.1f,0),perchHead,33,27).Handheld(.35f)
               .Dolly(CoilAt-.05f,perchHead+V(-1.1f,.12f,1.9f),perchHead+V(-1.0f,.08f,1.7f),perchHead+V(0,-.2f,0),perchHead+V(0,-.24f,0),30,28).Focus(0,0,.5f)
               .Dolly(LeapAt-.06f,V(.35f,.42f,1.8f),V(.36f,.4f,1.82f),V(0,.25f,0),V(0,-.1f,0),50,55).Follow(NativeEntranceAnchor.Chest,.04f).Ease(NativeIntroEase.InOut)
               .Dolly(LandAt-.02f,V(-2.7f,.45f,2.0f),V(-2.85f,.5f,2.2f),V(0,.55f,.35f),V(0,.62f,.3f),46,45);
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
            if(t<LandAt){float u=(t-LeapAt)/(LandAt-LeapAt);var p=Vector3.Lerp(perch,Landing,u);p.Y=perch.Y*(1-u)+Landing.Y*u+LeapRise*4*u*(1-u);return p;}
            return Landing;
        }

        // ---- the body: keyed (no match clips): both arms solved onto the fangs' ways, the feet planted on the lintel's top until the leap ----
        private readonly NativeIntroWield RightFang=new NativeIntroWield(),LeftFang=new NativeIntroWield();
        protected override float TipLength(bool left)=>.42f;
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            if(t<LeapAt+.06f)
            {
                // (the lintel is no floor: the feet stand on its top by leg IK, lifted to it, until they push off)
                float on=1-NativeEntranceMath.Ramp(t,LeapAt-.02f,LeapAt+.06f);var l=Perch+Local(.18f,0,.12f);var r=Perch+Local(-.2f,0,-.16f);
                NativeEntrancePose.PlantFoot(pose,root,rotation,true,l.X,l.Z,Facing+.2f,on,Perch.Y);NativeEntrancePose.PlantFoot(pose,root,rotation,false,r.X,r.Z,Facing-.4f,on,Perch.Y);
            }
            Wield(RightFang,t,pose,root,rotation,false);Wield(LeftFang,t,pose,root,rotation,true);
        }

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
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x07100b);l.FogNear=5;l.FogFar=27;l.DriveProbe=true;
            l.Sky=NativeEntranceFx.Hex(0x1b2a24,.4f);l.Ground=NativeEntranceFx.Hex(0x0a0c08,.3f);
            // (the set takes only directionals and the ambient: the moon's shaft down through the canopy's gap, a cold rim from the gate)
            l.Directional(new Vector3(.25f,1,.35f),Moon*.5f);l.Directional(new Vector3(-.3f,.45f,-1),Moon*.22f);
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
