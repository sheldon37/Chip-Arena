using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Fangs' rules (owner 2026-11-10: "DEV-Only Daggers ... Testing different abilities and animation sets"; docs/FANGS.md; Claude builds
    // them at the owner's word, logged for Astra). Its left click: five quick cuts, each leaving venom (stacks of the arena's poison), a pounce
    // on a sprint. Q a shadow step behind the foe nearest the aim with a stab; E a smoke veil (unseen by enemies until the next attack, which
    // strikes twice as hard); 1 a fan of venom knives; 2 the venom blooms; 3 an assassination: a paired takedown (both held through it), lethal
    // from behind or on a weakened foe, a frontal critical otherwise. Its ten chips (Core NativeFangsChips) change these rules here.
    public sealed partial class NativePilot { internal bool BladeVeilStrike;internal float BladeLandAt=-100,BladeRefillAt=-100; }
    public sealed partial class NativeArena
    {
        /// <summary>The Fangs' damage share on a blow at a foe on `hpShare` of its health: Predator's, the veil's strike.</summary>
        private float FangsShare(NativePilot p,float hpShare)
        {
            float s=1;if(BladeChip(p,NativeFangsChips.Predator)&&hpShare<NativeFangsChips.PreyBelow)s*=NativeFangsChips.PreyDamage;if(p.BladeVeilStrike)s*=NativeFangs.VeilBonus;return s;
        }
        private float FangsMove(NativeBladeKit k)=>k.CastKind==NativeFangs.Cut?(k.CastStep==4?.4f:.75f):k.CastKind==NativeFangs.Veil?.9f:k.CastKind==NativeFangs.Fan||k.CastKind==NativeFangs.Bloom?.6f:k.CastKind==NativeFangs.Assassinate?0:.5f;
        private int FangsVenom(NativePilot p,int add)=>add+(p.BladeVeilStrike?NativeFangs.VeilVenom:0);
        /// <summary>An attack begins: from the veil it is the veil's strike (twice as hard, more venom), and the veil ends (Silent Veil: not the first time).</summary>
        private void FangsStrike(NativePilot p)
        {
            var k=p.Blade;p.BladeVeilStrike=false;if(!BladeUnseen(p))return;p.BladeVeilStrike=true;
            if(BladeChip(p,NativeFangsChips.SilentVeil)&&!p.BladeVeilKept){p.BladeVeilKept=true;return;}
            k.VeilUntil=Time;
        }
        private float FangsStepRange(NativePilot p)=>NativeFangs.StepRange+(BladeChip(p,NativeFangsChips.LongShadow)?NativeFangsChips.ShadowFarther:0);
        private void FangsInput(NativePilot p,bool attack,bool press)
        {
            var k=p.Blade;if(BladeBusy(p)||Time<p.RollEnd||Time<p.BlockEnd||Time<p.Ready)return;
            float rate=Math.Max(.5f,(float)p.Stats["attackRate"]),cd=(float)p.Stats["abilityCooldown"];int key=p.Input.SirenAbility;
            // Long Shadow: two charges, each back on the cooldown.
            int charges=BladeChip(p,NativeFangsChips.LongShadow)?NativeFangsChips.ShadowCharges:1;
            if(charges>1&&Time>=p.BladeRefillAt)p.BladeCharges=charges;
            if(p.Input.AbilityQ&&(charges>1?p.BladeCharges>0&&Time>=p.BladeChargeAt:Time>=k.Ready[0]))
            {
                FangsStrike(p);BladeBegin(p,NativeFangs.Step,NativeFangs.StepVanish,NativeFangs.StepVanish+NativeFangs.StepRecover);k.ComboUntil=0;
                // The refill runs from the first charge spent; the card shows the next charge's moment.
                if(charges>1){if(p.BladeCharges==charges)p.BladeRefillAt=Time+NativeFangs.Cooldown[0]*cd;p.BladeCharges--;p.BladeChargeAt=Time+.4f;k.Ready[0]=p.BladeCharges>0?p.BladeChargeAt:p.BladeRefillAt;}else k.Ready[0]=Time+NativeFangs.Cooldown[0]*cd;
                return;
            }
            if(p.Input.AbilityE&&Time>=k.Ready[1])
            {
                p.BladeVeilStrike=false;BladeBegin(p,NativeFangs.Veil,NativeFangs.VeilCast,NativeFangs.VeilCast+.2f);k.ComboUntil=0;float lasts=NativeFangs.VeilFor+(BladeChip(p,NativeFangsChips.SilentVeil)?NativeFangsChips.SilentLonger:0);
                k.Ready[1]=k.CastRelease+lasts+NativeFangs.Cooldown[1]*cd;FangsTravel(p,-p.BladeFacing,NativeFangs.VeilHop);return;
            }
            if(key==1&&Time>=k.Ready[2]){FangsStrike(p);BladeBegin(p,NativeFangs.Fan,NativeFangs.FanRelease,NativeFangs.FanRelease+NativeFangs.FanRecover);k.ComboUntil=0;k.Ready[2]=Time+NativeFangs.Cooldown[2]*cd;return;}
            if(key==2&&Time>=k.Ready[3]){p.BladeVeilStrike=false;BladeBegin(p,NativeFangs.Bloom,NativeFangs.BloomRelease,NativeFangs.BloomRelease+NativeFangs.BloomRecover);k.ComboUntil=0;k.Ready[3]=Time+NativeFangs.Cooldown[3]*cd;FangsTravel(p,p.BladeFacing,NativeFangs.BloomLunge);return;}
            if(key==3&&Time>=k.Ready[4]&&FangsAssassinate(p,cd))return;
            if(!attack)return;
            if(p.Grounded&&Time>k.ComboUntil&&NativeMeleeFeel.SprintAttack(p.Input,p.Pose.Sprinting))
            {
                FangsStrike(p);BladeBegin(p,NativeFangs.Pounce,NativeFangs.PounceWind,NativeFangs.PounceWind+NativeFangs.PounceTime+NativeFangs.PounceRecover);k.ComboUntil=0;return;
            }
            int step=Time<=k.ComboUntil?(k.Combo+1)%NativeFangs.Steps:0;k.Combo=step;FangsStrike(p);
            BladeBegin(p,NativeFangs.Cut,NativeFangs.CutHit[step]/rate,NativeFangs.CutLength[step]/rate,step);k.ComboUntil=k.CastUntil+NativeFangs.ComboWindow;
            FangsTravel(p,p.BladeFacing,NativeFangs.CutLunge[step]);
        }
        /// <summary>A move that carries the pilot as its clip does (owner 2026-11-13: "The movements look like they should be moving the
        /// character location"; rules at the owner's word, logged for Astra): `metres` along `dir` (flat) from where the pilot stands, on the
        /// blade classes' own path (BladePath: a wall stops it short; the host moves the pilot, guests see it in the snapshots), timed by the
        /// clip's root (Core NativeBladeMotion.FangsTravelShare, through NativeBlades.PathAt) to the end of the slice the game plays. Only from
        /// the ground (a cut in the air does not hover along it), never off a drop (FangsFloorTo), and never through a foe: one in the way
        /// stops it at arm's length (FangsTravelFoe), so a lunge ends on the one it cuts.</summary>
        private void FangsTravel(NativePilot p,Vector3 dir,float metres)
        {
            var k=p.Blade;if(!(metres>0)||!p.Grounded||!NativeBladeMotion.FangsTravelSpan(k,out float ends,out float peak))return;
            dir.y=0;dir=dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.forward;var from=p.Body.position;
            BladePath(p,dir,metres*peak,Time,ends);float way=Math.Min(Math.Min(Vector3.Distance(from,Vec(k.To)),FangsFloorTo(from,dir,metres*peak)),FangsTravelFoe(p,from,dir));
            k.To=Vec(from+dir*(way/peak));
        }
        /// <summary>How far along `dir` from `from` the nearest foe in the way (within a body's width of the line) lets the pilot go: to
        /// FangsArm short of it (unlimited with none).</summary>
        private float FangsTravelFoe(NativePilot p,Vector3 from,Vector3 dir)
        {
            float best=float.MaxValue;
            void At(Vector3 q){var d=q-from;if(Math.Abs(d.y)>1.8f)return;d.y=0;float along=Vector3.Dot(d,dir);if(along<=0||(d-dir*along).magnitude>FangsWidth)return;best=Math.Min(best,Math.Max(0,along-FangsArm));}
            foreach(var q in BladePilots(p))At(q.Body.position);foreach(var m in BladeMinions(p))At(m.Body.position);return best;
        }
        private const float FangsWidth=.8f,FangsArm=.9f;
        /// <summary>How far along `dir` from `from` (at most `distance`) there is floor to stand on: the first point with none within a metre
        /// under the feet (a ledge, a lethal edge) ends the way short of it.</summary>
        private float FangsFloorTo(Vector3 from,Vector3 dir,float distance)
        {
            var physics=root.scene.GetPhysicsScene();
            for(float d=.25f;;d+=.25f)
            {
                float at=Math.Min(d,distance);if(!physics.Raycast(from+dir*at+Vector3.up*.3f,Vector3.down,out _,1.3f+NativeLocomotionRules.HalfHeight,WorldMask,QueryTriggerInteraction.Ignore))return Math.Max(0,at-.45f);
                if(at>=distance)return distance;
            }
        }
        private void FangsRelease(NativePilot p)
        {
            var k=p.Blade;
            switch(k.CastKind)
            {
                case NativeFangs.Cut:FangsCut(p,k.CastStep);break;
                case NativeFangs.Pounce:BladePath(p,p.BladeFacing,NativeFangs.PounceReach,Time,Time+NativeFangs.PounceTime);p.BladeLandAt=Time+NativeFangs.PounceTime;BladeHit(p,NativeFangs.HitPounce,p.Body.position+Vector3.up*.3f,p.BladeFacing);break;
                case NativeFangs.Step:FangsStep(p);break;
                case NativeFangs.Veil:
                {
                    float lasts=NativeFangs.VeilFor+(BladeChip(p,NativeFangsChips.SilentVeil)?NativeFangsChips.SilentLonger:0);k.VeilAt=Time;k.VeilUntil=Time+lasts;p.BladeVeilKept=false;
                    BladeHit(p,NativeFangs.HitSmoke,p.Body.position+Vector3.up*.2f,Vector3.up);
                    // Smokescreen: a cloud where the veil fell.
                    if(BladeChip(p,NativeFangsChips.Smokescreen)){p.BladeClouds.Add(new BladeCloud{At=p.Body.position,Until=Time+NativeFangsChips.CloudFor});BladeHit(p,NativeFangs.HitCloud,p.Body.position,Vector3.up);}
                    break;
                }
                case NativeFangs.Fan:
                {
                    bool hydra=BladeChip(p,NativeFangsChips.Hydra);int n=NativeFangs.FanKnives+(hydra?NativeFangsChips.HydraMore:0);float spread=NativeFangs.FanSpread*(hydra?1.6f:1);
                    var from=p.Body.position+Vector3.up*.55f;var aim=new System.Numerics.Vector3(p.BladeAim.x,0,p.BladeAim.z);
                    for(int i=0;i<n;i++){var d=NativeFangs.KnifeDir(aim,i,n,spread);p.BladeKnives.Add(new BladeKnife{Pos=from,Dir=new Vector3(d.X,0,d.Z),Pierce=hydra?1:0});}
                    BladeHit(p,NativeFangs.HitThrow,from,p.BladeAim,0,n);break;
                }
                case NativeFangs.Bloom:FangsBloom(p);break;
            }
        }
        /// <summary>A cut: a cone ahead, venom on each one it cuts (the finisher more; Twin Venom more again).</summary>
        private void FangsCut(NativePilot p,int step)
        {
            // Its reach from here, plus whatever of its step in it has not travelled by the hit (in the air, at a wall), so it lands where it would.
            var k=p.Blade;var at=p.Body.position;var f=p.BladeFacing;float travelled=k.PathAt==k.CastAt&&k.PathUntil>k.PathAt?new Vector2(at.x-k.From.x,at.z-k.From.z).magnitude:0;
            float reach=NativeFangs.CutReachAt(step,NativeBladeMotion.FangsTravelShare(k,k.CastRelease,out float share)?share:0,travelled),arc=NativeFangs.CutArc[step],damage=NativeFangs.CutDamage[step];bool finisher=step==NativeFangs.Steps-1;
            int venom=FangsVenom(p,finisher&&BladeChip(p,NativeFangsChips.TwinVenom)?NativeFangsChips.TwinFinisher:NativeFangs.CutVenom[step]);string kind=finisher?NativeFangs.HitFinisher:NativeFangs.HitCut;
            BladeCone(p,at,f,reach,arc,(q,d)=>{if(BladeHurt(p,q,damage,d,"cut",(float)p.Weapon.impulse,true)<=0)return;StaggerMelee(q,NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"]));int n=AddVenom(p,q,venom);BladeHit(p,kind,q.Body.position+Vector3.up*.45f,d,q.Seat.id,n);},
                (m,d)=>{if(BladeHurt(p,m,damage,d,"cut",0,true)<=0)return;int n=AddVenom(p,m,venom);BladeHit(p,kind,m.Body.position+Vector3.up*.45f,d,BladeKey(m),n);});
        }
        /// <summary>The shadow step: behind the foe nearest the aim (in range, in the cone, in sight), facing it, and the stab; else a blink along the look.</summary>
        private void FangsStep(NativePilot p)
        {
            var k=p.Blade;var eye=p.Body.position+Vector3.up*.5f;var look=p.BladeAim;float range=FangsStepRange(p),best=float.MaxValue;NativePilot target=null;NativeMinion minion=null;
            bool Candidate(Vector3 pos,Collider c,out float score){score=0;var d=pos-p.Body.position;float l=d.magnitude;if(l>range||l<.5f)return false;float ang=Vector3.Angle(new Vector3(look.x,0,look.z),new Vector3(d.x,0,d.z));if(ang>NativeFangs.StepCone)return false;if(!MeleeVisible(eye,pos,p,c))return false;score=ang*.08f+l*.04f;return true;}
            foreach(var q in BladePilots(p)){if(BladeHeld(q)||!Candidate(q.Body.position,q.Collider,out float s)||s>=best)continue;best=s;target=q;minion=null;}
            foreach(var m in BladeMinions(p)){if(!Candidate(m.Body.position,m.Collider,out float s)||s>=best)continue;best=s;minion=m;target=null;}
            var from=p.Body.position;BladeHit(p,NativeFangs.HitVanish,from+Vector3.up*.9f,look);
            if(target==null&&minion==null)
            {
                var dir=new Vector3(look.x,0,look.z);dir=dir.sqrMagnitude>1e-6f?dir.normalized:p.BladeFacing;var to=KernelTravel(from,from+dir*NativeFangs.StepBlink);p.Body.position=to;
                BladeHit(p,NativeFangs.HitVanish,to+Vector3.up*.9f,dir,0,1);return;
            }
            Vector3 tp=target!=null?target.Body.position:minion.Body.position;float yaw=target!=null?target.Yaw:minion.Yaw;var facing=NativeBlades.Facing(yaw);var back=new Vector3(-facing.X,0,-facing.Z);
            var spot=KernelTravel(tp,tp+back*NativeFangs.StepBehind);if((spot-tp).magnitude<.5f){var side=Vector3.Cross(Vector3.up,back).normalized;spot=KernelTravel(tp,tp+side*NativeFangs.StepBehind);}
            p.Body.position=new Vector3(spot.x,Math.Max(spot.y,tp.y),spot.z);if(!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;
            var face=tp-p.Body.position;face.y=0;if(face.sqrMagnitude>1e-4f){p.Yaw=Mathf.Atan2(face.x,face.z)*Mathf.Rad2Deg;p.BladeFacing=face.normalized;}
            BladeHit(p,NativeFangs.HitVanish,p.Body.position+Vector3.up*.9f,p.BladeFacing,0,1);
            int venom=FangsVenom(p,NativeFangs.StepVenom);
            if(target!=null){if(BladeHurt(p,target,NativeFangs.StepDamage,p.BladeFacing,"step",0,true)>0){StaggerMelee(target,.35f);int n=AddVenom(p,target,venom);BladeHit(p,NativeFangs.HitStab,target.Body.position+Vector3.up*.6f,p.BladeFacing,target.Seat.id,n);}}
            else if(BladeHurt(p,minion,NativeFangs.StepDamage,p.BladeFacing,"step",0,true)>0){int n=AddVenom(p,minion,venom);BladeHit(p,NativeFangs.HitStab,minion.Body.position+Vector3.up*.5f,p.BladeFacing,BladeKey(minion),n);}
        }
        /// <summary>The bloom: every poisoned foe within range takes the stacks at once, spent (Chain Bloom: a kill leaps its stacks on).</summary>
        private void FangsBloom(NativePilot p)
        {
            var at=p.Body.position;BladeHit(p,NativeFangs.HitBloom,at+Vector3.up*.9f,Vector3.up);bool chain=BladeChip(p,NativeFangsChips.ChainBloom);
            foreach(var id in p.BladeVenom.Keys.ToArray())
            {
                var q=Pilots.Find(x=>x.Seat.id==id);var m=q==null?Minions.Find(x=>BladeKey(x)==id):null;var pos=q!=null?q.Body.position:m!=null?m.Body.position:Vector3.zero;
                if(q==null&&m==null||Vector3.Distance(pos,at)>NativeFangs.BloomRange||(q!=null?q.Hp<=0||!CanHit(p,q):m.Hp<=0))continue;
                int n=SpendVenom(p,id);if(n<=0)continue;float damage=n*NativeFangs.BloomPerStack;bool killed;
                if(q!=null){BladeHurt(p,q,damage,pos-at,"bloom");killed=q.Hp<=0;}else{BladeHurt(p,m,damage,pos-at,"bloom");killed=m.Hp<=0;}
                BladeHit(p,NativeFangs.HitBloom,pos+Vector3.up*.6f,pos-at,id,n);
                if(killed&&chain)
                {
                    foreach(var o in BladePilots(p))if(o!=q&&Vector3.Distance(o.Body.position,pos)<=NativeFangsChips.ChainRange){int c=AddVenom(p,o,n);BladeHit(p,NativeFangs.HitSpread,o.Body.position+Vector3.up*.5f,o.Body.position-pos,o.Seat.id,c);}
                    foreach(var o in BladeMinions(p))if(o!=m&&Vector3.Distance(o.Body.position,pos)<=NativeFangsChips.ChainRange){int c=AddVenom(p,o,n);BladeHit(p,NativeFangs.HitSpread,o.Body.position+Vector3.up*.4f,o.Body.position-pos,BladeKey(o),c);}
                }
            }
        }
        /// <summary>The assassination: the foe in front within reach; a takedown (lethal) from behind or below the threshold, a frontal critical
        /// otherwise; both held through it. A boss takes a strike instead and is never held.</summary>
        private bool FangsAssassinate(NativePilot p,float cd)
        {
            var k=p.Blade;var at=p.Body.position;var f=Vec(V3.Forward(p.Input.Yaw));NativePilot target=null;NativeMinion minion=null;float best=float.MaxValue;
            bool Fits(Vector3 pos,Collider c,out float l){var d=pos-at;d.y=0;l=d.magnitude;if(l>NativeFangs.KillReach+.4f)return false;if(l>.5f&&Vector3.Angle(f,d)>NativeFangs.KillCone)return false;return MeleeVisible(at+Vector3.up*.3f,pos,p,c);}
            foreach(var q in BladePilots(p)){if(BladeHeld(q)||q.DogHolder!=null||!Fits(q.Body.position,q.Collider,out float l)||l>=best)continue;best=l;target=q;minion=null;}
            foreach(var m in BladeMinions(p)){if(!Fits(m.Body.position,m.Collider,out float l)||l>=best)continue;best=l;minion=m;target=null;}
            if(target==null&&minion==null)return false;
            FangsStrike(p);var tp=target!=null?target.Body.position:minion.Body.position;var look=tp-at;look.y=0;if(look.sqrMagnitude>1e-4f)p.Input.Yaw=Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg;
            bool boss=target!=null&&(SirenArmor(target)||Pve?.Boss==target||target.Seat.AuthorityBoss)||minion!=null&&IsZoneBoss(minion);
            float threshold=BladeChip(p,NativeFangsChips.DeathMark)?NativeFangsChips.MarkThreshold:NativeFangs.KillThreshold;
            float share=target!=null?target.Hp/Math.Max(1,(float)target.Stats["hp"]):minion.Hp/Math.Max(1,minion.MaxHp);
            bool behind=NativeFangs.Behind(new System.Numerics.Vector3(at.x,at.y,at.z),new System.Numerics.Vector3(tp.x,tp.y,tp.z),NativeBlades.Facing(target!=null?target.Yaw:minion.Yaw));
            bool takedown=!boss&&(behind||share<threshold);
            if(boss)
            {
                BladeBegin(p,NativeFangs.Assassinate,NativeFangs.FrontHit,NativeFangs.FrontHold*.7f);k.ComboUntil=0;k.Ready[4]=Time+NativeFangs.Cooldown[4]*cd;p.BladePairVictim=null;p.BladePairMinion=null;
                k.Pair=target!=null?target.Seat.id:BladeKey(minion);k.PairClip=null;k.PairAt=Time;k.PairHit=Time+NativeFangs.FrontHit;k.PairUntil=-100;return true;
            }
            float hit=takedown?NativeFangs.BehindHit:NativeFangs.FrontHit,hold=takedown?NativeFangs.BehindHold:NativeFangs.FrontHold;
            BladeBegin(p,NativeFangs.Assassinate,hit,hold);k.ComboUntil=0;k.Ready[4]=Time+NativeFangs.Cooldown[4]*cd;
            BladeStartPair(p,target,minion,takedown?NativeBladeMotion.FangsTakedown:NativeBladeMotion.FangsCritical,hit,hold,NativeFangs.PairGap,!takedown,takedown);
            p.Ready=k.CastUntil;return true;
        }
        private void FangsTakedownBlow(NativePilot p,NativePilot q,NativeMinion m)
        {
            var f=p.BladeFacing;bool lethal=p.BladePairLethal;
            if(q!=null&&q.Hp>0)
            {
                float damage=lethal?q.Hp*4+50:NativeFangs.KillFront;if(BladeHurt(p,q,damage,f,lethal?"takedown":"critical",0,true)>0){int n=AddVenom(p,q,FangsVenom(p,2));BladeHit(p,q.Hp<=0?NativeFangs.HitKill:NativeFangs.HitTakedown,q.Body.position+Vector3.up*.8f,f,q.Seat.id,n);}
            }
            else if(m!=null&&m.Hp>0)
            {
                float damage=lethal?m.Hp*4+50:NativeFangs.KillFront;if(BladeHurt(p,m,damage,f,lethal?"takedown":"critical",0,true)>0)BladeHit(p,m.Hp<=0?NativeFangs.HitKill:NativeFangs.HitTakedown,m.Body.position+Vector3.up*.6f,f,BladeKey(m));
            }
        }
        private void FangsLanded(NativePilot p,int id,bool killed)
        {
            // Death Mark: a takedown kill resets the assassination.
            var k=p.Blade;if(killed&&BladeChip(p,NativeFangsChips.DeathMark)&&k.CastKind==NativeFangs.Assassinate&&Time<k.CastUntil+.05f)k.Ready[4]=Time;
        }
        private void FangsWorld(NativePilot p,bool able)
        {
            var k=p.Blade;
            // A boss's strike (no hold): the blow at its moment.
            if(k.CastKind==NativeFangs.Assassinate&&k.PairUntil<-50&&k.PairHit>-50&&Time>=k.PairHit&&p.BladePairVictim==null&&p.BladePairMinion==null&&k.Pair!=0)
            {
                var q=Pilots.Find(x=>x.Seat.id==k.Pair);var m=q==null?Minions.Find(x=>BladeKey(x)==k.Pair):null;k.Pair=0;k.PairHit=-100;
                if(q!=null&&q.Hp>0&&BladeHurt(p,q,NativeFangs.KillBoss,p.BladeFacing,"critical",0,true)>0)BladeHit(p,NativeFangs.HitTakedown,q.Body.position+Vector3.up*.8f,p.BladeFacing,q.Seat.id);
                else if(m!=null&&m.Hp>0&&BladeHurt(p,m,NativeFangs.KillBoss,p.BladeFacing,"critical",0,true)>0)BladeHit(p,NativeFangs.HitTakedown,m.Body.position+Vector3.up*.6f,p.BladeFacing,BladeKey(m));
            }
            if(able&&k.CastKind==NativeFangs.Pounce&&Time>=k.CastRelease&&Time<=k.CastRelease+NativeFangs.PounceTime+.04f)FangsPounceStep(p);
            if(p.BladeLandAt>-50&&Time>=p.BladeLandAt){p.BladeLandAt=-100;if(able)FangsPounceLand(p);}
            if(p.BladeKnives.Count>0)FangsKnifeStep(p);
            for(int i=p.BladeClouds.Count-1;i>=0;i--)if(Time>=p.BladeClouds[i].Until)p.BladeClouds.RemoveAt(i);
            VenomStep(p);
            if(p.Hp<=0&&k.VeilUntil>Time)k.VeilUntil=Time;
            k.Chips=0;foreach(var id in NativeFangsChips.Ids)if(BladeChip(p,id))k.Chips|=NativeFangsChips.Bit(id);
            k.ChipAt=p.BladeClouds.Count>0?p.BladeClouds[p.BladeClouds.Count-1].Until-NativeFangsChips.CloudFor:-100;
        }
        /// <summary>The dash: everyone it passes within PounceWidth of its line is cut once (two stacks of venom) and staggered.</summary>
        private void FangsPounceStep(NativePilot p)
        {
            int venom=FangsVenom(p,NativeFangs.PounceVenom);bool Beside(Vector3 d)=>new Vector2(d.x,d.z).magnitude<=NativeFangs.PounceWidth&&Math.Abs(d.y)<=1.8f;
            foreach(var q in BladePilots(p))
            {
                if(p.BladeStruck.Contains(q.Seat.id))continue;var d=q.Body.position-p.Body.position;if(!Beside(d)||!MeleeVisible(p.Body.position+Vector3.up*.3f,q.Body.position,p,q.Collider))continue;p.BladeStruck.Add(q.Seat.id);
                if(BladeHurt(p,q,NativeFangs.PounceDamage,d,"pounce",0,true)>0){StaggerMelee(q,.35f);int n=AddVenom(p,q,venom);BladeHit(p,NativeFangs.HitCut,q.Body.position+Vector3.up*.45f,p.BladeFacing,q.Seat.id,n);}
            }
            foreach(var m in BladeMinions(p))
            {
                int id=BladeKey(m);if(p.BladeStruck.Contains(id))continue;var d=m.Body.position-p.Body.position;if(!Beside(d))continue;p.BladeStruck.Add(id);
                if(BladeHurt(p,m,NativeFangs.PounceDamage,d,"pounce",0,true)>0){int n=AddVenom(p,m,venom);BladeHit(p,NativeFangs.HitCut,m.Body.position+Vector3.up*.45f,p.BladeFacing,id,n);}
            }
        }
        /// <summary>The dash's end: its dust ring (the cuts were along the way).</summary>
        private void FangsPounceLand(NativePilot p)=>BladeHit(p,NativeFangs.HitPounce,p.Body.position,Vector3.up,0,1);
        /// <summary>The fan's knives: each flies on, the first foe it meets cut (Hydra: it passes one), a wall stops it.</summary>
        private void FangsKnifeStep(NativePilot p)
        {
            bool veiled=p.BladeVeilStrike;
            for(int i=p.BladeKnives.Count-1;i>=0;i--)
            {
                var s=p.BladeKnives[i];float step=Math.Min(NativeFangs.FanSpeed*Rules.Dt,NativeFangs.FanRange-s.Travel);var from=s.Pos;var to=from+s.Dir*step;bool done=false;
                var wall=SweepNative(from,to-from,.06f,BladeWall);if(wall.HasValue){to=from+s.Dir*Math.Max(0,wall.Value.Distance-.05f);done=true;}
                foreach(var q in BladePilots(p))
                {
                    if(s.Struck.Contains(q.Seat.id)||BladeSegment(from,to,q.Body.position+Vector3.up*.2f)>NativeFangs.FanRadius+.35f)continue;s.Struck.Add(q.Seat.id);
                    if(BladeHurt(p,q,NativeFangs.FanDamage,s.Dir,"knife")>0){int n=AddVenom(p,q,1+(veiled?1:0));BladeHit(p,NativeFangs.HitKnife,q.Body.position+Vector3.up*.4f,s.Dir,q.Seat.id,n);}
                    if(s.Pierce--<=0){done=true;break;}
                }
                if(!done)foreach(var m in BladeMinions(p))
                {
                    int id=BladeKey(m);if(s.Struck.Contains(id)||BladeSegment(from,to,m.Body.position)>NativeFangs.FanRadius+.4f)continue;s.Struck.Add(id);
                    if(BladeHurt(p,m,NativeFangs.FanDamage,s.Dir,"knife")>0){int n=AddVenom(p,m,1);BladeHit(p,NativeFangs.HitKnife,m.Body.position+Vector3.up*.3f,s.Dir,id,n);}
                    if(s.Pierce--<=0){done=true;break;}
                }
                s.Pos=to;s.Travel+=step;if(done||s.Travel>=NativeFangs.FanRange-1e-4f)p.BladeKnives.RemoveAt(i);
            }
        }
    }
}
