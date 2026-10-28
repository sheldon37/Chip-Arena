using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The advanced lighting in the arena and the killcam replay (docs/LIGHTING-ADVANCED.md; SourceWorld calls
    /// AdvancedLightingBegin before the maps' sceneries and AdvancedLightingWorld after them):
    ///  - the light volume's frame (Runtime NativeAdvancedLighting.Frame: the bake, the lightning, the wet clock), with the arena's
    ///    content materials and the map presentation's light candidates for the bake (the town's, the station's, the Ironworks');
    ///  - the volumetric beams: while the look's beams are on above the low tier, the maps' FX builders take this frame's beam list
    ///    (<see cref="AdvancedBeams"/>) in place of their billboard shafts, and one instanced draw shows them (NativeVolumetricBeams);
    ///  - the weather (Core NativeWeatherFx over the map's cover): the fall and the splashes through one NativeFxDraw; on Cyber
    ///    City its own rain stands down for it (NativeCyberCityFx.ExternalRain).
    /// Behind the menu, on a stage and on the low tier none of this runs: the builders keep their billboards and Cyber City its
    /// own rain, exactly as before. Allocation free per frame once its pieces are made.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>The graphics tier (Settings: Core NativeGraphicsTiers).</summary>
        public NativeGraphicsTier GraphicsTier=NativeGraphicsTier.High;
        private NativeVolumetricBeams beamDraw;private bool beamsFailed,beamFrame,weatherFrame;private readonly NativeBeamList beamList=new NativeBeamList();
        private NativeWeatherFx weatherFx;private NativeFxDraw weatherDraw;private bool weatherFailed;private NativeWeatherCover weatherCover;private ContentMap weatherMap;private readonly NativeFxLight weatherLight=new NativeFxLight();
        private readonly List<NativeVolumeLight> volumeCandidates=new List<NativeVolumeLight>();
        /// <summary>This frame's beam list for the FX builders (null: their billboards, as before).</summary>
        private NativeBeamList AdvancedBeams=>beamFrame?beamList:null;
        /// <summary>Whether the advanced weather draws the rain this frame (Cyber City's own then stands down).</summary>
        private bool AdvancedRain=>weatherFrame;
        private void AdvancedLightingBegin(NativeArena arena)
        {
            var s=NativeAdvancedLighting.State;beamList.Clear();beamFrame=weatherFrame=false;
            if(!s.Any||LowGraphics)return;
            if(s.Beams&&EnsureBeams()){beamList.Gain=s.BeamGain;beamFrame=true;}
            if(s.Weather.Length>0){EnsureWeather();weatherFrame=weatherFx!=null;}
        }
        private void AdvancedLightingWorld(NativeArena arena)
        {
            var s=NativeAdvancedLighting.State;bool beams=beamFrame,weather=weatherFrame;beamFrame=weatherFrame=false;if(!s.Any||LowGraphics){beamList.Clear();return;}
            NativeAdvancedLighting.Materials(arena.Content.materials);VolumeCandidates(arena);float now=PresentationClock(arena);NativeAdvancedLighting.Frame(now);
            var eye=WorldEye;
            if(beams)beamDraw.Draw(beamList,eye,s.BeamSoft,s.BeamSamples,now,0);else beamList.Clear();
            if(weather)WeatherWorld(arena,eye,now);
        }
        private bool EnsureBeams()
        {
            if(beamDraw!=null)return beamDraw.Ready;if(beamsFailed)return false;
            try{beamDraw=new NativeVolumetricBeams();if(!beamDraw.Ready){beamsFailed=true;return false;}return true;}
            catch(Exception e){Debug.LogWarning("Volumetric beams disabled: "+e.Message);beamsFailed=true;beamDraw=null;return false;}
        }
        private void EnsureWeather()
        {
            if(weatherFx!=null||weatherFailed)return;
            try{weatherFx=new NativeWeatherFx();weatherDraw=new NativeFxDraw();}catch(Exception e){Debug.LogWarning("Weather disabled: "+e.Message);weatherFailed=true;weatherFx=null;weatherDraw=null;}
        }
        // The map presentation's light candidates (more than the eight slots), for the volume's bake.
        private void VolumeCandidates(NativeArena arena)
        {
            volumeCandidates.Clear();
            if(townLights!=null)foreach(var c in townLights.Candidates)volumeCandidates.Add(new NativeVolumeLight(c.X,c.Y,c.Z,c.Range,c.R,c.G,c.B));
            else if(stationLights!=null)foreach(var c in stationLights.Candidates)volumeCandidates.Add(new NativeVolumeLight(c.X,c.Y,c.Z,c.Range,c.R,c.G,c.B));
            else if(iwLights!=null)foreach(var c in iwLights.Candidates)volumeCandidates.Add(new NativeVolumeLight(c.X,c.Y,c.Z,c.Range,c.R,c.G,c.B));
            NativeAdvancedLighting.Candidates(arena.MapId,volumeCandidates);
        }
        private void WeatherWorld(NativeArena arena,Camera camera,float now)
        {
            if(camera==null)return;var s=NativeAdvancedLighting.State;var map=arena.Map;
            if(!ReferenceEquals(map,weatherMap)){weatherMap=map;weatherCover=NativeWeatherCover.Build(map?.blocks,map?.bounds);}
            NativeAdvancedLighting.Cover(weatherCover);
            ReadSceneLight(weatherLight);var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            weatherFx.Build(s.Weather,s.Amount,s.Wind,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,weatherLight,weatherCover);
            var q=weatherFx.Quads;weatherDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?weatherDraw.Solid:weatherDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            weatherDraw.Draw(camera,null,now);
        }
        // The scene's key light and trilight ambient in linear values (as ReadCyberLight reads them).
        private static void ReadSceneLight(NativeFxLight l)
        {
            var sun=RenderSettings.sun;
            if(sun!=null&&sun.isActiveAndEnabled){var d=-sun.transform.forward;Color c=GraphicsSettings.lightsUseLinearIntensity?sun.color.linear*sun.intensity:(sun.color*sun.intensity).linear;l.Lx=d.x;l.Ly=d.y;l.Lz=d.z;l.Sr=c.r;l.Sg=c.g;l.Sb=c.b;}
            else{l.Lx=0;l.Ly=1;l.Lz=0;l.Sr=l.Sg=l.Sb=0;}
            bool tri=RenderSettings.ambientMode==AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,eq=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,gr=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            l.Ambient[0]=sky.r;l.Ambient[1]=sky.g;l.Ambient[2]=sky.b;l.Ambient[3]=eq.r;l.Ambient[4]=eq.g;l.Ambient[5]=eq.b;l.Ambient[6]=gr.r;l.Ambient[7]=gr.g;l.Ambient[8]=gr.b;
        }
        private void DisposeAdvancedLighting(){beamDraw?.Dispose();beamDraw=null;weatherDraw?.Dispose();weatherDraw=null;weatherFx=null;weatherCover=null;weatherMap=null;}
    }
}
