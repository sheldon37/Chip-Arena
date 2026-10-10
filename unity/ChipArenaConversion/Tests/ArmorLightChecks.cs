#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Pilot armor lights (Core/NativeArmorLight.cs, Resources/NativeArmorLight.cginc, Runtime/NativeArmorLights.cs). Owner
    // (2026-10-10): the lights on the armour pieces looked pasted on; they should blend into the armour and look real.
    // Checked on the real exported pilot: eleven emitters (accent trims and armor strips) shape their own voxel boxes; the
    // parts around them take the right emitters (ribcage: sternum, back light, both caps; forearm: its cuff; upper arm: its
    // cap; thigh: its kneecap, never the waist band across the hip hinge); no hinge emitter sits farther than HingeGap from
    // its pivot; the near field and window stay in range; the held weapons light only from their own hand's cuff; quality and
    // pulse follow the combat glow and reduced-effects settings; and the shader hooks stay minimal: NativeSourceLit keeps its
    // surf, StageTone and the Siren ink untouched and adds one include, one wrapper and one StageTone line.
    internal static class ArmorLightChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Armor light: "+what);count++;}
        private static List<NativeArmorLight.Section> Pilot(string root)
        {
            var env=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json"))).RootElement;
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            var pilot=JsonDocument.Parse(reader.ReadToEnd()).RootElement.GetProperty("models").EnumerateArray().First(m=>m.GetProperty("id").GetString()=="pilot");
            var list=new List<NativeArmorLight.Section>();
            foreach(var s in pilot.GetProperty("sections").EnumerateArray())
            {
                var p=s.GetProperty("positions").EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(p.Length==0)continue;
                var e=s.GetProperty("emission").EnumerateArray().Select(x=>x.GetSingle()).ToArray();var c=s.GetProperty("colors").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
                var sec=new NativeArmorLight.Section{Part=s.GetProperty("part").GetString(),Bone=s.GetProperty("bone").GetInt32()};sec.Emitter=e[3]>1.5f&&e[0]+e[1]+e[2]>0;sec.Level=sec.Emitter?e[1]/c[1]:0;
                int n=p.Length/3;var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);for(int i=0;i<n;i++){var v=new Vector3(p[i*3],p[i*3+1],p[i*3+2]);min=Vector3.Min(min,v);max=Vector3.Max(max,v);}sec.Min=min;sec.Max=max;
                // The runtime's split (Runtime/NativeArmorLights.cs ArmorSection): 24-vertex box chunks.
                bool boxes=n%24==0;for(int st=0;boxes&&st<n;st+=24){var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);for(int i=st;i<st+24;i++){var v=new Vector3(p[i*3],p[i*3+1],p[i*3+2]);lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);}
                    for(int i=st;i<st+24&&boxes;i++)for(int k=0;k<3;k++){float v=p[i*3+k],a=k==0?lo.X:k==1?lo.Y:lo.Z,b=k==0?hi.X:k==1?hi.Y:hi.Z;if(Math.Abs(v-a)>1e-5f&&Math.Abs(v-b)>1e-5f){boxes=false;break;}}
                    if(boxes)sec.Voxels.Add((lo,hi));}
                if(!boxes){sec.Voxels.Clear();sec.Voxels.Add((min,max));}
                list.Add(sec);
            }
            return list;
        }
        private static bool From(NativeArmorLight.Plan plan,NativeArmorLight.Section emitter,int receiverBone)
        {
            foreach(var e in NativeArmorLight.Merge(emitter.Voxels,emitter.Level))
            {var at=e.Center+NativeArmorLight.Rest(emitter.Bone)-NativeArmorLight.Rest(receiverBone);if(plan.Boxes.Any(b=>Vector3.Distance(b.Center,at)<1e-4f))return true;}
            return false;
        }
        public static void Run(string root)
        {
            var sections=Pilot(root);var plans=NativeArmorLight.Build(sections);
            NativeArmorLight.Section S(string part)=>sections.First(s=>s.Part==part);NativeArmorLight.Plan P(string part)=>plans[sections.IndexOf(S(part))];
            Check(sections.Count(s=>s.Emitter)==11&&plans.Count(p=>p.Role==NativeArmorLight.Emitter)==11,"eleven emitters");
            Check(plans.Count(p=>p.Role==NativeArmorLight.Receiver)==sections.Count(s=>!s.Emitter)&&plans.Count(p=>p.Role==NativeArmorLight.Receiver)>=37&&plans.All(p=>p.Role!=NativeArmorLight.Off),"every other pilot part is a receiver");
            foreach(var s in sections.Where(s=>s.Emitter))Check(Math.Abs(s.Level-1.4f)<.03f&&!s.Part.StartsWith("armor-",StringComparison.Ordinal)||Math.Abs(s.Level-2.4f)<.03f&&s.Part.StartsWith("armor-",StringComparison.Ordinal),"GLOW levels: "+s.Part+" "+s.Level);
            Check(NativeArmorLight.Merge(S("left-shoulder-cap").Voxels,1.4f).Count==1&&NativeArmorLight.Merge(S("waist-band").Voxels,1.4f).Count==1&&NativeArmorLight.Merge(S("left-cuff").Voxels,1.4f).Count==1,"caps, band and cuffs light as one box");
            Check(NativeArmorLight.Merge(S("armor-back-light").Voxels,2.4f).Count==2&&NativeArmorLight.Merge(S("sternum").Voxels,1.4f).Count>=3,"the back light's T and the sternum's bars stay apart");
            var rib=P("ribcage");Check(rib.Boxes.Length==NativeArmorLight.MaxBoxes&&From(rib,S("sternum"),CapturedRig.Chest)&&From(rib,S("armor-back-light"),CapturedRig.Chest)&&From(rib,S("left-shoulder-cap"),CapturedRig.Chest)&&From(rib,S("right-shoulder-cap"),CapturedRig.Chest),"ribcage: sternum, back light and both caps");
            Check(P("left-forearm").Boxes.Length==1&&From(P("left-forearm"),S("left-cuff"),CapturedRig.LElbow)&&From(P("right-forearm"),S("right-cuff"),CapturedRig.RElbow),"forearms: their own cuff");
            Check(From(P("left-upper-arm"),S("left-shoulder-cap"),CapturedRig.LArm)&&P("left-upper-arm").Boxes.Length==1,"upper arm: its cap");
            Check(P("left-thigh").Boxes.Length==1&&From(P("left-thigh"),S("left-kneecap"),CapturedRig.LHip)&&!From(P("left-thigh"),S("waist-band"),CapturedRig.LHip),"thigh: its kneecap, never the band across the hip hinge");
            Check(From(P("hip-shell"),S("waist-band"),CapturedRig.Pelvis)&&From(P("abdomen-shell"),S("waist-band"),CapturedRig.Spine),"waist band lights the hips and the abdomen");
            // Review: the head shell skipped ARMOR_SHELL and the arena shoulder (brighter than the collar beside it). Parts with no
            // trim in reach are receivers with no boxes: no spill, the same shell scale and tone as the rest of the armour.
            foreach(var part in new[]{"blank-head","crown","jaw","left-foot","right-foot","left-heel"})Check(P(part).Role==NativeArmorLight.Receiver&&P(part).Boxes.Length==0&&P(part).SeverJoints==0,part+": a receiver with no boxes");
            // No pose leaves a phantom light: a neighbour's emitter is a parent or child, and across a hinge it reaches the pivot.
            int hinged=0;
            for(int i=0;i<sections.Count;i++)if(plans[i].Role==NativeArmorLight.Receiver)foreach(var b in plans[i].Boxes)
            {
                Check(b.Sigma>=NativeArmorLight.SigmaMin-1e-6f&&b.Sigma<=NativeArmorLight.SigmaMax+1e-6f&&b.Level>1&&b.Level<2.5f,"near field "+sections[i].Part);
                bool found=false;
                foreach(var e in sections.Where(x=>x.Emitter))foreach(var m in NativeArmorLight.Merge(e.Voxels,e.Level))
                {
                    var at=m.Center+NativeArmorLight.Rest(e.Bone)-NativeArmorLight.Rest(sections[i].Bone);if(Vector3.Distance(at,b.Center)>1e-4f)continue;found=true;
                    if(e.Bone==sections[i].Bone)continue;int parent=CapturedRig.Bones[sections[i].Bone].Parent;
                    Check(e.Bone==parent||CapturedRig.Bones[e.Bone].Parent==sections[i].Bone,"neighbour bones only: "+sections[i].Part+" <- "+e.Part);
                    int hinge=e.Bone==parent?sections[i].Bone:e.Bone;if(NativeArmorLight.Stiff(hinge))continue;hinged++;
                    var pivot=NativeArmorLight.Rest(hinge);var c=m.Center+NativeArmorLight.Rest(e.Bone);var gap=Vector3.Max(Vector3.Zero,Vector3.Max(c-m.Half-pivot,pivot-c-m.Half)).Length();
                    Check(gap<=NativeArmorLight.HingeGap+1e-5f,"hinge emitter at its pivot: "+sections[i].Part+" <- "+e.Part+" "+gap);
                }
                Check(found,"every receiver box is an emitter's: "+sections[i].Part);
            }
            Check(hinged>=8,"cap, cuff and kneecap reach across their hinges");
            foreach(int hand in new[]{CapturedRig.LHand,CapturedRig.RHand}){var h=NativeArmorLight.HeldPlan(sections,hand);Check(h.Role==NativeArmorLight.Held&&h.Boxes.Length==1&&Math.Abs(h.Boxes[0].Center.Y-.015f)<.005f,"held weapon: its hand's cuff");}
            Check(NativeArmorLight.HandHeld("weapon-rifle")&&NativeArmorLight.HandHeld("weapon-shield")&&!NativeArmorLight.HandHeld("weapon-sword")&&!NativeArmorLight.HandHeld("weapon-fire")&&!NativeArmorLight.HandHeld("loose-rifle"),"only plain-hand weapons hold the cuff light");
            Check(NativeArmorLight.Quality(1)==1&&NativeArmorLight.Quality(.4f)==.5f&&NativeArmorLight.Quality(.3f)==.5f&&NativeArmorLight.Quality(0)==.5f,"quality from combat glow");
            Check(NativeArmorLight.PulseFor(1)==NativeArmorLight.Pulse&&NativeArmorLight.PulseFor(.4f)==NativeArmorLight.Pulse&&NativeArmorLight.PulseFor(.3f)==0&&NativeArmorLight.PulseFor(0)==0&&NativeArmorLight.Pulse<=.06f,"pulse off with reduced effects");
            // Shader hooks: minimal and leaving the Siren ink, StageTone's stage block and every other material alone.
            string lit=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeSourceLit.shader")),inc=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeArmorLight.cginc"));
            Check(lit.Contains("#pragma surface surfArmor Lambert")&&lit.Contains("finalcolor:StageTone")&&lit.Contains("#include \"NativeArmorLight.cginc\"")&&lit.Contains("if(ArmorTone(color,i.emission.w,_StageLit))return;")&&lit.Contains("surf(i,o);if(_InkFill>.5)return;"),"NativeSourceLit hooks");
            Check(lit.Contains("o.Emission+=_Ink*float3(.62,.56,.82)*(rim*_InkRim+smoothstep(.7,1,rise)*_InkFlow)"),"Siren ink branch untouched");
            Check(inc.Contains("if(_ArmorLight<.5||_ArmorLight>2.5||_ArmorQuality<=0)return false;")&&inc.Contains("if(_ArmorLight<.5||_ArmorQuality<=0||(_ArmorLight>2.5&&held<1.5))return;"),"off materials keep their exact look");
            Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeArmorLight.cginc.meta"))&&File.Exists(Path.Combine(root,"Assets/ChipArena/Core/NativeArmorLight.cs.meta"))&&File.Exists(Path.Combine(root,"Assets/ChipArena/Runtime/NativeArmorLights.cs.meta")),"metas");
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))if(!file.EndsWith("NativeArmorLights.cs",StringComparison.Ordinal))Check(!File.ReadAllText(file).Contains("\"_ArmorLight\""),"only the bank sets armor roles: "+Path.GetFileName(file));
            string glow=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCombatGlow.cs"));
            Check(glow.Contains("NativeArmorLights.Apply(Quality);")&&glow.Contains("ArmorHaloScale=.62f")&&glow.Contains("spill.Offer(pilot.Seat.id,i,(rig.Bones[CapturedRig.Chest].position-eye).magnitude);")&&glow.Contains("spill.Resolve(spillDt);"),"halos tightened, spill lights to the two nearest, held steady");
            Final(root,sections,plans,lit,inc,glow);
            Console.WriteLine("PASS Armor lights ("+count+" checks: 11 emitters and their voxel profiles, receivers on the real pilot, hinge-safe neighbours, held weapons, quality and pulse settings, minimal shader hooks, shell/stage/sternum levels, halo facing and pixel band, hue-true pools, untouched ink, Siren, hologram and portrait paths)");
        }
        // Review: every corpse lost its cap, kneecap and band spill on the death frame. Only a part whose boxes cross a torn joint
        // skips them; an ordinary ragdoll (joints locked within .08 m, shrunk to .85 as it sinks) keeps them.
        private static void Severed(List<NativeArmorLight.Section> sections,NativeArmorLight.Plan[] plans)
        {
            NativeArmorLight.Plan P(string part)=>plans[sections.FindIndex(s=>s.Part==part)];
            Check(P("left-upper-arm").SeverJoints==1<<CapturedRig.LArm&&P("right-upper-arm").SeverJoints==1<<CapturedRig.RArm,"upper arm: only the shoulder joint gates its cap light");
            Check(P("left-thigh").SeverJoints==1<<CapturedRig.LKnee&&P("left-hip-joint").SeverJoints==1<<CapturedRig.LKnee&&P("right-thigh").SeverJoints==1<<CapturedRig.RKnee,"thigh: only the knee gates its kneecap light (a leg thrown at the hip keeps it)");
            Check(P("abdomen-shell").SeverJoints==1<<CapturedRig.Spine&&P("abdomen").SeverJoints==1<<CapturedRig.Spine,"abdomen: only the spine joint gates the band light");
            Check(P("left-forearm").SeverJoints==0&&P("ribcage").SeverJoints==0&&P("hip-shell").SeverJoints==0&&plans.Where(p=>p.Role==NativeArmorLight.Emitter).All(p=>p.SeverJoints==0),"parts with no severable box are never gated");
            for(int i=0;i<plans.Length;i++){int mask=0;foreach(var b in plans[i].Boxes)if(b.Severs){Check(NativeArmorLight.Severable(b.Joint),"a Severs box names its severable joint: "+sections[i].Part);mask|=1<<b.Joint;}else Check(b.Joint<0||!NativeArmorLight.Severable(b.Joint),"a kept box crosses no severable joint: "+sections[i].Part);Check(mask==plans[i].SeverJoints,"SeverJoints matches the boxes: "+sections[i].Part);}
            Check(!NativeArmorLight.Detached(.08f,1)&&NativeArmorLight.Detached(.2f,1)&&NativeArmorLight.Detached(NativeArmorLight.DetachGap+.001f,1),"a locked ragdoll joint holds; a thrown limb is torn past DetachGap");
            // A shrinking corpse scales each part about its own ragdoll joint: the parent's held point moves by (1 - s) x the
            // distance from that part's joint (its nearest severable or pelvis ancestor) to the child; the slack covers it.
            float reach=0;
            for(int j=0;j<CapturedRig.Bones.Length;j++)
            {
                if(!NativeArmorLight.Severable(j))continue;int a=CapturedRig.Bones[j].Parent;while(a>CapturedRig.Pelvis&&!NativeArmorLight.Severable(a))a=CapturedRig.Bones[a].Parent;
                reach=Math.Max(reach,Vector3.Distance(NativeArmorLight.Rest(j),NativeArmorLight.Rest(a)));
                Check(Vector3.Distance(NativeArmorLight.RestLocal(j),Vector3.Zero)>0&&Math.Abs(NativeArmorLight.RestLocal(j).X+CapturedRig.Bones[j].Position.X)<1e-6f,"rest offset of joint "+j+" in its parent, Unity frame");
            }
            Check(reach<=NativeArmorLight.DetachSlack&&!NativeArmorLight.Detached(.07f+(1-.85f)*reach,.85f)&&!NativeArmorLight.Detached(.07f+(1-.3f)*reach,.3f)&&NativeArmorLight.Detached(.9f,.85f),"sink shrink and collapse never read as a tear ("+reach+" m reach)");
        }
        // Review: the nearest-two pick swapped the pools between pilots at about the same distance every few frames.
        private static void Picker()
        {
            const float dt=1/60f;float ramp=NativeArmorLight.SpillRamp;
            Check(NativeArmorLight.SpillMargin>=.1f&&NativeArmorLight.SpillMarginMin>=.3f&&ramp>=.1f&&ramp<=.2f&&NativeArmorSpillPicker.Slots==2,"hand-off margin and fade");
            // Pilot A at 3 m holds slot one; B and C stand side by side about 6 m out, run bob and an orbiting camera crossing
            // their order every few frames.
            var pick=new NativeArmorSpillPicker();int naive=0,lastNaive=-1,changes=0;var lit=new HashSet<int>();var prev=new float[2];float maxStep=0;
            for(int f=0;f<1200;f++)
            {
                float t=f*dt,b=6+.05f*(float)Math.Sin(t*13.8f)+.25f*(float)Math.Sin(t*1.1f),c=6+.05f*(float)Math.Sin(t*13.8f+2)-.25f*(float)Math.Sin(t*1.1f);
                pick.Begin();pick.Offer(1,0,3);pick.Offer(2,1,b);pick.Offer(3,2,c);pick.Resolve(dt);
                int second=b<c?2:3;if(lastNaive>=0&&second!=lastNaive)naive++;lastNaive=second;
                var now=new HashSet<int>();for(int k=0;k<2;k++){if(pick.Index(k)>=0&&pick.Weight(k)>0)now.Add(pick.Seat(k));maxStep=Math.Max(maxStep,Math.Abs(pick.Weight(k)-prev[k]));prev[k]=pick.Weight(k);}
                if(f>30&&!now.SetEquals(lit))changes++;lit=now;
            }
            Check(naive>=6,"the scenario reorders the pair ("+naive+" nearest-two swaps)");
            Check(changes==0&&lit.Contains(1)&&lit.Count==2,"the pools stay on their pilots ("+changes+" hand-offs)");
            Check(maxStep<=1.5f*dt/ramp+1e-4f,"pool strength changes smoothly ("+maxStep+" per frame)");
            // A challenger clearly nearer takes the light: the slot fades out, then in, never two pilots at once on one light.
            pick.Clear();int fadeFrames=0,takeAt=-1;bool sawDark=false;float fadeC=-1,edge=6-Math.Max(NativeArmorLight.SpillMargin*6,NativeArmorLight.SpillMarginMin);
            for(int f=0;f<400;f++)
            {
                float c=Math.Max(2.5f,7-f*.02f);pick.Begin();pick.Offer(1,0,3);pick.Offer(2,1,6);pick.Offer(3,2,c);pick.Resolve(dt);
                int slot=pick.Seat(0)==1?1:0;
                if(f==60)Check(pick.Seat(slot)==2&&pick.Weight(slot)>.999f,"cold start: the nearest two, faded in within a ramp");
                if(takeAt<0&&pick.Seat(slot)==3)takeAt=f;
                if(f>60&&pick.Seat(slot)==2&&pick.Weight(slot)<.999f){if(fadeC<0)fadeC=c;fadeFrames++;if(pick.Weight(slot)<=1e-6f)sawDark=true;}
            }
            Check(fadeC<edge&&fadeC>edge-.05f,"the hand-off starts only once the challenger is nearer by the margin (at "+fadeC+" m)");
            Check(takeAt>0&&sawDark&&fadeFrames<=ramp/dt+2,"a clearly nearer pilot takes the light after it fades out ("+fadeFrames+" frames)");
            // A holder that dies drops at once; a repeated render of the same frame changes nothing.
            pick.Clear();for(int f=0;f<30;f++){pick.Begin();pick.Offer(1,0,3);pick.Offer(2,1,4);pick.Offer(3,2,9);pick.Resolve(dt);}
            pick.Begin();pick.Offer(1,0,3);pick.Offer(3,2,9);pick.Resolve(dt);
            Check((pick.Seat(0)==3&&pick.Weight(0)==0||pick.Seat(1)==3&&pick.Weight(1)==0)&&pick.Index(0)!=1&&pick.Index(1)!=1,"a dead holder's light drops at once and passes on");
            float w0=pick.Weight(0),w1=pick.Weight(1);pick.Begin();pick.Offer(1,0,3);pick.Offer(3,2,9);pick.Resolve(0);
            Check(pick.Weight(0)==w0&&pick.Weight(1)==w1,"dt 0 holds the state");
            pick.Begin();pick.Resolve(dt);Check(pick.Index(0)<0&&pick.Index(1)<0&&pick.Weight(0)==0&&pick.Weight(1)==0,"no lit pilots: both lights off");
        }
        private static float Define(string inc,string name)
        {
            var m=System.Text.RegularExpressions.Regex.Match(inc,@"#define "+name+@"\s+([0-9.]+)");if(!m.Success)throw new Exception("Armor light: missing #define "+name);
            return float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
        }
        private static string Read(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        // The final spec's levels, halos, pools and the untouched paths (owner, 2026-10-10).
        private static void Final(string root,List<NativeArmorLight.Section> sections,NativeArmorLight.Plan[] plans,string lit,string inc,string glow)
        {
            // Shader constants mirrored in Core.
            Check(Math.Abs(Define(inc,"ARMOR_SHELL")-NativeArmorLight.Shell)<1e-6f&&NativeArmorLight.Shell==.80f,"SHELL .80 in the cginc and Core");
            Check(Math.Abs(Define(inc,"ARMOR_STAGE_SPILL")-NativeArmorLight.StageSpill)<1e-6f&&NativeArmorLight.StageSpill==.6f,"STAGE_SPILL .6 in the cginc and Core");
            Check(NativeArmorLight.SternumBoost==1.15f&&inc.Contains("the sternum's 1.15, Core NativeArmorLight.SternumBoost"),"SternumBoost 1.15 in Core, named by the cginc");
            Check(inc.Contains("s.x*ARMOR_SPILL*(stage>.5?ARMOR_STAGE_SPILL:1)")&&lit.Contains("glow.w,_StageLit);"),"stage spill only on _StageLit draws, diffuse term only");
            Check(inc.Contains("gain=lerp(ARMOR_EDGE,ARMOR_CORE,core)*light/max(_ArmorLevel,1e-4);")&&inc.Contains("light=_ArmorEmit[best].w;"),"an emitter face burns at its box's light level over its baked level");
            Check(inc.Contains("if(tag>.5&&tag<1.5)o.Emission*=ARMOR_SHELL;"),"SHELL scales the shell (tag 1) only");
            // Sternum: its own boxes and every receiver box from it carry 1.15 x its baked 1.4; nothing else is lifted.
            var sternum=sections.First(s=>s.Part==NativeArmorLight.Sternum);var sp=plans[sections.IndexOf(sternum)];
            Check(Math.Abs(sp.Level-1.4f)<.03f&&sp.Boxes.Length>=3&&sp.Boxes.All(b=>Math.Abs(b.Level-1.4f*1.15f)<.03f),"sternum: baked 1.4, light 1.61");
            var rib=plans[sections.FindIndex(s=>s.Part=="ribcage")];
            Check(rib.Boxes.Count(b=>Math.Abs(b.Level-1.61f)<.03f)>=3&&rib.Boxes.Count(b=>Math.Abs(b.Level-1.4f)<.03f)==2&&rib.Boxes.Count(b=>Math.Abs(b.Level-2.4f)<.03f)==2,"ribcage: sternum boxes 1.61, caps 1.4, back light 2.4");
            foreach(var s in sections.Where(x=>x.Emitter&&x.Part!=NativeArmorLight.Sternum))Check(Math.Abs(NativeArmorLight.Light(s)-s.Level)<1e-6f,"only the sternum is lifted: "+s.Part);
            // The shell premise: tag 1 shell voxels self-glow at GLOW.body (~.24 x colour), accents 1.4, strips 2.4, joints 0.
            var env=JsonDocument.Parse(Read(root,"Assets/Conversion/Resources/SourceModels.json")).RootElement;
            using(var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress))using(var reader=new StreamReader(zip))
            {
                var pilot=JsonDocument.Parse(reader.ReadToEnd()).RootElement.GetProperty("models").EnumerateArray().First(m=>m.GetProperty("id").GetString()=="pilot");int shells=0,joints=0;
                foreach(var s in pilot.GetProperty("sections").EnumerateArray())
                {
                    var e=s.GetProperty("emission").EnumerateArray().Select(x=>x.GetSingle()).ToArray();var c=s.GetProperty("colors").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
                    if(e.Length<4)continue;float ratio=c[1]>1e-4f?e[1]/c[1]:0;
                    if(e[3]>.5f&&e[3]<1.5f){shells++;Check(ratio>.2f&&ratio<.26f,"shell self-glow "+s.GetProperty("part").GetString());}
                    else if(e[3]<.5f){joints++;Check(ratio==0,"joints do not glow "+s.GetProperty("part").GetString());}
                    for(int i=3;i<e.Length;i+=4)if(e[i]!=e[3]){Check(false,"one emission tag per section "+s.GetProperty("part").GetString());break;}
                }
                Check(shells>=15&&joints>=15,"pilot tags: "+shells+" shell and "+joints+" joint sections");
            }
            // Severed limbs: the boxes across a severable joint (the wound parts) are flagged and skipped by corpse draws.
            NativeArmorLight.Plan P(string part)=>plans[sections.FindIndex(s=>s.Part==part)];
            Check(plans.Sum(p=>p.Boxes.Count(b=>b.Severs))==8,"eight severable-joint boxes: "+plans.Sum(p=>p.Boxes.Count(b=>b.Severs)));
            foreach(var part in new[]{"left-upper-arm","right-upper-arm","left-thigh","right-thigh","left-hip-joint","right-hip-joint"})Check(P(part).Boxes.Length==1&&P(part).Boxes[0].Severs,part+": its cap or kneecap leaves with the other side of a severable joint");
            Check(P("abdomen-shell").Boxes.Count(b=>b.Severs)==1&&P("abdomen").Boxes.Count(b=>b.Severs)==1,"abdomen: the waist band across the spine joint");
            foreach(var part in new[]{"left-forearm","right-forearm","left-elbow-joint","ribcage","collar","neck-joint","left-shin","hip-shell"})Check(P(part).Boxes.All(b=>!b.Severs),part+": kept on corpses (no severable joint between)");
            Check(NativeArmorLight.HeldPlan(sections,CapturedRig.LHand).Boxes.All(b=>!b.Severs)&&NativeArmorLight.CorpseGlow.W<1&&NativeArmorLight.HeldGlow.W>1,"held weapons are never gated; corpse glow.w below 1");
            Check(inc.Contains("if(sigma<0){if(severed)continue;sigma=-sigma;}")&&inc.Contains("ArmorSpill(p,n,v,full,held<1)"),"the shader skips severable-joint boxes on corpse draws");
            Check(Read(root,"Assets/ChipArena/Runtime/NativeArmorLights.cs").Contains("b.Severs?-b.Sigma:b.Sigma")&&Read(root,"Assets/ChipArena/Runtime/NativeKillModels.cs").Contains("Color.white,(severs&torn)!=0?CorpseGlow:Vector4.one,new Vector4(ac.r,ac.g,ac.b,1)"),"corpse and regeneration draws (QueueShadowless) pass the corpse glow only to parts across a torn joint");
            Severed(sections,plans);
            // Halo facing and the pixel band.
            Check(NativeArmorLight.HaloFacing(-1)==0&&NativeArmorLight.HaloFacing(-.10f)==0&&Math.Abs(NativeArmorLight.HaloFacing(.45f)-1)<1e-6f&&NativeArmorLight.HaloFacing(1)==1,"halo facing: off past -.10, full from .45");
            float prev=-1;for(float d=-1;d<=1.0001f;d+=.05f){float f=NativeArmorLight.HaloFacing(d);Check(f>=prev-1e-6f&&f>=0&&f<=1,"halo facing monotonic at "+d);prev=f;}
            float Px(float size,float depth,float fov,float h)=>size*h/(2*Math.Max(.05f,depth)*(float)Math.Tan(fov*Math.PI/360));
            Check(Px(NativeArmorLight.HaloPixelClamp(.75f*.62f,80,60,1080),80,60,1080)>=4.5f-1e-3f,"far chest halo keeps at least 4.5 px");
            Check(Math.Abs(Px(NativeArmorLight.HaloPixelClamp(.38f*.62f,70,60,1080),70,60,1080)-4.5f)<1e-3f,"far knee halo is lifted to 4.5 px");
            Check(Math.Abs(Px(NativeArmorLight.HaloPixelClamp(.75f*.62f,1.2f,60,1080),1.2f,60,1080)-70)<1e-3f,"killcam chest halo capped at 70 px");
            Check(NativeArmorLight.HaloPixelClamp(.75f*.62f,10,60,1080)==.75f*.62f,"mid-range halos keep their world size");
            Check(NativeArmorLight.HaloPixelClamp(.2f,0,60,1080)>0&&NativeArmorLight.HaloPixelClamp(.2f,-3,60,1080)>0&&NativeArmorLight.HaloPixelClamp(.2f,5,60,0)>0,"degenerate depth and screen stay finite");
            for(float depth=.1f;depth<100;depth*=1.7f){float p=Px(NativeArmorLight.HaloPixelClamp(.52f*.62f,depth,75,1440),depth,75,1440);Check(p>=4.5f-1e-3f&&p<=70+1e-3f,"pixel band at "+depth+" m: "+p+" px");}
            // Hue-true pools: brightest channel 1, the ember's channel ratios, pale accents keep a hue.
            foreach(var accent in new[]{"#70e1d0","#e8202c","#ffd21a","#9b6bff","#ff7a2f","#ffb3d1","#e8eef5","#3ddc84"})
            {
                var e=NativePortraitRules.Ember(NativePortraitRules.Parse(accent,NativePortraitRules.ExportAccent));var c=NativeArmorLight.SpillColor(e);float max=Math.Max(c.X,Math.Max(c.Y,c.Z)),min=Math.Min(c.X,Math.Min(c.Y,c.Z));
                Check(Math.Abs(max-1)<1e-5f&&Math.Abs(c.X*Math.Max(e.X,Math.Max(e.Y,e.Z))-e.X)<1e-5f&&Math.Abs(c.Z*Math.Max(e.X,Math.Max(e.Y,e.Z))-e.Z)<1e-5f,"pool colour is the ember at full brightness: "+accent);
                Check(min<.8f,"pool colour keeps a hue (not white): "+accent+" min "+min);
            }
            Check(NativeArmorLight.SpillColor(Vector3.Zero)==Vector3.One,"black ember falls back to white");
            // NativeCombatGlow: two pooled lights reused, nearest two, quality gated, the halo deltas and the first-person scale.
            Check(glow.Contains("armorLights=new Light[2]")&&glow.Contains("Disable(armorLights)")&&System.Text.RegularExpressions.Regex.Matches(glow,"AddComponent<Light>").Count==1,"armor spill: two pooled lights, created once, disabled each frame");
            Check(glow.Contains("if(Quality>=.5f)for(int k=0;k<NativeArmorSpillPicker.Slots;k++)")&&glow.Contains("NativeArmorLight.SpillColor(")&&glow.Contains("ArmorSpillIntensity*pulse*spill.Weight(k),ArmorSpillRange);")&&glow.Contains("ArmorSpillIntensity=4.2f,ArmorSpillRange=2.4f;"),"pools: quality .5+, hue-true colour, 4.2 x pulse x hand-off fade, range 2.4");
            Picker();
            Check(glow.Contains("NativeArmorLight.HaloFacing(Vector3.Dot(t.TransformDirection(0,0,side),(eye-at).normalized))")&&glow.Contains(".75f,1);")&&glow.Contains(".62f,-1);"),"front and back chest halos fade by facing");
            Check(System.Text.RegularExpressions.Regex.Matches(glow,"NativeArmorLight.HaloPixelClamp\\(").Count==2&&glow.Contains(".13f*ArmorHaloScale")&&glow.Contains(".45f*pulse*ArmorHaloScale"),"every armour halo in the pixel band; first-person hand halos x .62");
            Check(glow.Contains("view.Halo(viewStaff.Value.MultiplyPoint3x4(new Vector3(0,0,.72f)),right,up,.28f,School(arena.ViewPilot.Pose.MagicVisual),.55f);"),"the staff halo is unchanged");
            Check(glow.Contains("Owner")&&glow.Contains("ArmorHaloScale 1, ArmorSpillIntensity 8 and ArmorSpillRange 3.6"),"owner rollback documented");
            // Globals: written only on change, no allocation; every uniform the cginc reads is set under its exact name.
            string lights=Read(root,"Assets/ChipArena/Runtime/NativeArmorLights.cs");int at=lights.IndexOf("public static void Apply(",StringComparison.Ordinal);string apply=lights.Substring(at,lights.IndexOf("\n        }",at,StringComparison.Ordinal)-at);
            Check(!apply.Contains("new ")&&apply.Contains("if(q!=quality)")&&apply.Contains("if(p!=pulse)"),"Apply caches and allocates nothing");
            foreach(var u in new[]{"_ArmorLight","_ArmorLevel","_ArmorCount","_ArmorPhase","_ArmorEmit","_ArmorSize","_ArmorExportEmber","_ArmorQuality","_ArmorPulse"})
                Check(inc.Contains(" "+u+";")||inc.Contains(" "+u+"[8];"),"cginc declares "+u);
            foreach(var u in new[]{"_ArmorLight","_ArmorLevel","_ArmorCount","_ArmorPhase","_ArmorEmit","_ArmorSize","_ArmorExportEmber","_ArmorQuality","_ArmorPulse"})Check(lights.Contains("Shader.PropertyToID(\""+u+"\")"),"runtime sets "+u);
            Check(!inc.Contains("multi_compile")&&!inc.Contains("shader_feature")&&System.Text.RegularExpressions.Regex.Matches(lit,"#pragma").Count==3&&lit.Contains("#pragma target 3.5")&&lit.Contains("#pragma multi_compile_instancing"),"no new keywords or variants");
            Check(inc.Contains("min(_ArmorCount,full>.5?8:4)")&&inc.Contains("if(full>.5)"),"quality .5: four boxes, diffuse only");
            Check(!lit.Contains("Properties { _Armor")&&!System.Text.RegularExpressions.Regex.IsMatch(lit.Substring(0,lit.IndexOf("SubShader",StringComparison.Ordinal)),"_Armor"),"no armor Properties (per-material uniforms only)");
            string build=Read(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs");
            Check(build.Contains("\"NativeSourceLit\"")&&build.Contains("models.ArmorEmitters!=11||models.ArmorReceivers<37||models.ArmorHeld<6"),"NativeBuildChecks compiles NativeSourceLit and checks the tables");
            // Untouched paths: the Siren ink, the Siren files, the hologram, kill recolour and portrait shaders, the intro weapons.
            Check(lit.Contains("if(_InkFill>.5){o.Albedo=_InkColor.rgb;o.Emission=float3(0,0,0);}")&&lit.Contains("surf(i,o);if(_InkFill>.5)return;"),"inked pilots return before the armor light");
            string ink=Read(root,"Assets/ChipArena/Runtime/NativeDevEffects.cs");Check(!ink.Contains("_Armor")&&ink.Contains("Material=inkMaterial"),"the Siren ink material has no armor role");
            foreach(var dir in new[]{"Assets/ChipArena/Runtime","Assets/ChipArena/Core"})foreach(var file in Directory.GetFiles(Path.Combine(root,dir),"NativeSiren*.cs")){var text=File.ReadAllText(file);Check(!text.Contains("NativeArmorLight")&&!text.Contains("_ArmorLight")&&!text.Contains("HeldGlow"),"Siren file untouched by armor lights: "+Path.GetFileName(file));}
            Check(!Read(root,"Assets/ChipArena/Runtime/NativeIntros.cs").Contains("HeldGlow")&&!Read(root,"Assets/ChipArena/Runtime/NativeIntros.cs").Contains("NativeArmorLight"),"entrance weapons do not opt in");
            foreach(var shader in new[]{"NativeHologram","NativeKillBody","NativeKillGhost","NativePortrait","NativeSourceTransparent","NativeLit","NativeGlow"})
                Check(!Read(root,"Assets/ChipArena/Resources/"+shader+".shader").Contains("NativeArmorLight"),shader+" keeps its look");
            Check(Read(root,"Assets/ChipArena/Runtime/NativeSourcePresentation.cs").Contains("partTint:tint,glow:HeldGlow);"),"the arena / first-person weapon draw opts the cuff light in");
        }
    }
}
#endif
