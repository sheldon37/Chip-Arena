using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE DEEP END, the Poolrooms' boss (docs/EXPEDITION-BOSS-BACKROOMS.md section 4): the names the rest of the game reads, the intro's
    /// shots and card. Presentation only until Astra binds the record.
    /// </summary>
    public static class NativeDeepEndBoss
    {
        public const string Archetype="poolrooms-deep-end",Name="THE DEEP END",Area=NativePoolroomsGenerator.AreaId;
        public static readonly BossIntroShot[] Shots=Array.Empty<BossIntroShot>();
        public static readonly BossIntroCard Card=new BossIntroCard{Eyebrow="BOSS · THE POOLROOMS",Title="THE DEEP END",Line="THE WATER IS ALWAYS WARM",Ink=0x010608,TopLine=0xc4f4f4,Rule=0x9eeeee,BarFrom=0x1a9ea6,BarTo=0xf0ffff,Letters=0xf4ffff,Glow=0x6fe8e8,SplitA=0x40d8d8,SplitB=0xf8fff8};
    }
    /// <summary>THE DEEP END's DEV preview (DEV panel · Camera · "Poolrooms boss preview"): a scripted record on the presentation clock,
    /// nothing spawned. The capture tool reads these fields by name, as it reads THE 6:47's.</summary>
    public static class NativeDeepEndBossPreview
    {
        public static bool On;public static double Start=double.NaN,ShownClock;
    }
}
