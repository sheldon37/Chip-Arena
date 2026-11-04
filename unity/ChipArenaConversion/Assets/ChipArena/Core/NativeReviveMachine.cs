using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The hub's REVIVE-O-MATIC (docs/REVIVE-MACHINE.md). This file: the record the presentation reads, and every moment of the machine's
    // performance as pure functions of the clock, so a late joiner and every peer draw the same machine. Engine-free. Nothing here decides who
    // can be revived or takes money: the host writes the record (NativeReviveMachine.Board), and NativeReviveMachinePreview writes it the
    // same way for the Dev Sandbox.

    /// <summary>A dead pilot on the machine's list. Empty text is "not reported": the screen says so rather than inventing a cause.</summary>
    [Serializable] public sealed class NativeReviveDead
    {
        public string Account,Name,Weapon;
        /// <summary>The seat the pilot is drawn under, for the spit.</summary>
        public int Seat;
        /// <summary>The layout area the body lies in (ExpeditionViewMember.area), its display name from the catalogue, and a place inside it (a room or street), if known.</summary>
        public string Area,AreaName,Place;
        /// <summary>The body's point in that area. Located: the rules reported one.</summary>
        public V3 Body;public bool Located;
        /// <summary>What killed them: a line a person would say ("Chewed on by a shambler"). Killer is the name of whoever did it, if a person.</summary>
        public string Cause,Killer;
        /// <summary>The run clock's seconds when they went down (-1: not reported).</summary>
        public float DiedAt=-1;
        /// <summary>Connected: a disconnected pilot can't be spat out (the rules' Reprint needs them connected).</summary>
        public bool Connected=true;
    }
    /// <summary>One revive in progress. At: when the money landed, on the arena clock; everything else follows from it.</summary>
    [Serializable] public sealed class NativeReviveSpit
    {
        public int Id,Seat;public string Account;public float At;
        /// <summary>Where the pilot ends up (feet, world): the host's free spot on the mat, where it places the capsule at At + Spit.</summary>
        public V3 Land;
    }
    /// <summary>The whole of what the machine shows. The host (or the preview) keeps it current; the presentation only reads it.</summary>
    public sealed class NativeReviveBoard
    {
        public NativeReviveDead[] Dead=Array.Empty<NativeReviveDead>();
        public NativeReviveSpit[] Spits=Array.Empty<NativeReviveSpit>();
        /// <summary>The fixed price, and what the viewing pilot has.</summary>
        public int Cost=NativeReviveMachine.Price,Wallet;
        /// <summary>Why the viewer can't revive anyone right now, whoever they pick (null: they can). Shown over the list.</summary>
        public string Blocked;
        /// <summary>The Dev Sandbox's stand-in: labelled on the screen and sends nothing.</summary>
        public bool Preview;
    }

    public static partial class NativeReviveMachine
    {
        /// <summary>The owner's fixed price (2026-11-04). The host charges it; the presentation only shows it.</summary>
        public const int Price=250;
        /// <summary>What the presentation reads this frame. Null: no machine state (it stands idle with an empty list).</summary>
        public static NativeReviveBoard Board;
        /// <summary>The spit in progress for this seat at `time` (null: none).</summary>
        public static NativeReviveSpit SpitOf(NativeReviveBoard b,int seat,float time)
        {
            if(b?.Spits==null)return null;foreach(var s in b.Spits)if(s!=null&&s.Seat==seat&&time>=s.At&&time<s.At+End)return s;return null;
        }
        /// <summary>The revive the machine is performing at `time`: the newest started one still running.</summary>
        public static NativeReviveSpit Current(NativeReviveBoard b,float time)
        {
            NativeReviveSpit best=null;if(b?.Spits==null)return null;foreach(var s in b.Spits)if(s!=null&&time>=s.At&&time<s.At+End&&(best==null||s.At>best.At))best=s;return best;
        }

        // ---- the performance, in seconds from the money landing ------------------------------------------------------------------
        /// <summary>Coins up the tube; the flywheel starts; it starts to shake; it holds its breath; PTOOEY; the pilot's first touch; the second; the skid stops; on their feet; the doors shut; over.</summary>
        public const float CoinsEnd=1.15f,SpinFrom=.3f,ShakeFrom=.55f,ShakePeak=2.4f,Gulp=2.42f,Spit=3f,Flight=.85f,Bounce=.46f,Touch=Spit+Flight,Touch2=Touch+Bounce,SkidEnd=Touch2+.42f,RollAt=SkidEnd+.18f,RollEnd=RollAt+.42f,UpAt=RollEnd+.3f,ShutFrom=5.5f,Shut=5.95f,End=6.6f;
        /// <summary>The rules hold the pilot's input until here (the get-up ends): the pose is the machine's until then.</summary>
        public const float HoldUntil=UpAt;

        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Smooth(float v){v=Clamp01(v);return v*v*(3-2*v);}
        private static float Out(float v){v=Clamp01(v);return 1-(1-v)*(1-v);}
        // A damped wobble: starts at 1 and rings down at `hz` over `decay`.
        private static float Ring(float age,float hz,float decay)=>age<0?0:(float)(Math.Exp(-age*decay)*Math.Cos(age*hz*Math.PI*2));
        // Deterministic jitter in -1..1 from two incommensurate sines.
        private static float Noise(float t,float a,float b)=>(float)(Math.Sin(t*a)*.6+Math.Sin(t*b+1.3)*.4);

        /// <summary>How hard it is shaking, 0..1.</summary>
        public static float ShakeLevel(float t)
        {
            if(t<ShakeFrom)return 0;if(t<Gulp)return (float)Math.Pow(Clamp01((t-ShakeFrom)/(ShakePeak-ShakeFrom)),1.6);if(t<Spit)return .12f;float a=t-Spit;return .8f*(float)Math.Exp(-a*2.2f);
        }
        /// <summary>The hull's offset from rest: where it is on its springs, its lean and its shudder.</summary>
        public static Matrix4x4 HullPose(float t,float clock)
        {
            float breathe=.012f*(float)Math.Sin(clock*1.3f),lift=breathe,pitch=.006f*(float)Math.Sin(clock*.9f),roll=.003f*(float)Math.Sin(clock*.73f),yaw=0,slide=0,side=0;
            if(t>=0)
            {
                float sh=ShakeLevel(t);lift+=.05f*sh*Noise(t,71,97);pitch+=.03f*sh*Noise(t,83,61);roll+=.045f*sh*Noise(t,59,113);yaw+=.012f*sh*Noise(t,47,101);side+=.035f*sh*Noise(t,67,89);
                // The wind-up crouches as the pressure builds. Then the gulp: it rears back and sinks onto its back springs, and holds. Then the spit: it lunges forward, hard, and rings down on its springs.
                float crouch=Smooth((t-1.2f)/1.2f)*(1-Smooth((t-Gulp)/.2f));lift-=.06f*crouch;
                float rear=Smooth((t-Gulp)/.4f)*(1-Smooth((t-Spit)/.03f));pitch-=.17f*rear;lift-=.05f*rear;slide-=.05f*rear;
                if(t>=Spit)
                {
                    float a=t-Spit;pitch+=.24f*Ring(a,2.1f,2.8f);lift+=.15f*Ring(a,2.7f,3.4f)*(a<.08f?a/.08f:1);slide+=.1f*Ring(a,1.6f,3f)*(a<.05f?a/.05f:1);roll+=.03f*(float)(Math.Sin(a*19)*Math.Exp(-a*3));
                }
            }
            var p=NativeReviveMachineModels.HullPivot;
            return Matrix4x4.CreateTranslation(-p)*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateRotationZ(roll)*Matrix4x4.CreateRotationX(pitch)*Matrix4x4.CreateTranslation(p+new Vector3(side,lift,slide));
        }
        /// <summary>The flywheel's angle (radians, positive drives the crank pin over the top toward the mouth): idling, spun up, ringing down.</summary>
        public static float WheelAngle(float t,float clock)
        {
            float idle=clock*.7f;if(t<0)return idle;float u=Clamp01((t-SpinFrom)/(Spit-SpinFrom));
            // Integral of speed = 34 u^2 rad/s from SpinFrom to Spit, then 34 exp(-a * 1.1).
            float span=Spit-SpinFrom,build=34*u*u*u/3*span;if(t<=Spit)return idle+build;float a=t-Spit;return idle+build+34*(1-(float)Math.Exp(-a*1.1f))/1.1f;
        }
        /// <summary>The cheeks' inflation, 0..1 and a little over as they go.</summary>
        public static float Cheeks(float t)
        {
            if(t<0)return 0;if(t<Spit)return Smooth((t-1)/(Gulp+.1f-1))*(1+.04f*(float)Math.Sin(t*30));float a=t-Spit;return Math.Max(0,.9f*Ring(a,4.2f,7)*(float)Math.Exp(-a*4));
        }
        /// <summary>The lower jaw's angle (radians about x, positive falls forward), with its bounce and its slam.</summary>
        public static float JawAngle(float t)
        {
            if(t<Spit){float k=t>2.1f&&t<Spit?(t-2.1f)/(Spit-2.1f):0;return .012f*k*(float)Math.Sin(t*90);}
            const float open=1.72f;float a=t-Spit;if(t<ShutFrom){float go=Out(a/.07f);return open*go+.13f*Ring(a-.07f,4.5f,5)*(a>.07f?1:0)-(a>.07f?.13f*Ring(0,4.5f,5)*(float)Math.Exp(-a*5):0);}
            float s=Clamp01((t-ShutFrom)/(Shut-ShutFrom));s=s*s*s;float ring=t>=Shut?.05f*Ring(t-Shut,8,8):0;return open*(1-s)+ring;
        }
        /// <summary>The upper lip's angle (radians about x, negative swings up and out).</summary>
        public static float VisorAngle(float t)
        {
            if(t<Spit){float k=t>2.1f&&t<Spit?(t-2.1f)/(Spit-2.1f):0;return .012f*k*(float)Math.Sin(t*83+1);}
            const float open=-1.95f;float a=t-Spit;if(t<ShutFrom){float go=Out(a/.08f);return open*go-.18f*Ring(a-.08f,5.5f,5.5f)*(a>.08f?1:0)*(float)Math.Min(1,(a-.08f)*14);}
            float s=Clamp01((t-ShutFrom-.06f)/(Shut-ShutFrom));s=s*s*s;float ring=t>=Shut+.06f?-.05f*Ring(t-Shut-.06f,8,8):0;return open*(1-s)+ring;
        }
        /// <summary>How far the tongue has unrolled, 0..1 (over 1 as it whips), and back as it is slurped in.</summary>
        public static float TongueOut(float t)
        {
            if(t<Spit)return 0;float a=t-Spit;if(t<4.9f)return Out(a/.2f)+.06f*Ring(a-.2f,6,7)*(a>.2f?1:0);return 1-Smooth((t-4.9f)/.5f);
        }
        /// <summary>Each link's bend from the one before (radians about x; positive droops, negative curls up), at unrolled share `u`: the party blower.</summary>
        public static float TongueBend(int i,float u)
        {
            int n=NativeReviveMachineModels.TongueLinks;float wave=Smooth(u*(n+2)-i);const float curl=-1.08f;float flat=i<3?0:i<6?.2f:.1f;return curl+(flat-curl)*wave;
        }
        /// <summary>A gauge's pressure: 0 the bottom of the scale, 1 the red end's stop. The right eye (eye 1) lags the left and overshoots more: they don't agree.</summary>
        public static float Pressure(float t,float clock,int eye)
        {
            float idle=.14f+.02f*(float)Math.Sin(clock*(1.7f+eye*.6f)+eye);if(t<0)return idle;float lag=eye*.12f;
            if(t<Spit)
            {
                float k=Smooth((t-.35f-lag)/(ShakePeak-.35f));return idle+(.92f+.05f*eye-idle)*k+.05f*ShakeLevel(t)*Noise(t,120+eye*11,93);
            }
            float a=t-Spit;float spin=(eye==0?1:-1)*(float)(5.4*(1-Math.Exp(-a*2.4)));float settle=Smooth((a-.9f)/1.4f);return (1-settle)*(.9f+spin)+settle*idle+(1-settle)*.05f*(float)Math.Sin(a*14);
        }
        /// <summary>A needle's turn about its hub (radians about z): pressure p on the dial's 270 degrees, clockwise from the viewer.</summary>
        public static float NeedleTurn(float p)=>(float)(-Math.PI/4+p*1.5*Math.PI-Math.PI/2);
        /// <summary>The brows' tilt (radians: positive lowers the inner end) and lift (metres).</summary>
        public static void Brows(float t,out float tilt,out float lift)
        {
            tilt=.06f;lift=0;if(t<0)return;float anger=Smooth((t-.4f)/1.8f);tilt+=.36f*anger;lift-=.05f*anger;
            if(t>=Spit){float a=t-Spit;float shock=Math.Max(0,(float)Math.Exp(-a*1.6f));tilt=tilt*(1-Smooth(a/.05f))+(.06f-.42f*shock)*Smooth(a/.05f);lift=lift*(1-Smooth(a/.05f))+(.14f*shock+.015f*(float)Math.Sin(a*22)*shock)*Smooth(a/.05f);}
        }
        /// <summary>How far the stack's cap has left its seat (metres): popped by the spit, thrown up on its spring, ringing back down.</summary>
        public static float CapLift(float t)
        {
            if(t<Spit)return t>=Gulp?.015f*(1+(float)Math.Sin(t*60)):0;float a=t-Spit-.02f;if(a<0)return 0;return 1.15f*(float)(Math.Exp(-a*1.9)*Math.Abs(Math.Sin(a*5.2+.001)))+0f;
        }
        /// <summary>The nose: honked as it gulps, and once more as it goes.</summary>
        public static float Honk(float t)
        {
            float H(float at)=>t>=at&&t<at+.22f?(float)Math.Sin((t-at)/.22f*Math.PI):0;return Math.Max(H(.08f),Math.Max(H(Gulp-.02f),H(Spit-.2f)));
        }
        /// <summary>The console's lever: thrown as the money lands, sprung back after the spit (0 up, 1 down).</summary>
        public static float Lever(float t)=>t<0?0:Out(t/.14f)*(1-Smooth((t-Spit-.2f)/.4f));
        /// <summary>The mat's give at `t`: 1 flat out when the pilot lands on it, ringing back.</summary>
        public static float MatDent(float t)
        {
            float a=t-Touch,b=t-Touch2;return (a>=0?(float)(Math.Exp(-a*7)*Math.Abs(Math.Cos(a*14))):0)+.6f*(b>=0?(float)(Math.Exp(-b*8)*Math.Abs(Math.Cos(b*14))):0);
        }
        // ---- what glows ----
        /// <summary>The gullet's heat: a low flicker idling, roaring as it builds, a flash at the spit.</summary>
        public static float Heat(float t,float clock)
        {
            float idle=.35f+.08f*(float)Math.Sin(clock*7)+.05f*(float)Math.Sin(clock*17.3f);if(t<0)return idle;if(t<Spit)return idle+2.2f*Smooth((t-.5f)/2.2f)+.3f*ShakeLevel(t)*Noise(t,77,131);float a=t-Spit;return idle+6*(float)Math.Exp(-a*5)+.6f*(float)Math.Exp(-a*.9);
        }
        /// <summary>The marquee's bulbs (bank 0 or 1): an idle chase, faster and brighter through the wind-up, all flashing at the spit.</summary>
        public static float Bulbs(int bank,float t,float clock)
        {
            float rate=2.2f,level=.9f;if(t>=0&&t<Spit){rate+=16*Smooth(t/Spit);level=1.3f;}else if(t>=Spit){float a=t-Spit;if(a<1.1f)return (int)Math.Floor(a*11)%2==0?2.4f:.5f;rate=2.2f+6*(float)Math.Exp(-(a-1.1f));}
            bool on=(int)Math.Floor((clock*rate)%2)==bank;return on?level:.18f;
        }
        /// <summary>The marquee's lettering: steady, with a bad contact; it dips through the wind-up as the machine drinks the power.</summary>
        public static float Sign(float t,float clock)
        {
            float flick=Noise(clock,23,57)>.86f?.55f:1;if(t<0||t>=Spit+.2f)return flick;return (.55f+.45f*(float)Math.Abs(Math.Sin(t*19)))*flick+(t>=Spit?1.6f:0);
        }
        /// <summary>The machine's small lamps.</summary>
        public static float Lamps(float t,float clock)=>t>=0&&t<Spit?1.4f+.6f*(float)Math.Sin(t*26):.9f+.1f*(float)Math.Sin(clock*3);

        // ---- the money ----
        public const int Coins=14;
        /// <summary>Coin i's way up the tube: its point along the path 0..1 (below 0: not sent; over 1: gone), at t.</summary>
        public static float CoinProgress(int i,float t)=>t<0?-1:(t-.04f-i*.052f)/.46f;
        /// <summary>A point along the tube (machine frame), 0..1.</summary>
        public static Vector3 CoinAt(float u)
        {
            var p=NativeReviveMachineModels.CoinPath;u=Clamp01(u)*(p.Length-1);int i=Math.Min(p.Length-2,(int)u);float k=u-i;
            Vector3 a=p[Math.Max(0,i-1)],b=p[i],c=p[i+1],d=p[Math.Min(p.Length-1,i+2)];float k2=k*k,k3=k2*k;return .5f*((2*b)+(c-a)*k+(2*a-5*b+4*c-d)*k2+(3*b-a-3*c+d)*k3);
        }
        // ---- the bolts the spit shakes loose ----
        public const int Bolts=4;
        /// <summary>Bolt i's point (machine frame) and its tumble, while it flies and bounces; false before the spit or once it has come to rest long enough.</summary>
        public static bool BoltAt(int i,float t,out Vector3 at,out float spin)
        {
            at=default;spin=0;float a=t-Spit-.01f*i;if(a<0||t>=End)return false;
            float vx=(i%2==0?1:-1)*(1.3f+.5f*i),vz=2.6f+.7f*i,vy=3.8f+.7f*(i%3);var from=new Vector3(i%2==0?.82f:-.82f,1.9f-.12f*i,1.44f);
            // Bounces: the time to the floor, then half the speed.
            float x=from.X,y=from.Y,z=from.Z,left=a;for(int k=0;k<4;k++)
            {
                float td=(vy+(float)Math.Sqrt(vy*vy+2*9.8f*Math.Max(0,y-.02f)))/9.8f;if(left<td){y=y+vy*left-4.9f*left*left;x+=vx*left;z+=vz*left;spin=a*(14+i*3);at=new Vector3(x,y,z);return true;}
                x+=vx*td;z+=vz*td;left-=td;y=.02f;vy=-(vy-9.8f*td)*.45f;vx*=.6f;vz*=.6f;
            }
            at=new Vector3(x,.02f,z);spin=(i+1)*3;return true;
        }

        // ---- the pilot ----
        /// <summary>The mouth, where the pilot leaves (machine frame).</summary>
        public static readonly Vector3 Mouth=new Vector3(0,1.36f,1.32f);
        /// <summary>Where the pilot lands if the rules give no other spot (machine frame, feet).</summary>
        public static Vector3 DefaultLand=>NativeReviveMachineModels.MatAt+new Vector3(0,0,.1f);
        /// <summary>Where a spat pilot's rig root is drawn and how it has turned: `at`, in the same frame as `mouth` (the mouth's point; the root leaves RootCentre under it) and `land` (the feet at rest; `half` lifts them to the root
        /// the rules' capsule gives); `tumble` the turn end over end about the travel's right (radians, 0 at rest), `roll` about the travel. False before the spit (the pilot is not there yet) and once the pose is the rules' again.</summary>
        public static bool Flying(float t,Vector3 mouth,Vector3 land,float half,out Vector3 at,out float tumble,out float roll)
        {
            at=default;tumble=0;roll=0;if(t<Spit||t>=UpAt)return false;
            var rest=land+new Vector3(0,half,0);mouth.Y-=RootCentre;var path=rest-mouth;path.Y=0;
            Vector3 first=mouth+path*.68f,second=mouth+path*.9f;first.Y=rest.Y+.2f;second.Y=rest.Y+.02f;
            if(t<Touch)
            {
                float k=(t-Spit)/Flight;at=Vector3.Lerp(mouth,first,k);at.Y=mouth.Y+(first.Y-mouth.Y)*k+2.5f*4*k*(1-k)*.5f;
                // Two and a quarter turns on the way out, ending flat on its back on the mat.
                tumble=-(1-k)*(1-k*.25f)*(float)(Math.PI*4.5);roll=.55f*(float)Math.Sin(k*Math.PI*2.5);return true;
            }
            if(t<Touch2)
            {
                float k=(t-Touch)/Bounce;at=Vector3.Lerp(first,second,k);at.Y=first.Y+(second.Y-first.Y)*k+.82f*4*k*(1-k);tumble=-(1-k)*(float)(Math.PI*2);roll=.3f*(float)Math.Sin(k*Math.PI);return true;
            }
            // The skid: decelerating along the mat to its rest.
            {float k=Out((t-Touch2)/(SkidEnd-Touch2));at=Vector3.Lerp(second,rest,k);return true;}
        }
        /// <summary>The rig root's height under the body's centre (Actor.Position is the capsule's centre less this).</summary>
        public const float RootCentre=.865f;
        /// <summary>The knockdown's own moments for a spat pilot (Actor.Down*): held in its flight pose until the last touch, lies a beat, rolls and gets up. So both bodies take the game's pose for it.</summary>
        public static void DownTimes(float at,out float downAt,out float landed,out float pickup,out float rollAt,out float rollEnd,out float until)
        {downAt=at+Spit;landed=at+Touch2;pickup=at+SkidEnd;rollAt=at+RollAt;rollEnd=at+RollEnd;until=at+UpAt;}

        // ---- the parts in the machine's frame ----
        /// <summary>Every part of the machine at `t` (seconds from the money, below 0: idling) and clock (any steadily running seconds): model ids and their matrices in the machine's frame (System.Numerics row vectors). The
        /// list is reused by the caller; the draw adds no allocation.</summary>
        public static void PoseParts(float t,List<(string id,Matrix4x4 m)> o,float clock=0)
        {
            o.Clear();
            var I=Matrix4x4.Identity;o.Add((NativeReviveMachineModels.Skid,I));o.Add((NativeReviveMachineModels.Lane,I));
            var hull=HullPose(t,clock);o.Add((NativeReviveMachineModels.Hull,hull));
            // The springs, from their cups on the skid to the hull's underside at that corner.
            foreach(var at in NativeReviveMachineModels.SpringAt)
            {
                var top=Vector3.Transform(new Vector3(at.X,NativeReviveMachineModels.SpringTo,at.Z),hull);float h=top.Y-NativeReviveMachineModels.SpringFrom;
                o.Add((NativeReviveMachineModels.Spring,Matrix4x4.CreateScale(1,h,1)*Matrix4x4.CreateTranslation(top.X,NativeReviveMachineModels.SpringFrom,top.Z)));
            }
            Matrix4x4 On(Vector3 pivot)=>Matrix4x4.CreateTranslation(pivot)*hull;
            o.Add((NativeReviveMachineModels.Jaw,Matrix4x4.CreateRotationX(JawAngle(t<0?0:t))*On(NativeReviveMachineModels.JawHinge)));
            o.Add((NativeReviveMachineModels.Visor,Matrix4x4.CreateRotationX(VisorAngle(t<0?0:t))*On(NativeReviveMachineModels.VisorHinge)));
            // The tongue: links chained from its root; each adds its bend. Stowed (rolled up behind the shut mouth) it draws nothing the viewer can see, so it is skipped.
            float u=t<0?0:TongueOut(t);
            if(t>=Spit-.02f&&t<ShutFrom+.2f)
            {
                var link=On(NativeReviveMachineModels.TongueRoot);int n=NativeReviveMachineModels.TongueLinks;
                for(int i=0;i<n;i++)
                {
                    link=Matrix4x4.CreateRotationX(TongueBend(i,u))*link;
                    o.Add((i==n-1?NativeReviveMachineModels.TongueTip:NativeReviveMachineModels.Tongue,link));
                    link=Matrix4x4.CreateTranslation(0,0,NativeReviveMachineModels.TongueLink)*link;
                }
            }
            // The flywheel, the crank's pin and the side rod and piston it drives.
            var w=NativeReviveMachineModels.WheelAxle;float th=WheelAngle(t,clock);o.Add((NativeReviveMachineModels.Flywheel,Matrix4x4.CreateRotationX(th)*On(w)));
            float pinY=w.Y+(float)Math.Sin(-th)*NativeReviveMachineModels.CrankRadius*-1,pinZ=w.Z+(float)Math.Cos(th)*NativeReviveMachineModels.CrankRadius;float cross=pinZ+(float)Math.Sqrt(Math.Max(.01f,NativeReviveMachineModels.RodLength*NativeReviveMachineModels.RodLength-(pinY-w.Y)*(pinY-w.Y)));
            float tilt=(float)Math.Atan2(pinY-w.Y,cross-pinZ);
            o.Add((NativeReviveMachineModels.Rod,Matrix4x4.CreateRotationX(tilt)*On(new Vector3(NativeReviveMachineModels.RodX,pinY,pinZ))));
            o.Add((NativeReviveMachineModels.Piston,On(new Vector3(NativeReviveMachineModels.RodX,w.Y,cross))));
            // The eyes' needles, the brows, the cheeks (the far one turned, not mirrored), the cap and the spring it rides on, the nose.
            for(int e=0;e<2;e++)o.Add((NativeReviveMachineModels.Needle,Matrix4x4.CreateRotationZ(NeedleTurn(Pressure(t,clock,e)))*On(NativeReviveMachineModels.EyeAt[e])));
            Brows(t,out float tiltB,out float liftB);
            for(int e=0;e<2;e++){var b=NativeReviveMachineModels.BrowAt[e];o.Add((NativeReviveMachineModels.Brow,Matrix4x4.CreateRotationZ(e==0?tiltB:-tiltB)*On(b+new Vector3(0,liftB,0))));}
            float ck=Cheeks(t);
            for(int e=0;e<2;e++){var at=NativeReviveMachineModels.CheekAt[e];var sc=Matrix4x4.CreateScale(1+.62f*ck,1+.3f*ck,1+.3f*ck);o.Add((NativeReviveMachineModels.Cheek,sc*(e==0?I:Matrix4x4.CreateRotationY((float)Math.PI))*On(at)));}
            float cap=t<0?0:CapLift(t);var seat=NativeReviveMachineModels.CapAt;o.Add((NativeReviveMachineModels.Cap,Matrix4x4.CreateRotationZ(.5f*cap*(float)Math.Sin(t*17))*Matrix4x4.CreateRotationX(.4f*cap*(float)Math.Sin(t*13+1))*On(seat+new Vector3(0,cap,0))));
            if(cap>.02f)o.Add((NativeReviveMachineModels.Spring,Matrix4x4.CreateScale(.55f,cap,.55f)*On(seat)));
            float honk=t<0?0:Honk(t);o.Add((NativeReviveMachineModels.Bulb,Matrix4x4.CreateScale(1+.3f*honk,1+.3f*honk,1-.5f*honk)*On(NativeReviveMachineModels.BulbAt)));
            // The console and its lever stay on the floor; the mat gives under the pilot.
            var console=Matrix4x4.CreateRotationY(NativeReviveMachineModels.ConsoleYaw)*Matrix4x4.CreateTranslation(NativeReviveMachineModels.ConsoleAt);o.Add((NativeReviveMachineModels.Console,console));
            o.Add((NativeReviveMachineModels.Lever,Matrix4x4.CreateRotationX(.5f-1.25f*(t<0?0:Lever(t)))*Matrix4x4.CreateTranslation(NativeReviveMachineModels.LeverAt)*console));
            float dent=t<0?0:MatDent(t);o.Add((NativeReviveMachineModels.Mat,Matrix4x4.CreateScale(1+.03f*dent,1-.75f*dent,1+.03f*dent)*Matrix4x4.CreateTranslation(NativeReviveMachineModels.MatAt)));
            // Coins in the tube and the bolts in the air.
            if(t>=0&&t<CoinsEnd+.3f)for(int i=0;i<Coins;i++){float c=CoinProgress(i,t);if(c<0||c>1.02f)continue;var at=CoinAt(c);o.Add((NativeReviveMachineModels.Coin,Matrix4x4.CreateRotationX(t*19+i)*Matrix4x4.CreateRotationZ(t*11+i*2)*Matrix4x4.CreateTranslation(at)));}
            if(t>=Spit)for(int i=0;i<Bolts;i++)if(BoltAt(i,t,out var bat,out float spin))o.Add((NativeReviveMachineModels.Bolt,Matrix4x4.CreateRotationX(spin)*Matrix4x4.CreateRotationZ(spin*.7f)*Matrix4x4.CreateTranslation(bat)));
        }
        // ---- where it stands ----
        /// <summary>Where the machine stands on a hub map (yaw radians, model +z toward the plaza). False: no machine on this map. The generated hub's sanctuary is 49.6 m square with its gates on the axes and the party's slots at its middle; the authored town's is 24 m.</summary>
        public static bool Spot(string mapId,out Vector3 at,out float yaw)
        {
            if(mapId==NativeExpeditionGeneratedWorld.MapId){at=new Vector3(-13.5f,0,13.5f);yaw=(float)(Math.PI*.75);return true;}
            if(mapId=="expedition-city"){at=new Vector3(8.2f,0,8.2f);yaw=(float)(Math.PI*1.25);return true;}
            at=default;yaw=0;return false;
        }
        /// <summary>The machine's frame in the world (System.Numerics row vectors).</summary>
        public static Matrix4x4 Frame(Vector3 at,float yaw)=>Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(at);
        /// <summary>The solid boxes: the machine and its console (machine frame: centre, size, yaw about y). The mat and the lane are flat and do not collide.</summary>
        public static readonly (Vector3 centre,Vector3 size,float yaw)[] Solids=
        {
            (new Vector3(.1f,1.75f,-.08f),new Vector3(3.84f,3.5f,3.42f),0),
            (NativeReviveMachineModels.ConsoleAt+new Vector3(0,.7f,0),new Vector3(1.3f,1.4f,.96f),NativeReviveMachineModels.ConsoleYaw),
        };
        /// <summary>Where the pilot stands to use it: in front of the console (machine frame), and how far from there the prompt shows.</summary>
        public static Vector3 UsePoint=>NativeReviveMachineModels.ConsoleAt+new Vector3(.2f,0,1.15f);
        public const float UseRange=2.6f;
        /// <summary>A view member as a row: what the replicated view already says. The cause and the body's spot are not on the wire yet (the rules' part: docs/REVIVE-MACHINE.md), so they stay empty and the screen says so.</summary>
        public static NativeReviveDead FromMember(ExpeditionViewMember m)
        {
            if(m==null)return null;
            return new NativeReviveDead{Account=m.account,Name=string.IsNullOrEmpty(m.name)?m.account:m.name,Weapon=m.weapon,Seat=m.seat,Area=m.area,AreaName=AreaName(m.area),Connected=m.connected};
        }
        /// <summary>An area's display name, the HUD's own.</summary>
        public static string AreaName(string area)=>string.IsNullOrEmpty(area)?"SOMEWHERE OUT THERE":area=="exp-city"?NativeExpeditionJoinZones.Hub:NativeExpeditionJoinZones.KnownName(area)??area.ToUpperInvariant();
        /// <summary>A point in the machine's frame in the console's frame.</summary>
        public static Vector3 ConsoleLocal(Vector3 machine)
        {
            var d=machine-NativeReviveMachineModels.ConsoleAt;float c=(float)Math.Cos(-NativeReviveMachineModels.ConsoleYaw),s=(float)Math.Sin(-NativeReviveMachineModels.ConsoleYaw);
            return new Vector3(d.X*c+d.Z*s,d.Y,-d.X*s+d.Z*c);
        }
    }
    /// <summary>
    /// The Dev Sandbox's "Preview Revive Machine": the machine stood in front of you with a list of stand-in dead, written the way the host will write the record, so the screen, the machine, the pilot's flight and the sound are the real ones.
    /// It charges nobody and revives nobody: Request plays the part of the host. Auto runs it by itself through every state of the screen: three dead and paying, paying again, too poor, nobody dead.
    /// </summary>
    public sealed class NativeReviveMachinePreview
    {
        /// <summary>The stand-in pilot's id (the rig that is spat out).</summary>
        public const int StandIn=990900;
        public readonly NativeReviveBoard Board=new NativeReviveBoard{Preview=true};
        public Vector3 At;public float Yaw;public bool Placed;
        public int Cycle;private float nextAt=-1;private float scriptAt=-1;private int spitId;
        /// <summary>Auto's wish this frame: the screen shown, and which row it wants pressed (-1 none).</summary>
        public bool WantOpen;public int WantPress=-1;
        private static NativeReviveDead Row(string name,string weapon,string area,string areaName,string place,string cause,string killer,float x,float z,float died)=>
            new NativeReviveDead{Account=name.ToLowerInvariant(),Name=name,Weapon=weapon,Seat=StandIn,Area=area,AreaName=areaName,Place=place,Cause=cause,Killer=killer,Body=new V3(x,0,z),Located=true,DiedAt=died,Connected=true};
        public void Reset()
        {
            Board.Dead=new[]{
                Row("KESTREL","greatsword","exp-ward-zero","WARD ZERO","East ward, by the vending machine","Bitten by a shambler. Repeatedly.",null,34.5f,-12,214),
                Row("NOVA","pistol","exp-transit","CENTRAL STATION","Platform 2, on the tracks","Hit by the express. It was on time.",null,-41,8,260),
                Row("BISCUIT","rifle","exp-backrooms","THE BACKROOMS","Somewhere in the yellow","Lost an argument with a Hound",null,62,88,301),
            };
            Board.Spits=Array.Empty<NativeReviveSpit>();Board.Wallet=600;Board.Cost=NativeReviveMachine.Price;Board.Blocked=null;Cycle=0;nextAt=-1;scriptAt=-1;WantOpen=false;WantPress=-1;
        }
        public void Place(Vector3 feet,float facingRadians)
        {
            // Ten metres ahead, turned to face you, so the whole of it and the mat are in view.
            var f=new Vector3((float)Math.Sin(facingRadians),0,(float)Math.Cos(facingRadians));At=feet+f*13;At.Y=feet.Y;Yaw=facingRadians+(float)Math.PI;Placed=true;
        }
        /// <summary>The host's part: takes the money, starts the show. Null: done; else why not.</summary>
        public string Request(string account,float time)
        {
            var row=Array.Find(Board.Dead,d=>d.Account==account);if(row==null)return "They're not on the list.";if(NativeReviveMachine.Current(Board,time)!=null)return "The machine is busy.";if(Board.Wallet<Board.Cost)return "Not enough money.";
            Board.Wallet-=Board.Cost;Board.Dead=Array.FindAll(Board.Dead,d=>d!=row);
            var land=Vector3.Transform(NativeReviveMachine.DefaultLand,NativeReviveMachine.Frame(At,Yaw));Board.Spits=new[]{new NativeReviveSpit{Id=++spitId,Seat=StandIn,Account=account,At=time+.05f,Land=new V3(land.X,land.Y,land.Z)}};return null;
        }
        /// <summary>Auto's script, a step a frame: open the list, read it for a moment, press, watch; then the next state.</summary>
        public void Script(float time)
        {
            WantPress=-1;if(NativeReviveMachine.Current(Board,time)!=null){WantOpen=false;scriptAt=-1;return;}
            if(nextAt<0)nextAt=time+1.2f;if(time<nextAt)return;
            WantOpen=true;if(scriptAt<0)scriptAt=time;float read=time-scriptAt;
            if(Board.Dead.Length==0||Board.Wallet<Board.Cost)
            {
                if(read>3){WantOpen=false;scriptAt=-1;nextAt=time+.8f;if(Board.Dead.Length==0)Reset();else{Board.Dead=Array.Empty<NativeReviveDead>();}}
                return;
            }
            if(read>2.2f){WantPress=0;scriptAt=-1;nextAt=time+NativeReviveMachine.End+1;Cycle++;}
        }
    }
}
