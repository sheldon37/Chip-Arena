using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Deepwell's art poses (docs/arenas/STAGE-ART.md, docs/arenas/DEEPWELL.md): where each modelled rig part of the stage bank
    /// ChipArenaStageDeepwell (tools/stages/rig_props.py lists their frames) stands this frame. Core NativeDeepwellRig tags the box
    /// parts it builds from the replicated hazard state and the render clock (<see cref="NativeDeepwellRig.Slot"/>): each tagged
    /// part's own centre, turn and size place its model, so the drill's ram, the klaxons, the core, the vents' grates, the whales,
    /// the jellyfish, the fish, the kelp and the drone move exactly as the box rig's do. Pure and allocation free per frame.
    /// </summary>
    public static class NativeDeepwellArt
    {
        public const string Bank="ChipArenaStageDeepwell";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"ram","klaxon","core","grate","whale-body","whale-tail","whale-fin-l","whale-fin-r","jelly-bell","jelly-arms","fish","kelp","drone","ram-rod"};
        public const int Ram=0,Klaxon=1,Core=2,Grate=3,WhaleBody=4,WhaleTail=5,WhaleFinL=6,WhaleFinR=7,JellyBell=8,JellyArms=9,Fish=10,Kelp=11,Drone=12,RamRod=13;
        /// <summary>The slots that cast shadows: the ram alone (the sea's creatures swim outside the key's shadow range, and a
        /// whale's shadow across the gallery would read as a fault).</summary>
        public static bool Casts(int slot)=>slot==Ram;
        /// <summary>How a slot's model takes its part's size: 0 as modelled; 1 scaled alike by the part's level (a whale's share
        /// of the cow's length, a fish's half length); 2 by each half extent (a unit bell); 3 by the level across and the half
        /// height along y (the arms: a unit sheaf); 4 by the half height along y alone (a kelp joint or a guide rod of unit half length).</summary>
        public static int Sizing(int slot)=>slot>=WhaleBody&&slot<=WhaleFinR||slot==Fish?1:slot==JellyBell?2:slot==JellyArms?3:slot==Kelp||slot==RamRod?4:0;
        /// <summary>Whether a slot's glow takes its part's light (the klaxons, the core, the jellyfish).</summary>
        public static bool Lit(int slot)=>slot==Klaxon||slot==Core||slot==JellyBell||slot==JellyArms;

        /// <summary>A part's turn as a unit quaternion.</summary>
        public static FactoryQ Turn(in FactoryPart p){double l=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);return l>1e-9?new FactoryQ{X=p.Qx/l,Y=p.Qy/l,Z=p.Qz/l,W=p.Qw/l}:FactoryQ.Identity;}
        /// <summary>The frame's rig draws, from the rig's parts after its Build.</summary>
        public static void Pose(NativeDeepwellRig rig,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;var items=rig.Parts.Items;int n=rig.Parts.Count;
            for(int i=0;i<n;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;ref var p=ref items[i];var q=Turn(p);double l=rig.Level[i],r=1,g=1,b=1;if(Lit(slot)){r=rig.Tint[i*3];g=rig.Tint[i*3+1];b=rig.Tint[i*3+2];}
                switch(Sizing(slot))
                {
                    case 1:into.AddScaled(slot,p.X,p.Y,p.Z,q,l,l,l);break;
                    case 2:into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,p.Hy,p.Hz,r,g,b);break;
                    case 3:into.AddScaled(slot,p.X,p.Y,p.Z,q,l,p.Hy,l,r,g,b);break;
                    case 4:into.AddScaled(slot,p.X,p.Y,p.Z,q,1,p.Hy,1);break;
                    default:into.Add(slot,p.X,p.Y,p.Z,q,1,r,g,b);break;
                }
            }
        }
    }
}
