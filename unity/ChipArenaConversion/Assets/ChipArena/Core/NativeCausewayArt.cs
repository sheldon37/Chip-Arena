using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Starfall Causeway's art poses (docs/arenas/STAGE-ART.md, docs/arenas/CAUSEWAY.md): where each modelled rig part of the stage
    /// bank ChipArenaStageCauseway (tools/stages/causeway_props.py lists their frames) stands this frame. Core NativeCausewayRig
    /// tags the box parts it builds from the replicated hazard state (<see cref="NativeCausewayRig.Slot"/>): each tagged part's
    /// own centre and turn place its model, so the falling stars and their shards, the comet's nucleus, the lane's lit glyphs,
    /// the banners in the solar wind, the floating crystals, the Shattered Arch, the crowns' drifting ruins and the asteroids
    /// move exactly as the box rig's did. Pure and allocation free per frame.
    /// </summary>
    public static class NativeCausewayArt
    {
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"star","shard","shard-ember","glyph","banner","banner-hem","banner-star","crystal","crystal-violet","voussoir","voussoir-key",
            "crown-0","crown-1","crown-2","crown-3","crown-4","crown-5","crown-6","asteroid-0","asteroid-1","asteroid-2","asteroid-3","comet"};
        public const int Star=0,Shard=1,ShardEmber=2,Glyph=3,Banner=4,BannerHem=5,BannerStar=6,Crystal=7,CrystalViolet=8,Voussoir=9,VoussoirKey=10,Crown=11,Asteroid=18,Comet=22;
        /// <summary>The slots that cast shadows: the falling star, the arch's stones, the crowns' ruins.</summary>
        public static bool Casts(int slot)=>slot==Star||slot==Voussoir||slot==VoussoirKey||slot>=Crown&&slot<Asteroid;
        /// <summary>How a slot's model takes its part's size: 0 as modelled; 1 scaled alike by the part's half width (a unit rock,
        /// shard or nucleus); 2 by each half extent, upright and turned about y by the part's level (a unit crystal; a NaN level
        /// keeps the part's own turn).</summary>
        public static int Sizing(int slot)=>slot==Shard||slot==ShardEmber||slot==Comet||slot>=Asteroid&&slot<Comet?1:slot==Crystal||slot==CrystalViolet?2:0;
        /// <summary>A floating crystal's lean from the upright (rad).</summary>
        public const double CrystalLean=.1;
        /// <summary>A lit glyph's light (linear rgb times its gain) for a level 0..1 in the lane's amber or its cold blue.</summary>
        public static void GlyphLight(double level,bool cold,out double r,out double g,out double b)
        {double k=.5+1.9*Math.Max(0,Math.Min(1,level));if(cold){r=.275*k;g=.791*k;b=k;}else{r=k;g=.456*k;b=.102*k;}}

        /// <summary>A part's turn as a unit quaternion (the rig turns some parts about axes it does not normalise; a model must
        /// not be scaled by that).</summary>
        public static FactoryQ Turn(in FactoryPart p){double l=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);return l>1e-9?new FactoryQ{X=p.Qx/l,Y=p.Qy/l,Z=p.Qz/l,W=p.Qw/l}:FactoryQ.Identity;}
        /// <summary>The frame's rig draws, from the rig's parts after its Build.</summary>
        public static void Pose(NativeCausewayRig rig,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;var items=rig.Parts.Items;int n=rig.Parts.Count;
            for(int i=0;i<n;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;ref var p=ref items[i];var q=Turn(p);
                switch(Sizing(slot))
                {
                    case 1:into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,p.Hx,p.Hx);break;
                    case 2:{double turn=rig.Level[i];if(!double.IsNaN(turn))q=FactoryQ.Axis(0,1,0,turn)*FactoryQ.Axis(0,0,1,CrystalLean);into.AddScaled(slot,p.X,p.Y,p.Z,q,p.Hx,p.Hy,p.Hz);break;}
                    default:
                        if(slot==Glyph){GlyphLight(rig.Level[i],rig.Kind[i]==NativeCausewayLooks.RuneCold,out double r,out double g,out double b);into.Add(slot,p.X,p.Y,p.Z,q,1,r,g,b);}
                        else into.Add(slot,p.X,p.Y,p.Z,q);
                        break;
                }
            }
        }
    }
}
