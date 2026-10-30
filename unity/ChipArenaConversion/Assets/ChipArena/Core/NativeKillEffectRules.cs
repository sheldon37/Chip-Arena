using System;
namespace ChipArena.UnityConversion
{
    /// <summary>One box mesh of the source rig (NativeKillBoxes.Generated.cs): the rig bone it moves with, the ragdoll part
    /// root that owns it (-1 none), its socket zone (0 body, 1 weapon, 2 cosmetic), whether it is a detail, whether it (or an
    /// ancestor) is invisible, its colour tag (0 fixed, 1 body, 2 accent, 3 body edge, 4 body flex), fixed colour, linear
    /// emissive and its TRS local to the bone in source (three.js) axes. voxelBurst's traverse takes hidden boxes; the ragdoll's
    /// part copies, colliders, stump caps and crush fragments skip them.</summary>
    public readonly struct KillBox
    {
        public readonly string Name;public readonly int Bone,Root,Zone,Tag,Color;public readonly bool Detail,Hidden;
        public readonly float Er,Eg,Eb,Px,Py,Pz,Qx,Qy,Qz,Qw,Sx,Sy,Sz;
        public KillBox(string name,int bone,int root,int zone,bool detail,bool hidden,int tag,int color,float er,float eg,float eb,float px,float py,float pz,float qx,float qy,float qz,float qw,float sx,float sy,float sz)
        {Name=name;Bone=bone;Root=root;Zone=zone;Detail=detail;Hidden=hidden;Tag=tag;Color=color;Er=er;Eg=eg;Eb=eb;Px=px;Py=py;Pz=pz;Qx=qx;Qy=qy;Qz=qz;Qw=qw;Sx=sx;Sy=sy;Sz=sz;}
    }

    public static partial class NativeKillBoxes
    {
        public const int MaxRigBoxes=96;
        public static int SpecIndex(string spec)=>spec==null?-1:Array.IndexOf(Specs,spec);
        public static int HatIndex(string hat)=>hat==null?-1:Array.IndexOf(Hats,hat);
        /// <summary>This pilot's boxes in rig.group traversal order: the hat after the jaw, the off-hand weapon (then a caster's
        /// weapon, which magic-motion.ts moves to the left hand) after the left thumb, any other weapon after the right thumb.</summary>
        public static int Rig(string spec,string hat,KillBox[] into)
        {
            int s=SpecIndex(spec),h=HatIndex(hat),n=0;
            for(int i=0;i<=Body.Length;i++)
            {
                if(i==HatSlot&&h>=0)Append(HatBoxes[h],into,ref n);
                if(i==OffhandSlot&&s>=0){Append(Offhands[s],into,ref n);if(Magic[s])Append(Weapons[s],into,ref n);}
                if(i==WeaponSlot&&s>=0&&!Magic[s])Append(Weapons[s],into,ref n);
                if(i<Body.Length&&n<into.Length)into[n++]=Body[i];
            }
            return n;
        }
        private static void Append(KillBox[] list,KillBox[] into,ref int n){foreach(var b in list)if(n<into.Length)into[n++]=b;}
        /// <summary>team-identity.ts teamArmor: the four unlit team bands (chest, spine, left arm, right arm, in that order;
        /// NativeStatusVisuals.Band*). Each is added as the last child of its bone's group, so rig.group.traverse meets it right
        /// after that bone's subtree. Inserts them into a <see cref="Rig"/> list of <paramref name="n"/> boxes and returns the
        /// new count (a band that does not fit is left out). <paramref name="color"/>: the team colour, 0xRRGGBB.</summary>
        public static int Bands(KillBox[] into,int n,int color)
        {
            for(int k=0;k<NativeStatusVisuals.BandBones.Length&&n<into.Length;k++)
            {
                int bone=NativeStatusVisuals.BandBones[k],at=n;for(int i=0;i<n;i++)if(Within(into[i].Bone,bone))at=i+1;
                int root=bone;while(root>=0&&Array.IndexOf(PartRoots,root)<0)root=CapturedRig.Bones[root].Parent;
                var p=NativeStatusVisuals.BandPositions[k];var s=NativeStatusVisuals.BandSizes[k];
                for(int i=n;i>at;i--)into[i]=into[i-1];
                into[at]=new KillBox("team-identity",bone,root,0,false,false,0,color,0,0,0,p.X,p.Y,p.Z,0,0,0,1,s.X,s.Y,s.Z);n++;
            }
            return n;
        }
        /// <summary>The ragdoll part roots (rig bones), ragdoll.ts names.</summary>
        public static readonly int[] PartRoots={1,2,5,7,8,11,12,14,15,18,19};
        private static bool Within(int bone,int root){for(int b=bone;b>=0;b=CapturedRig.Bones[b].Parent)if(b==root)return true;return false;}
        /// <summary>CAP_MESH: the box a cut through this joint (rig bone) exposes, or null.</summary>
        public static string CapMesh(int joint){int k=Array.IndexOf(CapJoints,joint);return k<0?null:CapMeshes[k];}
        public static int Find(string name){for(int i=0;i<Body.Length;i++)if(Body[i].Name==name)return i;return -1;}
    }

