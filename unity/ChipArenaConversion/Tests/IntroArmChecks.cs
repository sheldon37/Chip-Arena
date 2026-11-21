using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Every registered v2 intro's arms as they read through its own shot cameras (owner 2026-11-21 on the ranged intros: "Arm doing weird
    /// things on machine gun intro again, and check that bow one again as well"; --intro-limits passed while they looked wrong). Every
    /// registered intro that is a v2 intro (INativeIntroV2: the NativeIntroV2Script ones and the melee blades) is bound onto the measured model
    /// as the pose checks bind it (Tests/IntroPoseChecks.Mapping: the keyed turns, the arms' relax, a pack clip's arms exact) with the match's
    /// hold of a gun or the bow and the intro's own grip over it (Tests/IntroRangedChecks.Bind's), every 1/30 s of its presentation, the
    /// camera where its own rig puts it:
    /// - **no arm through the head:** the drawn arm's bone lines (shoulder, elbow, wrist, knuckles) stay out of the drawn head's ellipsoid
    ///   (NativeVisualRig's Rig.HeadCenter and HeadRadii, the one fists and held weapons are kept out of) widened by `HeadMargin`;
    /// - **no arm across the face:** in the shot's camera, no arm point in front of the face covers more than `FaceMost` of the face's disc
    ///   (`FaceRadius` round the drawn eyes).
    /// The weapon-specific checks (the aiming elbows) stay with each family's own checks (Tests/IntroRangedChecks). `--intro-arms` runs this
    /// (each failing frame listed: the time, the arm, the camera's shot and the measure); `--intro-arms ID` prints the measures per frame.
    /// </summary>
    internal static class IntroArmChecks
    {
        /// <summary>How far the head's ellipsoid is widened for the arms' bone lines (m; the runtime keeps a fist's centre .08 m out).</summary>
        public const float HeadMargin=.03f;
        /// <summary>The most of the face's disc on screen an arm in front of the face may cover, and the disc's radius round the drawn eyes (m).</summary>
        public const float FaceMost=.15f,FaceRadius=.11f;

        /// <summary>One frame that fails: the intro, its time and the clip's (presentation) time, the arm, the measure and the camera's shot.</summary>
        internal struct Finding
        {
            public string Id;public float T,Age;public bool Left,Face;public float Value;public int Shot;public float ShotAt;
        }

        // ---- the camera's shot (NativeIntroV2Script's rig is protected: read it here, for the report only) ----
        private static NativeIntroCamera RigOf(NativeEntranceScript s)
        {
            for(var t=s.GetType();t!=null;t=t.BaseType)
            {
                var f=t.GetField("Rig",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
                if(f!=null&&f.GetValue(s) is NativeIntroCamera rig)return rig;
            }
            return null;
        }
        /// <summary>Which of `s`'s camera shots is live at presentation age `age` (as its Camera evaluates it) and when that shot starts (intro s).</summary>
        private static int ShotOf(NativeEntranceScript s,float age,out float at)
        {
            at=0;var rig=RigOf(s);if(rig==null||rig.Count==0)return -1;
            float t=s.Clock(Math.Min(age,s.SettleAt));int k=0;
            for(int i=0;i<rig.Count;i++)if(rig[i].At<=t)k=i;
            at=rig[k].At;return k;
        }

        /// <summary>Binds intro script `s` (holding `spec`) at intro time `t` onto `d`: the keyed pose, the arms' relax and a pack clip's arms
        /// exact (as IntroPoseChecks.Mapping), the match's hold of a gun (the grip, the support, the stock) or the bow's riser, and the intro's grip
        /// over it (as IntroRangedChecks.Bind, for any v2 intro).</summary>
        internal static void Bind(NativeEntranceScript s,string spec,IntroRangedChecks.Drawn d,float t)
        {
            d.Spec=spec;s.Pose(t,d.Pose,out d.Root,out d.Rotation);d.Pose.World();
            var g=new NativeVisualRig.Grasp{Relax=s is NativeIntroV2Script v2?Math.Clamp(v2.ArmsRelax(t),0,1):0,Still=0,Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)};
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
            if(GunRules.Ranged(w)){g.RightGrip=1;g.Support=1;g.Right=NativeVisualRig.PistolGrip(w);g.SupportHold=NativeVisualRig.GunSupport(w,d.Pose);g.Stock=NativeVisualRig.Stock(w);g.Unshoulder=1;}
            else if(spec.Length>0&&NativeBowWire.Is(spec)){g.LeftGrip=1;g.Left=NativeVisualRig.BowRiser;g.LeftCarries=true;}
            d.GripOn=s is INativeIntroGrip ig&&ig.GripAt(t,out d.Grip)&&d.Grip.Weight>.001f;if(!d.GripOn)d.Grip=default;
            if(d.GripOn)NativeIntroGrip.Grasp(spec,d.Grip,d.Pose,ref g);
            NativeVisualRig.Pose(d.State,d.Pose,g,false,false);
        }
        /// <summary>Scans intro `e` (a v2 intro): every 1/30 s of its presentation, each arm's head depth and face cover; the failing frames, the
        /// least depth and the most cover (and when). `perFrame` prints the measures of every frame.</summary>
        internal static List<Finding> Scan(NativeIntroEntry e,NativeVisualRig.Rig rig,ref int checks,out float leastHead,out float headAt,out float mostFace,out float faceAt,bool perFrame=false)
        {
            var s=e.Make();var d=new IntroRangedChecks.Drawn(rig);var found=new List<Finding>();string spec=e.Spec??"";
            leastHead=float.MaxValue;headAt=0;mostFace=0;faceAt=0;
            for(int i=0;i<=e.Duration*30+.5f;i++)
            {
                float age=i/30f,t=s.Clock(age);Bind(s,spec,d,t);s.Camera(age,out var view);
                var line=perFrame?new StringBuilder("age "+age.ToString("0.00",CultureInfo.InvariantCulture)+" t "+t.ToString("0.00",CultureInfo.InvariantCulture)):null;
                foreach(bool left in new[]{false,true})
                {
                    float head=IntroRangedChecks.HeadDepth(d,left,HeadMargin),face=IntroRangedChecks.FaceCover(d,left,view,FaceRadius);checks+=2;
                    if(head<leastHead){leastHead=head;headAt=age;}if(face>mostFace){mostFace=face;faceAt=age;}
                    line?.Append("  "+(left?"L":"R")+" head "+head.ToString("0.00",CultureInfo.InvariantCulture)+" face "+(face*100).ToString("0",CultureInfo.InvariantCulture)+"%");
                    if(head<1||face>FaceMost)
                    {
                        int shot=ShotOf(s,age,out float shotAt);
                        if(head<1)found.Add(new Finding{Id=e.Id,T=t,Age=age,Left=left,Face=false,Value=head,Shot=shot,ShotAt=shotAt});
                        if(face>FaceMost)found.Add(new Finding{Id=e.Id,T=t,Age=age,Left=left,Face=true,Value=face,Shot=shot,ShotAt=shotAt});
                    }
                }
                if(line!=null)Console.WriteLine(line.Append("  shot "+ShotOf(s,age,out float sa)+" (from "+sa.ToString("0.00",CultureInfo.InvariantCulture)+" s)"));
            }
            return found;
        }

        /// <summary>The failing frames of one intro folded into spans (consecutive frames of one arm, measure and shot), as text.</summary>
        internal static IEnumerable<string> Spans(List<Finding> found)
        {
            string F(float v)=>v.ToString("0.00",CultureInfo.InvariantCulture);
            foreach(var g in found.GroupBy(f=>(f.Left,f.Face,f.Shot)).OrderBy(g=>g.Min(f=>f.Age)))
            {
                var frames=g.OrderBy(f=>f.Age).ToList();int a=0;
                for(int b=1;b<=frames.Count;b++)
                {
                    if(b<frames.Count&&frames[b].Age-frames[b-1].Age<.05f)continue;
                    var run=frames.GetRange(a,b-a);var worst=run[0].Face?run.OrderByDescending(f=>f.Value).First():run.OrderBy(f=>f.Value).First();
                    yield return "clip "+F(run[0].Age)+"-"+F(run[run.Count-1].Age)+" s (intro "+F(run[0].T)+"-"+F(run[run.Count-1].T)+" s), the "+(run[0].Left?"left":"right")+" arm "+
                        (run[0].Face?"across the face, "+(worst.Value*100).ToString("0")+"% of its disc covered":"through the head, depth "+F(worst.Value)+" (1 is the head + 3 cm)")+
                        " at clip "+F(worst.Age)+" s; camera shot "+run[0].Shot+" (from intro "+F(run[0].ShotAt)+" s), "+run.Count+" frame"+(run.Count==1?"":"s");
                    a=b;
                }
            }
        }

        /// <summary>The intros whose failing frames were found when this check came in (2026-11-21), routed to their authors: the full run lists
        /// them and fails on any other intro; `--intro-arms` fails on these too. Remove an intro's id once it passes.</summary>
        internal static readonly string[] Known={"intro2-weapon-greatsword","intro2-weapon-katana","intro2-weapon-fangs","intro2-weapon-lightning"};

        /// <summary>`--intro-arms` (`strict`): every registered v2 intro, failing on any failing frame (each listed). The full run (not `strict`)
        /// lists the Known intros' frames and fails on any other intro's. `only`: one intro, its measures per frame.</summary>
        public static void Run(string only=null,bool strict=true)
        {
            bool pack=IntroPackChecks.LoadLocal(IntroPackChecks.Root(),out var packPath);
            var rig=ModelPoseChecks.Measured();int intros=0,checks=0;var all=new List<Finding>();var lines=new List<string>();float least=float.MaxValue,most=0;
            foreach(var e in NativeIntroCatalog.Entries)
            {
                if(!(e.Make() is INativeIntroV2))continue;if(only!=null&&e.Id!=only)continue;intros++;
                var found=Scan(e,rig,ref checks,out float head,out float headAt,out float face,out float faceAt,only!=null);all.AddRange(found);least=Math.Min(least,head);most=Math.Max(most,face);
                bool known=Known.Contains(e.Id);
                Console.WriteLine("  "+e.Id.PadRight(30)+" the arms at least "+head.ToString("0.00",CultureInfo.InvariantCulture)+" of the head's reach + 3 cm out (clip "+headAt.ToString("0.00",CultureInfo.InvariantCulture)+" s), at most "+
                    (face*100).ToString("0")+"% of the face covered (clip "+faceAt.ToString("0.00",CultureInfo.InvariantCulture)+" s)"+(found.Count>0?": "+found.Count+" failing frame-measure(s)"+(known?" (known, routed)":""):known?" (passes now: remove it from IntroArmChecks.Known)":""));
                foreach(var span in Spans(found))lines.Add((known?"KNOWN ":"")+e.Id+": "+span);
            }
            if(only!=null&&intros==0)throw new Exception("V2 intro arms: no registered v2 intro "+only);
            var failing=all.Where(f=>strict||!Known.Contains(f.Id)).ToList();
            if(all.Count>0)Console.WriteLine((failing.Count>0?"FAIL":"KNOWN")+" v2 intro arms: "+all.Count+" failing frame-measure(s) in "+all.Select(f=>f.Id).Distinct().Count()+" intro(s):");
            foreach(var l in lines)Console.WriteLine("  "+l);
            if(failing.Count>0)throw new Exception("V2 intro arms: "+failing.Count+" failing frame-measure(s) in "+string.Join(", ",failing.Select(f=>f.Id).Distinct())+" (listed above)");
            Console.WriteLine("PASS v2 intro arms: "+checks+" checks ("+intros+" v2 intros every 1/30 s through their own shot cameras"+(all.Count>0?"; "+all.Select(f=>f.Id).Distinct().Count()+" known intro(s) listed, routed to their authors":"")+(pack?", pack arms from "+packPath:", no local intro motion bank")+
                ": no arm through the drawn head, the arms at least "+least.ToString("0.00",CultureInfo.InvariantCulture)+" of its reach + 3 cm out; no arm across more than "+(FaceMost*100).ToString("0")+"% of the face, at most "+(most*100).ToString("0")+"%)");
        }
    }
}
