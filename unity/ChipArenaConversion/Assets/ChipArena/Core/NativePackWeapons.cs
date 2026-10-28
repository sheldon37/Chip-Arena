using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Pack weapons (docs/BOZO-INTEGRATION.md "Pack weapons"; owner 2026-10-28 "Lets do all of those"): life-size models from the
    // weapons pack bank (tools/pack_bank/selections/weapons.json -> Resources/ChipArenaPackWeapons, in the licensed-assets zip)
    // for the weapons the visual body's local bake carries no model of: the daggers, the shield, the bow and the caster staffs.
    // Each Fit lays a bank prop into the visual weapon frame (Unity's: the source weapon frame with x mirrored) with its grip on
    // the game handle's grip point and its tip or length on the game's sockets, so whatever is placed from those sockets (blade
    // trails, blood, enchants, the shield's edge, the staff's charge orb and muzzle, the bow string's anchors) stays where it is.
    // Shape turns the prop into one mesh in that frame: the bow without its baked string and arrow and with a limb weight per
    // vertex (the limbs bend about the game's pivots with the pull), the staff's crystal as its own piece in the school's colour.
    // Presentation only: no mesh here defines reach, hit volumes, timing or the wire. Without the bank nothing changes.
    public static class NativePackWeapons
    {
        public const string Bank="ChipArenaPackWeapons";
        /// <summary>How one bank prop sits in one weapon's frame (Unity weapon frame, metres).</summary>
        public sealed class Fit
        {
            public string Spec,Prop;
            /// <summary>The prop's grip point and axes (prop space): Along the frame's +z (blade, shaft, shield face or downrange),
            /// Up its +y; the frame's +x is Up x Along.</summary>
            public Vector3 Grip,Along,Up;
            /// <summary>The cross-section's scale (frame x and y).</summary>
            public float Scale=1;
            /// <summary>Along the axis: prop distances from the grip (ascending) and where each lands on the frame's z from At's
            /// (piecewise linear, the end slopes carried on): the staffs' shafts shorten more than their heads.</summary>
            public float[] From={0,1},To={0,1};
            /// <summary>Where the grip lands in the frame.</summary>
            public Vector3 At;
            /// <summary>Left: rides the left hand (the staffs' StaffMatrix, the bow hand). Dual: drawn on both hands (daggers).
            /// Seat: the visual fist closes on it there (a staff's shaft; the daggers' grips are the melee set's own).
            /// Limbs: a bow, its limbs bending with the pull. KeepLargest: only the largest connected piece is drawn.</summary>
            public bool Left,Dual,Seat,Limbs,KeepLargest;
            /// <summary>The prop's piece drawn in the weapon's colour (the staff's crystal): the connected piece whose centre lies
            /// nearest Mark (prop space; NaN: none).</summary>
            public Vector3 Mark=new Vector3(float.NaN);
            /// <summary>Measured on the prop (prop space), for the checks: the tip (blade point, the shield's top edge, the
            /// staff's crystal, the bow's upper string nock), the second end (the bow's lower nock, the staff's top), and the
            /// handle's radius about the axis at the grip.</summary>
            public Vector3 Tip,End;public float Handle;
            /// <summary>The weapon's colour (sRGB) and glow on the Mark piece.</summary>
            public int Color=0xffffff;public float Glow;
            public Vector3 Right=>Vector3.Normalize(Vector3.Cross(Up,Along));
        }

        // ---- the fits ----
        // Daggers: the sword pack's gladius (the falchion and greatsword's style), its grip in the fist at the reverse grip's
        // centre (NativeVisualRig.Blade, (0,0,.01)), its point at the game's .832; both hands.
        // Shield: the knights pack's quartered heater (blue and violet field, steel rim), its left arm loop at the grip bar the
        // braced fist reaches (NativeVisualRig.ShieldBar, (0,.09,.15)), the second loop on the forearm's side, height to the
        // game's (-.34 to .425); its top dips at the middle, 5 cm under the game's edge socket.
        // Bow: the low-poly pack's longbow (its baked string and nocked arrow dropped), its string nocks on the game's anchors
        // (NativeBow.Anchor at rest), the limbs bending about the game's limb pivots (NativeBow.PivotY), the grip in the fist.
        // Staffs: the fantasy pack's crystal staff, the left fist closed on its shaft at the game's main grip; the four schools'
        // shortened to the game's (head and crystal at the head's .75, the shaft at .54: the crystal on the staff tip, the charge
        // orb's muzzle at .88 inside the hook), L33t H4x's at .935 of its own length (its muzzle at the top, 1.52). The crystal
        // takes each school's core colour from its Standard staff (NativeWeaponModels.Magic).
        public static readonly Fit[] Fits=Make();
        private static Fit[] Make()
        {
            var list=new List<Fit>();
            list.Add(new Fit{Spec="daggers",Prop="weapon-short-sword",Grip=new Vector3(0,0,0),Along=new Vector3(0,-1,0),Up=new Vector3(1,0,0),Scale=1.19f,From=new[]{0f,.691f},To=new[]{0f,.822f},At=new Vector3(0,0,.01f),Dual=true,Tip=new Vector3(0,-.691f,0),End=new Vector3(0,.145f,0),Handle=.019f});
            list.Add(new Fit{Spec="shield",Prop="weapon-quartered-shield",Grip=new Vector3(-.076f,-.002f,0),Along=new Vector3(0,0,1),Up=new Vector3(0,1,0),Scale=1.12f,From=new[]{0f,1f},To=new[]{0f,1.12f},At=new Vector3(0,.09f,.15f),Tip=new Vector3(-.089f,.246f,.094f),End=new Vector3(-.076f,-.38f,.06f),Handle=.012f});
            list.Add(new Fit{Spec=NativeBowWire.Spec,Prop="weapon-long-bow",Grip=new Vector3(0,.009f,-.123f),Along=new Vector3(0,0,1),Up=new Vector3(0,1,0),Scale=1.141f,From=new[]{0f,1f},To=new[]{0f,1.141f},At=new Vector3(0,0,NativeBow.TipZ),Left=true,Limbs=true,KeepLargest=true,Tip=new Vector3(0,.554f,-.123f),End=new Vector3(0,-.536f,-.123f),Handle=.03f});
            foreach(var school in new[]{("fire",0xff7c3e,1.6f),("water",0x65d8ed,1.3f),("lightning",0xc2a2ff,1.6f),("darkness",0xb98bff,1.5f)})
                list.Add(new Fit{Spec=school.Item1,Prop="weapon-crystal-staff",Grip=new Vector3(0,-.2f,-.032f),Along=new Vector3(0,1,0),Up=new Vector3(0,0,1),Scale=.75f,From=new[]{-.663f,.875f,1.27f,1.525f},To=new[]{-.3592f,.474f,.77f,.9615f},Left=true,Seat=true,Mark=new Vector3(0,1.07f,-.0215f),Tip=new Vector3(0,1.07f,-.0215f),End=new Vector3(0,1.325f,-.032f),Handle=.03f,Color=school.Item2,Glow=school.Item3});
            list.Add(new Fit{Spec="leet",Prop="weapon-crystal-staff",Grip=new Vector3(0,-.31f,-.032f),Along=new Vector3(0,1,0),Up=new Vector3(0,0,1),Scale=.935f,From=new[]{0f,1f},To=new[]{0f,.935f},Left=true,Seat=true,Mark=new Vector3(0,1.07f,-.0215f),Tip=new Vector3(0,1.07f,-.0215f),End=new Vector3(0,1.325f,-.032f),Handle=.03f,Color=0x52ff83,Glow=1.4f});
            return list.ToArray();
        }
        public static Fit For(string spec){if(spec==null)return null;foreach(var f in Fits)if(f.Spec==spec)return f;return null;}

        // ---- placing ----
        /// <summary>Where the axis map takes a prop distance `a` from the grip (frame z from At's), and its slope there.</summary>
        public static float Axial(Fit f,float a,out float slope)
        {
            var x=f.From;var y=f.To;int n=Math.Min(x.Length,y.Length);
            if(n<2){slope=f.Scale;return a*f.Scale;}
            int i=0;while(i<n-2&&a>x[i+1])i++;
            slope=(y[i+1]-y[i])/Math.Max(1e-6f,x[i+1]-x[i]);return y[i]+(a-x[i])*slope;
        }
        /// <summary>A prop-space point in the weapon frame.</summary>
        public static Vector3 Place(Fit f,Vector3 p)
        {
            var d=p-f.Grip;float z=Axial(f,Vector3.Dot(d,f.Along),out _);
            return f.At+new Vector3(Vector3.Dot(d,f.Right)*f.Scale,Vector3.Dot(d,f.Up)*f.Scale,z);
        }
        /// <summary>A prop-space normal at `p` in the weapon frame (the inverse transpose of the local stretch).</summary>
        public static Vector3 PlaceNormal(Fit f,Vector3 p,Vector3 n)
        {
            Axial(f,Vector3.Dot(p-f.Grip,f.Along),out float k);
            var o=new Vector3(Vector3.Dot(n,f.Right)/f.Scale,Vector3.Dot(n,f.Up)/f.Scale,Vector3.Dot(n,f.Along)/Math.Max(1e-4f,k));
            float l=o.Length();return l>1e-8f?o/l:Vector3.UnitZ;
        }
        /// <summary>A bow vertex's limb weight at frame height `y`: 0 on the riser, rising from the limb pivot to 1 at
        /// PivotY + LimbBlend and beyond (the tips bend exactly as the game's limbs, so the string's anchors stay on them);
        /// signed: + the upper limb, - the lower.</summary>
        public const float LimbBlend=.16f;
        public static float LimbWeight(float y)
        {
            float t=Math.Clamp((Math.Abs(y)-NativeBow.PivotY)/LimbBlend,0,1);t=t*t*(3-2*t);return y<0?-t:t;
        }
        /// <summary>A point of the bow at rest (frame) bent by `pull` as its weight takes it (the limb piece's turn about its pivot).</summary>
        public static Vector3 Bent(Vector3 rest,float pull)
        {
            float w=LimbWeight(rest.Y);if(w==0)return rest;bool top=w>0;
            // Unity frame: x mirrored from the bow frame, which the limb's turn about x leaves alone.
            var s=new Vector3(-rest.X,rest.Y,rest.Z);var b=NativeBow.Limb(top,s,pull);
            return Vector3.Lerp(rest,new Vector3(-b.X,b.Y,b.Z),Math.Abs(w));
        }

        // ---- shaping ----
        /// <summary>One weapon's mesh in its frame: positions, normals and uvs, each vertex's limb weight (bow; else 0), and the
        /// triangles per bank material (Material -1: the weapon-colour piece).</summary>
        public sealed class Shaped
        {
            public Vector3[] Positions,Normals;public Vector2[] Uv;public float[] Limb;
            public readonly List<(int Material,int[] Indices)> Subs=new List<(int,int[])>();
            public int Triangles{get{int n=0;foreach(var s in Subs)n+=s.Indices.Length/3;return n;}}
        }
        /// <summary>The fit's prop from `bank` shaped into its frame; null when the bank has no such prop.</summary>
        public static Shaped Shape(NativePackBank.Bank bank,Fit f)
        {
            var prop=bank?.Find(f?.Prop);if(prop==null||prop.Parts.Length==0)return null;
            // Every part in prop space, its triangles by material.
            var pos=new List<Vector3>();var nrm=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<(int mat,int a,int b,int c)>();
            foreach(var part in prop.Parts)
            {
                if(part.Mesh<0||part.Mesh>=bank.Meshes.Count)continue;var m=bank.Meshes[part.Mesh];int start=pos.Count;
                Matrix4x4.Invert(part.Place,out var inv);var nm=Matrix4x4.Transpose(inv);
                for(int i=0;i<m.Positions.Length;i++)
                {
                    pos.Add(Vector3.Transform(m.Positions[i],part.Place));
                    var n=Vector3.TransformNormal(m.Normals[i],nm);float l=n.Length();nrm.Add(l>1e-8f?n/l:Vector3.UnitY);
                    uv.Add(m.Uv0!=null&&i<m.Uv0.Length?m.Uv0[i]:Vector2.Zero);
                }
                foreach(var s in m.Submeshes)for(int t=0;t+2<s.Indices.Length;t+=3)tris.Add((s.Material,start+s.Indices[t],start+s.Indices[t+1],start+s.Indices[t+2]));
            }
            // Connected pieces (vertices welded where they meet), for the largest piece and the marked one.
            int[] piece=null;
            if(f.KeepLargest||!float.IsNaN(f.Mark.X))piece=Pieces(pos,tris);
            bool[] keep=new bool[tris.Count];bool[] marked=new bool[tris.Count];for(int t=0;t<keep.Length;t++)keep[t]=true;
            if(piece!=null)
            {
                var size=new Dictionary<int,int>();foreach(var t in tris){int r=piece[t.a];size[r]=size.TryGetValue(r,out int c)?c+1:1;}
                if(f.KeepLargest){int best=-1,most=-1;foreach(var kv in size)if(kv.Value>most||kv.Value==most&&kv.Key<best){most=kv.Value;best=kv.Key;}for(int t=0;t<tris.Count;t++)keep[t]=piece[tris[t].a]==best;}
                if(!float.IsNaN(f.Mark.X))
                {
                    var sum=new Dictionary<int,(Vector3 s,int n)>();
                    for(int i=0;i<pos.Count;i++){int r=piece[i];sum[r]=sum.TryGetValue(r,out var q)?(q.s+pos[i],q.n+1):(pos[i],1);}
                    int near=-1;float d=float.MaxValue;foreach(var kv in sum){if(!size.ContainsKey(kv.Key))continue;float e=Vector3.DistanceSquared(kv.Value.s/kv.Value.n,f.Mark);if(e<d||e==d&&kv.Key<near){d=e;near=kv.Key;}}
                    for(int t=0;t<tris.Count;t++)marked[t]=piece[tris[t].a]==near;
                }
            }
            // The kept vertices in the frame, compacted.
            var map=new int[pos.Count];for(int i=0;i<map.Length;i++)map[i]=-1;var outP=new List<Vector3>();var outN=new List<Vector3>();var outU=new List<Vector2>();
            int Use(int i){if(map[i]<0){map[i]=outP.Count;outP.Add(Place(f,pos[i]));outN.Add(PlaceNormal(f,pos[i],nrm[i]));outU.Add(uv[i]);}return map[i];}
            var bySub=new SortedDictionary<int,List<int>>();
            for(int t=0;t<tris.Count;t++)
            {
                if(!keep[t])continue;int mat=marked[t]?-1:tris[t].mat;
                if(!bySub.TryGetValue(mat,out var list))bySub[mat]=list=new List<int>();
                list.Add(Use(tris[t].a));list.Add(Use(tris[t].b));list.Add(Use(tris[t].c));
            }
            var shaped=new Shaped{Positions=outP.ToArray(),Normals=outN.ToArray(),Uv=outU.ToArray(),Limb=new float[outP.Count]};
            if(f.Limbs)for(int i=0;i<shaped.Positions.Length;i++)shaped.Limb[i]=LimbWeight(shaped.Positions[i].Y);
            foreach(var kv in bySub)if(kv.Key>=0)shaped.Subs.Add((kv.Key,kv.Value.ToArray()));
            if(bySub.TryGetValue(-1,out var tint))shaped.Subs.Add((-1,tint.ToArray()));
            return shaped;
        }
        // Each vertex's connected piece (a root vertex index): triangles join their corners, and coincident vertices (within
        // .1 mm, the seams a model's split normals and uvs leave) are one.
        private static int[] Pieces(List<Vector3> pos,List<(int mat,int a,int b,int c)> tris)
        {
            var root=new int[pos.Count];for(int i=0;i<root.Length;i++)root[i]=i;
            int Find(int x){while(root[x]!=x){root[x]=root[root[x]];x=root[x];}return x;}
            void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b){if(a<b)root[b]=a;else root[a]=b;}}
            var seen=new Dictionary<(int,int,int),int>();
            for(int i=0;i<pos.Count;i++){var k=((int)MathF.Round(pos[i].X*1e4f),(int)MathF.Round(pos[i].Y*1e4f),(int)MathF.Round(pos[i].Z*1e4f));if(seen.TryGetValue(k,out int j))Join(i,j);else seen[k]=i;}
            foreach(var t in tris){Join(t.a,t.b);Join(t.b,t.c);}
            for(int i=0;i<root.Length;i++)root[i]=Find(i);
            return root;
        }
    }
}
