using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Cyber City's art (docs/arenas/STAGE-ART.md, docs/arenas/CYBERCITY-ART.md): what of the stage the bank
    /// ChipArenaStageCyberCity (tools/stages/cybercity_art.py; its frames are listed there) models, and where its one rig, the
    /// maglev train, stands this frame.
    /// <list type="bullet">
    /// <item>Static chunks: every static map block and the kit's steady parts on them (the street, the Cut, the frontage, the
    /// podiums, the station, the markets, the yards), the near ring of the backdrop and the Helix Line's tunnels.</item>
    /// <item>Block models: the breakable and carried blocks (stalls, vending machines, bins, bikes, the billboards' struts,
    /// trusses, frames and screens) with their steady kit parts, drawn where the block is.</item>
    /// <item>The rig: the train's three cars, posed from the maglev's head exactly as NativeCyberCityKit.TrainPlace puts its
    /// boxes.</item>
    /// </list>
    /// The kit's driven parts (<see cref="Kept"/>: the edge strips, coils, SAFE and EXIT lights, the couplings' rails, lamps and
    /// deck plates, the flickering signs) stay the kit's boxes over the art: they change colour or level each frame from the
    /// hazard states, and the telegraphs stay exactly the box kit's. Pure and allocation free per frame.
    /// </summary>
    public static class NativeCyberCityArt
    {
        public const string Bank="ChipArenaStageCyberCity";
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;"): the train's cars, each in the train's frame (+x
        /// the travel direction, y 0 on the guideway's top) with its origin at its own front end.</summary>
        public static readonly string[] Slots={"train-nose","train-mid","train-tail"};
        public const int Nose=0,Mid=1,Tail=2;
        /// <summary>The cars' lengths and the gap between them as the kit's train has them (NativeCyberCityKit.BuildTrain), and the
        /// whole train's length (m).</summary>
        public static readonly double[] Cars={14,13,14};
        public const double Gap=.5,Length=42;
        /// <summary>How far past the slab's middle a car still draws (the kit's own limit for its boxes, plus a car).</summary>
        public const double Reach=139;
        public static bool Casts(int slot)=>true;
        /// <summary>How far behind the nose car `car`'s front end is.</summary>
        public static double Front(int car){double x=0;for(int i=0;i<car&&i<Cars.Length;i++)x+=Cars[i]+Gap;return x;}
        /// <summary>A kit part that stays the kit's box while the art draws: every driven part. The steady parts are modelled
        /// (in the chunks, or in their block's model: see <see cref="Modelled"/>).</summary>
        public static bool Kept(in NativeCyberCityPart p)=>p.Drive!=NativeCyberCityKit.DriveSteady;
        /// <summary>Whether a block's role puts it in the static chunks (its steady kit parts never draw as boxes over the art);
        /// any other block has a block model, which draws only while the block stands at NativeStageArt.Keep or better.</summary>
        public static bool Modelled(ContentBlock b)=>b!=null&&b.role=="static";
        /// <summary>A backdrop box the bank models (the near ring right behind the frontage and the Helix Line's tunnels): it is
        /// left out of the box backdrop while the art draws. The street plates, the expressways and the towers stay boxes.</summary>
        public static bool Near(in NativeCyberBox b)=>b.Y<-.3f&&b.Hz<5f||Math.Abs(b.X)+b.Hx<=56.5f&&Math.Abs(b.Z)+b.Hz<=48.5f;

        /// <summary>Poses the train for the maglev's state (hazards and states indexed alike; maglev: the kit's index, -1 none).
        /// Nothing while the line is idle.</summary>
        public static void Pose(MapHazard[] hazards,NativeHazardState[] states,int maglev,NativeStageArtPose into)
        {
            into.Clear();if(hazards==null||states==null||maglev<0||maglev>=states.Length||maglev>=hazards.Length)return;
            var s=states[maglev];if(s.Phase==NativeHazardPhase.Idle||double.IsNaN(s.Head))return;
            int d=s.Direction>0?1:-1;var q=d>0?FactoryQ.Identity:FactoryQ.Axis(0,1,0,Math.PI);double track=hazards[maglev].center.z;
            for(int c=0;c<Cars.Length;c++){double x=s.Head-d*Front(c);if(Math.Abs(x)>Reach)continue;into.Add(c,x,NativeCyberCityKit.RailTop,track,q);}
        }
    }
}
