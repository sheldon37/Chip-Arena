using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>three.js r180 quaternion and matrix math on plain doubles (source numbers; no engine conventions).</summary>
    public static class NativeShotMath
    {
        /// <summary>Quaternion.setFromUnitVectors (minimal arc, three's antiparallel axis choice), then normalize().</summary>
        public static void FromUnitVectors(double fx,double fy,double fz,double tx,double ty,double tz,out double x,out double y,out double z,out double w)
        {
            double r=fx*tx+fy*ty+fz*tz+1;
            if(r<1e-8){r=0;if(Math.Abs(fx)>Math.Abs(fz)){x=-fy;y=fx;z=0;w=r;}else{x=0;y=-fz;z=fy;w=r;}}
            else{x=fy*tz-fz*ty;y=fz*tx-fx*tz;z=fx*ty-fy*tx;w=r;}
            Normalize(ref x,ref y,ref z,ref w);
        }
        /// <summary>The flight rotation: +Z to the normalised velocity (a zero velocity gives identity, as three's normalize()).</summary>
        public static void Flight(double vx,double vy,double vz,out double x,out double y,out double z,out double w)
        {double l=Math.Sqrt(vx*vx+vy*vy+vz*vz);if(l==0)l=1;l=1/l;FromUnitVectors(0,0,1,vx*l,vy*l,vz*l,out x,out y,out z,out w);}
        public static void Normalize(ref double x,ref double y,ref double z,ref double w)
        {double l=Math.Sqrt(x*x+y*y+z*z+w*w);if(l==0){x=y=z=0;w=1;}else{l=1/l;x*=l;y*=l;z*=l;w*=l;}}
        /// <summary>Quaternion.multiplyQuaternions(a,b): a then b in the local frame (Object3D.rotateX/rotateY append b).</summary>
        public static void Multiply(double ax,double ay,double az,double aw,double bx,double by,double bz,double bw,out double x,out double y,out double z,out double w)
        {x=ax*bw+aw*bx+ay*bz-az*by;y=ay*bw+aw*by+az*bx-ax*bz;z=az*bw+aw*bz+ax*by-ay*bx;w=aw*bw-ax*bx-ay*by-az*bz;}
        /// <summary>Object3D.rotateX(angle): q = q * axisAngle(+X, angle).</summary>
        public static void RotateX(ref double x,ref double y,ref double z,ref double w,double angle){double s=Math.Sin(angle/2),c=Math.Cos(angle/2);Multiply(x,y,z,w,s,0,0,c,out x,out y,out z,out w);}
        public static void RotateY(ref double x,ref double y,ref double z,ref double w,double angle){double s=Math.Sin(angle/2),c=Math.Cos(angle/2);Multiply(x,y,z,w,0,s,0,c,out x,out y,out z,out w);}
        /// <summary>Quaternion.setFromEuler for the default 'XYZ' order.</summary>
        public static void Euler(double ex,double ey,double ez,out double x,out double y,out double z,out double w)
        {
            double c1=Math.Cos(ex/2),c2=Math.Cos(ey/2),c3=Math.Cos(ez/2),s1=Math.Sin(ex/2),s2=Math.Sin(ey/2),s3=Math.Sin(ez/2);
            x=s1*c2*c3+c1*s2*s3;y=c1*s2*c3-s1*c2*s3;z=c1*c2*s3+s1*s2*c3;w=c1*c2*c3-s1*s2*s3;
        }
    }

    /// <summary>A 3x4 affine transform (rows of Matrix4.compose: translation in the last column), single precision.</summary>
    public struct NativeShotAffine
    {
        public float M00,M01,M02,M03,M10,M11,M12,M13,M20,M21,M22,M23;
        /// <summary>Matrix4.compose(position, quaternion, scale).</summary>
        public static NativeShotAffine Compose(double px,double py,double pz,double qx,double qy,double qz,double qw,double sx,double sy,double sz)
        {
            double x2=qx+qx,y2=qy+qy,z2=qz+qz,xx=qx*x2,xy=qx*y2,xz=qx*z2,yy=qy*y2,yz=qy*z2,zz=qz*z2,wx=qw*x2,wy=qw*y2,wz=qw*z2;
            return new NativeShotAffine{M00=(float)((1-(yy+zz))*sx),M10=(float)((xy+wz)*sx),M20=(float)((xz-wy)*sx),M01=(float)((xy-wz)*sy),M11=(float)((1-(xx+zz))*sy),M21=(float)((yz+wx)*sy),M02=(float)((xz+wy)*sz),M12=(float)((yz-wx)*sz),M22=(float)((1-(xx+yy))*sz),M03=(float)px,M13=(float)py,M23=(float)pz};
        }
        /// <summary>Parent group (position, rotation) times a child TRS, computed in double: the matrixWorld of a grouped part.</summary>
        public static NativeShotAffine Grouped(double gx,double gy,double gz,double gqx,double gqy,double gqz,double gqw,double px,double py,double pz,double qx,double qy,double qz,double qw,double sx,double sy,double sz)
        {
            NativeShotMath.Multiply(gqx,gqy,gqz,gqw,qx,qy,qz,qw,out double rx,out double ry,out double rz,out double rw);
            // Rotate the child position by the group rotation: v + 2w(q×v) + 2q×(q×v).
            double cx=gqy*pz-gqz*py,cy=gqz*px-gqx*pz,cz=gqx*py-gqy*px,ux=px+2*(gqw*cx+gqy*cz-gqz*cy),uy=py+2*(gqw*cy+gqz*cx-gqx*cz),uz=pz+2*(gqw*cz+gqx*cy-gqy*cx);
            return Compose(gx+ux,gy+uy,gz+uz,rx,ry,rz,rw,sx,sy,sz);
        }
        public void Apply(float x,float y,float z,out float ox,out float oy,out float oz){ox=M00*x+M01*y+M02*z+M03;oy=M10*x+M11*y+M12*z+M13;oz=M20*x+M21*y+M22*z+M23;}
    }

    /// <summary>Every mesh the projectile visuals draw. Primary bolt shapes are built at unit radius and scaled by the first radius.</summary>
    public enum NativeShotShape{Box,FireCone,WaterSphere,DarkIco,MagicIco,ArcadeSphere,Frag,Rocket,OrbitalMark,FlameLance,TidalWave,BallLightning,Shackles,Fallback,Ring,ModuleCone,Orb}
    public static class NativeShotShapes
    {
        public const int Count=(int)NativeShotShape.Orb+1;
        private static readonly NativeShotMesh[] meshes=new NativeShotMesh[Count];
        /// <summary>The source mesh for a shape, built once (renderer.ts ABILITY_SHOT and primary meshes, chip-effects.ts parts).</summary>
        public static NativeShotMesh Mesh(NativeShotShape shape)
        {
            int i=(int)shape;if(meshes[i]!=null)return meshes[i];
            NativeShotMesh m;
            switch(shape)
            {
                case NativeShotShape.FireCone:m=NativeShotGeometry.Cone(1.3,4,5);break;
                case NativeShotShape.WaterSphere:m=NativeShotGeometry.Sphere(1.4,8,5);break;
                case NativeShotShape.DarkIco:m=NativeShotGeometry.Icosahedron(1.8,1);break;
                case NativeShotShape.MagicIco:m=NativeShotGeometry.Icosahedron(1.4,0);break;
                case NativeShotShape.ArcadeSphere:m=NativeShotGeometry.Sphere(1,12,8);break;
                case NativeShotShape.Frag:m=NativeShotGeometry.Vox(new[]{0,0,0,.15,.17,.15},new[]{0,.11,0,.06,.05,.06},new[]{.06,.12,0,.08,.02,.02});break;
                case NativeShotShape.Rocket:m=NativeShotGeometry.Vox(new[]{0,0,0,.13,.13,.52},new[]{0,0,-.2,.32,.025,.12},new[]{0,0,-.2,.025,.32,.12},new[]{0,0,.29,.08,.08,.07});break;
                case NativeShotShape.OrbitalMark:m=NativeShotGeometry.Vox(new[]{0,0,0,.035,.035,.6});break;
                case NativeShotShape.FlameLance:m=NativeShotGeometry.Cone(.13,1.5,5).RotatedX(Math.PI/2);break;
                case NativeShotShape.TidalWave:m=NativeShotGeometry.Vox(new[]{0,0,0,2,.9,.35},new[]{0,.5,.12,1.8,.18,.3});break;
                case NativeShotShape.BallLightning:m=NativeShotGeometry.Vox(new[]{0,0,0,.42,.42,.42},new[]{.16,.12,-.1,.26,.26,.26},new[]{-.14,-.1,.14,.24,.24,.24});break;
                case NativeShotShape.Shackles:m=NativeShotGeometry.Icosahedron(.34,0);break;
                case NativeShotShape.Fallback:m=NativeShotGeometry.Icosahedron(.25,0);break;
                case NativeShotShape.Ring:m=NativeShotGeometry.Torus(1,.035,4,18);break;
                case NativeShotShape.ModuleCone:m=NativeShotGeometry.Cone(1,1,5);break;
                case NativeShotShape.Orb:m=NativeShotGeometry.Sphere(1,8,5);break;
                default:m=NativeShotGeometry.Box(1,1,1);break;
            }
            return meshes[i]=m;
        }
    }

    public enum NativeShotKind{Bullet,Ability,Melee}
    /// <summary>What the presentation knows about one live projectile this frame (host data or a replica's fallbacks).</summary>
    public struct NativeShotView
    {
        public NativeShotKind Kind;public int Id;
        public double X,Y,Z,VX,VY,VZ,Radius;
        /// <summary>Bullets and melee throws: the shot's spec. Ability shots: the owner's spec (source p.spec).</summary>
        public string Spec,Class;public int WeaponColor;
        /// <summary>Ability shots only: the ability id (null for an unknown or missing definition).</summary>
        public string Ability;
        /// <summary>Seconds until the frag fuse (1e9 when unknown, as the source's fuse??1e9).</summary>
        public double FuseLeft;
        /// <summary>The source element list [fire][poison] (attackElements order).</summary>
        public bool Fire,Poison;
        /// <summary>0 none, 1 Arcade red (owner holds arcade-red), 2 Arcade blue: the hadouken shots.</summary>
        public int Arcade;
        public bool SwordBeam,Bowling,Siren,Shield;
        /// <summary>Mods['velocity'] (Velocity Coil stacks).</summary>
        public int VelocityChips;
        /// <summary>Chip module inputs; read once, the first frame an id is seen.</summary>
        public NativeShotModules Modules;
        public int Elements=>(Fire?1:0)+(Poison?1:0);
    }
    /// <summary>The statsFor(p.mods) values chip-effects.ts reads, plus the raw velocity and meteor stacks.</summary>
    public struct NativeShotModules{public double Bounces,Homing,Split,Pierces,Impulse,Shrapnel,Pong,Boomerang,Zigzag,Vacuum,LifeSteal;public int Velocity,Meteor;}
    /// <summary>One opaque (A=1) or translucent part to draw: a shape, its world transform and its linear colour.</summary>
    public struct NativeShotDraw{public NativeShotShape Shape;public NativeShotAffine M;public float R,G,B,A;}

    /// <summary>Pure colour, timing and shape rules of the source projectile visuals (renderer.ts, projectile-glow.ts, chip-effects.ts).</summary>
    public static class NativeShotRules
    {
        public const int Fire=0xff822f,Poison=0x9de34d,Ranged=0xfff2b8,ArcadeRed=0xffc09c,ArcadeBlue=0xe2ffff,TrailRed=0xff6438,TrailBlue=0x5fd9ff,HaloRed=0xff692b,HaloBlue=0x43caff,Blink=0xff3020,RocketTrail=0xb9b2a6,RocketSmoke=0x8d8a85,ShackleSmoke=0x5a3480,White=0xffffff;
        public const double PuffSeconds=.035,EmitterRate=60;
        /// <summary>projectile-trails.ts budget: at most 120 live trails; later projectiles get none.</summary>
        public const int TrailLimit=120;
        /// <summary>
        /// The source emits its per-frame sparks and beams once per rendered frame. This steps a 60 Hz accumulator by the
        /// frame time and says whether this frame emits: every frame at 60 Hz and below (as the source there), about 60
        /// times a second on faster displays (the source would multiply the density).
        /// </summary>
        public static bool Emit(ref double clock,double dt)
        {
            const double step=1/EmitterRate;clock+=Math.Max(0,dt);if(clock<step-1e-9)return false;
            clock-=step;if(clock>step)clock=0;return true;
        }
        /// <summary>
        /// projectile-glow.ts lights: the indices of the count nearest projectiles, nearest first (a stable sort of the squared
        /// camera distances, then slice). Returns how many were written to output.
        /// </summary>
        public static int Nearest(IReadOnlyList<double> distances,int count,int[] output)
        {
            int n=Math.Min(Math.Min(count,output.Length),distances.Count);
            for(int i=0;i<n;i++)
            {
                int best=-1;
                for(int k=0;k<distances.Count;k++)
                {
                    bool taken=false;for(int j=0;j<i;j++)if(output[j]==k){taken=true;break;}
                    if(!taken&&(best<0||distances[k]<distances[best]))best=k;
                }
                output[i]=best;
            }
            return n;
        }
        /// <summary>chip-effects.ts statsFor(p.mods): the module inputs of a shot's chip stacks (no stacks: no parts).</summary>
        public static NativeShotModules ModulesOf(ConversionContent content,IReadOnlyDictionary<string,int> mods)
        {
            if(mods==null||mods.Count==0)return default;
            var s=NativeContentRules.Stats(content,null,mods);
            return new NativeShotModules{Bounces=s["bounces"],Homing=s["homing"],Split=s["split"],Pierces=s["pierces"],Impulse=s["impulse"],Shrapnel=s["shrapnel"],Pong=s["pong"],Boomerang=s["boomerang"],Zigzag=s["zigzag"],Vacuum=s["vacuum"],LifeSteal=s["lifeSteal"],
                Velocity=mods.TryGetValue("velocity",out int velocity)?velocity:0,Meteor=mods.TryGetValue("meteor",out int meteor)?meteor:0};
        }
        /// <summary>The active element, switching every 50 ms: elements[floor(t*20) mod n]. 0 none, 1 fire, 2 poison.</summary>
        public static int Element(in NativeShotView v,double time)
        {int n=v.Elements;if(n==0)return 0;if(n==1)return v.Fire?1:2;long k=(long)Math.Floor(time*20)%2;if(k<0)k+=2;return k==0?1:2;}
        public static int ElementColor(int element)=>element==1?Fire:Poison;
        /// <summary>ABILITY_SHOT: the mesh and fixed colour of an ability id (unknown ids: the white fallback icosahedron).</summary>
        public static NativeShotShape AbilityShape(string id,out int color)
        {
            switch(id)
            {
                case "frag-grenade":color=0x40633a;return NativeShotShape.Frag;case "rocket":color=0xd8d0c0;return NativeShotShape.Rocket;
                case "orbital-mark":color=0xff6a5c;return NativeShotShape.OrbitalMark;case "flame-lance":color=0xffc46b;return NativeShotShape.FlameLance;
                case "tidal-wave":color=0x7fdcff;return NativeShotShape.TidalWave;case "ball-lightning":color=0xd8b6ff;return NativeShotShape.BallLightning;
                case "shadow-shackles":color=0x7a45b0;return NativeShotShape.Shackles;default:color=White;return NativeShotShape.Fallback;
            }
        }
        /// <summary>The primary bolt mesh made once at creation (renderer.ts): arcade sphere, water, fire, darkness, other magic, else a box.</summary>
        public static NativeShotShape PrimaryShape(in NativeShotView v)
        {
            if(v.Spec=="ansatsuken")return NativeShotShape.ArcadeSphere;if(v.Spec=="water")return NativeShotShape.WaterSphere;if(v.Spec=="fire")return NativeShotShape.FireCone;
            if(v.Spec=="darkness")return NativeShotShape.DarkIco;if(v.Class=="magic")return NativeShotShape.MagicIco;return NativeShotShape.Box;
        }
        /// <summary>Mesh colour before the 30% lerp to white: arcade, active element, 0xfff2b8 for ranged, else the weapon colour.</summary>
        public static int MeshColor(in NativeShotView v,double time)
        {if(v.Arcade!=0)return v.Arcade==1?ArcadeRed:ArcadeBlue;int e=Element(v,time);if(e!=0)return ElementColor(e);return v.Class=="ranged"?Ranged:v.WeaponColor;}
        /// <summary>Frag fuse blink: red while left &lt; 3 s and sin(t·(10 + 30/(max(0,left)+.25))) &gt; .3.</summary>
        public static bool Blinks(double left,double time)=>left<3&&Math.Sin(time*(10+30/(Math.Max(0,left)+.25)))>.3;
        public static bool HasTrail(in NativeShotView v)=>!v.Siren&&!(v.Kind==NativeShotKind.Ability&&v.Ability=="tidal-wave");
        /// <summary>Trail colour (locked by the trail at its first sample): arcade, active element, weapon; ability table colours.</summary>
        public static int TrailColor(in NativeShotView v,double time)
        {
            if(v.Kind==NativeShotKind.Ability){AbilityShape(v.Ability,out int c);return v.Ability=="rocket"?RocketTrail:c;}
            if(v.Kind==NativeShotKind.Melee)return v.WeaponColor;
            if(v.Arcade!=0)return v.Arcade==1?TrailRed:TrailBlue;int e=Element(v,time);return e!=0?ElementColor(e):v.WeaponColor;
        }
        /// <summary>Trail width factor: primaries 1 + .28·Velocity Coils (+.45 elemental), rocket 2.2, thrown shield 3, thrown dagger 1.5.</summary>
        public static double TrailWidth(in NativeShotView v)
        {
            if(v.Kind==NativeShotKind.Ability)return v.Ability=="rocket"?2.2:1;if(v.Kind==NativeShotKind.Melee)return v.Shield?3:1.5;
            return 1+v.VelocityChips*.28+(v.Elements>0?.45:0);
        }
        /// <summary>Halo and spill-light colour (projectile-glow.ts): arcade, active element, then the weapon colour of p.spec.</summary>
        public static int HaloColor(in NativeShotView v,double time)
        {if(v.Arcade!=0)return v.Arcade==1?HaloRed:HaloBlue;int e=Element(v,time);return e!=0?ElementColor(e):v.WeaponColor;}
        /// <summary>three.js PointLight intensity: arcade 6, magic-class spec 5, else 2.8. Range: magic 5, else 3.8.</summary>
        public static double LightIntensity(in NativeShotView v)=>v.Arcade!=0?6:v.Class=="magic"?5:2.8;
        public static double LightRange(in NativeShotView v)=>v.Class=="magic"?5:3.8;
        /// <summary>Linear colour of a hex, optionally lerped 30% toward white in linear space (Color.lerp(white,.3)).</summary>
        public static void Linear(int hex,bool lerpWhite,out float r,out float g,out float b)
        {
            NativeBlastSim.Linear(hex,out double lr,out double lg,out double lb);
            if(lerpWhite){lr+=(1-lr)*.3;lg+=(1-lg)*.3;lb+=(1-lb)*.3;}
            r=(float)lr;g=(float)lg;b=(float)lb;
        }
        /// <summary>
        /// The chip-effects.ts parts for one projectile, in the source's build order. Positions, Euler XYZ angles and
        /// scales are the initial local values; Anim says how update() changes them every frame.
        /// </summary>
        public enum PartAnim:byte{None,Spin,Orbit,Flame,Bubble}
        public struct Part{public NativeShotShape Shape;public int Color;public double X,Y,Z,EX,EY,EZ,SX,SY,SZ;public PartAnim Anim;public int N;}
        private static void Add(List<Part> parts,NativeShotShape shape,int color,double x,double y,double z,double sx,double sy,double sz,double ex=0,double ey=0,double ez=0,PartAnim anim=PartAnim.None,int n=0)
        =>parts.Add(new Part{Shape=shape,Color=color,X=x,Y=y,Z=z,SX=sx,SY=sy,SZ=sz,EX=ex,EY=ey,EZ=ez,Anim=anim,N=n});
        public static void Modules(in NativeShotModules s,bool fire,bool poison,List<Part> parts)
        {
            parts.Clear();var p=parts;
            const NativeShotShape Ring=NativeShotShape.Ring,Cone=NativeShotShape.ModuleCone,Orb=NativeShotShape.Orb,Cube=NativeShotShape.Box;
            if(s.Bounces>0)for(int n=0;n<Math.Min(3,s.Bounces);n++)Add(p,Ring,0xffcc49,0,0,-.12-n*.15,.17,.17,.17);
            if(s.Homing>0)for(int n=0;n<3;n++)Add(p,Cone,0xf482d4,0,0,-.20,.06,.26,.06,-Math.PI/2,0,0,PartAnim.Orbit,n);
            if(s.Split>0)for(int side=-1;side<=1;side+=2)Add(p,Cube,0x96ffb7,side*.10,0,-.10,.035,.035,.36,0,side*.45);
            if(s.Pierces>0)Add(p,Cone,0xf3ffff,0,0,.15,.075,.6,.075,Math.PI/2);
            if(s.Velocity>0){Add(p,Cube,0x91f2ff,0,0,-.45,.035,.035,.9+.25*s.Velocity);for(int side=-1;side<=1;side+=2)Add(p,Cube,0x69d7ff,side*.10,0,-.4,.025,.025,.45,0,side*-.32);}
            if(s.Impulse>1.3)Add(p,Ring,0xffaa64,0,0,-.15,.23,.23,.23);
            if(s.Shrapnel>0)for(int n=0;n<3;n++)Add(p,Orb,0xff845c,Math.Cos(n*2.1)*.13,Math.Sin(n*2.1)*.13,-.15,.045,.045,.045);
            if(s.Pong>0){Add(p,Orb,0xff5269,0,0,0,.18,.18,.18);for(int n=0;n<3;n++)Add(p,Ring,0xffa6b0,0,0,0,.32,.32,.32,n*Math.PI/3,Math.PI/2,0,PartAnim.Spin,n+1);}
            if(s.Boomerang>0&&!(s.Pong>0))for(int side=-1;side<=1;side+=2)Add(p,Cube,0x7fffe0,side*.2,0,-.05,.32,.045,.09,0,side*.6);
            if(s.Zigzag>0&&!(s.Pong>0))for(int n=0;n<3;n++)Add(p,Ring,0x96b5ff,0,0,-n*.18,.15,.15,.15,0,0,0,PartAnim.Spin,n+1);
            if(s.Vacuum>0){Add(p,Orb,0x240b39,0,0,0,.22,.22,.22);Add(p,Ring,0xd58cff,0,0,0,.35,.35,.35,0,0,0,PartAnim.Spin,2);}
            if(s.Meteor>0)for(int n=0;n<6;n++)Add(p,Cone,0xffa050,Math.Sin(n)*.12,Math.Cos(n)*.12,-.3-n*.12,.15,.7,.15,-Math.PI/2,0,0,PartAnim.Flame,n);
            if(s.LifeSteal>0)Add(p,Ring,0xf273b5,0,0,-.1,.2,.2,.2);
            if(fire)for(int n=0;n<5;n++)Add(p,Cone,n%2==1?0xffdb75:0xff692a,Math.Sin(n*2.1)*.07,Math.Cos(n*2.1)*.07,-.22-n*.055,.08,.42,.08,-Math.PI/2,0,0,PartAnim.Flame,n);
            if(poison)for(int n=0;n<5;n++)Add(p,Orb,0x80ed75,Math.Sin(n*2.1)*.10,Math.Cos(n*2.1)*.10,-n*.12,.06,.06,.06,0,0,0,PartAnim.Bubble,n);
        }
        /// <summary>ChipEffects.update's per-frame changes to one part (spin, orbit, flame flicker, bubble orbit).</summary>
        public static Part Animate(Part p,double time)
        {
            switch(p.Anim)
            {
                case PartAnim.Spin:p.EZ=time*p.N*3;break;
                case PartAnim.Orbit:{double a=time*12+p.N*2.1;p.X=Math.Cos(a)*.15;p.Y=Math.Sin(a)*.15;break;}
                case PartAnim.Flame:p.SY=.3+Math.Sin(time*28+p.N)*.12;break;
                case PartAnim.Bubble:p.X=Math.Sin(time*7+p.N*2)*.12;p.Y=Math.Cos(time*7+p.N*2)*.12;break;
            }
            return p;
        }
    }
}
