using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The final killcam's slow-motion bodies (ArenaPresentation.SlowBodies): the fixed 1/60 s physics advances only when the
    // slowed clock passes a step, so the presented pose blends the pose before the newest step into the current one by the
    // clock's fraction of that step (the body is shown 1/60 s of replay time behind, continuous across steps). Off (Smooth
    // false) the body is presented exactly as before. A part that appeared after the remembered step is shown unblended.
    internal sealed partial class NativeCorpse
    {
        internal bool Smooth;private bool remembered;private float smoothT=1;private int rememberedMask;
        private const int SmoothWeapon=22;
        private readonly Matrix4x4[] before=new Matrix4x4[SmoothWeapon+1];
        private void Remember()
        {
            rememberedMask=0;foreach(var pair in Parts)if(pair.Key>=0&&pair.Key<SmoothWeapon&&pair.Value!=null){before[pair.Key]=pair.Value.transform.localToWorldMatrix;rememberedMask|=1<<pair.Key;}
            if(weapon!=null){before[SmoothWeapon]=weapon.transform.localToWorldMatrix;rememberedMask|=1<<SmoothWeapon;}remembered=true;
        }
        private void SmoothAt(float age)
        {
            smoothT=Smooth&&remembered?Mathf.Clamp01((age-clock)*60):1;
            if(Twin!=null){Twin.Smooth=Smooth;Twin.smoothT=Twin.remembered?smoothT:1;}
        }
        private Matrix4x4 Posed(int part,Matrix4x4 now)
        {
            if(!Smooth||smoothT>=1||part<0||part>SmoothWeapon||(rememberedMask&(1<<part))==0)return now;var a=before[part];float t=smoothT;
            return Matrix4x4.TRS(Vector3.Lerp(a.GetPosition(),now.GetPosition(),t),Quaternion.Slerp(a.rotation,now.rotation,t),now.lossyScale);
        }
    }
}
