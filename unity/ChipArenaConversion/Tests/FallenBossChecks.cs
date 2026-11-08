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
        Live(R);int sounds=Sounds(root);int cues=Cues(R);int intro=Intro(R);
        Console.WriteLine("PASS NOCTIEL ("+count+" checks; the most effect quads in a frame: "+maxQuads+"; live motion: every move's contact on its hit, glide and land, no pops; "+sounds+" sounds requested, each in the bank contract; "+cues+" cue moments on the authority's times; intro choreography "+intro+" checks)");
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
    // ---- The intro's choreography (Core NativeFallenIntroMotion; owner's four reference clips, option A) ----
    // On only in the cutscene; the cut lands on the drop after a 0.18 s load and a 0.20 s downswing; nothing jumps between frames;
    // the pull hands the fist from the plant to the key; the front foot never lets go and the back foot lifts only to step; the
    // keyed fist stays in reach of the shoulder and its blade off the torso; the point meets the water on the impact; the feather
    // falls like a leaf and lands on its cue. The cutscene's shots, cues and Slash are untouched.
    private static int Intro(Func<string,string> R)
    {
        int n0=count;var M=typeof(NativeFallenIntroMotion);
        Check(!NativeFallenIntroMotion.At(-.01f).On&&!NativeFallenIntroMotion.At(NativeFallenPilots.CutsceneEnd+.01f).On&&NativeFallenIntroMotion.At(20).On,"the layer is on only through the cutscene");
        var f=new FallenBossFrame();NativeFallenBossPreview.Eval(70,null,f);Check(!f.Intro.On,"a fight section has no intro layer");
        float impact=NativeFallenIntroMotion.ImpactAt;
        Check(Math.Abs(impact-NativeFallenIntroMotion.TopAt-NativeFallenIntroMotion.Downswing)<1e-4f&&Math.Abs(NativeFallenIntroMotion.Downswing-.2f)<1e-4f&&Math.Abs(NativeFallenIntroMotion.TopAt-NativeFallenIntroMotion.LoadAt-.18f)<1e-4f,"the load 0.18 s, the downswing 0.20 s, the impact on the drop");
        var hit=NativeFallenIntroMotion.At(impact);var tip=hit.Grip+hit.Blade*((.15f+1.5f)*NativeFallenBoss.Scale);
        Check(tip.Y>-.3f&&tip.Y<.25f&&tip.Z>3,"on the impact the point meets the water ahead, toward the lens ("+tip+")");
        Check(hit.Trail>.99f&&NativeFallenIntroMotion.At(50.5f).Trail==0,"the trail on the downswing only");
        // Continuity, sampled at 240 Hz through the whole cutscene.
        var last=NativeFallenIntroMotion.At(0);float maxGrip=0,maxBlade=0,maxRoot=0,maxLean=0,calmGrip=0,calmBlade=0,calmLean=0,calmBladeAt=0;
        for(int i=1;i<=(int)(impact*240);i++)
        {
            float p=i/240f;var o=NativeFallenIntroMotion.At(p);
            Check(float.IsFinite(o.Root.X+o.Root.Y+o.Root.Z+o.Lean+o.Twist+o.Head+o.Grip.X+o.Grip.Y+o.Grip.Z+o.Blade.X+o.Blade.Y+o.Blade.Z+o.Flat.X+o.Flat.Y+o.Flat.Z+o.LeftLift),"a finite pose at "+p);
            Check(Math.Abs(o.Blade.Length()-1)<1e-3f&&Math.Abs(Vector3.Dot(o.Blade,Vector3.Normalize(o.Flat)))<.995f,"a unit blade with a usable flat at "+p);
            float g=Vector3.Distance(o.Grip,last.Grip),b=(float)(Math.Acos(Math.Clamp(Vector3.Dot(o.Blade,last.Blade),-1,1))*180/Math.PI),r=Vector3.Distance(o.Root,last.Root),l=Math.Abs(o.Lean-last.Lean)+Math.Abs(o.Twist-last.Twist);
            if(o.Sword>0&&last.Sword>0){maxGrip=Math.Max(maxGrip,g);maxBlade=Math.Max(maxBlade,b);}maxRoot=Math.Max(maxRoot,r);maxLean=Math.Max(maxLean,l);
            if(p<NativeFallenIntroMotion.LoadAt){if(o.Sword>0&&last.Sword>0){calmGrip=Math.Max(calmGrip,g);if(b>calmBlade){calmBlade=b;calmBladeAt=p;}}calmLean=Math.Max(calmLean,l);}
            Check(Math.Abs(o.KneelLegs-last.KneelLegs)<.02f&&Math.Abs(o.KneelUpper-last.KneelUpper)<.02f&&Math.Abs(o.Sword-last.Sword)<.06f&&Math.Abs(o.LeftBlend-last.LeftBlend)<.02f&&Math.Abs(o.SwordLift-last.SwordLift)<.02f,"no weight jumps at "+p);
            // The front foot holds from the rise on; the back foot lifts only in its two steps.
            Check((o.FootR>0)==(p>=NativeFallenIntroMotion.RiseAt)&&(o.FootL>0)==(p>=NativeFallenIntroMotion.RiseAt),"the feet are held from the rise on at "+p);
            bool stepping=p>NativeFallenIntroMotion.StepFrom&&p<NativeFallenIntroMotion.StepTo||p>NativeFallenIntroMotion.LungeFrom&&p<NativeFallenIntroMotion.LungeTo;
            Check(stepping?o.LeftLift>=-1e-4f:Math.Abs(o.LeftLift)<1e-4f,"the back foot lifts only to step, at "+p);
            if(o.Sword>.5f)
            {
                // A shoulder from the hips' travel and the lean (the rig's own proportions at 1.95 scale): the fist within reach of it,
                // the blade off the torso.
                float lean=o.Lean*(float)Math.PI/180,tw=o.Twist*(float)Math.PI/180;var hips=o.Root+new Vector3(0,1.85f,0);
                var up=new Vector3((float)Math.Sin(tw)*0,(float)Math.Cos(lean),(float)Math.Sin(lean));var side=new Vector3((float)Math.Cos(tw),0,-(float)Math.Sin(tw));
                var shoulder=hips+up*.98f+side*.37f;float reach=Vector3.Distance(shoulder,o.Grip);
                Check(reach<=1.3f&&reach>=.3f,"the fist in reach of the shoulder at "+p+" ("+reach.ToString("0.00")+" m)");
                var neck=hips+up*1.25f;float clear=float.MaxValue;
                for(int k=0;k<=20;k++){var q=o.Grip+o.Blade*(.5f+k*.13f);float t=Math.Clamp(Vector3.Dot(q-hips,neck-hips)/Vector3.DistanceSquared(neck,hips),0,1);clear=Math.Min(clear,Vector3.Distance(q,hips+(neck-hips)*t));}
                Check(clear>=.36f,"the blade clear of the torso at "+p+" ("+clear.ToString("0.00")+" m)");
            }
            last=o;
        }
        Check(maxGrip<.08f&&maxBlade<4.5f&&maxRoot<.03f&&maxLean<2.5f,"nothing jumps between frames, the cut included (fist "+maxGrip.ToString("0.000")+" m, blade "+maxBlade.ToString("0.0")+", hips "+maxRoot.ToString("0.000")+" m, back "+maxLean.ToString("0.0")+" per 1/240 s)");
        Check(calmGrip<.03f&&calmBlade<1.6f&&calmLean<.5f,"before the load everything moves at a walk's pace (fist "+calmGrip.ToString("0.000")+" m, blade "+calmBlade.ToString("0.00")+" at "+calmBladeAt.ToString("0.00")+", back "+calmLean.ToString("0.00")+" per 1/240 s)");
        // The pull and the hand-over, the kneel letting go, the stance and the lunge.
        Check(NativeFallenIntroMotion.At(NativeFallenIntroMotion.PullFrom-.01f).SwordLift==0&&NativeFallenIntroMotion.At(NativeFallenIntroMotion.PullTo).SwordLift>=.5f&&NativeFallenIntroMotion.At(NativeFallenIntroMotion.KeyFrom-.01f).Sword==0&&NativeFallenIntroMotion.At(NativeFallenIntroMotion.KeyTo).Sword>=.999f,"the pull, then the keyed fist takes the sword");
        Check(NativeFallenIntroMotion.At(35.5f).KneelLegs==1&&NativeFallenIntroMotion.At(37.1f).KneelLegs==0&&NativeFallenIntroMotion.At(33.4f).KneelUpper==1&&NativeFallenIntroMotion.At(36.3f).KneelUpper==0,"the head lifts before the legs let go; standing by the step's end");
        var stand=NativeFallenIntroMotion.At(44);var lunge=NativeFallenIntroMotion.At(impact);
        Check(stand.LeftBlend>=.999f&&stand.LeftSpot==NativeFallenIntroMotion.LeftStand&&lunge.LeftSpot==NativeFallenIntroMotion.LeftLunge&&lunge.Root.Z>stand.Root.Z+.3f&&lunge.Root.Y<-.2f&&lunge.Lean>15,"standing square, then the lunge: the left foot forward, the hips forward and down, the back over");
        // The script: the fist on the plant until the key, no carry, the stance under the keyed cut; the feather's leaf fall.
        NativeFallenBossPreview.Eval(40,null,f);Check(f.Intro.On&&f.Plant==1&&f.Carry==0,"standing, the fist on the planted sword");
        NativeFallenBossPreview.Eval(50.5f,null,f);Check(f.Plant==0&&f.Carry==0&&f.ClipTo=="Unarmed-Idle","the cut keyed over the stance, not a pack clip");
        int turns=0;float lastX=.25f,lastDx=0,early=0,late=0;
        for(float p=1.5f;p<11;p+=1/120f)
        {
            NativeFallenIntroMotion.Feather(p,out var at,out _,out float pitch,out float roll,out bool shown);float dx=at.X-lastX;if(dx*lastDx<0)turns++;if(Math.Abs(dx)>1e-6f)lastDx=dx;lastX=at.X;
            if(p<NativeFallenBossPreview.FeatherLand)Check(at.Y>0,"the feather is in the air before it lands ("+p+")");else Check(at.Y<.01f&&Math.Abs(roll)<1e-3f&&Math.Abs(pitch)<1e-3f,"the feather lies flat on the water after it lands ("+p+")");
            Check(shown==(p<11),"the feather floats until the shot cuts");
            float off=Math.Abs(at.X-.25f);if(p<3.5f)early=Math.Max(early,off);if(p>8&&p<NativeFallenBossPreview.FeatherLand)late=Math.Max(late,off);
        }
        Check(turns>=8&&late<early*.6f,"a falling leaf: it swings back and forth ("+turns+" turns), smaller near the water");
        // The rig's application.
        var body=R("Assets/ChipArena/Runtime/NativeFallenBossBody.cs");var rig=R("Assets/ChipArena/Runtime/NativeFallenIntroBody.cs");var scenery=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");
        Check(body.Contains("bool intro=FallenIntroNow(f,now,out var ip,out float iw,out float iage);")&&body.Contains("if(intro&&!mocap)FallenIntroBody(ip,iw,yawRot);")&&body.IndexOf("FallenIntroBody(ip,iw,yawRot);")<body.IndexOf("FallenIntroFeet(ip,iw,iage,at,yawRot);")&&body.IndexOf("FallenIntroFeet(ip,iw,iage,at,yawRot);")<body.IndexOf("FallenGrip(f,origin,origin.y+f.Air+hover,intro,ip,iw,at,yawRot);"),"the hips and back, then the feet, then the fist");
        Check(body.Contains("float k=f.Intro.On?(i<8?f.Intro.KneelLegs:f.Intro.KneelUpper):f.Kneel;")&&rig.Contains("FallenLegTo(fbThighR,fbCalfR,fbFootR,goalR,turnR);FallenLegTo(fbThighL,fbCalfL,fbFootL,goalL,turnL);")&&rig.Contains("FbIntroHold=.25f,FbIntroRelease=.7f"),"the kneel lets go limb by limb, the feet are solved to their spots, the cut is held then released");
        Check(scenery.Contains("CreateFromYawPitchRoll(f.FeatherTurn*Mathf.Deg2Rad,f.FeatherPitch*Mathf.Deg2Rad,f.FeatherRoll*Mathf.Deg2Rad)"),"the feather drawn as it falls");
        return count-n0;
    }
    // ---- The cues (Core NativeFallenBossCues, Runtime NativeFallenBossAudio.cs) ----
    // Every key has its constant and is fired from somewhere; the cutscene's and the fight's moments land on their clocks' times
    // (each move's tell and strike on the authority's), any window split gives the same cues once, the OHK tells last their windups.
    private static string Dotted(string name)=>"boss.noctiel."+Regex.Replace(name,"(?<=[a-z])([A-Z])",".$1").ToLowerInvariant();
    private static int Cues(Func<string,string> R)
    {
        var keys=NativeFallenBossCues.Keys;var consts=typeof(NativeFallenBossCues).GetFields().Where(x=>x.IsLiteral&&x.FieldType==typeof(byte)).ToArray();
        Check(consts.Length==keys.Length&&consts.Select(x=>(byte)x.GetValue(null)).Distinct().Count()==keys.Length,"a constant for each of the 57 keys");
        foreach(var c in consts)Check(keys[(byte)c.GetValue(null)]==Dotted(c.Name),c.Name+" is "+Dotted(c.Name));
        var core=R("Assets/ChipArena/Core/NativeFallenBossCues.cs");var audio=R("Assets/ChipArena/Runtime/NativeFallenBossAudio.cs");
        foreach(var c in consts)Check(core.Contains("Add("+c.Name+",")||audio.Contains("NativeFallenBossCues."+c.Name+",")||audio.Contains("NativeFallenBossCues."+c.Name+":"),c.Name+" is fired from somewhere");
        var buf=new FallenBossCue[NativeFallenBossCues.QueueSize];int moments=0;
        // The cutscene: in order, every cue flat, the same in any windows.
        var all=new System.Collections.Generic.List<FallenBossCue>();int n=NativeFallenBossCues.Cutscene(-1,60,buf);all.AddRange(buf.Take(n));
        Check(n==17&&all.All(c=>c.Flat)&&all.Select(c=>c.At).SequenceEqual(all.Select(c=>c.At).OrderBy(x=>x)),"the cutscene's 17 cues, in order, all flat ("+n+")");
        Check(all[0].Key==NativeFallenBossCues.Feather&&Math.Abs(all[0].At-NativeFallenBossPreview.FeatherLand)<1e-4f&&all[n-1].Key==NativeFallenBossCues.SlashPart&&Math.Abs(all[n-1].At-NativeFallenPilots.CutsceneEnd)<1e-4f,"from the feather's touch to the opening Slash on the drop");
        var split=new System.Collections.Generic.List<FallenBossCue>();for(int i=0;i<4400;i++){int k=NativeFallenBossCues.Cutscene(-1+i*.0137f,-1+(i+1)*.0137f,buf);split.AddRange(buf.Take(k));}
        Check(split.Select(c=>(c.Key,c.At)).SequenceEqual(all.Select(c=>(c.Key,c.At))),"the cutscene the same in small windows: each cue once");moments+=n;
        // Each move: its tell at the action's start and its strike on the hit, as the authority strikes.
        var me=new Vector3(0,0,14);
        foreach(var m in NativeFallenBoss.Moves)
        {
            float A=10,wind=m.Id=="thrones"?1.2f:Math.Max(m.Kills?2.4f:0,m.Wind),H=A+wind;
            var v=new FallenCueFight{Boss=Vector3.Zero,From=Vector3.Zero,To=new Vector3(0,0,12),Listener=me,HasListener=true,Action=m.Id,ActionAt=A,HitAt=H,ActionYaw=0,SecondWing=true,Seed=4242,
                Pools=new[]{new V3(3,0,10),new V3(-20,0,4)},PhaseChangeAt=-100,PhaseSeenAt=-100,DraftAt=-100,DeathAt=-100,Started=-1,HushAt=-100};
            var whole=new System.Collections.Generic.List<FallenBossCue>();n=NativeFallenBossCues.Fight(v,A-1,H+8,buf);whole.AddRange(buf.Take(n));
            var parts=new System.Collections.Generic.List<FallenBossCue>();int steps=(int)((wind+9)*60);for(int i=0;i<steps;i++){int k=NativeFallenBossCues.Fight(v,A-1+i/60f,i+1==steps?H+8:A-1+(i+1)/60f,buf);parts.AddRange(buf.Take(k));}
            Check(n>0&&parts.Select(c=>(c.Key,c.At)).OrderBy(c=>c.At).ThenBy(c=>c.Key).SequenceEqual(whole.Select(c=>(c.Key,c.At)).OrderBy(c=>c.At).ThenBy(c=>c.Key)),m.Id+": the same cues in frame-sized windows");
            bool At(byte key,float at)=>whole.Any(c=>c.Key==key&&Math.Abs(c.At-at)<1e-3f);
            foreach(var c in whole)Check(float.IsFinite(c.At3.X+c.At3.Y+c.At3.Z)&&c.Gain>0&&c.Flat==NativeFallenBossCues.Flat(c.Key),m.Id+": a finite cue, flat as its key plays");
            switch(m.Id)
            {
                case "cleave":Check(At(NativeFallenBossCues.Cleave,H)&&At(NativeFallenBossCues.Cleave,H+.3f)&&At(NativeFallenBossCues.Cleave,H+.6f),"cleave: a cut on each of the authority's three strikes");break;
                case "vow":Check(At(NativeFallenBossCues.VowDraw,A)&&At(NativeFallenBossCues.Vow,H),"vow: the water parts, then the thrust on the hit");break;
                case "severance":Check(At(NativeFallenBossCues.SeveranceWind,A)&&At(NativeFallenBossCues.Severance,H)&&At(NativeFallenBossCues.Severance,H+.6f)&&At(NativeFallenBossCues.Wave,H+(14-3)/34f)&&At(NativeFallenBossCues.Wave,H+.6f+(14-3)/34f),"severance: the swing, the second wing, each wave as it reaches a pilot 14 m ahead (34 m/s)");break;
                case "descent":Check(At(NativeFallenBossCues.DescentMark,A)&&At(NativeFallenBossCues.Leap,NativeFallenLiveMotion.Takeoff(A,H))&&At(NativeFallenBossCues.Land,H)&&whole.First(c=>c.Key==NativeFallenBossCues.Land).At3==v.To,"descent: the ring, the leap as the body leaves, the landing on the hit where it lands");break;
                case "rain":Check(At(NativeFallenBossCues.RainLines,A)&&whole.Count(c=>c.Key==NativeFallenBossCues.Rain)==6&&whole.Where(c=>c.Key==NativeFallenBossCues.Rain).All(c=>c.At>=H&&c.At<H+2.4f),"rain: the lines, then blades through the strike");break;
                case "verdict":case "last":Check(At(NativeFallenBossCues.VerdictCrack,A)&&At(NativeFallenBossCues.VerdictSplit,H),m.Id+": the crack through the tell, the split on the hit");break;
                case "eclipse":Check(At(NativeFallenBossCues.Eclipse,A)&&whole.Count(c=>c.Key==NativeFallenBossCues.EclipsePool)==2,"eclipse: the dark through the tell, a chime at each pool");break;
                case "fall":Check(whole.Count(c=>c.Key==NativeFallenBossCues.FallMark)==4&&whole.Count(c=>c.Key==NativeFallenBossCues.Fall&&c.At>=H&&c.At<H+.2f)==4,"fall: four marks and four columns on the volley");break;
                case "sweep":Check(At(NativeFallenBossCues.SweepShake,A)&&At(NativeFallenBossCues.Sweep,H),"sweep: the shake, then the sweep on the hit");break;
                case "choir":case "thrones":Check(At(NativeFallenBossCues.Choir,A),m.Id+": the choir with the summons");break;
                case "penance":Check(At(NativeFallenBossCues.Penance,A),"penance: the pour");break;
            }
            moments+=n;
        }
        // The one-hit kills' tells: the shortest take still runs to within a beat of the hit, the longest ends by it.
        using(var doc=JsonDocument.Parse(R("../../docs/audio/sound-requests-noctiel.json")))
        {
            var target=doc.RootElement.GetProperty("sounds").EnumerateArray().ToDictionary(s=>s.GetProperty("key").GetString(),s=>(min:s.GetProperty("targetSeconds").GetProperty("min").GetSingle(),max:s.GetProperty("targetSeconds").GetProperty("max").GetSingle()));
            foreach(var (tell,wind) in new[]{("boss.noctiel.verdict.crack",2.4f),("boss.noctiel.eclipse",3.2f)})Check(target[tell].min>=wind-NativeFallenSong.Beat&&target[tell].max<=wind+.35f,tell+" lasts its windup");
        }
        // The phase change (seen late here: the Slash at once, the halves part on the cut), the chips, the death, the enrage and the Hush.
        {
            var v=new FallenCueFight{Boss=Vector3.Zero,Listener=me,HasListener=true,PhaseChangeAt=50,PhaseSeenAt=48.5f,DraftAt=60,DeathAt=100,Started=0,HushAt=30,HushSeconds=6,HushPlace=me};
            n=NativeFallenBossCues.Fight(v,0,800,buf);var c=buf.Take(n).ToList();bool At(byte key,float at)=>c.Any(x=>x.Key==key&&Math.Abs(x.At-at)<1e-3f);
            Check(At(NativeFallenBossCues.Slash,48.5f)&&At(NativeFallenBossCues.Slowdown,48.5f)&&At(NativeFallenBossCues.SlashPart,50-1.2f),"a phase change: the Slash when it is seen, the halves part on the cut");
            Check(c.Count(x=>x.Key==NativeFallenBossCues.Chip)==3&&At(NativeFallenBossCues.Chip,60+1.2f)&&At(NativeFallenBossCues.Chip,60+1.2f+4*NativeFallenSong.Beat),"the three chips turn over with the chip screen's");
            Check(At(NativeFallenBossCues.Death,100.2f)&&At(NativeFallenBossCues.Shatter,100+NativeFallenBossPreview.DeathHit)&&At(NativeFallenBossCues.Enrage,720)&&At(NativeFallenBossCues.Hush,34.5f),"the death, the shell's shatter on the song's hit, the enrage at 12:00, the Hush warning 1.5 s before the water opens");
            moments+=n;
        }
        // Fall of Light's columns are the authority's, nearest first; Feather Rain's blades fall on its lanes.
        {
            var v=new FallenCueFight{Listener=new Vector3(5,0,-9),HasListener=true,Seed=777,From=new Vector3(2,0,3)};float last=-1;var seen=new System.Collections.Generic.HashSet<Vector3>();
            for(int k=0;k<4;k++)
            {
                var c=NativeFallenBossCues.ColumnNear(v,k);int x=(int)Math.Floor(c.X/8),z=(int)Math.Floor(c.Z/8);float d=Vector2.Distance(new Vector2(c.X,c.Z),new Vector2(5,-9));
                Check(NativeFallenRules.Column(x,z,777)&&Math.Abs(c.X-(x*8+4))<1e-3f&&Math.Abs(c.Z-(z*8+4))<1e-3f&&d>=last-1e-3f&&seen.Add(c),"column "+k+" is a falling column, the next nearest");last=d;
            }
            for(int lane=0;lane<8;lane++)
            {
                var c=NativeFallenBossCues.RainAt(v,lane);float angle=777*37+lane*45+(lane%2)*19;var dd=NativeFallenRules.Dir(angle);var o=new Vector2(1,1.5f)+new Vector2(dd.Y,-dd.X)*(lane-3.5f)*7;var w=new Vector2(c.X,c.Z)-o;
                Check(Math.Abs(w.X*dd.Y-w.Y*dd.X)<1e-3f,"a rain blade on lane "+lane);
            }
        }
        // The wiring.
        var scenery=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");var chain=R("Assets/ChipArena/Runtime/NativeBackroomsBossPresentation.cs");
        Check(scenery.Contains("FallenShown(f);FallenCues(arena,f,now,live,p);")&&scenery.Contains("fbLast=-1;FallenCueReset();")&&scenery.Contains("FallenStepCue(fbFootL.position,now);")&&scenery.Contains("FallenStepCue(fbFootR.position,now);"),"the cues after the pose, reset with the stage, a footfall each time a sole touches");
        Check(chain.Contains("DeepEndBossCues(arena,menu);FallenBossCues(arena,menu);")&&audio.Contains("Cue(NativeFallenBossCues.Keys[c.Key],null,place,c.Gain);"),"NativeAudio plays them through the bank, no stand-in");
        Check(audio.Contains("if(a.Pve.Holding&&a.Pve.Cinematic==\"intro\")")&&audio.Contains("float p=a.Pve.HoldDuration-a.Pve.HoldLeft;"),"the cutscene's cues on the intro hold's clock");
        return moments;
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
        // The owner, 2026-11-07: walking never drops into the stance mid-stride, and it stands as the reference walk does, both feet down (the slow walk held).
        {
            var d=new NativeFallenLiveMotion{StandAt=.4f};var at=Vector3.Zero;var x=new NativeFallenLiveMotion.Facts{At=at,Yaw=0,Target=new Vector3(0,0,80)};float step=0;int drops=0;string lastTo=null;float standJump=0,lastStand=0;
            for(float now=0;now<6;now+=1/144f)
            {
                while(step<=now){float pace=now<3.5f?.62f+.3f*(float)Math.Sin(now*9):0;at+=new Vector3(0,0,pace/60);step+=1/60f;}x.Now=now;x.At=at;d.Step(f,x,bank);
                if(now>.8f&&now<3.4f&&f.ClipTo!="Armed-Walk-Slow")drops++;
                if(now>5)Check(f.ClipTo=="Armed-Walk-Slow"&&Math.Abs(f.TimeTo-.4f)<1e-4f&&f.Carry>.9f&&f.Air<.05f,"it stands as the reference does: the slow walk held with both feet down, the sword carried, at "+now);
                // The owner, 2026-11-07 (a recording of the idle): held, it settles into the reference's stance; walking, none of it.
                if(now>1&&now<3.4f)Check(f.Stand<1e-4f,"walking it takes none of the standing stance (Stand "+f.Stand+" at "+now+")");
                if(now>5)Check(f.Stand>.999f,"at rest it stands in the reference's stance (Stand "+f.Stand+" at "+now+")");
                if(now>0)standJump=Math.Max(standJump,Math.Abs(f.Stand-lastStand));lastStand=f.Stand;
                lastTo=f.ClipTo;
            }
            Check(drops==0,"a pace hovering at the walk's floor never drops it into the stance ("+drops+" frames)");
            Check(standJump<.06f,"the stance eases in and out (largest step "+standJump.ToString("0.000")+" a frame at 144 fps)");
            Check(float.IsNaN(new NativeFallenLiveMotion().StandAt),"without the bank's frame it keeps the old stance");
            {var old=new NativeFallenLiveMotion();var g=new FallenBossFrame();var y=new NativeFallenLiveMotion.Facts{At=Vector3.Zero,Yaw=0,Target=new Vector3(0,0,20)};for(float now=0;now<2;now+=1/60f){y.Now=now;old.Step(g,y,bank);}Check(g.Stand==0,"the old stance takes no standing stance");}
            var lib=new NativeVisualMotionClips.Library{Muscles=4};int stride=NativeVisualMotionClips.Head+4;var c=new NativeVisualMotionClips.Clip{Name="Armed-Walk-Slow",Length=1,Rate=10,Loop=true,Frames=11,Stride=stride,Data=new float[11*stride]};
            for(int k=0;k<11;k++){float ph=k/10f*6.2832f;c.Data[k*stride+NativeVisualMotionClips.Head+1]=(float)Math.Sin(ph+.6f);c.Data[k*stride+NativeVisualMotionClips.Head+2]=-(float)Math.Sin(ph+.6f);}lib.Clips[c.Name]=c;
            float feet=NativeFallenLiveMotion.BothFeetDown(lib,1,2);Check(Math.Abs(feet-.4f)<.051f||Math.Abs(feet-.9f)<.051f,"both feet down where the knees bend alike ("+feet+")");
            Check(float.IsNaN(NativeFallenLiveMotion.BothFeetDown(lib,-1,2))&&float.IsNaN(NativeFallenLiveMotion.BothFeetDown(null,1,2)),"no frame without the knees or the bank");
        }
        // The robe as cloth (the owner, 2026-11-07: "Its hard to see his legs ... integrate magica cloth"): open across the front, the sides to the knee,
        // the back to mid-calf, phase 2's shorter; the waist rows fixed for the simulation; both faces wound out and in; under the faulds' last ring.
        {
            float lowest1=9,lowest2=9,sideHem=9;bool open=true,front=false,wound=true,flipped=true,weights=true;
            foreach(int phase in new[]{1,2})
            {
                var g=NativeFallenRobe.Build(phase);int n=g.Side;
                Check(g.Positions.Length==n*2&&g.Normals.Length==n*2&&g.Triangles.Length%6==0,"phase "+phase+"'s robe has an outer and an inner copy");
                for(int i=0;i<n;i++)
                {
                    var q=g.Positions[i];float ang=(float)(Math.Atan2(q.X,q.Z-NativeFallenRobe.CentreZ)*180/Math.PI);if(Math.Abs(ang)>NativeFallenRobe.Front+5&&Math.Abs(ang)<NativeFallenRobe.Open)open=false;if(Math.Abs(ang)<5)front=true;
                    if(phase==1)lowest1=Math.Min(lowest1,q.Y);else lowest2=Math.Min(lowest2,q.Y);
                    if(phase==1&&Math.Abs(ang)<110&&Math.Abs(ang)>=55&&g.Uv[i].Y<.01f)sideHem=Math.Min(sideHem,q.Y);
                    if(g.Normals[n+i]!=-g.Normals[i]||g.Alpha[i]!=255||g.Alpha[n+i]!=0)flipped=false;
                    if(g.Weight1[i]<0||g.Weight1[i]>.5f||g.Bone1[i]<0||g.Bone1[i]>2)weights=false;
                }
                int half=g.Triangles.Length/2;
                for(int k=0;k<half;k+=3)
                {
                    int a=g.Triangles[k],b=g.Triangles[k+1],c=g.Triangles[k+2];var face=System.Numerics.Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);var o=g.Positions[a]+g.Positions[b]+g.Positions[c];o=new System.Numerics.Vector3(o.X/3,0,o.Z/3-NativeFallenRobe.CentreZ);
                    if(System.Numerics.Vector3.Dot(face,new System.Numerics.Vector3(o.X,0,o.Z))<0)wound=false;
                    if(g.Triangles[half+k]!=a+n||g.Triangles[half+k+1]!=c+n||g.Triangles[half+k+2]!=b+n)wound=false;
                }
            }
            Check(open&&front,"a narrow panel hangs between the legs, and nothing else within 55 degrees of the front: the legs show whole");
            Check(sideHem>.5f&&sideHem<.6f,"the side panels end at the knee ("+sideHem.ToString("0.000")+" m, the knee at .536)");
            Check(lowest1<.3f&&lowest2>lowest1+.08f,"the back hangs to mid-calf in phase 1 ("+lowest1.ToString("0.00")+"), burnt shorter in phase 2 ("+lowest2.ToString("0.00")+")");
            Check(wound&&flipped,"the outer faces face out, the inner copy faces in, the outline only outside");
            Check(weights,"skinned to the pelvis, the side hems leaning on their thighs at most half");
            Check(NativeFallenRobe.Fixed(1)&&NativeFallenRobe.Fixed(1-1f/NativeFallenRobe.Rows)&&!NativeFallenRobe.Fixed(1-2f/NativeFallenRobe.Rows),"the waist's two rows are fixed, the rest move");
            var paint=NativeFallenRobe.Paint(4,32);Check(paint[31*4]==0xFF0000u&&paint[30*4]==0xFF0000u&&paint[0]==0x00FF00u&&paint[27*4]==0x00FF00u,"the paint map: red at the waist, green below");
            Check(NativeFallenRobe.WaistX>=.158f&&NativeFallenRobe.WaistZ>=.126f&&NativeFallenRobe.Top<=.905f,"it hangs from under the faulds' last ring, outside it");
            var cloth=R("Assets/ChipArena/Runtime/NativeFallenCloth.cs");var body2=R("Assets/ChipArena/Runtime/NativeFallenBossBody.cs");var shown2=R("Assets/ChipArena/Runtime/NativeFallenBossPresentation.cs");
            Check(cloth.Contains("NativeVisualCloth.PaintMaps[name]=paint;")&&cloth.Contains("handle=NativeVisualCloth.Attach(root,skeleton);")&&body2.Contains("FallenClothBuild(most,skeleton);")&&body2.Contains("if(FallenClothOn&&f.Phase<=2&&part.Name.StartsWith(\"robe\",StringComparison.Ordinal))continue;")&&shown2.Contains("FallenClothFrame(f,eyeAt,flash,mirror,shown);"),"the skirt is built with the body, handed to the cloth, drawn after the pose, and the strips step aside");
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
        Check(body.Contains("FallenFeetPlant(f,now,hover);")&&body.IndexOf("FallenFeetPlant(f,now,hover);")<body.IndexOf("FallenGrip(f,origin,origin.y+f.Air+hover,intro,ip,iw,at,yawRot);")&&body.IndexOf("FallenGrip(f,origin,origin.y+f.Air+hover,intro,ip,iw,at,yawRot);")<body.IndexOf("FallenBladeClear(f,yawRot,now);")&&body.IndexOf("FallenBladeClear(f,yawRot,now);")<body.IndexOf("FallenChains(f,now,dt,yawRot,local);"),"feet, then the grip, then the blade cleared, then the chains");
        Check(!body.Contains("if(f.Plant>0)FallenPlant(f,origin);else if(f.Carry>0)FallenCarry(f);")&&body.Contains("g=Vector3.Lerp(cg,pg,plant);r=Quaternion.Slerp(cr,pr,plant);o=Vector3.Lerp(co,po,plant);w=Mathf.Clamp01(plant+carry);"),"a sword passing from the plant to the carry goes straight from one to the other");
        Check(body.Contains("Quaternion.FromToRotation(Vector3.down,FallenRobeWay(c.Pivot.X,c.Pivot.Z-.03f,c.Segment,yawRot))*yawRot"),"the robe drapes over the legs");
        Check(body.Contains("FbDragTip=.02f;private static readonly Vector3 FbDragWay=new Vector3(.99f,0,.1f)")&&body.Contains("var wrist=shoulder+yawRot*new Vector3(.17f*s,0,-.05f*s)+Vector3.down*(reach*.9f-breath);")&&body.Contains("float drop=Mathf.Clamp((grip.y-floor-FbDragTip*s)/along,.08f,.95f);"),"the sword carried as the reference carries it: from a hanging arm out from the hip and a little behind it, laid out to its right side and down, its point on the water");
        Check(body.Contains("if(fbLiveNow&&f.Stand>0)")&&body.IndexOf("if(fbLiveNow&&f.Stand>0)")<body.IndexOf("fbHandler.SetHumanPose(ref fbPose);")&&body.Contains("FbStandTo={.02f,.15f,0,.88f,0,0,.4f,.15f,0,.88f,0,0,0}"),"at rest the reference's stance is laid over the held stride before the pose is set");
        Check(scenery.Contains("if(tip.y<floor+.25f&&f.Plant<.5f&&now-fbWakeLast>.07f&&moved>=.6f)"),"the dragged point's wake is a ring every 0.6 m, none at rest");
        var audio=R("Assets/ChipArena/Runtime/NativeAudio.cs");var loading=R("Assets/ChipArena/Runtime/NativeStageLoadingUI.cs");var game=R("Assets/ChipArena/Runtime/ConversionGame.cs");
        Check(audio.Contains("map==NativeFallenRules.Map?NativeFallenSong.Id:")&&audio.Contains("Track(NativeFallenSong.Id,Music*.55f,StageCovered,")&&game.Contains("nativeAudio.StageCovered=stageLoad.Visible&&stageLoad.Phase!=StageLoadPhase.Reveal;"),"its song loads behind the cover and waits for the screen to open");
        Check(loading.Contains("bool intro=arena!=null&&(IntroActive||arena.Pve?.Holding==true&&arena.Pve.Cinematic==\"intro\");"),"a boss's intro hold keeps the intro's loading budget");
        Check(!R("Assets/ChipArena/Core/NativeFallenLiveMotion.cs").Contains("NativeArena"),"the director reads facts, not the arena");
    }
}
#endif
