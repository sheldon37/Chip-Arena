using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Brickfall's stage art (docs/arenas/BRICKFALL-ART.md, docs/arenas/STAGE-ART.md; Core NativeBrickfallArt): with the stage bank
    /// ChipArenaStageBrickfall, a NativeStageArtDraw draws the modelled yard in place of the dressed blocks, in the arena and the
    /// killcam (SourceWorld, after the scenery): the static blocks as chunks, every block the simulation moves or breaks as its
    /// model where the block is, each landed segment's rubble as one heap. Brickfall has no rig: its movers are its blocks. The
    /// collapse telegraph on the models is <see cref="BrickfallTell"/>, the dressing's own. Without the bank nothing is made and
    /// the dressed blocks draw exactly as before. Presentation only; allocation free per frame once prepared.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        // The stage art: its drawer, and per block id the segment's telegraph seed's id and whether it wears the band (once per map).
        private NativeStageArtDraw brickfallArt;private ContentMap brickfallArtMap;private NativeArena brickfallArtArena;private float brickfallArtNow;private bool brickfallArtTell,brickfallArtLocked,brickfallArtRed;
        private readonly System.Collections.Generic.Dictionary<string,(string segment,bool band)> brickfallArtBlocks=new System.Collections.Generic.Dictionary<string,(string,bool)>(System.StringComparer.Ordinal);
        /// <summary>The stage art drew last frame (the dressed blocks it covers did not).</summary>
        public bool BrickfallArtDrawn{get;private set;}

        // The stage art: prepared once per map (the stage prewarm calls this path behind the loading screen), drawn each frame.
        private bool BrickfallArtPrepare(ContentMap map)
        {
            if(map==null)return false;if(brickfallArt==null){brickfallArt=gameObject.AddComponent<NativeStageArtDraw>();brickfallArt.Tell=BrickfallTell;}
            if(!brickfallArt.Prepare(map,null,null))return false;if(ReferenceEquals(map,brickfallArtMap))return true;
            brickfallArtMap=map;brickfallArtBlocks.Clear();var look=NativeLighting.Look(map);
            foreach(var b in map.blocks??System.Array.Empty<ContentBlock>())if(b?.id!=null)brickfallArtBlocks[b.id]=(NativeBrickfallArt.Segment(map,b),NativeBrickfallArt.Banded(look,b));
            return true;
        }
        private void BrickfallArt(NativeArena arena)
        {
            BrickfallArtDrawn=false;if(arena?.Map==null||!BrickfallArtPrepare(arena.Map))return;
            float now=FieldClock(arena);var fx=structureFx?.Fx;var match=arena.Match;brickfallArtArena=arena;brickfallArtNow=now;
            brickfallArtTell=fx!=null&&fx.StressHolding(now);brickfallArtLocked=match!=null&&NativeBlockLooks.Locked(match.Phase,match.FightAt,now);brickfallArtRed=NativeStructureFx.LampStrobe(now)>0;
            BrickfallArtDrawn=brickfallArt.Render(arena,WorldEye,LowGraphics,null);
        }
        /// <summary>
        /// The collapse telegraph on a block's model, as NativeArenaDressing gives it to the block's box: a stressed segment's own
        /// blocks sag and lean (Core NativeStructureFx.TelegraphFor, the same record, clock and seed) and their windows strobe, then
        /// go dark; a banded member's lamp (the model's glow) is amber while the members are locked, strobes red under stress and at
        /// its last damage stage, else is dark (NativeBlockLooks.Lamp, NativeBrickfallArt.Lamp).
        /// </summary>
        private void BrickfallTell(NativePropBody body,ref Vector3 at,ref Quaternion rotation,ref Vector4 glow)
        {
            var arena=brickfallArtArena;var def=body.Definition;if(arena==null||def?.id==null||!brickfallArtBlocks.TryGetValue(def.id,out var k))return;
            float window=1;bool stressed=false;var fx=structureFx?.Fx;
            if(brickfallArtTell&&k.segment!=null&&fx!=null)
            {
                var match=arena.Match;stressed=fx.TelegraphFor(NativeStructureFx.Seed(arena.MapId,k.segment,NativeStructureFx.RoundIndex(match.Set,match.Round)),new V3(at.x,at.y,at.z),brickfallArtNow,out window,out var sag,out float qx,out float qy,out float qz,out float qw);
                if(stressed){at+=new Vector3(sag.x,sag.y,sag.z);rotation=new Quaternion(qx,qy,qz,qw)*rotation;}
            }
            if(!k.band){glow=new Vector4(window,window,window,1);return;}
            float max=NativeBlockLooks.StageMax(def,null,out float s1,out float s2);int stage=NativeStageArt.Stage(body.Health,max,body.Indestructible,s1,s2);
            NativeBrickfallArt.Lamp(NativeBlockLooks.Lamp(stage,stressed,brickfallArtLocked,brickfallArtRed),out float r,out float g,out float b);glow=new Vector4(r,g,b,1);
        }
    }
}
