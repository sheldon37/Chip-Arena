using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One moment of the Top Dog's mallet by hand (NativeDogMallet): the body's toon channels and where the mallet is. Angles in
    /// degrees. Twist turns the chest about the up axis (+ to its right), Bend tips it forward, Lean rolls it (+ to its left). Squash is the
    /// body's height share (under 1 squashed, over 1 stretched, its width the other way), Lift raises the whole body (a hop), Tuck draws the
    /// legs up. Grip (the right fist) is from the shoulders' middle and Handle the handle's way, both in the body's frame turned by Twist
    /// only (x right, y up, z forward: pitch is the world's). The mallet's barrel lies across the handle along Face, so its flat ends lead
    /// the swing. Floor brings the head down onto the floor (the slams), Smear is how fast the head goes (0..1), Squish its impact squash.
    /// Weight is the layer's share over the clip under it; Free how much the left hand is off the handle (its own pose: a thumbs up); Spin
    /// turns the whole body about its feet (degrees, + to the right: a whirl).</summary>
    public struct DogMalletPose
    {
        public float Weight,Twist,Bend,Lean,Squash,Lift,Tuck,Floor,Smear,Squish,Free,Spin;
        public Vector3 Grip,Handle,Face;
        public static DogMalletPose Lerp(in DogMalletPose a,in DogMalletPose b,float u)
        {
            float L(float x,float y)=>x+(y-x)*u;var face=b.Face;if(Vector3.Dot(a.Face,b.Face)<0)face=-face;
            return new DogMalletPose{Weight=L(a.Weight,b.Weight),Twist=L(a.Twist,b.Twist),Bend=L(a.Bend,b.Bend),Lean=L(a.Lean,b.Lean),Squash=L(a.Squash,b.Squash),Lift=L(a.Lift,b.Lift),Tuck=L(a.Tuck,b.Tuck),
                Floor=L(a.Floor,b.Floor),Smear=L(a.Smear,b.Smear),Squish=L(a.Squish,b.Squish),Free=L(a.Free,b.Free),Spin=L(a.Spin,b.Spin),Grip=Vector3.Lerp(a.Grip,b.Grip,u),Handle=NativeDogMallet.Unit(Vector3.Lerp(a.Handle,b.Handle,u),b.Handle),
                Face=NativeDogMallet.Unit(Vector3.Lerp(a.Face,face,u),face)};
        }
    }

    /// <summary>
    /// The Top Dog's mallet animated by hand (owner 2026-11-09: "fix combo animation to swing from right to left, left to right, then the
    /// overhead ... make it connect with ground and face the correct direction, it faces the side currently rather than the flat parts; Maybe
    /// make like a toon like jump overhead slam for the 3rd hit? Make this animation a lot better"; "Dash attack and air attack need to change
    /// hammer direction again too ... hitting from the side instead of the flat parts"; "fix the intro to slam the mallet with two hands").
    /// The pack's sword clips whipped the mallet's head onto its side, so every mallet move is keyed here as a toon pose on its own clock
    /// and laid over the legs' clip (Runtime NativeDogMalletPresentation.cs): the chest twists, bends and leans, the body squashes, stretches,
    /// hops and tucks, both fists close on the handle by reach, and the barrel is turned across the handle along the head's own travel, so the
    /// flat ends always lead. Each swing coils (a slow wind-up and a held beat), smears through the hit, overshoots and settles; the third is
    /// a hop, the mallet hung far behind the back at the top, then slammed flat onto the floor ahead in a deep squash. Pure: the same record and
    /// clock give the same pose. Nothing here reaches the rules: the hits stay at the authority's moments (each move's hit key is its moment).
    /// </summary>
    public static class NativeDogMallet
    {
        public enum Move{None,Swing1,Swing2,Swing3,Dash,Smash,Slam,Intro}
        /// <summary>The nominal body the poses are keyed on (the Hot Dog on the model, measured: the spine's pivot, the shoulders over it, half
        /// their width, a fist's reach from its shoulder) and the mallet (grip to the head's middle, the head's radius).</summary>
        public const float Pivot=1.03f,Shoulders=.37f,HalfWidth=.135f,Reach=.53f,Length=NativeWeaponModels.MalletHead,Head=NativeWeaponModels.MalletRadius;
        /// <summary>The support fist down the handle from the right (toward the knob), as the greatsword's.</summary>
        public const float Support=-.11f;
        /// <summary>The layer's fade in at a move's start and out after its end.</summary>
        public const float FadeIn=.06f,FadeOut=.2f;
        /// <summary>A key: its moment (s, from the move's hit, or from its start for the keys after a landing's), the channels, the grip and the
        /// handle as yaw, pitch (degrees, in the Twist frame) and the grip's distance, and how the segment into it eases.</summary>
        internal struct Key
        {
            public float T,Twist,Bend,Lean,Squash,Lift,Tuck,GripYaw,GripPitch,GripDist,HandleYaw,HandlePitch,Floor,Squish,Free,Spin;public Ease In;
        }
        internal enum Ease{Linear,Smooth,In,Strike,Out,Back}
        internal static Key K(float t,float twist,float bend,float squash,float gy,float gp,float gd,float hy,float hp,Ease e=Ease.Smooth,float lift=0,float tuck=0,float floor=0,float squish=0,float lean=0,float free=0,float spin=0)
            =>new Key{T=t,Twist=twist,Bend=bend,Lean=lean,Squash=squash,Lift=lift,Tuck=tuck,GripYaw=gy,GripPitch=gp,GripDist=gd,HandleYaw=hy,HandlePitch=hp,Floor=floor,Squish=squish,Free=free,Spin=spin,In=e};

        // ---- the moves (the swings' keys from the hit; nominal wind-up and follow-through, scaled to the record's) ----
        /// <summary>The combo's first: a forehand from the right to the left. Drawn back over the right shoulder, coiled with a held beat,
        /// then whipped across level through the front (the hit) and out to the left, the body following round.</summary>
        private static readonly Key[] Forehand=
        {
            K(-.2f,25,4,1,10,-20,.38f,40,50),
            K(-.16f,40,8,.95f,16,-4,.38f,60,22,Ease.Out),
            K(-.12f,46,8,.92f,18,-2,.37f,66,24,Ease.Smooth,lean:-4),
            K(0,-10,12,1.06f,0,-12,.48f,-4,-2,Ease.In,lean:4),
            K(.1f,-46,8,1,-10,-14,.42f,-80,-8,Ease.Out),
            K(.2f,-44,6,1,-8,-10,.4f,-78,2),
            K(.28f,-42,5,1,-6,-8,.4f,-78,6),
        };
        /// <summary>The second: a backhand back from the left to the right, ending with the head rising over the right shoulder (into the third).</summary>
        private static readonly Key[] Backhand=
        {
            K(-.2f,-42,5,1,-6,-8,.4f,-78,6),
            K(-.16f,-46,8,.95f,-8,-4,.38f,-80,22,Ease.Out),
            K(-.12f,-50,8,.92f,-10,-2,.37f,-80,26,Ease.Smooth,lean:4),
            K(0,12,12,1.06f,8,-12,.48f,4,0,Ease.In,lean:-4),
            K(.1f,50,8,1,22,-8,.42f,86,10,Ease.Out),
            K(.2f,46,6,1,22,12,.4f,104,36),
            K(.28f,38,5,1,14,24,.4f,140,52),
        };
        /// <summary>The third: a toon jump slam. A squat, a spring up stretched long with the mallet swung up and over until its head hangs far
        /// down behind the back at the top (knees up, held a beat), then everything comes down: the head over the top and flat onto the floor
        /// ahead in a deep squash, stuck there a moment, pulled up with an overshoot.</summary>
        private static readonly Key[] JumpSlam=
        {
            K(-.36f,38,5,1,14,24,.4f,140,52),
            K(-.31f,12,18,.8f,6,-20,.38f,70,78,Ease.Out),
            K(-.25f,0,-14,1.14f,0,62,.42f,0,112,Ease.Out,lift:.15f,tuck:.3f),
            K(-.19f,0,-30,1.08f,0,84,.45f,0,180,Ease.Out,lift:.38f,tuck:.85f),
            K(-.16f,0,-32,1.1f,0,86,.45f,0,186,Ease.Smooth,lift:.4f,tuck:.9f),
            K(0,0,68,.72f,0,-58,.49f,0,-8,Ease.In,floor:1,squish:1),
            K(.1f,0,62,.78f,0,-56,.48f,0,-7,Ease.Out,floor:1,squish:.3f),
            K(.22f,0,42,.95f,0,-48,.45f,0,4,Ease.Smooth,floor:.5f),
            K(.32f,0,14,1.04f,4,-26,.42f,0,40),
            K(.46f,4,6,1,6,-24,.4f,0,58),
        };
        /// <summary>The dash: tucked low with the mallet trailing behind the right hip through the charge, then swung round low and up through
        /// the front at the hit (an underarm scoop: the faces forward, the batted fly on ahead), up over the head, and back to the ready. From
        /// its start.</summary>
        private static readonly Key[] DashKeys=
        {
            K(0,15,10,1,10,-25,.4f,30,40),
            K(.1f,30,30,.92f,35,-55,.45f,100,-30,Ease.Out),
            K(.16f,32,34,1,36,-56,.45f,100,-32),
            K(.273f,-5,22,1.05f,5,-50,.48f,0,-8,Ease.In),
            K(.36f,-20,14,1.02f,-4,-12,.44f,-8,58,Ease.Out),
            K(.5f,-10,8,1,0,6,.42f,0,82),
            K(.78f,4,6,1,6,-24,.4f,0,58),
        };
        /// <summary>The overhead slam (looking down): up overhead in both hands, arched back with the head hung behind through the hang, swung
        /// over the top as it drops and held there, then on the landing down flat onto the floor ahead, a deep squash, held, pulled up. Its air
        /// keys from the start, its landing keys from the landing (no sooner than AirReady after the start).</summary>
        private static readonly Key[] SlamAir=
        {
            K(0,0,0,1,4,10,.4f,0,70),
            K(.12f,0,-28,1.12f,0,84,.45f,0,196,Ease.Out,tuck:.5f),
            K(.2f,0,-34,1.1f,0,86,.45f,0,204,Ease.Smooth,tuck:.7f),
            K(.3f,0,-8,1.06f,0,80,.45f,0,100,Ease.Out,tuck:.8f),
            K(.8f,0,-6,1.06f,0,80,.45f,0,96,Ease.Smooth,tuck:.8f),
        };
        private static readonly Key[] SlamLand=
        {
            K(0,0,-8,1.06f,0,80,.45f,0,100),
            K(.1f,0,70,.68f,0,-58,.49f,0,-8,Ease.In,floor:1,squish:1),
            K(.2f,0,62,.76f,0,-56,.48f,0,-7,Ease.Out,floor:1,squish:.3f),
            K(.3f,0,30,.98f,0,-40,.45f,0,16,Ease.Smooth,floor:.4f),
            K(.42f,4,6,1.02f,6,-24,.4f,0,58),
        };
        /// <summary>The smash (in the air, looking ahead): cocked over the right shoulder, then swung forward and down as the body pitches in
        /// over it; on the landing its head flat on the floor ahead.</summary>
        private static readonly Key[] SmashAir=
        {
            K(0,10,0,1,6,8,.4f,20,60),
            K(.1f,28,-16,1.08f,16,56,.42f,40,160,Ease.Out,tuck:.4f),
            K(.14f,26,-18,1.08f,16,58,.42f,40,166,Ease.Smooth,tuck:.5f),
            K(.3f,0,40,1,0,-35,.48f,0,-30,Ease.In,tuck:.6f),
            K(.8f,0,44,1,0,-38,.48f,0,-34,Ease.Smooth,tuck:.6f),
        };
        private static readonly Key[] SmashLand=
        {
            K(0,0,40,1,0,-35,.48f,0,-30),
            K(.07f,0,64,.74f,0,-58,.49f,0,-8,Ease.In,floor:1,squish:1),
            K(.16f,0,58,.8f,0,-55,.48f,0,-6,Ease.Out,floor:1,squish:.3f),
            K(.26f,0,26,1,0,-36,.44f,0,22,Ease.Smooth,floor:.3f),
            K(.36f,4,6,1.02f,6,-24,.4f,0,58),
        };
        /// <summary>The air keys' moment by which the mallet is ready to come down (a landing sooner waits for it).</summary>
        public const float AirReady=.3f;
        /// <summary>Order Up!'s slam (Core NativeTopDogIntro), on the intro's clock: out overhead in both hands, a squat, a spring up with the
        /// head swung far down behind the back (held at the top), the slam flat onto the floor in front, a deep squash and a long hold, a
        /// shiver, then up into the lean on the handle.</summary>
        private static readonly Key[] IntroSlam=
        {
            K(4.4f,0,-6,1,0,40,.42f,0,100),
            K(4.55f,0,16,.8f,0,-20,.38f,0,74,Ease.Out),
            K(4.64f,0,-18,1.16f,0,64,.43f,0,118,Ease.Out,lift:.22f,tuck:.4f),
            K(4.74f,0,-36,1.1f,0,86,.45f,0,194,Ease.Out,lift:.5f,tuck:.9f),
            K(4.81f,0,-40,1.12f,0,88,.45f,0,200,Ease.Smooth,lift:.52f,tuck:.95f),
            K(5.0f,0,72,.64f,0,-58,.49f,0,-8,Ease.In,floor:1,squish:1),
            K(5.14f,0,66,.72f,0,-57,.49f,0,-7,Ease.Out,floor:1,squish:.4f),
            K(5.36f,0,58,.86f,0,-55,.48f,0,-6,Ease.Smooth,floor:1),
            K(5.5f,0,30,1.05f,0,-42,.46f,0,12,Ease.Smooth,floor:.4f),
            // Up and leaning on it under the name: the head down on the floor in front, the handle up to the fists on its top, a hip out; the
            // left hand off it for the thumbs up.
            K(5.74f,8,6,1.03f,15,-32,.36f,15,-65,Ease.Smooth,floor:1,lean:-4),
            K(7.2f,8,6,1,15,-32,.36f,15,-65,Ease.Smooth,floor:1,lean:-4),
            K(7.3f,8,4,1,15,-32,.36f,15,-65,Ease.Smooth,floor:1,lean:-6,free:1),
            K(8.6f,8,4,1,15,-32,.36f,15,-65,Ease.Smooth,floor:1,lean:-6,free:1),
        };
        public const float IntroFrom=4.4f,IntroUntil=8.6f,IntroOut=.14f;
        private static readonly float[] Wind={.2f,.2f,.36f},Follow={.28f,.28f,.46f};

        /// <summary>The swing keys of a combo step.</summary>
        private static Key[] Swing(int step)=>step<=0?Forehand:step==1?Backhand:JumpSlam;

        /// <summary>The mallet's pose for the Top Dog's record at `time`, with its move and that move's start; false when no mallet move is
        /// going (the clip alone).</summary>
        public static bool Sample(NativeTopDogKit k,float time,out DogMalletPose pose,out Move move,out float from)
        {
            pose=default;move=Move.None;from=-100;if(k?.CastKind==null||time<k.CastAt)return false;
            float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);from=at;
            switch(k.CastKind)
            {
                case NativeTopDog.Mallet:
                {
                    int step=Math.Max(0,Math.Min(2,k.CastStep));if(time>=until+FadeOut)return false;move=step==0?Move.Swing1:step==1?Move.Swing2:Move.Swing3;
                    float wind=rel-at,follow=until-rel,t=time-rel;
                    // The keys' wind-up and follow-through scaled to the record's (an attack-rate chip shortens both).
                    float kt=t<0?t*Wind[step]/Math.Max(.01f,wind):t*Follow[step]/Math.Max(.01f,follow);
                    pose=Keyed(Swing(step),kt,move);pose.Weight=Fade(time,at,until);return true;
                }
                case NativeTopDog.Dash:
                {
                    if(time>=until+FadeOut)return false;move=Move.Dash;
                    // Keyed on the nominal dash (wind, charge, recover), its hit key on the rules' mid-charge moment.
                    float hit=rel+NativeTopDog.DashTime*.45f,t=time-at,kh=.273f;float kt=time<hit?t*kh/Math.Max(.01f,hit-at):kh+(time-hit)*(.78f-kh)/Math.Max(.01f,until-hit);
                    pose=Keyed(DashKeys,kt,move);pose.Weight=Fade(time,at,until);return true;
                }
                case NativeTopDog.Slam:
                case NativeTopDog.Smash:
                {
                    bool slam=k.CastKind==NativeTopDog.Slam;move=slam?Move.Slam:Move.Smash;float land=NativeTopDog.Landed(k);var airKeys=slam?SlamAir:SmashAir;
                    // In the air, and landed before the mallet is up and over (it comes down once it is: the strike never skips the top).
                    float down=land<-50?-100:Math.Max(land,at+AirReady);
                    if(land<-50||time<down){pose=Keyed(airKeys,time-at,move);pose.Weight=Fade(time,at,land<-50?until+100:down+1);return true;}
                    var keys=slam?SlamLand:SmashLand;if(time>=Math.Max(until,down+keys[keys.Length-1].T)+FadeOut)return false;
                    // The landing's keys from the air pose it came down from (held a little differently by the drop's own length).
                    var air=Keyed(airKeys,down-at,move);float t=time-down;
                    if(t<keys[1].T){float u=Shape(Ease.In,t/keys[1].T);pose=DogMalletPose.Lerp(air,Keyed(keys,keys[1].T,move),u);pose.Smear=Math.Max(pose.Smear,u);}else pose=Keyed(keys,t,move);
                    pose.Weight=Fade(time,at,Math.Max(until,down+keys[keys.Length-1].T));return true;
                }
            }
            return false;
        }
        /// <summary>Order Up!'s slam at intro time `t` (its share of the body: none outside it).</summary>
        public static bool Intro(float t,out DogMalletPose pose)
        {
            pose=default;if(t<IntroFrom||t>=IntroUntil+IntroOut)return false;pose=Keyed(IntroSlam,t,Move.Intro);
            pose.Weight=Clamp01((t-IntroFrom)/.08f)*(1-Clamp01((t-IntroUntil)/IntroOut));return true;
        }
        private static float Fade(float time,float at,float until)=>Clamp01((time-at)/FadeIn)*(1-Clamp01((time-until)/FadeOut));
        /// <summary>The move's keys at its own time `t`, with the barrel turned onto the head's travel (from the keys either side) and the smear.</summary>
        private static DogMalletPose Keyed(Key[] keys,float t,Move move)=>KeyedPose(keys,t,move==Move.Swing1||move==Move.Swing2,1);
        /// <summary>A move's keys at its own time `t`: `level` for a swing about the up axis (else about the right), `scale` the head's share
        /// of the mallet's length out from the fist (another weapon's keys: the Glaive's ring).</summary>
        internal static DogMalletPose KeyedPose(Key[] keys,float t,bool level,float scale)
        {
            // The head's travel just before now (what leads it into a hit).
            var p=Raw(keys,t);const float h=.008f;var before=Raw(keys,t-h);
            var v=(HeadAt(in p,scale)-HeadAt(in before,scale))/h;
            // The barrel across the handle: along the head's travel where it moves, else across the swing's plane (level swings: about the
            // up axis; the slams and the dash: about the right), so a hold keeps the way it last swung.
            var plane=level?Vector3.UnitY:Vector3.UnitX;var across=Unit(Vector3.Cross(plane,p.Handle),Vector3.UnitY);
            var travel=v-p.Handle*Vector3.Dot(v,p.Handle);float speed=travel.Length();
            if(Vector3.Dot(across,travel)<0)across=-across;
            float w=Clamp01(speed/2.5f);p.Face=Unit(Vector3.Lerp(across,speed>1e-4f?travel/speed:across,w),across);
            p.Smear=Clamp01((speed-3)/(9*Math.Max(.2f,scale)));return p;
        }
        private static DogMalletPose Raw(Key[] keys,float t)
        {
            int i=0;while(i<keys.Length-1&&t>=keys[i+1].T)i++;
            Key a=keys[i],b=i<keys.Length-1?keys[i+1]:keys[i];float u=b.T>a.T?Clamp01((t-a.T)/(b.T-a.T)):0;if(t<keys[0].T)u=0;u=Shape(b.In,u);
            float L(float x,float y)=>x+(y-x)*u;
            var grip=Dir(L(a.GripYaw,b.GripYaw),L(a.GripPitch,b.GripPitch))*L(a.GripDist,b.GripDist);var handle=Dir(L(a.HandleYaw,b.HandleYaw),L(a.HandlePitch,b.HandlePitch));
            return new DogMalletPose{Weight=1,Twist=L(a.Twist,b.Twist),Bend=L(a.Bend,b.Bend),Lean=L(a.Lean,b.Lean),Squash=L(a.Squash,b.Squash),Lift=L(a.Lift,b.Lift),Tuck=L(a.Tuck,b.Tuck),
                Floor=L(a.Floor,b.Floor),Squish=L(a.Squish,b.Squish),Free=L(a.Free,b.Free),Spin=L(a.Spin,b.Spin),Grip=grip,Handle=handle,Face=Vector3.UnitY};
        }
        private static float Shape(Ease e,float u)
        {
            u=Clamp01(u);
            switch(e)
            {
                case Ease.Linear:return u;
                case Ease.In:return u*u;
                case Ease.Strike:return u*u*u;
                case Ease.Out:return 1-(1-u)*(1-u);
                case Ease.Back:{const float c=1.7f;float x=u-1;return 1+(c+1)*x*x*x+c*x*x;}
                default:return u*u*(3-2*u);
            }
        }

        // ---- the nominal body (the checks, and the barrel's travel) ----
        /// <summary>A way at `yaw`, `pitch` (degrees; yaw + to the right, pitch + up; a pitch past 90 goes on over the top behind).</summary>
        public static Vector3 Dir(float yaw,float pitch)
        {
            float y=yaw*(float)Math.PI/180,p=pitch*(float)Math.PI/180;return new Vector3((float)(Math.Sin(y)*Math.Cos(p)),(float)Math.Sin(p),(float)(Math.Cos(y)*Math.Cos(p)));
        }
        /// <summary>`v` turned as the game turns a frame by `yaw` about up (+ to the right), then by `pitch` about its right (+ tips forward
        /// down) and `roll` about its forward (+ to the left): Unity's Quaternion.AngleAxis(yaw,up)*AngleAxis(pitch,right)*AngleAxis(roll,forward).</summary>
        public static Vector3 Turn(Vector3 v,float yaw,float pitch,float roll)
        {
            double r=roll*Math.PI/180,p=pitch*Math.PI/180,y=yaw*Math.PI/180;
            double x1=v.X*Math.Cos(r)-v.Y*Math.Sin(r),y1=v.X*Math.Sin(r)+v.Y*Math.Cos(r),z1=v.Z;
            double y2=y1*Math.Cos(p)-z1*Math.Sin(p),z2=y1*Math.Sin(p)+z1*Math.Cos(p),x2=x1;
            double x3=x2*Math.Cos(y)+z2*Math.Sin(y),z3=-x2*Math.Sin(y)+z2*Math.Cos(y);
            return new Vector3((float)x3,(float)y2,(float)z3);
        }
        /// <summary>The nominal body's shoulders' middle for a pose (from the feet, in the body's frame).</summary>
        public static Vector3 ShouldersAt(in DogMalletPose p)
        {
            float sq=Math.Max(.3f,p.Squash);return new Vector3(0,Pivot*sq+p.Lift,0)+Turn(new Vector3(0,Shoulders*sq,0),p.Twist+p.Spin,p.Bend,p.Lean);
        }
        /// <summary>A shoulder of the nominal body (`right` or the left).</summary>
        public static Vector3 ShoulderAt(in DogMalletPose p,bool right)
        {
            float sq=Math.Max(.3f,p.Squash);return new Vector3(0,Pivot*sq+p.Lift,0)+Turn(new Vector3(right?HalfWidth:-HalfWidth,Shoulders*sq,0),p.Twist+p.Spin,p.Bend,p.Lean);
        }
        /// <summary>The right fist's place for a pose, from the feet in the body's frame.</summary>
        public static Vector3 GripAt(in DogMalletPose p)=>ShouldersAt(p)+Turn(p.Grip,p.Twist+p.Spin,0,0);
        /// <summary>The handle's way in the body's frame.</summary>
        public static Vector3 HandleAt(in DogMalletPose p)=>Turn(p.Handle,p.Twist+p.Spin,0,0);
        public static Vector3 FaceAt(in DogMalletPose p)=>Turn(p.Face,p.Twist+p.Spin,0,0);
        /// <summary>The head's middle for a pose on the nominal body (the floor brought in as the presentation brings it).</summary>
        public static Vector3 HeadAt(in DogMalletPose p,float scale=1)
        {
            var g=GripAt(p);var d=FloorHandle(g,HandleAt(p),Turn(Vector3.UnitZ,p.Twist+p.Spin,0,0),p.Floor,scale);return g+d*Length*scale;
        }
        /// <summary>The handle tipped about its grip `g` so the head (Length * scale along it) rests on the floor (its radius over y = 0) by
        /// `floor`; its way across the floor kept. Only a handle out in front (along `ahead`, level) is brought down: one still swinging over
        /// from behind is left to come over first.</summary>
        public static Vector3 FloorHandle(Vector3 g,Vector3 handle,Vector3 ahead,float floor,float scale=1)
        {
            if(floor<=1e-4f)return handle;float len=Length*scale;var flat=new Vector3(handle.X,0,handle.Z);float across=flat.Length();if(across<1e-4f)return handle;flat/=across;
            var level=new Vector3(ahead.X,0,ahead.Z);if(level.LengthSquared()<1e-6f)return handle;
            // A correction only (at most about 12 degrees), coming in as the head nears the floor in front: never a jump.
            float want=(Head*scale-g.Y)/len,now=handle.Y,near=1-Clamp01((g.Y+now*len-Head*scale)/(.8f*scale));
            float w=Clamp01(floor)*near*Clamp01(Vector3.Dot(handle,Vector3.Normalize(level))*1.5f);if(w<=1e-4f)return handle;
            float y=now+Math.Max(-.2f,Math.Min(.2f,want-now))*w;y=Math.Max(-.99f,Math.Min(.99f,y));return flat*(float)Math.Sqrt(1-y*y)+Vector3.UnitY*y;
        }
        internal static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-6f?v/l:fallback;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;

        // ---- for the checks ----
        /// <summary>A move's hit moment on its own clock (the swings' and the dash's keyed hit; the slams' landing strike).</summary>
        public static float HitKey(Move m)=>m==Move.Dash?.273f:m==Move.Slam?SlamLand[1].T:m==Move.Smash?SmashLand[1].T:m==Move.Intro?5.0f:0;
        /// <summary>A move's pose at its own clock `t` (from its hit for the swings; from its start for the dash and the air phases; from the
        /// landing for `landed`), weight 1: for the checks.</summary>
        public static DogMalletPose At(Move m,float t,bool landed=false)
        {
            switch(m)
            {
                case Move.Swing1:return Keyed(Forehand,t,m);
                case Move.Swing2:return Keyed(Backhand,t,m);
                case Move.Swing3:return Keyed(JumpSlam,t,m);
                case Move.Dash:return Keyed(DashKeys,t,m);
                case Move.Slam:return Keyed(landed?SlamLand:SlamAir,t,m);
                case Move.Smash:return Keyed(landed?SmashLand:SmashAir,t,m);
                case Move.Intro:return Keyed(IntroSlam,t,m);
            }
            return default;
        }
        /// <summary>A move's key moments (for the checks: the fists' reach at each).</summary>
        public static float[] KeyTimes(Move m,bool landed=false)
        {
            Key[] k=m==Move.Swing1?Forehand:m==Move.Swing2?Backhand:m==Move.Swing3?JumpSlam:m==Move.Dash?DashKeys:m==Move.Slam?(landed?SlamLand:SlamAir):m==Move.Smash?(landed?SmashLand:SmashAir):m==Move.Intro?IntroSlam:new Key[0];
            var t=new float[k.Length];for(int i=0;i<k.Length;i++)t[i]=k[i].T;return t;
        }
    }
}
