using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using ChipArena.UnityConversion;

// Engine-free checks for the dev-siren entrance's light (Core/NativeSirenIntroLight.cs) against the pinned source
// (Tests/siren-intro-parity.json, tools/export_siren_intro_fixtures.mjs running client/siren/intro.ts): the bulb, fill,
// ambient, glow and halo frame by frame; the stutter and flash timeline against the beats the boss song is cued from
// (PveContent bossMusic); the cell's geometry and bulb in the scene bank; the stage block's light actually reaching the
// cell's walls and floor (three's falloff, independent of Unity's pixel-light limit), with the lit beats visible and the
// dark beats black after the tone curve; every entrance stage's point lights fitting the block; the intro music routing;
// the source shaders' stage block contract; and the clock a guest draws the entrance at (NativeIntroClock) against its locally
// playing song. Unity's rendering and audio are not exercised (see the offline renders in DEV_LOG).
internal static class SirenIntroChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren intro: "+what);}
    private static void Near(double a,double b,string what,double tolerance=2e-7){if(!(Math.Abs(a-b)<=tolerance*Math.Max(1,Math.Abs(b))))throw new Exception("Siren intro: "+what+" "+a+" vs "+b);}
    private static double[] Doubles(JsonElement e){var v=new double[e.GetArrayLength()];int i=0;foreach(var x in e.EnumerateArray())v[i++]=x.GetDouble();return v;}
    private static double Encode(double c)=>NativeEntranceLight.ToGamma(Math.Max(0,Math.Min(1,c)));
    // three r180 lights_lambert + getDistanceAttenuation, written out again so the Core block is checked against the source's formula.
    private static double ThreeLambert(double albedo,double colour,double intensity,double distance,double decay,double px,double py,double pz,double nx,double ny,double nz,double lx,double ly,double lz)
    {
        double dx=lx-px,dy=ly-py,dz=lz-pz,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),fall=1/Math.Max(Math.Pow(d,decay),.01);
        if(distance>0)fall*=Math.Pow(Math.Min(1,Math.Max(0,1-Math.Pow(d/distance,4))),2);
        return albedo/Math.PI*colour*intensity*fall*Math.Max(0,(nx*dx+ny*dy+nz*dz)/d);
    }
    public static IEnumerable<string> Run(string root)
    {
        using var fixtureDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-intro-parity.json")));var fx=fixtureDoc.RootElement;
        using var pveDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")));var music=pveDoc.RootElement.GetProperty("bossMusic");
        // 1. One beat table: the source intro's BEATS are boss-music.ts's first 18 beats, which the native music is cued from.
        double[] beats=Doubles(music.GetProperty("beats"));var track=fx.GetProperty("track");double[] sourceBeats=Doubles(track.GetProperty("beats"));
        Check(beats.Length>=NativeSirenIntroLight.BeatCount&&sourceBeats.Length==NativeSirenIntroLight.BeatCount,"beat tables");
        for(int i=0;i<sourceBeats.Length;i++)Check(beats[i]==sourceBeats[i],"beat "+i+" of the music table differs from the source intro's");
        Check(music.GetProperty("id").GetString()==track.GetProperty("id").GetString()&&music.GetProperty("introEnd").GetDouble()==track.GetProperty("introEnd").GetDouble(),"boss song identity");
        var cueBeat=new Dictionary<string,int>{{"bulb",NativeSirenIntroLight.BulbBeat},{"flicker",NativeSirenIntroLight.FlickerBeat},{"glimpse",NativeSirenIntroLight.GlimpseBeat},{"reveal",NativeSirenIntroLight.RevealBeat},{"blackout",NativeSirenIntroLight.BlackoutBeat},{"smile",NativeSirenIntroLight.SmileBeat}};int cues=0;
        foreach(var c in fx.GetProperty("intro").GetProperty("cues").EnumerateArray())
        {
            string name=c.GetProperty("cue").GetString();int beat=c.GetProperty("beat").GetInt32();double time=c.GetProperty("time").GetDouble();
            if(beat>=0)Check(Math.Abs(beats[beat]-time)<1e-9,"cue "+name+" is not on its beat");
            if(cueBeat.TryGetValue(name,out int core)){Check(core==beat&&NativeSirenIntroLight.Cue(beats,core)==time,"Core cue "+name);cues++;}
        }
        Check(cues==cueBeat.Count,"every light cue is in SIREN_INTRO");
        // 2. The source's lights frame by frame.
        var lights=fx.GetProperty("lights");double[] bulbColor=Doubles(lights.GetProperty("bulb").GetProperty("color")),fillColor=Doubles(lights.GetProperty("fill").GetProperty("color")),ambientColor=Doubles(lights.GetProperty("ambient").GetProperty("color"));
        for(int k=0;k<3;k++)
        {
            int shift=16-8*k;Near(NativeSirenIntroLight.Linear(NativeSirenIntroLight.BulbHex,shift),bulbColor[k],"bulb colour");Near(NativeSirenIntroLight.Linear(NativeSirenIntroLight.FillHex,shift),fillColor[k],"fill colour");
            Near(NativeSirenIntroLight.Linear(NativeSirenIntroLight.AmbientHex,shift),ambientColor[k],"ambient colour");Near(NativeSirenIntroLight.Linear(NativeSirenIntroLight.GlowHex,shift),Doubles(lights.GetProperty("glow").GetProperty("color"))[k],"glow colour");
            Near(NativeSirenIntroLight.Linear(NativeSirenIntroLight.HaloHex,shift),Doubles(lights.GetProperty("halo").GetProperty("color"))[k],"halo colour");
        }
        Check(lights.GetProperty("bulb").GetProperty("distance").GetDouble()==NativeSirenIntroLight.BulbDistance&&lights.GetProperty("bulb").GetProperty("decay").GetDouble()==NativeSirenIntroLight.BulbDecay,"bulb range and decay");
        Check(lights.GetProperty("fill").GetProperty("distance").GetDouble()==NativeSirenIntroLight.FillDistance&&lights.GetProperty("fill").GetProperty("decay").GetDouble()==NativeSirenIntroLight.FillDecay,"fill range and decay");
        Check(lights.GetProperty("halo").GetProperty("scale").GetDouble()==NativeSirenIntroLight.HaloScale&&lights.GetProperty("halo").GetProperty("additive").GetBoolean(),"halo sprite");
        var into=new NativeStageLight[NativeStageLighting.Max];int frames=0,lit=0,dark=0,stutters=0;double lastLevel=-1;
        foreach(var f in fx.GetProperty("frames").EnumerateArray())
        {
            double age=f.GetProperty("age").GetDouble(),level=NativeSirenIntroLight.Level(age,beats);string at=" at "+age.ToString("R");
            Near(level*NativeSirenIntroLight.BulbIntensity,f.GetProperty("bulb").GetDouble(),"bulb intensity"+at);Near(level*NativeSirenIntroLight.FillIntensity,f.GetProperty("fill").GetDouble(),"fill intensity"+at);
            Near(NativeSirenIntroLight.Ambient(level),f.GetProperty("ambient").GetDouble(),"ambient intensity"+at);Near(NativeSirenIntroLight.Glow(level),f.GetProperty("glow").GetDouble(),"bulb glow"+at);Near(NativeSirenIntroLight.Halo(level),f.GetProperty("halo").GetDouble(),"halo opacity"+at);
            double[] bulbPos=Doubles(f.GetProperty("bulbPos")),fillPos=Doubles(f.GetProperty("fillPos"));
            Near(NativeSirenIntroLight.BulbX(age),bulbPos[0],"bulb sway"+at);Near(NativeSirenIntroLight.BulbX(age),f.GetProperty("meshX").GetDouble(),"glow sway"+at);Near(NativeSirenIntroLight.BulbX(age),f.GetProperty("haloX").GetDouble(),"halo sway"+at);
            Check(bulbPos[1]==NativeSirenIntroLight.BulbY&&bulbPos[2]==NativeSirenIntroLight.BulbZ&&fillPos[0]==NativeSirenIntroLight.FillX&&fillPos[1]==NativeSirenIntroLight.FillY&&fillPos[2]==NativeSirenIntroLight.FillZ,"light positions"+at);
            int n=NativeSirenIntroLight.Lights(age,beats,into,out double l2);Check(l2==level,"Lights level"+at);
            if(level>0)
            {
                Check(n==2,"two cell lights while lit"+at);var b=into[0];var fl=into[1];
                Near(b.X,bulbPos[0],"bulb x");Check(b.Y==bulbPos[1]&&b.Z==bulbPos[2]&&b.Distance==NativeSirenIntroLight.BulbDistance&&b.Decay==NativeSirenIntroLight.BulbDecay&&fl.Distance==NativeSirenIntroLight.FillDistance&&fl.Decay==NativeSirenIntroLight.FillDecay,"cell light ranges"+at);
                Near(b.R,bulbColor[0]*f.GetProperty("bulb").GetDouble(),"bulb red"+at,1e-6);Near(b.B,bulbColor[2]*f.GetProperty("bulb").GetDouble(),"bulb blue"+at,1e-6);Near(fl.G,fillColor[1]*f.GetProperty("fill").GetDouble(),"fill green"+at,1e-6);
            }
            else Check(n==0,"no cell light while dark"+at);
            if(level>.3)lit++;else dark++;
            if(age>=beats[NativeSirenIntroLight.FlickerBeat]&&age<beats[NativeSirenIntroLight.RevealBeat]&&level==.04&&lastLevel>.3)stutters++;lastLevel=level;frames++;
        }
        Check(frames>1000&&lit>300&&dark>300,"fixture covers lit and dark frames");
        // 3. The flash timeline on the music: from beat 3 to beat 9 the bulb goes dark around every half beat (phase .42-.62)
        // and again early in each odd beat (.15-.24), the glimpse flashes 1.15 for .11 s at beat 6 and then holds .21 s dark.
        int windows=0;
        for(int b=NativeSirenIntroLight.FlickerBeat;b<NativeSirenIntroLight.RevealBeat;b++)
        {
            double t0=beats[b],span=beats[b+1]-t0,glimpse=beats[NativeSirenIntroLight.GlimpseBeat];
            bool Glimpse(double t)=>t>=glimpse&&t<glimpse+.32;
            foreach(var (from,to) in b%2==1?new[]{(.42,.62),(.15,.24)}:new[]{(.42,.62)})
            {
                double mid=t0+(from+to)/2*span,before=t0+(from-.02)*span,after=t0+(to+.02)*span;if(Glimpse(mid)||Glimpse(before)||Glimpse(after))continue;
                Check(NativeSirenIntroLight.Level(mid,beats)==.04,"beat "+b+" stutter "+from+"-"+to+" is not dark");
                Check(NativeSirenIntroLight.Level(before,beats)>.5&&NativeSirenIntroLight.Level(after,beats)>.5,"beat "+b+" is not lit around its stutter "+from+"-"+to);windows++;
            }
        }
        double g=beats[NativeSirenIntroLight.GlimpseBeat];
        Check(NativeSirenIntroLight.Level(g+.05,beats)==1.15&&NativeSirenIntroLight.Level(g+.2,beats)==0&&NativeSirenIntroLight.Level(beats[NativeSirenIntroLight.BulbBeat]-1e-6,beats)==0&&NativeSirenIntroLight.Level(beats[NativeSirenIntroLight.BulbBeat]+.2,beats)>.4,"glimpse flash and bulb cue");
        Check(NativeSirenIntroLight.Level(beats[NativeSirenIntroLight.RevealBeat]+.2,beats)>.7&&NativeSirenIntroLight.Level(beats[NativeSirenIntroLight.BlackoutBeat]-1e-6,beats)>.7&&NativeSirenIntroLight.Level(beats[NativeSirenIntroLight.BlackoutBeat],beats)==0,"reveal and blackout on their beats");
        Check(windows>=7&&stutters>=windows,"stutter windows found in the fixture ("+windows+" / "+stutters+")");
        yield return "Siren intro light: "+frames+" source frames (bulb, fill, ambient, glow, halo, sway) match NativeSirenIntroLight; "+cues+" cues and 18 beats equal the boss song's beat table; "+windows+" stutters land on their beats";
        // 4. The scene bank's cell: walls, floor and ceiling on the static track, the bulb glow on its own track, and the bank's
        // 30 Hz bulb samples equal the Core light at their frame times (the bake and the runtime agree).
        using var models=Decode(root,"SourceModels");JsonElement stage=default;bool found=false;
        foreach(var m in models.RootElement.GetProperty("models").EnumerateArray())if(m.GetProperty("id").GetString()=="stage-dev-siren"){stage=m;found=true;break;}
        Check(found,"stage-dev-siren model");
        double wallR=NativeSirenIntroLight.Linear(NativeSirenIntroLight.WallHex,16);int back=0,floor=0,ceiling=0,sides=0,wallVerts=0;var opaqueBones=new HashSet<int>();
        foreach(var s in stage.GetProperty("sections").EnumerateArray())
        {
            if(s.GetProperty("transparent").GetBoolean())continue;int bone=s.GetProperty("bone").GetInt32();var p=s.GetProperty("positions");var n=s.GetProperty("normals");var c=s.GetProperty("colors");int count=p.GetArrayLength()/3;if(count>0)opaqueBones.Add(bone);if(bone!=0)continue;
            for(int i=0;i<count;i++)
            {
                // Bank frame: x mirrored. Faces of the cell boxes (siren/intro.ts box()), by position and outward normal.
                double x=-p[i*3].GetDouble(),y=p[i*3+1].GetDouble(),z=p[i*3+2].GetDouble(),nx=-n[i*3].GetDouble(),ny=n[i*3+1].GetDouble(),nz=n[i*3+2].GetDouble();bool concrete=Math.Abs(c[i*4].GetDouble()-wallR)<1e-3;
                if(concrete)wallVerts++;
                if(Math.Abs(z+2.1)<1e-3&&nz>.99&&concrete)back++;else if(Math.Abs(y-.005)<1e-3&&ny>.99)floor++;else if(Math.Abs(y-3.0)<1e-3&&ny<-.99)ceiling++;else if((Math.Abs(x+2.1)<1e-3&&nx>.99||Math.Abs(x-2.5)<1e-3&&nx<-.99)&&concrete)sides++;
            }
        }
        Check(back>=4&&floor>=4&&ceiling>=4&&sides>=8&&wallVerts>=48,"the cell's walls, floor and ceiling are in the bank ("+back+"/"+floor+"/"+ceiling+"/"+sides+")");
        using var scenes=Decode(root,"SceneMotion");JsonElement clip=default;found=false;
        foreach(var c in scenes.RootElement.GetProperty("clips").EnumerateArray())if(c.GetProperty("key").GetString()=="stage-dev-siren"){clip=c;found=true;break;}
        Check(found,"stage-dev-siren clip");int clipFrames=clip.GetProperty("frames").GetInt32(),tracks=clip.GetProperty("tracks").GetInt32();double clipDuration=clip.GetProperty("duration").GetDouble();
        var values=new float[clip.GetProperty("values").GetArrayLength()];{int i=0;foreach(var v in clip.GetProperty("values").EnumerateArray())values[i++]=v.GetSingle();}
        int bulbTrack=-1,bulbTracks=0;for(int t=0;t<tracks;t++)if(NativeSirenIntroLight.AtBulb(values,clipFrames,t)&&opaqueBones.Contains(t)){bulbTrack=t;bulbTracks++;}
        Check(bulbTracks==1,"one bulb glow track in the bank ("+bulbTracks+")");
        int samples=0;bool bulbFound=false;
        foreach(var l in clip.GetProperty("lights").EnumerateArray())
        {
            var v=l.GetProperty("values");if(l.GetProperty("kind").GetString()!="point"||Math.Abs(v[0].GetDouble()-.62)>1e-3||Math.Abs(v[1].GetDouble()-2.02)>1e-3)continue;bulbFound=true;
            Check(Math.Abs(v[7].GetDouble()-NativeSirenIntroLight.BulbDistance)<1e-6,"bank bulb range");
            for(int f=0;f<clipFrames;f++){double t=f*clipDuration/(clipFrames-1);Near(v[f*11+6].GetDouble(),NativeSirenIntroLight.BulbIntensity*NativeSirenIntroLight.Level(t,beats),"bank bulb at frame "+f,2e-5);samples++;}
        }
        Check(bulbFound&&samples==clipFrames,"the bank's bulb light track");
        yield return "Siren cell in the bank: back wall, floor, ceiling and side walls on the static track ("+wallVerts+" concrete vertices), bulb glow on track "+bulbTrack+", "+samples+" baked bulb samples equal the Core light";
        // 5. The stage block lights the cell: three's Lambert falloff at the back wall, floor and both side walls, through the
        // tone curve; lit beats show the room, stutters and dark beats leave it black. No Unity light or pixel-light limit is involved.
        var points=new (double x,double y,double z,double nx,double ny,double nz,string name)[]{(0,1.6,-2.1,0,0,1,"back wall"),(.3,.005,-.6,0,1,0,"floor"),(-2.1,1.6,-1,1,0,0,"left wall"),(2.5,1.6,-1,-1,0,0,"right wall")};
        double litMin=1,darkMax=0;int reached=0;
        foreach(double age in new[]{.9,1.75,2.2,2.7,3.6,4.4,5.3,1.5,1.64,2.33,3.3,.3,2.85,5.8})
        {
            int n=NativeSirenIntroLight.Lights(age,beats,into,out double level);
            foreach(var q in points)
            {
                NativeStageLighting.Diffuse(into,n,q.x,q.y,q.z,q.nx,q.ny,q.nz,out double r,out _,out _);double radiance=wallR*r;
                double three=n==0?0:ThreeLambert(wallR,NativeSirenIntroLight.Linear(NativeSirenIntroLight.BulbHex,16),level*NativeSirenIntroLight.BulbIntensity,NativeSirenIntroLight.BulbDistance,NativeSirenIntroLight.BulbDecay,q.x,q.y,q.z,q.nx,q.ny,q.nz,NativeSirenIntroLight.BulbX(age),NativeSirenIntroLight.BulbY,NativeSirenIntroLight.BulbZ)
                    +ThreeLambert(wallR,NativeSirenIntroLight.Linear(NativeSirenIntroLight.FillHex,16),level*NativeSirenIntroLight.FillIntensity,NativeSirenIntroLight.FillDistance,NativeSirenIntroLight.FillDecay,q.x,q.y,q.z,q.nx,q.ny,q.nz,NativeSirenIntroLight.FillX,NativeSirenIntroLight.FillY,NativeSirenIntroLight.FillZ);
                Near(radiance,three,q.name+" radiance at "+age,1e-9);double shown=Encode(NativeStageLighting.Aces(radiance));
                if(level>.3){litMin=Math.Min(litMin,shown);Check(shown>.12,q.name+" not visibly lit at "+age+" ("+shown.ToString("0.000")+")");reached++;}
                else if(level<.1){darkMax=Math.Max(darkMax,shown);Check(shown<.03,q.name+" not dark at "+age+" ("+shown.ToString("0.000")+")");}
            }
        }
        yield return "Siren cell lit by the stage block: "+reached+" lit wall/floor samples equal three's Lambert falloff and show at least "+(litMin*255).ToString("0")+"/255 after ACES; dark beats stay under "+(darkMax*255).ToString("0.0")+"/255";
        // 6. Every entrance stage's point lights fit the block at every frame (Add keeps them all), and Add keeps the brightest.
        int worst=0;string worstAt="";
        foreach(var c in scenes.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=c.GetProperty("key").GetString();if(!key.StartsWith("stage-"))continue;int fs=c.GetProperty("frames").GetInt32();var ls=new List<JsonElement>();foreach(var l in c.GetProperty("lights").EnumerateArray()){string kind=l.GetProperty("kind").GetString();if(kind=="point")ls.Add(l.GetProperty("values"));}
            for(int f=0;f<fs;f++)
            {
                int kept=0,active=0;
                foreach(var v in ls){double I=v[f*11+6].GetDouble();if(!(I>0))continue;active++;kept=NativeStageLighting.Add(into,kept,new NativeStageLight(v[f*11].GetDouble(),v[f*11+1].GetDouble(),v[f*11+2].GetDouble(),v[f*11+3].GetDouble()*I,v[f*11+4].GetDouble()*I,v[f*11+5].GetDouble()*I,v[f*11+7].GetDouble(),2));}
                Check(kept==active,key+" drops a point light at frame "+f);if(active>worst){worst=active;worstAt=key;}
            }
        }
        Check(worst<=NativeStageLighting.Max,"an entrance needs more than "+NativeStageLighting.Max+" stage lights");
        var many=new NativeStageLight[NativeStageLighting.Max];int held=0;for(int i=0;i<12;i++)held=NativeStageLighting.Add(many,held,new NativeStageLight(0,1,1,i+1,0,0,0,2));
        Check(held==NativeStageLighting.Max,"Add holds the block's size");for(int i=0;i<held;i++)Check(many[i].R==12-i,"Add keeps the brightest first");
        Check(NativeStageLighting.Add(many,held,new NativeStageLight(0,1,1,0,0,0,0,2))==held,"a black light is skipped");
        yield return "entrance stage lights: every point light of the 20 entrance stages fits the stage block at every frame (at most "+worst+", "+worstAt+"); Add keeps the "+NativeStageLighting.Max+" brightest";
        // 7. Music: the Siren entrance plays black-siren-protocol from its first frame (no sting); every other entrance its sting.
        string song=NativeIntroRules.IntroSong("dev-siren");Check(song=="music/black-siren-protocol"&&NativeIntroRules.IntroSting("dev-siren")==null,"Siren intro music route");
        Check(track.GetProperty("file").GetString()==song+".mp3"&&File.Exists(Path.Combine(root,"Assets/Conversion/Resources/Audio/"+song+".mp3")),"boss song resource");
        int stings=0;foreach(var s in NativeIntroRules.Entrances){if(s=="dev-siren")continue;string sting=NativeIntroRules.IntroSting(s);Check(sting=="audio/intros/"+s&&NativeIntroRules.IntroSong(s)==null,s+" music route");if(File.Exists(Path.Combine(root,"Assets/Conversion/Resources/Audio/"+sting+".mp3")))stings++;}
        Check(NativeIntroRules.IntroSting(null)==null&&NativeIntroRules.IntroSting("weapon-siren")==null&&NativeIntroRules.IntroSong(null)==null,"unknown styles route nothing");
        yield return "intro music: dev-siren plays "+song+" from its first frame (bulb beats on the song's beats); "+stings+" of 19 other entrances have their sting";
        // 8. The source shaders carry the stage block the Core describes: Max-sized arrays, the falloff, the tone curve and the gate.
        foreach(var name in new[]{"NativeSourceLit","NativeSourceTransparent"})
        {
            string text=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/"+name+".shader"));
            foreach(var part in new[]{"finalcolor:StageTone","float4 _StageLightPos["+NativeStageLighting.Max+"]","float4 _StageLightColor["+NativeStageLighting.Max+"]","for(int k=0;k<"+NativeStageLighting.Max+";k++)","max(pow(len,_StageLightColor[k].w),.01)","q*=q;q*=q;float cut=saturate(1-q)",".31830988618","c*=.92/0.6","0.0245786","0.000090537","0.983729","0.4329510","0.238081","#ifndef UNITY_PASS_FORWARDADD","_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb)"})
                Check(text.Contains(part),name+" lacks "+part);
        }
        Check(NativeStageLighting.Exposure==.92&&NativeStageLighting.Aces(0)==0&&NativeStageLighting.Aces(100)<=1&&NativeStageLighting.Aces(.05)<.05&&NativeStageLighting.Aces(.3)>.3,"tone curve");
        yield return "stage shaders: NativeSourceLit and NativeSourceTransparent carry the "+NativeStageLighting.Max+"-light stage block, three's falloff and the ACES stage tone, gated to entrance draws";
        // 9. The clock the live entrance is drawn at (NativeIntroClock). A guest's replicated age steps at 20 Hz behind a jittery
        // link while its boss song plays locally; drawn on the clock, every stutter shows at 60 Hz and above, starts within a
        // frame of the heard beat, and the age never runs backwards; drawn on the raw age it is up to 80 ms off the song.
        double worstError=0;int checkedWindows=0;
        foreach(double fps in new[]{30.0,60,144,0})
        {
            var run=GuestRun(beats,fps,true,true);string at=fps>0?" at "+fps+" fps":" at uneven frame times";
            Check(run.Monotonic,"intro clock runs backwards"+at);Check(run.MaxError<=.025,"intro clock is "+run.MaxError.ToString("F4")+" s off the heard song"+at);
            Check(run.Skipped==0,run.Skipped+" stutters skipped"+at);Check(run.MaxOnset<=run.Interval+.03,"a stutter starts "+run.MaxOnset.ToString("F4")+" s off its heard beat"+at);
            worstError=Math.Max(worstError,run.MaxError);checkedWindows+=run.Windows;
        }
        var raw=GuestRun(beats,144,true,false);Check(raw.MaxError>.04&&raw.Skipped>0,"the stepped replicated age is "+raw.MaxError.ToString("F4")+" s off the song and skips "+raw.Skipped+" stutters (the harness no longer shows the 20 Hz steps)");
        var silent=GuestRun(beats,144,false,true);Check(silent.Monotonic&&silent.Smooth&&silent.MaxLag<=.06,"without the song the clock follows the replicated age smoothly ("+silent.MaxLag.ToString("F4")+" s)");
        Check(NativeIntroClock.Target(1,1.2)==1.2&&NativeIntroClock.Target(1,-1)==1&&NativeIntroClock.Target(.1,9.2)==.1&&NativeIntroClock.Target(2,2+NativeIntroClock.SongWindow)==2,"intro clock target");
        var jumps=new NativeIntroClock();jumps.Sample(8.9,.016);double next=jumps.Sample(0,.016);double resync=jumps.Sample(1.13,.016);jumps.Reset();
        Check(next==0&&resync==1.13&&jumps.Sample(.5,.016)==.5&&jumps.Sample(double.NaN,.016)==.5,"intro clock jumps to a new entrance and a song resync at once");
        yield return "intro clock: on a 20 Hz guest the Siren entrance stays within "+(worstError*1000).ToString("F1")+" ms of the heard song at 30/60/144 fps and uneven frames ("+checkedWindows+" stutter windows, none skipped from 60 fps); the raw age was "+(raw.MaxError*1000).ToString("F0")+" ms off and skipped "+raw.Skipped+" of "+raw.Windows+" at 144 fps";
    }
    private struct GuestResult{public bool Monotonic,Smooth;public double MaxError,MaxOnset,MaxLag,Interval;public int Skipped,Windows;}
    // A guest watching the dev-siren entrance: the host sends its intro age every 50 ms; frames arrive in order 80-110 ms
    // later; the boss song starts when the first one lands (at its age, as NativeAudio.Track cues it) and plays on the audio
    // clock, its position read in 1024-sample mix blocks at 48 kHz (AudioSource.time). Renders at `fps` (0: uneven frame
    // times), drawing at NativeIntroClock (`clock`) or at the raw replicated age; `song` false is a guest with no audio.
    private static GuestResult GuestRun(double[] beats,double fps,bool song,bool clock)
    {
        const double latency=.08,block=1024/48000.0;var arrive=new double[200];uint seed=12345;
        for(int k=0;k<arrive.Length;k++){seed=seed*1664525+1013904223;arrive[k]=Math.Max(k>0?arrive[k-1]:0,k*.05+latency+(seed>>8)/16777216.0*.03);}
        double[] uneven={.011,.019,.014,.024,.009,.016};
        var r=new GuestResult{Monotonic=true,Smooth=true,Interval=fps>0?1/fps:.024};var intro=new NativeIntroClock();
        double start=arrive[0],now=start,last=-1;int frame=0,latest=0;
        // The heard stutter windows (song time), each with whether a drawn frame was dark in it and its first dark frame's local time.
        var windows=new List<(double from,double to,bool seen,double onset)>();bool dark=false;double opened=0;
        for(double t=beats[NativeSirenIntroLight.BulbBeat];t<beats[NativeSirenIntroLight.RevealBeat]+.1;t+=.0002)
        {bool d=NativeSirenIntroLight.Level(t,beats)<.1;if(d&&!dark)opened=t;if(!d&&dark)windows.Add((opened,t,false,double.NaN));dark=d;}
        while(now-start<NativeSirenIntroLight.Cue(beats,NativeSirenIntroLight.RevealBeat)+.2)
        {
            while(latest+1<arrive.Length&&arrive[latest+1]<=now)latest++;
            double age=latest*.05,heard=now-start,dt=frame==0?0:fps>0?1/fps:uneven[frame%uneven.Length];
            double playback=song?Math.Floor(heard/block)*block:-1,drawn=clock?intro.Sample(NativeIntroClock.Target(age,playback),dt):age;
            if(frame>0&&drawn<last)r.Monotonic=false;
            if(frame>1&&clock&&(drawn-last<.3*dt||drawn-last>1.7*dt+1e-9))r.Smooth=false;
            if(heard>.25){r.MaxError=Math.Max(r.MaxError,Math.Abs(drawn-heard));r.MaxLag=Math.Max(r.MaxLag,Math.Abs(drawn-age));}
            if(NativeSirenIntroLight.Level(drawn,beats)<.1)for(int i=0;i<windows.Count;i++)
            {
                var w=windows[i];if(drawn<w.from||drawn>=w.to)continue;
                if(!w.seen){w.seen=true;w.onset=heard-w.from;}windows[i]=w;
            }
            last=drawn;frame++;now+=fps>0?1/fps:uneven[frame%uneven.Length];
        }
        foreach(var w in windows)
        {
            if(w.to-w.from<1.25*r.Interval)continue;r.Windows++;if(!w.seen){r.Skipped++;continue;}r.MaxOnset=Math.Max(r.MaxOnset,Math.Abs(w.onset));
        }
        return r;
    }
    private static JsonDocument Decode(string root,string name)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/"+name+".json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        return JsonDocument.Parse(reader.ReadToEnd());
    }
}
