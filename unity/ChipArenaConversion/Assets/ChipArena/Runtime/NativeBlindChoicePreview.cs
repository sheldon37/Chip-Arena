using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // DEV ONLY, the Editor only (compiled out of every player): the N-choice Blind Pick screen on a made-up opening view, so its
    // layout and animation can be checked before Astra's rules deal real offers (NativeBlindChoices.RulesReady). The local
    // Editor tool (BoZo) calls StartBlindChoicePreview by reflection from the menu; nothing in the game calls it. The preview
    // plays its own host: a click or key updates the selection, a lock (or the 20 s running out) shows the locked-in step for
    // three seconds, then it closes. It sends nothing, writes no preference and never reaches a room or a match.
    public sealed partial class ConversionGame
    {
#if UNITY_EDITOR
        private string blindPreviewSession;private float blindPreviewAt=-1,blindPreviewDoneAt=-1;
        private void StartBlindChoicePreview(int n)
        {
            if(arena!=null||openingRun!=null||openingView!=null||server!=null||content?.weapons==null)return;
            var pool=new List<string>();foreach(var w in content.weapons)if(w!=null&&!w.devOnly)pool.Add(w.id);if(pool.Count<NativeBlindChoices.Min)return;
            // Distinct offers spread over the pool (it runs class by class), as a deal across the classes would look.
            n=NativeBlindChoices.Normalize(Math.Max(n,NativeBlindChoices.Min),pool.Count);var offers=new string[n];int step=Math.Max(1,pool.Count/n);
            for(int i=0;i<n;i++)offers[i]=pool[(i*step+1)%pool.Count];
            blindPreviewSession=Guid.NewGuid().ToString("N");blindPreviewAt=Time.unscaledTime;blindPreviewDoneAt=-1;
            AcceptOpeningView(new NativeNetMessage{type="opening-view",pickSession=blindPreviewSession,pick=(int)OpeningStep.Weapon,chip=prefs.weapon,id=1,team=4,remainingSeconds=20,pickCards=offers});
        }
        private bool BlindPreviewing(NativeNetMessage v)=>v!=null&&blindPreviewSession!=null&&v.pickSession==blindPreviewSession;
        private float BlindPreviewLeft=>Mathf.Max(0,20-(Time.unscaledTime-blindPreviewAt));
        // The preview's host for an opening-choice: the selection, or the lock to the locked-in step.
        private bool BlindPreviewChoice(NativeNetMessage m)
        {
            var v=openingView;if(!BlindPreviewing(v)||v.pick!=(int)OpeningStep.Weapon)return BlindPreviewing(v);
            if(m.ready){BlindPreviewLock(m.chip);return true;}
            AcceptOpeningView(new NativeNetMessage{type=v.type,pickSession=v.pickSession,pick=v.pick,chip=m.chip,id=v.id,team=v.team,remainingSeconds=BlindPreviewLeft,pickCards=v.pickCards});return true;
        }
        private void BlindPreviewLock(string id)
        {
            var v=openingView;blindPreviewDoneAt=Time.unscaledTime;
            AcceptOpeningView(new NativeNetMessage{type=v.type,pickSession=v.pickSession,pick=(int)OpeningStep.Ready,chip=id,id=Math.Min(v.team,v.id+1),team=v.team,remainingSeconds=0,pickCards=Array.Empty<string>()});
        }
        // The deadline locks the selection, else a random offer; the preview closes three seconds after its lock.
        private void BlindPreviewTick()
        {
            var v=openingView;if(!BlindPreviewing(v))return;
            if(v.pick==(int)OpeningStep.Weapon&&BlindPreviewLeft<=0){var offers=v.pickCards??Array.Empty<string>();string id=Array.IndexOf(offers,highlightedWeapon)>=0?highlightedWeapon:offers.Length>0?offers[UnityEngine.Random.Range(0,offers.Length)]:v.chip;BlindPreviewLock(id);}
            else if(blindPreviewDoneAt>=0&&Time.unscaledTime-blindPreviewDoneAt>3){openingView=null;blindPreviewSession=null;}
        }
#else
        private bool BlindPreviewing(NativeNetMessage v)=>false;
        private bool BlindPreviewChoice(NativeNetMessage m)=>false;
        private void BlindPreviewTick(){}
#endif
    }
}
