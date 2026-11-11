using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Brickfall's stage art (docs/arenas/BRICKFALL-ART.md, docs/arenas/STAGE-ART.md): the rules the stage bank
    /// ChipArenaStageBrickfall (tools/stages/brickfall_art.py, brickfall_props.py, brickfall_kit.py), the runtime
    /// (Runtime NativeBrickfallPresentation, NativeStageArtDraw) and the checks share. Brickfall has no Core kit: its art is its
    /// map blocks, so every block is modelled. The static blocks are world-space chunks; every block the simulation moves or
    /// breaks (the carried slabs, decks, rails and drums, the masonry panels, the structural members, the cover props) is a
    /// `block/&lt;id&gt;` model drawn where its block is, so it sags, falls and breaks exactly as its box did; each segment's rubble
    /// is one heap model on the segment's first rubble block (<see cref="Heap"/>), the other rubble blocks covered.
    /// The collapse telegraph stays the dressing's (Runtime NativeArenaDressing, Core NativeStructureFx.TelegraphFor): a segment's
    /// own blocks sag and lean with it, a banded member's lamp is the model's glow in the lamp's colour (<see cref="Lamp"/>), and
    /// every other model's glow (its lit windows) follows the telegraph's window light. Presentation only; pure.
    /// </summary>
    public static class NativeBrickfallArt
    {
        public const string Bank="ChipArenaStageBrickfall";
        /// <summary>A dark lamp's lens still shows a little (linear).</summary>
        public const float LampDark=.02f;
        /// <summary>The segment a block answers to: its own, else the first it supports; null for a static block or a prop.</summary>
        public static string Segment(ContentMap map,ContentBlock b)
        {
            if(map?.segments==null||b==null||b.role=="static"||b.role=="rubble")return null;
            foreach(var s in map.segments){if(s.id==b.segment)return s.id;if(s.supports!=null)foreach(var p in s.supports)if(p.member==b.id)return s.id;}
            return null;
        }
        /// <summary>Whether the block wears the load-bearing band and its lamp (the map look's blockLooks, as the dressing reads them).</summary>
        public static bool Banded(MapLook look,ContentBlock b)=>b!=null&&(NativeBlockLooks.Resolve(look?.blockLooks,null,b.id,b.material,0xffffff).Flags&NativeBlockLooks.Band)!=0;
        /// <summary>The rubble block that carries its segment's heap model (the first); the segment's other rubble blocks are
        /// covered by it and draw nothing.</summary>
        public static string Heap(StructureDefinition s)=>s?.rubble!=null&&s.rubble.Length>0?s.rubble[0].id:null;
        /// <summary>The glow of a banded member's model (its lamp lens, linear rgb): NativeBlockLooks.Lamp's flags in the dressing's
        /// colours (the shader's lamp glow, NativeBlockLooks.LampGlow).</summary>
        public static void Lamp(int flags,out float r,out float g,out float b)
        {
            int hex=(flags&NativeBlockLooks.LampRed)!=0?NativeBlockLooks.LampRedColor:(flags&NativeBlockLooks.LampAmber)!=0?NativeBlockLooks.LampAmberColor:-1;
            if(hex<0){r=g=b=LampDark;return;}
            r=Linear((hex>>16&255)/255f)*NativeBlockLooks.LampGlow;g=Linear((hex>>8&255)/255f)*NativeBlockLooks.LampGlow;b=Linear((hex&255)/255f)*NativeBlockLooks.LampGlow;
        }
        private static float Linear(float c)=>c<=.04045f?c/12.92f:(float)Math.Pow((c+.055f)/1.055f,2.4);
    }
}
