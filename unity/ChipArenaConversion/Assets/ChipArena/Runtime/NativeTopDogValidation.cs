#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The Top Dog's rules against the real arena (docs/TOP-DOG.md): the DEV gate, every cast's clock and effect, the mallet's combo, the
    // feast's catch, feeding, let-go and pop into the burst death, the snap, a dead caster's pop, and the record over the real frame codec.
    // Run by the Editor build checks beside the Bombadier's.
    public sealed partial class NativeArena
    {
        public void ValidateTopDog()
        {
            var a=Pilots[0];var b=Pilots[1];int checks=0;
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            // A floor floating over the middle of the map (the travel and the ground points keep to the map's x and z, not its height).
            var bounds=Map?.bounds;var o=new Vector3(bounds==null?0:(bounds.minX+bounds.maxX)*.5f,100,bounds==null?0:(bounds.minZ+bounds.maxZ)*.5f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.layer=PropLayer;floor.transform.position=new Vector3(o.x,98.635f,o.z);floor.transform.localScale=new Vector3(80,1,80);
            void Check(bool ok,string label){if(!ok)throw new Exception("Native Top Dog: "+label);checks++;}
            void Advance(float t){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Match.Time+t,fightAt=0});}
            void Step(float seconds){int n=Mathf.Max(1,Mathf.RoundToInt(seconds*60));for(int i=0;i<n;i++){Advance(1f/60);StepDogWorld();}}
            void Setup()
            {
                Advance(10);ChipFields.Clear();Match.Teams=false;Match.Cooperative=false;
                foreach(var p in Pilots){p.Seat.chips.Clear();DogClear(p);p.DogHolder=null;p.DogPopped=false;p.Death=null;p.Seat.connected=p.Seat.alive=true;p.Seat.bot=false;p.Seat.spec=p==a?NativeTopDog.Spec:"pistol";p.Seat.access=p==a?grant:NativeAccess.Guest;p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Hp=(float)p.Stats["hp"];p.Mana=p.Stamina=100;p.Ammo=6;p.Ready=p.BlockEnd=p.RollEnd=p.ReloadEnd=p.AttackEnd=p.StunEnd=p.ShieldUntil=p.SlowEnd=0;p.SlowFactor=1;p.Down=null;p.Kernel=null;p.Technique=null;p.Swing=null;p.Special=null;p.AbilityCast=null;p.DevGod=false;p.Body.isKinematic=false;p.Body.useGravity=true;p.Collider.enabled=true;p.Body.position=o+new Vector3(0,0,p==a?0:2.5f);p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame();p.Grounded=true;}
                Physics.SyncTransforms();
            }
            void Cast(byte n=0,bool q=false,bool e=false,bool shoot=false){a.Ready=0;a.Input=new InputFrame{SirenAbility=n,AbilityQ=q,AbilityE=e,Shoot=shoot};StepDogInput(a);}
            try
            {
                var row=Content.Weapon(NativeTopDog.Spec);double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Check(row!=null&&row.devOnly&&row.@class=="melee","DEV melee row");
                Check(NativeLoadoutText.InClass(row,"dev",true,grant.Allows("weapon:"+row.id,now)),"a DEV account can pick the Top Dog");
                Check(!NativeLoadoutText.InClass(row,"dev",false,NativeAccess.Guest.Allows("weapon:"+row.id,now)),"a guest cannot");
                // The mallet: a guest cannot swing it; three swings, the third a slam that flattens.
                Setup();a.Seat.access=NativeAccess.Guest;Cast(shoot:true);Check(a.Dog.CastKind==null,"a guest cannot swing");a.Seat.access=grant;
                Cast(shoot:true);Check(a.Dog.CastKind==NativeTopDog.Mallet&&a.Dog.CastStep==0&&a.Dog.CastRelease>a.Dog.CastAt,"the first swing winds up");Check(!TryBlock(a)&&!TryRoll(a),"a committed swing cannot block or roll");
                float hp=b.Hp;Step(NativeTopDog.SwingLength[0]);Check(b.Hp<hp&&b.Hp>hp-30,"the first swing bonks");
                Cast(shoot:true);Check(a.Dog.CastStep==1,"the second swing follows on");Step(NativeTopDog.SwingLength[1]);
                b.StunEnd=0;b.StaggerImmune=0;Cast(shoot:true);Check(a.Dog.CastStep==2,"the third is the slam");hp=b.Hp;Step(NativeTopDog.SwingLength[2]);Check(b.Hp<hp&&b.StunEnd>Time&&a.Dog.Marks.Any(m=>m.Id==b.Seat.id&&m.Kind==NativeTopDog.Pancake),"the slam flattens");
                Advance(2);Cast(shoot:true);Check(a.Dog.CastStep==0,"the combo lapses");
                // Boing!
                Setup();Cast(q:true);Check(a.Dog.Ready[0]>Time,"the spring's cooldown");Step(.15f);Check(a.Body.linearVelocity.y>18&&a.DogAirborne&&a.Dog.Props.Any(p=>p.Kind==NativeTopDog.PropSpring),"the spring throws it high");
                // Zip: rooted through the wind-up, then carried ahead, swatting on the way.
                Setup();Cast(e:true);var vel=Vector3.one;DogMotion(a,ref vel);Check(vel.x==0&&vel.z==0,"rooted through the wind-up");var start=a.Body.position;hp=b.Hp;Step(NativeTopDog.ZipWind+NativeTopDog.ZipTime+.05f);Check(a.Body.position.z>start.z+2&&b.Hp<hp,"zipped ahead and swatted");
                // The anvil: dropped on a spot, it flattens what is under it when it lands.
                Setup();Cast(1);Check(a.Dog.Ready[2]>Time,"the anvil's cooldown");Step(NativeTopDog.AnvilRelease+.02f);Check(a.Dog.Props.Any(p=>p.Kind==NativeTopDog.PropAnvil),"an anvil in the air");
                var anvil=a.DogFlights.First(f=>f.Record.Kind==NativeTopDog.PropAnvil);anvil.Record.Pos=Vec(b.Body.position-Vector3.up*.865f);anvil.Record.LandAt=Time+.1f;hp=b.Hp;Step(.15f);Check(b.Hp<hp-20&&b.StunEnd>Time,"the anvil lands and flattens");
                // A peel: two charges; stepped on, a slip onto the back.
                Setup();Cast(2);Check(a.Dog.Peels==1,"a peel spent");Step(NativeTopDog.PeelRelease+.02f);var peel=a.DogFlights.First(f=>f.Record.Kind==NativeTopDog.PropPeel);peel.Record.To=Vec(b.Body.position-Vector3.up*.865f);peel.Record.LandAt=Time;hp=b.Hp;b.Grounded=true;Step(.05f);
                Check(b.Down!=null&&b.Hp<hp&&!a.DogFlights.Contains(peel),"slipped on it, the peel gone");Step(NativeTopDog.Cooldown[3]+.1f);Check(a.Dog.Peels==2,"the peel comes back");
                // The mustard: a stream that hurts and sticks.
                Setup();Cast(3);hp=b.Hp;Step(1);Check(b.Hp<hp&&b.SlowEnd>Time&&a.Dog.Props.Any(p=>p.Kind==NativeTopDog.PropSquirt),"the mustard hurts and slows");var mv=Vector3.one;DogMotion(a,ref mv);Check(Math.Abs(mv.x-.5f)<.01f,"half speed while squirting");
                // The glove: a punch that throws and dazes.
                Setup();Cast(4);hp=b.Hp;Step(NativeTopDog.GloveRelease+NativeTopDog.GloveOut+.05f);Check(b.Hp<hp&&new Vector2(b.Knockback.x,b.Knockback.z).magnitude>8&&b.StunEnd>Time,"the glove throws and dazes");
                // The pie: a splat in the face.
                Setup();Cast(5);hp=b.Hp;Step(NativeTopDog.PieRelease+.3f);Check(b.Hp<hp&&a.Dog.Marks.Any(m=>m.Id==b.Seat.id&&m.Kind==NativeTopDog.Pied),"the pie in the face");
                // ---- All You Can Eat ----
                Setup();Cast(6);Check(a.Dog.Feast==null,"no feast without the meter");a.Dog.Meter=1;Cast(6);var f0=a.Dog.Feast;Check(f0!=null&&a.Dog.Meter==0,"the meter starts the feast");
                var still=Vector3.one;DogMotion(a,ref still);Check(still.x==0&&still.z==0&&!TryRoll(a),"rooted through the twirl");Check(Damage(a,b,1,Vector3.forward,0,false)>0,"hittable while it twirls");
                Step(NativeTopDog.Twirl+.2f);Check(f0.Catch>-50&&f0.Victim==b.Seat.id&&b.DogHolder==a&&Incapacitated(b)&&PinDogVictim(b),"the lasso catches and holds");
                var frame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;var wire=frame.snapshot.Actors.First(x=>x.Id==a.Seat.id).Dog;
                Check(frame.Valid(Content)&&wire?.Feast!=null&&wire.Feast.Victim==b.Seat.id&&frame.snapshot.Actors.First(x=>x.Id==b.Seat.id).Dog==null,"the feast crosses the real frame codec");wire.Meter=float.NaN;Check(!frame.Valid(Content),"the wire refuses a broken kit");
                Step(f0.Release-Time-.02f);Check(f0.Fed==NativeTopDog.Feeds&&b.Hp>0&&f0.Snap<-50,"six hot dogs down, still alive");Check(DogPending(),"the round waits for the pop");
                Step(.1f);Check(a.Dog.Marks.Any(m=>m.Id==b.Seat.id&&m.Kind==NativeTopDog.Stuffed)&&b.DogHolder==a,"let go stuffed, still held to the pop");
                Step(f0.Pop-Time+.03f);Check(b.Hp<=0&&b.Death!=null&&b.Death.style==NativeTopDog.Burst&&b.DogHolder==null,"it pops: the burst death");Check(!DogPending(),"nothing left pending");
                // The snap: forty damage to the Top Dog while it feeds breaks the links.
                Setup();a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+.2f);var f1=a.Dog.Feast;Check(f1.Catch>-50,"caught again");Step(1);a.DogTaken=NativeTopDog.SnapDamage;Step(.05f);Check(f1.Snap>-50&&b.DogHolder==null&&b.Hp>0,"forty damage snaps the links, the victim free");
                // A dead Top Dog's pop still comes once it has let go.
                Setup();a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+.2f);var f2=a.Dog.Feast;Step(f2.Release-Time+.05f);a.Hp=0;a.Seat.alive=false;Step(f2.Pop-Time+.05f);Check(b.Hp<=0&&b.Death?.style==NativeTopDog.Burst,"a dead Top Dog's victim still pops");
                // A miss gives half the meter back.
                Setup();b.Body.position=o+new Vector3(0,0,60);Physics.SyncTransforms();a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+NativeTopDog.LassoReach/NativeTopDog.LassoSpeed+.2f);Check(a.Dog.Feast==null||a.Dog.Feast.Miss,"the lasso misses");Check(Math.Abs(a.Dog.Meter-NativeTopDog.MissRefund)<.02f,"half the meter back");
                // A bot spends a full meter.
                Setup();a.Dog.Meter=1;var bot=new InputFrame();BotDog(a,ref bot,b.Body.position,2.5f,true);Check(bot.SirenAbility==6,"a bot uses the ultimate");
                var empty=NativeFrameCodec.Clone(NetworkFrame());Check(empty.snapshot.Actors.First(x=>x.Id==b.Seat.id).Dog==null&&empty.snapshot.Actors.First(x=>x.Id==a.Seat.id).Dog?.Feast==null,"a null kit and an idle feast survive the clone");
                Debug.Log("native Top Dog simulation: "+checks+" checks");
            }
            finally{UnityEngine.Object.DestroyImmediate(floor);foreach(var p in Pilots){DogClear(p);p.DogHolder=null;}}
        }
    }
}
#endif
