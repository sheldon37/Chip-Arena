using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // A pilot on the model who falls into an open Ward Zero vent drops down its shaft as its own ragdoll (Core NativeVentFall; owner
    // 2026-11-13: "Falling through vent still reverts back to voxel model, but when falling down the player should ragdoll"). The vent
    // deaths used to skip the corpse for vent-fall.ts's scripted victim (NativeVentDeathPresentation.cs), which draws the voxel body;
    // now they take the corpse every other death takes, on the model and its skin, with the vent's shaft in its physics world. The
    // scripted victim stays for the voxel bodies (ArenaPresentation.VentModel). Presentation only.
    internal sealed partial class NativeCorpse
    {
        // The vent this body fell into (its centre on the floor; null for every other body), worked out once as the body is made,
        // with its death's seed and style then: a kept fall's record turns into its splat in place, so the body can tell it is stale.
        private bool ventKnown;private Vector3? ventShaft;private int ventSeed;private string ventStyle;
        private readonly Dictionary<NativePropBody,BoxCollider[]> ventFloors=new Dictionary<NativePropBody,BoxCollider[]>();
        private static readonly V3[] ventBoxes=new V3[10];private static V3[] ventSpots;
        /// <summary>The floor centre of the vent this body fell into, or null.</summary>
        internal Vector3? VentShaft=>ventShaft;
        /// <summary>Whether this body was made for an earlier record of its death (the fall, once its record is the splat).</summary>
        internal bool VentStale(NativeDeath death)=>ventKnown&&(death.seed!=ventSeed||death.style!=ventStyle);
        // RefreshWorld, the first time (the body being made): which vent it fell into, and that vent's shaft walls and bottom.
        private void VentShaftOnce()
        {
            if(ventKnown)return;ventKnown=true;ventSeed=Death.seed;ventStyle=Death.style;
            if(!NativeVentFall.Falls(Death.style)||arena==null||!NativeMapRules.WardInstance(arena.MapId))return;
            var vents=NativeArena.StageVents;if(ventSpots==null||ventSpots.Length!=vents.Length){ventSpots=new V3[vents.Length];for(int i=0;i<vents.Length;i++)ventSpots[i]=NativeArena.Vec(vents[i]);}
            int e=NativeVentFall.Vent(NativeArena.Vec(Death.position),ventSpots);if(e<0)return;ventShaft=vents[e];
            NativeVentFall.Shaft(ventSpots[e],ventBoxes);for(int i=0;i<5;i++)VentBox("Vent shaft "+i,ventBoxes[i*2],ventBoxes[i*2+1]);
        }
        private BoxCollider VentBox(string name,V3 min,V3 max)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);var box=go.AddComponent<BoxCollider>();box.sharedMaterial=surface;
            Vector3 a=NativeArena.Vec(min),b=NativeArena.Vec(max);box.center=(a+b)*.5f;box.size=b-a;return box;
        }
        /// <summary>RefreshWorld, for each block near the body: a floor the vent opens in is copied once round the opening (its four
        /// pieces; the floors are static) instead of whole. True when it is. A turned floor is copied whole, as before.</summary>
        private bool VentFloor(NativePropBody prop,BoxCollider original,Bounds bounds)
        {
            if(ventShaft==null)return false;if(ventFloors.ContainsKey(prop))return true;var at=NativeArena.Vec(ventShaft.Value);
            if(!NativeVentFall.Cuts(NativeArena.Vec(bounds.min),NativeArena.Vec(bounds.max),at)||Quaternion.Angle(original.transform.rotation,Quaternion.identity)>.01f)return false;
            int n=NativeVentFall.Around(NativeArena.Vec(bounds.min),NativeArena.Vec(bounds.max),at,ventBoxes);var copies=new BoxCollider[n];
            for(int i=0;i<n;i++)copies[i]=VentBox("Body obstacle "+prop.name+" round the vent",ventBoxes[i*2],ventBoxes[i*2+1]);
            ventFloors[prop]=copies;Physics.SyncTransforms();return true;
        }
        /// <summary>A vent body's start, as it is made: the whole body moved by <paramref name="by"/>, at <paramref name="velocity"/>,
        /// turning at <paramref name="spin"/> about its pelvis (its parts moving as one), the upper arms and forearms
        /// <paramref name="arms"/> m/s more upward.</summary>
        internal void VentLaunch(Vector3 by,Vector3 velocity,Vector3 spin,float arms)
        {
            if(!Parts.TryGetValue(CapturedRig.Pelvis,out var pelvis)||pelvis==null)return;Vector3 pivot=pelvis.transform.position+by;
            foreach(var pair in Parts)
            {
                var body=pair.Value;if(body==null)continue;var t=body.transform;t.position+=by;body.position=t.position;if(body.isKinematic)continue;
                int b=pair.Key;float up=b==CapturedRig.LArm||b==CapturedRig.LElbow||b==CapturedRig.RArm||b==CapturedRig.RElbow?arms:0;
                body.linearVelocity=velocity+Vector3.Cross(spin,t.position-pivot)+Vector3.up*up;body.angularVelocity=spin;
            }
            if(weapon!=null){weapon.transform.position+=by;weapon.position=weapon.transform.position;if(!weapon.isKinematic)weapon.linearVelocity=velocity+Vector3.Cross(spin,weapon.transform.position-pivot);}
            Physics.SyncTransforms();
        }
    }
    public sealed partial class ArenaPresentation
    {
        /// <summary>Whether a vent death is the model's ragdoll (a pilot on the model, its skin included) rather than vent-fall.ts's
        /// scripted victim (NativeVentDeathPresentation.cs), which is the voxel body's: Settings · Player model Classic (ClassicBodies),
        /// the --ca-voxel-bodies launch or a client without the bake keep it, and a voxel map could choose it the same way.</summary>
        private bool VentModel(NativeDeath death,bool pilot)=>pilot&&VisualDeathFor(death);
        /// <summary>Drops a vent death's body made for an earlier record of it (a kept fall's record turns into its splat in place).</summary>
        private void VentFresh(NativeDeath death)
        {if(corpses.TryGetValue(death.id,out var old)&&old.VentStale(death)){old.Dispose();corpses.Remove(death.id);}}
        /// <summary>A vent death's body as it is made: a fall starts down its shaft (straight down, the arms thrown up, tipping forward);
        /// a splat starts high over its spot, tipping so it lands face down at VENT_PIT.drop.</summary>
        private static void VentLaunch(NativeCorpse corpse,NativeDeath death)
        {
            var right=Quaternion.Euler(0,death.yaw,0)*Vector3.right;
            if(death.style=="vent-splat")corpse.VentLaunch(Vector3.up*NativeVentFall.SplatLift,Vector3.down*NativeVentFall.SplatSpeed,right*NativeVentFall.SplatTip,0);
            else if(corpse.VentShaft.HasValue)corpse.VentLaunch(Vector3.zero,Vector3.down*NativeVentFall.FallSpeed,right*NativeVentFall.FallTip,NativeVentFall.ArmsUp);
        }
        /// <summary>A body down its shaft is out of sight past the hide depth.</summary>
        private static bool VentHidden(NativeCorpse corpse,Vector3 pelvis)=>corpse.VentShaft is Vector3 v&&NativeVentFall.Hidden(pelvis.y,v.y);
        // The splat's dust directions per death (seed), made once.
        private readonly Dictionary<int,(int seed,NativeVentDeath.Dust[] dust)> ventSplats=new Dictionary<int,(int,NativeVentDeath.Dust[])>();
        /// <summary>The ragdoll's splat: the scripted splat's dust and SPLAT! pop once it has landed (VENT_PIT.drop after the record).</summary>
        private void VentSplatFx(NativeDeath death,float fx)
        {
            if(death.style!="vent-splat")return;float since=(death.kept?Math.Min(fx,NativeVentDeath.KeepAge):fx)-NativeVentDeath.Drop;if(since<0)return;
            if(!ventSplats.TryGetValue(death.id,out var s)||s.seed!=death.seed)
            {
                if(ventSplats.Count>=16)ventSplats.Clear();int n=NativeVentDeath.DustCount(Mathf.Clamp01(EffectsQuality));var dust=new NativeVentDeath.Dust[n];
                for(int i=0;i<n;i++)dust[i]=NativeVentDeath.DustDirection(death.seed,i);s=(death.seed,dust);ventSplats[death.id]=s;
            }
            VentSplatExtras(s.dust,death,since);
        }
    }
}
