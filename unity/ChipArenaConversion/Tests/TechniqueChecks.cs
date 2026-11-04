using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The weapon techniques' presentation (docs/WEAPON-TECHNIQUES.md): the table against the content's weapons, the record the rules will own, and
    /// the effects as pure functions of it over each cast's whole life: something drawn while it lives, nothing after, nothing broken, the same frame
    /// for the same clock, thinner on Reduced. The clips the poses name, and that the presentation adds nothing to the wire or the rules.
    /// </summary>
    internal static class TechniqueChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Techniques: "+what);count++;}
        private sealed class Recorder:ITechniqueFxSink
        {
            public int Draws;public float Hash;public bool Broken;
            private void P(Vector3 v,float x=1){if(!(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z))||v.Length()>1000)Broken=true;Hash=Hash*.99f+(v.X+v.Y*3+v.Z*7)*x*.001f;Draws++;}
            private void F(float v){if(!float.IsFinite(v))Broken=true;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){P(at);P(color,.1f);F(size);F(u);F(coverage);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){P(head);P(velocity,.1f);F(width);F(u);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){P(at);F(opacity);F(size);F(u);if(opacity<0)Broken=true;}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){P(at);F(size);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){P(tail);P(head);F(width);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){P(a);P(b);P(c);P(d);F(ua);F(ub);}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){P(at);F(opacity);F(size);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){P(at);P(segment);F(opacity);}
            public void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){P(head);P(segment);F(width);}
            public void Tongue(bool view,Vector3 tail,Vector3 head,Vector3 color,float width){P(tail);P(head);F(width);}
        }
        private sealed class Scene:IKitFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;
            public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+new Vector3(0,1.25f,0);return true;}
            public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=Feet(a)+new Vector3(.3f,1,.2f);tip=root+new Vector3(0,.8f,.4f);return !left;}
            public int Viewer=>0;public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int WeaponColor(Actor a)=>0xffffff;public int Shots=>0;public bool Shot(int i,out Vector3 at,out Vector3 v,out string fx,out int owner){at=v=default;fx=null;owner=0;return false;}
        }
        private sealed class One:ITechniqueSource{public NativeTechnique K;public NativeTechnique Of(int actor)=>actor==1?K:null;}
        public static void Run(string root)
        {
            count=0;var content=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")));
            var specs=new HashSet<string>();foreach(var w in content.RootElement.GetProperty("weapons").EnumerateArray())specs.Add(w.GetProperty("id").GetString());
            string[] standard={"pistol","rifle","machinegun","bow","fire","water","lightning","darkness","sword","daggers","shield","greatsword"};
            Check(NativeTechniques.All.Length==12&&NativeTechniques.All.Select(d=>d.Spec).OrderBy(x=>x).SequenceEqual(standard.OrderBy(x=>x)),"a technique for each of the twelve standard weapons, and no DEV class");
            Check(NativeTechniques.All.Select(d=>d.Id).Distinct().Count()==12&&NativeTechniques.All.Select(d=>d.Name).Distinct().Count()==12,"each its own id and name");
            foreach(var d in NativeTechniques.All)
            {
                Check(specs.Contains(d.Spec),d.Id+": its weapon exists");Check(d.Cooldown>=9&&d.Cooldown<=20,d.Id+": a long cooldown ("+d.Cooldown+" s)");
                Check(NativeTechniques.For(d.Spec)==d&&NativeTechniques.Find(d.Id)==d,d.Id+": found by weapon and id");
                // Dearer than the same weapon's Q: it is no new damage button.
                foreach(var a in content.RootElement.GetProperty("abilities").EnumerateArray())if(a.GetProperty("spec").GetString()==d.Spec&&a.GetProperty("slot").GetString()=="q"&&a.TryGetProperty("cooldown",out var q)&&q.GetDouble()>0)Check(d.Cooldown>q.GetDouble(),d.Id+": a longer cooldown than the weapon's Q ("+q.GetDouble()+" s)");
            }
            Check(NativeTechniques.For("siren")==null&&NativeTechniques.For("cyberlord")==null&&NativeTechniques.For("leet")==null,"not the DEV classes");
            Effects();Poses();Wiring(root);
            NativeTechniques.Source=null;
            Console.WriteLine("PASS techniques ("+count+" checks: the twelve against the content, each cast's record chronological and its effects whole, deterministic, gone after, thinned on Reduced; the clips the poses name; no rule or wire touched)");
        }
        private static void Effects()
        {
            var fx=new NativeTechniqueFx();var scene=new Scene();var src=new One();NativeTechniques.Source=src;
            var a=new Actor{Id=1,Hp=100,SourceSpec="sword",Position=new V3(2,.5f,-3)};scene.List.Add(a);
            foreach(var d in NativeTechniques.All)for(int variant=0;variant<2;variant++)
            {
                var k=new NativeTechnique();NativeTechniquePreview.Write(k,d.Id,new Vector3(2,.5f,-3),.7f,10,variant==0);src.K=k;a.SourceSpec=d.Spec;string n=d.Id+(variant==0?"":" (second)");
                Check(k.Valid(),n+": a valid record");Check(k.Kind==d.Id&&k.Release>=k.At&&k.Until>=k.Release&&k.End>=k.Until,n+": its times run forward");Check(NativeTechniquePreview.Period(d.Id)>k.End-k.At,n+": the preview's loop outlasts the cast ("+(k.End-k.At).ToString("0.0")+" s)");
                int drawn=0,frames=0;
                for(float t=k.At;t<k.End;t+=1/30f)
                {
                    var r=new Recorder();fx.Draw(r,scene,t,1,false);frames++;Check(!r.Broken,n+": nothing broken at "+(t-k.At).ToString("0.00"));if(r.Draws>0)drawn++;
                    var again=new Recorder();fx.Draw(again,scene,t,1,false);Check(again.Draws==r.Draws&&Math.Abs(again.Hash-r.Hash)<1e-4f,n+": the same clock draws the same frame (host, guests, spectators, the killcam)");
                    var thin=new Recorder();fx.Draw(thin,scene,t,.3f,true);Check(thin.Draws<=r.Draws&&!thin.Broken,n+": Reduced and the low tier draw no more");
                }
                if(d.Id!=NativeTechniques.Riposte||variant==0)Check(drawn>frames*.5f,n+": draws through its life ("+drawn+" of "+frames+" frames)");
                var after=new Recorder();fx.Draw(after,scene,k.End+NativeTechniqueFx.Tail+.01f,1,false);Check(after.Draws==0,n+": nothing left once it is over");
                var before=new Recorder();fx.Draw(before,scene,k.At-.01f,1,false);Check(before.Draws==0,n+": nothing before the press");
                // A record from an older clock (a rewind): no throw, the moments still ordered.
                for(int m=0;m<NativeTechniqueSounds.Moments;m++){float at=NativeTechniqueSounds.At(k,m);if(!float.IsNaN(at))Check(at>=k.At,n+": sound moment "+m+" within the cast");var o=new NativeKitSound[4];int c=NativeTechniqueSounds.Moment(d.Id,m,o,out _);Check(c>=0&&c<=4,n+": its cues");for(int i=0;i<c;i++)Check(!string.IsNullOrEmpty(o[i].Key),n+": a cue key");}
                bool any=false;for(int m=0;m<NativeTechniqueSounds.Moments;m++){var o=new NativeKitSound[4];if(NativeTechniqueSounds.Moment(d.Id,m,o,out _)>0)any=true;}Check(any,n+": it sounds");
            }
            // The solid things' own numbers.
            var b=new NativeTechnique();NativeTechniquePreview.Write(b,NativeTechniques.AnchorThrow,Vector3.Zero,0,10,true);var catcher=new Vector3(0,1.2f,0);
            var mid=NativeTechniques.BladeAt(b,(b.Release+b.Arm)*.5f,catcher,out float f0);Check(f0>.4f&&f0<.6f&&mid.Z>.5f,"the blade flies its line");var stuck=NativeTechniques.BladeAt(b,b.Arm+.2f,catcher,out float f1);Check(f1==1&&Math.Abs(stuck.Z-4.2f)<.01f,"the blade stands where it stuck");
            var c2=new NativeTechnique();NativeTechniquePreview.Write(c2,NativeTechniques.AnchorThrow,Vector3.Zero,0,10,false);var home=NativeTechniques.BladeAt(c2,c2.Until-.001f,catcher,out _);Check(Vector3.Distance(home,catcher)<.2f,"unused, it flies back to the pilot");
            Check(NativeTechniques.WeaponAway(b,b.Release+.1f)&&!NativeTechniques.WeaponAway(b,b.At)&&!NativeTechniques.WeaponAway(b,b.Until+.01f),"the blade leaves the hand only while it is thrown");
            var s=new NativeTechnique();NativeTechniquePreview.Write(s,NativeTechniques.SmokeVeil,Vector3.Zero,0,10,true);Check(!NativeTechniques.WeaponAway(s,s.Release+.1f),"other techniques keep the weapon");
            var coin=new NativeTechnique();NativeTechniquePreview.Write(coin,NativeTechniques.RicochetCoin,Vector3.Zero,0,10,true);Check(NativeTechniques.CoinAt(coin,coin.Release+.5f).Y>coin.From.y+1,"the coin climbs");Check(coin.HitAt<coin.Until&&Math.Abs(NativeTechniques.CoinAt(coin,coin.HitAt).Y-coin.Point.y)<1e-3f,"the bullet meets it where it is");
        }
        private static void Poses()
        {
            var sets=new[]{"sword","daggers","shield","greatsword","pistol","rifle","machinegun","bow","fire","water","lightning","darkness"};
            foreach(var spec in sets){var set=NativeVisualMotion.For(spec);Check(set!=null&&set.All.Length>0,spec+": a model set");Check(set.TechClips==null||set.TechClips.All(c=>set.All.Contains(c)),spec+": the bake carries its technique's clips");}
            Check(NativeVisualMotion.For("sword").TechClips.Contains("Sword-Attack-R4")&&NativeVisualMotion.For("shield").TechClips.Contains("Armed-Shield-Activate"),"the clips the moves play");
        }
        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            var arena=R("Core/ArenaModel.cs");Check(arena.Contains("NativeTechnique Technique"),"authority technique record on the actor");
            var hud=R("Runtime/NativeFightHud.cs");Check(hud.Contains("if(NativeTechniques.Source!=null&&NativeTechniques.For(p.Seat.spec) is var td&&td!=null)"),"the HUD's card shows only once something holds the records");
            var box=R("Runtime/NativeSandbox.cs");Check(box.Contains("case \"gallery-techniques\":presentation.TechniquePreviewOn=!presentation.TechniquePreviewOn;return DevRan;"),"the preview is the Dev Sandbox's");
            var pres=R("Runtime/NativeTechniquePresentation.cs");foreach(var bad in new[]{"arena.Sandbox","SendInput","Damage(","QReady","EReady"})Check(!pres.Contains(bad),"the presentation does not reach "+bad);
        }
    }
}
