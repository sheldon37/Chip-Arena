using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What THE 6:47's presentation reads (docs/EXPEDITION-BOSS-CENTRAL-STATION.md section 10, the proposed `pve.zoneBoss`
    /// record, plus the actor's root and health and the arrival's `spawned`). Authority state, replicated by Astra; the
    /// presentation only reads it. Until it exists every arena's view is <see cref="None"/> (Runtime
    /// NativeStationBossPresentation.cs StationBossViewOf is the one binding point), the boss draws nothing and the placeholder
    /// Siren fights on as today; the DEV preview fills its own view (<see cref="NativeStationBossPreview"/>).
    /// <list type="bullet">
    /// <item>Times are the arena clock in seconds; NaN means "not happening" (the wire's -100 sentinel converts with
    /// <see cref="Time"/>). Seat ids are -1 when unset.</item>
    /// <item><c>phase</c> 1..3 (LAST CALL, PLATFORM ALTERATION, TERMINUS) and <c>phaseAt</c> the start of its 3.5 s change.</item>
    /// <item><c>action</c> one of <see cref="NativeStationBoss.Moves"/> ids (null: none), <c>actionId</c> unique per start,
    /// <c>actionAt</c>, <c>hitAt</c> (the authority's hit: actionAt plus the move's anticipation), <c>duration</c>,
    /// <c>cancelAt</c> (a stagger or a phase change cuts it), <c>actionTarget</c> (the locked pilot).</item>
    /// <item>The lane (<c>laneX0..laneZ1</c>): Departure's, locked at the fourth note; X0/Z0 is also Shunt's and Terminus's centre.</item>
    /// <item>The hold (<c>holdTarget</c>, <c>holdSide</c> -1/+1, <c>holdUntil</c>), the stagger (<c>staggerAt</c>,
    /// <c>staggerSeconds</c>, <c>staggerPushX/Z</c>, <c>staggerCause</c>: crash, express, part, phase), the breaks
    /// (<c>lampBrokenAt</c>, <c>pantoBrokenAt</c>, <c>expressHitAt</c>, <c>sideBroken</c> bit 0 left, bit 1 right), Live Rail
    /// (<c>liveAt</c>, <c>liveUntil</c>), the arena changes (<c>barrierDownAt</c>, <c>boardDownAt</c>, <c>roofOpenAt</c>), the
    /// spill (<c>spillAt</c>, <c>spillIds</c>) and <c>diedAt</c>.</item>
    /// </list>
    /// </summary>
    public struct StationBossView
    {
        /// <summary>A boss record exists (false: <see cref="None"/>, nothing is drawn).</summary>
        public bool live;
        /// <summary>The actor's own frame: the root on the floor (m) and its facing (degrees, Unity yaw), its health.</summary>
        public float x,y,z,yaw,hp,maxHp;
        /// <summary>The arrival's record time (bossArrivals[].spawned on the arena clock): the arrival plays from it.</summary>
        public float spawned;
        public int phase;public float phaseAt;
        public string action;public int actionId;public float actionAt,hitAt,duration,cancelAt;public int actionTarget;
        public float laneX0,laneZ0,laneX1,laneZ1;
        public int holdTarget,holdSide;public float holdUntil;
        public float staggerAt,staggerSeconds,staggerPushX,staggerPushZ;public string staggerCause;
        public float lampBrokenAt,pantoBrokenAt,expressHitAt;public int sideBroken;
        public float liveAt,liveUntil;
        public float barrierDownAt,boardDownAt,roofOpenAt;
        public float spillAt;public int[] spillIds;
        public float diedAt;

        /// <summary>Every arena's view until the rules replicate the record: no boss.</summary>
        public static readonly StationBossView None=new StationBossView{live=false,x=0,y=0,z=0,yaw=0,hp=0,maxHp=0,spawned=float.NaN,phase=1,phaseAt=float.NaN,
            action=null,actionId=0,actionAt=float.NaN,hitAt=float.NaN,duration=float.NaN,cancelAt=float.NaN,actionTarget=-1,laneX0=float.NaN,laneZ0=float.NaN,laneX1=float.NaN,laneZ1=float.NaN,
            holdTarget=-1,holdSide=0,holdUntil=float.NaN,staggerAt=float.NaN,staggerSeconds=0,staggerPushX=0,staggerPushZ=0,staggerCause=null,
            lampBrokenAt=float.NaN,pantoBrokenAt=float.NaN,expressHitAt=float.NaN,sideBroken=0,liveAt=float.NaN,liveUntil=float.NaN,
            barrierDownAt=float.NaN,boardDownAt=float.NaN,roofOpenAt=float.NaN,spillAt=float.NaN,spillIds=null,diedAt=float.NaN};

        /// <summary>A wire time (finite, -100 the sentinel for "never") as the view's time (NaN for never).</summary>
        public static float Time(double wire)=>wire<=-99.5||double.IsNaN(wire)||double.IsInfinity(wire)?float.NaN:(float)wire;
        public static bool At(float t)=>!float.IsNaN(t);
        /// <summary>The move this record plays (an index into <see cref="NativeStationBoss.Moves"/>), or -1.</summary>
        public int Move{get{if(action==null)return -1;var m=NativeStationBoss.Moves;for(int i=0;i<m.Length;i++)if(m[i].Id==action)return i;return -1;}}
        /// <summary>Whether something has happened by `time` (the event's time is set and not later).</summary>
        public static bool Since(float at,float time)=>At(at)&&time>=at;
        /// <summary>The phase shown at `time` (1..3): a phase change shows its new phase from its start.</summary>
        public int PhaseAt(float time)=>Math.Max(1,Math.Min(NativeStationBoss.Phases,phase));
        public bool Dead(float time)=>Since(diedAt,time);
        public bool LampBroken(float time)=>Since(lampBrokenAt,time);
        public bool PantoBroken(float time)=>Since(pantoBrokenAt,time);
        public bool BarrierDown(float time)=>Since(barrierDownAt,time);
        public bool BoardDown(float time)=>Since(boardDownAt,time);
        public bool RoofOpen(float time)=>Since(roofOpenAt,time);
        /// <summary>Whether Live Rail's puddles are live at `time`.</summary>
        public bool Live(float time)=>Since(liveAt,time)&&(!At(liveUntil)||time<liveUntil);
        /// <summary>Whether All Aboard holds a pilot at `time` (from the grab's landing to the release).</summary>
        public bool Holding(float time)=>holdTarget>=0&&At(hitAt)&&action=="doors"&&time>=hitAt&&At(holdUntil)&&time<holdUntil;
    }

    /// <summary>
    /// The arena's fixed places the boss presentation uses (doc sections 3, 6 and 10): Live Rail's six puddles under the broken
    /// panes, the barrier's centre segments, the departure board over the booth, the arrival's line, the doors (body local).
    /// </summary>
    public static class NativeStationBossArena
    {
        /// <summary>Live Rail's puddles (x, z, radius): the fixed points of the doc's presentation table.</summary>
        public static readonly float[] Puddles={-3.0f,6.0f,2.2f, -12.5f,8.0f,2.2f, 2.2f,.8f,2.2f, -15.0f,-1.2f,2.2f, -6.0f,11.2f,2.2f, 3.6f,3.4f,2.2f};
        public const int PuddleCount=6;
        /// <summary>The platform barrier (z and height) and its centre segments' span (x): the two that fall in PLATFORM ALTERATION.</summary>
        public const float BarrierZ=-4.2f,BarrierHeight=1.1f,BarrierX0=-18f,BarrierX1=18f,GateHalf=3f;
        /// <summary>The departure board over the booth (NativeExpeditionStationKit's box: centre, half sizes) and how it lies across the
        /// booth once torn down (pitch, centre height and z: the proposed 30 degree board collider).</summary>
        public const float BoardX=0,BoardY=8.6f,BoardZ=11f,BoardHalfX=5.2f,BoardHalfY=2.2f,BoardHalfZ=2.2f,BoardFallPitch=-30,BoardFallY=4.9f,BoardFallZ=11.4f;
        /// <summary>T1 (the express line the boss arrives on), the track bed, P1's edge, the crossing at x 0, the west mouth.</summary>
        public const float LineZ=-12.5f,TrackBed=-1.2f,PitNear=-10f,PitFar=-15f,PlatformZ=-7f,CrossingX=0,WestMouth=-48f;
        /// <summary>The doors (body local x, z): where Next Stop's adds step out and All Aboard's arms shoot from.</summary>
        public const float DoorX=1.47f,DoorZ=-.35f;
        /// <summary>The floor under a point as the boss sees it: the express pit's bed between P1 and P2 (T1's), the floor elsewhere.</summary>
        public static float Floor(float x,float z)
        {
            // The pit's walls as a steep 0.25 m ramp (a hand landing on the edge never jumps between the bed and the platform).
            float near=Clamp01((PitNear-z)/.25f),far=Clamp01((z-PitFar)/.25f);return TrackBed*Math.Min(near,far);
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>The floor for the boss's gait and chains (the pit's bed, the concourse and platforms).</summary>
    public sealed class StationBossGround:IBossGround{public static readonly StationBossGround Station=new StationBossGround();public float Height(float x,float z)=>NativeStationBossArena.Floor(x,z);}
}
