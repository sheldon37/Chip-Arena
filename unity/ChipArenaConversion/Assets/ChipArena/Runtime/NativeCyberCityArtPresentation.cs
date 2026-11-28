using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Cyber City's stage art (docs/arenas/STAGE-ART.md, docs/arenas/CYBERCITY-ART.md): the hooks NativeCyberCityPresentation
    /// draws through. With the bank ChipArenaStageCyberCity (git-ignored, from the builder's Drive zip) one NativeStageArtDraw
    /// draws the modelled street, buildings and furniture, the breakable and carried blocks' models and the train's cars (Core
    /// NativeCyberCityArt.Pose), and the presentation leaves out what they replace: the steady kit parts on a static block, a
    /// breakable or carried block's steady parts while its model draws (NativeStageArt.Keep), the train's boxes and the backdrop's
    /// near ring and tunnels (NativeCyberCityArt.Near). The driven parts (NativeCyberCityArt.Kept) stay the kit's boxes, so every
    /// telegraph is exactly the box kit's. Without the bank nothing here draws and every box draws as before. Presentation only.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private NativeStageArtDraw metroArt;private readonly NativeStageArtPose metroPose=new NativeStageArtPose();private bool metroArtDrawn;
        // Per map block: in the static chunks; else its model's damage stages (NativeBlockLooks.StageMax).
        private bool[] metroModelled=Array.Empty<bool>();private float[] metroStageMax=Array.Empty<float>(),metroStage1=Array.Empty<float>(),metroStage2=Array.Empty<float>();
        /// <summary>Whether Cyber City's art drew this frame (false without its bank: the box kit drew).</summary>
        public bool CyberCityArtDrawn=>metroArtDrawn;

        // Once per arena (behind the loading screen through PrewarmStage): the bank's meshes and each block's stages.
        private void MetroArtPrepare(ContentMap map)
        {
            if(metroArt==null)metroArt=gameObject.AddComponent<NativeStageArtDraw>();metroArt.Prepare(map,NativeCyberCityArt.Slots,NativeCyberCityArt.Casts);
            int n=map.blocks.Length;metroModelled=new bool[n];metroStageMax=new float[n];metroStage1=new float[n];metroStage2=new float[n];
            for(int i=0;i<n;i++){var b=map.blocks[i];metroModelled[i]=NativeCyberCityArt.Modelled(b);if(b!=null&&!metroModelled[i])metroStageMax[i]=NativeBlockLooks.StageMax(b,null,out metroStage1[i],out metroStage2[i]);}
        }
        // One frame of the art; false without it.
        private bool MetroArtRender(NativeArena arena)
        {
            metroArtDrawn=false;if(metroArt==null||!metroArt.Ready||metroKit==null)return false;
            NativeCyberCityArt.Pose(metroHazards,metroStates,metroKit.Maglev,metroPose);metroArtDrawn=metroArt.Render(arena,WorldEye,LowGraphics,metroPose);return metroArtDrawn;
        }
        // Whether the art stands in for this kit part this frame (its box is left out).
        private bool MetroArtHas(in NativeCyberCityPart part,NativePropBody prop)
        {
            if(!metroArtDrawn||NativeCyberCityArt.Kept(part)||part.Parent<0||part.Parent>=metroModelled.Length)return false;if(metroModelled[part.Parent])return true;
            int p=part.Parent;return !prop.Fragment&&NativeStageArt.Stage(prop.Health,metroStageMax[p],prop.Indestructible,metroStage1[p],metroStage2[p])<=NativeStageArt.Keep;
        }
    }
}
