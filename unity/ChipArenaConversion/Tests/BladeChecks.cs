using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blade classes (docs/KATANA.md, docs/FANGS.md; owner 2026-11-10): the Katana public and the Fangs DEV in the content with their
    /// abilities, the shared record (valid, refused when broken, its copies), the moves on the pack's clips (each move a clip window fitted to
    /// its hit, the victims' paired clips, the sets' strides), the twenty chips (rows, lines, emblems, picks and their sounds), the effects
    /// (whole, deterministic, thinner on the low tier), the entrances, and the Runtime wiring read as text (CoreTests compiles Core only).
    /// </summary>
    internal static class BladeChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Blades: "+what);count++;}
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
            public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=Feet(a)+new Vector3(.3f,1.1f,.4f);tip=root+new Vector3((float)Math.Sin(a.Yaw),.3f,(float)Math.Cos(a.Yaw));return true;}
            public int Viewer=>0;public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int WeaponColor(Actor a)=>a.SourceSpec==NativeKatana.Spec?0xff3d6e:0x7dff3a;public int Shots=>0;public bool Shot(int index,out Vector3 at,out Vector3 velocity,out string fx,out int owner){at=velocity=default;fx=null;owner=0;return false;}
        }
        private sealed class Bank:NativeVisualMotion.IClips
        {
            public bool Has(string clip)=>!string.IsNullOrEmpty(clip);public float Length(string clip)=>3.5f;public float Hit(string clip)=>1.75f;
        }
        private static string R(string root,string file)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",file));
        public static void Run(string root)
        {
            count=0;var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});data.Validate();
            // ---- the rows ----
            var kr=data.Weapon(NativeKatana.Spec);Check(kr!=null&&!kr.devOnly&&kr.@class=="melee"&&kr.name==NativeKatana.Name,"the Katana is a public melee row");
            var fr=data.Weapon(NativeFangs.Spec);Check(fr!=null&&fr.devOnly&&fr.@class=="melee"&&fr.name==NativeFangs.Name,"the Fangs are a DEV melee row");
            Check(data.abilities.Count(a=>a.spec==NativeKatana.Spec)==2&&data.Ability(NativeKatana.Spec,"q")?.id==NativeKatana.Iai&&data.Ability(NativeKatana.Spec,"e")?.id==NativeKatana.Storm,"the Katana's Q (the iai) and E (the storm)");
            Check(data.abilities.Count(a=>a.spec==NativeFangs.Spec)==5&&NativeFangs.SlotMoves.Select((m,i)=>data.Ability(NativeFangs.Spec,i==0?"q":i==1?"e":(i-1).ToString())?.id==m).Count(x=>x)==5,"the Fangs' Q, E and 1-3 (the numbered rows by key)");
            Check(data.rarities.Any(r=>r.id==NativeKatanaChips.Tier&&r.weight==0&&r.color==NativeKatanaChips.Color)&&data.rarities.Any(r=>r.id==NativeFangsChips.Tier&&r.weight==0&&r.color==NativeFangsChips.Color),"their chip tiers, never rolled");
            Check(data.exclusiveOdds.Any(o=>o.spec==NativeKatana.Spec&&o.guaranteed==2)&&data.exclusiveOdds.Any(o=>o.spec==NativeFangs.Spec&&o.guaranteed==2),"their chips' odds in their drafts");
            Check(NativeRangedRules.KeyboardQ(data,NativeKatana.Spec,true)&&NativeRangedRules.KeyboardQ(data,NativeFangs.Spec,true),"Q is theirs");
            Check(NativeBlades.Slots(NativeKatana.Spec)==2&&NativeBlades.Slots(NativeFangs.Spec)==5&&NativeBlades.Slots("sword")==0&&NativeBlades.Is(NativeKatana.Spec)&&NativeBlades.Is(NativeFangs.Spec)&&!NativeBlades.Is(NativeGlaive.Spec),"their slots");
            // ---- the record ----
            var k=new NativeBladeKit{Ready=new float[2],CastKind=NativeKatana.Iai,CastAt=10,CastRelease=10.32f,CastUntil=10.94f,LineFrom=new V3(0,1,0),LineTo=new V3(0,1,7.5f),LineAt=10.44f,SplitAt=10.89f,
                Hits=new[]{new NativeBladeHit{Id=1,Kind=NativeKatana.HitMark,At=10.4f,Pos=new V3(0,1,3),Dir=new V3(0,0,1),Victim=2}}};
            Check(k.Valid(),"a live iai is valid");var c=k.ViewCopy();Check(c.Hits!=k.Hits&&c.Hits[0]!=k.Hits[0]&&c.Ready!=k.Ready&&c.Hits[0].Kind==k.Hits[0].Kind,"its copy shares nothing");
            Check(!new NativeBladeKit{CastKind="sword-launch"}.Valid()&&!new NativeBladeKit{CastStep=99}.Valid()&&!new NativeBladeKit{Ready=new float[9]}.Valid()&&!new NativeBladeKit{Hits=new[]{new NativeBladeHit{Kind="nope"}}}.Valid()&&!new NativeBladeKit{PathAt=float.NaN}.Valid(),"a broken one is refused");
            Check(NativeKatana.Sheathed(k,10.1f)&&!NativeKatana.Sheathed(k,10.5f)&&NativeKatana.Sheathed(k,10.85f)&&!NativeKatana.Sheathed(null,0),"the blade is in its saya for the draw and the click");
            // ---- the moves on the pack's clips ----
            var bank=new Bank();
            foreach(var spec in new[]{NativeKatana.Spec,NativeFangs.Spec})
            {
                var set=NativeVisualMotion.For(spec);Check(set!=null&&set.Spec==spec&&set.Locomotion.All(l=>l.StartsWith("Frank_RPG_",StringComparison.Ordinal))&&set.All.Length>30,spec+": a Frank set with its stride");
                Check(NativeVisualMotion.Loops(spec).Contains(set.Idle)&&NativeVisualMotion.Loops(spec).Length==NativeVisualStride.Clips+2,spec+": its stance and stride loop");
            }
            Check(NativeVisualMotion.Loops("sword").Length==0,"other sets keep their loops");
            var katanaActor=new Actor{Id=7,SourceSpec=NativeKatana.Spec,Grounded=true};
            for(int step=0;step<NativeKatana.Steps;step++)
            {
                katanaActor.Blade=new NativeBladeKit{Ready=new float[2],CastKind=NativeKatana.Cut,CastStep=step,CastAt=1,CastRelease=1+NativeKatana.CutHit[step],CastUntil=1+NativeKatana.CutLength[step]};
                Check(NativeBladeMotion.Clip(katanaActor,katanaActor.Blade,katanaActor.Blade.CastRelease,bank,out string clip,out float t,out _,out _)&&clip==NativeBladeMotion.KatanaCuts[step]&&Math.Abs(t-NativeBladeMotion.WindowOf(clip).Hit)<1e-3f,"cut "+step+": its clip's strike on the hit");
            }
            katanaActor.Blade=new NativeBladeKit{Ready=new float[2],CastKind=NativeKatana.Iai,CastAt=1,CastRelease=1.32f,CastUntil=1.94f,SplitAt=1.89f};
            Check(NativeBladeMotion.Clip(katanaActor,katanaActor.Blade,1.38f,bank,out string iai,out _,out _,out _)&&iai==NativeBladeMotion.KatanaIai&&NativeBladeMotion.Clip(katanaActor,katanaActor.Blade,1.89f,bank,out string sh,out float st,out _,out _)&&sh==NativeBladeMotion.KatanaSheathe&&Math.Abs(st-NativeBladeMotion.WindowOf(sh).Hit)<1e-3f,"the iai's dash, then the sheathe's click on the split");
            var fangsActor=new Actor{Id=8,SourceSpec=NativeFangs.Spec,Grounded=true,Blade=new NativeBladeKit{Ready=new float[5],CastKind=NativeFangs.Cut,CastStep=0,CastAt=1,CastRelease=1.1f,CastUntil=1.3f}};
            Check(NativeBladeMotion.Cast(NativeVisualMotion.For(NativeFangs.Spec),fangsActor,1.1f,bank,false,out var fp)&&fp.Clip==NativeBladeMotion.FangsCuts[0],"a Fangs cut standing takes the whole body");
            Check(!NativeBladeMotion.Cast(NativeVisualMotion.For(NativeFangs.Spec),fangsActor,1.1f,bank,true,out _)&&NativeBladeMotion.Upper(NativeVisualMotion.For(NativeFangs.Spec),fangsActor,1.1f,bank,out var up)&&up.A==NativeBladeMotion.FangsCuts[0],"on the move it is the upper body over the stride");
            // The victim of a pair plays its clip from the move's start, whatever its own set.
            var victim=new Actor{Id=9,SourceSpec="sword",PairBy=8,PairClip=NativeBladeMotion.FangsTakedownHit,PairAt=2,PairUntil=3.45f};
            Check(NativeBladeMotion.Victim(victim,2.5f,bank,out var vp)&&vp.Clip==NativeBladeMotion.FangsTakedownHit&&!NativeBladeMotion.Victim(victim,1.9f,bank,out _)&&!NativeBladeMotion.Victim(victim,4,bank,out _),"a held victim plays its paired clip, only while held");
            Check(NativeBladeMotion.VictimClip(NativeBladeMotion.KatanaCritical)==NativeBladeMotion.KatanaCriticalHit&&NativeBladeMotion.VictimClip(NativeBladeMotion.FangsTakedown)==NativeBladeMotion.FangsTakedownHit&&NativeBladeMotion.VictimClip("x")==null,"each attacker clip's victim clip");
            Check(NativeVisualMotion.For(NativeKatana.Spec).All.Contains(NativeBladeMotion.KatanaCriticalHit)&&NativeVisualMotion.For(NativeKatana.Spec).All.Contains(NativeBladeMotion.FangsTakedownHit)&&NativeVisualMotion.For(NativeFangs.Spec).All.Contains(NativeBladeMotion.KatanaCriticalHit),"the victims' clips are in the bank");
            // ---- the chips ----
            foreach(var katana in new[]{true,false})
            {
                var chips=katana?NativeKatanaChips.Chips():NativeFangsChips.Chips();string tier=katana?NativeKatanaChips.Tier:NativeFangsChips.Tier;string pre=katana?NativeKatanaChips.Prefix:NativeFangsChips.Prefix;
                Check(chips.Length==10&&chips.Select(x=>x.id).Distinct().Count()==10&&chips.All(x=>x.id.StartsWith(pre)&&x.rarity==tier&&x.mechanic==x.id&&x.tags.Contains(tier)&&x.max>=1&&x.max<=2&&x.description.Length>20),tier+": ten chips of their own");
                Check(chips.Select(x=>katana?NativeKatanaChips.Confirm(x.id):NativeFangsChips.Confirm(x.id)).Distinct().Count()==10,tier+": each its own line");
                Check(chips.All(x=>(katana?NativeKatanaChips.Glyph(x.id):NativeFangsChips.Glyph(x.id))?.Contains("<path")==true&&NativeChipIcons.Native(x.id)==x.id),tier+": each its own emblem");
                var picks=katana?NativeKatanaChips.Picks:NativeFangsChips.Picks;Check(picks.Length==10&&picks.All(l=>l.Length>=3&&l.All(cue=>NativeToonSynth.Known(cue.Id))),tier+": each pick's cues are sounds");
                Check(picks.All(l=>l.Zip(l.Skip(1),(x,y)=>x.At<=y.At).All(b=>b)),tier+": in time order");
                Check(NativeChipCartridge.Known(tier)&&NativeChipCartridge.Rarity(tier)==(katana?0xff3d6e:0x7dff3a),tier+": its cartridge");
                var all=new ConversionContent{chips=data.chips.ToArray(),incompatibilities=data.incompatibilities.ToArray(),contentRevision="x"};if(katana)NativeKatanaChips.Apply(all);else NativeFangsChips.Apply(all);int n0=all.chips.Length;if(katana)NativeKatanaChips.Apply(all);else NativeFangsChips.Apply(all);
                Check(all.chips.Length==n0&&all.chips.Count(x=>x.rarity==tier)==10,tier+": the overlay goes in once");
            }
            Check(NativeFangsChips.Bit(NativeFangsChips.Hydra)==2&&NativeKatanaChips.Holds(new NativeBladeKit{Chips=NativeKatanaChips.Bit(NativeKatanaChips.TwinMoons)},NativeKatanaChips.TwinMoons),"the record's chip bits");
            // ---- the effects ----
            var scene=new Scene();var fx=new NativeTechniqueFx();
            var ka=new Actor{Id=1,SourceSpec=NativeKatana.Spec,Position=new V3(0,0,0),Blade=new NativeBladeKit{Ready=new float[2],CastKind=NativeKatana.Storm,CastAt=0,CastRelease=.3f,CastUntil=1.2f,LineFrom=new V3(0,0,0),LineTo=new V3(0,0,7),LineAt=.4f,SplitAt=.9f,
                Hits=new[]{new NativeBladeHit{Id=1,Kind=NativeKatana.HitSplit,At=.9f,Pos=new V3(0,1,3),Dir=new V3(0,0,1),Victim=2},new NativeBladeHit{Id=2,Kind=NativeKatana.HitWave,At=.5f,Pos=new V3(0,1,0),Dir=new V3(0,0,1)}}}};
            var fa=new Actor{Id=2,SourceSpec=NativeFangs.Spec,Position=new V3(3,0,0),Blade=new NativeBladeKit{Ready=new float[5],VeilAt=0,VeilUntil=4,ChipAt=0,
                Hits=new[]{new NativeBladeHit{Id=3,Kind=NativeFangs.HitThrow,At=.2f,Pos=new V3(3,1,0),Dir=new V3(0,0,1),Amount=5},new NativeBladeHit{Id=4,Kind=NativeFangs.HitBloom,At=.6f,Pos=new V3(3,1,4),Dir=new V3(0,1,0),Victim=5,Amount=6},new NativeBladeHit{Id=5,Kind=NativeFangs.HitCloud,At=0,Pos=new V3(3,0,0),Dir=new V3(0,1,0)}}}};
            scene.List.Add(ka);scene.List.Add(fa);
            var full=new Recorder();fx.DrawBlades(full,scene,.95f,1,false);var full2=new Recorder();new NativeTechniqueFx().DrawBlades(full2,scene,.95f,1,false);var low=new Recorder();new NativeTechniqueFx().DrawBlades(low,scene,.95f,.3f,true);
            Check(!full.Broken&&full.Draws>100,"the effects draw ("+full.Draws+")");Check(full2.Draws==full.Draws,"the same frame draws the same");Check(low.Draws<full.Draws,"thinner on the low tier ("+low.Draws+" of "+full.Draws+")");
            // ---- the entrances ----
            Check(NativeIntroRules.Pickable(NativeKatana.Entrance)&&NativeIntroRules.Pickable(NativeFangs.Entrance)&&NativeEntranceDirector.Styles.Contains(NativeKatana.Entrance)&&NativeEntranceDirector.Styles.Contains(NativeFangs.Entrance),"their entrances are staged and pickable");
            var ki=new NativeKatanaIntro();var fi=new NativeFangsIntro();
            Check(Math.Abs(NativeIntroRules.Duration(NativeKatana.Entrance)-NativeKatanaIntro.Total)<1e-3&&Math.Abs(NativeIntroRules.NameAt(NativeFangs.Entrance)-NativeFangsIntro.CardAt)<1e-3,"their lengths and names' moments");
            for(float t=0;t<NativeKatanaIntro.Total;t+=.1f)Check(ki.Clip(t,out var kc,out float kt)&&kc.StartsWith("Frank_RPG_Katana_",StringComparison.Ordinal)&&kt>=0&&kt<6,"the Katana's entrance plays the pack's clips at "+t.ToString("F1"));
            for(float t=0;t<NativeFangsIntro.Total;t+=.1f)Check(fi.Clip(t,out var fc,out float ft)&&fc.StartsWith("Frank_",StringComparison.Ordinal)&&ft>=0&&ft<5,"the Fangs' entrance plays the pack's clips at "+t.ToString("F1"));
            Check(ki.Sheathed(.5f)&&!ki.Sheathed(3f)&&ki.Sheathed(5f)&&!fi.Sheathed(1),"the katana sheathed, drawn, sheathed again");
            // ---- the Runtime wiring (as text) ----
            Check(R(root,"NativeArena.cs").Contains("StepBladeWorld();")&&R(root,"NativeArena.cs").Contains("StepBladeInput(p);")&&R(root,"NativeArena.cs").Contains("if(PinBladeVictim(p))return;")&&R(root,"NativeArena.cs").Contains("BladeSnapshot(p,a);"),"the arena steps them, pins their victims and snapshots them");
            Check(R(root,"NativeChips.cs").Contains("BladeBlocked(p,attacker);")&&R(root,"NativeBots.cs").Contains("!BladeUnseen(q)")&&R(root,"NativeSourcePresentation.cs").Contains("reveal=BladeReveal(a,time,reveal);"),"blocks open the counter, the veil hides from bots and enemies");
            Check(R(root,"NativeStructures.cs").Contains("NativeKatanaChips.Apply(content);")&&R(root,"NativeStructures.cs").Contains("NativeFangsChips.Apply(content);"),"their chips go live");
            Check(R(root,"NativeBladeAuthority.cs").Contains("Allows(\"weapon:\"+NativeFangs.Spec"),"the DEV Fangs answer only a pilot allowed them");
            Console.WriteLine("PASS blades: "+count+" checks");
        }
    }
}
