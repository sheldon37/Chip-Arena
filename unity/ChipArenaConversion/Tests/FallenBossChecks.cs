#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// NOCTIEL (docs/EXPEDITION-BOSS-FINAL.md): nothing in play reads the preview; the script's a pure function of its clock and covers the whole song in order; every move lands where the floor shows it; one-hit kills have the telegraph the design promises; the Slash runs its
// stages; the bank's script and the game agree about the parts and the chains; the water's arrays fit the effects; the world and the boss stay monochrome.
public static class FallenBossChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("NOCTIEL: "+what);count++;}
    public static void Run(string root)
    {
        count=0;string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        // The song's map: in order, the file's own, one section after another with no gap.
        var secs=NativeFallenBossPreview.Sections;Check(secs.Length==9,"nine sections");for(int i=1;i<secs.Length;i++)Check(Math.Abs(secs[i].From-secs[i-1].To)<1e-3f,"section "+i+" follows");
        Check(secs[0].Clip==NativeFallenSong.IntroFrom&&secs[1].Clip==NativeFallenSong.Drop,"the cutscene runs into the drop");Check(Math.Abs(secs[8].To-secs[8].From-(NativeFallenSong.End-NativeFallenSong.DeathAt))<1e-3f,"the death plays the song out");
        Check(NativeFallenBoss.Phase(1)==1&&NativeFallenBoss.Phase(.7f)==2&&NativeFallenBoss.Phase(.4f)==3&&NativeFallenBoss.Phase(.15f)==4&&NativeFallenBoss.Phase(0)==4,"the phases' thresholds");
        // Pure: the same clock, the same frame.
        var a=new FallenBossFrame();var b=new FallenBossFrame();
        for(float p=0;p<NativeFallenBossPreview.Loop;p+=.37f)
        {
            NativeFallenBossPreview.Eval(p,null,a);NativeFallenBossPreview.Eval(p,null,b);Check(a.Boss==b.Boss&&a.Yaw==b.Yaw&&a.SongClip==b.SongClip&&a.ClipTo==b.ClipTo&&a.Eye==b.Eye,"a pure function of the clock at "+p);
            Check(a.ClipTo!=null,"always a pose at "+p);Check(float.IsFinite(a.Boss.X+a.Boss.Y+a.Boss.Z+a.Yaw+a.Eye.X+a.Eye.Y+a.Eye.Z+a.Look.X+a.Fov),"finite at "+p);
            Check(a.Fov>5&&a.Fov<90,"a sane lens at "+p);
            // It stays on the water: the stage's edge bounds everything it does.
            Check(new Vector2(a.Boss.X,a.Boss.Z).Length()<=NativeFallenBoss.Edge+1,"on the disc at "+p+": "+a.Boss);
        }
        // The song plays on: the clip runs with the clock within a section and the jumps are only between sections.
        for(int s=0;s<secs.Length;s++){NativeFallenBossPreview.Eval(secs[s].From+.5f,null,a);Check(Math.Abs(a.SongClip-secs[s].Clip-.5f)<1e-3f,"section "+s+"'s clip");Check(a.SongEpoch==s,"section "+s+"'s epoch");}
        // Each phase's moves: the design's OHKs telegraph at least 2 s, are 14 s apart and never in another phase.
        var acts=NativeFallenBossPreview.Acts;
        foreach(string id in new[]{"cleave","vow","severance","descent","rain","verdict","choir","sweep","eclipse","penance","fall","last","thrones"})Check(acts.Any(x=>x.Id==id),"the script shows "+id);
        float lastKill=-100;foreach(var x in acts.Where(y=>y.Id=="verdict"||y.Id=="eclipse"||y.Id=="fall"||y.Id=="last").OrderBy(y=>y.Hit)){if(x.Id!="fall")Check(x.Hit-x.At>=2,x.Id+" telegraphs at least 2 s");Check(x.Hit-lastKill>=14,x.Id+" at "+x.Hit+" is 14 s from the last");lastKill=x.Hit;}
        Check(acts.All(x=>x.Hit>=secs[1].From&&x.End<=secs[8].From+.01f),"every move in a fight section");foreach(var x in acts){int sec=NativeFallenBossPreview.SectionAt(x.Hit);Check(secs[sec].Kind==NativeFallenBossPreview.KindFight,x.Id+" at "+x.Hit+" lands in a fight");Check(secs[sec].Phase==x.Phase,x.Id+" in its phase");}
        // The moves' numbers exist for each phase, and OHKs carry none.
        foreach(var m in NativeFallenBoss.Moves){Check(m.Kills==(m.Damage==0&&(m.Id=="verdict"||m.Id=="eclipse"||m.Id=="fall"||m.Id=="last"))||!m.Kills,m.Id+" kills or hurts");Check(m.Duration>0&&m.Cooldown>0,m.Id+" has timing");if(m.Kills)Check(m.Wind>=1.6f,m.Id+" windup");}
        // The Slash's stages: draws, freezes on the cut, parts, and ends in black on a phase change or clear on the opening.
        foreach(byte k in new[]{NativeFallenSlash.Cut,NativeFallenSlash.Opening,NativeFallenSlash.Reveal})
        {
            float cut=NativeFallenSlash.FreezeAt(k);var s0=NativeFallenSlash.Eval(k,cut*.5f);Check(!s0.Freeze&&s0.Open==0,"before the cut it's whole");var s1=NativeFallenSlash.Eval(k,cut+.01f);Check(s1.Freeze&&s1.Flash>.2f,"the cut flashes");var s2=NativeFallenSlash.Eval(k,NativeFallenSlash.Length(k));Check(s2.Open>s1.Open,"the halves part");
            Check(k==NativeFallenSlash.Cut?s2.Black>=.99f:s2.Open>=1.2f,"a cut ends in black, the others off the screen");
        }
        // The effects fit the shader's arrays, in black and white only; building allocates nothing a frame once sized.
        var water=R("Assets/ChipArena/Resources/NativeFallenWater.shader");Check(water.Contains("_FbRing["+NativeFallenBossFx.MaxRings+"]")&&water.Contains("_FbWave["+NativeFallenBossFx.MaxWaves+"]")&&water.Contains("_FbPool["+NativeFallenBossFx.MaxPools+"]"),"the water's arrays are the effects' sizes");
        var fx=new NativeFallenBossFx();var song=new NativeFallenSong();float maxQuads=0;
        for(float p=0;p<NativeFallenBossPreview.Loop;p+=.11f)
        {
            NativeFallenBossPreview.Eval(p,null,a);fx.Build(a,new Vector3(0,3,-20),Vector3.UnitX,Vector3.UnitY,song,false);maxQuads=Math.Max(maxQuads,fx.Quads.Count);
            Check(fx.RingCount<=NativeFallenBossFx.MaxRings&&fx.WaveCount<=NativeFallenBossFx.MaxWaves&&fx.PropCount<=NativeFallenBossFx.MaxProps,"the arrays hold at "+p);
            for(int i=0;i<fx.Quads.Count*12;i++)Check(float.IsFinite(fx.Quads.Corners[i]),"quads finite at "+p);
            for(int i=0;i<fx.RingCount*4;i++)Check(float.IsFinite(fx.Rings[i]),"rings finite at "+p);
        }
        Check(maxQuads<fx.Quads.Max,"the quads never run out ("+maxQuads+" of "+fx.Quads.Max+")");
        // Chain data: the bank's script and the game agree.
        var script=R("tools/gear/fallen_boss.py");
        Check(Regex.IsMatch(script,@"BANK\s*=\s*'ChipArenaPackBossFallen'"),"the script's bank name");Check(script.Contains("ROBE_Z = .955")&&script.Contains("ROBE_LEN = (.3, .3, .32)"),"the robe's measures are the script's");Check(script.Contains("BLADE_RING_PIVOT = (0, -.2, 1.38)"),"the ring's pivot is the script's");
        foreach(var part in new[]{"cuirass","cuirass-broken","mask","mask-split","halo","sword-blade","sword-blade-dark","sword-blade-light","sword-hilt","sword-veins-1","sword-veins-2"}){Check(NativeFallenBossLook.Parts.Any(p=>p.Name==part),"the game knows "+part);Check(script.Contains("'"+part+"'"),"the script bakes "+part);}
        foreach(var c in NativeFallenBossLook.Chains)if(c.Parent!=null&&!c.Parent.Contains("-"))Check(new[]{"head","pelvis","spine_05"}.Contains(c.Parent),c.Name+" rides a bone the script fits");
        foreach(var part in NativeFallenBossLook.Parts)Check(part.Phases!=0&&part.Phases<16,part.Name+" shows in a phase");for(int phase=1;phase<=4;phase++){int bit=1<<(phase-1);Check(NativeFallenBossLook.Parts.Count(p=>p.Name.StartsWith("sword-blade")&&(p.Phases&bit)!=0)==1,"one blade in phase "+phase);}
        // Monochrome: the card and every finish in the script are greys, and no rule reads the preview.
        var card=NativeFallenBoss.Card;foreach(int c in new[]{card.Ink,card.TopLine,card.Rule,card.BarFrom,card.BarTo,card.Letters,card.Glow,card.SplitA,card.SplitB})Check(((c>>16)&255)==((c>>8)&255)&&((c>>8)&255)==(c&255),"the card is grey");
        foreach(Match m in Regex.Matches(script,@"finish\('[a-z-]+', 0x([0-9A-Fa-f]{6})")){int v=Convert.ToInt32(m.Groups[1].Value,16);int r=(v>>16)&255,g=(v>>8)&255,bl=v&255;Check(Math.Abs(r-g)<=12&&Math.Abs(g-bl)<=12,"finish "+m.Value+" is grey");}
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"*.cs")){if(Path.GetFileName(f).StartsWith("NativeFallen"))continue;Check(!File.ReadAllText(f).Contains("NativeFallenBossPreview"),Path.GetFileName(f)+" doesn't read the preview");}
        var presentation=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");Check(presentation.Contains("if(!NativeFallenBossPreview.On){FallenBossHide();return;}"),"with the preview off nothing runs");
        Console.WriteLine("PASS NOCTIEL ("+count+" checks; the most effect quads in a frame: "+maxQuads+")");
    }
}
#endif
