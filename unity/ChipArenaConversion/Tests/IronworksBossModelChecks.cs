#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // THE CONVERTER, the City Ironworks boss concept (Core/NativeIronworksBossModel.cs; docs/EXPEDITION-BOSS-CITY-IRONWORKS.md):
    // the model builds at every tier within its triangle budget, deterministically, with every joint drawn; the joint table is
    // well formed; every key pose solves to finite matrices inside the reach box, the standing keys keep everything but the chains
    // above the floor and plant their feet where the pose puts them; the body stays in its drawn bounds and the vessel in the
    // collision capsule; the tells read non-orange (cyan visor, cold-white lamps, white tuyères); the glow groups follow the pose;
    // posing allocates nothing; the action table matches the doc's; and nothing in the live game references the concept.
    internal static class IronworksBossModelChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok){if(Environment.GetEnvironmentVariable("IRONWORKS_ALL")=="1"){Console.WriteLine("FAIL ironworks boss model: "+what);return;}throw new Exception("Ironworks boss model: "+what);}count++;}
        private const string DocPath="../../docs/EXPEDITION-BOSS-CITY-IRONWORKS.md";
        /// <summary>The drawn bounds of the body joints in the idle pose (model space: half width, z from and to, y from and to).</summary>
        private const float BodyX=3.5f,BodyZ0=-4.1f,BodyZ1=2.5f,BodyY0=.9f,BodyY1=8.4f;
        public static void Run(string root)
        {
            Tables();Build();Poses();Bounds();Tells();Glow();Allocation();Actions(root);Unreferenced(root);
            Console.WriteLine("PASS ironworks boss model ("+count+" checks: THE CONVERTER builds at three tiers within budget and deterministically, every joint drawn; "+NativeIronworksBossModel.Keys+" key poses solve inside the reach box, "
                +"standing keys above the floor with planted feet; body and vessel in their bounds; cyan, cold-white and white tells; glow groups; no allocation posing; the doc's action table; referenced by nothing live but its presentation)");
        }

        // ---- the joint table ----
        private static void Tables()
        {
            Check(NativeIronworksBossModel.Names.Length==NativeIronworksBossModel.Count&&NativeIronworksBossModel.Parent.Length==NativeIronworksBossModel.Count,"names and parents for every joint");
            for(int j=1;j<NativeIronworksBossModel.Count;j++)Check(NativeIronworksBossModel.Parent[j]>=0&&NativeIronworksBossModel.Parent[j]<j,"joint "+NativeIronworksBossModel.Names[j]+"'s parent precedes it");
            Check(NativeIronworksBossModel.Parent[0]==-1,"the root has no parent");
            for(int leg=0;leg<NativeIronworksBossModel.Legs;leg++)
            {
                Check(NativeIronworksBossModel.Parent[NativeIronworksBossModel.LegJoint(leg,NativeIronworksBossModel.Coxa)]==NativeIronworksBossModel.Yoke,"leg "+leg+" hangs off the yoke");
                for(int part=1;part<4;part++)Check(NativeIronworksBossModel.Parent[NativeIronworksBossModel.LegJoint(leg,part)]==NativeIronworksBossModel.LegJoint(leg,part-1),"leg "+leg+" part "+part+" chains to the one above");
            }
            for(int leg=0;leg<2;leg++)for(int w=0;w<2;w++)Check(NativeIronworksBossModel.Parent[NativeIronworksBossModel.Jaw(leg,w)]==NativeIronworksBossModel.LegJoint(leg,NativeIronworksBossModel.Foot),"the front feet carry the tong jaws");
            Check(NativeIronworksBossModel.Jaw(1,1)==NativeIronworksBossModel.Count-1,"the jaws are the last joints");
            Check(NativeIronworksBossModel.KeyNames.Length==NativeIronworksBossModel.Keys&&NativeIronworksBossModel.KeyNames.Distinct().Count()==NativeIronworksBossModel.Keys,"a distinct name per key pose");
            for(int k=0;k<NativeIronworksBossModel.Keys;k++)Check(NativeIronworksBossModel.KeyIndex(NativeIronworksBossModel.KeyNames[k])==k,"key "+NativeIronworksBossModel.KeyNames[k]+" found by name");
        }

        // ---- building ----
        private static readonly IronworksBossMesh[] tiers=new IronworksBossMesh[3];
        private static void Build()
        {
            for(int t=0;t<3;t++)
            {
                var m=tiers[t]=NativeIronworksBossModel.Build(t);int tris=m.Triangles;
                Check(tris>0&&tris<=NativeIronworksBossModel.Budget[t],"tier "+t+": "+tris+" triangles within "+NativeIronworksBossModel.Budget[t]);
                Check(m.Link.Triangles>0&&m.Hook.Triangles>0&&m.Shackle.Triangles>0,"tier "+t+" has the chain props");
                foreach(var s in m.Body.Sections){Check(s.Bone>NativeIronworksBossModel.Root&&s.Bone<NativeIronworksBossModel.Count,"tier "+t+" sections ride real joints");Check(s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),"tier "+t+" indices in range");}
                Check(m.Body.Sections.All(s=>s.Positions.All(Finite)&&s.Normals.All(Finite)),"tier "+t+" positions and normals finite");
            }
            Check(tiers[0].Triangles>tiers[1].Triangles&&tiers[1].Triangles>tiers[2].Triangles,"each tier lighter than the one before ("+tiers[0].Triangles+", "+tiers[1].Triangles+", "+tiers[2].Triangles+")");
            for(int j=1;j<NativeIronworksBossModel.Count;j++)Check(tiers[0].Body.Sections.Any(s=>s.Bone==j&&s.Triangles>0),"joint "+NativeIronworksBossModel.Names[j]+" is drawn at full detail");
            for(int j=1;j<NativeIronworksBossModel.Count;j++)Check(tiers[2].Body.Sections.Any(s=>s.Bone==j&&s.Triangles>0),"joint "+NativeIronworksBossModel.Names[j]+" is drawn at low detail");
            var again=NativeIronworksBossModel.Build(0);Check(Sum(again.Body)==Sum(tiers[0].Body)&&again.Triangles==tiers[0].Triangles,"the build is deterministic");
        }
        private static double Sum(WeaponMesh m){double s=0;int i=0;foreach(var sec in m.Sections)foreach(var p in sec.Positions){i++;s+=p.X*1.3+p.Y*.7+p.Z*1.9+i*1e-7;}return s;}
        private static bool Finite(Vector3 v)=>!float.IsNaN(v.X)&&!float.IsNaN(v.Y)&&!float.IsNaN(v.Z)&&!float.IsInfinity(v.X)&&!float.IsInfinity(v.Y)&&!float.IsInfinity(v.Z);
        private static bool Finite(Matrix4x4 m)=>Finite(new Vector3(m.M11,m.M12,m.M13))&&Finite(new Vector3(m.M21,m.M22,m.M23))&&Finite(new Vector3(m.M31,m.M32,m.M33))&&Finite(new Vector3(m.M41,m.M42,m.M43));

        // ---- the key poses ----
        private static void Poses()
        {
            var mesh=tiers[0];var joints=new Matrix4x4[NativeIronworksBossModel.Count];var links=new Matrix4x4[NativeIronworksBossModel.ChainLinkMax];var ends=new Matrix4x4[NativeIronworksBossModel.Chains];
            for(int k=0;k<NativeIronworksBossModel.Keys;k++)
            {
                string name=NativeIronworksBossModel.KeyNames[k];var p=NativeIronworksBossModel.Key(k);NativeIronworksBossModel.Solve(p,joints);
                Check(joints.All(Finite),name+": every joint solves to a finite matrix");
                float lowest=float.MaxValue;bool inside=true;string outside=null;
                foreach(var s in mesh.Body.Sections)
                {
                    if(!NativeIronworksBossModel.Visible(s.Bone,p))continue;var m=joints[s.Bone];
                    foreach(var v in s.Positions)
                    {
                        var w=Vector3.Transform(v,m);lowest=Math.Min(lowest,w.Y);
                        if(Math.Abs(w.X)>NativeIronworksBossModel.ReachX||w.Z<NativeIronworksBossModel.ReachZ0||w.Z>NativeIronworksBossModel.ReachZ1||w.Y<NativeIronworksBossModel.ReachY0||w.Y>NativeIronworksBossModel.ReachY1){inside=false;outside??=NativeIronworksBossModel.Names[s.Bone]+" at "+w;}
                    }
                }
                Check(inside,name+": every vertex inside the reach box"+(outside==null?"":" ("+outside+")"));
                int ln=NativeIronworksBossModel.ChainPose(p,joints,links,ends);Check(ln>20&&ln<=links.Length&&links.Take(ln).All(Finite)&&ends.All(Finite),name+": the chains pose ("+ln+" links)");
                if(!NativeIronworksBossModel.Standing(k))continue;
                Check(lowest>=-.03f,name+": nothing but the chains goes through the floor (lowest "+lowest.ToString("F3")+" m)");
                for(int leg=0;leg<NativeIronworksBossModel.Legs;leg++)
                {
                    var f=p.Foot(leg);var want=new Vector3(f.X,f.Y+NativeIronworksBossModel.Ankle(leg),f.Z);var got=joints[NativeIronworksBossModel.LegJoint(leg,NativeIronworksBossModel.Foot)].Translation;
                    Check(Vector3.Distance(want,got)<.01f,name+": leg "+leg+" reaches its foot target ("+Vector3.Distance(want,got).ToString("F3")+" m off)");
                    if(f.Y==0)Check(lowest<=.05f,name+": a planted foot stands on the floor");
                }
            }
            // The knees bend up and out: in the stance every knee is above its hip and outside it.
            var st=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyIdle);NativeIronworksBossModel.Solve(st,joints);
            for(int leg=0;leg<NativeIronworksBossModel.Legs;leg++)
            {
                var hip=joints[NativeIronworksBossModel.LegJoint(leg,NativeIronworksBossModel.Coxa)].Translation;var knee=joints[NativeIronworksBossModel.LegJoint(leg,NativeIronworksBossModel.Tibia)].Translation;
                Check(knee.Y>hip.Y+.5f&&Math.Abs(knee.X)>Math.Abs(hip.X),"stance: leg "+leg+"'s knee rides above and outside its hip");
            }
            // Left and right mirror in the stance (the body is symmetric about x = 0 at rest, but for the foreman's panel and the drive).
            for(int leg=0;leg<2;leg++)
            {
                var a=joints[NativeIronworksBossModel.LegJoint(leg*2,NativeIronworksBossModel.Tibia)].Translation;var b=joints[NativeIronworksBossModel.LegJoint(leg*2+1,NativeIronworksBossModel.Tibia)].Translation;
                Check(Math.Abs(a.X+b.X)<1e-3f&&Math.Abs(a.Y-b.Y)<1e-3f&&Math.Abs(a.Z-b.Z)<1e-3f,"stance: the "+(leg==0?"front":"rear")+" knees mirror");
            }
        }

        // ---- bounds: the drawn body, the vessel in the collision capsule ----
        private static void Bounds()
        {
            var mesh=tiers[0];var joints=new Matrix4x4[NativeIronworksBossModel.Count];var p=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyIdle);NativeIronworksBossModel.Solve(p,joints);
            int[] body={NativeIronworksBossModel.Yoke,NativeIronworksBossModel.Vessel,NativeIronworksBossModel.VesselHeat,NativeIronworksBossModel.WindBox,NativeIronworksBossModel.CrustFront,NativeIronworksBossModel.CrustLeft,NativeIronworksBossModel.CrustRight,NativeIronworksBossModel.CrustBack,
                NativeIronworksBossModel.Pulpit,NativeIronworksBossModel.Lever,NativeIronworksBossModel.Foreman,NativeIronworksBossModel.ForemanArms};
            foreach(var s in mesh.Body.Sections)
            {
                if(!body.Contains(s.Bone))continue;var m=joints[s.Bone];
                string bad=null;foreach(var v in s.Positions){var w=Vector3.Transform(v,m);if(!(Math.Abs(w.X)<=BodyX&&w.Z>=BodyZ0&&w.Z<=BodyZ1&&w.Y>=BodyY0&&w.Y<=BodyY1))bad??=w.ToString();}
                Check(bad==null,"idle: "+NativeIronworksBossModel.Names[s.Bone]+" inside the body's drawn bounds"+(bad==null?"":" ("+bad+")"));
            }
            // The vessel's shell and crust (the trunnion pins, the bull gear and the blast pipe, |x| over 2.1 m in the vessel frame,
            // excepted) inside the proposed collision capsule about the trunnions' vertical, at any tilt.
            foreach(float tilt in new[]{-100f,-14f,10f,80f})
            {
                var q=p;q.Tilt=tilt;NativeIronworksBossModel.Solve(q,joints);float worst=0;
                foreach(var s in mesh.Body.Sections)
                {
                    if(s.Bone!=NativeIronworksBossModel.Vessel&&(s.Bone<NativeIronworksBossModel.CrustFront||s.Bone>NativeIronworksBossModel.CrustBack))continue;var m=joints[s.Bone];
                    foreach(var v in s.Positions){if(Math.Abs(v.X)>2.1f)continue;var w=Vector3.Transform(v,m);float r=(float)Math.Sqrt(w.X*w.X+w.Z*w.Z);worst=Math.Max(worst,Math.Max(r,0));}
                }
                Check(worst<=3.3f,"tilt "+tilt+": the vessel stays within 3.3 m of the trunnions' vertical ("+worst.ToString("F2")+")");
                if(tilt==10f)Check(worst<=NativeIronworksBossModel.CapsuleRadius,"standing: the vessel inside the collision capsule's radius ("+worst.ToString("F2")+" m of "+NativeIronworksBossModel.CapsuleRadius+")");
            }
            // The deslag's back roll clears the rear arch and the pulpit: no vessel vertex within the arch beam's box at any tilt back.
            for(float tilt=0;tilt>=-100;tilt-=5)
            {
                var q=p;q.Tilt=tilt;NativeIronworksBossModel.Solve(q,joints);var inv=Matrix4x4.Invert(joints[NativeIronworksBossModel.Yoke],out var yi)?yi:Matrix4x4.Identity;bool hit=false;
                foreach(var s in mesh.Body.Sections){if(s.Bone!=NativeIronworksBossModel.Vessel&&s.Bone!=NativeIronworksBossModel.VesselHeat)continue;var m=joints[s.Bone]*inv;foreach(var v in s.Positions){var y=Vector3.Transform(v,m);if(Math.Abs(y.X)<1.9f&&y.Y>1.72f&&y.Y<2.3f&&y.Z<-3.0f&&y.Z>-3.9f)hit=true;}}
                Check(!hit,"tilt "+tilt+": the vessel clears the rear arch's beam");
            }
        }

        // ---- tells: never orange ----
        private static void Tells()
        {
            var mesh=tiers[0];
            Vector3 Brightest(int joint){var best=Vector3.Zero;foreach(var s in mesh.Body.Sections)if(s.Bone==joint)foreach(var e in s.Emission){var v=new Vector3(e.X,e.Y,e.Z);if(v.LengthSquared()>best.LengthSquared())best=v;}return best;}
            var visor=Brightest(NativeIronworksBossModel.Foreman);Check(visor.Z>visor.X*2&&visor.Y>visor.X*2,"the foreman's brightest light is cyan ("+visor+")");
            var lamps=Brightest(NativeIronworksBossModel.Yoke);Check(lamps.Z>=lamps.X*.9f&&Math.Min(lamps.X,Math.Min(lamps.Y,lamps.Z))>.7f*Math.Max(lamps.X,Math.Max(lamps.Y,lamps.Z)),"the yoke's lamps are cold white ("+lamps+")");
            var tuy=Brightest(NativeIronworksBossModel.WindBox);Check(Math.Min(tuy.X,Math.Min(tuy.Y,tuy.Z))>.65f*Math.Max(tuy.X,Math.Max(tuy.Y,tuy.Z)),"the tuyère weak point glows white, not orange ("+tuy+")");
            var seam=Brightest(NativeIronworksBossModel.Seams);Check(Math.Min(seam.X,Math.Min(seam.Y,seam.Z))>.65f*Math.Max(seam.X,Math.Max(seam.Y,seam.Z)),"the phase 3 seams glow white ("+seam+")");
            var metal=Brightest(NativeIronworksBossModel.VesselHeat);Check(metal.X>=metal.Z&&metal.Length()>Brightest(NativeIronworksBossModel.CrustFront).Length()*2,"the mouth's metal outshines the crust's seams");
        }

        // ---- glow groups and visibility ----
        private static void Glow()
        {
            var p=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyBlow);
            Check(Math.Abs(NativeIronworksBossModel.GlowOf(NativeIronworksBossModel.WindBox,p)-p.Blow)<1e-5f&&Math.Abs(NativeIronworksBossModel.GlowOf(NativeIronworksBossModel.VesselHeat,p)-p.Heat)<1e-5f,"the blow and heat groups follow the pose");
            var dead=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyDead);
            Check(NativeIronworksBossModel.GlowOf(NativeIronworksBossModel.VesselHeat,dead)==0&&NativeIronworksBossModel.GlowOf(NativeIronworksBossModel.WindBox,dead)==0&&NativeIronworksBossModel.GlowOf(NativeIronworksBossModel.Seams,dead)==0&&NativeIronworksBossModel.GlowOf(NativeIronworksBossModel.Foreman,dead)<=.25f+1e-5f,"cooled: the metal, tuyères and seams go dark, the rest dims to a quarter");
            var idle=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyIdle);
            for(int c=0;c<4;c++)Check(NativeIronworksBossModel.Visible(NativeIronworksBossModel.CrustFront+c,idle),"phase 1: crust group "+c+" on");
            Check(!NativeIronworksBossModel.Visible(NativeIronworksBossModel.Seams,idle)&&NativeIronworksBossModel.Visible(NativeIronworksBossModel.Seams,NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyRunaway)),"the seams show in phase 3 only");
            var run=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyRunaway);for(int c=0;c<4;c++)Check(!NativeIronworksBossModel.Visible(NativeIronworksBossModel.CrustFront+c,run),"phase 3: crust group "+c+" gone");
            var shed=NativeIronworksBossModel.Key(NativeIronworksBossModel.KeyShed);for(int c=0;c<4;c++)Check(NativeIronworksBossModel.Visible(NativeIronworksBossModel.CrustFront+c,shed),"the shed: crust group "+c+" flies");
            var once=idle;once.Crust=15&~2;Check(!NativeIronworksBossModel.Visible(NativeIronworksBossModel.CrustLeft,once)&&NativeIronworksBossModel.Visible(NativeIronworksBossModel.CrustRight,once),"a broken crust group hides alone");
            Check(!NativeIronworksBossModel.Visible(NativeIronworksBossModel.Root,idle),"the root draws nothing");
            Check(NativeIronworksBoss.PhaseOf(1)==1&&NativeIronworksBoss.PhaseOf(.7f)==1&&NativeIronworksBoss.PhaseOf(.69f)==2&&NativeIronworksBoss.PhaseOf(.35f)==2&&NativeIronworksBoss.PhaseOf(.34f)==3,"phases at 70% and 35%");
        }

        // ---- posing allocates nothing ----
        private static void Allocation()
        {
            var joints=new Matrix4x4[NativeIronworksBossModel.Count];var keys=new IronworksBossPose[NativeIronworksBossModel.Keys];for(int k=0;k<keys.Length;k++)keys[k]=NativeIronworksBossModel.Key(k);
            var links=new Matrix4x4[NativeIronworksBossModel.ChainLinkMax];var ends=new Matrix4x4[NativeIronworksBossModel.Chains];float g=0;
            for(int i=0;i<40;i++){NativeIronworksBossModel.Solve(keys[i%keys.Length],joints);NativeIronworksBossModel.ChainPose(keys[i%keys.Length],joints,links,ends);g+=NativeIronworksBossModel.GlowOf(i%NativeIronworksBossModel.Count,keys[i%keys.Length]);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<2000;i++){var p=keys[i%keys.Length];NativeIronworksBossModel.Solve(p,joints);NativeIronworksBossModel.ChainPose(p,joints,links,ends);g+=NativeIronworksBossModel.GlowOf(i%NativeIronworksBossModel.Count,p);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0&&g>0,"posing, chains and glow allocate nothing ("+used+" bytes)");
        }

        // ---- the action table, and the doc's copy of it ----
        private static void Actions(string root)
        {
            var a=NativeIronworksBoss.Actions;
            Check(a.Select(x=>x.Id).Distinct().Count()==a.Length&&a.Select(x=>x.Code).Distinct().Count()==a.Length,"action ids and codes are unique");
            foreach(var x in a){Check(x.Total>0&&x.From>=1&&x.From<=3&&x.Windup>=0&&x.Strike>=0&&x.Follow>=0&&x.Recover>=0&&x.Cooldown>=0,x.Id+": a sane timeline");Check(NativeIronworksBoss.Find(x.Id).Code==x.Code,x.Id+" found by id");}
            foreach(var x in a.Where(x=>x.Code>=NativeIronworksBoss.Pour&&x.Code<=NativeIronworksBoss.Charge))Check(x.Windup>=.7f&&x.Recover>=.6f,x.Id+": an attack telegraphs at least .7 s and leaves at least .6 s to punish");
            Check(NativeIronworksBoss.Find("nothing").Code==NativeIronworksBoss.None,"an unknown id finds nothing");
            string doc=Path.Combine(root,DocPath);if(!File.Exists(doc)){Console.WriteLine("SKIP ironworks boss doc table: "+DocPath+" is not in this tree");return;}
            var text=File.ReadAllText(doc);
            foreach(var x in a)
            {
                var m=Regex.Match(text,@"^\| `"+Regex.Escape(x.Id)+@"` \| (\d+) \| ([0-9.]+) \| ([0-9.]+) \| ([0-9.]+) \| ([0-9.]+) \| ([0-9.]+) \|",RegexOptions.Multiline);
                Check(m.Success,"the doc's action table lists "+x.Id);if(!m.Success)continue;float F(int g)=>float.Parse(m.Groups[g].Value,System.Globalization.CultureInfo.InvariantCulture);
                Check(int.Parse(m.Groups[1].Value)==x.From&&Math.Abs(F(2)-x.Windup)<1e-4f&&Math.Abs(F(3)-x.Strike)<1e-4f&&Math.Abs(F(4)-x.Follow)<1e-4f&&Math.Abs(F(5)-x.Recover)<1e-4f&&Math.Abs(F(6)-x.Cooldown)<1e-4f,"the doc's "+x.Id+" row is the code's");
            }
            var cues=Regex.Matches(text,@"`(boss\.ironworks\.[a-z0-9.\-]+)`").Select(m=>m.Groups[1].Value).ToArray();var listed=Regex.Matches(text,@"^\| `(boss\.ironworks\.[a-z0-9.\-]+)` \|",RegexOptions.Multiline).Select(m=>m.Groups[1].Value).ToArray();
            Check(listed.Length>=30&&listed.Distinct().Count()==listed.Length,"the doc's cue table lists "+listed.Length+" distinct boss.ironworks.* keys");
            Check(cues.All(c=>listed.Contains(c)),"every boss.ironworks.* key the doc names is in its cue table");
            Check(text.Contains("`music/city-ironworks-boss`")&&text.Contains(NativeIronworksBoss.PlateName)&&text.Contains(NativeIronworksBoss.Archetype),"the doc names the boss track, the plate and the archetype");
        }

        // ---- nothing live references the concept ----
        /// <summary>The concept's type names (NativeIronworksBoss covers NativeIronworksBossModel).</summary>
        private static readonly string[] Names={"NativeIronworksBoss","IronworksBossPose","IronworksBossMesh"};
        private static readonly string[] TextKinds={".cs",".json",".shader",".cginc",".hlsl",".asset",".unity",".prefab",".mat",".asmdef",".txt",".md"};
        // Presentation only: besides the model, THE CONVERTER's presentation (docs "Presentation build": the runtime model, rig,
        // moves, effects, cues, preview, stage hook, view, the HUD's zone boss names, the runtime presenter and its audio) names it;
        // no rule, authority, network or content file does.
        private static readonly string[] Presentation={"NativeIronworksBossLook.cs","NativeIronworksBossMoves.cs","NativeIronworksBossRig.cs","NativeIronworksBossFx.cs","NativeIronworksBossCues.cs",
            "NativeIronworksBossPreview.cs","NativeIronworksBossStage.cs","NativeIronworksBossView.cs","NativeZoneBossNames.cs","NativeIronworksBossPresentation.cs","NativeIronworksBossAudio.cs","NativeIronworksBossLifetime.cs"};
        private static void Unreferenced(string root)
        {
            string self=Path.Combine(root,"Assets/ChipArena/Core/NativeIronworksBossModel.cs");
            var hits=Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories)
                .Where(p=>TextKinds.Contains(Path.GetExtension(p))&&!p.Replace('\\','/').Contains("/LocalAssets/")&&Path.GetFullPath(p)!=Path.GetFullPath(self)&&!Presentation.Contains(Path.GetFileName(p)))
                .Where(p=>{var t=File.ReadAllText(p);return Names.Any(n=>t.Contains(n,StringComparison.Ordinal))||t.Contains(NativeIronworksBoss.Archetype,StringComparison.Ordinal);}).ToArray();
            Check(hits.Length==0,"nothing under Assets/ but the model and its presentation names the concept or its archetype ("+string.Join(", ",hits.Select(Path.GetFileName))+")");
        }
    }
}
#endif
