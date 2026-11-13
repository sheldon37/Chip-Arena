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
        private sealed class Recorder:ITechniqueFxSink,INativeEntranceFx
        {
            public int Draws,Bands,Streaks;public bool Broken;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Draws++;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){V(head);Streaks++;}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){V(at);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){V(tail);V(head);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){V(a);V(b);V(c);V(d);Bands++;}
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
            Check(!NativeKatana.Sheathed(k,10.1f)&&!NativeKatana.Sheathed(k,10.5f)&&!NativeKatana.Sheathed(k,10.85f)&&NativeKatana.Sheathed(k,10.885f)&&NativeKatana.Sheathed(k,10.92f)&&!NativeKatana.Sheathed(null,0),"the blade in the hand through the draw and the sheathing, in its saya from the click");
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
            // The Fangs' combo and dash on the Frank Rogue set (owner 2026-11-11: utilize the Rogue from the Frank Slash animation set), each cut's
            // strike on its hit, the set's clips at its own rate.
            Check(NativeBladeMotion.FangsCuts.Length==NativeFangs.Steps&&NativeBladeMotion.FangsCuts.All(x=>x.StartsWith("Frank_Rogue_",StringComparison.Ordinal))&&NativeBladeMotion.FangsPounce.StartsWith("Frank_Rogue_",StringComparison.Ordinal)&&NativeBladeMotion.RateOf(NativeBladeMotion.FangsPounce)==2,"the Fangs' five cuts and the dash are the Rogue's");
            for(int step=0;step<NativeFangs.Steps;step++)
            {
                fangsActor.Blade=new NativeBladeKit{Ready=new float[5],CastKind=NativeFangs.Cut,CastStep=step,CastAt=1,CastRelease=1+NativeFangs.CutHit[step],CastUntil=1+NativeFangs.CutLength[step]};
                Check(NativeBladeMotion.Clip(fangsActor,fangsActor.Blade,fangsActor.Blade.CastRelease,bank,out string fclip,out float ftime,out _,out _)&&fclip==NativeBladeMotion.FangsCuts[step]&&Math.Abs(ftime-NativeBladeMotion.WindowOf(fclip).Hit)<1e-3f,"Fangs cut "+step+": its clip's strike on the hit");
            }
            fangsActor.Blade=new NativeBladeKit{Ready=new float[5],CastKind=NativeFangs.Cut,CastStep=0,CastAt=1,CastRelease=1.1f,CastUntil=1.3f};
            // The plunging slam (owner 2026-11-11: slam attacks from the air for the Glaive, the Fangs and the Katana): the standard plunge for
            // the three kits, its rules by kin, its look the set's air attack held to the landing.
            Check(new[]{NativeKatana.Spec,NativeFangs.Spec,NativeGlaive.Spec}.All(x=>NativeAirAttackSimulation.KitSlam(x)&&NativePlungeRules.Handles(x))&&!NativeAirAttackSimulation.KitSlam("sword")&&!NativeAirAttackSimulation.KitSlam("pistol"),"the three kits slam from the air");
            Check(NativeAirAttackSimulation.Kin(NativeFangs.Spec)=="daggers"&&NativeAirAttackSimulation.Kin(NativeKatana.Spec)=="sword"&&NativeAirAttackSimulation.Kin(NativeGlaive.Spec)=="sword"&&NativeAirAttackSimulation.DownRadius(NativeFangs.Spec)==NativeAirAttackSimulation.DownRadius("daggers")&&NativeAirAttackSimulation.DownRadius(NativeKatana.Spec)==NativeAirAttackSimulation.DownRadius("sword"),"their slams by kin");
            Check(new[]{NativeKatana.Spec,NativeFangs.Spec,NativeGlaive.Spec,"sword","daggers"}.All(x=>NativePlungeRules.Kit(x)==NativeAirAttackSimulation.KitSlam(x)&&NativePlungeRules.Kin(x)==NativeAirAttackSimulation.Kin(x)&&NativePlungeRules.Radius(x)==NativeAirAttackSimulation.DownRadius(x)),"the look's copy of the kits and their kin is the rule's");
            foreach(var spec in new[]{NativeKatana.Spec,NativeFangs.Spec,NativeGlaive.Spec})
            {
                var set=NativeVisualMotion.For(spec);var air=new Actor{Id=11,SourceSpec=spec,AirAttack=true,AirKind=NativePlungeFields.DownKind,AttackAt=2,ImpactAt=2.6f};
                Check(set?.AirAttack!=null&&NativeBladeMotion.Slam(set,air,2.3f,bank,out var sp)&&sp.Clip==set.AirAttack&&!NativeBladeMotion.Slam(set,new Actor{Id=12,SourceSpec=spec,AttackAt=2},2.3f,bank,out _),spec+": the slam plays the set's air attack");
            }
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
            // The weapon trails (owner 2026-11-11: weapon trails for both): their blades from their designs' sockets (the left fang mirrored);
            // a fast cut lays bands from both fangs, a still blade none, a veiled one none.
            Check(NativeBladeView.Line(NativeKatana.Spec,false,out _,out var kt0)&&kt0==NativeWeaponModels.KatanaBladeTip&&!NativeBladeView.Line(NativeKatana.Spec,true,out _,out _)&&NativeBladeView.Line(NativeFangs.Spec,true,out _,out var ft1)&&ft1.X==-NativeWeaponModels.FangBladeTip.X&&!NativeBladeView.Line("sword",false,out _,out _),"the blades' lines");
            int Trails(float yawStep,bool veiled)
            {
                var sc=new Scene();var tf=new NativeTechniqueFx();var cutter=new Actor{Id=21,SourceSpec=NativeFangs.Spec,Position=new V3(0,0,0),Blade=new NativeBladeKit{Ready=new float[5],CastKind=NativeFangs.Cut,CastStep=1,CastAt=0,CastRelease=.13f,CastUntil=.36f,VeilAt=veiled?0:-100,VeilUntil=veiled?4:-100}};sc.List.Add(cutter);
                var r=new Recorder();for(int i=0;i<12;i++){cutter.Yaw=i*yawStep;r.Bands=0;tf.DrawBlades(r,sc,i/60f,1,false);}return r.Bands;
            }
            Check(Trails(.3f,false)>=40&&Trails(0,false)==0&&Trails(.3f,true)==0,"a fast cut trails from both fangs, a still blade and a veiled one do not");
            // ---- the entrances ----
            Check(NativeIntroRules.Pickable(NativeKatana.Entrance)&&NativeIntroRules.Pickable(NativeFangs.Entrance)&&NativeEntranceDirector.Styles.Contains(NativeKatana.Entrance)&&NativeEntranceDirector.Styles.Contains(NativeFangs.Entrance),"their entrances are staged and pickable");
            var ki=new NativeKatanaIntro();var fi=new NativeFangsIntro();
            Check(Math.Abs(NativeIntroRules.Duration(NativeKatana.Entrance)-NativeKatanaIntro.Total)<1e-3&&Math.Abs(NativeIntroRules.NameAt(NativeFangs.Entrance)-NativeFangsIntro.CardAt)<1e-3,"their lengths and names' moments");
            for(float t=0;t<NativeKatanaIntro.Total;t+=.1f)Check(ki.Clip(t,out var kc,out float kt)&&kc.StartsWith("Frank_RPG_Katana_",StringComparison.Ordinal)&&kt>=0&&kt<6,"the Katana's entrance plays the pack's clips at "+t.ToString("F1"));
            for(float t=0;t<NativeFangsIntro.Total;t+=.1f)Check(fi.Clip(t,out var fc,out float ft)&&fc.StartsWith("Frank_",StringComparison.Ordinal)&&ft>=0&&ft<5,"the Fangs' entrance plays the pack's clips at "+t.ToString("F1"));
            Check(ki.Sheathed(.5f)&&!ki.Sheathed(3f)&&ki.Sheathed(5f)&&!fi.Sheathed(1),"the katana sheathed, drawn, sheathed again");
            // The saya where the pack wears it and moves it, the blade held as the match holds it (owner 2026-11-11: "Katana intro is broken, they
            // dont hold it. Also, sheathe is still not on body."; Core NativeKatanaSaya).
            Check(ki.Clip(NativeKatanaIntro.DrawAt+.2f,out var dc,out _)&&dc==NativeBladeMotion.KatanaIai&&ki.Clip(NativeKatanaIntro.SheatheAt+.2f,out var sc,out _)&&sc==NativeBladeMotion.KatanaSheathe
                &&ki.Clip(NativeKatanaIntro.ClickAt,out _,out float clickT)&&Math.Abs(clickT-NativeKatanaSaya.SheatheClick)<1e-3f&&ki.Clip(NativeKatanaIntro.GrabAt,out _,out float grabT)&&Math.Abs(grabT-NativeKatanaSaya.IaiDraw)<1e-3f,
                "Crimson Moon draws on the pack's iai and sheathes on its whole sheathing, the draw and the click on the pack's moments");
            Check(ki.Sheathed(NativeKatanaIntro.GrabAt-.01f)&&!ki.Sheathed(NativeKatanaIntro.GrabAt+.01f)&&!ki.Sheathed(NativeKatanaIntro.ClickAt-.05f)&&ki.Sheathed(NativeKatanaIntro.ClickAt),"its blade leaves the saya on the draw and enters on the click");
            // The draw seen (owner 2026-11-11: "They don't pull sword out until animation is done essentially"): the clip's draw-out before the cut
            // plays at under a third of its pace, the blade out of the saya before the cut, and the shot near enough to see it.
            Check(ki.Clip(NativeKatanaIntro.WhipAt,out _,out float whip0)&&ki.Clip(NativeKatanaIntro.WhipAt+.1f,out _,out float whip1)&&(whip1-whip0)/.1f<.35f&&ki.Clip(NativeKatanaIntro.WhipAt-.1f,out _,out float pull)&&pull<whip0
                &&NativeKatanaIntro.GrabAt<NativeKatanaIntro.WhipAt&&NativeKatanaIntro.WhipAt<NativeKatanaIntro.CutAt&&!ki.Sheathed(NativeKatanaIntro.WhipAt),"its draw-out is slowed before the cut, the blade already out of the saya");
            // The counter's victim thrown back as its clip carries it (owner 2026-11-11: "Parry with katana make enemy walk backwards in place").
            {
                const float at=20,until=20+NativeKatana.CounterHold;float Back(float t)=>NativeBladeMotion.VictimTravel(NativeBladeMotion.KatanaCritical,t,at,until);
                Check(Back(at)==0&&Back(at+.4f)==0&&Back(at+.8f)>.2f&&Back(at+1.0f)>Back(at+.8f)&&Math.Abs(Back(until)-1.23f)<1e-3f&&Back(until+1)==Back(until)
                    &&NativeBladeMotion.VictimTravel(NativeBladeMotion.FangsCritical,at+1,at,until)==0&&NativeBladeMotion.VictimTravel(NativeBladeMotion.FangsTakedown,at+1,at,until)==0,"the counter's victim thrown back after the blow, as its clip carries it; the Fangs' held");
                Check(R(root,"NativeBladeAuthority.cs").Contains("float back=NativeBladeMotion.VictimTravel(p.Blade.PairClip,Time,p.Blade.PairAt,p.Blade.PairUntil);"),"the hold follows it");
            }
            Check(ki.Fade(NativeKatanaIntro.DrawAt+.1f)>ki.Fade(.5f)&&ki.Fade(NativeKatanaIntro.SheatheAt+.1f)>ki.Fade(.5f)&&fi.Fade(1)==.12f,"it eases into the draw and the sheathing");
            Check(NativeKatanaSaya.Carry(NativeBladeMotion.KatanaIai,1,out int ih)==1&&ih==NativeKatanaSaya.Draw&&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaRush,.4f,out _)==1&&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaStorm,1,out _)==1&&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaStorm,1.6f,out _)==0
                &&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaSheathe,0,out int shh)==0&&shh==NativeKatanaSaya.Sheathe&&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaSheathe,NativeKatanaSaya.SheatheClick,out _)==1&&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaSheathe,2.5f,out _)==0
                &&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaIdle,1,out _)==0&&NativeKatanaSaya.Carry(NativeBladeMotion.KatanaCuts[0],.3f,out _)==0,"the left hand carries the saya in the stance-three moves and the sheathing, and nowhere else");
            Check(NativeKatanaSaya.Turn(NativeBladeMotion.KatanaSheathe,0)==0&&NativeKatanaSaya.Turn(NativeBladeMotion.KatanaSheathe,.3f)==1&&NativeKatanaSaya.Turn(NativeBladeMotion.KatanaSheathe,NativeKatanaSaya.SheatheClick)==1&&NativeKatanaSaya.Turn(NativeBladeMotion.KatanaIai,1)==0
                &&Math.Abs(NativeKatanaSaya.SheatheTurn.Length()-1)<1e-3f&&Math.Abs(NativeKatanaSaya.RestTurn.Length()-1)<1e-3f&&NativeKatanaSaya.CarryTurn.All(q=>Math.Abs(q.Length()-1)<1e-3f)&&NativeKatanaSaya.CarryAt.Length==NativeKatanaSaya.CarryTurn.Length,
                "the blade turns over in the hand for the sheathing only, and the saya's frames are whole turns");
            Check(NativeBladeGrip.Of(NativeKatana.Spec,false,out var kg)&&!NativeBladeGrip.Of(NativeKatana.Spec,true,out _)&&NativeBladeGrip.Of(NativeFangs.Spec,false,out var fg)&&NativeBladeGrip.Of(NativeFangs.Spec,true,out var fl)&&!NativeBladeGrip.Of("sword",false,out _)
                &&Math.Abs(kg.Length()-1)<1e-3f&&Math.Abs(fg.Length()-1)<1e-3f&&Math.Abs(fl.Length()-1)<1e-3f,"their entrances hold the blades as the match does");
            // Their effects follow the blades as the model holds them (owner 2026-11-11: floating parts, much improved effects): handed in, the
            // draw trails; a replay starts it over; never handed in, nothing trails; a broken blade is refused.
            int IntroStreaks(NativeBladeIntro intro,float at,bool feed)
            {
                if(feed)for(int i=0;i<24;i++){float t=at-.4f+i/60f;var root=new Vector3(0,1.2f,0);intro.Blade(t,0,root,root+new Vector3((float)Math.Sin(i*.25f),.2f,(float)Math.Cos(i*.25f)),true);}
                var r=new Recorder();intro.Effects(at-.4f+23/60f,r,1);return r.Streaks;
            }
            int bare=IntroStreaks(new NativeKatanaIntro(),NativeKatanaIntro.CutAt+.2f,false),fed=IntroStreaks(new NativeKatanaIntro(),NativeKatanaIntro.CutAt+.2f,true);
            var replay=new NativeKatanaIntro();IntroStreaks(replay,NativeKatanaIntro.CutAt+.2f,true);var again=new Recorder();replay.Blade(.5f,0,default,default,false);replay.Effects(.5f,again,1);var still=new Recorder();new NativeKatanaIntro().Effects(.5f,still,1);
            Check(fed>bare+20&&again.Streaks==still.Streaks,"the draw trails the blade handed in ("+bare+" / "+fed+"), and a replay starts over");
            var fiFed=new NativeFangsIntro();fiFed.Blade(1,0,new Vector3(0,1,0),new Vector3(float.NaN,0,0),true);var nan=new Recorder();fiFed.Effects(1,nan,1);Check(!nan.Broken,"a broken blade is refused");
            // ---- the Runtime wiring (as text) ----
            Check(R(root,"NativeArena.cs").Contains("StepBladeWorld();")&&R(root,"NativeArena.cs").Contains("StepBladeInput(p);")&&R(root,"NativeArena.cs").Contains("if(PinBladeVictim(p))return;")&&R(root,"NativeArena.cs").Contains("BladeSnapshot(p,a);"),"the arena steps them, pins their victims and snapshots them");
            Check(R(root,"NativeMelee.cs").Contains("bool riposte=!blocker.Seat.bot&&(NativeMeleeRules.Standard(blocker.Seat.spec)||blocker.Seat.spec==NativeKatana.Spec)&&attacker.Seat.spec!=\"siren\";")&&R(root,"NativeKatanaAuthority.cs").Contains("if(Time<p.BlockEnd){if(attack&&press&&RiposteOpen(p)&&Time<k.CounterUntil&&KatanaCounter(p))p.BlockEnd=0;return;}")
                &&R(root,"NativeBladeValidation.cs").Contains("the parry dazes its striker as a standard parry does"),"the Katana's timed parry gives the standard parry's riposte (its striker dazed as long; the counter answers it inside the window)");
            Check(R(root,"NativeChips.cs").Contains("BladeBlocked(p,attacker);")&&!R(root,"NativeGuard.cs").Contains("BladeBlocked(")&&R(root,"NativeArena.cs").Contains("&&Time<target.BlockEnd){BlockProc(target,source,")&&R(root,"NativeBladeValidation.cs").Contains("\"a blow held on the guard opens no counter and reels no one\"")&&R(root,"NativeCombatHud.cs").Contains("||presentation.BladeHidesHud(p.Seat.id)")&&R(root,"NativeBots.cs").Contains("!BladeUnseen(q)")&&R(root,"NativeSourcePresentation.cs").Contains("reveal=BladeReveal(a,time,reveal);"),"only a timed parry opens the counter (a blow held on the guard does not), the veil hides from bots and enemies");
            Check(R(root,"NativeStructures.cs").Contains("NativeKatanaChips.Apply(content);")&&R(root,"NativeStructures.cs").Contains("NativeFangsChips.Apply(content);"),"their chips go live");
            Check(R(root,"NativeBladeAuthority.cs").Contains("Allows(\"weapon:\"+NativeFangs.Spec"),"the DEV Fangs answer only a pilot allowed them");
            Check(R(root,"NativeMelee.cs").Contains("!NativeMeleeRules.Standard(p.Seat.spec)&&!KitSlam(p)")&&R(root,"NativeMeleeAbilities.cs").Contains("if(p.Input.Attack&&p.Swing==null&&KitSlam(p))TryMelee(p);")&&R(root,"NativeBladeAuthority.cs").Contains("if(KitSlam(p)){attack=press=false;}")&&R(root,"NativeGlaiveAuthority.cs").Contains("if(KitSlam(p)){")&&R(root,"NativeReplicaMotion.cs").Contains("NativeAirAttackSimulation.KitSlam(p.Seat.spec)&&a.AirAttack"),"the slam: the standard plunge for the kits, their own cuts held back, guests predicting it");
            Check(R(root,"NativeFangsAuthority.cs").Contains("FangsPounceStep(p);")&&R(root,"NativeBlockFxPresentation.cs").Contains("NativeBladeView.Line(spec,left,out var kitRoot,out var kitTip)")&&R(root,"NativeMeleeKitPresentation.cs").Contains("a.SourceSpec!=NativeFangs.Spec")&&R(root,"NativeIntros.cs").Contains("BladeIntroHeld(held,t,style,"),"the dash cuts its path; the trails find both blades; the intros get the blades as held");
            Check(R(root,"NativeVisualMotionPresentation.cs").Contains("NativeKatanaSaya.Carry(m.Names[i],m.Times[i],out int h)")&&R(root,"NativeVisualMotionPresentation.cs").Contains("NativeBladeGrip.Of(a.SourceSpec,false,out var grip)")&&R(root,"NativeVisualMotionPresentation.cs").Contains("weaponHand.rotation=m.RightHand.rotation*Quaternion.Slerp(Quaternion.identity,new Quaternion(q.X,q.Y,q.Z,q.W),body.SayaTurn)*m.HoldRotation;")
                &&R(root,"NativeBladePresentation.cs").Contains("frame=SayaRigid(pelvis)*Matrix4x4.TRS(SayaVec(NativeKatanaSaya.RestAt)")&&R(root,"NativeBladePresentation.cs").Contains("PalmGrip(introRig,NativeDevMotions.Base(bladeIntro.Spec)??bladeIntro.Spec,m,b==CapturedRig.LHand)")&&R(root,"NativeIntros.cs").Contains("bladeIntroHand??=b=>BladeIntroFrame(b,introGrip);")
                &&R(root,"NativeVisualBodyPresentation.cs").Contains("NativeBladeMotion.IntroFade=bladeIntro.Fade(bladeIntroClock);"),"the saya rides the pelvis and the left hand, the blade turns over in the hand, the entrances hold as the match does");
            Console.WriteLine("PASS blades: "+count+" checks");
        }
    }
}
