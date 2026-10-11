#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Brickfall's destruction VFX kit (Core/NativeStructureFx.cs, Runtime/NativeStructureFxPresentation.cs and
    // NativeStructureAudio.cs; docs/arenas/ARENA_PROPOSAL.md §3.9, §3.10, §3.11, §5.4, §5.5): material classification and the
    // seed rule; the dust wall's timing, radius, height, count, cap and near-camera thinning; material bursts per kind;
    // telegraphs never reduced and claiming their billboards first; purity in age (a late-adopted record, a second store and
    // the killcam draw the same frame) and allocation-free frames; fresh-to-dusty rubble; the 256-decal ring; shake by
    // distance; window strobe and sag; the cue routing and the synthesized clips; the preview pancake; the per-map kill-effect
    // floor; and scans of the runtime hooks (Break replaces the spark burst, RenderGlow, reset, shake, sandbox, gib floor,
    // the marked structure-event hook lines, no Light or AudioSource).
    internal static class StructureFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Structure fx: "+what);count++;}
        private static bool Near(double a,double b,double tol=1e-4)=>Math.Abs(a-b)<=tol;
        private static readonly V3 Eye=new V3(0,1.6f,-30);
        public static void Run(string root)
        {
            Rules();Wall();Bursts();Telegraph();Purity();Rubble();Marks();Shake();Sounds(root);Preview();KillFloor();Hooks(root);
            Console.WriteLine("PASS structure fx ("+count+" checks: material classes and seed rule; dust wall timing, radius 4-14 m, height, 24-40 billboards, 120 cap, near thinning; five material bursts; telegraph never reduced and first in the cap; pure in age, killcam-equal and allocation-free; fresh-to-dusty rubble; 256-decal ring; shake by distance; window strobe and sag; cue routing and synthesized clips; preview pancake; per-map kill floor; runtime hooks)");
        }
        private static void Rules()
        {
            Check(NativeStructureFx.Classify("wood")==NativeStructureMaterial.Timber&&NativeStructureFx.Classify("stone")==NativeStructureMaterial.Concrete&&NativeStructureFx.Classify("metal")==NativeStructureMaterial.Metal&&NativeStructureFx.Classify("foundation")==NativeStructureMaterial.Concrete,"today's materials map to timber, concrete, metal");
            Check(NativeStructureFx.Classify("brick")==NativeStructureMaterial.Masonry&&NativeStructureFx.Classify("brick-buff")==NativeStructureMaterial.Masonry&&NativeStructureFx.Classify("terracotta")==NativeStructureMaterial.Masonry&&NativeStructureFx.Classify("glass")==NativeStructureMaterial.Glass&&NativeStructureFx.Classify("steel-green")==NativeStructureMaterial.Metal&&NativeStructureFx.Classify(null)==NativeStructureMaterial.Concrete,"Brickfall looks map to their kinds");
            uint a=NativeStructureFx.Seed("brickfall","hotel-nw",NativeStructureFx.RoundIndex(1,1));
            Check(a==NativeStructureFx.Seed("brickfall","hotel-nw",65)&&a!=NativeStructureFx.Seed("brickfall","hotel-ne",65)&&a!=NativeStructureFx.Seed("brickfall","hotel-nw",66)&&a!=NativeStructureFx.Seed("moonlit","hotel-nw",65)&&a!=0,"seed = hash(mapId, segment, round index), distinct per input");
            Check(NativeStructureFx.MapMarks("brickfall")&&!NativeStructureFx.MapMarks("moonlit")&&NativeStructureFx.WindFor("brickfall").x>0&&NativeStructureFx.WindFor("foundry").x==0,"Brickfall-only decals and drift stay dormant elsewhere");
            Check(NativeStructureFx.Reduced(1)==1&&NativeStructureFx.Reduced(.3f)==.5f,"reduced effects halve debris and atmosphere");
        }
        private static void Wall()
        {
            Check(Near(NativeStructureFx.WallRadius(0),4)&&Near(NativeStructureFx.WallRadius(1.6f),14)&&Near(NativeStructureFx.WallRadius(5),14)&&NativeStructureFx.WallRadius(.8f)>NativeStructureFx.WallRadius(.4f),"wall radius 4 -> 14 m in 1.6 s");
            Check(Near(NativeStructureFx.WallOpacity(.5f),.5)&&Near(NativeStructureFx.WallOpacity(1.49f),.5,1e-3)&&Near(NativeStructureFx.WallOpacity(3),.2)&&Near(NativeStructureFx.WallOpacity(7),0)&&NativeStructureFx.WallOpacity(2.25f)<.5f&&NativeStructureFx.WallOpacity(2.25f)>.2f&&NativeStructureFx.WallOpacity(5)<.2f&&NativeStructureFx.WallOpacity(-1)==0,"opacity .5 for 1.5 s, .2 by 3 s, gone by 7 s");
            Check(NativeStructureFx.WallTop(1.6f,1)<=8.0001f&&Near(NativeStructureFx.WallTop(1.6f,1),8)&&NativeStructureFx.WallTop(.1f,1)<4,"up to 8 m tall");
            Check(NativeStructureFx.WallCount(0,1)==24&&NativeStructureFx.WallCount(1,1)==40&&NativeStructureFx.WallCount(1,.3f)==20,"24-40 billboards (half reduced)");
            Check(NativeStructureFx.DebrisCount(0,1)==40&&NativeStructureFx.DebrisCount(1,1)==120&&NativeStructureFx.DebrisCount(1,.3f)==60,"40-120 debris voxels (half reduced)");
            Check(Near(NativeStructureFx.NearThin(.5f),.2)&&Near(NativeStructureFx.NearThin(3),1)&&NativeStructureFx.NearThin(2)<1&&NativeStructureFx.NearThin(2)>.2f,"thinned within 3 m of the camera");
            // A landing: at 1 s the wall's 40 billboards ring the landing at 9-15 m; six landings stay within the 120 cap.
            var fx=new NativeStructureFx();fx.Land(new V3(0,1,0),7.2f,1,NativeStructureMaterial.Masonry,0,7,0,false);fx.Frame(1,Eye,1);
            int ring=0;for(int i=0;i<fx.PuffCount;i++){var p=fx.Puffs[i];float r=(float)Math.Sqrt(p.Center.x*p.Center.x+p.Center.z*p.Center.z);if(r>9&&r<16)ring++;Check(p.Alpha<=.5f+1e-4f,"no billboard above .5 opacity");}
            Check(ring==NativeStructureFx.WallCount(1,1)-NativeStructureFx.WallCore&&fx.CubeCount==NativeStructureFx.DebrisCount(1,1),"wall ring and debris at full quality ("+ring+", "+fx.CubeCount+")");
            for(int i=0;i<6;i++)fx.Land(new V3(i*3,1,0),7.2f,1,NativeStructureMaterial.Concrete,0,(uint)(100+i),.1f*i,false);fx.Frame(1,Eye,1);
            Check(fx.PuffCount==NativeStructureFx.PuffCap&&fx.LiveRecords==NativeStructureFx.EventSlots,"at most 120 live billboards map-wide, six event slots");
            fx.Frame(20,Eye,1);Check(fx.PuffCount==0&&fx.CubeCount==0&&fx.LiveRecords==0,"everything gone after 15 s");
        }
        private static void Bursts()
        {
            foreach(NativeStructureMaterial m in Enum.GetValues(typeof(NativeStructureMaterial)))
            {
                var fx=new NativeStructureFx();fx.Break(m,0,new V3(0,1,0),new V3(1,1,1),0,11,0,false);fx.Frame(.15f,Eye,1);int cubes=fx.CubeCount,puffs=fx.PuffCount,lines=fx.LineCount,glows=fx.GlowCount;
                NativeStructureFx.BurstCounts(m,NativeStructureFx.BurstScale(new V3(1,1,1)),out int pieces,out int grit,out int puffN,out int sparks);
                Check(cubes>=pieces&&cubes<=pieces+grit,m+" burst draws its pieces ("+cubes+")");
                bool dusty=m==NativeStructureMaterial.Masonry||m==NativeStructureMaterial.Concrete||m==NativeStructureMaterial.Timber;Check(dusty?puffs>0:puffs==0,m+" puff only for masonry, concrete and timber");
                if(m==NativeStructureMaterial.Metal){Check(lines==sparks&&fx.Lines.Take(lines).All(l=>l.Additive)&&glows==1,"metal: spark streaks and the orange core");fx.Frame(.05f,Eye,1);Check(fx.GlowCount==2,"metal: the white clank flash for .14 s");}
                if(m==NativeStructureMaterial.Timber){float longest=0;for(int i=0;i<pieces;i++)longest=Math.Max(longest,fx.Cubes[i].Size.z/fx.Cubes[i].Size.x);Check(longest>5,"timber throws splinters");}
                if(m==NativeStructureMaterial.Masonry){int brick=0;for(int i=0;i<pieces;i++){var c=fx.Cubes[i];if(c.R>c.B+.15f)brick++;}Check(brick>=pieces*.7,"masonry throws brick halves");}
                fx.Frame(.15f,Eye,.3f);Check(fx.CubeCount<cubes,m+" reduced draws fewer pieces");
                fx.Frame(NativeStructureFx.BurstLife,Eye,1);Check(fx.CubeCount==0&&fx.LiveBursts==0,m+" gone after its life");
            }
            var g=new NativeStructureFx();g.Break(NativeStructureMaterial.Glass,0,new V3(0,1,0),new V3(1,1,1),0,12,0,false);int glints=0;for(float t=0;t<1.5f;t+=.05f){g.Frame(t,Eye,1);glints+=g.GlowCount;}Check(glints>0,"glass shards glint");
            var b=new NativeStructureFx();b.Break(NativeStructureMaterial.Masonry,0x9c4a30,new V3(0,1,0),new V3(1,1,1),0,13,0,true);Check(b.LiveMarks==2,"a Brickfall break leaves a chip and a crack mark");
            var big=new NativeStructureFx();for(int i=0;i<40;i++)big.Break(NativeStructureMaterial.Masonry,0,new V3(i,1,0),new V3(4,4,.5f),0,(uint)i,i*.01f,false);big.Frame(.5f,Eye,1);Check(big.LiveBursts==NativeStructureFx.BurstSlots&&big.CubeCount<=NativeStructureFx.CubeCap,"bursts are pooled and capped");
        }
        private static void Telegraph()
        {
            var fx=new NativeStructureFx();fx.Stress(new V3(0,5.4f,0),7.2f,1.2f,new V3(),new V3(),0,21,0);fx.Frame(.8f,Eye,1);int puffs=fx.PuffCount,cubes=fx.CubeCount,flats=fx.FlatCount;
            fx.Frame(.8f,Eye,.3f);Check(fx.PuffCount==puffs&&fx.CubeCount==cubes&&fx.FlatCount==flats&&puffs>0&&cubes>0&&flats==5,"the telegraph is never reduced (footprint, grit columns, trickles)");
            // Six landings saturate the cap; a later telegraph still gets its billboards first.
            for(int i=0;i<5;i++)fx.Land(new V3(i*2,1,0),7.2f,1,NativeStructureMaterial.Masonry,0,(uint)(30+i),.5f,false);fx.Frame(.8f,Eye,1);Check(fx.PuffCount==NativeStructureFx.PuffCap&&fx.FlatCount>=5,"telegraph billboards claim the cap first");
            var alone=new NativeStructureFx();alone.Stress(new V3(0,5.4f,0),7.2f,1.2f,new V3(),new V3(),0,21,0);alone.Frame(.8f,Eye,1);bool first=alone.PuffCount==puffs;for(int i=0;i<puffs&&first;i++)first&=fx.Puffs[i].Equals(alone.Puffs[i]);Check(first,"the first billboards are the telegraph's");
            Check(fx.StressAt(new V3(0,3,0),.5f,out float age,out float tel)&&Near(age,.5f)&&Near(tel,1.2f)&&!fx.StressAt(new V3(30,3,0),.5f,out _,out _),"stress query inside the crush box only");
            Check(NativeStructureFx.WindowStrobe(.01f,1.2f)==1&&NativeStructureFx.WindowStrobe(.07f,1.2f)<.2f&&NativeStructureFx.WindowStrobe(.13f,1.2f)==1&&NativeStructureFx.WindowStrobe(1.3f,1.2f)==0&&fx.WindowAt(new V3(40,0,0),.5f)==1,"windows strobe at 8 Hz, then go black");
            Check(NativeStructureFx.Sag(0,1.2f)==0&&Near(NativeStructureFx.Sag(1.2f,1.2f),1)&&NativeStructureFx.SagDegrees<=1&&NativeStructureFx.SagMetres<=.08f,"sag at most 1 degree or 8 cm");
            Check(NativeStructureFx.LampStrobe(0)==1&&NativeStructureFx.LampStrobe(.13f)==0,"band lamp strobes red");
            NativeStructureFx.DefaultBox(new V3(0,5.4f,0),7.2f,0,out V3 bc,out V3 bh);Check(Near(bc.y-bh.y,0)&&bh.x>0&&bh.z>0,"default crush box stands on the floor");
        }
        private static void Purity()
        {
            NativeStructureFx Make(){var fx=new NativeStructureFx();fx.Wind=NativeStructureFx.WindFor("brickfall");fx.Stress(new V3(0,5,0),6,1.2f,new V3(),new V3(),0,41,10);fx.Fall(new V3(0,5,0),6,new V3(0,0,1),60,NativeStructureMaterial.Masonry,0,41,11.2f);fx.Land(new V3(0,1,0),5,.85f,NativeStructureMaterial.Masonry,0,41,12.2f);fx.Break(NativeStructureMaterial.Metal,0,new V3(3,1,0),new V3(1,1,1),0,42,12.5f,true);return fx;}
            var a=Make();var b=Make();
            // b skips frames (a guest or the killcam joins late): the same time draws the same lists.
            foreach(float t in new[]{10.5f,11.5f,12.4f,12.6f,13.1f})a.Frame(t,Eye,1);b.Frame(13.3f,Eye,1);a.Frame(13.3f,Eye,1);
            Check(Same(a,b),"pure in age: frame history does not matter (killcam parity)");
            var c=new NativeStructureFx();c.Land(new V3(0,1,0),5,.85f,NativeStructureMaterial.Masonry,0,41,12.2f);c.Frame(14,Eye,1);var d=new NativeStructureFx();d.Land(new V3(0,1,0),5,.85f,NativeStructureMaterial.Masonry,0,41,12.2f);d.Frame(14,Eye,1);Check(Same(c,d),"two stores agree");
            var e=new NativeStructureFx();e.Land(new V3(0,1,0),5,.85f,NativeStructureMaterial.Masonry,0,41,20);e.Frame(19.9f,Eye,1);Check(e.PuffCount==0&&e.CubeCount==0&&e.LiveRecords==1,"a record born in the future waits (preview schedule)");
            var f=Make();for(int i=0;i<30;i++)f.Frame(10+i*.1f,Eye,1);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++){f.Frame(10+i*.03f,Eye,i%2==0?1:.3f);f.ShakeOffset(10+i*.03f,Eye);f.FreshnessAt(new V3(),12+i*.03f);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"steady frames allocate nothing");
        }
        private static bool Same(NativeStructureFx a,NativeStructureFx b)
        {
            if(a.CubeCount!=b.CubeCount||a.PuffCount!=b.PuffCount||a.FlatCount!=b.FlatCount||a.LineCount!=b.LineCount||a.GlowCount!=b.GlowCount)return false;
            for(int i=0;i<a.CubeCount;i++)if(!a.Cubes[i].Equals(b.Cubes[i]))return false;for(int i=0;i<a.PuffCount;i++)if(!a.Puffs[i].Equals(b.Puffs[i]))return false;
            for(int i=0;i<a.FlatCount;i++)if(!a.Flats[i].Equals(b.Flats[i]))return false;for(int i=0;i<a.LineCount;i++)if(!a.Lines[i].Equals(b.Lines[i]))return false;for(int i=0;i<a.GlowCount;i++)if(!a.Glows[i].Equals(b.Glows[i]))return false;return true;
        }
        private static void Rubble()
        {
            Check(NativeStructureFx.Freshness(0)==1&&NativeStructureFx.Freshness(10)==0&&NativeStructureFx.Freshness(5)>0&&NativeStructureFx.Freshness(5)<1,"rubble fresh to dusty over 10 s");
            var fx=new NativeStructureFx();fx.Land(new V3(0,1,0),5,.85f,NativeStructureMaterial.Masonry,0,51,0,false);
            NativeStructureFx.Rgb(NativeStructureFx.DustCoat,out float cr,out float cg,out float cb);
            double Gap(float t){fx.Frame(t,Eye,1);double s=0;for(int i=0;i<fx.CubeCount;i++){var c=fx.Cubes[i];s+=Math.Abs(c.R-cr)+Math.Abs(c.G-cg)+Math.Abs(c.B-cb);}return s/Math.Max(1,fx.CubeCount);}
            Check(Gap(12)<Gap(2)*.5,"debris faces fade to the dust coat");
            Check(fx.FreshnessAt(new V3(1,0,1),1)>.9f&&fx.FreshnessAt(new V3(1,0,1),11)==0&&fx.FreshnessAt(new V3(30,0,0),1)==0,"freshness query for the dressing");
            int wisps=0;fx.Frame(8,Eye,1);for(int i=0;i<fx.PuffCount;i++)if(fx.Puffs[i].Height<5)wisps++;Check(wisps>0,"smoke wisps over the rubble after the wall");fx.Frame(14.9f,Eye,1);Check(fx.CubeCount>0,"rubble voxels last 15 s");
            float biggest=0;fx.Frame(3,Eye,1);for(int i=0;i<fx.CubeCount;i++)biggest=Math.Max(biggest,Math.Max(fx.Cubes[i].Size.x,Math.Max(fx.Cubes[i].Size.y,fx.Cubes[i].Size.z)));Check(biggest<=.42f,"loose debris stays below cover size ("+biggest+" m)");
        }
        private static void Marks()
        {
            var fx=new NativeStructureFx();for(int i=0;i<300;i++)fx.AddMark((NativeStructureMark)(i%4),new V3(i*.1f,0,0),new V3(0,1,0),1,(uint)i,0);
            Check(fx.LiveMarks==NativeStructureFx.DecalSlots,"decal ring buffer of 256");fx.Frame(1,Eye,1);Check(fx.FlatCount>=256&&fx.FlatCount<=NativeStructureFx.FlatCap,"decals draw within the flat cap");
            bool newest=false;for(int i=0;i<fx.FlatCount;i++)newest|=fx.Flats[i].Center.x>29.8f;bool oldest=false;for(int i=0;i<fx.FlatCount;i++)oldest|=fx.Flats[i].Center.x<1&&fx.Flats[i].Center.x>-1;Check(newest&&!oldest,"the oldest marks are overwritten");
            var w=new NativeStructureFx();w.AddMark(NativeStructureMark.Scorch,new V3(0,2,0),new V3(1,0,0),1,1,0);w.Frame(1,Eye,1);Check(w.FlatCount==2&&Math.Abs(w.Flats[0].Center.x)>.01f&&Math.Abs(w.Flats[0].U.x)<1e-4f,"a wall mark lies on its wall");
            var l=new NativeStructureFx();l.Land(new V3(0,1,0),5,1,NativeStructureMaterial.Masonry,0,1,0);Check(l.LiveMarks==9,"a landing leaves a dust stain and eight cracks");
        }
        private static void Shake()
        {
            Check(NativeStructureShake.Falloff(25)==0&&NativeStructureShake.Falloff(30)==0&&NativeStructureShake.Falloff(0)==1&&NativeStructureShake.Falloff(10)<NativeStructureShake.Falloff(5),"shake only within 25 m, falling with distance");
            Check(NativeStructureShake.Peak(NativeStructureCue.Fall,100)>NativeStructureShake.Peak(NativeStructureCue.Fall,10)&&NativeStructureShake.Peak(NativeStructureCue.Land,1)>NativeStructureShake.Peak(NativeStructureCue.Land,0)&&NativeStructureShake.Peak(NativeStructureCue.Land,1)<=NativeStructureShake.MaxOffset,"scaled by mass and energy");
            var fx=new NativeStructureFx();fx.Land(new V3(0,1,0),5,1,NativeStructureMaterial.Masonry,0,61,0,false);float near=0,far=0,max=0;
            for(float t=.01f;t<1.2f;t+=.02f){var o=fx.ShakeOffset(t,new V3(3,1.6f,0));float l=(float)Math.Sqrt(o.x*o.x+o.y*o.y+o.z*o.z);near=Math.Max(near,l);max=Math.Max(max,l);var p=fx.ShakeOffset(t,new V3(40,1.6f,0));far=Math.Max(far,(float)Math.Sqrt(p.x*p.x+p.y*p.y+p.z*p.z));}
            Check(near>.02f&&far==0&&max<=NativeStructureShake.MaxOffset+1e-5f,"a landing shakes nearby, not 40 m away ("+near+")");
            var o2=fx.ShakeOffset(5,new V3(3,1.6f,0));Check(o2.x==0&&o2.y==0&&o2.z==0,"the shake ends");
            Check(NativeStructureShake.Envelope(true,.1f,1)<NativeStructureShake.Envelope(true,.9f,1)&&NativeStructureShake.Envelope(false,.1f,1)>NativeStructureShake.Envelope(false,.9f,1),"the stress tremor rises, impacts decay");
        }
        private static void Sounds(string root)
        {
            var o=new NativeStructureLayer[NativeStructureSounds.MaxLayers];
            foreach(NativeStructureSound cue in Enum.GetValues(typeof(NativeStructureSound)))foreach(NativeStructureMaterial m in Enum.GetValues(typeof(NativeStructureMaterial)))
            {
                int n=NativeStructureSounds.Plan(cue,m,.8f,o);Check(n>0&&n<=NativeStructureSounds.MaxLayers,cue+" "+m+" plans 1-6 layers");
                for(int i=0;i<n;i++)
                {
                    string clip=o[i].Clip;bool synth=clip.StartsWith(NativeStructureSounds.Prefix,StringComparison.Ordinal);string file=synth?"Assets/ChipArena/Resources/StructureAudio/"+clip.Substring(NativeStructureSounds.Prefix.Length)+".wav":"Assets/Conversion/Resources/Audio/audio/"+clip+".wav";
                    Check(File.Exists(Path.Combine(root,file))&&(!synth||NativeStructureSounds.Synthesized.Contains(clip.Substring(NativeStructureSounds.Prefix.Length))),cue+" layer "+clip+" exists");
                }
            }
            int c=NativeStructureSounds.Plan(NativeStructureSound.Collapse,NativeStructureMaterial.Masonry,1,o);var clips=o.Take(c).Select(l=>l.Clip).ToArray();
            Check(clips.SequenceEqual(new[]{"structure/snap","structure/boom","structure/rumble","structure/glass","structure/debris"}),"collapse: snap, sub boom, rumble, glass, debris tail");
            int k=NativeStructureSounds.Plan(NativeStructureSound.Chimney,NativeStructureMaterial.Masonry,1,o);var thuds=o.Take(k).Where(l=>l.Clip=="structure/thud").Select(l=>l.Delay).ToArray();
            Check(o[0].Clip=="structure/creak"&&o[1].Clip=="dash"&&thuds.Length==4&&thuds.SequenceEqual(thuds.OrderBy(d=>d))&&thuds[0]>o[1].Delay,"chimney: creak, whoosh, four thuds in order");
            NativeStructureSounds.Plan(NativeStructureSound.Land,NativeStructureMaterial.Timber,1,o);Check(o[1].Clip=="wood","timber lands with wood");NativeStructureSounds.Plan(NativeStructureSound.Land,NativeStructureMaterial.Metal,1,o);Check(o[1].Clip=="metal","metal lands with metal");
            var hi=new NativeStructureLayer{Gain=1,High=true};var lo=new NativeStructureLayer{Gain=1};Check(NativeStructureSounds.Gain(hi,20)<NativeStructureSounds.Gain(lo,20)&&Near(NativeStructureSounds.Gain(hi,2),NativeStructureSounds.Gain(lo,2)),"distance dulls the high layers first");
            Check(NativeStructureSounds.ForEvent(NativeStructureCue.Stress,"stack-n",out var s1)&&s1==NativeStructureSound.Chimney&&!NativeStructureSounds.ForEvent(NativeStructureCue.Fall,"stack-s",out _)&&NativeStructureSounds.ForEvent(NativeStructureCue.Fall,"hotel-nw",out var s2)&&s2==NativeStructureSound.Collapse&&NativeStructureSounds.ForEvent(NativeStructureCue.Land,"hotel-nw",out var s3)&&s3==NativeStructureSound.Land,"one cue per segment event; the chimney plays its own");
            Check(NativeStructureSounds.Voices==6,"at most six rubble voices");
            int bank=Directory.EnumerateFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio"),"*",SearchOption.AllDirectories).Count(p=>p.EndsWith(".mp3")||p.EndsWith(".wav"));Check(bank==83,"the 83-clip source bank is untouched ("+bank+")");
            foreach(var name in NativeStructureSounds.Synthesized){var f=Path.Combine(root,"Assets/ChipArena/Resources/StructureAudio/"+name+".wav");Check(File.Exists(f)&&File.Exists(f+".meta")&&new FileInfo(f).Length<1024*1024,name+" clip and meta");}
        }
        private static void Preview()
        {
            float floor=0;for(int k=0;k<NativeStructurePreview.Storeys;k++){Check(Near(NativeStructurePreview.SlabY(k,NativeStructurePreview.Telegraph,floor),NativeStructurePreview.Storey*(k+1)),"slab "+k+" holds through the telegraph");Check(NativeStructurePreview.SlabY(k,NativeStructurePreview.LandAt+.01f,floor)<1.6f,"slab "+k+" has landed when the rubble swaps in");}
            Check(NativeStructurePreview.SlabY(2,NativeStructurePreview.Telegraph+.2f,floor)<NativeStructurePreview.SlabY(2,NativeStructurePreview.Telegraph,floor)&&Near(NativeStructurePreview.SlabY(0,NativeStructurePreview.Telegraph+.2f,floor),NativeStructurePreview.Storey),"pancake order: roof first, floor 1 last");
            var fx=new NativeStructureFx();fx.Preview(new V3(0,0,0),NativeStructureMaterial.Masonry,0,71,0);fx.Frame(.5f,Eye,1);int standing=fx.CubeCount;fx.Frame(NativeStructurePreview.LandAt+1,Eye,1);Check(standing>15&&fx.CubeCount==20,"the stand-in stands, then swaps to an 18-block mound and two shards");
        }
        private static void KillFloor()
        {
            // A voxel burst over a 7.2 m upper floor rests on it; without a floor function the source's flat floor stays.
            var boxes=new double[16];boxes[0]=boxes[5]=boxes[10]=boxes[15]=1;boxes[12]=0;boxes[13]=8.1;boxes[14]=0;var colors=new[]{0xffffff};
            var flat=new NativeKillBurst(boxes,colors,1,5,0,8.1,0,0,0,0,0,0,1,1,280);var upper=new NativeKillBurst(boxes,colors,1,5,0,8.1,0,0,0,0,0,0,1,1,280,(x,y,z)=>7.2);var none=new NativeKillBurst(boxes,colors,1,5,0,8.1,0,0,0,0,0,0,1,1,280,(x,y,z)=>double.NaN);
            flat.Pose(0,3,out _,out double fy,out _,out _,out _,out _,out double fs);upper.Pose(0,3,out _,out double uy,out _,out _,out _,out _,out _);none.Pose(0,3,out _,out double ny,out _,out _,out _,out _,out _);
            Check(fy<.5&&uy>7.2&&uy<7.6&&Math.Abs(ny-fy)<1e-9,"gibs settle on the upper floor; NaN keeps the flat floor ("+uy+")");
            var ledge=new NativeKillBurst(boxes,colors,1,5,0,8.1,0,0,0,0,0,0,1,1,280,(x,y,z)=>Math.Abs(x)<1&&Math.Abs(z)<1?7.2:0);int fell=0,stayed=0;for(int i=0;i<ledge.Count;i++){ledge.Pose(i,3,out double lx,out double ly,out double lz,out _,out _,out _,out _);if(ly<1)fell++;else if(ly>7.2&&Math.Abs(lx)<1.2&&Math.Abs(lz)<1.2)stayed++;}Check(fell>0&&stayed>0,"voxels stay on the 2 m platform, those thrown past its ledge fall to the floor below ("+stayed+" / "+fell+")");
            Check(new NativeKillSpray(1,280,1,KillSprayProfile.Gibber,0).Floor==0&&new NativeKillSpray(1,280,1,KillSprayProfile.Gibber,0,7.2).Floor==7.2&&new NativeKillSpray(1,280,1,KillSprayProfile.Sever,3,7.2).Floor==3,"the Gibber's fountain splats on the floor under the body");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string fx=R("NativeStructureFxPresentation.cs"),audio=R("NativeStructureAudio.cs"),arena=R("ArenaPresentation.cs"),blast=R("NativeBlastPresentation.cs"),game=R("ConversionGame.cs"),sandbox=R("NativeSandbox.cs"),death=R("NativeDeathPresentation.cs"),kill=R("NativeKillEffectPresentation.cs");
            Check(arena.Contains("fieldTelegraphs?.Collect(arena,fields);StructureFx(arena,camera,fields);")&&arena.Contains("blasts?.Clear();ClearStructureFx();")&&arena.Contains("DisposeStructureFx();"),"RenderGlow draws the kit on the field clock; reset clears it; destroy disposes it");
            Check(blast.Contains("e.Kind==EventKind.Break&&MaterialBursts)continue;")&&fx.Contains("blasts.MaterialBursts=structureFx!=null;"),"material bursts replace the 18-spark break burst");
            Check(game.Contains("if(prefs.cameraShake&&!menu&&Spectator==null&&!cinematic.Directing)cameraView.transform.position+=presentation.StructureShake("),"structure shake only with camera shake on, in a live local view");
            Check(sandbox.Contains("StructureFxRows();")&&fx.Contains("if(arena?.Sandbox!=true||!account.Developer||Online||kit==null)return;"),"the preview is a dev-only offline Sandbox row");
            Check(death.Contains("NativeStructureFxPresentation.KillFloor(arena)")&&kill.Contains("quality,budget,floor);"),"kill effects take the per-map floor");
            foreach(var cue in new[]{"Stress","Fall","Land"})Check(fx.Contains("// case EventKind.Structure"+cue+":Structure(arena,NativeStructureCue."+cue+",e);break;")&&audio.Contains("if(e.Kind==EventKind.Structure"+cue+"){StructureEvent(NativeStructureCue."+cue+",e);return;}"),"the "+cue+" hook lines are ready");
            Check(!fx.Contains("AddComponent<Light>")&&!fx.Contains("AddComponent<AudioSource>")&&!audio.Contains("AddComponent<AudioSource>"),"no new Unity lights or audio sources");
            Check(!fx.Contains("new List<")&&!fx.Contains("ToArray()"),"no per-frame collections in the presentation");
        }
    }
}
#endif
