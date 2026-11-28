using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Engine-independent authority. Flat-arena kinematics are intentional; only debris uses Unity PhysX.
    public sealed class ArenaWorld
    {
        public readonly List<Actor> Actors = new List<Actor>();
        public readonly List<Cover> Covers = new List<Cover>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<ArenaEvent> Events = new List<ArenaEvent>();
        public int Tick { get; private set; }
        public float Time { get { return Tick * Rules.Dt; } }
        public bool BotAttacks = true, StationaryBots, RangedBots;
        private uint randomState=0xCAFE4321;
        private int nextShot = 1, nextEvent = 1;

        public ArenaWorld()
        {
            AddHuman(1);
            for (int i = 0; i < 8; i++) Covers.Add(new Cover { Id = i + 1,
                Position = new V3((i % 4 - 1.5f) * 9, 0, i < 4 ? -5 : 5), Height = i % 2 == 0 ? 1.3f : 2.4f });
        }
        public Actor AddHuman(int id)
        {
            Actor existing = Find(id); if (existing != null) return existing;
            if (id != 1 && id != 2) throw new ArgumentOutOfRangeException("id");
            Actor a = new Actor { Id = id, Team = id, Position = new V3(id == 1 ? -3 : 3, 0, -13) };
            Actors.Add(a); return a;
        }
        public Actor Find(int id) { return Actors.Find(a => a.Id == id); }
        public void RemoveGuest() { Actors.RemoveAll(a => a.Id == 2); Shots.RemoveAll(s => s.Owner == 2); }
        public void SetBots(int count)
        {
            count = Math.Max(0, Math.Min(Rules.MaxBots, count));
            Actors.RemoveAll(a => a.Bot); Shots.RemoveAll(s => s.Owner >= 100);
            for (int i = 0; i < count; i++)
            {
                double angle = i * 2.399963; float r = 8 + i % 7 * 1.5f;
                Actor a = new Actor { Id = 100 + i, Team = 3, Bot = true, Position = new V3((float)Math.Sin(angle) * r, 0, (float)Math.Cos(angle) * r + 2) };
                if(RangedBots) { a.Weapon=(WeaponId)(1+i%3); a.Ammo=GunRules.Get(a.Weapon).Magazine; }
                Move(a, new V3()); Actors.Add(a);
            }
        }

        public void Step(InputFrame host, InputFrame guest)
        {
            Tick++;
            // Keep a bounded rolling history for snapshots; the renderer deduplicates monotonic event IDs.
            if (Events.Count > 128) Events.RemoveRange(0, Events.Count - 128);
            foreach (Actor a in Actors)
            {
                if (!a.Alive) { if (Time >= a.RespawnAt) Respawn(a); else continue; }
                InputFrame input = a.Bot ? BotInput(a) : (a.Id == 1 ? host : guest);
                input = input.Sanitized();
                a.LastInput = input.Sequence;
                if(input.SelectWeapon>0) Equip(a,(WeaponId)(input.SelectWeapon-1));
                if(a.ReloadUntil>0&&Time>=a.ReloadUntil)
                {
                    a.Ammo=GunRules.Get(a.Weapon).Magazine; a.Magazines[(int)a.Weapon]=a.Ammo; a.ReloadUntil=0;
                    Emit(EventKind.ReloadEnd,a.Id,0,a.Position,0);
                }
                bool aim=input.Aim&&GunRules.Ranged(a.Weapon)&&Time>=a.BlockUntil&&Time>=a.StunUntil&&!a.Rolling(Time)&&a.ReloadUntil<=0&&!input.Roll&&!input.Block;
                if(aim&&!a.Aiming) { a.AimAt=Time; a.AimShots=0; }
                a.Aiming=aim; a.Pitch=input.Pitch; a.AimYaw=input.Yaw;
                a.Yaw = a.Attacking(Time) || a.Rolling(Time) ? a.Yaw : input.Yaw;
                if(input.AimTarget.Length>.001f&&GunRules.Ranged(a.Weapon))
                {
                    V3 d=input.AimTarget-GunRules.Muzzle(a); a.AimYaw=(float)(Math.Atan2(d.x,d.z)*180/Math.PI);
                    a.Pitch=MathUtil.Clamp((float)(Math.Atan2(d.y,Math.Sqrt(d.x*d.x+d.z*d.z))*180/Math.PI),-75,75);
                }
                if (!input.Sprint) a.SprintExhausted = false;
                a.Sprinting = false;
                if (Time < a.StunUntil) { a.QueuedAttack = false; MoveActor(a, new InputFrame()); continue; }

                if (input.Roll && Time >= a.RollReady && !a.Rolling(Time) && a.ReloadUntil<=0 && a.Stamina >= Rules.RollCost)
                {
                    Spend(a, Rules.RollCost); a.RollAt = Time; a.RollReady = Time + Rules.RollCooldown;
                    a.RollDirection = new V3(input.X, 0, input.Z).Normalized;
                    if (a.RollDirection.Length < .1f) a.RollDirection = V3.Forward(a.Yaw);
                    a.AttackUntil = -1; a.BlockUntil = 0; a.QueuedAttack = false;
                    Emit(EventKind.Dodge, a.Id, 0, a.Position, 0);
                }
                if (input.Block && Time >= a.BlockReady && !a.Rolling(Time))
                {
                    a.BlockUntil = Time + Rules.BlockDuration; a.BlockReady = Time + Rules.BlockCooldown;
                    a.AttackUntil = -1; a.QueuedAttack = false;
                }
                if (input.Reload) BeginReload(a);
                if (a.Weapon==WeaponId.Sword && input.Attack && a.Attacking(Time)) a.QueuedAttack = true;
                if (!a.Attacking(Time) && !a.Rolling(Time) && Time >= a.BlockUntil)
                {
                    if(GunRules.Ranged(a.Weapon))
                    {
                        if(input.Shoot&&Time>=a.ShootReady&&a.ReloadUntil<=0)
                        {
                            if(a.Ammo<=0) BeginReload(a); else FireGun(a,input);
                        }
                    }
                    else if (input.Attack || a.QueuedAttack) { StartAttack(a); a.QueuedAttack = false; }
                    else if (input.Shoot && Time >= a.ShootReady && Shots.Count < Rules.MaxProjectiles)
                    {
                        a.ShootReady = Time + .35f;
                        V3 f = V3.Forward(a.Yaw);
                        Shots.Add(new Shot { Id = nextShot++, Owner = a.Id, Team = a.Team, Position = a.Position + new V3(0, .9f, 0) + f * .7f, Velocity = f * 24 });
                        Emit(EventKind.Shot, a.Id, 0, a.Position + new V3(0, 1, 0), 0);
                    }
                }
                MoveActor(a, input);
                if (!a.Sprinting && Time >= a.RegenAt) a.Stamina = Math.Min(100, a.Stamina + Rules.StaminaRegen * Rules.Dt);
            }
            // Defense for every actor is established before any attack contact is resolved.
            foreach (Actor a in Actors)
                if (a.Alive && a.Attacking(Time) && Time >= a.AttackAt + Rules.SwordWindup && Time < a.AttackAt + Rules.SwordWindup + Rules.SwordActive)
                    Melee(a);
            StepShots();
        }

        public bool Equip(Actor a,WeaponId weapon)
        {
            if(!GunRules.Valid(weapon)||a.Weapon==weapon||!a.Alive||a.ReloadUntil>Time||a.Attacking(Time)||a.Rolling(Time)||Time<a.StunUntil||Time<a.ShootReady) return false;
            a.Magazines[(int)a.Weapon]=a.Ammo; a.Weapon=weapon; a.Ammo=a.Magazines[(int)weapon];
            a.LastShot=-100; a.ReloadAt=-100; a.ReloadUntil=0; a.Aiming=false; a.AimShots=0;
            a.ShootReady=Time+.25f; a.QueuedAttack=false; return true;
        }
        private void BeginReload(Actor a)
        {
            if(!GunRules.Ranged(a.Weapon)||a.ReloadUntil>0||a.Rolling(Time)||Time<a.StunUntil||a.Ammo>=GunRules.Get(a.Weapon).Magazine) return;
            a.ReloadAt=Time; a.ReloadUntil=Time+GunRules.Get(a.Weapon).Reload; a.Aiming=false;
            Emit(EventKind.ReloadStart,a.Id,0,a.Position,0);
        }
        private float RandomUnit() { randomState^=randomState<<13; randomState^=randomState>>17; randomState^=randomState<<5; return (randomState&0xffffff)/16777216f; }
        private void FireGun(Actor a,InputFrame input)
        {
            if(Shots.Count>=Rules.MaxProjectiles) return;
            var gun=GunRules.Get(a.Weapon); V3 start=GunRules.Muzzle(a),target=input.AimTarget;
            // A missing target is an aim direction, never a special point at world zero.
            if(target.Length<.001f) target=start+GunRules.Direction(a.Yaw,a.Pitch)*gun.Range;
            V3 offset=target-start; if(offset.Length>gun.Range+5) offset=offset.Normalized*(gun.Range+5);
            float yaw=(float)(Math.Atan2(offset.x,offset.z)*180/Math.PI),distance=(float)Math.Sqrt(offset.x*offset.x+offset.z*offset.z);
            float pitch=a.Aiming?GunRules.BallisticPitch(distance,offset.y,gun.Speed,gun.Gravity):(float)(Math.Atan2(offset.y,distance)*180/Math.PI);
            float spread=GunRules.Spread(a,Time); V3 forward=(GunRules.Direction(yaw,pitch)+new V3((RandomUnit()-.5f)*spread,(RandomUnit()-.5f)*spread,(RandomUnit()-.5f)*spread)).Normalized;
            a.Ammo--; a.ShotsFired++; a.Magazines[(int)a.Weapon]=a.Ammo;
            a.AimShots=Time-a.LastShot>.25f?1:a.AimShots+1; a.LastShot=Time;
            a.ShootReady=Time+gun.Interval*(a.Aiming?gun.AimInterval:1);
            // Resolve the short torso-to-muzzle path too: even a barrel overlapping cover cannot fire through it.
            V3 torso=a.Position+new V3(0,1.18f,0); Cover blocked=null; float near=2;
            foreach(var c in Covers) { float t; if(c.Hp>0&&MathUtil.SegmentBox(torso,start,c,.045f,out t)&&t<near) { near=t;blocked=c; } }
            Emit(EventKind.Shot,a.Id,0,start,(int)a.Weapon);
            if(blocked!=null) { DamageCover(blocked,gun.Damage,a.Id); return; }
            Shots.Add(new Shot { Id=nextShot++,Owner=a.Id,Team=a.Team,Weapon=a.Weapon,Position=start,Velocity=forward*gun.Speed,
                Damage=gun.Damage,Gravity=gun.Gravity,Radius=.045f,Impulse=gun.Impulse,Life=3,DistanceLeft=160 });
        }
        private void StartAttack(Actor a)
        {
            if (a.Stamina < Rules.SwordCost) return;
            a.Combo = Time - a.AttackAt < 2.4f ? (a.Combo + 1) % 3 : 0;
            a.AttackAt = Time; a.AttackUntil = Time + Rules.SwordDuration; a.Action++;
            a.Hit.Clear(); Spend(a, Rules.SwordCost); a.RegenAt = a.AttackUntil + Rules.RegenDelay;
        }
        private void Spend(Actor a, float cost) { a.Stamina = Math.Max(0, a.Stamina - cost); a.RegenAt = Time + Rules.RegenDelay; }
        private InputFrame BotInput(Actor a)
        {
            Actor target = null; float nearest = float.MaxValue;
            foreach (Actor other in Actors)
            {
                if (other.Team == a.Team || !other.Alive) continue;
                float distance = (other.Position - a.Position).Length;
                if (distance < nearest) { target = other; nearest = distance; }
            }
            if (target == null || StationaryBots) return new InputFrame();
            V3 direction = (target.Position - a.Position).Normalized;
            float yaw = (float)(Math.Atan2(direction.x, direction.z) * 180 / Math.PI);
            if(GunRules.Ranged(a.Weapon))
            {
                float desired=a.Weapon==WeaponId.Rifle?20:10;
                V3 aim=target.Position+new V3(0,1.1f,0),offset=aim-GunRules.Muzzle(a);
                float advance=nearest>desired+1?1:nearest<desired-2?-.7f:0;
                return new InputFrame { X=direction.x*advance,Z=direction.z*advance,Yaw=yaw,
                    Pitch=(float)(Math.Atan2(offset.y,Math.Sqrt(offset.x*offset.x+offset.z*offset.z))*180/Math.PI),AimTarget=aim,Aim=true,
                    Shoot=BotAttacks&&nearest<40&&Visible(GunRules.Muzzle(a),aim),Reload=a.Ammo==0,
                    Block=BotAttacks&&nearest<4&&(Tick+a.Id*13)%211==0 };
            }
            return new InputFrame { X = nearest > 1.8f ? direction.x : 0, Z = nearest > 1.8f ? direction.z : 0, Yaw = yaw,
                Attack = BotAttacks && nearest < 2.6f && (Tick + a.Id * 7) % 61 == 0,
                Shoot = BotAttacks && a.Id % 5 == 0 && nearest > 6 && (Tick + a.Id) % 110 < 2,
                Block = BotAttacks && nearest < 4 && (Tick + a.Id * 13) % 211 == 0 };
        }
        private void MoveActor(Actor a, InputFrame input)
        {
            float dt = Rules.Dt;
            V3 move = new V3(input.X, 0, input.Z), delta;
            bool stunned = Time < a.StunUntil;
            if (a.Rolling(Time) && !stunned)
            {
                float p = (Time - a.RollAt) / Rules.RollDuration;
                float q = (Time + dt - a.RollAt) / Rules.RollDuration;
                delta = a.RollDirection * ((MathUtil.Smooth(q) - MathUtil.Smooth(p)) * Rules.RollDistance);
                a.Velocity.x = a.Velocity.z = 0;
            }
            else
            {
                bool locked = stunned || a.Attacking(Time);
                bool sprint = !locked && Time >= a.BlockUntil && !input.Shoot && !a.Aiming && a.ReloadUntil<=0 && input.Sprint && !a.SprintExhausted
                    && a.Stamina > .0001f && move.Length > .5f && V3.Dot(move.Normalized, V3.Forward(a.Yaw)) >= .6f;
                a.Sprinting = sprint;
                if (sprint) { Spend(a, Rules.SprintDrain * dt); if (a.Stamina <= .0001f) a.SprintExhausted = true; }
                float speed = Rules.Speed * (sprint ? Rules.SprintMultiplier : 1) * (Time < a.BlockUntil ? .45f : 1);
                if(a.Aiming) speed*=GunRules.Get(a.Weapon).AimMove;
                if (locked) { a.Velocity.x = a.Velocity.z = 0; }
                else
                {
                    a.Velocity.x = MathUtil.Approach(a.Velocity.x, move.x * speed, Rules.Acceleration * dt);
                    a.Velocity.z = MathUtil.Approach(a.Velocity.z, move.z * speed, Rules.Acceleration * dt);
                }
                delta = new V3(a.Velocity.x, 0, a.Velocity.z) * dt;
                string spec=a.SourceSpec??(a.Weapon==WeaponId.Sword&&a.VisualSet==1?"daggers":"sword");
                var jump=NativeJumpSimulation.Try(spec,input.Jump,a.Grounded,false,locked||a.Rolling(Time),a.Velocity.y,Rules.JumpSpeed,ref a.AirJumps,out float launch);
                if(jump!=NativeJumpKind.None){a.Velocity.y=launch;a.Grounded=false;if(jump==NativeJumpKind.Air)a.AirJumpAt=Time;}
            }
            a.Velocity.y -= Rules.Gravity * dt;
            delta.y = a.Velocity.y * dt;
            a.Knockback *= (float)Math.Exp(-3.1f * dt);
            Move(a, delta + a.Knockback * dt);
            if (a.Position.y <= 0) { a.Position.y = 0; a.Velocity.y = Math.Max(0, a.Velocity.y); }
            a.Grounded=a.Position.y<.02f&&a.Velocity.y<=0;if(a.Grounded)a.AirJumps=0;
        }
        private void Move(Actor a, V3 delta)
        {
            a.Position += delta;
            float edge = Rules.ArenaHalf - Rules.Radius;
            a.Position.x = MathUtil.Clamp(a.Position.x, -edge, edge); a.Position.z = MathUtil.Clamp(a.Position.z, -edge, edge);
            foreach (Cover b in Covers)
            {
                if (b.Hp <= 0 || a.Position.y >= b.Height || a.Position.y + 1.8f <= 0) continue;
                float dx = a.Position.x - b.Position.x, dz = a.Position.z - b.Position.z;
                float ox = b.HalfX + Rules.Radius - Math.Abs(dx), oz = b.HalfZ + Rules.Radius - Math.Abs(dz);
                if (ox <= 0 || oz <= 0) continue;
                if (ox < oz) a.Position.x += (dx >= 0 ? 1 : -1) * ox;
                else a.Position.z += (dz >= 0 ? 1 : -1) * oz;
            }
        }
        private bool Visible(V3 start, V3 end)
        {
            foreach (Cover c in Covers) { float t; if (c.Hp > 0 && MathUtil.SegmentBox(start, end, c, 0, out t)) return false; }
            return true;
        }
        private void Melee(Actor a)
        {
            V3 origin = a.Position + new V3(0, .9f, 0), forward = V3.Forward(a.Yaw);
            foreach (Actor target in Actors)
            {
                if (!a.Attacking(Time) || !a.Alive) break; // A parry cancels the remaining sweep.
                if (!target.Alive || target.Team == a.Team || a.Hit.Contains(target.Id)) continue;
                V3 point = target.Position + new V3(0, .9f, 0), diff = point - origin;
                if (diff.Length > Rules.SwordReach + Rules.Radius || V3.Dot(new V3(diff.x, 0, diff.z).Normalized, forward) < .55f || !Visible(origin, point)) continue;
                a.Hit.Add(target.Id);
                Hit(a, target, Rules.SwordDamage, diff.Normalized, false);
            }
            foreach (Cover c in Covers)
            {
                if (!a.Attacking(Time)) break;
                if (c.Hp <= 0 || a.Hit.Contains(-c.Id)) continue;
                float t;
                if (!MathUtil.SegmentBox(origin, origin + forward * Rules.SwordReach, c, .25f, out t)) continue;
                a.Hit.Add(-c.Id); DamageCover(c, Rules.SwordDamage, a.Id);
            }
        }
        private void Hit(Actor source, Actor target, float damage, V3 direction, bool projectile, float impulse=325, bool headshot=false)
        {
            if (target.Invulnerable(Time)) return;
            if (Time < target.BlockUntil)
            {
                Emit(EventKind.Block, target.Id, source.Id, target.Position + new V3(0, 1, 0), 0);
                if (!projectile) { source.StunUntil = Time + .55f; source.AttackUntil = -1; source.QueuedAttack = false; }
                return;
            }
            target.Hp = Math.Max(0, target.Hp - damage); target.FlashUntil = Time + .14f;
            target.Knockback += new V3(direction.x, 0, direction.z).Normalized * (impulse/65f);
            if (Time >= target.StaggerImmuneUntil)
            {
                target.StunUntil = Time + .25f; target.StaggerImmuneUntil = target.StunUntil + .75f;
                target.AttackUntil = -1; target.QueuedAttack = false;
            }
            Emit(headshot?EventKind.Headshot:EventKind.Hit, source.Id, target.Id, target.Position + new V3(0, headshot?1.51f:1, 0), damage);
            if (!target.Alive)
            {
                source.Kills++; target.RespawnAt = Time + (target.Bot ? 5 : 3);
                Emit(EventKind.Death, source.Id, target.Id, target.Position, 0);
            }
        }
        private void DamageCover(Cover c, float damage, int source)
        {
            c.Hp = Math.Max(0, c.Hp - damage);
            Emit(c.Hp <= 0 ? EventKind.Break : EventKind.Hit, source, -c.Id, c.Position + new V3(0, c.Height * .5f, 0), damage);
        }
        private void StepShots()
        {
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                Shot shot = Shots[i]; shot.Life -= Rules.Dt;
                V3 end = shot.Position + shot.Velocity * Rules.Dt + new V3(0,-.5f*shot.Gravity*Rules.Dt*Rules.Dt,0);
                shot.Velocity.y-=shot.Gravity*Rules.Dt;
                shot.DistanceLeft-=(end-shot.Position).Length;
                float nearest = 2; Actor target = null; Cover cover = null; bool headshot=false,ground=false;
                foreach (Cover c in Covers)
                {
                    float t; if (c.Hp > 0 && MathUtil.SegmentBox(shot.Position, end, c, shot.Radius, out t) && t < nearest) { nearest = t; cover = c; }
                }
                foreach (Actor a in Actors)
                {
                    if (!a.Alive || a.Team == shot.Team || a.Invulnerable(Time)) continue;
                    float t; bool head=false;
                    bool hit=GunRules.Ranged(shot.Weapon)?GunRules.SegmentActor(shot.Position,end,a,shot.Radius,out t,out head):MathUtil.SegmentSphere(shot.Position,end,a.Position+new V3(0,.9f,0),.6f,out t);
                    if(hit&&t<nearest) { nearest=t;target=a;cover=null;headshot=head; }
                }
                if(end.y<=shot.Radius&&shot.Position.y>shot.Radius)
                {
                    float t=(shot.Position.y-shot.Radius)/(shot.Position.y-end.y);
                    if(t<nearest) { nearest=t;target=null;cover=null;ground=true; }
                }
                if (nearest <= 1)
                {
                    shot.Position += (end - shot.Position) * nearest;
                    if (target != null && Time < target.BlockUntil)
                    {
                        Emit(EventKind.Block, target.Id, shot.Owner, shot.Position, 0);
                        if (shot.Reflections++ < 3)
                        {
                            shot.Owner = target.Id; shot.Team = target.Team; shot.Velocity *= -1;
                            shot.Position = target.Position + new V3(0, .9f, 0) + shot.Velocity.Normalized * .8f;
                            continue;
                        }
                    }
                    else if (target != null) { Actor source = Find(shot.Owner); if (source != null) Hit(source, target, shot.Damage*(headshot?1.5f:1), shot.Velocity.Normalized, true,shot.Impulse,headshot); }
                    else if (cover != null) DamageCover(cover, shot.Damage, shot.Owner);
                    else if(ground) Emit(EventKind.Impact,shot.Owner,0,shot.Position,0);
                    Shots.RemoveAt(i);
                }
                else if (shot.Life <= 0 || shot.DistanceLeft<=0 || end.y<0 || Math.Abs(end.x) >= Rules.ArenaHalf || Math.Abs(end.z) >= Rules.ArenaHalf) Shots.RemoveAt(i);
                else shot.Position = end;
            }
        }
        private void Respawn(Actor a)
        {
            a.Hp = a.Stamina = 100; a.Position = new V3(a.Id == 1 ? -3 : a.Id == 2 ? 3 : (a.Id % 15 - 7) * 2, 0, a.Bot ? 17 : -13);
            a.Velocity = a.Knockback = new V3(); a.AttackAt = a.AttackUntil = a.RollAt = -100;
            a.BlockUntil = a.BlockReady = a.RollReady = a.StunUntil = a.RegenAt = a.StaggerImmuneUntil = 0;
            a.QueuedAttack = a.SprintExhausted = false;a.Grounded=true;a.AirJumps=0;a.AirJumpAt=-100;
            for(int i=1;i<4;i++) a.Magazines[i]=GunRules.All[i].Magazine;
            a.Ammo=a.Magazines[(int)a.Weapon]; a.ReloadUntil=0; a.ReloadAt=a.LastShot=-100; a.Aiming=false; a.AimShots=0; a.ShootReady=Time; a.Hit.Clear(); Move(a, new V3());
        }
        private void Emit(EventKind kind, int source, int target, V3 position, float amount)
        { Events.Add(new ArenaEvent { Id = nextEvent++, Kind = kind, Source = source, Target = target, Position = position, Amount = amount }); }
        // Call on the authority thread. Encode immediately before the next Step; fields are live references.
        public Snapshot Capture() { return new Snapshot { Tick = Tick, Actors = Actors.ToArray(), Covers = Covers.ToArray(), Shots = Shots.ToArray(), Events = Events.ToArray() }; }
    }
}
