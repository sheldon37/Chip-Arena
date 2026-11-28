using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The City Ironworks performing to its boss song, Hydraulic Hell (docs/ZONE-BOSS-MUSIC.md; presentation only: nothing here
    /// touches a hazard's motion, lamp, telegraph, timing or collider, and nothing runs before the boss arrives). The song's beat
    /// comes from <see cref="NativeZoneBossSong.Evaluate"/> on the shared boss clock and is passed in.
    /// <list type="bullet">
    /// <item>The boss intro (the hold): the lights sag at the arrival; the furnace mouths stoke up bar by bar through the song's
    /// ignition and flare on its downbeats and slams, the coke standpipes puff steam on them; the drop lands as the fight begins:
    /// a shower of sparks from the roof over the casting floor and the house lights and furnaces surging.</item>
    /// <item>The fight: the furnaces and the steady glows breathe with the bar and flare on the pulse (every beat, the downbeat
    /// strongest; at most 3 flashes a second, 2 in Reduced, none in Calm); the work lights follow the section's level; sparks and
    /// steam follow the section; the parked ladle crane rocks on its hook with the bar.</item>
    /// </list>
    /// Every moving machine in the works is a hazard (the drop hammer, shear, pusher, slab, slag pot and the ladle's pour), so none
    /// pumps to the music: hazard motion stays on the hazard clock so its warnings keep matching its damage. Locking hazards to the
    /// beat is a rules change for Astra (docs/ZONE-BOSS-MUSIC.md, prompt 18).
    /// </summary>
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The furnaces' and molten glow's multiplier: the section's heat, the intro's stoking (bar by bar), the bar's
        /// breath, the pulse and the drop's flare (1 when nothing plays).</summary>
        public static float MusicHeat(in NativeZoneBossBeat m)
        {
            if(!m.On)return 1;bool calm=m.Mode==NativeKitFlash.Calm;float breath=calm?.97f+.03f*(float)Math.Cos(2*Math.PI*m.BarPhase):.9f+.1f*(float)Math.Cos(2*Math.PI*m.BarPhase);
            float stoke=m.Intro?.6f+.4f*m.Steps:1;return m.Mood.Heat*stoke*breath*(1+.45f*m.Pulse)*(1+.6f*m.Flare)*(1-.35f*m.Cut);
        }
        /// <summary>The work lights' multiplier (sodium, mercury, the office tube): the section's level, the cut, the flare and a
        /// small share of the pulse.</summary>
        public static float MusicLamp(in NativeZoneBossBeat m)=>m.On?m.Light(.18f):1;
        /// <summary>Whether a part follows the song: the lamps' and furnaces' drives and the steady lamp glows. Hazard drives (beacons,
        /// warning lamps, hazard glows, tapholes) never do.</summary>
        public static bool MusicReactive(in NativeIronPart p)
        {
            switch(p.Drive)
            {
                case DriveFlicker:case DriveSodium:case DriveMercury:case DriveFire:case DriveSeam:return true;
                case DriveSteady:return p.Role==RoleLamp&&p.Gain>1.01f&&p.Hazard<0;
                default:return false;
            }
        }
        /// <summary>A part's multiplier from the song (1 when nothing plays or the part does not follow it).</summary>
        public static float MusicGain(in NativeIronPart p,in NativeZoneBossBeat m)
        {
            if(!m.On||!MusicReactive(p))return 1;
            return p.Drive==DriveFire||p.Drive==DriveSeam?MusicHeat(m):MusicLamp(m);
        }
        /// <summary>A practical light's multiplier from the song by its glow kind: furnace mouths, coke doors, converters and the
        /// hearth take the heat, the work lights the lamp level; hazard lights (tapholes, runners, the pour, molds, the reheat
        /// door, the slab, the billet, slag, beacons) are never touched.</summary>
        public static float MusicLight(byte kind,in NativeZoneBossBeat m)
        {
            if(!m.On)return 1;
            switch(kind)
            {
                case GlowFurnace:case GlowCokeDoor:case GlowConverter:case GlowHearth:return MusicHeat(m);
                case GlowSodium:case GlowMercury:case GlowOffice:case GlowLamp:return MusicLamp(m);
                default:return 1;
            }
        }
        /// <summary>The steady light field's level (the furnaces' glow and the baked work lights) from the song: half lamp, half heat,
        /// gentler (the field lights whole rooms).</summary>
        public static float MusicField(in NativeZoneBossBeat m)=>m.On?.5f*m.Light(.08f)+.5f*(1+(MusicHeat(m)-1)*.6f):1;
        /// <summary>The parked ladle crane's extra rock on its hook (deg) with the bar: one swing a bar, deeper in the hard sections;
        /// none unless parked (it never pours in the boss encounter) and none in Calm.</summary>
        public static double MusicSway(in NativeZoneBossBeat m,bool parked)=>!m.On||!parked||m.Mode==NativeKitFlash.Calm?0:(1.2+1.6*m.Mood.Pulse)*Math.Sin(2*Math.PI*m.BarPhase);
    }
}
