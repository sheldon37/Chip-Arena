using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Afterimage", the daggers' intro (intros v2; docs/INTROS-MELEE.md): faster than its own image. 7.2 s, the name at 5.8 s. A night
    /// market lane (Lantern Alley: timber shopfronts, wet cobbles, paper lanterns strung overhead, steam from the grates). The pilot walks up
    /// the lane and stops; two echoes of it peel off to either side, one teal and one magenta, each a beat behind the body. It cuts, and the
    /// echoes cut after it, each throwing its edge up through a lantern's cord: the lanterns drop and roll on the cobbles. A second exchange;
    /// then the echoes slide back into the body as it crosses its daggers, three images landing as one, and it stands under the name.
    /// The model plays the daggers' clips (NativeBladeIntro); the keyed rig plays the game's own captures of them (the holds are the match's),
    /// and the echoes are that rig's ghosts a moment behind, drawn as the model. Presentation only.
    /// </summary>
    public sealed class NativeIntroAfterimage:NativeIntroMeleeBlade
    {
        public const string Title="Afterimage";
        public const float Total=7.2f,CardAt=5.8f;
        /// <summary>The beats (presentation s): the stop, the split, the first cut, the second, the convergence, the stance.</summary>
        public const float StopAt=1.6f,SplitAt=2.15f,CutAAt=2.5f,CutCAt=3.65f,ConvergeAt=4.6f,StanceAt=5.15f;
        private const float CutLength=1.0f,HitA=.536f,HitC=.494f,LagL=.16f,LagR=.32f,Spread=1.25f;
        private static readonly Vector3 Teal=NativeEntranceFx.Hex(0x37ffd8),Magenta=NativeEntranceFx.Hex(0xff4fc8),Mint=NativeEntranceFx.Hex(0x87ffd5),Lamp=NativeEntranceFx.Hex(0xffb070),
            Night=NativeEntranceFx.Hex(0x06070c),Steam=new Vector3(.36f,.38f,.42f);
        public override string Spec=>"daggers";
        protected override float TipLength(bool left)=>.84f;
        private static readonly Vector3 WalkFrom=Local(0,0,-2.4f);// (up the lane along its facing)
        // The lanterns (the alley set's rigs: their cords' rings, Unity space) and which echo's cut drops which (-1: none).
        private static readonly Vector3[] Lanterns={new Vector3(.1f,3.35f,6.0f),new Vector3(-.35f,3.55f,3.4f),new Vector3(.25f,3.2f,.8f),new Vector3(-.15f,3.45f,-1.8f),new Vector3(.4f,3.6f,-4.4f),new Vector3(-.3f,3.3f,-7.0f),new Vector3(.15f,3.5f,-9.5f),new Vector3(-.4f,3.25f,-12.0f)};
        private static readonly string[] lanternProps={"alley/lantern-0","alley/lantern-1","alley/lantern-2","alley/lantern-3","alley/lantern-4","alley/lantern-5","alley/lantern-6","alley/lantern-7"};
        /// <summary>When lantern k's cord is cut (presentation s; never for those that stay up).</summary>
        private static float CutAt(int k)=>k==2?CutAAt+HitA+LagL:k==3?CutAAt+HitA+LagR:k==1?CutCAt+HitC+LagR:1e6f;

        public NativeIntroAfterimage():base(NativeIntroCatalog.WeaponPrefix+"daggers",Total,CardAt,CutAAt+HitA,0x87ffd5)
        {
            RootPath=(t,r)=>r+Way(t);
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips) ----
            // The walk up the lane on planted steps (the root goes up the lane at an even pace, Way), the stop, the lunge's step, back.
            Start=Stance(.11f,-2.32f,.08f,-.11f,-2.72f,-.12f);
            Steps=new[]
            {
                Step(false,0,.4f,-.11f,-1.74f,-.1f,.09f),Step(true,.38f,.78f,.11f,-1.14f,.08f,.09f),Step(false,.76f,1.16f,-.11f,-.56f,-.1f,.09f),
                Step(true,1.12f,1.5f,.15f,.04f,.15f,.08f),Step(false,1.42f,1.72f,-.19f,-.18f,-.45f,.06f),
                Step(true,CutCAt+.2f,CutCAt+.45f,.2f,.42f,.15f,.08f),Step(true,ConvergeAt,ConvergeAt+.3f,.15f,.05f,.15f,.06f),
            };
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // Walking: the body rides up over each planted foot, the arms swing against the legs, the head steady.
            for(int i=0;i<4;i++)
            {
                float t0=i*.38f;bool right=i%2==0;// (the right foot steps first)
                k.Key(t0,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.035f).J(B,.08f).J(C,0,right?.06f:-.06f,0).J(Hd,.02f,right?-.05f:.05f,0)
                 .J(RA,right?.28f:-.32f,0,-.18f).J(RE,-.45f).J(LA,right?-.32f:.28f,0,.18f).J(LE,-.45f);
                k.Key(t0+.19f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(.0f).J(B,.07f).J(C,0,0,0).J(Hd,.02f,0,0)
                 .J(RA,-.02f,0,-.18f).J(RE,-.4f).J(LA,-.02f,0,.18f).J(LE,-.4f).PassKey();
            }
            // The stop: weight settles onto both feet; the head comes up.
            k.Key(1.52f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.06f).J(B,.1f).J(C,0,.04f,0).J(Hd,-.02f,0,0).J(RA,.15f,0,-.2f).J(RE,-.5f).J(LA,-.2f,0,.2f).J(LE,-.5f);
            k.Key(StopAt+.18f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.1f).J(B,.12f).J(C,0,0,0).J(Hd,-.04f,0,0).J(LL,-.15f,0,.14f).J(LK,.3f).J(RL,.1f,0,-.14f).J(RK,.3f)
             .J(RA,-.3f,0,-.25f).J(RE,-.8f).J(LA,-.3f,0,.25f).J(LE,-.8f);
            // Ready as the echoes peel away: low, the right dagger forward, the left drawn back.
            k.Key(SplitAt+.1f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.2f).J(B,.18f).J(Sp,.05f).J(C,.05f,-.2f,0).J(Hd,-.02f,.15f,0).J(LL,-.4f,0,.16f).J(LK,.65f).J(RL,.15f,0,-.16f).J(RK,.55f)
             .J(RA,-1.0f,0,-.3f).J(RE,-.9f).J(LA,-.4f,0,.35f).J(LE,-1.2f);
            // Cut A: wound to the left, the right dagger over the left shoulder; a backhand down across the front, the left following through.
            k.Key(CutAAt+.35f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.22f).J(B,.12f).J(Sp,0,.18f,0).J(C,0,.4f,0).J(Hd,0,-.2f,0).J(LL,-.42f,0,.16f).J(LK,.7f).J(RL,.18f,0,-.16f).J(RK,.6f)
             .J(RA,-1.6f,0,.4f).J(RE,-1.6f).J(LA,-.5f,0,.4f).J(LE,-1.1f);
            k.Key(CutAAt+HitA,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.28f).J(B,.25f).J(Sp,0,-.12f,0).J(C,.05f,-.3f,0).J(Hd,.04f,.15f,0).J(LL,-.5f,0,.16f).J(LK,.85f).J(RL,.2f,0,-.16f).J(RK,.7f)
             .J(RA,-1.0f,0,-.6f).J(RE,-.3f).J(LA,-1.4f,0,.6f).J(LE,-.8f).ImpactKey();
            k.Key(CutAAt+HitA+.18f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.28f).J(B,.22f).J(Sp,0,.05f,0).J(C,.05f,.25f,0).J(Hd,.04f,-.1f,0).J(LL,-.5f,0,.16f).J(LK,.85f).J(RL,.2f,0,-.16f).J(RK,.7f)
             .J(RA,-.6f,0,-.5f).J(RE,-.4f).J(LA,-1.2f,0,.1f).J(LE,-.4f).PassKey();
            k.Key(CutAAt+CutLength,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.2f).J(B,.16f).J(C,.03f,0,0).J(Hd,0,0,0).J(LL,-.4f,0,.16f).J(LK,.65f).J(RL,.15f,0,-.16f).J(RK,.55f)
             .J(RA,-.9f,0,-.3f).J(RE,-.9f).J(LA,-.6f,0,.35f).J(LE,-1.0f);
            // Cut C: drawn back, then a lunge onto the left foot driving both daggers straight out.
            k.Key(CutCAt+.2f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.24f).J(B,.08f).J(C,-.04f,0,0).J(Hd,-.04f,0,0).J(LL,-.35f,0,.16f).J(LK,.6f).J(RL,.2f,0,-.16f).J(RK,.7f)
             .J(RA,-.4f,0,-.35f).J(RE,-1.7f).J(LA,-.4f,0,.35f).J(LE,-1.7f);
            k.Key(CutCAt+HitC,stand).Root(0,.88f,.22f,Facing).Planted(1).Drop(-.3f).J(B,.35f).J(Sp,.05f).J(C,.05f,0,0).J(Hd,-.12f,0,0).J(LL,-.95f,0,.16f).J(LK,1.05f).J(RL,.4f,0,-.16f).J(RK,.3f)
             .J(RA,-1.5f,0,-.15f).J(RE,-.1f).J(LA,-1.3f,0,.15f).J(LE,-.15f).ImpactKey();
            k.Key(CutCAt+HitC+.3f,stand).Root(0,.88f,.22f,Facing).Planted(1).Drop(-.28f).J(B,.3f).J(Sp,.05f).J(C,.04f,0,0).J(Hd,-.1f,0,0).J(LL,-.9f,0,.16f).J(LK,1.0f).J(RL,.38f,0,-.16f).J(RK,.32f)
             .J(RA,-1.45f,0,-.15f).J(RE,-.15f).J(LA,-1.25f,0,.15f).J(LE,-.2f);
            // The echoes fold back in: it draws back to the spot and crosses the daggers before its chest.
            k.Key(ConvergeAt+.35f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.14f).J(B,.08f).J(C,-.04f,0,0).J(Hd,-.06f,0,0).J(LL,-.2f,0,.14f).J(LK,.35f).J(RL,.12f,0,-.14f).J(RK,.35f)
             .J(RA,-1.3f,0,.2f).J(RE,-1.5f).J(LA,-1.3f,0,-.2f).J(LE,-1.5f).ImpactKey();
            // The stance under the name: low and easy, daggers out at the sides.
            k.Key(StanceAt+.3f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.12f).J(B,.1f).J(C,0,.06f,0).J(Hd,-.04f,0,0).J(LL,-.2f,0,.16f).J(LK,.35f).J(RL,.12f,0,-.16f).J(RK,.38f)
             .J(RA,-.5f,0,-.4f).J(RE,-.7f).J(LA,-.45f,0,.4f).J(LE,-.8f).PassKey();
            k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.12f).J(B,.08f).J(C,0,.08f,0).J(Hd,-.05f,.04f,0).J(LL,-.2f,0,.16f).J(LK,.35f).J(RL,.12f,0,-.16f).J(RK,.38f)
             .J(RA,-.5f,0,-.4f).J(RE,-.7f).J(LA,-.45f,0,.4f).J(LE,-.8f);Keys=k;
            // ---- the daggers' ways (each hand's grip and blade, in the pilot's frame: it walks with the root) ----
            for(int i=0;i<4;i++)
            {
                float t0=i*.38f;bool right=i%2==0;
                RightDagger.Key(t0,V(-.25f,.76f,right?-.12f:.18f),V(0,-.3f,.95f)).Key(t0+.19f,V(-.25f,.74f,.03f),V(0,-.32f,.95f)).Pass();
                LeftDagger.Key(t0,V(.25f,.76f,right?.18f:-.12f),V(0,-.3f,.95f)).Key(t0+.19f,V(.25f,.74f,.03f),V(0,-.32f,.95f)).Pass();
            }
            RightDagger.Key(1.52f,V(-.24f,.78f,.05f),V(0,-.25f,.97f)).Key(StopAt+.18f,V(-.22f,.86f,.16f),V(0,-.1f,.99f))
                .Key(SplitAt+.1f,V(-.15f,1.05f,.4f),V(0,.15f,.99f))
                .Key(CutAAt+.35f,V(.14f,1.3f,.12f),V(.35f,.6f,-.72f))
                .Key(CutAAt+HitA-.06f,V(.02f,1.18f,.45f),V(-.3f,.2f,.93f)).Pass()
                .Key(CutAAt+HitA,V(-.14f,.98f,.48f),V(-.65f,-.4f,.65f)).Impact()
                .Key(CutAAt+HitA+.18f,V(-.38f,.76f,.24f),V(-.7f,-.6f,-.4f))
                .Key(CutAAt+CutLength,V(-.18f,.98f,.36f),V(0,.1f,1))
                .Key(CutCAt+.2f,V(-.2f,1.0f,-.04f),V(0,0,1))
                .Key(CutCAt+HitC,V(-.12f,1.08f,.72f),V(0,-.04f,1)).Impact()
                .Key(CutCAt+HitC+.3f,V(-.12f,1.06f,.68f),V(0,-.04f,1))
                .Key(ConvergeAt+.35f,V(.06f,1.14f,.32f),V(.5f,.75f,.4f)).Impact()
                .Key(StanceAt+.3f,V(-.26f,.9f,.3f),V(-.3f,-.2f,.93f))
                .Key(Total,V(-.26f,.9f,.3f),V(-.3f,-.2f,.93f));
            LeftDagger.Key(1.52f,V(.24f,.78f,.05f),V(0,-.25f,.97f)).Key(StopAt+.18f,V(.22f,.86f,.12f),V(0,-.1f,.99f))
                .Key(SplitAt+.1f,V(.2f,.9f,0),V(.1f,.3f,.95f))
                .Key(CutAAt+.35f,V(.26f,.86f,-.04f),V(0,.2f,.98f))
                .Key(CutAAt+HitA,V(.26f,1.1f,.3f),V(.5f,.5f,.7f)).Pass()
                .Key(CutAAt+HitA+.18f,V(-.04f,.96f,.46f),V(-.7f,-.3f,.65f)).Impact()
                .Key(CutAAt+CutLength,V(.2f,.92f,.22f),V(0,.2f,.98f))
                .Key(CutCAt+.2f,V(.18f,.82f,-.04f),V(0,.1f,1))
                .Key(CutCAt+HitC,V(.12f,.84f,.64f),V(0,.15f,.99f)).Impact()
                .Key(CutCAt+HitC+.3f,V(.12f,.84f,.6f),V(0,.15f,.99f))
                .Key(ConvergeAt+.35f,V(-.06f,1.12f,.32f),V(-.5f,.75f,.4f)).Impact()
                .Key(StanceAt+.3f,V(.26f,.88f,.16f),V(.35f,0,.94f))
                .Key(Total,V(.26f,.88f,.16f),V(.35f,0,.94f));
            // The camera: low in front as it walks up; close as the echoes peel off; down from a balcony on the three cutting and the
            // lanterns dropping; low at the side for the second exchange; in front as they converge; the hero.
            Rig.Dolly(0,V(-.6f,1.0f,4.6f),V(-.5f,1.05f,3.65f),V(0,1.3f,-1.8f),V(0,1.25f,-.3f),42,40)
               .Dolly(StopAt,V(-1.1f,1.0f,1.95f),V(-.9f,1.02f,1.62f),V(0,1.3f,0),V(0,1.28f,0),34,32)
               .Dolly(CutAAt-.1f,V(1.6f,3.6f,3.4f),V(1.3f,3.7f,3.0f),V(0,1.0f,-.4f),V(0,1.1f,-.6f),50,48)
               .Dolly(CutCAt-.05f,V(-1.5f,.45f,3.1f),V(-1.25f,.5f,2.7f),V(0,1.05f,0),V(0,1.1f,0),48,46).Handheld(.4f)
               .Dolly(ConvergeAt-.05f,V(.3f,.8f,3.2f),V(.25f,.85f,2.7f),V(0,1.1f,0),V(0,1.12f,0),38,36);
            HeroFull(StanceAt+.05f,1,4.4f,32,.35f);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
        }
        public override string SetBank=>Bank;
        /// <summary>The walk up the lane (source frame, from the keyed root): from Start to the mark by the stop.</summary>
        public static Vector3 Way(float t)=>WalkFrom*(1-NativeEntranceMath.Clamp01(t/StopAt));

        // ---- the body: keyed (no match clips), both arms solved onto the daggers' ways ----
        private readonly NativeIntroWield RightDagger=new NativeIntroWield(),LeftDagger=new NativeIntroWield();
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation){Wield(RightDagger,t,pose,root,rotation,false);Wield(LeftDagger,t,pose,root,rotation,true);}

        // ---- the echoes: the keyed rig's ghosts a moment behind, peeling out to either side and folding back in ----
        /// <summary>How far out the echoes stand (0..1) at `t`.</summary>
        private static float Out(float t)=>NativeEntranceMath.Smoother((t-SplitAt)/.3f)*(1-NativeEntranceMath.Smoother((t-ConvergeAt)/.45f));
        private static Vector3 Side=>Vector3.Transform(Vector3.UnitX,Quaternion.CreateFromAxisAngle(Vector3.UnitY,Facing));
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float o=Out(t);if(o<=.002f||into.Length<2)return 0;float lagOut=NativeEntranceMath.Smooth((t-SplitAt)/.4f);
            into[0]=new NativeEntranceGhost{Lag=LagL*lagOut,Opacity=.6f*o,Color=Teal,Offset=Side*(Spread*o)};
            into[1]=new NativeEntranceGhost{Lag=LagR*lagOut,Opacity=.6f*o,Color=Magenta,Offset=-Side*(Spread*o)};
            return 2;
        }

        // ---- the lanterns: swaying on their cords; the cut ones dropping, bouncing and rolling to a stop ----
        /// <summary>Lantern k's paper: its middle under the ring and its radius (the alley set's shapes: k 1 tall, 2 gourd, 3 round).</summary>
        private static float Middle(int k)=>k==1?.42f:k==2?.36f:.35f;
        private static float Girth(int k)=>k==1?.17f:k==2?.2f:.215f;
        private static Quaternion Hang(int k,float t){float sway=.06f*(float)Math.Sin(t*1.3f+k*1.7f)+.03f*(float)Math.Sin(t*2.1f+k);return Quaternion.CreateFromAxisAngle(Vector3.UnitX,sway)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.5f*sway);}
        /// <summary>Lantern k at `t`: where its prop goes (its ring), its turn and its paper's middle (for its light).</summary>
        private static void Lantern(int k,float t,out Vector3 at,out Quaternion turn,out Vector3 middle)
        {
            var r=Lanterns[k];var ring=U(r.X,r.Y,r.Z);float cy=Middle(k),cut=CutAt(k),s=t-cut;
            if(s<=0){turn=Hang(k,t);at=ring;middle=ring+Vector3.Transform(new Vector3(0,-cy,0),turn);return;}
            // Off its cord: it falls turning onto its side (the paper's middle under gravity, knocked down the lane), lands, hops once and
            // rolls a little further about its own axis to a stop. The turn is continuous throughout: hung, tumbling, lying, rolling.
            var hung=Hang(k,cut);var mid0=ring+Vector3.Transform(new Vector3(0,-cy,0),hung);
            // (knocked toward the nearer shopfront: the floor between the lens and the pilot stays clear for the name)
            float xs=mid0.X<0?-1:1,rad=Girth(k),rest=NativeEntranceStage.FloorY+rad,fall=(float)Math.Sqrt(2*Math.Max(.1f,mid0.Y-rest)/9.8f);
            const float Knock=.9f,Brake=2,Spin=2.5f;
            var lie=Quaternion.CreateFromAxisAngle(Vector3.UnitX,1.45f);// (on its side, its axis along the lane)
            if(s<fall)
            {
                float u=s/fall;turn=Quaternion.Slerp(hung,lie,NativeEntranceMath.Smooth(u))*Quaternion.CreateFromAxisAngle(Vector3.UnitY,Spin*u);
                middle=mid0+new Vector3(xs*Knock*s,-4.9f*s*s,0);
            }
            else
            {
                float b=s-fall,run=Knock/Brake*(1-(float)Math.Exp(-Brake*b)),hop=Math.Max(0,1.0f*b-4.9f*b*b);
                // (rolling across the lane about its own axis, which lies along z: the spin that carries it `run` m without sliding)
                turn=lie*Quaternion.CreateFromAxisAngle(Vector3.UnitY,Spin-xs*run/rad);
                middle=new Vector3(mid0.X+xs*(Knock*fall+run),rest+hop,mid0.Z);
            }
            at=middle+Vector3.Transform(new Vector3(0,cy,0),turn);
        }
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("alley/static"))return false;
            set.Piece("alley/static",SetAt,Quaternion.Identity,1,White,White);
            for(int k=0;k<Lanterns.Length;k++){Lantern(k,t,out var at,out var turn,out _);float lit=1-.7f*NativeEntranceMath.Ramp(t,CutAt(k)+.6f,CutAt(k)+1.4f);set.Piece(lanternProps[k],at,turn,1,White,White*lit);}
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x15161c),Mint,.8f,NativeEntranceFx.Hex(0x3a2440));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x0d0c14);l.FogNear=6;l.FogFar=30;l.DriveProbe=true;
            l.Sky=NativeEntranceFx.Hex(0x2a2440,.3f);l.Ground=NativeEntranceFx.Hex(0x120c10,.3f);
            l.Directional(new Vector3(.3f,1,.2f),NativeEntranceFx.Hex(0x8090c0,.18f));
            // The lanterns overhead (the near ones), the shop signs' neon either side, the echoes' own glow.
            for(int k=1;k<=3;k++){Lantern(k,t,out _,out _,out var mid);float lit=1-.7f*NativeEntranceMath.Ramp(t,CutAt(k)+.6f,CutAt(k)+1.4f);mid.Y=Math.Max(mid.Y,NativeEntranceStage.FloorY+.15f);l.Add(mid,Lamp*(2.2f*lit),5.5f);}
            l.Add(new Vector3(2.1f,2.4f,-1.5f),Magenta*1.6f,6);l.Add(new Vector3(-2.1f,2.4f,1.5f),Teal*1.4f,6);
            float o=Out(t);if(o>.01f){l.Add(Side*(Spread*o)+new Vector3(0,1.2f,0),Teal*(1.2f*o),3.5f);l.Add(-Side*(Spread*o)+new Vector3(0,1.2f,0),Magenta*(1.2f*o),3.5f);}
            float join=Flash(t,ConvergeAt+.35f,.02f,.3f);if(join>.01f)l.Add(new Vector3(0,1.2f,.4f),(White*2+Mint)*(4*join),6);
        }

        // ---- the effects ----
        private readonly Vector3[] tips=new Vector3[10];
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY;
            // Steam rising off the grates.
            for(int g=0;g<4;g++){float z=g==0?6.4f:g==1?1.2f:g==2?-4.6f:-9.4f;for(int i=0;i<(low?2:4);i++){float u=((t*.35f+i/4f+g*.21f)%1f);var p=U(.6f*(float)Math.Sin(i*2.1f+g),.1f+1.8f*u,z+.2f*(float)Math.Cos(i+t*.3f));fx.Smoke(p,Steam,.16f*(1-u),.3f+.7f*u,u,40500+g*8+i,floor);}}
            // The daggers' trails: the body's mint (its blades as drawn), each echo's in its own colour, a moment behind.
            float o=Out(t);
            bool cutting=t>=CutAAt+.25f&&t<CutAAt+CutLength||t>=CutCAt+.2f&&t<CutCAt+CutLength;
            if(cutting)
            {
                float from=t<CutCAt?CutAAt:CutCAt,until=t<CutCAt?CutAAt+CutLength:CutCAt+CutLength-.05f;
                BladeSweep(fx,t,0,from,.2f,Mint*1.6f,White,.014f,.2f,40600,low,until,.5f);BladeSweep(fx,t,1,from,.2f,Mint*1.6f,White,.014f,.2f,40640,low,until,.5f);
                if(o>.01f){Echo(fx,t,LagL,0,Side*(Spread*o),Teal*1.4f,Teal,.012f,40700,low);Echo(fx,t,LagR,0,-Side*(Spread*o),Magenta*1.4f,Magenta,.012f,40760,low);}
            }
            else if(o>.01f&&t>=CutAAt+.25f&&t<CutCAt+CutLength+LagR+.2f)
            {
                // (the echoes still finishing their cuts after the body)
                Echo(fx,t,LagL,0,Side*(Spread*o),Teal*1.4f,Teal,.012f,40700,low);Echo(fx,t,LagR,0,-Side*(Spread*o),Magenta*1.4f,Magenta,.012f,40760,low);
            }
            // Each cut lantern: the echo's edge thrown up through its cord as a thin crescent of light, then sparks off the falling paper.
            for(int k=1;k<=3;k++)
            {
                float s=t-CutAt(k);if(s<-.05f||s>1.4f)continue;var r=Lanterns[k];var cord=U(r.X,r.Y-.05f,r.Z);
                bool left=k==2;var from=Anchor(NativeEntranceAnchor.RightTip,t-(left?LagL:LagR))+(left?Side:-Side)*(Spread*o);
                if(s>=-.12f&&s<.2f)
                {
                    // (from the echo's tip a beat before the cut, through the cord at it and a little past, a .5 m stroke along its flight)
                    float u=NativeEntranceMath.Clamp01((s+.12f)/.14f),fade=1-NativeEntranceMath.Ramp(s,.02f,.2f);var dir=cord-from;
                    if(dir.LengthSquared()>1e-4f){dir=Vector3.Normalize(dir);var head=Vector3.Lerp(from,cord,u)+dir*(.35f*NativeEntranceMath.Ramp(s,0,.2f));
                        Line(fx,head,dir*.5f,(left?Teal:Magenta)*(2.4f*fade),.03f,.6f,0,40800+k);fx.Glow(head,(left?Teal:Magenta)*(1.2f*fade),0,.12f,1,0,40810+k);}
                }
                if(s>=0&&s<1.2f){Lantern(k,CutAt(k),out _,out _,out var mid);NativeEntranceFx.Sparks(fx,t,CutAt(k),mid,Vector3.UnitY,1,low?4:10,1.6f,.6f,6,.5f,Lamp*2,Lamp,.008f,40820+k*20,q,floor);}
            }
            // The convergence: the echoes folding back into the body in a flash of both colours.
            float a2=t-ConvergeAt-.35f;
            if(a2>=0&&a2<.4f){float u=a2/.4f;fx.Glow(new Vector3(0,1.2f,.1f),(White+Mint)*(2*(1-u)),0,.4f+.8f*u,1,u,40900);for(int i=0;i<(low?8:20);i++){float ang=i*.314f;var d=new Vector3((float)Math.Cos(ang),.3f*(float)Math.Sin(ang*2),(float)Math.Sin(ang));fx.Streak(new Vector3(0,1.2f,.1f)+d*(.3f+1.2f*u),d*4,(i%2==0?Teal:Magenta)*(1.4f*(1-u)),.01f,.05f,.04f,.5f,u,40910+i);}}
        }
        /// <summary>A dagger's trail behind an echo (`lag` s behind, displaced by `offset`; the echoes are the keyed rig's ghosts, so the
        /// captured tip is theirs): its path over the last fifth of a second within the cut it is in (never back into the idle either side,
        /// which the capture switches to at once), its segments laid onto the camera's plane.</summary>
        private void Echo(INativeEntranceFx fx,float t,float lag,int hand,Vector3 offset,Vector3 head,Vector3 tail,float width,int seed,bool low)
        {
            float now=t-lag,w0,w1;
            if(now>=CutAAt&&now<CutAAt+CutLength+.2f){w0=CutAAt;w1=CutAAt+CutLength;}else if(now>=CutCAt&&now<CutCAt+CutLength+.15f){w0=CutCAt;w1=CutCAt+CutLength-.05f;}else return;
            int n=low?6:tips.Length,c=0;var which=hand==0?NativeEntranceAnchor.RightTip:NativeEntranceAnchor.LeftTip;
            for(int i=0;i<n;i++){float s=now-.2f*i/(n-1);if(s<w0)break;tips[c++]=Anchor(which,Math.Min(s,w1-1e-3f))+offset;}
            for(int i=1;i<c;i++){float u=(float)i/c;var a=tips[i-1];var b=tips[i];if((a-b).LengthSquared()<1e-8f)continue;Line(fx,a,a-b,Vector3.Lerp(head,tail,u)*(1-u),width*(1-.6f*u),.2f,u,seed+i);}
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.afterimage.lane",null,"water",.25f,.6f),
            new NativeIntroCue(StopAt,"intro2.afterimage.stop","move.step.stone","wood",.4f),
            new NativeIntroCue(SplitAt,"intro2.afterimage.split","magic.blink-strike","dash",.7f),
            new NativeIntroCue(CutAAt+HitA-.08f,"intro2.afterimage.cut","melee.daggers.swing","daggers",.85f),
            new NativeIntroCue(CutAAt+HitA+LagL-.06f,"intro2.afterimage.echo","melee.daggers.swing","daggers",.6f,1.15f),
            new NativeIntroCue(CutAAt+HitA+LagR-.04f,"intro2.afterimage.echo2","melee.daggers.swing","daggers",.5f,1.3f),
            new NativeIntroCue(CutAAt+HitA+LagL+.75f,"intro2.afterimage.lanterns","break.wood","wood",.6f,1.2f),
            new NativeIntroCue(CutCAt+HitC-.08f,"intro2.afterimage.cut2","melee.daggers.swing","daggers",.85f,.9f),
            new NativeIntroCue(ConvergeAt+.35f,"intro2.afterimage.join","magic.blink-strike","dash",.8f,.8f),
            new NativeIntroCue(CardAt,"intro2.afterimage.name","melee.daggers.hit","daggers",.4f,1.3f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.95f,1,.98f),Mint*1.2f);card.Style=NativeIntroCardStyle.Rise;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
