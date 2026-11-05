#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateBombadier()
        {
            var a=Pilots[0];var b=Pilots[1];int checks=0;
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.layer=PropLayer;floor.transform.position=new Vector3(1000,98.635f,1000);floor.transform.localScale=new Vector3(80,1,80);
            void Check(bool ok,string label){if(!ok)throw new Exception("Native Bombadier: "+label);checks++;}
            void Advance(float t){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Match.Time+t,fightAt=0});}
            void Setup()
            {
                Advance(10);ChipFields.Clear();Match.Teams=false;Match.Cooperative=false;
                foreach(var p in Pilots){p.Seat.chips.Clear();p.BombMineCapacity=2;p.BombReleased=true;p.BombStruck=false;p.BombFlights.Clear();p.BombChains.Clear();p.Bomb=new NativeBombKit{MineCharges=2,Ready=new float[6]};p.BombMineReady=0;p.BombNoSteerUntil=0;p.Seat.connected=p.Seat.alive=true;p.Seat.bot=false;p.Seat.spec=p==a?NativeBombs.Spec:"pistol";p.Seat.access=p==a?grant:NativeAccess.Guest;p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Hp=(float)p.Stats["hp"];p.Mana=p.Stamina=100;p.Ammo=6;p.Ready=p.BlockEnd=p.RollEnd=p.ReloadEnd=p.AttackEnd=p.StunEnd=p.ShieldUntil=0;p.Down=null;p.Kernel=null;p.Technique=null;p.Swing=null;p.Special=null;p.AbilityCast=null;p.DevGod=false;p.Body.isKinematic=false;p.Collider.enabled=true;p.Body.position=new Vector3(1000,100,p==a?1000:1007);p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame();}
                Physics.SyncTransforms();
            }
            void Cast(byte n=0,bool q=false,bool e=false,bool shoot=false){a.Input=new InputFrame{SirenAbility=n,AbilityQ=q,AbilityE=e,Shoot=shoot};StepBombInput(a);}
            void Release(){Advance(.3f);StepBombWorld();}
            try
            {
                var row=Content.Weapon(NativeBombs.Spec);double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Check(row!=null&&row.devOnly&&NativeLoadoutText.Name("dev")=="???","DEV picker row");
                Check(NativeLoadoutText.InClass(row,"dev",true,grant.Allows("weapon:"+row.id,now)),"DEV account can select Bombadier under ???");
                Check(!NativeLoadoutText.InClass(row,"dev",false,NativeAccess.Guest.Allows("weapon:"+row.id,now)),"guest cannot select Bombadier");
                Setup();a.Seat.access=NativeAccess.Guest;Cast(shoot:true);Check(a.Bomb.CastKind==null&&a.Ammo==6,"guest cannot fire");a.Seat.access=grant;Cast(shoot:true);Check(a.Ammo==5&&a.Bomb.CastRelease-a.Bomb.CastAt>=.15f,"belt and windup");Check(!TryBlock(a)&&!TryRoll(a),"committed throw cannot block/roll");Advance(.1f);StepBombWorld();Check(a.BombFlights.Count==0,"no projectile before hand release");Release();Check(a.BombFlights.Count==1&&a.BombFlights[0].Record.At==a.Bomb.CastRelease,"projectile clock is release");
                var bombFrame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;
                var sampled=bombFrame.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Bombs.Single();Check(sampled.SampleAt==Time&&sampled.At==a.Bomb.CastRelease,"bomb sample and release clocks survive real frame codec");
                sampled.SampleAt=float.NaN;Check(!bombFrame.Valid(Content),"wire rejects corrupt bomb sample clock");
                Setup();BombBlast(a,NativeBombs.Pop,a.Body.position,2.4f,12,5);Check(Math.Abs(a.Hp-87)<.001&&a.Body.linearVelocity.y>=5,"quarter self damage/full launch");
                Setup();Cast(q:true);Release();Check(a.Hp==90&&a.Body.linearVelocity.y>20&&a.Bomb.Ready[0]>Time,"blast jump nine metre rise and no self damage");
                Setup();for(int n=0;n<4;n++){BombBlast(a,NativeBombs.Sticky,b.Body.position,3,16,5);Advance(.2f);}Check(a.BombChains[b.Seat.id].Scorched(Time)&&Math.Abs(b.Hp-54.4f)<.01,"four-hit chain scales 1/.8/.6/.45");b.Body.linearVelocity=Vector3.zero;BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(b.Body.linearVelocity==Vector3.zero&&Math.Abs(b.Hp-48.4f)<.01,"Scorched halves damage and prevents launch");a.Stats["damage"]=100;BombBlast(a,NativeBombs.Column,b.Body.position,4,40,12);Check(b.Hp>=40,"post-modifier chain budget survives damage modifiers");Advance(2.6f);BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(b.Hp<40,"budget expires after escape window");
                Setup();Cast(1);Release();Check(a.Bomb.MineCharges==1&&a.BombFlights.Single().Record.Kind==NativeBombs.Mine,"mine charge spent");var mine=a.BombFlights.Single();mine.Landed=true;mine.Record.Pos=Vec(b.Body.position);mine.Record.ArmAt=Time+1;mine.Record.Vel=default;StepBombWorld();Check(a.BombFlights.Count==1,"unarmed mine cannot trigger");Advance(1.01f);StepBombWorld();Check(a.BombFlights.Count==0&&b.Hp==94&&b.BombNoSteerUntil>Time,"armed proximity launch and half-second no steering");Advance(7);StepBombWorld();Check(a.Bomb.MineCharges==2,"mine recharge");
                Setup();AddBomb(a,NativeBombs.Mine,new Vector3(1000,100,1003),Vector3.zero,Time,20);Check(TechniqueObstacle(b,new Vector3(1000,100,1004),Vector3.back*2,.05f,14,out _)&&a.BombFlights.Count==1,"mine takes shot damage");Check(TechniqueObstacle(b,new Vector3(1000,100,1004),Vector3.back*2,.05f,1,out _)&&a.BombFlights.Count==0,"15 HP mine detonates when shot");
                Setup();AddBomb(a,NativeBombs.Mine,a.Body.position+Vector3.forward*3,Vector3.zero,Time-1,-1);TechniqueBlastObjects(b,a.Body.position+Vector3.forward*3,2,1);Check(a.BombFlights[0].Record.BlowAt>Time,"other blast schedules mine handoff");
                Setup();AddBomb(a,NativeBombs.Mine,a.Body.position+Vector3.forward*8,Vector3.zero,Time-1,-1);AddBomb(a,NativeBombs.Mine,a.Body.position+Vector3.forward*3,Vector3.zero,Time-1,-1);Cast(e:true);Release();var sorted=a.BombFlights.OrderBy(x=>x.Record.Pos.z).ToArray();Check(sorted.Length==1&&sorted[0].Record.Pos.z>1005,"switch detonates nearest first");
                Setup();Cast(2);Release();Check(a.BombFlights.Single().Record.Kind==NativeBombs.Sticky,"sticky cast");var sticky=a.BombFlights[0];sticky.Record.Stuck=b.Seat.id;sticky.Landed=true;sticky.Record.Pos=Vec(b.Body.position);sticky.Record.BlowAt=Time+1.2f;float fuse=sticky.Record.BlowAt;b.RollEnd=Time+1;StepBombWorld();Check(sticky.Record.Stuck==0&&sticky.Detached&&sticky.Record.BlowAt==fuse,"dodge drops attached fuse");b.RollEnd=0;StepBombWorld();Check(sticky.Record.Stuck==0&&sticky.Record.BlowAt==fuse,"dropped fuse cannot reattach after roll");sticky.Record.Pos=new V3(1000,99.2f,1007);sticky.Record.Vel=new V3(0,-10,0);StepBombWorld();Check(sticky.Landed&&sticky.Record.Stuck==0&&sticky.Record.BlowAt==fuse,"dropped fuse lands without restarting timer");
                Setup();Cast(3);Release();var lob=a.BombFlights.Single();ExplodeBomb(a,lob);Check(a.BombFlights.Count==6&&a.BombFlights.All(x=>x.Record.Kind==NativeBombs.Bomblet),"six cluster bomblets");
                Setup();a.Bomb.Line=new NativeBombLine{At=Time,From=Vec(a.Body.position),To=Vec(a.Body.position+Vector3.forward*5),Speed=14};a.BombLineIndex=0;Advance(2);StepBombWorld();Check(a.Bomb.Blasts.Count(x=>x.Kind==NativeBombs.Spark)==3,"short line never blasts past clipped end");
                Setup();a.Bomb.Meter=1;Cast(5);Check(a.Bomb.Finale!=null&&a.Bomb.Meter==0,"meter starts Finale");var f=a.Bomb.Finale;var expected=NativeBombs.BoxSpot(new System.Numerics.Vector3(1000,99.135f,1000),System.Numerics.Vector3.UnitZ);Check((f.Box-new V3(expected.X,expected.Y,expected.Z)).Length<.001f,"crate under fists");var vel=Vector3.one;BombMotion(a,ref vel);Check(vel==Vector3.zero&&!TryRoll(a),"Finale rooted and cannot roll");Check(Damage(a,b,1,Vector3.forward,0,false)>0,"Finale remains hittable");Advance(2.11f);StepBombWorld();var locked=f.Circles[0].Pos;b.Body.position+=Vector3.forward*10;Advance(.1f);StepBombWorld();Check((f.Circles[0].Pos-locked).Length<.001f,"circles lock");b.Body.position=Vec(locked)+Vector3.up*.865f;a.Hp=0;a.Seat.alive=false;a.Body.isKinematic=true;Check(BombPending(),"death cannot resolve round before pending strike");Advance(.5f);StepBombWorld();Check(b.Hp==60&&a.BombStruck,"dead caster still strikes for forty");StepBombWorld();Check(b.Hp==60,"Finale strikes once");
                Setup();a.Bomb.Meter=1;Cast(5);var snap=Snapshot();var actor=snap.Actors.First(x=>x.Id==a.Seat.id);Check(actor.Bomb?.Finale!=null,"live snapshot contains Finale");var frame=NetworkFrame();var clone=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=frame})).frame;Check(clone!=null&&clone.Valid(Content)&&clone.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Finale!=null,"real frame codec Finale and validity");var replay=NativeFrameCodec.Clone(frame);a.Bomb.Finale.Circles[0].Pos.x+=30;Check(replay.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Finale.Circles[0].Pos.x!=a.Bomb.Finale.Circles[0].Pos.x,"replay owns nested circles");clone.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Meter=float.NaN;Check(!clone.Valid(Content),"wire rejects invalid kit");
                Setup();var empty=NativeFrameCodec.Clone(NetworkFrame());Check(empty.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Finale==null&&empty.snapshot.Actors.First(x=>x.Id==b.Seat.id).Bomb==null,"null kit and nested records survive JsonUtility");
                Setup();foreach(var id in NativeBombChips.Ids)Check(Content.chips.Any(c=>c.id==id&&ImplementedChip(c)),"live supported chip "+id);
                void Chip(string id,int count=1){a.Seat.chips[id]=count;RefreshLiveStats(a);}
                Setup();Chip(NativeBombChips.ShortFuse,2);Cast(shoot:true);Advance(.16f);BombRelease(a);var pop=a.BombFlights.Single().Record;Check(Math.Abs(pop.BlowAt-pop.At-.4f)<.001&&Math.Abs(pop.Vel.Length-28*1.15f*1.15f)<.01,"two Short Fuses change real flight");
                Setup();Chip(NativeBombChips.Bandolier);Chip(NativeBombChips.BlackPowder);Check(Magazine(a)==8,"Bandolier eight sticks");a.Ammo=0;Reload(a);Check(Math.Abs(a.ReloadEnd-Time-1.36f)<.001,"Bandolier real restock");BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(Math.Abs(b.Hp-86.2f)<.01,"Black Powder blast damage");
                Setup();Chip(NativeBombChips.BouncingBetty,2);StepBombWorld();Check(a.Bomb.MineCharges==4&&a.Bomb.Valid(),"Betty capacity immediately live");Cast(1);Release();Check(a.Bomb.MineCharges==3,"Betty spends charge");Advance(7);StepBombWorld();Check(a.Bomb.MineCharges==4,"Betty recharge to four");
                for(int i=0;i<6;i++){a.Bomb.CastKind=NativeBombs.BounceMine;a.Bomb.CastRelease=Time;BombRelease(a);}Check(a.BombFlights.Count(x=>x.Record.Kind==NativeBombs.Mine)==5,"Betty five live mines cap");var betty=a.BombFlights[0];betty.Record.Pos=Vec(b.Body.position);ExplodeBomb(a,betty);Check(Math.Abs(b.Body.linearVelocity.y*b.Body.linearVelocity.y/46-12.5f)<.01,"Betty raises actual apex");
                var bettyFrame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;Check(bettyFrame.Valid(Content)&&bettyFrame.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.MineCharges==4,"four mines survive real codec");
                Setup();Chip(NativeBombChips.DaisyChain);for(int i=0;i<4;i++){BombBlast(a,NativeBombs.Sticky,b.Body.position,3,16,5);Advance(1.4f);}Check(Math.Abs(b.Hp-48)<.01&&a.BombChains[b.Seat.id].Scorched(Time),"Daisy spaced chain 52 damage and escape");
                Setup();Chip(NativeBombChips.BlastShield);BombBlast(a,NativeBombs.Pop,a.Body.position,2.4f,12,5);Check(a.Hp==90&&a.Body.linearVelocity.y==5,"Blast Shield keeps self launch");
                Setup();Chip(NativeBombChips.NitroBoots,2);Cast(q:true);Check(Math.Abs(a.Bomb.Ready[0]-Time-3.92f)<.001,"Nitro stacked cooldown");Release();Check(a.BombFlights.Count==1&&a.BombFlights[0].Record.Kind==NativeBombs.Bomblet&&Math.Abs(a.Body.linearVelocity.y*a.Body.linearVelocity.y/46-12.96f)<.01,"Nitro longer jump and dropped firecracker");
                Setup();Chip(NativeBombChips.ClusterFun);Cast(3);Check(Math.Abs(a.Bomb.Ready[4]-Time-8.8f)<.001,"Cluster Fun cooldown");Release();ExplodeBomb(a,a.BombFlights.Single());Check(a.BombFlights.Count==9,"nine real bomblets");
                // GroundPoint clamps to the live map; place generated fields on this isolated outside-map fixture floor.
                Setup();Chip(NativeBombChips.ScorchedEarth);BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);BombFire(a,b.Body.position,2.4f);foreach(var field in ChipFields)field.Position=b.Body.position-Vector3.up*.865f;float beforeFire=b.Hp;var fireAt=Time;for(int i=0;i<60;i++){StepChipFields();Advance(1f/60);}Check(Math.Abs(beforeFire-b.Hp-6)<.02,"overlapping floor fire is six per second");Check(ChipFields.All(f=>Math.Abs(f.Until-fireAt-2)<.001)&&a.BombChains[b.Seat.id].Link==0,"fire lasts two seconds without advancing chain");
                a.Stats["damage"]=10000;StepChipFields();Check(b.Hp>=40,"floor fire shares pilot budget");Advance(2);StepChipFields();Check(ChipFields.Count==0,"fire expires");
                Setup();Chip(NativeBombChips.Encore);StepBombWorld();Check(Math.Abs(a.Bomb.Meter-1.25f/6000)<.000001,"Encore passive meter");a.Bomb.Meter=0;BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(Math.Abs(a.Bomb.Meter-.1f)<.0001,"Encore hit meter");Advance(.1f);BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(Math.Abs(a.Bomb.Meter-.275f)<.0001,"Encore link meter");
                Setup();Chip(NativeBombChips.Encore);a.Bomb.Meter=1;Cast(5);a.Hp=0;Advance(2.61f);StepBombWorld();Check(a.BombFlights.Count(x=>x.Encore&&x.DamageOverride==10)==8&&BombPending(),"dead Finale schedules eight ten-damage fireworks");Advance(1);StepBombWorld();Check(!a.BombFlights.Any(x=>x.Encore)&&!BombPending(),"Encore finishes before resolution");
                Setup();var previousPve=Pve;
                try
                {
                    Pve=new NativePveState();var pick=new NativePvePick{Id=999,Offers=new List<string>{NativeBombChips.ShortFuse}};var pveRow=new NativePveRow{Pilot=a,Open=pick};pveRow.Picks.Add(pick);Pve.Rows[a.Seat.id]=pveRow;
                    a.Seat.access=NativeAccess.Guest;Check(!ChoosePve(a,999,NativeBombChips.ShortFuse)&&!a.Seat.chips.ContainsKey(NativeBombChips.ShortFuse),"PvE refuses forged guest signature pick");
                    a.Seat.access=grant;Check(ChoosePve(a,999,NativeBombChips.ShortFuse)&&a.Seat.chips[NativeBombChips.ShortFuse]==1,"PvE DEV signature pick installs");
                }
                finally{Pve=previousPve;}
                Debug.Log("native Bombadier simulation: "+checks+" checks");
            }
            finally{UnityEngine.Object.DestroyImmediate(floor);}
        }
    }
}
#endif
