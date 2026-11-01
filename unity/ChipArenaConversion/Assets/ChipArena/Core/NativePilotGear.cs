using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The pilot's chest core and right-wrist chip device (docs/CHARACTER-LOWPOLY.md section e; docs/BOZO-INTEGRATION.md step 4):
    /// our own pieces, built here as SourceLit sections (source frame: mirrored at upload as the weapons are) and drawn on the
    /// visual body at anchors its bake measured. Presentation only: the states are pure functions of what the pilot already
    /// presents (its health, the chip-select look and the wrist plant's timeline), never written back.
    /// <list type="bullet">
    /// <item>Core (anchor frame: z out of the chest, y up): a faceted shield housing sunk into the chest, a bevelled rim, a
    /// recessed gem glowing in the pilot's accent. Its glow (CoreGlow): a slow pulse, a flicker at low health, a flare as
    /// the body appears (the spawn-in, a revive); the hit flash and the spawn-in's reveal come with the body's draw.</item>
    /// <item>Device (anchor frame on the forearm's axis: y toward the elbow, -x the back of the forearm): a band round the
    /// sleeve (its radius from the bake), a housing with accent strips and a screen, a raised frame round the chip bay, a lid
    /// hinged at the elbow end with a lens, and the chip cartridge seated in the bay. Its states (Device): the lid opens with
    /// the chip-select look; the chip shows from its seat in the plant; the bay glows ready in the accent while selecting,
    /// pulses in the chip's colour as it is accepted, and the lens keeps that colour, decaying, once the lid has shut.</item>
    /// </list>
    /// </summary>
    public static class NativePilotGear
    {
        // ---- paints: metal untinted (tag 0), glow in the accent (tag 2), glow set per draw (tag 3: white, the draw's glow tint) ----
        private static readonly WeaponPaint MetalDark=WeaponPaint.Solid(0x1a1f26),Metal=WeaponPaint.Solid(0x3a4350),MetalLight=WeaponPaint.Solid(0x75818f,.04f);
        private static readonly WeaponPaint Glow=WeaponPaint.Accent(2.4f,.4f),GlowEdge=WeaponPaint.Accent(1.1f,.3f),Strip=WeaponPaint.Accent(1.6f,.35f),Screen=WeaponPaint.Accent(.55f,.12f);
        private static readonly WeaponPaint Set=new WeaponPaint(new Vector3(.08f,.08f,.08f),Vector3.One,3),Floor=WeaponPaint.Solid(0x0c0f13);
        private static readonly WeaponPaint ChipBody=WeaponPaint.Solid(0x2a2f38,.05f),ChipBezel=WeaponPaint.Solid(0x8d97a3,.04f),ChipGold=WeaponPaint.Solid(0xc9a43a,.15f);

        // ---- the core ----
        /// <summary>The core's size on the chest (1: the stopped base's, 12 cm across).</summary>
        public const float CoreScale=.72f;
        // The shield outline's right half (x, y), top centre to the bottom tip: a V notch on top, flat shoulders, a pointed base.
        private static readonly float[] ShieldHalf={0,.030f,.024f,.030f,.048f,.030f,.058f,.016f,.052f,-.008f,.034f,-.034f,.015f,-.052f,0,-.060f};
        /// <summary>The shield outline at scale `s`, counter-clockwise seen from +z, centred on its own middle, at depth z.</summary>
        public static Vector3[] Shield(float s,float z)
        {
            int half=ShieldHalf.Length/2;var right=new Vector3[half];
            for(int i=0;i<half;i++)right[i]=new Vector3(ShieldHalf[i*2]*s*CoreScale,(ShieldHalf[i*2+1]+.015f)*s*CoreScale,z);
            // Right side top to bottom is clockwise seen from +z: reversed, then the left side bottom to top.
            var ring=new Vector3[half*2-2];int k=0;
            for(int i=half-1;i>=0;i--)ring[k++]=right[i];
            for(int i=1;i<half-1;i++)ring[k++]=new Vector3(-right[i].X,right[i].Y,z);
            return ring;
        }
        /// <summary>The core: the housing's rings (scale, height, paint), then the gem rising to a ridge.</summary>
        public static WeaponMesh Core()
        {
            var mesh=new WeaponMesh{Spec="pilot-core"};var s=new WeaponSculpt(mesh,0);s.Into(0);
            float[] scale={1.05f,1.05f,.98f,.92f,.86f,.74f,.68f},height={-.010f,.007f,.012f,.0128f,.012f,.006f,.007f};
            var paints=new[]{MetalDark,MetalDark,Metal,MetalLight,Metal,MetalDark,GlowEdge};
            var rings=new System.Collections.Generic.List<Vector3[]>();for(int i=0;i<scale.Length;i++)rings.Add(Shield(scale[i],height[i]));
            s.Loft(rings,(i,k)=>paints[i+1],0,true,false,MetalDark);
            var gem=Shield(.38f,.013f);for(int i=0;i<gem.Length;i++)gem[i]+=new Vector3(0,.002f*CoreScale,0);
            var top=WeaponSculpt.Point(gem.Length,new Vector3(0,(-.004f+.015f)*CoreScale,.017f));
            s.Loft(new[]{rings[rings.Count-1],gem,top},(i,k)=>i==0?GlowEdge:Glow,0,false,false);
            return mesh;
        }

        // ---- the device ----
        /// <summary>The housing's top over the band's radius r (its outer face at -(r + HousingTop)), the frame's height on it, the
        /// bay (y from BayNear to BayFar along the forearm, z within BayHalf), the lid's hinge (at y LidHinge, on the frame) and
        /// its length toward the hand, its full opening and the chip's thickness.</summary>
        public const float HousingTop=.023f,FrameHeight=.006f,BayNear=-.013f,BayFar=.019f,BayHalf=.0165f,LidHinge=.024f,LidLength=.042f,LidHalf=.0205f,LidThick=.0032f,LidOpenDegrees=112,ChipThick=.004f;
        /// <summary>The band's inner and outer radius over the sleeve's (m).</summary>
        public const float BandIn=-.004f,BandOut=.007f;
        /// <summary>The device's body (band, housing, strips, screen, the bay's frame and floor) for a sleeve of radius r.</summary>
        public static WeaponMesh Device(float r)
        {
            var mesh=new WeaponMesh{Spec="pilot-device"};var s=new WeaponSculpt(mesh,0);s.Into(0);
            // The band about the forearm (the y axis: a lathe about +z turned onto +y), with an inner lip under the sleeve.
            s.Push(WeaponSculpt.Rx(-(float)Math.PI/2));
            var bandPaint=new[]{MetalDark,Metal,MetalDark,Metal,MetalDark};
            s.Lathe(i=>bandPaint[Math.Min(i,bandPaint.Length-1)],new[]{r+BandIn,-.0245f,r+.0015f,-.024f,r+BandOut,-.021f,r+BandOut,.021f,r+.0015f,.024f,r+BandIn,.0245f},16,0,0,false);
            s.Lathe(Strip,new[]{r+BandOut+.0008f,-.002f,r+BandOut+.0008f,.002f},16,1,0,false);
            s.Pop();
            // The housing on the back of the forearm (-x), embedded in the band.
            float top=r+HousingTop;
            s.Box(Metal,new Vector3(-(r+.003f+top)*.5f,.002f,0),new Vector3(top-(r+.003f),.056f,.050f),.004f);
            // Accent strips along both sides and a dim screen at the hand end of the top.
            for(int side=-1;side<=1;side+=2)s.Decal(Strip,new Vector3(-(r+.011f),.002f,side*.025f),new Vector3(0,.022f*side,0),new Vector3(-.0035f,0,0)*1f,.0006f);
            s.Decal(Screen,new Vector3(-top,-.0198f,0),new Vector3(0,0,.012f),new Vector3(0,.0016f,0),.0006f);
            // The raised frame round the bay (four bars) and the bay's dark floor.
            float bar=.004f,fx=-(top+FrameHeight*.5f);
            s.Box(MetalLight,new Vector3(fx,(BayNear+BayFar)*.5f,BayHalf+bar*.5f),new Vector3(FrameHeight,BayFar-BayNear+2*bar,bar),.001f);
            s.Box(MetalLight,new Vector3(fx,(BayNear+BayFar)*.5f,-(BayHalf+bar*.5f)),new Vector3(FrameHeight,BayFar-BayNear+2*bar,bar),.001f);
            s.Box(MetalLight,new Vector3(fx,BayNear-bar*.5f,0),new Vector3(FrameHeight,bar,2*BayHalf),.001f);
            s.Box(MetalLight,new Vector3(fx,BayFar+bar*.5f,0),new Vector3(FrameHeight,bar,2*BayHalf),.001f);
            s.Decal(Floor,new Vector3(-top,(BayNear+BayFar)*.5f,0),new Vector3(0,0,BayHalf),new Vector3(0,(BayFar-BayNear)*.5f,0),.0004f);
            return mesh;
        }
        /// <summary>The bay's glow (white: the draw's glow tint gives its colour): the floor's inner panel and the frame's inner walls.</summary>
        public static WeaponMesh Bay(float r)
        {
            var mesh=new WeaponMesh{Spec="pilot-device-bay"};var s=new WeaponSculpt(mesh,0);s.Into(0);float top=r+HousingTop,y=(BayNear+BayFar)*.5f,hy=(BayFar-BayNear)*.5f;
            s.Decal(Set,new Vector3(-top,y,0),new Vector3(0,0,BayHalf-.004f),new Vector3(0,hy-.004f,0),.0008f);
            // The frame's inner walls, facing into the bay.
            Vector3 A(float yy,float z)=>new Vector3(-top,yy,z);Vector3 B(float yy,float z)=>new Vector3(-(top+FrameHeight),yy,z);
            s.Quad(Set,A(BayFar,BayHalf),A(BayNear,BayHalf),B(BayNear,BayHalf),B(BayFar,BayHalf));
            s.Quad(Set,A(BayNear,-BayHalf),A(BayFar,-BayHalf),B(BayFar,-BayHalf),B(BayNear,-BayHalf));
            s.Quad(Set,A(BayNear,BayHalf),A(BayNear,-BayHalf),B(BayNear,-BayHalf),B(BayNear,BayHalf));
            s.Quad(Set,A(BayFar,-BayHalf),A(BayFar,BayHalf),B(BayFar,BayHalf),B(BayFar,-BayHalf));
            return mesh;
        }
        /// <summary>The lid in its hinge frame (the hinge line along z at the origin; closed it lies toward the hand, -y, its outer
        /// face toward -x), with the lens (white: the draw's glow tint).</summary>
        public static WeaponMesh Lid()
        {
            var mesh=new WeaponMesh{Spec="pilot-device-lid"};var s=new WeaponSculpt(mesh,0);s.Into(0);
            s.Box(MetalDark,new Vector3(-LidThick*.5f,-LidLength*.5f,0),new Vector3(LidThick,LidLength,2*LidHalf),.0012f);
            // A hex lens in the middle of the outer face.
            var hex=WeaponSculpt.Circle(6,.0085f,.0085f,0,(float)Math.PI/2);var lens=new Vector3[6];var c=new Vector3(-LidThick-.0004f,-LidLength*.5f,0);
            for(int k=0;k<6;k++)lens[k]=c+new Vector3(0,hex[k].X,hex[k].Y);
            var tip=c+new Vector3(-.0016f,0,0);for(int k=0;k<6;k++)s.Tri(Set,lens[(k+1)%6],lens[k],tip);
            return mesh;
        }
        /// <summary>The chip cartridge (the game's: shoulder cuts, a bezel, a hex crystal, a gold edge connector) in its seat frame:
        /// lying in the bay's plane (y along the forearm, z across), its face toward -x, the connector toward the hand.</summary>
        public static WeaponMesh Chip()
        {
            var mesh=new WeaponMesh{Spec="pilot-device-chip"};var s=new WeaponSculpt(mesh,0);s.Into(0);
            // The outline in (z, y) seen from -x, counter-clockwise: built in a frame turned so the slab's +z is -x.
            s.Push(WeaponSculpt.Ry(-(float)Math.PI/2));
            var outline=WeaponSculpt.Outline(0,-.0075f,.0155f,-.013f,.010f,-.013f,-.0125f,-.010f,-.0155f,.010f,-.0155f,.013f,-.0125f,.013f,.010f,.0075f,.0155f);
            s.Slab(_=>ChipBezel,ChipBody,ChipBody,outline,0,ChipThick,.0006f);
            var hex=WeaponSculpt.Circle(6,.0062f,.0062f,ChipThick+.0004f,(float)Math.PI/2,0,.0018f);var tip=new Vector3(0,.0018f,ChipThick+.0016f);
            for(int k=0;k<6;k++)s.Tri(Set,hex[k],hex[(k+1)%6],tip);
            s.Quad(ChipGold,new Vector3(-.0085f,-.0148f,ChipThick+.0003f),new Vector3(.0085f,-.0148f,ChipThick+.0003f),new Vector3(.0085f,-.0118f,ChipThick+.0003f),new Vector3(-.0085f,-.0118f,ChipThick+.0003f));
            s.Pop();
            return mesh;
        }
        /// <summary>The lid's hinge and the chip's seat in the device frame (source axes) for a sleeve of radius r.</summary>
        public static Vector3 Hinge(float r)=>new Vector3(-(r+HousingTop+FrameHeight),LidHinge,0);
        public static Vector3 Seat(float r)=>new Vector3(-(r+HousingTop),(BayNear+BayFar)*.5f+.001f,0);

        // ---- the face gear ----
        // The owner's concept pieces for the model's face (docs/BOZO-INTEGRATION.md "Default outfit"): a lower-face tech mask and a
        // glowing visor band over the eyes, in the model's head bone's frame (source axes: y up, z out of the face). Fitted to the
        // baked head (Head_AnimeYoung with its lashes, brows and front hair: the eyes at y .044-.080 with the lashes reaching
        // z .116, the nose tip at y .03, z .151, the chin at y -.04, the bangs to y .07, hair over the temples from |x| .06):
        // grids of points (columns at +-|x|, rows bottom to top) about 5 mm off the face, the shells FaceThick deep.
        public const float FaceThick=.004f;
        // The mask: its lower edge along the jaw, curling under the chin; over the nose; its top edge tucked under the visor.
        private static readonly float[] MaskX={0,.015f,.03f,.045f,.058f,.068f};
        private static readonly float[] MaskY={
            -.050f,-.047f,-.038f,-.026f,-.014f,-.004f,
            -.032f,-.030f,-.023f,-.013f,-.004f,.004f,
            -.014f,-.012f,-.008f,-.001f,.006f,.012f,
            .005f,.005f,.008f,.012f,.016f,.019f,
            .021f,.021f,.021f,.023f,.025f,.026f,
            .034f,.031f,.030f,.031f,.031f,.031f,
            .041f,.040f,.038f,.037f,.036f,.035f};
        // Refitted 2026-10-27 (owner: "Mask seems to glitch on face still"): the nose bridge had come through between the top
        // two rows (to 4 mm: the face and the shell fought there) and the sides stood 14-16 mm off the cheeks (a plate with
        // gaps); now at least 3 mm clear over the nose and 5-6 mm off the cheeks, the sides wrapping the jaw.
        private static readonly float[] MaskZ={
            .113f,.109f,.101f,.096f,.071f,.079f,
            .129f,.126f,.116f,.106f,.094f,.080f,
            .136f,.135f,.123f,.121f,.113f,.103f,
            .143f,.138f,.129f,.121f,.108f,.104f,
            .152f,.143f,.130f,.123f,.112f,.099f,
            .157f,.143f,.130f,.122f,.111f,.105f,
            .146f,.136f,.126f,.120f,.111f,.106f};
        // The visor: a band over the eyes (y .041-.083: the frame's 4 mm above and below the glass), wrapping to the temples in
        // front of the hair.
        private static readonly float[] VisorX={0,.02f,.04f,.055f,.068f,.08f,.09f};
        private static readonly float[] VisorY={
            .041f,.041f,.041f,.041f,.041f,.041f,.041f,
            .045f,.045f,.045f,.045f,.045f,.045f,.045f,
            .079f,.079f,.079f,.079f,.079f,.079f,.079f,
            .083f,.083f,.083f,.083f,.083f,.083f,.083f};
        private static readonly float[] VisorZ={
            .141f,.137f,.130f,.124f,.117f,.109f,.100f,
            .141f,.137f,.130f,.124f,.117f,.109f,.100f,
            .140f,.136f,.129f,.123f,.116f,.108f,.099f,
            .139f,.135f,.128f,.122f,.115f,.107f,.098f};
        private static readonly WeaponPaint VisorGlass=WeaponPaint.Accent(1.3f,.5f);
        /// <summary>The lower-face mask: a dark shell over the nose, mouth and chin, three vents on each cheek and a strip down the
        /// chin in the accent, and a strap from each side back into the hair.</summary>
        public static WeaponMesh Mask()
        {
            var mesh=new WeaponMesh{Spec="pilot-mask"};var s=new WeaponSculpt(mesh,0);s.Into(0);
            Shell(s,MaskX,MaskY,MaskZ,(r,c)=>r==5?Metal:MetalDark,MetalDark,Metal);
            for(int side=-1;side<=1;side+=2)
            {
                for(int k=0;k<3;k++){float y=-.006f+k*.009f;s.Decal(Strip,new Vector3(side*.04f,y,MaskSurface(.04f,y)),new Vector3(.73f,0,-side*.68f)*.0065f,new Vector3(0,.0015f,0),.0012f);}
                // The strap along the cheek to the ear, 5-7 mm off the skin (it had floated up to 3 cm out, a stray line).
                s.Tube(Metal,new[]{new Vector3(side*.070f,.013f,.100f),new Vector3(side*.077f,.015f,.078f),new Vector3(side*.078f,.018f,.058f),new Vector3(side*.082f,.022f,.042f)},.0025f,6,Vector3.UnitY);
            }
            s.Decal(Strip,new Vector3(0,-.026f,MaskSurface(0,-.026f)),new Vector3(.0016f,0,0),new Vector3(0,.009f,.002f),.0012f);
            return mesh;
        }
        /// <summary>The visor: a band of accent glass across the eyes in a dark frame, ending in pods at the temples.</summary>
        public static WeaponMesh Visor()
        {
            var mesh=new WeaponMesh{Spec="pilot-visor"};var s=new WeaponSculpt(mesh,0);s.Into(0);int last=VisorX.Length*2-3;
            Shell(s,VisorX,VisorY,VisorZ,(r,c)=>r==1&&c>0&&c<last?VisorGlass:MetalDark,MetalDark,Metal);
            for(int side=-1;side<=1;side+=2)
            {
                s.Box(Metal,new Vector3(side*.094f,.062f,.094f),new Vector3(.009f,.03f,.02f),.002f);
                s.Decal(Strip,new Vector3(side*.0985f,.062f,.094f),new Vector3(0,0,-side*.005f),new Vector3(0,.011f,0),.0008f);
            }
            return mesh;
        }
        // A shell over a grid (rows bottom to top, columns +-xs mirrored about x = 0, each point's y in ys and outer z in zs)
        // FaceThick deep toward the head's vertical axis behind the face: the outer faces painted by row and column (left to
        // right), the inner face and the rim.
        private static void Shell(WeaponSculpt s,float[] xs,float[] ys,float[] zs,Func<int,int,WeaponPaint> outer,WeaponPaint inner,WeaponPaint rim)
        {
            int n=xs.Length,rows=ys.Length/n,cols=n*2-1;var o=new Vector3[rows,cols];var i=new Vector3[rows,cols];
            for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
            {
                int k=c-(n-1),a=Math.Abs(k);var p=new Vector3(Math.Sign(k)*xs[a],ys[r*n+a],zs[r*n+a]);
                o[r,c]=p;i[r,c]=p+Vector3.Normalize(new Vector3(0,p.Y,.02f)-p)*FaceThick;
            }
            for(int r=0;r+1<rows;r++)for(int c=0;c+1<cols;c++)
            {
                s.Quad(outer(r,c),o[r,c],o[r,c+1],o[r+1,c+1],o[r+1,c]);
                s.Quad(inner,i[r,c],i[r+1,c],i[r+1,c+1],i[r,c+1]);
            }
            for(int c=0;c+1<cols;c++){s.Quad(rim,o[0,c],i[0,c],i[0,c+1],o[0,c+1]);s.Quad(rim,o[rows-1,c],o[rows-1,c+1],i[rows-1,c+1],i[rows-1,c]);}
            for(int r=0;r+1<rows;r++){s.Quad(rim,o[r,0],o[r+1,0],i[r+1,0],i[r,0]);s.Quad(rim,o[r,cols-1],i[r,cols-1],i[r+1,cols-1],o[r+1,cols-1]);}
        }
        /// <summary>The mask's outer z at (x, y) in the head bone's frame: the grid's column at |x| (linear between columns), its
        /// rows by their y there.</summary>
        public static float MaskSurface(float x,float y)
        {
            x=Math.Abs(x);int n=MaskX.Length,c=0;while(c+2<n&&x>MaskX[c+1])c++;float u=MathUtil.Clamp((x-MaskX[c])/(MaskX[c+1]-MaskX[c]),0,1);
            float At(float[] g,int r)=>g[r*n+c]+(g[r*n+c+1]-g[r*n+c])*u;
            int rows=MaskY.Length/n,k=0;while(k+2<rows&&y>At(MaskY,k+1))k++;
            float y0=At(MaskY,k),y1=At(MaskY,k+1),v=MathUtil.Clamp((y-y0)/Math.Max(1e-5f,y1-y0),0,1);return At(MaskZ,k)+(At(MaskZ,k+1)-At(MaskZ,k))*v;
        }

        // ---- the back emblem ----
        // The concept sheet's glowing emblem on the back (a smiley with X eyes; every model pilot wears it, as the concept's
        // default outfit does), our own: flat strokes in the accent, in its own
        // frame (x across, y up, z out of the back) and placed on the chest's model bone (spine_05) by EmblemAt/EmblemTurn,
        // measured on the baked hoodie: the upper back 7 cm below the bone, about 4 mm off the cloth, tilted with it.
        /// <summary>The emblem's radius, its strokes' width, and its centre and tilt on the chest bone (the bone's frame: x down
        /// the spine, y out of the back, z across).</summary>
        public const float EmblemRadius=.042f,EmblemStroke=.0045f,EmblemDown=.07f,EmblemOut=.1075f,EmblemSlope=.056f;
        private static readonly WeaponPaint EmblemGlow=WeaponPaint.Accent(2.0f,.4f);
        /// <summary>The emblem: a ring, two X eyes and a smile.</summary>
        public static WeaponMesh Emblem()
        {
            var mesh=new WeaponMesh{Spec="pilot-emblem"};var s=new WeaponSculpt(mesh,0);s.Into(0);float w=EmblemStroke*.5f;
            // A stroke from a to b in the emblem's plane, facing +z.
            void Stroke(Vector2 a,Vector2 b){var d=Vector2.Normalize(b-a);var n=new Vector2(-d.Y,d.X)*w;var c=(a+b)*.5f;var u=(b-a)*.5f;s.Decal(EmblemGlow,new Vector3(c,0),new Vector3(u,0),new Vector3(n,0),.0008f);}
            void Arc(Vector2 c,float r,float from,float to,int segments)
            {
                for(int k=0;k<segments;k++)
                {
                    float a0=from+(to-from)*k/segments,a1=from+(to-from)*(k+1)/segments;
                    Stroke(c+new Vector2((float)Math.Cos(a0),(float)Math.Sin(a0))*r,c+new Vector2((float)Math.Cos(a1),(float)Math.Sin(a1))*r);
                }
            }
            Arc(Vector2.Zero,EmblemRadius-w,0,(float)Math.PI*2,28);
            float e=.0075f;
            for(int side=-1;side<=1;side+=2){var c=new Vector2(side*.0155f,.011f);Stroke(c+new Vector2(-e,-e),c+new Vector2(e,e));Stroke(c+new Vector2(-e,e),c+new Vector2(e,-e));}
            Arc(new Vector2(0,.006f),.024f,(float)(Math.PI*1.18),(float)(Math.PI*1.82),10);
            return mesh;
        }

        // ---- states ----
        /// <summary>Health share under which the core flickers, the flicker's slot length (s) and the share of slots it drops in at
        /// zero health; the slow pulse's period and depth; the flare as the body appears (its rise, decay and peak).</summary>
        public const float LowHealth=.3f,FlickerSlot=1/14f,FlickerMost=.6f,PulsePeriod=2.6f,PulseDepth=.18f,IgniteRise=.35f,IgniteDecay=.25f,IgnitePeak=.8f;
        /// <summary>The core's glow multiplier at presentation time `time` for a pilot at `health` (0..1 of its maximum), `lit`
        /// seconds after its body appeared (negative: long lit), seeded per pilot.</summary>
        public static float CoreGlow(float time,float health,float lit,int seed)
        {
            if(!MathUtil.Finite(time))time=0;float h=MathUtil.Finite(health)?MathUtil.Clamp(health,0,1):1;
            float pulse=1-PulseDepth*.5f+PulseDepth*.5f*(float)Math.Sin(Math.PI*2*time/PulsePeriod+(seed&255)*.0245f);
            float ignite=1;if(MathUtil.Finite(lit)&&lit>=0){float rise=MathUtil.Smooth(lit/IgniteRise);ignite=rise*(1+IgnitePeak*(float)Math.Exp(-Math.Max(0,lit-IgniteRise)/IgniteDecay));}
            float flicker=1;
            if(h<LowHealth)
            {
                int slot=(int)Math.Floor(time/FlickerSlot);float chance=FlickerMost*(LowHealth-h)/LowHealth;
                if(Hash(seed,slot)<chance)flicker=.3f+.25f*Hash(seed,slot+7919);
            }
            return pulse*ignite*flicker;
        }
        private static float Hash(int seed,int k){unchecked{uint x=(uint)seed*0x9e3779b1u^(uint)k*0x85ebca6bu;x^=x>>15;x*=0x2c1b3c6du;x^=x>>12;x*=0x297a2d39u;x^=x>>15;return (x&0xffffff)/16777216f;}}

        /// <summary>The device this frame: the lid's opening (0 shut .. 1 open), whether the chip shows in the bay, the bay's glow
        /// (its level, and whether it takes the chip's colour, else the accent), the lens's level (and whether in the chip's
        /// colour) and the strips' level.</summary>
        public struct DeviceState{public float Lid,Bay,Lens,Strips;public bool Chip,BayChip,LensChip;}
        /// <summary>Seconds over which the lens keeps the accepted chip's colour after the pulse (the "activated" glow).</summary>
        public const float ActiveSeconds=1.5f;
        /// <summary>
        /// The device for a chip-select look of weight `look` (0..1: the hologram pose's), a wrist plant `plant` seconds old (negative
        /// or NaN: none), and `sinceSeat` seconds since the last plant seated its chip (NaN: none).
        /// </summary>
        public static DeviceState Device(float look,float plant,float sinceSeat,float time)
        {
            var d=new DeviceState();float w=MathUtil.Finite(look)?MathUtil.Clamp(look,0,1):0;bool planting=NativeChipPlant.Active(plant);
            d.Lid=MathUtil.Smooth(w);d.Strips=1+.5f*w;
            // The chip shows once it has reached the bay (the plant's carry end) and while the lid is still open over it.
            d.Chip=planting&&plant>=NativeDraftPlant.CarryEnd&&d.Lid>.02f;
            float pulse=planting?NativeDraftPlant.Pulse(plant):-1;
            if(pulse>=0){d.BayChip=true;d.Bay=1.2f+2.2f*(1-pulse)*(1-pulse);}
            else if(planting&&plant>=NativeDraftPlant.CarryEnd){d.BayChip=true;d.Bay=1.2f;}
            else d.Bay=w>0?.55f+.15f*(float)Math.Sin(time*5.2f):0;
            // The lens: the accent at rest, the chip's colour while it is accepted and decaying after.
            float since=MathUtil.Finite(sinceSeat)?sinceSeat:float.PositiveInfinity;float pulseLen=NativeDraftPlant.PulseEnd-NativeDraftPlant.CarryEnd;
            if(since>=0&&since<pulseLen+ActiveSeconds){d.LensChip=true;d.Lens=since<pulseLen?1.6f:.6f+1.0f*(1-MathUtil.Smooth((since-pulseLen)/ActiveSeconds));}
            else d.Lens=.6f;
            return d;
        }
    }
}
