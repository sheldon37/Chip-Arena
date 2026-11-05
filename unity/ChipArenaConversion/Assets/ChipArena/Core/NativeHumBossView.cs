using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What THE HUM's presentation reads (docs/EXPEDITION-BOSS-BACKROOMS.md section 8, the proposed record beside NativeZoneBossFrame, plus the actor's root and health and the arrival's
    /// `spawned`). Authority state, replicated by Astra; the presentation only reads it. Until it exists every arena's view is <see cref="None"/> (Runtime
    /// NativeHumBossPresentation.cs HumBossViewOf is the one binding point) and nothing is drawn; the DEV preview fills its own (<see cref="NativeHumBossPreview"/>).
    /// <list type="bullet">
    /// <item>Times are the arena clock in seconds; NaN means "not happening" (the wire's -100 sentinel converts with <see cref="Time"/>). Seat ids are -1 when unset.</item>
    /// <item><c>phase</c> 1..3 (HUM-BUZZ, BLACKOUT, NOCLIP), <c>phaseAt</c> the start of its 3.5 s change.</item>
    /// <item><c>action</c> one of <see cref="NativeHumBoss.Moves"/> ids, <c>actionId</c> unique per start, <c>actionAt</c>, <c>hitAt</c> (actionAt plus the move's wind), <c>duration</c>, <c>cancelAt</c>,
    /// <c>actionTarget</c> (the locked pilot).</item>
    /// <item>Hazards: the lunge's lane (<c>laneX0..Z1</c>); Row Burst's rows (<c>rowAxis</c>, <c>rowMask</c> a bit per row, <c>rowAt</c> the burst; the second burst of NOCLIP's double in <c>row2*</c>);
    /// Fall Through's circle (<c>tearX, tearZ, tearAt</c>) and the pit grid it leaves in NOCLIP (<c>torn</c>: x, z, at triples, <c>tornCount</c>); Max Hum's ring (<c>ringX, ringZ, ringAt</c>); Wall Hand
    /// (<c>handX, handY, handZ, handNx, handNz, handAt, handUntil</c>); Familiar Voices' wall points (<c>voices</c>: x, y, z, nx, nz, at).</item>
    /// <item>The room: <c>shiftAt</c> and <c>shiftMask</c> (a bit per partition: its place in NativeHumArena's table) with <c>shiftFrom</c> the mask before; <c>circuitMask</c> (dead circuits, a bit each) with <c>circuitAt</c>,
    /// when the last went.</item>
    /// <item>Its own parts: <c>parts</c> (bits 0..6 the tubes broken, bit 7 the head) and <c>partAt</c> (bit i's time, or NaN; a fixed eight); <c>clipAt</c> with <c>clipSeconds</c> (stuck half in a surface);
    /// the ceiling: <c>ceilingAt</c> (it starts the slow turn up: the root on the line it turns about) and <c>floorAt</c> (it starts back down).</item>
    /// <item>The stagger (<c>staggerAt, staggerSeconds, staggerPushX/Z, staggerCause</c>: choke, clip, part, phase), the death (<c>diedAt</c>), <c>heard</c> (the target's Fall Through meter 0..1, at <c>heardX, heardZ</c>).</item>
    /// </list>
    /// </summary>
    public struct HumBossView
    {
        public bool live;
        public float x,y,z,yaw,hp,maxHp;
        public float spawned;
        public int phase;public float phaseAt;
        public string action;public int actionId;public float actionAt,hitAt,duration,cancelAt;public int actionTarget;
        public float laneX0,laneZ0,laneX1,laneZ1;
        public int rowAxis,rowMask,row2Axis,row2Mask;public float rowAt,row2At;
        public float tearX,tearZ,tearAt;public int tornCount;public float[] torn;
        public float ringX,ringZ,ringAt;
        public float handX,handY,handZ,handNx,handNz,handAt,handUntil;
        public int voiceCount;public float[] voices;
        public float shiftAt;public int shiftMask,shiftFrom;
        public int circuitMask;public float circuitAt;
        public int parts;public float[] partAt;
        public float clipAt,clipSeconds;
        public float ceilingAt,floorAt;
        public float staggerAt,staggerSeconds,staggerPushX,staggerPushZ;public string staggerCause;
        public float diedAt;
        public float heard,heardX,heardZ;

        /// <summary>Every arena's view until the rules replicate the record: no boss.</summary>
        public static readonly HumBossView None=new HumBossView{live=false,spawned=float.NaN,phase=1,phaseAt=float.NaN,action=null,actionId=0,actionAt=float.NaN,hitAt=float.NaN,duration=float.NaN,cancelAt=float.NaN,actionTarget=-1,
            laneX0=float.NaN,laneZ0=float.NaN,laneX1=float.NaN,laneZ1=float.NaN,rowAt=float.NaN,row2At=float.NaN,tearX=float.NaN,tearZ=float.NaN,tearAt=float.NaN,ringX=float.NaN,ringZ=float.NaN,ringAt=float.NaN,
            handX=float.NaN,handY=float.NaN,handZ=float.NaN,handNx=0,handNz=0,handAt=float.NaN,handUntil=float.NaN,shiftAt=float.NaN,circuitAt=float.NaN,clipAt=float.NaN,clipSeconds=0,ceilingAt=float.NaN,floorAt=float.NaN,
            staggerAt=float.NaN,staggerSeconds=0,staggerCause=null,diedAt=float.NaN,heard=0,heardX=float.NaN,heardZ=float.NaN};

        /// <summary>A wire time (finite, -100 the sentinel for "never") as the view's time (NaN for never).</summary>
        public static float Time(double wire)=>wire<=-99.5||double.IsNaN(wire)||double.IsInfinity(wire)?float.NaN:(float)wire;
        public static bool At(float t)=>!float.IsNaN(t);
        public static bool Since(float at,float time)=>At(at)&&time>=at;
        /// <summary>The move this record plays (an index into <see cref="NativeHumBoss.Moves"/>), or -1.</summary>
        public int Move{get{if(action==null)return -1;var m=NativeHumBoss.Moves;for(int i=0;i<m.Length;i++)if(m[i].Id==action)return i;return -1;}}
        public bool Dead(float time)=>Since(diedAt,time);
        public bool TubeBroken(int i)=>(parts>>i&1)!=0;public bool HeadBroken=>(parts>>7&1)!=0;
        /// <summary>When a part broke (NaN: not yet, or not recorded).</summary>
        public float PartAt(int i)=>partAt!=null&&i<partAt.Length?partAt[i]:float.NaN;
        /// <summary>Whether it walks on the ceiling at `time`, and how far through the turn over: 0 the floor .. 1 the ceiling.</summary>
        public float Turned(float time)
        {
            float up=At(ceilingAt)?BossMath.Smoother((time-ceilingAt)/NativeHumBossMotion.TurnOver):0;
            float down=At(floorAt)?BossMath.Smoother((time-floorAt)/NativeHumBossMotion.TurnOver):0;
            return At(floorAt)&&At(ceilingAt)&&floorAt>=ceilingAt?up*(1-down):At(floorAt)&&At(ceilingAt)?Math.Max(up,1-down)*up:up;
        }
    }

    /// <summary>The motion's numbers (doc section 6): the gait, the head's stabiliser and the spring channels; the rig runs on them.</summary>
    public static class NativeHumBossMotion
    {
        /// <summary>The walk (m/s) and its cycle (m, one step of each of the four limbs; it takes one step where a pilot takes three), the duty, the limbs' lift (m) for the feet and hands.</summary>
        public const float WalkSpeed=2.3f,RunSpeed=6,Stride=3.0f,RunStride=5f,Duty=.66f,RunDuty=.5f,Lift=.34f,RunLift=.6f,TurnRate=62,TurnRateFast=110;
        /// <summary>The slow turn over from floor to ceiling (s).</summary>
        public const float TurnOver=1.7f;
        /// <summary>The head's stabiliser: how far the body may move before the head thrusts to catch up (m), the half lives at rest and thrusting, and how hard its look holds on the target (rad/s).</summary>
        public const float HeadSlack=.62f,HeadHold=.32f,HeadThrust=.13f,LookRate=9,LookAccel=60;
    }
}
