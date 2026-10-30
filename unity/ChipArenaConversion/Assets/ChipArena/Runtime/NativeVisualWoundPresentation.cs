using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Open wounds on the model (owner 2026-10-28: "a full skeleton inside the player ... that can be visible during gibs +
    // wounds"). A living pilot's model body opens each of its pilot's open wounds (a puncture, a cavity or a cut; not a bruise or
    // a burn), up to four: a hole through its skin and clothes where the wound's mark meets the model (NativeVisualBodyFx.cginc
    // WoundClip: a capsule round the wound's line, as wide as the wound, along a cut), and behind it a pit of flesh (a mesh on the
    // wound's bone, darker deeper in) with the skeleton inside the body showing where a bone runs through it
    // (NativeVisualSkeletonPresentation.cs). Where the mark meets the model is found once per wound on a worker, by a ray along
    // the mark's normal against the body's skin in its pose then; the hole then rides that bone. A corpse keeps its pilot's holes
    // (a split body's each on the half it lies on). Presentation only: the wound records are the game's.
    public sealed partial class ArenaPresentation
    {
        private const int VisualHoleMax=4;
        // A hole: its wound, the visual bone it rides, in that bone's frame its centre at the skin, the inward axis and the cut's
        // line, its radius, half length and depth; its pit; while it is being found, the ray.
        private sealed class VisualHole{public int Id,Bone;public Vector3 At,In,Along;public float Radius,Half,Depth,Rim;public GameObject Pit;public HoleRay Ray;}
        // Where a wound's mark (`Mark`, world) meets the model: the closest point on the skinned parts as posed, their triangles on
        // the wound's body only (`On`, per part and bone: an arm hanging over the flank is not the flank), and that face's outward
        // normal (`Entry`, `Out`; `Found` false for none within 12 cm); and how far a line from there straight in runs inside
        // the body (`Through`, from the far faces it meets).
        private sealed class HoleRay
        {
            public CutPart[] Parts;public bool[][] On;public Vector3 Mark,Normal,Entry,Out;public float Through;public bool Found;public System.Threading.Tasks.Task Task;
            public bool Done=>Task==null||Task.IsCompleted;
            public void Run()
            {
                try
                {
                    var posed=new Vector3[Parts.Length][];float best=.12f*.12f;
                    for(int k=0;k<Parts.Length;k++)
                    {
                        var p=Parts[k];var on=On[k];var w=p.Mesh.Weights;var at=posed[k]=CutJob.Posed(p);var tris=p.Mesh.Tris;
                        for(int t=0;t+2<tris.Length;t+=3)
                        {
                            if(!on[w[tris[t]].boneIndex0]&&!on[w[tris[t+1]].boneIndex0]&&!on[w[tris[t+2]].boneIndex0])continue;
                            Vector3 a=at[tris[t]],b=at[tris[t+1]],c=at[tris[t+2]];var q=Closest(Mark,a,b,c);float d=(q-Mark).sqrMagnitude;if(d>=best)continue;
                            var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-14f)continue;best=d;Entry=q;Out=n.normalized;Found=true;
                        }
                    }
                    if(!Found)return;
                    // Out of the body (the mark's own normal says which way: a cloth's inner face turns in), and out onto the outermost
                    // layer over the skin there (a sleeve, a jacket) within 6 cm, so the hole opens through it.
                    if(Vector3.Dot(Out,Normal)<0)Out=-Out;float outer=Cast(posed,Entry+Out*.0005f,Out,.06f,true);if(outer>0)Entry+=Out*(outer+.0005f);
                    // Straight in from the entry: the farthest face it meets (the body's far side) within 40 cm.
                    Through=Cast(posed,Entry-Out*.0005f,-Out,.4f,true);
                }
                catch(Exception){Found=false;}
            }
            // The farthest (or nearest) face a ray meets within `reach` (-1 for none), over the wound's body.
            private float Cast(Vector3[][] posed,Vector3 from,Vector3 dir,float reach,bool farthest)
            {
                float hit=-1;
                for(int k=0;k<Parts.Length;k++)
                {
                    var p=Parts[k];var on=On[k];var w=p.Mesh.Weights;var at=posed[k];var tris=p.Mesh.Tris;
                    for(int t=0;t+2<tris.Length;t+=3)
                    {
                        if(!on[w[tris[t]].boneIndex0]&&!on[w[tris[t+1]].boneIndex0]&&!on[w[tris[t+2]].boneIndex0])continue;
                        Vector3 a=at[tris[t]],b=at[tris[t+1]],c=at[tris[t+2]];
                        var ab=b-a;var ac=c-a;var h=Vector3.Cross(dir,ac);float det=Vector3.Dot(ab,h);if(Mathf.Abs(det)<1e-12f)continue;float inv=1/det;
                        var s=from-a;float u=Vector3.Dot(s,h)*inv;if(u<0||u>1)continue;var r=Vector3.Cross(s,ab);float v=Vector3.Dot(dir,r)*inv;if(v<0||u+v>1)continue;
                        float d=Vector3.Dot(ac,r)*inv;if(d<=.0005f||d>reach)continue;if(hit<0||(farthest?d>hit:d<hit))hit=d;
                    }
                }
                return hit;
            }
            // The point of triangle (a, b, c) closest to `p` (Ericson, Real-Time Collision Detection 5.1.5).
            private static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
            {
                var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
                var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
                float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
                var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
                float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
                float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
                float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
            }
        }
        private static readonly int WoundsId=Shader.PropertyToID("_Wounds"),WoundAtId=Shader.PropertyToID("_WoundAt"),WoundInId=Shader.PropertyToID("_WoundIn"),WoundAlongId=Shader.PropertyToID("_WoundAlong");
        private readonly Vector4[] holeAt=new Vector4[VisualHoleMax],holeIn=new Vector4[VisualHoleMax],holeAlong=new Vector4[VisualHoleMax];private MaterialPropertyBlock holeBlock;
        private static bool Opens(NativeWound w)=>w!=null&&!w.destroyed&&(w.kind=="puncture"||w.kind=="cavity"||w.kind=="cut");

        // VisualBodyLook, each frame a living pilot's model body draws (posed): its open wounds' holes found, placed and cut, and
        // its skeleton on while any shows.
        private void VisualWounds(VisualBody body,CapturedAnimation.Player rig,Actor a)
        {
            var wounds=a.Wounds;if(body.Holes==null&&(wounds==null||wounds.Length==0))return;body.Holes??=new List<VisualHole>();
            // Gone wounds (a respawn, a revive) take their holes with them.
            for(int i=body.Holes.Count-1;i>=0;i--){var h=body.Holes[i];bool kept=false;if(wounds!=null)foreach(var w in wounds)if(w.id==h.Id&&Opens(w)){kept=true;break;}if(!kept){if(h.Pit!=null)Destroy(h.Pit);body.Holes.RemoveAt(i);}}
            if(wounds!=null)foreach(var w in wounds)
            {
                if(body.Holes.Count>=VisualHoleMax)break;if(!Opens(w))continue;bool known=false;foreach(var h in body.Holes)if(h.Id==w.id){known=true;break;}if(known)continue;
                var hole=VisualHoleStart(body,rig,w);if(hole!=null)body.Holes.Add(hole);
            }
            foreach(var h in body.Holes)if(h.Ray!=null&&h.Ray.Done)VisualHolePlace(body.All,body.Binding.Bones,h);
            int shown=VisualHolesCut(body.All,body.Binding.Bones,body.Holes,null);
            if(body.Skeleton!=null&&body.Skeleton.enabled!=(shown>0))body.Skeleton.enabled=shown>0;
        }
        // A new wound's hole: where its mark is on the model now (VisualJointFrame), the ray along its normal started on a worker.
        private VisualHole VisualHoleStart(VisualBody body,CapturedAnimation.Player rig,NativeWound w)
        {
            var mark=sourceModels?.WoundMark(w);if(!mark.HasValue||body.JointBone==null)return new VisualHole{Id=w.id,Bone=-1};
            var m=mark.Value;if(m.bone<0||m.bone>=body.JointBone.Length||body.JointBone[m.bone]<0)return new VisualHole{Id=w.id,Bone=-1};
            var frame=VisualJointFrame(rig,m.bone);var p=frame.MultiplyPoint3x4(m.point);var n=frame.MultiplyVector(m.normal).normalized;var along=frame.MultiplyVector(m.direction);
            along-=n*Vector3.Dot(along,n);if(along.sqrMagnitude<1e-8f)along=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right);along.Normalize();
            var index=new Dictionary<Transform,int>();var bones=body.Binding.Bones;for(int v=0;v<bones.Length;v++)if(bones[v]!=null)index[bones[v]]=v;
            var parts=new List<CutPart>();var on=new List<bool[]>();int count=0;var joints=VisualJoints(body.Binding);int region=NativeBisect.Anchor(m.bone);
            foreach(var r in body.All)if(r is SkinnedMeshRenderer skin&&skin.enabled&&skin.gameObject.activeInHierarchy)
            {
                var part=VisualCutPart(skin,0,count,index,null,0,null);if(part==null)continue;parts.Add(part);count+=part.Mesh.At.Length;
                var flags=new bool[part.Visual.Length];for(int i=0;i<flags.Length;i++)flags[i]=part.Visual[i]>=0&&part.Visual[i]<joints.Length&&NativeBisect.Anchor(joints[part.Visual[i]])==region;on.Add(flags);
            }
            int bone=body.JointBone[m.bone];var into=bones[bone].worldToLocalMatrix;
            float radius=w.kind=="cut"?.0075f:w.kind=="cavity"?Mathf.Clamp(w.radius,.016f,.028f):Mathf.Clamp(w.radius*.8f,.008f,.015f);
            float half=w.kind=="cut"?Mathf.Clamp(w.radius*1.5f,.015f,.032f):0,depth=w.kind=="cut"?.022f:w.kind=="cavity"?.045f:.036f;
            var hole=new VisualHole{Id=w.id,Bone=bone,At=into.MultiplyPoint3x4(p),In=into.MultiplyVector(-n).normalized,Along=into.MultiplyVector(along).normalized,Radius=radius,Half=half,Depth=depth};
            var ray=new HoleRay{Parts=parts.ToArray(),On=on.ToArray(),Mark=p,Normal=n};hole.Ray=ray;
            if(Application.platform!=RuntimePlatform.WebGLPlayer)try{ray.Task=System.Threading.Tasks.Task.Run(ray.Run);}catch(Exception){ray.Task=null;}
            if(ray.Task==null)ray.Run();return hole;
        }
        // A found hole: on the skin where the mark meets it (none near: the hole is dropped), straight in along that face's normal
        // (the cut's line laid onto it), its depth kept inside the body, its pit made on its bone.
        private void VisualHolePlace(Renderer[] all,Transform[] bones,VisualHole h)
        {
            var ray=h.Ray;h.Ray=null;if(!ray.Found||h.Bone<0||h.Bone>=bones.Length||bones[h.Bone]==null){h.Bone=-1;return;}
            var bone=bones[h.Bone];var into=bone.worldToLocalMatrix;h.At=into.MultiplyPoint3x4(ray.Entry);h.In=into.MultiplyVector(-ray.Out).normalized;
            var along=h.Along-h.In*Vector3.Dot(h.Along,h.In);h.Along=along.sqrMagnitude>1e-8f?along.normalized:Vector3.Cross(h.In,Vector3.up).normalized;
            // Down to the bone: past the axis of the bone it rides (to its farthest child) when that is near, so the bone shows.
            // A limb's: just past its bone (the nearest point of the axis to its children), aimed at it when that is within 20
            // degrees of straight in (a steeper pit's rim would stand out of the skin), so the bone shows in the pit.
            float through=ray.Through>.005f?ray.Through:.08f,toBone=-1;var index=new HashSet<Transform>(bones);var onBone=Vector3.zero;
            foreach(Transform child in bone){if(!index.Contains(child))continue;var ab=child.position-bone.position;float l=ab.sqrMagnitude;if(l<1e-6f)continue;float t=Mathf.Clamp01(Vector3.Dot(ray.Entry-bone.position,ab)/l);var q=bone.position+ab*t;float d=(q-ray.Entry).magnitude;if(toBone<0||d<toBone){toBone=d;onBone=q;}}
            if(toBone>.01f&&toBone<.09f){if(Vector3.Dot(onBone-ray.Entry,-ray.Out)>.94f*toBone)h.In=into.MultiplyVector(onBone-ray.Entry).normalized;h.Depth=Mathf.Max(h.Depth,toBone+.006f);}
            h.Depth=Mathf.Min(h.Depth,through*.62f);
            // The pit's rim sunk as far as the limb's skin curves away across the hole, so it never stands out of a thin limb.
            float limb=Mathf.Clamp(through*.5f,.03f,.2f),rr=h.Radius*1.08f;h.Rim=Mathf.Min(rr*rr/(2*limb),h.Depth*.4f);
            h.Pit=VisualHolePit(all,bone,h);
        }
        // The pit: an open tube round the hole's capsule (a little wider) from the skin to its depth, closed at the bottom, flesh
        // darkening inward, in the cut's material (both sides; on the hole's bone).
        private GameObject VisualHolePit(Renderer[] all,Transform bone,VisualHole h)
        {
            var look=VisualCutCapMaterial(all);if(look==null)return null;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            var side=Vector3.Cross(h.In,h.Along).normalized;const int sides=14;var at=new List<Vector3>();var nm=new List<Vector3>();var col=new List<Color>();var tris=new List<int>();
            Color C(Color c,float glow){var o=linear?c:c.gamma;o.a=glow;return o;}
            var rimC=C(new Color(.42f,.06f,.07f),.5f);var midC=C(FleshOuter,.6f);var deepC=C(FleshDeep,.5f);float r=h.Radius*1.08f;
            Vector3 Ring(int k,float depth,out Vector3 normal)
            {
                float a=k*2*Mathf.PI/sides,ca=Mathf.Cos(a),sa=Mathf.Sin(a);var round=h.Along*ca+side*sa;var o=round*r+h.Along*(Mathf.Sign(ca)*h.Half);normal=-round;return h.At+h.In*depth+o;
            }
            float[] depths={h.Rim-.001f,(h.Rim+h.Depth)*.5f,h.Depth};Color[] cols={rimC,midC,deepC};
            for(int ring=0;ring<3;ring++)for(int k=0;k<sides;k++){at.Add(Ring(k,depths[ring],out var n));nm.Add(n);col.Add(cols[ring]);}
            for(int ring=0;ring<2;ring++)for(int k=0;k<sides;k++){int a=ring*sides+k,b=ring*sides+(k+1)%sides,c=a+sides,d=b+sides;tris.AddRange(new[]{a,c,b,b,c,d});}
            int bottom=at.Count;at.Add(h.At+h.In*h.Depth);nm.Add(-h.In);col.Add(C(FleshDeep*.7f,.4f));
            for(int k=0;k<sides;k++)tris.AddRange(new[]{bottom,2*sides+k,2*sides+(k+1)%sides});
            var mesh=new Mesh{name="Visual wound pit",hideFlags=HideFlags.HideAndDontSave};mesh.SetVertices(at);mesh.SetNormals(nm);mesh.SetColors(col);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();
            var go=new GameObject("Visual wound");go.transform.SetParent(bone,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=look;mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        // Each frame: the placed holes (world) on every renderer of `all` but the skeleton's and the caps' (the shader cuts them);
        // the number shown. Only holes whose half is `only` (a split corpse's), when given.
        private int VisualHolesCut(Renderer[] all,Transform[] bones,List<VisualHole> holes,Func<VisualHole,bool> only)
        {
            int n=0;
            if(holes!=null)foreach(var h in holes)
            {
                if(n>=VisualHoleMax||h.Ray!=null||h.Bone<0||h.Bone>=bones.Length||bones[h.Bone]==null||h.Pit==null||only!=null&&!only(h))continue;
                var m=bones[h.Bone].localToWorldMatrix;var at=m.MultiplyPoint3x4(h.At);var dir=m.MultiplyVector(h.In).normalized;var along=m.MultiplyVector(h.Along).normalized;
                holeAt[n]=new Vector4(at.x,at.y,at.z,h.Radius);holeIn[n]=new Vector4(dir.x,dir.y,dir.z,h.Depth);holeAlong[n]=new Vector4(along.x,along.y,along.z,h.Half);n++;
            }
            holeBlock??=new MaterialPropertyBlock();
            foreach(var r in all)
            {
                if(!(r is SkinnedMeshRenderer)||r.name==SkeletonRenderer)continue;
                r.GetPropertyBlock(holeBlock);if(n==0&&holeBlock.GetFloat(WoundsId)<.5f)continue;
                holeBlock.SetFloat(WoundsId,n);holeBlock.SetVectorArray(WoundAtId,holeAt);holeBlock.SetVectorArray(WoundInId,holeIn);holeBlock.SetVectorArray(WoundAlongId,holeAlong);r.SetPropertyBlock(holeBlock);
            }
            return n;
        }
        // VisualCorpseMake: a corpse's holes, its pilot's live body's placed ones (each on the same bone of the corpse's instance),
        // their pits made again and added to its renderers (they dissolve with it); `keep` picks those that stay on this body (a
        // split body's half, by where the hole lies).
        private List<VisualHole> VisualCorpseHoles(CapturedAnimation.Player rig,VisualCorpse c,Func<VisualHole,Vector3,bool> keep)
        {
            if(rig==null||!visualBodies.TryGetValue(rig,out var live)||live.Holes==null||c.Bones==null)return null;List<VisualHole> list=null;
            foreach(var h in live.Holes)
            {
                if(h.Ray!=null||h.Bone<0||h.Bone>=c.Bones.Length||c.Bones[h.Bone]==null||h.Pit==null)continue;var world=c.Bones[h.Bone].localToWorldMatrix.MultiplyPoint3x4(h.At);
                if(keep!=null&&!keep(h,world))continue;
                var copy=new VisualHole{Id=h.Id,Bone=h.Bone,At=h.At,In=h.In,Along=h.Along,Radius=h.Radius,Half=h.Half,Depth=h.Depth,Rim=h.Rim};copy.Pit=VisualHolePit(c.All,c.Bones[h.Bone],copy);
                if(copy.Pit!=null)(list??=new List<VisualHole>()).Add(copy);
            }
            if(list!=null){var pits=new List<Renderer>(c.All);foreach(var h in list)pits.Add(h.Pit.GetComponent<Renderer>());c.All=pits.ToArray();}
            return list;
        }
    }
}
