using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zombie mocap clips (Core/NativePackZombie.Generated.cs, retargeted from the owner's ZOMBIE PRO pack by
    /// tools/export_pack_zombie.mjs) as the horde's poses: a clip's 64 channels sampled at a time into a row, rows blended,
    /// and a row built into the 22 joints of the rig (CapturedRig's layout: the torso and legs as captured, the arms reached
    /// to their captured chest-space targets). Presentation only; engine-free; no allocation after a clip's first decode.
    /// <list type="bullet">
    /// <item>Cycles (walks, chases, sprints) loop one gait cycle from the left foot's strike; their marks are the natural
    /// ground speed on the rig and the feet's lift and strike phases (<see cref="Speed"/>, <see cref="LeftLift"/>,
    /// <see cref="RightLand"/>, <see cref="RightLift"/>).</item>
    /// <item>Idles loop the pack's standing takes; one-shots play a frame range (attacks, knockbacks, deaths, entrances and
    /// the jump-down).</item>
    /// </list>
    /// </summary>
    public static class NativeZombieClips
    {
        public const int Channels=CapturedClip.Channels;
        public static int Count=>NativePackZombie.Clips.Length;
        public static CapturedClip Clip(int i)=>NativePackZombie.Clips[i];
        public static string Key(int i)=>NativePackZombie.Keys[i];
        /// <summary>The clip with this key (exporter's key, without the "zombie/" prefix), or -1.</summary>
        public static int Find(string key){var k=NativePackZombie.Keys;for(int i=0;i<k.Length;i++)if(string.Equals(k[i],key,StringComparison.Ordinal))return i;return -1;}
        /// <summary>Like <see cref="Find"/>, but a missing key is an error (the tables below name exported clips only).</summary>
        public static int Need(string key){int i=Find(key);if(i<0)throw new InvalidOperationException("No zombie clip "+key);return i;}
        public static float Duration(int i)=>Clip(i).Duration;
        // ---- a cycle's marks (the exporter's: [speed, left lift, right strike, right lift]) ----
        /// <summary>The cycle's natural ground speed on the rig (m/s): its stride over its duration where the pack's feet plant.</summary>
        public static float Speed(int i)=>Clip(i).Marks[0];
        public static float LeftLift(int i)=>Clip(i).Marks[1];
        public static float RightLand(int i)=>Clip(i).Marks[2];
        public static float RightLift(int i)=>Clip(i).Marks[3];

        /// <summary>Samples clip `i` at `time` (s; looped or clamped by the clip) into `row` (64 channels).</summary>
        public static void Sample(int i,float time,float[] row)=>Clip(i).Sample(time,row);
        /// <summary>A loop sampled at a phase of its cycle (0..1).</summary>
        public static void SamplePhase(int i,float phase,float[] row){var c=Clip(i);c.Sample(Frac(phase)*c.Duration,row);}
        /// <summary>into = a + (b - a) t, channel by channel (rotation vectors and reach targets: close rows only).</summary>
        public static void Blend(float[] a,float[] b,float t,float[] into)
        {
            if(t<=0){if(!ReferenceEquals(a,into))Array.Copy(a,into,Channels);return;}if(t>=1){if(!ReferenceEquals(b,into))Array.Copy(b,into,Channels);return;}
            for(int k=0;k<Channels;k++)into[k]=a[k]+(b[k]-a[k])*t;
        }
        private static float Frac(float x)=>x-(float)Math.Floor(x);

        private static readonly int[] Joints={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LFoot,CapturedRig.RFoot};
        private static Vector3 Row(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rotation(float[] f,int i){var v=Row(f,i);float a=v.Length();return a<1e-8f?Quaternion.Identity:Quaternion.CreateFromAxisAngle(v/a,a);}
        /// <summary>The hips' height channel (rig metres from the capture's standing hips; the body sits .025 below it).</summary>
        public static float Hips(float[] f)=>f[2]-.025f;
        /// <summary>The heading channel (rad, about +y): the pelvis's turn from the cycle's travel.</summary>
        public static float Heading(float[] f)=>f[0];
        /// <summary>The hips' sideways and forward sway off the take's first frame (source frame, rig metres).</summary>
        public static Vector2 Sway(float[] f)=>new Vector2(f[1],f[3]);
        /// <summary>
        /// Builds a row into the rig: the body's height, pelvis (with its heading), spine, chest, neck, head, clavicles, hips,
        /// knees and ankles as captured, and both arms reached to their chest-space targets (CapturedRig.Arm). No floor lift: the
        /// caller places the feet. `arms` false leaves the arms at rest.
        /// </summary>
        public static void Pose(float[] f,CapturedRig.Pose p,bool arms=true)
        {
            p.Reset();p.Position[CapturedRig.Body]=new Vector3(0,Hips(f),0);
            p.Rotation[CapturedRig.Pelvis]=Quaternion.CreateFromAxisAngle(Vector3.UnitY,f[0])*Rotation(f,4);
            for(int i=0;i<Joints.Length;i++)p.Rotation[Joints[i]]=Rotation(f,7+i*3);
            p.Rotation[CapturedRig.LKnee]=CapturedRig.Rot(Vector3.UnitX,Math.Max(0,f[37]));p.Rotation[CapturedRig.RKnee]=CapturedRig.Rot(Vector3.UnitX,Math.Max(0,f[38]));
            if(!arms)return;
            Vector3 blade=CapturedRig.Unit(Row(f,42),Vector3.UnitZ),edge=Row(f,45);edge=CapturedRig.Unit(edge-blade*Vector3.Dot(blade,edge),Vector3.UnitX);
            CapturedRig.Arm(p,false,Row(f,39),CapturedRig.Basis(edge,Vector3.Cross(blade,edge),blade),Row(f,48));
            CapturedRig.Arm(p,true,Row(f,51),CapturedRig.Look(Row(f,54),Row(f,57)),Row(f,60));
        }
    }
}
