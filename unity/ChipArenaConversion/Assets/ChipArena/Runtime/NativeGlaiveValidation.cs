#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The Glaive's rules against the real arena (docs/GLAIVE.md): public access, the four cuts and their chain, the skim, the throw's cut,
    // ricochet and return to the hand, the burst, the halo, a bot's use, its ten chips' rules (Core NativeGlaiveChips), and the record over
    // the real frame codec. Run by the Editor build checks beside the Top Dog's.
    public sealed partial class NativeArena
    {
        private void GlaiveClear(NativePilot p){p.Glaive=new NativeGlaiveKit{Ready=new float[NativeGlaive.Slots]};p.GlaiveHits.Clear();p.GlaiveStruck.Clear();p.GlaiveHaloNext.Clear();p.GlaiveAttackHeld=false;p.GlaiveReleased=false;p.GlaiveTwinNext.Clear();p.GlaiveShards.Clear();p.GlaiveBleedUntil.Clear();p.GlaiveBleedShow.Clear();p.GlaiveQuickUntil=-100;}
        public void ValidateGlaive()
        {
            var a=Pilots[0];var b=Pilots[1];var c=Pilots.Count>2?Pilots[2]:null;int checks=0;
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
                    p.Seat.chips.Clear();GlaiveClear(p);p.Death=null;p.Seat.connected=p.Seat.alive=true;p.Seat.bot=false;p.Seat.spec=p==a?NativeGlaive.Spec:"pistol";p.Seat.access=NativeAccess.Guest;p.Weapon=Content.Weapon(p.Seat.spec);
                    p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Hp=(float)p.Stats["hp"];p.Mana=p.Stamina=100;p.Ammo=6;p.Ready=p.BlockEnd=p.RollEnd=p.ReloadEnd=p.AttackEnd=p.StunEnd=p.ShieldUntil=p.SlowEnd=0;p.SlowFactor=1;p.Down=null;
                    p.Kernel=null;p.Technique=null;p.Swing=null;p.Special=null;p.AbilityCast=null;p.DevGod=false;p.Body.isKinematic=false;p.Body.useGravity=true;p.Collider.enabled=true;
                    p.Body.position=o+new Vector3(p==a?0:p==b?0:4,0,p==a?0:p==b?2.2f:9);p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame();p.Grounded=true;p.StaggerImmune=0;
                }
                Physics.SyncTransforms();
            }
            void Press(bool shoot=false,bool q=false,bool e=false){a.Ready=0;a.Input=new InputFrame{Shoot=shoot,Attack=shoot,AbilityQ=q,AbilityE=e};StepGlaiveInput(a);}
            try
            {
                var row=Content.Weapon(NativeGlaive.Spec);
                Check(row!=null&&!row.devOnly&&row.@class=="melee","public melee row");
                Check(NativeLoadoutText.InClass(row,"melee",false,!row.devOnly),"a guest can pick the Glaive");
                Check(!NativeLoadoutText.InClass(row,"dev",true,true),"the Glaive is no longer a DEV weapon");
                // All combat checks below run as a guest, without developer access.
                Setup();
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
                // ---- the chips (Core NativeGlaiveChips) ----
                void Give(string id,int n=1)=>a.Seat.chips[id]=n;
                foreach(var id in NativeGlaiveChips.Ids)Check(Content.chips.Any(x=>x.id==id&&ImplementedChip(x)),"live supported chip "+id);
                // Keen Edge: the first cut deeper, and it bleeds.
                Setup();b.Dots.Clear();hp=b.Hp;Press(shoot:true);Step(NativeGlaive.SwingLength[0]);float plainCut=hp-b.Hp;
                Setup();b.Dots.Clear();Give(NativeGlaiveChips.KeenEdge,2);hp=b.Hp;Press(shoot:true);Step(NativeGlaive.SwingLength[0]);
                Check(plainCut>0&&hp-b.Hp>plainCut*1.1f&&b.Dots.ContainsKey(a.Seat.id+":bleed")&&a.Glaive.Keen==2,"Keen Edge cuts deeper and they bleed ("+plainCut+" to "+(hp-b.Hp)+")");b.Dots.Clear();
                // Fourth Bounce: five ricochets, a longer look, more of the hit kept.
                Setup();Check(GlaiveRicochets(a)==NativeGlaive.Ricochets&&GlaiveRicochetScale(a)==NativeGlaive.RicochetScale,"three ricochets without it");
                Give(NativeGlaiveChips.FourthBounce);Check(GlaiveRicochets(a)==NativeGlaive.Ricochets+2&&Math.Abs(GlaiveRicochetRange(a)-(NativeGlaive.RicochetRange+4))<1e-4f&&GlaiveRicochetScale(a)==NativeGlaiveChips.BounceKeep,"Fourth Bounce: five, 4 m farther, 90%");
                // Twin Orbit: the second ring cuts one standing on its own circle.
                Setup();Give(NativeGlaiveChips.TwinOrbit);b.Body.position=o+new Vector3(0,0,NativeGlaive.HaloRadius+NativeGlaiveChips.TwinOut);Physics.SyncTransforms();Press(e:true);hp=b.Hp;Step(NativeGlaive.HaloRelease+1);
                Check(a.Glaive.Twin&&b.Hp<hp&&a.GlaiveTwinNext.ContainsKey(b.Seat.id),"Twin Orbit's ring cuts on its own circle");
                // Shatterpoint: the burst reaches farther and throws out its shards, which fly their way and are gone.
                Setup();Give(NativeGlaiveChips.Shatterpoint);b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();Press(q:true);Step(NativeGlaive.ThrowRelease+NativeGlaive.BurstAfterThrow+.1f);
                var bat=a.GlaivePos;b.Body.position=bat+new Vector3(NativeGlaive.BurstRadius+.6f,-.2f,0);Physics.SyncTransforms();hp=b.Hp;Press(q:true);
                Check(a.Glaive.Disc==NativeGlaive.Gone&&b.Hp<hp&&a.GlaiveShards.Count==NativeGlaiveChips.Shards,"Shatterpoint: the wider burst reaches 4 m out and throws six shards");
                Step(NativeGlaiveChips.ShardRange/NativeGlaiveChips.ShardSpeed+.1f);Check(a.GlaiveShards.Count==0,"the shards fly their 5 m and are gone");
                // Magnet Return: the returning ring drags the one it cuts toward the wielder.
                Setup();Give(NativeGlaiveChips.MagnetReturn);b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();Press(q:true);Step(NativeGlaive.ThrowRelease+.3f);
                a.Glaive.Disc=NativeGlaive.Back;a.GlaiveStruck.Clear();b.Body.position=(a.GlaivePos+GlaiveHand(a))*.5f-Vector3.up*.2f;b.Knockback=Vector3.zero;Physics.SyncTransforms();
                for(int i=0;i<60&&a.Glaive.Disc==NativeGlaive.Back;i++)Step(1f/60);
                Check(Vector3.Dot(b.Knockback,a.Body.position-b.Body.position)>0&&a.GlaiveHits.Any(h=>h.Kind==NativeGlaive.HitPull&&h.Victim==b.Seat.id),"Magnet Return drags them in");
                // Quick Catch: the throw's catch takes 2.5 s off Q and puts on speed.
                Setup();Give(NativeGlaiveChips.QuickCatch);b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();Press(q:true);Step(NativeGlaive.ThrowRelease+.2f);
                a.Glaive.Disc=NativeGlaive.Back;for(int i=0;i<240&&a.Glaive.Disc!=NativeGlaive.Hand;i++)Step(1f/60);
                Check(a.Glaive.Disc==NativeGlaive.Hand&&a.Glaive.Ready[0]<=Time+NativeGlaive.Cooldown[0]-NativeGlaiveChips.QuickCut+.05f&&a.GlaiveQuickUntil>Time&&GlaiveSpeed(a)==NativeGlaiveChips.QuickSpeed,"Quick Catch: Q back sooner, a burst of speed");
                // Whirlwind: the whirl drags them in as it winds up and reaches 3.3 m.
                Setup();Give(NativeGlaiveChips.Whirlwind);a.Glaive.Combo=1;a.Glaive.ComboUntil=Time+1;b.Body.position=o+new Vector3(0,0,3.3f);b.Knockback=Vector3.zero;Physics.SyncTransforms();hp=b.Hp;Press(shoot:true);
                Check(a.Glaive.CastStep==2&&b.Knockback.z<-1,"Whirlwind drags them in as it winds up");Step(NativeGlaive.SwingLength[2]);Check(b.Hp<hp,"and its whirl reaches them at 3.3 m");
                // Long Arc: the flick's loop goes out long and cuts one 8.5 m away.
                Setup();Give(NativeGlaiveChips.LongArc);a.Glaive.Combo=2;a.Glaive.ComboUntil=Time+1;b.Body.position=o+new Vector3(0,0,NativeGlaive.FlickReach*NativeGlaiveChips.ArcReach-.3f);Physics.SyncTransforms();hp=b.Hp;Press(shoot:true);
                Step(NativeGlaive.SwingHit[3]+.02f);Check(a.Glaive.Disc==NativeGlaive.Flick&&a.Glaive.LongArc,"Long Arc: the flick goes out long");Step(NativeGlaive.FlickTime);Check(b.Hp<hp,"and cuts one 8.5 m out");
                // Skimming Stone: farther, and a slick that slows.
                Setup();Give(NativeGlaiveChips.SkimmingStone);b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();a.Pose.Sprinting=true;a.Input=new InputFrame{Shoot=true,Attack=true,Sprint=true,Z=1};StepGlaiveInput(a);
                var skimFrom=a.Body.position;Step(NativeGlaive.SkimWind+NativeGlaive.SkimTime+.05f);
                Check(a.Body.position.z>skimFrom.z+NativeGlaive.SkimReach*1.2f&&NativeGlaiveChips.SlickOn(a.Glaive.SlickAt,Time),"Skimming Stone lunges farther and leaves its slick");
                b.Body.position=skimFrom+new Vector3(0,0,2);b.SlowEnd=0;b.SlowFactor=1;Physics.SyncTransforms();Step(.1f);Check(b.SlowEnd>Time&&Math.Abs(b.SlowFactor-NativeGlaiveChips.SlickSlow)<.01f,"the slick slows them 40%");
                // Crescent Guard: 25% less while the ring circles; the halo cuts down a shot it meets.
                Setup();Give(NativeGlaiveChips.CrescentGuard);b.Body.position=o+new Vector3(0,0,30);Physics.SyncTransforms();Press(e:true);Step(NativeGlaive.HaloRelease+.3f);
                hp=a.Hp;Damage(a,b,40,Vector3.forward,0,false,direct:false,primary:false);float guarded=hp-a.Hp;Check(Math.Abs(guarded-40*NativeGlaiveChips.GuardTaken*(float)a.Stats["damageTaken"])<1,"Crescent Guard: 25% less while the ring is out ("+guarded+")");
                {
                    var c0=new System.Numerics.Vector3(a.Body.position.x,a.Body.position.y,a.Body.position.z)+System.Numerics.Vector3.UnitY*.1f;var nx=NativeGlaive.OrbitAt(c0,NativeGlaive.N(a.Glaive.DiscDir),a.Glaive.DiscAt,Time+1f/60);
                    var shot=new NativeBullet{Id=nextEvent++,Owner=b.Seat.id,Position=new Vector3(nx.X,nx.Y,nx.Z),Velocity=new Vector3(1,0,0),Radius=.1f,Life=2};Bullets.Add(shot);Step(1f/60);
                    Check(!Bullets.Contains(shot)&&a.GlaiveHits.Any(h=>h.Kind==NativeGlaive.HitGuard),"the halo cuts down an enemy shot");Bullets.Remove(shot);
                }
                {var cf=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;Check(cf.Valid(Content),"the chips' record crosses the codec");}
                Debug.Log("native Glaive simulation: "+checks+" checks");
            }
            finally{UnityEngine.Object.DestroyImmediate(floor);foreach(var p in Pilots)GlaiveClear(p);}
        }
    }
}
#endif
