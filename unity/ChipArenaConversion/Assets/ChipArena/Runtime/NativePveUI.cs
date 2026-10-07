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
            var v=arena.Pve;if(v==null)return;var p=arena.Local;
            string status=v.Stage=="boss"?"THE SIREN  ·  PHASE "+v.Phase:v.Stage=="wave"?"WAVE "+v.Wave+" / 3":v.Stage.ToUpperInvariant();
            GUI.Label(new Rect(width/2-220,22,440,50),status,heading);
            if(v.Stage=="boss"&&!v.Hidden){float hp=v.MaxHp>0?v.Boss.Hp/v.MaxHp:0;GUI.Box(new Rect(width/2-220,74,440,24),"");var old=GUI.color;GUI.color=new Color(.85f,.12f,.26f);GUI.DrawTexture(new Rect(width/2-218,76,436*Mathf.Clamp01(hp),20),Texture2D.whiteTexture);GUI.color=old;}
            if(v.Rows.TryGetValue(p.Seat.id,out var row))
            {
                GUI.Label(new Rect(26,295,330,70),"$"+row.Money+"  ·  "+row.Kills+" kills  ·  "+row.Picks.Count+" waiting Chips\nF · interact / collect / revive    C · Chips",small);
                if(!string.IsNullOrEmpty(v.Notice))GUI.Label(new Rect(width/2-220,height-120,440,50),v.Notice,label);
                if(p.Hp<=0)GUI.Label(new Rect(width/2-240,height/2+5,480,60),arena.InShaft(p)?"FALLING THROUGH THE VENT":row.SelfAt>=0?"Reviving in "+Math.Ceiling(row.SelfAt-arena.Time)+"s":"DOWNED · A teammate can revive you",label);
                if(row.Open!=null&&!v.Holding)
                {
                    float w=Mathf.Min(880,width-60),h=Mathf.Min(540,height-140);GUI.Box(new Rect((width-w)/2,(height-h)/2,w,h),"");
                    GUILayout.BeginArea(new Rect((width-w)/2+20,(height-h)/2+16,w-40,h-32));GUILayout.Label("Install a "+row.Open.Tier+" Chip",heading);GUILayout.Label("The run continues while you choose.",small);pveCards=GUILayout.BeginScrollView(pveCards);
                    int at=0,pickId=row.Open.Id;string chosen=null;foreach(string id in row.Open.Offers.ToArray())
                    {var c=arena.PveChip(id);if(at%2==0)GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width((w-70)/2));GUILayout.Label(c.name+" · "+c.rarity,label);GUILayout.Label(c.description,small);if(GUILayout.Button("Install",button))chosen=id;GUILayout.EndVertical();at++;if(at%2==0)GUILayout.EndHorizontal();}
                    if(at%2!=0)GUILayout.EndHorizontal();if(chosen!=null)ChoosePveChip(pickId,chosen);GUILayout.EndScrollView();if(GUILayout.Button("Choose later (C)",button))PveOpen(false);GUILayout.EndArea();
                }
            }
            if(v.Holding)
            {
                GUI.Box(new Rect(0,0,width,100),"");GUI.Box(new Rect(0,height-100,width,100),"");
                string title=v.Cinematic=="intro"?"SHE HAS BEEN WAITING":v.Cinematic=="victory"?"THE SIREN HAS FALLEN":v.Cinematic=="defeat"?"WARD ZERO CLAIMS ANOTHER":v.Reveal==2?"FIND THE POSSESSED":v.Reveal==3?"SHE RETURNS":v.Reveal==4?"NO RESTRAINT":"WARD ZERO";
                GUI.Label(new Rect(width/2-300,height-85,600,60),title,heading);
            }
            if(v.Stage=="ended")
            {GUI.Box(new Rect(width/2-240,height/2+70,480,140),"");int i=0;foreach(var r in v.Rows.Values.Where(r=>!r.Pilot.Seat.bot))GUI.Label(new Rect(width/2-220,height/2+82+i++*28,440,28),r.Pilot.Seat.name+" · "+r.Kills+" kills · $"+r.Earned+" earned · "+r.Revives+" revives",small);}
        }
    }
}
