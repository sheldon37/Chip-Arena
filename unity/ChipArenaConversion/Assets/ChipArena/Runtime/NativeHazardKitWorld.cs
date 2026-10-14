using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The hazard kit's scene for an arena (docs/HAZARD-KIT.md), shared by its presentation (NativeHazardKitPresentation.cs)
    /// and its sound (NativeHazardKitAudio.cs) so both see the same pieces in the same phases. The pieces are the map's
    /// presentation-only placements (Resources/HazardKit.json, by map id or look), or the DEV preview gallery in the Dev
    /// Sandbox. Each frame the adapter here reads the replicated state the triggers name (the match's fight start, the PvE
    /// wave, reveal, boss phase and outcome, the Ward breach records, the stage hazards' own clocks, the DEV hazard pulses)
    /// and fires the scene's ledger; nothing here writes sim data, collides, damages or enters the network frame.
    /// Adapters for systems not yet bound to an arena (an Expedition door sealing) call <see cref="Event"/>.
    /// </summary>
    public static class NativeHazardKitWorld
    {
        private sealed class Slot
        {
            public NativeArena Arena;public int Generation=-1,Frame=-1;public ContentMap Map;public bool Gallery;public NativeKitScene Scene;
            public int Wave=-1,BossPhase=-1;public string Stage;public double WaveAt=double.NaN,BossAt=double.NaN,PhaseAt=double.NaN;public double[] HazardWarn=Array.Empty<double>();
            public int[] Follow=Array.Empty<int>();public MapHazard[] Hazards=Array.Empty<MapHazard>();
        }
        private static readonly Slot[] slots={new Slot(),new Slot()};private static KitSet set;private static bool read;
        /// <summary>DEV preview (Sandbox only): the gallery is on, its music lock is on, and where and when it was opened.</summary>
        public static bool Gallery,GalleryMusic;public static Vector3 GalleryOrigin;public static float GalleryYaw;public static double GalleryAt=double.NaN,GalleryMusicAt=double.NaN;
        /// <summary>Settings · Alarm lights (Reduced effects forces at least Reduced).</summary>
        public static NativeKitFlash Flash=NativeKitFlash.Full;
        /// <summary>How long one gallery cycle runs (s) and each piece's offset into it.</summary>
        public const double GalleryCycle=12,GalleryStagger=.45;
        private static readonly Dictionary<string,(V3 pos,FactoryQ rot)> anchors=new Dictionary<string,(V3,FactoryQ)>(StringComparer.Ordinal);private static ContentMap anchorMap;

        /// <summary>The placements file (read and validated once; a broken file disables the kit with a warning).</summary>
        public static KitSet Placements
        {
            get
            {
                if(read)return set;read=true;
                try{var asset=Resources.Load<TextAsset>("HazardKit");if(asset!=null){var s=JsonUtility.FromJson<KitSet>(asset.text);NativeHazardKit.Validate(s);set=s;}}
                catch(Exception e){Debug.LogWarning("Hazard kit placements disabled: "+e.Message);set=null;}
                return set;
            }
        }
        private static (bool,V3,FactoryQ) Anchor(string id)
        {
            if(anchors.TryGetValue(id,out var a))return(true,a.pos,a.rot);return(false,default,FactoryQ.Identity);
        }
        private static void Anchors(ContentMap map)
        {
            if(ReferenceEquals(map,anchorMap))return;anchorMap=map;anchors.Clear();
            foreach(var b in map?.blocks??Array.Empty<ContentBlock>())if(b?.id!=null){var r=b.rotation;anchors[b.id]=(b.pos,r==null?FactoryQ.Identity:new FactoryQ{X=r.x,Y=r.y,Z=r.z,W=r.w});}
        }
        private static Slot SlotOf(NativeArena arena)
        {
            foreach(var s in slots)if(ReferenceEquals(s.Arena,arena))return s;
            // The live arena keeps slot 0; a replay arena takes slot 1.
            var slot=arena.ReplicaScene.IsValid()?slots[1]:slots[0];slot.Arena=arena;slot.Generation=-1;slot.Scene=null;return slot;
        }
        /// <summary>The DEV gallery shows in this arena (the offline Developer Sandbox only).</summary>
        public static bool GalleryShown(NativeArena arena)=>Gallery&&arena!=null&&arena.Sandbox&&!arena.ReplicaScene.IsValid()&&!double.IsNaN(GalleryAt);
        /// <summary>The beat the kit hears: the boss song while it plays; the gallery's preview of it when its music lock is on.</summary>
        public static NativeKitBeat Beat(NativeArena arena,double now)
        {
            if(arena==null)return NativeKitBeat.Off;var track=arena.BossTrack;
            if(arena.BossSongTime>=0)return NativeKitBeat.Of(track,arena.BossSongTime);
            if(GalleryShown(arena)&&GalleryMusic&&!double.IsNaN(GalleryMusicAt))return NativeKitBeat.Of(track,now-GalleryMusicAt);
            return NativeKitBeat.Off;
        }
        /// <summary>When the gallery's music preview started (arena clock), or NaN when it does not play.</summary>
        public static double GallerySongAt(NativeArena arena)=>GalleryShown(arena)&&GalleryMusic?GalleryMusicAt:double.NaN;

        /// <summary>
        /// The arena's kit scene stepped to `now` (null: no kit on this map). The adapter reads the triggers once a frame; the
        /// phases are re-stepped on every call (the presentation's clock may run a little ahead of the audio's).
        /// </summary>
        public static NativeKitScene For(NativeArena arena,double now)
        {
            if(arena?.Map==null)return null;var slot=SlotOf(arena);bool gallery=GalleryShown(arena);
            if(slot.Scene==null||slot.Generation!=arena.Generation||!ReferenceEquals(slot.Map,arena.Map)||slot.Gallery!=gallery)Build(slot,arena,gallery);
            var scene=slot.Scene;if(scene==null)return null;
            if(slot.Frame!=Time.frameCount){slot.Frame=Time.frameCount;if(gallery)GalleryTriggers(scene,now);else Triggers(slot,arena,now);}
            for(int i=0;i<slot.Follow.Length;i++){int h=slot.Follow[i];if(h>=0)scene.Follow(i,NativeWardZeroHazards.At(slot.Hazards[h],arena.Match.Phase,NativeStructureFx.RoundIndex(arena.Match.Set,arena.Match.Round),arena.Match.FightAt,now));}
            scene.Step(now,Flash,Beat(arena,now),gallery);return scene;
        }
        /// <summary>An event from a system with no arena hook of its own (an Expedition door sealing: "door-seal"), at `at`.</summary>
        public static int Event(NativeArena arena,string evt,double at,Vector3? where=null,string hazard=null)
        {
            var scene=For(arena,arena?.Time??0);if(scene==null)return 0;return scene.Fire(evt,at,where.HasValue,where.HasValue?NativeArena.Vec(where.Value):default,hazard);
        }
        private static void Build(Slot slot,NativeArena arena,bool gallery)
        {
            slot.Generation=arena.Generation;slot.Map=arena.Map;slot.Gallery=gallery;slot.Wave=slot.BossPhase=-1;slot.Stage=null;slot.WaveAt=slot.BossAt=slot.PhaseAt=double.NaN;
            KitRecord[] records;
            if(gallery){var o=NativeArena.Vec(GalleryOrigin);records=NativeHazardKit.Gallery(o,GalleryYaw*Math.PI/180);}
            else records=NativeHazardKit.For(Placements,arena.Map.id,arena.Map.look);
            if(records.Length==0){slot.Scene=null;slot.Follow=Array.Empty<int>();return;}
            Anchors(arena.Map);slot.Scene=new NativeKitScene(records,Anchor){Bays=gallery};
            // Pieces that follow a Ward stage hazard (the shutters): the hazard's index on this map, -1 none.
            slot.Hazards=arena.Map.hazards??Array.Empty<MapHazard>();slot.Follow=new int[records.Length];slot.HazardWarn=new double[slot.Hazards.Length];for(int h=0;h<slot.HazardWarn.Length;h++)slot.HazardWarn[h]=double.NaN;
            for(int i=0;i<records.Length;i++){slot.Follow[i]=-1;var hz=records[i].trigger?.hazard;if(hz==null)continue;for(int h=0;h<slot.Hazards.Length;h++)if(slot.Hazards[h]?.id==hz&&NativeStageHazards.Ward(slot.Hazards[h])){slot.Follow[i]=h;break;}}
        }
        // The gallery's manual cycle: each piece fires once a cycle, staggered along the row (turrets run their own preview
        // timeline; saws and alarms fire).
        private static void GalleryTriggers(NativeKitScene scene,double now)
        {
            double t=now-GalleryAt;if(t<0)return;
            for(int i=0;i<scene.Records.Length;i++)
            {
                if(scene.Records[i].kind==NativeHazardKit.Turret)continue;double offset=1.2+i*GalleryStagger,k=Math.Floor((t-offset)/GalleryCycle);if(k<0)continue;
                double fired=GalleryAt+offset+k*GalleryCycle;scene.FireAt(i,fired);
                // A saw takes a scripted hit 1.4 s into its run each cycle (the blood and the wet chop; no one is hurt).
                if(scene.Records[i].kind==NativeHazardKit.Saw){double hit=fired+NativeHazardKit.Warn(scene.Records[i])+1.4;if(hit<=now)scene.Hit(i,hit);}
            }
        }
        // The replicated state each trigger reads. Every source is reported with its own time, so the ledger stays idempotent.
        private static void Triggers(Slot slot,NativeArena arena,double now)
        {
            var scene=slot.Scene;var match=arena.Match;
            if(match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result)scene.Fire("round-start",match.FightAt);
            var pve=arena.Pve;
            if(pve!=null)
            {
                if(pve.Stage=="wave"&&(pve.Wave!=slot.Wave||slot.Stage!="wave")){slot.Wave=pve.Wave;slot.WaveAt=now;}
                if(pve.Stage=="boss"&&pve.Phase!=slot.BossPhase){if(slot.BossPhase>=1)slot.PhaseAt=now;slot.BossPhase=pve.Phase;}
                slot.Stage=pve.Stage;
                if(!double.IsNaN(slot.WaveAt))scene.Fire("wave-start",slot.WaveAt);if(!double.IsNaN(slot.PhaseAt))scene.Fire("boss-phase",slot.PhaseAt);
                if(pve.Holding){double began=now-(pve.HoldDuration-pve.HoldLeft);if(pve.Cinematic=="reveal")scene.Fire("boss-reveal",began);else if(pve.Cinematic=="victory")scene.Fire("victory",began);else if(pve.Cinematic=="defeat")scene.Fire("defeat",began);}
            }
            if(arena.WardBreaches is List<NativeBreachRecord> breaches)
            {
                var entries=NativeArena.StageContent?.entries;
                for(int i=0;i<breaches.Count;i++)
                {
                    var b=breaches[i];if(b==null||b.breakAt>now)continue;V3 at;
                    if(b.Dynamic)at=b.point;else if(entries!=null&&b.entry>=0&&b.entry<entries.Length&&entries[b.entry]!=null)at=entries[b.entry].center;else continue;
                    scene.Fire("breach",b.breakAt,true,at);
                }
            }
            // The Ward stage hazards: a shutter's warning start is a lockdown (pieces following it read its phase as well).
            var match0=arena.Match;int round=NativeStructureFx.RoundIndex(match0.Set,match0.Round);
            for(int h=0;h<slot.Hazards.Length;h++)
            {
                var hz=slot.Hazards[h];if(hz==null||hz.kind!=NativeWardZeroHazards.Shutter)continue;var s=NativeWardZeroHazards.At(hz,match0.Phase,round,match0.FightAt,now);
                if(s.Phase==NativeHazardPhase.Warn)slot.HazardWarn[h]=now-s.Since;if(!double.IsNaN(slot.HazardWarn[h]))scene.Fire("lockdown",slot.HazardWarn[h],true,hz.center,hz.id);
            }
            foreach(var p in arena.DevHazardPulses)if(p!=null&&p.at<=now)scene.Fire("dev-trigger",p.at,true,NativeArena.Vec(p.position),p.id);
        }
    }
}
