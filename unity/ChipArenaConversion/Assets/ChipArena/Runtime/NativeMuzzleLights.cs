using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The guns' muzzle light slots (Core/NativeMuzzleLight.cs; Resources/NativeMuzzleLight.cginc): the arena presentation's
    /// frame picks them (NativeMuzzleLightPicker, after every rig fired its flash) and Upload writes them as globals that
    /// NativeBlockLit, SourceLit, ZombieLit and NativeLit read on every map. Off empties them for an entrance, the draft and
    /// the rooftop, a map change and a presentation reset, so no flash outlives its arena. Linear colours (a gamma project's go
    /// to gamma, as NativeLighting.PickedLights writes the practical slots). Fixed arrays: nothing allocates; per frame, two
    /// four-element vector arrays and one float.
    /// </summary>
    public static class NativeMuzzleLights
    {
        private static readonly int PosId=Shader.PropertyToID("_MuzzleLightPos"),ColorId=Shader.PropertyToID("_MuzzleLightColor"),CountId=Shader.PropertyToID("_MuzzleLightCount");
        private static readonly Vector4[] pos=new Vector4[NativeMuzzleLight.Slots],color=new Vector4[NativeMuzzleLight.Slots];private static bool lit=true;
        /// <summary>The slots in use since the last upload.</summary>
        public static int Count{get;private set;}
        /// <summary>The picker's slots this frame.</summary>
        public static void Upload(NativeMuzzleLightPicker picker)
        {
            if(picker==null||picker.Count==0){Off();return;}
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var p=picker.Pos;var c=picker.Rgb;
            for(int k=0;k<NativeMuzzleLight.Slots;k++)
            {
                pos[k]=new Vector4(p[k*4],p[k*4+1],p[k*4+2],Mathf.Max(.0001f,p[k*4+3]));var rgb=new Color(c[k*4],c[k*4+1],c[k*4+2]);if(!linear)rgb=rgb.gamma;
                color[k]=new Vector4(rgb.r,rgb.g,rgb.b,c[k*4+3]);
            }
            Shader.SetGlobalVectorArray(PosId,pos);Shader.SetGlobalVectorArray(ColorId,color);Shader.SetGlobalFloat(CountId,picker.Count);Count=picker.Count;lit=true;
        }
        /// <summary>No muzzle light (the shaders skip the block).</summary>
        public static void Off()
        {
            if(!lit)return;lit=false;Count=0;
            for(int k=0;k<NativeMuzzleLight.Slots;k++){pos[k]=new Vector4(0,-10000,0,1);color[k]=new Vector4(0,0,0,NativeMuzzleLight.Near);}
            Shader.SetGlobalVectorArray(PosId,pos);Shader.SetGlobalVectorArray(ColorId,color);Shader.SetGlobalFloat(CountId,0);
        }
    }
}
