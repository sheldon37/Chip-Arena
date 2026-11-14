using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pier 9's art poses (docs/arenas/STAGE-ART.md, docs/arenas/HARBOR.md): where each modelled rig part of the stage bank
    /// ChipArenaStagePier (tools/stages/harbor_props.py lists their frames) stands this frame. Core NativePierRig tags the box
    /// parts it builds from the clocks and the replicated hazard state (<see cref="NativePierRig.Slot"/>): each tagged part's own
    /// centre and turn place its model, so the wheel, its cabins, the carousel's mounts, the train, the gondolas and the lamps
    /// move exactly as the box rig's do. The bulbs that chase (the wheel's rims, the marquees) are a white glow sub-mesh in three
    /// groups, each lit by this frame's tint. Pure and allocation free per frame.
    /// </summary>
    public static class NativePierArt
    {
        public const string Bank="ChipArenaStagePier";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"wheel","wheel-bulbs-0","wheel-bulbs-1","wheel-bulbs-2","cabin-0","cabin-1","cabin-2","cabin-3","table","mount-0","mount-1","star","doll","boat","car","car-end",
            "wigwag","lane-lamp","ring","beacon","marquee-0","marquee-1","marquee-2"};
        public const int Wheel=0,WheelBulbs=1,Cabin=4,Table=8,Mount=9,Star=11,Doll=12,Boat=13,Car=14,CarEnd=15,Wigwag=16,LaneLamp=17,Ring=18,Beacon=19,Marquee=20,Groups=3;
        /// <summary>The slots that cast shadows: the train, the gondolas' rings, the carousel's mounts.</summary>
        public static bool Casts(int slot)=>slot==Car||slot==CarEnd||slot==Ring||slot==Mount||slot==Mount+1;
        /// <summary>The light of chase group g (0..2) at render time t: a wave of light running through the three groups, never
        /// dark (a bulb's glow sub-mesh is white; this is its gain).</summary>
        public static double Chase(int g,double t,double rate=1.15){double c=Math.Cos(2*Math.PI*(t*rate-g/3.0));return .32+.68*Math.Max(0,c)*Math.Max(0,c);}
        /// <summary>A lamp's light (linear rgb times its gain) for a level 0..1: red (the wig-wags, the beacons, the lane in the
        /// danger) or the lane's warm amber.</summary>
        public static void Lamp(double level,bool red,out double r,out double g,out double b)
        {double k=.1+2.5*Math.Max(0,Math.Min(1,level));if(red){r=k;g=.07*k;b=.04*k;}else{r=k;g=.62*k;b=.22*k;}}
        /// <summary>A part's turn as a unit quaternion.</summary>
        public static FactoryQ Turn(in FactoryPart p){double l=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);return l>1e-9?new FactoryQ{X=p.Qx/l,Y=p.Qy/l,Z=p.Qz/l,W=p.Qw/l}:FactoryQ.Identity;}
        /// <summary>The frame's rig draws, from the rig's parts after its Build.</summary>
        public static void Pose(NativePierRig rig,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;var items=rig.Parts.Items;int n=rig.Parts.Count;double t=rig.Render;
            for(int g=0;g<Groups;g++){double c=Chase(g,t);into.Add(Marquee+g,0,0,0,FactoryQ.Identity,1,c,c,c);}
            for(int i=0;i<n;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;ref var p=ref items[i];var q=Turn(p);
                if(slot==Wigwag||slot==Beacon){Lamp(rig.Level[i],true,out double r,out double g,out double b);into.Add(slot,p.X,p.Y,p.Z,q,1,r,g,b);}
                else if(slot==LaneLamp){Lamp(rig.Level[i],rig.Kind[i]==NativePierLooks.BulbRed,out double r,out double g,out double b);into.Add(slot,p.X,p.Y,p.Z,q,1,r,g,b);}
                else
                {
                    into.Add(slot,p.X,p.Y,p.Z,q);
                    if(slot==Wheel)for(int g=0;g<Groups;g++){double c=Chase(g,t,.7);into.Add(WheelBulbs+g,p.X,p.Y,p.Z,q,1,c,c,c);}
                }
            }
        }
    }
}
