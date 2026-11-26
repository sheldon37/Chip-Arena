using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The female pilot (Core/NativeBodyKind; Astra's brief 2026-11-24 section 1; owner 2026-11-25: "I think the body needs to look
    // more feminine for the female body"): a level of the pack's female directions on the bake's one body, set when a body is dressed
    // (VisualFemale: the shapes by name on every worn mesh, so the clothes follow the body as the pack's own do, a weight past 0..100
    // baked into that mesh's own copy because the project clamps blend shape weights; the bones: the chest, the shoulders' span, the
    // upper arms' girth, the hands, the whole body, the chin), and what rides over the cloth fitted to it: the chest core
    // (VisualFemaleCore) and the robot armour's plates (SuitFemaleEnsure), each carried with the surface under it by that surface's
    // own shape deltas, so nothing worn over the cloth sinks into it. The hair, hats, helmet and face gear ride the head; the wrist
    // device rides the forearm, scaled with it. Every body made for the pilot wears it: the live, draft and first-person bodies,
    // corpses, revive bodies, death scenes and portraits. The lobby, Locker and portrait poses take the female stance
    // (VisualFemaleLobbyStance); the match idles only as the owner's option (FemaleMatchStance, off). Presentation only: the game
    // rig, hit volumes, reach, shot origins, movement and timing are the same for both bodies.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The level female bodies are made at (Core NativeBodyKind.Levels; DefaultLevel). A local preview: the capture tool sets
        /// it to film the levels side by side; a body made at another level is made again (VisualRetone).</summary>
        public static int FemaleLevel=NativeBodyKind.DefaultLevel;
        /// <summary>The owner's option, off: the light female stance on the match idles (VisualFemaleMatchStance).</summary>
        public static bool FemaleMatchStance;

        // ---- the level on a body (VisualDress) ----
        // Per mesh: its shapes the levels set (NativeBodyKind.Levels' Shapes order; -1 where it has none).
        private static readonly Dictionary<Mesh,int[]> femaleShapes=new Dictionary<Mesh,int[]>();
        // The bodies made female (instance ids of their roots) and the level each was made at (the suit's fit, SuitRigOf).
        private static readonly Dictionary<int,int> femaleRoots=new Dictionary<int,int>();
        private static int[] FemaleShapeIndex(Mesh mesh)
        {
            if(femaleShapes.TryGetValue(mesh,out var index))return index;
            var names=NativeBodyKind.Levels[0].Shapes;index=new int[names.Length];
            for(int k=0;k<index.Length;k++)
            {
                index[k]=-1;
                for(int i=0;i<mesh.blendShapeCount;i++)if(NativeZombieLooks.ShapeMatches(mesh.GetBlendShapeName(i),names[k])){index[k]=i;break;}
            }
            femaleShapes[mesh]=index;return index;
        }
        // A shape's displacement of every vertex at weight `w` as Unity draws it unclamped (between the frames either side, from 0 to
        // the first, and on past the first or the last frame along its slope), added into `sum` (normals and tangents too when given).
        private static void ShapeAdd(Mesh mesh,int s,float w,Vector3[] sum,Vector3[] sumN=null,Vector3[] sumT=null)
        {
            int frames=mesh.GetBlendShapeFrameCount(s),n=mesh.vertexCount;if(frames<1||w==0)return;
            int f=0;while(f<frames-1&&mesh.GetBlendShapeFrameWeight(s,f)<w)f++;
            float top=mesh.GetBlendShapeFrameWeight(s,f),bottom=f>0?mesh.GetBlendShapeFrameWeight(s,f-1):0;
            var hi=new Vector3[n];var hiN=new Vector3[n];var hiT=new Vector3[n];mesh.GetBlendShapeFrameVertices(s,f,hi,hiN,hiT);
            Vector3[] lo=null,loN=null,loT=null;if(f>0){lo=new Vector3[n];loN=new Vector3[n];loT=new Vector3[n];mesh.GetBlendShapeFrameVertices(s,f-1,lo,loN,loT);}
            float t=top>bottom?(w-bottom)/(top-bottom):1;
            for(int i=0;i<n;i++)
            {
                sum[i]+=lo!=null?Vector3.LerpUnclamped(lo[i],hi[i],t):hi[i]*t;
                if(sumN!=null)sumN[i]+=lo!=null?Vector3.LerpUnclamped(loN[i],hiN[i],t):hiN[i]*t;
                if(sumT!=null)sumT[i]+=lo!=null?Vector3.LerpUnclamped(loT[i],hiT[i],t):hiT[i]*t;
            }
        }
        // A mesh's female copy at `level`: the part of each of the level's weights outside 0..100 (which the clamp drops) baked into
        // its vertices, normals and tangents; null when every weight falls inside (the mesh is worn as it is) or it is not readable.
        // Made once per mesh and level; the copy keeps the mesh's shapes (the face rig's expressions, the hats' hair shapes).
        private static readonly Dictionary<(Mesh,int),Mesh> femaleMeshes=new Dictionary<(Mesh,int),Mesh>();
        private static readonly Dictionary<Mesh,Mesh> femaleSource=new Dictionary<Mesh,Mesh>();
        private static Mesh FemaleMesh(Mesh src,int level)
        {
            if(femaleMeshes.TryGetValue((src,level),out var made))return made;
            var L=NativeBodyKind.LevelOf(level);var index=FemaleShapeIndex(src);bool any=false;
            for(int k=0;k<index.Length;k++)if(index[k]>=0&&(L.Weights[k]<0||L.Weights[k]>100))any=true;
            if(!any||!src.isReadable){femaleMeshes[(src,level)]=null;return null;}
            int n=src.vertexCount;var v=src.vertices;var nm=src.normals;var tg=src.tangents;var dv=new Vector3[n];var dn=new Vector3[n];var dt=new Vector3[n];
            for(int k=0;k<index.Length;k++)
            {
                if(index[k]<0)continue;float w=L.Weights[k],part=w<0?w:w>100?w-100:0;if(part==0)continue;
                // (Past 100 the slope above the last frame: its whole weight less the 100 the renderer draws.)
                if(part<0)ShapeAdd(src,index[k],part,dv,dn,dt);else{var whole=new Vector3[n];var wholeN=new Vector3[n];var wholeT=new Vector3[n];var cap=new Vector3[n];var capN=new Vector3[n];var capT=new Vector3[n];
                    ShapeAdd(src,index[k],w,whole,wholeN,wholeT);ShapeAdd(src,index[k],100,cap,capN,capT);for(int i=0;i<n;i++){dv[i]+=whole[i]-cap[i];dn[i]+=wholeN[i]-capN[i];dt[i]+=wholeT[i]-capT[i];}}
            }
            var m=Instantiate(src);m.name=src.name+" (female "+L.Name+")";
            for(int i=0;i<n;i++)v[i]+=dv[i];m.vertices=v;
            if(nm.Length==n){for(int i=0;i<n;i++)nm[i]=(nm[i]+dn[i]).normalized;m.normals=nm;}
            if(tg.Length==n){for(int i=0;i<n;i++){var t3=new Vector3(tg[i].x,tg[i].y,tg[i].z)+dt[i];t3.Normalize();tg[i]=new Vector4(t3.x,t3.y,t3.z,tg[i].w);}m.tangents=tg;}
            m.RecalculateBounds();femaleMeshes[(src,level)]=m;femaleSource[m]=src;return m;
        }
        // The level on a body being made (before its binding reads the rest pose): its face pieces, each mesh's female copy and the
        // shapes by name (the in-range part of each weight), then the bones. Runs when a body is made, never per frame.
        private static void VisualFemale(GameObject root,int level)
        {
            var L=NativeBodyKind.LevelOf(level);
            // Its face pieces (the pack's female lashes and brows, baked off as the look data's "face" pieces): each worn in place of
            // the piece of its kind the head wears (the renderer with the same slot prefix). None in an older bake.
            var d=VisualLookData();
            if(d?.pieces!=null)
            {
                Dictionary<string,Renderer> named=null;
                foreach(var p in d.pieces)
                {
                    if(p?.renderers==null||p.slot!=NativeBodyKind.FaceSlot||Array.IndexOf(NativeBodyKind.FacePieces,p.id)<0)continue;
                    if(named==null){named=new Dictionary<string,Renderer>(StringComparer.Ordinal);foreach(var r in root.GetComponentsInChildren<Renderer>(true))named[r.name]=r;}
                    foreach(var n in p.renderers)
                    {
                        if(!named.TryGetValue(n,out var mine))continue;int dot=n.IndexOf('.');if(dot<=0)continue;
                        foreach(var r in named.Values)if(r!=mine&&r.gameObject.activeSelf&&string.CompareOrdinal(r.name,0,n,0,dot+1)==0)r.gameObject.SetActive(false);
                        mine.gameObject.SetActive(true);
                    }
                }
            }
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=skin.sharedMesh;if(mesh==null||mesh.blendShapeCount==0)continue;
                var copy=FemaleMesh(mesh,level);if(copy!=null){VisualHairMesh(skin,copy);mesh=copy;}
                var index=FemaleShapeIndex(mesh);
                for(int k=0;k<index.Length;k++)if(index[k]>=0)skin.SetBlendShapeWeight(index[k],Mathf.Clamp(L.Weights[k],0,100));
            }
            femaleRoots[root.GetInstanceID()]=level;
            var skeleton=root.transform.Find("VisualSkeleton");if(skeleton==null)return;
            Transform chest=null,head=null,rootBone=null,chin=null;
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))
            {
                string n=t.name;
                if(chest==null&&n==NativeBodyKind.ChestBone)chest=t;else if(head==null&&n==NativeBodyKind.HeadBone)head=t;else if(rootBone==null&&n=="root")rootBone=t;else if(chin==null&&n=="chin")chin=t;
                // The shoulders' span (the upper arm in along its collarbone), the upper arms' girth (their shoulder twist leaves'
                // cross-section), the hands.
                else if(n=="upperarm_l"||n=="upperarm_r")t.localPosition*=L.Span;
                else if(n=="shoulder_twist_l"||n=="shoulder_twist_r"){var k=t.localScale;t.localScale=new Vector3(k.x,k.y*L.ArmY,k.z*L.ArmZ);}
                else if(n=="hand_l"||n=="hand_r")t.localScale*=L.Hand;
            }
            if(rootBone!=null)rootBone.localScale*=L.Root;
            if(chin!=null){var k=chin.localScale;chin.localScale=new Vector3(k.x*L.Chin,k.y,k.z*L.Chin);}
            if(chest==null)return;chest.localScale*=L.Chest;
            if(head!=null&&head.IsChildOf(chest))head.localScale/=L.Chest;
        }

        // ---- the female surface's deltas ----
        // A mesh's vertices and the level's displacement of each from the bake's own mesh (mesh space: its body shapes at their whole
        // weights, in or out of range), cached per mesh and level; a female copy reads its source's.
        private static readonly Dictionary<(Mesh,int),(Vector3[] At,Vector3[] Delta)> femaleDeltas=new Dictionary<(Mesh,int),(Vector3[],Vector3[])>();
        private static bool FemaleDeltas(Mesh mesh,int level,out Vector3[] at,out Vector3[] delta)
        {
            if(femaleSource.TryGetValue(mesh,out var source))mesh=source;
            if(femaleDeltas.TryGetValue((mesh,level),out var known)){at=known.At;delta=known.Delta;return at!=null;}
            at=null;delta=null;int n=mesh.vertexCount;
            if(mesh.isReadable&&n>0)
            {
                var index=FemaleShapeIndex(mesh);var L=NativeBodyKind.LevelOf(level);
                for(int k=0;k<index.Length;k++){if(index[k]<0)continue;delta??=new Vector3[n];ShapeAdd(mesh,index[k],L.Weights[k],delta);}
                if(delta!=null)at=mesh.vertices;
            }
            femaleDeltas[(mesh,level)]=(at,at!=null?delta:null);if(at==null)delta=null;return at!=null;
        }
        // The weighted mean of a mesh's deltas round `p` (mesh space) within `reach`: how far the surface there moved.
        private static bool FemaleDeltaAt(Vector3[] at,Vector3[] delta,Vector3 p,float reach,ref Vector3 sum,ref float total)
        {
            bool any=false;float r2=reach*reach;
            for(int i=0;i<at.Length;i++){var d=at[i]-p;float q=d.sqrMagnitude;if(q>=r2)continue;float w=1-Mathf.Sqrt(q)/reach;w*=w;sum+=w*delta[i];total+=w;any=true;}
            return any;
        }

        // ---- the chest core ----
        // The core sits where the bake measured it on today's body (its top's surface over the sternum, beside the heart); on the
        // female body it rides out with that surface, by the worn meshes' deltas within CoreReach of it. Per top, hands and level, once.
        private const float CoreReach=.035f;
        private static readonly Dictionary<string,Vector3> femaleCores=new Dictionary<string,Vector3>(StringComparer.Ordinal);
        private static void VisualFemaleCore(VisualBody body)
        {
            if(body.GearCore==null||!NativeBodyKind.IsFemale(body.Look)||NativeHotDog.Worn(body.Look)||NativeVoxelCostume.Worn(body.Look))return;
            var bone=body.GearCore.parent;if(bone==null)return;int level=body.Root!=null&&femaleRoots.TryGetValue(body.Root.GetInstanceID(),out int made)?made:FemaleLevel;
            string key=NativeLook.Of(body.Look,NativeLook.Upper)+"|"+NativeLook.Of(body.Look,NativeLook.Hands)+"|"+level;
            if(!femaleCores.TryGetValue(key,out var offset))
            {
                // (A corpse's anchors before any live body made them: as measured.)
                if(body.All==null)return;
                Vector3 sum=Vector3.zero;float total=0;var local=body.GearCore.localPosition;
                foreach(var r in body.All)
                {
                    if(!(r is SkinnedMeshRenderer skin)||skin.sharedMesh==null||!FemaleDeltas(skin.sharedMesh,level,out var at,out var delta))continue;
                    int b=Array.IndexOf(skin.bones,bone);var binds=skin.sharedMesh.bindposes;if(b<0||b>=binds.Length)continue;
                    Vector3 s=Vector3.zero;float t=0;
                    if(FemaleDeltaAt(at,delta,binds[b].inverse.MultiplyPoint3x4(local),CoreReach,ref s,ref t)){sum+=binds[b].MultiplyVector(s);total+=t;}
                }
                offset=total>0?sum/total:Vector3.zero;femaleCores[key]=offset;
            }
            body.GearCore.localPosition+=offset;
        }

        // ---- the female stance ----
        // The pack's female idle against its male one (BMAC_F_Idle, BMAC_M_Idle): the thighs in toward each other (the feet set back
        // level on the floor), the knees turned in, the hips rolled and the chest against them, the chest up and back. Laid on a posed
        // body's bones after its pose and before its hands are fitted to what they hold (the arms are left to the pose): `amount` 0..1.
        private static void VisualFemaleStance(VisualBody body,float legsIn,float kneesIn,float chestUp,float hipRoll,float amount)
        {
            var s=body.Stance;if(s==null||amount<=0||body.Root==null)return;
            var frame=body.Root.transform;Vector3 forward=frame.forward,right=frame.right;
            Transform pelvis=s[0],thighL=s[1],thighR=s[2],chest=s[3],footL=s[4],footR=s[5],calfL=s[6],calfR=s[7];
            if(pelvis!=null&&hipRoll!=0)pelvis.rotation=Quaternion.AngleAxis(hipRoll*amount,forward)*pelvis.rotation;
            for(int side=0;side<2;side++)
            {
                var thigh=side==0?thighL:thighR;var calf=side==0?calfL:calfR;var foot=side==0?footL:footR;if(thigh==null)continue;
                float sign=side==0?1:-1;var feet=foot!=null?foot.rotation:Quaternion.identity;
                // (The left thigh at -x: its foot in toward +x about the facing; the knee turned in about the thigh's own line.)
                thigh.rotation=Quaternion.AngleAxis(sign*legsIn*amount,forward)*thigh.rotation;
                if(calf!=null){var along=(calf.position-thigh.position).normalized;thigh.rotation=Quaternion.AngleAxis(-sign*kneesIn*amount,along)*thigh.rotation;}
                // (The sole kept as the pose set it, turned in with the knee: only the roll the thigh's lean gave it undone.)
                if(foot!=null)foot.rotation=Quaternion.AngleAxis(-sign*legsIn*amount,forward)*foot.rotation;
            }
            if(chest!=null)chest.rotation=Quaternion.AngleAxis(-hipRoll*amount,forward)*Quaternion.AngleAxis(-chestUp*amount,right)*chest.rotation;
        }
        // The stance's bones on a female body, found once.
        private static void VisualFemaleStanceBones(VisualBody body)
        {
            if(body.Stance!=null||body.Binding?.Bones==null)return;string[] names={"pelvis","thigh_l","thigh_r",NativeBodyKind.ChestBone,"foot_l","foot_r","calf_l","calf_r"};
            var found=new Transform[names.Length];foreach(var t in body.Binding.Bones){if(t==null)continue;int k=Array.IndexOf(names,t.name);if(k>=0&&found[k]==null)found[k]=t;}
            body.Stance=found;
        }
        // The lobby, Locker and portrait poses that stand (not seated or laid down): the female stance whole.
        private void VisualFemaleLobbyStance(VisualBody body)
        {
            if(body?.FemaleMade!=true)return;VisualFemaleStanceBones(body);
            VisualFemaleStance(body,NativeBodyKind.StanceLegsIn,NativeBodyKind.StanceKneesIn,NativeBodyKind.StanceChestUp,NativeBodyKind.StanceHipRoll,1);
        }
        // The owner's option on the match idles (FemaleMatchStance, off): the lighter stance while the pilot stands on the ground,
        // eased in over a third of a second and out as it moves.
        private void VisualFemaleMatchStance(VisualBody body,Actor a)
        {
            if(body?.FemaleMade!=true||!FemaleMatchStance){if(body!=null)body.StanceAmount=0;return;}
            VisualFemaleStanceBones(body);var at=body.At;float dt=Mathf.Max(Time.deltaTime,1e-3f);var move=at-body.StanceAt;move.y=0;body.StanceAt=at;
            float want=a.Grounded&&move.magnitude/dt<.6f?1:0;body.StanceAmount=Mathf.MoveTowards(body.StanceAmount,want,dt*3);
            VisualFemaleStance(body,NativeBodyKind.MatchStanceLegsIn,NativeBodyKind.MatchStanceKneesIn,NativeBodyKind.MatchStanceChestUp,0,body.StanceAmount);
        }

        // ---- the robot armour's plates ----
        // The plates (Core NativeRobotSuit) are authored on today's body over the garments each robot piece is worn over (its
        // Under: the sweater, the skinny jeans, the gloves, the work boots). For the female body each plate vertex is carried with
        // its piece's garment there: the mean of that garment's deltas within SuitReach (found on a grid), moved from the garment's
        // bind frame into the plate's. Made once, when a female body first wears a robot piece; shared by every female body.
        private const float SuitReach=.07f;
        private readonly HashSet<int> suitFemaleTried=new HashSet<int>();
        private sealed class FemaleGrid
        {
            public Vector3[] At,Delta;public readonly Dictionary<long,List<int>> Cells=new Dictionary<long,List<int>>();public Matrix4x4[] Binds;public Transform[] Bones;
            public static long Key(int x,int y,int z)=>((long)(x&0x1fffff)<<42)|((long)(y&0x1fffff)<<21)|(long)(z&0x1fffff);
            public static int Cell(float v)=>Mathf.FloorToInt(v/SuitReach);
            public FemaleGrid(Vector3[] at,Vector3[] delta,Matrix4x4[] binds,Transform[] bones)
            {
                At=at;Delta=delta;Binds=binds;Bones=bones;
                for(int i=0;i<at.Length;i++){long k=Key(Cell(at[i].x),Cell(at[i].y),Cell(at[i].z));if(!Cells.TryGetValue(k,out var list))Cells[k]=list=new List<int>();list.Add(i);}
            }
            public Vector3 At3(Vector3 p)
            {
                Vector3 sum=Vector3.zero;float total=0,r2=SuitReach*SuitReach;int cx=Cell(p.x),cy=Cell(p.y),cz=Cell(p.z);
                for(int x=cx-1;x<=cx+1;x++)for(int y=cy-1;y<=cy+1;y++)for(int z=cz-1;z<=cz+1;z++)
                {
                    if(!Cells.TryGetValue(Key(x,y,z),out var list))continue;
                    foreach(int i in list){var d=At[i]-p;float q=d.sqrMagnitude;if(q>=r2)continue;float w=1-Mathf.Sqrt(q)/SuitReach;w*=w;sum+=w*Delta[i];total+=w;}
                }
                return total>0?sum/total:Vector3.zero;
            }
        }
        private void SuitFemaleEnsure(GameObject root,SuitRig suit,int level)
        {
            if(sourceModels==null||suit==null||!suitFemaleTried.Add(level))return;
            var d=VisualLookData();if(d?.pieces==null)return;
            var skins=new Dictionary<string,SkinnedMeshRenderer>(StringComparer.Ordinal);foreach(var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))skins[s.name]=s;
            var grids=new FemaleGrid[NativeLook.Slots.Length];
            for(int slot=1;slot<NativeLook.Slots.Length;slot++)
                foreach(var p in d.pieces)
                {
                    if(p?.renderers==null||p.slot!=NativeLook.Slots[slot]||p.id!=NativeRobotSuit.Under[slot])continue;
                    foreach(var n in p.renderers)if(grids[slot]==null&&skins.TryGetValue(n,out var skin)&&skin.sharedMesh!=null&&FemaleDeltas(skin.sharedMesh,level,out var at,out var delta))
                        grids[slot]=new FemaleGrid(at,delta,skin.sharedMesh.bindposes,skin.bones);
                }
            try
            {
                sourceModels.SuitFemaleModels(NativeSourceModels.SuitFemaleOf(level),(bone,v)=>
                {
                    var g=bone>=0&&bone<NativeRobotSuit.BoneSlot.Length?grids[NativeRobotSuit.BoneSlot[bone]]:null;if(g==null)return v;
                    // The plate's frame (its rig's bind) to the garment's (its own bind for that bone; the same space when it has none).
                    int b=Array.IndexOf(g.Bones,suit.Bones[bone]);var toGarment=b>=0&&b<g.Binds.Length?g.Binds[b].inverse*suit.Bind[bone]:Matrix4x4.identity;
                    return v+toGarment.inverse.MultiplyVector(g.At3(toGarment.MultiplyPoint3x4(v)));
                });
            }
            catch(Exception e){Debug.LogWarning("Visual body: the robot suit's female fit failed: "+e.Message);}
        }
        // The suit model a body's plates draw from: the female fit on a female body (once it is made), else today's.
        private string SuitModelOf(GameObject root,SuitRig suit)
        {
            if(!femaleRoots.TryGetValue(root.GetInstanceID(),out int level))return NativeSourceModels.Suit;string id=NativeSourceModels.SuitFemaleOf(level);
            if(!sourceModels.Has(id))SuitFemaleEnsure(root,suit,level);
            return sourceModels.Has(id)?id:NativeSourceModels.Suit;
        }
    }
}
