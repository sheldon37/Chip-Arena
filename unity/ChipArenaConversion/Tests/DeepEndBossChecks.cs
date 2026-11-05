using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// THE DEEP END (docs/EXPEDITION-BOSS-BACKROOMS.md section 4), in the style of StationBossPresentationChecks: the moves table against the doc, the hits on hitAt, determinism, allocation-free frames, the preview off by default, None draws nothing, the room carved and sealed, the water covering every cell.
public static class DeepEndBossChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("deepend-boss: "+what);count++;}
    private static bool Near(float a,float b,float e=1e-3f)=>Math.Abs(a-b)<=e;
    public static void Run(string root)
    {
        count=0;
        Check(NativeDeepEndBoss.Archetype=="poolrooms-deep-end","the archetype Astra binds");
        Check(NativeBackroomsBossIntro.Mine(NativeDeepEndBoss.Archetype)&&NativeBackroomsBossIntro.Card(NativeDeepEndBoss.Archetype)==NativeDeepEndBoss.Card,"the intro knows the boss");
        Moves(root);Look(root);Off();Routes();Rig();Frames();Arena();Water();Stage();Intro();Cues(root);Preview();
        var acts=new System.Text.StringBuilder("deepend preview script:");for(int i=0;i<NativeDeepEndBossPreview.ActCount;i++){var a=NativeDeepEndBossPreview.ActAt(i);acts.Append($" {a.Id}@{a.At:F1}");}acts.Append($" lamps@{NativeDeepEndBossPreview.LampsAt:F1} p2@{NativeDeepEndBossPreview.Phase2At:F1} p3@{NativeDeepEndBossPreview.Phase3At:F1} death@{NativeDeepEndBossPreview.DeathAt:F1} loop@{NativeDeepEndBossPreview.Loop:F1}");Console.WriteLine(acts);
        Console.WriteLine("PASS deepend-boss ("+count+" checks)");
    }
    // ---- the table against the doc's table ----
    private static void Moves(string root)
    {
        var doc=File.ReadAllText(Path.Combine(root,"..","..","docs","EXPEDITION-BOSS-BACKROOMS.md"));var ms=NativeDeepEndBoss.Moves;Check(ms.Length==9,"nine moves");
        foreach(var m in ms)
        {
            var row=Regex.Match(doc,@"\|\s*`"+m.Id+@"`\s*\|\s*([^|]+)\|\s*(\d)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)\s*\|");
            Check(row.Success,m.Id+" has a row in the doc");Check(row.Groups[1].Value.Trim()==m.Title,m.Id+" title");Check(int.Parse(row.Groups[2].Value)==m.Phase,m.Id+" phase");
            Check(Near(float.Parse(row.Groups[3].Value,System.Globalization.CultureInfo.InvariantCulture),m.Anticipation),m.Id+" wind");Check(Near(float.Parse(row.Groups[4].Value,System.Globalization.CultureInfo.InvariantCulture),m.Strike),m.Id+" strike");Check(Near(float.Parse(row.Groups[5].Value,System.Globalization.CultureInfo.InvariantCulture),m.Follow),m.Id+" follow");Check(Near(float.Parse(row.Groups[6].Value,System.Globalization.CultureInfo.InvariantCulture),m.Recover),m.Id+" recover");
            Check(Near(m.Duration,m.Anticipation+m.Strike+m.Follow+m.Recover),m.Id+" duration");Check(NativeDeepEndBoss.Index(m.Id)>=0&&NativeDeepEndBoss.Move(m.Id).Title==m.Title,m.Id+" looks up");
        }
        Check(NativeDeepEndBoss.Move("jet").Arc==110&&NativeDeepEndBoss.Move("jet").Reach==30&&NativeDeepEndBoss.Move("breach").Radius==3.2f&&NativeDeepEndBoss.Move("flop").Reach==24&&NativeDeepEndBoss.Move("flop").Width==4&&NativeDeepEndBoss.Move("thrash").Radius==5,"the shapes");
        Check(NativeDeepEndBoss.Move("lap").Radius==19&&NativeDeepEndBoss.LapInner==9&&NativeDeepEndBoss.SeicheHeight==.9f&&NativeDeepEndBoss.DrainPullMin==5&&NativeDeepEndBoss.DrainPullMax==9,"Last Lap, Seiche, the Drain");
        Check(NativeDeepEndBoss.StillSeconds==6&&NativeDeepEndBoss.StillSecondsLast==4&&NativeDeepEndBoss.HighRule==1.6f&&NativeDeepEndBoss.ImmuneMost==6,"the no-safe-place numbers");
    }
    // ---- the model's numbers against the script's ----
    private static void Look(string root)
    {
        var py=File.ReadAllText(Path.Combine(root,"tools","gear","deep_end_boss.py"));
        float Const(string n){var m=Regex.Match(py,@"^"+n+@"\s*=\s*([\d.]+)",RegexOptions.Multiline);Check(m.Success,n+" in the script");return float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);}
        Check(Const("LENGTH")==NativeDeepEndBossLook.Length&&(int)Const("JOINTS")==NativeDeepEndBossLook.Segments,"the spine");Check(Const("FIN_S")==NativeDeepEndBossLook.FinS,"the fins' root");
        var lamps=Regex.Match(py,@"^LAMPS\s*=\s*\(([^)]*)\)",RegexOptions.Multiline).Groups[1].Value.Split(',').Select(x=>float.Parse(x.Trim(),System.Globalization.CultureInfo.InvariantCulture)).ToArray();Check(lamps.SequenceEqual(NativeDeepEndBossLook.LampS),"the lamps' places");
        Check(py.Contains("BANK = '"+NativeDeepEndBossLook.Bank+"'"),"the bank's name");
        // The radius: a blunt head 2.8 wide, a 2.4 body, .5 at the tail (doc's measures).
        float wide=0;for(float s=0;s<3.6f;s+=.1f)wide=Math.Max(wide,NativeDeepEndBossLook.Radius(s));Check(Near(wide*2,2.8f,.01f),"the head is 2.8 wide");Check(Near(NativeDeepEndBossLook.Radius(9)*2,2.4f,.01f),"the body 2.4 thick");Check(NativeDeepEndBossLook.Radius(32)*2<.5f,"the tail .5");
        // The skin's weights sum to one and leave a straight spine straight.
        for(float q=0;q<=30;q+=.37f){NativeDeepEndBossLook.Weights(q,out int b,out float w0,out float w1,out float w2,out float w3);Check(Near(w0+w1+w2+w3,1,1e-4f),"weights "+q);if(b>=0&&b+3<=30){float at=b*w0+(b+1)*w1+(b+2)*w2+(b+3)*w3;Check(Near(at,q,1e-3f),"B-spline keeps q "+q);}}
        // The stand-in is well formed.
        NativeDeepEndBossLook.Fallback(out var p,out var n,out var c,out var u,out var t);Check(t.All(i=>i>=0&&i<p.Length)&&p.Length==n.Length&&p.Length==c.Length&&p.Length==u.Length&&t.Length%3==0,"the stand-in mesh");
    }
    // ---- off by default; None draws nothing ----
    private static void Off()
    {
        Check(!NativeDeepEndBossPreview.On,"the preview is off by default");Check(double.IsNaN(NativeDeepEndBossPreview.Start),"the preview hasn't started");
        var none=DeepEndBossView.None;Check(!none.live,"None is not live");Check(none.Move<0&&!none.Moving(0,out _,out _,out _),"None plays nothing");
        var s=NativeDeepEndBossStage.Evaluate(none,12,DeepEndBossBeats.Fallback);Check(!s.On,"None's stage is off");Check(Near(s.Tide,PoolroomsLayout.WaterY),"the level's own water");Check(!NativeDeepEndBossStage.Shown.On,"nothing shown until drawn");
        Check(NativeDeepEndBossCues.Collect(none,new DeepEndBossRig(),0,100,new DeepEndBossCue[8])==0,"None queues no cues");
        Check(DeepEndBossBeats.Fallback.Valid,"the beats are in order");Check(Near(DeepEndBossBeats.Fallback.Hold,9.0678f,.001f)&&Near(DeepEndBossBeats.Fallback.NameAt,6.4261f,.001f),"the card and the drop");
    }
    // ---- the head's path: continuous, ballistic, on the hit ----
    private static DeepEndBossView Rec(string id,float at,float tx,float tz,float ax=float.NaN,float az=float.NaN)
    {var m=NativeDeepEndBoss.Move(id);var v=DeepEndBossView.None;v.live=true;v.action=id;v.actionId=7;v.actionAt=at;v.hitAt=at+m.Anticipation;v.duration=m.Duration;v.targetX=tx;v.targetZ=tz;v.aimX=ax;v.aimZ=az;return v;}
    private static void Routes()
    {
        float s=PoolroomsLayout.WaterY;
        foreach(float hitShift in new[]{0f,.3f})
        {
            // Breach: the mouth crosses the surface exactly on the record's hit, even a stretched one.
            var r=new DeepEndRoute();r.Reset(0,new Vector3(8,-4,8),new Vector3(0,0,1));var v=Rec("breach",1,5,-3);v.hitAt+=hitShift;DeepEndBossPaths.Append(r,v,s);
            r.At(v.hitAt,out var p,out var d);Check(Near(p.Y,s,.01f),"Breach crosses at its hit");Check(Near(p.X,5,.01f)&&Near(p.Z,-3,.01f),"at the locked circle");Check(d.Y>7,"rising");
            // It rises to at most the doc's height, on a true parabola.
            float top=-99;for(float t=v.hitAt;t<v.hitAt+3;t+=.01f){r.At(t,out p,out _);top=Math.Max(top,p.Y);}Check(top-s<5.65f&&top-s>4.6f,"Breach's height "+(top-s));Check(top<NativeDeepEndArena.Ceiling-1,"under the ceiling");
            float T=DeepEndBossPaths.LeapRise/DeepEndBossPaths.RiseG;r.At(v.hitAt+T,out var apex,out var av);Check(Math.Abs(av.Y)<.05f,"it hangs at the top");r.At(v.hitAt+T*.5f,out _,out var up);r.At(v.hitAt+T*1.5f,out _,out var down);Check(Near(up.Y,-down.Y,.05f)&&up.Y>0,"up and down alike");
            // Speed is continuous across every leg.
            AssertContinuous(r,.5f,v.hitAt+5,"breach");
        }
        foreach(var (id,tx,tz,ax,az) in new[]{("jet",-10f,4f,8f,8f),("flop",-12f,-7f,12f,-7f),("thrash",3f,4f,float.NaN,float.NaN),("drain",0f,0f,float.NaN,float.NaN),("seiche",0f,0f,1f,0f),("lap",0f,0f,float.NaN,float.NaN)})
        {
            var r=new DeepEndRoute();r.Reset(0,new Vector3(6,-5,-6),new Vector3(1,0,0));var v=Rec(id,1,tx,tz,ax,az);DeepEndBossPaths.Append(r,v,s);AssertContinuous(r,.5f,v.actionAt+v.duration+1,id);
        }
        // The Flop's lift lands on its hit: it reaches the surface exactly then, lying there after, from high above it just before.
        float depth=4.4f;var fm=NativeDeepEndBoss.Move("flop");float hit=fm.Anticipation;Check(Near(DeepEndBossRig.FlopLift(hit,hit,depth,fm.Strike,fm.Recover),depth,.01f),"the Flop lands on the hit");Check(DeepEndBossRig.FlopLift(hit-.3f,hit,depth,fm.Strike,fm.Recover)>depth+1,"from high");Check(DeepEndBossRig.FlopLift(0,hit,depth,fm.Strike,fm.Recover)==0,"hidden at its start");
        // Last Lap's path is a rounded square reaching the corners, and tightens.
        var lm=NativeDeepEndBoss.Move("lap");var q0=DeepEndBossPaths.LapAt(0,BossMath.Pi*.25f,lm,lm.Strike+lm.Follow);Check(new Vector2(q0.X,q0.Z).Length()>23,"it runs into the corners");var q1=DeepEndBossPaths.LapAt(1,BossMath.Pi*.25f,lm,lm.Strike+lm.Follow);Check(new Vector2(q1.X,q1.Z).Length()<new Vector2(q0.X,q0.Z).Length()-5,"it tightens");
        foreach(float u in new[]{0f,.2f,.5f,.8f,1f}){var q=DeepEndBossPaths.LapAt(u,1.1f,lm,lm.Strike+lm.Follow);Check(Math.Abs(q.X)<NativeDeepEndArena.Half&&Math.Abs(q.Z)<NativeDeepEndArena.Half,"Lap stays in the room");}
    }
    private static void AssertContinuous(DeepEndRoute r,float from,float to,string what)
    {
        r.At(from,out var a,out var va);float most=0;const float h=1/240f;for(float t=from+h;t<to;t+=h){r.At(t,out var b,out var vb);float jump=Vector3.Distance(b,a);most=Math.Max(most,jump/h);Check(Vector3.Distance(vb,va)<3.2f,what+" velocity cuts at "+t+": "+va+" -> "+vb);a=b;va=vb;}
        Check(most<72,what+" speed "+most);
    }
    // ---- the body on the trail ----
    private static void Rig()
    {
        var o=new Vector3(24,0,24);var rig=new DeepEndBossRig();var r=NativeDeepEndBossPreview.Route;
        // Through the first Breach: every visible joint keeps its spacing; it never moves faster than a heavy body may while it's in sight; each joint passes where the head passed.
        float p0=NativeDeepEndBossPreview.BreachAt-.5f;var v=DeepEndBossView.None;float t=p0;float most=0,mostT=0;int mostAt=0;float dt=1/60f;bool high=false;
        for(int k=0;k<60*7;k++)
        {
            t+=dt;NativeDeepEndBossPreview.View(t,ref v,out _);rig.Update(t,dt,v,r,o,PoolroomsLayout.WaterY,false,default);
            for(int i=0;i<NativeDeepEndBossLook.Spine-1;i++){var a=rig.Shown.Position[i];var b=rig.Shown.Position[i+1];float y=a.Y;bool vis=y>PoolroomsLayout.WaterY&&b.Y>PoolroomsLayout.WaterY;if(vis){float seg=Vector3.Distance(a,b);Check(seg>NativeDeepEndBossLook.Seg*.8f&&seg<NativeDeepEndBossLook.Seg*1.3f,"joint spacing "+seg+" at "+i+" t "+t);}}
            if(rig.OutOfWater>6)high=true;
            for(int i=4;i<NativeDeepEndBossLook.Spine;i++)if(rig.Shown.Position[i].Y>PoolroomsLayout.WaterY&&k>2){float sp=rig.Velocity[i].Length();if(sp>most){most=sp;mostAt=i;mostT=t;}}
        }
        Check(high,"it rises out of the water");Check(most<24,"a visible joint's speed "+most+" joint "+mostAt+" at "+mostT);
        // Nothing moves at a constant speed: the head's speed changes through the arc.
        var rr=NativeDeepEndBossPreview.Route;float lo=99,hi=0;for(float q=NativeDeepEndBossPreview.BreachAt+1.1f;q<NativeDeepEndBossPreview.BreachAt+3.3f;q+=.05f){rr.At(q,out _,out var hv);float sp=hv.Length();lo=Math.Min(lo,sp);hi=Math.Max(hi,sp);}Check(hi>lo*1.3f,"the arc's speed changes: "+lo+" .. "+hi);
        // The frames are real rotations and the back stays up on a shallow stretch.
        foreach(var qr in rig.Shown.Rotation)Check(Math.Abs(qr.Length()-1)<1e-3f,"a unit rotation");
        // The lamps sit on the flanks, at the coping.
        for(int i=0;i<NativeDeepEndBossLook.Lamps;i++){rig.LampFrame(i,out var at,out var q,out float s);NativeDeepEndBossLook.Lamp(i,out float ls,out _,out _);rig.SpineFrame(ls,out var c,out _);float d=Vector3.Distance(at,c);float rad=NativeDeepEndBossLook.Radius(ls);Check(d>rad*.7f&&d<rad*1.25f,"lamp "+i+" on the flank: "+d);Check(s>.1f&&s<.5f,"lamp size");}
        Check(rig.BladeScale.All(x=>x>.3f&&x<1),"the blades' scale");
    }
    // ---- determinism and allocation ----
    private static void Frames()
    {
        var o=new Vector3(24,0,24);var r=NativeDeepEndBossPreview.Route;float start=NativeDeepEndBossPreview.ArrivalEnd-3;
        // Two frame rates, one pose (the trail is on fixed ticks; between ticks the head comes from the route).
        var a=new DeepEndBossRig();var b=new DeepEndBossRig();var v=DeepEndBossView.None;
        float ta=start;while(ta<start+8){ta+=1/30f;NativeDeepEndBossPreview.View(ta,ref v,out _);a.Update(ta,1/30f,v,r,o,PoolroomsLayout.WaterY,false,default);}float end=ta;
        float tb=start;while(tb<end-1/144f){tb+=1/144f;NativeDeepEndBossPreview.View(tb,ref v,out _);b.Update(tb,1/144f,v,r,o,PoolroomsLayout.WaterY,false,default);}NativeDeepEndBossPreview.View(end,ref v,out _);b.Update(end,end-tb,v,r,o,PoolroomsLayout.WaterY,false,default);
        for(int i=0;i<NativeDeepEndBossLook.Spine;i++)Check(Vector3.Distance(a.Shown.Position[i],b.Shown.Position[i])<.08f,"two frame rates, joint "+i+": "+Vector3.Distance(a.Shown.Position[i],b.Shown.Position[i]));
        // The effects are a function of the record and the clock.
        var waves=new DeepEndWaves();waves.Place();var st=NativeDeepEndBossStage.Evaluate(v,end,DeepEndBossBeats.Fallback);waves.Compose(st);var f1=new NativeDeepEndBossFx();var f2=new NativeDeepEndBossFx();var eye=new Vector3(30,5,30);
        f1.Build(a,v,st,waves,end,eye,Vector3.UnitX,Vector3.UnitY,1,false,false);f2.Build(a,v,st,waves,end,eye,Vector3.UnitX,Vector3.UnitY,1,false,false);
        Check(f1.Vfx.Count==f2.Vfx.Count&&f1.Over.VertexCount==f2.Over.VertexCount&&f1.Under.VertexCount==f2.Under.VertexCount,"the same clock, the same effects");for(int i=0;i<f1.Vfx.Count;i++)Check(f1.Vfx.Items[i].At==f2.Vfx.Items[i].At,"item "+i);
        var s1=NativeDeepEndBossStage.Evaluate(v,end,DeepEndBossBeats.Fallback);var s2=NativeDeepEndBossStage.Evaluate(v,end,DeepEndBossBeats.Fallback);Check(s1.Tide==s2.Tide&&s1.Ceiling==s2.Ceiling&&s1.Whirl==s2.Whirl,"the stage is pure");
        // Effects thin with the quality.
        f2.Build(a,v,st,waves,end,eye,Vector3.UnitX,Vector3.UnitY,.3,true,false);Check(f2.Vfx.Count<=f1.Vfx.Count,"thinned by quality");
        // No allocation per frame, through the moves (after a warm-up, on one fixed rig).
        var rig=new DeepEndBossRig();var fx=new NativeDeepEndBossFx();var w2=new DeepEndWaves();w2.Place();var script=NativeDeepEndBossPreview.Route;var cue=new DeepEndBossCue[32];float t=NativeDeepEndBossPreview.BreachAt-1;const float dt=1/60f;var view=DeepEndBossView.None;
        void Frame(){float last=t;t+=dt;NativeDeepEndBossPreview.View(t,ref view,out _);var stage=NativeDeepEndBossStage.Evaluate(view,t,DeepEndBossBeats.Fallback);rig.Update(t,dt,view,script,o,stage.Tide,false,default);fx.Couple(rig,view,w2,stage,last,t);w2.Step(dt,stage.Tide>.02f);w2.Compose(stage);fx.Build(rig,view,stage,w2,t,eye,Vector3.UnitX,Vector3.UnitY,1,false,false);NativeDeepEndBossCues.Collect(view,rig,last,t,cue);}
        for(int k=0;k<120;k++)Frame();long before=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<60*40;k++)Frame();long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"0 bytes per frame: "+used+" allocated");
        Check(fx.Over.Refused==0&&fx.Under.Refused==0,"the water meshes hold what's built");
    }
    // ---- the room ----
    private static void Arena()
    {
        var src=NativePoolroomsGenerator.Build(NativePoolroomsGenerator.ArenaSeed,false).Layout;var copy=(byte[])src.Floor.Clone();NativeDeepEndBossPreview.CarveAt(src,out int cx,out int cz);var l=NativeDeepEndArena.Carve(src,cx,cz);
        Check(copy.SequenceEqual(src.Floor),"Carve leaves its source alone");Check(!ReferenceEquals(l,src),"Carve returns a copy");
        int half=NativeDeepEndArena.Cells/2,decks=0,deep=0,steps=0,abyss=0,lights=0,pillars=0;
        for(int z=cz-half;z<cz+half;z++)for(int x=cx-half;x<cx+half;x++){int i=l.Index(x,z);Check(l.Ceiling[i]==NativeDeepEndArena.Ceiling,"the ceiling");if(l.Pillar[i])pillars++;if(l.Light[i])lights++;byte f=l.Floor[i];if(f==PoolroomsLayout.Deck)decks++;else if(f==PoolroomsLayout.Deep)deep++;else if(f==PoolroomsLayout.Steps)steps++;else if(f==PoolroomsLayout.Abyss)abyss++;}
        Check(pillars==0,"no pillars");Check(abyss==16,"a 4 x 4 abyss");Check(steps==8,"steps on all four sides, each flight two cells wide");Check(deep+steps+abyss==100,"a 10 x 10 pool");Check(decks==300,"the deck all round");Check(lights>=36,"round lights in the ceiling: "+lights);
        // Walled all round but for its four arches, two cells wide at each wall's middle.
        int open=0;for(int k=-half;k<half;k++){if(l.Between(cx-half,cz+k,-1,0)==PoolroomsLayout.Open)open++;if(l.Between(cx+half-1,cz+k,1,0)==PoolroomsLayout.Open)open++;if(l.Between(cx+k,cz-half,0,-1)==PoolroomsLayout.Open)open++;if(l.Between(cx+k,cz+half-1,0,1)==PoolroomsLayout.Open)open++;}Check(open==8,"four arches: "+open);
        for(int z=cz-half;z<cz+half;z++)for(int x=cx-half;x<cx+half-1;x++)Check(l.Between(x,z,1,0)==PoolroomsLayout.Open,"open inside");
        // The mesh builds, the abyss's walls go down and there's no floor in the pit above its depth.
        var mesh=new PoolroomsMesh();mesh.Build(l,Vector3.Zero);float lowest=0;foreach(var p in mesh.Parts[(int)PoolroomsMesh.Surface.Basin].Positions)lowest=Math.Min(lowest,p.Y);Check(Near(lowest,PoolroomsLayout.AbyssFloor,.01f),"the abyss goes down: "+lowest);
        Check(NativeDeepEndArena.Floor(0,0)==PoolroomsLayout.AbyssFloor&&NativeDeepEndArena.Floor(6,0)==NativeDeepEndArena.PoolFloor&&NativeDeepEndArena.Floor(15,0)==0,"the floor's heights");
        Check(NativeDeepEndArena.Lamps.Length>20&&NativeDeepEndArena.Lamps.All(a=>a.Order>=0&&a.Order<=1&&NativeDeepEndArena.InRoom(a.At.X,a.At.Z)),"the lights under the water");
        // The live level is what it was: no abyss in it, and the floors' heights stand.
        Check(!src.Floor.Contains(PoolroomsLayout.Abyss),"no abyss in the live level");Check(PoolroomsLayout.FloorY(PoolroomsLayout.Deep)==-PoolroomsLayout.DeepDepth&&PoolroomsLayout.FloorY(PoolroomsLayout.Shallow)==-PoolroomsLayout.ShallowDepth&&PoolroomsLayout.FloorY(PoolroomsLayout.Deck)==0,"the level's floors unchanged");
    }
    // ---- the water ----
    private static void Water()
    {
        var w=new DeepEndWaves();w.Place();Check(DeepEndWaves.Extent>=NativeDeepEndArena.Half*2,"the field covers the room");
        var dry=DeepEndBossStage.Idle;dry.On=true;
        // Decks dry: a disturbance on a deck does nothing; the pool takes it and it stays in.
        w.Disturb(15,15,1,.2f);Check(w.Energy()==0,"a dry deck takes no wave");w.Disturb(5,5,1,.3f);double e0=w.Energy();Check(e0>0,"the pool does");for(int k=0;k<60;k++)w.Step(1/60f,false);
        foreach(var v in new[]{w.WaveAt(15,15),w.WaveAt(-15,15)})Check(v==0,"no wave on the deck");for(int k=0;k<60*12;k++)w.Step(1/60f,false);Check(w.Energy()<e0*.4,"waves die away "+w.Energy()+" of "+e0);
        // The tide covers every cell: the composed height is over the deck everywhere inside the room when flooded.
        var flooded=dry;flooded.Tide=1.1f;flooded.Flood=NativeDeepEndArena.Half;var w2=new DeepEndWaves();w2.Place();w2.Step(1/60f,true);w2.Compose(flooded);
        for(int z=0;z<DeepEndWaves.Size;z+=3)for(int x=0;x<DeepEndWaves.Size;x+=3){float wx=(x+.5f-DeepEndWaves.Size*.5f)*DeepEndWaves.Texel,wz=(z+.5f-DeepEndWaves.Size*.5f)*DeepEndWaves.Texel;if(!NativeDeepEndArena.InRoom(wx,wz))continue;Check(w2.Rg[(x+DeepEndWaves.Size*z)*2]>=1.09f,"flooded at "+wx+","+wz);}
        w2.Disturb(15,15,1,.2f);Check(w2.Energy()>0,"a flooded deck takes the wave");for(int k=0;k<60*4;k++)w2.Step(1/60f,true);Check(Math.Abs(w2.WaveAt(0,0))>0||w2.Energy()>0,"it spreads over the room");
        // A flood's front: dry far out, wet near the pool.
        var run=dry;run.Tide=.5f;run.Flood=3;float near=DeepEndWaves.Analytic(11,0,run,out _),far=DeepEndWaves.Analytic(18,0,run,out _);Check(near>.3f&&far<0,"the flood's front: "+near+" "+far);
        // The whirlpool sinks the middle and turns; the seiche's crest stands at its place.
        var wh=dry;wh.Whirl=1;wh.WhirlTurn=1;float mid=DeepEndWaves.Analytic(0,0,wh,out float f0),edge=DeepEndWaves.Analytic(19,0,wh,out _);Check(mid<edge-1,"the funnel: "+mid+" "+edge);
        var se=dry;se.SeicheAxis=Vector3.UnitX;se.SeicheAt=4;se.SeicheHeight=.9f;float crest=DeepEndWaves.Analytic(4,0,se,out float cf);Check(crest>se.Tide+.7f&&cf>.5f,"the seiche's crest: "+crest);
    }
    private static void Stage()
    {
        var v=DeepEndBossView.None;v.live=true;v.tideFrom=PoolroomsLayout.WaterY;v.tideY=.5f;v.tideAt=10;v.tideUntil=13;var b=DeepEndBossBeats.Fallback;
        Check(Near(NativeDeepEndBossStage.Evaluate(v,9,b).Tide,PoolroomsLayout.WaterY),"before the rise");Check(Near(NativeDeepEndBossStage.Evaluate(v,13.1f,b).Tide,.5f),"after it");float mid=NativeDeepEndBossStage.Evaluate(v,11.5f,b).Tide;Check(mid>PoolroomsLayout.WaterY&&mid<.5f,"rising");
        // The last phase's ceiling goes out, and the water's lights stay.
        var p3=DeepEndBossView.None;p3.live=true;p3.phase=3;p3.phaseAt=20;p3.tideY=1.1f;var s=NativeDeepEndBossStage.Evaluate(p3,26,b);Check(s.CeilingOut>.99f&&s.Water==1&&s.Ambient<.5f,"the ceiling dies, the water's lights stay");
        // The arrival: dark for the spiral, the room up on the drop.
        var ar=DeepEndBossView.None;ar.live=true;ar.spawned=0;var a=NativeDeepEndBossStage.Evaluate(ar,b.DownShot+.4f,b);Check(a.Ceiling<.1f&&a.Ambient<.1f,"black for the spiral");var d=NativeDeepEndBossStage.Evaluate(ar,b.Hold+.5f,b);Check(d.Ceiling>.95f&&d.WaterOut<.01f&&d.Ambient>.8f,"the lights slam on at the drop");Check(NativeDeepEndBossStage.Evaluate(ar,1,b).Warm>.9f,"warm at first");Check(NativeDeepEndBossStage.WashAt(b.Hold-.1f,b)>.5f&&NativeDeepEndBossStage.WashAt(b.Hold+1,b)==0,"the wave over the camera, and gone");
    }
    // ---- the intro ----
    private static void Intro()
    {
        var shots=NativeDeepEndBoss.Shots;var b=DeepEndBossBeats.Fallback;Check(shots.Length>=5,"the table's shots");Check(shots[0].From==0,"from the start");for(int i=1;i<shots.Length;i++)Check(Near(shots[i].From,shots[i-1].To),"contiguous "+i);Check(shots[shots.Length-1].To>=b.Hold,"to the drop");
        var o=new Vector3(24,0,24);for(float t=0;t<b.Hold;t+=.05f)
        {
            Check(NativeBackroomsBossIntro.Sample(shots,t,o,0,out var e,out var l,out float fov),"a shot at "+t);Check(fov>30&&fov<80,"fov");var q=e-o;Check(NativeDeepEndArena.InRoom(q.X,q.Z),"the eye is in the room at "+t+": "+q);Check(q.Y<NativeDeepEndArena.Ceiling-.2f&&q.Y>-.3f,"under the ceiling at "+t);Check(Vector3.Distance(e,l)>1,"it looks at something");
        }
        // The top-down shot looks straight down over the abyss; the body rises through the shaft, in sight of it, with lamps below it: the spiral.
        var down=shots[2];Check(Math.Abs(down.EyeA.X)<1&&Math.Abs(down.EyeA.Z)<1,"over the abyss");Check(Near(down.From,b.DownShot)&&Near(down.To,b.BreakShot),"on its beats");
        var r=NativeDeepEndBossPreview.Route;float sp=NativeDeepEndBossPreview.SpawnAt;r.At(sp+b.Break,out var at,out var vel);Check(Near(at.Y,PoolroomsLayout.WaterY,.02f),"it breaks the surface on the beat");Check(vel.Y>4,"rising");r.At(sp+b.Hold-1.05f,out var top,out var tv);Check(top.Y>4.6f&&top.Y<NativeDeepEndArena.Ceiling-.6f,"it hangs high: "+top.Y);
        for(float t=sp+b.DownShot;t<sp+b.Break;t+=.1f){r.At(t,out var h,out _);Check(NativeDeepEndArena.InAbyss(h.X,h.Z),"in the shaft at "+t);}
    }
    private static void Cues(string root)
    {
        var doc=File.ReadAllText(Path.Combine(root,"..","..","docs","EXPEDITION-BOSS-BACKROOMS.md"));var k=NativeDeepEndBossCues.Keys;Check(k.Distinct().Count()==k.Length,"keys distinct");foreach(var x in k){Check(x.StartsWith("boss.deepend."),x+" prefix");Check(doc.Contains("`"+x+"`"),x+" has its brief in the doc");}
        // The right cues on a record's moments, once each.
        var rig=new DeepEndBossRig();var v=DeepEndBossView.None;NativeDeepEndBossPreview.View(NativeDeepEndBossPreview.BreachAt+1,ref v,out _);rig.Update(NativeDeepEndBossPreview.BreachAt+1,.016f,v,NativeDeepEndBossPreview.Route,Vector3.Zero,PoolroomsLayout.WaterY,false,default);
        var buf=new DeepEndBossCue[32];int n=NativeDeepEndBossCues.Collect(v,rig,v.hitAt-.01f,v.hitAt+.01f,buf);Check(Enumerable.Range(0,n).Any(i=>buf[i].Key==NativeDeepEndBossCues.Breach),"Breach's cue on its hit");int m=NativeDeepEndBossCues.Collect(v,rig,v.hitAt+.02f,v.hitAt+.03f,buf);Check(Enumerable.Range(0,m).All(i=>buf[i].Key!=NativeDeepEndBossCues.Breach),"once");
    }
    private static void Preview()
    {
        var seen=new System.Collections.Generic.HashSet<string>();for(int i=0;i<NativeDeepEndBossPreview.ActCount;i++)seen.Add(NativeDeepEndBossPreview.ActAt(i).Id);foreach(var m in NativeDeepEndBoss.Moves)Check(seen.Contains(m.Id),"the preview plays "+m.Id);
        Check(NativeDeepEndBossPreview.Phase2At<NativeDeepEndBossPreview.Phase3At&&NativeDeepEndBossPreview.Phase3At<NativeDeepEndBossPreview.DeathAt&&NativeDeepEndBossPreview.DeathAt<NativeDeepEndBossPreview.Loop,"its order");
        var v=DeepEndBossView.None;NativeDeepEndBossPreview.View(2,ref v,out _);Check(v.live&&v.phase==1&&Near(v.tideY,PoolroomsLayout.WaterY),"phase 1 dry");NativeDeepEndBossPreview.View(NativeDeepEndBossPreview.Phase2At+4,ref v,out _);Check(v.phase==2&&Near(v.tideY,.5f),"phase 2 at the knee");NativeDeepEndBossPreview.View(NativeDeepEndBossPreview.Phase3At+4,ref v,out _);Check(v.phase==3&&Near(v.tideY,1.1f),"phase 3 at the chest");NativeDeepEndBossPreview.View(NativeDeepEndBossPreview.DeathAt+1,ref v,out _);Check(v.Dead(NativeDeepEndBossPreview.DeathAt+1)&&v.hp==0,"the death");
        // A pure function of the clock.
        var a=DeepEndBossView.None;var b=DeepEndBossView.None;NativeDeepEndBossPreview.View(40,ref a,out _);NativeDeepEndBossPreview.View(40,ref b,out _);Check(a.action==b.action&&a.actionId==b.actionId&&a.hp==b.hp,"the same p, the same record");
        // The camera never leaves the room, through the whole loop.
        var o=new Vector3(24,0,24);for(float p=0;p<NativeDeepEndBossPreview.Loop;p+=.2f){NativeDeepEndBossPreview.Route.At(p,out var h,out _);Check(NativeDeepEndBossPreview.Camera(p,o,h,h,new Vector3(0,0,1),out var e,out var l,out float fov),"camera at "+p);var q=e-o;Check(NativeDeepEndArena.InRoom(q.X,q.Z)&&q.Y<NativeDeepEndArena.Ceiling,"the camera in the room at "+p+": "+q);}
    }
}
