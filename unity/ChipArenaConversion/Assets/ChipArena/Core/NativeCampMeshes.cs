using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>Belvedere Camp's mesh data (Runtime NativeCampDraw uploads it): a bank part appended into a combined mesh at a place.</summary>
    public static class NativeCampMeshes
    {
        /// <summary>Appends a bank part (its mesh through the part's own place, then `place`, 3 × 4 rows) to `into`: positions and normals
        /// turned, the solid and glow sub-meshes by their material (the bank's "stage" and "stage-glow").</summary>
        public static void Append(NativePackBank.Bank bank,NativePackBank.Part part,float[] place,NativeMeridianMesh into)
        {
            if(bank==null||into==null||part.Mesh<0||part.Mesh>=bank.Meshes.Count)return;var m=bank.Meshes[part.Mesh];var q=part.Place;var a=place;
            int n=m.Positions.Length,at=into.Reserve(n);
            for(int i=0;i<n;i++)
            {
                var p=m.Positions[i];
                // the part's own place (System.Numerics row vectors), then the piece's (rows)
                float x=p.X*q.M11+p.Y*q.M21+p.Z*q.M31+q.M41,y=p.X*q.M12+p.Y*q.M22+p.Z*q.M32+q.M42,z=p.X*q.M13+p.Y*q.M23+p.Z*q.M33+q.M43;
                float wx=a[0]*x+a[1]*y+a[2]*z+a[3],wy=a[4]*x+a[5]*y+a[6]*z+a[7],wz=a[8]*x+a[9]*y+a[10]*z+a[11];
                int v=(at+i)*3;into.P[v]=wx;into.P[v+1]=wy;into.P[v+2]=wz;into.Bound(wx,wy,wz);
                var nn=m.Normals!=null&&i<m.Normals.Length?m.Normals[i]:new System.Numerics.Vector3(0,1,0);
                float nx=nn.X*q.M11+nn.Y*q.M21+nn.Z*q.M31,ny=nn.X*q.M12+nn.Y*q.M22+nn.Z*q.M32,nz=nn.X*q.M13+nn.Y*q.M23+nn.Z*q.M33;
                float ox=a[0]*nx+a[1]*ny+a[2]*nz,oy=a[4]*nx+a[5]*ny+a[6]*nz,oz=a[8]*nx+a[9]*ny+a[10]*nz,l=(float)Math.Sqrt(ox*ox+oy*oy+oz*oz);if(l<1e-6f)l=1;
                into.N[v]=ox/l;into.N[v+1]=oy/l;into.N[v+2]=oz/l;
                int u=(at+i)*2;var t0=m.Uv0!=null&&i<m.Uv0.Length?m.Uv0[i]:default;var t1=m.Uv1!=null&&i<m.Uv1.Length?m.Uv1[i]:default;
                into.Uv0[u]=t0.X;into.Uv0[u+1]=t0.Y;into.Uv1[u]=t1.X;into.Uv1[u+1]=t1.Y;
                int c=(at+i)*4;if(m.Colors!=null&&m.Colors.Length>=(i+1)*4){into.C[c]=m.Colors[i*4];into.C[c+1]=m.Colors[i*4+1];into.C[c+2]=m.Colors[i*4+2];into.C[c+3]=m.Colors[i*4+3];}else{into.C[c]=into.C[c+1]=into.C[c+2]=into.C[c+3]=255;}
            }
            foreach(var s in m.Submeshes)
            {
                bool glow=s.Material>=0&&s.Material<bank.Materials.Count&&bank.Materials[s.Material].Name!=null&&bank.Materials[s.Material].Name.EndsWith("glow",StringComparison.Ordinal);
                var list=glow?into.Glow:into.Solid;foreach(int i in s.Indices)list.Add(at+i);
            }
        }
    }

    /// <summary>
    /// The DEV preview's clearance overlay (plan W5: "The DEV preview confirms every clearance at the field's worst draws before Astra
    /// writes the table"): A1's blocks, the gates' leaf travel and track, the 4 m lanes, the mess ring's walkway, and the landing
    /// field's pods (hull, skid apron and the capsule at ExitReach 2.4 m) for a seed. Boxes only (filled, never ring outlines).
    /// </summary>
    public static class NativeCampOverlay
    {
        public const int BlockHex=0xFFB040,PocketHex=0xFF3A30,LaneHex=0x36E07A,WalkHex=0x34E2FF,PodHex=0xF1F5F9,ApronHex=0xC8B25A,CapsuleHex=0x70B5FF;
        /// <summary>A box of the overlay: corners (min, max), colour and alpha.</summary>
        public readonly struct Box{public readonly V3 Lo,Hi;public readonly int Hex;public readonly float Alpha;public Box(V3 lo,V3 hi,int hex,float alpha){Lo=lo;Hi=hi;Hex=hex;Alpha=alpha;}}
        /// <summary>The camp's two machines as PveMachine records (the A1 hand-off's points: front 0.7, dispense 1.3, chipDrop 2.2).</summary>
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
        /// <summary>The overlay for a seed's landing field.</summary>
        public static List<Box> Boxes(uint seed)
        {
            var o=new List<Box>();
            foreach(var r in NativeCamp.Table)o.Add(new Box(r.Min,r.Max,BlockHex,.22f));
            for(int s=0;s<4;s++)foreach(int sign in new[]{-1,1})
            {
                // the leaf travel and pocket, from the inner face to 0.5 m inboard, to the track's top
                V3 a=NativeCamp.OnFence(s,sign*NativeCamp.GateHalf,0,-NativeCamp.PocketInboard),b=NativeCamp.OnFence(s,sign*NativeCamp.PocketTo,NativeCamp.PocketTop,0);
                o.Add(new Box(Min(a,b),Max(a,b),PocketHex,.3f));
            }
            for(int s=0;s<4;s++){V3 a=NativeCamp.OnFence(s,-2,0,0),b=NativeCamp.OnFence(s,2,.02f,9-NativeCamp.Inner);o.Add(new Box(Min(a,b),Max(a,b),LaneHex,.28f));}
            // the walkway round the mess ring (2.5 m wide, as four flat strips)
            float r0=4.6f,r1=7.1f;
            o.Add(new Box(new V3(-r1,0,r0),new V3(r1,.015f,r1),WalkHex,.22f));o.Add(new Box(new V3(-r1,0,-r1),new V3(r1,.015f,-r0),WalkHex,.22f));
            o.Add(new Box(new V3(r0,0,-r0),new V3(r1,.015f,r0),WalkHex,.22f));o.Add(new Box(new V3(-r1,0,-r0),new V3(-r0,.015f,r0),WalkHex,.22f));
            foreach(var (id,kind,x,z,yaw) in NativeCamp.Machines)o.Add(new Box(new V3(x-.65f,0,z-.65f),new V3(x+.65f,2.2f,z+.65f),BlockHex,.15f));
            // the landing field (its first four seats): hull (an octagon's bounding box), skid apron behind, the future capsule
            var field=NativeExpeditionHubPlan.LandingField(seed);
            for(int i=0;i<4&&i<field.Length;i++)
            {
                var p=field[i];double r=p.yaw*Math.PI/180;float fx=(float)Math.Sin(r),fz=(float)Math.Cos(r);float h=NativeExpeditionHubPlan.HullRadius;
                o.Add(new Box(new V3(p.x-h,0,p.z-h),new V3(p.x+h,3f,p.z+h),PodHex,.18f));
                var back=new V3(p.x-fx*NativeExpeditionHubPlan.SkidLength,0,p.z-fz*NativeExpeditionHubPlan.SkidLength);
                o.Add(new Box(new V3(Math.Min(p.x,back.x)-h*.6f,0,Math.Min(p.z,back.z)-h*.6f),new V3(Math.Max(p.x,back.x)+h*.6f,.02f,Math.Max(p.z,back.z)+h*.6f),ApronHex,.25f));
                var cap=new V3(p.x+fx*2.4f,0,p.z+fz*2.4f);o.Add(new Box(new V3(cap.x-.42f,0,cap.z-.42f),new V3(cap.x+.42f,1.7f,cap.z+.42f),CapsuleHex,.3f));
            }
            return o;
        }
        private static V3 Min(V3 a,V3 b)=>new V3(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Min(a.z,b.z));
        private static V3 Max(V3 a,V3 b)=>new V3(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z));
    }
}
