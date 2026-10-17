using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The footfalls the presentation draws (owner 2026-10-15: "can we add sounds for footsteps and zombies?"; Core NativeFootfalls):
    // each pilot's from its drawn gait's planted feet (CapturedAnimation.Player.FootContacts; my own pilot's rig is posed in first
    // person too, only not drawn), each zombie's from its gait's landed feet (NativeHordeGait.Landed), into one queue that
    // NativeAudio drains on its next frame (ConversionGame wires it). Each carries its planted foot, where NativeAudio probes the
    // surface (Core NativeFootSurface): a pilot's posed foot bone (the one leading the travel at the plant), a zombie's landed limb.
    // Presentation only: reads the replicated state and the drawn poses, never writes them; no allocation once warm.
    public sealed partial class ArenaPresentation
    {
        /// <summary>This frame's footfalls (pilots and zombies), drained by NativeAudio.</summary>
        public readonly NativeFootfallQueue Footfalls=new NativeFootfallQueue();
        private readonly Dictionary<int,NativeStepTracker> stepTrackers=new Dictionary<int,NativeStepTracker>();
        // One pilot after its pose: its plants (the gait's, or the distance stride where the route has none) and its landing.
        private void PilotFootfall(Actor a,Vector3 position,float time,bool posed)
        {
            if(Viewer)return;
            if(!a.Alive){stepTrackers.Remove(a.Id);return;}
            int contacts=posed&&rigs.TryGetValue(a.Id,out var rig)?rig.FootContacts:-1;
            stepTrackers.TryGetValue(a.Id,out var tracker);
            float speed=new Vector2(a.Velocity.x,a.Velocity.z).magnitude;
            int planted=tracker.Step(contacts,a.Grounded,a.Rolling(time),position.x,position.z,speed,a.Sprinting,time,out bool land,out float air);
            stepTrackers[a.Id]=tracker;
            if(planted==0&&!land)return;
            bool view=FirstPerson&&a.Id==LocalId;int foot=(planted&1)!=0?0:1;var at=PlantedFoot(a,position,foot,contacts>=0);
            Footfalls.Push(new NativeFootfall{Id=a.Id,Foot=foot,Land=land,Air=air,View=view,Sprint=a.Sprinting,X=position.x,Y=position.y,Z=position.z,Speed=speed,Time=time,FootX=at.x,FootZ=at.y,Scale=a.RigScale});
        }
        // The planted foot's ground point (x, z) for the surface probe: of the posed rig's two foot bones the one leading the travel
        // (a planting foot leads its step; standing, the gait's planted one), else the pilot's own position (no gait, a foot far off).
        private Vector2 PlantedFoot(Actor a,Vector3 position,int foot,bool gaited)
        {
            if(!gaited||!rigs.TryGetValue(a.Id,out var rig)||rig.Bones[CapturedRig.LFoot]==null||rig.Bones[CapturedRig.RFoot]==null)return new Vector2(position.x,position.z);
            Vector3 l=rig.Bones[CapturedRig.LFoot].position,r=rig.Bones[CapturedRig.RFoot].position;
            var f=NativeFootSurface.Leading(l.x,l.z,r.x,r.z,a.Velocity.x,a.Velocity.z,foot)==0?l:r;
            float dx=f.x-position.x,dz=f.z-position.z;
            return dx*dx+dz*dz<=NativeFootSurface.FootReach*NativeFootSurface.FootReach?new Vector2(f.x,f.z):new Vector2(position.x,position.z);
        }
        // One zombie after its gait: a footfall per limb that planted (upright the feet, on four limbs the hands' pulls).
        private void HordeFootfalls(HordeKindState s,NativeMinion m,int kind,float ground,float now)
        {
            var g=s.Gait;int landed=g.Landed;if(landed==0)return;bool crawl=g.Quad;float run=NativeHordeGait.RunBlend(kind,g.Speed),speed=g.Speed*s.Scale;
            for(int i=0;i<4;i++)
            {
                if((landed&(1<<i))==0||!NativeZombieSteps.Sounds(crawl,i))continue;var w=g.FootWorld[i]*s.Scale;
                Footfalls.Push(new NativeFootfall{Id=m.Id,Zombie=true,Kind=kind,Foot=i,Crawl=crawl,Drag=g.Dragged(i),Run=run,X=w.X,Y=ground,Z=w.Z,Speed=speed,Time=now,FootX=w.X,FootZ=w.Z,Scale=s.Scale});
            }
        }
    }
}
