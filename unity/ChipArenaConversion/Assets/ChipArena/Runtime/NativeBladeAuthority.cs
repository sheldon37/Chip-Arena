using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The blade classes' shared rules (owner 2026-11-10: "Make very high quality designed katana / dev dagger classes"; Claude builds the rules at
    // the owner's word, as for the Glaive and the Top Dog, logged for Astra; docs/KATANA.md, docs/FANGS.md). The host steps them at 60 Hz and
    // writes NativeBladeKit, the only thing guests, late joiners and the killcam see. Each class's moves are in its own file
    // (NativeKatanaAuthority.cs, NativeFangsAuthority.cs); here: the input gate, the release, a move's travel, the paired holds (an attacker's
    // takedown with its victim held in the matching clip), the hits, the venom stacks, the veil and the hooks the arena calls. The held guard,
    // the parry and the roll are any melee weapon's.
    internal sealed class BladeKnife{public Vector3 Pos,Dir;public float Travel;public int Pierce;public readonly HashSet<int> Struck=new HashSet<int>();}
    internal sealed class BladeCloud{public Vector3 At;public float Until;}
    internal sealed class BladeWave{public Vector3 From,Dir;public float At;public readonly HashSet<int> Struck=new HashSet<int>();}
    public sealed partial class NativePilot
    {
        public NativeBladeKit Blade=new NativeBladeKit{Ready=new float[NativeBlades.MaxSlots]};
        internal readonly List<NativeBladeHit> BladeHits=new List<NativeBladeHit>();
        // Who this move has cut (once each), the iai's marked, and the move's aim and facing at its cast.
        internal readonly HashSet<int> BladeStruck=new HashSet<int>(),BladeMarked=new HashSet<int>();
        internal bool BladeReleased,BladeAttackHeld;internal Vector3 BladeFacing,BladeAim;internal float BladeLastAttack=-100;
        // The Katana's storm (the next cut and its count), its counter's foe, Twin Moons' second split; the Fangs' veil broken once (Silent Veil).
        internal int BladeStormCut;internal float BladeStormNext=-100,BladeFirstSplit=-100,BladeSecondSplit=-100;internal NativePilot BladeCounterFoe;internal NativeMinion BladeCounterMinion;internal bool BladeVeilKept;
        // A paired move's victim (a pilot or a minion), where it is held and which way it faces; on the victim, who holds it.
        internal NativePilot BladePairVictim,BladeHeldBy;internal NativeMinion BladePairMinion;internal Vector3 BladePairAt;internal float BladePairYaw;internal bool BladePairStruck,BladePairLethal;
        // Venom on others (stacks and until when, by seat or minion id), knives in flight, smoke clouds, crescent waves, bleeding and the
        // launched (the chips'), the shadow step's charges.
        internal readonly Dictionary<int,int> BladeVenom=new Dictionary<int,int>();internal readonly Dictionary<int,float> BladeVenomUntil=new Dictionary<int,float>(),BladeVenomShow=new Dictionary<int,float>();
        internal readonly List<BladeKnife> BladeKnives=new List<BladeKnife>();internal readonly List<BladeCloud> BladeClouds=new List<BladeCloud>();internal readonly List<BladeWave> BladeWaves=new List<BladeWave>();
        internal readonly Dictionary<int,float> BladeBleedUntil=new Dictionary<int,float>(),BladeBleedShow=new Dictionary<int,float>(),BladeLaunched=new Dictionary<int,float>();
        internal int BladeCharges=1;internal float BladeChargeAt=-100;
    }
    public sealed partial class NativeArena
    {
        private static bool BladeSpec(NativePilot p)=>p!=null&&NativeBlades.Is(p.Seat.spec);
        private bool FangsAllowed(NativePilot p)=>p.Seat.spec==NativeFangs.Spec&&(p.Seat.access.Allows("weapon:"+NativeFangs.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())||p.Seat.devWeaponAccess?.DeveloperWeapon(NativeFangs.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())==true);
        private bool BladeBusy(NativePilot p)=>BladeSpec(p)&&Time<p.Blade.CastUntil;
        /// <summary>A pilot held in a blade class's paired move (the victim side).</summary>
        private bool BladeHeld(NativePilot q)=>q.BladeHeldBy!=null;
        /// <summary>The custom melee kits (the Glaive, the Katana, the Fangs) take the standard plunging slam (owner 2026-11-11: "Glaive,
        /// fangs, and katana need Slam attacks from the air"): in the air looking down an attack is the standard swing's plunge (Core
        /// NativeAirAttackSimulation: the drop, the landing ring, the stuck recovery), not the kit's own move. Not while a kit move runs, nor
        /// with the Glaive's ring out of the hand (its click is the burst then).</summary>
        private bool KitSlam(NativePilot p)=>p!=null&&NativeAirAttackSimulation.KitSlam(p.Seat.spec)&&!p.Grounded&&NativeAirAttackSimulation.DownPitch(p.Input.Pitch)&&!NativeHazards.InLava(MapId,Vec(p.Body.position))
            &&!BladeBusy(p)&&!GlaiveBusy(p)&&(p.Seat.spec!=NativeGlaive.Spec||p.Glaive?.Disc==NativeGlaive.Hand);
        private float BladeScale(NativePilot p)=>NativeAbilityRules.Scale(p.Stats,false);
        private IEnumerable<NativePilot> BladePilots(NativePilot p){foreach(var q in Pilots.ToArray())if(q!=p&&q.Hp>0&&q.Seat.connected&&CanHit(p,q)&&!HubInside(q.Body.position))yield return q;}
        private IEnumerable<NativeMinion> BladeMinions(NativePilot p){foreach(var m in Minions.ToArray())if(m.Hp>0&&CanHit(p,m)&&!HubInside(m.Body.position))yield return m;}
        /// <summary>A minion's id in the shared id sets (pilots' seats are small; minions are offset).</summary>
        private static int BladeKey(NativeMinion m)=>1000000+m.Id;
        private int BladeStacks(NativePilot p,string id,int cap=1)=>BladeSpec(p)&&Has(p,id)&&p.Seat.chips.TryGetValue(id,out int n)?Math.Min(cap,Math.Max(0,n)):0;
        private bool BladeChip(NativePilot p,string id)=>BladeStacks(p,id)>0;
        private NativeBladeHit BladeHit(NativePilot p,string kind,Vector3 at,Vector3 dir,int victim=0,int amount=0)
        {
            if(p.BladeHits.Count>=NativeBlades.MaxHits)p.BladeHits.RemoveAt(0);
            var h=new NativeBladeHit{Id=nextEvent++,Kind=kind,At=Time,Pos=Vec(at),Dir=Vec(dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.up),Victim=victim,Amount=Math.Max(0,Math.Min(99,amount))};p.BladeHits.Add(h);return h;
        }
        /// <summary>A blade class's blow on a pilot: its own damage share (melee: the weapon's; else the ability scale), the chips' share, the
        /// damage kind "blade-"+kind.</summary>
        private float BladeHurt(NativePilot p,NativePilot q,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
        {
            float share=p.Seat.spec==NativeKatana.Spec?KatanaShare(p,q.Seat.id,melee):FangsShare(p,q.Hp/Math.Max(1,(float)q.Stats["hp"]));
            float actual=Damage(q,p,amount*share*(melee?(float)p.Stats["damage"]:BladeScale(p)),dir,impulse,false,primary:melee,melee:melee,weaponHit:melee,damageKind:"blade-"+kind,hitPoint:q.Body.position+Vector3.up*.3f);
            if(actual>0)BladeLanded(p,q.Seat.id,q.Hp<=0);return actual;
        }
        private float BladeHurt(NativePilot p,NativeMinion m,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
        {
            float share=p.Seat.spec==NativeKatana.Spec?KatanaShare(p,BladeKey(m),melee):FangsShare(p,m.Hp/Math.Max(1,m.MaxHp));
            float actual=HitMinion(m,p,amount*share*(melee?(float)p.Stats["damage"]:BladeScale(p)),dir,impulse,m.Body.position,"blade-"+kind);
            if(actual>0)BladeLanded(p,BladeKey(m),m.Hp<=0);return actual;
        }
        private void BladePush(NativeMinion m,Vector3 horizontal,float lift)
        {
            if(m.Hp<=0||m.Body.isKinematic||IsZoneBoss(m))return;var v=m.Body.linearVelocity;v.y=Math.Max(v.y,lift);m.Body.linearVelocity=v;m.BombPush=new Vector3(horizontal.x,0,horizontal.z);m.BombPushUntil=Time+.4f;
        }
        /// <summary>Drags `q` toward `to` by `distance` (never closer than `close`): a pull on its knockback.</summary>
        private void BladeDrag(NativePilot q,Vector3 to,float distance,float close)
        {
            if(q.Hp<=0||q.Body.isKinematic||SirenArmor(q)||Pve?.Boss==q)return;var d=to-q.Body.position;d.y=0;float l=d.magnitude;float pull=Math.Min(distance,Math.Max(0,l-close));if(pull<.05f)return;q.Knockback=d/l*(pull*3.1f);
        }
        private void BladeDrag(NativeMinion m,Vector3 to,float distance,float close)
        {
            if(m.Hp<=0||m.Body.isKinematic||IsZoneBoss(m))return;var d=to-m.Body.position;d.y=0;float l=d.magnitude;float pull=Math.Min(distance,Math.Max(0,l-close));if(pull<.05f)return;BladePush(m,d/l*(pull/.235f),.5f);
        }
        private static float BladeSegment(Vector3 a,Vector3 b,Vector3 q){var ab=b-a;float l=ab.sqrMagnitude;float t=l>1e-8f?Mathf.Clamp01(Vector3.Dot(q-a,ab)/l):0;return Vector3.Distance(a+ab*t,q);}
        private bool BladeWall(Collider c)=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger;
        /// <summary>A cone ahead (or all round when arc covers it): everyone within reach and arc of the facing, in sight.</summary>
        private void BladeCone(NativePilot p,Vector3 at,Vector3 facing,float reach,float arc,Action<NativePilot,Vector3> pilot,Action<NativeMinion,Vector3> minion)
        {
            bool Inside(Vector3 q){var d=q-at;if(Math.Abs(d.y)>1.9f)return false;var flat=new Vector3(d.x,0,d.z);float l=flat.magnitude;if(l>reach+.45f)return false;if(l<.6f||arc>=3.1f)return true;return Vector3.Angle(facing,flat)*Mathf.Deg2Rad<=arc;}
            foreach(var q in BladePilots(p))if(Inside(q.Body.position)&&MeleeVisible(at+Vector3.up*.3f,q.Body.position,p,q.Collider))pilot(q,q.Body.position-at);
            foreach(var m in BladeMinions(p))if(Inside(m.Body.position)&&MeleeVisible(at+Vector3.up*.3f,m.Body.position,p,m.Collider))minion(m,m.Body.position-at);
        }

        // ---- the arena's hooks ----
        /// <summary>From StepPilot: the input (each class's).</summary>
        private void StepBladeInput(NativePilot p)
        {
            if(!BladeSpec(p))return;var k=p.Blade;int slots=NativeBlades.Slots(p.Seat.spec);if(k.Ready==null||k.Ready.Length!=slots)k.Ready=new float[slots];
            bool attack=p.Input.Shoot||p.Input.Attack,press=attack&&!p.BladeAttackHeld;p.BladeAttackHeld=attack;
            if(p.Hp<=0||Incapacitated(p)||HubInside(p.Body.position)||BladeHeld(p))return;
            // The plunging slam (the standard swing, KitSlam): none of the kit's moves while it lasts; its attack is the slam's.
            if(p.Swing!=null&&Time<p.AttackEnd)return;if(KitSlam(p)){attack=press=false;}
            // The Fangs are a DEV class: their kit answers only a pilot allowed it (a grant or developer access).
            if(p.Seat.spec==NativeFangs.Spec&&!FangsAllowed(p))return;
            if(p.Seat.spec==NativeKatana.Spec)KatanaInput(p,attack,press);else FangsInput(p,attack,press);
        }
        /// <summary>A move begins: the record's times, the facing and aim, the lockout, the cast event.</summary>
        private void BladeBegin(NativePilot p,string kind,float release,float until,int step=0)
        {
            var k=p.Blade;p.BladeReleased=false;k.CastKind=kind;k.CastAt=Time;k.CastStep=step;k.CastRelease=Time+release;k.CastUntil=Time+until;
            p.BladeAim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)).normalized;p.BladeFacing=Vec(V3.Forward(p.Input.Yaw));p.Yaw=p.Input.Yaw;p.AttackAt=p.LastAttack=p.BladeLastAttack=Time;
            p.Ready=k.CastUntil-(kind==NativeKatana.Cut||kind==NativeFangs.Cut?.04f:0);p.BladeStruck.Clear();
            Emit(EventKind.AbilityCast,p,0,p.Body.position,0,kind);
        }
        private void StepBladeWorld()
        {
            foreach(var p in Pilots)
            {
                if(!BladeSpec(p))continue;var k=p.Blade;int slots=NativeBlades.Slots(p.Seat.spec);if(k.Ready==null||k.Ready.Length!=slots)k.Ready=new float[slots];
                bool able=p.Hp>0&&p.Seat.connected&&!Incapacitated(p)&&!BladeHeld(p);
                // A move cut short (stunned, knocked down, killed) ends; so does its travel and any pair.
                if(k.CastKind!=null&&Time<k.CastUntil&&!able){k.CastUntil=Time;k.PathUntil=Math.Min(k.PathUntil,Time);if(p.BladePairVictim!=null||p.BladePairMinion!=null)BladeEndPair(p);}
                if(!p.BladeReleased&&k.CastKind!=null&&Time>=k.CastRelease){p.BladeReleased=true;if(able&&Time<=k.CastUntil+.001f){if(p.Seat.spec==NativeKatana.Spec)KatanaRelease(p);else FangsRelease(p);}}
                if(able)BladeTravel(p);
                BladePairStep(p);
                if(p.Seat.spec==NativeKatana.Spec)KatanaWorld(p,able);else FangsWorld(p,able);
                p.BladeHits.RemoveAll(h=>Time-h.At>1.5f);k.Hits=p.BladeHits.ToArray();
            }
        }
        /// <summary>A move's travel (the rush, the iai's dash, the pounce): the body along From to To, the way clear of walls, nothing else
        /// moving it.</summary>
        private void BladeTravel(NativePilot p)
        {
            var k=p.Blade;if(!NativeBlades.PathAt(k,Time,out var at,out float u))return;var to=new Vector3(at.X,at.Y,at.Z);
            p.Body.position=KernelTravel(p.Body.position,to);p.MoveVelocity=Vector3.zero;if(!p.Body.isKinematic)p.Body.linearVelocity=new Vector3(0,Math.Min(0,p.Body.linearVelocity.y),0);
        }
        /// <summary>Sets a move's travel from where the pilot is, `distance` m along `dir` (flat; a wall stops it short), between `at` and `until`.</summary>
        private void BladePath(NativePilot p,Vector3 dir,float distance,float at,float until)
        {
            var k=p.Blade;dir.y=0;dir=dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.forward;var from=p.Body.position;
            var hit=SweepNative(from+Vector3.up*.2f,dir*distance,.35f,BladeWall);if(hit.HasValue)distance=Math.Max(0,hit.Value.Distance-.45f);
            k.From=Vec(from);k.To=Vec(from+dir*distance);k.PathAt=at;k.PathUntil=until;
        }
        /// <summary>From the movement step: the blade classes' moves slow or stop the stride.</summary>
        private void BladeMotion(NativePilot p,ref Vector3 v)
        {
            if(!BladeSpec(p))return;var k=p.Blade;
            if(k.PathAt>-50&&Time>=k.PathAt-Rules.Dt&&Time<=k.PathUntil+Rules.Dt||p.BladePairVictim!=null||p.BladePairMinion!=null){v.x=v.z=0;p.MoveVelocity=Vector3.zero;return;}
            if(Time>=k.CastUntil||k.CastKind==null)return;float s=p.Seat.spec==NativeKatana.Spec?KatanaMove(k):FangsMove(k);v.x*=s;v.z*=s;
        }
        /// <summary>From the movement speed: the chips' share (Predator).</summary>
        private float BladeSpeed(NativePilot p)=>p.Seat.spec==NativeFangs.Spec&&BladeChip(p,NativeFangsChips.Predator)?NativeFangsChips.PreySpeed:1;
        /// <summary>From Damage: the share of a blow `source` deals to `target` (Smokescreen: a foe in a Fangs' cloud deals less).</summary>
        private float BladeDealt(NativePilot source,NativePilot target)
        {
            if(source==null)return 1;foreach(var p in Pilots){if(p.BladeClouds.Count==0||p==source||!Enemy(p,source))continue;foreach(var c in p.BladeClouds)if(Time<c.Until&&Vector3.Distance(c.At,source.Body.position)<=NativeFangsChips.CloudRadius+.4f)return NativeFangsChips.CloudShare;}
            return 1;
        }
        /// <summary>Whether `q` is unseen by enemies (the Fangs' veil): bots and the lock-on pass over it.</summary>
        private bool BladeUnseen(NativePilot q)=>q!=null&&q.Seat.spec==NativeFangs.Spec&&Time>=q.Blade.VeilAt&&Time<q.Blade.VeilUntil;
        /// <summary>From BlockProc: a timed parry landed (the Katana's counter opens; a blow held on the raised guard does not open it).</summary>
        private void BladeBlocked(NativePilot p,NativePilot attacker)
        {
            if(p.Seat.spec!=NativeKatana.Spec||p.Hp<=0)return;float window=NativeKatana.CounterWindow*(BladeChip(p,NativeKatanaChips.MirrorMoon)?NativeKatanaChips.MirrorWindow:1);
            p.Blade.CounterUntil=Time+window;p.BladeCounterFoe=attacker;p.BladeCounterMinion=null;
        }
        private void BotBlade(NativePilot p,ref InputFrame i,Vector3 target,float distance,bool visible)
        {
            if(!BladeSpec(p))return;var k=p.Blade;int slots=NativeBlades.Slots(p.Seat.spec);if(k.Ready==null||k.Ready.Length!=slots)k.Ready=new float[slots];
            i.Shoot=i.Attack=visible&&distance<(p.Seat.spec==NativeKatana.Spec?3f:2.4f);if(BladeBusy(p))return;bool Ready(int s)=>s<k.Ready.Length&&Time>=k.Ready[s];
            if(p.Seat.spec==NativeKatana.Spec)
            {
                if(visible&&distance>3.5f&&distance<7&&Ready(0))i.AbilityQ=true;else if(visible&&distance<2.8f&&Ready(1))i.AbilityE=true;
            }
            else
            {
                if(visible&&distance<2.2f&&Ready(4))i.SirenAbility=3;else if(visible&&distance>4&&distance<11&&Ready(0))i.AbilityQ=true;
                else if(visible&&distance>5&&distance<16&&Ready(2))i.SirenAbility=1;else if(p.BladeVenom.Count>0&&Ready(3))i.SirenAbility=2;else if(visible&&distance<6&&p.Hp<55&&Ready(1))i.AbilityE=true;
            }
            if(i.AbilityQ||i.AbilityE||i.SirenAbility>0)i.Shoot=i.Attack=false;
        }
        /// <summary>From the snapshot: the record, and the victim's side of a paired move (which clip its body plays, and when).</summary>
        private void BladeSnapshot(NativePilot p,Actor a)
        {
            a.Blade=BladeSpec(p)?p.Blade.ViewCopy():null;if(p.Seat.spec==NativeKatana.Spec)a.VisualSet=0;else if(p.Seat.spec==NativeFangs.Spec)a.VisualSet=1;
            var h=p.BladeHeldBy;var hk=h?.Blade;bool held=h!=null&&hk!=null&&h.BladePairVictim==p&&p.Hp>0;
            a.PairBy=held?h.Seat.id:0;a.PairClip=held?NativeDevMotions.VictimClip(hk.PairClip,h.Seat.motion):null;a.PairAt=held?hk.PairAt:-100;a.PairUntil=held?hk.PairUntil:-100;
        }

        // ---- the paired moves (the Katana's counter, the Fangs' takedowns) ----
        /// <summary>Starts a paired move on a pilot or a minion: both held from now to `hold` s, the blow at `hit` s (lethal: it kills), the victim
        /// `gap` m in front of the wielder, facing it (or turned away for a takedown from behind).</summary>
        private void BladeStartPair(NativePilot p,NativePilot q,NativeMinion m,string clip,float hit,float hold,float gap,bool facing,bool lethal)
        {
            var k=p.Blade;k.Pair=q!=null?q.Seat.id:m!=null?BladeKey(m):0;k.PairClip=clip;k.PairAt=Time;k.PairHit=Time+hit;k.PairUntil=Time+hold;
            p.BladePairVictim=q;p.BladePairMinion=m;p.BladePairStruck=false;p.BladePairLethal=lethal;
            var f=p.BladeFacing;f.y=0;f=f.sqrMagnitude>1e-6f?f.normalized:Vector3.forward;p.BladePairAt=p.Body.position+f*gap;p.BladePairYaw=Mathf.Atan2(facing?-f.x:f.x,facing?-f.z:f.z)*Mathf.Rad2Deg;
            if(q!=null){q.BladeHeldBy=p;CancelMelee(q);CancelSpecial(q);CancelAbilities(q);CancelFighter(q);q.Roll=null;q.RollEnd=Time;}
        }
        private void BladePairStep(NativePilot p)
        {
            var k=p.Blade;if(p.BladePairVictim==null&&p.BladePairMinion==null)return;
            if(p.Hp<=0||!p.Seat.connected||Time>=k.PairUntil){BladeEndPair(p);return;}
            // The blow, once: lethal for a takedown (a boss takes a share), else the counter's damage.
            if(!p.BladePairStruck&&Time>=k.PairHit)
            {
                p.BladePairStruck=true;var q=p.BladePairVictim;var m=p.BladePairMinion;
                if(p.Seat.spec==NativeKatana.Spec)KatanaCounterBlow(p,q,m);else FangsTakedownBlow(p,q,m);
            }
            if(p.BladePairMinion!=null){var m=p.BladePairMinion;if(m.Hp>0){m.Body.position=new Vector3(p.BladePairAt.x,m.Body.position.y,p.BladePairAt.z);m.Body.linearVelocity=Vector3.zero;m.NextAttack=Math.Max(m.NextAttack,Time+.4f);m.StrikeAt=-1;}}
        }
        private void BladeEndPair(NativePilot p)
        {
            var q=p.BladePairVictim;if(q!=null&&q.BladeHeldBy==p){q.BladeHeldBy=null;q.Body.useGravity=true;}
            p.BladePairVictim=null;p.BladePairMinion=null;p.Blade.Pair=0;p.Blade.PairUntil=Math.Min(p.Blade.PairUntil,Time);
        }
        /// <summary>From StepPilot: a pilot held in a paired move stays where the move holds it, facing as it says, and does nothing else.</summary>
        private bool PinBladeVictim(NativePilot q)
        {
            var p=q.BladeHeldBy;if(p==null)return false;
            if(p.BladePairVictim!=q||p.Hp<=0||Time>=p.Blade.PairUntil||q.Hp<=0){q.BladeHeldBy=null;q.Body.useGravity=true;return false;}
            // Thrown back as its clip carries it after the blow (Core NativeBladeMotion.VictimTravel; walls stop it), from where the hold put it.
            var at=p.BladePairAt;float back=NativeBladeMotion.VictimTravel(p.Blade.PairClip,Time,p.Blade.PairAt,p.Blade.PairUntil);
            if(back>0){var away=at-p.Body.position;away.y=0;if(away.sqrMagnitude>1e-6f)at+=away.normalized*back;}
            q.Body.position=KernelTravel(q.Body.position,new Vector3(at.x,q.Body.position.y,at.z));q.Body.linearVelocity=q.Knockback=q.MoveVelocity=Vector3.zero;
            q.Yaw=p.BladePairYaw;q.StunEnd=Math.Max(q.StunEnd,Time+.05f);return true;
        }

        // ---- venom (the Fangs'; on pilots the arena's poison by stacks, on minions their own ticks) ----
        private int VenomMax(NativePilot p)=>BladeChip(p,NativeFangsChips.TwinVenom)?NativeFangsChips.TwinMax:NativeFangs.VenomMax;
        private float VenomDps(NativePilot p,int stacks)=>stacks*NativeFangs.VenomDps*(1+NativeFangsChips.NeuroDamage*BladeStacks(p,NativeFangsChips.Neurotoxin,NativeFangsChips.NeuroMax));
        /// <summary>Adds `add` stacks of venom (to its most), refreshing every stack's clock; the poison's rate follows the stacks.</summary>
        private int AddVenom(NativePilot p,NativePilot q,int add)
        {
            if(add<=0||q.Hp<=0)return 0;int id=q.Seat.id;p.BladeVenom.TryGetValue(id,out int n);bool live=p.BladeVenomUntil.TryGetValue(id,out float until)&&until>Time;if(!live)n=0;
            n=Math.Min(VenomMax(p),n+add);p.BladeVenom[id]=n;p.BladeVenomUntil[id]=Time+NativeFangs.VenomFor;
            string key=p.Seat.id+":venom";q.Dots.TryGetValue(key,out var old);bool on=old!=null&&old.Until>=Match.Time;
            q.Dots[key]=new NativeDot{Source=p.Seat.id,Dps=VenomDps(p,n),Next=on?old.Next:Match.Time+.25,Until=Match.Time+NativeFangs.VenomFor};return n;
        }
        private int AddVenom(NativePilot p,NativeMinion m,int add)
        {
            if(add<=0||m.Hp<=0||HubDamageBlocked(p,m.Body.position))return 0;int id=BladeKey(m);p.BladeVenom.TryGetValue(id,out int n);bool live=p.BladeVenomUntil.TryGetValue(id,out float until)&&until>Time;if(!live)n=0;
            n=Math.Min(VenomMax(p),n+add);p.BladeVenom[id]=n;p.BladeVenomUntil[id]=Time+NativeFangs.VenomFor;
            string key=p.Seat.id+":venom";m.Dots.TryGetValue(key,out var old);bool on=old!=null&&old.Until>=Time;
            m.Dots[key]=new NativeDot{HubNoReward=HubInside(p.Body.position),Source=p.Seat.id,Dps=VenomDps(p,n),Next=on?old.Next:Time+.25,Until=Time+NativeFangs.VenomFor};return n;
        }
        /// <summary>Spends a victim's stacks (the bloom), returning how many there were.</summary>
        private int SpendVenom(NativePilot p,int id)
        {
            if(!p.BladeVenom.TryGetValue(id,out int n)||!p.BladeVenomUntil.TryGetValue(id,out float until)||until<=Time)return 0;p.BladeVenom.Remove(id);p.BladeVenomUntil.Remove(id);p.BladeVenomShow.Remove(id);
            string key=p.Seat.id+":venom";var q=Pilots.Find(x=>x.Seat.id==id);if(q!=null)q.Dots.Remove(key);else{var m=Minions.Find(x=>BladeKey(x)==id);m?.Dots.Remove(key);}return n;
        }
        /// <summary>Venom's clocks: lapsed stacks dropped, a drip for the effects now and then, Neurotoxin's slow by stacks, Bloodletting's drink.</summary>
        private void VenomStep(NativePilot p)
        {
            if(p.BladeVenom.Count==0)return;int neuro=BladeStacks(p,NativeFangsChips.Neurotoxin,NativeFangsChips.NeuroMax);
            foreach(var id in p.BladeVenom.Keys.ToArray())
            {
                var q=Pilots.Find(x=>x.Seat.id==id);var m=q==null?Minions.Find(x=>BladeKey(x)==id):null;bool alive=q!=null?q.Hp>0:m!=null&&m.Hp>0;
                if(!alive||!p.BladeVenomUntil.TryGetValue(id,out float until)||Time>=until){p.BladeVenom.Remove(id);p.BladeVenomUntil.Remove(id);p.BladeVenomShow.Remove(id);continue;}
                int n=p.BladeVenom[id];if(neuro>0){float slow=1-Math.Min(NativeFangsChips.NeuroSlowMost,NativeFangsChips.NeuroSlow*n);if(q!=null)Slow(q,slow,.3f);else SlowMinion(m,slow,.3f);}
                if(p.BladeVenomShow.TryGetValue(id,out float next)&&Time<next)continue;p.BladeVenomShow[id]=Time+.45f;
                var at=q!=null?q.Body.position+Vector3.up*.35f:m.Body.position+Vector3.up*.2f;BladeHit(p,NativeFangs.HitDrip,at,Vector3.down,id,n);
                if(BladeChip(p,NativeFangsChips.Bloodletting)){float drink=VenomDps(p,n)*.45f*NativeFangsChips.DrainShare;if(Heal(p,drink,"bloodletting")>0)BladeHit(p,NativeFangs.HitDrain,at,p.Body.position-at,id,n);}
            }
        }
        /// <summary>A Katana's chips' bleed (Honed Edge): the arena's damage over time, refreshed by the next cut.</summary>
        private void KatanaBleed(NativePilot p,NativePilot q)
        {
            if(BladeStacks(p,NativeKatanaChips.HonedEdge,NativeKatanaChips.HonedMax)<=0||q.Hp<=0)return;RefreshDot(q,p,"bleed",NativeKatanaChips.BleedDps,NativeKatanaChips.BleedFor);
            p.BladeBleedUntil[q.Seat.id]=Time+NativeKatanaChips.BleedFor;if(!p.BladeBleedShow.ContainsKey(q.Seat.id))p.BladeBleedShow[q.Seat.id]=Time+NativeKatanaChips.BleedShow;
        }
        private void KatanaBleed(NativePilot p,NativeMinion m)
        {
            if(BladeStacks(p,NativeKatanaChips.HonedEdge,NativeKatanaChips.HonedMax)<=0||m.Hp<=0||HubDamageBlocked(p,m.Body.position))return;
            string key=p.Seat.id+":bleed";m.Dots.TryGetValue(key,out var old);bool live=old!=null&&old.Until>=Time;
            m.Dots[key]=new NativeDot{HubNoReward=HubInside(p.Body.position),Source=p.Seat.id,Dps=NativeKatanaChips.BleedDps,Next=live?old.Next:Time+.25,Until=Time+NativeKatanaChips.BleedFor};
            int id=BladeKey(m);p.BladeBleedUntil[id]=Time+NativeKatanaChips.BleedFor;if(!p.BladeBleedShow.ContainsKey(id))p.BladeBleedShow[id]=Time+NativeKatanaChips.BleedShow;
        }
        private void BleedStep(NativePilot p)
        {
            if(p.BladeBleedUntil.Count==0)return;
            foreach(var id in p.BladeBleedUntil.Keys.ToArray())
            {
                var q=Pilots.Find(x=>x.Seat.id==id);var m=q==null?Minions.Find(x=>BladeKey(x)==id):null;bool alive=q!=null?q.Hp>0:m!=null&&m.Hp>0;
                if(!alive||Time>=p.BladeBleedUntil[id]){p.BladeBleedUntil.Remove(id);p.BladeBleedShow.Remove(id);continue;}
                if(p.BladeBleedShow.TryGetValue(id,out float next)&&Time<next)continue;p.BladeBleedShow[id]=Time+NativeKatanaChips.BleedShow;
                BladeHit(p,NativeKatana.HitBleed,q!=null?q.Body.position+Vector3.up*.35f:m.Body.position+Vector3.up*.2f,Vector3.down,id);
            }
        }
        /// <summary>After a blade class's blow landed on `id` (killed or not): each class's follow-up (Severing Line's refund, Death Mark's reset).</summary>
        private void BladeLanded(NativePilot p,int id,bool killed)
        {
            if(p.Seat.spec==NativeKatana.Spec)KatanaLanded(p,id,killed);else FangsLanded(p,id,killed);
        }
    }
}
