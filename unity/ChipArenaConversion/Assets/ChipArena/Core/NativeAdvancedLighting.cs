using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// MapLooks.json `advanced` (docs/LIGHTING-ADVANCED.md): the advanced lighting a look opts into, each part off when missing
    /// (JsonUtility fills a missing object with zeros, System.Text.Json leaves it null; both are off). Presentation only.
    /// <list type="bullet">
    /// <item>`gi`: the runtime light volume (Core NativeLightVolume): `on`; `occlusion` how far the baked occlusion darkens the
    /// ambient (0..1); `bounce` the gain on the baked one-bounce light; `floor` the least the occlusion leaves (0..1); `cell`
    /// the cell size asked for (m; the bake widens it to stay in its budget); `actors` the share a body takes (0..1).</item>
    /// <item>`scatter`: height fog with light scattering in the world shaders: `on`; `color` the fog's own colour (sRGB);
    /// `density` per metre at `height` (m), falling off by e every `falloff` m above it; `max` its greatest opacity (0..1);
    /// `key` the key light's share scattered toward the eye; `phase` the forward lobe (Henyey-Greenstein g, 0..0.9); `lights`
    /// the practical lights' share (their glow in the air).</item>
    /// <item>`beams`: volumetric beams in place of the billboard shafts: `on`; `gain` on the shafts' light; `dust` how
    /// much the scrolling dust breaks them up (0..1); `soft` the soft edge's power (view angle).</item>
    /// <item>`weather`: `kind` "", "rain", "drizzle" or "snow"; `amount` (0..1 of the kind's full fall); `wind` (m/s along x);
    /// `wet` the wet surfaces (0..1); `puddles` (0..1); `lightning` the flashes' strength (0 none), every `interval` s on
    /// average, in `lightningColor` (sRGB).</item>
    /// </list>
    /// </summary>
    [Serializable] public sealed class MapLookAdvanced {public MapLookGi gi;public MapLookScatter scatter;public MapLookBeams beams;public MapLookWeather weather;}
    [Serializable] public sealed class MapLookGi {public bool on;public float occlusion,bounce,floor,cell,actors;}
    [Serializable] public sealed class MapLookScatter {public bool on;public int color;public float density,height,falloff,max,key,phase,lights;}
    [Serializable] public sealed class MapLookBeams {public bool on;public float gain,dust,soft;}
    [Serializable] public sealed class MapLookWeather {public string kind;public float amount,wind,wet,puddles,lightning,interval;public int lightningColor;}

    /// <summary>The graphics tiers (Settings · Graphics): Low is today's performance tier; Medium and High both draw everything
    /// the game drew before, and differ only in the advanced lighting (docs/LIGHTING-ADVANCED.md).</summary>
    public enum NativeGraphicsTier{Low=0,Medium=1,High=2}

    /// <summary>
    /// The graphics choice to a tier. "performance" is Low, "balanced" Medium, "high" High; "auto" keeps today's rule for Low
    /// (under 2 GB of video memory or below shader model 4.5) and picks Medium under <see cref="AutoHighMemory"/> MB, High from it.
    /// An unknown saved value reads as "auto".
    /// </summary>
    public static class NativeGraphicsTiers
    {
        public static readonly string[] Choices={"auto","high","balanced","performance"};
        public static readonly string[] Labels={"Auto · targets 60 FPS","High · sharper image","Balanced · lighter lighting","Performance · no shadows"};
        public const int AutoLowMemory=2048,AutoLowShader=45,AutoHighMemory=6000;
        public static NativeGraphicsTier Pick(string choice,int memoryMb,int shaderLevel)
        {
            switch(choice){case "performance":return NativeGraphicsTier.Low;case "balanced":return NativeGraphicsTier.Medium;case "high":return NativeGraphicsTier.High;}
            if(memoryMb<AutoLowMemory||shaderLevel<AutoLowShader)return NativeGraphicsTier.Low;
            return memoryMb<AutoHighMemory?NativeGraphicsTier.Medium:NativeGraphicsTier.High;
        }
        /// <summary>A saved choice the settings know, else "auto".</summary>
        public static string Known(string choice)=>Array.IndexOf(Choices,choice)>=0?choice:"auto";
    }

    /// <summary>What the runtime applies of a look's `advanced` on a tier (<see cref="NativeAdvancedLooks.Resolve"/>): every value
    /// 0 for a part that is off, so the shaders' globals read as today's look.</summary>
    public sealed class AdvancedLightState
    {
        public bool Gi;public float GiOcclusion,GiBounce,GiFloor,GiCell,GiActors;public int GiRays;
        public bool Scatter;public int ScatterColor;public float Density,Height,Falloff,Max,Key,Phase,Lights;
        public bool Beams;public float BeamGain,BeamDust,BeamSoft;public int BeamSamples;
        public string Weather="";public float Amount,Wind,Wet,Puddles,Lightning,Interval;public int LightningColor;
        public bool Any=>Gi||Scatter||Beams||Weather.Length>0;
        public void Clear(){Gi=Scatter=Beams=false;GiOcclusion=GiBounce=GiFloor=GiCell=GiActors=0;GiRays=0;ScatterColor=0;Density=Height=Falloff=Max=Key=Phase=Lights=0;BeamGain=BeamDust=BeamSoft=0;BeamSamples=0;Weather="";Amount=Wind=Wet=Puddles=Lightning=Interval=0;LightningColor=0;}
    }

    /// <summary>
    /// The `advanced` block's rules: validation (MapLooks' loader calls it for every look), the defaults, and the one mapping
    /// from a look and a graphics tier to what the runtime applies. The low tier is exactly today's look (everything off: the
    /// billboard shafts, no volume, no scattering, no new weather; Cyber City keeps its own rain). Medium bakes a coarser
    /// volume with fewer rays, draws the beams without their dust's second octave and three fifths of the weather; High is
    /// everything. Reduced effects halves the weather's fall and the lightning. Pure; Resolve allocates nothing.
    /// </summary>
    public static class NativeAdvancedLooks
    {
        public const string Rain="rain",Drizzle="drizzle",Snow="snow";
        /// <summary>The volume's rays per cell on Medium and High, and Medium's widening of the cell.</summary>
        public const int RaysMedium=28,RaysHigh=40;public const float MediumCell=1.25f;
        /// <summary>Medium's share of the weather's fall, and Reduced effects' of the fall and the lightning.</summary>
        public const float MediumWeather=.6f,ReducedWeather=.5f,ReducedLightning=.5f;
        /// <summary>The defaults a written value of 0 takes (cell, soft edge, phase none, lightning every 14 s).</summary>
        public const float DefaultCell=1,DefaultSoft=2,DefaultInterval=14,DefaultFalloff=6;
        public static bool GiOn(MapLook look)=>look?.advanced?.gi!=null&&look.advanced.gi.on;
        public static bool ScatterOn(MapLook look)=>look?.advanced?.scatter!=null&&look.advanced.scatter.on;
        public static bool BeamsOn(MapLook look)=>look?.advanced?.beams!=null&&look.advanced.beams.on;
        public static string WeatherOf(MapLook look){var w=look?.advanced?.weather;return w==null||string.IsNullOrEmpty(w.kind)?"":w.kind;}

        /// <summary>The applied values for `look` on `tier` (Reduced effects `reduced`) into `into`.</summary>
        public static void Resolve(MapLook look,NativeGraphicsTier tier,bool reduced,AdvancedLightState into)
        {
            var s=into;s.Clear();if(look?.advanced==null||tier==NativeGraphicsTier.Low)return;var a=look.advanced;bool high=tier==NativeGraphicsTier.High;
            if(GiOn(look))
            {
                var g=a.gi;s.Gi=true;s.GiOcclusion=Clamp01(g.occlusion);s.GiBounce=Math.Max(0,g.bounce);s.GiFloor=Clamp01(g.floor);s.GiActors=Clamp01(g.actors);
                s.GiCell=(g.cell>0?g.cell:DefaultCell)*(high?1:MediumCell);s.GiRays=high?RaysHigh:RaysMedium;
            }
            if(ScatterOn(look))
            {
                var f=a.scatter;s.Scatter=true;s.ScatterColor=f.color;s.Density=Math.Max(0,f.density);s.Height=f.height;s.Falloff=f.falloff>0?f.falloff:DefaultFalloff;s.Max=Clamp01(f.max);
                s.Key=Math.Max(0,f.key);s.Phase=Math.Max(0,Math.Min(.9f,f.phase));s.Lights=Math.Max(0,f.lights);
            }
            if(BeamsOn(look)){var b=a.beams;s.Beams=true;s.BeamGain=Math.Max(0,b.gain);s.BeamDust=Clamp01(b.dust);s.BeamSoft=b.soft>0?b.soft:DefaultSoft;s.BeamSamples=high?2:1;}
            string kind=WeatherOf(look);
            if(kind.Length>0)
            {
                var w=a.weather;s.Weather=kind;s.Amount=Clamp01(w.amount)*(high?1:MediumWeather)*(reduced?ReducedWeather:1);s.Wind=w.wind;s.Wet=Clamp01(w.wet);s.Puddles=Clamp01(w.puddles);
                s.Lightning=Math.Max(0,w.lightning)*(reduced?ReducedLightning:1);s.Interval=w.interval>0?w.interval:DefaultInterval;s.LightningColor=w.lightningColor;
            }
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static bool Finite(params float[] v){foreach(var x in v)if(float.IsNaN(x)||float.IsInfinity(x))return false;return true;}
        /// <summary>Throws on an `advanced` block the runtime cannot apply as written (MapLooks' Validate calls it for each look).</summary>
        public static void Validate(string id,MapLookAdvanced a)
        {
            if(a==null)return;void Fail(string what)=>throw new InvalidOperationException("MapLooks.json "+id+": advanced "+what);
            var g=a.gi;if(g!=null&&(!Finite(g.occlusion,g.bounce,g.floor,g.cell,g.actors)||g.occlusion<0||g.occlusion>1||g.bounce<0||g.bounce>4||g.floor<0||g.floor>1||g.cell<0||g.cell>4||g.actors<0||g.actors>1))Fail("gi");
            var f=a.scatter;if(f!=null&&(!Finite(f.density,f.height,f.falloff,f.max,f.key,f.phase,f.lights)||f.color<0||f.color>0xffffff||f.density<0||f.density>.5f||f.falloff<0||f.max<0||f.max>1||f.key<0||f.phase<0||f.phase>.9f||f.lights<0||f.on&&!(f.density>0)))Fail("scatter");
            var b=a.beams;if(b!=null&&(!Finite(b.gain,b.dust,b.soft)||b.gain<0||b.gain>8||b.dust<0||b.dust>1||b.soft<0||b.soft>8))Fail("beams");
            var w=a.weather;
            if(w!=null)
            {
                string k=w.kind??"";if(k!=""&&k!=Rain&&k!=Drizzle&&k!=Snow)Fail("weather kind "+k);
                if(!Finite(w.amount,w.wind,w.wet,w.puddles,w.lightning,w.interval)||w.amount<0||w.amount>1||w.wet<0||w.wet>1||w.puddles<0||w.puddles>1||w.lightning<0||w.lightning>4||w.interval<0||w.lightningColor<0||w.lightningColor>0xffffff||Math.Abs(w.wind)>20)Fail("weather");
                if(k==""&&(w.amount>0||w.lightning>0))Fail("weather without a kind");
            }
        }
    }
}
