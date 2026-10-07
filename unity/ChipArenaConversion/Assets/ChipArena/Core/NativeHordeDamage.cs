using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    public static class NativeHordeDamage
    {
        public static List<(int part,float damage)> Shares(float damage,V3 position,float yaw,bool crawl,V3? point,string kind,float radius,bool head,Func<double> next)
        {
            var result=new List<(int,float)>();if(kind=="blast")
            {V3 p=point??position;float fall=1-Math.Min(.85f,Math.Max(0,(p-position).Length/(radius+.4f))),total=damage*fall;result.Add((0,total*.6f));int[] pool=crawl?new[]{1,2,4,8,16}:p.y<.4f?new[]{128,256,32,64,128,256,8,16}:new[]{1,2,4,8,16,32,64,128,256};int count=next()<.5?1:2;for(int i=0;i<count;i++)result.Add((pool[Math.Min(pool.Length-1,(int)(next()*pool.Length))],total*.4f/count));return result;}
            int limb=0;if(head)limb=1;else if(point.HasValue){V3 d=point.Value-position;float a=yaw*(float)Math.PI/180,x=d.x*(float)Math.Cos(a)-d.z*(float)Math.Sin(a),z=d.x*(float)Math.Sin(a)+d.z*(float)Math.Cos(a);if(crawl){if(z>.3f)limb=1;else if(Math.Abs(x)>.25f)limb=x>=0?(z>0?2:8):(z>0?4:16);}else if(d.y>.4f)limb=1;else if(d.y<-.28f)limb=x>=0?(d.y<-.56f?128:32):(d.y<-.56f?256:64);else if(Math.Abs(x)>.235f)limb=x>=0?(d.y>.02f?2:8):(d.y>.02f?4:16);}
            result.Add((limb,damage));return result;
        }
        public static int Descendants(int limb)=>limb|(limb==2?8:limb==4?16:limb==32?128:limb==64?256:0);
    }
}
