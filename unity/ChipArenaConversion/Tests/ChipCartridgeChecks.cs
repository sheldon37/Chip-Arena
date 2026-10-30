#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The chip's neural core cartridge design (Core/NativeChipCartridge.cs, NativeChipBake.cs; drawn by Runtime NativeChipArt /
    // NativeChipCard and the chip lists; the 3D cartridge is MachineModelChecks'): the tier table follows the rank order and the
    // scheme's colours exactly (GameContent's rarities); the tiers escalate in ornament and every tier has a shape signature of
    // its own (colour-blind cue); the outline, its walk and the traces are sound; every animation curve is bounded, continuous
    // and settles; the bakes are deterministic, in range, sized as drawn and wear the tier's ornaments in its colours; and the
    // runtime draws every layer at every chip site, keeps every hook (holo scan, lock, reveal, badge, recede, focus, chosen) and
    // allocates nothing per frame in the card draw.
    internal static class ChipCartridgeChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Chip cartridge: "+what);count++;}
        public static void Run(string root)
        {
            count=0;var R=new Func<string,string>(f=>File.ReadAllText(Path.Combine(root,f)));
            Tiers(root);Geometry();Curves();Bakes();Wiring(R);
            Console.WriteLine("PASS chip cartridge ("+count+" checks: tiers in rank order with the scheme's colours, escalating ornaments and a shape per tier, the outline and traces, bounded continuous animation curves, deterministic bakes in the tier's colours and ornaments, and every chip site drawing the cartridge with its hooks kept and no per-frame allocation)");
        }
        static Vector3 Rgb(int hex)=>new Vector3(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f);
        static float Hue(Vector3 c){float mx=Math.Max(c.X,Math.Max(c.Y,c.Z)),mn=Math.Min(c.X,Math.Min(c.Y,c.Z)),d=mx-mn;if(d<1e-4f)return -1;float h=mx==c.X?((c.Y-c.Z)/d)%6:mx==c.Y?(c.Z-c.X)/d+2:(c.X-c.Y)/d+4;h*=60;return h<0?h+360:h;}
        static float HueGap(float a,float b){float d=Math.Abs(a-b)%360;return d>180?360-d:d;}
        static float Sat(Vector3 c){float mx=Math.Max(c.X,Math.Max(c.Y,c.Z)),mn=Math.Min(c.X,Math.Min(c.Y,c.Z));return mx<=0?0:(mx-mn)/mx;}

        // ---- the tier table ----
        static void Tiers(string root)
        {
            var tiers=NativeChipCartridge.Tiers;
            for(int i=0;i<tiers.Length;i++){Check(NativeChipRanks.RarityRank(tiers[i])==i,tiers[i]+" in NativeChipRanks' order");Check(NativeDevChips.Rank(tiers[i])==i,tiers[i]+" in the DEV tools' order");}
            // The scheme: GameContent's rarities exactly, and the machines' lamps.
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json"))))
            {
                var rarities=doc.RootElement.GetProperty("rarities").EnumerateArray().ToList();Check(rarities.Count==tiers.Length,"one look per rarity ("+rarities.Count+")");
                foreach(var r in rarities){string id=r.GetProperty("id").GetString();int hex=Convert.ToInt32(r.GetProperty("color").GetString().TrimStart('#'),16);Check(tiers.Contains(id),id+" has a look");Check(NativeChipCartridge.Rarity(id)==hex,id+" is #"+hex.ToString("x6"));Check(NativeChipCartridge.Of(id).Rarity==hex,id+"'s look carries its colour");}
            }
            for(int i=0;i<6;i++)Check(NativeMachineModels.RarityColors[i]==NativeChipCartridge.Rarity(tiers[i]),tiers[i]+" lamp matches");
            Check(NativeChipCartridge.Of("rainbow").Crest==NativeChipCartridge.Crest.Prism&&NativeChipCartridge.Known("rainbow")&&!NativeChipCartridge.Known("bogus")&&NativeChipCartridge.Of("bogus").Tier=="common","rainbow is the prism; an unknown tier is common");
            // Escalation: ranks 1-6, ornaments strictly rising; the named ornaments where the design puts them.
            string[] std={"common","uncommon","rare","epic","legendary","mythic"};
            for(int i=0;i<6;i++){var l=NativeChipCartridge.Of(std[i]);Check(l.Rank==i+1,std[i]+" rank");if(i>0)Check(l.Ornaments>NativeChipCartridge.Of(std[i-1]).Ornaments,std[i]+" wears more than "+std[i-1]);}
            var c=NativeChipCartridge.Of("common");Check(c.Ornaments==0&&c.Metal==NativeChipCartridge.Metal.Steel&&c.Brushed,"common: plain brushed steel");
            Check(NativeChipCartridge.Of("uncommon").Seams,"uncommon: lit seams");var r2=NativeChipCartridge.Of("rare");Check(r2.SecondInlay&&r2.Holo,"rare: second inlay, holographic glass");
            var e=NativeChipCartridge.Of("epic");Check(e.Fins&&e.Ring,"epic: fins and the ring");
            var lg=NativeChipCartridge.Of("legendary");Check(lg.Metal==NativeChipCartridge.Metal.Gold&&lg.Crest==NativeChipCartridge.Crest.Crown&&lg.Gems&&lg.Embers,"legendary: gold, crown, gems, embers");
            var m=NativeChipCartridge.Of("mythic");Check(m.Fractured&&m.Foil&&m.Crest==NativeChipCartridge.Crest.Shards&&m.Metal==NativeChipCartridge.Metal.BlackChrome&&NativeChipCartridge.Shards(m)>0,"mythic: fractured crystal, foil, shards");
            var kits=new[]{"mishima","ansatsuken","l44t","siren"};Check(kits.All(k=>NativeChipCartridge.Of(k).Rank==0&&NativeChipCartridge.Of(k).Crest!=NativeChipCartridge.Crest.None),"each kit has a crest of its own");
            // A shape per tier (it reads without colour): no two tiers share ornament set, crest and metal.
            var sig=tiers.Select(t=>{var l=NativeChipCartridge.Of(t);return (l.Seams,l.SecondInlay,l.Holo,l.Fins,l.Ring,l.Gems,l.Fractured,l.Foil,l.Embers,l.Crest,l.Metal,l.Rank);}).ToList();
            Check(sig.Distinct().Count()==tiers.Length,"every tier has its own shape signature");
            // The look's colours: the crystal and inlay in the tier's hue (white tiers stay pale), breath within sense.
            foreach(var t in tiers)
            {
                var l=NativeChipCartridge.Of(t);float h=Hue(Rgb(l.Rarity));Check(l.Breath>=1.2f&&l.Breath<=4&&l.Depth>0&&l.Depth<.8f,t+" breath");
                if(Sat(Rgb(l.Rarity))>.3f&&t!="mishima"){Check(HueGap(Hue(Rgb(l.Inlay)),h)<25,t+" inlay in its hue");Check(HueGap(Hue(Rgb(l.Core)),h)<25,t+" core in its hue");}
                else if(t=="common")Check(Sat(Rgb(l.Inlay))<.15f&&Sat(Rgb(l.Core))<.2f,"common stays pale");
            }
            Check(HueGap(Hue(Rgb(NativeChipCartridge.Of("mishima").Core)),Hue(Rgb(0xff2448)))<20,"mishima's devil-red core in its steel");
        }

        // ---- the outline, its walk, the hexagon and the traces ----
        static void Geometry()
        {
            foreach(var (w,h,tc,bc) in new[]{(272f,156f,NativeChipCartridge.TopCut,NativeChipCartridge.BottomCut),(118f,120f,NativeChipCartridge.TopCut,NativeChipCartridge.BottomCut),(500f,300f,NativeChipCartridge.TopCutLarge,NativeChipCartridge.BottomCutLarge)})
            {
                var o=NativeChipCartridge.Outline(w,h,tc,bc);float sum=0;for(int i=0;i<8;i++)sum+=Vector2.Distance(o[i],o[(i+1)%8]);float L=NativeChipCartridge.Perimeter(w,h,tc,bc);
                Check(Math.Abs(sum-L)<1e-3f,"perimeter is the outline's length ("+sum+" vs "+L+")");
                for(int i=0;i<8;i++){var a=o[i];var b=o[(i+1)%8];var cc=o[(i+2)%8];float cross=(b.X-a.X)*(cc.Y-b.Y)-(b.Y-a.Y)*(cc.X-b.X);Check(cross>=-1e-4f,"outline convex and clockwise (y down)");}
                Check(o.All(p=>p.X>=0&&p.X<=w&&p.Y>=0&&p.Y<=h),"outline inside its rect");
                // The walk is continuous, starts at the top edge's left end and comes back round; inset points lie inside.
                Vector2 prev=NativeChipCartridge.Along(w,h,tc,bc,0);Check(Vector2.Distance(prev,o[0])<1e-3f,"the walk starts on the top edge's left end");
                for(float d=.5f;d<=L+.01f;d+=.5f){var p=NativeChipCartridge.Along(w,h,tc,bc,d);Check(Vector2.Distance(p,prev)<=.51f,"the walk is continuous at "+d);prev=p;}
                Check(Vector2.Distance(NativeChipCartridge.Along(w,h,tc,bc,L*1.5f),NativeChipCartridge.Along(w,h,tc,bc,L*.5f))<1e-3f,"the walk wraps");
                for(float d=0;d<L;d+=7){var p=NativeChipCartridge.Along(w,h,tc,bc,d,2);Check(NativeChipBake.Poly(new Vector2(p.X,p.Y),o)<-.5f,"an inset point lies inside");}
                var u=NativeChipCartridge.UnitOutline(w,h,tc,bc);Check(Math.Abs(u.Min(p=>p.X)+u.Max(p=>p.X))<1e-3f&&Math.Abs(u.Min(p=>p.Y)+u.Max(p=>p.Y))<1e-3f,"the unit outline is centred");
            }
            var hex=NativeChipCartridge.Hex(new Vector2(3,4),10,true);Check(hex.Length==6&&hex.All(p=>Math.Abs(Vector2.Distance(p,new Vector2(3,4))-10)<1e-3f)&&Math.Abs(hex[0].X-3)<1e-3f&&hex[0].Y<4,"a pointy-top hexagon");
            // The traces: PCB style (straight or 45 degrees), leaving the core's collar, inside the baked image's extent.
            foreach(var t in NativeChipCartridge.Traces)
            {
                Check(t[0].Length()>=.9f&&t[0].Length()<=1.3f,"a trace leaves the collar ("+t[0]+")");
                for(int i=1;i<t.Length;i++){var d=t[i]-t[i-1];bool ok=Math.Abs(d.X)<1e-4f||Math.Abs(d.Y)<1e-4f||Math.Abs(Math.Abs(d.X)-Math.Abs(d.Y))<1e-3f;Check(ok,"trace runs straight or at 45 degrees");}
                Check(t.All(p=>p.X>=NativeChipBake.TraceOrigin+.2f&&p.X<=NativeChipBake.TraceSpanX-.2f&&p.Y>=NativeChipBake.TraceOrigin+.2f&&p.Y<=NativeChipBake.TraceSpanY-.2f),"trace inside the baked extent");
                Check(Vector2.Distance(NativeChipCartridge.OnTrace(Array.IndexOf(NativeChipCartridge.Traces,t),0),t[0])<1e-4f&&Vector2.Distance(NativeChipCartridge.OnTrace(Array.IndexOf(NativeChipCartridge.Traces,t),1),t[t.Length-1])<1e-4f,"a packet runs the whole trace");
            }
            // The header stays clear: no trace crosses above the core's centre line to the right of the collar beyond 2 radii.
            Check(NativeChipCartridge.Traces.All(t=>t.All(p=>!(p.X>2&&p.Y<.3f))),"traces keep out of the title");
        }

        // ---- the animation curves ----
        static void Curves()
        {
            foreach(var t in NativeChipCartridge.Tiers.Concat(new[]{"rainbow"}))
            {
                var l=NativeChipCartridge.Of(t);float prev=NativeChipCartridge.Breath(l,0,false);
                for(float s=0;s<12;s+=1/240f)
                {
                    float b=NativeChipCartridge.Breath(l,s,false),light=NativeChipCartridge.CoreLight(l,s,false,0);
                    Check(b>=0&&b<=1,t+" breath in 0..1");Check(Math.Abs(b-prev)<.02f,t+" breath continuous");prev=b;Check(light>=.5f&&light<=1.4f,t+" core light bounded ("+light+")");
                }
                Check(Math.Abs(NativeChipCartridge.Breath(l,1.3f,false)-NativeChipCartridge.Breath(l,1.3f+l.Breath,false))<1e-4f,t+" breath periodic");
                Check(NativeChipCartridge.Breath(l,0,true)==NativeChipCartridge.Breath(l,7.7f,true),t+" calm holds still");
                Check(NativeChipCartridge.CoreLight(l,1,false,1)<=2.1f&&NativeChipCartridge.CoreLight(l,1,false,5)==NativeChipCartridge.CoreLight(l,1,false,1),t+" charge clamped");
            }
            // The sweep: off between passes, a monotone pass of SweepTime every SweepEvery, offset by the chip's phase; calm none.
            for(float ph=0;ph<1;ph+=.25f)
            {
                float last=-1;int on=0;bool wasOn=false;
                for(float s=0;s<NativeChipCartridge.SweepEvery*3;s+=1/120f)
                {
                    bool now=NativeChipCartridge.Sweep(s,ph,false,out float u);if(now){on++;Check(u>=0&&u<=1,"sweep progress in 0..1");if(wasOn)Check(u>=last-1e-4f,"sweep runs out monotonically");last=u;}else last=-1;wasOn=now;
                    Check(!NativeChipCartridge.Sweep(s,ph,true,out _),"calm: no sweep");
                }
                float share=on/(NativeChipCartridge.SweepEvery*3*120f);Check(Math.Abs(share-NativeChipCartridge.SweepTime/NativeChipCartridge.SweepEvery)<.02f,"sweep's share of the time");
            }
            var ids=new[]{"vital","ricochet","piercing","splitter","vacuum","chaos"};Check(ids.Select(NativeChipCartridge.Phase).Distinct().Count()>=5&&ids.All(i=>NativeChipCartridge.Phase(i)>=0&&NativeChipCartridge.Phase(i)<1)&&NativeChipCartridge.Phase("vital")==NativeChipCartridge.Phase("vital")&&NativeChipCartridge.Phase(null)==0,"phases spread the row's sweeps, stable per chip");
            // The sheen: one left-to-right pass of SheenTime.
            {float last=-9;for(float a=0;a<NativeChipCartridge.SheenTime;a+=.005f){Check(NativeChipCartridge.Sheen(a,out float x),"sheen during its pass");Check(x>=last-1e-4f&&x>=-1.3f&&x<=1.3f,"sheen crosses once");last=x;}Check(!NativeChipCartridge.Sheen(NativeChipCartridge.SheenTime+.01f,out _)&&!NativeChipCartridge.Sheen(-.1f,out _),"sheen only in its pass");}
            // The orbit: a full turn, continuous (wrapping), calm still.
            {float prev=NativeChipCartridge.Orbit(0,false);for(float s=0;s<12;s+=1/240f){float o=NativeChipCartridge.Orbit(s,false);Check(o>=0&&o<2*Math.PI+1e-4f,"orbit angle");float d=Math.Abs(o-prev);Check(d<.02f||d>2*Math.PI-.02f,"orbit continuous");prev=o;}Check(NativeChipCartridge.Orbit(1,true)==NativeChipCartridge.Orbit(9,true),"calm orbit still");}
            // The assembly: the frame snaps in (Spread → 0 by 45%, small overshoot), fades in, the core ignites (dark, a flash, settled
            // by 70%), the pads light left to right, everything settled at 1.
            {
                float prev=NativeChipCartridge.FrameOffset(0),ig=NativeChipCartridge.Ignite(0);Check(Math.Abs(prev-NativeChipCartridge.Spread)<1e-4f&&NativeChipCartridge.FrameAlpha(0)==0&&ig==0,"assembly starts apart, dark");
                for(float u=0;u<=1.0001f;u+=1/400f)
                {
                    float o=NativeChipCartridge.FrameOffset(u),i=NativeChipCartridge.Ignite(u),a=NativeChipCartridge.FrameAlpha(u);Check(o>=-NativeChipCartridge.Spread*.3f&&o<=NativeChipCartridge.Spread+1e-4f,"frame offset bounded");Check(Math.Abs(o-prev)<.6f,"frame snaps continuously");prev=o;
                    Check(i>=0&&i<=1.8f+1e-4f&&a>=0&&a<=1,"ignition and fade bounded");Check(Math.Abs(i-ig)<.08f,"ignition continuous");ig=i;
                    for(int p=1;p<12;p++)Check(NativeChipCartridge.PadLit(u,p,12)<=NativeChipCartridge.PadLit(u,p-1,12)+1e-5f,"pads light left to right");
                }
                Check(Math.Abs(NativeChipCartridge.FrameOffset(.5f))<1e-3f&&NativeChipCartridge.FrameOffset(1)==0&&NativeChipCartridge.FrameAlpha(1)==1&&NativeChipCartridge.Ignite(.7f)==1&&NativeChipCartridge.Ignite(1)==1&&NativeChipCartridge.Ignite(.3f)==0,"assembly settles");
                Check(Enumerable.Range(0,12).All(p=>NativeChipCartridge.PadLit(.69f,p,12)==0&&NativeChipCartridge.PadLit(1,p,12)==1),"pads dark until 70%, all lit at the end");
                Check(Enumerable.Range(0,400).Any(k=>NativeChipCartridge.Ignite(k/400f)>1.5f),"the core flashes as it ignites");
            }
            // Particles: lives in 0..1, opacity in and out.
            for(float s=0;s<10;s+=.01f)for(int i=0;i<8;i++){float k=NativeChipCartridge.Life(s,i,2.6f),a=NativeChipCartridge.LifeAlpha(k);Check(k>=0&&k<1&&a>=0&&a<=1,"particle life");}
            Check(NativeChipCartridge.Embers(NativeChipCartridge.Of("legendary"))>0&&NativeChipCartridge.Embers(NativeChipCartridge.Of("epic"))==0&&NativeChipCartridge.Shards(NativeChipCartridge.Of("legendary"))==0,"embers Legendary, shards Mythic");
        }

        // ---- the bakes ----
        static bool Same(NativeChipBake.Image a,NativeChipBake.Image b)=>a.W==b.W&&a.H==b.H&&a.Rgba.SequenceEqual(b.Rgba);
        static void InRange(NativeChipBake.Image im,string what){Check(im.W>0&&im.H>0&&im.Rgba.Length==im.W*im.H*4,what+" sized");Check(im.Rgba.All(v=>float.IsFinite(v)&&v>=0&&v<=1),what+" channels in 0..1");}
        // Pixel (x, y) from the top (images are bottom row first).
        static Vector4 Px(NativeChipBake.Image im,int x,int y){int i=((im.H-1-y)*im.W+x)*4;return new Vector4(im.Rgba[i],im.Rgba[i+1],im.Rgba[i+2],im.Rgba[i+3]);}
        static float AlphaSum(NativeChipBake.Image im,int y0,int y1){float a=0;for(int y=y0;y<y1;y++)for(int x=0;x<im.W;x++)a+=Px(im,x,y).W;return a;}
        static void Bakes()
        {
            foreach(var t in NativeChipCartridge.Tiers.Concat(new[]{"rainbow"}))
            {
                var l=NativeChipCartridge.Of(t);
                foreach(float sc in new[]{.8f,1.2f,1.6f})foreach(bool large in new[]{false,true})
                {
                    var sh=NativeChipBake.Shell(t,sc,large);InRange(sh,t+" shell");Check(Same(sh,NativeChipBake.Shell(t,sc,large)),t+" shell deterministic");
                    NativeChipBake.ShellSlices(large,out float side,out float top,out float bottom,out float mid);Check(sh.W==(int)Math.Round((2*side+mid)*sc)&&sh.H==(int)Math.Round((top+mid+bottom)*sc),t+" shell sized as the nine-slice draws it");
                    Check(Px(sh,0,0).W<.05f&&Px(sh,sh.W-1,0).W<.05f,t+" shell's shoulder cuts are open");Check(Px(sh,sh.W/2,sh.H/2).W>.99f,t+" shell's body is opaque");
                    // The inlay in the groove at the bezel's foot, in the tier's colour (the left edge's middle row).
                    // The groove's brightest pixel (the left edge's middle row, just inside the bezel's land): lit, and in the inlay's hue.
                    float bw=(large?NativeChipCartridge.BezelLarge:NativeChipCartridge.Bezel)*sc;var want=Rgb(l.Inlay);var lit=Vector3.Zero;
                    for(int x=(int)(bw-.5f);x<=(int)(bw+1.5f*sc+1);x++){var c=Px(sh,x,sh.H/2);var v=new Vector3(c.X,c.Y,c.Z);if(Vector3.Dot(v,want)>Vector3.Dot(lit,want))lit=v;}
                    Check(Vector3.Dot(lit,want)/want.LengthSquared()>.45f,t+" inlay lit at "+sc+" ("+lit+")");
                    if(Sat(want)>.3f)Check(HueGap(Hue(lit),Hue(want))<25,t+" inlay in its colour at "+sc+" ("+lit+" vs "+want+")");
                    if(l.Metal==NativeChipCartridge.Metal.Gold){var g=Px(sh,(int)(bw*.5f),sh.H/2);Check(g.X>g.Y&&g.Y>g.Z&&g.X>.45f,"legendary's bezel is gold ("+g+")");}
                }
                foreach(int px in new[]{36,54,84,120})
                {
                    var co=NativeChipBake.Core(t,px);InRange(co,t+" core");Check(Same(co,NativeChipBake.Core(t,px)),t+" core deterministic");Check(Px(co,0,0).W<.05f&&Px(co,px/2,px/2).W>.99f,t+" core: open corners, a solid centre");
                    // The crystal in the tier's hue (white and steel tiers pale, Siren's void dark).
                    var mean=Vector3.Zero;int n=0;for(int y=px/2-px/8;y<px/2+px/8;y++)for(int x=px/2-px/8;x<px/2+px/8;x++){var c=Px(co,x,y);mean+=new Vector3(c.X,c.Y,c.Z);n++;}mean/=n;
                    if(Sat(Rgb(l.Core))>.3f&&t!="rainbow")Check(HueGap(Hue(mean),Hue(Rgb(l.Core)))<30,t+" crystal in its hue ("+mean+")");
                    Check(mean.Length()<1.45f,t+" crystal leaves the icon contrast ("+mean+")");
                }
                var badge=NativeChipBake.Badge(t,1.2f);InRange(badge,t+" badge");Check(Same(badge,NativeChipBake.Badge(t,1.2f)),t+" badge deterministic");
                float crestStrip=AlphaSum(badge,0,(int)(2*1.2f));Check(l.Crest!=NativeChipCartridge.Crest.None?crestStrip>.4f:crestStrip<.05f,t+" badge's crest strip by tier ("+crestStrip+")");
                if(NativeChipBake.HasCrest(t)){var cr=NativeChipBake.Crest(t,1.2f);InRange(cr,t+" crest");Check(AlphaSum(cr,0,cr.H)>40&&Same(cr,NativeChipBake.Crest(t,1.2f)),t+" crest drawn");Check(AlphaSum(cr,0,(int)(NativeChipBake.CrestRise*1.2f*.5f))>2,t+" crest rises over the edge");}
                if(l.Fins){var fin=NativeChipBake.Fin(t,1.2f);InRange(fin,t+" fin");Check(AlphaSum(fin,0,fin.H)>20,t+" fin drawn");}
            }
            Check(!NativeChipBake.HasCrest("common")&&!NativeChipBake.HasCrest("epic")&&NativeChipBake.HasCrest("legendary")&&NativeChipBake.HasCrest("siren"),"crests where the design puts them");
            // The mythic's black chrome is darker than common's steel; the legendary shell has its gems (more lit pixels in the cuts).
            Check(Lum(NativeChipBake.Shell("mythic",1.2f,false))<Lum(NativeChipBake.Shell("common",1.2f,false)),"mythic's chrome darker than steel");
            foreach(float sc in new[]{.8f,1.2f,1.6f})
            {
                var tr=NativeChipBake.Traces(24,sc);InRange(tr,"traces");Check(Same(tr,NativeChipBake.Traces(24,sc))&&AlphaSum(tr,0,tr.H)>50,"traces deterministic and drawn");
                var bd=NativeChipBake.Board(sc);InRange(bd,"board");Check(Same(bd,NativeChipBake.Board(sc))&&AlphaSum(bd,0,bd.H)>bd.W*6,"board deterministic and drawn");
                bool seam=true;for(int y=0;y<bd.H;y++)seam&=Math.Abs(Px(bd,0,y).W-Px(bd,bd.W-1,y).W)<.35f;for(int x=0;x<bd.W;x++)seam&=Math.Abs(Px(bd,x,0).W-Px(bd,x,bd.H-1).W)<.35f;Check(seam,"board tiles without a seam");
                var notch=NativeChipBake.Notch(sc);InRange(notch,"notch");Check(Same(notch,NativeChipBake.Notch(sc))&&AlphaSum(notch,0,notch.H)>1&&Px(notch,0,0).W<.05f,"notch deterministic, a cut with clear space round it");
                var pad=NativeChipBake.Pad(sc);InRange(pad,"pad");Check(Px(pad,pad.W/2,pad.H/2).W>.99f&&Px(pad,0,pad.H/2).W<.05f,"a pad and its gap");
                foreach(bool f in new[]{true,false}){var ring=NativeChipBake.Ring(24,sc,f);InRange(ring,"ring");Check(AlphaSum(ring,0,ring.H/2)>1||AlphaSum(ring,ring.H/2,ring.H)>1,"ring half drawn");}
            }
        }
        static float Lum(NativeChipBake.Image im){double s=0;int n=0;for(int y=0;y<im.H;y++)for(int x=0;x<im.W;x++){var c=Px(im,x,y);if(c.W<.5f)continue;s+=.2126*c.X+.7152*c.Y+.0722*c.Z;n++;}return (float)(s/Math.Max(1,n));}

        // ---- the runtime: every layer and hook drawn, every chip site on the cartridge, no per-frame allocation ----
        static string Body(string src,string signature)
        {
            int at=src.IndexOf(signature,StringComparison.Ordinal);if(at<0)throw new Exception("Chip cartridge: missing "+signature);int open=src.IndexOf('{',at),depth=0;
            for(int i=open;i<src.Length;i++){if(src[i]=='{')depth++;else if(src[i]=='}'&&--depth==0)return src.Substring(open,i-open+1);}
            throw new Exception("Chip cartridge: unbalanced "+signature);
        }
        static void Wiring(Func<string,string> R)
        {
            var card=R("Assets/ChipArena/Runtime/NativeChipCard.cs");var art=R("Assets/ChipArena/Runtime/NativeChipArt.cs");var draw=Body(card,"public static void Draw(Rect r,ContentChip c,int copies,ChipSize s,ChipLook look)");
            foreach(var layer in new[]{"NativeChipArt.Shell(","NativeChipArt.Hardware(","NativeChipArt.Board(","NativeChipArt.Core(","NativeChipArt.Connector(","NativeChipArt.Traces(","NativeChipArt.Packets(","NativeChipArt.Crest(","NativeChipArt.Fins(","NativeChipArt.Body(","NativeChipArt.TitlePlate(","NativeChipArt.Embers(","NativeChipArt.Shards(","NativeChipArt.Aura(","NativeChipArt.Holographic(","NativeChipArt.Sweep("})
                Check(draw.Contains(layer),"the card draws "+layer);
            foreach(var hook in new[]{"NativeChipArt.HoloHalo(","NativeChipArt.HoloEdge(","NativeChipArt.HoloPads(","NativeChipArt.HoloScan(","NativeChipArt.HoloBrackets(","NativeChipArt.HoloGhost(","look.Recede","look.Focus","look.Chosen","look.Badge","look.Charge","look.Unbuilt","look.Bright","look.Calm","look.Plain","look.SweepAt","NativeHoloScan.DieCharge("})
                Check(draw.Contains(hook),"the card keeps "+hook);
            Check(draw.Contains("Cart.Sweep(")&&draw.Contains("Cart.Sheen(")&&draw.Contains("Cart.CoreLight(")&&draw.Contains("Cart.FrameOffset(")&&draw.Contains("Cart.FrameAlpha("),"the card runs the design's curves");
            Check(card.Contains("public static Color Rarity(string r)=>U.Hex(Cart.Rarity(r));"),"the card's rarity colour is the scheme's");
            // Allocation: the draw builds no string, array, list or lambda per frame (layouts and strings are cached).
            foreach(var bad in new[]{"new[]","new List<","new float[","new Color[","ToString(","ToList(",".Select(",".Where(","string.Format","+\"","\"+","=>{"})Check(!draw.Contains(bad),"the card draw allocates nothing ("+bad+")");
            Check(card.Contains("layouts.TryGetValue(key,out var cached)")&&card.Contains("counts.TryGetValue(key,out var text)"),"layouts and counts cached");
            foreach(var fn in new[]{"public static void Shell(","public static void Body(","public static void Board(","public static void Hardware(","public static void Traces(","public static void Packets(","public static void Connector(","public static void Fins(","public static void Crest(","public static void Core(","public static void Embers(","public static void Shards(","public static void Badge(","public static float Pips(","public static void HoloEdge(","public static void HoloPads(","public static void TitlePlate("})
            {var b=Body(art,fn);foreach(var bad in new[]{"new[]","new List<","new float[","new Color[","ToString(","=>{"})Check(!b.Contains(bad),fn+" allocates nothing ("+bad+")");}
            // The bakes run only on a cache miss (Baked); no draw calls a bake directly.
            var bakeCall=new Regex("NativeChipBake\\.(Shell|Core|Traces|Pad|Fin|Ring|Crest|Badge)\\(");
            Check(art.Contains("baked.TryGetValue(key,out var t)&&t!=null")&&bakeCall.Matches(art).Count==bakeCall.Matches(Body(art,"private static NativeChipBake.Image Raster(")).Count&&Body(art,"private static Texture2D Baked(").Contains("Raster("),"bakes are cached, never per frame");
            // Warming: the layers rasterised on a worker once a match is on screen, at the same size keys the draws use.
            var warm=Body(art,"public static void Warm()");Check(warm.Contains("Task.Run(")&&warm.Contains("CoreKey(54)")&&warm.Contains("CoreKey(84)")&&warm.Contains("scale==warmedScale")&&R("Assets/ChipArena/Runtime/ConversionGame.cs").Contains("NativeChipArt.Warm();"),"the layers warm on a worker before the first draft");
            Check(Body(art,"public static void Core(").Contains("CoreKey(core.width)")&&Body(art,"public static void Traces(").Contains("TraceStep(coreR)")&&Body(art,"public static void Crest(").Contains("CrestScale(r.width)"),"the draws use the warmed size keys");
            // The sites: the draft tray, fly, shatter and spotlight; the Siren reveal; the chip lists; the tag glyph; the hologram.
            var draft=R("Assets/ChipArena/Runtime/NativeDraftScreen.cs");Check(draft.Contains("Unbuilt=1-reveal.Build")&&draft.Contains("NativeChipArt.HoloLock(")&&draft.Contains("NativeChipCartridge.Of(chip.rarity).MetalMid")&&draft.Contains("NativeChipArt.Package(r,size.Radius"),"the draft tray assembles, locks and shatters the cartridge");
            Check(R("Assets/ChipArena/Runtime/NativeSirenReveal.cs").Contains("look.Unbuilt=1-Mathf.Clamp01(since/NativeChipCard.RevealSeconds(\"siren\"))"),"the Siren reveal assembles its chip");
            Check(R("Assets/ChipArena/Runtime/NativeMatchOverlays.cs").Contains("NativeChipArt.Badge(new Rect(b.x+5"),"the result badges show the cartridge");
            Check(R("Assets/ChipArena/Runtime/NativeDevPanel.cs").Contains("NativeChipArt.Badge(new Rect(r.x+8"),"the DEV chip list shows the cartridge");
            Check(R("Assets/ChipArena/Runtime/NativeChipSelectHud.cs").Contains("a tiny cartridge"),"the selecting tag's glyph is the cartridge");
            var holo=R("Assets/ChipArena/Resources/NativeChipSelect.shader");Check(holo.Contains("cartridge")&&!holo.Contains("a die with pins"),"the wrist hologram's cards are cartridges");
            var entrance=Body(card,"public static Reveal Entrance(string tier,float t)");Check(entrance.Contains("v.Build=k")&&entrance.Contains("Build=1")&&!entrance.Contains("new[]"),"the reveal's assembly runs on the entrance's own timing, allocation-free");
        }
    }
}
#endif
