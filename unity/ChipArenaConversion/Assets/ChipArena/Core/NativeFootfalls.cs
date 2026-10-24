using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One foot planting as the presentation drew it this frame (owner 2026-10-15: "can we add sounds for footsteps and zombies?").
    /// A pilot's plant comes from its drawn gait's contact feet (MeleeGait.Planted, through CapturedAnimation.Player.FootContacts);
    /// a zombie's from its gait's landed feet (NativeHordeGait.Landed). Presentation only: nothing here is read by the authority.
    /// <list type="bullet">
    /// <item>Pilot: <see cref="Id"/> its seat, <see cref="Speed"/> its horizontal speed, <see cref="Sprint"/>, <see cref="View"/> the
    /// local pilot seen in first person, <see cref="Land"/> a landing after <see cref="Air"/> s airborne (no step plays with it).</item>
    /// <item>Zombie: <see cref="Id"/> the add, <see cref="Kind"/> its drawn kind, <see cref="Foot"/> 0-1 the feet, 2-3 the crawler's
    /// hands, <see cref="Crawl"/> on four limbs, <see cref="Drag"/> the shambler's dragged foot, <see cref="Run"/> the walk-run blend.</item>
    /// <item>Both: (<see cref="X"/>, <see cref="Y"/>, <see cref="Z"/>) where the step sounds, Y the ground under the walker; (<see cref="FootX"/>,
    /// <see cref="FootZ"/>) the planted foot, where its surface is probed (<see cref="NativeFootSurface"/>); <see cref="Scale"/> the
    /// walker's drawn scale.</item>
    /// </list>
    /// </summary>
    public struct NativeFootfall
    {
        public int Id,Kind,Foot;public bool Zombie,Land,View,Sprint,Crawl,Drag;public float X,Y,Z,Speed,Air,Run,Time,FootX,FootZ,Scale;
    }

    /// <summary>
    /// The ground a foot contact lands on (owner 2026-10-16: "walking continuously on one surface cycles through sounds that resemble
    /// different terrains ... Keep concrete, metal, gravel, etc. in separate pools"): one probe down at the planted foot, the step key
    /// it names, and each walker's surface memory (<see cref="NativeSurfaceMemory"/>). Presentation only; the runtime casts the probe
    /// (Runtime/NativeFootstepAudio.cs) and the checks cast it against the maps' own colliders.
    /// <list type="bullet">
    /// <item>The probe starts <see cref="Above"/> over the foot's ground point (a pilot's replicated feet, the capsule's bottom; a
    /// zombie's floor) and reaches <see cref="Below"/> under it, both times the walker's scale when it is larger. A block whose top is
    /// at the feet is hit, a standing pilot's floor is always in reach, and a drawn position lagging a step's rise
    /// (NativeLocomotionRules.StepHeight) still starts above the new tread.</item>
    /// <item>A hit names its collider's material: the block's own, or the stair and ramp wedges' (their treads' physics material). An
    /// unnamed collider and a miss step on the map's floor (<see cref="NativeFootsteps.Surface(string,string)"/>).</item>
    /// <item>The zombies keep their gait's own takes on the hard floors (concrete, stone), and the crawler drags its body on every
    /// surface (a drag is the body's friction, which no surface's footfall resembles); on metal, wood, gravel and snow they step in that
    /// surface's own pool (move.step.*) at their own pitch and loudness.</item>
    /// </list>
    /// </summary>
    public static class NativeFootSurface
    {
        public const float Above=.4f,Below=.8f;
        public static float Scale(float scale)=>scale>1?scale:1;
        /// <summary>The probe's start height over a foot whose ground point is at <paramref name="groundY"/>.</summary>
        public static float StartY(float groundY,float scale)=>groundY+Above*Scale(scale);
        /// <summary>The probe's length (from its start, straight down).</summary>
        public static float Reach(float scale)=>(Above+Below)*Scale(scale);
        /// <summary>The step key of a probe on <paramref name="map"/>: a hit names its material (null: an unnamed collider), a miss
        /// is the map's floor.</summary>
        public static string Key(string map,bool hit,string material)=>NativeFootsteps.Surface(map,hit?material:null);
        /// <summary>The hard floors, where the zombies' own gait takes (shoes on asphalt, the heavy stone stomp) play.</summary>
        public static bool Hard(string step)=>step==null||step==NativeFootsteps.Concrete||step==NativeFootsteps.Stone;
        /// <summary>A zombie footfall's key: its gait's (<paramref name="gait"/>, NativeZombieSteps.Key) on a hard floor and for a
        /// crawl, else the surface's own step (<paramref name="step"/>).</summary>
        public static string Zombie(string gait,string step)=>gait==NativeZombieSteps.Crawling||Hard(step)?gait:step;
        /// <summary>The planted foot of two foot points: the one farther along the travel (vx, vz), since a planting foot leads its
        /// step; with less than .5 m/s of travel, or level, <paramref name="fallback"/>. 0 the first point, 1 the second.</summary>
        public static int Leading(float ax,float az,float bx,float bz,float vx,float vz,int fallback)
        {
            if(!(vx*vx+vz*vz>.25f))return fallback;float d=(ax-bx)*vx+(az-bz)*vz;return d>1e-4f?0:d<-1e-4f?1:fallback;
        }
        /// <summary>Within this horizontal distance of the walker a posed foot is believed (a rig mid-teleport is not).</summary>
        public const float FootReach=1.2f;
    }

    /// <summary>
    /// One walker's surface from plant to plant (a struct per pilot or add): the first plant, and the first after
    /// <see cref="Forget"/> s without one, a rewind or a landing (<c>fresh</c>), takes the probed surface at once; afterwards a different
    /// surface takes over only when <see cref="Confirm"/> plants in a row see it. A seam, a rail, an edge or one stray probe never
    /// flips a step, and a real boundary switches on its second plant and then stays. No allocation.
    /// </summary>
    public struct NativeSurfaceMemory
    {
        public const int Confirm=2;public const float Forget=2.5f;
        private string current,pending;private int seen;private float last;private bool has;
        public string Current=>current;
        /// <summary>The surface this plant steps on, given the surface it probed (<paramref name="observed"/>) at <paramref name="time"/>.</summary>
        public string Plant(string observed,float time,bool fresh=false)
        {
            if(!has||fresh||time<last-.1f||time-last>Forget){has=true;current=observed;pending=null;seen=0;last=time;return current;}
            last=time;
            if(observed==current){pending=null;seen=0;return current;}
            if(observed==pending){if(++seen>=Confirm){current=observed;pending=null;seen=0;}}
            else{pending=observed;seen=1;}
            return current;
        }
    }

    /// <summary>The frame's footfalls, written by the presentation and drained by NativeAudio (no allocation; a full queue drops).</summary>
    public sealed class NativeFootfallQueue
    {
        public const int Capacity=128;
        private readonly NativeFootfall[] items=new NativeFootfall[Capacity];
        public int Count{get;private set;}
        /// <summary>Footfalls refused because the queue was full (a check value: a crowd larger than the queue loses its farthest).</summary>
        public int Dropped{get;private set;}
        public ref readonly NativeFootfall this[int i]=>ref items[i];
        public bool Push(in NativeFootfall f){if(Count>=Capacity){Dropped++;return false;}items[Count++]=f;return true;}
        public void Clear()=>Count=0;
    }

    /// <summary>
    /// One pilot's footfalls from frame to frame (a struct the presentation keeps per seat): a step is a foot that becomes planted,
    /// each foot at most once a <see cref="FootGap"/> and any two steps at least <see cref="AnyGap"/> apart. Airborne feet are not
    /// tracked; touching down after <see cref="NativeFootsteps.MinAir"/> s is a landing, and the plants of the next
    /// <see cref="LandQuiet"/> s belong to it. A route with no gait (contacts -1: the Siren, the prototype's rigs) keeps the
    /// distance stride. A clock jump, a teleport or a fresh seat re-seats the feet without a step.
    /// </summary>
    public struct NativeStepTracker
    {
        public const float FootGap=.16f,AnyGap=.07f,LandQuiet=.12f;
        private bool seen,left,right,grounded;private float last,airAt,landAt,leftAt,rightAt,anyAt,distance,px,pz;private int alternate;
        /// <summary>
        /// This frame's plants (bit 1 the left foot, 2 the right) for `contacts` (the gait's planted feet, the same bits; -1 for no
        /// gait), and whether the pilot landed (`air` its seconds airborne).
        /// </summary>
        public int Step(int contacts,bool grounded,bool rolling,float x,float z,float speed,bool sprint,float time,out bool land,out float air)
        {
            land=false;air=0;float dx=x-px,dz=z-pz,moved=(float)Math.Sqrt(dx*dx+dz*dz);px=x;pz=z;
            if(!seen||time<last-.1f||time-last>.5f||moved>NativeFootsteps.MaxStep)
            {seen=true;last=time;this.grounded=grounded;left=contacts>=0&&(contacts&1)!=0;right=contacts>=0&&(contacts&2)!=0;distance=0;airAt=time;landAt=leftAt=rightAt=anyAt=-100;return 0;}
            last=time;
            if(!grounded){if(this.grounded)airAt=time;this.grounded=false;left=right=false;distance=0;return 0;}
            if(!this.grounded)
            {
                this.grounded=true;air=time-airAt;if(air>=NativeFootsteps.MinAir){land=true;landAt=anyAt=leftAt=rightAt=time;}
                left=contacts>=0&&(contacts&1)!=0;right=contacts>=0&&(contacts&2)!=0;distance=0;return 0;
            }
            int planted=0;
            if(contacts>=0)
            {
                bool l=(contacts&1)!=0,r=(contacts&2)!=0;
                if(l&&!left&&time-leftAt>=FootGap)planted|=1;if(r&&!right&&time-rightAt>=FootGap)planted|=2;left=l;right=r;
            }
            else if(!rolling&&speed>=NativeFootsteps.MinSpeed)
            {
                distance+=moved;float stride=NativeFootsteps.StrideFor(sprint);
                if(distance>=stride){distance-=stride;planted=alternate==0?1:2;alternate^=1;}
            }
            else distance=0;
            if(planted==0)return 0;
            // Both feet at once (a settle): one step. Rolling, landing and back-to-back plants are silent.
            if(planted==3)planted=alternate==0?1:2;
            if(rolling||time-landAt<LandQuiet||time-anyAt<AnyGap)return 0;
            if((planted&1)!=0)leftAt=time;else rightAt=time;anyAt=time;alternate=(planted&1)!=0?1:0;
            return planted;
        }
    }

    /// <summary>A footfall's gait for its loudness: a scuff (re-planting while nearly standing), a walk, a run, a sprint.</summary>
    public enum NativeStepGait:byte{Scuff,Walk,Run,Sprint}

    /// <summary>The zombies' footfalls (bank keys pve.zombie.step.*; docs/audio/SOUND-REQUESTS.md): by how the add moves.</summary>
    public static class NativeZombieSteps
    {
        public const string Shuffle="pve.zombie.step.shuffle",Running="pve.zombie.step.run",Heavy="pve.zombie.step.heavy",Crawling="pve.zombie.step.crawl";
        /// <summary>The key of a footfall: on four limbs a crawl; the brute, the bloater and the Hulk stomp; a run (the gait's run
        /// blend past half, a runner past a quarter) slaps; the rest shuffle.</summary>
        public static string Key(int kind,bool crawl,float run)
        {
            if(crawl||kind==NativeHordeKinds.Crawler)return Crawling;
            if(kind==NativeHordeKinds.Brute||kind==NativeHordeKinds.Bloater||kind==NativeHordeKinds.Hulk)return Heavy;
            return run>=.5f||kind==NativeHordeKinds.Runner&&run>=.25f?Running:Shuffle;
        }
        /// <summary>Whether limb `foot` sounds: on four limbs one hand's pull a cycle (2; each pull drags the body, so the cycle is one
        /// scrape, not four); upright the feet (0, 1).</summary>
        public static bool Sounds(bool crawl,int foot)=>crawl?foot==2:foot<2;
        /// <summary>The footfall's gain (the bank's trim and the distance falloff apply on top).</summary>
        public static float Gain(string key,int kind,bool drag)=>key==Heavy?(kind==NativeHordeKinds.Hulk?1f:.8f):key==Running?.5f:key==Crawling?.45f:drag?.5f:.38f;
        /// <summary>The add's footfall pitch: heavier kinds lower, each add a little apart (by its seed).</summary>
        public static float Rate(int kind,uint seed)
        {
            float k=kind==NativeHordeKinds.Hulk?.72f:kind==NativeHordeKinds.Brute?.86f:kind==NativeHordeKinds.Bloater?.82f:kind==NativeHordeKinds.Runner?1.05f:kind==NativeHordeKinds.Crawler?1.08f:.96f;
            return k*(.96f+.08f*NativeHordeKinds.Hash(seed,77));
        }
        /// <summary>The existing bank key a footfall borrows while its own key has no clips (a LocalAssets bank from before these
        /// keys): the heavy kinds' stone thud (today's), the others the map's floor step.</summary>
        public static string StandIn(string key,string floor)=>key==Heavy?"move.step.stone":floor;
        public static float StandInRate(string key,int kind)=>key==Heavy?(kind==NativeHordeKinds.Hulk?.55f:kind==NativeHordeKinds.Brute?.72f:.8f):.85f;
    }

    /// <summary>
    /// The frame's footfalls a listener hears (presentation only, the same rule on every peer for its own listener): the local
    /// pilot's always; the others by loudness (gain times the distance falloff), within <see cref="PilotRange"/> /
    /// <see cref="ZombieRange"/>, at most <see cref="MaxPerFrame"/> a frame, and each family within its rate
    /// (<see cref="PilotRate"/> / <see cref="ZombieRate"/> steps a second, bursts of <see cref="Burst"/>), so a crowd never floods
    /// the voices; a footfall more than <see cref="Stale"/> s older than the queue's newest (frames the audio missed) is dropped.
    /// The footfalls carry the presentation's clock (a guest's render time trails its latest snapshot), so staleness is judged
    /// within the queue. No allocation.
    /// </summary>
    public sealed class NativeStepMix
    {
        public const int MaxPerFrame=4;public const float PilotRange=45,ZombieRange=30,PilotRate=14,ZombieRate=10,Burst=4,Stale=.3f;
        public struct Pick{public int Index;public float Gain;public bool Mine;}
        private readonly Pick[] picks=new Pick[NativeFootfallQueue.Capacity];private readonly float[] order=new float[NativeFootfallQueue.Capacity];private readonly int[] index=new int[NativeFootfallQueue.Capacity];
        private float pilots=Burst,zombies=Burst;
        public int Count{get;private set;}
        public Pick this[int i]=>picks[i];
        public void Reset(){pilots=zombies=Burst;Count=0;}
        /// <summary>A footfall's loudness before the falloff (the gait, the landing, my own in first person quieter); every step,
        /// a pilot's or a zombie's, under <see cref="NativeFootsteps.StepTrim"/>.</summary>
        public static float Loudness(in NativeFootfall f,bool mine)
        {
            if(f.Zombie)return NativeZombieSteps.Gain(NativeZombieSteps.Key(f.Kind,f.Crawl,f.Run),f.Kind,f.Drag)*NativeFootsteps.StepTrim;
            float g=f.Land?NativeFootsteps.LandGain(f.Air):NativeFootsteps.StepGain(NativeFootsteps.Gait(f.Speed,f.Sprint))*NativeFootsteps.StepTrim;
            return g*(f.View?NativeFootsteps.ViewGain:mine?NativeFootsteps.MineGain:1);
        }
        /// <summary>Picks the footfalls of `q` to play for a listener at (lx, ly, lz); `local` the local pilot's seat (0 none); `dt`
        /// the seconds since the last pick (refills the rates). Returns <see cref="Count"/>.</summary>
        public int Choose(NativeFootfallQueue q,float lx,float ly,float lz,int local,float dt)
        {
            dt=Math.Max(0,Math.Min(.25f,dt));pilots=Math.Min(Burst,pilots+PilotRate*dt);zombies=Math.Min(Burst,zombies+ZombieRate*dt);
            int n=0,c=0;float newest=float.NegativeInfinity;for(int i=0;i<q.Count;i++)newest=Math.Max(newest,q[i].Time);
            for(int i=0;i<q.Count;i++)
            {
                ref readonly var f=ref q[i];if(!(newest-f.Time<=Stale))continue;
                bool mine=!f.Zombie&&local>0&&f.Id==local;float dx=f.X-lx,dy=f.Y-ly,dz=f.Z-lz,d=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
                float g=Loudness(f,mine);
                if(mine){picks[n++]=new Pick{Index=i,Gain=g,Mine=true};continue;}
                if(d>(f.Zombie?ZombieRange:PilotRange))continue;
                // Loudest first (an insertion keeps equal ones in queue order, so every runtime picks the same).
                float key=g*NativeEventSounds.Falloff(d);int at=c++;
                while(at>0&&order[at-1]<key){order[at]=order[at-1];index[at]=index[at-1];at--;}
                order[at]=key;index[at]=i;
            }
            int others=0;
            for(int k=0;k<c&&others<MaxPerFrame&&n<picks.Length;k++)
            {
                ref readonly var f=ref q[index[k]];ref float tokens=ref f.Zombie?ref zombies:ref pilots;if(tokens<1)continue;
                tokens-=1;others++;picks[n++]=new Pick{Index=index[k],Gain=Loudness(f,false)};
            }
            return Count=n;
        }
    }
}
