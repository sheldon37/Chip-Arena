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
        public const string Tube="eject-tube",Collar="eject-collar",Seat="eject-seat",Harness="eject-harness",Clamp="eject-clamp",Belly="eject-belly",City="eject-city",Cabin="eject-cabin",
            Clouds="eject-clouds",Rain="eject-rain";
        private static readonly WeaponPaint Panel=WeaponPaint.Solid(0x1c2029,.03f),PanelLight=WeaponPaint.Solid(0x2a303b,.04f),Seam=WeaponPaint.Solid(0x07080b),
            Steel=WeaponPaint.Solid(0xa3aab6,.06f),SteelDark=WeaponPaint.Solid(0x4a505c,.04f),Graphite=WeaponPaint.Solid(0x2a2d35,.03f),GraphiteDark=WeaponPaint.Solid(0x15171c,.02f),
            Hazard=WeaponPaint.Solid(0xd9a21f,.05f),Black=WeaponPaint.Solid(0x040506),Cushion=WeaponPaint.Solid(0x2b1416,.02f),CushionDark=WeaponPaint.Solid(0x1a0c0e,.01f),
            Pad=WeaponPaint.Solid(0x1e2128,.02f),Hull=WeaponPaint.Solid(0x1f2430,.03f),HullDark=WeaponPaint.Solid(0x12151c,.02f),Block=WeaponPaint.Solid(0x10141d),
            BlockTop=WeaponPaint.Solid(0x1a2030),Ground=WeaponPaint.Solid(0x080a10),
            // the harness's pads and webbing, and the cabin's lining (its own finish: it reads under any livery)
            Asphalt=WeaponPaint.Solid(0x0b0d12),HarnessPad=WeaponPaint.Solid(0x4b525e,.05f),Webbing=WeaponPaint.Solid(0xd8662a,.04f),Lining=WeaponPaint.Solid(0x5a6170,.06f),LiningDark=WeaponPaint.Solid(0x464c59,.05f),Kick=WeaponPaint.Solid(0x2a2e36,.03f);
        /// <summary>An unlit light: its colour is its emission (NativeSourceLit skips the grade), the draw's tint its level.</summary>
        private static WeaponPaint Lamp(int hex,float k=1){var c=WeaponPaint.Linear(hex)*k;return new WeaponPaint(c,c,0);}
        private static WeaponPaint LampRgb(Vector3 c){return new WeaponPaint(c,c,0);}
        private const int Ice=0xd4f2ff,Cool=0x7fd6ff,Amber=0xff9a2e,Danger=0xff3a3a,Warm=0xffb45a,Sodium=0xffc27a,White=0xf4f8ff,Go=0x6dffa8;
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Up=-(float)(Math.PI/2),Pi=(float)Math.PI;
        // A vertical cylinder (axis +y) of radius r from y0 to y1 at (x, z), outward or seen from inside.
        private static void Column(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides,bool inward=false,bool caps=true)
        {
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));var p=c;bool cap=caps&&!inward;
            s.Loft(new[]{WeaponSculpt.Circle(sides,r,r,y0),WeaponSculpt.Circle(sides,r,r,y1)},(i,k)=>p,0,cap,cap,null,false,inward);s.Pop();
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
            // the outer skin (seen under the belly for a deep pod's tube; a sleeve, open at both ends, so the tube shot looks down
            // into it) and its rim under the floor
            Column(s,GraphiteDark,r+.06f,0,0,-length,0,sides,false,false);FlatRing(s,Graphite,r,r+.07f,-.005f,sides);
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
            // open walls (no cap across the bore: a pilot falls through the rim into the cabin's dark disc, not into a lid)
            Column(s,SteelDark,r+.16f,0,0,-.02f,.24f,24,false,false);FlatRing(s,SteelDark,r+.1f,r+.16f,.241f,24);Column(s,Graphite,r+.1f,0,0,.24f,.34f,24,false,false);
            Column(s,Steel,r+.02f,0,0,.18f,.34f,24,true);
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
        /// <summary>The crossbar's centre from the pivot when the bar is down (in front of the seated pilot's stomach).</summary>
        public static readonly Vector3 Bar=new Vector3(0,-.46f,.46f);
        /// <summary>The harness bar's turn about x (radians) from 0 (down on the chest) to raised (Raised, up over the headrest).</summary>
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
        /// two padded arms over the shoulders to a padded crossbar with grips, an orange webbing strap down each pad to a steel
        /// buckle on the crossbar (so the bar reads under the cabin's red, not as two dark slabs), and its lock lamp (bone 1).</summary>
        public static WeaponMesh HarnessModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Harness,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            // over the shoulders (the pads) and down to the crossbar in front of the stomach, where the hands take its grips
            float by=Bar.Y,bz=Bar.Z;
            foreach(int side in new[]{-1,1})
            {
                var path=new[]{V(side*.15f,0,0),V(side*.19f,.03f,.2f),V(side*.21f,-.18f,.4f),V(side*.2f,by,bz)};
                s.Tube(Steel,path,.022f,10,Vector3.UnitY,1,true);
                s.Tube(HarnessPad,new[]{V(side*.18f,.04f,.14f),V(side*.2f,-.04f,.33f),V(side*.21f,-.22f,.42f)},.05f,10,Vector3.UnitY,1,true);
                // the webbing over the pad's front face and its stitched edges, down to the buckle on the crossbar
                s.Tube(Webbing,new[]{V(side*.165f,.075f,.15f),V(side*.17f,.005f,.34f),V(side*.175f,-.2f,.45f),V(side*.16f,by+.03f,bz+.03f)},.018f,8,Vector3.UnitY,1,true);
                s.Box(Steel,V(side*.16f,by+.02f,bz+.045f),V(.07f,.06f,.02f),.008f);s.Box(SteelDark,V(side*.16f,by+.02f,bz+.057f),V(.04f,.03f,.006f),.004f);
            }
            s.Tube(Steel,new[]{V(-.2f,by,bz),V(.2f,by,bz)},.025f,10,Vector3.UnitY,1,true);
            s.Tube(HarnessPad,new[]{V(-.1f,by,bz),V(.1f,by,bz)},.05f,12,Vector3.UnitY,1,true);
            foreach(int side in new[]{-1,1})s.Box(Hazard,V(side*.17f,by,bz),V(.07f,.1f,.07f),.015f);
            s.Box(SteelDark,V(0,0,0),V(.34f,.06f,.06f),.015f);
            // the centre buckle where the two straps meet, and its lock lamp
            s.Box(Steel,V(0,by+.01f,bz+.05f),V(.1f,.075f,.025f),.01f);
            s.Into(Lights);s.Box(Lamp(0xffffff),V(0,by,bz+.064f),V(.06f,.018f,.008f));
            return mesh;
        }

        // ---- the CABIN (pod frame: base at the origin, doorway toward +z) ---------------------------------------------------------
        /// <summary>The cabin's lining's apothem (inside the hull's panels, NativePodModels) and its height.</summary>
        public const float CabinApothem=.6f,CabinTop=1.98f;
        /// <summary>
        /// The cabin's lining, drawn in my own pod in the seat shot (the hub's pod model is its outside): seven lined wall panels
        /// round the octagon (the eighth is the doorway) with seams, rivets and grab rails; a red lamp strip over the seat (bone 1,
        /// the cabin's light), the porthole on the right-hand wall with its glass (bone 2, the night outside), status lamps (bone
        /// 3), a floor grille and the ceiling round the roof hatch. Its own neutral finish, so the cabin reads under any livery.
        /// </summary>
        public static WeaponMesh CabinModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Cabin,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);float f=NativePodModels.Floor,a0=CabinApothem;
            float w=2*a0*(float)Math.Tan(Pi/8)-.02f,h=CabinTop-f,mid=f+h/2;
            for(int i=1;i<8;i++)
            {
                float a=i*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(a0*(float)Math.Sin(a),0,a0*(float)Math.Cos(a)));
                // the lining (its face toward the cabin's middle: local -z), a darker kick plate, the seams and the rivets
                s.Box(i%2==0?Lining:LiningDark,V(0,mid,.015f),V(w,h,.03f));s.Box(Kick,V(0,f+.12f,-.004f),V(w-.02f,.22f,.012f));
                s.Box(Seam,V(0,f+1.08f,-.002f),V(w-.04f,.012f,.004f));s.Box(Seam,V(0,f+.5f,-.002f),V(w-.04f,.008f,.004f));
                foreach(float x in new[]{-w/2+.04f,w/2-.04f})foreach(float y in new[]{f+.3f,f+.9f,f+1.4f,h+f-.08f})s.Box(SteelDark,V(x,y,-.004f),V(.018f,.018f,.008f));
                s.Pop();
            }
            // grab rails each side of the doorway, inside the posts
            foreach(int side in new[]{-1,1})s.Tube(Hazard,new[]{V(side*.27f,f+.75f,.5f),V(side*.3f,f+.8f,.45f),V(side*.3f,f+1.45f,.45f),V(side*.27f,f+1.5f,.5f)},.016f,8,Vector3.UnitY,1,true);
            // the floor grille and the ceiling, its ring round the roof hatch's opening
            for(int k=-3;k<=3;k++)s.Box(SteelDark,V(0,f+.006f,k*.14f),V(1.0f,.012f,.03f));
            FlatRing(s,LiningDark,NativeDeployEject.TubeRadius+.02f,a0/(float)Math.Cos(Pi/8),CabinTop,16,true);
            // the instrument panels on the two back-quarter walls
            foreach(int side in new[]{-1,1})
            {
                float a=Pi+side*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T((a0-.02f)*(float)Math.Sin(a),0,(a0-.02f)*(float)Math.Cos(a)));
                s.Box(GraphiteDark,V(0,f+1.25f,-.01f),V(.3f,.22f,.03f),.01f);s.Box(Black,V(0,f+1.26f,-.027f),V(.24f,.14f,.004f));s.Pop();
            }
            // the porthole on the right-hand wall (+x: the screen's left from the doorway), its steel ring and bolts
            {
                float a=Pi/2;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T((a0-.005f)*(float)Math.Sin(a),0,0));
                s.Push(WeaponSculpt.Rx(0)*WeaponSculpt.T(0,f+1.3f,0));s.Push(WeaponSculpt.Rx(Up));
                FlatRing(s,Steel,.13f,.18f,.03f,20);FlatRing(s,SteelDark,.11f,.13f,.025f,20);s.Pop();
                for(int k=0;k<8;k++){float b=k*Pi/4;s.Box(Steel,V(.155f*(float)Math.Cos(b),.155f*(float)Math.Sin(b),-.035f),V(.018f,.018f,.012f));}
                s.Pop();s.Pop();
            }
            // the lamps: one down each back-quarter wall (bone 1; the pod's own cabin lamp is over the seat), the porthole's glass
            // (bone 2), the status lamps (bone 3)
            s.Into(Lights);
            foreach(int side in new[]{-1,1}){float a=Pi+side*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T((a0-.03f)*(float)Math.Sin(a),0,(a0-.03f)*(float)Math.Cos(a)));s.Box(Lamp(0xffffff),V(side*.2f,f+.95f,-.012f),V(.02f,.9f,.02f));s.Pop();}
            s.Into(Lights+1);{float a=Pi/2;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(a0-.01f,f+1.3f,0));s.Push(WeaponSculpt.Rx(Up));Disc(s,Lamp(0xffffff),.112f,.02f,20);s.Pop();s.Pop();}
            s.Into(Lights+2);
            foreach(int side in new[]{-1,1}){float a=Pi+side*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T((a0-.02f)*(float)Math.Sin(a),0,(a0-.02f)*(float)Math.Cos(a)));
                for(int k=0;k<4;k++)s.Box(Lamp(k==1?0xff3a3a:k==3?0xffb02e:0x6dffa8),V(-.09f+k*.06f,f+1.1f,-.03f),V(.025f,.018f,.006f));s.Pop();}
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
                // the docking ring: an open collar (its wall and its lip under the tube's skin), never a cap across the bore that
                // a deep pod's pilot and the tube shot's eye would fall through
                s.Push(WeaponSculpt.T(p.X,0,p.Z));Column(s,SteelDark,r+.12f,0,0,y-.16f,y-.02f,24,false,false);FlatRing(s,Graphite,NativeDeployEject.TubeRadius+.06f,r+.12f,y-.161f,24,true);s.Pop();
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
        /// The rolled city (`s`; its frame: metres x CityScale, +x east, +z north, the plaza at the origin, the ground at y 0), lit
        /// as a city seen at night from altitude (owner 2026-11-18: "more realistic"): the ground; dark asphalt streets on the road
        /// cells with rows of sodium lamps along them and a soft sodium haze over each (a continuous lit street, not tiles); on every
        /// other cell a massed block (a podium, often a setback tower, the tallest a crown and a red air light), tinted a little by
        /// its districts, its faces speckled with lit windows (warm, some cool); each district's hall at its entrance in its colour
        /// under a broad glow; the sanctuary's ring and the boss gate's red beacon. Glows are the Screen section (the stage draws the
        /// city with the glow texture, additive). An empty state gives the ground and the ring. Deterministic per state.
        /// </summary>
        public static WeaponMesh CityModel(ExpeditionDeploymentState st)
        {
            var mesh=new WeaponMesh{Spec="deploy-"+City,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);var glow=mesh.Section(NativeDeployModels.Screen,WeaponStretch.None);
            float k=NativeDeployEject.CityScale,cell=NativeDeployPresentation.CellMetres*k,hx=NativeDeployPresentation.WorldHalfX*k,hz=NativeDeployPresentation.WorldHalfZ*k;
            s.Box(Ground,V(0,-.5f,0),V(2*hx+cell,1,2*hz+cell));
            var pal=NativeDeployPresentation.Palette(st);var tint=new Vector3[4];for(int i=0;i<4;i++)tint[i]=WeaponPaint.Linear(pal[i]);
            bool world=st!=null&&st.worldRolled&&st.cells!=null&&st.cells.Length>0;
            int X=NativeDeployPresentation.HalfX,Z=NativeDeployPresentation.HalfZ;
            bool Road(int x,int z)=>world&&Math.Abs(x)<=X&&Math.Abs(z)<=Z&&NativeDeployPresentation.CellAt(st,x,z)?.road==true;
            bool Plaza(int x,int z)=>Math.Abs(x)<=2&&Math.Abs(z)<=2;
            var lamps=new List<Vector3>();var windows=new List<(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 color)>();var beacons=new List<Vector3>();
            var sodium=WeaponPaint.Linear(Sodium);
            for(int x=-X;x<=X;x++)for(int z=-Z;z<=Z;z++)
            {
                float cx=x*cell,cz=z*cell;if(Plaza(x,z))continue;
                if(Road(x,z))
                {
                    // the street: asphalt to the cell's edges (joining its neighbours), lamps along each kerb that faces a block
                    s.Box(Asphalt,V(cx,.02f,cz),V(cell,.04f,cell));
                    for(int e=0;e<4;e++)
                    {
                        int nx=x+(e==0?1:e==1?-1:0),nz=z+(e==2?1:e==3?-1:0);if(Road(nx,nz)||Plaza(nx,nz))continue;
                        for(int j=0;j<2;j++){float u=(j+.5f)/2-.5f;var at=e<2?V(cx+(e==0?1:-1)*cell*.4f,.1f,cz+u*cell):V(cx+u*cell,.1f,cz+(e==2?1:-1)*cell*.4f);lamps.Add(at);}
                    }
                    GlowQuad(glow,V(cx,.3f,cz),V(cell*.75f,0,0),V(0,0,cell*.75f),new Vector4(sodium*.32f,1));
                    continue;
                }
                var w=world?NativeDeployPresentation.Weights(st,x*NativeDeployPresentation.CellMetres,z*NativeDeployPresentation.CellMetres):null;
                var col=WeaponPaint.Linear(0x10141d);float near=0;if(w!=null){for(int d=1;d<4;d++){col=Mix(col,tint[d]*.08f,w[d]*.8f);near=Math.Max(near,w[d]);}}
                // the massing: a podium filling most of the cell; often a setback tower on it (taller toward the districts); the
                // tallest crowned and lit red on top
                float fw=cell*(.8f+.12f*Hash(x,z,2)),fd=cell*(.8f+.12f*Hash(x,z,3)),hp=(3.5f+4*Hash(x,z,1))*k;
                var body=new WeaponPaint(col,Vector3.Zero,0);var roof=new WeaponPaint(col*1.35f,Vector3.Zero,0);
                s.Box(body,V(cx,hp/2,cz),V(fw,hp,fd));Speckle(windows,x,z,0,V(cx,0,cz),fw,fd,0,hp,k);
                if(Hash(x,z,5)<.42f+.35f*near)
                {
                    float tw=fw*(.48f+.22f*Hash(x,z,6)),td=fd*(.48f+.22f*Hash(x,z,7)),ht=(9+22*Hash(x,z,8)*Hash(x,z,9)+14*near)*k;
                    float ox=(Hash(x,z,11)-.5f)*(fw-tw)*.8f,oz=(Hash(x,z,12)-.5f)*(fd-td)*.8f;
                    s.Box(body,V(cx+ox,hp+ht/2,cz+oz),V(tw,ht,td));Speckle(windows,x,z,1,V(cx+ox,0,cz+oz),tw,td,hp,hp+ht,k);
                    if(ht>20*k){float cw=tw*.6f,cd=td*.6f,hc=(3+4*Hash(x,z,13))*k;s.Box(roof,V(cx+ox,hp+ht+hc/2,cz+oz),V(cw,hc,cd));beacons.Add(V(cx+ox,hp+ht+hc+.15f,cz+oz));}
                    else s.Box(roof,V(cx+ox,hp+ht+.2f*k,cz+oz),V(tw*.4f,.4f*k,td*.35f));
                }
                else s.Box(roof,V(cx+(Hash(x,z,14)-.5f)*fw*.4f,hp+.3f*k,cz),V(fw*.3f,.6f*k,fd*.25f));
            }
            // the plaza's floor (a lighter slab) under the sanctuary ring
            s.Box(BlockTop,V(0,.02f,0),V(5*cell,.04f,5*cell));
            s.Into(Lights);
            foreach(var at in lamps)s.Quad(Lamp(Sodium,1.6f),at+V(-.16f,0,-.16f),at+V(-.16f,0,.16f),at+V(.16f,0,.16f),at+V(.16f,0,-.16f));
            foreach(var (a,b,c,d,color) in windows)s.Quad(LampRgb(color),a,b,c,d);
            foreach(var at in beacons)s.Box(Lamp(Danger,1.4f),at,V(.3f,.3f,.3f));
            // the sanctuary (the 49.6 m square's fence, lit) and its beacon
            float sq=24.8f*k;foreach(int side in new[]{-1,1}){s.Box(Lamp(Ice,.8f),V(side*sq,.4f,0),V(.25f,.8f,2*sq));s.Box(Lamp(Ice,.8f),V(0,.4f,side*sq),V(2*sq,.8f,.25f));}
            s.Sphere(Lamp(White),V(0,2.5f*k,0),V(1.2f*k,1.2f*k,1.2f*k),4,10);
            GlowQuad(glow,V(0,1,0),V(sq*1.6f,0,0),V(0,0,sq*1.6f),new Vector4(WeaponPaint.Linear(Ice)*.22f,1));
            if(world)
            {
                for(int d=0;d<3&&d<(st.districts?.Length??0);d++)
                {
                    var dd=st.districts[d];if(dd==null)continue;float ex=dd.x*cell,ez=dd.z*cell;var dc=tint[d+1];
                    s.Box(LampRgb(Vector3.Min(dc,Vector3.One)),V(ex,9*k,ez),V(cell*.8f,18*k,cell*.8f));s.Box(LampRgb(dc*.5f),V(ex,.05f,ez),V(cell*2.4f,.06f,cell*2.4f));
                    // the district's glow over its streets (a warm bloom a few blocks across, in its colour)
                    GlowQuad(glow,V(ex,6*k,ez),V(cell*5.5f,0,0),V(0,0,cell*5.5f),new Vector4(Vector3.Min(dc,Vector3.One)*.5f,1));
                }
                var b=NativeDeployPresentation.BossAnchor;s.Box(Lamp(Danger),V(b.X*k,12*k,b.Y*k),V(cell*.5f,24*k,cell*.5f));
                GlowQuad(glow,V(b.X*k,8*k,b.Y*k),V(cell*2.2f,0,0),V(0,0,cell*2.2f),new Vector4(WeaponPaint.Linear(Danger)*.35f,1));
            }
            return mesh;
        }
        // The lit windows on a block's four faces between heights y0 and y1 (`part` 0 the podium, 1 the tower): a grid of window
        // slots, a hashed few lit (most warm, some cool), each a quad just off the face.
        private static void Speckle(List<(Vector3,Vector3,Vector3,Vector3,Vector3)> o,int x,int z,int part,Vector3 c,float w,float d,float y0,float y1,float k)
        {
            float pitchX=1.7f*k,pitchY=2.3f*k,ww=.75f*k,wh=1.0f*k;int rows=Math.Max(1,(int)((y1-y0-.6f*k)/pitchY));
            for(int face=0;face<4;face++)
            {
                float span=face<2?d:w;int cols=Math.Max(1,(int)(span/pitchX));
                for(int r=0;r<rows;r++)for(int q=0;q<cols;q++)
                {
                    int salt=100+part*4000+face*1000+r*31+q;if(Hash(x,z,salt)>.2f)continue;
                    float u=(q+.5f)/cols-.5f,y=y0+.5f*k+(r+.5f)*pitchY;var wc=WeaponPaint.Linear(Hash(x,z,salt+7)>.78f?Ice:Warm)*(.35f+.55f*Hash(x,z,salt+13));
                    float off=.03f;Vector3 t;var at=c;
                    // (t runs to the face's right seen from outside, so each quad's front faces out: the sculpt's quad convention)
                    switch(face){case 0:t=-Vector3.UnitZ;at+=V(w/2+off,y,u*d);break;case 1:t=Vector3.UnitZ;at+=V(-w/2-off,y,u*d);break;
                        case 2:t=Vector3.UnitX;at+=V(u*w,y,d/2+off);break;default:t=-Vector3.UnitX;at+=V(u*w,y,-d/2-off);break;}
                    var hw=t*(ww/2);var hh=V(0,wh/2,0);o.Add((at-hw-hh,at+hw-hh,at+hw+hh,at-hw+hh,wc));
                }
            }
        }
        /// <summary>A soft glow quad (centre, half axes) written into a Screen section (the glow materials draw both faces) with its
        /// uv over the glow texture and a colour (rgb, alpha) the draw multiplies by its tint.</summary>
        private static void GlowQuad(WeaponSection g,Vector3 c,Vector3 ax,Vector3 ay,Vector4 color)
        {
            var n=Vector3.Cross(ay,ax);n=n.LengthSquared()>1e-12f?Vector3.Normalize(n):Vector3.UnitY;int i=g.Positions.Count;
            for(int j=0;j<4;j++)
            {
                float u=j==1||j==2?1:0,v=j>=2?1:0;g.Positions.Add(c+ax*(2*u-1)+ay*(2*v-1));g.Normals.Add(n);g.Colors.Add(color);g.Emission.Add(Vector4.Zero);g.Data.Add(new Vector2(u,v));
            }
            g.Indices.Add(i);g.Indices.Add(i+1);g.Indices.Add(i+2);g.Indices.Add(i);g.Indices.Add(i+2);g.Indices.Add(i+3);
        }

        // ---- the sky between the station and the city: the cloud deck and the rain ----------------------------------------------
        /// <summary>The cloud deck's level (station frame: under the pods, over the city; the release's rush plunges into it) and
        /// its reach round the station.</summary>
        public const float DeckY=-38f,DeckReach=110f;
        /// <summary>
        /// The broken cloud deck under the station (station frame; Screen section: the stage draws it with the glow texture,
        /// alpha-blended): soft moonlit puffs in a jittered grid with wide breaks the city shows through, each over a lower puff
        /// warmed by the city's light, and a few thin wisps higher up; deterministic.
        /// </summary>
        public static WeaponMesh CloudModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Clouds,Tier=0};var g=mesh.Section(NativeDeployModels.Screen,WeaponStretch.None);
            const float pitch=17f;int n=(int)(DeckReach/pitch);var moon=new Vector3(.36f,.42f,.56f);var warm=new Vector3(.5f,.36f,.26f);
            for(int i=-n;i<=n;i++)for(int j=-n;j<=n;j++)
            {
                float x=i*pitch+(Hash(i,j,201)-.5f)*pitch*.8f,z=j*pitch+(Hash(i,j,202)-.5f)*pitch*.8f,r=(float)Math.Sqrt(x*x+z*z);
                if(r>DeckReach)continue;
                // the breaks: a broad low-frequency field leaves the deck about half open
                float field=.5f*Hash((i+40)/3,(j+40)/3,203)+.5f*Hash((i+41)/2,(j+41)/2,204);if(field<.46f)continue;
                float size=9+10*Hash(i,j,205),y=DeckY+(Hash(i,j,206)-.5f)*4,a=(.5f+.35f*Hash(i,j,207))*Math.Min(1,(DeckReach-r)/25);
                GlowQuad(g,V(x,y-1.2f,z),V(size*1.1f,0,0),V(0,0,size),new Vector4(warm,a*.7f));
                GlowQuad(g,V(x,y,z),V(size,0,0),V(0,0,size*1.15f),new Vector4(moon,a));
            }
            for(int k=0;k<14;k++){float x=(Hash(k,0,210)-.5f)*120,z=(Hash(k,1,210)-.5f)*120;GlowQuad(g,V(x,DeckY+14+6*Hash(k,2,210),z),V(14+10*Hash(k,3,210),0,0),V(0,0,5+4*Hash(k,4,210)),new Vector4(moon*.9f,.22f));}
            return mesh;
        }
        /// <summary>The rain's volume (a box RainWidth across and RainHeight tall about its origin) and its streaks' length.</summary>
        public const float RainWidth=14f,RainHeight=20f,RainStreak=1.4f;
        /// <summary>The rain round the falling pod (Screen section, additive, the glow texture stretched into streaks): thin vertical
        /// streaks hashed through a box the stage stacks under and over the camera and scrolls up past it as the pod falls.</summary>
        public static WeaponMesh RainModel()
        {
            var mesh=new WeaponMesh{Spec="deploy-"+Rain,Tier=0};var g=mesh.Section(NativeDeployModels.Screen,WeaponStretch.None);
            for(int k=0;k<140;k++)
            {
                float x=(Hash(k,0,220)-.5f)*RainWidth,z=(Hash(k,1,220)-.5f)*RainWidth,y=(Hash(k,2,220)-.5f)*RainHeight;if(x*x+z*z<1.2f)continue;
                float a=.35f+.65f*Hash(k,3,220);var c=new Vector4(.55f*a,.62f*a,.75f*a,1);
                // two crossed thin quads, so a streak reads from any side and from above
                GlowQuad(g,V(x,y,z),V(.012f,0,0),V(0,RainStreak/2,0),c);GlowQuad(g,V(x,y,z),V(0,0,.012f),V(0,RainStreak/2,0),c);
            }
            return mesh;
        }
    }
}
