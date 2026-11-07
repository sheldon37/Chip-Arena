using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What smashed a body (<see cref="NativeSmashGib"/>): Cyber City's maglev, a Factory die press, the Factory crane's load.</summary>
    public enum SmashCause : byte { None, Train, Press, Crane }

    /// <summary>
    /// One smash, everything a client needs to present it (Unity world, System.Numerics), built from replicated data only: the
    /// death record (kind, time, place, yaw, seed), the map's hazards, the round and FightAt. A body is either pinned (crushed
    /// flat where it stood under a press ram or the crane's load) or thrown (hit by the train, or clipped by a crusher's edge).
    /// </summary>
    public sealed class SmashPlan
    {
        public SmashCause Cause;public uint Seed;
        /// <summary>Crushed flat where it stood (a press ram or the crane's load coming down over the body); false: thrown.</summary>
        public bool Pinned;
        /// <summary>The body centre at death and its yaw (degrees, NativeDeath.yaw).</summary>
        public Vector3 Body;public float Yaw;
        /// <summary>Unit: the way a thrown body, its fragments and its blood go (the train's travel, out from under a crusher's
        /// edge); straight down when pinned.</summary>
        public Vector3 Travel;
        /// <summary>The crusher's speed at the body when it struck (m/s).</summary>
        public float Speed;
        /// <summary>The surface the body is crushed onto (the press bed, the crane zone's floor, the track bed) and the body's top.</summary>
        public float Floor,Top;
        /// <summary>Pinned: seconds after the death the crusher is down on the floor (the squash is complete). Thrown: the
        /// hit-stop the body holds for before it flies.</summary>
        public float Contact;
        /// <summary>The crusher's footprint at the strike: centre and half extents (x, z); zero for the train.</summary>
        public Vector3 Center,Half;
        /// <summary>The pinned sprawl: +1 the body goes down on its back, -1 on its face.</summary>
        public int Fall;
        /// <summary>Thrown: the body's speed along <see cref="Travel"/> (m/s) and the speed its blood and fragments are carried at.</summary>
        public float Throw,Carry;
        /// <summary>Thrown: the ragdoll bodies crushed to fragments (NativeBisect.Bodies joints), the head first when it is one.</summary>
        public int Crushed;public readonly int[] Crush=new int[NativeSmashGib.MaxCrush];
        /// <summary>A Factory crusher, its round and its hazard clock at the death (its underside is a pure function of these).</summary>
        public FactoryHazard Hazard;public int Round;public double Clock;
        public bool Crushes(int body){for(int i=0;i<Crushed;i++)if(Crush[i]==body)return true;return false;}
    }

    /// <summary>
    /// The smash gib (owner 2026-10-14: "Need to add a "smash" gib, something that can be used when getting hit by the train on
    /// cyber city, or smashed from things on factory"). Engine free, deterministic, presentation only: no hit, damage, timing,
    /// network or hazard rule reads it, and no new death kind exists for it. A smash is derived from a death record the
    /// authority already replicates:
    /// <list type="bullet">
    /// <item>"maglev": Cyber City's train (<see cref="Train"/>). The body takes a hit-stop, then flies along the train's
    /// travel with several parts crushed to fragments; the blood bursts along the travel and streaks the track bed.</item>
    /// <item>"hazard" on the Factory: the hazard that killed is the one the record names (NativeDeath.hazardId); an older frame
    /// without it is reconstructed as the authority found it (<see cref="Killer"/>: the first lethal hazard touching the body
    /// in map order on the death's tick or the one before, else the nearest lethal one). A
    /// press ram or the crane's load coming down over the body pancakes it (<see cref="Squash"/>, <see cref="Sprawl"/>) in the
    /// crusher's own timing, blood squirting out from under the crusher's edges and pooling round it; a body only clipped by a
    /// crusher's edge (or struck while it rose) is thrown out from under it with a part or two crushed. Saws keep the bisect
    /// (NativeSawBisect); vents and everything else keep the current death.</item>
    /// </list>
    /// After the plan nothing allocates: every moment is a pure function of the plan and the age.
    /// </summary>
    public static class NativeSmashGib
    {
        public const int MaxCrush=4;
        /// <summary>The body's top above its centre (m), its pinned flat height share, how far it spreads, and the gap kept
        /// over the floor (no z-fighting).</summary>
        public const float BodyTop=.93f,Flat=.085f,SpreadMax=1.3f,Gap=.006f;
        /// <summary>How far (m) a body's centre may stand outside a crusher's footprint and still be pinned under it.</summary>
        public const float EdgeMargin=.2f;
        /// <summary>The train: hit-stop (s), how fast the body flies and the most the blood and fragments are carried at (m/s).</summary>
        public const float TrainHitch=.05f,MaxThrow=24,MaxCarry=30;
        /// <summary>A crusher's edge: hit-stop (s), throw and carry (m/s).</summary>
        public const float EdgeHitch=.03f,EdgeThrow=3.6f,EdgeCarry=4;
        /// <summary>How long after the death a hazard death's last wound must have been made to be its own (s): older, it is
        /// a stale wound from before (stage hazards make none).</summary>
        public const float StaleSlack=.02f;
        private static float H(uint seed,int i,int k)=>(float)NativeWoundCarve.Hash(seed,i,k);
        private static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        private static float Clamp(float v,float lo,float hi)=>v<lo?lo:v>hi?hi:v;
        public static uint Mix(int seed)=>unchecked((uint)seed*2654435761u^0x51ed270bu);

        /// <summary>
        /// The hazard gib gate, for the smash and the Factory saws' split alike: gore on, a pilot's own body, not a revivable
        /// PvE body, and not a Siren or vent ending (siren-dissipate, siren-crush, vent-fall, vent-splat). Owner decisions
        /// 2026-10-14 ("Keep the smash even with kill effects", "Always show gib in saws too"): any other style gibs, a purchased
        /// kill effect from a credited pusher included, and the presentation draws the gib instead of that effect's body (the
        /// replicated style, the kill feed and the credit are unchanged). It reads no kill-effect style by name, so it holds
        /// whatever kill effects exist. A melee cut keeps its own stock-ending gate (NativeBisect.Applies).
        /// </summary>
        public static bool Applies(bool gore,string style,bool kept,string model)=>gore&&!kept&&(model??"pilot")=="pilot"&&!Excluded(style);
        /// <summary>The endings a smash never replaces: the Siren's own and the Ward Zero vents' scripted falls.</summary>
        public static bool Excluded(string style)=>style=="siren-dissipate"||style=="siren-crush"||style=="vent-fall"||style=="vent-splat";
        /// <summary>A stage-hazard death (the authority's kinds that make no wound of their own).</summary>
        public static bool StageDeath(string damageKind)=>!string.IsNullOrEmpty(damageKind)&&NativeStageHazards.DeathKind(damageKind);
        /// <summary>A stage-hazard death's recorded wound that is older than the death: the last wound from an earlier hit, which
        /// must not bleed (or crush) again as the death's own.</summary>
        public static bool Stale(string damageKind,float deathAt,NativeWound w)=>w!=null&&StageDeath(damageKind)&&!(w.at>=deathAt-StaleSlack);

        // ================================================================================================ which hazard
        /// <summary>
        /// The Factory hazard that killed a pilot whose record says a stage hazard killed it at `deathAt` (match time) with its
        /// body centre at `body`, or -1. The authority's order: the first hazard live and touching the body in map order on the
        /// death's tick (or the tick before) kills (a bursting vent counts: its damage can kill); failing that (the record's float
        /// time), the nearest lethal hazard within <see cref="NativeSawBisect.Reach"/>. NativeSawBisect.Find is this for saws.
        /// </summary>
        public static int Killer(FactoryHazard[] hazards,int round,double fightAt,double deathAt,V3 body)
        {
            if(hazards==null||double.IsNaN(deathAt))return -1;double clock=NativeFactoryHazards.Clock(NativePhase.Fight,fightAt,deathAt);
            for(int pass=0;pass<2;pass++)
            {
                double t=clock-pass/60.0;
                for(int i=0;i<hazards.Length;i++)
                {
                    var h=hazards[i];if(h==null)continue;var s=NativeFactoryHazards.State(h,round,t);
                    if(!NativeSawBisect.Box(h,s,out var c,out var size))continue;if(NativeStageHazards.Touch(body,c,size))return i;
                }
            }
            int best=-1;double near=NativeSawBisect.Reach;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(h==null||!h.lethal||h.kind==NativeFactoryHazards.Vent)continue;
                for(int pass=0;pass<3;pass++)
                {
                    var s=NativeFactoryHazards.State(h,round,clock+(pass-1)/60.0);if(!NativeSawBisect.Box(h,s,out var c,out var size))continue;
                    double dx=Math.Max(0,Math.Abs(body.x-c.x)-size.x/2),dy=Math.Max(0,Math.Abs(body.y-c.y)-size.y/2-.43),dz=Math.Max(0,Math.Abs(body.z-c.z)-size.z/2),d=Math.Sqrt(dx*dx+dy*dy+dz*dz);
                    if(d<near){near=d;best=i;}
                }
            }
            return best;
        }
        /// <summary>The hazard a death record names (NativeDeath.hazardId, the authority's own: index into `hazards`), -1 when it
        /// names none of them, -2 when the record carries no name (an older frame: reconstruct it, <see cref="Killer"/>).</summary>
        public static int Named(FactoryHazard[] hazards,string hazardId)
        {
            if(string.IsNullOrEmpty(hazardId))return -2;if(hazards==null)return -1;
            for(int i=0;i<hazards.Length;i++)if(hazards[i]!=null&&hazards[i].id==hazardId)return i;return -1;
        }
        /// <summary>The Factory saw a hazard death was made by (its index), or -1: the one the record names (a named hazard
        /// that is not a saw: -1), else NativeSawBisect.Find from the pure clock (older frames).</summary>
        public static int Saw(FactoryHazard[] hazards,string hazardId,int round,double fightAt,double deathAt,V3 body)
        {
            int named=Named(hazards,hazardId);if(named!=-2)return named>=0&&NativeSawBisect.Saw(hazards[named])?named:-1;
            return NativeSawBisect.Find(hazards,round,fightAt,deathAt,body);
        }
        /// <summary>The Factory hazard that killed: the record's named one, else reconstructed from the pure clock.</summary>
        public static int Find(FactoryHazard[] hazards,string hazardId,int round,double fightAt,double deathAt,V3 body)
        {int named=Named(hazards,hazardId);return named!=-2?named:Killer(hazards,round,fightAt,deathAt,body);}
        /// <summary>The smash a Factory hazard makes (Press, Crane), None for a saw (the bisect), a vent or anything else.</summary>
        public static SmashCause CauseOf(FactoryHazard h)=>h==null?SmashCause.None:h.kind==NativeFactoryHazards.Press?SmashCause.Press:h.kind==NativeFactoryHazards.Crane?SmashCause.Crane:SmashCause.None;
        /// <summary>
        /// The smash a death makes, or null: the gate (<see cref="Applies"/>), then "maglev" (the map's maglev: the one the record
        /// names, else the first) or "hazard" on the Factory (<see cref="Factory"/>). Everything else (a saw, a vent, a grid, a
        /// collapse, any other death) is null and keeps its death as it was.
        /// </summary>
        public static SmashPlan Plan(bool gore,string style,bool kept,string model,string damageKind,string mapId,MapHazard[] hazards,string hazardId,int round,double fightAt,double deathAt,Vector3 body,Vector3 direction,float yaw,int seed)
        {
            if(!Applies(gore,style,kept,model)||hazards==null)return null;
            if(damageKind=="maglev")
            {
                MapHazard train=null;foreach(var h in hazards)if(NativeCyberCityHazards.IsMaglev(h)&&(train==null||!string.IsNullOrEmpty(hazardId)&&h.id==hazardId))train=h;
                return Train(train,round,fightAt,deathAt,body,direction,yaw,seed);
            }
            return damageKind=="hazard"&&NativeFactoryHazards.Active(mapId)?Factory(hazards,hazardId,round,fightAt,deathAt,body,yaw,seed):null;
        }
        /// <summary>A crusher's underside at its hazard clock (the press ram's, the crane load's).</summary>
        public static float Underside(FactoryHazard h,int round,double clock)=>(float)NativeFactoryHazards.State(h,round,clock).LoadY;

        // ================================================================================================ the plans
        /// <summary>
        /// The smash of a Factory hazard death (null: not a press or crane, or no hazard found). `hazardId`: NativeDeath.hazardId
        /// (null or empty on older frames); `body`: NativeDeath.position; `yaw`: NativeDeath.yaw; `seed`: NativeDeath.seed.
        /// </summary>
        public static SmashPlan Factory(FactoryHazard[] hazards,string hazardId,int round,double fightAt,double deathAt,Vector3 body,float yaw,int seed)
        {
            int index=Find(hazards,hazardId,round,fightAt,deathAt,new V3(body.X,body.Y,body.Z));if(index<0)return null;var h=hazards[index];var cause=CauseOf(h);if(cause==SmashCause.None)return null;
            double clock=NativeFactoryHazards.Clock(NativePhase.Fight,fightAt,deathAt);var s=NativeFactoryHazards.State(h,round,clock);
            var p=new SmashPlan{Cause=cause,Seed=Mix(seed),Body=body,Yaw=yaw,Hazard=h,Round=round,Clock=clock};
            if(cause==SmashCause.Press){p.Floor=h.b.y-h.size.y/2;p.Center=new Vector3(s.Center.x,0,s.Center.z);}
            else
            {
                int z=s.Zone>=0&&h.zones!=null&&s.Zone<h.zones.Length?s.Zone:-1;p.Floor=z>=0?h.zones[z].center.y:body.Y-.85f;p.Center=new Vector3((float)s.X,0,(float)s.Z);
            }
            p.Half=new Vector3(h.size.x/2,0,h.size.z/2);p.Top=Math.Max(p.Floor+.4f,body.Y+BodyTop);
            p.Speed=Math.Max(0,(Underside(h,round,clock-.01)-Underside(h,round,clock+.01))/.02f);if(float.IsNaN(p.Speed)||float.IsInfinity(p.Speed))p.Speed=0;
            bool coming=s.Phase==NativeFactoryHazards.Slamming||s.Phase==NativeFactoryHazards.Falling;float outside=Outside(p,body);
            p.Pinned=coming&&outside<=EdgeMargin;
            if(p.Pinned){p.Travel=-Vector3.UnitY;p.Fall=H(p.Seed,0,1)<.6f?1:-1;p.Contact=Down(h,round,clock,p.Floor);}
            else
            {
                // Clipped by an edge (or struck as it rose): thrown out from under it, the side under the crusher crushed.
                var away=new Vector3(body.X-Clamp(body.X,p.Center.X-p.Half.X,p.Center.X+p.Half.X),0,body.Z-Clamp(body.Z,p.Center.Z-p.Half.Z,p.Center.Z+p.Half.Z));
                if(away.LengthSquared()<1e-6f)away=new Vector3(body.X-p.Center.X,0,body.Z-p.Center.Z);
                p.Travel=CapturedRig.Unit(away,Forward(yaw+180));p.Contact=EdgeHitch;p.Throw=EdgeThrow;p.Carry=EdgeCarry;
                p.Crushed=CrushSet(p.Seed,yaw,-p.Travel,.6f,H(p.Seed,0,2)<.5f?1:2,p.Crush);
            }
            return p;
        }
        /// <summary>The smash of a "maglev" death: `h` the map's maglev, `direction` NativeDeath.direction (its sign along the
        /// track when the hazard's own state has no pass).</summary>
        public static SmashPlan Train(MapHazard h,int round,double fightAt,double deathAt,Vector3 body,Vector3 direction,float yaw,int seed)
        {
            if(h==null)return null;var st=NativeCyberCityHazards.Evaluate(h,round,fightAt,deathAt);var axis=CapturedRig.Unit(new Vector3(h.axis.x,0,h.axis.z),Vector3.UnitX);
            int d=st.Direction!=0?st.Direction:Vector3.Dot(direction,axis)>=0?1:-1;float speed=h.speed>0?h.speed:44;
            var p=new SmashPlan{Cause=SmashCause.Train,Seed=Mix(seed),Body=body,Yaw=yaw,Travel=axis*d,Speed=speed,Floor=h.center.y-h.size.y/2,Top=body.Y+BodyTop,Contact=TrainHitch};
            // A body above the cut (the record's float place) lies on its own floor.
            float feet=body.Y-.85f;if(feet<p.Floor-.3f||feet>p.Floor+1.2f)p.Floor=feet;
            p.Throw=Math.Min(MaxThrow,speed*.5f);p.Carry=Math.Min(MaxCarry,speed*.6f);
            p.Crushed=CrushSet(p.Seed,yaw,-p.Travel,0,H(p.Seed,0,3)<.35f?4:3,p.Crush);
            return p;
        }
        private static Vector3 Forward(float yaw){double r=yaw*Math.PI/180;return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        private static Vector3 Right(float yaw){double r=yaw*Math.PI/180;return new Vector3((float)Math.Cos(r),0,-(float)Math.Sin(r));}
        /// <summary>How far (m, horizontal) `point` lies outside a crusher's footprint (0 inside).</summary>
        public static float Outside(SmashPlan p,Vector3 point)
        {
            float dx=Math.Max(0,Math.Abs(point.X-p.Center.X)-p.Half.X),dz=Math.Max(0,Math.Abs(point.Z-p.Center.Z)-p.Half.Z);return (float)Math.Sqrt(dx*dx+dz*dz);
        }
        // The first moment from the death the crusher's underside is down on the floor (at most 1.5 s on).
        private static float Down(FactoryHazard h,int round,double clock,float floor)
        {
            for(int i=0;i<=360;i++){double t=i/240.0;if(Underside(h,round,clock+t)<=floor+.01f)return (float)t;}return 1.5f;
        }
        /// <summary>The ragdoll bodies a strike from `toward` (unit, from the body to what hit it) crushes: `count` of the head,
        /// upper arms and thighs, the most exposed first (their place in the body's frame: yaw only, so every peer agrees), a
        /// seeded share of chance; `up` weights the higher parts (a crusher's edge from above). The head goes first when taken.</summary>
        public static int CrushSet(uint seed,float yaw,Vector3 toward,float up,int count,int[] into)
        {
            count=Math.Max(0,Math.Min(Math.Min(count,MaxCrush),into?.Length??0));if(count==0)return 0;
            Vector3 f=Forward(yaw),r=Right(yaw);toward=CapturedRig.Unit(toward,-f);
            Span<int> parts=stackalloc int[]{CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LHip,CapturedRig.RHip};
            Span<float> score=stackalloc float[5];
            for(int i=0;i<5;i++)
            {
                // The rig's left is the source's +x, Unity's -x: left parts lie at -right.
                Vector3 o=i==0?new Vector3(0,.75f,0):i==1?-r*.34f+Vector3.UnitY*.35f:i==2?r*.34f+Vector3.UnitY*.35f:i==3?-r*.13f-Vector3.UnitY*.42f:r*.13f-Vector3.UnitY*.42f;
                score[i]=2*Vector3.Dot(new Vector3(o.X,0,o.Z),toward)+up*o.Y+.45f*H(seed,i,40);
            }
            int n=0;
            for(int k=0;k<count;k++)
            {
                int best=-1;for(int i=0;i<5;i++)if(score[i]>float.MinValue&&(best<0||score[i]>score[best]))best=i;if(best<0)break;
                into[n++]=parts[best];score[best]=float.MinValue;
            }
            // The head first (the corpse's own crush part: its hat flies loose as before).
            for(int i=1;i<n;i++)if(into[i]==CapturedRig.Head){into[i]=into[0];into[0]=CapturedRig.Head;}
            return n;
        }

        // ================================================================================================ pinned: the pancake
        /// <summary>The crusher's underside `age` s after the death (pinned; NaN for none).</summary>
        public static float Underside(SmashPlan p,float age)=>p?.Hazard==null?float.NaN:Underside(p.Hazard,p.Round,p.Clock+Math.Max(0,age));
        /// <summary>The pinned body's height share at `age` (1 standing .. <see cref="Flat"/>): squashed under the crusher's
        /// underside as it comes down, flat from the moment it is down, and flat for good (the remains stay pancaked).</summary>
        public static float Squash(SmashPlan p,float age)
        {
            if(p==null||!p.Pinned||!(age>0))return 1;if(age>=p.Contact)return Flat;
            float u=Underside(p,age),s=(u-p.Floor)/Math.Max(.3f,p.Top-p.Floor);return float.IsNaN(s)?Flat:Clamp(s,Flat,1);
        }
        /// <summary>How far the flattened body spreads across the floor (1 .. <see cref="SpreadMax"/>), with its squash.</summary>
        public static float Spread(float squash)=>1+(SpreadMax-1)*Smooth((1-squash)/(1-Flat));
        /// <summary>How far the pinned body has buckled into its sprawl (0..1): done by the time the crusher is down.</summary>
        public static float Progress(SmashPlan p,float age)=>p==null||!p.Pinned||!(age>0)?0:Smooth(age/Math.Max(.03f,p.Contact));
        /// <summary>The pinned body's squash about the floor at `age`: the pivot (the body's place on the floor), the height
        /// and floor-spread scales and the lift kept over the floor. False when the plan is not pinned.</summary>
        public static bool Overlay(SmashPlan p,float age,out Vector3 pivot,out float spread,out float height,out float lift)
        {
            pivot=Vector3.Zero;spread=height=1;lift=0;if(p==null||!p.Pinned)return false;
            height=Squash(p,age);spread=Spread(height);pivot=new Vector3(p.Body.X,p.Floor,p.Body.Z);lift=Gap*Smooth((1-height)/(1-Flat));return true;
        }
        /// <summary>One ragdoll body's rigid move (x' = Turn (x - Pivot) + Pivot + Offset, Unity world).</summary>
        public struct Move{public Quaternion Turn;public Vector3 Offset;public static readonly Move None=new Move{Turn=Quaternion.Identity};}
        /// <summary>
        /// The pinned body's sprawl at `progress` (0 the death pose .. 1 down): `joints` the 22 joint positions as it died (the
        /// corpse's bind), `into` (22) each joint's rigid move; the ragdoll bodies (NativeBisect.Bodies) carry theirs, every
        /// other joint its body's. It buckles and goes down on its back or face round the pelvis, legs and arms thrown out (a
        /// seeded spread), the head turned aside, the pelvis on the floor: what the squash then flattens reads as a body.
        /// </summary>
        public static void Sprawl(SmashPlan p,float progress,Vector3[] joints,Move[] into)
        {
            if(into==null)return;for(int i=0;i<into.Length;i++)into[i]=Move.None;if(p==null||joints==null||joints.Length<22||into.Length<22)return;
            float e=Smooth(progress);if(!(e>0))return;uint s=p.Seed;Vector3 up=Vector3.UnitY,f=Forward(p.Yaw),r=Right(p.Yaw);
            var fall=p.Fall>=0?-f:f;
            Quaternion About(Vector3 from,Vector3 toward,float angle)=>Quaternion.CreateFromAxisAngle(CapturedRig.Unit(Vector3.Cross(from,toward),r),angle*e);
            Quaternion Twist(Vector3 axis,float angle)=>Quaternion.CreateFromAxisAngle(axis,angle*e);
            // Local turns about each body's own joint (bind frame), then composed down the ragdoll's chains.
            Quaternion pelvis=About(up,fall,1.5f),spine=Twist(up,(H(s,1,1)-.5f)*.5f)*About(up,fall,(H(s,1,2)-.3f)*.25f);
            Quaternion head=Twist(up,(H(s,1,3)<.5f?-1:1)*(.7f+.5f*H(s,1,4)))*About(up,fall,(H(s,1,5)-.5f)*.5f);
            Quaternion LArm=About(-up,-r,1.15f+.85f*H(s,1,6)),RArm=About(-up,r,1.15f+.85f*H(s,1,7));
            Quaternion LFore=About(-up,-r,.15f+.55f*H(s,1,8)),RFore=About(-up,r,.15f+.55f*H(s,1,9));
            Quaternion LLeg=About(-up,-r,.38f+.35f*H(s,1,10)),RLeg=About(-up,r,.38f+.35f*H(s,1,11));
            Quaternion LShin=About(-up,-r,.08f+.3f*H(s,1,12)),RShin=About(-up,r,.08f+.3f*H(s,1,13));
            var drop=new Vector3(0,(p.Floor+.14f-joints[CapturedRig.Pelvis].Y)*e,0);
            Move Root(Quaternion q,Vector3 at)=>new Move{Turn=q,Offset=at-Vector3.Transform(at,q)+drop};
            Move Child(in Move parent,Quaternion q,Vector3 at)=>new Move{Turn=Quaternion.Normalize(parent.Turn*q),Offset=Vector3.Transform(at-Vector3.Transform(at,q),parent.Turn)+parent.Offset};
            var mp=Root(pelvis,joints[CapturedRig.Pelvis]);var ms=Child(mp,spine,joints[CapturedRig.Spine]);
            into[CapturedRig.Pelvis]=mp;into[CapturedRig.Spine]=ms;into[CapturedRig.Head]=Child(ms,head,joints[CapturedRig.Head]);
            var la=Child(ms,LArm,joints[CapturedRig.LArm]);var ra=Child(ms,RArm,joints[CapturedRig.RArm]);into[CapturedRig.LArm]=la;into[CapturedRig.RArm]=ra;
            into[CapturedRig.LElbow]=Child(la,LFore,joints[CapturedRig.LElbow]);into[CapturedRig.RElbow]=Child(ra,RFore,joints[CapturedRig.RElbow]);
            var ll=Child(mp,LLeg,joints[CapturedRig.LHip]);var rl=Child(mp,RLeg,joints[CapturedRig.RHip]);into[CapturedRig.LHip]=ll;into[CapturedRig.RHip]=rl;
            into[CapturedRig.LKnee]=Child(ll,LShin,joints[CapturedRig.LKnee]);into[CapturedRig.RKnee]=Child(rl,RShin,joints[CapturedRig.RKnee]);
            // Every other joint moves with its ragdoll body.
            for(int b=0;b<22;b++){int a=NativeBisect.Anchor(b);if(a!=b)into[b]=into[a];}
        }
        /// <summary>Applies a rigid move to a point.</summary>
        public static Vector3 Apply(in Move m,Vector3 x)=>Vector3.Transform(x,m.Turn)+m.Offset;

        // ================================================================================================ thrown
        /// <summary>A thrown body's ragdoll body `part` (at `at`; `centre` the body's) as it leaves the hit-stop: velocity and
        /// spin (Unity world, m/s and rad/s; the caller clamps the speed to the ragdoll's own limit).</summary>
        public static void Throw(SmashPlan p,int part,Vector3 at,Vector3 centre,out Vector3 velocity,out Vector3 spin)
        {
            velocity=spin=Vector3.Zero;if(p==null||p.Pinned)return;Vector3 up=Vector3.UnitY,t=p.Travel,side=CapturedRig.Unit(Vector3.Cross(up,t),Vector3.UnitX);
            float h0=H(p.Seed,part,50),h1=H(p.Seed,part,51),h2=H(p.Seed,0,52),h3=H(p.Seed,0,53);bool train=p.Cause==SmashCause.Train;
            velocity=t*(p.Throw*(.86f+.28f*h0))+up*((train?2.6f:1.6f)+(train?2.2f:1)*h1)+side*((h2-.5f)*(train?3:1.5f));
            // It tumbles along the travel (its top leads: the floor holds the feet back) with a seeded roll.
            var roll=Vector3.Cross(up,t)*((train?7:3)+(train?5:2)*h3)+t*((h2-.5f)*(train?6:2));
            spin=roll;velocity+=Vector3.Cross(roll,at-centre)*.15f;
        }
        /// <summary>A thrown body's crushed fragments: the velocity they inherit, the kick along the travel and the severity
        /// (NativeKillDebris), all along the plan's travel and clamped to <see cref="MaxCarry"/>.</summary>
        public static void Fragments(SmashPlan p,out Vector3 inherit,out float kick,out float severity)
        {
            inherit=Vector3.Zero;kick=0;severity=1;if(p==null||p.Pinned)return;bool train=p.Cause==SmashCause.Train;
            // The debris adds up to the kick and about 6 m/s of burst on top: the whole stays under MaxCarry.
            kick=train?6:3;inherit=p.Travel*Math.Min(MaxCarry-kick-7,p.Carry*(train?.8f:.6f))+Vector3.UnitY*(train?1.5f:.8f);severity=train?1:.8f;
        }

        // ================================================================================================ blood
        public const int Gushes=8;
        /// <summary>
        /// Blood gush `k` of <see cref="Gushes"/> (NativeBloodSim.Spray): when (s after the death), from where, out of which way,
        /// along what, carried by what (Unity world), its spray kind and severity. Pinned: squirts out from under the
        /// crusher's edges (from where the body lies toward each edge) the moment it is down. The train: a heavy burst at the
        /// strike along the travel, carried at the train's pace. A crusher's edge: a short burst out from under it. False: none.
        /// </summary>
        public static bool Gush(SmashPlan p,int k,out float at,out Vector3 origin,out Vector3 normal,out Vector3 along,out Vector3 carry,out string kind,out float severity)
        {
            at=0;origin=normal=along=carry=Vector3.Zero;kind=null;severity=0;if(p==null||k<0||k>=Gushes)return false;
            Vector3 up=Vector3.UnitY;float h0=H(p.Seed,k,60),h1=H(p.Seed,k,61),h2=H(p.Seed,k,62);
            if(p.Pinned)
            {
                float a=(float)(k*Math.PI*2/Gushes+(h0-.5f)*.6f+p.Yaw*Math.PI/180);var dir=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
                at=p.Contact+.012f*k+.01f*h1;origin=Edge(p,p.Body,dir,1.6f)+dir*.04f;origin.Y=p.Floor+.025f;
                normal=CapturedRig.Unit(dir+up*.12f,dir);along=dir;carry=dir*(2+2.5f*h2);kind="squirt";severity=.7f+.3f*h1;return true;
            }
            var t=p.Travel;var side=CapturedRig.Unit(Vector3.Cross(up,t),Vector3.UnitX);bool train=p.Cause==SmashCause.Train;
            if(!train&&k>=4)return false;
            at=(train?.006f:.01f)*k;origin=p.Body+up*((train?-.2f:.15f)+(train?.7f:.4f)*h0)+side*((h1-.5f)*.4f)-t*.1f;
            normal=CapturedRig.Unit(t+up*(.15f+.3f*h2)+side*((h1-.5f)*.6f),t);along=t;
            carry=t*(p.Carry*(train?.35f+.45f*h2:.5f+.5f*h2))+up*(train?1+h0:.6f);kind=train&&k<4?"sever":"crush";severity=train?1:.9f;return true;
        }
        /// <summary>Where a ray from `from` along `dir` (horizontal, unit) leaves the crusher's footprint, at most `reach` m away.</summary>
        public static Vector3 Edge(SmashPlan p,Vector3 from,Vector3 dir,float reach)
        {
            float t=reach;
            if(Math.Abs(dir.X)>1e-5f){float x=(dir.X>0?p.Center.X+p.Half.X:p.Center.X-p.Half.X);t=Math.Min(t,Math.Max(0,(x-from.X)/dir.X));}
            if(Math.Abs(dir.Z)>1e-5f){float z=(dir.Z>0?p.Center.Z+p.Half.Z:p.Center.Z-p.Half.Z);t=Math.Min(t,Math.Max(0,(z-from.Z)/dir.Z));}
            return new Vector3(from.X+dir.X*t,from.Y,from.Z+dir.Z*t);
        }
        public const int Pools=16;
        /// <summary>The splats a smash leaves (on low graphics 7, reduced effects 10).</summary>
        public static int PoolCount(float quality,bool low)=>low?7:quality<1?10:Pools;
        /// <summary>
        /// Splat `k` a smash leaves on the floor, laid straight on it (NativeBloodSplats): when (s after the death), where (a
        /// point on the floor's plane, the runtime casts it down onto the surface), along what and its half length and width.
        /// Pinned: a pool under and round the flattened body, squeezed out toward the crusher's edges. Thrown: streaks along
        /// the travel on the floor ahead of the strike (the train's track bed), or round a crusher's edge.
        /// </summary>
        public static bool Pool(SmashPlan p,int k,out float at,out Vector3 point,out Vector3 along,out float length,out float width)
        {
            at=0;point=along=Vector3.Zero;length=width=0;if(p==null||k<0||k>=Pools)return false;
            float h0=H(p.Seed,k,70),h1=H(p.Seed,k,71),h2=H(p.Seed,k,72),h3=H(p.Seed,k,73);
            if(p.Pinned)
            {
                float a=(float)(k*2.39996+h0*.8);var dir=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
                if(k<4){float rr=.08f+.18f*h1;point=p.Body+dir*rr;length=.24f+.12f*h2;width=.16f+.08f*h3;}
                else{float reach=(Edge(p,p.Body,dir,1.4f)-p.Body).Length()+.15f,rr=Math.Min(reach,.3f+.75f*(float)Math.Sqrt(h1));point=p.Body+dir*rr;length=.1f+.16f*h2;width=.06f+.07f*h3;}
                point.Y=p.Floor;along=dir;at=p.Contact+.02f+.01f*k;return true;
            }
            var t=p.Travel;var side=CapturedRig.Unit(Vector3.Cross(Vector3.UnitY,t),Vector3.UnitX);bool train=p.Cause==SmashCause.Train;
            float d=train?.6f+7.5f*(float)Math.Pow(h1,1.3):.2f+1.6f*h1;point=p.Body+t*d+side*((h0-.5f)*(train?1.8f:1.2f));point.Y=p.Floor;
            along=CapturedRig.Unit(t+side*((h2-.5f)*.3f),t);length=(train?.14f+.2f*h2:.08f+.12f*h2);width=.05f+.06f*h3;at=.08f+d/(train?18:6);return true;
        }
        public const int Mists=10;
        /// <summary>Blood mist puff `k` at `age`: where, how big, how thick (0..1) and its life share u; false when not shown.
        /// The train's rides along the travel, slowing; a crusher's leaves its edges low along the floor.</summary>
        public static bool Mist(SmashPlan p,int k,float age,out Vector3 at,out float size,out float opacity,out float u)
        {
            at=Vector3.Zero;size=opacity=u=0;if(p==null||k<0||k>=Mists)return false;float h0=H(p.Seed,k,80),h1=H(p.Seed,k,81),h2=H(p.Seed,k,82);
            bool train=p.Cause==SmashCause.Train;float born=p.Pinned?p.Contact+.01f*k:.012f*k,life=train?.9f+.3f*h2:p.Pinned?.75f+.25f*h2:.6f;float t=age-born;
            if(p.Pinned&&k>=8||!train&&!p.Pinned&&k>=5)return false;if(!(t>=0)||t>=life)return false;u=t/life;
            if(p.Pinned)
            {
                float a=(float)(k*Math.PI*2/8+(h0-.5f)*.6f+p.Yaw*Math.PI/180);var dir=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
                at=Edge(p,p.Body,dir,1.6f)+dir*(.1f+1.4f*(1-(float)Math.Exp(-t*3.5f))/3.5f*3);at.Y=p.Floor+.12f+.25f*u;size=.28f+.6f*u;opacity=.55f*(1-u)*(1-u);return true;
            }
            var tr=p.Travel;var side=CapturedRig.Unit(Vector3.Cross(Vector3.UnitY,tr),Vector3.UnitX);float v0=train?14+6*h1:2.5f;
            at=p.Body+tr*(v0*(1-(float)Math.Exp(-t*3))/3)+side*((h0-.5f)*(.3f+1.2f*u))+Vector3.UnitY*((h2-.4f)*.5f+.3f*u);size=(train?.35f:.25f)+(train?1.1f:.5f)*u;opacity=(train?.6f:.45f)*(1-u)*(1-u);return true;
        }
        /// <summary>The train's impact flash at `age`: where, how big and how bright (0..1); false after it.</summary>
        public static bool Flash(SmashPlan p,float age,out Vector3 at,out float size,out float level)
        {
            at=Vector3.Zero;size=level=0;if(p==null||p.Cause!=SmashCause.Train||!(age>=0)||age>=.12f)return false;
            float u=age/.12f;at=p.Body-p.Travel*.25f;size=2.2f*(1+u*1.6f);level=(1-u)*(1-u);return true;
        }
    }
}
