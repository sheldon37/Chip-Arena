using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The hub's REVIVE-O-MATIC (docs/REVIVE-MACHINE.md), sculpted with the Standard weapons' kit like the PvE machines and the
    // Deployment Station: a pot-bellied riveted boiler on coil springs that happens to have a face. Two pressure gauges for eyes, steel
    // brows, a chrome-toothed grille for a grin, a bulb horn for a nose, bellows for cheeks; a flywheel and side rod on its left, a stack
    // with a cap that pops off, a marquee on top. Engine-free and deterministic: the checks build what the player builds.
    //
    // Authored in the model frame the PvE machines use: origin on the floor at the spot, +z the front (the way it spits), +y up. A viewer
    // facing it sees +x on their left, so lettering is laid out mirrored (Word). Everything that shakes is on the Hull; what stays on the floor
    // is the Skid, the Console, the Mat and the Lane. Runtime/NativeReviveMachinePresentation.cs poses the moving parts from
    // Core/NativeReviveMachine.cs. Section bones, the bank's convention: 0 lit, 1 lights the draw tints, 2 and 3 the marquee's two bulb
    // banks, 4 the gullet's heat, -2 see-through glass.
    public static class NativeReviveMachineModels
    {
        public const int Body=0,Lights=1,BulbsA=2,BulbsB=3,Gullet=4,Glass=-2;
        public const string Skid="revive-machine-skid",Spring="revive-machine-spring",Hull="revive-machine-hull",Jaw="revive-machine-jaw",Visor="revive-machine-visor",Tongue="revive-machine-tongue",TongueTip="revive-machine-tongue-tip",
            Flywheel="revive-machine-flywheel",Rod="revive-machine-rod",Piston="revive-machine-piston",Needle="revive-machine-needle",Brow="revive-machine-brow",Cheek="revive-machine-cheek",Cap="revive-machine-cap",
            Bulb="revive-machine-bulb",Console="revive-machine-console",Lever="revive-machine-lever",Mat="revive-machine-mat",Lane="revive-machine-lane",Coin="revive-machine-coin",Bolt="revive-machine-bolt";
        /// <summary>Every model in the bank (id and builder), in draw order.</summary>
        public static readonly (string id,Func<WeaponMesh> build,int budget)[] All=
        {
            (Skid,BuildSkid,9000),(Spring,BuildSpring,2400),(Hull,BuildHull,70000),(Jaw,BuildJaw,3500),(Visor,BuildVisor,3500),(Tongue,BuildTongue,500),(TongueTip,BuildTongueTip,600),(Flywheel,BuildFlywheel,3500),
            (Rod,BuildRod,900),(Piston,BuildPiston,500),(Needle,BuildNeedle,200),(Brow,BuildBrow,600),(Cheek,BuildCheek,2600),(Cap,BuildCap,700),(Bulb,BuildBulb,500),(Console,BuildConsole,15000),(Lever,BuildLever,500),
            (Mat,BuildMat,3000),(Lane,BuildLane,3000),(Coin,BuildCoin,120),(Bolt,BuildBolt,120),
        };

        // ---- the parts' pivots in the machine's frame (model space, at rest) ----
        /// <summary>The height of the hull's floor: what the springs hold up.</summary>
        public const float SpringFrom=.2f,SpringTo=.64f;
        public static readonly Vector3[] SpringAt={new Vector3(1.02f,0,.62f),new Vector3(-1.02f,0,.62f),new Vector3(1.02f,0,-.86f),new Vector3(-1.02f,0,-.86f)};
        public static readonly Vector3 HullPivot=new Vector3(0,.64f,-.1f),JawHinge=new Vector3(0,MouthBottom,1.41f),VisorHinge=new Vector3(0,MouthTop,1.41f),TongueRoot=new Vector3(0,MouthBottom+.1f,.8f);
        /// <summary>The mouth's opening (model space): its bottom and top, its middle where the teeth meet, its width and corner.</summary>
        public const float MouthBottom=.88f,MouthTop=1.76f,MouthMid=(MouthBottom+MouthTop)/2,MouthWidth=1.46f,MouthCorner=.3f;
        public const float TongueLink=.27f;public const int TongueLinks=8;
        public static readonly Vector3 WheelAxle=new Vector3(1.68f,1.3f,-.78f);public const float CrankRadius=.32f,RodLength=1.2f,RodX=1.92f,WheelRadius=.78f;
        /// <summary>The eyes and brows, seen from the front: 0 on the viewer's left (+x).</summary>
        public static readonly Vector3[] EyeAt={new Vector3(.45f,2.52f,.755f),new Vector3(-.45f,2.52f,.755f)},BrowAt={new Vector3(.45f,2.9f,.74f),new Vector3(-.45f,2.9f,.74f)};
        public static readonly Vector3[] CheekAt={new Vector3(1.04f,1.74f,.3f),new Vector3(-1.04f,1.74f,.3f)};
        public static readonly Vector3 CapAt=new Vector3(StackX,StackTop,StackZ),BulbAt=new Vector3(0,2.16f,.72f);
        public const float StackX=.74f,StackZ=-.52f,StackTop=5.02f;
        /// <summary>Where the console stands (machine frame), its turn about y (radians) and the lever's hub on it (console frame).</summary>
        public static readonly Vector3 ConsoleAt=new Vector3(-2.75f,0,1.25f);public const float ConsoleYaw=-.32f;public static readonly Vector3 LeverAt=new Vector3(-.32f,1.02f,.02f);
        /// <summary>The mat's centre on the floor and the lane's reach (machine frame).</summary>
        public static readonly Vector3 MatAt=new Vector3(0,0,6.1f);public const float MatSize=3.6f;
        /// <summary>The glass money tube: from the console's coin funnel up and over into the hopper (machine frame).</summary>
        public static readonly Vector3[] CoinPath={new Vector3(-2.75f,1.62f,.86f),new Vector3(-2.72f,2.5f,.82f),new Vector3(-2.5f,3.2f,.6f),new Vector3(-2.02f,3.62f,.28f),new Vector3(-1.5f,3.72f,-.02f),new Vector3(-1.14f,3.66f,-.26f),new Vector3(-.92f,3.42f,-.38f)};
        public static readonly Vector3 HopperAt=new Vector3(-.92f,2.9f,-.38f);
        /// <summary>Steam leaves here: the two ears, the stack's collar, the belly's drain cocks, the cheeks' relief valves.</summary>
        public static readonly Vector3[] Vents={new Vector3(1.2f,2.72f,-.1f),new Vector3(-1.2f,2.72f,-.1f),new Vector3(StackX+.34f,3.02f,StackZ),new Vector3(StackX-.34f,3.02f,StackZ),new Vector3(.72f,.74f,1.46f),new Vector3(-.72f,.74f,1.46f),new Vector3(1.58f,1.74f,.3f),new Vector3(-1.58f,1.74f,.3f)};

        // ---- paints ----
        private static readonly WeaponPaint Enamel=WeaponPaint.Solid(0x2e7276,.04f),EnamelDark=WeaponPaint.Solid(0x1f5457,.03f),EnamelPale=WeaponPaint.Solid(0x3d8a8c,.05f),Cream=WeaponPaint.Solid(0xd8cfae,.03f),CreamDirty=WeaponPaint.Solid(0xa89e7f,.02f),
            Graphite=WeaponPaint.Solid(0x2a2d35,.03f),GraphiteLight=WeaponPaint.Solid(0x3d414c,.04f),GraphiteDark=WeaponPaint.Solid(0x17191e,.02f),Brass=WeaponPaint.Solid(0xc0913f,.1f),BrassDark=WeaponPaint.Solid(0x7d5b27,.06f),BrassBright=WeaponPaint.Solid(0xe8c070,.14f),
            Copper=WeaponPaint.Solid(0xb5683a,.09f),CopperDark=WeaponPaint.Solid(0x7a4426,.05f),Steel=WeaponPaint.Solid(0xa3aab6,.06f),SteelDark=WeaponPaint.Solid(0x5c6270,.04f),Chrome=WeaponPaint.Solid(0xd6dce6,.16f),Black=WeaponPaint.Solid(0x07080b),Rubber=WeaponPaint.Solid(0x121216),
            Seam=WeaponPaint.Solid(0x08090c),Red=WeaponPaint.Solid(0xb3261e,.08f),RedDark=WeaponPaint.Solid(0x761612,.05f),TongueRed=WeaponPaint.Solid(0xc23a3f,.05f),TongueDark=WeaponPaint.Solid(0x8e2429,.04f),Hazard=WeaponPaint.Solid(0xd9a21f,.05f),Ink=WeaponPaint.Solid(0x101014),
            Rust=WeaponPaint.Solid(0x5e3a22,.01f),Gold=WeaponPaint.Solid(0xe6b84a,.22f),MatBlue=WeaponPaint.Solid(0x27508e,.02f),MatBlueDark=WeaponPaint.Solid(0x1c3b6c,.015f),MatRed=WeaponPaint.Solid(0xb5302a,.02f),MatWhite=WeaponPaint.Solid(0xd8d8d0,.02f),Tape=WeaponPaint.Solid(0x7c8084,.02f);
        /// <summary>An unlit light: its colour is its emission, the draw's tint its level.</summary>
        private static WeaponPaint Lamp(int hex,float k=1){var c=WeaponPaint.Linear(hex)*k;return new WeaponPaint(c,c,0);}
        private const int Warm=0xffb45a,Ivory=0xffe2b0,Amber=0xff9a2e,Mint=0x5dffa8,DangerRed=0xff3a3a,Ember=0xff6a1e,Screen=0x8dffb0;
        /// <summary>Alpha of the see-through parts.</summary>
        public const float GlassAlpha=.16f,DomeAlpha=.14f;
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Up=-(float)(Math.PI/2),Pi=(float)Math.PI;

        // ---- shared pieces ----
        private static WeaponMesh New(string id,Action<WeaponSculpt,WeaponMesh> draw){var mesh=new WeaponMesh{Spec=id,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);draw(s,mesh);return mesh;}
        // A body of revolution about +y at (x, z): (radius, y) pairs in increasing y.
        private static void Turned(WeaponSculpt s,in WeaponPaint p,float x,float z,float[] ry,int sides,int smooth=1,bool caps=true)
        {var q=p;s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));s.Lathe(q,ry,sides,smooth,0,caps);s.Pop();}
        private static void Column(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides,float bevel=0)
        {s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));s.Cylinder(c,r,y0,y1,sides,bevel);s.Pop();}
        // A cylinder along +x from x0 to x1 through (y, z).
        private static void Axle(WeaponSculpt s,in WeaponPaint c,float r,float y,float z,float x0,float x1,int sides,float bevel=0)
        {s.Push(WeaponSculpt.Ry(Pi/2)*WeaponSculpt.T(0,y,z));s.Cylinder(c,r,x0,x1,sides,bevel);s.Pop();}
        private static void Panel(WeaponSculpt s,in WeaponPaint p,float w,float h,float corner,float cx,float cy,float z0,float z1,float bevel)
        {var q=p;s.Slab(_=>q,q,q,WeaponSculpt.Rect(w,h,corner,0,cx,cy),z0,z1,bevel);}
        private static void Rivet(WeaponSculpt s,in WeaponPaint p,Vector3 at,float r=.014f)=>s.Sphere(p,at,V(r,r,r),2,6);
        // Rivets round a circle about +z, on its rim at depth z.
        private static void RivetRing(WeaponSculpt s,in WeaponPaint p,float cx,float cy,float radius,float z,int count,float r=.015f,float phase=0)
        {for(int i=0;i<count;i++){double a=phase+i*Math.PI*2/count;Rivet(s,p,V(cx+radius*(float)Math.Cos(a),cy+radius*(float)Math.Sin(a),z),r);}}
        // A row of rivets on a flat face at depth z.
        private static void RivetRow(WeaponSculpt s,in WeaponPaint p,float x0,float y0,float x1,float y1,float z,int count,float r=.012f)
        {for(int i=0;i<count;i++){float k=count==1?.5f:i/(float)(count-1);Rivet(s,p,V(x0+(x1-x0)*k,y0+(y1-y0)*k,z),r);}}
        // Lettering (the Deployment Station's chamfered capitals) facing +z, centred on (cx, cy), `height` tall: each stroke a bar, laid
        // out toward -x so it reads from the front. A dollar sign is an S with a bar through it.
        private static void Word(WeaponSculpt s,in WeaponPaint p,string text,float cx,float cy,float z,float height,float depth)
        {
            float k=height/NativeDeployArt.GlyphHeight,x=cx+NativeDeployArt.TextWidth(text.Replace('$','S'))*k/2,top=cy+height/2,w=height*.11f;var paint=p;
            foreach(char c in text)
            {
                char g=c=='$'?'S':c;
                foreach(var (x0,y0,x1,y1) in NativeDeployArt.Strokes(g))
                {
                    float ax=x-x0*k,ay=top-y0*k,bx=x-x1*k,by=top-y1*k,dx=bx-ax,dy=by-ay,len=(float)Math.Sqrt(dx*dx+dy*dy);
                    s.Box(paint,V((ax+bx)/2,(ay+by)/2,z+depth/2),V(len+w,w,depth),0,0,0,(float)Math.Atan2(dy,dx));
                }
                if(c=='$'){float mid=x-NativeDeployArt.Advance('S')*k*.39f;s.Box(paint,V(mid,cy,z+depth/2),V(w,height*1.22f,depth));}
                x-=NativeDeployArt.Advance(g)*k;
            }
        }
        // The same words on the far side of a sign, turned about y through (0, z).
        private static void WordBack(WeaponSculpt s,in WeaponPaint p,string text,float cx,float cy,float z,float height,float depth)
        {s.Push(WeaponSculpt.Ry(Pi));Word(s,p,text,-cx,cy,-z,height,depth);s.Pop();}
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
        // Writes `draw` into the see-through section with `alpha` (NativeSourceTransparent reads the colour alpha).
        private static void Clear(WeaponSculpt s,WeaponMesh mesh,float alpha,Action draw)
        {
            var section=mesh.Section(Glass,WeaponStretch.None);int from=section.Colors.Count;s.Into(Glass);draw();
            for(int i=from;i<section.Colors.Count;i++){var c=section.Colors[i];section.Colors[i]=new Vector4(c.X,c.Y,c.Z,alpha);}
            s.Into(Body);
        }
        // A red handwheel valve facing +z at a pipe.
        private static void Handwheel(WeaponSculpt s,float x,float y,float z,float r)
        {
            s.Cylinder(BrassDark,r*.28f,z-.05f,z+.01f,10,.004f,x,y);s.Torus(Red,V(x,y,z+.02f),r,r*.13f,18,6);
            for(int k=0;k<3;k++){s.Box(Red,V(x,y,z+.02f),V(r*2,r*.18f,r*.14f),0,0,0,k*Pi/3);}
            s.Cylinder(RedDark,r*.22f,z+.01f,z+.045f,10,.004f,x,y);
        }
        // A flanged pipe through its points.
        private static void Pipe(WeaponSculpt s,in WeaponPaint p,float r,params Vector3[] points)
        {
            var smooth=new List<Vector3>();
            for(int i=0;i<points.Length-1;i++){for(int k=0;k<8;k++){float t=k/8f;smooth.Add(Catmull(points,i,t));}}smooth.Add(points[points.Length-1]);
            s.Tube(p,smooth,r,10,Vector3.UnitY);
        }
        private static Vector3 Catmull(Vector3[] p,int i,float t)
        {
            Vector3 a=p[Math.Max(0,i-1)],b=p[i],c=p[i+1],d=p[Math.Min(p.Length-1,i+2)];float t2=t*t,t3=t2*t;
            return .5f*((2*b)+(c-a)*t+(2*a-5*b+4*c-d)*t2+(3*b-a-3*c+d)*t3);
        }
        private static void Flange(WeaponSculpt s,in WeaponPaint p,Vector3 at,Vector3 along,float r,float thick)
        {
            // A disc across a pipe running along `along`.
            var q=p;var d=Vector3.Normalize(along);var path=new[]{at-d*thick/2,at+d*thick/2};var up=Math.Abs(d.Y)>.9f?Vector3.UnitX:Vector3.UnitY;
            s.Tube(q,path,r,10,up);
        }

        // ---- the skid: what stays on the floor ----------------------------------------------------------------------------------
        private static WeaponMesh BuildSkid()=>New(Skid,(s,mesh)=>
        {
            s.Box(GraphiteDark,V(0,.1f,-.08f),V(3.5f,.2f,3.3f),.035f);s.Box(Rubber,V(0,.012f,-.08f),V(3.42f,.024f,3.22f),.004f);
            // The brass kick rail, the deck plate and its grip pattern.
            s.Box(Brass,V(0,.215f,1.58f),V(3.46f,.03f,.05f),.01f);s.Box(BrassDark,V(0,.215f,-1.74f),V(3.46f,.03f,.05f),.008f);
            foreach(int side in new[]{-1,1})s.Box(BrassDark,V(side*1.76f,.215f,-.08f),V(.05f,.03f,3.3f),.008f);
            s.Box(Graphite,V(0,.206f,-.08f),V(3.3f,.012f,3.1f),.003f);
            for(int i=0;i<9;i++)for(int k=0;k<8;k++)s.Box(GraphiteLight,V(-1.5f+i*.375f,.216f,-1.4f+k*.38f),V(.1f,.006f,.022f),0,0,(i+k)%2==0?.78f:-.78f);
            // Hazard stripes along the front edge, feet at the corners, bolted down.
            Stripes(s,3.4f,.15f,0,.1f,1.57f,.17f,.012f);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                var c=V(sx*1.72f,0,-.08f+sz*1.62f);s.Box(SteelDark,V(c.X,.03f,c.Z),V(.36f,.06f,.36f),.012f);
                foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1}){Column(s,Steel,.022f,c.X+a*.12f,c.Z+b*.12f,.06f,.082f,6,.004f);}
            }
            // The spring cups.
            foreach(var at in SpringAt){Column(s,Steel,.27f,at.X,at.Z,.2f,.25f,20,.012f);Column(s,SteelDark,.2f,at.X,at.Z,.25f,.262f,20,.003f);}
            // What lives on the deck: an oil can, a bucket under the drip, a hung wrench's stand, the drain cocks' tray.
            Turned(s,Red,1.32f,1.28f,new[]{.1f,.22f,.11f,.3f,.05f,.34f,.018f,.4f,.018f,.46f},12);s.Push(WeaponSculpt.Rx(.5f)*WeaponSculpt.T(1.32f,.42f,1.28f));s.Box(Red,V(0,0,.09f),V(.018f,.018f,.2f));s.Pop();
            Turned(s,SteelDark,-1.34f,1.2f,new[]{.12f,.22f,.15f,.42f},14,1,true);Column(s,Black,.135f,-1.34f,1.2f,.415f,.42f,14);s.Push(WeaponSculpt.T(-1.34f,.42f,1.2f));s.Torus(Steel,V(0,0,0),.15f,.008f,12,5,0,Pi);s.Pop();
            s.Box(Rust,V(-1.34f,.206f,1.2f),V(.46f,.004f,.42f));
            // The side plate's maker's badge on the skid's front corner, its name too small to read from a distance.
            s.Box(Brass,V(1.16f,.11f,1.585f),V(.5f,.11f,.006f),.002f);Word(s,Ink,"REVIVE-O-MATIC",1.16f,.11f,1.588f,.03f,.002f);
        });
        // A coil spring from y 0 to y 1 about the origin (scaled in y as it squashes).
        private static WeaponMesh BuildSpring()=>New(Spring,(s,mesh)=>
        {
            var path=new List<Vector3>();const int turns=5,steps=14;for(int i=0;i<=turns*steps;i++){float a=i*(float)Math.PI*2/steps;path.Add(V((float)Math.Cos(a)*.2f,.03f+.94f*i/(turns*steps),(float)Math.Sin(a)*.2f));}
            s.Tube(Hazard,path,.036f,8,Vector3.UnitY);
        });

        // ---- the hull: everything that shakes ------------------------------------------------------------------------------------
        private const float BoilerY=1.92f,BoilerR=1.12f,BoilerBack=-1f,BoilerFront=.62f;
        private static WeaponMesh BuildHull()=>New(Hull,(s,mesh)=>
        {
            // The tray the boiler sits in, its saddles, and the undercarriage the springs push on.
            s.Box(Graphite,V(0,.72f,-.12f),V(2.52f,.16f,2.72f),.03f);s.Box(BrassDark,V(0,.805f,1.2f),V(2.3f,.02f,.05f),.006f);
            foreach(float z in new[]{-.78f,.3f})s.Box(GraphiteLight,V(0,.97f,z),V(2.36f,.36f,.3f),.03f);
            foreach(var at in SpringAt){Column(s,Steel,.27f,at.X,at.Z,.6f,.64f,20,.012f);}
            // The boiler: teal enamel over riveted plate, brass hoops; a darker patch riveted on where something once came through.
            s.Cylinder(Enamel,BoilerR,BoilerBack,BoilerFront,48,.03f,0,BoilerY,1);
            foreach(float z in new[]{-.88f,-.34f,.2f,.56f})
            {
                s.Cylinder(Brass,BoilerR+.032f,z-.04f,z+.04f,48,.012f,0,BoilerY,1);
                RivetRing(s,BrassBright,0,BoilerY,BoilerR+.034f,z,36,.011f);
            }
            // Longitudinal lap seams with rivets, on the shoulders.
            foreach(double a in new[]{.55,2.6,3.9,5.5})
            {
                float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);
                s.Push(WeaponSculpt.Rz((float)a-Pi/2)*WeaponSculpt.T(0,BoilerY,0));s.Box(EnamelDark,V(0,BoilerR+.002f,-.2f),V(.05f,.012f,1.56f));s.Pop();
                for(int k=0;k<12;k++){if(k%4==0)continue;Rivet(s,Steel,V(ca*(BoilerR+.012f),BoilerY+sa*(BoilerR+.012f),-.92f+k*.13f),.012f);}
            }
            // The patch, on the top right shoulder, with its own rivets and a run of rust; and duct tape, on the left.
            s.Push(WeaponSculpt.Rz(-.6f)*WeaponSculpt.T(0,BoilerY,0));s.Box(EnamelPale,V(0,BoilerR+.006f,-.62f),V(.46f,.014f,.34f),.004f);for(int i=0;i<4;i++)for(int k=0;k<2;k++){Rivet(s,Steel,V(-.2f+i*.133f,BoilerR+.016f,-.76f+k*.28f),.01f);}s.Box(Rust,V(.1f,BoilerR+.0145f,-.6f),V(.05f,.002f,.2f));s.Pop();
            s.Push(WeaponSculpt.Rz(.86f)*WeaponSculpt.T(0,BoilerY,0));s.Box(Tape,V(0,BoilerR+.006f,-.08f),V(.08f,.008f,.42f));s.Box(Tape,V(.03f,BoilerR+.006f,-.08f),V(.34f,.008f,.08f),0,0,.3f,0);s.Pop();
            // The rear head and its pressure tanks, strapped on, a control box: the machine still looks busy from behind.
            s.Cylinder(EnamelDark,BoilerR-.05f,BoilerBack-.05f,BoilerBack,48,.012f,0,BoilerY,1);RivetRing(s,Steel,0,BoilerY,BoilerR-.1f,BoilerBack-.052f,28,.012f);
            s.Push(WeaponSculpt.T(0,0,BoilerBack-.06f));Handwheel(s,0,BoilerY,0,.13f);s.Pop();
            foreach(int side in new[]{-1,1})
            {
                float x=side*.56f;Axle(s,Red,.17f,1.0f,-1.28f,x-.32f,x+.32f,16,.03f);
                foreach(float dx in new[]{-.2f,.2f}){s.Push(WeaponSculpt.Ry(Pi/2)*WeaponSculpt.T(x+dx,0,0));s.Torus(SteelDark,V(1.28f,1.0f,0),.178f,.012f,16,5);s.Pop();}
            }
            s.Box(GraphiteLight,V(0,2.25f,-1.1f),V(.72f,.44f,.14f),.02f);s.Box(Hazard,V(0,2.25f,-1.175f),V(.3f,.3f,.006f));s.Box(Ink,V(0,2.25f,-1.18f),V(.06f,.2f,.006f));
            // The face: a cream dish, a brass rim, dirty toward the edge.
            s.Cylinder(CreamDirty,BoilerR-.02f,BoilerFront,BoilerFront+.03f,48,.008f,0,BoilerY,1);s.Cylinder(Cream,BoilerR-.08f,BoilerFront+.03f,BoilerFront+.042f,48,.005f,0,BoilerY,1);
            s.Torus(BrassBright,V(0,BoilerY,BoilerFront+.03f),BoilerR-.015f,.024f,48,8);RivetRing(s,Brass,0,BoilerY,BoilerR-.05f,BoilerFront+.043f,32,.012f,.1f);
            Muzzle(s,mesh);Eyes(s,mesh);Sides(s,mesh);Top(s,mesh);Plumbing(s,mesh);
        });
        // A rounded rectangle, counter-clockwise from the right side's top, `seg` points to a corner.
        private static Vector3[] Stadium(float w,float h,float r,float z,float cx,float cy,int seg=6)
        {
            var o=new List<Vector3>();float hx=w/2-r,hy=h/2-r;var corners=new[]{(hx,hy,0.0),(-hx,hy,Math.PI/2),(-hx,-hy,Math.PI),(hx,-hy,Math.PI*1.5)};
            foreach(var (qx,qy,from) in corners)for(int i=0;i<=seg;i++){double a=from+i*(Math.PI/2)/seg;o.Add(V(cx+qx+r*(float)Math.Cos(a),cy+qy+r*(float)Math.Sin(a),z));}
            return o.ToArray();
        }
        // The mouth: a rounded heavy snout standing out of the face, a furnace at the back of its throat, rollers on its floor.
        private static void Muzzle(WeaponSculpt s,WeaponMesh mesh)
        {
            const float z0=.62f,z1=1.42f,zm=(z0+z1)/2,d=z1-z0,cy=MouthMid,ow=MouthWidth+.42f,oh=MouthTop-MouthBottom+.42f,or=MouthCorner+.2f,iw=MouthWidth,ih=MouthTop-MouthBottom,ir=MouthCorner;
            // The snout's shell, flaring a little toward the lip; the lip's brass tube; the liner inside it, ribbed.
            s.Loft(new[]{Stadium(ow-.1f,oh-.1f,or-.04f,z0,0,cy),Stadium(ow-.06f,oh-.06f,or-.03f,z0+.25f,0,cy),Stadium(ow,oh,or,z1-.1f,0,cy),Stadium(ow-.02f,oh-.02f,or-.01f,z1,0,cy)},(i,k)=>i==1?Enamel:EnamelDark,1,false,false);
            s.Loft(new[]{Stadium(ow-.02f,oh-.02f,or-.01f,z1,0,cy),Stadium(iw+.04f,ih+.04f,ir+.02f,z1+.012f,0,cy)},(i,k)=>EnamelDark,1,false,false);
            s.Loft(new[]{Stadium(iw,ih,ir,z0+.02f,0,cy),Stadium(iw,ih,ir,z1,0,cy)},(i,k)=>GraphiteDark,1,false,false,null,false,true);
            s.Tube(Brass,Stadium(iw+.07f,ih+.07f,ir+.03f,z1+.018f,0,cy),.032f,8,Vector3.UnitZ,1,false,true);
            s.Tube(BrassDark,Stadium(ow-.04f,oh-.04f,or-.02f,z1-.1f,0,cy),.018f,6,Vector3.UnitZ,1,false,true);
            foreach(float z in new[]{.84f,1.06f,1.26f})s.Tube(Black,Stadium(iw-.02f,ih-.02f,ir-.01f,z,0,cy),.018f,5,Vector3.UnitZ,1,false,true);
            var lip=Stadium(ow-.2f,oh-.2f,or-.1f,z1+.006f,0,cy,4);for(int i=0;i<lip.Length;i++)if(i%2==0)Rivet(s,BrassBright,lip[i],.015f);
            // Riveted straps over the snout's crown, down its cheeks; the corners of the frame bolted.
            float top=cy+oh/2;foreach(float x in new[]{-.5f,0,.5f}){s.Box(Brass,V(x,top-.002f,zm+.02f),V(.08f,.01f,d-.26f));RivetRow(s,BrassBright,x,top+.006f,x,top+.006f,zm-.2f,1,.013f);RivetRow(s,BrassBright,x,top+.006f,x,top+.006f,zm+.24f,1,.013f);}
            foreach(int side in new[]{-1,1}){s.Box(Brass,V(side*(ow/2-.002f),cy,zm+.04f),V(.01f,.34f,d-.3f));for(int k=0;k<3;k++)Rivet(s,BrassBright,V(side*(ow/2+.006f),cy-.11f+k*.11f,zm+.04f),.012f);}
            // The chin: a block under the snout down to the tray.
            s.Box(EnamelDark,V(0,.76f,1.04f),V(1.5f,.22f,.72f),.03f);
            // The throat's furnace: firebars glowing at the back, rollers along the floor.
            s.Into(Gullet);s.Box(Lamp(Ember,1.2f),V(0,cy,.668f),V(1.36f,.8f,.006f));s.Into(Body);
            for(int k=0;k<11;k++)s.Box(Black,V(-.6f+k*.12f,cy,.684f),V(.05f,.82f,.03f),.006f);
            s.Box(Black,V(0,cy+.34f,.69f),V(1.4f,.05f,.03f));s.Box(Black,V(0,cy-.34f,.69f),V(1.4f,.05f,.03f));
            for(int k=0;k<4;k++)Axle(s,k%2==0?Chrome:Steel,.045f,MouthBottom+.05f,.86f+k*.17f,-.62f,.62f,12,.006f);
            // Under the chin: the warranty plate and the three lamps.
            s.Box(Brass,V(0,.74f,1.405f),V(1.02f,.1f,.012f),.002f);Word(s,Ink,"WARRANTY VOID IF DEAD",0,.74f,1.411f,.042f,.002f);
            s.Into(Lights);foreach(var (x,c) in new[]{(.56f,Mint),(.62f,Amber),(.68f,DangerRed)}){s.Sphere(Lamp(c),V(x,.74f,1.408f),V(.022f,.022f,.014f),3,8);s.Sphere(Lamp(c),V(-x,.74f,1.408f),V(.022f,.022f,.014f),3,8);}s.Into(Body);
        }
        // The eyes: two big pressure gauges (their needles are their own model); the sockets for the brows; the nose's collar and horn.
        private static void Eyes(WeaponSculpt s,WeaponMesh mesh)
        {
            foreach(var e in EyeAt)
            {
                float x=e.X,y=e.Y,z=BoilerFront+.04f;
                s.Cylinder(Chrome,.32f,z,z+.07f,36,.016f,x,y);s.Cylinder(Black,.285f,z+.07f,z+.074f,36,0,x,y);s.Cylinder(Cream,.27f,z+.074f,z+.078f,36,0,x,y);
                // Ticks round the dial, the red end of the scale, little lettering's stand-in, the screws in the bezel.
                for(int k=0;k<=12;k++)
                {
                    double a=Math.PI*(-.25+k*(1.5/12));float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);bool red=k>=9;
                    s.Box(red?Red:Ink,V(x+ca*.215f,y+sa*.215f,z+.081f),V(k%3==0?.016f:.009f,k%3==0?.06f:.036f,.003f),0,0,0,(float)a-Pi/2);
                }
                for(int k=0;k<8;k++){double a=Math.PI*(-.25+(9+k*3f/7)*(1.5/12));float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);s.Box(Red,V(x+ca*.25f,y+sa*.25f,z+.0805f),V(.03f,.018f,.002f),0,0,0,(float)a-Pi/2);}
                Word(s,Ink,"PSI",x,y-.12f,z+.0795f,.036f,.002f);
                RivetRing(s,Steel,x,y,.302f,z+.07f,8,.009f);
                Clear(s,mesh,DomeAlpha,()=>{s.Sphere(WeaponPaint.Solid(0xdfefff,.2f),V(x,y,z+.082f),V(.272f,.272f,.05f),4,28);});
            }
            // The brow's mount, a steel bracket either side; the bulb horn's brass collar and its tube curling up over the left eye to its bell.
            foreach(var b in BrowAt){s.Box(SteelDark,V(b.X,b.Y+.02f,BoilerFront+.05f),V(.18f,.1f,.1f),.012f);}
            s.Cylinder(Brass,.078f,BoilerFront+.02f,BoilerFront+.09f,14,.012f,BulbAt.X,BulbAt.Y);
            Pipe(s,Brass,.022f,V(0,2.16f,.68f),V(.08f,2.2f,.72f),V(.12f,2.5f,.76f),V(.1f,2.86f,.72f),V(.42f,3.14f,.62f),V(.86f,3.3f,.54f),V(1.02f,3.34f,.5f));
            s.Push(WeaponSculpt.Rx(-.32f)*WeaponSculpt.T(1.02f,3.34f,.5f));s.Push(WeaponSculpt.Rx(Up));s.Lathe(BrassBright,new[]{.024f,0f,.04f,.08f,.1f,.14f,.17f,.17f},18,1,0,false);s.Pop();s.Pop();
        }
        // The flanks: the cheeks' mounts, the flywheel's bearing and slide bars, the steam cylinder, ears.
        private static void Sides(WeaponSculpt s,WeaponMesh mesh)
        {
            foreach(var c in CheekAt)
            {
                int side=Math.Sign(c.X);Axle(s,Brass,.44f,c.Y,c.Z,side>0?c.X-.06f:c.X,side>0?c.X:c.X+.06f,24,.012f);
                s.Box(SteelDark,V(side*1.07f,c.Y-.47f,c.Z),V(.12f,.16f,.18f),.015f);
                // Ears: brass relief trumpets high on each side.
                var v=V(side*1.0f,2.62f,-.1f);s.Push(WeaponSculpt.Rz(-side*(.95f))*WeaponSculpt.T(v.X,v.Y,v.Z));s.Push(WeaponSculpt.Rx(Up));s.Lathe(Brass,new[]{.05f,0f,.05f,.08f,.08f,.12f,.14f,.18f},16,1,0,false);s.Pop();s.Pop();
            }
            // Flywheel's bearing post, bolted to the hull's tray, reaching out past the boiler.
            var w=WheelAxle;s.Box(GraphiteLight,V(1.36f,1.02f,w.Z),V(.5f,.56f,.34f),.03f);s.Box(Graphite,V(1.45f,1.3f,w.Z),V(.3f,.3f,.3f),.04f);Axle(s,Steel,.075f,w.Y,w.Z,1.3f,1.66f,14,.01f);
            foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})Rivet(s,Steel,V(1.61f,1.02f+a*.2f,w.Z+b*.12f),.014f);
            // The drive motor on the tray, its belt up to the wheel's hub pulley.
            Axle(s,GraphiteDark,.17f,.98f,.1f,1.14f,1.56f,18,.02f);for(int k=0;k<6;k++)Axle(s,GraphiteLight,.178f,.98f,.1f,1.18f+k*.06f,1.2f+k*.06f,18);Axle(s,Steel,.06f,.98f,.1f,1.56f,1.6f,10);
            foreach(float dy in new[]{-.09f,.09f}){var at=V(1.585f,(.98f+1.3f)/2+dy,(.1f+w.Z)/2);float len=(float)Math.Sqrt(.32f*.32f+(.1f-w.Z)*(.1f-w.Z));s.Box(Rubber,at,V(.05f,.016f,len),0,(float)Math.Atan2(.32f,.1f-w.Z),0,0);}
            // The slide bars and the steam cylinder on its bracket, beside the muzzle: the side rod's reason to exist.
            float rx=RodX;foreach(float dy in new[]{-.1f,.1f})s.Box(Chrome,V(rx,w.Y+dy,.42f),V(.04f,.03f,1.2f),.006f);
            s.Box(GraphiteLight,V((rx+1f)/2,w.Y-.2f,.96f),V(rx-1f,.14f,.16f),.02f);s.Box(GraphiteLight,V((rx+1f)/2,w.Y+.2f,.96f),V(rx-1f,.1f,.14f),.02f);s.Box(Graphite,V(.98f,w.Y,.96f),V(.2f,.58f,.26f),.03f);
            s.Cylinder(Red,.19f,1.0f,1.62f,22,.022f,rx,w.Y,1);foreach(float z in new[]{1.04f,1.58f})s.Cylinder(Brass,.215f,z-.03f,z+.03f,22,.008f,rx,w.Y,1);
            RivetRing(s,BrassBright,rx,w.Y,.205f,1.64f,10,.011f);s.Cylinder(Steel,.05f,.94f,1.02f,12,.006f,rx,w.Y);
            // A big gauge cluster low on the right flank: three small dials, and a brass plate.
            float px=-.99f;s.Box(Graphite,V(px,1.05f,.2f),V(.05f,.32f,.72f),.012f);
            s.Push(WeaponSculpt.Ry(-Pi/2)*WeaponSculpt.T(px-.026f,1.05f,.2f));for(int k=0;k<3;k++){float zx=-.22f+k*.22f;s.Cylinder(Brass,.09f,0,.02f,18,.005f,zx,0);s.Cylinder(Cream,.075f,.02f,.024f,18,0,zx,0);s.Box(Ink,V(zx,.02f,.026f),V(.008f,.05f,.002f),0,0,0,-.4f+k*.5f);}s.Pop();
        }
        // The top: marquee, stack, hopper, beacons.
        private static void Top(WeaponSculpt s,WeaponMesh mesh)
        {
            // The stack: a tall flared chimney on a saddle, off to one side so it rises clear of the marquee; the cap and its spring are their own models.
            float sx=StackX,sz=StackZ;s.Box(EnamelDark,V(sx,2.86f,sz),V(.74f,.1f,.74f),.03f);
            Turned(s,Graphite,sx,sz,new[]{.3f,2.9f,.27f,3.02f,.205f,3.12f,.195f,4.62f,.23f,4.8f,.35f,4.94f,.37f,5.0f,.32f,5.02f},24,1,true);
            foreach(float y in new[]{3.4f,3.9f,4.5f}){Column(s,Brass,.215f,sx,sz,y,y+.06f,24,.008f);RivetRingFlat(s,BrassBright,sx,sz,.21f,y+.03f,12,.009f);}
            for(int k=0;k<8;k++){double a=k*Math.PI/4;s.Box(GraphiteDark,V(sx+(float)Math.Cos(a)*.2f,5.01f,sz+(float)Math.Sin(a)*.2f),V(.05f,.03f,.1f),0,0,-(float)a+Pi/2,0);}
            // The marquee on two chrome posts: a black board, chrome frame, chasing bulbs, the name in lamp lettering, the price under it, a red cross on the crown.
            const float cy=3.72f,cz=.16f,w=3.04f,h=.86f;
            foreach(int side in new[]{-1,1}){Column(s,Chrome,.05f,side*.98f,cz,2.4f,cy-h/2+.02f,14,.008f);Column(s,Brass,.08f,side*.98f,cz,2.4f,2.68f,14,.012f);}
            Panel(s,GraphiteDark,w,h,.08f,0,cy,cz-.07f,cz+.07f,.015f);
            foreach(int back in new[]{1,-1}){float z=back>0?cz+.07f:cz-.09f;s.Box(Chrome,V(0,cy+h/2-.01f,z+.01f),V(w-.1f,.03f,.02f),.005f);s.Box(Chrome,V(0,cy-h/2+.01f,z+.01f),V(w-.1f,.03f,.02f),.005f);foreach(int side in new[]{-1,1})s.Box(Chrome,V(side*(w/2-.01f),cy,z+.01f),V(.03f,h-.1f,.02f),.005f);}
            s.Into(Lights);
            const string name="REVIVE-O-MATIC",sub="$250 NO REFUNDS";const float nameH=.3f,subH=.095f;
            Word(s,Lamp(Ivory,1.1f),name,0,cy+.1f,cz+.07f,nameH,.012f);Word(s,Lamp(Amber,.9f),sub,0,cy-.22f,cz+.07f,subH,.008f);
            WordBack(s,Lamp(Ivory,1.1f),name,0,cy+.1f,cz-.07f,nameH,.012f);WordBack(s,Lamp(Amber,.9f),sub,0,cy-.22f,cz-.07f,subH,.008f);
            // The crest: a red cross in a brass ring over the board.
            s.Into(Body);s.Box(GraphiteDark,V(0,cy+h/2+.05f,cz),V(.3f,.1f,.1f),.02f);s.Cylinder(Brass,.22f,cz-.05f,cz+.05f,24,.012f,0,cy+h/2+.26f);s.Cylinder(Cream,.18f,cz+.05f,cz+.056f,24,0,0,cy+h/2+.26f);s.Cylinder(Cream,.18f,cz-.056f,cz-.05f,24,0,0,cy+h/2+.26f);
            s.Into(Lights);foreach(float z in new[]{cz+.056f,cz-.062f}){s.Box(Lamp(DangerRed,1.1f),V(0,cy+h/2+.26f,z+.003f),V(.24f,.075f,.006f));s.Box(Lamp(DangerRed,1.1f),V(0,cy+h/2+.26f,z+.003f),V(.075f,.24f,.006f));}
            // The bulbs: two banks round the board's edge, front and back.
            int n=0;foreach(int back in new[]{1,-1})
            {
                float z=back>0?cz+.085f:cz-.085f;
                void B(float x,float y){s.Into(n++%2==0?BulbsA:BulbsB);s.Sphere(Lamp(Warm,1.2f),V(x,y,z),V(.024f,.024f,.024f),3,8);}
                for(int i=0;i<=21;i++){float x=-w/2+.1f+i*(w-.2f)/21;B(x,cy+h/2-.045f);B(x,cy-h/2+.045f);}
                for(int i=1;i<5;i++){float y=cy-h/2+.045f+i*(h-.09f)/5;B(-w/2+.05f,y);B(w/2-.05f,y);}
            }
            s.Into(Body);
            // The hopper, into which the money goes: a brass funnel, gold in its throat, on a pipe into the boiler.
            var hp=HopperAt;Column(s,BrassDark,.06f,hp.X,hp.Z,2.6f,hp.Y+.04f,12,.008f);Turned(s,Brass,hp.X,hp.Z,new[]{.07f,hp.Y,.09f,hp.Y+.1f,.3f,hp.Y+.42f,.33f,hp.Y+.46f},22,1,false);Column(s,BrassDark,.08f,hp.X,hp.Z,hp.Y+.1f,hp.Y+.11f,20);
            for(int k=0;k<9;k++){double a=k*2.4;float r=.05f+.015f*k;s.Push(WeaponSculpt.Rz(.15f)*WeaponSculpt.Rx(Up+.2f*(k%3-1))*WeaponSculpt.T(hp.X+(float)Math.Cos(a)*r,hp.Y+.16f+.012f*k,hp.Z+(float)Math.Sin(a)*r));s.Cylinder(Gold,.05f,-.007f,.007f,12,.002f);s.Pop();}
            Torus3(s,Brass,V(hp.X,hp.Y+.46f,hp.Z),.33f,.016f);
            // The glass money tube, in from the console, ending over the hopper; brass collars along it.
            var path=new List<Vector3>();for(int i=0;i<CoinPath.Length-1;i++)for(int k=0;k<6;k++)path.Add(Catmull(CoinPath,i,k/6f));path.Add(CoinPath[CoinPath.Length-1]);
            Clear(s,mesh,GlassAlpha,()=>s.Tube(WeaponPaint.Solid(0xdfefff,.25f),path,.075f,14,Vector3.UnitY,1,false));
            for(int i=1;i<CoinPath.Length;i++){var a=CoinPath[i-1];var b=CoinPath[i];Flange(s,Brass,b,b-a,.09f,.03f);}
            // A clamp from the marquee post to the tube; two beacons on the shoulders.
            s.Box(SteelDark,V(-1.28f,3.5f,-.08f),V(.64f,.05f,.05f),.01f,0,.5f,-.1f);
            foreach(int side in new[]{-1,1})
            {
                float bx=side*.46f,by=3.2f,bz=-.3f;Column(s,GraphiteDark,.1f,bx,bz,by-.22f,by-.12f,14,.012f);
                s.Into(Lights);s.Sphere(Lamp(Amber,1.2f),V(bx,by,bz),V(.085f,.12f,.085f),5,12);s.Into(Body);
                Clear(s,mesh,DomeAlpha,()=>s.Sphere(WeaponPaint.Solid(0xffc070,.3f),V(bx,by,bz),V(.1f,.15f,.1f),5,14));
            }
        }
        private static void RivetRingFlat(WeaponSculpt s,in WeaponPaint p,float x,float z,float r,float y,int n,float size)
        {for(int i=0;i<n;i++){double a=i*Math.PI*2/n;Rivet(s,p,V(x+r*(float)Math.Cos(a),y,z+r*(float)Math.Sin(a)),size);}}
        private static void Torus3(WeaponSculpt s,in WeaponPaint p,Vector3 at,float major,float minor)
        {s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(at.X,at.Y,at.Z));s.Torus(p,V(0,0,0),major,minor,24,6);s.Pop();}
        // The copper: pipes, valves and a mess of plumbing over the shoulders and down the flanks.
        private static void Plumbing(WeaponSculpt s,WeaponMesh mesh)
        {
            Pipe(s,Copper,.03f,V(StackX-.1f,2.98f,-.44f),V(.92f,2.76f,-.3f),V(1.12f,2.36f,-.3f),V(1.19f,1.82f,-.34f),V(1.18f,1.26f,-.3f),V(1.06f,.84f,-.26f));
            Pipe(s,Copper,.03f,V(-.34f,2.98f,-.44f),V(-.84f,2.82f,-.32f),V(-1.14f,2.36f,-.34f),V(-1.19f,1.82f,-.34f),V(-1.18f,1.3f,-.4f),V(-1.06f,.84f,-.44f));
            Pipe(s,CopperDark,.022f,V(.5f,2.92f,.4f),V(.86f,2.62f,.46f),V(1.14f,2.16f,.5f),V(1.12f,1.3f,.56f),V(.94f,.9f,.6f));
            Pipe(s,CopperDark,.022f,V(-.5f,2.92f,.4f),V(-.86f,2.62f,.46f),V(-1.14f,2.16f,.5f),V(-1.12f,1.3f,.56f),V(-.94f,.9f,.6f));
            Pipe(s,Copper,.018f,V(.22f,3.0f,.22f),V(.42f,2.98f,.28f),V(.5f,2.92f,.4f));Pipe(s,Copper,.018f,V(-.22f,3.0f,.22f),V(-.42f,2.98f,.28f),V(-.5f,2.92f,.4f));
            foreach(var (at,along) in new[]{(V(1.14f,2.16f,.5f),V(0,-1,0)),(V(-1.14f,2.16f,.5f),V(0,-1,0)),(V(1.12f,2.36f,-.3f),V(.3f,-1,0)),(V(-1.14f,2.36f,-.34f),V(-.3f,-1,0)),(V(1.19f,1.4f,-.32f),V(0,-1,0)),(V(-1.19f,1.4f,-.38f),V(0,-1,0))})
                Flange(s,Brass,at,along,.05f,.03f);
            // Valves on the shoulders; a whistle on top.
            foreach(int side in new[]{-1,1})
            {
                s.Push(WeaponSculpt.Ry(side*.9f)*WeaponSculpt.T(side*.98f,2.5f,.14f));Handwheel(s,0,0,0,.075f);s.Pop();
                s.Box(BrassDark,V(side*.86f,2.5f,.08f),V(.16f,.05f,.05f),.008f);
            }
            Column(s,Brass,.04f,-.3f,-.82f,2.92f,3.32f,12,.008f);Turned(s,BrassBright,-.3f,-.82f,new[]{.045f,3.32f,.055f,3.4f,.04f,3.44f,.045f,3.6f,.02f,3.64f},12);
            // Down the belly, the two drain cocks that steam when it builds.
            foreach(int side in new[]{-1,1}){Pipe(s,CopperDark,.02f,V(side*.58f,1.02f,.56f),V(side*.68f,.92f,.72f),V(side*.72f,.74f,1.1f),V(side*.72f,.72f,1.44f));Column(s,Brass,.03f,side*.72f,1.44f,.68f,.76f,10,.006f);}
        }

        // ---- the moving parts (each about its own pivot at the origin) ----------------------------------------------------------
        // A half of the mouth's opening as a door: its outline from the hinge (y 0) to `edge`, `dir` +1 for the jaw (up from its hinge), -1 for the visor.
        private static Vector3[] DoorOutline(float dir,float edge)
        {
            var o=new List<Vector3>();float hw=MouthWidth/2-.012f,r=MouthCorner-.01f;
            // Counter-clockwise (seen from +z): for the jaw, along the hinge's corners then the straight lip; the visor's is its mirror in y, reversed.
            var pts=new List<Vector3>{V(hw,edge,0),V(-hw,edge,0)};for(int i=0;i<=6;i++){double a=Math.PI+i*(Math.PI/2)/6;pts.Add(V(-hw+r+r*(float)Math.Cos(a),r+r*(float)Math.Sin(a),0));}for(int i=0;i<=6;i++){double a=Math.PI*1.5+i*(Math.PI/2)/6;pts.Add(V(hw-r+r*(float)Math.Cos(a),r+r*(float)Math.Sin(a),0));}
            if(dir<0){pts=pts.ConvertAll(p=>V(p.X,-p.Y,0));pts.Reverse();}return pts.ToArray();
        }
        private const float Half=(MouthTop-MouthBottom)/2,ToothH=.13f,PlateEdge=Half-ToothH;
        // A door: its plate, a raised panel with its stencil, brass along the lip, rivets, the teeth along its edge with black behind them, hinge knuckles at the origin.
        private static void Door(WeaponSculpt s,float dir,string word)
        {
            var q=Enamel;s.Slab(_=>EnamelDark,q,EnamelDark,DoorOutline(dir,PlateEdge),-.036f,.036f,.012f);
            s.Box(EnamelPale,V(0,dir*(PlateEdge/2+.015f),.038f),V(1.02f,PlateEdge-.11f,.006f),.002f);
            Word(s,Ink,word,0,dir*(PlateEdge/2+.015f),.041f,.1f,.002f);
            s.Box(Brass,V(0,dir*(PlateEdge+.002f),.012f),V(MouthWidth-.04f,.024f,.07f),.008f);
            foreach(int side in new[]{-1,1}){RivetRow(s,BrassBright,side*.6f,dir*.14f,side*.6f,dir*(PlateEdge-.04f),.042f,2,.013f);}
            // Black behind the teeth, so the gaps between them read.
            s.Box(Black,V(0,dir*(Half-ToothH/2),-.014f),V(MouthWidth-.06f,ToothH+.01f,.02f));
            const int n=7;float pitch=(MouthWidth-.12f)/n;
            for(int i=0;i<n;i++)
            {
                float x=(i-(n-1)/2f)*pitch,h=ToothH*(i==0||i==n-1?.82f:1);
                s.Box(Chrome,V(x,dir*(PlateEdge+h/2),.006f),V(pitch-.024f,h-.006f,.056f),.014f);
                s.Box(Steel,V(x,dir*(PlateEdge+.012f),.03f),V(pitch-.05f,.016f,.012f));
            }
            for(int i=-3;i<=3;i++)Axle(s,i%2==0?Steel:SteelDark,.038f,0,0,i*.2f-.08f,i*.2f+.08f,10,.006f);
        }
        // The lower jaw: from its hinge at the origin up +y when shut; open it falls forward about x, and its inside is a ramp, hazard-striped.
        private static WeaponMesh BuildJaw()=>New(Jaw,(s,mesh)=>
        {
            Door(s,1,"SAY AHH");
            s.Push(WeaponSculpt.Ry(Pi));Stripes(s,1.12f,.2f,0,.15f,.036f,.2f,.006f);s.Pop();
        });
        private static WeaponMesh BuildVisor()=>New(Visor,(s,mesh)=>
        {
            Door(s,-1,"NEXT PATIENT");
        });
        // One link of the tongue: a red rubber slat from the origin along +z with a chevron on top; the draw chains them.
        private static WeaponMesh BuildTongue()=>New(Tongue,(s,mesh)=>
        {
            float l=TongueLink;s.Box(TongueRed,V(0,0,l/2),V(1,.055f,l+.012f),.016f);s.Box(TongueDark,V(0,.03f,l/2),V(.06f,.006f,l));
            foreach(int side in new[]{-1,1}){s.Box(Hazard,V(side*.22f,.029f,l/2),V(.26f,.004f,.05f),0,0,side*.5f,0);s.Box(TongueDark,V(side*.48f,.03f,l/2),V(.02f,.006f,l));}
        });
        private static WeaponMesh BuildTongueTip()=>New(TongueTip,(s,mesh)=>
        {
            float l=TongueLink;s.Box(TongueRed,V(0,0,l/4),V(1,.055f,l/2+.012f),.016f);s.Box(TongueDark,V(0,.03f,l/4),V(.06f,.006f,l/2));
            s.Push(WeaponSculpt.Rx(Pi/2)*WeaponSculpt.T(0,0,l/2));
            s.Slab(_=>TongueRed,TongueRed,TongueRed,WeaponSculpt.Circle(24,.5f,.2f,0),-.0275f,.0275f,.012f);s.Pop();
        });
        // The flywheel about its axle (x): a heavy red rim with white ticks so its spin reads, six spokes, a boss, the crank pin at CrankRadius on +x's face.
        private static WeaponMesh BuildFlywheel()=>New(Flywheel,(s,mesh)=>
        {
            s.Push(WeaponSculpt.Ry(Pi/2));
            float r=WheelRadius;var ring=new List<Vector3[]>();float[][] prof={new[]{r-.1f,-.08f},new[]{r-.02f,-.1f},new[]{r,-.07f},new[]{r,.07f},new[]{r-.02f,.1f},new[]{r-.1f,.08f},new[]{r-.12f,0f}};
            foreach(var p in prof)ring.Add(WeaponSculpt.Circle(48,p[0],p[0],p[1]));s.Loft(ring,(i,k)=>Red,1,false,false,null,true);
            for(int k=0;k<12;k++){double a=k*Math.PI/6;s.Box(Cream,V((float)Math.Cos(a)*(r-.05f),(float)Math.Sin(a)*(r-.05f),.102f),V(k%3==0?.2f:.09f,.03f,.004f),0,0,0,(float)a);s.Box(Cream,V((float)Math.Cos(a)*(r-.05f),(float)Math.Sin(a)*(r-.05f),-.102f),V(k%3==0?.2f:.09f,.03f,.004f),0,0,0,(float)a);}
            for(int k=0;k<6;k++){double a=k*Math.PI/3;s.Box(RedDark,V((float)Math.Cos(a)*(r/2),(float)Math.Sin(a)*(r/2),0),V(r-.1f,.09f,.05f),.012f,0,0,(float)a);}
            s.Cylinder(SteelDark,.17f,-.13f,.13f,18,.015f);s.Cylinder(Brass,.11f,.13f,.15f,14,.006f);RivetRing(s,BrassBright,0,0,.14f,.13f,6,.012f);
            // The counterweight's lump and the crank pin, out on the side rod's face.
            s.Box(GraphiteLight,V(-CrankRadius,0,.0f),V(.2f,.26f,.13f),.02f);
            s.Cylinder(Chrome,.04f,.08f,RodX-WheelAxle.X+.04f,12,.005f,CrankRadius,0);
            s.Pop();
        });
        // The side rod: from its crank eye at the origin along +z to its crosshead eye at RodLength.
        private static WeaponMesh BuildRod()=>New(Rod,(s,mesh)=>
        {
            float l=RodLength;s.Box(Steel,V(0,0,l/2),V(.05f,.09f,l-.1f),.012f);s.Box(SteelDark,V(0,0,l/2),V(.06f,.04f,l-.2f));
            foreach(float z in new[]{0,l}){s.Push(WeaponSculpt.Ry(Pi/2)*WeaponSculpt.T(0,0,z));s.Cylinder(Brass,.08f,-.04f,.04f,14,.008f);s.Pop();}
        });
        // The crosshead on the slide bars and the piston rod into the cylinder, from the crosshead at the origin along +z.
        private static WeaponMesh BuildPiston()=>New(Piston,(s,mesh)=>
        {
            s.Box(GraphiteLight,V(0,0,0),V(.13f,.16f,.2f),.02f);s.Cylinder(Chrome,.04f,0,.86f,12,.004f);
        });
        // A gauge's needle about its hub at the origin, pointing +y.
        private static WeaponMesh BuildNeedle()=>New(Needle,(s,mesh)=>
        {
            s.Box(Red,V(0,.11f,0),V(.022f,.24f,.006f));s.Box(Ink,V(0,-.03f,0),V(.03f,.08f,.006f));s.Cylinder(Ink,.03f,-.004f,.01f,12,.003f);s.Cylinder(BrassBright,.012f,.01f,.016f,8);
        });
        // A steel brow, level, about its mount at the origin.
        private static WeaponMesh BuildBrow()=>New(Brow,(s,mesh)=>
        {
            s.Box(SteelDark,V(0,0,.02f),V(.72f,.13f,.09f),.022f);s.Box(Steel,V(0,.012f,.068f),V(.66f,.07f,.01f),.004f);RivetRow(s,BrassBright,-.3f,.01f,.3f,.01f,.075f,6,.01f);
        });
        // A cheek: pleated rubber bellows along +x from the origin, with a brass end cap and a relief valve. Mirrored for the other side, scaled as it inflates.
        private static WeaponMesh BuildCheek()=>New(Cheek,(s,mesh)=>
        {
            s.Push(WeaponSculpt.Ry(Pi/2));var ry=new List<float>();for(int k=0;k<=12;k++){ry.Add(k%2==0?.3f:.4f);ry.Add(.0f+k*.034f);}
            s.Lathe(Rubber,ry.ToArray(),28,1,0,false);foreach(int k in new[]{0,4,8,12})s.Torus(BrassDark,V(0,0,k*.034f),.31f,.012f,28,5);
            s.Cylinder(Brass,.36f,.408f,.45f,28,.012f);s.Cylinder(BrassDark,.2f,.45f,.47f,18,.006f);RivetRing(s,BrassBright,0,0,.3f,.45f,10,.012f);
            s.Cylinder(Red,.05f,.47f,.53f,12,.008f);s.Pop();
        });
        // The stack's cap: a cone about its seat at the origin, which leaves on a spring.
        private static WeaponMesh BuildCap()=>New(Cap,(s,mesh)=>
        {
            Turned(s,GraphiteLight,0,0,new[]{.0f,-.02f,.34f,.02f,.42f,.05f,.36f,.08f,.14f,.22f,.05f,.32f,.0f,.34f},22,1,false);Turned(s,Red,0,0,new[]{.06f,.3f,.06f,.36f,.03f,.4f,.0f,.42f},10,1,false);
        });
        // The nose: a red rubber bulb, its back at the origin, pointing +z.
        private static WeaponMesh BuildBulb()=>New(Bulb,(s,mesh)=>
        {
            s.Sphere(Red,V(0,0,.11f),V(.12f,.12f,.12f),8,16);s.Sphere(RedDark,V(0,0,.228f),V(.03f,.03f,.012f),3,8);s.Cylinder(Brass,.06f,-.03f,.02f,12,.006f);
        });
        // The console, in its own frame (front +z): a battered pedestal, a green CRT on an arm, a big red button, the coin funnel that feeds the tube, a TAKE A NUMBER roll and the incident board on its pole.
        private static WeaponMesh BuildConsole()=>New(Console,(s,mesh)=>
        {
            s.Box(GraphiteDark,V(0,.05f,0),V(1.3f,.1f,.94f),.018f);s.Box(Rubber,V(0,.006f,0),V(1.26f,.012f,.9f),.003f);
            s.Box(Enamel,V(0,.52f,0),V(1.1f,.84f,.72f),.03f);s.Box(EnamelPale,V(0,.5f,.365f),V(.96f,.66f,.012f),.004f);Stripes(s,.96f,.1f,0,.16f,.362f,.1f,.008f);
            RivetRow(s,BrassBright,-.46f,.8f,.46f,.8f,.37f,9,.01f);RivetRow(s,BrassBright,-.46f,.26f,.46f,.26f,.37f,9,.01f);
            // The sloped desk, its brass edge, the controls.
            s.Push(WeaponSculpt.Rx(.42f)*WeaponSculpt.T(0,.94f,.04f));
            s.Box(Graphite,V(0,0,0),V(1.14f,.05f,.8f),.016f);s.Box(BrassDark,V(0,.026f,.38f),V(1.14f,.012f,.03f),.003f);
            // Big red button in a hazard collar; a row of toggles; lit lamps; a keypad that nobody uses.
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(.36f,.026f,.16f));s.Cylinder(Hazard,.13f,0,.012f,22,.002f);s.Cylinder(Black,.115f,.012f,.02f,22);s.Lathe(Red,new[]{.095f,.02f,.105f,.05f,.1f,.075f,.06f,.095f,.0f,.102f},18,1,0,false);s.Pop();
            for(int k=0;k<5;k++){float x=-.42f+k*.12f;s.Box(SteelDark,V(x,.03f,.26f),V(.05f,.012f,.08f),.003f);s.Push(WeaponSculpt.Rx(k%2==0?.5f:-.5f)*WeaponSculpt.T(x,.04f,.26f));s.Box(Chrome,V(0,.03f,0),V(.018f,.06f,.018f),.004f);s.Pop();}
            for(int i=0;i<3;i++)for(int k=0;k<3;k++)s.Box(GraphiteLight,V(.08f+k*.06f,.03f,-.14f+i*.06f),V(.05f,.012f,.05f),.004f);
            s.Into(Lights);foreach(var (x,c) in new[]{(-.42f,Mint),(-.3f,Amber),(-.18f,DangerRed)})s.Sphere(Lamp(c),V(x,.036f,.12f),V(.024f,.014f,.024f),3,8);s.Into(Body);
            s.Pop();
            // The screen on its arm: a CRT in a chunky hood, turned to whoever stands at it.
            s.Box(GraphiteLight,V(-.12f,1.14f,-.2f),V(.16f,.4f,.16f),.02f);
            s.Push(WeaponSculpt.Rx(-.16f)*WeaponSculpt.T(-.12f,1.62f,-.08f));
            s.Box(Graphite,V(0,0,-.1f),V(.86f,.68f,.5f),.05f);s.Box(GraphiteDark,V(0,0,.16f),V(.8f,.62f,.05f),.02f);
            s.Into(Lights);s.Box(Lamp(0x061e12,1),V(0,0,.19f),V(.68f,.5f,.006f));Word(s,Lamp(Screen,.9f),"REVIVE",0,.13f,.193f,.09f,.002f);Word(s,Lamp(Screen,.9f),"PRESS E",0,-.03f,.193f,.05f,.002f);s.Box(Lamp(Screen,.8f),V(0,-.13f,.194f),V(.42f,.012f,.002f));s.Box(Lamp(DangerRed,.9f),V(.28f,-.2f,.194f),V(.03f,.03f,.002f));
            for(int k=0;k<9;k++)s.Box(Lamp(0x0a2c1c,1),V(0,-.22f+k*.055f,.1932f),V(.68f,.003f,.001f));s.Into(Body);
            s.Pop();
            // The coin funnel on the tube's foot, at the back.
            var foot=NativeReviveMachine.ConsoleLocal(CoinPath[0]);Turned(s,Brass,foot.X,foot.Z,new[]{.13f,1.2f,.1f,1.32f,.09f,1.5f,.1f,foot.Y-.02f,.12f,foot.Y+.04f},16,1,true);s.Box(GraphiteLight,V(foot.X,1.05f,foot.Z),V(.3f,.3f,.26f),.02f);
            // The coin slot on the pedestal's flank; a cable drooping to the machine.
            s.Box(Brass,V(.56f,.76f,.12f),V(.012f,.18f,.1f),.004f);s.Box(Black,V(.566f,.76f,.12f),V(.004f,.11f,.012f));
            Pipe(s,Rubber,.026f,V(.5f,.2f,-.2f),V(.82f,.06f,-.3f),V(1.2f,.05f,-.5f),V(1.5f,.14f,-.8f));
            // The incident board on a pole, right where everyone can see it.
            Column(s,SteelDark,.03f,-.68f,-.1f,.1f,2.5f,10);
            s.Box(Cream,V(-.68f,2.3f,-.07f),V(.98f,.58f,.028f),.006f);s.Box(Red,V(-.68f,2.53f,-.054f),V(.94f,.12f,.004f));
            Word(s,Cream,"SAFETY FIRST",-.68f,2.53f,-.052f,.058f,.002f);Word(s,Ink,"DAYS SINCE LAST",-.68f,2.38f,-.056f,.05f,.002f);Word(s,Ink,"INCIDENT",-.68f,2.31f,-.056f,.05f,.002f);
            s.Box(Black,V(-.68f,2.15f,-.056f),V(.24f,.14f,.004f));Word(s,Lamp(DangerRed,1),"0",-.68f,2.15f,-.054f,.09f,.002f);
            // Take a number.
            s.Box(Red,V(.48f,1.0f,-.2f),V(.18f,.2f,.14f),.03f);s.Box(Cream,V(.48f,.92f,-.13f),V(.05f,.08f,.003f));Word(s,Cream,"TAKE A",.48f,1.05f,-.129f,.018f,.001f);
            // The stencil on its flank.
            s.Push(WeaponSculpt.Ry(Pi/2));Word(s,Ink,"NO REFUNDS",0,.5f,.56f,.05f,.002f);s.Pop();
        });
        // The console's lever, about its hub at the origin, pointing up.
        private static WeaponMesh BuildLever()=>New(Lever,(s,mesh)=>
        {
            s.Push(WeaponSculpt.Ry(Pi/2));s.Cylinder(SteelDark,.05f,-.03f,.03f,12,.006f);s.Pop();s.Box(Chrome,V(0,.2f,0),V(.03f,.42f,.03f),.006f);s.Sphere(Red,V(0,.44f,0),V(.055f,.055f,.055f),6,12);
        });
        // The crash mat: blue gym panels quilted in squares, a bullseye, a seam of tape where it split. Flat: it does not collide.
        private static WeaponMesh BuildMat()=>New(Mat,(s,mesh)=>
        {
            float m=MatSize,h=.06f;s.Box(MatBlueDark,V(0,h/2,0),V(m,h,m),.018f);
            for(int i=0;i<6;i++)for(int k=0;k<6;k++){float x=(i-2.5f)*m/6,z=(k-2.5f)*m/6;s.Box((i+k)%2==0?MatBlue:MatBlueDark,V(x,h+.002f,z),V(m/6-.02f,.006f,m/6-.02f),.002f);}
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,h+.006f,0));
            s.Cylinder(MatWhite,1.28f,0,.004f,40);s.Cylinder(MatRed,1.08f,.004f,.006f,40);s.Cylinder(MatWhite,.74f,.006f,.008f,36);s.Cylinder(MatRed,.42f,.008f,.01f,28);
            s.Pop();
            s.Box(Tape,V(1.2f,h+.018f,-1.05f),V(.8f,.003f,.07f),0,0,.3f,0);s.Box(Tape,V(1.06f,h+.018f,-1.22f),V(.6f,.003f,.07f),0,0,.1f,0);
            // The handles along its edge.
            foreach(int side in new[]{-1,1})for(int k=-1;k<=1;k++)s.Box(GraphiteDark,V(side*(m/2+.02f),h*.6f,k*1.1f),V(.05f,.02f,.3f),.004f);
        });
        // The lane painted on the floor between the mouth and the mat, and the stand-clear lines. Flat paint.
        private static WeaponMesh BuildLane()=>New(Lane,(s,mesh)=>
        {
            const float y=.006f,z0=1.75f,z1=MatZ-MatHalf-.05f,len=z1-z0;
            foreach(int side in new[]{-1,1})
            {
                for(int k=0;k<6;k++)s.Box(Hazard,V(side*1.42f,y,z0+.1f+k*(len-.1f)/6f+.16f),V(.1f,.004f,.32f));
            }
            // Chevrons toward the mat.
            for(int k=0;k<3;k++){float z=z0+.34f+k*.56f;foreach(int side in new[]{-1,1}){s.Box(k==1?Cream:Hazard,V(side*.27f,y,z),V(.62f,.004f,.13f),0,0,-side*.62f,0);}}
            // STAND CLEAR, lying on the floor, to be read by someone walking toward the mouth.
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,y+.002f,z1-.32f));Word(s,Cream,"STAND CLEAR",0,0,0,.16f,.002f);s.Pop();
        });
        private const float MatZ=6.1f,MatHalf=MatSize/2;
        // A coin of the money tube, about its centre, its axis y.
        private static WeaponMesh BuildCoin()=>New(Coin,(s,mesh)=>
        {
            s.Push(WeaponSculpt.Rx(Up));s.Cylinder(Gold,.05f,-.008f,.008f,14,.003f);s.Pop();
        });
        // The bolt that pops off when it spits.
        private static WeaponMesh BuildBolt()=>New(Bolt,(s,mesh)=>
        {
            s.Push(WeaponSculpt.Rx(Up));s.Cylinder(Steel,.018f,-.05f,.03f,8);s.Cylinder(SteelDark,.034f,.03f,.05f,6,.004f);s.Pop();
        });
    }
}
