using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>How hard a damaging hit staggers its target (presentation only; owner 2026-10-12: "more motion in getting
    /// hit. Small staggers back"): a flinch (daggers, pistol, lightning), a small step (sword, rifle, machine gun, the other
    /// schools, shield), a bigger stagger of up to two steps (greatsword, a heavy fighter strike, the Siren's claws, blasts,
    /// rockets, heavy magic and a shotgun-like volley of pellets in one tick).</summary>
    public enum StaggerWeight{None=-1,Flinch=0,Step=1,Heavy=2}

    /// <summary>One damaging hit on a pilot as the hit ledger read it from the frame's Hit / Headshot events (every peer, from
    /// replicated data only): when, how hard, whether it was a melee blow or a headshot, and the push direction in the Unity
    /// world (horizontal, unit; HasDir false when nothing told where it came from).</summary>
    public struct StaggerHit{public float At,Amount,Scale;public StaggerWeight Weight;public bool Head,Melee,HasDir,Long;public V3 Dir;public int Source,Serial,Chain;}

    /// <summary>
    /// The felt-hit rules (presentation only: never read by authority, no change to hit timing, hitboxes, damage, cooldowns,
    /// stuns, movement or the network frame). Pure functions, no allocation.
    /// <list type="bullet">
    /// <item>The hips are knocked along the blow by a critically damped spring: each hit adds an impulse sized so a lone hit
    /// from rest peaks at <see cref="Peak"/> about 1/omega after it, then returns (90% back within .4-.5 s). A hit landing
    /// soon after the last adds less (<see cref="Refractory"/>), so a stream of bullets leans the pilot back instead of
    /// stacking, and the shown offset is soft-limited to <see cref="MaxHip"/> (hard bound <see cref="HardHip"/>).</item>
    /// <item>The feet stay planted in the world: a foot that the hips or the authoritative push leave behind steps, lifting
    /// and planting; a step or heavy blow asks for one or two steps of its own (<see cref="Steps"/>).</item>
    /// <item>The upper body and head recoil along the blow on an impact envelope per hit (summed and soft-limited, so chained
    /// hits never pop); a headshot snaps the head.</item>
    /// </list>
    /// </summary>
    public static class NativeStaggerRules
    {
        /// <summary>The hips' shift (m): soft limit and hard bound.</summary>
        public const float MaxHip=.13f,HardHip=.15f;
        /// <summary>How long after a hit its record still matters (s).</summary>
        public const float Window=1.2f;
        public static float Peak(StaggerWeight w)=>w==StaggerWeight.Heavy?.11f:w==StaggerWeight.Step?.07f:w==StaggerWeight.Flinch?.03f:0;
        public static float Omega(StaggerWeight w)=>w==StaggerWeight.Heavy?9.2f:w==StaggerWeight.Step?10.4f:12f;
        public static int Steps(StaggerWeight w)=>w==StaggerWeight.Heavy?2:w==StaggerWeight.Step?1:0;
        /// <summary>The upper body's recoil per hit (relative to a sword blow).</summary>
        public static float Recoil(StaggerWeight w)=>w==StaggerWeight.Heavy?1.35f:w==StaggerWeight.Step?1:w==StaggerWeight.Flinch?.6f:0;
        /// <summary>A step's swing time and lift (m).</summary>
        public static float StepTime(StaggerWeight w)=>w==StaggerWeight.Heavy?.15f:.14f;
        public static float Lift(StaggerWeight w)=>w==StaggerWeight.Heavy?.075f:w==StaggerWeight.Step?.06f:.04f;
        /// <summary>How far past its new neutral a hit's own step lands, along the push (m): the step reads as a step back.</summary>
        public static float Overshoot(StaggerWeight w)=>w==StaggerWeight.Heavy?.05f:w==StaggerWeight.Step?.03f:0;
        /// <summary>A hit within <see cref="RefractorySeconds"/> of the last adds this share of its impulse (0 at once, 1 after).</summary>
        public const float RefractorySeconds=.35f;
        public static float Refractory(float sinceLast)=>!(sinceLast>0)?0:sinceLast>=RefractorySeconds?1:MeleeMath.Smooth(sinceLast/RefractorySeconds);
        /// <summary>A chain (hits from one attacker within <see cref="ChainSeconds"/> of the last: a combo, the Whirlwind's
        /// pulses, a burst): its later links add this share of their impulse, and only every <see cref="ChainStep"/>th asks for
        /// a step of its own, so a rapid string reads as one stagger that keeps going rather than a twitch per hit.</summary>
        public const float ChainSeconds=.6f;public const int ChainStep=3;
        public static float ChainShare(int chain)=>chain<=0?1:chain==1?.7f:.55f;
        public static bool ChainSteps(int chain)=>chain<=0||chain%ChainStep==0;

        /// <summary>
        /// The hit's weight from the attacker's weapon (null: a PvE add or no attacker), the Hit event's detail and its damage.
        /// Field ticks, thorns, the vent fall and structure collapses are no stagger (no direction or their own death scene).
        /// </summary>
        public static StaggerWeight Classify(string spec,string detail,float amount)
        {
            if(!(amount>=1)||float.IsInfinity(amount))return StaggerWeight.None;
            switch(detail)
            {
                case "field":case "thorns":case "stage-vent":case "collapse":return StaggerWeight.None;
                case "debris":case "chain":return StaggerWeight.Flinch;
                case "blast":return amount>=18?StaggerWeight.Heavy:StaggerWeight.Step;
            }
            if(detail!=null&&detail.StartsWith("stage-",StringComparison.Ordinal))return StaggerWeight.Flinch;
            bool ability=detail=="ability";
            switch(spec)
            {
                case "daggers":case "pistol":case "lightning":return ability&&amount>=18?StaggerWeight.Step:StaggerWeight.Flinch;
                // A greatsword ability's light ticks (the Whirlwind's 8.5-damage pulses) are steps; its heavy hits stagger.
                case "greatsword":return ability&&amount<15?StaggerWeight.Step:StaggerWeight.Heavy;
                case "siren":return StaggerWeight.Heavy;
                case "ansatsuken":return amount>=30?StaggerWeight.Heavy:StaggerWeight.Step;
                case "sword":case "shield":case "emperor":case "rifle":case "machinegun":return StaggerWeight.Step;
                case "fire":case "water":case "darkness":case "leet":return ability&&amount>=18||amount>=40?StaggerWeight.Heavy:StaggerWeight.Step;
                case null:return amount>=10?StaggerWeight.Step:StaggerWeight.Flinch;
                default:return StaggerWeight.Step;
            }
        }
        /// <summary>A volley of pellets from one gun in one tick (a shotgun-like spread) reads as one heavier blow.</summary>
        public static StaggerWeight Volley(StaggerWeight w,string spec,float total)=>w==StaggerWeight.None?w:NativeWeaponDressing.Gun(spec)&&total>=45?StaggerWeight.Heavy:w;
        /// <summary>The hit's strength within its weight, by its damage against that weight's typical damage.</summary>
        public static float Scale(StaggerWeight w,float amount)
        {
            float typical=w==StaggerWeight.Heavy?48:w==StaggerWeight.Step?26:14;
            return MeleeMath.Clamp(.55f+.45f*Finite(amount,typical)/typical,.7f,1.25f);
        }
        /// <summary>A critically damped spring from (x0, v0) after tau seconds: x(t) = (x0 + (v0 + w x0) t) e^-wt.</summary>
        public static void Spring(float x0,float v0,float omega,float tau,out float x,out float v)
        {
            if(!(tau>0)){x=x0;v=v0;return;}float e=(float)Math.Exp(-omega*tau),b=v0+omega*x0;x=(x0+b*tau)*e;v=(b-omega*(x0+b*tau))*e;
        }
        /// <summary>The impulse (m/s) that makes a lone hit from rest peak at `peak` (the peak of t e^-wt is 1/(w e)).</summary>
        public static float Impulse(float peak,float omega)=>peak*omega*(float)Math.E;
        /// <summary>A soft limit that keeps the curve's shape well below it, then the hard bound.</summary>
        public static Vector2 SoftLimit(Vector2 v,float soft,float hard)
        {
            float l=v.Length();if(!(l>1e-6f))return Vector2.Zero;float s=Math.Min(hard,soft*(float)Math.Tanh(l/soft)*(1+.04f*(float)Math.Tanh(l/soft)));return v*(Math.Min(s,l)/l);
        }
        public static float Finite(float v,float fallback)=>float.IsNaN(v)||float.IsInfinity(v)?fallback:v;
        /// <summary>The unit push direction (Unity world, horizontal) of a hit on a pilot at `target` from `from`, or false.</summary>
        // A replicated hit direction (Unity space) with a horizontal part: the push runs along it.
        public static bool Along(V3 n,out V3 dir)
        {
            dir=new V3(0,0,0);float l=(float)Math.Sqrt(n.x*n.x+n.z*n.z);if(!(l>1e-6f)||!(l<1e5f))return false;dir=new V3(n.x/l,0,n.z/l);return true;
        }
        public static bool Push(V3 target,V3 from,out V3 dir)
        {
            float x=target.x-from.x,z=target.z-from.z,l=(float)Math.Sqrt(x*x+z*z);dir=new V3(0,0,0);if(!(l>.05f)||!(l<1e5f))return false;dir=new V3(x/l,0,z/l);return true;
        }
    }

    /// <summary>
    /// One rig's felt-hit stagger (Core/NativeHitReact's layer runs it on every third-person route: captured melee, the DEV
    /// fighters, guns and magic). It reads only the hit ledger (the replicated Hit / Headshot / Explosion events and the
    /// attacker's replicated actor) and the struck pilot's replicated actor, and moves only the rig's bones and its root
    /// presentation (the hips), never the authoritative position, so hitboxes, timing and movement are untouched.
    /// <list type="bullet">
    /// <item>The hips: the spring of <see cref="NativeStaggerRules"/>, a pure function of the hit times (one segment per hit,
    /// each starting from the last one's state at its hit), so every peer and every frame rate shows the same offset.</item>
    /// <item>The feet (while grounded and stunned, or nearly still: a gait in motion owns its own feet): each is planted in the
    /// world; a foot left behind by the hips or by the authoritative push (more than <see cref="Drift"/>, or nearly out of
    /// reach) lifts and steps to where its neutral will be when it lands, so a planted foot never slides. A step or heavy
    /// blow asks for one or two steps of its own, landing a little past neutral along the push. Once the hips are back and
    /// the pilot is still, the feet step home and the route's own legs take over exactly where they stand.</item>
    /// <item>The upper body: per hit, an impact envelope along the blow (spine and chest bend away, the head snaps back or
    /// whips), summed over the recent hits and soft-limited; a headshot snaps the head on the neck.</item>
    /// </list>
    /// Stuns longer than <see cref="LongStun"/>, juggles, knockdowns, rolls, specials, blocks and deaths keep their own
    /// reactions (the layer skips those frames or a long stun's blow only flinches). No allocation per frame.
    /// </summary>
    public sealed class NativeStagger
    {
        public const float Drift=.085f,LegReach=.488f,StepReach=.47f,Home=.012f,HomeExact=.004f,LongStun=.45f,StillSpeed=1.6f,SettleQuiet=.32f,SkidSpeed=1.8f;
        private float feetTime=float.NaN;
        private struct Segment{public float At,X,Z,Vx,Vz,Omega;}
        private readonly Segment[] segments=new Segment[8];private int segmentCount;
        private struct Foot{public bool Swing;public Vector2 W,From,To;public float Y,T0,T1,Lift;}
        private readonly Foot[] feet=new Foot[2];private readonly Vector2[] stance=new Vector2[2];private readonly float[] stanceY=new float[2];
        private int epoch=-1,consumed=-1,id=int.MinValue;private float lastTime=float.NaN,lastHit=-100;
        private bool owned;private int pendingSteps;private float pendingAt;private Vector2 pendingPush;private StaggerWeight pendingWeight;private int lastStepFoot=-1;
        private readonly StaggerHit[] recent=new StaggerHit[NativeHitLedger.StaggerSlots];
        /// <summary>The last frame's shown hip offset (Unity world, m), the hip spring's raw offset, whether the layer owned the
        /// feet, each foot's swing and where it stands (source world), for the checks.</summary>
        public Vector2 HipWorld{get;private set;}
        public float HipShift=>HipWorld.Length();
        public bool OwnsFeet=>owned;
        public bool Swinging(int foot)=>feet[foot].Swing;
        public Vector2 FootAt(int foot)=>feet[foot].W;
        public int Steps{get;private set;}
        public bool Active{get;private set;}
        /// <summary>Gives the legs back at once (a skipped frame: dead, rolling, juggled, knocked down, in a special).</summary>
        public void Drop(){owned=false;pendingSteps=0;feet[0].Swing=feet[1].Swing=false;Placed=false;feetTime=float.NaN;}
        public void Reset(){feetTime=float.NaN;segmentCount=0;owned=false;pendingSteps=0;lastHit=-100;lastTime=float.NaN;feet[0]=feet[1]=default;lastStepFoot=-1;HipWorld=Vector2.Zero;Active=false;}

        /// <summary>Takes this pilot's new staggering hits from the ledger (in hit order) as spring segments.</summary>
        public void Observe(NativeHitLedger ledger,Actor a,float time)
        {
            if(ledger==null||a==null){Reset();return;}
            // Another pilot, a fresh ledger (a new arena, the killcam's replica) or a rewind: start over and re-read its hits.
            if(a.Id!=id||ledger.Epoch!=epoch||!float.IsNaN(lastTime)&&time<lastTime-.05f){Reset();id=a.Id;epoch=ledger.Epoch;consumed=-1;}
            lastTime=time;int n=ledger.Staggers(a.Id,recent);
            for(int i=0;i<n;i++)
            {
                var h=recent[i];if(h.Serial<=consumed)continue;consumed=h.Serial;if(h.Weight==StaggerWeight.None||segmentCount>0&&h.At<segments[segmentCount-1].At-1e-4f)continue;
                // A blow that starts a long stun keeps the stun's own reaction: it only flinches.
                var w=h.Long?StaggerWeight.Flinch:h.Weight;
                Eval(h.At,out float x,out float z,out float vx,out float vz);
                float share=NativeStaggerRules.Refractory(h.At-lastHit)*NativeStaggerRules.ChainShare(h.Chain),omega=NativeStaggerRules.Omega(w),k=NativeStaggerRules.Impulse(NativeStaggerRules.Peak(w)*h.Scale,omega)*share;
                // The spring runs in the source world (x mirrored) like the rig.
                float dx=h.HasDir?-h.Dir.x:0,dz=h.HasDir?h.Dir.z:0;
                if(segmentCount==segments.Length){Array.Copy(segments,1,segments,0,segments.Length-1);segmentCount--;}
                segments[segmentCount++]=new Segment{At=h.At,X=x,Z=z,Vx=vx+dx*k,Vz=vz+dz*k,Omega=omega};
                if(h.HasDir&&NativeStaggerRules.Steps(w)>0&&share>.38f&&NativeStaggerRules.ChainSteps(h.Chain)){pendingSteps=Math.Max(pendingSteps,NativeStaggerRules.Steps(w));pendingAt=h.At+.012f;pendingPush=new Vector2(dx,dz);pendingWeight=w;}
                lastHit=Math.Max(lastHit,h.At);
            }
        }
        // The spring at `time` (source world): the latest segment that has started.
        private void Eval(float time,out float x,out float z,out float vx,out float vz)
        {
            x=z=vx=vz=0;for(int i=segmentCount-1;i>=0;i--){var s=segments[i];if(s.At>time)continue;float tau=time-s.At;NativeStaggerRules.Spring(s.X,s.Vx,s.Omega,tau,out x,out vx);NativeStaggerRules.Spring(s.Z,s.Vz,s.Omega,tau,out z,out vz);return;}
        }
        /// <summary>The shown hip offset at `time` (source world, m), soft-limited.</summary>
        public Vector2 Hip(float time){Eval(time,out float x,out float z,out _,out _);return NativeStaggerRules.SoftLimit(new Vector2(x,z),NativeStaggerRules.MaxHip,NativeStaggerRules.HardHip);}
        /// <summary>Whether anything of a stagger shows at `time` (a recent hit, the hips not back yet, or feet still owned).</summary>
        public bool Wanted(float time)=>owned||segmentCount>0&&time>=segments[0].At&&time-lastHit<NativeStaggerRules.Window;

        private static Vector3 World(MeleeRig r,Vector3 local)=>Vector3.Transform(local,MeleeMath.AxisAngle(MeleeMath.Y,r.GroupYaw))+r.GroupPosition;
        private static Vector3 Local(MeleeRig r,Vector3 world)=>Vector3.Transform(world-r.GroupPosition,MeleeMath.AxisAngle(MeleeMath.Y,-r.GroupYaw));
        private static Vector2 Flat(Vector3 v)=>new Vector2(v.X,v.Z);

        /// <summary>
        /// The hips and the upper body, after the route and the hit layer's own accents: the hips shift along the spring (and dip
        /// a little), the spine, chest, neck and head recoil. `melee` scales the upper body down where the hit layer's melee
        /// accent and pack clips already recoil it. Returns the shown hip offset (local, group space).
        /// </summary>
        public Vector3 Body(MeleeRig r,Actor a,NativeHitLedger ledger,float time,float meleeShare)
        {
            Active=false;if(r==null||a==null||ledger==null)return Vector3.Zero;
            var hip=Hip(time);HipWorld=new Vector2(-hip.X,hip.Y);
            Vector3 local=Vector3.Transform(new Vector3(hip.X,0,hip.Y),MeleeMath.AxisAngle(MeleeMath.Y,-r.GroupYaw));float shift=hip.Length();
            Vector3 b=r.P[CapturedRig.Body];b.X+=local.X;b.Z+=local.Z;b.Y-=.22f*shift;r.P[CapturedRig.Body]=b;
            // The hips go first and the trunk lags over them: the pelvis tips the other way a little (pushed back, the body
            // folds forward over the planted feet; pushed aside, it leans back over them).
            r.AddE(CapturedRig.Body,-local.Z*.35f,0,local.X*.3f);
            // The upper body's recoil: each recent hit's envelope along its blow, in the rig's facing frame.
            int n=ledger.Staggers(a.Id,recent);float rx=0,rz=0,head=0,headSide=0;
            for(int i=0;i<n;i++)
            {
                var h=recent[i];if(h.Weight==StaggerWeight.None||!h.HasDir)continue;float age=time-h.At;if(!(age>=0)||age>.9f)continue;
                var w=h.Long?StaggerWeight.Flinch:h.Weight;float fall=.24f+.08f*(int)w;float e=NativeHitRules.Envelope(age,.024f,.016f,fall)*NativeStaggerRules.Recoil(w)*h.Scale*(h.Melee?meleeShare:1)*NativeStaggerRules.ChainShare(h.Chain);
                var d=Vector3.Transform(new Vector3(-h.Dir.x,0,h.Dir.z),MeleeMath.AxisAngle(MeleeMath.Y,-r.GroupYaw));rx+=d.X*e;rz+=d.Z*e;
                if(h.Head){float eh=NativeHitRules.Envelope(age-.006f,.018f,.012f,.22f);head+=d.Z*eh;headSide+=d.X*eh;}
            }
            float l=(float)Math.Sqrt(rx*rx+rz*rz);if(l>1e-5f){float s=1.6f*(float)Math.Tanh(l/1.6f)/l;rx*=s;rz*=s;}
            float front=Math.Max(0,-rz),back=Math.Max(0,rz);
            // Pushed back (a blow from the front): the chest and head go back first; from behind the body lurches and the head
            // whips back; from a side the trunk bends away. Matches the melee accent's signs (Core/NativeHitReact Impact).
            r.AddE(CapturedRig.Spine,(back*.08f-front*.05f),0,-rx*.06f);
            r.AddE(CapturedRig.Chest,(-front*.055f-back*.035f),rx*.04f,-rx*.05f);
            r.AddE(CapturedRig.Neck,(-front*.035f-back*.06f),0,-rx*.03f);
            r.AddE(CapturedRig.Head,(-front*.07f-back*.1f),0,-rx*.1f);
            float arm=(-front*.09f+back*.07f);r.Premultiply(CapturedRig.LClav,MeleeMath.Euler(arm,0,0));r.Premultiply(CapturedRig.RClav,MeleeMath.Euler(arm,0,0));
            if(head!=0||headSide!=0){r.AddE(CapturedRig.Neck,head*.14f,0,0);r.AddE(CapturedRig.Head,head*.3f,0,-headSide*.26f);}
            Active=shift>1e-4f||l>1e-4f||head!=0;
            return new Vector3(local.X,-.22f*shift,local.Z);
        }

        /// <summary>
        /// The feet: `ankle`, `footQ` and `knee` are where the route placed each ankle (group space) before any layer moved the
        /// body; `stunned` holds the route's gait planted. Owns the feet while grounded and (stunned or slower than
        /// <see cref="StillSpeed"/>) from a hit until they are home again, and solves both legs. False when it leaves the legs
        /// to the caller.
        /// </summary>
        /// <summary>Whether this frame's feet were placed by the stagger (Replant puts them back after the route's transitions).</summary>
        public bool Placed{get;private set;}
        private readonly Vector3[] placedAt=new Vector3[2],placedKnee=new Vector3[2];private readonly Quaternion[] placedQ=new Quaternion[2];
        public void Replant(MeleeRig r){if(!Placed||r==null)return;for(int i=0;i<2;i++)r.SolveLeg(i==0,placedAt[i],0,0,0,placedQ[i],placedKnee[i]);}
        public bool Feet(MeleeRig r,Actor a,float time,Vector3[] ankle,Quaternion[] footQ,Vector3[] knee,bool stunned)
        {
            Steps=0;Placed=false;if(r==null||a==null){owned=false;return false;}
            float speed=(float)Math.Sqrt(a.Velocity.x*a.Velocity.x+a.Velocity.z*a.Velocity.z);bool grounded=a.Grounded;
            bool want=grounded&&(stunned||speed<StillSpeed)&&(Wanted(time)||owned);
            if(!want){owned=false;pendingSteps=0;feetTime=float.NaN;return false;}
            var hip=Hip(time);Vector2 rootVel=new Vector2(-a.Velocity.x,a.Velocity.z);
            // The stance the route stood in when the stagger took the feet (group space): a gait route's own feet follow the
            // ankles placed here (MeleeGait.SyncFeet), so its later frames are not a neutral to step back to.
            Vector2 Neutral(int i,float at)
            {
                var h=Hip(at);var n=Flat(World(r,new Vector3(stance[i].X,stanceY[i],stance[i].Y)));return n+rootVel*(at-time)+h;
            }
            if(!owned)
            {
                owned=true;lastStepFoot=-1;for(int i=0;i<2;i++){stance[i]=new Vector2(ankle[i].X,ankle[i].Z);stanceY[i]=Math.Max(MeleeRig.FLOOR,Math.Min(ankle[i].Y,MeleeRig.FLOOR+.02f));}
                for(int i=0;i<2;i++)
                {
                    var w=World(r,ankle[i]);feet[i]=new Foot{W=Flat(w),Y=ankle[i].Y};
                    // A foot caught in the air by the hit lands where its neutral will be.
                    if(ankle[i].Y>MeleeRig.FLOOR+.02f){var to=Neutral(i,time+.1f);feet[i].Swing=true;feet[i].From=feet[i].W-(to-feet[i].W);feet[i].T0=time-.1f;feet[i].T1=time+.1f;feet[i].To=to;feet[i].Lift=Math.Min(.08f,ankle[i].Y-stanceY[i]);}
                }
            }
            // Land finished swings.
            for(int i=0;i<2;i++)if(feet[i].Swing&&time>=feet[i].T1){feet[i].Swing=false;feet[i].W=feet[i].To;}
            // A shove faster than steps can follow (the authority's knockback past SkidSpeed): the planted feet skid with the
            // excess, so the steps only have to make up SkidSpeed and the legs never stretch out behind the push.
            float dt=float.IsNaN(feetTime)||time<feetTime?0:Math.Min(.1f,time-feetTime);feetTime=time;
            if(speed>SkidSpeed&&dt>0){var skid=rootVel*((speed-SkidSpeed)/speed*dt);for(int i=0;i<2;i++)if(!feet[i].Swing)feet[i].W+=skid;else{feet[i].From+=skid;feet[i].To+=skid;}}
            float Drifted(int i)=>(feet[i].W-Neutral(i,time)).Length();
            Vector2 HomeAt(int i,float at)=>Flat(World(r,new Vector3(stance[i].X,stanceY[i],stance[i].Y)))+rootVel*(at-time);
            float FromHome(int i)=>(feet[i].W-HomeAt(i,time)).Length();
            // Once the hips are on their way back (a little after the last hit), steps go home to the stance itself.
            bool homeward=time-lastHit>.22f&&pendingSteps==0;
            // How far (3D, group space) the leg must reach to a foot at `w`: past LegReach it cannot (the IK would drag it).
            float Reach3(int i,Vector2 w){var at=Local(r,new Vector3(w.X,0,w.Y));at.Y=stanceY[i];return Vector3.Distance(r.HipInGroup(i==0),at);}
            float FromHip(int i)=>Reach3(i,feet[i].W);
            // A target the leg can reach as it lands: pulled toward the hip over the floor until it is within StepReach.
            Vector2 Reachable(int i,Vector2 w)
            {
                var hipAt=r.HipInGroup(i==0);var hw=Flat(World(r,hipAt));float vertical=Math.Abs(hipAt.Y-stanceY[i]);
                float flat=vertical<StepReach?(float)Math.Sqrt(StepReach*StepReach-vertical*vertical):0;var d=w-hw;float l=d.Length();return l>flat&&l>1e-5f?hw+d*(flat/l):w;
            }
            bool any=feet[0].Swing||feet[1].Swing;
            // A second foot may leave before the first lands only while the push carries the body faster than steps follow.
            bool overlap=speed>1.2f&&(feet[0].Swing?(time-feet[0].T0)/(feet[0].T1-feet[0].T0)>.45f:true)&&(feet[1].Swing?(time-feet[1].T0)/(feet[1].T1-feet[1].T0)>.45f:true);
            int pick=-1;StaggerWeight weight=StaggerWeight.Step;float over=0;Vector2 push=Vector2.Zero;bool recovery=false;
            if(!any||overlap)
            {
                float d0=feet[0].Swing?-1:Drifted(0),d1=feet[1].Swing?-1:Drifted(1);
                float r0=feet[0].Swing?-1:FromHip(0),r1=feet[1].Swing?-1:FromHip(1);
                // Out of reach (the push outran the stance): that foot must go now.
                if(r0>LegReach||r1>LegReach)pick=r0>=r1?0:1;
                else if(pendingSteps>0&&time>=pendingAt)
                {
                    // The blow's own step: the foot on the push side (source frame +x is the rig's left), else the one left
                    // furthest behind, never the same foot twice running.
                    var side=Vector3.Transform(new Vector3(pendingPush.X,0,pendingPush.Y),MeleeMath.AxisAngle(MeleeMath.Y,-r.GroupYaw));
                    int want0=Math.Abs(side.X)>.35f?(side.X>0?0:1):(d0>=d1?0:1);if(want0==lastStepFoot&&pendingSteps<2)want0=1-want0;if(feet[want0].Swing)want0=1-want0;
                    if(!feet[want0].Swing){pick=want0;weight=pendingWeight;over=NativeStaggerRules.Overshoot(weight);push=pendingPush;pendingSteps--;pendingAt=time+StepGap(weight);}
                }
                else if(Math.Max(d0,d1)>Drift)pick=d0>=d1?0:1;
                else if(!any&&time-lastHit>SettleQuiet&&hip.Length()<.02f&&speed<.5f&&Math.Max(FromHome(0),FromHome(1))>Home){pick=FromHome(0)>=FromHome(1)?0:1;recovery=true;}
            }
            if(pick>=0&&!feet[pick].Swing)
            {
                float dur=recovery?.16f:speed>2.5f?.11f:NativeStaggerRules.StepTime(weight),lift=recovery?.035f:NativeStaggerRules.Lift(weight);
                // A step home lands exactly in the stance it left (the hips' last few millimetres are not chased).
                var to=recovery||homeward&&over==0?HomeAt(pick,time+dur):Neutral(pick,time+dur)+push*over;var from=feet[pick].W;var delta=to-from;float len=delta.Length();if(len>.42f)to=from+delta*(.42f/len);to=Reachable(pick,to);
                feet[pick]=new Foot{Swing=true,W=from,From=from,To=to,Y=ankle[pick].Y,T0=time,T1=time+dur,Lift=lift};lastStepFoot=pick;
            }
            // Home: both planted where the route stands, nothing pending, the hips back: the route's legs again.
            if(!feet[0].Swing&&!feet[1].Swing&&pendingSteps==0&&time-lastHit>SettleQuiet&&hip.Length()<.004f&&FromHome(0)<=HomeExact&&FromHome(1)<=HomeExact)
            {owned=false;return false;}
            for(int i=0;i<2;i++)
            {
                // Heights from the stance it stood in (a gait route's own ankles follow these placements, lifts included).
                var f=feet[i];Vector2 at=f.W;float y=stanceY[i];
                if(f.Swing){float u=MeleeMath.Clamp((time-f.T0)/Math.Max(1e-3f,f.T1-f.T0),0,1);at=Vector2.Lerp(f.From,f.To,MeleeMath.Ease(u));y+=f.Lift*(float)Math.Sin(Math.PI*u);Steps++;}
                var w=new Vector3(at.X,0,at.Y);var local=Local(r,w);local.Y=Math.Max(y,MeleeRig.FLOOR);
                r.SolveLeg(i==0,local,0,0,0,footQ[i],knee[i]);placedAt[i]=local;placedQ[i]=footQ[i];placedKnee[i]=knee[i];
            }
            Placed=true;return true;
        }
        private static float StepGap(StaggerWeight w)=>w==StaggerWeight.Heavy?.1f:.14f;
    }
}
