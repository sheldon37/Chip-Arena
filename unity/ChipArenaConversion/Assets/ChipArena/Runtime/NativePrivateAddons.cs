using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>Optional presentation integration. No pack references or simulation authority.</summary>
    public static class NativePrivateAddons
    {
        public sealed class MapContext
        {
            public readonly string MapId;
            public readonly ContentMap Map;
            public readonly MapLook Look;
            public readonly GameObject Root;
            public MapContext(ContentMap map, GameObject root)
            { Map=map; MapId=map.id; Look=NativeLighting.Look(map); Root=root; }
        }
        // Register from RuntimeInitializeOnLoadMethod(BeforeSceneLoad). SubsystemRegistration
        // clears callbacks even when the Editor has domain reload disabled.
        public static event Action<MapContext> MapBuilt;
        public static event Action<MapContext> MapTearingDown;
        public static event Action<ContentMap,MapLook,Camera> LightingApplied;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){MapBuilt=null;MapTearingDown=null;LightingApplied=null;}
        internal static MapContext Built(ContentMap map,GameObject root)
        {
            if(MapBuilt==null&&MapTearingDown==null)return null;
            var context=new MapContext(map,root);Invoke(MapBuilt,context);return context;
        }
        internal static void TearingDown(MapContext context)
        {if(context!=null)Invoke(MapTearingDown,context);}
        private static void Invoke(Action<MapContext> handlers,MapContext context)
        {
            if(handlers==null)return;
            foreach(Action<MapContext> handler in handlers.GetInvocationList())
                try{handler(context);}catch(Exception error){Debug.LogException(error);}
        }
        internal static void Applied(ContentMap map,MapLook look,Camera camera)
        {
            if(LightingApplied==null)return;
            foreach(Action<ContentMap,MapLook,Camera> handler in LightingApplied.GetInvocationList())
                try{handler(map,look,camera);}catch(Exception error){Debug.LogException(error);}
        }
    }
}
