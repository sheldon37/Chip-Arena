using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The parry's holographic barrier on the body (owner 2026-11-09: "Lets cover the users player model in a holographic barrier effect
    // when the parry is active"; the parry on F, "just with different effects now"): while a pilot's replicated parry window is open
    // (Actor.BlockAt .. BlockUntil) each of its visual body's skinned pieces is drawn again in Resources/NativeParryHolo.shader, on the
    // same mesh and bones (so it moves, bends and wears blend shapes exactly as the body does), in the pilot's accent: it comes up with a
    // sweep from the feet, holds, flashes and ripples where a parried blow lands (the Block events, NativeBlockFxPresentation.cs), and
    // fades out as the window closes. The copies are made once per body and only drawn while it shows. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private sealed class ParryHolo{public SkinnedMeshRenderer[] From,Copies;public MaterialPropertyBlock Block;public bool On;}
        private readonly Dictionary<GameObject,ParryHolo> parryHolos=new Dictionary<GameObject,ParryHolo>();
        private static Material parryHoloMaterial;private static bool parryHoloFailed;
        private static readonly int HoloColorId=Shader.PropertyToID("_HoloColor"),HoloFadeId=Shader.PropertyToID("_HoloFade"),HoloAgeId=Shader.PropertyToID("_HoloAge"),
            HoloFlashId=Shader.PropertyToID("_HoloFlash"),HoloHitId=Shader.PropertyToID("_HoloHit"),HoloSpanId=Shader.PropertyToID("_HoloSpan");
        /// <summary>The barrier's strength at `time` for a window opened at `at` and closing at `until`: up over 0.06 s, out over 0.15 s
        /// after it closes, 0 outside.</summary>
        public static float ParryHoloFade(float at,float until,float time)
        {
            if(until<=at||time<at||time>until+.15f)return 0;return Mathf.Clamp01((time-at)/.06f)*Mathf.Clamp01((until+.15f-time)/.15f);
        }
        private static Material ParryHoloMaterial()
        {
            if(parryHoloMaterial!=null||parryHoloFailed)return parryHoloMaterial;var shader=Resources.Load<Shader>("NativeParryHolo");
            if(shader==null||!shader.isSupported){parryHoloFailed=true;return null;}
            return parryHoloMaterial=new Material(shader){name="Parry hologram",hideFlags=HideFlags.HideAndDontSave};
        }
        /// <summary>VisualBodyPose, third person: the barrier over the body while the pilot's parry is open (off otherwise).</summary>
        private void VisualParryHolo(VisualBody body,Actor a,float time)
        {
            if(body?.Root==null||a==null)return;float fade=ParryHoloFade(a.BlockAt,a.BlockUntil,time);
            if(!parryHolos.TryGetValue(body.Root,out var h)){if(fade<=0)return;h=MakeParryHolo(body);parryHolos[body.Root]=h;if(h==null)return;}
            if(h==null)return;
            if(fade<=0){if(h.On){foreach(var c in h.Copies)if(c!=null)c.enabled=false;h.On=false;}return;}
            // A parried blow: the latest Block event this pilot made, its age and where it landed.
            float flash=-1;Vector3 hit=Vector3.zero;foreach(var r in blockRecords)if(r.Blocker==a.Id&&r.At<=time&&r.At>=a.BlockAt-.05f&&(flash<0||time-r.At<flash)){flash=time-r.At;hit=r.Point;}
            var accent=AccentTint(a.AccentColor);var colour=new Color(accent.r*.55f+.12f,accent.g*.55f+.42f,accent.b*.55f+.55f)*2.2f;
            float feet=body.Root.transform.position.y;h.Block.SetColor(HoloColorId,colour.linear);h.Block.SetFloat(HoloFadeId,fade);h.Block.SetFloat(HoloAgeId,time-a.BlockAt);
            h.Block.SetFloat(HoloFlashId,flash);h.Block.SetVector(HoloHitId,hit);h.Block.SetVector(HoloSpanId,new Vector4(feet,feet+1.9f,0,0));
            for(int i=0;i<h.Copies.Length;i++)
            {
                var c=h.Copies[i];var f=h.From[i];if(c==null||f==null)continue;bool shows=f.enabled&&f.gameObject.activeInHierarchy&&f.sharedMesh!=null;c.enabled=shows;if(!shows)continue;
                if(c.sharedMesh!=f.sharedMesh)c.sharedMesh=f.sharedMesh;int shapes=f.sharedMesh.blendShapeCount;for(int k=0;k<shapes;k++)c.SetBlendShapeWeight(k,f.GetBlendShapeWeight(k));
                c.SetPropertyBlock(h.Block);
            }
            h.On=true;
        }
        private ParryHolo MakeParryHolo(VisualBody body)
        {
            var mat=ParryHoloMaterial();if(mat==null||body.All==null)return null;var from=new List<SkinnedMeshRenderer>();var copies=new List<SkinnedMeshRenderer>();
            foreach(var r in body.All)
            {
                if(!(r is SkinnedMeshRenderer s)||s.sharedMesh==null)continue;
                var go=new GameObject("Parry hologram");go.layer=s.gameObject.layer;go.transform.SetParent(s.transform.parent,false);go.transform.localPosition=s.transform.localPosition;go.transform.localRotation=s.transform.localRotation;go.transform.localScale=s.transform.localScale;
                var c=go.AddComponent<SkinnedMeshRenderer>();c.sharedMesh=s.sharedMesh;c.bones=s.bones;c.rootBone=s.rootBone;c.localBounds=s.localBounds;c.updateWhenOffscreen=s.updateWhenOffscreen;
                var mats=new Material[Mathf.Max(1,s.sharedMesh.subMeshCount)];for(int k=0;k<mats.Length;k++)mats[k]=mat;c.sharedMaterials=mats;
                c.shadowCastingMode=ShadowCastingMode.Off;c.receiveShadows=false;c.lightProbeUsage=LightProbeUsage.Off;c.reflectionProbeUsage=ReflectionProbeUsage.Off;c.enabled=false;
                from.Add(s);copies.Add(c);
            }
            return new ParryHolo{From=from.ToArray(),Copies=copies.ToArray(),Block=new MaterialPropertyBlock()};
        }
    }
}
