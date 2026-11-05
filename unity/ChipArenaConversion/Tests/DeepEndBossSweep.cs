using System;
using System.Numerics;
using ChipArena.UnityConversion;

// THE DEEP END's no-safe-place sweep (docs/EXPEDITION-BOSS-BACKROOMS.md section 1): for each phase, over every arena cell, corners and walls included, the moves' shapes from the table over a long stretch of the fight with a plausible
// rotation, for a pilot standing still and one circling. A spot that nothing threatens past the phase's limit fails. Threatened means the pilot stood inside a move's danger shape at any moment from its telegraph's lock to the end of its strike,
// so it had to respond (everything is dodgeable by moving, never ignorable by standing somewhere). The stillness move (The Lull) guarantees the still pilot's limit; removing it must expose a gap, or the sweep proves nothing.
public static class DeepEndBossSweep
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception(what);count++;}
    private const float Dt=.1f,Seconds=210;
    /// <summary>The phase's limit for a still pilot (the meter, then the Lull's wind and strike), and for a moving one (the rotation's reach).</summary>
    public static float StillLimit(int phase){var m=NativeDeepEndBoss.Move("lull");float meter=phase>=3?NativeDeepEndBoss.StillSecondsLast:NativeDeepEndBoss.StillSeconds;return meter+m.Anticipation+m.Strike+Dt*2;}
    public static float MoveLimit(int phase)=>phase==1?19f:phase==2?17f:15f;
    private static readonly string[][] Rotation={new[]{"breach","jet","flop","drain"},new[]{"seiche","breach","jet","whistle","flop","drain"},new[]{"lap","breach","seiche","jet","flop","whistle","drain"}};
    private struct Result{public float Gap;public Vector2 Spot;public string Last;}

    // Whether the pilot at p is inside move m's shape as locked on `lock` (from the head's place), at age e after the hit, the move's strike running.
    private static bool Inside(string id,DeepEndBossMove m,Vector2 p,Vector2 lockAt,Vector2 head,float e,int seed,int phase)
    {
        switch(id)
        {
            case "breach":return (phase>=2||NativeDeepEndArena.InPool(lockAt.X,lockAt.Y))&&Vector2.Distance(p,lockAt)<m.Radius;
            case "jet":
            {
                // Over `strike` s the 2 m beam sweeps the table's arc, starting on the aim (the target's place at lock), out to its reach.
                var d=p-head;float len=d.Length();if(len>m.Reach||len<.01f)return false;float a0=(float)Math.Atan2(lockAt.X-head.X,lockAt.Y-head.Y),sweep=m.Arc*BossMath.Deg*(seed%2==0?1:-1);
                float u=BossMath.Clamp01(e/m.Strike),a=a0+sweep*u,now=(float)Math.Atan2(d.X,d.Y);float da=Math.Abs(BossMath.WrapPi(now-a));return da*len<m.Width*.5f+.5f;
            }
            case "flop":
            {
                // The line through the target's place at lock, along the direction chosen by the seed, 24 m long, its width's lane.
                float an=seed*1.3f;var dir=new Vector2((float)Math.Sin(an),(float)Math.Cos(an));var off=p-lockAt;float along=Vector2.Dot(off,dir),across=Math.Abs(off.X*dir.Y-off.Y*dir.X);return Math.Abs(along)<m.Reach*.5f&&across<m.Width*.5f;
            }
            case "drain":return true;   // The whole room's water pulls toward the pit, at 5 to 9 m/s: a still pilot is carried to the mouth.
            case "seiche":return true;  // A wave crosses the room wall to wall, and back: jump it or roll through it.
            case "whistle":return true; // Adds surface round each pilot.
            case "lap":
            {
                // The rounded square its body runs (DeepEndBossPaths.LapAt): threatened when the body's lane passes over the place.
                float T=m.Strike+m.Follow,u=BossMath.Clamp01(e/T);var at=p;float best=float.MaxValue;
                for(float back=0;back<NativeDeepEndBossLook.Length;back+=1.5f){float uu=u-back/(T*m.Speed);if(uu<-1)break;var q=DeepEndBossPaths.LapAt(uu,seed*67*BossMath.Deg,m,T);best=Math.Min(best,Vector2.DistanceSquared(new Vector2(q.X,q.Z),at));}
                return best<m.Width*m.Width;
            }
        }
        return false;
    }
    private static Result Run(int phase,Vector2 spot,bool circling,bool withLull)
    {
        var rot=Rotation[phase-1];var ready=new float[rot.Length];var used=new float[rot.Length];for(int i=0;i<used.Length;i++)used[i]=-99+i;
        var r=new Result{Spot=spot,Last="start"};float last=0;float meter=0;Vector2 anchor=spot;float lullAt=-1;
        float limit=phase>=3?NativeDeepEndBoss.StillSecondsLast:NativeDeepEndBoss.StillSeconds;var lull=NativeDeepEndBoss.Move("lull");
        // Circling: round a point held inside the room, at the pace pilots wade (half speed in the last phase).
        var centre=spot;const float rad=5.2f;float pace=phase>=3?2f:phase==2?2.6f:3.4f;float wall=NativeDeepEndArena.Half-.5f-rad;centre.X=Math.Clamp(centre.X,-wall,wall);centre.Y=Math.Clamp(centre.Y,-wall,wall);
        Vector2 Pos(float t){if(!circling)return spot;float a=t*pace/rad+spot.X;return centre+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*rad;}
        int act=-1;float actAt=0;Vector2 lockAt=default,head=default;var move=default(DeepEndBossMove);float nextPick=2;int seed=1;
        for(float t=0;t<Seconds;t+=Dt)
        {
            var p=Pos(t);
            // The meter: within the still radius of one spot.
            if(withLull)
            {
                if(Vector2.Distance(p,anchor)>NativeDeepEndBoss.StillRadius){anchor=p;meter=0;}else meter+=Dt;
                if(meter>=limit&&lullAt<0){lullAt=t;}
                if(lullAt>=0&&t>=lullAt+lull.Anticipation){if(Vector2.Distance(p,anchor)<=NativeDeepEndBoss.StillRadius){r.Gap=Math.Max(r.Gap,0);last=t;r.Last="lull";}lullAt=-1;meter=0;anchor=p;}
            }
            // The rotation: one move at a time, the global gap between them (doc section 1: one second), the longest-unused one that's ready.
            if(act<0&&t>=nextPick)
            {
                int pick=-1;float oldest=float.MaxValue;for(int i=0;i<rot.Length;i++)if(t>=ready[i]&&used[i]<oldest){oldest=used[i];pick=i;}
                if(pick>=0)
                {
                    act=pick;actAt=t;move=NativeDeepEndBoss.Move(rot[pick]);lockAt=p;seed++;used[pick]=t;ready[pick]=t+move.Duration+move.Cooldown;
                    // It rises where it is near someone: the pool's edge nearest the target in the first phase, anywhere within its reach after.
                    float hd=phase==1?NativeDeepEndArena.PoolHalf-1:18;head=new Vector2(Math.Clamp(p.X,-hd,hd),Math.Clamp(p.Y,-hd,hd));if(Vector2.Distance(head,p)<6){var away=head-p;if(away.LengthSquared()<.01f)away=new Vector2(1,0);head=p+Vector2.Normalize(away)*6;}
                }
            }
            if(act>=0)
            {
                float age=t-actAt;float e=age-move.Anticipation;
                if(e>=0&&e<=Math.Max(move.Strike,.1f)&&Inside(move.Id,move,p,lockAt,head,e,seed,phase)){last=t;r.Last=move.Id;}
                if(age>=move.Duration){act=-1;nextPick=t+1;}
            }
            float gap=t-last;if(gap>r.Gap)r.Gap=gap;
        }
        return r;
    }
    private static Result Worst(int phase,bool circling,bool withLull)
    {
        var worst=new Result{Gap=-1};int cells=NativeDeepEndArena.Cells;float c=PoolroomsLayout.Cell,half=NativeDeepEndArena.Half;
        void Try(Vector2 s){var r=Run(phase,s,circling,withLull);if(r.Gap>worst.Gap)worst=r;}
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++)Try(new Vector2((x-cells*.5f+.5f)*c,(z-cells*.5f+.5f)*c));
        // The corners and against the walls, tighter than a cell's centre.
        float w=half-.45f;foreach(var sx in new[]{-1,1})foreach(var sz in new[]{-1,1})Try(new Vector2(sx*w,sz*w));for(float a=-w;a<=w;a+=4){Try(new Vector2(a,w));Try(new Vector2(a,-w));Try(new Vector2(w,a));Try(new Vector2(-w,a));}
        return worst;
    }
    public static int Run(string root)
    {
        count=0;
        for(int phase=1;phase<=NativeDeepEndBoss.Phases;phase++)
        {
            var still=Worst(phase,false,true);var moving=Worst(phase,true,true);var bare=Worst(phase,false,false);
            Console.WriteLine($"deepend-sweep phase {phase}: still worst {still.Gap:F1} s at ({still.Spot.X:F1},{still.Spot.Y:F1}), limit {StillLimit(phase):F1}; circling worst {moving.Gap:F1} s at ({moving.Spot.X:F1},{moving.Spot.Y:F1}), limit {MoveLimit(phase):F1}; without the Lull still worst {bare.Gap:F1} s at ({bare.Spot.X:F1},{bare.Spot.Y:F1})");
            Check(still.Gap<=StillLimit(phase),$"phase {phase}: a still pilot at ({still.Spot.X},{still.Spot.Y}) goes {still.Gap:F1} s unthreatened");
            Check(moving.Gap<=MoveLimit(phase),$"phase {phase}: a circling pilot at ({moving.Spot.X},{moving.Spot.Y}) goes {moving.Gap:F1} s unthreatened");
            // The sweep has teeth: with the stillness move taken out, a still pilot's gap goes over the limit, somewhere.
            Check(bare.Gap>StillLimit(phase),$"phase {phase}: without the Lull the sweep finds nothing past {StillLimit(phase):F1} s (worst {bare.Gap:F1} s): it proves nothing");
        }
        Console.WriteLine("PASS deepend-sweep ("+count+" checks)");
        return count;
    }
}
