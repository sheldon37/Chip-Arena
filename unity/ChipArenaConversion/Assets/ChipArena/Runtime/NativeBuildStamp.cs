using System;
using System.IO;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // The build stamp (Core/NativeBuildLabel.cs), drawn last on every screen: menus, lobby, match, intros, killcam, chip
        // selection. It sits in the bottom-right corner under the HUD's ability cards (NativeArcadeHud.Bottom leaves 93 units
        // free) and below the menu footer, small and faint. The installed build comes from the updater; otherwise the player
        // folder's own native-build.json is read once.
        private string buildStamp;
        private string BuildStamp()
        {
            if(buildStamp!=null)return buildStamp;
            NativeBuildIdentity build=Updater?.Installed;
            if(build==null)
                try{string file=Path.Combine(Application.dataPath,"..",NativeUpdateRules.IdentityFile);if(File.Exists(file))build=JsonUtility.FromJson<NativeBuildIdentity>(File.ReadAllText(file));}
                catch(Exception){build=null;}
            string text=NativeBuildLabel.Text(build,content?.sourceCommit);
            // Keep asking until the updater has started: it may still be about to read the installed identity.
            if(Updater!=null)buildStamp=text;
            return text;
        }
        private void BuildCorner()
        {
            string text=BuildStamp();float m=NativeBuildLabel.Margin,h=NativeBuildLabel.Height,w=U.Measure(text,NativeBuildLabel.Size,false,true).x+2;
            U.Text(new Rect(U.W-m-w,U.H-m-h,w,h),text,NativeBuildLabel.Size,NativeCyberTheme.Ink(.42f),TextAnchor.MiddleRight,mono:true,shadow:new Color(0,0,0,.55f));
        }
    }
}