    /// <summary>
    /// C18: what the source draws around a ragdoll kill (client/ragdoll.ts, ragdoll-vfx.ts, gibber-vfx.ts, body-trauma.ts and
    /// death-effects.ts voxelBurst), as pure, allocation-free rules in source (three.js) axes and linear colour. Per style:
    /// <list type="bullet">
    /// <item>Every ragdoll variant (impact-ragdoll, ragdoll, dev-gibber and the nine RAGDOLL_STYLES) sinks .35 m and shrinks to
    /// .85 over its last .65 s (fade = (5 - age) / .65); a PvE kept body holds min(age, 4.2).</item>
    /// <item>The nine styles add RagdollVfx (<see cref="NativeKillSwarm"/>): up to 16 additive points (6 at reduced effects,
    /// size .055 m, petals .10, size-attenuated), three additive torus rings (one white .025 tube, two style-coloured .01),
    /// the Singularity's black core, the Frostbreak / Firework / Singularity / Gravity collapse of every part and the Soul
    /// Drift (cyan translucent plain boxes) and Frostbreak (icy after 1.1 s) recolouring.</item>
    /// <item>dev-gibber and an impact sever / crush add GibberVfx (<see cref="NativeKillSpray"/>): 150/70/60 stretched blood
    /// drops (55/30/26 reduced) and up to ten splat disks, emitted on fixed 1/60 s steps from the moving stump.</item>
    /// <item>Severed and crushed joints get woundCap cross-sections (<see cref="NativeKillCaps"/>); a crushed part breaks into
    /// CrushDebris fragments (<see cref="NativeKillDebris"/>).</item>
    /// <item>voxel-burst hides the body and throws up to 120 (48 reduced) Lambert cubes cut from the rig's boxes
    /// (<see cref="NativeKillBurst"/>).</item>
    /// </list>
    /// Effects share a 280 piece budget (a kept body has its own 96); counts follow the source's order exactly.
    /// </summary>
    public static class NativeKillEffectRules
    {
        public const double DeathSeconds=5,KeepAge=4.2,Step=1/60.0;
        public const int DeathVoxels=120,ReducedDeathVoxels=48,MaxDeathVoxels=280,KeptBudget=96,Parts=11;
        public const double ArenaHalfWidth=32,ArenaHalfDepth=28;
        public static readonly string[] SwarmStyles={"ragdoll-ember","ragdoll-frost","ragdoll-static","ragdoll-gravity","ragdoll-pinball","ragdoll-fireworks","ragdoll-soul","ragdoll-petals","dev-singularity"};
        public static bool Swarm(string style){foreach(var s in SwarmStyles)if(s==style)return true;return false;}
        /// <summary>ragdoll.ts update(): the stock ending every ragdoll variant shares.</summary>
        public static double Fade(double age)=>Clamp((DeathSeconds-age)/.65);
        public static double Sink(double age){double f=Fade(age);return .35*(1-f)*(1-f);}
        public static double PartScale(double age)=>.85+.15*Fade(age);
        public static double Clamp(double v,double lo=0,double hi=1)=>Math.Min(hi,Math.Max(lo,v));
        /// <summary>shared/body-ragdoll.ts BODY_RAGDOLL.</summary>
        public const double MaxBodySpeed=24,MaxDetachedKick=10;
        /// <summary>bodyPartVelocity(motion, wound, destroyedPart, direction) for the destroyed part (a crushed part's fragments
        /// inherit it): the death velocity capped at 24 m/s plus min(10, momentum / part mass) along the blow, capped again.
        /// <paramref name="part"/> is the rig bone of the destroyed ragdoll root.</summary>
        public static void DetachedVelocity(double vx,double vy,double vz,int part,double momentum,double dx,double dy,double dz,out double ox,out double oy,out double oz)
        {
            Cap(ref vx,ref vy,ref vz,MaxBodySpeed);double mass=part==5?5:part==2?30:part==8||part==12||part==15||part==19?3:7,kick=Math.Min(MaxDetachedKick,momentum/mass);
            double l=Math.Sqrt(dx*dx+dy*dy+dz*dz),k=kick/(l==0?1:l);ox=vx+dx*k;oy=vy+dy*k;oz=vz+dz*k;Cap(ref ox,ref oy,ref oz,MaxBodySpeed);
        }
        private static void Cap(ref double x,ref double y,ref double z,double max){double l=Math.Sqrt(x*x+y*y+z*z);if(l>max){double k=max/l;x*=k;y*=k;z*=k;}}
        /// <summary>JavaScript Math.round (halves toward +infinity).</summary>
        public static double Round(double v)=>Math.Floor(v+.5);
        // ---- colour ------------------------------------------------------------------------------------------------
        public static double ToLinear(double c)=>c<0.04045?c*0.0773993808:Math.Pow(c*0.9478672986+0.0521327014,2.4);
        public static double ToSrgb(double c)=>c<0.0031308?c*12.92:1.055*Math.Pow(c,0.41666)-0.055;
        public static void Linear(int hex,out double r,out double g,out double b){r=ToLinear(((hex>>16)&255)/255.0);g=ToLinear(((hex>>8)&255)/255.0);b=ToLinear((hex&255)/255.0);}
        /// <summary>three.js Color.getHex of linear channels.</summary>
        public static int Hex(double r,double g,double b){int H(double c)=>(int)Round(Math.Max(0,Math.Min(255,ToSrgb(c)*255)));return H(r)*65536+H(g)*256+H(b);}
        /// <summary>character-rig.ts edgeOf / flexOf: the body colour's darker trim and joint colours.</summary>
        public static int EdgeOf(int body){Linear(body,out double r,out double g,out double b);Linear(0xb5c4ca,out double er,out double eg,out double eb);return Hex(r*.6+(er-r*.6)*.1,g*.6+(eg-g*.6)*.1,b*.6+(eb-b*.6)*.1);}
        public static int FlexOf(int body)
        {
            Linear(body,out double r,out double g,out double b);Linear(0x354853,out double fr,out double fg,out double fb);
            double cr=r*.08+(fr-r*.08)*.6,cg=g*.08+(fg-g*.08)*.6,cb=b*.08+(fb-b*.08)*.6;double Lum(double x,double y,double z)=>.2126*x+.7152*y+.0722*z;
            return Lum(cr,cg,cb)<Lum(r,g,b)*.6?Hex(cr,cg,cb):Hex(r*.45,g*.45,b*.45);
        }
        /// <summary>A rig box's material colour for this pilot.</summary>
        public static int BoxColor(in KillBox box,int body,int accent)=>box.Tag==1?body:box.Tag==2?accent:box.Tag==3?EdgeOf(body):box.Tag==4?FlexOf(body):box.Color;
        public static int ParseHex(string text,int fallback)
        {
            if(string.IsNullOrEmpty(text)||text.Length!=7||text[0]!='#')return fallback;int v=0;
            for(int i=1;i<7;i++){char c=text[i];int d=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(d<0)return fallback;v=v*16+d;}return v;
        }
        // ---- small three.js math, column-major 4x4 ---------------------------------------------------------------------
        public static void EulerXyz(double x,double y,double z,out double qx,out double qy,out double qz,out double qw)
        {
            double c1=Math.Cos(x/2),c2=Math.Cos(y/2),c3=Math.Cos(z/2),s1=Math.Sin(x/2),s2=Math.Sin(y/2),s3=Math.Sin(z/2);
            qx=s1*c2*c3+c1*s2*s3;qy=c1*s2*c3-s1*c2*s3;qz=c1*c2*s3+s1*s2*c3;qw=c1*c2*c3-s1*s2*s3;
        }
        public static void Compose(double[] m,double px,double py,double pz,double qx,double qy,double qz,double qw,double sx,double sy,double sz)
        {
            double x2=qx+qx,y2=qy+qy,z2=qz+qz,xx=qx*x2,xy=qx*y2,xz=qx*z2,yy=qy*y2,yz=qy*z2,zz=qz*z2,wx=qw*x2,wy=qw*y2,wz=qw*z2;
            m[0]=(1-(yy+zz))*sx;m[1]=(xy+wz)*sx;m[2]=(xz-wy)*sx;m[3]=0;m[4]=(xy-wz)*sy;m[5]=(1-(xx+zz))*sy;m[6]=(yz+wx)*sy;m[7]=0;
            m[8]=(xz+wy)*sz;m[9]=(yz-wx)*sz;m[10]=(1-(xx+yy))*sz;m[11]=0;m[12]=px;m[13]=py;m[14]=pz;m[15]=1;
        }
        public static void Multiply(double[] a,double[] b,double[] o)
        {
            for(int c=0;c<4;c++)for(int r=0;r<4;r++)o[c*4+r]=a[r]*b[c*4]+a[4+r]*b[c*4+1]+a[8+r]*b[c*4+2]+a[12+r]*b[c*4+3];
        }
        public static void Apply(double[] m,double x,double y,double z,out double ox,out double oy,out double oz)
        {double w=1/(m[3]*x+m[7]*y+m[11]*z+m[15]);ox=(m[0]*x+m[4]*y+m[8]*z+m[12])*w;oy=(m[1]*x+m[5]*y+m[9]*z+m[13])*w;oz=(m[2]*x+m[6]*y+m[10]*z+m[14])*w;}
        /// <summary>three.js Matrix4.invert (zero matrix when singular).</summary>
        public static void Invert(double[] m,double[] o)
        {
            double n11=m[0],n21=m[1],n31=m[2],n41=m[3],n12=m[4],n22=m[5],n32=m[6],n42=m[7],n13=m[8],n23=m[9],n33=m[10],n43=m[11],n14=m[12],n24=m[13],n34=m[14],n44=m[15];
            double t11=n23*n34*n42-n24*n33*n42+n24*n32*n43-n22*n34*n43-n23*n32*n44+n22*n33*n44,t12=n14*n33*n42-n13*n34*n42-n14*n32*n43+n12*n34*n43+n13*n32*n44-n12*n33*n44,
                t13=n13*n24*n42-n14*n23*n42+n14*n22*n43-n12*n24*n43-n13*n22*n44+n12*n23*n44,t14=n14*n23*n32-n13*n24*n32-n14*n22*n33+n12*n24*n33+n13*n22*n34-n12*n23*n34;
            double det=n11*t11+n21*t12+n31*t13+n41*t14;if(det==0){Array.Clear(o,0,16);return;}double d=1/det;
            o[0]=t11*d;o[1]=(n24*n33*n41-n23*n34*n41-n24*n31*n43+n21*n34*n43+n23*n31*n44-n21*n33*n44)*d;o[2]=(n22*n34*n41-n24*n32*n41+n24*n31*n42-n21*n34*n42-n22*n31*n44+n21*n32*n44)*d;o[3]=(n23*n32*n41-n22*n33*n41-n23*n31*n42+n21*n33*n42+n22*n31*n43-n21*n32*n43)*d;
            o[4]=t12*d;o[5]=(n13*n34*n41-n14*n33*n41+n14*n31*n43-n11*n34*n43-n13*n31*n44+n11*n33*n44)*d;o[6]=(n14*n32*n41-n12*n34*n41-n14*n31*n42+n11*n34*n42+n12*n31*n44-n11*n32*n44)*d;o[7]=(n12*n33*n41-n13*n32*n41+n13*n31*n42-n11*n33*n42-n12*n31*n43+n11*n32*n43)*d;
            o[8]=t13*d;o[9]=(n14*n23*n41-n13*n24*n41-n14*n21*n43+n11*n24*n43+n13*n21*n44-n11*n23*n44)*d;o[10]=(n12*n24*n41-n14*n22*n41+n14*n21*n42-n11*n24*n42-n12*n21*n44+n11*n22*n44)*d;o[11]=(n13*n22*n41-n12*n23*n41-n13*n21*n42+n11*n23*n42+n12*n21*n43-n11*n22*n43)*d;
            o[12]=t14*d;o[13]=(n13*n24*n31-n14*n23*n31+n14*n21*n33-n11*n24*n33-n13*n21*n34+n11*n23*n34)*d;o[14]=(n14*n22*n31-n12*n24*n31-n14*n21*n32+n11*n24*n32+n12*n21*n34-n11*n22*n34)*d;o[15]=(n12*n23*n31-n13*n22*n31+n13*n21*n32-n11*n23*n32-n12*n21*n33+n11*n22*n33)*d;
        }
        /// <summary>Object3D.getWorldScale (Matrix4.decompose): column lengths, x negated for a mirroring matrix.</summary>
        public static void Scale(double[] m,out double sx,out double sy,out double sz)
        {
            sx=Math.Sqrt(m[0]*m[0]+m[1]*m[1]+m[2]*m[2]);sy=Math.Sqrt(m[4]*m[4]+m[5]*m[5]+m[6]*m[6]);sz=Math.Sqrt(m[8]*m[8]+m[9]*m[9]+m[10]*m[10]);
            double det=m[0]*(m[5]*m[10]-m[6]*m[9])-m[4]*(m[1]*m[10]-m[2]*m[9])+m[8]*(m[1]*m[6]-m[2]*m[5]);if(det<0)sx=-sx;
        }
        /// <summary>A box's world matrix: bone world matrix times the box's local TRS.</summary>
        public static void BoxWorld(in KillBox box,double[] bone,double[] scratch,double[] o)
        {Compose(scratch,box.Px,box.Py,box.Pz,box.Qx,box.Qy,box.Qz,box.Qw,box.Sx,box.Sy,box.Sz);Multiply(bone,scratch,o);}
        /// <summary>Quaternion.setFromUnitVectors((0,1,0), v normalised).</summary>
        public static void FromUp(double vx,double vy,double vz,out double qx,out double qy,out double qz,out double qw)
        {
            double l=Math.Sqrt(vx*vx+vy*vy+vz*vz);if(l==0)l=1;vx/=l;vy/=l;vz/=l;double r=vy+1;
            if(r<1e-8){qx=0;qy=0;qz=1;qw=0;}else{qx=vz;qy=0;qz=-vx;qw=r;}
            double n=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(n==0){qx=qy=qz=0;qw=1;}else{qx/=n;qy/=n;qz/=n;qw/=n;}
        }
        // ---- budgets (ragdoll.ts and DeathEffects) ----------------------------------------------------------------
        /// <summary>RagdollVfx pieces for a ragdoll effect that received <paramref name="budget"/> (parts already taken).</summary>
        public static void SwarmCounts(string style,int budget,double quality,out int total,out int count,out int rings,out bool core)
        {
            bool sing=style=="dev-singularity";int reserve=sing?4:3;total=Math.Max(0,Math.Min(quality<.5?6:16,budget-reserve));
            count=total+Math.Min(reserve,Math.Max(0,budget-total));core=sing&&budget>=4;rings=count-total-(core?1:0);
        }
    }

