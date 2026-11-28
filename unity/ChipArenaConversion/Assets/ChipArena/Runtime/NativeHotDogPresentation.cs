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
        // Its hats (Core NativeHotDog.HatFit): the crown's four bones and weights (any vertex over the head: they share them) and
        // the head bone's place at the bind, read once bound; the mesh without its sprigs (worn under a closed hat); each body's
        // renderer by its root and by its hat anchor.
        private static int[] crownBone;private static float[] crownWeight;private static Vector3 crownHead;private static Mesh hotDogSprigless;
        private static readonly Dictionary<GameObject,SkinnedMeshRenderer> hotDogByRoot=new Dictionary<GameObject,SkinnedMeshRenderer>();
        private static readonly Dictionary<Transform,SkinnedMeshRenderer> hotDogByHat=new Dictionary<Transform,SkinnedMeshRenderer>();
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
                hotDogMesh.bindposes=pose;hotDogBound=true;HotDogCrownRead();
            }
            for(int i=0;i<bones.Length;i++)
            {
                if(!hotDogBones[i].StartsWith("~",StringComparison.Ordinal)){bones[i]=parents[i];continue;}
                var twin=new GameObject(HotDogTwin+hotDogBones[i].Substring(1)).transform;twin.SetParent(parents[i],false);bones[i]=twin;
                // Hats ride an anchor on the head's twin, placed on the sausage's crown as each is drawn (HotDogHatOn).
                if(hotDogBones[i]=="~head"){var hat=new GameObject(HotDogHat).transform;hat.SetParent(twin,false);}
            }
            var weaponHolder=root.transform.Find("VisualWeapons");
            foreach(var s in skins)if(weaponHolder==null||!s.transform.IsChildOf(weaponHolder))s.gameObject.SetActive(false);
            var go=new GameObject(HotDogRenderer){layer=body.gameObject.layer};var at0=go.transform;at0.SetParent(body.transform.parent,false);
            at0.localPosition=body.transform.localPosition;at0.localRotation=body.transform.localRotation;at0.localScale=body.transform.localScale;
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=hotDogMesh;r.bones=bones;r.rootBone=body.rootBone;
            HotDogKeep(root,r,bones);
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
            // Its twins bottom to top, each one's bone's turn at the bind (the mesh's frame), and its renderer.
            public Transform Pelvis;public Transform[] Twins;public Quaternion[] Rest;public SkinnedMeshRenderer Skin;public NativeHotDog.Jiggle Spring;
            public Quaternion[] Turn,Eased;
        }
        // VisualBodyPose, once the live pilot's body is posed: the spring stepped on the body's floor point (its root: the pilot's
        // motion without the clips' bob) and its twins set so the sausage leans about the pelvis (bending: more toward its top)
        // and squashes along the body's up.
        private void HotDogJiggle(VisualBody body)
        {
            if(!NativeHotDog.Worn(body.Look))return;
            var rig=body.Dog;
            if(rig==null||rig.Skin==null)
            {
                rig=body.Dog=new HotDogRig();var twins=new List<Transform>();
                // Only the twins the sausage's renderer skins with. The body's motion and stride proxies are copies of the skeleton
                // made after the twins, so each carries twins of its own, posed by other clips; gathered with the rest, they had the
                // evened sausage mixing three skeletons' poses, and in a pack clip (the Katana's parry and counter, 2026-11-11: "morph
                // player model in parry hit") it folded and wrung.
                hotDogByRoot.TryGetValue(body.Root,out var drawn);
                if(drawn!=null)foreach(var t in drawn.bones)if(t!=null&&t.name.StartsWith(HotDogTwin,StringComparison.Ordinal)){twins.Add(t);if(t.name==HotDogTwin+"pelvis")rig.Pelvis=t.parent;}
                // Bottom to top by where their bones rest, each with that rest turn (the bank's bind poses).
                var bind=hotDogMesh!=null?hotDogMesh.bindposes:new Matrix4x4[0];var rest=new List<(Transform t,float y,Quaternion q)>();
                foreach(var t in twins){int i=Array.IndexOf(hotDogBones,"~"+t.name.Substring(HotDogTwin.Length));if(i>=0&&i<bind.Length){var m=bind[i].inverse;rest.Add((t,m.m13,m.rotation));}}
                rest.Sort((a,b)=>a.y.CompareTo(b.y));rig.Twins=rest.ConvertAll(r=>r.t).ToArray();rig.Rest=rest.ConvertAll(r=>r.q).ToArray();
                rig.Turn=new Quaternion[rig.Twins.Length];rig.Eased=new Quaternion[rig.Twins.Length];hotDogByRoot.TryGetValue(body.Root,out rig.Skin);
            }
            if(rig.Pelvis==null||rig.Twins.Length==0||rig.Skin==null)return;
            var pelvis=rig.Pelvis.position;var floor=body.Root.transform.position;
            NativeHotDog.Step(ref rig.Spring,new System.Numerics.Vector3(floor.x,floor.y,floor.z),Time.deltaTime);
            NativeHotDog.Pose(rig.Spring,out var ax,out float angle,out float squash);
            var axis=new Vector3(ax.X,ax.Y,ax.Z);var up=Vector3.up;
            // The bend kept even (NativeHotDog.SpineEven): each twin's turn against its rest (the body's own turn off), eased toward
            // its neighbours' with the ends as posed.
            int n=rig.Twins.Length;var body0=rig.Skin.transform.rotation;var turn=rig.Turn;var eased=rig.Eased;
            for(int k=0;k<n;k++)turn[k]=eased[k]=rig.Twins[k].parent.rotation*Quaternion.Inverse(body0*rig.Rest[k]);
            for(int pass=0;pass<NativeHotDog.SpinePasses;pass++)
            {
                var prev=eased[0];
                for(int k=1;k<n-1;k++){var mid=Quaternion.Slerp(prev,eased[k+1],.5f);prev=eased[k];eased[k]=Quaternion.Slerp(eased[k],mid,.5f);}
            }
            float sy=1-squash,sxz=1/Mathf.Sqrt(Mathf.Max(.5f,sy));
            // The squash along world up about the pelvis (the same for every twin), then each twin's share of the lean.
            var squashAbout=Matrix4x4.Translate(pelvis)*Matrix4x4.Scale(new Vector3(sxz,sy,sxz))*Matrix4x4.Translate(-pelvis);
            foreach(var t in rig.Twins)
            {
                var bone=t.parent;float h=Vector3.Dot(bone.position-pelvis,up);
                var lean=Matrix4x4.Translate(pelvis)*Matrix4x4.Rotate(Quaternion.AngleAxis(angle*NativeHotDog.Bend(h)*Mathf.Rad2Deg,axis))*Matrix4x4.Translate(-pelvis);
                int k=Array.IndexOf(rig.Twins,t);var even=Quaternion.Slerp(turn[k],eased[k],NativeHotDog.SpineEven)*body0*rig.Rest[k];
                var local=bone.worldToLocalMatrix*lean*squashAbout*Matrix4x4.TRS(bone.position,even,bone.lossyScale);
                t.localPosition=local.GetColumn(3);t.localRotation=local.rotation;t.localScale=local.lossyScale;
            }
        }
        // ---- Hats on the crown (Core NativeHotDog.HatFit) ----
        // Once bound: the crown's skinning (the bones and weights of a vertex over the head that isn't a sprig) and the head bone's
        // place at the bind, and the mesh without its sprigs (the small parts wholly within SprigDepth of its top).
        private static void HotDogCrownRead()
        {
            var bind=hotDogMesh.bindposes;int head=Array.IndexOf(hotDogBones,"~head");if(head<0||head>=bind.Length)return;
            crownHead=bind[head].inverse.GetColumn(3);
            var pos=hotDogMesh.vertices;var weights=hotDogMesh.boneWeights;var tri=hotDogMesh.triangles;float top=float.MinValue;foreach(var p in pos)top=Mathf.Max(top,p.y);
            // The parts, welded where the bank split them at texture seams.
            var key=new Dictionary<Vector3Int,int>();var of=new int[pos.Length];
            for(int i=0;i<pos.Length;i++){var k=Vector3Int.RoundToInt(pos[i]*1e4f);if(!key.TryGetValue(k,out of[i]))key[k]=of[i]=key.Count;}
            var parent=new int[key.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
            int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            for(int t=0;t+2<tri.Length;t+=3){int a=Find(of[tri[t]]);for(int c=1;c<3;c++){int b=Find(of[tri[t+c]]);if(b!=a)parent[b]=a;}}
            var count=new Dictionary<int,int>();var low=new Dictionary<int,float>();
            for(int i=0;i<pos.Length;i++){int r=Find(of[i]);count[r]=count.TryGetValue(r,out int c0)?c0+1:1;low[r]=low.TryGetValue(r,out float l)?Mathf.Min(l,pos[i].y):pos[i].y;}
            bool Sprig(int i){int r=Find(of[i]);return low[r]>top-NativeHotDog.SprigDepth&&count[r]<NativeHotDog.SprigMost;}
            var kept=new List<int>(tri.Length);for(int t=0;t+2<tri.Length;t+=3)if(!Sprig(tri[t])){kept.Add(tri[t]);kept.Add(tri[t+1]);kept.Add(tri[t+2]);}
            if(kept.Count<tri.Length){hotDogSprigless=UnityEngine.Object.Instantiate(hotDogMesh);hotDogSprigless.name="Hot Dog (under a hat)";hotDogSprigless.hideFlags=HideFlags.HideAndDontSave;hotDogSprigless.triangles=kept.ToArray();}
            // The crown's skinning: the highest vertex over the head that isn't a sprig and rides only the sausage's twins.
            int best=-1;
            for(int i=0;i<pos.Length;i++)
            {
                if(pos[i].y<crownHead.y+.1f||Sprig(i)||best>=0&&pos[i].y<=pos[best].y)continue;var w=weights[i];
                bool Twin(int b,float x)=>x<=0||b<hotDogBones.Length&&hotDogBones[b].StartsWith("~",StringComparison.Ordinal);
                if(Twin(w.boneIndex0,w.weight0)&&Twin(w.boneIndex1,w.weight1)&&Twin(w.boneIndex2,w.weight2)&&Twin(w.boneIndex3,w.weight3))best=i;
            }
            if(best<0)return;var bw=weights[best];var bones=new List<int>();var ws=new List<float>();
            void Add(int b,float x){if(x>0){bones.Add(b);ws.Add(x);}}
            Add(bw.boneIndex0,bw.weight0);Add(bw.boneIndex1,bw.weight1);Add(bw.boneIndex2,bw.weight2);Add(bw.boneIndex3,bw.weight3);
            float total=0;foreach(var x in ws)total+=x;for(int k=0;k<ws.Count;k++)ws[k]/=total;crownBone=bones.ToArray();crownWeight=ws.ToArray();
        }
        // A dressed body's Hot Dog renderer, kept by its root and its hat anchor (stale entries dropped now and then).
        private static void HotDogKeep(GameObject root,SkinnedMeshRenderer r,Transform[] bones)
        {
            if(hotDogByRoot.Count>256){var gone=new List<GameObject>();foreach(var k in hotDogByRoot.Keys)if(k==null)gone.Add(k);foreach(var k in gone)hotDogByRoot.Remove(k);}
            if(hotDogByHat.Count>256){var gone=new List<Transform>();foreach(var k in hotDogByHat.Keys)if(k==null)gone.Add(k);foreach(var k in gone)hotDogByHat.Remove(k);}
            hotDogByRoot[root]=r;int head=Array.IndexOf(hotDogBones,"~head");var hat=head>=0?bones[head].Find(HotDogHat):null;if(hat!=null)hotDogByHat[hat]=r;
        }
        /// <summary>The frame hat `hat` rides on head bone `head`: in the Hot Dog, the anchor on its head's twin, placed in the
        /// crown's own skinning frame (its bones' blend: the hat stays on the crown however the head turns and as the sausage
        /// jiggles) with the hat's fit (Core NativeHotDog.HatFit); otherwise the head itself (null for null).</summary>
        private static Transform HotDogHatOn(string hat,Transform head)
        {
            if(head==null)return null;var anchor=head.Find(HotDogTwin+"head/"+HotDogHat);if(anchor==null)return head;
            if(crownBone==null||!hotDogByHat.TryGetValue(anchor,out var r)||r==null)return anchor;
            var bones=r.bones;var bind=hotDogMesh.bindposes;var m=new Matrix4x4();
            for(int k=0;k<crownBone.Length;k++)
            {
                int b=crownBone[k];if(b>=bones.Length||bones[b]==null||b>=bind.Length)return anchor;
                var s=bones[b].localToWorldMatrix*bind[b];float w=crownWeight[k];for(int e=0;e<16;e++)m[e]+=s[e]*w;
            }
            NativeHotDog.HatFit(hat,out float scale,out var move);
            var at=m.MultiplyPoint3x4(crownHead+new Vector3(move.X,move.Y,move.Z));var fwd=m.MultiplyVector(Vector3.forward);var up=m.MultiplyVector(Vector3.up);
            if(fwd.sqrMagnitude<1e-8f||up.sqrMagnitude<1e-8f)return anchor;float size=(fwd.magnitude+up.magnitude)*.5f*scale;
            anchor.SetPositionAndRotation(at,Quaternion.LookRotation(fwd,up));var ps=anchor.parent.lossyScale;
            anchor.localScale=new Vector3(size/Mathf.Max(1e-4f,ps.x),size/Mathf.Max(1e-4f,ps.y),size/Mathf.Max(1e-4f,ps.z));
            return anchor;
        }
        /// <summary>A body (`root`) in the Hot Dog drawn without its sprigs under a closed hat `hat` (NativeHotDog.HidesSprigs),
        /// with them otherwise; a mesh put in its place since (first person's forearms, a cut) is left alone.</summary>
        private static void HotDogUnderHat(GameObject root,string hat)
        {
            if(root==null||hotDogSprigless==null||!hotDogByRoot.TryGetValue(root,out var r)||r==null)return;
            var want=NativeHotDog.HidesSprigs(hat)?hotDogSprigless:hotDogMesh;
            if(r.sharedMesh!=want&&(r.sharedMesh==hotDogMesh||r.sharedMesh==hotDogSprigless))r.sharedMesh=want;
        }
        /// <summary>Whether a body being made (`root`) wears the Hot Dog (its renderer is there): no Sheldon hood on it.</summary>
        private static bool HotDogOn(GameObject root)
        {
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name==HotDogRenderer)return true;return false;
        }
    }
}
