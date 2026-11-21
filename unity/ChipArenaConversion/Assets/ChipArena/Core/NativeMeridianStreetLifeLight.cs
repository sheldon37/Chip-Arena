using System;

namespace ChipArena.UnityConversion
{
    // The street life's lit pieces on the city's lamp grid (2026-11-21, with the street lamps: a flat pool disc under each read as light
    // laid over the floor, not on it; Core NativeCityLamps, Runtime NativeMeridianStreetLifeDraw).
    public static partial class NativeMeridianStreetLife
    {
        /// <summary>A lit piece's light: from its head, its reach its pool's radius times its glow's share, its strength (times its
        /// linear colour r, g, b) by its glow; a flood a cone down its aim toward its pool's centre (`pool`, on the floor).</summary>
        public static NativeCityLamps.Lamp CityLight(Kind k,V3 head,V3 pool,float r,float g,float b)
        {
            float reach,strength,spot=0;
            switch(k.Glow)
            {
                case Glow.Flood:reach=1.65f;strength=2.8f;spot=2.5f;break;
                case Glow.Fire:reach=1.55f;strength=1.8f;break;
                case Glow.Flare:reach=1.5f;strength=1.6f;break;
                case Glow.Sign:reach=1.55f;strength=1f;break;
                case Glow.Festoon:reach=1.9f;strength=.6f;break;
                default:reach=1.5f;strength=.7f;break;
            }
            var aim=pool-head;float len=aim.Length;aim=spot>0&&len>1e-4f?aim/len:new V3(0,-1,0);
            return new NativeCityLamps.Lamp{At=head,Range=k.Pool*reach,R=r*strength,G=g*strength,B=b*strength,Aim=aim,Spot=spot,Floor=NativeCityLamps.SpotFloor};
        }
    }
}
