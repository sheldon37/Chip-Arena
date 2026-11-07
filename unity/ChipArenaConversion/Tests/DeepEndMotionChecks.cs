using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// THE DEEP END's body, measured frame by frame (the owner's report, 2026-11-07: weird twists, glitches, swimming where there's no water, appearing out of tile).
// The DEV preview's script and the real rules each drive the rig; every joint in sight is held to limits on twist, bend and speed, and no joint may stand in dry deck tile.
public static class DeepEndMotionChecks
{
    private sealed class Tally
    {
        public float Twist,Bend,Speed,Roll,Tile,Top;public int TileFrames,Frames;public float TwistAt,BendAt,SpeedAt,RollAt,TileAt;public int TwistJoint,BendJoint,SpeedJoint,RollJoint;
        public string Line(string name)=>$"{name,-10} twist {Twist,5:F1} deg at j{TwistJoint} t{TwistAt:F2}  bend {Bend,5:F1} deg at j{BendJoint} t{BendAt:F2}  speed {Speed,5:F1} m/s at j{SpeedJoint} t{SpeedAt:F2}  roll {Roll,6:F1} deg/s at j{RollJoint} t{RollAt:F2}  in tile {Tile:F2} m for {TileFrames} f t{TileAt:F2}  top {Top:F2} m  ({Frames} f)";
    }
    private const float Deg=180f/(float)Math.PI;
    private static readonly Vector3[] lastPos=new Vector3[NativeDeepEndBossLook.Spine],lastUp=new Vector3[NativeDeepEndBossLook.Spine];private static bool hasLast;
    private static void Measure(DeepEndBossRig r,Vector3 origin,float surface,float t,float dt,Tally a)
    {
        int n=NativeDeepEndBossLook.Spine;a.Frames++;
        var pos=r.Shown.Position;var rot=r.Shown.Rotation;
        for(int i=0;i<n;i++)
        {
            var p=pos[i]-origin;float radius=NativeDeepEndBossLook.Radius(i*NativeDeepEndBossLook.Seg);bool sight=p.Y+radius>surface&&NativeDeepEndArena.InRoom(p.X,p.Z);
            var up=Vector3.Transform(Vector3.UnitY,rot[i]);var fwd=Vector3.Transform(Vector3.UnitZ,rot[i]);
            a.Top=Math.Max(a.Top,p.Y+radius);
            // Standing in dry tile: over the deck with no water above it, part of it in sight and part below the floor.
            float floor=NativeDeepEndArena.Floor(p.X,p.Z);bool dry=surface<floor-.01f&&!NativeDeepEndArena.InPool(p.X,p.Z)&&NativeDeepEndArena.InRoom(p.X,p.Z);
            if(dry&&p.Y+radius>floor+.02f&&p.Y-radius*.82f<floor-.08f){float depth=floor-(p.Y-radius*.82f);if(depth>a.Tile){a.Tile=depth;a.TileAt=t;}a.TileFrames++;}
            if(!sight)continue;
            if(i>0)
            {
                var pp=pos[i-1]-origin;bool sightPrev=pp.Y+NativeDeepEndBossLook.Radius((i-1)*NativeDeepEndBossLook.Seg)>surface;
                if(sightPrev)
                {
                    var f0=Vector3.Transform(Vector3.UnitZ,rot[i-1]);var u0=Vector3.Transform(Vector3.UnitY,rot[i-1]);
                    float bend=(float)Math.Acos(Math.Clamp(Vector3.Dot(f0,fwd),-1,1))*Deg;if(bend>a.Bend){a.Bend=bend;a.BendAt=t;a.BendJoint=i;}
                    // The twist between neighbours: u0 carried along the bend, then the angle to this joint's up about its own axis.
                    var axis=Vector3.Cross(f0,fwd);float al=axis.Length();var carried=u0;if(al>1e-4f){axis/=al;carried=Vector3.Transform(u0,Quaternion.CreateFromAxisAngle(axis,(float)Math.Atan2(al,Vector3.Dot(f0,fwd))));}
                    float tw=(float)Math.Abs(Math.Atan2(Vector3.Dot(Vector3.Cross(carried,up),fwd),Vector3.Dot(carried,up)))*Deg;if(tw>a.Twist){a.Twist=tw;a.TwistAt=t;a.TwistJoint=i;}
                }
            }
            if(hasLast&&dt>0)
            {
                float speed=(p-lastPos[i]).Length()/dt;if(speed>a.Speed){a.Speed=speed;a.SpeedAt=t;a.SpeedJoint=i;}
                float roll=(float)Math.Abs(Math.Atan2(Vector3.Dot(Vector3.Cross(lastUp[i],up),fwd),Vector3.Dot(lastUp[i],up)))*Deg/dt;if(roll>a.Roll){a.Roll=roll;a.RollAt=t;a.RollJoint=i;}
            }
        }
        for(int i=0;i<n;i++){lastPos[i]=pos[i]-origin;lastUp[i]=Vector3.Transform(Vector3.UnitY,rot[i]);}hasLast=true;
    }
    public static void Run(string root)
    {
        // ---- the preview's script, move by move ----
        var byAct=new Dictionary<string,Tally>();var rig=new DeepEndBossRig();var origin=new Vector3(100,0,100);var view=DeepEndBossView.None;hasLast=false;
        const float dt=1f/60;float end=NativeDeepEndBossPreview.Loop-1;
        for(float t=0;t<end;t+=dt)
        {
            NativeDeepEndBossPreview.View(t,ref view,out var target);float surface=NativeDeepEndBossPreview.TideAt(t);
            rig.Update(t,dt,view,NativeDeepEndBossPreview.Route,origin,surface,true,new Vector3(target.X,1.2f,target.Y));
            string act="between";for(int i=0;i<NativeDeepEndBossPreview.ActCount;i++){var a=NativeDeepEndBossPreview.ActAt(i);if(t>=a.At&&t<a.At+a.Dur+.8f)act=a.Id;}
            if(t<NativeDeepEndBossPreview.ArrivalEnd)act="arrival";if(t>=NativeDeepEndBossPreview.DeathAt)act="death";
            if(!byAct.TryGetValue(act,out var tally))byAct[act]=tally=new Tally();
            Measure(rig,origin,surface,t,dt,tally);
        }
        Console.WriteLine("-- the preview's script");foreach(var kv in byAct)Console.WriteLine(kv.Value.Line(kv.Key));
        // ---- the real rules, in each phase, a pilot running circles round the room ----
        for(int phase=1;phase<=3;phase++)
        {
            var z=NativeBackroomsBossRules.Create(NativeDeepEndBoss.Archetype,10001,40,40,0);z.phase=phase;z.action=null;z.phaseAt=-100;float tide=phase==1?PoolroomsLayout.WaterY:phase==2?.5f:1.1f;z.backrooms.tideY=tide;z.backrooms.tideFrom=tide;z.backrooms.tideUntil=-100;
            var authority=new DeepEndBossRig();var live=new DeepEndBossRig();var output=new List<BackroomsBossContact>();var acts=new Dictionary<string,Tally>();hasLast=false;float hp=phase==1?1000:phase==2?600:200;var o=new Vector3(40,0,40);
            for(float t=20;t<190;t+=dt)
            {
                var feet=new Vector3(40+13*(float)Math.Sin(t*.17f),0,40+15*(float)Math.Cos(t*.13f));var p=new BackroomsBossPilot{Seat=1,Feet=feet};
                NativeBackroomsBossRules.Step(z,hp,1000,t,dt,new[]{p},output,authority);var b=z.backrooms;
                var v=NativeZoneBossViews.DeepEnd(z,b.x,authority.Root.Y,b.z,b.yaw,hp,1000,NativeZoneBossViews.BackroomsArrival(z));
                live.Update(t,dt,v,null,o,NativeBackroomsBossRules.Tide(z,t),true,feet-o);
                string act=z.action!=null&&t<z.actionAt+z.duration+.8f?z.action:"between";if(!acts.TryGetValue(act,out var tally))acts[act]=tally=new Tally();
                if(t>24)Measure(live,o,NativeBackroomsBossRules.Tide(z,t),t,dt,tally);
                var tr=Environment.GetEnvironmentVariable("DEEPEND_TRACE");if(tr!=null){var f=tr.Split(',');if(int.Parse(f[0])==phase&&t>=float.Parse(f[1])&&t<=float.Parse(f[2])){var hd=live.Shown.Position[0]-o;Console.WriteLine($"t{t:F2} {z.action} age {t-z.actionAt:F2} head {hd.X:F1},{hd.Y:F1},{hd.Z:F1} j8 {(live.Shown.Position[8]-o).Y:F1} j29 {(live.Shown.Position[29]-o).Y:F1} target {v.targetX:F1},{v.targetZ:F1} aim {v.aimX:F1},{v.aimZ:F1} x,z {v.x:F1},{v.z:F1}");}}
            }
            Console.WriteLine("-- the rules, phase "+phase);foreach(var kv in acts)Console.WriteLine(kv.Value.Line(kv.Key));
        }
    }
}
