using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Faultline", the greatsword's intro (intros v2; docs/INTROS-MELEE.md): the weight that cracks the ground. 8.0 s, the name at 6.4 s.
    /// Night in the Rift (a basalt quarry: columnar walls in benches, a molten seam smouldering in a corner, a timber derrick, quarry
    /// blocks). The camera glides low over the floor to the pilot in a middle guard; embers drift up from the faults. It leaps and chops the
    /// great sword over and down (slowed into the blow): the point bites the floor ahead, the shock throws the left hand off the hilt, and the
    /// floor round it breaks into slabs that heave up and tilt on a wave running outward, molten light wells up through the joints, cracks
    /// race to the walls, grit and sparks fly. The camera cranes up over the broken ring; the pilot wrenches the blade out, sets its point on
    /// the floor ahead and stands beside it in the glow.
    /// The body is keyed (no match clips), both hands on the hilt through the grip (INativeIntroGrip) until the shock. Presentation only.
    /// </summary>
    public sealed class NativeIntroFaultline:NativeIntroMeleeBlade,INativeIntroGrip
    {
        public const string Title="Faultline";
        public const float Total=8.0f,CardAt=6.4f;
        /// <summary>The beats (presentation s): the leap, the point biting the floor ahead, the blow (the feet down, the blade driven in), the
        /// cracks reaching the walls, the head up, the rise wrenching it out, the point grounded at the right side, the stance.</summary>
        public const float LeapAt=2.2f,ContactAt=2.79f,SlamAt=LeapAt+PlungeHit,SpreadAt=SlamAt+.5f,HeadUpAt=3.55f,RiseAt=3.85f,GroundAt=4.92f,StandAt=5.1f;
        private const float PlungeHit=.63f,SwordLength=1.69f;
        /// <summary>Where the blow's point goes into the floor (source frame: ahead of the pilot), how deep, and the blade's slope below level
        /// (a great sword strikes the floor far out: its grip then sits low before the knees in the squash).</summary>
        public static readonly Vector3 Bite=Local(0,0,1.75f);private const float Depth=.22f,Slope=.54f;
        /// <summary>Where its point rests on the floor at the end, out at the pilot's right front, and where the drawn grip holds it (the hip).</summary>
        public static readonly Vector3 Rest=Local(-.55f,0,1.55f),RestGrip=Local(-.27f,.86f,.12f);
        private static readonly Vector3 Ember=NativeEntranceFx.Hex(0xff8a3c),Molten=NativeEntranceFx.Hex(0xff5a14),Gold=NativeEntranceFx.Hex(0xffcb87),Night=NativeEntranceFx.Hex(0x0a0806),
            Dust=NativeEntranceFx.Hex(0x6e655c),Stone=NativeEntranceFx.Hex(0x3a3d42),Moon=NativeEntranceFx.Hex(0x9aaccc);
        public override string Spec=>"greatsword";
        // The rift set's fracture disc: its slabs' rests (Unity x, z), as melee_rift.py builds them.
        private static readonly Vector2[] Slabs={new Vector2(.015f,.015f),new Vector2(-.716f,-1.836f),new Vector2(1.448f,-1.371f),new Vector2(1.577f,1.162f),new Vector2(-.437f,1.956f),new Vector2(-1.993f,.140f),
            new Vector2(-.054f,-3.591f),new Vector2(2.655f,-2.502f),new Vector2(3.492f,.040f),new Vector2(2.336f,2.789f),new Vector2(-.329f,3.610f),new Vector2(-2.733f,2.273f),new Vector2(-3.705f,-.413f),new Vector2(-2.803f,-2.416f)};
        private static readonly string[] slabProps=MakeNames();
        private static string[] MakeNames(){var n=new string[14];for(int i=0;i<14;i++)n[i]="rift/slab-"+i;return n;}
        // The cracks that race from the disc to the walls (Unity x, z polylines on the floor).
        private static readonly Vector2[][] Cracks=
        {
            new[]{new Vector2(1.6f,-4.4f),new Vector2(2.9f,-6.8f),new Vector2(4.45f,-9.6f)},
            new[]{new Vector2(-1.8f,-4.3f),new Vector2(-3.0f,-6.9f),new Vector2(-4.05f,-9.45f)},
            new[]{new Vector2(-4.2f,1.9f),new Vector2(-6.2f,3.3f),new Vector2(-8.3f,4.5f)},
            new[]{new Vector2(4.6f,.6f),new Vector2(6.8f,1.4f),new Vector2(9.6f,.9f)},
            new[]{new Vector2(2.8f,3.8f),new Vector2(3.6f,6.2f),new Vector2(4.4f,9.0f)},
            new[]{new Vector2(-1.4f,4.5f),new Vector2(-2.0f,7.0f),new Vector2(-2.9f,9.8f)},
        };
        private const float CrackSpeed=11f;

        public NativeIntroFaultline():base(NativeIntroCatalog.WeaponPrefix+"greatsword",Total,CardAt,SlamAt,0xffcb87)
        {
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips; the feet and drops on the model, ModelFeet) ----
            // Feet (the model's ankles): a guard stance, left foot ahead; both leave the floor in the leap and come down a little wider; the
            // right foot squares up as it stands.
            Start=Stance(.14f,.12f,.12f,-.16f,-.14f,-.45f);
            Steps=new[]{Step(true,LeapAt,ContactAt-.06f,.18f,.2f,.14f,.16f),Step(false,LeapAt+.02f,ContactAt-.05f,-.2f,-.18f,-.48f,.16f),Step(false,GroundAt-.25f,GroundAt+.05f,-.17f,-.06f,-.4f,.07f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // On guard: the great sword low before the body in both hands, its point toward the floor ahead, the weight settled.
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.05f).J(B,.08f).J(C,.02f,-.1f,0).J(Hd,.04f,.08f,0);
            k.Key(1.45f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.06f).J(B,.09f).J(C,.03f,-.08f,0).J(Hd,.06f,.05f,0);
            // Anticipation: it sinks as the blade rises and tips back over the head, the chest opening, the eyes up.
            k.Key(1.9f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.14f).J(B,.06f).J(C,-.08f,-.04f,0).J(Hd,-.06f,0,0);
            k.Key(2.12f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.2f).J(B,.18f).J(Sp,.04f).J(C,-.14f,0,0).J(N,-.06f).J(Hd,-.1f,0,0);
            // Off the floor: the legs drive it up, long, the blade high behind.
            k.Key(LeapAt,stand).Root(0,.95f,.02f,Facing).Planted(.3f).Drop(0).J(B,-.02f).J(C,-.2f,0,0).J(Hd,-.1f,0,0).J(LL,-.1f,0,.1f).J(LK,.12f).J(RL,.12f,0,-.1f).J(RK,.15f).PassKey();
            // The top: the knees tucked, the body arched back under the blade.
            k.Key(LeapAt+.27f,stand).Root(0,1.28f,.04f,Facing).Planted(0).Drop(.02f).J(B,-.04f).J(C,-.14f,0,0).J(Hd,-.04f,0,0).J(LL,-1.0f,0,.14f).J(LK,1.3f).J(RL,-.7f,0,-.14f).J(RK,1.4f).PassKey();
            // Coming down with the blow: the body folds forward over the blade as it comes over, the legs reaching for the floor.
            k.Key(ContactAt-.04f,stand).Root(0,.98f,.05f,Facing).Planted(1).Drop(0).J(B,.24f).J(Sp,.06f).J(C,.1f,0,0).J(Hd,.06f,0,0).J(LA,-.7f,.05f,.75f).J(LE,-.55f).J(LL,-.4f,0,.12f).J(LK,.4f).J(RL,-.15f,0,-.12f).J(RK,.3f);
            // The blow: the feet down, a deep squash over the hilt, the head bowed.
            k.Key(SlamAt,stand).Root(0,.88f,.05f,Facing).Planted(1).Drop(-.3f).J(B,.36f).J(Sp,.08f).J(C,.1f,0,0).J(Hd,.2f,0,0).J(LA,-.35f,.05f,1.15f).J(LE,-.3f).ImpactKey();
            k.Key(SlamAt+.14f,stand).Root(0,.88f,.05f,Facing).Planted(1).Drop(-.4f).J(B,.42f).J(Sp,.1f).J(C,.12f,0,0).J(Hd,.3f,0,0).J(LA,-.3f,.05f,1.2f).J(LE,-.28f);
            k.Key(HeadUpAt-.15f,stand).Root(0,.88f,.05f,Facing).Planted(1).Drop(-.39f).J(B,.41f).J(Sp,.1f).J(C,.12f,0,0).J(Hd,.32f,0,0).J(LA,-.28f,.05f,1.0f).J(LE,-.35f);
            // The head comes up first; then it rises, wrenching the blade out, and swings it down to its right side.
            k.Key(HeadUpAt+.2f,stand).Root(0,.88f,.05f,Facing).Planted(1).Drop(-.38f).J(B,.36f).J(Sp,.08f).J(C,.02f,0,0).J(N,-.1f).J(Hd,-.24f,0,0).J(LA,-.25f,.05f,.85f).J(LE,-.4f);
            k.Key(RiseAt+.3f,stand).Root(0,.88f,.03f,Facing).Planted(1).Drop(-.2f).J(B,.12f).J(C,-.04f,-.14f,0).J(N,-.04f).J(Hd,-.12f,.06f,0).J(LA,-.15f,.06f,.4f).J(LE,-.5f).PassKey();
            k.Key(GroundAt,stand).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.08f).J(B,.06f).J(C,-.04f,-.12f,0).J(Hd,-.04f,.1f,0).J(LA,-.1f,.06f,.22f).J(LE,-.5f).ImpactKey();
            // The stance: tall beside the great sword resting on its point, the left fist low, the chest proud.
            k.Key(StandAt+.4f,stand).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.04f).J(B,.02f).J(C,-.08f,-.06f,0).J(Hd,-.06f,.06f,0).J(LA,-.08f,.06f,.2f).J(LE,-.42f);
            k.Key(Total,stand).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.04f).J(B,.02f).J(C,-.09f,-.06f,0).J(Hd,-.08f,.07f,0).J(LA,-.08f,.06f,.2f).J(LE,-.42f);Keys=k;
            // ---- the great sword's way (the right hand's grip and the blade, in the keyed chest's frame; the left on the hilt by the grip) ----
            Sword.InChest().Key(0,V(-.02f,-.13f,.3f),V(.08f,.64f,.76f))
                 .Key(1.45f,V(-.02f,-.12f,.3f),V(.08f,.66f,.75f))
                 .Key(1.9f,V(-.18f,.1f,.25f),V(.05f,.9f,-.4f))
                 .Key(2.02f,V(-.26f,.25f,.15f),V(-.5f,.75f,-.4f)).Pass()
                 .Key(2.12f,V(-.26f,.3f,.2f),V(-.55f,-.35f,-.76f))
                 .Key(LeapAt,V(-.24f,.34f,.2f),V(-.35f,-.39f,-.85f)).Pass()
                 .Key(LeapAt+.27f,V(-.14f,.46f,.2f),V(-.35f,-.2f,-.92f))
                 // (the chop comes over with the blade square to the forearms, and the wrists snap it down onto the floor only at the bite)
                 .Key(LeapAt+.38f,V(-.1f,.5f,.24f),V(0,.98f,-.2f)).Pass()
                 .Key(LeapAt+.46f,V(-.12f,.42f,.32f),V(0,.9f,-.42f)).Pass()
                 .Key(LeapAt+.51f,V(-.12f,.25f,.38f),V(0,.98f,.15f)).Pass()
                 .Key(ContactAt-.04f,V(-.12f,.05f,.42f),V(0,.75f,.66f))
                 .Key(RiseAt+.3f,V(-.15f,-.1f,.32f),V(.05f,.1f,.99f)).Pass()
                 .Key(4.55f,V(-.36f,-.22f,.16f),V(-.3f,-.5f,.81f))
                 .Key(Total,V(-.36f,-.24f,.15f),V(-.3f,-.52f,.8f));
            // The camera: low over the floor to the pilot; wide and low as it leaps; the blow, craning up over the breaking ring; high over the
            // cracks racing to the walls; the hero.
            Rig.Dolly(0,V(-2.6f,.42f,5.4f),V(-1.55f,.55f,3.55f),V(0,.85f,0),V(0,1.12f,0),44,40).Ease(NativeIntroEase.Out).Handheld(.3f)
               .Dolly(LeapAt-.15f,V(3.4f,.38f,1.7f),V(3.55f,.42f,1.95f),V(0,1.6f,0),V(0,1.2f,.1f),52,52)
               .Crane(SlamAt-.06f,V(-2.4f,1.0f,3.1f),V(-1.5f,1.5f,3.4f),5.2f,V(0,.4f,.6f),V(0,.2f,.4f),50,60).Ease(NativeIntroEase.InOut)
               .Dolly(SlamAt+1.15f,V(.9f,9.6f,3.4f),V(.4f,9.1f,1.9f),V(0,0,-.8f),V(0,0,-.4f),58,54);// (on the blow's clock: the cracks' later start must not crowd it into the hero)
            HeroFull(RiseAt+.3f,1,5.6f,33,.4f,0);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
            Ramps.Ramp(SlamAt-.2f,.25f,.3f,.9f);
        }
        public override string SetBank=>Bank;

        // ---- the body: keyed (no match clips), the arms solved onto the great sword's way ----
        private readonly NativeIntroWield Sword=new NativeIntroWield();
        protected override float TipLength(bool left)=>left?.15f:SwordLength;
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            // The right hand on its way (both hands on the hilt until it is wrenched out); from the moment the point bites, on the grip of the
            // blade driven into the floor ahead (the drawn grip where the blade's depth puts it); at the end, on the grip of the blade resting
            // on its point at the right side.
            TwoHands=Hands(t)*.9f;Wield(Sword,t,pose,root,rotation,false);
            float bite=NativeEntranceMath.Smooth((t-(ContactAt-.05f))/.05f)*(1-NativeEntranceMath.Smooth((t-RiseAt-.05f)/.3f));
            if(bite>.001f){HiltAt(t,out var grip,out var blade);HoldDrawn(pose,root,rotation,false,grip+BiteFix,blade,bite);}
            float rest=NativeEntranceMath.Smooth((t-(GroundAt-.22f))/.22f);
            if(rest>.001f)HoldDrawn(pose,root,rotation,false,RestGrip+RestFix+new Vector3(0,NativeEntranceStage.FloorY,0),Vector3.Normalize(Rest-RestGrip),rest);
        }
        /// <summary>Where the right hand aims so the DRAWN grip lands on the blow's and the rest's grips (the drawn arm is not the keyed arm
        /// scaled: measured on the model, the drawn grip sat 5 cm right, 10 cm high and 19 cm short of the blow's, and 8 cm right, 11 cm high
        /// and 3 cm short of the rest's; source frame).</summary>
        private static readonly Vector3 BiteFix=new Vector3(.045f,-.1f,.185f),RestFix=new Vector3(.084f,-.118f,.03f);
        /// <summary>The hilt in both hands until the point bites; the blow's shock throws the left hand off it, out for balance.</summary>
        private static float Hands(float t)=>1-NativeEntranceMath.Smooth((t-(ContactAt-.03f))/.06f);
        /// <summary>Where the drawn grip is and which way the blade points once its point bites the floor ahead: driven in along its own line
        /// from the contact to its depth at the blow.</summary>
        public static void HiltAt(float t,out Vector3 grip,out Vector3 blade)
        {
            float d=Depth*NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-ContactAt)/(SlamAt+.07f-ContactAt)));
            var fwd=Local(0,0,1);blade=Vector3.Normalize(fwd*(float)Math.Cos(Slope)-Vector3.UnitY*(float)Math.Sin(Slope));
            var tip=new Vector3(Bite.X,NativeEntranceStage.FloorY,Bite.Z)+blade*(d/(float)Math.Sin(Slope));grip=tip-blade*SwordLength;
        }
        /// <summary>Both hands on the hilt until the bite (Core NativeIntroGrip: the left at the two-hander's lower grip).</summary>
        public bool GripAt(float t,out NativeIntroGripAt grip){float w=Hands(t);grip=new NativeIntroGripAt(w);return w>.001f;}
        /// <summary>The feet come down in the blow's landing (their planting blends in as they arrive).</summary>
        public override bool FeetSliding(float t)=>t>=SlamAt-.2f&&t<SlamAt+.02f||t>=LeapAt-.1f&&t<LeapAt+.1f;

        // ---- the fracture: each slab heaves on the wave from the blow and settles tilted outward ----
        /// <summary>Slab `k`'s pose at `t` (source frame): its rest, lifted and tilted after the wave reaches it.</summary>
        public static void Slab(int k,float t,out Vector3 at,out Quaternion turn)
        {
            var c=Slabs[k];var rest=U(c.X,0,c.Y);at=rest;turn=Quaternion.Identity;float d=new Vector2(rest.X,rest.Z).Length(),reach=SlamAt+d/9f,s=t-reach;if(s<=0)return;
            // (the centre slab, where the pilot stands, only shudders: the feet stay on it)
            float h=NativeEntranceMath.Rand(36001,k),tilt=k==0?0:.12f+.16f*h,lift=k==0?0:.04f+.16f*h,jump=k==0?.025f:.18f+.22f*h*(1-d/6f);
            // A jump on the wave, damped, settling on its new height and tilt.
            float settle=1-(float)Math.Exp(-7*s),bounce=(float)(Math.Exp(-5.5*s)*Math.Sin(Math.Min(s*14,3.1416)));
            at.Y+=lift*settle+jump*bounce;
            var radial=d>.05f?Vector3.Normalize(new Vector3(rest.X,0,rest.Z)):Vector3.UnitZ;var axis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,radial));
            float spin=.25f*(h-.5f)*settle;turn=Quaternion.CreateFromAxisAngle(axis,-tilt*settle-tilt*.6f*bounce)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,spin*.2f);
        }
        /// <summary>How hot the quarry's cracks and seam run at `t` (the glow sub-mesh's level: the seam, the faults and the light under the disc).</summary>
        private float Heat(float t)=>(.55f+.15f*(float)Math.Sin(t*1.7f)*(Context.Reduced?0:1)+2.6f*NativeEntranceMath.Pulse(t,SlamAt,.06f,.9f)+.9f*NativeEntranceMath.Ramp(t,SlamAt,SlamAt+.6f))
            *(1-.72f*NativeEntranceMath.Ramp(t,StandAt,CardAt));// (cooling under the name: the seam ran as a bright bar through its band)

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("rift/static"))return false;
            set.Piece("rift/static",SetAt,Quaternion.Identity,1,White,White*Heat(t));
            for(int k=0;k<Slabs.Length;k++){Slab(k,t,out var at,out var turn);set.Piece(slabProps[k],at,turn,1,White,White);}
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x1a1714),Molten,.6f+.8f*NativeEntranceMath.Ramp(t,SlamAt,SlamAt+.5f),NativeEntranceFx.Hex(0x4a2412));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x120b07);l.FogNear=8;l.FogFar=42;l.DriveProbe=true;
            float heat=Heat(t),blow=Flash(t,SlamAt,.02f,.35f),after=NativeEntranceMath.Ramp(t,SlamAt,SlamAt+.5f);
            l.Sky=NativeEntranceFx.Hex(0x3a2a22,.24f+.1f*after);l.Ground=NativeEntranceFx.Hex(0x1a0e08,.3f+.4f*after);
            l.Directional(new Vector3(-.5f,1,-.4f),Moon*.32f);
            // (the set takes only directionals and the ambient: the molten light welling up from the broken floor lights the walls from below)
            l.Directional(new Vector3(.15f,-.55f,.8f),(Molten*.6f+Ember*.4f)*(.12f+.3f*after+.4f*blow));
            // The seam in the back corner, the light welling up through the broken disc, the blow, a warm rim behind, the name.
            l.Add(U(4.6f,1.6f,-9.6f),Molten*(4.5f*heat),10);
            l.Add(U(0,.15f,0),(Molten*1.5f+Ember)*(3.2f*after+6*blow),7);
            if(blow>.01f)l.Add(new Vector3(0,1.0f,.6f),(White*2+Gold)*(9*blow),9);
            l.Add(new Vector3(.5f,2.6f,-3.2f),Ember*1.8f,7);
            l.Add(new Vector3(-2.6f,1.6f,3.2f),Moon*.9f,7);
            float name=NativeEntranceMath.Ramp(t,CardAt-.25f,CardAt+.35f);if(name>.01f)l.Add(new Vector3(1.6f,2.0f,1.8f),Gold*(1.8f*name),8);
        }

        // ---- the effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY,after=t-SlamAt;
            // Embers drifting up off the quarry's faults and seam all through; many more off the broken disc after the blow.
            NativeEntranceFx.Embers(fx,t,-4,Total,U(3.0f,0,-7.0f),3.5f,low?6:14,.6f,2.6f,Ember*1.8f,.03f,36100,q,.3f);
            NativeEntranceFx.Embers(fx,t,SlamAt,Total,new Vector3(0,floor+.05f,0),4.2f,low?10:26,.9f,2.2f,Ember*2.2f,.035f,36200,q,.5f);
            // Dust hanging in the moonlight.
            for(int i=0,n=low?6:14;i<n;i++){var p=new Vector3(NativeEntranceMath.Signed(36301,i)*6,.6f+3*NativeEntranceMath.Rand(36303,i),NativeEntranceMath.Signed(36305,i)*6)+new Vector3((float)Math.Sin(t*.2f+i)*.3f,(float)Math.Sin(t*.13f+i*2)*.15f,0);fx.Glow(p,Moon*.06f,0,.03f,0,0,36310+i);}
            // The leap: grit kicked off the floor.
            NativeEntranceFx.GroundDust(fx,t,LeapAt+.12f,new Vector3(0,floor,0),low?5:10,.3f,1.6f,.8f,Dust,.35f,.45f,36400,q,floor);
            if(after<0)return;
            // The blow: a white-gold flash at the blade, sparks thrown wide and molten drops spraying out, a ring of dust running out.
            var hit=new Vector3(Bite.X,floor+.05f,Bite.Z);
            if(after<.22f)fx.Glow(hit+new Vector3(0,.25f,0),(White*2+Gold)*(2.6f*(1-after/.22f)),0,.4f+1.4f*after,1,after/.22f,36500);
            NativeEntranceFx.Sparks(fx,t,SlamAt,hit,Vector3.UnitY,1.6f,low?14:36,8.5f,1.0f,8,.8f,(White+Gold)*1.8f,Molten*2,.016f,36510,q,floor);
            if(after<1.4f)
            {
                int m=low?12:30;
                for(int i=0;i<m;i++)
                {
                    float life=.7f+.6f*NativeEntranceMath.Rand(36601,i);if(after>life)continue;float u=after/life,ang=i*6.2831853f/m+.3f*NativeEntranceMath.Signed(36603,i),sp=2.4f+3.2f*NativeEntranceMath.Rand(36605,i),up=2.2f+3f*NativeEntranceMath.Rand(36607,i);
                    var v0=new Vector3((float)Math.Cos(ang)*sp,up,(float)Math.Sin(ang)*sp);var p=new Vector3(0,floor+.1f,0)+v0*after+new Vector3(0,-4.9f*after*after,0);if(p.Y<floor)continue;
                    fx.Streak(p,v0+new Vector3(0,-9.8f*after,0),(i%3==0?Gold:Molten)*(2.2f*(1-u)),.012f,.03f,.03f,.6f,u,36610+i);
                }
            }
            NativeEntranceFx.GroundDust(fx,t,SlamAt,new Vector3(0,floor,0),low?10:22,1.2f,7.5f,1.6f,Dust,.55f,1.2f,36700,q,floor);
            // Chips of stone tumbling out of the joints.
            if(after<1.2f){int m=low?8:18;for(int i=0;i<m;i++){float life=.6f+.5f*NativeEntranceMath.Rand(36801,i);if(after>life)continue;float u=after/life,ang=NativeEntranceMath.Rand(36803,i)*6.2831853f,r0=.8f+3.6f*NativeEntranceMath.Rand(36805,i),reach=r0/9f;float a=after-reach;if(a<0)continue;
                var o=new Vector3((float)Math.Cos(ang)*r0,floor+.05f,(float)Math.Sin(ang)*r0);var v0=new Vector3((float)Math.Cos(ang)*1.2f,2.4f+2.4f*NativeEntranceMath.Rand(36807,i),(float)Math.Sin(ang)*1.2f);var p=o+v0*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<floor)continue;
                fx.Streak(p,v0+new Vector3(0,-9.8f*a,0),Stone*(1.4f*(1-u)),.03f,.04f,.015f,.2f,u,36810+i);}}
            // The cracks racing out from the disc to the walls, molten in their throats, cooling to a dull glow.
            for(int c=0;c<Cracks.Length;c++)
            {
                var path=Cracks[c];float run=CrackSpeed*Math.Max(0,t-SpreadAt-.05f*c),done=0;float cool=1-.55f*NativeEntranceMath.Ramp(t,SpreadAt+1.2f,Total);
                for(int s=0;s+1<path.Length;s++)
                {
                    var a=U(path[s].X,.025f,path[s].Y);var b=U(path[s+1].X,.025f,path[s+1].Y);float len=Vector3.Distance(a,b);float shown=Math.Min(len,run-done);done+=len;if(shown<=0)break;
                    var dir=(b-a)/len;var head=a+dir*shown;Line(fx,head,dir*shown,Molten*(1.8f*cool),.05f,.2f,0,36900+c*4+s);Line(fx,head,dir*shown,Gold*(1.2f*cool),.018f,.2f,0,36950+c*4+s);
                }
            }
            // Smoke climbing off the broken ring.
            if(after<4){int m=low?4:9;for(int i=0;i<m;i++){float u=((after*.32f+i/(float)m)%1f);float ang=i*2.4f;var p=new Vector3((float)Math.Cos(ang)*(1.5f+1.5f*NativeEntranceMath.Rand(37001,i)),floor+.2f+2.6f*u,(float)Math.Sin(ang)*(1.5f+1.5f*NativeEntranceMath.Rand(37003,i)));fx.Smoke(p,new Vector3(.16f,.13f,.12f),.25f*(1-u)*NativeEntranceMath.Ramp(after,0,.4f),.6f+1.4f*u,u,37010+i,floor);}}
            // Grit falling off the quarry walls after the blow.
            if(after<2.5f){int m=low?6:14;for(int i=0;i<m;i++){float born=.2f+1.2f*NativeEntranceMath.Rand(37101,i),a=after-born;if(a<0||a>1.2f)continue;var o=U(-9.5f+13.6f*NativeEntranceMath.Rand(37103,i),1.4f+1.1f*NativeEntranceMath.Rand(37105,i),-9.5f+.1f*NativeEntranceMath.Rand(37107,i));fx.Streak(o-new Vector3(0,4.9f*a*a,0),new Vector3(0,-9.8f*a,0),Dust*1.1f,.02f,.03f,.02f,.2f,a/1.2f,37110+i);}}
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.faultline.bed",null,"fire",.25f,.7f),
            new NativeIntroCue(LeapAt,"intro2.faultline.leap","melee.greatsword.swing","greatsword",.8f,.85f),
            new NativeIntroCue(SlamAt,"intro2.faultline.slam","blast.quake","explosion",1f,.8f),
            new NativeIntroCue(SlamAt+.05f,"intro2.faultline.crack","structure.crack","stone",.9f),
            new NativeIntroCue(SpreadAt+.25f,"intro2.faultline.rumble","structure.debris","stone",.7f,.7f),
            new NativeIntroCue(RiseAt,"intro2.faultline.lift","melee.greatsword.hit","metal",.4f,.8f),
            new NativeIntroCue(SpreadAt+.8f,"intro2.faultline.hiss","magic.fire.hit","fire",.4f,.7f),
            new NativeIntroCue(GroundAt,"intro2.faultline.ground","structure.gravel","stone",.55f),
            new NativeIntroCue(CardAt,"intro2.faultline.name","structure.thud","greatsword",.45f,.9f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(1,.97f,.92f),Ember*1.3f);card.Style=NativeIntroCardStyle.Slam;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
