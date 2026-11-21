using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private void FallenBattleHud(float width,float height)
        {
            var z=arena.ZoneBoss;var f=z.fallen;float W=NativeUi.W;
            var row=f.hush.FirstOrDefault(s=>s.id==arena.LocalId);
            if(row!=null){NativeUi.Text(new Rect(24,24,340,28),"LIVES  "+row.lives+" / 5",20,Color.white,TextAnchor.MiddleLeft,bold:true);NativeUi.Text(new Rect(24,55,380,22),row.lives==0?"WAITING FOR A TEAMMATE":row.respawnAt>=0?"RETURNING IN "+Math.Max(0,(int)Math.Ceiling(row.respawnAt-arena.Time))+"s":"HOLD INTERACT TO SHARE A LIFE",12,Color.white,TextAnchor.MiddleLeft);}
            if(f.draftAt>=0&&arena.Time-f.draftAt<NativeFallenRules.RevealSeconds){FallenChipScreen(width,height,arena.Time-f.draftAt,z.phase);return;}
            if(f.draftAt>=0){NativeUi.Text(new Rect(W/2-360,12,720,28),"NOCTIEL: "+string.Join(" · ",f.chipOffer.Select(id=>NativeFallenBoss.Chips.First(c=>c.Id==id).Name)),14,Color.white,TextAnchor.MiddleCenter);return;}
            // A Boss Battle's chips the countdown left: the pending tag with C, and the prompt as the fight starts (NativeBossPicksClock.cs).
            if(arena.Pve.Rows.TryGetValue(arena.LocalId,out var pending)){if(pending.Picks.Count!=pvePending){if(pending.Picks.Count>pvePending)pvePendingAt=Time.unscaledTime;pvePending=pending.Picks.Count;}if(pending.Picks.Count>0&&!PvePickOpen)PveChipsTag(W,NativeUi.H,pending.Picks.Count,Time.unscaledTime-pvePendingAt);}
            BossLeftoverHud(W,NativeUi.H);
            var m=fallenBanner;m.Name=NativeFallenBoss.Name;m.Area=NativeFallenBoss.PhaseNames[z.phase-1];m.PlateCount=1;m.PlateNames[0]=m.Name;m.PlateDown[0]=z.deathAt>=0;m.PlateHealth[0]=arena.Pve.MaxHp>0?arena.Pve.Boss.Hp/arena.Pve.MaxHp:0;m.PlatePercent[0]=null;m.PlateAge[0]=4;NativeExpeditionStatesHud.BossPlates(m,width,100,Time.unscaledTime,false);
        }
    }
}
