using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What THE CONVERTER's presentation reads (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md section 10.2 item 3: the proposed
    /// `pve.zoneBoss` record), plus the actor's own frame and health and the arrival's `spawned`. Authority state that Astra will
    /// replicate; the presentation only reads it. Until it exists every arena's view is <see cref="None"/> (Runtime
    /// NativeIronworksBossPresentation.cs IronworksBossViewOf is the one binding point), nothing of the boss is drawn and the
    /// placeholder Siren fights on as today. The DEV preview fills its own view (<see cref="NativeIronworksBossPreview"/>).
    /// <list type="bullet">
    /// <item>Times are on the arena clock (s); NaN means "not happening" (the wire's -100 converts with <see cref="Time"/>).</item>
    /// <item><c>action</c> one of <see cref="NativeIronworksBoss.Actions"/> ids (null none) from <c>actionAt</c>; its hit is
    /// <c>actionAt</c> + the action's windup (the table's), so contact is drawn exactly there.</item>
    /// <item><c>actionYaw</c> the facing locked at the start (degrees, Unity yaw; pour, slam, deslag, charge), <c>targetX</c> /
    /// <c>targetZ</c> the locked point (the pour's aim, the stomp's foot, the grab's target, the charge's end), <c>targetSeat</c>
    /// the grabbed pilot (-1 none: a grab whose seat clears before its follow-through ends was freed), <c>variant</c> the stomp's
    /// leg (0..3: FL, FR, RL, RR) or the call's vessels (bit 0 north, bit 1 south).</item>
    /// <item><c>phase</c> 1..3 and <c>phaseAt</c> the start of the current phase (the shed and the break play from it);
    /// <c>parts</c> bits 0-3 the crust groups intact (front, left, right, back), bit 4 the pulpit attached; <c>partAt0..4</c> each
    /// part's break time; <c>stagger</c> the 0..1 meter; <c>deathAt</c> the death's start.</item>
    /// </list>
    /// </summary>
    public struct IronworksBossView
    {
        /// <summary>A boss record exists (false: <see cref="None"/>, nothing is drawn).</summary>
        public bool live;
        /// <summary>The actor's own frame: the root on the floor under the trunnions (m), its facing (degrees, Unity yaw) and health.</summary>
        public float x,y,z,yaw,hp,maxHp;
        /// <summary>The arrival's record time (bossArrivals[].spawned, arena clock).</summary>
        public float spawned;
        public string action;public float actionAt,actionYaw,targetX,targetZ;public int targetSeat,variant;
        public int phase;public float phaseAt;
        public int parts;public float partAt0,partAt1,partAt2,partAt3,partAt4;
        public float stagger,deathAt;

        public const int PartFront=0,PartLeft=1,PartRight=2,PartBack=3,PartPulpit=4,Parts=5,AllParts=31;
        /// <summary>Every arena's view until the rules replicate the record: no boss.</summary>
        public static readonly IronworksBossView None=new IronworksBossView{live=false,x=0,y=0,z=0,yaw=0,hp=0,maxHp=0,spawned=float.NaN,action=null,actionAt=float.NaN,actionYaw=float.NaN,
            targetX=float.NaN,targetZ=float.NaN,targetSeat=-1,variant=0,phase=1,phaseAt=float.NaN,parts=AllParts,partAt0=float.NaN,partAt1=float.NaN,partAt2=float.NaN,partAt3=float.NaN,partAt4=float.NaN,
            stagger=0,deathAt=float.NaN};

        /// <summary>A wire time (finite, -100 the sentinel for "never") as the view's time (NaN for never).</summary>
        public static float Time(double wire)=>wire<=-99.5||double.IsNaN(wire)||double.IsInfinity(wire)?float.NaN:(float)wire;
        public static bool At(float t)=>!float.IsNaN(t);
        public static bool Since(float at,float time)=>At(at)&&time>=at;
        /// <summary>Part i's break time (NaN: intact or never recorded).</summary>
        public float PartAt(int i)=>i==0?partAt0:i==1?partAt1:i==2?partAt2:i==3?partAt3:i==4?partAt4:float.NaN;
        public void SetPartAt(int i,float t){if(i==0)partAt0=t;else if(i==1)partAt1=t;else if(i==2)partAt2=t;else if(i==3)partAt3=t;else if(i==4)partAt4=t;}
        /// <summary>Whether crust group g (0 front .. 3 back) or the pulpit (4) is still on.</summary>
        public bool Intact(int part)=>(parts&(1<<part))!=0;
        public bool Dead(float time)=>Since(deathAt,time);
        /// <summary>The playing action's index in <see cref="NativeIronworksBoss.Actions"/> (-1 none).</summary>
        public int Action{get{if(action==null||!At(actionAt))return -1;var a=NativeIronworksBoss.Actions;for(int i=0;i<a.Length;i++)if(a[i].Id==action)return i;return -1;}}
        /// <summary>The phase shown (1..3).</summary>
        public int Phase=>Math.Max(1,Math.Min(3,phase));
    }

    /// <summary>
    /// The Converter Hall's fixed places the boss presentation uses (the live `expedition-foundry` map and kit: docs/EXPEDITION-
    /// BOSS-CITY-IRONWORKS.md section 4): the hall's walls and roof, the pouring pit the boss rises from, boss point 0, the hall's
    /// two vessels' mouths, the converter crane's hook, the shutter in the east wall.
    /// </summary>
    public static class NativeIronworksBossArena
    {
        public const float HallX0=-50,HallX1=-24,HallZ0=-12,HallZ1=12,Roof=18;
        /// <summary>The pouring pit (x, z span) and its slag's surface; boss point 0 and its facing (east, toward the shutter).</summary>
        public const float PitX0=-38,PitX1=-32,PitZ0=-3,PitZ1=3,PitX=-35,PitZ=0,PitY=.035f,SpawnX=-35,SpawnZ=0,SpawnYaw=90;
        /// <summary>The hall's vessels A (north) and B (south): their mouths' centre (the kit's tipped mouth and molten pool).</summary>
        public const float VesselX=-44.2f,VesselY=9.35f,VesselZ=7f;
        /// <summary>The converter crane's hook over the pit (its eye and its tip).</summary>
        public const float HookX=-35,HookZ=0,HookTop=10.4f,HookTip=9.05f;
        /// <summary>The converter shutter's opening in the east wall (x, z half width, height).</summary>
        public const float ShutterX=-24,ShutterHalf=4,ShutterTop=5;
        /// <summary>Vessel `i` (0 A north, 1 B south) mouth's z.</summary>
        public static float VesselMouthZ(int i)=>i==0?VesselZ:-VesselZ;
        /// <summary>The hall's floor (flat; the pit's crust sits flush with it).</summary>
        public static float Floor(float x,float z)=>0;
        public static bool InPit(float x,float z)=>x>PitX0&&x<PitX1&&z>PitZ0&&z<PitZ1;
    }

    /// <summary>The floor for the boss's gait, chains and debris (the hall's flat floor).</summary>
    public sealed class IronworksBossGround:IBossGround{public static readonly IronworksBossGround Hall=new IronworksBossGround();public float Height(float x,float z)=>NativeIronworksBossArena.Floor(x,z);}

    /// <summary>
    /// A presentation clock that runs on the frame's own time and follows an authority clock that steps (the boss song clock during
    /// the intro hold advances only as the host's hold ticks reach this peer; the victory hold's age likewise): it never runs
    /// backwards, slews at most <see cref="MaxSkew"/> faster or slower toward the authority's value, and snaps when it is more
    /// than <see cref="Snap"/> off (a late join, a seek). NaN in, NaN out (and it resets). Allocation free.
    /// </summary>
    public struct IronworksBossClock
    {
        public double Value;public bool Running;
        public const double MaxSkew=.12,Snap=.25,CatchUp=4;
        public void Reset(){Running=false;Value=double.NaN;}
        public double Step(double authority,double dt)
        {
            if(double.IsNaN(authority)||double.IsInfinity(authority)){Reset();return double.NaN;}
            if(!Running||double.IsNaN(Value)||Math.Abs(authority-Value)>Snap){Value=authority;Running=true;return Value;}
            dt=Math.Max(0,Math.Min(.1,dt));double err=authority-Value,rate=1+Math.Max(-MaxSkew,Math.Min(MaxSkew,err*CatchUp));
            Value+=dt*rate;return Value;
        }
    }
}
