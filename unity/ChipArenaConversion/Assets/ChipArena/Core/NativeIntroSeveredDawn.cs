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
        private const float CutHit=.32f,CutLength=.8f,RibbonLength=.8f;
        private static readonly Vector3 Dawn=new Vector3(1,.86f,.62f),Gold=NativeEntranceFx.Hex(0xffd08a),Night=NativeEntranceFx.Hex(0x0b0806),Cool=NativeEntranceFx.Hex(0x7f95c4),Teal=NativeEntranceFx.Hex(0xb6fff0);
        public override string Spec=>"sword";
        protected override float TipLength(bool left)=>left?.15f:1.28f;
        // The shaft: from the cleft in the vault to the floor just in front of the pilot (source frame), and the cut through it.
        private static readonly Vector3 ShaftTop=U(0,10.0f,.6f),ShaftFoot=U(0,0,.75f),CutPoint=U(0,1.35f,.72f);
        private static readonly Vector3 CutNormal=Vector3.Normalize(new Vector3(.3f,1,.05f)),CutAcross=Vector3.Normalize(Vector3.Cross(CutNormal,Vector3.UnitZ)),Fall=Vector3.Normalize(new Vector3(CutNormal.X,0,CutNormal.Z)-CutNormal*Vector3.Dot(new Vector3(CutNormal.X,0,CutNormal.Z),CutNormal));

        public NativeIntroSeveredDawn():base(NativeIntroCatalog.WeaponPrefix+"sword",Total,CardAt,CutAt,0xb6fff0)
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1);k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1);Keys=k;
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

        // ---- the body: the sword set's clips on the model, the game's captures of them on the keyed rig ----
        private const int Idle=0,CutA=3,CutC=5;
        private static void Moves(float t,out int capture,out string clip,out float time)
        {
            if(t<WindAt){capture=Idle;clip="Armed-Idle";time=t;return;}
            if(t<FollowEnd){capture=CutA;clip="Sword-Attack-R1";time=t<CutAt?CutHit*NativeEntranceMath.Clamp01((t-WindAt)/(CutAt-WindAt)):CutHit+(CutLength-CutHit)*NativeEntranceMath.Clamp01((t-CutAt)/(FollowEnd-CutAt));return;}
            if(t<RibbonAt){capture=Idle;clip="Armed-Idle";time=.5f+(t-FollowEnd);return;}
            if(t<StanceAt){capture=CutC;clip="Sword-Attack-R6";time=RibbonLength*NativeEntranceMath.Clamp01((t-RibbonAt)/(StanceAt-RibbonAt));return;}
            capture=Idle;clip="Armed-Idle";time=1.2f+(t-StanceAt);
        }
        public override bool Clip(float t,out string clip,out float time){Moves(t,out _,out clip,out time);return true;}
        public override float Fade(float t)=>t>=WindAt-.02f&&t<WindAt+.1f?.05f:t>=FollowEnd-.02f&&t<FollowEnd+.4f?.35f:t>=StanceAt-.02f&&t<StanceAt+.45f?.4f:.12f;
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            base.Pose(t,pose,out root,out rotation);Moves(t,out int capture,out _,out float time);Captured(0,capture,time,pose);
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
            if(after>-.05f&&after<.35f)BladeSweep(fx,t,0,CutAt-.12f,.22f,White*1.6f+Teal,Gold,.022f,.45f,37800,low,FollowEnd);
            if(after>=0&&after<.25f){var tip=DrawnTip(t,0);float g=Flash(t,CutAt,.01f,.08f);fx.Glow(tip,(White*2+Gold)*(1.4f*g),0,.25f,1,0,37850);}
            // The blade holds the light: a glow running up its edge, a ribbon of gold behind the backhand.
            float blade=BladeLight(t);
            if(blade>.01f)
            {
                DrawnBlade(t,0,out var hand,out var tip);float run=(t*.9f)%1f;
                fx.Glow(Vector3.Lerp(hand,tip,.15f+.8f*run),Gold*(1.2f*blade),0,.09f,1,0,37900);fx.Glow(tip,Gold*(.8f*blade),0,.07f,1,0,37901);
                Line(fx,tip,tip-hand,Gold*(.5f*blade),.018f,.5f,0,37902);
            }
            if(t>=RibbonAt&&t<StanceAt+.5f)BladeSweep(fx,t,0,RibbonAt,.45f,White*.8f+Gold*1.2f,Gold*1.3f,.028f,.7f,38000,low,StanceAt);
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