    /// <summary>ragdoll-vfx.ts RagdollVfx: the point swarm, rings, Singularity core, part collapse and body recolour of the
    /// nine RAGDOLL_STYLES. Construct once per effect; <see cref="Update"/> is allocation-free.</summary>
    public sealed class NativeKillSwarm
    {
        public const int MaxPoints=16,MaxRings=4;
        private enum Kind{Ember,Frost,Static,Gravity,Pinball,Fireworks,Soul,Petals,Singularity}
        private readonly Kind kind;private readonly int seed;private readonly double burst;private readonly bool finish;
        public readonly string Style;public readonly int Total,Count,Rings,StyleColor;public readonly bool HasCore;public readonly double Size;
        /// <summary>Per point linear colour (rgb), per point position after <see cref="Update"/>.</summary>
        public readonly double[] Colors=new double[MaxPoints*3],Points=new double[MaxPoints*3];
        public readonly int[] RingColor=new int[MaxRings];public readonly double[] RingTube=new double[MaxRings];
        public readonly double[] RingScale=new double[MaxRings],RingOpacity=new double[MaxRings],RingEuler=new double[MaxRings*3];public readonly bool[] RingVisible=new bool[MaxRings];
        public double Opacity,CoreScale,Collapse=1,Cx,Cy,Cz;
        /// <summary>Body recolour: -1 keeps the material colour / emissive.</summary>
        public int BodyColor=-1,BodyEmissive=-1;public double BodyOpacity=1;
        public const int CoreColor=0x080011;
        public NativeKillSwarm(string style,int seed,int budget,double quality)
        {
            Style=style;this.seed=seed;int k=Array.IndexOf(NativeKillEffectRules.SwarmStyles,style);if(k<0)throw new ArgumentException("Not a RagdollVfx style: "+style);
            kind=(Kind)k;NativeKillEffectRules.SwarmCounts(style,budget,quality,out Total,out Count,out Rings,out HasCore);
            StyleColor=new[]{0xff762d,0x9feaff,0xffe667,0xa47eff,0x70ffc9,0xffa1e2,0x95dfed,0xff98c4,0xd7b4ff}[k];
            NativeKillEffectRules.Linear(StyleColor,out double r,out double g,out double b);
            for(int i=0;i<Total;i++)
            {
                double cr=r,cg=g,cb=b;if(kind==Kind.Fireworks||kind==Kind.Pinball)Hsl(i/(double)Math.Max(1,Total),.9,.68,out cr,out cg,out cb);
                Colors[i*3]=cr;Colors[i*3+1]=cg;Colors[i*3+2]=cb;
            }
            Size=kind==Kind.Petals?.10:.055;burst=kind==Kind.Frost?1.7:kind==Kind.Fireworks?1.6:kind==Kind.Singularity?2.4:kind==Kind.Gravity?1.8:.6;
            finish=kind==Kind.Frost||kind==Kind.Fireworks||kind==Kind.Singularity||kind==Kind.Gravity;
            for(int i=0;i<Rings;i++){RingTube[i]=i==0?.025:.01;RingColor[i]=i==0?0xffffff:StyleColor;}
        }
        // three.js Color.setHSL into the (linear) working colour space: the HSL triple is taken as linear rgb.
        private static void Hsl(double h,double s,double l,out double r,out double g,out double b)
        {
            h=((h%1)+1)%1;s=NativeKillEffectRules.Clamp(s);l=NativeKillEffectRules.Clamp(l);if(s==0){r=g=b=l;return;}
            double p=l<=.5?l*(1+s):l+s-l*s,q=2*l-p;
            double Hue(double P,double Q,double t){if(t<0)t+=1;if(t>1)t-=1;if(t<1/6.0)return P+(Q-P)*6*t;if(t<1/2.0)return Q;if(t<2/3.0)return P+(Q-P)*6*(2/3.0-t);return P;}
            r=Hue(q,p,h+1/3.0);g=Hue(q,p,h);b=Hue(q,p,h-1/3.0);
        }
        /// <summary>RagdollVfx.update(age, centre, parts): <paramref name="parts"/> holds the presented part positions (x,y,z
        /// in the ragdoll's part order).</summary>
        public void Update(double age,double cx,double cy,double cz,double[] parts,int partCount)
        {
            Cx=cx;Cy=cy;Cz=cz;double t=Math.Max(0,age-burst);Collapse=finish?Math.Max(0,1-t*5):1;
            if(kind==Kind.Soul){BodyOpacity=Math.Max(0,.72-age*.12);BodyColor=0x91dcec;BodyEmissive=-1;}
            else if(kind==Kind.Frost&&age>1.1){BodyColor=0xbdefff;BodyEmissive=0x244655;BodyOpacity=1;}
            else{BodyColor=BodyEmissive=-1;BodyOpacity=1;}
            for(int i=0;i<Total;i++)
            {
                double angle=i*2.39996+seed*.001,rad=.45+(i%5)*.08,phase=(age*(kind==Kind.Ember?1.2:.7)+i/(double)Total)%1,x=0,y=0,z=0;
                if(kind==Kind.Ember){x=Math.Cos(angle)*rad*phase;y=phase*2;z=Math.Sin(angle)*rad*phase;}
                else if(kind==Kind.Soul){x=Math.Cos(angle+age*2)*rad;y=phase*2.5;z=Math.Sin(angle+age*2)*rad;}
                else if(kind==Kind.Gravity||kind==Kind.Singularity){double radius=age<burst?(kind==Kind.Singularity?1.7:1.1)*(1-phase):t*5;x=Math.Cos(angle-age*7)*radius;z=Math.Sin(angle-age*7)*radius;y=Math.Sin(angle*2)*radius*.6;}
                else if(kind==Kind.Static){if(partCount>0){int p=i%partCount;x=parts[p*3]-cx+Math.Sin(age*90+i)*.12;y=parts[p*3+1]-cy;z=parts[p*3+2]-cz+Math.Cos(age*80+i)*.12;}}
                else{double travel=finish?t:phase,reach=kind==Kind.Fireworks?4:1.7;x=Math.Cos(angle)*travel*reach;z=Math.Sin(angle)*travel*reach;y=kind==Kind.Petals?Math.Sin(angle+age*2)*.15:travel*(1+(i%4)*.4)-travel*travel*2;}
                Points[i*3]=cx+x;Points[i*3+1]=cy+y;Points[i*3+2]=cz+z;
            }
            Opacity=finish?(age<burst?.45:Math.Max(0,1-t*.7)):Math.Min(1,(5-age)/.7)*.85;
            CoreScale=HasCore?(age<burst?Math.Min(.65,Math.Max(0,age-.5)*.45):Math.Max(0,.65-t*3)):0;
            for(int i=0;i<Rings;i++)
            {
                double scale=.4,opacity=0,rx=RingEuler[i*3],ry=RingEuler[i*3+1],rz=RingEuler[i*3+2];
                if(kind==Kind.Singularity){scale=(age<burst?1.15+Math.Sin(age*3+i)*.12:1.2+t*5)*(1+i*.15);rx=age*(i+1)*.4;ry=age*(i%2==1?-.7:.5);rz=age+i;opacity=age<burst?.65:Math.Max(0,1-t*1.8);}
                else if(kind==Kind.Gravity){scale=Math.Max(.03,1.1-age*.3);rx=Math.PI/2+i*.65;ry=age*1.4;rz=0;opacity=age<burst?.6:Math.Max(0,1-t*4);}
                else if(kind==Kind.Static){rx=age*4+i;ry=age*6+i;rz=0;scale=.65+i*.14;opacity=(Math.Sin(age*35+i)>.65?.7:.06)*Math.Max(0,1-age/3);}
                else if(kind==Kind.Pinball){double pulse=(age+.15*i)%.85;rx=Math.PI/2;scale=.35+pulse*2;opacity=Math.Max(0,1-pulse/.65)*.6;}
                else if(finish){rx=i*Math.PI/3;ry=age*.3;rz=0;scale=.3+t*3;opacity=age>=burst?Math.Max(0,1-t*2):0;}
                RingScale[i]=scale;RingOpacity[i]=opacity;RingVisible[i]=opacity>.001;RingEuler[i*3]=rx;RingEuler[i*3+1]=ry;RingEuler[i*3+2]=rz;
            }
        }
    }

