using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde's secondary motion (owner 2026-10-17: "fix the zombies animations ... Improve these vastly"; docs/ZOMBIE-MOTION.md),
    /// layered over the capture and the planted gait. Presentation only: it moves bones, never the body, and reads only what every
    /// peer holds (the presented root's motion, the gait's footfalls, the replicated hits and attack timing, the clock, the seed).
    /// <list type="bullet">
    /// <item>The lean: the presented root's bank into turns and pitch into speeding up and braking (Core/NativeHordeFlow) tilt the
    /// whole body over its planted feet.</item>
    /// <item>A loose head: it lags the body's turns and speed changes on second-order dynamics (it overshoots and wobbles back),
    /// nods on each footfall, lolls on slow noise, snaps on a hit (harder on a headshot) and whips on its own blow's contact.</item>
    /// <item>The trunk: the chest lags turns and speed changes, sways over the stance foot, and the body dips under each footfall.</item>
    /// <item>Dangling arms: each hand trails the body's acceleration like a weight on a cord (the arm turned about its shoulder so
    /// the hand moves by the lag), swings out on turns, bounces on footfalls; the shoulders drop and recover with each step.</item>
    /// <item>Per add (its seed): how loose the head is, a hunch, a dead arm that hangs and drags, a limp that dips one side, tics
    /// (a quick shoulder, head or arm jerk at hashed moments, eased in, the same on every peer). Hurt, it sags (on a health eased
    /// over <see cref="HurtHalfLife"/>): more hunch, a drooping head, heavier arms, a worse limp.</item>
    /// <item>The hinged jaws (the spitter's, the Hulk's) flap with the steps and the head's motion (<see cref="JawFlap"/>).</item>
    /// </list>
    /// By crowd tier (HordeMotionInput.Tier): Full all of it; Reduced the lean, head, trunk and arm lag (no shoulders, tics, jaw or
    /// body dip); Impostor the lean only. Fixed fields: no allocation per frame.
    /// </summary>
    public sealed partial class NativeHordeGait
    {
        // ---- the add's traits (its seed) ----
        private float lifeLoll=1,lifeHunch,lifeDead,lifeDeadSide=1,lifeLimp,lifeTicRate,lifeTicAmp,lifeArmHz=1.3f,lifeHeadHz=2;
        // ---- dynamics ----
        private BossSecondOrder headPitch,headYaw,headRoll,chestPitch,chestYaw,chestRoll,clavL,clavR,jaw,dip;
        private BossSecondOrder3 handL,handR;
        /// <summary>The half-life (s) of the limp's weight coming onto and off the bad leg.</summary>
        public const float LimpEase=.06f;private BossSpring limpEase;
        /// <summary>The time constant (s) a hit's snap is fed into the head, chest, hands and jaw over (the same impulse, eased in
        /// instead of landing in one frame, where it read as one more twitch: the head started at up to 30 rad/s).</summary>
        public const float HitSpread=.035f;
        private float hitPitch,hitRoll,hitYaw,hitChest,hitJaw;private Vector3 hitHands;
        /// <summary>The body's acceleration the head, chest and hands lag is eased over this time (s) and held within this much
        /// (m/s^2). The authority starts and stops an add, and knocks it back, in one tick: the presented root still turns round
        /// at 80 to 170 m/s^2 there, and the old 9 m/s^2 cap swung the loose head's lag by up to .9 rad in a frame, at every hit and
        /// every restart.</summary>
        public const float LifeAccelEase=.06f,LifeAccelMost=5f;private Vector2 lifeAcc;
        private Vector2 lifeVel;private float lifeYaw,lifeYawRate,lifeContactAge=float.NaN,lifeHitNewest=float.NegativeInfinity;private bool lifeReady;private int lifeTier;
        /// <summary>The jaw's flap this frame (rad of extra opening, a hinged jaw's), for the runtime to add to its opening.</summary>
        public float JawFlap{get;private set;}
        /// <summary>The last frame's body acceleration (rig frame, m/s^2: x its left, z forward) and the turn rate (rad/s, + right).</summary>
        public Vector3 LifeAccel{get;private set;}
        public float LifeYawRate=>lifeYawRate;
        /// <summary>The head's lag this frame (rad: pitch, yaw, roll), for the checks.</summary>
        public Vector3 HeadLag=>new Vector3(headPitch.Value,headYaw.Value,headRoll.Value);
        /// <summary>The seeded traits: head looseness, hunch (rad), dead arm (0..1, its side +1 left), limp (0..1), tic rate (Hz).</summary>
        public float Loll=>lifeLoll;public float Hunch=>lifeHunch;public float DeadArm=>lifeDead;public float LimpAmount=>lifeLimp;public float TicRate=>lifeTicRate;

        private static float Heavy(int k){k=NativeHordeKinds.Base(k);return k==NativeHordeKinds.Hulk?1.8f:k==NativeHordeKinds.Brute?1.35f:k==NativeHordeKinds.Bloater?1.3f:k==NativeHordeKinds.Runner?.85f:k==NativeHordeKinds.Crawler?.8f:1;}
        private void LifeReset()
        {
            float H(int k)=>NativeHordeKinds.Hash(seed,200+k);float w=Heavy(kind),hz=1/(float)Math.Sqrt(w);
            lifeLoll=.45f+.8f*H(0);lifeHunch=-.04f+.16f*H(1);lifeDead=H(2)<.32f?.55f+.45f*H(3):0;lifeDeadSide=H(4)<.5f?1:-1;
            lifeLimp=kind==NativeHordeKinds.Hulk?0:H(5)<.38f?.45f+.55f*H(6):0;lifeTicRate=.12f+.3f*H(7);lifeTicAmp=.5f+.5f*H(8);
            // A big body's dynamics run about the square root of its size slower; a looser head lolls lower and wobbles longer.
            lifeHeadHz=(2.4f-.7f*lifeLoll)*hz;lifeArmHz=(1.45f-.25f*H(9))*hz;
            float hz2=lifeHeadHz,z2=.42f-.12f*lifeLoll;
            headPitch.Configure(hz2,z2,0);headYaw.Configure(hz2,z2,0);headRoll.Configure(hz2*.85f,z2*.9f,0);
            chestPitch.Configure(1.7f*hz,.55f,0);chestYaw.Configure(1.5f*hz,.5f,0);chestRoll.Configure(1.6f*hz,.5f,0);
            clavL.Configure(3.1f*hz,.32f,0);clavR.Configure(3.1f*hz,.32f,0);jaw.Configure(4.6f,.18f,0);dip.Configure(3.4f*hz,.45f,0);
            handL.Configure(lifeArmHz*(lifeDead>0&&lifeDeadSide>0?.7f:1),.28f,0);handR.Configure(lifeArmHz*(lifeDead>0&&lifeDeadSide<0?.7f:1),.28f,0);
            headPitch.Reset(0);headYaw.Reset(0);headRoll.Reset(0);chestPitch.Reset(0);chestYaw.Reset(0);chestRoll.Reset(0);clavL.Reset(0);clavR.Reset(0);jaw.Reset(0);dip.Reset(0);handL.Reset(Vector3.Zero);handR.Reset(Vector3.Zero);
            limpEase.Reset(0);hitPitch=hitRoll=hitYaw=hitChest=hitJaw=0;hitHands=Vector3.Zero;lifeAcc=Vector2.Zero;lifeVel=velocity;lifeYaw=Yaw;lifeYawRate=0;lifeContactAge=float.NaN;lifeHitNewest=float.NegativeInfinity;JawFlap=0;LifeAccel=Vector3.Zero;lifeReady=true;
        }
        /// <summary>Steps the dynamics after the gait's step (its footfalls, the presented root's motion, the hits and the blow).</summary>
        private void LifeStep(in HordeMotionInput f,NativeHordeReact react,bool fresh)
        {
            lifeTier=f.Tier;if(fresh||!lifeReady)LifeReset();float dt=lastDt;if(!(dt>0)){lifeVel=velocity;lifeYaw=Yaw;return;}
            if(lifeTier>=NativeHordeModels.Impostor){lifeVel=velocity;lifeYaw=Yaw;JawFlap=0;hitPitch=hitRoll=hitYaw=hitChest=hitJaw=0;hitHands=Vector3.Zero;return;}
            // The root's acceleration in the rig's frame (source axes: x its left, z forward) and the turn rate.
            var a=(velocity-lifeVel)/dt;lifeVel=velocity;float al=-Vector2.Dot(a,Right),af=Vector2.Dot(a,Forward);
            {float ka=1-(float)Math.Exp(-dt/LifeAccelEase);lifeAcc+=(new Vector2(Math.Max(-9,Math.Min(9,al)),Math.Max(-9,Math.Min(9,af)))-lifeAcc)*ka;}
            float ax=Math.Max(-LifeAccelMost,Math.Min(LifeAccelMost,lifeAcc.X)),az=Math.Max(-LifeAccelMost,Math.Min(LifeAccelMost,lifeAcc.Y));
            float rate=f.Flow?f.YawRate:Wrap(Yaw-lifeYaw)*BossMath.Deg/dt;lifeYaw=Yaw;lifeYawRate+=(rate-lifeYawRate)*(1-(float)Math.Exp(-dt/.05f));
            float w=lifeYawRate,v=Speed,cent=Math.Max(-6,Math.Min(6,v*w)),mass=Heavy(kind),loose=lifeLoll*(1+.8f*(1-hurtShown));
            LifeAccel=new Vector3(ax,0,az);
            // ---- the head: lags the turn and the speed change, rolls out of the turn ----
            headPitch.Step(-.045f*az*loose,dt);headYaw.Step(Math.Max(-.45f,Math.Min(.45f,.16f*w*loose)),dt);headRoll.Step(-.05f*cent*loose,dt);
            chestPitch.Step(-.02f*az,dt);chestYaw.Step(Math.Max(-.25f,Math.Min(.25f,.07f*w)),dt);chestRoll.Step(-.025f*cent,dt);
            // ---- the hands: weights on cords (rig metres): they trail the acceleration and swing out of a turn ----
            var trail=new Vector3(-.016f*ax+.012f*cent,0,-.02f*az);
            handL.Step(trail*(lifeDead>0&&lifeDeadSide>0?1.5f:1),dt);handR.Step(trail*(lifeDead>0&&lifeDeadSide<0?1.5f:1),dt);
            bool full=lifeTier==NativeHordeModels.Full;
            if(full){clavL.Step(0,dt);clavR.Step(0,dt);dip.Step(0,dt);jaw.Step(0,dt);}
            // ---- footfalls: the head nods, the hands bounce, the shoulders drop, the body dips, the jaw flaps ----
            if(Landed!=0)
            {
                float k=Math.Min(1.4f,.35f+v*.32f)*(float)Math.Sqrt(mass);
                for(int i=0;i<(quad?4:2);i++)if((Landed&(1<<i))!=0)
                {
                    // (Side -1 is the left foot: the head tips toward the foot taking the weight, that shoulder drops most.)
                    float side=SideOf(i);headPitch.Kick(.55f*k*loose/mass);headRoll.Kick(.35f*side*k*loose/mass);if(!heavy)chestPitch.Kick(.25f*k/mass);
                    var bounce=new Vector3(0,-.32f*k,0);handL.Kick(bounce);handR.Kick(bounce);
                    if(full){if(side>0){clavR.Kick(-1.1f*k);clavL.Kick(-.45f*k);}else{clavL.Kick(-1.1f*k);clavR.Kick(-.45f*k);}dip.Kick(-.32f*k);jaw.Kick(2.4f*k);}
                }
            }
            // ---- hits (the replicated ones): the head snaps, a headshot whips it; the hands fly ----
            if(react!=null&&react.Newest>lifeHitNewest)
            {
                lifeHitNewest=react.Newest;
                if(react.NewestHit(out var push,out float size,out bool head)&&f.Time-lifeHitNewest<.25f)
                {
                    // The push in the rig's frame (source: x its left, z forward): a blow from the front pushes it back.
                    float lz=Vector2.Dot(push,Forward),lx=-Vector2.Dot(push,Right),s=Math.Min(1.6f,size)*(head?2.2f:1)/mass;
                    // (Fed in over HitSpread below, not kicked in one frame.)
                    hitPitch+=-5f*lz*s-(head?3f*s:0);hitRoll+=-4f*lx*s;hitYaw+=2.5f*lx*s;hitChest+=-2f*lz*s/mass;hitHands+=new Vector3(lx,.35f,lz)*(.9f*s);if(full)hitJaw+=4f*s;
                }
            }
            {
                float k=1-(float)Math.Exp(-dt/HitSpread);
                headPitch.Kick(hitPitch*k);headRoll.Kick(hitRoll*k);headYaw.Kick(hitYaw*k);chestPitch.Kick(hitChest*k);handL.Kick(hitHands*k);handR.Kick(hitHands*k);if(full)jaw.Kick(hitJaw*k);
                float left=1-k;hitPitch*=left;hitRoll*=left;hitYaw*=left;hitChest*=left;hitHands*=left;hitJaw*=left;
            }
            // ---- its own blow's contact (LastAttack + the authority's strike): the trunk and head jolt through the impact ----
            float age=f.AttackAge;
            if(!float.IsNaN(age)&&!f.Cancelled&&age>=f.Strike&&(float.IsNaN(lifeContactAge)||lifeContactAge<f.Strike)&&age<f.Strike+.2f)
            {
                bool smash=f.Attack==NativeHordeKinds.Attack.Smash,belly=f.Attack==NativeHordeKinds.Attack.Belly;float k=smash?1.6f:belly?1.1f:.8f;
                headPitch.Kick(2.2f*k);chestPitch.Kick(1.2f*k);var jolt=new Vector3(0,-.9f*k,.5f*k);handL.Kick(jolt);handR.Kick(jolt);
                if(full){dip.Kick(-(smash?1.2f:.4f)*k);clavL.Kick(-1.5f*k);clavR.Kick(-1.5f*k);jaw.Kick(3f*k);}
            }
            lifeContactAge=age;
            JawFlap=full?Math.Max(0,Math.Min(.35f,jaw.Value)):0;
        }
        // A tic: a pulse (eased in over TicRise, settled over .32 s) at hashed moments (about every 1/rate s), on the clock: every peer
        // twitches alike. (It came in over 40 ms, a snap that read as one more twitch toward the pilot.)
        public const float TicRise=.1f;
        private float Tic(float t,int channel,out float side)
        {
            side=1;if(lifeTicRate<=0)return 0;float period=1/lifeTicRate,k=(t+period*NativeHordeKinds.Hash(seed,230+channel))/period;int n=(int)Math.Floor(k);float u=(k-n)*period;
            if(NativeGoreRules.Hash01(seed,n,240+channel)>.55)return 0;side=NativeGoreRules.Hash01(seed,n,250+channel)<.5?-1:1;
            float amp=lifeTicAmp*(.6f+.4f*(float)NativeGoreRules.Hash01(seed,n,260+channel));return u<TicRise?amp*Sm(u/TicRise):u<TicRise+.32f?amp*(1-Sm((u-TicRise)/.32f)):0;
        }
        /// <summary>The body and trunk layer: the lean over the planted feet, hunch and hurt sag, the chest's lag and stance sway, the
        /// limp's dip, the footfall dip. Before the head's bends and the balance.</summary>
        private void LifeTrunk(CapturedRig.Pose p,in HordeMotionInput f,float moving)
        {
            float hurt=1-hurtShown;
            // The lean (the presented root's bank and pitch): the whole body tilts over its feet (source frame: +z rotation tilts to its right).
            if(f.Flow&&(f.Bank!=0||f.Pitch!=0))p.Rotation[CapturedRig.Body]=CapturedRig.Euler(f.Pitch*(heavy?.5f:1),0,f.Bank*(heavy?.6f:1))*p.Rotation[CapturedRig.Body];
            if(lifeTier>=NativeHordeModels.Impostor||!lifeReady)return;
            // (The crawler's trunk is its own: only the footfall dip.)
            if(quad){var q=p.Position[CapturedRig.Body];q.Y+=Math.Min(0,dip.Value)*.6f;p.Position[CapturedRig.Body]=q;return;}
            // (The Hulk's trunk is its own heavy plod, Core/NativeHordeHeavy: only the chest's lag and the hurt sag ride on it.)
            float hunch=(heavy?0:lifeHunch)+.16f*hurt*(heavy?.4f:1);
            float u=Frac(Phase),sway=heavy?0:(float)Math.Sin(2*Math.PI*u)*.035f*moving;
            // The limp: the bad leg's stance (its foot planted) dips and rolls the hips toward it.
            float limp=Math.Min(1,lifeLimp+.6f*hurt*(lifeLimp>0?1:.4f)),bad=0;
            // (Eased on a spring: the weight comes onto the bad leg as it lands, never on the frame.)
            if(limp>0&&!quad){int i=limpSide<0?0:1;if(!feet[i].Swing)bad=moving*limp;}
            bad=lastDt>0?Math.Max(0,limpEase.Step(bad,LimpEase,lastDt)):limpEase.Value;
            // (The dip only ever lowers the hips: the legs reach their planted feet at any moment. The Hulk plods on its own jolt.)
            var b=p.Position[CapturedRig.Body];b.Y+=(heavy?0:Math.Min(0,dip.Value))-.035f*bad;b.X+=(limpSide<0?.02f:-.02f)*bad;p.Position[CapturedRig.Body]=b;
            // (A heavy body's keyed move (a punch, a swipe, a knock, a charge) is keyed to the blow: no lag rides on it.)
            float lag=heavy&&(Replaces||f.Charging)?0:1;
            Bend(p,CapturedRig.Spine,hunch*.45f+chestPitch.Value*.4f*lag,chestYaw.Value*.4f*lag,sway+chestRoll.Value*.4f*lag+(limpSide<0?-.05f:.05f)*bad);
            Bend(p,CapturedRig.Chest,hunch*.55f+chestPitch.Value*.6f*lag,chestYaw.Value*.6f*lag,-sway*.6f+chestRoll.Value*.6f*lag);
        }
        /// <summary>The head layer: its lag, nods and loll, hurt droop and tics. After the trunk's bends and the head's tracking.</summary>
        private void LifeHead(CapturedRig.Pose p,in HordeMotionInput f,float alert)
        {
            // (The Hulk holds its head steady against its plod, and still through a keyed move.)
            if(lifeTier>=NativeHordeModels.Impostor||!lifeReady||heavy&&(Replaces||f.Charging))return;
            float hurt=1-hurtShown,t=f.Time,calm=(1-.6f*alert)*(heavy?.5f:1);
            float lp=.09f*lifeLoll*BossMath.Noise(seed^0x1f3u,t*.45f)*calm,lr=.13f*lifeLoll*BossMath.Noise(seed^0x2e7u,t*.38f)*calm;
            float pitch=headPitch.Value+lp+.22f*hurt,yaw=headYaw.Value,roll=headRoll.Value+lr+.1f*hurt*(limpSide<0?1:-1);
            if(lifeTier==NativeHordeModels.Full){float tic=Tic(t,0,out float ts);yaw+=.32f*tic*ts;roll+=.22f*tic*ts;pitch+=.12f*tic;}
            Bend(p,CapturedRig.Neck,pitch*.45f,yaw*.45f,roll*.45f);Bend(p,CapturedRig.Head,pitch*.55f,yaw*.55f,roll*.55f);
        }
        /// <summary>The arms' layer: the hands' trail (each upper arm turned about its shoulder so the hand moves by its lag), the dead
        /// arm's hang, the shoulders' drop, arm tics. After every arm pose, before the hands are kept within reach.</summary>
        private void LifeArms(CapturedRig.Pose p,in HordeMotionInput f,float attackW)
        {
            // (The Ironworks kinds carry their tools in both fists: their arms are the tool's.)
            if(lifeTier>=NativeHordeModels.Impostor||!lifeReady||NativeIronworksKinds.Is(f.Kind)||heavy&&(Replaces||f.Charging))return;
            float hurt=1-hurtShown,free=1-.75f*Clamp01(attackW),t=f.Time;bool full=lifeTier==NativeHordeModels.Full;
            if(full)
            {
                // The shoulders: the step's drop, a tic's shrug (+z on the left clavicle lifts it).
                float tic=Tic(t,1,out float ts);
                Bend(p,CapturedRig.LClav,0,0,clavL.Value*.6f+(ts>0?.3f*tic:0));Bend(p,CapturedRig.RClav,0,0,-clavR.Value*.6f-(ts<0?.3f*tic:0));
            }
            p.World();
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int arm=left?CapturedRig.LArm:CapturedRig.RArm,hand=arm+2;
                // The dead arm hangs: the upper arm turned about its shoulder so the hand drops toward just below and behind it (one
                // turn, no re-solve: the elbow and wrist keep their bend, nothing flips), and it drags on its slower cord. A blow
                // takes the arm back (it swings with the rest).
                float dead=(lifeDead>0&&(lifeDeadSide>0)==left?lifeDead:0)+.25f*hurt;float off=1-Clamp01(attackW);dead=Math.Min(.85f,dead)*off*off;
                if(dead>0)
                {
                    var sh0=p.WorldPosition[arm];var from=p.WorldPosition[hand]-sh0;var to=new Vector3(left?.05f:-.05f,-.47f,-.04f);float fl=from.Length();
                    if(fl>.05f)
                    {
                        var a=from/fl;var b=Vector3.Normalize(to);float cos=Vector3.Dot(a,b);
                        if(cos>-.8f){var hangAxis=Vector3.Cross(a,b);float sl=hangAxis.Length();if(sl>1e-5f){float hangAng=(float)Math.Atan2(sl,cos)*dead*.7f;var clav=p.WorldRotation[arm-1];
                            p.Rotation[arm]=Quaternion.Normalize(Quaternion.Inverse(clav)*CapturedRig.Rot(hangAxis/sl,hangAng)*clav*p.Rotation[arm]);p.World();}}
                    }
                }
                var lag=(left?handL:handR).Value*free;
                if(full){float tic=Tic(t,left?2:3,out float ts);lag+=new Vector3(.05f*ts,.07f,-.04f)*tic;}
                if(lag.LengthSquared()<1e-8f)continue;
                p.World();var sh=p.WorldPosition[arm];var h=p.WorldPosition[hand]-sh;float len=h.Length();if(len<.1f)continue;
                // The lag is in the rig's frame; turn the upper arm about the shoulder so the hand moves by it (its part across the arm).
                var across=lag-h*(Vector3.Dot(lag,h)/(len*len));
                // (Never outward from the body's axis: the claws stay where the hit volume's reach has them.)
                var hw=p.WorldPosition[hand];var radial=new Vector2(hw.X,hw.Z);float rl=radial.Length();
                if(rl>1e-3f){radial/=rl;float outward=across.X*radial.X+across.Z*radial.Y;if(outward>0){across.X-=outward*radial.X;across.Z-=outward*radial.Y;}}
                float ang=Math.Min(.9f,across.Length()/len);if(ang<1e-4f)continue;
                var axis=Vector3.Cross(h,across);float al=axis.Length();if(al<1e-8f)continue;axis/=al;
                var parentR=p.WorldRotation[arm-1];var turn=Quaternion.Inverse(parentR)*CapturedRig.Rot(axis,ang)*parentR;
                p.Rotation[arm]=Quaternion.Normalize(turn*p.Rotation[arm]);
                // ... and the elbow folds a little more as the hand drags behind (a weight on a cord bends its joint).
                float fold=Math.Max(0,Math.Min(.3f,-.7f*lag.Z));if(fold>0)p.Rotation[arm+1]=Quaternion.Normalize(CapturedRig.Euler(-fold,0,0)*p.Rotation[arm+1]);
            }
        }
    }
}
