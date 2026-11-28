using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Central Station performing to its boss song, Station Chime of Doom (docs/ZONE-BOSS-MUSIC.md; presentation only: nothing here
    /// touches a hazard's lamp, warning, timing or collider, and nothing runs before the boss arrives). The song's beat comes from
    /// <see cref="NativeZoneBossSong.Evaluate"/> on the shared boss clock and rides on <see cref="NativeStationState.Music"/>.
    /// <list type="bullet">
    /// <item>The boss intro (the hold): the power sags at the arrival; the PA chime steps the platform lamps on from west to east,
    /// one group a chime, while the board's first row reads LAST CALL and lights on each chime; the signals' red lamps come on
    /// at the first slam; the lights cut where the song's bass cuts and flare back on its slam; the chime returns, the lights cut
    /// once more and the drop flares them as the fight begins (the board turns to ALL SERVICES CANCELLED with the boss).</item>
    /// <item>The fight: the house lights, lamps, troffers and signs follow the section's level and cast and pump on the song's
    /// pulse (downbeats and beat 3, at most 3 flashes a second, 2 in Reduced, none in Calm); the board brightens on the pulse
    /// and pages its ticker every two bars; the red signal lamps kick on the downbeats; the platforms' red edge strips chase
    /// along the platform, a quarter a beat; the stalled train's lit interior breathes with the bar; the stop-start section's
    /// cuts dim the house.</item>
    /// </list>
    /// Never touched: the danger lamps and beacons, the live rail's glints, the flood, the cabinets, the express and its
    /// headlight, the levers, the track diagram, the green signal aspect and the board's APPROACHING row (the express's warning).
    /// Pure functions of the part, its place and the beat.
    /// </summary>
    public sealed partial class NativeExpeditionStationKit
    {
        /// <summary>The intro's lamp groups (west to east across the shell, one group a chime) and a waiting lamp's glow.</summary>
        public const int LampGroups=8;public const float LampWaiting=.12f;
        private static int lampLook=int.MinValue;
        /// <summary>Whether a part follows the song: the house lights, lamps, troffers, signs, the board, the red signal lamps, the
        /// edge strips and the train's windows. Hazard parts never do.</summary>
        public static bool MusicReactive(in NativeStationPart p)
        {
            switch(p.Drive)
            {
                case DriveFlicker:case DriveFluor:case DriveBox:case DriveWindow:case DriveStrip:case DriveBoard:case DriveTicker:return true;
                case DriveSignal:return p.Tag==0;
                case DriveSteady:
                    if(lampLook==int.MinValue)lampLook=NativeBlockLooks.Id("ward-light");
                    return (p.Role==RoleLamp||p.Role==RoleSign||p.Role==RoleHigh)&&p.Gain>1.01f||p.Look==lampLook;
                default:return false;
            }
        }
        /// <summary>Whether the song's multiplier also scales the part's light level (the troffers' panels light by their level).</summary>
        public static bool MusicScalesLevel(in NativeStationPart p){if(lampLook==int.MinValue)lampLook=NativeBlockLooks.Id("ward-light");return p.Look==lampLook;}
        /// <summary>A part's light multiplier from the song at world x `x` (1 when nothing plays or the part does not follow it).
        /// `signal` is the state's signal aspect: the red lamps only kick while the signal shows red.</summary>
        public static float MusicGain(in NativeStationPart p,in NativeZoneBossBeat m,float x,byte signal=NativeStationState.SignalRed)
        {
            if(!m.On||!MusicReactive(p))return 1;bool calm=m.Mode==NativeKitFlash.Calm;
            switch(p.Drive)
            {
                case DriveSignal:return !m.Signals?.03f:signal==NativeStationState.SignalRed?.72f+.45f*m.Down:1;
                case DriveBoard:case DriveTicker:return (1+.32f*m.Pulse)*(1+.45f*m.Flare);
                case DriveStrip:{if(calm)return 1-.5f*m.Cut;int seg=(int)Math.Floor(Clamp01((x+NativeExpeditionStation.ShellX)/(2*NativeExpeditionStation.ShellX))*16)%4;float lit=m.BeatInBar>0&&seg==m.BeatInBar-1?(float)Math.Pow(1-m.Phase,m.Mode==NativeKitFlash.Reduced?.6:1.5):0;return (.5f+(m.Mode==NativeKitFlash.Reduced?.5f:.95f)*lit)*(1-.5f*m.Cut);}
                case DriveWindow:{float breath=.5f+.5f*(float)Math.Cos(2*Math.PI*m.BarPhase);return (1-.6f*m.Cut)*(calm?.94f+.06f*breath:.78f+.34f*breath)*(1+.4f*m.Flare);}
                default:return m.Light(.25f)*(p.Role==RoleLamp?LampGate(m,x):1);
            }
        }
        /// <summary>The intro's lamp gate at world x: a lamp waits dim until its group's chime (west first); 1 after the intro.</summary>
        public static float LampGate(in NativeZoneBossBeat m,float x)
        {
            if(!m.On||!m.Intro)return 1;int group=(int)Math.Min(LampGroups-1,Math.Floor(Clamp01((x+NativeExpeditionStation.ShellX)/(2*NativeExpeditionStation.ShellX))*LampGroups));
            return m.Steps*LampGroups>=group+1-1e-4?1:LampWaiting;
        }
        /// <summary>A practical light's multiplier and cast from the song (Shade's last step): the look's house lights, lamps, tubes,
        /// globes, signs and warm and cold fills follow the section and the pulse; the board brightens on the pulse; the red
        /// signals follow their lamps; the train's windows breathe. Beacons, cabinets, the flood, the express's lights and the green
        /// aspect are never touched.</summary>
        public static void MusicLight(byte kind,string look,float x,in NativeZoneBossBeat m,byte signal,ref float r,ref float g,ref float b)
        {
            if(!m.On)return;float k;
            switch(kind)
            {
                // The look's own: the departures' light follows the board, the box's red its lamps, the sodium pendants step on with
                // the lamps; the tunnel's green (the express's aspect) and the cafe's fire are never touched.
                case NativeExpeditionStationLights.KindLook:
                    if(look=="tunnel-w-green"||look=="cafe-fire")return;if(look=="departures"){kind=GlowBoard;goto case GlowBoard;}if(look=="signal-box"){kind=GlowBox;goto case GlowBox;}
                    k=m.Light(.2f)*(look!=null&&look.StartsWith("sodium",StringComparison.Ordinal)?LampGate(m,x):1);break;
                case GlowLamp:case GlowLampFlicker:k=m.Light(.25f)*LampGate(m,x);break;
                case GlowTube:case GlowTubeFlicker:case GlowGlobe:case GlowSign:case GlowWarm:case GlowCold:case GlowScreen:k=m.Light(.2f);break;
                case GlowBoard:k=(1+.32f*m.Pulse)*(1+.45f*m.Flare);break;
                case GlowStrip:k=m.Mode==NativeKitFlash.Calm?1-.5f*m.Cut:(.6f+.5f*m.Pulse)*(1-.5f*m.Cut);break;
                case GlowWindow:{float breath=.5f+.5f*(float)Math.Cos(2*Math.PI*m.BarPhase);k=(1-.6f*m.Cut)*(m.Mode==NativeKitFlash.Calm?.94f+.06f*breath:.78f+.34f*breath);break;}
                case GlowSignal:if(signal!=NativeStationState.SignalRed)return;k=!m.Signals?0:.72f+.45f*m.Down;break;
                case GlowBox:k=.8f+.4f*m.Down;break;
                default:return;
            }
            r*=k;g*=k;b*=k;
            // The section's cast on the house light only (the signals keep their red, the board its amber).
            if(kind==GlowSignal||kind==GlowBoard||kind==GlowBox)return;float warm=Math.Max(0,m.Mood.Tint),cold=Math.Max(0,-m.Mood.Tint);
            r*=1+.22f*warm-.18f*cold;g*=1-.06f*warm-.04f*cold;b*=1-.25f*warm+.3f*cold;
        }
        private static float Clamp01(double v)=>(float)(v<0?0:v>1?1:v);
    }
}
