#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The Chips' looks on the player's model (owner 2026-10-29: "go through the chips that provide visual changes and update them
    // according to the new models (NO VOXEL appearances)"; Core/NativeBodyChipGear, Runtime/NativeBodyChipPresentation.cs). Every
    // Chip alone, and stacks of the body chips, through NativeBodyChips.Build (the source's pieces, RigDressingChecks' parity)
    // and onto the model's parts: every piece becomes a part, each part a built mesh of our own at the model's size, laid on its
    // anchor without crossing the core or another light; the plates and lames mirror; the team's armband is a closed band; the
    // runtime draws the parts on a model pilot (the Classic body keeps the source's pieces, the first-person view body none) and
    // the summoned zombie on the zombie model. Not a render: how the parts sit on each outfit is the owner's PC's to judge.
    internal static class BodyChipModelChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Body chips on the model: "+what);count++;}
        public static void Run(string root,ConversionContent data)
        {
            var built=new Dictionary<string,WeaponMesh>();
            foreach(var id in NativeBodyChipGear.Ids)
            {
                var m=NativeBodyChipGear.Build(id);Check(m!=null&&m.Sections.Count>0&&m.Triangles>0,id+" builds");built[id]=m;
                Bounds(m,out var lo,out var hi);var size=hi-lo;
                // Our own parts at the model's scale: nothing near the voxel pieces' 0.59 m chest plate or 0.26 m arm and boot boxes
                // (the sabaton runs from the ankle's cuff to the toes, a shoe's length).
                Check(Math.Max(size.X,Math.Max(size.Y,size.Z))<=(id==NativeBodyChipGear.Sabaton?.22f:.17f),id+" is model-sized ("+Max(size).ToString("0.000")+" m across)");
                foreach(var s in m.Sections)foreach(var n in s.Normals)Check(Math.Abs(n.Length()-1)<.01f,id+" has unit normals");
            }
            Check(built.Values.Sum(m=>m.Triangles)<4000,"all parts together stay under 4000 triangles ("+built.Values.Sum(m=>m.Triangles)+")");
            foreach(var (l,r) in new[]{(NativeBodyChipGear.PlateL,NativeBodyChipGear.PlateR),(NativeBodyChipGear.PauldronL,NativeBodyChipGear.PauldronR)})
            {
                Bounds(built[l],out var a0,out var a1);Bounds(built[r],out var b0,out var b1);
                Check(Math.Abs(a0.X+b1.X)<1e-5f&&Math.Abs(a1.X+b0.X)<1e-5f&&Vector3.Distance(new Vector3(0,a0.Y,a0.Z),new Vector3(0,b0.Y,b0.Z))<1e-5f&&a0.X+a1.X>0,r+" mirrors "+l+" (the left one on +x, the rig's left)");
            }
            Chest(built);Pieces(data);Armband();Toy();Sources(root);
            Console.WriteLine("PASS body chips on the model: "+count+" checks (eleven parts of our own, every Chip's pieces as parts on the chest core, the shoulders and the ankles; lights clear of the core and of each other; plates and lames mirrored; the team armband; the runtime's model, Classic and first-person routes; the summoned zombie on the zombie model; the Toy Soldier's figure)");
        }
        private static float Max(Vector3 v)=>Math.Max(v.X,Math.Max(v.Y,v.Z));
        private static void Bounds(WeaponMesh m,out Vector3 lo,out Vector3 hi)
        {
            lo=new Vector3(float.MaxValue);hi=new Vector3(float.MinValue);
            foreach(var s in m.Sections)foreach(var p in s.Positions){lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}
        }
        // The chest's lights round the core: off the core's shield, apart from each other, under the plates' inner edge.
        private static void Chest(Dictionary<string,WeaponMesh> built)
        {
            var lights=new List<(Vector2 at,float r,string what)>();
            for(int i=0;i<NativeBodyChipGear.VitalSlots.Length;i++)lights.Add((NativeBodyChipGear.VitalSlots[i],.0095f,"vital "+i));
            for(int i=0;i<NativeBodyChipGear.BlockSlots.Length;i++)lights.Add((NativeBodyChipGear.BlockSlots[i],.011f,"block "+i));
            for(int i=0;i<10;i++)lights.Add((NativeBodyChipGear.StaminaSlot(i),.006f,"stamina "+i));
            // The core: 8.6 cm by 6.5 cm (NativePilotGear.CoreScale), a bevel round it.
            float coreX=.06f*NativePilotGear.CoreScale+.004f,coreY=.045f*NativePilotGear.CoreScale+.004f;
            foreach(var l in lights)Check(Math.Abs(l.at.X)-l.r>coreX||Math.Abs(l.at.Y)-l.r>coreY,l.what+" is off the core");
            for(int i=0;i<lights.Count;i++)for(int j=i+1;j<lights.Count;j++)
                Check(Vector2.Distance(lights[i].at,lights[j].at)>lights[i].r+lights[j].r+.003f,lights[i].what+" and "+lights[j].what+" stand apart");
            // The plates: none of the lights under them (their footprint in the core's plane).
            foreach(var id in new[]{NativeBodyChipGear.PlateL,NativeBodyChipGear.PlateR})
            {
                Bounds(built[id],out var lo,out var hi);
                foreach(var l in lights)Check(l.at.X+l.r<lo.X||l.at.X-l.r>hi.X||l.at.Y+l.r<lo.Y||l.at.Y-l.r>hi.Y,l.what+" is not under "+id);
                Check(Math.Min(Math.Abs(lo.X),Math.Abs(hi.X))>coreX,id+" leaves the core clear");
            }
            // The orbs circle outside the torso (0.15 m to a side, 0.1 m deep from the core) and the arms.
            for(int k=0;k<24;k++){var p=NativeBodyChipGear.OrbAt(k%6,k*.37f);var d=new Vector2(p.X,p.Z-NativeBodyChipGear.OrbitCentre.Z);Check(d.Length()>=.24f,"orb clear of the torso ("+d.Length().ToString("0.00")+")");}
            Check(NativeBodyChipGear.OrbAt(-1,3)==NativeBodyChipGear.Parked&&NativeBodyChipGear.Parked.Z<-.25f,"under the frame plates the orbs park behind the back (as the source's do)");
        }
        // Every Chip alone, and stacks: each source piece becomes model parts on its anchor, nothing lost, nothing allocated.
        private static void Pieces(ConversionContent data)
        {
            var pieces=new BodyChip[NativeBodyChips.MaxPieces];var parts=new NativeBodyChipGear.Part[NativeBodyChipGear.MaxParts];int withPieces=0;var roles=new HashSet<BodyChipRole>();
            var sets=data.chips.Select(c=>new Dictionary<string,int>{[c.id]=1}).ToList();
            sets.Add(new Dictionary<string,int>{["vital"]=6,["frame"]=1,["mana"]=3,["stamina"]=4,["stabilizer"]=1,["dash"]=2,["rebound"]=1,["salvage"]=1});
            sets.Add(new Dictionary<string,int>{["vital"]=3,["mana"]=6,["stamina"]=5,["dash"]=1});
            foreach(var held in sets)
            {
                var known=new Dictionary<string,int>();foreach(var k in held)if(data.chips.Any(c=>c.id==k.Key))known[k.Key]=k.Value;if(known.Count==0)continue;
                var stats=NativeContentRules.Stats(data,"sword",known);int n=NativeBodyChips.Build(known,stats,pieces);if(n==0)continue;withPieces++;
                for(float t=0;t<2;t+=.5f)
                {
                    int m=0;
                    for(int i=0;i<n;i++)
                    {
                        var c=pieces[i];NativeBodyChips.Animate(ref c,t,t<1?.2f:0);int before=m;m=NativeBodyChipGear.Parts(c,t,parts,m);roles.Add(c.Role);
                        if(c.Role==BodyChipRole.Jet)Check(m-before==(c.Visible?2:1),"a dash jet's thruster always, its flame while lit");else Check(m>before,c.Role+" "+c.Index+" becomes a part");
                        for(int k=before;k<m;k++)
                        {
                            var p=parts[k];Check(NativeBodyChipGear.Ids.Contains(p.Id),p.Id+" is a built part");Check(p.Color==c.Piece.Color,"a part keeps its Chip's colour");
                            bool chest=c.Bone==CapturedRig.Chest,arm=c.Bone==CapturedRig.LArm||c.Bone==CapturedRig.RArm;
                            Check(chest==(p.Anchor==NativeBodyChipGear.Anchor.Core),"a chest piece sits on the core's frame");
                            if(arm)Check((c.Bone==CapturedRig.LArm)==(p.Anchor==NativeBodyChipGear.Anchor.LeftArm),"an arm piece on its own shoulder");
                            if(!chest&&!arm)Check((c.Bone==CapturedRig.LFoot)==(p.Anchor==NativeBodyChipGear.Anchor.LeftFoot),"a foot piece on its own ankle");
                            Check(p.Position.Length()<=(c.Role==BodyChipRole.Orb?.45f:.15f),p.Id+" stays by its anchor ("+p.Position.Length().ToString("0.00")+")");
                        }
                    }
                }
            }
            Check(withPieces>10&&roles.Count==Enum.GetValues(typeof(BodyChipRole)).Length,"the Chips cover every kind of piece ("+string.Join(",",roles)+")");
            // A frame: plates on the chest, a pauldron on each shoulder; boots on both feet.
            {
                var stats=NativeContentRules.Stats(data,"sword",new Dictionary<string,int>{["frame"]=1});int n=NativeBodyChips.Build(new Dictionary<string,int>{["frame"]=1},stats,pieces);int m=0;
                for(int i=0;i<n;i++)m=NativeBodyChipGear.Parts(pieces[i],0,parts,m);
                var ids=parts.Take(m).Select(p=>p.Id).ToList();
                Check(ids.Contains(NativeBodyChipGear.PlateL)&&ids.Contains(NativeBodyChipGear.PlateR)&&ids.Contains(NativeBodyChipGear.PauldronL)&&ids.Contains(NativeBodyChipGear.PauldronR),"Frame: two pectoral plates and two pauldrons");
            }
            {
                var held=new Dictionary<string,int>{["vital"]=6,["mana"]=3,["dash"]=1};var stats=NativeContentRules.Stats(data,"sword",held);int n=NativeBodyChips.Build(held,stats,pieces);
                int Run(){int m=0;for(int i=0;i<n;i++){var c=pieces[i];NativeBodyChips.Animate(ref c,1.3f,.1f);m=NativeBodyChipGear.Parts(c,1.3f,parts,m);}return m;}
                Run();long before=GC.GetAllocatedBytesForCurrentThread();int total=0;for(int k=0;k<1000;k++)total+=Run();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(total>0&&bytes==0,"laying the parts allocates nothing ("+bytes+" bytes over 1000 builds)");
            }
        }
        // The armband: a closed band, its faces outward (source frame, the right hand: the Unity boundary mirrors and turns them).
        private static void Armband()
        {
            NativeStatusVisuals.Armband(14,out var pts,out var tris);Check(tris.Length==14*24&&tris.All(i=>i>=0&&i<pts.Length),"the band's triangles");
            float inner=1-NativeStatusVisuals.ArmbandWall;int outside=0,inside=0,top=0,bottom=0;
            for(int i=0;i<tris.Length;i+=3)
            {
                Vector3 a=pts[tris[i]],b=pts[tris[i+1]],c=pts[tris[i+2]];var n=Vector3.Normalize(Vector3.Cross(b-a,c-a));var mid=(a+b+c)/3;var radial=Vector3.Normalize(new Vector3(mid.X,0,mid.Z));
                if(Math.Abs(a.Y-b.Y)<1e-6f&&Math.Abs(b.Y-c.Y)<1e-6f){if(a.Y>0){Check(n.Y>.99f,"the band's top faces up");top++;}else{Check(n.Y<-.99f,"the band's bottom faces down");bottom++;}}
                else{float r=new Vector2(mid.X,mid.Z).Length();if(r>(1+inner)/2){Check(Vector3.Dot(n,radial)>.9f,"the band's outside faces out");outside++;}else{Check(Vector3.Dot(n,radial)<-.9f,"the band's inside faces in");inside++;}}
            }
            Check(outside==28&&inside==28&&top==28&&bottom==28,"walls, top and bottom all closed");
            Check(NativeStatusVisuals.ArmbandRadius>.05f&&NativeStatusVisuals.ArmbandRadius<.07f&&NativeStatusVisuals.ArmbandDrop>.06f&&NativeStatusVisuals.ArmbandDrop<.14f,"round the model's upper arm, below the shoulder");
        }
        // The Toy Soldier's figure: a part on every joint the summon's rig moves, each within its limb, a toy's size.
        private static void Toy()
        {
            var m=NativeToySoldier.Build();var joints=m.Sections.Select(x=>x.Bone).Distinct().ToList();
            foreach(int j in new[]{CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand,CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot})
                Check(joints.Contains(j),"the toy soldier wears a part on "+CapturedRig.Bones[j].Name);
            Check(m.Triangles>800&&m.Triangles<9000,"the toy soldier is a modelled figure ("+m.Triangles+" triangles)");
            foreach(var s in m.Sections)
            {
                var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);foreach(var p in s.Positions){lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}
                // Each part within its joint's reach (the musket, stock to bayonet, half the toy's height; the shako and plume over the head; the epaulettes on the rig's .69 m shoulders).
                float most=s.Bone==CapturedRig.RHand?.95f:s.Bone==CapturedRig.Head?.62f:s.Bone==CapturedRig.Chest?.66f:.5f;Check(Max(hi-lo)<=most,"the toy's "+CapturedRig.Bones[s.Bone].Name+" part fits its joint ("+Max(hi-lo).ToString("0.00")+" m)");
            }
        }
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string dress=R("Runtime/NativeBodyChipPresentation.cs"),models=R("Runtime/NativeBodyChipModels.cs"),skins=R("Runtime/NativeWeaponSkinModels.cs"),status=R("Runtime/NativeStatusPresentation.cs"),src=R("Runtime/NativeSourcePresentation.cs"),summon=R("Runtime/NativeSummonPresentation.cs"),zm=R("Runtime/NativeZombieModelPresentation.cs"),gear=R("Core/NativeBodyChipGear.cs");
            Check(dress.Contains("if(first&&VisualViewDrawn(rig))return;var model=first?null:VisualPosedBody(rig);")&&dress.Contains("if(model!=null){parts=NativeBodyChipGear.Parts(c,time,bodyChipParts,parts);continue;}")
                &&dress.Contains("if(c.Visible)QueuePiece(c.Piece,first?rig.Bones[c.Bone].localToWorldMatrix:VisualJointFrame(rig,c.Bone),layer,camera);")&&dress.Contains("if(model!=null)BodyChipModel(model,rig,parts,layer,camera);"),
                "a model pilot wears the parts, the first-person view body none, the Classic body the source's pieces");
            Check(dress.Contains("if(body.GearCore!=null)return body.GearCore.localToWorldMatrix;")&&dress.Contains("Matrix4x4.TRS(at,turn,Vector3.one*body.Root.transform.lossyScale.x)"),"the chest parts on the core's anchor; the limbs' at the model's joints in the game joints' axes");
            Check(dress.Contains("sourceModels.Queue(p.Id,matrix,null,null,layer,camera,body:NativeArena.ColorOf(p.Color).linear);"),"each part in its Chip's colour");
            Check(dress.Contains("?VisualShoulderFrame(body,rig,LimbOf(p.Anchor)):")&&dress.Contains("Quaternion.Slerp(c.rotation,a.rotation,NativeBodyChipGear.PauldronArm)")&&NativeBodyChipGear.PauldronArm>0&&NativeBodyChipGear.PauldronArm<.5f,"the pauldrons ride the shoulder girdle more than the arm (a raised arm tips them, not lifts them off)");
            Check(skins.Contains("ChipModules();BodyChipParts();")&&models.Contains("foreach(var id in NativeBodyChipGear.Ids)")&&models.Contains("NoShadow=NativeBodyChipGear.Light(id)"),"the bank builds every part once, the lights without shadows");
            Check(status.Contains("var body=VisualPosedBody(rig);if(body!=null){var ring=teamArmbands[team];ring.Add(VisualLimbFrame(body,rig,CapturedRig.LArm)*armbandLocal);ring.Add(VisualLimbFrame(body,rig,CapturedRig.RArm)*armbandLocal);return;}")
                &&status.Contains("FlushStatus(teamBands,0,null);FlushStatus(teamArmbands,0,null);"),"a model pilot's team shows as armbands, not the voxel bands");
            Check(src.Contains("if(add==null&&m.Kind==\"zombie\"&&ZombieModelPose(rig,m.Id,NativeHordeKinds.Shambler,NativeHordeModels.Full,true,false,false,null,0,0,HitFlashCode(m.Id,arena.Time),1,0))continue;")
                &&summon.Contains("if(summon&&!toy&&ZombieModelDeath(rig,death.id,NativeHordeKinds.Shambler,death.lost,0))return true;")&&summon.Contains("if(!toy&&ZombieModelDrawn(e.Target))GoreInk(")
                &&zm.Contains("arena.Pve==null&&arena.Minions.Count==0&&!ZombieSummoner(arena)"),"a summoned zombie lives, falls and bleeds on the zombie model; bodies are made ahead for its summoner");
            Check(src.Contains("sourceModels.Queue(m.Pve==null?SummonModelOf(\"summon-\"+m.Kind):\"horde-\"+variant,")&&summon.Contains("sourceModels.Queue(SummonModelOf(death.model),")
                &&src.Contains("model==\"summon-toy\"&&!ClassicBodies&&VisualBodiesOn&&sourceModels.Has(NativeToySoldier.Model)?NativeToySoldier.Model:model")&&models.Contains("models[NativeToySoldier.Model]=figure.ToArray();"),
                "the Toy Soldier draws its figure, alive and falling, once the player's model is on (the Classic look keeps its blocks)");
            foreach(string w in new[]{"Bozo","BoZo","Synty","Polygon","NativeArena.","Authority"})Check(!gear.Contains(w)&&!R("Core/NativeToySoldier.cs").Contains(w),"no pack names or rules in the parts: "+w);
        }
    }
}
#endif
