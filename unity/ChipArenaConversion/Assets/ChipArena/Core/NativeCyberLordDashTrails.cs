using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Data Dash trails a chained dash leaves behind (owner report 2026-10-12; Data Dash's cooldown went from .5 s to none,
    /// NativeCyberRules.DashCooldown, so only the .2 s dash and its mana gate it: a pilot can chain a dash about every .2 s while
    /// each trail lasts 1.2 s past its dash): the replicated kit keeps one dash (DashFrom, DashTo, TrailUntil), so the next dash
    /// overwrote the last one's trail mid-fade and it snapped away. Each frame every Cyber Lord's current dash is noted from
    /// its replicated window (keyed by its start, CyberLordView.At[Q]: a guest and a late joiner note it from the snapshot
    /// they hold); the newest entry follows that window (an interrupt's cut, the trail's end), and the entries a newer dash has
    /// superseded keep the values they last had and finish their own fade while the new trail grows. TrailsPerPilot per
    /// pilot in a fixed ring (no allocation per frame), sized for dash spam: a trail is drawn from its dash's start to its fade's
    /// end (about 1.6 s), so dashes .2 s apart keep about eight alive, and a ninth retires the pilot's oldest, the one
    /// furthest into its fade (the oldest fades first). Past the pilot's FullTrails newest, the older trails draw on the low
    /// tier (fewer blocks, no rising bits), so a spammed chain stays cheap. Presentation only: the authority's damaging trail is its own (see
    /// docs/CYBER-LORD.md for the rules side).
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        /// <summary>A remembered dash trail: the pilot, the dash's start, the trail's birth (the release), the dash's end, the
        /// trail's end, the pilot's yaw then and the path's two feet points.</summary>
        public struct DashTrail{public int Actor;public float At,Born,End,Until,Yaw;public Vector3 From,To;}
        public const int MaxDashTrails=48,TrailsPerPilot=8,FullTrails=3;
        private readonly DashTrail[] dashTrails=new DashTrail[MaxDashTrails];private int dashTrailCount;
        /// <summary>The remembered trails this frame (read by the checks).</summary>
        public int DashTrails=>dashTrailCount;public DashTrail DashTrailOf(int i)=>dashTrails[i];
        /// <summary>Whether trail `i` draws on the low tier: its pilot has FullTrails newer ones (or the frame is low already).</summary>
        public bool TrailLow(int i){int newer=0;var d=dashTrails[i];for(int j=0;j<dashTrailCount;j++)if(j!=i&&dashTrails[j].Actor==d.Actor&&dashTrails[j].At>d.At)newer++;return newer>=FullTrails;}
        private void RemoveTrail(int i){Array.Copy(dashTrails,i+1,dashTrails,i,dashTrailCount-i-1);dashTrailCount--;}
        /// <summary>Drops the trails that have faded (and, on a rewind such as the killcam's, the ones not begun yet).</summary>
        private void ExpireTrails(float time){for(int i=dashTrailCount-1;i>=0;i--){var d=dashTrails[i];if(time>=d.Until+.2f||time<d.At-.25f)RemoveTrail(i);}}
        /// <summary>Notes pilot `v`'s current dash: a new start adds an entry (the pilot's oldest goes past TrailsPerPilot, the
        /// ring's oldest when full); the entry of the current start follows the replicated window. A dash whose trail has faded
        /// (or, on a rewind, not begun) is not noted again.</summary>
        private void NoteTrail(CyberLordView v,float time)
        {
            float at=v.At[CyberLordSpec.Q];if(!(at>-90)||!(v.TrailUntil>-90)||time>=v.TrailUntil+.2f||time<at-.25f)return;
            int idx=-1,mine=0,oldest=-1;
            for(int i=0;i<dashTrailCount;i++){var d=dashTrails[i];if(d.Actor!=v.Id)continue;mine++;if(Math.Abs(d.At-at)<1e-4f)idx=i;if(oldest<0||d.At<dashTrails[oldest].At)oldest=i;}
            if(idx<0)
            {
                if(mine>=TrailsPerPilot)RemoveTrail(oldest);else if(dashTrailCount>=MaxDashTrails)RemoveTrail(0);
                idx=dashTrailCount++;dashTrails[idx]=new DashTrail{Actor=v.Id,At=at,Yaw=v.Yaw};
            }
            ref var e=ref dashTrails[idx];e.Born=v.Release[CyberLordSpec.Q];e.End=v.Until[CyberLordSpec.Q];e.Until=v.TrailUntil;e.From=V(v.DashFrom);e.To=V(v.DashTo);
        }
    }
}
