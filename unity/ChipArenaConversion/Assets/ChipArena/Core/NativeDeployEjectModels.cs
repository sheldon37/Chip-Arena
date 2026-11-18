using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The eject's set under the Deployment Station's floor (Core NativeDeployEject's beats; docs/EXPEDITION-OVERHAUL.md R8), sculpted
    // with the Standard weapons' kit like the station itself (NativeDeployModels) and drawn by Runtime/NativeDeployScene.cs only
    // after Deploy. Presentation only and engine-free: the checks build what the player builds. Unity's frame as authored, y up.
    // Section bones as NativeDeployModels: 0 the lit body, 1 the lights the draw tints (the tube's rings, the collar's lamps, the
    // seat's strip, the clamps' tell-tales, the belly's floods, the city's roads and windows), -3 screen faces (none here).
    //   Tube(length)   a drop tube's inside from its top (y 0) down `length` m: dark panels between ring lights, four guide rails,
    //                  hazard bands at both ends, its outer skin for the length below the belly;
    //   Collar         the fitting where a tube meets its pod's roof (y 0 the pod's roof): a lit ring round the roof hatch's dark
    //                  opening (the pilot falls into the cabin's dark); the hatch's iris leaves are NativeDeployModels.HatchLeaf;
    //   Seat           the pod's bucket seat (pod frame: base at the origin, doorway toward +z), its pedestal and red strips;
    //   Harness        the harness bar (its pivot at the origin, at the seat's top-back; 0 down on the chest, the draw turns it up);
    //   Clamp          one docking arm (its hinge at the origin, at the dock over the pod's shoulder; the draw mirrors the second);
    //   Belly          the station's underside (station frame): the hull plate over the pods, panel lines, floods, nav lamps;
    //   City(state)    the rolled city far below, from the blueprint like the holo map: dark blocks, lit roads, the districts' halls
    //                  in their colours, the sanctuary ring and the boss gate's beacon (built per state on a worker).
    public static class NativeDeployEjectModels
    {
        public const int Body=0,Lights=1;
        public const string Tube="eject-tube",Collar="eject-collar",Seat="eject-seat",Harness="eject-harness",Clamp="eject-clamp",Belly="eject-belly",City="eject-city";
        private static readonly WeaponPaint Panel=WeaponPaint.Solid(0x1c2029,.03f),PanelLight=WeaponPaint.Solid(0x2a303b,.04f),Seam=WeaponPaint.Solid(0x07080b),
            Steel=WeaponPaint.Solid(0xa3aab6,.06f),SteelDark=WeaponPaint.Solid(0x4a505c,.04f),Graphite=WeaponPaint.Solid(0x2a2d35,.03f),GraphiteDark=WeaponPaint.Solid(0x15171c,.02f),
            Hazard=WeaponPaint.Solid(0xd9a21f,.05f),Black=WeaponPaint.Solid(0x040506),Cushion=WeaponPaint.Solid(0x2b1416,.02f),CushionDark=WeaponPaint.Solid(0x1a0c0e,.01f),
            Pad=WeaponPaint.Solid(0x1e2128,.02f),Hull=WeaponPaint.Solid(0x1f2430,.03f),HullDark=WeaponPaint.Solid(0x12151c,.02f),Block=WeaponPaint.Solid(0x10141d),
            BlockTop=WeaponPaint.Solid(0x1a2030),Ground=WeaponPaint.Solid(0x080a10);
        /// <summary>An unlit light: its colour is its emission (NativeSourceLit skips the grade), the draw's tint its level.</summary>
        private static WeaponPaint Lamp(int hex,float k=1){var c=WeaponPaint.Linear(hex)*k;return new WeaponPaint(c,c,0);}
        private static WeaponPaint LampRgb(Vector3 c){return new WeaponPaint(c,c,0);}
        private const int Ice=0xd4f2ff,Cool=0x7fd6ff,Amber=0xff9a2e,Danger=0xff3a3a,Warm=0xffb45a,Sodium=0xffc27a,White=0xf4f8ff,Go=0x6dffa8;
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Up=-(float)(Math.PI/2),Pi=(float)Math.PI;
        // A vertical cylinder (axis +y) of radius r from y0 to y1 at (x, z), outward or seen from inside.
        private static void Column(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides,bool inward=false)
        {
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));var p=c;
            s.Loft(new[]{WeaponSculpt.Circle(sides,r,r,y0),WeaponSculpt.Circle(sides,r,r,y1)},(i,k)=>p,0,!inward,!inward,null,false,inward);s.Pop();
        }
        // A flat ring facing +y (or -y) at height y about (x, z).
        private static void FlatRing(WeaponSculpt s,in WeaponPaint c,float r0,float r1,float y,int segments,bool down=false,float x=0,float z=0)
        {
            var a=WeaponSculpt.Circle(segments,r0,r0,0);var b=WeaponSculpt.Circle(segments,r1,r1,0);
            s.Push(WeaponSculpt.Rx(down?-Up:Up)*WeaponSculpt.T(x,y,z));for(int i=0;i<segments;i++){int j=(i+1)%segments;s.Quad(c,a[i],b[i],b[j],a[j]);}s.Pop();
        }
        // A flat disc facing +y (or -y) at height y.
        private static void Disc(WeaponSculpt s,in WeaponPaint c,float r,float y,int segments,bool down=false)
        {
            var a=WeaponSculpt.Circle(segments,r,r,0);s.Push(WeaponSculpt.Rx(down?-Up:Up)*WeaponSculpt.T(0,y,0));
            for(int i=0;i<segments;i++){int j=(i+1)%segments;s.Tri(c,Vector3.Zero,a[i],a[j]);}s.Pop();
        }

        // ---- the TUBE ----------------------------------------------------------------------------------------------------------
        /// <summary>The ring lights' pitch down a tube (m), the ring's own height.</summary>
        public const float RingPitch=.72f,RingHeight=.035f;
        /// <summary>A drop tube's inside (its top at y 0, down `length` m; NativeDeployEject.TubeRadius inside): dark panels between
        /// ring lights every RingPitch (ring k on bone 1 + k mod 3: the stage's chase lights the three banks in turn,
        /// NativeDeployEject.RingChase), four guide rails, hazard bands under the floor and over the collar, and its outer skin.</summary>
        public static WeaponMesh TubeOf(float length)
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Tube,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            float r=NativeDeployEject.TubeRadius;const int sides=24;length=Math.Max(1,length);int rings=(int)Math.Floor((length-.3f)/RingPitch);
            // the wall between the rings (inward), its panel seams, the hazard bands at both ends
            float y=0;for(int k=0;k<=rings;k++)
            {
                float next=k<rings?-(.3f+k*RingPitch):-length;if(next<y-.02f)Column(s,k%2==0?Panel:PanelLight,r,0,0,next,y,sides,true);y=next-RingHeight;
            }
            for(int k=0;k<8;k++){float a=k*Pi/4+Pi/8;s.Box(Seam,V((r-.004f)*(float)Math.Cos(a),-length/2,(r-.004f)*(float)Math.Sin(a)),V(.012f,length-.1f,.012f),0,0,-a,0);}
            Column(s,Hazard,r-.006f,0,0,-.22f,-.02f,sides,true);Column(s,Hazard,r-.006f,0,0,-length+.02f,-length+.2f,sides,true);
            for(int k=0;k<4;k++)
            {
                float a=k*Pi/2;var at=V((r-.03f)*(float)Math.Cos(a),-length/2,(r-.03f)*(float)Math.Sin(a));
                s.Box(Steel,at,V(.03f,length,.05f),.006f,0,-a,0);
            }
            // the outer skin (seen under the belly for a deep pod's tube) and its rim under the floor
            Column(s,GraphiteDark,r+.06f,0,0,-length,0,sides);FlatRing(s,Graphite,r,r+.07f,-.005f,sides);
            // the ring lights (bone 1): a lit band inside each joint, a little proud of the wall
            for(int k=0;k<rings;k++){float top=-(.3f+k*RingPitch);s.Into(Lights+k%3);Column(s,Lamp(Ice),r-.008f,0,0,top-RingHeight,top,sides,true);}
            return mesh;
        }

        // ---- the COLLAR and the roof hatch ----------------------------------------------------------------------------------------
        /// <summary>The collar (y 0 the pod's roof): a lit ring round the roof hatch's dark opening (NativeDeployEject.TubeRadius)
        /// and its lamps (bone 1: amber, red as the hatch seals).</summary>
        public static WeaponMesh CollarModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Collar,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);float r=NativeDeployEject.TubeRadius;
            Column(s,SteelDark,r+.16f,0,0,-.02f,.24f,24);Column(s,Graphite,r+.1f,0,0,.24f,.34f,24);Column(s,Steel,r+.02f,0,0,.18f,.24f,24,true);
            FlatRing(s,Graphite,r,r+.1f,.341f,24);
            for(int k=0;k<6;k++){float a=k*Pi/3;s.Box(SteelDark,V((r+.17f)*(float)Math.Cos(a),.1f,(r+.17f)*(float)Math.Sin(a)),V(.07f,.18f,.07f),.01f,0,-a,0);}
            // the opening into the dark cabin (what a pilot drops into: seen from the tube above, its dark swallows them)
            Disc(s,Black,r+.01f,.19f,24);
            s.Into(Lights);FlatRing(s,Lamp(0xffffff),r+.03f,r+.075f,.343f,24);for(int k=0;k<6;k++){float a=k*Pi/3+Pi/6;s.Box(Lamp(0xffffff),V((r+.14f)*(float)Math.Cos(a),.3f,(r+.14f)*(float)Math.Sin(a)),V(.05f,.02f,.05f));}
            return mesh;
        }
        /// <summary>The roof hatch's leaves: NativeDeployModels.HatchLeaf scaled by this (the tube is wider than the pad's hatch).</summary>
        public static float IrisScale=>(NativeDeployEject.TubeRadius+.01f)/NativeDeployModels.HatchRadius;

        // ---- the SEAT and its HARNESS (pod frame: base at the origin, doorway toward +z) --------------------------------------------
        /// <summary>The cushion's top over the pod's base (under the seated pilot's hips: the Chair pose puts them .43 m over the
        /// feet) and the backrest's face; the harness bar's pivot (the seat's top-back).</summary>
        public const float CushionTop=NativePodModels.Floor+.33f,BackZ=-.2f;
        public static readonly Vector3 HarnessPivot=new Vector3(0,NativePodModels.Floor+1.18f,-.17f);
        /// <summary>The harness bar's turn about x (radians) from 0 (down on the chest) to raised (Raised, behind the headrest).</summary>
        public const float Raised=-1.7f;
        public static float HarnessAngle(float down)=>Raised*(1-Math.Max(0,Math.Min(1,down)));
        public static WeaponMesh SeatModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Seat,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);float f=NativePodModels.Floor;
            // the pedestal, its foot plate and its two struts to the back wall
            s.Box(SteelDark,V(0,f+.02f,-.05f),V(.5f,.04f,.5f),.01f);Column(s,Graphite,.08f,0,-.05f,f+.04f,CushionTop-.1f,12);
            s.Box(SteelDark,V(0,CushionTop-.12f,.02f),V(.5f,.05f,.48f),.012f);
            foreach(int side in new[]{-1,1})s.Box(SteelDark,V(side*.2f,f+.55f,BackZ-.16f),V(.04f,.9f,.04f),.008f,-.12f);
            // the bucket: cushion, side bolsters, backrest (tilted back), headrest wings
            s.Box(Cushion,V(0,CushionTop-.05f,.04f),V(.46f,.1f,.46f),.035f);
            foreach(int side in new[]{-1,1}){s.Box(CushionDark,V(side*.25f,CushionTop+.01f,.04f),V(.08f,.16f,.44f),.03f);s.Box(CushionDark,V(side*.25f,f+.88f,BackZ+.02f),V(.08f,.62f,.16f),.03f,-.1f);}
            s.Push(WeaponSculpt.Rx(-.1f)*WeaponSculpt.T(0,f+.85f,BackZ-.04f));s.Box(Cushion,V(0,0,0),V(.44f,.8f,.1f),.04f);s.Box(Graphite,V(0,0,-.07f),V(.5f,.86f,.05f),.015f);s.Pop();
            s.Box(Cushion,V(0,f+1.36f,BackZ-.1f),V(.3f,.18f,.1f),.04f,-.1f);
            foreach(int side in new[]{-1,1})s.Box(CushionDark,V(side*.17f,f+1.36f,BackZ-.05f),V(.08f,.22f,.14f),.03f,-.1f,side*.25f);
            // the harness bar's pivot housing over the backrest
            s.Box(SteelDark,HarnessPivot+V(0,.02f,-.04f),V(.36f,.1f,.12f),.02f);
            s.Into(Lights);foreach(int side in new[]{-1,1})s.Box(Lamp(0xffffff),V(side*.27f,f+.85f,BackZ+.06f),V(.012f,.6f,.012f),0,-.1f);
            s.Box(Lamp(0xffffff),V(0,CushionTop-.12f,.27f),V(.4f,.012f,.012f));
            return mesh;
        }
        /// <summary>The harness bar (its pivot at the origin; 0: down, its crossbar on the chest; the draw turns it by HarnessAngle):
        /// two padded arms over the shoulders to a padded crossbar with grips, and its lock lamp (bone 1).</summary>
        public static WeaponMesh HarnessModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Harness,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            foreach(int side in new[]{-1,1})
            {
                var path=new[]{V(side*.15f,0,0),V(side*.18f,.02f,.18f),V(side*.2f,-.12f,.36f),V(side*.2f,-.27f,.45f)};
                s.Tube(Steel,path,.022f,10,Vector3.UnitY,1,true);
                s.Tube(Pad,new[]{V(side*.18f,.03f,.12f),V(side*.2f,-.06f,.32f),V(side*.2f,-.2f,.42f)},.045f,10,Vector3.UnitY,1,true);
            }
            s.Tube(Steel,new[]{V(-.2f,-.27f,.45f),V(.2f,-.27f,.45f)},.025f,10,Vector3.UnitY,1,true);
            s.Tube(Pad,new[]{V(-.14f,-.27f,.45f),V(.14f,-.27f,.45f)},.05f,12,Vector3.UnitY,1,true);
            foreach(int side in new[]{-1,1})s.Box(GraphiteDark,V(side*.2f,-.27f,.45f),V(.06f,.09f,.06f),.015f);
            s.Box(SteelDark,V(0,0,0),V(.34f,.06f,.06f),.015f);
            s.Into(Lights);s.Box(Lamp(0xffffff),V(0,-.27f,.505f),V(.08f,.02f,.008f));
            return mesh;
        }

        // ---- the CLAMP ------------------------------------------------------------------------------------------------------------
        /// <summary>The clamps' hinges over a docked pod (pod frame, mirrored in x for the second) and their swing open (radians
        /// about the pod's z at full release).</summary>
        public static readonly Vector3 ClampHinge=new Vector3(.62f,NativePodModels.Height+.12f,0);
        public const float ClampOpen=.75f;
        /// <summary>One docking arm (its hinge at the origin; it hangs down and in to grip the pod's shoulder at the pod's +x side;
        /// the draw mirrors it for the -x side and swings both out at the release) with its tell-tale (bone 1).</summary>
        public static WeaponMesh ClampModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Clamp,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            s.Push(WeaponSculpt.Ry(Pi/2));s.Cylinder(Steel,.07f,-.12f,.12f,12,.01f);s.Pop();
            s.Box(Graphite,V(.12f,-.32f,0),V(.12f,.62f,.2f),.02f,0,0,-.35f);
            s.Box(Graphite,V(.2f,-.78f,0),V(.11f,.5f,.18f),.02f,0,0,.12f);
            s.Box(SteelDark,V(.1f,-1.04f,0),V(.3f,.09f,.22f),.02f,0,0,.5f);
            s.Box(Hazard,V(.18f,-.46f,.101f),V(.08f,.3f,.006f),0,0,0,-.2f);
            s.Into(Lights);s.Box(Lamp(0xffffff),V(.24f,-.62f,.1f),V(.035f,.035f,.012f));
            return mesh;
        }

        // ---- the BELLY (station frame) --------------------------------------------------------------------------------------------
        /// <summary>The belly's underside (station y): the pods hang under it (a shallow pod's roof just below, NativeDeployEject).</summary>
        public const float BellyBottom=-5.85f,BellyTop=-5.2f;
        /// <summary>The station's underside over the pods: its hull plate (x -10..10, z -9..7) in panels on ribs, a docking ring round
        /// every pad's tube, two flood bars and four nav lamps (bone 1; the draw tints them).</summary>
        public static WeaponMesh BellyModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Belly,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            const float x0=-10,x1=10,z0=-9,z1=7;float y=BellyBottom;
            Holed(s,HullDark,V((x0+x1)/2,(BellyTop+y)/2,(z0+z1)/2),V(x1-x0,BellyTop-y,z1-z0),0,BellyHole);
            for(float x=x0+1;x<x1-.5f;x+=2)for(float z=z0+1;z<z1-.5f;z+=2)Holed(s,((int)(x+z)&2)==0?Hull:Panel,V(x,y-.02f,z),V(1.9f,.04f,1.9f),.008f,BellyHole);
            for(float x=x0+2;x<x1-1;x+=4)Holed(s,SteelDark,V(x,y-.08f,(z0+z1)/2),V(.18f,.12f,z1-z0-.4f),.02f,BellyHole);
            for(int i=0;i<NativeDeployPresentation.Pads;i++)
            {
                var p=NativeDeployPresentation.Pad(i);float r=NativeDeployEject.TubeRadius+.14f;
                s.Push(WeaponSculpt.T(p.X,0,p.Z));Column(s,SteelDark,r+.12f,0,0,y-.16f,y-.02f,24);FlatRing(s,Graphite,NativeDeployEject.TubeRadius+.06f,r+.12f,y-.161f,24,true);s.Pop();
            }
            s.Into(Lights);
            foreach(int side in new[]{-1,1})s.Box(Lamp(Cool,.9f),V(side*7.5f,y-.05f,(z0+z1)/2),V(.08f,.04f,z1-z0-2));
            for(int i=0;i<NativeDeployPresentation.Pads;i++){var p=NativeDeployPresentation.Pad(i);s.Push(WeaponSculpt.T(p.X,0,p.Z));FlatRing(s,Lamp(Amber,.8f),NativeDeployEject.TubeRadius+.2f,NativeDeployEject.TubeRadius+.24f,y-.17f,24,true);s.Pop();}
            foreach(var (x,z,hex) in new[]{(x0+.4f,z0+.4f,Danger),(x1-.4f,z0+.4f,Go),(x0+.4f,z1-.4f,Danger),(x1-.4f,z1-.4f,Go)})s.Sphere(Lamp(hex),V(x,y-.1f,z),V(.1f,.1f,.1f),4,10);
            return mesh;
        }

        /// <summary>The square opening the belly leaves round every pad's tube (half its side; the docking ring covers its edge).</summary>
        public const float BellyHole=.5f;
        /// <summary>A box (centre, size; y up) less the square holes (half side `hole`) round every pad, the rest in strips: the
        /// station's floor (NativeDeployModels.StationRoom) and the belly let the drop tubes through.</summary>
        internal static void Holed(WeaponSculpt s,in WeaponPaint p,Vector3 c,Vector3 size,float bevel,float hole)
        {
            float xa=c.X-size.X/2,xb=c.X+size.X/2,za=c.Z-size.Z/2,zb=c.Z+size.Z/2,h=hole;var cuts=new List<(float x0,float x1,float z0,float z1)>();
            for(int i=0;i<NativeDeployPresentation.Pads;i++){var q=NativeDeployPresentation.Pad(i);if(q.X+h>xa&&q.X-h<xb&&q.Z+h>za&&q.Z-h<zb)cuts.Add((Math.Max(xa,q.X-h),Math.Min(xb,q.X+h),Math.Max(za,q.Z-h),Math.Min(zb,q.Z+h)));}
            if(cuts.Count==0){s.Box(p,c,size,bevel);return;}
            var zs=new List<float>{za,zb};foreach(var k in cuts){zs.Add(k.z0);zs.Add(k.z1);}zs.Sort();
            for(int j=0;j+1<zs.Count;j++)
            {
                float a=zs[j],b=zs[j+1];if(b-a<1e-4f)continue;float m=(a+b)/2;var xs=new List<(float x0,float x1)>();
                foreach(var k in cuts)if(k.z0<m&&k.z1>m)xs.Add((k.x0,k.x1));xs.Sort();float x=xa;
                foreach(var (h0,h1) in xs){if(h0>x+1e-4f)s.Box(p,V((x+h0)/2,c.Y,m),V(h0-x,size.Y,b-a));x=Math.Max(x,h1);}
                if(xb>x+1e-4f)s.Box(p,V((x+xb)/2,c.Y,m),V(xb-x,size.Y,b-a));
            }
        }

        // ---- the CITY (the blueprint far below: dark blocks, lit roads, the districts glowing) ---------------------------------------
        private static float Hash(int x,int z,int salt){unchecked{uint h=(uint)(x*73856093)^(uint)(z*19349663)^(uint)(salt*83492791);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/(float)0x1000000;}}
        private static Vector3 Mix(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
        /// <summary>
        /// The rolled city (`s`; its frame: metres x CityScale, +x east, +z north, the plaza at the origin, the ground at y 0): the
        /// ground; a dark block on every non-road cell (height from the cell's hash, tinted a little by its districts); sodium-lit
        /// roads; scattered lit windows; each district's hall at its entrance in the district's colour; the sanctuary's ring and the
        /// boss gate's red beacon. An empty state gives the ground and the ring. Deterministic per state.
        /// </summary>
        public static WeaponMesh CityModel(ExpeditionDeploymentState st)
        {
            var mesh=new WeaponMesh{Spec="deploy-"+City,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            float k=NativeDeployEject.CityScale,cell=NativeDeployPresentation.CellMetres*k,hx=NativeDeployPresentation.WorldHalfX*k,hz=NativeDeployPresentation.WorldHalfZ*k;
            s.Box(Ground,V(0,-.5f,0),V(2*hx+cell,1,2*hz+cell));
            var pal=NativeDeployPresentation.Palette(st);var tint=new Vector3[4];for(int i=0;i<4;i++)tint[i]=WeaponPaint.Linear(pal[i]);
            var windows=new List<(Vector3 at,Vector3 size,Vector3 color)>();var roads=new List<(Vector3 at,Vector3 size)>();
            bool world=st!=null&&st.worldRolled&&st.cells!=null&&st.cells.Length>0;
            for(int x=-NativeDeployPresentation.HalfX;x<=NativeDeployPresentation.HalfX;x++)for(int z=-NativeDeployPresentation.HalfZ;z<=NativeDeployPresentation.HalfZ;z++)
            {
                float cx=x*cell,cz=z*cell;bool plaza=Math.Abs(x)<=2&&Math.Abs(z)<=2;var c=world?NativeDeployPresentation.CellAt(st,x,z):null;
                if(plaza)continue;
                if(c!=null&&c.road){roads.Add((V(cx,.03f,cz),V(cell*.62f,.04f,cell*.62f)));continue;}
                var w=world?NativeDeployPresentation.Weights(st,x*NativeDeployPresentation.CellMetres,z*NativeDeployPresentation.CellMetres):null;
                var col=WeaponPaint.Linear(0x10141d);if(w!=null)for(int d=1;d<4;d++)col=Mix(col,tint[d]*.08f,w[d]*.8f);
                float h=(8+14*Hash(x,z,1))*k,fw=cell*(.7f+.16f*Hash(x,z,2)),fd=cell*(.7f+.16f*Hash(x,z,3));
                var paint=new WeaponPaint(col,Vector3.Zero,0);s.Box(paint,V(cx,h/2,cz),V(fw,h,fd));
                // a few lit windows on its four faces (warm, some cool)
                int n=(int)(Hash(x,z,4)*4);
                for(int i=0;i<n;i++)
                {
                    int face=(int)(Hash(x,z,10+i)*4);float u=Hash(x,z,20+i)-.5f,v=.2f+.7f*Hash(x,z,30+i);var wc=WeaponPaint.Linear(Hash(x,z,40+i)>.75f?Ice:Warm)*(.45f+.5f*Hash(x,z,50+i));
                    var at=face==0?V(cx+fw/2+.01f,h*v,cz+u*fd*.7f):face==1?V(cx-fw/2-.01f,h*v,cz+u*fd*.7f):face==2?V(cx+u*fw*.7f,h*v,cz+fd/2+.01f):V(cx+u*fw*.7f,h*v,cz-fd/2-.01f);
                    var size=face<2?V(.02f,.35f*k*2,.9f*k*2):V(.9f*k*2,.35f*k*2,.02f);windows.Add((at,size,wc));
                }
            }
            // the plaza's floor (a lighter slab) under the sanctuary ring
            s.Box(BlockTop,V(0,.02f,0),V(5*cell,.04f,5*cell));
            s.Into(Lights);
            foreach(var (at,size) in roads)s.Box(Lamp(Sodium,.42f),at,size);
            foreach(var (at,size,color) in windows)s.Box(LampRgb(color),at,size);
            // the sanctuary (the 49.6 m square's fence, lit) and its beacon
            float sq=24.8f*k;foreach(int side in new[]{-1,1}){s.Box(Lamp(Ice,.8f),V(side*sq,.4f,0),V(.25f,.8f,2*sq));s.Box(Lamp(Ice,.8f),V(0,.4f,side*sq),V(2*sq,.8f,.25f));}
            s.Sphere(Lamp(White),V(0,2.5f*k,0),V(1.2f*k,1.2f*k,1.2f*k),4,10);
            if(world)
            {
                for(int d=0;d<3&&d<(st.districts?.Length??0);d++)
                {
                    var dd=st.districts[d];if(dd==null)continue;float ex=dd.x*cell,ez=dd.z*cell;var dc=tint[d+1];
                    s.Box(LampRgb(Vector3.Min(dc,Vector3.One)),V(ex,9*k,ez),V(cell*.8f,18*k,cell*.8f));s.Box(LampRgb(dc*.5f),V(ex,.05f,ez),V(cell*2.4f,.06f,cell*2.4f));
                }
                var b=NativeDeployPresentation.BossAnchor;s.Box(Lamp(Danger),V(b.X*k,12*k,b.Y*k),V(cell*.5f,24*k,cell*.5f));
            }
            return mesh;
        }
    }
}
