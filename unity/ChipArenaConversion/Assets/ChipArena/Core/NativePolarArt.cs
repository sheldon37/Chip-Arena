using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Polaris Station's art poses (docs/arenas/STAGE-ART.md, docs/arenas/POLAR.md): where each modelled rig part of the stage bank
    /// ChipArenaStagePolar (tools/stages/polar_props.py lists their frames) stands this frame. Core NativePolarRig tags the box
    /// parts it builds from the field clock and the replicated state (<see cref="NativePolarRig.Slot"/>): each tagged part's own
    /// centre and turn place its model, so the great dish on its yoke, the searchlight, the weather balloon and its sonde, the
    /// anemometers, the vents' lamps and the floes where the thin ice has gone move exactly as the box rig's do. Pure and
    /// allocation free per frame.
    /// </summary>
    public static class NativePolarArt
    {
        public const string Bank="ChipArenaStagePolar";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"dish","yoke","searchlight","balloon","sonde","cups","vent-lamp","floe"};
        public const int Dish=0,Yoke=1,Searchlight=2,Balloon=3,Sonde=4,Cups=5,VentLamp=6,Floe=7;
        /// <summary>The slots that cast shadows: the dish, its yoke and the balloon.</summary>
        public static bool Casts(int slot)=>slot==Dish||slot==Yoke||slot==Balloon;
        /// <summary>A vent lamp's light (linear rgb times its gain) for its heat: dark when idle, amber and beating through the
        /// warning (0..1), white-hot through the burst (1..2).</summary>
        public static void LampLight(double heat,out double r,out double g,out double b)
        {
            if(heat<=.02){r=.03;g=.03;b=.035;return;}
            if(heat<=1){double k=.4+2.2*heat;r=k;g=.42*k;b=.09*k;return;}
            double w=Math.Min(1,heat-1),q=2.6+1.4*w;r=q;g=q*(.42+.5*w);b=q*(.09+.75*w);
        }
        /// <summary>A part's turn as a unit quaternion.</summary>
        public static FactoryQ Turn(in FactoryPart p){double l=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);return l>1e-9?new FactoryQ{X=p.Qx/l,Y=p.Qy/l,Z=p.Qz/l,W=p.Qw/l}:FactoryQ.Identity;}
        /// <summary>The frame's rig draws, from the rig's parts after its Build.</summary>
        public static void Pose(NativePolarRig rig,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;var items=rig.Parts.Items;int n=rig.Parts.Count;
            for(int i=0;i<n;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;ref var p=ref items[i];var q=Turn(p);
                switch(slot)
                {
                    case Balloon:{double s=rig.Level[i];into.AddScaled(slot,p.X,p.Y,p.Z,q,s,s,s);break;}
                    case Floe:into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,1,p.Hz);break;
                    case VentLamp:{LampLight(rig.Level[i],out double r,out double g,out double b);into.Add(slot,p.X,p.Y,p.Z,q,1,r,g,b);break;}
                    default:into.Add(slot,p.X,p.Y,p.Z,q);break;
                }
            }
        }
    }
}
