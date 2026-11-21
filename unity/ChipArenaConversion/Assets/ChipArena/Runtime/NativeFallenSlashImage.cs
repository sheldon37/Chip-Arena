using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The cut across the finished frame: runs after the bloom (added later on the same camera), only while a Slash is on. It keeps a frozen frame of its own.
    [DisallowMultipleComponent]
    public sealed class NativeFallenSlashImage:MonoBehaviour
    {
        public Material Material;public NativeFallenSlash.State State;public float Angle,Seed;private byte kind;private RenderTexture frozen;private bool captured;
        private static readonly int FrozenId=Shader.PropertyToID("_FbFrozen"),LineId=Shader.PropertyToID("_FbLine"),CutId=Shader.PropertyToID("_FbCut"),Cut2Id=Shader.PropertyToID("_FbCut2"),SeedId=Shader.PropertyToID("_FbSeed");
        public void Arm(byte k){kind=k;captured=false;}
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(Material==null){Graphics.Blit(source,destination);return;}
            if(frozen==null||frozen.width!=source.width||frozen.height!=source.height){if(frozen!=null)frozen.Release();frozen=new RenderTexture(source.width,source.height,0,source.format){name="The Slash frame",hideFlags=HideFlags.HideAndDontSave};frozen.Create();captured=false;}
            if(!captured)
            {
                if(kind==NativeFallenSlash.Reveal){var was=RenderTexture.active;RenderTexture.active=frozen;GL.Clear(false,true,Color.black);RenderTexture.active=was;captured=true;}
                else if(State.Freeze){Graphics.Blit(source,frozen);captured=true;}
            }
            float a=Angle*Mathf.Deg2Rad;Material.SetTexture(FrozenId,frozen);Material.SetVector(LineId,new Vector4(Mathf.Cos(a),Mathf.Sin(a),(float)source.width/source.height,captured?1:0));
            Material.SetVector(CutId,new Vector4(State.Draw,State.Open,State.Gap,State.Width));Material.SetVector(Cut2Id,new Vector4(State.Flash,State.Black,State.Mono,State.Ink));Material.SetFloat(SeedId,Seed);
            Graphics.Blit(source,destination,Material);
        }
        private void OnDestroy(){if(frozen!=null){frozen.Release();Destroy(frozen);frozen=null;}}
    }
}
