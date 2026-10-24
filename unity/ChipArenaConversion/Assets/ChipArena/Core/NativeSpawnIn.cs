using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>How the beam lets a pilot go at the fight start (NativeSpawnIn.Mode).</summary>
    public enum SpawnInMode : byte
    {
        /// <summary>The authored spawn is above the floor (today: every map): the beam holds the pilot at the spawn's own
        /// height and releases it at the fight start; it drops for the authority's own fall and lands.</summary>
        Drop,
        /// <summary>A grounded spawn (lift under LiftMin, Astra's option 1 in docs/SPAWN-IN.md): the beam holds the pilot at
        /// Hover and lowers it to the floor by the fight start, a soft touchdown.</summary>
        Lower
    }

    /// <summary>One pilot's spawn-in at one presented moment (NativeSpawnIn.Evaluate). Heights are metres, times seconds.</summary>
    public struct SpawnInFrame
    {
        /// <summary>The spawn-in shows something for this pilot (from the beam's strike to the end of the landing).</summary>
        public bool Active;
        /// <summary>Presentation time minus the fight start: negative while the countdown runs, 0 at GO.</summary>
        public float Age;
        /// <summary>Build progress (0 before the body starts forming, 1 once it is whole) and the build front's height above
        /// the presented feet: parts below it are solid, a hologram slice leads it (under the feet, so nothing shows, before
        /// the build starts).</summary>
        public float Built,Front;
        /// <summary>The presented feet above the floor's rest height: the beam's hold and bob, then the drop or the lowering.</summary>
        public float Lift;
        /// <summary>The beam holds the pilot: its rig is posed on the ground (no Fall loop), whatever the contact flag says.</summary>
        public bool Held;
        /// <summary>The release drop: its rig is posed airborne, so the jump layer lands it at the touchdown.</summary>
        public bool Falling;
        /// <summary>The hardlight tint on the forming body (0..1), the completion's glitch flash (0..1) and the RGB split's
        /// offset (m; 0 none).</summary>
        public float Tint,Flash,Split;
        /// <summary>The beam: its opacity, its span as fractions of floor..top (bottom, top), its column's half width, its core
        /// line's opacity, the strike's head glow and the release flash at GO.</summary>
        public float Beam,BeamFrom,BeamTo,BeamWidth,Core,Strike,Release;
        /// <summary>The floor ring marking the spawn (opacity, radius), a countdown tick's pulse ring (opacity, radius), the
        /// glow pool on the floor, the rising motes and the scan ring at the build front.</summary>
        public float Ring,RingRadius,Pulse,PulseRadius,Pool,Motes,Scan;
        /// <summary>Seconds since the touchdown (its impact ring, dust and camera kick), -1 before it.</summary>
        public float Land;
    }

    /// <summary>The detail a tier draws (NativeSpawnIn.Tier): Full, Reduced effects, the low graphics tier.</summary>
    public struct SpawnInTier
    {
        public int Motes,Front,Dust,Sparks,Segments;
        /// <summary>The hologram slice and the completion's flash silhouette (the ghost shader); the RGB split and the hold's
        /// glitch blips; the beam's shimmer.</summary>
        public bool Ghost,Glitch,Shimmer;
        /// <summary>The glow gain of every piece (Reduced is calmer).</summary>
        public float Gain;
    }

    /// <summary>The first-person screen of my own spawn-in (NativeSpawnIn.Screen): a scanline wipe up the screen with the build,
    /// a veil over what has not materialized yet, a soft tinted vignette in the beam, an RGB edge split at the completion and a
    /// flash at GO. Opacities 0..1; Line is the scanline's height from the bottom of the screen (0..1).</summary>
    public struct SpawnInScreen
    {
        public float Line,LineGlow,Veil,Vignette,Split,Flash;
        public bool Any=>LineGlow>1e-3f||Veil>1e-3f||Vignette>1e-3f||Split>1e-3f||Flash>1e-3f;
    }

    /// <summary>
    /// Where the geometry goes (Runtime/NativeSpawnInPresentation.cs fills one dynamic mesh drawn with NativeGlow.shader,
    /// additive): position, uv (xy; z the shader's mode: 0 gaussian halo over uv in [-1,1], 1 ribbon with a (1-|u|)^2 profile
    /// across u, 2 flat) and colour (rgb, a the opacity). Vertex returns the vertex's index.
    /// </summary>
    public interface ISpawnInSink
    {
        int Vertex(Vector3 position,Vector3 uv,Vector4 color);
        void Triangle(int a,int b,int c);
    }

    /// <summary>A pilot's spawn place, fixed for one spawn-in (NativeSpawnIn.Place): the beam's floor centre, the ceiling the
    /// column stops under, the hold height, the drop's length, the mode, the seed and the beam colour (rgb, brightest channel 1,
    /// in the colour space the caller draws in).</summary>
    public struct SpawnInPlace
    {
        public Vector3 Centre;public float Floor,Top,Hold,Drop;public SpawnInMode Mode;public int Seed;public Vector3 Color;
    }

    /// <summary>
    /// The match-start spawn-in (owner 2026-10-13: "Lets also add a spawning in animation; Players tend to float for 3 seconds
    /// when match is starting currently lol"; docs/SPAWN-IN.md). Every round's countdown becomes a hardlight drop-in: a beam
    /// strikes each pilot's spawn and rings its floor; the body digitizes from the feet up (a hologram slice leading the solid
    /// body, a scan ring at the front, the team tint, a glitch flash and an RGB split as it completes); the beam holds it over
    /// the floor with a subtle bob while motes stream up and the countdown ticks; and at the fight start it lets go: a short
    /// drop and a landing with a hardlight impact ring, dust and a camera kick for the local pilot.
    ///
    /// The float it turns into this (simulation unchanged; the Astra hand-off is in docs/SPAWN-IN.md): every authored spawn is
    /// a capsule centre at 1.1 m on a floor at 0, 0.23 m above the ground motor's rest (HalfHeight + Skin = 0.87), and outside
    /// the fight the authority returns from its step before it probes the ground, so the replicated contact flag stays
    /// airborne through the intro and the countdown and every rig plays the jump layer's Fall loop until the first fight tick.
    ///
    /// Timing is a pure function of replicated match state (End: the countdown's PhaseEnd, then the fight's FightAt) and the
    /// presentation clock, so the host, guests (their own predicted pilot drawn GuestLead ahead), the killcam and late joiners
    /// draw the same moment on every round. The hold ends exactly at the fight start. The drop lasts the authority's own fall
    /// from the authored spawn (AuthorityFall) plus a tick, so the landing meets the real touchdown; a grounded spawn is lowered
    /// to the floor by the fight start instead (SpawnInMode.Lower). Presentation only: no rule, timing, hitbox, cooldown,
    /// movement or network field reads it. Engine free; nothing here allocates.
    /// </summary>
    public static class NativeSpawnIn
    {
        // ---- the clock ----
        /// <summary>The countdown NativeMatch holds before every fight (BeginRound and NextIntro: PhaseEnd = Time + 3).</summary>
        public const float Countdown=3;
        /// <summary>A live guest draws its own predicted pilot this far ahead of the snapshot clock it draws the others at
        /// (NativeSnapshotClock.Reset: Latest - .1), so its drop starts when its controls unlock.</summary>
        public const float GuestLead=.1f;
        /// <summary>The fight start this spawn-in ends at, from replicated match state: the countdown's end while it runs,
        /// then the fight's recorded start (Fight and the round's Result); NaN in every other phase.</summary>
        public static double End(NativePhase phase,double phaseEnd,double fightAt)
            =>phase==NativePhase.Countdown?phaseEnd:phase==NativePhase.Fight||phase==NativePhase.Result?fightAt:double.NaN;
        /// <summary>Presentation time `now` minus the fight start (NaN without one). A countdown frame never reads earlier than
        /// the strike, so a clock a tick behind the round's reset does not show the new pilot whole for a frame.</summary>
        public static float Age(NativePhase phase,double phaseEnd,double fightAt,double now)
        {
            double end=End(phase,phaseEnd,fightAt);if(double.IsNaN(end)||double.IsNaN(now)||double.IsInfinity(end)||double.IsInfinity(now))return float.NaN;
            float age=(float)(now-end);return phase==NativePhase.Countdown?Math.Max(age,StrikeAt):age;
        }

        // ---- the timeline (seconds from the fight start) ----
        /// <summary>The beam drives down from the top onto the spawn over StrikeSeconds, from the countdown's start.</summary>
        public const float StrikeAt=-Countdown,StrikeSeconds=.3f,StrikeEnd=StrikeAt+StrikeSeconds;
        /// <summary>The body forms from the feet up over BuildSeconds; the front climbs to BuildHeight above the feet (over a
        /// raised weapon or a hat).</summary>
        public const float BuildAt=StrikeAt+.25f,BuildSeconds=1.2f,BuiltAt=BuildAt+BuildSeconds,BuildHeight=2.35f;
        /// <summary>A part turns solid over Band once the front is SolidLag above it; the hologram slice reaches GhostLead
        /// above the front; a just-formed part keeps its hardlight heat over HeatSpan of the front's climb.</summary>
        public const float Band=.24f,SolidLag=.1f,GhostLead=.28f,GhostOpacity=.62f,HeatSpan=.55f;
        /// <summary>The completion: the glitch flash (FlashSeconds), the RGB split (SplitSeconds, SplitMax m) and the tint's
        /// fade (TintFade); two glitch blips in the hold (BlipSeconds, BlipSplit m) on Full only.</summary>
        public const float FlashSeconds=.16f,SplitSeconds=.16f,SplitMax=.05f,TintFade=.5f,BlipSeconds=.07f,BlipSplit=.02f;
        /// <summary>The hold's bob: Bob m (ReducedBob with Reduced effects) every BobPeriod s, easing in over BobIn after the
        /// build and out over Charge before the release.</summary>
        public const float Bob=.022f,ReducedBob=.012f,BobPeriod=1.45f,BobIn=.45f,Charge=.3f;
        /// <summary>A grounded spawn: the beam holds the feet Hover above the floor and lowers them over LowerSeconds, landing at
        /// the fight start. A spawn whose lift is over LiftMin drops; the hold never exceeds MaxHold.</summary>
        public const float Hover=.16f,LowerSeconds=.32f,LiftMin=.06f,MaxHold=.45f;
        /// <summary>The release: the beam retracts upward over ReleaseSeconds, its flash over ReleaseFlash; the landing's
        /// effects run LandSeconds from the touchdown.</summary>
        public const float ReleaseSeconds=.32f,ReleaseFlash=.2f,LandSeconds=.6f;
        /// <summary>The hold ring's radius, its charge radius (the beam tightening before GO) and the tick pulse's life.</summary>
        public const float RingRadiusHold=.58f,RingRadiusCharge=.4f,PulseSeconds=.42f;

        // ---- the spawn ----
        /// <summary>The presented feet sit this far under the capsule centre (NativeArena.Snapshot: body - .865).</summary>
        public const float FeetOffset=.865f;
        /// <summary>The ground motor's rest: capsule centre HalfHeight + Skin over the floor (0.87), its presented feet RestFeet
        /// over the floor (0.005).</summary>
        public const float RestCentre=NativeLocomotionRules.HalfHeight+NativeLocomotionRules.Skin,RestFeet=RestCentre-FeetOffset;
        /// <summary>The authority's gravity (NativeArena: Physics.gravity -23) for its fall from the spawn.</summary>
        public const float Gravity=NativeJumpRules.Gravity;
        /// <summary>An authored spawn within this horizontal distance of the presented pilot is its spawn.</summary>
        public const float SpawnReach=1.5f;
        /// <summary>How high the authored spawn's capsule centre sits over the ground motor's rest on `floorY`.</summary>
        public static float SpawnLift(float centreY,float floorY)=>centreY-floorY-RestCentre;
        public static SpawnInMode Mode(float lift)=>lift>LiftMin?SpawnInMode.Drop:SpawnInMode.Lower;
        /// <summary>The presented feet's hold above the floor's rest: the spawn's own lift when it drops (the float, now held on
        /// purpose), Hover when the beam lowers a grounded spawn.</summary>
        public static float HoldFor(SpawnInMode mode,float lift)=>mode==SpawnInMode.Drop?MathUtil.Clamp(lift,0,MaxHold):Hover;
        /// <summary>
        /// The authority's own fall from a spawn `lift` over its rest (seconds from the first fight tick until its ground motor
        /// holds the pilot on the floor): outside the fight its velocity is zeroed every tick, then each fight tick adds a tick
        /// of gravity before PhysX moves it; the ground probe finds the floor once its gap is within GroundReach and the motor
        /// snaps the rest. 0 for a spawn already within reach.
        /// </summary>
        public static float AuthorityFall(float lift)
        {
            float gap=lift+NativeLocomotionRules.Skin,v=0;const float dt=1f/60;
            for(int n=0;n<240;n++){if(gap<=NativeLocomotionRules.GroundReach)return n*dt;v+=Gravity*dt;gap-=v*dt;}
            return 240*dt;
        }
        /// <summary>The presented drop: the authority's fall plus the tick its touchdown takes to reach the next snapshot.</summary>
        public static float DropFor(float lift)=>MathUtil.Clamp(AuthorityFall(lift)+1f/60,.05f,.3f);
        /// <summary>The authored spawn (the map's FFA and team tables, or the content's legacy table) nearest (x, z) within
        /// SpawnReach: its capsule centre height.</summary>
        public static bool AuthoredSpawn(ConversionContent content,ContentMap map,float x,float z,out float centreY)
        {
            float best=SpawnReach*SpawnReach,y=0;bool found=false;var tables=map?.spawns;
            if(tables!=null)
            {
                if(tables.ffa!=null)foreach(var s in tables.ffa)if(s!=null)Nearer(s.pos,x,z,ref best,ref y,ref found);
                if(tables.teams!=null)foreach(var t in tables.teams)if(t?.points!=null)foreach(var s in t.points)if(s!=null)Nearer(s.pos,x,z,ref best,ref y,ref found);
            }
            else if(content?.spawns!=null)foreach(var p in content.spawns)Nearer(p,x,z,ref best,ref y,ref found);
            centreY=y;return found;
        }
        /// <summary>The authored floor under (x, z): the highest top of an unrotated static block whose footprint holds the point,
        /// at or under `below` (the spawn's capsule centre). The Runtime's fallback when the arena's colliders cannot be queried.</summary>
        public static bool AuthoredFloor(ContentMap map,float x,float z,float below,out float floorY)
        {
            floorY=0;bool found=false;if(map?.blocks==null)return false;
            foreach(var b in map.blocks)
            {
                if(b==null||b.dynamic||b.rotation!=null&&(Math.Abs(b.rotation.x)>1e-4f||Math.Abs(b.rotation.y)>1e-4f||Math.Abs(b.rotation.z)>1e-4f))continue;
                if(Math.Abs(x-b.pos.x)>b.size.x/2||Math.Abs(z-b.pos.z)>b.size.z/2)continue;float top=b.pos.y+b.size.y/2;
                if(top<=below+1e-3f&&(!found||top>floorY)){floorY=top;found=true;}
            }
            return found;
        }
        private static void Nearer(V3 p,float x,float z,ref float best,ref float y,ref bool found)
        {float dx=p.x-x,dz=p.z-z,d=dx*dx+dz*dz;if(d<=best&&MathUtil.Finite(p.y)){best=d;y=p.y;found=true;}}
        /// <summary>The spawn place of one spawn-in: the floor under the spawn and the authored spawn's capsule centre decide
        /// the mode, the hold and the drop; `top` is where the column stops (the ceiling, or the sky).</summary>
        public static SpawnInPlace Place(Vector3 centre,float floorY,float topY,float spawnCentreY,int seed,Vector3 color)
        {
            float lift=SpawnLift(spawnCentreY,floorY);var mode=Mode(lift);
            return new SpawnInPlace{Centre=new Vector3(centre.X,floorY,centre.Z),Floor=floorY,Top=Math.Max(floorY+3,topY),Mode=mode,Hold=HoldFor(mode,lift),Drop=mode==SpawnInMode.Drop?DropFor(lift):0,Seed=seed,Color=color};
        }
        /// <summary>A seed both peers agree on: the pilot's id and the fight start's tick (replicated), never a local counter.</summary>
        public static int Seed(int id,double end)=>unchecked(id*7919+(int)Math.Round(end*60)*31);
        /// <summary>When the touchdown happens, from the fight start.</summary>
        public static float LandAt(in SpawnInPlace p)=>p.Mode==SpawnInMode.Drop?p.Drop:0;

        // ---- one moment ----
        private const float Tau=(float)(Math.PI*2);
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Ease(float u){u=Clamp01(u);return 1-(1-u)*(1-u);}
        private static float Hash(int seed,int k,int salt)
        {
            unchecked{uint h=(uint)seed*0x9e3779b1u^(uint)(k+1)*0x85ebca6bu^(uint)(salt+7)*0xc2b2ae35u;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}
        }
        /// <summary>The build front's height above the feet at build progress `built`: steady, with soft ends.</summary>
        public static float FrontAt(float built){float u=Clamp01(built);return BuildHeight*(.45f*u+.55f*MathUtil.Smooth(u));}
        /// <summary>
        /// The spawn-in at `age` seconds from the fight start (NaN: none) for a pilot whose beam holds its feet `hold` over the
        /// floor's rest and, dropping, falls for `drop` seconds. A pure function: every peer, the killcam and a late joiner
        /// evaluate the same frame for the same replicated fight start.
        /// </summary>
        public static SpawnInFrame Evaluate(float age,SpawnInMode mode,float hold,float drop,int seed,bool reduced)
        {
            var f=new SpawnInFrame{Age=age,Land=-1,Built=1,Front=BuildHeight};
            float landAt=mode==SpawnInMode.Drop?Math.Max(0,drop):0;
            if(!MathUtil.Finite(age)||age<StrikeAt-1e-4f||age>=landAt+LandSeconds)return f;
            f.Active=true;
            // The body: the front climbs from the feet; the tint holds through the build and cools after it.
            f.Built=Clamp01((age-BuildAt)/BuildSeconds);f.Front=age<BuildAt?-GhostLead-Band:FrontAt(f.Built);
            f.Tint=(age<BuiltAt?1:1-MathUtil.Smooth((age-BuiltAt)/TintFade))*(reduced?.6f:1);
            float flashU=(age-(BuiltAt-.04f))/FlashSeconds;f.Flash=flashU>=0&&flashU<1?(1-flashU)*(1-flashU)*(reduced?.35f:1):0;
            if(!reduced)
            {
                float splitU=(age-(BuiltAt-.04f))/SplitSeconds;if(splitU>=0&&splitU<1)f.Split=SplitMax*(1-splitU);
                float blipA=-1.25f+.3f*Hash(seed,0,1),blipB=-.62f+.2f*Hash(seed,0,2);
                if(age>=blipA&&age<blipA+BlipSeconds||age>=blipB&&age<blipB+BlipSeconds)f.Split=Math.Max(f.Split,BlipSplit);
            }
            // The hold, its bob, then the release: the drop after GO, or the lowering that lands at GO.
            float releaseStart=mode==SpawnInMode.Drop?0:-LowerSeconds;
            float bobEnvelope=Ease((age-BuiltAt)/BobIn)*(1-MathUtil.Smooth((age-(releaseStart-Charge))/Charge));
            float bob=age>BuiltAt?(reduced?ReducedBob:Bob)*(float)Math.Sin(Tau*(age-BuiltAt)/BobPeriod)*bobEnvelope:0;
            hold=Math.Max(0,hold);
            if(mode==SpawnInMode.Drop){if(age<0)f.Lift=hold+bob;else if(age<landAt){float u=age/landAt;f.Lift=hold*(1-u*u);}}
            else if(age<releaseStart)f.Lift=hold+bob;else if(age<0)f.Lift=hold*(1-MathUtil.Smooth((age-releaseStart)/LowerSeconds));
            f.Lift=Math.Max(0,f.Lift);f.Held=age<0;f.Falling=mode==SpawnInMode.Drop&&age>=0&&age<landAt;
            // The beam: the strike drives its bottom down onto the floor, the charge tightens and brightens it, GO retracts it.
            float strikeU=(age-StrikeAt)/StrikeSeconds,charge=Clamp01((age+Charge)/Charge);
            f.BeamTo=1;f.BeamFrom=strikeU<1?1-Clamp01(strikeU)*Clamp01(strikeU):0;
            f.Strike=strikeU<1?1:Math.Max(0,1-(age-StrikeEnd)/.18f);
            f.Beam=(strikeU<1?.75f:.6f+.35f*charge)*(reduced?.8f:1);f.BeamWidth=.42f-.16f*charge;f.Core=.8f+.4f*charge;
            if(age>=0)
            {
                float r=age/ReleaseSeconds;f.BeamFrom=Ease(r);f.Beam*=Math.Max(0,1-r);f.Core*=Math.Max(0,1-r);if(r>=1)f.Beam=f.Core=0;
                f.Release=age<ReleaseFlash?(1-age/ReleaseFlash)*(1-age/ReleaseFlash)*(reduced?.5f:1):0;
            }
            // The floor: the ring marking the spawn from the strike's impact (tightening in the charge), the tick pulses, the pool.
            if(age>=StrikeEnd&&age<0){f.Ring=(.6f+.35f*charge)*Ease((age-StrikeEnd)/.1f);f.RingRadius=RingRadiusHold+(RingRadiusCharge-RingRadiusHold)*MathUtil.Smooth(charge);}
            else if(age>=0&&age<.08f){f.Ring=.95f*(1-age/.08f);f.RingRadius=RingRadiusCharge;}
            Pulse(ref f,age,StrikeEnd,.2f,1.3f,.8f);Pulse(ref f,age,-2,RingRadiusHold,RingRadiusHold+.6f,.55f);Pulse(ref f,age,-1,RingRadiusHold,RingRadiusHold+.6f,.55f);
            if(age>=StrikeEnd)f.Pool=age<0?.3f+.15f*charge+.7f*Math.Max(0,1-(age-StrikeEnd)/.25f):.45f*Math.Max(0,1-age/ReleaseSeconds);
            f.Motes=age<StrikeEnd?0:age<0?Ease((age-StrikeEnd)/.25f):Math.Max(0,1-age/.25f);
            f.Scan=age<BuildAt||f.Built>=1?0:Math.Min(1,Math.Min(f.Built/.05f,(1-f.Built)/.05f));
            if(age>=landAt)f.Land=age-landAt;
            return f;
        }
        private static void Pulse(ref SpawnInFrame f,float age,float at,float from,float to,float opacity)
        {
            float u=(age-at)/PulseSeconds;if(u<0||u>=1)return;float a=opacity*(1-u)*(1-u);if(a<=f.Pulse)return;f.Pulse=a;f.PulseRadius=from+(to-from)*Ease(u);
        }

        // ---- the body, part by part (CapturedRig bones) ----
        // A limb part runs from its joint to the next one (its height: the midpoint); a leaf part sits PartOffsets above its joint.
        private static readonly int[] PartChildren={-1,-1,-1,-1,-1,-1,-1,8,9,-1,-1,12,13,-1,15,16,17,-1,19,20,21,-1};
        private static readonly float[] PartOffsets={-.2f,0,.075f,.13f,.03f,.17f,0,0,0,-.06f,0,0,0,-.06f,0,0,0,0,0,0,0,0};
        /// <summary>The joint a bone's part runs to (its height is the midpoint of the two), or -1 for a part at its joint.</summary>
        public static int PartChild(int bone)=>bone>=0&&bone<PartChildren.Length?PartChildren[bone]:-1;
        /// <summary>A part's height from its joint's and (for a limb) its child joint's world heights.</summary>
        public static float PartHeight(int bone,float jointY,float childY)=>PartChild(bone)>=0?(jointY+childY)*.5f:jointY+(bone>=0&&bone<PartOffsets.Length?PartOffsets[bone]:0);
        /// <summary>A part `part` m above the feet: solid (0..1) under the front, its hologram slice (0..1) ahead of the solid,
        /// its hardlight heat (1 just formed, 0 once the front is HeatSpan past).</summary>
        public static float Solid(float front,float part)=>MathUtil.Smooth((front-SolidLag-part)/Band);
        public static float Ghost(float front,float part)=>Clamp01((front+GhostLead-part)/Band)*(1-Solid(front,part));
        public static float Heat(float front,float part)=>1-Clamp01((front-SolidLag-Band-part)/HeatSpan);

        // ---- tiers ----
        /// <summary>Full (quality 1), Reduced effects (quality under .5: fewer motes, no glitch or shimmer, calmer), the low
        /// graphics tier (no hologram or dust, fewer pieces; rings from fewer segments).</summary>
        public static SpawnInTier Tier(float quality,bool low)
        {
            bool reduced=!(quality>=.5f);
            return new SpawnInTier{Motes=low?(reduced?4:8):reduced?8:22,Front=low?0:reduced?4:10,Dust=low?0:reduced?3:7,Sparks=low?4:reduced?5:12,Segments=low?20:32,
                Ghost=!low,Glitch=!reduced&&!low,Shimmer=!reduced,Gain=reduced?.75f:1};
        }

        // ---- the beam, the floor and the landing (one additive mesh) ----
        /// <summary>
        /// One pilot's beam, rings, pool, motes, scan ring and landing rings into `sink`. `feet`: the presented feet (the scan
        /// ring rides the front above them); `eye`, `right`, `up`: the camera (the column and the motes face it); `column`:
        /// false for the view pilot in first person (its own column would fill the camera). Returns the vertices written.
        /// </summary>
        public static int Draw(ISpawnInSink sink,in SpawnInFrame f,in SpawnInPlace p,Vector3 feet,Vector3 eye,Vector3 right,Vector3 up,in SpawnInTier tier,bool column)
        {
            if(sink==null||!f.Active)return 0;int written=0;float gain=tier.Gain;var rgb=p.Color;var hot=Vector3.Lerp(rgb,Vector3.One,.55f);
            float floor=p.Floor,span=Math.Max(.5f,p.Top-floor);var c=p.Centre;int seg=Math.Max(8,tier.Segments);
            // The column (camera-facing: a soft wide ribbon and a white-hot core) and the strike's head glow.
            if(column&&f.Beam>1e-3f)
            {
                var d=new Vector3(c.X-eye.X,0,c.Z-eye.Z);var side=d.LengthSquared()>1e-6f?Vector3.Normalize(new Vector3(d.Z,0,-d.X)):new Vector3(right.X,0,right.Z);
                if(side.LengthSquared()<1e-6f)side=Vector3.UnitX;else side=Vector3.Normalize(side);
                float shimmer=tier.Shimmer?1+.08f*(float)Math.Sin(f.Age*9.3f+p.Seed%17):1;
                float bottom=floor+span*f.BeamFrom,top=floor+span*f.BeamTo;
                written+=Column(sink,c,bottom,top,floor,span,side,f.BeamWidth,rgb,f.Beam*.38f*gain*shimmer,6);
                written+=Column(sink,c,bottom,top,floor,span,side,.05f,hot,f.Beam*f.Core*.9f*gain,6);
                if(f.Strike>1e-3f)written+=Billboard(sink,new Vector3(c.X,bottom+.15f,c.Z),right,up,.55f+.35f*(1-f.Strike),hot,f.Strike*.9f*gain);
                if(f.Release>1e-3f)written+=Column(sink,c,floor+span*f.BeamFrom,floor+span*Math.Min(1,f.BeamFrom+.45f),floor,span,side,.16f+.3f*(1-f.Release),hot,f.Release*gain,3);
            }
            // The floor: the hold ring, the tick pulse, the glow pool.
            float y=floor+.02f;
            if(f.Ring>1e-3f)written+=Annulus(sink,c,y,f.RingRadius-.05f,f.RingRadius+.05f,seg,hot,f.Ring*gain);
            if(f.Pulse>1e-3f)written+=Annulus(sink,c,y+.004f,f.PulseRadius-.04f,f.PulseRadius+.04f,seg,rgb,f.Pulse*gain);
            if(f.Pool>1e-3f)written+=Flat(sink,c,y-.006f,1.1f,rgb,f.Pool*.55f*gain);
            // The motes streaming up the beam (deterministic per pilot and index, a pure function of the age).
            if(f.Motes>1e-3f)for(int k=0;k<tier.Motes;k++)
            {
                float h1=Hash(p.Seed,k,11),h2=Hash(p.Seed,k,12),h3=Hash(p.Seed,k,13),h4=Hash(p.Seed,k,14);
                float rise=.7f+.9f*h1,climb=h2+(f.Age-StrikeAt)*rise/3.2f;climb-=(float)Math.Floor(climb);
                float angle=Tau*h3+(f.Age-StrikeAt)*(.6f+.8f*h4)*(h4>.5f?1:-1),radius=.12f+.33f*(float)Math.Sqrt(h4);
                var at=new Vector3(c.X+radius*(float)Math.Cos(angle),floor+.08f+climb*3.2f,c.Z+radius*(float)Math.Sin(angle));
                float fade=Math.Min(1,climb/.12f)*(1-MathUtil.Smooth((climb-.6f)/.4f));if(column||Vector3.DistanceSquared(at,eye)>.36f)
                written+=Billboard(sink,at,right,up,.035f+.04f*h1,hot,f.Motes*fade*.9f*gain);
            }
            // The scan ring and the voxel sparks at the build front.
            if(f.Scan>1e-3f)
            {
                float fy=feet.Y+Math.Min(f.Front,2.05f);var fc=new Vector3(c.X,fy,c.Z);
                written+=Annulus(sink,fc,fy,.27f,.45f,seg,hot,f.Scan*.85f*gain);
                for(int k=0;k<tier.Front;k++)
                {
                    float a=Tau*k/Math.Max(1,tier.Front)+f.Age*2.4f+Hash(p.Seed,k,21)*1.3f,r=.34f+.12f*Hash(p.Seed,k,22),j=(Hash(p.Seed,k,23)-.5f)*.08f;
                    var at=new Vector3(c.X+r*(float)Math.Cos(a),fy+j,c.Z+r*(float)Math.Sin(a));
                    if(column||Vector3.DistanceSquared(at,eye)>.36f)written+=Billboard(sink,at,right,up,.03f,hot,f.Scan*gain);
                }
            }
            // The landing: the impact ring running out, a fast inner ring and the flash on the floor (soft when lowered).
            if(f.Land>=0)
            {
                bool soft=p.Mode==SpawnInMode.Lower;float u=f.Land/.5f,v=f.Land/.28f,w=f.Land/.2f;
                if(u<1){float r=soft?.25f+.85f*Ease(u):.3f+1.5f*(1-(1-u)*(1-u)*(1-u)),half=.07f-.045f*u;written+=Annulus(sink,c,y+.008f,r-half,r+half,seg,hot,(soft?.45f:1)*(float)Math.Pow(1-u,1.5)*gain);}
                if(!soft&&v<1){float r=.15f+.85f*Ease(v);written+=Annulus(sink,c,y+.012f,r-.035f,r+.035f,seg,rgb,.8f*(1-v)*(1-v)*gain);}
                if(w<1)written+=Flat(sink,c,y-.004f,soft?.9f:1.3f,hot,(soft?.35f:.85f)*(1-w)*(1-w)*gain);
            }
            return written;
        }
        private static int Column(ISpawnInSink s,Vector3 c,float bottom,float top,float floor,float span,Vector3 side,float half,Vector3 rgb,float alpha,int rows)
        {
            if(alpha<=1e-3f||top-bottom<.01f)return 0;int pa=-1,pb=-1;
            for(int i=0;i<=rows;i++)
            {
                float y=bottom+(top-bottom)*i/rows,h=Clamp01((y-floor)/span),fade=(float)Math.Pow(1-h,1.4);var col=new Vector4(rgb,alpha*fade);var at=new Vector3(c.X,y,c.Z);
                int a=s.Vertex(at-side*half,new Vector3(-1,0,1),col),b=s.Vertex(at+side*half,new Vector3(1,0,1),col);
                if(i>0){s.Triangle(pa,pb,b);s.Triangle(pa,b,a);}pa=a;pb=b;
            }
            return (rows+1)*2;
        }
        private static int Annulus(ISpawnInSink s,Vector3 c,float y,float inner,float outer,int seg,Vector3 rgb,float alpha)
        {
            if(alpha<=1e-3f||outer<=inner)return 0;inner=Math.Max(0,inner);var col=new Vector4(rgb,alpha);int pa=-1,pb=-1;
            for(int i=0;i<=seg;i++)
            {
                float a=Tau*i/seg,x=(float)Math.Cos(a),z=(float)Math.Sin(a);
                int v0=s.Vertex(new Vector3(c.X+x*inner,y,c.Z+z*inner),new Vector3(-1,0,1),col),v1=s.Vertex(new Vector3(c.X+x*outer,y,c.Z+z*outer),new Vector3(1,0,1),col);
                if(i>0){s.Triangle(pa,pb,v1);s.Triangle(pa,v1,v0);}pa=v0;pb=v1;
            }
            return (seg+1)*2;
        }
        private static int Flat(ISpawnInSink s,Vector3 c,float y,float radius,Vector3 rgb,float alpha)
        {
            if(alpha<=1e-3f)return 0;var col=new Vector4(rgb,alpha);
            int a=s.Vertex(new Vector3(c.X-radius,y,c.Z-radius),new Vector3(-1,-1,0),col),b=s.Vertex(new Vector3(c.X+radius,y,c.Z-radius),new Vector3(1,-1,0),col);
            int d=s.Vertex(new Vector3(c.X+radius,y,c.Z+radius),new Vector3(1,1,0),col),e=s.Vertex(new Vector3(c.X-radius,y,c.Z+radius),new Vector3(-1,1,0),col);
            s.Triangle(a,b,d);s.Triangle(a,d,e);return 4;
        }
        private static int Billboard(ISpawnInSink s,Vector3 at,Vector3 right,Vector3 up,float size,Vector3 rgb,float alpha)
        {
            if(alpha<=1e-3f)return 0;var col=new Vector4(rgb,alpha);var r=right*size;var u=up*size;
            int a=s.Vertex(at-r-u,new Vector3(-1,-1,0),col),b=s.Vertex(at+r-u,new Vector3(1,-1,0),col),d=s.Vertex(at+r+u,new Vector3(1,1,0),col),e=s.Vertex(at-r+u,new Vector3(-1,1,0),col);
            s.Triangle(a,b,d);s.Triangle(a,d,e);return 4;
        }

        // ---- the landing's dust and sparks (the shared effects renderer) ----
        /// <summary>The landing dust's life (s): a light, low ring of puffs, never rubble.</summary>
        public const float DustLife=.6f;
        /// <summary>Dust puff `k` of `count` from a drop's touchdown (none for a lowered pilot): where, how big, how opaque.</summary>
        public static bool Dust(int k,int count,in SpawnInPlace p,float land,out Vector3 at,out float size,out float opacity)
        {
            at=default;size=opacity=0;if(p.Mode!=SpawnInMode.Drop||land<0||land>=DustLife||k<0||k>=count)return false;
            float u=land/DustLife,a=Tau*(k+Hash(p.Seed,k,31)*.6f)/count,reach=.3f+1f*Ease(u)*(.8f+.4f*Hash(p.Seed,k,32));
            at=new Vector3(p.Centre.X+reach*(float)Math.Cos(a),p.Floor+.07f+.1f*u,p.Centre.Z+reach*(float)Math.Sin(a));size=.24f+.3f*u;opacity=.24f*(float)Math.Pow(1-u,1.6);return opacity>1e-3f;
        }
        /// <summary>Hardlight spark `k` of `count` thrown low off the touchdown: its head, its velocity and its fade.</summary>
        public static bool Spark(int k,int count,in SpawnInPlace p,float land,out Vector3 head,out Vector3 velocity,out float fade)
        {
            head=velocity=default;fade=0;const float life=.35f;if(land<0||land>=life||k<0||k>=count)return false;
            float speed=(p.Mode==SpawnInMode.Drop?4.5f:2.2f)*(.7f+.6f*Hash(p.Seed,k,41)),a=Tau*(k+Hash(p.Seed,k,42))/count,lift=.6f+1.2f*Hash(p.Seed,k,43);
            var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));velocity=dir*speed+Vector3.UnitY*(lift-Gravity*.35f*land);
            head=new Vector3(p.Centre.X,p.Floor+.06f,p.Centre.Z)+dir*(.3f+speed*land)+Vector3.UnitY*Math.Max(0,lift*land-Gravity*.175f*land*land);fade=(1-land/life)*(1-land/life);return fade>1e-3f;
        }

        // ---- the local pilot's camera and screen ----
        /// <summary>The touchdown's camera kick for the local pilot (m down, and the pitch dip in degrees): a quick dip and a
        /// settle; a lowered pilot's is a third of a drop's. The caller scales it (camera shake off: none; Reduced: .4).</summary>
        public static float Kick(in SpawnInFrame f,SpawnInMode mode,out float pitch)
        {
            pitch=0;if(!f.Active||f.Land<0)return 0;float x=f.Land,d=x<.05f?x/.05f:x<.32f?(1-(x-.05f)/.27f)*(1-(x-.05f)/.27f):0;
            bool soft=mode==SpawnInMode.Lower;pitch=(soft?.3f:.9f)*d;return (soft?.015f:.045f)*d;
        }
        /// <summary>My own first-person spawn-in screen: gentle (the veil at most .32, the vignette at most .24).</summary>
        public static SpawnInScreen Screen(in SpawnInFrame f,bool reduced)
        {
            var s=new SpawnInScreen();if(!f.Active)return s;float age=f.Age;
            s.Line=f.Built;s.LineGlow=reduced?f.Scan*.5f:f.Scan;
            s.Veil=(age<BuiltAt?1:1-MathUtil.Smooth((age-BuiltAt)/.25f))*(reduced?.2f:.32f);
            float charge=Clamp01((age+Charge)/Charge);
            s.Vignette=age<0?(.14f+.1f*charge)*Ease((age-StrikeAt)/.2f):Math.Max(0,.24f*(1-age/.3f));if(reduced)s.Vignette*=.7f;
            s.Split=reduced?0:f.Split/SplitMax;
            s.Flash=age>=0&&age<.3f?(reduced?.1f:.22f)*(1-age/.3f)*(1-age/.3f):0;
            return s;
        }
    }
}
