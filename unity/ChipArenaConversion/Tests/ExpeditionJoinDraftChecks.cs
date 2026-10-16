using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
namespace ChipArena.UnityConversion
{
    // The exp-layout-4 join circle draft (docs/EXPEDITION-HUB.md "Join circle at the instance doors"), written by
    // docs/arenas/scripts/expedition_layout.py --draft (or --fixtures) to Tests/expedition-layout-4-join-draft.json. NOT LIVE: the
    // game loads Resources/ExpeditionLayout.json (exp-layout-3) and nothing under Assets/ names the draft, so neither the runtime
    // nor the player build carries it (Tests/ is outside Assets/). Run from ExpeditionLayoutChecks with its capsule probe and the
    // imported navigation. Checks:
    //  - the live manifest is byte-identical to exp-layout-3 (pinned SHA-256), has no join, and Astra's parser and geometry gate
    //    still accept it;
    //  - Astra's parser and geometry gate (NativeExpeditionLayout.Parse, ValidateGeometry) accept the draft: one join on the hub's
    //    Ward Zero door, none on the instance exit;
    //  - the draft is the live manifest plus exactly the join, the approach moved to its centre, the moved returns and the
    //    revision (anchor, F interaction volume, closure and every other record unchanged);
    //  - base-y membership on the authoritative capsule centre: radius, base and top inclusive, 1 cm beyond each excluded; a
    //    standing pilot and a ground (plus air) jump inside, a pilot on the portico canopy not; no return inside (exp-layout-3's
    //    returns had four inside), none in the F volume;
    //  - the town dressing built on the draft's records puts nothing but flat paint in the cylinder and nothing on the moved
    //    returns or approach.
    // Static data and pure-code checks only: no scene, PhysX or network claim, and the managed join rule is not enabled in the live director (Astra's subset
    // transfer and view integration come first).
    internal static class ExpeditionJoinDraftChecks
    {
        internal const string DraftPath="Tests/expedition-layout-4-join-draft.json",LivePath="Tests/expedition-layout-draft.json";
        /// <summary>SHA-256 of the live manifest's bytes (exp-layout-3). A deliberate live layout change updates it together with
        /// PINNED in Tests/test_expedition_layout_draft.py; the join draft never changes it.</summary>
        internal const string LiveSha256="3c1a259f400c3ff6cfd6fe5fd920c67f4dcd3777b956927067045786770e144a";
        private static readonly string[] TextKinds={".cs",".json",".shader",".cginc",".hlsl",".asset",".unity",".prefab",".mat",".asmdef",".txt",".md"};
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition join draft: "+what);count++;}
        private struct Box{public double X0,X1,Y0,Y1,Z0,Z1;}
        // A dressing part's world bounds (its parent block's pose, then its own; as ExpeditionDressingChecks poses them).
        private static Box Pose(ContentBlock b,in NativeCityPart p)
        {
            var r=b.rotation;double qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r?.w??1;NativeExpeditionCityKit.Rotate(qx,qy,qz,qw,p.X,p.Y,p.Z,out double ox,out double oy,out double oz);
            double wx=qw*p.Qx+qx*p.Qw+qy*p.Qz-qz*p.Qy,wy=qw*p.Qy-qx*p.Qz+qy*p.Qw+qz*p.Qx,wz=qw*p.Qz+qx*p.Qy-qy*p.Qx+qz*p.Qw,ww=qw*p.Qw-qx*p.Qx-qy*p.Qy-qz*p.Qz;
            var w=new Box();NativeExpeditionCityKit.Extent(b.pos.x+ox,b.pos.y+oy,b.pos.z+oz,p.Hx,p.Hy,p.Hz,wx,wy,wz,ww,out w.X0,out w.X1,out w.Y0,out w.Y1,out w.Z0,out w.Z1);return w;
        }
        private static bool Disc(in Box w,double x,double z,double r,double y0,double y1)
        {
            if(w.Y1<=y0||w.Y0>=y1)return false;double cx=Math.Max(w.X0,Math.Min(x,w.X1)),cz=Math.Max(w.Z0,Math.Min(z,w.Z1));return (cx-x)*(cx-x)+(cz-z)*(cz-z)<r*r-1e-6;
        }
        internal static void Run(string root,ConversionContent content,NativePveContent pve,Func<string,V3,float,float,bool> probe,Func<string,V3,V3,float,float,bool> reach)
        {
            count=0;string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            // ---------------- the live layout: unchanged, no join, still accepted ----------------
            string liveText=R(LivePath),draftText=R(DraftPath);
            string sha;using(var h=SHA256.Create())sha=BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(Path.Combine(root,LivePath)))).Replace("-","").ToLowerInvariant();
            Check(sha==LiveSha256,"the historical baseline is exp-layout-3's bytes (sha256 "+sha+")");
            var live=NativeExpeditionLayout.Parse(liveText);
            Check(live.revision=="exp-layout-3"&&live.areas.All(a=>a.doors.All(d=>d.join==null)),"the live layout is exp-layout-3 without a join circle");
            NativeExpeditionLayout.ValidateGeometry(live,content,pve,probe,reach);count++;
            // ---------------- the draft: Astra's parser and geometry gate accept it ----------------
            var draft=NativeExpeditionLayout.Parse(draftText);
            Check(draft.revision=="exp-layout-4"&&draft.world==live.world&&draft.instances.SequenceEqual(live.instances)&&draft.areas.Select(a=>a.map).SequenceEqual(live.areas.Select(a=>a.map)),"the draft is exp-layout-4 of the same world, instances and maps");
            var town=draft.Area(draft.world);var door=town.doors.Single();var j=door.join;
            Check(j!=null&&town.anchors.Single(a=>a.id==door.anchor).building=="exp-ward-zero"&&draft.areas.Where(a=>a.id!=draft.world).All(a=>a.doors.All(d=>d.join==null)),"the join is at the hub entry into Ward Zero; the instance exit has none");
            NativeExpeditionLayout.ValidateGeometry(draft,content,pve,probe,reach);count++;
            Check(j.x==79&&j.y==0&&j.z==0&&j.r==2.5f&&j.h==4,"the join: centre (79, 0, 0), r 2.5 m, base y 0, h 4 m");
            // Exactly the join, the approach, the returns and the revision differ from the live manifest.
            var dn=JsonNode.Parse(draftText);var ln=JsonNode.Parse(liveText);var dd=dn["areas"][0]["doors"][0].AsObject();var ld=ln["areas"][0]["doors"][0];
            dd.Remove("join");dd["approach"]=ld["approach"].DeepClone();dd["returns"]=ld["returns"].DeepClone();dn["revision"]=ln["revision"].DeepClone();
            Check(JsonNode.DeepEquals(dn,ln),"the draft is the live manifest plus the join, the approach, the returns and the revision (anchor, F volume, closure and the rest unchanged)");
            Check(door.approach.x==j.x&&door.approach.y==j.y&&door.approach.z==j.z,"the door's approach is the circle's centre");
            // ---------------- membership: base y, the capsule centre, inclusive bounds ----------------
            float hh=NativeLocomotionRules.HalfHeight;V3 Centre(float x,float feet,float z)=>new V3(x,feet+hh,z);
            Check(j.Contains(Centre(j.x,j.y,j.z))&&j.Contains(Centre(j.x+j.r,j.y,j.z))&&j.Contains(Centre(j.x,j.y,j.z-j.r)),"a standing pilot's capsule centre at the centre and on the rim is inside (inclusive radius)");
            for(int k=0;k<8;k++)
            {
                double a=k*Math.PI/4;float cx=(float)Math.Cos(a),sz=(float)Math.Sin(a);
                Check(j.Contains(Centre(j.x+cx*(j.r-.005f),j.y,j.z+sz*(j.r-.005f)))&&!j.Contains(Centre(j.x+cx*(j.r+.01f),j.y,j.z+sz*(j.r+.01f))),"just inside the rim is inside, 1 cm beyond it outside (bearing "+k*45+")");
            }
            Check(j.Contains(new V3(j.x,j.y,j.z))&&!j.Contains(new V3(j.x,j.y-.01f,j.z)),"the base is the floor: a centre on it is inside, 1 cm below outside");
            Check(j.Contains(new V3(j.x,j.y+j.h,j.z))&&!j.Contains(new V3(j.x,j.y+j.h+.01f,j.z)),"the top is inclusive: 1 cm above it is outside");
            double g1=Rules.JumpSpeed*Rules.JumpSpeed/(2*Rules.Gravity),g2=Math.Pow(Rules.JumpSpeed*NativeJumpSimulation.AirMultiplier,2)/(2*Rules.Gravity);
            Check(j.Contains(Centre(j.x,(float)(j.y+g1),j.z))&&j.Contains(Centre(j.x,(float)(j.y+g1+g2),j.z)),"a ground jump (apex centre "+(hh+g1).ToString("0.00")+" m) and a ground plus air jump ("+(hh+g1+g2).ToString("0.00")+" m) stay inside (jumpPower 1)");
            Check(!j.Contains(Centre(80.5f,4.8f,j.z)),"a pilot on the portico canopy (top 4.8 m) is not inside");
            // ---------------- returns and the F volume ----------------
            foreach(var p in door.returns)
                Check(!j.Contains(Centre(p.x,p.y,p.z))&&Math.Sqrt((p.x-j.x)*(p.x-j.x)+(p.z-j.z)*(p.z-j.z))>=j.r+NativeLocomotionRules.Radius+.5&&!door.interact.Contains(p.Position),"return ("+p.x+", "+p.z+") lands outside the circle with its whole capsule, off the F volume");
            int before=live.Area(live.world).doors.Single().returns.Count(p=>j.Contains(Centre(p.x,p.y,p.z)));
            Check(before==4,"exp-layout-3's returns: "+before+" of 8 would land in the circle; the draft's: none");
            bool apart=true;for(double x=j.x-j.r;x<=j.x+j.r;x+=.25)for(double z=j.z-j.r;z<=j.z+j.r;z+=.25)if((x-j.x)*(x-j.x)+(z-j.z)*(z-j.z)<=j.r*j.r&&door.interact.Contains(new V3((float)x,j.y,(float)z)))apart=false;
            Check(apart,"the circle and the F interaction volume do not overlap (F entry stays as it is)");
            // ---------------- the town dressing on the draft's records ----------------
            var map=content.Map(town.map);var hub=ExpeditionHubLayout.Parse(draftText,town.id);Check(hub!=null,"the draft keeps the hub record");
            var kit=NativeExpeditionCityKit.Build(map.blocks,town,hub,map.bounds);int flat=0,stand=0,around=0;string first=null,onSlot=null;
            foreach(var p in kit.Parts)
            {
                var w=Pose(map.blocks[p.Parent],p);
                foreach(var q in door.returns.Append(door.approach))if(onSlot==null&&Disc(w,q.x,q.z,.6,q.y-1,q.y+2.2))onSlot=map.blocks[p.Parent].id+" on ("+q.x+", "+q.z+")";
                if(Disc(w,j.x,j.z,j.r+4,j.y,j.y+j.h))around++;
                if(!Disc(w,j.x,j.z,j.r,j.y,j.y+j.h))continue;
                if(w.Y1<=j.y+NativeExpeditionCityKit.Flat+1e-4)flat++;else{stand++;first??=map.blocks[p.Parent].id+" at "+((w.X0+w.X1)/2).ToString("0.0")+", "+((w.Y0+w.Y1)/2).ToString("0.00")+", "+((w.Z0+w.Z1)/2).ToString("0.0");}
            }
            Check(around>0,"the dressing test sees the parts round the door ("+around+" within 4 m of the circle)");
            Check(onSlot==null,"nothing dressed on a moved return or the approach"+(onSlot!=null?": "+onSlot:""));
            Check(stand==0,"the town dressing puts only flat paint in the cylinder ("+flat+" flat parts, "+stand+" standing"+(first!=null?": "+first:"")+")");
            // ---------------- not referenced: the runtime and the player build never see the draft ----------------
            var hits=Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories)
                .Where(f=>TextKinds.Contains(Path.GetExtension(f))&&!f.Replace('\\','/').Contains("/LocalAssets/"))
                .Where(f=>{var t=File.ReadAllText(f);return t.Contains("expedition-layout-4",StringComparison.Ordinal)||t.Contains("join-draft",StringComparison.Ordinal);}).ToArray();
            Check(hits.Length==0,"nothing under Assets/ names the draft ("+string.Join(", ",hits.Select(Path.GetFileName))+")");
            Check(!DraftPath.StartsWith("Assets/",StringComparison.Ordinal)&&NativeExpeditionLayout.ResourceName=="ExpeditionLayout"&&!R("tools/generate_expedition.py").Contains("join-draft"),"the draft sits outside Assets/; the runtime loads Resources/ExpeditionLayout and the importer imports the live fixture only");
            Console.WriteLine("PASS expedition join draft ("+count+" checks: exp-layout-4 at "+DraftPath+", not live and named nowhere under Assets/; the historical exp-layout-3 baseline byte-identical, without a join and still accepted; "
                +"Astra's parser and geometry gate accept the draft: join (79, 0, 0) r 2.5 h 4 at the Ward Zero door, approach at its centre, returns out of it (exp-layout-3 had 4 inside), F volume unchanged; "
                +"base-y capsule-centre membership inclusive at radius, base and top; standing and jumping pilots inside; dressing: "+flat+" flat parts and nothing standing in the cylinder, "+around+" parts within 4 m of it)");
        }
    }
}
