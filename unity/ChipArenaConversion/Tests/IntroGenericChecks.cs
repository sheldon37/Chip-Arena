using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;

// The five generic intros (docs/INTROS-GENERIC.md; owner 2026-11-20: "5 new intros ... great animations and effects, non-block based
// background and designs ... great camera work"): Cloudbreak (the new default), Whiteout, Slipstream, Waygate and Flashbulb. Each
// is registered with its own length and name beat, is deterministic under a scrub, allocates nothing per frame, keeps inside the
// v2 budgets with and without its bank, flashes no faster than 3 Hz in Reduced effects, lays every long streak on its head's camera
// plane, and plays its cues on its beats with sounds that exist. Presentation only.
internal static class IntroGenericChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Generic intros: "+what);count++;}
    /// <summary>The five, as registered (the default is Cloudbreak).</summary>
    public static NativeIntroGenericScript[] Make()=>new NativeIntroGenericScript[]
    {
        new NativeIntroCloudbreak(),new NativeIntroWhiteout(),new NativeIntroSlipstream(),new NativeIntroWaygate(),new NativeIntroFlashbulb(),
    };
    public static void Run(string root)
    {
        // (A review aid, not a check: with INTRO_GENERIC_VIEWS set to a folder, each intro's camera, anchors and set pieces every
        // 0.1 s are written there as JSON for an offline Blender preview of the framing; tools/intros/generic_preview.py.)
        var views=Environment.GetEnvironmentVariable("INTRO_GENERIC_VIEWS");if(!string.IsNullOrEmpty(views))foreach(var s in Make())Dump(s,views);
        count=0;var names=new HashSet<string>();var ids=new HashSet<string>();
        foreach(var s in Make())
        {
            string n=s.Title;Check(names.Add(n)&&ids.Add(s.Style),n+" has its own name and id");
            Registry(s);Timeline(s,root);Determinism(s);Budgets(s);Reduced(s);Streaks(s);
        }
        Check(NativeIntroCatalog.Find(NativeIntroCatalog.DefaultId)?.Make() is NativeIntroCloudbreak,"the new default is Cloudbreak");
        Console.WriteLine("PASS generic intros ("+count+" checks)");
    }

    private sealed class Record:INativeIntroSet
    {
        public bool Ready=>true;public bool Has(string prop)=>true;public readonly System.Text.StringBuilder Out=new System.Text.StringBuilder();public bool First=true;
        private static string F(float x)=>x.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);
        public void Piece(string prop,Vector3 at,Quaternion r,float scale,Vector3 tint,Vector3 glow){Out.Append(First?"":",");First=false;Out.Append("[\""+prop+"\","+F(at.X)+","+F(at.Y)+","+F(at.Z)+","+F(r.X)+","+F(r.Y)+","+F(r.Z)+","+F(r.W)+","+F(scale)+","+F(glow.X+glow.Y+glow.Z)+"]");}
        public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){}
        public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> turns,float scale,Vector3 tint){}
    }
    private static void Dump(NativeIntroGenericScript s,string folder)
    {
        string F(float x)=>x.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);string P(Vector3 v)=>"["+F(v.X)+","+F(v.Y)+","+F(v.Z)+"]";
        var anchors=new[]{NativeEntranceAnchor.Head,NativeEntranceAnchor.Chest,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.LeftHand,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.RightHand,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.LeftFoot,NativeEntranceAnchor.RightKnee,NativeEntranceAnchor.RightFoot};
        var sb=new System.Text.StringBuilder();sb.Append("{\"title\":\""+s.Title+"\",\"frames\":[");
        s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};
        for(int i=0;i<=(int)(s.Length*10);i++)
        {
            float age=i/10f,t=s.Clock(age);s.Camera(age,out var v);var rec=new Record();s.Set(t,rec);
            sb.Append(i>0?",":"").Append("{\"age\":"+F(age)+",\"t\":"+F(t)+",\"eye\":"+P(v.Eye)+",\"look\":"+P(v.Look)+",\"fov\":"+F(v.Fov)+",\"solid\":"+F(s.Solid(t,0))+",\"anchors\":[");
            for(int k=0;k<anchors.Length;k++)sb.Append(k>0?",":"").Append(P(s.Anchor(anchors[k],t)));
            sb.Append("],\"pieces\":["+rec.Out+"]}");
        }
        sb.Append("]}");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,s.Title.ToLowerInvariant()+".json"),sb.ToString());
    }

    // ---- the registry and the timeline ----
    private static void Registry(NativeIntroGenericScript s)
    {
        var e=NativeIntroCatalog.Find(s.Style);string n=s.Title;
        Check(e!=null,n+" is registered");
        Check(e.Name==n,n+"'s Locker name is its title ("+e.Name+")");
        Check(Math.Abs(e.Duration-s.Length)<1e-4f&&Math.Abs(e.NameAt-s.IntroNameAt)<1e-4f,n+": the registry's length and name beat are the script's");
        Check(s.Length>=6&&s.Length<=8.5f,n+" lasts 6-8.5 s ("+s.Length+")");
        Check(s.IntroNameAt>3&&s.IntroNameAt<s.Length-.8f,n+"'s name beat is clear of its end ("+s.IntroNameAt+")");
        Check(s.Ready&&s.SetBank==NativeIntroGenericScript.Bank,n+" is ready and reads the generic bank");
        Check(e.Kind==(s is NativeIntroCloudbreak?NativeIntroKind.Default:NativeIntroKind.Generic),n+" is listed as "+(s is NativeIntroCloudbreak?"the default":"a generic intro"));
        Check(NativeIntroRules.Duration(s.Style)==e.Duration&&NativeIntroRules.NameAt(s.Style)==e.NameAt,n+": the match reads the registry's timing");
    }
    private static void Timeline(NativeIntroGenericScript s,string root)
    {
        string n=s.Title,slug=n.ToLowerInvariant();var cues=s.Cues;float last=-1;
        Check(cues.Length>=6,n+" has a cue timeline");
        string clips=Path.Combine(root,"Assets/Conversion/Resources/Audio/audio");
        var keys=new HashSet<string>();foreach(var d in NativeSoundKeys.All)keys.Add(d.Key);
        foreach(var c in cues)
        {
            Check(c.At>=last&&c.At>=0&&c.At<=s.Length,n+": cue "+c.Key+" is in order inside the intro");last=c.At;
            Check(c.Key!=null&&c.Key.StartsWith("intro2."+slug+"."),n+": cue "+c.Key+" is named for its intro");
            Check(c.Alt==null||keys.Contains(c.Alt),n+": cue "+c.Key+"'s stand-in "+c.Alt+" is an effects-bank key");
            Check(c.Clip!=null&&(File.Exists(Path.Combine(clips,c.Clip+".mp3"))||File.Exists(Path.Combine(clips,c.Clip+".wav"))||File.Exists(Path.Combine(clips,c.Clip+".ogg"))),n+": cue "+c.Key+"'s built-in clip "+c.Clip+" ships");
            Check(c.Gain>0&&c.Gain<=1.2f&&c.Rate>.4f&&c.Rate<2,n+": cue "+c.Key+" has a sane gain and rate");
        }
        Check(Array.Exists(cues,c=>Math.Abs(c.At-s.IntroNameAt)<.02f),n+" has a cue on its name beat");
        // The clock: ramps repay before the name, so the name lands on its age.
        Check(Math.Abs(s.Clock(s.IntroNameAt)-s.IntroNameAt)<.01f,n+": the clock is back on time at the name ("+s.Clock(s.IntroNameAt)+")");
        float prev=-1;for(float a=0;a<=s.Length+.5f;a+=1/120f){float t=s.Clock(a);Check(t>=prev-1e-5f,n+": the clock never runs back");prev=t;}
    }

    // ---- fakes ----
    private sealed class Fx:INativeEntranceFx,INativeEntranceFire
    {
        public int Calls;public double Sum;public float Light;public bool Bad;public readonly List<(Vector3 head,Vector3 axis)> Long=new List<(Vector3,Vector3)>(64);public bool Record;
        public void Reset(){Calls=0;Sum=0;Light=0;Bad=false;Long.Clear();}
        private void Fin(Vector3 v,float a=0){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z)||!float.IsFinite(a))Bad=true;}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Calls++;Fin(at,size);Fin(color);Sum+=at.X*.31+at.Y*.17+at.Z*.13+color.X+size;Light+=(color.X+color.Y+color.Z)*size*size;}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)
        {Calls++;Fin(head,width);Fin(velocity);Sum+=head.X*.23+head.Z*.11+velocity.Y*.07+color.Y+width;var axis=velocity*blur;if(Record&&axis.Length()>1.05f)Long.Add((head,axis));Light+=(color.X+color.Y+color.Z)*width*Math.Max(minLength,axis.Length());}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Calls++;Fin(at,size);Sum+=at.Y*.19+opacity+size;}
        public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){Calls++;Fin(at,size);Sum+=at.X+opacity;}
    }
    private sealed class Set:INativeIntroSet
    {
        public bool Bank;public int Pieces,Primitives;public double Sum;public readonly HashSet<string> Missing=new HashSet<string>();
        public bool Ready=>Bank;
        public bool Has(string prop)=>Bank;
        public void Piece(string prop,Vector3 at,Quaternion rotation,float scale,Vector3 tint,Vector3 glow){Pieces++;Sum+=at.X*.7+at.Y*.3+at.Z*.11+rotation.W+scale+glow.X+glow.Y+tint.Z;if(prop==null||!prop.Contains("/"))Missing.Add(prop??"null");}
        public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Primitives++;Sum+=at.Y+scale.X+color.X;}
        public int Copies;
        public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> turns,float scale,Vector3 tint){Copies+=at.Length;for(int i=0;i<at.Length;i++)Sum+=at[i].X*.01+at[i].Y*.02;}
        public void Reset(){Pieces=0;Primitives=0;Copies=0;Sum=0;}
    }
    private sealed class Props:INativeEntranceProps
    {
        public int Count;
        public bool Model(string id,Vector3 position,Quaternion rotation,float scale,Vector3 tint){Count++;return false;}
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){Count++;}
    }
    private static readonly CapturedRig.Pose pose=new CapturedRig.Pose();
    private static readonly NativeEntranceLights lights=new NativeEntranceLights();
    private static readonly Fx fx=new Fx();private static readonly Set set=new Set();private static readonly Props props=new Props();
    /// <summary>One whole frame at match age `age`; a checksum of everything it drew.</summary>
    private static double Frame(NativeIntroGenericScript s,float age,bool bank,float quality,bool low,bool reduced)
    {
        s.Context=new NativeIntroContext{Reduced=reduced,Low=low,Quality=quality,Name="Pilot"};
        float t=s.Clock(age);double sum=0;
        s.Pose(t,pose,out var root,out var rot);sum+=root.X+root.Y*3+root.Z*5+rot.W;for(int b=0;b<22;b+=3)sum+=pose.Rotation[b].X+pose.Rotation[b].Y;
        s.Camera(age,out var view);sum+=view.Eye.X+view.Eye.Y*2+view.Look.Z+view.Fov;
        s.Lights(t,lights);sum+=lights.Points+lights.Fog.X+lights.FogFar*.01;for(int i=0;i<lights.Points;i++)sum+=lights.Point[i].R+lights.Point[i].X;
        props.Count=0;s.Props(t,props,quality);sum+=props.Count;
        set.Bank=bank;set.Reset();s.Set(t,set);sum+=set.Sum+set.Pieces*7+set.Primitives*3;
        fx.Reset();s.Effects(t,fx,quality);sum+=fx.Sum+fx.Calls;
        var card=new NativeIntroCardState();s.Card(t,ref card);sum+=card.Age+card.Exit+(card.Shown?1:0);
        s.Lens(t,out var lens);sum+=lens.Letterbox+lens.Black;
        return sum;
    }
    private static void Determinism(NativeIntroGenericScript s)
    {
        string n=s.Title;var a=Fresh(s);var b=Fresh(s);
        int frames=(int)(s.Length*30)+3;var order=new int[frames];for(int i=0;i<frames;i++)order[i]=i;
        var rng=new Random(7);for(int i=frames-1;i>0;i--){int j=rng.Next(i+1);(order[i],order[j])=(order[j],order[i]);}
        var seq=new double[frames];for(int i=0;i<frames;i++)seq[i]=Frame(a,i/30f,true,1,false,false);
        for(int k=0;k<frames;k++){int i=order[k];double x=Frame(b,i/30f,true,1,false,false);Check(Math.Abs(x-seq[i])<1e-3*Math.Max(1,Math.Abs(x)),n+" draws the same frame at "+(i/30f).ToString("0.000")+" s whatever was drawn before (a scrub)");}
        // Finite, and no allocation per frame once warm.
        for(float age=0;age<=s.Length;age+=1/60f){Frame(a,age,true,1,false,false);Check(!fx.Bad&&NativeIntroMotion.Finite(pose),n+" draws only finite values at "+age.ToString("0.00"));}
        for(int pass=0;pass<2;pass++)
        {
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(float age=0;age<=s.Length+.4f;age+=1/60f){Frame(a,age,pass==0,1,false,false);Frame(a,age,true,.3f,true,true);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;if(pass==1)Check(used==0,n+" allocates nothing per frame ("+used+" bytes)");
        }
    }
    private static NativeIntroGenericScript Fresh(NativeIntroGenericScript s)=>(NativeIntroGenericScript)Activator.CreateInstance(s.GetType());
    private static void Budgets(NativeIntroGenericScript s)
    {
        string n=s.Title;int fxMax=0,fxLow=0,pieces=0,prims=0,points=0,voidPrims=0;
        for(float age=0;age<=s.Length;age+=1/60f)
        {
            Frame(s,age,true,1,false,false);fxMax=Math.Max(fxMax,fx.Calls);pieces=Math.Max(pieces,set.Pieces);prims=Math.Max(prims,set.Primitives+props.Count);points=Math.Max(points,lights.Points);
            Check(set.Missing.Count==0,n+" names its bank's props in its own folder");
            Frame(s,age,true,.3f,true,false);fxLow=Math.Max(fxLow,fx.Calls);
            Frame(s,age,false,1,false,false);voidPrims=Math.Max(voidPrims,set.Primitives+props.Count);Check(set.Pieces==0,n+" draws no bank piece without the bank");
        }
        Check(fxMax<=600,n+": at most 600 effects a frame ("+fxMax+")");Check(fxLow<=300,n+": at most 300 effects a frame on Low ("+fxLow+")");
        Check(pieces>0&&pieces<=80,n+": 1-80 set pieces a frame ("+pieces+")");Check(prims<=120&&voidPrims<=120,n+": at most 120 primitives ("+prims+", "+voidPrims+" without the bank)");
        Check(voidPrims>0,n+" draws a set without its bank (the plain fallback, never blocks)");Check(points<=24,n+": at most 24 point lights ("+points+")");
    }
    /// <summary>In Reduced effects nothing flashes faster than 3 Hz: the lights' and the glows' totals, sampled at 60 Hz, have no two
    /// prominent peaks closer than 1/3 s.</summary>
    private static void Reduced(NativeIntroGenericScript s)
    {
        string n=s.Title;int frames=(int)(s.Length*60);var light=new float[frames];var glow=new float[frames];
        for(int i=0;i<frames;i++)
        {
            Frame(s,i/60f,true,1,false,true);float l=0;for(int k=0;k<lights.Points;k++){var p=lights.Point[k];l+=(float)(p.R+p.G+p.B);}light[i]=l;glow[i]=fx.Light;
        }
        Peaks(n+"'s lights",light);Peaks(n+"'s glows",glow);
    }
    private static void Peaks(string what,float[] v)
    {
        float max=0;foreach(var x in v)max=Math.Max(max,x);if(max<=0)return;int lastPeak=-1000;
        for(int i=1;i<v.Length-1;i++)
        {
            if(!(v[i]>=v[i-1]&&v[i]>v[i+1]))continue;
            float left=v[i],right=v[i];for(int k=1;k<=30&&i-k>=0;k++)left=Math.Min(left,v[i-k]);for(int k=1;k<=30&&i+k<v.Length;k++)right=Math.Min(right,v[i+k]);
            float prominence=v[i]-Math.Max(left,right);if(prominence<.25f*max)continue;
            Check(i-lastPeak>=20,what+" flash no faster than 3 Hz in Reduced effects (peaks at "+(lastPeak/60f).ToString("0.00")+" and "+(i/60f).ToString("0.00")+" s)");lastPeak=i;
        }
    }
    /// <summary>Every streak longer than 1 m lies on its head's camera plane (within 2 degrees), so its ends stay on its line.</summary>
    private static void Streaks(NativeIntroGenericScript s)
    {
        string n=s.Title;int seen=0;
        foreach(bool reduced in new[]{false,true})
        for(float age=0;age<=s.Length;age+=1/30f)
        {
            fx.Record=true;Frame(s,age,true,1,false,reduced);fx.Record=false;
            s.Camera(age,out var view);var f=Vector3.Normalize(view.Look-view.Eye);
            foreach(var (head,axis) in fx.Long)
            {
                seen++;float zh=Vector3.Dot(head-view.Eye,f);if(zh<.25f)continue;
                // the tail's depth along the camera's forward equals the head's
                float off=Math.Abs(Vector3.Dot(axis,f))/axis.Length();Check(off<(float)Math.Sin(2*Math.PI/180),n+": a "+axis.Length().ToString("0.0")+" m streak at "+age.ToString("0.00")+" s lies on its head's camera plane ("+off+")");
            }
        }
        Check(true,n+" laid "+seen+" long streaks on their camera planes");
    }
}
