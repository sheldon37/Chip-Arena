using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

// Meridian's flights (owner, 2026-11-20: "a lot of unnatural staircasing"; Core NativeMeridianDressStairs): every street plan's
// live deal dressed as the live run dresses it, and on each flight (Astra's MapRamps: the stairs, the sloped streets and the fire
// escapes, whose pilot colliders are closed wedges up to their slope):
//  - no side stands over the flight's line (a sloped street's surface, a stair's nosings), and where a side is seen it reaches
//    that line (the treads' flat tops stepped under and over it: a sawtooth on every retaining edge, cheek and fire escape);
//  - a wall beside a sloped street is drawn down to the street (it stopped at the treads' tops: notches open above the setts);
//  - a fire escape's treads carry only iron and nothing solid taller than its stringer (they stood as solid brick wedges), its
//    sides are caged below the stringer by bars no farther apart than BarStep (the wedge is solid: honest, and no pilot passes
//    the gap), and the floor runs on under the cage where the flight stands on it (seen through the grille);
//  - every trapezoid surface is planar and wound as its face.
internal static class MeridianStairsChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Meridian stairs: "+what);}
    private static float Clamp(float x,float a,float b)=>x<a?a:x>b?b:x;
    private static IEnumerable<V3> Corners(NativeMeridianDress.Quad q){yield return q.O;yield return q.O+q.U;yield return q.O+q.U+q.Vb;yield return q.O+q.V;}
    /// <summary>Whether a point lies on a generated surface (within `slack` m of its plane, inside its rectangle or trapezoid).</summary>
    private static bool InQuad(in NativeMeridianDress.Quad q,V3 p,float slack=.02f)
    {
        var dp=p-q.O;float nl=q.N.Length;if(nl<1e-4f||Math.Abs(V3.Dot(dp,q.N)/nl)>slack)return false;float lu=V3.Dot(q.U,q.U);if(lu<1e-6f)return false;
        float s=V3.Dot(dp,q.U)/lu;if(s<-.005f||s>1.005f)return false;var v=q.V+(q.Vb-q.V)*Clamp(s,0,1);float lv=V3.Dot(v,v);if(lv<1e-6f)return false;
        float t=V3.Dot(dp-q.U*s,v)/lv;return t>=-.005f&&t<=1.005f;
    }

    public static void Run(string root)
    {
        count=0;var report=new List<string>();
        float gap=NativeMeridianDress.BarStep-NativeMeridianDress.BarWidth;
        Check(gap<NativeMapRules.PilotRadius,"a fire escape grille's gap ("+gap+" m) is narrower than a pilot's radius (the wedge behind it is solid)");
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var plan=HubZFightChecks.Dress(live,null,false);var map=live.World.City.Map;string name=NativeCityLayout.Plans[live.Plan].Name;
            var solids=new NativeMeridianDress.Solids(map.blocks);var lots=NativeMeridianDress.Lots(plan.Layout);
            var index=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<map.blocks.Length;i++)if(map.blocks[i]?.id!=null)index[map.blocks[i].id]=i;
            var quadsOf=new Dictionary<int,List<int>>();
            for(int qi=0;qi<plan.Quads.Count;qi++){int bk=plan.Quads[qi].Block;if(bk<0)continue;if(!quadsOf.TryGetValue(bk,out var l))quadsOf[bk]=l=new List<int>();l.Add(qi);}
            var placementsOf=new Dictionary<int,List<int>>();
            var filled=new HashSet<int>();
            for(int pi=0;pi<plan.Placements.Count;pi++){int bk=plan.Placements[pi].Block;if(bk<0)continue;if(plan.Placements[pi].Face<0){filled.Add(bk);continue;}if(!placementsOf.TryGetValue(bk,out var l))placementsOf[bk]=l=new List<int>();l.Add(pi);}
            int BlockAt(V3 p,HashSet<int> skip){foreach(int i in solids.Near(p.x-.01f,p.x+.01f,p.z-.01f,p.z+.01f)){if(skip!=null&&skip.Contains(i))continue;int o=i*6;var B=solids.B;
                if(p.x>B[o]+.005f&&p.x<B[o+1]-.005f&&p.y>B[o+2]+.005f&&p.y<B[o+3]-.005f&&p.z>B[o+4]+.005f&&p.z<B[o+5]-.005f)return i;}return -1;}
            // a block's face drawn at a point: one of its surfaces, a module on that face, or a module filling the block (a post, a parapet)
            bool FaceDrawn(int bk,V3 p,V3 faceN)
            {
                if(filled.Contains(bk))return true;
                if(quadsOf.TryGetValue(bk,out var ql))foreach(int qi in ql){var q=plan.Quads[qi];if(V3.Dot(q.N,faceN)>.9f&&InQuad(q,p,.06f))return true;}
                if(placementsOf.TryGetValue(bk,out var pl))foreach(int pi in pl)
                {
                    var pc=plan.Placements[pi];if(V3.Dot(NativeMeridianDress.NormalOf(pc.Face),faceN)<.9f)continue;
                    NativeMeridianDress.FaceFrame(solids.B,bk*6,pc.Face,out var fo,out var fu,out _);float lu=fu.Length;if(lu<1e-4f)continue;
                    float a=V3.Dot(p-fo,fu/lu);if(a>=pc.U0-.02f&&a<=pc.U1+.02f&&p.y>=pc.V0-.02f&&p.y<=pc.V1+.02f)return true;
                }
                return false;
            }
            // every surface by the plane it lies in (its normal and offset to 5 mm): a side's point may be drawn by another block's face in
            // that plane (a terrace's face the flight's first tread continues: it draws it, the flight leaves it)
            var byPlane=new Dictionary<(int,int,int,long),List<int>>();
            (int,int,int,long) PlaneKey(V3 nn,V3 at)=>((int)Math.Round(nn.x*100),(int)Math.Round(nn.y*100),(int)Math.Round(nn.z*100),(long)Math.Round(V3.Dot(at,nn)*200));
            for(int qi=0;qi<plan.Quads.Count;qi++){var k=PlaneKey(plan.Quads[qi].N,plan.Quads[qi].O);if(!byPlane.TryGetValue(k,out var l))byPlane[k]=l=new List<int>();l.Add(qi);}
            bool InPlane(V3 p,V3 nn){var k=PlaneKey(nn,p);for(long dd=k.Item4-1;dd<=k.Item4+1;dd++)if(byPlane.TryGetValue((k.Item1,k.Item2,k.Item3,dd),out var l))foreach(int qi in l)if(InQuad(plan.Quads[qi],p))return true;return false;}
            int flights=0,escapes=0,sides=0,missed=0,above=0,walls=0,wallMissed=0,cage=0,gaps=0,floors=0,floorsMissed=0,slabs=0;
            foreach(var r in map.ramps??Array.Empty<MapRamp>())
            {
                var F=NativeMeridianDress.LineOf(r);bool fire=NativeMeridianDress.IsFireEscape(r.id);flights++;if(fire)escapes++;
                var own=new HashSet<int>();foreach(var id in r.blocks)if(index.TryGetValue(id,out int ti))own.Add(ti);
                foreach(int s in new[]{-1,1})
                {
                    var n=F.Across*s;var sideQuads=new List<NativeMeridianDress.Quad>();var bars=new List<NativeMeridianDress.Quad>();
                    foreach(int ti in own)if(quadsOf.TryGetValue(ti,out var ql))foreach(int qi in ql)
                    {
                        var q=plan.Quads[qi];if(Math.Abs(q.N.y)>.5f||V3.Dot(q.N,n)<.9f)continue;
                        if(q.Free)bars.Add(q);else sideQuads.Add(q);
                        // nothing of a side stands over the flight's line
                        foreach(var c in Corners(q))if(c.y>F.Line(Clamp(F.D(c),0,F.Run))+.01f){above++;if(above<4)Console.WriteLine(FormattableString.Invariant($"  {name} {r.id}: a side's corner {c.y:F3} over the line {F.Line(Clamp(F.D(c),0,F.Run)):F3} at {c.x:F2},{c.z:F2}"));}
                    }
                    var side=F.Low+F.Across*(s*F.Width*.5f);
                    bool Seen(float d,float y){foreach(float dd in new[]{-.06f,0,.06f})foreach(float dy in new[]{-.06f,0,.06f}){var q=side+F.Dir*(d+dd);q.y=y+dy;q=q+n*.05f;if(solids.Inside(q.x,q.y,q.z,-1))return false;}return true;}
                    for(float d=.1f;d<F.Run-.1f;d+=.25f)
                    {
                        float y=F.Line(d)-.05f;if(y<F.Bottom+.02f)continue;var p=side+F.Dir*d;p.y=y;
                        if(Seen(d,y)){sides++;if(!sideQuads.Any(q=>InQuad(q,p))&&!InPlane(p,n)){missed++;if(missed<4)Console.WriteLine(FormattableString.Invariant($"  {name} {r.id}: the side under its line is bare at {p.x:F2},{p.y:F2},{p.z:F2}"));}continue;}
                        if(F.Stair)continue;
                        // a wall beside a sloped street: drawn down to the street (just over its surface)
                        var w=side+F.Dir*d;w.y=F.Line(d)+.05f;int wb=BlockAt(w+n*.05f,own);if(wb<0||plan.Left.Contains(map.blocks[wb].id)||!plan.Covers.Contains(map.blocks[wb].id))continue;
                        // (a wall standing back from the street's edge is no wall on it, nor a block the street's corner is buried in)
                        if(BlockAt(w+n*.01f,own)!=wb||BlockAt(w-n*.05f,own)==wb)continue;
                        walls++;if(!FaceDrawn(wb,w,n*-1)){wallMissed++;if(wallMissed<4)Console.WriteLine(FormattableString.Invariant($"  {name} {r.id}: the wall {map.blocks[wb].id} beside it is open over the street at {w.x:F2},{w.y:F2},{w.z:F2}"));}
                    }
                    if(!fire)continue;
                    // a fire escape's grille: below its stringer, where its side is seen, a bar within half a step of every point
                    for(float d=.15f;d<F.Run-.15f;d+=.1f)for(float y=F.Bottom+.3f;y<F.Line(d)-NativeMeridianDress.Stringer-.08f;y+=.6f)
                    {
                        if(!Seen(d,y))continue;cage++;
                        bool barred=bars.Any(q=>{var c=q.O+q.U*.5f;float lo=Math.Min(q.O.y,q.O.y+q.V.y),hi=Math.Max(q.O.y,q.O.y+q.V.y);return Math.Abs(F.D(c)-d)<=NativeMeridianDress.BarStep*.5f+.011f&&y>=lo-.01f&&y<=hi+.01f;});
                        if(!barred){gaps++;if(gaps<4)Console.WriteLine(FormattableString.Invariant($"  {name} {r.id}: the cage has a gap at d {d:F2} y {y:F2} side {s}"));}
                    }
                }
                if(!fire)continue;
                // a fire escape's treads carry only iron, and nothing solid on a side taller than its stringer
                foreach(int ti in own)if(quadsOf.TryGetValue(ti,out var ql))foreach(int qi in ql)
                {
                    var q=plan.Quads[qi];
                    // (a riser, facing the flight's foot, is a step's height and the lip under the first)
                    float tall=q.Free||Math.Abs(q.N.y)>.5f||V3.Dot(q.N,F.Dir)<-.9f?0:Math.Max(Math.Abs(q.V.y),Math.Abs(q.Vb.y));
                    if(q.Fin!=NativeMeridianSurfaces.IronBlack&&q.Fin!=NativeMeridianSurfaces.IronGrate||tall>NativeMeridianDress.Stringer+.06f)
                    {slabs++;if(slabs<4)Console.WriteLine(FormattableString.Invariant($"  {name} {map.blocks[ti].id}: a surface of finish {q.Fin}, {tall:F2} m tall, at {q.O.x:F2},{q.O.y:F2},{q.O.z:F2} n {q.N.x:F1},{q.N.y:F1},{q.N.z:F1}"));}
                }
                // its floor runs on under its cage where it stands on one (seen through the grille)
                for(float d=.25f;d<F.Run-.25f;d+=.5f)foreach(float a in new[]{-.25f,.25f})
                {
                    var p=F.Low+F.Dir*d+F.Across*(a*F.Width);float fl=solids.Floor(p.x,p.z,r.low.y+.05f);if(fl<r.bottom-.01f)continue;p.y=fl;
                    int fb=-1;foreach(int i in solids.Near(p.x-.01f,p.x+.01f,p.z-.01f,p.z+.01f)){int o=i*6;var B=solids.B;if(!own.Contains(i)&&Math.Abs(B[o+3]-fl)<.005f&&p.x>=B[o]&&p.x<=B[o+1]&&p.z>=B[o+4]&&p.z<=B[o+5]){fb=i;break;}}
                    if(fb<0||!plan.Covers.Contains(map.blocks[fb].id)||NativeMeridianDress.RoleOf(map.blocks[fb].id,lots)==NativeMeridianDress.RTread)continue;
                    floors++;bool on=plan.Quads.Any(q=>q.N.y>.5f&&InQuad(q,p,.01f));if(!on){floorsMissed++;if(floorsMissed<4)Console.WriteLine(FormattableString.Invariant($"  {name} {r.id}: no floor under the cage at {p.x:F2},{p.y:F2},{p.z:F2}"));}
                }
            }
            Check(above==0,name+": "+above+" corners of flights' sides stand over their lines (a stepped profile)");
            Check(sides>500&&missed==0,name+": every seen side reaches its flight's line ("+missed+" of "+sides+" points under it bare)");
            Check(wallMissed==0,name+": every wall beside a sloped street runs down to it ("+wallMissed+" of "+walls+" points open over the setts)");
            Check(escapes>10&&slabs==0,name+": the fire escapes' treads carry only iron and nothing solid over a stringer's depth ("+slabs+" surfaces)");
            Check(cage>1000&&gaps==0,name+": every seen point of a fire escape's wedge under its stringer is caged ("+gaps+" of "+cage+" with no bar within half a step)");
            Check(floorsMissed==0,name+": the floor runs on under every fire escape's cage ("+floorsMissed+" of "+floors+" points bare)");
            // every trapezoid is planar and wound as its face
            int tapers=0;
            foreach(var q in plan.Quads)
            {
                if(!q.Taper)continue;tapers++;var nn=q.N;
                Check(Math.Abs(V3.Dot(q.V,nn))<1e-3f&&Math.Abs(V3.Dot(q.V1,nn))<1e-3f&&Math.Abs(V3.Dot(q.U,nn))<1e-3f,name+": a trapezoid lies in its plane");
                var w0=NativeMeridianDress.Cross(q.U,q.V);var w1=NativeMeridianDress.Cross(q.U,q.V1);Check((w0.Length<1e-6f||V3.Dot(w0,nn)>0)&&(w1.Length<1e-6f||V3.Dot(w1,nn)>0),name+": a trapezoid is wound as its face");
                NativeMeridianDress.EdgeOf(q,2,out var c2,out var c3);Check((NativeMeridianDress.QuadAt(q,1,1)-c2).Length<1e-4f&&(NativeMeridianDress.QuadAt(q,0,1)-c3).Length<1e-4f,name+": a trapezoid's corners are its edges'");
            }
            Check(tapers>100,name+": the flights' sides are trapezoids on their lines ("+tapers+")");
            // nothing of a flight lies in one plane with another surface, turned the same way, on one layer (a band under a first
            // tread dipped into the terrace face it continues)
            var (fights,fightArea)=Coplanar(plan,map,false);
            Check(fights==0,name+FormattableString.Invariant($": {fights} surfaces of the flights lie in one plane with another, turned the same way ({fightArea:F3} m2; --meridian-stairs-zfight lists them)"));
            report.Add(FormattableString.Invariant($"{name}: {flights} flights ({escapes} fire escape flights), {sides} side points on their lines, {walls} wall points beside sloped streets, {cage} caged points, {floors} floor points under cages, {tapers} trapezoids"));
        }
        foreach(var line in report)Console.WriteLine("  "+line);
        Console.WriteLine("PASS meridian stairs: "+count+" checks, the five plans' live deals: no stepped side on any flight, walls beside the sloped streets down to them, the fire escapes iron and caged, the floor under their cages");
    }
    // ---------------------------------------------------------------------------------------------------------------------
    // `--meridian-stairs-export PLAN X Z RADIUS OUT.ply [roles]` (CHIPARENA_MERIDIAN_BANK set): the live deal's dress within RADIUS
    // of (X, Z) at LOD0, as a PLY with vertex colours (the finishes, or with `roles` a colour per role), and the greybox it leaves.
    // ---------------------------------------------------------------------------------------------------------------------
    public static void Export(string root,int planIndex,float x,float z,float radius,string path,bool roles)
    {
        var bankPath=Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK");
        NativePackBank.Bank bank=null;Dictionary<string,int[]> modules=null;
        if(!string.IsNullOrEmpty(bankPath)&&File.Exists(bankPath)){bank=NativePackBank.Read(File.ReadAllBytes(bankPath));modules=NativeMeridianDress.Modules(bank);}
        var live=HubZFightChecks.Worlds(root).First(l=>l.Plan==planIndex);
        var plan=HubZFightChecks.Dress(live,modules!=null?modules.ContainsKey:(Func<string,bool>)null,false);
        var map=live.World.City.Map;var lots=NativeMeridianDress.Lots(plan.Layout);
        var treadRamp=new Dictionary<string,MapRamp>(StringComparer.Ordinal);foreach(var r in map.ramps??Array.Empty<MapRamp>())foreach(var id in r.blocks)treadRamp[id]=r;
        var mesh=new NativeMeridianMesh();var P=new List<float>();var C=new List<byte>();var I=new List<int>();
        bool In(float px,float pz)=>(px-x)*(px-x)+(pz-z)*(pz-z)<=radius*radius;
        void Take(byte r,byte g,byte b,bool keep)
        {
            int at=P.Count/3;
            for(int i=0;i<mesh.Vertices;i++)
            {
                P.Add(mesh.P[i*3]);P.Add(mesh.P[i*3+1]);P.Add(mesh.P[i*3+2]);
                if(keep){C.Add(mesh.C[i*4]);C.Add(mesh.C[i*4+1]);C.Add(mesh.C[i*4+2]);}else{C.Add(r);C.Add(g);C.Add(b);}C.Add(255);
            }
            foreach(int k in mesh.Solid)I.Add(at+k);foreach(int k in mesh.Glow)I.Add(at+k);
        }
        int[] Kinds=new int[16];
        for(int pi=0;pi<plan.Placements.Count;pi++)
        {
            var p=plan.Placements[pi];if(p.Dynamic||bank==null||!In(p.M[3],p.M[11]))continue;
            NativeMeridianDress.Combine(plan,p.Chunk,0,bank,modules,new List<int>{pi},null,mesh);
            if(!roles){Take(0,0,0,true);continue;}
            // modules: facade bays tan, cornices white, lamps yellow, the rest grey
            bool corn=p.Key.StartsWith("t.",StringComparison.Ordinal),fac=p.Key.StartsWith("g.",StringComparison.Ordinal)||p.Key.StartsWith("u.",StringComparison.Ordinal);
            if(corn)Take(240,240,240,false);else if(fac)Take(170,140,100,false);else if(p.Key.StartsWith("lamp.",StringComparison.Ordinal))Take(255,230,40,false);else Take(130,130,130,false);
        }
        for(int qi=0;qi<plan.Quads.Count;qi++)
        {
            var q=plan.Quads[qi];var c=q.O+q.U*.5f+q.V*.5f;if(!In(c.x,c.z))continue;
            mesh.Clear();NativeMeridianDress.AddQuad(q,0,mesh);
            if(!roles){Take(0,0,0,true);continue;}
            string id=q.Block>=0?map.blocks[q.Block].id:null;string role=id==null?"none":NativeMeridianDress.RoleOf(id,lots);
            bool up=q.N.y>.5f;
            if(role==NativeMeridianDress.RTread)
            {
                bool fire=id.StartsWith("fire-escape-",StringComparison.Ordinal);treadRamp.TryGetValue(id,out var r);bool stair=r==null||r.kind=="stair";
                bool side=!up&&Math.Abs(q.N.y)<.5f&&!(r!=null&&Riser(r,q.N));
                if(fire)Take(side?(byte)230:(byte)255,side?(byte)40:(byte)200,side?(byte)40:(byte)0,false);          // fire escape: red sides, amber treads
                else if(stair)Take(side?(byte)0:(byte)60,side?(byte)200:(byte)255,side?(byte)230:(byte)90,false);     // stairs: cyan sides, green treads
                else Take(40,80,255,false);                                                                             // sloped street treads: blue
            }
            else if(role==NativeMeridianDress.RLanding)Take(255,0,255,false);
            else if(role==NativeMeridianDress.RGround)Take(up?(byte)50:(byte)120,up?(byte)50:(byte)150,up?(byte)55:(byte)200,false);
            else if(role==NativeMeridianDress.RLot)Take(up?(byte)60:(byte)200,up?(byte)60:(byte)170,up?(byte)60:(byte)120,false);
            else if(id==null)Take(90,110,90,false);
            else Take(150,150,150,false);
        }
        // the greybox the dress leaves (halls, the court, landmarks): mid grey; the camp's fence and gates: dark green
        foreach(var blk in map.blocks)
        {
            if(blk?.id==null||plan.Covers.Contains(blk.id)||!In(blk.pos.x,blk.pos.z)||blk.id.StartsWith("camp-",StringComparison.Ordinal))continue;
            int b=P.Count/3;float hx=blk.size.x/2,hy=blk.size.y/2,hz=blk.size.z/2;bool camp=blk.id.StartsWith("camp-",StringComparison.Ordinal);
            for(int k=0;k<8;k++){P.Add(blk.pos.x+((k&1)==1?hx:-hx));P.Add(blk.pos.y+((k&2)==2?hy:-hy));P.Add(blk.pos.z+((k&4)==4?hz:-hz));C.Add(camp?(byte)40:(byte)120);C.Add(camp?(byte)90:(byte)120);C.Add(camp?(byte)50:(byte)128);C.Add(255);}
            int[] f={0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5};foreach(int k in f)I.Add(b+k);
        }
        using(var w=new BinaryWriter(File.Create(path)))
        {
            var head=string.Join("\n",new[]{"ply","format binary_little_endian 1.0","element vertex "+P.Count/3,"property float x","property float y","property float z","property uchar red","property uchar green","property uchar blue","property uchar alpha","element face "+I.Count/3,"property list uchar int vertex_indices","end_header",""});
            w.Write(System.Text.Encoding.ASCII.GetBytes(head));
            for(int i=0;i<P.Count/3;i++){w.Write(P[i*3]);w.Write(P[i*3+1]);w.Write(P[i*3+2]);w.Write(C[i*4]);w.Write(C[i*4+1]);w.Write(C[i*4+2]);w.Write(C[i*4+3]);}
            for(int i=0;i<I.Count;i+=3){w.Write((byte)3);w.Write(I[i]);w.Write(I[i+1]);w.Write(I[i+2]);}
        }
        Console.WriteLine("wrote "+path+": "+P.Count/3+" vertices, "+I.Count/3+" triangles; plan "+NativeCityLayout.Plans[planIndex].Name+" seed "+live.Seed+(bank==null?" (no bank: surfaces only)":""));
    }
    private static bool Riser(MapRamp r,V3 n)
    {
        var d=r.low-r.high;if(Math.Abs(d.x)>Math.Abs(d.z))return d.x>0?n.x>.5f:n.x<-.5f;return d.z>0?n.z>.5f:n.z<-.5f;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // `--meridian-stairs-probe`: per live plan, the stepped shapes and what the dress draws on them
    // ---------------------------------------------------------------------------------------------------------------------
    public static void Probe(string root)
    {
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var plan=HubZFightChecks.Dress(live,null,false);var map=live.World.City.Map;var lots=NativeMeridianDress.Lots(plan.Layout);
            var solids=new NativeMeridianDress.Solids(map.blocks);var B=solids.B;
            var index=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<map.blocks.Length;i++)if(map.blocks[i]?.id!=null)index[map.blocks[i].id]=i;
            var ramps=map.ramps??Array.Empty<MapRamp>();
            int fire=0,fireFlights=0,stairs=0,slopes=0;float fireSide=0,stairSide=0,slopeSide=0,fireHang=0;
            var escapes=new SortedSet<string>(StringComparer.Ordinal);
            var sideArea=new Dictionary<int,float>();
            foreach(var q in plan.Quads){if(q.Block<0||Math.Abs(q.N.y)>.5f)continue;sideArea.TryGetValue(q.Block,out float a);sideArea[q.Block]=a+Cross(q.U,q.V).Length;}
            foreach(var r in ramps)
            {
                bool isFire=r.id.StartsWith("fire-escape-",StringComparison.Ordinal);float side=0;
                foreach(var t in r.blocks){if(!index.TryGetValue(t,out int bi))continue;if(sideArea.TryGetValue(bi,out float a))side+=a;}
                if(isFire){fireFlights++;fireSide+=side;escapes.Add(r.id.Substring(0,r.id.LastIndexOf('-')));
                    // a flight that hangs: the floor under its low end is more than 2 m below its bottom
                    float under=solids.Floor(r.low.x,r.low.z,r.bottom-.05f);if(under<-1e5f||r.bottom-under>2)fireHang++;}
                else if(r.kind=="stair"){stairs++;stairSide+=side;}else{slopes++;slopeSide+=side;}
            }
            fire=escapes.Count;
            // what the lamps, plates, clocks and sirens hang on (a fire escape's grille, a flight's side)
            int onEscape=0,onFlight=0;
            foreach(var p in plan.Placements)
            {
                if(p.Block>=0||p.Paint)continue;var at=new V3(p.M[3],p.M[7],p.M[11]);var nrm=new V3(p.M[2],p.M[6],p.M[10]);float nl=nrm.Length;if(nl<1e-4f)continue;nrm=nrm/nl;
                var q=at-nrm*.06f;
                for(int i=0;i<map.blocks.Length;i++){int o=i*6;if(q.x>B[o]&&q.x<B[o+1]&&q.y>B[o+2]&&q.y<B[o+3]&&q.z>B[o+4]&&q.z<B[o+5]){var id=map.blocks[i].id;if(id.StartsWith("fire-escape-",StringComparison.Ordinal)){onEscape++;Console.WriteLine("  "+p.Key+" on "+id);}else if(id.Contains("-tread-"))onFlight++;break;}}
            }
            Console.WriteLine("  wall pieces on fire escapes "+onEscape+", on other flights' treads "+onFlight);
            Console.WriteLine(FormattableString.Invariant($"{NativeCityLayout.Plans[live.Plan].Name} (seed {live.Seed}): {fire} fire escapes ({fireFlights} flights, {fireHang} hanging), side surfaces {fireSide:N0} m2; {stairs} stairs, sides {stairSide:N0} m2; {slopes} sloped streets, sides {slopeSide:N0} m2"));
        }
    }
    private static V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);

    // `--meridian-stairs-zfight PLAN`: the generated surfaces that lie in one plane, turned the same way, and overlap (within 0.5 mm,
    // over 2 cm2), on the same layer, where either is a flight's (its treads' or a wall beside a sloped street): a diagnostic.
    public static void ZFight(string root,int planIndex)
    {
        var live=HubZFightChecks.Worlds(root).First(l=>l.Plan==planIndex);var plan=HubZFightChecks.Dress(live,null,false);
        var (pairs,total)=Coplanar(plan,live.World.City.Map,true);
        Console.WriteLine(FormattableString.Invariant($"{NativeCityLayout.Plans[planIndex].Name}: {pairs} same-facing coplanar pairs touching a flight, {total:F4} m2"));
    }
    /// <summary>The generated surfaces that lie in one plane (0.5 mm), turned the same way and on one layer, and overlap (over 2 cm2),
    /// where either is a flight's (its treads', a trapezoid, a bar): the pairs and their area (they would fight).</summary>
    private static (int,double) Coplanar(NativeMeridianDress.Plan plan,ContentMap map,bool print)
    {
        var lots=NativeMeridianDress.Lots(plan.Layout);var groups=new Dictionary<(int,int,int,long),List<int>>();
        for(int i=0;i<plan.Quads.Count;i++)
        {
            var q=plan.Quads[i];var n=q.N;int nx=(int)Math.Round(n.x*1000),ny=(int)Math.Round(n.y*1000),nz=(int)Math.Round(n.z*1000);long d=(long)Math.Round(V3.Dot(q.O,n)*2000);
            for(long dd=d-1;dd<=d+1;dd++){var k=(nx,ny,nz,dd);if(!groups.TryGetValue(k,out var l))groups[k]=l=new List<int>();if(dd==d)l.Add(i);}
        }
        double total=0;int pairs=0;var by=new Dictionary<string,double>();
        foreach(var kv in groups)
        {
            var l=kv.Value;if(l.Count<2)continue;
            for(int a=0;a<l.Count;a++)for(int c=a+1;c<l.Count;c++)
            {
                var A=plan.Quads[l[a]];var C=plan.Quads[l[c]];if(A.Layer!=C.Layer)continue;
                if(Math.Abs(V3.Dot(A.O-C.O,A.N))>.0005f)continue;
                double area=Overlap(A,C);if(area<2e-4)continue;
                string Who(NativeMeridianDress.Quad q){if(q.Block<0)return q.Taper?"taper(-)":"street";var id=map.blocks[q.Block].id;var role=NativeMeridianDress.RoleOf(id,lots);return (q.Free?"bar ":q.Taper?"taper ":"")+role+(id.StartsWith("fire-escape-",StringComparison.Ordinal)?" fire":"");}
                string wa=Who(A),wc=Who(C);bool mine=wa.Contains("tread")||wc.Contains("tread")||A.Taper||C.Taper||A.Free||C.Free||wa.Contains("fire")||wc.Contains("fire");
                if(!mine)continue;pairs++;total+=area;string key=string.CompareOrdinal(wa,wc)<0?wa+" | "+wc:wc+" | "+wa;by.TryGetValue(key,out double t);by[key]=t+area;
                if(print&&pairs<12)Console.WriteLine(FormattableString.Invariant($"  {wa} [{(A.Block>=0?map.blocks[A.Block].id:"-")}] | {wc} [{(C.Block>=0?map.blocks[C.Block].id:"-")}]: {area:F4} m2 at {A.O.x:F2},{A.O.y:F2},{A.O.z:F2} n {A.N.x:F2},{A.N.y:F2},{A.N.z:F2}"));
            }
        }
        if(print)foreach(var kv in by.OrderByDescending(k=>k.Value))Console.WriteLine(FormattableString.Invariant($"  {kv.Key}: {kv.Value:F4} m2"));
        return (pairs,total);
    }
    // the overlap area of two coplanar surfaces (convex polygons in their plane)
    private static double Overlap(NativeMeridianDress.Quad A,NativeMeridianDress.Quad C)
    {
        var n=A.N;var ax=Math.Abs(n.x)>.5f?new V3(0,1,0):new V3(1,0,0);var u=Cross(n,ax);u=u/u.Length;var w=Cross(n,u);
        List<(double,double)> Poly(NativeMeridianDress.Quad q){var r=new List<(double,double)>();foreach(var c in new[]{q.O,q.O+q.U,q.O+q.U+q.Vb,q.O+q.V})r.Add((V3.Dot(c,u),V3.Dot(c,w)));
            double s=0;for(int i=0;i<4;i++){var p=r[i];var t=r[(i+1)%4];s+=p.Item1*t.Item2-t.Item1*p.Item2;}if(s<0)r.Reverse();return r;}
        var P=Poly(A);var Q=Poly(C);
        // Sutherland-Hodgman: clip P by each edge of Q (both counter-clockwise)
        for(int i=0;i<Q.Count&&P.Count>0;i++)
        {
            var e0=Q[i];var e1=Q[(i+1)%Q.Count];var o=new List<(double,double)>();
            double Side((double,double) p)=>(e1.Item1-e0.Item1)*(p.Item2-e0.Item2)-(e1.Item2-e0.Item2)*(p.Item1-e0.Item1);
            for(int k=0;k<P.Count;k++)
            {
                var a=P[k];var b=P[(k+1)%P.Count];double sa=Side(a),sb=Side(b);
                if(sa>=0)o.Add(a);
                if(sa>=0!=sb>=0){double t=sa/(sa-sb);o.Add((a.Item1+(b.Item1-a.Item1)*t,a.Item2+(b.Item2-a.Item2)*t));}
            }
            P=o;
        }
        double area=0;for(int i=0;i<P.Count;i++){var p=P[i];var t=P[(i+1)%P.Count];area+=p.Item1*t.Item2-t.Item1*p.Item2;}
        return Math.Abs(area)*.5;
    }
}
