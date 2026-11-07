using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// The way to the boss room (Core NativeBossGuide; owner 2026-11-07: an animated guide to the boss room once the quota is cleared): its trigger, the
// route through the real Backrooms and Poolrooms grids (never through a wall), the trail fixed to the floor, the running light, the HUD line.
public static class BossGuideChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("boss-guide: "+what);count++;}
    private sealed class Sink:IKitFxSink
    {
        public int Bands,Glows;public bool Bad;public readonly List<Vector3> Tips=new List<Vector3>();
        static bool F(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Glows++;Bad|=!F(at)||!F(color);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Bad=true;}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Bad=true;}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Bad=true;}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Bad=true;}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Bands++;Bad|=view||!F(a)||!F(b)||!F(c)||!F(d)||!F(color)||ua<0||ub<0;Tips.Add((a+d)*.5f);}
    }
    private static ExpeditionView View(int progress,int target,bool cleared=false,double hp=5000,string[] defeated=null)
    {
        return new ExpeditionView{phase=ExpeditionPhase.Instance,area="exp-backrooms",instances=new[]{new ExpeditionViewInstance{id="exp-backrooms",target=target,progress=progress,current=true,cleared=cleared,bosses=new[]{"hum"},defeated=defeated??Array.Empty<string>()}},
            bossArrivals=new[]{new ExpeditionBossArrivalView{id="hum",area="exp-backrooms",archetype=NativeHumBoss.Archetype,x=60,y=0,z=70,hp=hp,maxHp=9000}}};
    }
    public static void Run(string root)
    {
        count=0;
        // The trigger.
        var g=NativeBossGuide.Of(View(30,30));Check(g.Active&&g.At==new Vector3(60,0,70)&&g.Archetype==NativeHumBoss.Archetype&&g.Name=="THE HUM","on once the quota is met and the boss has arrived");
        Check(!NativeBossGuide.Of(View(29,30)).Active,"off while the quota runs");Check(!NativeBossGuide.Of(View(30,30,true)).Active,"off once the instance is cleared");
        Check(!NativeBossGuide.Of(View(30,30,hp:0)).Active&&!NativeBossGuide.Of(View(30,30,defeated:new[]{"hum"})).Active,"off once its boss is beaten");
        var v=View(30,30);v.loading=true;Check(!NativeBossGuide.Of(v).Active,"off while loading");v=View(30,30);v.migrating=true;Check(!NativeBossGuide.Of(v).Active,"off in a host transfer");
        v=View(30,30);v.phase=ExpeditionPhase.Town;Check(!NativeBossGuide.Of(v).Active,"only in an instance");v=View(30,30);v.area="exp-poolrooms";Check(!NativeBossGuide.Of(v).Active,"only the viewer's own area");
        v=View(30,30);v.bossArrivals=Array.Empty<ExpeditionBossArrivalView>();Check(!NativeBossGuide.Of(v).Active,"not before its boss arrives");
        Check(NativeBossGuide.Of(null).Active==false,"no view, no guide");
        Check(NativeBossGuide.Near(NativeHumBoss.Archetype)<NativeHumArena.Size/2&&NativeBossGuide.Near(NativeDeepEndBoss.Archetype)<20&&NativeBossGuide.Near("siren")>0,"in its room counts as there");
        NativeBossGuide.Tone(NativeDeepEndBoss.Archetype,out int deep,out int light);Check(deep==NativeBossChips.Sets[4].Rarity&&light==NativeBossChips.Sets[4].Label,"in the boss's own colours");
        Check(NativeBossGuide.Tag("THE HUM",41.6f)=="THE HUM · 42 m"&&ReferenceEquals(NativeBossGuide.Tag("THE HUM",42.2f),NativeBossGuide.Tag("THE HUM",42.4f)),"the HUD line, cached");
        // The running light runs toward the room and stands still with Reduced effects.
        float Peak(float time){float best=-1,at=0;for(float s=0;s<=NativeBossGuide.Reach;s+=.1f){float p=NativeBossGuide.Pulse(s,time,false);if(p>best){best=p;at=s;}}return at;}
        Check(Peak(.9f)>Peak(.5f)&&Peak(1.4f)>Peak(.9f),"the light runs toward the room");Check(NativeBossGuide.Pulse(10,1,true)==0,"none with Reduced effects");
        Check(NativeBossGuide.Fade(.5f)==0&&NativeBossGuide.Fade(8)>.99f&&NativeBossGuide.Fade(NativeBossGuide.Reach)<.01f,"hidden underfoot, faded at its reach");
        // The Backrooms: from the entry to THE HUM's room, the real way.
        foreach(int seed in new[]{1,7,31,647031})
        {
            var built=NativeBackroomsGenerator.Build(seed,true);var l=built.Layout;
            int rx=Math.Max(0,Math.Min(l.Width-12,l.ExitX-6)),rz=Math.Max(0,Math.Min(l.Depth-12,l.ExitZ-6));var room=new HumRoom(rx,rz,Vector3.Zero).Centre;
            var e=NativeBackroomsGenerator.Position(l,l.Index(l.EntryX,l.EntryZ));var start=new Vector3(e.x+.4f,0,e.z-.3f);var r=new BossGuideRoute();
            Check(r.Update(l,start,room),"backrooms "+seed+": a route");
            var end=r.Points[r.Points.Count-1];Check(Vector2.Distance(new Vector2(end.X,end.Z),new Vector2(room.X,room.Z))<NativeHumArena.Size/2,"backrooms "+seed+": it reaches THE HUM's room ("+Vector2.Distance(new Vector2(end.X,end.Z),new Vector2(room.X,room.Z))+" m)");
            Check(r.Points[0].X==start.X&&r.Points[0].Z==start.Z&&r.Length>=Vector2.Distance(new Vector2(start.X,start.Z),new Vector2(end.X,end.Z))-.01f,"backrooms "+seed+": from the viewer, no shorter than the crow flies");
            int Cell(Vector3 p)=>l.Index(Math.Max(0,Math.Min(l.Width-1,(int)Math.Floor(p.X/BackroomsLayout.Cell))),Math.Max(0,Math.Min(l.Depth-1,(int)Math.Floor(p.Z/BackroomsLayout.Cell))));
            bool Joined(int a,int b){int ax=a%l.Width,az=a/l.Width,bx=b%l.Width,bz=b/l.Width,dx=bx-ax,dz=bz-az;if(Math.Abs(dx)+Math.Abs(dz)!=1)return false;return NativeBackroomsGenerator.Link(l,a,dx,dz)||NativeBackroomsGenerator.Link(l,b,-dx,-dz);}
            int prev=Cell(r.Points[0]);
            for(float s=.05f;s<=r.Length;s+=.05f){r.Sample(s,out var p,out _);int c=Cell(p);if(c==prev)continue;
                bool ok=Joined(prev,c);if(!ok){int px=prev%l.Width,pz=prev/l.Width,cx=c%l.Width,cz=c/l.Width;int m1=l.Index(cx,pz),m2=l.Index(px,cz);ok=Joined(prev,m1)&&Joined(m1,c)||Joined(prev,m2)&&Joined(m2,c);}
                Check(ok,"backrooms "+seed+": never through a wall (at "+s.ToString("0.0")+" m)");prev=c;}
            // The trail stays put on the floor as the viewer walks within their cell.
            var sink1=new Sink();NativeBossGuide.Draw(sink1,r,0,1,deep,light,1,false);Check(!sink1.Bad&&sink1.Bands>8,"backrooms "+seed+": chevrons drawn ("+sink1.Bands+")");
            var r2=new BossGuideRoute();r2.Update(l,start+new Vector3(-.2f,0,.15f),room);var sink2=new Sink();NativeBossGuide.Draw(sink2,r2,0,1,deep,light,1,false);
            int fixedTips=sink1.Tips.Take(sink1.Tips.Count-6).Count(a=>sink2.Tips.Any(b=>Vector3.Distance(a,b)<1e-3f));Check(fixedTips>=sink1.Tips.Count-8,"backrooms "+seed+": fixed to the floor as you walk ("+fixedTips+"/"+sink1.Tips.Count+")");
            var low=new Sink();NativeBossGuide.Draw(low,r,0,1,deep,light,.3f,true);Check(low.Bands<sink1.Bands&&!low.Bad,"backrooms "+seed+": fewer on the low tier");
            var none=new Sink();NativeBossGuide.Draw(none,r,0,0,deep,light,1,false);Check(none.Bands==0&&none.Glows==0,"backrooms "+seed+": nothing at level 0");
            Check(sink1.Tips.All(t=>Math.Abs(t.Y-NativeBossGuide.Lift)<.06f),"backrooms "+seed+": on the floor");
        }
        // The Poolrooms: over the decks to THE DEEP END's room.
        foreach(int seed in new[]{2,11,4242})
        {
            var built=NativePoolroomsGenerator.Build(seed,true);var l=built.Layout;var c=NativeDeepEndArena.Centre(l,Vector3.Zero,built.BossCell%l.Width,built.BossCell/l.Width);
            var e=NativePoolroomsGenerator.Position(l,l.Index(l.EntryX,l.EntryZ));var r=new BossGuideRoute();
            Check(r.Update(l,new Vector3(e.x,e.y,e.z),c),"poolrooms "+seed+": a route");var end=r.Points[r.Points.Count-1];
            Check(Vector2.Distance(new Vector2(end.X,end.Z),new Vector2(c.X,c.Z))<21,"poolrooms "+seed+": it reaches THE DEEP END's room ("+Vector2.Distance(new Vector2(end.X,end.Z),new Vector2(c.X,c.Z))+" m)");
            var sink=new Sink();NativeBossGuide.Draw(sink,r,1.3f,1,deep,light,1,false);Check(!sink.Bad&&sink.Bands>4,"poolrooms "+seed+": chevrons drawn");
        }
        var empty=new BossGuideRoute();Check(!empty.Update((BackroomsLayout)null,Vector3.Zero,Vector3.One)&&!empty.Valid,"no grid, no route");
        Console.WriteLine("PASS boss-guide ("+count+" checks)");
    }
}
