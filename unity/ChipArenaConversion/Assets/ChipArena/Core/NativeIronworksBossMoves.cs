using System;
using System.Numerics;
using M=ChipArena.UnityConversion.NativeIronworksBossModel;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE CONVERTER's pose channels (doc section 8): what an action, a moment, the arrival or the death sets over the gait. The
    /// body's offset is in the root's frame (x right, y up, z forward; m) with pitch (nose up), yaw and roll (right side up) in
    /// degrees; the vessel's tilt target on its trunnions (degrees, + pours forward); per leg an override weight (0 the gait plants
    /// it, 1 the channel's target), its target in the root frame (y the lift over the floor), the foot's pitch and a plant flag (1:
    /// the target is a landing, and the gait takes the foot over there); the front tongs' opening; the foreman's lever, lean, the
    /// valve wheel's spin (turns a second) and his brace; the glows (the mouth's heat, the tuyères' blow, the crust's cracks, the
    /// seals' steam, the lamps), the chains' lift in an updraft, the stance's width and length (the gait steps the feet out to
    /// them), the foreman's look weight.
    /// </summary>
    public static class IronworksBossCh
    {
        public const int BX=0,BY=1,BZ=2,BP=3,BW=4,BR=5,Tilt=6,LegBase=7,LegStride=6;
        public const int W=0,X=1,Y=2,Z=3,FP=4,PL=5;
        public static int Leg(int leg,int field)=>LegBase+leg*LegStride+field;
        public const int OpenL=31,OpenR=32,Lever=33,Lean=34,Valve=35,Brace=36,Heat=37,Blow=38,Crack=39,Steam=40,Lamp=41,ChainLift=42,StanceX=43,StanceZ=44,Look=45,Count=46;
        /// <summary>The standing pose: the gait plants every foot, the vessel tipped 10 degrees at rest, the tongs closed (2 degrees),
        /// the mouth's heat at its idle level, the tuyères ticking over, the lamps on, the foreman looking.</summary>
        public static readonly float[] Neutral=MakeNeutral();
        private static float[] MakeNeutral()
        {
            var n=new float[Count];n[Tilt]=10;n[OpenL]=n[OpenR]=2;n[Heat]=.85f;n[Blow]=.15f;n[Lamp]=1;n[Look]=1;n[StanceX]=1;n[StanceZ]=1;
            for(int leg=0;leg<4;leg++){var f=Stance(leg);n[Leg(leg,X)]=f.X;n[Leg(leg,Z)]=f.Z;}
            return n;
        }
        /// <summary>Leg `leg`'s foot on the floor at the stance (root frame): the concept's stance, as wide as the body stands.</summary>
        public static Vector3 Stance(int leg)
        {
            var p=M.Stance();var f=p.Foot(leg);return new Vector3(f.X*M.StanceWide,0,f.Z*M.StanceLong);
        }
        /// <summary>Which leg is a front (tong) leg; its side (-1 left, +1 right).</summary>
        public static bool Front(int leg)=>leg<2;
        public static int Side(int leg)=>leg==M.FL||leg==M.RL?-1:1;
        /// <summary>A leg's mirror (left for right).</summary>
        public static int Mirror(int leg)=>leg^1;
    }

    /// <summary>One authored pose as channel values (copied from the neutral, then set).</summary>
    public sealed class IronworksBossSnap
    {
        public readonly float[] V=(float[])IronworksBossCh.Neutral.Clone();
        public IronworksBossSnap Set(int c,float v){V[c]=v;return this;}
        /// <summary>The body: offset (root frame), pitch nose up, yaw, roll right side up (degrees).</summary>
        public IronworksBossSnap Body(float x,float y,float z,float pitch,float yaw=0,float roll=0){V[IronworksBossCh.BX]=x;V[IronworksBossCh.BY]=y;V[IronworksBossCh.BZ]=z;V[IronworksBossCh.BP]=pitch;V[IronworksBossCh.BW]=yaw;V[IronworksBossCh.BR]=roll;return this;}
        public IronworksBossSnap Tilt(float deg){V[IronworksBossCh.Tilt]=deg;return this;}
        /// <summary>A leg taken by the pose: its foot at (x, y, z) in the root frame, pitched (degrees); `plant` lands it there.</summary>
        public IronworksBossSnap Foot(int leg,float x,float y,float z,float pitch=0,float weight=1,bool plant=false)
        {int b=IronworksBossCh.Leg(leg,0);V[b]=weight;V[b+1]=x;V[b+2]=y;V[b+3]=z;V[b+4]=pitch;V[b+5]=plant?1:0;return this;}
        public IronworksBossSnap Tongs(float left,float right){V[IronworksBossCh.OpenL]=left;V[IronworksBossCh.OpenR]=right;return this;}
        public IronworksBossSnap Foreman(float lever,float lean,float brace=0){V[IronworksBossCh.Lever]=lever;V[IronworksBossCh.Lean]=lean;V[IronworksBossCh.Brace]=brace;return this;}
        public IronworksBossSnap Glow(float heat,float blow){V[IronworksBossCh.Heat]=heat;V[IronworksBossCh.Blow]=blow;return this;}
        public IronworksBossSnap Copy(){var s=new IronworksBossSnap();Array.Copy(V,s.V,V.Length);return s;}
        /// <summary>This pose with its left and right swapped (a sided move authored to one side, played to the other).</summary>
        public IronworksBossSnap Mirrored()
        {
            var s=Copy();var v=V;
            s.V[IronworksBossCh.BX]=-v[IronworksBossCh.BX];s.V[IronworksBossCh.BW]=-v[IronworksBossCh.BW];s.V[IronworksBossCh.BR]=-v[IronworksBossCh.BR];
            for(int leg=0;leg<4;leg++){int o=IronworksBossCh.Mirror(leg);for(int f=0;f<IronworksBossCh.LegStride;f++){float x=v[IronworksBossCh.Leg(o,f)];s.V[IronworksBossCh.Leg(leg,f)]=f==IronworksBossCh.X?-x:x;}}
            s.V[IronworksBossCh.OpenL]=v[IronworksBossCh.OpenR];s.V[IronworksBossCh.OpenR]=v[IronworksBossCh.OpenL];return s;
        }
    }

    /// <summary>
    /// The actions and moments as phrases on the shared action timeline (doc sections 6 and 8): one <see cref="BossActionDef"/> per
    /// table row with its key poses. Each phrase's phases sum to the table's windup, strike, follow-through and recover, and the
    /// first contact phase starts at the windup: the authority's hit (actionAt + windup), so what is telegraphed is what hits; the
    /// timeline stretches the phases before contact to the hit and those after to the length when the authority varies them. Key
    /// k of a phrase is the pose its progress reaches at k (0 the neutral, the last the neutral again). Sided moves (stomp, grab)
    /// are authored to the boss's right (FR) and mirrored for the left.
    /// </summary>
    public static class IronworksBossMoves
    {
        public static readonly BossActionDef[] Defs=new BossActionDef[NativeIronworksBoss.Actions.Length];
        public static readonly IronworksBossSnap[][] Keys=new IronworksBossSnap[NativeIronworksBoss.Actions.Length][];
        public static readonly IronworksBossSnap[][] KeysMirrored=new IronworksBossSnap[NativeIronworksBoss.Actions.Length][];
        /// <summary>The side a move is authored to (+1 right, 0 symmetric).</summary>
        public static readonly int[] Side=new int[NativeIronworksBoss.Actions.Length];
        /// <summary>The arrival's poses: under the pit with the legs folded, the tongs gripping the rim, heaved out, risen; the death's.</summary>
        public static readonly IronworksBossSnap Buried,Gripped,Heaved,Risen,Dead,Kneeling;
        private static BossPhase P(BossPhaseKind k,float s,float to,BossEase e,float shape=0)=>new BossPhase(k,s,to,e,shape);
        private const BossPhaseKind A=BossPhaseKind.Anticipation,S=BossPhaseKind.Strike,C=BossPhaseKind.Contact,F=BossPhaseKind.FollowThrough,R=BossPhaseKind.Recover;
        public static int Index(string id){var a=NativeIronworksBoss.Actions;for(int i=0;i<a.Length;i++)if(a[i].Id==id)return i;return -1;}
        private static void Def(string id,int side,BossActionDef def,params IronworksBossSnap[] keys)
        {
            int i=Index(id);Defs[i]=def;Keys[i]=keys;Side[i]=side;var m=new IronworksBossSnap[keys.Length];for(int k=0;k<keys.Length;k++)m[k]=keys[k].Mirrored();KeysMirrored[i]=m;
        }
        private static IronworksBossSnap N()=>new IronworksBossSnap();
        private static readonly Vector3 FR=IronworksBossCh.Stance(M.FR),FL=IronworksBossCh.Stance(M.FL),RL=IronworksBossCh.Stance(M.RL),RR=IronworksBossCh.Stance(M.RR);

        static IronworksBossMoves()
        {
            // ---- Pour: the lever hauled back, the vessel rocks back 14 degrees (anticipation) and rises, the tongs step wide; it
            // swings to 86 in .45 s, accelerating into the pour (In), the body dipping forward; settles to 80 while the stream sweeps;
            // thins; rocks upright (the trunnion swing carries the overshoot to -6 and the two sways) ----
            var po=NativeIronworksBoss.Find("pour");
            Def("pour",0,new BossActionDef("pour",.2f,.45f,P(A,.35f,1,BossEase.InOut),P(A,po.Windup-.35f,2,BossEase.InOut),P(C,.45f,3,BossEase.In,2.2f),P(S,po.Strike-.45f,4,BossEase.Settle,1.8f),P(F,po.Follow,5,BossEase.InOut),P(R,po.Recover,6,BossEase.Settle,1.5f)),
                N(),
                N().Body(0,.03f,-.05f,1).Tilt(-6).Foreman(28,-6).Glow(1.05f,.15f).Set(IronworksBossCh.StanceX,1.05f),
                N().Body(0,.07f,-.1f,4).Tilt(-14).Foreman(30,-8,.3f).Glow(1.25f,.18f).Set(IronworksBossCh.StanceX,1.08f),
                N().Body(0,-.35f,.25f,-6).Tilt(86).Foreman(-30,12,.5f).Glow(1.45f,.2f).Set(IronworksBossCh.StanceX,1.08f),
                N().Body(0,-.3f,.2f,-5).Tilt(80).Foreman(-28,10,.4f).Glow(1.4f,.2f).Set(IronworksBossCh.StanceX,1.08f),
                N().Body(0,-.18f,.1f,-3).Tilt(58).Foreman(-14,6).Glow(1.15f,.18f).Set(IronworksBossCh.StanceX,1.06f),
                N());
            // ---- Blow: it crouches, the vessel rights itself, the grate flares and inhales, steam from the seals, the valve spun, the
            // chains lifting; the column: it extends .55 m in .25 s (overshooting), the vessel recoiling 8; the rain; a slump; the
            // tuyères venting as it recovers ----
            var bl=NativeIronworksBoss.Find("blow");
            Def("blow",0,new BossActionDef("blow",.25f,.5f,P(A,.5f,1,BossEase.InOut),P(A,bl.Windup-.5f,2,BossEase.InOut),P(C,.25f,3,BossEase.OutBack,2.4f),P(S,1.75f,4,BossEase.Linear),P(S,bl.Strike-2.0f,5,BossEase.InOut),P(F,bl.Follow,6,BossEase.InOut),P(R,bl.Recover,7,BossEase.Settle,1.6f)),
                N(),
                N().Body(0,-.2f,0,1).Tilt(2).Foreman(8,-4).Glow(1.15f,.7f).Set(IronworksBossCh.Valve,1.2f).Set(IronworksBossCh.Steam,.6f).Set(IronworksBossCh.ChainLift,.2f),
                N().Body(0,-.3f,0,2).Tilt(-4).Foreman(10,-6,.3f).Glow(1.3f,1.0f).Set(IronworksBossCh.Valve,2.2f).Set(IronworksBossCh.Steam,1).Set(IronworksBossCh.ChainLift,.5f),
                N().Body(0,.25f,0,5).Tilt(-8).Foreman(10,-10,.6f).Glow(2.0f,1.3f).Set(IronworksBossCh.Valve,.8f).Set(IronworksBossCh.Steam,.5f).Set(IronworksBossCh.ChainLift,1).Set(IronworksBossCh.Look,.3f),
                N().Body(0,.2f,0,4).Tilt(-7).Foreman(10,-9,.6f).Glow(1.9f,1.3f).Set(IronworksBossCh.Valve,.5f).Set(IronworksBossCh.Steam,.4f).Set(IronworksBossCh.ChainLift,1).Set(IronworksBossCh.Look,.3f),
                N().Body(0,.05f,0,2).Tilt(-2).Foreman(6,-4,.3f).Glow(1.5f,.9f).Set(IronworksBossCh.ChainLift,.6f).Set(IronworksBossCh.Look,.6f),
                N().Body(0,-.15f,0,-1).Tilt(4).Foreman(2,4).Glow(1.1f,.6f).Set(IronworksBossCh.Steam,.8f),
                N());
            // ---- Stomp (authored on FR, mirrored for FL; a rear stomp uses the same arc on its bogie): the weight rolls off the
            // leg, the leg arcs up to 2.5 m over its landing and the tongs aim; it accelerates down (In) and plants with a lurch; the
            // body rolls past centre; the leg holds where it landed ----
            var st=NativeIronworksBoss.Find("stomp");var land=new Vector3(FR.X+.05f,0,FR.Z+.6f);
            Def("stomp",1,new BossActionDef("stomp",.14f,.35f,P(A,.25f,1,BossEase.InOut),P(A,st.Windup-.25f,2,BossEase.Out,2),P(C,st.Strike,3,BossEase.In,3),P(F,st.Follow,4,BossEase.Out),P(R,st.Recover,5,BossEase.Settle,1.4f)),
                N(),
                N().Body(-.18f,.02f,0,1,0,-3).Foot(M.FR,FR.X+.15f,.6f,FR.Z+.1f,-10).Tongs(2,6),
                N().Body(-.3f,.05f,.05f,2,0,-6).Foot(M.FR,land.X+.2f,2.5f,land.Z-.15f,-30).Tongs(2,10).Tilt(4),
                N().Body(.08f,-.4f,.12f,-3,0,3).Foot(M.FR,land.X,0,land.Z,0,1,true).Tongs(2,3).Tilt(15).Glow(1.25f,.15f).Foreman(-6,10,.5f),
                N().Body(.04f,-.2f,.06f,-1,0,1.5f).Tilt(12).Glow(1.1f,.15f).Foreman(-2,6,.3f),
                N());
            // ---- Grab (authored on FR): the leg draws back before the reach, the body leaning toward the target; the reach, the
            // tongs open to 44; the snap (the jaws shut, the body jolts back); the lift to 2.8 m, two shakes, the fling to the side;
            // the leg comes back down where it started (the target's point is the rig's: it aims the reach and the hold) ----
            var gr=NativeIronworksBoss.Find("grab");
            Def("grab",1,new BossActionDef("grab",.18f,.4f,P(A,.35f,1,BossEase.InOut),P(A,gr.Windup-.35f,2,BossEase.Out,2),P(C,gr.Strike,3,BossEase.In,3),P(F,.5f,4,BossEase.InOut),P(F,.5f,5,BossEase.Linear),P(F,gr.Follow-1.0f,6,BossEase.OutBack,1.6f),P(R,gr.Recover,7,BossEase.Settle,1.4f)),
                N(),
                N().Body(.1f,.05f,-.15f,2,6,2).Foot(M.FR,FR.X-.3f,1.7f,FR.Z-.9f,20).Tongs(2,14).Foreman(10,-4),
                N().Body(.2f,-.15f,.5f,-5,10,4).Foot(M.FR,1.0f,1.1f,6.3f,-62).Tongs(2,44).Foreman(-6,8,.4f),
                N().Body(.15f,-.05f,.3f,-3,8,3).Foot(M.FR,1.0f,1.0f,6.1f,-55).Tongs(2,5).Foreman(-8,6,.5f),
                N().Body(.05f,.1f,0,3,6,2).Foot(M.FR,2.6f,2.8f,4.6f,-28).Tongs(2,5).Tilt(-6).Foreman(15,-4,.4f),
                N().Body(.05f,.1f,0,3,6,2).Foot(M.FR,2.7f,2.9f,4.5f,-24).Tongs(2,5).Tilt(-4).Foreman(15,-4,.4f),
                N().Body(-.15f,0,0,1,-12,-4).Foot(M.FR,5.4f,2.2f,2.6f,10).Tongs(2,40).Tilt(6).Foreman(4,6,.3f),
                N().Foot(M.FR,FR.X,0,FR.Z,0,1,true));
            // ---- Slam: both front legs climb (FL then FR), the body rears 24 degrees, the weight back, the foreman bracing; the
            // hang; the tongs slam down (In), the body dropping .4 below its stance, pitch -9, the vessel lurching 34; the follow
            // through and a long recover ----
            var sl=NativeIronworksBoss.Find("slam");
            Def("slam",0,new BossActionDef("slam",.2f,.5f,P(A,.5f,1,BossEase.InOut),P(A,.6f,2,BossEase.InOut),P(A,sl.Windup-1.1f,3,BossEase.Hold),P(C,.14f,4,BossEase.In,3.2f),P(S,sl.Strike-.14f,5,BossEase.Out),P(F,sl.Follow,6,BossEase.InOut),P(R,sl.Recover,7,BossEase.Settle,1.5f)),
                N(),
                N().Body(0,.15f,-.25f,10).Foot(M.FL,FL.X+.1f,2.0f,FL.Z+.2f,30).Tongs(18,4).Tilt(2).Foreman(-4,-8,.4f),
                N().Body(0,.45f,-.55f,24).Foot(M.FL,FL.X+.4f,2.5f,FL.Z+.3f,34).Foot(M.FR,FR.X-.4f,2.8f,FR.Z+.45f,34).Tongs(26,28).Tilt(-12).Glow(1.4f,.2f).Foreman(-6,-16,1).Set(IronworksBossCh.Look,.4f),
                N().Body(0,.45f,-.55f,24).Foot(M.FL,FL.X+.4f,2.5f,FL.Z+.3f,34).Foot(M.FR,FR.X-.4f,2.8f,FR.Z+.45f,34).Tongs(26,28).Tilt(-12).Glow(1.4f,.2f).Foreman(-6,-16,1).Set(IronworksBossCh.Look,.4f),
                N().Body(0,-.65f,.25f,-9).Foot(M.FL,FL.X,0,FL.Z+.68f,0,1,true).Foot(M.FR,FR.X,0,FR.Z+.68f,0,1,true).Tongs(6,6).Tilt(34).Glow(1.6f,.2f).Foreman(-10,18,1),
                N().Body(0,-.5f,.18f,-7).Tilt(30).Glow(1.5f,.2f).Foreman(-8,14,.8f),
                N().Body(0,-.2f,.05f,-2).Tilt(-2).Glow(1.2f,.18f).Foreman(-2,4,.4f),
                N());
            // ---- Deslag: the foreman cranks, the vessel rolls back on its trunnions mouth to the rear, the body pitching forward to
            // counterbalance; three recoils as the lumps fly (additive kicks on the hit, +.4, +.8); the hold; it rolls upright and
            // overshoots forward 12 ----
            var de=NativeIronworksBoss.Find("deslag");
            Def("deslag",0,new BossActionDef("deslag",.25f,.5f,P(A,.3f,1,BossEase.InOut),P(A,de.Windup-.3f,2,BossEase.InOut),P(C,de.Strike,3,BossEase.Linear),P(F,de.Follow,4,BossEase.InOut),P(R,.55f,5,BossEase.InOut),P(R,de.Recover-.55f,6,BossEase.Settle,1.4f)),
                N(),
                N().Body(0,0,0,-1).Tilt(4).Foreman(30,-6,.3f),
                N().Body(0,-.08f,.1f,-4).Tilt(-100).Foreman(40,-10,.5f).Glow(1.0f,.15f).Set(IronworksBossCh.Look,.5f),
                N().Body(0,-.1f,.12f,-4.5f).Tilt(-104).Foreman(40,-10,.5f).Glow(.95f,.15f).Set(IronworksBossCh.Look,.5f),
                N().Body(0,-.06f,.08f,-3).Tilt(-96).Foreman(36,-8,.4f).Glow(.95f,.15f),
                N().Body(0,0,0,1).Tilt(12).Foreman(-6,4).Glow(1.0f,.15f),
                N());
            // ---- Call: it raises its front 10 degrees and tips its mouth toward the vessel it calls (the rig yaws it toward vessel A
            // or B); the bellow at the hit (a heat-haze shock); it holds the bellow and settles ----
            var ca=NativeIronworksBoss.Find("call");
            Def("call",0,new BossActionDef("call",.25f,.45f,P(A,ca.Windup,1,BossEase.InOut),P(C,0,1,BossEase.Hold),P(F,.2f,2,BossEase.OutBack,2.2f),P(F,ca.Follow-.2f,3,BossEase.Linear),P(R,ca.Recover,4,BossEase.Settle,1.4f)),
                N(),
                N().Body(0,.2f,-.2f,10).Tilt(-6).Foreman(14,-6).Glow(1.3f,.25f),
                N().Body(0,.25f,-.25f,12).Tilt(-10).Foreman(18,-8,.4f).Glow(1.8f,.4f),
                N().Body(0,.22f,-.22f,11).Tilt(-8).Foreman(16,-6,.3f).Glow(1.6f,.35f),
                N());
            // ---- Charge: it drops low (2.95 m), the stance lengthening (the bogies dig back), the vessel tipped forward 24 like a
            // lowered head; the gallop (the root runs the authority's path; the gait morphs to a gallop), pitching; the skid: the tongs
            // dig in and the rear rises, the vessel swinging 30 forward ----
            var ch=NativeIronworksBoss.Find("charge");
            Def("charge",0,new BossActionDef("charge",.2f,.4f,P(A,ch.Windup,1,BossEase.InOut),P(C,.15f,2,BossEase.Out),P(S,ch.Strike-.15f,3,BossEase.Linear),P(F,ch.Follow,4,BossEase.OutBack,1.4f),P(R,ch.Recover,5,BossEase.Settle,1.5f)),
                N(),
                N().Body(0,-.75f,-.15f,-9).Tilt(24).Foreman(-12,14,.8f).Glow(1.3f,.2f).Set(IronworksBossCh.StanceZ,1.12f).Set(IronworksBossCh.Look,.4f),
                N().Body(0,-.45f,.2f,-6).Tilt(20).Foreman(-14,16,1).Glow(1.5f,.2f).Set(IronworksBossCh.Look,.2f),
                N().Body(0,-.4f,.25f,-6).Tilt(20).Foreman(-14,16,1).Glow(1.5f,.2f).Set(IronworksBossCh.Look,.2f),
                N().Body(0,-.2f,.35f,-8).Tilt(30).Foreman(-10,20,1).Glow(1.4f,.2f),
                N());
            // ---- Moments ----
            // Shed: the shudder (the cracks brighten), the burst (the crust flies, the rig's), the body dropping in relief, the bellow.
            var sh=NativeIronworksBoss.Find("shed");
            Def("shed",0,new BossActionDef("shed",.2f,.5f,P(A,sh.Windup,1,BossEase.InOut),P(C,.15f,2,BossEase.OutBack,2),P(F,.85f,3,BossEase.InOut),P(F,sh.Follow-1.0f,4,BossEase.OutBack,1.8f),P(R,sh.Recover,5,BossEase.Settle,1.4f)),
                N(),
                N().Body(0,-.08f,0,3).Tilt(2).Foreman(20,-10,.6f).Set(IronworksBossCh.Crack,1).Glow(1.3f,.3f),
                N().Body(0,.12f,0,10).Tilt(-14).Foreman(35,-12,1).Set(IronworksBossCh.Crack,.6f).Glow(1.6f,.4f).Foot(M.FL,FL.X+.2f,1.0f,FL.Z+.3f,20,.6f),
                N().Body(0,-.3f,0,-2).Tilt(8).Foreman(10,4,.4f).Glow(1.3f,.3f),
                N().Body(0,-.1f,-.1f,9).Tilt(-6).Foreman(14,-6,.3f).Glow(1.8f,.45f),
                N());
            // Break: it staggers sideways (the rig's stagger), the arch cracks and the pulpit tears (the rig's), the seams split white.
            var br=NativeIronworksBoss.Find("break");
            Def("break",0,new BossActionDef("break",.2f,.5f,P(A,br.Windup,1,BossEase.Out),P(C,.2f,2,BossEase.OutBack,2),P(F,br.Follow-.2f,3,BossEase.InOut),P(R,br.Recover,4,BossEase.Settle,1.5f)),
                N(),
                N().Body(.35f,-.25f,0,-3,0,8).Tilt(14).Glow(1.4f,.3f),
                N().Body(.2f,-.15f,0,4,0,-4).Tilt(-10).Glow(1.8f,.4f),
                N().Body(.1f,-.05f,0,1,0,-1).Tilt(12).Glow(1.8f,.4f),
                N());
            // Choke: a cough of black smoke, the rain cancelled, a slump (the grate open, glowing as it cools).
            var co=NativeIronworksBoss.Find("choke");
            Def("choke",0,new BossActionDef("choke",.15f,.5f,P(A,co.Windup,1,BossEase.OutBack,2),P(C,0,1,BossEase.Hold),P(R,.6f,2,BossEase.InOut),P(R,co.Recover-.6f,3,BossEase.Settle,1.3f)),
                N(),
                N().Body(0,.1f,-.1f,6).Tilt(-12).Foreman(4,-10,.6f).Glow(1.1f,.9f),
                N().Body(0,-.42f,.1f,-5).Tilt(18).Foreman(-4,12,.3f).Glow(.7f,.55f).Set(IronworksBossCh.Look,.3f),
                N());
            // Kneel: both front legs buckle (the rig sequences them .15 s apart; the feet stay planted, the knees fold), the body drops
            // onto them and the mouth dips; steam from the knee rams; it rises at the end.
            Kneeling=N().Body(0,-1.0f,.2f,-15).Tilt(20).Foreman(-6,24,.6f).Glow(.9f,.7f).Set(IronworksBossCh.Steam,1).Tongs(12,12);
            var kn=NativeIronworksBoss.Find("kneel");
            Def("kneel",0,new BossActionDef("kneel",.12f,.6f,P(A,kn.Windup,1,BossEase.In,1.6f),P(C,0,1,BossEase.Hold),P(R,.35f,2,BossEase.OutBack,1.6f),P(R,kn.Recover-1.05f,3,BossEase.Linear),P(R,.7f,4,BossEase.Settle,1.4f)),
                N(),Kneeling.Copy().Body(0,-1.05f,.22f,-16),Kneeling.Copy(),Kneeling.Copy().Body(0,-.9f,.18f,-13),N());
            // Crash: the charge into a wall: a big shake, the vessel clanging, stunned.
            var cr=NativeIronworksBoss.Find("crash");
            Def("crash",0,new BossActionDef("crash",.08f,.5f,P(A,cr.Windup,1,BossEase.Out,3),P(C,0,1,BossEase.Hold),P(R,.5f,2,BossEase.OutBack,1.5f),P(R,cr.Recover-.5f,3,BossEase.Settle,1.2f)),
                N(),
                N().Body(0,-.35f,-.5f,9,0,-3).Tilt(-22).Foreman(-20,-20,1).Glow(1.4f,.3f),
                N().Body(0,-.25f,-.2f,2,0,2).Tilt(14).Foreman(-6,8,.5f).Glow(1.2f,.25f).Set(IronworksBossCh.Look,.2f),
                N());
            // Arrive and die are their own tracks (the rig's), keyed on the arrival's song clock and the death's clock.
            var ar=NativeIronworksBoss.Find("arrive");Def("arrive",0,new BossActionDef("arrive",.01f,.01f,P(A,ar.Windup,1,BossEase.Linear)),N(),N());
            var di=NativeIronworksBoss.Find("die");Def("die",0,new BossActionDef("die",.01f,.01f,P(A,di.Windup,1,BossEase.Linear)),N(),N());
            // ---- the arrival's poses (root on the pit's centre facing the shutter) ----
            Buried=N().Body(0,-8.4f,-.3f,16).Tilt(16).Glow(0,0).Set(IronworksBossCh.Lamp,0).Set(IronworksBossCh.Look,0)
                .Foot(M.FL,-2.4f,-2.2f,1.6f,20).Foot(M.FR,2.4f,-2.2f,1.7f,20).Foot(M.RL,-2.2f,-3.2f,-2.2f).Foot(M.RR,2.2f,-3.2f,-2.2f).Tongs(22,22);
            Gripped=N().Body(0,-5.2f,-.4f,18).Tilt(14).Glow(.45f,.1f).Set(IronworksBossCh.Lamp,0).Set(IronworksBossCh.Look,0).Foreman(0,-14)
                .Foot(M.FL,-2.6f,0,3.05f,8,1,true).Foot(M.FR,2.6f,0,3.05f,8,1,true).Foot(M.RL,-2.4f,-2.0f,-2.4f).Foot(M.RR,2.4f,-2.0f,-2.4f).Tongs(6,6);
            Heaved=N().Body(0,-2.2f,-.2f,13).Tilt(16).Glow(1.1f,.15f).Set(IronworksBossCh.Lamp,0).Set(IronworksBossCh.Look,0).Foreman(6,-8)
                .Foot(M.RL,-2.6f,-.6f,-2.6f).Foot(M.RR,2.6f,-.6f,-2.6f).Tongs(4,4);
            Risen=N().Body(0,.08f,0,2).Tilt(4).Glow(1.2f,.2f).Set(IronworksBossCh.Look,.6f);
            Dead=N().Body(.15f,-2.05f,.1f,-8,0,9).Tilt(62).Glow(0,0).Set(IronworksBossCh.Lamp,0).Set(IronworksBossCh.Look,0).Foreman(-10,30,1).Tongs(18,8);
        }
        /// <summary>Samples a phrase's keys at `progress` into `into` (linear between keys: the phases' eases shape the progress).</summary>
        public static void Sample(IronworksBossSnap[] keys,float progress,float[] into)
        {
            int n=keys.Length;if(n==0){Array.Copy(IronworksBossCh.Neutral,into,into.Length);return;}
            int k=(int)Math.Floor(progress);if(k<0)k=0;if(k>n-2)k=Math.Max(0,n-2);float u=n<2?0:BossMath.Clamp01(progress-k);var a=keys[k].V;var b=keys[Math.Min(n-1,k+1)].V;
            for(int c=0;c<into.Length;c++)into[c]=a[c]+(b[c]-a[c])*u;
            // A plant flag holds from the key that lands the foot (never blended: it is an event).
            for(int leg=0;leg<4;leg++){int f=IronworksBossCh.Leg(leg,IronworksBossCh.PL);into[f]=u>=1-1e-4f?b[f]:a[f];}
        }
    }
}
