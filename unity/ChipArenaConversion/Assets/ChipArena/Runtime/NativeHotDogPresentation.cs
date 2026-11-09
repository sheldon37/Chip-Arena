using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Hot Dog on the model (Core NativeHotDog): the bank's one skinned mesh (tools/gear/hotdog_tripo.py: the owner's Tripo
    // model retargeted) on the body's own bones, in place of everything the body wears, so every route that dresses a body (the
    // live pilot, first person's forearms, corpses and their cuts, the draft, the broadcast's bust, portraits, the Locker) draws
    // it. Its limbs are weighted to the body's arm, hand, finger, leg and foot bones; its sausage to twins of the spine's bones
    // (bank names "~pelvis" ..: a child of that bone, made here, at rest where its bone is), which its jiggle moves
    // (HotDogJiggle) so the arms, the hands and what they hold never do. Its bind poses are the body's, read once off the body's
    // mesh (a twin's its bone's). It draws in the model's toon with the bank's texture, so the outline, hit flash, wounds and
    // dissolve are the body's own. Without the bank a pilot in it wears the default top (VisualLookOf). Presentation only.
    public sealed partial class ArenaPresentation
    {
        public const string HotDogBank="ChipArenaHotDog";
        public const string HotDogRenderer="HotDog";
        /// <summary>A sausage twin's name: this and its bone's ("HotDog~pelvis"); the hat anchor on the head's twin.</summary>
        public const string HotDogTwin="HotDog~",HotDogHat="HotDog hat";
        /// <summary>How much further it reaches than the body's bake bounds (m): its crown over the pilot's head, its jiggle, its
        /// longer shoes and gloves.</summary>
        private const float HotDogBoundsPad=.3f;
        private static Mesh hotDogMesh;private static string[] hotDogBones;private static Texture2D hotDogTexture;private static Material hotDogMaterial;
        private static int hotDogState; // 0 unread, 1 read, -1 none
        private static bool hotDogBound;
        private static bool HotDogRead()
        {
            if(hotDogState!=0)return hotDogState>0;hotDogState=-1;
            var asset=Resources.Load<TextAsset>(HotDogBank);if(asset==null)return false;
            try
            {
                var b=asset.bytes;int at=0;
                int I(){int v=BitConverter.ToInt32(b,at);at+=4;return v;}float Fl(){float v=BitConverter.ToSingle(b,at);at+=4;return v;}
                string S(){int k=BitConverter.ToUInt16(b,at);at+=2;var t=System.Text.Encoding.UTF8.GetString(b,at,k);at+=k;return t;}
                if(b.Length<16||b[0]!='H'||b[1]!='D'||b[2]!='O'||b[3]!='G'){Debug.LogWarning("Hot Dog: the bank is not one.");return false;}
                at=4;if(I()!=2){Debug.LogWarning("Hot Dog: an unknown bank version (rebuild it with tools/gear/hotdog_tripo.py).");return false;}
                var names=new string[I()];for(int i=0;i<names.Length;i++)names[i]=S();
                int n=I();var pos=new Vector3[n];var nor=new Vector3[n];var col=new Color32[n];var uv=new Vector2[n];var weights=new BoneWeight[n];
                for(int i=0;i<n;i++)pos[i]=new Vector3(Fl(),Fl(),Fl());
                for(int i=0;i<n;i++)nor[i]=new Vector3(Fl(),Fl(),Fl());
                for(int i=0;i<n;i++){col[i]=new Color32(b[at],b[at+1],b[at+2],b[at+3]);at+=4;}
                for(int i=0;i<n;i++)uv[i]=new Vector2(Fl(),Fl());
                var idx=new int[n*4];for(int i=0;i<idx.Length;i++){idx[i]=BitConverter.ToUInt16(b,at);at+=2;}
                for(int i=0;i<n;i++)
                {
                    float w0=Fl(),w1=Fl(),w2=Fl(),w3=Fl();
                    weights[i]=new BoneWeight{boneIndex0=idx[i*4],weight0=w0,boneIndex1=idx[i*4+1],weight1=w1,boneIndex2=idx[i*4+2],weight2=w2,boneIndex3=idx[i*4+3],weight3=w3};
                }
                var tri=new int[I()*3];for(int i=0;i<tri.Length;i++)tri[i]=I();
                int texLength=I();Texture2D tex=null;
                if(texLength>0)
                {
                    var bytes=new byte[texLength];Buffer.BlockCopy(b,at,bytes,0,texLength);at+=texLength;
                    tex=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Hot Dog",hideFlags=HideFlags.HideAndDontSave};
                    if(!tex.LoadImage(bytes,false)){Debug.LogWarning("Hot Dog: the bank's texture is unreadable.");UnityEngine.Object.Destroy(tex);tex=null;}
                    else{tri=HotDogFaceOff(tex,pos,nor,uv,weights,tri); // its sculpted face off, the animated one's place read (NativeHotDogFacePresentation.cs)
                        tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Trilinear;tex.anisoLevel=4;tex.Apply(true,false);tex.Compress(true);tex.Apply(false,true);}
                }
                var mesh=new Mesh{name="Hot Dog",hideFlags=HideFlags.HideAndDontSave};if(n>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices=pos;mesh.normals=nor;mesh.colors32=col;mesh.uv=uv;mesh.boneWeights=weights;mesh.triangles=tri;mesh.RecalculateBounds();
                hotDogMesh=mesh;hotDogBones=names;hotDogTexture=tex;hotDogState=1;return true;
            }
            catch(Exception e){Debug.LogWarning("Hot Dog: the bank is unreadable ("+e.Message+").");return false;}
        }
        // Its material: the model's toon with the bank's texture as its colours (no accent recolour, no skin tone), outlined.
        private static Material HotDogMaterial()
        {
            if(hotDogMaterial!=null)return hotDogMaterial;
            var shader=Resources.Load<Shader>("NativeVisualBodyToon");if(shader==null||!shader.isSupported)return null;
            var m=new Material(shader){name="Hot Dog",hideFlags=HideFlags.HideAndDontSave};
            m.SetFloat(UseCustomId,0);m.SetFloat(VertexAlbedoId,0);m.SetFloat(UseOutlineId,1);m.SetFloat(CullId,2);m.SetFloat(CutoffId,.5f);
            if(hotDogTexture!=null)m.SetTexture(TextureId,hotDogTexture);
            return hotDogMaterial=m;
        }
        /// <summary>The look a body is dressed in: a look in the Hot Dog wears the default top when the bank is absent.</summary>
        private static string HotDogOrTop(string look)=>NativeHotDog.Worn(look)&&!HotDogRead()?NativeLook.With(look,NativeLook.Upper,NativeLook.Ids[NativeLook.SlotIndex(NativeLook.Upper)][0]):look;
        /// <summary>VisualDress, in the Hot Dog: everything the body would wear off, its one mesh on the body's bones and the
        /// sausage's twins.</summary>
        private static void HotDogWear(GameObject root)
        {
            var mat=HotDogRead()?HotDogMaterial():null;if(mat==null)return;
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);SkinnedMeshRenderer body=null;
            foreach(var s in skins)if(s.name.StartsWith("Body.",StringComparison.Ordinal)&&s.sharedMesh!=null){body=s;break;}if(body==null)return;
            var named=new Dictionary<string,Transform>(StringComparer.Ordinal);var bodyBones=body.bones;
            foreach(var t in bodyBones)if(t!=null)named[t.name]=t;
            // Each bank bone: the body's own, or a sausage twin made under it.
            var bones=new Transform[hotDogBones.Length];var parents=new Transform[hotDogBones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                var name=hotDogBones[i];bool twin=name.StartsWith("~",StringComparison.Ordinal);var of=twin?name.Substring(1):name;
                if(!named.TryGetValue(of,out parents[i])){Debug.LogWarning("Hot Dog: the body lacks "+of+".");return;}
            }
            if(!hotDogBound)
            {
                var bind=body.sharedMesh.bindposes;var pose=new Matrix4x4[bones.Length];
                for(int i=0;i<bones.Length;i++){int at=Array.IndexOf(bodyBones,parents[i]);if(at<0||at>=bind.Length)return;pose[i]=bind[at];}
                hotDogMesh.bindposes=pose;hotDogBound=true;
            }
            for(int i=0;i<bones.Length;i++)
            {
                if(!hotDogBones[i].StartsWith("~",StringComparison.Ordinal)){bones[i]=parents[i];continue;}
                var twin=new GameObject(HotDogTwin+hotDogBones[i].Substring(1)).transform;twin.SetParent(parents[i],false);bones[i]=twin;
                // Hats ride an anchor on the head's twin, on the sausage's crown (HotDogHatOn), so they wobble with it.
                if(hotDogBones[i]=="~head"){var hat=new GameObject(HotDogHat).transform;hat.SetParent(twin,false);hat.localPosition=new Vector3(NativeHotDog.HatOffset.X,NativeHotDog.HatOffset.Y,NativeHotDog.HatOffset.Z);}
            }
            var weaponHolder=root.transform.Find("VisualWeapons");
            foreach(var s in skins)if(weaponHolder==null||!s.transform.IsChildOf(weaponHolder))s.gameObject.SetActive(false);
            var go=new GameObject(HotDogRenderer){layer=body.gameObject.layer};var at0=go.transform;at0.SetParent(body.transform.parent,false);
            at0.localPosition=body.transform.localPosition;at0.localRotation=body.transform.localRotation;at0.localScale=body.transform.localScale;
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=hotDogMesh;r.bones=bones;r.rootBone=body.rootBone;
            // Its bounds from its own vertices at the bind, in its root bone's frame, and kept up to date as it moves: the body's box sat a
            // metre low on it, so a shot looking up at it or close on it culled it (owner 2026-11-09: "Hotdog disappears from intro for a
            // moment"; the ult's twirl too).
            hotDogBox??=HotDogBox(body,bodyBones);var box=hotDogBox??body.localBounds;box.Expand(HotDogBoundsPad*2);r.localBounds=box;r.sharedMaterial=mat;r.updateWhenOffscreen=true;
            // Its animated face on the head's twin (NativeHotDogFacePresentation.cs).
            int head=Array.IndexOf(hotDogBones,"~head");if(head>=0)HotDogFaceAttach(bones[head],r);
        }

        private static Bounds? hotDogBox;
        /// <summary>The Hot Dog mesh's box in the body's root bone frame at the bind (its vertices through that bone's bind pose).</summary>
        private static Bounds? HotDogBox(SkinnedMeshRenderer body,Transform[] bodyBones)
        {
            int root=Array.IndexOf(bodyBones,body.rootBone);var bind=body.sharedMesh.bindposes;if(root<0||root>=bind.Length||hotDogMesh==null)return null;
            var vs=hotDogMesh.vertices;if(vs.Length==0)return null;var m=bind[root];var box=new Bounds(m.MultiplyPoint3x4(vs[0]),Vector3.zero);foreach(var v in vs)box.Encapsulate(m.MultiplyPoint3x4(v));return box;
        }
        // ---- The sausage's jiggle (Core NativeHotDog.Jiggle) ----
        // Its twins (pelvis up to head) and their spring, found on the body's first jiggle.
        private sealed class HotDogRig
        {
            public Transform Pelvis;public Transform[] Twins;public NativeHotDog.Jiggle Spring;public bool Moved;
        }
        // VisualBodyPose, once the live pilot's body is posed: the spring stepped on the body's floor point (its root: the pilot's
        // motion without the clips' bob) and its twins set so the sausage leans about the pelvis (bending: more toward its top)
        // and squashes along the body's up.
        private void HotDogJiggle(VisualBody body)
        {
            if(!NativeHotDog.Worn(body.Look))return;
            var rig=body.Dog;
            if(rig==null)
            {
                rig=body.Dog=new HotDogRig();var twins=new List<Transform>();
                foreach(var t in body.Root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith(HotDogTwin,StringComparison.Ordinal)){twins.Add(t);if(t.name==HotDogTwin+"pelvis")rig.Pelvis=t.parent;}
                rig.Twins=twins.ToArray();
            }
            if(rig.Pelvis==null||rig.Twins.Length==0)return;
            var pelvis=rig.Pelvis.position;var floor=body.Root.transform.position;
            NativeHotDog.Step(ref rig.Spring,new System.Numerics.Vector3(floor.x,floor.y,floor.z),Time.deltaTime);
            NativeHotDog.Pose(rig.Spring,out var ax,out float angle,out float squash);
            if(angle<1e-4f&&Mathf.Abs(squash)<1e-4f){if(rig.Moved){foreach(var t in rig.Twins){t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;}rig.Moved=false;}return;}
            var axis=new Vector3(ax.X,ax.Y,ax.Z);var up=Vector3.up;
            float sy=1-squash,sxz=1/Mathf.Sqrt(Mathf.Max(.5f,sy));
            // The squash along world up about the pelvis (the same for every twin), then each twin's share of the lean.
            var squashAbout=Matrix4x4.Translate(pelvis)*Matrix4x4.Scale(new Vector3(sxz,sy,sxz))*Matrix4x4.Translate(-pelvis);
            foreach(var t in rig.Twins)
            {
                var bone=t.parent;float h=Vector3.Dot(bone.position-pelvis,up);
                var lean=Matrix4x4.Translate(pelvis)*Matrix4x4.Rotate(Quaternion.AngleAxis(angle*NativeHotDog.Bend(h)*Mathf.Rad2Deg,axis))*Matrix4x4.Translate(-pelvis);
                var local=bone.worldToLocalMatrix*lean*squashAbout*bone.localToWorldMatrix;
                t.localPosition=local.GetColumn(3);t.localRotation=local.rotation;t.localScale=local.lossyScale;
            }
            rig.Moved=true;
        }
        /// <summary>The frame hats ride on head bone `head`: in the Hot Dog, the anchor on its head's twin (on the sausage's crown,
        /// wobbling with it); otherwise the head itself (null for null).</summary>
        private static Transform HotDogHatOn(Transform head)
        {
            if(head==null)return null;var anchor=head.Find(HotDogTwin+"head/"+HotDogHat);return anchor!=null?anchor:head;
        }
        /// <summary>Whether a body being made (`root`) wears the Hot Dog (its renderer is there): no Sheldon hood on it.</summary>
        private static bool HotDogOn(GameObject root)
        {
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name==HotDogRenderer)return true;return false;
        }
    }
}
