using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // DEV PREVIEW ONLY (Claude, 2026-10-14): the Expedition screens (NativeExpeditionPreviewScreen) over a real rules authority driven by
    // scripted scenarios (Core/NativeExpeditionPreview.cs). Shown in place of the main menu only when the player is started with
    // --ca-expedition-preview, in the style of --ca-dress-blocks; no menu button leads here and nothing about the Expedition mode
    // becomes playable. Viewing as the host sends the host's own commands; any other viewer is a guest, whose controls say the
    // guest command channel does not exist yet. Saves go to a temporary folder, deleted when the preview closes or a scenario loads.
    public sealed partial class ConversionGame
    {
        private static int expeditionPreviewFlag=-1;
        private static bool ExpeditionPreviewRequested
        {get{if(expeditionPreviewFlag<0)expeditionPreviewFlag=Array.IndexOf(Environment.GetCommandLineArgs(),"--ca-expedition-preview")>=0?1:0;return expeditionPreviewFlag==1;}}
        private NativeExpeditionPreview expedition;private NativeExpeditionPreviewScreen.State expeditionState;private NativeExpeditionHud.Controls expeditionControls;
        private bool expeditionClosed;private Dictionary<string,ContentChip> expeditionChips;

        private string ExpeditionPreviewFolder=>Path.Combine(Application.temporaryCachePath,"expedition-preview");
        private void ExpeditionPreviewWrite(ExpeditionSavepoint save)
        {
            new NativeExpeditionSaveStore(ExpeditionPreviewFolder,save.checkpoint.run,s=>JsonUtility.ToJson(s),json=>{try{return JsonUtility.FromJson<ExpeditionSavepoint>(json);}catch(ArgumentException){return null;}}).Write(save);
        }
        private void ExpeditionPreviewCleanup(){try{if(Directory.Exists(ExpeditionPreviewFolder))Directory.Delete(ExpeditionPreviewFolder,true);}catch(IOException){}catch(UnauthorizedAccessException){}}

        /// <summary>The preview frame; false (draw the normal menu) unless the dev flag is set and the preview is open.</summary>
        private bool ExpeditionPreviewGui(float width,float height)
        {
            if(!ExpeditionPreviewRequested||expeditionClosed||arena!=null||!menu)return false;
            float now=Time.unscaledTime;
            if(expedition==null)
            {
                ExpeditionPreviewCleanup();expedition=new NativeExpeditionPreview(ExpeditionPreviewWrite);expeditionState=new NativeExpeditionPreviewScreen.State{Loaded=ExpeditionPreviewCleanup};expeditionState.Load(expedition,0,now);
                expeditionChips=new Dictionary<string,ContentChip>();if(content?.chips!=null)foreach(var c in content.chips)if(c!=null&&c.id!=null)expeditionChips[c.id]=c;
                expeditionControls=new NativeExpeditionHud.Controls{ChipName=id=>expeditionChips.TryGetValue(id,out var c)&&!string.IsNullOrEmpty(c.name)?c.name:id,
                    ChipTone=id=>expeditionChips.TryGetValue(id,out var c)?NativeChipCard.Rarity(c.rarity):NativeCyberTheme.CyanSoft()};
            }
            NativeExpeditionPreviewScreen.Frame(expedition,expeditionState,expeditionControls,NativeUi.W,NativeUi.H,now,Event.current.type==EventType.Repaint?Time.unscaledDeltaTime:0,prefs.reducedEffects);
            if(expeditionState.ExitRequested){expeditionClosed=true;expedition=null;ExpeditionPreviewCleanup();}
            return true;
        }
    }
}
