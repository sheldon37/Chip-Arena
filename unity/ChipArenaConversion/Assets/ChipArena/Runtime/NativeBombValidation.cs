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
                Advance(10);Match.Teams=false;Match.Cooperative=false;
                foreach(var p in Pilots){p.BombFlights.Clear();p.BombChains.Clear();p.Bomb=new NativeBombKit{MineCharges=2,Ready=new float[6]};p.BombMineReady=0;p.BombNoSteerUntil=0;p.Seat.connected=p.Seat.alive=true;p.Seat.bot=false;p.Seat.spec=p==a?NativeBombs.Spec:"pistol";p.Seat.access=p==a?grant:NativeAccess.Guest;p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Hp=(float)p.Stats["hp"];p.Mana=p.Stamina=100;p.Ammo=6;p.Ready=p.BlockEnd=p.RollEnd=p.ReloadEnd=p.AttackEnd=p.StunEnd=p.ShieldUntil=0;p.Down=null;p.Kernel=null;p.Technique=null;p.Swing=null;p.Special=null;p.AbilityCast=null;p.DevGod=false;p.Body.isKinematic=false;p.Collider.enabled=true;p.Body.position=new Vector3(1000,100,p==a?1000:1007);p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame();}
                Physics.SyncTransforms();
            }
            void Cast(byte n=0,bool q=false,bool e=false,bool shoot=false){a.Input=new InputFrame{SirenAbility=n,AbilityQ=q,AbilityE=e,Shoot=shoot};StepBombInput(a);}
            void Release(){Advance(.3f);StepBombWorld();}
            try
            {
                Setup();a.Seat.access=NativeAccess.Guest;Cast(shoot:true);Check(a.Bomb.CastKind==null&&a.Ammo==6,"guest cannot fire");a.Seat.access=grant;Cast(shoot:true);Check(a.Ammo==5&&a.Bomb.CastRelease-a.Bomb.CastAt>=.15f,"belt and windup");Check(!TryBlock(a)&&!TryRoll(a),"committed throw cannot block/roll");Advance(.1f);StepBombWorld();Check(a.BombFlights.Count==0,"no projectile before hand release");Release();Check(a.BombFlights.Count==1&&a.BombFlights[0].Record.At==a.Bomb.CastRelease,"projectile clock is release");
                Setup();BombBlast(a,NativeBombs.Pop,a.Body.position,2.4f,12,5);Check(Math.Abs(a.Hp-87)<.001&&a.Body.linearVelocity.y>=5,"quarter self damage/full launch");
                Setup();Cast(q:true);Release();Check(a.Hp==90&&a.Body.linearVelocity.y>20&&a.Bomb.Ready[0]>Time,"blast jump nine metre rise and no self damage");
                Setup();for(int n=0;n<4;n++){BombBlast(a,NativeBombs.Sticky,b.Body.position,3,16,5);Advance(.2f);}Check(a.BombChains[b.Seat.id].Scorched(Time)&&Math.Abs(b.Hp-54.4f)<.01,"four-hit chain scales 1/.8/.6/.45");b.Body.linearVelocity=Vector3.zero;BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(b.Body.linearVelocity==Vector3.zero&&Math.Abs(b.Hp-48.4f)<.01,"Scorched halves damage and prevents launch");a.Stats["damage"]=100;BombBlast(a,NativeBombs.Column,b.Body.position,4,40,12);Check(b.Hp>=40,"post-modifier chain budget survives damage modifiers");Advance(2.6f);BombBlast(a,NativeBombs.Pop,b.Body.position,2.4f,12,5);Check(b.Hp<40,"budget expires after escape window");
                Setup();Cast(1);Release();Check(a.Bomb.MineCharges==1&&a.BombFlights.Single().Record.Kind==NativeBombs.Mine,"mine charge spent");var mine=a.BombFlights.Single();mine.Landed=true;mine.Record.Pos=Vec(b.Body.position);mine.Record.ArmAt=Time+1;mine.Record.Vel=default;StepBombWorld();Check(a.BombFlights.Count==1,"unarmed mine cannot trigger");Advance(1.01f);StepBombWorld();Check(a.BombFlights.Count==0&&b.Hp==94&&b.BombNoSteerUntil>Time,"armed proximity launch and half-second no steering");Advance(7);StepBombWorld();Check(a.Bomb.MineCharges==2,"mine recharge");
                Setup();AddBomb(a,NativeBombs.Mine,new Vector3(1000,100,1003),Vector3.zero,Time,20);Check(TechniqueObstacle(b,new Vector3(1000,100,1004),Vector3.back*2,.05f,14,out _)&&a.BombFlights.Count==1,"mine takes shot damage");Check(TechniqueObstacle(b,new Vector3(1000,100,1004),Vector3.back*2,.05f,1,out _)&&a.BombFlights.Count==0,"15 HP mine detonates when shot");
                Setup();AddBomb(a,NativeBombs.Mine,a.Body.position+Vector3.forward*3,Vector3.zero,Time-1,-1);TechniqueBlastObjects(b,a.Body.position+Vector3.forward*3,2,1);Check(a.BombFlights[0].Record.BlowAt>Time,"other blast schedules mine handoff");
                Setup();AddBomb(a,NativeBombs.Mine,a.Body.position+Vector3.forward*8,Vector3.zero,Time-1,-1);AddBomb(a,NativeBombs.Mine,a.Body.position+Vector3.forward*3,Vector3.zero,Time-1,-1);Cast(e:true);Release();var sorted=a.BombFlights.OrderBy(x=>x.Record.Pos.z).ToArray();Check(sorted.Length==1&&sorted[0].Record.Pos.z>1005,"switch detonates nearest first");
                Setup();Cast(2);Release();Check(a.BombFlights.Single().Record.Kind==NativeBombs.Sticky,"sticky cast");var sticky=a.BombFlights[0];sticky.Record.Stuck=b.Seat.id;sticky.Landed=true;sticky.Record.Pos=Vec(b.Body.position);b.RollEnd=Time+1;StepBombWorld();Check(sticky.Record.Stuck==0&&!sticky.Landed,"dodge drops attached fuse");
                Setup();Cast(3);Release();var lob=a.BombFlights.Single();ExplodeBomb(a,lob);Check(a.BombFlights.Count==6&&a.BombFlights.All(x=>x.Record.Kind==NativeBombs.Bomblet),"six cluster bomblets");
                Setup();a.Bomb.Line=new NativeBombLine{At=Time,From=Vec(a.Body.position),To=Vec(a.Body.position+Vector3.forward*5),Speed=14};a.BombLineIndex=0;Advance(2);StepBombWorld();Check(a.Bomb.Blasts.Count(x=>x.Kind==NativeBombs.Spark)==3,"short line never blasts past clipped end");
                Setup();a.Bomb.Meter=1;Cast(5);Check(a.Bomb.Finale!=null&&a.Bomb.Meter==0,"meter starts Finale");var f=a.Bomb.Finale;var expected=NativeBombs.BoxSpot(new System.Numerics.Vector3(1000,99.135f,1000),System.Numerics.Vector3.UnitZ);Check((f.Box-new V3(expected.X,expected.Y,expected.Z)).Length<.001f,"crate under fists");var vel=Vector3.one;BombMotion(a,ref vel);Check(vel==Vector3.zero&&!TryRoll(a),"Finale rooted and cannot roll");Check(Damage(a,b,1,Vector3.forward,0,false)>0,"Finale remains hittable");Advance(2.11f);StepBombWorld();var locked=f.Circles[0].Pos;b.Body.position+=Vector3.forward*10;Advance(.1f);StepBombWorld();Check((f.Circles[0].Pos-locked).Length<.001f,"circles lock");b.Body.position=Vec(locked)+Vector3.up*.865f;a.Hp=0;a.Seat.alive=false;a.Body.isKinematic=true;Check(BombPending(),"death cannot resolve round before pending strike");Advance(.5f);StepBombWorld();Check(b.Hp==60&&a.BombStruck,"dead caster still strikes for forty");StepBombWorld();Check(b.Hp==60,"Finale strikes once");
                Setup();a.Bomb.Meter=1;Cast(5);var snap=Snapshot();var actor=snap.Actors.First(x=>x.Id==a.Seat.id);Check(actor.Bomb?.Finale!=null,"live snapshot contains Finale");var frame=NetworkFrame();var clone=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=frame})).frame;Check(clone!=null&&clone.Valid(Content)&&clone.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Finale!=null,"real frame codec Finale and validity");var replay=NativeFrameCodec.Clone(frame);a.Bomb.Finale.Circles[0].Pos.x+=30;Check(replay.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Finale.Circles[0].Pos.x!=a.Bomb.Finale.Circles[0].Pos.x,"replay owns nested circles");clone.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Meter=float.NaN;Check(!clone.Valid(Content),"wire rejects invalid kit");
                Setup();var empty=NativeFrameCodec.Clone(NetworkFrame());Check(empty.snapshot.Actors.First(x=>x.Id==a.Seat.id).Bomb.Finale==null&&empty.snapshot.Actors.First(x=>x.Id==b.Seat.id).Bomb==null,"null kit and nested records survive JsonUtility");
                Debug.Log("native Bombadier simulation: "+checks+" checks");
            }
            finally{UnityEngine.Object.DestroyImmediate(floor);}
        }
    }
}
#endif
