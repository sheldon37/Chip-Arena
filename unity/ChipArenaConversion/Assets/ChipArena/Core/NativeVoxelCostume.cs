using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One costume renderer's mesh: positions and normals in the model's root space at rest (Unity frame: y up, +z the
    /// front, a left limb at -x), each vertex on one model bone (<see cref="Bones"/>[<see cref="Bone"/>]); its uv a palette cell
    /// (<see cref="CostumeBuild.Palette"/>) and its colour the toon's layer pick (alpha 1: the outline draws).</summary>
    public sealed class CostumeMesh
    {
        public readonly List<string> Bones=new List<string>();
        public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
        public readonly List<Vector2> Uv=new List<Vector2>();
        public readonly List<Vector4> Colors=new List<Vector4>();
        public readonly List<int> Indices=new List<int>(),Bone=new List<int>();
        /// <summary>The exported part each vertex came from (NativeBodySkins.Parts' index), for the checks.</summary>
        public readonly List<int> Part=new List<int>();
        public int Triangles=>Indices.Count/3;public int Vertices=>Positions.Count;
    }
    /// <summary>A frame on the costume at rest (root space): where it sits, its outward normal and its up (or along) axis, the
    /// bone it rides and its size (a band's radius; a block's half width).</summary>
    public struct CostumeAnchor
    {
        public Vector3 At,Out,Up;public string Bone;public float Size;public bool Found;
    }
    /// <summary>One built costume (a variant at a tier): the two renderers' meshes, the palette their uvs point into, the head
    /// map for the hats and the gear's anchors.</summary>
    public sealed class CostumeBuild
    {
        public int Skin,Tier;public CostumeMesh Main=new CostumeMesh(),Glow=new CostumeMesh();
        /// <summary>The palette's cells, PaletteCells across and down (linear rgb; unused cells black): a main cell is a voxel
        /// colour, a glow cell a level split (x the lit share, z the glowing share; NativeVoxelCostume.Glow).</summary>
        public readonly Vector3[] Palette=new Vector3[NativeVoxelCostume.PaletteCells*NativeVoxelCostume.PaletteCells];
        public int Cells;
        /// <summary>The head map (the voxel head joint's frame on the costume at rest): a point p of anything made for the voxel
        /// head (the head block's parts, the voxel hats, the Sheldon Head, the Auditor halo) sits at HeadOrigin + p * HeadSize
        /// (root space), so a voxel hat sits on the costume's head block exactly as it sits on the voxel pilot's head.</summary>
        public Vector3 HeadOrigin;public float HeadSize=1;public string HeadBone;
        /// <summary>The head block at rest (root space): its box, and its top's and front's centres.</summary>
        public Vector3 HeadMin,HeadMax,HeadTop,HeadFront;
        public CostumeAnchor Core,Device,Emblem;
        /// <summary>The team bands round the upper arms: their drop below the model's shoulder joint (m) and the upper-arm blocks'
        /// surface there (their mean radius round the arm's axis).</summary>
        public float BandDrop,BandRadius;
        /// <summary>The boots: the model's ankle height (m over the floor), the boots' top over the floor and their half width
        /// round the ankle (the body chips' sabaton and thruster sit on them).</summary>
        public float AnkleY,BootTop,BootHalf,BootBack;
        /// <summary>The shoulder caps' reach out from the model's shoulder joints and their top over them (m; a pauldron sits on them).</summary>
        public float CapReach,CapTop;
        /// <summary>How long the fit took (ms; the Runtime's log).</summary>
        public double BuildMs;
        public int Triangles=>Main.Triangles+Glow.Triangles;
    }
    /// <summary>A voxel part as the bank holds it: the exported part's index (NativeBodySkins.Parts), its joint and its mesh in
    /// that joint's frame (bank frame).</summary>
    public struct CostumePart
    {
        public int Index,Bone;public WeaponSection Mesh;
        public CostumePart(int index,int bone,WeaponSection mesh){Index=index;Bone=bone;Mesh=mesh;}
    }
    /// <summary>The model's joints for the fit: per game joint (CapturedRig), the model bone its parts ride (the bone that
    /// takes the joint's whole turn, NativeVisualRig.Rig.Of) and where the joint sits on the model at rest (its chain's first
    /// bone; root space).</summary>
    public struct CostumeJoint
    {
        public string Bone;public Vector3 At;public bool Found;
        public CostumeJoint(string bone,Vector3 at){Bone=bone;At=at;Found=!string.IsNullOrEmpty(bone);}
    }

    // The voxel costume (owner 2026-11-18: "I want to essentially remove voxel entirely BUT keep its data incase I use it for a
    // map or game mode. The voxel player model + weapons can be selected as skins or costumes instead"; docs/VOXEL-RETIREMENT.md
    // 2.3, approved with every default: O1(a), the voxel blocks fitted onto the new body). The voxel pilot's 48 parts, in the
    // variant the pilot's skin map picks (NativeBodySkins: Standard, Prototype, the DEV Sheldon Clothes), rebuilt once per
    // variant and tier onto the detailed model's own skeleton at rest, every vertex rigid on the model bone of its part's joint,
    // so the costume rides whatever the model plays (its pack clips, stride, hand fits, deaths, cuts and effects) exactly like
    // the model's own clothes do. Worn as one look key, "costume:voxel" (NativeLook.Costume), which already travels per seat
    // into lobbies, killcams, replays and spectators. Engine-free and deterministic (the checks build what the player builds);
    // the Runtime wears it (Runtime/NativeVoxelCostumePresentation.cs). Presentation only: no rule, hit volume, timing, damage,
    // cooldown, movement or network authority reads it.
    //
    // The fit (the plan's fit 1): the long segments (upper arms, forearms, thighs, shins) stretched in their core to the model's
    // bone lengths, with each block's ends (its bevels and end trims, Keep) kept at their size; the torso's blocks set on the
    // model's heights (the hip line, the shoulder line, the neck and head) and narrowed to its hip and shoulder lines; the limbs
    // a little slimmer (ArmGirth, LegGirth) so they sit on the model's narrower frame; the head block on the head bone, smaller
    // (HeadScale) as the model's head is; the joints, hands, feet and shoulder caps rigid at their own size (scaled by their
    // limb's girth) round the model's joints, the feet on the floor.
    //
    // The colours ride the model's toon (no new shader keyword): each renderer's texture is a palette of flat cells (the uv), its
    // vertex colour picks a toon layer and the renderer's nine colours make each layer a tint (Tints): the main renderer's
    // layer 1 the colour as painted, layer 2 the pilot's body tint over it, layer 3 the accent tint (as NativeSourceLit's tags
    // 0, 1, 2 do); the glow renderer's layers carry the glowing paints in their accent channels, which the toon's accent glow
    // lights (the trims, strips and visor in the accent tint at their own levels; the Standard's two status LEDs).
    public static class NativeVoxelCostume
    {
        public const string Id="voxel";
        /// <summary>Whether a look wears the costume (Core/NativeLook.cs's costume key). Allocation free.</summary>
        public static bool Worn(string look)=>NativeLook.CostumeOf(look)==NativeLook.CostumeVoxel;
        /// <summary>The look with the costume put on or off (normalized).</summary>
        public static string With(string look,bool on)=>NativeLook.WithCostume(look,on?Id:NativeLook.Costumes[NativeLook.CostumeNone]);

        // ---- what the costume hides and keeps (docs/VOXEL-RETIREMENT.md 2.3; CostumeChecks cover every row) ----
        public enum Piece { Outfit, Cloth, RobotPlates, FaceGear, Skin, Hat, PilotGear, BodyChips, Sheath, HeldWeapon, Harness, Emblem }
        /// <summary>Whether a costumed pilot still draws a piece of class `p`: the outfit's skinned pieces and their cloth, the robot
        /// suit's plates, the face gear (the head block is the face), the skin, eyes and teeth, and the back emblem (the costume's
        /// own back light is there) are hidden; hats (at the costume's hat fit), the pilot gear and body chips (at its anchors),
        /// the Katana's saya and the Bombadier's belt, the held weapons (at the model's hands: a Prototype pick draws its voxel
        /// weapon there) and the pod's harness grips (the model's hands and seat) are kept.</summary>
        public static bool Keeps(Piece p)=>p==Piece.Hat||p==Piece.PilotGear||p==Piece.BodyChips||p==Piece.Sheath||p==Piece.HeldWeapon||p==Piece.Harness;
        /// <summary>The order the body's look is decided in (2.3, Precedence): a class body (the Top Dog's Hot Dog; the Siren's own),
        /// then a map's bodies key, then the costume, then the outfit (the DEV Hot Dog top included: the costume hides it).</summary>
        public enum Body { ClassBody, Map, Costume, Outfit }
        public static Body Decide(bool classBody,bool mapForces,string look)=>classBody?Body.ClassBody:mapForces?Body.Map:Worn(look)?Body.Costume:Body.Outfit;

        // ---- the voxel set (owner O2: every weapon that has a Prototype, the Katana, Fangs, Glaive, Top Dog and Bombadier keeping
        // their own look; remembered and restored on this PC only) ----
        /// <summary>The weapons the voxel set switches: every skinned weapon with a Prototype Series model (all of
        /// NativeWeaponSkins.Weapons but the Cyber Lord's look). The five newer classes have no entry there, so they keep Standard.</summary>
        public static bool InSet(string spec)=>NativeWeaponSkins.Modelled(spec);
        /// <summary>Whether a skin map has the voxel set on: every weapon in the set on the Prototype Series.</summary>
        public static bool SetOn(string skins)
        {
            foreach(var w in NativeWeaponSkins.Weapons)if(InSet(w)&&NativeWeaponSkins.Pick(skins,w)!=NativeWeaponSkins.PrototypeIndex)return false;
            return true;
        }
        /// <summary>The voxel set put on: every weapon in the set on the Prototype Series; `kept` gets the pick each one had
        /// ("spec:key" for every weapon in the set, Arsenal picks included, and "prototype" for one already on it), which stays on
        /// this PC (never sent). A weapon already on the Prototype that `remembered` (the list an earlier put left) names keeps
        /// that entry, so a second tap after one weapon was picked again merges into the list instead of losing the rest
        /// (review 2026-11-19).</summary>
        public static string SetPut(string skins,string remembered,out string kept)
        {
            var sb=new System.Text.StringBuilder();string map=NativeWeaponSkins.Normalize(skins);
            foreach(var w in NativeWeaponSkins.Weapons)
            {
                if(!InSet(w))continue;int k=NativeWeaponSkins.Pick(map,w);
                string key=k!=NativeWeaponSkins.PrototypeIndex?NativeWeaponSkins.KeysFor(w)[k]:Remembered(remembered,w)??NativeWeaponSkins.Prototype;
                if(sb.Length>0)sb.Append(',');sb.Append(w).Append(':').Append(key);map=NativeWeaponSkins.With(map,w,NativeWeaponSkins.Prototype);
            }
            kept=sb.ToString();return map;
        }
        /// <summary>The voxel set put on with nothing remembered.</summary>
        public static string SetPut(string skins,out string kept)=>SetPut(skins,null,out kept);
        /// <summary>The voxel set taken off: each weapon the set switched back to the pick `kept` remembers (a weapon that was on
        /// the Prototype before stays on it; Standard where it remembers none; a weapon picked again since, off the Prototype,
        /// keeps its new pick).</summary>
        public static string SetTake(string skins,string kept)
        {
            string map=NativeWeaponSkins.Normalize(skins);
            foreach(var w in NativeWeaponSkins.Weapons)
            {
                if(!InSet(w)||NativeWeaponSkins.Pick(map,w)!=NativeWeaponSkins.PrototypeIndex)continue;
                map=NativeWeaponSkins.With(map,w,Remembered(kept,w)??NativeWeaponSkins.Standard);
            }
            return map;
        }
        private static string Remembered(string kept,string spec)
        {
            if(string.IsNullOrEmpty(kept))return null;string found=null;
            foreach(var e in kept.Split(','))
            {
                int c=e.IndexOf(':');if(c<=0||e.Substring(0,c)!=spec)continue;var key=e.Substring(c+1);
                if(Array.IndexOf(NativeWeaponSkins.KeysFor(spec),key)>=0)found=key;
            }
            return found;
        }

        // ---- the fit ----
        /// <summary>The limbs' girth on the model's frame (a share of the voxel limb's thickness), the hands' and feet's size,
        /// the head block's size (the voxel head is 34 cm across and 40 tall; the model's skull with its hair about 24 by 24),
        /// and the torso's depth.</summary>
        public const float ArmGirth=.72f,LegGirth=.76f,HandScale=.82f,FootScale=.92f,HeadScale=.74f,TorsoDepth=.82f;
        /// <summary>The head block on the model's skull (review 2026-11-19: at .8 and on the bone its top stood 8 cm over the
        /// model's crown, so the clips made for the model's head put the Katana's overhead guard, its hands and its handle inside
        /// the block): its top this far over the head bone (the model skull's top, 21.2 cm, NativeModelHatMeshes, and HeadClear
        /// of room), its middle this far ahead of the bone (the skull's, NativeModelHatMeshes.BandZ).</summary>
        public const float SkullTop=.212f,HeadClear=.013f,SkullMid=.025f;
        /// <summary>The boots on the model's ankles (review 2026-11-19: the boots kept the voxel's heights, 2-4 cm under the
        /// model's ankle, so a flexed ankle opened a gap of floor between the shin and its boot): a boot's height up to the
        /// voxel's ankle stretched to the model's ankle, the collar above it BootCollar as tall, and the shin reaching ShinTuck
        /// past the model's ankle into the boot.</summary>
        public const float BootCollar=1.6f,ShinTuck=.03f;
        /// <summary>The team band's drop below the model's shoulder joint (under the costume's shoulder cap) and its clearance.</summary>
        public const float BandDropOnCostume=.13f,BandClear=.004f;
        /// <summary>How far past the model's shoulder joints the chest block reaches, and past its hip joints the hip block (m): the
        /// chest a little wider than the hips, as the voxel pilot's is (.272 against .225).</summary>
        public const float ShoulderReach=.045f,HipReach=.06f;
        /// <summary>The end of a block kept at its size when it stretches (its bevels and end trims; m), at most a third of it.</summary>
        public const float Keep=.022f;
        /// <summary>The palette: cells across and down, texels a cell side (the texture is Cells x Texels square).</summary>
        public const int PaletteCells=16,PaletteTexels=8;
        /// <summary>The glow renderer's accent glow (the toon's _AccentGlow): the strongest paint's level; each glowing paint's cell
        /// splits its colour into a lit share and a glowing share, so its glow over its albedo is its own level.</summary>
        public const float Glow=2.6f;
        /// <summary>The voxel pilot's height (its head block's top at rest, m), for the review sheet's comparisons.</summary>
        public const float VoxelHeight=1.725f;

        /// <summary>A part's class for the fit, by its exported name.</summary>
        private enum Kind { Torso, Neck, Head, Cap, Limb, Rigid, Hand, Foot }
        private static Kind KindOf(string name,int bone)
        {
            if(bone==CapturedRig.Head)return Kind.Head;
            if(bone==CapturedRig.Neck)return Kind.Neck;
            if(bone==CapturedRig.Pelvis||bone==CapturedRig.Spine||bone==CapturedRig.Chest)return Kind.Torso;
            if(bone==CapturedRig.LClav||bone==CapturedRig.RClav)return Kind.Cap;
            if(bone==CapturedRig.LHand||bone==CapturedRig.RHand)return Kind.Hand;
            if(bone==CapturedRig.LFoot||bone==CapturedRig.RFoot||bone==CapturedRig.LToe||bone==CapturedRig.RToe)return Kind.Foot;
            // The joints and plates round a joint keep their size; the segments' shells stretch.
            if(name.EndsWith("-joint",StringComparison.Ordinal)||name.EndsWith("kneecap",StringComparison.Ordinal))return Kind.Rigid;
            return Kind.Limb;
        }
        /// <summary>The voxel rig's joints at rest in the model's root space (Unity frame: the source's x mirrored, the floor at 0).</summary>
        public static Vector3 VoxelJoint(int j)
        {
            var p=Vector3.Zero;for(int k=j;k>=0;k=CapturedRig.Bones[k].Parent)p+=CapturedRig.Bones[k].Position;
            return new Vector3(-p.X,p.Y-NativeVisualRig.Floor,p.Z);
        }
        private static readonly int[] Segment={-1,-1,-1,-1,-1,-1,-1,CapturedRig.LElbow,CapturedRig.LHand,-1,-1,CapturedRig.RElbow,CapturedRig.RHand,-1,CapturedRig.LKnee,CapturedRig.LFoot,-1,-1,CapturedRig.RKnee,CapturedRig.RFoot,-1,-1};
        /// <summary>The joints the costume needs on the model (every one its parts ride, and the arms' and legs' ends).</summary>
        public static readonly int[] Needs={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,
            CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand,CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.LToe,CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot,CapturedRig.RToe};

        /// <summary>The model's joints for the fit, from its rig (NativeVisualRig.Rig: each joint's chain of bones) and its bones'
        /// rest places by name (root space; null for a bone it lacks): each joint's bone the last of its chain, its place the first's.</summary>
        public static CostumeJoint[] Joints(NativeVisualRig.Rig rig,Func<string,Vector3?> rest)
        {
            var o=new CostumeJoint[CapturedRig.Bones.Length];if(rig==null||rest==null)return o;
            for(int j=0;j<o.Length;j++)
            {
                int of=j<rig.Of.Length?rig.Of[j]:-1;if(of<0)continue;int start=of;
                for(int b=0;b<rig.Count;b++)if(rig.ChainStart[b]&&rig.Joint[b]==j){start=b;break;}
                var at=rest(rig.Names[start]);if(at!=null)o[j]=new CostumeJoint(rig.Names[of],at.Value);
            }
            return o;
        }
        /// <summary>The costume of `parts` (one variant at one tier: every exported part, each in its joint's frame) on the
        /// model whose joints are `joints` (22, CapturedRig order); null when a joint it needs is missing.</summary>
        public static CostumeBuild Build(IList<CostumePart> parts,CostumeJoint[] joints,int skin,int tier)
        {
            if(parts==null||joints==null||joints.Length<CapturedRig.Bones.Length)return null;
            foreach(int j in Needs)if(!joints[j].Found)return null;
            var fit=new Fit(joints);var build=new CostumeBuild{Skin=skin,Tier=tier,HeadBone=joints[CapturedRig.Head].Bone};
            // The head map first: the head's parts scaled as one block about their common middle, on the model's skull.
            var headMin=new Vector3(float.MaxValue);var headMax=new Vector3(float.MinValue);
            foreach(var part in parts)if(part.Mesh!=null&&part.Bone==CapturedRig.Head)foreach(var p in part.Mesh.Positions){headMin=Vector3.Min(headMin,p);headMax=Vector3.Max(headMax,p);}
            if(headMin.X>headMax.X)headMin=headMax=Vector3.Zero; // (a build of other parts alone: the map round the bone)
            fit.Head(headMin,headMax,out build.HeadOrigin,out build.HeadSize);
            var palette=new Dictionary<(int,int,int),int>();
            foreach(var part in parts)
            {
                if(part.Mesh==null||part.Bone<0||part.Bone>=joints.Length)continue;
                var spec=part.Index>=0&&part.Index<NativeBodySkins.Parts.Length?NativeBodySkins.Parts[part.Index]:default;
                var kind=KindOf(spec.Name??"",part.Bone);var m=part.Mesh;
                // The part's extent in its own frame (its stretch keeps its ends).
                var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);
                foreach(var p in m.Positions){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
                if(m.Positions.Count==0)continue;
                // Each triangle goes to the main renderer or, when its paint glows, the glow renderer; its vertices with it.
                var into=new int[m.Positions.Count];for(int i=0;i<into.Length;i++)into[i]=-1;
                var glowInto=new int[m.Positions.Count];for(int i=0;i<glowInto.Length;i++)glowInto[i]=-1;
                uint seed=Hash((uint)(part.Index*7919+part.Bone*104729+skin*31+tier));
                for(int t=0;t+2<m.Indices.Count;t+=3)
                {
                    bool glows=Glows(m,m.Indices[t]);var target=glows?build.Glow:build.Main;var map=glows?glowInto:into;
                    for(int c=0;c<3;c++)
                    {
                        int v=m.Indices[t+c];
                        if(map[v]<0)
                        {
                            fit.Place(kind,part.Bone,m.Positions[v],m.Normals.Count>v?m.Normals[v]:Vector3.UnitY,min,max,out var at,out var normal);
                            map[v]=target.Positions.Count;target.Positions.Add(at);target.Normals.Add(normal);target.Part.Add(part.Index);
                            target.Bone.Add(BoneIndex(target,joints[part.Bone].Bone));
                            Paint(build,palette,m,v,glows,seed,out var uv,out var layer);target.Uv.Add(uv);target.Colors.Add(layer);
                        }
                        target.Indices.Add(map[v]);
                    }
                }
                // The chest core on the sternum plate's face; the wrist device round the right forearm's cuff end.
                if(spec.Name=="sternum")
                {
                    fit.Place(kind,part.Bone,new Vector3((min.X+max.X)*.5f,(min.Y+max.Y)*.5f,max.Z),Vector3.UnitZ,min,max,out var at,out var n);
                    build.Core=new CostumeAnchor{At=at,Out=Vector3.Normalize(n),Up=Vector3.UnitY,Bone=joints[part.Bone].Bone,Size=(max.X-min.X)*.5f,Found=true};
                }
                if(spec.Name=="back-panel")
                {
                    fit.Place(kind,part.Bone,new Vector3((min.X+max.X)*.5f,(min.Y+max.Y)*.5f,min.Z),-Vector3.UnitZ,min,max,out var at,out var n);
                    build.Emblem=new CostumeAnchor{At=at,Out=Vector3.Normalize(n),Up=Vector3.UnitY,Bone=joints[part.Bone].Bone,Size=(max.X-min.X)*.5f,Found=true};
                }
                if(spec.Name=="right-forearm")
                {
                    // Round the forearm a little above the wrist (the voxel forearm runs down from the elbow; its far end is the wrist's).
                    var mid=new Vector3((min.X+max.X)*.5f,min.Y+(max.Y-min.Y)*.22f,(min.Z+max.Z)*.5f);
                    fit.Place(kind,part.Bone,mid,Vector3.UnitZ,min,max,out var at,out _);
                    fit.Place(kind,part.Bone,mid-Vector3.UnitY*.1f,Vector3.UnitZ,min,max,out var below,out _);
                    float r=Math.Max(max.X-min.X,max.Z-min.Z)*.5f*ArmGirth;
                    build.Device=new CostumeAnchor{At=at,Out=Vector3.UnitZ,Up=SafeUnit(at-below,Vector3.UnitY),Bone=joints[part.Bone].Bone,Size=r,Found=true};
                }
            }
            build.Cells=palette.Count;
            // The head block's box at rest.
            build.HeadMin=build.HeadOrigin+headMin*build.HeadSize;build.HeadMax=build.HeadOrigin+headMax*build.HeadSize;
            build.HeadTop=new Vector3((build.HeadMin.X+build.HeadMax.X)*.5f,build.HeadMax.Y,(build.HeadMin.Z+build.HeadMax.Z)*.5f);
            build.HeadFront=new Vector3(build.HeadTop.X,(build.HeadMin.Y+build.HeadMax.Y)*.5f,build.HeadMax.Z);
            Bands(build,joints);Boots(build,joints);
            return build;
        }
        /// <summary>The team bands' place round the upper-arm blocks: BandDropOnCostume below each shoulder joint, at the blocks'
        /// mean surface round the arm's axis (both arms' mean).</summary>
        private static void Bands(CostumeBuild build,CostumeJoint[] j)
        {
            build.BandDrop=BandDropOnCostume;float sum=0;int n=0;
            foreach(var (arm,elbow) in new[]{(CapturedRig.LArm,CapturedRig.LElbow),(CapturedRig.RArm,CapturedRig.RElbow)})
            {
                var axis=SafeUnit(j[elbow].At-j[arm].At,-Vector3.UnitY);var at=j[arm].At+axis*BandDropOnCostume;
                float r=Ring(build,at,axis,j[arm].Bone);if(r>0){sum+=r;n++;}
            }
            build.BandRadius=n>0?sum/n+BandClear:0;
        }
        /// <summary>The boots' top over the floor, their half width round the ankle and the heel's reach behind it (m; the left
        /// boot's, mirrored on the right).</summary>
        private static void Boots(CostumeBuild build,CostumeJoint[] j)
        {
            build.AnkleY=(j[CapturedRig.LFoot].At.Y+j[CapturedRig.RFoot].At.Y)*.5f;string heel=j[CapturedRig.LFoot].Bone,toe=j[CapturedRig.LToe].Bone;
            float top=0,half=0,back=0;var ankle=j[CapturedRig.LFoot].At;
            foreach(var m in new[]{build.Main,build.Glow})for(int i=0;i<m.Positions.Count;i++)
            {
                var b=m.Bones[m.Bone[i]];if(b!=heel&&b!=toe)continue;var p=m.Positions[i];
                top=Math.Max(top,p.Y);if(b==heel){half=Math.Max(half,Math.Abs(p.X-ankle.X));back=Math.Max(back,ankle.Z-p.Z);}
            }
            build.BootTop=top;build.BootHalf=half;build.BootBack=back;
            // The shoulder caps round the model's shoulder joints.
            float reach=0,capTop=0;
            foreach(var (clav,arm) in new[]{(CapturedRig.LClav,CapturedRig.LArm),(CapturedRig.RClav,CapturedRig.RArm)})
            {
                var at=j[arm].At;float outward=Math.Sign(at.X-j[CapturedRig.Chest].At.X);
                foreach(var m in new[]{build.Main,build.Glow})for(int i=0;i<m.Positions.Count;i++)
                {
                    int part=m.Part[i];if(part<0||part>=NativeBodySkins.Parts.Length||NativeBodySkins.Parts[part].Bone!=clav)continue;var p=m.Positions[i];
                    reach=Math.Max(reach,(p.X-at.X)*outward);capTop=Math.Max(capTop,p.Y-at.Y);
                }
            }
            build.CapReach=reach;build.CapTop=capTop;
        }
        /// <summary>The mean distance from the axis point `at` (direction `axis`) out to the costume's surface, over eight
        /// directions round it, on the triangles riding bone `bone` (0 where none is hit within 30 cm).</summary>
        public static float Ring(CostumeBuild build,Vector3 at,Vector3 axis,string bone)
        {
            var up=SafeUnit(axis,Vector3.UnitY);var u=SafeUnit(Vector3.Cross(up,Math.Abs(up.Y)<.9f?Vector3.UnitY:Vector3.UnitX),Vector3.UnitX);var w=Vector3.Cross(up,u);float sum=0;int n=0;
            for(int k=0;k<8;k++)
            {
                float ang=k*(float)Math.PI/4;var dir=u*(float)Math.Cos(ang)+w*(float)Math.Sin(ang);
                if(Surface(build,at,dir,bone,out float hit)&&hit<.3f){sum+=hit;n++;}
            }
            return n>0?sum/n:0;
        }
        /// <summary>How far from `from` along the unit direction `dir` the costume's surface lies (its farthest crossing on the
        /// triangles riding `bone`, any bone for null): where the ray leaves the blocks. False when it crosses none.</summary>
        public static bool Surface(CostumeBuild build,Vector3 from,Vector3 dir,string bone,out float distance)
        {
            distance=0;bool hit=false;
            foreach(var m in new[]{build.Main,build.Glow})
            {
                int bi=bone==null?-1:m.Bones.IndexOf(bone);if(bone!=null&&bi<0)continue;
                for(int t=0;t+2<m.Indices.Count;t+=3)
                {
                    int a=m.Indices[t],b=m.Indices[t+1],c=m.Indices[t+2];if(bi>=0&&(m.Bone[a]!=bi||m.Bone[b]!=bi||m.Bone[c]!=bi))continue;
                    if(Ray(from,dir,m.Positions[a],m.Positions[b],m.Positions[c],out float h)&&h>distance){distance=h;hit=true;}
                }
            }
            return hit;
        }
        /// <summary>A point seated on the costume: `at` moved along the level line from `centre` through it until it lies
        /// `clear` outside the surface of the blocks riding `bone` (pushed out of a block it was in, or pulled in to one it
        /// floated off); `at` itself where that line meets no block.</summary>
        public static Vector3 Seat(CostumeBuild build,Vector3 at,Vector3 centre,float clear,string bone)
        {
            var d=at-centre;d.Y=0;float l=d.Length();if(l<1e-5f)return at;var dir=d/l;
            var from=new Vector3(centre.X,at.Y,centre.Z);if(!Surface(build,from,dir,bone,out float surface))return at;
            return from+dir*(surface+clear);
        }
        private static bool Ray(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c,out float t)
        {
            t=0;var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-9f)return false;float inv=1/det;var s=o-a;float u=Vector3.Dot(s,p)*inv;if(u<0||u>1)return false;
            var q=Vector3.Cross(s,e1);float v=Vector3.Dot(d,q)*inv;if(v<0||u+v>1)return false;t=Vector3.Dot(e2,q)*inv;return t>0;
        }
        private static int BoneIndex(CostumeMesh m,string bone){int i=m.Bones.IndexOf(bone);if(i<0){i=m.Bones.Count;m.Bones.Add(bone);}return i;}
        private static Vector3 SafeUnit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-7f?v/l:fallback;}
        private static uint Hash(uint x){x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return x;}

        // ---- the paints ----
        /// <summary>Whether vertex `v`'s paint glows (an accent paint with an ember, or a fixed-colour light).</summary>
        private static bool Glows(WeaponSection m,int v)
        {
            if(v>=m.Emission.Count||v>=m.Colors.Count)return false;var e=m.Emission[v];
            float emit=e.X+e.Y+e.Z;if(emit<=1e-5f)return false;
            if(Math.Abs(e.W-2)<.5f)return true;
            // An untinted light glows well past its albedo (the Standard's LEDs: 2.2 over .3); a tinted shell's faint self light does not.
            var c=m.Colors[v];float lit=c.X+c.Y+c.Z;return e.W<.5f&&emit>lit*1.5f;
        }
        /// <summary>The level a glowing accent paint glows at over its albedo (1.4 trims, 2.4 strips, 2.6 the visor).</summary>
        private static float Level(WeaponSection m,int v)
        {
            var c=m.Colors[v];var e=m.Emission[v];float lit=Math.Max(1e-5f,c.X+c.Y+c.Z);return Math.Min(Glow,(e.X+e.Y+e.Z)/lit);
        }
        /// <summary>The colour of the accent paints (NativeBodyModels.AccentColor, the export's): every variant's tag-2 albedo.</summary>
        public static readonly Vector3 AccentBase=new Vector3(.044f,.505f,.41f);
        /// <summary>The two status LEDs' light colours (the Standard's cyan and magenta, NativeBodySkins.CyberCyan / CyberMagenta).</summary>
        public static readonly Vector3 LedCyan=NativePortraitRules.Linear(NativeBodySkins.CyberCyan),LedMagenta=NativePortraitRules.Linear(NativeBodySkins.CyberMagenta);
        private static readonly Vector4 Layer1=new Vector4(1,0,0,1),Layer2=new Vector4(0,1,0,1),Layer3=new Vector4(0,0,1,1);
        private static void Paint(CostumeBuild build,Dictionary<(int,int,int),int> palette,WeaponSection m,int v,bool glows,uint seed,out Vector2 uv,out Vector4 layer)
        {
            var c=v<m.Colors.Count?m.Colors[v]:Vector4.One;var e=v<m.Emission.Count?m.Emission[v]:Vector4.Zero;Vector3 cell;
            if(!glows)
            {
                // Layer 1 as painted, 2 in the body tint, 3 in the accent tint (NativeSourceLit's tags 0, 1, 2).
                layer=Math.Abs(e.W-1)<.5f?Layer2:Math.Abs(e.W-2)<.5f?Layer3:Layer1;cell=new Vector3(c.X,c.Y,c.Z);
            }
            else if(Math.Abs(e.W-2)<.5f)
            {
                // The accent paints: their colour in the renderer's layer 1 (lit and glowing channels both), the cell its split.
                float level=Level(m,v);layer=Layer1;cell=new Vector3(1-level/Glow,0,level/Glow);
            }
            else
            {
                // A fixed light: the cyan LED's layer 2, the magenta's 3 (the nearer of the two), all glowing.
                var light=Vector3.Normalize(new Vector3(e.X,e.Y,e.Z)+new Vector3(1e-6f));
                layer=Vector3.Distance(light,Vector3.Normalize(LedCyan))<=Vector3.Distance(light,Vector3.Normalize(LedMagenta))?Layer2:Layer3;cell=new Vector3(0,0,1);
            }
            // One cell a paint (quantised so a colour's vertices share it), the vertex at one of its inner texels picked by its part,
            // so the dissolve takes the costume block by block.
            var key=(Q(cell.X)+(glows?4096:0),Q(cell.Y),Q(cell.Z));
            if(!palette.TryGetValue(key,out int index)){index=palette.Count;if(index<build.Palette.Length)build.Palette[index]=cell;palette[key]=index;}
            index=Math.Min(index,build.Palette.Length-1);
            int cx=index%PaletteCells,cy=index/PaletteCells;int tx=1+(int)(seed%(PaletteTexels-2)),ty=1+(int)((seed/7)%(PaletteTexels-2));
            float size=PaletteCells*PaletteTexels;uv=new Vector2((cx*PaletteTexels+tx+.5f)/size,(cy*PaletteTexels+ty+.5f)/size);
        }
        private static int Q(float x)=>(int)Math.Round(Math.Max(0,x)*2048);

        /// <summary>The nine toon colours of a renderer (raw linear values: the Runtime writes them as vectors) for a pilot whose
        /// body tint is `body` and accent tint `accent` (NativeSourceLit's _BodyTint and _AccentTint, ratios over the defaults):
        /// the main renderer's identity, body and accent layers; the glow renderer's accent paint (lit and glowing alike) and its
        /// two LEDs (glowing only).</summary>
        public static void Tints(bool glow,Vector3 body,Vector3 accent,Vector3[] into)
        {
            if(!glow)
            {
                into[0]=Vector3.UnitX;into[1]=Vector3.UnitY;into[2]=Vector3.UnitZ;
                into[3]=new Vector3(body.X,0,0);into[4]=new Vector3(0,body.Y,0);into[5]=new Vector3(0,0,body.Z);
                into[6]=new Vector3(accent.X,0,0);into[7]=new Vector3(0,accent.Y,0);into[8]=new Vector3(0,0,accent.Z);
                return;
            }
            var paint=AccentBase*accent;
            into[0]=paint;into[1]=Vector3.Zero;into[2]=paint;
            into[3]=Vector3.Zero;into[4]=Vector3.Zero;into[5]=LedCyan*(2.2f/Glow);
            into[6]=Vector3.Zero;into[7]=Vector3.Zero;into[8]=LedMagenta*(2.2f/Glow);
        }
        /// <summary>What the toon draws for a vertex (its albedo and emission before the light; NativeVisualBodyToon VisualColours
        /// and VisualAccentPart, the cell as the texture's weights): the checks compare it with NativeSourceLit's.</summary>
        public static void Shade(Vector3 cell,Vector4 layer,Vector3[] tints,float accentGlow,out Vector3 albedo,out Vector3 emission)
        {
            Vector3 L(int k)=>tints[k*3]*cell.X+tints[k*3+1]*cell.Y+tints[k*3+2]*cell.Z;
            Vector3 one=L(0),two=L(1),three=L(2);albedo=Vector3.Lerp(Vector3.Lerp(one*layer.X,two,layer.Y),three,layer.Z);
            var accentPart=Vector3.Lerp(Vector3.Lerp(tints[2]*cell.Z*layer.X,tints[5]*cell.Z,layer.Y),tints[8]*cell.Z,layer.Z);emission=accentPart*accentGlow;
        }

        // ---- the fit, on the model's joints ----
        private sealed class Fit
        {
            private readonly CostumeJoint[] j;private readonly Vector3[] v=new Vector3[CapturedRig.Bones.Length];
            // The torso's heights: the hip line, the shoulder line, the neck and the head (voxel, model).
            private readonly float vHip,mHip,vShoulder,mShoulder,vNeck,mNeck,vHead,mHead;
            // The torso's widths: the hip block's and chest block's half widths on the voxel and the model.
            private readonly float vHipHalf,mHipHalf,vChestHalf,mChestHalf;
            // The ankles' height on the voxel and the model; the head map.
            private readonly float vAnkle,mAnkle;private Vector3 headOrigin;private float headSize=HeadScale;
            public Fit(CostumeJoint[] joints)
            {
                j=joints;for(int k=0;k<v.Length;k++)v[k]=VoxelJoint(k);
                vHip=(v[CapturedRig.LHip].Y+v[CapturedRig.RHip].Y)*.5f;mHip=(j[CapturedRig.LHip].At.Y+j[CapturedRig.RHip].At.Y)*.5f;
                vShoulder=(v[CapturedRig.LArm].Y+v[CapturedRig.RArm].Y)*.5f;mShoulder=(j[CapturedRig.LArm].At.Y+j[CapturedRig.RArm].At.Y)*.5f;
                vNeck=v[CapturedRig.Neck].Y;mNeck=Math.Max(mShoulder+.02f,j[CapturedRig.Neck].At.Y);
                vHead=v[CapturedRig.Head].Y;mHead=Math.Max(mNeck+.02f,j[CapturedRig.Head].At.Y);
                // The voxel's blocks reach .095 past its hip joints and .0... its chest block's edge sits .073 inside its shoulder joints.
                vHipHalf=.225f;mHipHalf=Math.Abs(j[CapturedRig.LHip].At.X-j[CapturedRig.RHip].At.X)*.5f+HipReach;
                vChestHalf=.272f;mChestHalf=Math.Abs(j[CapturedRig.LArm].At.X-j[CapturedRig.RArm].At.X)*.5f+ShoulderReach;
                vAnkle=Math.Max(.01f,(v[CapturedRig.LFoot].Y+v[CapturedRig.RFoot].Y)*.5f);mAnkle=Math.Max(.01f,(j[CapturedRig.LFoot].At.Y+j[CapturedRig.RFoot].At.Y)*.5f);
            }
            /// <summary>The head map for head parts spanning [min, max] (the head joint's frame): scaled HeadScale about their
            /// middle across and ahead, its top SkullTop + HeadClear over the model's head bone, its middle SkullMid ahead of it.</summary>
            public void Head(Vector3 min,Vector3 max,out Vector3 origin,out float size)
            {
                float cx=(min.X+max.X)*.5f,cz=(min.Z+max.Z)*.5f;size=headSize=HeadScale;var at=j[CapturedRig.Head].At;
                origin=headOrigin=new Vector3(at.X-cx*size,at.Y+SkullTop+HeadClear-max.Y*size,at.Z+SkullMid-cz*size);
            }
            /// <summary>The torso's height map: the voxel's rest height to the model's (piecewise through the hip line, shoulder
            /// line, neck and head; rigid below the hips and above the head).</summary>
            private float Height(float y)
            {
                if(y<=vHip)return y-vHip+mHip;
                if(y<=vShoulder)return mHip+(y-vHip)*(mShoulder-mHip)/(vShoulder-vHip);
                if(y<=vNeck)return mShoulder+(y-vShoulder)*(mNeck-mShoulder)/(vNeck-vShoulder);
                if(y<=vHead)return mNeck+(y-vNeck)*(mHead-mNeck)/(vHead-vNeck);
                return y-vHead+mHead;
            }
            /// <summary>The torso's width at a voxel rest height: the hip block's ratio at and below the hip line, the chest's at
            /// and above the shoulder line, between them blended.</summary>
            private float Width(float y)
            {
                float hip=mHipHalf/vHipHalf,chest=mChestHalf/vChestHalf;
                float k=Clamp01((y-vHip)/(vShoulder-vHip));return hip+(chest-hip)*k;
            }
            private static float Clamp01(float x)=>x<0?0:x>1?1:x;
            /// <summary>Coordinate `x` of a block spanning [a0, a1] mapped onto [b0, b1] with its ends' Keep kept at their size
            /// (linear through the middle; linear throughout when the block is too short to keep them); its scale there.</summary>
            private static float Span(float x,float a0,float a1,float b0,float b1,out float scale)
            {
                float e=Math.Min(Keep,(a1-a0)/3);scale=1;
                if(a1-a0<1e-6f){return b0+(x-a0);}
                if(b1-b0<2*e+1e-5f||e<1e-5f){scale=(b1-b0)/(a1-a0);return b0+(x-a0)*scale;}
                if(x<=a0+e)return b0+(x-a0);
                if(x>=a1-e)return b1-(a1-x);
                scale=(b1-b0-2*e)/(a1-a0-2*e);return b0+e+(x-a0-e)*scale;
            }
            /// <summary>A segment's along map: before the joint as it is, through the voxel segment stretched to the model's, past
            /// its end as it is.</summary>
            private static float Along(float t,float lv,float lm)=>t<=0?t:t>=lv?t-lv+lm:t*lm/lv;
            public void Place(Kind kind,int bone,Vector3 p,Vector3 n,Vector3 min,Vector3 max,out Vector3 at,out Vector3 normal)
            {
                switch(kind)
                {
                    case Kind.Torso:
                    case Kind.Neck:
                    {
                        // Heights through the torso's map, across narrowed by its width there, depth by TorsoDepth, round the joint's
                        // place on the model (its depth; the torso stays on the body's middle line).
                        var o=v[bone];float y0=o.Y+min.Y,y1=o.Y+max.Y,yc=(y0+y1)*.5f;
                        float w=kind==Kind.Neck?Math.Min(1,Width(vShoulder)*1.25f):Width(yc),d=kind==Kind.Neck?.85f:TorsoDepth;
                        float y=Span(o.Y+p.Y,y0,y1,Height(y0),Height(y1),out float sy);
                        float x=Span(p.X,min.X,max.X,min.X*w,max.X*w,out float sx);
                        float z=Span(p.Z,min.Z,max.Z,min.Z*d,max.Z*d,out float sz);
                        at=new Vector3(x,y,j[bone].At.Z+z);normal=SafeUnit(new Vector3(n.X/sx,n.Y/sy,n.Z/sz),n);return;
                    }
                    case Kind.Head:
                    {
                        // The head map: one block (every head part with the others), on the model's skull (Head).
                        at=headOrigin+p*headSize;normal=n;return;
                    }
                    case Kind.Cap:
                    {
                        // Round the model's shoulder joint, as round the voxel's, at the arm's girth.
                        int arm=bone+1;var off=v[arm]-v[bone];var q=(p-off)*ArmGirth;
                        at=j[arm].At+q;normal=n;return;
                    }
                    case Kind.Foot:
                    {
                        // Upright under the model's ankle or toe, its sole on the floor: up to the voxel's ankle stretched to the
                        // model's, its collar above that BootCollar as tall (so the shin's end, tucked ShinTuck past the ankle, stays
                        // inside it as the ankle flexes).
                        var c=new Vector3(0,0,(min.Z+max.Z)*.5f);var q=new Vector3(p.X*FootScale,p.Y,c.Z+(p.Z-c.Z)*FootScale);
                        float h=v[bone].Y+q.Y,k=h<=vAnkle?mAnkle/vAnkle:BootCollar,y=h<=vAnkle?h*k:mAnkle+(h-vAnkle)*k;
                        at=new Vector3(j[bone].At.X+q.X,Math.Max(0,y),j[bone].At.Z+q.Z);normal=SafeUnit(new Vector3(n.X/FootScale,n.Y/k,n.Z/FootScale),n);return;
                    }
                    default:
                    {
                        // A limb: its segment frame on the voxel (down the limb, its side, its front) to the model's, stretched
                        // along, slimmer across; a joint block and a hand rigid on their joint.
                        bool hand=kind==Kind.Hand;int seg=hand?bone-1:bone;int end=Segment[seg];
                        var va=SafeUnit(v[end]-v[seg],-Vector3.UnitY);var ma=SafeUnit(j[end].At-j[seg].At,-Vector3.UnitY);
                        Frame(va,out var vu,out var vw);Frame(ma,out var mu,out var mw);
                        float girth=bone>=CapturedRig.LHip?LegGirth:ArmGirth;if(hand)girth=HandScale;
                        float lv=(v[end]-v[seg]).Length(),lm=(j[end].At-j[seg].At).Length();
                        float s=Vector3.Dot(p,vu),t=-Vector3.Dot(p,-va),r=Vector3.Dot(p,vw);
                        float s0=Ext(min,max,vu,false),s1=Ext(min,max,vu,true),t0=Ext(min,max,va,false),t1=Ext(min,max,va,true),r0=Ext(min,max,vw,false),r1=Ext(min,max,vw,true);
                        float sa=1,ta=1,ra=1;float ss=Span(s,s0,s1,s0*girth,s1*girth,out sa),rr=Span(r,r0,r1,r0*girth,r1*girth,out ra),tt;
                        // (The shin's segment reaches ShinTuck past the model's ankle, into the boot.)
                        if(bone==CapturedRig.LKnee||bone==CapturedRig.RKnee)lm+=ShinTuck;
                        if(kind==Kind.Limb)tt=Span(t,t0,t1,Along(t0,lv,lm),Along(t1,lv,lm),out ta);
                        else if(hand){tt=t*HandScale;ta=HandScale;}
                        else tt=t;
                        var origin=hand?j[bone].At:j[bone].At;
                        // (A joint block's frame is its own joint's: the segment's start is the joint for the upper limb's parts.)
                        at=origin+mu*ss+ma*tt+mw*rr;
                        var nl=new Vector3(Vector3.Dot(n,vu)/sa,Vector3.Dot(n,va)/ta,Vector3.Dot(n,vw)/ra);
                        normal=SafeUnit(mu*nl.X+ma*nl.Y+mw*nl.Z,n);return;
                    }
                }
            }
            /// <summary>A limb's cross axes for its along axis `a`: its front (+z kept off the axis) and its side, the voxel
            /// arm's (side +x, along -y, front +z) handedness.</summary>
            private static void Frame(Vector3 a,out Vector3 side,out Vector3 front)
            {
                front=Vector3.UnitZ-a*Vector3.Dot(Vector3.UnitZ,a);
                if(front.LengthSquared()<1e-6f)front=-Vector3.UnitY-a*Vector3.Dot(-Vector3.UnitY,a);
                front=Vector3.Normalize(front);side=Vector3.Normalize(Vector3.Cross(front,a));
            }
            /// <summary>A box's extent along a unit axis (its far end with `max`, else its near end).</summary>
            private static float Ext(Vector3 min,Vector3 max,Vector3 axis,bool far)
            {
                float lo=0,hi=0;
                for(int c=0;c<8;c++){var q=new Vector3((c&1)!=0?max.X:min.X,(c&2)!=0?max.Y:min.Y,(c&4)!=0?max.Z:min.Z);float d=Vector3.Dot(q,axis);if(c==0){lo=hi=d;}else{lo=Math.Min(lo,d);hi=Math.Max(hi,d);}}
                return far?hi:lo;
            }
        }

        // ---- the body chips on the costume (2.3: kept, at the costume's anchors; the parts are made for the model's body) ----
        /// <summary>Where the model's parts reach (m, NativeBodyChipGear): the pauldron's side lame out from the shoulder joint, the
        /// sabaton's cuff round the ankle, the heel thruster's mount behind it, the pectoral plates' middle across.</summary>
        public const float ModelPauldronReach=.068f,ModelCuffRadius=.058f,ModelThrusterBack=.06f,ModelPlateAcross=.117f;
        /// <summary>A body chip part's place on the costume (its anchor frame, source axes): the chest's lights and plates brought
        /// forward onto the sternum plate's flat face (the model's chest curves back x²/2R: NativeBodyChipGear.ChestCurve), the
        /// pauldrons scaled out to the shoulder caps, the sabaton scaled round the boot and the heel thruster moved onto the
        /// boot's heel (review 2026-11-19: they were the model's, inside the blocks).</summary>
        public static void ChipPlace(CostumeBuild b,in NativeBodyChipGear.Part p,out Vector3 position,out Quaternion rotation,out float scale)
        {
            position=p.Position;rotation=p.Rotation;scale=1;
            switch(p.Anchor)
            {
                case NativeBodyChipGear.Anchor.Core:
                {
                    bool plate=p.Id==NativeBodyChipGear.PlateL||p.Id==NativeBodyChipGear.PlateR;float x=plate?ModelPlateAcross:p.Position.X;
                    position.Z+=x*x/(2*NativeBodyChipGear.ChestCurve);if(p.Id==NativeBodyChipGear.Pip||p.Id==NativeBodyChipGear.Node||p.Id==NativeBodyChipGear.Strip)rotation=Quaternion.Identity;return;
                }
                case NativeBodyChipGear.Anchor.LeftArm:case NativeBodyChipGear.Anchor.RightArm:scale=ChipScale(b,true);return;
                default:
                    scale=ChipScale(b,false);
                    if(p.Id==NativeBodyChipGear.Nozzle||p.Id==NativeBodyChipGear.Flame)position.Z-=Math.Max(0,(b.BootBack+.005f)/scale-ModelThrusterBack);
                    return;
            }
        }
        /// <summary>The scale an arm's (the shoulder caps') or a foot's (the boots') parts take on the costume.</summary>
        public static float ChipScale(CostumeBuild b,bool arm)=>arm?Math.Max(1,Math.Min(1.6f,(b.CapReach+.006f)/ModelPauldronReach)):Math.Max(1,Math.Min(1.8f,(b.BootHalf+.004f)/ModelCuffRadius));

        // ---- hats on the head block (the plan's costume hat fit, 2.3: the costume wears the voxel hat-* sculpts made for this
        // head, through the head map; review 2026-11-19: the model hats fitted onto the block sat inside it, unseen from behind) ----
        /// <summary>A voxel head-frame point (a voxel hat's, the Sheldon Head's) on the costume at rest (root space).</summary>
        public static Vector3 OnHead(CostumeBuild build,Vector3 p)=>build.HeadOrigin+p*build.HeadSize;
    }
}
