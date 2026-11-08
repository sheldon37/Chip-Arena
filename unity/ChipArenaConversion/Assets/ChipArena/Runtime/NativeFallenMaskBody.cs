using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // NOCTIEL's mask as Core NativeFallenMask builds it (the owner, 2026-11-08: "revise the face (Maybe just make it blink) so remove the little eyes
    // and eyebrows"), drawn on the head in place of the gear bank's mask, split mask and eye slits: the bank's face without its brows, and eyes that
    // blink. Built in the skeleton's bind space and taken onto the head by its bind pose, as the hood is. The eyes are rebuilt only while they move.
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The whole mask and phase 3's split one, each at both tiers; the eyes as last built.</summary>
        private readonly Mesh[,] fbMask=new Mesh[2,2];private Mesh fbMaskEyes;private bool fbMaskEyesGlowOnly;private float fbMaskEyesOpen=-1;private int fbMaskEyesTier=-1;
        private readonly NativeFallenKit fbMaskEyeKit=new NativeFallenKit();private Matrix4x4 fbMaskBind=Matrix4x4.identity;private int fbMaskGroup=-1;
        /// <summary>The bank's mask pieces stay hidden while the built one is there.</summary>
        private bool FallenMaskOn=>fbMask[0,0]!=null;
        private static bool FbMaskPart(string name)=>name=="mask"||name=="mask-split"||name=="mask-eyes";
        private void FallenMaskBuild()
        {
            FallenMaskDispose();
            if(!fbBind.TryGetValue("head",out fbMaskBind)||!fbGroupIndex.TryGetValue("head",out fbMaskGroup)){fbMaskGroup=-1;Debug.LogWarning("NOCTIEL: no head for the mask; the bank's stays");return;}
            try{for(int s=0;s<2;s++)for(int t=0;t<2;t++)fbMask[s,t]=FbKitMesh(NativeFallenMask.Build(s==1,t),fbMaskBind,s==1?"NOCTIEL mask split":"NOCTIEL mask",out _);}
            catch(Exception e){Debug.LogWarning("NOCTIEL's mask: "+e.Message);FallenMaskDispose();return;}
            // Where it sits against the bank's (its middle from its bounds: the bank's meshes aren't readable).
            foreach(var part in fbParts)if(part.Name=="mask"&&part.Mesh[0]!=null)Debug.Log("NOCTIEL: the mask built, its middle "+((fbMask[0,0].bounds.center-part.Mesh[0].bounds.center).magnitude*1000).ToString("0.0")+" mm from the bank's, its size "+fbMask[0,0].bounds.size.ToString("F3")+" to the bank's "+part.Mesh[0].bounds.size.ToString("F3"));
        }
        private void FallenMaskDispose()
        {
            for(int s=0;s<2;s++)for(int t=0;t<2;t++){if(fbMask[s,t]!=null)CapturedAnimation.Release(fbMask[s,t]);fbMask[s,t]=null;}
            if(fbMaskEyes!=null)CapturedAnimation.Release(fbMaskEyes);fbMaskEyes=null;fbMaskEyesOpen=-1;fbMaskEyesTier=-1;
        }
        /// <summary>The mask by phase (whole in 1 and 2, split in 3, none in 4) and in phases 1 and 2 its eyes `open` (Core NativeFallenMask.Blink), on the head and in the water.</summary>
        private void FallenMaskDraw(int phase,float inner,float flash,Matrix4x4 mirror,int tier,float now,float open)
        {
            if(phase>3||fbMaskGroup<0)return;var world=fbWorld[fbMaskGroup];var mesh=fbMask[phase==3?1:0,tier]??fbMask[phase==3?1:0,0];
            if(mesh!=null){FbDrawKit(mesh,false,world,0,flash,0,null);FbDrawKit(mesh,false,mirror*world,0,flash,1,null);}
            if(phase==3)return;
            if(fbMaskEyes==null||tier!=fbMaskEyesTier||Mathf.Abs(open-fbMaskEyesOpen)>.002f)
            {
                fbMaskEyeKit.Clear();NativeFallenMask.Eyes(fbMaskEyeKit,open,tier);fbMaskEyeKit.Finish();
                fbMaskEyes=FbKitMesh(fbMaskEyeKit,fbMaskBind,"NOCTIEL eyes",out fbMaskEyesGlowOnly,fbMaskEyes);fbMaskEyesOpen=open;fbMaskEyesTier=tier;
            }
            float glow=inner*(.9f+.1f*Mathf.Sin(now*1.3f));FbDrawKit(fbMaskEyes,fbMaskEyesGlowOnly,world,glow,flash,0,null);FbDrawKit(fbMaskEyes,fbMaskEyesGlowOnly,mirror*world,glow,flash,1,null);
        }
    }
}
