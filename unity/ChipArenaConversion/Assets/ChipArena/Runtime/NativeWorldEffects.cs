using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private void WorldEffects(NativeArena arena)
        {
            // Chip fields and ability zones are never outlined (owner request 2026-10-04): NativeFieldPresentation draws their
            // ground glow, cores and domes, NativeBlastPresentation their volume. The Siren's fields are its scorch decals and
            // eruption warnings (C9) and black flames, eruption columns and rain (C12, ArenaPresentation.DevEffects). Leet fields
            // (nuke field, tree) and storm-step trails are C12's DevFighters. Marked's crosshair on the marked player is the
            // source's camera-facing warn-red reticle over everything (ArenaPresentation.SirenMarks), not a ring of cubes.
            // A pilot under Admin Pause, a possession and a damage shield draw nothing in 3D, as in the source: Admin Pause is the
            // green leet-freeze burst (NativeImpactCues) and the ADMIN PAUSE / CONTROLLING / POSSESSED BY box (NativeCombatHud);
            // the shield is invisible (chaos.ts keeps it in a private map). The first port's cube cage, tether and ring are gone.
            // Ward Zero's floor vents (the boss song's eruptions and the pits): rim, wisps, eruption burst and jets, open flicker and
            // smoke are NativeStageVentPresentation, run by WardPresentation with the entries it swings the lids from.
            // A minion death draws no burst here: a horde add keeps its corpse (pve/horde-view.ts; gore only on a gib) and a summon
            // breaks up at SUMMON_LOOK.death.breakAt (chaos-visuals.ts), both in SourceMinionDeath.
        }
    }
}
