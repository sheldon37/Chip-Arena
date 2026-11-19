using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    public static class NativeExpeditionHubPlan
    {
        public const float ExitReach=0, HullRadius=1.17f, SkidLength=2.5f, MinimumSpacing=12;
        private const float Clearance=1;
        private static readonly List<(Vector3 centre,float width,float depth,float yaw)> Reservations=Camp();

        private static List<(Vector3,float,float,float)> Camp()
        {
            var rows=new List<(Vector3,float,float,float)>();
            void Box(float x,float z,float width,float depth)=>rows.Add((new Vector3(x,0,z),width,depth,0));
            // The ring's bounding square includes its posts, canopy, seats and fountain.
            Box(0,0,9.2f,9.2f);
            Box(16.4f,16.4f,2.6f,2.6f);
            Box(14.6f,12.4f,.3f,.3f);
            Box(12.4f,14.6f,.3f,.3f);
            Box(-16.9f,-16.9f,3,3);
            Box(13.5f,-13.5f,3.6f,3.6f);
            Box(-11.5f,23.8f,2,2);Box(23.8f,11.5f,2,2);
            Box(11.5f,-23.8f,2,2);Box(-23.8f,-11.5f,2,2);
            Box(13,23.9f,4,1.8f);Box(-12.5f,-23.8f,3,2);Box(24.3f,-12.5f,1,2);
            foreach(int side in new[]{-1,1})
            foreach(int offset in new[]{-1,1})
            {
                Box(7.5f*offset,20.5f*side,2.4f,.6f);
                Box(20.5f*side,7.5f*offset,.6f,2.4f);
            }
            foreach(int sign in new[]{-1,1})
            {
                var machine=new Vector3(sign*13.5f,0,sign*13.5f);
                float yaw=(float)(sign==1?Math.PI*1.25:Math.PI*.25);
                var forward=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
                rows.Add((machine-forward*.25f,1.3f,.9f,yaw));
                foreach(float distance in new[]{.7f,1.3f,2.2f})
                    rows.Add((machine+forward*distance,.9f,.9f,yaw));
            }
            NativeReviveMachine.Spot(NativeExpeditionGeneratedWorld.MapId,out var at,out var turn);
            var frame=NativeReviveMachine.Frame(at,turn);
            foreach(var solid in NativeReviveMachine.Solids)
                rows.Add((Vector3.Transform(solid.centre,frame),solid.size.X,solid.size.Z,turn+solid.yaw));
            rows.Add((Vector3.Transform(NativeReviveMachineModels.MatAt,frame),NativeReviveMachineModels.MatSize,NativeReviveMachineModels.MatSize,turn));
            rows.Add((Vector3.Transform(NativeReviveMachine.UsePoint,frame),.9f,.9f,turn));
            return rows;
        }

        // Test the entire swept hull in each obstacle's local frame. Inflating its
        // rectangle also protects its corners conservatively, without sampling the skid.
        private static bool Clip(float origin,float delta,float half,ref float enter,ref float leave)
        {
            if(Math.Abs(delta)<.00001f)return Math.Abs(origin)<=half;
            float a=(-half-origin)/delta,b=(half-origin)/delta;
            enter=Math.Max(enter,Math.Min(a,b));leave=Math.Min(leave,Math.Max(a,b));
            return enter<=leave;
        }

        public static bool Clear(ExpeditionPoint p)
        {
            double radians=p.yaw*Math.PI/180;
            float dx=-SkidLength*(float)Math.Sin(radians),dz=-SkidLength*(float)Math.Cos(radians);
            const float edge=HullRadius+Clearance;
            if(Math.Min(Math.Abs(p.x),Math.Abs(p.z))<4.2f ||
               Math.Max(Math.Abs(p.x+dx),Math.Abs(p.z+dz))+edge>24.8f ||
               Math.Max(Math.Abs(p.x),Math.Abs(p.z))+edge>24.8f)return false;
            foreach(var row in Reservations)
            {
                float x=p.x-row.centre.X,z=p.z-row.centre.Z;
                float c=(float)Math.Cos(row.yaw),s=(float)Math.Sin(row.yaw),enter=0,leave=1;
                if(Clip(x*c-z*s,dx*c-dz*s,row.width/2+edge,ref enter,ref leave) &&
                   Clip(x*s+z*c,dx*s+dz*c,row.depth/2+edge,ref enter,ref leave))return false;
            }
            return true;
        }

        public static ExpeditionPoint[] LandingField(uint seed)
        {
            var rng=new NativeExpeditionDeployment.Roller(seed);
            var slots=new ExpeditionPoint[8];
            int first=rng.Index(4),direction=rng.Index(2)==0?1:3;
            int orientation=rng.Index(2);
            for(int i=0;i<slots.Length;i++)
            {
                int quadrant=(first+i*direction)%4;
                int sx=quadrant<2?1:-1,sz=quadrant==0||quadrant==3?1:-1;
                for(int draw=0;;draw++)
                {
                    // After unlucky random draws, visit every point on a coarser lattice.
                    // An otherwise valid run must never fail because a random retry limit expired.
                    bool fallback=draw>=512;
                    int side=i<4?22:8;
                    int cell=draw-512;
                    if(fallback&&cell>=side*side*5)
                        throw new InvalidOperationException("Camp reservations exclude the landing sector "+quadrant);
                    float origin=i<4?4.25f:19.5f;
                    float stride=i<4?.5f:.25f;
                    // The four active pilots own the inner ring. Overflow slots use
                    // the corners inside the same fence, outside the arrival ring.
                    float x=origin+(fallback?(cell%side)*stride:rng.Index(i<4?211:36)/20f);
                    float z=origin+(fallback?(cell/side%side)*stride:rng.Index(i<4?211:36)/20f);
                    // Alternating the longer coordinate leaves at least 4.25 + 8 m
                    // across adjacent sectors, independent of every previous draw.
                    if(i<4&&((quadrant+orientation)%2==0?x:z)<8)continue;
                    x*=sx;z*=sz;
                    float radius2=x*x+z*z;
                    if(i<4&&(radius2<121||radius2>225))continue;
                    float jitter=fallback?15*(cell/(side*side))-30:rng.Index(61)-30;
                    float yaw=(float)(Math.Atan2(-x,-z)*180/Math.PI)+jitter;
                    var p=new ExpeditionPoint{x=x,z=z,yaw=(yaw+360)%360};
                    if(!Clear(p))continue;
                    bool apart=true;
                    for(int j=0;j<i;j++)
                    {
                        float ax=x-slots[j].x,az=z-slots[j].z;
                        if(ax*ax+az*az<MinimumSpacing*MinimumSpacing){apart=false;break;}
                    }
                    if(apart){slots[i]=p;break;}
                }
            }
            return slots;
        }
    }
}
