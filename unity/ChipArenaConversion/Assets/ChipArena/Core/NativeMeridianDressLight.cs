using System;

namespace ChipArena.UnityConversion
{
    // The street lamps' light (owner, 2026-11-20: "the light doesn't seem to be on the material properly? (Like this seems to be flat, in
    // the air light)"; docs/expedition/MERIDIAN-DRESS.md "The light"): where each lamp's light sits, how far it reaches, its colour and
    // its level this frame. Runtime NativeMeridianDressDraw hands them to the city's lamp grid (NativeCityLamps) and the nearest to the
    // practical slots; MeridianLampChecks holds the walls under the brackets and the floors under the lamps to their light.
    public static partial class NativeMeridianDress
    {
        /// <summary>Sodium (bible 3.1): the lamp #FFA14A, a route home's warmer #FF8E3A (linear).</summary>
        public static readonly V3 Sodium=new V3(1f,.631f,.29f),RouteSodium=new V3(1f,.557f,.227f);
        /// <summary>A working lamp's light (times the sodium), its reach over its pool's radius, the light's drop under a swan-neck's
        /// bowl (inside its shade) and how far a flush lamp's light stands off its wall (its lens throws forward, so the wall round it
        /// takes some: at its lens it would light only the street).</summary>
        public const float LampIntensity=1.35f,LampReach=1.25f,BowlDrop=.15f,FlushOut=.3f,FlushIntensity=.85f;
        /// <summary>A flickering lamp's steps a second (the photosensitivity rule: under 3) and, with Reduced effects, its slow breath (Hz)
        /// between <see cref="ReducedFloor"/> and full, without the dark beats.</summary>
        public const float FlickerHz=2.7f,ReducedHz=.35f,ReducedFloor=.6f;

        /// <summary>A lamp's light: a swan-neck's just under its bowl, a pendant's under its bowl, a flush lamp's a little off its wall in
        /// front of its lens; its reach the pool's radius times <see cref="LampReach"/>; sodium, a route home's warmer. A dead lamp has none
        /// (reach 0: no cell lists it).</summary>
        public static NativeCityLamps.Lamp LightOf(Lamp l)
        {
            var c=l.Protected?RouteSodium:Sodium;float k=LampIntensity*(l.Kind==Flush?FlushIntensity:1);var at=l.Bowl-new V3(0,BowlDrop,0);
            if(l.Kind==Flush){var n=l.Bowl-l.Mount;n=new V3(n.x,0,n.z);float len=n.Length;at=len>1e-4f?l.Bowl+n*(FlushOut/len):l.Bowl;}
            return new NativeCityLamps.Lamp{At=at,Range=l.State==Dead?0:l.Radius*LampReach,R=c.x*k,G=c.y*k,B=c.z*k};
        }
        /// <summary>Every lamp's light, in the plan's order.</summary>
        public static NativeCityLamps.Lamp[] LightsOf(Plan plan)
        {
            var o=new NativeCityLamps.Lamp[plan.Lamps.Count];for(int i=0;i<o.Length;i++)o[i]=LightOf(plan.Lamps[i]);return o;
        }
        /// <summary>A lamp's level this frame (0 dead .. 1): a flickering lamp stutters (at most <see cref="FlickerHz"/> steps a second, now
        /// and then dropping out); with Reduced effects it only breathes slowly, never under <see cref="ReducedFloor"/>.</summary>
        public static float LampLevel(Lamp l,float time,bool reduced)
        {
            if(l.State==Dead)return 0;if(l.State==Working)return 1;
            if(reduced)return ReducedFloor+(1-ReducedFloor)*(.5f+.5f*(float)Math.Sin(2*Math.PI*(ReducedHz*time+l.Seed)));
            float t=time*FlickerHz+l.Seed*17.3f;int k=(int)Math.Floor(t);
            float h=(float)(Math.Sin(k*12.9898+l.Seed*78.233)*43758.55);h-=(float)Math.Floor(h);
            return h<.32f?.08f+.12f*h:h<.45f?.55f:1f;
        }
    }
}
