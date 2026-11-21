using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public static class NativeThunderheart
    {
        public const string Id="thunderheart", Song="music/thunderheart", Revision="carillon-1";
        public const float Length=224.04f;
        public const string WestStop="amber-stop", EastStop="jade-stop";
        public const int NorthGate=0, SouthGate=1, WestWell=2, EastWell=3, Bellows=4, Grate=5;
        // The movement begins at the recorded build (36 s). The second crown comes down one phrase later.
        public static double Clock(NativePhase phase,double fightAt,double now)
            =>phase==NativePhase.Fight||phase==NativePhase.Result?Math.Max(0,now-fightAt):0;
        public static bool Fed(int well,bool amberIntact,bool jadeIntact)
            =>well==WestWell?!jadeIntact:well==EastWell&&!amberIntact;
        public static ContentMap Build()
        {
            var blocks=new List<ContentBlock>();
            void Solid(string id,V3 pos,V3 size,float tilt=0,string role="static",float hp=0,bool loose=false)
            {
                double half=tilt*Math.PI/360;
                blocks.Add(new ContentBlock{id=id,pos=pos,size=size,role=role,material=role=="static"?"foundation":"metal",
                    rotation=new ContentRotation{z=(float)Math.Sin(half),w=(float)Math.Cos(half)},hp=hp,mass=loose?58:24,dynamic=loose});
            }
            Solid("terrace",new V3(0,-.7f,0),new V3(52,1.4f,38));
            foreach(int side in new[]{-1,1})
            {
                float z=side*13;
                Solid("gallery"+side,new V3(0,1.5f,z),new V3(14,3,5));
                foreach(int end in new[]{-1,1})
                {
                    float tilt=-end*(float)(Math.Atan2(3,8)*180/Math.PI);
                    // Top face runs from (7,3) to (15,0); offset centre along the face normal.
                    Solid("ramp"+side+"."+end,new V3(end*10.9473f,1.35955f,z),new V3(8.544f,.3f,5),tilt);
                    Solid("pier"+side+"."+end,new V3(end*4.4f,2.6f,side*4.4f),new V3(2.4f,5.2f,2.4f));
                }
                Solid(side<0?WestStop:EastStop,new V3(side*12,1.1f,side*4),new V3(1.8f,2.2f,1.6f),0,"cover",55);
                Solid("votive"+side,new V3(side*17,1.35f,-side*5),new V3(3.4f,2.7f,.7f),0,"cover",88);
                Solid("drum"+side,new V3(side*8,.85f,-side*8),new V3(1.6f,1.7f,1.6f),0,"cover",110,true);
                Solid("sentinel"+side,new V3(side*20,1.45f,side*8),new V3(1.4f,2.9f,1.4f));
            }
            MapHazard Gate(int sign,float first)=>new MapHazard{id=Id+"-iris"+sign,kind="shutter",center=new V3(0,2.4f,sign*4.4f),size=new V3(6.4f,4.8f,.52f),axis=new V3(0,0,1),
                first=first,period=56,warn=3,travel=1.6f,dwell=10,rise=2,push=5};
            MapHazard Well(int sign)=>new MapHazard{id=Id+"-well"+sign,kind="vent",a=new V3(sign*5,0,sign*8),size=new V3(3,2.2f,3),axis=new V3(0,1,0),
                start=0,period=8,offset=0,warn=2,dwell=2,tick=.5f,push=11,pushMode="launch"};
            var hazards=new[]{Gate(1,33),Gate(-1,47),Well(-1),Well(1),
                new MapHazard{id=Id+"-bellows",kind="vent",a=new V3(0,0,-7.8f),axis=new V3(1,0,0),size=new V3(4,2,3),start=0,period=16,warn=2,dwell=2,tick=.5f,push=6,damage=1,pushMode="wind"},
                new MapHazard{id=Id+"-grate",kind="grid",center=new V3(0,.3f,7.8f),axis=new V3(1,0,0),size=new V3(4,.6f,3),first=12,period=16,charge=2,live=2,tick=.5f,damage=5}};
            var nodes=new List<MapNavNode>();var links=new List<MapNavLink>();
            MapNavNode Node(int x,int z)
            {
                float y=Math.Abs(z)==13?Math.Min(3,Math.Max(0,(15-Math.Abs(x))*3f/8)):0;
                var n=new MapNavNode{id=x+"_"+z,pos=new V3(x,y+.9f,z),safe=true};nodes.Add(n);return n;
            }
            void Link(MapNavNode a,MapNavNode b,string[] h)
            {links.Add(new MapNavLink{id=a.id+">"+b.id,a=a.id,b=b.id,hazards=h});}
            int[] xs={-22,-15,-7,0,7,15,22},zs={-16,-13,-9,-6,0,6,9,13,16};
            var grid=new MapNavNode[xs.Length,zs.Length];
            for(int i=0;i<xs.Length;i++)for(int j=0;j<zs.Length;j++)
            {
                var b=grid[i,j]=Node(xs[i],zs[j]);
                if(i>0){var a=grid[i-1,j];Link(a,b,null);Link(b,a,null);}
                if(j==0||Math.Abs(xs[i])<15&&(Math.Abs(zs[j])==13||Math.Abs(zs[j-1])==13))continue;
                var prev=grid[i,j-1];string[] gates=null;
                if(xs[i]==0){int index=zs[j]==6?NorthGate:zs[j]==0?SouthGate:zs[j]==9?Grate:-1;if(index>=0)gates=new[]{hazards[index].id};}
                Link(prev,b,gates);Link(b,prev,gates);
            }
            MapSpawn Spawn(float x,float z)=>new MapSpawn{pos=new V3(x,1.05f,z),yaw=(float)(Math.Atan2(-x,-z)*180/Math.PI)};
            var ffa=new[]{Spawn(-22,-12),Spawn(22,12),Spawn(-22,12),Spawn(22,-12),Spawn(-21,1),Spawn(21,-1),Spawn(-15,-16),Spawn(15,16)};
            return new ContentMap{id=Id,unity=true,rotation=false,name="Thunderheart Carillon",theme=Id,look="carillon-storm",music="thunderheart",tag="Break a tuning stop to power the opposite updraft. Watch the crown for closing gates.",
                sky=0x253f55,floor=0x47636a,accent=0xf5bd62,bounds=new MapBounds{minX=-25,maxX=25,minZ=-18,maxZ=18,ceiling=28,killY=-12},
                blocks=blocks.ToArray(),spawns=new MapSpawns{ffa=ffa,teams=new[]{new MapTeamSpawns{side=0,points=ffa.Where(p=>p.pos.x<0).ToArray()},new MapTeamSpawns{side=1,points=ffa.Where(p=>p.pos.x>0).ToArray()}}},
                hazards=hazards,segments=Array.Empty<StructureDefinition>(),pilotBlocks=Array.Empty<MapPilotBlock>(),nav=new MapNav{nodes=nodes.ToArray(),links=links.ToArray()}};
        }
    }
}
