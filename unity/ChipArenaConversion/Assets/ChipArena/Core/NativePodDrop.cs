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
        /// <summary>The harness bar: 0 down over the pilot's chest, 1 up.</summary>
        public float Harness;
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
        /// stands, the drop is over; the camera's stop over the crash, its arc down, and its hand-off back to the game's camera.</summary>
        public float Appear,Ride,CabinEnd,Punch,RetroAt,Impact,DoorAt,DoorSeconds,StandFrom,StandEnd,End,Stop,ArcFrom,ArcTo,HandOff;
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
    }

    /// <summary>The directed drop's camera at one moment (NativePodDrop.Shot): where it looks from and at, its up, its field of
    /// view, how much of the frame is directed (1; below it the game's own camera is blended back in, the hand-off at GO), and
    /// whether the eye rides my cabin (first person: my own body stays hidden).</summary>
    public struct PodShot{public Vector3 Eye,Look,Up;public float Fov,Directed;public bool Cabin;}

    /// <summary>
    /// The Expedition drop pods (owner 2026-11-02: "When all players load in, we essentially drop in to the hub from a pod. (This
    /// pod is how players can choose to leave as well) using their OWN pod. (Can have an LED sign with the player name)"; the
    /// crash: docs/EXPEDITION-OVERHAUL.md R7 and W4; the model is NativePodModels, the draw Runtime/NativePodPresentation.cs). In a
    /// generated run's hub the pods take the place of the spawn-in beam (NativeSpawnIn) over the same clock: the run's opening
    /// countdown, which already holds every pilot at its spawn, so nothing here changes a rule, a timing or the network.
    ///  - Each pod comes in on its own line (hashed from its record: seat, order, x, z, end), from behind its door within ±40°:
    ///    a burning streak high over the city (drawn past the fog like a meteor), a cloud punch and its boom, the retros flaring
    ///    (two pods in four sputter and fail and come in harder), and the impact at Impact (staggered by order): it touches down
    ///    at most MaxSkid short of its pod centre at 45-60°, ploughs in and comes to rest on the centre, sunk Sink and leaning at
    ///    most MaxLean (inside the collider envelope A1 will give it). Slam, shake, the shockwave and the dust wall, sparks and a
    ///    flash through the blast lights.
    ///  - The explosive bolts blow the door down into a ramp; the pilot, strapped in on the pod's full offset and lean, unbuckles
    ///    (the harness bar lifts) and stands up on the capsule by GO. The capsule never moves.
    ///  - What lasts, from the record and the clock: the crater, the scorch and the furrow, the hull cooling from white hot to
    ///    char over CoolSeconds and its smoke thinning over SmokeSeconds. Then the pod stays parked for the run (the pilot's way
    ///    out: leaving by pod is Astra's rule; Launch is its look).
    ///  - My own camera: Shot (the sky, the ride or the braced cabin, the stop over the crash, the arc down behind my pilot, the
    ///    hand-off at GO), Shake, and the still wide shot for Reduced effects.
    /// The opening is today's 3 s countdown or Astra's PodOpening (5 s, A0): every beat derives from it (Plan, OpeningFor).
    /// Pure functions of (age, plan): any frame, peer or late frame shows the same moment. Engine free; nothing allocates.
    /// </summary>
    public static class NativePodDrop
    {
        // ---- the opening ----
        /// <summary>The two openings a drop is timed for: today's countdown and Astra's PodOpening on generated hubs (A0).</summary>
        public const float ShortOpening=3,LongOpening=5,MaxOpening=6;
        /// <summary>The opening a drop spans by default: the countdown NativeMatch holds today (NativeSpawnIn.Countdown, a literal
        /// 3 s). When A0 lands, set it to ExpeditionRules.PodOpening; until then OpeningFor already reads a longer countdown it
        /// sees, so the crash is timed right on either build.</summary>
        public const float Opening=NativeSpawnIn.Countdown;
        /// <summary>The opening of a drop whose countdown has been seen as early as `earliest` s before GO (NaN: unseen): a
        /// countdown longer than the short one is the long one.</summary>
        public static float OpeningFor(float earliest)=>!float.IsNaN(earliest)&&earliest<-(ShortOpening+.25f)?Math.Max(Opening,LongOpening):Opening;
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
        /// <summary>The braced pilot's forward hunch against the bar, the eye's height over the cabin floor, and where the pilot
        /// stands in the cabin (the pod's frame).</summary>
        public const float HunchAngle=7*Deg,CabinEye=1.39f;
        public static readonly Vector3 CabinFeet=new Vector3(0,NativePodModels.Floor,0);
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
            float o=float.IsNaN(opening)?Opening:Math.Max(ShortOpening,Math.Min(MaxOpening,opening)),k=Clamp01((o-ShortOpening)/(LongOpening-ShortOpening));
            var p=new PodPlan{Order=Math.Max(0,Math.Min(7,order)),Opening=o};int s=p.Seed=PlanSeed(seat,p.Order,x,z,end);
            // the beats: 3 s (the fallback) to 5 s (the plan's opening): impacts at -1.75 + .12 n to -2.8 + .25 n, the door .35 to .6 after
            p.Appear=-o;p.Ride=L(-2.6f,-4.1f,k);p.CabinEnd=p.Ride+L(.55f,1,k);
            p.Impact=L(-1.75f,-2.8f,k)+L(.12f,.25f,k)*p.Order;p.RetroAt=p.Impact-L(.3f,.4f,k);
            p.DoorSeconds=L(.18f,.22f,k);p.DoorAt=Math.Min(p.Impact+L(.35f,.6f,k),-(p.DoorSeconds+DoorSettle+.05f));
            p.StandEnd=Math.Min(p.DoorAt+L(.95f,1.4f,k),-StandMargin);p.StandFrom=Math.Max(p.DoorAt+.04f,Math.Min(p.DoorAt+.12f,p.StandEnd-.3f));
            p.End=Math.Max(p.StandEnd,p.DoorAt+p.DoorSeconds+DoorSettle);
            p.Stop=p.Impact-L(.25f,.35f,k);p.ArcFrom=p.Impact+L(.35f,.55f,k);p.HandOff=L(.5f,.6f,k);p.ArcTo=Math.Min(-.02f,Math.Max(p.ArcFrom+.5f,-p.HandOff-.05f));p.ArcFrom=Math.Min(p.ArcFrom,p.ArcTo-.25f);
            // the line in: from behind the door within MaxBearing, at 45-60 degrees, braked by the retros (or not)
            p.Yaw=yawDegrees*Deg;p.Forward=new Vector3((float)Math.Sin(p.Yaw),0,(float)Math.Cos(p.Yaw));
            p.Bearing=(2*Hash(s,1)-1)*MaxBearing;p.Travel=Turn(p.Forward,p.Bearing);p.Side=new Vector3(p.Travel.Z,0,-p.Travel.X);
            p.Fails=Fails(end,p.Order);p.Dive=(45+15*Hash(s,2))*Deg;
            p.Skid=p.Fails?1.15f+.35f*Hash(s,3):.7f+.45f*Hash(s,3);p.SkidSeconds=L(.3f,.42f,k)*(p.Fails?1.15f:1);
            var up=Vector3.UnitY;var dive=p.Travel*(float)Math.Cos(p.Dive)-up*(float)Math.Sin(p.Dive);
            float s1=p.Fails?44:24,sr=p.Fails?72:82,tb=p.Impact-p.RetroAt;p.Speed=s1;
            p.P1=-p.Travel*p.Skid;p.V1=dive*s1;p.Vr=dive*sr;p.Pr=p.P1-dive*((sr+s1)*.5f*tb);
            float reach=L(120,200,k)*(.92f+.16f*Hash(s,4)),height=L(85,140,k)*(.92f+.16f*Hash(s,5)),ta=p.RetroAt-p.Appear,entry=(16+8*Hash(s,6))*Deg;
            p.P0=p.P1-p.Travel*reach+up*height;
            // the entry: shallow and fast, no faster than the cubic keeps monotone (its tangent within 2.6x the chord)
            var chord=p.Pr-p.P0;p.V0=(p.Travel*(float)Math.Cos(entry)-up*(float)Math.Sin(entry))*(1.3f*chord.Length()/ta);
            p.Punch=Crossing(p,L(45,50,k));
            // the rest: sunk and leaning a little, mostly along the plough
            p.Sink=Sink;var lean=Turn(p.Travel,(Hash(s,7)-.5f)*2.1f);p.LeanAxis=Vector3.Normalize(Vector3.Cross(up,lean));p.Lean=(1.4f+2.6f*Hash(s,8))*Deg;
            p.RestDoor=RestDoorTurn(p);
            p.Crater=(int)(Hash(s,9)*NativePodModels.CraterShapes)%NativePodModels.CraterShapes;p.CraterTurn=(2*Hash(s,10)-1)*10*Deg;p.CraterX=.9f+.2f*Hash(s,11);p.CraterZ=.92f+.14f*Hash(s,12);
            p.Furrow=NativePodModels.CraterRadius*1.06f*(1+NativePodModels.Plough)*p.CraterZ;
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
                float u=Clamp01((age-p.Appear)/(p.Impact-p.Appear));
                f.Heat=Smooth(u*3.2f)*(1-.6f*Smooth((age-p.RetroAt)/.25f));f.Hull=.9f*Smooth(u*2.4f);f.Retro=Retro(p,age);f.Cabin=.35f;
            }
            else
            {
                Ground(p,land,out f.Offset,out f.Tilt);
                f.Heat=1-Smooth(land/.5f);f.Hull=Cool(land);f.Retro=(1-Smooth(land/.1f))*Retro(p,p.Impact-.01f);
                f.Sign=SignOn(land);f.Cabin=Smooth((land-.15f)/.25f)*.5f;f.Beacon=((int)(land*4))%2==0?1:.2f;f.Smoke=SmokeLevel(land);
            }
            f.Seam=age<p.DoorAt?Smooth((age-(p.DoorAt-.3f))/.3f):1-Smooth((age-p.DoorAt)/.15f);f.Bolts=Bolts(age-p.DoorAt);
            f.Door=DoorTurn(age-p.DoorAt,p.DoorSeconds,p.RestDoor);if(f.Door>0)f.Cabin=Math.Max(f.Cabin,.5f+.5f*Smooth(f.Door/p.RestDoor));
            f.Shown=age>=p.DoorAt;Pilot(ref f,p,age);
            return f;
        }
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
            f.Dropping=false;f.Offset=RestOffset(p);f.Tilt=RestTilt(p);f.Door=p.RestDoor;f.Shown=true;f.Land=since;f.Sign=1;f.Cabin=.7f;f.Harness=1;
            f.Beacon=((int)(Math.Max(0,since)*1.25f))%2==0?1:.25f;f.Hull=Cool(since);f.Smoke=SmokeLevel(since);f.Pilot=Capsule(p);f.PilotTilt=Quaternion.Identity;
        }
        // The pilot: strapped in with the pod (hunched into the bar) until the harness lifts, then standing up on the capsule.
        private static void Pilot(ref PodFrame f,in PodPlan p,float age)
        {
            var strapped=PodPoint(f,p,CabinFeet);var braced=Quaternion.Multiply(f.Tilt,Quaternion.CreateFromAxisAngle(Pitch(p.Forward),HunchAngle));
            if(!f.Shown){f.Pilot=strapped;f.PilotTilt=braced;f.Harness=0;return;}
            float stand=Smooth((age-p.StandFrom)/Math.Max(.05f,p.StandEnd-p.StandFrom));f.Harness=Smooth((age-p.StandFrom)/HarnessSeconds);
            f.Pilot=Vector3.Lerp(strapped,Capsule(p),stand);f.PilotTilt=Quaternion.Slerp(braced,Quaternion.Identity,stand);
        }
        /// <summary>The ablative tiles' glow `land` s after the impact: hot off the touchdown, char black by CoolSeconds.</summary>
        public static float Cool(float land)=>land<0?.9f:.9f*(float)Math.Pow(Math.Max(0,1-land/CoolSeconds),1.6);
        /// <summary>The smoke left `land` s after the impact, thinning to nothing by SmokeSeconds.</summary>
        public static float SmokeLevel(float land)=>land<0?0:(float)Math.Pow(Math.Max(0,1-land/SmokeSeconds),1.3);
        /// <summary>The tiles' colour (linear, the draw's tint) for glow `hull`: char black, dull red, cherry, orange, white hot.</summary>
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

        // ---- leaving (Astra's rule triggers it; this is what it looks like) ----
        /// <summary>The door closes over this long, the retros light, the pod lifts away and is gone by LaunchGone.</summary>
        public const float LaunchClose=.5f,LaunchIgnite=.9f,LaunchGone=3.4f;
        /// <summary>Pod `p` parked, `since` s after its pilot boarded to leave: the door swings shut, the retros build, it rises
        /// out of its crater (standing up as it goes) slowly then fast straight up; Gone once away. The crater stays.</summary>
        public static PodFrame Launch(float since,in PodPlan p)
        {
            var f=new PodFrame{Age=since,Land=ParkedSince,Tilt=RestTilt(p),PilotTilt=Quaternion.Identity};if(!(since>=0)){Parked(ref f,p,ParkedSince);return f;}
            if(since>=LaunchGone){f.Gone=true;return f;}
            f.Dropping=true;f.Sign=1;f.Cabin=1;f.Beacon=((int)(since*8))%2==0?1:.2f;f.Pilot=Capsule(p);
            f.Door=p.RestDoor*(1-Smooth(since/LaunchClose));f.Retro=Smooth((since-LaunchClose)/(LaunchIgnite-LaunchClose));
            float up=since-LaunchIgnite;f.Offset=RestOffset(p)+new Vector3(0,up>0?up*up*(4+3*up):0,0);f.Heat=Clamp01(up*.6f);
            f.Tilt=Quaternion.Slerp(RestTilt(p),Quaternion.Identity,Smooth(up/.6f));
            return f;
        }

        // ---- the effects (closed form, through the directed entrances' emitters; world frame, y up) ----
        /// <summary>
        /// Pod `p`'s effects for frame `f` over its rest `centre` (on the floor at `floor`): the fireball under the shield, its
        /// plasma trail back along the curve it flew and the contrail it leaves in the sky; the cloud punch's vapour ring and flash;
        /// the retros (or their sputter, sparks and smoke); the impact's flash, shockwave, dust wall, sparks and spoil; the steam
        /// vents; the bolts' flashes and the door's blow; then the smoke off the hull and its embers while it is hot. `quality` the
        /// effects setting (Reduced: softer flashes, thinner dust).
        /// </summary>
        public static void Effects(INativeEntranceFx fx,in PodFrame f,in PodPlan p,Vector3 centre,float quality,float floor)
        {
            if(fx==null||f.Gone)return;float t=f.Age,land=t-p.Impact;int s=p.Seed&0xffffff;bool soft=quality<.5f;
            var white=Vector3.One;var fire=NativeEntranceFx.Hex(0xffb35a,2.6f);var up=Vector3.UnitY;
            if(land<0)
            {
                var b=centre+f.Offset;FlightAt(p,t,out var vel);float speed=vel.Length();var dir=speed>1e-3f?vel/speed:-up;
                if(f.Heat>.04f){fx.Glow(b+dir*.25f,white*(3.2f*f.Heat),0,1.7f,1,0,s+1);fx.Glow(b-dir*.4f,fire*f.Heat,0,3.8f,0,0,s+2);}
                for(int i=0;i<14;i++)
                {
                    if(i%2==1&&Hash(s+3,i)>quality)continue;float back=t-.02f*(i+1);if(back<p.Appear)break;
                    var at=centre+FlightAt(p,back,out var v)+Direction(s+4,i)*.15f;float k=i/14f;
                    fx.Streak(at,v*.9f,Vector3.Lerp(white*2.8f,fire,k)*(f.Heat*(1-k*.85f)),.22f-i*.011f,.6f,.03f,.8f,0,s+10+i);
                }
                if(f.Retro>.02f)Retros(fx,f,p,centre,t,s,quality,floor);
            }
            Contrail(fx,p,centre,t,s,quality,floor);
            float punch=t-p.Punch;
            if(punch>=0&&punch<1.4f)
            {
                // the cloud punch: a vapour ring torn open where it came through, left behind it, and a flash
                var at=centre+FlightAt(p,p.Punch,out var pv);var n=Vector3.Normalize(pv);var a=Vector3.Cross(n,up);a=a.LengthSquared()>1e-6f?Vector3.Normalize(a):Vector3.UnitX;var c=Vector3.Cross(a,n);
                float u=punch/1.4f;int count=soft?10:20;var vapour=NativeEntranceFx.Hex(0xdfe4ea);
                for(int i=0;i<count;i++)
                {
                    float ang=i*Tau/count+.2f*Hash(s+60,i),rad=1.5f+9*(1-(1-u)*(1-u))*(.85f+.3f*Hash(s+61,i));var r=a*(float)Math.Cos(ang)+c*(float)Math.Sin(ang);
                    fx.Smoke(at+r*rad-n*(2*u),vapour,.6f*(1-u)*(1-u)*Smooth(punch*12),2.2f+2.8f*u,u,s+62+i,floor);
                }
                if(punch<.18f)fx.Glow(at,white*((soft?2.5f:4.5f)*(1-punch/.18f)),0,soft?5:8,1,0,s+59);
            }
            if(land<0)return;
            var touch=centre+p.P1;
            // the impact: a flash, the shockwave racing out low, the dust wall rolling out to ~6 m and hanging, sparks forward
            if(land<.22f){float k=1-land/.22f;fx.Glow(touch+up*1.2f,white*((soft?2.5f:5)*k),0,soft?5:8,1,0,s+100);fx.Glow(touch+up*.5f,fire*(1.6f*k),0,soft?6:10,0,0,s+101);}
            NativeEntranceFx.GroundDust(fx,t,p.Impact,touch,soft?14:26,1.3f,15,1.1f,NativeEntranceFx.Hex(0x9aa0a8),.55f,.9f,s+110,quality,floor);
            NativeEntranceFx.GroundDust(fx,t,p.Impact+.03f,touch,soft?12:22,1.6f,11,3.4f,NativeEntranceFx.Hex(0x80786e),.7f,2.4f,s+140,quality,floor);
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
        private static void Contrail(INativeEntranceFx fx,in PodPlan p,Vector3 centre,float t,int s,float quality,float floor)
        {
            const float Rate=20,Life=3.2f;float to=Math.Min(t,p.Impact-.05f);if(to<p.Appear)return;
            int first=(int)Math.Max(0,Math.Floor((t-Life-p.Appear)*Rate)),last=(int)Math.Floor((to-p.Appear)*Rate);var smoke=NativeEntranceFx.Hex(0x585c66);
            for(int k=first;k<=last;k++)
            {
                if(Hash(s+40,k)>Math.Max(.35f,quality))continue;float born=p.Appear+k/Rate,a=t-born,life=Life*(.75f+.25f*Hash(s+41,k));if(a<0||a>life)continue;float u=a/life;
                var at=centre+FlightAt(p,born,out _)+Direction(s+42,k)*(.3f+1.2f*u)+new Vector3(0,.4f*a,0);float hot=1-.4f*(born-p.Appear)/(p.Impact-p.Appear);
                fx.Smoke(at,smoke,.55f*Smooth(u*5)*(1-u)*hot,1.6f+3.4f*Smooth(u),u,s+43+k,floor);
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
        /// last MeteorSeconds, camera-facing about `eye`, fading out as it nears the eye (the fogged effects take over close up).
        /// Before the touchdown only. Returns the vertices written.
        /// </summary>
        public static int Meteor(ISpawnInSink sink,in PodFrame f,in PodPlan p,Vector3 centre,Vector3 eye,float gain)
        {
            if(sink==null||f.Gone||!f.Dropping||f.Land>=0||!(f.Heat>.03f)||!(gain>0))return 0;const int N=18;float t=f.Age;int written=0,pa=-1,pb=-1;
            var head=centre+f.Offset;float near=Smooth((Vector3.Distance(head,eye)-25)/45),level=f.Heat*near*gain;if(level<.01f)return 0;
            var hot=new Vector3(1,.86f,.62f)*3.2f;var ember=new Vector3(1,.38f,.1f)*1.6f;
            for(int i=0;i<=N;i++)
            {
                float back=Math.Max(p.Appear,t-MeteorSeconds*i/N);var at=centre+FlightAt(p,back,out var v);var tangent=v.LengthSquared()>1e-6f?Vector3.Normalize(v):-Vector3.UnitY;
                var side=Vector3.Cross(tangent,eye-at);float sl=side.Length();if(sl<1e-4f)continue;float d=Vector3.Distance(at,eye),k=i/(float)N;
                float width=Math.Max(.5f+1.4f*k,d*.0035f*(1+k));side*=width*.5f/sl;var col=new Vector4(Vector3.Lerp(hot,ember,k),level*(float)Math.Pow(1-k,1.6));
                int va=sink.Vertex(at-side,new Vector3(-1,0,1),col),vb=sink.Vertex(at+side,new Vector3(1,0,1),col);written+=2;
                if(pa>=0){sink.Triangle(pa,pb,vb);sink.Triangle(pa,vb,va);}pa=va;pb=vb;
            }
            var view=eye-head;float vl=view.Length();if(vl<1e-3f)return written;view/=vl;var right=Vector3.Cross(Vector3.UnitY,view);right=right.LengthSquared()>1e-6f?Vector3.Normalize(right):Vector3.UnitX;var upv=Vector3.Cross(view,right);
            float size=Math.Max(2.2f,vl*.012f);var c=new Vector4(hot,.9f*level);
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
        /// My camera `age` s from GO over my pod `p` (its rest `centre`): the sky shot over the camp as the streaks come in; a cut
        /// to the chase behind my pod (in first person one braced second in the red-lit cabin, the city rushing up in the porthole,
        /// first); the chase slowing to a stop 10 m up while the pod punches in below it; the hold on the crash; the arc down behind
        /// my pilot (in first person into my pilot's eyes, `gameEye`); Directed falls from 1 to 0 over the plan's HandOff before GO,
        /// where the game's own camera is blended back in. The still shot holds a wide view from the plaza beside the approach and
        /// glides behind my pilot at the end. Shake on top is Shake's.
        /// </summary>
        public static PodShot Shot(in PodPlan p,Vector3 centre,float age,int mode,Vector3 gameEye)
        {
            var up=Vector3.UnitY;var shot=new PodShot{Up=up,Fov=60,Directed=1-Smooth((age+p.HandOff)/p.HandOff)};
            var endEye=mode==ShotFirst?gameEye:centre-p.Forward*4.3f+up*3;var endLook=mode==ShotFirst?gameEye+p.Forward*10-up*1.4f:centre+p.Forward*6+up*1.1f;
            float endFov=mode==ShotFirst?75:60;
            if(mode==ShotStill)
            {
                // beside the approach on the plaza (the other side when this one stands past the fence, never past PlazaHalf)
                float side=Hash(p.Seed,30)<.5f?-1:1;var eye=centre+p.Side*(13*side)-p.Travel*4+up*2.4f;var look=centre-p.Travel*2.5f+up*3.2f;
                if(Math.Max(Math.Abs(eye.X),Math.Abs(eye.Z))>PlazaHalf)eye=centre-p.Side*(13*side)-p.Travel*4+up*2.4f;
                eye.X=Math.Max(-PlazaHalf,Math.Min(PlazaHalf,eye.X));eye.Z=Math.Max(-PlazaHalf,Math.Min(PlazaHalf,eye.Z));
                float g=Smooth((age-p.ArcFrom)/Math.Max(.3f,p.ArcTo-p.ArcFrom));shot.Eye=Vector3.Lerp(eye,endEye,g);shot.Look=Vector3.Lerp(look,endLook,g);shot.Fov=L(62,endFov,g);return shot;
            }
            if(age<p.Ride)
            {
                // the sky: high over the camp beside my pod's line, looking up it as my streak comes burning in (seen from the side,
                // its whole trail shows)
                var pod=centre+FlightAt(p,Math.Max(p.Appear,age),out _);shot.Eye=centre+p.Travel*12+p.Side*(Hash(p.Seed,31)<.5f?-58:58)+up*42;shot.Look=Vector3.Lerp(centre+up*4,pod,.6f);shot.Fov=62;return shot;
            }
            var f=Evaluate(age,p);
            if(mode==ShotFirst&&age<p.CabinEnd&&age<p.Stop)
            {
                // braced in the cabin: my eye over the cabin floor, looking at the door's porthole, turning with the pod
                var rot=Quaternion.Multiply(f.Tilt,Facing(p));shot.Eye=centre+PodPoint(f,p,new Vector3(0,NativePodModels.Floor+CabinEye,-.05f));
                shot.Look=shot.Eye+Vector3.Transform(Vector3.Normalize(new Vector3(0,-.3f,1)),rot)*4;shot.Up=Vector3.Normalize(up+Vector3.Transform(up,rot));shot.Fov=72;shot.Cabin=true;return shot;
            }
            var touch=centre+p.P1;var stop=touch-p.Travel*6+up*10;
            if(age<p.Stop)
            {
                // the chase: behind and above my pod along its line, looking ahead of it
                var pos=centre+FlightAt(p,age,out var v);var d=Vector3.Normalize(v);shot.Eye=pos-d*ChaseBack+up*ChaseUp;shot.Look=pos+d*6;shot.Fov=L(66,60,Smooth((age-p.Ride)/(p.Stop-p.Ride)));return shot;
            }
            if(age<p.Impact)
            {
                // the stop: the chase eases to a halt 10 m up behind the touchdown; the pod punches in below it
                var pos=centre+FlightAt(p,p.Stop,out var v);var d=Vector3.Normalize(v);var from=pos-d*ChaseBack+up*ChaseUp;float T=p.Impact-p.Stop;
                var chord=stop-from;float most=2.5f*chord.Length()/Math.Max(1e-3f,v.Length()*T);var v0=v*Math.Min(1,most);
                shot.Eye=Hermite(from,v0,stop,Vector3.Zero,T,Clamp01((age-p.Stop)/T),out _);
                var now=centre+f.Offset;shot.Look=Vector3.Lerp(pos+d*6,now+up*.8f,Smooth((age-p.Stop)/T));shot.Fov=60;return shot;
            }
            var pod2=centre+f.Offset+up*1.2f;
            if(age<p.ArcFrom){shot.Eye=stop;shot.Look=pod2;shot.Fov=58;return shot;}
            // the arc down behind my pilot: a swoop from the stop to the end pose, the look easing from the pod to ahead
            float g2=Clamp01((age-p.ArcFrom)/Math.Max(.05f,p.ArcTo-p.ArcFrom)),e=g2*g2*g2*(g2*(g2*6-15)+10);
            var q=endEye+up*4+(stop-centre)*.25f;shot.Eye=(1-e)*(1-e)*stop+2*(1-e)*e*q+e*e*endEye;shot.Look=Vector3.Lerp(pod2,endLook,Smooth(g2*1.2f));shot.Fov=L(58,endFov,e);
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
