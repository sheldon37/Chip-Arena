using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private NativeOpeningDraft openingRun;
        private NativeNetMessage openingView;
        private Action openingLaunch;
        private bool applyingOpening;
        private double openingRevealUntil;
        private float nextOpeningView;
        private string highlightedWeapon;
        private Vector2 openingScroll;
        private bool BeginOpening(Action launch)
        {
            if(applyingOpening || server!=null || trainingRoom || trainingWanted || sandboxWanted)return false;
            if(openingRun!=null)return true;
            bool chips=prefs.deathmatch&&!prefs.pve&&prefs.deathmatchChips!=1;
            if(!prefs.blindPick&&(prefs.pve||!prefs.deathmatch))return false;
            if(peers.Any(p=>p.Seat!=null&&p.Connection?.Closed==false&&p.LoadoutProtocol<1))
            {
                networkStatus="All pilots need the Deathmatch and Blind Pick update to use these rules.";
                if(!dedicated)Notice(networkStatus);
                return true;
            }
            if(!prefs.blindPick&&!chips)return false;
            if(Hosting&&!GuestsReady)return true;
            var seats=new List<NativeSeat>();
            if(!dedicated)seats.Add(SanitizedSeat(1,Profile(),account.Token));
            seats.AddRange(peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).Select(p=>p.Seat));
            if(!prefs.pve || !prefs.expedition&&!NativeBossBattle.Zone(bossBattlePick))
            {
                if(!prefs.pve&&!Online&&!dedicated)prefs.bots=Math.Max(1,prefs.bots);
                seats.AddRange(LobbyBots(seats.Count));
            }
            var rng=new SourceRandom(NativeMatch.NewSeed());
            openingRun=new NativeOpeningDraft(seats,prefs.blindPick,chips,Time.unscaledTimeAsDouble,
                p=>NativeContentRules.Offers(content,p.Weapon,new Dictionary<string,int>(),rng.Next,prefs.choices,
                    NativeArena.ImplementedChip,abilityLoadout:p.Seat.weaponTechniques??"").ToArray(),OpeningWeaponAllowed);
            openingLaunch=launch;
            nextOpeningView=0;
            openingRevealUntil=0;
            OpeningTick();
            return true;
        }

        private bool OpeningWeaponAllowed(NativeSeat seat,string id)
        {
            if(id==null||id.Length>64)return false;
            var w=content.weapons.FirstOrDefault(candidate=>candidate.id==id);
            return w!=null&&(!prefs.pve||id!="siren")&&(!w.devOnly||seat.access.Allows("weapon:"+id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        private void WithOpening(Action launch)
        {
            applyingOpening=true;
            try{launch();}
            finally{ClearOpening();}
        }

        private void ClearOpening()
        {
            openingRun=null;openingView=null;openingLaunch=null;applyingOpening=false;
            highlightedWeapon=null;openingRevealUntil=0;openingScroll=Vector2.zero;
        }

        private void ApplyOpeningWeapon(NativeSeat seat)
        {
            if(applyingOpening && openingRun.Pilots.TryGetValue(seat.id,out var p) && OpeningWeaponAllowed(seat,p.Weapon))
                seat.spec=p.Weapon;
        }

        private void ApplyOpeningChip(NativeSeat seat)
        {
            if(applyingOpening && openingRun.Pilots.TryGetValue(seat.id,out var p) &&
                seat.spec==p.Weapon && p.Chip!=null)seat.chips[p.Chip]=1;
        }

        private void OpeningTick()
        {
            if(openingRun==null||openingLaunch==null)return;
            var now=Time.unscaledTimeAsDouble;
            openingRun.Tick(now);
            if(openingRun.Ready&&openingRevealUntil==0){openingRevealUntil=now+2;nextOpeningView=0;}
            if(openingRevealUntil>0&&now>=openingRevealUntil)
            {
                var launch=openingLaunch;openingLaunch=null;openingView=null;
                foreach(var peer in peers)if(peer.Seat!=null&&peer.Connection?.Closed==false)
                    peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="opening-end",pickSession=openingRun.Session}));
                launch();
                return;
            }
            if(Time.unscaledTime<nextOpeningView)return;
            nextOpeningView=Time.unscaledTime+.35f;
            int ready=openingRun.Pilots.Values.Count(p=>p.Step==OpeningStep.Ready);
            foreach(var p in openingRun.Pilots.Values)
            {
                if(p.Seat.bot)continue;
                var step=openingRevealUntil>0?OpeningStep.Reveal:p.Step;
                var view=new NativeNetMessage{type="opening-view",pickSession=openingRun.Session,pick=(int)step,
                    chip=p.Weapon,id=ready,team=openingRun.Pilots.Count,remainingSeconds=(float)Math.Max(0,p.Deadline-now),
                    pickCards=step==OpeningStep.Chip?p.Offers:step==OpeningStep.Reveal?
                        openingRun.Pilots.Values.Select(other=>other.Seat.name+"   ·   "+content.Weapon(other.Weapon).name).ToArray():Array.Empty<string>()};
                if(!dedicated&&p.Seat.id==1)AcceptOpeningView(view);
                else peers.Find(peer=>peer.Seat?.id==p.Seat.id)?.Connection?.Send(NativeNetCodec.Encode(view));
            }
        }

        private void AcceptOpeningView(NativeNetMessage view)
        {
            if(openingView?.pickSession!=view.pickSession){highlightedWeapon=view.chip;openingScroll=Vector2.zero;}
            if(openingView?.pick!=view.pick)
            {
                openingScroll=Vector2.zero;
                if(view.pick==(int)OpeningStep.Reveal&&prefs.weapon!=view.chip&&content.weapons.Any(w=>w.id==view.chip&&AllowedWeapon(w)))
                {prefs.weapon=view.chip;Save();}
            }
            openingView=view;
        }

        private bool OpeningClientMessage(NativeNetMessage m)
        {
            if(m.type=="opening-end"){if(openingView?.pickSession==m.pickSession)openingView=null;return true;}
            if(m.type!="opening-view")return false;
            if(m.pick>=0&&m.pick<=3&&m.id>=0&&m.team<=8&&m.id<=m.team&&m.pickSession?.Length==32&&m.pickCards?.Length<=12)
                AcceptOpeningView(m);
            return true;
        }

        private bool OpeningCommand(NativePeer peer,NativeNetMessage m)
        {
            if(m.type!="opening-choice")return openingRun!=null&&(m.type=="profile"||m.type=="ready"||m.type=="team");
            if(openingRun!=null&&openingLaunch!=null&&peer.Seat!=null&&m.pickSession==openingRun.Session&&
                m.chip?.Length<=64&&openingRun.Submit(peer.Seat.id,(OpeningStep)m.pick,m.chip,m.ready,Time.unscaledTimeAsDouble))
                nextOpeningView=0;
            return true;
        }

        private void SendOpeningChoice(string id,bool confirm)
        {
            var m=new NativeNetMessage{type="opening-choice",pickSession=openingView.pickSession,pick=openingView.pick,chip=id,ready=confirm};
            if(server!=null)SendServer(m);
            else if(openingRun!=null&&openingRun.Submit(1,(OpeningStep)m.pick,id,confirm,Time.unscaledTimeAsDouble))nextOpeningView=0;
        }

        private bool OpeningGui(float w,float h)
        {
            if(openingView==null)return false;
            var v=openingView;var step=(OpeningStep)v.pick;
            string status=v.id+" / "+v.team+" locked in";
            if(step==OpeningStep.Reveal || step==OpeningStep.Ready)
            {
                GUILayout.BeginArea(new Rect(w*.25f,h*.24f,w*.5f,h*.52f),GUI.skin.box);
                GUILayout.Label(step==OpeningStep.Reveal?"Weapons revealed":"Locked in",label);
                GUILayout.Label(status,small);
                if(step==OpeningStep.Reveal)foreach(var row in v.pickCards)GUILayout.Label(row,label);
                GUILayout.EndArea();return true;
            }
            bool weapon=step==OpeningStep.Weapon;
            var ids=weapon?content.weapons.Where(AllowedWeapon).Select(item=>item.id).ToArray():v.pickCards;
            DrawPickPanel(w,h,weapon?"Pick your weapon":"Pick your starting chip",
                status+"   ·   "+Mathf.CeilToInt(v.remainingSeconds)+" s",ids,weapon,highlightedWeapon,id=>{
                    if(weapon){highlightedWeapon=id;SendOpeningChoice(id,false);}
                    else SendOpeningChoice(id,true);
                },weapon?(Action)(()=>SendOpeningChoice(highlightedWeapon,true)):null);
            return true;
        }

        private void DrawPickPanel(float w,float h,string title,string hint,string[] ids,bool weapons,
            string selected,Action<string> choose,Action footer)
        {
            GUILayout.BeginArea(new Rect(w*.14f,h*.13f,w*.72f,h*.74f),GUI.skin.box);
            GUILayout.Label(title,label);GUILayout.Label(hint,small);
            openingScroll=GUILayout.BeginScrollView(openingScroll);
            for(int i=0;i<ids.Length;i+=3)
            {
                GUILayout.BeginHorizontal();
                for(int j=i;j<Math.Min(i+3,ids.Length);j++)
                {
                    string id=ids[j],name=weapons?content.Weapon(id).name:content.Chip(id).name;
                    string caption=(selected==id?"✓  ":"")+name+"\n"+(weapons?content.Weapon(id).description:content.Chip(id).description);
                    if(NativeControllerPointer.Button(caption,button,GUILayout.Width(w*.22f),GUILayout.MinHeight(78)))choose(id);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if(footer!=null&&NativeControllerPointer.Button(weapons?"Lock in weapon":"Choose later",button))footer();
            GUILayout.EndArea();
        }
    }
}
