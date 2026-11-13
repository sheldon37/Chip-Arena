using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The melee slams' floor debris (owner 2026-11-13: "We still seem to use voxel effects on slam attacks- Cubes show from
    /// collision; Can we improve this with higher quality poly or realistic effects?"). Every slam that reaches the floor
    /// throws chunks of the floor's own material through the destruction kit (Core NativeStructureFx.Slam, sized by Core
    /// NativeSlamDebris: a Greatsword's is big and heavy, a Fangs landing light):
    /// <list type="bullet">
    /// <item>the plunging stab (NativePlungeFx.Structures) and the jump attack's landing (NativeAirAttackFxPresentation), which
    /// call the kit themselves with the floor found here (<see cref="NativeStructureFxPresentation.FloorKind"/>);</item>
    /// <item>the Earthsplitter's fissure: a heavy burst where the blade lands, then a lighter one about every 1.25 m along the
    /// crack as its front passes (born at the front's own time), each chipping and cracking the floor;</item>
    /// <item>a kit move's landing (the Land events the melee kit raises: Skyfall and the rest), a Bulwark wall slam (chunks
    /// off the wall along its normal), and the techniques that strike the floor (Ground Slam's landing, Sunder's smash).</item>
    /// </list>
    /// Read from what every peer presents (the kit's snapshot and its clock), so host, guests, spectators and the killcam
    /// each draw it; the kit deduplicates by seed and time. Presentation only: no rule, timing, damage, collider or
    /// network field is touched, and no piece has a collider.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private readonly Dictionary<int,float> slamFissures=new Dictionary<int,float>(),slamTechniques=new Dictionary<int,float>();
        private int slamCursor,slamGeneration=-1;private float slamClock=-1;
        /// <summary>How far apart the fissure's bursts sit along it (m), and the most one fissure throws.</summary>
        public const float FissureStep=1.25f;public const int FissureBursts=8;
        private static uint SlamSeed(int id,float time,int k)=>((uint)id*73856093u)^(uint)BitConverter.SingleToInt32Bits(time)^((uint)k*19349663u)^0x51a3u;
        private void SlamDebris(NativeArena arena)
        {
            var s=kitSnapshot;var kit=structureFx?.Fx;if(arena==null||s?.Actors==null||kit==null)return;float t=kitClock;
            if(arena.Generation!=slamGeneration||t<slamClock-.25f){slamGeneration=arena.Generation;slamCursor=0;slamFissures.Clear();slamTechniques.Clear();}
            slamClock=t;var events=s.Events;
            if(events!=null)
            {
                int newest=0;for(int i=0;i<events.Length;i++)if(events[i].Id>newest)newest=events[i].Id;
                if(newest>0&&newest<slamCursor)slamCursor=0;
                for(int i=0;i<events.Length;i++)
                {
                    var e=events[i];if(e.Id<=slamCursor||t-e.Time>1)continue;
                    if(e.Kind==EventKind.Land&&!string.IsNullOrEmpty(e.Detail)&&!NativePlungeFields.Impact(e)&&e.Detail!="melee-air")
                    {
                        // A kit move's landing (the event's place is the pilot's centre).
                        Vector3 at=NativeArena.Vec(e.Position)-Vector3.up*.84f;
                        NativeStructureFxPresentation.Slam(arena,kit,at,at.y,NativeSlamDebris.Weapon(MeleeKitMotion.Spec(e.Detail))*.9f,Vector3.zero,SlamSeed(e.Source,e.Time,1),e.Time,true);
                    }
                    else if(e.Kind==EventKind.AbilityHit&&e.Detail=="bulwark-charge"&&e.WallSlam)
                    {
                        // The Bulwark's wall slam: chunks off the wall, thrown back along its normal.
                        Vector3 at=NativeArena.Vec(e.Position),n=NativeArena.Vec(e.Normal);
                        NativeStructureFxPresentation.FloorKind(arena,at,out var material,out int color,out float floor,at.y-1);
                        kit.Break(material,color,new V3(at.x,at.y,at.z),new V3(.5f,.5f,.5f),floor,SlamSeed(e.Source,e.Time,2),e.Time,false,new V3(n.x,0,n.z));
                    }
                }
                if(newest>slamCursor)slamCursor=newest;
            }
            for(int i=0;i<s.Actors.Length;i++)
            {
                var a=s.Actors[i];if(a==null)continue;var k=a.MeleeKit;
                if(k!=null&&k.FissureUntil>t&&k.FissureLength>0&&k.FissureSpeed>0&&(!slamFissures.TryGetValue(a.Id,out float seen)||seen!=k.FissureAt))
                {
                    slamFissures[a.Id]=k.FissureAt;Vector3 start=NativeArena.Vec(k.FissureStart),d=NativeArena.Vec(k.FissureDirection);d.y=0;d=d.sqrMagnitude>1e-6f?d.normalized:Vector3.forward;
                    float floor=start.y-.86f;int n=Mathf.Clamp(Mathf.CeilToInt(k.FissureLength/FissureStep),1,FissureBursts);
                    for(int j=0;j<n;j++)
                    {
                        float along=(j+.5f)*k.FissureLength/n;Vector3 at=new Vector3(start.x,floor,start.z)+d*along;
                        NativeStructureFxPresentation.Slam(arena,kit,at,floor,j==0?1:.5f,j==0?d:Vector3.zero,SlamSeed(a.Id,k.FissureAt,10+j),k.FissureAt+along/k.FissureSpeed,true);
                    }
                }
                var q=a.HasTechnique?a.Technique:null;if(q?.Kind==null)continue;
                bool ground=q.Kind=="ground-slam"&&q.Phase=="slam",smash=q.Kind=="sunder"&&q.Phase=="smash";
                if(!(ground||smash)||slamTechniques.TryGetValue(a.Id,out float pressed)&&pressed==q.At)continue;
                slamTechniques[a.Id]=q.At;Vector3 dir=NativeArena.Vec(q.Dir);dir.y=0;dir=dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.zero;Vector3 point=NativeArena.Vec(q.Point);
                float weapon=NativeSlamDebris.Weapon(a.SourceSpec);
                if(ground)NativeStructureFxPresentation.Slam(arena,kit,point,point.y,Mathf.Max(.8f,weapon),Vector3.zero,SlamSeed(a.Id,q.At,3),q.HitAt>-50?q.HitAt:t,true);
                else NativeStructureFxPresentation.Slam(arena,kit,point+dir,point.y,weapon*.9f,dir,SlamSeed(a.Id,q.At,4),t,true);
            }
        }
    }
}
