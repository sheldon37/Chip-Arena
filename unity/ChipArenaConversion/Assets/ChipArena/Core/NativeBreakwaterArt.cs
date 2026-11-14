using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Breakwater's art poses (docs/arenas/STAGE-ART.md, docs/arenas/BREAKWATER.md): where each modelled rig part of the stage bank
    /// ChipArenaStageBreakwater (tools/stages/breakwater_props.py lists their frames) stands this frame. Core NativeBreakwaterRig
    /// tags the box parts it builds (<see cref="NativeBreakwaterRig.Slot"/>): each tagged part's own centre and turn place its
    /// model, so the optic, the lanterns, the bell, the swells, the comber and the wash, the buoy, the boats, the gulls and the
    /// flags move exactly as the box rig's do. Pure and allocation free per frame.
    /// </summary>
    public static class NativeBreakwaterArt
    {
        public const string Bank="ChipArenaStageBreakwater";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"optic","lantern","bell","swell","comber","wash","wash-fall","buoy","buoy-lamp","boat","dinghy","gull-body","gull-wing-l","gull-wing-r","flag"};
        public const int Optic=0,Lantern=1,Bell=2,Swell=3,Comber=4,Wash=5,WashFall=6,Buoy=7,BuoyLamp=8,Boat=9,Dinghy=10,GullBody=11,GullWingL=12,GullWingR=13,Flag=14;
        /// <summary>The slots that cast shadows: the bell, the boats and the buoy (the water, the lights, the birds and the cloth
        /// do not).</summary>
        public static bool Casts(int slot)=>slot==Bell||slot==Buoy||slot==Boat||slot==Dinghy;
        /// <summary>How a slot's model takes its part's size: 0 as modelled; 1 by each half extent (a unit swell, comber or sheet
        /// of water: the model spans -1..1 on each axis); 2 scaled alike by the part's level (a flag's cloth).</summary>
        public static int Sizing(int slot)=>slot==Swell||slot==Comber||slot==Wash||slot==WashFall?1:slot==Flag?2:0;
        /// <summary>Whether a slot's glow takes the part's level as its light (the lanterns' flicker, the buoy's flash).</summary>
        public static bool Lit(int slot)=>slot==Lantern||slot==BuoyLamp||slot==Optic;

        /// <summary>The frame's rig draws, from the rig's parts after its Build.</summary>
        public static void Pose(NativeBreakwaterRig rig,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;var items=rig.Parts.Items;int n=rig.Parts.Count;
            for(int i=0;i<n;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;ref var p=ref items[i];var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};
                switch(Sizing(slot))
                {
                    case 1:into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,p.Hy,p.Hz);break;
                    case 2:{double s=rig.Level[i];into.AddScaled(slot,p.X,p.Y,p.Z,q,s,s,s);break;}
                    default:if(Lit(slot)){double l=rig.Level[i];into.Add(slot,p.X,p.Y,p.Z,q,1,l,l,l);}else into.Add(slot,p.X,p.Y,p.Z,q);break;
                }
            }
        }
    }
}
