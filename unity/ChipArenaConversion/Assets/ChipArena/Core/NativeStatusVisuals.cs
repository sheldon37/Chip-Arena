using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Cosmetic status rigs (client/status-effects.ts StatusRig, 05950ea) and team bands (client/team-identity.ts).
    // Everything is in the source frame (+x = left); the Unity adapter mirrors x once at its boundary.
    // Nothing here feeds the simulation: flags come from authoritative statuses, timing from the match clock.
    public static class NativeStatusVisuals
    {
        // status-effects.ts:7 STATUS_VFX, :24 (10 slow cubes, 8 mark cubes) and pve/horde-view.ts:36 STATUS_RIGS.
        public const int Flames=8,Embers=6,Bubbles=9,ReducedFlames=4,ReducedEmbers=2,ReducedBubbles=4,FirstPersonParts=3,SlowCubes=10,MarkCubes=8,MaxPieces=Flames+Embers+Bubbles+SlowCubes+MarkCubes,AddRigs=16;
        public const float FireRise=1.45f,BubbleRise=1.9f,BubblePeriod=1.65f;
        // ability-system.ts:162-165 keeps the kind of the strongest slow. Shadow Shackles (.4, the clamp floor) is the only
        // source at .4 (tidal wave/geyser .6, water soak .8), so a .4 factor is exactly the 'shackles' kind.
        public const float ShackleFactor=.4f;
        public static bool Shackled(float factor)=>factor<=ShackleFactor+1e-4f;
        public enum Kind:byte {Flame,Ember,Bubble,Slow,Shackle,Mark}
        // A shown root part. Flames carry their inner core and bubbles their rim/glint as fixed children (below).
        public struct Piece {public Kind Kind;public Vector3 Position;public Quaternion Rotation;public Vector3 Scale;public bool Popping;}
        public struct Rig
        {
            public bool Active,FirstPerson,Fire,Poison,Slowed,Shackled,Marked;
            public float SlowUntil,MarkBorn,MarkLock,MarkUntil,Quality;
            public Vector3 Root,Camera;public Quaternion CameraRotation;
        }
        // status-effects.ts:25 flame core and :27 bubble rim/glint, relative to their parent part.
        public static readonly Vector3 CorePosition=new Vector3(.012f,.015f,.008f),CoreScale=new Vector3(.45f,.64f,1),RimPosition=new Vector3(0,0,.05f),GlintPosition=new Vector3(-.3f,.35f,.86f),GlintScale=new Vector3(.18f,.28f,.13f);
        // status-effects.ts:10-15,18 MeshBasicMaterial surfaces: unlit and fogged; translucent ones (opacity < 1) never write depth.
        public enum Surface:byte {Fire,Core,Ember,Bubble,Rim,Glint,Slow,Shackle,Mark}
        public struct Style {public int Color;public float Opacity;public bool DoubleSide;public bool Transparent=>Opacity<1;public Style(int color,float opacity,bool doubleSide){Color=color;Opacity=opacity;DoubleSide=doubleSide;}}
        public static readonly Style[] Surfaces={new Style(0xff6b24,.72f,true),new Style(0xffdc78,.85f,true),new Style(0xffb34f,1,false),new Style(0x6ec134,.24f,false),new Style(0x94e94d,.75f,true),new Style(0xe0ffbd,.8f,false),new Style(0x9ff0ff,1,false),new Style(0xb45cff,1,false),new Style(0xff4b3a,1,false)};
        private static float Frac(double x)=>(float)(x-Math.Floor(x));
        private static Quaternion Euler(double x,double y,double z)=>CapturedRig.Euler((float)x,(float)y,(float)z);
        // StatusRig.update (status-effects.ts:29-52) for one rig at `time`. Returns the number of pieces written.
        public static int Build(in Rig rig,float time,Piece[] output)
        {
            int count=0;double t=time;bool fp=rig.FirstPerson,reduced=rig.Quality<.5f;
            if(rig.Active&&rig.Fire)
            {
                int flames=fp?FirstPersonParts:reduced?ReducedFlames:Flames;double yaw=Math.Atan2(rig.Camera.X-rig.Root.X,rig.Camera.Z-rig.Root.Z);
                for(int n=0;n<flames;n++)
                {
                    double angle=n*2.399,offset=t*2.9+n*.83,pulse=.75+.25*Math.Sin(offset*2),radius=fp?.045:.28+(n%3)*.055;
                    output[count++]=new Piece{Kind=Kind.Flame,Position=new Vector3((float)(Math.Cos(angle)*radius),(float)(fp?-.11+n*.035:-.65+(n%3)*.36),(float)(Math.Sin(angle)*radius+(fp?.14:0))),
                        Scale=new Vector3((float)((fp?.17:.43)*pulse),(float)((fp?.24:.75)*(.75+.25*Math.Sin(offset))),1),Rotation=fp?Euler(0,Math.PI,Math.Sin(offset)*.15):Euler(0,yaw,Math.Sin(offset)*.13)};
                }
                if(!fp)for(int n=0;n<(reduced?ReducedEmbers:Embers);n++)
                {
                    double u=Frac(t*.8+n*.173),angle=n*2.4+t*.45,r=.3+u*.12;
                    output[count++]=new Piece{Kind=Kind.Ember,Position=new Vector3((float)(Math.Cos(angle)*r),(float)(-.25+u*FireRise),(float)(Math.Sin(angle)*r)),Scale=new Vector3((float)(.027*(1-u)+.006)),Rotation=Euler(0,t*2+n,0)};
                }
            }
            if(rig.Active&&rig.Poison)
            {
                int bubbles=fp?FirstPersonParts:reduced?ReducedBubbles:Bubbles;Quaternion facing=fp?Euler(0,Math.PI,0):rig.CameraRotation;
                for(int n=0;n<bubbles;n++)
                {
                    double u=Frac(t/BubblePeriod+n*.117),angle=n*2.399+Math.Sin(t+n)*.18,radius=fp?.08:.43+Math.Sin(n*3)*.11;bool popping=u>.87;
                    double size=(fp?.028:.06+(n%3)*.025)*(1+u*.45)*(popping?1+(u-.87)*5:1);
                    output[count++]=new Piece{Kind=Kind.Bubble,Popping=popping,Position=new Vector3((float)(Math.Cos(angle)*radius),(float)(fp?-.14+u*.36:-.55+u*BubbleRise),(float)(Math.Sin(angle)*radius+(fp?.12:0))),
                        Scale=new Vector3((float)size,(float)(size*(1+Math.Sin(t*4+n)*.05)),(float)size),Rotation=facing};
                }
            }
            if(rig.Active&&!fp&&rig.Slowed&&rig.SlowUntil>time)
            {
                bool shackled=rig.Shackled;
                for(int n=0;n<SlowCubes;n++)
                {
                    double a=n*Math.PI/5+t*(shackled?1.2:2.4);int cuff=n%2==1?1:-1;var piece=new Piece{Kind=shackled?Kind.Shackle:Kind.Slow,Rotation=Euler(t+n,a,0)};
                    if(shackled){piece.Position=new Vector3((float)(cuff*.14+Math.Cos(a)*.13),(float)(-.8+Math.Sin(t*3+n)*.015),(float)(Math.Sin(a)*.13));piece.Scale=new Vector3(.1f,.08f,.1f);}
                    else{double u=Frac(t*.9+n*.1);piece.Position=new Vector3((float)(Math.Cos(a)*.42),(float)(-.25-u*.6),(float)(Math.Sin(a)*.42));piece.Scale=new Vector3((float)(.11*(1-u*.5)));}
                    output[count++]=piece;
                }
            }
            if(rig.Active&&!fp&&rig.Marked&&rig.MarkUntil>time)
            {
                double lockAt=rig.MarkLock,u=Math.Max(0,Math.Min(1,(lockAt-t)/Math.Max(.01,lockAt-rig.MarkBorn))),r=.32+u*.35;bool locked=time>=rig.MarkLock;
                if(!locked||Math.Sin(t*40)>-.4)for(int n=0;n<MarkCubes;n++)
                {
                    int k=n>>1;bool odd=n%2==1;double a=k*Math.PI/2+t*(locked?0:1.6),side=odd?.12:0;
                    output[count++]=new Piece{Kind=Kind.Mark,Position=new Vector3((float)(Math.Cos(a)*r-Math.Sin(a)*side),(float)(.62+(odd?0:.12)),(float)(Math.Sin(a)*r+Math.Cos(a)*side)),Rotation=Euler(0,-a,0),Scale=new Vector3(odd?.2f:.06f,odd?.06f:.2f,.06f)};
                }
            }
            return count;
        }
        // pve/horde-view.ts:121 root of an add's rig: ground + .85 (.35 crawling); impostors.ts:19,23 snaps ground within .1 of the
        // floor to 0. Native add capsules are 1.7 m with their centre .85 above the ground in every state.
        public static float AddRootHeight(float bodyY,bool crawl){float ground=bodyY-.85f;if(Math.Abs(ground)<.1f)ground=0;return ground+(crawl?.35f:.85f);}

        // ---- Team bands (team-identity.ts:5-13). Unlit, not tone-mapped, shown only in a teams match. ----
        public static readonly int[] TeamColors={0xff6575,0x69a6ff,0x7ed78a,0xf5db70,0xba94f5,0xffa565,0x65dce0,0xf18dcc};
        public static readonly int[] BandBones={CapturedRig.Chest,CapturedRig.Spine,CapturedRig.LArm,CapturedRig.RArm};
        public static readonly Vector3[] BandPositions={new Vector3(0,.22f,.165f),new Vector3(0,.12f,-.17f),new Vector3(0,-.12f,0),new Vector3(0,-.12f,0)};
        public static readonly Vector3[] BandSizes={new Vector3(.45f,.09f,.02f),new Vector3(.36f,.16f,.03f),new Vector3(.17f,.075f,.19f),new Vector3(.17f,.075f,.19f)};
        public static int TeamIndex(int team)=>((team%8)+8)%8;
        // The band team (TeamColors index) or -1. PvE forces a teams match with the party in blue and the Siren in purple
        // (pve/contract.ts:26,29); the native PvE seats use their own team numbers, so the source colours are applied here.
        public static int BandTeam(bool teams,bool cooperative,bool boss,int team)=>!teams?-1:cooperative?(boss?4:1):TeamIndex(team);

        // ---- Source geometry (three r180) for the status meshes; z = 0 flame plane, unit spheres/ring. ----
        // ShapeGeometry(flameShape,4) output (status-effects.ts:8-9), checked against three in Tests/status-parity.json.
        public static readonly float[] FlameOutline={0,0,-.232969f,.074844f,-.33375f,.20875f,-.305156f,.378281f,-.15f,.56f,-.164375f,.475625f,-.1475f,.4125f,-.099375f,.370625f,-.02f,.35f,.057813f,.509219f,.0475f,.65625f,.033438f,.812656f,.1f,1f,.163906f,.775156f,.25875f,.58125f,.306719f,.396719f,.23f,.2f,.275625f,.15375f,.2525f,.105f,.160625f,.05375f};
        public static readonly int[] FlameTriangles={1,0,19,19,18,17,16,15,14,13,12,11,5,4,3,3,2,1,1,19,17,16,14,13,13,11,10,6,5,3,3,1,17,16,13,10,7,6,3,3,17,16,16,10,9,8,7,3,16,9,8,8,3,16};
        public static void Sphere(int width,int height,out Vector3[] points,out int[] triangles)
        {
            // three SphereGeometry(1,width,height): rows from +y, phi from -x around y, poles skip their degenerate triangle.
            points=new Vector3[(width+1)*(height+1)];var index=new System.Collections.Generic.List<int>();
            for(int y=0;y<=height;y++){double v=(double)y/height;for(int x=0;x<=width;x++){double u=(double)x/width;points[y*(width+1)+x]=new Vector3((float)(-Math.Cos(u*Math.PI*2)*Math.Sin(v*Math.PI)),(float)Math.Cos(v*Math.PI),(float)(Math.Sin(u*Math.PI*2)*Math.Sin(v*Math.PI)));}}
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int a=y*(width+1)+x+1,b=y*(width+1)+x,c=(y+1)*(width+1)+x,d=(y+1)*(width+1)+x+1;
                if(y!=0){index.Add(a);index.Add(b);index.Add(d);}
                if(y!=height-1){index.Add(b);index.Add(c);index.Add(d);}
            }
            triangles=index.ToArray();
        }
        public static void Ring(float inner,float outer,int segments,out Vector3[] points,out int[] triangles)
        {
            // three RingGeometry(inner,outer,segments,1): inner ring then outer ring, theta from +x.
            points=new Vector3[2*(segments+1)];triangles=new int[segments*6];
            for(int j=0;j<=1;j++){double radius=j==0?inner:outer;for(int i=0;i<=segments;i++){double a=(double)i/segments*Math.PI*2;points[j*(segments+1)+i]=new Vector3((float)(radius*Math.Cos(a)),(float)(radius*Math.Sin(a)),0);}}
            for(int i=0;i<segments;i++){int a=i,b=i+segments+1,c=i+segments+2,d=i+1,o=i*6;triangles[o]=a;triangles[o+1]=b;triangles[o+2]=d;triangles[o+3]=b;triangles[o+4]=c;triangles[o+5]=d;}
        }
        // three OctahedronGeometry(1,0): eight faces, non-indexed in three; indexed here with the same corner order.
        public static readonly Vector3[] OctahedronPoints={new Vector3(1,0,0),new Vector3(-1,0,0),new Vector3(0,1,0),new Vector3(0,-1,0),new Vector3(0,0,1),new Vector3(0,0,-1)};
        public static readonly int[] OctahedronTriangles={2,4,0,4,3,0,3,5,0,5,2,0,2,5,1,5,3,1,3,4,1,4,2,1};
    }
}
