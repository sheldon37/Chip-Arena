using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV · Architect of Reality (docs/INTRO-DEV-ARCHITECT.md), its own checks beside the framework's (Tests/IntroV2Checks): the
    /// registry, the song's beats and cues, determinism under a scrub, no allocation per frame, the peaks (effects, set pieces,
    /// lights) on High and Low, the streak rule, Reduced effects (no shake, no fast flashes in the set's glow or the lights), the
    /// body's build, and the stroke font the live name is written in.
    /// </summary>
    internal static class IntroArchitectChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Architect intro: "+what);count++;}
        // High's and Low's peaks (the dev intro's own, asked of the framework; docs/INTRO-DEV-ARCHITECT.md "Budgets").
        public const int FxHigh=NativeIntroSetLimits.DevFx,FxLow=NativeIntroSetLimits.DevFxLow,PiecesHigh=NativeIntroSetLimits.DevPieces,PiecesLow=NativeIntroSetLimits.DevPiecesLow,Lights=8;
        private sealed class Sink:INativeEntranceFx,INativeIntroSet,INativeEntranceProps
        {
            public bool Has_=true,Ready_=true;public int Fx,Pieces,Prims,LongStreaks,OffPlane;public double Hash,GlowSum;public bool Broken;public float WorstAngle;
            public NativeEntranceView View;public bool CheckPlane;
            private void V(Vector3 v){if(!(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)))Broken=true;Hash=Hash*1.0000001+v.X*1.3+v.Y*1.7+v.Z*2.1;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Fx++;V(at);V(color);Hash+=size+profile;if(profile==2)Broken=true;}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)
            {
                Fx++;V(head);V(velocity);V(color);Hash+=width+taper;var axis=velocity*blur;float len=axis.Length();
                if(len>1.0001f&&CheckPlane)
                {
                    LongStreaks++;var f=Vector3.Normalize(View.Look-View.Eye);float sin=Math.Abs(Vector3.Dot(axis,f))/len;float deg=(float)(Math.Asin(Math.Min(1,sin))*180/Math.PI);
                    // (a head behind the eye is dropped by the intro, never drawn off its plane)
                    if(deg>2){OffPlane++;WorstAngle=Math.Max(WorstAngle,deg);}
                }
            }
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Fx++;V(at);}
            public bool Ready=>Ready_;public bool Has(string prop)=>Has_;
            public bool RingProp;
            public void Piece(string prop,Vector3 at,Quaternion rotation,float scale,Vector3 tint,Vector3 glow){Pieces++;V(at);V(tint);V(glow);Hash+=scale+prop.Length;GlowSum+=glow.X+glow.Y+glow.Z;if(!(scale>=0)||!(scale<=1.0001f))Broken=true;if(prop.StartsWith("crown-arc",StringComparison.Ordinal)||prop.StartsWith("tile-",StringComparison.Ordinal))RingProp=true;}
            public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Prims++;V(at);V(color);}
            public int Copies;public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> rotation,float scale,Vector3 tint){Copies+=at.Length;foreach(var p in at)V(p);}
            public bool Model(string id,Vector3 position,Quaternion rotation,float scale,Vector3 tint)=>false;
            public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){Prims++;V(position);}
            public void Reset(){Fx=Pieces=Prims=Copies=0;Hash=0;GlowSum=0;}
        }
        private static NativeIntroArchitect Make(bool reduced,bool low,string name)
        {
            var s=new NativeIntroArchitect();s.Prepare();s.Context=new NativeIntroContext{Reduced=reduced,Low=low,Quality=low?.45f:1,Name=name};return s;
        }
        /// <summary>One frame through every per-frame call; returns its hash.</summary>
        private static double Frame(NativeIntroArchitect s,float t,Sink sink,CapturedRig.Pose pose,NativeEntranceLights lights)
        {
            sink.Reset();s.Pose(t,pose,out var root,out var rot);s.Camera(t,out var view);sink.View=view;
            double h=root.X+root.Y*3+root.Z*5+rot.W+view.Eye.X+view.Eye.Y*3+view.Eye.Z*7+view.Fov;for(int b=0;b<22;b++)h+=pose.Rotation[b].X*(b+1)+pose.Rotation[b].Y*(b+2);
            s.Lights(t,lights);h+=lights.Points*11+lights.FogFar;for(int i=0;i<lights.Points;i++)h+=lights.Point[i].R+lights.Point[i].X;
            s.Props(t,sink,s.Context.Quality);s.Set(t,sink);s.Effects(t,sink,s.Context.Quality);
            var card=default(NativeIntroCardState);s.Card(t,ref card);s.Lens(t,out var lens);h+=card.Age+card.EffectA+card.EffectB+lens.Black+lens.Letterbox;
            for(int b=0;b<22;b++)h+=s.Solid(t,b);
            return h+sink.Hash;
        }
        // The song is heard to the end (integrator, 2026-11-20): the first trim faded it out in the song's own time, so it fell
        // silent at 10.5 s and the riser, the drop and the name played to nothing. 16-bit PCM WAV: as long as the intro, no half
        // second quieter than -40 dB before the fade, and the drop's half second louder than the half second before the riser.
        private static void Song(string path)
        {
            var b=System.IO.File.ReadAllBytes(path);int at=12,channels=0,rate=0,bits=0,data=-1,size=0;
            while(at+8<=b.Length){string id=System.Text.Encoding.ASCII.GetString(b,at,4);int n=BitConverter.ToInt32(b,at+4);
                if(id=="fmt "){channels=BitConverter.ToInt16(b,at+10);rate=BitConverter.ToInt32(b,at+12);bits=BitConverter.ToInt16(b,at+22);}
                if(id=="data"){data=at+8;size=Math.Min(n,b.Length-data);break;}at+=8+n+(n&1);}
            Check(data>0&&channels>0&&rate>0&&bits==16,"the song is 16-bit PCM WAV");
            int frames=size/(2*channels);double seconds=frames/(double)rate;
            Check(Math.Abs(seconds-NativeIntroArchitect.Total)<.1,"the song lasts the intro ("+seconds.ToString("0.00")+" s)");
            double Db(double from,double to){int f0=(int)(from*rate),f1=Math.Min(frames,(int)(to*rate));double s=0;int n=0;
                for(int f=f0;f<f1;f++)for(int c=0;c<channels;c++){double v=BitConverter.ToInt16(b,data+(f*channels+c)*2)/32768.0;s+=v*v;n++;}
                return n==0?-999:10*Math.Log10(s/n+1e-12);}
            for(double w=0;w+.5<=NativeIntroArchitect.FadeFrom;w+=.5)Check(Db(w,w+.5)>-40,"the song is heard at "+w.ToString("0.0")+" s ("+Db(w,w+.5).ToString("0.0")+" dB)");
            Check(Db(NativeIntroArchitect.Drop,NativeIntroArchitect.Drop+.5)>Db(NativeIntroArchitect.Gather-.5,NativeIntroArchitect.Gather)+3,"the drop is louder than the bar before the riser");
        }
        public static void Run(string root)
        {
            count=0;var pose=new CapturedRig.Pose();var lights=new NativeEntranceLights();
            // ---- the registry and the song ----
            var entry=NativeIntroCatalog.Find(NativeIntroCatalog.ArchitectId);
            Check(entry!=null&&entry.Kind==NativeIntroKind.Dev&&entry.Id=="dev-architect","registered as the DEV intro dev-architect");
            Check(entry.Duration==NativeIntroArchitect.Total&&entry.NameAt==NativeIntroArchitect.CardAt&&Math.Abs(NativeIntroRules.Duration("dev-architect")-15.7)<1e-4&&Math.Abs(NativeIntroRules.NameAt("dev-architect")-12.63)<1e-4,"15.7 s, the name at the drop (12.63 s)");
            var a=Make(false,false,"Sheldon");Check(a.Ready&&a.Length==NativeIntroArchitect.Total&&a.IntroNameAt==NativeIntroArchitect.CardAt&&a.Music==NativeIntroArchitect.SongId&&a.SetBank=="ChipArenaIntroDev","ready, its song and its bank");
            Check(System.IO.File.Exists(System.IO.Path.Combine(root,"Assets/ChipArena/Resources/Audio/"+NativeIntroArchitect.SongId+".wav")),"the song ships (Resources/Audio/music/architect-obey.wav)");
            Song(System.IO.Path.Combine(root,"Assets/ChipArena/Resources/Audio/"+NativeIntroArchitect.SongId+".wav"));
            // the beats in the song's order, the stutters inside the riser, the drop on the name, the fade at the end
            float[] beats={NativeIntroArchitect.Strike,NativeIntroArchitect.Break,NativeIntroArchitect.Tear,NativeIntroArchitect.Presence,NativeIntroArchitect.PulseA,NativeIntroArchitect.PulseB,NativeIntroArchitect.Draft,NativeIntroArchitect.Condense,NativeIntroArchitect.Body,NativeIntroArchitect.Still,NativeIntroArchitect.Gather,NativeIntroArchitect.Stutters[0],NativeIntroArchitect.Stutters[4],NativeIntroArchitect.Inhale,NativeIntroArchitect.DipA,NativeIntroArchitect.DipB,NativeIntroArchitect.Drop,NativeIntroArchitect.FadeFrom,NativeIntroArchitect.Total};
            for(int i=1;i<beats.Length;i++)Check(beats[i]>beats[i-1],"the beats in order ("+i+")");
            Check(Math.Abs(NativeIntroArchitect.Drop-NativeIntroArchitect.CardAt)<.02f,"the name lands on the drop");
            // ---- the cues ----
            var cues=a.Cues;Check(cues.Length>=15,"a cue for every beat");
            for(int i=0;i<cues.Length;i++)
            {
                var c=cues[i];Check(c.At>=0&&c.At<=NativeIntroArchitect.Total&&(i==0||c.At>=cues[i-1].At),"cue "+i+" sorted and inside the intro");
                Check(c.Key!=null&&c.Key.StartsWith("intro2.architect.",StringComparison.Ordinal)&&c.Alt!=null&&c.Clip!=null&&c.Gain>0&&c.Gain<=1&&c.Rate>.5f&&c.Rate<2,"cue "+i+" has its key, an existing sound and a clip");
            }
            foreach(float beat in new[]{NativeIntroArchitect.Strike,NativeIntroArchitect.Break,NativeIntroArchitect.Tear,NativeIntroArchitect.Presence,NativeIntroArchitect.Draft,NativeIntroArchitect.Body,NativeIntroArchitect.Still,NativeIntroArchitect.Gather,NativeIntroArchitect.Inhale,NativeIntroArchitect.Drop})
            {bool on=false;foreach(var c in cues)if(Math.Abs(c.At-beat)<.001f)on=true;Check(on,"a cue lands on the beat at "+beat);}
            foreach(float s in NativeIntroArchitect.Stutters){bool on=false;foreach(var c in cues)if(Math.Abs(c.At-s)<.001f)on=true;Check(on,"a cue on the stutter at "+s);}
            // ---- every frame: finite, its peaks, the streak rule ----
            foreach(bool low in new[]{false,true})foreach(bool set in new[]{true,false})
            {
                var s=Make(false,low,"Sheldon_Deans 2026");var sink=new Sink{Ready_=set,CheckPlane=true};int fx=0,pieces=0,lit=0;float fxAt=0;
                for(int f=0;f<=(int)(NativeIntroArchitect.Total*60);f++)
                {
                    float t=f/60f;Frame(s,t,sink,pose,lights);Check(!sink.Broken,"finite at "+t+(low?" (Low)":""));
                    if(sink.Fx>fx){fx=sink.Fx;fxAt=t;}pieces=Math.Max(pieces,sink.Pieces);lit=Math.Max(lit,lights.Points);
                    Check(NativeIntroMotion.Finite(pose),"a finite pose at "+t);
                }
                Check(fx<=(low?FxLow:FxHigh),"effects at peak "+fx+" at "+fxAt+(low?" (Low)":" (High)"));
                Check(pieces<=(low?PiecesLow:PiecesHigh),"set pieces at peak "+pieces+(low?" (Low)":""));
                Check(lit<=Lights,"lights at peak "+lit);
                Check(sink.OffPlane==0,"long streaks on their heads' camera planes ("+sink.OffPlane+" off, worst "+sink.WorstAngle+" degrees)");
                Check(sink.LongStreaks>0,"the long lines are checked");
                if(!set)Check(sink.Prims>0,"without the bank the void is drawn");
                Console.WriteLine("Architect intro peaks"+(low?" (Low)":" (High)")+(set?"":" without the bank")+": "+fx+" effects at "+fxAt.ToString("0.00")+" s, "+pieces+" set pieces, "+lit+" lights, "+sink.LongStreaks+" long streaks");
            }
            // ---- determinism under a scrub: two fresh scripts, the frames in a scrambled order ----
            {
                var s1=Make(false,false,"Architect");var s2=Make(false,false,"Architect");var k1=new Sink();var k2=new Sink();var times=new List<float>();for(int f=0;f<=157;f++)times.Add(f*.1f);
                var h1=new Dictionary<float,double>();foreach(float t in times)h1[t]=Frame(s1,t,k1,pose,lights);
                var rng=new Random(7);for(int i=times.Count-1;i>0;i--){int j=rng.Next(i+1);(times[i],times[j])=(times[j],times[i]);}
                foreach(float t in times)Check(Math.Abs(Frame(s2,t,k2,pose,lights)-h1[t])<1e-6*Math.Max(1,Math.Abs(h1[t])),"the same frame at "+t+" in any order");
            }
            // ---- no allocation per frame once warm ----
            {
                var s=Make(false,false,"Sheldon");var sink=new Sink{CheckPlane=false};for(int f=0;f<=942;f+=3)Frame(s,f/60f,sink,pose,lights);
                long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<=942;f+=3)Frame(s,f/60f,sink,pose,lights);long after=GC.GetAllocatedBytesForCurrentThread();
                Check(after-before==0,"no allocation per frame ("+(after-before)+" bytes)");
            }
            // ---- Reduced effects: no shake; the lights and the set's glow never flash faster than 3 Hz ----
            {
                var r1=Make(true,false,"Pilot");var r2=Make(true,false,"Pilot");var sink=new Sink();float lastPeak=-9;int fast=0;float prev=0,prev2=0;
                for(int f=0;f<=942;f++)
                {
                    float t=f/60f;r1.Camera(t,out var v1);r2.Camera(t,out var v2);Check(v1.Eye==v2.Eye&&v1.Look==v2.Look,"Reduced: the same camera twice at "+t);
                    r1.Lights(t,lights);float level=0;for(int i=0;i<lights.Points;i++)level+=(float)(lights.Point[i].R+lights.Point[i].G+lights.Point[i].B);sink.Reset();r1.Set(t,sink);level+=(float)sink.GlowSum*.02f;
                    // a peak: the level rises at least a third over the trough before it and falls after; peaks closer than 1/3 s count as fast
                    if(f>=2&&prev>prev2&&prev>=level&&prev>.2f){float trough=prev2;if(prev-trough>.33f*prev){if(t-lastPeak<1/3f)fast++;lastPeak=t;}}
                    prev2=prev;prev=level;
                }
                Check(fast==0,"Reduced: no flash faster than 3 Hz ("+fast+")");
            }
            // ---- the body builds from the core out and is whole from the ignition; it hovers over the dais from the drop (never lands) ----
            {
                var s=Make(false,false,"Pilot");for(int b=0;b<22;b++){Check(s.Solid(NativeIntroArchitect.Condense-.01f,b)==0,"no body before it condenses");Check(s.Solid(NativeIntroArchitect.Body+.03f,b)==1,"the whole body from the ignition");}
                Check(s.Solid(NativeIntroArchitect.Condense+.08f,CapturedRig.Chest)>s.Solid(NativeIntroArchitect.Condense+.08f,CapturedRig.LHand),"the chest before the hands");
                s.Pose(NativeIntroArchitect.Still,pose,out var r0,out _);Check(r0.Y>1.2f,"floating through the stillness");
                for(float t=NativeIntroArchitect.Drop+.5f;t<=NativeIntroArchitect.Total;t+=.1f){s.Pose(t,pose,out var r1,out _);Check(Math.Abs(r1.Y-(.88f+NativeIntroArchitect.Reign))<.06f,"hovering over the dais at "+t.ToString("0.0")+" ("+r1.Y.ToString("0.00")+")");}
                // the head's frame agrees with the body's facing; the visor sits up the head's axis from the skull-base anchor, in front
                foreach(float t in new[]{NativeIntroArchitect.Body+.5f,NativeIntroArchitect.Inhale+.3f,NativeIntroArchitect.Command+1f})
                {
                    s.HeadFrame(t,out var hu,out var hf);var fwd=s.Anchor(NativeEntranceAnchor.Forward,t);fwd=Vector3.Normalize(new Vector3(fwd.X,0,fwd.Z));var head=s.Anchor(NativeEntranceAnchor.Head,t);var visor=s.VisorAt(t);
                    Check(Vector3.Dot(hf,fwd)>.5f&&hu.Y>.6f,"the head's frame faces the body's way at "+t);Check(visor.Y-head.Y>.1f&&Vector3.Dot(visor-head,fwd)>.05f,"the visor above and in front of the head anchor at "+t);
                }
                // the inhale's close-up: frontal on the visor, and the lens outside the body and the raised arms (nothing passes through it)
                for(float t=NativeIntroArchitect.Inhale+.02f;t<NativeIntroArchitect.Drop;t+=.05f)
                {
                    s.Camera(t,out var v);var visor=s.VisorAt(t);s.HeadFrame(t,out _,out var hf);var toEye=v.Eye-visor;
                    Check(Vector3.Dot(Vector3.Normalize(toEye),hf)>.6f&&toEye.Length()>.9f&&v.Eye.Y<visor.Y,"the close-up is low and frontal on the visor at "+t.ToString("0.00"));
                    foreach(var part in new[]{NativeEntranceAnchor.Head,NativeEntranceAnchor.Chest,NativeEntranceAnchor.LeftHand,NativeEntranceAnchor.RightHand,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.RightShoulder})
                        Check(Vector3.Distance(v.Eye,s.Anchor(part,t))>.55f,"the close-up's lens clear of the "+part+" at "+t.ToString("0.00"));
                }
                // the hero frame: low, looking up; the architect about half the frame tall; the sky's name above its head
                foreach(float t in new[]{NativeIntroArchitect.Drop+1.2f,NativeIntroArchitect.Total})
                {
                    s.Camera(t,out var v);float Ny(Vector3 p){var f=Vector3.Normalize(v.Look-v.Eye);var r=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));var u=Vector3.Cross(r,f);var d=p-v.Eye;return Vector3.Dot(d,u)/Vector3.Dot(d,f)/(float)Math.Tan(v.Fov*Math.PI/360);}
                    var feet=(s.Anchor(NativeEntranceAnchor.LeftFoot,t)+s.Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;var top=s.VisorAt(t)+new Vector3(0,.16f,0);var name=NativeIntroArchitect.NameCentre-new Vector3(0,NativeIntroArchitect.LetterHeight*.5f,0);
                    Check(v.Eye.Y<.5f&&v.Look.Y>v.Eye.Y+1,"the hero frame is low, looking up, at "+t);float tall=(Ny(top)-Ny(feet))*.5f;
                    Check(tall>.36f&&tall<.62f,"the architect about half the hero frame tall at "+t+" ("+tall.ToString("0.00")+")");Check(Ny(feet)>-.98f&&Ny(name)>Ny(top)+.05f&&Ny(NativeIntroArchitect.NameCentre)<.92f,"the sky's name above the head and in frame at "+t+" (feet "+Ny(feet).ToString("0.00")+", head "+Ny(top).ToString("0.00")+", name's foot "+Ny(name).ToString("0.00")+", its middle "+Ny(NativeIntroArchitect.NameCentre).ToString("0.00")+")");
                }
                // the card is the sky name's caption: small, under the architect, its kicker the title
                var card=default(NativeIntroCardState);s.Card(14.5f,ref card);Check(card.Shown&&card.Size>0&&card.Size<=.05f&&card.Y>.85f&&card.Kicker=="THE ARCHITECT","the card is the sky name's small caption");
            }
            // ---- no ring shapes: the old ring crown and ring tiles are gone from the set (rays only) ----
            {
                var s=Make(false,false,"Pilot");var sink=new Sink();for(int f=0;f<=942;f+=2){sink.Reset();s.Set(f/60f,sink);}Check(!sink.RingProp,"no ring-shaped prop in the set (crown arcs, ring tiles)");
            }
            // ---- the stroke font: every letter, digit and the common marks, inside its cell ----
            {
                var segs=new List<float>();
                for(char c='A';c<='Z';c++)Check(NativeArchitectFont.Has(c)&&NativeArchitectFont.Has(char.ToLowerInvariant(c)),"the font writes "+c);
                for(char c='0';c<='9';c++)Check(NativeArchitectFont.Has(c),"the font writes "+c);
                foreach(char c in "-_.!?#'+*/:()[]=<>&@$%")Check(NativeArchitectFont.Has(c),"the font writes "+c);
                Check(NativeArchitectFont.Has('é')&&NativeArchitectFont.Has('Ñ')&&!NativeArchitectFont.Has('雪'),"accents fold, other scripts are left to the card");
                foreach(char c in NativeArchitectFont.Characters)
                {
                    segs.Clear();float ink=NativeArchitectFont.Strokes(c,0,segs);Check(ink>0&&segs.Count>0&&segs.Count%6==0,"strokes for "+c);
                    for(int i=0;i<segs.Count;i+=6)Check(segs[i]>=-.5f&&segs[i]<=4.5f&&segs[i+2]>=-.5f&&segs[i+2]<=4.5f&&segs[i+1]>=-1&&segs[i+1]<=7&&segs[i+3]>=-1&&segs[i+3]<=7,"the strokes of "+c+" inside the cell");
                }
                // a name the font can't write is left to the card; a 20-letter name fits the sky
                var s=Make(false,false,"雪雪雪雪");var sink=new Sink();Frame(s,13.5f,sink,pose,lights);int cjk=sink.Fx;s.Context=new NativeIntroContext{Name="WWWWWWWWWWWWWWWWWWWW",Quality=1};Frame(s,15f,sink,pose,lights);Check(sink.Fx>cjk,"a long name is written");
            }
            Console.WriteLine("Architect intro checks: "+count+" passed");
        }
    }
}
