using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The drop-in from the Deployment Station (docs/EXPEDITION-OVERHAUL.md R8, W3; owner 2026-11-18: "a more realistic drop-in
    /// ... give the players an animation when they fall"; Q22 approved the 5.6 s eject). Presentation only and engine-free: each
    /// client's own clock since it saw Deploy (NativeDeployClock) gives every beat, every pilot's drop, pose and pod, and the
    /// camera's shot, so host and guests see the same eject, each framed on its own pad. TravelAt (NativeDeployPresentation) is
    /// its end, and the host already waits on it before it loads the run: no rule, timing or wire change.
    ///  0-.6 s       the machines dim and the room turns red; the klaxon sounds with the pads' alarm (.15-1.23 s);
    ///  .4 s         every pilot drops into a ready crouch (the pack Jump take's gather, NativePackJumps);
    ///  .9-1.25 s    the hatches part over each occupied pad's lit drop tube;
    ///  1.35 + .09 x pad   each pilot falls through (a startled arm throw from the Jump take into the Fall loop) at 9.8 m/s²,
    ///               only ever down;
    ///  my drop + .35 s    the tube shot: my camera looks straight down my tube, ring lights streaking up, my pilot falling below;
    ///  my landing (2.7 s on pad 0)   the seat: my pilot lands in the seat of the pod docked under the tube (NativeLobbyPoses'
    ///               Chair with a land compression), the harness bar slams down in .15 s, the cabin goes red, the roof hatch
    ///               seals with a bass thump;
    ///  3.4 + .09 x pad   the release (Cut.Full): the clamps fire and each pod drops out of the station's belly over the rolled
    ///               city at night; Cut.Short keeps every other beat but holds the seat until the rush;
    ///  4.8-5.6 s    (Full; Short 3.3-4 s) a white rush, then the fade to the cradle card.
    /// The pods hang under the belly at two depths, alternating along the pad arc, so the 2.24 m pods clear each other under
    /// pads 1.53 m apart (Deep). Nothing here allocates.
    /// </summary>
    public static class NativeDeployEject
    {
        /// <summary>The full eject (5.6 s, the release shot) or the 4 s variant (the same beats without the release shot: the
        /// first to cut if pacing drags, plan R8).</summary>
        public enum Cut{Full,Short}
        /// <summary>The eject the live station plays (and so TravelAt, which the host waits on).</summary>
        public const Cut Played=Cut.Full;
        public const float FullTravel=5.6f,ShortTravel=4f;
        public static float TravelOf(Cut c)=>c==Cut.Short?ShortTravel:FullTravel;

        // ---- the beats (seconds after Deploy) ----
        /// <summary>The room's red over the first RedSeconds; the crouch from CrouchAt over CrouchSeconds; the console's panels
        /// fade from HudAt over HudSeconds (before the tube shot cuts in).</summary>
        public const float RedSeconds=.6f,CrouchAt=.4f,CrouchSeconds=.45f,HudAt=1.05f,HudSeconds=.45f;
        /// <summary>The fall: each pilot lands SeatSeconds after its drop starts on a shallow pod (2.7 s on pad 0), SeatDrop below
        /// its pad's top; a deep pod hangs DeepStep lower.</summary>
        public const float SeatSeconds=1.35f,SeatDrop=.5f*NativeDeployPresentation.DropGravity*SeatSeconds*SeatSeconds,DeepStep=3.4f;
        /// <summary>The tube shot starts this long after my drop; the harness bar starts down HarnessAt after the landing and is
        /// down HarnessSeconds later; the roof hatch seals SealAt after the landing over SealSeconds.</summary>
        public const float TubeLead=.35f,HarnessAt=.05f,HarnessSeconds=.15f,SealAt=.34f,SealSeconds=.2f;
        /// <summary>The release (Full): pod `pad`'s clamps fire at ReleaseAt + ReleaseStagger x pad and swing open over
        /// ClampSeconds, then the pod drops at ReleaseSpeed plus gravity.</summary>
        public const float ReleaseAt=3.4f,ReleaseStagger=.09f,ClampSeconds=.14f,ReleaseSpeed=3f;
        /// <summary>The white rush (its rise) and the fade to the card (its length), by cut.</summary>
        public const float RushAt=4.8f,ShortRushAt=3.3f,RushSeconds=.4f,FadeSeconds=.45f,ShortFadeSeconds=.6f;
        /// <summary>The pod's cabin floor over its base (NativePodModels.Floor) and its height, for the docked pod's frame.</summary>
        public const float CabinFloor=NativePodModels.Floor,PodHeight=NativePodModels.Height;
        /// <summary>The drop tube (station frame): its inner radius and its top just under the floor plates.</summary>
        public const float TubeRadius=.46f,TubeTop=-.03f;
        /// <summary>The tube shot: the eye's height over the pad's top as it starts and the share of my pilot's drop it follows
        /// (so my pilot falls away below it); the release shot's eye beside my pod.</summary>
        public const float TubeEye=2.6f,TubeFollow=.74f,ReleaseOut=4.4f,ReleaseRise=1.1f;
        /// <summary>The rolled city under the station (the blueprint drawn like Preview): its level and scale.</summary>
        public const float CityY=-140f,CityScale=.6f;

        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static int PadIndex(int pad)=>Math.Max(0,Math.Min(NativeDeployPresentation.Pads-1,pad));
        /// <summary>A brief bump 0..1..0 over `span` s from 0 (a land, a jolt).</summary>
        public static float Bump(float t,float span)=>!(t>0)||t>=span?0:(float)Math.Sin(Math.PI*t/span);

        // ---- each pad's tube, pod and pilot ----
        /// <summary>Which way pad `pad`'s pilot faces (the room's front, a little toward the middle): its pod's door too.</summary>
        public static Vector3 Facing(int pad){var p=NativeDeployPresentation.Pad(pad);return Vector3.Normalize(new Vector3(-p.X*.35f,0,-1));}
        /// <summary>That facing as a turn about y (radians; System.Numerics CreateRotationY takes +z to it).</summary>
        public static float Yaw(int pad){var f=Facing(pad);return (float)Math.Atan2(f.X,f.Z);}
        /// <summary>Whether pad `pad`'s pod hangs deep: the pads alternate along the arc (its step out from the host's is odd).</summary>
        public static bool Deep(int pad)=>((PadIndex(pad)+1)/2&1)==1;
        /// <summary>How far below its pad's top pad `pad`'s pilot lands (feet on the cabin floor).</summary>
        public static float Landing(int pad)=>SeatDrop+(Deep(pad)?DeepStep:0);
        public static float DropStart(int pad)=>NativeDeployPresentation.DropAt+NativeDeployPresentation.DropStagger*PadIndex(pad);
        /// <summary>When pad `pad`'s pilot lands in its seat.</summary>
        public static float LandAt(int pad)=>DropStart(pad)+(float)Math.Sqrt(2*Landing(pad)/NativeDeployPresentation.DropGravity);
        /// <summary>The last of the eight pads' landings (the party's drop is over).</summary>
        public static float LastLanding{get{float t=0;for(int i=0;i<NativeDeployPresentation.Pads;i++)t=Math.Max(t,LandAt(i));return t;}}
        /// <summary>When pad `pad`'s clamps fire: ReleaseAt + ReleaseStagger x pad, never before its pilot is sealed in (a deep pod's
        /// pilot lands later).</summary>
        public static float ReleaseOf(int pad)=>Math.Max(ReleaseAt+ReleaseStagger*PadIndex(pad),LandAt(pad)+SealAt+SealSeconds+.2f);
        public static float PodDropOf(int pad)=>ReleaseOf(pad)+ClampSeconds;
        /// <summary>How far pad `pad`'s pod has dropped from its dock `since` s after Deploy (0 docked; Short never releases).</summary>
        public static float PodFall(int pad,float since,Cut cut)
        {
            if(cut==Cut.Short||!(since>=0))return 0;float r=since-PodDropOf(pad);return r<=0?0:ReleaseSpeed*r+.5f*NativeDeployPresentation.DropGravity*r*r;
        }
        /// <summary>How far pad `pad`'s pilot has dropped below its pad's top: the fall at 9.8 m/s² to its seat, then with its pod.
        /// Never negative, never less than a moment before (pilots only ever move down).</summary>
        public static float Drop(int pad,float since,Cut cut)
        {
            if(!(since>=0))return 0;float t=since-DropStart(pad);if(t<=0)return 0;
            float fall=.5f*NativeDeployPresentation.DropGravity*t*t,land=Landing(pad);
            return fall<land?fall:land+PodFall(pad,since,cut);
        }
        /// <summary>The docked pod's base (station y): its cabin floor where the pilot lands.</summary>
        public static float PodBase(int pad)=>NativeDeployModels.PadTop-Landing(pad)-CabinFloor;
        /// <summary>The docked pod's roof (where its tube meets it).</summary>
        public static float PodRoof(int pad)=>PodBase(pad)+PodHeight;
        /// <summary>Pad `pad`'s tube length from TubeTop down to its pod's roof.</summary>
        public static float TubeLength(int pad)=>TubeTop-PodRoof(pad);
        /// <summary>Pad `pad`'s pod frame (base at the origin, doorway toward +z) in the station, `since` s after Deploy.</summary>
        public static Matrix4x4 PodRoot(int pad,float since,Cut cut)
        {
            var p=NativeDeployPresentation.Pad(pad);return Matrix4x4.CreateRotationY(Yaw(pad))*Matrix4x4.CreateTranslation(p.X,PodBase(pad)-PodFall(pad,since,cut),p.Z);
        }
        /// <summary>The docked frame (no fall), for the parts the pod leaves behind (the clamps, the collar).</summary>
        public static Matrix4x4 DockRoot(int pad)=>PodRoot(pad,0,Cut.Short);

        // ---- the moving parts ----
        /// <summary>Pad `pad`'s harness bar (0 up, 1 down on the chest).</summary>
        public static float Harness(int pad,float since){float a=since-LandAt(pad)-HarnessAt;return !(a>0)?0:a>=HarnessSeconds?1:Smooth(a/HarnessSeconds)*Smooth(a/HarnessSeconds);}
        /// <summary>Pad `pad`'s roof hatch (0 open, 1 sealed over its pilot).</summary>
        public static float Seal(int pad,float since){float a=since-LandAt(pad)-SealAt;return !(a>0)?0:Smooth(a/SealSeconds);}
        /// <summary>Pad `pad`'s clamps (0 shut on the pod, 1 swung open), and the release's flash (0..1, a quick burst).</summary>
        public static float Clamps(int pad,float since,Cut cut){if(cut==Cut.Short)return 0;float a=since-ReleaseOf(pad);return !(a>0)?0:Smooth(a/ClampSeconds);}
        public static float ReleaseFlash(int pad,float since,Cut cut){if(cut==Cut.Short)return 0;float a=since-ReleaseOf(pad);return !(a>=0)||a>.3f?0:a<.03f?a/.03f:(1-(a-.03f)/.27f)*(1-(a-.03f)/.27f);}
        /// <summary>The room's red (0..1), the console's panels (1 shown, 0 gone), the white rush (0..1) and the fade to the card.</summary>
        public static float Red(float since)=>!(since>=0)?0:Smooth(since/RedSeconds);
        public static float Hud(float since)=>!(since>=0)?1:1-Smooth((since-HudAt)/HudSeconds);
        public static float Rush(float since,Cut cut){if(!(since>=0))return 0;float at=cut==Cut.Short?ShortRushAt:RushAt;return Smooth((since-at)/RushSeconds);}
        public static float Fade(float since,Cut cut){if(!(since>=0))return 0;float len=cut==Cut.Short?ShortFadeSeconds:FadeSeconds;return Smooth((since-(TravelOf(cut)-len))/len);}
        /// <summary>The tube's ring-light chase: the rings are three banks lit in turn (NativeDeployEjectModels.TubeOf: ring k is
        /// bank k mod 3), so a pulse leaves the tube's foot every ChasePeriod s and runs up it a ring at a time; bank `bank`'s
        /// brightness (0..1) `since` s after Deploy.</summary>
        public const float ChasePeriod=.12f;
        public static float RingChase(int bank,float since)
        {
            if(!(since>=NativeDeployPresentation.HatchAt))return 0;float x=((bank+since/ChasePeriod*3)%3+3)%3;x=x>1.5f?3-x:x*1.6f;
            float g=1-x;return g<=0?0:g*g;
        }

        // ---- the camera ----
        /// <summary>The shot `since` s after Deploy for the viewer on pad `mine`: the party (the room's Deploy shot) through the
        /// crouch, the hatches and the first drops; my tube from my drop + TubeLead; my seat from my landing; the release from my
        /// clamps (Full; Short holds the seat).</summary>
        public static NativeDeployPresentation.Shot ShotOf(int mine,float since,Cut cut)
        {
            if(!(since>=0)||since<DropStart(mine)+TubeLead)return NativeDeployPresentation.Shot.Deploy;
            if(since<LandAt(mine))return NativeDeployPresentation.Shot.Tube;
            if(cut==Cut.Full&&since>=ReleaseOf(mine)-.06f)return NativeDeployPresentation.Shot.Release;
            return NativeDeployPresentation.Shot.Seat;
        }
        /// <summary>Whether the shot cuts in (no ease from the last one): every eject shot but the room's.</summary>
        public static bool Cuts(NativeDeployPresentation.Shot s)=>s==NativeDeployPresentation.Shot.Tube||s==NativeDeployPresentation.Shot.Seat||s==NativeDeployPresentation.Shot.Release;
        /// <summary>The view for an eject shot on pad `mine` at `since` (the room's shots: NativeDeployPresentation.ShotOf).</summary>
        public static NativeDeployPresentation.View ViewOf(NativeDeployPresentation.Shot shot,int mine,float since,Cut cut)
        {
            var pad=NativeDeployPresentation.Pad(mine);var f=Facing(mine);
            switch(shot)
            {
                case NativeDeployPresentation.Shot.Tube:
                {
                    // straight down my tube from a little behind its axis, following my pilot at TubeFollow of its drop
                    float y=NativeDeployModels.PadTop+TubeEye-TubeFollow*Drop(mine,since,cut);var eye=new Vector3(pad.X,y,pad.Z)-f*.12f;
                    return new NativeDeployPresentation.View{Eye=eye,Target=eye+new Vector3(0,-4,0)+f*.62f,Fov=64};
                }
                case NativeDeployPresentation.Shot.Seat:
                {
                    // in my pod's doorway, close on the seat: my pilot lands, the bar slams down, the hatch seals overhead
                    var root=PodRoot(mine,since,cut);float jolt=.035f*Bump(since-LandAt(mine),.22f);
                    var eye=Vector3.Transform(new Vector3(.06f,CabinFloor+1.3f-jolt,.58f),root);var at=Vector3.Transform(new Vector3(0,CabinFloor+1.02f-jolt*.5f,-.08f),root);
                    return new NativeDeployPresentation.View{Eye=eye,Target=at,Fov=62};
                }
                case NativeDeployPresentation.Shot.Release:
                {
                    // outboard of the arc beside my docked pod, under the belly, watching it drop away over the city; the eye
                    // tips down after it and, in the rush, plunges with it
                    var outward=new Vector3(pad.X-NativeDeployPresentation.Table.X,0,pad.Z-NativeDeployPresentation.Table.Z);outward=outward.LengthSquared()>1e-4f?Vector3.Normalize(outward):-Vector3.UnitZ;
                    var side=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,f));var dock=new Vector3(pad.X,PodBase(mine)+PodHeight*.5f,pad.Z);
                    float rush=Rush(since,cut),plunge=rush*rush*26;
                    var eye=dock+outward*ReleaseOut+side*1.3f+new Vector3(0,ReleaseRise-plunge,0);
                    var pod=dock-new Vector3(0,PodFall(mine,since,cut),0);var at=Vector3.Lerp(pod,new Vector3(pod.X,CityY,pod.Z),.18f+.25f*rush);
                    return new NativeDeployPresentation.View{Eye=eye,Target=at,Fov=58};
                }
                default:return NativeDeployPresentation.ShotOf(shot);
            }
        }

        // ---- the pilots' poses (the deploy cast; Runtime/NativeDeployCast.cs draws them on every body) ----
        /// <summary>What pad `pad`'s pilot is doing `since` s after Deploy, and for how long it has been (s).</summary>
        public enum Act{Stand,Crouch,Fall,Seat}
        public static Act ActOf(int pad,float since,out float age)
        {
            age=0;if(!(since>=CrouchAt))return Act.Stand;
            float drop=DropStart(pad),land=LandAt(pad);
            if(since<drop){age=since-CrouchAt;return Act.Crouch;}
            if(since<land){age=since-drop;return Act.Fall;}
            age=since-land;return Act.Seat;
        }
        /// <summary>The fall's arm throw: the Jump take from its takeoff to its fall entry over ThrowSeconds, into the Fall loop
        /// from LoopAt over LoopBlend; the seat's catch from the fall over CatchSeconds and its compression (m, rig hips).</summary>
        public const float ThrowSeconds=.34f,LoopAt=.1f,LoopBlend=.3f,CatchSeconds=.09f,Compression=.085f,CompressionSeconds=.3f;
        /// <summary>Where the seated pilot's hands grip the harness bar once it is down (chest space, +x the rig's left).</summary>
        public static readonly Vector3 GripLeft=new Vector3(.2f,-.2f,.33f),GripRight=new Vector3(-.2f,-.2f,.33f);
        /// <summary>The pack set a spec's eject plays (its jump set; the Siren and any spec without one: Unarmed).</summary>
        public static int SetFor(string spec){int s=NativeJumpRules.SetFor(spec);return s<0?NativeJumpRules.Unarmed:s;}

        /// <summary>One pilot's eject pose buffers (reused per pad; nothing allocates once made).</summary>
        public sealed class Pilot
        {
            public readonly MeleeRig Rig=new MeleeRig();
            public readonly CapturedRig.Pose Lobby=new CapturedRig.Pose(),Pack=new CapturedRig.Pose(),Seat=new CapturedRig.Pose();
            public readonly float[] Row=new float[CapturedClip.Channels],Row2=new float[CapturedClip.Channels];
        }
        /// <summary>
        /// Pad `pad`'s pilot (`spec`, lobby pose index `lobby` as the cast resolved it) `since` s after Deploy (NaN: not deployed)
        /// at portrait time `time` into `pose` (local; World() run): the lobby pose until the crouch; the Jump take's gather;
        /// the fall (the take's arm throw into the Fall loop); the Chair pose with a land compression, the hands taking the
        /// harness bar once it is down. The held weapon shows as in the lobby until the seat (a cut hides it holstering).
        /// </summary>
        public static void Sample(Pilot e,CapturedRig.Pose pose,string spec,int lobby,int pad,float since,float time,bool reduced,out LobbyHeld weapon,out LobbyHeld offhand)
        {
            float breath=reduced?0:time+pad*1.37f;
            e.Rig.Reset();NativeLobbyPoses.Sample(e.Rig,spec,lobby,breath,out weapon,out offhand);e.Rig.WritePose(e.Lobby);
            var act=ActOf(pad,since,out float age);
            if(act==Act.Stand){Copy(e.Lobby,pose);return;}
            int set=SetFor(spec);var jump=NativeJumpRules.Clip(set,NativeJumpRules.Jump);var loop=NativeJumpRules.Clip(set,NativeJumpRules.Fall);
            if(jump==null||loop==null||jump.Marks.Length<4){Copy(e.Lobby,pose);return;}
            float g=GatherAt(e,set,jump),m3=jump.Marks[3];
            if(act==Act.Crouch)
            {
                float u=Smooth(age/CrouchSeconds);jump.Sample(g,e.Row);CapturedRig.Sample(e.Row,set,e.Pack);Blend(e.Lobby,e.Pack,u,pose);return;
            }
            if(act==Act.Fall){FallPose(e,jump,loop,set,g,m3,age);Copy(e.Pack,pose);return;}
            // the seat: the fall as it was at the landing, caught into the Chair over CatchSeconds
            FallPose(e,jump,loop,set,g,m3,LandAt(pad)-DropStart(pad));
            e.Rig.Reset();NativeLobbyPoses.Sample(e.Rig,spec,NativeLobbyPoses.Chair,breath,out _,out _);
            float press=Compression*Bump(age,CompressionSeconds),grip=Smooth((age-HarnessAt-HarnessSeconds*.4f)/.16f);
            var b=e.Rig.P[CapturedRig.Body];e.Rig.P[CapturedRig.Body]=new Vector3(b.X,b.Y-press,b.Z);
            e.Rig.AddE(CapturedRig.Spine,press*2.4f,0,0);e.Rig.AddE(CapturedRig.Neck,press*2.8f,0,0);
            if(grip>1e-3f)
            {
                var q=NativeLobbyPoses.Aim(0,-.35f,1);
                e.Rig.Hand(true,GripLeft,q,grip,new Vector3(1,-.6f,-.4f));e.Rig.Hand(false,GripRight,q,grip,new Vector3(-1,-.6f,-.4f));
            }
            e.Rig.WritePose(e.Seat);Blend(e.Pack,e.Seat,Smooth(age/CatchSeconds),pose);
            weapon.Visible=false;offhand.Visible=false;
        }
        // The Jump take's gather: its deepest crouch before the push-off (its hips' lowest up to the takeoff mark), once per set.
        private static readonly float[] gather=new float[16];private static readonly bool[] gathered=new bool[16];
        private static float GatherAt(Pilot e,int set,CapturedClip jump)
        {
            if(set<0||set>=gather.Length)return 0;if(gathered[set])return gather[set];
            float best=0,low=float.MaxValue,end=Math.Max(jump.Marks[1],1/30f);
            for(int k=0;k<=16;k++){float t=end*k/16;jump.Sample(t,e.Row);CapturedRig.Sample(e.Row,set,e.Pack);float y=e.Pack.WorldPosition[CapturedRig.Pelvis].Y;if(y<low){low=y;best=t;}}
            gather[set]=best;gathered[set]=true;return best;
        }
        // The fall at `age` s into it: the Jump take's arm throw (its gather through the push-off to the fall entry), into the Fall loop.
        private static void FallPose(Pilot e,CapturedClip jump,CapturedClip loop,int set,float m1,float m3,float age)
        {
            jump.Sample(m1+(m3-m1)*Smooth(age/ThrowSeconds),e.Row);float w=Smooth((age-LoopAt)/LoopBlend);
            if(w>1e-4f){loop.Sample(age,e.Row2);MeleeCapture.BlendRowsTurning(e.Row,e.Row2,w,e.Row);}
            CapturedRig.Sample(e.Row,set,e.Pack);
        }
        private static void Copy(CapturedRig.Pose a,CapturedRig.Pose o)
        {
            for(int i=0;i<CapturedRig.Bones.Length;i++){o.Position[i]=a.Position[i];o.Rotation[i]=a.Rotation[i];}o.World();
        }
        /// <summary>Blends two local poses bone by bone (positions lerped, rotations slerped) into `o` and runs its World().</summary>
        public static void Blend(CapturedRig.Pose a,CapturedRig.Pose b,float w,CapturedRig.Pose o)
        {
            w=Clamp01(w);
            for(int i=0;i<CapturedRig.Bones.Length;i++){o.Position[i]=Vector3.Lerp(a.Position[i],b.Position[i],w);o.Rotation[i]=MeleeMath.Slerp(a.Rotation[i],b.Rotation[i],w);}
            o.World();
        }
    }
}