    public enum KillSprayProfile{Gibber,Sever,Crush}
    /// <summary>gibber-vfx.ts GibberVfx: the blood fountain from a stump, stepped at 1/60 s from the moving joint, and the
    /// splat disks where drops land. Construct once per effect; <see cref="Step"/> and <see cref="Update"/> do not allocate.</summary>
    public sealed class NativeKillSpray
    {
        public const int MaxDrops=150,MaxPools=10;
        public const int MaterialGibber=0xc10b25,PoolColor=0x780516;
        public readonly KillSprayProfile Profile;public readonly int Drops,Pools,Count;public readonly double Floor;
        /// <summary>How many drops have landed so far (each wrote pool slot n % Pools): the persistent blood splats read them.</summary>
        public int Splatted=>splatCursor;
        /// <summary>The impact profiles use the glowing wound material (WoundMaterial .3) and .6 size drops.</summary>
        public bool Glow=>Profile!=KillSprayProfile.Gibber;public double SizeScale=>Profile==KillSprayProfile.Gibber?1:.6;
        private uint seed;private int cursor,splatCursor;private double pending;
        public readonly double[] Pos=new double[MaxDrops*3],Vel=new double[MaxDrops*3],Life=new double[MaxDrops],Sizes=new double[MaxDrops];
        public readonly bool[] PoolVisible=new bool[MaxPools];public readonly double[] PoolPos=new double[MaxPools*3],PoolScale=new double[MaxPools*2],PoolRz=new double[MaxPools];
        /// <summary>After <see cref="Update"/>: drop i is drawn when Shown[i] (position Pos, rotation Q, scale S).</summary>
        public readonly bool[] Shown=new bool[MaxDrops];public readonly double[] Q=new double[MaxDrops*4],S=new double[MaxDrops*3];public double PoolOpacity;
        /// <summary><paramref name="gibberFloor"/>: where the Gibber's fountain splats (the source's 0; the kill-effect floor
        /// under the body on maps with upper floors, §5.5).</summary>
        public NativeKillSpray(int seed,int budget,double quality,KillSprayProfile profile,double floor,double gibberFloor=0)
        {
            Profile=profile;Floor=profile==KillSprayProfile.Gibber?gibberFloor:floor;this.seed=unchecked((uint)seed);
            int full=profile==KillSprayProfile.Gibber?150:profile==KillSprayProfile.Sever?70:60,low=profile==KillSprayProfile.Gibber?55:profile==KillSprayProfile.Sever?30:26;
            Drops=Math.Min(quality<.5?low:full,Math.Max(0,budget-10));Pools=Math.Max(0,Math.Min(10,budget-Drops));Count=Drops+Pools;
            for(int i=0;i<MaxPools;i++){PoolScale[i*2]=PoolScale[i*2+1]=1;}
        }
        /// <summary>The colour of drop i: the material colour times the instance colour (linear product).</summary>
        public void DropColor(int i,out double r,out double g,out double b)
        {
            NativeKillEffectRules.Linear(i%3!=0?0xc91428:0x750516,out r,out g,out b);
            if(Profile==KillSprayProfile.Gibber){NativeKillEffectRules.Linear(MaterialGibber,out double mr,out double mg,out double mb);r*=mr;g*=mg;b*=mb;}
        }
        private double Random(){seed=unchecked(seed*1664525u+1013904223u);return seed/4294967296.0;}
        private double Rate(double age)=>Profile==KillSprayProfile.Gibber?(age<.55?210:age<1.8?85:36):Profile==KillSprayProfile.Sever?(age<.35?150:age<1.2?36:0):(age<.18?300:age<.8?22:0);
        private double Speed(double age)=>Profile==KillSprayProfile.Gibber?(age<.55?4.8:2.4):Profile==KillSprayProfile.Sever?(age<.35?4:2):(age<.18?5:1.8);
        private double Stop=>Profile==KillSprayProfile.Gibber?3.6:Profile==KillSprayProfile.Sever?1.2:.8;
        /// <summary>GibberVfx.step(age, point, normal) at a fixed 1/60 s step.</summary>
        public void Step(double age,double px,double py,double pz,double nx,double ny,double nz)
        {
            const double dt=1/60.0;
            if(age<Stop)
            {
                pending+=dt*Rate(age);
                while(pending>=1&&Drops>0)
                {
                    pending--;int d=cursor++%Drops;double speed=Speed(age)+Random()*3;Pos[d*3]=px;Pos[d*3+1]=py;Pos[d*3+2]=pz;
                    double vx=(Random()-.5)*3,vy=1.8+Random()*2,vz=(Random()-.5)*3;Vel[d*3]=nx*speed+vx;Vel[d*3+1]=ny*speed+vy;Vel[d*3+2]=nz*speed+vz;
                    Life[d]=.6+Random()*.5;Sizes[d]=.025+Random()*.035;
                }
            }
            for(int d=0;d<Drops;d++)
            {
                if(Life[d]<=0)continue;Life[d]-=dt;Vel[d*3+1]-=15*dt;Pos[d*3]+=Vel[d*3]*dt;Pos[d*3+1]+=Vel[d*3+1]*dt;Pos[d*3+2]+=Vel[d*3+2]*dt;
                if(Pos[d*3+1]<Floor+.025)
                {
                    Life[d]=0;int slot=splatCursor++;
                    if(Pools>0){int p=slot%Pools;PoolVisible[p]=true;PoolPos[p*3]=Pos[d*3];PoolPos[p*3+1]=Floor+.012+(splatCursor%4)*.002;PoolPos[p*3+2]=Pos[d*3+2];PoolScale[p*2]=.08+Random()*.22;PoolScale[p*2+1]=.06+Random()*.16;PoolRz[p]=Random()*Math.PI;}
                }
            }
        }
        /// <summary>GibberVfx.update(age): drop matrices (stretched along their velocity) and the splat opacity.</summary>
        public void Update(double age)
        {
            double fade=NativeKillEffectRules.Fade(age);
            for(int d=0;d<Drops;d++)
            {
                double vx=Vel[d*3],vy=Vel[d*3+1],vz=Vel[d*3+2],speed=Math.Sqrt(vx*vx+vy*vy+vz*vz),k=Life[d]>0?fade*SizeScale:0;
                NativeKillEffectRules.FromUp(vx,vy,vz,out Q[d*4],out Q[d*4+1],out Q[d*4+2],out Q[d*4+3]);
                S[d*3]=Sizes[d]*k;S[d*3+1]=Sizes[d]*(1+Math.Min(4,speed*.2))*k;S[d*3+2]=Sizes[d]*k;Shown[d]=k>0&&Sizes[d]>0;
            }
            PoolOpacity=.85*fade;
        }
    }

