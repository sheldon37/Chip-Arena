using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // Pilot armor lights: the per-material tables of Resources/NativeArmorLight.cginc, set once when the bank loads
    // (Core/NativeArmorLight.cs builds them). Only the pilot's part materials and the hand-held weapons get a role; every
    // other material (maps, Siren, horde, hats, ink, ghosts, hologram, portraits) keeps _ArmorLight 0 and its exact look.
    public sealed partial class NativeSourceModels
    {
        private static readonly int ArmorLightId=Shader.PropertyToID("_ArmorLight"),ArmorLevelId=Shader.PropertyToID("_ArmorLevel"),ArmorCountId=Shader.PropertyToID("_ArmorCount"),
            ArmorPhaseId=Shader.PropertyToID("_ArmorPhase"),ArmorEmitId=Shader.PropertyToID("_ArmorEmit"),ArmorSizeId=Shader.PropertyToID("_ArmorSize");
        private List<NativeArmorLight.Section> armorPilot;
        // Corpses and regenerating pilots (QueueShadowless) skip the light boxes that cross a severable joint (glow.w .5): a
        // severed limb or torso flies off without the glow of a trim it left behind; emission and every other box are kept.
        private static readonly Vector4 CorpseGlow=new Vector4(NativeArmorLight.CorpseGlow.X,NativeArmorLight.CorpseGlow.Y,NativeArmorLight.CorpseGlow.Z,NativeArmorLight.CorpseGlow.W);
        /// <summary>Pilot part materials given an armor-light role (emitters, receivers), for NativeBuildChecks.</summary>
        public int ArmorEmitters{get;private set;}
        public int ArmorReceivers{get;private set;}
        public int ArmorHeld{get;private set;}
        // A section's tag, emitter level and voxel boxes (the export's 24-vertex box chunks; anything else is one box).
        private static NativeArmorLight.Section ArmorSection(SourceSection s)
        {
            int count=s.positions.Length/3;var section=new NativeArmorLight.Section{Part=s.part,Bone=s.bone};
            float tag=count>0?s.emission[3]:0,glow=count>0?s.emission[0]+s.emission[1]+s.emission[2]:0;
            section.Emitter=tag>1.5f&&glow>0;section.Level=section.Emitter&&s.colors[1]>1e-4f?s.emission[1]/s.colors[1]:0;
            var min=new NVector(float.MaxValue);var max=new NVector(float.MinValue);
            for(int i=0;i<count;i++){var p=new NVector(s.positions[i*3],s.positions[i*3+1],s.positions[i*3+2]);min=NVector.Min(min,p);max=NVector.Max(max,p);}
            section.Min=min;section.Max=max;
            bool boxes=count>0&&count%24==0;
            for(int start=0;boxes&&start<count;start+=24)
            {
                var lo=new NVector(float.MaxValue);var hi=new NVector(float.MinValue);
                for(int i=start;i<start+24;i++){var p=new NVector(s.positions[i*3],s.positions[i*3+1],s.positions[i*3+2]);lo=NVector.Min(lo,p);hi=NVector.Max(hi,p);}
                for(int i=start;i<start+24&&boxes;i++)for(int k=0;k<3;k++){float v=s.positions[i*3+k],a=k==0?lo.X:k==1?lo.Y:lo.Z,b=k==0?hi.X:k==1?hi.Y:hi.Z;if(Math.Abs(v-a)>1e-5f&&Math.Abs(v-b)>1e-5f){boxes=false;break;}}
                if(boxes)section.Voxels.Add((lo,hi));
            }
            if(!boxes){section.Voxels.Clear();if(count>0)section.Voxels.Add((min,max));}
            return section;
        }
        private void ArmorMaterial(Material material,NativeArmorLight.Plan plan)
        {
            if(material==null||plan.Role==NativeArmorLight.Off)return;
            var emit=new Vector4[NativeArmorLight.MaxBoxes];var size=new Vector4[NativeArmorLight.MaxBoxes];
            for(int k=0;k<plan.Boxes.Length&&k<emit.Length;k++){var b=plan.Boxes[k];emit[k]=new Vector4(b.Center.X,b.Center.Y,b.Center.Z,b.Level);size[k]=new Vector4(b.Half.X,b.Half.Y,b.Half.Z,b.Severs?-b.Sigma:b.Sigma);}
            material.SetFloat(ArmorLightId,plan.Role);material.SetFloat(ArmorLevelId,plan.Level);material.SetFloat(ArmorCountId,Math.Min(plan.Boxes.Length,NativeArmorLight.MaxBoxes));
            material.SetFloat(ArmorPhaseId,plan.Phase);material.SetVectorArray(ArmorEmitId,emit);material.SetVectorArray(ArmorSizeId,size);
            if(plan.Role==NativeArmorLight.Emitter)ArmorEmitters++;else if(plan.Role==NativeArmorLight.Receiver)ArmorReceivers++;else ArmorHeld++;
        }
        /// <summary>
        /// Called once per model while the bank loads, after its parts are built (parts follow its non-empty sections in
        /// order). The pilot comes first in the bank, so its cuffs are known when the hand-held weapons follow.
        /// </summary>
        private void ArmorLights(SourceModel model,List<Part> parts)
        {
            if(model.id=="pilot")
            {
                NativeArmorLights.Defaults();var sections=new List<NativeArmorLight.Section>();
                foreach(var s in model.sections)if(s.positions.Length>0)sections.Add(ArmorSection(s));
                if(sections.Count!=parts.Count)return;
                var plans=NativeArmorLight.Build(sections);for(int i=0;i<parts.Count;i++)ArmorMaterial(parts[i].Material,plans[i]);
                armorPilot=sections;return;
            }
            if(armorPilot==null||!NativeArmorLight.HandHeld(model.id))return;
            foreach(var part in parts)if(part.Bone==CapturedRig.LHand||part.Bone==CapturedRig.RHand)ArmorMaterial(part.Material,NativeArmorLight.HeldPlan(armorPilot,part.Bone));
        }
    }
    public sealed partial class ArenaPresentation
    {
        // The held weapon's draw opts its cuff spill in (NativeArmorLight.Held). Its accent tint reaches only the spill:
        // weapon voxels carry emission tag 0, so their own colours are untouched.
        private static readonly Vector4 HeldGlow=new Vector4(NativeArmorLight.HeldGlow.X,NativeArmorLight.HeldGlow.Y,NativeArmorLight.HeldGlow.Z,NativeArmorLight.HeldGlow.W);
    }
    /// <summary>The armor-light globals: the export's ember, the quality and the breathing amplitude.</summary>
    public static class NativeArmorLights
    {
        private static readonly int EmberId=Shader.PropertyToID("_ArmorExportEmber"),QualityId=Shader.PropertyToID("_ArmorQuality"),PulseId=Shader.PropertyToID("_ArmorPulse");
        private static float quality=-1,pulse=-1;
        public static void Defaults()
        {
            var e=NativePortraitRules.ExportEmber;Shader.SetGlobalVector(EmberId,new Vector4(e.X,e.Y,e.Z,1));
            quality=pulse=-1;Apply(1);
        }
        /// <summary>From the combat glow quality (ArenaPresentation.GlowQuality: 1 high, .4 low, .3 reduced effects, 0 off).</summary>
        public static void Apply(float glow)
        {
            float q=NativeArmorLight.Quality(glow),p=NativeArmorLight.PulseFor(glow);
            if(q!=quality){quality=q;Shader.SetGlobalFloat(QualityId,q);}
            if(p!=pulse){pulse=p;Shader.SetGlobalFloat(PulseId,p);}
        }
    }
}
