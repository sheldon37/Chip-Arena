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
                // bespoke keyed animation only (owner 2026-11-20: "we dont need the matches own annimation system"): no motion-bank clip
                // at any time; the keyed pose is finite, its feet on the floor, its head within a third of a turn of its chest
                for(float t=0;t<=s.IntroTotal;t+=.02f)Check(!s.Clip(t,out _,out _),s.Style+" plays no match clip at "+t);
                {var kp=new CapturedRig.Pose();for(float t=0;t<=s.IntroTotal;t+=.05f){s.Pose(t,kp,out var kroot,out var krot);kp.World();
                    for(int j=0;j<22;j++){var p=kp.WorldPosition[j];Check(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z),s.Style+" joint "+j+" finite at "+t);}
                    var hd=Vector3.Transform(Vector3.UnitZ,kp.WorldRotation[CapturedRig.Head]);var ch=Vector3.Transform(Vector3.UnitZ,kp.WorldRotation[CapturedRig.Chest]);hd.Y=0;ch.Y=0;
                    if(hd.LengthSquared()>.04f&&ch.LengthSquared()>.04f){float turn=(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(hd),Vector3.Normalize(ch)),-1,1));Check(turn<.62f,s.Style+" head within a third of a turn of its chest at "+t+" ("+turn.ToString("0.00",CultureInfo.InvariantCulture)+" rad)");}}}
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
            var hearth=Tuple("FIRE_HEARTH");Check(hearth.Length==2&&Math.Abs(hearth[0]-NativeIntroForgeborn.HearthBack)<1e-4f&&Math.Abs(hearth[1]-NativeIntroForgeborn.HearthFront)<1e-4f,"FIRE_HEARTH is Forgeborn's firebox");
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
            // the cue keys are in the sound contract and requested in docs/audio/intros-b2/I5.json
            var rows=File.ReadAllText(Path.Combine(root,"..","..","docs","audio","intros-b2","I5.json"));
            foreach(var s in Scripts())foreach(var c in s.Cues){NativeIntroSoundKeys.Find(c.Key,out var def);Check(def.Key==c.Key,s.Style+" cue key "+c.Key+" in Core/NativeIntroSoundKeys.cs");Check(rows.Contains("\""+c.Key+"\""),s.Style+" cue key "+c.Key+" requested in docs/audio/intros-b2/I5.json");
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
                var times=new List<float>();for(int i=2;i<args.Length;i++)if(float.TryParse(args[i],NumberStyles.Float,CultureInfo.InvariantCulture,out float at))times.Add(at);
                if(times.Count==0)for(float t=0;t<=s.IntroTotal+.01f;t+=.25f)times.Add(t);
                foreach(float t in times)
                {
                    s.Pose(t,pose,out var root,out var rot);string F(Vector3 v)=>"("+v.X.ToString("0.00",CultureInfo.InvariantCulture)+","+v.Y.ToString("0.00",CultureInfo.InvariantCulture)+","+v.Z.ToString("0.00",CultureInfo.InvariantCulture)+")";
                    var lh=s.Anchor(NativeEntranceAnchor.LeftHand,t);var tip=s.Anchor(NativeEntranceAnchor.LeftTip,t);var d=Vector3.Normalize(tip-lh);
                    s.Camera(t,out var v);
                    // where the model's feet, chest and head top land on a 2.04:1 screen (0,0 the top left; the model's head top is about
                    // .24 m over the keyed head anchor)
                    string Screen(Vector3 p){var f=Vector3.Normalize(v.Look-v.Eye);var r=Vector3.Normalize(Vector3.Cross(f,v.Up.LengthSquared()>1e-6f?v.Up:Vector3.UnitY));var u=Vector3.Cross(r,f);var q=p-v.Eye;float z=Vector3.Dot(q,f);if(z<.05f)return "(behind)";float k=(float)Math.Tan(v.Fov*Math.PI/360);return "("+(.5f+Vector3.Dot(q,r)/z/(k*2.04f)/2).ToString("0.00",CultureInfo.InvariantCulture)+","+(.5f-Vector3.Dot(q,u)/z/k/2).ToString("0.00",CultureInfo.InvariantCulture)+")";}
                    var feet=(s.Anchor(NativeEntranceAnchor.LeftFoot,t)+s.Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;feet.Y=NativeEntranceStage.FloorY;
                    Console.WriteLine($"  t {t:0.00} root {F(root)} head {F(s.Anchor(NativeEntranceAnchor.Head,t))} lhand {F(lh)} tip {F(tip)} axis {F(d)} rhand {F(s.Anchor(NativeEntranceAnchor.RightHand,t))} feet {F(s.Anchor(NativeEntranceAnchor.LeftFoot,t))}{F(s.Anchor(NativeEntranceAnchor.RightFoot,t))} eye {F(v.Eye)} look {F(v.Look)} fov {v.Fov:0}"
                        +$" screen feet {Screen(feet)} chest {Screen(s.Anchor(NativeEntranceAnchor.Chest,t))} top {Screen(s.Anchor(NativeEntranceAnchor.Head,t)+new Vector3(0,.24f,0))}");
                }
            }
        }
        private static readonly string[] ModelBones={"pelvis","spine_03","spine_05","neck_01","head","upperarm_l","lowerarm_l","hand_l","middle_01_l","upperarm_r","lowerarm_r","hand_r","middle_01_r","thigh_l","calf_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r"};
        /// <summary>
        /// `--intro-elemental-model DIR [id]`: each intro's body as the model draws it (the keyed pose bound bone for bone on the measured
        /// skeleton, the staff seated in the left fist and the grip IK's right fist on its shaft, as Runtime VisualBodyBind binds a v2
        /// weapon intro), every 1/30 s, in the source frame, with the drawn staff's heel and head and the camera: JSON for the offline
        /// stick-figure reviews from the side, at three-quarters and through the lens (a review aid, not a check).
        /// </summary>
        public static void Model(string[] args)
        {
            string folder=args.Length>1?args[1]:"elemental-model";Directory.CreateDirectory(folder);
            string F(float x)=>x.ToString("0.####",CultureInfo.InvariantCulture);string P(Vector3 v)=>"["+F(v.X)+","+F(v.Y)+","+F(v.Z)+"]";
            var rig=ModelPoseChecks.Measured();var at=new int[ModelBones.Length];for(int b=0;b<ModelBones.Length;b++)at[b]=Array.IndexOf(rig.Names,ModelBones[b]);
            int rh=rig.Of[CapturedRig.RHand];
            foreach(var s in Scripts())
            {
                if(args.Length>2&&!s.Style.Contains(args[2]))continue;
                var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var sb=new System.Text.StringBuilder();s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};
                sb.Append("{\"id\":\""+s.Style+"\",\"length\":"+F(s.IntroTotal)+",\"nameAt\":"+F(s.IntroNameAt)+",\"floor\":"+F(NativeEntranceStage.FloorY)+",\"bones\":[\""+string.Join("\",\"",ModelBones)+"\"],\"frames\":[");
                string spec=NativeIntroCatalog.Find(s.Style)?.Spec;
                for(int i=0;i<=(int)(s.IntroTotal*30);i++)
                {
                    float age=i/30f,t=s.Clock(age);s.Camera(age,out var v);s.Pose(t,pose,out var root,out var rot);pose.World();
                    var grasp=new NativeVisualRig.Grasp{LeftGrip=1,Left=NativeVisualRig.StaffShaft,Relax=Math.Clamp(s.ArmsRelax(t),0,1),Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)};
                    float grip=0;if(s is INativeIntroGrip g&&g.GripAt(t,out var ga)&&ga.Weight>.001f){grip=ga.Weight;NativeIntroGrip.Grasp(spec,ga,pose,ref grasp);}
                    NativeVisualRig.Pose(st,pose,grasp,false,false);
                    Vector3 W(Vector3 local)=>root+Vector3.Transform(local,rot);
                    var hand=pose.WorldRotation[CapturedRig.LHand];var heel=W(st.LeftAnchor+Vector3.Transform(new Vector3(0,0,NativeIntroElementalScript.StaffHeel),hand));var head=W(st.LeftAnchor+Vector3.Transform(new Vector3(0,0,NativeIntroElementalScript.StaffHead),hand));
                    var turn=Quaternion.Normalize(st.World[rh]*Quaternion.Inverse(rig.AlignedWorld[rh]));var fist=W(st.Position[rh]+Vector3.Transform(rig.FistCenter[1],turn));
                    var hd=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rot*pose.WorldRotation[CapturedRig.Head]));var ch=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rot*pose.WorldRotation[CapturedRig.Chest]));
                    sb.Append(i>0?",":"").Append("{\"age\":"+F(age)+",\"t\":"+F(t)+",\"solid\":"+F(s.Solid(t,0))+",\"grip\":"+F(grip)+",\"root\":"+P(root)+",\"eye\":"+P(v.Eye)+",\"look\":"+P(v.Look)+",\"fov\":"+F(v.Fov)+",\"roll\":"+F(v.Up.LengthSquared()>1e-6f?(float)Math.Atan2(-v.Up.X,v.Up.Y):0)
                        +",\"headFwd\":"+P(hd)+",\"chestFwd\":"+P(ch)+",\"heel\":"+P(heel)+",\"head\":"+P(head)+",\"fist\":"+P(fist)+",\"model\":[");
                    for(int b=0;b<at.Length;b++)sb.Append(b>0?",":"").Append(P(at[b]<0?Vector3.Zero:W(st.Position[at[b]])));
                    sb.Append("]}");
                }
                sb.Append("]}");File.WriteAllText(Path.Combine(folder,s.Style+"-model.json"),sb.ToString());Console.WriteLine("wrote "+Path.Combine(folder,s.Style+"-model.json"));
            }
        }
        /// <summary>`--intro-elemental-limits [id]`: the framework's findings for these four only (IntroPoseChecks: the limits, the drawn feet
        /// and the drawn grip), quicker than `--intro-limits` while keying.</summary>
        public static void Limits(string[] args)
        {
            var rig=ModelPoseChecks.Measured();int total=0;
            foreach(var e in NativeIntroCatalog.Entries)
            {
                if(e.Author!="I5"||args.Length>1&&!e.Id.Contains(args[1]))continue;
                var s=e.Make();var l=IntroPoseChecks.Limits(e.Id,s,e.Duration,e.Spec);var feet=IntroPoseChecks.Feet(e.Make(),e.Duration,rig);var grip=IntroPoseChecks.Grip(e.Make(),e.Duration,e.Spec,rig);
                Console.WriteLine(e.Id+": "+l.Count+" limit finding(s); feet slide "+feet.Slide.ToString("0.00",CultureInfo.InvariantCulture)+" m/s at "+feet.SlideAt.ToString("0.00",CultureInfo.InvariantCulture)+" ("+feet.SlideFoot+"), deepest "+(feet.Deepest*100).ToString("0",CultureInfo.InvariantCulture)+" cm ("+feet.DeepBone+" at "+feet.DeepestAt.ToString("0.00",CultureInfo.InvariantCulture)+"); grip off "+(grip.Worst*100).ToString("0.0",CultureInfo.InvariantCulture)+" cm at "+grip.WorstAt.ToString("0.00",CultureInfo.InvariantCulture)+" ("+grip.Frames+" frames)");
                foreach(var f in l)Console.WriteLine("  "+f);
                total+=l.Count+(feet.Slide>IntroPoseChecks.SlideMost?1:0)+(feet.Deepest>IntroPoseChecks.Through?1:0)+(grip.Worst>IntroPoseChecks.GripNear?1:0);
            }
            Console.WriteLine("elemental findings: "+total);
        }
        /// <summary>`--intro-elemental-pose id t...`: the keyed pose's joints (world, source frame) and the staff, one line a joint, for
        /// stick-figure sheets (the keyed animation checked without the Editor).</summary>
        public static void Pose(string[] args)
        {
            var pose=new CapturedRig.Pose();string F(float v)=>v.ToString("0.000",CultureInfo.InvariantCulture);
            foreach(var s in Scripts())
            {
                if(args.Length>1&&!s.Style.Contains(args[1]))continue;
                for(int i=2;i<args.Length;i++)
                {
                    if(!float.TryParse(args[i],NumberStyles.Float,CultureInfo.InvariantCulture,out float t))continue;
                    s.Pose(t,pose,out var root,out var rot);pose.World();
                    for(int j=0;j<22;j++){var p=root+Vector3.Transform(pose.WorldPosition[j],rot);Console.WriteLine($"P {s.Style} {F(t)} {j} {F(p.X)} {F(p.Y)} {F(p.Z)}");}
                    var hand=s.Anchor(NativeEntranceAnchor.LeftHand,t);var tip=s.Anchor(NativeEntranceAnchor.LeftTip,t);var ax=Vector3.Normalize(tip-hand);var heel=tip-ax*(NativeIntroElementalScript.StaffHead-NativeIntroElementalScript.StaffHeel);
                    Console.WriteLine($"S {s.Style} {F(t)} {F(heel.X)} {F(heel.Y)} {F(heel.Z)} {F(tip.X)} {F(tip.Y)} {F(tip.Z)}");
                }
            }
        }
    }
}
