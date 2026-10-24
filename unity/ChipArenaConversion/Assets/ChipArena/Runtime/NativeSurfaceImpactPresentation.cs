using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Surface impacts (Core NativeSurfaceImpacts): a gun's plain round that hits a prop (a replicated Impact event with its
    // surface) plays the bake's effect for the surface's kind there, out along the surface normal, and leaves its bullet hole,
    // which fades. The bake (the visual body's prefab, VisualImpacts: per kind an Effect of particle systems on the engine's
    // own particle shaders and a Decal quad) is read once; without it nothing draws. Pooled: effects per kind and a ring of
    // holes, no allocation once warm. The low graphics tier draws the holes only; reduced effects play fewer at once.
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        private sealed class LiveImpact { public GameObject Go; public ParticleSystem[] Systems; public float Until=float.NegativeInfinity; }
        private sealed class LiveDecal { public GameObject Go; public Renderer Renderer; public int Kind; public float Born=float.NegativeInfinity, Alpha=-1; }
        private GameObject[] surfaceEffects, surfaceDecals; private bool surfaceBankRead; private Transform surfaceRoot;
        private readonly List<LiveImpact>[] surfaceLive={new List<LiveImpact>(),new List<LiveImpact>(),new List<LiveImpact>()};
        private LiveDecal[] surfaceHoles; private int surfaceNextHole; private MaterialPropertyBlock surfaceHoleBlock;
        private NativeArena surfaceArena; private int surfaceGeneration=-1, surfaceSeen;
        private readonly Dictionary<string,string> surfaceMaterials=new Dictionary<string,string>();
        private static readonly int SurfaceTintId=Shader.PropertyToID("_TintColor");
        private static readonly Color SurfaceHoleTint=new Color(.5f,.5f,.5f,.5f);

        // The bake's effects and holes by kind (read once; null without a bake that carries them).
        private bool SurfaceBank()
        {
            if(surfaceBankRead)return surfaceEffects!=null;surfaceBankRead=true;
            var prefab=Resources.Load<GameObject>(VisualBodyResource);var holder=prefab!=null?prefab.transform.Find("VisualImpacts"):null;if(holder==null)return false;
            var effects=new GameObject[NativeSurfaceImpacts.Kinds];var decals=new GameObject[NativeSurfaceImpacts.Kinds];
            for(int k=0;k<effects.Length;k++){var entry=holder.Find(NativeSurfaceImpacts.Names[k]);if(entry==null)return false;effects[k]=entry.Find("Effect")?.gameObject;decals[k]=entry.Find("Decal")?.gameObject;}
            surfaceEffects=effects;surfaceDecals=decals;
            surfaceRoot=new GameObject("Surface impacts").transform;surfaceRoot.SetParent(transform,false);
            surfaceHoles=new LiveDecal[NativeSurfaceImpacts.Decals];surfaceHoleBlock=new MaterialPropertyBlock();
            return true;
        }

        // WeaponFx, each frame: new impacts played, finished ones put away, holes faded.
        private void SurfaceImpacts(NativeArena arena,float now)
        {
            if(!SurfaceBank())return;
            if(!ReferenceEquals(surfaceArena,arena)||surfaceGeneration!=arena.Generation)
            {
                ClearSurfaceImpacts();surfaceArena=arena;surfaceGeneration=arena.Generation;surfaceSeen=0;surfaceMaterials.Clear();
                foreach(var e in arena.Events)if(e.Id>surfaceSeen)surfaceSeen=e.Id;
            }
            for(int k=0;k<surfaceLive.Length;k++)foreach(var live in surfaceLive[k])if(live.Go.activeSelf&&now>=live.Until)live.Go.SetActive(false);
            for(int i=0;i<surfaceHoles.Length;i++)
            {
                var d=surfaceHoles[i];if(d==null||!d.Go.activeSelf)continue;float alpha=NativeSurfaceImpacts.DecalAlpha(now-d.Born);
                if(alpha<=0){d.Go.SetActive(false);continue;}
                alpha=Mathf.Round(alpha*32)/32;if(alpha==d.Alpha)continue;d.Alpha=alpha;
                d.Renderer.GetPropertyBlock(surfaceHoleBlock);surfaceHoleBlock.SetColor(SurfaceTintId,new Color(SurfaceHoleTint.r,SurfaceHoleTint.g,SurfaceHoleTint.b,SurfaceHoleTint.a*alpha));d.Renderer.SetPropertyBlock(surfaceHoleBlock);
            }
            foreach(var e in arena.Events)
            {
                if(e.Id<=surfaceSeen)continue;surfaceSeen=e.Id;
                if(e.Kind!=EventKind.Impact||string.IsNullOrEmpty(e.SurfaceId)||now-e.Time>NativeSurfaceImpacts.Stale)continue;
                string spec=null;foreach(var p in arena.Pilots)if(p.Seat.id==e.Source){spec=p.Seat.spec;break;}
                if(!NativeSurfaceImpacts.Shows(spec,e.Detail))continue;
                int kind=NativeSurfaceImpacts.Kind(SurfaceMaterialOf(arena,e.SurfaceId));
                Vector3 at=NativeArena.Vec(e.Position),normal=NativeArena.Vec(e.Normal);normal=normal.sqrMagnitude>.01f?normal.normalized:Vector3.up;
                if(!LowGraphics)SurfaceEffect(kind,at+normal*NativeSurfaceImpacts.Lift,normal,now);
                SurfaceHole(kind,at+normal*NativeSurfaceImpacts.DecalLift,normal,now,e.Id);
            }
        }
        private string SurfaceMaterialOf(NativeArena arena,string id)
        {
            if(surfaceMaterials.TryGetValue(id,out var material))return material;
            foreach(var prop in arena.AllBlocks)if(prop.Definition.id==id){material=prop.Definition.material;break;}
            surfaceMaterials[id]=material;return material;
        }
        private void SurfaceEffect(int kind,Vector3 at,Vector3 normal,float now)
        {
            var template=surfaceEffects[kind];if(template==null)return;
            var list=surfaceLive[kind];int cap=EffectsQuality<.5f?NativeSurfaceImpacts.PerKindReduced:NativeSurfaceImpacts.PerKind;LiveImpact use=null;
            foreach(var live in list)if(!live.Go.activeSelf){use=live;break;}
            if(use==null&&list.Count<cap){var go=Instantiate(template,surfaceRoot,false);go.name="Impact "+NativeSurfaceImpacts.Names[kind];use=new LiveImpact{Go=go,Systems=go.GetComponentsInChildren<ParticleSystem>(true)};list.Add(use);}
            if(use==null){use=list[0];foreach(var live in list)if(live.Until<use.Until)use=live;}
            use.Go.transform.SetPositionAndRotation(at,Quaternion.LookRotation(normal)*Quaternion.Euler(0,0,Random.Range(0f,360f)));
            if(!use.Go.activeSelf)use.Go.SetActive(true);
            foreach(var ps in use.Systems){ps.Clear(false);ps.Play(false);}
            use.Until=now+NativeSurfaceImpacts.EffectLife;
        }
        private void SurfaceHole(int kind,Vector3 at,Vector3 normal,float now,int seed)
        {
            var template=surfaceDecals[kind];if(template==null)return;
            int slot=surfaceNextHole;surfaceNextHole=(slot+1)%surfaceHoles.Length;var d=surfaceHoles[slot];
            if(d==null||d.Go==null){var go=Instantiate(template,surfaceRoot,false);d=surfaceHoles[slot]=new LiveDecal{Go=go,Renderer=go.GetComponent<Renderer>(),Kind=kind};}
            else if(d.Kind!=kind){var tr=template.GetComponent<Renderer>();if(tr!=null)d.Renderer.sharedMaterial=tr.sharedMaterial;d.Kind=kind;}
            // Its quad faces -z: turned to face out of the surface, spun and sized by the event (the same hole on every client).
            var side=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized;var spin=Quaternion.AngleAxis(seed*137.5f%360,normal)*side;
            float size=.85f+(seed*61%31)/100f;d.Go.transform.SetPositionAndRotation(at,Quaternion.LookRotation(-normal,spin));d.Go.transform.localScale=template.transform.localScale*size;
            d.Born=now;d.Alpha=-1;if(!d.Go.activeSelf)d.Go.SetActive(true);
        }
        private void ClearSurfaceImpacts()
        {
            foreach(var list in surfaceLive)foreach(var live in list)if(live.Go!=null)live.Go.SetActive(false);
            if(surfaceHoles!=null)foreach(var d in surfaceHoles)if(d?.Go!=null)d.Go.SetActive(false);
        }
    }
}
