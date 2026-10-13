using System;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // The build stamp in the screen's bottom-right corner (owner 2026-10-13: "in one of the corners can we show the game
    // version? (This way we keep track of builds)"). A packaged player names itself in its own native-build.json
    // (Editor/NativeUpdateBuild.Package): `version` is the source hash, shown as the same 12 characters as the menu footer and
    // the update card, and `revision` is the build's UTC time in DateTime ticks. A player without the file (the Editor, a
    // copied folder that lost it) reads DEV BUILD with the content's source commit. Presentation only.
    public static class NativeBuildLabel
    {
        /// <summary>Text size, the gap to the screen's corner and the line height, in canvas units.</summary>
        public const float Size=10,Margin=6,Height=14;
        /// <summary>Earliest tick count read as a build time; smaller revisions (test fixtures, hand-made identities) show no time.</summary>
        public static readonly long EarliestTicks=new DateTime(2020,1,1,0,0,0,DateTimeKind.Utc).Ticks;
        public static string Text(NativeBuildIdentity build,string source)
        {
            if(build!=null&&!string.IsNullOrEmpty(build.version))
                return "BUILD "+Short(build.version)+(Stamp(build.revision,out string when)?" · "+when:"");
            return "DEV BUILD"+(string.IsNullOrEmpty(source)?"":" · SRC "+Short(source));
        }
        public static string Short(string id)=>id.Length>12?id.Substring(0,12):id;
        /// <summary>The build time as "yyyy-MM-dd HH:mm UTC", or false when `ticks` is not a plausible build time.</summary>
        public static bool Stamp(long ticks,out string text)
        {
            text="";if(ticks<EarliestTicks||ticks>DateTime.MaxValue.Ticks)return false;
            text=new DateTime(ticks,DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+" UTC";return true;
        }
    }
}
