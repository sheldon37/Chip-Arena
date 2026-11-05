using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM, Level 0's boss (docs/EXPEDITION-BOSS-BACKROOMS.md section 3): the names the rest of the game reads, the intro's shots and
    /// card. Presentation only until Astra binds the record.
    /// </summary>
    public static class NativeHumBoss
    {
        public const string Archetype="level0-hum",Name="THE HUM",Area=NativeBackroomsGenerator.AreaId;
        public static readonly BossIntroShot[] Shots=Array.Empty<BossIntroShot>();
        public static readonly BossIntroCard Card=new BossIntroCard{Eyebrow="BOSS · LEVEL 0",Title="THE HUM",Line="IT HAS HEARD YOU",Ink=0x060502,TopLine=0xfff2c0,Rule=0xffe9a0,BarFrom=0xc8a820,BarTo=0xffffe8,Letters=0xfffef0,Glow=0xffe680,SplitA=0xffd040,SplitB=0xf4fff0};
    }
    /// <summary>THE HUM's DEV preview (DEV panel · Camera · "Level 0 boss preview"): a scripted record on the presentation clock, nothing
    /// spawned. The capture tool reads these fields by name, as it reads THE 6:47's.</summary>
    public static class NativeHumBossPreview
    {
        public static bool On;public static double Start=double.NaN,ShownClock;
    }
}
