using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int inspectSeat;private Vector2 inspectScroll,draftScroll;private bool BoardOpen=>arena!=null&&!menu&&(Input.GetKey(KeyCode.G)||Input.GetKey(KeyCode.Tab)&&!LockEligible||inspectSeat!=0);
        private void Inspect(float width,float height)
        {
            var seat=match.Seat(inspectSeat);if(seat==null)return;float w=Math.Min(570,width-40),x=(width-w)/2;GUI.Box(new Rect(x,100,w,height-180),"");
            GUILayout.BeginArea(new Rect(x+20,115,w-40,height-210));GUILayout.BeginHorizontal();GUILayout.Label(seat.name+" · "+content.Weapon(seat.spec).name,heading);if(GUILayout.Button("Close",button,GUILayout.Width(80)))inspectSeat=0;GUILayout.EndHorizontal();
            var p=arena.Pilots.Find(p=>p.Seat==seat);if(p!=null)GUILayout.Label("HP "+p.Hp.ToString("0")+" / "+p.Stats["hp"].ToString("0")+" · Damage ×"+p.Stats["damage"].ToString("0.##")+" · Speed "+p.Stats["speed"].ToString("0.##")+" · Attack rate ×"+p.Stats["attackRate"].ToString("0.##"),small);
            inspectScroll=GUILayout.BeginScrollView(inspectScroll);if(seat.chips.Count==0)GUILayout.Label("No Chips installed yet.",label);foreach(var pair in seat.chips.OrderBy(c=>content.Chip(c.Key).name)){var chip=content.Chip(pair.Key);GUILayout.Label(chip.name+" ×"+pair.Value+" · "+chip.rarity,label);GUILayout.Label(chip.description,small);}GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private static Color RarityColor(string rarity)=>rarity=="legendary"?new Color(1,.78f,.24f):rarity=="epic"?new Color(.8f,.45f,1):rarity=="rare"?new Color(.3f,.72f,1):new Color(.55f,.9f,.75f);
        private void DraftReveal(float width,float height)
        {
            if(string.IsNullOrEmpty(match.Chosen))return;var c=content.Chip(match.Chosen);GUI.Box(new Rect(width/2-280,height/2-150,560,290),"");GUI.color=RarityColor(c.rarity);GUI.Label(new Rect(width/2-250,height/2-125,500,55),c.name,heading);GUI.color=Color.white;GUI.Label(new Rect(width/2-250,height/2-62,500,100),c.description,label);GUI.Label(new Rect(width/2-250,height/2+58,500,40),"INSTALLED · "+match.Seat(match.DraftSeat)?.name,heading);
        }
    }
}
