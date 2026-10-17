using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What a station add's motion reads this frame beyond the gait's input (all presentation: the replicated
    /// LastAttack and NativeStationView, the presentation clock). Ages are seconds since the event, NaN when it is not on.
    /// Points are in the rig's source frame (the root at the body, +x the add's left, +z forward, rig metres).</summary>
    public struct StationMotionInput
    {
        public int Kind,Id;public uint Seed;public float Time,Dt;
        /// <summary>The special LastAttack started and its age (NaN: none showing); the content's melee age (NaN: none).</summary>
        public NativeStationKinds.Special Special;public float SpecialAge,MeleeAge,MeleeStrike;public NativeHordeKinds.Attack Melee;
        /// <summary>The gait's smoothed ground speed (rig m/s) and the body's velocity (rig frame).</summary>
        public float Speed;public Vector3 Velocity;
        public bool Grounded,LanternBroken,Submerged,Dying;
        /// <summary>Since the last call, the surfacing, the grab's landing (while held) and the release.</summary>
        public float LastCallAge,SurfaceAge,HoldAge,ReleaseAge;
        /// <summary>Where the arc or the grab aims (the target pilot's chest), and the held pilot's ankle.</summary>
        public bool HasAim,HasHold;public Vector3 Aim,Hold;
    }
    /// <summary>The overlay's results: how far the drawn root sinks (rig m), the body's glow scale, the telegraph's strength,
    /// and the kind's prop (the lantern or the door) in the rig's Unity-axes frame (x the add's right), with its visibility.</summary>
    public struct StationMotionOutput
    {
        public float Sink,Glow,Charge;public bool Prop;public NativeHordeModels.StationProp PropKind;public Vector3 PropAt;public Quaternion PropTurn;
        /// <summary>The joint that holds the prop (its hand) for the gore: a lost hand drops it.</summary>
        public int PropJoint;
    }

    /// <summary>
    /// The Central Station kinds' animation over the shared gait (Core/NativeHordeMotion poses the walk, run, idle, melee, hits
    /// and reels on the kinds' own takes; this lays each kind's own set on top). Presentation only: the authority's LastAttack
    /// and NativeStationView drive it, nothing is read back, and no hit timing, hitbox, damage, cooldown, movement or network
    /// state changes (the special's strike lands at LastAttack + its wind-up, as the authority's own).
    /// <list type="bullet">
    /// <item>Linesman: an electric twitch now and then (the pack's on-fire flail laid lightly over the upper body, more often
    /// grounded); the arc: the coil arm raised and trembling over the pack's reach for 1.1 s, a .25 s whip toward the target,
    /// the arm back; grounded: a kneel gripping the rail (the pack's stand-to-crawl held) for .8 s; the electrocuted collapse.</item>
    /// <item>Conductor: the lantern carried out from the left hip and swinging as a pendulum off the hand; the whistle: the
    /// right hand to the mouth, the chest heaving in for .8 s, the blast; the last call: the lantern raised high and rung for
    /// 1.2 s; the fall on the pack's forward death, the lantern dropping and shattering.</item>
    /// <item>Undertow: submerged, the pack's lunging crawl with only the head above the waterline; surfacing on the
    /// crawl-to-stand at 1.3 times; the grab's lunge (the gait's); the drag on the belly crawl played backward with a hand on
    /// the held pilot's ankle; the release's reel; the death that falls and sinks 1 m.</item>
    /// <item>Pileup: hunched behind its carriage door, both hands on it; the shove: braced back with the door's foot scraping
    /// the floor for .7 s, then thrust on the pack's two-handed push; the crush lifts the door overhead and slams it down with
    /// the hands; the split death: the body falls onto the door laid flat.</item>
    /// </list>
    /// Fixed fields and scratch poses: no allocation per frame (every clip decoded once).
    /// </summary>
    public sealed class NativeStationMotion
    {
        // ---- clips (NativeZombieClips keys; tools/export_pack_zombie.mjs exports the station's) ----
        private static int writhe=-1,deathShock,idle1,reach1,crawl1,crawl2,shove1,standCrawl,crawlStand,deathFront1,deathFront2,knock4,swipeR1;
        public static void Warm()
        {
            if(writhe>=0)return;NativeZombieMotion.Warm();int N(string k)=>NativeZombieClips.Need(k);
            deathShock=N("death-shock");idle1=N("idle-1");reach1=N("reach-1");crawl1=N("crawl-1");crawl2=N("crawl-2");shove1=N("shove-1");standCrawl=N("stand-crawl");crawlStand=N("crawl-stand");
            deathFront1=N("death-front-1");deathFront2=N("death-front-2");knock4=N("knock-4");swipeR1=N("swipe-r1");writhe=N("writhe-1");
        }
        // ---- per add ----
        private float groundedAt=float.NaN,swimPhase,dragPhase,last=float.NaN;private bool wasGrounded;private int id=int.MinValue;
        // The lantern's pendulum (rad about the hand's x and z, their rates) and the hand's last place and velocity (rig frame).
        private Vector2 swing,swingRate;private Vector3 handWas,handVel;private bool handKnown;
        private readonly CapturedRig.Pose clip=new CapturedRig.Pose(),scratch=new CapturedRig.Pose();
        private readonly float[] row=new float[NativeZombieClips.Channels];
        private static readonly float[] retimeAt=new float[4],retimeMarks=new float[4];
        /// <summary>Forgets the add (a new arena, a rewind, a jump).</summary>
        public void Reset(){id=int.MinValue;last=float.NaN;}

        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float C01(float x)=>x<0?0:x>1?1:x;
        private static bool On(float age)=>!float.IsNaN(age)&&age>=0;
        private float H(int k)=>NativeHordeKinds.Hash(seedNow,200+k);private uint seedNow;

        /// <summary>Lays the kind's own motion over the gait's pose `p` (source frame, root at the body) and fills `o`.</summary>
        public void Pose(in StationMotionInput f,CapturedRig.Pose p,ref StationMotionOutput o)
        {
            Warm();o=new StationMotionOutput{Glow=1,PropTurn=Quaternion.Identity};seedNow=f.Seed;
            float dt=float.IsNaN(last)?0:f.Time-last;if(id!=f.Id||dt<0||dt>.5f){id=f.Id;groundedAt=float.NaN;wasGrounded=f.Grounded;swing=swingRate=Vector2.Zero;handKnown=false;swimPhase=H(0);dragPhase=H(1);dt=0;}
            last=f.Time;dt=Math.Min(dt,.1f);
            switch(f.Kind)
            {
                case NativeStationKinds.Linesman:Linesman(f,p,ref o,dt);break;
                case NativeStationKinds.Conductor:Conductor(f,p,ref o,dt);break;
                case NativeStationKinds.Undertow:Undertow(f,p,ref o,dt);break;
                case NativeStationKinds.Pileup:Pileup(f,p,ref o);break;
            }
            p.World();
        }

        // ================================================================================================================
        // Linesman
        // ================================================================================================================
        /// <summary>The arc's phases at age a: the wind-up's strength (0..1, held), the whip (0..1 across .25 s from the strike),
        /// the whole weight (back to the gait by the end).</summary>
        public static void ArcCurve(float a,out float wind,out float whip,out float weight)
        {
            wind=whip=weight=0;if(float.IsNaN(a)||a<0)return;float W=NativeStationKinds.ArcWindup,end=W+NativeStationKinds.ArcRecover;if(a>=end)return;
            wind=a<W?Sm(a/.3f):1-Sm((a-W)/NativeStationKinds.ArcWhip);whip=a<W?0:Sm((a-W)/NativeStationKinds.ArcWhip);
            weight=a<W?Sm(a/.18f):1-Sm((a-W-NativeStationKinds.ArcWhip)/(end-W-NativeStationKinds.ArcWhip));
        }
        private void Linesman(in StationMotionInput f,CapturedRig.Pose p,ref StationMotionOutput o,float dt)
        {
            // The electric twitch: a short burst of the flail every few seconds (twice as often grounded).
            float period=f.Grounded?1.6f:3.4f,cyc=(f.Time+period*H(2))/period;int n=(int)Math.Floor(cyc);float u=(cyc-n)*period,len=.5f+.25f*(float)NativeGoreRules.Hash01(f.Seed,n,210);
            bool arcing=f.Special==NativeStationKinds.Special.Arc&&On(f.SpecialAge);
            if(u<len&&!arcing&&!f.Dying)
            {
                float env=(float)Math.Sin(Math.PI*u/len)*(f.Grounded?.42f:.3f);NativeZombieClips.Sample(writhe,.15f+u*1.4f,row);NativeZombieClips.Pose(row,clip);Upper(p,clip,env,false);
                Bend(p,CapturedRig.Head,.25f*env*(float)Math.Sin(f.Time*47),.3f*env*(float)Math.Sin(f.Time*31),.2f*env);
            }
            // Grounded: the kneel at the moment he grips the rail (not while walking off or attacking), the brighter glow.
            if(f.Grounded&&!wasGrounded)groundedAt=f.Time;wasGrounded=f.Grounded;if(!f.Grounded)groundedAt=float.NaN;
            float g=f.Grounded?f.Time-groundedAt:float.NaN;o.Glow=f.Grounded?NativeStationKinds.GroundedGlow:1;
            if(!float.IsNaN(g)&&g<NativeStationKinds.GroundKneel+.35f&&f.Speed<.6f&&!arcing&&float.IsNaN(f.MeleeAge))
            {
                float k=Sm(g/.25f)*(1-Sm((g-NativeStationKinds.GroundKneel)/.35f));NativeZombieClips.Sample(standCrawl,.3f+.36f*Sm(g/.4f),row);NativeZombieClips.Pose(row,clip);Whole(p,clip,k);
            }
            // The arc: the coil arm (his right: source -x) raised toward the target, trembling, then whipped at it.
            if(arcing)
            {
                ArcCurve(f.SpecialAge,out float wind,out float whip,out float weight);if(weight<=0)return;
                float a=f.SpecialAge,charge=C01(a/NativeStationKinds.ArcWindup);o.Charge=a<NativeStationKinds.ArcWindup?charge:Math.Max(0,1-(a-NativeStationKinds.ArcWindup)/.3f);o.Glow*=1+.9f*o.Charge;
                NativeZombieClips.Sample(reach1,.6f+.5f*charge,row);NativeZombieClips.Pose(row,clip);Upper(p,clip,.35f*weight*(1-whip),true);
                p.World();var aim=f.HasAim?f.Aim:new Vector3(0,.6f,4);var shoulder=p.WorldPosition[CapturedRig.RArm];var dir=Vector3.Normalize(new Vector3(aim.X-shoulder.X,0,Math.Max(.3f,aim.Z-shoulder.Z)));
                float tremor=(.006f+.03f*charge*charge)*wind;var shake=new Vector3((float)Math.Sin(a*53+1.3f),(float)Math.Sin(a*61+.4f),(float)Math.Sin(a*47+2.2f))*tremor;
                var raised=shoulder+dir*.18f+new Vector3(0,.44f,0)+shake;var thrust=shoulder+dir*.5f+new Vector3(0,.02f,0);var hand=Vector3.Lerp(raised,thrust,whip);
                Bend(p,CapturedRig.Spine,(-.14f*wind+.26f*whip)*weight,0,0);Bend(p,CapturedRig.Chest,(-.1f*wind+.2f*whip)*weight,-.25f*wind*weight,0);Bend(p,CapturedRig.Head,-.18f*wind*weight,0,0);
                ArmTo(p,false,hand,new Vector3(-.6f,.5f,-.4f),weight);
                // The free hand claws open at his side, braced.
                p.World();var left=p.WorldPosition[CapturedRig.LArm]+new Vector3(.12f,-.38f,.12f);ArmTo(p,true,left,new Vector3(.7f,-.5f,-.2f),.5f*weight*wind);
            }
        }

        // ================================================================================================================
        // Conductor
        // ================================================================================================================
        /// <summary>The whistle at age a: the inhale (0..1, held to the blast), the blast (1 at .8 s, decaying), the weight.</summary>
        public static void WhistleCurve(float a,out float inhale,out float blast,out float weight)
        {
            inhale=blast=weight=0;if(float.IsNaN(a)||a<0)return;float W=NativeStationKinds.WhistleWindup,end=NativeStationKinds.SpecialEnd(NativeStationKinds.Special.Whistle);if(a>=end)return;
            inhale=a<W?Sm(a/W):1-Sm((a-W)/.12f);blast=a<W?0:1-Sm((a-W-.1f)/.35f);weight=a<W?Sm(a/.2f):1-Sm((a-W-.25f)/(end-W-.25f));
        }
        /// <summary>The last call at age a: the lantern's raise (0..1), the ringing shake (rad), the weight.</summary>
        public static void LastCallCurve(float a,out float raise,out float ring,out float weight)
        {
            raise=ring=weight=0;if(float.IsNaN(a)||a<0)return;float T=NativeStationKinds.LastCallTelegraph;if(a>=T+.45f)return;
            raise=Sm(a/.3f)*(1-Sm((a-T)/.45f));ring=(float)Math.Sin(a*Math.PI*2*4.5f)*Sm((a-.25f)/.15f)*(1-Sm((a-T+.1f)/.2f));weight=raise;
        }
        private void Conductor(in StationMotionInput f,CapturedRig.Pose p,ref StationMotionOutput o,float dt)
        {
            float melee=float.IsNaN(f.MeleeAge)?0:1;p.World();
            // The lantern carried out from the left hip (his left: source +x), the elbow bent, steadier than a free arm.
            var carry=p.WorldPosition[CapturedRig.LArm]+new Vector3(.1f,-.36f,.2f);
            WhistleCurve(f.Special==NativeStationKinds.Special.Whistle?f.SpecialAge:float.NaN,out float inhale,out float blast,out float ww);
            LastCallCurve(f.LastCallAge,out float raise,out float ring,out float lw);
            if(f.LanternBroken)lw=0;
            // The last call: the lantern high over his head, shaken side to side (rung), his face up toward it.
            var high=p.WorldPosition[CapturedRig.LArm]+new Vector3(.05f+.07f*ring,.5f,.12f);var lantern=Vector3.Lerp(carry,high,raise);
            if(lw>0){NativeZombieClips.Sample(reach1,.9f+.3f*raise,row);NativeZombieClips.Pose(row,clip);Upper(p,clip,.25f*lw,true);Bend(p,CapturedRig.Chest,-.12f*raise,.1f*raise,-.06f*ring);Bend(p,CapturedRig.Head,-.35f*raise,.15f*raise,0);}
            ArmTo(p,true,lantern,new Vector3(.8f,-.5f,-.3f),Math.Max(lw,.62f*(1-melee)));
            // The whistle: the right hand to the mouth, the chest drawn in (shoulders up, head back), then the blast.
            if(ww>0&&!f.LanternBroken)
            {
                Bend(p,CapturedRig.Spine,-.1f*inhale*ww+.12f*blast*ww,0,0);Bend(p,CapturedRig.Chest,-.14f*inhale*ww+.16f*blast*ww,0,0);
                Bend(p,CapturedRig.LClav,0,0,.12f*inhale*ww);Bend(p,CapturedRig.RClav,0,0,-.12f*inhale*ww);Bend(p,CapturedRig.Head,(-.2f*inhale+.22f*blast)*ww,0,0);
                p.World();var mouth=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(new Vector3(0,.075f,.2f),p.WorldRotation[CapturedRig.Head]);
                ArmTo(p,false,mouth+new Vector3(-.02f,-.03f,.02f),new Vector3(-.9f,-.2f,-.3f),ww);
            }
            // The lantern's pendulum off the hand: driven by the hand's acceleration (the body's own velocity added), damped.
            p.World();var hand=p.WorldPosition[CapturedRig.LHand]+Vector3.Transform(new Vector3(0,-.1f,.02f),p.WorldRotation[CapturedRig.LHand]);
            if(dt>1e-4f)
            {
                var vel=(handKnown?(hand-handWas)/dt:Vector3.Zero)+f.Velocity;var acc=handKnown?(vel-handVel)/dt:Vector3.Zero;handVel=vel;
                acc=Vector3.Clamp(acc,new Vector3(-25),new Vector3(25));const float L=.22f,G=9.8f;
                // About x: swings forward and back (driven by z acceleration); about z: side to side (x acceleration).
                swingRate.X+=(-(G/L)*(float)Math.Sin(swing.X)+acc.Z/L-2.2f*swingRate.X)*dt;swingRate.Y+=(-(G/L)*(float)Math.Sin(swing.Y)-acc.X/L-2.2f*swingRate.Y)*dt;
                swing+=swingRate*dt;swing=Vector2.Clamp(swing,new Vector2(-1.1f),new Vector2(1.1f));
            }
            handWas=hand;handKnown=true;
            // In Unity axes (x mirrored): the lantern hangs straight down, turned by the pendulum.
            o.Prop=true;o.PropKind=f.LanternBroken?NativeHordeModels.StationProp.BrokenLantern:NativeHordeModels.StationProp.Lantern;o.PropJoint=CapturedRig.LHand;
            o.PropAt=new Vector3(-hand.X,hand.Y,hand.Z);o.PropTurn=CapturedRig.Euler(swing.X,0,-swing.Y);
            o.Charge=Math.Max(inhale*ww,raise);o.Glow=1+.6f*Math.Max(inhale*ww,.7f*raise)+1.2f*blast*ww;
        }
        /// <summary>The lantern's light (its flare: brighter in the whistle's inhale and the last call, flashing as it is rung).</summary>
        public static float LanternFlare(float whistleAge,float lastCallAge)
        {
            WhistleCurve(whistleAge,out float inhale,out float blast,out float ww);LastCallCurve(lastCallAge,out float raise,out float ring,out _);
            return 1+.8f*inhale*ww+1.4f*blast*ww+raise*(.7f+.5f*Math.Abs(ring));
        }

        // ================================================================================================================
        // Undertow
        // ================================================================================================================
        /// <summary>How long surfacing plays (the crawl-to-stand at 1.3 times).</summary>
        public static float SurfaceSeconds{get{Warm();return NativeZombieClips.Duration(crawlStand)/NativeStationKinds.SurfaceRate;}}
        /// <summary>The eyes' height over the floor when submerged (rig m): just above the flood's .35 m.</summary>
        public const float WaterEyes=.4f;
        private void Undertow(in StationMotionInput f,CapturedRig.Pose p,ref StationMotionOutput o,float dt)
        {
            float surf=f.SurfaceAge,surfDur=SurfaceSeconds;bool surfacing=On(surf)&&surf<surfDur;
            if(f.Submerged&&!surfacing)
            {
                // The swim: one lunging crawl stroke looped (a seamless cycle: the stroke and its reverse mixed at the seam), its
                // pace from the speed; only the head stays above the water.
                swimPhase+=dt*(.35f+.9f*Math.Min(3,f.Speed))/NativeZombieClips.Duration(crawl2);LoopSample(crawl2,swimPhase);NativeZombieClips.Pose(row,clip);Whole(p,clip,1);
                p.World();float eyes=p.WorldPosition[CapturedRig.Head].Y+.17f;o.Sink=Math.Max(0,eyes-(NativeHordeGait.Floor+WaterEyes));o.Glow=1.25f;return;
            }
            if(surfacing)
            {
                // Surfacing: up out of the water on the crawl-to-stand, handing back to the gait over its last .3 s.
                float k=1-Sm((surf-(surfDur-.3f))/.3f);NativeZombieClips.Sample(crawlStand,surf*NativeStationKinds.SurfaceRate,row);NativeZombieClips.Pose(row,clip);Whole(p,clip,k);
                p.World();o.Sink=Math.Max(0,(1-Sm(surf/(surfDur*.5f)))*.35f);return;
            }
            if(On(f.HoldAge))
            {
                // The drag: the belly crawl played backward (it hauls itself back toward the water), a claw on the held pilot.
                dragPhase-=dt*1.1f/NativeZombieClips.Duration(crawl1);LoopSample(crawl1,dragPhase);NativeZombieClips.Pose(row,clip);float k=Sm(f.HoldAge/.2f);Whole(p,clip,k);
                if(f.HasHold){p.World();var hold=f.Hold;var sh=p.WorldPosition[CapturedRig.RArm];var to=hold-sh;float l=to.Length();if(l>.5f)hold=sh+to*(.5f/l);ArmTo(p,false,hold,new Vector3(-.5f,.6f,-.3f),k);
                    var sh2=p.WorldPosition[CapturedRig.LArm];var to2=f.Hold+new Vector3(.12f,0,0)-sh2;float l2=to2.Length();ArmTo(p,true,sh2+to2*(Math.Min(.5f,l2)/Math.Max(1e-3f,l2)),new Vector3(.5f,.6f,-.3f),.8f*k);}
                return;
            }
            if(On(f.ReleaseAge)&&f.ReleaseAge<.7f)
            {
                // The release: thrown back on a knock-back reel.
                float k=Sm(f.ReleaseAge/.08f)*(1-Sm((f.ReleaseAge-.35f)/.35f));NativeZombieClips.Sample(knock4,.05f+f.ReleaseAge*.9f,row);NativeZombieClips.Pose(row,clip);Upper(p,clip,.85f*k,true);
            }
        }
        // A loop of a one-shot take (the station's crawls): the take and its own start mixed over the last fifth, so the cycle
        // closes without a pop.
        private void LoopSample(int c,float phase)
        {
            float d=NativeZombieClips.Duration(c),u=phase-(float)Math.Floor(phase),t=u*d,blend=.2f*d;NativeZombieClips.Sample(c,t,row);
            if(t>d-blend){var other=scratchRow;NativeZombieClips.Sample(c,t-(d-blend),other);NativeZombieClips.Blend(row,other,Sm((t-(d-blend))/blend),row);}
        }
        private readonly float[] scratchRow=new float[NativeZombieClips.Channels];

        // ================================================================================================================
        // Pileup
        // ================================================================================================================
        /// <summary>The shove at age a: the brace (0..1, held), the thrust (1 at the strike, .7 s), the weight; and the clip time
        /// of the pack's push (its braced crouch held through the wind-up, the push to full extension exactly at the strike).</summary>
        public static void ShoveCurve(float a,out float brace,out float thrust,out float weight,out float clipTime)
        {
            brace=thrust=weight=0;clipTime=0;if(float.IsNaN(a)||a<0)return;float W=NativeStationKinds.ShoveWindup,end=W+NativeStationKinds.ShoveRecover;if(a>=end)return;
            Warm();var c=NativeZombieClips.Clip(shove1);retimeAt[0]=0;retimeAt[1]=W-.16f;retimeAt[2]=W;retimeAt[3]=end-.1f;for(int i=0;i<4;i++)retimeMarks[i]=c.Marks[i];
            clipTime=CapturedClip.Retime(retimeAt,retimeMarks,a);
            brace=a<W-.16f?Sm(a/.3f):1-Sm((a-(W-.16f))/.16f);thrust=a<W-.16f?0:a<W?Sm((a-(W-.16f))/.16f):1-Sm((a-W-.12f)/.45f);weight=a<W?Sm(a/.15f):1-Sm((a-W-.3f)/(end-W-.3f));
        }
        /// <summary>The door's carry in the chest's frame (source axes): in front of the chest, its foot near the floor.</summary>
        public static readonly Vector3 DoorCarry=new Vector3(0,-.42f,.42f);
        private void Pileup(in StationMotionInput f,CapturedRig.Pose p,ref StationMotionOutput o)
        {
            // Hunched low behind the door.
            Bend(p,CapturedRig.Spine,.12f,0,0);Bend(p,CapturedRig.Chest,.08f,0,0);Bend(p,CapturedRig.Head,-.15f,0,0);
            bool shoving=f.Special==NativeStationKinds.Special.Shove&&On(f.SpecialAge);float brace=0,thrust=0,sw=0;
            if(shoving){ShoveCurve(f.SpecialAge,out brace,out thrust,out sw,out float ct);NativeZombieClips.Sample(shove1,ct,row);NativeZombieClips.Pose(row,clip);Upper(p,clip,.55f*sw,true);
                Bend(p,CapturedRig.Spine,(-.22f*brace+.3f*thrust)*sw,0,0);var b=p.Position[CapturedRig.Body];b.Y-=.07f*brace*sw;b.Z+=(-.06f*brace+.12f*thrust)*sw;p.Position[CapturedRig.Body]=b;}
            p.World();
            // The door: carried on the chest (its yaw only, so it stays upright), dropped to scrape in the brace, thrust in the
            // shove; in the melee crush it follows the hands up overhead and down (the hands then lead, not the door).
            var chestQ=p.WorldRotation[CapturedRig.Chest];var fw=Vector3.Transform(Vector3.UnitZ,chestQ);float yaw=(float)Math.Atan2(fw.X,fw.Z);var yawQ=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);
            var door=p.WorldPosition[CapturedRig.Chest]+Vector3.Transform(DoorCarry,yawQ);float tilt=.06f;
            if(sw>0){door+=Vector3.Transform(new Vector3(0,-.06f*brace+.08f*thrust,-.1f*brace+.42f*thrust),yawQ)*sw;tilt+=(-.16f*brace+.1f*thrust)*sw;}
            float crush=0;
            if(!float.IsNaN(f.MeleeAge)&&f.Melee==NativeHordeKinds.Attack.Smash)
            {
                var hands=(p.WorldPosition[CapturedRig.LHand]+p.WorldPosition[CapturedRig.RHand])*.5f;var rest=p.WorldPosition[CapturedRig.Chest]+Vector3.Transform(new Vector3(0,-.25f,.42f),yawQ);
                NativeHordeGait.Channels(f.MeleeAge,f.MeleeStrike,f.MeleeStrike+.9f,NativeHordeKinds.Attack.Smash,out _,out _,out _,out _,out crush);
                door+=(hands-rest)*crush;tilt+=-.25f*crush*C01((hands.Y-rest.Y)/.4f);
            }
            // Never into the floor: the door's foot scrapes at most at the floor's height.
            float foot=door.Y-NativeHordeModels.DoorHeight*.5f;if(foot<NativeHordeGait.Floor+.02f)door.Y+=NativeHordeGait.Floor+.02f-foot;
            var turn=yawQ*Quaternion.CreateFromAxisAngle(Vector3.UnitX,tilt);
            // The hands on the door's edges (inside face), except while the crush's hands lead.
            float hold=1-crush;
            if(hold>0)
            {
                float half=NativeHordeModels.DoorWidth*.5f-.05f;var l=door+Vector3.Transform(new Vector3(half,.02f,-.07f),turn);var r=door+Vector3.Transform(new Vector3(-half,-.06f,-.07f),turn);
                ArmTo(p,true,l,new Vector3(.8f,-.4f,-.3f),hold);ArmTo(p,false,r,new Vector3(-.8f,-.4f,-.3f),hold);
            }
            o.Prop=true;o.PropKind=NativeHordeModels.StationProp.Door;o.PropJoint=CapturedRig.LHand;o.PropAt=new Vector3(-door.X,door.Y,door.Z);o.PropTurn=CapturedRig.Mirror(turn);
            o.Charge=Math.Max(brace,thrust)*sw;
        }

        // ================================================================================================================
        // Deaths (the bodies' falls before the shared char and burn-away, NativeMinionFx)
        // ================================================================================================================
        /// <summary>
        /// A station kind's death at `age` s (0..NativeMinionFx.DeathSeconds): the Linesman's electrocuted convulsions and collapse
        /// (the head down by 1.05 s), the Conductor's forward fall, the Undertow's fall into a crawl and its sinking (`sink`, rig m),
        /// the Pileup's fall forward. The pose is kept on the floor (before the sink). False: not a station kind.
        /// </summary>
        public static bool Death(CapturedRig.Pose p,int kind,uint seed,float age,out float sink)
        {
            sink=0;if(!NativeStationKinds.Is(kind))return false;Warm();float a=Math.Max(0,age);int c;float t;
            switch(kind)
            {
                case NativeStationKinds.Linesman:{c=deathShock;var m=NativeZombieClips.Clip(c).Marks;t=a<.62f?.12f+a*2.2f:a<1.05f?1.48f+(a-.62f)/.43f*(m[1]-1.48f):m[1]+(m[2]-m[1])*C01((a-1.05f)/1.25f);break;}
                case NativeStationKinds.Conductor:{c=deathFront2;var m=NativeZombieClips.Clip(c).Marks;t=a<NativeHordeGait.DownAt?a/NativeHordeGait.DownAt*m[1]:m[1]+(m[2]-m[1])*C01((a-NativeHordeGait.DownAt)/1.3f);break;}
                case NativeStationKinds.Undertow:{c=standCrawl;t=.35f+Math.Min(a,.95f)*1.45f;sink=NativeStationKinds.DeathSink*Sm((a-.3f)/NativeStationKinds.DeathSinkSeconds);break;}
                default:{c=deathFront1;var m=NativeZombieClips.Clip(c).Marks;t=a<NativeHordeGait.DownAt?a/NativeHordeGait.DownAt*m[1]:m[1]+(m[2]-m[1])*C01((a-NativeHordeGait.DownAt)/1.3f);break;}
            }
            NativeZombieClips.Sample(c,t,deathRow);NativeZombieClips.Pose(deathRow,p);float low=p.Lowest();var b=p.Position[CapturedRig.Body];b.Y+=NativeHordeGait.Floor-low;p.Position[CapturedRig.Body]=b;p.World();return true;
        }
        private static readonly float[] deathRow=new float[NativeZombieClips.Channels];
        /// <summary>The Pileup's door in its death (Unity-axes rig frame, the death pose's root): it tips forward off the falling
        /// body about its foot and lies flat, its window down, by .5 s, a small bounce.</summary>
        public static void DoorFall(float age,out Vector3 at,out Quaternion turn)
        {
            float u=C01(age/.5f),fall=u*u,bounce=age>.5f&&age<.75f?(float)Math.Sin((age-.5f)/.25f*Math.PI)*.06f:0;float ang=fall*(float)Math.PI*.5f-bounce;
            float h=NativeHordeModels.DoorHeight*.5f,t=NativeHordeModels.DoorThick*.5f;var foot=new Vector3(0,NativeHordeGait.Floor,DoorCarry.Z+.08f);
            turn=Quaternion.CreateFromAxisAngle(Vector3.UnitX,ang);at=foot+Vector3.Transform(new Vector3(0,h,t),turn);
        }
        /// <summary>The Conductor's lantern dropped from his hand at death: it falls to the floor by .42 s and tips over (it shatters
        /// there: <see cref="LanternShatterAt"/>). `from`: the hand's place at death (Unity-axes rig frame).</summary>
        public static void LanternFall(float age,Vector3 from,out Vector3 at,out Quaternion turn)
        {
            float u=C01(age/LanternShatterAt);var floor=NativeHordeGait.Floor+NativeHordeModels.LanternDrop+NativeHordeModels.LanternHalf+.04f;
            at=new Vector3(from.X+.12f*u,from.Y+(floor-from.Y)*u*u,from.Z+.18f*u);float tip=C01((age-LanternShatterAt)/.25f);turn=CapturedRig.Euler(.25f*u+1.3f*tip,0,.4f*tip);
            if(tip>0)at.Y-=.18f*tip;
        }
        public const float LanternShatterAt=.42f;

        // ================================================================================================================
        // Helpers: blends of a take's pose and arm reaches (all in the source frame)
        // ================================================================================================================
        private static readonly int[] upperJoints={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        /// <summary>The take's trunk, head and arms over the pose by k (and its pelvis when `pelvis`).</summary>
        private static void Upper(CapturedRig.Pose p,CapturedRig.Pose q,float k,bool pelvis)
        {
            if(k<=0)return;k=Math.Min(1,k);foreach(int j in upperJoints)p.Rotation[j]=Quaternion.Slerp(p.Rotation[j],q.Rotation[j],k);
            if(pelvis)p.Rotation[CapturedRig.Pelvis]=Quaternion.Slerp(p.Rotation[CapturedRig.Pelvis],q.Rotation[CapturedRig.Pelvis],k*.5f);
        }
        /// <summary>The whole take over the pose by k: every joint and the body's place (legs too: kneels, crawls, the surfacing).</summary>
        private static void Whole(CapturedRig.Pose p,CapturedRig.Pose q,float k)
        {
            if(k<=0)return;k=Math.Min(1,k);for(int j=1;j<p.Rotation.Length;j++)p.Rotation[j]=Quaternion.Slerp(p.Rotation[j],q.Rotation[j],k);
            p.Position[CapturedRig.Body]=Vector3.Lerp(p.Position[CapturedRig.Body],q.Position[CapturedRig.Body],k);
        }
        private static void Bend(CapturedRig.Pose p,int bone,float x,float y,float z){if(x==0&&y==0&&z==0)return;p.Rotation[bone]=CapturedRig.Euler(x,y,z)*p.Rotation[bone];}
        private readonly Quaternion[] armWas=new Quaternion[3];
        /// <summary>Reaches the arm to `target` (rig frame) with its elbow toward `pole` (chest frame), weighted by k over the pose's.</summary>
        private void ArmTo(CapturedRig.Pose p,bool left,Vector3 target,Vector3 pole,float k)
        {
            if(k<=0)return;k=Math.Min(1,k);p.World();int a=left?CapturedRig.LArm:CapturedRig.RArm;for(int i=0;i<3;i++)armWas[i]=p.Rotation[a+i];
            var inv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);var local=Vector3.Transform(target-p.WorldPosition[CapturedRig.Chest],inv);
            var wrist=inv*p.WorldRotation[a+2];CapturedRig.Arm(p,left,local,wrist,pole);
            for(int i=0;i<3;i++)p.Rotation[a+i]=Quaternion.Slerp(armWas[i],p.Rotation[a+i],k);
        }
    }
}
