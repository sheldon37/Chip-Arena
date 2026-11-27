using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The hair bank's styles on the model (Core/NativeHairBank.cs; owner 2026-11-26: "Lets add a lot more hairstyles, some anime
    // ones as well ... Add magica cloth into these to simulate hair movement realistically"; docs/PILOT-BODY-HAIR.md section 3).
    // The bank's styles join the bake's look data as hair pieces (VisualLookAddPieces: so the Locker lists them, VisualDress turns
    // them on and off by name, and their tag shapes tuck under headgear as the bake's own hair does). When a body is dressed in
    // one (VisualHairBankDress, before VisualDress reads the instance), each of its pieces is built on the body: a skinned
    // renderer named as the bank names it ("HairBack.Bank_..."), on the head and on its chains, new bones under the head at
    // their rest offsets (each chain's root fixed: the local cloth bridge simulates the rest, NativeVisualCloth.Attach, which
    // runs after dressing and finds them as it finds the bake's hair chains). Meshes, textures and materials are made once and
    // shared by every body; the material is the bake's hair material (its palette, so the pilot's hair colour) on the piece's
    // own colour-weight texture. Presentation only: no rule, hit volume, timing or network message reads it.
    public sealed partial class ArenaPresentation
    {
        private static NativeHairBank.Bank hairBank;private static int hairBankState; // 0 unread, 1 read, -1 none
        private static Mesh[] hairBankMeshes;private static Texture2D[] hairBankTextures;
        private static readonly Dictionary<(Material,int),Material> hairBankMaterials=new Dictionary<(Material,int),Material>();
        /// <summary>The local hair bank, or null without one (the game then lists and draws the bake's hair only).</summary>
        internal static NativeHairBank.Bank HairBank()
        {
            if(hairBankState==0)
            {
                var text=Resources.Load<TextAsset>(NativeHairBank.Resource);hairBank=text!=null?NativeHairBank.Read(text.bytes):null;
                if(text!=null&&hairBank==null)Debug.LogWarning("Hair bank: "+NativeHairBank.Resource+" is not a version "+NativeHairBank.Version+" bank; only the bake's hairstyles are offered.");
                hairBankState=hairBank!=null?1:-1;
                if(hairBank!=null){hairBankMeshes=new Mesh[hairBank.Pieces.Length];hairBankTextures=new Texture2D[hairBank.Textures.Length];}
                // (The sleek pilot suit when the bank carries its garments and plates: Core/NativeRobotSuit.cs.)
                NativeRobotSuit.SleekSuit=hairBank!=null&&hairBank.Has(NativeLook.Upper,NativeRobotSuit.SleekUnder[1])&&hairBank.Has(NativeLook.Lower,NativeRobotSuit.SleekUnder[2])&&hairBank.Plates.Length>0;
                // (The sleek face gear when it carries every gear's plates: Core/NativeSleekFace.cs.)
                NativeSleekFace.On=hairBank!=null&&NativeSleekFace.HasAll(hairBank.Plates);
            }
            return hairBankState>0?hairBank:null;
        }
        // The look data (`d`, just read) with the pilot's pieces the bake draws under another id (Core/NativeWardrobeAliases.cs:
        // the zombies' clothes, each its piece's renderers, covers, tags and shoes' lift, and its gear anchors with every hands),
        // then the bank's styles as hair pieces after the bake's own (a style the bake already has keeps the bake's), and each
        // bank piece's tag shapes.
        private static void VisualLookAddPieces(LookData d)
        {
            if(d?.pieces==null)return;
            var pieces=new List<LookPiece>(d.pieces);
            foreach(var pair in NativeWardrobeAliases.Pairs)
            {
                LookPiece from=null;bool listed=false;
                foreach(var p in d.pieces)if(p!=null&&p.slot==pair[0]){if(p.id==pair[2])from=p;if(p.id==pair[1])listed=true;}
                if(from==null||listed)continue;
                pieces.Add(new LookPiece{slot=pair[0],id=pair[1],renderers=from.renderers,covers=from.covers,tags=from.tags,lift=from.lift});
                if(pair[0]==NativeLook.Upper&&d.anchors!=null)
                {
                    var anchors=new List<LookAnchor>(d.anchors);
                    foreach(var a in d.anchors)if(a!=null&&a.upper==pair[2])anchors.Add(new LookAnchor{upper=pair[1],hands=a.hands,core=a.core,device=a.device,emblem=a.emblem,radius=a.radius});
                    d.anchors=anchors.ToArray();
                }
            }
            d.pieces=pieces.ToArray();
            var bank=HairBank();if(bank==null)return;
            var shapes=new List<LookShape>(d.shapes??Array.Empty<LookShape>());
            var known=new HashSet<string>(StringComparer.Ordinal);foreach(var p in d.pieces)if(p!=null&&p.slot==NativeLook.Hair)known.Add(p.id);
            foreach(var id in bank.StyleOrder)
            {
                string slot=bank.Slot[id];if(slot==NativeLook.Hair&&known.Contains(id))continue;
                bool listed=false;foreach(var p in d.pieces)if(p!=null&&p.slot==slot&&p.id==id)listed=true;if(listed)continue;
                var idx=bank.Styles[id];var names=new string[idx.Length];
                for(int k=0;k<idx.Length;k++)names[k]=bank.Pieces[idx[k]].Renderer;
                pieces.Add(new LookPiece{slot=slot,id=id,renderers=names,covers=bank.Covers[id],tags=Array.Empty<string>()});
                // (a top takes another's gear anchors with every hands: the chest core and the wrist device sit as on it)
                string from=bank.AnchorsFrom[id];
                if(slot==NativeLook.Upper&&from.Length>0&&d.anchors!=null)
                {
                    var anchors=new List<LookAnchor>(d.anchors);
                    foreach(var a in d.anchors)if(a!=null&&a.upper==from)anchors.Add(new LookAnchor{upper=id,hands=a.hands,core=a.core,device=a.device,emblem=a.emblem,radius=a.radius});
                    d.anchors=anchors.ToArray();
                }
            }
            foreach(var p in bank.Pieces)for(int s=0;s<p.Shapes.Length;s++)
            {
                var n=p.Shapes[s].Name;int k=n.LastIndexOf("Tag_",StringComparison.OrdinalIgnoreCase);
                if(k>=0)shapes.Add(new LookShape{renderer=p.Renderer,shape=s,tag=n.Substring(k+4).ToLowerInvariant()});
            }
            d.pieces=pieces.ToArray();d.shapes=shapes.ToArray();
        }
        // The worn style's bank pieces built on a body instance (`root`, as made from the bake: its skeleton at rest), when the
        // look's hair (or the local preview's) is a bank style and the instance lacks them.
        private static void VisualHairBankDress(GameObject root,string look)
        {
            var bank=HairBank();if(bank==null||root==null)return;
            // the worn pieces of the body's other slots first (the sleek suit's bodysuit: on the body's own bones)
            for(int s=1;s<NativeLook.Slots.Length;s++)
            {
                string id=NativeLook.Of(look,NativeLook.Slots[s]);if(!bank.Has(NativeLook.Slots[s],id))continue;
                foreach(int i in bank.Styles[id])
                {
                    var p=bank.Pieces[i];bool made=false;foreach(Transform c in root.transform)if(c.name==p.Renderer){made=true;break;}
                    if(!made&&p.OnSkeleton)HairBankWorn(root,i,p);
                }
            }
            string hair=HairPreview??NativeLook.Of(look,NativeLook.Hair);if(hair==null||!bank.Has(NativeLook.Hair,hair))return;var idx=bank.Styles[hair];
            Transform head=null;Material template=null;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="head"&&t.parent!=null&&t.parent.name=="neck_02"){head=t;break;}
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(r.sharedMaterial==null||!r.name.StartsWith("HairBack.",StringComparison.Ordinal)||r.name.StartsWith("HairBack.Bank_",StringComparison.Ordinal))continue;
                template=r.sharedMaterial;if(r.name=="HairBack.HairBack_PineappleCut")break; // (the default hair's, else any of the bake's)
            }
            if(head==null||template==null)return;
            foreach(int i in idx)
            {
                var p=bank.Pieces[i];bool made=false;
                foreach(Transform c in root.transform)if(c.name==p.Renderer){made=true;break;}
                if(!made)HairBankPiece(root.transform,head,template,i,p);
            }
        }
        // A bank piece that rides the body's own bones (by name, with its bind poses), on the material of the bake's renderer it
        // names (its palette), its body shapes beside it (the female body sets them by name: NativeBodyKindPresentation.cs).
        private static readonly Dictionary<string,Transform> hairBankBones=new Dictionary<string,Transform>(StringComparer.Ordinal);
        private static void HairBankWorn(GameObject root,int index,NativeHairBank.Piece p)
        {
            hairBankBones.Clear();Material template=null;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(!hairBankBones.ContainsKey(t.name))hairBankBones[t.name]=t;
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name==p.MaterialFrom&&r.sharedMaterial!=null){template=r.sharedMaterial;break;}
            if(template==null)foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.sharedMaterial!=null&&(r.name.StartsWith("Top.",StringComparison.Ordinal)||r.name.StartsWith("Bottom.",StringComparison.Ordinal))){template=r.sharedMaterial;break;}
            var bones=new Transform[p.Skeleton.Length];
            for(int b=0;b<bones.Length;b++)if(!hairBankBones.TryGetValue(p.Skeleton[b],out bones[b])){hairBankBones.Clear();return;} // (a body without one of its bones: left off)
            hairBankBones.Clear();var mesh=HairBankMesh(index,p);if(mesh==null||template==null)return;
            var go=new GameObject(p.Renderer);go.transform.SetParent(root.transform,false);go.layer=root.layer;
            var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;
            int rootBone=Math.Max(0,Array.IndexOf(p.Skeleton,"pelvis"));skin.rootBone=bones[rootBone];
            // (its bounds in the root bone's frame, as the bind pose puts the mesh there, with room for the limbs' swing)
            var bind=mesh.bindposes[rootBone];var b0=mesh.bounds;var lo=Vector3.positiveInfinity;var hi=Vector3.negativeInfinity;
            for(int c=0;c<8;c++){var q=bind.MultiplyPoint3x4(new Vector3((c&1)==0?b0.min.x:b0.max.x,(c&2)==0?b0.min.y:b0.max.y,(c&4)==0?b0.min.z:b0.max.z));lo=Vector3.Min(lo,q);hi=Vector3.Max(hi,q);}
            var bounds=new Bounds((lo+hi)*.5f,hi-lo);bounds.Expand(.6f);skin.localBounds=bounds;
            skin.sharedMaterial=HairBankMaterial(template,p);
            for(int s=0;s<mesh.blendShapeCount;s++)skin.SetBlendShapeWeight(s,0);
        }
        private static void HairBankPiece(Transform root,Transform head,Material template,int index,NativeHairBank.Piece p)
        {
            var mesh=HairBankMesh(index,p);if(mesh==null)return;
            var go=new GameObject(p.Renderer);go.transform.SetParent(root,false);go.layer=head.gameObject.layer;
            var bones=new Transform[p.Bones.Length+1];bones[0]=head;
            for(int b=0;b<p.Bones.Length;b++)
            {
                var bone=p.Bones[b];var t=new GameObject(bone.Name).transform;t.SetParent(bone.Parent<0?head:bones[bone.Parent+1],false);
                t.localPosition=new Vector3(bone.X,bone.Y,bone.Z);t.localRotation=Quaternion.identity;t.localScale=Vector3.one;bones[b+1]=t;
            }
            var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=head;
            skin.localBounds=mesh.bounds;skin.sharedMaterial=HairBankMaterial(template,p);
            for(int s=0;s<mesh.blendShapeCount;s++)skin.SetBlendShapeWeight(s,0);
        }
        // The piece's mesh in the head's frame (bind pose: the head's bindpose the identity, each bank bone's its offset back).
        private static Mesh HairBankMesh(int index,NativeHairBank.Piece p)
        {
            if(hairBankMeshes[index]!=null)return hairBankMeshes[index];
            int n=p.VertexCount;var pos=new Vector3[n];var nor=new Vector3[n];var uv=new Vector2[n];var col=new Color32[n];var bw=new BoneWeight[n];
            for(int v=0;v<n;v++)
            {
                pos[v]=new Vector3(p.Positions[v*3],p.Positions[v*3+1],p.Positions[v*3+2]);nor[v]=new Vector3(p.Normals[v*3],p.Normals[v*3+1],p.Normals[v*3+2]);
                uv[v]=new Vector2(p.Uvs[v*2],p.Uvs[v*2+1]);col[v]=new Color32(p.Colors[v*4],p.Colors[v*4+1],p.Colors[v*4+2],p.Colors[v*4+3]);
                bw[v]=new BoneWeight{boneIndex0=p.BoneIndex[v*4],weight0=p.BoneWeight[v*4],boneIndex1=p.BoneIndex[v*4+1],weight1=p.BoneWeight[v*4+1],
                    boneIndex2=p.BoneIndex[v*4+2],weight2=p.BoneWeight[v*4+2],boneIndex3=p.BoneIndex[v*4+3],weight3=p.BoneWeight[v*4+3]};
            }
            Matrix4x4[] bind;
            if(p.OnSkeleton)
            {
                // (the body's bones' bind poses, row by row as the dump wrote them)
                bind=new Matrix4x4[p.Skeleton.Length];
                for(int b=0;b<bind.Length;b++){var m=new Matrix4x4();for(int k=0;k<16;k++)m[k/4,k%4]=p.SkeletonBind[b*16+k];bind[b]=m;}
            }
            else
            {
                bind=new Matrix4x4[p.Bones.Length+1];bind[0]=Matrix4x4.identity;
                for(int b=0;b<p.Bones.Length;b++){var at=p.HeadPoint(b);bind[b+1]=Matrix4x4.Translate(new Vector3(-at[0],-at[1],-at[2]));}
            }
            var mesh=new Mesh{name="Hair bank "+p.Renderer,hideFlags=HideFlags.DontSave};
            mesh.vertices=pos;mesh.normals=nor;mesh.uv=uv;mesh.colors32=col;mesh.triangles=p.Triangles;mesh.boneWeights=bw;mesh.bindposes=bind;
            foreach(var shape in p.Shapes)
            {
                var delta=new Vector3[n];for(int k=0;k<shape.Vertices.Length;k++)delta[shape.Vertices[k]]=new Vector3(shape.Deltas[k*3],shape.Deltas[k*3+1],shape.Deltas[k*3+2]);
                mesh.AddBlendShapeFrame(shape.Name,100,delta,null,null);
            }
            mesh.RecalculateBounds();mesh.RecalculateTangents();
            return hairBankMeshes[index]=mesh;
        }
        private static Material HairBankMaterial(Material template,NativeHairBank.Piece p)
        {
            int tex=p.Texture;if(hairBankMaterials.TryGetValue((template,tex),out var made)&&made!=null)return made;
            // (A worn piece's material keeps its template's culling and cut-off: the hair's are its own.)
            made=new Material(template){name=template.name+" (hair bank "+(tex<0?"plain":hairBank.Textures[tex].Name)+")",hideFlags=HideFlags.DontSave};
            var texture=tex>=0?HairBankTexture(tex):null;if(texture!=null&&made.HasProperty("_Texture2D"))made.SetTexture("_Texture2D",texture);
            if(made.HasProperty("_Cutoff"))made.SetFloat("_Cutoff",p.Cutoff);if(made.HasProperty("_Cull"))made.SetFloat("_Cull",p.Cull);
            return hairBankMaterials[(template,tex)]=made;
        }
        private static Texture2D HairBankTexture(int index)
        {
            if(hairBankTextures[index]!=null)return hairBankTextures[index];
            // (sRGB with mipmaps, as the pack's hair textures import.)
            var t=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Hair bank "+hairBank.Textures[index].Name,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            if(!t.LoadImage(hairBank.Textures[index].Png,true)){Debug.LogWarning("Hair bank: texture "+hairBank.Textures[index].Name+" does not decode.");return null;}
            return hairBankTextures[index]=t;
        }
    }
}
