using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory's art poses (docs/arenas/STAGE-ART.md, docs/arenas/FACTORY-ART.md): where each modelled rig part
    /// of the stage bank ChipArenaStageFactory (tools/stages/factory_art.py; factory_rig.py lists the frames) stands this frame.
    /// Read only from the simulation's own hazard states (NativeFactoryHazards.State at the hazard clock) with the numbers Core
    /// NativeFactoryRig poses its boxes by, so the models move exactly as the box rig did on every client and in the killcam: the
    /// slitter blades rising through their slots and the rail saws' carriages swinging out along their tracks, both spinning by
    /// the state's angle; the press rams with their guide rods running up into the heads; the crane's bridge, trolley, ropes,
    /// hook block, slings and beam bundle (swaying about the hook, falling, landed). The lathes' chucks turn on the field clock.
    /// The telegraph lamps and a bloodied blade's smears stay the box rig's (NativeFactoryRig.Kept). Pure and allocation free.
    /// </summary>
    public static class NativeFactoryArt
    {
        public const string Bank="ChipArenaStageFactory";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"blade","rail-saw","ram","press-rod","crane-bridge","crane-trolley","crane-ropes","crane-hook","crane-hook-high","crane-load","crane-load-high","crane-sling","chuck"};
        public const int Blade=0,RailSaw=1,Ram=2,PressRod=3,Bridge=4,Trolley=5,Ropes=6,Hook=7,HookHigh=8,Load=9,LoadHigh=10,Sling=11,Chuck=12;
        /// <summary>The slots that cast shadows: not the ropes and slings, nor the hook and the load while they hang high (the sun's
        /// shadow would land metres from the zone; the box rig's rule).</summary>
        public static bool Casts(int slot)=>slot!=Ropes&&slot!=Sling&&slot!=HookHigh&&slot!=LoadHigh;
        /// <summary>A lathe chuck's turn (rad/s) and the slings' reach over the load (shares of its size, the box rig's).</summary>
        public const double ChuckSpin=7.5,SlingX=.4,SlingZ=.4,SlackSling=1.0,SlackAside=.12;
        private static readonly FactoryQ Half=FactoryQ.Axis(0,1,0,Math.PI),ToX=FactoryQ.Axis(0,1,0,Math.PI/2);

        /// <summary>Poses the Factory's modelled rig for round `round` at the hazard clock `clock` (NativeFactoryHazards.Clock) and
        /// the field clock `now` (the chucks), exactly as NativeFactoryRig.Build poses its boxes.</summary>
        public static void Pose(FactoryHazard[] hazards,ContentBlock[] blocks,int round,double clock,double now,NativeStageArtPose into)
        {
            into.Clear();
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;var st=NativeFactoryHazards.State(h,round,clock);
                switch(h.kind)
                {
                    case NativeFactoryHazards.SawSlot:if(st.Lift>0)Saw(h,st.Center.x,st.Center.y,st.Center.z,st.Spin,into);break;
                    case NativeFactoryHazards.SawRail:into.Add(RailSaw,st.Center.x,st.Center.y,st.Center.z,FactoryQ.Identity);Saw(h,st.Center.x-NativeFactoryRig.BladeAside,st.Center.y,st.Center.z,st.Spin,into);break;
                    case NativeFactoryHazards.Press:Press(h,st,into);break;
                    case NativeFactoryHazards.Crane:Crane(h,st,into);break;
                }
            }
            for(int i=0;i<(blocks?.Length??0);i++)
            {
                var b=blocks[i];if(!LatheHead(b))continue;int dir=ChuckSide(b);
                into.Add(Chuck,b.pos.x+dir*b.size.x/2,b.pos.y,b.pos.z,(dir>0?FactoryQ.Identity:Half)*FactoryQ.Axis(1,0,0,now*ChuckSpin));
            }
        }
        /// <summary>A lathe's headstock block (its chuck turns on its bed side).</summary>
        public static bool LatheHead(ContentBlock b)=>b?.id!=null&&b.id.StartsWith("factory-lathe-",StringComparison.Ordinal)&&b.id.EndsWith("-head",StringComparison.Ordinal);
        /// <summary>The side of a headstock its bed lies on (+1: +x): the north hall's beds run east of their heads, the south's west.</summary>
        public static int ChuckSide(ContentBlock head)=>head.pos.z>0?1:-1;

        // A blade: the unit disc (its axle the prop's z) at the hazard's radius, turned as the box rig's plates are.
        private static void Saw(FactoryHazard h,double cx,double cy,double cz,double spin,NativeStageArtPose into)
        {
            bool alongX=Math.Abs(h.axis.x)>.5;into.AddScaled(Blade,cx,cy,cz,(alongX?ToX:FactoryQ.Identity)*FactoryQ.Axis(0,0,1,spin),h.radius,h.radius,1);
        }
        // A press: the ram at the simulation's stroke, its four guide rods from its top up into the head.
        private static void Press(FactoryHazard h,in FactoryHazardState st,NativeStageArtPose into)
        {
            double x=st.Center.x,y=st.Center.y,z=st.Center.z,hx=h.size.x/2,hy=h.size.y/2,hz=h.size.z/2,head=h.a.y+hy,top=Math.Max(y+hy+.01,head+NativeFactoryRig.RodInto);
            into.Add(Ram,x,y,z,FactoryQ.Identity);
            for(int s=-1;s<=1;s+=2)for(int u=-1;u<=1;u+=2)into.Add(PressRod,x+u*hx*NativeFactoryRig.RodX,y+hy,z+s*hz*NativeFactoryRig.RodZ,FactoryQ.Identity,top-(y+hy));
        }
        // The crane: the bridge on its rails, the trolley, the ropes down to the hook block, the block, the bundle swung about the
        // hook (hanging, falling or landed) and its slings (taut to the bundle's corners, or slack under the hook once it is let go).
        private static void Crane(FactoryHazard h,in FactoryHazardState st,NativeStageArtPose into)
        {
            double X=st.X,Z=st.Z,rail=h.a.y+NativeFactoryRig.RailUp,ty=rail+NativeFactoryRig.TrolleyUp,hookY=st.HookY,foot=hookY+NativeFactoryRig.RopeFoot;
            into.Add(Bridge,X,rail,0,FactoryQ.Identity);into.Add(Trolley,X,ty,Z,FactoryQ.Identity);
            if(ty>foot)into.Add(Ropes,X,foot,Z,FactoryQ.Identity,ty-foot);
            into.Add(hookY>NativeFactoryRig.HookHigh?HookHigh:Hook,X,hookY,Z,FactoryQ.Identity);
            var sway=FactoryQ.Axis(1,0,0,st.Sway);double ly=st.LoadY,top=h.size.y;sway.Rotate(0,ly-hookY,0,out double ox,out double oy,out double oz);
            into.Add(ly>NativeFactoryRig.LoadHigh?LoadHigh:Load,X+ox,hookY+oy,Z+oz,sway);
            if(st.Attached)
            {
                for(int ux=-1;ux<=1;ux+=2)for(int uz=-1;uz<=1;uz+=2)
                {sway.Rotate(ux*h.size.x*SlingX,ly+top-hookY,uz*h.size.z*SlingZ,out double cx,out double cy,out double cz);into.Span(Sling,X,hookY,Z,X+cx,hookY+cy,Z+cz);}
            }
            else for(int s=-1;s<=1;s+=2)into.Add(Sling,X+s*SlackAside,hookY-SlackSling,Z,FactoryQ.Identity,SlackSling);
        }
    }
}
