#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateFallenBattle(Action<NativeGameFrame> replicaCheck,bool wipe)
        {
            int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("NOCTIEL native: "+why);}
            var z=ZoneBoss;var f=z.fallen;var p=Members().First();var m=zoneBossActor;
            Check(z.archetype==NativeFallenBoss.Archetype&&z.Valid()&&Pve.HoldDuration==NativeFallenPilots.CutsceneEnd,"intro and initial record");
            foreach(var pilot in Members())Check(ExpeditionSupported(Vec(pilot.Body.position)-new V3(0,NativeLocomotionRules.HalfHeight,0),NativeLocomotionRules.Radius,NativeLocomotionRules.HalfHeight*2),"supported landing");
            void Wire(){var frame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=9,frame=NetworkFrame()})).frame;Check(frame.Valid(Content),"real codec frame valid at phase "+z.phase);replicaCheck(frame);}
            Wire();PveHoldTick(Pve.HoldDuration+.01f);Check(!Pve.Holding&&z.action==null,"intro releases");
            var life=f.hush.First(s=>s.id==p.Seat.id);p.ShieldUntil=0;bossHits.Clear();FallenHurt(p,0,true);Check(p.Hp==0&&life.lives==4&&life.respawnAt>=0,"OHK consumes one life");
            Match.SynchronizeExpeditionClock(Time+3.01f);FallenLives();Check(p.Hp>0&&life.lives==4&&Vector3.Distance(p.Body.position,m.Body.position)>=25,"respawn health and distance");
            if(wipe){foreach(var pilot in Members()){var s=f.hush.First(s=>s.id==pilot.Seat.id);s.lives=1;pilot.ShieldUntil=0;bossHits.Clear();FallenHurt(pilot,0,true);}StepFallenBattle();Check(Pve.Holding&&Pve.Cinematic=="defeat","last life wipe");PveHoldTick(4);Check(Match.Phase==NativePhase.Match&&Pve.Outcome=="defeat","wipe completion");Debug.Log("PASS native NOCTIEL wipe: "+PartySize+" pilots / "+n+" checks");return;}
            if(PartySize>1)
            {
                var giver=Members().Skip(1).First();life.lives=1;p.ShieldUntil=0;bossHits.Clear();FallenHurt(p,0,true);giver.Body.position=p.Body.position+Vector3.right;giver.Input=new InputFrame{InteractHeld=true};giver.LastDamage=-100;
                FallenLives();Match.SynchronizeExpeditionClock(Time+4.1f);FallenLives();Check(p.Hp>0&&life.lives==1&&f.hush.First(s=>s.id==giver.Seat.id).lives==4,"teammate transfers one life");giver.Input=new InputFrame();
            }
            // Hush covers corners and every band of the open disc independently of navigation.
            for(int phase=1;phase<=4;phase++)for(int angle=0;angle<360;angle+=45)foreach(float radius in new[]{0f,30f,73f,79f})
            {
                z.phase=phase;f.phaseChangeAt=f.draftAt=-100;bossGap=Time+100;z.action=null;var d=NativeFallenRules.Dir(angle);p.Body.position=new Vector3(d.X*radius,.9f,d.Y*radius);p.ShieldUntil=0;p.Hp=100;p.Dots.Clear();p.Wild.ImmuneUntil=p.Wild.DownUntil=0;life.x=p.Body.position.x;life.z=p.Body.position.z;life.hushAt=Time-10;
                StepFallenBattle();Check(p.Hp<100,"stationary sweep "+phase+"/"+angle+"/"+radius);
            }
            z.phase=1;f.chipOffer=Array.Empty<string>();p.Hp=100;
            for(int phase=2;phase<=4;phase++)
            {
                HitFallenBoss(m,p,m.MaxHp,null,"direct");Check(Mathf.Approximately(m.Hp,m.MaxHp*NativeFallenBoss.PhaseFrom[phase-1])&&f.phaseChangeAt>Time,"threshold clamps damage and waits for bar: phase="+phase+" hp="+m.Hp.ToString("R")+" expected="+(m.MaxHp*NativeFallenBoss.PhaseFrom[phase-1]).ToString("R")+" hold="+Pve.Holding+" pause="+FallenPaused+" change="+f.phaseChangeAt+" time="+Time);
                Check(HitFallenBoss(m,p,100,null,"direct")==0,"invulnerable at threshold");Match.SynchronizeExpeditionClock(f.phaseChangeAt+.01f);FallenPhase();Check(z.phase==phase&&f.chipOffer.Length==3,"seeded next phase");
                Match.SynchronizeExpeditionClock(f.draftAt+4.01f);FallenPhase();Check(Pve.Rows[p.Seat.id].Open?.Offers?.Count==3,"normal chip draft opens");var pick=Pve.Rows[p.Seat.id].Open;Check(ChoosePve(p,pick.Id,pick.Offers[0]),"real chip installs");Wire();Match.SynchronizeExpeditionClock(f.resumeAt+.01f);FallenPhase();Check(!FallenPaused,"phase resumes");
            }
            foreach(string action in new[]{"verdict","last","eclipse","fall"})
            {
                p.Hp=100;p.ShieldUntil=0;life.lives=5;p.Body.position=new Vector3(0,.9f,20);FallenStart(action,p);Check(z.hitAt-z.actionAt>=2.4f,"OHK minimum warning "+action);Wire();
                Vector3 safe=action=="eclipse"?Vec(f.pools[0])+Vector3.up*.9f:action=="fall"?new Vector3(0,.9f,0):new Vector3(8,.9f,20);
                p.Body.position=safe;Match.SynchronizeExpeditionClock(z.hitAt+.01f);FallenAttack();Check(p.Hp>0,"walk-out survival "+action);
            }
            foreach(string action in new[]{"choir","penance","thrones"}){FallenSummon(action);var add=Minions.Last(a=>a.Kind.StartsWith("fallen-",StringComparison.Ordinal));Check(HitMinion(add,p,add.Hp+1,Vector3.zero,0)>0,"killable add "+action);}
            Wire();m.Hp=0;StepFallenBattle();Check(z.deathAt>=0&&Match.Phase!=NativePhase.Match,"death scene");Match.SynchronizeExpeditionClock(Time+NativeFallenRules.DeathSeconds+.1f);StepFallenBattle();Check(Match.Phase==NativePhase.Match&&Pve.Outcome=="victory","victory ends encounter");
            Debug.Log("PASS native NOCTIEL: "+PartySize+" pilots / "+n+" checks");
        }
    }
}
#endif
