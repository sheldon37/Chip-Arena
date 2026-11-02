using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Backrooms layout contract and its stand-in Level 0 generator, the kit placement plan and the lamps (Core
    // NativeBackrooms; docs/BACKROOMS.md), and the DEV preview's wiring (Runtime NativeBackroomsPresentation, off by default).
    internal static class BackroomsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Backrooms: "+what);count++;}
        public static void Run(string root)
        {
            count=0;
            var a=NativeBackrooms.Preview(7,24,24);var b=NativeBackrooms.Preview(7,24,24);var c=NativeBackrooms.Preview(8,24,24);
            Check(a.WallX.SequenceEqual(b.WallX)&&a.WallZ.SequenceEqual(b.WallZ)&&a.Pillar.SequenceEqual(b.Pillar)&&a.Lamps.SequenceEqual(b.Lamps)&&a.Circuits.SequenceEqual(b.Circuits),"one seed gives one layout (host and guests alike)");
            Check(!a.WallX.SequenceEqual(c.WallX)||!a.WallZ.SequenceEqual(c.WallZ),"another seed gives another layout");
            foreach(var l in new[]{a,c,NativeBackrooms.Preview(31,40,40),NativeBackrooms.Preview(2,16,16)})
            {
                bool border=true;for(int z=0;z<l.Depth;z++)border&=l.Between(0,z,-1,0)==BackroomsLayout.Wall&&l.Between(l.Width-1,z,1,0)==BackroomsLayout.Wall;
                for(int x=0;x<l.Width;x++)border&=l.Between(x,0,0,-1)==BackroomsLayout.Wall&&l.Between(x,l.Depth-1,0,1)==BackroomsLayout.Wall;
                Check(border,"the outer border is walled ("+l.Seed+")");
                Check(NativeBackrooms.Flood(l,new bool[l.Width*l.Depth],new int[l.Width*l.Depth])==0,"every open cell is reachable from the entry ("+l.Seed+", "+l.Width+"x"+l.Depth+")");
                int walls=l.WallX.Count(w=>w!=0)+l.WallZ.Count(w=>w!=0)-2*(l.Width+l.Depth),lamps=l.Lamps.Count(m=>m!=0),pillars=l.Pillar.Count(p=>p!=0);
                Check(walls>l.Width*l.Depth/4&&lamps>=l.Width*l.Depth/3&&pillars>0,"Level 0: inner walls, rows of lamps and pillars ("+walls+" walls, "+lamps+" lamp cells, "+pillars+" pillars)");
                Check(l.Pillar[l.Index(l.EntryX,l.EntryZ)]==0&&l.Pillar[l.Index(l.ExitX,l.ExitZ)]==0,"the entry and exit cells are clear");
            }
            // The plan: only what lies within reach, every near cell floored and ceiled, its open ceiling slot filled.
            var origin=new Vector3(1000,0,1000);var eye=new Vector3(1004.5f,1.6f,1004.5f);var o=new NativeBackrooms.Piece[16000];
            int n=NativeBackrooms.Plan(a,origin,eye,20,o);var pieces=o.Take(n).ToArray();
            Check(pieces.All(p=>(p.X-eye.X)*(p.X-eye.X)+(p.Z-eye.Z)*(p.Z-eye.Z)<=20*20+1e-3f),"no piece beyond reach is drawn");
            int floors=pieces.Count(p=>p.Kind==NativeBackrooms.Kind.Floor),ceilings=pieces.Count(p=>p.Kind==NativeBackrooms.Kind.Ceiling);
            int centred=pieces.Count(p=>p.Kind==NativeBackrooms.Kind.Tile)+pieces.Count(p=>(p.Kind==NativeBackrooms.Kind.Lamp||p.Kind==NativeBackrooms.Kind.Vent)&&Math.Abs((p.X-origin.X)%3-1.5f)<.01f&&Math.Abs((p.Z-origin.Z)%3-1.5f)<.01f);
            Check(floors>0&&floors==ceilings&&centred==ceilings,"each near cell has a floor, a ceiling and its centre slot filled ("+floors+" cells)");
            Check(pieces.Any(p=>p.Kind==NativeBackrooms.Kind.Wall)&&pieces.Any(p=>p.Kind==NativeBackrooms.Kind.Post)&&pieces.All(p=>p.Kind!=NativeBackrooms.Kind.Wall||p.Yaw==0||p.Yaw==90),"walls on the grid lines, posts where they end or turn");
            Check(pieces.Where(p=>p.Kind==NativeBackrooms.Kind.Wall&&p.Yaw==0).All(p=>Math.Abs(((p.Z-origin.Z)%3+3)%3)<.01f),"a wall along x stands on a cell line");
            Check(NativeBackrooms.Plan(a,origin,new Vector3(-500,0,-500),20,o)==0,"nothing is planned far from the layout");
            // The lamps.
            Check(NativeBackrooms.Level(BackroomsLayout.On,3,1.5)==1&&NativeBackrooms.Level(BackroomsLayout.Dead,3,1.5)==0,"a lamp is on, or dead");
            float lo=1,hi=0;int steps=0;for(double t=0;t<20;t+=1/60.0){float v=NativeBackrooms.Level(BackroomsLayout.Flicker,11,t);lo=Math.Min(lo,v);hi=Math.Max(hi,v);steps++;}
            Check(lo==0&&hi==1,"a flickering circuit dips out and comes back");
            var pos=new float[32];var rgb=new float[24];int lit=NativeBackrooms.NearLamps(a,origin,eye,1.5,8,pos,rgb,new Vector3(1,1,1),6);
            bool deadOut=true;for(int k=0;k<8;k++){if(pos[k*4+1]<-5000)continue;int cx=(int)((pos[k*4]-origin.X)/3),cz=(int)((pos[k*4+2]-origin.Z)/3);deadOut&=a.Circuits[a.Circuit[a.Index(cx,cz)]]!=BackroomsLayout.Dead&&a.Lamps[a.Index(cx,cz)]!=0;}
            Check(lit>0&&lit<=8&&deadOut,"the nearest lamps light the scene, no dead one among them ("+lit+")");
            // The DEV preview: off unless asked for, drawn from the kit bank only.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));var pr=R("Runtime/NativeBackroomsPresentation.cs");
            Check(!PreviewOnByDefault(pr)&&R("Runtime/NativeSourcePresentation.cs").Contains("BackroomsWorld();"),"the preview is off by default and drawn from the world pass when on");
            Check(pr.Contains("var bank=NativePackBanks.Bank(BackroomsBank);if(bank==null)return;"),"without the kit bank nothing is drawn");
            Console.WriteLine("PASS backrooms ("+count+" checks: deterministic Level 0 layouts, walled and connected, the kit planned within reach, lamps and circuits, the DEV preview off by default)");
        }
        // The preview's flag is false unless the command line or a tool sets it.
        private static bool PreviewOnByDefault(string source)=>!source.Contains("public static bool On;")||source.Contains("public static bool On=true");
    }
}
