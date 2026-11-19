using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One pod at one presented moment (NativePodDrop.Evaluate): where it is, how it burns, its door, its pilot.</summary>
    public struct PodFrame
    {
        /// <summary>The drop is under way (from the countdown's start until its pilot stands); after it the pod is parked.</summary>
        public bool Dropping;
        /// <summary>Nothing to draw: before its streak appears, or once it has launched away.</summary>
        public bool Gone;
        /// <summary>Presentation time minus the fight start (NativeSpawnIn.Age).</summary>
        public float Age;
        /// <summary>The pod's base over its rest centre on the floor (world, m), and its tilt over its facing (world).</summary>
        public Vector3 Offset;public Quaternion Tilt;
        /// <summary>The tilt's angle (radians): kept for the station's ride code (W3), which read the old drop's lean; the pod's own
        /// turn is Tilt.</summary>
        public float Lean;
        /// <summary>Re-entry heat on the shield and seams (0..1); the ablative tiles' glow (1 white hot .. 0 char black:
        /// HullColor); the retro burn (0..1, sputtering when they fail); the cabin lamp (0..1); the LED sign's power (0..1,
        /// flickering on after the landing); the door seam's flare before the bolts (0..1); the beacon (0..1); the explosive
        /// bolts' flash (0..1); the lasting smoke (1 just landed .. 0 gone).</summary>
        public float Heat,Hull,Retro,Cabin,Sign,Seam,Beacon,Bolts,Smoke;
        /// <summary>The door's turn about its hinge (0 shut, the plan's RestDoor resting open as a ramp).</summary>
        public float Door;
        /// <summary>Seconds since the impact (negative before it).</summary>
        public float Land;
        /// <summary>The pilot shows (the bolts have blown the door); before it the pilot rides hidden inside.</summary>
        public bool Shown;
        /// <summary>The drawn pilot's feet over the pod's rest centre (world) and its tilt about them: strapped in on the pod's
        /// full offset and lean, then unbuckled and standing on the capsule (Capsule).</summary>
        public Vector3 Pilot;public Quaternion PilotTilt;
        /// <summary>The harness bar: 0 down over the pilot's stomach, 1 up. How seated the pilot is (1 braced in the jump seat in
        /// the lobby Chair pose, 0 standing) and the seat's cushion (0 down, 1 folded up against the backrest).</summary>
        public float Harness,Sit,Fold;
    }

    /// <summary>
    /// One pod's crash, fixed for one drop (NativePodDrop.Plan) from its record (seat, order, x, z, end, yaw) and the opening it
    /// spans: when each beat lands, the line it flies in on, how it lands and rests, the marks it leaves. Every peer, the killcam
    /// and a late joiner compute the same plan from the same record; nothing in it is a rule.
    /// </summary>
    public struct PodPlan
    {
        public int Order,Seed;public float Opening;
        /// <summary>The beats (seconds from GO): the streak appears, the director cuts to the ride, the first-person cabin moment
        /// ends, the cloud punch, the retros, the impact, the bolts blow the door (taking DoorSeconds), the pilot unbuckles and
        /// stands, the drop is over; the camera's stop over the crash, its arc down, its cut into my own eyes (first person) and
        /// its hand-off back to the game's camera.</summary>
        public float Appear,Ride,CabinEnd,Punch,RetroAt,Impact,DoorAt,DoorSeconds,StandFrom,StandEnd,End,Stop,ArcFrom,ArcTo,CutAt,HandOff;
        /// <summary>The roll on the way in: it burns in door down (its porthole on the city), and rolls door forward over
        /// FlipFrom..FlipTo as its retros light (RollDir the way it turns).</summary>
        public float FlipFrom,FlipTo,RollDir;
        /// <summary>The door's facing (yaw in radians), the final travel (Forward turned by Bearing, within MaxBearing: it comes in
        /// from behind its door) and the travel's right.</summary>
        public Vector3 Forward,Travel,Side;public float Yaw,Bearing;
        /// <summary>The touchdown's path angle (radians below the horizon) and speed (m/s), and whether its retros fail.</summary>
        public float Dive,Speed;public bool Fails;
        /// <summary>The flight (the base over the rest centre): from P0 at Appear through Pr at RetroAt to the touchdown P1 at
        /// Impact, with velocities V0, Vr and V1 (C1 throughout).</summary>
        public Vector3 P0,V0,Pr,Vr,P1,V1;
        /// <summary>The skid from the touchdown to the rest centre (m, at most MaxSkid) and its time; how far the base ploughs in;
        /// the rest lean (radians, at most MaxLean) about LeanAxis; the door's rest turn on the sunk, leaning pod (its top on the
        /// crater).</summary>
        public float Skid,SkidSeconds,Sink,Lean,RestDoor;public Vector3 LeanAxis;
        /// <summary>The lasting mark: the crater's shape, its turn off the approach (its ploughed tail runs back down the travel)
        /// and its scale (x across, z along); how far its furrow reaches back from the pod centre.</summary>
        public int Crater;public float CraterTurn,CraterX,CraterZ,Furrow;
        /// <summary>Which side of my pod the first-person front shot stands (the arc into the doorway's view): -1 or 1.</summary>
        public float FrontSide;
    }

    /// <summary>The directed drop's camera at one moment (NativePodDrop.Shot): where it looks from and at, its up, its field of
    /// view, how much of the frame is directed (1; below it the game's own camera is blended back in, the hand-off at GO),
    /// whether the eye rides my cabin (the braced second), whether it is inside my pod or my head (Inside: my own body stays
    /// hidden), and how hard the cabin buffets it (Judder 0..1: a turn on top, none with camera shake off).</summary>
    public struct PodShot{public Vector3 Eye,Look,Up;public float Fov,Directed,Judder;public bool Cabin,Inside;}

    /// <summary>
    /// The Expedition drop pods (owner 2026-11-02: "When all players load in, we essentially drop in to the hub from a pod. (This
    /// pod is how players can choose to leave as well) using their OWN pod. (Can have an LED sign with the player name)"; the
    /// crash: docs/EXPEDITION-OVERHAUL.md R7 and W4; the model is NativePodModels, the draw Runtime/NativePodPresentation.cs). In a
    /// generated run's hub the pods take the place of the spawn-in beam (NativeSpawnIn) over the same clock: the run's opening
    /// countdown (Astra's ExpeditionRules.PodOpening, A0), which already holds every pilot at its spot on A0's landing field, so
    /// nothing here changes a rule, a timing or the network.
    ///  - Each pod comes in on its own line (hashed from its record: seat, order, x, z, end), from behind its door within ±40°:
    ///    a burning streak high over the city (drawn past the fog like a meteor), door down so its porthole looks at the city, a
    ///    cloud deck it punches through with a vapour ring and a boom, the roll door forward as its retros flare (two pods in four
    ///    sputter and fail and come in harder), and the impact at Impact (staggered by order): it touches down at most MaxSkid
    ///    short of its pod centre at 45-60°, ploughs in and comes to rest on the centre, sunk Sink and leaning at most MaxLean
    ///    (inside the collider envelope A1 will give it). Slam, shake, a small hot flash at its base, the shockwave racing out and
    ///    the dust wall, sparks, and the flash through the blast lights.
    ///  - The explosive bolts blow the door down into a ramp; the pilot, braced in the jump seat on the pod's full offset and lean,
    ///    lifts the harness bar, stands up out of the seat (it folds away) and stands on the capsule by GO. The capsule never
    ///    moves.
    ///  - What lasts, from the record and the clock: the crater, the scorch and the furrow, the hull cooling from white hot to
    ///    char over CoolSeconds and its smoke thinning over SmokeSeconds. Then the pod stays parked for the run (the pilot's way
    ///    out: leaving by pod is Astra's rule; Launch is its look).
    ///  - My own camera: Shot (the sky over the camp as the streaks come in; the chase, or one braced second in my red-lit cabin
    ///    in first person; the stop over the crash; the arc down behind my pilot, or in first person round to the doorway and a
    ///    cut into my own eyes; the hand-off at GO), Shake, and the still wide shot for Reduced effects.
    /// The opening is Astra's PodOpening (5 s on generated hubs), and every beat derives from it: a 3 s opening (the owner's
    /// fallback for Q22) is planned too (Plan, OpeningFor). Pure functions of (age, plan): any frame, peer or late frame shows
    /// the same moment. Engine free; nothing allocates.
    /// </summary>
    public static class NativePodDrop
    {
        // ---- the opening ----
        /// <summary>The two openings a drop is timed for: the plan's 5 s and the 3 s fallback (Q22).</summary>
        public const float ShortOpening=3,LongOpening=5,MaxOpening=6;
        /// <summary>The opening a drop spans: the countdown a generated hub holds (Astra's ExpeditionRules.PodOpening, A0).</summary>
        public const float Opening=(float)ExpeditionRules.PodOpening;
        /// <summary>The countdown's start relative to GO (the plan's contract name: the station's ride, W3, times its beat by it).</summary>
        public const float Appear=-Opening;
        /// <summary>The opening of a drop whose countdown has been seen as early as `earliest` s before GO (NaN: unseen): a
        /// countdown seen longer than the short one is at least the long one.</summary>
        public static float OpeningFor(float earliest)=>!float.IsNaN(earliest)&&earliest<-(ShortOpening+.25f)?Math.Max(Opening,LongOpening):Opening;
        /// <summary>When pod `order` hits (s from GO) in the drop's opening (or `opening`): -2.8 + .25 n on 5 s, -1.75 + .12 n on 3 s.</summary>
        public static float ImpactOf(int order)=>ImpactOf(order,Opening);
        public static float ImpactOf(int order,float opening){float k=Blend(opening);return L(-1.75f,-2.8f,k)+L(.12f,.25f,k)*Math.Max(0,Math.Min(7,order));}
        private static float Blend(float opening){float o=float.IsNaN(opening)?Opening:Math.Max(ShortOpening,Math.Min(MaxOpening,opening));return Clamp01((o-ShortOpening)/(LongOpening-ShortOpening));}
        /// <summary>How far the pod's line may come in off straight behind its door, the most it skids and leans at rest, how far
        /// it sinks, and the camera's ride (behind and above my pod).</summary>
        public const float MaxBearing=40*Deg,MaxSkid=1.5f,MaxLean=4*Deg,Sink=.2f,ChaseBack=11,ChaseUp=2.6f;
        /// <summary>The hull cools to black and the smoke thins away over these; an unknown time parks it this long after.</summary>
        public const float CoolSeconds=20,SmokeSeconds=40,ParkedSince=60;
        /// <summary>The bolts fire this long before the door moves; the harness bar lifts over HarnessSeconds; the door settles
        /// over DoorSettle; the pilot stands by StandMargin before GO.</summary>
        public const float BoltLead=.06f,HarnessSeconds=.3f,DoorSettle=.3f,StandMargin=.05f;
        /// <summary>The shield's share of its turn into the flow on the way in, and once the retros have stood it up.</summary>
        public const float FlightTilt=.55f,TouchTilt=.14f;
        /// <summary>The braced pilot's forward hunch against the bar, and the seated eye in the cabin (the station's, W3's
        /// NativePodRide.Eye: strapped in at the back of the cabin, facing the door) over the pod's base.</summary>
        public const float HunchAngle=7*Deg;
        public static readonly Vector3 CabinEyeAt=new Vector3(0,NativePodModels.Floor+1.12f,-.12f);
        /// <summary>Where the pilot's feet are in the cabin (the pod's frame): on the pod centre, seated and standing alike.</summary>
        public static readonly Vector3 CabinFeet=new Vector3(0,NativePodModels.Floor,0);
        /// <summary>The cloud deck's height over the floor on the long and short openings (the punch), and the sky shot's eye: this
        /// far up over the camp, this far back past it from my pod's line.</summary>
        public const float DeckLong=50,DeckShort=45,SkyUp=140,SkyBack=80;
        private const float Deg=(float)(Math.PI/180),Pi=(float)Math.PI,Tau=(float)(Math.PI*2);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float L(float a,float b,float t)=>a+(b-a)*t;

        // ---- the plan ----
        /// <summary>
        /// The crash of the pod whose record is (seat, order, x, z, end, yaw in degrees), timed for an opening of `opening` s
        /// (ShortOpening .. MaxOpening; between them the beats blend, past LongOpening the sky shot just starts earlier).
        /// </summary>
        public static PodPlan Plan(int seat,int order,float x,float z,double end,float yawDegrees,float opening)
        {
            float o=float.IsNaN(opening)?Opening:Math.Max(ShortOpening,Math.Min(MaxOpening,opening)),k=Blend(o);
            var p=new PodPlan{Order=Math.Max(0,Math.Min(7,order)),Opening=o};int s=p.Seed=PlanSeed(seat,p.Order,x,z,end);
            // the beats: 3 s (the fallback) to 5 s (the plan's opening): impacts at -1.75 + .12 n to -2.8 + .25 n, the door .35 to .6 after
            p.Appear=-o;p.Ride=L(-2.6f,-4.1f,k);p.CabinEnd=p.Ride+L(.55f,1,k);
            p.Impact=ImpactOf(p.Order,o);p.RetroAt=p.Impact-L(.3f,.4f,k);
            p.DoorSeconds=L(.18f,.22f,k);p.DoorAt=Math.Min(p.Impact+L(.35f,.6f,k),-(p.DoorSeconds+DoorSettle+.05f));
            p.StandEnd=Math.Min(p.DoorAt+L(.95f,1.4f,k),-StandMargin);p.StandFrom=Math.Max(p.DoorAt+.04f,Math.Min(p.DoorAt+.12f,p.StandEnd-.3f));
            p.End=Math.Max(p.StandEnd,p.DoorAt+p.DoorSeconds+DoorSettle);
            p.Stop=p.Impact-L(.25f,.35f,k);p.ArcFrom=p.Impact+L(.35f,.55f,k);p.HandOff=L(.5f,.6f,k);p.ArcTo=Math.Min(-.02f,Math.Max(p.ArcFrom+.5f,-p.HandOff-.05f));p.ArcFrom=Math.Min(p.ArcFrom,p.ArcTo-.25f);
            // first person: the cut from the doorway's view into my own eyes, late in the stand-up and before the hand-off
            p.CutAt=Math.Max(p.ArcTo,Math.Min(p.StandEnd-.1f,-p.HandOff));
            p.FlipTo=p.RetroAt+.05f;p.FlipFrom=p.FlipTo-L(.45f,.55f,k);
            // the line in: from behind the door within MaxBearing, at 45-60 degrees, braked by the retros (or not)
            p.Yaw=yawDegrees*Deg;p.Forward=new Vector3((float)Math.Sin(p.Yaw),0,(float)Math.Cos(p.Yaw));
            // (leaning a little toward the camp's centre, so the four lines fan in on the camp from round it)
            float inward=Wrap((float)Math.Atan2(-x,-z)-p.Yaw),toward=Math.Max(-MaxBearing,Math.Min(MaxBearing,inward));
            p.Bearing=Math.Max(-MaxBearing,Math.Min(MaxBearing,.7f*toward+(2*Hash(s,1)-1)*22*Deg));p.Travel=Turn(p.Forward,p.Bearing);p.Side=new Vector3(p.Travel.Z,0,-p.Travel.X);
            p.Fails=Fails(end,p.Order);p.Dive=(45+15*Hash(s,2))*Deg;p.RollDir=Hash(s,13)<.5f?-1:1;p.FrontSide=Hash(s,14)<.5f?-1:1;
            p.Skid=p.Fails?1.15f+.35f*Hash(s,3):.7f+.45f*Hash(s,3);p.SkidSeconds=L(.3f,.42f,k)*(p.Fails?1.15f:1);
            var up=Vector3.UnitY;var dive=p.Travel*(float)Math.Cos(p.Dive)-up*(float)Math.Sin(p.Dive);
            float s1=p.Fails?44:24,sr=p.Fails?72:82,tb=p.Impact-p.RetroAt;p.Speed=s1;
            p.P1=-p.Travel*p.Skid;p.V1=dive*s1;p.Vr=dive*sr;p.Pr=p.P1-dive*((sr+s1)*.5f*tb);
            float reach=L(120,200,k)*(.92f+.16f*Hash(s,4)),height=L(85,140,k)*(.92f+.16f*Hash(s,5)),ta=p.RetroAt-p.Appear,entry=(16+8*Hash(s,6))*Deg;
            p.P0=p.P1-p.Travel*reach+up*height;
            // the entry: shallow and fast, no faster than the cubic keeps monotone (its tangent within 2.6x the chord)
            var chord=p.Pr-p.P0;p.V0=(p.Travel*(float)Math.Cos(entry)-up*(float)Math.Sin(entry))*(1.3f*chord.Length()/ta);
            p.Punch=Crossing(p,L(DeckShort,DeckLong,k));
            // the rest: sunk and leaning a little, mostly along the plough
            p.Sink=Sink;var lean=Turn(p.Travel,(Hash(s,7)-.5f)*2.1f);p.LeanAxis=Vector3.Normalize(Vector3.Cross(up,lean));p.Lean=(1.4f+2.6f*Hash(s,8))*Deg;
            p.RestDoor=RestDoorTurn(p);
            p.Crater=(int)(Hash(s,9)*NativePodModels.CraterShapes)%NativePodModels.CraterShapes;p.CraterTurn=(2*Hash(s,10)-1)*10*Deg;p.CraterX=.9f+.2f*Hash(s,11);p.CraterZ=.92f+.14f*Hash(s,12);
            p.Furrow=NativePodModels.CraterRadius*(1+NativePodModels.FadeOut)*(1+NativePodModels.Plough)*p.CraterZ;
            return p;
        }
        /// <summary>The pod's own hash: its seat, order, spot (to the centimetre) and fight start (to the millisecond).</summary>
        public static int PlanSeed(int seat,int order,float x,float z,double end)
        {
            unchecked
            {
                uint h=2166136261u;long e=(long)Math.Round(end*1000);
                h=(h^(uint)seat)*16777619u;h=(h^(uint)order)*16777619u;h=(h^(uint)(int)Math.Round(x*100))*16777619u;h=(h^(uint)(int)Math.Round(z*100))*16777619u;
                h=(h^(uint)e)*16777619u;h=(h^(uint)(e>>32))*16777619u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>13;return (int)(h&0x7fffffff);
            }
        }
        /// <summary>Whether pod `order`'s retros fail: of the run's first four pods (one fight start, so every peer agrees) the two
        /// that rank lowest by the start's hash; a later pod half the time.</summary>
        public static bool Fails(double end,int order)
        {
            int e;unchecked{long m=(long)Math.Round(end*1000);e=(int)(m^(m>>32))^0x51ed27;}
            float mine=Hash(e,700+order);if(order>3||order<0)return mine<.5f;
            int below=0;for(int k=0;k<4;k++)if(k!=order&&Hash(e,700+k)<mine)below++;return below<2;
        }
        /// <summary>The pod centre a record's crash rests on: the record (its capsule) back along its facing by ExitReach.</summary>
        public static Vector3 Centre(float x,float y,float z,float yawDegrees)
        {
            float a=yawDegrees*Deg;return new Vector3(x,y,z)-new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a))*NativeExpeditionHubPlan.ExitReach;
        }
        /// <summary>The capsule's rest feet over the pod centre: on it today, ExitReach ahead once A1 moves the slot to the ramp foot.</summary>
        public static Vector3 Capsule(in PodPlan p)=>p.Forward*NativeExpeditionHubPlan.ExitReach+new Vector3(0,NativeSpawnIn.RestFeet,0);
        private static float Wrap(float a){while(a>Pi)a-=Tau;while(a<-Pi)a+=Tau;return a;}
        private static Vector3 Turn(Vector3 v,float a){float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.X*c+v.Z*s,v.Y,-v.X*s+v.Z*c);}
        /// <summary>The pod's facing: its door toward Forward.</summary>
        public static Quaternion Facing(in PodPlan p)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,p.Yaw);
        /// <summary>The pod's rest tilt and offset (sunk Sink, leaning Lean about LeanAxis).</summary>
        public static Quaternion RestTilt(in PodPlan p)=>Quaternion.CreateFromAxisAngle(p.LeanAxis,p.Lean);
        public static Vector3 RestOffset(in PodPlan p)=>new Vector3(0,-p.Sink,0);
        /// <summary>A point of the pod's model frame in the world (over its rest centre) at frame `f`.</summary>
        public static Vector3 PodPoint(in PodFrame f,in PodPlan p,Vector3 local)=>f.Offset+Vector3.Transform(local,Quaternion.Multiply(f.Tilt,Facing(p)));
        // The door's turn whose top rests on the crater's floor (FlatLift) on the sunk, leaning pod.
        private static float RestDoorTurn(in PodPlan p)
        {
            var rot=Quaternion.Multiply(RestTilt(p),Facing(p));var off=RestOffset(p);
            float Tip(float a)=>(off+Vector3.Transform(NativePodModels.Hinge+new Vector3(0,NativePodModels.DoorHeight*(float)Math.Cos(a),NativePodModels.DoorHeight*(float)Math.Sin(a)),rot)).Y-NativePodModels.FlatLift-.01f;
            float lo=Pi/2-.4f,hi=NativePodModels.OpenAngle+.15f;if(Tip(lo)<0)return lo;if(Tip(hi)>0)return hi;
            for(int i=0;i<24;i++){float m=(lo+hi)*.5f;if(Tip(m)>0)lo=m;else hi=m;}return (lo+hi)*.5f;
        }
        // When the flight comes down through the cloud deck `deck` m over the floor.
        private static float Crossing(in PodPlan p,float deck)
        {
            float lo=p.Appear,hi=p.RetroAt;if(FlightAt(p,lo,out _).Y<=deck)return lo;if(FlightAt(p,hi,out _).Y>deck)return hi;
            for(int i=0;i<24;i++){float m=(lo+hi)*.5f;if(FlightAt(p,m,out _).Y>deck)lo=m;else hi=m;}return (lo+hi)*.5f;
        }

        // ---- the flight ----
        /// <summary>The base over the rest centre at `t` (Appear..Impact) and its velocity: two cubic Hermite legs, the fast,
        /// curving descent to the retros and the braking last leg to the touchdown.</summary>
        public static Vector3 FlightAt(in PodPlan p,float t,out Vector3 velocity)
        {
            if(t<p.RetroAt){float T=p.RetroAt-p.Appear;return Hermite(p.P0,p.V0,p.Pr,p.Vr,T,Clamp01((t-p.Appear)/T),out velocity);}
            float B=p.Impact-p.RetroAt;return Hermite(p.Pr,p.Vr,p.P1,p.V1,B,Clamp01((t-p.RetroAt)/B),out velocity);
        }
        private static Vector3 Hermite(Vector3 p0,Vector3 v0,Vector3 p1,Vector3 v1,float T,float u,out Vector3 velocity)
        {
            float u2=u*u,u3=u2*u;
            velocity=(p0*(6*u2-6*u)+v0*(T*(3*u2-4*u+1))+p1*(-6*u2+6*u)+v1*(T*(3*u2-2*u)))/T;
            return p0*(2*u3-3*u2+1)+v0*(T*(u3-2*u2+u))+p1*(-2*u3+3*u2)+v1*(T*(u3-u2));
        }
        // The turn that brings the base (local -y) `share` of the way round into the flow along `velocity`.
        private static Quaternion Into(Vector3 velocity,float share)
        {
            float l=velocity.Length();if(l<1e-3f||!(share>0))return Quaternion.Identity;var d=velocity/l;var down=-Vector3.UnitY;
            var axis=Vector3.Cross(down,d);float n=axis.Length();if(n<1e-4f)return Quaternion.Identity;
            return Quaternion.CreateFromAxisAngle(axis/n,(float)Math.Atan2(n,Vector3.Dot(down,d))*share);
        }
        /// <summary>The axis that tips the top toward `dir` (level).</summary>
        private static Vector3 Pitch(Vector3 dir){var a=Vector3.Cross(Vector3.UnitY,new Vector3(dir.X,0,dir.Z));float n=a.Length();return n>1e-5f?a/n:Vector3.UnitX;}
        /// <summary>The roll about the pod's own axis at `t` (radians): half a turn (door down, its porthole on the city) until
        /// FlipFrom, round to none (door forward) by FlipTo.</summary>
        public static float Roll(in PodPlan p,float t)=>t>=p.FlipTo?0:Pi*p.RollDir*(1-Smooth((t-p.FlipFrom)/(p.FlipTo-p.FlipFrom)));

        // ---- the frame ----
        /// <summary>
        /// Pod `p`'s frame `age` s from the fight start (NaN: an unknown time, parked long since). Before Appear nothing shows
        /// (Gone); after End the pod is parked: door open, cooling and smoking from the impact.
        /// </summary>
        public static PodFrame Evaluate(float age,in PodPlan p)
        {
            var f=new PodFrame{Age=age,Land=-1,Tilt=Quaternion.Identity,PilotTilt=Quaternion.Identity};
            if(float.IsNaN(age)){Parked(ref f,p,ParkedSince);return f;}
            if(age<p.Appear){f.Gone=true;return f;}
            float land=age-p.Impact;f.Land=land;
            if(age>=p.End){Parked(ref f,p,land);return f;}
            f.Dropping=true;
            if(land<0)
            {
                f.Offset=FlightAt(p,age,out var velocity);
                f.Tilt=Into(velocity,age<p.RetroAt?FlightTilt:L(FlightTilt,TouchTilt,Smooth((age-p.RetroAt)/.22f)));
                float roll=Roll(p,age);if(roll!=0)f.Tilt=Quaternion.Multiply(f.Tilt,Quaternion.CreateFromAxisAngle(Vector3.UnitY,roll));
                float u=Clamp01((age-p.Appear)/(p.Impact-p.Appear));
                // it burns from the first frame (a meteor, not a spark that grows), easing in over a few frames
                f.Heat=(.6f+.4f*Smooth(u*3.2f))*Smooth((age-p.Appear)/.12f)*(1-.6f*Smooth((age-p.RetroAt)/.25f));f.Hull=.45f+.45f*Smooth(u*2.4f);f.Retro=Retro(p,age);f.Cabin=.35f;
            }
            else
            {
                Ground(p,land,out f.Offset,out f.Tilt);
                f.Heat=1-Smooth(land/.5f);f.Hull=Cool(land);f.Retro=(1-Smooth(land/.1f))*Retro(p,p.Impact-.01f);
                f.Sign=SignOn(land);f.Cabin=Smooth((land-.15f)/.25f)*.5f;f.Beacon=((int)(land*4))%2==0?1:.2f;f.Smoke=SmokeLevel(land);
            }
            f.Seam=age<p.DoorAt?Smooth((age-(p.DoorAt-.3f))/.3f):1-Smooth((age-p.DoorAt)/.15f);f.Bolts=Bolts(age-p.DoorAt);
            f.Door=DoorTurn(age-p.DoorAt,p.DoorSeconds,p.RestDoor);if(f.Door>0)f.Cabin=Math.Max(f.Cabin,.5f+.5f*Smooth(f.Door/p.RestDoor));
            f.Shown=age>=p.DoorAt;Pilot(ref f,p,age);f.Lean=Tip(f.Tilt);
            return f;
        }
        /// <summary>The station's ride (W3) asked the old drop for a pod's frame by its order and seed: this is that drop's pod at the
        /// hub's centre facing +z (a pod's own frame is Evaluate(age, its plan)).</summary>
        public static PodFrame Evaluate(float age,int order,int seed)=>Evaluate(age,Plan(order,order,0,0,seed,0,Opening));
        // How far a tilt leans the pod's axis off upright (its roll about the axis aside).
        private static float Tip(Quaternion q){var a=Vector3.Transform(Vector3.UnitY,q);return (float)Math.Acos(Math.Max(-1,Math.Min(1,a.Y)));}
        // On the ground: from the touchdown it ploughs back to the rest centre (a hard stop), digs in, slams and recoils; the
        // touchdown's lean pitches over into the plough and rocks back to rest.
        private static void Ground(in PodPlan p,float land,out Vector3 offset,out Quaternion tilt)
        {
            float u=Clamp01(land/p.SkidSeconds),e=1-(1-u)*(1-u)*(1-u);
            float squash=land<.9f?-.08f*(float)Math.Exp(-8*land)*(float)Math.Sin(18*land):0;
            offset=p.P1*(1-e)+new Vector3(0,-p.Sink*Smooth(land/(p.SkidSeconds*.85f))+squash,0);
            float kick=(p.Fails?11:8)*Deg*(float)Math.Exp(-5.5f*land)*(float)Math.Sin(10*land);
            tilt=Quaternion.Multiply(Quaternion.CreateFromAxisAngle(Pitch(p.Travel),kick),Quaternion.Slerp(Into(p.V1,TouchTilt),RestTilt(p),Smooth(land/.4f)));
        }
        private static void Parked(ref PodFrame f,in PodPlan p,float since)
        {
            f.Dropping=false;f.Offset=RestOffset(p);f.Tilt=RestTilt(p);f.Door=p.RestDoor;f.Shown=true;f.Land=since;f.Sign=1;f.Cabin=.7f;f.Harness=1;f.Sit=0;f.Fold=1;
            f.Beacon=((int)(Math.Max(0,since)*1.25f))%2==0?1:.25f;f.Hull=Cool(since);f.Smoke=SmokeLevel(since);f.Pilot=Capsule(p);f.PilotTilt=Quaternion.Identity;f.Lean=p.Lean;
        }
        // The pilot: braced in the jump seat with the pod (hunched into the bar) until the harness lifts, then standing up out of the
        // seat (it folds away behind them) onto the capsule.
        private static void Pilot(ref PodFrame f,in PodPlan p,float age)
        {
            var strapped=PodPoint(f,p,CabinFeet);var braced=Quaternion.Multiply(f.Tilt,Quaternion.CreateFromAxisAngle(Pitch(p.Forward),HunchAngle));
            if(!f.Shown){f.Pilot=strapped;f.PilotTilt=braced;f.Harness=0;f.Sit=1;f.Fold=0;return;}
            f.Harness=Smooth((age-p.StandFrom)/HarnessSeconds);
            float from=p.StandFrom+.12f,span=Math.Max(.2f,p.StandEnd-from),stand=Smooth((age-from)/span);
            f.Sit=1-stand;f.Fold=Smooth((age-from-.25f*span)/(.6f*span));
            f.Pilot=Vector3.Lerp(strapped,Capsule(p),stand);f.PilotTilt=Quaternion.Slerp(braced,Quaternion.Identity,stand);
        }
        /// <summary>The ablative tiles' glow `land` s after the impact: hot off the touchdown, char black by CoolSeconds.</summary>
        public static float Cool(float land)=>land<0?.9f:.9f*(float)Math.Pow(Math.Max(0,1-land/CoolSeconds),1.6);
        /// <summary>The smoke left `land` s after the impact, thinning to nothing by SmokeSeconds.</summary>
        public static float SmokeLevel(float land)=>land<0?0:(float)Math.Pow(Math.Max(0,1-land/SmokeSeconds),1.3);
        /// <summary>The tiles' colour (linear, the glow the draw tints them with) for glow `hull`: char black, dull red, cherry,
        /// orange, white hot.</summary>
        public static Vector3 HullColor(float hull)
        {
            hull=Clamp01(hull);float x=hull*4;int i=Math.Min(3,(int)x);float t=x-i;return Vector3.Lerp(HullRamp[i],HullRamp[i+1],t);
        }
        private static readonly Vector3[] HullRamp={new Vector3(.07f,.062f,.055f),new Vector3(.5f,.06f,.02f),new Vector3(1.4f,.18f,.05f),new Vector3(2.6f,.8f,.18f),new Vector3(3.2f,2.4f,1.4f)};
        /// <summary>The retros' burn at `t`: flaring from RetroAt to the touchdown; a failing set sputters, weaker, and quits.</summary>
        public static float Retro(in PodPlan p,float t)
        {
            float on=Smooth((t-p.RetroAt)/.07f);if(on<=0)return 0;
            if(!p.Fails)return on*(.88f+.12f*(float)Math.Sin(t*97+(p.Seed&1023)));
            int k=(int)Math.Floor((t-p.RetroAt)*18);float gate=Hash(p.Seed,900+k)<.45f?1:.12f,quit=1-Smooth((t-p.RetroAt)/((p.Impact-p.RetroAt)*.75f));
            return on*(gate*(.35f+.4f*quit)*quit+.05f);
        }
        /// <summary>The explosive bolts' flash `since` s after the door starts to move (they fire BoltLead before it).</summary>
        public static float Bolts(float since){float b=since+BoltLead;return b<0?0:b<.05f?1:Math.Max(0,1-(b-.05f)/.12f);}
        // The sign flickers on a quarter second after the landing: two blinks, then steady.
        private static float SignOn(float a)=>a<.25f?0:a<.32f?1:a<.38f?.15f:a<.43f?1:a<.47f?.3f:1;
        /// <summary>The door's turn `t` s after the bolts blow it: kicked out, then falling fast to its rest `rest` over `seconds`,
        /// a slam and a small bounce.</summary>
        public static float DoorTurn(float t,float seconds,float rest)
        {
            if(!(t>0))return 0;if(t<seconds){float u=t/seconds;return rest*(.3f*u+.7f*u*u);}
            float b=t-seconds;return rest-.14f*(float)Math.Exp(-9*b)*Math.Abs((float)Math.Sin(15*b));
        }

        // ---- GO: the drawn pilot waits on the capsule while it settles (the fallback while the countdown can float) ----
        /// <summary>The longest the drawn pilot waits on its capsule after GO.</summary>
        public const float GoSettle=.45f;
        /// <summary>
        /// Whether the drawn pilot is held on its capsule's rest `since` s after GO, the capsule's feet `above` m over the rest and
        /// rising at `rise` m/s, on the ground or not: only while a floating capsule settles down onto the floor (above it by less
        /// than .3 m, not rising), for at most GoSettle. A capsule on its rest, rising (a jump at GO) or past GoSettle releases it,
        /// and the release holds (`released` latches), so a pilot is never held down and then snapped up.
        /// </summary>
        public static bool GoHold(float since,float above,float rise,bool grounded,ref bool released)
        {
            if(released||!(since>=0))return false;
            if(since>=GoSettle||!(rise<=.05f)||!(above<.3f)||above<-.05f||grounded&&Math.Abs(above)<.015f){released=true;return false;}
            return true;
        }

        // ---- leaving (Astra's rule triggers it; this is what it looks like) ----
        /// <summary>The door closes over this long, the retros light, the pod lifts away and is gone by LaunchGone.</summary>
        public const float LaunchClose=.5f,LaunchIgnite=.9f,LaunchGone=3.4f;
        /// <summary>Pod `p` parked, `since` s after its pilot boarded to leave: the door swings shut, the retros build, it rises
        /// out of its crater (standing up as it goes) slowly then fast straight up; Gone once away. The crater stays.</summary>
        public static PodFrame Launch(float since,in PodPlan p)
        {
            var f=new PodFrame{Age=since,Land=ParkedSince,Tilt=RestTilt(p),PilotTilt=Quaternion.Identity,Fold=1};if(!(since>=0)){Parked(ref f,p,ParkedSince);return f;}
            if(since>=LaunchGone){f.Gone=true;return f;}
            f.Dropping=true;f.Sign=1;f.Cabin=1;f.Beacon=((int)(since*8))%2==0?1:.2f;f.Pilot=Capsule(p);
            f.Door=p.RestDoor*(1-Smooth(since/LaunchClose));f.Retro=Smooth((since-LaunchClose)/(LaunchIgnite-LaunchClose));
            float up=since-LaunchIgnite;f.Offset=RestOffset(p)+new Vector3(0,up>0?up*up*(4+3*up):0,0);f.Heat=Clamp01(up*.6f);
            f.Tilt=Quaternion.Slerp(RestTilt(p),Quaternion.Identity,Smooth(up/.6f));f.Lean=Tip(f.Tilt);
            return f;
        }

        // ---- the effects (closed form, through the directed entrances' emitters; world frame, y up) ----
        /// <summary>Pod `p`'s effects for frame `f` (Effects, the eye far off).</summary>
        public static void Effects(INativeEntranceFx fx,in PodFrame f,in PodPlan p,Vector3 centre,float quality,float floor)=>Effects(fx,f,p,centre,quality,floor,new Vector3(0,1e6f,0));
        /// <summary>
        /// Pod `p`'s effects for frame `f` over its rest `centre` (on the floor at `floor`) seen from `eye`: the fireball under the
        /// shield, its burning trail back along the curve it flew and the contrail it leaves in the sky; the cloud deck it comes
        /// down through, the punch's vapour ring torn open in it and a flash; the retros (or their sputter, sparks and smoke); the
        /// impact's small hot flash at its base, the shockwave racing out low, the dust wall, sparks and spoil; the steam vents;
        /// the bolts' flashes and the door's blow; then the smoke off the hull and its embers while it is hot. Puffs and flashes
        /// close to the eye thin away (the chase flies through the deck and the ring). `quality` the effects setting (Reduced:
        /// softer flashes, thinner dust).
        /// </summary>
        public static void Effects(INativeEntranceFx fx,in PodFrame f,in PodPlan p,Vector3 centre,float quality,float floor,Vector3 eye)
        {
            if(fx==null||f.Gone)return;float t=f.Age,land=t-p.Impact;int s=p.Seed&0xffffff;bool soft=quality<.5f;
            var white=Vector3.One;var fire=NativeEntranceFx.Hex(0xffb35a,2.6f);var up=Vector3.UnitY;
            if(land<0)
            {
                var b=centre+f.Offset;FlightAt(p,t,out var vel);float speed=vel.Length();var dir=speed>1e-3f?vel/speed:-up;float near=Near(b,eye,2.5f,7);
                if(f.Heat>.04f&&near>0){fx.Glow(b+dir*.25f,white*(3.2f*f.Heat*near),0,1.7f,1,0,s+1);fx.Glow(b-dir*.4f,fire*(f.Heat*near),0,3.8f,0,0,s+2);}
                // the burning trail just behind it: overlapping streaks, white at the head to fire and ember behind
                for(int i=0;i<8;i++)
                {
                    if(i%2==1&&Hash(s+3,i)>quality)continue;float back=t-.022f*(i+1);if(back<p.Appear)break;
                    var at=centre+FlightAt(p,back,out var v)+Direction(s+4,i)*.12f;float k=i/8f,n=Near(at,eye,2,6);if(n<=0)continue;
                    fx.Streak(at,v*.9f,Vector3.Lerp(white*2.2f,fire*.8f,Smooth(k*1.6f))*(f.Heat*(1-k*.8f)*n),.34f-k*.16f,.8f,.045f,.35f,0,s+10+i);
                }
                if(f.Retro>.02f)Retros(fx,f,p,centre,t,s,quality,floor);
            }
            Contrail(fx,p,centre,t,s,quality,floor,eye);
            Deck(fx,p,centre,t,s,quality,floor,eye);
            float punch=t-p.Punch;
            if(punch>=0&&punch<1.6f)
            {
                // the cloud punch: a vapour ring torn open where it came through the deck, left behind it, and a flash (never one in
                // the eye's face: the chase flies through the hole)
                var at=centre+FlightAt(p,p.Punch,out var pv);var n=Vector3.Normalize(pv);var a=Vector3.Cross(n,up);a=a.LengthSquared()>1e-6f?Vector3.Normalize(a):Vector3.UnitX;var c=Vector3.Cross(a,n);
                float u=punch/1.6f;int count=soft?12:22;var vapour=NativeEntranceFx.Hex(0xdfe4ea);
                for(int i=0;i<count;i++)
                {
                    float ang=i*Tau/count+.2f*Hash(s+60,i),rad=3+10*(1-(1-u)*(1-u))*(.85f+.3f*Hash(s+61,i));var r=a*(float)Math.Cos(ang)+c*(float)Math.Sin(ang);
                    var pos=at+r*rad-n*(2*u);float k=Near(pos,eye,3,9);if(k<=0)continue;
                    fx.Smoke(pos,vapour,.6f*(1-u)*(1-u)*Smooth(punch*12)*k,2.4f+3.2f*u,u,s+62+i,floor);
                }
                float flash=Near(at,eye,10,25);if(punch<.12f&&flash>0)fx.Glow(at,white*((soft?1.4f:2.4f)*(1-punch/.12f)*flash),0,soft?3:4.5f,1,0,s+59);
            }
            if(land<0)return;
            var touch=centre+p.P1;var ground=new Vector3(touch.X,floor,touch.Z);
            // the impact: a small hot flash at its base (the blast lights carry the flash over the city), the shockwave racing out
            // low to ~6 m in a quarter second, the dust wall rolling out behind it and hanging, sparks forward
            if(land<.1f){float k=1-land/.1f;fx.Glow(ground+up*.35f,white*((soft?1.3f:2.2f)*k),0,soft?2.4f:3.2f,1,0,s+100);fx.Glow(ground+up*.2f,fire*(1.1f*k),0,soft?3:4,0,0,s+101);}
            if(land<.32f)
            {
                float r=.9f+5.4f*(1-(float)Math.Exp(-land*9)),k=1-Smooth(land/.32f);int n=soft?16:30;var ring=NativeEntranceFx.Hex(0xe8e2d6,1.6f);
                for(int i=0;i<n;i++)
                {
                    float a=(i+.4f*Hash(s+120,i))*Tau/n;var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var at=ground+d*r+up*(.1f+.06f*Hash(s+121,i));
                    fx.Streak(at,d*(26*(1-land*2.2f)+4),ring*k,.2f,.55f,.035f,.5f,0,s+130+i);
                }
            }
            NativeEntranceFx.GroundDust(fx,t,p.Impact,touch,soft?14:26,1.3f,15,1.1f,NativeEntranceFx.Hex(0x9aa0a8),.55f,.9f,s+110,quality,floor);
            NativeEntranceFx.GroundDust(fx,t,p.Impact+.03f,touch,soft?12:22,1.6f,11,3.4f,NativeEntranceFx.Hex(0x80786e),.7f,2.4f,s+140,quality,floor);
            // the dust wall: tall puffs rolling out to ~6 m and hanging there
            NativeEntranceFx.GroundDust(fx,t,p.Impact+.05f,touch,soft?10:18,2,9.5f,3.2f,NativeEntranceFx.Hex(0x8a8278),.62f,3.2f,s+190,quality,floor);
            NativeEntranceFx.Sparks(fx,t,p.Impact,touch+up*.15f,Vector3.Normalize(p.Travel+up*.6f),1.4f,soft?20:44,11,.9f,9.8f,.7f,white*3.2f,fire,.026f,s+170,quality,floor);
            if(land<p.SkidSeconds+1.6f)
            {
                // the plough: spoil thrown ahead and to the sides off the base while it skids, grit falling back
                var spoil=NativeEntranceFx.Hex(0x5e564d);int n=soft?8:16;
                for(int i=0;i<n;i++)
                {
                    float born=p.Impact+p.SkidSeconds*i/n,a=t-born,life=1.1f+.5f*Hash(s+200,i);if(a<0||a>life||Hash(s+201,i)>Math.Max(.4f,quality))continue;float u=a/life;
                    var from=centre+Ground0(p,born-p.Impact);var side=p.Side*(Hash(s+202,i)<.5f?-1:1);var v0=p.Travel*(3+4*Hash(s+203,i))+side*(2+3*Hash(s+204,i))+up*(2.5f+3*Hash(s+205,i));
                    var at=from+v0*a+new Vector3(0,-4.9f*a*a,0);if(at.Y<floor+.05f)at.Y=floor+.05f;
                    fx.Smoke(at,spoil,.55f*(1-u)*Smooth(a*20),.35f+.7f*u,u,s+206+i,floor);
                }
                if(land<p.SkidSeconds)NativeEntranceFx.Sparks(fx,t,p.Impact+.05f,centre+f.Offset+up*.1f-p.Travel*.8f,-p.Travel+up*.3f,1.1f,soft?8:18,6,.5f,9.8f,1,white*3,fire,.02f,s+220,quality,floor);
            }
            // the steam vents from the sides, as today's landing
            var podSide=new Vector3(p.Forward.Z,0,-p.Forward.X);var steam=NativeEntranceFx.Hex(0xc9ced8);
            NativeEntranceFx.Jet(fx,t,p.Impact+.1f,p.Impact+1.8f,centre+podSide*.78f+up*.5f,podSide+up*.15f,14,1.6f,1,.3f,steam,.4f,s+240,quality,floor,.5f,.18f);
            NativeEntranceFx.Jet(fx,t,p.Impact+.1f,p.Impact+1.8f,centre-podSide*.78f+up*.5f,-podSide+up*.15f,14,1.6f,1,.3f,steam,.4f,s+260,quality,floor,.5f,.18f);
            // the bolts: four flashes round the doorway, sparks and puffs; the door's blow vents steam; the ramp slams into the crater
            float bolt=t-(p.DoorAt-BoltLead);var rot=Quaternion.Multiply(f.Tilt,Facing(p));var fwd=Vector3.Transform(Vector3.UnitZ,rot);
            if(bolt>=0&&bolt<1.5f)
            {
                for(int i=0;i<4;i++)
                {
                    var at=centre+PodPoint(f,p,new Vector3(i%2==0?-.3f:.3f,i<2?.36f:1.78f,.78f));
                    if(bolt<.09f)fx.Glow(at,white*((soft?2.5f:5)*(1-bolt/.09f)),0,.55f,1,0,s+300+i);
                    NativeEntranceFx.Sparks(fx,t,p.DoorAt-BoltLead,at,fwd,1.2f,soft?4:8,5,.45f,9.8f,1.2f,white*3,fire,.016f,s+310+i*17,quality,floor);
                    if(bolt<.8f){float u=bolt/.8f;fx.Smoke(at+fwd*(.15f+.5f*u)+up*(.3f*u),NativeEntranceFx.Hex(0x6a6e76),.5f*(1-u)*Smooth(bolt*25),.25f+.5f*u,u,s+380+i,floor);}
                }
                var doorway=centre+PodPoint(f,p,new Vector3(0,1.05f,.74f));
                NativeEntranceFx.Jet(fx,t,p.DoorAt,p.DoorAt+.25f,doorway,fwd+up*.15f,70,2.2f,1.3f,.42f,NativeEntranceFx.Hex(0xb8bcc6),.5f,s+400,quality,floor,.35f,.5f);
                var tip=centre+PodPoint(f,p,NativePodModels.Hinge+new Vector3(0,0,NativePodModels.DoorHeight));tip.Y=floor;
                NativeEntranceFx.GroundDust(fx,t,p.DoorAt+p.DoorSeconds,tip,soft?6:10,.2f,3.5f,1.2f,NativeEntranceFx.Hex(0x80786e),.5f,.6f,s+470,quality,floor);
            }
            // what lasts: smoke off the hot hull (thinning to nothing by SmokeSeconds) and embers while it glows
            Lasting(fx,p,centre,t,land,s,quality,floor);
        }
        // How much of a puff or flash at `at` the eye sees: none within `from` m of it, all past `to` (it flies through them).
        private static float Near(Vector3 at,Vector3 eye,float from,float to)=>Smooth((Vector3.Distance(at,eye)-from)/(to-from));
        // The base over the rest centre `land` s after the touchdown, without the recoil (where the spoil leaves it).
        private static Vector3 Ground0(in PodPlan p,float land){float u=Clamp01(land/p.SkidSeconds);return p.P1*((1-u)*(1-u)*(1-u));}
        private static void Retros(INativeEntranceFx fx,in PodFrame f,in PodPlan p,Vector3 centre,float t,int s,float quality,float floor)
        {
            var jet=(p.Fails?NativeEntranceFx.Hex(0xffc48a,2.4f):NativeEntranceFx.Hex(0x9fd8ff,3.2f))*f.Retro;var rot=Quaternion.Multiply(f.Tilt,Facing(p));
            var down=Vector3.Transform(-Vector3.UnitY,f.Tilt);var b=centre+f.Offset;
            for(int i=0;i<4;i++)
            {
                float a=i*Pi/2;var o=b+Vector3.Transform(new Vector3(.47f*(float)Math.Sin(a),.02f,.47f*(float)Math.Cos(a)),rot);
                var d=Vector3.Normalize(down+Vector3.Transform(new Vector3((float)Math.Sin(a)*.35f,0,(float)Math.Cos(a)*.35f),rot));
                fx.Streak(o+d*.55f,d*10,jet,.17f,.7f,.05f,.9f,0,s+70+i);fx.Glow(o,jet*.6f,0,.65f,1,0,s+74+i);
            }
            float above=f.Offset.Y;if(above<7)fx.Glow(new Vector3(b.X,floor+.06f,b.Z),jet*(.4f*(1-above/7)),0,3,0,0,s+78);
            if(!p.Fails)return;
            // a failing set: sparks spat out of it and dark smoke coughed into the air behind it
            NativeEntranceFx.Sparks(fx,t,p.RetroAt+.04f,b,down,1.3f,16,7,.5f,9.8f,1,Vector3.One*3,NativeEntranceFx.Hex(0xff7a2a,2),.02f,s+80,quality,floor);
            for(int k=0;k<10;k++)
            {
                float born=p.RetroAt+k*.045f,a=t-born,life=1.4f;if(a<0||a>life||born>p.Impact)continue;float u=a/life;
                var at=centre+FlightAt(p,born,out _)+Direction(s+90,k)*(.3f+.8f*u)+new Vector3(0,.6f*a,0);
                fx.Smoke(at,NativeEntranceFx.Hex(0x24262b),.6f*(1-u)*Smooth(a*20),.5f+1.2f*u,u,s+91+k,floor);
            }
        }
        // The contrail: smoke puffs laid along the line it flew (born on a fixed schedule), growing and thinning in the sky.
        private static void Contrail(INativeEntranceFx fx,in PodPlan p,Vector3 centre,float t,int s,float quality,float floor,Vector3 eye)
        {
            const float Rate=20,Life=3.2f;float to=Math.Min(t,p.Impact-.05f);if(to<p.Appear)return;
            int first=(int)Math.Max(0,Math.Floor((t-Life-p.Appear)*Rate)),last=(int)Math.Floor((to-p.Appear)*Rate);var smoke=NativeEntranceFx.Hex(0x585c66);
            for(int k=first;k<=last;k++)
            {
                if(Hash(s+40,k)>Math.Max(.35f,quality))continue;float born=p.Appear+k/Rate,a=t-born,life=Life*(.75f+.25f*Hash(s+41,k));if(a<0||a>life)continue;float u=a/life;
                var at=centre+FlightAt(p,born,out _)+Direction(s+42,k)*(.3f+1.2f*u)+new Vector3(0,.4f*a,0);float hot=1-.4f*(born-p.Appear)/(p.Impact-p.Appear),n=Near(at,eye,2.5f,8);if(n<=0)continue;
                fx.Smoke(at,smoke,.55f*Smooth(u*5)*(1-u)*hot*n,1.6f+3.4f*Smooth(u),u,s+43+k,floor);
            }
        }
        /// <summary>The cloud deck round pod `p`'s line (DeckShort..DeckLong m up, where it punches through): a patch of broad
        /// puffs, there from the countdown's start, torn open round the punch (the hole races out with the vapour ring) and
        /// thinning away after it.</summary>
        public const int DeckPuffs=18;
        private static void Deck(INativeEntranceFx fx,in PodPlan p,Vector3 centre,float t,int s,float quality,float floor,Vector3 eye)
        {
            float after=t-p.Punch,life=Smooth((t-p.Appear)/.35f)*(1-Smooth((after-1.2f)/1.8f));if(!(life>.01f))return;
            var at=centre+FlightAt(p,p.Punch,out _);var deck=NativeEntranceFx.Hex(0x9aa2b4);float tear=after>0?9*(1-(float)Math.Exp(-after*2.2f)):0;
            int count=quality<.5f?10:DeckPuffs;
            for(int i=0;i<count;i++)
            {
                float a=Hash(s+700,i)*Tau,r=5+24*(float)Math.Sqrt(Hash(s+701,i));var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
                // torn open: the puffs near the hole pushed out with the ring
                float push=tear*Math.Max(0,1-r/18);var pos=at+d*(r+push)+new Vector3(0,(Hash(s+702,i)-.5f)*3,0);
                float k=Near(pos,eye,6,16);if(k<=0)continue;
                fx.Smoke(pos,deck,(.3f+.14f*Hash(s+703,i))*life*k,9+7*Hash(s+704,i),.35f+.3f*Hash(s+705,i),s+706+i,floor);
            }
        }
        // The smoke rising off the hull for SmokeSeconds (puffs on a fixed schedule, each kept by the smoke level at its birth) and
        // the embers over the crater while the tiles glow.
        private static void Lasting(INativeEntranceFx fx,in PodPlan p,Vector3 centre,float t,float land,int s,float quality,float floor)
        {
            const float Rate=6,Life=4.5f;float from=p.Impact+.3f;if(t<from)return;var smoke=NativeEntranceFx.Hex(0x4a4c52);
            int first=(int)Math.Max(0,Math.Floor((t-Life-from)*Rate)),last=(int)Math.Floor((Math.Min(t,p.Impact+SmokeSeconds)-from)*Rate);
            for(int k=first;k<=last;k++)
            {
                float born=from+k/Rate,a=t-born,life=Life*(.8f+.2f*Hash(s+500,k)),level=SmokeLevel(born-p.Impact);if(a<0||a>life||Hash(s+501,k)>level*Math.Max(.5f,quality))continue;float u=a/life;
                float ang=Hash(s+502,k)*Tau,r=.35f+.4f*Hash(s+503,k);var at=centre+new Vector3(r*(float)Math.Cos(ang),1.4f+1.1f*Hash(s+504,k),r*(float)Math.Sin(ang))+new Vector3(.35f*a,1.1f*a,.12f*a);
                fx.Smoke(at,smoke,.42f*(1-u)*Smooth(a*3),.7f+2.3f*u,u,s+505+k,floor);
            }
            float hot=Cool(land);if(hot>.15f)NativeEntranceFx.Embers(fx,t,p.Impact+.2f,p.Impact+CoolSeconds,centre+new Vector3(0,.3f,0),1.2f,5,1.4f,1.6f,NativeEntranceFx.Hex(0xff8a3a,1.8f)*hot,.1f,s+600,quality,.3f);
        }

        /// <summary>
        /// Pod `p`'s burning streak for frame `f` as one unfogged additive ribbon and a head glow into `sink` (NativeGlow.shader:
        /// drawn past the fog like a meteor, so the four streaks read over the city from far off), along the curve it flew over its
        /// last MeteorSeconds, camera-facing about `eye`: hot and wide at range from its first frame; close up a continuous plasma
        /// sheath trailing the pod, thinning away within a few metres of the eye (no sheet over the screen). Before the touchdown
        /// only. Returns the vertices written.
        /// </summary>
        public static int Meteor(ISpawnInSink sink,in PodFrame f,in PodPlan p,Vector3 centre,Vector3 eye,float gain)
        {
            if(sink==null||f.Gone||!f.Dropping||f.Land>=0||!(f.Heat>.03f)||!(gain>0))return 0;const int N=22;float t=f.Age;int written=0,pa=-1,pb=-1;
            var head=centre+f.Offset;float level=f.Heat*gain;if(level<.01f)return 0;
            var hot=new Vector3(1,.86f,.62f)*3.2f;var ember=new Vector3(1,.38f,.1f)*1.6f;
            for(int i=0;i<=N;i++)
            {
                float back=Math.Max(p.Appear,t-MeteorSeconds*i/N);var at=centre+FlightAt(p,back,out var v);var tangent=v.LengthSquared()>1e-6f?Vector3.Normalize(v):-Vector3.UnitY;
                var side=Vector3.Cross(tangent,eye-at);float sl=side.Length();if(sl<1e-4f)continue;float d=Vector3.Distance(at,eye),k=i/(float)N,fade=Smooth((d-3.5f)/6);
                float width=Math.Max(.9f+1.5f*k,d*.014f*(1+.8f*k));side*=width*.5f/sl;var col=new Vector4(Vector3.Lerp(hot,ember,Smooth(k*1.3f)),level*(float)Math.Pow(1-k,1.4)*fade);
                int va=sink.Vertex(at-side,new Vector3(-1,0,1),col),vb=sink.Vertex(at+side,new Vector3(1,0,1),col);written+=2;
                if(pa>=0){sink.Triangle(pa,pb,vb);sink.Triangle(pa,vb,va);}pa=va;pb=vb;
            }
            var view=eye-head;float vl=view.Length();if(vl<1e-3f)return written;view/=vl;var right=Vector3.Cross(Vector3.UnitY,view);right=right.LengthSquared()>1e-6f?Vector3.Normalize(right):Vector3.UnitX;var upv=Vector3.Cross(view,right);
            float size=Math.Max(1.6f,vl*.032f),near=Smooth((vl-3)/7);if(near<=0)return written;var c=new Vector4(hot,.9f*level*near);
            int a0=sink.Vertex(head-right*size-upv*size,new Vector3(-1,-1,0),c),a1=sink.Vertex(head+right*size-upv*size,new Vector3(1,-1,0),c),a2=sink.Vertex(head+right*size+upv*size,new Vector3(1,1,0),c),a3=sink.Vertex(head-right*size+upv*size,new Vector3(-1,1,0),c);
            sink.Triangle(a0,a1,a2);sink.Triangle(a0,a2,a3);return written+4;
        }
        /// <summary>The meteor ribbon's length in time back along the line.</summary>
        public const float MeteorSeconds=.55f;

        // ---- my own camera: the directed drop ----
        /// <summary>The director's modes: the chase in third person, the braced cabin in first person, the still wide shot
        /// (Reduced effects).</summary>
        public const int ShotChase=0,ShotFirst=1,ShotStill=2;
        /// <summary>The still shot stands on the plaza: within this of the hub's centre on both axes (the sanctuary's fence is at
        /// 24.8 m).</summary>
        public const float PlazaHalf=21;
        /// <summary>
        /// My camera `age` s from GO over my pod `p` (its rest `centre`): the sky shot high over the camp, looking down past it
        /// toward my pod's streak as it comes burning in (the tilt follows it down to the camp); a cut to the chase behind my pod
        /// (in first person, first one braced second in my red-lit cabin, my pod door down so the city rushes up in the porthole,
        /// then its roll door forward as the retros light); the chase slowing to a stop 10 m up while the pod punches in below it;
        /// the hold on the crash; the arc down behind my pilot (third person); in first person the arc round to the doorway (my
        /// pilot unbuckling and standing in the open pod) and a cut into my own eyes (`gameEye`), looking out of the door. Directed
        /// falls from 1 to 0 over the plan's HandOff before GO, where the game's own camera is blended back in. The still shot
        /// holds a wide view from the plaza beside the approach and glides behind my pilot at the end (in first person, `first`,
        /// it holds and cuts into my eyes). Shake on top is Shake's; Judder is the cabin's buffeting for the director to turn by.
        /// </summary>
        public static PodShot Shot(in PodPlan p,Vector3 centre,float age,int mode,Vector3 gameEye,bool first=false)
        {
            var up=Vector3.UnitY;var shot=new PodShot{Up=up,Fov=60,Directed=1-Smooth((age+p.HandOff)/p.HandOff)};
            bool fp=mode==ShotFirst||mode==ShotStill&&first;
            var endEye=centre-p.Forward*4.3f+up*3;var endLook=centre+p.Forward*6+up*1.1f;var eyeLook=gameEye+p.Forward*10-up*1.4f;
            // in first person, from the cut: in my own eyes, looking out of the door
            if(fp&&age>=p.CutAt){shot.Eye=gameEye;shot.Look=eyeLook;shot.Fov=75;shot.Inside=true;return shot;}
            if(mode==ShotStill)
            {
                // beside the approach on the plaza (the other side when this one stands past the fence, never past PlazaHalf)
                float side=Hash(p.Seed,30)<.5f?-1:1;var eye=centre+p.Side*(13*side)-p.Travel*4+up*2.4f;var look=centre-p.Travel*2.5f+up*3.2f;
                if(Math.Max(Math.Abs(eye.X),Math.Abs(eye.Z))>PlazaHalf)eye=centre-p.Side*(13*side)-p.Travel*4+up*2.4f;
                eye.X=Math.Max(-PlazaHalf,Math.Min(PlazaHalf,eye.X));eye.Z=Math.Max(-PlazaHalf,Math.Min(PlazaHalf,eye.Z));
                float g=fp?0:Smooth((age-p.ArcFrom)/Math.Max(.3f,p.ArcTo-p.ArcFrom));shot.Eye=Vector3.Lerp(eye,endEye,g);shot.Look=Vector3.Lerp(look,endLook,g);shot.Fov=L(62,60,g);return shot;
            }
            if(age<p.Ride)
            {
                // the sky: high over the camp, back past it from my pod's line, looking down past the camp toward my pod's streak;
                // the view tilts down after it (it rides high in the frame) until the camp fills the lower half
                var pod=centre+FlightAt(p,Math.Max(p.Appear,age),out _);var back=new Vector3(p.Travel.X,0,p.Travel.Z);back=back.LengthSquared()>1e-6f?Vector3.Normalize(back):Vector3.UnitZ;
                shot.Eye=centre+back*SkyBack+up*SkyUp;var to=pod-shot.Eye;float h=new Vector2(to.X,to.Z).Length(),el=(float)Math.Atan2(to.Y,h)-26*Deg;
                el=Math.Min(el,-30*Deg);var flat=h>1e-3f?new Vector3(to.X,0,to.Z)/h:-back;
                shot.Look=shot.Eye+(flat*(float)Math.Cos(el)+up*(float)Math.Sin(el))*40;shot.Fov=68;return shot;
            }
            var f=Evaluate(age,p);
            if(mode==ShotFirst&&age<p.CabinEnd&&age<p.Stop)
            {
                // braced in the cabin, in the jump seat: my eye at the seat, looking at the door's porthole (door down: the city in it,
                // until it rolls door forward), turning with the pod, buffeting
                var rot=Quaternion.Multiply(f.Tilt,Facing(p));shot.Eye=centre+PodPoint(f,p,CabinEyeAt);
                var porthole=new Vector3(0,NativePodModels.Hinge.Y+NativePodModels.PortholeY-.08f,NativePodModels.Hinge.Z);
                shot.Look=centre+PodPoint(f,p,porthole);shot.Up=Vector3.Normalize(up+Vector3.Transform(up,rot));shot.Fov=72;shot.Cabin=shot.Inside=true;
                float u=Clamp01((age-p.Ride)/(p.CabinEnd-p.Ride));shot.Judder=.55f+.45f*(float)Math.Sin(Pi*u)+(age>=p.FlipFrom&&age<p.FlipTo+.1f?.4f:0);return shot;
            }
            var touch=centre+p.P1;var stop=touch-p.Travel*6+up*10;
            if(age<p.Stop)
            {
                // the chase: behind and above my pod along its line, looking ahead of it
                var pos=centre+FlightAt(p,age,out var v);var d=Vector3.Normalize(v);shot.Eye=pos-d*ChaseBack+up*ChaseUp;shot.Look=pos+d*6+up*1.2f;shot.Fov=L(66,60,Smooth((age-p.Ride)/(p.Stop-p.Ride)));return shot;
            }
            if(age<p.Impact)
            {
                // the stop: the chase eases to a halt 10 m up behind the touchdown; the pod punches in below it
                var pos=centre+FlightAt(p,p.Stop,out var v);var d=Vector3.Normalize(v);var from=pos-d*ChaseBack+up*ChaseUp;float T=p.Impact-p.Stop;
                var chord=stop-from;float most=2.5f*chord.Length()/Math.Max(1e-3f,v.Length()*T);var v0=v*Math.Min(1,most);
                shot.Eye=Hermite(from,v0,stop,Vector3.Zero,T,Clamp01((age-p.Stop)/T),out _);
                var now=centre+f.Offset;shot.Look=Vector3.Lerp(pos+d*6+up*1.2f,now+up*.8f,Smooth((age-p.Stop)/T));shot.Fov=60;return shot;
            }
            var pod2=centre+f.Offset+up*1.2f;
            if(age<p.ArcFrom){shot.Eye=stop;shot.Look=pod2;shot.Fov=58;return shot;}
            float g2=Clamp01((age-p.ArcFrom)/Math.Max(.05f,p.ArcTo-p.ArcFrom)),e=g2*g2*g2*(g2*(g2*6-15)+10);
            if(mode==ShotFirst)
            {
                // first person: up and over my pod and round to its doorway, a few metres out to one side, looking in at my pilot as
                // the door blows into a ramp and they unbuckle and stand (outside the hull all the way); the cut comes at CutAt
                var front=centre+p.Forward*3.5f+p.Side*(1.6f*p.FrontSide)+up*1.8f;var over=centre+up*7.5f+p.Side*(4.5f*p.FrontSide)-p.Travel*1.5f;
                shot.Eye=(1-e)*(1-e)*stop+2*(1-e)*e*over+e*e*front;shot.Look=Vector3.Lerp(pod2,centre+up*1.15f+p.Forward*.15f,Smooth(g2*1.4f));shot.Fov=L(58,62,e);
                return shot;
            }
            // the arc down behind my pilot: a swoop from the stop to the end pose, the look easing from the pod to ahead
            var q=endEye+up*4+(stop-centre)*.25f;shot.Eye=(1-e)*(1-e)*stop+2*(1-e)*e*q+e*e*endEye;shot.Look=Vector3.Lerp(pod2,endLook,Smooth(g2*1.2f));shot.Fov=L(58,60,e);
            return shot;
        }
        /// <summary>
        /// The camera shake (m) pod `p`'s crash puts on an eye `distance` m from its touchdown `age` s from GO
        /// (NativeStructureShake's land peak and fall-off, harder for a failing pod); `mine` adds my own pod's ride: its rumble
        /// while it burns in, the cloud punch, the slam and the door's bolts.
        /// </summary>
        public static float Shake(in PodPlan p,float age,float distance,bool mine)
        {
            float a=0,land=age-p.Impact;
            if(land>=0&&land<NativeStructureShake.LandSeconds)a+=NativeStructureShake.Peak(NativeStructureCue.Land,p.Fails?1:.75f)*NativeStructureShake.Falloff(distance)*(mine?1.6f:1.2f)*NativeStructureShake.Envelope(false,land,NativeStructureShake.LandSeconds);
            if(!mine)return a;
            if(land<0&&age>=p.Ride){float u=Clamp01((age-p.Ride)/(p.Impact-p.Ride));a+=.012f+.03f*u;}
            float punch=age-p.Punch;if(punch>=0&&punch<.5f)a+=.09f*NativeStructureShake.Envelope(false,punch,.5f);
            float bolt=age-(p.DoorAt-BoltLead);if(bolt>=0&&bolt<.3f)a+=.025f*NativeStructureShake.Envelope(false,bolt,.3f);
            return Math.Min(NativeStructureShake.MaxOffset,a);
        }
        /// <summary>The shake's smooth noise on channel `ch` (-1..1), keyed by `seed` and the clock (NativeStructureFx's).</summary>
        public static float ShakeNoise(int seed,int ch,float x){int i=(int)Math.Floor(x);float f=x-i,u=f*f*(3-2*f),a=Hash(seed+ch*7919,i)*2-1,b=Hash(seed+ch*7919,i+1)*2-1;return a+(b-a)*u;}
        /// <summary>The cabin's red at the edges of my screen while the braced second holds (0..1): in with the cut, out at its end.</summary>
        public static float CabinRed(in PodPlan p,float age){float u=(age-p.Ride)/Math.Max(.05f,p.CabinEnd-p.Ride);return u<0||u>1?0:Clamp01(Math.Min(u/.12f,(1-u)/.18f));}
        /// <summary>The cabin's buffeting as a turn of my view (degrees of pitch and roll) at `age` for judder `k` (0..1).</summary>
        public static void Judder(int seed,float age,float k,out float pitch,out float roll)
        {
            pitch=(ShakeNoise(seed,5,age*31)*.7f+ShakeNoise(seed,6,age*13)*.3f)*1.3f*k;roll=(ShakeNoise(seed,7,age*27)*.6f+ShakeNoise(seed,8,age*11)*.4f)*2f*k;
        }
        /// <summary>
        /// How far pod `p`'s crater (its fading soot and cracks, NativePodModels.Outline) may be scaled (scale .5..1) so that no
        /// keep-out rectangle (`keep`: centre x, z, half extents and yaw in radians, each grown by `margin`) comes inside it, round
        /// the rest centre (x, z): a pod beside the REVIVE-O-MATIC's mat keeps its soot off the mat.
        /// </summary>
        public static float CraterFit(in PodPlan p,float x,float z,(float x,float z,float hx,float hz,float yaw)[] keep,float margin=.15f)
        {
            if(keep==null||keep.Length==0)return 1;
            var tr=new Vector2(p.Travel.X,p.Travel.Z);if(tr.LengthSquared()<1e-6f)return 1;tr=Vector2.Normalize(tr);
            float turn=(float)Math.Atan2(tr.X,tr.Y)+p.CraterTurn,cx=p.CraterX,cz=p.CraterZ;
            float c=(float)Math.Cos(turn),sn=(float)Math.Sin(turn);
            bool Inside(float scale)
            {
                // the crater's outline against each grown rectangle, and each rectangle's corners and edge midpoints against the crater
                for(int i=0;i<64;i++)
                {
                    float a=i*Tau/64,r=NativePodModels.Outline(a)*scale;
                    // the model's (x across, z along) scaled by the plan, turned so its +z runs along the travel (its tail behind)
                    float lx=(float)Math.Cos(a)*r*cx,lz=(float)Math.Sin(a)*r*cz,wx=x+lx*c+lz*sn,wz=z-lx*sn+lz*c;
                    foreach(var k in keep)
                    {
                        float dx=wx-k.x,dz=wz-k.z,kc=(float)Math.Cos(k.yaw),ks=(float)Math.Sin(k.yaw);
                        if(Math.Abs(dx*kc-dz*ks)<=k.hx+margin&&Math.Abs(dx*ks+dz*kc)<=k.hz+margin)return true;
                    }
                }
                foreach(var k in keep)
                {
                    float kc=(float)Math.Cos(k.yaw),ks=(float)Math.Sin(k.yaw),hx=k.hx+margin,hz=k.hz+margin;
                    for(int j=0;j<9;j++)
                    {
                        if(j==4)continue;float u=(j%3-1)*hx,v=(j/3-1)*hz,px=k.x+u*kc+v*ks-x,pz=k.z-u*ks+v*kc-z;
                        float ux=(px*c-pz*sn)/cx,uz=(px*sn+pz*c)/cz;if((float)Math.Sqrt(ux*ux+uz*uz)<NativePodModels.Outline((float)Math.Atan2(uz,ux))*scale)return true;
                    }
                }
                return false;
            }
            if(!Inside(1))return 1;float lo=.5f,hi=1;if(Inside(lo))return lo;
            for(int i=0;i<14;i++){float m=(lo+hi)*.5f;if(Inside(m))hi=m;else lo=m;}return lo;
        }
        /// <summary>The REVIVE-O-MATIC's footprint the crater keeps off (its solids, its mat and the spot its user stands on) for the
        /// machine at `at` turned `turn`: centre, half extents and yaw, as A0's landing field reserves them (NativeExpeditionHubPlan).</summary>
        public static (float x,float z,float hx,float hz,float yaw)[] ReviveKeepOut(Vector3 at,float turn)
        {
            var frame=NativeReviveMachine.Frame(at,turn);var o=new (float x,float z,float hx,float hz,float yaw)[NativeReviveMachine.Solids.Length+2];int n=0;
            foreach(var s in NativeReviveMachine.Solids){var c=Vector3.Transform(s.centre,frame);o[n++]=(c.X,c.Z,s.size.X/2,s.size.Z/2,turn+s.yaw);}
            var mat=Vector3.Transform(NativeReviveMachineModels.MatAt,frame);o[n++]=(mat.X,mat.Z,NativeReviveMachineModels.MatSize/2,NativeReviveMachineModels.MatSize/2,turn);
            var use=Vector3.Transform(NativeReviveMachine.UsePoint,frame);o[n++]=(use.X,use.Z,.45f,.45f,turn);
            return o;
        }

        // ---- the pod's look: paint, trim, lights (the Locker's Pod tab; prefs.pod) ----
        /// <summary>The look's entries, in canonical order: "paint:rrggbb,trim:rrggbb,light:rrggbb" (six lowercase hex digits,
        /// only the ones set; "" the default: the pilot's body colour, gunmetal trim, the pilot's accent in the lights).</summary>
        public static readonly string[] Keys={"paint","trim","light"};
        public const int DefaultTrim=0x3a4152;
        /// <summary>The Locker's swatches for the paint and trim (the lights use the accent swatches).</summary>
        public static readonly int[] Paints={0xe5e7eb,0x2a2f3a,0xd9433b,0xf08a24,0xf2c94c,0x58c472,0x2fb5a8,0x3a86d9,0x6c5ce7,0xd65db1,0x8a6a4f,0x0f1115};
        public static string Normalize(string look)
        {
            if(string.IsNullOrEmpty(look))return "";var v=new int[Keys.Length];for(int i=0;i<v.Length;i++)v[i]=-1;
            foreach(var entry in look.Split(','))
            {
                int colon=entry.IndexOf(':');if(colon<=0)continue;int k=Array.IndexOf(Keys,entry.Substring(0,colon).Trim());if(k<0)continue;
                int rgb=Hex(entry.Substring(colon+1).Trim());if(rgb>=0)v[k]=rgb;
            }
            return Join(v);
        }
        /// <summary>The look's entry `key` (0xRRGGBB), or -1 when it has none.</summary>
        public static int Of(string look,string key)
        {
            if(string.IsNullOrEmpty(look))return -1;int result=-1;
            foreach(var entry in look.Split(',')){int colon=entry.IndexOf(':');if(colon>0&&entry.Substring(0,colon).Trim()==key){int rgb=Hex(entry.Substring(colon+1).Trim());if(rgb>=0)result=rgb;}}
            return result;
        }
        /// <summary>The look with `key` set to `rgb` (negative: back to the default), normalized.</summary>
        public static string With(string look,string key,int rgb)
        {
            int k=Array.IndexOf(Keys,key);look=Normalize(look);if(k<0)return look;var v=new int[Keys.Length];for(int i=0;i<v.Length;i++)v[i]=Of(look,Keys[i]);v[k]=rgb<0?-1:rgb&0xffffff;return Join(v);
        }
        /// <summary>The colours a pod draws in (0xRRGGBB): the look's, else the pilot's body colour (paint), gunmetal (trim) and
        /// the pilot's accent (lights). `body` and `accent` are the seat's "#rrggbb" strings.</summary>
        public static void Colors(string look,string body,string accent,out int paint,out int trim,out int light)
        {
            paint=Of(look,"paint");trim=Of(look,"trim");light=Of(look,"light");
            if(paint<0)paint=Hex((body??"").TrimStart('#'));if(paint<0)paint=0xe5e7eb;if(trim<0)trim=DefaultTrim;
            if(light<0)light=Hex((accent??"").TrimStart('#'));if(light<0)light=0x70e1d0;
        }
        private static string Join(int[] v){var sb=new System.Text.StringBuilder();for(int i=0;i<Keys.Length;i++){if(v[i]<0)continue;if(sb.Length>0)sb.Append(',');sb.Append(Keys[i]).Append(':').Append(v[i].ToString("x6"));}return sb.ToString();}
        private static int Hex(string s)
        {
            if(s==null||s.Length!=6)return -1;int v=0;foreach(char c in s){int d=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(d<0)return -1;v=v*16+d;}return v;
        }

        // ---- the LED name sign ----
        /// <summary>The sign's dot grid: columns across the lit face (ten letters of 5 + 1 dots), rows (7 and a margin each side).</summary>
        public const int SignColumns=60,SignRows=9,MaxName=20;
        /// <summary>The name as the sign spells it: upper case, letters the font lacks as '?', trimmed to MaxName; "PILOT" when
        /// empty.</summary>
        public static string SignText(string name)
        {
            var sb=new System.Text.StringBuilder();foreach(char c0 in (name??"").Trim()){char c=char.ToUpperInvariant(c0);sb.Append(Glyph(c)!=null?c:'?');if(sb.Length>=MaxName)break;}
            var t=sb.ToString().Trim();return t.Length==0?"PILOT":t;
        }
        /// <summary>The sign's dot columns for `text` (a letter 5 dots and 1 gap); text wider than the sign scrolls.</summary>
        public static int TextColumns(string text)=>Math.Max(0,(text??"").Length*6-1);
        public static bool Scrolls(string text)=>TextColumns(text)>SignColumns;
        /// <summary>
        /// The sign as an LED dot texture laid along v (Runtime scrolls it along the sign: the screen shader scrolls v), RGBA
        /// bytes, bottom row first: `dot` pixels per LED, round lit dots in white and the unlit ones faint. Width SignRows x dot;
        /// height (the sign's length) the sign's columns (or the text's and a gap, when it scrolls) x dot. Column 0 is the
        /// sign's left end at v 0.
        /// </summary>
        public static byte[] SignPixels(string text,int dot,out int width,out int height)
        {
            text=text??"";bool scroll=Scrolls(text);int cols=scroll?TextColumns(text)+12:SignColumns,start=scroll?0:(SignColumns-TextColumns(text))/2;
            width=SignRows*dot;height=cols*dot;var o=new byte[width*height*4];float r=dot*.42f,c=(dot-1)*.5f;
            for(int col=0;col<cols;col++)for(int row=0;row<SignRows;row++)
            {
                bool lit=Lit(text,col-start,row-1);byte a=lit?(byte)255:(byte)24;
                for(int py=0;py<dot;py++)for(int px=0;px<dot;px++)
                {
                    float dx=px-c,dy=py-c,d=(float)Math.Sqrt(dx*dx+dy*dy);float k=Clamp01((r-d)/1.2f+.5f);if(k<=0)continue;
                    // the texture's x runs across the sign (its rows, top row at x 0), y along it (columns)
                    int x=row*dot+px,y=col*dot+py,i=(y*width+x)*4;o[i]=o[i+1]=o[i+2]=255;o[i+3]=(byte)(a*k);
                }
            }
            return o;
        }
        /// <summary>The sign laid along x (the Locker's preview card): the first SignColumns dots of the text, centred when it is
        /// shorter, RGBA bytes bottom row first; Width SignColumns x dot, height SignRows x dot.</summary>
        public static byte[] SignPixelsFlat(string text,int dot,out int width,out int height)
        {
            text=text??"";int start=Math.Max(0,(SignColumns-TextColumns(text))/2);width=SignColumns*dot;height=SignRows*dot;var o=new byte[width*height*4];float r=dot*.42f,c=(dot-1)*.5f;
            for(int col=0;col<SignColumns;col++)for(int row=0;row<SignRows;row++)
            {
                bool lit=Lit(text,col-start,row-1);byte a=lit?(byte)255:(byte)24;
                for(int py=0;py<dot;py++)for(int px=0;px<dot;px++)
                {
                    float dx=px-c,dy=py-c,d=(float)Math.Sqrt(dx*dx+dy*dy);float k=Clamp01((r-d)/1.2f+.5f);if(k<=0)continue;
                    int x=col*dot+px,y=(SignRows-1-row)*dot+py,i=(y*width+x)*4;o[i]=o[i+1]=o[i+2]=255;o[i+3]=(byte)(a*k);
                }
            }
            return o;
        }
        /// <summary>Whether text column `col` (0 the first letter's left), row `row` (0 the top of a letter) is lit.</summary>
        public static bool Lit(string text,int col,int row)
        {
            if(col<0||row<0||row>6||text==null)return false;int i=col/6,x=col%6;if(i>=text.Length||x==5)return false;var g=Glyph(text[i]);
            return g!=null&&(g[row]>>(4-x)&1)!=0;
        }
        // A 5 x 7 font: rows top to bottom, five bits each (bit 4 the left column).
        private static byte[] Glyph(char c)
        {
            if(c>='A'&&c<='Z')return Letters[c-'A'];if(c>='0'&&c<='9')return Digits[c-'0'];
            switch(c){case ' ':return Blank;case '-':return Dash;case '_':return Under;case '.':return Dot;case '!':return Bang;case '?':return Query;case '\'':return Tick;case '#':return Hash5;case '*':return Star;case '+':return Plus;case '/':return Slash;case ':':return Colon;}
            return null;
        }
        private static readonly byte[] Blank={0,0,0,0,0,0,0},Dash={0,0,0,0x1F,0,0,0},Under={0,0,0,0,0,0,0x1F},Dot={0,0,0,0,0,0x0C,0x0C},Bang={0x04,0x04,0x04,0x04,0x04,0,0x04},
            Query={0x0E,0x11,0x01,0x02,0x04,0,0x04},Tick={0x04,0x04,0x08,0,0,0,0},Hash5={0x0A,0x0A,0x1F,0x0A,0x1F,0x0A,0x0A},Star={0,0x04,0x15,0x0E,0x15,0x04,0},
            Plus={0,0x04,0x04,0x1F,0x04,0x04,0},Slash={0x01,0x01,0x02,0x04,0x08,0x10,0x10},Colon={0,0x0C,0x0C,0,0x0C,0x0C,0};
        private static readonly byte[][] Letters={
            new byte[]{0x0E,0x11,0x11,0x1F,0x11,0x11,0x11},new byte[]{0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E},new byte[]{0x0E,0x11,0x10,0x10,0x10,0x11,0x0E},new byte[]{0x1E,0x11,0x11,0x11,0x11,0x11,0x1E},
            new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F},new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x10},new byte[]{0x0E,0x11,0x10,0x17,0x11,0x11,0x0F},new byte[]{0x11,0x11,0x11,0x1F,0x11,0x11,0x11},
            new byte[]{0x0E,0x04,0x04,0x04,0x04,0x04,0x0E},new byte[]{0x07,0x02,0x02,0x02,0x02,0x12,0x0C},new byte[]{0x11,0x12,0x14,0x18,0x14,0x12,0x11},new byte[]{0x10,0x10,0x10,0x10,0x10,0x10,0x1F},
            new byte[]{0x11,0x1B,0x15,0x15,0x11,0x11,0x11},new byte[]{0x11,0x11,0x19,0x15,0x13,0x11,0x11},new byte[]{0x0E,0x11,0x11,0x11,0x11,0x11,0x0E},new byte[]{0x1E,0x11,0x11,0x1E,0x10,0x10,0x10},
            new byte[]{0x0E,0x11,0x11,0x11,0x15,0x12,0x0D},new byte[]{0x1E,0x11,0x11,0x1E,0x14,0x12,0x11},new byte[]{0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E},new byte[]{0x1F,0x04,0x04,0x04,0x04,0x04,0x04},
            new byte[]{0x11,0x11,0x11,0x11,0x11,0x11,0x0E},new byte[]{0x11,0x11,0x11,0x11,0x11,0x0A,0x04},new byte[]{0x11,0x11,0x11,0x15,0x15,0x15,0x0A},new byte[]{0x11,0x11,0x0A,0x04,0x0A,0x11,0x11},
            new byte[]{0x11,0x11,0x11,0x0A,0x04,0x04,0x04},new byte[]{0x1F,0x01,0x02,0x04,0x08,0x10,0x1F}};
        private static readonly byte[][] Digits={
            new byte[]{0x0E,0x11,0x13,0x15,0x19,0x11,0x0E},new byte[]{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E},new byte[]{0x0E,0x11,0x01,0x02,0x04,0x08,0x1F},new byte[]{0x1E,0x01,0x01,0x0E,0x01,0x01,0x1E},
            new byte[]{0x02,0x06,0x0A,0x12,0x1F,0x02,0x02},new byte[]{0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E},new byte[]{0x06,0x08,0x10,0x1E,0x11,0x11,0x0E},new byte[]{0x1F,0x01,0x02,0x04,0x08,0x08,0x08},
            new byte[]{0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E},new byte[]{0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C}};

        // ---- hashing ----
        private static float Hash(int a,int b){unchecked{uint h=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+0x27D4EB2Fu;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h>>8)*(1f/16777216f);}}
        private static float Signed(int a,int b)=>Hash(a,b)*2-1;
        private static Vector3 Direction(int a,int b){var v=new Vector3(Signed(a,b*3),Signed(a,b*3+1),Signed(a,b*3+2));return v.LengthSquared()>1e-6f?Vector3.Normalize(v):Vector3.UnitY;}
    }
}
