using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The home screen's modelled rooftop (owner 2026-11-11: the menu out of its boxy look; docs/MENU-ROOFTOP-ART.md): the plan of
    /// its local bank Resources/ChipArenaStageRooftop (git-ignored, from the builder's Drive zip; Core NativePackBank's format),
    /// made in Blender by tools/stages/rooftop_art.py on the layout Tests/RooftopLayout.cs writes. Runtime NativeRooftopModels
    /// draws it in place of the parts of Core NativeRooftopCity's quads it covers; without the bank nothing here is read and
    /// every quad draws exactly as before. Presentation only: never sim data, never in the network frame.
    /// <para>The bank's props, by name (tier 0 the High and reduced-effects tiers, 2 the Low graphics tier):</para>
    /// <list type="bullet">
    /// <item><c>static/&lt;name&gt;/&lt;tier&gt;</c> (group "static"): world-space scenery for ChipArena/NativeRooftopModel.</item>
    /// <item><c>facade/&lt;name&gt;/&lt;tier&gt;</c> (group "facade"): the towers' walls for the menu's facade shader: uv0 (the metric
    /// coordinate along the wall, the tower's top), uv1 (the tower's seed, its style plus .9 of its lit share), colour.r its hue.</item>
    /// <item><c>rig/car-&lt;kind&gt;/&lt;tier&gt;</c> (group "rig"): a flying car in its own frame (+z ahead, the lane's point at its
    /// origin).</item>
    /// <item><c>cover/perch</c>, <c>cover/city</c> (group "cover", no parts): the part of Core's quads the art stands in for.</item>
    /// </list>
    /// Pure; allocates only while reading a bank (once per quality tier).
    /// </summary>
    public static class NativeRooftopBank
    {
        public const string Name="ChipArenaStageRooftop";
        public const int High=0,Low=2;
        /// <summary>A bank's props sorted by role for one graphics tier.</summary>
        public sealed class Plan
        {
            /// <summary>The static scenery's and the facades' props (indices into the bank's props).</summary>
            public readonly List<int> Static=new List<int>(),Facade=new List<int>();
            /// <summary>The car kinds in name order, each with its near and its far prop.</summary>
            public readonly List<string> Cars=new List<string>();public readonly List<int> CarNear=new List<int>(),CarFar=new List<int>();
            /// <summary>The parts of Core's quads the art replaces (NativeRooftopCity.PerchPart, CityPart).</summary>
            public bool Perch,City;
            public bool Any=>Static.Count>0||Facade.Count>0;
            public bool Covers(int part)=>part==NativeRooftopCity.PerchPart?Perch:part==NativeRooftopCity.CityPart&&City;
        }
        /// <summary>The plan of a rooftop bank for the tier (null bank, or a bank without scenery: null). A missing tier takes
        /// the nearest present one. Unknown names are ignored.</summary>
        public static Plan Read(NativePackBank.Bank bank,bool low)
        {
            if(bank==null)return null;int want=low?Low:High;
            var tiers=new SortedDictionary<string,int[]>(StringComparer.Ordinal);bool perch=false,city=false;
            for(int i=0;i<bank.Props.Count;i++)
            {
                var p=bank.Props[i];if(p.Name==null)continue;
                if(p.Group=="cover"){if(p.Name=="cover/perch")perch=true;else if(p.Name=="cover/city")city=true;continue;}
                if(p.Parts==null||p.Parts.Length==0||!NativeStageArt.Split(p.Name,out string kind,out string id,out int tier))continue;
                if(kind!=p.Group||kind!="static"&&kind!="facade"&&!(kind=="rig"&&id.StartsWith("car-",StringComparison.Ordinal)))continue;
                string key=kind+"/"+id;if(!tiers.TryGetValue(key,out var t)){t=new[]{-1,-1,-1};tiers[key]=t;}t[tier]=i;
            }
            var plan=new Plan();
            foreach(var kv in tiers)
            {
                int at=Pick(kv.Value,want);if(at<0)continue;
                if(kv.Key.StartsWith("static/",StringComparison.Ordinal))plan.Static.Add(at);
                else if(kv.Key.StartsWith("facade/",StringComparison.Ordinal))plan.Facade.Add(at);
                else{plan.Cars.Add(kv.Key.Substring(8));plan.CarNear.Add(at);plan.CarFar.Add(Pick(kv.Value,Low));}
            }
            if(!plan.Any)return null;
            plan.Perch=perch;plan.City=city;return plan;
        }
        // The wanted tier's prop, else the nearest present tier's.
        private static int Pick(int[] t,int want){for(int d=0;d<NativeStageArt.Tiers;d++){if(want-d>=0&&t[want-d]>=0)return t[want-d];if(want+d<NativeStageArt.Tiers&&t[want+d]>=0)return t[want+d];}return -1;}
        /// <summary>A facade vertex's style (0..3) and lit share (0..1) from its packed uv1.y.</summary>
        public static void Facade(float packed,out float style,out float share){style=(float)Math.Floor(packed+1e-3f);share=Math.Min(1,Math.Max(0,(packed-style)/.9f));}
        /// <summary>The kind (0..kinds-1) of car `car` of lane `lane`: the same car every loop.</summary>
        public static int CarKind(int lane,int car,int kinds)=>kinds<=0?0:(int)(NativeRooftopCity.Hash2(lane*7+3,car*13+1)*kinds)%kinds;
    }
}
