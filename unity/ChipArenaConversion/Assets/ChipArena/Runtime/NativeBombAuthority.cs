using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    internal sealed class BombFlight
    {public NativeBomb Record;public float Hp=15,Expires,DamageOverride;public bool Encore;public int Bounces;public bool Landed,Detached;public Vector3 Offset;public V3 Handoff;public bool Handed;}
    public sealed partial class NativePilot
    {
        public NativeBombKit Bomb=new NativeBombKit{MineCharges=2,Ready=new float[6]};
        internal readonly List<BombFlight> BombFlights=new List<BombFlight>();
        internal readonly Dictionary<int,NativeBombChain> BombChains=new Dictionary<int,NativeBombChain>();
        internal bool BombReleased,BombStruck;internal int BombLineIndex;internal Vector3 BombAim,BombPoint,BombFacing;
        public float BombNoSteerUntil;internal float BombMineReady;internal int BombMineCapacity=2;
    }
    public sealed partial class NativeArena
    {
        private int BombStacks(NativePilot p,string id)=>Has(p,id)&&p.Seat.chips.TryGetValue(id,out int n)?Math.Min(2,Math.Max(0,n)):0;
        private float BombMeterRate(NativePilot p)=>Has(p,NativeBombChips.Encore)?1.25f:1;
        private NativeBombChain BombChain(NativePilot p,int id)
        {
            if(!p.BombChains.TryGetValue(id,out var c))p.BombChains[id]=c=new NativeBombChain();
            c.Daisy=Has(p,NativeBombChips.DaisyChain);c.Window=c.Daisy?1.6f:1.2f;c.Prepare(Time);return c;
        }
        private void BombMineCapacity(NativePilot p)
        {
            int capacity=2+BombStacks(p,NativeBombChips.BouncingBetty);
            p.Bomb.MineCharges=Math.Min(capacity,p.Bomb.MineCharges+Math.Max(0,capacity-p.BombMineCapacity));p.BombMineCapacity=capacity;
            if(p.Bomb.MineCharges>=capacity)p.BombMineReady=0;
            p.Bomb.Ready[2]=p.Bomb.MineCharges>0?0:p.BombMineReady;
        }
        private bool BombAllowed(NativePilot p)=>p.Seat.spec==NativeBombs.Spec&&(p.Seat.access.DeveloperWeapon(NativeBombs.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())||p.Seat.devWeaponAccess?.DeveloperWeapon(NativeBombs.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())==true);
        private bool BombPending()=>Pilots.Any(p=>(p.Bomb.Finale!=null&&!p.BombStruck)||p.BombFlights.Any(b=>b.Encore));
        private bool BombRooted(NativePilot p)=>p.Seat.spec==NativeBombs.Spec&&p.Bomb.Finale!=null&&Time<p.Bomb.Finale.Until;
        private bool BombBusy(NativePilot p)=>p.Seat.spec==NativeBombs.Spec&&(Time<p.Bomb.CastUntil||BombRooted(p));
        private void BombMotion(NativePilot p,ref Vector3 v)
        {
            if(BombRooted(p)){v=Vector3.zero;p.Knockback=p.MoveVelocity=Vector3.zero;p.Body.useGravity=false;}
            else if(Time<p.BombNoSteerUntil){v.x=p.Knockback.x;v.z=p.Knockback.z;p.MoveVelocity=Vector3.zero;}
        }
        private void BotBomb(NativePilot p,ref InputFrame i,Vector3 target,float distance,bool visible)
        {
            i.Attack=false;i.Shoot=visible&&distance<16;if(!BombAllowed(p)||BombBusy(p))return;
            if(p.Bomb.Meter>=1&&visible)i.SirenAbility=5;
            else if(distance<3&&Time>=p.Bomb.Ready[0])i.AbilityQ=true;
            else if(p.BombFlights.Any(b=>b.Landed&&Vector3.Distance(Vec(b.Record.Pos),target)<3)&&Time>=p.Bomb.Ready[1])i.AbilityE=true;
            else if(visible&&distance<18){for(int n=4;n>=1;n--)if(Time>=p.Bomb.Ready[n+1]&&(n!=1||p.Bomb.MineCharges>0)){i.SirenAbility=(byte)n;break;}}
            if(i.SirenAbility>0||i.AbilityQ||i.AbilityE)i.Shoot=i.Roll=false;
        }
        private void StepBombInput(NativePilot p)
        {
            if(!BombAllowed(p)||p.Hp<=0||BombBusy(p)||Incapacitated(p)||Time<p.RollEnd||Time<p.BlockEnd||p.ReloadEnd>0||Time<p.Ready||HubInside(p.Body.position))return;
            BombMineCapacity(p);var k=p.Bomb;string kind=null;int slot=-1;
            if(p.Input.SirenAbility==5&&k.Meter>=1)kind=NativeBombs.GrandFinale;
            else {slot=p.Input.AbilityQ?0:p.Input.AbilityE?1:p.Input.SirenAbility>=1&&p.Input.SirenAbility<=4?p.Input.SirenAbility+1:-1;if(slot>=0&&Time>=k.Ready[slot]&&(slot!=2||k.MineCharges>0))kind=NativeBombs.Slot[slot];}
            if(kind==null&&p.Input.Shoot){if(p.Ammo<=0){Reload(p);return;}kind=NativeBombs.PopBomb;slot=-1;}
            if(kind==null)return;
            k.CastKind=kind;k.CastAt=Time;k.CastRelease=Time+(kind==NativeBombs.ClusterLob?.28f:.16f);k.CastUntil=k.CastRelease+.4f;p.BombReleased=false;
            p.BombAim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));p.BombFacing=Vec(V3.Forward(p.Input.Yaw));p.BombPoint=AimedAbilityPoint(p,18);p.AttackAt=p.LastAttack=Time;p.Ready=kind==NativeBombs.PopBomb?Time+NativeBombs.PopPeriod:k.CastUntil;
            if(kind==NativeBombs.PopBomb)p.Ammo--;
            else if(kind==NativeBombs.GrandFinale)
            {
                k.Meter=0;p.BombStruck=false;var feet=p.Body.position-Vector3.up*.865f;var box=NativeBombs.BoxSpot(new System.Numerics.Vector3(feet.x,feet.y,feet.z),new System.Numerics.Vector3(p.BombFacing.x,0,p.BombFacing.z));
                k.Finale=new NativeBombFinale{At=Time,Lock=Time+2.1f,Strike=Time+2.6f,Until=Time+3.6f,Box=new V3(box.X,box.Y,box.Z),Dir=Vec(p.BombFacing),Circles=BombTargets(p).Where(t=>Vector3.Distance(t.at,p.Body.position)<=40).OrderBy(t=>Vector3.Distance(t.at,p.Body.position)).Take(NativeBombs.MaxCircles).Select(t=>new NativeBombCircle{Id=t.id,Pos=Vec(GroundPoint(t.at))}).ToArray()};k.CastUntil=k.Finale.Until;
            }
            else if(slot==2){k.MineCharges--;if(p.BombMineReady<=0)p.BombMineReady=Time+7;k.Ready[2]=k.MineCharges>0?0:p.BombMineReady;}
            else if(slot>=0)k.Ready[slot]=Time+(slot==0?NativeBombChipRules.JumpCooldown(BombStacks(p,NativeBombChips.NitroBoots)):slot==4&&Has(p,NativeBombChips.ClusterFun)?8.8f:new[]{8f,2,7,9,11,12}[slot]);
            Emit(EventKind.AbilityCast,p,0,p.Body.position,0,kind);
        }
        private IEnumerable<(int id,Vector3 at)> BombTargets(NativePilot p)
        {
            foreach(var q in Pilots)if(q.Hp>0&&q.Seat.connected&&Enemy(p,q))yield return(q.Seat.id,q.Body.position);
            foreach(var m in Minions)if(m.Hp>0&&Enemy(p,m))yield return(m.Id,m.Body.position);
        }
        private BombFlight AddBomb(NativePilot p,string kind,Vector3 pos,Vector3 velocity,float at,float fuse)
        {
            if(p.BombFlights.Count>=NativeBombs.MaxBombs)return null;
            var flight=new BombFlight{Expires=at+20,Record=new NativeBomb{Id=nextBullet++,Kind=kind,Pos=Vec(pos),Vel=Vec(velocity),At=at,SampleAt=at,ArmAt=at+(kind==NativeBombs.Mine?.5f:0),BlowAt=fuse<0?-100:at+fuse,Normal=new V3(0,1,0)}};p.BombFlights.Add(flight);return flight;
        }
        private void BombRelease(NativePilot p)
        {
            var k=p.Bomb;var feet=p.Body.position-Vector3.up*.865f;var hand=p.Body.position+Vector3.up*.4f;float at=k.CastRelease;
            switch(k.CastKind)
            {
                case NativeBombs.PopBomb:AddBomb(p,NativeBombs.Pop,hand,p.BombAim*(float)p.Weapon.speed*NativeBombChipRules.PopSpeed(BombStacks(p,NativeBombChips.ShortFuse)),at,NativeBombChipRules.PopFuse(BombStacks(p,NativeBombChips.ShortFuse)));break;
                case NativeBombs.BounceMine:
                    var mines=p.BombFlights.Where(b=>b.Record.Kind==NativeBombs.Mine).OrderBy(b=>b.Record.At).ToArray();if(mines.Length>=3+BombStacks(p,NativeBombChips.BouncingBetty))p.BombFlights.Remove(mines[0]);
                    AddBomb(p,NativeBombs.Mine,hand,(p.BombPoint-hand)/.65f+Vector3.up*(23*.65f/2),at,-1);break;
                case NativeBombs.StickyFuse:AddBomb(p,NativeBombs.Sticky,hand,p.BombAim*25,at,1.2f);break;
                case NativeBombs.ClusterLob:
                    AddBomb(p,NativeBombs.Lob,hand,(p.BombPoint+Vector3.up*4-hand)/.72f+Vector3.up*(23*.72f/2),at,.72f);break;
                case NativeBombs.BlastJump:
                    BombBlast(p,NativeBombs.Jump,feet,2.5f,8,5,skipOwner:true);float distance=NativeBombChipRules.JumpDistance(BombStacks(p,NativeBombChips.NitroBoots));
                    BombLaunch(p,p,p.BombFacing*(8*Mathf.Sqrt(distance)),Mathf.Sqrt(2*23*9*distance),0);
                    if(Has(p,NativeBombChips.NitroBoots))AddBomb(p,NativeBombs.Bomblet,feet+Vector3.up*.2f,Vector3.up*2,at,.45f);break;
                case NativeBombs.DeadMansSwitch:
                    int n=0;V3 prev=Vec(feet);foreach(var b in p.BombFlights.OrderBy(b=>(b.Record.Pos-Vec(feet)).Length)){b.Record.BlowAt=Time+n++*.12f;b.Handoff=prev;b.Handed=true;prev=b.Record.Pos;}break;
                case NativeBombs.FirecrackerLine:
                    var start=GroundPoint(feet);var end=start+p.BombFacing*16;var hit=SweepNative(start+Vector3.up*.3f,end-start,.1f,c=>c.gameObject.layer==PropLayer);if(hit.HasValue)end=start+p.BombFacing*Math.Max(0,hit.Value.Distance-.1f);
                    k.Line=new NativeBombLine{At=at,From=Vec(start),To=Vec(end),Speed=14};p.BombLineIndex=0;break;
            }
        }
        private void StepBombWorld()
        {
            foreach(var p in Pilots)
            {
                if(p.Seat.spec!=NativeBombs.Spec)continue;var k=p.Bomb;
                if(k.Ready==null||k.Ready.Length!=6)k.Ready=new float[6];
                BombMineCapacity(p);while(p.BombMineReady>0&&Time>=p.BombMineReady){k.MineCharges=Math.Min(p.BombMineCapacity,k.MineCharges+1);p.BombMineReady=k.MineCharges<p.BombMineCapacity?p.BombMineReady+7:0;k.Ready[2]=0;}
                if(p.Hp>0&&p.Seat.connected&&BombAllowed(p))k.Meter=Math.Min(1,k.Meter+BombMeterRate(p)/6000);
                if(!p.BombReleased&&k.CastKind!=null&&Time>=k.CastRelease)
                {p.BombReleased=true;if(p.Hp>0&&p.Seat.connected&&!Incapacitated(p)&&Time>=p.RollEnd)BombRelease(p);}
                foreach(var b in p.BombFlights.ToArray())
                {
                    var r=b.Record;if(Time<r.At)continue;if(Time>=b.Expires){p.BombFlights.Remove(b);continue;}
                    if(r.Stuck!=0)
                    {
                        var q=Pilots.Find(q=>q.Seat.id==r.Stuck);var m=Minions.Find(m=>m.Id==r.Stuck);
                        if(q!=null&&q.Hp>0&&Time>=q.RollEnd)r.Pos=Vec(q.Body.position+b.Offset);
                        else if(m!=null&&m.Hp>0)r.Pos=Vec(m.Body.position+b.Offset);
                        else {if(q!=null)r.Pos=Vec(q.Body.position+b.Offset);else if(m!=null)r.Pos=Vec(m.Body.position+b.Offset);r.Stuck=0;b.Landed=false;b.Detached=true;r.Vel=new V3(0,-1,0);}
                    }
                    if(!b.Landed&&r.Stuck==0)
                    {
                        // A shaken-off fuse remains live, but cannot stick again or restart its clock.
                        var from=Vec(r.Pos);var vel=Vec(r.Vel);vel.y-=23/60f;var delta=vel/60;
                        if(HubInside(from)||HubCross(from,from+delta,.12f)){p.BombFlights.Remove(b);continue;}
                        var hit=SweepNative(from,delta,.12f,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?!b.Detached&&q.Hp>0&&CanHit(p,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?!b.Detached&&m.Hp>0&&CanHit(p,m):true));
                        if(hit.HasValue)
                        {
                            var h=hit.Value;r.Pos=Vec(from+delta.normalized*Math.Max(0,h.Distance-.01f));r.Normal=Vec(h.Normal);var q=h.Collider.GetComponent<NativePilotBody>()?.Pilot;var m=h.Collider.GetComponent<NativeMinionBody>()?.Minion;
                            if(r.Kind==NativeBombs.Sticky){r.Stuck=q?.Seat.id??m?.Id??0;b.Offset=Vec(r.Pos)-(q?.Body.position??m?.Body.position??Vec(r.Pos));b.Landed=true;r.Vel=default;if(!b.Detached)r.BlowAt=Time+1.2f;}
                            else if(r.Kind==NativeBombs.Mine){b.Landed=true;r.Vel=default;r.ArmAt=Time+.5f;}
                            else if(r.Kind==NativeBombs.Pop&&q==null&&m==null&&b.Bounces++==0){r.Vel=Vec(Vector3.Reflect(vel,h.Normal)*.55f);r.Pos+=Vec(h.Normal*.03f);}
                            else r.BlowAt=Time;
                        }
                        else {r.Pos=Vec(from+delta);r.Vel=Vec(vel);}
                    }
                    r.SampleAt=Time;
                    if(r.Kind==NativeBombs.Mine&&b.Landed&&Time>=r.ArmAt&&BombTargets(p).Any(t=>Vector3.Distance(t.at,Vec(r.Pos))<=NativeBombs.MineReach))r.BlowAt=Time;
                    if(r.BlowAt>-50&&Time>=r.BlowAt)ExplodeBomb(p,b);
                }
                if(k.Line!=null)
                {
                    while(p.BombLineIndex<8&&Time>=k.Line.At+(p.BombLineIndex+.5f)*2/14+.16f)
                    {float dist=(p.BombLineIndex+++.5f)*2;if(dist<=k.Line.Length)BombBlast(p,NativeBombs.Spark,Vec(k.Line.From+(k.Line.To-k.Line.From).Normalized*dist),1.4f,5,3,forward:Vec((k.Line.To-k.Line.From).Normalized)*7);}
                    if(Time>=k.Line.End)k.Line=null;
                }
                var f=k.Finale;
                if(f!=null)
                {
                    // Finish the dead man's switch even when the owner dies. Circles never follow past Lock.
                    float follow=Math.Max(0,Math.Min(1f/60,f.Lock-(Time-1f/60)));
                    if(follow>0&&!p.BombStruck)foreach(var c in f.Circles){var t=BombTargets(p).FirstOrDefault(t=>t.id==c.Id);if(t.id!=0)c.Pos=Vec(Vector3.MoveTowards(Vec(c.Pos),GroundPoint(t.at),3*follow));}
                    if(!p.BombStruck&&Time>=f.Strike){p.BombStruck=true;var hit=new HashSet<int>();foreach(var c in f.Circles)BombBlast(p,NativeBombs.Column,Vec(c.Pos),4,40,12,ignoreCover:true,once:hit);if(Has(p,NativeBombChips.Encore))BombEncore(p,f);}
                    if(Time>=f.Until)k.Finale=null;
                }
                k.Bombs=p.BombFlights.Select(b=>b.Record).ToArray();k.Blasts=(k.Blasts??Array.Empty<NativeBlast>()).Where(b=>Time-b.At<1.5f).TakeLast(NativeBombs.MaxBlasts).ToArray();
                k.Scorched=p.BombChains.Where(t=>t.Value.Scorched(Time)).Take(NativeBombs.MaxCircles).Select(t=>new NativeBombScorch{Id=t.Key,At=t.Value.ScorchUntil-NativeBombs.ScorchSeconds,Until=t.Value.ScorchUntil}).ToArray();
                foreach(var id in p.BombChains.Where(t=>Time>=t.Value.ScorchUntil&&Time-Math.Max(t.Value.Last,t.Value.LastDamage)>3).Select(t=>t.Key).ToArray())p.BombChains.Remove(id);
                if(Time>=k.ChainUntil)k.Chain=0;
            }
        }
        private void ExplodeBomb(NativePilot p,BombFlight b)
        {
            if(!p.BombFlights.Remove(b))return;var r=b.Record;var at=Vec(r.Pos);
            if(r.Kind==NativeBombs.Lob)
            {
                int count=Has(p,NativeBombChips.ClusterFun)?9:6;for(int i=0;i<count;i++){float angle=i*Mathf.PI*2/count;var dest=GroundPoint(at)+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*3.6f;float life=.28f+i*.35f/(count-1);AddBomb(p,NativeBombs.Bomblet,at,(dest-at)/life+Vector3.up*(23*life/2),Time,life);}BombRecord(p,NativeBombs.Lob,at,1.2f);return;
            }
            float damage=b.DamageOverride>0?b.DamageOverride:r.Kind==NativeBombs.Pop?12:r.Kind==NativeBombs.Mine?6:r.Kind==NativeBombs.Sticky?16:8;
            float radius=r.Kind==NativeBombs.Pop?2.4f:r.Kind==NativeBombs.Mine?2.6f:r.Kind==NativeBombs.Sticky?3:1.9f;
            BombBlast(p,r.Kind,at,radius,damage,r.Kind==NativeBombs.Mine?NativeBombChipRules.MineLift(BombStacks(p,NativeBombChips.BouncingBetty)):5,from:b.Handed?(V3?)b.Handoff:null);
        }
        private NativeBlast BombRecord(NativePilot p,string kind,Vector3 at,float radius)
        {
            var b=new NativeBlast{Id=nextEvent++,Kind=kind,At=Time,Pos=Vec(at),FromPos=Vec(at),Radius=radius};var list=(p.Bomb.Blasts??Array.Empty<NativeBlast>()).ToList();list.Add(b);p.Bomb.Blasts=list.Skip(Math.Max(0,list.Count-NativeBombs.MaxBlasts)).ToArray();return b;
        }
        private void BombLaunch(NativePilot q,NativePilot p,Vector3 horizontal,float lift,float noSteer)
        {
            if(q.Hp<=0||q.Body.isKinematic||SirenArmor(q))return;q.Down=null;q.Grounded=false;q.Body.useGravity=true;
            var v=q.Body.linearVelocity;v.y=Math.Max(v.y,lift);q.Body.linearVelocity=v;q.Knockback=new Vector3(horizontal.x,0,horizontal.z);q.LaunchUntil=Time+.5f;q.BombNoSteerUntil=Time+noSteer;
        }
        private void BombBlast(NativePilot p,string kind,Vector3 at,float radius,float damage,float lift,bool skipOwner=false,bool ignoreCover=false,Vector3 forward=default,V3? from=null,HashSet<int> once=null)
        {
            var blast=BombRecord(p,kind,at,radius);if(from.HasValue){blast.Handed=true;blast.FromPos=from.Value;}
            if(HubInside(at))return;
            if(Has(p,NativeBombChips.ScorchedEarth))BombFire(p,at,radius);
            foreach(var q in Pilots.ToArray())
            {
                if(q.Hp<=0||!q.Seat.connected||q==p&&skipOwner||q!=p&&!CanHit(p,q)||HubInside(q.Body.position))continue;
                var delta=q.Body.position-at;bool inside=kind==NativeBombs.Column?new Vector2(delta.x,delta.z).magnitude<=radius:delta.magnitude<=radius;
                if(!inside||once?.Contains(q.Seat.id)==true||!ignoreCover&&!MeleeVisible(at+Vector3.up*.12f,q.Body.position,p,q.Collider))continue;
                bool self=q==p;NativeBombChain chain=null;float cap=float.PositiveInfinity,scale=self?.25f:1;
                if(!self){chain=BombChain(p,q.Seat.id);cap=chain.Budget((float)q.Stats["hp"]);scale=chain.Scale(Time);}
                bool shield=self&&Has(p,NativeBombChips.BlastShield);
                float actual=shield?0:Damage(q,p,damage*scale*(float)p.Stats["damage"],delta,0,false,direct:false,primary:false,damageKind:"bomb-"+kind,finalCap:cap,selfDamage:self);
                if(actual<=0&&!shield)continue;once?.Add(q.Seat.id);int link=-1;V3 old=chain?.From??default;
                if(!self){link=chain.Hit(Time,actual,Vec(at));p.Bomb.Meter=Math.Min(1,p.Bomb.Meter+(.08f+(link>0?.06f:0))*BombMeterRate(p));if(link>=0){p.Bomb.Chain=link;p.Bomb.ChainUntil=Time+chain.Window;if(link>blast.Link){blast.Link=link;blast.FromPos=old;blast.Handed=true;}}}
                if(chain?.Scorched(Time)==true){q.BombNoSteerUntil=Time;q.Down=null;continue;}
                var push=forward;if(kind==NativeBombs.Sticky){var near=p.BombFlights.OrderBy(b=>Vector3.Distance(Vec(b.Record.Pos),q.Body.position)).FirstOrDefault(b=>Vector3.Distance(Vec(b.Record.Pos),q.Body.position)<=12);if(near!=null){push=Vec(near.Record.Pos)-q.Body.position;push.y=0;push=push.normalized*12;}}
                BombLaunch(q,p,push,kind==NativeBombs.Bomblet&&!q.Grounded?lift*2:lift,kind==NativeBombs.Mine?.5f:0);
            }
            foreach(var m in Minions.ToArray())
            {
                if(m.Hp<=0||!CanHit(p,m)||once?.Contains(m.Id)==true||HubInside(m.Body.position))continue;var d=m.Body.position-at;
                if((kind==NativeBombs.Column?new Vector2(d.x,d.z).magnitude:d.magnitude)>radius||!ignoreCover&&!MeleeVisible(at+Vector3.up*.12f,m.Body.position,p,m.Collider))continue;
                var c=BombChain(p,m.Id);
                float actual=HitMinion(m,p,damage*c.Scale(Time)*(float)p.Stats["damage"],d,0,m.Body.position,"bomb-"+kind);if(actual<=0)continue;once?.Add(m.Id);V3 previous=c.From;int link=c.Hit(Time,actual,Vec(at));if(link>=0){p.Bomb.Chain=link;p.Bomb.ChainUntil=Time+c.Window;if(link>blast.Link){blast.Link=link;blast.FromPos=previous;blast.Handed=true;}}p.Bomb.Meter=Math.Min(1,p.Bomb.Meter+(.08f+(link>0?.06f:0))*BombMeterRate(p));
                if(c.Scorched(Time)){m.BombPushUntil=Time;m.BombPush=Vector3.zero;}
                if(!c.Scorched(Time)&&m.Hp>0&&!m.Body.isKinematic&&!IsZoneBoss(m)){var v=m.Body.linearVelocity;v.y=Math.Max(v.y,kind==NativeBombs.Bomblet&&m.Body.position.y-GroundPoint(m.Body.position).y>m.Collider.height*.5f+.15f?lift*2:lift);m.Body.linearVelocity=v;m.BombPush=forward;if(kind==NativeBombs.Sticky){var near=p.BombFlights.OrderBy(b=>Vector3.Distance(Vec(b.Record.Pos),m.Body.position)).FirstOrDefault(b=>Vector3.Distance(Vec(b.Record.Pos),m.Body.position)<=12);if(near!=null){var push=Vec(near.Record.Pos)-m.Body.position;push.y=0;m.BombPush=push.normalized*12;}}m.BombPushUntil=Time+.5f;SlowMinion(m,kind==NativeBombs.Mine?.5f:.15f,.1f);}
            }
            BombTouchMines(p,at,radius);
        }
        private void BombEncore(NativePilot p,NativeBombFinale finale)
        {
            // Eight visible falling charges, spread around locked circles (or the crate with no targets).
            for(int i=0;i<8;i++)
            {
                var center=finale.Circles.Length>0?Vec(finale.Circles[i%finale.Circles.Length].Pos):Vec(finale.Box);
                float angle=i*Mathf.PI/4,life=.1f*(i+1);var dest=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*2;
                var origin=dest+Vector3.up*4;
                if(p.BombFlights.Count>=NativeBombs.MaxBombs)p.BombFlights.RemoveAt(0);
                var b=AddBomb(p,NativeBombs.Bomblet,origin,(dest-origin)/life+Vector3.up*(23*life/2),Time,life);
                b.DamageOverride=10;b.Encore=true;
            }
        }
        private void BombFire(NativePilot p,Vector3 at,float radius)
        {
            var floor=GroundPoint(at);if(HubInside(floor))return;
            var fields=ChipFields.Where(f=>f.Owner==p&&f.Effect==NativeBombChips.ScorchedEarth).ToArray();
            if(fields.Length>=NativeBombChipRules.MaxFireFields)ChipFields.Remove(fields[0]);
            Field(p,"fire",floor,Math.Min(3,radius),2,6,NativeBombChips.ScorchedEarth);
        }
        private void BombFireTick(NativeChipField f,HashSet<(int owner,int victim)> hit)
        {
            var owner=f.Owner;if(owner==null||HubInside(f.Position))return;
            bool Inside(Vector3 point)
            {var d=point-f.Position;return Math.Abs(d.y)<=1.6f&&new Vector2(d.x,d.z).magnitude<=f.Radius&&!HubInside(point);}
            foreach(var q in NativeSnapshots.Capture(Pilots))
            {
                if(q.Hp<=0||q==owner||!q.Seat.connected||!CanHit(owner,q)||!Inside(q.Body.position)||!MeleeVisible(f.Position+Vector3.up*.2f,q.Body.position,owner,q.Collider)||!hit.Add((owner.Seat.id,q.Seat.id)))continue;
                var chain=BombChain(owner,q.Seat.id);float actual=Damage(q,owner,f.Damage/60*(float)owner.Stats["damage"]*(chain.Scorched(Time)?.5f:1),Vector3.zero,0,false,direct:false,primary:false,damageKind:NativeBombChips.ScorchedEarth,finalCap:chain.Budget((float)q.Stats["hp"]));
                if(actual>0){chain.Spent+=actual;chain.LastDamage=Time;}
            }
            foreach(var m in NativeSnapshots.Capture(Minions))
            {
                if(m.Hp<=0||!CanHit(owner,m)||!Inside(m.Body.position)||!MeleeVisible(f.Position+Vector3.up*.2f,m.Body.position,owner,m.Collider)||!hit.Add((owner.Seat.id,m.Id)))continue;
                var chain=BombChain(owner,m.Id);HitMinion(m,owner,f.Damage/60*(float)owner.Stats["damage"]*(chain.Scorched(Time)?.5f:1),Vector3.zero,0,kind:NativeBombChips.ScorchedEarth);
            }
        }
        private void StepBombMinionMotion()
        {foreach(var m in Minions)if(m.Hp>0&&!m.Body.isKinematic&&Time<m.BombPushUntil){var v=m.Body.linearVelocity;v.x=m.BombPush.x;v.z=m.BombPush.z;m.Body.linearVelocity=v;m.BombPush*=Mathf.Exp(-3.1f/60);}}
        private void BombTouchMines(NativePilot p,Vector3 at,float radius)
        {
            foreach(var owner in Pilots)foreach(var b in owner.BombFlights)if(b.Record.Kind==NativeBombs.Mine&&Time>=b.Record.ArmAt&&Vector3.Distance(at,Vec(b.Record.Pos))<=radius&&MeleeVisible(at+Vector3.up*.1f,Vec(b.Record.Pos)+Vector3.up*.1f,p,null))
            {if(b.Record.BlowAt<-50||b.Record.BlowAt>Time+.12f){b.Record.BlowAt=Time+.12f;b.Handoff=Vec(at);b.Handed=true;}}
        }
    }
}
