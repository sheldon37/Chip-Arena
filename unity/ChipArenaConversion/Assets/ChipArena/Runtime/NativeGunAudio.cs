using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The guns' recorded layers (Core NativeGunSounds, docs/GUN-SOUNDS.md) on the effects voices: a Shot event of the pistol, rifle or
    // machine gun plays its close and distant layers by range and its tail (the machine gun's once its stream stops), the rifle's
    // bolt and the shell casing; a reload plays its parts on the reload clip's span. Heard by everyone from the events and replicated
    // reload times; the reloading pilot's own first-person view takes its clip's warp. Without the gun's clips EventSounds keeps
    // today's route (GunEvent returns false). Presentation only: no rule, timing or wire field.
    public sealed partial class NativeAudio
    {
        /// <summary>The local view is in first person (ConversionGame sets it each frame): its own reload and bolt take the first-person
        /// clips' clocks.</summary>
        public bool FirstPerson;
        private struct GunStream{public float Last,TailAt;public Vector3 At;public bool Released;}
        private readonly Dictionary<int,GunStream> gunStreams=new Dictionary<int,GunStream>();
        private readonly System.Random gunRandom=new System.Random(0x9a5);
        private int gunGeneration=-2;

        /// <summary>A Shot, ReloadStart or ReloadEnd event of a gun: true when the gun's recorded layers play it (its routed plan is then
        /// skipped); false keeps today's sound.</summary>
        private bool GunEvent(NativeArena arena,ArenaEvent e,NativePilot source,Vector3 at,float age,bool mine)
        {
            string spec=source?.Seat.spec;if(!NativeGunSounds.Gun(spec))return false;
            if(e.Kind==EventKind.ReloadStart||e.Kind==EventKind.ReloadEnd)return GunReloads(spec);
            if(e.Kind!=EventKind.Shot||!bank.Has(NativeGunSounds.Near(spec)))return false;
            GunShot(arena,source,spec,e.Time,at,age,mine);return true;
        }
        private bool GunReloads(string spec){var parts=NativeGunSounds.Reload(spec);return parts.Length>0&&bank.Has(parts[0].Key);}

        private void GunShot(NativeArena arena,NativePilot p,string spec,float time,Vector3 at,float age,bool mine)
        {
            float d=(at-listener).magnitude,delay=Math.Max(0,NativeGunSounds.Delay(d)-age);var priority=NativeSoundPlan.Priority(NativeSoundPriority.Normal,mine);
            float near=NativeGunSounds.NearWeight(d),far=NativeGunSounds.FarWeight(d);
            if(near>=NativeGunSounds.Audible)BankPlay(NativeGunSounds.Near(spec),at,NativeGunSounds.Gain*near,delay,null,mine,priority);
            if(far>=NativeGunSounds.Audible&&bank.Has(NativeGunSounds.Far(spec)))BankPlay(NativeGunSounds.Far(spec),at,NativeGunSounds.Gain*far,delay,null,mine,priority);
            int id=p.Seat.id;gunStreams.TryGetValue(id,out var s);
            if(NativeGunSounds.Stream(spec)){s.Last=time;s.At=at;s.Released=false;}
            else if(time-s.TailAt>=NativeGunSounds.TailGap||time<s.TailAt){s.TailAt=time;BankPlay(NativeGunSounds.Tail(spec),at,NativeGunSounds.Gain*NativeGunSounds.TailWeight(d),delay,null,mine,NativeSoundPriority.Low);}
            gunStreams[id]=s;
            bool view=mine&&FirstPerson;float interval=NativeTacticalTiming.Interval(NativeGunSounds.Weapon(spec),p.Pose.LastShot,p.Pose.ShootReady);
            Vector3 body=p.Body!=null?p.Body.position:at;float near2=(body-listener).magnitude;if(!mine&&near2>NativeGunSounds.FoleyReach)return;
            float foley=mine?1:NativeGunSounds.Foley(near2);
            if(spec=="rifle")BankPlay(NativeGunSounds.Bolt,body,NativeGunSounds.BoltGain*foley,Math.Max(0,NativeGunSounds.BoltAge(NativeGunSounds.BoltAt,interval,view)-age),null,mine,priority);
            pilotSurfaces.TryGetValue(id,out var floor);string casing=NativeGunSounds.Casing(spec,floor.Current??NativeFootsteps.Surface(arena.MapId));
            if(casing!=null)BankPlay(casing,body,NativeGunSounds.CasingGain*foley,Math.Max(0,NativeGunSounds.CasingAge(spec,interval,view,(float)gunRandom.NextDouble())-age),null,mine,mine?NativeSoundPriority.Normal:NativeSoundPriority.Low);
        }

        /// <summary>Once a frame: each gun pilot's reload parts as its reload crosses them, and a stopped stream's tail.</summary>
        private void GunFrame(NativeArena arena,NativePilot me,bool menu)
        {
            if(arena.Generation!=gunGeneration){gunGeneration=arena.Generation;gunStreams.Clear();}
            if(menu||bank.Count==0)return;float t=arena.Time;
            foreach(var p in arena.Pilots)
            {
                string spec=p.Seat.spec;if(!NativeGunSounds.Gun(spec)||p.Body==null)continue;bool mine=p==me;int id=p.Seat.id;Vector3 pos=p.Body.position;
                if(p.ReloadEnd>p.ReloadAt&&t>=p.ReloadAt&&t<p.ReloadEnd&&GunReloads(spec))
                {
                    float d=(pos-listener).magnitude;var parts=NativeGunSounds.Reload(spec);
                    if(mine||d<=NativeGunSounds.FoleyReach)
                        for(int k=0;k<parts.Length;k++)
                        {
                            float at=p.ReloadAt+NativeGunSounds.ReloadShare(spec,parts[k].At,mine&&FirstPerson)*(p.ReloadEnd-p.ReloadAt);
                            if(t>=at&&t-at<.15f&&Once(id+":gun-reload:"+k,p.ReloadAt))BankPlay(parts[k].Key,pos,NativeGunSounds.ReloadGain*(mine?1:NativeGunSounds.Foley(d)),0,null,mine);
                        }
                }
                if(!NativeGunSounds.Stream(spec)||!gunStreams.TryGetValue(id,out var s)||s.Released)continue;
                float after=NativeGunSounds.ReleaseAfter(GunRules.Get(NativeGunSounds.Weapon(spec)).Interval);if(t<s.Last){s.Released=true;gunStreams[id]=s;continue;}
                if(t-s.Last<after)continue;s.Released=true;gunStreams[id]=s;if(t-s.Last>after+.25f)continue;
                // The release starts where it would be had it begun with the last shot (after its travel time).
                float dist=(s.At-listener).magnitude,since=t-(s.Last+NativeGunSounds.Delay(dist));
                BankPlay(NativeGunSounds.Tail(spec),s.At,NativeGunSounds.Gain*NativeGunSounds.TailWeight(dist),Math.Max(0,-since),null,mine,NativeSoundPriority.Low,1,Math.Max(0,since));
            }
        }
    }
}
