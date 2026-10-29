using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The Sheldon hood on a visual body (Core/NativeSheldonHood.cs; owner 2026-10-27: "remake Sheldon clothes into functional
    // clothing items (Hood/face, ...)", "utilize magica cloth for any kind of fabric movement", "the same face on the sheldon
    // face"). A body made for a pilot wearing the Sheldon Head (VisualHooded) wears the cloth hood: a skinned renderer on its
    // head, neck, upper spine and collarbones ("Hat.Sheldon_Hood", a clothing renderer: its piping glows with the trims), its
    // cloth's paint map handed to the local Magica bridge (NativeVisualCloth.PaintMaps) before the body's cloth attaches. The
    // mesh, its two materials (the cloth from the body's own toon, its dark lining) and its maps are made once and shared. The
    // face over it (SheldonHoodFace) is the Sheldon Head's: the void shaped to the opening and the live eyes, grin and ring.
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        private static Mesh hoodMesh;private static Material hoodCloth,hoodLining;private static Texture2D hoodColour,hoodPaint;private static bool hoodFailed;
        /// <summary>The lining's colour (sRGB): the Sheldon Head's dark inside.</summary>
        private const int HoodLining=0x0b0d14;
        private static bool VisualHooded(Actor a)=>a!=null&&a.Hat==NativeSheldonHead.Hat&&a.SourceSpec!="siren";
        // Under the hood the model's own head goes (owner 2026-10-29: "sheldon helmet seems to remove the "face" on the mask, and
        // instead show the player model face"): its hair (its cloth would swing the tips through the hood), its head and its face's
        // pieces. The void covers the opening; wherever it does not draw, the hood's dark lining shows, never the model's face. On
        // a body or corpse made with the hood, before its worn renderers are read.
        private static readonly string[] UnderHood={"HairFront.","HairBack.","Head.","Eyes.","EyeBrows.","EyeLashes.","Teeth."};
        private static void VisualUnderHood(GameObject root)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(false))foreach(var p in UnderHood)if(r.name.StartsWith(p,StringComparison.Ordinal)){r.gameObject.SetActive(false);break;}
        }

        // The hood on a body being made (`root`, its skeleton at rest): its renderer, or null when it cannot be made (the
        // voxel hood shows then).
        private static SkinnedMeshRenderer VisualHoodMake(GameObject root,Transform skeleton)
        {
            if(hoodFailed)return null;
            var bones=new Transform[NativeSheldonHood.Bones.Length];
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true)){int i=Array.IndexOf(NativeSheldonHood.Bones,t.name);if(i>=0&&bones[i]==null)bones[i]=t;}
            foreach(var b in bones)if(b==null){hoodFailed=true;Debug.LogWarning("Visual body: the bake has no "+string.Join(", ",NativeSheldonHood.Bones)+"; the Sheldon Head stays the voxel hood.");return null;}
            if(!VisualHoodAssets(root))return null;
            var go=new GameObject(NativeSheldonHood.Renderer);go.transform.SetParent(root.transform,false);
            var head=bones[NativeSheldonHood.HeadBone];go.transform.SetPositionAndRotation(head.position,head.rotation);
            // Bound at rest: every body's skeleton rests alike, so the first one's bind poses serve them all.
            if(hoodMesh.bindposes.Length==0){var bind=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bind[i]=bones[i].worldToLocalMatrix*go.transform.localToWorldMatrix;hoodMesh.bindposes=bind;}
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=hoodMesh;r.bones=bones;r.rootBone=head;r.sharedMaterials=new[]{hoodCloth,hoodLining};
            r.localBounds=hoodMesh.bounds;r.updateWhenOffscreen=false;r.skinnedMotionVectors=false;
            NativeVisualCloth.PaintMaps[NativeSheldonHood.Renderer]=hoodPaint;
            return r;
        }

        // The shared mesh, materials and maps (made once, from the body's own toon material).
        private static bool VisualHoodAssets(GameObject root)
        {
            if(hoodMesh!=null)return true;
            Material template=null;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){var m=r.sharedMaterial;if(m!=null&&m.HasProperty("_Texture2D")&&m.HasProperty("_Color_1")){template=m;if(r.name.StartsWith("Top.",StringComparison.Ordinal))break;}}
            if(template==null){hoodFailed=true;Debug.LogWarning("Visual body: no toon material for the Sheldon hood; it stays the voxel hood.");return false;}
            var built=NativeSheldonHood.Build();int n=built.Positions.Length;
            var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var uv=new Vector2[n];var weights=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                var p=built.Positions[i];var q=built.Normals[i];var c=built.Colors[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(q.X,q.Y,q.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);uv[i]=new Vector2(built.Uv[i].X,built.Uv[i].Y);
                int k=i*4;weights[i]=new BoneWeight{boneIndex0=built.Bones[k],weight0=built.Weights[k],boneIndex1=built.Bones[k+1],weight1=built.Weights[k+1],boneIndex2=built.Bones[k+2],weight2=built.Weights[k+2],boneIndex3=built.Bones[k+3],weight3=built.Weights[k+3]};
            }
            var mesh=new Mesh{name=NativeSheldonHood.Renderer,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.boneWeights=weights;mesh.subMeshCount=2;
            mesh.SetTriangles(built.Outer,0);mesh.SetTriangles(built.Inner,1);mesh.RecalculateBounds();
            // The maps: the colour weights (the cloth, the piping) and the cloth's paint map, readable for the simulation.
            int size=NativeSheldonHood.MapSize;var colour=new byte[size*size*4];var paint=new byte[size*size*4];NativeSheldonHood.Maps(colour,paint);
            hoodColour=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Sheldon hood colours",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};hoodColour.LoadRawTextureData(colour);hoodColour.Apply(false,true);
            hoodPaint=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Sheldon hood cloth",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};hoodPaint.LoadRawTextureData(paint);hoodPaint.Apply(false,false);
            hoodCloth=VisualHoodMaterial(template,"Sheldon hood",NativeSheldonWear.Cloth,true);hoodLining=VisualHoodMaterial(template,"Sheldon hood lining",HoodLining,false);
            hoodMesh=mesh;return true;
        }
        private static Material VisualHoodMaterial(Material template,string name,int cloth,bool outline)
        {
            var m=new Material(template){name=name,hideFlags=HideFlags.HideAndDontSave};
            m.SetTexture("_Texture2D",hoodColour);m.SetFloat("_UseCustomTexture",1);m.SetFloat("_Cull",2);m.SetFloat("_UseOutline",outline?1:0);
            var c=WearColor(cloth);for(int i=0;i<BodyColours.Length;i++)m.SetColor(BodyColours[i],i%3==2?WearGlow:c);
            return m;
        }

        // The Sheldon Head's face over a body's cloth hood (its void shaped to the opening, the live eyes, grin and ring on the
        // face's fit, VisualHatBone): false when `rig`'s body wears no hood (the voxel hood draws instead).
        private bool SheldonHoodFace(CapturedAnimation.Player rig,Actor a,Func<int,Matrix4x4> bone,float time,int layer,Camera camera,Func<int,Color> partTint,float flash)
        {
            if(!visualBodies.TryGetValue(rig,out var body)||body.HoodRenderer==null||sourceModels==null||!sourceModels.Has(NativeSourceModels.SheldonHoodVoid))return false;
            var glow=HatBody(a.Hat,a.HatColor)??Color.white;
            sourceModels.Queue(NativeSourceModels.SheldonHoodVoid,rig.Root.transform.localToWorldMatrix,bone,null,layer,camera,body:glow,partTint:partTint,hitFlash:flash);
            var face=sheldonScripted?sheldonScriptFace:NativeSheldonHead.Evaluate(time,a.Id,SheldonHitAge(a.Id,time),SheldonKillAge(a.Id,time),HaloReduced);
            sourceModels.QueueSheldonPieces(bone(CapturedRig.Head),face,glow,layer,camera,1,flash,partTint?.Invoke(CapturedRig.Head)??Color.white);
            return true;
        }
        // The same at rest on a body no arena pass draws the hat of (the portraits, the rooftop, the draft's pilot, the revive
        // gallery): its void and the idle face and ring on `time`.
        private bool SheldonHoodStill(VisualBody body,string hat,string color,Matrix4x4 root,Func<int,Matrix4x4> bone,float time,int seed,bool reduced,int layer,Camera camera)
        {
            if(hat!=NativeSheldonHead.Hat||body?.HoodRenderer==null||sourceModels==null||!sourceModels.Has(NativeSourceModels.SheldonHoodVoid))return false;
            var glow=HatBody(hat,color)??Color.white;sourceModels.Queue(NativeSourceModels.SheldonHoodVoid,root,bone,null,layer,camera,body:glow);
            sourceModels.QueueSheldonPieces(bone(CapturedRig.Head),NativeSheldonHead.Evaluate(time,seed,-1,-1,reduced),glow,layer,camera,1,1,Color.white);return true;
        }
        // On a dead model's head (a corpse's, a death scene's) fitted at `fit`: its void and the face as it died, at `alpha`.
        private bool VisualDeadHood(Matrix4x4 fit,NativeDeath death,float alpha)
        {
            var a=death.actor;if(a==null||sourceModels==null||!sourceModels.Has(NativeSourceModels.SheldonHoodVoid))return false;
            corpseHat=fit;corpseHatBone??=_=>corpseHat;var glow=HatBody(a.Hat,a.HatColor)??Color.white;
            sourceModels.Queue(NativeSourceModels.SheldonHoodVoid,Matrix4x4.identity,corpseHatBone,null,body:glow,opacity:alpha);
            sourceModels.QueueSheldonPieces(corpseHat,NativeSheldonHead.Evaluate(death.at,death.id,-1,-1,HaloReduced),glow,0,null,alpha,1,Color.white);return true;
        }
    }
}
