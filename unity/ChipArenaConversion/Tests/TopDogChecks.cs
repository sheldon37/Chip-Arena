using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Top Dog (docs/TOP-DOG.md): its content row and access, the key-6 input, the replicated record (valid through the preview's loop,
    /// refused when broken, deep-copied), the feast's timeline and the belly, the effects (whole, deterministic, thinned, gone with an empty
    /// kit), the models, the toon sounds, the motion set, the burst gib's plan, the entrance, and the wiring in the Runtime (read as text).
    /// </summary>
    internal static class TopDogChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Top Dog: "+what);count++;}
        private sealed class Recorder:ITechniqueFxSink
        {
            public int Draws;public float Hash;public bool Broken;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Hash+=v.X*.13f+v.Y*.17f+v.Z*.19f;Draws++;}
            private void F(float f){if(!MathUtil.Finite(f))Broken=true;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);F(size);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){V(head);V(velocity);F(width);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);F(opacity);F(size);}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){V(at);F(size);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){V(tail);V(head);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){V(a);V(b);V(c);V(d);}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){V(at);F(opacity);F(size);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){V(at);}
            public void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){V(head);V(segment);F(width);}
            public void Tongue(bool view,Vector3 tail,Vector3 head,Vector3 color,float width){V(tail);V(head);}
        }
        private sealed class Scene:IKitFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
            public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+Vector3.UnitY*1.25f;return true;}public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int Viewer=>0;public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}public int WeaponColor(Actor a)=>0xe2362c;public int Shots=>0;
            public bool Shot(int index,out Vector3 at,out Vector3 velocity,out string fx,out int owner){at=velocity=default;fx=null;owner=0;return false;}
        }
        private sealed class One:ITopDogSource{public NativeTopDogKit K;public int Id;public NativeTopDogKit Of(int actor)=>actor==Id?K:null;}
        private sealed class Book:NativeVisualMotion.IClips{public bool Has(string c)=>c!=null;public float Length(string c)=>1.2f;public float Hit(string c)=>.42f;}
        private static bool Finite(Vector3 v)=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z);
        public static void Run(string root)
        {
            count=0;
            // ---- the content row, access and input ----
            var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});data.Validate();var w=data.Weapon(NativeTopDog.Spec);
            Check(w!=null&&w.devOnly&&w.@class=="melee"&&data.Weight(NativeTopDog.Spec)>0,"a DEV melee weapon row with a weight");
            var rows=data.abilities.Where(a=>a.spec==NativeTopDog.Spec).ToArray();Check(rows.Length==8&&rows.All(a=>a.live)&&new[]{"q","e","1","2","3","4","5","6"}.All(s=>rows.Any(a=>a.slot==s)),"eight live ability rows on Q, E and 1-6");
            for(int i=0;i<NativeTopDog.Slots;i++){var row=data.Ability(NativeTopDog.Spec,i==0?"q":i==1?"e":(i-1).ToString());Check(row!=null&&row.id==NativeTopDog.Slot[i]&&Math.Abs(row.cooldown-NativeTopDog.Cooldown[i])<.001,NativeTopDog.Slot[i]+": its row on its key, its cooldown");}
            Check(data.Ability(NativeTopDog.Spec,"6")?.id==NativeTopDog.Feast,"the ultimate on 6");
            Check(!NativeAccess.Guest.Allows("weapon:"+NativeTopDog.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),"a guest cannot take it");
            Check(new InputFrame{SirenAbility=6}.Valid&&!new InputFrame{SirenAbility=7}.Valid,"key 6 is a valid input, 7 is not");
            Check(NativeRangedRules.KeyboardQ(data,NativeTopDog.Spec,true),"Q on the keyboard reaches it");
            // ---- the record through the preview's loop ----
            var feet=new Vector3(2,.5f,-3);var victimAt=new Vector3(2,.5f,-.5f);var kit=new NativeTopDogKit();var fx=new NativeTechniqueFx();var scene=new Scene();var src=new One{K=kit,Id=1};NativeDogView.Source=src;
            scene.List.Add(new Actor{Id=1,SourceSpec=NativeTopDog.Spec,Position=new V3(feet.X,feet.Y,feet.Z),Hp=100});scene.List.Add(new Actor{Id=9,Position=new V3(victimAt.X,victimAt.Y,victimAt.Z),Hp=100});
            var props=new HashSet<string>();var hits=new HashSet<string>();var marks=new HashSet<string>();var casts=new HashSet<string>();bool caught=false,popped=false,fed=false;
            for(float t=0;t<NativeTopDogPreview.Loop;t+=1/30f)
            {
                NativeTopDogPreview.Write(kit,feet,.4f,100,100+t,9,victimAt);Check(kit.Valid(),"the record is valid at "+t.ToString("0.00"));
                foreach(var p in kit.Props)props.Add(p.Kind);foreach(var h in kit.Hits)hits.Add(h.Kind);foreach(var m in kit.Marks)marks.Add(m.Kind);if(kit.CastKind!=null)casts.Add(kit.CastKind);
                if(kit.Feast!=null){if(kit.Feast.Catch>-50)caught=true;if(kit.Feast.Fed==NativeTopDog.Feeds)fed=true;}if(kit.Hits.Any(h=>h.Kind==NativeTopDog.HitPop))popped=true;
                var r=new Recorder();fx.DrawDogs(r,scene,100+t,1,false);Check(!r.Broken,"nothing broken at "+t.ToString("0.00"));
                var r2=new Recorder();fx.DrawDogs(r2,scene,100+t,1,false);Check(r.Draws==r2.Draws&&Math.Abs(r.Hash-r2.Hash)<1e-3f,"the same clock draws the same frame at "+t.ToString("0.00"));
                var thin=new Recorder();fx.DrawDogs(thin,scene,100+t,.3f,true);Check(thin.Draws<=r.Draws,"Reduced and low draw no more at "+t.ToString("0.00"));
                var copy=kit.ViewCopy();Check(copy.Valid()&&(copy.Props==null||copy.Props.Length==kit.Props.Length),"its view copy is whole");
            }
            foreach(var k in new[]{NativeTopDog.Mallet,NativeTopDog.Boing,NativeTopDog.Zip,NativeTopDog.Anvil,NativeTopDog.Peel,NativeTopDog.Mustard,NativeTopDog.Glove,NativeTopDog.Pie,NativeTopDog.Feast})Check(casts.Contains(k),k+": the loop casts it");
            foreach(var k in new[]{NativeTopDog.PropAnvil,NativeTopDog.PropPeel,NativeTopDog.PropPie,NativeTopDog.PropGlove,NativeTopDog.PropSquirt,NativeTopDog.PropPuddle,NativeTopDog.PropSpring,NativeTopDog.PropZip})Check(props.Contains(k),k+": the loop shows it");
            Check(caught&&fed&&popped&&marks.Contains(NativeTopDog.Stuffed),"the loop's feast catches, feeds all six, stuffs and pops");
            src.K=new NativeTopDogKit();var none=new Recorder();fx.DrawDogs(none,scene,100,1,false);Check(none.Draws==0,"an empty kit draws nothing");
            // A copy is deep.
            NativeTopDogPreview.Write(kit,feet,.4f,100,100+20,9,victimAt);var deep=kit.ViewCopy();deep.Feast.Fed=0;deep.Ready[0]=-5;if(deep.Props.Length>0)deep.Props[0].Size=63;
            Check(kit.Feast.Fed!=0||kit.Feast.Catch<-50,"the copy's feast is its own");Check(kit.Ready[0]!=-5,"the copy's ready times are its own");Check(kit.Props.Length==0||kit.Props[0].Size!=63,"the copy's props are its own");
            NativeDogView.Source=null;
            // ---- a bad record is refused ----
            Check(!new NativeTopDogKit{Meter=2}.Valid(),"meter out of range");Check(!new NativeTopDogKit{Peels=9}.Valid(),"too many peels");Check(!new NativeTopDogKit{Combo=3}.Valid(),"a fourth swing");
            Check(!new NativeTopDogKit{Props=new[]{new NativeDogProp{Kind="nuke"}}}.Valid(),"an unknown prop");Check(!new NativeTopDogKit{Props=new NativeDogProp[NativeTopDog.MaxProps+1]}.Valid(),"too many props");
            Check(!new NativeTopDogKit{Marks=new[]{new NativeDogMark{Kind="cursed"}}}.Valid(),"an unknown mark");Check(!new NativeTopDogKit{Feast=new NativeDogFeast{Fed=7}}.Valid(),"more than six hot dogs");
            Check(!new NativeTopDogKit{Feast=new NativeDogFeast{Pop=float.NaN}}.Valid(),"a broken clock");Check(!new NativeTopDogKit{Ready=new float[NativeTopDog.Slots+1]}.Valid(),"too many cards");Check(!new NativeTopDogKit{CastKind=new string('x',25)}.Valid(),"a long cast");
            Check(new NativeTopDogKit{Ready=new float[NativeTopDog.Slots],Peels=2}.Valid(),"a fresh kit is valid");
            // ---- the feast's timeline and the belly ----
            var f=new NativeDogFeast{At=10,Throw=10.6f,Catch=11};f.FeedAt=NativeTopDog.FeedAt(f.Catch);f.Release=NativeTopDog.ReleaseAt(f.FeedAt);f.Pop=NativeTopDog.PopAt(f.Release);f.Until=f.Pop+NativeTopDog.After;
            Check(f.Catch<f.FeedAt&&f.FeedAt<f.Release&&f.Release<f.Pop&&f.Pop<f.Until,"catch, feed, release, pop, over, in that order");
            float last=0;bool rising=true;for(float t=f.Catch;t<f.Pop-.01f;t+=.02f){float b=NativeTopDog.Belly(f,t);if(b+1e-4f<last)rising=false;last=b;}
            Check(rising,"the belly only grows to the pop");Check(NativeTopDog.Belly(f,f.Catch)==0&&Math.Abs(NativeTopDog.Belly(f,f.Release)-1)<.02f&&NativeTopDog.Belly(f,f.Pop-.02f)>1.5f&&NativeTopDog.Belly(f,f.Pop+.01f)==0,"empty, full at the let-go, ballooning to the pop, gone after");
            Check(NativeTopDog.Tremble(f,f.Release-.1f)==0&&Math.Abs(NativeTopDog.Tremble(f,f.Pop-.05f))<.1f,"it trembles only once let go");
            var snapped=new NativeDogFeast{At=f.At,Catch=f.Catch,FeedAt=f.FeedAt,Release=f.Release,Pop=f.Pop,Snap=f.FeedAt+1.3f};Check(NativeTopDog.Belly(snapped,snapped.Snap+.6f)==0&&NativeTopDog.Belly(snapped,snapped.Snap-.01f)>0,"a snapped feast's belly goes down");
            for(int i=0;i<NativeTopDog.Feeds;i++)
            {
                Check(NativeDogView.FeedPhase(f,i,NativeTopDog.OfferAt(f.FeedAt,i)-.5f,out _,out _)==0,"hot dog "+i+": not yet before its turn");
                Check(NativeDogView.FeedPhase(f,i,NativeTopDog.OfferAt(f.FeedAt,i)+.05f,out float mv,out _)==1&&mv>0,"hot dog "+i+": brought to the mouth");
                Check(NativeDogView.FeedPhase(f,i,NativeTopDog.DownAt(f.FeedAt,i)-.05f,out _,out float into)==2&&into>.5f,"hot dog "+i+": going in");
                Check(NativeDogView.FeedPhase(f,i,NativeTopDog.DownAt(f.FeedAt,i)+.01f,out _,out _)==3,"hot dog "+i+": down");
            }
            Check(NativeDogView.LassoPhase(f,f.At+.3f)==1&&NativeDogView.LassoPhase(new NativeDogFeast{At=10,Throw=10.6f},10.8f)==2&&NativeDogView.LassoPhase(f,f.FeedAt+1)==3&&NativeDogView.LassoPhase(f,f.Release+.1f)==0,"the links: twirled, flying, round the victim, gone");
            // The wrapped victim's struggle (owner 2026-11-09: "their arms are downwards, and they're struggling to get out").
            Check(NativeTopDog.Bound(f,f.Catch-.01f)==0&&NativeTopDog.Bound(f,f.Catch+NativeTopDog.BindIn)>.99f&&NativeTopDog.Bound(f,(f.FeedAt+f.Release)*.5f)==1&&NativeTopDog.Bound(f,f.Release)==0,"the links hold from the catch to the let-go");
            Check(NativeTopDog.Bound(snapped,snapped.Snap+.01f)==0&&NativeTopDog.Strain(snapped,snapped.Snap+.01f,out _)==0,"nothing holds once spat out");
            {
                float most=0,least=1,lastSide=0,peak=0,slack=0;bool swapped=false,ranged=true;
                for(float t=f.Catch+.3f;t<f.Release-.2f;t+=.01f){float st=NativeTopDog.Strain(f,t,out float sd);if(st<0||st>1||sd<-1||sd>1)ranged=false;if(st>most){most=st;peak=t;}if(st<least){least=st;slack=t;}if(lastSide!=0&&Math.Sign(sd)!=Math.Sign(lastSide))swapped=true;lastSide=sd;}
                Check(ranged&&most>.8f&&least<.05f&&swapped,"it strains in jerks, hard then slack, throwing its weight one way then the other ("+least.ToString("0.00")+".."+most.ToString("0.00")+")");
                var wrapped=new NativeDogFeast{At=f.At,Throw=f.Throw,Catch=f.Catch,FeedAt=f.FeedAt,Release=f.Release,Pop=f.Pop,Until=f.Until,Victim=9,Dir=new V3(0,0,1)};
                NativeDogView.Source=src;var keep=src.K;src.K=new NativeTopDogKit{Feast=wrapped};
                var hard=new Recorder();fx.DrawDogs(hard,scene,peak,1,false);var easy=new Recorder();fx.DrawDogs(easy,scene,slack,1,false);var thinWrap=new Recorder();fx.DrawDogs(thinWrap,scene,peak,.3f,true);
                Check(hard.Draws>easy.Draws+3&&!hard.Broken&&thinWrap.Draws<hard.Draws,"strain lines fly off its sides on a heave, sweat off its head, dust at its feet ("+hard.Draws+" hard, "+easy.Draws+" slack, "+thinWrap.Draws+" thin)");
                src.K=keep;NativeDogView.Source=null;
                string tp=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeTopDogPresentation.cs"));
                Check(tp.Contains("if(bound>0)DogStruggle(body,a,r,v.feast,bound,time);")&&tp.Contains("var target=shoulder+Vector3.down*(reach*.93f)")&&tp.Contains("if(dogWraps.TryGetValue(victim.Id,out var w)&&w.frame==Time.frameCount)"),"its body: the spine twisting, the arms pinned down its sides by IK, the links wrapped round them on its spine");
            }
            {var p=new NativeDogProp{Kind=NativeTopDog.PropAnvil,At=0,LandAt=.9f,Until=4.4f};float h0=NativeDogView.AnvilDrop(p,0,out _),h1=NativeDogView.AnvilDrop(p,.45f,out _),h2=NativeDogView.AnvilDrop(p,.9f,out _);NativeDogView.AnvilDrop(p,.945f,out float sq);Check(h0>h1&&h1>h2&&h2==0&&sq<.8f,"the anvil falls faster as it comes and squashes landing");}
            {var p=new NativeDogProp{Kind=NativeTopDog.PropGlove,At=0,LandAt=.2f,Until=.45f,Size=13};Check(NativeDogView.GloveExtent(p,0)==0&&Math.Abs(NativeDogView.GloveExtent(p,.2f)-13)<.2f&&NativeDogView.GloveExtent(p,.45f)<.05f,"the glove shoots out and winds back");}
            // ---- names, icons, sounds ----
            Check(NativeTopDog.Slot.Length==NativeTopDog.Slots&&NativeTopDog.SlotKeys.Length==NativeTopDog.Slots&&NativeTopDog.Cooldown.Length==NativeTopDog.Slots,"a key and a cooldown for every card");
            foreach(var id in NativeTopDog.Slot.Concat(new[]{NativeTopDog.Mallet,NativeTopDog.Feast}))
            {
                Check(NativeTopDog.AbilityName(id).Length>0,id+": a name");var glyph=NativeTopDog.Glyph(NativeTopDog.Icon(id));Check(glyph!=null,id+": a glyph");
                var cover=NativeIconRaster.Coverage(NativeIconRaster.Parse(glyph,1,NativeIconRaster.Mode.Markup),48,NativeTopDog.GlyphBox);float ink=cover.Sum()/cover.Length;Check(ink>.06f&&ink<.7f,id+": its glyph draws ("+ink.ToString("0.00")+")");
            }
            Check(NativeTopDog.Glyph("bomb")==null&&NativeTopDog.Glyph("dog:nothing")==null,"only its own glyphs");
            var o=new (string id,float gain)[4];
            foreach(var k in new[]{NativeTopDog.HitBonk,NativeTopDog.HitSlam,NativeTopDog.HitClang,NativeTopDog.HitThud,NativeTopDog.HitSwat,NativeTopDog.HitAnvil,NativeTopDog.HitSlip,NativeTopDog.HitPow,NativeTopDog.HitPie,NativeTopDog.HitGulp,NativeTopDog.HitPop,NativeTopDog.HitSnap,NativeTopDog.HitBoing,NativeTopDog.Zip})
            {int n=NativeTopDogSounds.Hit(k,o);Check(n>0,k+": a sound");for(int i=0;i<n;i++)Check(NativeToonSynth.Known(o[i].id),k+": its sound "+o[i].id+" is synthesized");}
            foreach(var id in NativeToonSynth.Ids)
            {
                var s=NativeToonSynth.Samples(id);float peak=0;bool ok=true;foreach(var v in s){if(!MathUtil.Finite(v))ok=false;peak=Math.Max(peak,Math.Abs(v));}
                Check(ok&&s.Length==Math.Max(16,(int)(NativeToonSynth.Length(id)*NativeToonSynth.Rate))&&peak>.3f&&peak<=NativeToonSynth.Peak+1e-3f,id+": a whole, audible, unclipped sound (finite "+ok+", "+s.Length+" samples, peak "+peak.ToString("0.000")+")");
                Check(Math.Abs(s[0])<.02f&&Math.Abs(s[s.Length-1])<.02f,id+": no click at either end");
                var again=NativeToonSynth.Samples(id);Check(again.Length==s.Length&&again[s.Length/3]==s[s.Length/3],id+": the same each time");
            }
            Check(NativeTopDogSounds.Intro.Length>10&&NativeTopDogSounds.Intro.Zip(NativeTopDogSounds.Intro.Skip(1),(a,b)=>a.At<=b.At).All(x=>x)&&NativeTopDogSounds.Intro.All(c=>NativeToonSynth.Known(c.Id)&&c.At>=0&&c.At<NativeTopDogIntro.Total),"the entrance's cues are in order, inside it, all synthesized");
            // ---- the models ----
            foreach(int tier in new[]{0,1})
            {
                foreach(var k in new[]{NativeTopDogModels.Anvil(tier),NativeTopDogModels.BananaPeel(tier),NativeTopDogModels.Pie(tier),NativeTopDogModels.Glove(tier),NativeTopDogModels.Bottle(tier),NativeTopDogModels.StarShape(tier),NativeTopDogModels.Cart(0,tier),NativeTopDogModels.Cart(1,tier)})
                {Check(k.Count>0&&k.Lit.Count>0&&k.Lit.Count%3==0&&k.P.All(Finite)&&k.N.All(n=>Math.Abs(n.Length()-1)<.01f),"a whole toon model at tier "+tier);}
                var dog=new NativeFallenKit();NativeTopDogModels.HotDog(dog,Matrix4x4.Identity,1,1,0,1,tier);dog.Finish();var half=new NativeFallenKit();NativeTopDogModels.HotDog(half,Matrix4x4.Identity,1,1,0,.5f,tier);half.Finish();
                Check(dog.Count>0&&half.Count>0&&half.P.Max(p=>p.Z)<dog.P.Max(p=>p.Z)-.08f&&dog.P.All(Finite),"a hot dog half gone into a mouth has lost its front");
                var still=new NativeFallenKit();NativeTopDogModels.HotDog(still,Matrix4x4.Identity,1,0,0,1,tier);var squirm=new NativeFallenKit();NativeTopDogModels.HotDog(squirm,Matrix4x4.Identity,1,1,0,1,tier);Check(squirm.P.Max(p=>Math.Abs(p.X))>still.P.Max(p=>Math.Abs(p.X))+.01f,"a hot dog wiggles");
                var spring=new NativeFallenKit();NativeTopDogModels.Spring(spring,Matrix4x4.Identity,.6f,tier);Check(spring.Count>0&&Math.Abs(spring.P.Max(p=>p.Y)-.6f)<.02f,"the spring stands as tall as asked");
                var arm=new NativeFallenKit();NativeTopDogModels.ScissorArm(arm,Matrix4x4.Identity,12,tier);Check(arm.Count>0&&arm.P.Min(p=>p.Z)<-12,"the scissor arm reaches back the whole way");
                var links=new NativeFallenKit();NativeTopDogModels.Links(links,new[]{Vector3.Zero,Vector3.UnitX,new Vector3(2,0,0)},tier);Check(links.Count>0,"the links");
            }
            Check(NativeWeaponModels.Designed(NativeTopDog.Spec),"the mallet is designed");
            for(int tier=0;tier<NativeWeaponModels.Tiers;tier++){var m=NativeWeaponModels.Build(NativeTopDog.Spec,tier);Check(m.Triangles>0&&m.Triangles<=NativeWeaponModels.Budget[tier],"the mallet fits tier "+tier+" ("+m.Triangles+")");}
            // ---- the motion set ----
            var set=NativeVisualMotion.For(NativeTopDog.Spec);Check(set!=null&&set.Spec==NativeTopDog.Spec&&!set.Gun&&set.All.Contains("2Hand-Sword-Attack11")&&set.All.Contains("Unarmed-Attack-Kick-R2"),"its own two-handed set, every cast clip in it");
            // ---- the burst gib ----
            var burst=NativeSmashGib.Burst(new Vector3(1,1.2f,2),30,77);Check(burst!=null&&!burst.Pinned&&burst.Cause==SmashCause.Burst&&burst.Crushed==1&&burst.Crush[0]==CapturedRig.Spine,"the burst crushes the trunk");
            Check(NativeSmashGib.BurstSevered.Contains(CapturedRig.Head)&&NativeSmashGib.BurstSevered.Length==5&&!NativeSmashGib.BurstSevered.Contains(CapturedRig.Pelvis),"head, arms and legs come away");
            foreach(var part in new[]{CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LHip,CapturedRig.RHip})
            {
                var at=new Vector3(1,1.2f,2)+(part==CapturedRig.Head?new Vector3(0,.5f,0):part==CapturedRig.LArm?new Vector3(-.3f,.3f,0):part==CapturedRig.RArm?new Vector3(.3f,.3f,0):part==CapturedRig.LHip?new Vector3(-.12f,-.25f,0):new Vector3(.12f,-.25f,0));
                NativeSmashGib.Throw(burst,part,at,new Vector3(1,1.2f,2),out var v,out var spin);Check(Finite(v)&&Vector3.Dot(v,at-(new Vector3(1,1.2f,2)-Vector3.UnitY*.15f))>0&&v.Length()>3&&spin.Length()>1,"part "+part+" flies out from the belly");
            }
            int gushes=0;for(int k=0;k<NativeSmashGib.Gushes;k++)if(NativeSmashGib.Gush(burst,k,out _,out var origin,out var normal,out _,out _,out _,out _)&&Finite(origin)&&Math.Abs(normal.Length()-1)<.01f)gushes++;Check(gushes==NativeSmashGib.Gushes,"blood out of it every way");
            Check(NativeSmashGib.Applies(true,NativeTopDog.Burst,false,"pilot")&&!NativeSmashGib.Applies(false,NativeTopDog.Burst,false,"pilot"),"it gibs with gore on, not off");
            // What the burst wears of the killer's kill effect: the ragdoll endings, the swarms and the Gibber on its pieces; Hotdog Friends carry
            // the pieces over the stock ragdoll; an effect that takes the whole body away leaves it the stock ending.
            foreach(var style in new[]{"ragdoll","dev-gibber","ragdoll-ember","ragdoll-frost","ragdoll-static","ragdoll-gravity","ragdoll-pinball","ragdoll-fireworks","ragdoll-soul","ragdoll-petals","dev-singularity"})
                Check(NativeSmashGib.BurstStyle(style)==style,"a burst wears "+style);
            foreach(var style in new[]{null,"impact-ragdoll",NativeHotdogFriends.Style,"voxel-burst","confetti","ascension","shrink","launch","ghost","dev-raging-demon","dev-clown","dev-clown-love","dev-bass-van"})
                Check(NativeSmashGib.BurstStyle(style)=="impact-ragdoll","a burst under "+(style??"no effect")+" ends as the stock ragdoll");
            Check(NativeTopDog.Popped(NativeTopDog.PopKind)&&!NativeTopDog.Popped(null)&&!NativeTopDog.Popped("hazard")&&!NativeSmashGib.StageDeath(NativeTopDog.PopKind)&&!NativeStageHazards.DeathKind(NativeTopDog.PopKind),"the pop is its own kind, not a stage hazard");
            // ---- the entrance ----
            Check(Math.Abs(NativeIntroRules.Duration(NativeTopDog.Entrance)-NativeTopDogIntro.Total)<.001&&Math.Abs(NativeIntroRules.NameAt(NativeTopDog.Entrance)-NativeTopDogIntro.CardAt)<.001,"the match gives the entrance its length and its card its beat");
            Check(NativeEntranceDirector.Directed(NativeTopDog.Entrance)&&NativeIntroRules.Pickable(NativeTopDog.Entrance)&&NativeIntroRules.StyleName(NativeTopDog.Entrance).Contains("Order Up")&&NativeIntroRules.IntroSting(NativeTopDog.Entrance)==null&&NativeIntroRules.IntroWeapon(NativeTopDog.Entrance)=="weapon-"+NativeTopDog.Spec,"staged, pickable, named, its own sound, the mallet in hand");
            var script=(NativeTopDogIntro)NativeEntranceDirector.Script(NativeTopDog.Entrance);script.Prepare();var pose=new CapturedRig.Pose();var toons=new List<NativeIntroToon>();float lastClock=-1;bool clocked=true;
            for(float age=0;age<NativeTopDogIntro.Total+.5f;age+=1/30f)
            {
                float t=script.Clock(age);if(t+1e-4f<lastClock)clocked=false;lastClock=t;script.Pose(t,pose,out var rootAt,out var rot);Check(Finite(rootAt),"the pilot's root at "+age.ToString("0.00"));
                script.Camera(age,out var view);Check(Finite(view.Eye)&&Finite(view.Look)&&view.Fov>15&&view.Fov<80,"the camera at "+age.ToString("0.00"));
                script.Toons(t,toons);Check(toons.Count>0&&toons.All(x=>Finite(x.At)&&MathUtil.Finite(x.Param)),"the toon models at "+age.ToString("0.00"));
                var lights=new NativeEntranceLights();script.Lights(t,lights);var rec=new Recorder();script.Effects(t,new FxRecorder(rec),1);Check(!rec.Broken,"the effects at "+age.ToString("0.00"));
            }
            Check(clocked,"the entrance's clock never runs back");
            Check(script.Solid(NativeTopDogIntro.LidAt-.1f,CapturedRig.Chest)==0&&script.Solid(NativeTopDogIntro.LidAt+.1f,CapturedRig.Chest)==1,"hidden in the cart until the lid goes");
            Check(NativeTopDogIntro.MalletScale(NativeTopDogIntro.MalletAt)==0&&Math.Abs(NativeTopDogIntro.MalletScale(NativeTopDogIntro.Total)-1)<.02f,"the mallet comes out of nowhere and settles at its size");
            Check(NativeTopDogIntro.LidOpen(NativeTopDogIntro.DrumAt-.1f)==0&&NativeTopDogIntro.LidOpen(NativeTopDogIntro.LidAt+1)>.95f,"the lid is shut, then thrown open");
            Check(NativeTopDogIntro.BellAt<NativeTopDogIntro.DrumAt&&NativeTopDogIntro.DrumAt<NativeTopDogIntro.LidAt&&NativeTopDogIntro.LidAt<=NativeTopDogIntro.SpringAt&&NativeTopDogIntro.SpringAt<NativeTopDogIntro.ApexAt&&NativeTopDogIntro.ApexAt<NativeTopDogIntro.LandAt&&NativeTopDogIntro.LandAt<NativeTopDogIntro.MalletAt&&NativeTopDogIntro.MalletAt<NativeTopDogIntro.SlamAt&&NativeTopDogIntro.SlamAt<NativeTopDogIntro.CardAt&&NativeTopDogIntro.CardAt<NativeTopDogIntro.IrisClose&&NativeTopDogIntro.IrisClose<NativeTopDogIntro.Total,"its beats in order");
            // ---- the chips (Core NativeDogChips; owner 2026-11-08: about ten of its own, their picks animated, significant) ----
            {
                var chips=NativeDogChips.Chips();Check(chips.Length==10&&chips.Select(c=>c.id).Distinct().Count()==10&&chips.All(c=>NativeDogChips.Is(c.id)&&c.rarity==NativeDogChips.Tier&&c.mechanic==c.id&&c.tags.Contains(NativeDogChips.Tier)&&c.max>=1&&c.max<=2&&c.description.Length>40),"ten chips of its own, each its own rule");
                Check(chips.All(c=>NativeDogChips.Glyph(c.id)?.Contains("<path")==true&&NativeTopDog.Glyph(c.id)==NativeDogChips.Glyph(c.id))&&chips.Select(c=>NativeDogChips.Glyph(c.id)).Distinct().Count()==10&&NativeChipIcons.Native(chips[0].id)==chips[0].id,"each its own emblem, drawn through the class's glyphs");
                Check(chips.Select(c=>NativeDogChips.Confirm(c.id)).Distinct().Count()==10,"each its own line as it goes in");
                Check(NativeChipCartridge.Known(NativeDogChips.Tier)&&NativeChipCartridge.Of(NativeDogChips.Tier).Crest==NativeChipCartridge.Crest.Crown&&NativeChipCartridge.Rarity(NativeDogChips.Tier)==0xe8322b,"its tier's look on the 3D chip");
                Check(data.rarities.Any(r=>r.id==NativeDogChips.Tier&&r.weight==0&&r.color=="#e8322b")&&data.exclusiveOdds.Any(o=>o.spec==NativeTopDog.Spec&&o.guaranteed==2&&o.chance>.8),"its rarity and its signature odds in the content");
                Check(data.chips.All(c=>!NativeDogChips.Is(c.id)),"the rows come from the overlay");
                string before=data.contentRevision;NativeDogChips.Apply(data);int n0=data.chips.Length;NativeDogChips.Apply(data);Check(data.chips.Length==n0&&data.contentRevision!=before,"the overlay goes in once and marks the revision");data.Validate();
                var held=new Dictionary<string,int>();Check(NativeContentRules.Eligible(data,NativeTopDog.Spec,held).Count(c=>NativeDogChips.Is(c.id))==10,"the Top Dog can be offered all ten");
                foreach(var spec in new[]{"pistol","sword",NativeBombs.Spec})Check(!NativeContentRules.Eligible(data,spec,held).Any(c=>NativeDogChips.Is(c.id)),"nobody else is offered them: "+spec);
                var rng=new Random(47);int offers=0,ours=0;for(int i=0;i<400;i++){var picks=NativeContentRules.Offers(data,NativeTopDog.Spec,held,rng.NextDouble,3);offers+=picks.Count;ours+=picks.Count(NativeDogChips.Is);}Check(ours>offers/3,"likely in its own draft ("+ours+" of "+offers+")");
                foreach(var id in NativeDogChips.Ids)Check(NativeSoundCues.DevInstall(id),"its install is its own: "+id);
                Check(NativeTopDogSounds.Picks.Length==10&&NativeTopDogSounds.Picks.All(l=>l.Length>=3&&l.All(c=>Array.IndexOf(NativeToonSynth.Ids,c.Id)>=0&&c.At>=0&&c.At<3.2f)),"each pick has its own toon cues");
                Check(NativeDogChips.RevealSeconds>=NativeDraftPlant.DetailsAt+.55f+2.9f,"the draft holds for the whole gag");
                Check(NativeTopDog.MaxPeelCharges==4&&new NativeTopDogKit{Peels=4,PeelCap=4,Mallet=2}.Valid()&&!new NativeTopDogKit{Peels=5}.Valid()&&!new NativeTopDogKit{Mallet=3}.Valid(),"the record holds the chips' share");
                Check(NativeTopDog.IsKind(NativeTopDog.PropPiano)&&NativeTopDog.IsKind(NativeTopDog.PropSnack)&&NativeTopDog.IsMark(NativeTopDog.Sauced)&&NativeTopDog.IsMark(NativeTopDog.Walled),"the new props and marks are known");
                var piano=NativeTopDogModels.Piano();Check(piano.P.Count>200&&piano.P.Count<12000&&piano.P.All(v=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z)),"the grand piano builds ("+piano.P.Count+" vertices)");
                // Their moments drawn: a piano falling and landing, the snacks, hot sauce, the wall splat and Toon Logic, whole and thinned.
                var k2=new NativeTopDogKit{Sauce=true,Mallet=2,PeelCap=4,Peels=4};var src2=new One{K=k2,Id=1};NativeDogView.Source=src2;var fx2=new NativeTechniqueFx();
                k2.Props=new[]{new NativeDogProp{Id=1,Kind=NativeTopDog.PropPiano,Pos=new V3(2,.5f,0),At=0,LandAt=.55f,Until=2.8f,Dir=new V3(0,0,1)},new NativeDogProp{Id=2,Kind=NativeTopDog.PropSnack,Pos=new V3(0,1,0),Vel=new V3(2,5,0),To=new V3(1.4f,.5f,0),At=0,SampleAt=0,LandAt=.7f,Until=10},new NativeDogProp{Id=3,Kind=NativeTopDog.PropSquirt,Pos=new V3(0,1.4f,0),Dir=new V3(0,0,1),Size=9,At=0,Until=1.6f,Hit=1},new NativeDogProp{Id=4,Kind=NativeTopDog.PropPuddle,Pos=new V3(0,.5f,4),Size=.6f,At=0,Until=6,Hit=1}};
                k2.Hits=new[]{new NativeDogHit{Id=5,Kind=NativeTopDog.HitPiano,At=.55f,Pos=new V3(2,.5f,0),Dir=new V3(0,1,0)},new NativeDogHit{Id=6,Kind=NativeTopDog.HitWall,At=.2f,Pos=new V3(0,1.4f,3),Dir=new V3(0,0,-1)},new NativeDogHit{Id=7,Kind=NativeTopDog.HitMunch,At=.3f,Pos=new V3(0,.8f,0),Dir=new V3(0,1,0)},new NativeDogHit{Id=8,Kind=NativeTopDog.HitToonFlat,At=.1f,Pos=new V3(0,.5f,0),Dir=new V3(0,1,0)},new NativeDogHit{Id=9,Kind=NativeTopDog.HitToonPop,At=1.6f,Pos=new V3(0,1.4f,0),Dir=new V3(0,1,0)}};
                k2.Marks=new[]{new NativeDogMark{Id=9,Kind=NativeTopDog.Sauced,At=0,Until=3},new NativeDogMark{Id=9,Kind=NativeTopDog.Walled,At=.2f,Until=1.5f}};Check(k2.Valid(),"a chip-laden record is valid");
                int full=0,thin=0;for(float t=0;t<3;t+=1/20f){var rec=new Recorder();fx2.DrawDogs(rec,scene,t,1,false);full+=rec.Draws;Check(!rec.Broken,"the chips' moments are finite at "+t.ToString("0.00"));var low=new Recorder();fx2.DrawDogs(low,scene,t,.3f,true);thin+=low.Draws;}
                Check(full>200&&thin<full,"the chips' moments are drawn ("+full+") and thinner on the low tier ("+thin+")");NativeDogView.Source=src;
            }
            // ---- the mallet's dash and slam (owner 2026-11-09: "Give them a dash sprint attack and slam attack as well") ----
            Check(NativeTopDog.MalletMove(NativeTopDog.Dash)&&NativeTopDog.MalletMove(NativeTopDog.Slam)&&NativeTopDog.MalletMove(NativeTopDog.Mallet)&&!NativeTopDog.MalletMove(NativeTopDog.Zip),"the dash and the slam are the mallet's moves");
            var dashKit=new NativeTopDogKit{CastKind=NativeTopDog.Dash,CastAt=100,CastRelease=100+NativeTopDog.DashWind,CastUntil=100+NativeTopDog.DashWind+NativeTopDog.DashTime+NativeTopDog.DashRecover};
            var slamKit=new NativeTopDogKit{CastKind=NativeTopDog.Slam,CastAt=100,CastRelease=100+NativeTopDog.SlamHang,CastUntil=100+NativeTopDog.SlamHang+NativeTopDog.SlamMaxFall};
            Check(NativeDogView.MalletShown(dashKit,100.2f)==1&&NativeDogView.MalletShown(slamKit,100.3f)==1,"the mallet stays in hand through both");
            Check(NativeTopDog.SlamLanded(slamKit)<-50&&NativeTopDog.SlamLanded(dashKit)<-50,"in the air the slam has not landed");
            Check(dashKit.Valid()&&slamKit.Valid()&&NativeTopDog.Dash.Length<=24&&NativeTopDog.Slam.Length<=24,"both records are valid on the wire");
            Check(NativeTopDog.AbilityName(NativeTopDog.Dash)=="Mallet Dash"&&NativeTopDog.AbilityName(NativeTopDog.Slam)=="Mallet Slam","their names");
            {
                src.K=dashKit;var dr=new Recorder();fx.DrawDogs(dr,scene,100+NativeTopDog.DashWind+.1f,1,false);Check(dr.Draws>=9&&!dr.Broken,"the dash streams speed lines and kicks dust ("+dr.Draws+")");
                var sr=new Recorder();src.K=slamKit;fx.DrawDogs(sr,scene,100+NativeTopDog.SlamHang+.05f,1,false);Check(sr.Draws>=8&&!sr.Broken,"the drop rushes lines up past it ("+sr.Draws+")");
                var quake=new NativeTopDogKit{Hits=new[]{new NativeDogHit{Id=1,Kind=NativeTopDog.HitQuake,At=100,Pos=new V3(feet.X,feet.Y,feet.Z),Dir=new V3(0,1,0)}}};src.K=quake;
                var qr=new Recorder();fx.DrawDogs(qr,scene,100.2f,1,false);var ql=new Recorder();fx.DrawDogs(ql,scene,100.2f,.3f,true);Check(qr.Draws>60&&ql.Draws<qr.Draws&&!qr.Broken,"the landing's ring, cracks, dust and stars ("+qr.Draws+", "+ql.Draws+" thin)");
                var dashHit=new NativeTopDogKit{Hits=new[]{new NativeDogHit{Id=2,Kind=NativeTopDog.HitDash,At=100,Pos=new V3(feet.X,feet.Y,feet.Z),Dir=new V3(0,0,1)}}};src.K=dashHit;var hr=new Recorder();fx.DrawDogs(hr,scene,100.1f,1,false);Check(hr.Draws>5,"the dash's start puffs and streaks");
                // The mallet's smear through a swing's hit and its poofs in and out of hammerspace (owner 2026-11-09).
                var swing=new NativeTopDogKit{CastKind=NativeTopDog.Mallet,CastAt=100,CastRelease=100.2f,CastUntil=100.48f};src.K=swing;
                var sm=new Recorder();fx.DrawDogs(sm,scene,100.19f,1,false);var calm=new Recorder();fx.DrawDogs(calm,scene,100.02f,1,false);Check(sm.Draws>calm.Draws+20&&!sm.Broken,"a swing smears behind its head through the hit ("+sm.Draws+" against "+calm.Draws+" before it)");
                var anvil=new NativeTopDogKit{CastKind=NativeTopDog.Anvil,CastAt=100,CastRelease=100.22f,CastUntil=100.62f};NativeDogView.MalletPoofs(anvil,out float hide,out float show);
                NativeDogView.MalletPoofs(swing,out float nh,out float ns);Check(hide==100&&show==100.62f&&nh<-50&&ns<-50,"the mallet poofs away when an ability takes the hands and back when it ends, never for its own moves");
                src.K=anvil;var gone=new Recorder();fx.DrawDogs(gone,scene,100.08f,1,false);var back=new Recorder();fx.DrawDogs(back,scene,100.7f,1,false);var later=new Recorder();fx.DrawDogs(later,scene,101.4f,1,false);
                Check(gone.Draws>later.Draws+8&&back.Draws>later.Draws+8&&!gone.Broken,"both poofs are drawn ("+gone.Draws+", "+back.Draws+", "+later.Draws+" after)");
                src.K=kit;
            }
            {
                string tp=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeTopDogPresentation.cs"));
                Check(tp.Contains("NativeDogView.Nozzles[a.Id]=")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeTopDogFx.cs")).Contains("NativeDogView.Nozzles.TryGetValue(owner.Id,out var nz)"),"the mustard leaves the drawn bottle's nozzle");
                string mp=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualMotionPresentation.cs"));Check(mp.Contains("DogMalletLayer(body,a,time,m.HoldRotation,wgt);"),"the keyed mallet goes on before the weapon follows the hand");
                Check(R2("NativeVisualBodyPresentation.cs").Contains("DogMalletIntro(body,rig,dogIntroClock);")&&R2("NativeIntros.cs").Contains("dogIntroClock=t;"),"Order Up!'s slam is keyed by hand on the intro's body");
                string R2(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            }
            var snd=new (string id,float gain)[8];Check(NativeTopDogSounds.Hit(NativeTopDog.HitQuake,snd)>=2&&NativeTopDogSounds.Hit(NativeTopDog.HitDash,snd)>=1&&NativeTopDogSounds.Release(NativeTopDog.Dash,0,snd)==1&&NativeTopDogSounds.Release(NativeTopDog.Slam,0,snd)==1&&NativeTopDogSounds.Cast(NativeTopDog.Dash,0,snd)==1,"their sounds");
            {
                string vm=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeVisualMotion.cs"));Check(vm.Contains("if(time<rel+NativeTopDog.DashTime)return Loop(DogRun,rel,rel+NativeTopDog.DashTime,2.6f,out pick);")&&vm.Contains("case NativeTopDog.Smash:")&&vm.Contains("return Part(set.Land,land,until+.15f,out pick);"),"their legs: a stride through the charge, the fall and the landing's squat (the mallet keyed by hand over them)");
                string au=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeTopDogAuthority.cs"));
                Check(au.Contains("kind=DogMalletMove(p);")&&au.Contains("if(able){DogChannel(p);DogMalletMoves(p);}")&&au.Contains("NativeMeleeFeel.SprintAttack(p.Input,p.Pose.Sprinting))return NativeTopDog.Dash;")&&au.Contains("p.AirAttackUsed=true;p.DogAirborne=false;"),"the rules: an attack on a sprint dashes, one in the air slams (once an airtime)");
                Check(au.Contains("return NativeAirAttackSimulation.DownPitch(p.Input.Pitch)?NativeTopDog.Slam:NativeTopDog.Smash;")&&au.Contains("DogSmashed(p);")&&au.Contains("DogHit(p,NativeTopDog.HitClang,head,Vector3.up,first);"),"in the air: the slam looking down, the smash looking ahead, landing on its head's point");
            }
            // ---- casts on the move (owner 2026-11-09: "when they cast abilities while running, they start sliding") ----
            {
                var dogSet=NativeVisualMotion.For(NativeTopDog.Spec);var book=new Book();var me=new Actor{Id=1,Grounded=true,SourceSpec=NativeTopDog.Spec};
                src.K=new NativeTopDogKit{CastKind=NativeTopDog.Mallet,CastAt=200,CastRelease=200.2f,CastUntil=200.5f};
                // The swings are keyed by hand (NativeDogMallet) over whatever the legs do: no pack swing, no clip on the upper body.
                Check(NativeVisualMotion.Choose(dogSet,me,200.1f,new NativeVisualMotion.Memory(),book,false,out var still)&&!still.Clip.Contains("Attack")&&!NativeVisualMotion.Layer(dogSet,me,200.1f,book,out _),"standing, a swing is the stance under the hand-keyed mallet");
                Check(NativeVisualMotion.Choose(dogSet,me,200.1f,new NativeVisualMotion.Memory(),book,true,out var run)&&run.Locomotion&&!NativeVisualMotion.Layer(dogSet,me,200.1f,book,out _,true),"running, the legs keep the stride under it (no slide)");
                me.Grounded=false;Check(NativeVisualMotion.Choose(dogSet,me,200.1f,new NativeVisualMotion.Memory(),book,false,out var air)&&!air.Clip.Contains("Attack"),"in the air, the jump's legs under it");me.Grounded=true;
                foreach(var kind in new[]{NativeTopDog.Anvil,NativeTopDog.Peel,NativeTopDog.Mustard,NativeTopDog.Glove,NativeTopDog.Pie})
                {
                    src.K=new NativeTopDogKit{CastKind=kind,CastAt=200,CastRelease=200.2f,CastUntil=200.6f};
                    foreach(float at in new[]{200.1f,200.4f})
                    {
                        Check(NativeVisualMotion.Choose(dogSet,me,at,new NativeVisualMotion.Memory(),book,false,out var stand)&&!stand.Locomotion&&NativeVisualMotion.Choose(dogSet,me,at,new NativeVisualMotion.Memory(),book,true,out var move)&&move.Locomotion
                            &&NativeVisualMotion.Layer(dogSet,me,at,book,out var arms,true)&&arms.Raised&&arms.A==stand.Clip,kind+" at "+(at-200).ToString("0.0")+" s: the whole body standing, the stride and the arms running");
                    }
                }
                var feastKit=new NativeTopDogKit{Feast=new NativeDogFeast{At=200,Throw=200.4f,Until=205}};src.K=feastKit;
                Check(NativeVisualMotion.Choose(dogSet,me,200.2f,new NativeVisualMotion.Memory(),book,true,out var twirl)&&twirl.Locomotion&&NativeVisualMotion.Layer(dogSet,me,200.2f,book,out var twirlUp,true)&&twirlUp.A=="Armed-Cast-R-Attack3_start","the feast's twirl on the move: the stride and the arms");
                src.K=new NativeTopDogKit{CastKind=NativeTopDog.Dash,CastAt=200,CastRelease=200+NativeTopDog.DashWind,CastUntil=200+NativeTopDog.DashWind+NativeTopDog.DashTime+NativeTopDog.DashRecover};
                Check(NativeVisualMotion.Choose(dogSet,me,200.2f,new NativeVisualMotion.Memory(),book,true,out var dash)&&dash.Clip=="2Hand-Sword-Run-Forward"&&!NativeVisualMotion.Layer(dogSet,me,200.2f,book,out _,true),"the dash's legs run through the charge, the mallet keyed over them");
                MalletChecks();
                src.K=kit;
            }
            // ---- the wiring (the Runtime is read as text: CoreTests compiles Core only) ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var arena=R("NativeArena.cs");Check(arena.Contains("StepBombInput(p);StepDogInput(p);")&&arena.Contains("StepBombWorld();StepDogWorld();")&&arena.Contains("if(PinDogVictim(p))return;")&&arena.Contains("BombPending()||DogPending()")&&arena.Contains("DogMotion(p,ref velocity);")&&arena.Contains("DogDamaged(target,actual);")&&arena.Contains("a.Dog=p.Seat.spec==NativeTopDog.Spec?p.Dog.ViewCopy():null;"),"the rules are stepped, held, waited for, moved, hurt and snapshotted");
            Check(R("NativeFrameCodec.cs").Contains("a.HasDog=a.Dog!=null;a.Dog?.CapturePresence();")&&R("NativeNetworkFrame.cs").Contains("a.Dog!=null&&!a.Dog.Valid()"),"the record crosses the wire and is checked");
            // The pop keeps the killer's kill effect (owner 2026-11-09: "use the kill effect if applicable (Like hot dog friends should work)"):
            // its record's damage kind says it burst, accepted on the wire; the corpse bursts and wears the effect where it fits.
            Check(!R("NativeDeaths.cs").Contains("style=NativeTopDog.Burst")&&R("NativeArena.cs").Contains("||damageKind==NativeTopDog.PopKind?damageKind:null,stage,stageHazard);")
                &&R("NativeTopDogAuthority.cs").Contains("damageKind:NativeTopDog.PopKind,causeKind:NativeTopDog.Spec);")&&R("NativeNetworkFrame.cs").Contains("NativeTopDog.Popped(p.death.damageKind)")
                &&R("NativeSmashPresentation.cs").Contains("bool burst=death!=null&&NativeTopDog.Popped(death.damageKind);")&&R("NativeSmashPresentation.cs").Contains("NativeSmashGib.Burst(")
                &&R("NativeDeathPresentation.cs").Contains("Style=Smash!=null&&Smash.Cause==SmashCause.Burst?NativeSmashGib.BurstStyle(death.style):")&&R("NativeAudio.cs").Contains("NativeTopDog.Popped(target.Death.damageKind)?NativeTopDog.Burst:"),
                "the pop records the killer's kill effect and its own kind; the corpse bursts in it, and sounds its burst");
            var pres=R("ArenaPresentation.cs");Check(pres.Contains("DogBegin(s,renderTime);")&&pres.Contains("RenderDog(s,renderTime);"),"the presentation reads and draws it");
            Check(R("NativeTechniquePresentation.cs").Contains("DogFx(now);")&&R("NativeCinematicCamera.cs").Contains("presentation.DogFeastDirect(")&&R("NativeVisualBodyPresentation.cs").Contains("TopDogBody(body,a,time);")&&R("NativeFightHud.cs").Contains("DogCards(p);")&&R("ConversionGame.cs").Contains("DogScreen(width,height);")&&R("NativeAudio.cs").Contains("DogMoments(p,pos,t,mine);"),"its effects, camera, body, cards, screen and sound are hooked");
            Check(R("ConversionGame.cs").Contains("KeyCode.Alpha6"),"key 6 is read");
            Check(arena.Contains("DogSave(target,amount))return 0;")&&R("NativeStructures.cs").Contains("NativeDogChips.Apply(content);foreach(var m in NativeDogChips.Mechanics)NativeArena.SupportedMechanics.Add(m);")&&R("NativeChipCard.cs").Contains("NativeDogChipArt.Face(")&&R("NativeDraftScreen.cs").Contains("NativeDogChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);")&&R("NativeAudio.cs").Contains("DogPick(match.Chosen,"),"the chips' rules, rows, cards, pick and sound are wired");
            foreach(var file in new[]{"NativeTopDogPresentation.cs","NativeTopDogScreen.cs","NativeDogChipArt.cs"}){var text=R(file);Check(!text.Contains("Damage(")&&!text.Contains("Knockback")&&!text.Contains("SendInput"),file+": presentation only");}
            Console.WriteLine("PASS top dog: "+count+" checks (the row and key 6, the record through the preview, the feast and the belly, the effects, "+NativeToonSynth.Ids.Length+" toon sounds, the models, the burst gib, Order Up!, the ten chips, the wiring)");
        }
        /// <summary>The mallet by hand (Core NativeDogMallet; owner 2026-11-09): every move's fists in reach of their shoulders on the nominal
        /// body, the barrel across the handle, the head never in the floor and never jumping, the flat ends leading at every hit, the forehand
        /// right to left and the backhand left to right, each swing running on from the last, every slam's head flat on the floor ahead, and
        /// the record's moments reaching the keyed hits.</summary>
        private static void MalletChecks()
        {
            var moves=new (NativeDogMallet.Move m,bool landed)[]{(NativeDogMallet.Move.Swing1,false),(NativeDogMallet.Move.Swing2,false),(NativeDogMallet.Move.Swing3,false),(NativeDogMallet.Move.Dash,false),
                (NativeDogMallet.Move.Slam,false),(NativeDogMallet.Move.Slam,true),(NativeDogMallet.Move.Smash,false),(NativeDogMallet.Move.Smash,true),(NativeDogMallet.Move.Intro,false)};
            foreach(var (m,landed) in moves)
            {
                var keys=NativeDogMallet.KeyTimes(m,landed);string name=m+(landed?" landed":"");float worstR=0,worstL=0,lowest=9,fastest=0;
                Vector3? last=null;const float dt=1/240f;float fastAt=0;Vector3 fastFrom=default,fastTo=default;
                for(float t=keys[0];t<=keys[keys.Length-1]+1e-4f;t+=dt)
                {
                    var p=NativeDogMallet.At(m,t,landed);var d=NativeDogMallet.HandleAt(p);var f=NativeDogMallet.FaceAt(p);
                    Check(Math.Abs(d.Length()-1)<1e-3f&&Math.Abs(f.Length()-1)<1e-3f&&Math.Abs(Vector3.Dot(d,f))<2e-3f,name+": the handle and the barrel are unit and square at "+t.ToString("0.000"));
                    var g=NativeDogMallet.GripAt(p);var support=g+d*NativeDogMallet.Support;
                    worstR=Math.Max(worstR,(g-NativeDogMallet.ShoulderAt(p,true)).Length());worstL=Math.Max(worstL,(support-NativeDogMallet.ShoulderAt(p,false)).Length());
                    var head=NativeDogMallet.HeadAt(p);lowest=Math.Min(lowest,head.Y);
                    if(last.HasValue){float sp=(head-last.Value).Length()/dt;if(sp>fastest){fastest=sp;fastAt=t;fastFrom=last.Value;fastTo=head;}}last=head;
                }
                Console.WriteLine("  mallet "+name+": reach R "+worstR.ToString("0.00")+" L "+worstL.ToString("0.00")+", lowest head "+lowest.ToString("0.00")+", fastest "+fastest.ToString("0.0")+" at "+fastAt.ToString("0.000")+" "+fastFrom+" > "+fastTo);
                Check(worstR<=NativeDogMallet.Reach*1.05f,name+": the right fist stays in its arm's reach (worst "+worstR.ToString("0.00")+" m)");
                Check(worstL<=NativeDogMallet.Reach*1.08f,name+": the left fist reaches its place on the handle (worst "+worstL.ToString("0.00")+" m)");
                Check(lowest>=NativeDogMallet.Head*.85f,name+": the head never goes into the floor (lowest "+lowest.ToString("0.00")+" m)");
                Check(fastest<=100,name+": the head never jumps (a strike at most 100 m/s, a pop is several times that; fastest "+fastest.ToString("0.0")+" m/s)");
            }
            // The flat ends lead at each hit, the head going fast (a smear).
            foreach(var m in new[]{NativeDogMallet.Move.Swing1,NativeDogMallet.Move.Swing2,NativeDogMallet.Move.Dash})
            {
                float hit=NativeDogMallet.HitKey(m),h=.004f;var a=NativeDogMallet.HeadAt(NativeDogMallet.At(m,hit-h));var b=NativeDogMallet.HeadAt(NativeDogMallet.At(m,hit-h*.01f));var p=NativeDogMallet.At(m,hit-h*.5f);
                var v=(b-a);var d=NativeDogMallet.HandleAt(p);var across=v-d*Vector3.Dot(v,d);float speed=across.Length()/(h*.99f);
                Check(speed>6&&Math.Abs(Vector3.Dot(Vector3.Normalize(across),NativeDogMallet.FaceAt(p)))>.95f,m+": at the hit a flat end leads (the head at "+speed.ToString("0.0")+" m/s, the barrel along its travel)");
                Check(p.Smear>.3f,m+": it smears through the hit");
            }
            {
                float Side(NativeDogMallet.Move m,float t)=>NativeDogMallet.HeadAt(NativeDogMallet.At(m,t)).X;
                Check(Side(NativeDogMallet.Move.Swing1,-.06f)>.4f&&Side(NativeDogMallet.Move.Swing1,.09f)<-.4f,"the first swing goes from the right to the left ("+Side(NativeDogMallet.Move.Swing1,-.06f).ToString("0.00")+" to "+Side(NativeDogMallet.Move.Swing1,.09f).ToString("0.00")+")");
                Check(Side(NativeDogMallet.Move.Swing2,-.06f)<-.4f&&Side(NativeDogMallet.Move.Swing2,.09f)>.4f,"the second from the left to the right");
                bool Same(DogMalletPose x,DogMalletPose y)=>(NativeDogMallet.GripAt(x)-NativeDogMallet.GripAt(y)).Length()<.03f&&Vector3.Dot(NativeDogMallet.HandleAt(x),NativeDogMallet.HandleAt(y))>.995f&&Math.Abs(x.Twist-y.Twist)<1;
                Check(Same(NativeDogMallet.At(NativeDogMallet.Move.Swing1,.28f),NativeDogMallet.At(NativeDogMallet.Move.Swing2,-.2f))&&Same(NativeDogMallet.At(NativeDogMallet.Move.Swing2,.28f),NativeDogMallet.At(NativeDogMallet.Move.Swing3,-.36f)),"each swing starts where the last one ended (the combo flows)");
                var top=NativeDogMallet.At(NativeDogMallet.Move.Swing3,-.17f);Check(top.Lift>.35f&&top.Tuck>.8f&&NativeDogMallet.HeadAt(top).Z<-.3f,"the third hops, knees up, the head hung behind at the top");
            }
            // Every slam's head flat on the floor ahead at its impact, the body squashed.
            foreach(var (m,t,landed,ahead,tol) in new[]{(NativeDogMallet.Move.Swing3,.02f,false,NativeTopDog.SlamAhead,.45f),(NativeDogMallet.Move.Slam,.1f,true,1.4f,.6f),(NativeDogMallet.Move.Smash,.07f,true,NativeTopDog.SmashAhead,.45f),(NativeDogMallet.Move.Intro,5.02f,false,NativeTopDogIntro.SlamAhead,.05f)})
            {
                var p=NativeDogMallet.At(m,t,landed);var head=NativeDogMallet.HeadAt(p);var f=NativeDogMallet.FaceAt(p);
                Console.WriteLine("  mallet "+m+" impact: head "+head.X.ToString("0.00")+","+head.Y.ToString("0.00")+","+head.Z.ToString("0.00")+" barrel up "+Math.Abs(f.Y).ToString("0.00")+" squash "+p.Squash.ToString("0.00"));
                Check(Math.Abs(head.Y-NativeDogMallet.Head)<.04f&&Math.Abs(f.Y)>.9f&&Math.Abs(head.Z-ahead)<tol&&Math.Abs(head.X)<.25f&&p.Squash<.85f,
                    m+": the head lands flat on the floor ahead ("+head.Z.ToString("0.00")+" m out, "+head.Y.ToString("0.00")+" up, the barrel "+Math.Abs(f.Y).ToString("0.00")+" upright), the body squashed");
            }
            {
                var lean=NativeDogMallet.At(NativeDogMallet.Move.Intro,6.6f);var head=NativeDogMallet.HeadAt(lean);var thumbs=NativeDogMallet.At(NativeDogMallet.Move.Intro,7.6f);
                Check(Math.Abs(head.Y-NativeDogMallet.Head)<.05f&&NativeDogMallet.HandleAt(lean).Y<-.8f&&lean.Free<.01f&&thumbs.Free>.99f&&NativeDogMallet.Intro(8.5f,out var end)&&end.Weight>.99f,"Order Up! ends leaning on the mallet, its head on the floor, the left hand free for the thumbs up");
            }
            // The record's moments reach the keys: the hit key at the release, full weight inside, nothing after the fade.
            {
                var k=new NativeTopDogKit{CastKind=NativeTopDog.Mallet,CastAt=50,CastRelease=50.2f,CastUntil=50.48f,CastStep=1};
                Check(NativeDogMallet.Sample(k,50.2f,out var p,out var mv,out float from)&&mv==NativeDogMallet.Move.Swing2&&from==50&&Math.Abs(p.Weight-1)<1e-4f&&(NativeDogMallet.GripAt(p)-NativeDogMallet.GripAt(NativeDogMallet.At(mv,0))).Length()<1e-3f,"a swing's release is its hit key, at full weight");
                Check(NativeDogMallet.Sample(k,50.01f,out var p0,out _,out _)&&p0.Weight<.3f&&!NativeDogMallet.Sample(k,50.48f+NativeDogMallet.FadeOut+.01f,out _,out _,out _),"it fades in from the clip and is gone after its fade");
                k.CastStep=2;k.CastRelease=50.36f;k.CastUntil=50.82f;Check(NativeDogMallet.Sample(k,50.2f,out _,out var m3,out _)&&m3==NativeDogMallet.Move.Swing3,"the third step is the jump slam");
                var slam=new NativeTopDogKit{CastKind=NativeTopDog.Slam,CastAt=60,CastRelease=60.2f,CastUntil=62.7f};Check(NativeDogMallet.Sample(slam,60.17f,out var sa,out var sm,out _)&&sm==NativeDogMallet.Move.Slam&&sa.Bend<-20,"the slam in the air: raised overhead, arched back");
                slam.CastStep=1;slam.CastUntil=60.5f+NativeTopDog.SlamRecover;Check(Math.Abs(NativeTopDog.Landed(slam)-60.5f)<1e-4f&&NativeDogMallet.Sample(slam,60.5f+NativeDogMallet.HitKey(NativeDogMallet.Move.Slam),out var sl,out _,out _)&&sl.Floor>.99f&&sl.Squash<.75f,"landed, the slam's head on the floor");
                var smash=new NativeTopDogKit{CastKind=NativeTopDog.Smash,CastAt=70,CastRelease=70+NativeTopDog.SmashHang,CastUntil=72.6f};Check(smash.Valid()&&NativeTopDog.Smash.Length<=24&&NativeTopDog.AbilityName(NativeTopDog.Smash)=="Mallet Smash"&&NativeTopDog.MalletMove(NativeTopDog.Smash)&&NativeTopDog.Landed(smash)<-50,"the smash: valid on the wire, named, the mallet's own, in the air");
                var snd=new (string id,float gain)[8];Check(NativeTopDogSounds.Cast(NativeTopDog.Smash,0,snd)==1&&NativeTopDogSounds.Release(NativeTopDog.Smash,0,snd)==1,"the smash's sounds");
                var dash=new NativeTopDogKit{CastKind=NativeTopDog.Dash,CastAt=80,CastRelease=80+NativeTopDog.DashWind,CastUntil=80+NativeTopDog.DashWind+NativeTopDog.DashTime+NativeTopDog.DashRecover};
                Check(NativeDogMallet.Sample(dash,80+NativeTopDog.DashWind+NativeTopDog.DashTime*.45f,out var dp,out var dm,out _)&&dm==NativeDogMallet.Move.Dash&&(NativeDogMallet.GripAt(dp)-NativeDogMallet.GripAt(NativeDogMallet.At(dm,NativeDogMallet.HitKey(dm)))).Length()<1e-3f,"the dash's hit key on the rules' mid-charge moment");
            }
        }
        // The entrance's effects into the effects recorder.
        private sealed class FxRecorder:INativeEntranceFx
        {
            private readonly Recorder r;public FxRecorder(Recorder r){this.r=r;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>r.Glow(at,color,coverage,size,profile,u,seed);
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>r.Streak(head,velocity,color,width,minLength,blur,taper,u,seed);
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>r.Smoke(at,albedo,opacity,size,u,seed,floor);
        }
    }
}