    public struct KillCell{public double X,Y,Z,Qx,Qy,Qz,Qw,Sx,Sy,Sz,R,G,B;}
    /// <summary>body-trauma.ts woundCap and faceAlong: the voxel cross-section of a severed or crushed joint (local XZ plane,
    /// +Y out of the body) and where a part's geometry ends along its bone.</summary>
    public static class NativeKillCaps
    {
        public const int Shallow=0xa8141f,Mid=0x780a14,Deep=0x4f0610,Bone=0xe8dfcd,Marrow=0x7d2a22;
        public const int MaxCells=256;
        /// <summary>woundCap(): fills <paramref name="cells"/> and returns the count. bone: 0 centre, 1 back, 2 none.</summary>
        public static int Cap(double width,double depth,bool ragged,uint seed,double sr,double sg,double sb,int bone,bool low,KillCell[] cells)
        {
            double H(int a,int b,int c=0)=>NativeWoundCarve.Hash(seed,a,b,c);
            double g=Math.Min(low?.034:.025,Math.Min(width,depth)/(low?5:7));int nx=Math.Max(2,(int)NativeKillEffectRules.Round(width/g)),nz=Math.Max(2,(int)NativeKillEffectRules.Round(depth/g));
            double bx=0,bz=bone==1?-depth*.28:0,boneHalf=Math.Max(g*.55,Math.Min(width,depth)*.16);int n=0;
            for(int i=0;i<nx;i++)for(int k=0;k<nz;k++)
            {
                double x=(i+.5-nx/2.0)*(width/nx),z=(k+.5-nz/2.0)*(depth/nz),h=H(i,k);bool edge=i==0||k==0||i==nx-1||k==nz-1,corner=(i==0||i==nx-1)&&(k==0||k==nz-1);
                if(corner&&(ragged||h<.5))continue;if(ragged&&edge&&h<.3)continue;if(n>=cells.Length)return n;
                bool isBone=bone!=2&&Math.Abs(x-bx)<=boneHalf&&Math.Abs(z-bz)<=boneHalf,rim=edge&&ragged&&h>.55;double r,gg,b;
                if(rim){double s=.7+.15*h;r=sr*s;gg=sg*s;b=sb*s;}
                else if(isBone)NativeKillEffectRules.Linear(Math.Abs(x-bx)<boneHalf*.5&&Math.Abs(z-bz)<boneHalf*.5&&!ragged?Marrow:Bone,out r,out gg,out b);
                else{NativeKillEffectRules.Linear(h<.45?Shallow:h<.8?Mid:Deep,out r,out gg,out b);double s=.88+.24*H(k,i,3);r*=s;gg*=s;b*=s;}
                double height=ragged?(rim?.03+.02*h:.012+.035*H(i,k,5)):(isBone?.02:.014);
                cells[n++]=new KillCell{X=x,Z=z,Y=height/2,Sx=width/nx,Sy=height,Sz=depth/nz,R=r,G=gg,B=b};
            }
            if(ragged&&bone!=2)for(int s=0;s<(low?1:3)&&n<cells.Length;s++)
            {
                double a=H(s,9)*Math.PI*2,rr=boneHalf*.6*H(s,10),h=.04+.05*H(s,11);NativeKillEffectRules.Linear(Bone,out double r,out double gg,out double b);
                cells[n++]=new KillCell{X=bx+Math.Cos(a)*rr,Z=bz+Math.Sin(a)*rr,Y=h/2,Sx=g*.45,Sy=h,Sz=g*.45,R=r,G=gg,B=b};
            }
            double gap=ragged?.96:1;
            for(int i=0;i<n;i++)
            {
                double tilt=ragged&&cells[i].Sx<g*.5?.35:0;NativeKillEffectRules.EulerXyz(tilt*(H(i,12)-.5)*2,0,tilt*(H(i,13)-.5)*2,out cells[i].Qx,out cells[i].Qy,out cells[i].Qz,out cells[i].Qw);
                cells[i].Sx*=gap;cells[i].Sz*=gap;
            }
            return n;
        }
        /// <summary>faceAlong(): the distance along <c>bone</c> from the joint to the end face of the part whose unit-cube
        /// meshes have the inverse world matrices <paramref name="inverses"/> (16 doubles each).</summary>
        public static double FaceAlong(double jx,double jy,double jz,double bx,double by,double bz,double[] inverses,int count,bool parent,double reach=.14)
        {
            if(count==0)return 0;const double step=.004;double side=parent?1:-1;bool Inside(double s)
            {
                double px=jx+bx*s,py=jy+by*s,pz=jz+bz*s;
                for(int m=0;m<count;m++){int o=m*16;double x=inverses[o]*px+inverses[o+4]*py+inverses[o+8]*pz+inverses[o+12],y=inverses[o+1]*px+inverses[o+5]*py+inverses[o+9]*pz+inverses[o+13],z=inverses[o+2]*px+inverses[o+6]*py+inverses[o+10]*pz+inverses[o+14],w=inverses[o+3]*px+inverses[o+7]*py+inverses[o+11]*pz+inverses[o+15];x/=w;y/=w;z/=w;if(Math.Abs(x)<=.5&&Math.Abs(y)<=.5&&Math.Abs(z)<=.5)return true;}
                return false;
            }
            bool start=Inside(0);double dir=start?side:-side;
            for(int n=1;n*step<=reach;n++){double s=dir*n*step;if(Inside(s)!=start)return s-dir*step/2;}
            return 0;
        }
    }

