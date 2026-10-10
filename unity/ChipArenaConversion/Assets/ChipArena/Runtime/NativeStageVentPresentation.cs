using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Ward Zero's floor vents (client/siren/stage.ts WardZeroVisuals, Core/NativeStageVentFx.cs): the rim under each grate that
    // fills warn red while the vent arms and turns white while it erupts, black wisps while it warns, the eruption burst and
    // black flame jet, the open-vent rim flicker and the smoke spilling out while a lid swings. The beat-synced eruptions are
    // the boss song's schedule (NativeBossTrack.Vent, the simulation's own damage source); the pits are the entry schedule
    // (NativeArena.Entry: PvE entries or the Ward Zero cycle) that NativeWardPresentation swings the lids from. Everything runs
    // on the field clock (FieldClock: continuous for guests, the presentation clock for host and killcam), so host, guests and
    // the killcam replay all draw it from data they hold. The flames burn in the shared Siren flame pool (WorldFlames,
    // NativeDevEffects draws and ages it); the rim is the source's glowMaterial (NativeFieldGlow, normal blend, double sided,
    // fogged, raw colour). Presentation only: nothing here touches the vent timing, falls, damage or networking.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeStageVentState[] ventStates=NewVentStates();
        private readonly float[] ventWardWarn=new float[NativeStageVentFx.Count],ventGlow=new float[NativeStageVentFx.Count];
        // One rim mesh and material for the process (rebuilt if Unity destroyed them); the property block is per presentation.
        private static Mesh ventRimMesh;private static Material ventRimMaterial;private MaterialPropertyBlock ventRimProperties;
        private NativeArena ventArena;private int ventGeneration=-1;private float ventLast;
        private static NativeStageVentState[] NewVentStates()
        {var s=new NativeStageVentState[NativeStageVentFx.Count];for(int i=0;i<s.Length;i++)s[i]=new NativeStageVentState();return s;}
        private void ClearStageVents(){foreach(var s in ventStates)s.Clear();ventArena=null;ventGeneration=-1;}
        /// <summary>Called by WardPresentation (Ward Zero only) with the entries it swung the lids from this frame (vents 0-7).</summary>
        private void StageVentEffects(NativeArena arena,NativeEntryView[] entries)
        {
            float clock=FieldClock(arena);
            if(arena!=ventArena||arena.Generation!=ventGeneration||clock<ventLast-.25f){ClearStageVents();ventArena=arena;ventGeneration=arena.Generation;}
            double dt=NativeStageVentFx.Step(ventLast,clock),q=NativeStageVentFx.Quality(EffectsQuality);ventLast=clock;
            var fire=WorldFlames();var vents=NativeArena.StageVents;bool ward=arena.Pve==null;int count=Math.Min(Math.Min(vents.Length,entries.Length),NativeStageVentFx.Count);
            // updateEntries: each vent's opening (drawn ahead of the authority outside PvE), its warning and rim flicker, and the
            // smoke while its lid swings.
            for(int i=0;i<count;i++)
            {
                var entry=entries[i];float open=NativeVentRules.Open(entry,clock,ward);
                ventWardWarn[i]=ward&&entry!=null?NativeVentRules.Warning(entry,clock):0;
                bool active=ward?NativeStageVentFx.PitActive(open):entry!=null&&NativeStageVentFx.EntryActive(entry.openAt,entry.closeAt,clock);
                ventGlow[i]=active?(float)NativeStageVentFx.OpenGlow(open,clock):0;
                ventStates[i].Smoke=NativeStageVentFx.EntrySmoke(fire,ventStates[i].Smoke,-vents[i].x,vents[i].z,open,dt,q);
            }
            // The boss song's eruptions on the same clock (a guest's song time runs with its render clock).
            double song=arena.BossSongTime;bool sung=song>=0;if(sung)song+=clock-arena.Time;
            if(ventRimMesh==null||ventRimMaterial==null)CreateVentRim();if(ventRimProperties==null)ventRimProperties=new MaterialPropertyBlock();
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            for(int i=0;i<count;i++)
            {
                var hazard=sung?arena.BossTrack.Vent(song,i%2):(active:false,warning:0f);double warn=Math.Max(hazard.warning,ventWardWarn[i]);Vector3 at=vents[i];
                NativeStageVentFx.Vent(fire,ventStates[i],-at.x,at.z,warn,hazard.active,dt,q);
                NativeStageVentFx.Rim(hazard.active,warn,ventGlow[i],out int hex,out double opacity);if(opacity<=.001)continue;
                // A glowMaterial colour reaches the screen as its linear value (NativeFieldRules.SirenDisplay); vertex and uniform
                // colours are not converted, so a linear project writes the linearised display value.
                NativeFieldRules.SirenDisplay(hex,1,out double r,out double g,out double b);var c=new Color((float)r,(float)g,(float)b);if(linear)c=c.linear;c.a=(float)opacity;
                ventRimProperties.Clear();ventRimProperties.SetColor("_Color",c);
                Graphics.DrawMesh(ventRimMesh,Matrix4x4.Translate(new Vector3(at.x,0,at.z)),ventRimMaterial,0,null,0,ventRimProperties,ShadowCastingMode.Off,false);
            }
        }
        private static void CreateVentRim()
        {
            var points=new Vector3[NativeStageVentFx.RimVertices];var colors=new Color[points.Length];
            for(int i=0;i<points.Length;i++){NativeStageVentFx.RimVertex(i,out double x,out double y,out double z);points[i]=new Vector3((float)x,(float)y,(float)z);colors[i]=Color.white;}
            ventRimMesh=new Mesh{name="Native Ward Zero vent rim"};ventRimMesh.vertices=points;ventRimMesh.colors=colors;ventRimMesh.triangles=NativeStageVentFx.RimTriangles;ventRimMesh.RecalculateBounds();
            var shader=Resources.Load<Shader>("NativeFieldGlow");if(shader==null)throw new InvalidOperationException("NativeFieldGlow shader is missing");
            // Transparent, depthWrite off, DoubleSide, renderOrder 0: before the open pit's shaft (Transparent+1), which covers it.
            ventRimMaterial=new Material(shader){name="Native Ward Zero vent rim",renderQueue=(int)RenderQueue.Transparent};
            ventRimMaterial.SetFloat("_Cull",(float)CullMode.Off);ventRimMaterial.SetFloat("_Offset",0);ventRimMaterial.SetFloat("_Radial",0);
        }
    }
}
