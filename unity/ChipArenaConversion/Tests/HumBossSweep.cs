#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// THE HUM's no-safe-place sweep (docs/EXPEDITION-BOSS-BACKROOMS.md section 1). For each phase, on a grid over every cell of the arena (corners and against walls included), it plays the move table's shapes over a long stretch of the fight with a plausible rotation, for a pilot standing still and
// one circling, and fails if any spot goes unthreatened past the phase's limit. The stillness move guarantees 6 s (4 s in NOCLIP) for a still pilot, so a run with Fall Through removed must show the sweep would catch a spot: it reports the worst spot and its gap. No rule here is the game's: a stand-in
// AI over the design's shapes. Astra should run the same sweep against the real rules.
public static class HumBossSweep
{
    private const float Step=.1f;
    private static readonly HumBossMove MTear=NativeHumBoss.Move("tear"),MLunge=NativeHumBoss.Move("lunge"),MPulse=NativeHumBoss.Move("pulse");
    private static readonly string[] Rot1={"reach","stoop","row","lunge","reach","stoop"},Rot2={"reach","pulse","hand","cut","row","lunge","voices","stoop"},Rot3={"reach","pulse","row","lunge","hand","cut","stoop","lunge"};
    // A threat: was this point inside a move's shape (a hit it had to answer) at this time.
    private sealed class Sim
    {
        public readonly HumRoom Room=new HumRoom(2,2,Vector3.Zero);
        public Vector2 Boss;public float Yaw;public int Phase;public bool NoTear;public int Mask;public int Rng=12345;
        private readonly Dictionary<string,float> ready=new Dictionary<string,float>();
        public float Rand(){unchecked{Rng^=Rng<<13;Rng^=(int)((uint)Rng>>17);Rng^=Rng<<5;}return (Rng&0xffffff)/16777216f;}
        private string[] Rotation=>Phase==1?Rot1:Phase==2?Rot2:Rot3;
        private float MoveEnd=-1;private string playing;private float hit;public Vector2 LaneA,LaneB;private int rowAxis,rowMask;private float tearAt=float.NaN;private Vector2 tearP;private float stillFrom;private Vector2 stillAt;private Vector2 last;private float ringAt=float.NaN;private Vector2 ringP;private float lastTear=-100;
        public float ThreatAt=float.NaN;
        /// <summary>How close a shape must land to count (m): 0 for a still pilot; a moving pilot only misses a shape by moving, so a near miss counts.</summary>
        public float Near;
        public void Reset(Vector2 pilot){Boss=new Vector2(Room.Centre.X,Room.MaxZ-7);Yaw=0;ready.Clear();MoveEnd=-1;playing=null;tearAt=float.NaN;stillFrom=0;stillAt=pilot;last=pilot;ringAt=float.NaN;Rng=12345+(int)(pilot.X*7+pilot.Y*131);Mask=0;Elapsed=0;ThreatAt=float.NaN;lastShift=0;}
        public float Elapsed;private float lastShift;
        // One step of the stand-in fight. Returns whether `pilot` is inside a landed shape this step.
        public bool Tick(float t,Vector2 pilot)
        {
            Elapsed=t;bool threat=false;
            // The Fall Through meter: within 3 m for the phase's limit.
            if((pilot-stillAt).Length()>NativeHumBoss.StillRadius){stillAt=pilot;stillFrom=t;}
            if(!NoTear&&float.IsNaN(tearAt)&&t-stillFrom>=NativeHumBoss.StillFor(Phase)){tearAt=t+MTear.Wind;tearP=pilot;}
            if(!float.IsNaN(tearAt)&&t>=tearAt){if((pilot-tearP).Length()<MTear.Radius)threat=true;tearAt=float.NaN;stillFrom=t;stillAt=pilot;lastTear=t;}
            // Shifts on their period move the partitions (they only change Wall Hand's nearest surface).
            float every=NativeHumBoss.ShiftEvery[Phase-1];if(t-lastShift>=every){Mask=(int)(Rand()*256);lastShift=t;}
            // The rotation: a gap, the next move off its cooldown. It walks toward the pilot between moves.
            if(playing==null||t>=MoveEnd)
            {
                playing=null;var d=pilot-Boss;float dist=d.Length();if(dist>.1f){Yaw=(float)Math.Atan2(d.X,d.Y);if(dist>2.6f)Boss+=d/dist*NativeHumBossMotion.WalkSpeed*Step;}
                if(t>=nextMoveAt)
                {
                    // Of what it can do from here, whatever has been ready longest.
                    string best=null;float bestReady=float.MaxValue;
                    for(int k=0;k<Rotation.Length;k++)
                    {
                        string id=Rotation[k];var m=NativeHumBoss.Move(id);float r=ready.TryGetValue(id,out float rr)?rr:0;if(t<r||r>=bestReady)continue;
                        // A short reach waits to be near; Stoop waits to be on top of them; too far to reach, Noclip closes any gap first.
                        if((id=="reach"&&dist>m.Reach+1.2f)||(id=="stoop"&&dist>m.Radius+1.5f))continue;
                        if(dist>16&&id!="lunge"&&id!="pulse"&&!(ready.TryGetValue("lunge",out float lr)&&t<lr))continue;
                        best=id;bestReady=r;
                    }
                    if(best!=null){var m=NativeHumBoss.Move(best);playing=best;hit=t+m.Wind;MoveEnd=t+m.Duration;ready[best]=t+m.Cooldown*(Phase>=3?.8f:1);nextMoveAt=MoveEnd+NativeHumBoss.MoveGap;Begin(best,pilot,t);}
                }
            }
            if(playing!=null&&t>=hit&&t<hit+Step*1.01f)threat|=Land(playing,pilot,t);
            // A lunge's body and Max Hum's ring land over a stretch.
            if(playing=="lunge"){if(t>=hit&&t<hit+MLunge.Strike){float k=(t-hit)/MLunge.Strike;var at=LaneA+(LaneB-LaneA)*k;if(Lane(pilot,LaneA,LaneB,MLunge.Width*.5f+Near)&&(pilot-at).Length()<MLunge.Width+Near)threat=true;Boss=at;}}
            if(!float.IsNaN(ringAt)&&t>=ringAt){float r=(t-ringAt)*MPulse.Speed;float d=(pilot-ringP).Length();if(d>r-MPulse.Speed*Step-.01f&&d<=r)threat=true;if(r>60)ringAt=float.NaN;}
            return threat;
        }
        private float nextMoveAt;
        private void Begin(string id,Vector2 pilot,float t)
        {
            if(id=="row"){rowAxis=(rowAxis+1)%2;rowMask=(int)(Rand()*2)==0?0b010101:0b101010;}
            if(id=="lunge"){var v=pilot-Boss;float dl=v.Length();var d=Vector2.Normalize(v+new Vector2(.001f,.001f));LaneA=Boss;float L=Math.Min(MLunge.Reach,dl+5);var e=Boss+d*L;e.X=BossMath.Clamp(e.X,Room.MinX+1,Room.MaxX-1);e.Y=BossMath.Clamp(e.Y,Room.MinZ+1,Room.MaxZ-1);LaneB=e;}
            if(id=="cut"){var d=pilot-Boss;Yaw=(float)Math.Atan2(d.X,d.Y);var f=new Vector2((float)Math.Sin(Yaw),(float)Math.Cos(Yaw));Boss=pilot-f*2.4f;}
        }
        private bool Land(string id,Vector2 pilot,float t)
        {
            var m=NativeHumBoss.Move(id);var d=pilot-Boss;float dist=d.Length();var f=new Vector2((float)Math.Sin(Yaw),(float)Math.Cos(Yaw));
            switch(id)
            {
                case "reach":return dist<=m.Reach+Near&&Vector2.Dot(Vector2.Normalize(d+new Vector2(1e-4f,0)),f)>=(float)Math.Cos(m.Arc*.5f*BossMath.Deg);
                case "stoop":return dist<=m.Radius+Near;
                case "cut":return dist<=m.Reach+1+Near&&Vector2.Dot(Vector2.Normalize(d+new Vector2(1e-4f,0)),f)>=(float)Math.Cos(m.Arc*.5f*BossMath.Deg);
                case "row":
                {
                    for(int r=0;r<NativeHumArena.FixtureSide;r++){if((rowMask>>r&1)==0)continue;float line=NativeHumArena.RowLine(Room,rowAxis,r);float at=rowAxis==0?pilot.Y:pilot.X;if(Math.Abs(at-line)<=m.Width*.5f+Near)return true;}
                    return false;
                }
                case "pulse":ringAt=t;ringP=Boss;return false;
                case "hand":{float w=NativeHumArena.NearestWall(Room,Mask,pilot.X,pilot.Y,out _,out _,out _,out _);return w<=m.Grab+Near;}
                default:return false;
            }
        }
        private static bool Lane(Vector2 p,Vector2 a,Vector2 b,float half){var ab=b-a;float l=ab.Length();if(l<1e-3f)return false;var u=ab/l;float s=Vector2.Dot(p-a,u);if(s<0||s>l)return false;return Math.Abs(p.X*u.Y-p.Y*u.X-(a.X*u.Y-a.Y*u.X))<=half;}
    }
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("THE HUM sweep: "+what);count++;}
    /// <summary>The longest stretch (s) at spot `at` with no landed shape reaching it, over `span` seconds in `phase`.</summary>
    private static float Gap(int phase,Vector2 at,bool circle,bool noTear,float span,HumRoom room)
    {
        var s=new Sim{Phase=phase,NoTear=noTear,Near=circle?1.5f:0};s.Reset(at);float last=0,worst=0;
        for(float t=0;t<span;t+=Step)
        {
            var p=at;if(circle){float a=t*1.1f;p=at+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*3.6f;p.X=BossMath.Clamp(p.X,room.MinX+.4f,room.MaxX-.4f);p.Y=BossMath.Clamp(p.Y,room.MinZ+.4f,room.MaxZ-.4f);}
            if(s.Tick(t,p)){worst=Math.Max(worst,t-last);last=t;}
        }
        return Math.Max(worst,span-last);
    }
    public static string Report="";
    public static int Run(string root)
    {
        count=0;var room=new HumRoom(2,2,Vector3.Zero);var sb=new System.Text.StringBuilder();
        // The limits (s): still: the meter, its wind and a little; circling: the rotation's own.
        float[] stillLimit={NativeHumBoss.StillFor(1)+NativeHumBoss.Move("tear").Wind+.5f,NativeHumBoss.StillFor(2)+NativeHumBoss.Move("tear").Wind+.5f,NativeHumBoss.StillFor(3)+NativeHumBoss.Move("tear").Wind+.5f},moveLimit={24,24,20};
        const float span=110;
        for(int phase=1;phase<=3;phase++)
        {
            float worstStill=0,worstMove=0,worstNoTear=0;Vector2 atStill=default,atMove=default,atNoTear=default;
            // Every cell's centre plus each corner of the room, and hard against each wall.
            float cell=BackroomsLayout.Cell;
            for(int z=-1;z<=NativeHumArena.Cells;z++)for(int x=-1;x<=NativeHumArena.Cells;x++)
            {
                var p=new Vector2(x<0?room.MinX+.45f:x>=NativeHumArena.Cells?room.MaxX-.45f:room.MinX+(x+.5f)*cell,z<0?room.MinZ+.45f:z>=NativeHumArena.Cells?room.MaxZ-.45f:room.MinZ+(z+.5f)*cell);
                float g=Gap(phase,p,false,false,span,room);if(g>worstStill){worstStill=g;atStill=p;}
                float n=Gap(phase,p,false,true,span,room);if(n>worstNoTear){worstNoTear=n;atNoTear=p;}
                if(((x+z)&1)==0){float m=Gap(phase,p,true,false,span,room);if(m>worstMove){worstMove=m;atMove=p;}}
            }
            Check(worstStill<=stillLimit[phase-1],"phase "+phase+": a still pilot at ("+atStill.X.ToString("0.0")+", "+atStill.Y.ToString("0.0")+") goes "+worstStill.ToString("0.0")+" s unthreatened (limit "+stillLimit[phase-1]+")");
            Check(worstMove<=moveLimit[phase-1],"phase "+phase+": a circling pilot round ("+atMove.X.ToString("0.0")+", "+atMove.Y.ToString("0.0")+") goes "+worstMove.ToString("0.0")+" s unthreatened (limit "+moveLimit[phase-1]+")");
            Check(worstNoTear>stillLimit[phase-1],"phase "+phase+": with Fall Through removed the sweep must catch a spot (it found only "+worstNoTear.ToString("0.0")+" s)");
            sb.Append("  "+NativeHumBoss.PhaseNames[phase-1]+": still worst "+worstStill.ToString("0.0")+" s at ("+(atStill.X-room.MinX).ToString("0.0")+", "+(atStill.Y-room.MinZ).ToString("0.0")+") limit "+stillLimit[phase-1].ToString("0.0")+"; circling worst "+worstMove.ToString("0.0")+" s at ("+(atMove.X-room.MinX).ToString("0.0")+", "+(atMove.Y-room.MinZ).ToString("0.0")+") limit "+moveLimit[phase-1]+"; without Fall Through, still worst "+worstNoTear.ToString("0.0")+" s at ("+(atNoTear.X-room.MinX).ToString("0.0")+", "+(atNoTear.Y-room.MinZ).ToString("0.0")+") (caught)\n");
        }
        Report=sb.ToString();Console.Write(Report);
        return count;
    }
}
#endif
