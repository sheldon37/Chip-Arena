#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // The guns' recorded layers (owner 2026-11-24, docs/GUN-SOUNDS.md; Core NativeGunSounds): the keys in the bank contract, the
    // close/distant blend and the tail by range, the travel delay, the reload parts on the first- and third-person reload clocks, the
    // rifle's bolt on its fire action, the casings by floor, the machine gun's release, and the runtime's hooks and fallbacks.
    internal static class GunSoundChecks
    {
        private static int count;
        private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Gun sounds: "+what);}
        private static bool Near(float a,float b,float e=1e-3f)=>Math.Abs(a-b)<=e;

        public static void Run(string root)
        {
            count=0;string[] guns={"pistol","rifle","machinegun"};
            // ---------- keys ----------
            var keys=NativeGunSounds.Keys;Check(keys.Length==29&&keys.Distinct().Count()==29,"29 distinct keys");
            foreach(var k in keys){Check(NativeSoundKeys.Find(k,out var def)&&def.Space==NativeSoundSpace.Positional,k+" is a positional key in the contract");}
            foreach(var g in guns)
            {
                Check(NativeGunSounds.Gun(g)&&keys.Contains(NativeGunSounds.Near(g))&&keys.Contains(NativeGunSounds.Far(g))&&keys.Contains(NativeGunSounds.Tail(g)),g+" layers");
                Check(NativeSoundKeys.Find(NativeGunSounds.Tail(g),out var tail)&&tail.Priority==NativeSoundPriority.Low,g+"'s tail yields its voice first");
                Check(NativeGunSounds.Reload(g).Length>=2&&NativeGunSounds.Reload(g).All(p=>keys.Contains(p.Key)),g+" reload parts");
                foreach(var floor in new[]{null,NativeFootsteps.Concrete,NativeFootsteps.Stone,NativeFootsteps.Metal,NativeFootsteps.Wood,NativeFootsteps.Gravel})Check(keys.Contains(NativeGunSounds.Casing(g,floor)),g+" casing on "+(floor??"an unknown floor"));
                Check(NativeGunSounds.Casing(g,NativeFootsteps.Snow)==null,g+": snow swallows the casing");
                Check(NativeGunSounds.Casing(g,null)==NativeGunSounds.Casing(g,NativeFootsteps.Stone)&&NativeGunSounds.Casing(g,null).EndsWith(".concrete"),g+": stone and unknown floors take concrete");
                Check(NativeSoundKeys.Find(NativeGunSounds.Near(g),out var near)&&NativeSoundKeys.Find("gun."+g+".shot",out var today)&&near.Priority==today.Priority,g+": the close layer keeps today's shot priority");
            }
            Check(!NativeGunSounds.Gun("bow")&&NativeGunSounds.Near("fire")==null&&NativeGunSounds.Casing("bow",null)==null&&NativeGunSounds.Reload("sword").Length==0,"other weapons keep their sounds");
            Check(NativeSoundKeys.Find("gun.machinegun.near",out var mg)&&mg.Cap<=.25f,"the machine gun's close layer stays short at its .085 s interval");
            // ---------- range ----------
            Check(NativeGunSounds.NearWeight(5)==1&&NativeGunSounds.FarWeight(5)==0&&Near(NativeGunSounds.NearWeight(80),0)&&Near(NativeGunSounds.FarWeight(80),1),"close at 5 m, distant at 80 m");
            float lastNear=2,lastFar=-1,lastTail=0;
            for(float d=0;d<=100;d+=.5f)
            {
                float n=NativeGunSounds.NearWeight(d),f=NativeGunSounds.FarWeight(d),t=NativeGunSounds.TailWeight(d),fall=NativeEventSounds.Falloff(d);
                Check(Near(n*n+f*f,1,1e-4f),"equal-power blend at "+d);Check(n<=lastNear+1e-6f&&f>=lastFar-1e-6f,"the blend moves one way at "+d);
                Check(t>=lastTail-1e-5f,"the tail's share grows with range at "+d);
                Check(t*fall>=.2f&&t*fall<=.5f&&t<=NativeGunSounds.TailMax,"the tail stays heard at "+d+" m ("+t*fall+")");
                lastNear=n;lastFar=f;lastTail=t;
            }
            Check(Near(NativeGunSounds.Delay(80),80f/343)&&NativeGunSounds.Delay(5)<.02f&&NativeGunSounds.Delay(1000)==NativeGunSounds.MaxDelay&&NativeGunSounds.Delay(float.NaN)==0,"the travel delay");
            float interval=GunRules.Get(WeaponId.MachineGun).Interval;Check(Near(interval,.085f)&&NativeGunSounds.ReleaseAfter(interval)>interval*1.4f&&NativeGunSounds.ReleaseAfter(interval)<interval*2,"the stream's release waits past one interval, under two");
            Check(NativeGunSounds.Stream("machinegun")&&!NativeGunSounds.Stream("pistol")&&NativeGunSounds.TailGap<GunRules.Get(WeaponId.Pistol).Interval,"the semi-automatic guns ring a tail each shot");
            // ---------- reload parts on the clips' clocks ----------
            var rng=new Random(3);
            for(int i=0;i<200;i++){float v=(float)rng.NextDouble();float u=NativeGunSounds.Unwarp(NativeTacticalGun.Pistol.ReloadWarp,v);Check(Near(NativeTacticalTiming.Warp(NativeTacticalGun.Pistol.ReloadWarp,u),v,1e-4f),"the warp's inverse at "+v);}
            foreach(var g in guns)
            {
                var w=NativeGunSounds.Weapon(g);float span=GunRules.Get(w).Reload,at=10;var tgun=NativeTacticalGun.For(w);
                var tp=g=="pistol"?NativeTacticalMotion.Pistol:g=="rifle"?NativeTacticalMotion.Rifle:NativeTacticalMotion.MachineGun;
                float clip=g=="pistol"?NativeGunSounds.PistolClip:g=="rifle"?NativeGunSounds.RifleClip:NativeGunSounds.MachineGunClip;
                foreach(bool fp in new[]{true,false})
                {
                    float last=-1;
                    foreach(var part in NativeGunSounds.Reload(g))
                    {
                        float u=NativeGunSounds.ReloadShare(g,part.At,fp),time=at+u*span;Check(u>last&&u>0&&u<1,g+" "+part.Key+" inside the reload, in order ("+(fp?"first":"third")+" person)");last=u;
                        float clock=fp?NativeTacticalTiming.ReloadClipTime(time,at,at+span,clip,tgun.ReloadWarp):NativeTacticalMotion.ReloadTime(tp,time,at,at+span);
                        Check(Near(clock,part.At,2e-3f),g+" "+part.Key+" lands on its clip moment "+part.At+" ("+(fp?"first":"third")+" person: "+clock+")");
                    }
                }
            }
            // ---------- the rifle's bolt ----------
            {
                float rule=GunRules.Get(WeaponId.Rifle).Interval,action=NativeTacticalMotion.Rifle.FireReady;
                foreach(float iv in new[]{rule,.9f,1.6f})
                {
                    float fpAge=NativeGunSounds.BoltAge(NativeGunSounds.BoltAt,iv,true),tpAge=NativeGunSounds.BoltAge(NativeGunSounds.BoltAt,iv,false);
                    Check(Near(NativeTacticalTiming.FireActionTime(fpAge,0,iv,action,action+.5f),NativeGunSounds.BoltAt,1e-3f),"first person: the bolt on the fire action at "+iv);
                    Check(Near(NativeTacticalMotion.FireTime(NativeTacticalMotion.Rifle,tpAge,iv),NativeGunSounds.BoltAt,1e-3f),"third person: the bolt on the fire clip at "+iv);
                    Check(NativeGunSounds.BoltAge(NativeGunSounds.BoltAt+.75f,iv,true)<=iv+.05f,"the bolt is home by the next shot at "+iv);
                    Check(NativeGunSounds.CasingAge("rifle",iv,true,.5f)>fpAge&&Near(NativeGunSounds.CasingAge("pistol",iv,true,.5f),NativeGunSounds.CasingFall),"cases land after the shot, the rifle's after its bolt opens");
                }
            }
            // ---------- runtime ----------
            {
                string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
                string audio=R("NativeAudio.cs"),gun=R("NativeGunAudio.cs"),game=R("ConversionGame.cs");
                Check(audio.Contains("if(GunEvent(arena,e,source,pos,arena.Time-e.Time,context.SourceMe))plays=0;")&&audio.Contains("GunFrame(arena,me,menu);"),"NativeAudio hooks the gun layers");
                Check(gun.Contains("if(e.Kind!=EventKind.Shot||!bank.Has(NativeGunSounds.Near(spec)))return false;")&&gun.Contains("return parts.Length>0&&bank.Has(parts[0].Key);")&&gun.Contains("if(menu||bank.Count==0)return;"),"without the gun's clips today's shot and reload cues play");
                Check(!gun.Contains("AddComponent")&&!gun.Contains("new AudioSource")&&gun.Contains("BankPlay(NativeGunSounds.Near(spec),at,"),"the layers play on the bank's voices");
                Check(!gun.Contains(".Ammo")&&!gun.Contains("Emit(")&&!gun.Contains("ReloadEnd=")&&!gun.Contains("ReloadAt=")&&!gun.Contains("LastShot="),"presentation only: no pilot state written");
                Check(game.Contains("nativeAudio.FirstPerson=presentation.FirstPerson;nativeAudio.HulkWarning="),"the view's first person reaches the audio");
            }
            Console.WriteLine("PASS gun sounds: "+count+" checks: 29 keys in the contract, the close/distant blend and the tail by range, the travel delay, the reload parts on the first- and third-person clocks, the rifle's bolt, the casings by floor, the machine gun's release, hooks and fallbacks");
        }
    }
}
#endif
