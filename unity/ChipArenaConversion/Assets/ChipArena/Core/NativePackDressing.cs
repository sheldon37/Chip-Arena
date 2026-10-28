using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The pack dressing table (Resources/PackDressing.json; docs/arenas/STAGE-KITS.md): which kit prop each map's blocks
    /// draw as (rules) and the presentation-only harbour yards round the map (yards). Names only: the props come from the bank.</summary>
    [Serializable] public sealed class PackDressingSet {public int schema;public string bank,note;public PackKit[] kits;public PackMapDressing[] maps;}
    /// <summary>A kit: how a block's box is filled with bank props. `fill` puts one prop in the box, `grid` puts `along` × `across`
    /// props side by side (along the box's longer side and across it); either may stand on a `pallet` prop `palletHeight` m
    /// tall. `stretch` (0..1) lets each prop grow toward its slot per axis (0 keeps its proportions).</summary>
    [Serializable] public sealed class PackKit {public string id,layout,note;public string[] props,pallet;public float stretch,palletHeight;public int along,across;}
    /// <summary>A block rule: the block id (exact, or a `*` glob as MapLooks' blockLooks), the kit, and the last damage stage the
    /// prop draws at (`keep`, 0..2; 0 means the default <see cref="NativePackDressing.KeepDefault"/>): past it the block draws as today.</summary>
    [Serializable] public sealed class PackRule {public string block,kit,note;public int keep;}
    /// <summary>One map's entry: its block rules, its yards (Core NativePackScenery) and the lights atlas' night level (`glow`).</summary>
    [Serializable] public sealed class PackMapDressing {public string map,note;public float glow;public PackRule[] rules;public PackYard[] yards;}
    /// <summary>A yard piece round the map (Core NativePackScenery): its kind, its anchor and turn (degrees, Unity yaw), its extent
    /// (length along the turned x, depth along the turned z), a height or tier count, a count, a seed, optional props in place
    /// of the kind's, and `detail` (left out on the low tier).</summary>
    [Serializable] public sealed class PackYard {public string kind,note;public float x,z,yaw,length,depth,height;public int count,seed;public bool detail;public string[] props;}

    /// <summary>One bank prop placed in a block: the prop and its place (prop space to block space: the block's centre and axes, metres;
    /// System.Numerics row vectors, v' = v · M).</summary>
    public struct PackPiece {public int Prop;public Matrix4x4 Place;}
    /// <summary>A block drawn as bank props: its index in the map's blocks, its kit, the last damage stage the props draw at, the pieces.</summary>
    public sealed class PackDress {public int Block;public string Id,Kit;public int Keep;public PackPiece[] Pieces;}
    /// <summary>A map's plan: the dressed blocks (by index and by id), why every other ruled block keeps its box, the yards' glow.</summary>
    public sealed class PackPlan
    {
        public string Map;public float Glow;public PackDress[] ByBlock=Array.Empty<PackDress>();public readonly Dictionary<string,PackDress> ById=new Dictionary<string,PackDress>(StringComparer.Ordinal);
        public readonly List<string> Kept=new List<string>();public int Dressed=>ById.Count;
        public PackDress For(string id)=>id!=null&&ById.TryGetValue(id,out var d)?d:null;
    }

    /// <summary>
    /// Pack dressing (docs/arenas/STAGE-KITS.md): a map's blocks drawn as the owner's kit props (the pack bank, Core NativePackBank)
    /// in place of their boxes. Presentation only: each block keeps its collider, health, stages and every effect that reads its
    /// transform; Runtime/NativeArenaDressing draws the plan's pieces where it would draw the box. The rules here say, from the
    /// table (PackDressing.json) and the bank, which prop each ruled block draws, deterministically (the block id's hash picks the
    /// prop and its half turn; never random per frame), and when a block keeps its box:
    ///  - no bank, no map entry or no rule: every block keeps its box (today's look);
    ///  - only `static` and `cover` blocks of a non-foundation material, outside every collapse segment (its members and supports
    ///    sag and fall with the destruction kit), at most <see cref="MaxSide"/> m on a side and <see cref="MaxHeight"/> m tall;
    ///  - a kit whose props are all in the bank and all <see cref="Solid"/> groups (a prop that reads as a closed volume: an open
    ///    frame, fence or vehicle would show gaps where the collider stops shots);
    ///  - the fit (<see cref="NativePackBank.Fit"/> per slot): nothing proud of the collider (to 1 mm, inside the stage rule of at
    ///    most 0.25 m below 3 m), and no slot left more than <see cref="MaxGap"/> m short of its box on a side or at its top, so no
    ///    invisible collider edge stands out of the prop;
    ///  - at runtime: fragments and rubble, and a dressed block past its kept damage stage (S2 and the stub by default), draw boxes.
    /// Pure and deterministic; allocates only while planning.
    /// </summary>
    public static class NativePackDressing
    {
        public const string Resource="PackDressing";public const int Schema=1,KeepDefault=1;
        public const float MaxGap=.25f,Proud=.25f,ProudBelow=3f,Tolerance=.001f,MaxSide=13f,MaxHeight=6.5f;
        /// <summary>The bank groups (tools/pack_bank/selections/kit-harbor.json) a block may draw as: closed volumes.</summary>
        public static readonly string[] Solid={"container","crate","crate-metal","barrel-cage","pallet","locker","cabinet","barrel","gas-barrel","box","wood-stack","bricks","concrete-block","tank"};
        public static readonly string[] Layouts={"fill","grid"};

        /// <summary>Throws on a malformed table: schema, kit ids unique, layouts known, counts and stretch in range, rules naming kits.</summary>
        public static void Validate(PackDressingSet set)
        {
            if(set==null)throw new ArgumentNullException(nameof(set));if(set.schema!=Schema)throw new InvalidOperationException("pack dressing schema "+set.schema);
            var kits=new HashSet<string>(StringComparer.Ordinal);
            foreach(var k in set.kits??Array.Empty<PackKit>())
            {
                if(k?.id==null||!kits.Add(k.id))throw new InvalidOperationException("pack kit id "+k?.id);
                if(Array.IndexOf(Layouts,k.layout)<0)throw new InvalidOperationException("pack kit "+k.id+" layout "+k.layout);
                if(k.props==null||k.props.Length==0)throw new InvalidOperationException("pack kit "+k.id+" has no props");
                if(!(k.stretch>=0&&k.stretch<=1)||k.along<0||k.across<0||k.along>6||k.across>6||!(k.palletHeight>=0&&k.palletHeight<1))throw new InvalidOperationException("pack kit "+k.id+" counts");
                if(k.pallet!=null&&k.pallet.Length>0&&!(k.palletHeight>0))throw new InvalidOperationException("pack kit "+k.id+" pallet height");
            }
            var maps=new HashSet<string>(StringComparer.Ordinal);
            foreach(var m in set.maps??Array.Empty<PackMapDressing>())
            {
                if(m?.map==null||!maps.Add(m.map))throw new InvalidOperationException("pack map "+m?.map);if(!(m.glow>=0&&m.glow<=2))throw new InvalidOperationException("pack map "+m.map+" glow");
                foreach(var r in m.rules??Array.Empty<PackRule>())if(r?.block==null||r.kit==null||!kits.Contains(r.kit)||r.keep<0||r.keep>2)throw new InvalidOperationException("pack rule "+r?.block+" in "+m.map);
                foreach(var y in m.yards??Array.Empty<PackYard>())if(y?.kind==null||Array.IndexOf(NativePackScenery.Kinds,y.kind)<0)throw new InvalidOperationException("pack yard "+y?.kind+" in "+m.map);
            }
        }
        public static PackMapDressing Entry(PackDressingSet set,string mapId){if(set?.maps==null||mapId==null)return null;foreach(var m in set.maps)if(m?.map==mapId)return m;return null;}
        public static PackKit Kit(PackDressingSet set,string id){if(set?.kits==null)return null;foreach(var k in set.kits)if(k?.id==id)return k;return null;}
        /// <summary>The rule for a block id: an exact id wins over a glob, the first glob over later ones (MapLooks' order).</summary>
        public static PackRule Rule(PackMapDressing m,string id)
        {
            if(m?.rules==null||id==null)return null;PackRule best=null;int score=-1;
            foreach(var r in m.rules){if(r?.block==null)continue;int s=r.block.IndexOf('*')<0?(r.block==id?2:-1):NativeBlockLooks.Glob(r.block,id)?1:-1;if(s>score){score=s;best=r;}}
            return best;
        }

        /// <summary>The plan for a map (null without a bank or an entry: every block keeps its box).</summary>
        public static PackPlan Plan(PackDressingSet set,ContentMap map,NativePackBank.Bank bank)
        {
            if(set==null||map?.blocks==null||bank==null)return null;var entry=Entry(set,map.id);if(entry==null)return null;
            var plan=new PackPlan{Map=map.id,Glow=entry.glow,ByBlock=new PackDress[map.blocks.Length]};
            for(int i=0;i<map.blocks.Length;i++)
            {
                var b=map.blocks[i];var rule=Rule(entry,b?.id);if(rule==null)continue;
                if(!Eligible(b,map,out string why)){plan.Kept.Add(b.id+": "+why);continue;}
                var kit=Kit(set,rule.kit);var d=Dress(kit,b,bank,out why);if(d==null){plan.Kept.Add(b.id+": "+why);continue;}
                d.Block=i;d.Keep=rule.keep>0?rule.keep:KeepDefault;plan.ByBlock[i]=d;if(!plan.ById.ContainsKey(b.id))plan.ById[b.id]=d;
            }
            return plan;
        }
        /// <summary>Whether a block may draw as props at all (see the class rules); `why` names the first rule it fails.</summary>
        public static bool Eligible(ContentBlock b,ContentMap map,out string why)
        {
            why=null;if(b?.id==null){why="no id";return false;}
            if(b.role!="static"&&b.role!="cover"){why="role "+b.role;return false;}
            if(b.material=="foundation"){why="foundation";return false;}
            if(Math.Max(b.size.x,b.size.z)>MaxSide||b.size.y>MaxHeight){why="too big";return false;}
            if(!(b.size.x>0&&b.size.y>0&&b.size.z>0)){why="empty";return false;}
            foreach(var s in map?.segments??Array.Empty<StructureDefinition>())
            {
                if(s==null)continue;if(s.id!=null&&s.id==b.segment){why="segment "+s.id;return false;}
                if(s.blocks!=null&&Array.IndexOf(s.blocks,b.id)>=0){why="segment "+s.id;return false;}
                if(s.supports!=null)foreach(var p in s.supports)if(p?.member==b.id){why="support of "+s.id;return false;}
            }
            return true;
        }
        /// <summary>The prop index in the bank for each of a kit list's names (-1 where missing).</summary>
        public static int[] Props(NativePackBank.Bank bank,string[] names)
        {
            var o=new int[names?.Length??0];for(int i=0;i<o.Length;i++)o[i]=bank!=null&&names[i]!=null&&bank.PropIndex.TryGetValue(names[i],out int k)?k:-1;return o;
        }
        public static bool IsSolid(NativePackBank.Prop p)=>p!=null&&Array.IndexOf(Solid,p.Group)>=0;
        /// <summary>The slot pick: the block id's hash (FNV-1a, NativeBlockLooks.Hash) mixed with the slot number.</summary>
        public static uint Pick(string id,int slot){uint h=NativeBlockLooks.Hash(id);unchecked{h^=(uint)(slot+1)*2654435761u;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;}return h;}
        /// <summary>A prop's quarter turns for a slot: its longer footprint side along the slot's, the half turn by the hash; a
        /// near-square prop (sides within 15%) takes any quarter turn.</summary>
        public static int Yaw(NativePackBank.Prop p,float sx,float sz,uint h)
        {
            var s=p.Size;float lo=Math.Min(s.X,s.Z),hi=Math.Max(s.X,s.Z);if(hi<=lo*1.15f)return (int)(h&3);
            return ((sx>=sz)==(s.X>=s.Z)?0:1)+2*(int)((h>>2)&1);
        }

        /// <summary>The kit's pieces in a block's box (block space), or null with the reason (a missing or open prop, a gap, a fit
        /// proud of the box).</summary>
        public static PackDress Dress(PackKit kit,ContentBlock b,NativePackBank.Bank bank,out string why)
        {
            why=null;if(kit==null){why="no kit";return null;}
            var props=Props(bank,kit.props);var pallets=Props(bank,kit.pallet);
            foreach(int k in props)if(k<0){why="prop missing";return null;}else if(!IsSolid(bank.Props[k])){why="open prop "+bank.Props[k].Name;return null;}
            foreach(int k in pallets)if(k<0){why="pallet missing";return null;}else if(!IsSolid(bank.Props[k])){why="open pallet "+bank.Props[k].Name;return null;}
            float hx=b.size.x*.5f,hy=b.size.y*.5f,hz=b.size.z*.5f;var pieces=new List<PackPiece>();int slot=0;float floor=-hy;
            if(pallets.Length>0)
            {
                float ph=kit.palletHeight;if(ph>=b.size.y-.1f){why="pallet too tall";return null;}
                if(!Place(bank,pallets,b.id,slot++,new Vector3(0,floor+ph*.5f,0),new Vector3(b.size.x,ph,b.size.z),1,pieces,out why))return null;floor+=ph;
            }
            float top=hy-floor;bool alongX=b.size.x>=b.size.z;int along=kit.layout=="grid"?Math.Max(1,kit.along):1,across=kit.layout=="grid"?Math.Max(1,kit.across):1;
            int nx=alongX?along:across,nz=alongX?across:along;float cx=b.size.x/nx,cz=b.size.z/nz;
            for(int ix=0;ix<nx;ix++)for(int iz=0;iz<nz;iz++)
            {
                var centre=new Vector3(-hx+cx*(ix+.5f),floor+top*.5f,-hz+cz*(iz+.5f));
                if(!Place(bank,props,b.id,slot++,centre,new Vector3(cx,top,cz),kit.stretch,pieces,out why))return null;
            }
            var d=new PackDress{Id=b.id,Kit=kit.id,Pieces=pieces.ToArray()};
            if(!Inside(bank,d,b.size,out why))return null;
            return d;
        }
        // One slot: the hash's prop, its turn and fit, then the gap rule (each side and the top within MaxGap of the slot).
        private static bool Place(NativePackBank.Bank bank,int[] props,string id,int slot,Vector3 centre,Vector3 size,float stretch,List<PackPiece> pieces,out string why)
        {
            why=null;uint h=Pick(id,slot);int k=props[(int)(h%(uint)props.Length)];var p=bank.Props[k];int yaw=Yaw(p,size.X,size.Z,h>>8);
            var m=NativePackBank.Fit(p,size,yaw,stretch)*Matrix4x4.CreateTranslation(centre);
            Bounds(p,m,out var lo,out var hi);
            float gx=Math.Max(lo.X-(centre.X-size.X*.5f),(centre.X+size.X*.5f)-hi.X),gz=Math.Max(lo.Z-(centre.Z-size.Z*.5f),(centre.Z+size.Z*.5f)-hi.Z),gy=(centre.Y+size.Y*.5f)-hi.Y;
            if(gx>MaxGap+Tolerance||gz>MaxGap+Tolerance||gy>MaxGap+Tolerance){why="gap "+Math.Max(gx,Math.Max(gy,gz)).ToString("0.00")+" m ("+p.Name+")";return false;}
            pieces.Add(new PackPiece{Prop=k,Place=m});return true;
        }
        /// <summary>A prop's bounds under a place (its bounds box's eight corners transformed).</summary>
        public static void Bounds(NativePackBank.Prop p,Matrix4x4 m,out Vector3 lo,out Vector3 hi)
        {
            lo=new Vector3(float.MaxValue);hi=new Vector3(float.MinValue);
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?p.Min.X:p.Max.X,(c&2)==0?p.Min.Y:p.Max.Y,(c&4)==0?p.Min.Z:p.Max.Z),m);lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);}
        }
        /// <summary>Whether every piece lies inside the block's box (block space, to <see cref="Tolerance"/>): never proud of the collider.</summary>
        public static bool Inside(NativePackBank.Bank bank,PackDress d,V3 size,out string why)
        {
            why=null;var h=new Vector3(size.x*.5f,size.y*.5f,size.z*.5f)+new Vector3(Tolerance);
            foreach(var piece in d.Pieces){Bounds(bank.Props[piece.Prop],piece.Place,out var lo,out var hi);if(lo.X<-h.X||lo.Y<-h.Y||lo.Z<-h.Z||hi.X>h.X||hi.Y>h.Y||hi.Z>h.Z){why="proud of the box ("+bank.Props[piece.Prop].Name+")";return false;}}
            return true;
        }
        /// <summary>The largest distance any piece stands out of the box below <see cref="ProudBelow"/> m over its base (the stage rule,
        /// 0 for an inside fit), for the checks.</summary>
        public static float ProudOf(NativePackBank.Bank bank,PackDress d,V3 size)
        {
            float worst=0;var h=new Vector3(size.x*.5f,size.y*.5f,size.z*.5f);
            foreach(var piece in d.Pieces){Bounds(bank.Props[piece.Prop],piece.Place,out var lo,out var hi);if(lo.Y+h.Y>ProudBelow)continue;worst=Math.Max(worst,Math.Max(Math.Max(-h.X-lo.X,hi.X-h.X),Math.Max(-h.Z-lo.Z,hi.Z-h.Z)));}
            return worst;
        }

        /// <summary>Whether the prop draws in a block's place at this damage stage (S0 to its kept stage; a fragment never).</summary>
        public static bool Draws(PackDress d,int stage,bool fragment)=>d!=null&&!fragment&&stage<=d.Keep;

        /// <summary>
        /// Whether a kit part (a presentation box of a map's dressing kit: centre, half extents, rotation x y z w, in the world)
        /// belongs to a dressed block's box and stops drawing with it: it lies inside the box grown by <see cref="Proud"/> m on the
        /// sides and below, and starts below the box's top (a part resting on the top, such as scrap heaped in a box or a sign over
        /// a cabinet, stays). Judged in the block's own frame.
        /// </summary>
        public static bool Covers(ContentBlock b,double x,double y,double z,double hx,double hy,double hz,double qx,double qy,double qz,double qw)
        {
            if(b==null)return false;var r=b.rotation;double bx=r?.x??0,by=r?.y??0,bz=r?.z??0,bw=r==null?1:r.w;
            // The part's centre and rotation into the block's frame (its rotation's inverse).
            Rot(-bx,-by,-bz,bw,x-b.pos.x,y-b.pos.y,z-b.pos.z,out double lx,out double ly,out double lz);
            double ix=-bx,iy=-by,iz=-bz,iw=bw;double px=iw*qx+ix*qw+iy*qz-iz*qy,py=iw*qy-ix*qz+iy*qw+iz*qx,pz=iw*qz+ix*qy-iy*qx+iz*qw,pw=iw*qw-ix*qx-iy*qy-iz*qz;
            return CoversLocal(b,lx,ly,lz,hx,hy,hz,px,py,pz,pw);
        }
        /// <summary><see cref="Covers"/> for a part given in its block's frame (centre relative to the block's centre, rotation
        /// relative to the block's), as the station and town kits store them.</summary>
        public static bool CoversLocal(ContentBlock b,double x,double y,double z,double hx,double hy,double hz,double qx,double qy,double qz,double qw)
        {
            if(b==null)return false;double ex=b.size.x*.5+Proud,ey=b.size.y*.5,ez=b.size.z*.5+Proud;double minY=double.MaxValue,maxY=double.MinValue,ax=0,az=0;
            for(int c=0;c<8;c++)
            {
                Rot(qx,qy,qz,qw,(c&1)==0?-hx:hx,(c&2)==0?-hy:hy,(c&4)==0?-hz:hz,out double wx,out double wy,out double wz);
                ax=Math.Max(ax,Math.Abs(x+wx));az=Math.Max(az,Math.Abs(z+wz));minY=Math.Min(minY,y+wy);maxY=Math.Max(maxY,y+wy);
            }
            return ax<=ex&&az<=ez&&minY>=-ey-Proud&&minY<ey-.02&&maxY<=ey+Proud;
        }
        private static void Rot(double qx,double qy,double qz,double qw,double vx,double vy,double vz,out double ox,out double oy,out double oz)
        {double tx=2*(qy*vz-qz*vy),ty=2*(qz*vx-qx*vz),tz=2*(qx*vy-qy*vx);ox=vx+qw*tx+(qy*tz-qz*ty);oy=vy+qw*ty+(qz*tx-qx*tz);oz=vz+qw*tz+(qx*ty-qy*tx);}
    }
}
