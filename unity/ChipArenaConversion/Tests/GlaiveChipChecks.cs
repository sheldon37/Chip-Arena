using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Glaive's ten chips (Core NativeGlaiveChips; docs/GLAIVE.md "The chips"; owner 2026-11-09: "create uniquely designed chips to
    /// benefit the glaive with effects on selection"): the set and its owner's lines, the emblems, the tier's look and content rows, who is
    /// offered them, the pick's cues and hold, every rule's number, the record's new share (valid, refused when broken), the shared paths the
    /// rules and the effects both read, the world effects (whole, deterministic, thinner on the low tier) and the wiring in the Runtime (read as
    /// text: CoreTests compiles Core only).
    /// </summary>
    internal static class GlaiveChipChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Glaive chips: "+what);count++;}
        private sealed class Recorder:ITechniqueFxSink
        {
            public int Draws;public bool Broken;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Draws++;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){V(head);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){V(at);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){V(tail);V(head);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){V(a);V(b);V(c);V(d);}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){V(at);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){V(at);}
            public void Drop(Vector3 at,Vector3 segment,Vector3 color,float opacity,float size,float u,float seed){V(at);}
            public void Tongue(bool view,Vector3 a,Vector3 b,Vector3 color,float width){V(a);V(b);}
        }
        private sealed class Scene:IKitFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
            public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+Vector3.UnitY*1.2f;return true;}
            public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int Viewer=>0;public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int WeaponColor(Actor a)=>0x6fe4ff;public int Shots=>0;public bool Shot(int index,out Vector3 at,out Vector3 velocity,out string fx,out int owner){at=velocity=default;fx=null;owner=0;return false;}
        }
        private static float Angle(Vector3 v,Vector3 c)=>(float)Math.Atan2(v.X-c.X,v.Z-c.Z);
        private static float Turned(float a0,float a1){float d=a1-a0;while(d>Math.PI)d-=(float)(2*Math.PI);while(d<-Math.PI)d+=(float)(2*Math.PI);return d;}
        public static void Run(string root)
        {
            count=0;var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});data.Validate();
            // ---- the set ----
            var chips=NativeGlaiveChips.Chips();
            Check(chips.Length==10&&chips.Select(c=>c.id).Distinct().Count()==10&&chips.All(c=>NativeGlaiveChips.Is(c.id)&&c.id.StartsWith(NativeGlaiveChips.Prefix)&&c.rarity==NativeGlaiveChips.Tier&&c.mechanic==c.id&&c.tags.Contains(NativeGlaiveChips.Tier)&&c.max>=1&&c.max<=2&&c.description.Length>40&&c.effects.Length==0),"ten chips of its own, each its own rule");
            Check(chips.Where(c=>c.max==2).Select(c=>c.id).SequenceEqual(new[]{NativeGlaiveChips.KeenEdge})&&NativeGlaiveChips.Mechanics.SequenceEqual(NativeGlaiveChips.Ids),"only Keen Edge stacks (twice); every one a mechanic");
            Check(!NativeGlaiveChips.Is(NativeGlaive.Throw)&&!NativeGlaiveChips.Is(NativeGlaive.Swing)&&!NativeGlaiveChips.Is(null)&&!NativeGlaiveChips.Is("dog-hammerspace"),"the kit's own moves and other sets are not its chips");
            string[] names={"Fourth Bounce","Keen Edge","Twin Orbit","Shatterpoint","Magnet Return","Quick Catch","Whirlwind","Long Arc","Skimming Stone","Crescent Guard"};
            string[] lines={"AND ANOTHER","RAZOR SHARP","DOUBLE TROUBLE","BREAK POINT","COME HERE","NICE CATCH","SPIN TO WIN","THE LONG WAY ROUND","SKIP, SKIP, SKIP","EDGE OF DEFENSE"};
            for(int i=0;i<10;i++){Check(chips[i].name==names[i]&&NativeGlaiveChips.Index(chips[i].id)==i,names[i]+" in its place");Check(NativeGlaiveChips.Confirm(chips[i].id)==lines[i],names[i]+": its line as it goes in");}
            Check(chips.Select(c=>NativeGlaiveChips.Confirm(c.id)).Distinct().Count()==10,"each its own line");
            // ---- the emblems ----
            Check(chips.All(c=>NativeGlaiveChips.Glyph(c.id)?.Contains("<path")==true)&&chips.Select(c=>NativeGlaiveChips.Glyph(c.id)).Distinct().Count()==10&&chips.All(c=>NativeChipIcons.Native(c.id)==c.id),"each its own emblem, drawn as itself");
            Check(NativeGlaiveChips.Glyph(NativeGlaive.Throw)==null&&NativeGlaiveChips.Glyph("dog-bigger-mallet")==null&&NativeGlaiveChips.Glyph(null)==null,"only its own emblems");
            var covers=new List<float[]>();
            foreach(var c in chips)
            {
                var glyph=NativeGlaiveChips.Glyph(c.id);var cover=NativeIconRaster.Coverage(NativeIconRaster.Parse(glyph,1,NativeIconRaster.Mode.Markup),48,NativeGlaiveChips.GlyphBox);float ink=cover.Sum()/cover.Length;covers.Add(cover);
                Check(ink>.06f&&ink<.7f,c.name+": its emblem draws ("+ink.ToString("0.00")+" of the box)");
            }
            for(int i=0;i<10;i++)for(int j=i+1;j<10;j++){float diff=0;for(int n=0;n<covers[i].Length;n++)diff+=Math.Abs(covers[i][n]-covers[j][n]);Check(diff/covers[i].Length>.05f,names[i]+" and "+names[j]+": their emblems differ");}
            // ---- the tier ----
            Check(NativeChipCartridge.Known(NativeGlaiveChips.Tier)&&NativeChipCartridge.Of(NativeGlaiveChips.Tier).Crest==NativeChipCartridge.Crest.Crescent&&NativeChipCartridge.Rarity(NativeGlaiveChips.Tier)==0x4fe8ff&&NativeChipBake.HasCrest(NativeGlaiveChips.Tier),"its tier's look on the 3D chip: the tri-crescent crest");
            Check(Array.IndexOf(NativeChipCartridge.Tiers,NativeGlaiveChips.Tier)==Array.IndexOf(NativeChipCartridge.Tiers,NativeDogChips.Tier)+1&&NativeChipRanks.RarityRank(NativeGlaiveChips.Tier)<NativeChipRanks.RarityRank("boss-siren"),"its rank after the Top Dog's, before the boss relics");
            Check(NativeChipCartridge.Tiers.Count(t=>NativeChipCartridge.Of(t).Crest==NativeChipCartridge.Crest.Crescent)==1,"the crescent crest is its alone");
            Check(data.rarities.Any(r=>r.id==NativeGlaiveChips.Tier&&r.weight==0&&r.leetWeight==0&&r.color==NativeGlaiveChips.Color)&&data.exclusiveOdds.Any(o=>o.spec==NativeGlaive.Spec&&o.guaranteed==2&&Math.Abs(o.chance-.85)<1e-9),"its rarity (never rolled) and its signature odds in the content");
            Check(data.chips.All(c=>!NativeGlaiveChips.Is(c.id)),"the rows come from the overlay");
            string before=data.contentRevision;NativeGlaiveChips.Apply(data);int n0=data.chips.Length;NativeGlaiveChips.Apply(data);Check(data.chips.Length==n0&&data.contentRevision!=before,"the overlay goes in once and marks the revision");data.Validate();
            // ---- who is offered them ----
            var held=new Dictionary<string,int>();var mine=NativeContentRules.Eligible(data,NativeGlaive.Spec,held);
            Check(mine.Count(c=>NativeGlaiveChips.Is(c.id))==10,"the Glaive can be offered all ten");
            Check(!mine.Any(c=>c.tags?.Contains("projectile")==true),"the Glaive is not offered the ordinary projectiles' chips (its ring is its own)");
            foreach(var spec in new[]{"pistol","sword","greatsword",NativeBombs.Spec,NativeTopDog.Spec})Check(!NativeContentRules.Eligible(data,spec,held).Any(c=>NativeGlaiveChips.Is(c.id)),"nobody else is offered them: "+spec);
            {var keen=new Dictionary<string,int>{[NativeGlaiveChips.KeenEdge]=1,[NativeGlaiveChips.TwinOrbit]=1};var after=NativeContentRules.Eligible(data,NativeGlaive.Spec,keen);Check(after.Any(c=>c.id==NativeGlaiveChips.KeenEdge)&&!after.Any(c=>c.id==NativeGlaiveChips.TwinOrbit),"Keen Edge can come again, a single one cannot");}
            var rng=new Random(53);int offers=0,ours=0;for(int i=0;i<400;i++){var picks=NativeContentRules.Offers(data,NativeGlaive.Spec,held,rng.NextDouble,3);offers+=picks.Count;ours+=picks.Count(NativeGlaiveChips.Is);}
            Check(ours>offers/3,"likely in its own draft ("+ours+" of "+offers+")");
            // ---- the pick ----
            foreach(var id in NativeGlaiveChips.Ids)Check(NativeSoundCues.DevInstall(id),"its install is its own: "+id);
            Check(NativeGlaiveChips.Picks.Length==10&&NativeGlaiveChips.Picks.All(l=>l.Length>=3&&l.All(c=>NativeToonSynth.Known(c.Id)&&c.At>=0&&c.At<3.2f&&c.Gain>0&&c.Gain<=1)&&l.Zip(l.Skip(1),(x,y)=>x.At<=y.At).All(o=>o)),"each pick has its own cues, in order, all synthesized");
            Check(NativeGlaiveChips.Picks.Select(l=>string.Join(",",l.Select(c=>c.Id))).Distinct().Count()==10,"no two picks sound the same");
            Check(NativeGlaiveChips.RevealSeconds>=NativeDraftPlant.DetailsAt+.55f+2.9f,"the draft holds for the whole animation");
            foreach(var id in new[]{"ring-chime","ring-skip"}){var smp=NativeToonSynth.Samples(id);float peak=smp.Max(Math.Abs);Check(smp.Length>1000&&smp.All(MathUtil.Finite)&&peak>.3f&&peak<=NativeToonSynth.Peak+1e-3f&&Math.Abs(smp[0])<.02f&&Math.Abs(smp[smp.Length-1])<.02f,id+" is synthesized whole");}
            // ---- the numbers (the owner's design) ----
            Check(NativeGlaiveChips.BounceMore==2&&NativeGlaiveChips.BounceLook==4&&NativeGlaiveChips.BounceKeep==.9f&&NativeGlaive.RicochetScale==.85f,"Fourth Bounce: 2 more ricochets, 4 m farther, 90% kept (not 85%)");
            Check(NativeGlaiveChips.KeenMax==2&&NativeGlaiveChips.KeenDamage==.1f&&NativeGlaiveChips.BleedDps==3&&NativeGlaiveChips.BleedFor==2,"Keen Edge: +10% a copy, a bleed of 3 a second for 2 s");
            Check(NativeGlaiveChips.TwinOut==1.2f,"Twin Orbit: 1.2 m farther out");
            Check(NativeGlaiveChips.ShatterWide==1.4f&&NativeGlaiveChips.Shards==6&&NativeGlaiveChips.ShardRange==5&&NativeGlaiveChips.ShardDamage==8,"Shatterpoint: 40% wider, 6 shards, 5 m, 8 each");
            Check(NativeGlaiveChips.MagnetDrag==3,"Magnet Return: 3 m");
            Check(NativeGlaiveChips.QuickCut==2.5f&&NativeGlaiveChips.QuickSpeed==1.25f&&NativeGlaiveChips.QuickFor==1.5f,"Quick Catch: 2.5 s off Q, +25% speed for 1.5 s");
            Check(NativeGlaiveChips.WhirlReach==3.5f&&NativeGlaiveChips.WhirlDrag==1.5f&&NativeGlaiveChips.WhirlDamage==1.2f&&NativeGlaiveChips.WhirlReach>NativeGlaive.SwingReach[2],"Whirlwind: 3.5 m, 1.5 m in, +20%");
            Check(NativeGlaiveChips.ArcReach==1.6f&&NativeGlaiveChips.ArcWide==1.4f&&NativeGlaiveChips.ArcDamage==1.25f,"Long Arc: 60% farther, 40% wider, +25%");
            Check(NativeGlaiveChips.SkimFarther==1.5f&&NativeGlaiveChips.SlickTime==2&&Math.Abs(NativeGlaiveChips.SlickSlow-.6f)<1e-6f,"Skimming Stone: 50% farther, a 2 s slick, 40% slower");
            Check(NativeGlaiveChips.GuardTaken==.75f,"Crescent Guard: 25% less damage");
            // ---- the record's new share ----
            var k=new NativeGlaiveKit{Ready=new float[NativeGlaive.Slots],Disc=NativeGlaive.Out,Bounces=NativeGlaive.Ricochets+NativeGlaiveChips.BounceMore,Keen=2,Twin=true,Shatter=true,LongArc=true,Guard=true,QuickUntil=12,SlickAt=10,SlickFrom=new V3(0,0,0),SlickTo=new V3(0,0,6.75f)};
            Check(k.Valid(),"a chip-laden record is valid (five ricochets)");var copy=k.ViewCopy();Check(copy.Twin&&copy.Keen==2&&copy.SlickTo.z==6.75f&&copy.QuickUntil==12,"its copy carries the chips' share");
            foreach(var broken in new Func<NativeGlaiveKit,NativeGlaiveKit>[]{x=>{x.Bounces=NativeGlaive.Ricochets+NativeGlaiveChips.BounceMore+1;return x;},x=>{x.Keen=3;return x;},x=>{x.Keen=-1;return x;},x=>{x.QuickUntil=float.NaN;return x;},x=>{x.SlickAt=float.PositiveInfinity;return x;},x=>{x.SlickFrom=new V3(float.NaN,0,0);return x;},x=>{x.SlickTo=new V3(0,1e9f,0);return x;}})
                Check(!broken(k.ViewCopy()).Valid(),"a broken share is refused");
            Check(new NativeGlaiveKit().QuickUntil<-50&&new NativeGlaiveKit().SlickAt<-50&&!NativeGlaiveChips.SlickOn(new NativeGlaiveKit().SlickAt,0),"a fresh record shows no slick and no rush");
            foreach(var kind in new[]{NativeGlaive.HitShard,NativeGlaive.HitPull,NativeGlaive.HitGuard,NativeGlaive.HitBleed})Check(NativeGlaive.IsHit(kind)&&kind.Length<=24,kind+" is a known hit");
            // ---- the shared paths ----
            var centre=new Vector3(0,.965f,0);var dir=Vector3.UnitZ;
            {
                var t1=NativeGlaiveChips.TwinAt(centre,dir,0,1);var t2=NativeGlaiveChips.TwinAt(centre,dir,0,1.05f);var o1=NativeGlaive.OrbitAt(centre,dir,0,1);var o2=NativeGlaive.OrbitAt(centre,dir,0,1.05f);
                Check(Math.Abs(new Vector2(t1.X,t1.Z).Length()-(NativeGlaive.HaloRadius+NativeGlaiveChips.TwinOut))<.01f,"the twin circles 1.2 m farther out");
                Check(Turned(Angle(o1,centre),Angle(o2,centre))*Turned(Angle(t1,centre),Angle(t2,centre))<0,"the twin goes the other way round");
                var s0=NativeGlaiveChips.TwinAt(centre,dir,0,0);var h0=NativeGlaive.OrbitAt(centre,dir,0,0);Check(Vector3.Dot(Vector3.Normalize(new Vector3(s0.X,0,s0.Z)),Vector3.Normalize(new Vector3(h0.X,0,h0.Z)))<-.99f,"it sets off opposite the halo's ring");
                Check(Vector3.Distance(NativeGlaiveChips.TwinAt(centre,dir,3,4.2f),NativeGlaiveChips.TwinAt(centre,dir,3,4.2f))<1e-6f,"deterministic");
            }
            {
                var dirs=Enumerable.Range(0,NativeGlaiveChips.Shards).Select(i=>NativeGlaiveChips.ShardDir(77,i)).ToArray();
                Check(dirs.All(d=>Math.Abs(d.Length()-1)<1e-4f&&d.Y==0)&&Enumerable.Range(0,dirs.Length).All(i=>Math.Abs(Vector3.Dot(dirs[i],dirs[(i+1)%dirs.Length])-Math.Cos(2*Math.PI/NativeGlaiveChips.Shards))<1e-3f),"six shards, flat and evenly spread");
                Check(Vector3.Distance(NativeGlaiveChips.ShardDir(77,0),NativeGlaiveChips.ShardDir(78,0))>.1f&&NativeGlaiveChips.ShardDir(77,2)==NativeGlaiveChips.ShardDir(77,2),"each burst turns them its own way, the same on every machine");
                Check(NativeGlaiveChips.ShardTravel(0)==0&&NativeGlaiveChips.ShardTravel(.1f)>0&&NativeGlaiveChips.ShardTravel(10)==NativeGlaiveChips.ShardRange,"a shard flies its 5 m and stops");
            }
            Check(NativeGlaiveChips.SlickOn(10,10)&&NativeGlaiveChips.SlickOn(10,10+NativeGlaive.SkimTime+NativeGlaiveChips.SlickTime-.01f)&&!NativeGlaiveChips.SlickOn(10,10+NativeGlaive.SkimTime+NativeGlaiveChips.SlickTime+.01f)&&!NativeGlaiveChips.SlickOn(10,9.9f),"the slick lasts the skim and 2 s after");
            {
                var flick=new NativeGlaiveKit{Ready=new float[2],Disc=NativeGlaive.Flick,DiscAt=20,DiscFrom=new V3(0,1.2f,0),DiscDir=new V3(0,0,1)};
                NativeGlaive.DiscAt(flick,20+NativeGlaive.FlickTime*.5f,centre,out var plain);NativeGlaive.DiscAt(flick,20+NativeGlaive.FlickTime*.25f,centre,out var plainSide);
                flick.LongArc=true;NativeGlaive.DiscAt(flick,20+NativeGlaive.FlickTime*.5f,centre,out var longer);NativeGlaive.DiscAt(flick,20+NativeGlaive.FlickTime*.25f,centre,out var longSide);NativeGlaive.DiscAt(flick,20+NativeGlaive.FlickTime,centre,out var back);
                Check(Math.Abs(plain.Z-NativeGlaive.FlickReach)<.05f&&Math.Abs(longer.Z-NativeGlaive.FlickReach*NativeGlaiveChips.ArcReach)<.05f,"Long Arc's loop goes 60% farther ("+longer.Z.ToString("0.00")+" m)");
                Check(Math.Abs(longSide.X/plainSide.X-NativeGlaiveChips.ArcWide)<.02f&&Vector3.Distance(back,new Vector3(0,1.2f,0))<.01f,"and 40% wider, back to the hand");
            }
            // ---- the effects ----
            {
                var scene=new Scene();var a=new Actor{Id=4,SourceSpec=NativeGlaive.Spec,Position=new V3(0,0,0),Yaw=0};scene.List.Add(a);var fx=new NativeTechniqueFx();
                int Draw(NativeGlaiveKit kit,float time,float q=1,bool low=false){a.Glaive=kit;var r=new Recorder();fx.DrawGlaives(r,scene,time,q,low);Check(!r.Broken,"the chips' moments are finite at "+time.ToString("0.00"));return r.Draws;}
                NativeGlaiveKit Orbit(bool chips)=>new NativeGlaiveKit{Ready=new float[2],Disc=NativeGlaive.Orbit,DiscAt=30,DiscDir=new V3(0,0,1),Twin=chips,Guard=chips};
                Check(Draw(Orbit(true),31)>Draw(Orbit(false),31)+20,"Twin Orbit's ring of light and Crescent Guard's shimmer are drawn");
                NativeGlaiveKit Burst(bool shatter)=>new NativeGlaiveKit{Ready=new float[2],Disc=NativeGlaive.Gone,DiscAt=40,Shatter=shatter,Hits=new[]{new NativeGlaiveHit{Id=9,Kind=NativeGlaive.HitBurst,At=40,Pos=new V3(0,1,4),Dir=new V3(0,1,0)}}};
                Check(Draw(Burst(true),40.1f)>Draw(Burst(false),40.1f)+12,"Shatterpoint's shards fly out of the burst");
                var slick=new NativeGlaiveKit{Ready=new float[2],SlickAt=50,SlickFrom=new V3(0,0,0),SlickTo=new V3(0,0,6.75f)};int on=Draw(slick,51),off=Draw(slick,50+NativeGlaive.SkimTime+NativeGlaiveChips.SlickTime+.1f);
                Check(on>off+20&&Draw(slick,51,.3f,true)<on,"Skimming Stone's slick shimmers on the floor, then is gone, thinner on the low tier");
                var quick=new NativeGlaiveKit{Ready=new float[2],QuickUntil=61.5f};Check(Draw(quick,60.5f)>Draw(quick,62)+6,"Quick Catch's rush streams off behind");
                foreach(var kind in new[]{NativeGlaive.HitShard,NativeGlaive.HitPull,NativeGlaive.HitGuard,NativeGlaive.HitBleed})
                {var h=new NativeGlaiveKit{Ready=new float[2],Hits=new[]{new NativeGlaiveHit{Id=3,Kind=kind,At=70,Pos=new V3(0,1,2),Dir=new V3(0,0,-1)}}};Check(Draw(h,70.08f)>3,kind+" draws");}
                var all=new NativeGlaiveKit{Ready=new float[2],Disc=NativeGlaive.Orbit,DiscAt=80,DiscDir=new V3(1,0,0),Keen=2,Twin=true,Shatter=true,Guard=true,QuickUntil=81,SlickAt=79.8f,SlickFrom=new V3(0,0,0),SlickTo=new V3(4,0,4),
                    Hits=new[]{new NativeGlaiveHit{Id=11,Kind=NativeGlaive.HitBurst,At=80,Pos=new V3(0,1,3),Dir=new V3(0,1,0)},new NativeGlaiveHit{Id=12,Kind=NativeGlaive.HitBleed,At=80.2f,Pos=new V3(1,1,0),Dir=new V3(0,-1,0)},new NativeGlaiveHit{Id=13,Kind=NativeGlaive.HitPull,At=80.1f,Pos=new V3(2,1,0),Dir=new V3(-1,0,0)}}};
                Check(all.Valid(),"the whole set at once is a valid record");int full=0,thin=0;for(float t=80;t<82;t+=1/30f){full+=Draw(all,t);thin+=Draw(all,t,.3f,true);}
                Check(full>500&&thin<full&&Draw(all,80.3f)==Draw(all,80.3f),"the whole set draws ("+full+"), thinner on the low tier ("+thin+"), the same each time");
            }
            // ---- the wiring (the Runtime is read as text) ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            Check(R("NativeStructures.cs").Contains("NativeGlaiveChips.Apply(content);foreach(var m in NativeGlaiveChips.Mechanics)NativeArena.SupportedMechanics.Add(m);")&&R("NativeSandboxTools.cs").Contains("if(rarity==NativeGlaiveChips.Tier)NativeGlaiveChips.Apply(Content);"),"the rows go in with the live content and the sandbox's preview");
            Check(R("NativeChipCard.cs").Contains("NativeGlaiveChipArt.Face(")&&R("NativeChipCard.cs").Contains("\"GLAIVE SPECIAL\"")&&R("NativeInspect.cs").Contains("NativeGlaiveChips.Tier?0x4fe8ff"),"its card and grade");
            var draft=R("NativeDraftScreen.cs");Check(draft.Contains("NativeGlaiveChipArt.Pose(chip.id,t,prefs.reducedEffects,")&&draft.Contains("NativeGlaiveChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);")&&draft.Contains("NativeGlaiveChips.Confirm(chip.id)")&&draft.Contains("NativeGlaiveChips.Is(id)?(float)NativeGlaiveChips.RevealSeconds:"),"its pick animates the card, says its line and holds the draft");
            Check(R("NativeAudio.cs").Contains("GlaivePick(match.Chosen,")&&R("NativeGlaivePresentation.cs").Contains("NativeGlaiveChips.Picks[i]"),"its pick's cues play");
            Check(R("NativeIcons.cs").Contains("NativeGlaiveChips.Glyph(id)!=null")&&R("NativeIcons.cs").Contains("??NativeGlaiveChips.Glyph(id);"),"its emblems are drawn wherever a chip's icon is");
            var arena=R("NativeArena.cs");Check(arena.Contains("if(!forced)amount*=GlaiveGuardTaken(target);")&&arena.Contains("speed*=GlaiveSpeed(p);"),"Crescent Guard's share of a blow and Quick Catch's speed are the arena's");
            var rules=R("NativeGlaiveAuthority.cs");
            foreach(var id in new[]{"FourthBounce","KeenEdge","TwinOrbit","Shatterpoint","MagnetReturn","QuickCatch","Whirlwind","LongArc","SkimmingStone","CrescentGuard"})Check(rules.Contains("NativeGlaiveChips."+id),"its rule reads "+id);
            Check(rules.Contains("RefreshDot(q,p,\"bleed\",NativeGlaiveChips.BleedDps,NativeGlaiveChips.BleedFor)")&&rules.Contains("amount*GlaiveKeen(p)*"),"Keen Edge: more damage and the bleed");
            Check(rules.Contains("k.Bounces<GlaiveRicochets(p)")&&rules.Contains("float best=GlaiveRicochetRange(p);")&&rules.Contains("Mathf.Pow(GlaiveRicochetScale(p),k.Bounces)"),"Fourth Bounce: the ricochets, their look and their share");
            Check(rules.Contains("NativeGlaiveChips.TwinAt(centre,")&&rules.Contains("GlaiveHaloCuts(p,new Vector3(tw.X,tw.Y,tw.Z),p.GlaiveTwinNext);"),"Twin Orbit: the second ring and its own cuts");
            Check(rules.Contains("float radius=GlaiveBurstRadius(p);")&&rules.Contains("p.GlaiveShards.Add(")&&rules.Contains("if(p.GlaiveShards.Count>0)GlaiveShardStep(p);"),"Shatterpoint: the wider burst and the shards flown");
            Check(rules.Contains("if(magnet)GlaiveDrag(p,q,NativeGlaiveChips.MagnetDrag,NativeGlaiveChips.MagnetClose);"),"Magnet Return: the drag on a return's cut");
            Check(rules.Contains("k.Ready[0]=Math.Max(Time,k.Ready[0]-NativeGlaiveChips.QuickCut);p.GlaiveQuickUntil=Time+NativeGlaiveChips.QuickFor;")&&rules.Contains("if(u>=1)Caught(true);"),"Quick Catch: time off Q and the speed at a catch (not the combo's flick)");
            Check(rules.Contains("if(step==2&&GlaiveChip(p,NativeGlaiveChips.Whirlwind))GlaiveWhirlDrag(p);")&&rules.Contains("if(wind){reach=NativeGlaiveChips.WhirlReach;damage*=NativeGlaiveChips.WhirlDamage;}"),"Whirlwind: the drag in at the wind-up, the reach and the damage at the cut");
            Check(rules.Contains("NativeGlaive.FlickReach*NativeGlaiveChips.ArcReach")&&rules.Contains("NativeGlaive.SwingDamage[3]*(arc?NativeGlaiveChips.ArcDamage:1)"),"Long Arc: the longer loop and its cuts");
            Check(rules.Contains("NativeGlaive.SkimReach*(stone?NativeGlaiveChips.SkimFarther:1)")&&rules.Contains("k.SlickAt=Time;")&&rules.Contains("Slow(q,NativeGlaiveChips.SlickSlow,NativeGlaiveChips.SlickHold);"),"Skimming Stone: the longer skim, the slick and its slow");
            Check(rules.Contains("if(GlaiveChip(p,NativeGlaiveChips.CrescentGuard))GlaiveGuardShots(p,pos);")&&rules.Contains("EndBullet(b);")&&rules.Contains("AbilityShots.Remove(s);"),"Crescent Guard: the halo cuts down enemy shots");
            Check(R("NativeGlaiveValidation.cs").Contains("// ---- the chips (Core NativeGlaiveChips) ----"),"the rules are checked against the real arena in the Editor's build checks");
            foreach(var file in new[]{"NativeGlaiveChipArt.cs","NativeGlaivePresentation.cs"}){var text=R(file);Check(!text.Contains("Damage(")&&!text.Contains("Knockback")&&!text.Contains("SendInput"),file+": presentation only");}
            var art=R("NativeGlaiveChipArt.cs");foreach(var fn in new[]{"FourthBounce(","KeenEdge(","TwinOrbit(","Shatterpoint(","MagnetReturn(","QuickCatch(","Whirlwind(","LongArc(","SkimmingStone(","CrescentGuard("})Check(art.Contains("private static void "+fn),"its own pick: "+fn.TrimEnd('('));
            Check(!art.Contains("new[]")&&!art.Contains("new List<")&&!art.Contains("=>{"),"the card art allocates nothing per frame");
            Console.WriteLine("PASS glaive chips: "+count+" checks (the ten and their lines, the emblems, the tier and its crest, the rows and odds, who is offered them, the picks, the numbers, the record, the shared paths, the effects, the wiring)");
        }
    }
}
