using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The model's skeleton as one mesh: positions in the model's root space at rest, each vertex on one model bone
    /// (<see cref="Bones"/>[<see cref="Bone"/>]), with its sRGB colour (alpha 1: the outline draws) and what its inside is
    /// (<see cref="Kind"/>, for the cut's cap).</summary>
    public sealed class SkeletonMesh
    {
        public readonly List<string> Bones=new List<string>();
        public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
        public readonly List<Vector4> Colors=new List<Vector4>();
        public readonly List<int> Indices=new List<int>(),Bone=new List<int>();
        public readonly List<byte> Kind=new List<byte>();
    }

    // The model's skeleton (owner 2026-10-28: "Can you design a full skeleton inside the player (From skull to toe) that can be
    // visible during gibs + wounds? Ananomically correct and in the same style of the model, this should also improve slicing
    // graphics"): built from the model's own rest joints (root space, the bake's mannequin bone names) and fitted inside its
    // body (measured from the bake's base body: the vertebral column 3-5 cm in from the back skin, the rib cage 2 cm inside the
    // chest, the skull inside the head, every long bone inside its limb), in the model's look: smooth low-poly ivory pieces that
    // take the toon's light and outline.
    //  - Skull (head): the cranium, the face with dark orbits and nasal opening, the cheekbones; the mandible on the jaw bone.
    //  - Spine: 7 cervical, 12 thoracic and 5 lumbar vertebrae (bodies, spinous and transverse processes; discs between), each on
    //    the spine bone at its height; the sacrum and coccyx.
    //  - Thorax: 12 pairs of ribs from the vertebrae round to the front (costal cartilage to the sternum for the first seven, the
    //    eighth to tenth to the costal margin, the last two floating), the manubrium and sternum.
    //  - Shoulders and arms: the clavicles and scapulae (blade, spine, glenoid) on the clavicles; the humerus (head, shaft,
    //    epicondyles); the ulna (olecranon) behind and the radius on the thumb's side; the carpals, the five metacarpals and each
    //    finger's phalanges on the hand's own bones.
    //  - Pelvis and legs: the iliac wings, acetabula and pubic and ischial rami; the femur (head, neck, greater trochanter, shaft,
    //    condyles); the patella; the tibia (plateau, shaft, medial malleolus) and fibula (head, lateral malleolus); the talus,
    //    calcaneus, midfoot, five metatarsals and the toes.
    // Every piece rides one bone rigidly. Presentation only (Runtime NativeVisualSkeletonPresentation.cs).
    public static class NativeModelSkeleton
    {
        /// <summary>What a piece's inside is, for a cut's cap: a long bone's cortex round its marrow, spongy bone, the skull
        /// round the brain, cartilage, an orbit's dark.</summary>
        public const byte LongBone=0,Spongy=1,Skull=2,Cartilage=3,Socket=4;
        /// <summary>The bone, the ends of the long bones, cartilage and the orbits (sRGB).</summary>
        public const int Ivory=0xe8dcc2,Joint=0xf1e8d4,CartilageColor=0xcfdde2,SocketColor=0x2b1d1a;
        public const int Vertebrae=24,Ribs=12;
        /// <summary>How far a cut bone stands out of the flesh's cut face (m), on either side of a split.</summary>
        public const float BoneOut=.004f;
        /// <summary>The cut face of a piece of kind `kind` (Core NativeBisect.CapOf; linear colours): a long bone's cortex round a
        /// band of spongy bone round the red marrow; spongy bone's honeycomb flecked with marrow; the skull round the brain; cartilage
        /// and an orbit their own colour throughout.</summary>
        public static BisectLook CutLook(byte kind)=>kind==LongBone?LongCut:kind==Skull?SkullCut:kind==Spongy?SpongyCut:SolidCut;
        private static Vector3 L(int hex)=>NativePortraitRules.Linear(hex);
        private static readonly BisectLook LongCut=new BisectLook{Rim=.0022f,Inner=.0016f,RimShade=1,InnerColor=L(0xd8b88a)};
        private static readonly BisectLook SpongyCut=new BisectLook{Rim=.0014f,Inner=1,RimShade=1,InnerColor=L(0xd3ab82),ConduitShare=.22f,Conduit=L(0x9c2a24),ConduitLevel=.25f};
        private static readonly BisectLook SkullCut=new BisectLook{Rim=.0035f,Inner=1,RimShade=1,InnerColor=L(0xd9a3a0),ConduitShare=.12f,Conduit=L(0xa86868),ConduitLevel=.2f};
        private static readonly BisectLook SolidCut=new BisectLook{Solid=true,RimShade=1};

        /// <summary>The skeleton for joints at `rest` (a bone's rest position in the model's root space by its name, null when
        /// the model has none); null when a bone it needs is missing.</summary>
        public static SkeletonMesh Build(Func<string,Vector3?> rest)
        {
            try{var b=new Builder(rest);b.All();return b.M;}catch(MissingBone){return null;}
        }

        private sealed class MissingBone:Exception{}
        private sealed class Builder
        {
            public readonly SkeletonMesh M=new SkeletonMesh();private readonly Func<string,Vector3?> rest;
            public Builder(Func<string,Vector3?> rest){this.rest=rest;}
            private Vector3 J(string name)=>rest(name)??throw new MissingBone();
            private static Vector4 C(int hex)=>new Vector4((hex>>16&255)/255f,(hex>>8&255)/255f,(hex&255)/255f,1);
            private static readonly Vector4 Bone=C(Ivory),End=C(Joint),Gristle=C(CartilageColor),Dark=C(SocketColor);
            private int BoneOf(string name){int i=M.Bones.IndexOf(name);if(i<0){i=M.Bones.Count;M.Bones.Add(name);}return i;}
            private int Vert(Vector3 p,Vector3 n,Vector4 c,int bone,byte kind){M.Positions.Add(p);M.Normals.Add(n);M.Colors.Add(c);M.Bone.Add(bone);M.Kind.Add(kind);return M.Positions.Count-1;}
            // A triangle facing away from `inside` (Unity's front face: Cross(b-a, c-a) points out).
            private void Tri(int a,int b,int c,Vector3 inside)
            {
                Vector3 pa=M.Positions[a],pb=M.Positions[b],pc=M.Positions[c];var n=Vector3.Cross(pb-pa,pc-pa);if(n.LengthSquared()<1e-18f)return;
                if(Vector3.Dot(n,(pa+pb+pc)/3-inside)<0){int t=b;b=c;c=t;}M.Indices.Add(a);M.Indices.Add(b);M.Indices.Add(c);
            }
            private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-7f?v/l:fallback;}

            // A tube along `path`: elliptic sections `wide` across along `up` (projected off the path) and `thick` the other way,
            // rounded caps; `color`/`kind` per ring when given.
            public void Tube(string bone,byte kind,IList<Vector3> path,IList<float> wide,IList<float> thick,Vector3 up,int sides=6,bool caps=true,Func<int,Vector4> color=null,Func<int,byte> kinds=null,bool closed=false)
            {
                int n=path.Count,b=BoneOf(bone);var first=new int[n];var tan=new Vector3[n];
                for(int i=0;i<n;i++)
                {
                    Vector3 t=closed?path[(i+1)%n]-path[(i+n-1)%n]:i==0?path[1]-path[0]:i==n-1?path[n-1]-path[n-2]:path[i+1]-path[i-1];t=Unit(t,Vector3.UnitY);tan[i]=t;
                    var u=up-t*Vector3.Dot(up,t);u=Unit(u,Unit(Vector3.Cross(t,Math.Abs(t.Y)<.9f?Vector3.UnitY:Vector3.UnitX),Vector3.UnitX));var v=Vector3.Cross(t,u);
                    first[i]=M.Positions.Count;var c=color?.Invoke(i)??Bone;byte k=kinds?.Invoke(i)??kind;
                    for(int s=0;s<sides;s++)
                    {
                        float a=s*2*MathF.PI/sides,ca=MathF.Cos(a),sa=MathF.Sin(a);
                        Vert(path[i]+u*(ca*wide[i])+v*(sa*thick[i]),Unit(u*(ca/Math.Max(wide[i],1e-4f))+v*(sa/Math.Max(thick[i],1e-4f)),u),c,b,k);
                    }
                }
                int segments=closed?n:n-1;
                for(int i=0;i<segments;i++)
                {
                    int j=(i+1)%n;var inside=(path[i]+path[j])*.5f;
                    for(int s=0;s<sides;s++){int a=first[i]+s,a2=first[i]+(s+1)%sides,c=first[j]+s,c2=first[j]+(s+1)%sides;Tri(a,a2,c2,inside);Tri(a,c2,c,inside);}
                }
                if(!caps||closed)return;
                for(int e=0;e<2;e++)
                {
                    int i=e==0?0:n-1;var t=e==0?-tan[0]:tan[n-1];float r=Math.Min(wide[i],thick[i]);
                    int tip=Vert(path[i]+t*(r*.6f),t,color?.Invoke(i)??Bone,b,kinds?.Invoke(i)??kind);var inside=path[i]-t*r;
                    for(int s=0;s<sides;s++)Tri(tip,first[i]+s,first[i]+(s+1)%sides,inside);
                }
            }
            public void Tube(string bone,byte kind,IList<Vector3> path,float wide,float thick,Vector3 up,int sides=6,bool caps=true)
            {var w=new float[path.Count];var t=new float[path.Count];for(int i=0;i<w.Length;i++){w[i]=wide;t[i]=thick;}Tube(bone,kind,path,w,t,up,sides,caps);}
            // An ellipsoid at `c` with semi-axes `ax`, `ay` (its poles) and `az`.
            public void Blob(string bone,byte kind,Vector3 c,Vector3 ax,Vector3 ay,Vector3 az,Vector4? color=null,int rings=4,int segs=8)
            {
                int b=BoneOf(bone);var col=color??End;int first=M.Positions.Count;
                Vector3 nx=ax/Math.Max(ax.LengthSquared(),1e-10f),ny=ay/Math.Max(ay.LengthSquared(),1e-10f),nz=az/Math.Max(az.LengthSquared(),1e-10f);
                for(int r=0;r<=rings;r++)
                {
                    float phi=-MathF.PI/2+r*MathF.PI/rings,cp=MathF.Cos(phi),sp=MathF.Sin(phi);
                    for(int s=0;s<segs;s++){float th=s*2*MathF.PI/segs,x=cp*MathF.Cos(th),z=cp*MathF.Sin(th);Vert(c+ax*x+ay*sp+az*z,Unit(nx*x+ny*sp+nz*z,ay),col,b,kind);}
                }
                for(int r=0;r<rings;r++)for(int s=0;s<segs;s++)
                {
                    int a=first+r*segs+s,a2=first+r*segs+(s+1)%segs,c2=first+(r+1)*segs+(s+1)%segs,c3=first+(r+1)*segs+s;
                    if(r>0)Tri(a,a2,c2,c);if(r<rings-1)Tri(a,c2,c3,c);
                }
            }
            public void Ball(string bone,byte kind,Vector3 c,float x,float y,float z,Vector4? color=null,int rings=4,int segs=8)=>Blob(bone,kind,c,new Vector3(x,0,0),new Vector3(0,y,0),new Vector3(0,0,z),color,rings,segs);
            // A thick sheet between two rows of points (a flat bone: the scapula, the iliac wing), `thick` through, its faces turned
            // to `side` and away from it.
            public void Sheet(string bone,byte kind,IList<Vector3> top,IList<Vector3> bottom,float thick,Vector3 side)
            {
                int n=top.Count,b=BoneOf(bone);var mid=new Vector3[2,n];var nrm=new Vector3[n];
                for(int i=0;i<n;i++)
                {
                    mid[0,i]=top[i];mid[1,i]=bottom[i];var along=top[Math.Min(n-1,i+1)]-top[Math.Max(0,i-1)]+bottom[Math.Min(n-1,i+1)]-bottom[Math.Max(0,i-1)];
                    var n0=Unit(Vector3.Cross(along,bottom[i]-top[i]),side);if(Vector3.Dot(n0,side)<0)n0=-n0;nrm[i]=n0;
                }
                var id=new int[2,2,n];
                for(int f=0;f<2;f++)for(int r=0;r<2;r++)for(int i=0;i<n;i++){float sg=f==0?1:-1;id[f,r,i]=Vert(mid[r,i]+nrm[i]*(sg*thick*.5f),nrm[i]*sg,Bone,b,kind);}
                for(int f=0;f<2;f++)for(int i=0;i+1<n;i++){var inside=(mid[0,i]+mid[0,i+1]+mid[1,i]+mid[1,i+1])/4;Tri(id[f,0,i],id[f,0,i+1],id[f,1,i+1],inside);Tri(id[f,0,i],id[f,1,i+1],id[f,1,i],inside);}
                // The rims: along the top and bottom rows and down each end.
                for(int r=0;r<2;r++)for(int i=0;i+1<n;i++){var inside=(mid[r,i]+mid[r,i+1])*.5f+(mid[1-r,i]-mid[r,i])*.2f;Tri(id[0,r,i],id[0,r,i+1],id[1,r,i+1],inside);Tri(id[0,r,i],id[1,r,i+1],id[1,r,i],inside);}
                for(int e=0;e<2;e++){int i=e==0?0:n-1,o=e==0?1:n-2;var inside=(mid[0,i]+mid[1,i])*.5f+((mid[0,o]+mid[1,o])*.5f-(mid[0,i]+mid[1,i])*.5f)*.2f;Tri(id[0,0,i],id[0,1,i],id[1,1,i],inside);Tri(id[0,0,i],id[1,1,i],id[1,0,i],inside);}
            }

            public void All()
            {
                Spine();Thorax();Cranium();Pelvis();
                foreach(int side in new[]{-1,1}){Arm(side);Leg(side);}
            }

            // ---- the spine: each vertebra on the spine bone at its height, behind the rig's spine line as the back's profile puts it
            private static readonly string[] Chain={"pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02","head"};
            // Each vertebra's height (0 the pelvis joint, 1 the head's), from C1 down to L5, and its body's centre behind the rig's
            // spine line there (m; measured from the base body's back).
            private static readonly float[] Height={.980f,.950f,.923f,.896f,.869f,.840f,.807f,.773f,.737f,.701f,.666f,.630f,.592f,.555f,.515f,.474f,.433f,.391f,.346f,.299f,.244f,.188f,.131f,.075f};
            private static readonly float[] Behind={0,.002f,.003f,.002f,0,-.006f,-.014f,-.021f,-.026f,-.029f,-.031f,-.032f,-.033f,-.034f,-.035f,-.036f,-.037f,-.037f,-.034f,-.037f,-.043f,-.046f,-.049f,-.052f};
            private Vector3 SpineAt(float y,out string bone)
            {
                bone=Chain[0];Vector3 below=J(Chain[0]);
                for(int i=1;i<Chain.Length;i++){var above=J(Chain[i]);if(above.Y>=y){float t=(y-below.Y)/Math.Max(1e-5f,above.Y-below.Y);return Vector3.Lerp(below,above,Math.Clamp(t,0,1));}below=above;bone=Chain[i];}
                return below;
            }
            public readonly Vector3[] Vertebra=new Vector3[Vertebrae];public readonly string[] VertebraBone=new string[Vertebrae];
            private void Spine()
            {
                float y0=J("pelvis").Y,y1=J("head").Y;
                for(int k=0;k<Vertebrae;k++)
                {
                    float y=y0+Height[k]*(y1-y0);var on=SpineAt(y,out var bone);var c=new Vector3(0,y,on.Z+Behind[k]);Vertebra[k]=c;VertebraBone[k]=bone;
                    bool cerv=k<7,lum=k>=19;float t=cerv?0:lum?1:(k-7)/11f;
                    float rx=cerv?(k==0?.015f:.0095f):lum?.017f+.0008f*(k-19):.0115f+.004f*t,ry=cerv?.006f:lum?.011f:.0075f+.0015f*t,rz=cerv?.0085f:lum?.014f:.010f+.0035f*t;
                    if(k>0)Ball(bone,Spongy,c,rx,ry,rz,Bone,3,8);else{var ring=Ring(c+new Vector3(0,0,.003f),.014f,.008f,8);var rr=new float[ring.Length];for(int i=0;i<rr.Length;i++)rr[i]=.0035f;Tube(bone,Spongy,ring,rr,rr,Vector3.UnitY,5,false,null,null,true);}
                    // The disc below (cartilage), the spinous process back (and down in the thorax), the transverse processes out.
                    if(k>=2&&k<Vertebrae-1)Ball(bone,Cartilage,c-new Vector3(0,ry+.0025f,0),rx*.92f,.0022f,rz*.92f,Gristle,2,8);
                    float len=cerv?(k==6?.016f:k<4?.006f:.010f):lum?.019f:.015f,drop=cerv?.004f:lum?.002f:.012f+.006f*(float)Math.Sin(t*Math.PI);
                    var root=c-new Vector3(0,0,rz*.85f);var tip=root+new Vector3(0,-drop,-len);
                    Tube(bone,Spongy,new[]{root,(root+tip)*.5f,tip},new[]{lum?.007f:.0045f,lum?.0065f:.0038f,lum?.0055f:.0028f},new[]{.0035f,.003f,.0025f},Vector3.UnitY,5);
                    if(k>0)foreach(int s in new[]{-1,1})
                    {
                        float out_=cerv?.013f:lum?.026f:.021f-.004f*t;var a=c+new Vector3(s*rx*.7f,ry*.3f,-rz*.5f);var e=c+new Vector3(s*out_,ry*.4f,-rz*.9f-.004f);
                        Tube(bone,Spongy,new[]{a,e},.003f,.003f,Vector3.UnitY,4);
                    }
                }
                // The sacrum and coccyx (pelvis): curving back below L5.
                var l5=Vertebra[Vertebrae-1];var p=J("pelvis");
                var path=new[]{l5+new Vector3(0,-.020f,-.004f),new Vector3(0,p.Y-.016f,l5.Z-.012f),new Vector3(0,p.Y-.034f,l5.Z-.013f),new Vector3(0,p.Y-.045f,l5.Z-.008f)};
                Tube("pelvis",Spongy,path,new[]{.038f,.032f,.019f,.007f},new[]{.011f,.010f,.008f,.005f},Vector3.UnitX,6);
            }
            private static Vector3[] Ring(Vector3 c,float rx,float rz,int n){var r=new Vector3[n];for(int i=0;i<n;i++){float a=i*2*MathF.PI/n;r[i]=c+new Vector3(MathF.Cos(a)*rx,0,MathF.Sin(a)*rz);}return r;}

            // ---- the thorax: the sternum, and the ribs round from the thoracic vertebrae (each on its vertebra's bone)
            private static readonly float[] RibWidth={.050f,.068f,.082f,.092f,.099f,.104f,.106f,.105f,.102f,.097f,.090f,.084f};
            private static readonly float[] RibDrop={.020f,.035f,.048f,.058f,.066f,.072f,.076f,.078f,.075f,.068f,.050f,.038f};
            private static readonly float[] RibEnd={2.92f,2.96f,2.97f,2.97f,2.96f,2.95f,2.93f,2.72f,2.58f,2.44f,1.95f,1.62f};
            private static readonly float[] RibGristle={2.55f,2.35f,2.35f,2.35f,2.33f,2.30f,2.25f,2.12f,2.12f,2.12f,9f,9f};
            // The sternum's front by height (m above the pelvis joint's height share: 0 pelvis, 1 head; z ahead of the rig's spine line).
            private Vector3 SternumAt(float y)
            {
                float y0=J("pelvis").Y,y1=J("head").Y,h=(y-y0)/(y1-y0);
                float[] hs={.40f,.435f,.486f,.555f,.623f,.691f,.751f,.79f};float[] z={.052f,.060f,.071f,.079f,.082f,.076f,.056f,.045f};
                int i=0;while(i<hs.Length-2&&h>hs[i+1])i++;float t=Math.Clamp((h-hs[i])/(hs[i+1]-hs[i]),0,1);
                var on=SpineAt(y,out _);return new Vector3(0,y,on.Z+z[i]+(z[i+1]-z[i])*t);
            }
            private void Thorax()
            {
                float y0=J("pelvis").Y,y1=J("head").Y;float H(float h)=>y0+h*(y1-y0);
                // The manubrium (spine_05) and the body and xiphoid (spine_04).
                Tube("spine_05",Spongy,new[]{SternumAt(H(.775f)),SternumAt(H(.745f)),SternumAt(H(.715f))},new[]{.020f,.019f,.014f},new[]{.0055f,.0055f,.005f},Vector3.UnitX,6);
                Tube("spine_04",Spongy,new[]{SternumAt(H(.708f)),SternumAt(H(.64f)),SternumAt(H(.57f)),SternumAt(H(.50f)),SternumAt(H(.46f))},new[]{.014f,.016f,.017f,.013f,.006f},new[]{.005f,.0055f,.0055f,.005f,.003f},Vector3.UnitX,6);
                for(int r=0;r<Ribs;r++)
                {
                    int k=7+r;var v=Vertebra[k];string bone=VertebraBone[k];float yk=v.Y+.004f;
                    float front=SternumAt(yk-RibDrop[r]).Z,back=v.Z-.024f,zc=(front+back)*.5f,depth=(front-back)*.5f,a=RibWidth[r];
                    foreach(int s in new[]{-1,1})
                    {
                        var path=new List<Vector3>{new Vector3(s*.012f,yk,v.Z)};var grist=new List<bool>{false};
                        int steps=r<10?11:7;float th0=.42f,th1=RibEnd[r];
                        for(int i=0;i<=steps;i++)
                        {
                            float th=th0+(th1-th0)*i/steps,f=(1-MathF.Cos(th))*.5f;
                            path.Add(new Vector3(s*a*MathF.Sin(th),yk-RibDrop[r]*MathF.Pow(f,1.3f),zc-depth*MathF.Cos(th)));grist.Add(th>RibGristle[r]);
                        }
                        int n=path.Count;var wide=new float[n];var thick=new float[n];for(int i=0;i<n;i++){float f=i/(float)(n-1);wide[i]=r==0?.007f:.0085f-.0025f*f;thick[i]=.0032f;}
                        Tube(bone,Spongy,path,wide,thick,Vector3.UnitY,5,true,i=>grist[i]?Gristle:Bone,i=>grist[i]?Cartilage:Spongy);
                    }
                }
            }

            // ---- the skull (head) and mandible (jaw), fitted inside the head; offsets from the head joint
            private void Cranium()
            {
                var h=J("head");Vector3 At(float x,float y,float z)=>h+new Vector3(x,y,z);
                Blob("head",Skull,At(0,.104f,.021f),new Vector3(.083f,0,0),new Vector3(0,.088f,0),new Vector3(0,0,.096f),Bone,6,12);
                // The face: the maxilla and nose bridge, the cheekbones, the orbits and the nasal opening (dark).
                Blob("head",Skull,At(0,.029f,.084f),new Vector3(.028f,0,0),new Vector3(0,.021f,0),new Vector3(0,0,.022f),Bone,4,10);
                foreach(int s in new[]{-1,1})
                {
                    Blob("head",Socket,At(s*.041f,.058f,.093f),new Vector3(.022f,0,0),new Vector3(0,.019f,0),new Vector3(0,0,.012f),Dark,3,8);
                    Tube("head",Skull,new[]{At(s*.052f,.040f,.088f),At(s*.066f,.040f,.060f),At(s*.071f,.044f,.030f)},.006f,.004f,Vector3.UnitY,5);
                }
                Blob("head",Socket,At(0,.034f,.123f),new Vector3(.011f,0,0),new Vector3(0,.015f,0),new Vector3(0,0,.006f),Dark,3,6);
                // The mandible: from each condyle down the ramus, along the jaw line to the chin.
                string jaw=rest("jaw").HasValue?"jaw":"head";
                var path=new List<Vector3>();
                foreach(var p in new[]{(-.046f,.026f,.026f),(-.038f,-.004f,.034f),(-.030f,-.020f,.060f),(-.014f,-.031f,.078f),(0f,-.034f,.086f),(.014f,-.031f,.078f),(.030f,-.020f,.060f),(.038f,-.004f,.034f),(.046f,.026f,.026f)})path.Add(At(p.Item1,p.Item2,p.Item3));
                Tube(jaw,Skull,path,.007f,.0038f,Vector3.UnitY,6);
            }

            // ---- the pelvis: the iliac wings, the acetabula, the pubic and ischial rami (each side mirrored)
            private void Pelvis()
            {
                foreach(int s in new[]{-1,1})
                {
                    var hip=J(s<0?"thigh_l":"thigh_r");var p=J("pelvis");Vector3 At(float x,float y,float z)=>new Vector3(s*Math.Abs(x),p.Y+y,p.Z+z);
                    // The wing (mirrored offsets for the left, x out): its crest from the sacroiliac joint round to the front spine.
                    var crest=new[]{At(.034f,.034f,-.068f),At(.072f,.068f,-.062f),At(.110f,.072f,-.036f),At(.122f,.054f,-.002f),At(.108f,.024f,.030f)};
                    var low=new[]{At(.040f,-.012f,-.064f),At(.070f,-.016f,-.034f),At(.085f,-.018f,-.012f),At(.096f,-.014f,.004f),At(.100f,-.010f,.020f)};
                    Sheet("pelvis",Spongy,crest,low,.007f,new Vector3(s,0,0));
                    // The acetabulum round the femoral head, the rami round the obturator foramen to the symphysis.
                    Ball("pelvis",Spongy,hip+new Vector3(s*.007f,.011f,-.002f),.019f,.018f,.020f,Bone,4,8);
                    var ring=new[]{At(.088f,-.040f,.022f),At(.045f,-.050f,.011f),At(.010f,-.060f,.004f),At(.028f,-.073f,.000f),At(.058f,-.079f,-.022f),At(.078f,-.071f,-.040f),At(.094f,-.048f,-.020f)};
                    var rr=new float[ring.Length];for(int i=0;i<rr.Length;i++)rr[i]=.0065f;Tube("pelvis",Spongy,ring,rr,rr,Vector3.UnitY,5,false,null,null,true);
                }
            }

            // ---- an arm: clavicle and scapula (clavicle), humerus, ulna and radius, the hand's bones on their own bones
            private void Arm(int s)
            {
                string Side(string n)=>n+(s<0?"_l":"_r");
                var cl=J(Side("clavicle"));var sh=J(Side("upperarm"));var el=J(Side("lowerarm"));var wr=J(Side("hand"));
                Vector3 X(float x)=>new Vector3(s*x,0,0);
                // The clavicle: an S from the manubrium out to the acromion over the shoulder joint.
                var top=SternumAt(J("pelvis").Y+.772f*(J("head").Y-J("pelvis").Y));
                var cpath=new[]{new Vector3(s*.019f,top.Y+.002f,top.Z-.002f),new Vector3(s*.060f,cl.Y-.006f,top.Z-.010f),new Vector3(s*.100f,cl.Y,cl.Z+.022f),new Vector3(sh.X+X(.004f).X,sh.Y+.024f,sh.Z+.006f)};
                Tube(Side("clavicle"),LongBone,cpath,new[]{.0065f,.0055f,.0055f,.0065f},new[]{.0055f,.0045f,.0045f,.0040f},Vector3.UnitY,6);
                // The scapula: the blade on the back, its spine up to the acromion, the glenoid by the humeral head.
                var g=sh+X(-.012f)+new Vector3(0,0,-.004f);float bz=cl.Z;
                var sup=new[]{new Vector3(s*.062f,cl.Y-.008f,bz-.041f),new Vector3(s*.090f,cl.Y-.012f,bz-.031f),g+new Vector3(0,.010f,-.004f)};
                var inf=new[]{new Vector3(s*.074f,cl.Y-.143f,bz-.034f),new Vector3(s*.091f,cl.Y-.088f,bz-.031f),g+new Vector3(0,-.014f,-.004f)};
                Sheet(Side("clavicle"),Spongy,sup,inf,.0045f,new Vector3(0,0,-1));
                Tube(Side("clavicle"),Spongy,new[]{new Vector3(s*.064f,cl.Y-.030f,bz-.046f),new Vector3(s*.100f,cl.Y-.018f,bz-.039f),cpath[3]+new Vector3(0,-.004f,-.006f)},new[]{.003f,.005f,.006f},new[]{.0045f,.0045f,.004f},new Vector3(0,0,-1),5);
                Ball(Side("clavicle"),Spongy,g,.008f,.011f,.009f,Bone,3,8);
                // The humerus: head, shaft, the epicondyles across the elbow's hinge.
                var arm=Vector3.Normalize(el-sh);var fore=Vector3.Normalize(wr-el);var hinge=Unit(Vector3.Cross(arm,fore),new Vector3(0,0,1));
                Ball(Side("upperarm"),Spongy,sh,.018f,.018f,.018f,End,4,8);
                Tube(Side("upperarm"),LongBone,new[]{sh+arm*.020f,sh+arm*.09f,el-arm*.07f,el-arm*.018f},new[]{.0105f,.0088f,.0088f,.0115f},new[]{.0105f,.0088f,.0085f,.0085f},hinge,7);
                Blob(Side("upperarm"),Spongy,el-arm*.006f,hinge*.020f,arm*.010f,Vector3.Cross(hinge,arm)*.010f,End,3,8);
                // The ulna (little-finger side, the olecranon behind the elbow) and the radius (thumb side, wider at the wrist).
                var thumb=J(Side("thumb_01"))-wr;var rad=Unit(thumb-fore*Vector3.Dot(thumb,fore),hinge);
                // Behind the elbow: away from the side the forearm bends to.
                var back=Unit(-(fore-arm*Vector3.Dot(fore,arm)),new Vector3(0,0,-1));
                Tube(Side("lowerarm"),LongBone,new[]{el-fore*.020f-rad*.004f+back*.007f,el+fore*.03f-rad*.008f+back*.002f,wr-fore*.06f-rad*.009f,wr-fore*.012f-rad*.010f},new[]{.0085f,.0065f,.0050f,.0050f},new[]{.0095f,.0065f,.0050f,.0050f},back,6);
                Tube(Side("lowerarm"),LongBone,new[]{el+fore*.012f+rad*.008f,el+fore*.06f+rad*.008f,wr-fore*.05f+rad*.007f,wr-fore*.010f+rad*.005f},new[]{.0060f,.0050f,.0065f,.0095f},new[]{.0060f,.0050f,.0050f,.0060f},rad,6);
                // The carpals; each metacarpal and phalanx on the hand's own bone (the thumb's three, the fingers' four).
                var meta=new List<Vector3>();foreach(var f in new[]{"index","middle","ring","pinky"})if(rest(Side(f+"_metacarpal")).HasValue)meta.Add(J(Side(f+"_metacarpal")));
                var palm=meta.Count>0?Average(meta):wr+fore*.03f;var across=meta.Count>1?Unit(meta[0]-meta[meta.Count-1],rad):rad;var flat=Unit(Vector3.Cross(Unit(palm-wr,fore),across),back);
                Blob(Side("hand"),Spongy,(wr+palm)*.5f,across*.016f,Unit(palm-wr,fore)*.012f,flat*.0075f,End,3,8);
                foreach(var f in new[]{"index","middle","ring","pinky"})
                {
                    if(!rest(Side(f+"_metacarpal")).HasValue||!rest(Side(f+"_01")).HasValue)continue;
                    Digit(new[]{Side(f+"_metacarpal"),Side(f+"_01"),Side(f+"_02"),Side(f+"_03")},new[]{.0042f,.0038f,.0033f,.0029f},flat);
                }
                if(rest(Side("thumb_01")).HasValue)Digit(new[]{Side("thumb_01"),Side("thumb_02"),Side("thumb_03")},new[]{.0048f,.0040f,.0034f},flat);
            }
            private static Vector3 Average(List<Vector3> v){var s=Vector3.Zero;foreach(var p in v)s+=p;return s/v.Count;}
            // A finger's (or toe's) bones: each from its joint to the next (the last on past its joint), knuckles at the joints.
            private void Digit(string[] bones,float[] radius,Vector3 up)
            {
                int n=0;var at=new List<Vector3>();foreach(var b in bones){var p=rest(b);if(!p.HasValue)break;at.Add(p.Value);n++;}
                if(n<2)return;at.Add(at[n-1]+(at[n-1]-at[n-2])*.6f);
                for(int i=0;i<n;i++)
                {
                    var d=at[i+1]-at[i];float l=d.Length();if(l<1e-4f)continue;var u=d/l;float r=radius[Math.Min(i,radius.Length-1)];
                    Tube(bones[i],LongBone,new[]{at[i]+u*Math.Min(r*.8f,l*.2f),at[i]+d*.5f,at[i+1]-u*Math.Min(r*(i==n-1?0:.9f),l*.2f)},new[]{r*1.15f,r*.85f,r*1.05f},new[]{r*1.0f,r*.8f,r*.9f},up,5);
                }
            }

            // ---- a leg: femur, patella, tibia and fibula, the foot's bones
            private void Leg(int s)
            {
                string Side(string n)=>n+(s<0?"_l":"_r");Vector3 X(float x)=>new Vector3(s*x,0,0);
                var hip=J(Side("thigh"));var knee=J(Side("calf"));var ankle=J(Side("foot"));var ball=J(Side("ball"));
                var down=Vector3.Normalize(knee-hip);var shin=Vector3.Normalize(ankle-knee);var fwd=new Vector3(0,0,1);
                // The femur: head, neck out to the greater trochanter, the shaft bowed a little forward, the condyles.
                var neck=hip+X(.030f)+new Vector3(0,-.022f,-.004f);
                Ball(Side("thigh"),Spongy,hip,.0195f,.0195f,.0195f,End,4,8);
                Tube(Side("thigh"),LongBone,new[]{hip+X(.008f),neck},.0115f,.0100f,fwd,6);
                Ball(Side("thigh"),Spongy,hip+X(.044f)+new Vector3(0,-.014f,-.010f),.011f,.016f,.012f,End,3,8);
                var mid=Vector3.Lerp(hip,knee,.5f)+X(.006f)+new Vector3(0,0,.007f);
                Tube(Side("thigh"),LongBone,new[]{neck+new Vector3(0,-.012f,0),mid,knee-down*.045f,knee-down*.022f},new[]{.0125f,.0118f,.0130f,.0175f},new[]{.0125f,.0118f,.0125f,.0145f},fwd,7);
                foreach(float m in new[]{-1f,1f})Ball(Side("thigh"),Spongy,knee-down*.010f+X(m*.012f)+new Vector3(0,0,-.004f),.011f,.014f,.016f,End,3,8);
                Ball(Side("calf"),Spongy,knee+new Vector3(0,.018f,.026f),.011f,.014f,.0055f,End,3,8);
                // The tibia: plateau, shaft (its crest close under the shin), the medial malleolus; the fibula behind and out.
                Ball(Side("calf"),Spongy,knee+shin*.014f+new Vector3(0,0,-.002f),.021f,.009f,.018f,End,3,8);
                Tube(Side("calf"),LongBone,new[]{knee+shin*.025f+new Vector3(0,0,.002f),Vector3.Lerp(knee,ankle,.5f)+new Vector3(0,0,.004f),ankle-shin*.050f+X(-.002f),ankle-shin*.020f+X(-.004f)},new[]{.0130f,.0102f,.0095f,.0115f},new[]{.0120f,.0098f,.0088f,.0095f},fwd,7);
                Ball(Side("calf"),Spongy,ankle-shin*.004f+X(-.012f),.0055f,.0095f,.0065f,End,3,8);
                var fhead=knee+shin*.028f+X(.017f)+new Vector3(0,0,-.012f);
                Ball(Side("calf"),Spongy,fhead,.0065f,.0075f,.0065f,End,3,8);
                Tube(Side("calf"),LongBone,new[]{fhead+shin*.006f,Vector3.Lerp(fhead,ankle+X(.014f),.5f)+new Vector3(0,0,-.006f),ankle-shin*.012f+X(.013f)+new Vector3(0,0,-.005f)},new[]{.0050f,.0042f,.0050f},new[]{.0050f,.0042f,.0045f},fwd,6);
                Ball(Side("calf"),Spongy,ankle-shin*.002f+X(.014f)+new Vector3(0,0,-.004f),.0055f,.010f,.0065f,End,3,8);
                // The foot: talus, calcaneus to the heel, the midfoot, five metatarsals fanned to the ball, the toes (ball).
                Ball(Side("foot"),Spongy,ankle+new Vector3(0,-.018f,.004f),.014f,.011f,.016f,Bone,3,8);
                Tube(Side("foot"),Spongy,new[]{ankle+new Vector3(0,-.030f,-.004f),ankle+new Vector3(.0f,-.062f,-.022f)},.013f,.014f,Vector3.UnitY,6);
                Ball(Side("foot"),Spongy,ankle+new Vector3(0,-.073f,-.026f),.012f,.013f,.012f,Bone,3,8);
                var midfoot=Vector3.Lerp(ankle,ball,.38f)+new Vector3(0,-.020f,0);
                Ball(Side("foot"),Spongy,midfoot,.020f,.010f,.017f,Bone,3,8);
                float[] baseX={-.020f,-.010f,0,.010f,.019f},headX={-.023f,-.010f,.002f,.011f,.018f},r={.0058f,.0042f,.0040f,.0038f,.0038f},toe={.034f,.028f,.023f,.018f,.014f};
                for(int i=0;i<5;i++)
                {
                    var b0=new Vector3(ball.X+X(baseX[i]).X,midfoot.Y+.002f,midfoot.Z+.014f);var h0=new Vector3(ball.X+X(headX[i]).X,ball.Y+.004f,ball.Z-.006f);
                    Tube(Side("foot"),LongBone,new[]{b0,(b0+h0)*.5f+new Vector3(0,.003f,0),h0},new[]{r[i]*1.1f,r[i]*.85f,r[i]*1.15f},new[]{r[i],r[i]*.8f,r[i]*1.05f},Vector3.UnitY,5);
                    var t1=h0+new Vector3(0,-.002f,toe[i]*.55f);var t2=h0+new Vector3(0,-.003f,toe[i]);
                    Tube(Side("ball"),LongBone,new[]{h0+new Vector3(0,0,.006f),t1,t2},new[]{r[i]*.85f,r[i]*.75f,r[i]*.7f},new[]{r[i]*.8f,r[i]*.7f,r[i]*.6f},Vector3.UnitY,5);
                }
            }
        }
    }
}
