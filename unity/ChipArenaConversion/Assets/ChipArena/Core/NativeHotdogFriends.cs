using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Hotdog Friends, a DEV kill effect (owner 2026-11-09: "Id also like to make a DEV kill effect called "Hotdog Friends" after players are
    /// gibbed/ragdolled like normal. Tiny little hotdogs appear from a tiny door (near whereever the body parts land/stop) and take all the body
    /// parts through the door. (Have them move accordingly so it fits the body parts seamlessly)"; then: "some hotdogs pickup nothing, fix this
    /// ... animate this a lot better ... We want comical hotdog runs and running away into the door. (They also dont bring things in the door,
    /// they kind of just leave them out)"). The body falls as the stock impact ragdoll (its gibs, severs and slices too). Once it has come to
    /// rest a little arched door rises out of the floor beside the pieces and swings open, and a file of tiny hot dogs on stick legs hops out
    /// and dashes, leaning into it, to each piece (the pieces still joined together are one load), skids to a stop under it (each friend under
    /// one of the load's own pieces: <see cref="Slots"/>; more round a bigger one: <see cref="Friends"/>), crouches, heaves it up overhead,
    /// waddles it back to the door and stuffs it through, then dashes for the door and dives in head first; the very last one turns on the
    /// step and waves before it goes. The door slams, bounces and sinks away. Every moment is a pure function of the death's seed, where the
    /// pieces came to rest and the effect clock, so every client and the killcam draw the same scene. Runtime NativeHotdogFriendsPresentation.cs
    /// gathers the loads, moves and shrinks the body's pieces and draws the door and the friends (built here) in the Top Dog's toon props
    /// shader. Presentation only. Unity's axes.
    /// </summary>
    public static class NativeHotdogFriends
    {
        public const string Style="dev-hotdog-friends",Label="Hotdog Friends · DEV ONLY",Text="Tiny hot dogs carry the pieces off through a tiny door.";public const int Swatch=0xf4c430;
        /// <summary>The body rests this long (s after the death) before the door rises; the door's rise, opening, slam, bounce and sinking (s);
        /// its height and width, and how far past the pieces it stands (m).</summary>
        public const float Settle=1.2f,DoorUp=.22f,DoorOpen=.15f,DoorShut=.12f,DoorBounce=.12f,DoorDown=.22f,DoorHeight=.46f,DoorWidth=.3f,DoorAway=.55f,DoorSwing=105;
        /// <summary>The friends: their height (m); the dash and the carry (m/s) and the longest of each (s: farther pieces are hurried, so the
        /// scene ends inside the body's five seconds); the hop out of the door, the skid, the crouch and the heave (s) and how high the load is
        /// held (m); the most stagger between loads and the span it is squeezed into, the file's gap, the stuffing through, the dive and the
        /// wave (s); a running step and a carrying one (m).</summary>
        public const float Height=.27f,Run=3f,CarrySpeed=1.8f,MostRun=.45f,MostCarry=.75f,Pop=.1f,Skid=.08f,Crouch=.12f,Heave=.18f,LiftTime=Crouch+Heave,LiftHeight=.22f,
            MostStagger=.08f,StaggerSpan=.35f,File=.05f,Squeeze=.22f,Dive=.12f,Wave=.32f,RunStride=.14f,CarryStride=.07f;
        /// <summary>The friend models: WalkFrames stride frames and one standing (index WalkFrames), each with its arms pumping, up under a load
        /// or waving.</summary>
        public const int MaxLoads=12,MostFriends=6,WalkFrames=8,Frames=WalkFrames+1,ArmsSwing=0,ArmsUp=1,ArmsWave=2,ArmsPush=3,ArmKinds=4;
        /// <summary>A load too big for the doorway (owner 2026-11-09: "If parts are too big for the door, make them comically (squish into
        /// there) like cartoons"): wider or taller than the doorway by JamFit, it bonks into the frame and squashes against it (JamHit s), the
        /// friends push in JamPulses heaves while it squishes thin enough to fit and stretches into the dark, the frame bulging (JamPush s),
        /// then it slurps through and is gone (JamPop s) and the friends tumble on their faces (Tumble s) before they scramble in after it.</summary>
        public const float JamFit=1.08f,JamHit=.08f,JamPush=.26f,JamPop=.1f,JamTime=JamHit+JamPush+JamPop,Tumble=.16f;public const int JamPulses=3;
        /// <summary>The door is open from here (s after the death).</summary>
        public const float Opened=Settle+DoorUp+DoorOpen;
        /// <summary>How many carry a load `size` m across.</summary>
        public static int Friends(float size)=>size<.2f?2:size<.38f?3:size<.65f?4:MostFriends;

        private static float Clamp(float x)=>x<0?0:x>1?1:x;
        private static float Ease(float x){x=Clamp(x);return 1-(1-x)*(1-x);}
        private static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Sin(float x)=>(float)Math.Sin(x);
        private const float PI=(float)Math.PI;
        public static float Hash(uint seed,int k){uint h=seed*0x9E3779B1u^(uint)k*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;return (h&0xffffff)/16777216f;}
        private static Vector3 Flat(Vector3 v){v.Y=0;float l=v.Length();return l>1e-5f?v/l:Vector3.UnitZ;}
        private static float FlatLength(Vector3 v){v.Y=0;return v.Length();}

        /// <summary>The door's candidate spot k (0..7) round the pieces' middle `centre` (`radius` their spread): eight directions from a seeded
        /// start; the Runtime keeps the first with floor under it and nothing in the way. `yaw` (degrees) turns the door's mouth to the middle.</summary>
        public static Vector3 DoorSpot(Vector3 centre,float radius,uint seed,int k,out float yaw)
        {
            float a=Hash(seed,1)*6.2831853f+k*.7853982f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
            yaw=(float)(Math.Atan2(-d.X,-d.Z)*180/Math.PI);return centre+d*(radius+DoorAway);
        }
        /// <summary>When the door slams behind the last friend (in at `last`).</summary>
        public static float ShutAt(float last)=>Math.Max(Opened,last+.03f);
        /// <summary>How far the door has risen out of the floor (0..1) and how open it is (0..1; a slam, then it bounces open a crack) at effect
        /// age `t`, the last friend in at `last`; false once it has sunk away (or before it rises).</summary>
        public static bool Door(float t,float last,out float rise,out float open)
        {
            rise=open=0;if(t<Settle)return false;float shutAt=ShutAt(last),downAt=shutAt+DoorShut+DoorBounce;if(t>=downAt+DoorDown)return false;
            rise=t<downAt?Ease((t-Settle)/DoorUp):1-Ease((t-downAt)/DoorDown);
            if(t<Settle+DoorUp)open=0;
            else if(t<shutAt)open=Ease((t-Settle-DoorUp)/DoorOpen);
            else if(t<shutAt+DoorShut){float u=(t-shutAt)/DoorShut;open=1-u*u;}
            else{float u=Clamp((t-shutAt-DoorShut)/DoorBounce);open=.12f*Sin(u*PI)*(1-u);}
            return true;
        }
        /// <summary>When the door is gone: the effect is over.</summary>
        public static float Over(float last)=>ShutAt(last)+DoorShut+DoorBounce+DoorDown;
        /// <summary>Whether the very last friend (in at `last` without it) has time to wave inside the body's five seconds.</summary>
        public static bool WaveFits(float last)=>Over(last+Wave)<(float)NativeKillEffectRules.DeathSeconds-.1f;
        /// <summary>Whether a scene whose last friend is in at `last` ends inside the body's five seconds (jams are given up, the latest first,
        /// until it does).</summary>
        public static bool Fits(float last)=>Over(last)<(float)NativeKillEffectRules.DeathSeconds-.05f;

        /// <summary>The stagger between the loads' files for `loads` loads: squeezed into StaggerSpan when there are many.</summary>
        public static float Stagger(int loads)=>loads<=1?0:Math.Min(MostStagger,StaggerSpan/(loads-1));
        /// <summary>One load's trip: when its file leaves the door, has gathered under it, has it up, is back at the door and has it through;
        /// `Floor` the floor under it.</summary>
        public struct Trip{public float Leave,Arrive,Lifted,Back,In,Floor,Fit;public bool Jam;}
        /// <summary>The trip of the load `order`th nearest the door of `loads`, `distance` m from it, carried by `friends`; `fit` how many
        /// times too big for the doorway it is (Fit; over JamFit it jams).</summary>
        public static Trip Plan(int order,int loads,float distance,int friends,float floor,float fit=0)
        {
            float leave=Opened+order*Stagger(loads),run=Pop+Math.Min(MostRun,Math.Max(.05f,distance)/Run)+Math.Max(0,friends-1)*File*.5f,carry=Math.Min(MostCarry,Math.Max(.05f,distance)/CarrySpeed);
            bool jam=fit>JamFit;float arrive=leave+run,back=arrive+LiftTime+carry;
            return new Trip{Leave=leave,Arrive=arrive,Lifted=arrive+LiftTime,Back=back,In=back+(jam?JamTime:Squeeze),Floor=floor,Fit=Math.Max(1,fit),Jam=jam};
        }
        /// <summary>How many times too big for the doorway a load is: its width across the way it comes in (`across`, m) against the
        /// doorway's, its height against the doorway's.</summary>
        public static float Fit(float across,float height)=>Math.Max(across/(DoorWidth*.92f),height/(DoorHeight*.8f));
        /// <summary>When a jammed load's push ends and it slurps through.</summary>
        public static float PopAt(in Trip trip)=>trip.Back+JamHit+JamPush;
        /// <summary>The push's heave at age `t` (0..1 at each of the JamPulses heaves), for the friends, the squish and the frame's bulge.</summary>
        public static float PushAt(in Trip trip,float t)
        {
            if(!trip.Jam)return 0;float u=(t-trip.Back-JamHit)/JamPush;if(u<0||u>=1)return 0;float c=u*JamPulses%1;return (float)Math.Pow(Sin(c*PI),2);
        }
        /// <summary>The doorway's frame bulging as a jammed load is pushed through it (0..1).</summary>
        public static float Bulge(in Trip trip,float t)
        {
            if(!trip.Jam)return 0;float h=t-trip.Back;if(h<0||h>JamHit+JamPush+JamPop+.12f)return 0;
            if(h<JamHit)return .5f*Ease(h/JamHit);if(h<JamHit+JamPush)return .45f+.55f*PushAt(trip,t);
            float after=h-JamHit-JamPush;return (float)(Math.Exp(-after*14)*Math.Cos(after*45))*.8f;
        }

        /// <summary>
        /// The friends' spots under a load (offsets on the floor from its middle), one for each of `n`: under the load's own pieces (`spots`,
        /// their middles from the load's, `halves` their half-widths), spread evenly along the carry (`toDoor`) and to both sides of each piece,
        /// so every friend has something over it. Called once a load (it allocates).
        /// </summary>
        public static Vector3[] Slots(Vector3[] spots,float[] halves,Vector3 toDoor,int n)
        {
            var dir=Flat(toDoor);var side=new Vector3(dir.Z,0,-dir.X);var slots=new Vector3[Math.Max(0,n)];int m=spots?.Length??0;
            if(m==0){for(int j=0;j<n;j++){float a=(j+.5f)/Math.Max(1,n)*6.2831853f;slots[j]=dir*((float)Math.Cos(a)*.1f)+side*((float)Math.Sin(a)*.1f);}return slots;}
            var order=new int[m];for(int i=0;i<m;i++)order[i]=i;Array.Sort(order,(a,b)=>Vector3.Dot(spots[a],dir).CompareTo(Vector3.Dot(spots[b],dir)));
            var taken=new int[m];
            for(int j=0;j<n;j++)
            {
                int r=Math.Min(m-1,(int)((j+.5f)*m/n)),k=order[r];int c=taken[k]++;var at=spots[k];at.Y=0;
                float half=halves!=null&&k<halves.Length?halves[k]:.06f,off=Math.Min(.08f,Math.Max(.03f,half*.5f));
                // Each piece's first friend to one side, its second to the other, any more along it.
                float across=(r+c)%2==0?off:-off,along=c<2?0:((c/2)%2==1?1:-1)*.08f*((c+2)/4);
                slots[j]=at+side*across+dir*along;
            }
            return slots;
        }
        /// <summary>When friend `j` of a trip is through the door (the last one shuts it), its spot `slot` under the load; `waves` the very
        /// last friend of all, who turns on the step and waves first.</summary>
        public static float InAt(in Trip trip,int j,Vector3 slot,bool waves)=>Go(trip,j)+Math.Max(.06f,FlatLength(slot)/Run)+Dive+(waves?Wave:0);
        /// <summary>When friend `j` of a trip lets go and dashes for the door: once the load is in (a jammed one after the tumble).</summary>
        public static float Go(in Trip trip,int j)=>trip.Jam?PopAt(trip)+Tumble+j*File*.5f:trip.Back+.04f+j*File;

        /// <summary>
        /// The load at age `t`: from its rest middle `rest` to the door's mouth `mouth`; its middle `at`, its `scale` (1, to 0 as it is
        /// stuffed through) and its sway (`tilt`, radians about the carry's side): still while the friends crouch, heaved up overhead with an
        /// overshoot, bobbing with their steps as it is carried, then pulled wobbling into the doorway and away.
        /// </summary>
        public static void Load(in Trip trip,Vector3 rest,Vector3 mouth,float t,out Vector3 at,out float scale,out float tilt)=>Load(trip,rest,mouth,t,out at,out scale,out tilt,out _);
        /// <summary>The load at age `t` as Load, with its `squish`: its stretch along the way it is carried into the door (z), across it (x)
        /// and up (y); (1,1,1) unless it jams in the doorway.</summary>
        public static void Load(in Trip trip,Vector3 rest,Vector3 mouth,float t,out Vector3 at,out float scale,out float tilt,out Vector3 squish)
        {
            at=rest;scale=1;tilt=0;squish=Vector3.One;if(t<trip.Arrive)return;
            if(t<trip.Lifted)
            {
                float c=(t-trip.Arrive)/Crouch;if(c<1){tilt=.025f*Sin(c*PI*4);return;}
                float u=(t-trip.Arrive-Crouch)/Heave,up=u<.7f?1.12f*Ease(u/.7f):1.12f-.12f*Smooth((u-.7f)/.3f);
                at=rest+Vector3.UnitY*(LiftHeight*up);tilt=.08f*Sin(u*PI*3)*(1-u);return;
            }
            var flat=mouth-rest;flat.Y=0;float length=flat.Length();
            if(t<trip.Back)
            {
                float u=(t-trip.Lifted)/Math.Max(1e-3f,trip.Back-trip.Lifted),s=Smooth(u),steps=length*s/CarryStride;
                at=rest+flat*s+Vector3.UnitY*(LiftHeight+.016f*Math.Abs(Sin(steps*PI)));tilt=.06f*Sin(steps*PI);return;
            }
            var inside=mouth+Flat(flat)*.14f+Vector3.UnitY*Math.Min(DoorHeight*.35f,Math.Max(.06f,rest.Y-trip.Floor));var from=rest+flat+Vector3.UnitY*LiftHeight;
            if(trip.Jam)
            {
                // Too big: it bonks into the frame and squashes on it, is pushed in heave by heave, squishing thin enough to fit and stretching
                // into the dark, then slurps through.
                var dir=Flat(flat);var contact=mouth-dir*.04f+Vector3.UnitY*Math.Min(DoorHeight*.4f,Math.Max(.08f,rest.Y-trip.Floor+.04f));float h=t-trip.Back,fit=trip.Fit,thin=1/fit;
                if(h<JamHit){float u=h/JamHit,e=Ease(u);at=Vector3.Lerp(from,contact,e*e);squish=new Vector3(1+.14f*e,1+.08f*e,1-.24f*e);tilt=.05f*Sin(u*PI*2);return;}
                if(h<JamHit+JamPush)
                {
                    float u=(h-JamHit)/JamPush,heave=PushAt(trip,t),done=Smooth(u)*.75f+.25f*(float)Math.Floor(u*JamPulses+.5f)/JamPulses;done=Clamp(done);
                    float x=Lerp(1.14f,thin*.92f,done)-.05f*heave,y=Lerp(1.08f,Math.Min(1,1.15f*thin+.1f),done)-.03f*heave,z=Lerp(.76f,1.25f+.6f*(fit-1),done)+.12f*heave;
                    squish=new Vector3(x,y,z);at=contact+dir*(.03f+.1f*done+.03f*heave)+new Vector3(.006f*Sin(h*80),.004f*Sin(h*67),0);tilt=.03f*Sin(h*40)*(1-done);return;
                }
                {
                    float u=Clamp((h-JamHit-JamPush)/JamPop);at=Vector3.Lerp(contact+dir*.13f,inside+dir*.1f,u*u);
                    squish=new Vector3(thin*.92f*(1-.4f*u),Math.Min(1,1.15f*thin+.1f)*(1-.3f*u),(1.25f+.6f*(fit-1))*(1+.6f*u));scale=u>=1?0:1-(float)Math.Pow(u,2.2f);return;
                }
            }
            // Stuffed through the door: pulled into its mouth faster and faster, wobbling, shrinking away.
            float q=Clamp((t-trip.Back)/Squeeze);
            at=Vector3.Lerp(from,inside,q*q);scale=q>=1?0:1-(float)Math.Pow(q,1.4f);tilt=.12f*Sin(q*PI*3)*(1-q);
        }

        /// <summary>A friend's pose: its `Feet` and `Facing` (flat, unit); `Step` its stride's phase (cycles: two steps each) and `Standing`
        /// (feet together, no stride); `Hold` how high its arms are (0..1) and `Arms` which model (swinging, up, waving); `Scale` (popping
        /// out of the door, shrinking into it); `Lean` forward (radians, negative back), `Hop` off the floor (m), `Squash` (its height's
        /// share, the width taking the rest) and `Roll` (a waddle's sway, radians about its facing).</summary>
        public struct Pose{public Vector3 Feet,Facing;public float Step,Hold,Scale,Lean,Hop,Squash,Roll;public bool Standing;public int Arms;}
        private static float Bounce(float steps)=>Math.Abs(Sin(steps*PI));
        /// <summary>
        /// Friend `j` of `n` on a load's trip at age `t`, its spot `slot` under the load (from <see cref="Slots"/>), the load from its rest
        /// middle `rest` to the door's mouth `mouth`; `waves` for the very last friend of all. False before it comes out and after it has
        /// gone in.
        /// </summary>
        public static bool Friend(in Trip trip,int j,int n,Vector3 rest,Vector3 mouth,Vector3 slot,bool waves,float t,out Pose p)
        {
            p=new Pose{Facing=Vector3.UnitZ,Scale=1,Squash=1,Feet=mouth};
            float leave=Math.Min(trip.Leave+j*File,trip.Arrive-.1f),inAt=InAt(trip,j,slot,waves);if(t<leave||t>=inAt)return false;
            var toDoor=mouth-rest;toDoor.Y=0;var dir=Flat(toDoor);
            var door=new Vector3(mouth.X,trip.Floor,mouth.Z);var stand=new Vector3(rest.X+slot.X,trip.Floor,rest.Z+slot.Z);
            if(t<trip.Arrive)
            {
                // Out of the door with a hop, then a dash to its spot, leaning into it, scurrying, skidding to a stop.
                float u=(t-leave)/Math.Max(.05f,trip.Arrive-leave),s=1-(float)Math.Pow(1-u,1.6f),pop=Clamp((t-leave)/Pop);
                var path=stand-door;float far=path.Length();var pd=far>1e-4f?path/far:-dir;var ps=new Vector3(pd.Z,0,-pd.X);
                p.Feet=door+path*s+ps*(.035f*Sin(s*PI*3)*(1-s));p.Facing=pd;p.Step=far*s/(2*RunStride);p.Arms=ArmsSwing;
                float b=Bounce(far*s/RunStride);p.Hop=pop<1?.07f*Sin(pop*PI):.022f*b;p.Squash=pop<1?1:.9f+.2f*b;p.Scale=.5f+.5f*Ease(pop);p.Lean=.35f;
                float skid=(t-(trip.Arrive-Skid))/Skid;if(skid>0){p.Lean=Lerp(.35f,-.3f,Ease(skid));p.Squash=.88f;p.Hop=0;}
                return true;
            }
            if(t<trip.Lifted)
            {
                // Crouched under it, arms reaching up, then the heave.
                p.Feet=stand;var face=rest-stand;face.Y=0;p.Facing=face.LengthSquared()>1e-4f?Flat(face):dir;p.Standing=true;
                float c=(t-trip.Arrive)/Crouch;
                if(c<1){p.Squash=1-.2f*Ease(c);p.Hold=Ease(c);p.Lean=.15f*c;p.Arms=p.Hold>.5f?ArmsUp:ArmsSwing;}
                else{float h=(t-trip.Arrive-Crouch)/Heave;p.Squash=h<.5f?.8f+.64f*h:1.12f-.24f*(h-.5f);p.Hop=.03f*Sin(Clamp(h)*PI);p.Hold=1;p.Arms=ArmsUp;}
                return true;
            }
            if(t<trip.Back)
            {
                // Carrying: a quick waddle under the load to the door.
                Load(trip,rest,mouth,t,out var at,out _,out _);float carried=FlatLength(at-rest),steps=carried/CarryStride;
                p.Feet=new Vector3(at.X+slot.X,trip.Floor,at.Z+slot.Z);p.Facing=dir;p.Step=carried/(2*CarryStride);p.Hold=1;p.Arms=ArmsUp;
                float b=Bounce(steps);p.Roll=.16f*Sin(steps*PI);p.Hop=.012f*b;p.Squash=1-.06f*b;p.Lean=.1f;return true;
            }
            // The load in: it lets go and dashes for the door, diving in after it (the last of all turns on the step and waves first).
            float go=Go(trip,j),run=Math.Max(.06f,FlatLength(slot)/Run);var under=door+new Vector3(slot.X,0,slot.Z);var step=door-dir*.1f;var target=waves?step:door-dir*.03f;
            if(trip.Jam&&t<go)
            {
                // A load stuck in the doorway: shoulders to it, they push heave by heave, feet scrabbling, then it slurps in and they fall flat
                // on their faces after it and scramble up.
                float pop=PopAt(trip),heave=PushAt(trip,t);var behind=under-dir*(.08f+.04f*(j%2));
                if(t<pop)
                {
                    float h=t-trip.Back;p.Feet=behind+dir*(.02f*Clamp(h/JamHit))+dir*(.025f*heave);p.Facing=dir;p.Arms=ArmsPush;p.Hold=1;p.Step=h*(5.5f+.6f*j);
                    p.Lean=.55f+.35f*heave;p.Squash=.9f+.14f*heave;p.Hop=.006f*Bounce(h*11+j);p.Roll=.06f*Sin(h*30+j);return true;
                }
                float u=Clamp((t-pop)/Tumble),fall=u<.45f?Ease(u/.45f):1;p.Feet=behind+dir*(.03f+.09f*fall);p.Facing=dir;p.Standing=true;p.Arms=ArmsPush;p.Hold=1;
                p.Lean=Lerp(.9f,1.45f,fall)-(u>.7f?1.2f*Smooth((u-.7f)/.3f):0);p.Squash=u<.45f?1:.8f+.2f*Smooth((u-.45f)/.55f);p.Hop=.04f*Sin(Clamp(u/.45f)*PI);return true;
            }
            if(t<go){p.Feet=under;p.Facing=dir;p.Hold=1;p.Arms=ArmsUp;p.Standing=true;return true;}
            if(t<go+run)
            {
                if(trip.Jam)under=under-dir*(.08f+.04f*(j%2))+dir*.12f;
                float u=(t-go)/run;var path=target-under;float far=path.Length();p.Feet=under+path*u;p.Facing=far>1e-3f?Flat(path):dir;p.Step=far*u/(2*RunStride);
                float b=Bounce(far*u/RunStride);p.Hop=.022f*b;p.Squash=.9f+.2f*b;p.Lean=.45f;p.Arms=ArmsSwing;return true;
            }
            if(waves&&t<go+run+Wave)
            {
                float w=(t-go-run)/Wave;p.Feet=step;p.Facing=-dir;p.Standing=true;p.Arms=ArmsWave;p.Step=w*2.5f;p.Hop=.012f*Bounce(w*5);return true;
            }
            // The dive: a hop through the doorway head first, arms out ahead, shrinking as it passes the mouth.
            float q=Clamp((t-go-run-(waves?Wave:0))/Dive);var into=door+dir*.12f;
            p.Feet=Vector3.Lerp(target,into,q);p.Facing=dir;p.Hop=.09f*Sin(q*PI);p.Lean=.5f+.9f*q;p.Arms=ArmsUp;p.Hold=1;p.Standing=true;p.Scale=1-Smooth((q-.35f)/.65f);
            return true;
        }
        /// <summary>The model for a pose: its frame (a stride frame, or the standing one; a wave's frame is its hand's) and its arms.</summary>
        public static int Frame(in Pose p)=>p.Arms==ArmsWave?Phase(p.Step):p.Standing?WalkFrames:Phase(p.Step);
        private static int Phase(float cycles)=>(int)(((cycles%1)+1)%1*WalkFrames)%WalkFrames;

        // ---- the models (NativeFallenKit, sRGB hex colours, the Top Dog's toon palette) ----
        /// <summary>A friend: a hot dog stood on end in its bun, googly eyes on the sausage, stick legs and arms in white gloves, its feet at the
        /// origin facing +z. `frame` is the stride (WalkFrames to a step pair; WalkFrames itself stands, feet together), `arms` pumping as it
        /// runs, up over its head (palms up, a load on them) or the right one waving (its frame the wave's, the legs standing).</summary>
        public static NativeFallenKit Friend(int frame,int arms,int tier=0)
        {
            var k=new NativeFallenKit();float s=.78f,hip=.068f;bool stand=frame>=WalkFrames||arms==ArmsWave;float phase=(frame%WalkFrames)*2*(float)Math.PI/WalkFrames;int seg=tier>0?5:7;
            var body=Matrix4x4.CreateScale(s)*Matrix4x4.CreateRotationX((float)Math.PI/2)*Matrix4x4.CreateTranslation(0,hip+NativeTopDogModels.DogLength*s*.5f,0);
            NativeTopDogModels.HotDog(k,body,stand?0:phase*.5f,stand?.12f:.25f,0,1,tier,true);
            // The eyes, high on the sausage's front, and their pupils looking ahead.
            float ey=hip+NativeTopDogModels.DogLength*s*.8f,ez=NativeTopDogModels.SausageRadius*s+.004f;
            foreach(float side in new[]{-1f,1})
            {
                var e=new Vector3(side*.012f,ey,ez);k.Ellipsoid(e,Vector3.UnitX*.0105f,Vector3.UnitY*.013f,Vector3.UnitZ*.008f,tier>0?6:9,tier>0?4:6,NativeTopDogModels.White);
                k.Ellipsoid(e+new Vector3(side*.001f,-.002f,.007f),Vector3.UnitX*.0048f,Vector3.UnitY*.0056f,Vector3.UnitZ*.003f,5,4,NativeTopDogModels.Ink,line:false);
            }
            // A grin under them.
            var grin=new List<Vector3>();for(int i=0;i<=6;i++){float a=(i/6f-.5f)*1.6f;grin.Add(new Vector3((float)Math.Sin(a)*.012f,ey-.022f-.004f*(float)Math.Cos(a),ez+.001f));}k.Tube(grin,.0022f,4,NativeTopDogModels.Ink,line:false);
            // The legs: hips to feet; running, a long stride with the knee thrown up as each foot comes through; standing, side by side.
            foreach(float side in new[]{-1f,1})
            {
                float off=side>0?0:(float)Math.PI,swing=stand?0:(float)Math.Sin(phase+off),up=stand?0:Math.Max(0,(float)Math.Cos(phase+off));
                var hipAt=new Vector3(side*.02f,hip,0);var foot=new Vector3(side*.022f,.01f+up*.022f,swing*.045f);var knee=(hipAt+foot)*.5f+new Vector3(0,up*.012f,.008f+up*.016f);
                k.Tube(new[]{hipAt,knee,foot},.0062f,seg,NativeTopDogModels.Ink);
                k.Ellipsoid(foot+new Vector3(0,-.002f,.008f),Vector3.UnitX*.011f,Vector3.UnitY*.008f,Vector3.UnitZ*.017f,tier>0?6:8,tier>0?4:5,NativeTopDogModels.Red);
            }
            // The arms from the bun's sides: pumping, bent at the elbow, against the legs; up over the head; or the right waving overhead.
            float shoulder=hip+NativeTopDogModels.DogLength*s*.62f;
            foreach(float side in new[]{-1f,1})
            {
                var sh=new Vector3(side*.036f,shoulder,0);Vector3 elbow,hand;
                if(arms==ArmsPush){hand=new Vector3(side*.026f,shoulder+.012f,.07f);elbow=(sh+hand)*.5f+new Vector3(side*.014f,-.008f,0);}
                else if(arms==ArmsUp||arms==ArmsWave&&side>0)
                {
                    float wave=arms==ArmsWave?(float)Math.Sin(phase)*.028f:0;
                    hand=new Vector3(side*.03f+wave,shoulder+.1f,.012f);elbow=(sh+hand)*.5f+new Vector3(side*.012f,0,0);
                }
                else if(arms==ArmsWave){hand=new Vector3(side*.05f,shoulder-.075f,0);elbow=(sh+hand)*.5f+new Vector3(side*.012f,0,0);}
                else
                {
                    float swing=stand?0:(float)Math.Sin(phase+(side>0?Math.PI:0));
                    elbow=new Vector3(side*.048f,shoulder-.04f,-swing*.03f);hand=new Vector3(side*.046f,shoulder-.035f+.012f*Math.Max(0,swing),.028f+swing*.035f);
                }
                k.Tube(new[]{sh,elbow,hand},.0052f,seg,NativeTopDogModels.Ink);
                k.Ellipsoid(hand,Vector3.UnitX*.011f,Vector3.UnitY*.01f,Vector3.UnitZ*.011f,tier>0?6:8,tier>0?4:5,NativeTopDogModels.White);
            }
            k.Finish();return k;
        }
        private static List<Vector3> Arch(float w,float h,float z,float inset,int n)
        {
            var pts=new List<Vector3>();float r=w*.5f-inset,top=h-w*.5f;pts.Add(new Vector3(-r,0,z));pts.Add(new Vector3(-r,top,z));
            for(int i=1;i<n;i++){float a=(float)Math.PI*(1-i/(float)n);pts.Add(new Vector3((float)Math.Cos(a)*r,top+(float)Math.Sin(a)*r,z));}
            pts.Add(new Vector3(r,top,z));pts.Add(new Vector3(r,0,z));return pts;
        }
        /// <summary>The door's surround: an arched mustard frame round a dark doorway in a little wooden block, a step in front; its sill at the
        /// origin, its mouth facing +z.</summary>
        public static NativeFallenKit DoorFrame(int tier=0)
        {
            var k=new NativeFallenKit();int n=tier>0?10:16;float w=DoorWidth,h=DoorHeight;
            // The block behind: the arch's outline pushed back, its face round the doorway.
            var front=Arch(w+.08f,h+.05f,-.006f,0,n).ToArray();var back=Arch(w+.08f,h+.05f,-.07f,0,n).ToArray();var c0=new Vector3(0,h*.45f,-.006f);var c1=new Vector3(0,h*.45f,-.07f);
            k.Loft(new List<Vector3[]>{front,back},new[]{c0,c1},NativeTopDogModels.WoodDark,capLast:true);
            // The doorway: dark inside.
            k.Cap(Arch(w,h,-.004f,0,n).ToArray(),new Vector3(0,h*.4f,-.004f),Vector3.UnitZ,NativeTopDogModels.Ink,line:false);
            // The frame: a round mustard moulding up, over and down.
            k.Tube(NativeFallenKit.Curve(Arch(w,h,.004f,-.01f,n),2),.017f,tier>0?6:8,NativeTopDogModels.Mustard);
            // The step.
            k.Ellipsoid(new Vector3(0,.006f,.045f),Vector3.UnitX*(w*.6f),Vector3.UnitY*.012f,Vector3.UnitZ*.045f,tier>0?10:16,4,NativeTopDogModels.Wood);
            // A little hot dog sign over it.
            NativeTopDogModels.HotDog(k,Matrix4x4.CreateScale(.42f)*Matrix4x4.CreateRotationY((float)Math.PI/2)*Matrix4x4.CreateTranslation(0,h+.05f,.012f),0,0,0,1,tier,true);
            k.Finish();return k;
        }
        /// <summary>The door itself: a red arched leaf hinged at the origin (its frame's left side), shut along +x, a mustard knob.</summary>
        public static NativeFallenKit DoorLeaf(int tier=0)
        {
            var k=new NativeFallenKit();int n=tier>0?10:16;float w=DoorWidth,h=DoorHeight;
            var f=Arch(w,h,.002f,.012f,n);var b=Arch(w,h,-.012f,.012f,n);for(int i=0;i<f.Count;i++){f[i]+=new Vector3(w*.5f,0,0);b[i]+=new Vector3(w*.5f,0,0);}
            var cf=new Vector3(w*.5f,h*.45f,.002f);var cb=new Vector3(w*.5f,h*.45f,-.012f);
            k.Loft(new List<Vector3[]>{f.ToArray(),b.ToArray()},new[]{cf,cb},NativeTopDogModels.Red,capFirst:true,capLast:true);
            // Two panels pressed into it, and the knob.
            k.Tube(NativeFallenKit.Curve(Arch(w*.62f,h*.78f,.004f,0,n).ConvertAll(p=>p+new Vector3(w*.5f,h*.08f,0)),2),.004f,4,NativeTopDogModels.RedDark,line:false);
            k.Ellipsoid(new Vector3(w*.82f,h*.42f,.012f),Vector3.UnitX*.013f,Vector3.UnitY*.013f,Vector3.UnitZ*.01f,8,5,NativeTopDogModels.Mustard);
            k.Finish();return k;
        }
    }
}
