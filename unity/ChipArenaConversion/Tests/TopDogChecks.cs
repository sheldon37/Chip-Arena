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
            // ---- the wiring (the Runtime is read as text: CoreTests compiles Core only) ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var arena=R("NativeArena.cs");Check(arena.Contains("StepBombInput(p);StepDogInput(p);")&&arena.Contains("StepBombWorld();StepDogWorld();")&&arena.Contains("if(PinDogVictim(p))return;")&&arena.Contains("BombPending()||DogPending()")&&arena.Contains("DogMotion(p,ref velocity);")&&arena.Contains("DogDamaged(target,actual);")&&arena.Contains("a.Dog=p.Seat.spec==NativeTopDog.Spec?p.Dog.ViewCopy():null;"),"the rules are stepped, held, waited for, moved, hurt and snapshotted");
            Check(R("NativeFrameCodec.cs").Contains("a.HasDog=a.Dog!=null;a.Dog?.CapturePresence();")&&R("NativeNetworkFrame.cs").Contains("a.Dog!=null&&!a.Dog.Valid()"),"the record crosses the wire and is checked");
            Check(R("NativeDeaths.cs").Contains("if(p.DogPopped)style=NativeTopDog.Burst;")&&R("NativeSmashPresentation.cs").Contains("NativeSmashGib.Burst(")&&R("NativeDeathPresentation.cs").Contains("SmashCause.Burst"),"the pop records its style and the corpse bursts");
            var pres=R("ArenaPresentation.cs");Check(pres.Contains("DogBegin(s,renderTime);")&&pres.Contains("RenderDog(s,renderTime);"),"the presentation reads and draws it");
            Check(R("NativeTechniquePresentation.cs").Contains("DogFx(now);")&&R("NativeCinematicCamera.cs").Contains("presentation.DogFeastDirect(")&&R("NativeVisualBodyPresentation.cs").Contains("TopDogBody(body,a,time);")&&R("NativeFightHud.cs").Contains("DogCards(p);")&&R("ConversionGame.cs").Contains("DogScreen(width,height);")&&R("NativeAudio.cs").Contains("DogMoments(p,pos,t,mine);"),"its effects, camera, body, cards, screen and sound are hooked");
            Check(R("ConversionGame.cs").Contains("KeyCode.Alpha6"),"key 6 is read");
            Check(arena.Contains("DogSave(target,amount))return 0;")&&R("NativeStructures.cs").Contains("NativeDogChips.Apply(content);foreach(var m in NativeDogChips.Mechanics)NativeArena.SupportedMechanics.Add(m);")&&R("NativeChipCard.cs").Contains("NativeDogChipArt.Face(")&&R("NativeDraftScreen.cs").Contains("NativeDogChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);")&&R("NativeAudio.cs").Contains("DogPick(match.Chosen,"),"the chips' rules, rows, cards, pick and sound are wired");
            foreach(var file in new[]{"NativeTopDogPresentation.cs","NativeTopDogScreen.cs","NativeDogChipArt.cs"}){var text=R(file);Check(!text.Contains("Damage(")&&!text.Contains("Knockback")&&!text.Contains("SendInput"),file+": presentation only");}
            Console.WriteLine("PASS top dog: "+count+" checks (the row and key 6, the record through the preview, the feast and the belly, the effects, "+NativeToonSynth.Ids.Length+" toon sounds, the models, the burst gib, Order Up!, the ten chips, the wiring)");
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
