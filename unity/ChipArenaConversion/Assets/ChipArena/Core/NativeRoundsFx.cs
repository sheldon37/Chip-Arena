using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One frame of the ROUNDS packs' bespoke visuals (NativeRoundsLooks, NativeRoundsFields, NativeRoundsUnits,
    /// NativeRoundsProcs): Runtime NativeRoundsFxPresentation feeds it from the arena's replicated state (seat chips, chip fields,
    /// toy minions and their deaths, events, shots, beams) or the DEV preview's stand-ins, then draws its outputs: the particles
    /// through the shared effects renderer (NativeBossVfxSubmit), the lit models (NativeVfxDraw.LitPieces), the field surfaces
    /// (Resources/NativeRoundsField.shader) and, off the low tier, the slow-time, gravity and repulsion distortion
    /// (Resources/NativeRoundsWarp.shader). Order: Begin, the inputs (events first, then fields, drones, deaths, beams, pilots,
    /// shots), End. A clock that runs back more than .25 s (the killcam, a new round) or another arena forgets everything, as the
    /// other presentations do. Allocation free per frame.
    /// </summary>
    public sealed class NativeRoundsFx
    {
        public readonly NativeBossVfx Particles=new NativeBossVfx(12000);
        public readonly RoundsSurface Ground=new RoundsSurface(48000,240000),Warp=new RoundsSurface(12000,64000);
        public readonly RoundsLit Lit=new RoundsLit();
        public readonly NativeRoundsFields Fields=new NativeRoundsFields();
        public readonly NativeRoundsUnits Units=new NativeRoundsUnits();
        public readonly NativeRoundsProcs Procs=new NativeRoundsProcs();
        public RoundsShots Shots=RoundsShots.Make(96);
        public readonly RoundsPilotInput[] Pilots=new RoundsPilotInput[16];public int PilotCount;
        public readonly RoundsBody[] Bodies=new RoundsBody[24];public int BodyCount;
        public float Clock{get;private set;}
        /// <summary>Nothing remembered or fading (the presenter can skip a frame with no pack chip held).</summary>
        public bool Idle=>Fields.Idle&&Units.Idle&&Procs.Idle;
        private float last=float.NaN;private double quality=1;private bool low;
        /// <summary>The frame's tier (checks): effects quality and the low tier.</summary>
        public double Quality=>quality;public bool Low=>low;
        public void Clear(){Fields.Clear();Units.Clear();Procs.Clear();Particles.Begin(quality,low);Ground.Clear();Warp.Clear();Lit.Clear();last=float.NaN;PilotCount=BodyCount=0;Shots.Count=0;}
        /// <summary>Starts a frame at `clock` (the field clock) for the effects quality and tier; `reset` (another arena or round)
        /// forgets everything.</summary>
        public void Begin(float clock,double effects,bool lowTier,bool reset=false)
        {
            if(reset||!float.IsNaN(last)&&clock<last-.25f)Clear();
            last=float.IsNaN(last)?clock:Math.Max(last,clock);Clock=clock;quality=double.IsNaN(effects)?1:Math.Max(0,Math.Min(1,effects));low=lowTier;
            PilotCount=BodyCount=0;Shots.Count=0;
        }
        /// <summary>A pilot this frame: its seat, presented body centre, aim yaw (rad), alive, and the looks its seat chips give.</summary>
        public void Pilot(int seat,Vector3 body,float aimYaw,bool alive,int satellites,RoundsProc procs,int stasis,float frozenUntil)
        {
            if(PilotCount<Pilots.Length)Pilots[PilotCount++]=new RoundsPilotInput{Seat=seat,Satellites=satellites,Body=body,AimYaw=aimYaw,Alive=alive};
            if(BodyCount<Bodies.Length)Bodies[BodyCount++]=new RoundsBody{Seat=seat,Body=body,Alive=alive,Procs=procs,Stasis=stasis,FrozenUntil=frozenUntil};
        }
        /// <summary>A stand-in body (the DEV preview's dummies) for the procs only.</summary>
        public void Dummy(int seat,Vector3 body){if(BodyCount<Bodies.Length)Bodies[BodyCount++]=new RoundsBody{Seat=seat,Body=body,Alive=true};}
        /// <summary>A shot inside any field reacts (slow-time echoes, gravity pull).</summary>
        public void Shot(Vector3 p,Vector3 v)=>Shots.Add(p,v);
        /// <summary>Draws the frame into the outputs.</summary>
        public void End()
        {
            Particles.Begin(quality,low);Ground.Clear();Warp.Clear();Lit.Clear();
            Fields.Draw(Clock,Particles,Ground,Warp,Lit,quality,low,Shots);
            Units.EndDrones(Clock);Units.DrawSatellites(Pilots,PilotCount,Clock,Particles,Lit);Units.DrawShots(Clock,Particles);Units.DrawDrones(Clock,Particles,Lit,low);
            Procs.Draw(Clock,Bodies,BodyCount,Particles,Lit,Ground,low);
        }
    }

    /// <summary>
    /// The DEV panel's ROUNDS effects preview (developers only; presentation only: nothing is spawned, damaged or sent): stand-in
    /// inputs for one entry on a loop round the local pilot, fed through the same NativeRoundsFx the live game uses, so it shows
    /// exactly what a match would. Entries: each field look (its authority radius and life, born every loop ahead of the pilot),
    /// each satellite look (on the pilot, firing a volley's two homing shots every .8 s), the Drone Nest cycle (three drones: one
    /// fights, one expires at its 6 s, one is shot down), and the procs and buffs in turn. The offline renders use these too.
    /// </summary>
    public static class NativeRoundsPreview
    {
        public static bool On;public static int Entry;
        /// <summary>Entry count: the field looks, the four satellite looks, the drones, the procs.</summary>
        public static int Count=>NativeRoundsLooks.Looks.Length+4+2;
        public static string Name(int e)
        {
            int f=NativeRoundsLooks.Looks.Length;if(e<f)return NativeRoundsLooks.Looks[e].Name+" field";e-=f;if(e<4)return NativeRoundsLooks.SatelliteNames[e]+" satellites";e-=4;
            return e==0?"Drone Nest drones":"Procs and buffs";
        }
        // The authority's radius and life of each field look (NativeChips fieldRules, ChipRoll, BlockProc, ChipDamaged).
        public static void FieldRule(int look,out float radius,out float life)
        {
            var l=NativeRoundsLooks.Looks[look];
            switch(l.Style)
            {
                case RoundsStyle.Fire:radius=3;life=3;return;case RoundsStyle.Frost:radius=3.5f;life=3;return;
                case RoundsStyle.Time:if(l.Variant>=2){radius=5;life=3;}else{radius=4;life=4;}return;
                case RoundsStyle.Push:if(l.Variant==1){radius=6;life=4;}else{radius=3.5f;life=3;}return;
                case RoundsStyle.Pull:radius=5;life=3.5f;return;default:radius=4;life=3;return;
            }
        }
        /// <summary>The loop length of an entry (s).</summary>
        public static float Loop(int e){int f=NativeRoundsLooks.Looks.Length;if(e<f){FieldRule(e,out _,out float life);return life+NativeRoundsFields.Fade(NativeRoundsLooks.Looks[e].Style)+1;}e-=f;return e<4?4:e==4?8.5f:14;}
        private const int Base=1<<29;
        /// <summary>
        /// Feeds entry `e` at time `t` (the presentation clock) for a pilot of seat `seat` whose body is at `body`, facing `yaw`
        /// (rad), standing on `floor`. Call between NativeRoundsFx.Begin and End (the live pilot inputs, if any, first).
        /// </summary>
        public static void Fill(NativeRoundsFx fx,int e,float t,int seat,Vector3 body,float yaw,float floor)
        {
            if(fx==null||e<0||e>=Count)return;float loop=Loop(e),k=(float)Math.Floor(t/loop),t0=k*loop;var fwd=BossMath.Forward(yaw);var right=BossMath.Right(yaw);int f=NativeRoundsLooks.Looks.Length;
            if(e<f)
            {
                FieldRule(e,out float radius,out float life);var l=NativeRoundsLooks.Looks[e];
                // A dodge or block field is born at a pilot's centre; an impact field where a shot hits a standing pilot.
                var c=new Vector3(body.X,floor+NativeRoundsFields.PilotCentre,body.Z)+fwd*(radius+3);
                fx.Fields.Observe(new RoundsFieldInput{Id=Base+e*4096+(int)k%4096,Look=e,Centre=c,Floor=floor,Radius=radius,Born=t0+.4f,Until=t0+.4f+life},t);
                if(l.Style==RoundsStyle.Time||l.Style==RoundsStyle.Pull)
                    for(int s=0;s<3;s++){float a=t-t0-.6f-s*.35f;if(a<0)continue;var from=c-fwd*(radius+2)+right*((s-1)*1.4f)+new Vector3(0,.4f,0);float speed=l.Style==RoundsStyle.Time&&Vector3.Distance(from+fwd*30*a,c)<radius?6:30;var p=from+fwd*(Math.Min(a,.25f)*30+Math.Max(0,a-.25f)*speed);if(Vector3.Distance(p,c)<radius*1.6f)fx.Shot(p,fwd*speed);}
                return;
            }
            e-=f;
            if(e<4)
            {
                // The satellites need the pilot input to carry the look: the live input for this seat is replaced.
                bool found=false;for(int i=0;i<fx.PilotCount;i++)if(fx.Pilots[i].Seat==seat){fx.Pilots[i].Satellites=e;found=true;}
                if(!found)fx.Pilot(seat,body,yaw,true,e,RoundsProc.None,0,0);
                for(int v=0;v<6;v++)
                {
                    float born=t0+.4f+v*.6f,a=t-born;if(a<0||a>1.2f)continue;
                    for(int side=-1;side<=1;side+=2)
                    {
                        var start=NativeRoundsLooks.SatelliteAt(body,born,side);var aim=body+new Vector3(0,.6f,0)+fwd*14+right*(side*1.5f);var dir=Vector3.Normalize(aim-start);
                        var curve=right*(-side*(float)Math.Sin(Math.Min(1,a*3))*.8f);var p=start+dir*(22*a)+curve*a;
                        fx.Units.Shot(new RoundsShotInput{Id=Base+(int)k*64+v*2+(side>0?1:0),Owner=seat,Look=e,Homing=true,P=p,V=dir*22,Born=born},t,fx.Pilots,fx.PilotCount);
                    }
                }
                return;
            }
            e-=4;var accent=new Vector3(.1f,.85f,.75f);
            if(e==0)
            {
                // Three drones summoned .3 s apart: A fights the dummy, B expires at its 6 s, C is shot down at 3.2 s.
                var dummy=body+fwd*6.5f;fx.Dummy(Base-1,dummy);
                for(int d=0;d<3;d++)
                {
                    int id=Base+(int)k*8+d;float spawn=t0+.3f+d*.3f,a=t-spawn;var home=body+fwd*2.2f+right*((d-1)*1.6f)+new Vector3(0,-.865f+.6f+.3f,0);
                    if(a<0)continue;if(a<.02f||t-spawn<1/30f)fx.Units.Summoned(id,seat,spawn,home-new Vector3(0,.6f,0));
                    float killAt=3.2f;bool dead=d==2&&a>=killAt,expired=d==1&&a>=NativeRoundsLooks.DroneLife;
                    if(dead){fx.Units.Summoned(id,seat,spawn,home);fx.Units.Died(id,spawn+killAt,home,yaw);continue;}
                    if(expired||a>=NativeRoundsLooks.DroneLife+.6f)continue;
                    var to=dummy-home;to.Y=0;float dyaw=(float)Math.Atan2(to.X,to.Z);float wander=(float)Math.Sin(a*1.3f+d)*.4f;var at=home+right*wander;
                    float lastAttack=a<1?-100:spawn+1+(float)Math.Floor((a-1)/1.1f)*1.1f;
                    fx.Units.Summoned(id,seat,spawn,home);
                    fx.Units.Observe(new RoundsDroneInput{Id=id,Owner=seat,At=at,Velocity=right*((float)Math.Cos(a*1.3f+d)*.52f),Yaw=dyaw,LastAttack=lastAttack,LastHit=d==2&&a>2.4f?spawn+2.4f+(float)Math.Floor((a-2.4f)/.4f)*.4f:-100,Health=1,Accent=accent},t);
                    float strike=lastAttack+NativeRoundsLooks.DroneWindup;if(t>=strike&&t<strike+.22f)fx.Units.Beam(new RoundsBeamInput{From=at+new Vector3(0,NativeRoundsLooks.DroneMuzzle,0),To=dummy+new Vector3(0,.35f,0),Until=strike+.22f},t);
                }
                return;
            }
            // Procs in turn, one every 1.4 s, on the pilot and a dummy ahead of it.
            var dm=body+fwd*4;fx.Dummy(Base-2,dm);var all=RoundsProc.Phase|RoundsProc.Packet|RoundsProc.Grapple|RoundsProc.Tractor|RoundsProc.Stasis|RoundsProc.Execute|RoundsProc.Reclaim|RoundsProc.Transit|RoundsProc.GuardPulse|RoundsProc.ArcFault|RoundsProc.Fragment|RoundsProc.Thorns|RoundsProc.BlockHeal|RoundsProc.Regen|RoundsProc.ReloadHeal|RoundsProc.Vent;
            for(int s=0;s<10;s++)
            {
                float at=t0+.3f+s*1.4f;if(t<at)break;int id=Base+(int)k*32+s;var e2=new ArenaEvent{Id=id,Source=seat,Target=Base-2,Time=at,Position=new V3(dm.X,dm.Y+.3f,dm.Z),From=new V3(body.X,body.Y,body.Z)};
                switch(s)
                {
                    case 0:e2.Kind=EventKind.Block;e2.Position=new V3(body.X,body.Y,body.Z);break;
                    case 1:e2.Kind=EventKind.Hit;e2.Detail="direct";e2.Amount=12;fx.Procs.Event(e2,RoundsProc.Packet,true,false,0,floor);continue;
                    case 2:e2.Kind=EventKind.Hit;e2.Detail="direct";e2.Amount=12;fx.Procs.Event(e2,RoundsProc.Grapple,true,false,0,floor);e2.Id+=1<<20;fx.Procs.Event(e2,RoundsProc.Execute,true,true,0,floor);continue;
                    case 3:e2.Kind=EventKind.Freeze;e2.Amount=1.2f;break;
                    case 4:e2.Kind=EventKind.Teleport;e2.From=new V3(body.X-fwd.X*6,body.Y,body.Z-fwd.Z*6);e2.Position=new V3(body.X,body.Y,body.Z);fx.Procs.Event(e2,RoundsProc.Reclaim,false,false,0,floor);continue;
                    case 5:e2.Kind=EventKind.BlockPulse;e2.Radius=3;e2.Position=new V3(body.X,body.Y,body.Z);break;
                    case 6:e2.Kind=EventKind.Explosion;e2.Detail="static";e2.Radius=3;break;
                    case 7:e2.Kind=EventKind.Explosion;e2.Detail="shrapnel";e2.Radius=1.8f;break;
                    case 8:e2.Kind=EventKind.Hit;e2.Detail="thorns";e2.Target=Base-2;e2.Amount=4;break;
                    default:e2.Kind=EventKind.Teleport;e2.From=new V3(body.X,body.Y,body.Z);e2.Position=new V3(dm.X,dm.Y,dm.Z);fx.Procs.Event(e2,RoundsProc.Transit,false,false,0,floor);continue;
                }
                fx.Procs.Event(e2,all&~(RoundsProc.Reclaim|RoundsProc.Transit|RoundsProc.Packet|RoundsProc.Grapple|RoundsProc.Tractor|RoundsProc.Execute),true,false,s==3?(int)k%3:0,floor);
            }
            // Regeneration through the loop's second half.
            if(t-t0>7){var heal=new ArenaEvent{Id=Base+(int)k*32+31,Kind=EventKind.Heal,Source=seat,Target=seat,Time=t,Amount=.07f};fx.Procs.Event(heal,RoundsProc.Regen,false,false,0,floor);}
        }
    }
}
