using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV voxel draw counter's panel (Core NativeVoxelDraws; docs/VOXEL-STYLE.md; docs/VOXEL-RETIREMENT.md V0): the last whole
    /// frame's voxel draws by kind, over every screen (a match, an expedition, the menus, the Deployment Station) while it is shown.
    /// Shown by the DEV panel's Camera tab ("Voxel draws", a read-only overlay), by --ca-voxel-counter in the Editor and Development
    /// builds, or by a capture. A kind above 0 that is neither allowed (the Siren, the primitive pieces) nor informational (the
    /// Standard sculpts in the voxel weapon frame) reads in the danger colour, with the package that moves it onto the model.
    /// Repaint only; it allocates nothing once warm but a count's text when that count changes. Presentation only.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private static int voxelCounterSwitch=-1;
        /// <summary>--ca-voxel-counter (the Editor and Development builds only): the counter on and shown from the start.</summary>
        private static bool VoxelCounterSwitch{get{if(voxelCounterSwitch<0)voxelCounterSwitch=Debug.isDebugBuild&&Array.IndexOf(Environment.GetCommandLineArgs(),"--ca-voxel-counter")>=0?1:0;return voxelCounterSwitch==1;}}
        private bool voxelCounterApplied;
        private readonly int[] voxelShown=new int[NativeVoxelDraws.Kinds];private readonly string[] voxelText=new string[NativeVoxelDraws.Kinds];
        private int voxelUnexpectedShown=-1;private string voxelUnexpectedText="";
        /// <summary>The DEV panel's toggle (DevView "overlay-voxel"): counting and the panel together; a fresh count each time.</summary>
        private static void VoxelCounterToggle(){bool on=!NativeVoxelDraws.Shown;NativeVoxelDraws.Reset();NativeVoxelDraws.On=NativeVoxelDraws.Shown=on;}
        private void VoxelDrawsGui()
        {
            if(!voxelCounterApplied){voxelCounterApplied=true;if(VoxelCounterSwitch){NativeVoxelDraws.Reset();NativeVoxelDraws.On=NativeVoxelDraws.Shown=true;}}
            if(!NativeVoxelDraws.Shown||Event.current.type!=EventType.Repaint)return;
            NativeVoxelDraws.Frame(Time.frameCount);
            const float w=300,row=14;float x=24,y=Mathf.Max(96,U.H*.12f);
            if(devLogOn&&arena!=null&&!menu)x+=Mathf.Min(460,U.W*.3f)+12;
            int unexpected=NativeVoxelDraws.Unexpected;if(unexpected!=voxelUnexpectedShown){voxelUnexpectedShown=unexpected;voxelUnexpectedText=unexpected==0?"0 UNEXPECTED":unexpected+" UNEXPECTED";}
            var r=new Rect(x,y,w,40+row*(NativeVoxelDraws.Kinds-1));Color accent=unexpected>0?C.Danger():C.Cyan();
            H.Panel(r,1,accent,NativeHudCyber.TagCut,H.Corners.Diagonal,true,.86f);
            U.Text(new Rect(r.x+12,r.y+6,170,14),"VOXEL DRAWS · LAST FRAME",9,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            U.Text(new Rect(r.xMax-118,r.y+6,106,14),voxelUnexpectedText,9,unexpected>0?C.Danger():C.Good(),TextAnchor.MiddleRight,bold:true,mono:true);
            float ly=r.y+26;
            for(int k=1;k<NativeVoxelDraws.Kinds;k++)
            {
                var kind=(NativeVoxelDraws.Kind)k;int n=NativeVoxelDraws.Last(kind);
                if(voxelText[k]==null||n!=voxelShown[k]){voxelShown[k]=n;voxelText[k]=n.ToString(System.Globalization.CultureInfo.InvariantCulture);}
                bool allowed=NativeVoxelDraws.Allowed(kind),info=NativeVoxelDraws.Informational(kind);
                Color tone=n==0?C.Muted(.75f):allowed?C.Warn(.95f):info?C.CyanSoft(.9f):C.Danger();
                U.Text(new Rect(r.x+12,ly,150,row),NativeVoxelDraws.Labels[k],9,tone,TextAnchor.MiddleLeft,mono:true);
                U.Text(new Rect(r.x+150,ly,80,row),allowed?"allowed":info?"info":NativeVoxelDraws.Packages[k],8,C.Muted(.6f),TextAnchor.MiddleLeft,mono:true);
                U.Text(new Rect(r.xMax-62,ly,50,row),voxelText[k],10,n==0?C.Muted(.75f):C.Ink(),TextAnchor.MiddleRight,bold:n>0,mono:true);
                ly+=row;
            }
        }
    }
}
