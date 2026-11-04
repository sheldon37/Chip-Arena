#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private void ValidateProjectileRework(Action<string> setup,Action<float> advance,NativePilot a,NativePilot b,GameObject wall)
        {
            int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception("Native projectile rework: "+why);checks++;}
            var legacy=JsonUtility.FromJson<ContentWeapon>("{\"id\":\"legacy\",\"gravity\":30}");
            Check(legacy.gravityMultiplier==1&&legacy.projectileBounceRetention==1&&NativeProjectileRules.Range(legacy)==160,"real JSON missing profile fields retain legacy defaults");
            void Reset(string spec){setup(spec);Events.Clear();instantBudgetAt=Time;instantContacts=0;}
            void Aim(){a.Input.AimTarget=Vec(b.Body.position);Physics.SyncTransforms();}
            foreach(string spec in new[]{"pistol","machinegun","fire","water","lightning","darkness"})
            {
                Reset(spec);b.Body.position+=Vector3.forward*20;Aim();Fire(a);var shot=Bullets.Single();
                Check(shot.Gravity==0&&Math.Abs(shot.Velocity.magnitude-a.Weapon.speed)<.01,"straight speed "+spec);
                Check(Math.Abs(shot.Velocity.magnitude*shot.ImpactSpeedScale-a.Weapon.projectileImpactSpeed)<.01,"wound energy baseline "+spec);
                float y=shot.Position.y,vy=shot.Velocity.y;StepBullets();Check(Math.Abs(shot.Position.y-y-vy/60)<.001,"no drop "+spec);
            }
            Reset("rifle");Fire(a);Check(b.Hp<100&&Bullets.Count==0,"same-call hit and no lingering body");
            float hp=b.Hp;StepBullets();Check(b.Hp==hp,"no duplicate tick damage");
            var frame=NativeFrameCodec.Clone(NetworkFrame());var trace=frame.snapshot.Events.Single(e=>e.Kind==EventKind.Hitscan);
            Check(trace.Projectile>0&&trace.Detail=="rifle"&&trace.Radius>0&&trace.From.z<trace.Position.z&&frame.projectileEnds.Any(e=>e.Id==trace.Projectile),"real JSON segment and endpoint for guest/late join/replay");
            Reset("rifle");wall.SetActive(true);Physics.SyncTransforms();Fire(a);Check(b.Hp==100&&Bullets.Count==0,"cover occlusion");
            Reset("rifle");b.Body.position+=Vector3.right*5;Physics.SyncTransforms();Fire(a);Check(b.Hp==100,"old target position does not hit moved target");
            Aim();Fire(a);Check(b.Hp<100,"current authority target position hits");
            Reset("rifle");b.BlockEnd=Time+1;Fire(a);Check(b.Hp==100&&Bullets.Count==1&&Bullets[0].Owner==b.Seat.id&&!Bullets[0].Instant,"block returns simulated shot");
            foreach(string stat in new[]{"homing","boomerang","zigzag","projectileGravityAdd","projectileSpeed"})
            {
                Reset("rifle");a.Stats[stat]=stat=="projectileSpeed"?.5:1;Fire(a);
                Check(b.Hp==100&&Bullets.Count==1,"travel fallback "+stat);
            }
            foreach(string chip in new[]{"lobber","meteor"})
            {Reset("rifle");a.Seat.chips[chip]=1;RefreshLiveStats(a);Fire(a);Check(Bullets.Count==1&&Bullets[0].Gravity>0,"ballistic chip fallback "+chip);}
            foreach(string chip in TravelMods)
            {
                Reset("rifle");var shot=new NativeBullet{Owner=a.Seat.id,Spec="rifle"};shot.Mods[chip]=1;
                Check(NeedsTravel(shot,a),"time-dependent modifier "+chip);
            }
            Reset("rifle");a.Stats["extraProjectiles"]=3;Fire(a);Check(Events.Count(e=>e.Kind==EventKind.Hitscan)>=4,"instant multishot");
            Reset("rifle");a.Stats["split"]=2;Fire(a);Check(Bullets.Count==4&&Bullets.All(x=>!x.Direct&&x.EffectsUsed&&!x.Instant),"impact splits keep simulated travel and recursion guard");
            Reset("rifle");a.Stats["bounces"]=1;wall.SetActive(true);Physics.SyncTransforms();Fire(a);
            Check(b.Hp==100&&Events.Count(e=>e.Kind==EventKind.Hitscan)==2,"same-shot reflected cover segment");
            Reset("rifle");a.Stats["pierces"]=1;Fire(a);Check(b.Hp<100&&Events.Count(e=>e.Kind==EventKind.Hitscan)==2,"pierce continues without duplicate target hit");
            Reset("rifle");b.Hp=1;Fire(a);Check(b.Death!=null&&b.Death.projectile==0,"instant kill uses victim camera, never nonexistent flight");
            Reset("rifle");bool replica=Replica;try{Replica=true;Fire(a);StepBullets();Check(b.Hp==100&&Bullets.Count==0,"replica cannot deal damage");}finally{Replica=replica;}
            Reset("rifle");a.Stats["extraProjectiles"]=11;a.Stats["bounces"]=1;wall.SetActive(true);Physics.SyncTransforms();
            for(int player=0;player<8;player++)Fire(a);
            Check(Events.Count(e=>e.Kind==EventKind.Hitscan)<=InstantContactsPerTick&&Bullets.Count>0,"shared ray budget falls back without discarding shots");
            // CPU/PhysX benchmark is reported by the real builder, not inferred from managed maths.
            Reset("machinegun");a.Stats["extraProjectiles"]=11;a.Stats["projectileSpeed"]=4;a.Stats["split"]=6;a.OverdriveAt=Time;a.OverdriveUntil=Time+4;
            wall.SetActive(true);a.Input.AimTarget=Vec(wall.transform.position);Physics.SyncTransforms();
            var watch=System.Diagnostics.Stopwatch.StartNew();int peak=0;
            for(int tick=0;tick<120;tick++){if(tick%3==0)for(int player=0;player<8;player++)Fire(a);peak=Math.Max(peak,ProjectileCount);StepBullets();advance(1f/60);}
            watch.Stop();Check(peak<=100&&Bullets.All(x=>!float.IsNaN(x.Position.x)),"eight-source Overdrive/multishot stress stays bounded");
            Debug.Log("PASS native projectile rework: "+checks+" checks; 120 stress ticks, peak "+peak+", CPU ms "+watch.Elapsed.TotalMilliseconds);
        }
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
