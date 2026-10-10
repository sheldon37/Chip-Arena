using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The dev-siren cell's light on its baked stage (Core/NativeSirenIntroLight.cs, client/siren/intro.ts): NativeLighting.Intro
    /// hands the bulb and fill to the source shaders' stage block; here the bulb glow mesh follows the bulb's level (glowMaterial
    /// uIntensity) and the bulb's halo sprite, which the scene bank cannot hold, is drawn on the Siren effect layer. All of it
    /// reads the age the entrance is drawn at (live: NativeIntroClock, locked to the boss song as this machine plays it) and the
    /// song's beats, the same clock and beat table the music plays from.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        // The boss song's beats while the dev-siren entrance is staged (NativeArena.BossTrack, as NativeAudio cues the song), else null.
        private double[] introBeats;
        private NativeSceneClip sirenClip;private int sirenBulb=-1;private float sirenGlow=1;
        private void SirenCell(NativeSceneClip clip,string key,float age,Camera camera)
        {
            if(introBeats==null){sirenGlow=1;return;}
            if(clip!=sirenClip){sirenClip=clip;sirenBulb=-1;for(int t=0;t<clip.tracks&&sirenBulb<0;t++)if(NativeSirenIntroLight.AtBulb(clip.values,clip.frames,t)&&sourceModels.OpaqueBone(key,t))sirenBulb=t;}
            double level=NativeSirenIntroLight.Level(age,introBeats);sirenGlow=(float)NativeSirenIntroLight.Glow(level);
            float halo=(float)NativeSirenIntroLight.HaloLinear(age,introBeats);if(halo<=.0005f||camera==null)return;
            if(fxDraw==null)fxDraw=new NativeFxDraw();fxDraw.Clear();var tint=NativeArena.ColorOf(NativeSirenIntroLight.HaloHex);tint.a=halo;
            var at=new Vector3(-(float)NativeSirenIntroLight.BulbX(age),(float)NativeSirenIntroLight.BulbY,(float)NativeSirenIntroLight.BulbZ);
            fxDraw.Sprite(fxDraw.Effects,at,camera.transform.right,camera.transform.up,(float)NativeSirenIntroLight.HaloScale,tint,NativeFxDraw.Additive+NativeFxDraw.Authored);
            fxDraw.Draw(camera,null,age);
        }
    }
    public sealed partial class NativeSourceModels
    {
        /// <summary>True when model `id` draws an opaque section on joint or track `bone`.</summary>
        public bool OpaqueBone(string id,int bone)
        {
            if(!models.TryGetValue(id,out var parts))return false;
            foreach(var part in parts)if(part.Bone==bone&&part.Material!=null&&part.Material.renderQueue<(int)UnityEngine.Rendering.RenderQueue.Transparent)return true;return false;
        }
    }
}
