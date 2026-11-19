#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The voxel costume (owner 2026-11-18: "The voxel player model + weapons can be selected as skins or costumes instead";
    // docs/VOXEL-RETIREMENT.md 2.3 and V2; Core/NativeLook.cs's costume key, Core/NativeVoxelCostume.cs,
    // Runtime/NativeVoxelCostumePresentation.cs). Checked: the key round-trips (prefs, a login whose saved look lacks it, the
    // host's gate, the seat frame's normaliser, an older client's drop) and is never a sixth slot; it makes another outfit;
    // the precedence (the Top Dog's Hot Dog, then the costume over the DEV Hot Dog top); the voxel set and its restore (the five
    // newer classes untouched); the hide-and-keep table and the runtime's routes for it; each variant and tier built on the
    // synthetic mannequin (VisualRigChecks.Synthetic): every part kept, every vertex rigid on one bone the model has, deterministic,
    // within the voxel skins' triangle budgets; its colours the voxel shader's (albedo and glow within a few percent) through the
    // toon's palette; the attachments on its surface (within 1 cm), hats within 1 cm of their band, the capsule check.
    internal static class CostumeChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Costume: "+what);count++;}
        private static readonly List<string> report=new List<string>();

        public static void Run(string root)
        {
            count=0;report.Clear();
            Look();Console.WriteLine("PASS costume: the look key (after the slots, before the skin; never a sixth slot), its round trips and an older client's drop ("+count+" checks)");
            VoxelSet();Console.WriteLine("PASS costume: the voxel set switches every weapon with a Prototype and restores the picks it replaced; the five newer classes keep Standard ("+count+" checks)");
            Builds(root);Console.WriteLine("PASS costume: every variant and tier on the model's skeleton: rigid, deterministic, in budget, the voxel colours, attachments on its surface, hats on their band, the capsule ("+count+" checks)");
            foreach(var line in report)Console.WriteLine("INFO costume: "+line);report.Clear();
            Precedence(root);Console.WriteLine("PASS costume: the Top Dog keeps its Hot Dog, the costume hides the DEV Hot Dog top and every outfit piece ("+count+" checks)");
            Table(root);Console.WriteLine("PASS costume: the hide-and-keep table and the runtime's routes for every row ("+count+" checks)");
            foreach(var line in report)Console.WriteLine("INFO costume: "+line);
        }

        // ---- the key ----
        private static void Look()
        {
            Check(NativeLook.Slots.Length==5&&NativeLook.SlotIndex(NativeLook.Costume)<0&&NativeLook.Costume=="costume"&&NativeLook.Costumes[0]=="none"&&NativeLook.Costumes[1]=="voxel","a key, never a slot: five slots");
            Check(NativeLook.CostumeOf("")==NativeLook.CostumeNone&&NativeLook.CostumeOf(null)==NativeLook.CostumeNone&&NativeLook.CostumeOf("costume:voxel")==NativeLook.CostumeVoxel&&NativeLook.CostumeOf("costume:nope")==NativeLook.CostumeNone&&NativeLook.CostumeOf("xcostume:voxel,costumex:voxel")==NativeLook.CostumeNone,"read back, unknowns none");
            string a=NativeLook.WithCostume("skin:c48a62,upper:hard-jacket","voxel");
            Check(a=="upper:hard-jacket,costume:voxel,skin:c48a62","written after the slots and before the skin ("+a+")");
            Check(NativeLook.Normalize(" skin:c48a62 , costume : voxel ,upper:hard-jacket,costume:voxel")==a&&NativeLook.Normalize(a)==a,"normalized in place");
            Check(NativeLook.WithCostume(a,"none")=="upper:hard-jacket,skin:c48a62"&&NativeLook.WithCostume(a,"nope")=="upper:hard-jacket,skin:c48a62"&&NativeLook.Normalize("costume:none")=="","none is never written");
            Check(NativeLook.With(a,NativeLook.Feet,"sneakers")=="upper:hard-jacket,feet:sneakers,costume:voxel,skin:c48a62"&&NativeLook.WithSkin(a,0x112233)=="upper:hard-jacket,costume:voxel,skin:112233"&&NativeLook.WithSkin(a,-1)=="upper:hard-jacket,costume:voxel","a slot or tone pick keeps it");
            Check(NativeVoxelCostume.Worn(a)&&!NativeVoxelCostume.Worn("upper:hard-jacket")&&NativeVoxelCostume.With("",true)=="costume:voxel"&&NativeVoxelCostume.With("costume:voxel",false)=="","worn and put on or off");
            // Not DEV gated: the gate keeps it, with or without DEV access, dropping only DEV pieces.
            Check(NativeLook.Gate(a,false)==a&&NativeLook.Gate(a,true)==a&&NativeLook.Gate("upper:sheldon-robe,costume:voxel",false)=="costume:voxel","the gate passes it");
            // Another outfit (the body is dressed again), unlike the skin tone alone.
            Check(!NativeLook.SameOutfit("upper:hard-jacket",a)&&NativeLook.SameOutfit(a,NativeLook.WithSkin(a,0x112233))&&NativeLook.SameOutfit("costume:voxel","costume:voxel,skin:ffffff"),"SameOutfit compares it");
            // A login whose saved look lacks it keeps this PC's pick (until the Worker keeps the key: Astra's VA1); a saved one stays.
            Check(NativeLook.KeepSkin("hair:wild","upper:tee,costume:voxel")=="hair:wild,costume:voxel"&&NativeLook.KeepSkin("hair:wild,costume:voxel","")=="hair:wild,costume:voxel"&&NativeLook.KeepSkin("hair:wild","")=="hair:wild","a login keeps this PC's costume");
            Check(NativeLook.KeepSkin("hair:wild","costume:voxel,skin:c48a62")=="hair:wild,costume:voxel,skin:c48a62","with the tone");
            // The longest map stays far under the contract's limit.
            var longest=string.Join(",",NativeLook.Slots.Select((s,i)=>s+":"+NativeLook.Ids[i].OrderByDescending(x=>x.Length).First()))+",costume:voxel,skin:ffffff";
            Check(NativeLook.Normalize(longest).Length<=NativeLook.MaxLength&&NativeLook.Normalize(longest).Contains("costume:voxel"),"the longest map fits ("+NativeLook.Normalize(longest).Length+" / "+NativeLook.MaxLength+")");
            // An older client's normaliser (the slots and the skin only) drops the key and draws the outfit; the outfit survives.
            string Old(string map)=>string.Join(",",map.Split(',').Where(e=>{int c=e.IndexOf(':');var k=c>0?e.Substring(0,c):"";return NativeLook.SlotIndex(k)>=0||k==NativeLook.Skin;}));
            Check(Old(a)=="upper:hard-jacket,skin:c48a62"&&NativeLook.Normalize(Old(a))==Old(a)&&!NativeVoxelCostume.Worn(Old(a)),"an older client drops it");
            // The slots' readers ignore it (Index allocates nothing and finds the slot's own pick).
            Check(NativeLook.Index(a,NativeLook.Upper)==Array.IndexOf(NativeLook.Ids[1],"hard-jacket")&&NativeLook.Index("costume:voxel",NativeLook.Upper)==0&&NativeRobotSuit.Dress(a).Contains("costume:voxel"),"slots and dressing keep it");
            for(int i=0;i<200;i++)NativeLook.CostumeOf(a);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++){NativeLook.CostumeOf(a);NativeVoxelCostume.Worn(a);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"reading it allocates nothing");
        }

        // ---- precedence ----
        private static void Precedence(string root)
        {
            string dog=NativeLook.With("",NativeLook.Upper,NativeHotDog.Id),both=NativeVoxelCostume.With(dog,true);
            Check(NativeHotDog.Worn(dog)&&!NativeHotDog.Worn(both)&&NativeVoxelCostume.Worn(both),"the costume hides the DEV Hot Dog top");
            Check(NativeRobotSuit.Shown(NativeVoxelCostume.With("",true),false)==0&&NativeRobotSuit.Shown("",false)!=0,"and the robot suit's plates");
            Check(NativeVoxelCostume.Decide(true,true,both)==NativeVoxelCostume.Body.ClassBody&&NativeVoxelCostume.Decide(false,true,both)==NativeVoxelCostume.Body.Map&&NativeVoxelCostume.Decide(false,false,both)==NativeVoxelCostume.Body.Costume&&NativeVoxelCostume.Decide(false,false,dog)==NativeVoxelCostume.Body.Outfit,"class body, map, costume, outfit");
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r\n","\n");
            // The Top Dog's class body: its look always the Hot Dog, the costume taken off.
            Check(R("NativeTopDogPresentation.cs").Contains("dogLooks[look]=dog=NativeVoxelCostume.With(NativeLook.With(look,NativeLook.Upper,NativeHotDog.Id),false);"),"the Top Dog keeps the Hot Dog");
        }

        // ---- the voxel set ----
        private static void VoxelSet()
        {
            foreach(var w in new[]{NativeKatana.Spec,"fangs",NativeGlaive.Spec,NativeTopDog.Spec,NativeBombs.Spec})Check(!NativeVoxelCostume.InSet(w)&&!NativeWeaponSkins.Skinned(w),"no Prototype for "+w+": it keeps its look");
            Check(!NativeVoxelCostume.InSet(CyberLordSpec.Id)&&NativeVoxelCostume.InSet("sword")&&NativeVoxelCostume.InSet("bow"),"the set: every weapon with a Prototype model");
            string start="pistol:arsenal2,sword:prototype,bow:standard,face:visor,body:sheldon";
            var on=NativeVoxelCostume.SetPut(start,out var kept);
            Check(NativeVoxelCostume.SetOn(on)&&!NativeVoxelCostume.SetOn(start)&&!NativeVoxelCostume.SetOn(""),"put on: every one Prototype");
            Check(NativeWeaponSkins.Of(on,NativeWeaponSkins.FaceEntry)=="visor"&&NativeBodySkins.Of(on)==NativeBodySkins.SheldonIndex&&NativeWeaponSkins.Of(on,CyberLordSpec.Id)==NativeWeaponSkins.Standard,"the body, face and Cyber Lord picks untouched");
            var keptList=kept.Split(',');Check(keptList.Contains("pistol:arsenal2")&&!keptList.Any(k=>k.StartsWith("sword:",StringComparison.Ordinal))&&keptList.Contains("rifle:standard"),"it remembers the picks it replaced ("+kept+")");
            Check(on.Length<=NativeWeaponSkins.MaxLength,"the set fits the skin map's limit ("+on.Length+")");
            var off=NativeVoxelCostume.SetTake(on,kept);
            Check(NativeWeaponSkins.Normalize(off)==NativeWeaponSkins.Normalize("pistol:arsenal2,face:visor,body:sheldon"),"taken off: the remembered picks back, the sword (already Prototype before) to Standard ("+off+")");
            // A weapon picked again since keeps its new pick.
            var changed=NativeWeaponSkins.With(on,"rifle",NativeWeaponSkins.Arsenal);
            Check(NativeWeaponSkins.Of(NativeVoxelCostume.SetTake(changed,kept),"rifle")==NativeWeaponSkins.Arsenal,"a later pick stays");
            Check(NativeVoxelCostume.SetTake(on,"")==NativeWeaponSkins.Normalize("face:visor,body:sheldon")&&NativeVoxelCostume.SetTake(on,"pistol:nope")==NativeWeaponSkins.Normalize("face:visor,body:sheldon"),"nothing remembered: Standard");
        }

        // ---- the hide-and-keep table ----
        private static void Table(string root)
        {
            var kept=new[]{NativeVoxelCostume.Piece.Hat,NativeVoxelCostume.Piece.PilotGear,NativeVoxelCostume.Piece.BodyChips,NativeVoxelCostume.Piece.Sheath,NativeVoxelCostume.Piece.HeldWeapon,NativeVoxelCostume.Piece.Harness};
            foreach(NativeVoxelCostume.Piece p in Enum.GetValues(typeof(NativeVoxelCostume.Piece)))Check(NativeVoxelCostume.Keeps(p)==kept.Contains(p),"the table's row "+p);
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r\n","\n");
            var vbp=R("NativeVisualBodyPresentation.cs");var wear=R("NativeVoxelCostumePresentation.cs");
            // The outfit, its cloth, skin, face meshes: off (every skinned piece but the held weapons), simulating nothing, no lift.
            Check(vbp.Contains("if(NativeVoxelCostume.Worn(look))CostumeWear(root,look);")&&wear.Contains("foreach(var s in skins)if(weaponHolder==null||!s.transform.IsChildOf(weaponHolder))s.gameObject.SetActive(false);"),"the outfit and skin hidden");
            Check(vbp.Contains("if(NativeHotDog.Worn(look)||NativeVoxelCostume.Worn(look))simulate=false;"),"no cloth");
            Check(vbp.Contains("if(NativeVoxelCostume.Worn(look))return 0; // (The costume's soles are on the floor"),"no shoe lift");
            Check(R("NativeRobotHelmPresentation.cs").Contains("private int FaceGearOf(int id)=>NativeVoxelCostume.Worn(VisualOutfitOf(id))?NativeWeaponSkins.FaceNone:NativeWeaponSkins.Index(SeatWeaponSkins(id),NativeWeaponSkins.FaceEntry);"),"no face gear or helmet (the head block is the face)");
            Check(R("NativeSheldonHoodPresentation.cs").Contains("if(CostumeOn(root))return null; // (Nor on the voxel costume"),"no cloth hood (the voxel hood it was made for)");
            Check(vbp.Contains("if(!first&&!WearsArmour(a.Id)&&!CostumeOn(body.Root))"),"no back emblem");
            // Kept: the hat on the costume's head, the gear at its anchors, held weapons at the model's hands.
            Check(vbp.Contains("var head=CostumeHatOn(hat,HotDogHatOn(hat,VisualHeadOf(body)));"),"hats ride the costume's hat fit");
            Check(wear.Contains("CostumeAnchors(root,asset);"),"the gear at the costume's anchors");
        }

        // ---- the builds ----
        private static CostumeJoint[] Mannequin(out NativeVisualRig.Rig rig)
        {
            rig=VisualRigChecks.Synthetic(out var world);var at=new Dictionary<string,Vector3>();
            // (The synthetic skeleton is in the pose's body space, source frame; the costume's is the model's root space, Unity frame.)
            for(int i=0;i<rig.Count;i++){var p=world[i];at[rig.Names[i]]=new Vector3(-p.X,p.Y-NativeVisualRig.Floor,p.Z);}
            return NativeVoxelCostume.Joints(rig,n=>at.TryGetValue(n,out var p)?p:(Vector3?)null);
        }
        private static List<CostumePart> Parts(Dictionary<string,List<BisectChecks.Model>> bank,int skin,int tier)
        {
            var o=new List<CostumePart>();
            if(skin==NativeBodySkins.PrototypeIndex){foreach(var m in bank[NativeBodySkins.PrototypeModel])o.Add(new CostumePart(NativeBodySkins.PartIndex(m.Name,m.Bone),m.Bone,m.Mesh));return o;}
            var body=NativeBodyModels.Build(skin,tier);for(int i=0;i<body.Parts.Length;i++)o.Add(new CostumePart(i,body.Parts[i].Bone,body.Parts[i].Mesh));return o;
        }
        private static void Builds(string root)
        {
            var joints=Mannequin(out var rig);var bank=BisectChecks.Bank(root);
            foreach(int j in NativeVoxelCostume.Needs)Check(joints[j].Found,"the mannequin has joint "+CapturedRig.Bones[j].Name);
            Check(NativeVoxelCostume.Build(Parts(bank,0,0),new CostumeJoint[22],0,0)==null,"no costume without the model's joints");
            var tints=new Vector3[9];var glowTints=new Vector3[9];
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.PrototypeIndex,NativeBodySkins.SheldonIndex})
            for(int tier=0;tier<NativeBodySkins.Tiers;tier++)
            {
                if(skin==NativeBodySkins.PrototypeIndex&&tier>0)continue;
                string tag=NativeBodySkins.Labels[skin]+" tier "+tier;var parts=Parts(bank,skin,tier);
                var a=NativeVoxelCostume.Build(parts,joints,skin,tier);var watch=System.Diagnostics.Stopwatch.StartNew();var b=NativeVoxelCostume.Build(Parts(bank,skin,tier),joints,skin,tier);double buildMs=watch.Elapsed.TotalMilliseconds;
                Check(a!=null,tag+" builds");
                // Every part kept (all 48), every vertex rigid on one bone the model has, every array whole.
                var seen=new HashSet<int>(a.Main.Part.Concat(a.Glow.Part));int drawn=parts.Count(p=>p.Mesh.Vertices>0);
                Check(seen.Count==drawn&&drawn>=40,tag+": every drawn part ("+seen.Count+" of "+drawn+")");
                foreach(var m in new[]{a.Main,a.Glow})
                {
                    Check(m.Positions.Count==m.Normals.Count&&m.Positions.Count==m.Uv.Count&&m.Positions.Count==m.Colors.Count&&m.Positions.Count==m.Bone.Count&&m.Indices.Count%3==0&&m.Indices.All(i=>i>=0&&i<m.Positions.Count),tag+" arrays");
                    Check(m.Bone.All(x=>x>=0&&x<m.Bones.Count)&&m.Bones.All(n=>rig.Names.Contains(n)),tag+": each vertex on one model bone");
                    Check(m.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&m.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),tag+": finite, unit normals");
                    Check(m.Colors.All(c=>c.W==1&&(c.X+c.Y+c.Z)==1),tag+": a layer pick a vertex, the outline on");
                    Check(m.Uv.All(u=>u.X>0&&u.X<1&&u.Y>0&&u.Y<1),tag+": uvs in the palette");
                }
                Check(a.Triangles==parts.Sum(p=>p.Mesh.Triangles),tag+": every triangle kept ("+a.Triangles+")");
                Check(a.Triangles<=NativeBodySkins.Budget[tier],tag+" within the voxel skins' budget ("+a.Triangles+" / "+NativeBodySkins.Budget[tier]+")");
                Check(Same(a.Main,b.Main)&&Same(a.Glow,b.Glow)&&a.Palette.SequenceEqual(b.Palette),tag+": deterministic");
                Check(a.Cells>0&&a.Cells<=NativeVoxelCostume.PaletteCells*NativeVoxelCostume.PaletteCells,tag+": the palette holds every paint ("+a.Cells+")");
                // The faces still face out: the winding agrees with the normals.
                int agree=0,total=0;foreach(var m in new[]{a.Main,a.Glow})for(int t=0;t+2<m.Indices.Count;t+=3){var p0=m.Positions[m.Indices[t]];var n=Vector3.Cross(m.Positions[m.Indices[t+1]]-p0,m.Positions[m.Indices[t+2]]-p0);if(n.LengthSquared()<1e-14f)continue;total++;if(Vector3.Dot(n,m.Normals[m.Indices[t]]+m.Normals[m.Indices[t+1]]+m.Normals[m.Indices[t+2]])>0)agree++;}
                Check(agree>=total*.98f,tag+": faces turn out ("+agree+" of "+total+")");
                // Colours: the toon's albedo and glow for each vertex against NativeSourceLit's (the tag's tint over the paint; the
                // ember at its level), with a body tint and an accent tint off the defaults.
                var body=new Vector3(.62f,.9f,1.3f);var accent=new Vector3(1.6f,.4f,.9f);NativeVoxelCostume.Tints(false,body,accent,tints);NativeVoxelCostume.Tints(true,body,accent,glowTints);
                float worstAlbedo=0,worstGlow=0;
                foreach(var part in parts)
                {
                    var src=part.Mesh;
                    for(int v=0;v<src.Positions.Count;v++)
                    {
                        var c=new Vector3(src.Colors[v].X,src.Colors[v].Y,src.Colors[v].Z);var e=src.Emission[v];var tint=Math.Abs(e.W-1)<.5f?body:Math.Abs(e.W-2)<.5f?accent:Vector3.One;
                        var wantAlbedo=c*tint;var wantGlow=new Vector3(e.X,e.Y,e.Z)*tint;
                        // Find this vertex in the build (the same part, the nearest rest place is not needed: shade from its cell and layer).
                        Shade(a,part,v,tints,glowTints,out var albedo,out var glow,out bool glowing);
                        if(!glowing){worstAlbedo=Math.Max(worstAlbedo,(albedo-wantAlbedo).Length()/(wantAlbedo.Length()+.05f));}
                        else if(Math.Abs(e.W-2)<.5f){worstAlbedo=Math.Max(worstAlbedo,(albedo-wantAlbedo).Length()/(wantAlbedo.Length()+.05f));worstGlow=Math.Max(worstGlow,(glow-wantGlow).Length()/(wantGlow.Length()+.05f));}
                    }
                }
                Check(worstAlbedo<.04f,tag+": the voxel colours through the palette (worst "+(worstAlbedo*100).ToString("0.0")+"%)");
                Check(worstGlow<.05f,tag+": the trims, strips and visor glow at their own levels in the accent (worst "+(worstGlow*100).ToString("0.0")+"%)");
                // The attachments: the chest core and back emblem on the costume's faces, the wrist device round its forearm; hats on the
                // head block's band.
                foreach(var (anchor,name) in new[]{(a.Core,"the chest core"),(a.Emblem,"the back emblem")})
                {
                    Check(anchor.Found,tag+": "+name+" found");float d=Distance(a,anchor.At);
                    Check(d<=.01f,tag+": "+name+" within 1 cm of the costume's surface ("+(d*100).ToString("0.00")+" cm)");
                }
                Check(a.Device.Found&&a.Device.Size>.03f&&a.Device.Size<.12f&&Math.Abs(a.Device.Up.Length()-1)<1e-3f,tag+": the wrist device round the right forearm (radius "+a.Device.Size.ToString("0.000")+" m)");
                float ring=Ring(a,a.Device);Check(Math.Abs(ring-a.Device.Size)<=.012f,tag+": the device's band on the forearm block (its surface "+ring.ToString("0.000")+" m out)");
                // Hats: a model hat's band (the model skull's) scaled and moved onto the head block's band, its crown on the block's top.
                var headAt=joints[CapturedRig.Head].At;NativeVoxelCostume.HatFit(a.HeadTop-headAt,a.HeadFront-headAt,a.HeadHalf,out float hs,out var hm);
                var band=new Vector3(NativeModelHatMeshes.BandX,NativeModelHatMeshes.BandY,NativeModelHatMeshes.BandZ)*hs+hm+headAt;
                float side=Distance(a,band,true);Check(side<=.01f,tag+": a hat's band sits within 1 cm of the head block ("+(side*100).ToString("0.00")+" cm)");
                var crown=new Vector3(0,NativeModelHatMeshes.CrownTop,NativeModelHatMeshes.BandZ)*hs+hm+headAt;Check(Math.Abs(crown.Y-a.HeadTop.Y)<.005f,tag+": its crown on the block's top");
                // The capsule check (2.3): at rest the chest, hip and head blocks stay within the hit capsule's radius plus 2 cm of the
                // body's axis (the pilot's capsule stands on the pelvis's line).
                float worstCapsule=0;var axis=new Vector2(joints[CapturedRig.Pelvis].At.X,joints[CapturedRig.Pelvis].At.Z);
                foreach(var m in new[]{a.Main,a.Glow})for(int i=0;i<m.Positions.Count;i++)
                {
                    int bone=Array.IndexOf(new[]{joints[CapturedRig.Pelvis].Bone,joints[CapturedRig.Spine].Bone,joints[CapturedRig.Chest].Bone,joints[CapturedRig.Head].Bone},m.Bones[m.Bone[i]]);if(bone<0)continue;
                    worstCapsule=Math.Max(worstCapsule,(new Vector2(m.Positions[i].X,m.Positions[i].Z)-axis).Length());
                }
                Check(worstCapsule<=.42f+.02f,tag+": the chest, hip and head blocks inside the capsule ("+worstCapsule.ToString("0.000")+" m of .44)");
                // Its proportions on the model: as tall as the model's frame, feet on the floor.
                float top=a.Main.Positions.Concat(a.Glow.Positions).Max(p=>p.Y),bottom=a.Main.Positions.Concat(a.Glow.Positions).Min(p=>p.Y);
                Check(Math.Abs(bottom)<.005f,tag+": its soles on the floor ("+bottom.ToString("0.000")+")");
                if(tier==0)report.Add(tag+": "+a.Triangles+" triangles in two renderers ("+a.Main.Triangles+" + "+a.Glow.Triangles+" glowing), "+a.Cells+" palette cells, "+a.Main.Bones.Count+" bones; "+top.ToString("0.00")+" m tall on the mannequin (the voxel pilot "+NativeVoxelCostume.VoxelHeight+"); worst colour "+(worstAlbedo*100).ToString("0.0")+"%, glow "+(worstGlow*100).ToString("0.0")+"%; capsule reach "+worstCapsule.ToString("0.00")+" m; built in "+buildMs.ToString("0.0")+" ms (.NET 8, once a variant and tier)");
            }
        }
        private static bool Same(CostumeMesh a,CostumeMesh b)=>a.Positions.SequenceEqual(b.Positions)&&a.Normals.SequenceEqual(b.Normals)&&a.Uv.SequenceEqual(b.Uv)&&a.Colors.SequenceEqual(b.Colors)&&a.Indices.SequenceEqual(b.Indices)&&a.Bone.SequenceEqual(b.Bone)&&a.Bones.SequenceEqual(b.Bones);
        // A source vertex's shading in the build: rebuilt here from its paint the way the builder files it (the same cell and layer).
        private static void Shade(CostumeBuild a,CostumePart part,int v,Vector3[] tints,Vector3[] glowTints,out Vector3 albedo,out Vector3 glow,out bool glowing)
        {
            // The vertex's cell and layer: find a built vertex of the same part whose cell matches its paint, by building one alone.
            var one=new WeaponSection{Bone=part.Bone};one.Positions.Add(part.Mesh.Positions[v]);one.Positions.Add(part.Mesh.Positions[v]+Vector3.UnitX*.001f);one.Positions.Add(part.Mesh.Positions[v]+Vector3.UnitY*.001f);
            for(int k=0;k<3;k++){one.Normals.Add(Vector3.UnitZ);one.Colors.Add(part.Mesh.Colors[v]);one.Emission.Add(part.Mesh.Emission[v]);}one.Indices.AddRange(new[]{0,1,2});
            var b=NativeVoxelCostume.Build(new[]{new CostumePart(part.Index,part.Bone,one)},lastJoints,a.Skin,a.Tier);
            glowing=b.Glow.Positions.Count>0;var m=glowing?b.Glow:b.Main;var uv=m.Uv[0];
            int cx=(int)(uv.X*NativeVoxelCostume.PaletteCells),cy=(int)(uv.Y*NativeVoxelCostume.PaletteCells);var cell=b.Palette[cy*NativeVoxelCostume.PaletteCells+cx];
            NativeVoxelCostume.Shade(cell,m.Colors[0],glowing?glowTints:tints,glowing?NativeVoxelCostume.Glow:0,out albedo,out glow);
        }
        private static CostumeJoint[] lastJoints=>cachedJoints??=Mannequin(out _);private static CostumeJoint[] cachedJoints;
        // The nearest distance from a point to the costume's triangles (`side`: only those facing sideways, a band's).
        private static float Distance(CostumeBuild a,Vector3 p,bool side=false)
        {
            float best=float.MaxValue;
            foreach(var m in new[]{a.Main,a.Glow})for(int t=0;t+2<m.Indices.Count;t+=3)
            {
                Vector3 x=m.Positions[m.Indices[t]],y=m.Positions[m.Indices[t+1]],z=m.Positions[m.Indices[t+2]];
                if(side){var n=Vector3.Cross(y-x,z-x);if(n.LengthSquared()<1e-14f||Math.Abs(Vector3.Normalize(n).Y)>.3f)continue;}
                best=Math.Min(best,TriDistance(p,x,y,z));
            }
            return best;
        }
        // How far out from the device's axis the right forearm block's surface lies round it (the mean over eight directions).
        private static float Ring(CostumeBuild a,CostumeAnchor d)
        {
            var up=Vector3.Normalize(d.Up);var u=Vector3.Normalize(Vector3.Cross(up,Math.Abs(up.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var w=Vector3.Cross(up,u);float sum=0;int n=0;
            for(int k=0;k<8;k++)
            {
                float ang=k*(float)Math.PI/4;var dir=u*(float)Math.Cos(ang)+w*(float)Math.Sin(ang);float hit=0;
                foreach(var m in new[]{a.Main,a.Glow})for(int t=0;t+2<m.Indices.Count;t+=3)
                {
                    if(Ray(d.At,dir,m.Positions[m.Indices[t]],m.Positions[m.Indices[t+1]],m.Positions[m.Indices[t+2]],out float h)&&h<.2f)hit=Math.Max(hit,h);
                }
                if(hit>0){sum+=hit;n++;}
            }
            return n>0?sum/n:0;
        }
        private static bool Ray(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c,out float t)
        {
            t=0;var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-9f)return false;float inv=1/det;var s=o-a;float u=Vector3.Dot(s,p)*inv;if(u<0||u>1)return false;
            var q=Vector3.Cross(s,e1);float v=Vector3.Dot(d,q)*inv;if(v<0||u+v>1)return false;t=Vector3.Dot(e2,q)*inv;return t>0;
        }
        private static float TriDistance(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            // Ericson's closest point on a triangle.
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return (p-a).Length();
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return (p-b).Length();
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0){float v=d1/(d1-d3);return (p-(a+ab*v)).Length();}
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return (p-c).Length();
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0){float w=d2/(d2-d6);return (p-(a+ac*w)).Length();}
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0){float w=(d4-d3)/((d4-d3)+(d5-d6));return (p-(b+(c-b)*w)).Length();}
            float denom=1/(va+vb+vc);float vv=vb*denom,ww=vc*denom;return (p-(a+ab*vv+ac*ww)).Length();
        }
    }
}
#endif
