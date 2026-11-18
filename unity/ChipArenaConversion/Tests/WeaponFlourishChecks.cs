using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ChipArena.UnityConversion;
using Plan=ChipArena.UnityConversion.NativeWeaponFlourishPlan;

// The Blind Pick weapon flourishes (owner 2026-11-18: "Give each weapon choice a selection animation for the player"): every
// roster weapon has its own move of 1-2 s and a finisher under a second, each sound is a real, short, quiet cue and plays once,
// and each pilot sees only their own (the reveal's other cards are quiet). Presentation only.
internal static class WeaponFlourishChecks
{
    public static void Run(string root)
    {
        int checks=0;
        void Check(bool passed,string message){if(!passed)throw new Exception("WeaponFlourish: "+message);checks++;}
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
        var seen=new HashSet<Plan.Move>();
        foreach(var w in content.weapons)
        {
            var m=Plan.Of(w.id);Check(m!=Plan.Move.Generic,w.id+" has its own flourish");Check(seen.Add(m),w.id+"'s flourish is its own (not shared)");
            float len=Plan.Length(m);Check(len>=1&&len<=2,w.id+" plays 1-2 s ("+len+")");Check(Plan.Length(m,true)<=1,w.id+"'s gentle version is short");
        }
        Check(content.weapons.Length>=24&&Plan.Count==content.weapons.Length,"every roster row, and only those, is planned ("+Plan.Count+" for "+content.weapons.Length+")");
        for(int i=0;i<Plan.Count;i++){string id=Plan.Id(i);Check(Array.Exists(content.weapons,w=>w.id==id),"planned "+id+" is a roster weapon");}
        Check(Plan.Of("not-a-weapon")==Plan.Move.Generic&&Plan.Of(null)==Plan.Move.Generic,"an unknown weapon plays the generic flourish");
        Check(Plan.Finish<=1&&Plan.Gentle<=1,"the lock-in finisher is short");

        // The sounds: bank keys that exist and are one-shots, fallback clips that ship, toon sounds the synth makes; quiet,
        // inside the flourish, in time order.
        string clips=Path.Combine(root,"Assets/Conversion/Resources/Audio/audio");
        foreach(Plan.Move m in Enum.GetValues(typeof(Plan.Move)))
        {
            foreach(bool finish in new[]{false,true})
            {
                var cues=finish?Plan.FinishCues(m):Plan.Cues(m);float len=finish?Plan.Finish:Plan.Length(m),last=-1;
                if(m!=Plan.Move.Generic)Check(cues.Length>0,m+(finish?" finisher":"")+" has a sound");
                foreach(var c in cues)
                {
                    Check(c.At>=0&&c.At<=len&&c.At>=last,m+" cue at "+c.At+" lies in the flourish, in order");last=c.At;
                    Check(c.Gain>0&&c.Gain<=.25f,m+" cue "+(c.Key??c.Toon)+" is quiet ("+c.Gain+")");
                    if(c.Toon!=null){Check(NativeToonSynth.Known(c.Toon)&&c.Key==null&&c.Clip==null,m+" toon "+c.Toon+" exists");continue;}
                    Check(c.Key!=null||c.Clip!=null,m+" cue names a sound");
                    if(c.Key!=null){Check(NativeSoundKeys.Find(c.Key,out var def),m+" key "+c.Key+" is a bank key");Check(def.Space!=NativeSoundSpace.Loop&&def.Cap<=2.5f,m+" key "+c.Key+" is a short one-shot");}
                    if(c.Clip!=null)Check(File.Exists(Path.Combine(clips,c.Clip+".wav")),m+" fallback clip "+c.Clip+" ships");
                }
            }
        }

        // The clock cues each sound once at any frame rate, restarts with a new flourish, and a flourish seen late skips its
        // earlier sounds.
        var clock=new Plan.CueClock();
        foreach(float fps in new[]{30f,60f,144f,23f})
        {
            foreach(Plan.Move m in Enum.GetValues(typeof(Plan.Move)))
            {
                var cues=Plan.Cues(m);var fired=new int[cues.Length];float dt=1/fps;clock.Reset();
                for(int pass=0;pass<2;pass++)
                {
                    float start=100+pass*5;Array.Clear(fired,0,fired.Length);
                    for(float age=dt*.37f;age<=Plan.Length(m)+.2f;age+=dt){clock.Step(m,start,age,dt);for(int i=0;i<cues.Length;i++)if(clock.Due(cues[i]))fired[i]++;}
                    for(int i=0;i<cues.Length;i++)Check(fired[i]==1,m+" cue "+i+" plays once at "+fps+" fps (pass "+pass+": "+fired[i]+")");
                }
            }
        }
        var pistol=Plan.Cues(Plan.Move.Pistol);clock.Reset();clock.Step(Plan.Move.Pistol,10,.9f,1/60f);int late=0;foreach(var c in pistol)if(clock.Due(c))late++;
        Check(late==0,"a flourish first seen late does not fire its earlier shots at once");
        clock.Step(Plan.Move.Pistol,10,1.01f,1/60f);late=0;foreach(var c in pistol)if(clock.Due(c))late++;Check(late==1,"then plays on from there");
        clock.Step(Plan.Move.Rifle,10.5f,.01f,1/60f);Check(clock.From<0,"another weapon starts its own window");
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<5000;i++){clock.Step(Plan.Move.MachineGun,20+(i/300)*3,(i%300)/60f,1/60f);foreach(var c in Plan.Cues(Plan.Move.MachineGun))if(clock.Due(c))checks+=0;}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the cue clock allocates nothing per frame");

        // The screens: the weapon pick and the locked-in card draw the flourishes; the reveal (everyone's weapons) does not.
        string ui=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeOpeningDraftUI.cs")).Replace("\r\n","\n");
        string Body(string method){int at=ui.IndexOf("private void "+method+"(",StringComparison.Ordinal);Check(at>=0,method+" exists");int next=ui.IndexOf("\n        private ",at+10,StringComparison.Ordinal);return ui.Substring(at,(next<0?ui.Length:next)-at);}
        Check(Body("OpeningWeaponStep").Contains("NativeWeaponCard.Flourishes();"),"the weapon pick draws the chosen card's flourish");
        Check(Body("OpeningReadyStep").Contains("NativeWeaponCard.Flourishes();"),"the locked-in card plays its finisher");
        var reveal=Body("OpeningRevealStep");Check(reveal.Contains("look.Quiet=!you;look.Finish=you;"),"the reveal plays only the pilot's own card's finisher (each pilot sees only their own)");
        string audio=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeAudio.cs"));
        Check(audio.Contains("NativeWeaponCard.Sound+=FlourishHeard;")&&audio.Contains("NativeWeaponCard.Sound-=FlourishHeard;"),"the sounds reach the audio");
        // No per-frame garbage in the drawing: no LINQ, string building or collections made while drawing.
        foreach(var f in new[]{"NativeWeaponFlourish.cs","NativeWeaponFlourish.Moves.cs"})
        {
            string code=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            foreach(var bad in new[]{"System.Linq","new List<","new Dictionary<",".ToString(","string.Format","$\"","string.Concat","+\" ","=>{"})Check(!code.Contains(bad),f+" has no "+bad);
        }
        Console.WriteLine("PASS weapon flourishes ("+checks+" checks)");
    }
}
