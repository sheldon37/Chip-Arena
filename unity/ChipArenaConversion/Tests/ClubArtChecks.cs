using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Substation 128's stage art (docs/arenas/STAGE-ART.md, docs/arenas/CLUB.md "Stage art"): the bank's name and rig slots, the art
// poses against the box rig at every moment (the cones, the moving heads, the laser posts, the mirror balls, the Grey DJ and the
// crowd of Greys stand and move where the rig's own boxes do), the layout export, the runtime's wiring and its bank-absent
// fallback; and, where the bank is on this machine (it is git-ignored), the bank itself against the map and its budget.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --club-art` (also in the full run).
internal static class ClubArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Club art: "+why);}
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("club");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,"club");var looks=NativeClubLooks.Resolve(look.kitLooks);

        // ---- the registry and the slots ----
        Check(NativeStageArt.BankOf("club")=="ChipArenaStageClub"&&NativeSubstationArt.Bank=="ChipArenaStageClub","the club's bank is named");
        var S=NativeSubstationArt.Slots;
        Check(S.Length==25&&S.Distinct().Count()==25&&S[NativeSubstationArt.Cones+1]=="cones-e"&&S[NativeSubstationArt.YokeStand]=="yoke-stand"&&S[NativeSubstationArt.Head]=="head"&&S[NativeSubstationArt.LaserPost]=="laser-post"&&S[NativeSubstationArt.Ball]=="ball"
            &&S[NativeSubstationArt.DjLegs]=="dj-legs"&&S[NativeSubstationArt.DjFore]=="dj-fore"&&S[NativeSubstationArt.GreyLegs+3]=="grey-legs-3"&&S[NativeSubstationArt.GreyBody]=="grey-body-0"&&S[NativeSubstationArt.GreyArm+3]=="grey-arm-3","the rig slots");
        Check(!NativeSubstationArt.Casts(NativeSubstationArt.Cones)&&NativeSubstationArt.Casts(NativeSubstationArt.Head)&&NativeSubstationArt.Casts(NativeSubstationArt.DjHead)&&!NativeSubstationArt.Casts(NativeSubstationArt.GreyBody)&&!NativeSubstationArt.Casts(NativeSubstationArt.GreyArm+3),"the fixtures and the DJ cast shadows, the cones and the crowd do not");
        var variants=Enumerable.Range(0,64).Select(NativeSubstationArt.Variant).ToArray();
        Check(variants.All(v=>v>=0&&v<NativeSubstationArt.Variants)&&variants.Distinct().Count()==NativeSubstationArt.Variants&&variants.SequenceEqual(Enumerable.Range(0,64).Select(NativeSubstationArt.Variant)),"every look is in the crowd, stable per figure");
        for(int k=0;k<64;k++){NativeClubRig.CrowdAt(k,out double x,out _,out double z,out double yaw);double f=NativeSubstationArt.Facing(k,yaw);double fx=Math.Sin(f),fz=Math.Cos(f);
            Check(Math.Abs(x)>map.bounds.maxX||z>map.bounds.maxZ,"crowd figure "+k+" stands outside the play space");
            Check(k<48?fx*-Math.Sign(x)>.99:fz<-.5,"crowd figure "+k+" faces the hall (the galleries') or the stage (the pit's)");}

        // ---- the poses agree with the box rig at every moment ----
        var show=new NativeClubShow();show.Prepare(map);var pulse=new NativeClubPulse();var rig=new NativeClubRig(looks);var pose=new NativeStageArtPose();
        double C=NativeClubLooks.SpeakerTop/4,front=NativeClubLooks.SpeakerZ,worst=0,worstArm=0,worstBody=0;int seen=0,posts=0,up=0,down=0,lifted=0,maxDraws=0,lowDraws=0;bool thrown=false;
        foreach(bool low in new[]{false,true})for(int round=0;round<2;round++)for(double t=0;t<70;t+=.05)
        {
            pulse.Feed(null,0,0,.05,NativeClubLooks.Beats(NativePhase.Fight,0,t,t),NativeKitFlash.Full);show.Build(round,NativePhase.Fight,0,t,t,pulse,NativeKitFlash.Full);rig.Build(show,t,low,1);NativeSubstationArt.Pose(rig,show,t,pose);seen++;
            Check(rig.Screens==rig.Tiles+rig.Panels&&rig.Screens==NativeClubLooks.Tiles+NativeClubShow.WallW*NativeClubShow.WallH+NativeClubShow.CubePanels,"the lit screens come first in the rig (they draw with the art)");
            if(low)lowDraws=Math.Max(lowDraws,pose.Count);else maxDraws=Math.Max(maxDraws,pose.Count);
            int heads=0,yokes=0,balls=0,crowd=0,arms=0,legs=0;
            for(int i=0;i<pose.Count;i++)
            {
                int slot=pose.Slot[i];
                if(slot==NativeSubstationArt.Cones||slot==NativeSubstationArt.Cones+1)
                {
                    // a woofer's cone (the box rig's two crossed plates .82 r x .34 r) at the lowest cabinet's outer woofer
                    double side=slot==NativeSubstationArt.Cones?-1:1,cx=side*(NativeClubLooks.SpeakerX0+NativeClubLooks.SpeakerX1)/2;
                    At(pose,i,cx+.66,C*.5,front-.02,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.44f*.82f,.44f*.34f,.02f));if(pose.Place[i*12+11]<-.1)thrown=true;
                }
                else if(slot==NativeSubstationArt.Head){heads++;At(pose,i,0,0,.25,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.11f,.11f,.012f));}
                else if(slot==NativeSubstationArt.YokeHang||slot==NativeSubstationArt.YokeStand){yokes++;double u=slot==NativeSubstationArt.YokeHang?1:-1;At(pose,i,.2,u*.15,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.025f,.17f,.06f));}
                else if(slot==NativeSubstationArt.LaserPost){posts++;At(pose,i,0,NativeClubLooks.LaserTop+.02,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.15f,.03f,.15f));}
                else if(slot==NativeSubstationArt.Ball){balls++;float r=(float)NativeClubLooks.BallRadius*.92f;At(pose,i,0,0,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,r,r,r));}
                else if(slot==NativeSubstationArt.DjLegs){At(pose,i,0,.45,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.17f,.45f,.12f));}
                else if(slot==NativeSubstationArt.DjTorso){At(pose,i,0,0,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.22f,.34f,.13f));}
                else if(slot==NativeSubstationArt.DjHead){At(pose,i,0,0,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.11f,.13f,.12f));}
                else if(slot==NativeSubstationArt.DjUpper)
                {
                    // the box arm's middle, .24 down the arm from the shoulder; the forearm starts at the upper arm's end
                    At(pose,i,0,-.24,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.05f,.24f,.05f));
                    At(pose,i,0,-NativeSubstationArt.DjUpperArm,0,out double ex,out double ey,out double ez);Check(pose.Slot[i+1]==NativeSubstationArt.DjFore,"the forearm follows the upper arm");
                    At(pose,i+1,0,0,0,out double fx,out double fy,out double fz);worst=Math.Max(worst,Math.Sqrt((ex-fx)*(ex-fx)+(ey-fy)*(ey-fy)+(ez-fz)*(ez-fz)));
                    At(pose,i+1,0,-.45,0,out _,out double hy,out _);if(hy>NativeClubLooks.StageY+2.0)lifted++;
                }
                else if(slot>=NativeSubstationArt.GreyBody&&slot<NativeSubstationArt.GreyBody+NativeSubstationArt.Variants)
                {
                    // the box torso's middle (.2 x .3 tall x .11): the body's own, .26 over its hip, within the sway's reach
                    crowd++;At(pose,i,0,.26,0,out double x,out double y,out double z);worstBody=Math.Max(worstBody,NearestXZ(rig,x,y,z,.2f,.11f));
                }
                else if(slot>=NativeSubstationArt.GreyLegs&&slot<NativeSubstationArt.GreyLegs+NativeSubstationArt.Variants)
                {
                    // the legs reach from the floor to the body's hip (the next draw)
                    legs++;At(pose,i,0,NativeSubstationArt.Hip,0,out double x,out double y,out double z);At(pose,i+1,0,0,0,out double bx,out double by,out double bz);
                    worst=Math.Max(worst,Math.Sqrt((x-bx)*(x-bx)+(y-by)*(y-by)+(z-bz)*(z-bz)));
                }
                else if(slot>=NativeSubstationArt.GreyArm&&slot<NativeSubstationArt.GreyArm+NativeSubstationArt.Variants)
                {
                    // an arm: its shoulder by the box arm's near end, the arm along the box arm; a raised arm's hand over its shoulder
                    arms++;At(pose,i,0,0,0,out double sx,out double sy,out double sz);At(pose,i,0,-1,0,out double hx,out double hy,out double hz);
                    double dx=hx-sx,dy=hy-sy,dz=hz-sz,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);dx/=l;dy/=l;dz/=l;bool raised=dy>0;if(raised)up++;else down++;
                    worstArm=Math.Max(worstArm,ArmEnd(rig,sx,sy,sz,dx,dy,dz,raised));
                }
            }
            Check(heads==NativeClubLooks.Heads&&yokes==NativeClubLooks.Heads&&balls==2&&crowd==NativeClubRig.CrowdCount(low)&&legs==crowd&&arms==2*crowd,"every head, yoke, ball and crowd figure is posed (low "+low+")");
        }
        Check(worst<2e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.0000")+" m)");
        Check(worstBody<.03,"every Grey's body stands on the box rig's torso (worst "+worstBody.ToString("0.000")+" m)");
        Check(worstArm<.17,"every Grey's arm hangs or rises from the box arm's near end, along it (worst "+worstArm.ToString("0.000")+" m)");
        Check(posts>0&&thrown&&up>0&&down>0&&lifted>0,"the poses cover the laser posts up, the cones thrown at the drop, arms up and down, the DJ's arm up");
        Check(maxDraws>250&&maxDraws<=NativeStageArtPose.Capacity&&lowDraws<maxDraws-100,"a frame's rig draws ("+maxDraws+", "+lowDraws+" on the low tier) fit the pose buffer");
        show.Build(0,NativePhase.Fight,0,31,31,pulse,NativeKitFlash.Full);rig.Build(show,31,false,1);NativeSubstationArt.Pose(rig,show,31,pose);int n=pose.Count;var copy=pose.Place.Take(n*12).ToArray();
        rig.Build(show,31,false,1);NativeSubstationArt.Pose(rig,show,31,pose);Check(pose.Count==n&&pose.Place.Take(n*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/60.0;show.Build(1,NativePhase.Fight,0,t,t,pulse,NativeKitFlash.Full);rig.Build(show,t,false,1);NativeSubstationArt.Pose(rig,show,t,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"club",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;var kit=NativeClubKit.Build(map,look);
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("parts").GetArrayLength()==kit.Parts.Count&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block, hazard and kit part");
            Check(kit.Family.Count==kit.Parts.Count&&kit.Owner.Count==kit.Parts.Count&&kit.Kind.Count==kit.Parts.Count&&kit.Family.All(f=>!string.IsNullOrEmpty(f)),"every kit part has its family, owner and kind");
            Check(Enumerable.Range(0,kit.Parts.Count).All(i=>kit.Overhead[i]==(kit.Owner[i]==null)&&kit.Kind[i]>=0&&kit.Kind[i]<NativeClubLooks.Kinds.Length),"an attached part names its block, an overhead part none");
            var fams=j.GetProperty("parts").EnumerateArray().Select(p=>p.GetProperty("f").GetString()).Distinct().ToArray();
            Check(new[]{"truss","tower","speaker","backbar","south-wall","floor","upholstery","window"}.All(fams.Contains),"the families ("+string.Join(",",fams)+")");
            Check(j.GetProperty("heads").GetArrayLength()==NativeClubLooks.Heads&&j.GetProperty("crowd").GetArrayLength()==NativeClubRig.CrowdMax&&Math.Abs(j.GetProperty("club").GetProperty("hip").GetDouble()-NativeSubstationArt.Hip)<1e-9,"the heads, the crowd's places and the Greys' build");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the runtime's wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));var pres=R("Assets/ChipArena/Runtime/NativeClubPresentation.cs");
        Check(pres.Contains("art.Prepare(map,NativeSubstationArt.Slots,NativeSubstationArt.Casts);")&&pres.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the club's presentation prepares and renders its art");
        Check(pres.Contains("if(!ArtDrawn)foreach(var b in kit)Draw(b,instancing);")&&pres.Contains("Fill(rigBuilder,ArtDrawn?rigBuilder.Screens:rigBuilder.Parts.Count);")&&pres.Contains("if(!(ArtDrawn&&i==NativeClubScenery.Near))"),"the box kit, the whole box rig and the near backdrop draw whenever the art does not; the lit screens always");
        Check(pres.Contains("ArtDrawn=false;if(rigBuilder!=null)rigBuilder.Build(show,now,low,quality);"),"the rig is built either way (its state feeds the effects and the art's poses)");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Club art: the club's bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bytes=File.ReadAllBytes(bankPath);var bank=NativePackBank.Read(bytes);var sp=NativeStageArt.Read(bank);
            Check(bytes.Length<=21*1048576,"the bank keeps to its budget, about 20 MB ("+(bytes.Length/1048576.0).ToString("0.0")+" MB)");
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var breakable=map.blocks.Where(b=>b.role!="static").ToArray();
            Check(breakable.Length==20&&breakable.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)&&sp.Blocks[b.id].Distinct().Count()==3),"every breakable prop has its model in three tiers");
            Check(S.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()==3),"every rig slot has its model in three tiers ("+string.Join(",",S.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            Check(sp.Static.Count>=20&&sp.Static.All(c=>c.Props.Distinct().Count()==3),"the static chunks, each in three tiers");
            // a block model fills its block: inside it grown by .25 m (a lamp on a table a little more), and most of its height
            foreach(var b in breakable)
            {
                var prop=bank.Props[sp.Blocks[b.id][0]];double top=double.MinValue,bottom=double.MaxValue,out_=0;
                foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions){top=Math.Max(top,v.Y);bottom=Math.Min(bottom,v.Y);out_=Math.Max(out_,Math.Max(Math.Abs(v.X)-b.size.x/2,Math.Max(Math.Abs(v.Y)-b.size.y/2,Math.Abs(v.Z)-b.size.z/2)));}
                Check(out_<=.25&&top>=b.size.y/2-.12&&bottom<=-b.size.y/2+.02,"the model of "+b.id+" fills its block (out "+out_.ToString("0.00")+", top "+top.ToString("0.00")+")");
            }
            // the play space: inside the bounds, below 2 m over its floor (the hall's, the upper level's), the Fine art keeps to the
            // blocks (within .25 m; a thin prop within .9 m); higher is overhead; nothing stands in a spawn's capsule
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            foreach(var c in sp.Static)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    if(v.X<map.bounds.minX||v.X>map.bounds.maxX||v.Z<map.bounds.minZ||v.Z>map.bounds.maxZ)continue;
                    double floor=v.Y<4.15?0:4.2;if(v.Y-floor>2||v.Y<floor-.05)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25){proud++;var key=((int)Math.Floor(v.X))+","+((int)Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            int tris(int tier)=>sp.Static.Sum(c=>bank.Props[c.Props[tier]].Parts.Sum(p=>bank.Meshes[p.Mesh].Submeshes.Sum(s=>s.Indices.Length/3)));
            Console.WriteLine("Club art: bank "+(bytes.Length/1048576.0).ToString("0.00")+" MB, "+bank.Props.Count+" props; static triangles "+tris(0)+" / "+tris(1)+" / "+tris(2)+"; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Club art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(30).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS club art ("+count+" checks; "+seen+" moments posed)");
    }
    // The point (lx, ly, lz) of draw i's frame in the world.
    private static void At(NativeStageArtPose p,int i,double lx,double ly,double lz,out double x,out double y,out double z)
    {int o=i*12;var m=p.Place;x=m[o]*lx+m[o+1]*ly+m[o+2]*lz+m[o+3];y=m[o+4]*lx+m[o+5]*ly+m[o+6]*lz+m[o+7];z=m[o+8]*lx+m[o+9]*ly+m[o+10]*lz+m[o+11];}
    // The distance from (x, y, z) to the nearest box rig part of these half extents (or infinity).
    private static double Nearest(NativeClubRig rig,double x,double y,double z,float hx,float hy,float hz)
    {
        double best=double.MaxValue;for(int i=0;i<rig.Parts.Count;i++){ref var p=ref rig.Parts.Items[i];if(Math.Abs(p.Hx-hx)>1e-3||Math.Abs(p.Hy-hy)>1e-3||Math.Abs(p.Hz-hz)>1e-3)continue;double d=Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y)+(p.Z-z)*(p.Z-z));if(best>d)best=d;}
        return best;
    }
    // The same for a part of any height (a crowd figure's torso is as tall as the figure).
    private static double NearestXZ(NativeClubRig rig,double x,double y,double z,float hx,float hz)
    {
        double best=double.MaxValue;for(int i=rig.Screens;i<rig.Parts.Count;i++){ref var p=ref rig.Parts.Items[i];if(Math.Abs(p.Hx-hx)>1e-3||Math.Abs(p.Hz-hz)>1e-3)continue;double d=Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y)+(p.Z-z)*(p.Z-z));if(best>d)best=d;}
        return best;
    }
    // A crowd arm against the box arms (.045 x .26 or .3 x .045): the least, over the box arms whose axis lies along the model's
    // (d, shoulder to hand), of the distance from the model's shoulder to the box's near end.
    private static double ArmEnd(NativeClubRig rig,double sx,double sy,double sz,double dx,double dy,double dz,bool raised)
    {
        double best=double.MaxValue;float hy=raised?.3f:.26f;
        for(int i=rig.Screens;i<rig.Parts.Count;i++)
        {
            ref var p=ref rig.Parts.Items[i];if(Math.Abs(p.Hx-.045f)>1e-3||Math.Abs(p.Hz-.045f)>1e-3||Math.Abs(p.Hy-hy)>1e-3)continue;
            var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};q.Rotate(0,1,0,out double ax,out double ay,out double az);
            // the box's own axis points up its length: a raised arm's hand is its far (upper) end, a hanging arm's its lower end
            double along=(ax*dx+ay*dy+az*dz)*(raised?1:-1);if(along<.999)continue;
            double s=raised?-1:1,ex=p.X+s*ax*hy,ey=p.Y+s*ay*hy,ez=p.Z+s*az*hy,d=Math.Sqrt((ex-sx)*(ex-sx)+(ey-sy)*(ey-sy)+(ez-sz)*(ez-sz));if(best>d)best=d;
        }
        return best;
    }
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageClub.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageClub.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
}
