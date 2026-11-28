using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private NativeMeridianStreetLifeDraw liveStreetLife;

        /// <summary>The live run's street life (docs/expedition/MERIDIAN-STREETS.md): one call in ShowMeridian once the street dress,
        /// the bleed and the landmarks are made, from the same records (the deal's city, its world seed, its hall deal), with what they
        /// draw to keep off: the dress's plan, and the bleed's only while the bleed draws. Planned on a worker; nothing without the
        /// streets' bank. The dress's own hooks stay as they were.</summary>
        private void StreetLifeOpen(ExpeditionGeneratedWorld world,uint layout,NativeCityBleed bleed,NativeCityBleedDress.Plan bleedPlan)
        {
            StreetLifeClose();if(world?.City==null)return;
            try
            {
                var input=NativeMeridianStreetLife.InputOf(world.City,layout,world.Blueprint.worldSeed,world.HallSlots,liveStreets!=null?liveStreets.Plan:null,bleed,bleedPlan);
                // Meridian's route draws its crossing, footways, shopfront and sign there (docs/expedition/MERIDIAN-ROUTE.md)
                if(liveRoutePlan!=null)input.KeepOut.AddRange(liveRoutePlan.KeepOut);
                liveStreetLife=NativeMeridianStreetLifeDraw.OpenAsync(input,cameraView);
            }
            catch(Exception e){Debug.LogException(e);liveStreetLife=null;}
        }
        /// <summary>The live run's street life dropped (the one line in ShowMeridian's teardown).</summary>
        private void StreetLifeClose(){if(liveStreetLife!=null)liveStreetLife.Close();liveStreetLife=null;}
    }
}
