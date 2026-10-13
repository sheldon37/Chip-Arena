#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Wounds on the sculpted body skins (Core/NativeWoundSkin.cs): the owner's Windows report "getting shot in head reverts
    // model back to prototype". The Prototype's carve is pinned to the digest of the aead19c code (anchors and carved boxes of
    // 22 replicated wound lists); the Standard and Sheldon Clothes cut the same carve into their own parts at every tier, on
    // their own surface, closed along every cut column, deterministic, and the Runtime never composes a Prototype part into
    // a body skin's wounded model.
    internal static class WoundSkinChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Wound skins: "+what);count++;}
        /// <summary>The digest of the anchors and carved boxes of WoundSkinCases.All on aead19c (the Runtime's Anchors and Core's
        /// NativeWoundCarve before this change), computed by the scratch digest harness from the original code.</summary>
        private const string PrototypeDigest="8d0ed1aeb3e99540bdb2b0e7";
        /// <summary>The aead19c Runtime CarveMesh (the Prototype's carved part mesh), byte for byte: SHA-256 prefix.</summary>
        private const string CarveMeshDigest="aabe0822c3073c140401afa4";
        public static void Run(string root)
        {
            count=0;var options=new JsonSerializerOptions{IncludeFields=true};
            var parts=JsonSerializer.Deserialize<NativeWoundParts>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/WoundParts.json")),options);
            Check(parts.commit=="05950eaef80ff2483a9c44d898cad98be1a76bbc"&&parts.parts.Length==31,"the exported wound boxes load in Core (31 parts)");
            var report=new List<string>();

            // ---- 1. The Prototype keeps today's carve exactly ----
            var all=new StringBuilder();int solids=0;
            foreach(var c in WoundSkinCases.All())
            {
                var open=WoundSkinCases.Open(c.Wounds);var anchors=open.SelectMany(w=>NativeWoundAnchors.Of(parts.parts,w,c.Low,WoundSkinCases.Body)).ToList();
                foreach(var a in anchors)WoundSkinCases.Spec(all,a.Part.name,a.Spec);
                var groups=NativeWoundAnchors.Carve(anchors);var plain=new StringBuilder();
                foreach(var g in groups){WoundSkinCases.Solids(all,g.Part.name,g.Solids);solids+=g.Solids.Count;}
                // Recording the cells changes nothing the carve returns.
                foreach(var g in anchors.GroupBy(x=>x.Part.name)){var s=g.First().Part.solids.ToList();foreach(var x in g)s=NativeWoundCarve.Carve(s,x.Spec);WoundSkinCases.Solids(plain,g.Key,s);}
                var recorded=new StringBuilder();foreach(var g in groups)WoundSkinCases.Solids(recorded,g.Part.name,g.Solids);
                Check(plain.ToString()==recorded.ToString(),c.Name+": the recorded carve equals the plain carve");
                Check(anchors.Count>0&&anchors[0].First&&groups.All(g=>g.Steps.Count==g.First.Count&&g.Steps.Count>0),c.Name+": anchors and carve records");
            }
            string digest=WoundSkinCases.Hash(all.ToString());
            Check(digest==PrototypeDigest,"the Prototype's anchors and carved boxes equal the aead19c code's ("+digest+", pinned "+PrototypeDigest+")");
            string geometry=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeWoundGeometry.cs")).Replace("\r","");
            int ci=geometry.IndexOf("        private Part CarveMesh(",StringComparison.Ordinal),cj=geometry.IndexOf("        public (int bone,Vector3 point,Vector3 normal,Vector3 direction)? WoundMark",StringComparison.Ordinal);
            Check(ci>0&&cj>ci&&WoundSkinCases.Hash(geometry.Substring(ci,cj-ci))==CarveMeshDigest,"the Prototype's carved part mesh (CarveMesh) is the aead19c code byte for byte");
            Check(geometry.Contains("owned.Add(CarveMesh(d,group.Solids,template.Material));")&&geometry.Contains("var list=models[basis].Where(p=>!carved.Replaced.Contains(p.Name)).ToList();list.AddRange(carved.Owned);parts=list.ToArray();"),"the Prototype is composed as before: its parts with each wounded part replaced by name by its carved boxes");
            report.Add("Prototype: "+WoundSkinCases.All().Count+" wound lists, "+solids+" carved boxes, digest "+digest+" = aead19c; recording leaves every carve unchanged; CarveMesh text unchanged");

            // ---- 2. The body skins cut the same carve into their own parts ----
            int cuts=0,inside=0,columns=0,closed=0;double worstOut=0,worstIn=0;
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})for(int tier=0;tier<NativeBodySkins.Tiers;tier++)
            {
                var body=NativeBodyModels.Build(skin,tier);var look=NativeBodyModels.WoundLook(skin);string lab=NativeBodySkins.Labels[skin]+" tier "+tier;
                foreach(var c in WoundSkinCases.All())
                {
                    var open=WoundSkinCases.Open(c.Wounds);var groups=NativeWoundAnchors.Carve(open.SelectMany(w=>NativeWoundAnchors.Of(parts.parts,w,c.Low,WoundSkinCases.Body)));
                    var result=NativeWoundSkin.Cut(body,groups,look);var again=NativeWoundSkin.Cut(body,groups,look);string where=lab+" "+c.Name;
                    Check(result.Count==again.Count&&result.Zip(again,(a,b)=>a.Part==b.Part&&a.Name==b.Name&&a.Mesh.Positions.SequenceEqual(b.Mesh.Positions)&&a.Mesh.Colors.SequenceEqual(b.Mesh.Colors)&&a.Mesh.Indices.SequenceEqual(b.Mesh.Indices)).All(x=>x),where+": deterministic");
                    var cut=result.Where(m=>m.Part>=0).ToList();var insides=result.Where(m=>m.Part<0).ToList();cuts+=cut.Count;inside+=insides.Sum(m=>m.Mesh.Triangles);
                    Check(cut.Count>0&&insides.Count>0&&insides.All(m=>m.Mesh.Triangles>0),where+": the wound shows (the body's parts cut, an inside laid)");
                    Check(cut.All(m=>m.Part<body.Parts.Length&&m.Name==body.Parts[m.Part].Name&&m.Bone==body.Parts[m.Part].Bone&&m.Material==m.Part),where+": every cut part is the body's own part (name, joint, material)");
                    Check(insides.All(m=>m.Name.StartsWith("wound ",StringComparison.Ordinal)&&m.Material>=0&&body.Parts[m.Material].Bone==m.Bone&&groups.Any(g=>g.Part.bone==m.Bone)),where+": every inside rides its wounded joint with a body part's material");
                    foreach(var m in result)
                    {
                        Check(m.Mesh.Positions.Count==m.Mesh.Normals.Count&&m.Mesh.Positions.Count==m.Mesh.Colors.Count&&m.Mesh.Positions.Count==m.Mesh.Emission.Count&&m.Mesh.Indices.Count%3==0&&m.Mesh.Indices.All(i=>i>=0&&i<m.Mesh.Positions.Count),where+" "+m.Name+": a well-formed mesh");
                        Check(m.Mesh.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&p.Length()<1)&&m.Mesh.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),where+" "+m.Name+": finite, joint-local, unit normals");
                        Check(Outward(m.Mesh),where+" "+m.Name+": faces wound out (the bank's winding)");
                    }
                    // Along the face axis through every column a first-anchor carve touched: the body's surface over the hit is
                    // opened, the inside lies under the body's surface (never floating off it), and no column sees through.
                    var byPart=cut.ToDictionary(m=>m.Part);
                    foreach(var g in groups)for(int s=0;s<g.Steps.Count;s++)
                    {
                        if(!g.First[s])continue;var st=g.Steps[s];var d=g.Part;
                        var original=Triangles(body,d.bone,null,null);var hurt=Triangles(body,d.bone,byPart,insides);
                        var cols=new HashSet<(int,int)>(st.Removed.Select(r=>(r.i,r.j)));
                        foreach(var (i,j) in cols)
                        {
                            var (o,dir)=Column(d,st,i,j);double before=First(original,o,dir),after=First(hurt,o,dir);if(double.IsInfinity(before))continue;columns++;
                            Check(!double.IsInfinity(after),where+" "+d.name+" column "+i+","+j+": no see-through along the cut");
                            Check(after<=before+(st.M+3)*st.G+.03,where+" "+d.name+" column "+i+","+j+": the hole has a floor ("+(after-before).ToString("0.000")+" m below the surface)");
                            worstIn=Math.Max(worstIn,after-before);closed++;
                        }
                        // The hit's own column: the inside's first cell sits at the body's surface (within a cell), not inside it.
                        var centre=Column(d,st,0,0);double surface=First(original,centre.o,centre.dir),cell=First(Triangles(body,d.bone,null,insides,only:true),centre.o,centre.dir);
                        if(!double.IsInfinity(surface)&&!double.IsInfinity(cell)){worstOut=Math.Max(worstOut,surface-cell);Check(cell>=surface-.6*st.G-.0005,where+" "+d.name+" step "+s+" "+st.Kind+" g "+st.G+": the wound does not stand off the surface ("+(surface-cell).ToString("0.000")+" m)");}
                    }
                }
            }
            Check(columns>2000&&closed==columns,"every cut column of every wound list closes ("+closed+" columns)");
            report.Add("skins: "+WoundSkinCases.All().Count+" wound lists x Standard / Sheldon Clothes x 3 tiers: "+cuts+" body parts cut, "+inside+" inside triangles; "+columns+" cut columns closed along the face axis (deepest floor "+worstIn.ToString("0.000")+" m), insides at the surface (most "+worstOut.ToString("0.000")+" m proud)");

            // ---- 3. The skins' look: own paint at the rim, a dark layer under it, the Prototype's flesh below ----
            {
                var c=WoundSkinCases.All().First(x=>x.Name=="head-front-pistol");var groups=NativeWoundAnchors.Carve(WoundSkinCases.Open(c.Wounds).SelectMany(w=>NativeWoundAnchors.Of(parts.parts,w,false,WoundSkinCases.Body)));
                foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})
                {
                    var look=NativeBodyModels.WoundLook(skin);var body=NativeBodyModels.Build(skin,0);var ins=NativeWoundSkin.Cut(body,groups,look).Where(m=>m.Part<0).SelectMany(m=>m.Mesh.Colors).Select(v=>new Vector3(v.X,v.Y,v.Z)).Distinct().ToList();
                    var paints=body.Parts.Where(p=>p.Bone==CapturedRig.Head).SelectMany(p=>p.Mesh.Colors).Select(v=>new Vector3(v.X,v.Y,v.Z)).Distinct().ToList();
                    Check(ins.Any(v=>Vector3.Distance(v,look.Inner)<1e-4f),NativeBodySkins.Labels[skin]+": the layer under the surface shows ("+(look.Cloth?"the coat's lining":"the undersuit")+")");
                    Check(ins.Any(v=>paints.Any(p=>Vector3.Distance(v,p*look.Shade)<1e-3f)),NativeBodySkins.Labels[skin]+": the rim is the body's own paint, darkened");
                    Check(ins.Any(v=>v.X>.05f&&v.X>4*v.Y&&v.X>4*v.Z),NativeBodySkins.Labels[skin]+": flesh red deeper in");
                }
                Check(NativeBodyModels.WoundLook(NativeBodySkins.SheldonIndex).Cloth&&!NativeBodyModels.WoundLook(NativeBodySkins.StandardIndex).Cloth&&NativeBodyModels.WoundLook(NativeBodySkins.StandardIndex).ConduitLevel>0,"torn cloth for the Sheldon Clothes, breached armour with accent conduits for the Standard");
            }

            // ---- 4. Kill boxes in the skin's colours (voxel burst, crush debris, stump caps) ----
            {
                var boxes=new KillBox[NativeKillBoxes.MaxRigBoxes];int n=NativeKillBoxes.Rig("sword","cap",boxes);int body=0xe5e7eb,accent=0x70e1d0,changed=0;
                for(int k=0;k<n;k++)
                {
                    var b=boxes[k];int proto=NativeKillEffectRules.BoxColor(b,body,accent);bool part=NativeBodySkins.PartIndex(b.Name,b.Bone)>=0;
                    Check(NativeBodyModels.GibColor(NativeBodySkins.PrototypeIndex,b,body,accent)==proto,"the Prototype keeps its kill-box colours ("+b.Name+")");
                    if(!part)Check(NativeBodyModels.GibColor(NativeBodySkins.SheldonIndex,b,body,accent)==proto&&NativeBodyModels.GibColor(NativeBodySkins.StandardIndex,b,body,accent)==proto,"a hat or weapon box keeps its colour on every skin ("+b.Name+")");
                    else if(NativeBodyModels.GibColor(NativeBodySkins.SheldonIndex,b,body,accent)!=proto)changed++;
                }
                foreach(var b in NativeBodyModels.SheldonGibs)Check(NativeBodyModels.GibColor(NativeBodySkins.SheldonIndex,b,body,accent)==NativeKillEffectRules.BoxColor(b,body,accent),"the coat panels keep their cloth colour");
                var head=boxes.Take(n).First(b=>b.Name=="blank-head");
                Check(NativeBodyModels.GibColor(NativeBodySkins.StandardIndex,head,body,accent)==0xe5e7eb&&NativeBodyModels.GibColor(NativeBodySkins.StandardIndex,head,0x3b82f6,accent)==0x3b82f6,"a Standard helmet bursts in the pilot's body colour, as its plate is painted");
                Check(changed>=20&&NativeBodyModels.GibColor(NativeBodySkins.SheldonIndex,head,body,accent)!=NativeKillEffectRules.BoxColor(head,body,accent),"a Sheldon Clothes body bursts in its own dark coat and frame, not the Prototype's light shell ("+changed+" body boxes)");
                report.Add("kill boxes: the Prototype, hats, weapons and coat panels keep BoxColor; "+changed+" Sheldon Clothes body boxes take the skin's own paint");
            }

            // ---- 5. Wiring (source text; Unity cannot run here) ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string Method(string text,string start){int a=text.IndexOf(start,StringComparison.Ordinal);if(a<0)return "";int b=text.IndexOf("\n        private ",a+start.Length,StringComparison.Ordinal);int b2=text.IndexOf("\n        public ",a+start.Length,StringComparison.Ordinal);int e=new[]{b<0?text.Length:b,b2<0?text.Length:b2}.Min();return text.Substring(a,e-a);}
            // No Prototype part in a body skin's wounded model: the skin composition reads only that body's parts and its cuts.
            string skinPath=Method(geometry,"private Part[] SkinWounds(")+Method(geometry,"private SkinCut SkinCutOf(");
            Check(skinPath.Length>500&&!skinPath.Contains("PrototypeModel")&&!skinPath.Contains("CarveMesh")&&!skinPath.Contains("carved.Owned")&&!skinPath.Contains("WoundParts")&&skinPath.Contains("var baseParts=models[basis];")&&skinPath.Contains("NativeWoundSkin.Cut(body,groups,look)"),"a body skin's wounded model is that body's parts and their cuts, never a Prototype part");
            string wounded=Method(geometry,"private string Wounded(");
            Check(Regex.Matches(geometry,@"models\[NativeBodySkins\.PrototypeModel\]").Count==1&&wounded.Contains("if(basis==NativeBodySkins.PrototypeModel)")&&wounded.IndexOf("models[NativeBodySkins.PrototypeModel]",StringComparison.Ordinal)>wounded.IndexOf("if(basis==NativeBodySkins.PrototypeModel)",StringComparison.Ordinal)&&wounded.IndexOf("models[NativeBodySkins.PrototypeModel]",StringComparison.Ordinal)<wounded.IndexOf("else if(!pilotBodies.TryGetValue(basis,out var body))return basis;",StringComparison.Ordinal),"the Prototype's parts are read only when the Prototype itself is drawn; a skin without its mesh draws unwounded");
            Check(geometry.Contains("RemoveWounds(id);RemoveSkinCuts(id,null);return basis;")&&geometry.Contains("foreach(var cut in skinCuts.Values)ReleaseSkinCut(cut);skinCuts.Clear();")&&geometry.Contains("if(!skinCuts.TryGetValue(key,out var cut)||cut.Signature!=signature){if(cut!=null)ReleaseSkinCut(cut);"),"skin cuts are kept per joint and wound set, built once and released with the wounds");
            Check(R("NativeBodySkinModels.cs").Contains("models[id]=parts.ToArray();pilotBodies[id]=built;"),"every body tier keeps its Core mesh for the cuts (no second build)");
            var callers=new (string file,string text,string what)[]
            {
                ("NativeSourcePresentation.cs","sourceModels.WoundedPilot(a.Id,a.Wounds,a.BodyColor,ForceCompatibilityRendering,RigBody(rig,a,first,out bodySkin))","live, first-person, entrance, viewer and killcam rigs: the rig's body at its tier"),
                ("NativeDeathPresentation.cs","sourceModels.WoundedPilot(death.id,death.actor?.Wounds,death.actor?.BodyColor,ForceCompatibilityRendering,BodyAt(death.id,center,out bodySkin))","corpses, ragdolls, the collapse and crush deaths"),
                ("NativeSceneMotion.cs","sourceModels.WoundedPilot(death.id,death.actor?.Wounds,death.actor?.BodyColor,ForceCompatibilityRendering,victimBody)","death scenes"),
                ("NativeSirenDropPresentation.cs","sourceModels.WoundedPilot(death.id,death.actor?.Wounds,death.actor?.BodyColor,ForceCompatibilityRendering,victimBody)","the Siren's crush drop"),
                ("NativeInteractionPresentation.cs","sourceModels.WoundedPilot(r.Id,r.Death.actor?.Wounds,r.Death.actor?.BodyColor,ForceCompatibilityRendering,healed)","the revive scan's wounded body (healed is the pilot's skin model)"),
                ("NativeBisectPresentation.cs","sourceModels.WoundedPilot(death.id,a?.Wounds,a?.BodyColor,ForceCompatibilityRendering,BodyAt(death.id,pelvis,out fx.Skin))","the saw split's halves (the body the corpse draws, cut in two)"),
                ("NativeCyberLordPresentation.cs","sourceModels.Queue(Ghost(BodyModel(BodySkin(g.Actor),BodyTier(rig.Root.transform.position,false)))??pilot,","the Cyber Lord's glitch and fork-bomb ghosts"),
                ("NativeCyberLordPresentation.cs","string shell=Ghost(BodyModel(BodySkin(a.Id),BodyTier(rig.Root.transform.position,false)))??pilot;","the Cyber Lord idle stance's split shimmer"),
                ("NativeKillEffectPresentation.cs","RigCount=NativeBodyModels.Gibs(bodySkin,RigBoxes,RigCount);","the coat panels among the gibs"),
            };
            foreach(var w in callers)Check(R(w.file).Contains(w.text),w.what+" ("+w.file+")");
            Check(Regex.Matches(R("NativeCyberLordPresentation.cs"),@"sourceModels\.Queue\(pilot,").Count==0,"no Cyber Lord ghost draws the Prototype over a body skin");
            int calls=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Sum(f=>Regex.Matches(File.ReadAllText(f),@"\.WoundedPilot\(").Count);
            Check(calls==6,"every WoundedPilot caller is listed and passes its body ("+calls+")");
            Check(R("NativeInteractionPresentation.cs").Contains("string healed=BodyAt(r.Id,"),"the revive's fifth argument is the body skin model at the body's distance");
            var kill=R("NativeKillEffectPresentation.cs");
            Check(!kill.Contains("NativeKillEffectRules.BoxColor(")&&Regex.Matches(kill,@"NativeBodyModels\.GibColor\(bodySkin,").Count==3,"voxel bursts, crush debris and stump caps take the skin's colours");
            // Presentation only: no simulation file reads the skin cut.
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"*.cs").Where(f=>!f.EndsWith("NativeWoundSkin.cs")&&!f.EndsWith("NativeBodyModels.cs")))
                Check(!File.ReadAllText(f).Contains("NativeWoundSkin")&&!File.ReadAllText(f).Contains("WoundLook("),Path.GetFileName(f)+" (rules, hit volumes, motion) never reads the skin cut");
            var sim=new[]{"NativeArena.cs","NativeProjectiles.cs","NativeMelee.cs","NativeDeaths.cs","NativeNetworkFrame.cs","NativeCorpseWorld.cs"};
            foreach(var f in sim)Check(!R(f).Contains("NativeWoundSkin")&&!R(f).Contains("GibColor"),f+" never reads the skin cut or its colours");

            foreach(var line in report)Console.WriteLine("PASS wound skins: "+line);
            Console.WriteLine("PASS wound skins: "+count+" checks (the Prototype's carve pinned to aead19c, the skins' cuts at every tier, their look, kill-box colours, "+callers.Length+" wiring sites)");
        }

        private static bool Outward(WeaponSection m)
        {
            int good=0,bad=0;
            for(int k=0;k+2<m.Indices.Count;k+=3){var a=m.Positions[m.Indices[k]];var b=m.Positions[m.Indices[k+1]];var c=m.Positions[m.Indices[k+2]];var n=m.Normals[m.Indices[k]]+m.Normals[m.Indices[k+1]]+m.Normals[m.Indices[k+2]];var x=Vector3.Cross(b-a,c-a);if(x.LengthSquared()<1e-14f)continue;if(Vector3.Dot(x,n)>0)good++;else bad++;}
            return bad==0;   // a part the hole takes whole (a small trim) is left empty and dropped
        }
        // A column of a carve's grid as a ray in the bank frame: from outside the face, inward along its axis.
        private static (Vector3 o,Vector3 dir) Column(NativeWoundPart d,WoundCarveCells c,int i,int j)
        {
            var p=new double[3];p[c.Ua]=c.Hu+i*c.G;p[c.Va]=c.Hv+j*c.G;p[c.Axis]=c.Face+c.Sign*.12;var dir=new double[3];dir[c.Axis]=-c.Sign;
            return (new Vector3((float)-(p[0]+d.matrix[12]),(float)(p[1]+d.matrix[13]),(float)(p[2]+d.matrix[14])),new Vector3((float)-dir[0],(float)dir[1],(float)dir[2]));
        }
        private static List<(Vector3 a,Vector3 b,Vector3 c)> Triangles(PilotBodyMesh body,int bone,Dictionary<int,WoundSkinMesh> cut,List<WoundSkinMesh> insides,bool only=false)
        {
            var list=new List<(Vector3,Vector3,Vector3)>();
            void Add(WeaponSection m){for(int k=0;k+2<m.Indices.Count;k+=3)list.Add((m.Positions[m.Indices[k]],m.Positions[m.Indices[k+1]],m.Positions[m.Indices[k+2]]));}
            if(!only)for(int p=0;p<body.Parts.Length;p++){if(body.Parts[p].Bone!=bone)continue;Add(cut!=null&&cut.TryGetValue(p,out var m)?m.Mesh:body.Parts[p].Mesh);}
            if(insides!=null)foreach(var m in insides)if(m.Bone==bone)Add(m.Mesh);
            return list;
        }
        /// <summary>The distance to the first front face along the ray (infinity for none).</summary>
        private static double First(List<(Vector3 a,Vector3 b,Vector3 c)> tris,Vector3 o,Vector3 dir)
        {
            double best=double.PositiveInfinity;
            foreach(var t in tris)
            {
                var e1=t.b-t.a;var e2=t.c-t.a;if(Vector3.Dot(Vector3.Cross(e1,e2),dir)>=0)continue;var pv=Vector3.Cross(dir,e2);float det=Vector3.Dot(e1,pv);if(Math.Abs(det)<1e-12f)continue;
                var tv=o-t.a;float u=Vector3.Dot(tv,pv)/det;if(u<-1e-5f||u>1+1e-5f)continue;var q=Vector3.Cross(tv,e1);float v=Vector3.Dot(dir,q)/det;if(v<-1e-5f||u+v>1+1e-5f)continue;float dist=Vector3.Dot(e2,q)/det;if(dist>0&&dist<best)best=dist;
            }
            return best;
        }
    }

    // Deterministic replicated wound lists (NativeArena.Wound's fields: Resolve, then id, at, point and travel in the pilot's
    // source frame) over every wound kind and root, for the Prototype pin and the skin cuts. The scratch digest harness that
    // computed PrototypeDigest from the aead19c code runs exactly these lists.
    internal static class WoundSkinCases
    {
        public sealed class Case{public string Name;public NativeWound[] Wounds;public bool Low;}
        public static List<Case> All()
        {
            int id=100;float at=1;var list=new List<Case>();
            NativeWound W(NativeBodyImpact impact,int part,float damage,bool lethal,bool head,float px,float py,float pz,float dx,float dy,float dz)
            {var w=NativeBodyDamage.Resolve(impact,part,damage,lethal,head);w.id=id++;w.at=at;at+=.25f;w.point=new V3(px,py,pz);w.direction=new V3(dx,dy,dz).Normalized;return w;}
            var pistol=NativeBodyDamage.Projectile("pistol",120,.045f);var rifle=NativeBodyDamage.Projectile("rifle",210,.045f);var mg=NativeBodyDamage.Projectile("machinegun",160,.045f);
            var fire=NativeBodyDamage.Projectile("fire",40,.3f);var sword=NativeBodyDamage.Melee("sword",1);var great=NativeBodyDamage.Melee("greatsword",1);var shield=NativeBodyDamage.Melee("shield",1);var dagger=NativeBodyDamage.Melee("daggers",1);var blast=NativeBodyDamage.Blast(45);
            void Add(string name,bool low,params NativeWound[] w)=>list.Add(new Case{Name=name,Wounds=w,Low=low});
            Add("head-front-pistol",false,W(pistol,5,23,false,true,.02f,.66f,.2f,0,-.05f,-1));
            Add("head-side-rifle",false,W(rifle,5,58,false,true,.25f,.7f,.03f,-1,0,-.1f));
            Add("head-back-mg",false,W(mg,5,8,false,true,-.03f,.6f,-.25f,0,0,1));
            Add("head-top-pistol",false,W(pistol,5,23,false,true,.0f,.9f,.02f,0,-1,-.2f));
            Add("torso-centre-pistol",false,W(pistol,2,23,false,false,0,.2f,.3f,0,0,-1));
            Add("torso-left-rifle",false,W(rifle,2,58,false,false,.13f,.12f,.3f,0,-.1f,-1));
            Add("torso-back-mg",false,W(mg,2,8,false,false,-.08f,.1f,-.3f,0,0,1));
            Add("belly-rifle",false,W(rifle,2,58,false,false,.05f,-.08f,.3f,0,0,-1));
            Add("arm-pistol",false,W(pistol,7,23,false,false,.36f,.1f,.2f,0,0,-1));
            Add("forearm-mg",false,W(mg,8,8,false,false,-.36f,-.16f,.2f,0,0,-1));
            Add("thigh-rifle",false,W(rifle,14,58,false,false,.13f,-.42f,.25f,0,0,-1));
            Add("shin-pistol",false,W(pistol,15,23,false,false,.13f,-.68f,.25f,0,0,-1));
            Add("cut-torso",false,W(great,2,52,false,false,0,.1f,.3f,-1,0,-.3f));
            Add("cut-head",false,W(sword,5,30,false,false,.05f,.68f,.25f,-1,.2f,-.4f));
            Add("dagger-arm",false,W(dagger,11,15,false,false,-.36f,.05f,.2f,1,0,-.4f));
            Add("bruise-torso",false,W(shield,2,23,false,false,0,.15f,.3f,0,0,-1));
            Add("scorch-torso",false,W(fire,2,22,false,false,-.1f,.18f,.3f,0,0,-1));
            Add("blast-back",false,W(blast,2,45,false,false,0,.12f,-.3f,0,0,1));
            Add("head-twice",false,W(pistol,5,23,false,true,.03f,.66f,.2f,0,0,-1),W(pistol,5,23,false,true,-.05f,.7f,.2f,.1f,0,-1));
            Add("eight",false,W(pistol,5,23,false,true,.02f,.64f,.2f,0,0,-1),W(rifle,2,58,false,false,.1f,.15f,.3f,0,0,-1),W(mg,2,8,false,false,-.12f,.05f,.3f,0,0,-1),W(sword,7,30,false,false,.36f,.1f,.2f,-1,0,-.4f),
                W(pistol,14,23,false,false,.13f,-.42f,.25f,0,0,-1),W(mg,18,8,false,false,-.13f,-.4f,.25f,0,0,-1),W(fire,2,22,false,false,0,.2f,-.3f,0,0,1),W(shield,5,23,false,false,.2f,.65f,0,-1,0,0));
            Add("head-front-pistol-low",true,W(pistol,5,23,false,true,.02f,.66f,.2f,0,-.05f,-1));
            Add("torso-left-rifle-low",true,W(rifle,2,58,false,false,.13f,.12f,.3f,0,-.1f,-1));
            return list;
        }
        /// <summary>The wounds a pilot draws (WoundedPilot's filter and order).</summary>
        public static NativeWound[] Open(NativeWound[] w)=>w.Where(x=>NativeBodyDamage.Valid(x)&&!x.destroyed).Take(8).OrderBy(x=>x.at).ThenBy(x=>x.id).ToArray();
        public static WoundVector Body=>NativeWoundCarve.Linear(0xe5e7eb);
        private static string F(double v)=>v.ToString("R",CultureInfo.InvariantCulture);
        public static void Spec(StringBuilder sb,string part,WoundCarveSpec s)
        {sb.Append(part).Append('|').Append(s.axis).Append(s.sign).Append('|');foreach(var v in new[]{s.hit,s.dir,s.size,s.body})sb.Append(F(v.x)).Append(',').Append(F(v.y)).Append(',').Append(F(v.z)).Append(';');sb.Append(s.kind).Append(s.trauma).Append(F(s.radius)).Append(F(s.severity)).Append(F(s.vertical)).Append(s.seed).Append(s.low).Append('\n');}
        public static void Solids(StringBuilder sb,string part,IEnumerable<WoundSolid> solids)
        {sb.Append('#').Append(part).Append('\n');foreach(var s in solids){foreach(var v in new[]{s.min,s.max,s.color})sb.Append(F(v.x)).Append(',').Append(F(v.y)).Append(',').Append(F(v.z)).Append(';');sb.Append(s.flesh?'f':'p').Append('\n');}}
        public static string Hash(string text){using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Take(12).Select(b=>b.ToString("x2")));}
    }
}
#endif
