using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Backrooms layout contract and its stand-in Level 0 generator, the Level 0 geometry (Core NativeBackrooms,
    // BackroomsMesh; docs/BACKROOMS.md; the owner's reference photo, 2026-11-02: enclosed rooms and corridors, walls to a low drop
    // ceiling, doorways, strip fixtures), the lights, and the DEV preview's wiring (Runtime NativeBackroomsPresentation).
    internal static class BackroomsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Backrooms: "+what);count++;}
        public static void Run(string root)
        {
            count=0;
            var a=NativeBackrooms.Preview(7,24,24);var b=NativeBackrooms.Preview(7,24,24);var c=NativeBackrooms.Preview(8,24,24);
            Check(a.WallX.SequenceEqual(b.WallX)&&a.WallZ.SequenceEqual(b.WallZ)&&a.Pillar.SequenceEqual(b.Pillar)&&a.Light.SequenceEqual(b.Light)&&a.Circuits.SequenceEqual(b.Circuits),"one seed gives one layout (host and guests alike)");
            Check(!a.WallX.SequenceEqual(c.WallX)||!a.WallZ.SequenceEqual(c.WallZ),"another seed gives another layout");
            foreach(var l in new[]{a,c,NativeBackrooms.Preview(31,40,40),NativeBackrooms.Preview(2,16,16)})
            {
                bool border=true;for(int z=0;z<l.Depth;z++)border&=l.Between(0,z,-1,0)==BackroomsLayout.Wall&&l.Between(l.Width-1,z,1,0)==BackroomsLayout.Wall;
                for(int x=0;x<l.Width;x++)border&=l.Between(x,0,0,-1)==BackroomsLayout.Wall&&l.Between(x,l.Depth-1,0,1)==BackroomsLayout.Wall;
                Check(border,"the outer border is walled ("+l.Seed+")");
                Check(NativeBackrooms.Flood(l,new bool[l.Width*l.Depth],new int[l.Width*l.Depth])==0,"every cell is reachable from the entry ("+l.Seed+", "+l.Width+"x"+l.Depth+")");
                int inner=(l.Width-1)*l.Depth+l.Width*(l.Depth-1),walled=0,doors=0;
                for(int z=0;z<l.Depth;z++)for(int x=0;x<l.Width;x++){if(x+1<l.Width){byte e=l.Between(x,z,1,0);if(e!=0)walled++;if(e==BackroomsLayout.Short)doors++;}if(z+1<l.Depth){byte e=l.Between(x,z,0,1);if(e!=0)walled++;if(e==BackroomsLayout.Short)doors++;}}
                Check(walled>inner/4&&doors>0,"Level 0 is rooms and corridors behind walls with doorways, not an open hall ("+walled+" of "+inner+" inner edges walled, "+doors+" doorways)");
                Check(l.Light.Count(x=>x)>=l.Width*l.Depth/5,"a fixture over every other cell");
            }
            // The geometry.
            var mesh=new BackroomsMesh();var origin=new Vector3(1000,0,1000);mesh.Build(a,origin);
            var carpet=mesh.Parts[(int)BackroomsMesh.Surface.Carpet];var ceiling=mesh.Parts[(int)BackroomsMesh.Surface.Ceiling];var paper=mesh.Parts[(int)BackroomsMesh.Surface.Wallpaper];
            int cells=a.Width*a.Depth;
            Check(carpet.Indices.Count==cells*6&&ceiling.Indices.Count==cells*6,"every cell has its carpet and its ceiling tiles");
            Check(ceiling.Positions.All(p=>Math.Abs(p.Y-BackroomsLayout.Height)<1e-4f)&&ceiling.Normals.All(n=>n.Y<-.99f),"the drop ceiling is low and faces down");
            Check(paper.Positions.Count>0&&Math.Abs(paper.Positions.Max(p=>p.Y)-BackroomsLayout.Height)<1e-4f&&paper.Positions.Min(p=>p.Y)>-1e-4f,"the walls run from the floor to the ceiling");
            bool facing=true;foreach(var part in mesh.Parts)for(int t=0;t+2<part.Indices.Count;t+=3){var p0=part.Positions[part.Indices[t]];var f=Vector3.Cross(part.Positions[part.Indices[t+1]]-p0,part.Positions[part.Indices[t+2]]-p0);facing&=Vector3.Dot(f,part.Normals[part.Indices[t]])>0;}
            Check(facing,"every face is wound to face out along its normal");
            int lit=a.Light.Select((x,i)=>x&&a.Circuits[a.Circuit[i]]!=BackroomsLayout.Flicker&&a.Circuits[a.Circuit[i]]!=BackroomsLayout.Dead).Count(x=>x);
            int dead=a.Light.Select((x,i)=>x&&a.Circuits[a.Circuit[i]]==BackroomsLayout.Dead).Count(x=>x);
            Check(mesh.Parts[(int)BackroomsMesh.Surface.Light].Indices.Count==lit*6&&mesh.Parts[(int)BackroomsMesh.Surface.DeadLight].Indices.Count==dead*6&&mesh.Flickers.Count==a.Light.Count(x=>x)-lit-dead,"each fixture is lit, dead or flickering by its circuit");
            Check(paper.Uv.All(u=>Math.Abs(u.X)<1e5f&&u.Y>=-1e-4f&&u.Y<=BackroomsLayout.Height/BackroomsMesh.WallpaperRepeat+1e-3f),"the wallpaper repeats with the wall's run and height");
            var block=new BackroomsMesh();block.Build(a,origin,0,0,8,8);Check(block.Parts[(int)BackroomsMesh.Surface.Carpet].Indices.Count==64*6,"a block builds its own cells only");
            // The lights.
            Check(NativeBackrooms.Level(BackroomsLayout.On,3,1.5)==1&&NativeBackrooms.Level(BackroomsLayout.Dead,3,1.5)==0,"a fixture is on, or dead");
            float lo=1,hi=0;for(double t=0;t<20;t+=1/60.0){float v=NativeBackrooms.Level(BackroomsLayout.Flicker,11,t);lo=Math.Min(lo,v);hi=Math.Max(hi,v);}
            Check(lo==0&&hi==1,"a flickering circuit dips out and comes back");
            var eye=new Vector3(origin.X+3.6f,1.6f,origin.Z+3.6f);var pos=new float[32];var rgb=new float[24];int on=NativeBackrooms.NearLamps(a,origin,eye,1.5,8,pos,rgb,new Vector3(1,1,1),6);
            bool deadOut=true;for(int k=0;k<8;k++){if(pos[k*4+1]<-5000)continue;int cx=(int)((pos[k*4]-origin.X)/BackroomsLayout.Cell),cz=(int)((pos[k*4+2]-origin.Z)/BackroomsLayout.Cell);deadOut&=a.Circuits[a.Circuit[a.Index(cx,cz)]]!=BackroomsLayout.Dead&&a.Light[a.Index(cx,cz)];}
            Check(on>0&&on<=8&&deadOut,"the nearest fixtures light the scene, no dead one among them ("+on+")");
            // The DEV preview: off unless asked for, drawn from the bank's Level 0 textures only.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));var pr=R("Runtime/NativeBackroomsPresentation.cs");
            Check(pr.Contains("public static bool On;")&&!pr.Contains("public static bool On=true")&&R("Runtime/NativeSourcePresentation.cs").Contains("BackroomsWorld();"),"the preview is off by default and drawn from the world pass when on");
            Check(pr.Contains("if(NativePackBanks.Texture(BackroomsTextures[0])==null)return;"),"without the bank's Level 0 textures nothing is drawn");
            Console.WriteLine("PASS backrooms ("+count+" checks: deterministic Level 0 layouts, enclosed rooms and corridors with doorways, all connected; walls to the low drop ceiling, faces out, fixtures by circuit, the DEV preview off by default)");
        }
    }
}
