using System;
using System.Collections.Generic;
using System.Numerics;
using Row=ChipArena.UnityConversion.NativeCamp.Row;
using RowKind=ChipArena.UnityConversion.NativeCamp.RowKind;

namespace ChipArena.UnityConversion
{
    public static class NativeExpeditionHubPlan
    {
        public const float ExitReach=2.4f, HullRadius=1.17f, SkidLength=2.5f, MinimumSpacing=12;
        private const float Clearance=1;
        public static readonly Row[] Table=BuildTable();
        private static Row[] BuildTable()
        {
            var t=new List<Row>
            {
                new Row("camp-fountain",RowKind.Fountain,0,0,3.6f,.9f,3.6f),
                new Row("camp-canopy-n",RowKind.Canopy,0,3.4f,9.2f,.2f,2.4f,2.6f),new Row("camp-canopy-s",RowKind.Canopy,0,-3.4f,9.2f,.2f,2.4f,2.6f),
                new Row("camp-canopy-e",RowKind.Canopy,3.4f,0,2.4f,.2f,4.4f,2.6f),new Row("camp-canopy-w",RowKind.Canopy,-3.4f,0,2.4f,.2f,4.4f,2.6f),
                new Row("camp-canopy-post-0",RowKind.CanopyPost,4.4f,4.4f,.3f,2.6f,.3f),new Row("camp-canopy-post-1",RowKind.CanopyPost,-4.4f,4.4f,.3f,2.6f,.3f),
                new Row("camp-canopy-post-2",RowKind.CanopyPost,4.4f,-4.4f,.3f,2.6f,.3f),new Row("camp-canopy-post-3",RowKind.CanopyPost,-4.4f,-4.4f,.3f,2.6f,.3f),
                // the benches' long axis runs round the ring
                new Row("camp-bench-0",RowKind.Bench,0,3.4f,.9f,1.4f,.7f),new Row("camp-bench-1",RowKind.Bench,3.4f,0,.7f,1.4f,.9f),
                new Row("camp-bench-2",RowKind.Bench,0,-3.4f,.9f,1.4f,.7f),new Row("camp-bench-3",RowKind.Bench,-3.4f,0,.7f,1.4f,.9f),
                new Row("camp-kiosk",RowKind.Kiosk,16.4f,16.4f,2.6f,3.2f,2.6f),
                // The awning posts flank the chip machine 1.35 m out along its sides, 1.05 m clear of its body (owner 2026-11-21: "Move
                // to 1 m"; the preview's 1.1 m left 0.694 m).
                new Row("camp-awning-post-0",RowKind.AwningPost,14.85f,12.15f,.3f,3.4f,.3f),new Row("camp-awning-post-1",RowKind.AwningPost,12.15f,14.85f,.3f,3.4f,.3f),
                new Row("camp-infirmary",RowKind.Infirmary,-16.9f,-16.9f,3f,2.6f,3f),
                new Row("camp-lookout",RowKind.Lookout,13.5f,-13.5f,3f,9f,3f),
                new Row("camp-lookout-cabin",RowKind.LookoutCabin,13.5f,-13.5f,3.6f,2.6f,3.6f,9f),
                new Row("camp-searchlight",RowKind.Searchlight,13.5f,-13.5f,1.2f,1.2f,1.2f,11.6f),
                new Row("camp-sentry-n",RowKind.Sentry,-11.5f,23.8f,2f,5.2f,2f),new Row("camp-sentry-e",RowKind.Sentry,23.8f,11.5f,2f,5.2f,2f),
                new Row("camp-sentry-s",RowKind.Sentry,11.5f,-23.8f,2f,5.2f,2f),new Row("camp-sentry-w",RowKind.Sentry,-23.8f,-11.5f,2f,5.2f,2f),
                new Row("camp-radio",RowKind.Radio,13f,23.9f,4f,3.3f,1.8f),
                new Row("camp-supply",RowKind.Supply,-12.5f,-23.8f,3f,3.3f,2f),
                new Row("camp-generator",RowKind.Generator,24.3f,-12.5f,1f,1.4f,2f),
            };
            int k=0;
            foreach(int s in new[]{-1,1})foreach(int o in new[]{-1,1})
            {
                t.Add(new Row("camp-sandbag-"+(k++).ToString(System.Globalization.CultureInfo.InvariantCulture),RowKind.Sandbag,7.5f*o,20.5f*s,2.4f,1f,.6f));
                t.Add(new Row("camp-sandbag-"+(k++).ToString(System.Globalization.CultureInfo.InvariantCulture),RowKind.Sandbag,20.5f*s,7.5f*o,.6f,1f,2.4f));
            }
            return t.ToArray();
        }
        public static PveMachine[] Machines()
        {
            var o=new PveMachine[NativeCamp.Machines.Length];
            for(int i=0;i<o.Length;i++)
            {
                var (id,kind,x,z,yaw)=NativeCamp.Machines[i];float fx=(float)Math.Sin(yaw),fz=(float)Math.Cos(yaw);
                PvePoint P(float d)=>new PvePoint{x=x+fx*d,y=0,z=z+fz*d};
                o[i]=new PveMachine{m=i,id=id,kind=kind,x=x,y=0,z=z,yaw=yaw,front=P(.7f),dispense=P(1.3f),chipDrop=P(2.2f)};
            }
            return o;
        }
        private static readonly List<(Vector3 centre,float width,float depth,float yaw)> Reservations=Camp();

        public static IEnumerable<ContentBlock> Blocks()
        {
            foreach(var row in Table)
                yield return new ContentBlock{id=row.Id,role="static",material="foundation",
                    pos=new V3(row.X,row.Y0+row.Sy/2,row.Z),size=new V3(row.Sx,row.Sy,row.Sz),rotation=new ContentRotation()};
        }

        public static ExpeditionPoint RampFoot(ExpeditionPoint centre)
        {
            var forward=NativeExpeditionGeneratedWorld.CacheForward(centre.yaw);
            return new ExpeditionPoint{x=centre.x+forward.x*ExitReach,y=centre.y,z=centre.z+forward.z*ExitReach,yaw=centre.yaw};
        }

        private static List<(Vector3,float,float,float)> Camp()
        {
            var rows=new List<(Vector3,float,float,float)>();
            foreach(var row in Table)
                rows.Add((new Vector3(row.X,0,row.Z),row.Sx,row.Sz,0));
            foreach(var machine in Machines())
            {
                var body=NativeExpeditionLayout.MachineBlock(machine);
                rows.Add((new Vector3(body.pos.x,0,body.pos.z),body.size.x,body.size.z,machine.yaw));
                foreach(var point in new[]{machine.front,machine.dispense,machine.chipDrop})
                    rows.Add((new Vector3(point.x,0,point.z),.9f,.9f,machine.yaw));
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
            var foot=RampFoot(p);
            foreach(var row in Reservations)
            {
                float x=p.x-row.centre.X,z=p.z-row.centre.Z;
                float c=(float)Math.Cos(row.yaw),s=(float)Math.Sin(row.yaw),enter=0,leave=1;
                float px=foot.x-row.centre.X,pz=foot.z-row.centre.Z;
                float gapX=Math.Max(0,Math.Abs(px*c-pz*s)-row.width/2);
                float gapZ=Math.Max(0,Math.Abs(px*s+pz*c)-row.depth/2);
                const float footGap=NativeLocomotionRules.Radius+Clearance;
                if(gapX*gapX+gapZ*gapZ<footGap*footGap)return false;
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
