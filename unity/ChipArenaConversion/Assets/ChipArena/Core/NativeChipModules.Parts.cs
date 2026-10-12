using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Standard Chip attachments, sculpted with the Standard weapons' kit (NativeWeaponSculpt) at the same three tiers: dark
    // chamfered housings in the weapons' gunmetal, panel slots, and Chip-coloured emissive strips. Emission tag 1 marks the
    // Chip-coloured zones: the draw passes the kind's colour as the body tint (NativeSourceLit tints tag 1 by _BodyTint), so the
    // old pieces' colours carry over (NativeChipModules colour constants). Shared parts (drones, fins, charges, ...) are one mesh
    // per tier for every weapon, sized by the module's uniform scale; per-weapon parts (collars, coils, coils' runs, intakes,
    // pods, cells and the blade strips) are built round that weapon's own sections and profile. Every part is built once, at
    // bank load (Runtime/NativeChipModuleModels.cs). Engine-free and deterministic.
    public static partial class NativeChipModules
    {
        private static readonly WeaponPaint Shell=WeaponPaint.Solid(0x3d535e,.05f),ShellLight=WeaponPaint.Solid(0x60767f,.06f),ShellDark=WeaponPaint.Solid(0x1e2a30,.02f),
            Hole=WeaponPaint.Solid(0x0d1215),Steel=WeaponPaint.Solid(0x8fa6ac,.1f),Sleeve=WeaponPaint.Solid(0x647e89,.05f);
        /// <summary>A Chip-coloured light (tag 1): albedo share and emission strength of the kind's colour.</summary>
        private static WeaponPaint Glow(float strength=1.8f,float albedo=.35f)=>new WeaponPaint(Vector3.One*albedo,Vector3.One*strength*1.2f,1);
        /// <summary>A lit Chip-coloured surface (tag 1, the source's lit magazine block).</summary>
        private static WeaponPaint Tone(float k=.72f)=>new WeaponPaint(Vector3.One*k,Vector3.One*k*.05f,1);
        /// <summary>Parts one mesh serves on every weapon (scaled per mount); the rest are built per weapon.</summary>
        public static bool Shared(ChipPart p)=>p==ChipPart.Drone||p==ChipPart.Fin||p==ChipPart.Seeker||p==ChipPart.Prism||p==ChipPart.Vane||p==ChipPart.Charge||p==ChipPart.Frag||p==ChipPart.Capacitor||
            p==ChipPart.Flame||p==ChipPart.Mote||p==ChipPart.Spike||p==ChipPart.Flywheel||p==ChipPart.Clamp||p==ChipPart.Chevron;
        /// <summary>The parts a weapon's layout can place (the Runtime builds exactly these).</summary>
        public static bool Uses(string spec,ChipPart p)
        {
            var f=Family(spec);if(f==ChipFamily.None||!Designed(spec))return false;
            switch(p)
            {
                case ChipPart.Pod:case ChipPart.Lance:case ChipPart.Launcher:case ChipPart.Cap:return f==ChipFamily.Gun;
                case ChipPart.Helix:return f==ChipFamily.Staff||f==ChipFamily.Gun&&spec!="pistol";
                case ChipPart.Flywheel:return spec=="pistol";
                case ChipPart.Piston:return f==ChipFamily.Gun||f==ChipFamily.Staff;
                case ChipPart.Fin:return f==ChipFamily.Gun||f==ChipFamily.Staff;
                case ChipPart.Frag:return f==ChipFamily.Staff;
                case ChipPart.Collar:case ChipPart.Coil:return f!=ChipFamily.Shield;
                case ChipPart.Drone:return f!=ChipFamily.Gun;
                case ChipPart.EdgeRail:case ChipPart.Echo:return true;
                case ChipPart.Spinner:case ChipPart.Clamp:case ChipPart.Sawback:case ChipPart.HeatEdge:case ChipPart.Coat:case ChipPart.Flame:case ChipPart.Mote:return f==ChipFamily.Blade||f==ChipFamily.Shield;
                case ChipPart.Chevron:return f==ChipFamily.Shield;
                case ChipPart.Spike:return f==ChipFamily.Staff||f==ChipFamily.Shield;
                case ChipPart.Tip:return f==ChipFamily.Blade;
                case ChipPart.Count:return false;
                default:return true;   // Gimbal, Intake, Seeker, Prism, Vane, Charge, Capacitor, Cell
            }
        }
        /// <summary>How many variants a part has on `spec` (the collars and coils: one per section they wrap).</summary>
        public static int VariantCount(string spec,ChipPart p){var s=Sockets(spec);return (p==ChipPart.Collar||p==ChipPart.Coil)&&s?.Sections!=null?Math.Min(Variants,s.Sections.Length):1;}
        /// <summary>The left dagger mirrors its blade (NativeWeaponModels: MirrorLeft); its per-weapon parts get mirrored copies.</summary>
        public static bool HasLeft(string spec,ChipPart p)=>!Shared(p)&&(NativeWeaponModels.Get(spec)?.MirrorLeft??false)&&Family(spec)==ChipFamily.Blade;

        /// <summary>Parts that are only light (no housing to shade): they cast no shadow.</summary>
        public static bool Light(ChipPart p)=>p==ChipPart.Flame||p==ChipPart.Mote||p==ChipPart.EdgeRail||p==ChipPart.Echo||p==ChipPart.HeatEdge||p==ChipPart.Coat||p==ChipPart.Gimbal;
        private static string[] idTable;private static Dictionary<string,int> idSpecs;
        /// <summary>
        /// The bank id of one part mesh ("chip-<spec or shared>-<part>-<variant>-<tier>[-left]"); null when no such mesh is built.
        /// Every id is made once, on the first call, so a draw's lookup allocates nothing.
        /// </summary>
        public static string ModelId(string spec,ChipPart part,int variant,int tier,bool left=false)
        {
            if(idTable==null)BuildIds();int s;if(Shared(part))s=0;else if(spec==null||!idSpecs.TryGetValue(spec,out s))return null;
            return idTable[IdKey(s,part,variant,tier,left)];
        }
        private static int IdKey(int s,ChipPart part,int variant,int tier,bool left)=>((((s*(int)ChipPart.Count+(int)part)*Variants+Math.Max(0,Math.Min(Variants-1,variant)))*3+Math.Max(0,Math.Min(2,tier)))*2)+(left?1:0);
        private static void BuildIds()
        {
            var specs=new List<string>();foreach(var spec in NativeWeaponModels.Specs)if(Designed(spec))specs.Add(spec);
            var map=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<specs.Count;i++)map[specs[i]]=i+1;var table=new string[(specs.Count+1)*(int)ChipPart.Count*Variants*3*2];
            for(int p=0;p<(int)ChipPart.Count;p++)
            {
                var part=(ChipPart)p;string name=part.ToString().ToLowerInvariant();
                if(Shared(part)){for(int t=0;t<3;t++)table[IdKey(0,part,0,t,false)]="chip-shared-"+name+"-0-"+t;continue;}
                for(int i=0;i<specs.Count;i++){string spec=specs[i];if(!Uses(spec,part))continue;for(int v=0;v<VariantCount(spec,part);v++)for(int t=0;t<3;t++){table[IdKey(i+1,part,v,t,false)]="chip-"+spec+"-"+name+"-"+v+"-"+t;if(HasLeft(spec,part))table[IdKey(i+1,part,v,t,true)]="chip-"+spec+"-"+name+"-"+v+"-"+t+"-left";}}
            }
            idSpecs=map;idTable=table;
        }
        /// <summary>Every mesh the bank builds: (spec or null for a shared part, part, variant, tier, left).</summary>
        public static IEnumerable<(string spec,ChipPart part,int variant,int tier,bool left)> Meshes()
        {
            for(int p=0;p<(int)ChipPart.Count;p++)
            {
                var part=(ChipPart)p;bool used=false;foreach(var spec in NativeWeaponModels.Specs)used|=Uses(spec,part);if(!used)continue;
                if(Shared(part)){for(int t=0;t<3;t++)yield return (null,part,0,t,false);continue;}
                foreach(var spec in NativeWeaponModels.Specs)if(Uses(spec,part))for(int v=0;v<VariantCount(spec,part);v++)for(int t=0;t<3;t++){yield return (spec,part,v,t,false);if(HasLeft(spec,part))yield return (spec,part,v,t,true);}
            }
        }
        /// <summary>
        /// A fresh mesh of one part (source weapon frame, one section on the model root), or null when `spec` does not use it.
        /// Shared parts ignore `spec`. `left`: mirrored across x for the left dagger.
        /// </summary>
        public static WeaponMesh Build(string spec,ChipPart part,int variant,int tier,bool left=false)
        {
            if(!Shared(part)&&!Uses(spec,part))return null;tier=Math.Max(0,Math.Min(2,tier));var s=Shared(part)?null:Sockets(spec);
            var mesh=new WeaponMesh{Spec=Shared(part)?"chip":spec,Tier=tier};var k=new WeaponSculpt(mesh,tier);k.Into(-1);
            switch(part)
            {
                case ChipPart.Pod:Pod(k,s);break;case ChipPart.Drone:Drone(k);break;case ChipPart.Collar:Collar(k,s.Sections[Math.Min(variant,s.Sections.Length-1)],s.Family==ChipFamily.Blade);break;
                case ChipPart.Coil:Coil(k,s.Sections[Math.Min(variant,s.Sections.Length-1)],s.Family==ChipFamily.Blade);break;case ChipPart.Helix:Helix(k,s);break;case ChipPart.Gimbal:Gimbal(k,GimbalRadius(s));break;
                case ChipPart.Intake:Intake(k,s);break;case ChipPart.Fin:Fin(k);break;case ChipPart.Seeker:Seeker(k);break;case ChipPart.Prism:Prism(k);break;case ChipPart.Lance:Lance(k,s.LanceLength);break;
                case ChipPart.Cell:Cell(k,s);break;case ChipPart.Cap:CellCap(k,s);break;case ChipPart.Vane:Vane(k);break;case ChipPart.Piston:Piston(k,s.PistonLength);break;case ChipPart.Charge:Charge(k);break;
                case ChipPart.Launcher:Launcher(k,s.LauncherLength);break;case ChipPart.Sawback:Sawback(k,s,tier);break;case ChipPart.Frag:Frag(k);break;case ChipPart.Capacitor:Capacitor(k);break;
                case ChipPart.EdgeRail:EdgeRail(k,s,tier);break;case ChipPart.Echo:EchoInlay(k,s,tier);break;case ChipPart.HeatEdge:HeatEdge(k,s,tier);break;case ChipPart.Coat:Coat(k,s,tier);break;
                case ChipPart.Flame:FlameTongue(k);break;case ChipPart.Mote:Mote(k);break;case ChipPart.Spinner:Spinner(k,s.SpinnerR);break;case ChipPart.Spike:Spike(k);break;case ChipPart.Flywheel:Flywheel(k);break;
                case ChipPart.Clamp:Clamp(k);break;case ChipPart.Chevron:Chevron(k);break;case ChipPart.Tip:TipCap(k,s,tier);break;default:return null;
            }
            mesh.Sections.RemoveAll(x=>x.Positions.Count==0);if(mesh.Sections.Count==0)return null;
            if(left)foreach(var x in mesh.Sections)
            {
                for(int i=0;i<x.Positions.Count;i++){var p=x.Positions[i];p.X=-p.X;x.Positions[i]=p;var nn=x.Normals[i];nn.X=-nn.X;x.Normals[i]=nn;}
                for(int i=0;i+2<x.Indices.Count;i+=3){int t=x.Indices[i+1];x.Indices[i+1]=x.Indices[i+2];x.Indices[i+2]=t;}
            }
            return mesh;
        }
        private static float GimbalRadius(ChipSockets s)=>s.Family==ChipFamily.Gun?s.DeviceR:s.GimbalR;

        // ---- measuring the Standard models (sockets and the blade strips read the meshes themselves) ----
        /// <summary>
        /// The section of `mesh` (bone `bone`, sections of `stretch` or all when null) cut by the plane z: its x span and its
        /// largest |y|. False when nothing crosses the plane. Exact for the lofted blades (each triangle edge crossing z).
        /// </summary>
        public static bool Slice(WeaponMesh mesh,int bone,WeaponStretch? stretch,float z,out float minX,out float maxX,out float halfY)
        {
            minX=float.MaxValue;maxX=float.MinValue;halfY=0;bool any=false;
            foreach(var s in mesh.Sections)
            {
                if(s.Bone!=bone||stretch!=null&&s.Stretch!=stretch.Value)continue;
                for(int i=0;i+2<s.Indices.Count;i+=3)for(int e=0;e<3;e++)
                {
                    var a=s.Positions[s.Indices[i+e]];var b=s.Positions[s.Indices[i+(e+1)%3]];if((a.Z-z)*(b.Z-z)>0||Math.Abs(b.Z-a.Z)<1e-7f)continue;
                    float t=(z-a.Z)/(b.Z-a.Z);var p=a+(b-a)*t;minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);halfY=Math.Max(halfY,Math.Abs(p.Y));any=true;
                }
            }
            return any;
        }
        private static readonly Dictionary<(string,int),WeaponMesh> measured=new Dictionary<(string,int),WeaponMesh>();
        private static WeaponMesh Measured(string spec,int tier){if(!measured.TryGetValue((spec,tier),out var m)){m=NativeWeaponModels.Build(spec,tier);measured[(spec,tier)]=m;}return m;}
        // The blade's profile every 2 cm from its base to the tip socket, sliced from the tier's own blade sections.
        private static void Profile(string spec,ChipSockets s,int tier=0)
        {
            var d=NativeWeaponModels.Get(spec);var m=Measured(spec,tier);var zs=new List<float>();var lo=new List<float>();var hi=new List<float>();var hy=new List<float>();
            for(float z=d.StretchBase+.004f;z<d.StretchTip-.002f;z+=.02f)if(Slice(m,d.Bone,WeaponStretch.Blade,z,out float a,out float b,out float c)){zs.Add(z);lo.Add(a);hi.Add(b);hy.Add(c);}
            s.ProfileZ=zs.ToArray();s.ProfileMinX=lo.ToArray();s.ProfileMaxX=hi.ToArray();s.ProfileHalfY=hy.ToArray();
        }
        private static ChipSockets TierProfile(ChipSockets s,int tier){if(tier==0||s.Family!=ChipFamily.Blade)return s;var t=new ChipSockets{StripZ0=s.StripZ0,StripZ1=s.StripZ1,DoubleEdge=s.DoubleEdge,EdgeSign=s.EdgeSign};Profile(s.Spec,t,tier);return t;}

        // ---- helpers ----
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static Vector3[] Ring(WeaponSculpt k,in ChipSection s,float grow,float z,int sides=16)
            =>s.Round?Circle(k.Sides(sides*3/4),s.HalfX+grow,s.HalfX+grow,z,Pi/sides):Rect(2*(s.HalfX+grow),2*(s.HalfY+grow),s.Corner+grow*.6f,z);
        private static void Disc(WeaponSculpt k,WeaponPaint c,Vector3 at,float r,int sides){var ring=Circle(sides,r,r,at.Z,0,at.X,at.Y);for(int i=0;i<sides;i++)k.Tri(c,at,ring[i],ring[(i+1)%sides]);}
        // A quad / triangle turned to face away from `inside` (the sculpt kit's front is counter-clockwise).
        private static void QuadAway(WeaponSculpt k,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 inside)
        {var n=Vector3.Cross(d-a,e-b);if(Vector3.Dot(n,(a+b+d+e)*.25f-inside)<0){var t=b;b=e;e=t;}k.Quad(c,a,b,d,e);}
        private static void TriAway(WeaponSculpt k,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 inside)
        {var n=Vector3.Cross(b-a,d-a);if(Vector3.Dot(n,(a+b+d)/3-inside)<0){var t=b;b=d;d=t;}k.Tri(c,a,b,d);}
        // A solid ribbon between two polylines in the yz plane (x = +-th): a fin, a vane. `outer` edge faces get `edge`.
        private static void Ribbon(WeaponSculpt k,Vector3[] outer,Vector3[] inner,float th,WeaponPaint face,WeaponPaint edge,WeaponPaint back)
        {
            int n=outer.Length;Vector3 X=V(th,0,0);
            for(int i=0;i+1<n;i++)
            {
                var mid=(outer[i]+outer[i+1]+inner[i]+inner[i+1])*.25f;
                QuadAway(k,face,inner[i]+X,outer[i]+X,outer[i+1]+X,inner[i+1]+X,mid);QuadAway(k,face,inner[i]-X,outer[i]-X,outer[i+1]-X,inner[i+1]-X,mid);
                QuadAway(k,edge,outer[i]+X,outer[i]-X,outer[i+1]-X,outer[i+1]+X,mid);QuadAway(k,back,inner[i]+X,inner[i]-X,inner[i+1]-X,inner[i+1]+X,mid);
            }
            var m0=(outer[0]+outer[1]+inner[0]+inner[1])*.25f;var m1=(outer[n-1]+outer[n-2]+inner[n-1]+inner[n-2])*.25f;
            QuadAway(k,back,inner[0]+X,inner[0]-X,outer[0]-X,outer[0]+X,m0);QuadAway(k,edge,outer[n-1]+X,outer[n-1]-X,inner[n-1]-X,inner[n-1]+X,m1);
        }
        // A strip loft along z through (x(z), y(z)): a thin glowing inlay or rail of section w x h, capped.
        private static void Strip(WeaponSculpt k,WeaponPaint c,ChipSockets s,float z0,float z1,Func<float,Vector2> at,float w,float h,int steps)
        {
            var rings=new List<Vector3[]>();for(int i=0;i<=steps;i++){float z=z0+(z1-z0)*i/steps;var p=at(z);float x=w/2,y=h/2;rings.Add(new[]{V(p.X+x,p.Y-y,z),V(p.X+x,p.Y+y,z),V(p.X-x,p.Y+y,z),V(p.X-x,p.Y-y,z)});}
            k.Loft(rings,(i,j)=>c,0,true,true);
        }
        private static int StripSteps(WeaponSculpt k,float length)=>Math.Max(4,(int)(length/(k.Fine?.05f:k.Mid?.09f:.14f)));

        // ---- axial parts ----
        // Ricochet: a chamfered deflector collar clamped round the section with a glowing groove (and bolt heads at full detail).
        private static void Collar(WeaponSculpt k,ChipSection s,bool slim=false)
        {
            float h=slim?.0045f:.0065f,t=slim?.0042f:.0065f,c=slim?.0015f:.0022f;
            k.Loft(new[]{Ring(k,s,.0012f,-h),Ring(k,s,t,-h+c),Ring(k,s,t,h-c),Ring(k,s,.0012f,h)},(i,j)=>i==1?ShellLight:Shell,0,true,true);
            float g=k.Mid?.0008f:.0004f,gh=slim?.0014f:.0019f;k.Loft(new[]{Ring(k,s,t+g,-gh),Ring(k,s,t+g,gh)},(i,j)=>Glow(1.9f,.4f),0,false,false);
            if(k.Fine)for(int q=-1;q<=1;q+=2){float x=q*(s.HalfX+t+.0004f);k.Box(Steel,V(x,0,-.0042f),V(.0012f,.003f,.003f),0);k.Box(Steel,V(x,0,.0042f),V(.0012f,.003f,.003f),0);}
        }
        // Velocity: a thin accelerator coil ring on standoffs (an arch over the pistol's slide, open at the frame).
        private static void Coil(WeaponSculpt k,ChipSection s,bool slim=false)
        {
            var g=Glow(2f,.45f);
            if(s.Arch)
            {
                float hx=s.HalfX+.0042f,hy=s.HalfY+.0042f,c=s.Corner+.002f,bottom=-s.HalfY+.004f;
                var path=new List<Vector3>{V(hx,bottom,0),V(hx,hy-c,0),V(hx-c,hy,0),V(-hx+c,hy,0),V(-hx,hy-c,0),V(-hx,bottom,0)};
                k.Tube(_=>g,path,_=>.0026f,k.Mid?6:4,Vector3.UnitZ,0,true,false,1.2f,Pi/4);
                for(int q=-1;q<=1;q+=2){k.Box(Shell,V(q*(s.HalfX+.0022f),bottom+.002f,0),V(.0045f,.007f,.008f),0);if(k.Mid)k.Box(Shell,V(q*(hx-c*.5f),hy-c*.5f,0),V(.006f,.006f,.007f),0,0,0,q*Pi/4);}
                return;
            }
            float o=slim?.0036f:.0056f,w=slim?.0016f:.0026f;k.Loft(new[]{Ring(k,s,.0016f,-w),Ring(k,s,o,-w*.65f),Ring(k,s,o,w*.65f),Ring(k,s,.0016f,w)},(i,j)=>i==1?g:Shell,0,true,true);
            if(k.Mid)for(int q=0;q<4;q++){float a=q*Pi/2+Pi/4;var n=s.Round?V((float)Math.Cos(a),(float)Math.Sin(a),0):V(q<2?1:-1,q%3==0?1:-1,0);
                var at=s.Round?n*(s.HalfX+.0016f):V(n.X*(s.HalfX-s.Corner*.3f+.0012f),n.Y*(s.HalfY-s.Corner*.3f+.0012f),0);k.Box(Shell,at,V(.004f,.004f,.0065f),0,0,0,a);}
        }
        // Fire / cast / swing rate: a glowing helical coil between two washers and three spacer bars, spinning about the bore.
        private static void Helix(WeaponSculpt k,ChipSockets s)
        {
            float R=s.HelixR,L=s.HelixLength,inner=(s.Family==ChipFamily.Staff?s.Sections[0].HalfX:s.Sections[s.Sections.Length-1].HalfX)+.0008f;if(s.Spec=="rifle")inner=s.Sections[0].HalfX+.0008f;
            int turns=3,steps=k.Fine?42:k.Mid?27:18;var path=new List<Vector3>(steps+1);
            for(int i=0;i<=steps;i++){float t=(float)i/steps,a=t*turns*2*Pi;path.Add(V(R*(float)Math.Cos(a),R*(float)Math.Sin(a),-L/2+.007f+t*(L-.014f)));}
            k.Tube(_=>Glow(1.9f,.4f),path,_=>.0034f,k.Fine?5:4,Vector3.UnitZ,1,true);
            for(int e=-1;e<=1;e+=2){float z=e*(L/2-.0035f);k.Lathe(i=>i==1?ShellLight:Shell,new[]{inner,z-.0035f,R+.0045f,z-.0035f,R+.0045f,z+.0035f,inner,z+.0035f},k.Sides(14),0);}
            if(k.Mid)for(int b=0;b<3;b++){float a=b*Pi*2/3+Pi/6;k.Box(ShellLight,V((R+.0038f)*(float)Math.Cos(a),(R+.0038f)*(float)Math.Sin(a),0),V(.0045f,.0045f,L-.008f),.0012f,0,0,a);}
        }
        // Blade / shield rate: a hardlight ring with three node housings, spinning round the ricasso or the boss.
        private static void Spinner(WeaponSculpt k,float R)
        {
            k.Torus(Glow(1.8f,.4f),V(0,0,0),R,.0028f,k.Sides(24),k.Fine?4:3);
            for(int q=0;q<3;q++){float a=q*Pi*2/3;var at=V(R*(float)Math.Cos(a),R*(float)Math.Sin(a),0);k.Box(Shell,at,V(.011f,.011f,.0105f),k.Fine?.0025f:0,0,0,a);if(k.Mid)k.Box(Glow(2.2f,.5f),at+V(0,0,.0056f),V(.0045f,.0045f,.0012f),0,0,0,a);}
            if(k.Fine)k.Torus(ShellDark,V(0,0,-.0035f),R,.0016f,20,3);
        }
        // Pong: one rally ring of the gimbal (three are drawn at 60 degrees), with two node housings.
        private static void Gimbal(WeaponSculpt k,float R)
        {
            k.Torus(Glow(1.7f,.4f),V(0,0,0),R,.0026f,k.Sides(20),k.Fine?4:3);
            for(int q=0;q<2;q++){var at=V(q==0?R:-R,0,0);k.Box(Shell,at,V(.008f,.009f,.009f),0);if(k.Mid)k.Box(Glow(2.2f,.5f),at+V(q==0?.0042f:-.0042f,0,0),V(.0012f,.004f,.004f),0);}
        }
        // Vacuum: a flared intake bell on the muzzle (guns), a hardlight ring round the head (staffs), the blade or the boss.
        private static void Intake(WeaponSculpt k,ChipSockets s)
        {
            if(s.Family==ChipFamily.Gun)
            {
                float rb=s.Device.Reach+.002f,rf=s.IntakeR,L=.024f;int n=k.Sides(14);
                k.Loft(new[]{Circle(n,rb,rb,-L),Circle(n,rb+.003f,rb+.003f,-L+.003f),Circle(n,rf,rf,-.004f),Circle(n,rf-.0025f,rf-.0025f,0)},(i,j)=>i==1?ShellLight:Shell,0,false,false);
                k.Loft(new[]{Circle(n,rf-.0055f,rf-.0055f,0),Circle(n,rb-.0005f,rb-.0005f,-L+.004f)},(i,j)=>Hole,0,false,false,null,false,true);
                k.Torus(Glow(2f,.45f),V(0,0,-.0012f),rf-.004f,.0021f,n,4);
                if(k.Mid)for(int q=0;q<6;q++){float a=q*Pi/3+Pi/6,r=(rb+rf)*.5f-.002f;k.Box(Shell,V(r*(float)Math.Cos(a),r*(float)Math.Sin(a),-L*.45f),V(rf-rb-.004f,.0025f,.012f),0,0,0,a);}
                return;
            }
            float R=s.Family==ChipFamily.Staff?s.IntakeR:s.VacuumR;
            k.Torus(Glow(1.9f,.45f),V(0,0,0),R,.0034f,k.Sides(30),k.Fine?4:3);if(k.Fine)k.Torus(Glow(1.1f,.3f),V(0,0,0),R-.009f,.0014f,20,3);
            int nodes=s.Family==ChipFamily.Shield?8:4;for(int q=0;q<nodes;q++){float a=q*Pi*2/nodes+Pi/nodes;var at=V(R*(float)Math.Cos(a),R*(float)Math.Sin(a),0);k.Box(Shell,at,V(.012f,.007f,.008f),0,0,0,a);}
        }

        // ---- run parts (on their surface at y = 0, rising along +y, long along z) ----
        // Multishot (guns): an auxiliary hex barrel on bridges, a flared muzzle sleeve with a glowing emitter ring, a status strip.
        private static void Pod(WeaponSculpt k,ChipSockets s)
        {
            float r=s.PodR,L=s.PodLength,g=s.PodGap,cy=g+r;
            foreach(float b in s.PodBridges)k.Box(Shell,V(0,cy*.5f,b),V(r*1.1f,cy+.002f,.014f),0);
            k.Push(T(0,cy,0));
            k.Lathe(Sleeve,new[]{r*.92f,-L/2,r,-L/2+.004f,r,L/2-.026f},6,0,Pi/6,true);
            k.Lathe(i=>i==1?Glow(2f,.45f):ShellLight,new[]{r*1.02f,L/2-.028f,r*1.15f,L/2-.022f,r*1.15f,L/2-.009f,r*.98f,L/2},k.Sides(8),0,0,false);
            Disc(k,Hole,V(0,0,L/2-.0005f),r*.55f,k.Sides(8));if(k.Fine)k.Lathe(Hole,new[]{r*.55f,L/2-.0005f,r*.55f,L/2-.012f},8,0,0,false);
            if(k.Mid){k.Box(Glow(1.2f,.3f),V(0,r*.88f,-L*.12f),V(r*.42f,.0012f,L*.42f),0);if(k.Fine)for(int i=0;i<3;i++)k.Box(ShellDark,V(0,0,-L/2+.03f+i*.012f),V(r*2.06f,r*1.7f,.004f),0);}
            k.Pop();
        }
        // Pierce: a rail-mounted penetrator lance (a chamfered housing tapering to a white lens, a glowing core line each side).
        private static void Lance(WeaponSculpt k,float L)
        {
            float w=.02f,h=.018f,cy=.004f+h/2;
            k.Loft(new[]{Rect(w*.8f,h*.8f,.004f,-L/2,0,cy),Rect(w,h,.005f,-L/2+.005f,0,cy),Rect(w,h,.005f,L/2-.04f,0,cy),Rect(w*.62f,h*.7f,.003f,L/2-.006f,0,cy-.001f),Rect(w*.5f,h*.55f,.002f,L/2,0,cy-.001f)},(i,j)=>i==1?Shell:ShellLight,0,true,false);
            Disc(k,Glow(2.6f,.6f),V(0,cy-.001f,L/2+.0004f),h*.24f,k.Sides(10));
            for(int q=-1;q<=1;q+=2)k.Box(Glow(2f,.45f),V(q*(w/2+.0005f),cy+.002f,-.01f),V(.0012f,.0024f,L*.66f),0);
            for(int q=-1;q<=1;q+=2)k.Box(ShellDark,V(0,.002f,q*L*.3f),V(w*1.1f,.004f,.014f),.001f);
            if(k.Fine)for(int i=0;i<4;i++)k.Decal(Hole,V(0,cy+h/2,-L/2+.02f+i*.012f),V(0,0,.002f),V(w*.32f,0,0),.0009f);
        }
        // Kinetic: a recoil damper (sleeve, rod and cap) on two feet, glowing rings at the sleeve's ends and a lit status strip.
        private static void Piston(WeaponSculpt k,float L)
        {
            float r=.0085f,cy=.0065f+r;int n=k.Sides(8);
            k.Cylinder(Shell,r,-L/2,L*.14f,n,.0015f,0,cy);k.Cylinder(Steel,r*.52f,L*.14f,L/2-.012f,k.Sides(8),0,0,cy);k.Cylinder(ShellLight,r*.86f,L/2-.012f,L/2,n,.0015f,0,cy);
            foreach(float z in new[]{L*.14f-.004f,-L/2+.008f})k.Torus(Glow(2f,.45f),V(0,cy,z),r+.0004f,.0017f,n,3);
            if(k.Mid)k.Box(Glow(1.3f,.35f),V(0,cy+r+.0004f,-L*.18f),V(.0028f,.0012f,L*.36f),0);
            for(int q=-1;q<=1;q+=2)k.Box(ShellDark,V(0,.0035f,q*L*.32f),V(r*1.5f,.007f,.016f),0);
        }
        // Shrapnel (guns): an under-barrel launcher tube with a segmented frag head, glowing seams between its plates.
        private static void Launcher(WeaponSculpt k,float L)
        {
            float r=.0145f,cy=.005f+r;int n=k.Sides(10);
            k.Cylinder(Shell,r,-L/2,L/2-.026f,n,.002f,0,cy);k.Cylinder(ShellDark,r*1.12f,-L/2,-L/2+.012f,n,.0015f,0,cy);
            var head=V(0,cy,L/2-.012f);k.Sphere(Glow(1.8f,.45f),head,V(r*.98f,r*.98f,r*1.05f),k.Mid?5:3,k.Sides(8),true);
            int plates=k.Mid?4:2;for(int q=0;q<plates;q++){float a=q*Pi*2/plates+Pi/4;k.Box(Shell,head+V(r*.62f*(float)Math.Cos(a),r*.62f*(float)Math.Sin(a),0),V(r*.72f,r*.72f,r*1.5f),.002f,0,0,a);}
            for(int q=-1;q<=1;q+=2)k.Box(ShellDark,V(0,.0026f,q*L*.28f),V(r*1.1f,.0055f,.016f),.001f);
            if(k.Mid)k.Box(Glow(1.2f,.3f),V(0,cy+r+.0003f,-L*.15f),V(.003f,.0012f,L*.3f),0);
        }
        // Magazine: one ammunition cell (lit in the Chip's gold with a glowing round-count window), and the extension's end cap.
        private static void Cell(WeaponSculpt k,ChipSockets s)
        {
            var c=s.MagCell;
            if(s.MagDisc||s.Family==ChipFamily.Blade)
            {
                k.Push(WeaponSculpt.Rx(-Pi/2));float r=c.X,h=c.Y*.9f;int ns=k.Sides(s.MagDisc?12:8);k.Cylinder(Tone(),r,-h/2,h/2,ns,0,0,0,0);k.Torus(Glow(1.9f,.45f),V(0,0,0),r+.0003f,.0018f,ns,3);k.Pop();return;
            }
            k.Box(Tone(),V(0,0,0),V(c.X,c.Y*.92f,c.Z),0);
            k.Box(Glow(1.9f,.45f),V(0,0,c.Z/2+.0004f),V(c.X*.62f,c.Y*.4f,.0012f),0);
            if(k.Mid)for(int q=-1;q<=1;q+=2)k.Box(ShellDark,V(q*(c.X/2+.0004f),0,0),V(.0012f,c.Y*.5f,c.Z*.7f),0);
        }
        private static void CellCap(WeaponSculpt k,ChipSockets s)
        {
            var c=s.MagCell;
            if(s.MagDisc){k.Push(WeaponSculpt.Rx(-Pi/2));k.Cylinder(Shell,c.X*1.04f,-.006f,0,k.Sides(12),k.Fine?.002f:0,0,0,0);k.Cylinder(ShellLight,c.X*.5f,-.009f,-.006f,k.Sides(8),0,0,0,0);k.Pop();return;}
            k.Box(Shell,V(0,-.0045f,0),V(c.X*1.06f,.009f,c.Z*1.05f),.003f);k.Box(Glow(1.6f,.4f),V(0,-.0045f,c.Z*.525f+.0004f),V(c.X*.3f,.0028f,.0012f),0);
        }

        // ---- shared mounted parts ----
        // Multishot (staffs, blades, shield): a hardlight drone, a faceted core in a ring housing with two wing fins (+y out).
        private static void Drone(WeaponSculpt k)
        {
            k.Push(WeaponSculpt.Rx(-Pi/2));
            k.Gem(Glow(2.3f,.5f),V(0,0,0),.0105f,.032f,6,.5f,0,.5f);k.Torus(Shell,V(0,0,0),.0175f,.004f,k.Sides(10),3);
            if(k.Mid){for(int q=-1;q<=1;q+=2)k.Box(ShellLight,V(q*.026f,0,-.002f),V(.013f,.018f,.0035f),0);k.Box(Glow(2f,.4f),V(0,.0178f,.004f),V(.004f,.0025f,.0025f),0);}
            k.Pop();
        }
        // Homing: a swept guidance canard (its leading edge glows) on a root block.
        private static void Fin(WeaponSculpt k)
        {
            var outer=new[]{V(0,.004f,.038f),V(0,.024f,.012f),V(0,.038f,-.01f),V(0,.04f,-.024f)};var inner=new[]{V(0,.002f,.02f),V(0,.002f,0),V(0,.002f,-.018f),V(0,.03f,-.028f)};
            Ribbon(k,outer,inner,.0018f,Shell,Glow(2f,.45f),ShellLight);k.Box(ShellDark,V(0,.002f,.004f),V(.007f,.004f,.05f),.0012f);
            if(k.Mid)k.Box(Glow(1.2f,.3f),V(.0019f,.012f,.004f),V(.0006f,.0016f,.022f),0,-.62f);
        }
        // Homing: a seeker head (a housing with a glowing lens forward and a sensor dome on top).
        private static void Seeker(WeaponSculpt k)
        {
            k.Box(Shell,V(0,.0062f,-.002f),V(.02f,.0124f,.026f),k.Fine?.003f:0);Disc(k,Glow(2.4f,.55f),V(0,.0065f,.0112f),.0052f,k.Sides(10));
            k.Sphere(Glow(1.9f,.45f),V(0,.0124f,-.004f),V(.006f,.0045f,.006f),k.Mid?3:2,k.Sides(8));
            if(k.Fine)k.Torus(ShellLight,V(0,.0065f,.0114f),.0062f,.0012f,10,3);
        }
        // Split: a splitter prism (a three-sided crystal) in a bracket, with two forked emitter prongs.
        private static void Prism(WeaponSculpt k)
        {
            k.Box(Shell,V(0,.004f,0),V(.018f,.008f,.05f),.002f);k.Gem(Glow(2.1f,.45f),V(0,.0155f,0),.0085f,.058f,3,.5f,Pi/2);
            if(k.Mid)for(int q=-1;q<=1;q+=2){k.Box(ShellLight,V(q*.0095f,.011f,.028f),V(.0026f,.0035f,.024f),0,0,q*.3f,0);k.Box(Glow(2.2f,.5f),V(q*.0132f,.011f,.04f),V(.0028f,.0028f,.0028f),0);}
        }
        // Boomerang: a curved return vane sweeping back (its outer edge glows).
        private static void Vane(WeaponSculpt k)
        {
            var outer=new[]{V(0,.003f,.03f),V(0,.018f,.02f),V(0,.028f,.004f),V(0,.032f,-.014f),V(0,.028f,-.032f)};var inner=new[]{V(0,.001f,.012f),V(0,.006f,.002f),V(0,.012f,-.01f),V(0,.018f,-.022f),V(0,.024f,-.032f)};
            Ribbon(k,outer,inner,.0016f,ShellLight,Glow(2f,.45f),Shell);k.Box(ShellDark,V(0,.0015f,.012f),V(.006f,.003f,.03f),.001f);
        }
        // Demolition: a breaching charge (a cylinder lying on the surface, a glowing band, dark end caps and a detonator stud).
        private static void Charge(WeaponSculpt k)
        {
            float r=.0105f;int n=k.Sides(8);k.Cylinder(Shell,r,-.014f,.014f,n,k.Fine?.0015f:0,0,r);
            k.Push(T(0,r,0));k.Lathe(Glow(2f,.45f),new[]{r+.0005f,-.0035f,r+.0005f,.0035f},n,1,Pi/8,false);k.Pop();
            k.Cylinder(ShellDark,r*.72f,.014f,.017f,n,0,0,r);k.Cylinder(ShellDark,r*.72f,-.017f,-.014f,n,0,0,r);
            if(k.Mid){k.Box(Steel,V(0,r*2+.002f,.004f),V(.004f,.004f,.006f),0);k.Box(Glow(2.4f,.6f),V(0,r*2+.0042f,.004f),V(.0018f,.0012f,.0018f),0);}
        }
        // Static: a capacitor stack standing on its base (three glowing bands between dark discs) with a discharge prong.
        private static void Capacitor(WeaponSculpt k)
        {
            k.Box(ShellDark,V(0,.0025f,0),V(.016f,.005f,.016f),0);k.Push(WeaponSculpt.Rx(-Pi/2));int n=k.Sides(8);
            var stack=new List<float>();for(int i=0;i<7;i++)stack.AddRange(new[]{.0068f,.005f+i*.0036f});
            k.Lathe(i=>i%2==1?Glow(2f,.45f):Shell,stack.ToArray(),n,0,Pi/8,true);
            k.Cylinder(Steel,.0016f,.027f,.038f,6,0,0,0,0);k.Gem(Glow(2.6f,.6f),V(0,0,.04f),.0032f,.007f,4,.4f);
            k.Pop();
        }
        // Shrapnel (staffs): a spiked frag collar, a dark band round the heel with glowing frag studs.
        private static void Frag(WeaponSculpt k)
        {
            float R=.05f;k.Torus(Shell,V(0,0,0),R,.0075f,k.Sides(16),k.Mid?6:4);int studs=k.Mid?6:4;
            for(int q=0;q<studs;q++){float a=q*Pi*2/studs;var nn=V((float)Math.Cos(a),(float)Math.Sin(a),0);k.Push(Matrix4x4.CreateRotationY(Pi/2)*Matrix4x4.CreateRotationZ(a)*Matrix4x4.CreateTranslation(nn*(R+.006f)));k.Gem(Glow(2.1f,.5f),V(0,0,0),.006f,.022f,4,.3f,Pi/4);k.Pop();}
        }
        // Pierce (staffs, blades, shield): a white penetrator sleeve, a collar and a long glowing point along +z.
        private static void Spike(WeaponSculpt k)
        {
            k.Lathe(i=>i==1?ShellLight:Shell,new[]{.016f,0,.024f,.006f,.024f,.014f,.02f,.018f},k.Sides(8),0,Pi/8,true);
            k.Loft(new[]{Circle(4,.02f,.02f,.016f,Pi/4),Circle(4,.014f,.014f,.05f,Pi/4),Point(4,V(0,0,.09f))},(i,j)=>Glow(2.3f,.55f),0,false,false);
        }
        // Rate (pistol): a toothed flywheel on the flank (its back on the frame at z = 0), a glowing ring round the hub.
        private static void Flywheel(WeaponSculpt k)
        {
            int n=k.Sides(12);k.Cylinder(Shell,.019f,0,.0055f,n,.0012f,0,0,0);k.Cylinder(Steel,.006f,.0055f,.0085f,k.Sides(8),.001f,0,0,0);
            k.Torus(Glow(2f,.45f),V(0,0,.0058f),.012f,.0016f,n,4);int teeth=k.Mid?10:6;
            for(int q=0;q<teeth;q++){float a=q*Pi*2/teeth;k.Box(ShellLight,V(.0205f*(float)Math.Cos(a),.0205f*(float)Math.Sin(a),.0028f),V(.005f,.0045f,.0048f),0,0,0,a);}
        }
        // Kinetic (blades) and ricochet (shield rim): a chamfered clamp block round a bar along x, glowing slots on both faces.
        private static void Clamp(WeaponSculpt k)
        {
            k.Box(Shell,V(0,0,0),V(.03f,.034f,.034f),.005f);for(int q=-1;q<=1;q+=2)k.Box(Glow(2f,.45f),V(0,q*.0172f,0),V(.018f,.0012f,.004f),0);
            if(k.Mid)for(int q=-1;q<=1;q+=2)k.Box(ShellLight,V(q*.0152f,0,0),V(.0012f,.022f,.022f),0);
            if(k.Fine)for(int q=-1;q<=1;q+=2)k.Box(Steel,V(.009f*q,.0172f,.011f),V(.003f,.0012f,.003f),0);
        }
        // Velocity (shield): a chevron vent pointing down the face, glowing on a dark backing plate (rising from z = 0).
        private static void Chevron(WeaponSculpt k)
        {
            for(int q=-1;q<=1;q+=2){k.Box(ShellDark,V(q*.022f,.012f,.0012f),V(.052f,.014f,.0024f),.002f,0,0,q*.5f);k.Box(Glow(2f,.45f),V(q*.022f,.012f,.003f),V(.044f,.005f,.0012f),0,0,0,q*.5f);}
        }
        // Fire: a flame tongue rising along +y and leaning to +z (drawn flickering, NativeChipModules.Animate).
        private static void FlameTongue(WeaponSculpt k)
        {
            k.Push(WeaponSculpt.Rx(-Pi/2));int n=k.Fine?6:4;
            k.Loft(new[]{Circle(n,.0034f,.011f,0),Circle(n,.0048f,.016f,.012f,0,0,-.004f),Circle(n,.004f,.013f,.026f,0,0,-.012f),Circle(n,.0028f,.008f,.039f,0,0,-.022f),Circle(n,.0016f,.0042f,.05f,0,0,-.033f),Point(n,V(0,-.046f,.058f))},(i,j)=>i==0?Glow(3.2f,.55f):i>=3?Glow(2f,.45f):Glow(2.6f,.5f),1,true,false);
            k.Pop();
        }
        // Poison: a venom droplet (a faceted bead).
        private static void Mote(WeaponSculpt k){k.Gem(Glow(2.2f,.5f),V(0,0,0),.0065f,.018f,k.Mid?6:4,.35f,0,.6f);}

        // Pierce (blades): a white penetrator sheath over the point, following the blade's own taper to just past its tip.
        private static void TipCap(WeaponSculpt k,ChipSockets s,int tier)
        {
            var p=TierProfile(s,tier);var d=NativeWeaponModels.Get(s.Spec);float z0=s.TipZ,z1=d.StretchTip;int steps=k.Mid?6:3;var rings=new List<Vector3[]>();
            for(int i=0;i<=steps;i++)
            {
                float z=z0+(z1-z0-.012f)*i/steps,lo=p.MinX(z),hi=p.MaxX(z),cx=(lo+hi)/2,w=hi-lo+.005f,h=2*p.HalfY(z)+.0045f;
                rings.Add(Rect(Math.Max(.004f,w),Math.Max(.003f,h),Math.Min(w,h)*.3f,z,cx,0));
            }
            float tx=(p.MinX(z1-.004f)+p.MaxX(z1-.004f))/2;rings.Add(Point(8,V(tx,0,z1+.006f)));
            k.Loft(rings,(i,j)=>i==0?ShellLight:Glow(2.4f,.55f),0,true,false);
            if(k.Mid)k.Loft(new[]{Rect(p.MaxX(z0)-p.MinX(z0)+.009f,2*p.HalfY(z0)+.008f,.003f,z0-.004f,(p.MinX(z0)+p.MaxX(z0))/2,0),Rect(p.MaxX(z0)-p.MinX(z0)+.009f,2*p.HalfY(z0)+.008f,.003f,z0+.006f,(p.MinX(z0)+p.MaxX(z0))/2,0)},(i,j)=>Shell,0,true,true);
        }

        // ---- strips along the blade (or the shield's rim), from the tier's own profile ----
        private static float[] RimLoop(float inset,out int count)
        {
            var o=NativeWeaponModels.HeaterOutline;var ring=Inset(Outline(0,o),inset,0);count=ring.Length;var v=new float[count*2];for(int i=0;i<count;i++){v[i*2]=ring[i].X;v[i*2+1]=ring[i].Y;}return v;
        }
        private static void RimTube(WeaponSculpt k,WeaponPaint c,float inset,float lift,Func<float,float,float> z,float r)
        {
            var loop=RimLoop(inset,out int count);var path=new List<Vector3>(count);
            for(int i=0;i<count;i++){float x=loop[i*2],y=loop[i*2+1];path.Add(V(x,y,z(x,y)+lift));}
            k.Tube(c,path,r,4,Vector3.UnitZ,0,false,true,1);
        }
        // Reach: a hardlight edge rail just outside the real edge (both edges; the dagger's one), or round the shield's rim.
        private static void EdgeRail(WeaponSculpt k,ChipSockets s,int tier)
        {
            var g=Glow(2.1f,.45f);
            if(s.Family==ChipFamily.Gun||s.Family==ChipFamily.Staff){RunRails(k,g,s,1);return;}
            if(s.Family==ChipFamily.Shield){var loop=RimLoop(-.0028f,out int count);var path=new List<Vector3>(count);for(int i=0;i<count;i++){float x=loop[i*2],y=loop[i*2+1];path.Add(V(x,y,.214f+NativeWeaponModels.Bow(x)*.5f));}k.Tube(g,path,.0026f,4,Vector3.UnitZ,0,false,true,1.6f);return;}
            var p=TierProfile(s,tier);int steps=StripSteps(k,s.StripZ1-s.StripZ0);
            Strip(k,g,p,s.StripZ0,s.StripZ1,z=>new Vector2(p.MaxX(z)+.0018f,0),.0036f,.0062f,steps);
            if(s.DoubleEdge)Strip(k,g,p,s.StripZ0,s.StripZ1,z=>new Vector2(p.MinX(z)-.0018f,0),.0036f,.0062f,steps);
        }
        // Reach (+1) and echo strike (-1) on a gun or staff (melee-only kinds the source still draws): a pair of hardlight rails
        // along the flanks, reach above the side run and echo below it (on a staff: along the bay, reach at the sides, echo top and bottom).
        private static void RunRails(WeaponSculpt k,WeaponPaint g,ChipSockets s,int which)
        {
            int steps=2;
            if(s.Family==ChipFamily.Gun)for(int q=-1;q<=1;q+=2){float x=q*(s.Side.X+.0016f),y=s.Side.Y+which*.016f;Strip(k,g,s,s.Side.Z0+.01f,s.Side.Z1-.01f,z=>new Vector2(x,y),.0032f,.0042f,steps);}
            else for(int q=-1;q<=1;q+=2){float a=which>0?(q>0?0:Pi):(q>0?Pi/2:-Pi/2),r=s.Sections[0].HalfX+.0016f;Strip(k,g,s,s.Sections[0].HalfX>.04f?.4f:.105f,s.RicochetZ[2]-.012f,z=>new Vector2(r*(float)Math.Cos(a),r*(float)Math.Sin(a)),.0032f,.0032f,steps);}
        }
        // Echo strike: phantom inlays on both flats (two lines a flat beside the fuller; the dagger's one near its spine), or the shield's inner trim.
        private static void EchoInlay(WeaponSculpt k,ChipSockets s,int tier)
        {
            var g=Glow(1.8f,.4f);
            if(s.Family==ChipFamily.Gun||s.Family==ChipFamily.Staff){RunRails(k,g,s,-1);return;}
            if(s.Family==ChipFamily.Shield){RimTube(k,g,.078f,.0016f,ShieldFace,.0022f);return;}
            var p=TierProfile(s,tier);int steps=StripSteps(k,s.StripZ1-s.StripZ0);float z0=s.StripZ0+.03f,z1=s.StripZ1-.12f;
            for(int f=-1;f<=1;f+=2)
            {
                if(s.DoubleEdge)for(int q=-1;q<=1;q+=2)Strip(k,g,p,z0,z1,z=>new Vector2((q>0?p.MaxX(z):p.MinX(z))*.58f,f*(p.HalfY(z)+.0004f)),.0042f,.0016f,steps);
                else Strip(k,g,p,z0,z1,z=>new Vector2(p.MinX(z)+(p.MaxX(z)-p.MinX(z))*.3f,f*(p.HalfY(z)*.82f+.0005f)),.0042f,.0016f,steps);
            }
        }
        // Fire: the heated edge, glowing lines on both bevels of each edge (or along the shield's rim top).
        private static void HeatEdge(WeaponSculpt k,ChipSockets s,int tier)
        {
            var g=Glow(2.4f,.5f);
            if(s.Family==ChipFamily.Shield){RimTube(k,g,.008f,.0014f,(x,y)=>.252f+NativeWeaponModels.Bow(x),.0022f);return;}
            var p=TierProfile(s,tier);int steps=StripSteps(k,s.StripZ1-s.StripZ0);
            for(int f=-1;f<=1;f+=2)for(int q=-1;q<=1;q+=2)
            {
                if(!s.DoubleEdge&&q<0)continue;
                Strip(k,g,p,s.StripZ0,s.StripZ1-.02f,z=>new Vector2(q>0?p.MaxX(z)-.0045f:p.MinX(z)+.0045f,f*p.HalfY(z)*.46f),.0042f,.0022f,steps);
            }
        }
        // Poison: venom channels on both flats just inside each edge (or round the shield's inner trim).
        private static void Coat(WeaponSculpt k,ChipSockets s,int tier)
        {
            var g=Glow(1.9f,.42f);
            if(s.Family==ChipFamily.Shield){RimTube(k,g,.05f,.0016f,ShieldFace,.0022f);return;}
            var p=TierProfile(s,tier);int steps=StripSteps(k,s.StripZ1-s.StripZ0);
            for(int f=-1;f<=1;f+=2)for(int q=-1;q<=1;q+=2)
            {
                if(!s.DoubleEdge&&q<0)continue;
                Strip(k,g,p,s.StripZ0+.02f,s.StripZ1-.05f,z=>new Vector2(q>0?p.MaxX(z)-.013f:p.MinX(z)+.013f,f*(p.HalfY(z)*.86f+.0005f)),.0048f,.0016f,steps);
            }
        }
        // Shrapnel (blades, shield): a sawback of glowing-tipped teeth along the (first) edge near the base, or the lower rim.
        private static void Sawback(WeaponSculpt k,ChipSockets s,int tier)
        {
            var steel=Steel;var tip=Glow(2.2f,.5f);
            void Tooth(Vector3 root,Vector3 outward,Vector3 along,float h,float w,float t)
            {
                // A wedge: its back on the edge, leaning toward the base like a saw tooth, the glowing face forward.
                var side=Vector3.Normalize(Vector3.Cross(outward,along))*t;var a=root-along*w;var b=root+along*w;var apex=root+outward*h-along*w*.9f;var c=(a+b+apex)/3;
                TriAway(k,steel,a+side,b+side,apex+side*.2f,c);TriAway(k,steel,a-side,b-side,apex-side*.2f,c);
                QuadAway(k,steel,a+side,apex+side*.2f,apex-side*.2f,a-side,c);QuadAway(k,tip,b+side,apex+side*.2f,apex-side*.2f,b-side,c);QuadAway(k,steel,a+side,b+side,b-side,a-side,c);
            }
            if(s.Family==ChipFamily.Shield)
            {
                var loop=RimLoop(0,out int count);
                for(int i=0;i<count;i++){float x=loop[i*2],y=loop[i*2+1];if(y>-.05f||Math.Abs(x)<.02f)continue;int j=(i+1)%count;var a=V(x,y,.214f+NativeWeaponModels.Bow(x)*.5f);var b=V(loop[j*2],loop[j*2+1],a.Z);
                    for(int q=0;q<2;q++){var root=a+(b-a)*(.3f+q*.4f);var along=Vector3.Normalize(b-a);var outward=Vector3.Normalize(V(along.Y,-along.X,0));if(Vector3.Dot(outward,V(root.X,root.Y-.06f,0))<0)outward=-outward;Tooth(root,outward,along,.016f,.008f,.006f);}}
                return;
            }
            var p=TierProfile(s,tier);float z0=s.StripZ0+.01f,z1=z0+(s.StripZ1-s.StripZ0)*.28f;int teeth=k.Mid?8:5;
            for(int i=0;i<teeth;i++)
            {
                float z=z0+(z1-z0)*(i+.5f)/teeth;bool spine=!s.DoubleEdge;float x=spine?p.MinX(z):p.MaxX(z);var outward=V(spine?-1:1,0,0);
                Tooth(V(x-outward.X*.001f,0,z),outward,V(0,0,1),.011f,(z1-z0)/teeth*.42f,p.HalfY(z)*.8f+.001f);
            }
        }
    }
}
