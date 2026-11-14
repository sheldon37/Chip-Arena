using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>Central Station's presentation lifetime (Runtime/NativeExpeditionStationPresentation.cs): added beside
    /// ArenaPresentation the first time the station draws, it releases the station's material, mesh and effect batches when
    /// the presentation goes, so the station needs no line in the shared teardown.</summary>
    public sealed class NativeStationLifetime : MonoBehaviour
    {
        public Action Gone;
        private void OnDestroy(){var g=Gone;Gone=null;g?.Invoke();}
    }
}
