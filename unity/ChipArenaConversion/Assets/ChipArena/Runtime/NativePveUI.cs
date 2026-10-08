using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool PvePickOpen=>arena?.Pve!=null&&arena.Pve.Rows.TryGetValue(arena.Local.Seat.id,out var row)&&row.Open!=null;
        private Vector2 pveCards;
        private void PveHud(float width,float height)
        {
            var v=arena.Pve;if(v==null)return;var p=arena.Local;PveBanner();
            string status=v.Stage=="boss"?"THE SIREN  ·  PHASE "+v.Phase:v.Stage=="wave"?"WAVE "+v.Wave+" / 3":v.Stage.ToUpperInvariant();
            GUI.Label(new Rect(width/2-220,22,440,50),status,heading);
            if(v.Stage=="boss"&&!v.Hidden){float hp=v.MaxHp>0?v.Boss.Hp/v.MaxHp:0;GUI.Box(new Rect(width/2-220,74,440,24),"");var old=GUI.color;GUI.color=new Color(.85f,.12f,.26f);GUI.DrawTexture(new Rect(width/2-218,76,436*Mathf.Clamp01(hp),20),Texture2D.whiteTexture);GUI.color=old;}
            if(v.Rows.TryGetValue(p.Seat.id,out var row))
            {
                GUI.Label(new Rect(26,295,330,70),"$"+row.Money+"  ·  "+row.Kills+" kills  ·  "+row.Picks.Count+" waiting Chips\nF · interact / collect / revive    C · Chips",small);
                if(p.Hp<=0)GUI.Label(new Rect(width/2-240,height/2+5,480,60),arena.InShaft(p)?"FALLING THROUGH THE VENT":row.SelfAt>=0?"Reviving in "+Math.Ceiling(row.SelfAt-arena.Time)+"s":"DOWNED · A teammate can revive you",label);
            }
            if(v.Stage=="ended")
            {GUI.Box(new Rect(width/2-240,height/2+70,480,140),"");int i=0;foreach(var r in v.Rows.Values.Where(r=>!r.Pilot.Seat.bot))GUI.Label(new Rect(width/2-220,height/2+82+i++*28,440,28),r.Pilot.Seat.name+" · "+r.Kills+" kills · $"+r.Earned+" earned · "+r.Revives+" revives",small);}
        }
    }
}
