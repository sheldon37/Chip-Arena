using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Skyframe's art poses (docs/arenas/STAGE-ART.md, docs/arenas/SKYFRAME.md): where each modelled rig part of the stage bank
    /// ChipArenaStageSkyframe (tools/stages/skyframe_props.py lists their frames) stands this frame. Core NativeSkyframeRig tags
    /// the box parts it builds from the replicated hazard state (<see cref="NativeSkyframeRig.Slot"/>): each tagged part's own
    /// centre and turn place its model, so the crane's top, trolley, ropes, hook and bundle, the trees' crowns, the windsocks,
    /// the turbines' rotors, the airships, the birds and the cloud islands move exactly as the box rig's do. Pure and allocation
    /// free per frame.
    /// </summary>
    public static class NativeSkyframeArt
    {
        public const string Bank="ChipArenaStageSkyframe";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"crane-top","trolley","cable","hook","load","crown-0","crown-1","crown-2","windsock","rotor","airship-0","airship-1","bird","wing-l","wing-r",
            "cloud-0","cloud-1","cloud-2","cloud-3"};
        public const int CraneTop=0,Trolley=1,Cable=2,Hook=3,Load=4,Crown=5,Crowns=3,Windsock=8,Rotor=9,Airship=10,AirshipShapes=2,Bird=12,WingL=13,WingR=14,Cloud=15;
        /// <summary>The slots that cast shadows: the crane's parts over the garden, the bundle, the trees' crowns.</summary>
        public static bool Casts(int slot)=>slot<=Load||slot>=Crown&&slot<Crown+Crowns;
        /// <summary>How a slot's model takes its part's size: 0 as modelled; 1 scaled alike by the part's half width (a unit
        /// airship); 2 by each half extent (a unit rope, a unit cloud).</summary>
        public static int Sizing(int slot)=>slot==Cable||slot>=Cloud?2:slot==Airship||slot==Airship+1?1:0;
        /// <summary>A part's turn as a unit quaternion.</summary>
        public static FactoryQ Turn(in FactoryPart p){double l=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);return l>1e-9?new FactoryQ{X=p.Qx/l,Y=p.Qy/l,Z=p.Qz/l,W=p.Qw/l}:FactoryQ.Identity;}
        /// <summary>The frame's rig draws, from the rig's parts after its Build.</summary>
        public static void Pose(NativeSkyframeRig rig,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;var items=rig.Parts.Items;int n=rig.Parts.Count;
            for(int i=0;i<n;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;ref var p=ref items[i];var q=Turn(p);
                switch(Sizing(slot))
                {
                    case 1:into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,p.Hx,p.Hx);break;
                    case 2:into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,p.Hy,p.Hz);break;
                    default:into.Add(slot,p.X,p.Y,p.Z,q);break;
                }
            }
        }
    }
}
