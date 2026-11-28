using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>THE CONVERTER's presentation lifetime (Runtime/NativeIronworksBossPresentation.cs): added beside ArenaPresentation
    /// the first time the boss draws, it releases the boss's meshes, material and effect batches when the presentation goes, so
    /// the boss needs no line in the shared teardown.</summary>
    public sealed class NativeIronworksBossLifetime : MonoBehaviour
    {
        public Action Gone;
        private void OnDestroy(){var g=Gone;Gone=null;g?.Invoke();}
    }
}
