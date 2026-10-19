using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A pilot this frame for the satellites: seat, satellite look (-1 none), presented body centre, aim yaw (rad), alive.</summary>
    public struct RoundsPilotInput{public int Seat,Satellites;public Vector3 Body;public float AimYaw;public bool Alive;}
    /// <summary>A homing (or last-round) shot this frame: replicated id and firer, its look, drawn position and velocity, birth.</summary>
    public struct RoundsShotInput{public int Id,Owner,Look;public bool Homing,LastRound;public Vector3 P,V;public float Born;}
    /// <summary>A Drone Nest minion this frame: replicated id, owner seat, presented capsule centre, velocity, yaw (rad), last
    /// attack and last hit (authority s), health share, the owner's accent (linear).</summary>
    public struct RoundsDroneInput{public int Id,Owner;public Vector3 At,Velocity;public float Yaw,LastAttack,LastHit,Health;public Vector3 Accent;}
    /// <summary>A minion shot beam this frame (the toy's AbilityFx record: from its muzzle to its target).</summary>
    public struct RoundsBeamInput{public Vector3 From,To;public float Until;}
    /// <summary>What a drone drew this frame (checks).</summary>
    public struct RoundsDroneShape{public int Id;public Vector3 Body;public float Age,Scale;public byte Phase;public int Pieces;}

    /// <summary>
    /// The ROUNDS packs' satellites and combat drones (owner 2026-10-19: "Escort Array's orbiting satellites, Drone Nest's combat
    /// toys" had no visuals of their own).
    /// <list type="bullet">
    /// <item>Satellites (Escort Array, Twin Escort, Manifest Array, Sentry Array): two per holder, drawn exactly where the
    /// authority launches a homing shot from (NativeRoundsLooks.SatelliteAt on the presentation clock), so every homing shot leaves
    /// its satellite. Each look is a small lit model (blade escort, crescent scythe, arcane prism with its ring, sentry pod with
    /// its barrel on the holder's aim) with a glowing core and an orbit trail; a homing shot's first sight flashes its satellite's
    /// muzzle and kicks it back; the shot itself gets a hot head and a curving trail from its drawn path.</item>
    /// <item>Drone Nest's combat toys: a quad-rotor drone in the holder's accent at the toy's replicated position and yaw: a
    /// holographic summon-in (from its Summon event, else its first sight), a hover bob, banking into its motion, spinning rotors,
    /// the attack's windup (the optic and barrel charging over the authority's .45 s from its replicated last attack) and its
    /// laser bolt along the toy's own shot beam, a jolt and sparks when hit, a red blink in its last second (its 6 s life from the
    /// summon), a power-down when it expires or is replaced (it leaves without a death record), and a breakup (blast, sparks,
    /// tumbling parts) when it is killed (its death record).</item>
    /// </list>
    /// Pure functions of replicated ids, times and positions on the presentation clock. Allocation free.
    /// </summary>
    public sealed class NativeRoundsUnits
    {
        private static float H(uint s,int i)=>BossMath.Hash(s,i);
        // ---- satellites ----
        /// <summary>The satellite models' scale (their meshes are built at unit scale; drawn about .5 m across, a drone's size).</summary>
        public const float SatelliteScale=1.7f;
        /// <summary>Satellite world positions drawn this frame (checks): seat, side, position.</summary>
        public readonly (int seat,int side,Vector3 at)[] Satellites=new (int,int,Vector3)[32];public int SatelliteCount;
        private struct Flash{public int Seat,Side,Shot;public float At;public Vector3 Dir;}
        private readonly Flash[] flashes=new Flash[32];private int nextFlash;
        private struct Trail{public int Id,Look,Count,Head;public float Seen;public Vector3[] P;public bool LastRound;}
        private const int TrailPoints=14;
        private readonly Trail[] trails=new Trail[48];
        public NativeRoundsUnits(){for(int i=0;i<trails.Length;i++)trails[i].P=new Vector3[TrailPoints];for(int i=0;i<drones.Length;i++)drones[i].Id=-1;}
        public void Clear(){SatelliteCount=0;for(int i=0;i<flashes.Length;i++)flashes[i].At=-1e9f;for(int i=0;i<trails.Length;i++){trails[i].Id=0;trails[i].Count=0;}for(int i=0;i<drones.Length;i++)drones[i].Id=-1;DroneCount=0;}

        /// <summary>The satellites of every holder at `clock`.</summary>
        public void DrawSatellites(RoundsPilotInput[] pilots,int count,float clock,NativeBossVfx fx,RoundsLit lit)
        {
            SatelliteCount=0;
            for(int i=0;i<count;i++)
            {
                var p=pilots[i];if(p.Satellites<0||!p.Alive)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    var at=NativeRoundsLooks.SatelliteAt(p.Body,clock,side);float a=clock*4+(side>0?0:(float)Math.PI);
                    var tangent=Vector3.Normalize(new Vector3((float)Math.Cos(a)*.75f,.5f*(float)Math.Cos(a*2),-(float)Math.Sin(a)*.75f));
                    // A muzzle flash: kicked back along the shot for .12 s.
                    float kick=0;Vector3 dir=tangent;
                    for(int f=0;f<flashes.Length;f++){var fl=flashes[f];if(fl.Seat!=p.Seat||fl.Side!=side)continue;float u=(clock-fl.At)/.14f;if(u<0||u>1)continue;kick=Math.Max(kick,1-u);dir=fl.Dir;}
                    var shown=at-dir*(.05f*kick);
                    Satellite(p.Satellites,shown,tangent,p.AimYaw,clock,side,p.Seat,kick,fx,lit);
                    if(SatelliteCount<Satellites.Length)Satellites[SatelliteCount++]=(p.Seat,side,at);
                    // The orbit trail: soft glows a few hundredths of a second back along the orbit.
                    for(int k=1;k<=5;k++){var b=NativeRoundsLooks.SatelliteAt(p.Body,clock-k*.025f,side);fx.Glow(b,NativeRoundsLooks.SatelliteHot[p.Satellites]*(.55f/k),.07f,NativeBossVfx.Mote);}
                    if(kick>0){fx.Glow(shown+dir*.12f,NativeRoundsLooks.SatelliteCore[p.Satellites]*(5*kick),.32f*kick+.08f,NativeBossVfx.Core);for(int s=0;s<4;s++){var j=Vector3.Normalize(dir+new Vector3(H(7,s)-.5f,H(9,s)-.5f,H(11,s)-.5f)*.9f);fx.Spark(shown+dir*.1f+j*(.18f*(1-kick)),j*6,NativeRoundsLooks.SatelliteHot[p.Satellites]*(2.6f*kick),.016f,.05f,.03f,.85f);}}
                }
            }
        }
        private static void Satellite(int look,Vector3 at,Vector3 tangent,float aimYaw,float clock,int side,int seat,float kick,NativeBossVfx fx,RoundsLit lit)
        {
            var hot=NativeRoundsLooks.SatelliteHot[look];var core=NativeRoundsLooks.SatelliteCore[look];var body=NativeRoundsLooks.SatelliteBody[look];float pulse=.75f+.25f*(float)Math.Sin(clock*7+side+seat);
            // A frame facing along the orbit (blade, scythe) or the aim (sentry).
            float yaw=(float)Math.Atan2(tangent.X,tangent.Z),pitch=-(float)Math.Asin(Math.Max(-1,Math.Min(1,tangent.Y)));
            var face=Matrix4x4.CreateRotationX(pitch)*Matrix4x4.CreateRotationY(yaw);var big=Matrix4x4.CreateScale(SatelliteScale);
            switch(look)
            {
                case 0:
                {
                    var m=big*Matrix4x4.CreateRotationZ(clock*2.2f+side)*face*Matrix4x4.CreateTranslation(at);
                    lit.Add(NativeRoundsMeshes.SatBlade,m,body*1.8f,1.3f,hot*(.9f*pulse),.92f,hot*3.2f);
                    for(int f=-1;f<=1;f+=2)lit.Add(NativeRoundsMeshes.SatFin,Matrix4x4.CreateTranslation(f*.05f,0,-.06f)*m,body,.9f,hot*.3f,.8f,hot);
                    break;
                }
                case 1:
                {
                    var m=big*Matrix4x4.CreateRotationY(clock*14*side)*Matrix4x4.CreateRotationX(.25f)*Matrix4x4.CreateTranslation(at);
                    lit.Add(NativeRoundsMeshes.SatScythe,m,body*1.6f,1.2f,hot*(1.2f*pulse),.9f,hot*3.2f);
                    break;
                }
                case 2:
                {
                    var m=big*Matrix4x4.CreateRotationY(clock*3+side)*Matrix4x4.CreateRotationX(clock*1.7f)*Matrix4x4.CreateTranslation(at);
                    lit.Add(NativeRoundsMeshes.SatPrism,m,body*1.6f,1.1f,hot*(1.1f*pulse),.95f,hot*3.2f);
                    lit.Add(NativeRoundsMeshes.SatRing,big*Matrix4x4.CreateRotationX(1.1f)*Matrix4x4.CreateRotationY(-clock*2.4f)*Matrix4x4.CreateTranslation(at),hot*.2f,.4f,hot*(1.6f*pulse),.6f,hot);
                    for(int r=0;r<3;r++){float a=clock*5+r*2.094f;fx.Glow(at+new Vector3((float)Math.Cos(a)*.13f,(float)Math.Sin(a*1.3f)*.05f,(float)Math.Sin(a)*.13f),hot*1.6f,.06f,NativeBossVfx.Mote);}
                    break;
                }
                default:
                {
                    var m=big*Matrix4x4.CreateRotationY(aimYaw)*Matrix4x4.CreateTranslation(at);
                    lit.Add(NativeRoundsMeshes.SatPod,m,body*1.8f,1.0f,hot*.15f,.85f,hot*2.4f);
                    lit.Add(NativeRoundsMeshes.SatCollar,Matrix4x4.CreateRotationY(clock*3)*m,body*.8f,.8f,hot*(1.3f*pulse),.7f,hot);
                    lit.Add(NativeRoundsMeshes.SatBarrel,Matrix4x4.CreateRotationX((float)Math.PI/2)*Matrix4x4.CreateTranslation(0,0,.1f-.04f*kick)*m,body,.9f,hot*.4f,.8f,hot*.8f);
                    break;
                }
            }
            fx.Glow(at,core*(1.3f*pulse),.1f,NativeBossVfx.Core);fx.Glow(at,hot*(.5f*pulse),.42f,NativeBossVfx.Mote);
        }
        /// <summary>
        /// A homing or last-round shot seen this frame (its id is replicated): a homing shot's first sight (within .15 s of its
        /// birth) flashes the satellite it left; its drawn path is kept as a trail.
        /// </summary>
        public void Shot(in RoundsShotInput s,float clock,RoundsPilotInput[] pilots,int count)
        {
            int free=-1,found=-1;
            for(int i=0;i<trails.Length;i++){if(trails[i].Id==s.Id&&trails[i].Count>0){found=i;break;}if(free<0&&(trails[i].Count==0||clock-trails[i].Seen>.5f))free=i;}
            if(found<0)
            {
                if(free<0||!s.Homing&&!s.LastRound)return;found=free;trails[found].Id=s.Id;trails[found].Look=s.Look;trails[found].Count=0;trails[found].Head=0;trails[found].LastRound=s.LastRound;
                if(s.Homing&&clock-s.Born<.15f)for(int i=0;i<count;i++)
                {
                    var p=pilots[i];if(p.Seat!=s.Owner||p.Satellites<0)continue;
                    float best=1e9f;int side=1;for(int k=-1;k<=1;k+=2){float d=Vector3.DistanceSquared(NativeRoundsLooks.SatelliteAt(p.Body,s.Born,k),s.P-s.V*Math.Max(0,clock-s.Born));if(d<best){best=d;side=k;}}
                    flashes[nextFlash++%flashes.Length]=new Flash{Seat=s.Owner,Side=side,Shot=s.Id,At=s.Born,Dir=s.V.LengthSquared()>1e-6f?Vector3.Normalize(s.V):Vector3.UnitZ};
                }
            }
            ref var t=ref trails[found];t.Seen=clock;
            if(t.Count==0||Vector3.DistanceSquared(t.P[(t.Head+TrailPoints-1)%TrailPoints],s.P)>.0016f){t.P[t.Head]=s.P;t.Head=(t.Head+1)%TrailPoints;if(t.Count<TrailPoints)t.Count++;}
        }
        /// <summary>The shots' heads and trails (a shot unseen this frame is dropped).</summary>
        public void DrawShots(float clock,NativeBossVfx fx)
        {
            for(int i=0;i<trails.Length;i++)
            {
                ref var t=ref trails[i];if(t.Count==0)continue;if(clock-t.Seen>.001f){t.Count=0;continue;}
                Vector3 hot=t.LastRound?new Vector3(3.2f,2.1f,.6f):NativeRoundsLooks.SatelliteHot[Math.Max(0,Math.Min(3,t.Look))]*2.2f;
                var head=t.P[(t.Head+TrailPoints-1)%TrailPoints];fx.Glow(head,hot*1.2f,t.LastRound?.3f:.22f,NativeBossVfx.Core);
                for(int k=1;k<t.Count;k++)
                {
                    var a=t.P[(t.Head+TrailPoints-k)%TrailPoints];var b=t.P[(t.Head+TrailPoints-k-1)%TrailPoints];float u=(float)k/t.Count;
                    fx.Spark(a,a-b,hot*(1-u)*.9f,(t.LastRound?.05f:.035f)*(1-u*.6f),(a-b).Length(),1,.9f,u,k);
                }
            }
        }

        // ---- drones ----
        private struct Drone
        {
            public int Id,Owner;public bool Known;public float Spawn,Seen,Gone,Dead;public Vector3 At,Velocity,Accent,DeadAt;public float Yaw,LastAttack,LastHit,Health,DeadYaw;
            public Vector3 Bolt,BoltTo;public float BoltAt;
        }
        public const int MaxDrones=32;
        private readonly Drone[] drones=new Drone[MaxDrones];
        public readonly RoundsDroneShape[] DroneShapes=new RoundsDroneShape[MaxDrones];public int DroneCount;
        /// <summary>Drone phases (checks): 1 summoning, 2 hovering, 3 attacking, 4 expiring, 5 powering down, 6 destroyed.</summary>
        public const byte Summoning=1,Hovering=2,Attacking=3,Expiring=4,PoweringDown=5,Destroyed=6;
        /// <summary>The summon-in, power-down and breakup durations (s).</summary>
        public const float SummonIn=.45f,PowerDown=.42f,Breakup=1.6f,DeathMemory=6;
        private int Find(int id){for(int i=0;i<drones.Length;i++)if(drones[i].Id==id)return i;return -1;}
        /// <summary>Whether minion `id` is a remembered drone (the toy soldier's death presentation then stands aside).</summary>
        public bool Known(int id)=>id>=0&&Find(id)>=0;
        /// <summary>Nothing remembered (no drone, no trail).</summary>
        public bool Idle{get{for(int i=0;i<drones.Length;i++)if(drones[i].Id>=0)return false;for(int i=0;i<trails.Length;i++)if(trails[i].Count>0)return false;return true;}}
        /// <summary>A Summon event for minion `id` at authority time `at` (its spawn: the summon-in and the 6 s life count from it).</summary>
        public void Summoned(int id,int owner,float at,Vector3 where)
        {
            int i=Find(id);if(i<0){i=Free();if(i<0)return;drones[i]=new Drone{Id=id,Owner=owner,Known=true,Spawn=at,Seen=float.NaN,Gone=float.NaN,Dead=float.NaN,At=where+new Vector3(0,.6f,0),Yaw=0,LastAttack=-100,LastHit=-100,Health=1,BoltAt=-100};}
            else if(!drones[i].Known||at<drones[i].Spawn){drones[i].Spawn=at;drones[i].Known=true;}
        }
        private int Free(){for(int i=0;i<drones.Length;i++)if(drones[i].Id<0)return i;int best=0;float old=float.MaxValue;for(int i=0;i<drones.Length;i++){float t=float.IsNaN(drones[i].Seen)?-1e9f:drones[i].Seen;if(t<old){old=t;best=i;}}return best;}
        /// <summary>A live drone this frame (the replicated toy minion of a Drone Nest holder).</summary>
        public void Observe(in RoundsDroneInput d,float clock)
        {
            int i=Find(d.Id);
            // First seen without its Summon event (a late join): no summon-in and no expiry blink (its life's start is unknown).
            if(i<0){i=Free();if(i<0)return;drones[i]=new Drone{Id=d.Id,Known=false,Spawn=clock-SummonIn,Gone=float.NaN,Dead=float.NaN,LastAttack=-100,LastHit=-100,BoltAt=-100};}
            ref var r=ref drones[i];r.Owner=d.Owner;r.Seen=clock;r.At=d.At;r.Velocity=d.Velocity;r.Yaw=d.Yaw;r.LastHit=d.LastHit;r.Health=d.Health;r.Accent=d.Accent;r.Gone=float.NaN;
            if(d.LastAttack>r.LastAttack+.01f){r.LastAttack=d.LastAttack;r.BoltAt=-100;}
        }
        /// <summary>A death record for minion `id` (it was killed): its breakup from `at`.</summary>
        public void Died(int id,float at,Vector3 where,float yaw)
        {int i=Find(id);if(i<0)return;ref var r=ref drones[i];if(!float.IsNaN(r.Dead))return;r.Dead=at;r.DeadAt=where;r.DeadYaw=yaw;}
        /// <summary>Ends a frame of observations: a remembered live drone not seen at `clock` (and with no death record) left without
        /// dying: it expired or was replaced, and powers down from its last sight.</summary>
        public void EndDrones(float clock)
        {
            for(int i=0;i<drones.Length;i++)
            {
                ref var r=ref drones[i];if(r.Id<0)continue;
                if(float.IsNaN(r.Seen)){if(clock-r.Spawn>.5f&&float.IsNaN(r.Dead))r.Id=-1;continue;}
                if(float.IsNaN(r.Dead)&&float.IsNaN(r.Gone)&&r.Seen<clock-1e-4f)r.Gone=r.Seen;
                if(!float.IsNaN(r.Gone)&&clock-r.Gone>PowerDown+.5f&&float.IsNaN(r.Dead))r.Id=-1;
                // A destroyed drone is remembered while its death record can still be in the replicated list (five seconds), so the
                // toy soldier's death presentation never takes it over.
                if(!float.IsNaN(r.Dead)&&clock-r.Dead>DeathMemory)r.Id=-1;
            }
        }
        /// <summary>Keeps a drone's bolt on its own shot beam: a beam whose start is at the drone's muzzle.</summary>
        public void Beam(in RoundsBeamInput b,float clock)
        {
            for(int i=0;i<drones.Length;i++)
            {
                ref var r=ref drones[i];if(r.Id<0||!float.IsNaN(r.Gone)||!float.IsNaN(r.Dead))continue;
                var muzzle=r.At+new Vector3(0,NativeRoundsLooks.DroneMuzzle,0);if(Vector3.DistanceSquared(muzzle,b.From)>.36f)continue;
                if(clock-r.BoltAt>.25f){r.BoltAt=Math.Min(clock,b.Until-.22f);r.Bolt=b.From;r.BoltTo=b.To;}
            }
        }
        /// <summary>Every remembered drone at `clock`.</summary>
        public void DrawDrones(float clock,NativeBossVfx fx,RoundsLit lit,bool low)
        {
            DroneCount=0;
            for(int i=0;i<drones.Length;i++)
            {
                ref var r=ref drones[i];if(r.Id<0||float.IsNaN(r.Seen))continue;uint seed=(uint)r.Id*2246822519u+3u;int l0=lit.Pieces;var shape=new RoundsDroneShape{Id=r.Id,Age=clock-r.Spawn};
                if(!float.IsNaN(r.Dead)){if(clock-r.Dead>Breakup)continue;Breakage(r,clock-r.Dead,seed,fx);shape.Phase=Destroyed;shape.Body=r.DeadAt;}
                else
                {
                    float age=clock-r.Spawn,scale=1;byte phase=Hovering;Vector3 flash=Vector3.Zero;float glow=1,bob=(float)Math.Sin(clock*9.5f+r.Id)*.035f;
                    if(age<SummonIn){phase=Summoning;float u=Math.Max(0,age/SummonIn);scale=.55f+.45f*BossMath.Smoother(u);glow=1+3*(1-u);}
                    bool expiring=r.Known&&age>NativeRoundsLooks.DroneLife-.9f&&float.IsNaN(r.Gone);if(expiring)phase=Expiring;
                    float gone=float.IsNaN(r.Gone)?-1:clock-r.Gone;if(gone>=0){phase=PoweringDown;float u=Math.Min(1,gone/PowerDown);scale=1-BossMath.Smooth(u);bob-=.12f*u;if(scale<.02f){Fizzle(r,gone,seed,fx);shape.Phase=phase;shape.Body=r.At;if(DroneCount<DroneShapes.Length)DroneShapes[DroneCount++]=shape;continue;}}
                    float atk=clock-r.LastAttack;bool attacking=atk>=0&&atk<NativeRoundsLooks.DroneWindup+NativeRoundsLooks.DroneRecover&&gone<0;if(attacking&&phase==Hovering)phase=Attacking;
                    float charge=attacking&&atk<NativeRoundsLooks.DroneWindup?BossMath.Smooth(atk/NativeRoundsLooks.DroneWindup):0,recoil=attacking&&atk>=NativeRoundsLooks.DroneWindup?Math.Max(0,1-(atk-NativeRoundsLooks.DroneWindup)/.2f):0;
                    float hit=clock-r.LastHit;var jolt=hit>=0&&hit<.18f?new Vector3(H(seed,(int)(clock*60))-.5f,H(seed,(int)(clock*60)+7)-.5f,H(seed,(int)(clock*60)+13)-.5f)*(.06f*(1-hit/.18f)):Vector3.Zero;
                    var body=r.At+new Vector3(0,.22f+bob,0)+jolt;shape.Body=body;
                    DroneModel(body,r.Yaw,r.Velocity,clock,scale,glow,charge,recoil,expiring?age:-1,gone,r.Accent,seed,fx,lit,low);
                    if(age<SummonIn)SummonFx(r,age,body,seed,fx);
                    if(hit>=0&&hit<.2f)fx.SparkBurst(clock,body,Vector3.UnitY,1.6f,3.2f,8,.3f,r.LastHit,.02f,seed,(int)(r.LastHit*60)&0xffff,1.1f,.014f,1.4f,true,r.At.Y-.6f);
                    // The bolt: a laser streak from the muzzle to the beam's target over .07 s, then sparks where it lands.
                    float b=clock-r.BoltAt;
                    if(b>=0&&b<.3f)
                    {
                        var from=r.Bolt;var to=r.BoltTo;float u=Math.Min(1,b/.07f);var head=Vector3.Lerp(from,to,u);var tail=Vector3.Lerp(from,to,Math.Max(0,u-.45f));float fade=b<.07f?1:Math.Max(0,1-(b-.07f)/.18f);
                        var c=r.Accent*2.6f+new Vector3(.6f,.6f,.6f);fx.Spark(head,head-tail,c*(2.2f*fade),.045f,(head-tail).Length(),1,.85f);fx.Spark(head,head-tail,r.Accent*(1.6f*fade),.11f,(head-tail).Length(),1,.9f);
                        if(b<.12f)fx.Glow(from,c*(3*(1-b/.12f)),.22f,NativeBossVfx.Core);
                        if(b>=.07f){fx.Glow(to,c*(2.5f*fade),.28f,NativeBossVfx.Core);fx.SparkBurst(clock,to,Vector3.Normalize(from-to+new Vector3(0,.001f,0)),1.3f,3,7,.28f,r.BoltAt+.07f,.01f,seed,999,1,.012f,1.6f,true,to.Y-1.2f);}
                    }
                    shape.Phase=phase;shape.Scale=scale;
                }
                shape.Pieces=lit.Pieces-l0;if(DroneCount<DroneShapes.Length)DroneShapes[DroneCount++]=shape;
            }
        }
        // The drone: a chamfered hull with a glass canopy and the accent's light strips, an optic at the nose over the barrel, four
        // arms to spinning two-blade rotors, two skids. It banks into its motion (up to 16 degrees), its optic and barrel charge
        // through the windup and kick on the shot, its optic blinks red in its last second and dims through a power-down.
        private static void DroneModel(Vector3 at,float yaw,Vector3 vel,float clock,float scale,float glow,float charge,float recoil,float expiring,float gone,Vector3 accent,uint seed,NativeBossVfx fx,RoundsLit lit,bool low)
        {
            float cy=(float)Math.Cos(yaw),sy=(float)Math.Sin(yaw);float fwd=vel.X*sy+vel.Z*cy,side=vel.X*cy-vel.Z*sy;
            float pitch=BossMath.Clamp(fwd*.07f,-.28f,.28f)-.12f*charge+.18f*recoil,roll=BossMath.Clamp(-side*.07f,-.28f,.28f);
            var m=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateRotationZ(roll)*Matrix4x4.CreateRotationX(pitch)*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(at);
            var hull=new Vector3(.055f,.058f,.066f);var dark=new Vector3(.02f,.022f,.026f);float dim=gone>=0?1-Math.Min(1,gone/PowerDown):1;
            bool blink=expiring>=0&&((int)Math.Floor(expiring*8))%2==0;var eye=blink?new Vector3(3,.15f,.1f):accent*(2+4*charge)+new Vector3(.4f,.4f,.4f)*charge;
            lit.Add(NativeRoundsMeshes.DroneHull,m,hull,1.0f,accent*(.05f*glow),.82f,accent*(.7f*dim));
            lit.Add(NativeRoundsMeshes.DroneCanopy,Matrix4x4.CreateScale(1.15f,.55f,1.25f)*Matrix4x4.CreateTranslation(0,.045f,-.01f)*m,dark,1.4f,accent*(.25f*dim*glow),.95f,accent*1.2f);
            for(int s=-1;s<=1;s+=2)lit.Add(NativeRoundsMeshes.DroneStrip,Matrix4x4.CreateTranslation(s*.132f,.0f,0)*m,dark,.5f,accent*(2.4f*dim*glow),.5f,accent);
            lit.Add(NativeRoundsMeshes.DroneLens,Matrix4x4.CreateTranslation(0,.005f,.16f)*m,dark,1.5f,eye*dim,.95f,eye*.5f);
            lit.Add(NativeRoundsMeshes.DroneBarrel,Matrix4x4.CreateRotationX((float)Math.PI/2)*Matrix4x4.CreateTranslation(0,-.05f,.16f-.05f*recoil)*m,dark,.9f,accent*(1.5f*charge),.8f,accent*.4f);
            for(int s=-1;s<=1;s+=2)lit.Add(NativeRoundsMeshes.DroneSkid,Matrix4x4.CreateTranslation(s*.09f,-.075f,0)*m,hull*.8f,.6f,Vector3.Zero,.6f,accent*.3f);
            float spin=clock*(gone>=0?60*dim:70)+H(seed,1)*6;
            for(int k=0;k<4;k++)
            {
                float ax=(k&1)==0?-1:1,az=(k&2)==0?-1:1;var arm=Matrix4x4.CreateRotationY(ax*az>0?-.75f:.75f)*Matrix4x4.CreateTranslation(ax*.12f,.01f,az*.12f)*m;
                lit.Add(NativeRoundsMeshes.DroneArm,arm,hull*.9f,.8f,Vector3.Zero,.7f,accent*.3f);
                var hub=Matrix4x4.CreateTranslation(ax*.22f,.025f,az*.21f)*m;lit.Add(NativeRoundsMeshes.DroneHub,hub,dark,.8f,accent*(.6f*dim),.7f,accent*.5f);
                // Three blade copies a little apart in angle read as the motion blur of a fast rotor.
                int copies=low?1:3;for(int c=0;c<copies;c++){float a=spin*(k%2==0?1:-1)+c*.35f;lit.Add(NativeRoundsMeshes.DroneBlade,Matrix4x4.CreateRotationY(a)*Matrix4x4.CreateTranslation(0,.025f,0)*hub,dark,.6f,accent*(c==0?.2f:0),.5f,accent*(c==0?.25f:.08f));}
                // The rotor's blur: a faint horizontal ring of light at the blade tips (the disc it sweeps).
                var hp=Vector3.Transform(new Vector3(ax*.22f,.03f,az*.21f),m);Ring(fx,hp,.1f*scale,accent*(.55f*dim),.006f,low?8:12,spin*.1f);
            }
            // Under-glow of the lift, and the optic's bloom.
            var eyeAt=Vector3.Transform(new Vector3(0,.005f,.19f),m);fx.Glow(eyeAt,eye*(.9f*dim),.14f+.12f*charge,NativeBossVfx.Core);
            fx.Glow(Vector3.Transform(new Vector3(0,-.1f,0),m),accent*(.5f*dim),.45f*scale,NativeBossVfx.Mote);
            if(charge>0)fx.Glow(Vector3.Transform(new Vector3(0,-.05f,.25f),m),accent*(2.5f*charge),.08f+.12f*charge,NativeBossVfx.Core);
        }
        // A horizontal ring of light (streak segments) of radius r round `c`.
        private static void Ring(NativeBossVfx fx,Vector3 c,float r,Vector3 rgb,float width,int seg,float turn)
        {
            for(int k=0;k<seg;k++){float a0=turn+k*6.2831853f/seg,a1=turn+(k+.85f)*6.2831853f/seg;var p0=c+new Vector3((float)Math.Cos(a0)*r,0,(float)Math.Sin(a0)*r);var p1=c+new Vector3((float)Math.Cos(a1)*r,0,(float)Math.Sin(a1)*r);fx.Spark(p1,p1-p0,rgb,width,(p1-p0).Length(),1,.95f);}
        }
        // The summon-in: a column of scanlines building the drone from the floor and a landing ring on the floor below.
        private static void SummonFx(in Drone r,float age,Vector3 body,uint seed,NativeBossVfx fx)
        {
            float u=Math.Max(0,age/SummonIn);var c=r.Accent*2+new Vector3(.3f,.3f,.3f);float floor=r.At.Y-.6f;
            for(int k=0;k<4;k++){float y=floor+(body.Y+.15f-floor)*Math.Min(1,u*1.3f-k*.08f);if(y<floor)continue;int n=10;for(int j=0;j<n;j++){float a=j*6.2831853f/n+age*4,rr=.3f;var p=new Vector3(body.X+(float)Math.Cos(a)*rr,y,body.Z+(float)Math.Sin(a)*rr);var q=new Vector3(body.X+(float)Math.Cos(a+.63f)*rr,y,body.Z+(float)Math.Sin(a+.63f)*rr);fx.Spark(q,q-p,c*(1.5f*(1-u)),.012f,(q-p).Length(),1,.9f);}}
            Ring(fx,new Vector3(body.X,floor+.03f,body.Z),.15f+.45f*u,c*(2.2f*(1-u)),.02f,20,age*2);
            fx.SparkBurst(age+r.Spawn,new Vector3(body.X,floor+.05f,body.Z),Vector3.UnitY,1.4f,2.4f,10,.4f,r.Spawn,.05f,seed,77,.8f,.012f,1.2f,true,floor);
        }
        // The power-down's last moment: a small implosion flash and a puff of sparks where it hovered.
        private static void Fizzle(in Drone r,float gone,uint seed,NativeBossVfx fx)
        {
            var at=r.At+new Vector3(0,.12f,0);float u=(gone-PowerDown)/.12f;if(u<1)fx.Glow(at,(r.Accent*2+Vector3.One)*(2*(1-Math.Max(0,u))),.35f,NativeBossVfx.Core);
            fx.SparkBurst(gone+r.Gone,at,Vector3.UnitY,1.8f,2,8,.35f,r.Gone+PowerDown*.8f,.05f,seed,55,.8f,.012f,1.2f,true,r.At.Y-.6f);
        }
        // Destroyed: a flash and a small fireball, sparks, smoke, and the hull, arms and rotors as tumbling lit pieces that bounce.
        private static void Breakage(in Drone r,float a,uint seed,NativeBossVfx fx)
        {
            var at=r.DeadAt+new Vector3(0,.22f,0);float floor=r.DeadAt.Y-.6f;float t=a+r.Dead;
            if(a<.14f)fx.Glow(at,new Vector3(4,3,2.2f)*(1-a/.14f),.9f,NativeBossVfx.Core);
            if(a<.6f)for(int i=0;i<fx.Scale(7);i++){float u=a/.6f;var d=NativeBossVfx.Cone(Vector3.UnitY,1.6f,H(seed,300+i),H(seed,310+i),H(seed,320+i));fx.Fire(at+d*(.25f*u+.05f),new Vector3(3,1.6f,.5f),.8f*(1-u),.28f+.3f*u,u,300+i,floor);}
            fx.SparkBurst(t,at,Vector3.UnitY,1.8f,6,22,.6f,r.Dead,.03f,seed,400,1.3f,.018f,.9f,true,floor);
            fx.SmokeBurst(t,at,new Vector3(0,.5f,0),.25f,.6f,6,1.4f,.25f,.9f,new Vector3(.06f,.06f,.065f),.4f,r.Dead+.05f,.2f,seed,500,glow:.3f);
            fx.RubbleBurst(t,at,Vector3.UnitY,1.4f,3.2f,9,Breakup,r.Dead,.06f,new Vector3(.06f,.065f,.075f),seed,600,.6f,floor);
            fx.RubbleBurst(t,at,Vector3.UnitY,1.2f,2.6f,4,Breakup,r.Dead,.035f,r.Accent*.6f+new Vector3(.05f,.05f,.05f),seed,700,1.4f,floor);
        }
    }
}
