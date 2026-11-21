using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM's pose channels: what a move, the arrival, a phase change, a stagger or the death sets over the gait. Offsets are in the root's frame (x right, y up, z where it faces) in
    /// metres, angles in degrees (a pitch + nods forward). Limb 0 and 1 are the left and right legs, 2 and 3 the left and right arms; each has an override weight (0: the gait plants it, 1: the channel's target),
    /// its target and its elbow's or knee's pole. The spine takes a bend (shared down its joints), a twist and a roll, and `Rear` straightens it (0 the stoop, 1 upright, through the ceiling). The head goes where the stabiliser
    /// wants plus an offset; `Lock` is how hard it holds on its target (1) or follows the body (0); `Jaw` splits the diffuser, `Ribs` opens the cage; `CurlL/R` closes the hands (1 a claw, 0 the arch it stands on, -1 fingers
    /// straight), `SpreadL/R` fans them. `PalmL/R` turns a hand on a channel about its forearm until its palm faces where the hand is going (0..1, at speed: a slap leads with the
    /// palm) and `CockL/R` bends it back at the wrist (degrees: + the fingers trail behind the palm, - they flick through ahead of it).
    /// </summary>
    public static class HumCh
    {
        public const int BX=0,BY=1,BZ=2,BP=3,BW=4,BR=5,Bend=6,Twist=7,Roll=8,Rear=9,HX=10,HY=11,HZ=12,HP=13,Lock=14,Jaw=15,Ribs=16,CurlL=17,CurlR=18,SpreadL=19,SpreadR=20,Shrug=21,PalmL=22,PalmR=23,CockL=24,CockR=25,LimbBase=26,LimbStride=7;
        public const int W=0,X=1,Y=2,Z=3,PX=4,PY=5,PZ=6;
        public const int Count=LimbBase+4*LimbStride;
        public static int Limb(int limb,int field)=>LimbBase+limb*LimbStride+field;
        public static readonly float[] Neutral=MakeNeutral();
        public static readonly int[] MirrorOf=new int[Count];public static readonly bool[] MirrorFlip=new bool[Count];
        static HumCh()
        {
            for(int c=0;c<Count;c++)MirrorOf[c]=c;
            for(int l=0;l<4;l++){int o=l^1;for(int f=0;f<LimbStride;f++){MirrorOf[Limb(l,f)]=Limb(o,f);MirrorFlip[Limb(l,f)]=f==X||f==PX;}}
            MirrorOf[CurlL]=CurlR;MirrorOf[CurlR]=CurlL;MirrorOf[SpreadL]=SpreadR;MirrorOf[SpreadR]=SpreadL;
            MirrorOf[PalmL]=PalmR;MirrorOf[PalmR]=PalmL;MirrorOf[CockL]=CockR;MirrorOf[CockR]=CockL;
            foreach(int c in new[]{BX,BW,BR,Twist,Roll,HX})MirrorFlip[c]=true;
        }
        private static float[] MakeNeutral()
        {
            var n=new float[Count];n[Lock]=1;
            for(int l=0;l<4;l++){var p=DefaultPole(l);n[Limb(l,PX)]=p.X;n[Limb(l,PY)]=p.Y;n[Limb(l,PZ)]=p.Z;var h=NativeHumBossLook.Bind[l<2?(l==0?NativeHumBossLook.FootL:NativeHumBossLook.FootR):(l==2?NativeHumBossLook.HandL:NativeHumBossLook.HandR)];n[Limb(l,X)]=h.X;n[Limb(l,Y)]=h.Y;n[Limb(l,Z)]=h.Z;}
            return n;
        }
        /// <summary>A limb's pole on the gait, in the root's frame: knees forward, elbows out and back.</summary>
        public static Vector3 DefaultPole(int limb)
        {
            float s=limb%2==0?-1:1;return limb<2?new Vector3(s*.03f,0,1):Vector3.Normalize(new Vector3(s*.85f,.35f,-.4f));
        }
    }

    /// <summary>One authored pose as channel values.</summary>
    public sealed class HumSnap
    {
        public readonly float[] V=(float[])HumCh.Neutral.Clone();
        public HumSnap Set(int c,float v){V[c]=v;return this;}
        /// <summary>The body's move (offset in the root's frame, pitch, yaw, roll).</summary>
        public HumSnap Lean(float x,float y,float z,float pitch=0,float yaw=0,float roll=0){V[HumCh.BX]=x;V[HumCh.BY]=y;V[HumCh.BZ]=z;V[HumCh.BP]=pitch;V[HumCh.BW]=yaw;V[HumCh.BR]=roll;return this;}
        /// <summary>The spine's bend (a pitch shared down it), twist and roll.</summary>
        public HumSnap Spine(float bend,float twist=0,float roll=0){V[HumCh.Bend]=bend;V[HumCh.Twist]=twist;V[HumCh.Roll]=roll;return this;}
        /// <summary>A limb reaching for (x, y, z) (root frame), bending toward the pole.</summary>
        public HumSnap Reach(int limb,float x,float y,float z,float px,float py,float pz,float weight=1)
        {int b=HumCh.Limb(limb,0);var p=Vector3.Normalize(new Vector3(px,py,pz));V[b]=weight;V[b+1]=x;V[b+2]=y;V[b+3]=z;V[b+4]=p.X;V[b+5]=p.Y;V[b+6]=p.Z;return this;}
        /// <summary>A limb reaching with its default pole.</summary>
        public HumSnap Reach(int limb,float x,float y,float z,float weight=1){var p=HumCh.DefaultPole(limb);return Reach(limb,x,y,z,p.X,p.Y,p.Z,weight);}
        /// <summary>The head's offset from where the stabiliser wants it (root frame), its pitch, its lock.</summary>
        public HumSnap Head(float x,float y,float z,float pitch=0,float lockOn=1){V[HumCh.HX]=x;V[HumCh.HY]=y;V[HumCh.HZ]=z;V[HumCh.HP]=pitch;V[HumCh.Lock]=lockOn;return this;}
        public HumSnap Hands(float curlL,float curlR,float spreadL=0,float spreadR=0){V[HumCh.CurlL]=curlL;V[HumCh.CurlR]=curlR;V[HumCh.SpreadL]=spreadL;V[HumCh.SpreadR]=spreadR;return this;}
        public HumSnap Jaw(float open){V[HumCh.Jaw]=open;return this;}
        public HumSnap Ribs(float open){V[HumCh.Ribs]=open;return this;}
        public HumSnap Rear(float up){V[HumCh.Rear]=up;return this;}
        public HumSnap Shrug(float up){V[HumCh.Shrug]=up;return this;}
        /// <summary>An arm's hand turned to lead its travel with the palm (0..1) and bent back at the wrist (degrees; see <see cref="HumCh"/>).</summary>
        public HumSnap Wrist(int arm,float palm,float cock=0){bool left=arm==2;V[left?HumCh.PalmL:HumCh.PalmR]=palm;V[left?HumCh.CockL:HumCh.CockR]=cock;return this;}
    }

    /// <summary>
    /// The moves as phrases on the shared action timeline (doc section 6): one <see cref="BossActionDef"/> per move with its key poses, each phrase's phases summing to the move table's timings and its first contact phase starting at the
    /// move's wind (the authority's `hitAt`), so what is telegraphed is what hits. Key k of a phrase is the pose its progress reaches at k (0 the neutral; the last the neutral again). Sided moves are authored to its right and mirrored
    /// toward their target. Also: the phase changes, the staggers, the clipped hold and the poses the intro and the death blend through.
    /// </summary>
    public static class HumBossMoves
    {
        public static readonly BossActionDef[] Defs=new BossActionDef[NativeHumBoss.Moves.Length];
        public static readonly HumSnap[][] Keys=new HumSnap[NativeHumBoss.Moves.Length][];
        public static readonly int[] Side=new int[NativeHumBoss.Moves.Length];
        public static BossActionDef Scream,RearUp,Stagger,Choke,Flinch;public static HumSnap[] ScreamKeys,RearKeys,StaggerKeys,ChokeKeys,FlinchKeys;
        /// <summary>
        /// The turn over, animated (the owner, 2026-11-07: "when they go upside down, introduce an animation for this (Currently it just rotates them, animate actually flipping upside down and when they
        /// flip back to normal)"). Up or down, the same flip in the body's own frame: it crouches, arms swung back; pushes off, arms thrown out toward the surface it's going to; tucks as it turns over,
        /// knees to its chest; plants its hands and then its feet on the new floor (the ceiling, or the carpet coming down), sinking to take the weight; and straightens. Keyed on the turn's own clock
        /// (<see cref="TurnKey"/>); the rotation itself waits through the crouch (<see cref="HumBossView.TurnStart"/>).
        /// </summary>
        public static readonly HumSnap[] TurnKeys=MakeTurn();
        /// <summary>The turn's key at `u` (0..1 of <see cref="NativeHumBossMotion.TurnOver"/>): crouch at .2, push at .36, tuck at .56, plant at .8; each stretch eased.</summary>
        private static readonly float[] TurnAt={0,.2f,.36f,.56f,.8f,1};
        public static float TurnKey(float u)
        {
            var at=TurnAt;u=BossMath.Clamp01(u);for(int k=0;k<at.Length-1;k++)if(u<=at[k+1])return k+BossMath.Smooth((u-at[k])/(at[k+1]-at[k]));return at.Length-1;
        }
        private static HumSnap[] MakeTurn()=>new[]{
            N(),
            N().Lean(0,-.75f,-.12f,14,0,0).Spine(20).Reach(ArmL,-1.15f,1.05f,-1.35f,-.5f,.3f,-1).Reach(ArmR,1.15f,1.05f,-1.35f,.5f,.3f,-1).Hands(.3f,.3f).Head(0,-.45f,.25f,12).Shrug(.12f),
            N().Lean(0,.6f,.15f,-12,0,0).Spine(-10).Reach(ArmL,-.6f,4.5f,.45f,-1,0,-.3f).Reach(ArmR,.6f,4.5f,.45f,1,0,-.3f).Reach(LegL,-.32f,.55f,-.25f,-.1f,0,1).Reach(LegR,.32f,.55f,-.25f,.1f,0,1).Hands(-.6f,-.6f,.8f,.8f).Head(0,.15f,.1f,-14),
            N().Lean(0,.45f,0,32,0,0).Spine(44).Reach(ArmL,-.75f,2.05f,1.15f,-1,.2f,.3f).Reach(ArmR,.75f,2.05f,1.15f,1,.2f,.3f).Reach(LegL,-.45f,1.55f,.75f,-.1f,0,1).Reach(LegR,.45f,1.55f,.75f,.1f,0,1).Hands(.8f,.8f).Head(0,-.35f,.3f,22),
            N().Lean(0,-.6f,.12f,12,0,0).Spine(16).Reach(ArmL,-1.25f,.22f,1.15f,-.8f,.6f,.2f,.85f).Reach(ArmR,1.25f,.22f,1.25f,.8f,.6f,.2f,.85f).Hands(-.8f,-.8f,.6f,.6f).Head(0,-.3f,.4f,8).Jaw(.3f),
            N()};
        /// <summary>The intro's and the death's poses, and the clipped hold.</summary>
        public static readonly HumSnap Stand=new HumSnap(),Folded=MakeFolded(),Upright=MakeUpright(),Clipped=MakeClipped(),Lunging=MakeLunging();
        private const int LegL=0,LegR=1,ArmL=2,ArmR=3;
        private const BossPhaseKind A=BossPhaseKind.Anticipation,S=BossPhaseKind.Strike,C=BossPhaseKind.Contact,F=BossPhaseKind.FollowThrough,R=BossPhaseKind.Recover;
        private static BossPhase P(BossPhaseKind k,float s,float to,BossEase e,float shape=0)=>new BossPhase(k,s,to,e,shape);
        private static HumSnap N()=>new HumSnap();
        private static void Def(string id,int side,BossActionDef def,params HumSnap[] keys){int i=NativeHumBoss.MoveIndex(id);Defs[i]=def;Keys[i]=keys;Side[i]=side;}
        static HumBossMoves()
        {
            // ---- Reach, thrown with the whole body (the owner, 2026-11-07: "make it look like they're actually using their body for momentum"). A small gather, then it coils: weight back over its right side and low, the shoulders
            // wound far round to the right, the arm cocked behind it. The throw goes up the chain: the hips unwind first while the chest still faces back, then the chest, then the arm, which whips across 130 degrees at chest
            // height, fingers straight; the arm's weight drags the whole body on round after it, lunging forward and down to the left, and it has to settle back off that. The head never leaves the target ----
            // Faster, and at them (the owner, again: "turn their arm and actually swipe at them, make it a little faster"): the arm is cocked high behind it with the elbow up, then turns over as it comes
            // (the elbow rolling from up and back to down and through, the palm turning to face the stroke) and the hand is driven out through the space in front of it where the target stands, the body
            // lunging after it, before the swing carries it across and down.
            // A slap (the owner: "we need to turn their hand to, think like they're smacking someone"): the wrist lays back as the arm winds; as the stroke starts the hand turns over
            // so it leads with its palm wherever it goes, the fingers drawn together, flat at the hit, and they flick through after it as the swing carries across.
            var m=NativeHumBoss.Move("reach");
            Def("reach",1,new BossActionDef("reach",.12f,.34f,P(A,.14f,1,BossEase.InOut),P(A,m.Wind-.24f,2,BossEase.InOut),P(S,.1f,3,BossEase.In,2.8f),P(C,m.Strike,4,BossEase.InOut),P(F,m.Follow,5,BossEase.OutBack,1.6f),P(R,m.Recover,6,BossEase.Settle,1.3f)),
                N(),
                N().Lean(.08f,.05f,-.12f,-2,10,-2).Spine(-2,12,-2).Reach(ArmR,1.9f,2.45f,-.2f,.6f,1,-.6f).Hands(0,-.4f,0,.6f).Head(0,0,0).Shrug(.08f),
                N().Lean(.4f,-.42f,-.5f,-5,34,-8).Spine(5,40,-10).Reach(ArmR,2.35f,3.05f,-1.95f,.45f,1,-.85f).Hands(0,-.9f,0,.6f).Wrist(ArmR,0,40).Head(0,-.12f,0).Jaw(.35f).Shrug(.18f),
                N().Lean(.1f,-.4f,.35f,7,4,-1).Spine(11,18,-4).Reach(ArmR,2.75f,2.0f,2.55f,.75f,.35f,.35f).Hands(0,-1,0,.3f).Wrist(ArmR,1,6).Head(0,-.16f,.08f).Jaw(.6f).Shrug(.1f),
                N().Lean(-.22f,-.42f,.95f,14,-22,9).Spine(18,-28,8).Reach(ArmR,-1.6f,1.4f,3.9f,-.25f,-.85f,.45f).Hands(0,-1,0,.3f).Wrist(ArmR,1,-16).Head(0,-.22f,.18f).Jaw(.9f),
                N().Lean(-.38f,-.48f,.72f,14,-34,11).Spine(20,-40,11).Reach(ArmR,-3.0f,.95f,1.1f,-.55f,-.75f,.1f).Hands(0,-.5f,0,.6f).Wrist(ArmR,.8f,-30).Head(0,-.2f,.1f,0,.85f).Jaw(.4f),
                N());
            // ---- Stoop: it gathers itself up (the back presses the ceiling, both arms rising wide, the head dropping low and forward, in reach, the fingers fanned), then folds: both hands and the head down at its own feet.
            // The head comes down to 1.2 m at the hit. The head's weak point is open through the wind-up and the recovery ----
            m=NativeHumBoss.Move("stoop");
            Def("stoop",0,new BossActionDef("stoop",.14f,.35f,P(A,m.Wind-.12f,1,BossEase.InOut),P(S,.12f,2,BossEase.In,2.8f),P(C,m.Strike,3,BossEase.Out),P(F,m.Follow,4,BossEase.InOut),P(R,m.Recover,5,BossEase.Settle,1.5f)),
                N(),
                N().Lean(0,.04f,-.18f,-4).Spine(-4).Reach(ArmL,-2.2f,2.9f,1.4f,-.9f,.7f,-.5f).Reach(ArmR,2.2f,2.9f,1.4f,.9f,.7f,-.5f).Hands(-.6f,-.6f,1,1).Head(0,-.55f,-.15f,8).Jaw(.35f),
                N().Lean(0,-.42f,.28f,10).Spine(26).Reach(ArmL,-.72f,.52f,2.0f,-.9f,.5f,-.3f).Reach(ArmR,.72f,.52f,2.0f,.9f,.5f,-.3f).Hands(.2f,.2f,.4f,.4f).Head(0,-1.55f,.18f,14).Jaw(1),
                N().Lean(0,-.62f,.34f,13).Spine(32).Reach(ArmL,-.82f,.42f,1.92f,-.9f,.5f,-.3f).Reach(ArmR,.82f,.42f,1.92f,.9f,.5f,-.3f).Hands(0,0,.2f,.2f).Head(0,-1.9f,.2f,16).Jaw(.8f),
                N().Lean(0,-.3f,.12f,6).Spine(16).Reach(ArmL,-.95f,.4f,1.2f,.5f).Reach(ArmR,.95f,.4f,1.2f,.5f).Head(0,-.8f,.1f,8).Jaw(.3f),
                N());
            // ---- Row Burst: it stands still and listens: chest up, arms lifted a little from the floor and spread, the head tipped back toward the ceiling grid while the row's lamps brighten; at the burst the hands snap shut and the whole
            // body flinches down with the bang, as if the lamps were part of it ----
            m=NativeHumBoss.Move("row");
            Def("row",0,new BossActionDef("row",.2f,.35f,P(A,m.Wind-.1f,1,BossEase.InOut),P(S,.1f,2,BossEase.In,3),P(C,m.Strike,3,BossEase.Out),P(F,m.Follow,4,BossEase.InOut),P(R,m.Recover,5,BossEase.Settle,1.6f)),
                N(),
                N().Lean(0,.1f,-.12f,-5).Spine(-8).Reach(ArmL,-1.9f,1.05f,1.1f,.8f).Reach(ArmR,1.9f,1.05f,1.1f,.8f).Hands(-.4f,-.4f,1,1).Head(0,.28f,-.1f,-24).Jaw(.2f).Shrug(.08f),
                N().Lean(0,-.16f,.05f,5).Spine(8).Reach(ArmL,-1.75f,.72f,1.2f,.8f).Reach(ArmR,1.75f,.72f,1.2f,.8f).Hands(.9f,.9f,0,0).Head(0,-.12f,.05f,5).Jaw(.6f),
                N().Lean(0,-.22f,.08f,7).Spine(11).Reach(ArmL,-1.65f,.62f,1.2f,.6f).Reach(ArmR,1.65f,.62f,1.2f,.6f).Hands(.8f,.8f,0,0).Head(0,-.16f,.05f,6).Jaw(.35f),
                N().Lean(0,-.06f,.02f,2).Spine(3).Head(0,-.05f,0,1),
                N());
            // ---- Noclip: it sinks down onto its left hand and leans into the lane, the right arm pressing forward along it as if pushing into a wall, the jaw opening; at the hit it is thrown down the lane, the body flat as a thrown thing, arms laid back along
            // its flanks, legs trailing; the brake drags its hands and throws its head on ahead of it; it climbs back up ----
            m=NativeHumBoss.Move("lunge");
            Def("lunge",0,new BossActionDef("lunge",.2f,.45f,P(A,m.Wind-.15f,1,BossEase.InOut),P(S,.15f,2,BossEase.In,2.2f),P(C,m.Strike,3,BossEase.InOut),P(F,m.Follow,4,BossEase.Out,2),P(R,m.Recover,5,BossEase.Settle,1.4f)),
                N(),
                N().Lean(0,-.72f,-.2f,15,0,3).Spine(20).Reach(ArmR,.72f,1.55f,3.1f,.9f,.3f,-.5f).Hands(0,-1,0,.8f).Head(0,-.85f,.4f,6).Jaw(.55f),
                MakeLunging(),
                MakeLunging().Set(HumCh.BY,-.62f),
                N().Lean(0,-.5f,-.3f,16,0,4).Spine(20).Reach(ArmL,-1.4f,.42f,2.2f).Reach(ArmR,1.4f,.42f,2.4f).Hands(0,0,.3f,.3f).Head(0,-.7f,.5f,6).Jaw(.3f),
                N());
            // ---- Fall Through: its body hardly moves: the head tips to the pilot's feet and the fingers of one hand drum; the carpet does the rest ----
            m=NativeHumBoss.Move("tear");
            Def("tear",1,new BossActionDef("tear",.3f,.4f,P(A,m.Wind,1,BossEase.InOut),P(C,m.Strike,2,BossEase.Out)),
                N(),
                N().Lean(0,-.06f,0,3).Spine(6,0,4).Head(0,-.28f,.1f,18).Hands(.05f,-.1f,0,.5f),
                N());
            // ---- Peripheral Shift: nothing moves, except one twitch of the head, as the lights dip ----
            m=NativeHumBoss.Move("shift");
            Def("shift",0,new BossActionDef("shift",.05f,.2f,P(R,.45f,1,BossEase.Settle,1.2f)),N(),N());
            // ---- Max Hum: it rears a little, the arms open wide off the floor, the cage of ribs opening on the Ballast, the head back and the jaw splitting while everything brightens; at the hit it throws its chest out, the ring leaves from the
            // Ballast, and it shakes with it for the length of the strike ----
            m=NativeHumBoss.Move("pulse");
            Def("pulse",0,new BossActionDef("pulse",.25f,.45f,P(A,m.Wind-.15f,1,BossEase.InOut),P(S,.15f,2,BossEase.In,3),P(C,m.Strike,3,BossEase.InOut),P(F,m.Follow,4,BossEase.Out),P(R,m.Recover,5,BossEase.Settle,1.5f)),
                N(),
                N().Lean(0,.14f,-.28f,-9).Spine(-16).Rear(.1f).Reach(ArmL,-3.1f,2.45f,.3f,-.6f,1,-.4f).Reach(ArmR,3.1f,2.45f,.3f,.6f,1,-.4f).Hands(-.8f,-.8f,1,1).Head(0,.28f,-.15f,-20).Jaw(.55f).Ribs(.85f),
                N().Lean(0,.18f,-.1f,-5).Spine(-10).Rear(.1f).Reach(ArmL,-3.3f,2.3f,.4f,-.6f,1,-.4f).Reach(ArmR,3.3f,2.3f,.4f,.6f,1,-.4f).Hands(-1,-1,1,1).Head(0,.2f,0,-14).Jaw(1).Ribs(1),
                N().Lean(0,.18f,-.1f,-5).Spine(-10).Rear(.1f).Reach(ArmL,-3.3f,2.3f,.4f,-.6f,1,-.4f).Reach(ArmR,3.3f,2.3f,.4f,.6f,1,-.4f).Hands(-1,-1,1,1).Head(0,.2f,0,-14).Jaw(1).Ribs(1),
                N().Lean(0,-.1f,.04f,3).Spine(6).Reach(ArmL,-1.6f,.7f,.9f,.5f).Reach(ArmR,1.6f,.7f,.9f,.5f).Head(0,-.12f,0,3).Jaw(.3f).Ribs(.3f),
                N());
            // ---- Lights Out: the dark is the wind-up: it goes rigid where it stands (it isn't where you left it when the tubes come back): a crouch, its arm cocked. The strike is a backhand sweep behind the target as the tubes slam on ----
            m=NativeHumBoss.Move("cut");
            Def("cut",-1,new BossActionDef("cut",.1f,.3f,P(A,m.Wind-.1f,1,BossEase.InOut),P(S,.1f,2,BossEase.In,2.6f),P(C,m.Strike,3,BossEase.Linear),P(F,m.Follow,4,BossEase.OutBack,1.4f),P(R,m.Recover,5,BossEase.Settle,1.4f)),
                N(),
                N().Lean(0,-.3f,0,8,-10,-3).Spine(12,-12).Reach(ArmL,-2.6f,2.6f,-.55f,-.6f,1,-.5f).Hands(-.8f,0,1,0).Head(0,-.3f,.1f,4),
                N().Lean(0,-.3f,.2f,9,-4,1).Spine(14,-4).Reach(ArmL,-3.0f,1.5f,1.7f,-.3f,1,-.5f).Hands(-1,0,1,0).Head(0,-.25f,.1f,4).Jaw(.5f),
                N().Lean(0,-.3f,.24f,9,22,4).Spine(14,22).Reach(ArmL,2.5f,1.3f,2.5f,.1f,1,-.4f).Hands(-1,0,.8f,0).Head(0,-.25f,.1f,4).Jaw(.8f),
                N().Lean(0,-.2f,.1f,6,26,3).Spine(10,26).Reach(ArmL,3.0f,1.6f,1.2f,.2f,1,-.4f).Hands(-.4f,0,.5f,0).Head(0,-.15f,.05f,2),
                N());
            // ---- Wall Hand: its own right arm plunges into the floor beside it (the hand that comes out of the wall is this one, elsewhere): the shoulder sinks, the head stays on the victim, it drags the arm back at the end ----
            m=NativeHumBoss.Move("hand");
            Def("hand",1,new BossActionDef("hand",.15f,.35f,P(A,m.Wind-.1f,1,BossEase.InOut),P(S,.1f,2,BossEase.In,2.4f),P(C,m.Strike,3,BossEase.Out),P(F,m.Follow,4,BossEase.InOut),P(R,m.Recover,5,BossEase.Settle,1.4f)),
                N(),
                N().Lean(.1f,-.2f,.05f,5,6,6).Spine(9,6,7).Reach(ArmR,1.5f,1.15f,1.35f,.85f,.8f,-.3f).Hands(0,-.6f,0,1).Head(0,-.1f,0,3),
                N().Lean(.2f,-.36f,.1f,8,8,10).Spine(14,8,11).Reach(ArmR,1.1f,-.45f,1.35f,.85f,.8f,-.3f).Hands(0,-1,0,1).Head(0,-.15f,0,3),
                N().Lean(.22f,-.38f,.1f,8,8,11).Spine(14,8,12).Reach(ArmR,1.1f,-.6f,1.35f,.85f,.8f,-.3f).Hands(0,-1,0,1).Head(0,-.15f,0,3),
                N().Lean(.2f,-.34f,.08f,7,6,10).Spine(12,6,11).Reach(ArmR,1.1f,-.55f,1.3f,.85f,.8f,-.3f).Hands(0,-1,0,1).Head(0,-.13f,0,3),
                N());
            // ---- Familiar Voices: its jaw hangs, it stands very still with its hands against the air, listening to what it is doing in the walls ----
            m=NativeHumBoss.Move("voices");
            Def("voices",0,new BossActionDef("voices",.3f,.5f,P(A,m.Wind,1,BossEase.InOut),P(C,m.Strike,1,BossEase.Hold),P(R,m.Recover,2,BossEase.Settle,1.6f)),
                N(),
                N().Lean(0,.06f,-.12f,-3).Spine(-6,0,-3).Reach(ArmL,-1.75f,1.3f,1.3f,.8f).Reach(ArmR,1.75f,1.3f,1.3f,.8f).Hands(-.4f,-.4f,1,1).Head(0,.05f,-.05f,-6).Jaw(.75f),
                N());
            for(int i=0;i<Defs.Length;i++)
            {
                var mv=NativeHumBoss.Moves[i];if(Defs[i]==null)throw new InvalidOperationException("no phrase for "+mv.Id);
                if(mv.Id!="shift"&&(Math.Abs(Defs[i].Seconds-mv.Duration)>1e-3f||Math.Abs(Defs[i].HitAt-mv.Wind)>1e-3f))throw new InvalidOperationException(mv.Id+" phrase timing differs from the move table");
                if(Keys[i].Length!=(int)Math.Round(Defs[i].End)+1)throw new InvalidOperationException(mv.Id+" keys");
            }
            // ---- BLACKOUT's change (3.5 s): the hum climbs to a scream: it rears against the ceiling, arms wide, jaw wide, shaking; it stops dead; the rows die toward the middle; it sags ----
            Scream=new BossActionDef("scream",.3f,.5f,P(A,1.2f,1,BossEase.InOut),P(A,.9f,2,BossEase.InOut),P(S,.15f,3,BossEase.In,2.4f),P(C,.25f,4,BossEase.Out),P(F,.5f,5,BossEase.InOut),P(R,.5f,6,BossEase.Settle,1.6f));
            ScreamKeys=new[]{N(),
                N().Lean(0,.1f,-.3f,-10).Spine(-14).Reach(ArmL,-2.6f,2.2f,.6f,-.7f,1,-.4f).Reach(ArmR,2.6f,2.2f,.6f,.7f,1,-.4f).Hands(-.6f,-.6f,1,1).Head(0,.22f,-.1f,-24).Jaw(.7f).Ribs(.5f),
                N().Lean(0,.2f,-.34f,-13).Spine(-20).Reach(ArmL,-3.2f,2.6f,.4f,-.6f,1,-.4f).Reach(ArmR,3.2f,2.6f,.4f,.6f,1,-.4f).Hands(-1,-1,1,1).Head(0,.32f,-.15f,-34).Jaw(1).Ribs(1),
                N().Lean(0,-.2f,.1f,12).Spine(20).Reach(ArmL,-1.8f,.5f,1.6f).Reach(ArmR,1.8f,.5f,1.6f).Hands(.5f,.5f,0,0).Head(0,-.4f,.1f,14).Jaw(.3f),
                N().Lean(0,-.3f,.12f,14).Spine(24).Reach(ArmL,-1.6f,.42f,1.5f).Reach(ArmR,1.6f,.42f,1.5f).Hands(0,0,0,0).Head(0,-.55f,.1f,16).Jaw(.2f),
                N().Lean(0,-.16f,.06f,7).Spine(12).Head(0,-.3f,.05f,8).Jaw(.1f),
                N()};
            // ---- NOCLIP's change (3.5 s): it rears right up, head and shoulders through the ceiling, arms up after them, tiles raining round its waist; it comes back down looking at you ----
            RearUp=new BossActionDef("rear",.35f,.5f,P(A,.9f,1,BossEase.InOut),P(S,.6f,2,BossEase.In,1.6f),P(C,.9f,3,BossEase.Linear),P(F,.5f,4,BossEase.InOut),P(R,.6f,5,BossEase.Settle,1.6f));
            RearKeys=new[]{N(),
                N().Lean(0,-.34f,.1f,8).Spine(16).Reach(ArmL,-1.3f,.42f,1.4f).Reach(ArmR,1.3f,.42f,1.4f).Head(0,-.5f,.1f,8).Jaw(.3f),
                MakeUpright(),
                MakeUpright().Set(HumCh.BY,.28f),
                N().Lean(0,-.1f,.12f,6).Spine(12).Reach(ArmL,-1.4f,.46f,1.2f,.6f).Reach(ArmR,1.4f,.46f,1.2f,.6f).Head(0,-.3f,.1f,6).Jaw(.4f),
                N()};
            // ---- The stagger (the choke of three tubes in a charge, a clipped stumble): knees and back giving, one hand folding under, the head alone holding still ----
            Stagger=new BossActionDef("stagger",.06f,.5f,P(S,.14f,1,BossEase.Out,2),P(F,1,1.15f,BossEase.InOut),P(R,1,2,BossEase.Settle,1.2f));
            StaggerKeys=new[]{N(),N().Lean(.12f,-.5f,.12f,13,0,-9).Spine(24,0,-10).Reach(ArmL,-.75f,.62f,1.15f).Hands(.8f,.1f,0,0).Head(0,-.55f,.08f,8,.6f).Jaw(.5f).Ribs(.4f),N()};
            Choke=new BossActionDef("choke",.05f,.5f,P(S,.1f,1,BossEase.Out,2),P(F,.8f,1.1f,BossEase.InOut),P(R,.9f,2,BossEase.Settle,1.3f));
            ChokeKeys=new[]{N(),N().Lean(0,-.62f,.2f,18).Spine(30).Reach(ArmL,-1.1f,.42f,1.2f).Reach(ArmR,1.1f,.42f,1.2f).Hands(.5f,.5f,0,0).Head(0,-1.1f,.1f,12,.5f).Jaw(.9f).Ribs(.9f),N()};
            Flinch=new BossActionDef("flinch",.05f,.3f,P(S,.1f,1,BossEase.Out),P(R,.6f,2,BossEase.Settle,1.4f));
            FlinchKeys=new[]{N(),N().Lean(0,-.1f,-.14f,5,0,3).Spine(10,0,-3).Head(0,-.08f,0,2,1),N()};
        }
        // The lunge's flat throw: the body nearly level, arms laid back, legs trailing, the head far out ahead on its neck.
        private static HumSnap MakeLunging()=>new HumSnap().Lean(0,-.9f,-.2f,58,0,0).Spine(14)
            .Reach(ArmL,-.62f,.92f,-.9f,-.7f,.6f,.3f).Reach(ArmR,.62f,.92f,-.9f,.7f,.6f,.3f).Reach(LegL,-.34f,.62f,-2.75f,-.1f,.1f,-1).Reach(LegR,.34f,.72f,-2.6f,.1f,.1f,-1).Hands(-1,-1,.2f,.2f).Head(0,-.65f,.85f,0).Jaw(.9f);
        private static HumSnap MakeUpright()=>new HumSnap().Lean(0,.22f,.42f,-22).Spine(-34).Rear(1).Reach(HumArm(0),-1.2f,4.1f,.75f,-.8f,.2f,-.7f).Reach(HumArm(1),1.2f,4.3f,.75f,.8f,.2f,-.7f).Hands(-.8f,-.8f,1,1).Head(0,2.3f,-.65f,6).Jaw(.9f).Ribs(.7f);
        private static int HumArm(int i)=>2+i;
        // The death's fold: down onto its knuckles, the arms given way, the back sagging, the head hanging low and forward, buzzing.
        private static HumSnap MakeFolded()=>new HumSnap().Lean(0,-1.28f,.42f,22,0,4).Spine(38,3,6)
            .Reach(ArmL,-1.05f,.2f,1.1f,-.9f,.85f,-.25f).Reach(ArmR,1.0f,.2f,1.25f,.9f,.85f,-.25f).Reach(LegL,-.42f,.42f,-.78f,-.2f,0,1).Reach(LegR,.42f,.42f,-.82f,.2f,0,1).Hands(1,1,0,0).Head(.1f,-2.25f,.45f,8,0).Jaw(.25f).Ribs(.3f);
        // Half inside a surface: frozen in mid-lean against it, its hands flat ahead of it; the head alone still tracks.
        private static HumSnap MakeClipped()=>new HumSnap().Lean(0,-.55f,.3f,24,0,5).Spine(18,6,5).Reach(ArmL,-1.25f,1.0f,2.2f).Reach(ArmR,1.0f,1.2f,2.5f).Hands(-.2f,-.3f,.7f,.7f).Head(0,-.5f,.25f,0).Jaw(.6f);
        /// <summary>A phrase's channels at a progress (keys piecewise linear, extended past both ends so an overshooting ease carries through), mirrored when asked.</summary>
        public static void Sample(HumSnap[] keys,float progress,bool mirror,float[] into)
        {
            int n=keys.Length;int k=(int)Math.Floor(progress);if(k<0)k=0;if(k>n-2)k=n-2;float f=progress-k;var a=keys[k].V;var b=keys[k+1].V;
            for(int c=0;c<HumCh.Count;c++)into[c]=a[c]+(b[c]-a[c])*f;
            for(int l=0;l<4;l++){int w=HumCh.Limb(l,HumCh.W);into[w]=BossMath.Clamp01(into[w]);}
            if(mirror)Mirror(into);
        }
        private static readonly float[] mirrorScratch=new float[HumCh.Count];
        public static void Mirror(float[] v)
        {
            Array.Copy(v,mirrorScratch,HumCh.Count);
            for(int c=0;c<HumCh.Count;c++){int m=HumCh.MirrorOf[c];float x=mirrorScratch[m];v[c]=HumCh.MirrorFlip[c]?-x:x;}
        }
        public static HumSnap[] KeysOf(BossActionDef d){for(int i=0;i<Defs.Length;i++)if(ReferenceEquals(Defs[i],d))return Keys[i];return null;}
    }
}
