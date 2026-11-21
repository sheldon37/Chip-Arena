using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What THE DEEP END's presentation reads (docs/EXPEDITION-BOSS-BACKROOMS.md section 8, the proposed record beside `NativeZoneBossFrame`). Authority state, replicated by Astra; the
    /// presentation only reads it. Until it exists every arena's view is <see cref="None"/> (Runtime DeepEndBossViewOf is the one binding point) and nothing draws; the DEV preview fills its
    /// own (<see cref="NativeDeepEndBossPreview"/>). Times are the arena clock in seconds, NaN "not happening" (the wire's -100 converts with <see cref="Time"/>).
    /// <list type="bullet">
    /// <item><c>x, z</c> where it means to be (the abyss's centre through the arrival), <c>yaw</c> its facing (degrees), <c>hp, maxHp</c>; <c>spawned</c> the arrival's start.</item>
    /// <item><c>phase</c> 1..3, <c>phaseAt</c> the start of its 3.5 s change. <c>action</c> one of <see cref="NativeDeepEndBoss.Moves"/>' ids, <c>actionId</c> unique per start, <c>actionAt, hitAt, duration, cancelAt, actionTarget</c>.</item>
    /// <item>The move's places: <c>targetX, targetZ</c> Breach's locked circle, the Flop's line start, the Jet's first aim, the Lull's pilot; <c>aimX, aimZ</c> the Flop's line end, the Jet's last aim, the Seiche's direction (a unit vector).</item>
    /// <item>The route between moves: <c>diveAt</c>, <c>surfaceAt, surfaceX, surfaceZ</c>, <c>pathSeed</c> (each client derives the swim from them).</item>
    /// <item>The room: <c>tideY</c> (m over the deck; -.15 the level's own water), <c>tideFrom, tideAt, tideUntil</c> the rise, <c>whirlAt, whirlUntil</c> the Drain's turning, <c>seicheAt</c>.</item>
    /// <item><c>lull</c> the local pilot's stillness meter 0..1 (the warning), <c>parts</c> a bit per broken flank lamp with <c>partAt</c> their break times, <c>staggerAt, staggerSeconds, staggerCause</c> (lamps, stranded, phase), <c>diedAt</c>.</item>
    /// </list>
    /// </summary>
    public struct DeepEndBossView
    {
        public bool live;public float x,y,z,yaw,hp,maxHp,spawned;
        public int phase;public float phaseAt;
        public string action;public int actionId;public float actionAt,hitAt,duration,cancelAt;public int actionTarget;
        public float targetX,targetZ,aimX,aimZ;
        public float diveAt,surfaceAt,surfaceX,surfaceZ;public int pathSeed;
        public float tideY,tideFrom,tideAt,tideUntil,whirlAt,whirlUntil,seicheAt,lull;
        public int parts;public float[] partAt;
        public float staggerAt,staggerSeconds;public string staggerCause;
        public float diedAt;
        /// <summary>Every arena's view until the rules replicate the record: no boss.</summary>
        public static readonly DeepEndBossView None=new DeepEndBossView{live=false,spawned=float.NaN,phase=1,phaseAt=float.NaN,action=null,actionId=0,actionAt=float.NaN,hitAt=float.NaN,duration=float.NaN,cancelAt=float.NaN,actionTarget=-1,
            targetX=float.NaN,targetZ=float.NaN,aimX=float.NaN,aimZ=float.NaN,diveAt=float.NaN,surfaceAt=float.NaN,surfaceX=float.NaN,surfaceZ=float.NaN,pathSeed=0,tideY=PoolroomsLayout.WaterY,tideFrom=PoolroomsLayout.WaterY,tideAt=float.NaN,tideUntil=float.NaN,
            whirlAt=float.NaN,whirlUntil=float.NaN,seicheAt=float.NaN,lull=0,parts=0,partAt=null,staggerAt=float.NaN,staggerSeconds=0,staggerCause=null,diedAt=float.NaN};
        public static float Time(double wire)=>wire<=-99.5||double.IsNaN(wire)||double.IsInfinity(wire)?float.NaN:(float)wire;
        public static bool At(float t)=>!float.IsNaN(t);
        public static bool Since(float at,float time)=>At(at)&&time>=at;
        public int Move=>action==null?-1:NativeDeepEndBoss.Index(action);
        public bool Dead(float time)=>Since(diedAt,time);
        public bool LampBroken(int i,float time)=>(parts>>i&1)!=0&&(partAt==null||i>=partAt.Length||!At(partAt[i])||time>=partAt[i]);
        /// <summary>The move's clock at `time`: its age, and the share of each part. False when none plays.</summary>
        public bool Moving(float time,out DeepEndBossMove m,out float age,out float hit)
        {
            m=default;age=hit=0;int k=Move;if(k<0||!At(actionAt))return false;m=NativeDeepEndBoss.Moves[k];age=time-actionAt;hit=At(hitAt)?hitAt-actionAt:m.Anticipation;return age>=0&&age<=(At(duration)?duration:m.Duration)+2;
        }
    }
}
