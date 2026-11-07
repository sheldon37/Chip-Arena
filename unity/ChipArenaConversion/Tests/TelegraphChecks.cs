using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// Attack warnings (Core NativeTelegraph; docs/TELEGRAPHS.md): the shapes' distances and extents agree; the frame maps both ways;
// the fill runs 0..1 to the hit, the lock-on is the last moments, a warning goes after its flash or its live window; and NOCTIEL's
// warnings are honest: every point its rule would hit is inside the warning it draws, at every move, with the warning full on
// the hit. The shader and the renderer are wired.
public static class TelegraphChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Telegraphs: "+what);count++;}
    private static NativeTelegraph.Spec S(byte shape,float A,float B=0,float C=0,float yaw=0,Vector3 at=default)=>new NativeTelegraph.Spec{Shape=shape,A=A,B=B,C=C,Yaw=yaw,At=at,Strength=1};
    public static void Run(string root)
    {
        count=0;string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        Shapes();Timing();Noctiel();Hum();DeepEnd();Station();Converter();
        // The shader and the renderer.
        var shader=R("Assets/ChipArena/Resources/NativeTelegraph.shader");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeTelegraph.shader.meta"))&&shader.Contains("Shader \"ChipArena/NativeTelegraph\"")&&shader.Contains("Blend One OneMinusSrcAlpha")&&shader.Contains("float _TgClock;"),"the shader, premultiplied, on its clock");
        Check(shader.Split('\n').Where(l=>!l.TrimStart().StartsWith("//")).All(l=>!System.Text.RegularExpressions.Regex.IsMatch(l,@"\bhalf\b")),"no HLSL type name (half) used as a name");
        foreach(var (name,v) in new[]{("Fallen",NativeTelegraph.Fallen),("FallenKill",NativeTelegraph.FallenKill),("Safe",NativeTelegraph.Safe),("Industrial",NativeTelegraph.Industrial),("Molten",NativeTelegraph.Molten),("Fluorescent",NativeTelegraph.Fluorescent),("Water",NativeTelegraph.Water),("Siren",NativeTelegraph.Siren)})
            Check(shader.Contains("style=="+v),"the shader knows style "+name+" ("+v+")");
        Check(NativeTelegraph.Shapes==6&&shader.Contains("if(shape==4)return Box(p,float2(A,A));")&&shader.Contains("if(shape==3)return Box(p-float2(0,(A-C)*.5),float2(B,(A+C)*.5));"),"the shader's shapes are Core's");
        var draw=R("Assets/ChipArena/Runtime/NativeTelegraphPresentation.cs");var frame=R("Assets/ChipArena/Runtime/NativeAbilityPresentation.cs");var fallen=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");
        Check(frame.Contains("TelegraphsBegin();SourceWorld(arena);")&&frame.Contains("WorldEffects(arena);TelegraphsDraw(hazardClock);"),"cleared before the world, drawn after its effects");
        Check(draw.Contains("NativeTelegraph.Corners(s,")&&draw.Contains("mesh.SetUVs(3,tgK);")&&fallen.Contains("for(int i=0;i<fbFx.Telegraphs.Count;i++)Telegraph(fbFx.Telegraphs.Items[i],origin);"),"one quad a warning; NOCTIEL's on its stage");
        Console.WriteLine("PASS telegraphs ("+count+" checks: six shapes, the fill and the lock-on, every NOCTIEL, HUM, DEEP END, 6:47 and CONVERTER warning covers what its rule hits)");
    }
    private static void Shapes()
    {
        var rnd=new Random(7);
        var specs=new[]{S(NativeTelegraph.Circle,9,yaw:30),S(NativeTelegraph.Ring,10,6,yaw:-40),S(NativeTelegraph.Cone,8,75.5f,0,120),S(NativeTelegraph.Cone,28,150,2,10),S(NativeTelegraph.Lane,35,3,3,200,new Vector3(4,0,-7)),S(NativeTelegraph.Square,3.68f,yaw:0,at:new Vector3(12,0,4)),S(NativeTelegraph.Wave,40,1.2f,60,75)};
        foreach(var s in specs)
        {
            NativeTelegraph.Extent(s,out float x0,out float x1,out float y0,out float y1);
            var core=s.Shape==NativeTelegraph.Cone?new Vector2(0,(s.A+s.C)*.5f):s.Shape==NativeTelegraph.Ring?new Vector2(0,(s.A+s.B)*.5f):s.Shape==NativeTelegraph.Wave?new Vector2(0,s.A):s.Shape==NativeTelegraph.Lane?new Vector2(0,s.A*.5f):Vector2.Zero;
            Check(NativeTelegraph.Distance(s,core)<0,"shape "+s.Shape+": its heart inside");
            for(int i=0;i<4000;i++)
            {
                var p=new Vector2((float)(rnd.NextDouble()*2-1)*(s.A+s.B+s.C+6),(float)(rnd.NextDouble()*2-1)*(s.A+s.B+s.C+6));
                if(NativeTelegraph.Distance(s,p)<0)Check(p.X>=x0-1e-3f&&p.X<=x1+1e-3f&&p.Y>=y0-1e-3f&&p.Y<=y1+1e-3f,"shape "+s.Shape+": inside lies within its extent ("+p+")");
                var w=NativeTelegraph.World(s,p);var back=NativeTelegraph.Local(s,w);Check(Vector2.Distance(back,p)<1e-3f&&Math.Abs(w.Y-s.At.Y)<1e-5f,"shape "+s.Shape+": its frame maps both ways, on the floor");
            }
            // The quad covers the shape and its margin.
            NativeTelegraph.Corners(s,out var a,out var b,out var c,out var d,out var la,out var lb,out var lc,out var ld);
            Check(la.X<=x0-NativeTelegraph.Margin+1e-4f&&lc.X>=x1+NativeTelegraph.Margin-1e-4f&&la.Y<=y0-NativeTelegraph.Margin+1e-4f&&lc.Y>=y1+NativeTelegraph.Margin-1e-4f&&Vector3.Distance(a,NativeTelegraph.World(s,la))<1e-3f&&Vector3.Distance(c,NativeTelegraph.World(s,lc))<1e-3f,"shape "+s.Shape+": the quad covers it and its glow");
        }
        // Distances: the game's yaw (0 faces +z, 90 +x); a cone's front is ahead; a lane runs forward A and back C.
        var lane=S(NativeTelegraph.Lane,10,1,2,90);Check(NativeTelegraph.Distance(lane,NativeTelegraph.Local(lane,new Vector3(9,0,0)))<0&&NativeTelegraph.Distance(lane,NativeTelegraph.Local(lane,new Vector3(-1.5f,0,0)))<0&&NativeTelegraph.Distance(lane,NativeTelegraph.Local(lane,new Vector3(-2.5f,0,0)))>0&&NativeTelegraph.Distance(lane,NativeTelegraph.Local(lane,new Vector3(0,0,1.5f)))>0,"a lane at yaw 90 runs along +x");
        var cone=S(NativeTelegraph.Cone,8,45,0,0);Check(NativeTelegraph.Distance(cone,NativeTelegraph.Local(cone,new Vector3(0,0,5)))<0&&NativeTelegraph.Distance(cone,NativeTelegraph.Local(cone,new Vector3(0,0,-5)))>0&&NativeTelegraph.Distance(cone,NativeTelegraph.Local(cone,new Vector3(5,0,1)))>0,"a cone at yaw 0 faces +z");
    }
    private static void Timing()
    {
        float last=-1;for(float t=-.5f;t<=2.5f;t+=.01f){float p=NativeTelegraph.Progress(0,2,t);Check(p>=last-1e-6f&&p>=0&&p<=1,"the fill only grows");last=p;}
        Check(NativeTelegraph.Progress(0,2,0)==0&&Math.Abs(NativeTelegraph.Progress(0,2,2)-1)<1e-5f&&NativeTelegraph.LockOf(2,2-NativeTelegraph.LockSeconds-.01f)==0&&NativeTelegraph.LockOf(2,2)==1&&NativeTelegraph.LockOf(2,2.1f)==0,"full on the hit; the lock-on only in its last moments");
        var s=S(NativeTelegraph.Circle,3);Check(!NativeTelegraph.Timed(ref s,1,2,.9f)&&NativeTelegraph.Timed(ref s,1,2,1.5f)&&s.After<0&&NativeTelegraph.Timed(ref s,1,2,2+NativeTelegraph.FlashSeconds-.01f)&&s.After>0&&!NativeTelegraph.Timed(ref s,1,2,2+NativeTelegraph.FlashSeconds+.01f),"shown from its start through its flash");
        Check(NativeTelegraph.Timed(ref s,1,2,3.5f,2)&&s.Live>0&&!NativeTelegraph.Timed(ref s,1,2,4.01f,2),"a live hazard shows through its live window");
        var list=new NativeTelegraphList(2);Check(list.Add(s)&&list.Add(s)&&!list.Add(s)&&list.Count==2,"the list holds what it was sized for");
    }
    // ---- NOCTIEL: the warnings drawn against the authority's own hit tests (NativeFallenAuthority.FallenAttack) ----
    private static void Noctiel()
    {
        var fx=new NativeFallenBossFx();var f=new FallenBossFrame();var rnd=new Random(11);
        var rec=new NativeFallenRecord{pools=new[]{new V3(10,0,12),new V3(-30,0,4)},hush=Array.Empty<NativeFallenSeat>(),chipOffer=Array.Empty<string>()};
        var target=new Vector3(14,0,-20);var from=new Vector3(-6,0,8);
        foreach(var m in NativeFallenBoss.Moves)
        {
            float A=10,wind=m.Id=="thrones"?1.2f:Math.Max(m.Kills?2.4f:0,m.Wind),H=A+wind;float aim=(float)(Math.Atan2(target.X-from.X,target.Z-from.Z)*180/Math.PI);
            var to=m.Id=="vow"?from+Vector3.Normalize(target-from)*Math.Min(35,Vector3.Distance(target,from)):target;
            var act=new NativeFallenBossPreview.Act{Id=m.Id,At=A,Hit=H,End=H+3,Phase=m.Phase,Seed=4321,From=from,To=to,Aim=aim};
            f.Live=rec;f.Actions=new[]{act};f.Boss=from;f.Kind=NativeFallenBossPreview.KindFight;f.Phase=m.Phase;
            foreach(float t in new[]{A+.01f,A+wind*.5f,H-.05f})
            {
                f.P=t;fx.Build(f,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,null,false);var w=fx.Telegraphs;
                bool any=m.Id!="choir"&&m.Id!="penance"&&m.Id!="thrones";Check(!any||w.Count>0,m.Id+": a warning through its windup");
                for(int i=0;i<w.Count;i++)Check(w.Items[i].Fill01>=0&&w.Items[i].Fill01<=1&&w.Items[i].After<0,m.Id+": filling, before the hit");
                if(!any||t<H-.1f)continue;
                // Every point the rule hits is inside a warning (the warning may be a little larger at a lane's ends, never smaller).
                int hits=0;
                for(int i=0;i<6000;i++)
                {
                    var q=new Vector2((float)(rnd.NextDouble()*2-1)*90,(float)(rnd.NextDouble()*2-1)*90);if(q.Length()>80)continue;
                    bool hit=RuleHits(m.Id,q,act,rec,out bool sure);if(!hit||!sure)continue;hits++;
                    bool warned=false;for(int k=0;k<w.Count&&!warned;k++)warned=NativeTelegraph.Distance(w.Items[k],NativeTelegraph.Local(w.Items[k],new Vector3(q.X,0,q.Y)))<=.05f;
                    Check(warned,m.Id+": the rule hits "+q+" and the warning shows it");
                }
                if(m.Id!="eclipse"&&m.Id!="severance")Check(hits>0,m.Id+": the samples found its area");
            }
            // Full on the hit; gone after its flash (or its live window).
            f.P=H-.001f;fx.Build(f,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,null,false);
            for(int i=0;i<fx.Telegraphs.Count;i++)if(fx.Telegraphs.Items[i].Shape!=NativeTelegraph.Wave)Check(fx.Telegraphs.Items[i].Fill01>.99f&&fx.Telegraphs.Items[i].Lock>.99f,m.Id+": full and locked on the hit");
            f.P=H+6;fx.Build(f,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,null,false);Check(fx.Telegraphs.Count==0,m.Id+": gone after the move");
        }
        // Severance's waves: the front as it travels is what hurts (34 m/s from 3 m, 1.2 m either side, a 0.5 dot).
        {
            var act=new NativeFallenBossPreview.Act{Id="severance",At=0,Hit=.9f,End=6,Seed=1,From=Vector3.Zero,To=new Vector3(0,0,30),Aim=0};f.Actions=new[]{act};f.Boss=Vector3.Zero;
            f.P=.9f+1;fx.Build(f,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,null,false);int waves=0;for(int i=0;i<fx.Telegraphs.Count;i++)if(fx.Telegraphs.Items[i].Shape==NativeTelegraph.Wave)waves++;Check(waves==1,"one wave out after a second ("+waves+")");
            for(int i=0;i<3000;i++)
            {
                var q=new Vector2((float)(rnd.NextDouble()*2-1)*60,(float)(rnd.NextDouble()*2-1)*60);float r=q.Length();
                bool hit=Math.Abs(r-(34*1f+3))<=1.2f&&r>.01f&&Vector2.Dot(Vector2.Normalize(q),NativeFallenRules.Dir(0))>=.5f;if(!hit)continue;
                bool warned=false;for(int k=0;k<fx.Telegraphs.Count&&!warned;k++)warned=NativeTelegraph.Distance(fx.Telegraphs.Items[k],NativeTelegraph.Local(fx.Telegraphs.Items[k],new Vector3(q.X,0,q.Y)))<=.05f;
                Check(warned,"the wave's front shows where it hurts "+q);
            }
        }
        // The Hush: a pilot standing still sees the water fill under them before it opens.
        {
            var seat=new NativeFallenSeat{id=3,x=20,z=-10,hushAt=0};var r2=new NativeFallenRecord{hush=new[]{seat},pools=Array.Empty<V3>(),chipOffer=Array.Empty<string>()};f.Live=r2;f.Actions=Array.Empty<NativeFallenBossPreview.Act>();f.Phase=1;
            float secs=NativeFallenRules.HushSeconds(r2,1,(float)Math.Sqrt(20*20+10*10));f.P=secs*.2f;fx.Build(f,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,null,false);Check(fx.Telegraphs.Count==0,"the Hush quiet at first");
            f.P=secs*.8f;fx.Build(f,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,null,false);Check(fx.Telegraphs.Count==1&&fx.Telegraphs.Items[0].A==3&&Vector3.Distance(fx.Telegraphs.Items[0].At,new Vector3(20,0,-10))<1e-4f&&fx.Telegraphs.Items[0].Fill01>.5f,"the Hush fills a 3 m circle under the pilot");
        }
    }
    // THE HUM: every point NativeBackroomsBossRules.Contacts would hit (its own Arc, Line and distances) is inside a warning drawn
    // at that moment, through each move's live window; each warning fills to its hit; a called-off move's warning goes.
    private static void Hum()
    {
        var room=NativeHumBossPreview.Room;var fx=new NativeHumBossFx{Room=room};var rig=new HumBossRig();var st=new HumBossStage();var ground=new BossFlatGround(room.Origin.Y);var rnd=new Random(17);
        var centre=new Vector3(room.Centre.X,0,room.Centre.Z);float floor=room.Origin.Y;
        HumBossView V(string id,float at,float hit,int phase=1){var v=HumBossView.None;v.live=true;v.spawned=-200;v.maxHp=v.hp=8000;v.phase=phase;v.x=centre.X+2;v.z=centre.Z-3;v.yaw=35;v.action=id;v.actionAt=at;v.hitAt=hit;v.duration=6;
            v.laneX0=centre.X+2;v.laneZ0=centre.Z-3;v.actionYaw=50;v.cancelAt=-100;return v;}
        void Frame(in HumBossView v,float t){rig.Update(t,1/30f,v,ground,Vector3.Zero,false,false,default);st.Evaluate(v,t,HumBossBeats.Fallback,room,false);fx.Build(rig,v,st,t,centre.X,1.7,centre.Z-15,1,0,0,0,1,0,1,false,false);}
        bool Warned(Vector3 p){var w=fx.Telegraphs;for(int k=0;k<w.Count;k++)if(NativeTelegraph.Distance(w.Items[k],NativeTelegraph.Local(w.Items[k],new Vector3(p.X,floor,p.Z)))<=.05f)return true;return false;}
        Vector3 Sample(){return new Vector3(room.MinX+(float)rnd.NextDouble()*(room.MaxX-room.MinX),0,room.MinZ+(float)rnd.NextDouble()*(room.MaxZ-room.MinZ));}
        void Move(string id,HumBossView v,float live,Func<Vector3,float,bool> rule,int phase=1)
        {
            float A=v.actionAt,H=v.hitAt;rig=new HumBossRig();
            for(float t=A-.2f;t<A;t+=1/30f)Frame(V(null,-100,-100),t);
            float shownFrom=id=="cut"?H-.25f:A;
            foreach(float t in new[]{shownFrom+.02f,(shownFrom+H)*.5f,H-.03f}){Frame(v,t);Check(fx.Telegraphs.Count>0,id+": a warning in the windup at "+(t-A).ToString("0.00"));for(int i=0;i<fx.Telegraphs.Count;i++)Check(fx.Telegraphs.Items[i].Style==NativeTelegraph.Fluorescent,id+": THE HUM's look");}
            Frame(v,H-.001f);for(int i=0;i<fx.Telegraphs.Count;i++)if(fx.Telegraphs.Items[i].Shape!=NativeTelegraph.Wave)Check(fx.Telegraphs.Items[i].Fill01>.99f,id+": full on the hit");
            int hits=0;
            for(float dt=0;dt<=live;dt+=Math.Max(.03f,live/12))
            {
                float t=H+dt;Frame(v,t);
                for(int i=0;i<1500;i++){var p=Sample();if(!rule(p,dt))continue;hits++;Check(Warned(p),id+": the rule hits "+p+" "+dt.ToString("0.00")+" s in and a warning shows it");}
            }
            Check(hits>0,id+": the samples found its area");
            Frame(v,H+live+3);Check(fx.Telegraphs.Count==0||id=="row","");
        }
        var o=new Vector3(centre.X+2,0,centre.Z-3);
        var reach=NativeHumBoss.Move("reach");Move("reach",V("reach",10,10+reach.Wind),reach.Strike,(p,dt)=>NativeBackroomsBossRules.Arc(p,o,50,6,130));
        var stoop=NativeHumBoss.Move("stoop");Move("stoop",V("stoop",10,10+stoop.Wind),stoop.Strike,(p,dt)=>Vector3.Distance(new Vector3(p.X,0,p.Z),o)<=4.5f);
        var cut=NativeHumBoss.Move("cut");{var v=V("cut",10,10+cut.Wind,2);Move("cut",v,cut.Strike,(p,dt)=>NativeBackroomsBossRules.Arc(p,new Vector3(v.x,0,v.z),v.yaw,3.5f,180),2);}
        var hand=NativeHumBoss.Move("hand");{var v=V("hand",10,10+hand.Wind,2);v.handX=room.MaxX-.4f;v.handZ=centre.Z+4;v.handY=1.5f;v.handNx=-1;v.handNz=0;v.handAt=10;v.handUntil=v.hitAt+1.5f;var c=new Vector3(v.handX,0,v.handZ);Move("hand",v,1.5f,(p,dt)=>Vector3.Distance(new Vector3(p.X,0,p.Z),c)<=2.5f,2);}
        var tear=NativeHumBoss.Move("tear");{var v=V("tear",10,10+tear.Wind);v.tearX=centre.X-6;v.tearZ=centre.Z+5;v.tearAt=v.hitAt;var c=new Vector3(v.tearX,0,v.tearZ);Move("tear",v,.2f,(p,dt)=>Vector3.Distance(new Vector3(p.X,0,p.Z),c)<=2.4f);}
        var pulse=NativeHumBoss.Move("pulse");{var v=V("pulse",10,10+pulse.Wind,2);v.ringX=o.X;v.ringZ=o.Z;v.ringAt=v.hitAt;Move("pulse",v,2.6f,(p,dt)=>Math.Abs(Vector3.Distance(new Vector3(p.X,0,p.Z),o)-dt*14)<=.9f,2);}
        var row=NativeHumBoss.Move("row");
        foreach(int axis in new[]{0,1}){var v=V("row",10,10+row.Wind);v.rowAxis=axis;v.rowMask=21;v.rowAt=v.hitAt;
            Move("row",v,.1f,(p,dt)=>{for(int r=0;r<6;r++)if((21&(1<<r))!=0&&Math.Abs((axis==0?p.Z:p.X)-NativeHumArena.RowLine(room,axis,r))<=.7f)return true;return false;});}
        var lunge=NativeHumBoss.Move("lunge");
        foreach(int phase in new[]{1,3})
        {
            var v=V("lunge",10,10+lunge.Wind,phase);var a=o;var b=o+new Vector3(14,0,9);v.laneX1=b.X;v.laneZ1=b.Z;
            Move("lunge",v,phase==3?3.6f:1.2f,(p,dt)=>{bool second=phase==3&&dt>=2.4f;float age=second?dt-2.4f:dt;if(age<0||age>1.2f)return false;var s0=second?b:a;var s1=second?a:b;
                var q=Vector3.Lerp(s0,s1,Math.Clamp(age/1.2f,0,1));var prev=Vector3.Lerp(s0,s1,Math.Clamp((age-1/30f)/1.2f,0,1));return NativeBackroomsBossRules.Line(p,prev,q,3);},phase);
        }
        // Called off (a stagger before the hit): no warning after it.
        {var v=V("reach",10,10+reach.Wind);v.cancelAt=10.3f;Frame(v,10.4f);Check(fx.Telegraphs.Count==0,"reach: called off, its warning goes");}
        // The DEV preview's scripted fight draws its warnings at real places (its moves' origins and aims filled as the authority's).
        int shown=0;var pv=HumBossView.None;
        for(float t=0;t<NativeHumBossPreview.DeathAt;t+=.25f){NativeHumBossPreview.View(t,0,ref pv,out _,out _);rig.Update(t,.25f,pv,ground,Vector3.Zero,false,false,default);st.Evaluate(pv,t,HumBossBeats.Fallback,room,false);fx.Build(rig,pv,st,t,centre.X,1.7,centre.Z-15,1,0,0,0,1,0,1,false,false);
            for(int i=0;i<fx.Telegraphs.Count;i++){var w=fx.Telegraphs.Items[i];Check(float.IsFinite(w.At.X)&&float.IsFinite(w.At.Z)&&float.IsFinite(w.Yaw)&&float.IsFinite(w.A),"the preview's warnings are at real places ("+t+")");shown++;}}
        Check(shown>50,"the preview shows its warnings ("+shown+")");
    }
    // THE DEEP END, through its whole scripted fight (the DEV preview: every move, the real rig on its route): wherever the rules
    // (NativeBackroomsBossRules.Contacts) would hit, in pool coordinates, a warning drawn at that moment covers it; each move with a
    // hit shows its warning through its anticipation.
    private static void DeepEnd()
    {
        var o=new Vector3(24,0,24);var rig=new DeepEndBossRig();var r=NativeDeepEndBossPreview.Route;var fx=new NativeDeepEndBossFx();var waves=new DeepEndWaves();waves.Place();var v=DeepEndBossView.None;var rnd=new Random(23);
        float dt=1/30f;var seen=new System.Collections.Generic.Dictionary<string,int>();var windup=new System.Collections.Generic.HashSet<string>();
        float half=NativeDeepEndArena.Half;
        bool Warned(Vector3 local){var w=fx.Telegraphs;var p=o+local;for(int k=0;k<w.Count;k++)if(NativeTelegraph.Distance(w.Items[k],NativeTelegraph.Local(w.Items[k],p))<=.05f)return true;return false;}
        int frames=(int)(NativeDeepEndBossPreview.DeathAt*30);
        for(int n=0;n<frames;n++)
        {
            float t=n*dt;NativeDeepEndBossPreview.View(t,ref v,out _);rig.Update(t,dt,v,r,o,PoolroomsLayout.WaterY,false,default);
            if(!v.Moving(t,out var m,out float age,out float hit))continue;bool sampled=n%2==0;
            var st=NativeDeepEndBossStage.Evaluate(v,t,DeepEndBossBeats.Fallback);waves.Compose(st);fx.Build(rig,v,st,waves,t,o+new Vector3(0,6,-30),Vector3.UnitX,Vector3.UnitY,1,false,false);
            for(int i=0;i<fx.Telegraphs.Count;i++)Check(fx.Telegraphs.Items[i].Style==NativeTelegraph.Water&&float.IsFinite(fx.Telegraphs.Items[i].At.X),m.Id+": the water look, at a real place");
            if(m.Id!="whistle"&&age<hit-.05f&&age>.05f){Check(fx.Telegraphs.Count>0||m.Id=="lap",m.Id+": a warning through its anticipation");windup.Add(m.Id);}
            float tr=age-hit;if(tr<0||!sampled)continue;
            var tp=new Vector3(v.targetX,0,v.targetZ);var org=new Vector3(v.x,0,v.z);var aim=new Vector3(v.aimX,0,v.aimZ);float water=o.Y+st.Tide;
            for(int i=0;i<500;i++)
            {
                var q=new Vector3((float)(rnd.NextDouble()*2-1)*half,0,(float)(rnd.NextDouble()*2-1)*half);bool hitHere=false;
                switch(m.Id)
                {
                    case "breach":hitHere=tr<=.25f&&Vector3.Distance(q,tp)<=3.2f;break;
                    case "flop":hitHere=tr<=.2f&&NativeBackroomsBossRules.Line(q,tp,aim,4);break;
                    case "thrash":hitHere=tr<=.3f&&Vector3.Distance(q,org)<=5;break;
                    case "lull":hitHere=tr<=1.8f&&Vector3.Distance(q,tp)<=1.8f;break;
                    case "drain":hitHere=tr<=4&&q.Length()<=2.6f;break;
                    case "seiche":{if(tr>7||aim.LengthSquared()<1e-4f)break;var d=Vector3.Normalize(aim);float travel=tr*11.4f;bool back=travel>40;float wave=back?60-travel:travel-20;hitHere=Math.Abs(Vector3.Dot(q,d)-wave)<=1.3f;break;}
                    case "jet":{if(tr>2.2f)break;var head=rig.MouthAt;var d=DeepEndBossRig.JetDir(v,age,hit,m,head-o,st.Tide);var hl=head-o;hitHere=NativeBackroomsBossRules.Line(q,hl,hl+d*30,2);break;}
                    case "lap":{if(tr>6)break;var feet=new Vector3(q.X,water-o.Y,q.Z);for(int j=1;j<NativeDeepEndBossLook.Spine&&!hitHere;j++){var a=rig.Shown.Position[j-1]-o;var b=rig.Shown.Position[j]-o;if(Math.Max(a.Y,b.Y)+NativeDeepEndBossLook.Radius(j*NativeDeepEndBossLook.Seg)>=feet.Y&&NativeBackroomsBossRules.Line(feet,a,b,3.2f))hitHere=true;}break;}
                }
                if(!hitHere)continue;seen[m.Id]=seen.TryGetValue(m.Id,out int c)?c+1:1;
                Check(Warned(q),m.Id+": the rule hits "+q+" "+tr.ToString("0.00")+" s in and a warning shows it");
            }
        }
        foreach(var id in new[]{"breach","flop","thrash","lull","drain","seiche","jet","lap"})Check(seen.ContainsKey(id),id+": the preview's samples found its area");
        foreach(var id in new[]{"breach","flop","thrash","lull","drain","seiche","jet"})Check(windup.Contains(id),id+": warned through its anticipation");
    }
    // THE 6:47 (the Station), through its scripted fight: wherever Runtime NativeZoneBossAuthority's station moves (and the zone's
    // glass) would hit, a warning drawn at that moment covers it; each move with a hit is warned through its windup.
    private static void Station()
    {
        var rig=new StationBossRig();var fx=new NativeStationBossFx();var v=StationBossView.None;var rnd=new Random(29);float dt=1/30f;
        var seen=new System.Collections.Generic.HashSet<string>();var windup=new System.Collections.Generic.HashSet<string>();
        bool Warned(Vector3 p){var w=fx.Telegraphs;for(int k=0;k<w.Count;k++)if(NativeTelegraph.Distance(w.Items[k],NativeTelegraph.Local(w.Items[k],p))<=.05f)return true;return false;}
        int frames=(int)(NativeStationBossPreview.Loop*30);
        for(int n=0;n<frames;n++)
        {
            float t=n*dt;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,dt,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
            fx.GlassCount=NativeStationBossPreview.Glass(v,fx.GlassXZ);fx.DrawGlass=true;fx.Build(rig,v,t,4,3,14,1,0,0,0,1,0,1,false);
            for(int i=0;i<fx.Telegraphs.Count;i++)Check(fx.Telegraphs.Items[i].Style==NativeTelegraph.Industrial&&float.IsFinite(fx.Telegraphs.Items[i].At.X),"the 6:47: the industrial look, at a real place");
            if(v.action==null||!StationBossView.At(v.actionAt)||!StationBossView.At(v.hitAt))continue;if(StationBossView.At(v.cancelAt)&&v.cancelAt>=v.actionAt&&t>=v.cancelAt)continue;
            string id=v.action;float tr=t-v.hitAt;
            if(tr<-.05f&&t>v.actionAt+.05f&&NativeStationBoss.Move(id).Damage>0&&(id!="alteration"||fx.GlassCount>0)){Check(fx.Telegraphs.Count>0,id+": a warning through its windup");windup.Add(id);}
            if(tr<0||n%2!=0)continue;
            var boss=new Vector3(v.x,0,v.z);var c=StationBossView.At(v.laneX0)?new Vector3(v.laneX0,0,v.laneZ0):boss;var e=new Vector3(v.laneX1,0,v.laneZ1);
            for(int i=0;i<600;i++)
            {
                var p=new Vector3((float)(rnd.NextDouble()*2-1)*27,0,(float)(rnd.NextDouble()*2-1)*13);bool hit=false;
                switch(id)
                {
                    case "departure":{var m=NativeStationBoss.Move("departure");int leg=NativeZoneBossRules.DepartureLeg(v.phase,tr,m.Strike);if(leg<0||!StationBossView.At(v.laneX1))break;float ct=leg==1?tr-2:tr;var a=leg==1?e:c;var b=leg==1?c:e;var at=Vector3.Lerp(a,b,Math.Clamp(ct/m.Strike,0,1));hit=Vector3.Distance(p,at)<2.1f;break;}
                    case "doors":{var m=NativeStationBoss.Move("doors");if(tr>m.Strike)break;int side=v.holdSide<0?-1:1;var d=p-boss;float lx=Vector3.Dot(d,BossMath.Right(v.yaw*BossMath.Deg)),lz=Vector3.Dot(d,BossMath.Forward(v.yaw*BossMath.Deg));hit=Math.Abs(lx-side*3)<1.6f&&Math.Abs(lz+.35f)<1.3f;break;}
                    case "buffer":case "shunt":{var m=NativeStationBoss.Move(id);if(tr>m.Strike)break;var d=p-c;float reach=id=="buffer"?4.4f:5,arc=id=="buffer"?60:35;var lp=new Vector3(Vector3.Dot(d,BossMath.Right(v.actionYaw*BossMath.Deg)),0,Vector3.Dot(d,BossMath.Forward(v.actionYaw*BossMath.Deg)));hit=lp.Length()<=reach&&Math.Abs(Math.Atan2(lp.X,lp.Z)*180/Math.PI)<=arc;break;}
                    case "alteration":{if(tr>.1f)break;for(int g=0;g<fx.GlassCount&&!hit;g++)hit=Vector3.Distance(p,new Vector3(fx.GlassXZ[g*2],0,fx.GlassXZ[g*2+1]))<1.4f;break;}
                    case "live-rail":{if(!v.Live(t)||v.PantoBroken(t))break;for(int q=0;q<NativeStationBossArena.PuddleCount&&!hit;q++)hit=Vector3.Distance(p,new Vector3(NativeStationBossArena.Puddles[q*3],0,NativeStationBossArena.Puddles[q*3+1]))<2.2f;break;}
                    case "terminus":{float d=Vector3.Distance(p,c);hit=(tr<.15f&&d<3)||(tr<10/12f&&Math.Abs(d-Math.Min(10,tr*12))<.65f);break;}
                }
                if(!hit)continue;seen.Add(id);Check(Warned(p),id+": the rule hits "+p+" "+tr.ToString("0.00")+" s in and a warning shows it");
            }
        }
        foreach(var id in new[]{"buffer","shunt","departure","doors","alteration","live-rail","terminus"}){Check(seen.Contains(id),id+": the preview's samples found its area");Check(windup.Contains(id),id+": warned through its windup");}
    }
    // THE CONVERTER (the Ironworks): its moves through its scripted fight, and the authority's hazard pools through a scenario like
    // the realism checks': wherever the zone boss authority's converter rules would hit (or a pool burn), a warning covers it then.
    private static void Converter()
    {
        var rnd=new Random(31);float dt=1/30f;var seen=new System.Collections.Generic.HashSet<string>();var windup=new System.Collections.Generic.HashSet<string>();
        NativeIronworksBossFx fx=null;bool Warned(Vector3 p){var w=fx.Warnings;for(int k=0;k<w.Count;k++)if(NativeTelegraph.Distance(w.Items[k],NativeTelegraph.Local(w.Items[k],new Vector3(p.X,w.Items[k].At.Y,p.Z)))<=.05f)return true;return false;}
        {
            var rig=new IronworksBossRig();fx=new NativeIronworksBossFx();var v=IronworksBossView.None;int frames=(int)(NativeIronworksBossPreview.Loop*30);
            for(int n=0;n<frames;n++)
            {
                float t=n*dt;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);rig.Update(t,dt,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);
                fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);
                for(int i=0;i<fx.Warnings.Count;i++)Check(fx.Warnings.Items[i].Style==NativeTelegraph.Molten&&float.IsFinite(fx.Warnings.Items[i].At.X),"the Converter: the molten look, at a real place");
                if(v.action==null||!IronworksBossView.At(v.hitAt))continue;string id=v.action;float tr=t-v.hitAt;var act=NativeIronworksBoss.Find(id);
                bool damaging=id=="pour"||id=="slam"||id=="charge"||((id=="stomp"||id=="grab")&&IronworksBossView.At(v.targetX));
                if(damaging&&tr<-.05f&&t>v.actionAt+.05f){Check(fx.Warnings.Count>0,id+": a warning through its windup");windup.Add(id);}
                if(!damaging||tr<0||n%2!=0)continue;
                var o=IronworksBossView.At(v.laneX0)?new Vector3(v.laneX0,0,v.laneZ0):new Vector3(v.x,0,v.z);float aim=(IronworksBossView.At(v.actionYaw)?v.actionYaw:v.yaw)*BossMath.Deg;var fwd=BossMath.Forward(aim);var right=BossMath.Right(aim);var target=new Vector3(v.targetX,0,v.targetZ);
                for(int i=0;i<600;i++)
                {
                    var p=o+new Vector3((float)(rnd.NextDouble()*2-1)*16,0,(float)(rnd.NextDouble()*2-1)*16);var d=p-o;float lx=Vector3.Dot(d,right),lz=Vector3.Dot(d,fwd),dist=d.Length();bool hit=false;
                    switch(id)
                    {
                        case "pour":hit=tr<=act.Strike&&dist>=1.2f&&dist<=8&&Math.Abs(Math.Atan2(lx,lz)*180/Math.PI)<=26;break;
                        case "stomp":hit=tr<=act.Strike&&Vector3.Distance(p,target)<=1.9f;break;
                        case "grab":hit=tr<=act.Strike&&Vector3.Distance(p,target)<=1.2f;break;
                        case "slam":hit=(tr<.6f&&Math.Abs(dist-Math.Min(9,tr*15))<.65f)||(tr>=.6f&&tr<1.2f&&lz>0&&lz<6&&Math.Abs(lx)<3);break;
                        case "charge":{if(tr>act.Strike)break;var at=o+fwd*(14*Math.Clamp(tr/act.Strike,0,1));hit=Vector3.Distance(p,at)<3.2f;break;}
                    }
                    if(!hit)continue;seen.Add(id);Check(Warned(p),id+": the rule hits "+p+" "+tr.ToString("0.00")+" s in and a warning shows it");
                }
            }
            foreach(var id in new[]{"pour","slam","charge"}){Check(seen.Contains(id),id+": the preview's samples found its area");Check(windup.Contains(id),id+": warned through its windup");}
        }
        // The authority's pools: each burns from its hit to its end within its radius.
        {
            var rig=new IronworksBossRig();fx=new NativeIronworksBossFx();var v=IronworksBossView.None;v.live=true;v.x=-35;v.z=0;v.yaw=90;v.phase=1;v.maxHp=v.hp=6000;v.spawned=-100;v.action="pour";v.actionAt=1;v.hitAt=2.2f;v.laneX0=-35;v.laneZ0=0;v.actionYaw=90;v.targetX=-29;v.targetZ=0;
            var hz=new[]{new NativeZoneBossHazardFrame{id=1,kind="pour",x=-29,y=0,z=0,radius=3,hitAt=3.8f,until=7.8f},new NativeZoneBossHazardFrame{id=2,kind="blow",x=-31,y=0,z=4,radius=1,hitAt=3.0f,until=4.0f},
                new NativeZoneBossHazardFrame{id=6,kind="deslag",x=-43,y=0,z=-2.5f,radius=1.4f,hitAt=5.0f,until=11.0f},new NativeZoneBossHazardFrame{id=9,kind="charge",x=-36,y=0,z=5,radius=1.8f,hitAt=1.0f,until=4.0f}};
            v.hazards=hz;int pools=0;
            for(int n=0;n<=12*30;n++)
            {
                float t=n*dt;rig.Update(t,dt,v,IronworksBossGround.Hall,Vector3.Zero,false,true,new Vector3(-29,1.6f,0),double.NaN,double.NaN);fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);
                foreach(var h in hz)
                {
                    if(t>h.hitAt-.3f&&t<h.hitAt)Check(fx.Warnings.Count>0,h.kind+" pool: warned before it lands");
                    if(t<h.hitAt||t>h.until-.05f)continue;
                    for(int i=0;i<60;i++){var p=new Vector3(h.x+(float)(rnd.NextDouble()*2-1)*h.radius,0,h.z+(float)(rnd.NextDouble()*2-1)*h.radius);if(Vector2.Distance(new Vector2(p.X,p.Z),new Vector2(h.x,h.z))>h.radius)continue;pools++;Check(Warned(p),h.kind+" pool: it burns at "+p+" ("+(t-h.hitAt).ToString("0.00")+" s in) and a warning shows it");}
                }
            }
            Check(pools>500,"the pools were sampled ("+pools+")");
        }
    }
    // The authority's hit tests (NativeFallenAuthority.FallenAttack), on the floor. sure: the point is clearly in (not on an edge).
    private static bool RuleHits(string id,Vector2 q,NativeFallenBossPreview.Act a,NativeFallenRecord f,out bool sure)
    {
        var from=new Vector2(a.From.X,a.From.Z);var to=new Vector2(a.To.X,a.To.Z);var d=q-from;float r=d.Length();sure=true;
        switch(id)
        {
            case "cleave":sure=Math.Abs(r-8)>.1f;return r<=8&&(r<.01f||Vector2.Dot(Vector2.Normalize(d),NativeFallenRules.Dir(a.Aim))>=.25f+.01f);
            case "vow":{var lane=to-from;float t=Math.Clamp(Vector2.Dot(q-from,lane)/lane.LengthSquared(),0,1);float dist=Vector2.Distance(q,from+lane*t);sure=Math.Abs(dist-3)>.1f;return dist<=3;}
            case "descent":sure=Math.Abs(Vector2.Distance(q,to)-9)>.1f;return Vector2.Distance(q,to)<=9;
            case "sweep":sure=Math.Abs(r-28)>.1f;return r<=28;
            case "verdict":sure=Math.Abs(NativeFallenRules.Line(q,to,a.Aim)-1.5f)>.05f;return NativeFallenRules.Line(q,to,a.Aim)<=1.5f;
            case "last":{float best=new[]{a.Aim,a.Aim+60,a.Aim-60}.Min(y=>NativeFallenRules.Line(q,to,y));sure=Math.Abs(best-1.5f)>.05f;return best<=1.5f;}
            case "eclipse":{bool safe=NativeFallenRules.SafePool(q,f.pools);sure=f.pools.All(v=>Math.Abs(Vector2.Distance(q,new Vector2(v.x,v.z))-6)>.1f);return safe;}
            case "fall":return NativeFallenRules.InColumn(q,a.Seed);
            case "rain":
                for(int lane=0;lane<8;lane++){float angle=a.Seed*37+lane*45+(lane%2)*19;var dd=NativeFallenRules.Dir(angle);var c=from*.5f+new Vector2(dd.Y,-dd.X)*(lane-3.5f)*7;float l=NativeFallenRules.Line(q,c,angle);if(l<=1.25f){sure=Math.Abs(l-1.25f)>.05f;return true;}}
                return false;
            case "severance":{float x=Math.Abs(r-3);sure=false;return false;}
            default:sure=false;return false;
        }
    }
}
