#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Called by the existing real-Unity ranged gate; API compilation alone does not execute these contacts.
        private void ValidateRangedQ(Action<string> setup,Action<float> advance,NativePilot a,NativePilot b,GameObject wall)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native ranged Q: "+why);}
            bool Near(float x,float y)=>Math.Abs(x-y)<.0005f;
            void Start(string spec,int ammo=-1)
            {setup(spec);Events.Clear();a.Mana=a.Stamina=0;if(ammo>=0)a.Ammo=ammo;a.Input.AbilityQ=true;StepAbilities(a);a.Input.AbilityQ=false;}
            int Shots(string detail)=>Events.Count(e=>e.Kind==EventKind.Shot&&e.Source==a.Seat.id&&e.Detail==detail);
            void FanAt(float time){advance(Math.Max(0,time-Time));StepFan(a);}
            foreach(int ammo in new[]{1,2,3,4,7})
            {
                Start("pistol",ammo);float at=Time,ready=a.QReady;
                Check(Shots("fan")==1&&a.Ammo==ammo-1&&Near(ready-at,7)&&a.Mana==0&&a.Stamina==0,"instant first round / free activation");
                Check(Near(Bullets[0].Damage,23*.8f)&&Bullets[0].Spec=="pistol","normal projectile with 80 percent damage");
                StepAbilities(a);StepFan(a);Check(Shots("fan")==1,"no repeat immediate round");
                for(int i=1;i<4;i++)
                {FanAt(at+i*.075f-.001f);Check(Shots("fan")==Math.Min(ammo,i),"no early burst round");FanAt(at+i*.075f+.001f);Check(Shots("fan")==Math.Min(ammo,i+1),"scheduled burst round");}
                Check(a.Fan==null&&a.Ammo==ammo-Math.Min(4,ammo)&&a.QReady==ready,"bounded burst and single cooldown");
                Check(ammo<=4?a.ReloadEnd>Time:a.ReloadEnd==0,"empty magazine automatically reloads");
                if(ammo<=4){advance(a.ReloadEnd-Time+.001f);StepPilot(a);Check(a.Ammo==Magazine(a)&&a.ReloadEnd==0,"normal reload completion");}
            }
            Start("pistol",0);Check(a.QReady==0&&Shots("fan")==0&&a.Fan==null,"empty activation refused");
            setup("pistol");for(int i=0;i<100;i++)Bullets.Add(new NativeBullet());int before=a.Ammo;
            PressAbility(a,"q");Check(a.QReady==0&&a.Ammo==before&&a.Fan==null,"projectile cap does not spend ammo/cooldown");
            foreach(string stop in new[]{"reload","roll","block","stun","cancel","death"})
            {
                Start("pistol");before=a.Ammo;
                if(stop=="reload")a.Input.Reload=true;if(stop=="roll")a.RollEnd=Time+1;if(stop=="block")a.BlockEnd=Time+1;
                if(stop=="stun")a.StunEnd=Time+1;if(stop=="cancel")a.Input.CancelAbilities=true;if(stop=="death")a.Hp=0;
                advance(.1f);StepFan(a);Check(a.Fan==null&&a.Ammo==before&&Shots("fan")==1&&a.QReady>Time,"interrupt cancels only unpaid rounds: "+stop);
            }
            Start("pistol");var shot=Bullets[0];a.Fan=null;b.BlockEnd=Time+1;shot.Position=b.Body.position;shot.Velocity=Vector3.forward*120;
            StepBullets();Check(shot.Owner==b.Seat.id&&b.Hp==100,"Fan uses normal deflection");
            Start("pistol");a.Fan=null;shot=Bullets[0];shot.Position=new Vector3(1000,10,1000.8f);shot.Velocity=Vector3.forward*120;shot.Gravity=0;wall.SetActive(true);Physics.SyncTransforms();
            StepBullets();Check(b.Hp==100,"Fan uses normal cover contacts");
            Debug.Log("PASS native Fan the Hammer: 1-4 actual shots, ammo/cooldown once, reload, interruptions, cap, normal projectiles/deflection/cover");

            Start("machinegun");float start=a.OverdriveAt,until=a.OverdriveUntil,vent=a.VentUntil;
            Check(Near(start,Time)&&Near(until-start,4)&&Near(vent-until,.8f)&&Near(a.QReady-start,10)&&a.Mana==0&&a.Stamina==0,"Overdrive activation clocks/cost");
            a.Input.Shoot=true;a.Input.Aim=true;a.AimShots=80;before=a.Ammo;StepPilot(a);
            Check(Shots("overdrive")==1&&a.Ammo==before-1&&!a.Aiming&&a.AimShots==0&&Near(a.Ready-Time,.085f/1.6f),"boosted ordinary round, no ADS/bloom");
            a.Input.Shoot=false;a.Input.X=1;for(int i=0;i<15;i++){advance(1f/60);StepPilot(a);}
            Check(Near(a.MoveVelocity.x,5.44f),"host movement penalty");
            var frame=NativeFrameCodec.Clone(NetworkFrame());var pilot=frame.pilots.Single(p=>p.id==a.Seat.id);var actor=frame.snapshot.Actors.Single(p=>p.Id==a.Seat.id);
            Check(pilot.overdriveAt==start&&pilot.overdriveUntil==until&&pilot.ventUntil==vent&&actor.OverdriveAt==start&&actor.OverdriveUntil==until&&actor.VentUntil==vent,"real JsonUtility retains pilot and presentation clocks");
            advance(until-Time+.001f);a.Ready=0;a.Input.Shoot=true;before=a.Ammo;int count=Shots("overdrive");StepPilot(a);
            Check(Venting(a)&&a.Ammo==before&&Shots("overdrive")==count,"vent refuses primary at start");
            advance(vent-Time-.001f);StepPilot(a);Check(a.Ammo==before,"vent refuses primary before end");
            advance(.002f);StepPilot(a);Check(!Venting(a)&&a.Ammo==before-1&&Events.Last(e=>e.Kind==EventKind.Shot).Detail==null&&Near(a.Ready-Time,.085f),"normal shots resume after vent");
            Start("machinegun");a.RollEnd=Time+1;CancelWindup(a);Check(Overdriving(a),"roll does not erase buff or future vent");
            setup("rifle");a.Input.Aim=true;StepPilot(a);Check(a.Aiming,"rifle scope retained");
            Debug.Log("PASS native Overdrive: clocks, 1.6 fire-rate, no bloom/ADS, 80 percent host motion, JSON, vent fire lock and normal resumption; rifle scope retained");
        }
    }
}
#endif
