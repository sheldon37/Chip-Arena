using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeFighter
    {
        public NativeFighter ViewCopy()=>(NativeFighter)MemberwiseClone();
        public string Kind;public int Id,Index,Total,Next,Count;public float At,HitAt,Until,Gap,HadokenReady,UppercutReady,LastHit=-100,LastUntil,Charge,StepUntil;
        public int ComboTarget,Hits;public float ComboDamage;public bool Single;
    }
    public sealed class NativeJuggle
    {public NativePilot Owner;public float At,Until,LastHit,SpinUntil;public int Hits,ElectricHits;public bool Bound,Wall,Pickup;}
    public sealed class NativeFighterEcho
    {public NativePilot Owner,Spare;public Vector3 Position,Direction;public float At,Until,Radius,Damage,Impulse,Gap;public int Remaining;public bool Electric;}
    public sealed class NativeStormTrail
    {public NativePilot Owner;public Vector3 Position;public float At,Until;public readonly HashSet<int> Hit=new HashSet<int>();}
    public sealed partial class NativePilot { public NativeFighter Fighter;public NativeJuggle Juggle; }
    public sealed partial class NativeArena
    {
        public readonly List<NativeFighterEcho> FighterEchoes=new List<NativeFighterEcho>();public readonly List<NativeStormTrail> StormTrails=new List<NativeStormTrail>();
        private bool Dev(NativePilot p,string spec)=>p!=null&&p.Seat.spec==spec&&(p.Seat.AuthorityBoss&&spec=="siren"||p.Seat.access.Allows("weapon:"+spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        private NativeFighter Fighter(NativePilot p)=>p.Fighter??(p.Fighter=new NativeFighter());
        private void CancelFighter(NativePilot p){if(p.Fighter==null)return;p.Fighter.Kind=null;p.Fighter.Charge=p.Fighter.StepUntil=0;p.Fighter.Count=p.Fighter.Next=0;}
        private bool FighterReady(NativePilot p)=>p.Hp>0&&p.Seat.connected&&!Incapacitated(p)&&Time>=p.BlockEnd&&Time>=p.RollEnd;
        private bool StartFighter(NativePilot p,string kind)
        {
            bool emperor=Dev(p,"emperor");if(!(emperor||Dev(p,"ansatsuken"))||!FighterReady(p))return false;var f=Fighter(p);
            bool cancel=!emperor&&kind!="combo"&&Has(p,"arcade-cancel")&&f.Kind=="combo"&&f.LastHit>=f.At&&Time-f.LastHit<.5f;
            if((Time<p.Ready||f.Kind!=null)&&!cancel)return false;if(kind=="tatsu"&&Time<p.EReady||kind=="shoryuken"&&Time<f.UppercutReady)return false;
            bool single=kind=="combo"||kind=="jab",electric=kind=="electric";
            int total=emperor?(electric?1:Has(p,"mishima-pressure")?3:2):kind=="combo"?(Has(p,"arcade-rush")?10:3):kind=="tatsu"?(Has(p,"arcade-cyclone")?7:4):(Has(p,"arcade-rising")?3:1);
            int index=single&&f.Single&&f.LastUntil>f.At&&Time<=f.LastUntil+2.4f?(f.Index+1)%total:0;
            float cost=(emperor?(electric?0:6f/total):kind=="combo"?(total==10?22f:12f)/total:kind=="tatsu"?22:25)*(float)p.Stats["resourceCost"];if(p.Stamina<cost)return false;p.Stamina-=cost;
            float rate=(float)p.Stats["attackRate"]*(electric&&Has(p,"mishima-godfist")?1.4f:1),gap=(emperor?.17f:kind=="combo"?(total==10?.115f:.25f):kind=="tatsu"?.18f:.16f)/rate;
            float hit=Time+(emperor?(electric?.1f:.075f):kind=="shoryuken"?.08f:kind=="combo"?Math.Min(.12f,gap*rate*.6f):.12f)/rate;
            float until=emperor?(electric?Time+.4f/rate:index==total-1?hit+.14f/rate:Time+gap):kind=="combo"?(index==total-1?hit+.22f:Time+gap):hit+(total-1)*gap+(kind=="shoryuken"?.68f:.22f);
            f.Kind=kind;f.Id=nextAttack++;f.At=Time;f.HitAt=hit;f.Gap=gap;f.Index=index;f.Total=total;f.Count=single?1:total;f.Next=0;f.Until=f.LastUntil=until;f.Single=single;f.Charge=0;
            p.Combo=index;p.Yaw=p.Input.Yaw;p.AttackAt=p.LastAttack=Time;p.AttackEnd=p.Ready=until;p.RegenAt=until+.45f;p.MoveVelocity=Vector3.zero;
            if(kind=="combo")ChipMeleeStart(p);
            float recovery=Has(p,"arcade-cancel")?.72f:1;
            if(kind=="tatsu"){p.EReady=Time+5*recovery;p.Knockback+=Vec(V3.Forward(p.Input.Yaw))*5+Vector3.up*3;}
            if(kind=="shoryuken"){f.UppercutReady=Time+6*recovery;p.Knockback+=Vec(V3.Forward(p.Input.Yaw))*4+Vector3.up*10;}
            if(electric&&p.Grounded)f.StepUntil=Time+.1f;
            return true;
        }
        private void StepFighter(NativePilot p)
        {
            bool emperor=Dev(p,"emperor"),arcade=Dev(p,"ansatsuken");if(!emperor&&!arcade){CancelFighter(p);return;}var f=Fighter(p);if(!FighterReady(p)){CancelFighter(p);return;}
            if(f.Kind=="charge")
            {f.Charge=Mathf.Clamp01((Time-f.At)/1.4f);if(!p.Input.HadokenHeld||Time>=f.Until)Hadoken(p,f.Charge>=1);}
            if(f.Kind!=null&&f.Kind!="charge")
            {
                while(f.Next<f.Count&&Time>=f.HitAt+f.Next*f.Gap){int index=f.Single?f.Index:f.Next;FighterStrike(p,f.Kind,index,f.Total,f.Id);if(!FighterReady(p)||f.Kind==null)return;f.Next++;}
                if(f.Kind=="tatsu"&&Has(p,"arcade-cyclone"))foreach(var q in Pilots){Vector3 d=p.Body.position-q.Body.position;if(q.Hp>0&&CanHit(p,q)&&d.magnitude<4.5f&&MeleeVisible(p.Body.position,q.Body.position,p,q.Collider))q.Knockback+=d.normalized*(7f/60);}
                if(Time>=f.Until)f.Kind=null;
            }
            if(p.Input.Shoot)StartFighter(p,emperor?"jab":"combo");
            if(emperor)
            {
                if(p.Input.AbilityQ)StartFighter(p,"electric");
                if(p.Input.AbilityEHeld&&Time>=p.Ready&&f.Kind==null){f.Kind="wave";f.At=Time;f.Total=1;f.Until=Time+.18f;p.AttackEnd=f.Until;f.Next=f.Count=0;if(Has(p,"mishima-trail")&&StormTrails.Count<24)StormTrails.Add(new NativeStormTrail{Owner=p,Position=p.Body.position,At=Time,Until=Time+.7f});}
            }
            else
            {
                if(p.Input.AbilityE)StartFighter(p,"tatsu");if(p.Input.Uppercut)StartFighter(p,"shoryuken");
                if(p.Input.Hadoken&&Time>=f.HadokenReady&&Time>=p.Ready&&f.Kind==null)
                {if(Has(p,"arcade-shinku")){if(p.Stamina>=12*p.Stats["resourceCost"]){f.Kind="charge";f.At=Time;f.Until=Time+3;f.Single=false;f.Charge=0;}}else Hadoken(p,false);}
            }
        }
        private Vector3 FighterMotion(NativePilot p)
        {var f=p.Fighter;if(f==null)return Vector3.zero;return Vec(V3.Forward(p.Input.Yaw))*(f.Kind=="wave"&&Time<f.Until?9:f.Kind=="electric"&&Time<f.StepUntil?6:0);}
        private void Hadoken(NativePilot p,bool super)
        {
            var f=Fighter(p);float cost=(super?30:12)*(float)p.Stats["resourceCost"];if(ProjectileCount>=100||p.Stamina<cost){CancelFighter(p);return;}p.Stamina-=cost;
            Vector3 dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)),pos=p.Body.position+Vector3.up*.18f+dir*.72f;
            var b=new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec=p.Seat.spec,Born=Time,Position=pos,Velocity=dir*(super?27:23),Damage=(super?22:23)*(float)p.Stats["damage"],Impulse=(super?180:320)*(float)p.Stats["impulse"],Radius=(super?.5f:.24f)*(float)p.Stats["projectileSize"],Life=4,Travel=75,Direct=false,EffectsUsed=true,Pierces=super?7:0,Burn=(Has(p,"arcade-red")?12:4)+(float)p.Stats["burn"],Poison=(float)p.Stats["poison"],Hadoken=true,Super=super,Mods=new Dictionary<string,int>(p.Seat.chips)};Bullets.Add(b);
            f.Kind=super?"shinku":"hadoken";f.At=Time;f.Until=Time+.48f;f.Single=false;f.Charge=0;f.Count=f.Next=0;f.HadokenReady=Time+(super?3.4f:1.1f);p.AttackAt=p.LastAttack=p.LastShot=Time;p.BlockEnd=0;p.AttackEnd=p.Ready=f.Until;p.RegenAt=f.Until+.45f;Emit(EventKind.Shot,p,0,pos,0);
        }
        private void FighterStrike(NativePilot p,string kind,int index,int total,int id)
        {
            bool emperor=Dev(p,"emperor"),electric=kind=="electric",last=index==total-1;var f=Fighter(p);f.Index=index;p.Combo=index;p.LastAttack=Time;
            Vector3 origin=p.Body.position+Vector3.up*(emperor?.18f:.15f),dir=Vec(V3.Forward(p.Input.Yaw));float radius=(emperor?(electric?2.4f:2):kind=="tatsu"?2.65f:kind=="shoryuken"?2.05f:2)*(float)p.Stats["reach"];
            float damage=emperor?(electric?8:index==2?18:index>0?12:8):kind=="combo"?(last?19:total==10?6:index==0?9:11):kind=="tatsu"?9:last?30:11;
            float impulse=(emperor?(electric?120:65):last?(kind=="shoryuken"?850:kind=="combo"?650:450):45)*(float)p.Stats["impulse"];
            foreach(var q in Pilots.ToArray())
            {
                Vector3 d=q.Body.position-origin;float distance=emperor?new Vector2(d.x,d.z).magnitude:d.magnitude;
                if(q.Hp<=0||!CanHit(p,q)||distance>radius+(emperor?.35f:.4f)||Math.Abs(d.y)>(emperor?(electric?3.2f:2.1f):kind=="shoryuken"?3:1.8f)||kind!="tatsu"&&Vector3.Dot(new Vector3(d.x,0,d.z).normalized,dir)<Mathf.Cos((emperor?.9f:.8f)*(float)p.Stats["meleeArc"])||!MeleeVisible(origin,q.Body.position,p,q.Collider))continue;
                float scale=emperor?(Has(p,"mishima-rage")&&p.Hp<p.Stats["hp"]/3?1.45f:1)*(electric&&Has(p,"mishima-counter")&&Time-q.LastAttack<.4f?1.6f:1)*(Has(p,"mishima-screw")&&((q.Juggle?.Hits??0)+1)%3==0?1.25f:1):(Has(p,"arcade-juggle")&&!q.Grounded?1.45f:1)*(Has(p,"arcade-counter")&&Time-q.LastAttack<.35f?1.35f:1);
                Vector3 aim=electric?(dir+Vector3.up*1.2f).normalized:kind=="shoryuken"?(dir*.3f+Vector3.up).normalized:(d.normalized+Vector3.up*(last?.25f:0)).normalized;
                Emit(emperor?EventKind.Emperor:EventKind.Arcade,p,q.Seat.id,q.Body.position,damage,p.Fighter?.Kind);float actual=Damage(q,p,damage*(float)p.Stats["damage"]*scale,aim,impulse*(!emperor&&Has(p,"arcade-counter")&&Time-q.LastAttack<.35f?1.4f:1),false,melee:true,weaponHit:true);
                if(actual>0){ImpactChips(p,q,null,q.Body.position,aim,id,id+index);ConfirmFighter(p,q,actual,electric,last?.38f:kind=="tatsu"?.22f:total==10?.15f:.3f);if(!emperor&&last&&kind=="shoryuken"&&q.Hp>0&&!SirenArmor(q)){bool pickup=q.Down?.PickupUsed==true||q.Juggle?.Pickup==true||NativeSpecialRules.Pickup(q.Down,Time);q.Juggle=null;q.Down=new NativeRecovery{At=Time,Until=Time+5,PickupUsed=pickup};q.Grounded=false;q.MoveVelocity=Vector3.zero;}if(!emperor&&last&&Has(p,"arcade-perfect")){p.Stamina=Math.Min((float)p.Stats["maxStamina"],p.Stamina+22);Heal(p,8);}if(electric&&Has(p,"mishima-twin")&&FighterEchoes.Count<32)FighterEchoes.Add(new NativeFighterEcho{Owner=p,Position=q.Body.position,At=Time+.13f,Until=Time+.15f,Radius=1.6f,Damage=3*(float)p.Stats["damage"],Remaining=1,Electric=true});}
                if(!FighterReady(p)||f.Kind==null)return;
            }
            foreach(var prop in Props.ToArray())
            {if(prop==null||prop.Indestructible)continue;Vector3 d=prop.transform.position-origin;if(d.magnitude>radius+.4f||kind!="tatsu"&&Vector3.Dot(new Vector3(d.x,0,d.z).normalized,dir)<Mathf.Cos((emperor?.9f:.8f)*(float)p.Stats["meleeArc"])||!MeleeVisible(origin,prop.transform.position,p,prop.GetComponent<Collider>()))continue;ImpactChips(p,null,prop,prop.transform.position,d.normalized,id,id+index);DamageProp(prop,damage*(float)(p.Stats["damage"]*p.Stats["demolition"]),d.normalized*impulse,prop.transform.position,p);}
            foreach(var m in Minions.ToArray()){Vector3 d=m.Body.position-origin;if(p.Hp>0&&CanHit(p,m)&&d.magnitude<radius+(emperor?.5f:.4f)&&(kind=="tatsu"||Vector3.Dot(d.normalized,dir)>(emperor?.2f:.25f))&&MeleeVisible(origin,m.Body.position,p,m.Collider))Heal(p,HitMinion(m,p,(emperor?(electric?8:10):damage)*(float)p.Stats["damage"],emperor?dir:d.normalized,emperor?(electric?550:150):impulse)*(float)p.Stats["lifeSteal"]);}
            if(!emperor&&kind=="combo"&&Has(p,"arcade-afterimage")&&FighterEchoes.Count<64)FighterEchoes.Add(new NativeFighterEcho{Owner=p,Position=origin+dir*1.1f,Direction=dir,Radius=1.1f*(float)p.Stats["reach"],Damage=damage*.38f*(float)p.Stats["damage"],Impulse=50,At=Time+.22f,Until=Time+.3f,Remaining=1});
            if(kind=="shoryuken"&&last&&Has(p,"arcade-rising"))foreach(var prop in Props.ToArray())if(prop!=null&&!prop.Indestructible&&Vector3.Distance(prop.transform.position,origin)<3.5f)DamageProp(prop,80,(prop.transform.position-origin+Vector3.up*2).normalized*1500*(float)p.Stats["impulse"],prop.transform.position,p);
            // No swing blast: the source draws none (emperor-effects.ts bolts and the C12 DEV effects cover the fighters), and the
            // old stand-in drew the lightning (Shock) blast on every basic hit (owner, 2026-10-09). Presentation feed only.
        }
        private void ConfirmFighter(NativePilot p,NativePilot q,float actual,bool electric,float stun,bool echo=false)
        {
            var f=Fighter(p);bool emperor=Dev(p,"emperor");if(f.ComboTarget!=q.Seat.id||Time-f.LastHit>(emperor?1.35f:1.1f)){f.Hits=0;f.ComboDamage=0;}f.ComboTarget=q.Seat.id;f.Hits++;f.ComboDamage+=actual;f.LastHit=Time;
            if(q.Hp>0&&!SirenArmor(q))
            {
                var old=q.Juggle;bool pickup=NativeSpecialRules.Pickup(q.Down,Time),air=old!=null||!q.Grounded||pickup;StunMelee(q,Math.Max(.09f,(emperor?(electric?.42f:.25f):stun)/(float)p.Stats["attackRate"]));
                if(emperor&&(electric||air))
                {
                    var j=old??new NativeJuggle{At=Time,Owner=p,Pickup=pickup||q.Down?.PickupUsed==true};j.Hits++;if(electric&&!echo)j.ElectricHits++;j.LastHit=Time;j.Until=Time+1.25f;j.Owner=p;if(Has(p,"mishima-screw")&&j.Hits%3==0)j.SpinUntil=Time+.65f;q.Juggle=j;q.Down=null;
                    float h=q.Body.position.y-p.Body.position.y,vy=electric?Mathf.Clamp(7.7f-h*3.5f,1.5f,10):Mathf.Clamp(5-h*2,1,6);q.Knockback.y=vy-q.Body.linearVelocity.y;q.Grounded=false;
                    Vector3 dir=Vec(V3.Forward(p.Input.Yaw));float push=(Math.Min(36,3+Math.Max(0,j.ElectricHits-1)*4.5f)+(Has(p,"mishima-wall")?4:0))*(float)(p.Stats["impulse"]*q.Stats["knockbackTaken"]);q.Knockback.x=dir.x*push;q.Knockback.z=dir.z*push;
                    if(Has(p,"mishima-magnet")){Vector3 d=p.Body.position+dir*1.45f-q.Body.position,lateral=d-dir*Vector3.Dot(d,dir);lateral.y=0;q.Knockback+=lateral.normalized*Math.Min(6,lateral.magnitude*5);}
                }
            }
            if(electric&&p.Hp>0&&Has(p,"mishima-bloodline")){Heal(p,5);p.Stamina=Math.Min((float)p.Stats["maxStamina"],p.Stamina+12);}
        }
        private void StepFighterWorld()
        {
            foreach(var e in FighterEchoes.ToArray())
            {if(e.Remaining<=0||Time>e.Until||e.Owner.Hp<=0){FighterEchoes.Remove(e);continue;}if(Time<e.At)continue;foreach(var q in Pilots)if(q!=e.Spare&&q.Hp>0&&CanHit(e.Owner,q)&&Vector3.Distance(q.Body.position,e.Position)<e.Radius&&MeleeVisible(e.Position,q.Body.position,e.Owner,q.Collider)){if(Time<q.BlockEnd){BlockProc(q,e.Owner,q.Body.position);continue;}float actual=Damage(q,e.Owner,e.Damage,e.Direction,e.Remaining==1?e.Impulse:25,false,primary:false);if(actual>0)ConfirmFighter(e.Owner,q,actual,e.Electric,e.Remaining==1?.26f:.14f,true);}e.Remaining--;e.At+=e.Gap;}
            foreach(var trail in StormTrails.ToArray())
            {if(Time>=trail.Until||trail.Owner.Hp<=0){StormTrails.Remove(trail);continue;}foreach(var q in Pilots)if(q.Hp>0&&CanHit(trail.Owner,q)&&!trail.Hit.Contains(q.Seat.id)&&Vector3.Distance(q.Body.position,trail.Position)<1.4f&&MeleeVisible(trail.Position,q.Body.position,trail.Owner,q.Collider)){trail.Hit.Add(q.Seat.id);Damage(q,trail.Owner,6*(float)trail.Owner.Stats["damage"],Vector3.zero,0,false,primary:false);}}
            foreach(var q in Pilots)
            {
                var j=q.Juggle;if(j==null)continue;if(q.Hp<=0||!q.Seat.connected){q.Juggle=null;continue;}var p=j.Owner;
                if(q.Grounded&&Time-j.LastHit>.15f&&q.Body.linearVelocity.y<.5f)
                {if(p.Hp>0&&Has(p,"mishima-bound")&&!j.Bound&&!j.Pickup){j.Bound=j.Pickup=true;j.LastHit=Time;j.Until=Time+.85f;q.Knockback.y=6.5f-q.Body.linearVelocity.y;q.Grounded=false;Damage(q,p,8*(float)p.Stats["damage"],Vector3.zero,0,false,direct:false,primary:false);}else{q.Juggle=null;q.Down=new NativeRecovery{At=j.At,Until=Time+1,PickupUsed=j.Pickup};NativeSpecialRules.Land(q.Down,Time);}}
                if(q.Juggle!=null&&p.Hp>0&&Has(p,"mishima-wall")&&!j.Wall&&Time-j.LastHit>.06f)
                {Vector3 dir=Vec(V3.Forward(p.Input.Yaw));if(root.scene.GetPhysicsScene().Raycast(q.Body.position,dir,out var hit,.75f,WorldMask)&&hit.collider.bounds.size.y>1){j.Wall=true;j.LastHit=Time;j.Until=Time+.85f;q.Knockback=-dir*2.5f+Vector3.up*(6-q.Body.linearVelocity.y);Damage(q,p,12*(float)p.Stats["damage"],Vector3.zero,0,false,direct:false,primary:false);}}
            }
        }
        private void FighterProjectileHit(NativeBullet b,NativePilot p,NativePilot q,float actual)
        {
            if(!b.Hadoken||actual<=0)return;RefreshDot(q,p,"fire",b.Burn,2);RefreshDot(q,p,"poison",b.Poison,4);ConfirmFighter(p,q,actual,false,b.Super?.15f:.26f);
            if(b.Super&&FighterEchoes.Count<64)FighterEchoes.Add(new NativeFighterEcho{Owner=p,Position=q.Body.position,Direction=b.Velocity.normalized,At=Time+.1f,Until=Time+.5f,Radius=1.5f,Damage=Math.Min(40,b.Damage*.45f),Impulse=550,Remaining=4,Gap=.1f});
        }
    }
}
