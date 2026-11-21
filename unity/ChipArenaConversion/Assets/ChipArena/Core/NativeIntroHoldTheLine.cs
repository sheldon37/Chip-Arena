using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Hold the Line", the shield's intro (intros v2; docs/INTROS-MELEE.md): an immovable wall taking a barrage. 7.8 s, the name at 6.3 s.
    /// Dusk at the Breach (a broken fortress wall, braziers smouldering either side of the gap, a scorched field out to a ridge with siege
    /// engines). The pilot steps into the breach and plants the shield low; from behind it the camera sees lights climb off the far ridge:
    /// the barrage. The shield comes up before the chest, the left fist bracing its rim, and burning bolts burst on its face one after
    /// another, each rocking it back; near misses throw up earth and fire round it. Silence, smoke, the shield's face glowing hot; then a
    /// bash off the back leg blows the smoke back, and it stands in the breach, the shield lowered at its side, under the name.
    /// The body is keyed (no match clips), the shield on its keyed path, the left fist on its rim through the grip (INativeIntroGrip).
    /// Presentation only.
    /// </summary>
    public sealed class NativeIntroHoldTheLine:NativeIntroMeleeBlade,INativeIntroGrip
    {
        public const string Title="Hold the Line";
        public const float Total=7.8f,CardAt=6.3f;
        /// <summary>The beats (presentation s): the plant, the launch on the ridge, the guard raised, the barrage's hits, the silence, the bash, the stance.</summary>
        public const float PlantAt=1.2f,LaunchAt=2.0f,GuardAt=2.35f,SilenceAt=4.65f,BashAt=5.3f,StanceAt=6.15f;
        public static readonly float[] Hits={2.75f,3.15f,3.5f,3.9f,4.3f};
        private static readonly Vector3 Fire=NativeEntranceFx.Hex(0xff8a2c),Hot=NativeEntranceFx.Hex(0xffd27a),Dusk=NativeEntranceFx.Hex(0xff9a5c),Steel=NativeEntranceFx.Hex(0x8cceff),
            Smoke=new Vector3(.12f,.1f,.1f),Earth=NativeEntranceFx.Hex(0x4a3a2c),Sky=NativeEntranceFx.Hex(0x2a1c22);
        public override string Spec=>"shield";
        /// <summary>Where each bolt comes from (the ridge, source frame) and where it strikes the shield's face: the face as drawn (FaceAt),
        /// else this point in front of the chest (no model drawn: a checker, the Classic body).</summary>
        private static readonly Vector3 Face=new Vector3(0,1.2f,.62f);
        /// <summary>The middle of the shield's face in its weapon frame, a little proud of it (the match's block socket spans y .05-.4 of
        /// the frame, .25-.29 in front: Runtime NativeBlockFxPresentation).</summary>
        private static readonly Vector3 FaceLocal=new Vector3(0,.22f,.32f);
        /// <summary>The shield's face as drawn at `t` (`within`: the latest frame's, for the lights).</summary>
        private Vector3 FaceAt(float t,float within=1e-4f)=>DrawnFrame(t,0,FaceLocal,within,out var at)?at:Face;
        // Each hit's burst stays where the face was when the bolt struck (its sparks fly from there while the shield recoils).
        private readonly Vector3[] hitFace=new Vector3[5];private readonly bool[] hitSeen=new bool[5];private float faceClock=-1;
        private Vector3 HitFace(int i,float t)
        {
            if(t<faceClock-1e-4f)Array.Clear(hitSeen,0,hitSeen.Length);// (a clock that runs back starts over)
            faceClock=t;if(hitSeen[i])return hitFace[i];var f=FaceAt(t);if(t>=Hits[i]){hitFace[i]=f;hitSeen[i]=true;}return f;
        }
        private static Vector3 Origin(int i)=>new Vector3(-14+9*i+3*NativeEntranceMath.Signed(39001,i),9,72);
        private const float Flight=.72f;
        /// <summary>Near misses: earth and fire thrown up round the pilot (source frame) and when.</summary>
        private static readonly Vector3[] Misses={new Vector3(2.6f,0,-.2f),new Vector3(1.2f,0,-3.0f),new Vector3(-2.3f,0,-1.9f)};
        private static readonly float[] MissAt={3.0f,3.72f,4.12f};
        private static readonly string[] debrisProps={"breach/debris-0","breach/debris-1","breach/debris-2","breach/debris-3"};
        /// <summary>Which near miss throws each rock (the nearest).</summary>
        private static readonly int[] DebrisMiss={0,2,0,2};
        private static readonly Vector2[] DebrisRest={new Vector2(-1.2f,1.55f),new Vector2(1.25f,1.85f),new Vector2(-1.15f,2.45f),new Vector2(1.2f,2.3f)};

        public NativeIntroHoldTheLine():base(NativeIntroCatalog.WeaponPrefix+"shield",Total,CardAt,Hits[0],0x8cceff)
        {
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips) ----
            // Feet: the left foot steps into the gap with the plant; the bash drives in with another; the right foot follows into the stance.
            Start=Stance(.15f,.12f,.1f,-.2f,-.16f,-.45f);
            Steps=new[]{Step(true,PlantAt-.28f,PlantAt+.02f,.2f,.45f,.15f,.09f),Step(true,BashAt+.02f,BashAt+.24f,.22f,.78f,.15f,.08f),Step(false,StanceAt-.3f,StanceAt+.05f,-.16f,.56f,-.35f,.08f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // The shield carried at the right side; it gathers itself.
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.05f).J(B,.04f).J(C,0,.08f,0).J(Hd,.04f,0,0).J(LL,-.1f,0,.12f).J(LK,.15f).J(RL,.1f,0,-.12f).J(RK,.15f)
             .J(RA,-.2f,0,-.3f).J(RE,-.6f).J(LA,-.1f,0,.16f).J(LE,-.3f);
            k.Key(PlantAt-.3f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.1f).J(B,.12f).J(C,0,.18f,0).J(Hd,.02f,0,0).J(LL,-.3f,0,.12f).J(LK,.4f).J(RL,.12f,0,-.12f).J(RK,.3f)
             .J(RA,-.7f,0,-.35f).J(RE,-1.1f).J(LA,-.3f,0,.3f).J(LE,-.6f);
            // The plant: down into the gap behind the shield, set low in front, braced on the stepping leg.
            k.Key(PlantAt,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.33f).J(B,.3f).J(Sp,.1f).J(C,.1f,.08f,0).J(Hd,.04f,0,0).J(LL,-.85f,0,.14f).J(LK,1.25f).J(RL,.28f,0,-.14f).J(RK,.72f)
             .J(RA,-1.0f,0,-.3f).J(RE,-1.2f).J(LA,-.7f,0,.45f).J(LE,-1.0f).ImpactKey();
            k.Key(PlantAt+.45f,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.3f).J(B,.26f).J(Sp,.08f).J(C,.08f,.08f,0).J(Hd,-.04f,0,0).J(LL,-.82f,0,.14f).J(LK,1.2f).J(RL,.26f,0,-.14f).J(RK,.7f)
             .J(RA,-1.0f,0,-.3f).J(RE,-1.2f).J(LA,-.7f,0,.45f).J(LE,-1.0f);
            // The ridge launches: the head comes up over the rim.
            k.Key(LaunchAt+.1f,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.28f).J(B,.18f).J(Sp,.04f).J(C,-.04f,.08f,0).J(Hd,-.32f,0,0).J(LL,-.78f,0,.14f).J(LK,1.12f).J(RL,.24f,0,-.14f).J(RK,.66f)
             .J(RA,-1.0f,0,-.3f).J(RE,-1.2f).J(LA,-.7f,0,.45f).J(LE,-1.0f);
            // The guard: the shield up over the face, the body tucked behind it, the left hand braced at its back.
            float[] Guard(float drop,float lean,float head)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.BodyY]=drop;NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Sp,.08f,0,0);
                NativeEntranceKeys.Set(v,C,.12f,.15f,0);NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,LL,-.72f,0,.14f);NativeEntranceKeys.Set(v,LK,1.05f,0,0);NativeEntranceKeys.Set(v,RL,.22f,0,-.14f);NativeEntranceKeys.Set(v,RK,.72f,0,0);
                NativeEntranceKeys.Set(v,RA,-1.35f,0,-.45f);NativeEntranceKeys.Set(v,RE,-1.6f,0,0);NativeEntranceKeys.Set(v,LA,-1.0f,0,.6f);NativeEntranceKeys.Set(v,LE,-1.7f,0,0);});
            k.Key(GuardAt,Guard(-.2f,.2f,.12f)).Root(0,.88f,.06f,Facing).Planted(1);
            // Each hit rocks it back on its heels behind the shield, the head flinching; it comes forward into the guard again.
            for(int i=0;i<Hits.Length;i++)
            {
                k.Key(Hits[i]-.03f,Guard(-.2f,.2f,.1f)).Root(0,.88f,.06f,Facing).Planted(1);
                k.Key(Hits[i]+.06f,Guard(-.22f,.06f,.24f)).Root(0,.88f,.06f,Facing).Planted(1).ImpactKey();
                k.Key(Hits[i]+.28f,Guard(-.2f,.18f,.12f)).Root(0,.88f,.06f,Facing).Planted(1);
            }
            // Silence: still behind the shield, peering over the rim.
            k.Key(SilenceAt+.2f,Guard(-.2f,.16f,-.08f)).Root(0,.88f,.06f,Facing).Planted(1);
            // The bash: wound back, then driven forward off the back leg, the shield punched out.
            k.Key(BashAt,stand).Root(0,.88f,.06f,Facing).Planted(1).Drop(-.3f).J(B,.1f).J(C,0,.38f,0).J(Hd,.0f,-.1f,0).J(LL,-.7f,0,.14f).J(LK,1.0f).J(RL,.25f,0,-.14f).J(RK,.85f)
             .J(RA,-.8f,0,-.6f).J(RE,-1.5f).J(LA,-.6f,0,.6f).J(LE,-1.2f);
            k.Key(BashAt+.25f,stand).Root(0,.88f,.26f,Facing).Planted(1).Drop(-.22f).J(B,.36f).J(Sp,.08f).J(C,.06f,-.22f,0).J(Hd,.06f,.1f,0).J(LL,-.95f,0,.14f).J(LK,1.05f).J(RL,.38f,0,-.14f).J(RK,.35f)
             .J(RA,-1.5f,0,-.2f).J(RE,-.6f).J(LA,-.4f,0,.7f).J(LE,-.5f).ImpactKey();
            k.Key(BashAt+.55f,stand).Root(0,.88f,.26f,Facing).Planted(1).Drop(-.2f).J(B,.3f).J(Sp,.06f).J(C,.05f,-.18f,0).J(Hd,.02f,.08f,0).J(LL,-.9f,0,.14f).J(LK,1.0f).J(RL,.35f,0,-.14f).J(RK,.38f)
             .J(RA,-1.45f,0,-.2f).J(RE,-.65f).J(LA,-.4f,0,.65f).J(LE,-.5f);
            // The stance under the name: up, the right foot brought in, the shield on the arm before the body, chin up.
            k.Key(StanceAt+.1f,stand).Root(0,.88f,.56f,Facing).Planted(1).Drop(-.05f).J(B,0).J(C,-.05f,.1f,0).J(Hd,-.06f,0,0).J(LL,-.12f,0,.12f).J(LK,.14f).J(RL,.08f,0,-.12f).J(RK,.14f)
             .J(RA,-.65f,0,-.3f).J(RE,-1.25f).J(LA,-.12f,0,.18f).J(LE,-.35f).PassKey();
            k.Key(Total,stand).Root(0,.88f,.56f,Facing).Planted(1).Drop(-.05f).J(B,-.01f).J(C,-.06f,.1f,0).J(Hd,-.08f,0,0).J(LL,-.12f,0,.12f).J(LK,.14f).J(RL,.08f,0,-.12f).J(RK,.14f)
             .J(RA,-.65f,0,-.3f).J(RE,-1.25f).J(LA,-.12f,0,.18f).J(LE,-.35f);Keys=k;
            // ---- the shield's way (the right fist and its face, in the keyed chest's frame: it rides the crouch and the recoil) ----
            Shield.InChest().Key(0,V(-.38f,-.3f,.1f),V(-.25f,-.45f,.86f))
                  .Key(PlantAt-.3f,V(-.3f,-.12f,.24f),V(-.1f,-.1f,.99f))
                  .Key(PlantAt,V(-.17f,-.18f,.32f),V(0,-.05f,1)).Impact()
                  .Key(PlantAt+.45f,V(-.17f,-.17f,.32f),V(0,-.04f,1))
                  .Key(LaunchAt+.1f,V(-.16f,-.14f,.32f),V(0,0,1))
                  .Key(GuardAt,V(-.1f,.03f,.32f),V(0,.25f,.97f));
            for(int i=0;i<Hits.Length;i++)
                Shield.Key(Hits[i]-.03f,V(-.1f,.03f,.32f),V(0,.25f,.97f)).Key(Hits[i]+.06f,V(-.1f,.06f,.24f),V(0,.42f,.9f)).Impact().Key(Hits[i]+.28f,V(-.1f,.04f,.31f),V(0,.27f,.96f));
            Shield.Key(SilenceAt+.2f,V(-.1f,0,.32f),V(0,.15f,.99f))
                  .Key(BashAt,V(-.22f,-.02f,.2f),V(-.35f,.05f,.94f))
                  .Key(BashAt+.25f,V(-.08f,.04f,.41f),V(0,.05f,1)).Impact()
                  .Key(BashAt+.55f,V(-.1f,.02f,.38f),V(0,.04f,1))
                  .Key(StanceAt+.1f,V(-.38f,-.25f,.15f),V(-.35f,-.2f,.91f)).Pass()
                  .Key(Total,V(-.39f,-.24f,.15f),V(-.38f,-.18f,.9f));
            // The camera: wide and low from the field; the plant, low and close; over the shoulder through the breach as the ridge launches;
            // low in front with bolts streaking past the lens; from the side; the silence, pushing in; the bash, wide; the hero.
            Rig.Dolly(0,V(-2.4f,.72f,7.5f),V(-1.9f,.82f,6.2f),V(0,1.6f,-1),V(0,1.5f,-1),40,38)
               .Dolly(PlantAt,V(2.7f,.7f,2.7f),V(2.45f,.75f,2.4f),V(0,1.0f,.3f),V(0,.98f,.3f),40,38)
               .Dolly(LaunchAt-.05f,V(1.05f,1.6f,-1.3f),V(1.0f,1.66f,-1.5f),V(-.4f,3.2f,30),V(-.4f,6.5f,40),46,44)
               .Dolly(Hits[0]-.2f,V(1.25f,.5f,3.65f),V(1.05f,.55f,3.3f),V(0,1.2f,0),V(0,1.2f,0),50,48).Handheld(.5f)
               .Dolly(Hits[2]-.08f,V(-3.2f,1.0f,.85f),V(-3.4f,1.1f,1.2f),V(0,1.15f,.3f),V(0,1.1f,.3f),44,42).Handheld(.4f)
               .Dolly(SilenceAt,V(.42f,1.3f,3.45f),V(.32f,1.3f,2.85f),V(0,1.25f,0),V(0,1.25f,0),36,34)
               .Dolly(BashAt-.05f,V(-1.0f,.6f,4.2f),V(-1.1f,.62f,4.5f),V(0,1.0f,.5f),V(0,1.0f,.6f),44,44);
            HeroFull(StanceAt-.25f,-1,4.6f,32,.3f);
            MakeBlasts();
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
        }
        public override string SetBank=>Bank;

        // ---- the body: keyed (no match clips), the right arm solved onto the shield's way ----
        private readonly NativeIntroWield Shield=new NativeIntroWield();
        // (carried and at rest the shield's face hangs below the fist; raised against the barrage and in the bash it rides above it, over the head)
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)=>Wield(Shield,t,pose,root,rotation,false);
        /// <summary>The left fist braces the shield's rim from the guard through the barrage and the silence (Core NativeIntroGrip: the
        /// keyed off arm and the drawn fist on the drawn rim); it lets go to wind up the bash.</summary>
        public bool GripAt(float t,out NativeIntroGripAt grip)
        {
            float w=NativeEntranceMath.Smooth((t-(GuardAt-.2f))/.2f)*(1-NativeEntranceMath.Smooth((t-(BashAt-.3f))/.2f));
            grip=new NativeIntroGripAt(w);return w>.001f;
        }

        // ---- the barrage ----
        /// <summary>Bolt `i`'s head at `t` (false: not in flight): launched Flight s before its hit, on an arc from the ridge to the shield.</summary>
        private static bool Bolt(int i,float t,Vector3 face,out Vector3 at,out Vector3 v)
        {
            float s=t-(Hits[i]-Flight);at=v=Vector3.Zero;if(s<0||s>Flight)return false;float u=s/Flight;var o=Origin(i);
            at=Vector3.Lerp(o,face,u)+new Vector3(0,18*u*(1-u),0);v=(face-o)/Flight+new Vector3(0,18*(1-2*u)/Flight,0);return true;
        }
        /// <summary>Debris rock `k` at `t`: at rest, then thrown by its near miss.</summary>
        private static void Debris(int k,float t,out Vector3 at,out Quaternion turn)
        {
            var r=DebrisRest[k];at=U(r.X,.1f,r.Y);turn=Quaternion.Identity;int m=DebrisMiss[k];float s=t-MissAt[m];if(s<=0)return;
            var away=at-Misses[m];away.Y=0;away=away.LengthSquared()>1e-4f?Vector3.Normalize(away):Vector3.UnitZ;var v=away*(2.5f+NativeEntranceMath.Rand(39101,k))+new Vector3(0,3.2f+NativeEntranceMath.Rand(39103,k),0);
            float land=2*v.Y/9.8f;if(s>land)s=land+(s-land)*0;at+=v*s+new Vector3(0,-4.9f*s*s,0);if(at.Y<NativeEntranceStage.FloorY+.1f)at.Y=NativeEntranceStage.FloorY+.1f;
            turn=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.3f,.8f,.5f)),Math.Min(s,land)*9);
        }
        /// <summary>The near misses through the game's own blast (fire, smoke, flash and the world light), in front and to the sides of the
        /// pilot: built with the script (the runtime reads Blasts every frame; Plan runs only when the anchors are first built).</summary>
        private void MakeBlasts(){var list=new NativeEntranceBlast[Misses.Length];for(int i=0;i<Misses.Length;i++)list[i]=new NativeEntranceBlast{At=MissAt[i],Radius=2.6f,Size=1.3f,Style=NativeBlastStyle.Fire,Position=Misses[i]+new Vector3(0,.4f,0)};Blasts=list;}
        protected override void Plan()=>MakeBlasts();

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("breach/static"))return false;
            float coals=.85f+.15f*(float)Math.Sin(t*2.1f)*(Context.Reduced?0:1);set.Piece("breach/static",SetAt,Quaternion.Identity,1,White,White*coals);
            for(int k=0;k<debrisProps.Length;k++){Debris(k,t,out var at,out var turn);set.Piece(debrisProps[k],at,turn,1,White,White);}
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x2a221e),Dusk,.8f,NativeEntranceFx.Hex(0x6a3a2a));

        // ---- the light ----
        /// <summary>How hot the shield's face glows (each hit adds, cooling slowly).</summary>
        private static float Heat(float t){float h=0;for(int i=0;i<Hits.Length;i++)h+=NativeEntranceMath.Pulse(t,Hits[i],.02f,1.6f)*.45f;return Math.Min(1.2f,h);}
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Sky;l.Fog=NativeEntranceFx.Hex(0x3a2422);l.FogNear=10;l.FogFar=75;l.DriveProbe=true;
            l.Sky=NativeEntranceFx.Hex(0x6a4a5a,.34f);l.Ground=NativeEntranceFx.Hex(0x2a1a14,.3f);
            // The low sun behind the far ridge (the pilot faces it), a cool sky fill from behind the wall, the braziers either side.
            l.Directional(new Vector3(.3f,.28f,1),Dusk*.95f);
            l.Directional(new Vector3(-.4f,.8f,-1),Steel*.18f);
            float fl=Context.Reduced?1:.9f+.1f*(float)Math.Sin(t*7.7f);
            l.Add(U(1.75f,1.2f,-2.05f),Fire*(2.2f*fl),5);l.Add(U(-1.8f,1.2f,-2.0f),Fire*(2.2f*fl),5);
            // Each hit's flash on the shield's face, its heat after; the bash's.
            float hit=0;for(int i=0;i<Hits.Length;i++)hit+=Flash(t,Hits[i],.01f,.12f);hit=Math.Min(1.4f,hit);var face=FaceAt(t,.1f);
            if(hit>.01f)l.Add(face+new Vector3(0,0,.4f),(Hot*2+White)*(5*hit),7);
            float heat=Heat(t);if(heat>.02f)l.Add(face+new Vector3(0,0,.25f),Fire*(1.6f*heat),2.6f);
            float bash=Flash(t,BashAt+.3f,.02f,.3f);if(bash>.01f)l.Add(face+new Vector3(0,0,.6f),Steel*(2.2f*bash),5);
            float name=NativeEntranceMath.Ramp(t,CardAt-.25f,CardAt+.35f);if(name>.01f)l.Add(new Vector3(-1.6f,2.0f,1.8f),Steel*(1.5f*name),8);
        }

        // ---- the effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY;
            // Smoke drifting across the breach all through, thicker after the barrage.
            float thick=.5f+.5f*NativeEntranceMath.Ramp(t,Hits[0],SilenceAt)*(1-.6f*NativeEntranceMath.Ramp(t,BashAt+.2f,BashAt+.9f));
            for(int i=0,n=low?5:11;i<n;i++)
            {
                float u=((t*.06f+i/(float)n)%1f);var p=new Vector3(-7+14*u,.6f+1.6f*NativeEntranceMath.Rand(39201,i),-.4f+4.5f*NativeEntranceMath.Rand(39203,i));
                // The bash blows it back, away from the shield.
                float blow=NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-BashAt-.25f)/.8f));p+=new Vector3(0,.3f*blow,2.6f*blow*(1-Math.Abs(p.X)/8));
                float size=1.6f+.8f*NativeEntranceMath.Rand(39205,i);fx.Smoke(p,Smoke,.22f*thick*NearFade(p,size),size,.5f,39210+i,floor);
            }
            // Ash drifting down through the dusk light.
            for(int i=0,n=low?8:20;i<n;i++)
            {
                float per=4+2*NativeEntranceMath.Rand(39351,i),u=((t/per)+NativeEntranceMath.Rand(39353,i))%1f;var p=new Vector3(NativeEntranceMath.Signed(39355,i)*5+.5f*(float)Math.Sin(t*.9f+i),4.5f-4.4f*u,-1.5f+6*NativeEntranceMath.Rand(39357,i));
                fx.Glow(p,new Vector3(.32f,.29f,.27f)*.35f,.6f,.025f,0,u,39360+i);
            }
            // Embers off the braziers.
            NativeEntranceFx.Embers(fx,t,-4,Total,U(1.75f,1.1f,-2.05f),.25f,low?3:6,.8f,1.6f,Fire*2,.02f,39300,q,.3f);NativeEntranceFx.Embers(fx,t,-4,Total,U(-1.8f,1.1f,-2.0f),.25f,low?3:6,.8f,1.6f,Fire*2,.02f,39320,q,.3f);
            // The launch: lights climbing off the far ridge.
            if(t>=LaunchAt-.1f&&t<LaunchAt+1.4f){for(int i=0;i<Hits.Length+3;i++){float s=t-LaunchAt-.08f*i;if(s<0)continue;var o=new Vector3(-16+5*i,8,74);var p=o+new Vector3(0,9*s,-5*s);fx.Glow(p,Fire*(2.2f*(1-NativeEntranceMath.Ramp(s,.9f,1.3f))),0,.9f,1,0,39400+i);}}
            // The bolts in flight: a burning head and a smoking tail, laid onto the camera's plane where long.
            for(int i=0;i<Hits.Length;i++)
            {
                if(!Bolt(i,t,FaceAt(t),out var at,out var v))continue;var tail=v*.09f;
                Line(fx,at,tail,Hot*2.4f,.03f,.3f,0,39500+i*2);fx.Glow(at,Fire*1.8f,0,.22f,1,0,39501+i*2);
                for(int j=0;j<(low?2:5);j++){float a=.06f*(j+1);var p=at-v*a;fx.Smoke(p,Smoke*1.5f,.28f*(1-j/6f),.18f+.1f*j,j/6f,39600+i*8+j,floor);}
            }
            // Each hit: a white-hot burst on the face, sparks and burning splinters thrown back and out, a puff of smoke.
            for(int i=0;i<Hits.Length;i++)
            {
                float a=t-Hits[i];if(a<0||a>1.2f)continue;var face=HitFace(i,t);
                if(a<.15f)fx.Glow(face+new Vector3(0,0,.12f),(White*2+Hot)*(2.2f*(1-a/.15f)),0,.35f+1.2f*a,1,a/.15f,39700+i);
                NativeEntranceFx.Sparks(fx,t,Hits[i],face+new Vector3(0,0,.1f),new Vector3(0,.3f,1),1.2f,low?10:24,6.5f,.7f,7,.6f,Hot*2.4f,Fire*1.6f,.012f,39710+i*40,q,floor);
                if(a<.9f){float u=a/.9f;fx.Smoke(face+new Vector3(.1f*(i%2==0?1:-1),.1f+.3f*u,.25f+.4f*u),Smoke*1.4f,.35f*(1-u),.3f+.6f*u,u,39900+i,floor);}
            }
            // The shield's face glowing hot after the barrage, cooling.
            float heat=Heat(t);if(heat>.02f)fx.Glow(FaceAt(t)+new Vector3(0,0,.08f),Fire*(.6f*heat),0,.45f,1,0,39950);
            // Near misses: earth thrown up in clods (the blast itself is the game's own fire and smoke).
            for(int m=0;m<Misses.Length;m++)
            {
                float a=t-MissAt[m];if(a<0||a>1.3f)continue;int n=low?6:14;
                for(int i=0;i<n;i++){var v0=new Vector3(NativeEntranceMath.Signed(40001+m,i)*2.2f,3+3*NativeEntranceMath.Rand(40011+m,i),NativeEntranceMath.Signed(40021+m,i)*2.2f);var p=Misses[m]+new Vector3(0,.1f,0)+v0*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<floor)continue;fx.Streak(p,v0+new Vector3(0,-9.8f*a,0),Earth*(1.2f*(1-a/1.3f)),.035f,.04f,.02f,.2f,a/1.3f,40100+m*20+i);}
            }
            // The bash: a wall of air from the shield's face, a ring of dust pushed out along the floor.
            NativeEntranceFx.GroundDust(fx,t,BashAt+.3f,new Vector3(0,floor,.9f),low?6:14,.5f,4.5f,1.0f,new Vector3(.3f,.26f,.22f),.35f,.6f,40200,q,floor);
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.holdtheline.wind",null,"fire",.25f,.7f),
            new NativeIntroCue(PlantAt+.3f,"intro2.holdtheline.plant","melee.shield.block","shield",.8f,.8f),
            new NativeIntroCue(LaunchAt,"intro2.holdtheline.launch","structure.whoosh","explosion",.4f,.7f),
            new NativeIntroCue(2.75f,"intro2.holdtheline.hit","melee.shield.block","shield",.95f),
            new NativeIntroCue(3.0f,"intro2.holdtheline.miss","blast.fire","explosion",.7f),
            new NativeIntroCue(3.15f,"intro2.holdtheline.hit2","impact.metal","metal",.9f),
            new NativeIntroCue(3.5f,"intro2.holdtheline.hit3","melee.shield.block","shield",.95f,.9f),
            new NativeIntroCue(3.72f,"intro2.holdtheline.miss2","blast.fire","explosion",.7f,.9f),
            new NativeIntroCue(3.9f,"intro2.holdtheline.hit4","impact.metal","metal",.9f,.85f),
            new NativeIntroCue(4.12f,"intro2.holdtheline.miss3","blast.debris","explosion",.6f),
            new NativeIntroCue(4.3f,"intro2.holdtheline.hit5","melee.shield.block","shield",1f,.8f),
            new NativeIntroCue(SilenceAt+.15f,"intro2.holdtheline.hot","magic.fire.hit","fire",.3f,.6f),
            new NativeIntroCue(BashAt+.3f,"intro2.holdtheline.bash","impact.metal","shield",1f,.7f),
            new NativeIntroCue(CardAt,"intro2.holdtheline.name","structure.thud","stone",.5f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.96f,.98f,1),Steel*1.2f);card.Style=NativeIntroCardStyle.Slam;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
