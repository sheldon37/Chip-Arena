using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Katana's rules (owner 2026-11-10: "Make very high quality designed katana ... classes"; docs/KATANA.md; Claude builds them at the owner's
    // word, logged for Astra). Its left click is its own: four cuts that chain (the third steps in, the fourth cleaves), a rushing draw on a
    // sprint, and within a moment of a timed parry the counter (a paired critical, the foe held through it). Q the iai: sheathed and
    // gathered, a dash along the aim, and the line it cut splits a moment later. E the petal storm: cuts all round, the last a launch. Its
    // ten chips (Core NativeKatanaChips) change these rules here, each only for a Katana holding it.
    public sealed partial class NativeArena
    {
        /// <summary>The Katana's damage share on a blow at `id`: Honed Edge's copies, Still Water's readied cut, Falling Blossom's launched mark.</summary>
        private float KatanaShare(NativePilot p,int id,bool melee)
        {
            float s=1+NativeKatanaChips.HonedDamage*BladeStacks(p,NativeKatanaChips.HonedEdge,NativeKatanaChips.HonedMax);
            if(melee&&p.Blade.ChipAt>-50&&BladeChip(p,NativeKatanaChips.StillWater)&&p.Blade.CastKind==NativeKatana.Cut&&p.Blade.ChipAt==p.Blade.CastAt)s*=NativeKatanaChips.StillDamage;
            if(melee&&p.BladeLaunched.TryGetValue(id,out float until)&&Time<until&&BladeChip(p,NativeKatanaChips.FallingBlossom)){s*=NativeKatanaChips.BlossomDamage;p.BladeLaunched.Remove(id);}
            return s;
        }
        private float KatanaMove(NativeBladeKit k)=>k.CastKind==NativeKatana.Storm?NativeKatana.StormSlow:k.CastKind==NativeKatana.Cut?(k.CastStep>=2?.35f:.6f):k.CastKind==NativeKatana.Iai?0:.5f;
        private float KatanaIaiDraw(NativePilot p)=>BladeChip(p,NativeKatanaChips.SheathedFury)?NativeKatanaChips.FuryDraw:NativeKatana.IaiDraw;
        private int KatanaStormCuts(NativePilot p)=>NativeKatana.StormCuts+(BladeChip(p,NativeKatanaChips.ThousandPetals)?NativeKatanaChips.PetalsMore:0);
        private void KatanaInput(NativePilot p,bool attack,bool press)
        {
            var k=p.Blade;if(BladeBusy(p)||Time<p.RollEnd||Time<p.BlockEnd||Time<p.Ready)return;
            float rate=Math.Max(.5f,(float)p.Stats["attackRate"]),cd=(float)p.Stats["abilityCooldown"];
            if(p.Input.AbilityQ&&Time>=k.Ready[0])
            {
                // Q: sheathed and gathered, then the dash (its cooldown from the split, Sheathed Fury shorter).
                float draw=KatanaIaiDraw(p);BladeBegin(p,NativeKatana.Iai,draw,draw+NativeKatana.IaiDash+NativeKatana.IaiRecover);k.ComboUntil=0;k.Ready[0]=Time+60;p.BladeMarked.Clear();k.SplitAt=-100;p.BladeSecondSplit=-100;return;
            }
            if(p.Input.AbilityE&&Time>=k.Ready[1])
            {
                int cuts=KatanaStormCuts(p);BladeBegin(p,NativeKatana.Storm,NativeKatana.StormWind,NativeKatana.StormWind+cuts*NativeKatana.StormEvery+NativeKatana.StormRecover);k.ComboUntil=0;
                p.BladeStormCut=0;p.BladeStormNext=k.CastRelease;k.Ready[1]=k.CastUntil+NativeKatana.Cooldown[1]*cd;return;
            }
            if(!attack)return;
            // The counter: a cut within the window after a timed parry, its foe within reach in front.
            if(press&&Time<k.CounterUntil&&KatanaCounter(p))return;
            if(p.Grounded&&Time>k.ComboUntil&&NativeMeleeFeel.SprintAttack(p.Input,p.Pose.Sprinting))
            {
                BladeBegin(p,NativeKatana.Rush,NativeKatana.RushWind,NativeKatana.RushWind+NativeKatana.RushTime+NativeKatana.RushRecover);k.ComboUntil=0;return;
            }
            int step=Time<=k.ComboUntil?(k.Combo+1)%NativeKatana.Steps:0;k.Combo=step;
            // Still Water: readied after a stillness without attacking (the record marks the cut it readies).
            bool still=BladeChip(p,NativeKatanaChips.StillWater)&&Time-p.BladeLastAttack>=NativeKatanaChips.StillAfter;
            BladeBegin(p,NativeKatana.Cut,NativeKatana.CutHit[step]/rate,NativeKatana.CutLength[step]/rate,step);k.ComboUntil=k.CastUntil+NativeKatana.ComboWindow;
            k.ChipAt=still?k.CastAt:-100;if(still)BladeHit(p,NativeKatana.HitFocus,p.Body.position+Vector3.up*1.1f,Vector3.up);
            float lunge=NativeKatana.CutLunge[step];if(lunge>0)BladePath(p,p.BladeFacing,lunge,Time,k.CastRelease);
        }
        private void KatanaRelease(NativePilot p)
        {
            var k=p.Blade;
            switch(k.CastKind)
            {
                case NativeKatana.Cut:KatanaCut(p,k.CastStep);break;
                case NativeKatana.Rush:
                {
                    // Flash Step: farther, and untouchable through the dash.
                    bool flash=BladeChip(p,NativeKatanaChips.FlashStep);float reach=NativeKatana.RushReach*(flash?NativeKatanaChips.FlashFarther:1);
                    BladePath(p,p.BladeFacing,reach,Time,Time+NativeKatana.RushTime);if(flash)p.ShieldUntil=Math.Max(p.ShieldUntil,Time+NativeKatana.RushTime+.05f);
                    BladeHit(p,NativeKatana.HitDraw,p.Body.position+Vector3.up*.9f,p.BladeFacing);break;
                }
                case NativeKatana.Iai:
                {
                    // The dash along the flat aim; the line from where it set off to where it ended is what splits.
                    var d=NativeKatana.DashDir(p.BladeAim.x,p.BladeAim.z);var dir=new Vector3(d.X,0,d.Z);var from=p.Body.position;
                    BladePath(p,dir,NativeKatana.IaiReach,Time,Time+NativeKatana.IaiDash);k.LineFrom=Vec(from);k.LineTo=k.To;k.LineAt=Time+NativeKatana.IaiDash;k.SplitAt=k.LineAt+NativeKatana.IaiSplit;
                    p.BladeFirstSplit=k.SplitAt;p.BladeSecondSplit=BladeChip(p,NativeKatanaChips.TwinMoons)?k.SplitAt+NativeKatanaChips.TwinDelay:-100;
                    BladeHit(p,NativeKatana.HitDraw,from+Vector3.up*.9f,dir);break;
                }
            }
        }
        /// <summary>A cut: a cone ahead; the finisher pushes and stuns longer (Crescent Wave: and sends its wave).</summary>
        private void KatanaCut(NativePilot p,int step)
        {
            var at=p.Body.position;var f=p.BladeFacing;float reach=NativeKatana.CutReach[step],arc=NativeKatana.CutArc[step],damage=NativeKatana.CutDamage[step];bool finisher=step==NativeKatana.Steps-1;
            string kind=finisher?NativeKatana.HitFinisher:NativeKatana.HitCut;float stun=NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"])*(finisher?NativeKatana.FinisherStun:1);
            BladeCone(p,at,f,reach,arc,(q,d)=>{if(BladeHurt(p,q,damage,d,"cut",(float)p.Weapon.impulse,true)<=0)return;StaggerMelee(q,stun);if(finisher)q.Knockback+=new Vector3(d.x,0,d.z).normalized*NativeKatana.FinisherPush;KatanaBleed(p,q);BladeHit(p,kind,q.Body.position+Vector3.up*.45f,d,q.Seat.id,step);},
                (m,d)=>{if(BladeHurt(p,m,damage,d,"cut",0,true)<=0)return;if(finisher)BladePush(m,new Vector3(d.x,0,d.z).normalized*NativeKatana.FinisherPush,1);KatanaBleed(p,m);BladeHit(p,kind,m.Body.position+Vector3.up*.45f,d,BladeKey(m),step);});
            if(finisher&&BladeChip(p,NativeKatanaChips.CrescentWave)){var w=new BladeWave{From=at+Vector3.up*.3f,Dir=f,At=Time};p.BladeWaves.Add(w);BladeHit(p,NativeKatana.HitWave,w.From,f);}
        }
        /// <summary>The counter: the foe of the block or parry, if it is within reach in front and in sight; the paired critical begins.</summary>
        private bool KatanaCounter(NativePilot p)
        {
            var k=p.Blade;var q=p.BladeCounterFoe;var at=p.Body.position;
            bool Fits(Vector3 pos){var d=pos-at;d.y=0;return d.magnitude<=NativeKatana.CounterReach;}
            if(q==null||q.Hp<=0||!q.Seat.connected||!CanHit(p,q)||!Fits(q.Body.position)||!MeleeVisible(at+Vector3.up*.3f,q.Body.position,p,q.Collider)||BladeHeld(q)||q.DogHolder!=null||SirenArmor(q)||Pve?.Boss==q)return false;
            // Turned onto the foe, both held through the critical.
            var look=q.Body.position-at;look.y=0;p.Input.Yaw=Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg;
            BladeBegin(p,NativeKatana.Counter,NativeKatana.CounterHit,NativeKatana.CounterHold);k.ComboUntil=0;k.CounterUntil=-100;
            BladeStartPair(p,q,null,NativeBladeMotion.KatanaCritical,NativeKatana.CounterHit,NativeKatana.CounterHold,NativeKatana.CounterStand,true,false);
            p.Ready=k.CastUntil;return true;
        }
        private void KatanaCounterBlow(NativePilot p,NativePilot q,NativeMinion m)
        {
            bool mirror=BladeChip(p,NativeKatanaChips.MirrorMoon);float damage=NativeKatana.CounterDamage*(mirror?NativeKatanaChips.MirrorDamage:1);var f=p.BladeFacing;
            if(q!=null&&q.Hp>0&&BladeHurt(p,q,damage,f,"counter",0,true)>0){KatanaBleed(p,q);BladeHit(p,NativeKatana.HitCounter,q.Body.position+Vector3.up*.6f,f,q.Seat.id);}
            if(mirror)Heal(p,NativeKatanaChips.MirrorHeal,"mirror-moon");
        }
        private void KatanaWorld(NativePilot p,bool able)
        {
            var k=p.Blade;float cd=(float)p.Stats["abilityCooldown"];
            // The iai: everyone the dash passes is marked; at the split each marked one takes the cut (Twin Moons: twice).
            if(k.CastKind==NativeKatana.Iai&&k.LineAt>-50&&Time>=k.CastRelease&&Time<=k.LineAt+Rules.Dt)KatanaMarks(p);
            if(p.BladeFirstSplit>-50&&Time>=p.BladeFirstSplit){p.BladeFirstSplit=-100;KatanaSplit(p,1);k.Ready[0]=Time+NativeKatana.Cooldown[0]*cd*(BladeChip(p,NativeKatanaChips.SheathedFury)?NativeKatanaChips.FuryCooldown:1);}
            if(p.BladeSecondSplit>-50&&Time>=p.BladeSecondSplit){p.BladeSecondSplit=-100;KatanaSplit(p,NativeKatanaChips.TwinShare);}
            // A cast cut short before its dash: Q is back at once.
            if(k.CastKind==NativeKatana.Iai&&k.Ready[0]>Time+30&&Time>=k.CastUntil&&k.SplitAt<-50)k.Ready[0]=Time;
            if(able&&k.CastKind==NativeKatana.Rush&&Time>=k.CastRelease&&Time<=k.CastRelease+NativeKatana.RushTime+Rules.Dt)KatanaRushStep(p);
            if(able&&k.CastKind==NativeKatana.Storm&&Time<k.CastUntil)KatanaStormStep(p);
            if(p.BladeWaves.Count>0)KatanaWaveStep(p);
            BleedStep(p);
            foreach(var id in p.BladeLaunched.Keys.ToArray())if(Time>=p.BladeLaunched[id])p.BladeLaunched.Remove(id);
            k.Chips=0;foreach(var id in NativeKatanaChips.Ids)if(BladeChip(p,id))k.Chips|=NativeKatanaChips.Bit(id);
            if(Time>=k.CounterUntil)p.BladeCounterFoe=null;
        }
        private void KatanaMarks(NativePilot p)
        {
            var k=p.Blade;var a=Vec(k.LineFrom);var b=p.Body.position;
            foreach(var q in BladePilots(p)){if(p.BladeMarked.Contains(q.Seat.id))continue;if(BladeSegment(a,b,q.Body.position)<=NativeKatana.IaiWidth&&Math.Abs(q.Body.position.y-b.y)<2){p.BladeMarked.Add(q.Seat.id);BladeHit(p,NativeKatana.HitMark,q.Body.position+Vector3.up*.6f,b-a,q.Seat.id);}}
            foreach(var m in BladeMinions(p)){int id=BladeKey(m);if(p.BladeMarked.Contains(id))continue;if(BladeSegment(a,b,m.Body.position)<=NativeKatana.IaiWidth+.2f&&Math.Abs(m.Body.position.y-b.y)<2.2f){p.BladeMarked.Add(id);BladeHit(p,NativeKatana.HitMark,m.Body.position+Vector3.up*.5f,b-a,id);}}
        }
        /// <summary>The split: each one the line marked takes the cut (`share` of it) and staggers; Severing Line kills the ones it leaves low.</summary>
        private void KatanaSplit(NativePilot p,float share)
        {
            var k=p.Blade;var dir=Vec(k.LineTo)-Vec(k.LineFrom);bool sever=BladeChip(p,NativeKatanaChips.SeveringLine);
            BladeHit(p,NativeKatana.HitSheathe,p.Body.position+Vector3.up*.9f,dir,0,share<1?1:0);
            foreach(var id in p.BladeMarked.ToArray())
            {
                var q=Pilots.Find(x=>x.Seat.id==id);var m=q==null?Minions.Find(x=>BladeKey(x)==id):null;
                if(q!=null&&q.Hp>0&&CanHit(p,q))
                {
                    if(BladeHurt(p,q,NativeKatana.IaiDamage*share,dir,"iai")>0){StaggerMelee(q,NativeKatana.IaiStagger);KatanaBleed(p,q);BladeHit(p,NativeKatana.HitSplit,q.Body.position+Vector3.up*.7f,dir,id,share<1?1:0);}
                    if(sever&&q.Hp>0&&q.Hp<NativeKatanaChips.SeverBelow*(float)q.Stats["hp"]&&!SirenArmor(q)&&Pve?.Boss!=q)BladeHurt(p,q,q.Hp*4+10,dir,"sever");
                }
                else if(m!=null&&m.Hp>0)
                {
                    if(BladeHurt(p,m,NativeKatana.IaiDamage*share,dir,"iai")>0){KatanaBleed(p,m);BladeHit(p,NativeKatana.HitSplit,m.Body.position+Vector3.up*.6f,dir,id,share<1?1:0);}
                    if(sever&&m.Hp>0&&m.Hp<NativeKatanaChips.SeverBelow*m.MaxHp&&!IsZoneBoss(m))BladeHurt(p,m,m.Hp*4+10,dir,"sever");
                }
            }
        }
        private void KatanaLanded(NativePilot p,int id,bool killed)
        {
            // Severing Line: a kill by the iai takes time off Q.
            var k=p.Blade;if(killed&&BladeChip(p,NativeKatanaChips.SeveringLine)&&k.CastKind==NativeKatana.Iai&&k.SplitAt>-50&&Time>=k.SplitAt-.01f)k.Ready[0]=Math.Max(Time,k.Ready[0]-NativeKatanaChips.SeverRefund);
        }
        private void KatanaRushStep(NativePilot p)
        {
            var k=p.Blade;bool Beside(Vector3 d)=>new Vector2(d.x,d.z).magnitude<=NativeKatana.RushWidth&&Math.Abs(d.y)<=1.8f;
            foreach(var q in BladePilots(p))
            {
                if(p.BladeStruck.Contains(q.Seat.id))continue;var d=q.Body.position-p.Body.position;if(!Beside(d))continue;p.BladeStruck.Add(q.Seat.id);
                if(BladeHurt(p,q,NativeKatana.RushDamage,d,"rush",0,true)>0){var side=Vector3.Cross(Vector3.up,p.BladeFacing);if(Vector3.Dot(side,d)<0)side=-side;q.Knockback+=(side*.5f+p.BladeFacing*.5f).normalized*NativeKatana.RushPush;StaggerMelee(q,.3f);KatanaBleed(p,q);BladeHit(p,NativeKatana.HitRush,q.Body.position+Vector3.up*.45f,side,q.Seat.id);}
            }
            foreach(var m in BladeMinions(p))
            {
                int id=BladeKey(m);if(p.BladeStruck.Contains(id))continue;var d=m.Body.position-p.Body.position;if(!Beside(d))continue;p.BladeStruck.Add(id);
                if(BladeHurt(p,m,NativeKatana.RushDamage,d,"rush",0,true)>0){BladePush(m,p.BladeFacing*NativeKatana.RushPush,1);KatanaBleed(p,m);BladeHit(p,NativeKatana.HitRush,m.Body.position+Vector3.up*.45f,p.BladeFacing,id);}
            }
        }
        /// <summary>The storm: a cut all round every StormEvery s from the release (Thousand Petals: more, each drawing them in); the last launches.</summary>
        private void KatanaStormStep(NativePilot p)
        {
            var k=p.Blade;int cuts=KatanaStormCuts(p);if(p.BladeStormCut>=cuts||Time<p.BladeStormNext)return;
            int cut=p.BladeStormCut++;p.BladeStormNext=k.CastRelease+(cut+1)*NativeKatana.StormEvery;bool last=cut==cuts-1,petals=BladeChip(p,NativeKatanaChips.ThousandPetals),blossom=BladeChip(p,NativeKatanaChips.FallingBlossom);
            var at=p.Body.position;float lift=blossom?NativeKatanaChips.BlossomLift:NativeKatana.StormLift;
            BladeCone(p,at,p.BladeFacing,NativeKatana.StormRadius,3.2f,(q,d)=>
            {
                if(petals&&!last)BladeDrag(q,at,NativeKatanaChips.PetalPull,NativeKatanaChips.PetalClose);
                if(BladeHurt(p,q,NativeKatana.StormDamage,d,"storm",0,true)<=0)return;KatanaBleed(p,q);
                if(last){BombLaunch(q,p,new Vector3(d.x,0,d.z).normalized*1.5f,lift,0);if(blossom)p.BladeLaunched[q.Seat.id]=Time+NativeKatanaChips.BlossomFor;BladeHit(p,NativeKatana.HitLaunch,q.Body.position+Vector3.up*.5f,Vector3.up,q.Seat.id);}
                else{StaggerMelee(q,.18f);BladeHit(p,petals?NativeKatana.HitPetal:NativeKatana.HitStorm,q.Body.position+Vector3.up*.6f,d,q.Seat.id,cut);}
            },(m,d)=>
            {
                if(petals&&!last)BladeDrag(m,at,NativeKatanaChips.PetalPull,NativeKatanaChips.PetalClose);
                if(BladeHurt(p,m,NativeKatana.StormDamage,d,"storm",0,true)<=0)return;KatanaBleed(p,m);
                if(last){BladePush(m,new Vector3(d.x,0,d.z).normalized*1.5f,lift);if(blossom)p.BladeLaunched[BladeKey(m)]=Time+NativeKatanaChips.BlossomFor;BladeHit(p,NativeKatana.HitLaunch,m.Body.position+Vector3.up*.5f,Vector3.up,BladeKey(m));}
                else BladeHit(p,petals?NativeKatana.HitPetal:NativeKatana.HitStorm,m.Body.position+Vector3.up*.5f,d,BladeKey(m),cut);
            });
            // A ring of petals each cut, even when it meets nobody (the effects' beat).
            BladeHit(p,NativeKatana.HitStorm,at+Vector3.up*.9f,p.BladeFacing,0,cut);
        }
        /// <summary>Crescent Wave: the finisher's wave flies on, cutting whoever it passes once.</summary>
        private void KatanaWaveStep(NativePilot p)
        {
            for(int i=p.BladeWaves.Count-1;i>=0;i--)
            {
                var w=p.BladeWaves[i];float age=Time-w.At,travel=NativeKatanaChips.WaveTravel(age),was=NativeKatanaChips.WaveTravel(age-Rules.Dt);var a=w.From+w.Dir*was;var b=w.From+w.Dir*travel;
                var wall=SweepNative(a,b-a,.15f,BladeWall);bool done=travel>=NativeKatanaChips.WaveReach-1e-3f||wall.HasValue;if(wall.HasValue)b=a+(b-a).normalized*wall.Value.Distance;
                foreach(var q in BladePilots(p)){if(w.Struck.Contains(q.Seat.id)||BladeSegment(a,b,q.Body.position)>NativeKatanaChips.WaveWidth+.35f)continue;w.Struck.Add(q.Seat.id);if(BladeHurt(p,q,NativeKatanaChips.WaveDamage,w.Dir,"wave")>0){StaggerMelee(q,.2f);BladeHit(p,NativeKatana.HitWave,q.Body.position+Vector3.up*.5f,w.Dir,q.Seat.id,1);}}
                foreach(var m in BladeMinions(p)){int id=BladeKey(m);if(w.Struck.Contains(id)||BladeSegment(a,b,m.Body.position)>NativeKatanaChips.WaveWidth+.4f)continue;w.Struck.Add(id);if(BladeHurt(p,m,NativeKatanaChips.WaveDamage,w.Dir,"wave")>0)BladeHit(p,NativeKatana.HitWave,m.Body.position+Vector3.up*.4f,w.Dir,id,1);}
                if(done)p.BladeWaves.RemoveAt(i);
            }
        }
    }
}
