using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Severed Dawn", the sword's intro (intros v2; docs/INTROS-MELEE.md): a draw so fast it cuts the light. 7.4 s, the name at 5.9 s. A
    /// ruined vaulted hall at dawn (the Lightwell): one shaft of light falls through a cleft in the vault onto the flagstones in front of the
    /// pilot, dust turning in it. The camera comes down the shaft to the pilot standing at its edge, the sword low; the hand tightens on
    /// the grip; the light reaches the visor. One forehand cut, too fast to see (the frame shows it begun and ended), and the shaft of light
    /// is severed: its lower half slides off along the cut and breaks into falling flakes, the hall going dark but for the blade, which now
    /// holds the light. A slower backhand draws a ribbon of it; the dawn comes back softly under the name.
    /// The model plays the sword set's clips (NativeBladeIntro), the keyed rig the game's own captures of the same clips (the hold is the
    /// match's, and the blade's tip, Anchor RightTip, follows the captured blade for the trails). Presentation only.
    /// </summary>
    public sealed class NativeIntroSeveredDawn:NativeIntroMeleeBlade
    {
        public const string Title="Severed Dawn";
        public const float Total=7.4f,CardAt=5.9f;
        /// <summary>The beats (presentation s): the grip, the visor, the cut's wind, the cut, the second cut, the stance.</summary>
        public const float GripAt=1.6f,VisorAt=2.32f,WindAt=2.45f,CutAt=2.62f,FollowEnd=3.35f,RibbonAt=4.35f,RibbonHit=4.35f+.307f*1.15f,StanceAt=5.3f;
        private static readonly Vector3 Dawn=new Vector3(1,.86f,.62f),Gold=NativeEntranceFx.Hex(0xffd08a),Night=NativeEntranceFx.Hex(0x0b0806),Cool=NativeEntranceFx.Hex(0x7f95c4),Teal=NativeEntranceFx.Hex(0xb6fff0);
        public override string Spec=>"sword";
        protected override float TipLength(bool left)=>left?.15f:1.28f;
        // The shaft: from the cleft in the vault to the floor just in front of the pilot (source frame), and the cut through it.
        private static readonly Vector3 ShaftTop=U(0,10.0f,.6f),ShaftFoot=U(0,0,.75f),CutPoint=U(0,1.35f,.72f);
        private static readonly Vector3 CutNormal=Vector3.Normalize(new Vector3(.3f,1,.05f)),CutAcross=Vector3.Normalize(Vector3.Cross(CutNormal,Vector3.UnitZ)),Fall=Vector3.Normalize(new Vector3(CutNormal.X,0,CutNormal.Z)-CutNormal*Vector3.Dot(new Vector3(CutNormal.X,0,CutNormal.Z),CutNormal));

        public NativeIntroSeveredDawn():base(NativeIntroCatalog.WeaponPrefix+"sword",Total,CardAt,CutAt,0xb6fff0)
        {
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips) ----
            // Feet: the left foot a little ahead; the cut steps it forward and out, the stance draws it back under the salute.
            Start=Stance(.15f,.1f,.1f,-.19f,-.14f,-.45f);
            Steps=new[]{Step(true,WindAt+.06f,CutAt+.04f,.27f,.4f,.2f,.07f),Step(true,StanceAt-.2f,StanceAt+.15f,.16f,.12f,.1f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // Still at the light's edge, the sword low at the right side, the head a little bowed.
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.04f).J(B,.04f).J(C,.02f,-.08f,0).J(Hd,.16f,.04f,0).J(LL,-.08f,0,.1f).J(LK,.12f).J(RL,.08f,0,-.12f).J(RK,.14f)
             .J(RA,-.35f,0,-.25f).J(RE,-.5f).J(LA,-.1f,0,.16f).J(LE,-.3f);
            k.Key(GripAt-.1f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.05f).J(B,.05f).J(C,.02f,-.1f,0).J(Hd,.12f,.02f,0).J(LL,-.08f,0,.1f).J(LK,.14f).J(RL,.08f,0,-.12f).J(RK,.16f)
             .J(RA,-.35f,0,-.25f).J(RE,-.5f).J(LA,-.1f,0,.16f).J(LE,-.3f);
            // The grip tightens: the shoulders set, the weight comes onto the balls of the feet.
            k.Key(GripAt+.2f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.08f).J(B,.08f).J(C,.04f,-.12f,0).J(Hd,.1f,0,0).J(LL,-.12f,0,.1f).J(LK,.2f).J(RL,.1f,0,-.12f).J(RK,.22f)
             .J(RA,-.4f,0,-.28f).J(RE,-.6f).J(LA,-.15f,0,.2f).J(LE,-.4f);
            // The light reaches the visor: the chin comes up into it.
            k.Key(VisorAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.09f).J(B,.06f).J(C,-.02f,-.12f,0).J(Hd,-.12f,0,0).J(LL,-.12f,0,.1f).J(LK,.22f).J(RL,.1f,0,-.12f).J(RK,.24f)
             .J(RA,-.4f,0,-.28f).J(RE,-.6f).J(LA,-.15f,0,.2f).J(LE,-.4f);
            // The wind: coiled to the right, the blade chambered over the right shoulder, the weight on the back leg.
            k.Key(WindAt+.1f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.14f).J(B,-.02f).J(Sp,0,-.15f,0).J(C,-.05f,-.42f,0).J(Hd,-.08f,.3f,0).J(LL,-.2f,0,.1f).J(LK,.3f).J(RL,-.05f,0,-.12f).J(RK,.45f)
             .J(RA,-2.0f,0,-.6f).J(RE,-1.4f).J(LA,-.6f,0,.5f).J(LE,-.6f);
            // The cut: uncoiling forward and to the left onto the stepping foot, the blade through the light and down past the left hip.
            k.Key(CutAt,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.2f).J(B,.22f).J(Sp,0,.15f,0).J(C,.08f,.35f,0).J(Hd,.02f,-.2f,0).J(LL,-.55f,0,.14f).J(LK,.75f).J(RL,.2f,0,-.14f).J(RK,.3f)
             .J(RA,-1.0f,0,-.2f).J(RE,-.3f).J(LA,-.4f,0,.9f).J(LE,-.5f).ImpactKey();
            k.Key(CutAt+.3f,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.22f).J(B,.25f).J(Sp,0,.18f,0).J(C,.1f,.42f,0).J(Hd,.04f,-.24f,0).J(LL,-.58f,0,.14f).J(LK,.8f).J(RL,.22f,0,-.14f).J(RK,.32f)
             .J(RA,-.8f,0,-.1f).J(RE,-.25f).J(LA,-.35f,0,.95f).J(LE,-.45f);
            // Held there while the light falls, the head turning up to it; then back to a middle guard.
            k.Key(FollowEnd,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.2f).J(B,.2f).J(Sp,0,.15f,0).J(C,.06f,.38f,0).J(Hd,-.15f,-.3f,0).J(LL,-.55f,0,.14f).J(LK,.76f).J(RL,.2f,0,-.14f).J(RK,.3f)
             .J(RA,-.8f,0,-.1f).J(RE,-.25f).J(LA,-.35f,0,.9f).J(LE,-.45f);
            k.Key(RibbonAt-.25f,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.12f).J(B,.08f).J(C,.02f,.15f,0).J(Hd,.0f,.05f,0).J(LL,-.4f,0,.14f).J(LK,.5f).J(RL,.15f,0,-.14f).J(RK,.25f)
             .J(RA,-1.1f,0,-.25f).J(RE,-1.0f).J(LA,-.3f,0,.4f).J(LE,-.6f);
            // The backhand: chambered left, then drawn slowly across the front to the right, the hips leading.
            k.Key(RibbonAt,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.14f).J(B,.08f).J(Sp,0,.12f,0).J(C,.02f,.38f,0).J(Hd,0,-.1f,0).J(LL,-.42f,0,.14f).J(LK,.55f).J(RL,.15f,0,-.14f).J(RK,.28f)
             .J(RA,-1.2f,0,.3f).J(RE,-1.3f).J(LA,-.3f,0,.4f).J(LE,-.6f);
            k.Key(RibbonHit,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.16f).J(B,.1f).J(Sp,0,-.05f,0).J(C,0,-.15f,0).J(Hd,-.04f,.1f,0).J(LL,-.4f,0,.14f).J(LK,.5f).J(RL,.12f,0,-.14f).J(RK,.35f)
             .J(RA,-1.45f,0,-.4f).J(RE,-.4f).J(LA,-.4f,0,.6f).J(LE,-.5f).PassKey();
            k.Key(RibbonHit+.3f,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.15f).J(B,.08f).J(Sp,0,-.12f,0).J(C,0,-.35f,0).J(Hd,-.04f,.2f,0).J(LL,-.36f,0,.14f).J(LK,.45f).J(RL,.1f,0,-.14f).J(RK,.38f)
             .J(RA,-1.3f,0,-.9f).J(RE,-.3f).J(LA,-.45f,0,.7f).J(LE,-.5f);
            // The stance under the name: tall, the blade raised before the face in salute, the left arm loose.
            k.Key(StanceAt+.25f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(B,-.02f).J(C,-.06f,-.08f,0).J(Hd,-.06f,0,0).J(LL,-.1f,0,.1f).J(LK,.1f).J(RL,.08f,0,-.12f).J(RK,.12f)
             .J(RA,-.9f,0,-.2f).J(RE,-1.6f).J(LA,-.08f,0,.2f).J(LE,-.35f).PassKey();
            k.Key(StanceAt+.6f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(B,-.03f).J(C,-.08f,-.06f,0).J(Hd,-.08f,0,0).J(LL,-.1f,0,.1f).J(LK,.1f).J(RL,.08f,0,-.12f).J(RK,.12f)
             .J(RA,-.9f,0,-.2f).J(RE,-1.65f).J(LA,-.08f,0,.2f).J(LE,-.35f);
            k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(B,-.03f).J(C,-.08f,-.06f,0).J(Hd,-.08f,0,0).J(LL,-.1f,0,.1f).J(LK,.1f).J(RL,.08f,0,-.12f).J(RK,.12f)
             .J(RA,-.9f,0,-.2f).J(RE,-1.65f).J(LA,-.08f,0,.2f).J(LE,-.35f);Keys=k;
            // ---- the sword's way (the right hand's grip and the blade, in the pilot's frame) ----
            Sword.Key(0,V(-.27f,.8f,.2f),V(-.15f,-.45f,.88f))
                 .Key(GripAt-.1f,V(-.27f,.8f,.21f),V(-.15f,-.44f,.88f))
                 .Key(GripAt+.2f,V(-.26f,.83f,.25f),V(-.12f,-.3f,.95f))
                 .Key(VisorAt,V(-.26f,.84f,.25f),V(-.12f,-.28f,.95f))
                 .Key(WindAt+.1f,V(-.3f,1.38f,-.02f),V(.25f,.55f,-.8f))
                 .Key(CutAt-.05f,V(-.12f,1.28f,.42f),V(.25f,.3f,.92f)).Pass()
                 .Key(CutAt,V(.12f,.95f,.42f),V(.55f,-.5f,.67f)).Pass()
                 .Key(CutAt+.12f,V(.3f,.76f,.22f),V(.5f,-.7f,-.5f)).Impact()
                 .Key(FollowEnd,V(.29f,.78f,.22f),V(.5f,-.68f,-.53f))
                 .Key(RibbonAt-.25f,V(.05f,1.04f,.38f),V(.3f,.25f,.92f))
                 .Key(RibbonAt,V(.26f,1.12f,.28f),V(.88f,.15f,.45f))
                 .Key(RibbonHit,V(-.04f,1.14f,.5f),V(-.05f,.1f,1)).Pass()
                 .Key(RibbonHit+.3f,V(-.36f,1.1f,.24f),V(-.9f,.12f,-.4f))
                 .Key(StanceAt+.25f,V(-.1f,1.18f,.3f),V(.05f,.98f,-.12f)).Pass()
                 .Key(StanceAt+.6f,V(-.08f,1.2f,.3f),V(.05f,.98f,-.15f))
                 .Key(Total,V(-.08f,1.2f,.3f),V(.05f,.98f,-.15f));
            // The camera: down the shaft from the vault; close on the hand at the grip; the visor as the light reaches it; the cut seen wide
            // from the side (begun and ended, nothing between), slowed; round the severed light; the backhand from the front; the hero.
            Rig.Crane(0,V(.5f,7.6f,3.2f),V(.95f,1.5f,3.65f),0,V(0,4.2f,.6f),V(0,1.15f,.2f),46,40).Ease(NativeIntroEase.InOut)
               .Dolly(GripAt,V(-.95f,1.1f,1.05f),V(-.8f,1.05f,.9f),V(-.3f,.95f,.15f),V(-.28f,.95f,.15f),26,24).Focus(0,0,.55f)
               .Dolly(VisorAt,V(.1f,1.4f,1.3f),V(.06f,1.42f,1.12f),V(0,.15f,.06f),V(0,.16f,.06f),28,26).Follow(NativeEntranceAnchor.Head).Focus(0,0,.5f)
               .Dolly(CutAt-.03f,V(3.2f,1.3f,1.3f),V(3.25f,1.32f,1.45f),V(0,1.3f,.45f),V(0,1.28f,.45f),44,42)
               .Orbit(FollowEnd-.05f,new Vector3(0,0,.5f),70,22,3.1f,2.7f,1.45f,1.3f,V(0,1.3f,.5f),V(0,1.2f,.4f),40,38).Ease(NativeIntroEase.InOut)
               .Dolly(RibbonAt-.1f,V(-.95f,.72f,2.65f),V(-.75f,.78f,2.35f),V(0,1.2f,0),V(0,1.25f,0),42,40);
            HeroFull(StanceAt,-1,4.5f,32,.12f);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
            Ramps.Ramp(CutAt+.04f,.22f,.55f,1.0f);
        }
        public override string SetBank=>Bank;

        // ---- the body: keyed (no match clips), the right arm solved onto the sword's way ----
        private readonly NativeIntroWield Sword=new NativeIntroWield();
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)=>Wield(Sword,t,pose,root,rotation,false);

        /// <summary>The visor shot frames the visor as drawn: the model's crouch carries its head lower than the capture's keyed head the shot
        /// is authored on (the first captures framed the helmet's crown), so the shot moves by the gap between the two.</summary>
        protected override void Frame(float t,ref NativeEntranceView view)
        {
            if(t<VisorAt||t>=CutAt-.03f||!Eyes(t,.1f,out var eyes))return;
            var d=eyes-(Smoothed((int)NativeEntranceAnchor.Head,t)+new Vector3(0,.155f,.06f));view.Eye+=d;view.Look+=d;
        }

        // ---- the light ----
        /// <summary>How much of the dawn falls through the cleft at `t`: full until the cut, the hall going dark after it, coming back under the name.</summary>
        private static float ShaftLevel(float t)=>t<CutAt?1:.22f+.78f*(1-NativeEntranceMath.Ramp(t,CutAt,CutAt+.9f))+.35f*NativeEntranceMath.Ramp(t,CardAt-.6f,CardAt+.6f);
        /// <summary>How much light the blade holds (from the cut, swelling into the backhand, settling under the name).</summary>
        private static float BladeLight(float t)=>t<CutAt?0:NativeEntranceMath.Ramp(t,CutAt,CutAt+.4f)*(.7f+.6f*NativeEntranceMath.Bump(t,RibbonAt,StanceAt+.2f));
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("lightwell/static"))return false;
            // The aisle windows' glass (the glow sub-mesh), dimmer as the hall goes dark.
            set.Piece("lightwell/static",SetAt,Quaternion.Identity,1,White,White*(.5f+.5f*ShaftLevel(t)));return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x1c1712),Dawn,ShaftLevel(t),NativeEntranceFx.Hex(0x3b2d22));
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x140f0b);l.FogNear=6;l.FogFar=32;l.DriveProbe=true;
            float lvl=ShaftLevel(t),cut=Flash(t,CutAt,.01f,.3f),blade=BladeLight(t);
            l.Sky=NativeEntranceFx.Hex(0x3a2c22,.18f+.1f*lvl);l.Ground=NativeEntranceFx.Hex(0x120c08,.25f);
            // The dawn: a sun straight down through the cleft (the vault's shadow makes the shaft on the floor), its bounce off the lit stones.
            l.Directional(new Vector3(0,1,-.012f),Dawn*(1.25f*lvl));
            l.Add(U(0,.35f,.8f),Dawn*(2.4f*lvl),5.5f);
            l.Add(new Vector3(-5,3.2f,-2),Cool*1.0f,10);
            l.Add(new Vector3(0,4.2f,-10),Cool*2.0f,14);
            if(cut>.01f)l.Add(CutPoint,(White*2+Gold)*(6*cut),6);
            if(blade>.01f){DrawnBlade(t,0,out var root,out var tip,.1f);l.Add(Vector3.Lerp(root,tip,.45f),Gold*(1.3f*blade),3.2f);}
        }

        // ---- the effects (on the blade as drawn: DrawnBlade) ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float lvl=ShaftLevel(t),after=t-CutAt;
            // The shaft of dawn, whole until the cut; after it, its upper part held over the cut and its lower part sliding off along it, fading.
            if(after<0)NativeEntranceFx.Shaft(fx,t,ShaftTop,ShaftFoot,1.05f,.72f,Dawn*.5f,lvl,low?8:14,low?14:34,37500,q);
            else
            {
                NativeEntranceFx.Shaft(fx,t,ShaftTop,CutPoint,1.05f,.8f,Dawn*.5f,lvl,low?6:11,low?10:24,37500,q);
                float slide=.08f+.55f*NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01(after/1.4f)),fade=1-NativeEntranceMath.Ramp(after,.15f,1.3f);
                if(fade>.01f)NativeEntranceFx.Shaft(fx,t,CutPoint+Fall*slide,ShaftFoot+Fall*slide+new Vector3(0,-.4f*slide,0),.8f,.72f,Dawn*.5f,fade,low?4:6,low?6:12,37540,q);
                // The cut's hairline across the shaft (its width, no further: wider it read as a wire across the hall), bright, gone in half a second.
                float hair=1-NativeEntranceMath.Ramp(after,.08f,.55f);if(hair>.01f){var a=CutAcross*.5f;Line(fx,CutPoint+a,a*2,(White*2.4f+Gold)*hair,.01f,.9f,0,37560);Line(fx,CutPoint+a,a*2,Gold*(.9f*hair),.035f,.9f,0,37561);}
                // The severed light breaking into flakes that fall and turn, drawn into the backhand's ribbon when it comes.
                int m=low?20:46;
                for(int i=0;i<m;i++)
                {
                    float born=.05f+.9f*NativeEntranceMath.Rand(37601,i),a=after-born;if(a<0||a>2.8f)continue;float u=a/2.8f;
                    var o=CutPoint+Fall*(slide*.6f)+new Vector3(NativeEntranceMath.Signed(37603,i)*.7f,-.9f*NativeEntranceMath.Rand(37605,i),NativeEntranceMath.Signed(37607,i)*.6f);
                    var p=o+new Vector3((float)Math.Sin(a*2.1f+i)*.12f,-.22f*a,(float)Math.Cos(a*1.7f+i)*.12f);
                    // The backhand pulls them in toward the blade.
                    float pull=NativeEntranceMath.Ramp(t,RibbonAt+.1f,RibbonHit+.2f);if(pull>0)p=Vector3.Lerp(p,DrawnTip(t,0),pull*.7f*NativeEntranceMath.Rand(37609,i));
                    float tw=.6f+.4f*(float)Math.Sin(a*5+i*1.3f);fx.Glow(p,Gold*(1.1f*(1-u)*tw),0,.022f+.012f*NativeEntranceMath.Rand(37611,i),0,u,37620+i);
                }
            }
            // Dust turning in the hall's dark.
            for(int i=0,n=low?6:14;i<n;i++){var p=new Vector3(NativeEntranceMath.Signed(37701,i)*4,.4f+3.5f*NativeEntranceMath.Rand(37703,i),NativeEntranceMath.Signed(37705,i)*5)+new Vector3((float)Math.Sin(t*.2f+i)*.2f,(float)Math.Sin(t*.15f+i*2)*.1f,0);fx.Glow(p,Dawn*.04f,0,.025f,0,0,37710+i);}
            // The cut: the blade's sweep, a white-teal arc along the tip's path, too fast to see but for its trail.
            if(after>-.05f&&after<.35f)BladeSweep(fx,t,0,CutAt-.12f,.22f,White*1.6f+Teal,Gold,.022f,.3f,37800,low,FollowEnd,.45f);
            if(after>=0&&after<.25f){var tip=DrawnTip(t,0);float g=Flash(t,CutAt,.01f,.08f);fx.Glow(tip,(White*2+Gold)*(1.4f*g),0,.25f,1,0,37850);}
            // The blade holds the light: a glow running up its edge, a ribbon of gold behind the backhand.
            float blade=BladeLight(t);
            if(blade>.01f)
            {
                DrawnBlade(t,0,out var hand,out var tip);float run=(t*.9f)%1f;
                fx.Glow(Vector3.Lerp(hand,tip,.15f+.8f*run),Gold*(1.2f*blade),0,.09f,1,0,37900);fx.Glow(tip,Gold*(.8f*blade),0,.07f,1,0,37901);
                Line(fx,tip,tip-hand,Gold*(.5f*blade),.018f,.5f,0,37902);
            }
            if(t>=RibbonAt&&t<StanceAt+.5f)BladeSweep(fx,t,0,RibbonAt,.32f,White*.8f+Gold*1.2f,Gold*1.3f,.028f,.4f,38000,low,StanceAt,.55f);
            // Under the name: a glint along the blade.
            if(t>=CardAt-.1f&&t<CardAt+.6f){DrawnBlade(t,0,out var hand,out var tip);float g=NativeEntranceMath.Bump(t,CardAt-.1f,CardAt+.6f);
                NativeEntranceFx.Glint(fx,Vector3.Lerp(hand,tip,NativeEntranceMath.Smooth((t-CardAt+.1f)/.7f)),(White+Gold)*(1.5f*g),.12f*g,Vector3.UnitX,Vector3.UnitY,38100);}
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.severeddawn.hall","boss.noctiel.severance.wind","darkness",.3f,.8f),
            new NativeIntroCue(GripAt,"intro2.severeddawn.grip","boss.noctiel.gauntlet","metal",.3f),
            new NativeIntroCue(CutAt,"intro2.severeddawn.cut","boss.noctiel.slash","sword",1f,1.1f),
            new NativeIntroCue(CutAt+.05f,"intro2.severeddawn.sever","boss.noctiel.shatter","install",.7f),
            new NativeIntroCue(CutAt+.45f,"intro2.severeddawn.flakes","boss.noctiel.feather","win",.35f,1.3f),
            new NativeIntroCue(RibbonAt+.2f,"intro2.severeddawn.ribbon","melee.sword.swing","sword",.7f,.9f),
            new NativeIntroCue(CardAt,"intro2.severeddawn.name","boss.noctiel.hush.open","install",.4f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(1,.98f,.94f),Gold*1.2f);card.Style=NativeIntroCardStyle.Assemble;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
