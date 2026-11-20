using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The elemental intros' own checks (docs/INTROS-ELEMENTAL.md), beside the framework's IntroV2Checks: the set script and the
    /// Core scripts keep the same numbers (the vent, the lava rings, the rivers, the hall, the spires, the monoliths); the staff
    /// is where its grip track puts it (the left arm reaches it); each intro draws its whole modelled set through a fake bank
    /// and the void without one; every streak over 1 m is laid on its head's camera plane; the cue keys are the generator's
    /// pattern; Reduced effects keeps the lights' flicker slow. `--intro-elemental-probe` prints the anchors for tuning.
    /// </summary>
    internal static class IntroElementalChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Elemental intros: "+what);count++;}
        internal static NativeIntroElementalScript[] Scripts()=>new NativeIntroElementalScript[]{new NativeIntroForgeborn(),new NativeIntroStillTide(),new NativeIntroStrikePoint(),new NativeIntroBlackSun()};
        private sealed class Set:INativeIntroSet
        {
            public bool Bank;public readonly HashSet<string> Asked=new HashSet<string>();public int Pieces,Primitives;public readonly List<string> Drawn=new List<string>();
            public bool Ready=>Bank;public bool Has(string prop){Asked.Add(prop);return Bank;}
            public void Piece(string prop,Vector3 at,Quaternion rotation,float scale,Vector3 tint,Vector3 glow){Pieces++;Drawn.Add(prop);if(!float.IsFinite(glow.X)||glow.X<0)throw new Exception("glow "+prop);}
            public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Primitives++;if(shape==WeaponShape.Ring)throw new Exception("a ring primitive");}
            public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> rotation,float scale,Vector3 tint){Pieces++;Drawn.Add(prop);}
        }
        private sealed class Fx:INativeEntranceFx
        {
            public int Calls;public NativeEntranceView View;public float Worst;public int Long;
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Calls++;if(profile==2)throw new Exception("a ring glow");}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)
            {
                Calls++;var axis=velocity*blur;if(axis.Length()<=1.0001f)return;Long++;
                // on its head's camera plane: the tail's depth along the view equals the head's
                var f=Vector3.Normalize(View.Look-View.Eye);float zh=Vector3.Dot(head-View.Eye,f);if(zh<=.2f)return;var tail=head-axis;float zt=Vector3.Dot(tail-View.Eye,f);
                float off=Math.Abs(zh-zt)/Math.Max(.01f,axis.Length());Worst=Math.Max(Worst,off);
            }
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Calls++;}
        }
        public static void Run(string root)
        {
            count=0;
            SetScript(root);
            foreach(var s in Scripts())
            {
                var entry=NativeIntroCatalog.Find(s.Style);
                Check(entry==null||Math.Abs(entry.Duration-s.IntroTotal)<1e-4f&&Math.Abs(entry.NameAt-s.IntroNameAt)<1e-4f,s.Style+" registry length and name beat");
                Check(s.IntroTotal>=6&&s.IntroTotal<=8.5f,s.Style+" 6-8.5 s");
                Check(s.SetBank=="ChipArenaIntroElemental",s.Style+" bank");
                // the model's clips: from the first window to the end, in order, each a staff clip the motion bank has (mirrored: the staff hand)
                {string first=null;float lastEnd=-1;for(float t=0;t<=s.IntroTotal;t+=.02f){if(s.Clip(t,out var clip,out float time)){first??=clip;Check(Known(clip),s.Style+" clip "+clip);Check(time>=-1e-4f,s.Style+" clip time");lastEnd=t;}else Check(first==null,s.Style+" never leaves its clips for the keys (a pop) at "+t);}
                 Check(first!=null&&lastEnd>=s.IntroTotal-.03f,s.Style+" plays the model's clips to the end");}
                // the set: every piece through the bank, the void without it
                var set=new Set{Bank=true};
                for(float t=0;t<=s.IntroTotal;t+=.25f){set.Pieces=0;s.Set(t,set);Check(set.Pieces>=4&&set.Pieces<=80,s.Style+" set pieces "+set.Pieces+" at "+t);Check(set.Primitives==0,s.Style+" no primitives over the bank");}
                var none=new Set{Bank=false};s.Set(1,none);Check(none.Pieces==0&&none.Primitives>0,s.Style+" the void without the bank");
                // cue keys: the generator's pattern (letters, digits, dots), sorted, inside the intro
                float last=-1;foreach(var c in s.Cues){Check(c.Key==null||Regex.IsMatch(c.Key,"^intro2\\.[a-z0-9.]+$"),s.Style+" cue key "+c.Key);Check(c.At>=last&&c.At>=0&&c.At<=s.IntroTotal,s.Style+" cue order");last=c.At;Check(c.Alt!=null&&c.Clip!=null,s.Style+" cue fallbacks");}
                // streaks on their camera plane, within budget, at every 1/30 s
                foreach(bool reduced in new[]{false,true})foreach(bool low in new[]{false,true})
                {
                    s.Context=new NativeIntroContext{Reduced=reduced,Low=low,Quality=low?.6f:1,Name="Pilot"};var fx=new Fx();int peak=0;float peakAt=0;
                    for(float age=0;age<=s.IntroTotal;age+=1/30f)
                    {
                        float t=s.Clock(age);s.Camera(age,out fx.View);fx.Calls=0;s.Effects(t,fx,s.Context.Quality);if(fx.Calls>peak){peak=fx.Calls;peakAt=t;}
                    }
                    Console.WriteLine("  "+s.Style+(reduced?" reduced":"")+(low?" low":"")+": peak at t "+peakAt.ToString("0.00",CultureInfo.InvariantCulture));
                    Check(fx.Worst<.035f,s.Style+" long streaks off their head's plane by "+fx.Worst.ToString("0.000",CultureInfo.InvariantCulture));
                    Check(peak<=(low?300:600),s.Style+" FX calls at peak "+peak+(low?" (low)":""));
                    Console.WriteLine("  "+s.Style+(reduced?" reduced":"")+(low?" low":"")+": peak "+peak+" FX calls, "+fx.Long+" long streaks");
                }
                s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};
                // the staff where its track puts it: the left wrist on the grip within 4 cm wherever the arm can reach
                var pose=new CapturedRig.Pose();int reached=0,tried=0;
                for(float t=0;t<=s.IntroTotal;t+=.1f){s.Pose(t,pose,out _,out _);tried++;if(Finite(s.Anchor(NativeEntranceAnchor.LeftTip,t)))reached++;}
                Check(reached==tried,s.Style+" staff anchors finite");
            }
            Console.WriteLine("PASS elemental intros ("+count+" checks)");
        }
        /// <summary>The motion bank's staff clips these intros use (Resources/ChipArenaVisualMotion.bytes, 2026-11-20).</summary>
        private static readonly HashSet<string> Bank=new HashSet<string>{"2Hand-Staff-Idle~mirror","2Hand-Staff-Cast-L-Buff1_start~mirror","2Hand-Staff-Cast-L-Buff1_end~mirror","2Hand-Staff-Cast-L-Buff2_start~mirror","2Hand-Staff-Cast-L-Buff2_end~mirror","2Hand-Staff-Cast-L-AOE1_start~mirror","2Hand-Staff-Cast-L-AOE1_end~mirror","2Hand-Staff-Cast-L-AOE2_start~mirror","2Hand-Staff-Cast-L-AOE2_end~mirror","2Hand-Staff-Cast-L-Summon1_start~mirror","2Hand-Staff-Cast-L-Summon1_end~mirror","2Hand-Staff-Cast-L-Summon2_start~mirror","2Hand-Staff-Cast-L-Summon2_end~mirror","2Hand-Staff-Cast-Attack1_start~mirror","2Hand-Staff-Cast-Attack1_end~mirror"};
        private static bool Known(string clip)=>clip!=null&&Bank.Contains(clip);
        private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
        /// <summary>The set script's numbers are the Core scripts'.</summary>
        private static void SetScript(string root)
        {
            var path=Path.Combine(root,"tools","intros","elemental_set.py");Check(File.Exists(path),"tools/intros/elemental_set.py");var text=File.ReadAllText(path);
            float[] Tuple(string name){var m=Regex.Match(text,"^"+name+@"\s*=\s*\(([^)]*)\)",RegexOptions.Multiline);Check(m.Success,"set script "+name);var parts=m.Groups[1].Value.Split(',');var o=new List<float>();foreach(var p in parts){var q=p.Trim();if(q.Length>0)o.Add(float.Parse(q,CultureInfo.InvariantCulture));}return o.ToArray();}
            var vent=Tuple("FIRE_VENT");Check(Math.Abs(vent[0]-NativeIntroForgeborn.Vent.X)<.002f&&Math.Abs(vent[1]-NativeIntroForgeborn.Vent.Z)<.002f,"FIRE_VENT is Forgeborn's vent");
            var rings=Tuple("FIRE_RINGS");Check(rings.Length==NativeIntroForgeborn.Rings.Length,"FIRE_RINGS count");for(int i=0;i<rings.Length;i++)Check(Math.Abs(rings[i]-NativeIntroForgeborn.Rings[i])<1e-4f,"FIRE_RINGS "+i);
            var rivers=Tuple("FIRE_RIVERS");for(int i=0;i<rivers.Length;i++)Check(Math.Abs(rivers[i]-NativeIntroForgeborn.Rivers[i])<1e-4f,"FIRE_RIVERS "+i);
            // "A, B = 1, 2" lines
            float Named(string name){var m=Regex.Match(text,@"^([A-Z_, ]+)=\s*([-0-9., ]+)$",RegexOptions.Multiline);foreach(Match l in Regex.Matches(text,@"^([A-Z0-9_, ]+?)\s*=\s*([-0-9.]+(?:\s*,\s*[-0-9.]+)*)\s*(?:#.*)?$",RegexOptions.Multiline)){var names=l.Groups[1].Value.Split(',');var vals=l.Groups[2].Value.Split(',');for(int i=0;i<names.Length&&i<vals.Length;i++)if(names[i].Trim()==name)return float.Parse(vals[i].Trim(),CultureInfo.InvariantCulture);}Check(false,"set script "+name);return 0;}
            void Same(float[] a,float[] b,string what){Check(a.Length==b.Length,what+" count");for(int i=0;i<a.Length;i++)Check(Math.Abs(a[i]-b[i])<1e-4f,what+" "+i);}
            Check(Math.Abs(Named("FACING")-NativeProceduralEntrance.Facing)<1e-5f,"FACING is the hero facing");
            Same(Tuple("WATER_SKYLIGHTS"),NativeIntroStillTide.Skylights,"WATER_SKYLIGHTS");Same(Tuple("WATER_ICE_RINGS"),NativeIntroStillTide.IceRings,"WATER_ICE_RINGS");
            Same(Tuple("WATER_LEVEL"),new[]{NativeIntroStillTide.Shallow,NativeIntroStillTide.Tide},"WATER_LEVEL");Check(Math.Abs(Named("WATER_HALF")-NativeIntroStillTide.HalfLength)<1e-4f,"WATER_HALF");
            Check(Math.Abs(Named("VAULT_TOP")-NativeIntroStillTide.VaultTop)<1e-4f&&Math.Abs(Named("ROSE_Z")-NativeIntroStillTide.RoseZ)<1e-4f&&Math.Abs(Named("ROSE_Y")-NativeIntroStillTide.RoseY)<1e-4f,"the hall's vault and rose");
            Same(Tuple("SPIRE_BEARINGS"),NativeIntroStrikePoint.SpireBearings,"SPIRE_BEARINGS");Check(Math.Abs(Named("SPIRE_RING")-NativeIntroStrikePoint.SpireRing)<1e-4f&&Math.Abs(Named("SPIRE_TIP")-NativeIntroStrikePoint.SpireTip)<1e-4f,"the spires' ring and tips");
            Same(Tuple("DARK_MONOLITHS"),NativeIntroBlackSun.Monoliths,"DARK_MONOLITHS");Check(Math.Abs(Named("DARK_RING")-NativeIntroBlackSun.MonolithRing)<1e-4f&&Math.Abs(Named("DARK_GATE_Z")-NativeIntroBlackSun.GateZ)<1e-4f,"the monoliths' ring and the gate");
            // the props the scripts draw are the ones the script builds
            var built=new HashSet<string>(StringComparer.Ordinal);foreach(Match m in Regex.Matches(text,@"'((?:fire|water|lightning|darkness)/[a-z/-]+)'"))built.Add(m.Groups[1].Value);
            foreach(var s in Scripts()){var set=new Set{Bank=true};for(float t=0;t<=s.IntroTotal;t+=.1f)s.Set(t,set);foreach(var prop in set.Drawn){string rest=prop.Substring(prop.IndexOf('/')+1),numbered=Regex.Replace(prop,@"/\d+$","/%d"),restNumbered=Regex.Replace(rest,@"/\d+$","/%d");Check(built.Contains(prop)||text.Contains("'"+numbered+"'")||text.Contains("'"+rest+"'")||text.Contains("'"+restNumbered+"'"),s.Style+" draws "+prop+", which the set script builds");}}
            // the cue keys are in the sound contract and requested in docs/audio/intros/I5.json
            var rows=File.ReadAllText(Path.Combine(root,"..","..","docs","audio","intros","I5.json"));
            foreach(var s in Scripts())foreach(var c in s.Cues){NativeIntroSoundKeys.Find(c.Key,out var def);Check(def.Key==c.Key,s.Style+" cue key "+c.Key+" in Core/NativeIntroSoundKeys.cs");Check(rows.Contains("\""+c.Key+"\""),s.Style+" cue key "+c.Key+" requested in docs/audio/intros/I5.json");
                // the existing key plays as a one-shot: the bank refuses a loop entry here (Runtime NativeEffectsAudio.BankPlay)
                Check(NativeSoundKeys.Find(c.Alt,out var alt)&&alt.Space!=NativeSoundSpace.Loop,s.Style+" cue "+c.Key+" falls back to "+c.Alt+", a known one-shot bank key");}
        }
        /// <summary>`--intro-elemental-probe [id] [t...]`: the anchors (hands, the staff's tip, the head, the root) at sampled times.</summary>
        public static void Probe(string[] args)
        {
            var pose=new CapturedRig.Pose();
            foreach(var s in Scripts())
            {
                if(args.Length>1&&!s.Style.Contains(args[1]))continue;Console.WriteLine(s.Style);
                for(float t=0;t<=s.IntroTotal+.01f;t+=.25f)
                {
                    s.Pose(t,pose,out var root,out var rot);string F(Vector3 v)=>"("+v.X.ToString("0.00",CultureInfo.InvariantCulture)+","+v.Y.ToString("0.00",CultureInfo.InvariantCulture)+","+v.Z.ToString("0.00",CultureInfo.InvariantCulture)+")";
                    var lh=s.Anchor(NativeEntranceAnchor.LeftHand,t);var tip=s.Anchor(NativeEntranceAnchor.LeftTip,t);var d=Vector3.Normalize(tip-lh);
                    s.Camera(t,out var v);
                    Console.WriteLine($"  t {t:0.00} root {F(root)} head {F(s.Anchor(NativeEntranceAnchor.Head,t))} lhand {F(lh)} tip {F(tip)} axis {F(d)} rhand {F(s.Anchor(NativeEntranceAnchor.RightHand,t))} feet {F(s.Anchor(NativeEntranceAnchor.LeftFoot,t))}{F(s.Anchor(NativeEntranceAnchor.RightFoot,t))} eye {F(v.Eye)} look {F(v.Look)} fov {v.Fov:0}");
                }
            }
        }
    }
}
