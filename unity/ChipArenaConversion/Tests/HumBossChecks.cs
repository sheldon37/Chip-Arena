#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// THE HUM (docs/EXPEDITION-BOSS-BACKROOMS.md section 3): normal play is unchanged (the view's binding returns None: nothing draws, lights, sounds or changes on the level); the moves table is the doc's; the phrases land on the record's hit; the DEV preview is off by default and a pure function
// of the clock; every planted limb stays planted; nothing pops; the head stays level and locked; nothing allocates per frame; the skeleton is the modelling script's; the room's partitions never seal a pocket.
public static class HumBossChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("THE HUM: "+what);count++;}
    private static readonly Vector3 Target=new Vector3(26,1.6f,14);
    public static void Run(string root)
    {
        count=0;
        Check(NativeHumBoss.Archetype=="level0-hum","the archetype Astra binds");
        Check(NativeBackroomsBossIntro.Mine(NativeHumBoss.Archetype)&&NativeBackroomsBossIntro.Shots(NativeHumBoss.Archetype).Length==5,"the intro's five shots");
        Table(root);None(root);Determinism();Contact(out string contact);Feet(out string feet);Pops(out string pops);Head(out string head);Allocation(out string alloc);Skeleton(root);Room(out string room);Intro();Cues(root);Lanes();
        Console.WriteLine("PASS hum-boss ("+count+" checks: "+contact+"; "+feet+"; "+pops+"; "+head+"; "+alloc+"; "+room+")");
    }
    // ---- the table against the doc's ----
    private static void Table(string root)
    {
        var doc=File.ReadAllText(Path.Combine(root,"..","..","docs","EXPEDITION-BOSS-BACKROOMS.md"));
        var ids=new[]{"reach","stoop","row","lunge","tear","shift","pulse","cut","hand","voices"};Check(ids.Length==NativeHumBoss.Moves.Length,"every move");
        foreach(var id in ids)
        {
            var m=NativeHumBoss.Move(id);Check(m.Id==id,id+" is in the table");
            var row=Regex.Match(doc,"\\| `"+id+"` \\| ([^|]+)\\| (\\d) \\| ([.\\d]+) \\| ([.\\d]+) \\| ([.\\d]+) \\| ([.\\d]+) \\|");
            Check(row.Success,id+" is in the doc's table");
            Check(row.Groups[1].Value.Trim()==m.Title,id+"'s title");Check(int.Parse(row.Groups[2].Value)==m.Phase,id+"'s phase");
            float F(int g)=>float.Parse(row.Groups[g].Value,System.Globalization.CultureInfo.InvariantCulture);
            Check(Math.Abs(F(3)-m.Wind)<1e-4f&&Math.Abs(F(4)-m.Strike)<1e-4f&&Math.Abs(F(5)-m.Follow)<1e-4f&&Math.Abs(F(6)-m.Recover)<1e-4f,id+"'s timings are the doc's");
            int i=NativeHumBoss.MoveIndex(id);var def=HumBossMoves.Defs[i];Check(def!=null&&HumBossMoves.Keys[i].Length>=2,id+" has a phrase");
            if(id!="shift")Check(Math.Abs(def.Seconds-m.Duration)<1e-3f&&Math.Abs(def.HitAt-m.Wind)<1e-3f,id+"'s phrase sums to its table, its hit on the wind");
        }
        Check(NativeHumBoss.Phase(1)==1&&NativeHumBoss.Phase(.65f)==2&&NativeHumBoss.Phase(.3f)==3,"the phases at 65% and 30%");
        Check(NativeHumBoss.StillFor(1)==6&&NativeHumBoss.StillFor(3)==4,"the anti-camp meter");
        Check(NativeHumBoss.WeakPoints.Length==9&&NativeHumBoss.WeakPoints.Count(w=>w.Breaks)==8,"the weak points: the head, the Ballast and seven tubes");
    }
    // ---- None draws nothing, and no boss leaves the level as it is ----
    private static void None(string root)
    {
        var none=HumBossView.None;Check(!none.live&&none.Move==-1&&!none.Dead(100)&&none.Turned(100)==0,"the None view has no boss, move or turn");
        var rig=new HumBossRig();var fx=new NativeHumBossFx();var st=new HumBossStage();var cues=new HumBossCue[32];var ground=new BossFlatGround(0);
        for(int f=0;f<120;f++){float t=f/60f;st.Evaluate(none,t,HumBossBeats.Fallback,NativeHumBossPreview.Room,false);rig.Update(t,1/60f,none,ground,Vector3.Zero,false,true,Target);fx.Build(rig,none,st,t,0,2,10,1,0,0,0,1,0,1,false,false);
            Check(!rig.Visible&&fx.Quads.Count==0&&fx.LightCount==0&&fx.Surface.Patches==0&&NativeHumBossCues.Collect(none,rig,NativeHumBossPreview.Room,t-1/60f,t,cues)==0,"None draws no boss, no effect, no light, no surface and no cue");}
        Check(!st.On&&st.Air==1,"the room is as it is");for(int i=0;i<st.Lamp.Length;i++)Check(st.Lamp[i]==1&&!st.Burst[i],"every lamp as the level has it");
        Check(!NativeHumBossPreview.On&&double.IsNaN(NativeHumBossPreview.Start),"the DEV preview is off by default");
        var rt=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHumBossPresentation.cs"));Check(rt.Contains("internal static HumBossView HumBossViewOf(NativeArena arena)=>HumBossView.None;"),"the binding returns None until Astra binds it");
        var room=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHumBossRoom.cs"));Check(room.Contains("if(!NativeHumBossPreview.On)return layout;"),"with no preview the level's own layout draws");
        // A level layout comes out of the presentation copy untouched.
        var l=NativeBackroomsGenerator.Build(NativeBackroomsGenerator.ArenaSeed,false).Layout;var before=l.WallX.Concat(l.WallZ).Concat(l.Floor).Concat(l.Pillar).Concat(l.Circuit).ToArray();var c=NativeHumArena.PresentationCopy(l,8,8,Vector3.Zero,out var r);
        Check(before.SequenceEqual(l.WallX.Concat(l.WallZ).Concat(l.Floor).Concat(l.Pillar).Concat(l.Circuit)),"the level's own layout is untouched by the copy");
        for(int z=r.Z0;z<r.Z0+NativeHumArena.Cells;z++)for(int x=r.X0;x<r.X0+NativeHumArena.Cells;x++){Check(!c.Light[c.Index(x,z)],"the room's fixtures leave the static meshes");if(x+1<r.X0+NativeHumArena.Cells)Check(c.Between(x,z,1,0)==BackroomsLayout.Open,"the room is open");}
    }
    private static void Determinism()
    {
        var a=HumBossView.None;var b=HumBossView.None;
        for(int i=0;i<3000;i++)
        {
            float p=i*NativeHumBossPreview.Loop/3000;NativeHumBossPreview.View(p,10,ref a,out var va,out var ta);NativeHumBossPreview.View(p,10,ref b,out var vb,out var tb);
            Check(a.x==b.x&&a.z==b.z&&a.yaw==b.yaw&&a.action==b.action&&a.actionId==b.actionId&&float.Equals(a.hitAt,b.hitAt)&&a.phase==b.phase&&float.Equals(a.diedAt,b.diedAt)&&va==vb,"the preview's record is a pure function of the clock");
        }
        var r1=new HumBossRig();var r2=new HumBossRig();
        for(int f=0;f<=60*30;f++)
        {
            float t=f/60f;Step(r1,t,1/60f);Step(r2,t,1/60f);
            if(f%30==0)for(int j=0;j<HumBossRig.Count;j++)Check(r1.Shown.Position[j]==r2.Shown.Position[j]&&r1.Shown.Rotation[j]==r2.Shown.Rotation[j],"two runs draw the same pose to the bit");
        }
        // Effects: the same frame on two peers.
        var fa=new NativeHumBossFx();var fb=new NativeHumBossFx();var st=new HumBossStage();var r3=new HumBossRig();var r4=new HumBossRig();fa.Room=fb.Room=NativeHumBossPreview.Room;
        for(int f=0;f<=60*60;f+=1)
        {
            float t=f/60f;Step(r3,t,1/60f);Step(r4,t,1/60f);if(f%20!=0)continue;var v=HumBossView.None;NativeHumBossPreview.View(t,0,ref v,out _,out _);st.Evaluate(v,t,HumBossBeats.Fallback,NativeHumBossPreview.Room,false);
            fa.Build(r3,v,st,t,20,1.7,12,1,0,0,0,1,0,1,false,false);fb.Build(r4,v,st,t,20,1.7,12,1,0,0,0,1,0,1,false,false);
            Check(fa.Quads.Count==fb.Quads.Count&&fa.Vfx.Count==fb.Vfx.Count&&fa.Surface.Paper[0].VertexCount==fb.Surface.Paper[0].VertexCount,"two peers draw the same effects at "+t);
            for(int q=0;q<fa.Vfx.Count;q+=7)Check(fa.Vfx.Items[q].At==fb.Vfx.Items[q].At,"the same particle");
            for(int s=0;s<fa.Surface.Paper[0].VertexCount;s+=11)Check(fa.Surface.Paper[0].Vertices[s].P==fb.Surface.Paper[0].Vertices[s].P,"the same tear");
        }
    }
    private static void Step(HumBossRig r,float t,float dt){var v=HumBossView.None;NativeHumBossPreview.View(t,0,ref v,out var vel,out var tg);r.Update(t,dt,v,Ground,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));}
    private static readonly BossFlatGround Ground=new BossFlatGround(0);
    private static HumBossView Standing(){var v=HumBossView.None;v.live=true;v.x=26;v.z=26;v.yaw=180;v.phase=1;v.maxHp=8000;v.hp=8000;v.spawned=-100;return v;}
    // ---- contact lands on the record's hit ----
    private static void Contact(out string summary)
    {
        float worst=0;var moves=NativeHumBoss.Moves;
        for(int i=0;i<moves.Length;i++)foreach(float stretch in new[]{1f,1.15f,.9f})foreach(float fps in new[]{60f,144f})
        {
            var m=moves[i];if(m.Id=="shift")continue;var v=Standing();v.action=m.Id;v.actionId=100+i;v.actionAt=2;v.hitAt=2+m.Wind*stretch;v.duration=m.Duration-m.Wind+m.Wind*stretch;
            var rig=new HumBossRig();float struck=float.NaN;
            for(int f=0;f<=(int)(fps*(2+v.duration+.5f));f++){float t=f/fps;rig.Update(t,f==0?0:1/fps,v,Ground,Vector3.Zero,false,true,Target);if(float.IsNaN(struck)&&rig.Act.Active&&rig.Act.Struck)struck=t;}
            Check(!float.IsNaN(struck),m.Id+" reaches its contact");float err=struck-v.hitAt;worst=Math.Max(worst,Math.Abs(err));Check(err>=-1e-4f&&err<=1/fps+1e-4f,m.Id+"'s contact lands on the record's hit within one frame ("+err.ToString("0.0000")+" s at "+fps+" fps, x"+stretch+")");
        }
        summary="every move's contact lands on the record's hit within one frame (worst "+(worst*1000).ToString("0.0")+" ms)";
    }
    // ---- planted limbs don't slip ----
    private static void Feet(out string summary)
    {
        var rig=new HumBossRig();var prev=new Vector3[4];var held=new bool[4];float worst=0,worstT=0;int plants=0,worstLimb=-1;const float fps=60;
        for(int f=0;f<=(int)(fps*18);f++)
        {
            float t=f/fps;var v=Standing();WalkPath(t,out var p,out var vel,out float yaw);v.x=p.X;v.z=p.Z;v.yaw=yaw;
            rig.Update(t,f==0?0:1/fps,v,Ground,vel,false,true,Target);
            for(int i=0;i<4;i++){bool now=rig.PlantedFree(i);var h=rig.Limb(i);if(now&&held[i]){float sl=Vector3.Distance(h,prev[i]);if(sl>worst){worst=sl;worstLimb=i;worstT=t;}}if(now&&!held[i])plants++;held[i]=now;prev[i]=h;}
        }
        Check(plants>16,"the gait planted its limbs ("+plants+")");if(worst>=.012f)Console.WriteLine("slip limb "+worstLimb+" at "+worstT);Check(worst<.012f,"a planted limb slips less than 1.2 cm ("+(worst*100).ToString("0.000")+" cm)");
        summary=plants+" plants walking a curve, stopping and turning on the spot: a planted limb slips "+(worst*1000).ToString("0.00")+" mm at worst";
    }
    private static void WalkPath(float t,out Vector3 p,out Vector3 vel,out float yaw)
    {
        Vector3 P(float s){var c=new Vector3(26,0,26);float r=9;float a=Math.Min(s,9)*.24f;return c+new Vector3((float)Math.Sin(a)*r,0,-(float)Math.Cos(a)*r);}
        float Y(float s){float a=Math.Min(s,9)*.24f;float y=a/BossMath.Deg+90;if(s>9.5f)y+=BossMath.Smoother((s-9.5f)/4.5f)*150;return y;}
        p=P(t);vel=(P(t+1/240f)-P(t-1/240f))*120;yaw=Y(t);
    }
    // ---- nothing pops: the frame-to-frame change halves when the frame rate doubles, across the whole script ----
    private static void Pops(out string summary)
    {
        float W(float fps,bool print)
        {
            var r=new HumBossRig();var last=new BossPose(HumBossRig.Count);bool has=false;float worst=0,at=0;int joint=-1;var top=new System.Collections.Generic.List<(float mv,float t,int j)>();
            for(int f=0;f<=(int)(fps*NativeHumBossPreview.Loop);f++)
            {
                float t=f/fps;Step(r,t,f==0?0:1/fps);
                if(has&&!Jumpy(t))
                {
                    float mv=0;int jj=-1;for(int j=0;j<NativeHumBossLook.WallFore;j++){float d=Vector3.Distance(r.Shown.Position[j],last.Position[j]);if(d>mv){mv=d;jj=j;}}
                    if(mv>worst){worst=mv;at=t;joint=jj;}
                    if(print&&mv>.34f)top.Add((mv,t,jj));
                }
                last.CopyFrom(r.Shown);has=true;
            }
            worstAt=at;worstJoint=joint;
            if(print){int shown=0;float lastT=-9;foreach(var e in top){if(e.t-lastT>.25f&&shown<30){Console.WriteLine("pop "+e.mv.ToString("0.00")+" m at "+e.t.ToString("0.00")+" "+NativeHumBossLook.Names[e.j]);shown++;}lastT=e.t;}}
            return worst;
        }
        float a=W(60,Environment.GetEnvironmentVariable("HUM_POPS")!=null),b=W(120,false);
        Check(b<a*.78f+.02f,"a frame's largest joint move falls with the frame rate ("+a.ToString("0.000")+" m at 60 fps, "+b.ToString("0.000")+" m at 120 fps, at "+worstAt+" "+NativeHumBossLook.Names[Math.Max(0,worstJoint)]+")");
        summary="largest joint move "+(a*100).ToString("0.0")+" cm a frame at 60 fps and "+(b*100).ToString("0.0")+" at 120 (at "+worstAt.ToString("0.00")+", "+NativeHumBossLook.Names[Math.Max(0,worstJoint)]+"), Lights Out and the turn aside";
    }
    private static int worstJoint;
    private static float worstAt;
    private static bool Jumpy(float t)=>Math.Abs(t-(NativeHumBossPreview.CutAt+.41f))<.6f||Math.Abs(t-NativeHumBossPreview.FloorAt)<.3f;
    // ---- the head: level, and locked on its target while the body moves round it ----
    private static void Head(out string summary)
    {
        var rig=new HumBossRig();float worstRoll=0,worstAim=0,minH=9,maxH=0;
        for(int f=0;f<=60*10;f++)
        {
            float t=f/60f;var v=Standing();WalkPath(t,out var p,out var vel,out float yaw);v.x=p.X;v.z=p.Z;v.yaw=yaw;rig.Update(t,f==0?0:1/60f,v,Ground,vel,false,true,Target);if(t<1)continue;
            var q=rig.Shown.Rotation[NativeHumBossLook.Head];var r=Vector3.Transform(Vector3.UnitX,q);worstRoll=Math.Max(worstRoll,Math.Abs(r.Y));var fw=Vector3.Transform(Vector3.UnitZ,q);var to=Target-rig.HeadPos;to.Y=0;fw.Y=0;
            float aim=(float)Math.Acos(Math.Min(1,Vector3.Dot(Vector3.Normalize(fw),Vector3.Normalize(to))));worstAim=Math.Max(worstAim,aim);minH=Math.Min(minH,rig.HeadPos.Y);maxH=Math.Max(maxH,rig.HeadPos.Y);
        }
        Check(worstRoll<.002f,"the bar of light stays level ("+worstRoll+")");Check(worstAim<14*BossMath.Deg,"its look holds on the target within a few degrees while it walks and turns ("+worstAim/BossMath.Deg+")");Check(maxH-minH<.07f,"its height holds through the walk ("+(maxH-minH)+" m)");
        summary="walking and turning the bar's roll stays under .1 degree, its aim within "+(worstAim/BossMath.Deg).ToString("0.0")+" degrees, its height within "+((maxH-minH)*100).ToString("0.0")+" cm";
    }
    // ---- allocation ----
    private static void Allocation(out string summary)
    {
        var rig=new HumBossRig();var fx=new NativeHumBossFx();var st=new HumBossStage();var cues=new HumBossCue[48];fx.Room=NativeHumBossPreview.Room;long worst=0;
        // Warm it up through the whole loop once, then measure a second pass (frames only; the preview's View reads its fixed tables).
        void Frame(float t,float dt){var v=HumBossView.None;NativeHumBossPreview.View(t,0,ref v,out var vel,out var tg);st.Evaluate(v,t,HumBossBeats.Fallback,NativeHumBossPreview.Room,false);rig.Hitch=st.Hitch;rig.Update(t,dt,v,Ground,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));fx.Build(rig,v,st,t,20,1.7,12,1,0,0,0,1,0,1,false,false);NativeHumBossCues.Collect(v,rig,NativeHumBossPreview.Room,t-dt,t,cues);}
        for(float t=0;t<NativeHumBossPreview.Loop;t+=1/30f)Frame(t,1/30f);
        for(float t=0;t<NativeHumBossPreview.Loop;t+=.37f)
        {
            long before=GC.GetAllocatedBytesForCurrentThread();Frame(t,.1f);long d=GC.GetAllocatedBytesForCurrentThread()-before;worst=Math.Max(worst,d);
        }
        Check(worst==0,"a frame allocates nothing ("+worst+" bytes at worst)");
        summary="0 bytes allocated per frame across the preview (rig, stage, effects, surfaces, cues)";
    }
    // ---- the skeleton is the modelling script's ----
    private static void Skeleton(string root)
    {
        var py=File.ReadAllText(Path.Combine(root,"tools/gear/hum_boss.py"));
        foreach(var (name,val) in new[]{("UPPER_ARM",NativeHumBossLook.UpperArm),("FORE_ARM",NativeHumBossLook.ForeArm),("THIGH",NativeHumBossLook.Thigh),("SHIN",NativeHumBossLook.Shin)})
        {
            var cons=Regex.Match(py,"UPPER_ARM, FORE_ARM, PALM = ([.\\d]+), ([.\\d]+), ([.\\d]+)");Check(cons.Success,"the script's arm lengths");
            var legs=Regex.Match(py,"THIGH, SHIN = ([.\\d]+), ([.\\d]+)");Check(legs.Success,"the script's leg lengths");
            float U=float.Parse(cons.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture),Fo=float.Parse(cons.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture),Th=float.Parse(legs.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture),Sh=float.Parse(legs.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
            Check(U==NativeHumBossLook.UpperArm&&Fo==NativeHumBossLook.ForeArm&&Th==NativeHumBossLook.Thigh&&Sh==NativeHumBossLook.Shin,"the limbs' lengths agree");
        }
        var names=NativeHumBossLook.Names;Check(names.Length==NativeHumBossLook.Count&&names.All(n=>n!=null),"every joint is named");
        for(int j=0;j<NativeHumBossLook.Count;j++)Check(NativeHumBossLook.Parent[j]<j,"parents come first");
        Check(names[NativeHumBossLook.Finger(1,3,2)]=="finger-r3-2"&&names[NativeHumBossLook.Tube(6)]=="tube-6"&&names[NativeHumBossLook.Wire(6,1)]=="wire-6-1"&&names[NativeHumBossLook.Strip(9,3)]=="strip-9-3"&&names[NativeHumBossLook.Tile(3)]=="tile-3","the joints' numbers are the table's");
        // The limb lengths are the bones' (the bind pose is two-bone solved).
        Check(Math.Abs(Vector3.Distance(NativeHumBossLook.Bind[NativeHumBossLook.UpperL],NativeHumBossLook.Bind[NativeHumBossLook.ForeL])-NativeHumBossLook.UpperArm)<1e-3f,"the upper arm's length");
        Check(Math.Abs(Vector3.Distance(NativeHumBossLook.Bind[NativeHumBossLook.ThighL],NativeHumBossLook.Bind[NativeHumBossLook.ShinL])-NativeHumBossLook.Thigh)<1e-3f,"the thigh's length");
        var lens=Regex.Matches(py,"\\('(?:clav-.|chest|spine1|pelvis)', [-.\\d]+, [.\\d]+, [-.\\d]+, ([.\\d]+), [.\\d]+\\)");Check(lens.Count==NativeHumBossLook.Strips,"the strips' table");
        for(int s=0;s<lens.Count;s++)Check(Math.Abs(float.Parse(lens[s].Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)-NativeHumBossLook.StripLength[s])<1e-3f,"strip "+s+"'s length");
    }
    // ---- the room: partitions never seal a pocket, in any mix of their places ----
    private static void Room(out string summary)
    {
        var l=new BackroomsLayout("t",1,16,16);var room=NativeHumArena.Carve(l,8,8);l.EntryX=room.X0+6;l.EntryZ=room.Z0+1;
        Check(room.X0==2&&room.Z0==2&&Math.Abs(room.MaxX-room.MinX-38.4f)<1e-3f,"12 x 12 cells, 38.4 m");int lit=0;for(int z=room.Z0;z<room.Z0+12;z++)for(int x=room.X0;x<room.X0+12;x++){if(l.Light[l.Index(x,z)])lit++;Check(l.Pillar[l.Index(x,z)]==BackroomsLayout.NoPillar,"no pillars");}Check(lit==NativeHumArena.FixtureCount,"fixtures every other cell");
        for(int k=0;k<12;k++){Check(l.Between(room.X0,room.Z0+k,-1,0)==BackroomsLayout.Wall&&l.Between(room.X0+11,room.Z0+k,1,0)==BackroomsLayout.Wall&&l.Between(room.X0+k,room.Z0,0,-1)==BackroomsLayout.Wall&&l.Between(room.X0+k,room.Z0+11,0,1)==BackroomsLayout.Wall,"the door's papered over: a sealed room");}
        var c=new int[NativeHumArena.Circuits];for(int i=0;i<NativeHumArena.FixtureCount;i++)c[NativeHumArena.CircuitOf(i)]++;Check(c.All(n=>n>0),"eight circuits, each with fixtures");
        int masks=0;var seen=new bool[l.Width*l.Depth];var stack=new int[l.Width*l.Depth];
        for(int mask=0;mask<256;mask++)
        {
            var t=new BackroomsLayout("t",1,16,16);var r=NativeHumArena.Carve(t,8,8);NativeHumArena.WallPartitions(t,r,mask);
            // Flood from one cell in the room, stopping at its walls.
            Array.Clear(seen,0,seen.Length);int top=0;int start=t.Index(r.X0+6,r.Z0+6);seen[start]=true;stack[top++]=start;int reached=0;
            while(top>0){int cc=stack[--top];reached++;int x=cc%t.Width,z=cc/t.Width;foreach(var (dx,dz) in new[]{(1,0),(-1,0),(0,1),(0,-1)}){int nx=x+dx,nz=z+dz;if(nx<r.X0||nz<r.Z0||nx>=r.X0+12||nz>=r.Z0+12)continue;if(t.Between(x,z,dx,dz)==BackroomsLayout.Wall)continue;int n=t.Index(nx,nz);if(!seen[n]){seen[n]=true;stack[top++]=n;}}}
            Check(reached==144,"mask "+mask+" leaves a sealed pocket ("+reached+" of 144)");masks++;
        }
        // The eye's push never leaves the boss's side.
        summary=masks+" mixes of the partitions' places: no sealed pocket";
    }
    // ---- the intro's shots and beats ----
    private static void Intro()
    {
        var s=NativeHumBoss.Shots;Check(s[0].From==0&&Math.Abs(s[s.Length-1].To-NativeBackroomsBossIntro.Hold)<1e-3f,"the shots run the hold");for(int i=1;i<s.Length;i++)Check(s[i].From==s[i-1].To,"no gap between shots");
        Check(HumBossBeats.Fallback.Valid,"the beats are in order");Check(HumBossBeats.Fallback.Name==NativeBackroomsBossIntro.NameAt,"the name card at 6.4261");Check(Math.Abs(HumBossBeats.Fallback.Hold-9.0678f)<1e-3f,"the drop at 9.0678");
        // Every eye stays inside the room and under the ceiling.
        NativeHumArena.Spawn(NativeHumBossPreview.Room,out var root,out float yaw);for(float t=0;t<NativeBackroomsBossIntro.Hold;t+=.05f){Check(NativeBackroomsBossIntro.Sample(NativeHumBoss.Shots,t,root,yaw,out var e,out var l,out float fov),"a shot at "+t);Check(NativeHumBossPreview.Room.Inside(e.X,e.Z)&&e.Y>.2f&&e.Y<3.9f,"the eye is in the room at "+t+" ("+e+")");}
        // The beat table drives the lamps: the far row first, nearest last, everything out through the walk, and everything on at the drop.
        var st=new HumBossStage();var v=HumBossView.None;v.live=true;v.spawned=0;v.phase=1;var b=HumBossBeats.Fallback;var room=NativeHumBossPreview.Room;
        st.Evaluate(v,.1f,b,room,false);Check(st.Lamp[0]==1&&st.Lamp[35]==1,"lit at the start");st.Evaluate(v,b.RowOut0+.2f,b,room,false);Check(st.Lamp[5*6+1]==0&&st.Lamp[0]==1,"the far row out first");
        st.Evaluate(v,b.WalkOut+.5f,b,room,false);Check(st.Lamp.All(x=>x==0),"dark through the walk");st.Evaluate(v,b.Hold+.01f,b,room,false);Check(st.Lamp.All(x=>x>=1),"every lamp on at the drop");
        st.Evaluate(v,b.Hold+.01f,b,room,true);Check(st.Lamp.All(x=>x>=1&&x<2),"Reduced effects: no flash past the steady");
        // The rig's lit tubes strike one at a time.
        var rig=new HumBossRig();var fx=new NativeHumBossFx();fx.Room=room;for(float t=0;t<b.Tube(3)+.5f;t+=1/60f){st.Evaluate(v,t,b,room,false);v.x=room.Centre.X;v.z=room.MaxZ-NativeHumArena.IntroWallBehind;v.yaw=180;rig.Update(t,1/60f,v,Ground,Vector3.Zero,false,true,Target);}
        fx.Build(rig,v,st,b.Tube(3)+.5f,20,2,20,1,0,0,0,1,0,1,false,false);Check(fx.Tube[0]>0&&fx.Tube[3]>0&&fx.Tube[6]==0,"the tubes strike up the spine in order");
    }
    private static void Cues(string root)
    {
        Check(NativeHumBossCues.Keys.Length==NativeHumBossCues.Briefs.Length,"every cue has its brief");foreach(var k in NativeHumBossCues.Keys)Check(k.StartsWith("boss.hum."),k);
        Check(NativeHumBossCues.Keys.Distinct().Count()==NativeHumBossCues.Keys.Length,"keys are unique");
        // Across the preview every key but a chosen few is reached, and each fires on its time.
        var rig=new HumBossRig();var cues=new HumBossCue[48];var seen=new bool[NativeHumBossCues.Keys.Length];var v=HumBossView.None;float dt=1/30f;var st=new HumBossStage();
        for(float t=dt;t<NativeHumBossPreview.Loop;t+=dt){NativeHumBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,dt,v,Ground,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));int n=NativeHumBossCues.Collect(v,rig,NativeHumBossPreview.Room,t-dt,t,cues);for(int i=0;i<n;i++)seen[cues[i].Key]=true;}
        for(int i=0;i<seen.Length;i++)if(i!=NativeHumBossCues.HeadBreak&&i!=NativeHumBossCues.Hurt)Check(seen[i],NativeHumBossCues.Keys[i]+" is queued in the preview");
    }
    private static void Lanes()
    {
        // Row Burst's lanes lie on the fixtures' rows; the axis of NOCLIP's second burst crosses the first's.
        var room=NativeHumBossPreview.Room;for(int r=0;r<6;r++){int f=r*6+2;Check(Math.Abs(NativeHumArena.RowLine(room,0,r)-room.FixtureAt[f].Z)<1e-3f,"a row's line is on its fixtures");f=2*6+r;Check(Math.Abs(NativeHumArena.RowLine(room,1,r)-room.FixtureAt[f].X)<1e-3f,"a column's line is on its fixtures");}
        Check(NativeHumArena.RowOf(35,0)==5&&NativeHumArena.RowOf(35,1)==5,"the far corner's rows");
        // The directed camera never leaves the room.
        for(float p=NativeHumBossPreview.IntroEnd;p<NativeHumBossPreview.Loop;p+=.25f){NativeHumBossPreview.Root(p,out float x,out float z,out float yaw);NativeHumBossPreview.Camera(p,new Vector3(x,3,z),new Vector3(x,0,z),yaw,out var e,out var l,out float fov);Check(room.Inside(e.X,e.Z)&&e.Y>0&&e.Y<4&&fov>20&&fov<90,"the preview's eye is in the room at "+p);}
    }
}
#endif
