using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ChipArena.UnityConversion;

// The DEV Sheldon Intro (Core/NativeSheldonIntro.cs): its DEV gate on real NativeAccess values (live DEV access only, as the
// Sheldon Head), its replication through the existing intro id, its music key and timing, the beat marks the glitch, the
// figure, the face and the camera land on, determinism, the reduced-effects tier (every beat kept, nothing strobing) and the
// low graphics tier, the glitch-typed card text, and source scans of the runtime hooks (Runtime is not compiled here). The
// general entrance rules (camera clearance and smoothness, purity, budgets, no allocation) run on it in EntranceDirectorChecks.
internal static class SheldonIntroChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Sheldon Intro: "+what);count++;}
    private static NativeAccess Access(bool developer,double expires,params string[] grants)
    {
        var a=(NativeAccess)Activator.CreateInstance(typeof(NativeAccess),true);var t=typeof(NativeAccess);
        t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
        var set=(HashSet<string>)t.GetField("grants",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(a);foreach(var g in grants)set.Add(g);return a;
    }
    public static IEnumerable<string> Run(string root)
    {
        const string S=NativeSheldonIntro.Style;double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // 1. The gate: live DEV access only; a grant, an expired DEV token or a guest falls back to the weapon signature.
        var dev=Access(true,now+60000);var granted=Access(false,now+60000,"intro:"+S);var expired=Access(true,now-1000);
        Check(S=="sheldon-intro"&&NativeCosmeticGate.DevOnly(S)&&NativeCosmeticGate.Restricted(S),"the id is DEV-only");
        Check(NativeCosmeticGate.Allowed(dev,"intro",S,now)&&!NativeCosmeticGate.Allowed(granted,"intro",S,now)&&!NativeCosmeticGate.Allowed(expired,"intro",S,now)&&!NativeCosmeticGate.Allowed(NativeAccess.Guest,"intro",S,now)&&!NativeCosmeticGate.Allowed(null,"intro",S,now),"only live DEV access may pick it");
        foreach(var (a,ok) in new[]{(dev,true),(granted,false),(expired,false),(NativeAccess.Guest,false)})
        {
            var seat=new NativeSeat{spec="sword",intro=S,overrideIntro=true,access=a};Check(NativeIntroRules.Style(seat)==(ok?S:"weapon-sword"),"the host stages it for "+(ok?"DEV":"a non-DEV seat")+" ("+NativeIntroRules.Style(seat)+")");
        }
        {string intro=S;bool on=true;NativeIntroRules.Migrate(ref intro,ref on);Check(intro==S&&on,"a saved or received pick survives migration");}
        Check(NativeIntroRules.Pickable(S)&&NativeIntroRules.Entrance(S,"pistol")==S&&Array.IndexOf(NativeIntroRules.Entrances,S)<0&&NativeEntranceDirector.Directed(S),"every client stages the replicated id (the director), outside the public entrance list");
        Check(NativeIntroRules.Duration(S)==4.8&&NativeIntroRules.NameAt(S)==3.2&&NativeIntroRules.OverlayDuration(S)==4.8&&NativeIntroRules.IntroWeapon(S)==null&&NativeIntroRules.IntroSong(S)==null,"standard timing (4.8 s, name card 3.2 s), empty hands, no boss song");
        Check(NativeSoundKeys.Intro(S)=="intro.sheldon-intro"&&NativeSoundKeys.Find("intro.sheldon-intro",out var key)&&Math.Abs(key.Cap-4.8f)<1e-5f&&!key.Key.Contains("dev-"),"its music key (2D, 4.8 s; no dev- prefix)");
        Check(File.Exists(Path.Combine(root,"Assets/Conversion/Resources/Audio/"+NativeIntroRules.IntroSting(S)+".mp3")),"a built-in sting without LocalAssets");
        Check(NativeLocker.DevItem(S)&&NativeLocker.IntroText(S).Length>0&&NativeLocker.IntroIcon(S)=="scan-line"&&NativeIntroRules.StyleName(S)=="DEV · Sheldon Intro","the Locker card (DEV badge, text, icon)");
        yield return $"Sheldon Intro gate: live DEV access only (grants, expired tokens and guests get their weapon signature); every client stages the replicated id; standard 4.8 s timing; music key intro.sheldon-intro";

        // 2. The beat marks: the music's moments (s from the intro start), and what lands on each.
        var marks=new[]{.49f,1.43f,1.90f,1.99f,2.00f,2.37f,3.36f,3.76f,4.24f};
        Check(NativeSheldonIntro.Beats.SequenceEqual(marks)&&marks.Zip(marks.Skip(1),(a,b)=>a<b).All(x=>x),"the beat marks are the music's (in order)");
        var sc=NativeEntranceDirector.Script(S) as NativeSheldonIntroScript;Check(sc!=null&&sc.Ready&&sc.Style==S,"the director makes the script");sc.Reduced=false;sc.Prepare();
        for(float age=0;age<=5;age+=1/240f)Check(sc.Clock(age)==Math.Min(age,NativeEntranceScript.Duration+1),"the clock is the age (no slow-motion beat) at "+age);
        NativeSheldonGlitch G(float t,bool r=false,bool low=false)=>NativeSheldonIntro.Glitch(t,r,low);
        Check(G(NativeSheldonIntro.Slam-.02f).Tear<.35f&&G(NativeSheldonIntro.Slam+.004f).Tear>.9f&&G(NativeSheldonIntro.Slam+.004f).Blocks>.7f,"the slam tears the frame on .49 s");
        Check(G(NativeSheldonIntro.Dip+.3f).Tear>G(NativeSheldonIntro.Dip-.01f).Tear-.4f&&G(NativeSheldonIntro.Silence-.02f).Black>.3f,"the dip darkens into the silence");
        for(float t=NativeSheldonIntro.Silence;t<NativeSheldonIntro.Return;t+=.005f){var g=G(t);Check(g.Black==1&&g.Tear==0&&g.Split==0&&sc.Solid(t,CapturedRig.Chest)==0,"the silence is a black cut frame at "+t);}
        Check(G(NativeSheldonIntro.Return+.02f).Black>.3f&&G(NativeSheldonIntro.Return+.02f).Black<.7f&&sc.Holo(NativeSheldonIntro.Return+.05f,out _,out float holo,out _)&&holo>.1f,"the soft re-entry: a dim frame and a projection");
        Check(G(NativeSheldonIntro.Resolve+.004f).Tear>.75f&&G(NativeSheldonIntro.Resolve+.004f).Black<.05f&&G(NativeSheldonIntro.Resolve+.004f).Chroma>.9f,"the hard re-entry hit on 2.37 s");
        for(float t=0;t<NativeSheldonIntro.Resolve;t+=1/240f)Check(sc.Solid(t,CapturedRig.Head)<=.5f||!(t>=NativeSheldonIntro.Return)&&t>NativeSheldonIntro.Slam-.03f&&t<NativeSheldonIntro.Silence,"the hood stays off until the resolve (but for glitch flickers before the silence) at "+t);
        for(int b=0;b<22;b++)Check(sc.Solid(NativeSheldonIntro.Resolve+.001f,b)==1&&sc.Solid(4.79f,b)==1,"the figure resolves solid on 2.37 s (bone "+b+")");
        float build0=G(NativeSheldonIntro.BuildFrom-.05f).Chroma,build=G((NativeSheldonIntro.BuildFrom+NativeSheldonIntro.BuildTo)*.5f).Chroma;Check(build>build0+.3f,"the push-in's chromatic aberration ("+build0+" -> "+build+")");
        Check(G(NativeSheldonIntro.Flare+.004f).Split>.7f&&G(NativeSheldonIntro.Flare+NativeSheldonIntro.Stutter+.004f).Split>.55f&&G(NativeSheldonIntro.Flare+.06f).Split<G(NativeSheldonIntro.Flare+.004f).Split,"the flare's three stutters from 4.24 s");
        Check(G(NativeSheldonIntro.Cut-.05f).Black<.3f&&G(4.799f).Black>.95f&&G(4.75f).Tear>.7f,"the final glitch cut to black at the hand-off");
        var rest=NativeSheldonIntro.Face(3.0f,false);float jaw=0,wide=0;for(float t=NativeSheldonIntro.Flare;t<NativeSheldonIntro.Flare+.55f;t+=1/240f){var f=NativeSheldonIntro.Face(t,false);jaw=Math.Max(jaw,f.JawOpen);wide=Math.Max(wide,f.GrinWide);}
        Check(jaw>.6f&&wide>rest.GrinWide+.12f&&NativeSheldonIntro.Face(4.79f,false).JawOpen<.3f,"the grin flares into a laugh on 4.24 s and closes (jaw "+jaw+", width "+wide+")");
        Check(NativeSheldonIntro.Face(NativeSheldonIntro.BuildTo,false).GrinWide>rest.GrinWide+.05f,"the grin builds through the dip");
        var snap=Enumerable.Range(0,80).Select(i=>NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+i/240f,false).RingGrow).Distinct().Count();
        Check(NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+.005f,false).RingGrow>.2f&&NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+.3f,false).RingGrow==0&&snap==5,"the ring clicks into place in four steps ("+snap+" values)");
        Check(NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+.01f,false).Glow>1.8f,"the face flashes on as it snaps on");
        // The push-in: square on the face, closing in from the dip to the hand-off; the hand on the hood.
        sc.Camera(NativeSheldonIntro.BuildFrom+.01f,out var v0);sc.Camera(NativeEntranceScript.Settle,out var v1);var head0=sc.Anchor(NativeEntranceAnchor.Head,NativeSheldonIntro.BuildFrom+.01f);var head1=sc.Anchor(NativeEntranceAnchor.Head,NativeEntranceScript.Settle);
        float d0=(v0.Eye-head0).Length(),d1=(v1.Eye-head1).Length(),prev=99;
        for(float t=NativeSheldonIntro.BuildFrom+.01f;t<=NativeEntranceScript.Settle;t+=1/60f){sc.Camera(t,out var v);float d=(v.Eye-head0).Length();Check(d<=prev+1e-4f,"the push-in never backs off at "+t);prev=d;}
        Check(d1<d0*.65f&&v1.Fov<v0.Fov,"the push-in closes in ("+d0.ToString("0.00")+" -> "+d1.ToString("0.00")+" m, fov "+v0.Fov+" -> "+v1.Fov+")");
        var facing=sc.Anchor(NativeEntranceAnchor.Forward,4f);var toEye=v1.Eye-head1;Check(Vector3.Dot(Vector3.Normalize(new Vector3(facing.X,0,facing.Z)),Vector3.Normalize(new Vector3(toEye.X,0,toEye.Z)))>.97f,"the push-in ends square on the face");
        for(float t=NativeSheldonIntroScript.HandAt;t<=4.8f;t+=.05f)Check((sc.Anchor(NativeEntranceAnchor.LeftHand,t)-sc.Anchor(NativeEntranceAnchor.Head,t)).Length()<.3f,"the left hand is on the hood at "+t);
        yield return $"Sheldon Intro beats: slam .49, dip 1.43, black 1.90-1.99, soft return 2.00, resolve 2.37 (hood, face and ring snap on), build 3.36-3.76 (chromatic push-in {d0:0.00} -> {d1:0.00} m), flare 4.24 (three stutters), cut to black by 4.8";

        // 3. Determinism: pure functions of the age (any order, repeated).
        var ages=Enumerable.Range(0,577).Select(i=>i/120f).ToArray();var first=ages.Select(a=>(G(a),NativeSheldonIntro.Face(a,false),G(a,true),NativeSheldonIntro.Face(a,true))).ToArray();
        var rng=new Random(37);foreach(int i in Enumerable.Range(0,ages.Length).OrderBy(_=>rng.Next())){var a=ages[i];Check(first[i].Equals((G(a),NativeSheldonIntro.Face(a,false),G(a,true),NativeSheldonIntro.Face(a,true))),"the same frame at "+a);}
        var buf=new char[64];var buf2=new char[64];
        for(float t=0;t<1.5f;t+=.01f){int n=NativeSheldonIntro.Typed("SHELDON",t,false,19,buf,out _),m=NativeSheldonIntro.Typed("SHELDON",t,false,19,buf2,out _);Check(n==m&&new string(buf,0,n)==new string(buf2,0,m),"the typed name is deterministic");}
        yield return $"Sheldon Intro determinism: {ages.Length} ages evaluated twice in shuffled order give the same glitch and face (both tiers)";

        // 4. Reduced effects: every beat, no strobing (soft steps at 6 Hz, ramped blackouts, no blocks, specks or flicker).
        float worstStep=0,worstFull=0;int flips=0,seeds=0,last=-1;float lastBlack=0,lastDir=0;
        for(int i=0;i<=(int)(4.8f*60);i++)
        {
            float t=i/60f;var r=G(t,true);var f=G(t);
            Check(r.Blocks==0&&r.Noise==0&&r.Tear<=.3f&&r.Split<=.35f&&r.Chroma<=.4f&&r.Scan<=.3f,"reduced glitch stays soft at "+t);
            if(i>0){worstStep=Math.Max(worstStep,Math.Abs(r.Black-lastBlack));float dir=Math.Sign(r.Black-lastBlack);if(dir!=0&&lastDir!=0&&dir!=lastDir)flips++;if(dir!=0)lastDir=dir;}
            worstFull=Math.Max(worstFull,i>0?Math.Abs(f.Black-G((i-1)/60f).Black):0);lastBlack=r.Black;if(r.Seed!=last){seeds++;last=r.Seed;}
        }
        Check(worstStep<=.2f&&worstFull>=.6f,"reduced blackouts ramp ("+worstStep+" a frame at most; full effects cut: "+worstFull+")");
        Check(flips<=6,"reduced blackouts never flicker ("+flips+" turns)");Check(seeds<=30,"reduced glitch steps at 6 Hz ("+seeds+" in 4.8 s)");
        Check(G(NativeSheldonIntro.Slam+.04f,true).Tear>.2f&&G(NativeSheldonIntro.Resolve+.06f,true).Split>.2f&&G(NativeSheldonIntro.Silence+.05f,true).Black>.8f&&G(NativeSheldonIntro.Flare+.08f,true).Split>.2f,"reduced effects keep every beat");
        sc.Reduced=true;int solidChanges=0;float lastSolid=-1;
        for(float t=NativeSheldonIntro.Slam;t<NativeSheldonIntro.Silence-.01f;t+=1/240f){float s=sc.Solid(t,CapturedRig.Chest);if(lastSolid>=0&&s!=lastSolid)solidChanges++;lastSolid=s;}
        var ghosts=new NativeEntranceGhost[8];float apart=0;for(float t=0;t<4.8f;t+=1/60f){int n=sc.Ghosts(t,ghosts);for(int k=0;k<n;k++)apart=Math.Max(apart,ghosts[k].Offset.Length());}
        var lights=new NativeEntranceLights();int lightTurns=0,lastPoints=-1;for(float t=NativeSheldonIntro.Slam+.8f;t<NativeSheldonIntro.Silence-.05f;t+=1/60f){sc.Lights(t,lights);if(lastPoints>=0&&lights.Points!=lastPoints)lightTurns++;lastPoints=lights.Points;}
        sc.Reduced=false;
        Check(solidChanges==0&&apart<=.03f&&lightTurns==0,"reduced: a steady figure ("+solidChanges+" changes), close RGB ghosts ("+apart.ToString("0.000")+" m), no flickering light ("+lightTurns+")");
        int fullChanges=0;lastSolid=-1;for(float t=NativeSheldonIntro.Slam;t<NativeSheldonIntro.Silence-.01f;t+=1/240f){float s=sc.Solid(t,CapturedRig.Chest);if(lastSolid>=0&&s!=lastSolid)fullChanges++;lastSolid=s;}
        Check(fullChanges>5,"full effects: the figure glitches ("+fullChanges+" drop-outs)");
        // The low graphics tier: one shader stage, fewer set pieces.
        for(float t=0;t<4.8f;t+=.02f){var l=G(t,false,true);Check(l.Passes==1&&l.Blocks==0&&l.Scan==0&&l.Noise==0,"low graphics runs one stage at "+t);}
        Check(G(1).Passes==3,"full graphics runs every stage");
        var sink=new Counter();int hi=0,lo=0;for(float t=0;t<4.8f;t+=1/30f){sink.N=0;sc.Effects(t,sink,1);hi+=sink.N;sink.N=0;sc.Effects(t,sink,.3f);lo+=sink.N;}
        Check(lo<hi*.8f,"the effects setting thins the effects ("+lo+" of "+hi+")");
        yield return $"Sheldon Intro tiers: reduced effects keep every beat with ramped blackouts ({worstStep:0.00} a frame), {seeds} soft steps, a steady figure and no flicker; low graphics runs one shader stage";

        // 5. The card's glitch-typed text.
        Check(NativeSheldonIntro.Typed("SHELDON",-1,false,1,buf,out _)==0&&NativeSheldonIntro.Typed("",1,false,1,buf,out _)==0,"nothing before the card");
        {int n=NativeSheldonIntro.Typed("SHELDON",5,false,1,buf,out bool done);Check(done&&n==7&&new string(buf,0,n)=="SHELDON","the name ends typed");}
        {int n=NativeSheldonIntro.Typed("SHELDON",.1f,false,1,buf,out bool done);int shown=(int)(.1f*NativeSheldonIntro.TypeRate);Check(!done&&n==shown+4&&new string(buf,0,shown)=="SH","revealed letters then junk glyphs");for(int i=shown;i<n;i++)Check("#%&@$01<>/\\|=+*?!".IndexOf(buf[i])>=0,"junk ahead of the cursor");Check(NativeSheldonIntro.Typed("SHELDON",.1f,true,1,buf,out _)==shown+2,"reduced effects show fewer junk glyphs");}
        yield return "Sheldon Intro card: the name glitch-types in (revealed letters, junk glyphs ahead of the cursor, done by the end); deterministic";

        // 6. Runtime hooks (source scans).
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string intros=R("NativeIntros.cs"),hat=R("NativeHatPresentation.cs"),pres=R("NativeSheldonIntroPresentation.cs"),overlay=R("NativeMatchOverlays.cs"),game=R("ConversionGame.cs"),profile=R("NativeProfileUI.cs");
        Check(intros.Contains("var sheldon=script as NativeSheldonIntroScript;if(sheldon!=null)actor=SheldonIntroBegin(sheldon,actor,pilot,t);")&&intros.Contains("try{sheldonScripted=sheldon!=null;SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age,reveal:entranceReveal);}finally{sheldonScripted=false;}"),"the directed intro dresses the pilot and scripts the face only while it draws the pilot");
        Check(pres.Contains("var look=actor.ViewCopy();look.Hat=NativeSheldonHead.Hat;look.HatColor=pilot.Seat.hatColor;look.BodyColor=NativeSheldonIntro.Suit;look.AccentColor=NativeSheldonIntro.Accent(pilot.Seat.hatColor);")&&NativeSheldonIntro.Accent("")=="#22e6ff"&&NativeSheldonIntro.Accent("#ff3dcf")=="#ff3dcf"&&hat.Contains("var face=sheldonScripted?sheldonScriptFace:NativeSheldonHead.Evaluate("),"the intro wears the whole Sheldon Head look in the pilot's hat colour (a copy: the pilot keeps its own hat and colours)");
        Check(overlay.Contains("if(!boss&&style==NativeSheldonIntro.Style){SheldonCard(pilot,rawAge,index,total,team);return;}"),"the corrupted card replaces the standard one");
        Check(game.Contains("private void LateUpdate(){LateFrame();DamageScreen();GlitchScreen();}")&&game.Contains("NativeGlitchScreenPass.Release(true);")&&pres.Contains("bool on=presentation!=null&&presentation.GlitchFrame==Time.frameCount;"),"the glitch pass runs only on frames the intro staged, and is released");
        var shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeGlitchScreen.shader"));
        Check(shader.Contains("Shader \"Hidden/ChipArena/NativeGlitchScreen\"")&&shader.Contains("// BEGIN SHARED")&&shader.Contains("// END SHARED")&&shader.Contains("float4 _GlA;")&&shader.Contains("float4 _GlB;")&&shader.Contains("float4 _GlC;")&&shader.Contains("float4 _GlTint;")&&!shader.Contains("tex2D(")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeGlitchScreen.shader.meta")),"the glitch shader (no derivative texture reads)");
        Check(pres.Contains("Resources.Load<Shader>(\"NativeGlitchScreen\")")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("\"NativeGlitchScreen\"}"),"the pass loads it and the Editor build check covers it");
        Check(profile.Contains("\"dev-siren\",\"sheldon-intro\"};")&&profile.Contains("{\"sheldon-intro\",\"DEV · Sheldon Intro\"}"),"the Locker lists it (gated)");
        yield return $"Sheldon Intro runtime: the pilot copy in the Sheldon Head, the scripted face, the glitch pass on staged frames only, the corrupted card and the Locker entry ({count} checks in all)";
    }
    private sealed class Counter:INativeEntranceFx,INativeEntranceProps
    {
        public int N;
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>N++;
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>N++;
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>N++;
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color)=>N++;
    }
}
