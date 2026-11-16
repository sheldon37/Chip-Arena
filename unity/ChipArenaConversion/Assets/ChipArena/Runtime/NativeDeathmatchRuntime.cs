using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private void RespawnDeathmatchPilots()
        {
            foreach (var p in Pilots)
            {
                var seat = p.Seat;
                if (!seat.connected) continue;
                if (seat.bot && seat.offers.Count > 0)
                    InstallDeathmatchChip(seat.id, seat.pickTicket, seat.offers[0]);
                if (!Match.TryDeathmatchRespawn(seat)) continue;
                int index = Match.Seats.IndexOf(seat);
                var spawn = NativeMapRules.Spawn(Content, Map, Match.Seats, index, Match.Teams);
                RevivePve(p, Vec(spawn.pos), true);
                p.Death = null;
                p.LastAttacker = null;
                p.Yaw = spawn.yaw;
                p.Mana = (float)p.Stats["maxMana"];
                p.Stamina = (float)p.Stats["maxStamina"];
                p.Ammo = Magazine(p);
                p.ECharges = NativeAbilityRules.Charges(p.Stats);
            }
        }

#if UNITY_EDITOR
        // Called in the native builder with two isolated arenas, including replica serialization.
        public void ValidateDeathmatchReplica(NativeArena guest)
        {
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,deathmatchTarget=4});
            Match.ComebackChips=true;
            var killer=Pilots[0];var victim=Pilots[1];
            void Check(bool ok,string name){if(!ok)throw new Exception("Deathmatch native gate: "+name);}
            void Deliver()
            {
                var wire=NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=1,frame=NetworkFrame()});
                var frame=NativeNetCodec.Decode(wire).frame;
                Check(frame.Valid(Content),"valid serialized match");
                guest.ApplyFrame(frame,victim.Seat.id);
            }
            for(int i=1;i<=4;i++)
            {
                Damage(victim,killer,victim.Hp+1000,Vector3.zero,0,false,forced:true);
                Deliver();
                Check(guest.Match.DeathmatchTarget==4&&guest.Match.Seat(killer.Seat.id).score==i,"replicated target and score");
                if(i==4)break;
                for(int t=0;t<12;t++)Match.Tick(.25);
                RespawnDeathmatchPilots();Deliver();
                Check(guest.Local.Hp>0&&guest.Local.Seat.alive&&guest.Local.Death==null,"respawn clears replica corpse");
                if(i==3)
                {
                    var seat=guest.Local.Seat;
                    Check(seat.offers.Count>0&&InstallDeathmatchChip(seat.id,seat.pickTicket,seat.offers[0]),"deferred chip");
                    Deliver();Check(guest.Local.Seat.chips.Count==1,"replica receives chip");
                }
            }
            Check(guest.Match.Phase==NativePhase.Result&&guest.Match.FinalVictim==victim.Seat.id,"final replay victim");
        }
#endif

        public bool InstallDeathmatchChip(int seatId, int ticket, string chip)
        {
            var p = Pilots.Find(candidate => candidate.Seat.id == seatId);
            if (p == null || !Match.TakeComebackChip(p.Seat, ticket, chip)) return false;
            RefreshLiveStats(p);
            Emit(EventKind.Install, p, seatId, p.Body.position, p.Seat.chips[chip], chip);
            return true;
        }
    }

    public sealed partial class ConversionGame
    {
        private bool deathPickOpen, deathPickWasDead;
        private NativeMatch deathPickMatch;
        private int deathPickTicket = -1;

        private void DeathmatchPickKeys()
        {
            var seat = arena?.Local?.Seat;
            if (match != deathPickMatch || match?.Phase != NativePhase.Fight ||
                match.DeathmatchTarget <= 0 || seat == null || seat.offers.Count == 0)
            {
                deathPickOpen = false;
                deathPickTicket = -1;
                deathPickMatch = match;
                return;
            }
            if (deathPickWasDead && seat.alive) deathPickOpen=false;
            deathPickWasDead=!seat.alive;
            if (deathPickTicket != seat.pickTicket)
            {
                deathPickOpen = !seat.alive;
                deathPickTicket = seat.pickTicket;
            }
            if (!menu && (Input.GetKeyDown(KeyCode.C) || PadDown(PadAction.Chips))) deathPickOpen = !deathPickOpen;
        }

        private bool DeathmatchPickGui(float w, float h)
        {
            if (menu || arena?.Local == null || match.DeathmatchTarget <= 0 || match.Phase != NativePhase.Fight) return false;
            var seat = arena.Local.Seat;
            if (!seat.alive && seat.respawnAt >= 0)
                GUI.Label(new Rect(w/2-110,h*.23f,220,36),"Respawn in "+Mathf.Max(0,Mathf.CeilToInt((float)(seat.respawnAt-match.Time))),label);
            if (seat.offers.Count == 0) return false;
            if (!deathPickOpen)
            {
                GUI.Label(new Rect(22,h-145,420,36),"Comeback chip ready · Open chips (C)",label);
                return false;
            }
            DrawPickPanel(w,h,"Comeback chip","Respawn continues while you choose.",seat.offers.ToArray(),false,null,id=>{
                if (server == null) arena.InstallDeathmatchChip(seat.id,seat.pickTicket,id);
                else SendServer(new NativeNetMessage{type="dm-chip",chip=id,pick=seat.pickTicket});
                deathPickOpen=false;
            },()=>deathPickOpen=false);
            return true;
        }

        private void DeathmatchScores(float x,float y,float w,List<NativeSeat> sides)
        {
            NativeHudChrome.Panel(new Rect(x,y,w,46+28*sides.Count));
            GUI.Label(new Rect(x+14,y+6,w-28,28),"DEATHMATCH · FIRST TO "+match.DeathmatchTarget,label);
            foreach(var side in sides.OrderByDescending(s=>s.score))
            {
                y+=28;
                GUI.Label(new Rect(x+14,y+10,w-28,26),(match.Teams?NativeMatch.TeamNames[side.team]:side.name)+"   "+side.score,label);
            }
        }
    }
}
