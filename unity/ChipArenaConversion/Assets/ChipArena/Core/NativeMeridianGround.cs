using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's open grounds by zone (2026-11-21, owner, Crown Hill's sanctuary plaza at night: "Theres very big empty areas, Like I
    /// feel the grounds should be textured?"; docs/expedition/MERIDIAN-DRESS.md "The grounds"). Every ground block's top (a `terrace-`)
    /// was one flat finish, the asphalt (#2E3038, no fine surface): the hub's plaza, tens of metres of it, read as a dark void at night.
    /// Now each open floor wears its zone's finish, each with a pattern and broad variation ChipArena/StageLit draws at every distance
    /// (Resources/NativeStageLit.shader, Ground):
    ///  - the square: the hub's precinct at its level (the plans' hub.precinct), York flags in bays framed by granite bands on the
    ///    camp's own 8 m grid (a band runs out of each gate), lighter than the roads;
    ///  - its border: a 2 m band of granite setts round the square's outer edge;
    ///  - the roads: the routes' flat legs at their level, asphalt (patched, cracked and sealed, stained, dusty, with puddles);
    ///  - the yards: every other open floor (the back streets, courts and quays off the routes), granite setts.
    /// Pure and deterministic: the same layout and routes give the same zones on every peer. Presentation only.
    /// </summary>
    public sealed class NativeMeridianGround
    {
        public const int Square=0,Border=1,Road=2,Yard=3,Zones=4;
        /// <summary>The square's border (m), how near a floor's level must be to a zone's (m), and the hub's floor (every plan's camp
        /// stands at y 0).</summary>
        public const float BorderWidth=2f,Level=.3f,HubY=0f;
        /// <summary>Each street plan's precinct (its seed, then rectangles x0, z0, x1, z1): docs/expedition/meridian-plans/*.json
        /// hub.precinct (MeridianGroundChecks holds the table to the plans). A layout not listed has no square.</summary>
        public static readonly (uint layout,float x0,float z0,float x1,float z1)[] Precincts={
            (647,-60,-46,60,42),                                         // Crown Hill
            (784,-85,-45,85,45),                                         // The Navigation
            (1058,-45,-45,45,45),                                        // The Arcades
            (1332,-50,-45,70,40),                                        // The Stride
            (1606,-45,-30,60,45),(1606,-30,-60,45,-30),(1606,-15,-85,30,-60), // The Yards
        };
        /// <summary>A zone's finish (NativeMeridianSurfaces).</summary>
        public static int FinOf(int zone)=>zone==Square?NativeMeridianSurfaces.Square:zone==Road?NativeMeridianSurfaces.Asphalt:NativeMeridianSurfaces.Setts;
        public static string NameOf(int zone)=>zone==Square?"square":zone==Border?"border":zone==Road?"road":zone==Yard?"yard":"zone "+zone;

        private readonly List<(float x0,float z0,float x1,float z1)> precinct=new List<(float,float,float,float)>();
        private readonly List<(float x0,float z0,float x1,float z1,float y)> roads=new List<(float,float,float,float,float)>();
        public int PrecinctRects=>precinct.Count;public int RoadRects=>roads.Count;

        /// <summary>The zones of a layout (its plan's seed) under its routes (null: no roads).</summary>
        public NativeMeridianGround(uint layout,IReadOnlyList<NativeMeridianDress.Route> routes)
        {
            foreach(var p in Precincts)if(p.layout==layout)precinct.Add((p.x0,p.z0,p.x1,p.z1));
            if(routes==null)return;
            foreach(var r in routes)
            {
                if(r?.Points==null)continue;
                for(int i=0;i+1<r.Points.Length;i++)
                {
                    var a=r.Points[i];var b=r.Points[i+1];string kind=r.Kinds!=null&&i<r.Kinds.Length?r.Kinds[i]:"flat";
                    if(kind!="flat"||Math.Abs(a.y-b.y)>.01f)continue;
                    // a leg's street: its width across, run on by half its width at each end so a turn's corner is the road's too
                    float w=(r.Widths!=null&&i<r.Widths.Length?r.Widths[i]:15f)*.5f;
                    roads.Add((Math.Min(a.x,b.x)-w,Math.Min(a.z,b.z)-w,Math.Max(a.x,b.x)+w,Math.Max(a.z,b.z)+w,a.y));
                }
            }
        }
        private bool InPrecinct(float x,float z){foreach(var r in precinct)if(x>r.x0&&x<r.x1&&z>r.z0&&z<r.z1)return true;return false;}
        /// <summary>The zone of an open floor at a point (its top's level).</summary>
        public int At(V3 p)
        {
            if(Math.Abs(p.y-HubY)<Level&&InPrecinct(p.x,p.z))
            {
                const float d=BorderWidth;
                return InPrecinct(p.x+d,p.z)&&InPrecinct(p.x-d,p.z)&&InPrecinct(p.x,p.z+d)&&InPrecinct(p.x,p.z-d)?Square:Border;
            }
            foreach(var r in roads)if(Math.Abs(p.y-r.y)<Level&&p.x>r.x0&&p.x<r.x1&&p.z>r.z0&&p.z<r.z1)return Road;
            return Yard;
        }
        /// <summary>The world x and z lines where the zones of floors at level `y` meet (a floor's grid takes them, so no cell straddles a
        /// zone's edge). The lists are cleared first.</summary>
        public void Cuts(float y,List<float> xs,List<float> zs)
        {
            xs.Clear();zs.Clear();
            if(Math.Abs(y-HubY)<Level)foreach(var r in precinct)
            {
                xs.Add(r.x0);xs.Add(r.x0+BorderWidth);xs.Add(r.x1-BorderWidth);xs.Add(r.x1);
                zs.Add(r.z0);zs.Add(r.z0+BorderWidth);zs.Add(r.z1-BorderWidth);zs.Add(r.z1);
            }
            foreach(var r in roads)if(Math.Abs(y-r.y)<Level){xs.Add(r.x0);xs.Add(r.x1);zs.Add(r.z0);zs.Add(r.z1);}
        }
    }
}
