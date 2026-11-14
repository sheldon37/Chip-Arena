#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The blade classes' rules against the real arena (docs/KATANA.md, docs/FANGS.md): the Katana public and the Fangs DEV; the Katana's cuts,
    // rush, iai (the dash, the marks, the delayed split), storm and counter (the paired hold); the Fangs' venom stacks, step, veil, fan, bloom
    // and takedown, its dash cutting through; the plunging slam from the air for both; their chips registered; the record over the real frame
    // codec. Run by the Editor build checks beside the Glaive's.
    public sealed partial class NativeArena
    {
        private void BladeClear(NativePilot p)
        {
            p.Blade=new NativeBladeKit{Ready=new float[NativeBlades.MaxSlots]};p.BladeHits.Clear();p.BladeStruck.Clear();p.BladeMarked.Clear();p.BladeAttackHeld=false;p.BladeReleased=false;
            p.BladeVenom.Clear();p.BladeVenomUntil.Clear();p.BladeVenomShow.Clear();p.BladeKnives.Clear();p.BladeClouds.Clear();p.BladeWaves.Clear();p.BladeBleedUntil.Clear();p.BladeBleedShow.Clear();p.BladeLaunched.Clear();
            p.BladeFirstSplit=p.BladeSecondSplit=-100;p.BladeCounterFoe=null;p.BladePairVictim=null;p.BladePairMinion=null;p.BladeHeldBy=null;p.BladeVeilStrike=false;p.BladeLandAt=-100;p.BladeRefillAt=-100;p.BladeCharges=1;p.BladeLastAttack=-100;
        }
        public void ValidateBlades()
        {
            var a=Pilots[0];var b=Pilots[1];int checks=0;
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            var bounds=Map?.bounds;var o=new Vector3(bounds==null?0:(bounds.minX+bounds.maxX)*.5f,100,bounds==null?0:(bounds.minZ+bounds.maxZ)*.5f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.layer=PropLayer;floor.transform.position=new Vector3(o.x,98.635f,o.z);floor.transform.localScale=new Vector3(80,1,80);
            void Check(bool ok,string label){if(!ok)throw new Exception("Native blades: "+label);checks++;}
            void Advance(float t){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Match.Time+t,fightAt=0});}
            // (Steps are whole 1/60 s frames: a move's length rounds down short of its end, so the next press would find it still busy.)
            const float Frame=.03f;
            void Step(float seconds){int n=Mathf.Max(1,Mathf.RoundToInt(seconds*60));for(int i=0;i<n;i++){Advance(1f/60);StepBladeWorld();foreach(var q in Pilots)PinBladeVictim(q);}}
            void Setup(string spec)
            {
                Advance(10);ChipFields.Clear();Match.Teams=false;Match.Cooperative=false;
                foreach(var p in Pilots)
                {
                    p.Seat.chips.Clear();BladeClear(p);p.Death=null;p.Seat.connected=p.Seat.alive=true;p.Seat.bot=false;p.Seat.spec=p==a?spec:"pistol";p.Seat.access=p==a&&spec==NativeFangs.Spec?grant:NativeAccess.Guest;p.Weapon=Content.Weapon(p.Seat.spec);
                    p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Hp=(float)p.Stats["hp"];p.Mana=p.Stamina=100;p.Ammo=6;p.Ready=p.BlockEnd=p.RollEnd=p.ReloadEnd=p.AttackEnd=p.StunEnd=p.ShieldUntil=p.SlowEnd=0;p.RiposteAt=-100;p.RiposteBy=-1;p.RiposteUntil=0;p.SlowFactor=1;p.Down=null;p.Dots.Clear();
                    p.Kernel=null;p.Technique=null;p.Swing=null;p.Special=null;p.AbilityCast=null;p.DevGod=false;p.Body.isKinematic=false;p.Body.useGravity=true;p.Collider.enabled=true;p.Yaw=0;
                    p.Body.position=o+new Vector3(p==a?0:p==b?0:6,0,p==a?0:p==b?2.2f:12);p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame();p.Grounded=true;p.StaggerImmune=0;
                    // (The Glaive's validation, run before this one in the same arena, leaves its pilot sprinting: a cut would be the rush.)
                    p.Pose.Sprinting=false;
                }
                Physics.SyncTransforms();
            }
            void Press(bool shoot=false,bool q=false,bool e=false,byte key=0){a.Ready=0;a.Input=new InputFrame{Shoot=shoot,Attack=shoot,AbilityQ=q,AbilityE=e,SirenAbility=key};StepBladeInput(a);}
            try
            {
                // ---- the Katana ----
                var row=Content.Weapon(NativeKatana.Spec);Check(row!=null&&!row.devOnly&&row.@class=="melee","the Katana is a public melee row");
                Check(NativeLoadoutText.InClass(row,"melee",false,!row.devOnly),"a guest can pick the Katana");
                Setup(NativeKatana.Spec);
                Press(shoot:true);Check(a.Blade.CastKind==NativeKatana.Cut&&a.Blade.CastStep==0&&a.Blade.CastRelease>a.Blade.CastAt,"the first cut winds up");Check(!TryBlock(a)&&!TryRoll(a),"a committed cut cannot block or roll");
                float hp=b.Hp;Step(NativeKatana.CutLength[0]+Frame);Check(b.Hp<hp,"the first cut lands");
                for(int step=1;step<NativeKatana.Steps;step++){a.BladeAttackHeld=false;Press(shoot:true);Check(a.Blade.CastStep==step,"cut "+step+" follows on");b.StaggerImmune=0;b.Body.position=a.Body.position+Vector3.forward*2.2f;Physics.SyncTransforms();hp=b.Hp;Step(NativeKatana.CutLength[step]+Frame);Check(b.Hp<hp,"cut "+step+" lands");}
                Advance(2);a.BladeAttackHeld=false;Press(shoot:true);Check(a.Blade.CastStep==0,"the combo lapses");
                // The rush on a sprint.
                Setup(NativeKatana.Spec);a.Pose.Sprinting=true;a.Input=new InputFrame{Shoot=true,Attack=true,Sprint=true,Z=1};StepBladeInput(a);Check(a.Blade.CastKind==NativeKatana.Rush,"a cut on a sprint is the rush");
                var start=a.Body.position;hp=b.Hp;Step(NativeKatana.RushWind+NativeKatana.RushTime+.05f);Check(a.Body.position.z>start.z+1.5f&&b.Hp<hp,"it rushes ahead and cuts the one in its path");
                // The iai: drawn, a dash along the aim past the foe, marked, the split a moment later.
                Setup(NativeKatana.Spec);b.Body.position=o+new Vector3(0,0,3.5f);Physics.SyncTransforms();Press(q:true);Check(a.Blade.CastKind==NativeKatana.Iai&&a.Blade.Ready[0]>Time+10,"Q draws, its cooldown held until the split");
                hp=b.Hp;Step(NativeKatana.IaiDraw+NativeKatana.IaiDash+.05f);Check(a.Body.position.z>o.z+4&&a.BladeMarked.Contains(b.Seat.id)&&b.Hp==hp,"the dash passes the foe and marks it, unhurt yet");
                Step(NativeKatana.IaiSplit+.05f);Check(b.Hp<hp&&a.Blade.Hits.Any(h=>h.Kind==NativeKatana.HitSplit&&h.Victim==b.Seat.id),"the line splits and cuts it");Check(a.Blade.Ready[0]>Time&&a.Blade.Ready[0]<=Time+NativeKatana.Cooldown[0]+.1f,"Q's cooldown from the split");
                // The storm: cuts all round, the last a launch.
                Setup(NativeKatana.Spec);b.Body.position=o+new Vector3(-1.6f,0,-1f);Physics.SyncTransforms();Press(e:true);Check(a.Blade.CastKind==NativeKatana.Storm,"E is the storm");
                hp=b.Hp;Step(NativeKatana.StormWind+NativeKatana.StormEvery*NativeKatana.StormCuts+.1f);Check(b.Hp<hp-NativeKatana.StormDamage*2&&a.Blade.Hits.Any(h=>h.Kind==NativeKatana.HitLaunch),"it cuts the one beside again and again, then launches");
                // The counter: after a timed parry, the next cut holds the foe in the paired critical and strikes. Only the parry opens it (owner
                // 2026-11-11: "Katana / Dev katana for some reason able to parry even on normal blocks?"): a blow held on the raised guard (right
                // click) opens nothing and staggers no one; one inside the F parry's window opens the counter on its striker and reels it.
                Setup(NativeKatana.Spec);a.Input=new InputFrame{Guard=true};StepGuard(a);Check(a.Guarding,"the Katana raises its guard");
                Damage(a,b,20,Vector3.back,0,false,melee:true,weaponHit:true);Check(a.Blade.CounterUntil<=Time&&a.BladeCounterFoe==null&&b.StunEnd<=Time,"a blow held on the guard opens no counter and reels no one");
                a.Input=new InputFrame();StepGuard(a);Check(!a.Guarding&&TryBlock(a),"the guard down, the parry");
                Damage(a,b,20,Vector3.back,0,false,melee:true,weaponHit:true);Check(a.Blade.CounterUntil>Time&&a.BladeCounterFoe==b&&b.StunEnd>Time,"a blow on the parry opens the counter on its striker and reels it");
                // The standard parry's daze (owner 2026-11-12): the riposte's stun (BlockPunish, about a second; BlockStun was a third of one).
                Check(RiposteOpen(a)&&b.RiposteBy==a.Seat.id&&b.StunEnd-Time>=1f&&b.StunEnd-Time>NativeMeleeRules.BlockStun(20,0)*2,"the parry dazes its striker as a standard parry does ("+(b.StunEnd-Time).ToString("0.00")+" s, the riposte's)");
                // (The click comes once the parry's window has shut: no move starts inside it, KatanaInput.)
                Step(Mathf.Max(0f,a.BlockEnd-Time)+Frame);Check(Time>=a.BlockEnd&&a.Blade.CounterUntil>Time,"the parry's window shut, the counter still open");
                hp=b.Hp;Press(shoot:true);
                Check(a.Blade.CastKind==NativeKatana.Counter&&b.BladeHeldBy==a&&PinBladeVictim(b),"the cut is the counter, the foe held");Step(NativeKatana.CounterHold+.05f);Check(b.Hp<hp&&b.BladeHeldBy==null,"it strikes and lets go");
                {var gap=b.Body.position-a.Body.position;gap.y=0;Check(gap.magnitude>NativeKatana.CounterStand+.8f,"the foe thrown back as its clip carries it ("+gap.magnitude.ToString("0.00")+" m)");}
                // The riposte inside the window: once the parry has landed, the click is the counter at once and the window ends; the counter's
                // blow ends the daze (SettleRiposte), so a parry never chains into a lock.
                Setup(NativeKatana.Spec);Check(TryBlock(a),"the parry again");Damage(a,b,20,Vector3.back,0,false,melee:true,weaponHit:true);Check(RiposteOpen(a)&&Time<a.BlockEnd,"the riposte open inside the window");
                hp=b.Hp;Press(shoot:true);Check(a.Blade.CastKind==NativeKatana.Counter&&b.BladeHeldBy==a&&a.BlockEnd<=Time,"inside the window the click is the counter, the window ended");
                Step(NativeKatana.CounterHold+.05f);Check(b.Hp<hp&&b.BladeHeldBy==null&&b.StunEnd<=Time+NativeKatana.CounterHold&&b.RiposteBy<0,"its blow lands and ends the daze");
                // The record crosses the codec.
                Setup(NativeKatana.Spec);Press(q:true);Step(NativeKatana.IaiDraw+.05f);
                {var cf=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;Check(cf.Valid(Content),"the record crosses the codec");var ka=cf.snapshot.Actors.First(x=>x.Id==a.Seat.id).Blade;Check(ka!=null&&ka.CastKind==NativeKatana.Iai&&ka.Valid(),"the iai in the frame");}
                foreach(var id in NativeKatanaChips.Ids)Check(Content.chips.Any(x=>x.id==id&&ImplementedChip(x)),"live supported chip "+id);
                // Honed Edge: deeper cuts.
                Setup(NativeKatana.Spec);hp=b.Hp;Press(shoot:true);Step(NativeKatana.CutLength[0]+Frame);float plain=hp-b.Hp;
                Setup(NativeKatana.Spec);a.Seat.chips[NativeKatanaChips.HonedEdge]=2;hp=b.Hp;Press(shoot:true);Step(NativeKatana.CutLength[0]+Frame);Check(hp-b.Hp>plain*1.15f,"Honed Edge cuts deeper");
                // ---- the Fangs ----
                var frow=Content.Weapon(NativeFangs.Spec);Check(frow!=null&&frow.devOnly&&frow.@class=="melee","the Fangs are a DEV melee row");
                Setup(NativeFangs.Spec);a.Seat.access=NativeAccess.Guest;Press(shoot:true);Check(a.Blade.CastKind==null,"a guest's Fangs do nothing");
                Setup(NativeFangs.Spec);var cutFrom=a.Body.position;Press(shoot:true);Step(NativeFangs.CutLength[0]+Frame);Check(a.BladeVenom.TryGetValue(b.Seat.id,out int n1)&&n1==1&&b.Dots.ContainsKey(a.Seat.id+":venom"),"a cut leaves a venom stack");
                // The cuts carry the pilot as their clips' roots do (owner 2026-11-13: "The movements look like they should be moving the character location").
                Check(a.Body.position.z>cutFrom.z+NativeFangs.CutLunge[0]*.7f&&Mathf.Abs(a.Body.position.x-cutFrom.x)<.05f,"the first cut steps in along the facing ("+(a.Body.position.z-cutFrom.z).ToString("F2")+" m)");
                for(int step=1;step<NativeFangs.Steps;step++){a.BladeAttackHeld=false;b.StaggerImmune=0;b.Body.position=a.Body.position+Vector3.forward*1.8f;Physics.SyncTransforms();Press(shoot:true);Step(NativeFangs.CutLength[step]+Frame);}
                Check(a.BladeVenom.TryGetValue(b.Seat.id,out int n5)&&n5>=5,"the flurry stacks the venom ("+n5+")");Check(b.Body.position.z-a.Body.position.z>.6f,"its lunges stop on the foe, never through it ("+(b.Body.position.z-a.Body.position.z).ToString("F2")+" m apart)");
                // The bloom spends the stacks at once.
                hp=b.Hp;Press(key:2);Step(NativeFangs.BloomRelease+.05f);Check(b.Hp<hp-NativeFangs.BloomPerStack*3&&!a.BladeVenom.ContainsKey(b.Seat.id),"2 blooms the venom");
                // The step: behind the foe nearest the aim, and the stab.
                Setup(NativeFangs.Spec);b.Body.position=o+new Vector3(0,0,6);b.Yaw=180;Physics.SyncTransforms();hp=b.Hp;Press(q:true);Step(NativeFangs.StepVanish+.05f);
                Check(a.Body.position.z>b.Body.position.z+.6f&&b.Hp<hp,"Q steps behind the foe and stabs (at "+a.Body.position.ToString("F2")+", foe at "+b.Body.position.ToString("F2")+")");
                // The veil: unseen, the next cut twice as hard (once its 0.4 s cast is over), then seen again.
                Setup(NativeFangs.Spec);var veilFrom=a.Body.position;Press(e:true);Step(NativeFangs.VeilCast+.05f);Check(BladeUnseen(a),"E veils");
                {var bot=Pilots.Where(q=>q!=a&&q.Hp>0&&Enemy(b,q)&&!BladeUnseen(q)).ToList();Check(!bot.Contains(a),"bots pass over the veiled");}
                Step(.2f);Check(a.Body.position.z<veilFrom.z-NativeFangs.VeilHop*.85f,"its backward evade hops back as its clip does ("+(veilFrom.z-a.Body.position.z).ToString("F2")+" m)");
                // A drop ahead ends a move's travel short of it (the floor here ends 40 m out).
                {float floorTo=FangsFloorTo(o+new Vector3(0,0,39.2f),Vector3.forward,2);Check(floorTo>.2f&&floorTo<1f,"the travel stops short of a drop ("+floorTo.ToString("F2")+" m of 2)");Check(FangsFloorTo(o,Vector3.forward,2)==2,"and runs on over the floor");}
                b.Body.position=a.Body.position+Vector3.forward*1.6f;Physics.SyncTransforms();hp=b.Hp;a.BladeAttackHeld=false;Press(shoot:true);Step(NativeFangs.CutLength[0]+Frame);float veiled=hp-b.Hp;Check(!BladeUnseen(a)&&veiled>NativeFangs.CutDamage[0]*1.5f,"the veil's strike is twice as hard and ends it");
                // The fan: knives into the foe.
                Setup(NativeFangs.Spec);b.Body.position=o+new Vector3(0,0,8);Physics.SyncTransforms();hp=b.Hp;Press(key:1);Step(NativeFangs.FanRelease+.5f);Check(b.Hp<hp&&a.BladeVenom.ContainsKey(b.Seat.id),"1 fans knives into the foe");
                // The takedown from behind: both held, lethal at its blow.
                Setup(NativeFangs.Spec);b.Body.position=o+new Vector3(0,0,1.6f);b.Yaw=0;Physics.SyncTransforms();Press(key:3);
                Check(a.Blade.CastKind==NativeFangs.Assassinate&&b.BladeHeldBy==a&&a.BladePairLethal,"3 from behind is the takedown");Step(NativeFangs.BehindHold+.05f);Check(b.Hp<=0,"lethal at its blow");
                foreach(var id in NativeFangsChips.Ids)Check(Content.chips.Any(x=>x.id==id&&ImplementedChip(x)),"live supported chip "+id);
                // The dash on a sprint (owner 2026-11-11: the old leap was "VERY hard to hit"): it cuts the one close in its path, not only where it ends.
                Setup(NativeFangs.Spec);a.Pose.Sprinting=true;a.Input=new InputFrame{Shoot=true,Attack=true,Sprint=true,Z=1};StepBladeInput(a);Check(a.Blade.CastKind==NativeFangs.Pounce,"a cut on a sprint is the dash");
                hp=b.Hp;Step(NativeFangs.PounceWind+NativeFangs.PounceTime+.05f);Check(b.Hp<hp&&a.BladeVenom.ContainsKey(b.Seat.id),"the dash cuts the one close in its path and poisons it");
                // The plunging slam from the air (owner 2026-11-11: "Slam attacks" from the air): a moment airborne and looking down, a click is the
                // standard plunge (Core NativeAirAttackSimulation.KitSlam), the kit's own cut held back.
                foreach(var spec in new[]{NativeKatana.Spec,NativeFangs.Spec})
                {
                    Setup(spec);a.Body.useGravity=false;a.Body.position=o+new Vector3(0,3,0);a.Grounded=false;a.AirborneAt=Time-1;a.AirAttackUsed=false;Physics.SyncTransforms();
                    a.Ready=0;a.Input=new InputFrame{Shoot=true,Attack=true,Pitch=-80};StepBladeInput(a);StepMeleeInput(a);
                    Check(a.Swing!=null&&NativeAirAttackSimulation.Down(a.Swing)&&a.Blade.CastKind==null,spec+": airborne and looking down, a click is the plunging slam");
                    a.Body.useGravity=true;
                }
                Debug.Log("Native blades: "+checks+" checks");
            }
            finally{UnityEngine.Object.DestroyImmediate(floor);foreach(var p in Pilots){BladeClear(p);p.Seat.chips.Clear();p.Body.useGravity=true;p.Pose.Sprinting=false;p.Swing=null;p.AirAttackUsed=false;p.AirborneAt=-1;p.Grounded=true;}}
        }
    }
}
#endif
