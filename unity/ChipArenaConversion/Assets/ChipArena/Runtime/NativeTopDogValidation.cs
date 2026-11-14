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
                // It walks and sprints as any pilot (the movement step's own velocity).
                Setup();a.Input=new InputFrame{Z=1,Sprint=true};for(int s=0;s<20;s++)StepPilot(a);
                Check(a.MoveVelocity.z>5&&!a.Body.isKinematic,"the Top Dog walks and sprints (velocity "+a.MoveVelocity.ToString("F2")+", kinematic "+a.Body.isKinematic+", stats speed "+a.Stats["speed"]+")");
                // The mallet's dash on a sprint: carried ahead, whoever is in its path batted on (owner 2026-11-09).
                Setup();a.Ready=0;a.Pose.Sprinting=true;a.Input=new InputFrame{Shoot=true,Attack=true,Sprint=true,Z=1};StepDogInput(a);Check(a.Dog.CastKind==NativeTopDog.Dash,"an attack on a sprint is the dash");
                start=a.Body.position;hp=b.Hp;Step(NativeTopDog.DashWind+NativeTopDog.DashTime+.05f);Check(a.Body.position.z>start.z+2&&b.Hp<hp&&a.Dog.Hits.Any(h=>h.Kind==NativeTopDog.HitPow&&h.Victim==b.Seat.id),"dashed ahead and batted the one in its path");
                Step(NativeTopDog.DashRecover);a.Ready=0;a.Pose.Sprinting=false;a.Input=new InputFrame{Shoot=true};StepDogInput(a);Check(a.Dog.CastKind==NativeTopDog.Mallet,"off the sprint it swings");
                // The smash in the air looking ahead (owner 2026-11-09): hung, then down and carried on along the facing, its head landing ahead:
                // the one standing there hurt.
                Setup();b.Body.position=o+new Vector3(0,0,2.2f);a.Body.position+=Vector3.up*3;a.Grounded=false;a.AirborneAt=Time-.3f;a.AirAttackUsed=false;Physics.SyncTransforms();
                a.Ready=0;a.Input=new InputFrame{Shoot=true,Attack=true};StepDogInput(a);Check(a.Dog.CastKind==NativeTopDog.Smash&&a.AirAttackUsed,"an attack in the air looking ahead is the smash");
                float sHigh=a.Body.position.y,sz=a.Body.position.z;Step(NativeTopDog.SmashHang*.5f);Check(Math.Abs(a.Body.position.y-sHigh)<.05f,"the smash hangs a moment");
                hp=b.Hp;b.StunEnd=0;b.StaggerImmune=0;Step(NativeTopDog.SmashHang*.5f+.4f);
                Check(NativeTopDog.Landed(a.Dog)>0&&a.Body.position.y<sHigh-2.5f&&a.Body.position.z>sz+.2f&&b.Hp<hp&&a.Dog.Hits.Any(h=>h.Kind==NativeTopDog.HitClang),"it drops forward, its head lands ahead and hurts the one there");
                // The slam in the air looking down: hung, dropped straight down, the ring round the landing hurt and its middle flattened.
                Setup();b.Body.position=o+new Vector3(0,0,1.5f);a.Body.position+=Vector3.up*3;a.Grounded=false;a.AirborneAt=Time-.3f;a.AirAttackUsed=false;Physics.SyncTransforms();
                a.Ready=0;a.Input=new InputFrame{Shoot=true,Attack=true,Pitch=-60};StepDogInput(a);Check(a.Dog.CastKind==NativeTopDog.Slam&&a.AirAttackUsed,"an attack in the air looking down is the slam");
                float high=a.Body.position.y;Step(NativeTopDog.SlamHang*.5f);Check(Math.Abs(a.Body.position.y-high)<.05f,"it hangs a moment");
                hp=b.Hp;b.StunEnd=0;b.StaggerImmune=0;Step(NativeTopDog.SlamHang*.5f+.3f);
                Check(NativeTopDog.SlamLanded(a.Dog)>0&&a.Body.position.y<high-2.5f&&b.Hp<hp&&b.StunEnd>Time&&a.Dog.Hits.Any(h=>h.Kind==NativeTopDog.HitQuake),"it lands, hurts and flattens round it");
                Check(a.Ready<=Time+NativeTopDog.SlamRecover+.01f,"and stands again soon after");
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
                // It pops in the Top Dog's own kill effect (owner 2026-11-09: "use the kill effect if applicable"): the record's kind says it burst.
                a.Seat.killEffect=NativeHotdogFriends.Style;Step(f0.Pop-Time+.03f);Check(b.Hp<=0&&b.Death!=null&&NativeTopDog.Popped(b.Death.damageKind)&&b.Death.style==NativeHotdogFriends.Style&&b.DogHolder==null,"it pops: the burst death, in the Top Dog's kill effect");
                var popped=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;a.Seat.killEffect="impact-ragdoll";
                Check(popped.Valid(Content)&&popped.pilots.First(x=>x.id==b.Seat.id).death?.damageKind==NativeTopDog.PopKind&&popped.pilots.First(x=>x.id==b.Seat.id).death.style==NativeHotdogFriends.Style,"the pop's kind and style cross the real frame codec");Check(!DogPending(),"nothing left pending");
                // The snap: forty damage to the Top Dog while it feeds breaks the links.
                Setup();a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+.2f);var f1=a.Dog.Feast;Check(f1.Catch>-50,"caught again");Step(1);a.DogTaken=NativeTopDog.SnapDamage;Step(.05f);Check(f1.Snap>-50&&b.DogHolder==null&&b.Hp>0,"forty damage snaps the links, the victim free");
                // A dead Top Dog's pop still comes once it has let go.
                Setup();a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+.2f);var f2=a.Dog.Feast;Step(f2.Release-Time+.05f);a.Hp=0;a.Seat.alive=false;Step(f2.Pop-Time+.05f);Check(b.Hp<=0&&NativeTopDog.Popped(b.Death?.damageKind),"a dead Top Dog's victim still pops");
                // A miss gives half the meter back.
                Setup();b.Body.position=o+new Vector3(0,0,60);Physics.SyncTransforms();a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+NativeTopDog.LassoReach/NativeTopDog.LassoSpeed+.2f);Check(a.Dog.Feast==null||a.Dog.Feast.Miss,"the lasso misses");Check(Math.Abs(a.Dog.Meter-NativeTopDog.MissRefund)<.02f,"half the meter back");
                // ---- the chips (Core NativeDogChips) ----
                void Give(string id,int n=1)=>a.Seat.chips[id]=n;
                foreach(var id in NativeDogChips.Ids)Check(Content.chips.Any(c=>c.id==id&&ImplementedChip(c)),"live supported chip "+id);
                // Bigger Mallet: it reaches what a plain mallet cannot, and the record says how big.
                Setup();b.Body.position=o+new Vector3(0,0,3.7f);Physics.SyncTransforms();hp=b.Hp;Cast(shoot:true);Step(NativeTopDog.SwingLength[0]);Check(b.Hp==hp,"a plain mallet falls short at 3.7 m");
                Setup();Give(NativeDogChips.BiggerMallet,2);b.Body.position=o+new Vector3(0,0,3.7f);Physics.SyncTransforms();hp=b.Hp;Cast(shoot:true);Step(NativeTopDog.SwingLength[0]);Check(b.Hp<hp&&a.Dog.Mallet==2,"Bigger Mallet reaches it, the record two sizes up");
                // Hammerspace: the fourth swing that lands drops a piano on them.
                Setup();Give(NativeDogChips.Hammerspace);for(int i=0;i<4;i++){b.StunEnd=0;b.StaggerImmune=0;b.Hp=(float)b.Stats["hp"];a.Dog.ComboUntil=Time+1;Cast(shoot:true);Step(NativeTopDog.SwingLength[a.Dog.CastStep]+.02f);}
                var piano=a.DogFlights.FirstOrDefault(f=>f.Record.Kind==NativeTopDog.PropPiano);Check(piano!=null&&piano.Record.Hit==b.Seat.id,"the fourth hit pulls a piano out of nowhere");
                piano.Record.Pos=Vec(b.Body.position-Vector3.up*.865f);piano.Record.LandAt=Time+.05f;b.Hp=(float)b.Stats["hp"];b.StunEnd=0;hp=b.Hp;Step(.1f);Check(b.Hp<=hp-NativeDogChips.PianoDamage*.8f&&b.StunEnd>Time,"the piano lands on them and flattens");
                // Anvil Shower: three anvils, two smaller to the sides.
                Setup();Give(NativeDogChips.AnvilShower);Cast(1);Step(NativeTopDog.AnvilRelease+.02f);Check(a.Dog.Props.Count(p=>p.Kind==NativeTopDog.PropAnvil)==3&&a.Dog.Props.Count(p=>p.Kind==NativeTopDog.PropAnvil&&p.Size>0)==2,"Anvil Shower drops three");
                // Banana Bonanza: more peels in hand, and a peel that flies on with its victim.
                Setup();Give(NativeDogChips.BananaBonanza,2);Step(.02f);Check(a.Dog.Peels==4&&a.Dog.PeelCap==4,"two more peels in hand");
                Cast(2);Step(NativeTopDog.PeelRelease+.02f);var peel2=a.DogFlights.First(f=>f.Record.Kind==NativeTopDog.PropPeel);peel2.Record.To=Vec(b.Body.position-Vector3.up*.865f);peel2.Record.LandAt=Time;b.Grounded=true;Step(.05f);
                Check(b.Down!=null&&a.DogFlights.Contains(peel2)&&!peel2.Landed&&peel2.Target==b.Seat.id,"the peel flies on with them to land again");
                // Hot Sauce: it burns.
                Setup();Give(NativeDogChips.HotSauce);Cast(3);hp=b.Hp;Step(1);Check(b.Hp<hp&&b.Dots.Keys.Any(x=>x.EndsWith(":fire"))&&a.Dog.Marks.Any(m=>m.Id==b.Seat.id&&m.Kind==NativeTopDog.Sauced)&&a.Dog.Sauce,"Hot Sauce sets them burning");
                // Pie Volley: three pies.
                Setup();Give(NativeDogChips.PieVolley);Cast(5);Step(NativeTopDog.PieRelease+.02f);Check(a.Dog.Props.Count(p=>p.Kind==NativeTopDog.PropPie)==3,"Pie Volley throws three");
                // Knockout Glove: the whole line, and a splat against a wall.
                Setup();Give(NativeDogChips.Knockout);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.layer=PropLayer;wall.transform.position=o+new Vector3(0,1,6);wall.transform.localScale=new Vector3(8,4,.5f);Physics.SyncTransforms();
                try
                {
                    Cast(4);Step(NativeTopDog.GloveRelease+.02f);var line=a.DogFlights.FirstOrDefault(f=>f.Line!=null);Check(line!=null&&line.Line.Any(x=>x.id==b.Seat.id)&&line.Record.Size<6,"the glove's line, stopped at the wall");
                    hp=b.Hp;Step(NativeTopDog.GloveOut*2+.05f);Check(b.Hp<hp,"punched");b.Body.position=o+new Vector3(0,0,5.3f);Physics.SyncTransforms();b.StunEnd=0;Step(.05f);
                    Check(a.Dog.Marks.Any(m=>m.Id==b.Seat.id&&m.Kind==NativeTopDog.Walled)&&b.StunEnd>Time,"punched into the wall, splatted on it");
                }
                finally{UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();}
                // Double Boing: a second bounce in hand, spent by the second; the landing flattens.
                Setup();Give(NativeDogChips.DoubleBoing);Cast(q:true);Check(a.DogBoingSecond&&a.Dog.Ready[0]-Time<.5f,"a second bounce in hand");Step(.4f);Cast(q:true);Check(!a.DogBoingSecond&&a.Dog.Ready[0]-Time>5,"the second spends it");
                Setup();Give(NativeDogChips.DoubleBoing);a.DogAirborne=true;a.DogAirAt=Time-1;a.Grounded=true;hp=b.Hp;Step(.02f);Check(b.Hp<hp&&a.Dog.Marks.Any(m=>m.Id==b.Seat.id&&m.Kind==NativeTopDog.Pancake),"the landing flattens");
                // Second Helpings: the meter, and the pop's hot dogs that heal.
                Setup();Give(NativeDogChips.SecondHelpings);a.Dog.Meter=0;Step(1);Check(Math.Abs(a.Dog.Meter-NativeTopDog.MeterPerSecond*NativeDogChips.HelpingMeter)<.003f,"the meter fills faster");
                a.Dog.Meter=1;Cast(6);Step(NativeTopDog.Twirl+.2f);var f3=a.Dog.Feast;Check(f3!=null&&f3.Catch>-50,"caught");Step(f3.Pop-Time+.03f);Check(a.Dog.Props.Count(p=>p.Kind==NativeTopDog.PropSnack)==NativeDogChips.Snacks,"the pop throws out hot dogs");
                var snack=a.DogFlights.First(f=>f.Record.Kind==NativeTopDog.PropSnack);snack.Record.LandAt=Time;snack.Record.To=Vec(a.Body.position-Vector3.up*.865f);a.Hp=50;Step(.05f);Check(a.Hp>50&&!a.DogFlights.Contains(snack),"walking over one heals");
                // Toon Logic: flattened instead of dying, back up, once.
                Setup();Give(NativeDogChips.ToonLogic);Damage(a,b,a.Hp+50,Vector3.forward,0,false);Check(a.Hp==1&&a.DogToonUsed&&a.DogToonUntil>Time&&a.DogMarks.Any(m=>m.Id==a.Seat.id&&m.Kind==NativeTopDog.Pancake),"Toon Logic flattens it instead");
                Step(NativeDogChips.ToonFlat+.05f);Check(a.Hp>=(float)a.Stats["hp"]*NativeDogChips.ToonHealth-.01f&&a.DogToonUntil==0&&b.Hp<(float)b.Stats["hp"],"it springs back up and knocks them away");
                Step(.4f);Damage(a,b,a.Hp+50,Vector3.forward,0,false);Check(a.Hp<=0,"once a round");
                {var cf=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;Check(cf.Valid(Content),"the chips' record crosses the codec");}
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
