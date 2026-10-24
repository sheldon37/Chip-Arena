#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // A Hulk's breach of an interior Ward Zero wall drawn (owner 2026-10-13: "make the hulk punch through any wall in ward zero if
    // he cant fit"; Astra's dynamic NativeBreachRecord, the whole block's collider removed at the break). Presentation only: the
    // whole block's wall frame, hole, pieces and restore from the record (Core NativeBreach.BlockWall / BuildBlock) on every
    // eligible wall of the real map, the entry breach's timeline and the authority's collider clock, the seed from the block id,
    // determinism, no allocation, entry breaches unchanged, and a runtime that finds the block by id and hides and shows it on
    // every draw path from the replicated records.
    internal static class HulkBreachFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Hulk breach fx: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
        public static void Run(string root)
        {
            count=0;var json=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            var ward=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),json).maps.Single(m=>m.id=="siren");
            var walls=ward.blocks.Where(b=>NativeHulkBreach.Eligible(ward,b)).ToArray();Check(walls.Length>50,"the real Ward walls ("+walls.Length+")");
            Seeds();Floors(ward,walls);Blocks(ward,walls);Timeline(walls[0]);Determinism(ward,walls);Entry();Allocation(ward,walls);Runtime(root);
            Console.WriteLine("PASS hulk wall breach fx ("+count+" checks: the whole block's wall, hole and pieces on every eligible Ward wall from both sides, the burst along the Hulk's facing, the whole block rebuilt before its look returns, the entry breach's timeline on the authority's collider clock, the seed from the block id, deterministic, no allocation, entry breaches unchanged, the runtime's block lookup, hide and show)");
        }
        // A real record: a Hulk 1.6 m off the block's face (`side` of its thin axis, `along` the wall from its middle, `skew` its
        // heading off square), walking through it.
        private static NativeBreachRecord Punch(ContentBlock wall,int side,float along,float skew,float now)
        {
            bool thinX=wall.size.x<wall.size.z;var n=thinX?new V3(-side,0,0):new V3(0,0,-side);var t=thinX?new V3(0,0,1):new V3(1,0,0);
            float bottom=wall.pos.y-wall.size.y*.5f;var face=wall.pos+n*-1*((thinX?wall.size.x:wall.size.z)*.5f);
            var from=new V3(wall.pos.x,bottom+NativeHordeKinds.HulkHeight*.5f,wall.pos.z)+n*-1*((thinX?wall.size.x:wall.size.z)*.5f+NativeHordeKinds.HulkRadius+.2f)+t*along;
            var heading=n+t*skew;heading=heading/heading.Length;var target=from+heading*6;
            if(!NativeHulkBreach.Blocks(wall,from,target,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight,out _,out var point))point=new V3(face.x+t.x*along,from.y,face.z+t.z*along);
            return NativeHulkBreach.Begin(wall,10001,point,target-from,now);
        }
        private static NativeBreach.Hole Hole(ContentMap ward,ContentBlock wall,NativeBreachRecord r,out NativeBreach.Wall w)
        {
            w=NativeBreach.BlockWall(N(wall.pos),Quaternion.Identity,N(r.holeSize),N(r.point),N(r.normal));
            return NativeBreach.BuildBlock(w,N(r.point),N(r.normal),NativeBreach.FloorUnder(wall,ward.blocks),NativeBreach.Seed(r.block,r.breakAt));
        }

        // ---- the seed: FNV-1a over the block id's characters and the break, never string.GetHashCode ----
        private static void Seeds()
        {
            uint Fnv(string s){unchecked{uint h=2166136261;foreach(char c in s)h=(h^c)*16777619;return h;}}
            uint Ref(string s,float at){unchecked{uint h=Fnv(s);h^=(uint)(long)Math.Round(at*1000.0)*0x9E3779B9U;h^=h>>16;h*=0x7feb352dU;h^=h>>15;h*=0x846ca68bU;h^=h>>16;return h==0?1u:h;}}
            foreach(var id in new[]{"siren-hall-n-wall-6","siren-atrium-wall-e-0","a",""})foreach(float at in new[]{0f,3.6f,123.456f,86400.5f})
                Check(NativeBreach.Seed(id,at)==Ref(id,at)&&NativeBreach.Seed(new string(id.ToCharArray()),at)==NativeBreach.Seed(id,at),"the seed is FNV-1a over the id's characters and the break ("+id+")");
            Check(NativeBreach.Seed("siren-hall-n-wall-6",10)!=NativeBreach.Seed("siren-hall-s-wall-6",10)&&NativeBreach.Seed("siren-hall-n-wall-6",10)!=NativeBreach.Seed("siren-hall-n-wall-6",40),"another wall or another break breaks differently");
            Check(NativeBreach.Seed(null,1)!=0&&NativeBreach.Seed("x",1)!=0,"never a zero seed");
        }
        // ---- the floor the pieces land on ----
        private static void Floors(ContentMap ward,ContentBlock[] walls)
        {
            int lintels=0;
            foreach(var w in walls)
            {
                float bottom=w.pos.y-w.size.y*.5f,f=NativeBreach.FloorUnder(w,ward.blocks);
                Check(f<=bottom+1e-4f&&f>=bottom-7,w.id+": its floor is at or under its foot ("+f+" / "+bottom+")");
                if(Math.Abs(bottom)<.01f||Math.Abs(bottom-NativeWardZeroKit.Upper)<.01f)Check(Math.Abs(f-bottom)<.06f,w.id+": a wall on a floor lands its pieces there");
                if(bottom>.5f&&bottom<5.5f){lintels++;Check(f<bottom-1,w.id+": a lintel's pieces fall to the floor under its door ("+f+")");}
            }
            Check(lintels>0,"the map has lintels to fall from ("+lintels+")");
            var alone=new ContentBlock{id="alone-wall-1",role="static",pos=new V3(0,9,0),size=new V3(3,2,.4f)};Check(NativeBreach.FloorUnder(alone,new[]{alone})==8,"nothing under it: its own foot");
        }
        // ---- the whole block: wall, hole, pieces, burst and restore, on every eligible wall from both sides ----
        private static void Blocks(ContentMap ward,ContentBlock[] walls)
        {
            int built=0;
            foreach(var wall in walls)foreach(int side in new[]{-1,1})foreach(float skew in new[]{0f,.35f})
            {
                bool thinX=wall.size.x<wall.size.z;float len=thinX?wall.size.z:wall.size.x,thick=thinX?wall.size.x:wall.size.z;
                var r=Punch(wall,side,len*.18f,skew,20);Check(r.Valid()&&NativeHulkBreach.ValidRecords(new[]{r},ward),wall.id+": a valid replicated record");
                var h=Hole(ward,wall,r,out var w);var nrm=N(r.normal);built++;
                // The wall: the block's own box, its normal the way the Hulk faces (the blows push the block that way).
                Check(Vector3.Distance(w.Center,N(wall.pos))<1e-4f&&Math.Abs(w.Width-len)<1e-4f&&Math.Abs(w.Height-wall.size.y)<1e-4f&&Math.Abs(w.Thickness-thick)<1e-4f,wall.id+": the wall is the whole block");
                Check(Vector3.Dot(w.Normal,nrm)>.5f&&Math.Abs(Vector3.Dot(w.Right,w.Normal))<1e-5f&&Math.Abs(w.Normal.Y)<1e-6f,wall.id+": its normal points the way the Hulk faces");
                // The hole: the whole face, nothing of the block left.
                float hw=w.Width*.5f,hh=w.Height*.5f,ht=w.Thickness*.5f;
                Check(h.Full&&h.Positions.Length==0&&h.Triangles.Length==0&&Math.Abs(h.HalfWidth-hw)<1e-5f&&Math.Abs(h.HalfHeight-hh)<1e-5f&&h.Center==Vector2.Zero,wall.id+": the hole is the whole face, no panel left");
                for(int i=0;i<48;i++){float a=(float)(Math.PI*2*i/48);var p=new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*h.RadiusAlong(a);Check(Math.Abs(Math.Abs(p.X)-hw)<1e-3f||Math.Abs(Math.Abs(p.Y)-hh)<1e-3f,wall.id+": the rim is the face's edge");}
                for(float x=-hw+.02f;x<hw;x+=Math.Max(.1f,hw/6))for(float y=-hh+.02f;y<hh;y+=Math.Max(.1f,hh/6))Check(NativeBreach.InHole(h,x,y),wall.id+": all of the face is open");
                // The blows land where the record says, on the face.
                var local=r.point-wall.pos;Check(Math.Abs(h.Strike.X)<=hw&&Math.Abs(h.Strike.Y)<=hh&&Math.Abs(h.Strike.X-Vector3.Dot(N(local),w.Right))<.21f&&Math.Abs(h.Strike.Y-local.y)<.21f,wall.id+": the blows land on the record's punch point");
                // The pieces tile the block: inside its box, no two overlapping, their volumes the block's.
                Check(h.Chunks>=4&&h.Chunks<=NativeBreach.MaxBlockChunks,wall.id+": its pieces ("+h.Chunks+")");
                double vol=0;
                for(int k=0;k<h.Chunks;k++)
                {
                    var s=h.Slot[k];var z=h.Size[k]*.5f;vol+=(double)h.Size[k].X*h.Size[k].Y*h.Size[k].Z;
                    Check(z.X>.05f&&z.Y>.05f&&z.Z>.02f&&s.X-z.X>=-hw-1e-4f&&s.X+z.X<=hw+1e-4f&&s.Y-z.Y>=-hh-1e-4f&&s.Y+z.Y<=hh+1e-4f&&s.Z-z.Z>=-ht-1e-4f&&s.Z+z.Z<=ht+1e-4f,wall.id+": piece "+k+" at rest is inside the block");
                    for(int j=0;j<k;j++){var o=h.Slot[j];var q=h.Size[j]*.5f;float ox=Math.Min(s.X+z.X,o.X+q.X)-Math.Max(s.X-z.X,o.X-q.X),oy=Math.Min(s.Y+z.Y,o.Y+q.Y)-Math.Max(s.Y-z.Y,o.Y-q.Y),oz=Math.Min(s.Z+z.Z,o.Z+q.Z)-Math.Max(s.Z-z.Z,o.Z-q.Z);Check(ox<=1e-3f||oy<=1e-3f||oz<=1e-3f,wall.id+": pieces "+j+" and "+k+" do not overlap");}
                    // The burst: out along the normal, onto the floor, never through it; home and solid by the rebuild's end.
                    Check(h.Velocity[k].Z>0,wall.id+": piece "+k+" bursts out along the Hulk's facing");
                    NativeBreach.Piece(h,k,0,out var at0,out _);NativeBreach.Piece(h,k,4,out var rest,out _);NativeBreach.Piece(h,k,8,out var rest2,out _);
                    Check(Vector3.Distance(at0,s)<1e-5f&&rest.Z>s.Z+.1f&&Math.Abs(rest.Y-(h.Floor+h.Size[k].Y*.42f))<1e-3f&&Vector3.Distance(rest,rest2)<1e-4f,wall.id+": piece "+k+" flies out and comes to rest on the floor");
                    for(float t=0;t<3;t+=.05f){NativeBreach.Piece(h,k,t,out var p,out _);Check(p.Y>=h.Floor+h.Size[k].Y*.42f-1e-4f,wall.id+": never through the floor");}
                    NativeBreach.Restore(h,k,NativeBreach.Rebuild-.04f,out float back,out float solid);Check(back>.999f&&solid>.999f,wall.id+": piece "+k+" home and solid before the block's look returns");
                    NativeBreach.PieceAt(h,k,NativeBreach.Phase.Rebuild,NativeBreach.Rebuild-.04f,30,out var home,out var spin,out float holo,out _);Check(Vector3.Distance(home,s)<1e-3f&&spin.Length()<1e-3f&&holo<1e-3f,wall.id+": back in its slot");
                }
                double whole=(double)w.Width*w.Height*w.Thickness;Check(Math.Abs(vol-whole)<whole*1e-4,wall.id+": the pieces fill the whole block ("+vol+" / "+whole+")");
                // An oblique facing (the same blow turned off square) throws the same pieces along it too.
                if(skew==0)foreach(float turn in new[]{-.35f,.35f})
                {
                    var o=r.ViewCopy();var tn=Vector3.Transform(nrm,Quaternion.CreateFromAxisAngle(Vector3.UnitY,turn));o.normal=new V3(tn.X,0,tn.Z);
                    var ow=NativeBreach.BlockWall(N(wall.pos),Quaternion.Identity,N(o.holeSize),N(o.point),N(o.normal));var oh=NativeBreach.BuildBlock(ow,N(o.point),N(o.normal),NativeBreach.FloorUnder(wall,ward.blocks),h.Seed);
                    double sum=0;for(int k=0;k<h.Chunks;k++)sum+=oh.Velocity[k].X-h.Velocity[k].X;float along=Vector3.Dot(N(o.normal),ow.Right);
                    Check(oh.Chunks==h.Chunks&&Vector3.Distance(ow.Normal,w.Normal)<1e-5f&&Math.Sign(sum)==Math.Sign(along)&&Math.Abs(along)>.3f,wall.id+": an oblique blow throws the pieces its way");
                }
                // The scan sweeps all of it; the cracks start at the blow and stay on the face.
                float lo=9,hi=-9;for(float t=0;t<1.8f;t+=.01f)if(NativeBreach.Scan(h,t,out float sy,out _)){lo=Math.Min(lo,sy);hi=Math.Max(hi,sy);}Check(lo<=-hh&&hi>=hh,wall.id+": the scan sweeps the whole block");
                int cracks=0;for(int c=0;c<NativeBreach.CrackLines;c++)if(NativeBreach.Crack(h,c,1,out var ca,out var cb,out _)){cracks++;Check(Vector2.Distance(ca,h.Strike)<.16f&&Math.Abs(cb.X)<=hw&&Math.Abs(cb.Y)<=hh&&Vector2.Distance(ca,cb)<=h.Reach*1.4f,wall.id+": a crack runs from the blow and stays on the face");}
                Check(cracks==NativeBreach.CrackLines,wall.id+": every crack shows by the last knock");
            }
            Check(built==walls.Length*4,"every wall from both sides, square and oblique ("+built+")");
        }
        // ---- the timeline: the entry breach's, on the authority's collider clock ----
        private static void Timeline(ContentBlock wall)
        {
            var r=Punch(wall,1,0,0,7);float knock=NativeBreach.KnockAt(r.breakAt);
            Check(Math.Abs(knock-7)<1e-4f&&r.restoreAt==NativeBreach.DefaultRestoreAt(r.breakAt),"the knock starts when the Hulk starts it, the restore at the default");
            var prev=NativeBreach.Phase.Intact;
            for(float t=5;t<NativeBreach.SolidAt(r.restoreAt)+2;t+=.005f)
            {
                var ph=NativeBreach.At(t,knock,r.breakAt,r.restoreAt,out float into);Check(ph>=prev&&into>=0,"the phases run in the entry breach's order");prev=ph;
                bool open=ph==NativeBreach.Phase.Broken||ph==NativeBreach.Phase.Rebuild;Check(open==!NativeHulkBreach.Solid(r,t),"the block is drawn gone exactly while its collider is off ("+t+")");
                if(ph==NativeBreach.Phase.Knock)Check(Math.Abs(into-(t-knock))<1e-4f,"the knock's clock starts at the knock");
            }
            foreach(float blow in NativeBreach.Blows)Check(Math.Abs(NativeBreach.KnockAge(blow,.42f)-.42f)<1e-5f,"each knock's impact lands on its blow (.55 / 1.2 / 2.1 s)");
            Check(NativeBreach.Blows.Last()==NativeBreach.Knock&&Math.Abs(r.breakAt-knock-NativeBreach.Knock)<1e-4f,"the third blow is the break");
            // Held open while occupied: the restore follows the replicated record.
            float restore=r.restoreAt,solid=NativeBreach.SolidAt(restore);NativeHulkBreach.HoldOpen(r,restore,true);
            Check(r.restoreAt>restore&&NativeBreach.At(restore+.2f,knock,r.breakAt,r.restoreAt,out _)==NativeBreach.Phase.Broken&&!NativeHulkBreach.Solid(r,solid)&&NativeBreach.At(solid,knock,r.breakAt,r.restoreAt,out _)==NativeBreach.Phase.Rebuild,"an occupied block stays open, drawn open, the rebuild after it");
        }
        // ---- determinism ----
        private static void Determinism(ContentMap ward,ContentBlock[] walls)
        {
            byte[] Run(float at)
            {
                var ms=new MemoryStream();var bw=new BinaryWriter(ms);
                foreach(var wall in walls.Take(12)){var r=Punch(wall,1,.4f,.2f,at);var h=Hole(ward,wall,r,out _);bw.Write(h.Chunks);for(int k=0;k<h.Chunks;k++){var s=h.Slot[k];bw.Write(s.X);bw.Write(s.Y);bw.Write(s.Z);NativeBreach.Piece(h,k,1.3f,out var p,out var sp);bw.Write(p.X);bw.Write(p.Y);bw.Write(p.Z);bw.Write(sp.Y);}
                    for(int c=0;c<NativeBreach.CrackLines;c++){NativeBreach.Crack(h,c,.8f,out var a,out var b,out _);bw.Write(a.X);bw.Write(b.Y);}}
                return ms.ToArray();
            }
            var one=Run(30);var two=Run(30);var later=Run(60);
            Check(one.Length>0&&one.SequenceEqual(two),"the same record breaks the same way on every run (every peer)");
            Check(!one.SequenceEqual(later),"a later break of the same walls breaks differently");
        }
        // ---- entry breaches: the fields the shared queries now read hold exactly what they computed before ----
        private static void Entry()
        {
            foreach(float yaw in new[]{0f,.7f,2.6f})for(uint seed=1;seed<9;seed++)
            {
                var rot=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);var n=Vector3.Transform(Vector3.UnitZ,rot);var c=new Vector3(1,2.1f,2);
                var w=NativeBreach.WallOf(c,rot,new Vector3(6,4.2f,.5f),c-n*2,c+n*3);var h=NativeBreach.Build(w,c,.954f,3.06f,seed);
                Check(!h.Full&&h.Strike==h.Center&&h.Reach==Math.Max(h.HalfWidth,h.HalfHeight)&&h.Floor==-w.Height*.5f&&h.Slot.Length==NativeBreach.MaxChunks&&h.Chunks<=NativeBreach.MaxChunks,"an entry breach's blow, crack reach, floor and pieces as before");
                Check(h.Positions.Length>0&&h.Triangles.Length>0,"an entry breach still leaves its broken panel");
            }
        }
        // ---- no allocation per frame ----
        private static void Allocation(ContentMap ward,ContentBlock[] walls)
        {
            var wall=walls.OrderByDescending(b=>b.size.x*b.size.z*b.size.y).First();var r=Punch(wall,-1,1,.3f,4);var h=Hole(ward,wall,r,out _);
            float knock=NativeBreach.KnockAt(r.breakAt);
            void Frame(int i)
            {
                float t=knock-.5f+i/60f%20;var ph=NativeBreach.At(t,knock,r.breakAt,r.restoreAt,out float into);float since=t-r.breakAt;
                for(int k=0;k<h.Chunks;k++){NativeBreach.PieceAt(h,k,ph,into,since,out _,out _,out _,out _);NativeBreach.Restore(h,k,into,out _,out _);}
                NativeBreach.Scan(h,into,out _,out _);NativeBreach.Shudder(into);NativeBreach.KnockAge(into,.42f);float cracked=NativeBreach.Cracked(into);
                for(int c=0;c<NativeBreach.CrackLines;c++)NativeBreach.Crack(h,c,cracked,out _,out _,out _);NativeBreach.InHole(h,t%1,0);h.RadiusAlong(t);NativeBreach.Seed(r.block,r.breakAt);NativeHulkBreach.Solid(r,t);
            }
            for(int i=0;i<300;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=300;i<1500;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1200 frames of a whole block's knock, burst, pieces, cracks and hologram restore ("+used+" bytes, "+h.Chunks+" pieces)");
        }
        // ---- the runtime: the block by its id once, hidden and shown on every draw path, from the replicated records ----
        private static string Body(string text,string signature)
        {
            int at=text.IndexOf(signature,StringComparison.Ordinal);if(at<0)throw new Exception("Hulk breach fx: missing "+signature);
            int open=text.IndexOf('{',at),depth=0;for(int i=open;i<text.Length;i++){if(text[i]=='{')depth++;else if(text[i]=='}'&&--depth==0)return text.Substring(at,i-at+1);}
            throw new Exception("Hulk breach fx: unbalanced "+signature);
        }
        private static void Runtime(string root)
        {
            var breach=R(root,"Assets/ChipArena/Runtime/NativeHordeBreachPresentation.cs");var kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            var ward=R(root,"Assets/ChipArena/Runtime/NativeWardZeroPresentation.cs");var dressing=R(root,"Assets/ChipArena/Runtime/NativeArenaDressing.cs");var core=R(root,"Assets/ChipArena/Core/NativeBreach.cs");
            // The records: the arena's (PvE, the sandbox, guests and the killcam's replay arena alike), dynamic ones by block and break.
            var frames=Body(breach,"private void HordeBreachFrames(");var frame=Body(breach,"private void HordeBreachFrame(");var job=Body(breach,"private void HordeWallJob(");
            Check(frames.Contains("arena.WardBreaches is List<NativeBreachRecord> list")&&frame.Contains("if(frame.Dynamic){HordeWallJob(arena,frame);return;}"),"dynamic records from arena.WardBreaches go to the wall job");
            Check(job.Contains("string.Equals(b.Block,r.block,StringComparison.Ordinal)")&&job.Contains("Mathf.Abs(b.BreakAt-r.breakAt)<.01f")&&job.Contains("b.RestoreAt=r.restoreAt"),"a wall breach is keyed by its block and its break and takes the replicated restore");
            int early=job.IndexOf("b.RestoreAt=r.restoreAt;b.Actor=r.actor;return;",StringComparison.Ordinal),lookup=job.IndexOf("p.Definition.id==r.block",StringComparison.Ordinal);
            Check(early>0&&lookup>early&&job.Contains("var statics=arena.StaticBlocks;"),"the block is found by its definition id among the static blocks, once per breach");
            Check(job.Contains("NativeBreach.Seed(r.block,r.breakAt)")&&!breach.Contains("GetHashCode")&&!core.Contains("GetHashCode"),"the seed comes from the block id's characters and the break");
            Check(job.Contains("NativeBreach.BlockWall(")&&job.Contains("r.holeSize")&&job.Contains("r.point")&&job.Contains("r.normal")&&job.Contains("NativeBreach.BuildBlock(")&&job.Contains("NativeBreach.FloorUnder(def,blocks)"),"the whole block's wall and pieces from the record's punch point, facing and full opening");
            foreach(var bad in new[]{"WallBreach","Collider",".Hp=","Damage(","linearVelocity=","Body.position=","AddComponent<","Destroy("})Check(!breach.Contains(bad),"the breach reads only replicated state and never touches the simulation (`"+bad+"`)");
            // Hidden on every draw path at the break, shown again after the restore and on release.
            var hide=Body(breach,"private void HordeBreachHide(");var show=Body(breach,"private void HordeBreachShow(");var release=Body(breach,"private void HordeBreachRelease(");var draw=Body(breach,"private void HordeBreachesDraw(");
            Check(hide.Contains(".enabled=false")&&hide.Contains("if(b.Dynamic)SetWardBlockBreach(b.Block,true);"),"the break hides the block's renderers and reports it by id");
            Check(show.Contains(".enabled=true")&&show.Contains("if(b.Dynamic)SetWardBlockBreach(b.Block,false);")&&show.Contains("HordeBreachSettle(b);")&&release.Contains("HordeBreachShow(b);"),"whole again (and on release) its renderers, dressing and kit come back, its shudder settled");
            Check(Body(breach,"private void ClearHordeBreaches(").Contains("HordeBreachRelease("),"a new arena or a rewind gives every block back");
            Check(draw.Contains("if(phase==NativeBreach.Phase.Solid){if(into>.4f)HordeBreachRelease(b);else HordeBreachShow(b);continue;}")&&draw.Contains("HordeBreachSettle(b);HordeBreachHide(b);"),"hidden from the break until the rebuild is solid");
            Check(draw.Contains("if(b.Dynamic&&phase==NativeBreach.Phase.Knock&&BreachDressed(b)){b.Nudged=true;NudgeWardBlock(b.Block,")&&draw.Contains("if(!b.Hole.Full)Graphics.DrawMesh(b.PanelMesh"),"the dressed block shudders in its own look, then only its pieces are drawn");
            var setBlock=Body(ward,"public void SetWardBlockBreach(");var nudge=Body(ward,"public void NudgeWardBlock(");var kit=Body(ward,"private void DrawWardKit(");
            Check(setBlock.Contains("NativeArenaDressing.Hidden.Add(block)")&&setBlock.Contains("NativeArenaDressing.Hidden.Remove(block)")&&setBlock.Contains("wzBlockOut[i]=open"),"any wall block leaves the block dressing and the kit while open");
            Check(kit.Contains("if(wzBlockOut[part.Parent])continue;")&&kit.Contains("tr.position+wzNudge[part.Parent]"),"the kit parts on the block hide with it and shudder with it");
            Check(nudge.Contains("NativeArenaDressing.Nudged.Remove(block)")&&nudge.Contains("NativeArenaDressing.Nudged[block]=by"),"the knock's shudder goes to the dressing by id");
            Check(dressing.Contains("Nudged.TryGetValue(p.Definition.id,out var nudge))pos+=nudge;")&&dressing.Contains("Hidden.Clear();Nudged.Clear();")&&dressing.Contains("Hidden.Contains(p.Definition.id))continue;"),"the dressing draws a nudged block displaced, skips a hidden one, and clears both for a new live arena");
            Check(!Regex.IsMatch(dressing+ward+breach,@"\.transform\.(position|rotation|localPosition)\s*=(?!=)"),"no block's transform (or collider) is ever moved");
            // The Hulk pounds the wall from the record (its actor), with no slam on the floor beyond it.
            Check(kinds.Contains("else if(!entering&&HordeWallKnock(m.Id,now,out float knockAt)){attack=NativeHordeKinds.Attack.Smash;strike=.42f;recover=.6f;age=NativeBreach.KnockAge(now-knockAt,strike);knocking=true;}")&&kinds.Contains("knocking?float.NaN:age"),"the record's Hulk pounds the wall on the breach's knocks, no floor slam job");
            Check(Body(breach,"private bool HordeWallKnock(").Contains("b.Actor==actor&&now>=b.SpawnAt&&now<b.BreakAt"),"only while its knock runs");
            Check(kinds.Contains("if(arena==null)return;HordeWallCues(arena);if(arena.Pve==null)return;")&&Body(kinds,"private void HordeWallCues(").Contains("structure.boom"),"the blows, the burst and the restore are heard in PvE and the sandbox");
            // No per-frame allocation in the draw paths (LINQ, closures, strings, collections, native objects).
            foreach(var (name,text) in new[]{("frames",frames),("frame",frame),("draw",draw),("fx",Body(breach,"private void DrawBlockBreachFx(")),("knock",Body(breach,"private bool HordeWallKnock(")),("settle",Body(breach,"private void HordeBreachSettle(")),("set block",setBlock),("nudge",nudge),("kit",kit)})
                foreach(var bad in new[]{".Select(",".Where(",".Any(",".First",".ToArray(",".ToList(","=>","\"+","+\"","$\"","new List<","new Dictionary<","new Mesh","new Material","new HashSet<","new int[","new float[","new Vector3["})Check(!text.Contains(bad),name+": nothing allocated per frame (`"+bad+"`)");
            Check(!Regex.IsMatch(breach,@"private\s+(readonly\s+)?(Mesh|Material)\s+\w+\s*=\s*new"),"no native object in a field initializer");
            Check(!core.Contains("UnityEngine")&&!core.Contains("NativeArena")&&!core.Contains("new Random("),"NativeBreach.cs stays engine free and clock free");
        }
    }
}
#endif
