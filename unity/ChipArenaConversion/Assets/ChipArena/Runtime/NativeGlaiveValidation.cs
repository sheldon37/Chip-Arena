#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The Glaive's rules against the real arena (docs/GLAIVE.md): the DEV gate, the four cuts and their chain, the skim, the throw's cut,
    // ricochet and return to the hand, the burst, the halo, a bot's use, and the record over the real frame codec. Run by the Editor build
    // checks beside the Top Dog's.
    public sealed partial class NativeArena
    {
        private void GlaiveClear(NativePilot p){p.Glaive=new NativeGlaiveKit{Ready=new float[NativeGlaive.Slots]};p.GlaiveHits.Clear();p.GlaiveStruck.Clear();p.GlaiveHaloNext.Clear();p.GlaiveAttackHeld=false;p.GlaiveReleased=false;}
        public void ValidateGlaive()
        {
            var a=Pilots[0];var b=Pilots[1];var c=Pilots.Count>2?Pilots[2]:null;int checks=0;
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            var bounds=Map?.bounds;var o=new Vector3(bounds==null?0:(bounds.minX+bounds.maxX)*.5f,100,bounds==null?0:(bounds.minZ+bounds.maxZ)*.5f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.layer=PropLayer;floor.transform.position=new Vector3(o.x,98.635f,o.z);floor.transform.localScale=new Vector3(80,1,80);
            void Check(bool ok,string label){if(!ok)throw new Exception("Native Glaive: "+label);checks++;}
            void Advance(float t){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Match.Time+t,fightAt=0});}
            void Step(float seconds){int n=Mathf.Max(1,Mathf.RoundToInt(seconds*60));for(int i=0;i<n;i++){Advance(1f/60);StepGlaiveWorld();}}
            void Setup()
            {
                Advance(10);ChipFields.Clear();Match.Teams=false;Match.Cooperative=false;
                foreach(var p in Pilots)
                {
                    p.Seat.chips.Clear();GlaiveClear(p);p.Death=null;p.Seat.connected=p.Seat.alive=true;p.Seat.bot=false;p.Seat.spec=p==a?NativeGlaive.Spec:"pistol";p.Seat.access=p==a?grant:NativeAccess.Guest;p.Weapon=Content.Weapon(p.Seat.spec);
                    p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Hp=(float)p.Stats["hp"];p.Mana=p.Stamina=100;p.Ammo=6;p.Ready=p.BlockEnd=p.RollEnd=p.ReloadEnd=p.AttackEnd=p.StunEnd=p.ShieldUntil=p.SlowEnd=0;p.SlowFactor=1;p.Down=null;
                    p.Kernel=null;p.Technique=null;p.Swing=null;p.Special=null;p.AbilityCast=null;p.DevGod=false;p.Body.isKinematic=false;p.Body.useGravity=true;p.Collider.enabled=true;
                    p.Body.position=o+new Vector3(p==a?0:p==b?0:4,0,p==a?0:p==b?2.2f:9);p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame();p.Grounded=true;p.StaggerImmune=0;
                }
                Physics.SyncTransforms();
            }
            void Press(bool shoot=false,bool q=false,bool e=false){a.Ready=0;a.Input=new InputFrame{Shoot=shoot,Attack=shoot,AbilityQ=q,AbilityE=e};StepGlaiveInput(a);}
            try
            {
                var row=Content.Weapon(NativeGlaive.Spec);double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Check(row!=null&&row.devOnly&&row.@class=="melee","DEV melee row");
                Check(NativeLoadoutText.InClass(row,"dev",true,grant.Allows("weapon:"+row.id,now)),"a DEV account can pick the Glaive");
                Check(!NativeLoadoutText.InClass(row,"dev",false,NativeAccess.Guest.Allows("weapon:"+row.id,now)),"a guest cannot");
                // The cuts: a guest cannot; four that chain, the fourth a flick that comes back to the hand.
                Setup();a.Seat.access=NativeAccess.Guest;Press(shoot:true);Check(a.Glaive.CastKind==null,"a guest cannot cut");a.Seat.access=grant;
                Press(shoot:true);Check(a.Glaive.CastKind==NativeGlaive.Swing&&a.Glaive.CastStep==0&&a.Glaive.CastRelease>a.Glaive.CastAt,"the first cut winds up");Check(!TryBlock(a)&&!TryRoll(a),"a committed cut cannot block or roll");
                float hp=b.Hp;Step(NativeGlaive.SwingLength[0]);Check(b.Hp<hp,"the first cut lands");
                a.GlaiveAttackHeld=false;Press(shoot:true);Check(a.Glaive.CastStep==1,"the second follows on");b.StaggerImmune=0;hp=b.Hp;Step(NativeGlaive.SwingLength[1]);Check(b.Hp<hp,"the backhand lands");
                a.GlaiveAttackHeld=false;Press(shoot:true);Check(a.Glaive.CastStep==2,"the third is the whirl");b.StaggerImmune=0;b.Body.position=o+new Vector3(0,0,-1.8f);Physics.SyncTransforms();hp=b.Hp;Step(NativeGlaive.SwingLength[2]);Check(b.Hp<hp,"the whirl cuts behind too");
                b.Body.position=o+new Vector3(0,0,4.5f);Physics.SyncTransforms();
                a.GlaiveAttackHeld=false;Press(shoot:true);Check(a.Glaive.CastStep==3,"the fourth is the flick");Step(NativeGlaive.SwingHit[3]+.02f);Check(a.Glaive.Disc==NativeGlaive.Flick,"the ring leaves the hand on its loop");
                b.StaggerImmune=0;hp=b.Hp;Step(NativeGlaive.FlickTime);Check(a.Glaive.Disc==NativeGlaive.Hand&&b.Hp<hp,"it cuts out on its loop and is caught again");
                Advance(2);a.GlaiveAttackHeld=false;Press(shoot:true);Check(a.Glaive.CastStep==0,"the combo lapses");
                // The skim on a sprint.
                Setup();a.Pose.Sprinting=true;a.Input=new InputFrame{Shoot=true,Attack=true,Sprint=true,Z=1};StepGlaiveInput(a);Check(a.Glaive.CastKind==NativeGlaive.Skim,"a cut on a sprint is the skim");
                var start=a.Body.position;hp=b.Hp;Step(NativeGlaive.SkimWind+NativeGlaive.SkimTime+.05f);Check(a.Body.position.z>start.z+1.5f&&b.Hp<hp,"it lunges ahead and cuts the one in its path");
                // The throw: cuts the first, ricochets to the second, comes back to the hand; Q waits for the catch.
                Setup();b.Body.position=o+new Vector3(0,0,7);if(c!=null)c.Body.position=o+new Vector3(4,0,9);Physics.SyncTransforms();
                Press(q:true);Check(a.Glaive.CastKind==NativeGlaive.Throw&&a.Glaive.Ready[0]>Time+10,"Q throws, its cooldown held until the catch");
                hp=b.Hp;float hc=c?.Hp??0;Step(NativeGlaive.ThrowRelease+.02f);Check(a.Glaive.Disc==NativeGlaive.Out,"the ring is out");Step(.4f);
                Check(b.Hp<hp&&a.Glaive.Hits.Any(h=>h.Kind==NativeGlaive.HitThrow&&h.Victim==b.Seat.id),"it cuts the first in its way");
                if(c!=null){Step(.5f);Check(c.Hp<hc&&a.Glaive.Hits.Any(h=>h.Kind==NativeGlaive.HitRicochet&&h.Victim==c.Seat.id),"it ricochets to the next in sight");}
                for(int i=0;i<240&&a.Glaive.Disc!=NativeGlaive.Hand;i++)Step(1f/60);Check(a.Glaive.Disc==NativeGlaive.Hand&&a.Glaive.Hits.Any(h=>h.Kind==NativeGlaive.HitCatch),"it comes back to the hand");
                Check(a.Glaive.Ready[0]>Time&&a.Glaive.Ready[0]<=Time+NativeGlaive.Cooldown[0]+.05f,"Q's cooldown from the catch");
                // The burst: Q again while it is out.
                Setup();b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();Press(q:true);Step(NativeGlaive.ThrowRelease+NativeGlaive.BurstAfterThrow+.1f);Check(a.Glaive.Disc==NativeGlaive.Out,"out again");
                var at=a.GlaivePos;b.Body.position=at+new Vector3(1.5f,-.2f,0);Physics.SyncTransforms();hp=b.Hp;Press(q:true);
                Check(a.Glaive.Disc==NativeGlaive.Gone&&b.Hp<hp&&a.GlaiveHits.Any(h=>h.Kind==NativeGlaive.HitBurst),"Q while it is out bursts it, hurting round it (disc "+a.Glaive.Disc+", hp "+hp+" to "+b.Hp+", burst at "+at.ToString("F2")+", b at "+b.Body.position.ToString("F2")+", seen "+MeleeVisible(at,b.Body.position,a,b.Collider)+", since "+(Time-a.Glaive.DiscAt).ToString("F2")+")");
                Step(NativeGlaive.Reform+.03f);Check(a.Glaive.Disc==NativeGlaive.Hand&&a.Glaive.Reformed,"it re-forms in the hand");
                // The halo: circles, cuts, comes back.
                Setup();b.Body.position=o+new Vector3(NativeGlaive.HaloRadius,0,0);Physics.SyncTransforms();Press(e:true);Check(a.Glaive.CastKind==NativeGlaive.Halo&&a.Glaive.Ready[1]>Time+NativeGlaive.HaloTime,"E sets the halo");
                hp=b.Hp;Step(NativeGlaive.HaloRelease+1);Check(a.Glaive.Disc==NativeGlaive.Orbit&&b.Hp<hp,"it circles and cuts the one beside");
                Step(NativeGlaive.HaloTime);for(int i=0;i<120&&a.Glaive.Disc!=NativeGlaive.Hand;i++)Step(1f/60);Check(a.Glaive.Disc==NativeGlaive.Hand,"and comes back");
                // A bot throws at range; the record crosses the codec and the clone.
                Setup();var bot=new InputFrame();BotGlaive(a,ref bot,o+new Vector3(0,0,9),9,true);Check(bot.AbilityQ,"a bot throws at range");
                b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();Press(q:true);Step(NativeGlaive.ThrowRelease+.1f);
                {var cf=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;Check(cf.Valid(Content),"the record crosses the codec");var ga=cf.snapshot.Actors.First(x=>x.Id==a.Seat.id).Glaive;Check(ga!=null&&(ga.Disc==NativeGlaive.Out||ga.Disc==NativeGlaive.Back)&&ga.Valid(),"the ring's flight in the frame (decoded "+ga?.Disc+", host "+a.Glaive.Disc+", frame "+NetworkFrame().snapshot.Actors.First(x=>x.Id==a.Seat.id).Glaive?.Disc+", cast "+a.Glaive.CastKind+")");}
                var empty=NativeFrameCodec.Clone(NetworkFrame());Check(empty.snapshot.Actors.First(x=>x.Id==b.Seat.id).Glaive==null,"a null kit survives the clone");
                Debug.Log("native Glaive simulation: "+checks+" checks");
            }
            finally{UnityEngine.Object.DestroyImmediate(floor);foreach(var p in Pilots)GlaiveClear(p);}
        }
    }
}
#endif
