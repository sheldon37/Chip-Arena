namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's sounds (docs/audio/sound-requests-noctiel.json, docs/EXPEDITION-BOSS-FINAL.md section 17): the `boss.noctiel.*`
    /// keys and when each plays. The owner generates the clips; until the batch is imported a key has no clips and is silent.
    /// The keys are in the bank contract (Core/NativeSoundBank.cs); 2D (<see cref="Flat"/>) keys play flat, the rest at their place.
    /// The cues that fire them come with the imported batch.
    /// </summary>
    public static class NativeFallenBossCues
    {
        public static readonly string[] Keys={
            "boss.noctiel.step","boss.noctiel.glide","boss.noctiel.wings.open","boss.noctiel.wings.beat","boss.noctiel.plate","boss.noctiel.hurt",
            "boss.noctiel.feather","boss.noctiel.ripple","boss.noctiel.gauntlet","boss.noctiel.veins","boss.noctiel.halo.crack","boss.noctiel.rise",
            "boss.noctiel.mask","boss.noctiel.sword.pull","boss.noctiel.slash","boss.noctiel.slash.part","boss.noctiel.slowdown","boss.noctiel.kneel",
            "boss.noctiel.chip","boss.noctiel.cleave","boss.noctiel.vow.draw","boss.noctiel.vow","boss.noctiel.severance.wind","boss.noctiel.severance",
            "boss.noctiel.wave","boss.noctiel.leap","boss.noctiel.descent.mark","boss.noctiel.land","boss.noctiel.rain.lines","boss.noctiel.rain",
            "boss.noctiel.verdict.plant","boss.noctiel.verdict.crack","boss.noctiel.verdict.split","boss.noctiel.choir","boss.noctiel.cantor","boss.noctiel.cantor.death",
            "boss.noctiel.sweep.shake","boss.noctiel.sweep","boss.noctiel.eclipse","boss.noctiel.eclipse.pool","boss.noctiel.penance","boss.noctiel.penitent",
            "boss.noctiel.penitent.latch","boss.noctiel.fall.mark","boss.noctiel.fall","boss.noctiel.throne","boss.noctiel.throne.fire","boss.noctiel.throne.break",
            "boss.noctiel.hush","boss.noctiel.hush.open","boss.noctiel.life.lost","boss.noctiel.return","boss.noctiel.revive","boss.noctiel.revive.done",
            "boss.noctiel.death","boss.noctiel.shatter","boss.noctiel.enrage"};
        /// <summary>When each key plays, in the same order (the doc's table reads from this).</summary>
        public static readonly string[] Briefs={
            "a footfall on the water (the feet the live motion plants)","it takes off into a glide to chase","its wings spread (the cutscene, a long charge, Feather Rain, Fall of Light)",
            "the wings thrown out on a strike","standing turns and small moves: its plate shifting","a pilot's hit lands on it",
            "the cutscene's one feather touches the water (0:04-0:15)","a slow ring across the whole water (the feather, each pair of wings opening)","the close-up: the gauntlet on the hilt (0:28)",
            "black veins crawl up the blade","the crack runs through the halo","it rises from its knee (0:39)",
            "the blank mask lifts toward the lens (0:48)","it pulls the sword out of the water","the Slash cuts the screen (the opening on the drop, every phase change)",
            "the two halves of the frame slide apart","time slows hard as it swings at the camera","it drops to one knee at a threshold",
            "each of its three modifier chips turns over on the beat","each of Cleave's three cuts","Vow's tell: the water ahead parts in a line",
            "Vow's thrust crosses its lane in a blink","Severance's tell: the blade held wide","the flat swing throws the crescent across the map (and the second wing)",
            "the crescent passing close to a pilot","Descent: it leaps into the dark","Descent's tell: the white ring closing on its pilot",
            "Descent lands on the hit","Feather Rain's tell: thin lines of light on the water","a steel feather falls into its lane",
            "Verdict / Last Verdict: it plants the sword","the OHK tell: the crack brightens across the whole map (2.4 s; 2.8 s for Last Verdict)","the map splits along the crack",
            "Choir: it lifts its off hand and the Cantors come","a Cantor sings its beam at a pilot","a Cantor is killed",
            "Wing Sweep's tell: the blade wings spread and shake","the full-circle Wing Sweep","the OHK tell: the dark rolls in from the edge (3.2 s)",
            "a pool of light opens on the water","Penance: it pours from the blade","a Penitent rises from the water by a pilot",
            "a Penitent latches on","Fall of Light's tell: a bright square on the water, one bar ahead","a column of light falls",
            "a Throne hangs and turns","a Throne shoots down at a pilot","a Throne is brought down",
            "the Hush warning: the water stills and brightens under a pilot who stands still","the water opens under a pilot who stayed still","a pilot loses a feather (a life)",
            "a pilot falls back in from the dark","a teammate starts giving a feather (the revive hold)","the feather is given and the pilot stands",
            "NOCTIEL falls: its plate cracks and the light goes out","the hollow shell shatters into the water (the song's hit at 4:08)","The Light Goes Out (the 12:00 enrage): white, then black"};
        /// <summary>The 2D keys: the cutscene, the Slash and chip screen, the map-wide tells, the Hush, a pilot's own lives, the end.</summary>
        public static bool Flat(int key)=>key==6||key==7||key==8||key==9||key==10||key==11||key==12||key==13||key==14||key==15||key==16||key==18||key==28||key==31||key==32||key==38||key==48||key==50||key==52||key==53||key==54||key==55||key==56;
    }
}