    /// <summary>
    /// The surface height under a source-axes point, or NaN where there is none (docs/arenas/ARENA_PROPOSAL.md §5.5 Gibs). The
    /// runtime answers it with a raycast down through the map's static geometry
    /// (NativeStructureFxPresentation.KillFloor), so gibs settle on upper floors instead of the flat y .05 floor.
    /// </summary>
    public delegate double NativeKillFloor(double x,double y,double z);
    /// <summary>death-effects.ts voxelBurst: the rig's boxes cut into ~19 cm cubes, an even pick of them thrown out from the
    /// body; pure in age. Construct once per death. With a <see cref="NativeKillFloor"/> each cube rests .05 m above the surface
    /// under it, or under where it lands when that is lower (it falls off the ledge); without one, or where it answers NaN, the
    /// source's flat floor (.05 inside the 64 x 56 arena, -8 outside).</summary>
    public sealed class NativeKillBurst
    {
        public readonly int Count;private readonly double[] start,vel,spin,floor,size;public readonly int[] Colors;
        /// <summary><paramref name="boxes"/> holds world matrices (16 doubles each, unit cubes) in rig traversal order.</summary>
        public NativeKillBurst(double[] boxes,int[] boxColors,int boxCount,int seed,double px,double py,double pz,double vx,double vy,double vz,double dx,double dy,double dz,double quality,int budget,NativeKillFloor floorAt=null)
        {
            double[] cx=null,cs=null;int[] cc=null;
            // Two passes: count the candidates, then cut them (no cap, so the even pick matches the source for any rig).
            var m=new double[16];int n=0;
            for(int pass=0;pass<2;pass++)
            {
                if(pass==1){cx=new double[n*3];cs=new double[n];cc=new int[n];n=0;}
                for(int b=0;b<boxCount;b++)
                {
                    Array.Copy(boxes,b*16,m,0,16);NativeKillEffectRules.Scale(m,out double sx,out double sy,out double sz);
                    int ax=Math.Max(1,(int)Math.Ceiling(sx/.19)),ay=Math.Max(1,(int)Math.Ceiling(sy/.19)),az=Math.Max(1,(int)Math.Ceiling(sz/.19));
                    if(pass==0){n+=ax*ay*az;continue;}
                    double piece=Math.Min(.2,Math.Max(.07,Math.Min(sx/ax,Math.Min(sy/ay,sz/az))));
                    for(int x=0;x<ax;x++)for(int y=0;y<ay;y++)for(int z=0;z<az;z++)
                    {NativeKillEffectRules.Apply(m,(x+.5)/ax-.5,(y+.5)/ay-.5,(z+.5)/az-.5,out cx[n*3],out cx[n*3+1],out cx[n*3+2]);cs[n]=piece;cc[n]=boxColors[b];n++;}
                }
            }
            int limit=Math.Min(budget,quality<.5?NativeKillEffectRules.ReducedDeathVoxels:NativeKillEffectRules.DeathVoxels);Count=Math.Max(0,Math.Min(limit,n));
            start=new double[Count*3];vel=new double[Count*3];spin=new double[Count*3];floor=new double[Count];size=new double[Count];Colors=new int[Count];
            uint s=unchecked((uint)seed);double Random(){s=unchecked(s*1664525u+1013904223u);return s/4294967296.0;}
            double vl=Math.Sqrt(vx*vx+vy*vy+vz*vz),k=Math.Max(0,Math.Min(7,vl))/(vl==0?1:vl)*.35;double ix=vx*k,iy=vy*k,iz=vz*k;
            for(int i=0;i<Count;i++)
            {
                int c=(int)Math.Floor(i*(double)n/Count);double x=cx[c*3],y=cx[c*3+1],z=cx[c*3+2];
                start[i*3]=x;start[i*3+1]=y;start[i*3+2]=z;
                double rx=(Random()-.5)*3,ry=2+Random()*3,rz=(Random()-.5)*3;
                vel[i*3]=(x-px)*3.5+rx+ix+dx*2;vel[i*3+1]=(y-py)*3.5+ry+iy+dy*2;vel[i*3+2]=(z-pz)*3.5+rz+iz+dz*2;
                spin[i*3]=Random()*9-4.5;spin[i*3+1]=Random()*9-4.5;spin[i*3+2]=Random()*9-4.5;
                floor[i]=Math.Abs(x)<NativeKillEffectRules.ArenaHalfWidth&&Math.Abs(z)<NativeKillEffectRules.ArenaHalfDepth?.05:-8;size[i]=cs[c];Colors[i]=cc[c];
                if(floorAt!=null)
                {
                    double under=floorAt(x,y,z);if(double.IsNaN(under))continue;double f=under+.05,vy0=vel[i*3+1],hit=(vy0+Math.Sqrt(Math.Max(0,vy0*vy0+2*12*Math.Max(0,y-f))))/12;
                    double there=floorAt(x+vel[i*3]*hit,Math.Min(y,f+1),z+vel[i*3+2]*hit);floor[i]=!double.IsNaN(there)&&there+.05<f-.3?there+.05:f;
                }
            }
        }
        /// <summary>Chunk <paramref name="i"/> at <paramref name="age"/>: position, Euler XYZ rotation and uniform scale.</summary>
        public void Pose(int i,double age,out double x,out double y,out double z,out double ex,out double ey,out double ez,out double scale)
        {
            double t=Math.Max(0,age),sx=start[i*3],sy=start[i*3+1],sz=start[i*3+2],vx=vel[i*3],vy=vel[i*3+1],vz=vel[i*3+2],f=floor[i];
            double hit=(vy+Math.Sqrt(Math.Max(0,vy*vy+2*12*Math.Max(0,sy-f))))/12,travel=Math.Min(t,hit)+Math.Max(0,t-hit)*.1;
            x=sx+vx*travel;z=sz+vz*travel;y=t<hit?sy+vy*t-6*t*t:f+size[i]*.5+Math.Max(0,Math.Sin(Math.Min(Math.PI,(t-hit)*7)))*.15;
            double turn=Math.Min(t,hit+.3);ex=spin[i*3]*turn;ey=spin[i*3+1]*turn;ez=spin[i*3+2]*turn;
            scale=size[i]*.92*Math.Min(1,Math.Max(0,(NativeKillEffectRules.DeathSeconds-t)/.7));
        }
    }

