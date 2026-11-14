using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The slash pack on the special moves and finishers (Core NativePackSlashes; docs/PACK-EFFECTS.md): the bake's VisualSlashFx
    // carries the pack's ring slash (and its mirror), line slashes and claw marks as particle systems on our own shader
    // (Resources/NativePackSlash.shader). A finisher plays at its strike's hit window, a melee Q/E ability or special move at
    // its release, laid about the pilot as its look says (flat round it, stood up across its facing, or ahead along it),
    // tinted to the pilot's sweep colour (the coloured strokes; the ink stays black). Read once; without the bake nothing
    // draws. Pooled per kind, no allocation once warm; off on the low graphics tier and for my own first-person pilot.
    // Presentation only: every move's timing, reach and effect are the rules'.
    public sealed partial class ArenaPresentation
    {
        private sealed class PackSlash { public GameObject Go; public ParticleSystem[] Systems; public Renderer[] Tinted; public float Until=float.NegativeInfinity; }
        private sealed class PackSlashCue { public int Id; public float At; public NativePackSlashes.Look Look; public Color Tint; }
        private GameObject[] packSlashTemplates; private bool packSlashRead; private Transform packSlashRoot;
        private readonly List<PackSlash>[] packSlashLive={new List<PackSlash>(),new List<PackSlash>(),new List<PackSlash>(),new List<PackSlash>()};
        private readonly Dictionary<int,float> packSlashStrike=new Dictionary<int,float>(),packSlashRelease=new Dictionary<int,float>();
        private readonly Dictionary<int,NativePackSlashes.Look> packSlashFinisher=new Dictionary<int,NativePackSlashes.Look>();
        private readonly List<PackSlashCue> packSlashCues=new List<PackSlashCue>();private readonly Stack<PackSlashCue> spareSlashCues=new Stack<PackSlashCue>();
        private MaterialPropertyBlock packSlashBlock;private int packSlashGeneration=-1;private readonly NativeMeleeSwing packSwing=new NativeMeleeSwing();
        private static readonly int PackTintId=Shader.PropertyToID("_TintColor");private const float TintGain=1.3f;

        private bool PackSlashBank()
        {
            if(packSlashRead)return packSlashTemplates!=null;packSlashRead=true;
            var prefab=Resources.Load<GameObject>(VisualBodyResource);var holder=prefab!=null?prefab.transform.Find("VisualSlashFx"):null;if(holder==null)return false;
            var t=new GameObject[NativePackSlashes.Kinds];for(int k=0;k<t.Length;k++){var e=holder.Find(NativePackSlashes.Names[k]);if(e==null)return false;t[k]=e.gameObject;}
            packSlashTemplates=t;packSlashRoot=new GameObject("Pack slashes").transform;packSlashRoot.SetParent(transform,false);packSlashBlock=new MaterialPropertyBlock();
            return true;
        }

        // RenderMeleeTrails, after the rigs are posed: new finishers and releases cued, due cues played, finished effects put away.
        private void PackSlashFx(float time)
        {
            var s=weaponSnapshot;if(!NativePackSlashes.On||s?.Actors==null||LowGraphics||!PackSlashBank())return;
            int generation=WeaponLoadout?.Generation??-1;if(generation!=packSlashGeneration){ClearPackSlashes();packSlashGeneration=generation;}
            foreach(var a in s.Actors)
            {
                if(!a.Alive||FirstPerson&&a.Id==LocalId)continue;
                // A strike seen once (its start, the attack index as the sweep counts it); a finisher waits for its hit window (the
                // swing, replicated with or after the strike), given up a second on.
                if(a.AttackAt>-90&&(!packSlashStrike.TryGetValue(a.Id,out float seen)||seen!=a.AttackAt))
                {
                    packSlashStrike[a.Id]=a.AttackAt;packSlashFinisher.Remove(a.Id);
                    int index=a.Combo+1;packSwing.Spec=a.SourceSpec;packSwing.Index=index;bool reverse=NativeMeleeRules.Reverse(packSwing);
                    if(time-a.AttackAt<NativePackSlashes.Stale+1&&NativePackSlashes.Finisher(a.SourceSpec,index,reverse,out var look))packSlashFinisher[a.Id]=look;
                }
                if(packSlashFinisher.TryGetValue(a.Id,out var finisher))
                {
                    if(a.SwingAt>=a.AttackAt&&time>=a.SwingAt){packSlashFinisher.Remove(a.Id);Cue(a.Id,a.SwingAt,finisher,a);}
                    else if(time>a.AttackAt+1)packSlashFinisher.Remove(a.Id);
                }
                // A special move's or melee Q/E ability's release, once: the special's when one is set (a kit's launch is the sword's
                // launch, its recast Skyfall), else the kit's.
                bool special=!string.IsNullOrEmpty(a.SpecialKind);string kind=special?a.SpecialKind:a.MeleeKit!=null&&a.MeleeKit.At>-90?a.MeleeKit.Kind:null;
                float release=special?a.SpecialRelease:a.MeleeKit!=null&&a.MeleeKit.At>-90?a.MeleeKit.Release:0;
                if(!string.IsNullOrEmpty(kind)&&release>0&&time>=release&&(!packSlashRelease.TryGetValue(a.Id,out float fired)||fired!=release))
                {
                    packSlashRelease[a.Id]=release;
                    if(time-release<NativePackSlashes.Stale&&NativePackSlashes.Ability(kind,out var look))Cue(a.Id,release,look,a);
                }
            }
            for(int i=packSlashCues.Count-1;i>=0;i--)
            {
                var c=packSlashCues[i];if(time<c.At)continue;packSlashCues.RemoveAt(i);spareSlashCues.Push(c);
                if(time-c.At<NativePackSlashes.Stale&&rigs.TryGetValue(c.Id,out var rig)&&rig?.Root!=null)PlayPackSlash(c.Look,rig.Root.transform,c.Tint,time);
            }
            for(int k=0;k<packSlashLive.Length;k++)foreach(var live in packSlashLive[k])if(live.Go.activeSelf&&time>=live.Until)live.Go.SetActive(false);
        }
        private void Cue(int id,float at,in NativePackSlashes.Look look,Actor a)
        {
            var c=spareSlashCues.Count>0?spareSlashCues.Pop():new PackSlashCue();c.Id=id;c.At=at;c.Look=look;
            // The sweep colour as authored (a material colour: the engine linearises it in a linear project), a little over one
            // as the pack's own tints are.
            int rgb=NativeWeaponDressing.SweepColor(LoadoutPilot(id)?.Seat?.chips);c.Tint=new Color(((rgb>>16)&255)/255f*TintGain,((rgb>>8)&255)/255f*TintGain,(rgb&255)/255f*TintGain,1);
            packSlashCues.Add(c);
        }
        private void PlayPackSlash(in NativePackSlashes.Look look,Transform root,Color tint,float time)
        {
            var template=packSlashTemplates[look.Kind];if(template==null)return;var list=packSlashLive[look.Kind];PackSlash use=null;
            foreach(var live in list)if(!live.Go.activeSelf){use=live;break;}
            if(use==null&&list.Count<NativePackSlashes.PerKind)
            {
                var go=Instantiate(template,packSlashRoot,false);go.name="Pack slash "+NativePackSlashes.Names[look.Kind];
                var tinted=new List<Renderer>();foreach(var r in go.GetComponentsInChildren<Renderer>(true))if(r.name.StartsWith("Tint ",System.StringComparison.Ordinal))tinted.Add(r);
                // Started from the systems not under another (each with its children; a sub-emitter plays from its parent).
                var top=new List<ParticleSystem>();foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){var up=ps.transform.parent;if(up==null||up.GetComponentInParent<ParticleSystem>(true)==null)top.Add(ps);}
                use=new PackSlash{Go=go,Systems=top.ToArray(),Tinted=tinted.ToArray()};list.Add(use);
            }
            if(use==null){use=list[0];foreach(var live in list)if(live.Until<use.Until)use=live;}
            // Laid about the pilot (its rig root: its hips, facing its yaw or its roll): flat round it, stood up across its facing
            // (the ring's plane square to it, its front turned up: whole to a camera behind), or ahead; scaled with the rig.
            float rigScale=root.lossyScale.y;var facing=Quaternion.Euler(0,root.eulerAngles.y,0);var at=root.position+Vector3.up*(look.Height*rigScale);
            var turn=look.Lay==NativePackSlashes.Lay.Upright?facing*Quaternion.Euler(-90,0,0):facing;
            use.Go.transform.SetPositionAndRotation(at,turn);use.Go.transform.localScale=Vector3.one*(look.Scale*rigScale);
            foreach(var r in use.Tinted){r.GetPropertyBlock(packSlashBlock);packSlashBlock.SetColor(PackTintId,tint);r.SetPropertyBlock(packSlashBlock);}
            if(!use.Go.activeSelf)use.Go.SetActive(true);
            foreach(var ps in use.Systems){ps.Clear(true);ps.Play(true);}
            use.Until=time+NativePackSlashes.EffectLife;
        }
        private void ClearPackSlashes()
        {
            foreach(var list in packSlashLive)foreach(var live in list)if(live.Go!=null)live.Go.SetActive(false);
            foreach(var c in packSlashCues)spareSlashCues.Push(c);packSlashCues.Clear();packSlashStrike.Clear();packSlashRelease.Clear();packSlashFinisher.Clear();
        }
    }
}
