using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The holographic revive (owner 2026-10-13: wounded or gibbed pilots repaired with cyber holographic lines, levitating for a
    // moment and back to normal). Core NativeReviveHolo (the lift, the repair curves, the hardlight pieces, the wireframe edges),
    // NativeWoundSkin's hole recorder and NativeBloodSplats.FadeNear; Runtime NativeReviveHoloPresentation / NativeReviveModels /
    // NativeInteractionPresentation and the two-slot wound cache (text wiring only: no Unity here). The lift stays inside the
    // C17 revive window and lands exactly on the authority body; every crater cell is where the cut takes it, recording changes
    // no cut; the repair completes by the scan's end at every scan coordinate; nothing allocates per frame; and no rule reads it.
    internal static class ReviveHoloChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Revive holo: "+what);count++;}

        public static void Run(string root)
        {
            count=0;var report=new List<string>();var options=new JsonSerializerOptions{IncludeFields=true};
            var woundParts=JsonSerializer.Deserialize<NativeWoundParts>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/WoundParts.json")),options).parts;

            // ---- 1. The lift: inside the C17 window, .4-.6 m, smooth, exactly on the floor before and from its end ----
            Check(NativeReviveRules.Scan==.8f&&NativeReviveRules.Stand==.6f&&NativeReviveHolo.End==NativeReviveRules.Seconds,"the C17 revive window is unchanged (scan .8 s, stand .6 s) and the lift ends with it");
            float peak=0,step=0,prev=0;
            for(int i=-100;i<=400;i++){float t=i/200f,l=NativeReviveHolo.Lift(t);Check(l>=0&&l<=NativeReviveHolo.Height+1e-6f&&float.IsFinite(l),"lift in range at "+t);if(l>peak)peak=l;if(i>-100)step=Math.Max(step,Math.Abs(l-prev));prev=l;}
            Check(peak>=.4f&&peak<=.6f,"the body rises .4-.6 m ("+peak.ToString("0.000")+")");
            Check(step<.02f,"the lift is smooth (largest change per 5 ms "+step.ToString("0.0000")+" m)");
            foreach(float t in new[]{-1f,0f,NativeReviveHolo.RiseAt,NativeReviveHolo.End,NativeReviveHolo.End+1e-4f,NativeReviveHolo.Total,5f,float.NaN,float.PositiveInfinity})Check(NativeReviveHolo.Lift(t)==0,"on the floor at "+t);
            Check(NativeReviveHolo.Lift(NativeReviveRules.Scan)>.45f&&NativeReviveHolo.Lift(NativeReviveRules.Scan+.1f)>.4f,"still floating as it starts to stand");
            Check(NativeReviveHolo.Lift(NativeReviveHolo.End-.01f)<.002f,"set down by the window's end ("+NativeReviveHolo.Lift(NativeReviveHolo.End-.01f).ToString("0.0000")+" m 10 ms before)");
            float view=0;for(int i=0;i<=400;i++){float t=i/200f;Check(NativeReviveHolo.ViewLift(t,true)==0,"the still view never moves");view=Math.Max(view,NativeReviveHolo.ViewLift(t,false));}
            Check(view>.1f&&view<=.2f,"the first-person view rises gently ("+view.ToString("0.000")+" m)");
            foreach(float t in new[]{-.1f,NativeReviveHolo.Total,NativeReviveHolo.Total+1,float.NaN}){Check(NativeReviveHolo.Vignette(t)==0&&NativeReviveHolo.Aura(t)==0,"vignette and aura off at "+t);}
            Check(NativeReviveHolo.Vignette(.5f)>.9f&&NativeReviveHolo.Aura(.5f)>.9f&&NativeReviveHolo.Aura(NativeReviveHolo.End+.1f)>0,"vignette and aura on through the revive, the aura lingering after it lands");
            report.Add("lift: up to "+peak.ToString("0.00")+" m from "+NativeReviveHolo.RiseAt+" s, back on the floor at "+NativeReviveHolo.End.ToString("0.00")+" s (the C17 window's end), first person "+view.ToString("0.00")+" m or still");

            // ---- 2. The repair follows the C17 front and completes by the scan's end ----
            for(int i=0;i<=40;i++)
            {
                float c=i/40f,a=NativeReviveHolo.Arrive(c);
                if(c>.01f)Check(NativeReviveRules.Front(a+1e-4f)*1.2f>=c&&NativeReviveRules.Front(a-2e-3f)*1.2f<c,"arrival is the C17 front's switch at "+c);
                Check(NativeReviveHolo.Heal(NativeReviveRules.Scan,c)==1&&NativeReviveHolo.Solid(NativeReviveRules.Scan,c)==1,"healed and solid by the scan's end at "+c);
                Check(NativeReviveHolo.Wire(NativeReviveRules.Scan,c)<1e-4f&&NativeReviveHolo.Fill(NativeReviveRules.Scan,c)<1e-4f,"no hologram left at the scan's end at "+c);
                float tw=First(t=>NativeReviveHolo.Wire(t,c)>.5f),tf=First(t=>NativeReviveHolo.Fill(t,c)>.5f),ts=First(t=>NativeReviveHolo.Solid(t,c)>.5f),th=First(t=>NativeReviveHolo.Heal(t,c)>.5f);
                Check(tw<tf&&tf<ts&&ts<NativeReviveRules.Scan&&th<NativeReviveRules.Scan,"a missing part re-forms wire, hologram, then skin at "+c);
                float last=0;for(int k=0;k<=160;k++){float v=NativeReviveHolo.Heal(k/200f,c);Check(v>=last-1e-6f,"healing never reverses");last=v;}
            }
            report.Add("repair: wire, hologram, then skin at every scan coordinate, all done by "+NativeReviveRules.Scan+" s on the C17 front");

            // ---- 3. The crater cells: where the cut takes them; recording changes no cut ----
            var bank=BisectChecks.Bank(root);int holesSkin=0,holesProto=0,cases=0,outside=0;float worst=0;
            foreach(var c in WoundSkinCases.All())
            {
                var open=WoundSkinCases.Open(c.Wounds);if(open.Length==0)continue;cases++;
                var groups=NativeWoundAnchors.Carve(open.SelectMany(w=>NativeWoundAnchors.Of(woundParts,w,c.Low,WoundSkinCases.Body)));
                foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})foreach(int tier in new[]{0,2})
                {
                    var body=NativeBodyModels.Build(skin,tier);var look=NativeBodyModels.WoundLook(skin);var holes=new List<WoundHole>();
                    var plain=NativeWoundSkin.Cut(body,groups,look);var recorded=NativeWoundSkin.Cut(body,groups,look,holes);
                    Check(Digest(plain)==Digest(recorded),"recording the holes changes no cut ("+c.Name+", skin "+skin+")");
                    int removed=groups.Sum(g=>g.Steps.Sum(s=>s.Removed.Count));Check(holes.Count<=removed,"no more holes than removed cells");
                    if(open.Any(w=>w.kind!="scorch"))Check(holes.Count>0,"a wound that takes cells leaves holes ("+c.Name+")");holesSkin+=holes.Count;
                    // Each hole on its joint, inside the body's own parts there (it lies on the skin, not in the Prototype's boxes).
                    foreach(var hole in holes)
                    {
                        Check(hole.Bone>=0&&hole.Bone<22&&float.IsFinite(hole.Center.X+hole.U.Length()+hole.V.Length()+hole.W.Length()),"a finite hole on a joint");
                        var (lo,hi)=Bounds(body.Parts.Where(p=>p!=null&&p.Bone==hole.Bone).Select(p=>p.Mesh));float d=Outside(hole.Center,lo,hi);worst=Math.Max(worst,d);if(d>.025f)outside++;
                    }
                }
                var proto=new List<WoundHole>();NativeWoundSkin.PrototypeHoles(groups,proto);Check(proto.Count==groups.Sum(g=>g.Steps.Sum(s=>s.Removed.Count)),"the Prototype's holes are its carve's removed cells");holesProto+=proto.Count;
                foreach(var hole in proto)
                {
                    var g=groups.First(x=>x.Part.bone==hole.Bone);var (lo,hi)=Bounds(groups.Where(x=>x.Part.bone==hole.Bone).Select(x=>BisectChecks.CarveMesh(x.Part,x.Part.solids.ToList())));
                    Check(Outside(hole.Center,lo,hi)<.005f,"a Prototype hole lies in its part's boxes");
                }
            }
            Check(outside==0,"every skin hole lies on its joint's body (worst "+worst.ToString("0.000")+" m outside)");
            report.Add("crater cells: "+cases+" wound lists x Standard / Sheldon Clothes (tiers 0, 2): "+holesSkin+" skin holes, all on their joint's body; "+holesProto+" Prototype holes = its removed cells; recording changes no cut");

            // ---- 4. The hardlight pieces: budgets, the viewer, determinism and no allocation ----
            var pose=Pose(new Vector3(0,0,0));var cs=WoundSkinCases.All().ToDictionary(x=>x.Name);
            var eight=NativeWoundAnchors.Carve(WoundSkinCases.Open(cs["eight"].Wounds).SelectMany(w=>NativeWoundAnchors.Of(woundParts,w,false,WoundSkinCases.Body)));
            var many=new List<WoundHole>();NativeWoundSkin.Cut(NativeBodyModels.Build(NativeBodySkins.StandardIndex,0),eight,NativeBodyModels.WoundLook(NativeBodySkins.StandardIndex),many);
            Vector3 feet=new Vector3(0,0,0),head=new Vector3(0,1.85f,0);var coordinates=many.Select(hl=>NativeReviveHolo.Along(Vector3.Transform(hl.Center,pose[hl.Bone]),feet,head)).ToArray();
            var into=new ReviveGlow[NativeReviveHolo.CraterBudget(NativeReviveHolo.Full)+NativeReviveHolo.MaxAura+4];int most=0;
            foreach(int tier in new[]{NativeReviveHolo.Full,NativeReviveHolo.Reduced,NativeReviveHolo.Low})
            {
                int seen=0;
                for(int f=0;f<=90;f++)
                {
                    float t=f/60f;int n=NativeReviveHolo.Crater(many,coordinates,pose,NativeReviveHolo.Lift(t),t,tier,into,0);Check(n<=NativeReviveHolo.CraterBudget(tier),"crater voxels within the tier's budget");seen=Math.Max(seen,n);
                    for(int k=0;k<n;k++)Check(into[k].Kind==NativeReviveHolo.Cube&&into[k].Color.W>0&&into[k].Color.W<=1&&float.IsFinite(into[k].Matrix.M41+into[k].Matrix.M42+into[k].Matrix.M43),"a finite, visible voxel");
                    if(t>NativeReviveRules.Scan+.1f)Check(n==0,"the craters are full by the scan's end");
                    int m=NativeReviveHolo.Halo(t,77,new Vector3(0,0,0),1.1f,NativeReviveHolo.Lift(t),tier,false,into,0);Check(m<=NativeReviveHolo.MaxAura,"halo within its budget");
                    if(tier==NativeReviveHolo.Low)Check(Enumerable.Range(0,m).Count(k=>into[k].Kind==NativeReviveHolo.Cube)==0,"no streaks on the low tier");
                    int v=NativeReviveHolo.Halo(t,77,new Vector3(0,0,0),1.1f,NativeReviveHolo.Lift(t),tier,true,into,0);Check(Enumerable.Range(0,v).All(k=>into[k].Kind==NativeReviveHolo.Ring),"a first-person viewer stands in no column");
                }
                Check(seen>0,"crater voxels show during the repair (tier "+tier+")");most=Math.Max(most,seen);
            }
            Check(NativeReviveHolo.Halo(NativeReviveHolo.Total+.01f,77,Vector3.Zero,1,0,0,false,into,0)==0,"the halo is gone after it lands");
            var a1=new ReviveGlow[into.Length];var a2=new ReviveGlow[into.Length];int n1=NativeReviveHolo.Crater(many,coordinates,pose,.3f,.31f,0,a1,0),n2=NativeReviveHolo.Crater(many,coordinates,pose,.3f,.31f,0,a2,0);
            Check(n1==n2&&Enumerable.Range(0,n1).All(k=>a1[k].Matrix==a2[k].Matrix&&a1[k].Color==a2[k].Color),"deterministic");
            float Frames(){float sum=0;for(int f=0;f<600;f++){float t=(f%130)/60f;sum+=NativeReviveHolo.Lift(t)+NativeReviveHolo.ViewLift(t,false)+NativeReviveHolo.Vignette(t)+NativeReviveHolo.Aura(t)+NativeReviveHolo.Heal(t,.5f)+NativeReviveHolo.Wire(t,.5f)+NativeReviveHolo.Fill(t,.5f)+NativeReviveHolo.Solid(t,.5f)+NativeReviveHolo.Band(t);
                sum+=NativeReviveHolo.Crater(many,coordinates,pose,NativeReviveHolo.Lift(t),t,0,into,0);sum+=NativeReviveHolo.Halo(t,77,Vector3.Zero,1.1f,NativeReviveHolo.Lift(t),0,false,into,0);}return sum;}
            Frames();long before=GC.GetAllocatedBytesForCurrentThread();Frames();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"nothing allocates per frame ("+bytes+" bytes over 600 frames)");
            report.Add("hardlight: up to "+most+" crater voxels ("+string.Join("/",new[]{0,1,2}.Select(NativeReviveHolo.CraterBudget))+" by tier), the ring, disc, column and "+NativeReviveHolo.StreakCount(0)+" streaks; no column round a first-person viewer; deterministic; 600 frames, "+bytes+" bytes");

            // ---- 5. The wireframe: a mesh's feature edges ----
            var cube=Cube();var edges=new List<int>();NativeReviveHolo.Edges(cube,edges);Check(edges.Count==24,"a cube's wireframe is its 12 edges ("+edges.Count/2+")");
            var quad=new WeaponSection();foreach(var p in new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0)}){quad.Positions.Add(p);quad.Normals.Add(Vector3.UnitZ);quad.Colors.Add(Vector4.One);quad.Emission.Add(Vector4.Zero);}quad.Indices.AddRange(new[]{0,1,2,0,2,3});
            NativeReviveHolo.Edges(quad,edges);Check(edges.Count==8,"a flat quad's wireframe is its outline, not its diagonal");
            int bodyEdges=0,bodyTris=0;foreach(var part in NativeBodyModels.Build(NativeBodySkins.StandardIndex,0).Parts.Where(p=>p!=null&&p.Bone==CapturedRig.Head)){NativeReviveHolo.Edges(part.Mesh,edges);var again=new List<int>();NativeReviveHolo.Edges(part.Mesh,again);Check(edges.SequenceEqual(again),"the wireframe is deterministic");bodyEdges+=edges.Count/2;bodyTris+=part.Mesh.Indices.Count/3;Check(edges.All(i=>i>=0&&i<part.Mesh.Positions.Count),"wire indices are the part's own vertices");}
            Check(bodyEdges>0&&bodyEdges<bodyTris*3/2,"a Standard head's wireframe is its outline ("+bodyEdges+" edges over "+bodyTris+" triangles)");
            report.Add("wireframe: feature edges only (a cube's 12, a quad's 4; the Standard head "+bodyEdges+" over "+bodyTris+" triangles)");

            // ---- 6. The wash: the blood and ink round the body fade, nothing else ----
            var splats=new NativeBloodSplats();int k0=0;foreach(var at in new[]{new Vector3(.5f,0,0),new Vector3(0,0,1.4f),new Vector3(2.2f,0,0),new Vector3(-1,0,-.5f)})splats.Add(k0++,at,Vector3.UnitY,Vector3.UnitX,.2f,.1f,1,0,1,k0==4);
            splats.Items[3].FadeAt=.5f;int version=splats.Version,steady=splats.Steady;int faded=splats.FadeNear(Vector3.Zero,NativeReviveHolo.WashRadius,2,NativeReviveHolo.WashFor);
            Check(faded==2&&splats.Steady==steady-2&&splats.Version>version&&splats.Items[2].FadeAt<0&&splats.Items[0].FadeAt==2&&splats.Items[1].FadeAt==2&&splats.Items[3].FadeAt==.5f,"the wash fades the steady blood and ink within "+NativeReviveHolo.WashRadius+" m, nothing further or already fading");
            report.Add("wash: blood and ink within "+NativeReviveHolo.WashRadius+" m fade over "+NativeReviveHolo.WashFor+" s");

            // ---- 7. Wiring (source text; Unity cannot run here) and presentation only ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r","");
            string Method(string text,string start){int a=text.IndexOf(start,StringComparison.Ordinal);if(a<0)return "";int b=text.IndexOf("\n        private ",a+start.Length,StringComparison.Ordinal);int b2=text.IndexOf("\n        public ",a+start.Length,StringComparison.Ordinal);int e=new[]{b<0?text.Length:b,b2<0?text.Length:b2}.Min();return text.Substring(a,e-a);}
            var interaction=R("Runtime/NativeInteractionPresentation.cs");var holo=R("Runtime/NativeReviveHoloPresentation.cs");var models=R("Runtime/NativeReviveModels.cs");var game=R("Runtime/ConversionGame.cs");var geometry=R("Runtime/NativeWoundGeometry.cs");
            Check(interaction.Contains("StandFromBody(r,rig,time-r.At);VisualStandPose(rig,a,time);}\n                // The levitation")&&interaction.Contains("if(r!=null)ReviveLift(r,rig,time,a.Id);")&&interaction.Contains("BeginHolo(r,rig,p.Seat.bodyColor,p.Seat.accentColor,p.Seat.hat,p.Seat.hatColor);")&&interaction.Contains("GibsOf(r,rig);")&&interaction.Contains("RenderRevival(arena,r,rig,time,!(FirstPerson&&r.Id==LocalId));"),"the C17 revive starts, lifts and draws the hologram (rig lifted after it stands from the body)");
            Check(Method(holo,"private void ReviveLift(").Contains("if(FirstPerson&&id==LocalId)return;")&&Method(holo,"private void ReviveLift(").Contains("rig.Root.transform.position+=Vector3.up*lift;"),"only the drawn rig's root lifts (the first-person viewer's view rises instead)");
            Check(holo.Contains("r.Body[hole.Bone].MultiplyPoint3x4(CutVec(hole.Center))")&&holo.Contains("sourceModels.WoundHoles(h.Wounded,h.Holes);")&&holo.Contains("NativeReviveHolo.Crater(h.Holes,h.HoleAt,h.Draw,lift,raw,tier,reviveGlows,0)"),"the craters refill from the wounded model's own cells");
            Check(holo.Contains("blood.Splats.FadeNear(CutNum(floor),")&&holo.Contains("raw>=NativeReviveHolo.WashAt"),"the blood and ink wash away once, at the same moment on every client");
            Check(holo.Contains("FloorY")&&Method(holo,"private static void ReviveFloor(").Contains("r.FloorY"),"the ring and column stand on the revive anchor's floor (NativeReviveAnchor)");
            foreach(var name in new[]{"private void RenderRevival(","private void DrawReviveGlows(","private void ReviveLinger(","private static Matrix4x4 HalfBone("})
            {var m=Method(holo,name);Check(m.Length>100&&!m.Contains("=>")&&!m.Contains("new List")&&!m.Contains("ToArray")&&!m.Contains(".Select(")&&!m.Contains("new[]"),name+" makes no delegate, list or array per frame");}
            Check(Method(holo,"private void BeginHolo(").Contains("h.Bone=b=>h.Up*r.Body[b];"),"the per-joint curves are cached once per revive");
            int kick=game.IndexOf("presentation.GunKitKick(",StringComparison.Ordinal),viewCall=game.IndexOf("presentation.ReviveView(cameraView,arena.ViewPilot.Seat.id,renderTime,prefs.reducedEffects||!prefs.cameraShake);",StringComparison.Ordinal),render=game.IndexOf("presentation.Render(frame,renderTime,false);",StringComparison.Ordinal);
            Check(kick>0&&viewCall>kick&&render>viewCall,"the first-person view rises after the aim is placed and before the frame draws; reduced effects or camera shake off keep it still");
            Check(geometry.Contains("if(Same(a,impacts,bodyHex,low)){a.Used=now;Stale(b,now);return Wounded(a,basis);}")&&geometry.Contains("if(Same(b,impacts,bodyHex,low)){b.Used=now;Stale(a,now);return Wounded(b,basis);}")&&geometry.Contains("Key=second?~id:id"),"a reviving pilot and its corpse keep their own carves (no recut every frame); a kept carve is found without allocating");
            Check(models.Contains("MeshTopology.Lines")&&models.Contains("Resources.Load<Shader>(\"NativeReviveHolo\")")&&R("Runtime/NativeSandbox.cs").Contains("\"Preview Revive\"")&&R("Editor/NativeBuildChecks.cs").Contains("new[]{\"NativeReviveHolo\",\"NativeReviveScreen\"}"),"wireframes, the hardlight shader, the Sandbox preview and the build gate's shader compile");
            foreach(var shader in new[]{"NativeReviveHolo","NativeReviveScreen"}){var text=R("Resources/"+shader+".shader");Check(text.Contains("Blend SrcAlpha One")&&text.Contains("ZWrite Off")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources",shader+".shader.meta")),shader+" is additive, writes no depth and has its meta");}
            Check(!R("Core/NativeReviveHolo.cs").Contains("UnityEngine"),"the revive's rules are engine free");
            Check(R("Runtime/NativePve.cs").Contains("p.ShieldUntil=Time+1.5f;")&&R("Runtime/NativePve.cs").Contains("p.Body.position=respawn??p.Body.position;")&&R("Core/NativeInteractionPose.cs").Contains("public const float Scan=.8f,Stand=.6f,Seconds=Scan+Stand,Settle=.05f,Lead=1,PlaceRadius=1.6f,Dissolve=.7f;"),"revive rules, timing, shield and anchor unchanged");
            foreach(var f in new[]{"Runtime/NativeArena.cs","Runtime/NativePve.cs","Runtime/NativeReviveAnchor.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeDeaths.cs","Runtime/NativeCorpseWorld.cs","Runtime/NativeProjectiles.cs","Runtime/NativeMelee.cs"})
            {var text=R(f);Check(!text.Contains("NativeReviveHolo")&&!text.Contains("ReviveLift")&&!text.Contains("WoundHole"),f+" (rules, network, hit volumes) never reads the hologram");}
            report.Add("wiring: C17 start/lift/draw, rig-only lift, craters from the wounded model, one wash, anchor floor, cached curves, first-person view after the aim, two-slot wound cache, Sandbox preview, shader gate; no rule reads it");

            foreach(var line in report)Console.WriteLine("PASS revive holo: "+line);
            Console.WriteLine("PASS revive holo: "+count+" checks");
        }

        private static float First(Func<float,bool> on){for(int i=0;i<=400;i++){float t=i/400f;if(on(t))return t;}return float.MaxValue;}
        private static string Digest(List<WoundSkinMesh> meshes)
        {
            var sb=new System.Text.StringBuilder();
            foreach(var m in meshes){sb.Append(m.Part).Append('/').Append(m.Bone).Append('/').Append(m.Name).Append(':');foreach(var p in m.Mesh.Positions)sb.Append(p.X.ToString("R")).Append(',').Append(p.Y.ToString("R")).Append(',').Append(p.Z.ToString("R")).Append(';');foreach(var c in m.Mesh.Colors)sb.Append(c.X.ToString("R")).Append(';');foreach(int i in m.Mesh.Indices)sb.Append(i).Append(',');sb.Append('\n');}
            return WoundSkinCases.Hash(sb.ToString());
        }
        private static (Vector3,Vector3) Bounds(IEnumerable<WeaponSection> meshes)
        {
            var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);foreach(var m in meshes)foreach(var p in m.Positions){lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}return (lo,hi);
        }
        private static float Outside(Vector3 p,Vector3 lo,Vector3 hi)=>Vector3.Distance(p,Vector3.Clamp(p,lo,hi));
        private static WeaponSection Cube()
        {
            var w=new WeaponSection();
            for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
            {
                var n=Vector3.Zero;if(axis==0)n.X=sign;else if(axis==1)n.Y=sign;else n.Z=sign;var u=axis==0?Vector3.UnitY:axis==1?Vector3.UnitZ:Vector3.UnitX;var v=Vector3.Cross(n,u);int start=w.Positions.Count;
                foreach(var c in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){w.Positions.Add((n+u*c.X+v*c.Y)*.5f);w.Normals.Add(n);w.Colors.Add(Vector4.One);w.Emission.Add(Vector4.Zero);}
                w.Indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            return w;
        }
        /// <summary>The C17 rig standing at `feet` (Unity frame, the bank's mirror), each joint's draw matrix.</summary>
        private static Matrix4x4[] Pose(Vector3 feet)
        {
            var p=new CapturedRig.Pose();p.World();var bones=new Matrix4x4[22];
            for(int b=0;b<22;b++){var q=p.WorldRotation[b];var at=p.WorldPosition[b];bones[b]=Matrix4x4.CreateFromQuaternion(new Quaternion(q.X,-q.Y,-q.Z,q.W))*Matrix4x4.CreateTranslation(new Vector3(-at.X,at.Y+.865f,at.Z)+feet);}
            return bones;
        }
    }
}