    /// <summary>body-trauma.ts CrushDebris: a crushed part's boxes cut into ~7.5 cm fragments (a third show the flesh inside)
    /// thrown along the blow, plus loose objects it carried; pure in age.</summary>
    public sealed class NativeKillDebris
    {
        public readonly int Count,Chunks,Loose;private readonly double[] start,vel,spin,size;public readonly double[] Colors;
        private readonly double[] looseStart,looseVel,looseSpin;private readonly double floor;
        /// <summary><paramref name="sources"/>: world matrices (16 doubles each) and linear colours (3 each) of the crushed
        /// part's meshes; <paramref name="loose"/>: the start positions (3 each) of the objects it carried.</summary>
        public NativeKillDebris(double[] sources,double[] sourceColors,int sourceCount,double[] loose,int looseCount,double ox,double oy,double oz,double dx,double dy,double dz,double ix,double iy,double iz,double kick,double severity,uint seed,int limit,double floor)
        {
            this.floor=floor;double dl=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(dl*dl<1e-6){dx=0;dy=0;dz=1;dl=1;}dx/=dl;dy/=dl;dz/=dl;
            // Two passes (count, then cut), so any number of source boxes keeps the source's even pick.
            double[] cp=null,csz=null;int[] cc=null;int n=0;var m=new double[16];
            for(int pass=0;pass<2;pass++)
            {
                if(pass==1){cp=new double[n*3];csz=new double[n];cc=new int[n];n=0;}
                for(int b=0;b<sourceCount;b++)
                {
                    Array.Copy(sources,b*16,m,0,16);NativeKillEffectRules.Scale(m,out double sx,out double sy,out double sz);
                    int ax=Math.Max(1,Math.Min(4,(int)NativeKillEffectRules.Round(sx/.075))),ay=Math.Max(1,Math.Min(4,(int)NativeKillEffectRules.Round(sy/.075))),az=Math.Max(1,Math.Min(4,(int)NativeKillEffectRules.Round(sz/.075)));
                    if(pass==0){n+=ax*ay*az;continue;}
                    double piece=Math.Min(.085,Math.Max(.04,Math.Min(sx/ax,Math.Min(sy/ay,sz/az))));
                    for(int x=0;x<ax;x++)for(int y=0;y<ay;y++)for(int z=0;z<az;z++){NativeKillEffectRules.Apply(m,(x+.5)/ax-.5,(y+.5)/ay-.5,(z+.5)/az-.5,out cp[n*3],out cp[n*3+1],out cp[n*3+2]);csz[n]=piece;cc[n]=b;n++;}
                }
            }
            Loose=Math.Min(looseCount,Math.Max(0,limit));Chunks=Math.Max(0,Math.Min(limit-Loose,n));double sev=NativeKillEffectRules.Clamp(severity);
            start=new double[Chunks*3];vel=new double[Chunks*3];spin=new double[Chunks*3];size=new double[Chunks];Colors=new double[Chunks*3];
            for(int i=0;i<Chunks;i++)
            {
                int c=(int)Math.Floor(i*(double)n/Chunks);double H(int k)=>NativeWoundCarve.Hash(seed,i,k);double h0=H(0),r,g,b;
                if(h0<.22){NativeKillEffectRules.Linear(NativeKillCaps.Shallow,out r,out g,out b);double s=.8+.3*H(1);r*=s;g*=s;b*=s;}
                else if(h0<.3)NativeKillEffectRules.Linear(NativeKillCaps.Bone,out r,out g,out b);
                else if(h0<.36)NativeKillEffectRules.Linear(NativeKillCaps.Mid,out r,out g,out b);
                else{double s=.75+.3*H(1);r=sourceColors[cc[c]*3]*s;g=sourceColors[cc[c]*3+1]*s;b=sourceColors[cc[c]*3+2]*s;}
                Colors[i*3]=r;Colors[i*3+1]=g;Colors[i*3+2]=b;
                double x=cp[c*3],y=cp[c*3+1],z=cp[c*3+2],qx=x-ox,qy=Math.Max(y-oy,0),qz=z-oz;if(qx*qx+qy*qy+qz*qz<1e-6){qx=H(2)-.5;qy=.5;qz=H(3)-.5;}
                double ql=Math.Sqrt(qx*qx+qy*qy+qz*qz);qx/=ql;qy/=ql;qz/=ql;double push=(2+2.6*H(4))*(.6+.6*sev),along=kick*(.35+.55*H(5));
                vel[i*3]=ix+qx*push+dx*along+(H(6)-.5)*1.4;vel[i*3+1]=iy+qy*push+dy*along+1.2+1.8*H(7);vel[i*3+2]=iz+qz*push+dz*along+(H(8)-.5)*1.4;
                spin[i*3]=H(9)*14-7;spin[i*3+1]=H(10)*14-7;spin[i*3+2]=H(11)*14-7;size[i]=csz[c]*(.7+.4*H(12));start[i*3]=x;start[i*3+1]=y;start[i*3+2]=z;
            }
            looseStart=new double[Loose*3];looseVel=new double[Loose*3];looseSpin=new double[Loose*3];
            for(int i=0;i<Loose;i++)
            {
                double H(int k)=>NativeWoundCarve.Hash(seed,i,20+k);Array.Copy(loose,i*3,looseStart,i*3,3);
                looseVel[i*3]=ix+dx*kick*.55+(H(0)-.5)*1.6;looseVel[i*3+1]=iy+dy*kick*.55+2.4+1.4*H(1);looseVel[i*3+2]=iz+dz*kick*.55+(H(2)-.5)*1.6;
                looseSpin[i*3]=H(3)*8-4;looseSpin[i*3+1]=H(4)*8-4;looseSpin[i*3+2]=H(5)*8-4;
            }
            Count=Chunks+Loose;
        }
        private static bool Ballistic(double sx,double sy,double sz,double vx,double vy,double vz,double floor,double t,out double x,out double y,out double z,out double hit)
        {
            const double gravity=15;hit=(vy+Math.Sqrt(Math.Max(0,vy*vy+2*gravity*Math.Max(0,sy-floor))))/gravity;double flight=Math.Min(t,hit),slide=Math.Max(0,t-hit),travel=flight+(1-Math.Exp(-slide*4))*.12;
            x=sx+vx*travel;y=t<hit?sy+vy*t-gravity/2*t*t:floor;z=sz+vz*travel;if(t>=hit)y+=Math.Max(0,Math.Sin(Math.Min(Math.PI,slide*9)))*Math.Min(.12,Math.Abs(vy)*.02)*Math.Exp(-slide*3);return t>=hit;
        }
        public void Chunk(int i,double age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double scale)
        {
            double t=Math.Max(0,age),fade=NativeKillEffectRules.Clamp((NativeKillEffectRules.DeathSeconds-t)/.7);
            bool landed=Ballistic(start[i*3],start[i*3+1],start[i*3+2],vel[i*3],vel[i*3+1],vel[i*3+2],floor+size[i]/2,t,out x,out y,out z,out double hit);double spinT=landed?hit+.25*(1-Math.Exp(-(t-hit)*4)):t;
            NativeKillEffectRules.EulerXyz(spin[i*3]*spinT,spin[i*3+1]*spinT,spin[i*3+2]*spinT,out qx,out qy,out qz,out qw);scale=size[i]*fade;
        }
        /// <summary>Loose object i: position, the spin to put in front of its original rotation, and its uniform scale.</summary>
        public void LooseAt(int i,double age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double scale)
        {
            double t=Math.Max(0,age),fade=NativeKillEffectRules.Clamp((NativeKillEffectRules.DeathSeconds-t)/.7);
            bool landed=Ballistic(looseStart[i*3],looseStart[i*3+1],looseStart[i*3+2],looseVel[i*3],looseVel[i*3+1],looseVel[i*3+2],floor+.08,t,out x,out y,out z,out double hit);double spinT=landed?hit+.3*(1-Math.Exp(-(t-hit)*4)):t;
            NativeKillEffectRules.EulerXyz(looseSpin[i*3]*spinT,looseSpin[i*3+1]*spinT,looseSpin[i*3+2]*spinT,out qx,out qy,out qz,out qw);scale=Math.Max(.001,fade);
        }
    }
}
