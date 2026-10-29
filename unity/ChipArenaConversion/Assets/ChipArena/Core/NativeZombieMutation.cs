using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Hulk's mutation on the player's model (owner 2026-10-29: "Hulk zombies need to look a LOT different, they are essentially
    // mutated. Theyre supposed to be huge, muscled zombies. They look just like a taller version of the player model right now";
    // then, of the first pass's lumps: the giant-brute archetype, a head sunk between boulder shoulders, a barrel chest, huge long
    // arms on small legs). Over its build (Core NativeZombieLooks.Build: the neck shortened so the head sits low, the arms longer,
    // the hands huge, the right arm grown a little past the left) a mutant variant wears grown flesh the model has not got
    // (Runtime/NativeZombieModelPresentation.cs ZombieGrowthMake):
    // - the trunk: boulder shoulders rising past the sunken head, the trapezius swollen up round it, a mound over the upper back,
    //   a barrel chest, a gut, lats flaring the sides;
    // - the arms sheathed in swollen muscle, each forearm a club that flares toward the fist and turns raw toward it;
    // - bone spurs breaking out of the shoulders, the back and the right forearm (ivory, blood at the root), and pustules (they
    //   glow a little).
    // Each mass is a smooth, lightly lobed ellipsoid placed from the model's rest joints (root space, +z its front, +y up, its left
    // at -x), rigid on one bone; a limb's lies along the limb, its length a share of the limb's. Its vertex colours are the flesh
    // (the skin tone, raw red patches and creases, a few dark veins), a spur's ivory and a pustule's yellow (alpha 1: it glows).
    // Presentation only.
    public static class NativeZombieMutation
    {
        public sealed class Mass
        {
            public string Name,Bone,From,To;public float Share;
            /// <summary>Its centre's offset from the point Share of the way From to To: across (toward its side; +x for a centred
            /// mass), up (along the limb for a limb's) and ahead (m, at the model's rest).</summary>
            public float Across,Up,Ahead;
            /// <summary>Its half sizes: across, up and ahead (m; a limb's up is a share of the limb's length, along it), how lobed (a
            /// share of the radius), how much wider it grows toward To (a limb's club) and how raw it turns there.</summary>
            public Vector3 Radii;public float Lumps,Taper,Raw;public bool Limb;
        }
        /// <summary>A spur out of a mass: its direction (across, up, ahead; made square to the limb on a limb's), length and base radius.</summary>
        public readonly struct Spur{public readonly string Mass;public readonly Vector3 Dir;public readonly float Length,Radius;public Spur(string mass,Vector3 dir,float length,float radius){Mass=mass;Dir=dir;Length=length;Radius=radius;}}
        public sealed class Growth
        {
            public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
            /// <summary>0..1 colours as authored (the runtime takes them to linear, as the sac's); alpha the glow's share (a pustule's).</summary>
            public readonly List<Vector4> Colors=new List<Vector4>();
            /// <summary>Each vertex's bones (indices into Bones) and the second's weight (the first takes the rest): a merged mass
            /// blends between the bones of the masses it is grown from; a spur or a pustule is rigid on its mass's (Weight2 0).</summary>
            public readonly List<int> Bone=new List<int>(),Bone2=new List<int>();public readonly List<float> Weight2=new List<float>();
            public readonly List<int> Indices=new List<int>();
            public readonly List<string> Bones=new List<string>();
        }
        private static Mass M(string name,string bone,string from,string to,float share,float across,float up,float ahead,float rx,float ry,float rz,float lumps,bool limb=false,float taper=0,float raw=0)
            =>new Mass{Name=name,Bone=bone,From=from,To=to,Share=share,Across=across,Up=up,Ahead=ahead,Radii=new Vector3(rx,ry,rz),Lumps=lumps,Limb=limb,Taper=taper,Raw=raw};
        /// <summary>The masses (a side's name ends in -l or -r: its across is toward that side).</summary>
        public static readonly Mass[] Masses=
        {
            // The trunk (one smooth surface): boulder shoulders' roots in the trapezius behind and round the sunken head, a mound over
            // the upper back, a barrel chest low on the front, a gut, lats flaring the sides.
            M("trap-l","spine_05","spine_05","upperarm_l",.42f,0,.07f,-.075f,.13f,.095f,.11f,.05f),
            M("trap-r","spine_05","spine_05","upperarm_r",.42f,0,.07f,-.075f,.13f,.095f,.11f,.05f),
            M("mound","spine_05","spine_05","spine_05",0,0,.06f,-.14f,.24f,.18f,.13f,.06f),
            M("chest","spine_05","spine_05","spine_05",0,0,-.075f,.08f,.21f,.13f,.1f,.04f),
            M("gut","spine_03","spine_03","spine_03",0,0,-.01f,.075f,.17f,.12f,.09f,.04f),
            M("lat-l","spine_04","spine_04","spine_04",0,.12f,.02f,-.02f,.075f,.13f,.09f,.05f),
            M("lat-r","spine_04","spine_04","spine_04",0,.12f,.02f,-.02f,.075f,.13f,.09f,.05f),
            // Each arm (one smooth surface): a boulder shoulder, the upper arm swollen, the forearm a club flaring and raw toward
            // the fist; the right a little bigger.
            M("shoulder-l","upperarm_l","upperarm_l","upperarm_l",0,.025f,.05f,-.02f,.15f,.14f,.14f,.05f),
            M("shoulder-r","upperarm_r","upperarm_r","upperarm_r",0,.03f,.055f,-.02f,.16f,.15f,.15f,.06f),
            M("arm-l","upperarm_l","upperarm_l","lowerarm_l",.55f,0,0,0,.11f,.6f,.11f,.05f,true),
            M("arm-r","upperarm_r","upperarm_r","lowerarm_r",.55f,0,0,0,.12f,.6f,.12f,.06f,true),
            M("forearm-l","lowerarm_l","lowerarm_l","hand_l",.58f,0,0,0,.135f,.66f,.125f,.06f,true,.65f,.75f),
            M("forearm-r","lowerarm_r","lowerarm_r","hand_r",.58f,0,0,0,.145f,.66f,.135f,.07f,true,.65f,.8f),
        };
        public static readonly Spur[] Spurs=
        {
            new Spur("mound",new Vector3(.35f,.6f,-.7f),.11f,.022f),new Spur("mound",new Vector3(-.45f,.5f,-.75f),.1f,.02f),new Spur("mound",new Vector3(.05f,.25f,-1),.08f,.018f),
            new Spur("shoulder-r",new Vector3(.75f,.65f,-.1f),.1f,.02f),new Spur("shoulder-r",new Vector3(.6f,.35f,-.7f),.08f,.017f),new Spur("shoulder-l",new Vector3(.7f,.6f,-.35f),.07f,.016f),
            new Spur("forearm-r",new Vector3(.6f,.2f,-.8f),.09f,.017f),new Spur("forearm-r",new Vector3(.8f,-.35f,-.4f),.07f,.015f),
        };
        /// <summary>Pustules: on which mass, toward where (across, up, ahead) and how big (m).</summary>
        public static readonly (string Mass,Vector3 Dir,float Radius)[] Pustules=
        {
            ("mound",new Vector3(.6f,.3f,-.5f),.016f),("mound",new Vector3(-.2f,.7f,-.6f),.012f),("shoulder-r",new Vector3(.5f,-.2f,.8f),.014f),
            ("chest",new Vector3(.55f,.35f,1),.013f),("forearm-r",new Vector3(.5f,.4f,.7f),.012f),("lat-l",new Vector3(.6f,0,.6f),.011f),
        };
        public const int Rings=12,Sides=18,SpurSides=7;
        public const int RawColour=0x7a2a2c,VeinColour=0x4a2a3a,BoneColour=0xe2d8bd,BoneTipColour=0xa59a80,PusColour=0xd7dd56;
        public static Mass Find(string name){foreach(var m in Masses)if(m.Name==name)return m;return null;}

        /// <summary>Where a mass sits (root space at rest), its frame (x across toward its side, y up or along the limb toward From,
        /// z ahead) and its half sizes (a limb's along its length); false when a joint is missing (from `rest(name)`).</summary>
        public static bool Place(Mass m,Func<string,Vector3?> rest,out Vector3 centre,out Vector3 x,out Vector3 y,out Vector3 z,out Vector3 radii)
        {
            centre=x=y=z=radii=Vector3.Zero;if(m==null)return false;
            var a=rest(m.From);var b=rest(m.To);var ul=rest("upperarm_l");var ur=rest("upperarm_r");if(a==null||b==null||ul==null||ur==null)return false;
            var right=ur.Value-ul.Value;right.Y=0;if(right.LengthSquared()<1e-8f)return false;right=Vector3.Normalize(right);
            x=m.Name.EndsWith("-l",StringComparison.Ordinal)?-right:right;y=Vector3.UnitY;radii=m.Radii;
            if(m.Limb)
            {
                var axis=a.Value-b.Value;float len=axis.Length();if(len<1e-4f)return false;y=axis/len;x-=y*Vector3.Dot(x,y);if(x.LengthSquared()<1e-8f)return false;x=Vector3.Normalize(x);
                radii.Y*=len;
            }
            // (z faces the model's front: on the left the across is -x, and x × y alone would face behind.)
            z=Vector3.Normalize(Vector3.Cross(x,y));if(Vector3.Dot(z,Vector3.UnitZ)<0)z=-z;
            centre=Vector3.Lerp(a.Value,b.Value,m.Share)+x*m.Across+y*m.Up+z*m.Ahead;
            return true;
        }
        /// <summary>A mass's radius share in direction (u, v, w) of its frame: 1 ± Lumps in a few seeded broad lobes, and a limb's
        /// club growing by Taper toward To (-v).</summary>
        public static float Lump(Vector3 d,Mass m,float seed)
        {
            float a=MathF.Atan2(d.X,d.Z),b=MathF.Asin(Math.Max(-1,Math.Min(1,d.Y)));
            float n=.65f*MathF.Sin(2*a+seed*1.7f)*MathF.Sin(2*b+seed*2.9f)+.35f*MathF.Sin(3*a+seed*4.1f)*MathF.Cos(3*b+seed*.7f);
            return (1+m.Lumps*n)*Club(d,m);
        }
        private static float Club(Vector3 d,Mass m)=>1+m.Taper*Smooth((-d.Y+.2f)/1.2f);
        private static float Ellipsoid(Vector3 d,Vector3 r)=>1/MathF.Sqrt(d.X*d.X/(r.X*r.X)+d.Y*d.Y/(r.Y*r.Y)+d.Z*d.Z/(r.Z*r.Z));
        // A placed mass: where, its frame and sizes, its bone and lobes' seed.
        private readonly struct Placed{public readonly Vector3 C,X,Y,Z,R;public readonly int Bone;public readonly float Seed,Reach2;public readonly Mass M;
            public Placed(Vector3 c,Vector3 x,Vector3 y,Vector3 z,Vector3 r,int bone,float seed,Mass m)
            {C=c;X=x;Y=y;Z=z;R=r;Bone=bone;Seed=seed;M=m;float reach=Math.Max(r.X,Math.Max(r.Y,r.Z))*(1+m.Lumps)*(1+m.Taper)*Soft;Reach2=reach*reach;}}
        /// <summary>A mass's field at a point (root space): (1 - (q/Soft)^2)^3 where q is the point's distance from its centre in its
        /// lobed, clubbed radii (1 on its own surface), 0 past Soft; masses of one group add, so they meet in smooth fillets.</summary>
        private static float Field(in Placed m,Vector3 p)
        {
            var d=p-m.C;float d2=d.LengthSquared();if(d2>=m.Reach2)return 0;
            var local=new Vector3(Vector3.Dot(d,m.X),Vector3.Dot(d,m.Y),Vector3.Dot(d,m.Z));float len=MathF.Sqrt(d2);
            if(len<1e-6f)return 1;var dir=local/len;float q=len/(Ellipsoid(dir,m.R)*Lump(dir,m.M,m.Seed));
            if(q>=Soft)return 0;float t=1-q*q/(Soft*Soft);return t*t*t;
        }
        /// <summary>How far a lone mass's field reaches (a share of its radius), the surface's level (a lone mass's at its radius), and
        /// the grid's step (m, at the model's rest).</summary>
        public const float Soft=1.55f,Cell=.023f;
        public static readonly float Level=MathF.Pow(1-1/(Soft*Soft),3);
        /// <summary>A mass's group: the trunk's masses (on the spine) grow as one surface, each arm's as another.</summary>
        public static int Group(Mass m)=>m.Bone.StartsWith("spine",StringComparison.Ordinal)?0:m.Bone.EndsWith("_l",StringComparison.Ordinal)?1:2;
        /// <summary>The mutation at rest for a model whose rest joints `rest` gives (root space), its flesh from `skin` (0xRRGGBB),
        /// lobed by `seed`; null when the model lacks the joints. Each group's masses become one smooth surface (surface nets over
        /// their summed field), each vertex weighted to the bones of the masses it grew from by their shares there.</summary>
        public static Growth Build(Func<string,Vector3?> rest,int skin,uint seed)
        {
            var g=new Growth();var placed=new Dictionary<string,Placed>();var all=new List<Placed>();
            Vector4 own=Rgb(skin),raw=Rgb(RawColour),vein=Rgb(VeinColour);var flesh=own*.97f;flesh.W=0;
            int k=0;
            foreach(var m in Masses)
            {
                if(!Place(m,rest,out var c,out var x,out var y,out var z,out var radii))return null;
                int bone=g.Bones.IndexOf(m.Bone);if(bone<0){bone=g.Bones.Count;g.Bones.Add(m.Bone);}
                float sd=(seed%997)*.013f+k*2.31f;k++;var pl=new Placed(c,x,y,z,radii,bone,sd,m);placed[m.Name]=pl;all.Add(pl);
            }
            for(int group=0;group<3;group++)
            {
                var ms=all.FindAll(q=>Group(q.M)==group);if(ms.Count==0)continue;
                // The grid over the group's reach.
                Vector3 lo=new Vector3(float.MaxValue),hi=new Vector3(float.MinValue);
                foreach(var q in ms){float r=Math.Max(q.R.X,Math.Max(q.R.Y,q.R.Z))*(1+q.M.Lumps)*(1+q.M.Taper)*Soft;lo=Vector3.Min(lo,q.C-new Vector3(r));hi=Vector3.Max(hi,q.C+new Vector3(r));}
                int nx=(int)MathF.Ceiling((hi.X-lo.X)/Cell)+1,ny=(int)MathF.Ceiling((hi.Y-lo.Y)/Cell)+1,nz=(int)MathF.Ceiling((hi.Z-lo.Z)/Cell)+1;
                var f=new float[nx*ny*nz];int I(int a,int b,int c)=>(c*ny+b)*nx+a;
                for(int c=0;c<nz;c++)for(int b=0;b<ny;b++)for(int a=0;a<nx;a++){var at=lo+new Vector3(a,b,c)*Cell;float v=0;foreach(var m in ms)v+=Field(m,at);f[I(a,b,c)]=v-Level;}
                // A vertex in each cell the surface crosses: the mean of its edges' crossings.
                var cellVertex=new int[(nx-1)*(ny-1)*(nz-1)];int C(int a,int b,int c)=>(c*(ny-1)+b)*(nx-1)+a;
                for(int c=0;c<nz-1;c++)for(int b=0;b<ny-1;b++)for(int a=0;a<nx-1;a++)
                {
                    cellVertex[C(a,b,c)]=-1;int inside=0;
                    for(int q=0;q<8;q++)if(f[I(a+(q&1),b+(q>>1&1),c+(q>>2))]>0)inside++;
                    if(inside==0||inside==8)continue;
                    Vector3 sum=Vector3.Zero;int n=0;
                    for(int e=0;e<12;e++)
                    {
                        int axis=e/4,u=e&1,v=e>>1&1;Vector3 o0=axis==0?new Vector3(0,u,v):axis==1?new Vector3(u,0,v):new Vector3(u,v,0);Vector3 o1=o0+(axis==0?Vector3.UnitX:axis==1?Vector3.UnitY:Vector3.UnitZ);
                        float f0=f[I(a+(int)o0.X,b+(int)o0.Y,c+(int)o0.Z)],f1=f[I(a+(int)o1.X,b+(int)o1.Y,c+(int)o1.Z)];if(f0>0==f1>0)continue;
                        sum+=Vector3.Lerp(o0,o1,f0/(f0-f1));n++;
                    }
                    var at=lo+(new Vector3(a,b,c)+sum/n)*Cell;cellVertex[C(a,b,c)]=g.Positions.Count;g.Positions.Add(at);
                    // Its normal down the field, its bones by the masses' shares, its colour the leading mass's.
                    Vector3 grad=Vector3.Zero;const float h=.004f;
                    foreach(var m in ms)grad+=new Vector3(Field(m,at+new Vector3(h,0,0))-Field(m,at-new Vector3(h,0,0)),Field(m,at+new Vector3(0,h,0))-Field(m,at-new Vector3(0,h,0)),Field(m,at+new Vector3(0,0,h))-Field(m,at-new Vector3(0,0,h)));
                    g.Normals.Add(grad.LengthSquared()>1e-14f?-Vector3.Normalize(grad):Vector3.UnitY);
                    int b1=-1,b2=-1;float w1=0,w2=0,best=-1;Placed lead=ms[0];var share=new Dictionary<int,float>();
                    foreach(var m in ms){float w=Field(m,at);share.TryGetValue(m.Bone,out float was);share[m.Bone]=was+w;if(w>best){best=w;lead=m;}}
                    foreach(var kv in share){if(kv.Value>w1){b2=b1;w2=w1;b1=kv.Key;w1=kv.Value;}else if(kv.Value>w2){b2=kv.Key;w2=kv.Value;}}
                    if(b1<0)b1=lead.Bone;float wsum=w1+w2;g.Bone.Add(b1);g.Bone2.Add(b2<0?b1:b2);g.Weight2.Add(b2<0||wsum<=1e-6f?0:w2/wsum);
                    g.Colors.Add(Flesh(lead,at,flesh,raw,vein));
                }
                // A quad round each grid edge the surface crosses, turned to face out (down the field).
                for(int c=1;c<nz-1;c++)for(int b=1;b<ny-1;b++)for(int a=1;a<nx-1;a++)
                {
                    float f0=f[I(a,b,c)];
                    for(int axis=0;axis<3;axis++)
                    {
                        int a1=a+(axis==0?1:0),b1=b+(axis==1?1:0),c1=c+(axis==2?1:0);if(a1>=nx||b1>=ny||c1>=nz)continue;
                        float f1=f[I(a1,b1,c1)];if(f0>0==f1>0)continue;
                        // The four cells round the edge.
                        int q0,q1,q2,q3;
                        if(axis==0){q0=cellVertex[C(a,b-1,c-1)];q1=cellVertex[C(a,b,c-1)];q2=cellVertex[C(a,b,c)];q3=cellVertex[C(a,b-1,c)];}
                        else if(axis==1){q0=cellVertex[C(a-1,b,c-1)];q1=cellVertex[C(a,b,c-1)];q2=cellVertex[C(a,b,c)];q3=cellVertex[C(a-1,b,c)];}
                        else{q0=cellVertex[C(a-1,b-1,c)];q1=cellVertex[C(a,b-1,c)];q2=cellVertex[C(a,b,c)];q3=cellVertex[C(a-1,b,c)];}
                        if(q0<0||q1<0||q2<0||q3<0)continue;
                        Face(g,q0,q1,q2);Face(g,q0,q2,q3);
                    }
                }
            }
            foreach(var sp in Spurs)
            {
                if(!placed.TryGetValue(sp.Mass,out var p))continue;var d=Out(p,all,sp.Dir,out var surface);
                Cone(g,surface-d*sp.Radius*.6f,d,sp.Length,sp.Radius,p.Bone,raw);
            }
            foreach(var (mass,dir,radius) in Pustules)
            {
                if(!placed.TryGetValue(mass,out var p))continue;var d=Out(p,all,dir,out var surface);
                Blob(g,surface-d*radius*.35f,radius,p.Bone,Rgb(PusColour));
            }
            return g;
        }
        // A mass's flesh at a point: raw red where its lobes dip and in broad patches, rawer toward a club's fist; a few dark veins.
        private static Vector4 Flesh(in Placed m,Vector3 at,Vector4 flesh,Vector4 raw,Vector4 vein)
        {
            var d=at-m.C;var local=new Vector3(Vector3.Dot(d,m.X),Vector3.Dot(d,m.Y),Vector3.Dot(d,m.Z));if(local.LengthSquared()<1e-10f)return flesh;var dir=Vector3.Normalize(local);
            float th=MathF.Atan2(dir.X,dir.Z),phi=MathF.Acos(Math.Max(-1,Math.Min(1,dir.Y))),lump=Lump(dir,m.M,m.Seed)/Club(dir,m.M),s=m.Seed;
            float dip=m.M.Lumps>0?Smooth((1+m.M.Lumps-lump)/(2*m.M.Lumps)*1.4f-.5f):0;
            float patch=Smooth((MathF.Sin(3*th+s*5.3f)*MathF.Sin(2.5f*phi+s*3.1f)-.55f)*3);
            float rawShare=Math.Max(Math.Max(dip*.7f,patch*.55f),m.M.Raw*Smooth((-dir.Y-.05f)/.85f));
            float veinBand=MathF.Abs(MathF.Sin(7*th+5*dir.Y+s*3.3f))<.05f?.6f:0;
            var col=Vector4.Lerp(flesh,raw,rawShare);col=Vector4.Lerp(col,vein,veinBand);col.W=0;return col;
        }
        // A face of a merged surface, turned to face out (along its vertices' normals, which point down the field).
        private static void Face(Growth g,int a,int b,int c)
        {
            var f=Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);if(f.LengthSquared()<1e-14f)return;
            if(Vector3.Dot(f,g.Normals[a]+g.Normals[b]+g.Normals[c])>=0){g.Indices.Add(a);g.Indices.Add(b);g.Indices.Add(c);}else{g.Indices.Add(a);g.Indices.Add(c);g.Indices.Add(b);}
        }
        // A direction out of a placed mass (its frame's across, up, ahead; square to a limb on a limb's) and where it leaves its
        // group's merged surface.
        private static Vector3 Out(Placed p,List<Placed> all,Vector3 dir,out Vector3 surface)
        {
            var local=Vector3.Normalize(dir);if(p.M.Limb){local.Y*=.35f;local=Vector3.Normalize(local);}
            var world=Vector3.Normalize(p.X*local.X+p.Y*local.Y+p.Z*local.Z);int group=Group(p.M);
            float t=0,limit=Math.Max(p.R.X,Math.Max(p.R.Y,p.R.Z))*3;
            while(t<limit){var at=p.C+world*t;float v=0;foreach(var m in all)if(Group(m.M)==group)v+=Field(m,at);if(v<Level)break;t+=.003f;}
            surface=p.C+world*Math.Max(0,t-.004f);return world;
        }
        // A spur: a seven-sided cone from `at` along `dir`, blood at its root, ivory up to a darker tip.
        private static void Cone(Growth g,Vector3 at,Vector3 dir,float length,float radius,int bone,Vector4 root)
        {
            var u=Vector3.Normalize(Vector3.Cross(dir,MathF.Abs(dir.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var v=Vector3.Cross(dir,u);
            Vector4 ivory=Rgb(BoneColour),tip=Rgb(BoneTipColour);root.W=0;
            int first=g.Positions.Count;var centre=at+dir*length*.3f;
            for(int ring=0;ring<3;ring++)
            {
                float t=ring==0?0:ring==1?.35f:.7f,r=radius*(ring==0?1:ring==1?.72f:.4f);
                for(int j=0;j<SpurSides;j++){float th=MathF.PI*2*j/SpurSides;g.Positions.Add(at+dir*length*t+(u*MathF.Cos(th)+v*MathF.Sin(th))*r);g.Bone.Add(bone);g.Bone2.Add(bone);g.Weight2.Add(0);g.Colors.Add(ring==0?root:Vector4.Lerp(ivory,tip,t));}
            }
            int apex=g.Positions.Count;g.Positions.Add(at+dir*length);g.Bone.Add(bone);g.Bone2.Add(bone);g.Weight2.Add(0);g.Colors.Add(tip);
            for(int ring=0;ring<2;ring++)for(int j=0;j<SpurSides;j++)
            {
                int a0=first+ring*SpurSides+j,a1=first+ring*SpurSides+(j+1)%SpurSides,b0=a0+SpurSides,b1=a1+SpurSides;Tri(g,a0,b0,a1,centre);Tri(g,a1,b0,b1,centre);
            }
            for(int j=0;j<SpurSides;j++)Tri(g,first+2*SpurSides+j,apex,first+2*SpurSides+(j+1)%SpurSides,centre);
            Smooth(g,first,centre);
        }
        // A pustule: a small sphere, glowing (alpha 1).
        private static void Blob(Growth g,Vector3 at,float radius,int bone,Vector4 colour)
        {
            colour.W=1;int first=g.Positions.Count;const int R=5,S=8;
            for(int r=0;r<=R;r++){float phi=MathF.PI*r/R;for(int j=0;j<S;j++){float th=MathF.PI*2*j/S;g.Positions.Add(at+new Vector3(MathF.Sin(phi)*MathF.Sin(th),MathF.Cos(phi),MathF.Sin(phi)*MathF.Cos(th))*radius);g.Bone.Add(bone);g.Bone2.Add(bone);g.Weight2.Add(0);g.Colors.Add(colour);}}
            for(int r=0;r<R;r++)for(int j=0;j<S;j++){int a0=first+r*S+j,a1=first+r*S+(j+1)%S,b0=a0+S,b1=a1+S;if(r>0)Tri(g,a0,b0,a1,at);if(r<R-1)Tri(g,a1,b0,b1,at);}
            Smooth(g,first,at);
        }
        // A triangle turned to face away from its part's centre (its cross product outward, as the sac's and the skeleton's).
        private static void Tri(Growth g,int a,int b,int c,Vector3 centre)
        {
            var pa=g.Positions[a];var f=Vector3.Cross(g.Positions[b]-pa,g.Positions[c]-pa);if(f.LengthSquared()<1e-14f)return;var mid=(pa+g.Positions[b]+g.Positions[c])/3;
            if(Vector3.Dot(f,mid-centre)>=0){g.Indices.Add(a);g.Indices.Add(b);g.Indices.Add(c);}else{g.Indices.Add(a);g.Indices.Add(c);g.Indices.Add(b);}
        }
        // A part's normals from its faces (area weighted), its vertices from `first` on; a pole's repeated vertices each its own.
        private static void Smooth(Growth g,int first,Vector3 centre)
        {
            int n=g.Positions.Count;while(g.Normals.Count<n)g.Normals.Add(Vector3.Zero);
            int start=g.Indices.Count;while(start>=3&&g.Indices[start-3]>=first)start-=3;
            for(int i=start;i+2<g.Indices.Count;i+=3){int a=g.Indices[i],b=g.Indices[i+1],c=g.Indices[i+2];var f=Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);g.Normals[a]+=f;g.Normals[b]+=f;g.Normals[c]+=f;}
            for(int i=first;i<n;i++){var v=g.Normals[i];g.Normals[i]=v.LengthSquared()>1e-16f?Vector3.Normalize(v):Vector3.Normalize(g.Positions[i]-centre);}
        }
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static Vector4 Rgb(int c)=>new Vector4((c>>16&255)/255f,(c>>8&255)/255f,(c&255)/255f,0);
    }
}
