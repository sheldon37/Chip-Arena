#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
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
        var presentation=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");Check(presentation.Contains("if(!NativeFallenBossPreview.On&&!live){FallenBossHide();return;}"),"without a preview or real encounter nothing runs");
        Live(R);int sounds=Sounds(root);
        Console.WriteLine("PASS NOCTIEL ("+count+" checks; the most effect quads in a frame: "+maxQuads+"; live motion: every move's contact on its hit, glide and land, no pops; "+sounds+" sounds requested, each in the bank contract)");
    }
    // ---- The sounds (docs/audio/sound-requests-noctiel.json; owner 2026-11-06: "write a sound cue list for elevenlabs") ----
    // Every NOCTIEL key is in the bank contract and the request, flat or positional as it plays, inside its cap, at the boss level,
    // in a default batch; the OHK tells are first in line and as long as their warnings; the bundled copy matches; no audio in git.
    private static int Sounds(string root)
    {
        var path=Path.Combine(root,"../../docs/audio/sound-requests-noctiel.json");if(!File.Exists(path))return 0;
        using var doc=JsonDocument.Parse(File.ReadAllText(path));var sounds=doc.RootElement.GetProperty("sounds").EnumerateArray().ToDictionary(s=>s.GetProperty("key").GetString());
        var contract=NativeSoundKeys.All.ToDictionary(k=>k.Key);var keys=NativeFallenBossCues.Keys;
        Check(keys.Length==57&&NativeFallenBossCues.Briefs.Length==keys.Length&&sounds.Count==keys.Length&&keys.Distinct().Count()==keys.Length,"57 sounds, one request and one brief for each key");
        for(int i=0;i<keys.Length;i++)
        {
            string key=keys[i];bool flat=NativeFallenBossCues.Flat(i);
            Check(Regex.IsMatch(key,"^boss[.]noctiel([.][a-z0-9]+)+$"),key+" fits the generator's key pattern");
            Check(contract.ContainsKey(key)&&sounds.ContainsKey(key),key+" is in the bank contract and requested");
            var d=contract[key];var s=sounds[key];float lo=s.GetProperty("targetSeconds").GetProperty("min").GetSingle(),hi=s.GetProperty("targetSeconds").GetProperty("max").GetSingle(),gen=s.GetProperty("generateSeconds").GetSingle();
            Check(d.Cap+1e-4f>=hi&&lo>0&&lo<hi&&gen>=hi&&gen<=22,key+" fits its cap and can be generated");
            Check(d.Space==(flat?NativeSoundSpace.Flat:NativeSoundSpace.Positional)&&(s.TryGetProperty("space",out var sp)&&sp.GetString()=="2d")==flat,key+" plays "+(flat?"flat":"at its place")+", and its request says so");
            Check(s.GetProperty("category").GetString()=="boss"&&s.GetProperty("priority").GetInt32()<3&&s.GetProperty("count").GetInt32()>=1&&s.GetProperty("count").GetInt32()<=NativeSoundKeys.MaxClips,key+": the boss level, a default batch, takes within the bank");
            float level=s.GetProperty("levelDb").GetSingle();Check(level>=-18&&level<=6&&s.GetProperty("fadeOutMs").GetInt32()>0,key+" has a level and a fade");
            var prompt=s.GetProperty("prompt").GetString();Check(prompt.Contains("isolated")&&prompt.Contains("no music")&&prompt.Length<400,key+"'s prompt asks for the sound alone");
        }
        // The one-hit kills' tells come first and last as long as their warnings (Verdict 2.4 s, Eclipse 3.2 s).
        foreach(var tell in new[]{"verdict.crack","eclipse","fall.mark","descent.mark","hush"})Check(sounds["boss.noctiel."+tell].GetProperty("priority").GetInt32()==1&&contract["boss.noctiel."+tell].Priority!=NativeSoundPriority.Low,tell+" is a priority-1 tell, never dropped for a busier voice");
        Check(sounds["boss.noctiel.verdict.crack"].GetProperty("targetSeconds").GetProperty("min").GetSingle()>=2.2f&&sounds["boss.noctiel.eclipse"].GetProperty("targetSeconds").GetProperty("min").GetSingle()>=3,"the OHK tells last their warnings");
        float loudest=sounds.Values.Max(s=>s.GetProperty("levelDb").GetSingle());Check(sounds["boss.noctiel.verdict.split"].GetProperty("levelDb").GetSingle()==loudest&&sounds["boss.noctiel.feather"].GetProperty("levelDb").GetSingle()<=-14,"the Verdict's split is the loudest thing and the feather one of the quietest");
        var bundled=Path.Combine(root,"tools/sound_bundle/docs/audio/sound-requests-noctiel.json");Check(File.Exists(bundled)&&File.ReadAllText(bundled).Replace("\r\n","\n")==File.ReadAllText(path).Replace("\r\n","\n"),"the builder bundle's copy matches");
        Check(!Directory.EnumerateFiles(Path.Combine(root,"../../docs/audio")).Any(f=>f.EndsWith(".mp3")||f.EndsWith(".wav")||f.EndsWith(".ogg")),"no audio in the repo");
        return sounds.Count;
    }
    // ---- Live motion (Core NativeFallenLiveMotion; owner 2026-11-06: "a lot of feet sliding ... needs better attack animations") ----
    private static NativeVisualMotionClips.Library Bank(params string[] missing)
    {
        var lib=new NativeVisualMotionClips.Library();
        void C(string name,float length,float hit,bool loop,float speed=0){if(Array.IndexOf(missing,name)<0)lib.Clips[name]=new NativeVisualMotionClips.Clip{Name=name,Length=length,Hit=hit,Rate=30,SpeedZ=speed,Loop=loop,Frames=2,Stride=1,Data=new float[2]};}
        C("Unarmed-Idle",3,1.5f,true);C("Armed-Dash-Forward",.8f,.4f,true,6);C("Armed-Run-Forward",.7f,.35f,true,5.2f);C("Armed-Walk",1.1f,.5f,true,1.4f);C("Armed-Walk-Slow",1.6f,.8f,true,.9f);C("Armed-Idle",3,1.5f,true);
        foreach(var n in new[]{"Sword-Attack-R1","Sword-Attack-R2","Sword-Attack-R3","Sword-Attack-R4","Sword-Attack-R6","Armed-Run-Attack-R1","2Hand-Sword-Attack1","2Hand-Sword-Attack3","2Hand-Sword-Attack11","2Hand-Sword-Activate","Armed-Activate-Left"})C(n,1.3f,.5f,false);
        C("2Hand-Sword-Jump",.9f,.3f,false);C("2Hand-Sword-Air-Attack1",1.1f,.55f,false);C("2Hand-Sword-Land",.8f,.1f,false);
        return lib;
    }
    private static float Angle(float a,float b){float d=(b-a)%360;if(d>180)d-=360;if(d<-180)d+=360;return Math.Abs(d);}
    private static void Live(Func<string,string> R)
    {
        var bank=Bank();var f=new FallenBossFrame();
        // Every move: the clip's contact lands on the authority's strike (each Cleave cut, the second wing on its wave).
        foreach(var m in NativeFallenBoss.Moves)
        {
            var d=new NativeFallenLiveMotion();var from=new Vector3(0,0,0);var to=new Vector3(0,0,12);float A=10,wind=m.Id=="thrones"?1.2f:Math.Max(m.Kills?2.4f:0,m.Wind),H=A+wind;
            float duration=wind+(m.Id=="severance"?5.5f:m.Id=="fall"?3.2f:m.Id=="thrones"?1.2f:m.Strike+m.Follow+m.Recover);
            var x=new NativeFallenLiveMotion.Facts{At=from,Yaw=0,Target=to,Action=m.Id,ActionAt=A,HitAt=H,Duration=duration,ActionYaw=0,From=from,To=to,SecondWing=true};
            string lastTo=null;float maxAir=0,lastYaw=0;bool first=true;var shots=new System.Collections.Generic.List<NativeFallenLiveMotion.Shot>();
            for(float now=A-1;now<A+duration+1;now+=1/120f)
            {
                x.Now=now;x.At=m.Id=="vow"&&now>=H?Vector3.Lerp(from,to,Math.Clamp((now-H)/.5f,0,1)):m.Id=="descent"&&now>=H?to:from;
                if(now<A){x.Action=null;}else x.Action=m.Id;
                for(int w=0;w<3;w++)f.WingOpen[w]=.3f;f.Kneel=0;
                d.Step(f,x,bank,first?"2Hand-Sword-Attack1":null,1);
                Check(float.IsFinite(f.Boss.X+f.Boss.Y+f.Boss.Z+f.Yaw+f.Air+f.TimeTo+f.TimeFrom)&&f.Mix>=0&&f.Mix<=1&&f.Carry>=0&&f.Carry<=1,m.Id+": a finite pose at "+now);
                // No pops: a new clip always comes in under a crossfade from the one before.
                if(!first&&f.ClipTo!=lastTo)Check(f.ClipFrom==lastTo&&f.Mix<.5f,m.Id+": "+lastTo+" crossfades to "+f.ClipTo+" at "+now);
                if(!first)Check(Angle(lastYaw,f.Yaw)<12,m.Id+": it turns, never snaps, at "+now);
                lastTo=f.ClipTo;lastYaw=f.Yaw;first=false;maxAir=Math.Max(maxAir,f.Air);
                foreach(var s in d.Plan.ToArray())if(!shots.Exists(q=>q.Key==s.Key&&q.T0==s.T0))shots.Add(s);
                if(Math.Abs(now-H)<.004f)Check(Angle(f.Yaw,0)<3,m.Id+": it faces its aim on the hit");
                if(now<A-.1f)Check(f.Trail==0,m.Id+": no trail before the move");
            }
            float[] hits=m.Id=="cleave"?new[]{H,H+.3f,H+.6f}:m.Id=="severance"?new[]{H,H+.6f}:m.Id=="vow"?new[]{H+.12f}:new[]{H};
            foreach(float hit in hits)Check(shots.Exists(s=>Math.Abs(NativeFallenLiveMotion.ContactOf(s,bank)-hit)<.035f),m.Id+": a clip's contact at "+(hit-A).ToString("0.00")+" s");
            if(m.Id=="descent")Check(maxAir>3,"descent leaps ("+maxAir.ToString("0.0")+" m)");
            if(m.Id=="rain"||m.Id=="fall")Check(maxAir>.8f,m.Id+" lifts it off the water");
            if(m.Id=="cleave"||m.Id=="sweep"||m.Id=="verdict")Check(maxAir<.01f,m.Id+" keeps its feet on the water");
        }
        // Descent's path: where it stood until it leaves, on the arc, down where the authority puts it on the hit.
        {
            var d=new NativeFallenLiveMotion();var from=new Vector3(0,0,0);var to=new Vector3(18,0,6);var mv=NativeFallenBoss.Move("descent");float A=5,H=A+mv.Wind;
            var x=new NativeFallenLiveMotion.Facts{At=from,Target=to,Action="descent",ActionAt=A,HitAt=H,Duration=mv.Wind+mv.Strike+mv.Follow+mv.Recover,ActionYaw=71.6f,From=from,To=to};
            for(float now=A;now<H+.5f;now+=1/120f){x.Now=now;x.At=now>=H?to:from;d.Step(f,x,bank);
                if(now<A+.1f)Check(Vector3.Distance(f.Boss,from)<.05f,"descent: it stands until it leaves");
                if(now>=H)Check(Vector3.Distance(f.Boss,to)<.3f&&f.Air<.05f,"descent: it lands where the hit is, on the hit ("+Vector3.Distance(f.Boss,to).ToString("0.00")+", air "+f.Air.ToString("0.00")+")");}
        }
        // The chase: the authority's 9 m/s, stepped at 60 Hz and drawn at 144: it glides (off the water, the glide's clip, facing
        // its way), then stops, lands and stands with the sword carried.
        {
            var d=new NativeFallenLiveMotion();var at=new Vector3(0,0,0);var x=new NativeFallenLiveMotion.Facts{At=at,Yaw=180,Target=new Vector3(40,0,40)};
            float lastYaw=0,step=0;bool first=true;string lastTo=null;
            for(float now=0;now<4;now+=1/144f)
            {
                while(step<=now&&now<2){at+=Vector3.Normalize(new Vector3(1,0,1))*(9f/60);step+=1/60f;}if(now>=2)step=now;
                x.Now=now;x.At=at;d.Step(f,x,bank,first?"Unarmed-Idle":null,0);
                if(!first){Check(Angle(lastYaw,f.Yaw)<8,"the chase turns smoothly at "+now);if(f.ClipTo!=lastTo)Check(f.ClipFrom==lastTo&&f.Mix<.5f,"the chase crossfades at "+now);}
                first=false;lastYaw=f.Yaw;lastTo=f.ClipTo;
                if(now>.8f&&now<2)Check(d.Gliding&&f.Gliding&&f.Air>.3f&&f.ClipTo=="Armed-Dash-Forward"&&Angle(f.Yaw,45)<10&&f.Carry<.05f,"chasing it glides off the water, facing its way, at "+now+" (air "+f.Air.ToString("0.00")+", yaw "+f.Yaw.ToString("0")+")");
                if(now>3.2f)Check(!d.Gliding&&f.Air<.05f&&f.ClipTo=="Unarmed-Idle"&&f.Carry>.9f,"stopped, it lands and stands with the sword carried at "+now);
                Check(Vector3.Distance(f.Boss,at)<2.5f,"the body keeps up with the actor at "+now);
            }
        }
        // The walk (owner's reference video, 2026-11-06: about a step a second, the sword trailing at its side): at a walking pace
        // the slow walk at that cadence, the stride matched to the travel, the sword carried, feet on the water; brisker, the walk.
        foreach(var (pace,clip,steps) in new[]{(1.4f,"Armed-Walk-Slow",1f),(2.6f,"Armed-Walk",2.6f/(1.4f*NativeFallenBoss.Scale)*2/1.1f)})
        {
            var d=new NativeFallenLiveMotion();var at=Vector3.Zero;var x=new NativeFallenLiveMotion.Facts{At=at,Yaw=0,Target=new Vector3(0,0,80)};float step=0,lastTime=0,advance=0,len=bank.Length(clip);string lastTo=null;
            for(float now=0;now<5;now+=1/144f)
            {
                while(step<=now){at+=new Vector3(0,0,pace/60);step+=1/60f;}x.Now=now;x.At=at;d.Step(f,x,bank);
                if(now>1.5f&&f.ClipTo==clip&&lastTo==clip){float dt2=f.TimeTo-lastTime;if(dt2<0)dt2+=len;advance+=dt2;}
                if(now>1.5f)Check(!d.Gliding&&f.ClipTo==clip&&f.Mix>=1&&f.Air<.05f&&f.Carry>.9f&&Angle(f.Yaw,0)<2,"at "+pace+" m/s it walks ("+f.ClipTo+"), sword carried, feet down, at "+now);
                lastTime=f.TimeTo;lastTo=f.ClipTo;
            }
            float cadence=2*advance/len/3.5f;Check(Math.Abs(cadence-steps)<.06f*steps,"at "+pace+" m/s, "+cadence.ToString("0.00")+" steps a second (want "+steps.ToString("0.00")+")");
        }
        // A standing turn: past 18 degrees off its quarry it turns to it, smoothly.
        {
            var d=new NativeFallenLiveMotion();var x=new NativeFallenLiveMotion.Facts{At=Vector3.Zero,Yaw=0,Target=new Vector3(0,0,20)};float lastYaw=0;
            for(float now=0;now<3;now+=1/144f){x.Now=now;if(now>1)x.Target=new Vector3(20,0,0);d.Step(f,x,bank);if(now>0)Check(Angle(lastYaw,f.Yaw)<6,"a standing turn is smooth at "+now);lastYaw=f.Yaw;
                if(now<1)Check(Angle(f.Yaw,0)<1,"it holds still facing its quarry");if(now>2.4f)Check(Angle(f.Yaw,90)<5,"it has turned to its quarry ("+f.Yaw.ToString("0")+")");}
        }
        // A bank without a clip falls back to the nearest it has.
        var thin=Bank("Sword-Attack-R1","Armed-Dash-Forward","2Hand-Sword-Jump");
        Check(NativeFallenLiveMotion.Pick("Sword-Attack-R1",thin)=="2Hand-Sword-Attack1"&&NativeFallenLiveMotion.Pick("Armed-Dash-Forward",thin)=="Armed-Run-Forward"&&NativeFallenLiveMotion.Pick("2Hand-Sword-Jump",thin)=="Armed-Activate-Left"&&NativeFallenLiveMotion.Pick("Sword-Attack-R2",thin)=="Sword-Attack-R2"&&NativeFallenLiveMotion.Pick("x",null)=="x","missing clips fall back");
        // The wiring: the director after the binding, the feet planted, the blade cleared, the robe draped, the song held under the cover.
        var scenery=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");var body=R("Assets/ChipArena/Runtime/NativeFallenBossBody.cs");var live=R("Assets/ChipArena/Runtime/NativeFallenLiveBody.cs");
        Check(scenery.Contains("if(live){FallenBossViewOf(arena,f);FallenLiveMotion(arena,f,now);}else{fbLive.Reset();fbLiveNow=false;")&&scenery.Contains("FallenPose(f,now,dt,origin,eyeAt);FallenShown(f);"),"the live director runs after the binding, the preview keeps its script");
        Check(live.Contains("fbLive.Step(f,facts,VisualMotionBank,")&&live.Contains("!(a.Pve.Holding&&a.Pve.Cinematic==\"intro\")&&z.deathAt<0"),"the intro and the death keep the cutscene's pose");
        Check(body.Contains("FallenFeetPlant(f,now,hover);")&&body.IndexOf("FallenFeetPlant(f,now,hover);")<body.IndexOf("FallenGrip(f,origin,origin.y+f.Air+hover);")&&body.IndexOf("FallenGrip(f,origin,origin.y+f.Air+hover);")<body.IndexOf("FallenBladeClear(f,yawRot,now);")&&body.IndexOf("FallenBladeClear(f,yawRot,now);")<body.IndexOf("FallenChains(f,now,dt,yawRot,local);"),"feet, then the grip, then the blade cleared, then the chains");
        Check(!body.Contains("if(f.Plant>0)FallenPlant(f,origin);else if(f.Carry>0)FallenCarry(f);")&&body.Contains("FallenSwordTo(Vector3.Lerp(cg,pg,plant),Quaternion.Slerp(cr,pr,plant),Mathf.Clamp01(plant+carry),"),"the sword drawn from the water goes straight to the carry");
        Check(body.Contains("Quaternion.FromToRotation(Vector3.down,FallenRobeWay(c.Pivot.X,c.Pivot.Z-.03f,c.Segment,yawRot))*yawRot"),"the robe drapes over the legs");
        Check(body.Contains("FbDragWay=new Vector3(.85f,0,-.53f)")&&body.Contains("var wrist=shoulder+yawRot*new Vector3(.07f*s,0,.09f*s)+Vector3.down*(reach*.93f-breath);")&&body.Contains("float drop=Mathf.Clamp((grip.y-floor-FbDragTip*s)/along,.08f,.95f);"),"the sword carried as the reference walk carries it: from a hanging arm, trailing out and back, its point just off the water");
        Check(scenery.Contains("if(tip.y<floor+.25f&&f.Plant<.5f&&now-fbWakeLast>.07f&&moved>=.6f)"),"the dragged point's wake is a ring every 0.6 m, none at rest");
        var audio=R("Assets/ChipArena/Runtime/NativeAudio.cs");var loading=R("Assets/ChipArena/Runtime/NativeStageLoadingUI.cs");var game=R("Assets/ChipArena/Runtime/ConversionGame.cs");
        Check(audio.Contains("map==NativeFallenRules.Map?NativeFallenSong.Id:")&&audio.Contains("Track(NativeFallenSong.Id,Music*.55f,StageCovered,")&&game.Contains("nativeAudio.StageCovered=stageLoad.Visible&&stageLoad.Phase!=StageLoadPhase.Reveal;"),"its song loads behind the cover and waits for the screen to open");
        Check(loading.Contains("bool intro=arena!=null&&(IntroActive||arena.Pve?.Holding==true&&arena.Pve.Cinematic==\"intro\");"),"a boss's intro hold keeps the intro's loading budget");
        Check(!R("Assets/ChipArena/Core/NativeFallenLiveMotion.cs").Contains("NativeArena"),"the director reads facts, not the arena");
    }
}
#endif
