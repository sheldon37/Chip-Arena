#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV preview of Belvedere Camp in the Developer walk (Settings · Developer · Walk Meridian greybox; Editor and development
    /// builds only, never a live run): the walk's layout and deal, whether the preview is on (C), how many rescued survivors have
    /// joined (U: 0-5, one per cleared instance; a run clears three of the five) and the clearance overlay (K). Runtime NativeCampDraw draws it; the walk's own map id
    /// is the only map it answers to.
    /// </summary>
    public static class NativeCampWalk
    {
        public static string Map;public static uint Seed;public static string[] Deal;public static int Plan;public static int Rescued=5;public static bool Preview=true;
        public static bool Active(string mapId)=>mapId!=null&&mapId==Map;
    }

    public sealed partial class ConversionGame
    {
        /// <summary>The walk opened (the one line in OpenCityWalk): the camp follows its layout and deal.</summary>
        private void CampWalkOpen(NativeLayeredCityWalk walk,int plan,uint seed,string[] deal)
        {NativeCampWalk.Map=walk?.Map?.id;NativeCampWalk.Seed=seed;NativeCampWalk.Plan=plan;NativeCampWalk.Deal=deal;}
        /// <summary>The walk closed (the one line in CloseCityWalk).</summary>
        private void CampWalkClose()=>NativeCampWalk.Map=null;
        /// <summary>The walk's keys (the one line in LayeredCityWalkKeys): C the preview, U the rescued, K the clearances.</summary>
        private bool CampWalkKeys()
        {
            if(NativeCampWalk.Map==null)return false;
            if(Input.GetKeyDown(KeyCode.C)){NativeCampWalk.Preview=!NativeCampWalk.Preview;return true;}
            if(Input.GetKeyDown(KeyCode.U)){NativeCampWalk.Rescued=(NativeCampWalk.Rescued+1)%6;return true;}
            if(Input.GetKeyDown(KeyCode.K)){NativeCampDraw.Clearances=!NativeCampDraw.Clearances;return true;}
            return false;
        }
        /// <summary>The walk panel's line (the one line in LayeredCityWalkGui's panel).</summary>
        private void CampWalkLabel()=>GUILayout.Label("C   Camp preview "+(NativeCampWalk.Preview?"on":"off")+"     U   Rescued "+NativeCampWalk.Rescued+"     K   Clearances "+(NativeCampDraw.Clearances?"on":"off"));
        /// <summary>While the preview shows, the banner that says it is not gameplay (the one line after the walk's panel).</summary>
        private void CampWalkBanner(float width)
        {
            if(NativeCampWalk.Map==null||!NativeCampWalk.Preview||Event.current.type!=EventType.Repaint)return;
            const string label="DEV PREVIEW · BELVEDERE CAMP (W5 PLAN) · NOT GAMEPLAY";var size=U.Measure(label,11,true,true,.12f);
            var r=new Rect(width/2-size.x/2-14,64,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,label,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
        }
    }
}
#endif
