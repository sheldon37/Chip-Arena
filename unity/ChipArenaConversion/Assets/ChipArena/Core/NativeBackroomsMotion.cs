using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Backrooms' entities' own moves over the gait's pose (docs/EXPEDITION-ENEMY-REWORK.md section 4; the authority's names
    // and timings: section 6, Core NativeBackroomsKinds and the zone frame). Each runs on the rig's pose (its frame: +x the body's
    // left, +y up, +z ahead), before the voxel model and the player's model are posed from it:
    // - the Faceling dormant at its wall: bowed toward it, swaying; its wake (the .45 s tell): the head snaps round to the pilot
    //   first, the neck and shoulders after it, until the body (the authority's turn) catches up;
    // - the Hound's howl (.8 s): the head thrown back;
    // - the Lifeguard's whistle (1 s wind-up): the left hand brings the whistle to the mouth and holds it; its rescue hook (.6 s
    //   wind-up, then the drag): the pole raised back over the shoulder, thrust out at the pilot, hauled back;
    // - the Drowned under the water (submerged), and its surfacing: how far its body sinks.
    // The blows (the claw, the pounce, the lunges, the pole's sweep) are the gait's own takes at the authority's timings
    // (BackroomsAttack in the presentation). Presentation only: nothing here is read by a rule, a hit volume or the network.
    public static class NativeBackroomsMotion
    {
        /// <summary>The Hound's kind: it runs on all fours (the gait's quadruped).</summary>
        public static readonly int HoundKind=NativeHordeKinds.Resolve("hound");
        public static bool Quad(int kind)=>kind>=0&&kind==HoundKind;
        /// <summary>The Faceling's wake tell (the authority's .45 s) and how long its head stays ahead of the body after it.</summary>
        public const float WakeTell=.45f,WakeSettle=.35f;
        /// <summary>The Hound's howl, the Lifeguard's whistle wind-up and hook wind-up and drag (the authority's).</summary>
        public const float Howl=.8f,Whistle=1f,HookWindup=.6f,HookDrag=.5f;
        /// <summary>How far the Drowned sinks under the water's surface while submerged (m, rig units), and how long it takes to rise.</summary>
        public const float DrownedSink=1.25f,Surface=.4f;

        private static void Bend(CapturedRig.Pose p,int bone,float x,float y,float z){if(x==0&&y==0&&z==0)return;p.Rotation[bone]=Quaternion.Normalize(CapturedRig.Euler(x,y,z)*p.Rotation[bone]);}
        private static float S(float t)=>t<=0?0:t>=1?1:t*t*(3-2*t);

        /// <summary>The Faceling at rest facing its wall: bowed toward it, swaying slowly on its own clock.</summary>
        public static void Dormant(CapturedRig.Pose p,uint seed,float time)
        {
            float ph=(seed%997)*.0137f,sway=(float)Math.Sin(time*1.9f+ph),drift=(float)Math.Sin(time*.7f+ph*2);
            Bend(p,CapturedRig.Chest,.16f+.03f*drift,0,.05f*sway);Bend(p,CapturedRig.Neck,.22f,0,.04f*sway);Bend(p,CapturedRig.Head,.12f,.06f*drift,0);p.World();
        }
        /// <summary>The Faceling's wake, `age` s after it began: the head snaps round to `look` (the pilot's direction in the rig's
        /// frame, flat) in .08 s with a small overshoot, the neck and the chest follow, all handing back to the body after the tell.</summary>
        public static float Wake(CapturedRig.Pose p,float age,Vector3 look)
        {
            if(!(age>=0)||age>WakeTell+WakeSettle||look.LengthSquared()<1e-4f)return 0;
            float yaw=(float)Math.Atan2(look.X,look.Z);yaw=Math.Max(-2.4f,Math.Min(2.4f,yaw));
            float snap=age<.08f?S(age/.08f)*1.12f:age<.16f?1.12f-.12f*S((age-.08f)/.08f):1;
            float release=age<WakeTell?1:1-S((age-WakeTell)/WakeSettle);
            Bend(p,CapturedRig.Chest,0,yaw*.2f*S(age/.3f)*release,0);Bend(p,CapturedRig.Neck,-.1f*snap*release,yaw*.35f*snap*release,0);Bend(p,CapturedRig.Head,-.08f*snap*release,yaw*.45f*snap*release,0);
            p.World();return release;
        }
        /// <summary>The Hound's howl, `age` s in: the head thrown back, the chest lifted, then down again.</summary>
        public static void HowlPose(CapturedRig.Pose p,float age)
        {
            if(!(age>=0)||age>Howl+.3f)return;float k=age<.2f?S(age/.2f):age<Howl?1:1-S((age-Howl)/.3f);
            Bend(p,CapturedRig.Chest,-.32f*k,0,0);Bend(p,CapturedRig.Neck,-.6f*k,0,0);Bend(p,CapturedRig.Head,-.5f*k,0,0);p.World();
        }
        /// <summary>The Hound's jaw through its howl (0..1: wide open while it holds), over the gait's.</summary>
        public static float HowlJaw(float age)=>!(age>=0)||age>Howl+.3f?0:age<.15f?S(age/.15f):age<Howl?1:1-S((age-Howl)/.3f);
        /// <summary>The Lifeguard's whistle, `age` s after its wind-up began: the left hand to the mouth, held, lowered; its weight.</summary>
        public static float WhistlePose(CapturedRig.Pose p,float age)
        {
            if(!(age>=0)||age>Whistle+.6f)return 0;float k=age<.25f?S(age/.25f):age<Whistle+.2f?1:1-S((age-Whistle-.2f)/.4f);
            p.World();var head=p.WorldPosition[CapturedRig.Head];var at=head+Vector3.Transform(new Vector3(-.01f,.07f,.16f),p.WorldRotation[CapturedRig.Head]);
            Reach(p,true,at,new Vector3(.8f,-.5f,.1f),k);Bend(p,CapturedRig.Head,-.1f*k,0,0);p.World();return k;
        }
        /// <summary>The Lifeguard's rescue hook, `age` s after its wind-up began, at `target` (the pilot, rig frame): the pole raised
        /// back over the shoulder through the wind-up, thrust out at the pilot at its end, hauled back through the drag; its weight.</summary>
        public static float HookPose(CapturedRig.Pose p,float age,Vector3 target)
        {
            float end=HookWindup+HookDrag+.4f;if(!(age>=0)||age>end)return 0;
            p.World();var shoulder=p.WorldPosition[CapturedRig.RArm];var aim=target-shoulder;aim.Y=Math.Max(-.3f,Math.Min(.6f,aim.Y));
            aim=aim.LengthSquared()>1e-4f?Vector3.Normalize(aim):Vector3.UnitZ;
            var back=shoulder+new Vector3(-.12f,.42f,-.22f);var thrust=shoulder+aim*.62f+new Vector3(0,.05f,0);var haul=shoulder+aim*.25f+new Vector3(0,-.15f,0);
            Vector3 at;float k;
            if(age<HookWindup){float t=S(age/HookWindup);at=Vector3.Lerp(shoulder+new Vector3(-.15f,-.3f,.1f),back,t);k=S(age/.15f);}
            else if(age<HookWindup+.12f){at=Vector3.Lerp(back,thrust,S((age-HookWindup)/.12f));k=1;}
            else if(age<HookWindup+HookDrag){at=Vector3.Lerp(thrust,haul,S((age-HookWindup-.12f)/(HookDrag-.12f)));k=1;}
            else{at=haul;k=1-S((age-HookWindup-HookDrag)/.4f);}
            Reach(p,false,at,new Vector3(-.8f,-.3f,-.4f),k);
            float lean=age<HookWindup?-.12f*S(age/HookWindup):age<HookWindup+HookDrag?.18f:.18f*(1-S((age-HookWindup-HookDrag)/.4f));
            Bend(p,CapturedRig.Spine,lean,0,0);p.World();return k;
        }
        /// <summary>The Hound's pin (the authority's: held .9 s from the grab, bites at .3 and .6 s after the pounce's own): its bites'
        /// times from the grab, how long it holds.</summary>
        public static readonly float[] HoundBites={0,.3f,.6f};public const float PinHold=.9f;
        /// <summary>The Hound on the pilot it pinned, `age` s after the grab, at `target` (the pilot's chest, rig frame): its front reared
        /// up onto them, its forelimbs on their chest, its head driving in on each bite; its weight.</summary>
        public static float HoundPin(CapturedRig.Pose p,float age,Vector3 target)
        {
            if(!(age>=0)||age>PinHold+.3f)return 0;float k=S(age/.12f)*(1-S((age-PinHold)/.3f));if(k<=0)return 0;
            float bite=0;foreach(float b in HoundBites){float x=(age-b)/.07f;bite=Math.Max(bite,(float)Math.Exp(-x*x));}
            Bend(p,CapturedRig.Spine,-.25f*k,0,0);Bend(p,CapturedRig.Chest,-.45f*k,0,0);p.World();
            for(int i=0;i<2;i++){bool left=i==0;var at=target+new Vector3(left?.16f:-.16f,-.05f,-.12f);Reach(p,left,at,new Vector3(left?.6f:-.6f,-.3f,-.7f),k);}
            Bend(p,CapturedRig.Neck,(.3f+.35f*bite)*k,0,0);Bend(p,CapturedRig.Head,(.15f+.3f*bite)*k,0,0);p.World();return k;
        }
        /// <summary>The Hound's jaw (0..1): hanging open, panting as it runs (`running` 0..1), wide before each bite of a pin and snapped
        /// shut on it (`pinAge` s after the grab, NaN none).</summary>
        public static float HoundJaw(float time,float running,float pinAge)
        {
            float jaw=.26f+.14f*running*(.5f+.5f*(float)Math.Sin(time*13));
            if(pinAge>=0&&pinAge<PinHold+.2f)foreach(float b in HoundBites){float x=pinAge-b;if(x>-.22f&&x<0)jaw=Math.Max(jaw,S((x+.22f)/.16f));else if(x>=0&&x<.12f)jaw=Math.Min(jaw,.05f+.5f*S(x/.12f));}
            return jaw;
        }
        /// <summary>How far the Drowned's body sinks now: all the way while submerged, rising over Surface after `surfaceAge`.</summary>
        public static float Sink(bool submerged,float surfaceAge)
            =>submerged?DrownedSink:surfaceAge>=0&&surfaceAge<Surface?DrownedSink*(1-S(surfaceAge/Surface)):0;

        [ThreadStatic] private static Quaternion[] wasStore;
        private static void Reach(CapturedRig.Pose p,bool left,Vector3 target,Vector3 pole,float k)
        {
            if(k<=0)return;k=Math.Min(1,k);var was=wasStore??=new Quaternion[3];int a=left?CapturedRig.LArm:CapturedRig.RArm;for(int i=0;i<3;i++)was[i]=p.Rotation[a+i];
            var inv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            CapturedRig.Arm(p,left,Vector3.Transform(target-p.WorldPosition[CapturedRig.Chest],inv),Quaternion.Normalize(inv*p.WorldRotation[a+2]),pole);
            for(int i=0;i<3;i++)p.Rotation[a+i]=Quaternion.Normalize(Quaternion.Slerp(was[i],p.Rotation[a+i],k));p.World();
        }
    }
}
