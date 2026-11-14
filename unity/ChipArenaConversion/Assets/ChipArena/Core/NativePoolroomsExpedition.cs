using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativePoolroomsExpedition
    {
        public const string Objective="poolrooms-exit";
        public static ExpeditionAreaLayout Area(PoolroomsGenerated generated)
        {
            var l=generated.Layout;var entry=generated.Entry;var exit=generated.Exit;string id=NativePoolroomsGenerator.AreaId,door=id+":return";
            ExpeditionPoint Point(V3 p)=>new ExpeditionPoint{x=p.x,y=p.y,z=p.z};
            var anchor=new ExpeditionWorldAnchor{id=door,area=id,kind="door",building=id,x=entry.x,z=entry.z};
            var slots=generated.Players.Select(c=>Point(NativePoolroomsGenerator.Position(l,c))).ToArray();
            var cache=new ExpeditionWorldAnchor{id=id+":exit-cache",area=id,kind="chest",x=exit.x,z=exit.z};
            return new ExpeditionAreaLayout{id=id,role="instance",map=NativePoolroomsGenerator.InstanceMap,
                anchors=new[]{anchor,cache},doors=new[]{new ExpeditionDoorLayout{anchor=door,approach=Point(entry),interact=new ExpeditionVolume{x=entry.x,y=1,z=entry.z,sx=2,sy=2,sz=2},closure=new ExpeditionVolume{x=entry.x,y=PoolroomsLayout.Height/2,z=entry.z-PoolroomsLayout.Cell/2,sx=2,sy=PoolroomsLayout.Height,sz=PoolroomsLayout.Thickness},entries=slots,returns=Array.Empty<ExpeditionPoint>()}},
                interactions=new[]{new ExpeditionInteractionLayout{anchor=cache.id,x=exit.x,z=exit.z,range=2.5f}},gates=Array.Empty<ExpeditionGateLayout>(),sanctuary=Array.Empty<ExpeditionVolume>(),slots=Array.Empty<ExpeditionPoint>(),bossPoints=new[]{Point(exit)},
                machines=NativeInstanceMachines.Machines(id,generated.Machines,c=>NativePoolroomsGenerator.Position(l,c),PoolroomsLayout.Cell,PoolroomsLayout.Thickness),
                spawns=NativeBackroomsKinds.Anchors(generated)};
        }
        // Physical evidence comes only from the authority's director, never from a guest command.
        public static bool AtExit(ExpeditionSnapshot s,V3 position,V3 exit,bool visible)=>s!=null&&s.area==NativePoolroomsGenerator.AreaId&&s.phase==ExpeditionPhase.Instance&&!s.loading&&
            s.instances.Any(i=>i.id==s.area&&!i.cleared&&i.progress>=i.target)&&(position-exit).Length<=2&&visible;
    }
}
