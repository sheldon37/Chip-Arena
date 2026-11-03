using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeBackroomsExpedition
    {
        public const string Objective="level0-exit";
        public static ExpeditionAreaLayout Area(BackroomsGenerated generated)
        {
            var l=generated.Layout;var entry=generated.Entry;var exit=generated.Exit;string id=NativeBackroomsGenerator.AreaId,door=id+":return";
            ExpeditionPoint Point(V3 p)=>new ExpeditionPoint{x=p.x,y=p.y,z=p.z};
            var anchor=new ExpeditionWorldAnchor{id=door,area=id,kind="door",building=id,x=entry.x,z=entry.z};
            var slots=generated.Players.Select(c=>Point(NativeBackroomsGenerator.Position(l,c))).ToArray();
            var cache=new ExpeditionWorldAnchor{id=id+":exit-cache",area=id,kind="chest",x=exit.x,z=exit.z};
            return new ExpeditionAreaLayout{id=id,role="instance",map=NativeBackroomsGenerator.InstanceMap,
                anchors=new[]{anchor,cache},doors=new[]{new ExpeditionDoorLayout{anchor=door,approach=Point(entry),interact=new ExpeditionVolume{x=entry.x,y=1,z=entry.z,sx=2,sy=2,sz=2},closure=new ExpeditionVolume{x=entry.x,y=BackroomsLayout.Height/2,z=entry.z-BackroomsLayout.Cell/2,sx=BackroomsLayout.Doorway,sy=BackroomsLayout.Height,sz=BackroomsLayout.Thickness},entries=slots,returns=Array.Empty<ExpeditionPoint>()}},
                interactions=new[]{new ExpeditionInteractionLayout{anchor=cache.id,x=exit.x,z=exit.z,range=2.5f}},gates=Array.Empty<ExpeditionGateLayout>(),sanctuary=Array.Empty<ExpeditionVolume>(),slots=Array.Empty<ExpeditionPoint>(),bossPoints=new[]{Point(exit)},
                spawns=NativeBackroomsKinds.Anchors(generated)};
        }
        // Physical evidence comes only from the authority's director, never from a guest command.
        public static bool AtExit(ExpeditionSnapshot s,V3 position,V3 exit,bool visible)=>s!=null&&s.area==NativeBackroomsGenerator.AreaId&&s.phase==ExpeditionPhase.Instance&&!s.loading&&
            s.instances.Any(i=>i.id==s.area&&!i.cleared&&i.progress>=i.target)&&(position-exit).Length<=2&&visible;
    }
    public sealed partial class ExpeditionDirector
    {
        public bool TryBackroomsExit()
        {
            var s=Session.Run.Snapshot();bool pool=s.area==NativePoolroomsGenerator.AreaId;if(s.area!=NativeBackroomsGenerator.AreaId&&!pool)return false;string objective=pool?NativePoolroomsExpedition.Objective:NativeBackroomsExpedition.Objective;var exit=Layout.Area(s.area).bossPoints[0].Position;
            if(!s.members.Any(m=>m.connected&&m.alive&&!m.extracted&&position(m.account) is V3 p&&(pool?NativePoolroomsExpedition.AtExit(s,p,exit,visible(p,exit)):NativeBackroomsExpedition.AtExit(s,p,exit,visible(p,exit)))))return false;
            var instance=s.instances.Single(i=>i.id==s.area);
            if(instance.bosses.Length==0&&!Session.Run.StartBosses(new[]{objective}))return false;
            // Reuse the existing once-only encounter reward/cleared ledger; no boss entity is spawned for Level 0.
            return BossDefeated(objective,exit);
        }
    }
}
