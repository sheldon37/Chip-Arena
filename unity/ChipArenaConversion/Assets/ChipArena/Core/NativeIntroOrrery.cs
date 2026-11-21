using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Orrery", the Glaive's intro (intros v2; docs/INTROS-MELEE.md): the ring that sets the heavens in order. 7.6 s, the name at 6.0 s.
    /// Twilight in the Observatory (an open stone observatory on a mountain summit: a round floor inlaid with brass rings, a balustrade of
    /// columns and iron ribs rising to an oculus, a great brass refractor; beyond, a sea of cloud and two ranges of peaks). Over the pilot
    /// four great armillary rings hang level, turning slowly, their inlays lit. The camera comes down through them to the pilot on the dais,
    /// the ring blade at its side. It winds the ring across its body and throws it backhand: the ring ricochets round the orrery, striking
    /// each armillary ring in turn (each rings and swings out of level, its inlay flaring), and comes back round behind the pilot to the
    /// hand, caught high. At the catch the four rings swing into an armillary sphere round the pilot and lock with a chime, a shaft of
    /// starlight falls through the oculus onto it, and it stands under the name, the ring at its side facing out, its edge lit.
    /// While the ring is out of the hand the runtime draws no held ring (Sheathed) and the set's own ring blade flies. Presentation only:
    /// pure functions of the presentation clock.
    /// </summary>
    public sealed class NativeIntroOrrery:NativeIntroMeleeBlade
    {
        public const string Title="Orrery";
        public const float Total=7.6f,CardAt=6.0f;
        /// <summary>The beats (presentation s): the wind-up, the release, the four ricochets, the catch, the alignment locking, the stance.</summary>
        public const float WindAt=1.45f,ReleaseAt=2.12f,CatchAt=4.3f,LockAt=5.15f,StanceAt=5.5f;
        public static readonly float[] HitAt={2.47f,2.87f,3.27f,3.64f};
        private static readonly Vector3 Twilight=NativeEntranceFx.Hex(0x6d6fb0),Dusk=NativeEntranceFx.Hex(0xff9e6a),Inlay=NativeEntranceFx.Hex(0x5fe3dc),Edge=NativeEntranceFx.Hex(0xa6f2ff),
            Star=NativeEntranceFx.Hex(0xdfeeff),Night=NativeEntranceFx.Hex(0x0e1124),Brass=NativeEntranceFx.Hex(0xe8c27a);
        public override string Spec=>NativeGlaive.Spec;
        // ---- the orrery: four armillary rings round (0, 2.4, 0) over the dais (Unity y; melee_observatory.py RING_RADII, RING_REST_Y) ----
        private static readonly float[] Radius={2.6f,3.2f,3.8f,4.4f};
        private const float RingY=2.4f,Spin=.12f;
        /// <summary>Which ring each ricochet strikes (the outermost first) and where on it (rad round from the pilot's front, + to its left).</summary>
        private static readonly int[] HitRing={3,1,2,0};
        private static readonly float[] HitAz={-.62f,1.05f,2.8f,-1.92f};
        /// <summary>Each ring's tilt in the sphere at the lock (rad) and the azimuth of the axis it tilts about (from the pilot's front).</summary>
        private static readonly float[] LockTilt={1.0f,.73f,.56f,.44f},LockAxis={0,1.5708f,.785f,-.785f};
        private static readonly string[] ringProps={"observatory/arm-ring-0","observatory/arm-ring-1","observatory/arm-ring-2","observatory/arm-ring-3"};
        /// <summary>Where the drawn ring leaves the hand and where the hand takes it back (source frame).</summary>
        private static readonly Vector3 ReleaseGrip=Local(-.5f,1.28f,.42f),CatchGrip=Local(-.42f,1.72f,.2f);
        private const float RingCentre=NativeWeaponModels.GlaiveCentre;

        public NativeIntroOrrery():base(NativeIntroCatalog.WeaponPrefix+NativeGlaive.Spec,Total,CardAt,ReleaseAt,0x7fd8ff)
        {
            // ---- the body (bespoke keys, owner 2026-11-20: no match clips; the feet and drops on the model, ModelFeet) ----
            // Feet (the model's ankles): easy on the dais; the left foot steps across for the throw; the right foot pivots out as it turns to
            // follow the ring behind it; both square up for the stance.
            Start=Stance(.12f,.04f,.12f,-.13f,-.06f,-.32f);
            Steps=new[]{Step(true,WindAt-.05f,WindAt+.25f,.2f,.22f,.3f,.07f),Step(false,ReleaseAt+.1f,ReleaseAt+.4f,-.24f,-.02f,-.7f,.06f),
                Step(true,CatchAt+.35f,CatchAt+.7f,.13f,.06f,.15f,.06f),Step(false,LockAt+.05f,LockAt+.38f,-.15f,-.04f,-.35f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // On the dais at dusk, easy, the ring low at its side, looking up at the turning rings.
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.02f).J(B,0).J(C,-.03f,0,0).J(N,-.1f).J(Hd,-.22f,.05f,0).J(LA,-.05f,.06f,.16f).J(LE,-.3f);
            k.Key(WindAt-.15f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(B,.02f).J(C,-.02f,.05f,0).J(N,-.04f).J(Hd,-.08f,0,0).J(LA,-.05f,.06f,.16f).J(LE,-.3f);
            // The wind-up: the left foot across, the chest coiled left, the ring wound across to the left hip, the eyes on the throw's line.
            k.Key(ReleaseAt-.2f,stand).Root(.02f,.88f,.08f,Facing+.15f).Planted(1).Drop(-.16f).J(B,.1f).J(Sp,0,.2f,0).J(C,.04f,.42f,0).J(N,0,-.2f,0).J(Hd,-.04f,-.34f,0)
             .J(LA,-.2f,.1f,.55f).J(LE,-.5f);
            // The throw: the body unwinds right, the arm whips out, the left arm flung back for balance.
            k.Key(ReleaseAt,stand).Root(.02f,.88f,.1f,Facing).Planted(1).Drop(-.12f).J(B,.08f).J(Sp,0,-.12f,0).J(C,.02f,-.3f,0).J(N,0,.06f,0).J(Hd,-.06f,.06f,0)
             .J(LA,.15f,.1f,.7f).J(LE,-.35f).ImpactKey();
            k.Key(ReleaseAt+.25f,stand).Root(0,.88f,.06f,Facing-.15f).Planted(1).Drop(-.08f).J(B,.04f).J(Sp,0,-.16f,0).J(C,0,-.36f,0).J(N,-.05f,.05f,0).J(Hd,-.18f,.1f,0)
             .J(LA,.05f,.08f,.4f).J(LE,-.4f);
            // Watching it round the orrery: the head and chest follow it behind, over the left shoulder, then the right.
            k.Key(HitAt[1],stand).Root(0,.88f,.04f,Facing+.1f).Planted(1).Drop(-.06f).J(B,.02f).J(Sp,0,.18f,0).J(C,-.04f,.3f,0).J(N,-.1f,.3f,0).J(Hd,-.22f,.3f,0).J(LA,-.05f,.06f,.2f).J(LE,-.35f);
            k.Key(HitAt[2],stand).Root(0,.88f,.04f,Facing+.25f).Planted(1).Drop(-.07f).J(B,.02f).J(Sp,0,.3f,0).J(C,-.04f,.42f,0).J(N,-.08f,.5f,0).J(Hd,-.2f,.6f,0).J(LA,-.05f,.06f,.2f).J(LE,-.35f);
            k.Key(HitAt[3],stand).Root(0,.88f,.04f,Facing-.25f).Planted(1).Drop(-.08f).J(B,.03f).J(Sp,0,-.3f,0).J(C,-.04f,-.42f,0).J(N,-.08f,-.45f,0).J(Hd,-.16f,-.5f,0).J(LA,-.05f,.06f,.2f).J(LE,-.35f);
            // The catch: reaching up to its right for the returning ring; it takes it and the ring's weight swings the arm back down.
            k.Key(CatchAt-.12f,stand).Root(0,.88f,.04f,Facing-.15f).Planted(1).Drop(-.04f).J(B,-.04f).J(Sp,0,-.12f,0).J(C,-.1f,-.2f,0).J(N,-.08f,-.1f,0).J(Hd,-.22f,-.12f,0).J(LA,-.1f,.06f,.3f).J(LE,-.35f);
            k.Key(CatchAt+.12f,stand).Root(0,.88f,.03f,Facing-.08f).Planted(1).Drop(-.16f).J(B,.1f).J(Sp,0,-.05f,0).J(C,.02f,-.12f,0).J(Hd,-.02f,-.05f,0).J(LA,-.25f,.08f,.5f).J(LE,-.5f).ImpactKey();
            // The orrery aligns: it rises, the ring brought down to its side, the eyes up to the sphere closing round it.
            k.Key(LockAt-.2f,stand).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.05f).J(B,-.02f).J(C,-.08f,0,0).J(N,-.1f).J(Hd,-.3f,0,0).J(LA,-.08f,.06f,.22f).J(LE,-.4f);
            k.Key(LockAt+.12f,stand).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.06f).J(B,-.02f).J(C,-.1f,0,0).J(N,-.12f).J(Hd,-.34f,0,0).J(LA,-.08f,.06f,.22f).J(LE,-.4f).ImpactKey();
            // The stance under the name: tall, the chin coming down level to the lens, the ring at its side.
            k.Key(StanceAt+.35f,NativeIntroPoses.Hero()).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.03f).J(C,-.07f,-.08f,0).J(Hd,-.04f,.08f,0).PassKey();
            k.Key(Total,NativeIntroPoses.Hero()).Root(0,.88f,.02f,Facing).Planted(1).Drop(-.03f).J(C,-.08f,-.08f,0).J(Hd,-.05f,.09f,0);Keys=k;
            // ---- the ring's way in the hand (the grip and the way from the fist to the ring's middle, in the keyed chest's frame) ----
            Ring.InChest().Key(0,V(-.4f,-.28f,.14f),V(-.25f,-.45f,.86f)).Key(WindAt-.15f,V(-.4f,-.27f,.15f),V(-.25f,-.44f,.86f))
                .Key(WindAt+.17f,V(-.25f,-.18f,.25f),V(.8f,-.33f,.51f)).Pass()
                .Key(ReleaseAt-.2f,V(-.1f,-.08f,.28f),V(.8f,.05f,-.6f))
                .Key(ReleaseAt-.1f,V(-.24f,.04f,.4f),V(.46f,.38f,.8f)).Pass()
                .Key(ReleaseAt-.08f,V(-.38f,.08f,.37f),V(.27f,.6f,.75f)).Pass()
                .Key(ReleaseAt-.05f,V(-.52f,.13f,.44f),V(-.05f,.3f,.95f)).Pass()
                .Key(ReleaseAt,V(-.58f,.16f,.43f),V(-.03f,.1f,1)).Pass()
                .Key(ReleaseAt+.07f,V(-.5f,.08f,.38f),V(-.3f,.5f,.81f)).Pass()
                // (the hand empty while the ring flies: the arm comes down easy, the hand as if it held it)
                .Key(ReleaseAt+.18f,V(-.42f,-.03f,.27f),V(-.25f,.38f,.89f)).Pass()
                .Key(ReleaseAt+.4f,V(-.4f,-.2f,.2f),V(-.22f,-.31f,.93f))
                .Key(HitAt[1],V(-.4f,-.28f,.14f),V(-.25f,-.45f,.86f))
                .Key(CatchAt-.45f,V(-.4f,-.26f,.15f),V(-.25f,-.44f,.86f))
                .Key(CatchAt-.3f,V(-.43f,-.01f,.27f),V(-.36f,.44f,.82f))
                .Key(CatchAt-.23f,V(-.46f,.18f,.33f),V(-.71f,.61f,.35f)).Pass()
                .Key(CatchAt,V(-.55f,.48f,.34f),V(-.77f,.58f,-.28f))
                .Key(CatchAt+.1f,V(-.51f,.28f,.28f),V(-.82f,.56f,-.09f)).Pass()
                .Key(CatchAt+.22f,V(-.46f,.1f,.26f),V(-.8f,.54f,.26f))
                .Key(LockAt-.2f,V(-.42f,-.25f,.16f),V(-.85f,-.2f,.48f))
                .Key(Total,V(-.42f,-.26f,.15f),V(-.85f,-.2f,.48f));
            // The camera: down through the oculus and the turning rings to the pilot; close on the wind-up; low and wide as the ring strikes
            // round the orrery; on the catch; pulling back up as the rings close into a sphere round it; the hero.
            Rig.Crane(0,V(.6f,8.6f,2.2f),V(1.2f,1.6f,4.6f),0,V(0,2.6f,0),V(0,1.25f,0),56,42).Ease(NativeIntroEase.InOut)
               .Dolly(WindAt-.1f,V(-1.7f,1.25f,2.6f),V(-1.5f,1.2f,2.3f),V(0,1.1f,0),V(0,1.05f,0),38,36)
               .Orbit(ReleaseAt+.08f,Vector3.Zero,-40,140,6.2f,6.0f,.4f,.55f,V(0,2.2f,0),V(0,2.0f,0),62,60).Ease(NativeIntroEase.InOut)
               .Dolly(CatchAt-.3f,V(-1.4f,1.15f,1.9f),V(-1.3f,1.2f,1.75f),Local(-.35f,1.7f,.15f),Local(-.35f,1.5f,.15f),38,36)
               .Crane(CatchAt+.3f,V(.8f,1.0f,3.4f),V(1.2f,2.6f,6.6f),.6f,V(0,1.6f,0),V(0,2.2f,0),46,58).Ease(NativeIntroEase.InOut);
            HeroFull(StanceAt,1,4.6f,33,.38f);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
            Ramps.Ramp(CatchAt-.08f,.35f,.25f,.8f);
        }
        public override string SetBank=>Bank;

        // ---- the body: keyed (no match clips): the right arm on the ring's way; the drawn hand where the ring leaves it and where it comes back ----
        private readonly NativeIntroWield Ring=new NativeIntroWield();
        protected override float TipLength(bool left)=>left?.15f:RingCentre+NativeWeaponModels.GlaiveFang;
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            if(!WieldTarget(Ring,t,pose,root,rotation,out var at,out var axis,out _))return;
            // (one solve on blended targets: at the release the drawn grip where the flight starts; reaching for the catch, where it ends)
            float rel=NativeEntranceMath.Bump(t,ReleaseAt-.12f,ReleaseAt+.1f),cat=NativeEntranceMath.Smooth((t-(CatchAt-.3f))/.22f)*(1-NativeEntranceMath.Smooth((t-(CatchAt+.02f))/.12f));
            if(rel>.001f){at=Vector3.Lerp(at,DrawnTarget(pose,root,rotation,false,ReleaseGrip+new Vector3(0,NativeEntranceStage.FloorY,0)),rel);axis=Dir(axis,ReleaseAxis,rel);}
            if(cat>.001f){at=Vector3.Lerp(at,DrawnTarget(pose,root,rotation,false,CatchGrip+new Vector3(0,NativeEntranceStage.FloorY,0)),cat);axis=Dir(axis,CatchAxis,cat);}
            HoldAt(pose,root,rotation,false,at,axis,1);
        }
        /// <summary>The ring's line from the fist as it leaves the hand (out along the throw) and as it comes in (from behind, to the right).</summary>
        private static readonly Vector3 ReleaseAxis=Vector3.Normalize(Local(-.41f,.1f,.91f)),CatchAxis=Vector3.Normalize(Local(-.6f,.55f,-.58f));
        private static Vector3 Dir(Vector3 a,Vector3 b,float u){var d=Vector3.Lerp(a,b,u);return d.LengthSquared()>1e-8f?Vector3.Normalize(d):b;}
        /// <summary>The held ring is not drawn while it flies (the blade intros' Sheathed: no saya for the Glaive).</summary>
        public override bool Sheathed(float t)=>t>=ReleaseAt&&t<CatchAt;

        // ---- the ring's flight: from the hand round the four rings and back ----
        private static readonly Vector3[] Points=FlightPoints();
        private static readonly float[] PointAt={ReleaseAt,HitAt[0],HitAt[1],HitAt[2],HitAt[3],CatchAt};
        private static Vector3[] FlightPoints()
        {
            var p=new Vector3[6];float fy=NativeEntranceStage.FloorY;
            p[0]=ReleaseGrip+new Vector3(0,fy,0)+ReleaseAxis*RingCentre;
            for(int i=0;i<4;i++){float r=Radius[HitRing[i]],a=HitAz[i];p[i+1]=Local(r*(float)Math.Sin(a),0,r*(float)Math.Cos(a))+new Vector3(0,RingY+fy,0);}
            p[5]=CatchGrip+new Vector3(0,fy,0)+CatchAxis*RingCentre;return p;
        }
        /// <summary>Where the flying ring is at `t` (false: in the hand) and which way it travels: each leg a curve that bows out and up (a
        /// frisbee's banked flight), sharp at each ricochet.</summary>
        public static bool Flight(float t,out Vector3 at,out Vector3 travel)
        {
            at=travel=default;if(t<ReleaseAt||t>=CatchAt)return false;int leg=0;while(leg<4&&t>=PointAt[leg+1])leg++;
            float u=(t-PointAt[leg])/(PointAt[leg+1]-PointAt[leg]);var a=Points[leg];var b=Points[leg+1];var mid=(a+b)*.5f;var d=b-a;
            var side=Vector3.Normalize(new Vector3(-d.Z,0,d.X));var m=mid+side*(.12f*d.Length())+new Vector3(0,.25f,0);
            if(leg==4)u=1-(1-u)*(1-u)*.6f-(1-u)*.4f;// (the return slows into the hand)
            at=(1-u)*(1-u)*a+2*u*(1-u)*m+u*u*b;travel=2*(1-u)*(m-a)+2*u*(b-m);travel=travel.LengthSquared()>1e-8f?Vector3.Normalize(travel):Vector3.UnitZ;return true;
        }
        /// <summary>The ring's spin (rad) and its plane: banked 14 degrees into its travel, as the match throws it.</summary>
        private static Quaternion RingTurn(float t,Vector3 travel)
        {
            var flat=new Vector3(travel.X,0,travel.Z);if(flat.LengthSquared()<1e-6f)flat=Vector3.UnitZ;flat=Vector3.Normalize(flat);
            var normal=Vector3.Transform(Vector3.UnitY,Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY,flat)),.24f));
            var lay=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-1.5708f);// (the prop's ring lies in its x-y plane: its axis +z onto +y)
            return Quaternion.Normalize(Quaternion.CreateFromAxisAngle(normal,t*25.1f)*Turn(normal,lay));
        }
        private static Quaternion Turn(Vector3 normal,Quaternion lay)
        {
            var from=Vector3.UnitY;var c=Vector3.Cross(from,normal);float s=c.Length(),dd=Vector3.Dot(from,normal);
            var q=s<1e-6f?Quaternion.Identity:Quaternion.CreateFromAxisAngle(c/s,(float)Math.Atan2(s,dd));return Quaternion.Normalize(q*lay);
        }

        // ---- the armillary rings: turning slowly; each swung out of level by its ricochet, ringing; all closed into a sphere at the lock ----
        /// <summary>How ring `k` is turned at `t` (source frame): its slow turn about the vertical, its swing after it is struck, its tilt into
        /// the sphere from the catch to the lock.</summary>
        private static Quaternion RingPose(int k,float t,out float struck)=>Quaternion.Normalize(RingTilt(k,t,out struck)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,Spin*(1+.35f*k)*t*(k%2==0?1:-1)+.6f*k));
        /// <summary>Ring `k`'s tilt at `t` without its slow turn about the vertical (which leaves a level circle where it is).</summary>
        private static Quaternion RingTilt(int k,float t,out float struck)
        {
            struck=0;
            // (the ricochet: a swing about the line square to the blow, ringing down to a small set tilt)
            int hit=Array.IndexOf(HitRing,k);float swingTilt=0;Vector3 swingAxis=Vector3.UnitX;
            if(hit>=0&&t>=HitAt[hit])
            {
                float s=t-HitAt[hit];struck=(float)Math.Exp(-2.2f*s);float a=HitAz[hit];var radial=Local((float)Math.Sin(a),0,(float)Math.Cos(a));
                swingAxis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,radial));swingTilt=.22f*(float)(Math.Sin(s*9)*Math.Exp(-2.6f*s))+.12f*(1-(float)Math.Exp(-5*s));
            }
            float lockA=LockAxis[k],lockW=NativeEntranceMath.Smoother((t-CatchAt-.05f)/(LockAt-CatchAt-.05f));
            var lockAxis=Local((float)Math.Cos(lockA),0,-(float)Math.Sin(lockA));
            var swing=Quaternion.CreateFromAxisAngle(swingAxis,swingTilt*(1-lockW));
            // (it settles into the sphere with a small overshoot at the lock)
            float settle=lockW+.06f*(float)(Math.Sin(Math.Max(0,t-LockAt)*11)*Math.Exp(-4*Math.Max(0,t-LockAt)))*(t>=LockAt?1:0);
            var tilt=Quaternion.CreateFromAxisAngle(Vector3.Normalize(lockAxis),LockTilt[k]*settle);
            return Quaternion.Normalize(tilt*swing);
        }
        private static Vector3 RingCentreAt=>new Vector3(0,RingY+NativeEntranceStage.FloorY,0);
        /// <summary>The lowest point of armillary ring `k` at `t` (source frame y): its centre less its radius times its tilt's sine.</summary>
        public static float RingLowest(int k,float t){var n=Vector3.Transform(Vector3.UnitY,RingTilt(k,t,out _));return RingY+NativeEntranceStage.FloorY-Radius[k]*(float)Math.Sqrt(Math.Max(0,1-n.Y*n.Y));}

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("observatory/static"))return false;
            // The floor's lit lines and the panes (the glow sub-mesh), brighter as the sphere locks.
            float lockGlow=1+.8f*NativeEntranceMath.Ramp(t,LockAt-.3f,LockAt+.3f);
            set.Piece("observatory/static",SetAt,Quaternion.Identity,1,White,White*lockGlow);
            for(int k=0;k<4;k++){var q=RingPose(k,t,out float struck);float flare=1+2.2f*struck+1.6f*NativeEntranceMath.Pulse(t,LockAt,.02f,.6f);set.Piece(ringProps[k],RingCentreAt,q,1,White,White*flare);}
            if(Flight(t,out var at,out var travel))set.Piece("observatory/ringblade",at,RingTurn(t,travel),1,White,White*1.4f);
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x1e2132),Inlay,.8f,NativeEntranceFx.Hex(0x3a3560));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x2a2a48);l.FogNear=14;l.FogFar=90;l.DriveProbe=true;
            float lockLvl=NativeEntranceMath.Ramp(t,LockAt-.3f,LockAt+.4f);
            l.Sky=NativeEntranceFx.Hex(0x5a5c96,.5f+.15f*lockLvl);l.Ground=NativeEntranceFx.Hex(0x2a2430,.4f);
            l.ReflectSky=NativeEntranceFx.Hex(0x40447a);l.ReflectHorizon=NativeEntranceFx.Hex(0xc07a6a);l.ReflectGround=NativeEntranceFx.Hex(0x1a1820);
            // (the set takes only directionals and the ambient: the last of the sunset low behind the lens's left, the cold sky from above)
            l.Directional(new Vector3(-.75f,.28f,.6f),Dusk*.75f);l.Directional(new Vector3(.2f,1,-.3f),Twilight*.45f);
            l.Directional(new Vector3(0,1,0),Star*(.9f*lockLvl));
            // The rings' inlays over the pilot, the flying ring's light, each ricochet's flash, the catch, the starlight at the lock.
            l.Add(new Vector3(0,RingY+1.2f,0),Inlay*(1.2f+1.4f*lockLvl),7);
            if(Flight(t,out var at,out _))l.Add(at,Edge*2.2f,3.5f);
            for(int i=0;i<4;i++){float f=Flash(t,HitAt[i],.01f,.2f);if(f>.01f){float r=Radius[HitRing[i]],a=HitAz[i];l.Add(Local(r*(float)Math.Sin(a),0,r*(float)Math.Cos(a))+RingCentreAt,(White+Edge)*(3.5f*f),5);}}
            float c=Flash(t,CatchAt,.01f,.25f);if(c>.01f)l.Add(CatchGrip+new Vector3(0,NativeEntranceStage.FloorY,0),(White*2+Edge)*(3*c),4);
            if(lockLvl>.01f)l.Add(new Vector3(0,3.2f,0),Star*(2.4f*lockLvl),6);
            float name=NativeEntranceMath.Ramp(t,CardAt-.25f,CardAt+.35f);if(name>.01f)l.Add(new Vector3(1.6f,2.0f,1.8f),Edge*(1.2f*name),7);
        }

        // ---- the effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY,lockLvl=NativeEntranceMath.Ramp(t,LockAt-.3f,LockAt+.4f);
            // The first stars over the oculus and through the open sky, twinkling slowly.
            for(int i=0,n=low?14:36;i<n;i++)
            {
                float a=NativeEntranceMath.Rand(41001,i)*6.2831853f,e=.45f+.5f*NativeEntranceMath.Rand(41003,i),r=60;var p=new Vector3((float)Math.Cos(a)*(float)Math.Cos(e)*r,10+(float)Math.Sin(e)*r*.6f,(float)Math.Sin(a)*(float)Math.Cos(e)*r);
                float tw=.55f+.45f*(float)Math.Sin(t*(.7f+NativeEntranceMath.Rand(41005,i))+i);fx.Glow(p,Star*(.5f*tw*(.6f+.6f*lockLvl)),0,.35f,0,0,41010+i);
            }
            // Dust turning in the dome's dusk light.
            for(int i=0,n=low?6:14;i<n;i++){var p=new Vector3(NativeEntranceMath.Signed(41101,i)*4,.6f+3.4f*NativeEntranceMath.Rand(41103,i),NativeEntranceMath.Signed(41105,i)*4)+new Vector3((float)Math.Sin(t*.2f+i)*.25f,(float)Math.Sin(t*.17f+i*2)*.12f,0);fx.Glow(p,Brass*.05f,0,.025f,0,0,41110+i);}
            // The ring in flight: its spinning light and a wake back along its path, laid onto the camera's plane.
            if(Flight(t,out var at,out var travel))
            {
                fx.Glow(at,(Edge*1.6f+White*.4f),0,.32f,1,0,41200);
                int m=low?6:12;var prev=at;
                for(int i=1;i<=m;i++){float s=t-.022f*i;if(!Flight(s,out var p,out _))break;float f=1-i/(float)(m+1);Line(fx,prev,prev-p,Edge*(1.4f*f*f),.05f*f+.01f,.2f,1-f,41210+i);prev=p;}
            }
            // The ring in the hand: a crescent of light off its edge through the fast part of the throw and the catch.
            if(t>=ReleaseAt-.25f&&t<ReleaseAt||t>=CatchAt&&t<CatchAt+.3f)BladeSweep(fx,t,0,t<ReleaseAt?ReleaseAt-.25f:CatchAt,.18f,Edge*1.5f,Edge,.022f,.3f,41300,low);
            // Each ricochet: a ping of light and sparks off the struck ring, a ripple of light running round its inlay.
            for(int i=0;i<4;i++)
            {
                float s=t-HitAt[i];if(s<0||s>1.2f)continue;int k=HitRing[i];float r=Radius[k],a=HitAz[i];var hit=Local(r*(float)Math.Sin(a),0,r*(float)Math.Cos(a))+RingCentreAt;
                if(s<.16f)fx.Glow(hit,(White*2+Edge)*(2*(1-s/.16f)),0,.3f+.9f*s,1,s/.16f,41400+i);
                NativeEntranceFx.Sparks(fx,t,HitAt[i],hit,Vector3.Normalize(new Vector3(-hit.X,.4f,-hit.Z)),1.1f,low?10:24,6.5f,.6f,7,.5f,White*2+Edge,Brass*1.5f,.01f,41410+i*40,q,floor);
                // (the ripple: two lights running round the ring from the blow, in the ring's own tilted plane)
                if(s<1.0f){var tilt=RingTilt(k,t,out _);float run=s*3.2f,fade=1-s;for(int j=-1;j<=1;j+=2){float az=a+j*run;var p=RingCentreAt+Vector3.Transform(Local(r*(float)Math.Sin(az),0,r*(float)Math.Cos(az)),tilt);fx.Glow(p,Inlay*(1.6f*fade),0,.14f,1,0,41500+i*4+j+1);}}
            }
            // The catch: a flash in the hand and the ring's light drawn into it.
            float cs=t-CatchAt;if(cs>=0&&cs<.3f){var g=CatchGrip+new Vector3(0,floor,0);fx.Glow(g,(White*2+Edge)*(1.6f*(1-cs/.3f)),0,.25f+.6f*cs,1,cs/.3f,41600);}
            // The lock: the starlight shaft down the oculus onto the dais, motes rising in it, the sphere's inlays flaring once.
            if(lockLvl>.01f)
            {
                NativeEntranceFx.Shaft(fx,t,new Vector3(0,11+floor,0),new Vector3(0,floor,0),1.0f,.85f,Star*.45f,lockLvl,low?6:12,low?10:24,41700,q);
                for(int i=0,n=low?8:20;i<n;i++){float per=2.2f+NativeEntranceMath.Rand(41801,i),u=((t/per)+NativeEntranceMath.Rand(41803,i))%1f;var p=new Vector3(NativeEntranceMath.Signed(41805,i)*.7f,floor+.1f+3.4f*u,NativeEntranceMath.Signed(41807,i)*.7f);fx.Glow(p,Star*(.6f*lockLvl*(1-u)),0,.03f,0,u,41810+i);}
            }
            // Under the name: a glint round the ring's edge.
            if(t>=CardAt-.1f&&t<CardAt+.6f){DrawnBlade(t,0,out var root,out var tip);float g=NativeEntranceMath.Bump(t,CardAt-.1f,CardAt+.6f);NativeEntranceFx.Glint(fx,Vector3.Lerp(root,tip,.8f),(White+Edge)*(1.4f*g),.12f*g,Vector3.UnitX,Vector3.UnitY,41900);}
        }

        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.orrery.dusk","boss.noctiel.hush","darkness",.25f,.7f),
            new NativeIntroCue(WindAt,"intro2.orrery.wind","melee.shield.throw","dash",.35f,.9f),
            new NativeIntroCue(ReleaseAt,"intro2.orrery.throw","melee.daggers.throw","dash",.8f),
            new NativeIntroCue(HitAt[0],"intro2.orrery.ping","impact.metal","metal",.8f,.8f),
            new NativeIntroCue(HitAt[1],"intro2.orrery.ping2","impact.metal","metal",.8f,1.05f),
            new NativeIntroCue(HitAt[2],"intro2.orrery.ping3","impact.metal","metal",.8f,.92f),
            new NativeIntroCue(HitAt[3],"intro2.orrery.ping4","impact.metal","metal",.85f,1.2f),
            new NativeIntroCue(CatchAt-.2f,"intro2.orrery.return","structure.whoosh","dash",.5f,1.2f),
            new NativeIntroCue(CatchAt,"intro2.orrery.catch","melee.shield.block","parry",.85f,1.1f),
            new NativeIntroCue(LockAt,"intro2.orrery.lock","boss.noctiel.hush.open","install",.75f),
            new NativeIntroCue(CardAt,"intro2.orrery.name","ui.install.lock","win",.45f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.95f,.98f,1),Edge*1.2f);card.Style=NativeIntroCardStyle.Assemble;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
