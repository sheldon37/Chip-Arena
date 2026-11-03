using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The zombies' bespoke garments (owner 2026-11-03: "May have to create the outfits, kimono doesnt work for those well", then
    // "Utilize magica cloth for the farbric on the clothes"; Core NativeZombieLooks Variant.Garments; docs/ZOMBIE-MODELS.md 4l):
    // the hospital gown, the lab coat and the scrubs, made by tools/gear/garments.py on the pack body's bind pose into the local
    // bank Resources/ChipArenaGarments (made from the pack's body, so kept local like it; without it the pack pieces stay).
    // - Each garment is a skinned mesh on the model's own bones (their bind poses the body's, read once off its mesh), with the
    //   body's nine shapes (ZombieShapes sets them as on every worn mesh, so it follows the zombie's build), drawn with the zone
    //   gear's toon material in the variant's colour and wear (its vertex colour a middle grey the tint makes that colour).
    // - It takes its slot from the pack (ZombieDress leaves that slot's piece off, so the skin under it shows and is the zombie's).
    // - Its loose fabric is Magica Cloth (the local install's bridge, NativeVisualCloth: a paint map per garment, pinned above the
    //   height its fabric hangs free from and free below it), on the nearest few zombies only (a crowd's every hem would cost
    //   too much): attached once per body when it first comes near, switched off beyond ZombieClothNear or past ZombieClothMost.
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        public const string ZombieGarmentBank="ChipArenaGarments";
        /// <summary>The cloth on zombies: within this distance (m) and at most this many at once.</summary>
        public const float ZombieClothNear=8f;public const int ZombieClothMost=4;
        private sealed class ZombieGarment{public string Id;public int Slot;public float FreeV;public Mesh Mesh;public string[] Bones;public bool Bound,Failed;public Texture2D Paint;}
        private static Dictionary<string,ZombieGarment> zombieGarments;
        private static Dictionary<string,ZombieGarment> ZombieGarmentBankRead()
        {
            if(zombieGarments!=null)return zombieGarments;zombieGarments=new Dictionary<string,ZombieGarment>(StringComparer.Ordinal);
            var asset=Resources.Load<TextAsset>(ZombieGarmentBank);if(asset==null)return zombieGarments;
            try
            {
                var b=asset.bytes;int at=0;
                int I(){int v=BitConverter.ToInt32(b,at);at+=4;return v;}float Fl(){float v=BitConverter.ToSingle(b,at);at+=4;return v;}
                string S(){int n=BitConverter.ToUInt16(b,at);at+=2;var t=System.Text.Encoding.UTF8.GetString(b,at,n);at+=n;return t;}
                if(b.Length<12||b[0]!='G'||b[1]!='A'||b[2]!='R'||b[3]!='M'){Debug.LogWarning("Zombie garments: the bank is not one; the pack's pieces stay.");return zombieGarments;}
                at=4;if(I()!=1){Debug.LogWarning("Zombie garments: an unknown bank version; the pack's pieces stay.");return zombieGarments;}
                var names=new string[I()];for(int i=0;i<names.Length;i++)names[i]=S();
                int count=I();bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
                for(int k=0;k<count;k++)
                {
                    var g=new ZombieGarment{Id=S(),Slot=b[at++]};g.FreeV=Fl();int n=I();
                    var pos=new Vector3[n];var nor=new Vector3[n];var uv=new Vector2[n];var col=new Color32[n];
                    for(int i=0;i<n;i++)pos[i]=new Vector3(Fl(),Fl(),Fl());for(int i=0;i<n;i++)nor[i]=new Vector3(Fl(),Fl(),Fl());for(int i=0;i<n;i++)uv[i]=new Vector2(Fl(),Fl());
                    for(int i=0;i<n;i++){col[i]=new Color32(b[at],b[at+1],b[at+2],b[at+3]);at+=4;}
                    var idx=new int[n*4];for(int i=0;i<idx.Length;i++){idx[i]=BitConverter.ToUInt16(b,at);at+=2;}
                    var wt=new float[n*4];for(int i=0;i<wt.Length;i++)wt[i]=Fl();
                    int tris=I();var tri=new int[tris*3];for(int i=0;i<tri.Length;i++)tri[i]=I();
                    // (Its own bones: those its weights name, in the order first met.)
                    var local=new Dictionary<int,int>();var bones=new List<string>();var bw=new BoneWeight[n];
                    for(int i=0;i<n;i++)
                    {
                        int L(int j){if(wt[i*4+j]<=0)return 0;int gi=idx[i*4+j];if(!local.TryGetValue(gi,out int li)){li=bones.Count;local[gi]=li;bones.Add(names[gi]);}return li;}
                        bw[i]=new BoneWeight{boneIndex0=L(0),weight0=wt[i*4],boneIndex1=L(1),weight1=wt[i*4+1],boneIndex2=L(2),weight2=wt[i*4+2],boneIndex3=L(3),weight3=wt[i*4+3]};
                    }
                    var cs=new Color[n];for(int i=0;i<n;i++){Color c=col[i];cs[i]=linear?new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),c.a):c;}
                    var mesh=new Mesh{name="Zombie garment "+g.Id,hideFlags=HideFlags.HideAndDontSave};if(n>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices=pos;mesh.normals=nor;mesh.uv=uv;mesh.colors=cs;mesh.boneWeights=bw;mesh.triangles=tri;
                    int shapes=I();var none=new Vector3[n];
                    for(int s=0;s<shapes;s++){string sn=S();var d=new Vector3[n];for(int i=0;i<n;i++)d[i]=new Vector3(Fl(),Fl(),Fl());mesh.AddBlendShapeFrame("Garment.Shape_"+sn,100,d,none,none);}
                    mesh.RecalculateBounds();g.Mesh=mesh;g.Bones=bones.ToArray();zombieGarments[g.Id]=g;
                }
            }
            catch(Exception e){Debug.LogWarning("Zombie garments: the bank is unreadable ("+e.Message+"); the pack's pieces stay.");zombieGarments.Clear();}
            return zombieGarments;
        }
        /// <summary>The slots (a bit each) a variant's garments take from the pack: those the bank has.</summary>
        private static int ZombieGarmentSlots(NativeZombieLooks.Variant v)
        {
            if(v.Garments.Length==0)return 0;var bank=ZombieGarmentBankRead();int slots=0;
            foreach(var id in v.Garments)if(bank.TryGetValue(id,out var g)&&!g.Failed)slots|=1<<g.Slot;
            return slots;
        }
        // A garment's bind poses: the body's own for the bones it names (one bind for every body made from the bake).
        private static bool ZombieGarmentBind(ZombieGarment g,SkinnedMeshRenderer body)
        {
            if(g.Bound)return true;if(g.Failed)return false;var bones=body.bones;var bind=body.sharedMesh.bindposes;var pose=new Matrix4x4[g.Bones.Length];
            for(int i=0;i<g.Bones.Length;i++)
            {
                int at=Array.FindIndex(bones,t=>t!=null&&t.name==g.Bones[i]);
                if(at<0||at>=bind.Length){g.Failed=true;Debug.LogWarning("Zombie garments: "+g.Id+" names a bone the body lacks ("+g.Bones[i]+"); the pack's piece stays.");return false;}
                pose[i]=bind[at];
            }
            g.Mesh.bindposes=pose;g.Bound=true;return true;
        }
        /// <summary>ZombieModelMake, after ZombieDress: the variant's garments on the model (their shapes, colour and wear the
        /// zombie's); none without the bank.</summary>
        private static void ZombieGarmentsMake(GameObject root,NativeZombieLooks.Variant v)
        {
            if(v.Garments.Length==0)return;var bank=ZombieGarmentBankRead();if(bank.Count==0)return;var mats=ZombieGearMaterials();if(mats==null)return;
            SkinnedMeshRenderer body=null;foreach(var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(s.name.StartsWith("Body.",StringComparison.Ordinal)&&s.sharedMesh!=null){body=s;break;}if(body==null)return;
            var named=new Dictionary<string,Transform>(StringComparer.Ordinal);foreach(var t in body.bones)if(t!=null)named[t.name]=t;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var skin=WearColor(v.Skin);if(linear)skin=skin.linear;float seed=NativeHordeKinds.Hash(NativeGoreRules.Fnv(v.Id),5)*997+1;
            // (Its vertex colour's brightness in the shader's colour space: the tint takes each pixel against it.)
            float main=linear?Mathf.GammaToLinearSpace(.5f):.5f;
            foreach(var id in v.Garments)
            {
                if(!bank.TryGetValue(id,out var g)||!ZombieGarmentBind(g,body))continue;
                var bones=new Transform[g.Bones.Length];bool ok=true;for(int i=0;i<bones.Length;i++)if(!named.TryGetValue(g.Bones[i],out bones[i])){ok=false;break;}if(!ok)continue;
                var go=new GameObject("Garment."+id){layer=body.gameObject.layer};var t=go.transform;t.SetParent(body.transform.parent,false);
                t.localPosition=body.transform.localPosition;t.localRotation=body.transform.localRotation;t.localScale=body.transform.localScale;
                var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=g.Mesh;r.bones=bones;r.rootBone=body.rootBone;r.localBounds=body.localBounds;r.sharedMaterial=mats[0];r.updateWhenOffscreen=false;
                ZombieShapes(r,v.Shapes);
                int slot=Mathf.Clamp(g.Slot,0,v.Colours.Length-1);if(v.Colours[slot]>=0)ZombieTintOn(r,v.Colours[slot],linear,1,main);
                ZombieWearOn(r,v.Grime,v.Tears,0,seed,skin,v.Blood);
            }
        }
        // ---- the cloth (the local Magica bridge) on the nearest few ----
        private int zombieClothFrame=-1,zombieClothOn;
        private static Texture2D ZombieGarmentPaint(ZombieGarment g)
        {
            if(g.Paint!=null||g.FreeV<0)return g.Paint;const int H=64;var tex=new Texture2D(4,H,TextureFormat.RGBA32,false,true){name="Garment paint "+g.Id,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var px=new Color32[4*H];
            for(int y=0;y<H;y++){float v=y/(float)(H-1),free=Mathf.SmoothStep(0,1,(v-g.FreeV+.04f)/.12f);var c=new Color32((byte)(255*(1-free)),(byte)(255*free),0,255);for(int x=0;x<4;x++)px[y*4+x]=c;}
            tex.SetPixels32(px);tex.Apply(false,false);g.Paint=tex;return tex;
        }
        /// <summary>ZombieModelBind: the body's garments' cloth on while it is near (and among the first few this frame), off
        /// beyond; attached the first time it comes near.</summary>
        private void ZombieGarmentCloth(ZombieBody z,float distanceSq)
        {
            var v=z.Variant;if(v.Garments.Length==0||!NativeVisualCloth.Registered||LowGraphics)return;
            if(zombieClothFrame!=Time.frameCount){zombieClothFrame=Time.frameCount;zombieClothOn=0;}
            bool near=!z.Dying&&distanceSq<ZombieClothNear*ZombieClothNear&&zombieClothOn<ZombieClothMost;
            if(near&&z.Cloth==null&&!z.ClothTried)
            {
                z.ClothTried=true;var bank=ZombieGarmentBankRead();bool any=false;
                foreach(var id in v.Garments)if(bank.TryGetValue(id,out var g)&&g.Bound&&ZombieGarmentPaint(g) is Texture2D paint){NativeVisualCloth.PaintMaps["Garment."+id]=paint;any=true;}
                if(any)try{z.Cloth=NativeVisualCloth.Attach(z.Body.Root,z.Body.Root.transform.Find("VisualSkeleton"));}catch(Exception e){NativeVisualCloth.Attach=null;Debug.LogWarning("Zombie garments: the cloth failed ("+e.Message+"); off for the session.");}
            }
            if(z.Cloth==null)return;
            if(near){zombieClothOn++;if(!z.ClothOn){NativeVisualCloth.Reset(z.Cloth);NativeVisualCloth.Enable(z.Cloth,true);z.ClothOn=true;}}
            else if(z.ClothOn){NativeVisualCloth.Enable(z.Cloth,false);z.ClothOn=false;}
        }
    }
}
