#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Executed only by the activated Unity build gate, in its isolated preview physics scene.
        // Calls the actual contact/defense handlers; no fake Unity types or simulated test contacts.
        public void ValidateMelee()
        {
            var a=Pilots[0];var b=Pilots[1];var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Melee occlusion check";wall.transform.SetParent(root.transform);wall.layer=PropLayer;
            wall.transform.position=new Vector3(1000,100,1001);wall.transform.localScale=new Vector3(3,3,.2f);wall.SetActive(false);
            void Check(bool ok,string message){if(!ok)throw new Exception("Native melee gate: "+message);}
            void Advance(float seconds){while(seconds>.000001f){float dt=Math.Min(.05f,seconds);Match.Tick(dt);seconds-=dt;}}
            void Position(NativePilot p,Vector3 pos){p.Body.position=pos;p.Body.linearVelocity=Vector3.zero;p.Knockback=p.MoveVelocity=Vector3.zero;Physics.SyncTransforms();}
            void Setup(string spec="sword",string defender="daggers")
            {
                Match.Teams=false;wall.SetActive(false);
                foreach(var p in new[]{a,b})
                {
                    p.Seat.spec=p==a?spec:defender;p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);
                    p.Wounds.Clear();p.Death=null;Events.Clear();
                    p.Seat.bot=false;p.Seat.alive=p.Seat.connected=true;p.Hp=p.Stamina=p.Mana=100;p.Collider.enabled=true;p.Body.isKinematic=false;
                    p.Ready=p.AttackEnd=p.BlockEnd=p.BlockReady=p.RollEnd=p.RollReady=p.StunEnd=p.StaggerImmune=p.ReloadEnd=0;p.CastAt=0;p.RegenAt=Time+10;
                    p.MeleeKit=new NativeMeleeKit();p.MeleeBufferedUntil=-1;p.MeleeComboUntil=0;p.Pose.Sprinting=false;p.ArmourHitUntil=0;p.Swing=null;p.Special=null;p.Down=null;p.Juggle=null;p.FrozenUntil=p.ScareUntil=0;p.Roll=null;p.Combo=-1;p.RiposteAt=p.BlockAt=-100;p.RiposteBy=-1;p.Dots.Clear();CancelAbilities(p);
                    p.AirborneAt=-1;p.AirAttackUsed=false;p.Grounded=true;p.Input=new InputFrame{Yaw=p==a?0:180};p.Yaw=p.Input.Yaw;
                    Position(p,new Vector3(1000,100,p==a?1000:1002));
                }
            }
            try
            {
                foreach(string spec in new[]{"sword","daggers","greatsword","shield"})
                {
                    Setup(spec);Check(TryMelee(a),"primary refused "+spec);NearNative(a.Stamina,100-(float)a.Weapon.cost,"stamina cost");
                    Check(!TryBlock(a),"block cancelled a committed primary");StepMelee(a);Check(b.Hp==100,"windup hit early");
                    Position(b,new Vector3(1000,100,998));Advance(a.Swing.HitAt-Time+.001f);StepMelee(a);Check(b.Hp==100,"rear target hit");
                    Position(b,new Vector3(1000,100,1002));Advance((a.Swing.HitAt+a.Swing.HitUntil)/2-Time);StepMelee(a);
                    NearNative(b.Hp,100-(float)a.Weapon.damage,"late entry into sweep "+spec);var hitEvent=Events.Last(e=>e.Kind==EventKind.Hit&&e.Target==b.Seat.id);
                    Check(hitEvent.Projectile==a.Swing.Id&&hitEvent.Projectile>0&&Vec(hitEvent.From)==a.Body.position,"authoritative melee origin and swing id "+spec);
                    float hp=b.Hp;Advance(.001f);StepMelee(a);Check(b.Hp==hp,"duplicate primary contact");
                }
                // Metadata must identify the arrow on both ordinary and head hits, including terminal hits.
                foreach(bool head in new[]{false,true})foreach(int pierces in new[]{0,1})
                {
                    Setup("bow");b.Hp=1;
                    var point=b.Body.position+Vector3.up*(head?.6f:0);
                    var arrow=new NativeBullet{Id=nextBullet++,Owner=a.Seat.id,Original=a.Seat.id,Spec="bow",Direct=true,Damage=10,Radius=.045f,Velocity=Vector3.forward*80,Pierces=pierces,Attack=nextAttack++};
                    bool stopped=BulletImpact(arrow,a,new NativeContact{Collider=b.Collider,Point=point,Contact=point,Normal=Vector3.back});
                    var evt=Events.Last(e=>e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot);
                    Check(evt.Projectile==arrow.Id&&evt.Pierced==(pierces>0)&&stopped==(pierces==0),"arrow id and pass-through agree with actual continuation");
                    Check(evt.Kind==(head?EventKind.Headshot:EventKind.Hit),"head and body metadata paths");
                    Check(b.Death?.wound?.trauma=="pierce"&&b.Death.wound.kind!="scorch"&&b.Death.projectile==arrow.Id,"fatal arrow wound and replay identity");
                    var copy=NativeFrameCodec.Clone(NetworkFrame());var copied=copy.snapshot.Events.Last(e=>e.Id==evt.Id);
                    Check(copy.Valid(Content)&&copied.Projectile==evt.Projectile&&copied.Pierced==evt.Pierced,"arrow metadata survives real frame JSON");
                }
                // Cancel the actual committed contact and queued echoes, including a bot's greatsword.
                foreach(string control in new[]{"juggle","down","stun","freeze"})foreach(bool active in new[]{false,true})
                {
                    Setup("greatsword");a.Seat.bot=true;Check(TryMelee(a),"CC swing setup");
                    var interrupted=a.Swing;float hit=(interrupted.HitAt+interrupted.HitUntil)/2,readyAt=a.Ready;
                    interrupted.Remaining=2;a.MeleeBufferedUntil=Time+2;
                    if(active)Advance(hit-Time);
                    if(control=="juggle")a.Juggle=new NativeJuggle{At=Time,Until=Time+1,Owner=b};
                    if(control=="down")a.Down=new NativeRecovery{At=Time,Until=Time+1};
                    if(control=="stun")a.StunEnd=Time+1;
                    if(control=="freeze")a.FrozenUntil=Time+1;
                    StepMelee(a);Check(a.Swing==null&&!a.HitPending&&a.MeleeBufferedUntil<0&&a.AttackEnd==0,"CC cancels swing/echo/buffer "+control);
                    Check(a.Ready==readyAt,"CC refunded cooldown");
                    a.Juggle=null;a.Down=null;a.StunEnd=a.FrozenUntil=0;Advance(Math.Max(0,hit-Time));StepMelee(a);
                    Check(b.Hp==100&&a.Swing==null,"interrupted swing resumed after CC "+control);
                }
                Setup("greatsword");Check(TryMelee(a),"push swing setup");a.Knockback=Vector3.up*4;
                Advance((a.Swing.HitAt+a.Swing.HitUntil)/2-Time);StepMelee(a);Check(b.Hp<100,"ordinary push incorrectly cancelled swing");
                // Landing attacks use the real hit, block and roll handlers; airtime never resolves damage.
                foreach(string spec in new[]{"daggers","sword","shield","greatsword"})
                {
                    foreach(string defense in new[]{"none","block","roll","wall"})
                    {
                        Setup(spec);a.Grounded=false;a.AirborneAt=Time-.2f;a.Input.Attack=true;
                        Check(TryMelee(a)&&a.Swing.Air&&a.AirAttackUsed,"air commitment "+spec);
                        NearNative(a.Stamina,100-NativeMeleeFeel.Cost(a.Weapon,a.Stats)*1.25f,"air stamina");
                        Advance(.2f);StepMelee(a);Check(b.Hp==100,"airborne damage");
                        if(defense=="block")Check(TryBlock(b),"landing defender block");
                        if(defense=="roll"){b.Roll=NativeContentRules.Roll(Content,b.Seat.spec,b.Stats);b.RollAt=Time-.1f;b.RollEnd=Time+.3f;}
                        if(defense=="wall"){wall.SetActive(true);Physics.SyncTransforms();}
                        a.Grounded=true;StepMelee(a);
                        if(defense=="none"){NearNative(b.Hp,100-(float)a.Weapon.damage*(spec=="greatsword"?1.5f:1.3f),"landing damage");Check(b.StunEnd>Time,"landing stagger");Check(Events.Exists(e=>e.Kind==EventKind.Hit&&e.Source==a.Seat.id&&e.Target==b.Seat.id&&e.Time==Time&&e.Normal.Length>.9f),"pilot hit carries its actual direction");float hp=b.Hp;StepMelee(a);Check(b.Hp==hp,"duplicate landing hit");NearNative(a.Ready-Time,NativeAirAttackSimulation.Recovery(spec),"landing recovery");}
                        else Check(b.Hp==100,"landing defense "+defense);
                    }
                }
                Debug.Log("PASS native landing attacks: four weapons, no air damage, cost, landing damage/stagger/recovery, dedup, block/roll/cover");
                // Sprint contacts use the real damage/defense route; do not replace these with profile-only checks.
                foreach(string spec in new[]{"sword","daggers","shield","greatsword"})
                {
                    Setup(spec);a.Input.Attack=a.Input.Sprint=true;a.Input.Z=1;
                    float cost=NativeMeleeFeel.Cost(a.Weapon,a.Stats)*1.3f;
                    a.Stamina=cost-.01f;Check(!TryMelee(a)&&a.Swing==null,"unfunded sprint "+spec);a.Stamina=100;
                    Check(TryMelee(a)&&a.Swing.SprintAttack,"sprint start "+spec);NearNative(a.Stamina,100-cost,"sprint commitment cost");
                    Check(!TryBlock(a),"sprint block cancel "+spec);
                    if(spec=="greatsword")Check(!a.Grounded&&a.Body.linearVelocity.y>0,"sprint hop launch");
                    float hit=a.Swing.HitAt+(a.Swing.HitUntil-a.Swing.HitAt)*(spec=="daggers"?.25f:.5f);
                    Advance(hit-Time);StepMelee(a);float scale=spec=="sword"?1.2f:spec=="daggers"?.7f:spec=="shield"?.9f:1.4f;
                    NearNative(b.Hp,100-(float)a.Weapon.damage*scale,"sprint damage "+spec);
                    if(spec=="shield"){NearNative(b.StunEnd-Time,.35f,"charge stagger");Check(b.Down==null,"charge must not launch");}
                    float hp=b.Hp;StepMelee(a);NearNative(b.Hp,hp,"sprint duplicate contact");
                    if(spec=="daggers"){Advance(.12f);StepMelee(a);NearNative(b.Hp,100-(float)a.Weapon.damage*1.4f,"twin second hit");}
                }
                Setup("greatsword");a.Input.Attack=a.Input.Sprint=true;a.Input.Z=1;TryMelee(a);Position(b,new Vector3(1000,100,998.5f));
                Advance(a.Swing.HitAt-Time+.02f);StepMelee(a);Check(b.Hp==100,"airborne ring");a.Grounded=true;StepMelee(a);Check(b.Hp<100,"landed rear ring");
                Setup("greatsword");a.Input.Attack=a.Input.Sprint=true;a.Input.Z=1;TryMelee(a);Position(b,new Vector3(1000,100,998.5f));
                Advance(a.Swing.HitAt-Time+.02f);a.Grounded=true;b.Roll=NativeContentRules.Roll(Content,b.Seat.spec,b.Stats);b.RollAt=Time-.1f;b.RollEnd=Time+.3f;
                StepMelee(a);Check(b.Hp==100,"ring roll defense");
                Setup("greatsword");a.Input.Attack=a.Input.Sprint=true;a.Input.Z=1;TryMelee(a);Position(b,new Vector3(1000,100,998.5f));
                Advance(a.Swing.HitAt-Time+.02f);a.Grounded=true;Check(TryBlock(b),"ring defender block");StepMelee(a);Check(b.Hp==100&&a.Swing==null,"ring block interrupts");
                Setup();Check(TryMelee(a),"wall attack setup");Advance((a.Swing.HitAt+a.Swing.HitUntil)/2-Time);wall.SetActive(true);Physics.SyncTransforms();StepMelee(a);Check(b.Hp==100,"melee passed through cover");
                wall.SetActive(false);Physics.SyncTransforms();StepMelee(a);Check(b.Hp<100,"blocked ray spent contact ledger");
                Setup();Match.Teams=true;a.Seat.team=b.Seat.team=0;TryMelee(a);Advance((a.Swing.HitAt+a.Swing.HitUntil)/2-Time);StepMelee(a);Check(b.Hp==100,"friendly fire bypass");
                Setup();TryMelee(a);Advance((a.Swing.HitAt+a.Swing.HitUntil)/2-Time);b.Roll=NativeContentRules.Roll(Content,b.Seat.spec,b.Stats);b.RollAt=Time-.1f;b.RollEnd=Time+.3f;StepMelee(a);Check(b.Hp==100,"roll i-frame bypass");

                Setup();TryMelee(a);Advance(a.Swing.HitAt-Time);Check(TryBlock(b),"defender block refused");Advance(.08f);StepMelee(a);
                Check(b.Hp==100&&a.Swing==null&&a.StunEnd>Time+1&&RiposteOpen(b),"successful block did not interrupt/open riposte");
                Check(a.Knockback.z<0,"block did not rebound attacker");float blockEnd=b.BlockEnd,stunEnd=a.StunEnd;
                b.Stamina=0;Check(!TryMelee(b)&&b.BlockEnd==blockEnd,"failed counter dropped protection");
                SettleRiposte(a,new NativePilot{Seat=new NativeSeat{id=999}},1);Check(a.StunEnd==stunEnd,"another attacker ended riposte");
                SettleRiposte(a,b,0);Check(a.StunEnd==stunEnd,"absorbed counter ended riposte");
                b.Stamina=100;Check(TryMelee(b)&&b.BlockEnd==0,"counter did not release guard");Advance((b.Swing.HitAt+b.Swing.HitUntil)/2-Time);StepMelee(b);
                Check(a.Hp<100&&a.RiposteBy<0,"counter did not settle riposte");NearNative(a.StunEnd-Time,NativeMeleeRules.HitStun(b.Weapon,1),"dagger counter pair stun");
                Setup();b.Seat.bot=true;SuccessfulMeleeBlock(b,a,30,Vector3.forward,610);Check(b.RiposteAt<0&&a.RiposteBy<0,"bot gained human riposte");NearNative(a.StunEnd-Time,NativeMeleeRules.BlockStun(30,610),"bot block stun");

                Setup("greatsword");TryMelee(a);Advance(.04f);float stamina=a.Stamina;var profile=NativeContentRules.Roll(Content,a.Seat.spec,a.Stats);
                Check(TryRoll(a)&&a.Swing==null&&a.AttackEnd==0,"windup roll cancel failed");NearNative(a.Ready,Time+(float)profile.duration,"early roll cooldown refund");NearNative(a.Stamina,stamina-(float)profile.cost,"roll refunded attack stamina");
                Setup("greatsword");TryMelee(a);Advance(a.Swing.HitAt-Time+.02f);float ready=a.Ready;Check(TryRoll(a)&&a.Ready==ready,"late cancel refunded attack cooldown");
                Setup();TryMelee(a);a.Stamina=0;Check(!TryRoll(a)&&a.Swing!=null,"unfunded roll cancelled attack");
                Setup();TryBlock(a);Check(!TryRoll(a),"ordinary Block cancelled by roll");
                Setup();a.CastAt=Time+1;Check(!TryRoll(a),"roll cancelled a committed cast");
                Setup();a.Seat.bot=true;TryMelee(a);Check(!TryRoll(a),"bot used human roll cancel");
                Setup();Position(a,new Vector3(1000,10,1000));a.Grounded=false;a.Knockback=new Vector3(6,4,0);StepPilot(a);
                NearNative(a.Body.linearVelocity.x,6*Mathf.Exp(-3.1f/60),"external velocity integration");NearNative(a.Body.linearVelocity.y,4,"one-shot vertical kick");Check(a.Knockback.y==0,"lift retained for next tick");
                Setup();Position(b,new Vector3(1000,100,1005));a.Input.LockTarget=b.Seat.id;a.Input.Z=1;
                Check(TryMelee(a)&&a.Swing.OpenerLunge&&!a.Swing.SprintAttack,"normal opener flag");NearNative(a.Swing.Distance,2.2f,"host lock lunge cap");
                Position(b,new Vector3(1005,100,1005));Advance(Rules.Dt);StepMelee(a);
                NearNative(a.Yaw,12,"windup turn rate");NearNative(a.Swing.Yaw*Mathf.Rad2Deg,a.Yaw,"commit yaw follows turn");
                float heading=a.Yaw;Advance(a.Swing.HitAt-Time);a.Input.LockTarget=0;a.Input.Yaw=180;StepMelee(a);NearNative(a.Yaw,heading,"active yaw locked");
                var previous=a.Swing;Advance(previous.ChainAt-Time);a.Input.Attack=true;StepMeleeInput(a);
                Check(a.Swing!=previous&&a.Swing.Distance==.9f,"chain starts with default step without lock");NearNative(a.Yaw,heading,"chain does not snap yaw");
                float comboEnd=a.MeleeComboUntil;a.Input.Attack=false;Advance(comboEnd-Time+.001f);StepMelee(a);a.Input.Attack=true;StepMeleeInput(a);Check(a.Combo==0&&a.Swing.Index==1,"combo reset after pause");
                Setup();Position(b,new Vector3(1000,100,1005));wall.SetActive(true);Physics.SyncTransforms();a.Input.LockTarget=b.Seat.id;TryMelee(a);NearNative(a.Swing.Distance,.9f,"cover refuses target-assisted lunge");
                Setup("greatsword");Position(b,new Vector3(1000,100,1020));TryMelee(a);previous=a.Swing;Advance(previous.HitAt-Time);a.Input.Attack=true;StepMeleeInput(a);
                a.Input.Attack=false;Advance(previous.ChainAt-Time);StepMeleeInput(a);Check(a.Swing==previous,"greatsword expired buffer must not chain");
                Setup();TryMelee(a);previous=a.Swing;Advance(previous.ChainAt-Time);a.Input.Shoot=true;StepMeleeInput(a);Check(a.Swing==previous,"held input must retain full recovery");
                Setup();ClearRemoteInput(b.Seat.id);AcceptInput(b.Seat.id,new InputFrame{Sequence=1,Attack=true,Shoot=true,LockTarget=a.Seat.id});AcceptInput(b.Seat.id,new InputFrame{Sequence=2,LockTarget=a.Seat.id});
                var tap=RemoteInput(b.Seat.id);Check(tap.Attack&&!tap.Shoot&&tap.LockTarget==a.Seat.id,"coalesced release lost LMB edge");
                AcceptInput(b.Seat.id,new InputFrame{Sequence=2,Attack=true});Check(!RemoteInput(b.Seat.id).Attack,"edge replayed after consumption/duplicate");ClearRemoteInput(b.Seat.id);
                Debug.Log("PASS native melee feel: host lock/LOS lunge, sprint/opener flags, bounded windup turning, active/chain yaw, combo reset, buffer expiry, held recovery and coalesced input edge");
                Debug.Log("PASS native melee contacts: four primaries, late sweep entry, cover, team/i-frame protection, riposte/counter, cancellation and impulse integration");
            }
            finally{CapturedAnimation.Release(wall);}
        }
        private static void NearNative(float actual,float expected,string label){if(Math.Abs(actual-expected)>.0002f)throw new Exception("Native melee gate: "+label+" "+actual+" != "+expected);}
    }
}
#endif
