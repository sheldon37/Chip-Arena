#if ARENA_TESTS
using System;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class HordeHitChecks
    {
        public static void Run()
        {
            int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Horde hit metadata: "+why);}
            var points=new[]{(0,new V3(0,0,0)),(1,new V3(0,.6f,0)),(2,new V3(.3f,.1f,0)),(4,new V3(-.3f,.1f,0)),(8,new V3(.3f,-.1f,0)),(16,new V3(-.3f,-.1f,0)),(32,new V3(.1f,-.4f,0)),(64,new V3(-.1f,-.4f,0)),(128,new V3(.1f,-.7f,0)),(256,new V3(-.1f,-.7f,0))};
            foreach(var pair in points)
            {
                int rolls=0;var shares=NativeHordeDamage.Shares(300,new V3(),0,false,pair.Item2,"projectile",3,false,()=>{rolls++;return .2;});
                var data=new NativeHordeHit(300);foreach(var share in shares)data.Share(share.part,share.damage);
                Check(rolls==0&&shares.Count==1&&shares[0].damage==300&&data.Part==pair.Item1&&data.HasPart&&data.Head==(pair.Item1==1),"direct part and unchanged shares");
                var hit=new ArenaEvent{Kind=EventKind.Hit,Target=10001,Amount=10,HasHordeHit=true,Raw=data.Raw,Head=data.Head,HasPart=data.HasPart,Part=data.Part};
                var json=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var copy=JsonSerializer.Deserialize<ArenaEvent>(JsonSerializer.Serialize(hit,json),json);
                Check(NativeHordeDamage.ValidHit(copy)&&copy.Raw==300&&copy.Amount==10&&copy.Part==pair.Item1&&copy.Head==hit.Head,"overkill and anatomy survive serialization without altering actual damage");
            }
            int draws=0;var blast=NativeHordeDamage.Shares(100,new V3(0,1,0),0,false,new V3(0,1,0),"blast",3,false,()=>{draws++;return 0;});var b=new NativeHordeHit(100);foreach(var share in blast)b.Share(share.part,share.damage);
            Check(draws==2&&b.Head&&b.HasPart&&b.Part==0&&blast.Sum(s=>s.damage)==100,"blast head share, largest torso share, no additional random draws");
            var tie=new NativeHordeHit(10);tie.Share(2,5);tie.Share(4,5);Check(tie.Part==2,"stable first-share tie");tie.Share(1,0);Check(!tie.Head,"zero share is not a head impact");
            Check(NativeHordeDamage.Part(new V3(),0,true,new V3(0,0,.4f),false)==1,"crawl head");
            Check(NativeHordeDamage.Part(new V3(),0,false,null,true)==1,"explicit head");
            Check(NativeHordeDamage.ValidHit(new ArenaEvent())&&!new ArenaEvent().HasHordeHit,"old wire defaults retain fallback");
            var unknown=new ArenaEvent{Kind=EventKind.Hit,Target=10001,HasHordeHit=true,Raw=20};Check(NativeHordeDamage.ValidHit(unknown)&&!unknown.HasPart,"unknown fallback blast part differs from known torso");
            foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-1}){var e=unknown;e.Raw=bad;Check(!NativeHordeDamage.ValidHit(e),"invalid raw");}
            foreach(int bad in new[]{-1,3,511,512}){var e=unknown;e.HasPart=true;e.Part=bad;Check(!NativeHordeDamage.ValidHit(e),"invalid limb bit");}
            var pilot=unknown;pilot.Target=1;Check(!NativeHordeDamage.ValidHit(pilot),"horde payload cannot label a pilot");
            Console.WriteLine("PASS horde hit authority metadata: "+count+" checks");
        }
    }
}
#endif
