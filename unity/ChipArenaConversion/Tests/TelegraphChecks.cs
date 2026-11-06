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
        Shapes();Timing();Noctiel();
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
        Console.WriteLine("PASS telegraphs ("+count+" checks: six shapes, the fill and the lock-on, every NOCTIEL warning covers what its rule hits)");
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
