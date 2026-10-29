using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The pack weapons on the visual body (Core/NativePackWeapons.cs; docs/BOZO-INTEGRATION.md "Pack weapons"). Where the
    // local bake carries no life-size model of a weapon (VisualWeapons: the pistol, rifle, machine gun, sword and greatsword),
    // an arena pilot's visual body draws the weapons pack bank's (Resources/ChipArenaPackWeapons, the licensed-assets zip):
    // the daggers on both hands, the shield on the right, the bow and the caster staffs on the left. Each is one mesh in its
    // weapon frame, built once from the bank for every body (Core NativePackWeapons.Shape: the grip on the game's grip point,
    // the tip on the game's sockets), on the visual body's toon with the pack's texture as drawn (the staffs' crystal in the
    // school's colour, glowing); each body makes its GameObjects once per weapon and shows or hides them as the bake's models
    // (VisualWeaponDrawn, VisualWeaponShown), so nothing is allocated per frame once warm. The rules for when it draws are the
    // bake's: the Standard skin in hand on an arena pilot's visual body or the first-person view body (the Prototype Series,
    // the Classic voxel body, the entrances, the menus and the portraits keep the game's model), hidden with the weapon put
    // away or thrown (the main dagger alone hides its copy). Without the bank, its prop or the shader, every pilot draws
    // today's model.
    public sealed partial class ArenaPresentation
    {
        // One weapon's built pieces, shared by every body: the fit, the mesh (skinned to the riser and two limb bones for the
        // bow) and its materials. Null where it cannot be built.
        private sealed class PackWeaponAsset{public NativePackWeapons.Fit Fit;public Mesh Mesh;public Material[] Materials;public Bounds Bounds;}
        private static readonly Dictionary<string,PackWeaponAsset> packWeaponAssets=new Dictionary<string,PackWeaponAsset>(StringComparer.Ordinal);
        private static readonly Dictionary<int,Material> packWeaponMaterials=new Dictionary<int,Material>();
        private static Shader packWeaponShader;private static bool packWeaponShaderRead;
        private static readonly int UseCustomId=Shader.PropertyToID("_UseCustomTexture"),UseOutlineId=Shader.PropertyToID("_UseOutline"),CullId=Shader.PropertyToID("_Cull"),PackColour1Id=Shader.PropertyToID("_Color_1"),PackColour2Id=Shader.PropertyToID("_Color_2");

        // The pack weapon for `spec` on `body` (made on first use: its mount, model and, for daggers, the off-hand copy, under
        // the body's root on its layer, the model hidden), or null without one.
        private VisualWeapon PackWeaponOf(VisualBody body,string spec)
        {
            body.Packs??=new Dictionary<string,VisualWeapon>(StringComparer.Ordinal);
            if(body.Packs.TryGetValue(spec,out var w))return w;
            var asset=PackWeaponAssetOf(spec);w=asset!=null&&body.Root!=null?PackWeaponMake(body.Root,asset):null;
            body.Packs[spec]=w;return w;
        }
        private static VisualWeapon PackWeaponMake(GameObject root,PackWeaponAsset asset)
        {
            var fit=asset.Fit;int layer=root.layer;
            Transform Mount(string name){var t=new GameObject(name){layer=layer}.transform;t.SetParent(root.transform,false);return t;}
            GameObject Model(Transform mount,out Transform upper,out Transform lower)
            {
                var model=new GameObject("Model"){layer=layer};model.transform.SetParent(mount,false);upper=lower=null;
                if(fit.Limbs)
                {
                    // The riser is the model itself; each limb a bone on the mount, bent about its pivot with the pull (PackBowDrawn).
                    upper=new GameObject("Upper limb"){layer=layer}.transform;upper.SetParent(mount,false);lower=new GameObject("Lower limb"){layer=layer}.transform;lower.SetParent(mount,false);
                    var skin=model.AddComponent<SkinnedMeshRenderer>();skin.bones=new[]{model.transform,upper,lower};skin.rootBone=mount;skin.sharedMesh=asset.Mesh;skin.sharedMaterials=asset.Materials;
                    skin.localBounds=asset.Bounds;skin.updateWhenOffscreen=false;skin.quality=SkinQuality.Bone2;
                }
                else{model.AddComponent<MeshFilter>().sharedMesh=asset.Mesh;model.AddComponent<MeshRenderer>().sharedMaterials=asset.Materials;}
                model.SetActive(false);return model;
            }
            var w=new VisualWeapon{Pack=true,Seat=fit.Seat,Mount=Mount("Pack weapon "+fit.Spec)};
            w.Model=Model(w.Mount,out w.Upper,out w.Lower);
            if(fit.Dual){w.LeftMount=Mount("Pack weapon "+fit.Spec+" (off hand)");w.LeftModel=Model(w.LeftMount,out _,out _);}
            return w;
        }

        // The built pieces for `spec` (once per run; null without the bank, its prop or the shader).
        private static PackWeaponAsset PackWeaponAssetOf(string spec)
        {
            if(packWeaponAssets.TryGetValue(spec,out var asset))return asset;
            asset=null;var fit=NativePackWeapons.For(spec);var bank=fit!=null?NativePackBanks.Bank(NativePackWeapons.Bank):null;
            if(bank!=null)
            {
                try{asset=PackWeaponBuild(bank,fit);}
                catch(Exception e){asset=null;Debug.LogWarning("Pack weapon "+spec+": "+e.Message+"; the game's model draws.");}
            }
            packWeaponAssets[spec]=asset;return asset;
        }
        private static PackWeaponAsset PackWeaponBuild(NativePackBank.Bank bank,NativePackWeapons.Fit fit)
        {
            if(!packWeaponShaderRead){packWeaponShaderRead=true;var shader=Resources.Load<Shader>("NativeVisualBodyToon");packWeaponShader=shader!=null&&shader.isSupported?shader:null;}
            if(packWeaponShader==null)return null;
            var shaped=NativePackWeapons.Shape(bank,fit);if(shaped==null||shaped.Positions.Length==0||shaped.Subs.Count==0)return null;
            int n=shaped.Positions.Length;var v=new Vector3[n];var nr=new Vector3[n];var uv=new Vector2[n];var colours=new Color32[n];
            for(int i=0;i<n;i++){var p=shaped.Positions[i];v[i]=new Vector3(p.X,p.Y,p.Z);var q=shaped.Normals[i];nr[i]=new Vector3(q.X,q.Y,q.Z);uv[i]=new Vector2(shaped.Uv[i].X,shaped.Uv[i].Y);colours[i]=new Color32(255,0,0,0);}
            var mesh=new Mesh{name="Pack weapon "+fit.Spec};if(n>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            // The vertex colour picks the toon's first colour layer (the weapon-colour piece's) and its alpha keeps the outline off.
            mesh.vertices=v;mesh.normals=nr;mesh.uv=uv;mesh.colors32=colours;mesh.subMeshCount=shaped.Subs.Count;
            var materials=new Material[shaped.Subs.Count];
            for(int s=0;s<shaped.Subs.Count;s++){mesh.SetTriangles(shaped.Subs[s].Indices,s,false);materials[s]=shaped.Subs[s].Material<0?PackWeaponTint(fit):PackWeaponMaterial(bank,shaped.Subs[s].Material);if(materials[s]==null)return null;}
            if(fit.Limbs)
            {
                // Riser bone 0, upper limb 1, lower limb 2; every bone at the model's frame at rest.
                var weights=new BoneWeight[n];
                for(int i=0;i<n;i++){float w=shaped.Limb[i],k=Mathf.Abs(w);weights[i]=new BoneWeight{boneIndex0=0,weight0=1-k,boneIndex1=w>0?1:w<0?2:0,weight1=k};}
                mesh.boneWeights=weights;mesh.bindposes=new[]{Matrix4x4.identity,Matrix4x4.identity,Matrix4x4.identity};
            }
            mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.2f);
            mesh.UploadMeshData(true);
            return new PackWeaponAsset{Fit=fit,Mesh=mesh,Materials=materials,Bounds=bounds};
        }
        // A bank material on the visual body's toon: its texture as drawn, cut out only where the pack's material is, no outline.
        private static Material PackWeaponMaterial(NativePackBank.Bank bank,int index)
        {
            if(packWeaponMaterials.TryGetValue(index,out var m))return m;
            m=null;var src=index>=0&&index<bank.Materials.Count?bank.Materials[index]:null;
            if(src!=null)
            {
                m=new Material(packWeaponShader){name="Pack weapon "+src.Name,enableInstancing=false};
                var tex=NativePackBanks.Texture(NativePackWeapons.Bank,src.Albedo);m.SetTexture(TextureId,tex!=null?tex:Texture2D.whiteTexture);
                m.SetFloat(UseCustomId,0);m.SetFloat(UseOutlineId,0);m.SetFloat(CutoffId,(src.Flags&NativePackBank.MatCutout)!=0?.5f:0);m.SetFloat(CullId,(src.Flags&NativePackBank.MatTwoSided)!=0?0:2);
                m.SetFloat(HitFlashId,1);m.SetFloat(AccentGlowId,0);
            }
            packWeaponMaterials[index]=m;return m;
        }
        // The weapon-colour piece (a staff's crystal): the toon's nine-colour mode on a white map, its first layer black but its
        // accent, which is the weapon's colour (linear) and glows by the fit's Glow.
        private static Material PackWeaponTint(NativePackWeapons.Fit fit)
        {
            var m=new Material(packWeaponShader){name="Pack weapon "+fit.Spec+" colour",enableInstancing=false};
            var c=new Color(((fit.Color>>16)&255)/255f,((fit.Color>>8)&255)/255f,(fit.Color&255)/255f).linear;
            m.SetTexture(TextureId,Texture2D.whiteTexture);m.SetFloat(UseCustomId,1);m.SetFloat(UseOutlineId,0);m.SetFloat(CutoffId,0);m.SetFloat(CullId,2);m.SetFloat(HitFlashId,1);
            m.SetColor(PackColour1Id,Color.black);m.SetColor(PackColour2Id,Color.black);m.SetColor(AccentChannels[0],c);m.SetFloat(AccentGlowId,fit.Glow);
            return m;
        }

        // BowDraw, for a pilot drawn with its visual body: the pack bow in place of the riser and limbs, at the bow hand
        // (`hand`), its limbs bent by the pull as the game's limb pieces are (Core NativeBowRig.Limb), so the string's halves
        // reach its tips. False: the game's riser and limbs draw.
        private bool PackBowDrawn(CapturedAnimation.Player rig,Matrix4x4 hand,float pull)
        {
            var body=VisualHolder(rig);if(body==null||body.Held==null||!body.Held.Pack||body.Held.Upper==null||body.PosedFrame!=Time.frameCount)return false;
            var w=body.Held;if(!VisualWeaponMount(w.Mount,hand))return false;
            for(int l=0;l<2;l++){var piece=BowPiece(NativeBowRig.Limb(l==0,pull));var bone=l==0?w.Upper:w.Lower;bone.localPosition=piece.GetColumn(3);bone.localRotation=piece.rotation;}
            if(body.Shown!=w){if(body.Shown!=null)body.Shown.Hide();body.Shown=w;}
            if(!w.Model.activeSelf)w.Model.SetActive(true);
            body.ShownFrame=Time.frameCount;return true;
        }
    }
}
