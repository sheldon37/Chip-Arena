using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's solid street props: the contract for Astra (the notes' meridian-street-props-astra.md; docs/expedition/
    /// MERIDIAN-STREETS.md, part 2). Wrecked and abandoned cars, buses, benches, dumpsters, barricades, police cordons, newsstands,
    /// bus shelters, planters, bollards, hydrants and lamp posts stand only on collision blocks, which are Astra's to build (the
    /// owner's honesty rule: nothing solid-looking a pilot can walk through, no invisible walls). This is the proposal: each prop's
    /// block stem and size (the model fills its block to within 0.25 m on every side), and the rules for where they go
    /// (NativeMeridianStreetLife.ProposeProps places a layout's candidates by them; the DEV walk's preview draws them).
    ///
    /// Until Astra's blocks exist nothing here draws in a live run: the preview is the Developer walk's only, compiled into the
    /// Editor and development builds, and answers to the walk's maps alone.
    /// </summary>
    public static class NativeMeridianStreetProps
    {
        public const string Version="meridian-street-props-1";
        /// <summary>The bank file (Resources) the preview's models come from (rig/props/&lt;model&gt;/&lt;tier&gt;).</summary>
        public const string Bank="ChipArenaCityStreetProps";
        /// <summary>A prop block's id: "prop-" + its stem + "-" + its index (the plan's block role prefix `prop-`).</summary>
        public const string IdPrefix="prop-";

        /// <summary>A stem: its key, its block in the model's frame (x across, height, z along its front's way: the street kit's frame,
        /// a car's nose and a bench's seat toward +z; axis-aligned in the world, turned only in quarter turns), the models that may
        /// dress it, and what it is.</summary>
        public sealed class Stem
        {
            public readonly string Key,What;public readonly V3 Size;public readonly string[] Models;public readonly float Fill;
            public Stem(string key,V3 size,float fill,string what,params string[] models){Key=key;Size=size;Fill=fill;What=what;Models=models;}
        }
        private static V3 V(float x,float y,float z)=>new V3(x,y,z);
        /// <summary>The table (block sizes in metres; Fill: the share of a layout's candidates a run fills, by its world seed).</summary>
        public static readonly Stem[] Stems=
        {
            new Stem("car",V(1.8f,1.45f,4.2f),.7f,"an abandoned Meridian Motors Vale, parked at the kerb","car.blue","car.cream","car.maroon","car.green","car.mustard","car.white","car.smashed","car.rack"),
            new Stem("wreck",V(1.9f,1.45f,4.3f),.85f,"a crashed or burnt-out Vale, stopped where it hit","wreck.burnt","wreck.crashed"),
            new Stem("police",V(1.8f,1.6f,4.2f),1f,"the police's Vale at a cordon, its light bar dark","police.car"),
            new Stem("bus",V(2.55f,3.05f,11f),.85f,"a route 647 single-decker, stopped at its stop","bus.647"),
            new Stem("shelter",V(3.6f,2.6f,1.6f),.9f,"the cast-iron bus shelter, its back to the wall","shelter.cast"),
            new Stem("bench",V(1.8f,.85f,.66f),.85f,"a municipal bench, its back to the wall","bench","bench.broken","bench.blanket"),
            new Stem("bin",V(.6f,1.0f,.6f),.85f,"a cast-iron litter bin at the kerb","bin"),
            new Stem("dumpster",V(2.0f,1.4f,1.25f),.85f,"a commercial dumpster against a wall","dumpster","dumpster.open"),
            new Stem("barricade",V(3.5f,2.1f,1.5f),.9f,"the survivors' barricade of furniture across half an alley mouth","barricade.furniture"),
            new Stem("barrier",V(2.3f,1.1f,.6f),1f,"a steel crowd barrier, end to end in a cordon","barrier"),
            new Stem("newsstand",V(2.4f,2.4f,1.3f),.85f,"a news kiosk against a shop wall, its shutters down","newsstand"),
            new Stem("kiosk",V(1.2f,2.6f,1.2f),.9f,"the Meridian Telephones Type M kiosk","kiosk.m"),
            new Stem("planter",V(1.5f,.9f,1.0f),.85f,"a stone planter with a dead shrub","planter"),
            new Stem("bollard",V(.3f,.95f,.3f),.9f,"a cannon bollard","bollard.cannon","bollard.sleeve"),
            new Stem("hydrant",V(.4f,.85f,.4f),.9f,"a fire hydrant at the kerb","hydrant"),
            new Stem("lamp",V(.3f,6.5f,.3f),1f,"the Type 6 sodium post where no wall carries a lamp","lamp.post","lamp.post.dead"),
        };
        private static readonly Dictionary<string,int> stemIndex=Index();
        private static Dictionary<string,int> Index(){var d=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<Stems.Length;i++)d.Add(Stems[i].Key,i);return d;}
        public static Stem StemOf(string key)=>key!=null&&stemIndex.TryGetValue(key,out int i)?Stems[i]:null;

        // ---- the rules (m) ----------------------------------------------------------------------------------------------------
        /// <summary>The clear lane down every route: its half width is the larger of <see cref="LaneHalf"/> and the route's half
        /// width less <see cref="KerbBand"/> (the band a parked car takes), so a horde and a Hulk always pass.</summary>
        public const float LaneHalf=3.5f,KerbBand=2.6f;
        /// <summary>No prop within this of a route's turn (its junction), but bollards and hydrants on the pavement.</summary>
        public const float Junction=6f;
        /// <summary>No prop within this of a stair's, a sloped street's or a fire escape's footprint.</summary>
        public const float Flight=4f;
        /// <summary>No prop within this of an instance door or the final court's gateway (the door front and the join circle).</summary>
        public const float Door=18f;
        /// <summary>No prop within this of a camp gate's middle (its approach), nor inside the camp.</summary>
        public const float Gate=14f;
        /// <summary>Beyond a cache's keep-out (the locker and the 2 m before it), this much more.</summary>
        public const float CacheMargin=1f;
        /// <summary>The gap rule: a prop stands flush against a wall or another prop (this or less), or a pilot's width away.</summary>
        public const float Flush=.05f,Pass=1.0f;
        /// <summary>The pavement's kerb from the wall (the street dress's pavement and kerb) and the gutter.</summary>
        public const float Kerb=2.28f,Gutter=2.55f;

        /// <summary>A proposed prop: its block (id, stem, centre at its middle, size in world axes), its model and the model's turn
        /// (radians, a quarter turn's multiple: its +z faces (sin, 0, cos)), its candidate slot and whether the run fills it.</summary>
        public struct Block
        {
            public string Id,Stem,Model;public V3 Pos,Size;public float Yaw;public bool Filled;public int Slot;
            public V3 Lo=>Pos-Size*.5f;public V3 Hi=>Pos+Size*.5f;
        }
        /// <summary>A stem's block in the world for a turn: its size with x and z swapped on a quarter turn.</summary>
        public static V3 WorldSize(Stem s,float yaw){bool quarter=Math.Abs(Math.Sin(yaw))>.5;return quarter?new V3(s.Size.z,s.Size.y,s.Size.x):s.Size;}
        /// <summary>A prop's collision block as the map would hold it (axis-aligned, static).</summary>
        public static ContentBlock ToBlock(in Block b)=>new ContentBlock{id=b.Id,role="static",material="metal",pos=b.Pos,size=b.Size,rotation=new ContentRotation()};
        /// <summary>The model's place: centred on its block's floor.</summary>
        public static V3 FootOf(in Block b)=>new V3(b.Pos.x,b.Pos.y-b.Size.y*.5f,b.Pos.z);
    }
}
