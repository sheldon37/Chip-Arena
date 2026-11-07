#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Entrance music from the licensed bank (owner 2026-10-12: new music for every non-DEV entrance). The clips are never in this
    // repository: they are intro.<style> keys of the effects bank's manifest in the git-ignored Assets/LocalAssets
    // (tools/install_local_assets.py). Always checked: one intro key per non-DEV entrance (2D, 4.8 s, the entrance's length) and
    // none for the DEV ones; the clip choice (first listed clip, gain trim) with the bank absent, empty, filled and broken; every
    // keyed entrance keeps a built-in sting to fall back on; text scans that NativeAudio plays the bank clip or else today's
    // sting on the same voice, volume, intro-age seek and late-join guard, and that the Siren keeps its boss song. With
    // LocalAssets present: every installed intro clip is 4.5-4.8 s, 44.1 kHz, mono or stereo.
    internal static class IntroMusicChecks
    {
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro music: "+what);}
        public static IEnumerable<string> Run(string root)
        {
            var passed=new List<string>();
            // ---------- key contract ----------
            var styles=NativeIntroRules.Entrances.Where(s=>!s.StartsWith("dev-",StringComparison.Ordinal)).ToArray();
            // The owner's DEV Sheldon Intro has its own music too (intro.sheldon-intro, last): its id carries no dev- prefix, so the
            // dev- entrances still have none (its DEV gate is the cosmetic rules', NativeCosmeticGate).
            var keyed=styles.Append(NativeSheldonIntro.Style).ToArray();
            var intro=NativeSoundKeys.All.Where(k=>NativeSoundKeys.IsIntro(k.Key)).ToArray();
            Check(styles.Length==19&&intro.Length==keyed.Length&&intro.Select(k=>k.Key).SequenceEqual(keyed.Select(s=>"intro."+s)),"one intro key per non-DEV entrance and the Sheldon Intro, in the entrance order ("+intro.Length+")");
            Check(NativeSoundKeys.Intro(NativeSheldonIntro.Style)=="intro.sheldon-intro"&&!NativeSheldonIntro.Style.StartsWith("dev-",StringComparison.Ordinal)&&NativeCosmeticGate.DevOnly(NativeSheldonIntro.Style),"the Sheldon Intro's key is intro.sheldon-intro and the intro stays DEV-only");
            foreach(var k in intro)
            {
                string style=k.Key.Substring(NativeSoundKeys.IntroPrefix.Length);
                Check(k.Space==NativeSoundSpace.Flat&&k.Priority==NativeSoundPriority.High&&Math.Abs(k.Cap-NativeIntroRules.Duration(style))<1e-4,k.Key+" is 2D with the entrance's 4.8 s cap");
                Check(NativeSoundKeys.Intro(style)==k.Key,k.Key+" maps from its style");
            }
            foreach(var s in new[]{"dev-emperor","dev-ansatsuken","dev-matrix","dev-siren",null,"","neon-drop","pulse-wave","weapon-siren","weapon-emperor","intro.weapon-sword"})Check(NativeSoundKeys.Intro(s)==null,(s??"null")+" has no intro key");
            Check(NativeSoundKeys.IsIntro("intro.blackout")&&!NativeSoundKeys.IsIntro("ui.go")&&!NativeSoundKeys.IsIntro(null),"intro key prefix");
            passed.Add("intro music: "+intro.Length+" intro keys, one per non-DEV entrance and the DEV Sheldon Intro (2D, 4.8 s cap, high priority); the 4 dev- entrances, retired and unknown styles have none");

            // ---------- the clip choice ----------
            var everyStyle=NativeIntroRules.Entrances.Concat(NativeIntroRules.Retired).Concat(new string[]{null,"","weapon-siren"}).ToArray();
            NativeSoundBank absent=NativeSoundBank.Parse(null),empty=NativeSoundBank.Parse("{\"schema\":1,\"events\":{"+string.Join(",",styles.Select(s=>"\"intro."+s+"\":{\"clips\":[]}"))+"}}");
            Check(empty.Errors.Count==0,"empty intro entries parse: "+string.Join("; ",empty.Errors));
            foreach(var b in new[]{absent,empty})foreach(var s in everyStyle){Check(b.IntroClip(s,out float g)==null&&g==1,(s??"null")+": no installed clip, today's sting at gain 1");}
            string Entry(string s,int i)=>"\"intro."+s+"\":{\"clips\":[\"pick-"+i+"\",\"alt-a\",\"alt-b\"]"+(i==3?",\"gain\":0.8":"")+",\"space\":\"2d\",\"maxLength\":4.8}";
            var filled=NativeSoundBank.Parse("{\"schema\":1,\"events\":{"+string.Join(",",styles.Select((s,i)=>Entry(s,i)))+",\"ui.go\":{\"clips\":[\"0\"]}}}");
            Check(filled.Errors.Count==0&&filled.Count==styles.Length+1,"a filled intro manifest parses: "+string.Join("; ",filled.Errors.Take(3)));
            for(int i=0;i<styles.Length;i++)
            {
                string path=filled.IntroClip(styles[i],out float g);
                Check(path=="EffectsAudio/intro."+styles[i]+"/pick-"+i&&Math.Abs(g-(i==3?.8f:1))<1e-6,styles[i]+": the first listed clip plays with its gain trim ("+path+")");
                Check(!filled.Entry("intro."+styles[i]).Positional&&filled.Entry("intro."+styles[i]).Count==3,styles[i]+" keeps its alternates, 2D");
            }
            foreach(var s in new[]{"dev-emperor","dev-ansatsuken","dev-matrix","dev-siren","neon-drop",null})Check(filled.IntroClip(s,out float g)==null&&g==1,(s??"null")+" keeps its built-in music with a filled bank");
            // The bank's variation bag never picks an intro clip: the intro voice always takes the first, so the choice is stable.
            for(int n=0;n<10;n++)Check(filled.IntroClip("weapon-sword",out _)=="EffectsAudio/intro.weapon-sword/pick-0","the same clip every time");
            string Broken(string entry)=>"{\"schema\":1,\"events\":{"+entry+"}}";
            foreach(var (json,why) in new[]{(Broken("\"intro.weapon-sword\":{\"clips\":[\"0\"],\"space\":\"3d\"}"),"positional space"),(Broken("\"intro.weapon-sword\":{\"clips\":[\"0\"],\"maxLength\":5.2}"),"longer than the entrance"),
                (Broken("\"intro.dev-siren\":{\"clips\":[\"0\"]}"),"a DEV entrance key"),(Broken("\"intro.weapon-sword\":{\"clips\":[\"Sword Pick\"]}"),"clip name"),(Broken("\"intro.weapon-sword\":{\"clips\":[\"0\"],\"gain\":-1}"),"gain range")})
            {var b=NativeSoundBank.Parse(json);Check(b.Errors.Count>0&&b.IntroClip("weapon-sword",out float g)==null&&g==1&&b.IntroClip("dev-siren",out _)==null,"rejects "+why+", keeping today's sting");}
            passed.Add("intro music: the first listed clip plays with its gain trim for all 19 keyed entrances; bank absent, every entry empty and 5 broken entries fall back to today's sting at gain 1; DEV entrances never take a bank clip");

            // ---------- the built-in fallback stays ----------
            int stings=0;
            foreach(var s in keyed)
            {
                string sting=NativeIntroRules.IntroSting(s);Check(sting!=null,s+" has a built-in sting");
                Check(File.Exists(Path.Combine(root,"Assets/Conversion/Resources/Audio/"+sting+".mp3")),s+": "+sting+".mp3 is still in the project");stings++;
            }
            Check(NativeIntroRules.IntroSting("dev-siren")==null&&NativeIntroRules.IntroSong("dev-siren")=="music/black-siren-protocol","the Siren plays its boss song, never a sting");
            passed.Add("intro music: all "+stings+" keyed entrances keep their built-in sting mp3, so builds without LocalAssets play exactly today's music");

            // ---------- runtime routing (text scans: Runtime is not compiled here) ----------
            {
                string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
                string audio=R("NativeAudio.cs"),fx=R("NativeEffectsAudio.cs");
                Check(audio.Contains("if(key==null||sting==null||Muted||Music<=0){introVoice.Stop();introKey=\"\";return;}introVoice.volume=Mathf.Clamp01(Music*introGain);"),"Entrance keeps its stop rules and the music volume (gain 1 without a bank clip)");
                Check(audio.Contains("if(introKey==key)return;introKey=key;introVoice.Stop();introVoice.clip=IntroClip(style,sting,out introGain);introVoice.volume=Mathf.Clamp01(Music*introGain);if(introVoice.clip==null||age>=introVoice.clip.length)return;introVoice.time=Math.Max(0,age);introVoice.Play();"),
                    "Entrance plays the chosen clip once per key on the intro voice, seeking to the intro age (late joiners) and skipping a finished one");
                Check(audio.Contains("private float introGain=1;"),"the intro gain starts at 1");
                Check(audio.Contains("string song=NativeIntroRules.IntroSong(style);")&&audio.Contains("if(song==null){Entrance(key,style,age);HoldMusic();return;}")&&audio.Contains("Entrance(null,null,0);string from=track;"),"the Locker/Customize preview plays the same entrance music; the Siren preview keeps its song");
                Check(fx.Contains("if(!bankLoaded)LoadBank();string path=bank.IntroClip(style,out gain);var clip=path!=null?BankClip(path):null;")&&fx.Contains("if(clip!=null&&clip.loadState!=AudioDataLoadState.Failed)return clip;")&&fx.Contains("gain=1;return Clip(sting);"),
                    "IntroClip takes the bank clip only when it loads, else today's sting at gain 1");
                Check(fx.Contains("n=NativeSoundKeys.IsIntro(e.Key)?Math.Min(1,e.Count):e.Count;"),"only the playing intro clip preloads");
                Check(!fx.Contains("BankPlay(NativeSoundKeys.Intro")&&!audio.Contains("Cue(NativeSoundKeys.Intro"),"intro clips never play as effects");
                passed.Add("intro music: NativeAudio.Entrance and the Locker preview play the bank clip or else today's sting on the intro voice with the same volume, intro-age seek and late-join guard; the Siren keeps its boss song");
            }

            // ---------- LocalAssets ----------
            string local=Path.Combine(root,NativeSoundKeys.LocalRoot,"manifest.json");
            if(!File.Exists(local))passed.Add("intro music: LocalAssets absent, so every entrance plays its built-in sting");
            else
            {
                var installed=NativeSoundBank.Parse(File.ReadAllText(local));string folder=Path.GetDirectoryName(local);int clips=0,withMusic=0;
                foreach(var s in keyed)
                {
                    var e=installed.Entry("intro."+s);if(e==null||e.Count==0)continue;withMusic++;
                    foreach(var c in e.Clips)
                    {
                        string file=Path.Combine(folder,e.Key,c+".ogg");
                        Check(EffectsAudioChecks.OggInfo(file,out double seconds,out int channels,out int rate),e.Key+"/"+c+" is Ogg Vorbis");
                        Check(seconds>=4.5&&seconds<=NativeIntroRules.Duration(s)+.02&&rate==44100&&channels>=1&&channels<=2,e.Key+"/"+c+": "+seconds.ToString("0.000")+" s, "+channels+" ch, "+rate+" Hz (4.5-4.8 s, 44.1 kHz)");clips++;
                    }
                }
                passed.Add("intro music: LocalAssets present: "+withMusic+" of "+keyed.Length+" entrances have bank music ("+clips+" clips, defaults first), each 4.5-4.8 s at 44.1 kHz; the rest play their built-in sting");
            }
            return passed;
        }
    }
}
#endif
