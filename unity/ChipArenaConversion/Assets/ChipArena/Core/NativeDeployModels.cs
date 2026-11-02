using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition Deployment Station's set and machines (docs/EXPEDITION-DEPLOYMENT.md "Presentation"), sculpted with the
    // Standard weapons' kit (NativeWeaponSculpt) like the PvE machines (NativeMachineModels). Presentation only and engine-free:
    // the checks build what the player builds. Each model is authored in its own floor-origin frame in the station's axes (x
    // right, y up, z away from the default camera: Unity's frame, uploaded as it is) and faces -z; Runtime/NativeDeployScene.cs
    // places them at NativeDeployPresentation's layout (Root). Section bones:
    //   0 the lit body; 1 the lights the draw scales (power, flicker, dim); 2.. extra lights the draw tints on their own (the
    //   approach strip's districts, the polarity rings, SKIP, OPTIONAL, the DEPLOY sign and button); -2 see-through glass
    //   (NativeSourceTransparent); -3 screen faces, uv in WeaponSection.Data (Runtime/NativeDeployScreen.shader: the reel and
    //   card strips, the holo map, the stamps).
    // Lights are "unlit" paints (colour equal to emission, NativeSourceLit's Unlit test) so the stage's ACES grade leaves them
    // raw and the draw's HDR tint makes them bloom; their hue is baked, the tint is a level.
    // Footprints (model space, metres; Footprint returns each): instance machine x -1.42..1.07 (the lever on -x reaches -1.42),
    // z -.79...81, y 0..3.36; mutator machine x -1.07..1.07, z -.79...79, y 0..2.63; war-table radius 1.62, y 0..1 (the holo map
    // floats at 1.2, its light to 1.24); pad radius .58, y 0...1; gate x -3.36..3.36, z -.76..4.21 (the lift bay behind),
    // y -.05..5.6; console x -.95...95, z -.45...45, y 0..1.32; the room x -11.7..11.7, z -12.6..10.3, y -.07..7.81.
    public static class NativeDeployModels
    {
        public const int Body=0,Lights=1,Glass=-2,Screen=-3;
        /// <summary>The instance machine's approach strip (bones Approach + i, district i's colour) and its marquee's two
        /// alternating bulb banks.</summary>
        public const int Approach=2,Bulbs=5,BulbsAlt=6;
        /// <summary>The mutator machine's polarity rings (Ring + i, dial i's polarity), SKIP plate and OPTIONAL sign.</summary>
        public const int Ring=2,Skip=5,Optional=6;
        /// <summary>The gate's DEPLOY sign and the console's DEPLOY button.</summary>
        public const int Sign=2,Button=2;
        /// <summary>The room's table ring (cyan) and the walkway to the gate.</summary>
        public const int FloorRing=2,Walkway=3;
        public const string Instance="instance",Mutator="mutator",Table="table",Pad="pad",Gate="gate",Console="console",Room="room";

        // ---- the machines' moving parts and screens (model space) ----
        /// <summary>The reel drums: radius, width and symbols round the circumference (square symbols: 2 pi R = Slots x Width).</summary>
        public const float DrumRadius=.318f,DrumWidth=.4f;public const int DrumSlots=5;
        /// <summary>Reel i's axle (x axis) in the instance machine: left to right reels 0, 1, 2 (EAST, WEST, SOUTH).</summary>
        public static Vector3 ReelAt(int i)=>new Vector3(-.62f+.62f*i,1.72f,-.112f);
        public static readonly string[] EntranceLabels={"EAST","WEST","SOUTH"};
        /// <summary>The lever's hub (its axis is x): 0 up and leaning back, pulled forward and down.</summary>
        public static readonly Vector3 LeverPivot=new Vector3(-1.2f,1.42f,.02f);
        public const float LeverRest=.2f,LeverThrow=1.3f;
        /// <summary>The lever's turn about x (radians; Unity Euler x, System.Numerics CreateRotationX) at pull 0..1.</summary>
        public static float LeverAngle(float pull)=>LeverRest-LeverThrow*Math.Max(0,Math.Min(1,pull));
        /// <summary>Dial i's spindle (y axis) and card centre in the mutator machine.</summary>
        public static Vector3 CardAt(int i)=>new Vector3(-.6f+.6f*i,1.37f,.16f);
        public const float CardWidth=.32f,CardHeight=.44f,CardArt=.72f;
        /// <summary>The card's turn about its spindle (radians): half a turn per symbol, so a card faces front on every symbol
        /// and the face it shows changes while it is edge on (Card).</summary>
        public static float CardTurn(float offset)=>(float)(offset*Math.PI);
        /// <summary>The symbol a card shows at strip position `offset` (the nearest whole symbol, wrapped).</summary>
        public static int Card(float offset,int strip){int n=Math.Max(1,strip);int k=(int)Math.Floor(offset+.5f);return ((k%n)+n)%n;}
        /// <summary>Where the PENDING / ACTIVE stamp sits over card i (in front of its face, turned a little).</summary>
        public static Vector3 StampAt(int i)=>CardAt(i)+new Vector3(.01f,.03f,-.03f);
        public const float StampWidth=.3f,StampHeight=.11f,StampTilt=.21f;
        /// <summary>The holo map's plane over the war-table (local to the table): centre and size (the blueprint's 25 x 19 cells).</summary>
        public static readonly Vector3 MapAt=new Vector3(0,1.2f,0);public const float MapWidth=2.2f,MapDepth=MapWidth*19f/25f;
        /// <summary>A blueprint point (metres, NativeDeployPresentation.Weights' frame: +x east, +z north) on the holo map (table local).</summary>
        public static Vector3 MapPoint(float wx,float wz)=>MapAt+new Vector3(wx/NativeDeployPresentation.WorldHalfX*MapWidth*.5f,0,wz/NativeDeployPresentation.WorldHalfZ*MapDepth*.5f);
        /// <summary>The blast doors (each leaf slides out by DoorTravel x Gate) and the lift (rises LiftTravel x Lift), gate local.</summary>
        public const float DoorTravel=2.5f,LiftTravel=4.6f;
        public static readonly Vector3 LiftAt=new Vector3(0,0,2f);
        /// <summary>The console in front of the gate (station frame) and its eight readiness lamps (console local).</summary>
        public static readonly Vector3 ConsoleAt=NativeDeployPresentation.Gate+new Vector3(0,0,-2.3f);
        public static Vector3 ConsoleLampAt(int i)=>new Vector3(-.49f+.14f*i,.8f,-.32f);
        /// <summary>The gate header's readiness lamps (gate local), one per pad.</summary>
        public static Vector3 GateLampAt(int i)=>new Vector3(-1.75f+.5f*i,4.72f,-.72f);
        /// <summary>A placed model's transform (System.Numerics row vectors): turned by `yaw` degrees (NativeDeployPresentation's
        /// machine yaws: positive turns the front, -z, toward +x) and moved to `at`.</summary>
        public static Matrix4x4 Root(Vector3 at,float yaw)=>Matrix4x4.CreateRotationY(-yaw*(float)Math.PI/180)*Matrix4x4.CreateTranslation(at);
        /// <summary>A model's front direction in the station for a yaw (degrees).</summary>
        public static Vector3 Facing(float yaw)=>Vector3.TransformNormal(-Vector3.UnitZ,Matrix4x4.CreateRotationY(-yaw*(float)Math.PI/180));
        public static void Footprint(string model,out Vector3 min,out Vector3 max)
        {
            switch(model)
            {
                case Instance:min=new Vector3(-1.42f,0,-.79f);max=new Vector3(1.07f,3.36f,.81f);return;
                case Mutator:min=new Vector3(-1.07f,0,-.79f);max=new Vector3(1.07f,2.63f,.79f);return;
                case Table:min=new Vector3(-1.62f,0,-1.62f);max=new Vector3(1.62f,1.0f,1.62f);return;
                case Pad:min=new Vector3(-.58f,0,-.58f);max=new Vector3(.58f,.1f,.58f);return;
                case Gate:min=new Vector3(-3.36f,-.05f,-.76f);max=new Vector3(3.36f,5.6f,4.21f);return;
                case Console:min=new Vector3(-.95f,0,-.45f);max=new Vector3(.95f,1.32f,.45f);return;
                default:min=new Vector3(-11.7f,-.07f,-12.6f);max=new Vector3(11.7f,7.81f,10.3f);return;
            }
        }

        // ---- paints ----
        private static readonly WeaponPaint Graphite=WeaponPaint.Solid(0x2a2d35,.03f),GraphiteLight=WeaponPaint.Solid(0x3d414c,.04f),GraphiteDark=WeaponPaint.Solid(0x17191e,.02f),
            Brass=WeaponPaint.Solid(0xc0913f,.1f),BrassDark=WeaponPaint.Solid(0x7d5b27,.06f),BrassBright=WeaponPaint.Solid(0xe8c070,.14f),
            Steel=WeaponPaint.Solid(0xa3aab6,.06f),SteelDark=WeaponPaint.Solid(0x5c6270,.04f),Black=WeaponPaint.Solid(0x07080b),Rubber=WeaponPaint.Solid(0x101014),
            Enamel=WeaponPaint.Solid(0x303a4b,.03f),EnamelLight=WeaponPaint.Solid(0x404d60,.04f),Plate=WeaponPaint.Solid(0x313843,.02f),PlateDark=WeaponPaint.Solid(0x262b34,.02f),
            Seam=WeaponPaint.Solid(0x08090c),Wall=WeaponPaint.Solid(0x2a303b,.02f),WallLight=WeaponPaint.Solid(0x3a414e,.03f),Hazard=WeaponPaint.Solid(0xd9a21f,.05f),
            Red=WeaponPaint.Solid(0xb3261e,.08f),Chrome=WeaponPaint.Solid(0xc6ccd6,.12f);
        /// <summary>An unlit light: its colour is its emission (NativeSourceLit skips the grade), the draw's tint its level.</summary>
        private static WeaponPaint Lamp(int hex,float k=1){var c=WeaponPaint.Linear(hex)*k;return new WeaponPaint(c,c,0);}
        private const int Warm=0xffb45a,Ivory=0xffe2b0,Amber=0xff9a2e,Cool=0x7fd6ff,Ice=0xd4f2ff,Violet=0xb88cff,White=0xf4f8ff,Danger=0xff3a3a,Go=0x6dffa8;
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Up=-(float)(Math.PI/2),Pi=(float)Math.PI;

        // ---- shared pieces ----
        private static void Panel(WeaponSculpt s,in WeaponPaint p,float w,float h,float corner,float cx,float cy,float z0,float z1,float bevel)
        {var q=p;s.Slab(_=>q,q,q,WeaponSculpt.Rect(w,h,corner,0,cx,cy),z0,z1,bevel);}
        // A vertical cylinder (axis +y) of radius r from y0 to y1 at (x, z).
        private static void Column(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides,float bevel=0)
        {s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));s.Cylinder(c,r,y0,y1,sides,bevel);s.Pop();}
        // A cylinder along +x from x0 to x1 through (y, z).
        private static void Axle(WeaponSculpt s,in WeaponPaint c,float r,float y,float z,float x0,float x1,int sides,float bevel=0)
        {s.Push(WeaponSculpt.Ry(Pi/2)*WeaponSculpt.T(0,y,z));s.Cylinder(c,r,x0,x1,sides,bevel);s.Pop();}
        // A flat ring (annulus) facing +z at depth z.
        private static void Annulus(WeaponSculpt s,in WeaponPaint c,float r0,float r1,float z,int segments,float cx=0,float cy=0)
        {
            var a=WeaponSculpt.Circle(segments,r0,r0,z,0,cx,cy);var b=WeaponSculpt.Circle(segments,r1,r1,z,0,cx,cy);
            for(int i=0;i<segments;i++){int j=(i+1)%segments;s.Quad(c,a[i],b[i],b[j],a[j]);}
        }
        // A flat ring lying on the floor (facing +y) at height y about (x, z).
        private static void FlatRing(WeaponSculpt s,in WeaponPaint c,float r0,float r1,float y,int segments,float x=0,float z=0)
        {s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,y,z));Annulus(s,c,r0,r1,0,segments);s.Pop();}
        // Lettering (NativeDeployArt's chamfered monoline capitals) facing +z, centred on (cx, cy), 5 x `cell` tall, raised `depth`
        // from z; each stroke a bar.
        private static void Letters(WeaponSculpt s,in WeaponPaint p,string text,float cx,float cy,float z,float cell,float depth)
        {
            float height=cell*5,k=height/NativeDeployArt.GlyphHeight,x=cx-NativeDeployArt.TextWidth(text)*k/2,top=cy+height/2,w=height*.1f;var paint=p;
            foreach(char c in text)
            {
                foreach(var (x0,y0,x1,y1) in NativeDeployArt.Strokes(c))
                {
                    float ax=x+x0*k,ay=top-y0*k,bx=x+x1*k,by=top-y1*k,dx=bx-ax,dy=by-ay,len=(float)Math.Sqrt(dx*dx+dy*dy);
                    s.Box(paint,V((ax+bx)/2,(ay+by)/2,z+depth/2),V(len+w,w,depth),0,0,0,(float)Math.Atan2(dy,dx));
                }
                x+=NativeDeployArt.Advance(c)*k;
            }
        }
        /// <summary>The lettering's width for `cell` (5 x cell tall).</summary>
        public static float LettersWidth(string text,float cell)=>NativeDeployArt.TextWidth(text)*cell*5/NativeDeployArt.GlyphHeight;
        // A dashed light outline round a w x h rectangle facing +z.
        private static void Dashes(WeaponSculpt s,in WeaponPaint p,float w,float h,float cx,float cy,float z,float dash,float width,float depth)
        {
            var paint=p;
            void Run(float x0,float y0,float x1,float y1)
            {
                float len=(float)Math.Sqrt((x1-x0)*(x1-x0)+(y1-y0)*(y1-y0));int n=Math.Max(1,(int)Math.Round(len/(dash*2)));float step=len/n,dx=(x1-x0)/len,dy=(y1-y0)/len;
                for(int i=0;i<n;i++){float m=(i+.5f)*step;float x=x0+dx*m,y=y0+dy*m;s.Box(paint,V(x,y,z+depth*.5f),V(Math.Abs(dx)*step*.55f+Math.Abs(dy)*width,Math.Abs(dy)*step*.55f+Math.Abs(dx)*width,depth));}
            }
            float l=cx-w/2,r=cx+w/2,b=cy-h/2,t=cy+h/2;Run(l,t,r,t);Run(r,t,r,b);Run(r,b,l,b);Run(l,b,l,t);
        }
        // A row of rivets (domed heads) facing +z from a to b.
        private static void Rivets(WeaponSculpt s,in WeaponPaint p,Vector2 a,Vector2 b,float z,int count,float r=.011f)
        {
            for(int i=0;i<count;i++){var q=Vector2.Lerp(a,b,count==1?.5f:i/(float)(count-1));s.Cylinder(p,r,z,z+.008f,8,.003f,q.X,q.Y);}
        }
        // Hazard stripes on a w x h band facing +z: amber bars slanted across its short side over a black backing, kept inside it.
        private static void Stripes(WeaponSculpt s,float w,float h,float cx,float cy,float z,float pitch,float depth)
        {
            s.Box(Black,V(cx,cy,z+depth*.25f),V(w,h,depth*.5f));bool across=h<=w;float span=across?w:h,thick=across?h:w,slant=.6f,len=thick/(float)Math.Cos(slant)-pitch*.36f*(float)Math.Tan(slant);
            float margin=thick*(float)Math.Tan(slant)/2+pitch*.2f;int n=Math.Max(0,(int)((span-2*margin)/pitch)+1);float start=-(n-1)*pitch/2;
            for(int i=0;i<n;i++)
            {
                float t=start+i*pitch;var at=across?V(cx+t,cy,z+depth*.75f):V(cx,cy+t,z+depth*.75f);
                s.Push(WeaponSculpt.Rz(across?slant:Pi/2+slant)*WeaponSculpt.T(at.X,at.Y,at.Z));s.Box(Hazard,V(0,0,0),V(pitch*.36f,len,depth*.5f));s.Pop();
            }
        }
        // A small round gauge facing +z: brass bezel, black face, ivory ticks and a lit needle.
        private static void Gauge(WeaponSculpt s,float x,float y,float z,float r,float needle)
        {
            s.Into(Body);s.Cylinder(Brass,r,z,z+.03f,24,.006f,x,y);s.Cylinder(Black,r*.82f,z+.03f,z+.034f,24,0,x,y);
            s.Into(Lights);for(int k=0;k<9;k++){double a=Math.PI*(1.25-k*.1875);float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);s.Box(Lamp(Ivory,.7f),V(x+ca*r*.68f,y+sa*r*.68f,z+.036f),V(r*.06f,r*.16f,.003f),0,0,0,(float)a-Pi/2);}
            s.Box(Lamp(Amber),V(x+(float)Math.Cos(needle)*r*.32f,y+(float)Math.Sin(needle)*r*.32f,z+.038f),V(r*.66f,r*.05f,.003f),0,0,0,needle);
            s.Into(Body);s.Cylinder(BrassBright,r*.1f,z+.034f,z+.044f,10,0,x,y);
        }
        // A cylindrical bulb (warm marquee lamp) standing out of a face at depth z.
        private static void Bulb(WeaponSculpt s,in WeaponPaint p,float x,float y,float z,float r)
        {s.Sphere(p,V(x,y,z+r*.5f),V(r,r,r*.8f),3,8);}
        // Builds `draw` into a model that faces +z as authored, mirrored so it faces -z (the station's camera side); x is kept.
        private static WeaponMesh Faced(string spec,Action<WeaponSculpt,WeaponMesh> draw)
        {
            var mesh=new WeaponMesh{Spec="deploy-"+spec,Tier=0};var s=new WeaponSculpt(mesh,0);s.Push(WeaponSculpt.S(1,1,-1));draw(s,mesh);s.Pop();return mesh;
        }
        // Writes `draw` into the see-through section with `alpha` (NativeSourceTransparent reads the colour alpha).
        private static void Clear(WeaponSculpt s,WeaponMesh mesh,float alpha,Action draw)
        {
            var section=mesh.Section(Glass,WeaponStretch.None);int from=section.Colors.Count;s.Into(Glass);draw();
            for(int i=from;i<section.Colors.Count;i++){var c=section.Colors[i];section.Colors[i]=new Vector4(c.X,c.Y,c.Z,alpha);}
        }
        /// <summary>Alpha of the see-through parts.</summary>
        public const float GlassAlpha=.14f,GlintAlpha=.09f,DomeAlpha=.13f;
        // Gives a screen section's vertices their uv (Data) from their position.
        private static void Uv(WeaponMesh mesh,Func<Vector3,Vector2> uv)
        {
            foreach(var s in mesh.Sections)if(s.Bone==Screen)for(int i=0;i<s.Positions.Count;i++)s.Data[i]=uv(s.Positions[i]);
        }

        // ---- the INSTANCE MACHINE: a tall graphite and brass "destination engine" ----------------------------------------------
        /// <summary>The instance machine's cabinet (the reels and lever are drawn on their own: Drum, DrumRims, Lever).</summary>
        public static WeaponMesh InstanceMachine()=>Faced(Instance,(s,mesh)=>
        {
            // Authored facing +z (front at z .45-.55); mirrored at the end, so z here is -z in the model. Reel windows at ReelAt.
            s.Into(Body);
            // The plinth: a chamfered footing on a rubber shadow gap with a brass kick rail and a warm under-glow.
            s.Box(GraphiteDark,V(0,.1f,.08f),V(2.12f,.2f,1.4f),.035f);s.Box(Rubber,V(0,.012f,.08f),V(2.02f,.024f,1.3f),.004f);
            s.Box(Brass,V(0,.215f,.62f),V(2.04f,.03f,.04f),.01f);s.Box(BrassDark,V(0,.205f,-.6f),V(2.04f,.02f,.03f),.006f);
            // The lower cabinet and its raised fascia (two-tone), vents and three gauges, a brass maker's plate with rivets.
            Panel(s,Graphite,2.0f,.98f,.05f,0,.72f,-.66f,.46f,.025f);Panel(s,GraphiteLight,1.84f,.84f,.04f,0,.72f,.46f,.5f,.012f);
            foreach(int side in new[]{-1,1})
            {
                for(int k=0;k<7;k++)s.Box(GraphiteDark,V(side*.7f,.42f+k*.075f,.505f),V(.28f,.026f,.012f),.004f);
                s.Box(Brass,V(side*.7f,.395f+.075f*3,.5f),V(.34f,.6f,.006f),.002f);s.Box(GraphiteLight,V(side*.7f,.395f+.075f*3,.503f),V(.31f,.57f,.006f),.002f);
                for(int k=0;k<7;k++)s.Box(Seam,V(side*.7f,.42f+k*.075f,.507f),V(.26f,.02f,.006f));
            }
            Gauge(s,-.22f,.9f,.5f,.085f,1.7f);Gauge(s,0,.9f,.5f,.1f,2.4f);Gauge(s,.22f,.9f,.5f,.085f,.9f);
            s.Into(Body);Panel(s,Brass,.6f,.2f,.03f,0,.52f,.5f,.515f,.006f);Panel(s,BrassDark,.54f,.14f,.02f,0,.52f,.515f,.52f,.003f);
            Rivets(s,BrassBright,new Vector2(-.27f,.6f),new Vector2(.27f,.6f),.515f,6,.008f);Rivets(s,BrassBright,new Vector2(-.27f,.44f),new Vector2(.27f,.44f),.515f,6,.008f);
            s.Into(Lights);Letters(s,Lamp(Ivory,.8f),"DESTINATION",0,.535f,.52f,.0105f,.004f);
            s.Box(Lamp(Warm,.6f),V(-.17f,.485f,.522f),V(.02f,.02f,.003f));s.Box(Lamp(Go,.8f),V(.17f,.485f,.522f),V(.02f,.02f,.003f));
            // Hydraulic struts on the front corners from the plinth to the reel band (chrome rods in graphite sleeves).
            s.Into(Body);
            foreach(int side in new[]{-1,1})
            {
                float x=side*.96f;Column(s,GraphiteDark,.05f,x,.5f,.22f,.72f,12,.008f);Column(s,Chrome,.026f,x,.5f,.72f,1.2f,12,.004f);Column(s,Brass,.055f,x,.5f,.68f,.74f,12,.006f);
                Column(s,Brass,.06f,x,.5f,1.18f,1.26f,12,.008f);
            }
            // The approach strip under the reels: a black channel, three district segments (each with chevrons running outward).
            s.Box(Black,V(0,1.25f,.49f),V(1.9f,.1f,.04f),.008f);s.Box(Brass,V(0,1.31f,.5f),V(1.94f,.02f,.05f),.006f);s.Box(Brass,V(0,1.19f,.5f),V(1.94f,.02f,.05f),.006f);
            for(int i=0;i<3;i++)
            {
                float cx=ReelAt(i).X;s.Into(Approach+i);s.Box(Lamp(0xffffff,.55f),V(cx,1.25f,.512f),V(.5f,.05f,.006f));
                for(int k=0;k<3;k++)s.Box(Lamp(0xffffff),V(cx-.12f+k*.12f,1.25f,.517f),V(.035f,.06f,.006f),0,0,0,.6f);
                s.Into(Body);if(i<2)s.Box(BrassDark,V(cx+.31f,1.25f,.51f),V(.03f,.09f,.03f),.005f);
            }
            // The reel band: a recessed cabinet section, the brass-lipped bezel with three windows, the cavities the drums turn in.
            s.Into(Body);Panel(s,Graphite,2.0f,.92f,.05f,0,1.75f,-.66f,-.3f,.02f);
            const float wy=1.72f,ww=.48f,wh=.56f;
            s.Box(GraphiteLight,V(0,wy+wh/2+.07f,.46f),V(1.98f,.14f,.12f),.025f);s.Box(GraphiteLight,V(0,wy-wh/2-.06f,.46f),V(1.98f,.12f,.12f),.025f);
            foreach(int side in new[]{-1,1}){s.Box(GraphiteLight,V(side*.94f,wy,.46f),V(.1f,wh,.12f),.02f);s.Box(GraphiteLight,V(side*.31f,wy,.46f),V(.14f,wh,.12f),.02f);}
            for(int i=0;i<3;i++)
            {
                float cx=ReelAt(i).X;
                // brass lip, the black cavity behind (top, bottom, sides, back), the payline pointers either side
                s.Box(Brass,V(cx,wy+wh/2+.012f,.525f),V(ww+.05f,.024f,.03f),.008f);s.Box(Brass,V(cx,wy-wh/2-.012f,.525f),V(ww+.05f,.024f,.03f),.008f);
                foreach(int side in new[]{-1,1})s.Box(Brass,V(cx+side*(ww/2+.012f),wy,.525f),V(.024f,wh+.05f,.03f),.008f);
                s.Box(Black,V(cx,wy,.112f-.36f),V(ww,wh+.08f,.02f));s.Box(GraphiteDark,V(cx,wy+.345f,.2f),V(ww,.02f,.5f));s.Box(GraphiteDark,V(cx,wy-.345f,.2f),V(ww,.02f,.5f));
                foreach(int side in new[]{-1,1}){s.Box(GraphiteDark,V(cx+side*(ww/2+.01f),wy,.2f),V(.02f,wh,.5f));Axle(s,SteelDark,.03f,wy,.112f,cx+side*(DrumWidth/2+.02f),cx+side*(ww/2+.005f),10);}
                s.Into(Lights);
                foreach(int side in new[]{-1,1})s.Tri(Lamp(Danger),V(cx+side*(ww/2+.04f),wy+.03f,.545f),V(cx+side*(ww/2+.04f),wy-.03f,.545f),V(cx+side*(ww/2+.005f),wy,.545f));
                // a warm lamp along the cavity's top lights the drum face (hidden behind the lip) and a thin edge glow
                s.Box(Lamp(Ivory),V(cx,wy+wh/2-.005f,.44f),V(ww-.04f,.012f,.02f));s.Box(Lamp(Warm,.7f),V(cx,wy-wh/2+.005f,.44f),V(ww-.04f,.008f,.02f));
                // the entrance label above the window: a brass plate with lit capitals
                s.Into(Body);Panel(s,Brass,.44f,.13f,.025f,cx,wy+wh/2+.12f,.52f,.535f,.006f);Panel(s,GraphiteDark,.41f,.1f,.02f,cx,wy+wh/2+.12f,.535f,.54f,.003f);
                s.Into(Lights);Letters(s,Lamp(Ivory),EntranceLabels[i],cx,wy+wh/2+.12f,.54f,.016f,.004f);
                // the window glass and its glints
                Clear(s,mesh,GlassAlpha,()=>s.Box(WeaponPaint.Glow(0xfff1d8,.03f,.4f),V(cx,wy,.505f),V(ww,wh,.004f)));
                Clear(s,mesh,GlintAlpha,()=>{s.Box(WeaponPaint.Glow(0xffffff,.3f,.6f),V(cx-.12f,wy+.06f,.509f),V(.05f,.42f,.001f),0,0,0,-.5f);s.Box(WeaponPaint.Glow(0xffffff,.3f,.6f),V(cx-.03f,wy+.04f,.509f),V(.016f,.36f,.001f),0,0,0,-.5f);});
                s.Into(Body);
            }
            // The crown: a stepped marquee with an arched top, the brass compass medallion, DESTINATIONS in lit capitals, two
            // alternating bulb banks round the arch, exhaust stacks at the back corners.
            Panel(s,Graphite,2.0f,.62f,.06f,0,2.52f,-.66f,.44f,.025f);Panel(s,GraphiteLight,1.86f,.5f,.05f,0,2.5f,.44f,.48f,.012f);
            s.Box(Brass,V(0,2.21f,.47f),V(2.0f,.03f,.06f),.01f);s.Box(Brass,V(0,2.82f,.47f),V(2.0f,.03f,.06f),.01f);
            var arch=new List<Vector3>();for(int k=0;k<=16;k++){double a=Math.PI*k/16;arch.Add(V(-(float)Math.Cos(a)*.98f,2.84f+(float)Math.Sin(a)*.5f,0));}
            var outline=new Vector3[arch.Count+2];for(int k=0;k<arch.Count;k++)outline[k]=arch[k];outline[arch.Count]=V(.98f,2.8f,0);outline[arch.Count+1]=V(-.98f,2.8f,0);
            Array.Reverse(outline);s.Slab(_=>Graphite,GraphiteLight,Graphite,outline,-.6f,.4f,.02f);
            Vector3[] Ellipse(float rx,float ry,float z){var o=new Vector3[25];for(int k=0;k<25;k++){double a=Math.PI*k/24;o[k]=V(-(float)Math.Cos(a)*rx,2.84f+(float)Math.Sin(a)*ry,z);}return o;}
            s.Tube(Brass,Ellipse(.95f,.48f,.4f),.024f,8,Vector3.UnitZ);s.Tube(BrassDark,Ellipse(.8f,.36f,.41f),.014f,6,Vector3.UnitZ);
            // the medallion: a brass ring with four compass points, its core lit
            s.Cylinder(BrassDark,.2f,.4f,.44f,32,.008f,0,3.0f);s.Cylinder(Black,.165f,.44f,.448f,32,0,0,3.0f);Annulus(s,BrassBright,.165f,.2f,.441f,32,0,3.0f);
            for(int k=0;k<4;k++){float a=k*Pi/2;s.Push(WeaponSculpt.Rz(a)*WeaponSculpt.T(0,3.0f,.446f));s.Tri(Brass,V(-.03f,0,0),V(.03f,0,0),V(0,.15f,0));s.Tri(BrassDark,V(-.022f,0,0),V(.022f,0,0),V(0,-.07f,0));s.Pop();}
            s.Into(Lights);s.Cylinder(Lamp(Warm),.05f,.448f,.455f,20,0,0,3.0f);Annulus(s,Lamp(Amber,.8f),.12f,.13f,.45f,32,0,3.0f);
            Letters(s,Lamp(Ivory),"DESTINATIONS",0,2.5f,.48f,.032f,.006f);
            for(int k=0;k<22;k++){double a=Math.PI*(k+.5)/22;s.Into(k%2==0?Bulbs:BulbsAlt);Bulb(s,Lamp(Warm),-(float)Math.Cos(a)*.86f,2.84f+(float)Math.Sin(a)*.42f,.4f,.026f);}
            for(int k=0;k<14;k++){float x=-.91f+k*.14f;s.Into(k%2==0?Bulbs:BulbsAlt);Bulb(s,Lamp(Warm),x,2.26f,.48f,.022f);Bulb(s,Lamp(Warm),x,2.765f,.48f,.022f);}
            s.Into(Body);
            foreach(int side in new[]{-1,1})
            {
                Column(s,GraphiteDark,.09f,side*.82f,-.48f,2.8f,3.3f,16,.01f);Column(s,Brass,.1f,side*.82f,-.48f,3.26f,3.32f,16,.01f);Column(s,Black,.07f,side*.82f,-.48f,3.32f,3.34f,16);
                for(int k=0;k<3;k++)Column(s,Brass,.098f,side*.82f,-.48f,2.86f+k*.12f,2.89f+k*.12f,16,.006f);
            }
            // The cheeks: two-tone side panels with brass ribs and rivet rows; on the -x side the lever's quadrant and hub.
            foreach(int side in new[]{-1,1})
            {
                s.Box(GraphiteLight,V(side*1.0f,1.5f,-.1f),V(.04f,2.6f,1.12f),.012f);
                foreach(float z in new[]{-.55f,.0f,.35f})s.Box(Brass,V(side*1.025f,1.5f,z),V(.016f,2.5f,.03f),.006f);
                for(int k=0;k<9;k++)foreach(float z in new[]{-.55f,.35f}){s.Push(WeaponSculpt.Ry(side*Pi/2)*WeaponSculpt.T(side*1.034f,.4f+k*.27f,z));s.Cylinder(BrassBright,.009f,0,.006f,8,.002f);s.Pop();}
            }
            {
                float px=-1.0f,py=LeverPivot.Y,pz=-LeverPivot.Z;
                var quad=new List<Vector3>{V(0,0,0)};for(int k=0;k<=10;k++){double a=Math.PI*(.36+.5*k/10);quad.Add(V((float)Math.Cos(a)*.62f,(float)Math.Sin(a)*.62f,0));}
                var ring=quad.ToArray();
                s.Push(WeaponSculpt.Ry(-Pi/2)*WeaponSculpt.T(px-.04f,py,pz));s.Slab(_=>Brass,BrassDark,BrassDark,ring,-.03f,0,.006f);s.Pop();
                Axle(s,GraphiteDark,.13f,py,pz,-1.2f,-1.03f,24,.01f);Axle(s,Brass,.145f,py,pz,-1.1f,-1.07f,24,.006f);
                for(int k=0;k<12;k++){float a=k*Pi/6;Axle(s,BrassBright,.014f,py+(float)Math.Sin(a)*.1f,pz+(float)Math.Cos(a)*.1f,-1.205f,-1.195f,6);}
                s.Into(Lights);for(int k=0;k<6;k++){double a=Math.PI*(.42+.08*k);s.Push(WeaponSculpt.Ry(-Pi/2)*WeaponSculpt.T(px-.045f,py,pz));s.Box(Lamp(k<2?Go:k<4?Warm:Danger,.9f),V((float)Math.Cos(a)*.52f,(float)Math.Sin(a)*.52f,0),V(.06f,.022f,.006f),0,0,0,(float)a);s.Pop();}
                s.Into(Body);
            }
            // The back: a service panel with vents, two cable conduits from the crown to the plinth.
            Panel(s,GraphiteDark,1.7f,2.4f,.05f,0,1.45f,-.7f,-.66f,.006f);
            for(int k=0;k<10;k++)s.Box(Seam,V(0,.5f+k*.2f,-.705f),V(1.4f,.04f,.01f));
            foreach(int side in new[]{-1,1}){s.Push(WeaponSculpt.T(side*.6f,0,0));s.Tube(SteelDark,new[]{V(0,2.9f,-.62f),V(0,2.9f,-.74f),V(0,2.6f,-.76f),V(0,.4f,-.76f),V(0,.25f,-.7f)},.04f,10,Vector3.UnitZ);s.Pop();}
            // Lights: the corner strips, the bezel's under-glow, the kick glow.
            s.Into(Lights);
            foreach(int side in new[]{-1,1}){s.Box(Lamp(Warm,.85f),V(side*.985f,1.75f,.405f),V(.012f,.84f,.012f));s.Box(Lamp(Amber,.7f),V(side*1.02f,.72f,.45f),V(.008f,.8f,.008f));}
            s.Box(Lamp(Warm,.7f),V(0,1.385f,.52f),V(1.8f,.008f,.006f));s.Box(Lamp(Amber,.8f),V(0,.19f,.63f),V(1.9f,.012f,.008f));
        });

        /// <summary>One reel's symbol band about its axle (x), front toward -z; uv x across the band, y the strip position in
        /// symbols from the front (DrumSlots round the drum), so the draw scrolls it (Runtime: _Scroll, _Rep) and any strip lands.</summary>
        public static WeaponMesh Drum()
        {
            var mesh=new WeaponMesh{Spec="deploy-drum",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Screen);var sec=mesh.Section(Screen,WeaponStretch.None);const int n=60;float r=DrumRadius,w=DrumWidth/2;
            for(int k=0;k<n;k++)
            {
                float a0=-Pi+2*Pi*k/n,a1=-Pi+2*Pi*(k+1)/n;Vector3 P(float x,float a)=>V(x,r*(float)Math.Sin(a),-r*(float)Math.Cos(a));
                int at=sec.Positions.Count;s.Quad(WeaponPaint.Solid(0xffffff),P(-w,a0),P(-w,a1),P(w,a1),P(w,a0));if(sec.Positions.Count-at!=4)continue;
                float v0=a0/(2*Pi)*DrumSlots,v1=a1/(2*Pi)*DrumSlots;sec.Data[at]=new Vector2(0,v0);sec.Data[at+1]=new Vector2(0,v1);sec.Data[at+2]=new Vector2(1,v1);sec.Data[at+3]=new Vector2(1,v0);
            }
            return mesh;
        }
        /// <summary>A reel's two brass rims with knurled edges and spokes (bone 0), turned with the strip about the axle (x).</summary>
        public static WeaponMesh DrumRims()
        {
            var mesh=new WeaponMesh{Spec="deploy-drum-rims",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);float r=DrumRadius;
            foreach(int side in new[]{-1,1})
            {
                float x=side*(DrumWidth/2+.012f);Axle(s,Brass,r+.014f,0,0,x-.012f,x+.012f,40,.004f);Axle(s,BrassDark,r-.05f,0,0,x+side*.012f-.004f,x+side*.012f+.004f,32);
                for(int k=0;k<20;k++){float a=k*Pi/10;s.Box(BrassBright,V(x,(r+.012f)*(float)Math.Sin(a),-(r+.012f)*(float)Math.Cos(a)),V(.026f,.012f,.012f),0,a,0,0);}
                for(int k=0;k<6;k++){float a=k*Pi/3;s.Push(WeaponSculpt.Rx(a));s.Box(BrassDark,V(x+side*.016f,0,-r*.5f),V(.01f,.03f,r*.9f),.003f);s.Pop();}
            }
            return mesh;
        }
        /// <summary>The lever about LeverPivot (local, the hub at the origin): a chrome arm out from the cabinet's side, a brass
        /// collar, a big red ball with a highlight band; up and leaning back at rest (LeverAngle(0)).</summary>
        public static WeaponMesh Lever()
        {
            var mesh=new WeaponMesh{Spec="deploy-lever",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            Axle(s,Brass,.07f,0,0,-.13f,-.02f,20,.008f);Axle(s,BrassBright,.075f,0,0,-.13f,-.115f,20,.004f);
            s.Box(SteelDark,V(-.085f,.07f,0),V(.06f,.18f,.08f),.012f);
            Column(s,Chrome,.032f,-.085f,0,.12f,.98f,14,.004f);Column(s,Brass,.045f,-.085f,0,.14f,.22f,14,.008f);Column(s,Brass,.04f,-.085f,0,.88f,.94f,14,.006f);
            Column(s,Rubber,.038f,-.085f,0,.6f,.86f,14,.006f);for(int k=0;k<6;k++)Column(s,Black,.04f,-.085f,0,.62f+k*.04f,.635f+k*.04f,14);
            s.Sphere(Red,V(-.085f,1.06f,0),V(.12f,.12f,.12f),10,18);
            s.Into(Lights);s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(-.085f,1.06f,0));s.Torus(Lamp(Warm),V(0,0,0),.121f,.008f,32,6);s.Pop();
            return mesh;
        }

        // ---- the MUTATOR MACHINE: a cooler "wild card" cabinet, three card dials under a glass dome -----------------------------
        public static WeaponMesh MutatorMachine()=>Faced(Mutator,(s,mesh)=>
        {
            // Authored facing +z (front at z .48); the dome sits on the counter flush with its front, the sign behind it.
            s.Into(Body);
            // The plinth and counter: blue-graphite enamel with chrome trim, cooler than the instance machine's brass.
            s.Box(GraphiteDark,V(0,.1f,.08f),V(2.12f,.2f,1.4f),.035f);s.Box(Rubber,V(0,.012f,.08f),V(2.02f,.024f,1.3f),.004f);s.Box(Chrome,V(0,.215f,.62f),V(2.04f,.026f,.035f),.008f);
            Panel(s,Enamel,2.0f,.86f,.06f,0,.64f,-.66f,.44f,.025f);Panel(s,EnamelLight,1.84f,.72f,.05f,0,.62f,.44f,.48f,.012f);
            s.Box(Chrome,V(0,1.065f,.06f),V(2.02f,.03f,1.2f),.01f);
            foreach(int side in new[]{-1,1}){s.Push(WeaponSculpt.Rz(side*.1f)*WeaponSculpt.T(side*.95f,.62f,.47f));s.Box(Chrome,V(0,0,0),V(.05f,.66f,.06f),.012f);s.Pop();}
            // the fascia: WILD CARD in lit capitals, the SKIP plate (its dashed outline) on the left, the ROLL plunger on the
            // right, three legend lamps (boon green, bane red, chaos violet) along the foot
            s.Into(Lights);Letters(s,Lamp(Ice,.9f),"WILD CARD",0,.9f,.48f,.022f,.005f);
            s.Into(Body);Panel(s,SteelDark,.64f,.26f,.03f,-.42f,.6f,.48f,.5f,.008f);Panel(s,Graphite,.58f,.2f,.025f,-.42f,.6f,.5f,.515f,.006f);
            s.Into(Skip);Letters(s,Lamp(0xffd27a),"SKIP",-.42f,.6f,.515f,.026f,.006f);Dashes(s,Lamp(0xffd27a),.7f,.32f,-.42f,.6f,.48f,.03f,.012f,.006f);
            s.Into(Body);s.Cylinder(Chrome,.15f,.48f,.52f,32,.01f,.42f,.6f);s.Cylinder(GraphiteDark,.12f,.52f,.54f,32,.004f,.42f,.6f);
            s.Into(Lights);s.Cylinder(Lamp(Violet),.105f,.54f,.6f,32,.014f,.42f,.6f);Annulus(s,Lamp(Ice),.122f,.138f,.522f,32,.42f,.6f);
            int[] legend={0x5dff9a,0xff4d5e,0xc77dff};
            for(int i=0;i<3;i++){float x=-.3f+.3f*i;s.Into(Body);s.Cylinder(Chrome,.045f,.48f,.5f,20,.004f,x,.33f);s.Into(Lights);s.Cylinder(Lamp(legend[i]),.034f,.5f,.506f,20,0,x,.33f);}
            // The dome's deck: the spindle hubs ringed by the polarity lights (bones Ring + i), the spine the dials hang from.
            s.Into(Body);s.Box(GraphiteDark,V(0,1.095f,-.14f),V(1.9f,.03f,1.0f),.006f);
            for(int i=0;i<3;i++)
            {
                var c=CardAt(i);float x=c.X,z=-c.Z;s.Into(Body);Column(s,Chrome,.1f,x,z,1.1f,1.14f,24,.006f);Column(s,GraphiteDark,.075f,x,z,1.14f,1.16f,24,.004f);
                Column(s,Steel,.012f,x,z,1.16f,c.Y-CardHeight/2,8);Column(s,Steel,.012f,x,z,c.Y+CardHeight/2,c.Y+CardHeight/2+.04f,8);Column(s,Chrome,.03f,x,z,c.Y+CardHeight/2+.035f,c.Y+CardHeight/2+.065f,12,.004f);
                s.Into(Ring+i);s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,1.142f,z));s.Torus(Lamp(0xffffff),V(0,0,0),.14f,.012f,36,6);s.Torus(Lamp(0xffffff,.6f),V(0,0,0),.17f,.006f,36,4);s.Pop();
                FlatRing(s,Lamp(0xffffff,.35f),.08f,.125f,1.111f,28,x,z);
            }
            const float dr=.58f,dy=1.11f,dz=-.16f;
            s.Into(Body);Axle(s,Chrome,.016f,CardAt(0).Y+CardHeight/2+.075f,dz,-.92f,.92f,10,.004f);
            // the light wall behind the dome: enamel with five vertical ribs (cool and violet) and chrome edges
            s.Box(Enamel,V(0,1.5f,-.76f),V(2.0f,.84f,.04f),.01f);s.Box(Chrome,V(0,1.93f,-.76f),V(2.0f,.03f,.05f),.008f);
            for(int k=0;k<5;k++){float x=-.8f+.4f*k;s.Into(Body);s.Box(GraphiteDark,V(x,1.5f,-.735f),V(.06f,.74f,.01f),.003f);s.Into(Lights);s.Box(Lamp(k%2==0?Cool:Violet,.8f),V(x,1.5f,-.728f),V(.018f,.7f,.004f));}
            // The dome: a glass half-cylinder over the dials with chrome arches at its ends and between the dials.
            s.Into(Body);foreach(float x in new[]{-.94f,-.3f,.3f,.94f}){s.Push(WeaponSculpt.Ry(Pi/2)*WeaponSculpt.T(x,dy,dz));s.Torus(Chrome,V(0,0,0),dr+.012f,.018f,24,8,0,Pi);s.Pop();}
            Clear(s,mesh,DomeAlpha,()=>
            {
                var g=WeaponPaint.Glow(0xdcecff,.04f,.45f);const int seg=28;
                for(int k=0;k<seg;k++){float a0=Pi*k/seg,a1=Pi*(k+1)/seg;Vector3 P(float x,float a)=>V(x,dy+dr*(float)Math.Sin(a),dz+dr*(float)Math.Cos(a));s.Quad(g,P(-.94f,a0),P(-.94f,a1),P(.94f,a1),P(.94f,a0));s.Quad(g,P(-.94f,a1),P(-.94f,a0),P(.94f,a0),P(.94f,a1));}
                foreach(int side in new[]{-1,1})for(int k=0;k<seg;k++){float a0=Pi*k/seg,a1=Pi*(k+1)/seg;Vector3 P(float a)=>V(side*.94f,dy+dr*(float)Math.Sin(a),dz+dr*(float)Math.Cos(a));var o=V(side*.94f,dy,dz);s.Tri(g,o,P(a0),P(a1));s.Tri(g,o,P(a1),P(a0));}
            });
            Clear(s,mesh,GlintAlpha,()=>{for(int k=0;k<2;k++){float a=Pi*(.6f+.07f*k);s.Box(WeaponPaint.Glow(0xffffff,.3f,.6f),V(-.1f+k*.1f,dy+(dr+.004f)*(float)Math.Sin(a),dz+(dr+.004f)*(float)Math.Cos(a)),V(1.5f,.02f-.008f*k,.002f),0,a-Pi/2,0,0);}});
            // The OPTIONAL sign behind and above the dome: chrome posts, a dark plate, OPTIONAL in lit capitals in a dashed outline.
            s.Into(Body);foreach(int side in new[]{-1,1}){Column(s,Chrome,.025f,side*.98f,-.7f,1.08f,2.58f,10,.004f);Column(s,GraphiteDark,.04f,side*.98f,-.7f,2.56f,2.62f,10,.006f);}
            Panel(s,GraphiteDark,1.86f,.4f,.05f,0,2.34f,-.76f,-.68f,.012f);Panel(s,Black,1.76f,.32f,.04f,0,2.34f,-.68f,-.67f,.004f);
            s.Box(Chrome,V(0,2.555f,-.72f),V(1.9f,.025f,.1f),.006f);s.Box(Chrome,V(0,2.125f,-.72f),V(1.9f,.025f,.1f),.006f);
            s.Into(Optional);Letters(s,Lamp(Ice),"OPTIONAL",0,2.34f,-.67f,.034f,.008f);Dashes(s,Lamp(Cool),1.64f,.27f,0,2.34f,-.67f,.04f,.014f,.008f);
            // The cheeks: enamel sides with chrome edge strips and cool light slits; the back service panel.
            s.Into(Body);
            foreach(int side in new[]{-1,1})
            {
                s.Box(EnamelLight,V(side*1.0f,.64f,-.1f),V(.04f,.84f,1.12f),.012f);s.Box(Chrome,V(side*1.024f,.64f,.4f),V(.016f,.8f,.03f),.005f);
                s.Into(Lights);for(int k=0;k<4;k++)s.Box(Lamp(k%2==0?Cool:Violet,.8f),V(side*1.025f,.38f+k*.16f,-.2f),V(.006f,.02f,.5f));s.Into(Body);
            }
            Panel(s,GraphiteDark,1.7f,.9f,.05f,0,.64f,-.7f,-.66f,.006f);
            s.Into(Lights);s.Box(Lamp(Cool,.8f),V(0,.19f,.63f),V(1.9f,.012f,.008f));s.Box(Lamp(Violet,.7f),V(0,1.04f,.485f),V(1.84f,.008f,.006f));
        });
        /// <summary>One card dial about its spindle (local: the card's centre at the origin, its axis y): the card's two faces (the
        /// screen section; each face's uv reads left to right from its own side, u over CardArt of the tile, centred) and its chrome
        /// edge frame.</summary>
        public static WeaponMesh CardDial()
        {
            var mesh=new WeaponMesh{Spec="deploy-card",Tier=0};var s=new WeaponSculpt(mesh,0);float w=CardWidth/2,h=CardHeight/2,t=.007f;
            s.Into(Body);var o=WeaponSculpt.Rect(CardWidth+.02f,CardHeight+.02f,.03f,0);s.Slab(_=>Chrome,GraphiteDark,GraphiteDark,o,-t,t,.003f);
            s.Into(Screen);var white=WeaponPaint.Solid(0xffffff);
            s.Quad(white,V(w,-h,-t-.001f),V(-w,-h,-t-.001f),V(-w,h,-t-.001f),V(w,h,-t-.001f));
            s.Quad(white,V(-w,-h,t+.001f),V(w,-h,t+.001f),V(w,h,t+.001f),V(-w,h,t+.001f));
            Uv(mesh,p=>{float side=p.Z<0?1:-1;return new Vector2(.5f+CardArt*(side*p.X)/CardWidth,(p.Y+h)/CardHeight);});
            return mesh;
        }
        /// <summary>A unit quad facing -z (uv 0..1 left to right, bottom to top from the front): the stamps and other flat screens.</summary>
        public static WeaponMesh Quad()
        {
            var mesh=new WeaponMesh{Spec="deploy-quad",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Screen);
            s.Quad(WeaponPaint.Solid(0xffffff),V(.5f,-.5f,0),V(-.5f,-.5f,0),V(-.5f,.5f,0),V(.5f,.5f,0));Uv(mesh,p=>new Vector2(p.X+.5f,p.Y+.5f));return mesh;
        }

        // ---- the holo WAR-TABLE: radius 1.6, a projector ring under a glass top, the map floating over it ----------------------
        public static WeaponMesh WarTable()
        {
            var mesh=new WeaponMesh{Spec="deploy-table",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            // the stepped plinth with a brass trim ring and floor bolts
            Column(s,GraphiteDark,1.3f,0,0,0,.1f,64,.02f);Column(s,Graphite,1.12f,0,0,.1f,.22f,64,.02f);FlatRing(s,Brass,1.1f,1.16f,.222f,64);
            for(int k=0;k<12;k++){float a=k*Pi/6;Column(s,SteelDark,.03f,1.2f*(float)Math.Cos(a),1.2f*(float)Math.Sin(a),.1f,.12f,6);}
            // the pedestal: a ribbed column with cyan slits between the ribs
            Column(s,GraphiteDark,.56f,0,0,.22f,.8f,48,.01f);
            for(int k=0;k<16;k++){float a=(k+.5f)*Pi/8;s.Push(WeaponSculpt.Ry(-a));s.Box(GraphiteLight,V(0,.51f,.57f),V(.08f,.56f,.06f),.012f);s.Pop();}
            s.Into(Lights);for(int k=0;k<16;k++){float a=k*Pi/8;s.Push(WeaponSculpt.Ry(-a));s.Box(Lamp(Cool,.8f),V(0,.51f,.565f),V(.015f,.48f,.01f));s.Pop();}
            // the tabletop: a thick disc with a brass rim, the projector ring of lenses round the glass, the dark display bed
            s.Into(Body);Column(s,Graphite,1.6f,0,0,.8f,.94f,96,.025f);s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.94f,0));s.Torus(Brass,V(0,0,0),1.585f,.022f,96,8);s.Pop();
            Column(s,GraphiteDark,1.6f,0,0,.76f,.8f,96,.01f);FlatRing(s,GraphiteLight,1.46f,1.58f,.942f,96);FlatRing(s,BrassDark,1.44f,1.46f,.946f,96);
            Column(s,Black,1.44f,0,0,.93f,.95f,96);
            for(int k=0;k<32;k++){float a=k*Pi/16;float x=1.52f*(float)Math.Cos(a),z=1.52f*(float)Math.Sin(a);s.Into(Body);Column(s,SteelDark,.03f,x,z,.94f,.965f,10,.004f);s.Into(Lights);Column(s,Lamp(Ice),.02f,x,z,.965f,.97f,10);}
            // a faint grid etched in the bed under the glass
            s.Into(Lights);for(int k=-6;k<=6;k++){float c=k*.2f,half=(float)Math.Sqrt(Math.Max(0,1.42f*1.42f-c*c));s.Box(Lamp(Cool,.18f),V(c,.952f,0),V(.006f,.002f,2*half));s.Box(Lamp(Cool,.18f),V(0,.952f,c),V(2*half,.002f,.006f));}
            FlatRing(s,Lamp(Cool,.5f),1.38f,1.4f,.953f,96);
            // the glass top and its glint
            Clear(s,mesh,GlassAlpha,()=>{s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.968f,0));s.Cylinder(WeaponPaint.Glow(0xc8ecff,.03f,.4f),1.44f,0,.006f,96);s.Pop();});
            return mesh;
        }
        /// <summary>The holo map's plane (table local; MapAt, MapWidth x MapDepth), its uv (0,0) the blueprint's south-west corner.</summary>
        public static WeaponMesh MapPlane()
        {
            var mesh=new WeaponMesh{Spec="deploy-map",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Screen);float w=MapWidth/2,d=MapDepth/2,y=MapAt.Y;
            const int n=12;for(int i=0;i<n;i++)for(int j=0;j<n;j++){float x0=-w+2*w*i/n,x1=-w+2*w*(i+1)/n,z0=-d+2*d*j/n,z1=-d+2*d*(j+1)/n;s.Quad(WeaponPaint.Solid(0xffffff),V(x0,y,z0),V(x0,y,z1),V(x1,y,z1),V(x1,y,z0));}
            Uv(mesh,p=>new Vector2((p.X+w)/(2*w),(p.Z+d)/(2*d)));return mesh;
        }
        /// <summary>The projector's light volume from the ring up to the map (screen section, additive; vertex alpha fades it up).</summary>
        public static WeaponMesh HoloVolume()
        {
            var mesh=new WeaponMesh{Spec="deploy-holo",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Screen);const int n=64;
            for(int k=0;k<n;k++)
            {
                float a0=2*Pi*k/n,a1=2*Pi*(k+1)/n;Vector3 P(float r,float y,float a)=>V(r*(float)Math.Cos(a),y,r*(float)Math.Sin(a));
                s.Quad(WeaponPaint.Solid(0xffffff),P(1.5f,.97f,a0),P(1.5f,.97f,a1),P(1.3f,MapAt.Y+.04f,a1),P(1.3f,MapAt.Y+.04f,a0));
                s.Quad(WeaponPaint.Solid(0xffffff),P(1.5f,.97f,a1),P(1.5f,.97f,a0),P(1.3f,MapAt.Y+.04f,a0),P(1.3f,MapAt.Y+.04f,a1));
            }
            foreach(var sec in mesh.Sections)for(int i=0;i<sec.Positions.Count;i++){float u=(sec.Positions[i].Y-.97f)/(MapAt.Y+.04f-.97f);sec.Colors[i]=new Vector4(1,1,1,1-u);sec.Data[i]=new Vector2(.5f,.5f);}
            return mesh;
        }
        /// <summary>A holo beacon (an entrance or the boss gate on the map): a thin hexagonal light column on a ring, base at the
        /// origin, drawn additive and tinted.</summary>
        public static WeaponMesh Beacon()
        {
            var mesh=new WeaponMesh{Spec="deploy-beacon",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Screen);var white=WeaponPaint.Solid(0xffffff);
            Column(s,white,.022f,0,0,0,.32f,6);FlatRing(s,white,.07f,.085f,.004f,24);FlatRing(s,white,.04f,.05f,.004f,24);
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.36f,0));s.Gem(white,V(0,0,0),.05f,.08f,4,.5f);s.Pop();
            foreach(var sec in mesh.Sections)for(int i=0;i<sec.Positions.Count;i++){float y=sec.Positions[i].Y;sec.Colors[i]=new Vector4(1,1,1,y<.3f?.55f+.45f*(1-y/.3f):1);sec.Data[i]=new Vector2(.5f,.5f);}
            return mesh;
        }

        // ---- a party PAD: a raised disc with a ready-lamp ring (bone 1, the seat's readiness) --------------------------------------
        public static WeaponMesh PartyPad()
        {
            var mesh=new WeaponMesh{Spec="deploy-pad",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            Column(s,GraphiteDark,.58f,0,0,0,.05f,48,.012f);Column(s,Graphite,.5f,0,0,.05f,.085f,48,.01f);FlatRing(s,GraphiteDark,.5f,.56f,.052f,48);
            FlatRing(s,BrassDark,.36f,.38f,.0855f,40);
            var hex=WeaponSculpt.Circle(6,.2f,.2f,0,Pi/6);s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.085f,0));s.Slab(_=>Brass,BrassDark,BrassDark,hex,0,.006f,.002f);s.Pop();
            for(int k=0;k<3;k++){float a=k*2*Pi/3+Pi/6;s.Box(SteelDark,V(.54f*(float)Math.Cos(a),.06f,.54f*(float)Math.Sin(a)),V(.08f,.04f,.08f),.01f,0,-a,0);}
            s.Into(Lights);FlatRing(s,Lamp(0xffffff),.515f,.545f,.054f,48);
            for(int k=0;k<12;k++){float a=k*Pi/6;s.Box(Lamp(0xffffff,.8f),V(.43f*(float)Math.Cos(a),.0865f,.43f*(float)Math.Sin(a)),V(.06f,.002f,.02f),0,-a,0);}
            return mesh;
        }

        // ---- the DEPLOY GATE: a portal frame on the back wall, two blast-door leaves, a lift platform in the bay behind ---------
        public static WeaponMesh GateFrame()=>Faced(Gate,(s,mesh)=>
        {
            // Authored facing +z (front at z .7); the bay runs back to z -4.2 (mirrored: +z in the model).
            s.Into(Body);const float hw=2.45f,top=4.45f;
            // the pillars: massive stepped graphite with brass caps, hazard-striped jambs and lit slots
            foreach(int side in new[]{-1,1})
            {
                float x=side*(hw+.42f);s.Box(Graphite,V(x,2.75f,0),V(.84f,5.5f,1.3f),.04f);s.Box(GraphiteLight,V(x+side*.06f,2.75f,.66f),V(.6f,5.3f,.06f),.015f);
                s.Box(BrassDark,V(x,5.52f,0),V(.92f,.08f,1.36f),.02f);s.Box(GraphiteDark,V(x,.15f,.05f),V(.96f,.3f,1.42f),.03f);
                s.Push(WeaponSculpt.Ry(-side*Pi/2)*WeaponSculpt.T(side*hw,0,0));Stripes(s,1.1f,4.2f,0,2.15f,-.004f,.22f,.02f);s.Pop();
                for(int k=0;k<5;k++)s.Box(GraphiteDark,V(x+side*.06f,.8f+k*.95f,.7f),V(.4f,.05f,.02f),.004f);
                s.Into(Lights);s.Box(Lamp(Cool,.9f),V(x-side*.22f,2.75f,.695f),V(.02f,4.8f,.01f));s.Into(Body);
            }
            // the header: a deep beam with the DEPLOY sign, eight readiness lamp sockets, a hazard band and track rails for the leaves
            s.Box(Graphite,V(0,top+.5f,0),V(2*hw+1.7f,1.0f,1.3f),.04f);s.Box(GraphiteLight,V(0,top+.55f,.66f),V(2*hw+.2f,.7f,.06f),.015f);
            Stripes(s,2*hw,.14f,0,top+.08f,.66f,.24f,.02f);
            Panel(s,BrassDark,2.3f,.34f,.04f,0,top+.62f,.69f,.71f,.006f);Panel(s,Black,2.2f,.26f,.03f,0,top+.62f,.71f,.72f,.004f);
            s.Into(Sign);Letters(s,Lamp(White),"DEPLOY",0,top+.62f,.72f,.05f,.01f);
            s.Into(Body);for(int i=0;i<NativeDeployPresentation.Pads;i++){var l=GateLampAt(i);s.Cylinder(Brass,.07f,.69f,.72f,20,.006f,l.X,l.Y);}
            foreach(float z in new[]{.42f,-.42f})s.Box(SteelDark,V(0,top-.05f,z),V(2*hw+1.6f,.08f,.06f),.01f);
            // the threshold: a raised steel sill with chevrons and the floor track
            s.Box(SteelDark,V(0,.03f,.1f),V(2*hw,.06f,.9f),.01f);
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.06f,.42f));Stripes(s,2*hw,.18f,0,0,0,.2f,.01f);s.Pop();
            // the bay: floor, side walls with ribs and lights, the shaft's back wall with a vertical light column, a ceiling hood
            s.Box(PlateDark,V(0,-.02f,-2.1f),V(2*hw+.4f,.04f,3.9f));
            foreach(int side in new[]{-1,1})
            {
                s.Box(Wall,V(side*(hw+.25f),2.6f,-2.1f),V(.3f,5.2f,3.9f));
                for(int k=0;k<4;k++){s.Into(Body);s.Box(WallLight,V(side*(hw+.08f),2.6f,-.6f-k*1.0f),V(.08f,5.0f,.14f),.02f);s.Into(Lights);s.Box(Lamp(Ice,.9f),V(side*(hw+.03f),2.6f,-1.1f-k*1.0f),V(.02f,4.6f,.04f));}
                s.Into(Body);
            }
            s.Box(Wall,V(0,2.6f,-4.1f),V(2*hw+.8f,5.2f,.2f));s.Box(WallLight,V(0,top+.4f,-2.1f),V(2*hw+.4f,.5f,3.9f));
            s.Into(Lights);s.Box(Lamp(Ice),V(0,2.4f,-3.99f),V(.5f,4.4f,.02f));s.Box(Lamp(White,.8f),V(0,top+.14f,-2.1f),V(2*hw-.4f,.02f,3.4f));
            foreach(int side in new[]{-1,1})s.Box(Lamp(Ice,.7f),V(side*1.6f,2.4f,-3.99f),V(.14f,4.4f,.02f));
        });
        /// <summary>A blast-door leaf, closed (gate local): the left (-1) spans x -2.45..0 at the door plane, the right (+1) is it
        /// mirrored. Each slides out DoorTravel x Gate.</summary>
        public static WeaponMesh DoorLeaf(int side)
        {
            var mesh=new WeaponMesh{Spec="deploy-door-"+(side<0?"left":"right"),Tier=0};var s=new WeaponSculpt(mesh,0);s.Push(WeaponSculpt.S(side<0?1:-1,1,-1));s.Into(Body);
            const float w=2.45f,h=4.45f,cx=-w/2;
            s.Box(SteelDark,V(cx,h/2,0),V(w,h,.3f),.03f);
            for(int r=0;r<4;r++)for(int c=0;c<2;c++){float px=cx-.55f+c*1.1f,py=.62f+r*1.06f;Panel(s,Graphite,1.0f,.94f,.08f,px,py,.15f,.19f,.015f);s.Box(GraphiteDark,V(px,py,.195f),V(.8f,.06f,.01f),.004f);}
            // the meeting edge: interlocking teeth and a lit seam; a hazard band at the foot; heavy rivets down the edge
            for(int k=0;k<9;k++){float y=.3f+k*.48f;s.Box(Steel,V(-.06f,y,.08f),V(.12f,.22f,.16f),.02f);}
            s.Push(WeaponSculpt.T(0,0,.19f));Stripes(s,w-.1f,.32f,cx,.2f,0,.26f,.02f);s.Pop();
            Rivets(s,SteelDark,new Vector2(-.22f,.5f),new Vector2(-.22f,h-.3f),.15f,12,.016f);Rivets(s,SteelDark,new Vector2(-w+.12f,.5f),new Vector2(-w+.12f,h-.3f),.15f,12,.016f);
            s.Into(Lights);s.Box(Lamp(Warm),V(-.01f,h/2,.16f),V(.02f,h-.4f,.01f));s.Box(Lamp(Danger,.9f),V(cx,h-.18f,.16f),V(w-.4f,.03f,.01f));
            // half of each lit chevron (the leaves meet in an upward arrow at the seam)
            for(int k=0;k<3;k++){float y=2.0f+k*.42f;s.Box(Lamp(Ice,.6f+.2f*k),V(-.3f,y-.17f,.2f),V(.62f,.05f,.012f),0,0,0,.52f);}
            s.Pop();return mesh;
        }
        /// <summary>The light shaft a pilot rises in at Deploy (pad local; screen section, additive): a vertical ribbon in the
        /// local XY plane that the stage turns to face the camera, soft at its edges with a bright core and fading upward (vertex
        /// alpha), so it reads as a shaft of light from any shot instead of a solid tube; and a soft glow disc on the pad.</summary>
        public static WeaponMesh Beam()
        {
            var mesh=new WeaponMesh{Spec="deploy-beam",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Screen);const int rows=8;const float h=4.2f;
            // denser columns near the centre carry the core's falloff through linear interpolation
            float[] xs={-.36f,-.26f,-.17f,-.1f,-.06f,-.03f,0,.03f,.06f,.1f,.17f,.26f,.36f};
            for(int k=0;k+1<xs.Length;k++)for(int j=0;j<rows;j++)
            {
                float y0=h*j/rows,y1=h*(j+1)/rows;
                s.Quad(WeaponPaint.Solid(0xffffff),V(xs[k],y0,0),V(xs[k+1],y0,0),V(xs[k+1],y1,0),V(xs[k],y1,0));
            }
            var sec=mesh.Sections[mesh.Sections.Count-1];int ribbon=sec.Positions.Count;
            for(int i=0;i<ribbon;i++)
            {
                var p=sec.Positions[i];float x=Math.Abs(p.X)/.36f,y=p.Y/h,halo=(float)Math.Pow(Math.Max(0,1-x),1.5),core=(float)Math.Exp(-p.X*p.X/(.05f*.05f));
                sec.Colors[i]=new Vector4(1,1,1,Math.Min(1,.5f*halo+.5f*core)*(1-y)*(1-y));sec.Data[i]=new Vector2(.5f,.5f);
            }
            Column(s,WeaponPaint.Solid(0xffffff),.3f,0,0,0,.02f,32);
            for(int i=ribbon;i<sec.Positions.Count;i++){sec.Colors[i]=new Vector4(1,1,1,.45f);sec.Data[i]=new Vector2(.5f,.5f);}
            return mesh;
        }
        /// <summary>The pad's top (its brass inlay): where a pilot's feet stand.</summary>
        public const float PadTop=.091f;
        /// <summary>The lift platform (gate local, at LiftAt when down): a ribbed deck with rails, guard posts and edge lights.</summary>
        public static WeaponMesh LiftDeck()
        {
            var mesh=new WeaponMesh{Spec="deploy-lift",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            s.Box(SteelDark,V(0,.08f,0),V(4.6f,.16f,3.4f),.03f);for(int k=0;k<14;k++)s.Box(Steel,V(-2.1f+k*.32f,.17f,0),V(.06f,.02f,3.2f),.006f);
            foreach(int side in new[]{-1,1})
            {
                for(int k=0;k<3;k++)Column(s,Hazard,.035f,side*2.2f,-1.5f+k*1.5f,.16f,1.1f,10,.004f);
                s.Push(WeaponSculpt.T(side*2.2f,0,0));s.Tube(Hazard,new[]{V(0,1.1f,-1.55f),V(0,1.1f,1.55f)},.03f,10,Vector3.UnitY);s.Tube(Hazard,new[]{V(0,.62f,-1.55f),V(0,.62f,1.55f)},.025f,10,Vector3.UnitY);s.Pop();
            }
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.18f,-1.5f));Stripes(s,4.4f,.24f,0,0,0,.24f,.01f);s.Pop();
            s.Into(Lights);foreach(int side in new[]{-1,1})s.Box(Lamp(Go),V(side*2.27f,.1f,0),V(.02f,.04f,3.3f));s.Box(Lamp(Go),V(0,.1f,-1.72f),V(4.5f,.04f,.02f));
            return mesh;
        }
        /// <summary>The DEPLOY console (station frame at ConsoleAt, facing -z): a slanted lectern with the big DEPLOY button (bone
        /// Button), a status screen (bone 1) and eight readiness lamp sockets (the Lamp draws).</summary>
        public static WeaponMesh DeployConsole()=>Faced(Console,(s,mesh)=>
        {
            s.Into(Body);s.Box(GraphiteDark,V(0,.06f,0),V(1.8f,.12f,.8f),.02f);
            var side=WeaponSculpt.Outline(0,-.3f,.12f,.32f,.12f,.32f,.98f,-.3f,1.22f);
            foreach(float x in new[]{-.84f,.84f}){s.Push(WeaponSculpt.Ry(-Pi/2)*WeaponSculpt.T(x,0,0));s.Slab(_=>Graphite,GraphiteLight,GraphiteLight,side,-.06f,.06f,.012f);s.Pop();}
            s.Box(Graphite,V(0,.55f,0),V(1.62f,.86f,.6f),.02f);
            s.Push(WeaponSculpt.Rx(.53f)*WeaponSculpt.T(0,1.1f,.02f));
            s.Box(GraphiteLight,V(0,0,0),V(1.8f,.06f,.74f),.015f);s.Box(Brass,V(0,.035f,.37f),V(1.8f,.02f,.03f),.006f);
            s.Push(WeaponSculpt.Rx(Up));Panel(s,Black,.72f,.36f,.03f,-.42f,.02f,.03f,.04f,.004f);Panel(s,Brass,.44f,.44f,.06f,.42f,.02f,.03f,.045f,.008f);
            s.Cylinder(SteelDark,.17f,.045f,.07f,32,.008f,.42f,.02f);s.Cylinder(Red,.13f,.07f,.1f,32,.012f,.42f,.02f);
            s.Into(Button);s.Cylinder(Lamp(0xffffff),.105f,.1f,.13f,32,.02f,.42f,.02f);
            s.Into(Lights);Panel(s,Lamp(Cool,.5f),.66f,.3f,.02f,-.42f,.02f,.04f,.042f,0);for(int k=0;k<4;k++)s.Box(Lamp(Ice),V(-.6f+k*.08f+(k%2)*.04f,.08f-k*.05f,.043f),V(.1f+(k%3)*.06f,.012f,.002f));
            s.Pop();s.Pop();
            s.Into(Body);Panel(s,BrassDark,1.22f,.12f,.03f,0,ConsoleLampAt(0).Y,.3f,.315f,.004f);Panel(s,Black,1.16f,.08f,.02f,0,ConsoleLampAt(0).Y,.315f,.32f,.002f);
            s.Into(Lights);s.Box(Lamp(Cool,.8f),V(0,.13f,.41f),V(1.7f,.012f,.008f));
        });
        /// <summary>A readiness lamp (its socket face at the origin facing -z): a chrome bezel and the lamp (bone 1, tinted).</summary>
        public static WeaponMesh Lamp()
        {
            var mesh=new WeaponMesh{Spec="deploy-lamp",Tier=0};var s=new WeaponSculpt(mesh,0);s.Push(WeaponSculpt.S(1,1,-1));
            s.Into(Body);s.Cylinder(Chrome,.05f,0,.025f,20,.006f);s.Into(Lights);s.Sphere(Lamp(0xffffff),V(0,0,.025f),V(.04f,.04f,.03f),4,16);s.Pop();return mesh;
        }

        // ---- the ROOM: floor plates, the back wall round the gate, side walls with gantries, ceiling beams and light bars ------
        public static WeaponMesh StationRoom()
        {
            var mesh=new WeaponMesh{Spec="deploy-room",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            const float x0=-11,x1=11,z0=-12,z1=9.6f,ceil=7.4f;
            // the floor: a dark bed and 2 m plates on it (seams between), checkered in two tones, tread plates on the walkway
            s.Box(Seam,V(0,-.04f,(z0+z1)/2),V(x1-x0+1,.04f,z1-z0+1));
            for(float x=x0;x<x1-.01f;x+=2)for(float z=z0;z<z1-.01f;z+=2)
            {
                float cx=x+1,cz=z+1;bool walk=Math.Abs(cx)<1.6f&&cz>3.4f;var paint=walk?SteelDark:(((int)(x/2)+(int)(z/2))&1)==0?Plate:PlateDark;
                s.Box(paint,V(cx,-.012f,cz),V(1.96f,.024f,Math.Min(1.96f,z1-z-.04f)),.006f);
                if(walk)for(int k=0;k<6;k++)s.Box(Steel,V(cx-.7f+k*.28f,.002f,cz),V(.05f,.006f,1.7f),.002f);
                else{s.Box(SteelDark,V(cx-.86f,.001f,cz-.86f),V(.05f,.004f,.05f));s.Box(SteelDark,V(cx+.86f,.001f,cz+.86f),V(.05f,.004f,.05f));}
            }
            // the table's inlay ring and spokes toward the pads (bone FloorRing), the walkway's edge lights to the gate (Walkway)
            var t=NativeDeployPresentation.Table;
            s.Into(FloorRing);FlatRing(s,Lamp(Cool,.7f),2.3f,2.36f,.003f,96,t.X,t.Z);FlatRing(s,Lamp(Cool,.35f),2.5f,2.52f,.003f,96,t.X,t.Z);
            for(int i=0;i<NativeDeployPresentation.Pads;i++){var p=NativeDeployPresentation.Pad(i);var d=Vector3.Normalize(p-t);var a=t+d*2.55f;var b=p-d*.62f;var m=(a+b)*.5f;s.Box(Lamp(Cool,.45f),V(m.X,.003f,m.Z),V(.025f,.002f,Vector3.Distance(a,b)),0,0,(float)Math.Atan2(d.X,d.Z),0);}
            s.Into(Walkway);foreach(int side in new[]{-1,1})s.Box(Lamp(White,.8f),V(side*1.62f,.004f,6.5f),V(.04f,.006f,6.0f));
            for(int k=0;k<6;k++){s.Push(WeaponSculpt.T(0,.004f,4.2f+k*.9f));s.Box(Lamp(White,.5f),V(-.3f,0,0),V(.5f,.004f,.06f),0,0,.7f,0);s.Box(Lamp(White,.5f),V(.3f,0,0),V(.5f,.004f,.06f),0,0,-.7f,0);s.Pop();}
            s.Into(Body);
            // the back wall round the gate: panels on ribs, a light band, DEPLOYMENT STATION lettering, pipes along the top
            const float gw=3.3f;
            foreach(int side in new[]{-1,1})
            {
                float cx=side*(gw+(x1-gw)/2);s.Box(Wall,V(cx,ceil/2,z1+.3f),V(x1-gw,ceil,.6f));
                for(float x=gw+1.2f;x<x1-.2f;x+=2.4f){s.Box(WallLight,V(side*x,ceil/2,z1-.02f),V(.3f,ceil,.12f),.02f);s.Into(Lights);s.Box(Lamp(Cool,.7f),V(side*x,ceil/2-.6f,z1-.085f),V(.03f,ceil-2.4f,.01f));s.Into(Body);}
                {var m=WallMapAt(side);s.Box(GraphiteDark,V(m.X,m.Y,z1-.04f),V(WallMapWidth+.34f,WallMapHeight+.34f,.08f),.02f);s.Box(Black,V(m.X,m.Y,z1-.085f),V(WallMapWidth+.06f,WallMapHeight+.06f,.01f));
                 s.Box(Brass,V(m.X,m.Y+WallMapHeight/2+.12f,z1-.09f),V(WallMapWidth+.2f,.03f,.02f),.006f);s.Box(Brass,V(m.X,m.Y-WallMapHeight/2-.12f,z1-.09f),V(WallMapWidth+.2f,.03f,.02f),.006f);
                 s.Into(Lights);foreach(int k in new[]{-1,1})s.Box(Lamp(Cool),V(m.X+k*(WallMapWidth/2+.13f),m.Y,z1-.09f),V(.02f,WallMapHeight,.01f));Letters(s,Lamp(Ice,.8f),side<0?"SECTOR SCAN":"ROUTE PLAN",m.X,m.Y+WallMapHeight/2+.32f,z1-.1f,.032f,.01f);s.Into(Body);}
                for(float x=gw+.6f;x<x1-1;x+=2.4f)for(int r=0;r<3;r++)Panel2(s,side*(x+.6f),1.4f+r*1.9f,z1+.0f,1.9f,1.6f);
                s.Into(Lights);s.Box(Lamp(Cool,.9f),V(cx,5.95f,z1-.08f),V(x1-gw-.4f,.05f,.02f));s.Box(Lamp(Warm,.6f),V(cx,.32f,z1-.08f),V(x1-gw-.4f,.03f,.02f));s.Into(Body);
                foreach(float y in new[]{6.45f,6.7f}){s.Push(WeaponSculpt.T(0,y,z1-.25f));s.Tube(SteelDark,new[]{V(side*gw,0,0),V(side*x1,0,0)},.08f,12,Vector3.UnitY);s.Pop();}
            }
            s.Box(Wall,V(0,(5.6f+ceil)/2,z1+.3f),V(2*gw,ceil-5.6f,.6f));
            s.Into(Lights);Letters(s,Lamp(Ice,.8f),"DEPLOYMENT STATION",0,6.3f,z1-.06f,.09f,.02f);s.Into(Body);
            // side walls with ribs and panels, the gantry (deck, railings, brackets, under-lights) and a stair at the front
            foreach(int side in new[]{-1,1})
            {
                float wx=side*x1;s.Box(Wall,V(wx+side*.3f,ceil/2,(z0+z1)/2),V(.6f,ceil,z1-z0+.6f));
                for(float z=z0+1;z<z1;z+=3)
                {
                    s.Box(WallLight,V(wx-side*.08f,ceil/2,z),V(.18f,ceil,.4f),.02f);s.Into(Lights);s.Box(Lamp(Cool,.6f),V(wx-side*.175f,1.6f,z),V(.01f,2.6f,.03f));s.Into(Body);
                    Panel3(s,side,wx,1.6f,z+1.5f);Panel3(s,side,wx,5.6f,z+1.5f);
                }
                const float gy=3.5f,gd=1.5f;float gx=wx-side*gd/2;
                const float g0=-6.7f;s.Box(SteelDark,V(gx,gy,(g0+z1)/2),V(gd,.12f,z1-g0-.3f),.02f);
                for(float z=g0+.3f;z<z1-.4f;z+=1.5f)
                {
                    Column(s,Steel,.025f,wx-side*(gd-.06f),z,gy,gy+1.05f,8,.004f);
                    s.Push(WeaponSculpt.T(wx-side*gd/2,gy-.5f,z));s.Box(SteelDark,V(0,0,0),V(.08f,1.3f,.08f),.01f,0,0,side*.85f);s.Pop();
                }
                foreach(float y in new[]{gy+1.05f,gy+.55f}){s.Push(WeaponSculpt.T(wx-side*(gd-.06f),y,0));s.Tube(Steel,new[]{V(0,0,g0+.2f),V(0,0,z1-.4f)},.022f,8,Vector3.UnitY);s.Pop();}
                s.Box(Hazard,V(wx-side*(gd-.02f),gy+.07f,(g0+z1)/2),V(.04f,.03f,z1-g0-.3f));
                s.Into(Lights);s.Box(Lamp(Cool,.9f),V(wx-side*(gd-.1f),gy-.08f,(g0+z1)/2),V(.03f,.02f,z1-g0-.8f));for(float z=z0+1.5f;z<z1-1;z+=3)s.Box(Lamp(Warm,.7f),V(wx-side*.14f,gy+.8f,z),V(.02f,.4f,.1f));s.Into(Body);
                // the stair: a stringer and steps from the floor to the gantry, against the wall at the room's front
                for(int k=0;k<14;k++){float y=.12f+k*.25f,z=-11.0f+k*.32f;s.Box(SteelDark,V(wx-side*.75f,y,z),V(1.2f,.05f,.3f),.008f);}
                s.Push(WeaponSculpt.T(wx-side*1.38f,0,0));s.Tube(Steel,new[]{V(0,.9f,-11.2f),V(0,4.4f,-6.6f)},.025f,8,Vector3.UnitY);s.Pop();
            }
            // the ceiling: a dark deck, I-beams across every 3 m, two rows of light bars hung between them
            s.Box(GraphiteDark,V(0,ceil+.2f,(z0+z1)/2),V(x1-x0+1,.4f,z1-z0+1));
            for(float z=z0+1.5f;z<z1;z+=3)
            {
                s.Box(SteelDark,V(0,ceil-.04f,z),V(x1-x0,.08f,.36f));s.Box(SteelDark,V(0,ceil-.36f,z),V(x1-x0,.56f,.06f));s.Box(SteelDark,V(0,ceil-.66f,z),V(x1-x0,.08f,.36f));
            }
            for(float z=z0+3;z<z1-3f;z+=3)foreach(float x in new[]{-5.5f,0f,5.5f})
            {
                s.Box(Graphite,V(x,ceil-.92f,z),V(3.4f,.12f,.3f),.02f);foreach(int k in new[]{-1,1})Column(s,SteelDark,.012f,x+k*1.4f,z,ceil-.86f,ceil-.6f,6);
                s.Into(Lights);s.Box(Lamp(Ice),V(x,ceil-.985f,z),V(3.2f,.012f,.2f));s.Into(Body);
            }
            return mesh;
        }
        /// <summary>The back wall's two map screens (station frame, facing -z): left (-1) and right (+1) of the gate.</summary>
        public static Vector3 WallMapAt(int side)=>new Vector3(side*6.4f,3.3f,9.5f);public const float WallMapWidth=3.0f,WallMapHeight=WallMapWidth*19f/25f;
        // A recessed wall panel facing -z at depth z (the back wall).
        private static void Panel2(WeaponSculpt s,float cx,float cy,float z,float w,float h)
        {s.Box(WallLight,V(cx,cy,z-.02f),V(w,h,.04f),.015f);s.Box(Wall,V(cx,cy,z-.045f),V(w-.16f,h-.16f,.02f),.008f);s.Box(Seam,V(cx,cy+h*.3f,z-.06f),V(w-.4f,.03f,.01f));}
        // A side wall panel (facing the room) on the wall at x.
        private static void Panel3(WeaponSculpt s,int side,float wx,float cy,float cz)
        {s.Box(WallLight,V(wx-side*.02f,cy,cz),V(.04f,2.6f,2.4f),.015f);s.Box(Wall,V(wx-side*.045f,cy,cz),V(.02f,2.4f,2.2f),.008f);s.Box(Seam,V(wx-side*.06f,cy-.6f,cz),V(.01f,.03f,1.8f));}

        // ---- the station's light (practical, NativeEntranceLights in the station frame: Runtime mirrors x into the stage block) --
        /// <summary>
        /// The station's lights for one frame (NativeLighting.Entrance's units: three's colour x intensity, linear): a dark navy
        /// backdrop and fog, a cool trilight ambient, a soft key from the front above, and eight practical point lights (the source frame, x mirrored, as NativeLighting.Entrance reads them): each
        /// machine's own (warm amber for the instance machine at `instancePower`, cool violet for the mutator at `mutatorPower`,
        /// flickering with `flicker`), the holo table, the gate (brightening as it opens), two ceiling fills, the party's key and
        /// the console (`console` 0..1: the DEPLOY button's level). `dim` (Deploying's Dim) lowers the room for the gate.
        /// </summary>
        public static void StationLights(NativeEntranceLights l,float instancePower,float mutatorPower,float flicker,float table,float gate,float console,float dim)
        {
            l.Clear();l.Background=NativeEntranceFx.Hex(0x04060b);l.Fog=NativeEntranceFx.Hex(0x080b14);l.FogNear=16;l.FogFar=44;
            float room=1-.55f*dim;
            l.Sky=NativeEntranceFx.Hex(0x86a6dc,1.5f*room);l.Ground=NativeEntranceFx.Hex(0x221c2e,1.1f*room);
            l.Directional(Source(new Vector3(-.35f,1,-.55f)),NativeEntranceFx.Hex(0xd6e2ff,1.1f*room));
            var im=NativeDeployPresentation.InstanceMachineAt+Facing(NativeDeployPresentation.InstanceYaw)*1.4f+new Vector3(0,2.6f,0);
            var mm=NativeDeployPresentation.MutatorMachineAt+Facing(NativeDeployPresentation.MutatorYaw)*1.4f+new Vector3(0,2.4f,0);
            l.Add(Source(im),NativeEntranceFx.Hex(0xffa850,70*instancePower),9);
            l.Add(Source(mm),NativeEntranceFx.Hex(0x9d8cff,60*mutatorPower*flicker),9);
            l.Add(Source(NativeDeployPresentation.Table+new Vector3(0,1.9f,0)),NativeEntranceFx.Hex(0x48d4ff,30*table),5.5f);
            l.Add(Source(NativeDeployPresentation.Gate+new Vector3(0,4.4f,-1.8f)),NativeEntranceFx.Hex(0xcfe6ff,70+320*gate),14);
            l.Add(Source(new Vector3(-5.5f,6.2f,1)),NativeEntranceFx.Hex(0xbcd0ff,150*room),16);
            l.Add(Source(new Vector3(5.5f,6.2f,1)),NativeEntranceFx.Hex(0xbcd0ff,150*room),16);
            l.Add(Source(new Vector3(0,5.6f,-6.2f)),NativeEntranceFx.Hex(0xe6edff,140*room),15);
            l.Add(Source(ConsoleAt+new Vector3(0,1.6f,-.6f)),NativeEntranceFx.Hex(0x7dffb0,6+30*console),4.5f);
        }
        /// <summary>A station point in the source frame NativeEntranceLights is read in (x mirrored; NativeLighting mirrors it back).</summary>
        public static Vector3 Source(Vector3 station)=>new Vector3(-station.X,station.Y,station.Z);
    }
}
