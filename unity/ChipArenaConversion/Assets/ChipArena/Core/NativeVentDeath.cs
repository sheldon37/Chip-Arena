using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Ward Zero vent kill effects (client/pve/vent-fall.ts at 05950ea, VENT_FX; timing from shared/pve/vent-pits.ts
    /// VENT_PIT) as pure functions of age on the captured rig, in source (three.js) axes:
    /// <list type="bullet">
    /// <item>'vent-fall': the victim keeps its last pose for an instant (DeathPoseCarry over .1 s), throws its arms up and drops
    /// into the shaft (24 m/s² after a .1 s stumble), flailing (sin(age * 31)) and spinning (group yaw age * 1.6); hidden once
    /// 4 m below the floor.</item>
    /// <item>'vent-splat': VENT_PIT.drop (.6 s) of falling from 13 m face down, spread-eagle and stretched (scale y 1 + .32 fall),
    /// limbs flapping; on impact a squash to .34 that wobbles (exp(-7 t) cos(26 t)) onto the flat .62 rest, a seeded dust and
    /// voxel puff (26, or 12 at reduced effects; every fifth voxel in the player's colour) and a SPLAT! pop; the kept body
    /// holds the pose from VENT_PIT.drop + 1.3 s.</item>
    /// </list>
    /// HP, hitboxes, timing and the authority are untouched; nothing here is read by the simulation or the network frame.
    /// </summary>
    public static class NativeVentDeath
    {
        public const float Stumble=.1f,Gravity=24,HideBelow=4,Height=13,Lie=.17f,Squash=.34f,Rest=.62f,Drop=.6f,KeepAge=Drop+1.3f,CarrySeconds=.1f,FloorOffset=.85f;
        public const int DustHigh=26,DustLow=12,DustColor=0x8d8597;public const float DustSpeed=3.4f,DustLife=.9f,PopLife=1.25f,PopSize=1.5f,PopHeight=1.3f,PopRise=.45f;
        /// <summary>The SPLAT! canvas (256 x 128, 900 70px HOLO_FONT, rotated -.12 about its centre): a 14 px #2b1030 stroke
        /// 4 px low, the #ffd23a fill, a 3 px #fff6d0 stroke.</summary>
        public const int PopWidth=256,PopHeightPx=128,PopFontPx=70,PopShadow=0x2b1030,PopFill=0xffd23a,PopLine=0xfff6d0;public const float PopAngle=-.12f,PopShadowWidth=14,PopShadowDrop=4,PopLineWidth=3;
        public static readonly float SplatTilt=(float)(Math.PI/2);
        private static float Clamp01(float x)=>Math.Min(1,Math.Max(0,x));
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        /// <summary>cinematic-victim.ts ease (smootherstep).</summary>
        public static float Ease(float x){x=Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
        /// <summary>ventFall frame before the carry: <paramref name="pose"/> reset to rest and the scripted joints written; the
        /// root drops <paramref name="drop"/> metres, is shown while above the hide depth, and the rig group turns
        /// <paramref name="spin"/> radians about y.</summary>
        public static void Fall(float age,CapturedRig.Pose pose,out float drop,out bool visible,out float spin)
        {
            pose.Reset();float t=Math.Max(0,age-Stumble),flail=(float)Math.Sin(age*31);drop=.5f*Gravity*t*t;visible=drop<HideBelow;spin=age*1.6f;
            pose.Rotation[CapturedRig.LArm]=MeleeMath.Euler(-.3f,0,2.5f+.35f*flail);pose.Rotation[CapturedRig.RArm]=MeleeMath.Euler(-.3f,0,-2.5f+.35f*flail);
            pose.Rotation[CapturedRig.LElbow]=MeleeMath.Euler(-.4f,0,0);pose.Rotation[CapturedRig.RElbow]=MeleeMath.Euler(-.4f,0,0);
            pose.Rotation[CapturedRig.LHip]=MeleeMath.Euler(-.5f+.6f*flail,0,0);pose.Rotation[CapturedRig.RHip]=MeleeMath.Euler(-.5f-.6f*flail,0,0);
            pose.Rotation[CapturedRig.LKnee]=MeleeMath.Euler(.7f,0,0);pose.Rotation[CapturedRig.RKnee]=MeleeMath.Euler(.7f,0,0);
            pose.Rotation[CapturedRig.Head]=MeleeMath.Euler(-.45f,0,0);
        }
        /// <summary>DeathPoseCarry.apply: blend every joint (and the body position) from the captured death pose into the
        /// scripted one over <see cref="CarrySeconds"/>.</summary>
        public static void Carry(CapturedRig.Pose pose,Quaternion[] from,Vector3 fromBody,float age)
        {
            float w=Ease(age/CarrySeconds);if(w>=1)return;
            for(int i=0;i<CapturedRig.Bones.Length;i++)pose.Rotation[i]=MeleeMath.Slerp(from[i],pose.Rotation[i],w);
            pose.Position[CapturedRig.Body]=Vector3.Lerp(fromBody,pose.Position[CapturedRig.Body],w);
        }
        /// <summary>ventSplat frame: the face-down spread-eagle pose (the rig group is tilted <see cref="SplatTilt"/> about x),
        /// the root's height above the lying spot (<paramref name="lift"/>, 0 once landed) and its squash and stretch.</summary>
        public static void Splat(float age,CapturedRig.Pose pose,out float lift,out float scaleXZ,out float scaleY,out bool landed)
        {
            pose.Reset();float fall=Clamp01(age/Drop),since=age-Drop;landed=since>=0;float flap=landed?0:(float)Math.Sin(age*26)*.25f;
            pose.Rotation[CapturedRig.LArm]=MeleeMath.Euler(0,0,1.35f+flap);pose.Rotation[CapturedRig.RArm]=MeleeMath.Euler(0,0,-1.35f-flap);
            pose.Rotation[CapturedRig.LElbow]=MeleeMath.Euler(-.15f,0,0);pose.Rotation[CapturedRig.RElbow]=MeleeMath.Euler(-.15f,0,0);
            pose.Rotation[CapturedRig.LHip]=MeleeMath.Euler(0,0,.42f-flap);pose.Rotation[CapturedRig.RHip]=MeleeMath.Euler(0,0,-.42f+flap);
            pose.Rotation[CapturedRig.LKnee]=MeleeMath.Euler(.1f,0,0);pose.Rotation[CapturedRig.RKnee]=MeleeMath.Euler(.1f,0,0);
            pose.Rotation[CapturedRig.Head]=MeleeMath.Euler(landed?-.25f:-.6f,0,0);
            if(!landed){lift=Height*(1-fall*fall);float st=1+.32f*fall;scaleY=st;scaleXZ=1/(float)Math.Sqrt(st);}
            else{lift=0;float y=Rest+(Squash-Rest)*(float)Math.Exp(-since*7)*(float)Math.Cos(since*26);scaleY=y;scaleXZ=1/(float)Math.Sqrt(Math.Max(.2f,y));}
        }
        public static int DustCount(float quality)=>quality<.5f?DustLow:DustHigh;
        public struct Dust{public float X,Z,K,Up,Size,Spin;}
        /// <summary>dustDir: the seeded direction, reach, lift, size and spin of puff voxel <paramref name="i"/> (seed: death.seed || 7).</summary>
        public static Dust DustDirection(int seed,int i)
        {
            uint s=unchecked((uint)(seed==0?7:seed)^(uint)(i+1)*0x9e3779b1u);double R(){s=unchecked(s*1664525u+1013904223u);return s/4294967296.0;}
            double a=i/7.3+R()*.8;var d=new Dust{X=(float)Math.Sin(a),Z=(float)Math.Cos(a)};d.K=(float)(.55+.45*R());d.Up=(float)(.6+R()*1.6);d.Size=(float)(.07+R()*.09);d.Spin=(float)(R()*12-6);return d;
        }
        /// <summary>A puff voxel <paramref name="since"/> seconds after impact: shown, its offset from the spot along its
        /// direction (.5 + reach), its height above the floor (centre), its Euler (x, y) spin and its edge.</summary>
        public static bool DustPose(in Dust d,float since,out float along,out float height,out float rx,out float ry,out float scale)
        {
            float t=since;along=height=rx=ry=scale=0;if(t<0||t>=DustLife)return false;
            float r=DustSpeed*d.K*(1-(float)Math.Exp(-t*4))/4,h=Math.Max(0,d.Up*t-3.2f*t*t)*.6f,fade=1-Smooth((t-.35f)/(DustLife-.35f));
            along=.5f+r;height=.05f+h+d.Size/2;rx=d.Spin*t;ry=d.Spin*.7f*t;scale=Math.Max(.001f,d.Size*(.6f+.8f*fade));return true;
        }
        public static float DustOpacity(float since)=>since>=0?1-Smooth((since-.35f)/(DustLife-.35f)):1;
        /// <summary>The SPLAT! pop: overshoot to 1.25, settle, float up and fade. Scale is the sprite width (height half of it).</summary>
        public static bool Pop(float since,out float width,out float rise,out float opacity)
        {
            width=rise=opacity=0;if(since<0||since>=PopLife)return false;float grow=since<.12f?since/.12f*1.25f:since<.24f?1.25f-(since-.12f)/.12f*.25f:1;
            width=PopSize*grow;rise=PopHeight+since*PopRise;opacity=1-Smooth((since-.8f)/(PopLife-.8f));return true;
        }
    }
}
