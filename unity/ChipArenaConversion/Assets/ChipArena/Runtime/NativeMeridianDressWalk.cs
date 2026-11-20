#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private NativeMeridianDressDraw meridianDress;
        /// <summary>The Developer walk's dress (Core NativeMeridianDress from the walk's own records: its map's blocks and ramps, its
        /// routes, roof hatches and hall deal), made when a layout opens; without the city bank nothing is made and the greybox
        /// draws. OpenCityWalk calls it after the look is applied (the night replaces it here); CloseCityWalk drops it.</summary>
        private void MeridianDressOpen(NativeLayeredCityWalk walk,uint layout,uint run)
        {
            MeridianDressClose();if(walk==null)return;
            var input=new NativeMeridianDress.Input{Map=walk.Map,Layout=layout,Run=run,Slots=surveySlots,Hatches=walk.RoofHatches.ToArray(),KeepOut=NativeCityBleedDress.CacheKeepOuts(walk.Caches,walk.Map),
                Routes=walk.Routes.Select(r=>new NativeMeridianDress.Route{Name=r.Name,Points=r.Points,Widths=r.Widths,Kinds=r.Kinds}).ToArray()};
            meridianDress=new GameObject("Meridian dress").AddComponent<NativeMeridianDressDraw>();
            if(!meridianDress.Prepare(input,cameraView)){Destroy(meridianDress.gameObject);meridianDress=null;return;}
            // the presentation's look table (Core NativeMeridianDress.LookTable): the walk's map draws in the night while its lamps
            // are there (the generator's look string is the walk's own record; nothing else reads it)
            var night=NativeMeridianDress.LookOf(walk.Map.id,null);
            if(night!=null){float far=cameraView.farClipPlane;walk.Map.look=night;NativeLighting.Apply(walk.Map,cameraView);cameraView.farClipPlane=far;}
        }
        private void MeridianDressClose(){if(meridianDress!=null)Destroy(meridianDress.gameObject);meridianDress=null;}
    }
}
#endif
