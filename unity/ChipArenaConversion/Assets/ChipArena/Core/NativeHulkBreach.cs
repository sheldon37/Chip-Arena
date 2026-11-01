using System;
namespace ChipArena.UnityConversion
{
    /// <summary>Allocation-free clearance/progress and breach clocks shared by the host and managed checks.</summary>
    public static class NativeHulkBreach
    {
        public const float StuckSeconds=1.5f,Reach=1.35f;
        public struct Progress
        {
            public V3 anchor;public float since;public bool tracking;
            public bool Step(V3 position,float time,bool blocked)
            {
                var d=position-anchor;d.y=0;
                if(!blocked){tracking=false;return false;}
                if(!tracking||d.Length>.15f){tracking=true;anchor=position;since=time;return false;}
                return time-since>=StuckSeconds;
            }
        }
        // Ward's authored interior wall blocks only. Fail closed for unknown shapes/rotations, shell,
        // floor, ceiling, props, panels and hazard objects. Outer walls never inherit eligibility.
        public static bool Eligible(ContentMap map,ContentBlock b)
        {
            if(map?.id!="siren"||map.bounds==null||b==null||b.role!="static"||b.dynamic||b.id==null||b.id.IndexOf("-wall-",StringComparison.Ordinal)<0||!string.IsNullOrEmpty(b.segment))return false;
            if(b.rotation!=null&&(Math.Abs(b.rotation.x)>.0001f||Math.Abs(b.rotation.y)>.0001f||Math.Abs(b.rotation.z)>.0001f||Math.Abs(b.rotation.w-1)>.0001f))return false;
            if(b.size.y<=.5f||Math.Min(b.size.x,b.size.z)>1||b.size.y<=Math.Min(b.size.x,b.size.z))return false;
            var h=b.size*.5f;var p=b.pos;var bounds=map.bounds;
            if(p.x-h.x<bounds.minX||p.x+h.x>bounds.maxX||p.z-h.z<bounds.minZ||p.z+h.z>bounds.maxZ)return false;
            foreach(var hazard in map.hazards??Array.Empty<MapHazard>())if(b.id==hazard.id||b.id.StartsWith(hazard.id,StringComparison.Ordinal)&&b.id.Length>hazard.id.Length&&b.id[hazard.id.Length]=='-')return false;
            return true;
        }
        // Swept Hulk-sized clearance box. Conservative at rounded capsule corners; no point-ray
        // shortcut through a narrow doorway. Ignore walls behind us or on a different floor.
        public static bool Blocks(ContentBlock wall,V3 from,V3 toward,float radius,float height,out float distance,out V3 point,float reach=Reach)
        {
            var d=toward-from;d.y=0;float length=d.Length;distance=0;point=from;
            if(length<.001f)return false;d=d/length;float lo=0,hi=Math.Min(length,reach);
            var h=wall.size*.5f;var p=wall.pos;
            if(from.y+height*.5f<=p.y-h.y+.03f||from.y-height*.5f>=p.y+h.y-.03f)return false;
            if(V3.Dot(p-from,d)+Math.Abs(d.x)*h.x+Math.Abs(d.z)*h.z<0)return false;
            if(!Slab(from.x,d.x,p.x-h.x-radius,p.x+h.x+radius,ref lo,ref hi)||!Slab(from.z,d.z,p.z-h.z-radius,p.z+h.z+radius,ref lo,ref hi))return false;
            distance=lo;var at=from+d*lo;
            point=new V3(MathUtil.Clamp(at.x,p.x-h.x,p.x+h.x),MathUtil.Clamp(at.y,p.y-h.y,p.y+h.y),MathUtil.Clamp(at.z,p.z-h.z,p.z+h.z));return true;
        }
        public static bool Shorter(float route,float direct,float floorDifference)=>!float.IsNaN(route)&&!float.IsNaN(direct)&&direct>0&&Math.Abs(floorDifference)<.6f&&route>direct+1;
        public static V3 Exit(ContentBlock wall,V3 from,V3 toward,float radius)
        {
            var d=toward-from;d.y=0;d=d.Normalized;bool x=wall.size.x<wall.size.z;
            float component=x?d.x:d.z;if(Math.Abs(component)<.001f)return from;
            float face=(x?wall.pos.x:wall.pos.z)+Math.Sign(component)*((x?wall.size.x:wall.size.z)*.5f+radius+.08f);
            return from+d*((face-(x?from.x:from.z))/component);
        }
        private static bool Slab(float origin,float delta,float min,float max,ref float lo,ref float hi)
        {
            if(Math.Abs(delta)<1e-6f)return origin>=min&&origin<=max;
            float a=(min-origin)/delta,b=(max-origin)/delta;if(a>b){float swap=a;a=b;b=swap;}
            lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi;
        }
        public static NativeBreachRecord Begin(ContentBlock wall,int actor,V3 point,V3 facing,float now)
        {
            facing.y=0;float at=now+NativeBreach.Knock;
            return new NativeBreachRecord{entry=-1,block=wall.id,actor=actor,point=point,normal=facing.Normalized,holeSize=wall.size,breakAt=at,restoreAt=NativeBreach.DefaultRestoreAt(at)};
        }
        public static bool Solid(NativeBreachRecord b,float time)=>time<b.breakAt||time>=NativeBreach.SolidAt(b.restoreAt);
        public static void HoldOpen(NativeBreachRecord b,float time,bool occupied)
        {if(occupied&&time>=b.breakAt&&time>=b.restoreAt)b.restoreAt=Math.Max(b.restoreAt+.5f,time+.5f);}
        public static bool ValidRecords(NativeBreachRecord[] records,ContentMap map)
        {
            if(records==null)return true;if(records.Length>64)return false;
            for(int i=0;i<records.Length;i++)
            {
                var b=records[i];if(b==null||!b.Valid())return false;
                if(b.Dynamic)
                {
                    ContentBlock wall=null;foreach(var w in map.blocks)if(w.id==b.block){wall=w;break;}
                    if(!Eligible(map,wall)||(wall.size-b.holeSize).Length>.001f)return false;
                    var d=b.point-wall.pos;var h=wall.size*.5f;
                    if(Math.Abs(d.x)>h.x+.01f||Math.Abs(d.y)>h.y+.01f||Math.Abs(d.z)>h.z+.01f)return false;
                }
                for(int j=0;j<i;j++)if(b.Dynamic?records[j].Dynamic&&records[j].block==b.block:!records[j].Dynamic&&records[j].entry==b.entry)return false;
            }
            return true;
        }
    }
}
