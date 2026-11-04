#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // THE 6:47 as a real model and the Central Station's four kinds off voxels (owner 2026-11-04; Runtime/NativeStationBossBody.cs,
    // tools/gear/station_boss.py, docs/EXPEDITION-ENEMY-REWORK.md): the Blender script builds on the game's own joints (their rests and
    // limb lengths agree with Core); the presentation draws the bank before the concept's sculpt, the sculpt only without the bank or
    // the bake; the four kinds rank first and stay on the model at every distance, a frame without a body drawing no voxel; and, where
    // the local bank is at hand (the owner's PC; STATION_BOSS_BANK names another copy), every tier holds its budget and the body stays in
    // the carriage's hit box. The bank is local (never committed), so a clean checkout skips only that part.
    internal static class StationBossBodyChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Station boss body: "+what);count++;}
        /// <summary>The bank's triangles per tier (the one boss on screen: rigid parts, no skinning).</summary>
        public static readonly int[] Budget={170000,50000,12000};
        public static void Run(string root)
        {
            Script(root);Draws(root);Kinds(root);string bank=Bank(root);
            Console.WriteLine("PASS station boss body ("+count+" checks: the Blender script on Core's joints, the bank drawn before the sculpt, the four kinds never voxel on the model"+bank+")");
        }
        private static float[] Numbers(string py,string name)
        {
            var m=Regex.Match(py,"^"+Regex.Escape(name)+@"\s*=\s*\(?([^)\n]*)\)?\s*$",RegexOptions.Multiline);Check(m.Success,"the script names "+name);
            return m.Groups[1].Value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();
        }
        private static void Same(float a,float b,string what)=>Check(Math.Abs(a-b)<1e-3f,what+" agrees with Core ("+a+", "+b+")");
        private static void Script(string root)
        {
            string py=File.ReadAllText(Path.Combine(root,"tools/gear/station_boss.py"));
            var lift=Numbers(py,"BODY_LIFT, RIDER");Same(lift[0],NativeStationBossModel.BodyLift,"the body's lift");Same(lift[1],NativeStationBossModel.Rider,"the rider's scale");
            var hull=Numbers(py,"HALF_WIDTH, REAR_Z, CAB_Z, NOSE_Z");Same(hull[0],NativeStationBossModel.HalfWidth,"the half width");Same(hull[1],NativeStationBossModel.RearZ,"the rear");Same(hull[2],NativeStationBossModel.CabZ,"the cab");Same(hull[3],NativeStationBossModel.NoseZ,"the nose");
            var rows=Numbers(py,"ROW_Z");Check(rows.Length==3,"three rows");for(int r=0;r<3;r++)Same(rows[r],NativeStationBossModel.RowZ[r],"row "+r);
            var win=Numbers(py,"WINDOW_Z");Check(win.Length==4,"four windows");for(int k=0;k<4;k++)Same(win[k],NativeStationBossModel.WindowZ[k],"window "+k);
            var links=Numbers(py,"CHAIN_LINKS, CABLE_LINKS, WINDOW_ARMS, TAIL_LINKS");Same(links[0],NativeStationBossModel.ChainLinks,"the chain");Same(links[1],NativeStationBossModel.CableLinks,"the cables");Same(links[2],NativeStationBossModel.WindowArms,"the window arms");Same(links[3],NativeStationBossLook.TailLinks,"the tails");
            Same(Numbers(py,"TAIL_LENGTH")[0],NativeStationBossLook.TailLength,"a tail link");Same(Numbers(py,"PALM_LIFT")[0],NativeStationBossModel.PalmLift,"the palm's lift");
            var pivot=NativeStationBossLook.WatchPivot(out _,out _);var w=Numbers(py,"WATCH_PIVOT");Check(Math.Abs(w[0]-pivot.X)<.006f&&Math.Abs(w[1]-pivot.Y)<.006f&&Math.Abs(w[2]-pivot.Z)<.006f,"the watch's pivot is Core's ("+pivot+")");
            Check(py.Contains("return 2.6 if row_of(leg) == 0 else 2.7")&&py.Contains("return 3.3 if row_of(leg) == 0 else 3.5"),"the limbs' lengths in the script");
            Same(2.6f,NativeStationBossModel.Thigh(0),"a front thigh");Same(2.7f,NativeStationBossModel.Thigh(2),"a rear thigh");Same(3.3f,NativeStationBossModel.Shin(0),"a front shin");Same(3.5f,NativeStationBossModel.Shin(4),"a rear shin");
            // Every joint the rig poses is in the script's table (a frame, or a part).
            foreach(var name in NativeStationBossLook.Names)
            {
                string stem=Regex.Replace(Regex.Replace(Regex.Replace(name,"-(l|r)-(front|mid|rear)$",""),"-[0-9]+$",""),"-(l|r)$","");if(name=="root")continue;
                Check(py.Contains("'"+name+"'")||py.Contains("'"+stem+"-")||py.Contains("'"+stem+"'"),name+" is in the script's table");
            }
            Check(py.Contains("BANK = 'ChipArenaPackBoss647'"),"the script writes the bank the game reads");
        }
        private static void Draws(string root)
        {
            string body=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeStationBossBody.cs")),pres=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeStationBossPresentation.cs"));
            Check(body.Contains("StationBossBodyBank=\"ChipArenaPackBoss647\""),"the game reads the script's bank");
            int bank=pres.IndexOf("if(StationBossBodyDraw(pose,fx,tier,code))return;",StringComparison.Ordinal),sculpt=pres.IndexOf("var mesh=meshes[i];int j=joints[i];",StringComparison.Ordinal);Check(bank>=0&&sculpt>bank,"the bank's model before the sculpt");
            Check(body.Contains("NativeZombieLooks.Stationmaster"),"the Stationmaster on the player's model");Check(NativeZombieLooks.Stationmaster.Gear.Length==0&&!NativeZombieLooks.All.Contains(NativeZombieLooks.Stationmaster),"no add wears him");
            foreach(var bad in new[]{"Rigidbody","Collider","NativeNetwork","Physics.","Damage("})Check(!body.Contains(bad),"the body stays presentation only (no "+bad+")");
        }
        private static void Kinds(string root)
        {
            string h=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs"));
            Check(h.Contains("s.Tier=s.Pinned||r<full&&"),"a pinned kind is on the full tier at any distance and rank");Check(h.Contains("s.Possessed||s.Pinned?-1:"),"it ranks first");
            Check(h.Contains("if(!HordeNoVoxel(kind)){HordeQueue("),"a frame without a body draws no voxel");Check(h.Contains("!ZombieModelDeath(rig,death.id,d.Kind,death.lost,d.Slice)&&!HordeNoVoxel(d.Kind)"),"nor does its fall");
            for(int k=0;k<NativeStationKinds.KindCount;k++){int kind=NativeStationKinds.Kind(k);Check(NativeZombieLooks.Modelled(kind),NativeStationKinds.Kinds[k].Id+" is live on the model");Check(NativeZoneCombat.Cap(NativeStationKinds.Kinds[k].Id,4,true)<=2,NativeStationKinds.Kinds[k].Id+" stays few: its cap is what pays for its bodies");}
        }
        // The local bank, when it is here.
        private static string Bank(string root)
        {
            string path=Environment.GetEnvironmentVariable("STATION_BOSS_BANK");if(string.IsNullOrEmpty(path))path=Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaPackBoss647.bytes");if(!File.Exists(path))return "; no local bank here";
            var bank=NativePackBank.Read(File.ReadAllBytes(path));var tris=new int[3];
            foreach(var p in bank.Props)
            {
                var m=Regex.Match(p.Name,"^647-(.*)-([0-2])$");Check(m.Success,p.Name+" is named for its joint and tier");Check(Array.IndexOf(NativeStationBossLook.Names,m.Groups[1].Value)>=0,p.Name+" rides a joint the rig poses");Check(p.Group==m.Groups[1].Value,p.Name+"'s group is its joint");
                int tier=int.Parse(m.Groups[2].Value);foreach(var part in p.Parts)foreach(var sub in bank.Meshes[part.Mesh].Submeshes)tris[tier]+=sub.Indices.Length/3;
                if(m.Groups[1].Value!="body")continue;
                foreach(var part in p.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)Check(v.X>=NativeStationBossModel.BodyBoxMin.X-.01f&&v.X<=NativeStationBossModel.BodyBoxMax.X+.01f&&v.Y>=NativeStationBossModel.BodyBoxMin.Y-.01f&&v.Y<=NativeStationBossModel.BodyBoxMax.Y+.01f&&v.Z>=NativeStationBossModel.BodyBoxMin.Z-.01f&&v.Z<=NativeStationBossModel.BodyBoxMax.Z+.01f,p.Name+" stays in the carriage's box ("+v+")");
            }
            for(int t=0;t<3;t++)Check(tris[t]>0&&tris[t]<=Budget[t],"tier "+t+" holds its budget ("+tris[t]+" of "+Budget[t]+")");
            return "; the local bank's tiers "+string.Join(", ",tris)+" triangles";
        }
    }
}
#endif
